using DeviceLink.Protocol;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace DeviceLink.Simulator
{
    public static class CommandReader
    {
        public static async Task ReadAsync(PipeReader reader, Action<Command> onCommand, CancellationToken ct)
        {
            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync(ct);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    while (FrameCodec.TryParse(ref buffer, out Frame frame))
                    {
                        if (frame.Type == Command.MessageType &&
                            Command.TryRead(frame.Payload, out var command))
                        {
                            onCommand(command);
                        }
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (result.IsCompleted)
                        break;
                }
            }
            finally
            {
                await reader.CompleteAsync();
            }
        }
    }
}
