using System.Net;
using System.Net.Http.Json;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests;

public sealed class SelfTestTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly SelfTestReport Report = new([new SelfTestStep("home-visible", true, 12.5, null)], 420);

    [Fact]
    public async Task Report_InSelfTestMode_CompletesChannel()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop, SelfTest = true });

        using var response = await backend.Client.PostAsJsonAsync(new Uri("/api/self-test/report", UriKind.Relative), Report, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var received = await backend.Services.GetRequiredService<SelfTestChannel>().Report.WaitAsync(Ct);
        received.Steps.Single().Name.ShouldBe("home-visible");
        received.ReadyAtMs.ShouldBe(420);
    }

    [Fact]
    public async Task Report_Twice_Returns409()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop, SelfTest = true });
        using var first = await backend.Client.PostAsJsonAsync(new Uri("/api/self-test/report", UriKind.Relative), Report, Ct);

        using var second = await backend.Client.PostAsJsonAsync(new Uri("/api/self-test/report", UriKind.Relative), Report, Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Report_WithoutSelfTestMode_Returns404()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop });

        using var response = await backend.Client.PostAsJsonAsync(new Uri("/api/self-test/report", UriKind.Relative), Report, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Report_WithoutToken_Returns401()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop, SelfTest = true });
        using var anonymous = new HttpClient { BaseAddress = backend.BaseAddress };

        using var response = await anonymous.PostAsJsonAsync(new Uri("/api/self-test/report", UriKind.Relative), Report, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
