using DeviceLink.GroundStation.Desktop.Options;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop.Auth
{
    public sealed class AuthService
    {
        private readonly OidcClient _oidc;
        readonly SemaphoreSlim _lock = new(1, 1);
        string? _accessToken, _refreshToken, _idToken;
        DateTimeOffset _expiresAt;
        public AuthService(IOptions<AuthOptions> options)
        {
            var o = options.Value;

            _oidc = new OidcClient(new OidcClientOptions
            {
                Authority = o.Authority,
                ClientId = o.ClientId,
                RedirectUri = o.RedirectUrl,
                PostLogoutRedirectUri = o.PostLogoutRedirectUri,
                Scope = "openid profile email offline_access",
                Browser = new LoopbackBrowser(o.RedirectPort),
                Policy = { Discovery = { RequireHttps = o.RequireHttps } }
            });
        }

        public ClaimsPrincipal? User { get; private set; }

        public async Task<bool> LoginAsync(CancellationToken ct = default)
        {
            var r = await _oidc.LoginAsync(new LoginRequest(), ct);
            if (r.IsError) return false;

            User = r.User;
            _idToken = r.IdentityToken;
            Store(r.AccessToken, r.AccessTokenExpiration, r.RefreshToken);
            return true;
        }

        public async Task<bool> TryRestoreAsync()
        {
            var saved = TokenStore.Load();
            if (saved is null) return false;

            var r = await _oidc.RefreshTokenAsync(saved);
            if (r.IsError) { TokenStore.Clear(); return false; }

            _idToken = r.IdentityToken;
            Store(r.AccessToken, r.AccessTokenExpiration, r.RefreshToken);

            var info = await _oidc.GetUserInfoAsync(r.AccessToken);
            User = new ClaimsPrincipal(new ClaimsIdentity(info.Claims, "oidc", "preferred_username", "roles"));
            return true;
        }

        public async Task<string> GetAccessTokenAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
                    return _accessToken;

                var r = await _oidc.RefreshTokenAsync(_refreshToken!);
                if (r.IsError) throw new InvalidOperationException("Session expired");

                Store(r.AccessToken, r.AccessTokenExpiration, r.RefreshToken);
                return _accessToken!;
            }
            finally { _lock.Release(); }
        }

        public async Task LogoutAsync()
        {
            await _oidc.LogoutAsync(new LogoutRequest { IdTokenHint = _idToken });
            TokenStore.Clear();
            User = null; _accessToken = _refreshToken = _idToken = null;
        }

        void Store(string access, DateTimeOffset expiresAt, string refresh)
        {
            _accessToken = access;
            _expiresAt = expiresAt;
            _refreshToken = refresh;
            TokenStore.Save(refresh);
        }
    }
}
