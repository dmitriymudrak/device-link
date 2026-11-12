using DeviceLink.GroundStation.Desktop.Options;
using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop.Auth
{
    /// <summary>OIDC login via Keycloak: interactive login, session restore and access token refresh.</summary>
    public sealed class AuthService
    {
        private readonly OidcClient _oidc;
        private readonly TokenStore _tokenStore;
        private readonly ILogger<AuthService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private string? _accessToken, _refreshToken, _idToken;
        private DateTimeOffset _expiresAt;

        /// <summary>Creates the OidcClient from Auth options and the given browser.</summary>
        public AuthService(IOptions<AuthOptions> options, IBrowser browser, TokenStore tokenStore, ILogger<AuthService> logger)
        {
            var o = options.Value;
            _tokenStore = tokenStore;
            _logger = logger;

            _oidc = new OidcClient(new OidcClientOptions
            {
                Authority = o.Authority,
                ClientId = o.ClientId,
                RedirectUri = o.RedirectUrl,
                PostLogoutRedirectUri = o.PostLogoutRedirectUri,
                Scope = "openid profile email offline_access",
                Browser = browser,
                Policy = { Discovery = { RequireHttps = o.RequireHttps } }
            });
        }

        /// <summary>Current user; null if not logged in.</summary>
        public ClaimsPrincipal? User { get; private set; }

        public string? UserName => User?.FindFirst("preferred_username")?.Value;

        /// <summary>Whether the current user has the role in the "roles" claim.</summary>
        public bool HasRole(string role) => User?.FindAll("roles").Any(r => r.Value == role) == true;

        /// <summary>Interactive browser login; true on success, false if cancelled or failed.</summary>
        public async Task<bool> LoginAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Interactive login started");
            LoginResult r;
            try
            {
                r = await _oidc.LoginAsync(new LoginRequest(), ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Interactive login cancelled");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Identity provider is unreachable");
                return false;
            }

            if (r.IsError)
            {
                _logger.LogWarning("Login failed: {Error} {Description}", r.Error, r.ErrorDescription);
                return false;
            }

            User = r.User;
            _idToken = r.IdentityToken;
            Store(r.AccessToken, r.AccessTokenExpiration, r.RefreshToken);
            _logger.LogInformation("User {User} logged in", UserName);
            return true;
        }

        /// <summary>Restores the session from the saved refresh token without a browser; true on success.</summary>
        public async Task<bool> TryRestoreAsync()
        {
            var saved = _tokenStore.Load();
            if (saved is null) return false;

            try
            {
                var r = await _oidc.RefreshTokenAsync(saved);
                if (r.IsError)
                {
                    _logger.LogInformation("Saved session is no longer valid: {Error}", r.Error);
                    _tokenStore.Clear();
                    return false;
                }

                _idToken = r.IdentityToken;
                Store(r.AccessToken, r.AccessTokenExpiration, r.RefreshToken);

                var info = await _oidc.GetUserInfoAsync(r.AccessToken);
                if (info.IsError)
                {
                    _logger.LogWarning("Failed to load user info: {Error}", info.Error);
                    return false;
                }

                User = new ClaimsPrincipal(new ClaimsIdentity(info.Claims, "oidc", "preferred_username", "roles"));
                _logger.LogInformation("Session restored for user {User}", UserName);
                return true;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Identity provider is unreachable, session not restored");
                return false;
            }
        }

        /// <summary>Returns a valid access token, refreshing it if needed (thread-safe).</summary>
        public async Task<string> GetAccessTokenAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
                    return _accessToken;

                if (_refreshToken is null)
                    throw new InvalidOperationException("User is not logged in");

                _logger.LogDebug("Access token expired, refreshing");
                var r = await _oidc.RefreshTokenAsync(_refreshToken);
                if (r.IsError)
                {
                    _logger.LogWarning("Token refresh failed: {Error}", r.Error);
                    throw new InvalidOperationException("Session expired");
                }

                Store(r.AccessToken, r.AccessTokenExpiration, r.RefreshToken);
                return _accessToken!;
            }
            finally { _lock.Release(); }
        }

        /// <summary>Logs out at the provider and clears the local session even if that fails.</summary>
        public async Task LogoutAsync()
        {
            _logger.LogInformation("Logging out user {User}", UserName);
            try
            {
                await _oidc.LogoutAsync(new LogoutRequest { IdTokenHint = _idToken });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Logout at identity provider failed, clearing local session anyway");
            }

            _tokenStore.Clear();
            User = null;
            _accessToken = _refreshToken = _idToken = null;
        }

        /// <summary>Keeps tokens in memory and persists the refresh token to TokenStore.</summary>
        private void Store(string access, DateTimeOffset expiresAt, string refresh)
        {
            _accessToken = access;
            _expiresAt = expiresAt;
            _refreshToken = refresh;
            _tokenStore.Save(refresh);
            _logger.LogDebug("Access token stored, expires at {ExpiresAt}", expiresAt);
        }
    }
}
