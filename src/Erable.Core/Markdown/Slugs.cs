using System.Globalization;
using System.Text;

namespace Erable.Core.Markdown;

/// <summary>Heading anchors (docs/markdown-syntax.md §3.1); the TypeScript twin is <c>src/web/src/shared/markdown/slug.ts</c>.</summary>
public static class Slugs
{
    public const string Fallback = "section";

    private static readonly Dictionary<char, string> Replacements = new()
    {
        ['œ'] = "oe",
        ['Œ'] = "oe",
        ['æ'] = "ae",
        ['Æ'] = "ae",
        ['ß'] = "ss",
        ['ø'] = "o",
        ['Ø'] = "o",
        ['đ'] = "d",
        ['Đ'] = "d",
        ['ł'] = "l",
        ['Ł'] = "l",
    };

    public static string Slugify(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var replaced = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (Replacements.TryGetValue(c, out var replacement))
            {
                replaced.Append(replacement);
            }
            else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                replaced.Append(c).Append(text[++i]);
            }
            else if (char.IsSurrogate(c))
            {
                // A lone surrogate is not text; Normalize would throw. It is a separator, as in JavaScript.
                replaced.Append(' ');
            }
            else
            {
                replaced.Append(c);
            }
        }

        var slug = new StringBuilder(replaced.Length);
        var pendingDash = false;
        foreach (var c in replaced.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && slug.Length > 0)
                {
                    slug.Append('-');
                }

                pendingDash = false;
                slug.Append(lower);
            }
            else
            {
                pendingDash = true;
            }
        }

        return slug.Length == 0 ? Fallback : slug.ToString();
    }

    /// <summary>Makes anchors unique within a page: the second <c>a</c> becomes <c>a-1</c>, the third <c>a-2</c>.</summary>
    public sealed class UniqueAnchors
    {
        private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);

        public string Next(string anchor)
        {
            ArgumentNullException.ThrowIfNull(anchor);
            if (!_seen.TryGetValue(anchor, out var count))
            {
                _seen[anchor] = 1;
                return anchor;
            }

            string candidate;
            do
            {
                candidate = $"{anchor}-{count.ToString(CultureInfo.InvariantCulture)}";
                count++;
            }
            while (_seen.ContainsKey(candidate));

            _seen[anchor] = count;
            _seen[candidate] = 1;
            return candidate;
        }
    }
}
