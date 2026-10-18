using System.Text;
using System.Text.Json;
using Erable.Core;
using Erable.Core.Elements;

namespace Erable.Cli;

/// <summary>
/// A page as portable Markdown (export <c>--markdown</c>, docs/cli.md): a YAML front matter with the identity,
/// categories and effective properties of the page, then its source where each <c>[[ID]]</c> becomes a relative link
/// readable by any Markdown tool. Pages are <c>pages/&lt;ID&gt;.md</c>, attachments <c>attachments/&lt;ID&gt;/&lt;name&gt;</c>.
/// </summary>
internal static class MarkdownExport
{
    public static string Write(ProjectContent content, PageDto page)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(page);
        var text = new StringBuilder();
        text.Append("---\n");
        // Scalars are JSON: a JSON string, number, array or null is valid YAML, and no escaping rule is left to chance.
        Line(text, "id", page.Element.Id.ToString());
        Line(text, "title", page.Element.Name);
        Line(text, "path", page.Element.Path);
        Line(text, "categories", page.Categories.Select(c => c.Name).ToList());
        if (page.Properties.Count == 0)
        {
            text.Append("properties: {}\n");
        }
        else
        {
            text.Append("properties:\n");
            foreach (var property in page.Properties)
            {
                text.Append("  ").Append(Json(property.Key)).Append(": ").Append(property.Value?.ToJsonString(Contract.Compact) ?? "null").Append('\n');
            }
        }

        Line(text, "updatedAt", page.Element.UpdatedAt);
        Line(text, "updatedBy", page.Element.UpdatedBy);
        text.Append("---\n\n");
        text.Append(Rewrite(content, page.Markdown ?? string.Empty));
        if (text[^1] != '\n')
        {
            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Replaces each reference by a Markdown link: a page by <c>[name](ID.md#anchor)</c>, an attachment by a link (an
    /// image embed by <c>![caption](…)</c>, SVG excepted: never rendered inline, §A-9), another element by its name. A
    /// reference to a deleted or unknown element is left as it is. Code spans and blocks are not references, so they
    /// stay untouched.
    /// </summary>
    public static string Rewrite(ProjectContent content, string markdown)
    {
        var result = new StringBuilder(markdown.Length);
        var position = 0;
        foreach (var reference in ProjectContent.ReferencesOf(markdown).OrderBy(r => r.Start))
        {
            if (reference.Start < position || reference.End > markdown.Length)
            {
                continue;
            }

            result.Append(markdown, position, reference.Start - position);
            var original = markdown[reference.Start..reference.End];
            result.Append(content.Find(reference.Id) is { } target
                ? Replacement(content, target.Id, target.Kind, target.Name, reference.Anchor, reference.Label, reference.IsEmbed) ?? original
                : original);
            position = reference.End;
        }

        result.Append(markdown, position, markdown.Length - position);
        return result.ToString();
    }

    private static string? Replacement(
        ProjectContent content, ElementId id, ElementKind kind, string name, string? anchor, string? label, bool embed)
    {
        switch (kind)
        {
            case ElementKind.Page:
                return $"[{EscapeText(label ?? name)}](<{id}.md{(anchor is null ? string.Empty : "#" + anchor)}>)";
            case ElementKind.Attachment when content.FindAttachment(id) is { } attachment:
                var dto = content.Attachment(attachment);
                var link = $"<../{dto.File}>";
                var image = embed && attachment.Mime.StartsWith("image/", StringComparison.Ordinal) && attachment.Mime != "image/svg+xml";
                return $"{(image ? "!" : string.Empty)}[{EscapeText(label ?? attachment.FileName)}]({link})";
            case ElementKind.Attachment:
            case ElementKind.Root:
                return null;
            case ElementKind.Folder:
            case ElementKind.Category:
            case ElementKind.PropertyDefinition:
            case ElementKind.LinkType:
                return EscapeText(label ?? name);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>Backslash before what Markdown would read as markup in a link text or plain text.</summary>
    private static string EscapeText(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '\\' or '[' or ']' or '*' or '_' or '`' or '<' or '>')
            {
                escaped.Append('\\');
            }

            escaped.Append(c);
        }

        return escaped.ToString();
    }

    private static void Line<T>(StringBuilder text, string key, T value) => text.Append(key).Append(": ").Append(Json(value)).Append('\n');

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, Contract.Compact);
}
