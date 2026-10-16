using System.Net;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using System.Net.Http.Json;
using Erable.Server.Api;
using Erable.Server.Accounts;
using Erable.Server.Hosting;
using Erable.Server.Realtime;
using Erable.Server.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests;

/// <summary>Members of a project and accounts of the server (M4-08, E2E-14-10, F14.R1).</summary>
public sealed class MembersApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static async Task<(TestBackend Backend, ProjectSummary Project)> StartAsync()
    {
        var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync("Chroniques");
        foreach (var (login, role) in new[] { ("camille", ProjectRole.Editor), ("lea", ProjectRole.Reader) })
        {
            backend.Accounts.CreateAccount(login, login, "password-" + login).IsSuccess.ShouldBeTrue();
            backend.Accounts.Store.SetMember(project.Id, login, role);
        }

        backend.Accounts.CreateAccount("externe", "Externe", "password-externe").IsSuccess.ShouldBeTrue();
        return (backend, project);
    }

    private static string Members(ElementId project) => $"/api/v1/projects/{project}/members";

    // ─── Who may see the members ────────────────────────────────────────────

    [Fact]
    public async Task List_ByAnAdministrator_ShowsEveryoneWithTheirRole()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;

        var members = await (await backend.Client.GetAsync(Url(Members(project.Id)), Ct)).ReadAsync<MemberItem[]>();

        members.Select(m => (m.UserId, m.Role)).ShouldBe(
            [("admin", ProjectRole.Admin), ("camille", ProjectRole.Editor), ("lea", ProjectRole.Reader)], ignoreOrder: true);
        members.Single(m => m.UserId == "admin").IsMe.ShouldBeTrue();
        members.Single(m => m.UserId == "lea").IsMe.ShouldBeFalse();
    }

    [Fact]
    public async Task Members_ByAnEditorOrAReader_AreForbidden()
    {
        // E2E-14-10: « un éditeur n'a pas accès à l'écran de gestion des membres »
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        using var camille = backend.ClientFor("camille");
        using var lea = backend.ClientFor("lea");

        (await camille.GetAsync(Url(Members(project.Id)), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await lea.GetAsync(Url(Members(project.Id)), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await camille.PutAsJsonAsync(Url(Members(project.Id) + "/lea"), new SetMemberRequest(ProjectRole.Admin), ApiJson.Options, Ct))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await camille.DeleteAsync(Url(Members(project.Id) + "/lea"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Members_ByAStranger_AreNotFound()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        using var externe = backend.ClientFor("externe");

        (await externe.GetAsync(Url(Members(project.Id)), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ─── Changing roles ─────────────────────────────────────────────────────

    [Fact]
    public async Task SetMember_ReaderToEditor_TakesEffectAtTheNextRequest()
    {
        // E2E-14-10: lea passe de Lecteur à Éditeur et peut alors écrire.
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        using var lea = backend.ClientFor("lea");
        (await lea.PostJsonAsync($"/api/v1/projects/{project.Id}/folders", new CreateFolderRequest(Roots.Pages.Id, "Avant")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var response = await backend.Client.PutAsJsonAsync(Url(Members(project.Id) + "/lea"), new SetMemberRequest(ProjectRole.Editor), ApiJson.Options, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<MemberItem>()).Role.ShouldBe(ProjectRole.Editor);
        (await lea.PostJsonAsync($"/api/v1/projects/{project.Id}/folders", new CreateFolderRequest(Roots.Pages.Id, "Après")))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await (await lea.GetAsync(Url($"/api/v1/projects/{project.Id}"), Ct)).ReadAsync<ProjectSummary>()).MyRole.ShouldBe(ProjectRole.Editor);
        page.Id.ShouldNotBe(ElementId.None);
    }

    [Fact]
    public async Task SetMember_AnExistingAccount_JoinsTheProject()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PutAsJsonAsync(Url(Members(project.Id) + "/Externe"), new SetMemberRequest(ProjectRole.Reader), ApiJson.Options, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        backend.Accounts.Store.GetRole(project.Id, "externe").ShouldBe(ProjectRole.Reader);
    }

    [Fact]
    public async Task SetMember_UnknownAccount_Returns404()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PutAsJsonAsync(Url(Members(project.Id) + "/personne"), new SetMemberRequest(ProjectRole.Reader), ApiJson.Options, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.AccountNotFound);
    }

    [Fact]
    public async Task SetMember_DemotingTheLastAdministrator_Returns409()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PutAsJsonAsync(Url(Members(project.Id) + "/admin"), new SetMemberRequest(ProjectRole.Editor), ApiJson.Options, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.MemberLastAdmin);
        backend.Accounts.Store.GetRole(project.Id, "admin").ShouldBe(ProjectRole.Admin);
    }

    [Fact]
    public async Task SetMember_AnUnknownRole_Returns422()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PutAsJsonAsync(Url(Members(project.Id) + "/lea"), new { role = "owner" }, ApiJson.Options, Ct);

        response.IsSuccessStatusCode.ShouldBeFalse();
    }

    // ─── Removing ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_AMember_LosesTheProjectAtOnce()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        using var camille = backend.ClientFor("camille");
        (await camille.GetAsync(Url($"/api/v1/projects/{project.Id}/tree"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await backend.Client.DeleteAsync(Url(Members(project.Id) + "/camille"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await camille.GetAsync(Url($"/api/v1/projects/{project.Id}/tree"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var listed = await (await camille.GetAsync(Url("/api/v1/projects"), Ct)).ReadAsync<ProjectSummary[]>();
        listed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Remove_TheLastAdministrator_Returns409()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.DeleteAsync(Url(Members(project.Id) + "/admin"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.MemberLastAdmin);
    }

    [Fact]
    public async Task Remove_ANonMember_Returns404()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.DeleteAsync(Url(Members(project.Id) + "/externe"), Ct);

        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.MemberNotFound);
    }

    // ─── Live ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangingARole_TellsTheConnectedMembers()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        await using var lea = new HubClient(backend, "lea");
        (await lea.JoinAsync(project.Id)).ShouldBeTrue();

        (await backend.Client.PutAsJsonAsync(Url(Members(project.Id) + "/lea"), new SetMemberRequest(ProjectRole.Editor), ApiJson.Options, Ct)).Dispose();

        (await lea.NextAsync<MembersChangedEvent>()).ProjectId.ShouldBe(project.Id);
    }

    [Fact]
    public async Task RemovingAMember_CutsTheirEventsAtOnce()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Version 1");
        await using var camille = new HubClient(backend, "camille");
        await using var lea = new HubClient(backend, "lea");
        (await camille.JoinAsync(project.Id)).ShouldBeTrue();
        (await lea.JoinAsync(project.Id)).ShouldBeTrue();

        (await backend.Client.DeleteAsync(Url(Members(project.Id) + "/camille"), Ct)).Dispose();
        using var save = new HttpRequestMessage(HttpMethod.Put, Url($"/api/v1/projects/{project.Id}/pages/{page.Id}/content"))
        {
            Content = System.Net.Http.Json.JsonContent.Create(new SavePageContentRequest("Version 2"), options: ApiJson.Options),
        };
        save.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        (await backend.Client.SendAsync(save, Ct)).Dispose();

        // The reader still connected hears about the save; the removed editor hears nothing of it.
        await lea.NextAsync<MembersChangedEvent>();
        (await lea.NextAsync<ElementChangedEvent>()).ElementId.ShouldBe(page.Id);
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            using var brief = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            brief.CancelAfter(TimeSpan.FromMilliseconds(300));
            await camille.NextAsyncWithToken<ElementChangedEvent>(brief.Token);
        });
    }

    // ─── Accounts of the server ─────────────────────────────────────────────

    [Fact]
    public async Task Accounts_AreCreatedByTheServerAdministratorOnly()
    {
        var (backend, _) = await StartAsync();
        await using var _backend = backend;
        using var camille = backend.ClientFor("camille");

        var request = new CreateAccountRequest("nouvelle", "Nouvelle Personne", "mot-de-passe-1");

        (await camille.PostJsonAsync("/api/v1/accounts", request)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var created = await backend.Client.PostJsonAsync("/api/v1/accounts", request);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var account = await created.ReadAsync<AccountItem>();
        account.Id.ShouldBe("nouvelle");
        account.Name.ShouldBe("Nouvelle Personne");
        account.IsServerAdmin.ShouldBeFalse();
        // The new account can sign in.
        using var anonymous = backend.NewAnonymousClient();
        (await anonymous.PostJsonAsync("/api/v1/auth/login", new LoginRequest("nouvelle", "mot-de-passe-1"))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("a", "Nom", "mot-de-passe-1", HttpStatusCode.UnprocessableEntity)]
    [InlineData("valide", "", "mot-de-passe-1", HttpStatusCode.UnprocessableEntity)]
    [InlineData("valide", "Nom", "court", HttpStatusCode.UnprocessableEntity)]
    [InlineData("camille", "Doublon", "mot-de-passe-1", HttpStatusCode.Conflict)]
    public async Task CreateAccount_Invalid_IsRefused(string login, string name, string password, HttpStatusCode expected)
    {
        var (backend, _) = await StartAsync();
        await using var _backend = backend;

        using var response = await backend.Client.PostJsonAsync("/api/v1/accounts", new CreateAccountRequest(login, name, password));

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task UpdateAccount_Disabling_EndsItsSessionsAndBlocksTheLogin()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        using var camille = backend.ClientFor("camille");
        (await camille.GetAsync(Url("/api/v1/me"), Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await backend.Client.PatchJsonAsync("/api/v1/accounts/camille", new UpdateAccountRequest(Disabled: true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await camille.GetAsync(Url("/api/v1/me"), Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var anonymous = backend.NewAnonymousClient();
        (await anonymous.PostJsonAsync("/api/v1/auth/login", new LoginRequest("camille", "password-camille"))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        project.Id.ShouldNotBe(ElementId.None);
    }

    [Fact]
    public async Task UpdateAccount_NewPassword_ReplacesTheOldOne()
    {
        var (backend, _) = await StartAsync();
        await using var _backend = backend;

        (await backend.Client.PatchJsonAsync("/api/v1/accounts/camille", new UpdateAccountRequest(Password: "nouveau-mot-de-passe"))).EnsureSuccessStatusCode();

        using var anonymous = backend.NewAnonymousClient();
        (await anonymous.PostJsonAsync("/api/v1/auth/login", new LoginRequest("camille", "password-camille"))).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostJsonAsync("/api/v1/auth/login", new LoginRequest("camille", "nouveau-mot-de-passe"))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateAccount_TheLastServerAdministrator_CannotBeDisabledNorDemoted()
    {
        var (backend, _) = await StartAsync();
        await using var _backend = backend;

        using var disable = await backend.Client.PatchJsonAsync("/api/v1/accounts/admin", new UpdateAccountRequest(Disabled: true));
        using var demote = await backend.Client.PatchJsonAsync("/api/v1/accounts/admin", new UpdateAccountRequest(IsServerAdmin: false));

        disable.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        demote.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await disable.ProblemCodeAsync()).ShouldBe(ErrorCodes.AccountLastAdmin);
    }

    [Fact]
    public async Task ListAccounts_NeverShowsPasswordHashes()
    {
        var (backend, _) = await StartAsync();
        await using var _backend = backend;

        var body = await (await backend.Client.GetAsync(Url("/api/v1/accounts"), Ct)).Content.ReadAsStringAsync(Ct);

        body.ShouldContain("camille");
        body.ShouldNotContain("AQAAAA", customMessage: "an Identity hash starts with it");
        body.ShouldNotContain("hash", Case.Insensitive);
    }

    [Fact]
    public async Task Desktop_HasNeitherMembersNorAccounts()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop });
        var project = await backend.CreateProjectAsync();

        (await backend.Client.GetAsync(Url(Members(project.Id)), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await backend.Client.GetAsync(Url("/api/v1/accounts"), Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        backend.Services.GetService<AccountService>().ShouldNotBeNull();
    }
}
