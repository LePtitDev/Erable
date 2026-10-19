using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erable.Core;
using Erable.Server.Hosting;
using Erable.Server.Tests.Infrastructure;
using Erable.Testing;

namespace Erable.Server.Tests;

public sealed class HostingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetInfo_Default_ReturnsContract()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop });

        using var response = await backend.Client.GetAsync(new Uri("/api/info", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        json.GetProperty("apiVersion").GetString().ShouldBe("v1");
        json.GetProperty("formatVersion").GetInt32().ShouldBe(ProjectFormat.CurrentVersion);
        json.GetProperty("mode").GetString().ShouldBe("desktop");
        json.GetProperty("appVersion").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api/v1/nothing/here")]
    public async Task Get_UnknownApiRoute_Returns404ProblemDetails(string path)
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        json.GetProperty("status").GetInt32().ShouldBe(404);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/p/3F2A9C14E07B5D21/e/0123456789ABCDEF")]
    public async Task Get_ClientRoute_ReturnsIndexHtml(string path)
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("<div id=\"root\">");
        response.Headers.CacheControl?.NoCache.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/some/client/route")]
    public async Task Get_SpaShell_SendsContentSecurityPolicy(string path)
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        var policy = string.Join(';', response.Headers.GetValues("Content-Security-Policy"));
        policy.ShouldContain("default-src 'self'");
        policy.ShouldContain("script-src 'self'");
        policy.ShouldContain("frame-ancestors 'self'");
        policy.ShouldContain("base-uri 'none'");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/api/info")]
    [InlineData("/api/does-not-exist")]
    public async Task Get_AnyResponse_SendsSecurityHeaders(string path)
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["SAMEORIGIN"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
    }

    [Fact]
    public async Task Get_WebManifest_IsServedAsManifestJson()
    {
        // F18.R8: browsers ignore a manifest served with another type.
        using var web = TempDirectory.Create();
        await File.WriteAllTextAsync(web.Combine("index.html"), TestBackend.IndexHtml, Ct);
        await File.WriteAllTextAsync(web.Combine("manifest.webmanifest"), """{"name":"Erable"}""", Ct);
        await using var backend = await TestBackend.StartAsync(o => o with { WebRootPath = web.Path });

        using var response = await backend.Client.GetAsync(new Uri("/manifest.webmanifest", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/manifest+json");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/some/client/route")]
    public async Task Get_SpaShell_CarriesThemeBeforeFirstPaint(string path)
    {
        await using var backend = await TestBackend.StartAsync();

        var html = await backend.Client.GetStringAsync(new Uri(path, UriKind.Relative), Ct);

        html.ShouldContain("<html data-theme=\"system\">");
    }

    [Fact]
    public async Task TestEndpoints_TestModeEnabled_ExistOnlyWhenCompiledIn()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { TestMode = true });

        using var response = await backend.Client.GetAsync(new Uri("/_test/ping", UriKind.Relative), Ct);

        // Debug builds compile the endpoints; Release builds must not expose them even in test mode.
        response.StatusCode.ShouldBe(ErableHost.TestEndpointsCompiled ? HttpStatusCode.NoContent : HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestEndpoints_TestModeDisabled_AreNotFound()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { TestMode = false });

        using var response = await backend.Client.GetAsync(new Uri("/_test/ping", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TestEndpointsCompiled_ReleaseBuild_IsFalse()
    {
#if DEBUG
        Assert.Skip("Only meaningful in Release builds (dotnet test -c Release).");
#else
        ErableHost.TestEndpointsCompiled.ShouldBeFalse();
#endif
    }

    [Fact]
    public async Task Start_DevelopmentEnvironment_Succeeds()
    {
        // Regression: the static web assets manifest pointed to a missing src/Erable.Server/wwwroot in Development.
        await using var backend = await TestBackend.StartAsync(o => o with { EnvironmentName = "Development" });

        using var response = await backend.Client.GetAsync(new Uri("/api/info", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Start_TwoHostsInSameProcess_AreIndependent()
    {
        await using var desktop = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop });
        await using var server = await TestBackend.StartAsync(o => o with { Mode = HostMode.Server });

        var a = await desktop.Client.GetFromJsonAsync<JsonElement>(new Uri("/api/info", UriKind.Relative), Ct);
        var b = await server.Client.GetFromJsonAsync<JsonElement>(new Uri("/api/info", UriKind.Relative), Ct);

        a.GetProperty("mode").GetString().ShouldBe("desktop");
        b.GetProperty("mode").GetString().ShouldBe("server");
    }
}
