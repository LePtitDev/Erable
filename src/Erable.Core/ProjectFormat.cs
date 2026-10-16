namespace Erable.Core;

/// <summary>On-disk project format (docs/architecture.md §7.1).</summary>
public static class ProjectFormat
{
    /// <summary>Latest <c>formatVersion</c> this build can read and write.</summary>
    public const int CurrentVersion = 4;
}
