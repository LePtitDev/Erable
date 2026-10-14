using Erable.Storage;

namespace Erable.Storage.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void StorageAssembly_Name_IsErableStorage()
    {
        StorageAssembly.Assembly.GetName().Name.ShouldBe("Erable.Storage");
    }
}
