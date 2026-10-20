using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Storage;

namespace Erable.Server.Api;

// Categories, property definitions and values. Values and rules are untagged JSON whose shape depends on the
// property type (docs/architecture.md §5.4): the SPA reads them with src/shared/properties.

/// <summary>A category by ID and name.</summary>
public sealed record CategorySummary(ElementId Id, string Name);

/// <summary>A live category with its inheritance link (pickers, cycle feedback).</summary>
public sealed record CategoryListItem(ElementId Id, string Name, ElementId? ParentCategoryId);

/// <summary>One option of an enum property.</summary>
public sealed record EnumOptionItem(string Key, string Label);

/// <summary>An option sent by the SPA; a missing key is derived from the label (new option).</summary>
public sealed record EnumOptionInput(string? Key, string Label);

/// <summary>A property definition; <c>default</c> is a value of <c>type</c>, <c>rules</c> an array of rules.</summary>
public sealed record PropertyDefinitionItem(
    ElementId Id, string Name, string Key, string Description, string Type, JsonNode? Default, JsonArray Rules, IReadOnlyList<EnumOptionItem> Options);

/// <summary>A rule a value does not satisfy; <c>index</c> for an item of a list.</summary>
public sealed record ViolationItem(JsonObject Rule, int? Index);

/// <summary>A category providing a value in a conflict (« PNJ (Non) / Montagnard (Oui) »).</summary>
public sealed record ConflictCandidateItem(ElementId CategoryId, string CategoryName, JsonNode Value);

/// <summary>What a property would be without the value the page defines (« Rétablir la valeur héritée »).</summary>
public sealed record FallbackItem(
    JsonNode? Value, ValueOrigin Origin, IReadOnlyList<CategorySummary> Sources, IReadOnlyList<ConflictCandidateItem> Candidates);

/// <summary>
/// A property of a page after inheritance (F04.R4): <c>value</c> is the effective value (null in a conflict),
/// <c>ownValue</c> the one the page defines, <c>sources</c> the categories it comes from (« via PNJ »),
/// <c>fallback</c> what it would be without <c>ownValue</c> (null when the page is the only source).
/// </summary>
public sealed record PagePropertyItem(
    PropertyDefinitionItem Property,
    JsonNode? OwnValue,
    JsonNode? Value,
    ValueOrigin Origin,
    IReadOnlyList<CategorySummary> Sources,
    IReadOnlyList<ConflictCandidateItem> Candidates,
    IReadOnlyList<ViolationItem> Violations,
    FallbackItem? Fallback);

/// <summary>Categories and resolved properties of a page.</summary>
public sealed record PageProperties(IReadOnlyList<CategorySummary> Categories, IReadOnlyList<PagePropertyItem> Properties);

/// <summary>A value the page defines itself, sent with its content.</summary>
public sealed record PagePropertyValue(ElementId PropertyId, JsonNode Value);

/// <summary>
/// What the page editor would save: its categories and the values it defines. The answer is the resolution that
/// saving would give (nothing is written); values that do not parse yet are ignored.
/// </summary>
public sealed record PagePreviewRequest(IReadOnlyList<ElementId> Categories, IReadOnlyList<PagePropertyValue> Properties);

public sealed record CreateCategoryRequest(ElementId ParentId, string Name, int? Index = null, ElementId? ParentCategoryId = null, string? Description = null);

/// <summary>Every editable field of a category (its name goes through <c>PATCH elements/{id}</c>).</summary>
public sealed record UpdateCategoryRequest(string Description, ElementId? ParentCategoryId);

/// <summary>Default value a category gives to a property.</summary>
public sealed record CategoryValueRequest(JsonNode Value);

/// <summary>
/// A row of the property table of a category: its value (own or inherited) and whether it overrides an inherited one.
/// </summary>
public sealed record CategoryPropertyItem(PropertyDefinitionItem Property, JsonNode? Value, ValueOrigin Origin, CategorySummary? Source, bool Overrides);

/// <summary>Everything the category screen needs (F07).</summary>
public sealed record CategoryView(
    ElementDetails Element,
    string Description,
    CategorySummary? Parent,
    IReadOnlyList<CategorySummary> Lineage,
    IReadOnlyList<CategorySummary> SubCategories,
    IReadOnlyList<CategoryPropertyItem> Properties,
    int PageCount,
    int PageCountWithSubCategories);

/// <summary>A page of a category, through which of its categories (« Ilsa (via Marchand) »).</summary>
public sealed record CategoryPageItem(ElementId Id, string Name, IReadOnlyList<PathItem> Path, CategorySummary Via);

public sealed record CreatePropertyRequest(
    ElementId ParentId,
    string Name,
    string Type,
    int? Index = null,
    string? Key = null,
    string? Description = null,
    JsonNode? Default = null,
    JsonArray? Rules = null,
    IReadOnlyList<EnumOptionInput>? Options = null);

/// <summary>
/// Every editable field of a definition (its name goes through <c>PATCH elements/{id}</c>). <c>replacements</c> maps
/// a removed option still in use to the option replacing it (F08.R5).
/// </summary>
public sealed record UpdatePropertyRequest(
    string Key,
    string Description,
    string Type,
    JsonNode? Default,
    JsonArray Rules,
    IReadOnlyList<EnumOptionInput> Options,
    IReadOnlyDictionary<string, string>? Replacements = null);

/// <summary>A page whose effective value breaks rules of the property.</summary>
public sealed record PropertyViolationPage(ElementId PageId, string PageName, JsonNode? Value, IReadOnlyList<ViolationItem> Violations);

/// <summary>Everything the property screen needs (F08).</summary>
public sealed record PropertyView(
    ElementDetails Element,
    PropertyDefinitionItem Definition,
    IReadOnlyList<CategorySummary> UsedBy,
    int FilledCount,
    IReadOnlyList<PropertyViolationPage> Violations);
