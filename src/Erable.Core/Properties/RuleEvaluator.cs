using System.Text;
using Erable.Core.Elements;

namespace Erable.Core.Properties;

/// <summary>
/// Data the <c>kind</c> and <c>in_category</c> rules need (« Points d'attention »): the evaluator never reads
/// the storage itself.
/// </summary>
public interface IRuleContext
{
    /// <summary>Kind of a live element; null when it is unknown or deleted.</summary>
    ElementKind? KindOf(ElementId id);

    /// <summary>True when the page has the category, or one of its sub-categories.</summary>
    bool IsInCategory(ElementId pageId, ElementId categoryId);
}

/// <summary>A context that knows nothing: every reference is unknown.</summary>
public sealed class EmptyRuleContext : IRuleContext
{
    public static readonly EmptyRuleContext Instance = new();

    public ElementKind? KindOf(ElementId id) => null;

    public bool IsInCategory(ElementId pageId, ElementId categoryId) => false;
}

/// <summary>
/// A rule a value does not satisfy. The interface builds the message from the rule (key
/// <c>validation.rule.&lt;op&gt;</c> with the rule operand as parameters, « Doit être inférieur ou égal à 60 »).
/// </summary>
/// <param name="Rule">The rule failed.</param>
/// <param name="Index">For an item rule of a list, the index of the failing item.</param>
public sealed record RuleViolation(ValidationRule Rule, int? Index = null);

/// <summary>
/// Evaluates rules (docs/architecture.md §5.4). Violations come in rule order; for an item rule of a list, in item
/// order. Same algorithm as <c>src/web/src/shared/properties/rules.ts</c>, checked by <c>spec/fixtures/rules</c>.
/// </summary>
public static class RuleEvaluator
{
    /// <summary>White space for <c>not_empty</c>: the ECMAScript WhiteSpace and LineTerminator sets, identical on both sides.</summary>
    private const string WhiteSpace =
        "\u0009\u000A\u000B\u000C\u000D\u0020\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF";

    public static IReadOnlyList<RuleViolation> Evaluate(IReadOnlyList<ValidationRule> rules, PropertyValue value, IRuleContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(value);
        var ctx = context ?? EmptyRuleContext.Instance;
        var violations = new List<RuleViolation>();
        foreach (var rule in rules)
        {
            if (value is ListValue list)
            {
                if (rule.IsCountRule)
                {
                    if (!Satisfies(rule, list.Items.Length))
                    {
                        violations.Add(new RuleViolation(rule));
                    }
                }
                else
                {
                    for (var i = 0; i < list.Items.Length; i++)
                    {
                        if (!Satisfies(rule, list.Items[i], ctx))
                        {
                            violations.Add(new RuleViolation(rule, i));
                        }
                    }
                }
            }
            else if (!rule.IsCountRule && !Satisfies(rule, value, ctx))
            {
                violations.Add(new RuleViolation(rule));
            }
        }

        return violations;
    }

    /// <summary>Length as Unicode code points (a lone surrogate counts as one), the same on both sides.</summary>
    public static int CodePointLength(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }

            count++;
        }

        return count;
    }

    private static bool Satisfies(ValidationRule rule, int count) => rule switch
    {
        LengthRule { Op: RuleOp.MinCount } min => count >= min.Value,
        LengthRule { Op: RuleOp.MaxCount } max => count <= max.Value,
        _ => true,
    };

    /// <summary>A rule that does not apply to the kind of value (stale rule after a type change) is satisfied.</summary>
    private static bool Satisfies(ValidationRule rule, PropertyValue value, IRuleContext context)
    {
        switch (rule)
        {
            case NumberRule number when Number(value) is { } n:
                return number.Op switch
                {
                    RuleOp.GreaterThan => n > number.Value,
                    RuleOp.GreaterOrEqual => n >= number.Value,
                    RuleOp.LessThan => n < number.Value,
                    RuleOp.LessOrEqual => n <= number.Value,
                    RuleOp.NotEqual => n != number.Value,
                    _ => true,
                };
            case BetweenRule between when Number(value) is { } n:
                return n >= between.Min && n <= between.Max;
            case LengthRule { Op: RuleOp.MinLength } min when value is TextValue text:
                return CodePointLength(text.Value) >= min.Value;
            case LengthRule { Op: RuleOp.MaxLength } max when value is TextValue text:
                return CodePointLength(text.Value) <= max.Value;
            case MatchRule match when value is TextValue text:
                return match.Pattern.IsMatch(text.Value);
            case NotEmptyRule when value is TextValue text:
                return text.Value.Any(c => !WhiteSpace.Contains(c, StringComparison.Ordinal));
            case InRule allowed when value is EnumValue option:
                return allowed.Keys.Contains(option.Key);
            case DateRule { Op: RuleOp.After } after when value is DateValue date:
                return date.Value > after.Value;
            case DateRule { Op: RuleOp.Before } before when value is DateValue date:
                return date.Value < before.Value;
            case KindRule kind when value is RefValue reference:
                return context.KindOf(reference.Id) == (kind.Kind == RefKind.Page ? ElementKind.Page : ElementKind.Category);
            case InCategoryRule category when value is RefValue reference:
                return context.IsInCategory(reference.Id, category.CategoryId);
            default:
                return true;
        }
    }

    private static double? Number(PropertyValue value) => value switch
    {
        IntValue i => i.Value,
        FloatValue f => f.Value,
        _ => null,
    };
}

/// <summary>Whether existing values survive a type change (F08.R6).</summary>
public static class ValueConversion
{
    /// <summary>
    /// Converts a value to another type. <c>Success(null)</c> means the value disappears (an empty list converted to
    /// a scalar); a failure means the value is not convertible and blocks the change. Supported: identity, int ↔ float
    /// (integral floats only), anything → string/text (its text form; enum: its label), string/text → any scalar it
    /// parses as (enum: by key or label, ignoring case), scalar → list of one, list of at most one → scalar, list →
    /// list item by item.
    /// </summary>
    public static Result<PropertyValue?> Convert(
        PropertyValue value, PropertyType from, IReadOnlyList<EnumOption> fromOptions, PropertyType to, IReadOnlyList<EnumOption> toOptions)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (value is ListValue list)
        {
            if (to.IsList)
            {
                var items = new List<PropertyValue>();
                foreach (var item in list.Items)
                {
                    var converted = ConvertScalar(item, from.Scalar, fromOptions, to.Scalar, toOptions);
                    if (converted is null)
                    {
                        return NotConvertible(item, to);
                    }

                    items.Add(converted);
                }

                return new ListValue([.. items]);
            }

            return list.Items.Length switch
            {
                0 => Result.Success<PropertyValue?>(null),
                1 => ConvertScalar(list.Items[0], from.Scalar, fromOptions, to.Scalar, toOptions) is { } single
                    ? single
                    : NotConvertible(list.Items[0], to),
                _ => NotConvertible(value, to),
            };
        }

        var scalar = ConvertScalar(value, from.Scalar, fromOptions, to.Scalar, toOptions);
        if (scalar is null)
        {
            return NotConvertible(value, to);
        }

        return to.IsList ? new ListValue([scalar]) : scalar;
    }

    private static PropertyValue? ConvertScalar(
        PropertyValue value, ScalarType from, IReadOnlyList<EnumOption> fromOptions, ScalarType to, IReadOnlyList<EnumOption> toOptions)
    {
        if (from == to && to != ScalarType.Enum)
        {
            return value;
        }

        switch (to)
        {
            case ScalarType.String or ScalarType.Text:
                var text = TextOf(value, fromOptions);
                return to == ScalarType.String && (text.Length > PropertyValues.MaxStringLength || text.Any(c => char.IsControl(c) && c != '\t'))
                    ? null
                    : new TextValue(text.Normalize(NormalizationForm.FormC));
            case ScalarType.Int:
                return value switch
                {
                    FloatValue f when Math.Floor(f.Value) == f.Value && Math.Abs(f.Value) <= PropertyValues.MaxSafeInteger => new IntValue((long)f.Value),
                    TextValue t when long.TryParse(t.Value.Trim(), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var i)
                        && Math.Abs(i) <= PropertyValues.MaxSafeInteger => new IntValue(i),
                    _ => null,
                };
            case ScalarType.Float:
                return value switch
                {
                    IntValue i => new FloatValue(i.Value),
                    TextValue t when double.TryParse(t.Value.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var real)
                        && double.IsFinite(real) => new FloatValue(real),
                    _ => null,
                };
            case ScalarType.Bool:
                return value is TextValue b && bool.TryParse(b.Value.Trim(), out var flag) ? new BoolValue(flag) : null;
            case ScalarType.Date:
                return value is TextValue d && PropertyValues.TryParseDate(d.Value.Trim(), out var date) ? new DateValue(date) : null;
            case ScalarType.Ref:
                return value is TextValue r && ElementId.TryParse(r.Value.Trim(), out var id) ? new RefValue(id) : null;
            case ScalarType.Enum:
                var label = value is EnumValue e ? fromOptions.FirstOrDefault(o => o.Key == e.Key)?.Label ?? e.Key : value is TextValue s ? s.Value.Trim() : null;
                var key = value is EnumValue k ? k.Key : null;
                var option = toOptions.FirstOrDefault(o => o.Key == key)
                    ?? toOptions.FirstOrDefault(o => string.Equals(o.Key, label, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(o.Label, label, StringComparison.OrdinalIgnoreCase));
                return label is null || option is null ? null : new EnumValue(option.Key);
            default:
                return null;
        }
    }

    /// <summary>The text form of a scalar: invariant numbers, ISO dates, canonical IDs, option labels.</summary>
    public static string TextOf(PropertyValue value, IReadOnlyList<EnumOption> options) => value switch
    {
        BoolValue b => b.Value ? "true" : "false",
        IntValue i => i.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FloatValue f => f.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        TextValue t => t.Value,
        EnumValue e => options.FirstOrDefault(o => o.Key == e.Key)?.Label ?? e.Key,
        DateValue d => PropertyValues.FormatDate(d.Value),
        RefValue r => r.Id.ToString(),
        ListValue l => string.Join(", ", l.Items.Select(item => TextOf(item, options))),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static DomainError NotConvertible(PropertyValue value, PropertyType to) =>
        new(ErrorCodes.PropertyValueNotConvertible, $"{PropertyValues.ToJsonString(value)} cannot be converted to {to}.");
}
