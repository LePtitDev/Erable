namespace Erable.Core.Elements;

/// <summary>Anything shown in the explorer, plus attachments (docs/architecture.md §5.1).</summary>
/// <param name="Id">Stable identifier; never changes on rename or move (F02.R4).</param>
/// <param name="Kind">Type of element.</param>
/// <param name="ParentId"><see cref="ElementId.None"/> for roots only.</param>
/// <param name="Name">Display name; siblings may share it (F02.R6).</param>
/// <param name="SortOrder">Position among siblings (see <see cref="Tree.SortOrder"/>).</param>
/// <param name="Revision">Incremented on every saved change of the element.</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
/// <param name="CreatedBy">Author of the creation.</param>
/// <param name="UpdatedAt">Last modification time (UTC).</param>
/// <param name="UpdatedBy">Author of the last modification.</param>
/// <param name="DeletedAt">Set when the element is in the trash (M2-08).</param>
public sealed record Element(
    ElementId Id,
    ElementKind Kind,
    ElementId ParentId,
    string Name,
    long SortOrder,
    long Revision,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    DateTimeOffset? DeletedAt);

/// <summary>Validation of element names.</summary>
public static class ElementName
{
    public const int MaxLength = 200;

    /// <summary>Trims the name and checks it: not empty, at most <see cref="MaxLength"/> characters, no control characters.</summary>
    public static Result<string> Normalize(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return new DomainError(ErrorCodes.NameEmpty, "The name is empty.");
        }

        if (trimmed.Length > MaxLength)
        {
            return new DomainError(ErrorCodes.NameTooLong, $"The name exceeds {MaxLength} characters.");
        }

        if (trimmed.Any(char.IsControl))
        {
            return new DomainError(ErrorCodes.NameInvalid, "The name contains control characters.");
        }

        return trimmed;
    }

    /// <summary>Two sibling names are considered duplicates when they are equal ignoring case (F02.R6).</summary>
    public static bool AreDuplicates(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
