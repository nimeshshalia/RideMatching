using RideMatching.Application.DTOs;
using RideMatching.Domain.Enums;

namespace RideMatching.Application.Interfaces;

public interface IRealtimeNotifier
{
    Task NotifyDriverRideOfferAsync(Guid driverId, RideOfferDto offer);
    Task NotifyDriverOfferRevokedAsync(Guid driverId, Guid rideId, string reason);
    Task NotifyRiderRideUpdatedAsync(Guid riderId, RideDto ride);
    Task NotifyDriverLocationUpdatedAsync(Guid rideId, Guid driverId, LocationDto location, double? bearing);
    Task BroadcastDriverLocationAsync(Guid driverId, LocationDto location, DriverStatus status);
    Task BroadcastSystemMetricsAsync(SystemMetricsDto metrics);
}
