using DeviceLink.Api.Protos;
using DeviceLink.Protocol;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation
{
    public sealed class ApiForwarder(string apiAddress, string stationId)
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
            using (var channel = GrpcChannel.ForAddress(apiAddress))
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
                        Console.Error.WriteLine($"Api недоступний ({ex.StatusCode}), повтор через 2 с");
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    }
                }
            }
        }
    }
}
