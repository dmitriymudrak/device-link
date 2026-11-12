using System;
using System.ComponentModel.DataAnnotations;

namespace DeviceLink.GroundStation.Desktop.Options
{
    /// <summary>Keycloak OIDC login settings ("Auth" section).</summary>
    public sealed class AuthOptions
    {
        /// <summary>OIDC provider address (Keycloak realm).</summary>
        [Required]
        public string Authority { get; set; } = "";
        [Required]
        public string ClientId { get; set; } = "";
        [Required]
        public string RedirectUrl { get; set; } = "";
        [Required]
        public string PostLogoutRedirectUri { get; set; } = "";
        /// <summary>Port of the local HttpListener that receives the redirect after browser login.</summary>
        [Range(1024, 65535)]
        public int RedirectPort { get; init; }
        /// <summary>Whether HTTPS is required for the provider discovery document.</summary>
        public bool RequireHttps { get; init; } = true;
    }
}