using RideMatching.Application.DTOs;
using RideMatching.Domain.Enums;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Application.Interfaces;

public interface IDriverService
{
    Task<DriverDto> RegisterDriverAsync(CreateDriverRequestDto request, CancellationToken cancellationToken = default);
    Task<DriverDto?> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DriverDto>> GetAllDriversAsync(CancellationToken cancellationToken = default);
    Task<DriverDto> SetDriverOnlineAsync(Guid driverId, GeoLocation initialLocation, CancellationToken cancellationToken = default);
    Task<DriverDto> SetDriverOfflineAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<bool> UpdateLocationAsync(Guid driverId, GeoLocation location, double? speedKmh = null, double? bearing = null, CancellationToken cancellationToken = default);
    Task<bool> AcceptRideOfferAsync(Guid driverId, Guid rideId, CancellationToken cancellationToken = default);
    Task<bool> RejectRideOfferAsync(Guid driverId, Guid rideId, CancellationToken cancellationToken = default);
    Task<RideDto> UpdateRideStatusAsync(Guid driverId, Guid rideId, RideStatus newStatus, CancellationToken cancellationToken = default);
}
