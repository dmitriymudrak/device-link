using System;
using System.ComponentModel.DataAnnotations;

namespace DeviceLink.Viewer.Desktop.Options
{
    /// <summary>Viewer settings from the "Viewer" configuration section.</summary>
    public sealed class ViewerOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string Section = "Viewer";

        /// <summary>Address of the gRPC Api.</summary>
        [Required, Url]
        public string ApiAddress { get; init; } = "http://localhost:5100";

        /// <summary>Delay before reconnecting to the Api.</summary>
        [Range(typeof(TimeSpan), "00:00:00.100", "00:10:00")]
        public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(2);

        /// <summary>Station filter; empty means all stations.</summary>
        public string StationId { get; init; } = "";
    }
}
