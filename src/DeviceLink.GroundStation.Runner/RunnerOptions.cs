using System.ComponentModel.DataAnnotations;

namespace DeviceLink.GroundStation.Runner
{
    /// <summary>Station settings ("Station" section): drone port, Api address, station id.</summary>
    public sealed class RunnerOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string Section = "Station";

        /// <summary>Local TCP port where the station waits for the drone to connect.</summary>
        [Range(1, 65535)]
        public int Port { get; init; } = 5000;

        /// <summary>Address of the Api gRPC service that telemetry is forwarded to.</summary>
        [Required, Url]
        public string ApiAddress { get; init; } = "http://localhost:5100";

        /// <summary>Station id attached to telemetry sent to the Api.</summary>
        [Required]
        public string StationId { get; init; } = "station-1";

        /// <summary>
        /// Access token with the telemetry-sender role. The Runner has no interactive login,
        /// so the token is supplied externally (e.g. via the Station__AccessToken env var).
        /// </summary>
        public string? AccessToken { get; init; }
    }
}
