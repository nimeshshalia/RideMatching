using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace RideMatching.Infrastructure.Hubs;

public class RideHub : Hub
{
    private readonly ILogger<RideHub> _logger;

    public RideHub(ILogger<RideHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinRiderGroup(string riderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"rider_{riderId}");
        _logger.LogInformation("Connection {ConnectionId} joined rider group rider_{RiderId}", Context.ConnectionId, riderId);
    }

    public async Task LeaveRiderGroup(string riderId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"rider_{riderId}");
    }

    public async Task JoinDriverGroup(string driverId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"driver_{driverId}");
        _logger.LogInformation("Connection {ConnectionId} joined driver group driver_{DriverId}", Context.ConnectionId, driverId);
    }

    public async Task LeaveDriverGroup(string driverId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"driver_{driverId}");
    }

    public async Task JoinRideGroup(string rideId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ride_{rideId}");
    }

    public async Task LeaveRideGroup(string rideId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ride_{rideId}");
    }

    public async Task JoinMonitorGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "monitors");
        _logger.LogInformation("Connection {ConnectionId} joined monitors group", Context.ConnectionId);
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected to RideHub: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected from RideHub: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
