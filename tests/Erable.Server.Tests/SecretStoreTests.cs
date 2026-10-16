using System.Runtime.Versioning;
using System.Text;
using Erable.Server.Servers;
using Erable.Testing;

namespace Erable.Server.Tests;

/// <summary>The vault of the tokens of the servers (M4-07): never in clear text on disk.</summary>
public sealed class SecretStoreTests
{
    [Fact]
    public void Memory_StoresReadsAndDeletes()
    {
        var store = new MemorySecretStore();

        store.Write("server-A", "secret").ShouldBeTrue();
        store.Read("server-A").ShouldBe("secret");
        store.Read("server-B").ShouldBeNull();
        store.Delete("server-A");
        store.Read("server-A").ShouldBeNull();
        store.IsPersistent.ShouldBeFalse();
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Dpapi_RoundTripsAndNeverWritesTheSecretInClearText()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI exists on Windows only");
        using var dir = TempDirectory.Create();
        var store = new DpapiSecretStore(dir.Path);
        const string Secret = "tokenWITH-some_chars0123456789";

        store.Write("server-A", Secret).ShouldBeTrue();

        store.Read("server-A").ShouldBe(Secret);
        store.IsPersistent.ShouldBeTrue();
        var files = Directory.GetFiles(dir.Path);
        files.Length.ShouldBe(1);
        Encoding.UTF8.GetString(File.ReadAllBytes(files[0])).ShouldNotContain(Secret);
        Convert.ToBase64String(File.ReadAllBytes(files[0])).ShouldNotContain(Convert.ToBase64String(Encoding.UTF8.GetBytes(Secret)));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Dpapi_AKeyDoesNotOpenAnotherKeysSecret()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI exists on Windows only");
        using var dir = TempDirectory.Create();
        var store = new DpapiSecretStore(dir.Path);
        store.Write("server-A", "un").ShouldBeTrue();
        store.Write("server-B", "deux").ShouldBeTrue();

        // Swapping the files of two keys does not let one read the other: the entropy is part of the key.
        var files = Directory.GetFiles(dir.Path);
        var a = File.ReadAllBytes(files[0]);
        var b = File.ReadAllBytes(files[1]);
        File.WriteAllBytes(files[0], b);
        File.WriteAllBytes(files[1], a);

        store.Read("server-A").ShouldBeNull();
        store.Read("server-B").ShouldBeNull();
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Dpapi_DamagedFileOrMissingKey_ReadsAsNothing()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI exists on Windows only");
        using var dir = TempDirectory.Create();
        var store = new DpapiSecretStore(dir.Path);
        store.Write("server-A", "secret").ShouldBeTrue();
        File.WriteAllBytes(Directory.GetFiles(dir.Path)[0], [1, 2, 3]);

        store.Read("server-A").ShouldBeNull();
        store.Read("never-written").ShouldBeNull();
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void Dpapi_Delete_RemovesTheSecret()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI exists on Windows only");
        using var dir = TempDirectory.Create();
        var store = new DpapiSecretStore(dir.Path);
        store.Write("server-A", "secret").ShouldBeTrue();

        store.Delete("server-A");

        store.Read("server-A").ShouldBeNull();
        Directory.GetFiles(dir.Path).ShouldBeEmpty();
    }

    [Fact]
    public void SecretStores_OnThisSystem_ChoosesAVaultOrFallsBackToMemory()
    {
        using var dir = TempDirectory.Create();

        var store = SecretStores.Create(dir.Path);

        store.ShouldNotBeNull();
        (store is MemorySecretStore || store.IsPersistent).ShouldBeTrue();
    }
}
