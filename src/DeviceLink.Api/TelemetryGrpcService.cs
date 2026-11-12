using DeviceLink.Api.Protos;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace DeviceLink.Api
{
    /// <summary>Telemetry gRPC service: receives streams from stations and broadcasts a live stream to viewers.</summary>
    [Authorize]
    public class TelemetryGrpcService(TelemetryHub hub, ILogger<TelemetryGrpcService> logger)
        : TelemetryService.TelemetryServiceBase
    {
        /// <summary>Streams live points to a viewer (optionally filtered by station) until disconnect or token expiry.</summary>
        [Authorize(Roles = "viewer")]
        public override async Task SubscribeTelemetry(SubscribeRequest request, IServerStreamWriter<TelemetryPoint> responseStream, ServerCallContext context)
        {
            var user = UserName(context);
            using var scope = logger.BeginScope(new Dictionary<string, object> { ["User"] = user });
            logger.LogInformation("Viewer {User} subscribed, station filter '{Station}', subscribers {Count}",
                user, request.StationId, hub.SubscriberCount + 1);

            using var cts = TokenLifetime(context);
            ulong sent = 0;
            try
            {
                await foreach (var point in hub.SubscribeAsync(cts.Token))
                {
                    if (request.StationId.Length == 0 || point.StationId == request.StationId)
                    {
                        await responseStream.WriteAsync(point);
                        sent++;
                    }
                }
            }
            catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Viewer {User} token expired, closing stream", user);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Token expired"));
            }
            finally
            {
                logger.LogInformation("Viewer {User} unsubscribed, {Sent} points sent", user, sent);
            }
        }

        /// <summary>Receives a point stream from a station, publishes it to the hub and returns the accepted count.</summary>
        [Authorize(Roles = "telemetry-sender")]
        public override async Task<UploadSummary> UploadTelemetry(IAsyncStreamReader<TelemetryPoint> requestStream, ServerCallContext context)
        {
            var user = UserName(context);
            using var scope = logger.BeginScope(new Dictionary<string, object> { ["User"] = user });
            logger.LogInformation("Station {User} started upload from {Peer}", user, context.Peer);

            using var cts = TokenLifetime(context);
            ulong accepted = 0;
            string? stationId = null;
            try
            {
                await foreach (var point in requestStream.ReadAllAsync(cts.Token))
                {
                    if (stationId is null)
                    {
                        stationId = point.StationId;
                        logger.LogInformation("Upload stream identified as station {Station}", stationId);
                    }
                    hub.Publish(point);
                    accepted++;
                }
            }
            catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Station {Station} token expired after {Accepted} points", stationId, accepted);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Token expired"));
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            {
                logger.LogInformation("Station {Station} disconnected after {Accepted} points", stationId, accepted);
                throw;
            }

            logger.LogInformation("Station {Station} finished upload, {Accepted} points accepted", stationId, accepted);
            return new UploadSummary { Accepted = accepted };
        }

        /// <summary>Returns the user name from the JWT, or "anonymous".</summary>
        private static string UserName(ServerCallContext context) =>
            context.GetHttpContext().User.Identity?.Name ?? "anonymous";

        /// <summary>Creates a token that is cancelled when the client disconnects or the JWT expires.</summary>
        private CancellationTokenSource TokenLifetime(ServerCallContext context)
        {
            var user = context.GetHttpContext().User;
            var exp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(user.FindFirst("exp")!.Value));

            var left = exp - DateTimeOffset.UtcNow;
            if (left <= TimeSpan.Zero)
            {
                logger.LogWarning("Rejected call {Method}: token already expired", context.Method);
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Token expired"));
            }

            logger.LogDebug("Call {Method} token valid for {Left}", context.Method, left);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(left);
            return cts;
        }
    }
}
