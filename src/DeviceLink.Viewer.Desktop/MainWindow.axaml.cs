using Avalonia.Controls;
using Avalonia.Threading;
using DeviceLink.Api.Protos;
using DeviceLink.Viewer.Desktop.Services;
using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Avalonia;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace DeviceLink.Viewer.Desktop;

/// <summary>Main window: map with drones and the station list.</summary>
public partial class MainWindow : Window
{
    private const string BaseTitle = "DeviceLink Viewer";

    private readonly DroneMapLayer _drones = new();
    private readonly ObservableCollection<string> _stations = new();
    private readonly MapControl _mapControl = new();
    private readonly ILogger<MainWindow>? _logger;

    // For the Avalonia designer.
    /// <summary>Creates the window without dependencies (for the designer).</summary>
    public MainWindow() => InitializeComponent();

    /// <summary>Sets up the OSM map, station list and telemetry subscription for the window's lifetime.</summary>
    public MainWindow(TelemetrySubscriber subscriber, DroneMapLayer drones, ILogger<MainWindow> logger) : this()
    {
        _drones = drones;
        _logger = logger;

        var map = new Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer("DeviceLink-Viewer"));
        map.Layers.Add(_drones.TrailLayer);
        map.Layers.Add(_drones.Layer);
        var (x, y) = SphericalMercator.FromLonLat(30.5234, 50.4501);
        map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 5);

        _mapControl.Map = map;
        MapHost.Content = _mapControl;

        StationsList.ItemsSource = _stations;
        StationsList.SelectionChanged += (_, _) =>
        {
            var station = StationsList.SelectedItem as string;
            _logger.LogInformation("Station {Station} selected", station);
            _drones.Select(station);
            _mapControl.Refresh();
        };

        Opened += (_, _) =>
        {
            subscriber.PointReceived += OnPoint;
            subscriber.StatusChanged += OnStatus;
            subscriber.Start();
            _logger.LogInformation("Main window opened");
        };

        Closing += (_, _) =>
        {
            subscriber.PointReceived -= OnPoint;
            subscriber.StatusChanged -= OnStatus;
            subscriber.Stop();
            _logger.LogInformation("Main window closing");
        };
    }

    /// <summary>Updates the map with the point on the UI thread; adds new stations to the list.</summary>
    private void OnPoint(TelemetryPoint point) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_drones.Update(point))
            {
                _logger?.LogInformation("New station {Station} on map", point.StationId);
                _stations.Add(point.StationId);
            }
            _mapControl.Refresh();
        });

    /// <summary>Shows the connection status in the window title.</summary>
    private void OnStatus(string? error) =>
        Dispatcher.UIThread.Post(() =>
            Title = error is null ? BaseTitle : $"{BaseTitle} — {error}, повтор…");
}
