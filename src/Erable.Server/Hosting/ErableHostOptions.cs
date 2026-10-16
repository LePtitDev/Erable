namespace Erable.Server.Hosting;

/// <summary>Everything needed to start a backend instance. No static state: several hosts may run in one process.</summary>
public sealed record ErableHostOptions
{
    public required HostMode Mode { get; init; }

    /// <summary>IP address Kestrel listens on.</summary>
    public string Address { get; init; } = "127.0.0.1";

    /// <summary>TCP port; 0 picks a random free port (read it back with <see cref="ErableHost.GetListeningUri"/>).</summary>
    public int Port { get; init; }

    /// <summary>Root data folder (projects in server mode, application profile in desktop mode).</summary>
    public string? DataDirectory { get; init; }

    /// <summary>Folder containing the built SPA (<c>index.html</c>, <c>assets/</c>). Defaults to <c>wwwroot</c> next to the binary.</summary>
    public string? WebRootPath { get; init; }

    /// <summary>Desktop session token required on every request (docs/architecture.md §9).</summary>
    public string? SessionToken { get; init; }

    /// <summary>Enables the <c>/_test</c> endpoints. Ignored by builds compiled without them (Release).</summary>
    public bool TestMode { get; init; }

    /// <summary>
    /// <c>spec/projects</c> of the repository (test mode): reference projects copied by <c>POST /_test/reset</c> and
    /// test users. Null outside tests.
    /// </summary>
    public string? FixturesDirectory { get; init; }

    /// <summary>Desktop smoke test mode (<c>--self-test</c>): enables <c>/api/self-test/report</c> (docs/testing.md §10).</summary>
    public bool SelfTest { get; init; }

    /// <summary>ASP.NET Core environment name; null uses the usual environment variables.</summary>
    public string? EnvironmentName { get; init; }

    /// <summary>Maximum size of one attachment in bytes (F06.R5, docs/architecture.md §9).</summary>
    public long MaxAttachmentSize { get; init; } = DefaultMaxAttachmentSize;

    public const long DefaultMaxAttachmentSize = 256L * 1024 * 1024;

    /// <summary>Clock used by the whole backend; tests replace it.</summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <summary>
    /// Lets API callers create or open projects at a path of this machine (the desktop way). Always on in desktop mode;
    /// off in server mode, where projects live under <c>data/projects</c>; on in test mode, where the e2e tests drive the
    /// home screen of the local application against a server backend. Null picks that default.
    /// </summary>
    public bool? FolderProjects { get; init; }

    public bool FolderProjectsEnabled => FolderProjects ?? (Mode == HostMode.Desktop || TestMode);

    /// <summary>How long a login session lasts without activity (it is renewed while in use).</summary>
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromHours(12);

    /// <summary>Failed logins tolerated for one login name within <see cref="LoginWindow"/> before it is locked out.</summary>
    public int LoginMaxFailures { get; init; } = 5;

    /// <summary>Failed logins tolerated from one address within <see cref="LoginWindow"/>.</summary>
    public int LoginMaxFailuresPerAddress { get; init; } = 40;

    public TimeSpan LoginWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Forces the <c>Secure</c> flag of the cookie (TLS ends at a reverse proxy); otherwise set when the request is HTTPS.</summary>
    public bool CookieSecure { get; init; }

    /// <summary>First server administrator, created at startup when no account exists (<c>ERABLE_ADMIN_LOGIN</c> / <c>ERABLE_ADMIN_PASSWORD</c>).</summary>
    public string? BootstrapAdminLogin { get; init; }

    public string? BootstrapAdminPassword { get; init; }
}
