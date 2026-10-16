using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.History;
using Erable.Core.Tree;
using Erable.Server.Projects;
using Erable.Server.Users;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

// Revision history (M3-05, F13).

/// <summary>A line of the history. <c>changes</c>: created, name, text, categories, properties, other.</summary>
public sealed record RevisionItem(
    long Revision, string Author, string AuthorName, DateTimeOffset At, IReadOnlyList<string> Changes, long? RestoredFrom);

/// <summary>A property value a revision holds.</summary>
public sealed record RevisionValue(ElementId PropertyId, string PropertyName, JsonNode? Value);

/// <summary>A revision with its state: what the page was at that moment.</summary>
public sealed record RevisionView(
    RevisionItem Entry, string Name, string? Markdown, IReadOnlyList<CategorySummary> Categories, IReadOnlyList<RevisionValue> Values);

public sealed record DiffPieceItem(string Text, bool Changed);

public sealed record DiffLineItem(int Number, string Text, IReadOnlyList<DiffPieceItem> Pieces);

/// <summary>A row of the side by side text diff; <c>old</c> is null for an added line, <c>new</c> for a removed one.</summary>
public sealed record DiffRowItem(DiffRowKind Kind, DiffLineItem? Old, DiffLineItem? New);

/// <summary>A property whose own value differs between the two revisions (null on the side where there is none).</summary>
public sealed record ValueChangeItem(ElementId PropertyId, string PropertyName, JsonNode? Old, JsonNode? New);

/// <summary>Two revisions compared: <c>from</c> is the older (F13.R2).</summary>
public sealed record RevisionDiffView(
    RevisionItem From,
    RevisionItem To,
    bool NameChanged,
    string FromName,
    string ToName,
    IReadOnlyList<DiffRowItem> Text,
    IReadOnlyList<CategorySummary> CategoriesAdded,
    IReadOnlyList<CategorySummary> CategoriesRemoved,
    IReadOnlyList<ValueChangeItem> Values);

/// <summary>A restoration: the new revision, and what could not come back because it was deleted since.</summary>
public sealed record RevisionRestoredView(long Revision, int SkippedCategories, int SkippedValues);

/// <summary>Revisions of an element: list, one revision, comparison, restoration (docs/architecture.md §8.1).</summary>
public static class HistoryEndpoints
{
    public static RouteGroupBuilder MapHistoryEndpoints(this RouteGroupBuilder project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var revisions = project.MapGroup("/elements/{id}/revisions").WithTags("History");
        revisions.MapGet("/", List).WithName("ListRevisions");
        revisions.MapGet("/diff", Diff).WithName("DiffRevisions");
        revisions.MapGet("/{revision:long}", Get).WithName("GetRevision");
        revisions.MapPost("/{revision:long}/restore", Restore).WithName("RestoreRevision");
        return project;
    }

    private static Results<Ok<RevisionItem[]>, ProblemHttpResult> List(ElementId projectId, ElementId id, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        return service.ListRevisions(id) is { } entries
            ? TypedResults.Ok(entries.Select(e => ItemOf(e, users)).ToArray())
            : Problems.ElementNotFound(id);
    }

    private static Results<Ok<RevisionView>, ProblemHttpResult> Get(
        ElementId projectId, ElementId id, long revision, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        if (service.GetRevision(id, revision) is not { } detail)
        {
            return Problems.Problem(StatusCodes.Status404NotFound, ErrorCodes.RevisionNotFound, $"Element {id} has no revision {revision}.");
        }

        var names = NamesOf(service);
        var snapshot = detail.Snapshot;
        return TypedResults.Ok(new RevisionView(
            ItemOf(detail.Entry, users),
            snapshot.Name,
            snapshot.Markdown,
            [.. (snapshot.Categories ?? []).Select(c => new CategorySummary(c, names(c)))],
            [.. (snapshot.Values ?? new Dictionary<string, JsonNode?>())
                .Select(v => (Id: ElementId.TryParse(v.Key, out var p) ? p : ElementId.None, v.Value))
                .Where(v => !v.Id.IsNone)
                .Select(v => new RevisionValue(v.Id, names(v.Id), v.Value?.DeepClone()))]));
    }

    private static Results<Ok<RevisionDiffView>, ProblemHttpResult> Diff(
        ElementId projectId, ElementId id, long from, long to, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var diff = service.DiffRevisions(id, from, to);
        if (!diff.IsSuccess)
        {
            return Problems.From(diff.Error);
        }

        var names = NamesOf(service);
        var d = diff.Value;
        static DiffLineItem? Line(DiffLine? line) =>
            line is null ? null : new DiffLineItem(line.Number, line.Text, [.. line.Pieces.Select(p => new DiffPieceItem(p.Text, p.Changed))]);
        return TypedResults.Ok(new RevisionDiffView(
            ItemOf(d.From.Entry, users),
            ItemOf(d.To.Entry, users),
            d.From.Snapshot.Name != d.To.Snapshot.Name,
            d.From.Snapshot.Name,
            d.To.Snapshot.Name,
            [.. d.Text.Select(r => new DiffRowItem(r.Kind, Line(r.Old), Line(r.New)))],
            [.. d.CategoriesAdded.Select(c => new CategorySummary(c, names(c)))],
            [.. d.CategoriesRemoved.Select(c => new CategorySummary(c, names(c)))],
            [.. d.Values.Select(v => new ValueChangeItem(v.PropertyId, names(v.PropertyId), v.Old?.DeepClone(), v.New?.DeepClone()))]));
    }

    private static async Task<Results<Ok<RevisionRestoredView>, ProblemHttpResult>> Restore(
        ElementId projectId, ElementId id, long revision, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var restored = await service.RestoreRevisionAsync(id, revision, CurrentUser.Id(http), http.RequestAborted).ConfigureAwait(false);
        return restored.IsSuccess
            ? TypedResults.Ok(new RevisionRestoredView(restored.Value.Revision, restored.Value.SkippedCategories, restored.Value.SkippedValues))
            : Problems.From(restored.Error);
    }

    private static RevisionItem ItemOf(RevisionEntry entry, UserDirectory users) => new(
        entry.Revision,
        entry.Author,
        users.NameOf(entry.Author),
        entry.At,
        [.. entry.Changes.Select(c => c.ToString().ToLowerInvariant())],
        entry.RestoredFrom);

    /// <summary>Current name of an element by ID (categories and properties of an old revision); the ID when it is gone.</summary>
    private static Func<ElementId, string> NamesOf(ProjectService service)
    {
        var tree = service.LoadTree();
        return id => tree.Find(id)?.Name ?? service.GetElement(id)?.Element.Name ?? id.ToString();
    }
}
