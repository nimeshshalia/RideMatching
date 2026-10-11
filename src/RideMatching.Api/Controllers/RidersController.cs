using Microsoft.AspNetCore.Mvc;
using RideMatching.Application.DTOs;
using RideMatching.Application.Interfaces;

namespace RideMatching.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RidersController : ControllerBase
{
    private readonly IRiderService _riderService;
    private readonly IRideService _rideService;

    public RidersController(IRiderService riderService, IRideService rideService)
    {
        _riderService = riderService;
        _rideService = rideService;
    }

    /// <summary>
    /// Registers a new rider in the platform.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RiderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RiderDto>> RegisterRider([FromBody] CreateRiderRequestDto request, CancellationToken ct)
    {
        var rider = await _riderService.RegisterRiderAsync(request, ct);
        return CreatedAtAction(nameof(GetRider), new { id = rider.Id }, rider);
    }

    /// <summary>
    /// Gets all registered riders.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RiderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RiderDto>>> GetAllRiders(CancellationToken ct)
    {
        var riders = await _riderService.GetAllRidersAsync(ct);
        return Ok(riders);
    }

    /// <summary>
    /// Gets a specific rider by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RiderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiderDto>> GetRider(Guid id, CancellationToken ct)
    {
        var rider = await _riderService.GetRiderAsync(id, ct);
        if (rider == null) return NotFound($"Rider with ID {id} was not found.");
        return Ok(rider);
    }

    /// <summary>
    /// Gets the current active ride for this rider, if any.
    /// </summary>
    [HttpGet("{id:guid}/active-ride")]
    [ProducesResponseType(typeof(RideDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RideDto?>> GetActiveRide(Guid id, CancellationToken ct)
    {
        var ride = await _rideService.GetRiderActiveRideAsync(id, ct);
        return Ok(ride);
    }
}
