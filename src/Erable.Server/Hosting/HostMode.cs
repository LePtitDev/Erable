namespace Erable.Server.Hosting;

/// <summary>How the backend is assembled (docs/architecture.md §2).</summary>
public enum HostMode
{
    /// <summary>Hosted in-process by the desktop shell, loopback only.</summary>
    Desktop,

    /// <summary>Standalone server (<c>erable serve</c>, Docker).</summary>
    Server,
}
