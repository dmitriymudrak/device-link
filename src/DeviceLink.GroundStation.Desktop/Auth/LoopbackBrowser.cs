using DeviceLink.GroundStation.Desktop.Options;
using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop.Auth
{
    /// <summary>Opens the system browser and waits for the redirect to http://127.0.0.1:{port}/.</summary>
    public sealed class LoopbackBrowser(IOptions<AuthOptions> options, ILogger<LoopbackBrowser> logger) : IBrowser
    {
        private readonly int _port = options.Value.RedirectPort;

        /// <summary>Opens the browser at StartUrl and returns the URL of the first request hitting the loopback port.</summary>
        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken ct = default)
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{_port}/");

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                logger.LogError(ex, "Cannot listen for login callback on port {Port}", _port);
                return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = ex.Message };
            }

            logger.LogInformation("Opening system browser for login, callback port {Port}", _port);
            Process.Start(new ProcessStartInfo(options.StartUrl) { UseShellExecute = true });

            try
            {
                var ctx = await listener.GetContextAsync().WaitAsync(ct);
                var html = "<html><body>Готово, поверніться в застосунок.</body></html>"u8.ToArray();
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.OutputStream.WriteAsync(html, ct);
                ctx.Response.Close();

                logger.LogDebug("Login callback received");
                return new BrowserResult { ResultType = BrowserResultType.Success, Response = ctx.Request.Url!.ToString() };
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Login in browser cancelled by user");
                return new BrowserResult { ResultType = BrowserResultType.UserCancel };
            }
        }
    }
}
