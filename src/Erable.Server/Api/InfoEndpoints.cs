using System.Reflection;
using Erable.Core;
using Erable.Server.Hosting;

namespace Erable.Server.Api;

/// <summary>Version handshake used before any other call (docs/architecture.md §2).</summary>
/// <param name="AppVersion">Application version (SemVer).</param>
/// <param name="ApiVersion">REST API version, e.g. <c>v1</c>.</param>
/// <param name="FormatVersion">Latest project <c>formatVersion</c> supported.</param>
/// <param name="Mode">How this backend is hosted.</param>
public sealed record ApiInfo(string AppVersion, string ApiVersion, int FormatVersion, HostMode Mode);

public static class InfoEndpoints
{
    public const string ApiVersion = "v1";

    private static readonly string AppVersion =
        typeof(InfoEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/info", (ErableHostOptions options) =>
                TypedResults.Ok(new ApiInfo(AppVersion, ApiVersion, ProjectFormat.CurrentVersion, options.Mode)))
            .WithName("GetInfo");
        return endpoints;
    }
}
