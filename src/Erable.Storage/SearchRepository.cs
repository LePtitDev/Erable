using System.Text;
using System.Text.Json;
using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Properties;
using Erable.Core.Search;
using Erable.Core.Tree;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>A run of text of a title or an excerpt; <see cref="Hit"/> when it is a passage that matched (never HTML).</summary>
public sealed record TextSegment(string Text, bool Hit);

/// <summary>A rule an effective value of a page breaks (« hors règles »).</summary>
public sealed record SearchIssue(ElementId PropertyId, string PropertyName, string RuleOp, string RuleJson, int? ItemIndex);

/// <summary>One result: the element, where it is, the matching passages, its categories and, for a page, its violations.</summary>
public sealed record SearchHit(
    Element Element,
    IReadOnlyList<TreeNode> Path,
    IReadOnlyList<TextSegment> Title,
    IReadOnlyList<TextSegment> Excerpt,
    IReadOnlyList<(ElementId Id, string Name)> Categories,
    IReadOnlyList<SearchIssue> Issues);

/// <summary>A page of results, the number of results of the whole search and the counters of the type facets.</summary>
public sealed record SearchResults(IReadOnlyList<SearchHit> Hits, int Total, IReadOnlyDictionary<ElementKind, int> Facets, int? NextOffset);

/// <summary>
/// SQL of the search (schema v4, M3-01): the full-text index <c>search_fts</c>, the structured filters on elements,
/// categories and resolved values, the type facets and the passages to highlight. Every value of the request is a bound
/// parameter; the words typed by the user only reach FTS5 through <see cref="FullTextExpression"/>.
/// </summary>
public static class SearchRepository
{
    // Private-use characters bracket a hit in the text FTS5 returns; they cannot come from a user's words.
    private const char HitStart = '';
    private const char HitEnd = '';
    private const int ExcerptLength = 200;

    // The name weighs more than the body in the ranking (bm25 weights, in the order of the columns of search_fts). bm25
    // also favours short rows, which would put every attachment (a name and no text) before the pages of the same name:
    // elements other than pages start a few points behind (lower is better), as in the mockup.
    private const string Relevance = """
        (bm25(search_fts, 8.0, 1.0)
            + CASE e.kind WHEN 'page' THEN 0.0 WHEN 'attachment' THEN 6.0 WHEN 'folder' THEN 4.0 ELSE 2.0 END)
        """;

    public static SearchResults Search(ProjectRepository repository, SqliteConnection connection, SearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);
        var match = FullTextExpression.Build(request.Text, request.NameOnly);
        var filter = request.Filter;

        var facets = CountByKind(connection, match, filter);
        var total = filter.Kinds.Count == 0 ? facets.Values.Sum() : facets.Where(f => filter.Kinds.Contains(f.Key)).Sum(f => f.Value);

        var parameters = new DynamicParameters();
        var where = Where(parameters, match, filter, withKinds: true);
        var order = request.Sort switch
        {
            SearchSort.Name => $"e.name COLLATE {ProjectDatabase.FoldCollation}, e.id",
            SearchSort.Modified => "e.updated_at DESC, e.id",
            SearchSort.Relevance => match is null ? "e.updated_at DESC, e.id" : $"{Relevance}, e.id",
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Sort, null),
        };
        parameters.Add("Limit", request.Limit + 1);
        parameters.Add("Offset", request.Offset);
        var ids = connection.Query<long>(
            $"SELECT e.id FROM {From(match)} WHERE {where} ORDER BY {order} LIMIT @Limit OFFSET @Offset", parameters).ToList();
        var hasMore = ids.Count > request.Limit;
        var pageIds = ids.Take(request.Limit).Select(ElementId.FromInt64).ToList();

        return new SearchResults(
            pageIds.Count == 0 ? [] : Describe(repository, connection, pageIds, match),
            total,
            facets,
            hasMore ? request.Offset + request.Limit : null);
    }

    private static string From(string? match) =>
        match is null ? "elements e" : "search_fts JOIN elements e ON e.id = search_fts.rowid";

    private static Dictionary<ElementKind, int> CountByKind(SqliteConnection connection, string? match, SearchFilter filter)
    {
        var parameters = new DynamicParameters();
        var where = Where(parameters, match, filter, withKinds: false);
        return connection.Query<(string Kind, long Count)>(
                $"SELECT e.kind, count(*) FROM {From(match)} WHERE {where} GROUP BY e.kind", parameters)
            .ToDictionary(r => StorageFormat.ParseKind(r.Kind), r => (int)r.Count);
    }

    private static string Where(DynamicParameters parameters, string? match, SearchFilter filter, bool withKinds)
    {
        var where = new StringBuilder("e.deleted_at IS NULL AND e.kind <> 'root'");
        if (match is not null)
        {
            where.Append(" AND search_fts MATCH @Match");
            parameters.Add("Match", match);
        }

        if (withKinds && filter.Kinds.Count > 0)
        {
            where.Append(" AND e.kind IN (SELECT value FROM json_each(@Kinds))");
            parameters.Add("Kinds", JsonSerializer.Serialize(filter.Kinds.Select(StorageFormat.Kind)));
        }

        if (filter.ModifiedSince is { } since)
        {
            where.Append(" AND e.updated_at >= @Since");
            parameters.Add("Since", StorageFormat.Timestamp(since));
        }

        // Categories, values and violations only exist on pages.
        if (filter.Categories.Count > 0 || filter.Conditions.Count > 0 || filter.OutOfRules)
        {
            where.Append(" AND e.kind = 'page'");
        }

        if (filter.Categories.Count > 0)
        {
            where.Append(" AND e.id IN (SELECT page_id FROM page_categories WHERE category_id IN (SELECT value FROM json_each(@Categories)))");
            parameters.Add("Categories", StructureRepository.Json(filter.Categories));
        }

        for (var i = 0; i < filter.Conditions.Count; i++)
        {
            where.Append(" AND ").Append(Condition(parameters, filter.Conditions[i], i));
        }

        if (filter.OutOfRules)
        {
            where.Append(" AND EXISTS (SELECT 1 FROM validation_issues i WHERE i.owner_id = e.id)");
        }

        return where.ToString();
    }

    /// <summary>
    /// A condition on the effective value of a property. A list holds an item per row: the condition holds when one
    /// item satisfies it (<c>idx &gt;= 0</c> leaves out the marker row of an empty list).
    /// </summary>
    private static string Condition(DynamicParameters parameters, PropertyCondition condition, int index)
    {
        var property = $"P{index}";
        var value = $"V{index}";
        parameters.Add(property, condition.PropertyId.ToInt64());
        var column = condition.Scalar switch
        {
            ScalarType.Bool or ScalarType.Int => "v_int",
            ScalarType.Float => "v_real",
            ScalarType.Ref => "v_ref",
            _ => "v_text",
        };
        string Exists(string extra) =>
            $"EXISTS (SELECT 1 FROM resolved_values r WHERE r.page_id = e.id AND r.property_id = @{property} AND r.idx >= 0{extra})";

        if (condition.Op is ConditionOp.IsSet)
        {
            return Exists(string.Empty);
        }

        if (condition.Op is ConditionOp.IsNotSet)
        {
            return "NOT " + Exists(string.Empty);
        }

        var operand = condition.Operand!;
        if (condition.Op == ConditionOp.Contains)
        {
            parameters.Add(value, TextFolding.Fold(operand.Text));
            return Exists($" AND instr({ProjectDatabase.FoldFunction}(r.v_text), @{value}) > 0");
        }

        parameters.Add(value, operand.Whole ?? operand.Real ?? (object?)operand.Text ?? operand.Ref?.ToInt64());
        return condition.Op switch
        {
            ConditionOp.Equal => Exists($" AND r.{column} = @{value}"),
            ConditionOp.NotEqual => $"{Exists(string.Empty)} AND NOT {Exists($" AND r.{column} = @{value}")}",
            ConditionOp.Less => Exists($" AND r.{column} < @{value}"),
            ConditionOp.LessOrEqual => Exists($" AND r.{column} <= @{value}"),
            ConditionOp.Greater => Exists($" AND r.{column} > @{value}"),
            ConditionOp.GreaterOrEqual => Exists($" AND r.{column} >= @{value}"),
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition.Op, null),
        };
    }

    // ─── Describing the results ─────────────────────────────────────────────

    private static List<SearchHit> Describe(ProjectRepository repository, SqliteConnection connection, IReadOnlyList<ElementId> ids, string? match)
    {
        var json = StructureRepository.Json(ids);
        var elements = repository.GetElements(ids, connection).ToDictionary(e => e.Id);
        var paths = Paths(connection, json);
        var titles = new Dictionary<long, IReadOnlyList<TextSegment>>();
        var excerpts = new Dictionary<long, IReadOnlyList<TextSegment>>();
        if (match is not null)
        {
            foreach (var (rowid, title, body) in connection.Query<(long RowId, string Title, string Body)>(
                """
                SELECT rowid, highlight(search_fts, 0, @Start, @End), snippet(search_fts, 1, @Start, @End, '…', 24)
                FROM search_fts
                WHERE search_fts MATCH @Match AND rowid IN (SELECT value FROM json_each(@Ids))
                """,
                new { Start = HitStart.ToString(), End = HitEnd.ToString(), Match = match, Ids = json }))
            {
                titles[rowid] = Segments(title);
                excerpts[rowid] = Segments(body);
            }
        }

        foreach (var (id, body) in connection.Query<(long Id, string Body)>(
            "SELECT id, body FROM search_source WHERE id IN (SELECT value FROM json_each(@Ids))", new { Ids = json }))
        {
            if (!excerpts.ContainsKey(id))
            {
                excerpts[id] = [new TextSegment(Beginning(body), false)];
            }
        }

        var categories = connection.Query<(long PageId, long Id, string Name)>(
                """
                SELECT pc.page_id, c.id, c.name
                FROM page_categories pc JOIN elements c ON c.id = pc.category_id AND c.deleted_at IS NULL
                WHERE pc.page_id IN (SELECT value FROM json_each(@Ids))
                ORDER BY pc.page_id, pc.sort_order
                """, new { Ids = json })
            .ToLookup(r => r.PageId, r => (ElementId.FromInt64(r.Id), r.Name));
        var issues = connection.Query<(long OwnerId, long PropertyId, string Name, string RuleOp, string RuleJson, long? ItemIndex)>(
                """
                SELECT i.owner_id, i.property_id, p.name, i.rule_op, i.rule_json, i.item_index
                FROM validation_issues i JOIN elements p ON p.id = i.property_id
                WHERE i.owner_id IN (SELECT value FROM json_each(@Ids))
                ORDER BY i.owner_id, i.property_id, i.rule_index, i.item_index
                """, new { Ids = json })
            .ToLookup(r => r.OwnerId, r => new SearchIssue(
                ElementId.FromInt64(r.PropertyId), r.Name, r.RuleOp, r.RuleJson, r.ItemIndex is { } n ? (int)n : null));

        return [.. ids.Select(id =>
        {
            var key = id.ToInt64();
            var element = elements[id];
            return new SearchHit(
                element,
                paths.GetValueOrDefault(key) ?? [],
                titles.GetValueOrDefault(key) ?? [new TextSegment(element.Name, false)],
                excerpts.GetValueOrDefault(key) ?? [],
                [.. categories[key]],
                [.. issues[key]]);
        })];
    }

    /// <summary>Ancestors of each element, root first, in one query.</summary>
    private static Dictionary<long, IReadOnlyList<TreeNode>> Paths(SqliteConnection connection, string idsJson) =>
        connection.Query<(long Start, long Id, long? ParentId, string Kind, string Name)>(
                """
                WITH RECURSIVE ancestors (start_id, id, parent_id, kind, name, depth) AS (
                    SELECT e.id, p.id, p.parent_id, p.kind, p.name, 0
                    FROM elements e JOIN elements p ON p.id = e.parent_id
                    WHERE e.id IN (SELECT value FROM json_each(@Ids))
                    UNION ALL
                    SELECT a.start_id, p.id, p.parent_id, p.kind, p.name, a.depth + 1
                    FROM ancestors a JOIN elements p ON p.id = a.parent_id
                )
                SELECT start_id, id, parent_id, kind, name FROM ancestors ORDER BY start_id, depth DESC
                """, new { Ids = idsJson })
            .GroupBy(r => r.Start)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TreeNode>)[.. g.Select(r => new TreeNode(
                    ElementId.FromInt64(r.Id), StorageFormat.ParseKind(r.Kind),
                    r.ParentId is { } p ? ElementId.FromInt64(p) : ElementId.None, r.Name, 0))]);

    /// <summary>Splits text returned by <c>highlight()</c>/<c>snippet()</c> into plain and matching runs.</summary>
    internal static List<TextSegment> Segments(string marked)
    {
        var segments = new List<TextSegment>();
        var current = new StringBuilder();
        var hit = false;
        foreach (var c in marked)
        {
            if (c is HitStart or HitEnd)
            {
                if (current.Length > 0)
                {
                    segments.Add(new TextSegment(current.ToString(), hit));
                    current.Clear();
                }

                hit = c == HitStart;
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            segments.Add(new TextSegment(current.ToString(), hit));
        }

        return MergeNeighbours(segments);
    }

    /// <summary>FTS5 marks word by word: « Col » + « gelé » is one passage when only blanks separate the hits.</summary>
    private static List<TextSegment> MergeNeighbours(List<TextSegment> segments)
    {
        var merged = new List<TextSegment>();
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Hit && merged.Count > 0 && merged[^1].Hit)
            {
                merged[^1] = new TextSegment(merged[^1].Text + segment.Text, true);
            }
            else if (!segment.Hit && string.IsNullOrWhiteSpace(segment.Text) && i + 1 < segments.Count && segments[i + 1].Hit && merged.Count > 0 && merged[^1].Hit)
            {
                merged[^1] = new TextSegment(merged[^1].Text + segment.Text, true);
            }
            else
            {
                merged.Add(segment);
            }
        }

        return merged;
    }

    private static string Beginning(string body)
    {
        if (body.Length <= ExcerptLength)
        {
            return body;
        }

        var cut = body.LastIndexOf(' ', ExcerptLength);
        return body[..(cut > ExcerptLength / 2 ? cut : ExcerptLength)] + "…";
    }
}
