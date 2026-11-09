using Avalonia.Controls;
using Avalonia.Threading;
using DeviceLink.Protocol;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop;

public partial class MainWindow : Window
{
    private const int Port = 5000;
    private const string StationId = "station-1";
    private const string ApiAddress = "http://localhost:5100";

    private readonly CancellationTokenSource _cts = new();
    private readonly ApiForwarder _forwarder = new(ApiAddress, StationId);
    private readonly DroneListener _listener;
    private DateTime _lastPointAt = DateTime.MinValue;
    public MainWindow()
    {
        InitializeComponent();
        StationText.Text = $"Станція {StationId}, порт {Port}";
        _listener = new DroneListener(new IPEndPoint(IPAddress.Loopback, Port), OnTelemetry);

        HoldButton.Click += (_, _) => Send(CommandCode.Hold);
        ResumeButton.Click += (_, _) => Send(CommandCode.Resume);
        DestroyButton.Click += (_, _) => Send(CommandCode.Destroy);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => LinkText.Text =
            DateTime.Now - _lastPointAt < TimeSpan.FromSeconds(3) ? "Дрон: на зв'язку" : "Дрон: немає даних";
        timer.Start();

        Opened += (_, _) =>
        {
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
}