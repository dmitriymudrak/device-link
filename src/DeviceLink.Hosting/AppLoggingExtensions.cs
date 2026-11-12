using Microsoft.Extensions.Hosting;
using Serilog;

namespace DeviceLink.Hosting
{
    public static class AppLoggingExtensions
    {
        public static HostApplicationBuilder? AddDeviceLinkLogging(this HostApplicationBuilder? builder, string appName)
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeviceLink", "logs");

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)          // рівні з appsettings.json
                .Enrich.FromLogContext()
                .Enrich.WithProperty("App", appName)
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(Path.Combine(logDir, $"{appName}-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}")
                .CreateLogger();

            builder.Services.AddSerilog(); 
            return builder;
        }

        public static void HookUnhandledExceptions()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception");
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception");
                e.SetObserved();
            };
        }
    }
}