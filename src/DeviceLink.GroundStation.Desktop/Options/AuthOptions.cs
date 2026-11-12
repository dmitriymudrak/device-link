using System;
using System.ComponentModel.DataAnnotations;

namespace DeviceLink.GroundStation.Desktop.Options
{
    public sealed class AuthOptions
    {
        [Required]
        public string Authority { get; set; } = "";
        [Required]
        public string ClientId { get; set; } = "";
        [Required]
        public string RedirectUrl { get; set; } = "";
        [Required]
        public string PostLogoutRedirectUri { get; set; } = "";
        [Range(1024, 65535)] 
        public int RedirectPort { get; init; }
        public bool RequireHttps { get; init; } = true;
    }
}