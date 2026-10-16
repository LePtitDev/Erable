using System.Globalization;
using System.Text;

namespace Erable.Core.Search;

/// <summary>
/// Case and accent folding (F10.R2): « Col gelé » and « col gele » fold to the same text. The same rule serves the
/// sort by name and the « contains » filter, so that they agree with the full-text index (<c>remove_diacritics</c>).
/// </summary>
public static class TextFolding
{
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
