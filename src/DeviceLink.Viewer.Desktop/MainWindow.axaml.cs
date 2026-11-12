using Avalonia.Controls;
using DeviceLink.Api.Protos;
using Grpc.Core;
using Grpc.Net.Client;
using Mapsui;
using Mapsui.Projections;
using Mapsui.Tiling;
using Mapsui.UI.Avalonia;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
namespace DeviceLink.Viewer.Desktop;

public partial class MainWindow : Window
{
    private readonly DroneMapLayer _drones = new();
    private readonly ObservableCollection<string> _stations = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly MapControl _mapControl = new();
    public MainWindow()
    {
        InitializeComponent();

        Mapsui.Logging.Logger.LogDelegate += (level, message, ex) =>
    System.Diagnostics.Debug.WriteLine($"[Mapsui {level}] {message} {ex}");

        var map = new Map();
        map.Layers.Add(OpenStreetMap.CreateTileLayer("DeviceLink-Viewer"));
        map.Layers.Add(_drones.TrailLayer);
        map.Layers.Add(_drones.Layer);
        var (x, y) = SphericalMercator.FromLonLat(30.5234, 50.4501);
        map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), 5);

        _mapControl.Map = map;  
        MapHost.Content = _mapControl;

        StationsList.ItemsSource = _stations;

        Opened += (_, _) => _ = SubscribeAsync("http://localhost:5100", _cts.Token);
        Closing += (_, _) => _cts.Cancel();

        StationsList.SelectionChanged += (_, _) =>
        {
            _drones.Select(StationsList.SelectedItem as string);
            _mapControl.Refresh();
        };
    }

    private async Task SubscribeAsync(string apiAddress, CancellationToken ct)
    {
        using var channel = GrpcChannel.ForAddress(apiAddress);
        var client = new TelemetryService.TelemetryServiceClient(channel);
        try
        {
            while (true)
            {
                try
                {
                    using var call = client.SubscribeTelemetry(new SubscribeRequest(), cancellationToken: ct);
                    Title = "DeviceLink Viewer";
                    await foreach (var point in call.ResponseStream.ReadAllAsync(ct))
                    {
                       // Title = $"---{point.StationId}: {point.LatE7 / 1e7:F5}, {point.LonE7 / 1e7:F5}";
                        if (_drones.Update(point))
                        {
                            _stations.Add(point.StationId);
                            
                        }
                        _mapControl.Refresh();
                    }
                }
                catch (RpcException ex) when (!ct.IsCancellationRequested)
                {
                    Title = $"DeviceLink Viewer Ч Api недоступний ({ex.StatusCode}), повтор через 2 с";
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // в≥кно закрили
        }
    }
}