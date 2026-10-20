using System.Net;
using System.Net.Http.Json;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Tests.Infrastructure;

namespace Erable.Server.Tests;

/// <summary>The name a user is shown under, changed from the preferences (F15).</summary>
public sealed class DisplayNameApiTests
{
    private const string MeName = "/api/v1/me/name";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RenameMe_Desktop_MeReturnsTheNewName()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop });

        using var put = await backend.Client.PutAsJsonAsync(MeName, new RenameMeRequest("  Camille  "), Ct);
        var me = await backend.Client.GetFromJsonAsync<CurrentUserInfo>("/api/v1/me", Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await put.ReadAsync<CurrentUserInfo>()).Name.ShouldBe("Camille");
        me!.Name.ShouldBe("Camille");
    }

    [Fact]
    public async Task RenameMe_Desktop_AuthorsUseTheNewName()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop });
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa");

        using var put = await backend.Client.PutAsJsonAsync(MeName, new RenameMeRequest("Camille"), Ct);
        var read = await backend.Client.GetFromJsonAsync<ElementDetails>($"/api/v1/projects/{project.Id}/elements/{page.Id}", ApiJson.Options, Ct);

        read!.CreatedBy.ShouldBe("local");
        read.CreatedByName.ShouldBe("Camille");
    }

    [Fact]
    public async Task RenameMe_Desktop_NewBackendOnTheSameProfile_KeepsTheName()
    {
        var profile = Directory.CreateTempSubdirectory("erable-profile-").FullName;
        try
        {
            await using (var first = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop, DataDirectory = profile }))
            {
                using var put = await first.Client.PutAsJsonAsync(MeName, new RenameMeRequest("Camille"), Ct);
                put.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            await using var second = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop, DataDirectory = profile });
            var me = await second.Client.GetFromJsonAsync<CurrentUserInfo>("/api/v1/me", Ct);

            me!.Name.ShouldBe("Camille");
        }
        finally
        {
            Directory.Delete(profile, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Line\nbreak")]
    public async Task RenameMe_InvalidName_Returns422AndKeepsTheName(string name)
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop });
        var before = await backend.Client.GetFromJsonAsync<CurrentUserInfo>("/api/v1/me", Ct);

        using var put = await backend.Client.PutAsJsonAsync(MeName, new RenameMeRequest(name), Ct);
        var after = await backend.Client.GetFromJsonAsync<CurrentUserInfo>("/api/v1/me", Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await put.ProblemCodeAsync()).ShouldBe(ErrorCodes.AccountNameInvalid);
        after!.Name.ShouldBe(before!.Name);
    }

    [Fact]
    public async Task RenameMe_TooLongName_Returns422()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop });

        using var put = await backend.Client.PutAsJsonAsync(MeName, new RenameMeRequest(new string('a', AccountRules.MaxDisplayNameLength + 1)), Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task RenameMe_Server_ChangesTheAccountNameSeenByOthers()
    {
        await using var backend = await TestBackend.StartAsync();
        using var camille = backend.ClientFor("camille");

        using var put = await camille.PutAsJsonAsync(MeName, new RenameMeRequest("Camille R."), Ct);
        var accounts = await backend.Client.GetFromJsonAsync<AccountItem[]>("/api/v1/accounts", ApiJson.Options, Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        accounts!.Single(a => a.Id == "camille").Name.ShouldBe("Camille R.");
    }

    [Fact]
    public async Task RenameMe_Server_KeepsTheSessionAndTheRights()
    {
        await using var backend = await TestBackend.StartAsync();
        using var camille = backend.ClientFor("camille", serverAdmin: true);

        using var put = await camille.PutAsJsonAsync(MeName, new RenameMeRequest("Camille R."), Ct);
        var me = await camille.GetFromJsonAsync<CurrentUserInfo>("/api/v1/me", Ct);

        me!.Name.ShouldBe("Camille R.");
        me.IsServerAdmin.ShouldBeTrue();
    }

    [Fact]
    public async Task RenameMe_Anonymous_Returns401()
    {
        await using var backend = await TestBackend.StartAsync();
        using var anonymous = backend.NewAnonymousClient();

        using var put = await anonymous.PutAsJsonAsync(MeName, new RenameMeRequest("Intrus"), Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
