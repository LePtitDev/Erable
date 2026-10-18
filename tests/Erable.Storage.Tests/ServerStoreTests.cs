using Erable.Core;
using Erable.Core.Accounts;
using Erable.Testing;

namespace Erable.Storage.Tests;

public sealed class ServerStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly ServerStore _store;

    public ServerStoreTests()
    {
        _store = ServerStore.OpenOrCreate(_dir.Path);
    }

    public void Dispose()
    {
        _store.Dispose();
        _dir.Dispose();
    }

    private static AccountRecord Account(string id, bool admin = false) => new(id, id.ToUpperInvariant(), "hash", admin, false, Now);

    [Fact]
    public void CreateAccount_DuplicateLogin_IsRefused()
    {
        _store.CreateAccount(Account("camille")).IsSuccess.ShouldBeTrue();

        _store.CreateAccount(Account("camille")).Error.Code.ShouldBe(ErrorCodes.AccountLoginTaken);
    }

    [Fact]
    public void GetAccount_AfterCreate_ReturnsEveryField()
    {
        _store.CreateAccount(new AccountRecord("lea", "Léa M.", "h", true, false, Now));

        _store.GetAccount("lea").ShouldBe(new AccountRecord("lea", "Léa M.", "h", true, false, Now));
        _store.GetAccount("absent").ShouldBeNull();
    }

    [Fact]
    public void OpenOrCreate_ExistingFile_KeepsTheData()
    {
        _store.CreateAccount(Account("malo"));
        _store.Dispose();

        using var reopened = ServerStore.OpenOrCreate(_dir.Path);

        reopened.GetAccount("malo").ShouldNotBeNull();
    }

    [Fact]
    public void FindSession_AfterAdd_ReturnsIt()
    {
        _store.CreateAccount(Account("malo"));
        _store.AddSession(new SessionRecord("h1", "malo", ServerStore.SessionKindCookie, Now, Now.AddHours(1)));

        _store.FindSession("h1")!.ExpiresAt.ShouldBe(Now.AddHours(1));
        _store.RemoveSession("h1").ShouldBeTrue();
        _store.FindSession("h1").ShouldBeNull();
    }

    [Fact]
    public void PurgeExpiredSessions_RemovesOnlyTheExpiredOnes()
    {
        _store.CreateAccount(Account("malo"));
        _store.AddSession(new SessionRecord("old", "malo", ServerStore.SessionKindCookie, Now, Now.AddHours(1)));
        _store.AddSession(new SessionRecord("new", "malo", ServerStore.SessionKindCookie, Now, Now.AddHours(5)));

        _store.PurgeExpiredSessions(Now.AddHours(2)).ShouldBe(1);

        _store.FindSession("old").ShouldBeNull();
        _store.FindSession("new").ShouldNotBeNull();
    }

    [Fact]
    public void SetPassword_EndsEverySessionOfTheAccount()
    {
        _store.CreateAccount(Account("malo"));
        _store.AddSession(new SessionRecord("s", "malo", ServerStore.SessionKindBearer, Now, Now.AddHours(1)));

        _store.SetPassword("malo", "new-hash").ShouldBeTrue();

        _store.FindSession("s").ShouldBeNull();
        _store.GetAccount("malo")!.PasswordHash.ShouldBe("new-hash");
    }

    [Fact]
    public void UpgradePasswordHash_KeepsTheSessions()
    {
        _store.CreateAccount(Account("malo"));
        _store.AddSession(new SessionRecord("s", "malo", ServerStore.SessionKindBearer, Now, Now.AddHours(1)));
        var current = _store.GetAccount("malo")!.PasswordHash;

        _store.UpgradePasswordHash("malo", current, "stronger-hash").ShouldBeTrue();

        _store.FindSession("s").ShouldNotBeNull();
        _store.GetAccount("malo")!.PasswordHash.ShouldBe("stronger-hash");
    }

    [Fact]
    public void UpgradePasswordHash_AfterAConcurrentChange_KeepsTheNewPassword()
    {
        _store.CreateAccount(Account("malo"));
        var current = _store.GetAccount("malo")!.PasswordHash;
        _store.SetPassword("malo", "changed-meanwhile");

        _store.UpgradePasswordHash("malo", current, "stronger-hash").ShouldBeFalse();

        _store.GetAccount("malo")!.PasswordHash.ShouldBe("changed-meanwhile");
    }

    [Fact]
    public void UpdateAccount_Disabling_EndsSessions()
    {
        _store.CreateAccount(Account("malo"));
        _store.AddSession(new SessionRecord("s", "malo", ServerStore.SessionKindBearer, Now, Now.AddHours(1)));

        _store.UpdateAccount("malo", "Malo", false, disabled: true);

        _store.FindSession("s").ShouldBeNull();
    }

    [Fact]
    public void GetRole_NonMember_IsNull()
    {
        var project = ElementId.NewRandom();
        _store.CreateAccount(Account("externe"));
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));

        _store.GetRole(project, "externe").ShouldBeNull();
    }

    [Fact]
    public void SetMember_ChangesTheRole()
    {
        var project = ElementId.NewRandom();
        _store.CreateAccount(Account("lea"));
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));

        _store.SetMember(project, "lea", ProjectRole.Reader);
        _store.SetMember(project, "lea", ProjectRole.Editor);

        _store.GetRole(project, "lea").ShouldBe(ProjectRole.Editor);
        _store.ListMembers(project).Count.ShouldBe(1);
    }

    [Fact]
    public void GetRole_DisabledAccount_IsNull()
    {
        var project = ElementId.NewRandom();
        _store.CreateAccount(Account("lea"));
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));
        _store.SetMember(project, "lea", ProjectRole.Admin);

        _store.UpdateAccount("lea", "Léa", false, disabled: true);

        _store.GetRole(project, "lea").ShouldBeNull();
        _store.CountAdmins(project).ShouldBe(0);
    }

    [Fact]
    public void ListProjectsOf_ReturnsOnlyTheMembershipsOfTheUser()
    {
        var a = ElementId.NewRandom();
        var b = ElementId.NewRandom();
        _store.CreateAccount(Account("lea"));
        _store.CreateAccount(Account("externe"));
        _store.RegisterProject(new ProjectRegistration(a, "a", Now));
        _store.RegisterProject(new ProjectRegistration(b, "b", Now.AddMinutes(1)));
        _store.SetMember(a, "lea", ProjectRole.Reader);
        _store.SetMember(b, "externe", ProjectRole.Admin);

        var projects = _store.ListProjectsOf("lea");

        projects.Select(p => (p.Project.ProjectId, p.Role)).ShouldBe([(a, ProjectRole.Reader)]);
    }

    [Fact]
    public void UnregisterProject_RemovesItsMembers()
    {
        var project = ElementId.NewRandom();
        _store.CreateAccount(Account("lea"));
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));
        _store.SetMember(project, "lea", ProjectRole.Reader);

        _store.UnregisterProject(project).ShouldBeTrue();

        _store.GetRole(project, "lea").ShouldBeNull();
        _store.IsRegistered(project).ShouldBeFalse();
    }

    [Fact]
    public void RemoveMember_Member_RemovesIt()
    {
        var project = ElementId.NewRandom();
        _store.CreateAccount(Account("lea"));
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));
        _store.SetMember(project, "lea", ProjectRole.Reader);

        _store.RemoveMember(project, "lea").ShouldBeTrue();
        _store.RemoveMember(project, "lea").ShouldBeFalse();
    }

    [Fact]
    public void ChangeRole_UnknownOrDisabledAccount_IsRefused()
    {
        var project = ElementId.NewRandom();
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));
        _store.CreateAccount(Account("lea"));
        _store.UpdateAccount("lea", "Léa", false, disabled: true);

        _store.ChangeRole(project, "personne", ProjectRole.Reader).ShouldBe(MemberChange.UnknownAccount);
        _store.ChangeRole(project, "lea", ProjectRole.Reader).ShouldBe(MemberChange.UnknownAccount);
    }

    [Fact]
    public void ChangeRole_TheLastAdministratorOut_IsRefusedAndNothingChanges()
    {
        var project = ElementId.NewRandom();
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));
        _store.CreateAccount(Account("admin"));
        _store.CreateAccount(Account("malo"));
        _store.SetMember(project, "admin", ProjectRole.Admin);

        _store.ChangeRole(project, "admin", ProjectRole.Editor).ShouldBe(MemberChange.LastAdministrator);
        _store.RemoveMemberChecked(project, "admin").ShouldBe(MemberChange.LastAdministrator);
        _store.GetRole(project, "admin").ShouldBe(ProjectRole.Admin);

        // With another administrator the first one may leave.
        _store.ChangeRole(project, "malo", ProjectRole.Admin).ShouldBe(MemberChange.Done);
        _store.RemoveMemberChecked(project, "admin").ShouldBe(MemberChange.Done);
        _store.GetRole(project, "admin").ShouldBeNull();
    }

    [Fact]
    public void RemoveMemberChecked_NotAMember_ReportsIt()
    {
        var project = ElementId.NewRandom();
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));
        _store.CreateAccount(Account("lea"));

        _store.RemoveMemberChecked(project, "lea").ShouldBe(MemberChange.NotAMember);
    }

    [Fact]
    public void ChangeRole_TwoAdministratorsLeavingAtOnce_OneStays()
    {
        // The check and the write are one step: whatever the interleaving, an administrator remains.
        var project = ElementId.NewRandom();
        _store.RegisterProject(new ProjectRegistration(project, "p", Now));
        _store.CreateAccount(Account("a"));
        _store.CreateAccount(Account("b"));
        _store.SetMember(project, "a", ProjectRole.Admin);
        _store.SetMember(project, "b", ProjectRole.Admin);

        Parallel.Invoke(
            () => _store.RemoveMemberChecked(project, "a"),
            () => _store.RemoveMemberChecked(project, "b"));

        _store.CountAdmins(project).ShouldBe(1);
    }
}
