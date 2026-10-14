using Erable.Storage;
using Erable.Testing;

namespace Erable.Storage.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void StorageAssembly_Name_IsErableStorage()
    {
        StorageAssembly.Assembly.GetName().Name.ShouldBe("Erable.Storage");
    }

    [Fact]
    public void TempDirectory_Dispose_DeletesFolderAndContent()
    {
        string path;
        using (var dir = TempDirectory.Create())
        {
            path = dir.Path;
            Directory.CreateDirectory(dir.Combine("blobs", "3a"));
            File.WriteAllText(dir.Combine("erable.json"), "{}");
            Directory.Exists(path).ShouldBeTrue();
        }

        Directory.Exists(path).ShouldBeFalse();
    }
}
