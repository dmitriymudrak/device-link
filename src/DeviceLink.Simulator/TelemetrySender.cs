using System.Buffers;

namespace DeviceLink.Simulator
{
    public sealed class TelemetrySender(Route route, TimeSpan period)
    {
        private readonly ArrayBufferWriter<byte> _buffer = new(64);
        private byte _sequence;

        public async Task RunAsync(Stream stream, CancellationToken cancellationToken)
        {
            using (var timer = new PeriodicTimer(period))
            {
                do
                {
                    _buffer.ResetWrittenCount();
                    route.Next().WriteFrame(_buffer, _sequence);
                    await stream.WriteAsync(_buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
                    _sequence++;
                }
                while (await timer.WaitForNextTickAsync(cancellationToken));
            }
        }
    }
}
    