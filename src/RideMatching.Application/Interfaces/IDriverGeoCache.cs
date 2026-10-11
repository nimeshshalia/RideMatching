using RideMatching.Application.DTOs;
using RideMatching.Domain.Enums;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Application.Interfaces;

public interface IDriverGeoCache
{
    void UpdateDriver(Guid driverId, string name, string vehicleModel, string licensePlate, GeoLocation location, DriverStatus status);
    void UpdateLocation(Guid driverId, GeoLocation location);
    void UpdateStatus(Guid driverId, DriverStatus status);
    void RemoveDriver(Guid driverId);
    DriverLocationSnapshot? GetDriver(Guid driverId);
    IReadOnlyList<DriverCandidateDto> FindNearestAvailableDrivers(GeoLocation pickupLocation, double radiusKm, int limit, IReadOnlySet<Guid>? excludedDriverIds = null);
    IReadOnlyList<DriverCandidateDto> GetAllOnlineDrivers();
}
