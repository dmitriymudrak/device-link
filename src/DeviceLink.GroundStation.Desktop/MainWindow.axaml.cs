using Avalonia.Controls;
using Avalonia.Threading;
using DeviceLink.GroundStation.Desktop.Auth;
using DeviceLink.GroundStation.Desktop.Services;
using DeviceLink.Protocol;
using Microsoft.Extensions.Logging;
using System;

namespace DeviceLink.GroundStation.Desktop;

/// <summary>Station main window: drone position, link status and command buttons.</summary>
public partial class MainWindow : Window
{
    private static readonly TimeSpan LinkTimeout = TimeSpan.FromSeconds(3);

    private readonly StationService? _station;
    private readonly ILogger<MainWindow>? _logger;
    private readonly DispatcherTimer _linkTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime _lastPointAt = DateTime.MinValue;
    private bool _linkUp;

    // For the Avalonia designer.
    public MainWindow() => InitializeComponent();

    /// <summary>Wires the command buttons and starts/stops the station together with the window.</summary>
    public MainWindow(StationService station, AuthService auth, ILogger<MainWindow> logger) : this()
    {
        _station = station;
        _logger = logger;

        StationText.Text = $"Станція {station.StationName}, порт {station.Port} — {auth.UserName}";

        HoldButton.Click += (_, _) => Send(CommandCode.Hold);
        ResumeButton.Click += (_, _) => Send(CommandCode.Resume);
        DestroyButton.Click += (_, _) => Send(CommandCode.Destroy);

        _linkTimer.Tick += (_, _) => UpdateLinkState();

        Opened += (_, _) =>
        {
            station.TelemetryReceived += OnTelemetry;
            station.Faulted += OnFaulted;
            station.Start();
            _linkTimer.Start();
            _logger.LogInformation("Main window opened");
        };

        Closing += (_, _) =>
        {
            _linkTimer.Stop();
            station.TelemetryReceived -= OnTelemetry;
            station.Faulted -= OnFaulted;
            station.Stop();
            _logger.LogInformation("Main window closing");
        };
    }

    /// <summary>Updates the link indicator from the last point time and logs changes.</summary>
    private void UpdateLinkState()
    {
        var up = DateTime.Now - _lastPointAt < LinkTimeout;
        LinkText.Text = up ? "Дрон: на зв'язку" : "Дрон: немає даних";

        if (up != _linkUp)
        {
            _linkUp = up;
            if (up) _logger?.LogInformation("Drone link up");
            else _logger?.LogWarning("Drone link lost: no telemetry for {Timeout}", LinkTimeout);
        }
    }

    /// <summary>Shows the new drone position, marshalling to the UI thread.</summary>
    private void OnTelemetry(byte seq, Telemetry t) =>
        Dispatcher.UIThread.Post(() =>
        {
            _lastPointAt = DateTime.Now;
            PositionText.Text = $"seq {seq,3}   {t.Latitude:F6}, {t.Longitude:F6}";
        });

    /// <summary>Shows a station background loop error on the UI thread.</summary>
    private void OnFaulted(string name, Exception ex) =>
        Dispatcher.UIThread.Post(() => LogText.Text = $"{name}: {ex.Message}");

    /// <summary>Sends a command to the drone and shows the result in the log line.</summary>
    private void Send(CommandCode code)
    {
        var sent = _station!.TrySend(code);
        LogText.Text = sent
            ? $"{DateTime.Now:HH:mm:ss} надіслано: {code}"
            : $"{DateTime.Now:HH:mm:ss} дрон не підключений";
    }
}
