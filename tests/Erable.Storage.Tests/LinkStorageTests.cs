using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>Link types and typed links on the reference project (F09).</summary>
public sealed class LinkStorageTests : IDisposable
{
    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly OpenedProject _project;
    private readonly ProjectService _service;

    private static readonly ElementId MentorDe = Chroniques.IdOf("Mentor de");
    private static readonly ElementId AllieDe = Chroniques.IdOf("Allié de");
    private static readonly ElementId SituéÀ = Chroniques.IdOf("Situé à");

    public LinkStorageTests()
    {
        _project = Chroniques.Write(_dir.Path);
        _service = new ProjectService(_project, new FakeTimeProvider(Chroniques.Now));
    }

    public void Dispose()
    {
        _project.Dispose();
        _dir.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ListLinkTypes_ReferenceProject_HasTheThreeTypesWithTheirInverseAndCount()
    {
        var types = _service.ListLinkTypes();

        types.Select(t => (t.Name, t.InverseName, t.LinkCount)).ShouldBe(
            [("Allié de", "Allié de", 1), ("Mentor de", "Élève de", 0), ("Situé à", "Lieu de", 1)]);
    }

    [Fact]
    public void GetPageLinks_Ilsa_HasAnOutgoingAllyAndLocation()
    {
        var links = _service.GetPageLinks(Chroniques.Ilsa)!;

        links.Outgoing.Select(l => (l.Label, l.OtherName)).ShouldBe([("Allié de", "Capitaine Orsen"), ("Situé à", "Hautbois")]);
        links.Incoming.ShouldBeEmpty();
    }

    [Fact]
    public void GetPageLinks_Hautbois_ShowsTheInverseLabel()
    {
        // E2E-09-03
        var links = _service.GetPageLinks(Chroniques.Hautbois)!;

        links.Incoming.Select(l => (l.Label, l.OtherName)).ShouldBe([("Lieu de", "Ilsa Varenne")]);
    }

    [Fact]
    public async Task CreateLink_BetweenTwoPages_IsSeenFromBothSides()
    {
        // E2E-09-02
        var created = await _service.CreateLinkAsync(Chroniques.Ilsa, MentorDe, Chroniques.Orsen, "camille", Ct);

        created.IsSuccess.ShouldBeTrue();
        _service.GetPageLinks(Chroniques.Ilsa)!.Outgoing.ShouldContain(l => l.Label == "Mentor de" && l.OtherId == Chroniques.Orsen);
        _service.GetPageLinks(Chroniques.Orsen)!.Incoming.ShouldContain(l => l.Label == "Élève de" && l.OtherId == Chroniques.Ilsa);
        _service.GetLinkType(MentorDe)!.LinkCount.ShouldBe(1);
    }

    [Fact]
    public async Task CreateLink_TypeWithoutInverse_ShowsItsNameOnTheTarget()
    {
        var type = await _service.CreateLinkTypeAsync(Roots.Links.Id, "Voisin de", null, "", null, "camille", Ct);
        await _service.CreateLinkAsync(Chroniques.Ilsa, type.Value.Id, Chroniques.Orsen, "camille", Ct);

        _service.GetPageLinks(Chroniques.Orsen)!.Incoming.ShouldContain(l => l.TypeName == "Voisin de" && l.Label == "Voisin de");
    }

    [Fact]
    public async Task CreateLink_SamePageOnBothEnds_IsRefused()
    {
        var created = await _service.CreateLinkAsync(Chroniques.Ilsa, MentorDe, Chroniques.Ilsa, "camille", Ct);

        created.Error.Code.ShouldBe(ErrorCodes.LinkSelf);
    }

    [Fact]
    public async Task CreateLink_Twice_IsRefusedAsDuplicate()
    {
        var created = await _service.CreateLinkAsync(Chroniques.Ilsa, AllieDe, Chroniques.Orsen, "camille", Ct);

        created.Error.Code.ShouldBe(ErrorCodes.LinkDuplicate);
    }

    [Fact]
    public async Task CreateLink_ReverseDirection_IsAnotherLink()
    {
        var created = await _service.CreateLinkAsync(Chroniques.Orsen, AllieDe, Chroniques.Ilsa, "camille", Ct);

        created.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateLink_EndThatIsNotAPage_IsRefused()
    {
        var created = await _service.CreateLinkAsync(Chroniques.Ilsa, MentorDe, Chroniques.Pnj, "camille", Ct);

        created.Error.Code.ShouldBe(ErrorCodes.LinkPageInvalid);
    }

    [Fact]
    public async Task CreateLink_UnknownType_IsRefused()
    {
        var created = await _service.CreateLinkAsync(Chroniques.Ilsa, Chroniques.Orsen, Chroniques.Hautbois, "camille", Ct);

        created.Error.Code.ShouldBe(ErrorCodes.LinkTypeNotFound);
    }

    [Fact]
    public async Task DeleteLink_RemovesItFromBothPages()
    {
        // E2E-09-04
        var link = _service.GetPageLinks(Chroniques.Ilsa)!.Outgoing.Single(l => l.Label == "Allié de");

        (await _service.DeleteLinkAsync(link.LinkId, Ct)).IsSuccess.ShouldBeTrue();

        _service.GetPageLinks(Chroniques.Ilsa)!.Outgoing.ShouldNotContain(l => l.Label == "Allié de");
        _service.GetPageLinks(Chroniques.Orsen)!.Incoming.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteLink_Unknown_IsRefused()
    {
        (await _service.DeleteLinkAsync(Chroniques.Orsen, Ct)).Error.Code.ShouldBe(ErrorCodes.LinkNotFound);
    }

    [Fact]
    public async Task CreateLinkType_WithInverse_IsListedUnderTheLinksRoot()
    {
        // E2E-09-01
        var created = await _service.CreateLinkTypeAsync(Roots.Links.Id, "Rival de", null, "Deux rivaux.", "Rival de", "camille", Ct);

        created.IsSuccess.ShouldBeTrue();
        var type = _service.GetLinkType(created.Value.Id)!;
        (type.Name, type.Description, type.InverseName).ShouldBe(("Rival de", "Deux rivaux.", "Rival de"));
        _project.Repository.GetElement(created.Value.Id)!.ParentId.ShouldBe(Roots.Links.Id);
    }

    [Fact]
    public async Task UpdateLinkType_ChangesTheInverseLabelOfTheIncomingLinks()
    {
        await _service.UpdateLinkTypeAsync(SituéÀ, "Un lieu.", "Habité par", "camille", Ct);

        _service.GetPageLinks(Chroniques.Hautbois)!.Incoming.Single().Label.ShouldBe("Habité par");
        _service.GetLinkType(SituéÀ)!.Description.ShouldBe("Un lieu.");
    }

    [Fact]
    public async Task UpdateLinkType_BumpsTheRevisionAndKeepsTheFieldsInIt()
    {
        var before = _project.Repository.GetElement(MentorDe)!.Revision;

        await _service.UpdateLinkTypeAsync(MentorDe, "Nouveau texte.", "Disciple de", "camille", Ct);

        _project.Repository.GetElement(MentorDe)!.Revision.ShouldBe(before + 1);
        using var connection = _project.Database.OpenConnection();
        var snapshot = RevisionSnapshot.Deserialize(connection.ExecuteScalar<byte[]>(
            "SELECT snapshot FROM revisions WHERE element_id = @Id ORDER BY revision DESC LIMIT 1", new { Id = MentorDe.ToInt64() })!);
        snapshot.LinkType.ShouldBe(new LinkTypeSnapshot("Nouveau texte.", "Disciple de"));
    }

    [Fact]
    public async Task UpdateLinkType_Unknown_IsRefused()
    {
        var updated = await _service.UpdateLinkTypeAsync(Chroniques.Orsen, "x", null, "camille", Ct);

        updated.Error.Code.ShouldBe(ErrorCodes.LinkTypeNotFound);
    }

    [Fact]
    public async Task CreateLinkType_InverseWithControlCharacters_IsRefused()
    {
        var created = await _service.CreateLinkTypeAsync(Roots.Links.Id, "Rival de", null, "", "a\nb", "camille", Ct);

        created.Error.Code.ShouldBe(ErrorCodes.LinkTypeInverseInvalid);
    }

    [Fact]
    public async Task CreateLinkType_UnderAnotherRoot_IsRefused()
    {
        var created = await _service.CreateLinkTypeAsync(Roots.Pages.Id, "Rival de", null, "", null, "camille", Ct);

        created.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Doctor_LinkToSomethingThatIsNotAPage_IsReported()
    {
        _project.Database.Write((c, t) => c.Execute(
            "INSERT INTO links (id, source_id, link_type_id, target_id, created_at, created_by) VALUES (7, @Source, @Type, @Target, '2026-01-01T00:00:00.0000000+00:00', 'local')",
            new { Source = Chroniques.Ilsa.ToInt64(), Type = MentorDe.ToInt64(), Target = Chroniques.Pnj.ToInt64() }, t));

        ProjectDoctor.Diagnose(_project, fix: false).Findings.ShouldContain(f => f.Code == DoctorCodes.LinkDangling && f.ElementId == Chroniques.Ilsa);
    }

    [Fact]
    public void Doctor_ReferenceProject_HasNoDanglingLink()
    {
        ProjectDoctor.Diagnose(_project, fix: false).Findings.ShouldNotContain(f => f.Code == DoctorCodes.LinkDangling);
    }
}
