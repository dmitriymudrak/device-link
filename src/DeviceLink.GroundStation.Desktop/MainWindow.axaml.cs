using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DeviceLink.GroundStation.Desktop.Auth;
using DeviceLink.GroundStation.Desktop.Options;
using DeviceLink.Protocol;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop;

public partial class MainWindow : Window
{

    private readonly CancellationTokenSource _cts = new();
    private readonly ApiForwarder _forwarder;
    private readonly DroneListener _listener;
    private DateTime _lastPointAt = DateTime.MinValue;
    
    public MainWindow(IOptions<ApiOptions> apiOptions, AuthService auth)
    {
        InitializeComponent();
        StationText.Text = $"Станція {apiOptions.Value.StationName}, порт {apiOptions.Value.Port}";
        _listener = new DroneListener(new IPEndPoint(IPAddress.Loopback, apiOptions.Value.Port), OnTelemetry);
        _forwarder = new(apiOptions.Value.Address, apiOptions.Value.StationName, () => auth.GetAccessTokenAsync());

        HoldButton.Click += (_, _) => Send(CommandCode.Hold);
        ResumeButton.Click += (_, _) => Send(CommandCode.Resume);
        DestroyButton.Click += (_, _) => Send(CommandCode.Destroy);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => LinkText.Text =
            DateTime.Now - _lastPointAt < TimeSpan.FromSeconds(3) ? "Дрон: на зв'язку" : "Дрон: немає даних";
        timer.Start();

        Opened += async (_, _) =>
        {
            if (!await auth.TryRestoreAsync())                    
            {
                var ok = await new LoginWindow(auth).ShowDialog<bool>(this);
                if (!ok) { Close(); return; }
            }

            if (!auth.User!.FindAll("roles").Any(r => r.Value == "telemetry-sender"))
            {
                await auth.LogoutAsync();    
                await MessageAsync("Немає доступу до станції. Потрібна роль telemetry-sender.");
                Close();
                return;
            }
            StationText.Text += $" — {auth.User?.FindFirst("preferred_username")?.Value}";
            _ = RunGuardedAsync(_listener.RunAsync, "Дрон");
            _ = RunGuardedAsync(_forwarder.RunAsync, "Api");
        };
        Closing += (_, _) => _cts.Cancel();
    }
    private void OnTelemetry(byte seq, Telemetry t)
    {
        _forwarder.Enqueue(seq, t);
        Dispatcher.UIThread.Post(() =>
        {
            _lastPointAt = DateTime.Now;
            PositionText.Text = $"seq {seq,3}   {t.Latitude:F6}, {t.Longitude:F6}";
        });
    }

    private void Send(CommandCode code) =>
        LogText.Text = _listener.TrySend(code)
            ? $"{DateTime.Now:HH:mm:ss} надіслано: {code}"
            : $"{DateTime.Now:HH:mm:ss} дрон не підключений";

    private async Task RunGuardedAsync(Func<CancellationToken, Task> run, string name)
    {
        try
        {
            await Task.Run(() => run(_cts.Token));
        }
        catch (OperationCanceledException)
        {
            // вікно закрили
        }
        catch (Exception ex)
        {
            LogText.Text = $"{name}: {ex.Message}";
        }
    }
    private Task MessageAsync(string text)
    {
        var w = new Window
        {
            Title = "Доступ заборонено",
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
            {
                new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                new Button { Content = "OK" }
            }
            }
        };
        ((Button)((StackPanel)w.Content).Children[1]).Click += (_, _) => w.Close();
        return w.ShowDialog(this);
    }
}