using DeviceLink.Protocol;
using Microsoft.Extensions.Logging;
using System.Buffers;

namespace DeviceLink.Simulator
{
    /// <summary>Periodically sends telemetry frames along the route; supports holding position.</summary>
    public sealed class TelemetrySender(Route route, TimeSpan period, ILogger<TelemetrySender> logger)
    {
        private readonly ArrayBufferWriter<byte> _buffer = new(64);
        private byte _sequence;

        private volatile bool _holding;
        private Telemetry _last;

        /// <summary>Stops movement: the last known position is sent from now on.</summary>
        public void Hold()
        {
            _holding = true;
            logger.LogInformation("Holding position");
        }

        /// <summary>Resumes movement along the route.</summary>
        public void Resume()
        {
            _holding = false;
            logger.LogInformation("Resuming route");
        }

        /// <summary>Sends a frame immediately, then every period until cancelled or the stream fails.</summary>
        public async Task RunAsync(Stream stream, CancellationToken cancellationToken)
        {
            logger.LogInformation("Sending telemetry every {Period}", period);
            using (var timer = new PeriodicTimer(period))
            {
                do
                {
                    _buffer.ResetWrittenCount();
                    var telemetry = _holding ? _last : (_last = route.Next());
                    telemetry.WriteFrame(_buffer, _sequence);
                    await stream.WriteAsync(_buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
                    logger.LogDebug("seq={Seq} lat={Lat:F7} lon={Lon:F7}", _sequence, telemetry.Latitude, telemetry.Longitude);
                    _sequence++;
                }
                while (await timer.WaitForNextTickAsync(cancellationToken));
            }
        }
    }
}
