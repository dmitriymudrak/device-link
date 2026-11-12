using DeviceLink.Api.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace DeviceLink.Api
{
    [Authorize]
    public class TelemetryGrpcService(TelemetryHub hub):TelemetryService.TelemetryServiceBase
    {
        [Authorize(Roles ="viewer")]
        public override async Task SubscribeTelemetry(SubscribeRequest request, IServerStreamWriter<TelemetryPoint> responseStream, ServerCallContext context)
        {

            using(var cts = TokenLifetime(context))
            {
                try
                {
                    await foreach (var point in hub.SubscribeAsync(context.CancellationToken))
                    {
                        if (request.StationId.Length == 0 || point.StationId == request.StationId)
                        {
                            await responseStream.WriteAsync(point);
                        }
                    }
                }catch(OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
                {
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Token expired"));
                }
                
            }

        }

        [Authorize(Roles = "telemetry-sender")]
        public override async Task<UploadSummary> UploadTelemetry(IAsyncStreamReader<TelemetryPoint> requestStream, ServerCallContext context)
        {
            using var cts = TokenLifetime(context);  
            ulong accepted = 0;
            try
            {
                await foreach (var point in requestStream.ReadAllAsync(cts.Token))
                {
                    hub.Publish(point);
                    accepted++;
                }
            }
            catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Token expired"));  
            }

            return new UploadSummary { Accepted = accepted };
        }

        static CancellationTokenSource TokenLifetime(ServerCallContext context)
        {
            var user = context.GetHttpContext().User;
            var exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(user.FindFirst("exp")!.Value));  

            var left = exp - DateTimeOffset.UtcNow;
            if (left <= TimeSpan.Zero)
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Token expired"));

            var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(left);
            return cts;
        }

    }
}