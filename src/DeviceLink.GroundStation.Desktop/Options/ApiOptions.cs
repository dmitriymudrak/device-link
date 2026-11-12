using System.ComponentModel.DataAnnotations;

namespace DeviceLink.GroundStation.Desktop.Options
{
    public sealed class ApiOptions
    {
        [Required, Url] 
        public string Address { get; init; } = "";
        [Required]
        public string StationName { get; init; } = "";
        [Range(1024, 65535)]
        public int Port { get; init; }
    }
}
