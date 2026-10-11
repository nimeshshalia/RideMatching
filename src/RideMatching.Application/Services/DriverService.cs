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

public class DriverService : IDriverService
{
    private readonly IApplicationDbContext _context;
    private readonly IDriverGeoCache _geoCache;
    private readonly IRealtimeNotifier _notifier;
    private readonly IRideMatchingQueue _matchingQueue;
    private readonly ILogger<DriverService> _logger;

    public DriverService(
        IApplicationDbContext context,
        IDriverGeoCache geoCache,
        IRealtimeNotifier notifier,
        IRideMatchingQueue matchingQueue,
        ILogger<DriverService> logger)
    {
        _context = context;
        _geoCache = geoCache;
        _notifier = notifier;
        _matchingQueue = matchingQueue;
        _logger = logger;
    }

    public async Task<DriverDto> RegisterDriverAsync(CreateDriverRequestDto request, CancellationToken cancellationToken = default)
    {
        var driver = new Driver(request.Name, request.Email, request.Phone, request.VehicleModel, request.LicensePlate);
        _context.Drivers.Add(driver);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Driver registered successfully: {DriverId} ({Name})", driver.Id, driver.Name);
        return driver.ToDto();
    }

    public async Task<DriverDto?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers.AsNoTracking().FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);
        return driver?.ToDto();
    }

    public async Task<IReadOnlyList<DriverDto>> GetAllDriversAsync(CancellationToken cancellationToken = default)
    {
        var drivers = await _context.Drivers.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);
        return drivers.Select(d => d.ToDto()).ToList();
    }

    public async Task<DriverDto> SetDriverOnlineAsync(Guid driverId, GeoLocation initialLocation, CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);
        if (driver == null)
            throw new RideDomainException($"Driver {driverId} not found.");

        driver.GoOnline(initialLocation);
        await _context.SaveChangesAsync(cancellationToken);

        // Update high-speed in-memory spatial cache
        _geoCache.UpdateDriver(
            driver.Id,
            driver.Name,
            driver.VehicleModel,
            driver.LicensePlate,
            initialLocation,
            DriverStatus.Available);

        var dto = driver.ToDto();
        await _notifier.BroadcastDriverLocationAsync(driver.Id, dto.CurrentLocation!, DriverStatus.Available);
        _logger.LogInformation("Driver {DriverId} went online at {Location}", driverId, initialLocation);
        return dto;
    }

    public async Task<DriverDto> SetDriverOfflineAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);
        if (driver == null)
            throw new RideDomainException($"Driver {driverId} not found.");

        driver.GoOffline();
        await _context.SaveChangesAsync(cancellationToken);

        _geoCache.RemoveDriver(driverId);
        var dto = driver.ToDto();
        if (dto.CurrentLocation != null)
        {
            await _notifier.BroadcastDriverLocationAsync(driver.Id, dto.CurrentLocation, DriverStatus.Offline);
        }

        _logger.LogInformation("Driver {DriverId} went offline", driverId);
        return dto;
    }

    public async Task<bool> UpdateLocationAsync(
        Guid driverId,
        GeoLocation location,
        double? speedKmh = null,
        double? bearing = null,
        CancellationToken cancellationToken = default)
    {
        var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);
        if (driver == null)
            return false;

        // Calculate bearing automatically if not provided and previous location exists
        if (!bearing.HasValue && driver.CurrentLocation != null && driver.CurrentLocation.DistanceToKm(location) > 0.005)
        {
            bearing = driver.CurrentLocation.BearingTo(location);
        }

        driver.UpdateLocation(location);

        // Record telemetry log
        _context.DriverLocationHistories.Add(new DriverLocationHistory(driverId, location, speedKmh, bearing));
        await _context.SaveChangesAsync(cancellationToken);

        // Update real-time geo cache immediately
        _geoCache.UpdateLocation(driverId, location);

        var locDto = new LocationDto(location.Latitude, location.Longitude);
        await _notifier.BroadcastDriverLocationAsync(driverId, locDto, driver.Status);

        // If driver is currently serving an active ride, notify the rider with the updated live location and bearing
        if (driver.CurrentRideId.HasValue)
        {
            await _notifier.NotifyDriverLocationUpdatedAsync(driver.CurrentRideId.Value, driverId, locDto, bearing);
        }

        return true;
    }

    public async Task<bool> AcceptRideOfferAsync(Guid driverId, Guid rideId, CancellationToken cancellationToken = default)
    {
        var ride = await _context.Rides
            .Include(r => r.Rider)
            .Include(r => r.Offers)
            .FirstOrDefaultAsync(r => r.Id == rideId, cancellationToken);

        if (ride == null)
            throw new RideDomainException($"Ride {rideId} was not found.");

        var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);
        if (driver == null)
            throw new RideDomainException($"Driver {driverId} was not found.");

        // Check if offer expired before acceptance
        if (ride.OfferExpiresAtUtc.HasValue && DateTime.UtcNow > ride.OfferExpiresAtUtc.Value)
        {
            throw new RideDomainException("Ride offer has already expired.");
        }

        // Apply domain state transitions with concurrency protection
        ride.AcceptByDriver(driverId);
        driver.AcceptRide(rideId);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict while driver {DriverId} attempted to accept ride {RideId}.", driverId, rideId);
            throw new ConcurrencyConflictException("This ride has already been accepted or modified by another process.");
        }

        // Update spatial cache driver status to Busy
        _geoCache.UpdateStatus(driverId, DriverStatus.Busy);

        // Real-time notification to Rider
        var rideDto = ride.ToDto(ride.Rider?.Name ?? "Rider", driver.Name, driver.VehicleModel, driver.LicensePlate);
        await _notifier.NotifyRiderRideUpdatedAsync(ride.RiderId, rideDto);

        _logger.LogInformation("Driver {DriverId} successfully accepted ride {RideId}", driverId, rideId);
        return true;
    }

    public async Task<bool> RejectRideOfferAsync(Guid driverId, Guid rideId, CancellationToken cancellationToken = default)
    {
        var ride = await _context.Rides
            .Include(r => r.Offers)
            .FirstOrDefaultAsync(r => r.Id == rideId, cancellationToken);

        if (ride == null) return false;

        var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);
        if (driver != null)
        {
            driver.ReleaseFromOffer(rideId);
            _geoCache.UpdateStatus(driverId, DriverStatus.Available);
        }

        ride.RejectByDriver(driverId);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Driver {DriverId} rejected ride {RideId}. Re-queueing ride for next nearest driver.", driverId, rideId);

        // Re-enqueue ride to match another driver
        await _matchingQueue.EnqueueAsync(rideId, cancellationToken);
        return true;
    }

    public async Task<RideDto> UpdateRideStatusAsync(Guid driverId, Guid rideId, RideStatus newStatus, CancellationToken cancellationToken = default)
    {
        var ride = await _context.Rides
            .Include(r => r.Rider)
            .Include(r => r.Driver)
            .FirstOrDefaultAsync(r => r.Id == rideId, cancellationToken);

        if (ride == null)
            throw new RideDomainException($"Ride {rideId} was not found.");

        if (ride.DriverId != driverId)
            throw new RideDomainException($"Driver {driverId} is not assigned to ride {rideId}.");

        var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId, cancellationToken);

        switch (newStatus)
        {
            case RideStatus.DriverArriving:
                ride.MarkDriverArriving();
                break;

            case RideStatus.InProgress:
                ride.StartTrip();
                break;

            case RideStatus.Completed:
                ride.CompleteTrip();
                if (driver != null)
                {
                    driver.CompleteRide(rideId);
                    _geoCache.UpdateStatus(driverId, DriverStatus.Available);
                }
                break;

            case RideStatus.Cancelled:
                ride.Cancel("Cancelled by driver");
                if (driver != null)
                {
                    driver.CompleteRide(rideId);
                    _geoCache.UpdateStatus(driverId, DriverStatus.Available);
                }
                break;

            default:
                throw new InvalidStateTransitionException($"Unsupported status transition to {newStatus}.");
        }

        await _context.SaveChangesAsync(cancellationToken);

        var rideDto = ride.ToDto(
            ride.Rider?.Name ?? "",
            ride.Driver?.Name,
            ride.Driver?.VehicleModel,
            ride.Driver?.LicensePlate);

        await _notifier.NotifyRiderRideUpdatedAsync(ride.RiderId, rideDto);
        _logger.LogInformation("Ride {RideId} transitioned to {Status} by driver {DriverId}", rideId, newStatus, driverId);

        return rideDto;
    }
}
