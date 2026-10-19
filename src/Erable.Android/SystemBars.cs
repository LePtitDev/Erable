using Android.Views;

namespace Erable.Mobile;

/// <summary>
/// Edge to edge (enforced from Android 15): the content pads itself with the system bars, the display cutout and the
/// keyboard, so that nothing hides under them and the SPA shrinks above the keyboard (F18.R6). Before Android 11 the
/// window fits the bars by itself and resizes for the keyboard (<c>adjustResize</c>). The padded view must be a
/// container: a <c>WebView</c> ignores its own padding and would still draw under the bars.
/// </summary>
internal static class SystemBars
{
    /// <summary>
    /// Adds the insets to the padding of <paramref name="content"/>. With a <paramref name="header"/>, the top inset
    /// goes to the header instead, whose background then fills the space under the status bar.
    /// </summary>
    public static void Pad(Window window, View content, View? header = null)
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

        content.SetOnApplyWindowInsetsListener(new Listener(content, header));
    }

    private sealed class Listener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        private readonly View? _header;
        private readonly int _contentTop;
        private readonly int _headerTop;

        public Listener(View content, View? header)
        {
            _header = header;
            _contentTop = content.PaddingTop;
            _headerTop = header?.PaddingTop ?? 0;
        }

        public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout() | WindowInsets.Type.Ime());
                if (_header is null)
                {
                    view.SetPadding(bars.Left, _contentTop + bars.Top, bars.Right, bars.Bottom);
                }
                else
                {
                    view.SetPadding(bars.Left, _contentTop, bars.Right, bars.Bottom);
                    _header.SetPadding(_header.PaddingLeft, _headerTop + bars.Top, _header.PaddingRight, _header.PaddingBottom);
                }

                return WindowInsets.Consumed!;
            }

            return insets;
        }
    }
}
