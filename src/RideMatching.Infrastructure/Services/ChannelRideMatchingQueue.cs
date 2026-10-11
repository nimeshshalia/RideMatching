using System.Threading.Channels;
using RideMatching.Application.Interfaces;

namespace RideMatching.Infrastructure.Services;

public class ChannelRideMatchingQueue : IRideMatchingQueue
{
    private readonly Channel<Guid> _channel;

    public ChannelRideMatchingQueue(int capacity = 10_000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Guid>(options);
    }

    public ValueTask EnqueueAsync(Guid rideId, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(rideId, cancellationToken);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
