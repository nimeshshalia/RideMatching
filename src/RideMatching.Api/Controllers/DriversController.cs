using Microsoft.AspNetCore.Mvc;
using RideMatching.Application.DTOs;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Enums;
using RideMatching.Domain.ValueObjects;

namespace RideMatching.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DriversController : ControllerBase
{
    private readonly IDriverService _driverService;
    private readonly IRideService _rideService;

    public DriversController(IDriverService driverService, IRideService rideService)
    {
        _driverService = driverService;
        _rideService = rideService;
    }

    /// <summary>
    /// Registers a new driver in the system.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DriverDto>> RegisterDriver([FromBody] CreateDriverRequestDto request, CancellationToken ct)
    {
        var driver = await _driverService.RegisterDriverAsync(request, ct);
        return CreatedAtAction(nameof(GetDriver), new { id = driver.Id }, driver);
    }

    /// <summary>
    /// Returns all registered drivers and their current status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DriverDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DriverDto>>> GetAllDrivers(CancellationToken ct)
    {
        var drivers = await _driverService.GetAllDriversAsync(ct);
        return Ok(drivers);
    }

    /// <summary>
    /// Gets details of a specific driver by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverDto>> GetDriver(Guid id, CancellationToken ct)
    {
        var driver = await _driverService.GetDriverAsync(id, ct);
        if (driver == null) return NotFound($"Driver with ID {id} was not found.");
        return Ok(driver);
    }

    /// <summary>
    /// Sets a driver to Online status with an initial GPS location.
    /// </summary>
    [HttpPost("{id:guid}/online")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DriverDto>> GoOnline(Guid id, [FromBody] LocationDto location, CancellationToken ct)
    {
        var geo = new GeoLocation(location.Latitude, location.Longitude);
        var driver = await _driverService.SetDriverOnlineAsync(id, geo, ct);
        return Ok(driver);
    }

    /// <summary>
    /// Sets a driver to Offline status.
    /// </summary>
    [HttpPost("{id:guid}/offline")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DriverDto>> GoOffline(Guid id, CancellationToken ct)
    {
        var driver = await _driverService.SetDriverOfflineAsync(id, ct);
        return Ok(driver);
    }

    /// <summary>
    /// Updates driver's live GPS coordinates, bearing, and speed.
    /// </summary>
    [HttpPost("{id:guid}/location")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationDto location, CancellationToken ct)
    {
        var geo = new GeoLocation(location.Latitude, location.Longitude);
        var success = await _driverService.UpdateLocationAsync(id, geo, location.SpeedKmh, location.Bearing, ct);
        if (!success) return NotFound($"Driver with ID {id} was not found.");
        return Ok(new { success = true, driverId = id, location });
    }

    /// <summary>
    /// Driver accepts a pending ride offer.
    /// </summary>
    [HttpPost("{id:guid}/rides/{rideId:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptRide(Guid id, Guid rideId, CancellationToken ct)
    {
        var success = await _driverService.AcceptRideOfferAsync(id, rideId, ct);
        return Ok(new { success, message = "Ride accepted successfully" });
    }

    /// <summary>
    /// Driver rejects a pending ride offer.
    /// </summary>
    [HttpPost("{id:guid}/rides/{rideId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectRide(Guid id, Guid rideId, CancellationToken ct)
    {
        var success = await _driverService.RejectRideOfferAsync(id, rideId, ct);
        return Ok(new { success, message = "Ride offer declined. Re-routing ride." });
    }

    /// <summary>
    /// Updates ride lifecycle status (DriverArriving, InProgress, Completed, Cancelled).
    /// </summary>
    [HttpPost("{id:guid}/rides/{rideId:guid}/status")]
    [ProducesResponseType(typeof(RideDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RideDto>> UpdateRideStatus(Guid id, Guid rideId, [FromBody] UpdateRideStatusRequest request, CancellationToken ct)
    {
        var ride = await _driverService.UpdateRideStatusAsync(id, rideId, request.Status, ct);
        return Ok(ride);
    }

    /// <summary>
    /// Gets active ride assigned to or offered to this driver.
    /// </summary>
    [HttpGet("{id:guid}/active-ride")]
    [ProducesResponseType(typeof(RideDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RideDto?>> GetActiveRide(Guid id, CancellationToken ct)
    {
        var ride = await _rideService.GetDriverActiveRideAsync(id, ct);
        return Ok(ride);
    }
}

public record UpdateRideStatusRequest(RideStatus Status);
