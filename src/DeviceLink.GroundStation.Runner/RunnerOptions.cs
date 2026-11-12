using System.ComponentModel.DataAnnotations;

namespace DeviceLink.GroundStation.Runner
{
    /// <summary>Station settings ("Station" section): drone endpoint, Api address, station id and credentials.</summary>
    public sealed class RunnerOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string Section = "Station";

        /// <summary>IP address the drone listener binds to; use 0.0.0.0 inside a container.</summary>
        [Required]
        public string ListenAddress { get; init; } = "127.0.0.1";

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
        /// Static access token with the telemetry-sender role, used when <see cref="Auth"/> is not configured
        /// (e.g. via the Station__AccessToken env var).
        /// </summary>
        public string? AccessToken { get; init; }

        /// <summary>OAuth2 client credentials; when ClientId is set the Runner obtains tokens itself.</summary>
        public ClientCredentialsOptions Auth { get; init; } = new();
    }

    /// <summary>OAuth2 client credentials grant settings ("Station:Auth" section).</summary>
    public sealed class ClientCredentialsOptions
    {
        /// <summary>Token endpoint URL, e.g. http://keycloak:8080/realms/device-link-realm/protocol/openid-connect/token.</summary>
        public string? TokenEndpoint { get; init; }

        /// <summary>Confidential client id with a service account; empty disables client credentials.</summary>
        public string? ClientId { get; init; }

        /// <summary>Client secret of the confidential client.</summary>
        public string? ClientSecret { get; init; }

        /// <summary>Whether client credentials are configured.</summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
    }
}
