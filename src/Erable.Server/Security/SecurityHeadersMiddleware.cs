using Microsoft.Net.Http.Headers;

namespace Erable.Server.Security;

/// <summary>
/// Defence in depth on every response (docs/architecture.md §9): no MIME sniffing, no referrer sent to other sites,
/// no framing by another origin (clickjacking). The SPA shell adds its own <see cref="SpaContentSecurityPolicy"/>;
/// attachments keep theirs. Same-origin framing stays allowed: a PDF attachment is shown in an iframe of the page.
/// </summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Policy of the SPA shell. Styles allow <c>'unsafe-inline'</c>: Radix UI and CodeMirror inject style elements at
    /// run time. Images from <c>http</c>/<c>https</c> stay allowed, because pages may embed external images.
    /// </summary>
    public const string SpaContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: http: https:; "
        + "font-src 'self' data:; connect-src 'self'; frame-src 'self'; object-src 'none'; base-uri 'none'; "
        + "form-action 'self'; frame-ancestors 'self'";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers[HeaderNames.XFrameOptions] = "SAMEORIGIN";
        headers["Referrer-Policy"] = "no-referrer";
        return next(context);
    }
}
