using DeviceLink.Api.Protos;
using DeviceLink.Protocol;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace DeviceLink.GroundStation
{
    public sealed class ApiForwarder(string apiAddress, string stationId, 
        Func<Task<string>> getAccessToken, ILogger<ApiForwarder> logger)
    {
        private readonly Channel<TelemetryPoint> _queue
            = Channel.CreateBounded<TelemetryPoint>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        public void Enqueue(byte seq, Telemetry telemetry)
        {
            _queue.Writer.TryWrite(new TelemetryPoint
            {
                StationId = stationId,
                Seq = seq,
                LatE7 = telemetry.LatE7,
                LonE7 = telemetry.LonE7,
                ReceivedAt = Timestamp.FromDateTimeOffset(DateTime.UtcNow)
            });
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var callCreds = CallCredentials.FromInterceptor(async (_, metadata) =>
                    metadata.Add("Authorization", $"Bearer {await getAccessToken()}"));

            using (var channel = GrpcChannel.ForAddress(apiAddress, new GrpcChannelOptions // +
            {
                Credentials = ChannelCredentials.Create(ChannelCredentials.Insecure, callCreds),
                UnsafeUseInsecureChannelCallCredentials = true   // http
            }))
            {

                var client = new TelemetryService.TelemetryServiceClient(channel);
                while (true)
                {
                    try
                    {
                        using (var call = client.UploadTelemetry(cancellationToken: cancellationToken))
                        {
                            await foreach (var point in _queue.Reader.ReadAllAsync(cancellationToken))
                            {
                                await call.RequestStream.WriteAsync(point);
                            }
                        }
                    }
                    catch (RpcException ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        // +Unauthenticated, токен експ + повтор із новим токеном
                        logger.LogError($"Api недоступний ({ex.StatusCode}), повтор через 2 с");
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    }
                }
            }
        }
    }
}
