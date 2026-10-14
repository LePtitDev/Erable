using System.Text.Json;
using Erable.Core;
using Erable.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erable.Server.Tests;

/// <summary>The API's own JSON settings keep element IDs as strings (docs/architecture.md §4).</summary>
public sealed class ElementIdJsonTests
{
    [Fact]
    public async Task ApiSerializer_ElementId_IsCanonicalString()
    {
        await using var backend = await TestBackend.StartAsync();
        var options = backend.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        JsonSerializer.Serialize(new Holder(ElementId.Parse("3f2a9c14e07b5d21")), options)
            .ShouldBe("""{"id":"3F2A9C14E07B5D21"}""");
    }

    [Fact]
    public async Task ApiSerializer_ElementIdAsNumber_IsRejected()
    {
        await using var backend = await TestBackend.StartAsync();
        var options = backend.Services.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Holder>("""{"id":4551684617428835617}""", options));
    }

    private sealed record Holder(ElementId Id);
}
