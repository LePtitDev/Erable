using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Properties;

namespace Erable.Cli;

/// <summary>
/// The output contract of <c>erable-cli</c> (docs/cli.md). Every JSON document carries <see cref="Version"/>: adding a
/// field, a command or an option keeps it; removing or renaming one, or changing its meaning, bumps it.
/// </summary>
public static class Contract
{
    public const string Version = "erable-cli/1";

    /// <summary>Compact JSON (standard output without <c>--pretty</c>).</summary>
    public static readonly JsonSerializerOptions Compact = Create(indented: false);

    /// <summary>Indented JSON (<c>--pretty</c>, files of an export).</summary>
    public static readonly JsonSerializerOptions Indented = Create(indented: true);

    private static JsonSerializerOptions Create(bool indented) => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = indented,
        // Absent values are written as null: every documented field is always there.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        // Accents and other non-ASCII text stay readable; the output is never embedded in HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new ElementIdJsonConverter(), new UtcTimestampConverter() },
    };

    /// <summary>Kind of an element, as the API and the database name it.</summary>
    public static string KindName(ElementKind kind) => kind switch
    {
        ElementKind.Root => "root",
        ElementKind.Folder => "folder",
        ElementKind.Page => "page",
        ElementKind.Category => "category",
        ElementKind.PropertyDefinition => "propertyDefinition",
        ElementKind.LinkType => "linkType",
        ElementKind.Attachment => "attachment",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

/// <summary>UTC ISO 8601 with milliseconds and a <c>Z</c>, as stored: <c>2026-10-13T08:00:00.000Z</c>.</summary>
internal sealed class UtcTimestampConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
    }
}

// ─── Documents (one per command; the folder export writes the same ones) ────────────────────────────────────────

public sealed record InfoDocument(string Contract, ProjectDto Project, CountsDto Counts, ToolDto Tool);

public sealed record TreeDocument(string Contract, ProjectDto Project, IReadOnlyList<ElementDto> Elements);

public sealed record PageDocument(string Contract, ProjectDto Project, PageDto Page);

public sealed record PagesDocument(string Contract, ProjectDto Project, IReadOnlyList<PageDto> Pages);

public sealed record SchemaDocument(
    string Contract, ProjectDto Project, IReadOnlyList<CategoryDto> Categories, IReadOnlyList<PropertyDto> Properties, IReadOnlyList<LinkTypeDto> LinkTypes);

public sealed record LinksDocument(string Contract, ProjectDto Project, IReadOnlyList<LinkDto> Links, IReadOnlyList<ReferenceEdgeDto> References);

public sealed record AttachmentsDocument(string Contract, ProjectDto Project, IReadOnlyList<AttachmentDto> Attachments);

/// <summary>Summary of <c>export</c>, on the standard output.</summary>
/// <param name="Output">Absolute path of the export folder.</param>
/// <param name="MissingAttachments">Attachments whose content is missing from the project (<c>erable doctor</c> says more).</param>
public sealed record ExportDocument(
    string Contract, ProjectDto Project, string Output, int Pages, int Attachments, bool Markdown, IReadOnlyList<ElementId> MissingAttachments);

/// <summary>What went wrong, on the error output: <c>{"error":{"code":"…","message":"…"}}</c>.</summary>
public sealed record ErrorDocument(ErrorDto Error);

public sealed record ErrorDto(string Code, string Message);

// ─── Parts ───────────────────────────────────────────────────────────────────────────────────────────────────

/// <param name="Folder">Absolute path of the project folder that was read.</param>
public sealed record ProjectDto(ElementId Id, string Name, string ContentLanguage, int FormatVersion, DateTimeOffset CreatedAt, string Folder);

public sealed record CountsDto(int Pages, int Folders, int Categories, int Properties, int LinkTypes, int Links, int Attachments, long AttachmentBytes);

public sealed record ToolDto(string Version);

/// <summary>A live element (the trash is never exported).</summary>
/// <param name="ParentId">Null for the four roots.</param>
/// <param name="Root">Key of the root the element lives under: pages, categories, properties or links.</param>
/// <param name="Path">Names from the first level under the root down to the element itself (empty for a root).</param>
public sealed record ElementDto(
    ElementId Id, string Kind, ElementId? ParentId, string Name, long SortOrder, string? Root, IReadOnlyList<string> Path,
    long Revision, DateTimeOffset CreatedAt, string CreatedBy, DateTimeOffset UpdatedAt, string UpdatedBy);

public sealed record NamedDto(ElementId Id, string Kind, string Name);

/// <param name="Markdown">Source of the page; null with <c>--no-content</c>.</param>
/// <param name="PlainText">Text without markup (what search indexes); null with <c>--no-content</c>.</param>
public sealed record PageDto(
    ElementDto Element,
    IReadOnlyList<NamedDto> Categories,
    IReadOnlyList<PagePropertyDto> Properties,
    string? Markdown,
    string? PlainText,
    IReadOnlyList<HeadingDto> Headings,
    IReadOnlyList<ReferenceDto> References,
    IReadOnlyList<NamedDto> Backlinks,
    PageLinksDto Links,
    IReadOnlyList<AttachmentDto> Attachments);

/// <summary>A property of a page after inheritance (F07, F08).</summary>
/// <param name="Value">Effective value (JSON form of the type); null in a conflict.</param>
/// <param name="Origin">own, inherited, default or conflict.</param>
/// <param name="SourceIds">Categories that provide an inherited value.</param>
/// <param name="OwnValue">Value set on the page itself, if any.</param>
/// <param name="Candidates">Values the categories disagree on (conflict only).</param>
/// <param name="Violations">Rules the effective value breaks: they warn, never block.</param>
public sealed record PagePropertyDto(
    ElementId PropertyId, string Key, string Name, string Type, JsonNode? Value, string Origin, IReadOnlyList<ElementId> SourceIds,
    JsonNode? OwnValue, IReadOnlyList<CandidateDto> Candidates, IReadOnlyList<ViolationDto> Violations);

public sealed record CandidateDto(ElementId CategoryId, JsonNode Value);

/// <param name="Index">Failing item of a list value; null for the value as a whole.</param>
public sealed record ViolationDto(JsonObject Rule, int? Index);

public sealed record HeadingDto(int Level, string Text, string Anchor);

/// <summary>A <c>[[ID]]</c> or <c>![[ID]]</c> of the page, in order.</summary>
/// <param name="State">existing, deleted (in the trash) or unknown.</param>
/// <param name="Target">The target when it exists.</param>
public sealed record ReferenceDto(
    ElementId TargetId, string? Anchor, string? Label, bool Embed, EmbedDto? Attributes, string State, NamedDto? Target);

public sealed record EmbedDto(int? Width, string? Align);

public sealed record PageLinksDto(IReadOnlyList<PageLinkDto> Outgoing, IReadOnlyList<PageLinkDto> Incoming);

/// <param name="Label">Name of the type (outgoing) or its inverse name (incoming).</param>
/// <param name="PageId">The page at the other end.</param>
public sealed record PageLinkDto(ElementId LinkId, ElementId TypeId, string Type, string Label, ElementId PageId, string PageName);

/// <param name="File">Where an export puts its content: <c>attachments/&lt;ID&gt;/&lt;file name safe on every system&gt;</c>.</param>
public sealed record AttachmentDto(ElementId Id, ElementId PageId, string FileName, string Mime, long Size, string Sha256, string File);

/// <param name="Values">Values set on the category itself: the defaults its pages inherit.</param>
public sealed record CategoryDto(
    ElementId Id, string Name, IReadOnlyList<string> Path, ElementId? ParentCategoryId, string Description, IReadOnlyList<CategoryValueDto> Values);

public sealed record CategoryValueDto(ElementId PropertyId, string Key, JsonNode Value);

/// <param name="Type">bool, int, float, string, text, enum, date, ref, or list&lt;…&gt; of one of them.</param>
/// <param name="Rules">Validation rules, in the JSON form of the API.</param>
public sealed record PropertyDto(
    ElementId Id, string Name, string Key, string Description, string Type, JsonNode? Default, JsonArray Rules, IReadOnlyList<EnumOption> Options);

public sealed record LinkTypeDto(ElementId Id, string Name, string Description, string? InverseName);

public sealed record LinkDto(ElementId Id, ElementId TypeId, string Type, ElementId SourceId, ElementId TargetId, DateTimeOffset CreatedAt, string CreatedBy);

/// <param name="State">existing, deleted or unknown, as for <see cref="ReferenceDto"/>.</param>
public sealed record ReferenceEdgeDto(ElementId SourceId, ElementId TargetId, string? Anchor, bool Embed, string State);
