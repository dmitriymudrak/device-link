using DeviceLink.Api.Protos;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;


namespace DeviceLink.Api
{
    public sealed class TelemetryHub
    {
        public ConcurrentDictionary<Guid, Channel<TelemetryPoint>> _subscribers = new();

        public void Publish(TelemetryPoint point)
        {
            foreach (var channel in _subscribers.Values)
            {
                channel.Writer.TryWrite(point);
            }
        }

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

            try
            {
                await foreach (var point in channel.Reader.ReadAllAsync(cancellationToken))
                    yield return point;
            }
            finally
            {
                _subscribers.TryRemove(id, out _);
            }
        }
    }
}
