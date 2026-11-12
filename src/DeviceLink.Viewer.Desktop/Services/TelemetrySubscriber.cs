using DeviceLink.Api.Protos;
using DeviceLink.Viewer.Desktop.Options;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.Viewer.Desktop.Services
{
    /// <summary>
    /// Maintains the gRPC telemetry subscription with automatic reconnect.
    /// Events are raised NOT on the UI thread.
    /// </summary>
    public sealed class TelemetrySubscriber(IOptions<ViewerOptions> options, ILogger<TelemetrySubscriber> logger) : IDisposable
    {
        private readonly ViewerOptions _options = options.Value;
        private CancellationTokenSource? _cts;

        /// <summary>A new telemetry point was received.</summary>
        public event Action<TelemetryPoint>? PointReceived;

        /// <summary>null means the connection is active; otherwise the error text.</summary>
        public event Action<string?>? StatusChanged;

        /// <summary>Starts the background subscription if it is not running yet.</summary>
        public void Start()
        {
            if (_cts is not null) return;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => RunAsync(_cts.Token));
        }

        /// <summary>Cancels the background subscription.</summary>
        public void Stop()
        {
            if (_cts is null) return;
            logger.LogInformation("Telemetry subscription stopping");
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        /// <summary>Stops the subscription.</summary>
        public void Dispose() => Stop();

        /// <summary>Subscription loop: reads the point stream and reconnects after drops until cancelled.</summary>
        private async Task RunAsync(CancellationToken ct)
        {
            using var channel = GrpcChannel.ForAddress(_options.ApiAddress);
            var client = new TelemetryService.TelemetryServiceClient(channel);
            var request = new SubscribeRequest { StationId = _options.StationId };

            try
            {
                while (true)
                {
                    try
                    {
                        logger.LogInformation("Subscribing to telemetry at {Api}", _options.ApiAddress);
                        using var call = client.SubscribeTelemetry(request, cancellationToken: ct);

                        var first = true;
                        await foreach (var point in call.ResponseStream.ReadAllAsync(ct))
                        {
                            if (first)
                            {
                                first = false;
                                logger.LogInformation("Telemetry stream is active");
                                StatusChanged?.Invoke(null);
                            }
                            PointReceived?.Invoke(point);
                        }

                        logger.LogWarning("Telemetry stream ended by server, reconnecting");
                    }
                    catch (RpcException ex) when (!ct.IsCancellationRequested)
                    {
                        logger.LogWarning("Api unavailable ({Status}: {Detail}), retry in {Delay}",
                            ex.StatusCode, ex.Status.Detail, _options.ReconnectDelay);
                        StatusChanged?.Invoke($"Api недоступний ({ex.StatusCode})");
                    }

                    await Task.Delay(_options.ReconnectDelay, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.LogDebug("Telemetry subscription cancelled");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telemetry subscription failed");
                StatusChanged?.Invoke($"Помилка: {ex.Message}");
            }
        }
    }
}
