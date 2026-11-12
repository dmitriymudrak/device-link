using DeviceLink.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace DeviceLink.GroundStation
{
    /// <summary>TCP server for the drone: receives telemetry and sends commands to the current connection.</summary>
    public sealed class DroneListener(IPEndPoint endpoint, Action<byte,Telemetry> onTelemetry,
        ILogger<DroneListener>? logger = null)
    {
        private readonly ILogger _logger = (ILogger?)logger ?? NullLogger.Instance;
        private volatile Channel<Command>? _commands;
        private int _nextCommandId;
        /// <summary>Listens on the endpoint and handles each new drone connection until cancelled.</summary>
        public async Task RunAsync(CancellationToken ct)
        {
            using (var listener = new TcpListener(endpoint))
            {
                listener.Start();
                _logger.LogInformation("Waiting for drone on {Endpoint}", endpoint);

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync(ct);
                    _logger.LogInformation("Drone connected from {Remote}", client.Client.RemoteEndPoint);
                    _ = HandleAsync(client, ct);
                }
            }
        }
        /// <summary>Queues a command for the connected drone; false if no drone is connected or the queue is full.</summary>
        public bool TrySend(CommandCode code)
        {
            var commands = _commands;
            if (commands is null)
                return false;

            var id = (ushort)Interlocked.Increment(ref _nextCommandId);
            return commands.Writer.TryWrite(new Command(id, code, 0));
        }

        /// <summary>Reads commands from the channel and writes them to the drone stream as frames with increasing seq.</summary>
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

        /// <summary>Serves one connection: reads telemetry and sends commands concurrently until either side ends.</summary>
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
            catch (IOException ex)
            {
                _logger.LogInformation("Drone disconnected: {Reason}", ex.Message);
                // drone disconnected, wait for the next connection
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Drone connection error");
            }
            finally
            {
                _commands = null;
                connection.Cancel();
                client.Dispose();
                _logger.LogDebug("Drone connection closed");
            }
        }
    }
}
