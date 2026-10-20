using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Erable.Server.Hosting;

namespace Erable.Server.Native;

/// <summary>A running desktop instance, as advertised in the application profile.</summary>
/// <param name="ProcessId">Its process.</param>
/// <param name="Port">Its loopback port.</param>
/// <param name="IpcToken">Secret that only lets another instance ask it to come to the front.</param>
public sealed record InstanceInfo(int ProcessId, int Port, string IpcToken);

/// <summary>Body of <c>POST /ipc/activate</c>.</summary>
/// <param name="Folder">Folder of the project to show; null brings the window to the front whatever it shows.</param>
public sealed record ActivateRequest(string? Folder);

/// <summary>
/// Single instance per project (F16.R2). Each desktop instance writes <c>instances/&lt;pid&gt;.json</c>
/// in the application profile (port and IPC token) while it runs. An instance that finds a project locked asks the
/// other instances, through <c>POST /ipc/activate</c> on their loopback port, which one has it: that one comes to the
/// front. The IPC token grants nothing else (it is not the session token).
/// </summary>
public sealed class InstanceRegistry : IDisposable
{
    public const string IpcHeader = "X-Erable-Ipc";
    public const string ActivatePath = "/ipc/activate";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(3);

    private readonly string _directory;
    private readonly byte[] _token = Encoding.ASCII.GetBytes(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    public InstanceRegistry(ErableHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _directory = DirectoryOf(ErableHost.ProfileDirectory(options));
    }

    private string? _file;

    /// <summary>Advertises this instance, once its port is known.</summary>
    public void Register(int port)
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, $"{Environment.ProcessId}-{port}.json");
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new InstanceInfo(Environment.ProcessId, port, Encoding.ASCII.GetString(_token)), Json));
        File.Move(temporary, file, overwrite: true);
        _file = file;
    }

    /// <summary>Also on disposal: a host disposed without being stopped must not leave its file behind.</summary>
    public void Dispose() => Unregister();

    public void Unregister()
    {
        if (_file is null)
        {
            return;
        }

        try
        {
            File.Delete(_file);
        }
        catch (IOException)
        {
            // A leftover file is harmless: its process is gone, the next lookup removes it.
        }
    }

    /// <summary>Constant-time check of the token of an IPC request.</summary>
    public bool IsValidToken(string? candidate) =>
        !string.IsNullOrEmpty(candidate) && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), _token);

    /// <summary>
    /// Asks the running instances to come to the front if they have <paramref name="folder"/> open (any of them when
    /// null). True when one did. <paramref name="allowForeground"/> runs first with its process id (Windows). The
    /// caller may be one of them: it answers 404 for a folder it does not have open, like any other.
    /// </summary>
    public static async Task<bool> ActivateOwnerAsync(
        string profileDirectory, string? folder, Action<int>? allowForeground, CancellationToken cancellationToken)
    {
        var directory = DirectoryOf(profileDirectory);
        if (!Directory.Exists(directory))
        {
            return false;
        }

        using var http = new HttpClient { Timeout = CallTimeout };
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            if (Read(file) is not { } instance)
            {
                continue;
            }

            if (!IsRunning(instance.ProcessId))
            {
                TryDelete(file);
                continue;
            }

            allowForeground?.Invoke(instance.ProcessId);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"http://{ErableHost.DesktopAddress}:{instance.Port}{ActivatePath}"))
            {
                Content = JsonContent.Create(new ActivateRequest(folder), options: Json),
            };
            request.Headers.Add(IpcHeader, instance.IpcToken);
            try
            {
                using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening (yet, or any more): ask the next one.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timed out: ask the next one.
            }
        }

        return false;
    }

    private static string DirectoryOf(string profileDirectory) => Path.Combine(profileDirectory, "instances");

    private static InstanceInfo? Read(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<InstanceInfo>(File.ReadAllText(file), Json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
            // Another instance cleans it up at the same time.
        }
    }
}
