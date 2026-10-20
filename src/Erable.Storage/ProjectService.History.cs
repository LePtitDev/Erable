using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.History;
using Erable.Core.Properties;

namespace Erable.Storage;

/// <summary>What a revision changed compared with the one before it.</summary>
public enum RevisionChange
{
    /// <summary>The first revision of an element.</summary>
    Created,
    Name,
    Text,
    Categories,
    Properties,

    /// <summary>The element changed place in the tree, or other fields (a category, a definition…).</summary>
    Other,
}

/// <summary>A line of the history: who saved, when, what changed (F13.R1). <see cref="RestoredFrom"/> for a restoration (F13.R3).</summary>
public sealed record RevisionEntry(long Revision, string Author, DateTimeOffset At, IReadOnlyList<RevisionChange> Changes, long? RestoredFrom);

/// <summary>A revision with its whole state.</summary>
public sealed record RevisionDetail(RevisionEntry Entry, RevisionSnapshot Snapshot);

/// <summary>A value of a property that differs between two revisions; null on the side where the page has none.</summary>
public sealed record ValueChange(ElementId PropertyId, JsonNode? Old, JsonNode? New);

/// <summary>The comparison of two revisions of a page: text line by line, categories, own values (F13.R2).</summary>
public sealed record RevisionDiff(
    RevisionDetail From,
    RevisionDetail To,
    IReadOnlyList<DiffRow> Text,
    IReadOnlyList<ElementId> CategoriesAdded,
    IReadOnlyList<ElementId> CategoriesRemoved,
    IReadOnlyList<ValueChange> Values);

/// <summary>A restoration: the new revision, and what of the old state could not come back (deleted since).</summary>
public sealed record RevisionRestored(long Revision, int SkippedCategories, int SkippedValues);

public sealed partial class ProjectService
{
    // ─── History (F13) ───────────────────────────────────────────────

    /// <summary>The history of an element, newest first; null when the element does not exist (deleted ones have one too).</summary>
    public IReadOnlyList<RevisionEntry>? ListRevisions(ElementId id)
    {
        using var connection = Project.Database.OpenConnection();
        if (Repository.GetElement(id, connection) is null)
        {
            return null;
        }

        return [.. Entries(Repository.LoadRevisions(id, connection)).OrderByDescending(e => e.Revision)];
    }

    /// <summary>One revision with its state; null when the element or the revision does not exist.</summary>
    public RevisionDetail? GetRevision(ElementId id, long revision)
    {
        using var connection = Project.Database.OpenConnection();
        return Details(Repository.LoadRevisions(id, connection)).FirstOrDefault(d => d.Entry.Revision == revision);
    }

    /// <summary>Compares two revisions of an element (any order: the older one is « from »).</summary>
    public Result<RevisionDiff> DiffRevisions(ElementId id, long from, long to)
    {
        using var connection = Project.Database.OpenConnection();
        var details = Details(Repository.LoadRevisions(id, connection)).ToDictionary(d => d.Entry.Revision);
        if (!details.TryGetValue(Math.Min(from, to), out var older) || !details.TryGetValue(Math.Max(from, to), out var newer))
        {
            return new DomainError(ErrorCodes.RevisionNotFound, $"Element {id} has no such revision.");
        }

        var before = older.Snapshot;
        var after = newer.Snapshot;
        var oldCategories = before.Categories ?? [];
        var newCategories = after.Categories ?? [];
        var oldValues = before.Values ?? new Dictionary<string, JsonNode?>();
        var newValues = after.Values ?? new Dictionary<string, JsonNode?>();
        var values = oldValues.Keys.Union(newValues.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Where(key => !JsonNode.DeepEquals(oldValues.GetValueOrDefault(key), newValues.GetValueOrDefault(key)))
            .Select(key => new ValueChange(ElementId.Parse(key), oldValues.GetValueOrDefault(key), newValues.GetValueOrDefault(key)));

        return new RevisionDiff(
            older,
            newer,
            TextDiff.Compare(before.Markdown, after.Markdown),
            [.. newCategories.Except(oldCategories)],
            [.. oldCategories.Except(newCategories)],
            [.. values]);
    }

    /// <summary>
    /// Brings back the content, categories and own values of a past revision of a page as a <b>new</b> revision marked
    /// « restoration of revision N »: the history is never rewritten (F13.R3). A category or property deleted since, or a
    /// value that no longer fits its property, is left out and counted. The name and place of the page stay as they are.
    /// </summary>
    public Task<Result<RevisionRestored>> RestoreRevisionAsync(
        ElementId id, long revision, string author, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var element = Repository.GetElement(id, connection);
            if (element is not { Kind: ElementKind.Page, DeletedAt: null })
            {
                return Result.Failure<RevisionRestored>(new DomainError(ErrorCodes.RevisionNotRestorable, $"Page {id} cannot be restored."));
            }

            var target = Repository.LoadRevisions(id, connection).FirstOrDefault(r => r.Revision == revision);
            if (target?.Snapshot.Markdown is not { } markdown)
            {
                return Result.Failure<RevisionRestored>(new DomainError(ErrorCodes.RevisionNotFound, $"Page {id} has no revision {revision}."));
            }

            var state = StructureState.Load(connection, []);
            var wantedCategories = (target.Snapshot.Categories ?? []).Distinct().ToList();
            var categories = wantedCategories.Where(state.Categories.ContainsKey).ToList();
            var values = new List<(ElementId, PropertyValue)>();
            var skippedValues = 0;
            foreach (var (key, json) in target.Snapshot.Values ?? new Dictionary<string, JsonNode?>())
            {
                if (json is not null && ElementId.TryParse(key, out var property)
                    && state.Definitions.TryGetValue(property, out var definition)
                    && PropertyValues.FromJson(System.Text.Json.JsonSerializer.SerializeToElement(json), definition.Type, definition.Options) is { IsSuccess: true } parsed)
                {
                    values.Add((property, parsed.Value));
                }
                else
                {
                    skippedValues++;
                }
            }

            StructureRepository.ReplacePageCategories(connection, transaction, id, categories);
            StructureRepository.ReplaceValues(connection, transaction, id, values);
            var now = clock.GetUtcNow();
            WriteContent(connection, transaction, id, markdown);
            StructureState.Load(connection, [id]).WriteDerived(connection, transaction, [id], null);
            var newRevision = ProjectRepository.Touch(connection, transaction, id, now, author);
            InsertRevision(connection, transaction, element with { Revision = newRevision, UpdatedAt = now, UpdatedBy = author }, markdown, author, now, restoredFrom: revision);
            return Result.Success(new RevisionRestored(newRevision, wantedCategories.Count - categories.Count, skippedValues));
        }, cancellationToken);

    private static List<RevisionEntry> Entries(IReadOnlyList<StoredRevision> revisions) =>
        [.. Details(revisions).Select(d => d.Entry)];

    private static List<RevisionDetail> Details(IReadOnlyList<StoredRevision> revisions)
    {
        var details = new List<RevisionDetail>(revisions.Count);
        RevisionSnapshot? previous = null;
        foreach (var revision in revisions)
        {
            var entry = new RevisionEntry(revision.Revision, revision.Author, revision.At, ChangesOf(previous, revision.Snapshot), revision.Snapshot.RestoredFrom);
            details.Add(new RevisionDetail(entry, revision.Snapshot));
            previous = revision.Snapshot;
        }

        return details;
    }

    private static List<RevisionChange> ChangesOf(RevisionSnapshot? before, RevisionSnapshot after)
    {
        if (before is null)
        {
            return [RevisionChange.Created];
        }

        var changes = new List<RevisionChange>();
        if (before.Name != after.Name)
        {
            changes.Add(RevisionChange.Name);
        }

        if (before.Markdown != after.Markdown)
        {
            changes.Add(RevisionChange.Text);
        }

        if (!(before.Categories ?? []).SequenceEqual(after.Categories ?? []))
        {
            changes.Add(RevisionChange.Categories);
        }

        var oldValues = before.Values ?? new Dictionary<string, JsonNode?>();
        var newValues = after.Values ?? new Dictionary<string, JsonNode?>();
        if (oldValues.Count != newValues.Count || oldValues.Any(v => !newValues.TryGetValue(v.Key, out var other) || !JsonNode.DeepEquals(v.Value, other)))
        {
            changes.Add(RevisionChange.Properties);
        }

        if (changes.Count == 0)
        {
            changes.Add(RevisionChange.Other);
        }

        return changes;
    }
}
