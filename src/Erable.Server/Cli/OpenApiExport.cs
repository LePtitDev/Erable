using Erable.Server.Api;
using Erable.Server.Hosting;

namespace Erable.Server.Cli;

/// <summary>Writes the OpenAPI document to a file; source of the generated TypeScript client.</summary>
internal static class OpenApiExport
{
    public static async Task<int> RunAsync(FileInfo output, CancellationToken cancellationToken)
    {
        var options = new ErableHostOptions { Mode = HostMode.Server, Port = 0, EnvironmentName = "Production" };
        await using var app = ErableHost.Build(options, builder => builder.Logging.ClearProviders());
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var http = new HttpClient { BaseAddress = ErableHost.GetListeningUri(app) };
            var document = await http.GetStringAsync(new Uri(ApiRoutes.OpenApiPath, UriKind.Relative), cancellationToken)
                .ConfigureAwait(false);

            output.Directory?.Create();
            await File.WriteAllTextAsync(output.FullName, document.ReplaceLineEndings("\n") + "\n", cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }
        finally
        {
            await app.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
