using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace Erable.Server.Servers;

/// <summary>
/// Where the desktop application keeps the sign-in token of a server (docs/architecture.md §9): never in
/// clear text on disk, never in the web page. Each platform has its own vault; where none is available the token is
/// kept in memory only and the user signs in again at the next launch.
/// </summary>
public interface ISecretStore
{
    /// <summary>True when a secret survives the end of the application.</summary>
    bool IsPersistent { get; }

    string? Read(string key);

    /// <summary>Stores a secret; false when the vault refused it (the caller keeps it in memory instead).</summary>
    bool Write(string key, string secret);

    void Delete(string key);
}

/// <summary>Keeps secrets for the life of the process: the fallback, and the store of the tests.</summary>
public sealed class MemorySecretStore : ISecretStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public bool IsPersistent => false;

    public string? Read(string key) => _secrets.GetValueOrDefault(key);

    public bool Write(string key, string secret)
    {
        _secrets[key] = secret;
        return true;
    }

    public void Delete(string key) => _secrets.TryRemove(key, out _);
}

/// <summary>
/// Windows: DPAPI (<c>ProtectedData</c>, current user scope). The ciphertext only opens for this Windows account on
/// this machine; it is kept in a file of the application profile and bound to its key by an entropy.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretStore(string directory) : ISecretStore
{
    public bool IsPersistent => true;

    public string? Read(string key)
    {
        var path = PathOf(key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy(key), DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException)
        {
            // Another account, another machine, a damaged file: as if there were no secret.
            return null;
        }
    }

    public bool Write(string key, string secret)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy(key), DataProtectionScope.CurrentUser);
            var path = PathOf(key);
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, protectedBytes);
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Delete(string key)
    {
        try
        {
            File.Delete(PathOf(key));
        }
        catch (IOException)
        {
            // Nothing more to do: the secret is unreadable without this account anyway.
        }
    }

    private string PathOf(string key) => Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32] + ".bin");

    private static byte[] Entropy(string key) => Encoding.UTF8.GetBytes("erable:secret:" + key);
}

/// <summary>
/// macOS (<c>security</c>, the Keychain) and Linux (<c>secret-tool</c>, the Secret Service of libsecret). The secret
/// travels on the standard input of the tool, never on its command line, which other users can read. Keys and tokens
/// are made of letters, digits, <c>-</c> and <c>_</c> only: nothing needs quoting.
/// </summary>
public sealed class CommandSecretStore : ISecretStore
{
    private const string Service = "Erable";

    private readonly bool _mac;

    private CommandSecretStore(bool mac)
    {
        _mac = mac;
    }

    public bool IsPersistent => true;

    /// <summary>The store of this system, or null when its tool is missing (no keyring, minimal container…).</summary>
    public static CommandSecretStore? ForThisSystem()
    {
        if (OperatingSystem.IsMacOS() && File.Exists("/usr/bin/security"))
        {
            return new CommandSecretStore(mac: true);
        }

        if (OperatingSystem.IsLinux() && Run("secret-tool", ["--version"], stdin: null).ExitCode == 0)
        {
            return new CommandSecretStore(mac: false);
        }

        return null;
    }

    public string? Read(string key)
    {
        var result = _mac
            ? Run("/usr/bin/security", ["find-generic-password", "-a", key, "-s", Service, "-w"], stdin: null)
            : Run("secret-tool", ["lookup", "service", Service, "account", key], stdin: null);
        return result.ExitCode == 0 && result.Output.Trim() is { Length: > 0 } secret ? secret : null;
    }

    public bool Write(string key, string secret)
    {
        if (!IsPlain(key) || !IsPlain(secret))
        {
            return false;
        }

        if (_mac)
        {
            // « security -i » reads its commands from the standard input.
            return Run("/usr/bin/security", ["-i"], $"add-generic-password -U -a {key} -s {Service} -w {secret}\n").ExitCode == 0;
        }

        return Run("secret-tool", ["store", "--label=Erable", "service", Service, "account", key], secret).ExitCode == 0;
    }

    public void Delete(string key)
    {
        if (_mac)
        {
            Run("/usr/bin/security", ["delete-generic-password", "-a", key, "-s", Service], stdin: null);
        }
        else
        {
            Run("secret-tool", ["clear", "service", Service, "account", key], stdin: null);
        }
    }

    private static bool IsPlain(string value) => value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static (int ExitCode, string Output) Run(string tool, string[] arguments, string? stdin)
    {
        var start = new ProcessStartInfo(tool)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return (-1, string.Empty);
            }

            if (stdin is not null)
            {
                process.StandardInput.Write(stdin);
            }

            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill();
                return (-1, string.Empty);
            }

            return (process.ExitCode, output);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (-1, string.Empty);
        }
    }
}

/// <summary>Chooses the vault of the platform; the memory store when there is none.</summary>
public static class SecretStores
{
    public static ISecretStore Create(string profileDirectory)
    {
        ArgumentNullException.ThrowIfNull(profileDirectory);
        if (OperatingSystem.IsWindows())
        {
            return new DpapiSecretStore(Path.Combine(profileDirectory, "secrets"));
        }

        return CommandSecretStore.ForThisSystem() as ISecretStore ?? new MemorySecretStore();
    }
}
