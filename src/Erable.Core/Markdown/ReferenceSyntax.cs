namespace Erable.Core.Markdown;

/// <summary>Display attributes of an embed: <c>![[ID]]{width=320 align=right}</c>.</summary>
public sealed record EmbedAttributes(int? Width, string? Align);

/// <summary>A reference or embed found in Markdown (docs/markdown-syntax.md §2).</summary>
/// <param name="Id">Target element.</param>
/// <param name="Anchor">Heading anchor of the target (references only).</param>
/// <param name="Label">Free label or embed caption; null when absent or blank.</param>
/// <param name="IsEmbed"><c>![[…]]</c> rather than <c>[[…]]</c>.</param>
/// <param name="Attributes">Embed attributes, null when absent.</param>
/// <param name="Start">UTF-16 offset of the first character (<c>[</c> or <c>!</c>).</param>
/// <param name="End">UTF-16 offset after the last consumed character.</param>
public sealed record MarkdownReference(
    ElementId Id, string? Anchor, string? Label, bool IsEmbed, EmbedAttributes? Attributes, int Start, int End);

/// <summary>
/// Character-level grammar of references, independent of Markdig: scans one candidate starting at a given index.
/// Kept separate so that the rules of docs/markdown-syntax.md §2.1 read in one place.
/// </summary>
internal static class ReferenceSyntax
{
    public const int MaxWidth = 4096;

    /// <summary>Longest label (before trimming): bounds the scan, so that parsing stays linear (R11).</summary>
    public const int MaxLabelLength = 200;

    /// <summary>Longest attribute block between the braces (R11).</summary>
    public const int MaxAttributesLength = 64;

    /// <summary>A match: the parsed reference (offsets relative to the scanned text) and its length.</summary>
    public readonly record struct Match(ElementId Id, string? Anchor, string? Label, bool IsEmbed, EmbedAttributes? Attributes, int Length);

    /// <summary>Tries to read a reference or embed at <paramref name="start"/> of <paramref name="text"/>, up to <paramref name="end"/> (exclusive).</summary>
    /// <param name="text">Text to scan.</param>
    /// <param name="start">Index of the candidate's first character.</param>
    /// <param name="end">Exclusive upper bound of the scan.</param>
    /// <param name="inTableRow">True inside a GFM table row, where a bare bar is a cell delimiter (R12).</param>
    public static Match? TryMatch(string text, int start, int end, bool inTableRow = false)
    {
        var i = start;
        var embed = false;
        if (i < end && text[i] == '!')
        {
            embed = true;
            i++;
        }

        if (i + 1 >= end || text[i] != '[' || text[i + 1] != '[')
        {
            return null;
        }

        i += 2;
        if (i + ElementId.Length > end || !ElementId.TryParse(text.AsSpan(i, ElementId.Length), out var id))
        {
            return null;
        }

        i += ElementId.Length;
        string? anchor = null;
        if (i < end && text[i] == '#')
        {
            if (embed)
            {
                return null;
            }

            var anchorStart = ++i;
            while (i < end && IsAnchorChar(text[i]))
            {
                i++;
            }

            if (i == anchorStart)
            {
                return null;
            }

            anchor = text[anchorStart..i];
        }

        string? label = null;
        var separator = SeparatorLength(text, i, end);
        if (separator > 0)
        {
            // In a GFM table row a bare bar splits the cell before inline parsing (R12).
            if (inTableRow && separator == 1)
            {
                return null;
            }

            i += separator;
            var labelStart = i;
            while (i + 1 < end && !(text[i] == ']' && text[i + 1] == ']'))
            {
                if (text[i] is '\n' or '\r' || i - labelStart >= MaxLabelLength
                    || (inTableRow && text[i] == '|' && text[i - 1] != '\\'))
                {
                    return null;
                }

                i++;
            }

            if (i + 1 >= end)
            {
                return null;
            }

            var trimmed = text[labelStart..i].Replace("\\|", "|", StringComparison.Ordinal).Trim();
            label = trimmed.Length == 0 ? null : trimmed;
        }

        if (i + 1 >= end || text[i] != ']' || text[i + 1] != ']')
        {
            return null;
        }

        i += 2;
        EmbedAttributes? attributes = null;
        if (embed && TryReadAttributes(text, i, end, out var parsed, out var length))
        {
            attributes = parsed;
            i += length;
        }

        return new Match(id, anchor, label, embed, attributes, i - start);
    }

    /// <summary>Label separator at <paramref name="i"/>: 1 for <c>|</c>, 2 for <c>\|</c>, 0 otherwise.</summary>
    private static int SeparatorLength(string text, int i, int end) =>
        i < end && text[i] == '|' ? 1
        : i + 1 < end && text[i] == '\\' && text[i + 1] == '|' ? 2
        : 0;

    public static bool IsAnchorChar(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_';

    /// <summary><c>{width=320 align=right}</c>: known keys, each at most once, valid values; otherwise nothing is consumed.</summary>
    private static bool TryReadAttributes(string text, int start, int end, out EmbedAttributes? attributes, out int length)
    {
        attributes = null;
        length = 0;
        if (start >= end || text[start] != '{')
        {
            return false;
        }

        var close = text.IndexOf('}', start + 1, Math.Min(end - start - 1, MaxAttributesLength + 1));
        if (close < 0)
        {
            return false;
        }

        var body = text[(start + 1)..close];
        if (body.Length == 0 || body[0] == ' ' || body[^1] == ' ')
        {
            return false;
        }

        int? width = null;
        string? align = null;
        foreach (var pair in body.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                return false;
            }

            var key = pair[..equals];
            var value = pair[(equals + 1)..];
            switch (key)
            {
                case "width" when width is null && TryParseWidth(value, out var w):
                    width = w;
                    break;
                case "align" when align is null && value is "left" or "center" or "right":
                    align = value;
                    break;
                default:
                    return false;
            }
        }

        attributes = new EmbedAttributes(width, align);
        length = close - start + 1;
        return true;
    }

    private static bool TryParseWidth(string value, out int width)
    {
        width = 0;
        if (value.Length is 0 or > 4 || value[0] == '0' || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        width = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        return width <= MaxWidth;
    }
}
