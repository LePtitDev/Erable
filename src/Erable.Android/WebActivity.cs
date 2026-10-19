using Android.Content;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Android.Window;
using Erable.Mobile.Shell;
using Uri = Android.Net.Uri;

namespace Erable.Mobile;

/// <summary>
/// The SPA of one server in a WebView (F19.R2–R5). The WebView stays on the origin of the server, the session is the
/// cookie of the server (kept across launches by the WebView, F19.R6), files go through the Android pickers and the
/// download manager. Configuration changes are handled here: a rotation does not reload the page being edited.
/// </summary>
[Activity(Theme = "@style/ErableTheme.Web", Exported = false,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.UiMode)]
public sealed class WebActivity : Activity
{
    public const string OriginExtra = "origin";
    private const int PickFilesRequest = 1;

    private WebView _web = null!;
    private string _origin = string.Empty;
    private IValueCallback? _pickedFiles;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _origin = Intent?.GetStringExtra(OriginExtra) ?? string.Empty;
        if (_origin.Length == 0)
        {
            Finish();
            return;
        }

#if DEBUG
        WebView.SetWebContentsDebuggingEnabled(true);
#endif
        _web = new WebView(this);
        var settings = _web.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        // Nothing of the phone is reachable from the page (F19.R2).
        settings.AllowFileAccess = false;
        settings.AllowContentAccess = false;
        settings.MixedContentMode = MixedContentHandling.NeverAllow;
        settings.SetSupportMultipleWindows(false);

        var cookies = CookieManager.Instance!;
        cookies.SetAcceptCookie(true);
        cookies.SetAcceptThirdPartyCookies(_web, false);

        _web.SetWebViewClient(new Client(this));
        _web.SetWebChromeClient(new Chrome(this));
        _web.SetDownloadListener(new Downloads(this));
        SetContentView(_web);
        SystemBars.Pad(Window!, _web);

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            OnBackInvokedDispatcher.RegisterOnBackInvokedCallback(0, new Back(this));
        }

        if (savedInstanceState is null || _web.RestoreState(savedInstanceState) is null)
        {
            _web.LoadUrl(_origin + "/");
        }
    }

    protected override void OnSaveInstanceState(Bundle outState)
    {
        base.OnSaveInstanceState(outState);
        _web.SaveState(outState);
    }

    protected override void OnPause()
    {
        base.OnPause();
        // The session survives a kill of the application (F19.R6).
        CookieManager.Instance?.Flush();
    }

    protected override void OnDestroy()
    {
        _web?.Destroy();
        base.OnDestroy();
    }

    /// <summary>F19.R4: the history of the WebView first, then back to the list of servers.</summary>
    private void GoBack()
    {
        if (_web.CanGoBack())
        {
            _web.GoBack();
        }
        else
        {
            Finish();
        }
    }

#pragma warning disable CS0618, CA1422 // Before Android 13 only: OnBackInvokedDispatcher takes over from there.
    public override void OnBackPressed() => GoBack();
#pragma warning restore CS0618, CA1422

    protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != PickFilesRequest || _pickedFiles is null)
        {
            return;
        }

        var files = WebChromeClient.FileChooserParams.ParseResult((int)resultCode, data);
        _pickedFiles.OnReceiveValue(files is null ? null : Java.Lang.Object.FromArray(files));
        _pickedFiles = null;
    }

    private void OpenOutside(Uri uri)
    {
        try
        {
            StartActivity(new Intent(Intent.ActionView, uri));
        }
        catch (ActivityNotFoundException)
        {
            Toast.MakeText(this, Resource.String.open_external_failed, ToastLength.Short)?.Show();
        }
    }

    private sealed class Client(WebActivity activity) : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request)
        {
            if (request?.Url is not { } url)
            {
                return true;
            }

            switch (NavigationPolicy.Decide(activity._origin, url.ToString() ?? string.Empty))
            {
                case NavigationDecision.Stay:
                    return false;
                case NavigationDecision.External:
                    activity.OpenOutside(url);
                    return true;
                case NavigationDecision.Block:
                    return true;
                default:
                    throw new InvalidOperationException("Unknown navigation decision.");
            }
        }

        public override void OnReceivedError(WebView? view, IWebResourceRequest? request, WebResourceError? error)
        {
            base.OnReceivedError(view, request, error);
            if (request?.IsForMainFrame == true)
            {
                var message = activity.GetString(Resource.String.web_error, new Java.Lang.String(error?.Description ?? string.Empty));
                Toast.MakeText(activity, message, ToastLength.Long)?.Show();
            }
        }
    }

    /// <summary>The file picker of an <c>&lt;input type="file"&gt;</c> (attachments, F19.R5).</summary>
    private sealed class Chrome(WebActivity activity) : WebChromeClient
    {
        public override bool OnShowFileChooser(WebView? webView, IValueCallback? filePathCallback, FileChooserParams? fileChooserParams)
        {
            activity._pickedFiles?.OnReceiveValue(null);
            activity._pickedFiles = filePathCallback;
            try
            {
                var intent = fileChooserParams?.CreateIntent() ?? new Intent(Intent.ActionGetContent).SetType("*/*");
                activity.StartActivityForResult(intent, PickFilesRequest);
                return true;
            }
            catch (ActivityNotFoundException)
            {
                activity._pickedFiles = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Attachments to download go to the download manager, with the cookie of the session (F19.R5). Public Downloads
    /// from Android 10 (no permission needed), the folder of the application before.
    /// </summary>
    private sealed class Downloads(WebActivity activity) : Java.Lang.Object, IDownloadListener
    {
        public void OnDownloadStart(string? url, string? userAgent, string? contentDisposition, string? mimetype, long contentLength)
        {
            if (url is null || NavigationPolicy.Decide(activity._origin, url) != NavigationDecision.Stay)
            {
                return;
            }

            var name = URLUtil.GuessFileName(url, contentDisposition, mimetype) ?? "erable";
            var request = new DownloadManager.Request(Uri.Parse(url));
            if (CookieManager.Instance?.GetCookie(url) is { } cookie)
            {
                request.AddRequestHeader("Cookie", cookie);
            }

            if (userAgent is not null)
            {
                request.AddRequestHeader("User-Agent", userAgent);
            }

            request.SetMimeType(mimetype);
            request.SetTitle(name);
            request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);
            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                request.SetDestinationInExternalPublicDir(Android.OS.Environment.DirectoryDownloads, name);
            }
            else
            {
                request.SetDestinationInExternalFilesDir(activity, Android.OS.Environment.DirectoryDownloads, name);
            }

            if (activity.GetSystemService(DownloadService) is DownloadManager manager)
            {
                manager.Enqueue(request);
                Toast.MakeText(activity, activity.GetString(Resource.String.download_started, new Java.Lang.String(name)), ToastLength.Short)?.Show();
            }
        }
    }

    private sealed class Back(WebActivity activity) : Java.Lang.Object, IOnBackInvokedCallback
    {
        public void OnBackInvoked() => activity.GoBack();
    }
}
