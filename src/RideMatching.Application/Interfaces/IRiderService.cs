using RideMatching.Application.DTOs;

namespace RideMatching.Application.Interfaces;

public interface IRiderService
{
    Task<RiderDto> RegisterRiderAsync(CreateRiderRequestDto request, CancellationToken cancellationToken = default);
    Task<RiderDto?> GetRiderAsync(Guid riderId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RiderDto>> GetAllRidersAsync(CancellationToken cancellationToken = default);
}
