using DeviceLink.Protocol;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace DeviceLink.GroundStation
{
    public sealed class DroneListener(IPEndPoint endpoint, Action<byte,Telemetry> onTelemetry)
    {
        private volatile Channel<Command>? _commands;
        private int _nextCommandId;
        public async Task RunAsync(CancellationToken ct)
        {
            using (var listener = new TcpListener(endpoint))
            {
                listener.Start();

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(ct);
                    _ = HandleAsync(client, ct);
                }
            }
        }
        public bool TrySend(CommandCode code)
        {
            var commands = _commands;
            if (commands is null)
                return false;

            var id = (ushort)Interlocked.Increment(ref _nextCommandId);
            return commands.Writer.TryWrite(new Command(id, code, 0));
        }

        private static async Task WriteCommandsAsync(Stream stream, ChannelReader<Command> commands, CancellationToken ct)
        {
            var buffer = new ArrayBufferWriter<byte>(64);
            byte sequence = 0;
            await foreach (var command in commands.ReadAllAsync(ct))
            {
                buffer.ResetWrittenCount();
                command.WriteFrame(buffer, sequence++);
                await stream.WriteAsync(buffer.WrittenMemory, ct);
            }
        }

        private async Task HandleAsync(TcpClient client, CancellationToken ct)
        {
            var stream = client.GetStream();
            var commands = Channel.CreateBounded<Command>(16);
            using var connection = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _commands = commands;
            try
            {
                var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
                Task read = TelemetryReader.ReadAsync(reader, onTelemetry, connection.Token);
                Task write = WriteCommandsAsync(stream, commands.Reader, connection.Token);
                await await Task.WhenAny(read, write);
            }
            catch (IOException)
            {
                // дрон відключився — чекаємо наступного підключення
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"Помилка з'єднання з дроном: {ex}");
            }
            finally
            {
                _commands = null;
                connection.Cancel();
            }
        }
    }
}
