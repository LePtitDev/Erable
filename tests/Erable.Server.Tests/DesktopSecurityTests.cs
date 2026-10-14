using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Erable.Server.Tests.Infrastructure;

namespace Erable.Server.Tests;

/// <summary>E2E-16-02 (integration level), F16.R1, docs/testing.md §7 "Sécurité du mode local".</summary>
public sealed class DesktopSecurityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<TestBackend> StartDesktopAsync(CapturingLoggerProvider? logger = null) =>
        TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop }, logger);

    private static HttpClient Anonymous(TestBackend backend) => new() { BaseAddress = backend.BaseAddress };

    [Fact]
    public async Task Api_WithoutToken_Returns401()
    {
        await using var backend = await StartDesktopAsync();
        using var client = Anonymous(backend);

        using var response = await client.GetAsync(new Uri("/api/info", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Theory]
    [InlineData("/api/info")]
    [InlineData("/api/openapi/v1.json")]
    [InlineData("/hubs/project")]
    [InlineData("/_test/ping")]
    public async Task ProtectedPrefixes_WithWrongToken_Return401(string path)
    {
        await using var backend = await StartDesktopAsync();
        using var client = Anonymous(backend);
        client.DefaultRequestHeaders.Add(DesktopSession.TokenHeader, TestBackend.NewToken());

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Api_WithTokenHeader_Succeeds()
    {
        await using var backend = await StartDesktopAsync();

        using var response = await backend.Client.GetAsync(new Uri("/api/info", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("evil.example:80")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public async Task Request_WithForeignHost_IsRefused(string host)
    {
        await using var backend = await StartDesktopAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/info");
        request.Headers.Host = host;

        using var response = await backend.Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SpaShell_WithForeignHost_IsRefused()
    {
        // DNS rebinding: a page on evil.example resolving to 127.0.0.1 must not even get the shell.
        await using var backend = await StartDesktopAsync();
        using var client = Anonymous(backend);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = $"evil.example:{backend.BaseAddress.Port}";

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SpaShell_WithoutToken_IsServed()
    {
        await using var backend = await StartDesktopAsync();
        using var client = Anonymous(backend);

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnsafeRequest_FromForeignOrigin_Returns403()
    {
        await using var backend = await StartDesktopAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session");
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await backend.Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateSession_WithToken_SetsStrictHttpOnlyCookieThatAuthenticates()
    {
        await using var backend = await StartDesktopAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session");
        request.Headers.Add("Origin", backend.BaseAddress.GetLeftPart(UriPartial.Authority));

        using var response = await backend.Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        cookie.ShouldStartWith($"{DesktopSession.TokenCookie}=");
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);

        // The cookie alone (as sent by the WebView) authenticates later requests.
        using var anonymous = Anonymous(backend);
        using var next = new HttpRequestMessage(HttpMethod.Get, "/api/info");
        next.Headers.Add("Cookie", cookie.Split(';')[0]);
        using var authenticated = await anonymous.SendAsync(next, Ct);
        authenticated.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SessionToken_IsNeverLogged()
    {
        var logs = new CapturingLoggerProvider();
        await using var backend = await StartDesktopAsync(logs);
        using var anonymous = Anonymous(backend);

        using var withHeader = await backend.Client.GetAsync(new Uri("/api/info", UriKind.Relative), Ct);
        using var session = await backend.Client.PostAsync(new Uri("/api/session", UriKind.Relative), null, Ct);
        using var refused = await anonymous.GetAsync(new Uri("/api/info", UriKind.Relative), Ct);

        logs.Messages.ShouldNotBeEmpty();
        logs.Messages.ShouldAllBe(message => !message.Contains(backend.Options.SessionToken!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DesktopHost_ListensOnLoopbackOnly()
    {
        await using var backend = await StartDesktopAsync();

        backend.BaseAddress.Host.ShouldBe("127.0.0.1");
        backend.BaseAddress.Port.ShouldBeGreaterThan(0);
        await ShouldNotAcceptOnOtherInterfacesAsync(backend.BaseAddress.Port);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("192.168.1.10")]
    public void Build_DesktopModeOnNonLoopbackAddress_Throws(string address)
    {
        var options = new ErableHostOptions { Mode = HostMode.Desktop, Address = address, SessionToken = TestBackend.NewToken() };

        Should.Throw<InvalidOperationException>(() => ErableHost.Build(options));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    public void Build_DesktopModeWithoutStrongToken_Throws(string? token)
    {
        var options = new ErableHostOptions { Mode = HostMode.Desktop, SessionToken = token };

        Should.Throw<InvalidOperationException>(() => ErableHost.Build(options));
    }

    [Fact]
    public async Task CreateSession_InServerMode_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session");
        request.Headers.Add(DesktopSession.TokenHeader, TestBackend.NewToken());

        using var response = await backend.Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>Connecting through any non-loopback local address of this machine must fail.</summary>
    private static async Task ShouldNotAcceptOnOtherInterfacesAsync(int port)
    {
        var addresses = (await Dns.GetHostAddressesAsync(Dns.GetHostName(), Ct))
            .Where(a => !IPAddress.IsLoopback(a) && a.AddressFamily == AddressFamily.InterNetwork);
        foreach (var address in addresses)
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await Should.ThrowAsync<SocketException>(() => socket.ConnectAsync(address, port, Ct).AsTask());
        }
    }
}
