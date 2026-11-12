using DeviceLink.GroundStation.Desktop.Options;
using DeviceLink.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop.Services
{
    /// <summary>
    /// Runs drone telemetry reception and forwarding to the Api.
    /// The UI subscribes to events and knows nothing about DroneListener/ApiForwarder.
    /// </summary>
    public sealed class StationService : IDisposable
    {
        private readonly ApiForwarder _forwarder;
        private readonly DroneListener _listener;
        private readonly ILogger<StationService> _logger;
        private CancellationTokenSource? _cts;

        /// <summary>Creates a DroneListener on the loopback port from the Api options.</summary>
        public StationService(IOptions<ApiOptions> options, ApiForwarder forwarder,
            ILogger<StationService> logger, ILogger<DroneListener> listenerLogger)
        {
            _forwarder = forwarder;
            _logger = logger;
            StationName = options.Value.StationName;
            Port = options.Value.Port;
            _listener = new DroneListener(new IPEndPoint(IPAddress.Loopback, Port), OnTelemetry, listenerLogger);
        }

        public string StationName { get; }
        /// <summary>Local TCP port where the station waits for the drone.</summary>
        public int Port { get; }

        /// <summary>New telemetry point. Raised NOT on the UI thread.</summary>
        public event Action<byte, Telemetry>? TelemetryReceived;

        /// <summary>A background loop failed. Raised NOT on the UI thread.</summary>
        public event Action<string, Exception>? Faulted;

        /// <summary>Starts the drone receive and Api forward loops; repeated calls are ignored.</summary>
        public void Start()
        {
            if (_cts is not null) return;
            _cts = new CancellationTokenSource();

            _logger.LogInformation("Station {Station} starting: drone port {Port}", StationName, Port);
            _ = RunGuardedAsync(_listener.RunAsync, "Дрон", _cts.Token);
            _ = RunGuardedAsync(_forwarder.RunAsync, "Api", _cts.Token);
        }

        /// <summary>Sends a command to the drone; false if no drone is connected.</summary>
        public bool TrySend(CommandCode code)
        {
            var sent = _listener.TrySend(code);
            if (sent)
                _logger.LogInformation("Command {Command} sent to drone", code);
            else
                _logger.LogWarning("Command {Command} not sent: drone is not connected", code);
            return sent;
        }

        /// <summary>Cancels the background loops; safe to call repeatedly.</summary>
        public void Stop()
        {
            if (_cts is null) return;
            _logger.LogInformation("Station {Station} stopping", StationName);
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        /// <summary>Stops the station.</summary>
        public void Dispose() => Stop();

        /// <summary>Queues the point for forwarding to the Api and notifies subscribers.</summary>
        private void OnTelemetry(byte seq, Telemetry t)
        {
            _logger.LogTrace("Telemetry seq={Seq} lat={Lat} lon={Lon}", seq, t.Latitude, t.Longitude);
            _forwarder.Enqueue(seq, t);
            TelemetryReceived?.Invoke(seq, t);
        }

        /// <summary>Runs a loop on the thread pool; logs failures and reports them via the Faulted event.</summary>
        private async Task RunGuardedAsync(Func<CancellationToken, Task> run, string name, CancellationToken ct)
        {
            try
            {
                await Task.Run(() => run(ct), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogDebug("{Loop} loop stopped", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Loop} loop failed", name);
                Faulted?.Invoke(name, ex);
            }
        }
    }
}
