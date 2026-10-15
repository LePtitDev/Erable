using System.Text.RegularExpressions;
using Erable.Server.Projects;
using Microsoft.Net.Http.Headers;

namespace Erable.Server.Spa;

/// <summary>
/// Serves the SPA shell (<c>index.html</c>) with the user's language and theme on <c>&lt;html&gt;</c>, so that the
/// first paint is already right (F15.R3) and the browser never stores durable state itself (decision P-03).
/// </summary>
internal static partial class SpaIndexEndpoint
{
    private const string IndexFile = "index.html";

    public static async Task<IResult> HandleAsync(
        HttpContext context, IWebHostEnvironment environment, UserProfile profile)
    {
        var file = environment.WebRootFileProvider.GetFileInfo(IndexFile);
        if (!file.Exists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "SPA not built",
                detail: "Run `pnpm -C src/web build` to produce the web client.");
        }

        string html;
        await using (var stream = file.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            html = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        var preferences = profile.GetPreferences();
        var theme = preferences.Theme.ToString().ToLowerInvariant();
        // Only known values reach the markup: Normalize() keeps the language to the interface list.
        var lang = preferences.Language is { } language ? $" lang=\"{language}\"" : string.Empty;
        html = HtmlTag().Replace(html, $"<html{lang} data-theme=\"{theme}\"", 1);

        context.Response.Headers[HeaderNames.CacheControl] = "no-cache";
        return TypedResults.Content(html, "text/html; charset=utf-8");
    }

    [GeneratedRegex("<html(?=[\\s>])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTag();
}
