using RideMatching.Domain.Common;
using RideMatching.Domain.Enums;
using RideMatching.Domain.Exceptions;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Domain.Entities;

public class Ride : BaseEntity<Guid>
{
    public Guid RiderId { get; private set; }
    public Rider? Rider { get; private set; }

    public Guid? DriverId { get; private set; }
    public Driver? Driver { get; private set; }

    public double PickupLatitude { get; private set; }
    public double PickupLongitude { get; private set; }
    public GeoLocation PickupLocation => new(PickupLatitude, PickupLongitude);
    public string PickupAddress { get; private set; } = string.Empty;

    public double DropoffLatitude { get; private set; }
    public double DropoffLongitude { get; private set; }
    public GeoLocation DropoffLocation => new(DropoffLatitude, DropoffLongitude);
    public string DropoffAddress { get; private set; } = string.Empty;

    public RideStatus Status { get; private set; } = RideStatus.Requested;

    public decimal EstimatedFare { get; private set; }
    public double EstimatedDistanceKm { get; private set; }

    public DateTime? MatchedAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public string? CancellationReason { get; private set; }

    // Concurrency and Offer state tracking
    public Guid? CurrentOfferedDriverId { get; private set; }
    public DateTime? OfferExpiresAtUtc { get; private set; }
    public int OfferAttemptCount { get; private set; }
    public string ConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString("N");

    // Offers audit
    public List<RideOffer> Offers { get; private set; } = new();

    protected Ride() { }

    public Ride(
        Guid riderId,
        GeoLocation pickupLocation,
        string pickupAddress,
        GeoLocation dropoffLocation,
        string dropoffAddress)
    {
        Id = Guid.NewGuid();
        RiderId = riderId;
        ArgumentNullException.ThrowIfNull(pickupLocation);
        ArgumentNullException.ThrowIfNull(dropoffLocation);
        PickupLatitude = pickupLocation.Latitude;
        PickupLongitude = pickupLocation.Longitude;
        DropoffLatitude = dropoffLocation.Latitude;
        DropoffLongitude = dropoffLocation.Longitude;
        PickupAddress = pickupAddress ?? string.Empty;
        DropoffAddress = dropoffAddress ?? string.Empty;
        Status = RideStatus.Requested;
        CreatedAtUtc = DateTime.UtcNow;

        EstimatedDistanceKm = pickupLocation.DistanceToKm(dropoffLocation);
        EstimatedFare = CalculateFare(EstimatedDistanceKm);
    }

    public static decimal CalculateFare(double distanceKm)
    {
        const decimal baseFare = 3.50m;
        const decimal perKmRate = 1.85m;
        const decimal minimumFare = 5.00m;

        decimal calculated = baseFare + (decimal)distanceKm * perKmRate;
        return Math.Max(minimumFare, Math.Round(calculated, 2));
    }

    public void MarkMatching()
    {
        if (Status != RideStatus.Requested && Status != RideStatus.Matching && Status != RideStatus.OfferPending)
            throw new InvalidStateTransitionException($"Cannot mark ride {Id} as Matching from status {Status}.");

        Status = RideStatus.Matching;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrency();
    }

    public RideOffer DispatchOfferToDriver(Guid driverId, TimeSpan offerDuration)
    {
        if (Status != RideStatus.Requested && Status != RideStatus.Matching && Status != RideStatus.OfferPending)
            throw new InvalidStateTransitionException($"Cannot dispatch offer for ride {Id} in status {Status}.");

        Status = RideStatus.OfferPending;
        CurrentOfferedDriverId = driverId;
        OfferExpiresAtUtc = DateTime.UtcNow.Add(offerDuration);
        OfferAttemptCount++;
        UpdatedAtUtc = DateTime.UtcNow;

        var offer = new RideOffer(Id, driverId, OfferExpiresAtUtc.Value);
        Offers.Add(offer);
        BumpConcurrency();
        return offer;
    }

    public void AcceptByDriver(Guid driverId)
    {
        if (Status != RideStatus.OfferPending)
            throw new InvalidStateTransitionException($"Cannot accept ride {Id} because status is {Status}, not OfferPending.");

        if (CurrentOfferedDriverId != driverId)
            throw new InvalidStateTransitionException($"Offer was issued to driver {CurrentOfferedDriverId}, but {driverId} tried to accept.");

        Status = RideStatus.Accepted;
        DriverId = driverId;
        CurrentOfferedDriverId = null;
        OfferExpiresAtUtc = null;
        AcceptedAtUtc = DateTime.UtcNow;
        MatchedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;

        var activeOffer = Offers.LastOrDefault(o => o.DriverId == driverId && o.Status == OfferStatus.Pending);
        activeOffer?.Accept();

        BumpConcurrency();
    }

    public void RejectByDriver(Guid driverId)
    {
        if (CurrentOfferedDriverId == driverId)
        {
            var activeOffer = Offers.LastOrDefault(o => o.DriverId == driverId && o.Status == OfferStatus.Pending);
            activeOffer?.Reject();

            CurrentOfferedDriverId = null;
            OfferExpiresAtUtc = null;
            Status = RideStatus.Matching;
            UpdatedAtUtc = DateTime.UtcNow;
            BumpConcurrency();
        }
    }

    public void ExpireOffer()
    {
        if (Status == RideStatus.OfferPending && CurrentOfferedDriverId.HasValue)
        {
            var activeOffer = Offers.LastOrDefault(o => o.DriverId == CurrentOfferedDriverId.Value && o.Status == OfferStatus.Pending);
            activeOffer?.Expire();

            CurrentOfferedDriverId = null;
            OfferExpiresAtUtc = null;
            Status = RideStatus.Matching;
            UpdatedAtUtc = DateTime.UtcNow;
            BumpConcurrency();
        }
    }

    public void MarkDriverArriving()
    {
        if (Status != RideStatus.Accepted)
            throw new InvalidStateTransitionException($"Cannot mark DriverArriving from status {Status}.");

        Status = RideStatus.DriverArriving;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrency();
    }

    public void StartTrip()
    {
        if (Status != RideStatus.Accepted && Status != RideStatus.DriverArriving)
            throw new InvalidStateTransitionException($"Cannot start trip from status {Status}.");

        Status = RideStatus.InProgress;
        StartedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrency();
    }

    public void CompleteTrip()
    {
        if (Status != RideStatus.InProgress)
            throw new InvalidStateTransitionException($"Cannot complete trip from status {Status}.");

        Status = RideStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrency();
    }

    public void Cancel(string reason)
    {
        if (Status is RideStatus.Completed or RideStatus.Cancelled)
            throw new InvalidStateTransitionException($"Ride {Id} is already {Status}.");

        Status = RideStatus.Cancelled;
        CancelledAtUtc = DateTime.UtcNow;
        CancellationReason = reason;
        CurrentOfferedDriverId = null;
        OfferExpiresAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrency();
    }

    public void MarkNoDriversFound()
    {
        Status = RideStatus.NoDriversAvailable;
        CurrentOfferedDriverId = null;
        OfferExpiresAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrency();
    }

    private void BumpConcurrency() => ConcurrencyStamp = Guid.NewGuid().ToString("N");
}
