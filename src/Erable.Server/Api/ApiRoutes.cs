namespace Erable.Server.Api;

public static class ApiRoutes
{
    /// <summary>Versioned REST API (docs/architecture.md §8.1).</summary>
    public const string V1 = "/api/v1";

    /// <summary>Path prefixes that never fall back to the SPA shell.</summary>
    public static readonly IReadOnlyList<string> NonSpaPrefixes = ["/api", "/_test", "/hubs"];
}
