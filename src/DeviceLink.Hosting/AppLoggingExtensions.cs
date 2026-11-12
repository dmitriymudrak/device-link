using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DeviceLink.Hosting
{
    /// <summary>Shared Serilog logging setup for DeviceLink applications.</summary>
    public static class AppLoggingExtensions
    {
        /// <summary>Console log line template.</summary>
        public const string OutputTemplateConsole =
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

        /// <summary>File log line template (with date, milliseconds and properties).</summary>
        public const string OutputTemplateFile =
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}";

        /// <summary>Log directory: %LOCALAPPDATA%\DeviceLink\logs.</summary>
        public static string LogDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeviceLink", "logs");

        /// <summary>
        /// Plugs in Serilog (console + file {appName}-yyyyMMdd.log) as the ILogger provider.
        /// Levels are read from the "Serilog" section of appsettings.json.
        /// Works for both HostApplicationBuilder and WebApplicationBuilder.
        /// </summary>
        public static TBuilder AddDeviceLinkLogging<TBuilder>(this TBuilder builder, string appName)
            where TBuilder : IHostApplicationBuilder
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("App", appName)
                .WriteTo.Console(outputTemplate: OutputTemplateConsole)
                .WriteTo.File(Path.Combine(LogDirectory, $"{appName}-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: OutputTemplateFile)
                .CreateLogger();

            builder.Logging.ClearProviders();
            builder.Services.AddSerilog();
            return builder;
        }

        /// <summary>Logs unhandled AppDomain exceptions and unobserved task exceptions.</summary>
        public static void HookUnhandledExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
                Log.CloseAndFlush();
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception");
                e.SetObserved();
            };
        }
    }
}
