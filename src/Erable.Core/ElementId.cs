using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Erable.Core;

/// <summary>
/// Universal identifier of every element (page, folder, category, property, link type, attachment), docs/architecture.md §4.
/// Outside the database it is <b>always</b> a 16-character hexadecimal string: a JSON number would silently lose
/// precision above 2^53 in JavaScript.
/// </summary>
[JsonConverter(typeof(ElementIdJsonConverter))]
public readonly record struct ElementId : IParsable<ElementId>, ISpanParsable<ElementId>
{
    /// <summary>Length of the canonical form.</summary>
    public const int Length = 16;

    /// <summary>Regular expression of the canonical form, as published in the OpenAPI schema.</summary>
    public const string Pattern = "^[0-9A-F]{16}$";

    /// <summary>The reserved value 0 (« none »). Never a valid element.</summary>
    public static readonly ElementId None;

    public ElementId(ulong value)
    {
        Value = value;
    }

    /// <summary>Raw value. Never exposed in JSON: use <see cref="ToString"/>.</summary>
    public ulong Value { get; }

    public bool IsNone => Value == 0;

    /// <summary>
    /// Values below this one are reserved for well-known elements shared by every project (the four roots):
    /// <see cref="NewRandom"/> never returns them.
    /// </summary>
    public const ulong FirstRandomValue = 0x10000;

    /// <summary>64 random bits from a cryptographic generator, never 0 nor a reserved value (F03.R1).</summary>
    public static ElementId NewRandom()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        ulong value;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            value = BitConverter.ToUInt64(bytes);
        }
        while (value < FirstRandomValue);

        return new ElementId(value);
    }

    /// <summary>SQLite stores a signed <c>INTEGER</c>: lossless reinterpretation of the 64 bits.</summary>
    public static ElementId FromInt64(long value) => new(unchecked((ulong)value));

    /// <summary>SQLite stores a signed <c>INTEGER</c>: lossless reinterpretation of the 64 bits.</summary>
    public long ToInt64() => unchecked((long)Value);

    /// <summary>Canonical form: 16 upper-case hexadecimal characters, e.g. <c>3F2A9C14E07B5D21</c>.</summary>
    public override string ToString() => Value.ToString("X16", CultureInfo.InvariantCulture);

    /// <summary>Display form: groups of four, e.g. <c>3F2A 9C14 E07B 5D21</c>.</summary>
    public string ToDisplayString()
    {
        var hex = ToString();
        return string.Create(Length + 3, hex, static (span, h) =>
        {
            var written = 0;
            for (var i = 0; i < Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                {
                    span[written++] = ' ';
                }

                span[written++] = h[i];
            }
        });
    }

    /// <summary>
    /// Reads exactly 16 hexadecimal characters (case-insensitive). Anything else fails: spaces, 15 or 17 characters,
    /// a sign, a <c>0x</c> prefix, and the reserved value 0.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, out ElementId result)
    {
        result = None;
        if (s.Length != Length)
        {
            return false;
        }

        ulong value = 0;
        foreach (var c in s)
        {
            int digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'A' and <= 'F' => c - 'A' + 10,
                >= 'a' and <= 'f' => c - 'a' + 10,
                _ => -1,
            };
            if (digit < 0)
            {
                return false;
            }

            value = (value << 4) | (uint)digit;
        }

        if (value == 0)
        {
            return false;
        }

        result = new ElementId(value);
        return true;
    }

    public static bool TryParse([NotNullWhen(true)] string? s, out ElementId result)
    {
        if (s is null)
        {
            result = None;
            return false;
        }

        return TryParse(s.AsSpan(), out result);
    }

    public static ElementId Parse(string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return TryParse(s, out var id) ? id : throw new FormatException($"'{s}' is not a valid element ID (16 hexadecimal characters, not zero).");
    }

    // IParsable / ISpanParsable: used by minimal API route and query binding.
    static ElementId IParsable<ElementId>.Parse(string s, IFormatProvider? provider) => Parse(s);

    static bool IParsable<ElementId>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out ElementId result) =>
        TryParse(s, out result);

    static ElementId ISpanParsable<ElementId>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        TryParse(s, out var id) ? id : throw new FormatException("Not a valid element ID.");

    static bool ISpanParsable<ElementId>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out ElementId result) =>
        TryParse(s, out result);
}

/// <summary>Reads and writes an <see cref="ElementId"/> as a canonical string only; a JSON number is rejected.</summary>
public sealed class ElementIdJsonConverter : JsonConverter<ElementId>
{
    public override ElementId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"An element ID must be a JSON string, not {reader.TokenType}.");
        }

        return ElementId.TryParse(reader.GetString(), out var id)
            ? id
            : throw new JsonException("An element ID is 16 hexadecimal characters and not zero.");
    }

    public override void Write(Utf8JsonWriter writer, ElementId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }

    public override ElementId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ElementId.TryParse(reader.GetString(), out var id)
            ? id
            : throw new JsonException("An element ID is 16 hexadecimal characters and not zero.");

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ElementId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WritePropertyName(value.ToString());
    }
}
