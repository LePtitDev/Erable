using System.Text;
using System.Text.Json;
using Erable.Core;
using Erable.Core.Elements;

namespace Erable.Cli;

/// <summary>
/// <c>export</c>: the whole live project as files (docs/cli.md §Export), stable from one export to the next so that
/// a diff shows what changed:
/// <code>
/// project.json  tree.json  schema.json  links.json  attachments.json
/// pages/&lt;ID&gt;.json            (pages/&lt;ID&gt;.md with --markdown)
/// attachments/&lt;ID&gt;/&lt;name&gt;     (unless --no-attachments)
/// </code>
/// </summary>
internal static class FolderExport
{
    /// <summary>What an export writes at the top of its folder; <c>--force</c> replaces these and nothing else.</summary>
    public static readonly IReadOnlyList<string> Entries =
        ["project.json", "tree.json", "schema.json", "links.json", "attachments.json", "pages", "attachments"];

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public static Result<ExportDocument> Run(ProjectContent content, string output, bool markdown, bool attachments, bool force)
    {
        ArgumentNullException.ThrowIfNull(content);
        output = Path.GetFullPath(output);
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            if (!force)
            {
                return new DomainError(CliErrors.OutputNotEmpty, $"'{output}' is not empty. Add --force to replace a previous export there.");
            }

            foreach (var entry in Entries.Select(e => Path.Combine(output, e)))
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }
            }
        }

        Directory.CreateDirectory(Path.Combine(output, "pages"));
        WriteJson(Path.Combine(output, "project.json"), content.Info());
        WriteJson(Path.Combine(output, "tree.json"), content.TreeDocument());
        WriteJson(Path.Combine(output, "schema.json"), content.Schema());
        WriteJson(Path.Combine(output, "links.json"), content.LinksDocument());
        var attachmentList = content.AttachmentsDocument();
        WriteJson(Path.Combine(output, "attachments.json"), attachmentList);

        var pages = 0;
        foreach (var id in content.Pages())
        {
            var page = content.Page(id);
            WriteJson(Path.Combine(output, "pages", $"{id}.json"), new PageDocument(Contract.Version, content.Project, page));
            if (markdown)
            {
                File.WriteAllText(Path.Combine(output, "pages", $"{id}.md"), MarkdownExport.Write(content, page), Utf8);
            }

            pages++;
        }

        var missing = new List<ElementId>();
        if (attachments)
        {
            foreach (var attachment in attachmentList.Attachments)
            {
                using var source = content.FindAttachment(attachment.Id) is { } record ? content.OpenAttachment(record) : null;
                if (source is null)
                {
                    missing.Add(attachment.Id);
                    continue;
                }

                var target = Path.Combine([output, .. attachment.File.Split('/')]);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var file = File.Create(target);
                source.CopyTo(file);
            }
        }

        return new ExportDocument(
            Contract.Version, content.Project, output, pages, attachments ? attachmentList.Attachments.Count - missing.Count : 0, markdown, missing);
    }

    private static void WriteJson<T>(string path, T document) =>
        File.WriteAllText(path, JsonSerializer.Serialize(document, Contract.Indented) + "\n", Utf8);
}

/// <summary>Error codes of <c>erable-cli</c> besides those of the storage (<c>project.*</c>).</summary>
public static class CliErrors
{
    public const string ElementNotFound = "element.notFound";

    public const string NotAPage = "element.notAPage";

    public const string NotAnAttachment = "element.notAnAttachment";

    public const string NotACategory = "element.notACategory";

    public const string InvalidId = "element.invalidId";

    public const string OutputNotEmpty = "output.notEmpty";

    public const string ContentMissing = "attachment.contentMissing";
}
