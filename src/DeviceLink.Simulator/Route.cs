using DeviceLink.Protocol;

namespace DeviceLink.Simulator
{
    public sealed class Route
    {
        private readonly (double lat, double lon)[] _points;
        private readonly int _stepsPerSegment;
        private int _step;

        public Route((double lat, double lon)[] points, int stepsPerSegment)
        {
            _points = points;
            _stepsPerSegment = stepsPerSegment;
        }

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
