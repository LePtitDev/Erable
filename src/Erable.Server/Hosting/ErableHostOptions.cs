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
    public TimeSpan SessionLifetime { get; init; } = DefaultSessionLifetime;

    public static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromHours(12);

    /// <summary>
    /// Failed logins tolerated for one login name from one address within <see cref="LoginWindow"/> before that pair is
    /// blocked; from all addresses together, the number after which attempts on the login are slowed down.
    /// </summary>
    public int LoginMaxFailures { get; init; } = 5;

    /// <summary>First delay applied to a login that failed too often from all addresses; doubled at each failure.</summary>
    public TimeSpan LoginDelayStep { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Cap of the delay: the owner of a targeted login always gets in, at most this much later.</summary>
    public TimeSpan LoginMaxDelay { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Failed logins tolerated from one address within <see cref="LoginWindow"/>.</summary>
    public int LoginMaxFailuresPerAddress { get; init; } = 40;

    public TimeSpan LoginWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>How often the hub pings its clients.</summary>
    public TimeSpan HubKeepAlive { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A client silent for this long is considered gone: its presence disappears from the other screens (F14.R3,
    /// E2E-14-05: less than 30 seconds). Must be at least twice <see cref="HubKeepAlive"/>.
    /// </summary>
    public TimeSpan HubClientTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Forces the <c>Secure</c> flag of the cookie (TLS ends at a reverse proxy); otherwise set when the request is HTTPS.</summary>
    public bool CookieSecure { get; init; }

    /// <summary>First server administrator, created at startup when no account exists (<c>ERABLE_ADMIN_LOGIN</c> / <c>ERABLE_ADMIN_PASSWORD</c>).</summary>
    public string? BootstrapAdminLogin { get; init; }

    public string? BootstrapAdminPassword { get; init; }

    /// <summary>
    /// Room for the attachments of one project, in bytes; null for none (M4-06, docs/architecture.md §9). A server
    /// shared by several teams sets it so that one project cannot fill the disk.
    /// </summary>
    public long? ProjectQuota { get; init; }

    /// <summary>The address users type to reach the server (<c>https://wiki.example.org</c>): allowed by CORS, and <c>https</c> makes the cookie <c>Secure</c>.</summary>
    public string? PublicUrl { get; init; }

    /// <summary>Other origins allowed to call the API from a browser (a SPA hosted elsewhere), exact match.</summary>
    public IReadOnlyList<string> CorsOrigins { get; init; } = [];

    /// <summary>
    /// Pages served from a loopback address may call the API (the desktop application connected to a server, M4-07):
    /// they carry a bearer token, never a cookie.
    /// </summary>
    public bool AllowLoopbackOrigins { get; init; } = true;

    /// <summary>The server sits behind a reverse proxy (Caddy…): trust <c>X-Forwarded-For / -Proto / -Host</c>.</summary>
    public bool TrustProxy { get; init; }

    /// <summary>
    /// A project folder shared as it is (<c>erable serve ./MonWiki</c>): registered in the server at startup, its first
    /// administrator being the first server administrator.
    /// </summary>
    public string? SharedProjectFolder { get; init; }
}
