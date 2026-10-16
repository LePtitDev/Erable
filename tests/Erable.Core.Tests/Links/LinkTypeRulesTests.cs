using Erable.Core;
using Erable.Core.Links;

namespace Erable.Core.Tests.Links;

public sealed class LinkTypeRulesTests
{
    [Fact]
    public void Normalize_TrimsTheFields()
    {
        var result = LinkTypeRules.Normalize("  Un mentor forme un élève.  ", "  Élève de ");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new LinkTypeFields("Un mentor forme un élève.", "Élève de"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_BlankInverse_MeansNone(string? inverse)
    {
        LinkTypeRules.Normalize("", inverse).Value.InverseName.ShouldBeNull();
    }

    [Fact]
    public void Normalize_NullDescription_IsEmpty()
    {
        LinkTypeRules.Normalize(null, null).Value.Description.ShouldBe(string.Empty);
    }

    [Fact]
    public void Normalize_DescriptionTooLong_IsRefused()
    {
        var result = LinkTypeRules.Normalize(new string('a', LinkTypeRules.MaxDescription + 1), null);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(ErrorCodes.LinkTypeDescriptionTooLong);
    }

    [Theory]
    [InlineData("tab\there")]
    [InlineData("line\nbreak")]
    public void Normalize_InverseWithControlCharacters_IsRefused(string inverse)
    {
        LinkTypeRules.Normalize("", inverse).Error.Code.ShouldBe(ErrorCodes.LinkTypeInverseInvalid);
    }

    [Fact]
    public void Normalize_InverseTooLong_IsRefused()
    {
        LinkTypeRules.Normalize("", new string('a', LinkTypeRules.MaxInverseName + 1)).Error.Code
            .ShouldBe(ErrorCodes.LinkTypeInverseInvalid);
    }
}
