using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Realtime;
using Erable.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace Erable.Server.Tests;

/// <summary>Presence (F14.R3): who reads or edits which page, and which section.</summary>
public sealed class PresenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(TestBackend Backend, ProjectSummary Project, ElementDetails Page)> StartAsync(
        Func<Erable.Server.Hosting.ErableHostOptions, Erable.Server.Hosting.ErableHostOptions>? configure = null)
    {
        var backend = await TestBackend.StartAsync(configure);
        var project = await backend.CreateProjectAsync("Chroniques");
        foreach (var (login, name) in new[] { ("camille", "Camille R."), ("malo", "Malo L.") })
        {
            backend.Accounts.CreateAccount(login, name, "password-" + login).IsSuccess.ShouldBeTrue();
            backend.Accounts.Store.SetMember(project.Id, login, ProjectRole.Editor);
        }

        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa Varenne", "# Présentation\n\n# Histoire\n");
        return (backend, project, page);
    }

    [Fact]
    public async Task JoinProject_AnswersWithWhoIsThere()
    {
        var (backend, project, _) = await StartAsync();
        await using var _backend = backend;
        await using var camille = new HubClient(backend, "camille");
        await camille.JoinAsync(project.Id);
        await using var malo = new HubClient(backend, "malo");

        var joined = await malo.JoinWithPresenceAsync(project.Id);

        joined.Presence.Select(u => u.UserId).ShouldBe(["camille", "malo"]);
        joined.Presence.Select(u => u.Name).ShouldBe(["Camille R.", "Malo L."]);
        joined.Presence.ShouldAllBe(u => u.Mode == PresenceMode.Read && u.ElementId == null);
    }

    [Fact]
    public async Task SetPresence_Editing_IsSeenByTheOthersWithItsSection()
    {
        var (backend, project, page) = await StartAsync();
        await using var _backend = backend;
        await using var malo = new HubClient(backend, "malo");
        await using var camille = new HubClient(backend, "camille");
        await malo.JoinAsync(project.Id);
        await camille.JoinAsync(project.Id);

        await camille.SetPresenceAsync(project.Id, page.Id, "edit", "  Histoire  ");

        var seen = await malo.PresenceWhereAsync(p => p.Users.Any(u => u.UserId == "camille" && u.Mode == PresenceMode.Edit));
        var user = seen.Users.Single(u => u.UserId == "camille");
        user.ElementId.ShouldBe(page.Id);
        user.Section.ShouldBe("Histoire");
        user.Name.ShouldBe("Camille R.");
    }

    [Fact]
    public async Task SetPresence_Reading_HasNoSection()
    {
        var (backend, project, page) = await StartAsync();
        await using var _backend = backend;
        await using var malo = new HubClient(backend, "malo");
        await using var camille = new HubClient(backend, "camille");
        await malo.JoinAsync(project.Id);
        await camille.JoinAsync(project.Id);

        await camille.SetPresenceAsync(project.Id, page.Id, "read", "Histoire");

        var seen = await malo.PresenceWhereAsync(p => p.Users.Any(u => u.UserId == "camille" && u.ElementId == page.Id));
        seen.Users.Single(u => u.UserId == "camille").Section.ShouldBeNull();
    }

    [Fact]
    public async Task SetPresence_InvalidValues_AreIgnored()
    {
        var (backend, project, page) = await StartAsync();
        await using var _backend = backend;
        await using var malo = new HubClient(backend, "malo");
        await using var camille = new HubClient(backend, "camille");
        await malo.JoinAsync(project.Id);
        await camille.JoinAsync(project.Id);

        await camille.SetPresenceAsync(project.Id, page.Id, "shout");
        await camille.Connection.InvokeAsync("SetPresence", project.Id.ToString(), "not-an-id", "read", null, Ct);
        await camille.SetPresenceAsync(project.Id, page.Id, "edit", new string('x', 1000));

        var seen = await malo.PresenceWhereAsync(p => p.Users.Any(u => u.UserId == "camille" && u.Mode == PresenceMode.Edit));
        seen.Users.Single(u => u.UserId == "camille").Section!.Length.ShouldBe(ProjectConnections.MaxSectionLength);
    }

    [Fact]
    public async Task SetPresence_ForAProjectNotJoined_ChangesNothing()
    {
        var (backend, project, page) = await StartAsync();
        await using var _backend = backend;
        var other = await backend.CreateProjectAsync("Autre");
        backend.Accounts.Store.SetMember(other.Id, "camille", ProjectRole.Editor);
        await using var malo = new HubClient(backend, "malo");
        await using var camille = new HubClient(backend, "camille");
        await malo.JoinAsync(project.Id);
        await camille.JoinAsync(project.Id);

        await camille.SetPresenceAsync(other.Id, page.Id, "edit", "Histoire");
        await camille.SetPresenceAsync(project.Id, page.Id, "edit", "Présentation");

        // The first call, for a project camille did not join, changed nothing: the first edit seen is the second call.
        var seen = await malo.PresenceWhereAsync(p => p.Users.Any(u => u.UserId == "camille" && u.Mode == PresenceMode.Edit));
        seen.Users.Single(u => u.UserId == "camille").Section.ShouldBe("Présentation");
    }

    [Fact]
    public async Task TwoTabsOfOneUser_AreOneLineShowingTheLatestActivity()
    {
        var (backend, project, page) = await StartAsync();
        await using var _backend = backend;
        await using var malo = new HubClient(backend, "malo");
        await using var first = new HubClient(backend, "camille");
        await using var second = new HubClient(backend, "camille");
        await malo.JoinAsync(project.Id);
        await first.JoinAsync(project.Id);
        await second.JoinAsync(project.Id);

        await first.SetPresenceAsync(project.Id, page.Id, "edit", "Histoire");
        await second.SetPresenceAsync(project.Id, null, "read");

        var seen = await malo.PresenceWhereAsync(p =>
            p.Users.Count(u => u.UserId == "camille") == 1 && p.Users.Single(u => u.UserId == "camille").Mode == PresenceMode.Read
            && p.Users.Single(u => u.UserId == "camille").ElementId is null && p.Users.Count == 2);
        seen.Users.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Leaving_RemovesThePresenceForTheOthers()
    {
        var (backend, project, _) = await StartAsync();
        await using var _backend = backend;
        await using var malo = new HubClient(backend, "malo");
        var camille = new HubClient(backend, "camille");
        await malo.JoinAsync(project.Id);
        await camille.JoinAsync(project.Id);
        await malo.PresenceWhereAsync(p => p.Users.Any(u => u.UserId == "camille"));

        await camille.Connection.StopAsync(Ct);
        await camille.DisposeAsync();

        await malo.PresenceWhereAsync(p => p.Users.All(u => u.UserId != "camille"));
    }

    [Fact]
    public async Task Presence_OfAnotherProject_IsNotShown()
    {
        var (backend, project, _) = await StartAsync();
        await using var _backend = backend;
        var other = await backend.CreateProjectAsync("Autre");
        backend.Accounts.Store.SetMember(other.Id, "camille", ProjectRole.Editor);
        await using var camille = new HubClient(backend, "camille");
        await camille.JoinAsync(other.Id);
        await using var malo = new HubClient(backend, "malo");

        var joined = await malo.JoinWithPresenceAsync(project.Id);

        joined.Presence.Select(u => u.UserId).ShouldBe(["malo"]);
    }

    [Fact]
    public async Task Presence_OfAClientThatStopsAnswering_ExpiresWithoutAGoodbye()
    {
        // The server gives up on a silent client after one second (30 s at most in production, E2E-14-05).
        var (backend, project, _) = await StartAsync(o => o with
        {
            HubKeepAlive = TimeSpan.FromMilliseconds(250),
            HubClientTimeout = TimeSpan.FromSeconds(1),
        });
        await using var _backend = backend;
        await using var malo = new HubClient(backend, "malo", keepAlive: TimeSpan.FromMilliseconds(250));
        // A client that never pings: as if its network had been cut.
        await using var camille = new HubClient(backend, "camille", keepAlive: TimeSpan.FromHours(1));
        await malo.JoinAsync(project.Id);
        await camille.JoinAsync(project.Id);
        await malo.PresenceWhereAsync(p => p.Users.Any(u => u.UserId == "camille"));

        await malo.PresenceWhereAsync(p => p.Users.All(u => u.UserId != "camille"));
    }
}
