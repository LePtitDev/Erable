using System.Buffers.Text;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Search;
using Erable.Server.Projects;
using Erable.Server.Users;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Erable.Server.Api;

/// <summary>Full-text search and filters (docs/architecture.md §8.1, F10).</summary>
public static class SearchEndpoints
{
    private const string CursorPrefix = "o:";

    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder project)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.MapGet("/search", Search).WithName("Search").WithTags("Search");
        return project;
    }

    private static Results<Ok<SearchResultPage>, ProblemHttpResult> Search(
        ElementId projectId, [AsParameters] SearchQueryParameters query, ProjectWorkspace workspace, UserDirectory users, TimeProvider clock)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var conditions = new List<PropertyCondition>();
        foreach (var raw in query.Conditions ?? [])
        {
            var parsed = ParseCondition(service, raw);
            if (!parsed.IsSuccess)
            {
                return Problems.From(parsed.Error);
            }

            conditions.Add(parsed.Value);
        }

        if (!TryReadCursor(query.Cursor, out var offset))
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, ErrorCodes.SearchCursorInvalid, "The cursor is not one of this server.");
        }

        var kinds = new HashSet<ElementKind>();
        foreach (var name in query.Kinds ?? [])
        {
            if (!Enum.TryParse<ElementKind>(name, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            {
                return Problems.Problem(StatusCodes.Status400BadRequest, "request.invalidKind", $"'{name}' is not an element kind.");
            }

            kinds.Add(kind);
        }

        var categories = new List<ElementId>();
        foreach (var text in query.Categories ?? [])
        {
            if (!ElementId.TryParse(text, out var category))
            {
                return Problems.Problem(StatusCodes.Status400BadRequest, "request.invalidId", $"'{text}' is not an element ID.");
            }

            categories.Add(category);
        }

        ModifiedWindow? window = null;
        if (query.Modified is not null)
        {
            if (!Enum.TryParse<ModifiedWindow>(query.Modified, ignoreCase: true, out var parsedWindow) || !Enum.IsDefined(parsedWindow))
            {
                return Problems.Problem(StatusCodes.Status400BadRequest, "request.invalidModified", $"'{query.Modified}' is not day, week or month.");
            }

            window = parsedWindow;
        }

        var sort = SearchSort.Relevance;
        if (query.Sort is not null && (!Enum.TryParse(query.Sort, ignoreCase: true, out sort) || !Enum.IsDefined(sort)))
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, "request.invalidSort", $"'{query.Sort}' is not relevance, modified or name.");
        }

        var filter = new SearchFilter(
            kinds,
            categories,
            query.SubCategories is true,
            conditions,
            window is { } since ? clock.GetUtcNow() - ModifiedWindows.DurationOf(since) : null,
            query.OutOfRules is true);
        var limit = query.Limit is { } l ? Math.Clamp(l, 1, SearchRequest.MaxLimit) : SearchRequest.DefaultLimit;
        var results = service.Search(new SearchRequest(query.Text, query.NameOnly is true, filter, sort, offset, limit));

        var language = service.Project.Manifest.ContentLanguage;
        var attachments = service.Project.Repository
            .GetAttachments([.. results.Hits.Where(h => h.Element.Kind == ElementKind.Attachment).Select(h => h.Element.Id)])
            .ToDictionary(a => a.Id);
        return TypedResults.Ok(new SearchResultPage(
            [.. results.Hits.Select(h => new SearchHitItem(
                h.Element.Id,
                h.Element.Kind,
                h.Element.Name,
                [.. h.Path.Select(n => ProjectEndpoints.PathItemOf(n, language))],
                [.. h.Title.Select(s => new TextSegmentItem(s.Text, s.Hit))],
                [.. h.Excerpt.Select(s => new TextSegmentItem(s.Text, s.Hit))],
                [.. h.Categories.Select(c => new CategorySummary(c.Id, c.Name))],
                h.Element.UpdatedAt,
                h.Element.UpdatedBy,
                users.NameOf(h.Element.UpdatedBy),
                [.. h.Issues.Select(i => new SearchIssueItem(i.PropertyId, i.PropertyName, JsonNode.Parse(i.RuleJson)!.AsObject(), i.ItemIndex))],
                attachments.TryGetValue(h.Element.Id, out var attachment) ? AttachmentItem.From(attachment) : null))],
            results.Total,
            [.. results.Facets.OrderBy(f => f.Key).Select(f => new KindCount(f.Key, f.Value))],
            results.NextOffset is { } next ? WriteCursor(next) : null));
    }

    /// <summary><c>propertyId:operator:value</c>; the value may itself contain colons.</summary>
    private static Result<PropertyCondition> ParseCondition(ProjectService service, string raw)
    {
        var parts = raw.Split(':', 3);
        if (parts.Length < 2 || !ElementId.TryParse(parts[0], out var property) || OperatorOf(parts[1]) is not { } op)
        {
            return new DomainError(ErrorCodes.SearchConditionInvalid, $"'{raw}' is not a condition (propertyId:operator:value).");
        }

        return service.CreateCondition(property, op, parts.Length == 3 ? parts[2] : null);
    }

    private static ConditionOp? OperatorOf(string name) => name switch
    {
        "eq" => ConditionOp.Equal,
        "ne" => ConditionOp.NotEqual,
        "lt" => ConditionOp.Less,
        "le" => ConditionOp.LessOrEqual,
        "gt" => ConditionOp.Greater,
        "ge" => ConditionOp.GreaterOrEqual,
        "contains" => ConditionOp.Contains,
        "set" => ConditionOp.IsSet,
        "unset" => ConditionOp.IsNotSet,
        _ => null,
    };

    // The cursor is opaque for the client: today it carries the position of the next result.
    private static string WriteCursor(int offset) =>
        Base64Url.EncodeToString(Encoding.ASCII.GetBytes(CursorPrefix + offset.ToString(CultureInfo.InvariantCulture)));

    private static bool TryReadCursor(string? cursor, out int offset)
    {
        offset = 0;
        if (string.IsNullOrEmpty(cursor))
        {
            return true;
        }

        try
        {
            var text = Encoding.ASCII.GetString(Base64Url.DecodeFromChars(cursor));
            return text.StartsWith(CursorPrefix, StringComparison.Ordinal)
                && int.TryParse(text.AsSpan(CursorPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out offset);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
