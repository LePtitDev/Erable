using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Erable.Core.Markdown;

/// <summary>A heading of the page, with its anchor (docs/markdown-syntax.md §3).</summary>
public sealed record MarkdownHeading(int Level, string Text, string Anchor);

/// <summary>What the backend needs from a page: references (index), headings, plain text (search).</summary>
public sealed record MarkdownAnalysis(IReadOnlyList<MarkdownReference> References, IReadOnlyList<MarkdownHeading> Headings, string PlainText);

/// <summary>
/// Server-side reading of Erable Markdown (docs/markdown-syntax.md): CommonMark + GFM through Markdig, raw HTML
/// disabled, plus the <c>[[ID]]</c> / <c>![[ID]]</c> extension. The rendering twin is the remark plugin of the SPA;
/// both are checked against <c>spec/fixtures/markdown</c>.
/// </summary>
public static partial class ErableMarkdown
{
    public static MarkdownPipeline Pipeline { get; } = BuildPipeline();

    public static MarkdownAnalysis Analyze(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        Markdig.Syntax.MarkdownDocument document;
        try
        {
            document = Markdig.Markdown.Parse(markdown, Pipeline);
        }
        catch (ArgumentException)
        {
            // Markdig refuses input nested deeper than its limit (« [ » or « > » repeated thousands of times). The page is
            // not sensible Markdown but it is the user's text: it is kept and indexed as plain text, with no reference and no
            // heading, instead of failing the save (found by the fuzzing of M5-04).
            return new MarkdownAnalysis([], [], NormalizeWhitespace(markdown));
        }

        var references = new List<MarkdownReference>();
        var headings = new List<MarkdownHeading>();
        var anchors = new Slugs.UniqueAnchors();
        var plain = new StringBuilder();

        foreach (var node in document.Descendants())
        {
            switch (node)
            {
                case ReferenceInline reference:
                    references.Add(reference.Reference);
                    break;
                case HeadingBlock heading:
                    var (text, explicitAnchor) = SplitExplicitAnchor(NormalizeWhitespace(InlineText(heading.Inline)));
                    headings.Add(new MarkdownHeading(heading.Level, text, anchors.Next(explicitAnchor ?? Slugs.Slugify(text))));
                    break;
                default:
                    break;
            }
        }

        AppendBlocks(plain, document);
        return new MarkdownAnalysis(references, headings, NormalizeWhitespace(plain.ToString()));
    }

    /// <summary>Collapses every run of whitespace (line endings included) to one space and trims.</summary>
    public static string NormalizeWhitespace(string text) => Whitespace().Replace(text, " ").Trim();

    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseTaskLists()
            .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
            .UseAutoLinks()
            .UseFootnotes()
            .UsePreciseSourceLocation()
            .DisableHtml();
        builder.InlineParsers.InsertBefore<LinkInlineParser>(new ReferenceInlineParser());
        return builder.Build();
    }

    /// <summary><c>## Titre {#ancre}</c> → (« Titre », « ancre »).</summary>
    private static (string Text, string? Anchor) SplitExplicitAnchor(string text)
    {
        var match = ExplicitAnchor().Match(text);
        return match.Success ? (match.Groups[1].Value.TrimEnd(), match.Groups[2].Value) : (text, null);
    }

    private static void AppendBlocks(StringBuilder plain, ContainerBlock container)
    {
        foreach (var block in container)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    plain.Append(SplitExplicitAnchor(NormalizeWhitespace(InlineText(heading.Inline))).Text).Append('\n');
                    break;
                case LeafBlock { Inline: { } inline }:
                    plain.Append(InlineText(inline)).Append('\n');
                    break;
                case CodeBlock code:
                    plain.Append(code.Lines.ToString()).Append('\n');
                    break;
                case ContainerBlock child:
                    // Tables are containers of rows of cells: each cell's paragraph is a separate block.
                    AppendBlocks(plain, child);
                    break;
                default:
                    break;
            }
        }
    }

    private static string InlineText(ContainerInline? container)
    {
        var text = new StringBuilder();
        AppendInlines(text, container);
        return text.ToString();
    }

    /// <summary>
    /// The text of the inlines in document order. Iterative on purpose: a hostile page can nest thousands of inlines
    /// (« [[ » repeated 5 000 times), which a recursive walk turns into a stack overflow that no handler can catch and
    /// that takes the whole server down (found by the fuzzing of M5-04).
    /// </summary>
    private static void AppendInlines(StringBuilder text, ContainerInline? container)
    {
        // The next sibling to visit in each container that is still open.
        var resume = new Stack<Inline?>();
        var inline = container?.FirstChild;
        while (true)
        {
            if (inline is null)
            {
                if (resume.Count == 0)
                {
                    return;
                }

                inline = resume.Pop();
                continue;
            }

            var current = inline;
            inline = current.NextSibling;
            switch (current)
            {
                case ReferenceInline reference:
                    text.Append(reference.Reference.Label);
                    break;
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case HtmlEntityInline entity:
                    text.Append(entity.Transcoded.ToString());
                    break;
                case AutolinkInline autolink:
                    text.Append(autolink.Url);
                    break;
                case LineBreakInline:
                    text.Append('\n');
                    break;
                case TaskList:
                    break;
                case ContainerInline child:
                    resume.Push(inline);
                    inline = child.FirstChild;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// GFM splits table rows on bare bars before inline parsing; Markdig parses inlines first. A paragraph whose second
    /// line is a delimiter row (and first line has a bar) is a table: references there must use the escaped separator (R12).
    /// </summary>
    private static bool IsTableParagraph(string paragraph)
    {
        // During inline parsing Markdig has already released the block lines; the slice text is the whole paragraph.
        var firstEnd = paragraph.IndexOf('\n', StringComparison.Ordinal);
        if (firstEnd < 0 || !paragraph.AsSpan(0, firstEnd).Contains('|'))
        {
            return false;
        }

        var secondEnd = paragraph.IndexOf('\n', firstEnd + 1);
        var delimiter = paragraph[(firstEnd + 1)..(secondEnd < 0 ? paragraph.Length : secondEnd)];
        return delimiter.Contains('|', StringComparison.Ordinal) && DelimiterRow().IsMatch(delimiter);
    }

    [GeneratedRegex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$")]
    private static partial Regex DelimiterRow();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^(.*?)\s+\{#([A-Za-z0-9_-]+)\}$", RegexOptions.Singleline)]
    private static partial Regex ExplicitAnchor();

    /// <summary>Markdig syntax node of a reference or embed.</summary>
    private sealed class ReferenceInline(MarkdownReference reference) : LeafInline
    {
        public MarkdownReference Reference { get; } = reference;
    }

    /// <summary>Runs before the link parser on <c>[</c> and <c>!</c>; declines anything that is not a reference.</summary>
    private sealed class ReferenceInlineParser : InlineParser
    {
        public ReferenceInlineParser()
        {
            OpeningCharacters = ['[', '!'];
        }

        public override bool Match(InlineProcessor processor, ref StringSlice slice)
        {
            var match = ReferenceSyntax.TryMatch(slice.Text, slice.Start, slice.End + 1, IsTableParagraph(slice.Text));
            if (match is not { } m)
            {
                return false;
            }

            var start = processor.GetSourcePosition(slice.Start, out var line, out var column);
            var reference = new MarkdownReference(m.Id, m.Anchor, m.Label, m.IsEmbed, m.Attributes, start, start + m.Length);
            processor.Inline = new ReferenceInline(reference)
            {
                Span = new SourceSpan(start, start + m.Length - 1),
                Line = line,
                Column = column,
            };
            slice.Start += m.Length;
            return true;
        }
    }
}
