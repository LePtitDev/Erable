using System.CommandLine;
using Erable.Server.Hosting;

namespace Erable.Server.Cli;

/// <summary>Command line of the <c>erable</c> executable.</summary>
public static class ErableCli
{
    public const int DefaultPort = 5080;

    public static Task<int> RunAsync(string[] args)
    {
        var folder = new Argument<DirectoryInfo>("folder")
        {
            Description = "Data folder containing the projects to serve.",
        };
        var port = new Option<int>("--port")
        {
            Description = "TCP port to listen on (0 = random).",
            DefaultValueFactory = _ => DefaultPort,
        };
        var address = new Option<string>("--address")
        {
            Description = "IP address to listen on. Defaults to loopback until accounts exist (M4).",
            DefaultValueFactory = _ => "127.0.0.1",
        };

        var maxAttachment = new Option<long>("--max-attachment-mb")
        {
            Description = "Maximum size of one attachment, in MiB.",
            DefaultValueFactory = _ => ErableHostOptions.DefaultMaxAttachmentSize / (1024 * 1024),
        };

        var serve = new Command("serve", "Serve projects over HTTP.") { folder, port, address, maxAttachment };
        serve.SetAction(async (parseResult, cancellationToken) =>
        {
            var options = new ErableHostOptions
            {
                Mode = HostMode.Server,
                DataDirectory = parseResult.GetRequiredValue(folder).FullName,
                Port = parseResult.GetValue(port),
                Address = parseResult.GetRequiredValue(address),
                MaxAttachmentSize = parseResult.GetValue(maxAttachment) * 1024 * 1024,
                TestMode = Environment.GetEnvironmentVariable("ERABLE_TEST_MODE") == "1",
                FixturesDirectory = Environment.GetEnvironmentVariable("ERABLE_TEST_FIXTURES"),
            };
            await using var app = ErableHost.Build(options);
            await app.StartAsync(cancellationToken).ConfigureAwait(false);
            await app.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        });

        var output = new Option<FileInfo>("--output")
        {
            Description = "Destination file.",
            Required = true,
        };
        var openApi = new Command("openapi", "Write the OpenAPI document (development tool).") { output };
        openApi.Hidden = true;
        openApi.SetAction((parseResult, cancellationToken) =>
            OpenApiExport.RunAsync(parseResult.GetRequiredValue(output), cancellationToken));

        var project = new Argument<DirectoryInfo>("folder") { Description = "Project folder (closed: the command takes its lock)." };
        var asJson = new Option<bool>("--json") { Description = "Machine-readable report." };
        var fix = new Option<bool>("--fix") { Description = "Safe repairs only: rebuild derived indexes, delete unreferenced files." };
        var doctor = new Command("doctor", "Check the integrity of a project.") { project, asJson, fix };
        doctor.SetAction(parseResult => DoctorCommand.Run(
            parseResult.GetRequiredValue(project).FullName, parseResult.GetValue(asJson), parseResult.GetValue(fix), parseResult.InvocationConfiguration.Output));

        var root = new RootCommand("Erable, a structured wiki.") { serve, doctor, openApi };
        return root.Parse(args).InvokeAsync();
    }
}
