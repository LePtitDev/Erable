using System.Globalization;
using Erable.Core.Elements;
using Erable.Core.Properties;

namespace Erable.Core.Search;

/// <summary>Order of the results (F10.R4).</summary>
public enum SearchSort
{
    /// <summary>Best match first (bm25); by last modification when there is no text.</summary>
    Relevance,

    /// <summary>Most recently modified first.</summary>
    Modified,

    /// <summary>Alphabetical, ignoring case and accents.</summary>
    Name,
}

/// <summary>« Modifié » filter of the search screen: the last 24 hours, 7 days or 30 days.</summary>
public enum ModifiedWindow
{
    Day,
    Week,
    Month,
}

public static class ModifiedWindows
{
    public static TimeSpan DurationOf(ModifiedWindow window) => window switch
    {
        ModifiedWindow.Day => TimeSpan.FromHours(24),
        ModifiedWindow.Week => TimeSpan.FromDays(7),
        ModifiedWindow.Month => TimeSpan.FromDays(30),
        _ => throw new ArgumentOutOfRangeException(nameof(window), window, null),
    };
}

/// <summary>Comparison of a property condition (F10.R3). On a list, a condition holds when one item satisfies it.</summary>
public enum ConditionOp
{
    Equal,

    /// <summary>The page has a value and none of its items equals the operand.</summary>
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,

    /// <summary>Folded substring of a string, text or date…: strings and texts only.</summary>
    Contains,

    /// <summary>The page has an effective value.</summary>
    IsSet,

    /// <summary>The page has no effective value.</summary>
    IsNotSet,
}

/// <summary>An operand in the typed columns of a value (<c>v_int</c>, <c>v_real</c>, <c>v_text</c>, <c>v_ref</c>).</summary>
public sealed record ConditionOperand(long? Whole, double? Real, string? Text, ElementId? Ref);

/// <summary>A condition on a property, inherited values included. The operand is parsed against the type of the property.</summary>
public sealed record PropertyCondition(ElementId PropertyId, ScalarType Scalar, ConditionOp Op, ConditionOperand? Operand)
{
    /// <summary>
    /// Builds a condition from the text of the URL (<c>niveau &gt; 50</c>): refused when the operator does not exist for
    /// the type or the operand is not a value of that type.
    /// </summary>
    public static Result<PropertyCondition> Create(ElementId propertyId, ScalarType scalar, ConditionOp op, string? raw)
    {
        if (op is ConditionOp.IsSet or ConditionOp.IsNotSet)
        {
            return new PropertyCondition(propertyId, scalar, op, null);
        }

        if (!OperatorAllowed(scalar, op))
        {
            return Invalid($"The operator {op} does not apply to a {PropertyType.Name(scalar)} property.");
        }

        var operand = Parse(scalar, raw ?? string.Empty);
        return operand is null
            ? Invalid($"'{raw}' is not a {PropertyType.Name(scalar)}.")
            : new PropertyCondition(propertyId, scalar, op, operand);
    }

    public static bool OperatorAllowed(ScalarType scalar, ConditionOp op) => op switch
    {
        ConditionOp.Equal or ConditionOp.NotEqual or ConditionOp.IsSet or ConditionOp.IsNotSet => true,
        ConditionOp.Less or ConditionOp.LessOrEqual or ConditionOp.Greater or ConditionOp.GreaterOrEqual =>
            scalar is ScalarType.Int or ScalarType.Float or ScalarType.Date,
        ConditionOp.Contains => scalar is ScalarType.String or ScalarType.Text,
        _ => false,
    };

    private static ConditionOperand? Parse(ScalarType scalar, string raw)
    {
        var text = raw.Trim();
        switch (scalar)
        {
            case ScalarType.Bool:
                return text.ToUpperInvariant() switch
                {
                    "TRUE" => new ConditionOperand(1, null, null, null),
                    "FALSE" => new ConditionOperand(0, null, null, null),
                    _ => null,
                };
            case ScalarType.Int:
                return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var i)
                    ? new ConditionOperand(i, null, null, null)
                    : null;
            case ScalarType.Float:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d)
                    ? new ConditionOperand(null, d, null, null)
                    : null;
            case ScalarType.Date:
                return PropertyValues.TryParseDate(text, out var date)
                    ? new ConditionOperand(null, null, PropertyValues.FormatDate(date), null)
                    : null;
            case ScalarType.Ref:
                return ElementId.TryParse(text, out var id) ? new ConditionOperand(null, null, null, id) : null;
            case ScalarType.String:
            case ScalarType.Text:
                return raw.Length == 0 ? null : new ConditionOperand(null, null, raw, null);
            case ScalarType.Enum:
                return text.Length == 0 ? null : new ConditionOperand(null, null, text, null);
            default:
                return null;
        }
    }

    private static DomainError Invalid(string message) => new(ErrorCodes.SearchConditionInvalid, message);
}

/// <summary>
/// What narrows a search; every part applies together (AND, F10.R3). Kinds are OR-ed with each other, so are the
/// categories. Conditions and categories only concern pages.
/// </summary>
public sealed record SearchFilter(
    IReadOnlySet<ElementKind> Kinds,
    IReadOnlyList<ElementId> Categories,
    bool IncludeSubCategories,
    IReadOnlyList<PropertyCondition> Conditions,
    DateTimeOffset? ModifiedSince,
    bool OutOfRules)
{
    public static SearchFilter None { get; } = new(new HashSet<ElementKind>(), [], false, [], null, false);
}

/// <summary>A search: text (cut by <see cref="FullTextExpression"/>), filters, order and page.</summary>
public sealed record SearchRequest(
    string? Text,
    bool NameOnly,
    SearchFilter Filter,
    SearchSort Sort,
    int Offset,
    int Limit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
}
