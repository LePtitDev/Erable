using Erable.Core.Projects;
using Erable.Server.Cli;
using Erable.Server.Hosting;
using Erable.Storage;
using Erable.Testing;

namespace Erable.Server.Tests;

/// <summary>`erable serve`: command line and ERABLE_* variables become host options.</summary>
public sealed class ServeConfigurationTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] variables)
    {
        var map = variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
        return name => map.GetValueOrDefault(name);
    }

    private static ErableHostOptions Resolve(ServeArguments arguments, Func<string, string?>? env = null)
    {
        var plan = ServeConfiguration.Resolve(arguments, env ?? Env(), applicationData: Path.GetTempPath());
        plan.IsSuccess.ShouldBeTrue(plan.IsSuccess ? string.Empty : plan.Error.Message);
        return plan.Value.Options;
    }

    private static string Error(ServeArguments arguments, Func<string, string?> env) =>
        ServeConfiguration.Resolve(arguments, env, Path.GetTempPath()).Error.Message;

    [Fact]
    public void Resolve_NothingGiven_ListensOnThisMachineOnly()
    {
        using var data = TempDirectory.Create();

        var options = Resolve(new ServeArguments(data.Path));

        options.Mode.ShouldBe(HostMode.Server);
        options.Address.ShouldBe("127.0.0.1");
        options.Port.ShouldBe(ServeConfiguration.DefaultPort);
        options.DataDirectory.ShouldBe(data.Path);
        options.SharedProjectFolder.ShouldBeNull();
        options.ProjectQuota.ShouldBeNull();
        options.TrustProxy.ShouldBeFalse();
        options.CookieSecure.ShouldBeFalse();
        options.MaxAttachmentSize.ShouldBe(ErableHostOptions.DefaultMaxAttachmentSize);
        options.SessionLifetime.ShouldBe(ErableHostOptions.DefaultSessionLifetime);
    }

    [Fact]
    public void Resolve_Lan_ListensOnEveryInterface()
    {
        using var data = TempDirectory.Create();

        Resolve(new ServeArguments(data.Path, Lan: true)).Address.ShouldBe("0.0.0.0");
    }

    [Fact]
    public void Resolve_Environment_FillsWhatTheCommandLineDoesNotSay()
    {
        using var data = TempDirectory.Create();
        var env = Env(
            ("ERABLE_PORT", "8080"), ("ERABLE_ADDRESS", "0.0.0.0"), ("ERABLE_MAX_ATTACHMENT_MB", "64"),
            ("ERABLE_PROJECT_QUOTA_MB", "2048"), ("ERABLE_PUBLIC_URL", "https://wiki.example.org"),
            ("ERABLE_CORS_ORIGINS", "https://a.example.org, https://b.example.org"), ("ERABLE_TRUST_PROXY", "true"),
            ("ERABLE_SESSION_HOURS", "24"), ("ERABLE_ADMIN_LOGIN", "root"), ("ERABLE_ADMIN_PASSWORD", "secret-password"));

        var options = Resolve(new ServeArguments(data.Path), env);

        options.Port.ShouldBe(8080);
        options.Address.ShouldBe("0.0.0.0");
        options.MaxAttachmentSize.ShouldBe(64L * 1024 * 1024);
        options.ProjectQuota.ShouldBe(2048L * 1024 * 1024);
        options.PublicUrl.ShouldBe("https://wiki.example.org");
        options.CorsOrigins.ShouldBe(["https://a.example.org", "https://b.example.org"]);
        options.TrustProxy.ShouldBeTrue();
        options.CookieSecure.ShouldBeTrue("an https public address makes the cookie Secure");
        options.SessionLifetime.ShouldBe(TimeSpan.FromHours(24));
        options.BootstrapAdminLogin.ShouldBe("root");
        options.BootstrapAdminPassword.ShouldBe("secret-password");
    }

    [Fact]
    public void Resolve_CommandLine_WinsOverTheEnvironment()
    {
        using var data = TempDirectory.Create();
        var env = Env(("ERABLE_PORT", "8080"), ("ERABLE_ADDRESS", "0.0.0.0"), ("ERABLE_MAX_ATTACHMENT_MB", "64"));

        var options = Resolve(new ServeArguments(data.Path, Port: 5090, Address: "10.0.0.5", MaxAttachmentMb: 8), env);

        options.Port.ShouldBe(5090);
        options.Address.ShouldBe("10.0.0.5");
        options.MaxAttachmentSize.ShouldBe(8L * 1024 * 1024);
    }

    [Fact]
    public void Resolve_ProjectFolder_IsSharedAsItIsAndItsAccountsStayElsewhere()
    {
        using var folder = TempDirectory.Create();
        using var appData = TempDirectory.Create();
        var project = Path.Combine(folder.Path, "MonWiki");
        ProjectStore.Create(project, "Mon wiki", "fr", TimeProvider.System).Value.Dispose();
        var manifest = ManifestFile.Read(project).Value;

        var plan = ServeConfiguration.Resolve(new ServeArguments(project), Env(), appData.Path).Value;

        plan.SharedProject.ShouldBeTrue();
        plan.Options.SharedProjectFolder.ShouldBe(project);
        plan.Options.DataDirectory.ShouldBe(Path.Combine(appData.Path, "Erable", "serve", manifest.ProjectId.ToString()));
        plan.Options.DataDirectory!.ShouldNotStartWith(project);
    }

    [Theory]
    [InlineData("ERABLE_PORT", "eighty")]
    [InlineData("ERABLE_PORT", "70000")]
    [InlineData("ERABLE_ADDRESS", "not-an-ip")]
    [InlineData("ERABLE_MAX_ATTACHMENT_MB", "0")]
    [InlineData("ERABLE_PROJECT_QUOTA_MB", "-1")]
    [InlineData("ERABLE_PUBLIC_URL", "wiki.example.org")]
    [InlineData("ERABLE_CORS_ORIGINS", "https://ok.example.org,nope")]
    [InlineData("ERABLE_TRUST_PROXY", "maybe")]
    [InlineData("ERABLE_SESSION_HOURS", "0")]
    public void Resolve_InvalidSetting_StopsTheServerAndNamesIt(string name, string value)
    {
        using var data = TempDirectory.Create();

        var message = Error(new ServeArguments(data.Path), Env((name, value)));

        message.ShouldContain(name);
    }

    [Fact]
    public void Resolve_InvalidPortOnTheCommandLine_IsRefused()
    {
        using var data = TempDirectory.Create();

        Error(new ServeArguments(data.Path, Port: 99999), Env()).ShouldContain("--port");
    }
}
