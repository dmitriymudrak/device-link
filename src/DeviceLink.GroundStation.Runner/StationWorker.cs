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
            if (o.Auth.IsConfigured)
                logger.LogInformation("Using client credentials of {ClientId} for Api calls", o.Auth.ClientId);
            else if (string.IsNullOrEmpty(o.AccessToken))
                logger.LogWarning("Neither Station:Auth nor Station:AccessToken is set, Api will reject telemetry upload");

            var endpoint = new IPEndPoint(IPAddress.Parse(o.ListenAddress), o.Port);
            var listener = new DroneListener(endpoint, (seq, t) =>
            {
                logger.LogInformation("seq={Seq,3} lat={Lat:F7} lon={Lon:F7}", seq, t.Latitude, t.Longitude);
                forwarder.Enqueue(seq, t);
            }, listenerLogger);

            logger.LogInformation("Station {Station}: drone endpoint {Endpoint}, Api {Api}. Ctrl+C to stop",
                o.StationId, endpoint, o.ApiAddress);

            try
            {
                Task[] tasks = [listener.RunAsync(stoppingToken), forwarder.RunAsync(stoppingToken)];
                await await Task.WhenAny(tasks);

                // Both loops run forever; finishing without cancellation means the station is no longer working.
                logger.LogCritical("Station loop exited unexpectedly, stopping");
                lifetime.StopApplication();
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
