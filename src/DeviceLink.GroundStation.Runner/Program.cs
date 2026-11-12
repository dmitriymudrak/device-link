using DeviceLink.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace DeviceLink.GroundStation.Runner
{
    /// <summary>Entry point of the headless ground station.</summary>
    internal class Program
    {
        // Positional args as before: [port] [apiAddress] [stationId].
        // --Station:Port=... etc. also work, as do env vars like Station__AccessToken=...
        /// <summary>Configures the Generic Host, registers ApiForwarder and StationWorker and runs them.</summary>
        static async Task Main(string[] args)
        {
            AppLoggingExtensions.HookUnhandledExceptions();
            try
            {
                var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
                {
                    Args = args,
                    ContentRootPath = AppContext.BaseDirectory
                });
                builder.Configuration.AddInMemoryCollection(PositionalArgs(args));
                builder.AddDeviceLinkLogging("Runner");

                builder.Services.AddOptions<RunnerOptions>().BindConfiguration(RunnerOptions.Section)
                    .ValidateDataAnnotations().ValidateOnStart();

                // Client credentials when Station:Auth:ClientId is set, otherwise the static Station:AccessToken.
                if (builder.Configuration.GetSection($"{RunnerOptions.Section}:Auth")["ClientId"] is { Length: > 0 })
                    builder.Services.AddHttpClient<IAccessTokenProvider, ClientCredentialsTokenProvider>();
                else
                    builder.Services.AddSingleton<IAccessTokenProvider, StaticAccessTokenProvider>();

                builder.Services.AddSingleton(sp =>
                {
                    var o = sp.GetRequiredService<IOptions<RunnerOptions>>().Value;
                    var tokens = sp.GetRequiredService<IAccessTokenProvider>();
                    return new ApiForwarder(o.ApiAddress, o.StationId,
                        () => tokens.GetAccessTokenAsync(),
                        sp.GetRequiredService<ILogger<ApiForwarder>>());
                });
                builder.Services.AddHostedService<StationWorker>();

                await builder.Build().RunAsync();
            }
            catch (Exception ex) when (ex is not HostAbortedException)
            {
                Log.Fatal(ex, "Runner terminated unexpectedly");
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        /// <summary>Maps positional args [port] [apiAddress] [stationId] to Station section keys.</summary>
        private static Dictionary<string, string?> PositionalArgs(string[] args)
        {
            var positional = args.TakeWhile(a => !a.StartsWith('-')).ToArray();
            var map = new Dictionary<string, string?>();
            if (positional.Length > 0) map[$"{RunnerOptions.Section}:Port"] = positional[0];
            if (positional.Length > 1) map[$"{RunnerOptions.Section}:ApiAddress"] = positional[1];
            if (positional.Length > 2) map[$"{RunnerOptions.Section}:StationId"] = positional[2];
            return map;
        }
    }
}
