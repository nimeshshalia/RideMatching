using RideMatching.Domain.Common;
using RideMatching.Domain.Enums;
using RideMatching.Domain.Exceptions;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Domain.Entities;

public class Driver : BaseEntity<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string VehicleModel { get; private set; } = string.Empty;
    public string LicensePlate { get; private set; } = string.Empty;
    public DriverStatus Status { get; private set; } = DriverStatus.Offline;

    public double? CurrentLatitude { get; private set; }
    public double? CurrentLongitude { get; private set; }
    public GeoLocation? CurrentLocation => CurrentLatitude.HasValue && CurrentLongitude.HasValue
        ? new GeoLocation(CurrentLatitude.Value, CurrentLongitude.Value)
        : null;

    public DateTime? LastLocationUpdateUtc { get; private set; }
    public double Rating { get; private set; } = 5.0;
    public Guid? CurrentRideId { get; private set; }

    /// <summary>
    /// Concurrency stamp updated on every state transition to ensure atomic test-and-set locks.
    /// </summary>
    public string ConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString("N");

    // EF Core constructor
    protected Driver() { }

    public Driver(string name, string email, string phone, string vehicleModel, string licensePlate)
    {
        Id = Guid.NewGuid();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Phone = phone ?? throw new ArgumentNullException(nameof(phone));
        VehicleModel = vehicleModel ?? throw new ArgumentNullException(nameof(vehicleModel));
        LicensePlate = licensePlate ?? throw new ArgumentNullException(nameof(licensePlate));
        Status = DriverStatus.Offline;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void GoOnline(GeoLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        CurrentLatitude = location.Latitude;
        CurrentLongitude = location.Longitude;
        Status = DriverStatus.Available;
        LastLocationUpdateUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrencyStamp();
    }

    public void GoOffline()
    {
        if (Status == DriverStatus.Busy)
            throw new InvalidStateTransitionException("Cannot go offline while in the middle of an active ride.");

        Status = DriverStatus.Offline;
        CurrentRideId = null;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrencyStamp();
    }

    public void UpdateLocation(GeoLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        CurrentLatitude = location.Latitude;
        CurrentLongitude = location.Longitude;
        LastLocationUpdateUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void OfferRide(Guid rideId)
    {
        if (Status != DriverStatus.Available)
            throw new InvalidStateTransitionException($"Driver {Id} is {Status}, cannot receive ride offer.");

        Status = DriverStatus.Offered;
        CurrentRideId = rideId;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrencyStamp();
    }

    public void AcceptRide(Guid rideId)
    {
        if (CurrentRideId != rideId)
            throw new InvalidStateTransitionException($"Driver {Id} cannot accept ride {rideId}; current offered ride is {CurrentRideId}.");

        Status = DriverStatus.Busy;
        UpdatedAtUtc = DateTime.UtcNow;
        BumpConcurrencyStamp();
    }

    public void ReleaseFromOffer(Guid rideId)
    {
        if (CurrentRideId == rideId && Status == DriverStatus.Offered)
        {
            Status = DriverStatus.Available;
            CurrentRideId = null;
            UpdatedAtUtc = DateTime.UtcNow;
            BumpConcurrencyStamp();
        }
    }

    public void CompleteRide(Guid rideId)
    {
        if (CurrentRideId == rideId)
        {
            Status = DriverStatus.Available;
            CurrentRideId = null;
            UpdatedAtUtc = DateTime.UtcNow;
            BumpConcurrencyStamp();
        }
    }

    private void BumpConcurrencyStamp() => ConcurrencyStamp = Guid.NewGuid().ToString("N");
}
