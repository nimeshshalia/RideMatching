using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RideMatching.Application.Common;
using RideMatching.Application.DTOs;
using RideMatching.Application.Interfaces;
using RideMatching.Domain.Entities;

namespace RideMatching.Application.Services;

public class RiderService : IRiderService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<RiderService> _logger;

    public RiderService(IApplicationDbContext context, ILogger<RiderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<RiderDto> RegisterRiderAsync(CreateRiderRequestDto request, CancellationToken cancellationToken = default)
    {
        var rider = new Rider(request.Name, request.Email, request.Phone);
        _context.Riders.Add(rider);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Rider registered: {RiderId} ({Name})", rider.Id, rider.Name);
        return rider.ToDto();
    }

    public async Task<RiderDto?> GetRiderAsync(Guid riderId, CancellationToken cancellationToken = default)
    {
        var rider = await _context.Riders.AsNoTracking().FirstOrDefaultAsync(r => r.Id == riderId, cancellationToken);
        return rider?.ToDto();
    }

    public async Task<IReadOnlyList<RiderDto>> GetAllRidersAsync(CancellationToken cancellationToken = default)
    {
        var riders = await _context.Riders.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);
        return riders.Select(r => r.ToDto()).ToList();
    }
}
