using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RideMatching.Application.Common;
using RideMatching.Application.DTOs;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Enums;
using RideMatching.Infrastructure.Data;

namespace RideMatching.Infrastructure.Services;

public class RideMatchingEngine : IRideMatchingEngine
{
    private readonly AppDbContext _context;
    private readonly IDriverGeoCache _geoCache;
    private readonly IRideMatchingQueue _queue;
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<RideMatchingEngine> _logger;

    private const int MaxOfferAttempts = 5;
    private static readonly TimeSpan DriverOfferTimeout = TimeSpan.FromSeconds(20);

    public RideMatchingEngine(
        AppDbContext context,
        IDriverGeoCache geoCache,
        IRideMatchingQueue queue,
        IRealtimeNotifier notifier,
        ILogger<RideMatchingEngine> logger)
    {
        _context = context;
        _geoCache = geoCache;
        _queue = queue;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<bool> MatchRideAsync(Guid rideId, CancellationToken cancellationToken = default)
    {
        var ride = await _context.Rides
            .Include(r => r.Rider)
            .Include(r => r.Offers)
            .FirstOrDefaultAsync(r => r.Id == rideId, cancellationToken);

        if (ride == null)
        {
            _logger.LogWarning("Matching aborted: Ride {RideId} not found.", rideId);
            return false;
        }

        if (ride.Status != RideStatus.Requested && ride.Status != RideStatus.Matching)
        {
            _logger.LogInformation("Ride {RideId} is already in state {Status}, skipping matching.", rideId, ride.Status);
            return false;
        }

        if (ride.Status == RideStatus.Requested)
        {
            ride.MarkMatching();
            await _context.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyRiderRideUpdatedAsync(ride.RiderId, ride.ToDto(ride.Rider?.Name ?? ""));
        }

        // Collect drivers who have already been offered this ride
        var excludedDriverIds = ride.Offers
            .Select(o => o.DriverId)
            .ToHashSet();

        // Progressive radius expansion: 5km -> 10km -> 20km
        double[] searchRadii = [5.0, 10.0, 20.0];
        IReadOnlyList<DriverCandidateDto> candidates = Array.Empty<DriverCandidateDto>();

        foreach (var radius in searchRadii)
        {
            candidates = _geoCache.FindNearestAvailableDrivers(ride.PickupLocation, radius, 10, excludedDriverIds);
            if (candidates.Count > 0)
            {
                _logger.LogInformation("Found {Count} candidate driver(s) within {Radius} km for Ride {RideId}.", candidates.Count, radius, rideId);
                break;
            }
        }

        if (candidates.Count == 0)
        {
            if (ride.OfferAttemptCount >= MaxOfferAttempts)
            {
                _logger.LogWarning("No drivers found after {Attempts} attempts for Ride {RideId}. Marking NoDriversAvailable.", ride.OfferAttemptCount, rideId);
                ride.MarkNoDriversFound();
                await _context.SaveChangesAsync(cancellationToken);
                await _notifier.NotifyRiderRideUpdatedAsync(ride.RiderId, ride.ToDto(ride.Rider?.Name ?? ""));
                return false;
            }

            _logger.LogInformation("No drivers currently in radius for Ride {RideId}. Will retry...", rideId);
            return false;
        }

        // Attempt to atomically offer ride to the nearest available driver
        foreach (var candidate in candidates)
        {
            try
            {
                var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == candidate.DriverId, cancellationToken);
                if (driver == null || driver.Status != DriverStatus.Available)
                {
                    continue;
                }

                // Atomic reservation: Driver status -> Offered, Ride status -> OfferPending
                driver.OfferRide(ride.Id);
                var offer = ride.DispatchOfferToDriver(driver.Id, DriverOfferTimeout);
                _context.RideOffers.Add(offer);

                await _context.SaveChangesAsync(cancellationToken);

                // Update cache status to Offered
                _geoCache.UpdateStatus(driver.Id, DriverStatus.Offered);

                _logger.LogInformation("Dispatched offer for Ride {RideId} to Driver {DriverId} ({DriverName}, {Distance:F2} km away).",
                    ride.Id, driver.Id, driver.Name, candidate.DistanceKm);

                // Send real-time notification to Driver
                var offerDto = new RideOfferDto(
                    ride.Id,
                    driver.Id,
                    ride.RiderId,
                    ride.Rider?.Name ?? "Rider",
                    new LocationDto(ride.PickupLocation.Latitude, ride.PickupLocation.Longitude),
                    ride.PickupAddress,
                    new LocationDto(ride.DropoffLocation.Latitude, ride.DropoffLocation.Longitude),
                    ride.DropoffAddress,
                    candidate.DistanceKm,
                    candidate.EstimatedEtaMinutes,
                    ride.EstimatedFare,
                    ride.OfferExpiresAtUtc!.Value,
                    (int)DriverOfferTimeout.TotalSeconds);

                await _notifier.NotifyDriverRideOfferAsync(driver.Id, offerDto);

                // Notify Rider of updated status
                await _notifier.NotifyRiderRideUpdatedAsync(ride.RiderId, ride.ToDto(ride.Rider?.Name ?? ""));

                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict: Driver {DriverId} was grabbed by another request concurrently. Falling back to next driver candidate.", candidate.DriverId);
                // Detach or clear entries to retry next candidate
                _context.ChangeTracker.Clear();
                // Reload ride to fresh state
                ride = await _context.Rides
                    .Include(r => r.Rider)
                    .Include(r => r.Offers)
                    .FirstOrDefaultAsync(r => r.Id == rideId, cancellationToken);

                if (ride == null || ride.Status != RideStatus.Matching)
                    return false;
            }
        }

        return false;
    }

    public async Task CheckAndProcessExpiredOffersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var expiredRides = await _context.Rides
            .Include(r => r.Offers)
            .Where(r => r.Status == RideStatus.OfferPending && r.OfferExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);

        foreach (var ride in expiredRides)
        {
            var driverId = ride.CurrentOfferedDriverId;
            if (driverId.HasValue)
            {
                var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == driverId.Value, cancellationToken);
                if (driver != null)
                {
                    driver.ReleaseFromOffer(ride.Id);
                    _geoCache.UpdateStatus(driver.Id, DriverStatus.Available);
                }

                await _notifier.NotifyDriverOfferRevokedAsync(driverId.Value, ride.Id, "Offer expired without response");
            }

            ride.ExpireOffer();
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Offer expired for Ride {RideId}. Re-queueing ride for next available driver.", ride.Id);

            // Re-enqueue ride for next match
            await _queue.EnqueueAsync(ride.Id, cancellationToken);
        }
    }
}
