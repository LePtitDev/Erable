using System.Collections.Concurrent;
using System.Net;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Server.Accounts;
using Erable.Server.Hosting;
using Erable.Server.Projects;
using Erable.Server.Security;
using Erable.Storage;
using Erable.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>
/// An Erable server in server mode on a real loopback port: the kind of server a desktop application connects to
///. Comes with a project and three members.
/// </summary>
public sealed class RealServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly TempDirectory _data;
    private readonly TempDirectory _web;

    private RealServer(WebApplication app, TempDirectory data, TempDirectory web, Uri address, ProjectSummaryLite project)
    {
        _app = app;
        _data = data;
        _web = web;
        Address = address;
        Project = project;
    }

    /// <summary>Origin of the server, e.g. <c>http://127.0.0.1:5123</c>.</summary>
    public Uri Address { get; }

    public ProjectSummaryLite Project { get; }

    public IServiceProvider Services => _app.Services;

    public string Origin => Address.GetLeftPart(UriPartial.Authority);

    public static async Task<RealServer> StartAsync(Func<ErableHostOptions, ErableHostOptions>? configure = null)
    {
        var data = TempDirectory.Create("erable-remote-");
        var web = TempDirectory.Create("erable-remote-web-");
        await File.WriteAllTextAsync(web.Combine("index.html"), TestBackend.IndexHtml, TestContext.Current.CancellationToken);
        var options = new ErableHostOptions
        {
            Mode = HostMode.Server,
            Address = "127.0.0.1",
            Port = 0,
            DataDirectory = data.Path,
            WebRootPath = web.Path,
            FolderProjects = true,
        };
        options = configure?.Invoke(options) ?? options;
        var app = ErableHost.Build(options, builder => builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Warning));
        await app.StartAsync(TestContext.Current.CancellationToken);

        var accounts = app.Services.GetRequiredService<AccountService>();
        accounts.CreateAccount("admin", "Admin", "password-admin", isServerAdmin: true).IsSuccess.ShouldBeTrue();
        foreach (var login in new[] { "camille", "malo", "lea" })
        {
            accounts.CreateAccount(login, login, "password-" + login).IsSuccess.ShouldBeTrue();
        }

        var catalog = app.Services.GetRequiredService<ProjectCatalog>();
        var created = catalog.Create("Chroniques d’Aube", "fr", "admin", profile: null);
        created.IsSuccess.ShouldBeTrue();
        var projectId = created.Value.Project.Manifest.ProjectId;
        accounts.Store.SetMember(projectId, "camille", ProjectRole.Editor);
        accounts.Store.SetMember(projectId, "lea", ProjectRole.Reader);

        var uri = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First());
        return new RealServer(app, data, web, uri, new ProjectSummaryLite(projectId, "Chroniques d’Aube"));
    }

    public Task<bool> HasSessionsAsync(string login)
    {
        // The sessions of an account are removed with its password change: used by tests to end a token.
        return Task.FromResult(Services.GetRequiredService<AccountService>().Store.GetAccount(login) is not null);
    }

    /// <summary>Ends every session of an account (the token held by a client is dead from now on).</summary>
    public void EndSessionsOf(string login)
    {
        var accounts = Services.GetRequiredService<AccountService>();
        accounts.ChangePassword(login, "new-password-" + login).IsSuccess.ShouldBeTrue();
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        _web.Dispose();
        _data.Dispose();
    }
}

public sealed record ProjectSummaryLite(ElementId Id, string Name);

/// <summary>
/// A server that answers with what the test says and notes every request it gets. Used for a server of another version
/// (E2E-16-06: no other request than the handshake may reach it) and to see what the application forwards.
/// </summary>
public sealed class FakeServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    private FakeServer(WebApplication app, Uri address)
    {
        _app = app;
        Address = address;
    }

    public Uri Address { get; }

    public string Origin => Address.GetLeftPart(UriPartial.Authority);

    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    public sealed record RecordedRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers);

    /// <param name="answer">Builds the answer of a request: status and JSON body.</param>
    public static async Task<FakeServer> StartAsync(Func<string, string, (int Status, string Json)> answer)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseKestrel(k => k.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        var app = builder.Build();
        FakeServer? self = null;
        app.Run(async context =>
        {
            self!._requests.Enqueue(new RecordedRequest(
                context.Request.Method,
                context.Request.Path + context.Request.QueryString,
                context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase)));
            var (status, json) = answer(context.Request.Method, context.Request.Path);
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        var address = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First());
        self = new FakeServer(app, address);
        return self;
    }

    /// <summary>A server that speaks another version of the API.</summary>
    public static Task<FakeServer> OfAnotherVersionAsync() => StartAsync((_, path) =>
        path == "/api/info"
            ? (200, """{"appVersion":"9.0.0","apiVersion":"v9","formatVersion":42,"mode":"cloud","folderProjects":false}""")
            : (200, "{}"));

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}
