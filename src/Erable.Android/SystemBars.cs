using Android.Views;

namespace Erable.Mobile;

/// <summary>
/// Edge to edge (enforced from Android 15): the content pads itself with the system bars, the display cutout and the
/// keyboard, so that nothing hides under them and the SPA shrinks above the keyboard (F18.R6). Before Android 11 the
/// window fits the bars by itself and resizes for the keyboard (<c>adjustResize</c>).
/// </summary>
internal static class SystemBars
{
    public static void Pad(Window window, View content)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            return;
        }

        // Android 15 draws edge to edge whatever this says (and makes it obsolete).
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetDecorFitsSystemWindows(false);
        }

        content.SetOnApplyWindowInsetsListener(new Listener());
    }

    private sealed class Listener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout() | WindowInsets.Type.Ime());
                view.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
                return WindowInsets.Consumed!;
            }

            return insets;
        }
    }
}
