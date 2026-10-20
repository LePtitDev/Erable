using System.Text.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Merge;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>What a save that arrived late was merged with: the page as saved, and who else had changed it.</summary>
/// <param name="Markdown">The text after the merge: the client shows it instead of what it sent.</param>
/// <param name="Categories">The categories after the merge; null when the save did not carry any.</param>
/// <param name="Values">The own values after the merge (property ID → JSON); null when the save did not carry any.</param>
/// <param name="MergedWith">The authors of the revisions saved since the one this save started from.</param>
public sealed record PageMerge(
    string Markdown, IReadOnlyList<ElementId>? Categories, IReadOnlyDictionary<string, JsonNode?>? Values, IReadOnlyList<string> MergedWith);

/// <summary>A part of the merged text of a conflict, for the resolution view.</summary>
/// <param name="Kind"><c>stable</c> (the text) or <c>conflict</c> (the three versions).</param>
public sealed record MergeBlockDto(string Kind, string? Text = null, string? Base = null, string? Mine = null, string? Theirs = null);

/// <summary>
/// The categories and values the page would have if the conflicting fields were left as the other editor has them: the
/// base of the resolution (null: the save carried none of them).
/// </summary>
public sealed record MergedStructure(IReadOnlyList<ElementId>? Categories, IReadOnlyDictionary<string, JsonNode?>? Values);

/// <summary>The page as the other editor saved it: what the resolution view starts from, and the revision to save against.</summary>
public sealed record MergeCurrent(long Revision, string Markdown, IReadOnlyList<ElementId> Categories, IReadOnlyDictionary<string, JsonNode?> Values);

public sealed partial class ProjectService
{
    // ─── Concurrent saves (F14.R5) ────────────────────────────────────

    /// <summary>The stale save, merged: text, structure to write, and what to tell the client.</summary>
    private sealed record MergedSave(string Markdown, PageStructureInput? Structure, PageMerge Info);

    /// <summary>
    /// A save started from <paramref name="expectedRevision"/> of a page that has moved on. The text and the fields the
    /// two editors changed are merged against the revision they both started from (<see cref="ThreeWayMerge"/>,
    /// <see cref="FieldMerge"/>), inside the transaction of the save, so that nothing can slip in between. Without
    /// conflict the merged page is what gets saved; with one, the error carries both versions and nothing is written.
    /// </summary>
    private Result<MergedSave> MergeStaleSave(
        SqliteConnection connection, Element current, string mine, long expectedRevision, PageStructureInput? structure, string author)
    {
        var pageId = current.Id;
        var baseRevision = expectedRevision > 0 && expectedRevision < current.Revision
            ? Repository.LoadRevision(pageId, expectedRevision, connection)
            : null;
        if (baseRevision?.Snapshot.Markdown is not { } baseMarkdown)
        {
            // A revision this page never had: nothing to merge against.
            return RevisionConflict;
        }

        var theirsMarkdown = Repository.GetPageContent(pageId, connection)?.Markdown ?? string.Empty;
        var theirs = Snapshot(connection, current, theirsMarkdown);
        var text = ThreeWayMerge.Merge(baseMarkdown, mine, theirsMarkdown);

        IReadOnlyList<ElementId>? categories = null;
        if (structure?.Categories is { } mineCategories)
        {
            categories = FieldMerge.MergeSet(baseRevision.Snapshot.Categories ?? [], mineCategories, theirs.Categories ?? []);
        }

        ValuesMerge? values = null;
        if (structure?.Values is { } mineValues)
        {
            var mineJson = mineValues.ToDictionary(
                v => v.Property.ToString(),
                v => v.Value.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(v.Value.GetRawText()),
                StringComparer.Ordinal);
            values = FieldMerge.MergeValues(
                baseRevision.Snapshot.Values ?? new Dictionary<string, JsonNode?>(),
                mineJson,
                theirs.Values ?? new Dictionary<string, JsonNode?>());
        }

        var authors = Repository.RevisionAuthorsAfter(pageId, expectedRevision, connection).Where(a => a != author).Distinct().ToList();
        if (text.HasConflicts || values?.Conflicts.Count > 0)
        {
            return new DomainError(RevisionConflict.Code, RevisionConflict.Message, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["current"] = new MergeCurrent(
                    current.Revision, theirsMarkdown, theirs.Categories ?? [], theirs.Values ?? new Dictionary<string, JsonNode?>()),
                ["blocks"] = text.Blocks.Select(b => b switch
                {
                    StableBlock stable => new MergeBlockDto("stable", Text: stable.Text),
                    ConflictBlock conflict => new MergeBlockDto("conflict", Base: conflict.Base, Mine: conflict.Mine, Theirs: conflict.Theirs),
                    _ => throw new InvalidOperationException("Unknown block."),
                }).ToArray(),
                ["mergedStructure"] = new MergedStructure(categories, values?.Merged),
                ["valueConflicts"] = values?.Conflicts ?? [],
                ["mergedWith"] = authors,
            });
        }

        var merged = text.Text;
        var mergedStructure = structure is null
            ? null
            : new PageStructureInput(
                categories,
                values?.Merged.Select(kv => (ElementId.Parse(kv.Key), JsonSerializer.SerializeToElement(kv.Value))).ToArray());
        return new MergedSave(merged, mergedStructure, new PageMerge(merged, categories, values?.Merged, authors));
    }
}
