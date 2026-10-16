namespace Erable.Core.Accounts;

/// <summary>
/// What a member may do in one project (decision Q5: rights are per project only, never per folder or page).
/// Ordered by power: each role includes the ones below it.
/// </summary>
public enum ProjectRole
{
    /// <summary>Reads everything, changes nothing (F14.R1).</summary>
    Reader = 1,

    /// <summary>Reads and writes the content and the structure.</summary>
    Editor = 2,

    /// <summary>Editor, plus the members of the project.</summary>
    Admin = 3,
}

/// <summary>Reading and checking roles.</summary>
public static class ProjectRoles
{
    /// <summary>True when <paramref name="actual"/> is at least as powerful as <paramref name="required"/>.</summary>
    public static bool Allows(ProjectRole actual, ProjectRole required) => actual >= required;

    /// <summary>The JSON / storage name of a role: <c>reader</c>, <c>editor</c> or <c>admin</c>.</summary>
    public static string NameOf(ProjectRole role) => role switch
    {
        ProjectRole.Reader => "reader",
        ProjectRole.Editor => "editor",
        ProjectRole.Admin => "admin",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role."),
    };

    public static bool TryParse(string? name, out ProjectRole role)
    {
        switch (name)
        {
            case "reader":
                role = ProjectRole.Reader;
                return true;
            case "editor":
                role = ProjectRole.Editor;
                return true;
            case "admin":
                role = ProjectRole.Admin;
                return true;
            default:
                role = default;
                return false;
        }
    }
}
