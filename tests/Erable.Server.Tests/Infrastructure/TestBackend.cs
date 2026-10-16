using System.Net.Http.Headers;
using System.Security.Cryptography;
using Erable.Server.Accounts;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Erable.Storage;
using Erable.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>
/// Backend built through the same <see cref="ErableHost.Build"/> entry point as production, with its own
/// temporary web root and data folder (the future project storage). Server mode runs in memory (TestServer); desktop mode runs a real Kestrel on loopback,
/// because its security relies on the actual host and port. Each instance is isolated: tests run in parallel.
/// </summary>
public sealed class TestBackend : IAsyncDisposable
{
    /// <summary>The server administrator of a server-mode backend: the default client is signed in as this account.</summary>
    public const string AdminLogin = "admin";

    public const string AdminPassword = "admin-password-2026";

    public const string IndexHtml = "<!doctype html><html><body><div id=\"root\"></div></body></html>";

    private readonly WebApplication _app;
    private readonly TempDirectory _webRoot;
    private readonly TempDirectory _data;

    private TestBackend(WebApplication app, TempDirectory webRoot, TempDirectory data, HttpClient client, ErableHostOptions options)
    {
        _app = app;
        _webRoot = webRoot;
        _data = data;
        Client = client;
        Options = options;
    }

    /// <summary>Client authenticated like the SPA (session token header in desktop mode).</summary>
    public HttpClient Client { get; }

    public ErableHostOptions Options { get; }

    public IServiceProvider Services => _app.Services;

    /// <summary>The in-memory server (server mode): SignalR clients connect through its handler.</summary>
    public Microsoft.AspNetCore.TestHost.TestServer Server => _app.GetTestServer();

    public AccountService Accounts => _app.Services.GetRequiredService<AccountService>();

    /// <summary>A client with no credentials (server mode).</summary>
    public HttpClient NewAnonymousClient() => _app.GetTestClient();

    /// <summary>Creates the account if needed and returns a client signed in as it (bearer token, server mode).</summary>
    public HttpClient ClientFor(string login, bool serverAdmin = false)
    {
        if (Accounts.Store.GetAccount(login) is null)
        {
            Accounts.CreateAccount(login, char.ToUpperInvariant(login[0]) + login[1..], "password-" + login, serverAdmin).IsSuccess.ShouldBeTrue();
        }

        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Accounts.StartSession(login, ServerStore.SessionKindBearer).Token);
        return client;
    }

    /// <summary>Base address of a real listening backend (desktop mode only).</summary>
    public Uri BaseAddress => ErableHost.GetListeningUri(_app);

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static async Task<TestBackend> StartAsync(
        Func<ErableHostOptions, ErableHostOptions>? configure = null,
        ILoggerProvider? logger = null,
        Action<IServiceCollection>? services = null,
        bool skipDefaultAdmin = false,
        Action<WebApplication>? beforeStart = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var webRoot = TempDirectory.Create("erable-web-");
        var data = TempDirectory.Create("erable-data-");
        await File.WriteAllTextAsync(webRoot.Combine("index.html"), IndexHtml, ct);

        var options = new ErableHostOptions
        {
            Mode = HostMode.Server,
            WebRootPath = webRoot.Path,
            DataDirectory = data.Path,
            // The tests of the project API create and open projects by path, as the desktop does.
            FolderProjects = true,
            FixturesDirectory = RepositoryPaths.Combine("spec", "projects"),
        };
        options = configure?.Invoke(options) ?? options;
        var desktop = options.Mode == HostMode.Desktop;
        if (desktop)
        {
            options = options with { Address = ErableHost.DesktopAddress, Port = 0, SessionToken = options.SessionToken ?? NewToken() };
        }

        var app = ErableHost.Build(options, builder =>
        {
            if (!desktop)
            {
                builder.WebHost.UseTestServer();
            }

            services?.Invoke(builder.Services);

            if (logger is not null)
            {
                builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(logger);
            }
        });
        beforeStart?.Invoke(app);
        await app.StartAsync(ct);

        HttpClient client;
        if (desktop)
        {
            client = new HttpClient { BaseAddress = ErableHost.GetListeningUri(app) };
            client.DefaultRequestHeaders.Add(DesktopSession.TokenHeader, options.SessionToken);
        }
        else
        {
            client = app.GetTestClient();
            if (skipDefaultAdmin)
            {
                return new TestBackend(app, webRoot, data, app.GetTestClient(), options);
            }

            var accounts = app.Services.GetRequiredService<AccountService>();
            // Test mode seeds its own « admin » (spec/projects/users.json): it is that account then.
            if (accounts.Store.GetAccount(AdminLogin) is null)
            {
                accounts.CreateAccount(AdminLogin, "Admin", AdminPassword, isServerAdmin: true).IsSuccess.ShouldBeTrue();
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accounts.StartSession(AdminLogin, ServerStore.SessionKindBearer).Token);
        }

        return new TestBackend(app, webRoot, data, client, options);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
        _webRoot.Dispose();
        _data.Dispose();
    }
}
