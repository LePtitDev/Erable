using Erable.Testing;

namespace Erable.Core.Tests;

/// <summary>
/// Example of the shared specification runner (docs/testing.md §5): the same file is executed by
/// src/web/src/test/fixtures.example.test.ts. Delete with spec/fixtures/example once real fixtures exist (M1-01).
/// </summary>
public sealed class SharedFixtureExampleTests
{
    public static TheoryData<FixtureCase> Cases => SharedFixtures.Load("example");

    [Theory]
    [MemberData(nameof(Cases))]
    public void Sum_SharedCases_MatchesExpected(FixtureCase fixture)
    {
        var c = fixture.As<SumCase>();

        (c.A + c.B).ShouldBe(c.Sum);
    }

    public sealed record SumCase(string Name, long A, long B, long Sum);
}
