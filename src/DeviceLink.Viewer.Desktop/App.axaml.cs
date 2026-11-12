using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DeviceLink.Hosting;
using DeviceLink.Viewer.Desktop.Options;
using DeviceLink.Viewer.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Threading.Tasks;

namespace DeviceLink.Viewer.Desktop;

/// <summary>Viewer Avalonia app; starts the Generic Host with DI, logging and options.</summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>Loads the application XAML.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Builds and starts the host, registers services and shows the main window.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppLoggingExtensions.HookUnhandledExceptions();

            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = desktop.Args,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.AddDeviceLinkLogging("Viewer");

            builder.Services.AddOptions<ViewerOptions>().BindConfiguration(ViewerOptions.Section)
                .ValidateDataAnnotations().ValidateOnStart();
            builder.Services.AddSingleton<TelemetrySubscriber>();
            builder.Services.AddSingleton<DroneMapLayer>();
            builder.Services.AddTransient<MainWindow>();

            _host = builder.Build();
            _host.Start();

            var services = _host.Services;
            RouteMapsuiLogs(services.GetRequiredService<ILoggerFactory>().CreateLogger("Mapsui"));

            var logger = services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Viewer starting");

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = services.GetRequiredService<MainWindow>();
            desktop.Exit += (_, _) => StopHost(logger);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Routes Mapsui internal logs to ILogger.</summary>
    private static void RouteMapsuiLogs(Microsoft.Extensions.Logging.ILogger logger)
    {
        Mapsui.Logging.Logger.LogDelegate += (level, message, ex) =>
        {
            var lvl = level switch
            {
                Mapsui.Logging.LogLevel.Error => LogLevel.Error,
                Mapsui.Logging.LogLevel.Warning => LogLevel.Warning,
                Mapsui.Logging.LogLevel.Information => LogLevel.Information,
                Mapsui.Logging.LogLevel.Debug => LogLevel.Debug,
                _ => LogLevel.Trace
            };
            logger.Log(lvl, ex, "{Message}", message);
        };
    }

    /// <summary>Stops the host (up to 5 s) on exit and flushes Serilog.</summary>
    private void StopHost(Microsoft.Extensions.Logging.ILogger logger)
    {
        if (_host is null) return;
        logger.LogInformation("Viewer exiting");
        try
        {
            Task.Run(() => _host.StopAsync(TimeSpan.FromSeconds(5))).GetAwaiter().GetResult();
        }
        finally
        {
            _host.Dispose();
            _host = null;
            Log.CloseAndFlush();
        }
    }
}
