using System.Reflection;
using Erable.Core;
using Erable.Core.Categories;
using Erable.Core.Elements;
using Erable.Core.Markdown;
using Erable.Core.Properties;
using Erable.Core.Tree;
using Erable.Storage;
using Microsoft.Data.Sqlite;

namespace Erable.Cli;

/// <summary>
/// Everything a command reads, loaded once from a snapshot of the project (<see cref="ProjectStore.OpenSnapshot"/>):
/// the live tree and elements, the structure (categories, properties, values), links, references and attachments.
/// Page contents are read on demand. Builds the documents of <see cref="Contract"/>.
/// </summary>
internal sealed class ProjectContent : IDisposable
{
    private readonly OpenedProject _project;
    private readonly SqliteConnection _connection;
    private readonly Dictionary<ElementId, Element> _elements;
    private readonly StructureState _structure;
    private readonly Dictionary<ElementId, LinkTypeRecord> _linkTypes;
    private readonly IReadOnlyList<LinkRecord> _links;
    private readonly IReadOnlyList<AttachmentRecord> _attachments;
    private readonly ILookup<ElementId, StoredReference> _referencesBySource;
    private readonly ILookup<ElementId, StoredReference> _referencesByTarget;
    private readonly HashSet<ElementId> _deleted;

    private ProjectContent(OpenedProject project)
    {
        _project = project;
        _connection = project.Database.OpenConnection();
        Tree = project.Repository.LoadTree(_connection);
        _elements = project.Repository.GetElements([.. Tree.Nodes.Select(n => n.Id)], _connection).ToDictionary(e => e.Id);
        _structure = StructureState.Load(_connection, null, Tree);
        _linkTypes = LinkRepository.LoadTypes(_connection).Where(t => Tree.Contains(t.Id)).ToDictionary(t => t.Id);
        // A link shows when its type and both pages are live (as in the application).
        _links = [.. LinkRepository.LoadLinks(_connection).Where(l =>
            _linkTypes.ContainsKey(l.LinkTypeId) && IsLivePage(l.SourceId) && IsLivePage(l.TargetId))];
        _attachments = [.. project.Repository.GetAllAttachments(_connection).Where(a => Tree.Contains(a.Id))];
        var references = project.Repository.GetAllReferences(_connection).Where(r => Tree.Contains(r.SourceId)).ToList();
        _referencesBySource = references.ToLookup(r => r.SourceId);
        _referencesByTarget = references.ToLookup(r => r.TargetId);
        // Targets that are not live: in the trash when the database still knows them, unknown otherwise.
        var missing = references.Select(r => r.TargetId).Where(id => !Tree.Contains(id)).ToHashSet();
        _deleted = [.. project.Repository.GetElements(missing, _connection).Select(e => e.Id)];
        Project = new ProjectDto(
            project.Manifest.ProjectId, project.Manifest.Name, project.Manifest.ContentLanguage, project.Manifest.FormatVersion,
            project.Manifest.CreatedAt, project.Folder);
    }

    public ElementTree Tree { get; }

    public ProjectDto Project { get; }

    /// <summary>Version of this tool (informational version without the commit).</summary>
    public static string ToolVersion { get; } =
        typeof(ProjectContent).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public static Result<ProjectContent> Open(string folder)
    {
        var opened = ProjectStore.OpenSnapshot(folder);
        if (!opened.IsSuccess)
        {
            return opened.Error;
        }

        try
        {
            return new ProjectContent(opened.Value);
        }
        catch
        {
            opened.Value.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
        _project.Dispose();
    }

    /// <summary>Live elements, depth first from the roots, siblings in their order.</summary>
    public IEnumerable<TreeNode> Ordered()
    {
        var stack = new Stack<TreeNode>(Roots.All.Select(r => Tree.Find(r.Id)).OfType<TreeNode>().Reverse());
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in Tree.Children(node.Id).Reverse())
            {
                stack.Push(child);
            }
        }
    }

    public bool IsLive(ElementId id) => Tree.Contains(id);

    public TreeNode? Find(ElementId id) => Tree.Find(id);

    // ─── Documents ──────────────────────────────────────────────────────────

    public InfoDocument Info()
    {
        int Count(ElementKind kind) => Tree.Nodes.Count(n => n.Kind == kind);
        return new InfoDocument(
            Contract.Version,
            Project,
            new CountsDto(
                Count(ElementKind.Page), Count(ElementKind.Folder), Count(ElementKind.Category), Count(ElementKind.PropertyDefinition),
                Count(ElementKind.LinkType), _links.Count, _attachments.Count, _attachments.Sum(a => a.Size)),
            new ToolDto(ToolVersion));
    }

    public TreeDocument TreeDocument() => new(Contract.Version, Project, [.. Ordered().Select(n => Element(n.Id))]);

    public SchemaDocument Schema()
    {
        var categories = Ordered().Where(n => n.Kind == ElementKind.Category && _structure.Categories.ContainsKey(n.Id)).Select(n =>
        {
            var record = _structure.Categories[n.Id];
            return new CategoryDto(
                n.Id, n.Name, PathOf(n.Id), record.ParentCategoryId.IsNone ? null : record.ParentCategoryId, record.Description,
                [.. _structure.ValuesOf(n.Id).Where(v => _structure.Definitions.ContainsKey(v.Property))
                    .Select(v => new CategoryValueDto(v.Property, _structure.Definitions[v.Property].Key, PropertyValues.ToJson(v.Value)))]);
        });
        var properties = Ordered().Where(n => _structure.Definitions.ContainsKey(n.Id)).Select(n =>
        {
            var d = _structure.Definitions[n.Id];
            return new PropertyDto(
                d.Id, d.Name, d.Key, d.Description, d.Type.ToString(), d.Default is { } value ? PropertyValues.ToJson(value) : null,
                ValidationRules.ListToJson(d.Rules), d.Options);
        });
        var linkTypes = Ordered().Where(n => _linkTypes.ContainsKey(n.Id)).Select(n =>
        {
            var t = _linkTypes[n.Id];
            return new LinkTypeDto(t.Id, n.Name, t.Description, t.InverseName);
        });
        return new SchemaDocument(Contract.Version, Project, [.. categories], [.. properties], [.. linkTypes]);
    }

    public LinksDocument LinksDocument()
    {
        var order = Ordered().Select((n, i) => (n.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var links = _links
            .OrderBy(l => order[l.SourceId]).ThenBy(l => Tree.Get(l.LinkTypeId).Name, StringComparer.Ordinal).ThenBy(l => order[l.TargetId])
            .Select(l => new LinkDto(l.Id, l.LinkTypeId, Tree.Get(l.LinkTypeId).Name, l.SourceId, l.TargetId, l.CreatedAt, l.CreatedBy));
        var references = _referencesBySource.OrderBy(g => order[g.Key]).SelectMany(g => g)
            .Select(r => new ReferenceEdgeDto(r.SourceId, r.TargetId, r.Anchor, r.IsEmbed, StateOf(r.TargetId)));
        return new LinksDocument(Contract.Version, Project, [.. links], [.. references]);
    }

    public AttachmentsDocument AttachmentsDocument()
    {
        var order = Ordered().Select((n, i) => (n.Id, i)).ToDictionary(x => x.Id, x => x.i);
        return new AttachmentsDocument(Contract.Version, Project, [.. _attachments.OrderBy(a => order[a.Id]).Select(Attachment)]);
    }

    public AttachmentRecord? FindAttachment(ElementId id) => _attachments.FirstOrDefault(a => a.Id == id);

    public Stream? OpenAttachment(AttachmentRecord attachment) => _project.Blobs.OpenRead(attachment.Sha256);

    /// <summary>Live pages in tree order, optionally under an element and in a category (or one of its sub-categories).</summary>
    public IEnumerable<ElementId> Pages(ElementId? under = null, ElementId? category = null) =>
        Ordered()
            .Where(n => n.Kind == ElementKind.Page)
            .Where(n => under is not { } ancestor || Tree.Ancestors(n.Id).Any(a => a.Id == ancestor))
            .Where(n => category is not { } c || _structure.IsInCategory(n.Id, c))
            .Select(n => n.Id);

    public bool IsCategory(ElementId id) => _structure.Categories.ContainsKey(id);

    /// <summary>A live page in full; <paramref name="content"/> false leaves out the Markdown and the plain text.</summary>
    public PageDto Page(ElementId id, bool content = true)
    {
        var markdown = _project.Repository.GetPageContent(id, _connection) is { } stored ? stored.Markdown : string.Empty;
        var analysis = ErableMarkdown.Analyze(markdown);
        return new PageDto(
            Element(id),
            [.. _structure.CategoriesOf(id).Select(Named)],
            [.. _structure.ResolvePage(id).Select(Property)],
            content ? markdown : null,
            content ? analysis.PlainText : null,
            [.. analysis.Headings.Select(h => new HeadingDto(h.Level, h.Text, h.Anchor))],
            [.. analysis.References.Select(r => new ReferenceDto(
                r.Id, r.Anchor, r.Label, r.IsEmbed, r.Attributes is { } a ? new EmbedDto(a.Width, a.Align) : null, StateOf(r.Id),
                Tree.Contains(r.Id) ? Named(r.Id) : null))],
            [.. _referencesByTarget[id].Select(r => r.SourceId).Distinct().Select(Named)
                .OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(n => n.Id.Value)],
            Links(id),
            [.. _attachments.Where(a => a.PageId == id).Select(Attachment)]);
    }

    /// <summary>References of a page with their source offsets (Markdown export).</summary>
    public static IReadOnlyList<MarkdownReference> ReferencesOf(string markdown) => ErableMarkdown.Analyze(markdown).References;

    // ─── Parts ──────────────────────────────────────────────────────────────

    public ElementDto Element(ElementId id)
    {
        var node = Tree.Get(id);
        var element = _elements[id];
        return new ElementDto(
            id, Contract.KindName(node.Kind), node.ParentId.IsNone ? null : node.ParentId, node.Name, node.SortOrder, Tree.RootOf(id)?.Key,
            PathOf(id), element.Revision, element.CreatedAt, element.CreatedBy, element.UpdatedAt, element.UpdatedBy);
    }

    public NamedDto Named(ElementId id)
    {
        var node = Tree.Get(id);
        return new NamedDto(id, Contract.KindName(node.Kind), node.Name);
    }

    public AttachmentDto Attachment(AttachmentRecord a) =>
        new(a.Id, a.PageId, a.FileName, a.Mime, a.Size, a.Sha256, $"attachments/{a.Id}/{SafeFileName.Of(a.FileName)}");

    /// <summary>existing, deleted (in the trash) or unknown.</summary>
    public string StateOf(ElementId id) => Tree.Contains(id) ? "existing" : _deleted.Contains(id) ? "deleted" : "unknown";

    private IReadOnlyList<string> PathOf(ElementId id)
    {
        var node = Tree.Get(id);
        return node.IsRoot ? [] : [.. Tree.Ancestors(id).Where(a => !a.IsRoot).Reverse().Select(a => a.Name), node.Name];
    }

    private bool IsLivePage(ElementId id) => Tree.Find(id) is { Kind: ElementKind.Page };

    private PagePropertyDto Property(ResolvedProperty p)
    {
        var origin = p.Origin switch
        {
            ValueOrigin.Own => "own",
            ValueOrigin.Inherited => "inherited",
            ValueOrigin.Default => "default",
            ValueOrigin.Conflict => "conflict",
            _ => throw new ArgumentOutOfRangeException(nameof(p), p.Origin, null),
        };
        var sources = p.Resolution is Resolution<PropertyValue>.Inherited inherited ? inherited.Sources : [];
        var candidates = p.Resolution is Resolution<PropertyValue>.Conflict conflict
            ? conflict.Candidates.Select(c => new CandidateDto(c.CategoryId, PropertyValues.ToJson(c.Value))).ToList()
            : [];
        return new PagePropertyDto(
            p.Definition.Id, p.Definition.Key, p.Definition.Name, p.Definition.Type.ToString(),
            p.Resolution.EffectiveValue is { } value ? PropertyValues.ToJson(value) : null, origin, sources,
            p.OwnValue is { } own ? PropertyValues.ToJson(own) : null, candidates,
            [.. p.Violations.Select(v => new ViolationDto(ValidationRules.ToJson(v.Rule), v.Index))]);
    }

    private PageLinksDto Links(ElementId pageId)
    {
        PageLinkDto Side(LinkRecord link, bool outgoing)
        {
            var type = _linkTypes[link.LinkTypeId];
            var typeName = Tree.Get(type.Id).Name;
            var other = outgoing ? link.TargetId : link.SourceId;
            return new PageLinkDto(link.Id, type.Id, typeName, outgoing ? typeName : type.InverseName ?? typeName, other, Tree.Get(other).Name);
        }

        static IReadOnlyList<PageLinkDto> Ordered(IEnumerable<PageLinkDto> sides) =>
            [.. sides.OrderBy(l => l.Label, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.PageName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.LinkId.Value)];

        return new PageLinksDto(
            Ordered(_links.Where(l => l.SourceId == pageId).Select(l => Side(l, true))),
            Ordered(_links.Where(l => l.TargetId == pageId).Select(l => Side(l, false))));
    }
}

/// <summary>
/// File name usable on Windows, macOS and Linux, for the files of an export: characters forbidden somewhere
/// (<c>&lt;&gt;:"/\|?*</c>, control characters) become <c>_</c>, trailing dots and spaces go, a reserved Windows device
/// name (<c>CON</c>, <c>NUL</c>, <c>COM1</c>…) gets a leading <c>_</c>.
/// </summary>
public static class SafeFileName
{
    private static readonly HashSet<string> Reserved = new(
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).SelectMany(i => new[] { $"COM{i}", $"LPT{i}" })],
        StringComparer.OrdinalIgnoreCase);

    public static string Of(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var chars = name.Select(c => c < ' ' || "<>:\"/\\|?*".Contains(c, StringComparison.Ordinal) ? '_' : c).ToArray();
        var safe = new string(chars).TrimEnd('.', ' ');
        if (safe.Length == 0)
        {
            return "file";
        }

        var stem = safe.Split('.')[0];
        return Reserved.Contains(stem) ? "_" + safe : safe;
    }
}
