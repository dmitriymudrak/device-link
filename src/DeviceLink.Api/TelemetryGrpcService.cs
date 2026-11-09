using DeviceLink.Api.Protos;
using Grpc.Core;

namespace DeviceLink.Api
{
    public class TelemetryGrpcService(TelemetryHub hub):TelemetryService.TelemetryServiceBase
    {
        public override async Task SubscribeTelemetry(SubscribeRequest request, IServerStreamWriter<TelemetryPoint> responseStream, ServerCallContext context)
        {
            await foreach (var point in hub.SubscribeAsync(context.CancellationToken))
            {
                if (request.StationId.Length == 0 || point.StationId == request.StationId)
                {
                    await responseStream.WriteAsync(point);
                }
            }
        }

        public override async Task<UploadSummary> UploadTelemetry(IAsyncStreamReader<TelemetryPoint> requestStream, ServerCallContext context)
        {
            ulong accepted = 0;
            await foreach(var point in requestStream.ReadAllAsync(context.CancellationToken))
            {
                hub.Publish(point);
                accepted++;
            }

            return new UploadSummary { Accepted = accepted };
        }
    }
}