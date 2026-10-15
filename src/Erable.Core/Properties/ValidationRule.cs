using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Erable.Core.Properties;

/// <summary>Validation operators (docs/architecture.md §5.4); the JSON name is the snake_case form (<c>less_or_equal</c>).</summary>
public enum RuleOp
{
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Between,
    NotEqual,
    MinLength,
    MaxLength,
    Match,
    NotEmpty,
    In,
    After,
    Before,
    Kind,
    InCategory,
    MinCount,
    MaxCount,
}

/// <summary>What a <c>kind</c> rule requires a reference to point to.</summary>
public enum RefKind
{
    Page,
    Category,
}

/// <summary>
/// A validation rule, as data: <c>{ "op": "less_or_equal", "value": 60 }</c> (docs/architecture.md §5.4). Rules only
/// warn, they never block a save (§5.4, F08.R3). Evaluated identically by Core (authority) and by the SPA.
/// </summary>
public abstract record ValidationRule(RuleOp Op)
{
    /// <summary>Applies to the list itself (<c>min_count</c>, <c>max_count</c>) rather than to each item.</summary>
    public bool IsCountRule => Op is RuleOp.MinCount or RuleOp.MaxCount;
}

/// <summary><c>greater_than</c>, <c>greater_or_equal</c>, <c>less_than</c>, <c>less_or_equal</c>, <c>not_equal</c>.</summary>
public sealed record NumberRule(RuleOp Op, double Value) : ValidationRule(Op);

/// <summary>Inclusive bounds.</summary>
public sealed record BetweenRule(double Min, double Max) : ValidationRule(RuleOp.Between);

/// <summary><c>min_length</c>, <c>max_length</c> (Unicode code points), <c>min_count</c>, <c>max_count</c>.</summary>
public sealed record LengthRule(RuleOp Op, int Value) : ValidationRule(Op);

/// <summary>A regular expression of the safe subset (<see cref="SafeRegex"/>), searched anywhere in the value.</summary>
public sealed record MatchRule(SafeRegex Pattern) : ValidationRule(RuleOp.Match);

/// <summary>At least one character that is not white space.</summary>
public sealed record NotEmptyRule() : ValidationRule(RuleOp.NotEmpty);

/// <summary>Allowed subset of the options of an enum.</summary>
public sealed record InRule(ImmutableArray<string> Keys) : ValidationRule(RuleOp.In)
{
    public bool Equals(InRule? other) => other is not null && Keys.SequenceEqual(other.Keys);

    public override int GetHashCode() => Keys.Aggregate(0, (h, k) => HashCode.Combine(h, k));
}

/// <summary><c>after</c>, <c>before</c>: strictly after or before a date.</summary>
public sealed record DateRule(RuleOp Op, DateOnly Value) : ValidationRule(Op);

/// <summary>The referenced element is a page, or a category.</summary>
public sealed record KindRule(RefKind Kind) : ValidationRule(RuleOp.Kind);

/// <summary>The referenced page belongs to the category or one of its sub-categories.</summary>
public sealed record InCategoryRule(ElementId CategoryId) : ValidationRule(RuleOp.InCategory);

/// <summary>Which rules each type offers (F08.R2), and the JSON form of rules.</summary>
public static class ValidationRules
{
    private static readonly RuleOp[] NumberOps =
        [RuleOp.GreaterThan, RuleOp.GreaterOrEqual, RuleOp.LessThan, RuleOp.LessOrEqual, RuleOp.Between, RuleOp.NotEqual];

    /// <summary>Operators available for a type, in the order the interface proposes them (none for <c>bool</c>).</summary>
    public static IReadOnlyList<RuleOp> Available(PropertyType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        IReadOnlyList<RuleOp> item = type.Scalar switch
        {
            ScalarType.Bool => [],
            ScalarType.Int or ScalarType.Float => NumberOps,
            ScalarType.String => [RuleOp.MinLength, RuleOp.MaxLength, RuleOp.Match, RuleOp.NotEmpty],
            ScalarType.Text => [RuleOp.MaxLength],
            ScalarType.Enum => [RuleOp.In],
            ScalarType.Date => [RuleOp.After, RuleOp.Before],
            ScalarType.Ref => [RuleOp.Kind, RuleOp.InCategory],
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
        return type.IsList ? [RuleOp.MinCount, RuleOp.MaxCount, .. item] : item;
    }

    public static string OpName(RuleOp op) => op switch
    {
        RuleOp.GreaterThan => "greater_than",
        RuleOp.GreaterOrEqual => "greater_or_equal",
        RuleOp.LessThan => "less_than",
        RuleOp.LessOrEqual => "less_or_equal",
        RuleOp.Between => "between",
        RuleOp.NotEqual => "not_equal",
        RuleOp.MinLength => "min_length",
        RuleOp.MaxLength => "max_length",
        RuleOp.Match => "match",
        RuleOp.NotEmpty => "not_empty",
        RuleOp.In => "in",
        RuleOp.After => "after",
        RuleOp.Before => "before",
        RuleOp.Kind => "kind",
        RuleOp.InCategory => "in_category",
        RuleOp.MinCount => "min_count",
        RuleOp.MaxCount => "max_count",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public static RuleOp? ParseOp(string? name) =>
        Enum.GetValues<RuleOp>().Cast<RuleOp?>().FirstOrDefault(op => OpName(op!.Value) == name);

    /// <summary>
    /// Reads a rule for a property of <paramref name="type"/>: the operator must be available for the type and the
    /// operand well formed (<c>between</c> with min ≤ max, a valid regular expression…).
    /// </summary>
    public static Result<ValidationRule> FromJson(JsonElement json, PropertyType type, IReadOnlyList<EnumOption>? options = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("op", out var opJson) || opJson.ValueKind != JsonValueKind.String)
        {
            return Invalid("A rule is an object with an \"op\".");
        }

        if (ParseOp(opJson.GetString()) is not { } op)
        {
            return Invalid($"Unknown rule '{opJson.GetString()}'.");
        }

        if (!Available(type).Contains(op))
        {
            return Invalid($"The rule '{OpName(op)}' is not available for {type}.");
        }

        var hasValue = json.TryGetProperty("value", out var value);
        switch (op)
        {
            case RuleOp.GreaterThan or RuleOp.GreaterOrEqual or RuleOp.LessThan or RuleOp.LessOrEqual or RuleOp.NotEqual:
                return hasValue && Number(value) is { } number ? new NumberRule(op, number) : Invalid("The value is a number.");
            case RuleOp.Between:
                if (hasValue && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 2
                    && Number(value[0]) is { } min && Number(value[1]) is { } max)
                {
                    return min <= max ? new BetweenRule(min, max) : Invalid("between: the minimum exceeds the maximum.");
                }

                return Invalid("The value of between is [min, max].");
            case RuleOp.MinLength or RuleOp.MaxLength or RuleOp.MinCount or RuleOp.MaxCount:
                // Integral numbers whatever their JSON form (« 3 », « 3.0 »): JavaScript cannot tell them apart.
                return hasValue && Number(value) is { } count && Math.Floor(count) == count && count is >= 0 and <= int.MaxValue
                    ? new LengthRule(op, (int)count)
                    : Invalid("The value is a non-negative integer.");
            case RuleOp.Match:
                if (!hasValue || value.ValueKind != JsonValueKind.String)
                {
                    return Invalid("The value of match is a regular expression.");
                }

                var pattern = SafeRegex.Parse(value.GetString()!);
                return pattern.IsSuccess ? new MatchRule(pattern.Value) : pattern.Error;
            case RuleOp.NotEmpty:
                return new NotEmptyRule();
            case RuleOp.In:
                if (!hasValue || value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(k => k.ValueKind != JsonValueKind.String))
                {
                    return Invalid("The value of in is an array of option keys.");
                }

                var keys = value.EnumerateArray().Select(k => k.GetString()!).Distinct(StringComparer.Ordinal).ToImmutableArray();
                return options is not null && keys.Any(k => options.All(o => o.Key != k))
                    ? Invalid("in: unknown option.")
                    : new InRule(keys);
            case RuleOp.After or RuleOp.Before:
                return hasValue && value.ValueKind == JsonValueKind.String && PropertyValues.TryParseDate(value.GetString(), out var date)
                    ? new DateRule(op, date)
                    : Invalid("The value is a date yyyy-MM-dd.");
            case RuleOp.Kind:
                return (hasValue && value.ValueKind == JsonValueKind.String ? value.GetString() : null) switch
                {
                    "page" => new KindRule(RefKind.Page),
                    "category" => new KindRule(RefKind.Category),
                    _ => Invalid("The value of kind is \"page\" or \"category\"."),
                };
            case RuleOp.InCategory:
                return hasValue && value.ValueKind == JsonValueKind.String && ElementId.TryParse(value.GetString(), out var category)
                    ? new InCategoryRule(category)
                    : Invalid("The value of in_category is a category ID.");
            default:
                throw new ArgumentOutOfRangeException(nameof(json), op, null);
        }
    }

    /// <summary>Reads a JSON array of rules; fails on the first invalid one.</summary>
    public static Result<IReadOnlyList<ValidationRule>> ListFromJson(JsonElement json, PropertyType type, IReadOnlyList<EnumOption>? options = null)
    {
        if (json.ValueKind != JsonValueKind.Array)
        {
            return Invalid("The rules are a JSON array.");
        }

        var rules = new List<ValidationRule>();
        foreach (var item in json.EnumerateArray())
        {
            var rule = FromJson(item, type, options);
            if (!rule.IsSuccess)
            {
                return rule.Error;
            }

            rules.Add(rule.Value);
        }

        return rules;
    }

    public static JsonObject ToJson(ValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var json = new JsonObject { ["op"] = OpName(rule.Op) };
        JsonNode? value = rule switch
        {
            NumberRule n => JsonValue.Create(n.Value),
            BetweenRule b => new JsonArray(JsonValue.Create(b.Min), JsonValue.Create(b.Max)),
            LengthRule l => JsonValue.Create(l.Value),
            MatchRule m => JsonValue.Create(m.Pattern.Source),
            NotEmptyRule => null,
            InRule i => new JsonArray([.. i.Keys.Select(k => JsonValue.Create(k))]),
            DateRule d => JsonValue.Create(PropertyValues.FormatDate(d.Value)),
            KindRule k => JsonValue.Create(k.Kind == RefKind.Page ? "page" : "category"),
            InCategoryRule c => JsonValue.Create(c.CategoryId.ToString()),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
        };
        if (value is not null)
        {
            json["value"] = value;
        }

        return json;
    }

    public static JsonArray ListToJson(IEnumerable<ValidationRule> rules) => [.. rules.Select(ToJson)];

    private static double? Number(JsonElement json) =>
        json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var value) && double.IsFinite(value) ? value : null;

    private static DomainError Invalid(string message) => new(ErrorCodes.PropertyRuleInvalid, message);
}
