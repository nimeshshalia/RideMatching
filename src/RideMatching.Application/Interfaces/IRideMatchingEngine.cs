using RideMatching.Domain.Entities;

namespace RideMatching.Application.Interfaces;

public interface IRideMatchingEngine
{
    /// <summary>
    /// Processes matching for a requested ride. Returns true if an available driver was found and offered/assigned.
    /// </summary>
    Task<bool> MatchRideAsync(Guid rideId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles expiration of pending offers and triggers re-matching or status updates.
    /// </summary>
    Task CheckAndProcessExpiredOffersAsync(CancellationToken cancellationToken = default);
}
