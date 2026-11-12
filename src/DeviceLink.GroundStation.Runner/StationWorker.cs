using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace DeviceLink.GroundStation.Runner
{
    /// <summary>Receives drone telemetry and forwards it to the Api.</summary>
    public sealed class StationWorker(
        IOptions<RunnerOptions> options,
        ApiForwarder forwarder,
        IHostApplicationLifetime lifetime,
        ILogger<StationWorker> logger,
        ILogger<DroneListener> listenerLogger) : BackgroundService
    {
        /// <summary>Runs drone reception and Api forwarding; stops the application on failure.</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var o = options.Value;
            if (string.IsNullOrEmpty(o.AccessToken))
                logger.LogWarning("Station:AccessToken is not set, Api will reject telemetry upload");

            var listener = new DroneListener(new IPEndPoint(IPAddress.Loopback, o.Port), (seq, t) =>
            {
                logger.LogInformation("seq={Seq,3} lat={Lat:F7} lon={Lon:F7}", seq, t.Latitude, t.Longitude);
                forwarder.Enqueue(seq, t);
            }, listenerLogger);

            logger.LogInformation("Station {Station}: drone port {Port}, Api {Api}. Ctrl+C to stop",
                o.StationId, o.Port, o.ApiAddress);

            try
            {
                Task[] tasks = [listener.RunAsync(stoppingToken), forwarder.RunAsync(stoppingToken)];
                await await Task.WhenAny(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Station stopped");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Station failed");
                lifetime.StopApplication();
            }
        }
    }
}
