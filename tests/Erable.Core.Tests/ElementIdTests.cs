using System.Text.Json;
using CsCheck;
using Erable.Testing;

namespace Erable.Core.Tests;

public sealed class ElementIdTests
{
    public static TheoryData<FixtureCase> Cases => SharedFixtures.Load("ids");

    [Theory]
    [MemberData(nameof(Cases))]
    public void TryParse_SharedCases_MatchesSpecification(FixtureCase fixture)
    {
        var c = fixture.As<IdCase>();

        var ok = ElementId.TryParse(c.Input, out var id);

        ok.ShouldBe(c.Valid);
        if (c.Valid)
        {
            id.ToString().ShouldBe(c.Canonical);
            id.ToDisplayString().ShouldBe(c.Display);
        }
        else
        {
            id.ShouldBe(ElementId.None);
        }
    }

    [Fact]
    public void ParseFormat_AnyNonZeroValue_RoundTrips()
    {
        Gen.ULong.Where(v => v != 0).Sample(value =>
        {
            var id = new ElementId(value);
            ElementId.Parse(id.ToString()).ShouldBe(id);
            ElementId.Parse(id.ToString().ToLowerInvariant()).ShouldBe(id);
        });
    }

    [Fact]
    public void Int64Conversion_AnyValue_IsLossless()
    {
        Gen.ULong.Sample(value =>
        {
            var id = new ElementId(value);
            ElementId.FromInt64(id.ToInt64()).ShouldBe(id);
        });
    }

    [Fact]
    public void ToInt64_SignBitSet_IsNegative()
    {
        ElementId.Parse("8000000000000000").ToInt64().ShouldBe(long.MinValue);
    }

    [Fact]
    public void TryParse_AnyStringOfOtherLength_Fails()
    {
        Gen.String.Where(s => s.Length != ElementId.Length).Sample(s => ElementId.TryParse(s, out _).ShouldBeFalse());
    }

    [Fact]
    public void TryParse_Null_Fails()
    {
        ElementId.TryParse((string?)null, out var id).ShouldBeFalse();
        id.IsNone.ShouldBeTrue();
    }

    [Fact]
    public void Parse_Invalid_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => ElementId.Parse("0000000000000000"));
    }

    [Fact]
    public void NewRandom_ManyDraws_AreNonZeroAndDistinct()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => ElementId.NewRandom()).ToList();

        ids.ShouldAllBe(id => !id.IsNone);
        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void Serialize_Id_WritesCanonicalString()
    {
        var json = JsonSerializer.Serialize(new Holder(ElementId.Parse("3f2a9c14e07b5d21")));

        json.ShouldBe("""{"Id":"3F2A9C14E07B5D21"}""");
    }

    [Fact]
    public void Deserialize_CanonicalString_ReadsId()
    {
        var holder = JsonSerializer.Deserialize<Holder>("""{"Id":"3f2a9c14e07b5d21"}""");

        holder.ShouldNotBeNull().Id.ShouldBe(ElementId.Parse("3F2A9C14E07B5D21"));
    }

    [Theory]
    [InlineData("""{"Id":4551684617428835617}""")]
    [InlineData("""{"Id":1}""")]
    [InlineData("""{"Id":null}""")]
    [InlineData("""{"Id":"0000000000000000"}""")]
    [InlineData("""{"Id":"3F2A 9C14 E07B 5D21"}""")]
    public void Deserialize_NumberOrInvalidString_Throws(string json)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Holder>(json));
    }

    [Fact]
    public void Serialize_DictionaryKey_UsesCanonicalString()
    {
        var id = ElementId.Parse("7A1C4E90B3F2D615");
        var json = JsonSerializer.Serialize(new Dictionary<ElementId, int> { [id] = 1 });

        json.ShouldBe("""{"7A1C4E90B3F2D615":1}""");
        JsonSerializer.Deserialize<Dictionary<ElementId, int>>(json).ShouldNotBeNull()[id].ShouldBe(1);
    }

    [Fact]
    public void IParsable_Interface_IsUsableGenerically()
    {
        static T ParseGeneric<T>(string s)
            where T : IParsable<T> => T.Parse(s, null);

        ParseGeneric<ElementId>("3F2A9C14E07B5D21").ToString().ShouldBe("3F2A9C14E07B5D21");
    }

    private sealed record Holder(ElementId Id);

    public sealed record IdCase(string Name, string Input, bool Valid, string? Canonical, string? Display);
}
