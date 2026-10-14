using System.Text.Json.Nodes;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;
using Erable.Testing;

namespace Erable.Server.Tests;

/// <summary>
/// API contract (docs/testing.md §7). The versioned document <c>src/web/src/api/openapi.json</c> is both the
/// snapshot reviewed in diffs and the source of the generated TypeScript client.
/// </summary>
public sealed class OpenApiTests
{
    private static readonly string SnapshotPath = RepositoryPaths.Combine("src", "web", "src", "api", "openapi.json");

    [Fact]
    public async Task OpenApiDocument_CurrentApi_MatchesVersionedDocument()
    {
        await using var backend = await TestBackend.StartAsync();
        var ct = TestContext.Current.CancellationToken;

        var live = await backend.Client.GetStringAsync(new Uri(ApiRoutes.OpenApiPath, UriKind.Relative), ct);
        var versioned = await File.ReadAllTextAsync(SnapshotPath, ct);

        if (!JsonNode.DeepEquals(JsonNode.Parse(live), JsonNode.Parse(versioned)))
        {
            var received = Path.ChangeExtension(SnapshotPath, ".received.json");
            await File.WriteAllTextAsync(received, live, ct);
            Assert.Fail(
                $"The API changed but {SnapshotPath} was not regenerated (live document written to {received}). " +
                "Run `pnpm -C src/web api:generate` and review the diff.");
        }
    }
}
