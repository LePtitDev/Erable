using System.Drawing;
using Erable.Server.Projects;
using Erable.Testing;

namespace Erable.Desktop.Tests;

public sealed class WindowPlacementTests
{
    private static readonly Rectangle[] OneScreen = [new(0, 0, 1920, 1040)];

    [Fact]
    public void Load_AfterSave_ReturnsThePlacement()
    {
        using var dir = TempDirectory.Create();
        var profile = new UserProfile(dir.Path);
        var placement = new WindowPlacement(120, 80, 1400, 900, Maximized: true);

        placement.Save(profile);

        WindowPlacement.Load(profile).ShouldBe(placement);
    }

    [Fact]
    public void Load_NoFile_ReturnsNull()
    {
        using var dir = TempDirectory.Create();

        WindowPlacement.Load(new UserProfile(dir.Path)).ShouldBeNull();
    }

    [Fact]
    public void Load_UnreadableFile_ReturnsNull()
    {
        using var dir = TempDirectory.Create();
        File.WriteAllText(dir.Combine(WindowPlacement.FileName), "{ not json");

        WindowPlacement.Load(new UserProfile(dir.Path)).ShouldBeNull();
    }

    [Fact]
    public void Load_SizeBelowTheMinimum_IsEnlarged()
    {
        using var dir = TempDirectory.Create();
        var profile = new UserProfile(dir.Path);
        new WindowPlacement(0, 0, 300, 200, Maximized: false).Save(profile);

        WindowPlacement.Load(profile).ShouldBe(new WindowPlacement(0, 0, WindowPlacement.MinWidth, WindowPlacement.MinHeight, false));
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(1800, 100, true)]
    [InlineData(-8, -8, true)]
    [InlineData(2500, 100, false)]
    [InlineData(100, 1030, false)]
    [InlineData(-1250, 100, false)]
    public void IsVisibleOn_OneScreen_NeedsTheTitleBarOnIt(int left, int top, bool visible)
    {
        new WindowPlacement(left, top, 1280, 820, false).IsVisibleOn(OneScreen).ShouldBe(visible);
    }

    [Fact]
    public void IsVisibleOn_SecondScreen_IsVisible()
    {
        Rectangle[] screens = [.. OneScreen, new(1920, 0, 2560, 1400)];

        new WindowPlacement(2500, 100, 1280, 820, false).IsVisibleOn(screens).ShouldBeTrue();
    }
}
