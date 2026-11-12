using DeviceLink.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DeviceLink.Simulator
{
    /// <summary>Entry point of the console drone simulator.</summary>
    public class Program
    {
        // Positional args as before: [host] [port]. --Simulator:Host=... --Simulator:Port=... also work.
        /// <summary>Configures the Generic Host (config, logging, options) and runs DroneSimulator.</summary>
        public static async Task Main(string[] args)
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
                builder.AddDeviceLinkLogging("Simulator");

                builder.Services.AddOptions<SimulatorOptions>().BindConfiguration(SimulatorOptions.Section)
                    .ValidateDataAnnotations().ValidateOnStart();
                builder.Services.AddHostedService<DroneSimulator>();

                await builder.Build().RunAsync();
            }
            catch (Exception ex) when (ex is not HostAbortedException)
            {
                Log.Fatal(ex, "Simulator terminated unexpectedly");
            }
            finally
            {
                await Log.CloseAndFlushAsync();
            }
        }

        /// <summary>Maps positional args [host] [port] to Simulator section configuration keys.</summary>
        private static Dictionary<string, string?> PositionalArgs(string[] args)
        {
            var positional = args.TakeWhile(a => !a.StartsWith('-')).ToArray();
            var map = new Dictionary<string, string?>();
            if (positional.Length > 0) map[$"{SimulatorOptions.Section}:Host"] = positional[0];
            if (positional.Length > 1) map[$"{SimulatorOptions.Section}:Port"] = positional[1];
            return map;
        }
    }
}
