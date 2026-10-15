using System.Globalization;
using System.Text;

// CA1720 (identifier contains a type name): int, float and string are the type names of the documents.
#pragma warning disable CA1720

namespace Erable.Core.Properties;

/// <summary>Type of a single value (docs/architecture.md §5.4); the names are those of the documents.</summary>
public enum ScalarType
{
    Bool,
    Int,
    Float,

    /// <summary>One line of text.</summary>
    String,

    /// <summary>Multi-line text.</summary>
    Text,

    /// <summary>One option of the definition, stored by its key.</summary>
    Enum,

    /// <summary>A calendar date, ISO 8601 <c>yyyy-MM-dd</c>.</summary>
    Date,

    /// <summary>The ID of an element (page or category).</summary>
    Ref,
}

/// <summary>
/// Type of a property: a scalar, or a list of scalars (<c>list&lt;T&gt;</c>). Written as in the documents:
/// <c>int</c>, <c>list&lt;string&gt;</c>.
/// </summary>
public sealed record PropertyType(ScalarType Scalar, bool IsList = false)
{
    public static readonly PropertyType Bool = new(ScalarType.Bool);
    public static readonly PropertyType Int = new(ScalarType.Int);
    public static readonly PropertyType Float = new(ScalarType.Float);
    public static readonly PropertyType String = new(ScalarType.String);
    public static readonly PropertyType Text = new(ScalarType.Text);
    public static readonly PropertyType Enum = new(ScalarType.Enum);
    public static readonly PropertyType Date = new(ScalarType.Date);
    public static readonly PropertyType Ref = new(ScalarType.Ref);

    /// <summary>The type of one item: the type itself for a scalar.</summary>
    public PropertyType Item => this with { IsList = false };

    public static PropertyType ListOf(ScalarType scalar) => new(scalar, IsList: true);

    public static Result<PropertyType> Parse(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        var isList = value.StartsWith("list<", StringComparison.Ordinal) && value.EndsWith('>');
        var scalar = isList ? value[5..^1].Trim() : value;
        return ScalarOf(scalar) is { } parsed
            ? new PropertyType(parsed, isList)
            : new DomainError(ErrorCodes.PropertyTypeInvalid, $"'{text}' is not a property type.");
    }

    public override string ToString() => IsList ? $"list<{Name(Scalar)}>" : Name(Scalar);

    public static string Name(ScalarType scalar) => scalar switch
    {
        ScalarType.Bool => "bool",
        ScalarType.Int => "int",
        ScalarType.Float => "float",
        ScalarType.String => "string",
        ScalarType.Text => "text",
        ScalarType.Enum => "enum",
        ScalarType.Date => "date",
        ScalarType.Ref => "ref",
        _ => throw new ArgumentOutOfRangeException(nameof(scalar), scalar, null),
    };

    private static ScalarType? ScalarOf(string name) => name switch
    {
        "bool" => ScalarType.Bool,
        "int" => ScalarType.Int,
        "float" => ScalarType.Float,
        "string" => ScalarType.String,
        "text" => ScalarType.Text,
        "enum" => ScalarType.Enum,
        "date" => ScalarType.Date,
        "ref" => ScalarType.Ref,
        _ => null,
    };
}

/// <summary>One option of an <c>enum</c> property: a stable key (stored in the values) and a label (shown, renamable, F08.R5).</summary>
public sealed record EnumOption(string Key, string Label);

/// <summary>
/// Readable identifier of a property (<c>niveau</c>, <c>points_de_vie</c>), unique in a project and renamable without
/// breaking anything: references go through the ID (F08.R1, E2E-08-11). Also used for enum option keys.
/// </summary>
public static class PropertyKey
{
    public const int MaxLength = 64;

    /// <summary>Lower-case ASCII letters, digits, <c>_</c> and <c>-</c>, starting with a letter.</summary>
    public static Result<string> Normalize(string? key)
    {
        var trimmed = key?.Trim() ?? string.Empty;
        var valid = trimmed.Length is > 0 and <= MaxLength
            && trimmed[0] is >= 'a' and <= 'z'
            && trimmed.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-');
        return valid
            ? trimmed
            : new DomainError(ErrorCodes.PropertyKeyInvalid, $"'{key}' is not a valid key (a-z, 0-9, '_', '-', starting with a letter).");
    }

    /// <summary>A key derived from a display name: « Points de vie » → <c>points_de_vie</c>, « Âge » → <c>age</c>.</summary>
    public static string FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                builder.Append(lower);
            }
            else if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        var key = builder.ToString().Trim('_');
        if (key.Length > MaxLength)
        {
            key = key[..MaxLength].TrimEnd('_');
        }

        return key.Length == 0 || key[0] is not (>= 'a' and <= 'z') ? "p_" + key : key;
    }

    /// <summary>The first of <c>key</c>, <c>key_2</c>, <c>key_3</c>… not in <paramref name="taken"/>.</summary>
    public static string Unique(string key, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var used = taken.ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(key))
        {
            return key;
        }

        for (var i = 2; ; i++)
        {
            var suffix = "_" + i.ToString(CultureInfo.InvariantCulture);
            var candidate = (key.Length + suffix.Length > MaxLength ? key[..(MaxLength - suffix.Length)] : key) + suffix;
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
