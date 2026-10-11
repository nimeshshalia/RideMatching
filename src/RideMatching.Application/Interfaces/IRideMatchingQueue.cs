namespace RideMatching.Application.Interfaces;

public interface IRideMatchingQueue
{
    ValueTask EnqueueAsync(Guid rideId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);
}
