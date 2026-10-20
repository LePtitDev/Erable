using Erable.Core;
using Erable.Core.Elements;

namespace Erable.Server.Api;

// Deletion and trash (F12).

/// <summary>An element, by ID and name.</summary>
public sealed record ImpactItemView(ElementId Id, string Name);

/// <summary>What deleting an element would take with it, to show before the user confirms (F12.R2).</summary>
public sealed record DeleteImpactView(
    ElementId Id,
    ElementKind Kind,
    string Name,
    IReadOnlyList<ImpactItemView> ReferencedBy,
    int SubPages,
    int Descendants,
    int Links,
    int Pages,
    int Values);

/// <summary>One deletion in the trash: <c>descendants</c> came along, <c>path</c> is where it was.</summary>
public sealed record TrashEntryView(
    ElementId Id,
    ElementKind Kind,
    string Name,
    DateTimeOffset DeletedAt,
    string DeletedBy,
    int Descendants,
    IReadOnlyList<PathItem> Path,
    bool ParentAvailable);

/// <summary>An entry brought back; <c>relocated</c> when its parent was gone and it went to <c>parentName</c> (F12.R3).</summary>
public sealed record RestoreView(ElementId Id, ElementKind Kind, string Name, ElementId ParentId, string ParentName, bool Relocated, int Restored);

/// <summary>How many entries were removed for good.</summary>
public sealed record PurgeView(int Removed);
