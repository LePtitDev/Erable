namespace Erable.Core.Markdown;

/// <summary>The rule a page broke (docs/markdown-syntax.md §6); each has a stable business error code.</summary>
public enum MarkdownLimitRule
{
    /// <summary>The page is longer than <see cref="MarkdownLimits.MaxLength"/>.</summary>
    Size,

    /// <summary>A line is longer than <see cref="MarkdownLimits.MaxLineLength"/>.</summary>
    LineLength,

    /// <summary>One of <c>[ ] ( ) &gt;</c> repeated more than <see cref="MarkdownLimits.MaxRepeatedCharacters"/> times in a row.</summary>
    RepeatedCharacter,

    /// <summary>Quotes and list markers nested deeper than <see cref="MarkdownLimits.MaxNesting"/>, or an indentation too wide.</summary>
    Nesting,

    /// <summary>More than <see cref="MarkdownLimits.MaxParagraphLines"/> consecutive lines without a blank line.</summary>
    ParagraphLines,
}

/// <param name="Rule">The rule that was broken.</param>
/// <param name="Line">1-based line of the first offence; 0 for the size of the whole page.</param>
public sealed record MarkdownLimitViolation(MarkdownLimitRule Rule, int Line)
{
    public string Code => Rule switch
    {
        MarkdownLimitRule.Size => ErrorCodes.PageContentTooLarge,
        MarkdownLimitRule.LineLength => ErrorCodes.PageContentLineTooLong,
        MarkdownLimitRule.RepeatedCharacter => ErrorCodes.PageContentRepeatedCharacter,
        MarkdownLimitRule.Nesting => ErrorCodes.PageContentTooNested,
        _ => ErrorCodes.PageContentParagraphTooLong,
    };
}

/// <summary>
/// Safety limits on the Markdown of a page (docs/markdown-syntax.md §6). The parsers take quadratic time, or
/// overflow their stack, on a few dozen kilobytes of degenerate input (<c>]</c> × 40 000, <c>&gt; </c> × 8 000): one
/// linear pre-scan refuses those shapes before any parser runs. Real pages never come near. The TypeScript twin is
/// <c>shared/markdown/limits.ts</c>; both are driven by <c>spec/fixtures/limits</c>.
/// </summary>
public static class MarkdownLimits
{
    /// <summary>UTF-16 code units: storage, search index and revisions, not parsing time.</summary>
    public const int MaxLength = 1_048_576;

    public const int MaxLineLength = 10_000;

    public const int MaxRepeatedCharacters = 256;

    /// <summary>Quote and list markers at the start of one line (<c>&gt; &gt; 1. - </c>).</summary>
    public const int MaxNesting = 32;

    /// <summary>Leading spaces and tabs of a line (64 levels of four spaces).</summary>
    public const int MaxIndentation = 256;

    public const int MaxParagraphLines = 2_000;

    /// <summary>The first broken rule in document order, or null when the page is within the limits.</summary>
    public static MarkdownLimitViolation? Check(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        if (markdown.Length > MaxLength)
        {
            return new MarkdownLimitViolation(MarkdownLimitRule.Size, 0);
        }

        var lineNumber = 0;
        var consecutiveLines = 0;
        var start = 0;
        while (true)
        {
            var newline = markdown.IndexOf('\n', start);
            var end = newline < 0 ? markdown.Length : newline;
            var lineEnd = end > start && markdown[end - 1] == '\r' ? end - 1 : end;
            lineNumber++;

            if (!CheckLine(markdown.AsSpan(start, lineEnd - start), out var rule, out var blank))
            {
                return new MarkdownLimitViolation(rule, lineNumber);
            }

            consecutiveLines = blank ? 0 : consecutiveLines + 1;
            if (consecutiveLines > MaxParagraphLines)
            {
                return new MarkdownLimitViolation(MarkdownLimitRule.ParagraphLines, lineNumber);
            }

            if (newline < 0)
            {
                return null;
            }

            start = newline + 1;
        }
    }

    /// <summary>True when the line is within the limits.</summary>
    private static bool CheckLine(ReadOnlySpan<char> line, out MarkdownLimitRule rule, out bool blank)
    {
        rule = default;
        blank = true;
        if (line.Length > MaxLineLength)
        {
            rule = MarkdownLimitRule.LineLength;
            return false;
        }

        var run = 0;
        var previous = '\0';
        foreach (var c in line)
        {
            if (c is not (' ' or '\t'))
            {
                blank = false;
            }

            run = c == previous ? run + 1 : 1;
            previous = c;
            if (run > MaxRepeatedCharacters && c is '[' or ']' or '(' or ')' or '>')
            {
                rule = MarkdownLimitRule.RepeatedCharacter;
                return false;
            }
        }

        if (!blank && !NestingWithinLimits(line))
        {
            rule = MarkdownLimitRule.Nesting;
            return false;
        }

        return true;
    }

    /// <summary>Leading whitespace, then quote and list markers: <c>&gt; &gt; - 1. text</c>.</summary>
    private static bool NestingWithinLimits(ReadOnlySpan<char> line)
    {
        var i = SkipBlanks(line, 0);
        if (i > MaxIndentation)
        {
            return false;
        }

        var markers = 0;
        while (i < line.Length)
        {
            var width = MarkerWidth(line, i);
            if (width == 0)
            {
                return true;
            }

            markers++;
            if (markers > MaxNesting)
            {
                return false;
            }

            i = SkipBlanks(line, i + width);
        }

        return true;
    }

    private static int SkipBlanks(ReadOnlySpan<char> line, int from)
    {
        while (from < line.Length && line[from] is ' ' or '\t')
        {
            from++;
        }

        return from;
    }

    /// <summary>Width of the quote or list marker at <paramref name="i"/>, 0 when the text is not one.</summary>
    private static int MarkerWidth(ReadOnlySpan<char> line, int i)
    {
        var c = line[i];
        if (c == '>')
        {
            return 1;
        }

        if (c is '-' or '*' or '+')
        {
            return i + 1 < line.Length && line[i + 1] == ' ' ? 2 : 0;
        }

        var digits = 0;
        while (i + digits < line.Length && char.IsAsciiDigit(line[i + digits]))
        {
            digits++;
        }

        var after = i + digits;
        return digits is >= 1 and <= 9 && after + 1 < line.Length && line[after] is '.' or ')' && line[after + 1] == ' '
            ? digits + 2
            : 0;
    }
}
