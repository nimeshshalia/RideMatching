using RideMatching.Application.DTOs;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Application.Interfaces;

public interface IRideService
{
    Task<RideDto> RequestRideAsync(CreateRideRequestDto request, CancellationToken cancellationToken = default);
    Task<RideDto?> GetRideAsync(Guid rideId, CancellationToken cancellationToken = default);
    Task<RideDto?> GetRiderActiveRideAsync(Guid riderId, CancellationToken cancellationToken = default);
    Task<RideDto?> GetDriverActiveRideAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<RideDto> CancelRideAsync(Guid rideId, string reason, CancellationToken cancellationToken = default);
    FareEstimateDto GetFareEstimate(GeoLocation pickup, GeoLocation dropoff);
    Task<IReadOnlyList<RideDto>> GetRecentRidesAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<SystemMetricsDto> GetSystemMetricsAsync(CancellationToken cancellationToken = default);
}
