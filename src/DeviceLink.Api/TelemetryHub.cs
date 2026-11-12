using DeviceLink.Api.Protos;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace DeviceLink.Api
{
    /// <summary>Fans out station telemetry points to all active viewer subscribers.</summary>
    public sealed class TelemetryHub(ILogger<TelemetryHub> logger)
    {
        private readonly ConcurrentDictionary<Guid, Channel<TelemetryPoint>> _subscribers = new();

        public int SubscriberCount => _subscribers.Count;

        /// <summary>Writes the point to every subscriber's channel without waiting.</summary>
        public void Publish(TelemetryPoint point)
        {
            foreach (var channel in _subscribers.Values)
            {
                channel.Writer.TryWrite(point);
            }
        }

        /// <summary>Registers a subscriber with a bounded buffer (oldest points dropped) and streams points until cancelled.</summary>
        public async IAsyncEnumerable<TelemetryPoint> SubscribeAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var channel = Channel.CreateBounded<TelemetryPoint>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });

            var id = Guid.NewGuid();
            _subscribers[id] = channel;
            logger.LogDebug("Subscriber {Id} added, total {Count}", id, _subscribers.Count);

            try
            {
                await foreach (var point in channel.Reader.ReadAllAsync(cancellationToken))
                    yield return point;
            }
            finally
            {
                _subscribers.TryRemove(id, out _);
                logger.LogDebug("Subscriber {Id} removed, total {Count}", id, _subscribers.Count);
            }
        }
    }
}
