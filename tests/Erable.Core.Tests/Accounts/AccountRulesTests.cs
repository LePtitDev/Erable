using Erable.Core.Accounts;

namespace Erable.Core.Tests.Accounts;

public sealed class AccountRulesTests
{
    [Theory]
    [InlineData("Camille", "camille")]
    [InlineData("  malo  ", "malo")]
    [InlineData("lea.m-2_x", "lea.m-2_x")]
    public void NormalizeLogin_ValidLogin_IsTrimmedAndLowercased(string input, string expected)
    {
        AccountRules.NormalizeLogin(input).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("has space")]
    [InlineData("accentué")]
    [InlineData(".hidden")]
    [InlineData("../etc")]
    [InlineData("local")]
    public void NormalizeLogin_InvalidLogin_IsRefused(string? input)
    {
        AccountRules.NormalizeLogin(input).Error.Code.ShouldBe(ErrorCodes.AccountLoginInvalid);
    }

    [Fact]
    public void NormalizeLogin_TooLong_IsRefused()
    {
        AccountRules.NormalizeLogin(new string('a', AccountRules.MaxLoginLength + 1)).IsSuccess.ShouldBeFalse();
        AccountRules.NormalizeLogin(new string('a', AccountRules.MaxLoginLength)).IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void NormalizeDisplayName_Empty_IsRefused(string input)
    {
        AccountRules.NormalizeDisplayName(input).Error.Code.ShouldBe(ErrorCodes.AccountNameInvalid);
    }

    [Fact]
    public void NormalizeDisplayName_WithControlCharacter_IsRefused()
    {
        AccountRules.NormalizeDisplayName("Ca\nmille").IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ValidatePassword_ByLengthOnly()
    {
        AccountRules.ValidatePassword("short").ShouldNotBeNull();
        AccountRules.ValidatePassword(null).ShouldNotBeNull();
        AccountRules.ValidatePassword("aaaaaaaa").ShouldBeNull();
        AccountRules.ValidatePassword(new string('x', AccountRules.MaxPasswordLength + 1)).ShouldNotBeNull();
    }

    [Fact]
    public void Allows_RolesAreOrdered()
    {
        ProjectRoles.Allows(ProjectRole.Admin, ProjectRole.Editor).ShouldBeTrue();
        ProjectRoles.Allows(ProjectRole.Editor, ProjectRole.Editor).ShouldBeTrue();
        ProjectRoles.Allows(ProjectRole.Reader, ProjectRole.Editor).ShouldBeFalse();
        ProjectRoles.Allows(ProjectRole.Editor, ProjectRole.Admin).ShouldBeFalse();
    }

    [Theory]
    [InlineData(ProjectRole.Reader, "reader")]
    [InlineData(ProjectRole.Editor, "editor")]
    [InlineData(ProjectRole.Admin, "admin")]
    public void NameOf_RoundTripsThroughTryParse(ProjectRole role, string name)
    {
        ProjectRoles.NameOf(role).ShouldBe(name);
        ProjectRoles.TryParse(name, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(role);
    }

    [Fact]
    public void TryParse_UnknownName_Fails()
    {
        ProjectRoles.TryParse("owner", out _).ShouldBeFalse();
    }
}
