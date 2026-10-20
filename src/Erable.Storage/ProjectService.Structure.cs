using System.Text.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Categories;
using Erable.Core.Elements;
using Erable.Core.Properties;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>Categories and own values of a page, saved with its content (same revision). Null fields are left unchanged.</summary>
/// <param name="Categories">Every category of the page, in order.</param>
/// <param name="Values">Every value the page defines itself (JSON of the property type); others are removed.</param>
public sealed record PageStructureInput(IReadOnlyList<ElementId>? Categories, IReadOnlyList<(ElementId Property, JsonElement Value)>? Values);

/// <summary>Editable fields of a category (its name goes through the tree).</summary>
public sealed record CategoryInput(string Description, ElementId ParentCategoryId);

/// <summary>Editable fields of a property definition (its name goes through the tree).</summary>
/// <param name="Key">Null: derived from the name (creation) or unchanged (update).</param>
/// <param name="Description">Plain text.</param>
/// <param name="Type"><c>int</c>, <c>list&lt;string&gt;</c>…</param>
/// <param name="Default">JSON of the type, or null.</param>
/// <param name="Rules">JSON array of rules, or null for none.</param>
/// <param name="Options">Enum options; a null key is derived from the label.</param>
/// <param name="Replacements">Removed option key → option that replaces it in the values using it (F08.R5).</param>
public sealed record PropertyInput(
    string? Key,
    string Description,
    string Type,
    JsonElement? Default,
    JsonElement? Rules,
    IReadOnlyList<(string? Key, string Label)> Options,
    IReadOnlyDictionary<string, string>? Replacements = null);

/// <summary>A page whose value blocks an operation on a property (type change, option removal).</summary>
public sealed record BlockingValue(ElementId OwnerId, string OwnerName, ElementKind OwnerKind, string Value);

public sealed partial class ProjectService
{
    public const int MaxCategoryDescription = 2_000;
    public const int MaxPageCategories = 50;

    // ─── Reads ──────────────────────────────────────────────────────────────

    /// <summary>The structure state for some pages (all when null), on a new connection.</summary>
    public StructureState LoadStructure(IReadOnlyCollection<ElementId>? pages)
    {
        using var connection = Project.Database.OpenConnection();
        return StructureState.Load(connection, pages);
    }

    /// <summary>Pages of a category, with or without its sub-categories (F07.R4), « via » their most specific category.</summary>
    public IReadOnlyList<CategoryMember> GetCategoryPages(ElementId categoryId, bool includeSubCategories)
    {
        using var connection = Project.Database.OpenConnection();
        var state = StructureState.Load(connection, null);
        var pages = state.LivePages(null).Select(p => (p, state.CategoriesOf(p)));
        return CategoryMembership.PagesOf(state.Graph, categoryId, includeSubCategories, pages);
    }

    /// <summary>Live definitions, by name.</summary>
    public IReadOnlyList<PropertyDefinition> ListDefinitions()
    {
        using var connection = Project.Database.OpenConnection();
        var tree = Repository.LoadTree(connection);
        return [.. StructureRepository.LoadDefinitions(connection).Where(d => tree.Contains(d.Id))
            .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(d => d.Id.Value)];
    }

    // ─── Categories ─────────────────────────────────────────────────────────

    public Task<Result<Element>> CreateCategoryAsync(
        ElementId parentId, string name, int? index, CategoryInput input, string author, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Project.Database.WriteAsync((connection, transaction) =>
        {
            if (CheckDescription(input.Description) is { } invalid)
            {
                return Result.Failure<Element>(invalid);
            }

            var tree = Repository.LoadTree(connection);
            var state = StructureState.Load(connection, [], tree);
            var id = NewUniqueId(connection, transaction);
            var created = tree.Create(id, ElementKind.Category, parentId, name, index);
            if (!created.IsSuccess)
            {
                return Result.Failure<Element>(created.Error);
            }

            var graph = state.Graph;
            if (graph.Add(id, input.ParentCategoryId) is { IsSuccess: false } refused)
            {
                return Result.Failure<Element>(refused.Error);
            }

            var now = clock.GetUtcNow();
            var node = created.Value.Node;
            var element = new Element(id, ElementKind.Category, node.ParentId, node.Name, node.SortOrder, 1, now, author, now, author, null);
            ProjectRepository.InsertElement(connection, transaction, element);
            ProjectRepository.UpdateStructure(connection, transaction, created.Value.Changed.Where(n => n.Id != id), id, now, author);
            StructureRepository.UpsertCategory(connection, transaction, new CategoryRecord(id, input.ParentCategoryId, input.Description.Trim()));
            InsertRevision(connection, transaction, element, null, author, now);
            return Result.Success(element);
        }, cancellationToken);
    }

    /// <summary>Description and parent of a category; a parent creating a loop is refused (F07.R2, E2E-07-03).</summary>
    public Task<Result<Element>> UpdateCategoryAsync(ElementId id, CategoryInput input, string author, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Project.Database.WriteAsync((connection, transaction) =>
        {
            if (CheckDescription(input.Description) is { } invalid)
            {
                return Result.Failure<Element>(invalid);
            }

            var state = StructureState.Load(connection, []);
            if (!state.Categories.TryGetValue(id, out var category))
            {
                return Result.Failure<Element>(new DomainError(ErrorCodes.ElementNotFound, $"Category {id} does not exist."));
            }

            if (category.ParentCategoryId != input.ParentCategoryId && state.Graph.SetParent(id, input.ParentCategoryId) is { IsSuccess: false } refused)
            {
                return Result.Failure<Element>(refused.Error);
            }

            StructureRepository.UpsertCategory(connection, transaction, category with
            {
                ParentCategoryId = input.ParentCategoryId,
                Description = input.Description.Trim(),
            });
            // Pages under the category inherit through it: only them are affected.
            return Result.Success(TouchAndRecompute(connection, transaction, id, author, AffectedPages(connection, state, id)));
        }, cancellationToken);
    }

    /// <summary>Sets (or removes, with null) the default value a category gives to a property (F07.R1, E2E-07-08).</summary>
    public Task<Result<Element>> SetCategoryValueAsync(
        ElementId categoryId, ElementId propertyId, JsonElement? value, string author, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var state = StructureState.Load(connection, []);
            if (!state.Categories.ContainsKey(categoryId))
            {
                return Result.Failure<Element>(new DomainError(ErrorCodes.ElementNotFound, $"Category {categoryId} does not exist."));
            }

            if (!state.Definitions.TryGetValue(propertyId, out var definition))
            {
                return Result.Failure<Element>(new DomainError(ErrorCodes.PropertyNotFound, $"Property {propertyId} does not exist."));
            }

            PropertyValue? parsed = null;
            if (value is { } json)
            {
                var read = PropertyValues.FromJson(json, definition.Type, definition.Options);
                if (!read.IsSuccess)
                {
                    return Result.Failure<Element>(read.Error);
                }

                parsed = read.Value;
            }

            StructureRepository.SetValue(connection, transaction, categoryId, propertyId, parsed);
            return Result.Success(TouchAndRecompute(connection, transaction, categoryId, author, AffectedPages(connection, state, categoryId), [propertyId]));
        }, cancellationToken);

    // ─── Properties ─────────────────────────────────────────────────────────

    public Task<Result<Element>> CreatePropertyAsync(
        ElementId parentId, string name, int? index, PropertyInput input, string author, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Project.Database.WriteAsync((connection, transaction) =>
        {
            var tree = Repository.LoadTree(connection);
            var id = NewUniqueId(connection, transaction);
            var created = tree.Create(id, ElementKind.PropertyDefinition, parentId, name, index);
            if (!created.IsSuccess)
            {
                return Result.Failure<Element>(created.Error);
            }

            var node = created.Value.Node;
            var key = input.Key ?? PropertyKey.Unique(PropertyKey.FromName(node.Name), StructureRepository.LoadDefinitions(connection).Select(d => d.Key));
            var definition = BuildDefinition(connection, id, node.Name, input with { Key = key }, current: null);
            if (!definition.IsSuccess)
            {
                return Result.Failure<Element>(definition.Error);
            }

            var now = clock.GetUtcNow();
            var element = new Element(id, ElementKind.PropertyDefinition, node.ParentId, node.Name, node.SortOrder, 1, now, author, now, author, null);
            ProjectRepository.InsertElement(connection, transaction, element);
            ProjectRepository.UpdateStructure(connection, transaction, created.Value.Changed.Where(n => n.Id != id), id, now, author);
            StructureRepository.UpsertDefinition(connection, transaction, definition.Value);
            InsertRevision(connection, transaction, element, null, author, now);
            return Result.Success(element);
        }, cancellationToken);
    }

    /// <summary>
    /// Updates a definition: key (unique, E2E-08-11), description, type (only if every value converts, F08.R6), default,
    /// rules (violations of every page recomputed, F08.R4) and options (removed options in use need a replacement,
    /// F08.R5). All in one transaction.
    /// </summary>
    public Task<Result<Element>> UpdatePropertyAsync(ElementId id, PropertyInput input, string author, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Project.Database.WriteAsync((connection, transaction) =>
        {
            var tree = Repository.LoadTree(connection);
            var current = StructureRepository.GetDefinition(connection, id);
            if (current is null || !tree.Contains(id))
            {
                return Result.Failure<Element>(new DomainError(ErrorCodes.ElementNotFound, $"Property {id} does not exist."));
            }

            var built = BuildDefinition(connection, id, current.Name, input with { Key = input.Key ?? current.Key }, current);
            if (!built.IsSuccess)
            {
                return Result.Failure<Element>(built.Error);
            }

            var definition = built.Value;
            var converted = ConvertValues(connection, tree, current, definition, input.Replacements ?? new Dictionary<string, string>());
            if (!converted.IsSuccess)
            {
                return Result.Failure<Element>(converted.Error);
            }

            StructureRepository.UpsertDefinition(connection, transaction, definition);
            foreach (var (owner, value) in converted.Value)
            {
                StructureRepository.SetValue(connection, transaction, owner, id, value);
            }

            return Result.Success(TouchAndRecompute(connection, transaction, id, author, pages: null, properties: [id]));
        }, cancellationToken);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static DomainError? ApplyPageStructure(SqliteConnection connection, SqliteTransaction transaction, ElementId pageId, PageStructureInput structure)
    {
        var state = StructureState.Load(connection, []);
        if (structure.Categories is { } categories)
        {
            var distinct = categories.Distinct().ToArray();
            if (distinct.Length > MaxPageCategories || distinct.FirstOrDefault(c => !state.Categories.ContainsKey(c)) is { IsNone: false } unknown)
            {
                return new DomainError(ErrorCodes.PageCategoryInvalid, "A category of the page does not exist.");
            }

            StructureRepository.ReplacePageCategories(connection, transaction, pageId, distinct);
        }

        if (structure.Values is { } values)
        {
            var parsed = new List<(ElementId, PropertyValue)>();
            foreach (var (property, json) in values.DistinctBy(v => v.Property))
            {
                if (!state.Definitions.TryGetValue(property, out var definition))
                {
                    return new DomainError(ErrorCodes.PropertyNotFound, $"Property {property} does not exist.",
                        new Dictionary<string, object?>(StringComparer.Ordinal) { ["propertyId"] = property.ToString() });
                }

                var value = PropertyValues.FromJson(json, definition.Type, definition.Options);
                if (!value.IsSuccess)
                {
                    return value.Error with
                    {
                        Extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["propertyId"] = property.ToString() },
                    };
                }

                parsed.Add((property, value.Value));
            }

            StructureRepository.ReplaceValues(connection, transaction, pageId, parsed);
        }

        return null;
    }

    /// <summary>Live pages that have the category or one of its sub-categories.</summary>
    private static IReadOnlyList<ElementId> AffectedPages(SqliteConnection connection, StructureState state, ElementId categoryId) =>
        StructureRepository.PagesWithCategories(connection, [categoryId, .. state.Graph.Descendants(categoryId)]);

    /// <summary>New revision of the element, then the derived tables of the affected pages (all when null).</summary>
    private Element TouchAndRecompute(
        SqliteConnection connection, SqliteTransaction transaction, ElementId id, string author, IReadOnlyCollection<ElementId>? pages,
        IReadOnlyCollection<ElementId>? properties = null)
    {
        if (pages is null || pages.Count > 0)
        {
            StructureState.Load(connection, pages).WriteDerived(connection, transaction, pages, properties);
        }

        var now = clock.GetUtcNow();
        ProjectRepository.Touch(connection, transaction, id, now, author);
        var element = Repository.GetElement(id, connection)!;
        InsertRevision(connection, transaction, element, null, author, now);
        return element;
    }

    private static DomainError? CheckDescription(string? description) =>
        description is null || description.Length > MaxCategoryDescription
            ? new DomainError(ErrorCodes.PropertyDescriptionTooLong, $"The description exceeds {MaxCategoryDescription} characters.")
            : null;

    /// <summary>Reads and checks a definition from its input; keys of new options are derived from their labels.</summary>
    private static Result<PropertyDefinition> BuildDefinition(SqliteConnection connection, ElementId id, string name, PropertyInput input, PropertyDefinition? current)
    {
        var type = PropertyType.Parse(input.Type);
        if (!type.IsSuccess)
        {
            return type.Error;
        }

        var key = PropertyKey.Normalize(input.Key);
        if (!key.IsSuccess)
        {
            return key.Error;
        }

        if (StructureRepository.KeyIsTaken(connection, key.Value, id))
        {
            return new DomainError(ErrorCodes.PropertyKeyTaken, $"The key '{key.Value}' is already used.");
        }

        var options = new List<EnumOption>();
        foreach (var (optionKey, label) in input.Options)
        {
            var trimmed = label?.Trim() ?? string.Empty;
            var resolved = optionKey ?? current?.Options.FirstOrDefault(o => o.Label == trimmed)?.Key
                ?? PropertyKey.Unique(PropertyKey.FromName(trimmed), options.Select(o => o.Key).Concat(input.Options.Where(o => o.Key is not null).Select(o => o.Key!)));
            options.Add(new EnumOption(resolved, trimmed));
        }

        IReadOnlyList<ValidationRule> rules = [];
        if (input.Rules is { ValueKind: not JsonValueKind.Null } rulesJson)
        {
            var read = ValidationRules.ListFromJson(rulesJson, type.Value, options);
            if (!read.IsSuccess)
            {
                return read.Error;
            }

            rules = read.Value;
        }

        PropertyValue? @default = null;
        if (input.Default is { ValueKind: not JsonValueKind.Null } defaultJson)
        {
            var read = PropertyValues.FromJson(defaultJson, type.Value, options);
            if (!read.IsSuccess)
            {
                return read.Error;
            }

            @default = read.Value;
        }

        var definition = new PropertyDefinition(id, name, key.Value, input.Description.Trim(), type.Value, @default, rules, options);
        return definition.Check() is { } error ? error : definition;
    }

    /// <summary>
    /// Values to rewrite after a definition change: removed options replaced (F08.R5), then every value converted to the
    /// new type (F08.R6). Fails with the blocking owners in the <c>pages</c> extension.
    /// </summary>
    private static Result<IReadOnlyList<(ElementId Owner, PropertyValue? Value)>> ConvertValues(
        SqliteConnection connection, Core.Tree.ElementTree tree, PropertyDefinition current, PropertyDefinition next,
        IReadOnlyDictionary<string, string> replacements)
    {
        var definitions = new Dictionary<ElementId, PropertyDefinition> { [current.Id] = current };
        var stored = StructureRepository.LoadValues(connection, definitions, null, current.Id);
        // Options removed from an enum that stays an enum; leaving the enum type is a conversion (by label), not a removal.
        var removed = next.Type.Scalar == ScalarType.Enum
            ? current.Options.Select(o => o.Key).Except(next.Options.Select(o => o.Key)).ToHashSet(StringComparer.Ordinal)
            : [];
        if (replacements.Values.Any(r => next.Options.All(o => o.Key != r)))
        {
            return new DomainError(ErrorCodes.PropertyOptionInvalid, "A replacement is not an option of the property.");
        }

        var changes = new List<(ElementId, PropertyValue?)>();
        var missingReplacement = new List<StoredValue>();
        var notConvertible = new List<StoredValue>();
        foreach (var value in stored)
        {
            var replaced = Replace(value.Value, removed, replacements, out var missing);
            if (missing)
            {
                missingReplacement.Add(value);
                continue;
            }

            var converted = ValueConversion.Convert(replaced, current.Type, current.Options, next.Type, next.Options);
            if (!converted.IsSuccess)
            {
                notConvertible.Add(value);
            }
            else if (converted.Value != value.Value)
            {
                changes.Add((value.OwnerId, converted.Value));
            }
        }

        if (missingReplacement.Count > 0)
        {
            return Blocking(tree, ErrorCodes.PropertyOptionInUse, "Removed options are still used: choose a replacement.", missingReplacement, current);
        }

        if (notConvertible.Count > 0)
        {
            return Blocking(tree, ErrorCodes.PropertyValueNotConvertible, $"Some values cannot be converted to {next.Type}.", notConvertible, current);
        }

        return changes;
    }

    private static PropertyValue Replace(PropertyValue value, HashSet<string> removed, IReadOnlyDictionary<string, string> replacements, out bool missing)
    {
        var lacking = false;
        PropertyValue Map(PropertyValue v)
        {
            if (v is EnumValue e && removed.Contains(e.Key))
            {
                if (replacements.TryGetValue(e.Key, out var replacement))
                {
                    return new EnumValue(replacement);
                }

                lacking = true;
            }

            return v;
        }

        var result = value is ListValue list ? new ListValue([.. list.Items.Select(Map)]) : Map(value);
        missing = lacking;
        return result;
    }

    private static DomainError Blocking(
        Core.Tree.ElementTree tree, string code, string message, IEnumerable<StoredValue> values, PropertyDefinition definition)
    {
        var elements = values.Select(v => (v, Node: tree.Find(v.OwnerId))).Where(x => x.Node is not null).ToList();
        var pages = elements
            .OrderBy(x => x.Node!.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = x.v.OwnerId.ToString(),
                ["name"] = x.Node!.Name,
                ["kind"] = x.Node.Kind == ElementKind.Page ? "page" : "category",
                ["value"] = ValueConversion.TextOf(x.v.Value, definition.Options),
            })
            .ToArray();
        return new DomainError(code, message, new Dictionary<string, object?>(StringComparer.Ordinal) { ["pages"] = pages });
    }

    /// <summary>The complete state of an element for its revision (F13.R1), read in the caller's transaction.</summary>
    public static RevisionSnapshot Snapshot(SqliteConnection connection, Element element, string? markdown)
    {
        var snapshot = new RevisionSnapshot(element.Id, element.Kind, element.ParentId, element.Name, markdown, null);
        switch (element.Kind)
        {
            case ElementKind.Page:
                return snapshot with
                {
                    Categories = StructureRepository.LoadPageCategories(connection, [element.Id]).GetValueOrDefault(element.Id) ?? [],
                    Values = ValuesSnapshot(connection, element.Id),
                };
            case ElementKind.Category when StructureRepository.GetCategory(connection, element.Id) is { } category:
                return snapshot with
                {
                    Category = new CategorySnapshot(category.Description, category.ParentCategoryId.IsNone ? null : category.ParentCategoryId),
                    Values = ValuesSnapshot(connection, element.Id),
                };
            case ElementKind.PropertyDefinition when StructureRepository.GetDefinition(connection, element.Id) is { } definition:
                return snapshot with { Definition = DefinitionJson(definition) };
            case ElementKind.LinkType when LinkRepository.GetType(connection, element.Id) is { } linkType:
                return snapshot with { LinkType = new LinkTypeSnapshot(linkType.Description, linkType.InverseName) };
            default:
                return snapshot;
        }
    }

    private static Dictionary<string, JsonNode?> ValuesSnapshot(SqliteConnection connection, ElementId owner)
    {
        var definitions = StructureRepository.LoadDefinitions(connection).ToDictionary(d => d.Id);
        return StructureRepository.LoadValues(connection, definitions, [owner])
            .ToDictionary(v => v.PropertyId.ToString(), v => (JsonNode?)PropertyValues.ToJson(v.Value), StringComparer.Ordinal);
    }

    /// <summary>JSON form of a definition (revisions, API).</summary>
    public static JsonObject DefinitionJson(PropertyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new JsonObject
        {
            ["key"] = definition.Key,
            ["description"] = definition.Description,
            ["type"] = definition.Type.ToString(),
            ["default"] = definition.Default is null ? null : PropertyValues.ToJson(definition.Default),
            ["rules"] = ValidationRules.ListToJson(definition.Rules),
            ["options"] = new JsonArray([.. definition.Options.Select(o => new JsonObject { ["key"] = o.Key, ["label"] = o.Label })]),
        };
    }
}
