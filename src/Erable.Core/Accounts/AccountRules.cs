using System.Text.RegularExpressions;

namespace Erable.Core.Accounts;

/// <summary>Business rules of server accounts (M4-01): login names, display names and passwords.</summary>
public static partial class AccountRules
{
    public const int MinLoginLength = 2;
    public const int MaxLoginLength = 32;
    public const int MaxDisplayNameLength = 80;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 256;

    /// <summary>
    /// A login is a short, lowercase, URL-safe identifier: it is also the author recorded in projects
    /// (<c>createdBy</c>, <c>updatedBy</c>), so it never changes and never needs escaping.
    /// </summary>
    public static Result<string> NormalizeLogin(string? login)
    {
        var value = login?.Trim().ToLowerInvariant() ?? string.Empty;
        if (value.Length < MinLoginLength || value.Length > MaxLoginLength || !LoginPattern().IsMatch(value))
        {
            return new DomainError(ErrorCodes.AccountLoginInvalid, $"A login is {MinLoginLength} to {MaxLoginLength} characters: a-z, 0-9, '.', '_' or '-'.");
        }

        // The author of the desktop application cannot be impersonated by an account.
        return value == Projects.KnownUsers.Local
            ? new DomainError(ErrorCodes.AccountLoginInvalid, $"'{value}' is reserved.")
            : value;
    }

    public static Result<string> NormalizeDisplayName(string? name)
    {
        var value = name?.Trim() ?? string.Empty;
        return value.Length is 0 or > MaxDisplayNameLength || value.Any(char.IsControl)
            ? new DomainError(ErrorCodes.AccountNameInvalid, $"A display name is 1 to {MaxDisplayNameLength} characters, without control characters.")
            : value;
    }

    /// <summary>Length only (NIST SP 800-63B): no composition rules, no truncation.</summary>
    public static DomainError? ValidatePassword(string? password) =>
        password is { Length: >= MinPasswordLength and <= MaxPasswordLength }
            ? null
            : new DomainError(ErrorCodes.AccountPasswordInvalid, $"A password is {MinPasswordLength} to {MaxPasswordLength} characters.");

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]*$")]
    private static partial Regex LoginPattern();
}

/// <summary>A member of a project as the rules of the members see it.</summary>
/// <param name="Active">False for a disabled account: it cannot administer anything.</param>
public sealed record MemberRoleEntry(string UserId, ProjectRole Role, bool Active);

/// <summary>Rules on the members of a project (M4-08, F14.R1, decision Q5).</summary>
public static class MemberRules
{
    /// <summary>
    /// True when the change would take the last active administrator away from a project that has one: a project is never
    /// left that nobody can administer. <paramref name="newRole"/> is null to remove the member.
    /// </summary>
    public static bool LeavesNoAdministrator(IReadOnlyCollection<MemberRoleEntry> members, string userId, ProjectRole? newRole)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(userId);
        var before = members.Count(m => m is { Active: true, Role: ProjectRole.Admin });
        if (before == 0)
        {
            return false;
        }

        var after = members
            .Where(m => m.UserId != userId)
            .Count(m => m is { Active: true, Role: ProjectRole.Admin })
            + (newRole == ProjectRole.Admin && members.FirstOrDefault(m => m.UserId == userId) is not { Active: false } ? 1 : 0);
        return after == 0;
    }
}
