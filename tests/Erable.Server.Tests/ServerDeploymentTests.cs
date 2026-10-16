using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Cli;
using Erable.Server.Hosting;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Erable.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Erable.Server.Tests;

/// <summary>Running a server for others (M4-06): quotas, cross-origin calls, a reverse proxy, a shared project, the health check.</summary>
public sealed class ServerDeploymentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri Url(string path) => new(path, UriKind.Relative);

    // ─── Quota ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_BeyondTheProjectQuota_Returns413WithTheQuotaCode()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { ProjectQuota = 1000 });
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");

        using var first = await backend.UploadAsync(project.Id, page.Id, "un.txt", new byte[700]);
        using var second = await backend.UploadAsync(project.Id, page.Id, "deux.txt", new byte[700]);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await second.ProblemCodeAsync()).ShouldBe(ErrorCodes.AttachmentQuotaExceeded);
    }

    [Fact]
    public async Task Upload_ThatFitsWhatIsLeft_IsAccepted()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { ProjectQuota = 1000 });
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        (await backend.UploadAsync(project.Id, page.Id, "un.txt", new byte[700])).Dispose();

        using var response = await backend.UploadAsync(project.Id, page.Id, "petit.txt", new byte[300]);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Upload_WhenTheQuotaIsUsedUp_IsRefusedBeforeReadingTheFile()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { ProjectQuota = 500 });
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        (await backend.UploadAsync(project.Id, page.Id, "plein.txt", new byte[500])).Dispose();

        using var response = await backend.UploadAsync(project.Id, page.Id, "un-octet.txt", new byte[1]);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.AttachmentQuotaExceeded);
    }

    [Fact]
    public async Task Upload_BiggerThanTheMaximumButWithinTheQuota_StaysTheTooLargeError()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { ProjectQuota = 10_000_000, MaxAttachmentSize = 100 });
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");

        using var response = await backend.UploadAsync(project.Id, page.Id, "gros.bin", new byte[500]);

        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.AttachmentTooLarge);
    }

    // ─── CORS ───────────────────────────────────────────────────────────────

    private static async Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin, string path = "/api/v1/projects")
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, Url(path));
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Cors_ConfiguredOrigin_IsAnsweredBeforeAnyCredentialsAreAsked()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { CorsOrigins = ["https://wiki.example.org"] });
        using var anonymous = backend.NewAnonymousClient();

        using var response = await PreflightAsync(anonymous, "https://wiki.example.org");

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("https://wiki.example.org");
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().ShouldContain("authorization", Case.Insensitive);
    }

    [Fact]
    public async Task Cors_PublicUrl_IsAllowed()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { PublicUrl = "https://wiki.example.org/" });
        using var anonymous = backend.NewAnonymousClient();

        using var response = await PreflightAsync(anonymous, "https://wiki.example.org");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeTrue();
    }

    [Fact]
    public async Task Cors_ADesktopPageOnLoopback_IsAllowedAndAnotherSiteIsNot()
    {
        await using var backend = await TestBackend.StartAsync();
        using var anonymous = backend.NewAnonymousClient();

        using var desktop = await PreflightAsync(anonymous, "http://127.0.0.1:53211");
        using var stranger = await PreflightAsync(anonymous, "https://evil.example");

        desktop.Headers.Contains("Access-Control-Allow-Origin").ShouldBeTrue();
        stranger.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Cors_LoopbackNotAllowed_IsRefused()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { AllowLoopbackOrigins = false });
        using var anonymous = backend.NewAnonymousClient();

        using var response = await PreflightAsync(anonymous, "http://127.0.0.1:53211");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Cors_AllowedOriginWithABearerToken_ReadsTheApi()
    {
        await using var backend = await TestBackend.StartAsync();
        using var client = backend.ClientFor("camille");
        using var request = new HttpRequestMessage(HttpMethod.Get, Url("/api/v1/me"));
        request.Headers.Add("Origin", "http://127.0.0.1:53211");

        using var response = await client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("http://127.0.0.1:53211");
    }

    // ─── Reverse proxy ──────────────────────────────────────────────────────

    [Fact]
    public async Task Login_BehindATrustedProxy_SecuresTheCookieAndCountsFailuresPerClientAddress()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { TrustProxy = true, LoginMaxFailuresPerAddress = 3, LoginMaxFailures = 100 });
        backend.Accounts.CreateAccount("camille", "Camille", "password-camille").IsSuccess.ShouldBeTrue();
        using var anonymous = backend.NewAnonymousClient();

        async Task<HttpResponseMessage> LoginAsync(string password, string client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/v1/auth/login"))
            {
                Content = JsonContent.Create(new LoginRequest("camille", password), options: ApiJson.Options),
            };
            request.Headers.Add("X-Forwarded-For", client);
            request.Headers.Add("X-Forwarded-Proto", "https");
            return await anonymous.SendAsync(request, Ct);
        }

        for (var i = 0; i < 3; i++)
        {
            (await LoginAsync("wrong", "203.0.113.7")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // That client is blocked; another one behind the same proxy is not.
        (await LoginAsync("password-camille", "203.0.113.7")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        using var other = await LoginAsync("password-camille", "198.51.100.9");
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
        other.Headers.GetValues("Set-Cookie").Single().ShouldContain("secure", Case.Insensitive);
    }

    [Fact]
    public async Task Login_WithoutAProxy_IgnoresForwardedHeaders()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { TrustProxy = false });
        backend.Accounts.CreateAccount("camille", "Camille", "password-camille").IsSuccess.ShouldBeTrue();
        using var anonymous = backend.NewAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/v1/auth/login"))
        {
            Content = JsonContent.Create(new LoginRequest("camille", "password-camille"), options: ApiJson.Options),
        };
        request.Headers.Add("X-Forwarded-Proto", "https");

        using var response = await anonymous.SendAsync(request, Ct);

        response.Headers.GetValues("Set-Cookie").Single().ShouldNotContain("secure", Case.Insensitive);
    }

    // ─── A project shared as it is ──────────────────────────────────────────

    [Fact]
    public async Task SharedProject_IsRegisteredAndAdministeredByTheFirstServerAdministrator()
    {
        using var folder = TempDirectory.Create();
        var project = Path.Combine(folder.Path, "MonWiki");
        ProjectStore.Create(project, "Mon wiki", "fr", TimeProvider.System).Value.Dispose();
        var projectId = ManifestFile.Read(project).Value.ProjectId;

        await using var backend = await TestBackend.StartAsync(
            o => o with { SharedProjectFolder = project, BootstrapAdminLogin = "root", BootstrapAdminPassword = "root-password-1" },
            skipDefaultAdmin: true);

        backend.Accounts.Store.GetRole(projectId, "root").ShouldBe(ProjectRole.Admin);
        using var root = backend.ClientFor("root", serverAdmin: true);
        var listed = await (await root.GetAsync(Url("/api/v1/projects"), Ct)).ReadAsync<ProjectSummary[]>();
        listed.Select(p => p.Name).ShouldBe(["Mon wiki"]);
    }

    [Fact]
    public async Task SharedProject_StartedAgain_KeepsItsMembers()
    {
        using var folder = TempDirectory.Create();
        var project = Path.Combine(folder.Path, "MonWiki");
        ProjectStore.Create(project, "Mon wiki", "fr", TimeProvider.System).Value.Dispose();
        var projectId = ManifestFile.Read(project).Value.ProjectId;
        using var data = TempDirectory.Create();

        async Task StartOnceAsync(Action<TestBackend>? work = null)
        {
            await using var backend = await TestBackend.StartAsync(
                o => o with
                {
                    DataDirectory = data.Path,
                    SharedProjectFolder = project,
                    BootstrapAdminLogin = "root",
                    BootstrapAdminPassword = "root-password-1",
                },
                skipDefaultAdmin: true);
            work?.Invoke(backend);
        }

        await StartOnceAsync(b =>
        {
            b.Accounts.CreateAccount("lea", "Léa", "password-lea").IsSuccess.ShouldBeTrue();
            b.Accounts.Store.SetMember(projectId, "lea", ProjectRole.Reader);
        });
        ProjectLockCheck(project);

        await StartOnceAsync(b =>
        {
            b.Accounts.Store.GetRole(projectId, "lea").ShouldBe(ProjectRole.Reader);
            b.Accounts.Store.GetRole(projectId, "root").ShouldBe(ProjectRole.Admin);
            b.Accounts.Store.ListProjects().Count.ShouldBe(1);
        });
    }

    private static void ProjectLockCheck(string project)
    {
        // The first run released the project (lock and WAL): it can be opened again.
        using var reopened = ProjectStore.Open(project, TimeProvider.System).Value;
        reopened.Manifest.Name.ShouldBe("Mon wiki");
    }

    // ─── Health check ───────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_OnARunningServer_Succeeds_AndFailsWhenNothingListens()
    {
        using var data = TempDirectory.Create();
        using var web = TempDirectory.Create();
        await File.WriteAllTextAsync(web.Combine("index.html"), TestBackend.IndexHtml, Ct);
        var app = ErableHost.Build(
            new ErableHostOptions { Mode = HostMode.Server, Port = 0, Address = "127.0.0.1", DataDirectory = data.Path, WebRootPath = web.Path },
            builder => builder.Logging.ClearProviders());
        await app.StartAsync(Ct);
        int port;
        try
        {
            port = ErableHost.GetListeningUri(app).Port;
            (await HealthCheck.RunAsync(port, Ct)).ShouldBe(0);
        }
        finally
        {
            await app.DisposeAsync();
        }

        (await HealthCheck.RunAsync(port, Ct)).ShouldBe(1);
    }
}
