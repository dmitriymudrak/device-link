using Duende.IdentityModel.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeviceLink.GroundStation.Runner
{
    /// <summary>Supplies the Bearer token used for Api calls.</summary>
    public interface IAccessTokenProvider
    {
        /// <summary>Returns a currently valid access token.</summary>
        Task<string> GetAccessTokenAsync(CancellationToken ct = default);
    }

    /// <summary>Returns the static token from Station:AccessToken (or an empty string).</summary>
    public sealed class StaticAccessTokenProvider(IOptions<RunnerOptions> options) : IAccessTokenProvider
    {
        /// <summary>Returns the configured token as is.</summary>
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) =>
            Task.FromResult(options.Value.AccessToken ?? "");
    }

    /// <summary>Obtains and caches tokens via the OAuth2 client credentials grant, renewing them before expiry.</summary>
    public sealed class ClientCredentialsTokenProvider(
        HttpClient http,
        IOptions<RunnerOptions> options,
        ILogger<ClientCredentialsTokenProvider> logger) : IAccessTokenProvider
    {
        private static readonly TimeSpan RenewBefore = TimeSpan.FromSeconds(30);

        private readonly ClientCredentialsOptions _auth = options.Value.Auth;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private string? _token;
        private DateTimeOffset _expiresAt;

        /// <summary>Returns the cached token or requests a new one if it is about to expire (thread-safe).</summary>
        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            try
            {
                if (_token is not null && DateTimeOffset.UtcNow < _expiresAt - RenewBefore)
                    return _token;

                logger.LogDebug("Requesting access token for client {ClientId} from {Endpoint}",
                    _auth.ClientId, _auth.TokenEndpoint);

                var response = await http.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
                {
                    Address = _auth.TokenEndpoint,
                    ClientId = _auth.ClientId!,
                    ClientSecret = _auth.ClientSecret
                }, ct);

                if (response.IsError)
                {
                    logger.LogWarning("Token request failed: {Error} {Description}", response.Error, response.ErrorDescription);
                    throw new InvalidOperationException($"Token request failed: {response.Error}");
                }

                _token = response.AccessToken!;
                _expiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
                logger.LogInformation("Access token obtained, valid for {Seconds} s", response.ExpiresIn);
                return _token;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
