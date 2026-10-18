using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Security;
using Erable.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Server.Tests;

/// <summary>Accounts, sessions and roles (M4-01; E2E-14-01 to E2E-14-04 at API level).</summary>
public sealed class AuthApiTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Signs in through the API and returns the session cookie value, the way a browser would keep it.</summary>
    private static async Task<(HttpResponseMessage Response, string? Cookie)> LoginAsync(HttpClient client, string login, string password, bool issueToken = false)
    {
        var response = await client.PostJsonAsync("/api/v1/auth/login", new LoginRequest(login, password, issueToken));
        var cookie = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(SessionCookies.CookieName + "=", StringComparison.Ordinal))
            : null;
        return (response, cookie?.Split(';')[0]);
    }

    private static HttpRequestMessage WithCookie(HttpMethod method, string path, string cookie)
    {
        var request = new HttpRequestMessage(method, Url(path));
        request.Headers.Add("Cookie", cookie);
        return request;
    }

    private static async Task<TestBackend> StartAsync(FakeTimeProvider? clock = null, Func<Erable.Server.Hosting.ErableHostOptions, Erable.Server.Hosting.ErableHostOptions>? configure = null)
    {
        var backend = await TestBackend.StartAsync(o =>
        {
            var configured = clock is null ? o : o with { TimeProvider = clock };
            return configure?.Invoke(configured) ?? configured;
        });
        backend.Accounts.CreateAccount("camille", "Camille R.", "camille-pass-1").IsSuccess.ShouldBeTrue();
        return backend;
    }

    [Fact]
    public async Task Login_ValidCredentials_SetsAnHttpOnlyStrictCookieThatOpensTheSession()
    {
        await using var backend = await StartAsync();
        using var anonymous = backend.NewAnonymousClient();

        var (response, cookie) = await LoginAsync(anonymous, "Camille", "camille-pass-1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        (await response.ReadAsync<SessionResult>()).Token.ShouldBeNull();
        using var me = await anonymous.SendAsync(WithCookie(HttpMethod.Get, "/api/v1/me", cookie!), Ct);
        (await me.ReadAsync<CurrentUserInfo>()).Id.ShouldBe("camille");
    }

    [Fact]
    public async Task Login_WrongPasswordOrUnknownLogin_GivesTheSameAnswer()
    {
        await using var backend = await StartAsync();
        using var anonymous = backend.NewAnonymousClient();

        var (wrongPassword, _) = await LoginAsync(anonymous, "camille", "not-the-password");
        var (unknownLogin, _) = await LoginAsync(anonymous, "personne", "camille-pass-1");

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await wrongPassword.ProblemCodeAsync()).ShouldBe("auth.invalidCredentials");
        (await unknownLogin.ProblemCodeAsync()).ShouldBe("auth.invalidCredentials");
        wrongPassword.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Login_DisabledAccount_IsRefused()
    {
        await using var backend = await StartAsync();
        backend.Accounts.Store.UpdateAccount("camille", "Camille R.", false, disabled: true);
        using var anonymous = backend.NewAnonymousClient();

        var (response, _) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_TooManyFailures_AnswersTooManyRequestsEvenWithTheRightPassword()
    {
        var clock = new FakeTimeProvider(Start);
        await using var backend = await StartAsync(clock, o => o with { LoginMaxFailures = 3 });
        using var anonymous = backend.NewAnonymousClient();
        for (var i = 0; i < 3; i++)
        {
            (await LoginAsync(anonymous, "camille", "wrong-" + i)).Response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var (blocked, _) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        blocked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        blocked.Headers.RetryAfter.ShouldNotBeNull();
        clock.Advance(TimeSpan.FromMinutes(16));
        (await LoginAsync(anonymous, "camille", "camille-pass-1")).Response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithAHashToUpgrade_UpgradesItAndKeepsTheOtherSessions()
    {
        await using var backend = await StartAsync();
        // A hash of an older strength (fewer iterations): verified as SuccessRehashNeeded.
        var weak = new PasswordHasher<string>(Options.Create(new PasswordHasherOptions { IterationCount = 1_000 }));
        var weakHash = weak.HashPassword("camille", "camille-pass-1");
        backend.Accounts.Store.SetPassword("camille", weakHash);
        // A session opened on another device before the upgrade.
        var other = backend.Accounts.StartSession("camille", Erable.Storage.ServerStore.SessionKindCookie);
        using var anonymous = backend.NewAnonymousClient();

        (await LoginAsync(anonymous, "camille", "camille-pass-1")).Response.StatusCode.ShouldBe(HttpStatusCode.OK);

        backend.Accounts.Store.GetAccount("camille")!.PasswordHash.ShouldNotBe(weakHash);
        using var me = await anonymous.SendAsync(WithCookie(HttpMethod.Get, "/api/v1/me", $"{SessionCookies.CookieName}={other.Token}"), Ct);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static HttpRequestMessage ChangePassword(string cookie, string current, string next)
    {
        var request = WithCookie(HttpMethod.Put, "/api/v1/auth/password", cookie);
        request.Content = JsonContent.Create(new ChangeMyPasswordRequest(current, next), options: ApiJson.Options);
        return request;
    }

    [Fact]
    public async Task ChangeMyPassword_WithTheCurrentPassword_KeepsThisDeviceSignedInAndEndsTheOthers()
    {
        await using var backend = await StartAsync();
        using var anonymous = backend.NewAnonymousClient();
        var (_, here) = await LoginAsync(anonymous, "camille", "camille-pass-1");
        var (_, elsewhere) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        using var change = await anonymous.SendAsync(ChangePassword(here!, "camille-pass-1", "camille-pass-2"), Ct);

        change.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renewed = change.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith(SessionCookies.CookieName + "=", StringComparison.Ordinal)).Split(';')[0];
        (await anonymous.SendAsync(WithCookie(HttpMethod.Get, "/api/v1/me", renewed), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await anonymous.SendAsync(WithCookie(HttpMethod.Get, "/api/v1/me", elsewhere!), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(anonymous, "camille", "camille-pass-1")).Response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await LoginAsync(anonymous, "camille", "camille-pass-2")).Response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangeMyPassword_WrongCurrentPassword_Returns403AndKeepsThePassword()
    {
        await using var backend = await StartAsync();
        using var anonymous = backend.NewAnonymousClient();
        var (_, cookie) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        using var change = await anonymous.SendAsync(ChangePassword(cookie!, "not-my-password", "camille-pass-2"), Ct);

        change.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await change.ProblemCodeAsync()).ShouldBe("auth.wrongCurrentPassword");
        (await LoginAsync(anonymous, "camille", "camille-pass-1")).Response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangeMyPassword_TooShortNewPassword_Returns422()
    {
        await using var backend = await StartAsync();
        using var anonymous = backend.NewAnonymousClient();
        var (_, cookie) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        using var change = await anonymous.SendAsync(ChangePassword(cookie!, "camille-pass-1", "short"), Ct);

        change.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await change.ProblemCodeAsync()).ShouldBe("account.password.invalid");
    }

    [Fact]
    public async Task Logout_InvalidatesTheOldCookie()
    {
        await using var backend = await StartAsync();
        using var anonymous = backend.NewAnonymousClient();
        var (_, cookie) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        using var logout = await anonymous.SendAsync(WithCookie(HttpMethod.Post, "/api/v1/auth/logout", cookie!), Ct);
        using var after = await anonymous.SendAsync(WithCookie(HttpMethod.Get, "/api/v1/me", cookie!), Ct);

        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        after.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Session_AfterItsLifetime_Returns401()
    {
        var clock = new FakeTimeProvider(Start);
        await using var backend = await StartAsync(clock, o => o with { SessionLifetime = TimeSpan.FromHours(2) });
        using var anonymous = backend.NewAnonymousClient();
        var (_, cookie) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        clock.Advance(TimeSpan.FromHours(3));
        using var response = await anonymous.SendAsync(WithCookie(HttpMethod.Get, "/api/v1/me", cookie!), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ProblemCodeAsync()).ShouldBe("auth.required");
    }

    [Fact]
    public async Task Session_UsedPastHalfItsLifetime_IsRenewedAndTheCookieWrittenAgain()
    {
        var clock = new FakeTimeProvider(Start);
        await using var backend = await StartAsync(clock, o => o with { SessionLifetime = TimeSpan.FromHours(2) });
        using var anonymous = backend.NewAnonymousClient();
        var (_, cookie) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        clock.Advance(TimeSpan.FromMinutes(90));
        using var renewed = await anonymous.SendAsync(WithCookie(HttpMethod.Get, "/api/v1/me", cookie!), Ct);
        clock.Advance(TimeSpan.FromMinutes(90));
        using var stillThere = await anonymous.SendAsync(WithCookie(HttpMethod.Get, "/api/v1/me", cookie!), Ct);

        renewed.Headers.Contains("Set-Cookie").ShouldBeTrue();
        stillThere.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithIssueToken_ReturnsABearerTokenAndNoCookie()
    {
        await using var backend = await StartAsync();
        using var anonymous = backend.NewAnonymousClient();

        var (response, cookie) = await LoginAsync(anonymous, "camille", "camille-pass-1", issueToken: true);

        cookie.ShouldBeNull();
        var session = await response.ReadAsync<SessionResult>();
        session.Token.ShouldNotBeNullOrEmpty();
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        using var me = await anonymous.GetAsync(Url("/api/v1/me"), Ct);
        (await me.ReadAsync<CurrentUserInfo>()).Id.ShouldBe("camille");
    }

    [Fact]
    public async Task Anonymous_ProtectedEndpoints_Return401ButTheHandshakeIsPublic()
    {
        await using var backend = await StartAsync();
        using var anonymous = backend.NewAnonymousClient();

        (await anonymous.GetAsync(Url("/api/info"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await anonymous.GetAsync(Url("/api/v1/me"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync(Url("/api/v1/projects"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync(Url("/"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Projects_List_ShowsOnlyTheProjectsOfTheMember()
    {
        await using var backend = await StartAsync();
        var mine = await backend.CreateProjectAsync("Chroniques");
        var other = await backend.CreateProjectAsync("Secret");
        backend.Accounts.Store.SetMember(mine.Id, "camille", ProjectRole.Editor);
        using var camille = backend.ClientFor("camille");

        var listed = await (await camille.GetAsync(Url("/api/v1/projects"), Ct)).ReadAsync<ProjectSummary[]>();

        listed.Select(p => p.Id).ShouldBe([mine.Id]);
        listed[0].MyRole.ShouldBe(ProjectRole.Editor);
        listed.ShouldNotContain(p => p.Id == other.Id);
    }

    [Fact]
    public async Task Project_NonMember_GetsA404ThatDoesNotRevealTheProject()
    {
        await using var backend = await StartAsync();
        var project = await backend.CreateProjectAsync("Secret");
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa Varenne");
        using var outsider = backend.ClientFor("externe");

        using var response = await outsider.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{page.Id}"), Ct);
        using var unknown = await outsider.GetAsync(Url($"/api/v1/projects/{ElementId.NewRandom()}/pages/{page.Id}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ProblemCodeAsync()).ShouldBe("project.notFound");
        var leaked = await response.Content.ReadAsStringAsync(Ct);
        leaked.ShouldNotContain("Ilsa");
        leaked.ShouldNotContain("Secret");
        // Same answer down to the byte, the trace identifier of the request apart.
        WithoutTrace(await unknown.Content.ReadAsStringAsync(Ct)).ShouldBe(WithoutTrace(leaked));
    }

    private static string WithoutTrace(string problem) =>
        System.Text.RegularExpressions.Regex.Replace(problem, "\"traceId\":\"[^\"]*\"", string.Empty, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));

    [Fact]
    public async Task Reader_CanRead_ButAForgedWriteIsRefusedWith403()
    {
        await using var backend = await StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Version 1");
        backend.Accounts.Store.SetMember(project.Id, "camille", ProjectRole.Reader);
        using var reader = backend.ClientFor("camille");

        using var read = await reader.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{page.Id}"), Ct);
        using var write = new HttpRequestMessage(HttpMethod.Put, Url($"/api/v1/projects/{project.Id}/pages/{page.Id}/content"))
        {
            Content = JsonContent.Create(new SavePageContentRequest("Piraté"), options: ApiJson.Options),
        };
        write.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        using var forged = await reader.SendAsync(write, Ct);

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        forged.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await forged.ProblemCodeAsync()).ShouldBe("access.forbidden");
        using var after = await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{page.Id}/content"), Ct);
        (await after.ReadAsync<PageContent>()).Markdown.ShouldBe("Version 1");
    }

    [Fact]
    public async Task Reader_CanRecordItsOwnViews()
    {
        await using var backend = await StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        backend.Accounts.Store.SetMember(project.Id, "camille", ProjectRole.Reader);
        using var reader = backend.ClientFor("camille");

        using var response = await reader.PostJsonAsync($"/api/v1/projects/{project.Id}/me/views", new { elementId = page.Id });

        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task Editor_CannotCreateAProject()
    {
        await using var backend = await StartAsync();
        using var camille = backend.ClientFor("camille");

        using var response = await camille.PostJsonAsync("/api/v1/projects", new CreateProjectRequest(null, "Atlas", "fr"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProject_ByTheServerAdministrator_IsHostedByTheServerAndAdministeredByItsCreator()
    {
        await using var backend = await StartAsync(configure: o => o with { FolderProjects = false });

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest(null, "Atlas", "fr"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var project = await response.ReadAsync<ProjectSummary>();
        project.MyRole.ShouldBe(ProjectRole.Admin);
        project.Folder.ShouldBeEmpty();
        Directory.Exists(Path.Combine(backend.Options.DataDirectory!, "projects")).ShouldBeTrue();
        backend.Accounts.Store.GetRole(project.Id, TestBackend.AdminLogin).ShouldBe(ProjectRole.Admin);
    }

    [Fact]
    public async Task OpenProject_ByPathOnAServer_IsDisabled()
    {
        await using var backend = await StartAsync(configure: o => o with { FolderProjects = false });

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(backend.Options.DataDirectory!));
        using var withFolder = await backend.Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest(backend.Options.DataDirectory!, "Atlas", "fr"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        withFolder.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Write_WithTheCookieFromAnotherOrigin_IsRefused()
    {
        await using var backend = await StartAsync();
        var project = await backend.CreateProjectAsync();
        backend.Accounts.Store.SetMember(project.Id, "camille", ProjectRole.Editor);
        using var anonymous = backend.NewAnonymousClient();
        var (_, cookie) = await LoginAsync(anonymous, "camille", "camille-pass-1");

        HttpRequestMessage Create(string? origin)
        {
            var request = WithCookie(HttpMethod.Post, $"/api/v1/projects/{project.Id}/folders", cookie!);
            request.Content = JsonContent.Create(new CreateFolderRequest(Roots.Pages.Id, "Lieux"), options: ApiJson.Options);
            if (origin is not null)
            {
                request.Headers.Add("Origin", origin);
            }

            return request;
        }

        using var foreign = await anonymous.SendAsync(Create("https://evil.example"), Ct);
        using var same = await anonymous.SendAsync(Create("http://localhost"), Ct);

        foreign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await foreign.ProblemCodeAsync()).ShouldBe("access.crossOrigin");
        same.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Authors_AreTheAccountNames()
    {
        await using var backend = await StartAsync();
        var project = await backend.CreateProjectAsync();
        backend.Accounts.Store.SetMember(project.Id, "camille", ProjectRole.Editor);
        using var camille = backend.ClientFor("camille");

        using var response = await camille.PostJsonAsync($"/api/v1/projects/{project.Id}/pages", new CreatePageRequest(Roots.Pages.Id, "Ilsa"));

        var page = await response.ReadAsync<ElementDetails>();
        page.CreatedBy.ShouldBe("camille");
        page.CreatedByName.ShouldBe("Camille R.");
    }

    [Fact]
    public async Task Bootstrap_AdminFromConfiguration_CanSignInAsServerAdministrator()
    {
        await using var backend = await TestBackend.StartAsync(
            o => o with { BootstrapAdminLogin = "root", BootstrapAdminPassword = "root-password-1" }, skipDefaultAdmin: true);
        using var anonymous = backend.NewAnonymousClient();

        var (response, _) = await LoginAsync(anonymous, "root", "root-password-1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<SessionResult>()).IsServerAdmin.ShouldBeTrue();
    }
}
