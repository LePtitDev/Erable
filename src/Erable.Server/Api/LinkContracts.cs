using Erable.Core;

namespace Erable.Server.Api;

// Link types and typed links between pages (F09).

/// <summary>A live link type with the number of links using it (confirmation before deleting it, F09.R3).</summary>
public sealed record LinkTypeItem(ElementId Id, string Name, string Description, string? InverseName, int LinkCount);

/// <summary>Everything the link type screen needs.</summary>
public sealed record LinkTypeView(ElementDetails Element, string Description, string? InverseName, int LinkCount);

public sealed record CreateLinkTypeRequest(ElementId ParentId, string Name, int? Index = null, string? Description = null, string? InverseName = null);

/// <summary>Every editable field of a link type (its name goes through <c>PATCH elements/{id}</c>); a blank inverse label means none.</summary>
public sealed record UpdateLinkTypeRequest(string Description, string? InverseName);

/// <summary>
/// A link seen from a page: <c>label</c> is the type name for an outgoing link and the inverse label (or the name when
/// there is none) for an incoming one; <c>pageId</c> is the page at the other end.
/// </summary>
public sealed record PageLinkItem(ElementId Id, ElementId LinkTypeId, string TypeName, string Label, ElementId PageId, string PageName);

/// <summary>Typed links of a page, outgoing and incoming (implicit references stay in <c>backlinks</c>, E2E-09-03).</summary>
public sealed record PageLinksView(IReadOnlyList<PageLinkItem> Outgoing, IReadOnlyList<PageLinkItem> Incoming);

public sealed record CreateLinkRequest(ElementId SourceId, ElementId LinkTypeId, ElementId TargetId);

public sealed record LinkItem(ElementId Id, ElementId SourceId, ElementId LinkTypeId, ElementId TargetId);
