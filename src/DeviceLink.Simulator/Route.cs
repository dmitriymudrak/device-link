using DeviceLink.Protocol;

namespace DeviceLink.Simulator
{
    /// <summary>Closed drone route: linear interpolation between waypoints.</summary>
    public sealed class Route
    {
        private readonly (double lat, double lon)[] _points;
        private readonly int _stepsPerSegment;
        private int _step;

        /// <summary>Sets the waypoints and the number of interpolation steps per segment.</summary>
        public Route((double lat, double lon)[] points, int stepsPerSegment)
        {
            _points = points;
            _stepsPerSegment = stepsPerSegment;
        }

        /// <summary>Returns the next route point; wraps to the start after the last waypoint.</summary>
        public Telemetry Next()
        {
            int segment = _step / _stepsPerSegment;
            double t = (double)(_step % _stepsPerSegment) / _stepsPerSegment;

            var from = _points[segment];
            var to = _points[(segment + 1) % _points.Length];

            _step = (_step+1) % (_stepsPerSegment * _points.Length);

            return Telemetry.FromDegress(
                from.lat + (to.lat - from.lat) * t,
                from.lon + (to.lon - from.lon) * t);
        }
    }
}
