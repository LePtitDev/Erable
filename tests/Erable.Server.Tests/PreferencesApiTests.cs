using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Server.Hosting;
using Erable.Server.Tests.Infrastructure;

namespace Erable.Server.Tests;

public sealed class PreferencesApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_NeverWritten_ReturnsDefaults()
    {
        await using var backend = await TestBackend.StartAsync();

        var read = await backend.Client.GetFromJsonAsync<JsonObject>("/api/v1/me/preferences", Ct);

        read!["language"].ShouldBeNull();
        read["theme"]!.GetValue<string>().ShouldBe("system");
        read["shortcuts"]!.AsObject().Count.ShouldBe(0);
    }

    [Fact]
    public async Task PutThenGet_Preferences_RoundTrip()
    {
        await using var backend = await TestBackend.StartAsync();

        using var put = await backend.Client.PutAsJsonAsync(
            "/api/v1/me/preferences", new { language = "en", theme = "dark", shortcuts = new { search = "Ctrl+K" } }, Ct);
        var read = await backend.Client.GetFromJsonAsync<JsonObject>("/api/v1/me/preferences", Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        read!["language"]!.GetValue<string>().ShouldBe("en");
        read["theme"]!.GetValue<string>().ShouldBe("dark");
        read["shortcuts"]!["search"]!.GetValue<string>().ShouldBe("Ctrl+K");
    }

    [Fact]
    public async Task Put_UnsupportedLanguage_Returns400WithCode()
    {
        await using var backend = await TestBackend.StartAsync();

        using var put = await backend.Client.PutAsJsonAsync("/api/v1/me/preferences", new { language = "it", theme = "light" }, Ct);
        var problem = await put.Content.ReadFromJsonAsync<JsonObject>(Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        problem!["code"]!.GetValue<string>().ShouldBe("preferences.languageUnsupported");
    }

    [Fact]
    public async Task Put_UnknownTheme_Returns400()
    {
        await using var backend = await TestBackend.StartAsync();

        using var put = await backend.Client.PutAsJsonAsync("/api/v1/me/preferences", new { language = "fr", theme = "sepia" }, Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_SpaShell_CarriesTheSavedLanguageAndTheme()
    {
        await using var backend = await TestBackend.StartAsync();
        using var put = await backend.Client.PutAsJsonAsync("/api/v1/me/preferences", new { language = "de", theme = "dark" }, Ct);

        var html = await backend.Client.GetStringAsync(new Uri("/", UriKind.Relative), Ct);

        html.ShouldContain("<html lang=\"de\" data-theme=\"dark\">");
    }

    [Fact]
    public async Task Preferences_NewBackendOnTheSameProfile_AreKept()
    {
        var profile = Directory.CreateTempSubdirectory("erable-profile-").FullName;
        try
        {
            await using (var first = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop, DataDirectory = profile }))
            {
                using var put = await first.Client.PutAsJsonAsync("/api/v1/me/preferences", new { language = "es", theme = "light" }, Ct);
                put.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            // A desktop restart: another port, the same application profile.
            await using var second = await TestBackend.StartAsync(o => o with { Mode = HostMode.Desktop, DataDirectory = profile });
            var read = await second.Client.GetFromJsonAsync<JsonObject>("/api/v1/me/preferences", Ct);

            read!["language"]!.GetValue<string>().ShouldBe("es");
            read["theme"]!.GetValue<string>().ShouldBe("light");
        }
        finally
        {
            Directory.Delete(profile, recursive: true);
        }
    }
}
