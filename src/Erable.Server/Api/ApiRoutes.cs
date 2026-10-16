namespace Erable.Server.Api;

public static class ApiRoutes
{
    /// <summary>Versioned REST API (docs/architecture.md §8.1).</summary>
    public const string V1 = "/api/v1";

    /// <summary>The SignalR hub of the projects (events, presence).</summary>
    public const string ProjectHub = "/hubs/project";

    public const string OpenApiDocumentName = "v1";

    /// <summary>Route of the OpenAPI document; <c>/api/openapi/v1.json</c>.</summary>
    public const string OpenApiPattern = "/api/openapi/{documentName}.json";

    public const string OpenApiPath = "/api/openapi/" + OpenApiDocumentName + ".json";

    /// <summary>Path prefixes that never fall back to the SPA shell.</summary>
    public static readonly IReadOnlyList<string> NonSpaPrefixes = ["/api", "/_test", "/hubs"];
}
