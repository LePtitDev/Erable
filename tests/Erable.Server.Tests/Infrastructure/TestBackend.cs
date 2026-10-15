using System.Security.Cryptography;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Erable.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>
/// Backend built through the same <see cref="ErableHost.Build"/> entry point as production, with its own
/// temporary web root and data folder (the future project storage). Server mode runs in memory (TestServer); desktop mode runs a real Kestrel on loopback,
/// because its security relies on the actual host and port. Each instance is isolated: tests run in parallel.
/// </summary>
public sealed class TestBackend : IAsyncDisposable
{
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

    /// <summary>Base address of a real listening backend (desktop mode only).</summary>
    public Uri BaseAddress => ErableHost.GetListeningUri(_app);

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static async Task<TestBackend> StartAsync(
        Func<ErableHostOptions, ErableHostOptions>? configure = null,
        ILoggerProvider? logger = null,
        Action<IServiceCollection>? services = null)
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
