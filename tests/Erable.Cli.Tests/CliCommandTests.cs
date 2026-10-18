using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Erable.Testing;
using Erable.Testing.Reference;

namespace Erable.Cli.Tests;

/// <summary>Commands of erable-cli on the reference project (docs/cli.md).</summary>
public sealed class CliCommandTests : IDisposable
{
    private readonly CliProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public async Task Info_ReferenceProject_PrintsIdentityAndCounts()
    {
        var run = await CliProject.RunAsync("info", _project.Folder);

        run.Code.ShouldBe(CliApp.Success);
        var json = run.Json;
        json["contract"]!.GetValue<string>().ShouldBe(Contract.Version);
        json["project"]!["id"]!.GetValue<string>().ShouldBe(Chroniques.ProjectId.ToString());
        json["project"]!["name"]!.GetValue<string>().ShouldBe(Chroniques.Name);
        json["counts"]!["pages"]!.GetValue<int>().ShouldBe(9);
        json["counts"]!["links"]!.GetValue<int>().ShouldBe(2);
        json["counts"]!["attachments"]!.GetValue<int>().ShouldBe(5);
    }

    [Fact]
    public async Task Any_Command_LeavesTheProjectUntouched()
    {
        var before = FolderFingerprint.Of(_project.Folder);

        (await CliProject.RunAsync("pages", _project.Folder)).Code.ShouldBe(CliApp.Success);
        (await CliProject.RunAsync("export", _project.Folder, "--output", _project.Scratch("out"), "--markdown")).Code.ShouldBe(CliApp.Success);

        FolderFingerprint.Of(_project.Folder).ShouldBe(before);
    }

    [Fact]
    public async Task Page_Ilsa_GivesEffectivePropertiesWithTheirOrigin()
    {
        var run = await CliProject.RunAsync("page", _project.Folder, Chroniques.Ilsa.ToString());

        run.Code.ShouldBe(CliApp.Success);
        var properties = run.Json["page"]!["properties"]!.AsArray();
        var niveau = properties.Single(p => p!["key"]!.GetValue<string>() == "niveau")!;
        niveau["value"]!.GetValue<int>().ShouldBe(72);
        niveau["origin"]!.GetValue<string>().ShouldBe("own");
        niveau["violations"]!.AsArray().Single()!["rule"]!["op"]!.GetValue<string>().ShouldBe("less_or_equal");
        var hostile = properties.Single(p => p!["key"]!.GetValue<string>() == "hostile")!;
        hostile["origin"]!.GetValue<string>().ShouldBe("inherited");
        hostile["sourceIds"]!.AsArray().Select(s => s!.GetValue<string>()).ShouldBe([Chroniques.Pnj.ToString()]);
    }

    [Fact]
    public async Task Page_Ilsa_ListsReferencesLinksAndAttachments()
    {
        var page = (await CliProject.RunAsync("page", _project.Folder, Chroniques.Ilsa.ToString())).Json["page"]!;

        page["element"]!["path"]!.AsArray().Select(p => p!.GetValue<string>()).ShouldBe(["Personnages", "PNJ", "Ilsa Varenne"]);
        page["references"]!.AsArray().Select(r => r!["targetId"]!.GetValue<string>())
            .ShouldContain(Chroniques.Hautbois.ToString());
        page["references"]!.AsArray().ShouldAllBe(r => r!["state"]!.GetValue<string>() == "existing");
        page["links"]!["outgoing"]!.AsArray().Select(l => l!["pageName"]!.GetValue<string>())
            .ShouldBe(["Capitaine Orsen", "Hautbois"], ignoreOrder: true);
        page["attachments"]!.AsArray().Single()!["fileName"]!.GetValue<string>().ShouldBe("portrait_ilsa.png");
        page["markdown"]!.GetValue<string>().ShouldStartWith("Ilsa Varenne tient le comptoir");
    }

    [Fact]
    public async Task Page_NoContent_LeavesOutTheText()
    {
        var page = (await CliProject.RunAsync("page", _project.Folder, Chroniques.Ilsa.ToString().ToLowerInvariant(), "--no-content")).Json["page"]!;

        page["markdown"].ShouldBeNull();
        page["plainText"].ShouldBeNull();
        page["headings"]!.AsArray().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Page_UnknownId_FailsWithElementNotFound()
    {
        var run = await CliProject.RunAsync("page", _project.Folder, "0123456789ABCDEF");

        run.Code.ShouldBe(CliApp.ElementNotFound);
        run.Output.ShouldBeEmpty();
        run.ErrorJson["error"]!["code"]!.GetValue<string>().ShouldBe(CliErrors.ElementNotFound);
    }

    [Fact]
    public async Task Page_Category_FailsWithWrongKind()
    {
        var run = await CliProject.RunAsync("page", _project.Folder, Chroniques.Pnj.ToString());

        run.Code.ShouldBe(CliApp.ElementNotFound);
        run.ErrorJson["error"]!["code"]!.GetValue<string>().ShouldBe(CliErrors.NotAPage);
    }

    [Fact]
    public async Task Pages_Category_IncludesThePagesOfSubCategories()
    {
        var run = await CliProject.RunAsync("pages", _project.Folder, "--category", Chroniques.Personnage.ToString(), "--no-content");

        var names = run.Json["pages"]!.AsArray().Select(p => p!["element"]!["name"]!.GetValue<string>()).ToList();
        names.ShouldContain("Ilsa Varenne");
        names.ShouldNotContain("Hautbois");
    }

    [Fact]
    public async Task Tree_Kind_KeepsOnlyThatKind()
    {
        var run = await CliProject.RunAsync("tree", _project.Folder, "--kind", "linkType");

        run.Json["elements"]!.AsArray().Select(e => e!["name"]!.GetValue<string>()).ShouldBe(["Allié de", "Situé à", "Mentor de"], ignoreOrder: true);
    }

    [Fact]
    public async Task Schema_Category_GivesItsParentAndInheritedValues()
    {
        var schema = (await CliProject.RunAsync("schema", _project.Folder)).Json;

        var pnj = schema["categories"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == Chroniques.Pnj.ToString())!;
        pnj["parentCategoryId"]!.GetValue<string>().ShouldBe(Chroniques.Personnage.ToString());
        pnj["values"]!.AsArray().Select(v => v!["key"]!.GetValue<string>()).ShouldContain("hostile");
        schema["properties"]!.AsArray().Single(p => p!["key"]!.GetValue<string>() == "niveau")!["type"]!.GetValue<string>().ShouldBe("int");
        schema["linkTypes"]!.AsArray().Count.ShouldBe(3);
    }

    [Fact]
    public async Task Links_ReferenceProject_GivesTypedLinksAndReferences()
    {
        var json = (await CliProject.RunAsync("links", _project.Folder)).Json;

        json["links"]!.AsArray().Select(l => l!["type"]!.GetValue<string>()).ShouldBe(["Allié de", "Situé à"], ignoreOrder: true);
        json["references"]!.AsArray().ShouldContain(r =>
            r!["sourceId"]!.GetValue<string>() == Chroniques.Ilsa.ToString() && r["targetId"]!.GetValue<string>() == Chroniques.Orsen.ToString());
    }

    [Fact]
    public async Task Attachment_StandardOutput_GivesTheExactContent()
    {
        var list = (await CliProject.RunAsync("attachments", _project.Folder)).Json["attachments"]!.AsArray();
        var portrait = list.Single(a => a!["fileName"]!.GetValue<string>() == "portrait_ilsa.png")!;

        var run = await CliProject.RunAsync("attachment", _project.Folder, portrait["id"]!.GetValue<string>());

        run.Code.ShouldBe(CliApp.Success);
        Convert.ToHexStringLower(SHA256.HashData(run.Binary)).ShouldBe(portrait["sha256"]!.GetValue<string>(), StringCompareShould.IgnoreCase);
        run.Binary.Length.ShouldBe(portrait["size"]!.GetValue<int>());
    }

    [Fact]
    public async Task Attachment_Page_FailsWithWrongKind()
    {
        var run = await CliProject.RunAsync("attachment", _project.Folder, Chroniques.Ilsa.ToString());

        run.Code.ShouldBe(CliApp.ElementNotFound);
        run.ErrorJson["error"]!["code"]!.GetValue<string>().ShouldBe(CliErrors.NotAnAttachment);
    }

    [Fact]
    public async Task Any_NotAProject_FailsWithProjectUnreadable()
    {
        var empty = Directory.CreateDirectory(_project.Scratch("empty")).FullName;

        var run = await CliProject.RunAsync("info", empty);

        run.Code.ShouldBe(CliApp.ProjectUnreadable);
        run.ErrorJson["error"]!["code"]!.GetValue<string>().ShouldBe("project.notAProject");
    }

    [Fact]
    public async Task Any_Pretty_IndentsTheSameDocument()
    {
        var compact = await CliProject.RunAsync("schema", _project.Folder);
        var pretty = await CliProject.RunAsync("schema", _project.Folder, "--pretty");

        compact.Output.Trim().ShouldNotContain('\n');
        pretty.Output.ShouldContain('\n');
        JsonNode.DeepEquals(compact.Json, pretty.Json).ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_Command_FailsWithUsageError()
    {
        (await CliProject.RunAsync("frobnicate", _project.Folder)).Code.ShouldBe(CliApp.UsageError);
    }
}
