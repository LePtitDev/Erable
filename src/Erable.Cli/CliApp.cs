using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Storage;

namespace Erable.Cli;

/// <summary>
/// Command line of <c>erable-cli</c> (docs/cli.md): reads a project, never writes it. Each command prints one JSON
/// document (<see cref="Contract"/>) on the standard output; an error prints <see cref="ErrorDocument"/> on the error
/// output and exits with one of the codes below.
/// </summary>
public static class CliApp
{
    public const int Success = 0;

    /// <summary>Invalid command line (unknown command or option, missing argument).</summary>
    public const int UsageError = 1;

    /// <summary>The folder is not a readable project, or its format is newer than this tool.</summary>
    public const int ProjectUnreadable = 2;

    /// <summary>No live element has this ID, or it is not of the expected kind.</summary>
    public const int ElementNotFound = 3;

    /// <summary>The output cannot be written (export folder not empty without <c>--force</c>, IO error).</summary>
    public const int OutputError = 4;

    public static Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, Func<Stream>? standardOutput = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        standardOutput ??= Console.OpenStandardOutput;

        var pretty = new Option<bool>("--pretty") { Description = "Indented JSON.", Recursive = true };
        Argument<DirectoryInfo> Project() => new("project") { Description = "Project folder (the one holding erable.json)." };
        Argument<string> Id(string description) => new("id") { Description = description };
        Option<bool> NoContent() => new("--no-content") { Description = "Leave out the Markdown and the plain text of the pages." };

        var info = new Command("info", "Project identity and counts.");
        var infoProject = Project();
        info.Add(infoProject);
        info.SetAction(parse => Read<InfoDocument>(parse, infoProject, content => content.Info()));

        var tree = new Command("tree", "Every live element, depth first, siblings in their order.");
        var treeProject = Project();
        var kinds = new Option<string[]>("--kind")
        {
            Description = "Only these kinds (repeatable): folder, page, category, propertyDefinition, linkType, attachment, root.",
            AllowMultipleArgumentsPerToken = true,
        };
        tree.Add(treeProject);
        tree.Add(kinds);
        tree.SetAction(parse => Read<TreeDocument>(parse, treeProject, content =>
        {
            var document = content.TreeDocument();
            var wanted = parse.GetValue(kinds) ?? [];
            return wanted.Length == 0 ? document : document with { Elements = [.. document.Elements.Where(e => wanted.Contains(e.Kind, StringComparer.Ordinal))] };
        }));

        var page = new Command("page", "One page in full: Markdown, categories, effective properties, references, links, attachments.");
        var pageProject = Project();
        var pageId = Id("ID of the page (16 hexadecimal characters).");
        var pageNoContent = NoContent();
        page.Add(pageProject);
        page.Add(pageId);
        page.Add(pageNoContent);
        page.SetAction(parse => Read<PageDocument>(parse, pageProject, content =>
        {
            var found = Find(content, parse.GetRequiredValue(pageId), ElementKind.Page, CliErrors.NotAPage);
            return found.IsSuccess
                ? new PageDocument(Contract.Version, content.Project, content.Page(found.Value, !parse.GetValue(pageNoContent)))
                : found.Error;
        }));

        var pages = new Command("pages", "Every live page in full, in tree order.");
        var pagesProject = Project();
        var under = new Option<string?>("--under") { Description = "Only the pages under this element (folder or page ID)." };
        var category = new Option<string?>("--category") { Description = "Only the pages of this category or of one of its sub-categories." };
        var pagesNoContent = NoContent();
        pages.Add(pagesProject);
        pages.Add(under);
        pages.Add(category);
        pages.Add(pagesNoContent);
        pages.SetAction(parse => Read<PagesDocument>(parse, pagesProject, content =>
        {
            ElementId? underId = null;
            ElementId? categoryId = null;
            if (parse.GetValue(under) is { } u)
            {
                var found = Find(content, u, null, null);
                if (!found.IsSuccess)
                {
                    return found.Error;
                }

                underId = found.Value;
            }

            if (parse.GetValue(category) is { } c)
            {
                var found = Find(content, c, ElementKind.Category, CliErrors.NotACategory);
                if (!found.IsSuccess)
                {
                    return found.Error;
                }

                categoryId = found.Value;
            }

            var withContent = !parse.GetValue(pagesNoContent);
            return new PagesDocument(Contract.Version, content.Project, [.. content.Pages(underId, categoryId).Select(id => content.Page(id, withContent))]);
        }));

        var schema = new Command("schema", "Categories (with the values their pages inherit), property definitions, link types.");
        var schemaProject = Project();
        schema.Add(schemaProject);
        schema.SetAction(parse => Read<SchemaDocument>(parse, schemaProject, content => content.Schema()));

        var links = new Command("links", "Typed links between pages and [[ID]] references of every page: the graph of the project.");
        var linksProject = Project();
        links.Add(linksProject);
        links.SetAction(parse => Read<LinksDocument>(parse, linksProject, content => content.LinksDocument()));

        var attachments = new Command("attachments", "Metadata of every live attachment.");
        var attachmentsProject = Project();
        attachments.Add(attachmentsProject);
        attachments.SetAction(parse => Read<AttachmentsDocument>(parse, attachmentsProject, content => content.AttachmentsDocument()));

        var attachment = new Command("attachment", "Raw content of one attachment, on the standard output or in a file.");
        var attachmentProject = Project();
        var attachmentId = Id("ID of the attachment.");
        var attachmentOutput = new Option<FileInfo?>("--output", "-o") { Description = "Destination file (overwritten) instead of the standard output." };
        attachment.Add(attachmentProject);
        attachment.Add(attachmentId);
        attachment.Add(attachmentOutput);
        attachment.SetAction(parse => Open(parse, attachmentProject, content =>
        {
            var found = Find(content, parse.GetRequiredValue(attachmentId), ElementKind.Attachment, CliErrors.NotAnAttachment);
            if (!found.IsSuccess)
            {
                return Fail(found.Error, ElementNotFound);
            }

            using var source = content.FindAttachment(found.Value) is { } record ? content.OpenAttachment(record) : null;
            if (source is null)
            {
                return Fail(new DomainError(CliErrors.ContentMissing, $"The content of {found.Value} is missing from the project (run erable doctor)."), OutputError);
            }

            try
            {
                using var target = parse.GetValue(attachmentOutput) is { } file ? file.Create() : standardOutput();
                source.CopyTo(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Fail(new DomainError("output.writeFailed", ex.Message), OutputError);
            }

            return Success;
        }));

        var export = new Command("export", "Write the whole project to a folder: JSON documents, pages, attachments, optional Markdown.");
        var exportProject = Project();
        var exportOutput = new Option<DirectoryInfo>("--output", "-o") { Description = "Destination folder (created if missing).", Required = true };
        var markdown = new Option<bool>("--markdown") { Description = "Also write each page as Markdown with front matter, [[ID]] as relative links." };
        var noAttachments = new Option<bool>("--no-attachments") { Description = "Leave out the content of the attachments." };
        var force = new Option<bool>("--force") { Description = "Replace a previous export in a non-empty folder (only the files an export writes)." };
        export.Add(exportProject);
        export.Add(exportOutput);
        export.Add(markdown);
        export.Add(noAttachments);
        export.Add(force);
        export.SetAction(parse => Open(parse, exportProject, content =>
        {
            Result<ExportDocument> result;
            try
            {
                result = FolderExport.Run(
                    content, parse.GetRequiredValue(exportOutput).FullName, parse.GetValue(markdown), !parse.GetValue(noAttachments), parse.GetValue(force));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Fail(new DomainError("output.writeFailed", ex.Message), OutputError);
            }

            if (!result.IsSuccess)
            {
                return Fail(result.Error, OutputError);
            }

            Print(result.Value, parse.GetValue(pretty));
            return Success;
        }));

        var root = new RootCommand(
            $"erable-cli {ProjectContent.ToolVersion} — reads an Erable project and prints it as JSON ({Contract.Version}). Never writes the project; works while it is open.")
        {
            info, tree, page, pages, schema, links, attachments, attachment, export,
        };
        root.Add(pretty);
        return root.Parse(args).InvokeAsync(new InvocationConfiguration { Output = output, Error = error });

        // Opens a snapshot of the project, runs the command, tells what failed.
        int Open(ParseResult parse, Argument<DirectoryInfo> project, Func<ProjectContent, int> run)
        {
            var opened = ProjectContent.Open(parse.GetRequiredValue(project).FullName);
            if (!opened.IsSuccess)
            {
                return Fail(opened.Error, ProjectUnreadable);
            }

            using var content = opened.Value;
            return run(content);
        }

        // A query: one document, or an element error.
        int Read<T>(ParseResult parse, Argument<DirectoryInfo> project, Func<ProjectContent, Result<T>> query) => Open(parse, project, content =>
        {
            var document = query(content);
            if (!document.IsSuccess)
            {
                return Fail(document.Error, ElementNotFound);
            }

            Print(document.Value, parse.GetValue(pretty));
            return Success;
        });

        void Print<T>(T document, bool indented)
        {
            output.Write(JsonSerializer.Serialize(document, indented ? Contract.Indented : Contract.Compact));
            output.Write('\n');
        }

        int Fail(DomainError problem, int code)
        {
            error.Write(JsonSerializer.Serialize(new ErrorDocument(new ErrorDto(problem.Code, problem.Message)), Contract.Compact));
            error.Write('\n');
            return code;
        }
    }

    /// <summary>A live element of <paramref name="kind"/> (any kind when null) from its ID as typed (case ignored).</summary>
    private static Result<ElementId> Find(ProjectContent content, string typed, ElementKind? kind, string? wrongKindCode)
    {
        if (!ElementId.TryParse(typed.Trim().ToUpperInvariant(), out var id))
        {
            return new DomainError(CliErrors.InvalidId, $"'{typed}' is not an element ID (16 hexadecimal characters).");
        }

        if (content.Find(id) is not { } node)
        {
            return new DomainError(CliErrors.ElementNotFound, string.Create(CultureInfo.InvariantCulture, $"No live element {id} in the project."));
        }

        if (kind is { } expected && node.Kind != expected)
        {
            return new DomainError(
                wrongKindCode!, $"{id} is a {Contract.KindName(node.Kind)}, not a {Contract.KindName(expected)}.");
        }

        return id;
    }
}
