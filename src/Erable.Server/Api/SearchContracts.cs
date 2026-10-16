using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Search;
using Microsoft.AspNetCore.Mvc;

namespace Erable.Server.Api;

// Search (M3-01, F10). The whole state of a search is in the query string, so that the SPA can mirror it in its URL
// (F10.R4): a search is a link.

/// <summary>
/// Query of <c>GET search</c>. <c>q</c> is the text; <c>nameOnly</c> restricts it to names (quick search). Every filter
/// narrows the result (AND); <c>modified</c> is <c>day</c>, <c>week</c> or <c>month</c>, <c>sort</c> is <c>relevance</c>, <c>modified</c> or <c>name</c>; the values of a repeated parameter are alternatives (OR). <c>where</c> is
/// <c>propertyId:operator:value</c> with operator <c>eq</c>, <c>ne</c>, <c>lt</c>, <c>le</c>, <c>gt</c>, <c>ge</c>,
/// <c>contains</c>, <c>set</c> or <c>unset</c> (no value for the last two).
/// </summary>
public sealed record SearchQueryParameters
{
    [FromQuery(Name = "q")]
    public string? Text { get; init; }

    [FromQuery(Name = "nameOnly")]
    public bool? NameOnly { get; init; }

    [FromQuery(Name = "kind")]
    public string[]? Kinds { get; init; }

    [FromQuery(Name = "category")]
    public string[]? Categories { get; init; }

    [FromQuery(Name = "subCategories")]
    public bool? SubCategories { get; init; }

    [FromQuery(Name = "where")]
    public string[]? Conditions { get; init; }

    [FromQuery(Name = "modified")]
    public string? Modified { get; init; }

    [FromQuery(Name = "outOfRules")]
    public bool? OutOfRules { get; init; }

    [FromQuery(Name = "sort")]
    public string? Sort { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }
}

/// <summary>A run of text of a title or an excerpt. The SPA renders <c>hit</c> runs with <c>&lt;mark&gt;</c>; it is never HTML.</summary>
public sealed record TextSegmentItem(string Text, bool Hit);

/// <summary>A rule the effective value of a property breaks on a page (filter « hors règles »).</summary>
public sealed record SearchIssueItem(ElementId PropertyId, string PropertyName, JsonObject Rule, int? Index);

/// <summary>One result. <c>attachment</c> is set for an attachment (type, size).</summary>
public sealed record SearchHitItem(
    ElementId Id,
    ElementKind Kind,
    string Name,
    IReadOnlyList<PathItem> Path,
    IReadOnlyList<TextSegmentItem> Title,
    IReadOnlyList<TextSegmentItem> Excerpt,
    IReadOnlyList<CategorySummary> Categories,
    DateTimeOffset UpdatedAt,
    string UpdatedBy,
    string UpdatedByName,
    IReadOnlyList<SearchIssueItem> Issues,
    AttachmentItem? Attachment);

/// <summary>Number of results of a type, the other filters applied but not the type filter itself.</summary>
public sealed record KindCount(ElementKind Kind, int Count);

/// <summary>A page of results; <c>nextCursor</c> is null on the last page.</summary>
public sealed record SearchResultPage(IReadOnlyList<SearchHitItem> Hits, int Total, IReadOnlyList<KindCount> Facets, string? NextCursor);
