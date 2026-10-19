using System.Drawing;
using Erable.Server.Projects;

namespace Erable.Desktop;

/// <summary>
/// Size, position and maximized state of the window, kept in the profile from one launch to the next. The bounds are
/// those of the window when it is neither maximized nor minimized, so that it restores to them.
/// </summary>
internal sealed record WindowPlacement(int Left, int Top, int Width, int Height, bool Maximized)
{
    public const string FileName = "window.json";

    public const int MinWidth = 720;

    public const int MinHeight = 480;

    /// <summary>Height of the strip at the top of the window that must stay on a screen to grab the window.</summary>
    private const int TitleBarHeight = 32;

    /// <summary>The saved placement, null when there is none or it is unreadable; a size below the minimum is enlarged.</summary>
    public static WindowPlacement? Load(UserProfile profile)
    {
        var saved = profile.Read<WindowPlacement>(FileName);
        return saved is null || saved.Width <= 0 || saved.Height <= 0
            ? null
            : saved with { Width = Math.Max(saved.Width, MinWidth), Height = Math.Max(saved.Height, MinHeight) };
    }

    public void Save(UserProfile profile) => profile.Write(FileName, this);

    /// <summary>
    /// Whether the top of the window lies on one of the work areas. It does not when the screen it was on is gone (a
    /// monitor unplugged, a lower resolution): the window is then centered rather than out of reach.
    /// </summary>
    public bool IsVisibleOn(IEnumerable<Rectangle> workAreas)
    {
        var titleBar = new Rectangle(Left, Top, Width, TitleBarHeight);
        return workAreas.Any(area =>
        {
            var visible = Rectangle.Intersect(area, titleBar);
            return visible.Width >= Math.Min(100, Width) && visible.Height >= TitleBarHeight / 2;
        });
    }
}
