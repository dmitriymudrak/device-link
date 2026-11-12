using DeviceLink.Protocol;
using System.Buffers;
using System.IO.Pipelines;

namespace DeviceLink.GroundStation
{
    /// <summary>Reads telemetry frames from the drone stream.</summary>
    public static class TelemetryReader
    {
        /// <summary>Reads frames from the PipeReader until end of stream and invokes the callback for each valid telemetry.</summary>
        public static async Task ReadAsync(
            PipeReader reader, Action<byte, Telemetry> onTelemetry, CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync(cancellationToken);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (FrameCodec.TryParse(ref buffer, out Frame frame))
                    {
                        if (frame.Type == Telemetry.MessageType &&
                            Telemetry.TryRead(frame.Payload, out Telemetry telemetry))
                        {
                            onTelemetry(frame.Sequence, telemetry);
                        }
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);

                    if (result.IsCompleted) break;
                }
            }
            finally 
            {
                await reader.CompleteAsync();
            }
        }
    }
}
