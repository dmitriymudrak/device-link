using DeviceLink.Api.Protos;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using NetTopologySuite.Geometries;
using System.Collections.Generic;
using System.Linq;

namespace DeviceLink.Viewer.Desktop
{
    public sealed class DroneMapLayer
    {
        private const int MaxTrailPoints = 300;
        private readonly Dictionary<string, PointFeature> _drones = new();
        private readonly Dictionary<string, Queue<Coordinate>> _trails = new();
        private string? _selected;
        public MemoryLayer Layer { get; } = new()
        {
            Name = "Дрони",
            Style = new SymbolStyle { SymbolScale = 0.2, Fill = new Brush(Color.Red) },
        };

        public MemoryLayer TrailLayer { get; } = new()
        {
            Name = "Слід",
            Style = new VectorStyle { Line = new Pen(Color.Orange, 3) }
        };

        public void Select(string? stationId)
        {
            _selected = stationId;
            RefreshTrail();
        }

        private void RefreshTrail()
        {
            var features = new List<IFeature>();
            if (_selected is not null && _trails.TryGetValue(_selected, out var trail) && trail.Count >= 2)
                features.Add(new GeometryFeature(new LineString(trail.ToArray())));

            TrailLayer.Features = features;
            TrailLayer.DataHasChanged();
        }

        public bool Update(TelemetryPoint point)
        {
            bool isNew = !_drones.ContainsKey(point.StationId);

            var (x, y) = SphericalMercator.FromLonLat(point.LonE7 / 1e7, point.LatE7 / 1e7);
            _drones[point.StationId] = new PointFeature(x, y);

            if (!_trails.TryGetValue(point.StationId, out var trail))
                _trails[point.StationId] = trail = new Queue<Coordinate>();
            trail.Enqueue(new Coordinate(x, y));
            if (trail.Count > MaxTrailPoints)
                trail.Dequeue();

            Layer.Features = _drones.Values.ToList();
            Layer.DataHasChanged();

            if (point.StationId == _selected)
                RefreshTrail();

            return isNew;
        }
    }
}
