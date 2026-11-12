using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DeviceLink.GroundStation.Desktop.Auth;
using DeviceLink.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop;

/// <summary>Station application: starts the Generic Host, performs login and opens the main window.</summary>
public partial class App : Application
{
    private const string RequiredRole = "telemetry-sender";

    private IHost? _host;

    /// <summary>Loads the application XAML.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Builds and starts the host with logging and DI, then starts the UI asynchronously.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppLoggingExtensions.HookUnhandledExceptions();

            // appsettings.json sits next to the exe, not in the process working directory.
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = desktop.Args,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.AddDeviceLinkLogging("Station");
            builder.Services.AddGroundStationDesktop();

            _host = builder.Build();
            _host.Start();

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => StopHost();

            _ = StartAsync(desktop, _host.Services);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Restores the session or shows login, checks the role and opens MainWindow; otherwise shuts down.</summary>
    private static async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<App>>();
        try
        {
            logger.LogInformation("Station desktop starting");
            var auth = services.GetRequiredService<AuthService>();

            var ok = await auth.TryRestoreAsync();
            if (!ok)
            {
                var login = services.GetRequiredService<Func<LoginWindow>>()();
                ok = await login.ShowAndWaitAsync();
            }

            if (!ok)
            {
                logger.LogInformation("User is not authenticated, shutting down");
                desktop.Shutdown();
                return;
            }

            if (!auth.HasRole(RequiredRole))
            {
                logger.LogWarning("User {User} has no role {Role}, access denied", auth.UserName, RequiredRole);
                await auth.LogoutAsync();
                await Dialogs.ShowMessageAsync("Доступ заборонено",
                    $"Немає доступу до станції. Потрібна роль {RequiredRole}.");
                desktop.Shutdown();
                return;
            }

            var main = services.GetRequiredService<MainWindow>();
            desktop.MainWindow = main;
            main.Closed += (_, _) => desktop.Shutdown();
            main.Show();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Station desktop failed to start");
            desktop.Shutdown(1);
        }
    }

    /// <summary>Stops and disposes the host and flushes Serilog on exit.</summary>
    private void StopHost()
    {
        if (_host is null) return;
        try
        {
            // StopAsync off the UI thread to avoid a SynchronizationContext deadlock.
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
