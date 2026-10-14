using Erable.Server.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>
/// In-memory backend built through the same <see cref="ErableHost.Build"/> entry point as production,
/// with its own temporary web root. Each instance is isolated: tests can run in parallel.
/// </summary>
public sealed class TestBackend : IAsyncDisposable
{
    public const string IndexHtml = "<!doctype html><html><body><div id=\"root\"></div></body></html>";

    private readonly WebApplication _app;
    private readonly string _webRoot;

    private TestBackend(WebApplication app, string webRoot)
    {
        _app = app;
        _webRoot = webRoot;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<TestBackend> StartAsync(Func<ErableHostOptions, ErableHostOptions>? configure = null)
    {
        var webRoot = Directory.CreateTempSubdirectory("erable-web-").FullName;
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), IndexHtml, TestContext.Current.CancellationToken);

        var options = new ErableHostOptions { Mode = HostMode.Server, WebRootPath = webRoot };
        options = configure?.Invoke(options) ?? options;

        var app = ErableHost.Build(options, builder => builder.WebHost.UseTestServer());
        await app.StartAsync(TestContext.Current.CancellationToken);
        return new TestBackend(app, webRoot);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }
}
