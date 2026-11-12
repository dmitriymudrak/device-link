using Duende.IdentityModel.OidcClient.Browser;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceLink.GroundStation.Desktop.Auth
{
    internal class LoopbackBrowser(int port) : IBrowser
    {
        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken ct = default)
        {
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            Process.Start(new ProcessStartInfo(options.StartUrl) { UseShellExecute = true });

            try
            {
                var ctx = await listener.GetContextAsync().WaitAsync(ct);
                var html = "<html><body>Готово, поверніться в застосунок.</body></html>"u8.ToArray();
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.OutputStream.WriteAsync(html, ct);
                ctx.Response.Close();

                return new BrowserResult { ResultType = BrowserResultType.Success, Response = ctx.Request.Url!.ToString() };
            }
            catch (OperationCanceledException)
            {
                return new BrowserResult { ResultType = BrowserResultType.UserCancel };
            }
        }
    }
}
