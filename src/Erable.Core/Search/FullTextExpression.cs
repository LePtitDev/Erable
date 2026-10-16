using System.Globalization;
using System.Text;

namespace Erable.Core.Search;

/// <summary>
/// Turns what the user typed into an FTS5 <c>MATCH</c> expression. The user text is never passed to FTS5 as it is: it is
/// cut into words (letters, digits and their marks), each word becomes a quoted string, and the last one is a prefix so
/// that the search answers while typing. Operators, column names and quotes typed by the user are therefore plain text.
/// </summary>
public static class FullTextExpression
{
    public const int MaxWords = 16;
    public const int MaxWordLength = 64;

    /// <summary>The words of a text, in order, as FTS5 would cut them (empty for no word).</summary>
    public static IReadOnlyList<string> Words(string? text)
    {
        var words = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return words;
        }

        var current = new StringBuilder();
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsWordRune(rune))
            {
                current.Append(rune.ToString());
                continue;
            }

            Flush(current, words);
        }

        Flush(current, words);
        return words;
    }

    /// <summary>
    /// The <c>MATCH</c> expression for <paramref name="text"/>, or null when it has no word. With
    /// <paramref name="nameOnly"/> only the name of the elements is searched (quick search).
    /// </summary>
    public static string? Build(string? text, bool nameOnly = false)
    {
        var words = Words(text);
        if (words.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < words.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            // A word holds neither a quote nor any other character FTS5 reads as syntax.
            builder.Append('"').Append(words[i]).Append('"');
            if (i == words.Count - 1)
            {
                builder.Append('*');
            }
        }

        return nameOnly ? $"{{name}} : ({builder})" : builder.ToString();
    }

    private static bool IsWordRune(Rune rune) => Rune.GetUnicodeCategory(rune) switch
    {
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber
            or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark => true,
        _ => false,
    };

    private static void Flush(StringBuilder current, List<string> words)
    {
        if (current.Length > 0 && words.Count < MaxWords)
        {
            var word = current.ToString();
            if (word.Length > MaxWordLength)
            {
                // Never cut a surrogate pair in two.
                word = word[..(char.IsHighSurrogate(word[MaxWordLength - 1]) ? MaxWordLength - 1 : MaxWordLength)];
            }

            words.Add(word);
        }

        current.Clear();
    }
}
