using DeviceLink.Api.Protos;
using DeviceLink.Protocol;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace DeviceLink.GroundStation
{
    /// <summary>Buffers station telemetry and streams it to the Api over gRPC with a Bearer token and reconnects.</summary>
    public sealed class ApiForwarder(string apiAddress, string stationId, 
        Func<Task<string>> getAccessToken, ILogger<ApiForwarder> logger)
    {
        private readonly Channel<TelemetryPoint> _queue
            = Channel.CreateBounded<TelemetryPoint>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

        /// <summary>Queues a telemetry point for upload; drops the oldest points when the queue is full.</summary>
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

        /// <summary>Sends the queue to the Api via a streaming UploadTelemetry call; retries after 2 s on RpcException.</summary>
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
                logger.LogInformation("Forwarding telemetry of {Station} to {Address}", stationId, apiAddress);
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
                        // +Unauthenticated: token expired, retry with a fresh token
                        logger.LogWarning(ex, "Api {Address} unavailable ({Status}), retry in 2 s", apiAddress, ex.StatusCode);
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException && !cancellationToken.IsCancellationRequested)
                    {
                        // e.g. the token provider failed (identity provider not reachable yet)
                        logger.LogWarning(ex, "Upload to {Address} failed, retry in 2 s", apiAddress);
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    }
                }
            }
        }
    }
}
