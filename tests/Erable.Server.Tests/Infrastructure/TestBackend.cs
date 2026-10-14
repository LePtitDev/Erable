using System.Security.Cryptography;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>
/// Backend built through the same <see cref="ErableHost.Build"/> entry point as production, with its own
/// temporary web root. Server mode runs in memory (TestServer); desktop mode runs a real Kestrel on loopback,
/// because its security relies on the actual host and port. Each instance is isolated: tests run in parallel.
/// </summary>
public sealed class TestBackend : IAsyncDisposable
{
    public const string IndexHtml = "<!doctype html><html><body><div id=\"root\"></div></body></html>";

    private readonly WebApplication _app;
    private readonly string _webRoot;

    private TestBackend(WebApplication app, string webRoot, HttpClient client, ErableHostOptions options)
    {
        _app = app;
        _webRoot = webRoot;
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
        ILoggerProvider? logger = null)
    {
        var ct = TestContext.Current.CancellationToken;
        var webRoot = Directory.CreateTempSubdirectory("erable-web-").FullName;
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), IndexHtml, ct);

        var options = new ErableHostOptions { Mode = HostMode.Server, WebRootPath = webRoot };
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

        return new TestBackend(app, webRoot, client, options);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }
}
