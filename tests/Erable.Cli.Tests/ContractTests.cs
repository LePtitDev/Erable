using System.Text.Json.Nodes;

namespace Erable.Cli.Tests;

/// <summary>
/// The shape of the documents is a contract with other tools (docs/cli.md): a field may be added, never removed or
/// renamed without bumping <see cref="Contract.Version"/>. These tests list the fields; adding one means adding it here
/// and in docs/cli.md, removing one means a new contract version.
/// </summary>
public sealed class ContractTests : IDisposable
{
    private readonly CliProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public async Task Pages_Document_KeepsItsFields()
    {
        var json = (await CliProject.RunAsync("pages", _project.Folder)).Json;

        Paths(json).ShouldBe(
        [
            "contract",
            "pages", "pages[].attachments", "pages[].attachments[].file", "pages[].attachments[].fileName", "pages[].attachments[].id",
            "pages[].attachments[].mime", "pages[].attachments[].pageId", "pages[].attachments[].sha256", "pages[].attachments[].size",
            "pages[].backlinks", "pages[].backlinks[].id", "pages[].backlinks[].kind", "pages[].backlinks[].name",
            "pages[].categories", "pages[].categories[].id", "pages[].categories[].kind", "pages[].categories[].name",
            "pages[].element", "pages[].element.createdAt", "pages[].element.createdBy", "pages[].element.id", "pages[].element.kind",
            "pages[].element.name", "pages[].element.parentId", "pages[].element.path", "pages[].element.revision", "pages[].element.root",
            "pages[].element.sortOrder", "pages[].element.updatedAt", "pages[].element.updatedBy",
            "pages[].headings", "pages[].headings[].anchor", "pages[].headings[].level", "pages[].headings[].text",
            "pages[].links", "pages[].links.incoming", "pages[].links.incoming[].label", "pages[].links.incoming[].linkId",
            "pages[].links.incoming[].pageId", "pages[].links.incoming[].pageName", "pages[].links.incoming[].type", "pages[].links.incoming[].typeId",
            "pages[].links.outgoing", "pages[].links.outgoing[].label", "pages[].links.outgoing[].linkId", "pages[].links.outgoing[].pageId",
            "pages[].links.outgoing[].pageName", "pages[].links.outgoing[].type", "pages[].links.outgoing[].typeId",
            "pages[].markdown", "pages[].plainText",
            "pages[].properties", "pages[].properties[].candidates", "pages[].properties[].key", "pages[].properties[].name",
            "pages[].properties[].origin", "pages[].properties[].ownValue", "pages[].properties[].propertyId", "pages[].properties[].sourceIds",
            "pages[].properties[].type", "pages[].properties[].value", "pages[].properties[].violations", "pages[].properties[].violations[].index",
            "pages[].properties[].violations[].rule",
            "pages[].references", "pages[].references[].anchor", "pages[].references[].attributes", "pages[].references[].embed",
            "pages[].references[].label", "pages[].references[].state", "pages[].references[].target", "pages[].references[].target.id",
            "pages[].references[].target.kind", "pages[].references[].target.name", "pages[].references[].targetId",
            "project", "project.contentLanguage", "project.createdAt", "project.folder", "project.formatVersion", "project.id",
            "project.name",
        ]);
    }

    [Fact]
    public async Task Schema_Document_KeepsItsFields()
    {
        var json = (await CliProject.RunAsync("schema", _project.Folder)).Json;

        Paths(json).Where(p => !p.StartsWith("project", StringComparison.Ordinal)).ShouldBe(
        [
            "categories", "categories[].description", "categories[].id", "categories[].name", "categories[].parentCategoryId",
            "categories[].path", "categories[].values", "categories[].values[].key", "categories[].values[].propertyId",
            "categories[].values[].value",
            "contract",
            "linkTypes", "linkTypes[].description", "linkTypes[].id", "linkTypes[].inverseName", "linkTypes[].name",
            "properties", "properties[].default", "properties[].description", "properties[].id", "properties[].key",
            "properties[].name", "properties[].options", "properties[].options[].key", "properties[].options[].label",
            "properties[].rules", "properties[].type",
        ]);
    }

    [Fact]
    public async Task Links_Document_KeepsItsFields()
    {
        var json = (await CliProject.RunAsync("links", _project.Folder)).Json;

        Paths(json).Where(p => !p.StartsWith("project", StringComparison.Ordinal)).ShouldBe(
        [
            "contract",
            "links", "links[].createdAt", "links[].createdBy", "links[].id", "links[].sourceId", "links[].targetId", "links[].type",
            "links[].typeId",
            "references", "references[].anchor", "references[].embed", "references[].sourceId", "references[].state",
            "references[].targetId",
        ]);
    }

    /// <summary>
    /// Every field path of a document, sorted: <c>a.b</c> for an object member, <c>a[]</c> for the items of an array.
    /// The values of properties and rules are free JSON (their type says how to read them): not descended into.
    /// </summary>
    private static List<string> Paths(JsonNode node)
    {
        var paths = new SortedSet<string>(StringComparer.Ordinal);
        void Walk(JsonNode? current, string path)
        {
            switch (current)
            {
                case JsonObject obj:
                    foreach (var (name, child) in obj)
                    {
                        var childPath = path.Length == 0 ? name : $"{path}.{name}";
                        paths.Add(childPath);
                        if (name is not ("value" or "ownValue" or "default" or "rule" or "rules" or "candidates" or "attributes"))
                        {
                            Walk(child, childPath);
                        }
                    }

                    break;
                case JsonArray array:
                    foreach (var item in array)
                    {
                        Walk(item, path + "[]");
                    }

                    break;
                default:
                    break;
            }
        }

        Walk(node, string.Empty);
        return [.. paths];
    }
}
