using System.Globalization;
using System.Net;
using Erable.Core;
using Erable.Server.Hosting;
using Erable.Storage;

namespace Erable.Server.Cli;

/// <summary>What <c>erable serve</c> got on its command line (null: not given, the environment and the defaults decide).</summary>
public sealed record ServeArguments(
    string Folder, int? Port = null, string? Address = null, bool Lan = false, long? MaxAttachmentMb = null, long? ProjectQuotaMb = null);

/// <summary>The configuration of a server: the options of the host, and where its data lives.</summary>
/// <param name="Options">Host options, ready for <see cref="ErableHost.Build"/>.</param>
/// <param name="SharedProject">True when <c>folder</c> was a project, shared as it is.</param>
public sealed record ServePlan(ErableHostOptions Options, bool SharedProject);

/// <summary>
/// Turns the command line and the <c>ERABLE_*</c> environment variables into host options (docs/deployment.md).
/// The command line wins over the environment, which wins over the defaults. Every error names the setting at fault:
/// a misspelled value must stop the server, not silently run with the default.
/// </summary>
public static class ServeConfiguration
{
    public const string ConfigurationError = "serve.invalidConfiguration";

    public const int DefaultPort = 5080;

    /// <param name="env">Reads an environment variable (null when unset or empty).</param>
    /// <param name="applicationData">Where an application keeps its data (<c>%APPDATA%</c>); the accounts of a shared project folder go below it.</param>
    public static Result<ServePlan> Resolve(ServeArguments args, Func<string, string?> env, string applicationData)
    {
        try
        {
            return ResolveCore(args, env, applicationData);
        }
        catch (ConfigurationException ex)
        {
            return ex.Error;
        }
    }

    private static Result<ServePlan> ResolveCore(ServeArguments args, Func<string, string?> env, string applicationData)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(applicationData);

        var port = args.Port ?? ParseInt(env, "ERABLE_PORT", 0, 65535) ?? DefaultPort;
        if (port is < 0 or > 65535)
        {
            return Error("--port", "a port is 0 to 65535");
        }

        var address = args.Lan ? "0.0.0.0" : args.Address ?? env("ERABLE_ADDRESS") ?? "127.0.0.1";
        if (!IPAddress.TryParse(address, out _))
        {
            return Error(args.Address is null ? "ERABLE_ADDRESS" : "--address", $"'{address}' is not an IP address");
        }

        var maxAttachmentMb = args.MaxAttachmentMb ?? ParseInt(env, "ERABLE_MAX_ATTACHMENT_MB", 1, 1_048_576);
        var quotaMb = args.ProjectQuotaMb ?? ParseInt(env, "ERABLE_PROJECT_QUOTA_MB", 0, 100_000_000);
        var hours = ParseInt(env, "ERABLE_SESSION_HOURS", 1, 24 * 90);
        var trustProxy = ParseBool(env, "ERABLE_TRUST_PROXY");
        var cookieSecure = ParseBool(env, "ERABLE_COOKIE_SECURE");
        if (maxAttachmentMb is <= 0 or > 1_048_576)
        {
            return Error("--max-attachment-mb", "it is 1 to 1048576 MiB");
        }

        if (quotaMb is < 0)
        {
            return Error("--project-quota-mb", "it cannot be negative");
        }

        string? publicUrl = env("ERABLE_PUBLIC_URL");
        if (publicUrl is not null && !IsHttpUrl(publicUrl))
        {
            return Error("ERABLE_PUBLIC_URL", $"'{publicUrl}' is not an http(s) address");
        }

        var cors = (env("ERABLE_CORS_ORIGINS") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (cors.FirstOrDefault(origin => !IsHttpUrl(origin)) is { } bad)
        {
            return Error("ERABLE_CORS_ORIGINS", $"'{bad}' is not an http(s) origin");
        }

        // A folder that is a project is shared as it is; any other folder is the data folder of a multi-project server.
        var folder = Path.GetFullPath(args.Folder);
        var shared = ManifestFile.Exists(folder);
        string dataDirectory = folder;
        string? sharedFolder = null;
        if (shared)
        {
            var manifest = ManifestFile.Read(folder);
            if (!manifest.IsSuccess)
            {
                return manifest.Error;
            }

            // The accounts stay out of the project folder: it may be copied or shared with the people who must not see them.
            dataDirectory = Path.Combine(applicationData, "Erable", "serve", manifest.Value.ProjectId.ToString());
            sharedFolder = folder;
        }

        var options = new ErableHostOptions
        {
            Mode = HostMode.Server,
            DataDirectory = dataDirectory,
            SharedProjectFolder = sharedFolder,
            Port = port,
            Address = address,
            MaxAttachmentSize = (maxAttachmentMb ?? ErableHostOptions.DefaultMaxAttachmentSize / (1024 * 1024)) * 1024 * 1024,
            ProjectQuota = quotaMb is > 0 ? quotaMb * 1024 * 1024 : null,
            PublicUrl = publicUrl,
            CorsOrigins = cors,
            TrustProxy = trustProxy ?? false,
            CookieSecure = cookieSecure ?? publicUrl?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true,
            SessionLifetime = hours is { } h ? TimeSpan.FromHours(h) : ErableHostOptions.DefaultSessionLifetime,
            BootstrapAdminLogin = env("ERABLE_ADMIN_LOGIN"),
            BootstrapAdminPassword = env("ERABLE_ADMIN_PASSWORD"),
            TestMode = env("ERABLE_TEST_MODE") == "1",
            FixturesDirectory = env("ERABLE_TEST_FIXTURES"),
        };
        return new ServePlan(options, shared);
    }

    private static DomainError Error(string setting, string reason) =>
        new(ConfigurationError, $"Invalid {setting}: {reason}.");

    /// <summary>A whole number in <c>[min, max]</c>; null when the variable is unset; anything else stops the server.</summary>
    private static int? ParseInt(Func<string, string?> env, string name, int min, int max)
    {
        if (env(name) is not { } text)
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max
            ? value
            : throw new ConfigurationException(Error(name, $"'{text}' is not a whole number from {min} to {max}"));
    }

    private static bool? ParseBool(Func<string, string?> env, string name)
    {
        if (env(name) is not { } text)
        {
            return null;
        }

        return text.ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => throw new ConfigurationException(Error(name, $"'{text}' is neither true nor false")),
        };
    }

    private sealed class ConfigurationException(DomainError error) : Exception(error.Message)
    {
        public DomainError Error { get; } = error;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
