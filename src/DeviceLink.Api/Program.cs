
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace DeviceLink.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddGrpc();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.Authority = "http://localhost:8080/realms/device-link-realm";
                o.Audience = "dashboard-api";
                o.RequireHttpsMetadata = false;
                o.MapInboundClaims = false;
                o.TokenValidationParameters.RoleClaimType = "roles";
                o.TokenValidationParameters.NameClaimType = "preferred_username";
            });
            builder.Services.AddAuthorization();
            builder.Services.AddSingleton<TelemetryHub>();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGrpcService<TelemetryGrpcService>();
            // Configure the HTTP request pipeline.
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.Run();
        }
    }
}