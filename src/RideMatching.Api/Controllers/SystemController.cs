using Microsoft.AspNetCore.Mvc;
using RideMatching.Application.Interfaces;
using RideMatching.Infrastructure.Data;

namespace RideMatching.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IDriverGeoCache _geoCache;
    private readonly ILogger<SystemController> _logger;

    public SystemController(AppDbContext context, IDriverGeoCache geoCache, ILogger<SystemController> logger)
    {
        _context = context;
        _geoCache = geoCache;
        _logger = logger;
    }

    /// <summary>
    /// System health check.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }

    /// <summary>
    /// Seeds or resets the sample drivers and riders.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        await DbSeeder.SeedAsync(_context, _geoCache, _logger);
        return Ok(new { message = "Sample data seeded successfully" });
    }
}
