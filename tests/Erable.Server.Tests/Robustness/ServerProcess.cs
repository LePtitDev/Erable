using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Erable.Server.Tests.Robustness;

/// <summary>
/// The real server (<c>erable serve</c>) as a child process, which a test can kill without warning (docs/testing.md §6.1).
/// Accounts and projects live in <c>dataDirectory</c>, so a restart finds what the killed process left.
/// </summary>
internal sealed class ServerProcess : IAsyncDisposable
{
    public const string AdminLogin = "admin";
    public const string AdminPassword = "crash-test-2026";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    private readonly Process _process;

    private ServerProcess(Process process, Uri address, HttpClient http)
    {
        _process = process;
        Address = address;
        Http = http;
    }

    public Uri Address { get; }

    /// <summary>Signed in as the administrator (bearer token).</summary>
    public HttpClient Http { get; }

    public int ProcessId => _process.Id;

    public static async Task<ServerProcess> StartAsync(string dataDirectory, CancellationToken cancellationToken)
    {
        var entry = Path.Combine(AppContext.BaseDirectory, "erable.dll");
        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        info.ArgumentList.Add(entry);
        info.ArgumentList.Add("serve");
        info.ArgumentList.Add(dataDirectory);
        info.ArgumentList.Add("--port");
        info.ArgumentList.Add("0");
        info.Environment["ERABLE_ADMIN_LOGIN"] = AdminLogin;
        info.Environment["ERABLE_ADMIN_PASSWORD"] = AdminPassword;
        info.Environment["DOTNET_NOLOGO"] = "1";

        var process = new Process { StartInfo = info };
        var listening = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            output.Enqueue(e.Data);
            const string marker = "Listening on ";
            if (e.Data.StartsWith(marker, StringComparison.Ordinal) && Uri.TryCreate(e.Data[marker.Length..].Trim(), UriKind.Absolute, out var uri))
            {
                listening.TrySetResult(uri);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.Enqueue(e.Data);
            }
        };
        process.Exited += (_, _) => listening.TrySetException(new InvalidOperationException(
            "The server stopped before it listened:" + Environment.NewLine + string.Join(Environment.NewLine, output)));
        process.EnableRaisingEvents = true;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            var address = await listening.Task.WaitAsync(StartTimeout, cancellationToken);
            var http = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(30) };
            using var login = await http.PostAsJsonAsync(
                "/api/v1/auth/login", new { login = AdminLogin, password = AdminPassword, issueToken = true }, cancellationToken);
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<JsonObject>(cancellationToken))!["token"]!.GetValue<string>();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return new ServerProcess(process, address, http);
        }
        catch
        {
            Kill(process);
            process.Dispose();
            throw;
        }
    }

    /// <summary>Kills the process and its children at once: no shutdown, no flush, like a power cut for the process.</summary>
    public void Kill()
    {
        Kill(_process);
        Http.Dispose();
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }

    public ValueTask DisposeAsync()
    {
        Kill();
        _process.Dispose();
        return ValueTask.CompletedTask;
    }
}
