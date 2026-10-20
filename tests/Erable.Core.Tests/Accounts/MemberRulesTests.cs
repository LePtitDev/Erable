using CsCheck;
using Erable.Core.Accounts;

namespace Erable.Core.Tests.Accounts;

/// <summary>A project is never left without an administrator.</summary>
public sealed class MemberRulesTests
{
    private static MemberRoleEntry Member(string id, ProjectRole role, bool active = true) => new(id, role, active);

    [Fact]
    public void LeavesNoAdministrator_RemovingTheOnlyAdministrator_IsRefused()
    {
        MemberRules.LeavesNoAdministrator([Member("admin", ProjectRole.Admin), Member("camille", ProjectRole.Editor)], "admin", null).ShouldBeTrue();
    }

    [Fact]
    public void LeavesNoAdministrator_DemotingTheOnlyAdministrator_IsRefused()
    {
        MemberRules.LeavesNoAdministrator([Member("admin", ProjectRole.Admin)], "admin", ProjectRole.Editor).ShouldBeTrue();
    }

    [Fact]
    public void LeavesNoAdministrator_WithAnotherAdministrator_IsAllowed()
    {
        MemberRules.LeavesNoAdministrator([Member("admin", ProjectRole.Admin), Member("malo", ProjectRole.Admin)], "admin", null).ShouldBeFalse();
    }

    [Fact]
    public void LeavesNoAdministrator_ADisabledAdministratorDoesNotCount()
    {
        var members = new[] { Member("admin", ProjectRole.Admin), Member("ancien", ProjectRole.Admin, active: false) };

        MemberRules.LeavesNoAdministrator(members, "admin", null).ShouldBeTrue();
    }

    [Fact]
    public void LeavesNoAdministrator_PromotingSomeoneElseFirst_Allows()
    {
        // admin demotes himself in the same change as malo's promotion? No: one change at a time, malo first.
        var members = new[] { Member("admin", ProjectRole.Admin), Member("malo", ProjectRole.Editor) };

        MemberRules.LeavesNoAdministrator(members, "malo", ProjectRole.Admin).ShouldBeFalse();
    }

    [Fact]
    public void LeavesNoAdministrator_ChangingAnEditor_NeverMatters()
    {
        var members = new[] { Member("admin", ProjectRole.Admin), Member("camille", ProjectRole.Editor) };

        MemberRules.LeavesNoAdministrator(members, "camille", ProjectRole.Reader).ShouldBeFalse();
        MemberRules.LeavesNoAdministrator(members, "camille", null).ShouldBeFalse();
    }

    [Fact]
    public void LeavesNoAdministrator_AProjectWithoutAdministratorAlready_IsNotBlocked()
    {
        MemberRules.LeavesNoAdministrator([Member("camille", ProjectRole.Editor)], "camille", null).ShouldBeFalse();
    }

    [Fact]
    public void LeavesNoAdministrator_AnyAllowedSequence_KeepsAnAdministrator()
    {
        var users = new[] { "a", "b", "c", "d" };
        var change = Gen.Select(Gen.Int[0, 3], Gen.Int[0, 3]);
        change.List[1, 40].Sample(changes =>
        {
            var members = new List<MemberRoleEntry> { Member("a", ProjectRole.Admin) };
            foreach (var (who, what) in changes)
            {
                var user = users[who];
                ProjectRole? role = what == 3 ? null : (ProjectRole)(what + 1);
                if (MemberRules.LeavesNoAdministrator(members, user, role))
                {
                    continue;
                }

                members.RemoveAll(m => m.UserId == user);
                if (role is { } value)
                {
                    members.Add(Member(user, value));
                }

                members.Any(m => m.Role == ProjectRole.Admin).ShouldBeTrue();
            }
        });
    }
}
