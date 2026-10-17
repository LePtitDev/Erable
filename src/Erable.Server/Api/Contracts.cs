using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Attachments;
using Erable.Core.Elements;
using Erable.Storage;

namespace Erable.Server.Api;

// Request and response bodies of /api/v1. They are the API contract: the TypeScript client is generated from them
// (CLAUDE.md §6.9). Element IDs are always strings in JSON (ElementIdJsonConverter).

/// <summary>An open project, with the role of the caller in it.</summary>
public sealed record ProjectSummary(
    ElementId Id, string Name, string Folder, string ContentLanguage, int FormatVersion, DateTimeOffset CreatedAt, ProjectRole MyRole);

/// <param name="Folder">Where to create the project; omitted on a server, which hosts it under its own data folder.</param>
public sealed record CreateProjectRequest(string? Folder, string Name, string ContentLanguage);

/// <param name="ConfirmMigration">The user accepted the migration of an older project (F01.R3, M2-09).</param>
public sealed record OpenProjectRequest(string Folder, bool ConfirmMigration = false);

public sealed record InspectFolderRequest(string Folder);

/// <summary>What a folder is, read without writing anything (F01.R1, F01.R3).</summary>
public sealed record FolderInspection(FolderStatus Status, string? Name, ElementId? ProjectId, int? FormatVersion);

/// <summary>A project of the recent list, with the current state of its folder (F01.R2).</summary>
public sealed record RecentProjectItem(
    ElementId ProjectId, string Name, string Folder, DateTimeOffset LastOpenedAt, ElementId? LastElementId, FolderStatus Status);

public sealed record RecordLastElementRequest(ElementId ElementId);

/// <summary>One node of the explorer (attachments excluded).</summary>
public sealed record TreeNodeItem(ElementId Id, ElementKind Kind, ElementId? ParentId, string Name, long SortOrder, bool DuplicateName);

/// <summary>Number of elements under each root, folders excluded (E2E-02-08).</summary>
public sealed record RootCounts(int Pages, int Categories, int Properties, int Links);

public sealed record ProjectTree(IReadOnlyList<TreeNodeItem> Nodes, RootCounts Counts);

/// <summary>One step of a breadcrumb: root first, parent last.</summary>
public sealed record PathItem(ElementId Id, ElementKind Kind, string Name);

public sealed record ElementDetails(
    ElementId Id,
    ElementKind Kind,
    ElementId? ParentId,
    string Name,
    long Revision,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    bool Deleted,
    IReadOnlyList<PathItem> Path,
    string CreatedByName,
    string UpdatedByName);

/// <summary>The user making the request, and where their new projects go by default.</summary>
public sealed record CurrentUserInfo(string Id, string Name, string DefaultProjectsFolder, bool IsServerAdmin);

/// <summary>The name a user is shown under (home screen, authors, presence); 1 to 80 characters.</summary>
public sealed record RenameMeRequest(string Name);

/// <summary>Rename (<c>name</c>), move (<c>parentId</c>) and/or reorder (<c>index</c> among the new siblings).</summary>
public sealed record UpdateElementRequest(string? Name = null, ElementId? ParentId = null, int? Index = null);

public sealed record CreateFolderRequest(ElementId ParentId, string Name, int? Index = null);

public sealed record CreatePageRequest(ElementId ParentId, string Name, int? Index = null, string? Markdown = null);

public sealed record ChildPage(ElementId Id, string Name);

/// <summary>An attachment and how the SPA presents it (F06.R2); its content is at <c>attachments/{id}</c>.</summary>
public sealed record AttachmentItem(ElementId Id, ElementId PageId, string FileName, string Mime, long Size, AttachmentDisplay Display)
{
    public static AttachmentItem From(AttachmentRecord a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return new(a.Id, a.PageId, a.FileName, a.Mime, a.Size, MediaTypes.DisplayOf(a.Mime));
    }
}

/// <summary>Everything the page view needs in one call (F04): content, sub-pages, attachments, categories, resolved properties.</summary>
public sealed record PageView(
    ElementDetails Element,
    string Markdown,
    IReadOnlyList<ChildPage> SubPages,
    IReadOnlyList<AttachmentItem> Attachments,
    IReadOnlyList<CategorySummary> Categories,
    IReadOnlyList<PagePropertyItem> Properties);

public sealed record PageContent(string Markdown, long Revision);

/// <summary>
/// The Markdown of a page; when given, its categories and every value it defines (full replacement), saved in the
/// same revision (M2-06).
/// </summary>
public sealed record SavePageContentRequest(string Markdown, IReadOnlyList<ElementId>? Categories = null, IReadOnlyList<PagePropertyValue>? Properties = null);

/// <summary>A user, by login and display name.</summary>
public sealed record AuthorItem(string Id, string Name);

/// <summary>
/// A save that arrived after another one and was merged with it (M4-05): the page as it is now, which the editor shows
/// instead of what it sent, and whom it was merged with (« Fusionné avec les modifications de Camille R. »).
/// </summary>
/// <param name="Categories">Null when the save carried no categories.</param>
/// <param name="Properties">Null when the save carried no values.</param>
public sealed record PageMergedInfo(
    string Markdown,
    IReadOnlyList<ElementId>? Categories,
    IReadOnlyList<PagePropertyValue>? Properties,
    IReadOnlyList<AuthorItem> MergedWith);

public sealed record PageSavedResult(long Revision, DateTimeOffset UpdatedAt, string UpdatedBy, PageMergedInfo? Merged = null);

/// <summary>Current state of a referenced ID; <c>attachment</c> is set for attachments (embeds, F06.R2).</summary>
public sealed record ResolvedItem(
    ElementId Id, ResolvedState State, ElementKind? Kind, string? Name, IReadOnlyList<PathItem> Path, AttachmentItem? Attachment = null);

public sealed record ResolveResult(IReadOnlyList<ResolvedItem> Items);

/// <summary>An element whose content references the target, with how many times and whether as an embed.</summary>
public sealed record BacklinkItem(ElementId SourceId, ElementKind SourceKind, string SourceName, IReadOnlyList<PathItem> Path, int Count, bool Embedded);
