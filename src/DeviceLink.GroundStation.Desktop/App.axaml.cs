using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DeviceLink.GroundStation.Desktop.Auth;
using DeviceLink.GroundStation.Desktop.Options;
using DeviceLink.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop;

public partial class App : Application
{
    public static AuthService Auth { get; }  
    public static IServiceProvider Services { get; private set; } = null;
    private IHost? _host;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddDeviceLinkLogging("Station");
        AppLoggingExtensions.HookUnhandledExceptions();
        builder.Services.AddOptions<AuthOptions>().BindConfiguration("Auth")
            .ValidateDataAnnotations().ValidateOnStart();

        builder.Services.AddOptions<ApiOptions>().BindConfiguration("Api")
            .ValidateDataAnnotations().ValidateOnStart();

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddTransient<LoginWindow>();
        builder.Services.AddTransient<MainWindow>();

        _host = builder.Build();
        _host.Start();

        Services = _host.Services;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => _host.Dispose();

            _ = StartAsync(desktop); 
        }

        base.OnFrameworkInitializationCompleted();
    }

    static async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var auth = Services.GetRequiredService<AuthService>();
        var ok = await Auth.TryRestoreAsync();
        if (!ok)
        {
            var login = new LoginWindow();
            var closed = new TaskCompletionSource();
            login.Closed += (_, _) => closed.SetResult();
            login.Show();
            await closed.Task;
            ok = login.Success;
        }

        if (!ok) { desktop.Shutdown(); return; }
    }
}