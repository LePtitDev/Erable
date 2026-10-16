using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Realtime;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests;

/// <summary>Real-time events (M4-03, F14.R4): a write is announced to the members who joined the project, after the commit.</summary>
public sealed class HubEventsTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A client of the hub, signed in as <paramref name="login"/>; everything it receives is queued in order.</summary>
    private sealed class HubClient : IAsyncDisposable
    {
        private readonly Channel<object> _events = Channel.CreateUnbounded<object>();

        public HubClient(TestBackend backend, string login)
        {
            var token = backend.Accounts.StartSession(login, ServerStore.SessionKindBearer).Token;
            var server = backend.Server;
            Connection = new HubConnectionBuilder()
                .WithUrl(new Uri(server.BaseAddress, ApiRoutes.ProjectHub), options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .Build();
            Connection.On<ElementChangedEvent>("ElementChanged", e => _events.Writer.TryWrite(e));
            Connection.On<ElementMovedEvent>("ElementMoved", e => _events.Writer.TryWrite(e));
            Connection.On<ElementDeletedEvent>("ElementDeleted", e => _events.Writer.TryWrite(e));
            Connection.On<TreeChangedEvent>("TreeChanged", e => _events.Writer.TryWrite(e));
        }

        public HubConnection Connection { get; }

        public async Task<bool> JoinAsync(ElementId projectId)
        {
            if (Connection.State == HubConnectionState.Disconnected)
            {
                await Connection.StartAsync(Ct);
            }

            return (await Connection.InvokeAsync<JoinResult>("JoinProject", projectId.ToString(), Ct)).Joined;
        }

        public async Task<T> NextAsync<T>()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(Wait);
            var next = await _events.Reader.ReadAsync(timeout.Token);
            return next.ShouldBeOfType<T>();
        }

        /// <summary>Reads the next event with the caller's own cancellation (to check that nothing arrives).</summary>
        public async Task<T> NextAsyncWithToken<T>(CancellationToken cancellation) =>
            (await _events.Reader.ReadAsync(cancellation)).ShouldBeOfType<T>();

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }

    private static async Task<(TestBackend Backend, ProjectSummary Project)> StartAsync()
    {
        var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync("Chroniques");
        foreach (var login in new[] { "camille", "malo" })
        {
            backend.Accounts.CreateAccount(login, login, "password-" + login).IsSuccess.ShouldBeTrue();
            backend.Accounts.Store.SetMember(project.Id, login, ProjectRole.Editor);
        }

        return (backend, project);
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, ElementId project, ElementId page, string markdown, string revision)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/v1/projects/{project}/pages/{page}/content", UriKind.Relative))
        {
            Content = JsonContent.Create(new SavePageContentRequest(markdown), options: ApiJson.Options),
        };
        request.Headers.TryAddWithoutValidation("If-Match", revision);
        return client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task SavePage_ByOneMember_IsAnnouncedToAnotherMemberOfTheProject()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Version 1");
        await using var malo = new HubClient(backend, "malo");
        (await malo.JoinAsync(project.Id)).ShouldBeTrue();
        using var camille = backend.ClientFor("camille");

        using var saved = await SaveAsync(camille, project.Id, page.Id, "Version 2", "\"1\"");

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        var change = await malo.NextAsync<ElementChangedEvent>();
        change.ProjectId.ShouldBe(project.Id);
        change.ElementId.ShouldBe(page.Id);
        change.Revision.ShouldBe(2);
        change.By.ShouldBe("camille");
    }

    [Fact]
    public async Task Events_OfAnotherProject_DoNotReachAClientThatDidNotJoinIt()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var other = await backend.CreateProjectAsync("Autre");
        backend.Accounts.Store.SetMember(other.Id, "malo", ProjectRole.Editor);
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Version 1");
        var otherPage = await backend.CreatePageAsync(other.Id, Roots.Pages.Id, "Secret", "Version 1");
        await using var malo = new HubClient(backend, "malo");
        (await malo.JoinAsync(other.Id)).ShouldBeTrue();

        // The write of the first project comes first: if it were delivered, it would be the first event received.
        (await SaveAsync(backend.Client, project.Id, page.Id, "Version 2", "\"1\"")).Dispose();
        (await SaveAsync(backend.Client, other.Id, otherPage.Id, "Version 2", "\"1\"")).Dispose();

        var change = await malo.NextAsync<ElementChangedEvent>();
        change.ProjectId.ShouldBe(other.Id);
    }

    [Fact]
    public async Task JoinProject_NotAMember_IsRefusedAndReceivesNothing()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        backend.Accounts.CreateAccount("externe", "Externe", "password-externe").IsSuccess.ShouldBeTrue();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Version 1");
        await using var stranger = new HubClient(backend, "externe");
        await using var camille = new HubClient(backend, "camille");
        (await camille.JoinAsync(project.Id)).ShouldBeTrue();

        (await stranger.JoinAsync(project.Id)).ShouldBeFalse();
        (await SaveAsync(backend.Client, project.Id, page.Id, "Version 2", "\"1\"")).Dispose();

        await camille.NextAsync<ElementChangedEvent>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            using var brief = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            brief.CancelAfter(TimeSpan.FromMilliseconds(300));
            await stranger.NextAsyncWithToken<ElementChangedEvent>(brief.Token);
        });
    }

    [Fact]
    public async Task JoinProject_UnknownProject_IsRefused()
    {
        var (backend, _) = await StartAsync();
        await using var _backend = backend;
        await using var camille = new HubClient(backend, "camille");

        (await camille.JoinAsync(ElementId.NewRandom())).ShouldBeFalse();
    }

    [Fact]
    public async Task FailedWrite_AnnouncesNothing()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Version 1");
        (await SaveAsync(backend.Client, project.Id, page.Id, "Version 2", "\"1\"")).Dispose();
        await using var malo = new HubClient(backend, "malo");
        (await malo.JoinAsync(project.Id)).ShouldBeTrue();

        // A stale revision (409) first, then a good write: the first event received is the second write's.
        using var stale = await SaveAsync(backend.Client, project.Id, page.Id, "Version concurrente", "\"1\"");
        (await SaveAsync(backend.Client, project.Id, page.Id, "Version 3", "\"2\"")).Dispose();

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await malo.NextAsync<ElementChangedEvent>()).Revision.ShouldBe(3);
    }

    [Fact]
    public async Task Rename_IsAnnouncedAsMovedAndTreeChanged()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        await using var malo = new HubClient(backend, "malo");
        (await malo.JoinAsync(project.Id)).ShouldBeTrue();

        using var response = await backend.Client.PatchJsonAsync($"/api/v1/projects/{project.Id}/elements/{page.Id}", new UpdateElementRequest("Hautbois-sur-Brume"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await malo.NextAsync<ElementMovedEvent>()).ElementId.ShouldBe(page.Id);
        (await malo.NextAsync<TreeChangedEvent>()).ProjectId.ShouldBe(project.Id);
    }

    [Fact]
    public async Task Delete_IsAnnouncedAsDeletedAndTreeChanged()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        await using var malo = new HubClient(backend, "malo");
        (await malo.JoinAsync(project.Id)).ShouldBeTrue();

        using var response = await backend.Client.DeleteAsync(new Uri($"/api/v1/projects/{project.Id}/elements/{page.Id}", UriKind.Relative), Ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
        (await malo.NextAsync<ElementDeletedEvent>()).ElementId.ShouldBe(page.Id);
        await malo.NextAsync<TreeChangedEvent>();
    }

    [Fact]
    public async Task CreatePage_IsAnnouncedAsTreeChanged()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        await using var malo = new HubClient(backend, "malo");
        (await malo.JoinAsync(project.Id)).ShouldBeTrue();

        await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Col gelé");

        (await malo.NextAsync<TreeChangedEvent>()).ProjectId.ShouldBe(project.Id);
    }

    [Fact]
    public async Task RevokedMember_StopsReceivingEvents()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Version 1");
        await using var malo = new HubClient(backend, "malo");
        await using var camille = new HubClient(backend, "camille");
        (await malo.JoinAsync(project.Id)).ShouldBeTrue();
        (await camille.JoinAsync(project.Id)).ShouldBeTrue();

        await backend.Services.GetRequiredService<ProjectEvents>().RevokeAsync(project.Id, "malo");
        (await SaveAsync(backend.Client, project.Id, page.Id, "Version 2", "\"1\"")).Dispose();

        await camille.NextAsync<ElementChangedEvent>();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            using var brief = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            brief.CancelAfter(TimeSpan.FromMilliseconds(300));
            await malo.NextAsyncWithToken<ElementChangedEvent>(brief.Token);
        });
    }

    [Fact]
    public async Task Hub_WithoutCredentials_Returns401()
    {
        await using var backend = await TestBackend.StartAsync();
        using var anonymous = backend.NewAnonymousClient();

        using var response = await anonymous.PostAsync(new Uri(ApiRoutes.ProjectHub + "/negotiate?negotiateVersion=1", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Disconnect_ForgetsTheConnection()
    {
        var (backend, project) = await StartAsync();
        await using var _ = backend;
        var connections = backend.Services.GetRequiredService<ProjectConnections>();
        var malo = new HubClient(backend, "malo");
        (await malo.JoinAsync(project.Id)).ShouldBeTrue();
        connections.Of(project.Id).Count.ShouldBe(1);

        await malo.Connection.StopAsync(Ct);
        await malo.DisposeAsync();

        // The server learns of the departure when the poll ends.
        var until = DateTime.UtcNow + Wait;
        while (connections.Of(project.Id).Count > 0 && DateTime.UtcNow < until)
        {
            await Task.Delay(50, Ct);
        }

        connections.Of(project.Id).ShouldBeEmpty();
    }
}
