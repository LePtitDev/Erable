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
}
