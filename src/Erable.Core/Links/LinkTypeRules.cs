namespace Erable.Core.Links;

/// <summary>Editable fields of a link type once normalized (F09.R1). Its name is the element name.</summary>
public sealed record LinkTypeFields(string Description, string? InverseName);

/// <summary>Rules of the fields of a link type (docs/architecture.md §5.5).</summary>
public static class LinkTypeRules
{
    public const int MaxDescription = 2_000;
    public const int MaxInverseName = 200;

    /// <summary>
    /// Trims the fields; a blank inverse label means none (incoming links then show the label of the type).
    /// </summary>
    public static Result<LinkTypeFields> Normalize(string? description, string? inverseName)
    {
        var text = description?.Trim() ?? string.Empty;
        if (text.Length > MaxDescription)
        {
            return new DomainError(ErrorCodes.LinkTypeDescriptionTooLong, $"The description exceeds {MaxDescription} characters.");
        }

        var inverse = inverseName?.Trim();
        if (string.IsNullOrEmpty(inverse))
        {
            return new LinkTypeFields(text, null);
        }

        return inverse.Length > MaxInverseName || inverse.Any(char.IsControl)
            ? new DomainError(ErrorCodes.LinkTypeInverseInvalid, "The inverse label is too long or holds control characters.")
            : new LinkTypeFields(text, inverse);
    }
}
