using Microsoft.AspNetCore.SignalR;
using RideMatching.Application.DTOs;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Enums;
using RideMatching.Infrastructure.Hubs;

namespace RideMatching.Infrastructure.Services;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<RideHub> _hubContext;

    public SignalRRealtimeNotifier(IHubContext<RideHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyDriverRideOfferAsync(Guid driverId, RideOfferDto offer)
    {
        await _hubContext.Clients.Group($"driver_{driverId}").SendAsync("ReceiveRideOffer", offer);
    }

    public async Task NotifyDriverOfferRevokedAsync(Guid driverId, Guid rideId, string reason)
    {
        await _hubContext.Clients.Group($"driver_{driverId}").SendAsync("OfferRevoked", new { rideId, reason });
    }

    public async Task NotifyRiderRideUpdatedAsync(Guid riderId, RideDto ride)
    {
        await _hubContext.Clients.Group($"rider_{riderId}").SendAsync("RideUpdated", ride);

        if (ride.DriverId.HasValue)
        {
            await _hubContext.Clients.Group($"driver_{ride.DriverId.Value}").SendAsync("RideUpdated", ride);
        }

        // Broadcast to monitor dashboard
        await _hubContext.Clients.Group("monitors").SendAsync("RideUpdated", ride);
    }

    public async Task NotifyDriverLocationUpdatedAsync(Guid rideId, Guid driverId, LocationDto location, double? bearing)
    {
        await _hubContext.Clients.Group($"ride_{rideId}").SendAsync("DriverLocationUpdated", new { driverId, location, bearing });
        await _hubContext.Clients.Group("monitors").SendAsync("DriverLocationUpdated", new { driverId, location, bearing });
    }

    public async Task BroadcastDriverLocationAsync(Guid driverId, LocationDto location, DriverStatus status)
    {
        await _hubContext.Clients.Group("monitors").SendAsync("DriverStatusUpdated", new { driverId, location, status });
    }

    public async Task BroadcastSystemMetricsAsync(SystemMetricsDto metrics)
    {
        await _hubContext.Clients.Group("monitors").SendAsync("SystemMetricsUpdated", metrics);
    }
}
