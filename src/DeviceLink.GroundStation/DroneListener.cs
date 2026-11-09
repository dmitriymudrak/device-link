using DeviceLink.Protocol;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace DeviceLink.GroundStation
{
    public sealed class DroneListener(IPEndPoint endpoint, Action<byte,Telemetry> onTelemetry)
    {
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

        private async Task HandleAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                try
                {
                    var reader = PipeReader.Create(client.GetStream());
                    await TelemetryReader.ReadAsync(reader, onTelemetry, ct);
                }
                catch(IOException)
                {
                    //drone off
                }
                catch(Exception ex) when (ex is IOException or OperationCanceledException)
                {
                    //connection issue
                }
            }
        }
    }
}
