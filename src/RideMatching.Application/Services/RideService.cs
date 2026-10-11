using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RideMatching.Application.Common;
using RideMatching.Application.DTOs;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Entities;
using RideMatching.Domain.Enums;
using RideMatching.Domain.Exceptions;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Application.Services;

public class RideService : IRideService
{
    private readonly IApplicationDbContext _context;
    private readonly IRideMatchingQueue _matchingQueue;
    private readonly IDriverGeoCache _geoCache;
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<RideService> _logger;

    private static readonly RideStatus[] ActiveStatuses =
    [
        RideStatus.Requested,
        RideStatus.Matching,
        RideStatus.OfferPending,
        RideStatus.Accepted,
        RideStatus.DriverArriving,
        RideStatus.InProgress
    ];

    public RideService(
        IApplicationDbContext context,
        IRideMatchingQueue matchingQueue,
        IDriverGeoCache geoCache,
        IRealtimeNotifier notifier,
        ILogger<RideService> logger)
    {
        _context = context;
        _matchingQueue = matchingQueue;
        _geoCache = geoCache;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<RideDto> RequestRideAsync(CreateRideRequestDto request, CancellationToken cancellationToken = default)
    {
        var rider = await _context.Riders.FirstOrDefaultAsync(r => r.Id == request.RiderId, cancellationToken);
        if (rider == null)
            throw new RideDomainException($"Rider {request.RiderId} was not found.");

        // Check if rider already has an active ride
        var existingActive = await _context.Rides
            .Include(r => r.Driver)
            .FirstOrDefaultAsync(r => r.RiderId == request.RiderId && r.Status < RideStatus.Completed, cancellationToken);

        if (existingActive != null)
        {
            _logger.LogInformation("Rider {RiderId} already has an active ride {RideId}.", request.RiderId, existingActive.Id);
            return existingActive.ToDto(rider.Name, existingActive.Driver?.Name, existingActive.Driver?.VehicleModel, existingActive.Driver?.LicensePlate);
        }

        var pickup = new GeoLocation(request.PickupLocation.Latitude, request.PickupLocation.Longitude);
        var dropoff = new GeoLocation(request.DropoffLocation.Latitude, request.DropoffLocation.Longitude);

        var ride = new Ride(request.RiderId, pickup, request.PickupAddress, dropoff, request.DropoffAddress);
        _context.Rides.Add(ride);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Ride requested: {RideId} for Rider: {RiderId}. Estimated Distance: {Distance} km, Fare: ${Fare}",
            ride.Id, rider.Id, ride.EstimatedDistanceKm, ride.EstimatedFare);

        // Enqueue to asynchronous background matching channel
        await _matchingQueue.EnqueueAsync(ride.Id, cancellationToken);

        var rideDto = ride.ToDto(rider.Name);
        await _notifier.NotifyRiderRideUpdatedAsync(rider.Id, rideDto);

        return rideDto;
    }

    public async Task<RideDto?> GetRideAsync(Guid rideId, CancellationToken cancellationToken = default)
    {
        var ride = await _context.Rides
            .Include(r => r.Rider)
            .Include(r => r.Driver)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rideId, cancellationToken);

        if (ride == null) return null;

        return ride.ToDto(
            ride.Rider?.Name ?? "",
            ride.Driver?.Name,
            ride.Driver?.VehicleModel,
            ride.Driver?.LicensePlate);
    }

    public async Task<RideDto?> GetRiderActiveRideAsync(Guid riderId, CancellationToken cancellationToken = default)
    {
        var ride = await _context.Rides
            .Include(r => r.Rider)
            .Include(r => r.Driver)
            .AsNoTracking()
            .Where(r => r.RiderId == riderId && r.Status < RideStatus.Completed)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (ride == null) return null;

        return ride.ToDto(
            ride.Rider?.Name ?? "",
            ride.Driver?.Name,
            ride.Driver?.VehicleModel,
            ride.Driver?.LicensePlate);
    }

    public async Task<RideDto?> GetDriverActiveRideAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var ride = await _context.Rides
            .Include(r => r.Rider)
            .Include(r => r.Driver)
            .AsNoTracking()
            .Where(r => (r.DriverId == driverId || r.CurrentOfferedDriverId == driverId) && r.Status < RideStatus.Completed)
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (ride == null) return null;

        return ride.ToDto(
            ride.Rider?.Name ?? "",
            ride.Driver?.Name,
            ride.Driver?.VehicleModel,
            ride.Driver?.LicensePlate);
    }

    public async Task<RideDto> CancelRideAsync(Guid rideId, string reason, CancellationToken cancellationToken = default)
    {
        var ride = await _context.Rides
            .Include(r => r.Rider)
            .Include(r => r.Driver)
            .FirstOrDefaultAsync(r => r.Id == rideId, cancellationToken);

        if (ride == null)
            throw new RideDomainException($"Ride {rideId} was not found.");

        Guid? affectedDriverId = ride.DriverId ?? ride.CurrentOfferedDriverId;
        ride.Cancel(reason);

        if (affectedDriverId.HasValue)
        {
            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == affectedDriverId.Value, cancellationToken);
            if (driver != null)
            {
                driver.CompleteRide(rideId);
                _geoCache.UpdateStatus(driver.Id, DriverStatus.Available);
            }

            await _notifier.NotifyDriverOfferRevokedAsync(affectedDriverId.Value, rideId, reason);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var rideDto = ride.ToDto(
            ride.Rider?.Name ?? "",
            ride.Driver?.Name,
            ride.Driver?.VehicleModel,
            ride.Driver?.LicensePlate);

        await _notifier.NotifyRiderRideUpdatedAsync(ride.RiderId, rideDto);
        _logger.LogInformation("Ride {RideId} was cancelled with reason: {Reason}", rideId, reason);

        return rideDto;
    }

    public FareEstimateDto GetFareEstimate(GeoLocation pickup, GeoLocation dropoff)
    {
        double distanceKm = pickup.DistanceToKm(dropoff);
        decimal fare = Ride.CalculateFare(distanceKm);
        int estimatedMinutes = (int)Math.Max(3, Math.Round(distanceKm * 2.2)); // Approx 27 km/h urban speed
        return new FareEstimateDto(distanceKm, fare, estimatedMinutes);
    }

    public async Task<IReadOnlyList<RideDto>> GetRecentRidesAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var rides = await _context.Rides
            .Include(r => r.Rider)
            .Include(r => r.Driver)
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rides.Select(r => r.ToDto(
            r.Rider?.Name ?? "",
            r.Driver?.Name,
            r.Driver?.VehicleModel,
            r.Driver?.LicensePlate)).ToList();
    }

    public async Task<SystemMetricsDto> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
    {
        var onlineDrivers = _geoCache.GetAllOnlineDrivers().Count;
        var availableDrivers = _geoCache.FindNearestAvailableDrivers(new GeoLocation(0, 0), double.MaxValue, 1000).Count;

        var activeRidesCount = await _context.Rides
            .CountAsync(r => r.Status < RideStatus.Completed, cancellationToken);

        var completedRidesCount = await _context.Rides
            .CountAsync(r => r.Status == RideStatus.Completed, cancellationToken);

        var pendingRequestsCount = await _context.Rides
            .CountAsync(r => r.Status == RideStatus.Requested || r.Status == RideStatus.Matching, cancellationToken);

        return new SystemMetricsDto(
            onlineDrivers,
            availableDrivers,
            activeRidesCount,
            completedRidesCount,
            pendingRequestsCount);
    }
}
