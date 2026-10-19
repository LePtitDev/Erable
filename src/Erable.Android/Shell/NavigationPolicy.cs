namespace Erable.Mobile.Shell;

/// <summary>What the WebView does with a link it is asked to follow.</summary>
public enum NavigationDecision
{
    /// <summary>A page of the server that is open: the WebView loads it.</summary>
    Stay,

    /// <summary>Another site, a mail or a phone link: the system opens it (browser, mail application…).</summary>
    External,

    /// <summary>Anything else (<c>file:</c>, <c>content:</c>, <c>javascript:</c>, <c>intent:</c>…): ignored.</summary>
    Block,
}

/// <summary>
/// The WebView never leaves the origin of the server it shows (F19.R2): the SPA, its API and its attachments live
/// there; everything else belongs to other applications or is refused. Pure, tested without Android.
/// </summary>
public static class NavigationPolicy
{
    public static NavigationDecision Decide(string serverOrigin, string url)
    {
        ArgumentNullException.ThrowIfNull(serverOrigin);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var target))
        {
            return NavigationDecision.Block;
        }

        switch (target.Scheme)
        {
            case "http":
            case "https":
                return Uri.TryCreate(serverOrigin, UriKind.Absolute, out var origin) && SameOrigin(origin, target)
                    ? NavigationDecision.Stay
                    : NavigationDecision.External;
            case "mailto":
            case "tel":
                return NavigationDecision.External;
            default:
                return NavigationDecision.Block;
        }
    }

    /// <summary>Scheme, host and port: <c>https://wiki.example.org</c> and <c>https://wiki.example.org:443/p/…</c> match.</summary>
    private static bool SameOrigin(Uri a, Uri b) =>
        string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase)
        && a.Port == b.Port;
}
