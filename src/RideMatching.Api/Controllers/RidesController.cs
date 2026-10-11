using Microsoft.AspNetCore.Mvc;
using RideMatching.Application.DTOs;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RidesController : ControllerBase
{
    private readonly IRideService _rideService;

    public RidesController(IRideService rideService)
    {
        _rideService = rideService;
    }

    /// <summary>
    /// Creates a new ride request and enqueues it for asynchronous nearest-driver matching.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RideDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RideDto>> RequestRide([FromBody] CreateRideRequestDto request, CancellationToken ct)
    {
        var ride = await _rideService.RequestRideAsync(request, ct);
        return CreatedAtAction(nameof(GetRide), new { id = ride.Id }, ride);
    }

    /// <summary>
    /// Gets details and current state of a ride request.
    /// </summary>
    [HttpGet("{id:guid}", Name = nameof(GetRide))]
    [ProducesResponseType(typeof(RideDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RideDto>> GetRide(Guid id, CancellationToken ct)
    {
        var ride = await _rideService.GetRideAsync(id, ct);
        if (ride == null) return NotFound($"Ride with ID {id} was not found.");
        return Ok(ride);
    }

    /// <summary>
    /// Cancels an active or pending ride request.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(RideDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RideDto>> CancelRide(Guid id, [FromBody] CancelRideRequest request, CancellationToken ct)
    {
        var ride = await _rideService.CancelRideAsync(id, request.Reason ?? "Cancelled by user", ct);
        return Ok(ride);
    }

    /// <summary>
    /// Calculates distance and estimated fare based on pickup and dropoff coordinates.
    /// </summary>
    [HttpGet("estimate")]
    [ProducesResponseType(typeof(FareEstimateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<FareEstimateDto> GetFareEstimate(
        [FromQuery] double pickupLat,
        [FromQuery] double pickupLon,
        [FromQuery] double dropoffLat,
        [FromQuery] double dropoffLon)
    {
        var pickup = new GeoLocation(pickupLat, pickupLon);
        var dropoff = new GeoLocation(dropoffLat, dropoffLon);
        var estimate = _rideService.GetFareEstimate(pickup, dropoff);
        return Ok(estimate);
    }

    /// <summary>
    /// Returns the most recent rides across the platform for monitoring.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IReadOnlyList<RideDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RideDto>>> GetRecentRides([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var rides = await _rideService.GetRecentRidesAsync(limit, ct);
        return Ok(rides);
    }

    /// <summary>
    /// Returns current system-wide metrics (online drivers, active rides, completed rides).
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(SystemMetricsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemMetricsDto>> GetMetrics(CancellationToken ct)
    {
        var metrics = await _rideService.GetSystemMetricsAsync(ct);
        return Ok(metrics);
    }
}

public record CancelRideRequest(string? Reason);
