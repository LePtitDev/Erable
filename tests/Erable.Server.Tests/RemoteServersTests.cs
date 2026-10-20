using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Servers;
using Erable.Server.Tests.Infrastructure;
using Erable.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests;

/// <summary>The desktop application connected to a server (E2E-01-06, E2E-16-06, F16.R4).</summary>
public sealed class RemoteServersTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static async Task<TestBackend> StartDesktopAsync(MemorySecretStore? vault = null) =>
        await TestBackend.StartAsync(
            o => o with { Mode = HostMode.Desktop },
            services: s =>
            {
                if (vault is not null)
                {
                    s.AddSingleton<ISecretStore>(vault);
                }
            });

    private static async Task<ServerItem> ConnectAsync(TestBackend desktop, string origin, string login, string password)
    {
        using var response = await desktop.Client.PostJsonAsync("/api/v1/servers/connect", new ConnectServerRequest(origin, login, password));
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        return await response.ReadAsync<ServerItem>();
    }

    // ─── Addresses ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://wiki.example.org", "https://wiki.example.org")]
    [InlineData("  https://wiki.example.org/  ", "https://wiki.example.org")]
    [InlineData("wiki.example.org", "https://wiki.example.org")]
    [InlineData("http://192.168.1.20:5080/some/path?x=1#y", "http://192.168.1.20:5080")]
    public void Normalize_KeepsTheOriginOnly(string typed, string expected)
    {
        RemoteServers.Normalize(typed).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://wiki.example.org")]
    [InlineData("https://user:secret@wiki.example.org")]
    [InlineData("javascript:alert(1)")]
    public void Normalize_NotAnAddressOfAServer_IsRefused(string typed)
    {
        RemoteServers.Normalize(typed).Error.Code.ShouldBe(RemoteServers.InvalidUrl);
    }

    // ─── Versions (F16.R4) ──────────────────────────────────────────────────

    [Fact]
    public async Task Probe_CompatibleServer_GivesBothVersions()
    {
        await using var server = await RealServer.StartAsync();
        await using var desktop = await StartDesktopAsync();

        using var response = await desktop.Client.PostJsonAsync("/api/v1/servers/probe", new ProbeServerRequest(server.Origin));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var probe = await response.ReadAsync<ServerProbe>();
        probe.Compatible.ShouldBeTrue();
        probe.ServerApiVersion.ShouldBe(InfoEndpoints.ApiVersion);
        probe.AppApiVersion.ShouldBe(InfoEndpoints.ApiVersion);
        probe.Host.ShouldBe(server.Address.Authority);
    }

    [Fact]
    public async Task Probe_ServerOfAnotherVersion_IsFlaggedWithTheVersionsInPresence()
    {
        // E2E-16-06
        await using var fake = await FakeServer.OfAnotherVersionAsync();
        await using var desktop = await StartDesktopAsync();

        using var response = await desktop.Client.PostJsonAsync("/api/v1/servers/probe", new ProbeServerRequest(fake.Origin));

        var probe = await response.ReadAsync<ServerProbe>();
        probe.Compatible.ShouldBeFalse();
        probe.ServerApiVersion.ShouldBe("v9");
        probe.ServerAppVersion.ShouldBe("9.0.0");
        probe.ServerFormatVersion.ShouldBe(42);
        probe.AppApiVersion.ShouldBe(InfoEndpoints.ApiVersion);
    }

    [Fact]
    public async Task Connect_ServerOfAnotherVersion_FailsAfterTheHandshakeAlone()
    {
        // E2E-16-06: « sans aucune autre requête vers le serveur »
        await using var fake = await FakeServer.OfAnotherVersionAsync();
        await using var desktop = await StartDesktopAsync();

        using var response = await desktop.Client.PostJsonAsync("/api/v1/servers/connect", new ConnectServerRequest(fake.Origin, "camille", "password-camille"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!;
        problem["code"]!.GetValue<string>().ShouldBe(RemoteServers.Incompatible);
        problem["serverApiVersion"]!.GetValue<string>().ShouldBe("v9");
        problem["appApiVersion"]!.GetValue<string>().ShouldBe(InfoEndpoints.ApiVersion);
        fake.Requests.Select(r => $"{r.Method} {r.Path}").ShouldBe(["GET /api/info"]);
    }

    [Fact]
    public async Task Probe_SomethingThatIsNotAnErableServer_IsReported()
    {
        await using var fake = await FakeServer.StartAsync((_, _) => (200, "<html>hello</html>"));
        await using var desktop = await StartDesktopAsync();

        using var response = await desktop.Client.PostJsonAsync("/api/v1/servers/probe", new ProbeServerRequest(fake.Origin));

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await response.ProblemCodeAsync()).ShouldBe(RemoteServers.NotErable);
    }

    [Fact]
    public async Task Probe_NothingListens_IsReportedAsUnreachable()
    {
        await using var desktop = await StartDesktopAsync();

        using var response = await desktop.Client.PostJsonAsync("/api/v1/servers/probe", new ProbeServerRequest("http://127.0.0.1:1"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await response.ProblemCodeAsync()).ShouldBe(RemoteServers.Unreachable);
    }

    // ─── Signing in ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Connect_ValidCredentials_ListsTheServerAndKeepsTheTokenInTheVaultOnly()
    {
        // E2E-01-06
        await using var server = await RealServer.StartAsync();
        var vault = new MemorySecretStore();
        await using var desktop = await StartDesktopAsync(vault);

        var item = await ConnectAsync(desktop, server.Origin, "camille", "password-camille");

        item.Connected.ShouldBeTrue();
        item.UserId.ShouldBe("camille");
        item.Url.ShouldBe(server.Origin);
        var token = vault.Read(ServerRegistry.TokenKey(item.Id)).ShouldNotBeNull();
        using var listed = await desktop.Client.GetAsync(Url("/api/v1/servers"), Ct);
        var body = await listed.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain(token, customMessage: "the token never reaches the page");
        var list = System.Text.Json.JsonSerializer.Deserialize<ServerList>(body, ApiJson.Options)!;
        list.Servers.Select(s => s.Id).ShouldBe([item.Id]);
    }

    [Fact]
    public async Task Connect_WrongPassword_Returns401AndStoresNothing()
    {
        await using var server = await RealServer.StartAsync();
        var vault = new MemorySecretStore();
        await using var desktop = await StartDesktopAsync(vault);

        using var response = await desktop.Client.PostJsonAsync("/api/v1/servers/connect", new ConnectServerRequest(server.Origin, "camille", "mauvais"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).ShouldBe("auth.invalidCredentials");
        vault.Read(ServerRegistry.TokenKey(ServerRegistry.IdOf(server.Origin))).ShouldBeNull();
        var list = await (await desktop.Client.GetAsync(Url("/api/v1/servers"), Ct)).ReadAsync<ServerList>();
        list.Servers.ShouldBeEmpty();
    }

    [Fact]
    public async Task Connect_Twice_UpdatesTheSameEntry()
    {
        await using var server = await RealServer.StartAsync();
        await using var desktop = await StartDesktopAsync();

        var first = await ConnectAsync(desktop, server.Origin, "camille", "password-camille");
        var second = await ConnectAsync(desktop, server.Origin + "/", "malo", "password-malo");

        second.Id.ShouldBe(first.Id);
        second.UserId.ShouldBe("malo");
        (await (await desktop.Client.GetAsync(Url("/api/v1/servers"), Ct)).ReadAsync<ServerList>()).Servers.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ServerList_WithoutAVault_SaysTokensDoNotSurviveARestart()
    {
        await using var desktop = await StartDesktopAsync(new MemorySecretStore());

        var list = await (await desktop.Client.GetAsync(Url("/api/v1/servers"), Ct)).ReadAsync<ServerList>();

        list.TokensSurviveRestart.ShouldBeFalse();
    }

    // ─── Projects of the server ─────────────────────────────────────────────

    [Fact]
    public async Task Projects_OfTheServer_AreTheOnesTheUserIsAMemberOf()
    {
        await using var server = await RealServer.StartAsync();
        await using var desktop = await StartDesktopAsync();
        var item = await ConnectAsync(desktop, server.Origin, "lea", "password-lea");

        var projects = await (await desktop.Client.GetAsync(Url($"/api/v1/servers/{item.Id}/projects"), Ct)).ReadAsync<RemoteProject[]>();

        var project = projects.ShouldHaveSingleItem();
        project.Id.ShouldBe(server.Project.Id);
        project.Name.ShouldBe("Chroniques d’Aube");
        project.MyRole.ShouldBe(ProjectRole.Reader);
        project.ServerId.ShouldBe(item.Id);
    }

    [Fact]
    public async Task Requests_ForAProjectOfTheServer_AreForwardedWithTheTokenOfTheUser()
    {
        await using var server = await RealServer.StartAsync();
        await using var desktop = await StartDesktopAsync();
        var item = await ConnectAsync(desktop, server.Origin, "camille", "password-camille");
        (await desktop.Client.GetAsync(Url($"/api/v1/servers/{item.Id}/projects"), Ct)).EnsureSuccessStatusCode();
        var root = $"/api/v1/projects/{server.Project.Id}";

        // Reading: the page tree of the server, through the local origin.
        using var tree = await desktop.Client.GetAsync(Url($"{root}/tree"), Ct);
        tree.StatusCode.ShouldBe(HttpStatusCode.OK);
        tree.Headers.GetValues(RemoteProxyHeaders.Server).Single().ShouldBe(item.Id);
        (await tree.ReadAsync<ProjectTree>()).Nodes.Count.ShouldBeGreaterThan(3);

        // Writing: camille is an editor, and the author is her, not the local user.
        using var created = await desktop.Client.PostJsonAsync($"{root}/pages", new CreatePageRequest(Roots.Pages.Id, "Ilsa Varenne"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await created.ReadAsync<ElementDetails>()).CreatedBy.ShouldBe("camille");
    }

    [Fact]
    public async Task Requests_ForAReaderOfTheServer_AreRefusedByTheServerNotByTheDesktop()
    {
        await using var server = await RealServer.StartAsync();
        await using var desktop = await StartDesktopAsync();
        var item = await ConnectAsync(desktop, server.Origin, "lea", "password-lea");
        (await desktop.Client.GetAsync(Url($"/api/v1/servers/{item.Id}/projects"), Ct)).EnsureSuccessStatusCode();

        using var response = await desktop.Client.PostJsonAsync($"/api/v1/projects/{server.Project.Id}/pages", new CreatePageRequest(Roots.Pages.Id, "Interdit"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ProblemCodeAsync()).ShouldBe("access.forbidden");
    }

    [Fact]
    public async Task Forwarding_NeverSendsTheLocalSessionToTheServer()
    {
        await using var fake = await FakeServer.StartAsync((method, path) => path switch
        {
            "/api/info" => (200, """{"appVersion":"0.1.0","apiVersion":"v1","formatVersion":4,"mode":"server","folderProjects":false}"""),
            "/api/v1/auth/login" => (200, """{"id":"camille","name":"Camille R.","isServerAdmin":false,"expiresAt":"2030-01-01T00:00:00Z","token":"abcDEF123_-"}"""),
            "/api/v1/projects" => (200, """[{"id":"3F2A9C14E07B5D21","name":"Chroniques","folder":"","contentLanguage":"fr","formatVersion":4,"createdAt":"2026-01-01T00:00:00Z","myRole":"editor"}]"""),
            _ => (200, method == "GET" ? "[]" : "{}"),
        });
        await using var desktop = await StartDesktopAsync();
        var item = await ConnectAsync(desktop, fake.Origin, "camille", "password-camille");
        (await desktop.Client.GetAsync(Url($"/api/v1/servers/{item.Id}/projects"), Ct)).EnsureSuccessStatusCode();

        using var request = new HttpRequestMessage(HttpMethod.Get, Url("/api/v1/projects/3F2A9C14E07B5D21/tree"));
        request.Headers.Add("Cookie", "erable_session=local-secret; other=1");
        request.Headers.Add("Origin", desktop.BaseAddress.GetLeftPart(UriPartial.Authority));
        using var response = await desktop.Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forwarded = fake.Requests[^1];
        forwarded.Path.ShouldBe("/api/v1/projects/3F2A9C14E07B5D21/tree");
        forwarded.Headers["Authorization"].ShouldBe("Bearer abcDEF123_-");
        forwarded.Headers.ShouldNotContainKey("Cookie");
        forwarded.Headers.ShouldNotContainKey("Origin");
        forwarded.Headers.ShouldNotContainKey("X-Erable-Session");
    }

    [Fact]
    public async Task Requests_AfterTheTokenWasEnded_Return401AndTheServerShowsAsDisconnected()
    {
        await using var server = await RealServer.StartAsync();
        await using var desktop = await StartDesktopAsync();
        var item = await ConnectAsync(desktop, server.Origin, "camille", "password-camille");
        (await desktop.Client.GetAsync(Url($"/api/v1/servers/{item.Id}/projects"), Ct)).EnsureSuccessStatusCode();
        server.EndSessionsOf("camille");

        using var response = await desktop.Client.GetAsync(Url($"/api/v1/projects/{server.Project.Id}/tree"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.GetValues(RemoteProxyHeaders.Server).Single().ShouldBe(item.Id);
        var list = await (await desktop.Client.GetAsync(Url("/api/v1/servers"), Ct)).ReadAsync<ServerList>();
        list.Servers.Single().Connected.ShouldBeFalse();
    }

    [Fact]
    public async Task Requests_WhenTheServerIsGone_Return502()
    {
        var server = await RealServer.StartAsync();
        await using var desktop = await StartDesktopAsync();
        var item = await ConnectAsync(desktop, server.Origin, "camille", "password-camille");
        (await desktop.Client.GetAsync(Url($"/api/v1/servers/{item.Id}/projects"), Ct)).EnsureSuccessStatusCode();
        var projectId = server.Project.Id;
        await server.DisposeAsync();

        using var response = await desktop.Client.GetAsync(Url($"/api/v1/projects/{projectId}/tree"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await response.ProblemCodeAsync()).ShouldBe(RemoteServers.Unreachable);
    }

    [Fact]
    public async Task Forget_RemovesTheServerItsTokenAndItsSessionOnTheServer()
    {
        await using var server = await RealServer.StartAsync();
        var vault = new MemorySecretStore();
        await using var desktop = await StartDesktopAsync(vault);
        var item = await ConnectAsync(desktop, server.Origin, "camille", "password-camille");
        var token = vault.Read(ServerRegistry.TokenKey(item.Id))!;

        using var response = await desktop.Client.DeleteAsync(Url($"/api/v1/servers/{item.Id}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        vault.Read(ServerRegistry.TokenKey(item.Id)).ShouldBeNull();
        (await (await desktop.Client.GetAsync(Url("/api/v1/servers"), Ct)).ReadAsync<ServerList>()).Servers.ShouldBeEmpty();
        // The session ended on the server too: the token opens nothing.
        using var anonymous = new HttpClient { BaseAddress = new Uri(server.Origin) };
        anonymous.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        (await anonymous.GetAsync(Url("/api/v1/me"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownProjects_StayLocal()
    {
        await using var desktop = await StartDesktopAsync();

        using var response = await desktop.Client.GetAsync(Url($"/api/v1/projects/{ElementId.NewRandom()}/tree"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Headers.Contains(RemoteProxyHeaders.Server).ShouldBeFalse();
    }

    [Fact]
    public async Task Servers_AreNotAvailableOnAServer()
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.GetAsync(Url("/api/v1/servers"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
