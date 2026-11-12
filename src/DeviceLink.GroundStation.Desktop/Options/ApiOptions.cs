using System.ComponentModel.DataAnnotations;

namespace DeviceLink.GroundStation.Desktop.Options
{
    /// <summary>Api connection and station settings ("Api" section).</summary>
    public sealed class ApiOptions
    {
        /// <summary>Address of the Api gRPC service.</summary>
        [Required, Url]
        public string Address { get; init; } = "";
        [Required]
        public string StationName { get; init; } = "";
        /// <summary>Local TCP port where the station waits for the drone to connect.</summary>
        [Range(1024, 65535)]
        public int Port { get; init; }
    }
}
