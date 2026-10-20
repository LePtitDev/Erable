using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Erable.Core;
using Erable.Core.Categories;
using Erable.Core.Properties;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>Stored fields of a category (its name is the element name).</summary>
public sealed record CategoryRecord(ElementId Id, ElementId ParentCategoryId, string Description);

/// <summary>A value defined on a page or a category.</summary>
public sealed record StoredValue(ElementId OwnerId, ElementId PropertyId, PropertyValue Value);

/// <summary>
/// SQL of categories, property definitions, values and their derived tables (schema v2). Same conventions as
/// <see cref="ProjectRepository"/>: reads open their own connection when none is given, writes take the transaction.
/// </summary>
public static class StructureRepository
{
    // ─── Categories ─────────────────────────────────────────────────────────

    /// <summary>Category rows, deleted elements included (the caller filters with the live tree).</summary>
    public static IReadOnlyList<CategoryRecord> LoadCategories(SqliteConnection connection) =>
        [.. connection.Query<(long Id, long? Parent, string Description)>(
            "SELECT id, parent_category_id, description FROM categories")
            .Select(r => new CategoryRecord(ElementId.FromInt64(r.Id), r.Parent is { } p ? ElementId.FromInt64(p) : ElementId.None, r.Description))];

    public static CategoryRecord? GetCategory(SqliteConnection connection, ElementId id) =>
        connection.QuerySingleOrDefault<(long Id, long? Parent, string Description)?>(
            "SELECT id, parent_category_id, description FROM categories WHERE id = @Id", new { Id = id.ToInt64() }) is { } r
            ? new CategoryRecord(id, r.Parent is { } p ? ElementId.FromInt64(p) : ElementId.None, r.Description)
            : null;

    public static void UpsertCategory(SqliteConnection connection, SqliteTransaction transaction, CategoryRecord category)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(category);
        connection.Execute(
            """
            INSERT INTO categories (id, parent_category_id, description) VALUES (@Id, @Parent, @Description)
            ON CONFLICT (id) DO UPDATE SET parent_category_id = excluded.parent_category_id, description = excluded.description
            """,
            new { Id = category.Id.ToInt64(), Parent = category.ParentCategoryId.IsNone ? (long?)null : category.ParentCategoryId.ToInt64(), category.Description },
            transaction);
    }

    // ─── Page categories ────────────────────────────────────────────────────

    /// <summary>Categories of the given pages (all pages when null), in their order.</summary>
    public static Dictionary<ElementId, IReadOnlyList<ElementId>> LoadPageCategories(SqliteConnection connection, IReadOnlyCollection<ElementId>? pages)
    {
        var rows = pages is null
            ? connection.Query<(long Page, long Category)>("SELECT page_id, category_id FROM page_categories ORDER BY page_id, sort_order")
            : connection.Query<(long Page, long Category)>(
                "SELECT page_id, category_id FROM page_categories WHERE page_id IN (SELECT value FROM json_each(@Ids)) ORDER BY page_id, sort_order",
                new { Ids = Json(pages) });
        return rows.GroupBy(r => ElementId.FromInt64(r.Page))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ElementId>)[.. g.Select(r => ElementId.FromInt64(r.Category))]);
    }

    /// <summary>Pages having one of the categories (the trash is not filtered).</summary>
    public static IReadOnlyList<ElementId> PagesWithCategories(SqliteConnection connection, IEnumerable<ElementId> categories) =>
        [.. connection.Query<long>(
            "SELECT DISTINCT page_id FROM page_categories WHERE category_id IN (SELECT value FROM json_each(@Ids))",
            new { Ids = Json(categories) }).Select(ElementId.FromInt64)];

    public static void ReplacePageCategories(SqliteConnection connection, SqliteTransaction transaction, ElementId pageId, IReadOnlyList<ElementId> categories)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(categories);
        connection.Execute("DELETE FROM page_categories WHERE page_id = @Id", new { Id = pageId.ToInt64() }, transaction);
        connection.Execute(
            "INSERT INTO page_categories (page_id, category_id, sort_order) VALUES (@Page, @Category, @Order)",
            categories.Select((c, i) => new { Page = pageId.ToInt64(), Category = c.ToInt64(), Order = i }),
            transaction);
    }

    // ─── Definitions ────────────────────────────────────────────────────────

    /// <summary>
    /// Definition rows, deleted elements included. A row that cannot be read (corrupted JSON) yields a definition
    /// without rules rather than an exception; <c>doctor</c> reports it.
    /// </summary>
    public static IReadOnlyList<PropertyDefinition> LoadDefinitions(SqliteConnection connection, ElementId? only = null)
    {
        var rows = connection.Query<DefinitionRow>(
            """
            SELECT d.id AS Id, e.name AS Name, d.key AS Key, d.description AS Description, d.type AS Type,
                d.default_json AS DefaultJson, d.rules_json AS RulesJson, d.enum_json AS EnumJson
            FROM property_defs d JOIN elements e ON e.id = d.id
            WHERE @Only IS NULL OR d.id = @Only
            """,
            new { Only = only?.ToInt64() });
        return [.. rows.Select(r => r.ToDefinition())];
    }

    public static PropertyDefinition? GetDefinition(SqliteConnection connection, ElementId id) => LoadDefinitions(connection, id) is [var found, ..] ? found : null;

    public static bool KeyIsTaken(SqliteConnection connection, string key, ElementId except) =>
        connection.ExecuteScalar<long>("SELECT count(*) FROM property_defs WHERE key = @Key AND id <> @Id", new { Key = key, Id = except.ToInt64() }) > 0;

    public static void UpsertDefinition(SqliteConnection connection, SqliteTransaction transaction, PropertyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(definition);
        connection.Execute(
            """
            INSERT INTO property_defs (id, key, description, type, default_json, rules_json, enum_json)
            VALUES (@Id, @Key, @Description, @Type, @DefaultJson, @RulesJson, @EnumJson)
            ON CONFLICT (id) DO UPDATE SET key = excluded.key, description = excluded.description, type = excluded.type,
                default_json = excluded.default_json, rules_json = excluded.rules_json, enum_json = excluded.enum_json
            """,
            new
            {
                Id = definition.Id.ToInt64(),
                definition.Key,
                definition.Description,
                Type = definition.Type.ToString(),
                DefaultJson = definition.Default is null ? null : PropertyValues.ToJsonString(definition.Default),
                RulesJson = ValidationRules.ListToJson(definition.Rules).ToJsonString(),
                EnumJson = JsonSerializer.Serialize(definition.Options.Select(o => new { key = o.Key, label = o.Label })),
            },
            transaction);
    }

    // ─── Values ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Values of the given owners (all when null), read with the type of their definition. Values of an unknown
    /// property, or that no longer match its type, are skipped (<c>doctor</c> reports them).
    /// </summary>
    public static IReadOnlyList<StoredValue> LoadValues(
        SqliteConnection connection, IReadOnlyDictionary<ElementId, PropertyDefinition> definitions, IReadOnlyCollection<ElementId>? owners,
        ElementId? property = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var rows = connection.Query<ValueRow>(
            $"""
            SELECT owner_id AS OwnerId, property_id AS PropertyId, idx AS Idx, v_int AS VInt, v_real AS VReal, v_text AS VText, v_ref AS VRef
            FROM property_values
            WHERE ({(owners is null ? "1" : "owner_id IN (SELECT value FROM json_each(@Owners))")}) AND (@Property IS NULL OR property_id = @Property)
            ORDER BY owner_id, property_id, idx
            """,
            new { Owners = owners is null ? null : Json(owners), Property = property?.ToInt64() });
        var result = new List<StoredValue>();
        foreach (var group in rows.GroupBy(r => (r.OwnerId, r.PropertyId)))
        {
            var propertyId = ElementId.FromInt64(group.Key.PropertyId);
            if (definitions.TryGetValue(propertyId, out var definition) && ValueStorage.Read([.. group], definition.Type) is { } value)
            {
                result.Add(new StoredValue(ElementId.FromInt64(group.Key.OwnerId), propertyId, value));
            }
        }

        return result;
    }

    /// <summary>Raw value rows of a property (doctor, type conversions).</summary>
    public static IReadOnlyList<ValueRow> LoadValueRows(SqliteConnection connection, ElementId? property = null) =>
        [.. connection.Query<ValueRow>(
            """
            SELECT owner_id AS OwnerId, property_id AS PropertyId, idx AS Idx, v_int AS VInt, v_real AS VReal, v_text AS VText, v_ref AS VRef
            FROM property_values WHERE @Property IS NULL OR property_id = @Property ORDER BY owner_id, property_id, idx
            """,
            new { Property = property?.ToInt64() })];

    /// <summary>Sets (or removes, when <paramref name="value"/> is null) the value an owner defines for a property.</summary>
    public static void SetValue(SqliteConnection connection, SqliteTransaction transaction, ElementId owner, ElementId property, PropertyValue? value)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.Execute(
            "DELETE FROM property_values WHERE owner_id = @Owner AND property_id = @Property",
            new { Owner = owner.ToInt64(), Property = property.ToInt64() }, transaction);
        if (value is not null)
        {
            connection.Execute(
                "INSERT INTO property_values (owner_id, property_id, idx, v_int, v_real, v_text, v_ref) VALUES (@OwnerId, @PropertyId, @Idx, @VInt, @VReal, @VText, @VRef)",
                ValueStorage.Rows(owner, property, value), transaction);
        }
    }

    /// <summary>Replaces every value an owner defines.</summary>
    public static void ReplaceValues(SqliteConnection connection, SqliteTransaction transaction, ElementId owner, IEnumerable<(ElementId Property, PropertyValue Value)> values)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(values);
        connection.Execute("DELETE FROM property_values WHERE owner_id = @Owner", new { Owner = owner.ToInt64() }, transaction);
        foreach (var (property, value) in values)
        {
            connection.Execute(
                "INSERT INTO property_values (owner_id, property_id, idx, v_int, v_real, v_text, v_ref) VALUES (@OwnerId, @PropertyId, @Idx, @VInt, @VReal, @VText, @VRef)",
                ValueStorage.Rows(owner, property, value), transaction);
        }
    }

    // ─── Derived tables ─────────────────────────────────────────────────────

    /// <summary>One row of <c>resolved_values</c>.</summary>
    public sealed record ResolvedRow(long PageId, long PropertyId, long Idx, long? VInt, double? VReal, string? VText, long? VRef, string Origin, long? SourceId);

    /// <summary>One row of <c>validation_issues</c>.</summary>
    public sealed record IssueRow(long OwnerId, long PropertyId, long RuleIndex, string RuleOp, string RuleJson, long? ItemIndex);

    public static IReadOnlyList<ResolvedRow> LoadResolvedRows(SqliteConnection connection) =>
        [.. connection.Query<ResolvedRow>(
            """
            SELECT page_id AS PageId, property_id AS PropertyId, idx AS Idx, v_int AS VInt, v_real AS VReal, v_text AS VText, v_ref AS VRef,
                origin AS Origin, source_id AS SourceId
            FROM resolved_values ORDER BY page_id, property_id, idx
            """)];

    public static IReadOnlyList<IssueRow> LoadIssueRows(SqliteConnection connection) =>
        [.. connection.Query<IssueRow>(
            """
            SELECT owner_id AS OwnerId, property_id AS PropertyId, rule_index AS RuleIndex, rule_op AS RuleOp, rule_json AS RuleJson, item_index AS ItemIndex
            FROM validation_issues ORDER BY owner_id, property_id, rule_index, item_index
            """)];

    /// <summary>
    /// Replaces the derived rows of <paramref name="pages"/> (all pages when null), restricted to
    /// <paramref name="properties"/> when given.
    /// </summary>
    public static void ReplaceDerived(
        SqliteConnection connection, SqliteTransaction transaction, IReadOnlyCollection<ElementId>? pages, IReadOnlyCollection<ElementId>? properties,
        IEnumerable<ResolvedRow> resolved, IEnumerable<IssueRow> issues)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var scope = (pages is null ? "1" : "{0} IN (SELECT value FROM json_each(@Pages))")
            + (properties is null ? string.Empty : " AND property_id IN (SELECT value FROM json_each(@Properties))");
        var parameters = new { Pages = pages is null ? null : Json(pages), Properties = properties is null ? null : Json(properties) };
        connection.Execute("DELETE FROM resolved_values WHERE " + string.Format(CultureInfo.InvariantCulture, scope, "page_id"), parameters, transaction);
        connection.Execute("DELETE FROM validation_issues WHERE " + string.Format(CultureInfo.InvariantCulture, scope, "owner_id"), parameters, transaction);
        connection.Execute(
            """
            INSERT INTO resolved_values (page_id, property_id, idx, v_int, v_real, v_text, v_ref, origin, source_id)
            VALUES (@PageId, @PropertyId, @Idx, @VInt, @VReal, @VText, @VRef, @Origin, @SourceId)
            """,
            resolved, transaction);
        connection.Execute(
            """
            INSERT INTO validation_issues (owner_id, property_id, rule_index, rule_op, rule_json, item_index)
            VALUES (@OwnerId, @PropertyId, @RuleIndex, @RuleOp, @RuleJson, @ItemIndex)
            """,
            issues, transaction);
    }

    internal static string Json(IEnumerable<ElementId> ids) => "[" + string.Join(',', ids.Select(i => i.ToInt64())) + "]";

    private sealed record DefinitionRow(long Id, string Name, string Key, string Description, string Type, string? DefaultJson, string RulesJson, string EnumJson)
    {
        public PropertyDefinition ToDefinition()
        {
            var type = PropertyType.Parse(Type) is { IsSuccess: true } parsed ? parsed.Value : PropertyType.String;
            IReadOnlyList<EnumOption> options;
            try
            {
                options = JsonSerializer.Deserialize<List<OptionJson>>(EnumJson)?.Select(o => new EnumOption(o.key, o.label)).ToArray() ?? [];
            }
            catch (JsonException)
            {
                options = [];
            }

            IReadOnlyList<ValidationRule> rules = [];
            try
            {
                using var document = JsonDocument.Parse(RulesJson);
                rules = ValidationRules.ListFromJson(document.RootElement, type, options) is { IsSuccess: true } read ? read.Value : [];
            }
            catch (JsonException)
            {
            }

            var @default = DefaultJson is null ? null : PropertyValues.FromJson(DefaultJson, type, options) is { IsSuccess: true } d ? d.Value : null;
            return new PropertyDefinition(ElementId.FromInt64(Id), Name, Key, Description, type, @default, rules, options);
        }
    }

#pragma warning disable IDE1006, CA1812 // JSON shape of enum_json; instantiated by the serializer.
    private sealed record OptionJson(string key, string label);
#pragma warning restore IDE1006, CA1812
}

/// <summary>One stored row of a value (<c>property_values</c> layout).</summary>
public sealed record ValueRow(long OwnerId, long PropertyId, long Idx, long? VInt, double? VReal, string? VText, long? VRef);

/// <summary>Typed columns of a value (docs/architecture.md §7.4): what goes where, and back.</summary>
public static class ValueStorage
{
    public const long EmptyListIndex = -1;

    public static IEnumerable<ValueRow> Rows(ElementId owner, ElementId property, PropertyValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is ListValue list)
        {
            if (list.Items.IsEmpty)
            {
                yield return new ValueRow(owner.ToInt64(), property.ToInt64(), EmptyListIndex, null, null, null, null);
            }

            for (var i = 0; i < list.Items.Length; i++)
            {
                yield return Row(owner, property, i, list.Items[i]);
            }
        }
        else
        {
            yield return Row(owner, property, 0, value);
        }
    }

    /// <summary>The value back from its rows; null when the rows do not match the type.</summary>
    public static PropertyValue? Read(IReadOnlyList<ValueRow> rows, PropertyType type)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(type);
        if (!type.IsList)
        {
            return rows is [{ Idx: 0 } row] ? Scalar(row, type.Scalar) : null;
        }

        if (rows is [{ Idx: EmptyListIndex }])
        {
            return new ListValue([]);
        }

        var items = ImmutableArray.CreateBuilder<PropertyValue>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Idx != i || Scalar(rows[i], type.Scalar) is not { } item)
            {
                return null;
            }

            items.Add(item);
        }

        return items.Count == 0 ? null : new ListValue(items.MoveToImmutable());
    }

    private static ValueRow Row(ElementId owner, ElementId property, long index, PropertyValue value)
    {
        var (vInt, vReal, vText, vRef) = value switch
        {
            BoolValue b => ((long?)(b.Value ? 1 : 0), (double?)null, (string?)null, (long?)null),
            IntValue i => (i.Value, null, null, null),
            FloatValue f => (null, f.Value, null, null),
            TextValue t => (null, null, t.Value, null),
            EnumValue e => (null, null, e.Key, null),
            DateValue d => (null, null, PropertyValues.FormatDate(d.Value), null),
            RefValue r => (null, null, null, r.Id.ToInt64()),
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Lists are stored item by item."),
        };
        return new ValueRow(owner.ToInt64(), property.ToInt64(), index, vInt, vReal, vText, vRef);
    }

    private static PropertyValue? Scalar(ValueRow row, ScalarType scalar) => scalar switch
    {
        ScalarType.Bool => row.VInt is 0 or 1 ? new BoolValue(row.VInt == 1) : null,
        ScalarType.Int => row.VInt is { } i ? new IntValue(i) : null,
        ScalarType.Float => row.VReal is { } f ? new FloatValue(f) : null,
        ScalarType.String or ScalarType.Text => row.VText is { } t ? new TextValue(t) : null,
        ScalarType.Enum => row.VText is { } k ? new EnumValue(k) : null,
        ScalarType.Date => PropertyValues.TryParseDate(row.VText, out var d) ? new DateValue(d) : null,
        ScalarType.Ref => row.VRef is { } r && r != 0 ? new RefValue(ElementId.FromInt64(r)) : null,
        _ => null,
    };
}
