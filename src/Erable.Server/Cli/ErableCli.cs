using System.CommandLine;
using System.Globalization;
using Erable.Core.Accounts;
using Erable.Server.Hosting;
using Erable.Storage;

namespace Erable.Server.Cli;

/// <summary>Command line of the <c>erable</c> executable.</summary>
public static class ErableCli
{
    public const int DefaultPort = ServeConfiguration.DefaultPort;

    public static Task<int> RunAsync(string[] args)
    {
        var folder = new Argument<DirectoryInfo>("folder")
        {
            Description = "A project folder to share as it is, or the data folder of a server (projects, accounts).",
        };
        var port = new Option<int?>("--port")
        {
            Description = $"TCP port to listen on (0 = random). Default {DefaultPort}, or ERABLE_PORT.",
        };
        var address = new Option<string?>("--address")
        {
            Description = "IP address to listen on. Default 127.0.0.1 (this machine only), or ERABLE_ADDRESS.",
        };
        var lan = new Option<bool>("--lan")
        {
            Description = "Listen on every network interface (--address 0.0.0.0) to share with the local network.",
        };
        var maxAttachment = new Option<long?>("--max-attachment-mb")
        {
            Description = "Maximum size of one attachment, in MiB (ERABLE_MAX_ATTACHMENT_MB).",
        };
        var quota = new Option<long?>("--project-quota-mb")
        {
            Description = "Room for the attachments of one project, in MiB; none by default (ERABLE_PROJECT_QUOTA_MB).",
        };

        var serve = new Command("serve", "Serve projects over HTTP, with accounts.") { folder, port, address, lan, maxAttachment, quota };
        serve.SetAction((parseResult, cancellationToken) =>
            ServeAsync(
                new ServeArguments(
                    parseResult.GetRequiredValue(folder).FullName,
                    parseResult.GetValue(port),
                    parseResult.GetValue(address),
                    parseResult.GetValue(lan),
                    parseResult.GetValue(maxAttachment),
                    parseResult.GetValue(quota)),
                Environment.GetEnvironmentVariable,
                Console.Out,
                Console.Error,
                cancellationToken));

        var healthPort = new Option<int?>("--port") { Description = "Port of the server (default: ERABLE_PORT or 8080)." };
        var health = new Command("healthcheck", "Exit with 0 when the local server answers (container health check).") { healthPort };
        health.SetAction((parseResult, cancellationToken) =>
            HealthCheck.RunAsync(
                parseResult.GetValue(healthPort)
                    ?? (int.TryParse(Environment.GetEnvironmentVariable("ERABLE_PORT"), NumberStyles.None, CultureInfo.InvariantCulture, out var fromEnv) ? fromEnv : 8080),
                cancellationToken));

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

        var root = new RootCommand("Erable, a structured wiki.") { serve, health, doctor, openApi };
        return root.Parse(args).InvokeAsync();
    }

    /// <summary>
    /// Starts the server (M4-06). The first start of a server without any account asks for the first administrator, or
    /// takes it from <c>ERABLE_ADMIN_LOGIN</c> / <c>ERABLE_ADMIN_PASSWORD</c>; without either it stops, since nobody could sign in.
    /// </summary>
    internal static async Task<int> ServeAsync(
        ServeArguments arguments, Func<string, string?> env, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        // The variables of the environment are what an empty value means « unset » for.
        string? Env(string name) => env(name) is { Length: > 0 } value ? value : null;
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var plan = ServeConfiguration.Resolve(arguments, Env, applicationData);
        if (!plan.IsSuccess)
        {
            await error.WriteLineAsync(plan.Error.Message).ConfigureAwait(false);
            return 2;
        }

        var options = plan.Value.Options;
        if (!Directory.Exists(options.DataDirectory))
        {
            Directory.CreateDirectory(options.DataDirectory!);
        }

        if (await EnsureAdministratorAsync(options, error, cancellationToken).ConfigureAwait(false) is not { } withAdmin)
        {
            return 2;
        }

        await using var app = ErableHost.Build(withAdmin);
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        var uri = ErableHost.GetListeningUri(app);
        await output.WriteLineAsync($"Erable serves {(plan.Value.SharedProject ? "the project " + options.SharedProjectFolder : "the data folder " + options.DataDirectory)}").ConfigureAwait(false);
        await output.WriteLineAsync($"Listening on {uri}").ConfigureAwait(false);
        if (options.Address is "127.0.0.1" or "::1")
        {
            await output.WriteLineAsync("Only this machine can reach it. Add --lan to share it with the local network.").ConfigureAwait(false);
        }

        await app.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
        return 0;
    }

    /// <summary>The options with the first administrator filled in; null (after telling why) when there is none to create.</summary>
    private static async Task<ErableHostOptions?> EnsureAdministratorAsync(ErableHostOptions options, TextWriter error, CancellationToken cancellationToken)
    {
        int accounts;
        using (var store = ServerStore.OpenOrCreate(options.DataDirectory!))
        {
            accounts = store.CountAccounts();
        }

        // Test mode creates its own users (spec/projects/users.json).
        if (accounts > 0 || options.TestMode || (!string.IsNullOrEmpty(options.BootstrapAdminLogin) && !string.IsNullOrEmpty(options.BootstrapAdminPassword)))
        {
            return options;
        }

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            await error.WriteLineAsync(
                "This server has no account yet. Set ERABLE_ADMIN_LOGIN and ERABLE_ADMIN_PASSWORD to create the first administrator.")
                .ConfigureAwait(false);
            return null;
        }

        var (login, password) = AdminPrompt.Ask(Console.Out);
        cancellationToken.ThrowIfCancellationRequested();
        return options with { BootstrapAdminLogin = login, BootstrapAdminPassword = password };
    }
}

/// <summary>Asks the first administrator on the terminal.</summary>
internal static class AdminPrompt
{
    public static (string Login, string Password) Ask(TextWriter output)
    {
        output.WriteLine("This server has no account yet: create its first administrator.");
        string login;
        while (true)
        {
            output.Write("Login: ");
            var typed = Console.ReadLine();
            if (AccountRules.NormalizeLogin(typed) is { IsSuccess: true } valid)
            {
                login = valid.Value;
                break;
            }

            output.WriteLine("A login is 2 to 32 characters: a-z, 0-9, '.', '_' or '-'.");
        }

        while (true)
        {
            var password = ReadSecret(output, "Password: ");
            if (AccountRules.ValidatePassword(password) is { } weak)
            {
                output.WriteLine(weak.Message);
                continue;
            }

            if (ReadSecret(output, "Again: ") == password)
            {
                return (login, password);
            }

            output.WriteLine("The two passwords differ.");
        }
    }

    private static string ReadSecret(TextWriter output, string prompt)
    {
        output.Write(prompt);
        var typed = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                output.WriteLine();
                return typed.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                typed.Append(key.KeyChar);
            }
        }
    }
}

/// <summary><c>erable healthcheck</c>: the container health check, a chiseled image having no shell nor curl.</summary>
public static class HealthCheck
{
    public static async Task<int> RunAsync(int port, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await http.GetAsync(new Uri($"http://127.0.0.1:{port}/api/info"), cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
