#if ERABLE_TEST_ENDPOINTS
namespace Erable.Server.Testing;

/// <summary>Test-only endpoints (docs/features.md §1.4). Compiled out of Release builds.</summary>
internal static class TestEndpoints
{
    public const string Prefix = "/_test";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Prefix).ExcludeFromDescription();
        group.MapGet("/ping", () => TypedResults.NoContent());
    }
}
#endif
