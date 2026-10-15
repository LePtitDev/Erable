using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Erable.Core.Properties;

/// <summary>
/// A typed property value (docs/architecture.md §5.4). Its JSON form is untagged (the definition gives the type):
/// <c>true</c>, <c>72</c>, <c>1.5</c>, <c>"texte"</c>, <c>"marchande"</c> (enum key), <c>"2026-10-13"</c>,
/// <c>"3F2A9C14E07B5D21"</c>, <c>[…]</c>. Equality is structural, lists included.
/// </summary>
public abstract record PropertyValue
{
    private protected PropertyValue()
    {
    }
}

public sealed record BoolValue(bool Value) : PropertyValue;

/// <summary>An integer within ±(2^53 − 1): JavaScript reads it exactly.</summary>
public sealed record IntValue(long Value) : PropertyValue;

/// <summary>A finite number.</summary>
public sealed record FloatValue(double Value) : PropertyValue;

/// <summary>Value of a <c>string</c> or <c>text</c> property, NFC-normalized.</summary>
public sealed record TextValue(string Value) : PropertyValue;

/// <summary>The key of an option of an <c>enum</c> property.</summary>
public sealed record EnumValue(string Key) : PropertyValue;

public sealed record DateValue(DateOnly Value) : PropertyValue;

public sealed record RefValue(ElementId Id) : PropertyValue;

public sealed record ListValue(ImmutableArray<PropertyValue> Items) : PropertyValue
{
    public bool Equals(ListValue? other) => other is not null && Items.SequenceEqual(other.Items);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}

/// <summary>Reading and writing values as JSON, checked against a type (and the options of an enum).</summary>
public static class PropertyValues
{
    /// <summary>2^53 − 1: the largest integer a JavaScript number holds exactly.</summary>
    public const long MaxSafeInteger = 9_007_199_254_740_991;

    public const int MaxStringLength = 1_000;
    public const int MaxTextLength = 100_000;
    public const int MaxListCount = 1_000;

    /// <summary>
    /// Parses a JSON value of <paramref name="type"/>. Malformed input is refused (<c>property.value.invalid</c>):
    /// unlike rule violations, which only warn, a value always matches its type.
    /// </summary>
    public static Result<PropertyValue> FromJson(JsonElement json, PropertyType type, IReadOnlyList<EnumOption>? options = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsList)
        {
            return Scalar(json, type.Scalar, options);
        }

        if (json.ValueKind != JsonValueKind.Array)
        {
            return Invalid($"A {type} value is a JSON array.");
        }

        if (json.GetArrayLength() > MaxListCount)
        {
            return Invalid($"A list holds at most {MaxListCount} items.");
        }

        var items = ImmutableArray.CreateBuilder<PropertyValue>();
        foreach (var item in json.EnumerateArray())
        {
            var parsed = Scalar(item, type.Scalar, options);
            if (!parsed.IsSuccess)
            {
                return parsed;
            }

            items.Add(parsed.Value);
        }

        return new ListValue(items.ToImmutable());
    }

    public static Result<PropertyValue> FromJson(string json, PropertyType type, IReadOnlyList<EnumOption>? options = null)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return FromJson(document.RootElement, type, options);
        }
        catch (JsonException)
        {
            return Invalid("Malformed JSON.");
        }
    }

    public static JsonNode ToJson(PropertyValue value) => value switch
    {
        BoolValue b => JsonValue.Create(b.Value),
        IntValue i => JsonValue.Create(i.Value),
        FloatValue f => JsonValue.Create(f.Value),
        TextValue t => JsonValue.Create(t.Value),
        EnumValue e => JsonValue.Create(e.Key),
        DateValue d => JsonValue.Create(FormatDate(d.Value)),
        RefValue r => JsonValue.Create(r.Id.ToString()),
        ListValue l => new JsonArray([.. l.Items.Select(ToJson)]),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown value."),
    };

    public static string ToJsonString(PropertyValue value) => ToJson(value).ToJsonString();

    /// <summary>True when the value has the shape of <paramref name="type"/> (scalar kind and list-ness).</summary>
    public static bool Matches(PropertyValue value, PropertyType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return value is ListValue list
            ? type.IsList && list.Items.All(i => MatchesScalar(i, type.Scalar))
            : !type.IsList && MatchesScalar(value, type.Scalar);
    }

    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool MatchesScalar(PropertyValue value, ScalarType scalar) => (value, scalar) switch
    {
        (BoolValue, ScalarType.Bool) or (IntValue, ScalarType.Int) or (FloatValue, ScalarType.Float)
            or (TextValue, ScalarType.String or ScalarType.Text) or (EnumValue, ScalarType.Enum)
            or (DateValue, ScalarType.Date) or (RefValue, ScalarType.Ref) => true,
        _ => false,
    };

    private static Result<PropertyValue> Scalar(JsonElement json, ScalarType scalar, IReadOnlyList<EnumOption>? options)
    {
        switch (scalar)
        {
            case ScalarType.Bool:
                return json.ValueKind switch
                {
                    JsonValueKind.True => new BoolValue(true),
                    JsonValueKind.False => new BoolValue(false),
                    _ => Invalid("A bool value is true or false."),
                };
            case ScalarType.Int:
                // Integral JSON numbers only (« 72 », « 72.0 »), within the range JavaScript reads exactly.
                if (json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var number)
                    && Math.Floor(number) == number && Math.Abs(number) <= MaxSafeInteger)
                {
                    return new IntValue((long)number);
                }

                return Invalid($"An int value is an integer between -{MaxSafeInteger} and {MaxSafeInteger}.");
            case ScalarType.Float:
                return json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var real) && double.IsFinite(real)
                    ? new FloatValue(real)
                    : Invalid("A float value is a finite number.");
            case ScalarType.String:
            case ScalarType.Text:
                if (json.ValueKind != JsonValueKind.String)
                {
                    return Invalid($"A {PropertyType.Name(scalar)} value is a JSON string.");
                }

                var text = json.GetString()!.Normalize(NormalizationForm.FormC);
                if (scalar == ScalarType.String && (text.Length > MaxStringLength || text.Any(c => char.IsControl(c) && c != '\t')))
                {
                    return Invalid($"A string value is one line of at most {MaxStringLength} characters.");
                }

                if (scalar == ScalarType.Text && text.Length > MaxTextLength)
                {
                    return Invalid($"A text value has at most {MaxTextLength} characters.");
                }

                return new TextValue(text);
            case ScalarType.Enum:
                if (json.ValueKind != JsonValueKind.String)
                {
                    return Invalid("An enum value is the key of an option.");
                }

                var key = json.GetString()!;
                return options is null || options.Any(o => o.Key == key)
                    ? new EnumValue(key)
                    : new DomainError(ErrorCodes.PropertyValueInvalid, $"'{key}' is not an option of the property.");
            case ScalarType.Date:
                return json.ValueKind == JsonValueKind.String && TryParseDate(json.GetString(), out var date)
                    ? new DateValue(date)
                    : Invalid("A date value is a string yyyy-MM-dd.");
            case ScalarType.Ref:
                return json.ValueKind == JsonValueKind.String && ElementId.TryParse(json.GetString(), out var id)
                    ? new RefValue(id)
                    : Invalid("A ref value is an element ID.");
            default:
                throw new ArgumentOutOfRangeException(nameof(scalar), scalar, null);
        }
    }

    private static DomainError Invalid(string message) => new(ErrorCodes.PropertyValueInvalid, message);
}
