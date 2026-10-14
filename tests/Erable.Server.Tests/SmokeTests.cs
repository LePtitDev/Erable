namespace Erable.Server.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ServerAssembly_IsNamedErable()
    {
        typeof(Program).Assembly.GetName().Name.ShouldBe("erable");
    }
}
