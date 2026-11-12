using DeviceLink.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

namespace DeviceLink.Api
{
    /// <summary>Entry point of the DeviceLink Api.</summary>
    public class Program
    {
        /// <summary>Configures logging, Keycloak JWT authentication and the gRPC service, then runs the server.</summary>
        public static void Main(string[] args)
        {
            AppLoggingExtensions.HookUnhandledExceptions();
            try
            {
                var builder = WebApplication.CreateBuilder(args);
                builder.AddDeviceLinkLogging("Api");

                builder.Services.AddGrpc();

                // Section "Jwt": Authority, Audience, RequireHttpsMetadata and, for containers,
                // MetadataAddress (internal Keycloak URL) when it differs from the public issuer.
                var jwt = builder.Configuration.GetSection("Jwt");
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.Authority = jwt["Authority"]
                        ?? throw new InvalidOperationException("Configuration value Jwt:Authority is required");
                    o.Audience = jwt["Audience"] ?? "dashboard-api";
                    o.RequireHttpsMetadata = jwt.GetValue("RequireHttpsMetadata", true);
                    if (!string.IsNullOrWhiteSpace(jwt["MetadataAddress"]))
                        o.MetadataAddress = jwt["MetadataAddress"]!;
                    o.MapInboundClaims = false;
                    o.TokenValidationParameters.RoleClaimType = "roles";
                    o.TokenValidationParameters.NameClaimType = "preferred_username";
                    o.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = ctx =>
                        {
                            ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                                .LogWarning("JWT authentication failed: {Reason}", ctx.Exception.Message);
                            return Task.CompletedTask;
                        }
                    };
                });
                builder.Services.AddAuthorization();
                builder.Services.AddSingleton<TelemetryHub>();

                var app = builder.Build();
                app.UseAuthentication();
                app.UseAuthorization();
                app.MapGrpcService<TelemetryGrpcService>();
                app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

                app.Logger.LogInformation("DeviceLink Api starting");
                app.Run();
            }
            catch (Exception ex) when (ex is not HostAbortedException)
            {
                Log.Fatal(ex, "DeviceLink Api terminated unexpectedly");
                throw;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
