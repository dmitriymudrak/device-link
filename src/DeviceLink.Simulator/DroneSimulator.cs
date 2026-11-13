using DeviceLink.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace DeviceLink.Simulator
{
    /// <summary>Connects to the station, sends telemetry along the route and executes commands.</summary>
    public sealed class DroneSimulator(
        IOptions<SimulatorOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<DroneSimulator> logger,
        ILogger<TelemetrySender> senderLogger) : BackgroundService
    {
        private readonly SimulatorOptions _options = options.Value;

        /// <summary>One link session: connect, exchange data until disconnect or Destroy, then stop the host.</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            var route = new Route(
                [(50.4501, 30.5234), (50.4700, 30.5600), (50.4350, 30.5700), (50.4250, 30.5150)],
                stepsPerSegment: 60);
            var sender = new TelemetrySender(route, _options.Period, senderLogger);
            bool destroyed = false;

            // Executes a station command: Hold/Resume control the sender, Destroy ends the session.
            void OnCommand(Command command)
            {
                logger.LogInformation("Command #{Id}: {Code}", command.CommandId, command.Code);
                switch (command.Code)
                {
                    case CommandCode.Hold: sender.Hold(); break;
                    case CommandCode.Resume: sender.Resume(); break;
                    case CommandCode.Destroy: destroyed = true; cts.Cancel(); break;
                    default: logger.LogWarning("Unknown command {Code}, ignored", command.Code); break;
                }
            }

            try
            {
                using var client = new TcpClient();
                logger.LogInformation("Connecting to station {Host}:{Port}", _options.Host, _options.Port);
                await client.ConnectAsync(_options.Host, _options.Port, cts.Token);
                logger.LogInformation("Connected to station {Host}:{Port}", _options.Host, _options.Port);

                var stream = client.GetStream();
                var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
                Task[] tasks = [sender.RunAsync(stream, cts.Token), CommandReader.ReadAsync(reader, OnCommand, cts.Token)];

                await await Task.WhenAny(tasks);
                logger.LogInformation("Station closed the connection");
            }
            catch (IOException ex)
            {
                logger.LogWarning("Connection lost: {Reason}", ex.Message);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                if (destroyed) logger.LogWarning("Drone destroyed by station command");
                else logger.LogInformation("Simulator stopped");
            }
            catch (SocketException ex)
            {
                logger.LogError("No connection to {Host}:{Port}: {Reason}", _options.Host, _options.Port, ex.Message);
            }
            finally
            {
                // The simulator is a one-shot process: stop the host once the session ends.
                lifetime.StopApplication();
            }
        }
    }
}
