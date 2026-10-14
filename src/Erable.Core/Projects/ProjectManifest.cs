namespace Erable.Core.Projects;

/// <summary>Content of <c>erable.json</c>, the file that makes a folder an Erable project (F01.R1, docs/architecture.md §7.1).</summary>
/// <param name="FormatVersion">Version of the on-disk format; equals the database schema version.</param>
/// <param name="ProjectId">Stable project identifier, same format as element IDs (decision P-08).</param>
/// <param name="Name">Display name.</param>
/// <param name="ContentLanguage">Language of the content, fixed at creation (F01.R4, decision Q1).</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
public sealed record ProjectManifest(int FormatVersion, ElementId ProjectId, string Name, string ContentLanguage, DateTimeOffset CreatedAt);

/// <summary>Who did something. Desktop mode has a single local user (accounts arrive with M4).</summary>
public static class KnownUsers
{
    /// <summary>The user of the desktop application; the UI shows it as « moi ».</summary>
    public const string Local = "local";
}
