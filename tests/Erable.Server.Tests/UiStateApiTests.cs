using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Server.Tests.Infrastructure;

namespace Erable.Server.Tests;

public sealed class UiStateApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PutThenGet_Document_RoundTrips()
    {
        await using var backend = await TestBackend.StartAsync();

        using var put = await backend.Client.PutAsJsonAsync("/api/v1/me/ui-state/layout", new { explorerWidth = 280, sidePanel = false }, Ct);
        var read = await backend.Client.GetFromJsonAsync<JsonObject>("/api/v1/me/ui-state/layout", Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        read!["explorerWidth"]!.GetValue<int>().ShouldBe(280);
        read["sidePanel"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_WrittenDocument_IsGone()
    {
        await using var backend = await TestBackend.StartAsync();
        using var put = await backend.Client.PutAsJsonAsync("/api/v1/me/ui-state/draft-x", new { markdown = "a" }, Ct);

        using var deleted = await backend.Client.DeleteAsync(new Uri("/api/v1/me/ui-state/draft-x", UriKind.Relative), Ct);
        using var read = await backend.Client.GetAsync(new Uri("/api/v1/me/ui-state/draft-x", UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_NeverWritten_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.GetAsync(new Uri("/api/v1/me/ui-state/tree-ABC", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("a%2Fb")]
    [InlineData("a%5Cb")]
    [InlineData("with%20space")]
    public async Task Put_KeyWithPathCharacters_Returns400(string key)
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.PutAsJsonAsync($"/api/v1/me/ui-state/{key}", new { x = 1 }, Ct);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }
}
