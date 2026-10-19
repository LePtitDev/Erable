using Android.Content;
using Android.Text;
using Android.Util;
using Android.Views;
using Android.Widget;
using Erable.Core;
using Erable.Mobile.Shell;

namespace Erable.Mobile;

/// <summary>
/// The first screen (F19.R1, R3, R6): the servers added on this phone. A tap opens one, a long press forgets it, the
/// button adds one after checking it. Drawn as the home screen of the SPA on a phone (Resources/layout/servers.xml).
/// </summary>
[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true, Theme = "@style/ErableTheme.Web",
    LaunchMode = Android.Content.PM.LaunchMode.SingleTop)]
public sealed class ServersActivity : Activity
{
    private ServerStore _store = null!;
    private LinearLayout _list = null!;
    private View _empty = null!;
    private View _hint = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _store = new ServerStore(this);

        SetContentView(Resource.Layout.servers);
        _list = FindViewById<LinearLayout>(Resource.Id.list)!;
        _empty = FindViewById(Resource.Id.empty)!;
        _hint = FindViewById(Resource.Id.hint)!;
        FindViewById<TextView>(Resource.Id.version)!.Text = GetString(Resource.String.app_version, new Java.Lang.String(AppVersion()));
        FindViewById(Resource.Id.add)!.Click += (_, _) => AskForAddress();

        SystemBars.Pad(Window!, FindViewById(Resource.Id.root)!, FindViewById(Resource.Id.header));
    }

    protected override void OnResume()
    {
        base.OnResume();
        Show(_store.Load().Servers);
    }

    /// <summary>One row per server: its host, then its version (and « Non chiffré » for plain HTTP, F19.R3).</summary>
    private void Show(IReadOnlyList<SavedServer> servers)
    {
        _list.RemoveAllViews();
        foreach (var server in servers)
        {
            var row = LayoutInflater.Inflate(Resource.Layout.server_row, _list, false)!;
            var details = GetString(Resource.String.server_version, new Java.Lang.String(server.AppVersion));
            if (server.IsCleartext)
            {
                details += " · " + GetString(Resource.String.cleartext);
            }

            row.FindViewById<TextView>(Resource.Id.host)!.Text = server.Host;
            row.FindViewById<TextView>(Resource.Id.details)!.Text = details;
            row.Click += (_, _) => Open(server);
            row.LongClick += (_, _) => ConfirmForget(server);
            _list.AddView(row);
        }

        var none = servers.Count == 0;
        _empty.Visibility = none ? ViewStates.Visible : ViewStates.Gone;
        _list.Visibility = none ? ViewStates.Gone : ViewStates.Visible;
        _hint.Visibility = none ? ViewStates.Gone : ViewStates.Visible;
    }

    private string AppVersion()
    {
        var info = OperatingSystem.IsAndroidVersionAtLeast(33)
            ? PackageManager!.GetPackageInfo(PackageName!, Android.Content.PM.PackageManager.PackageInfoFlags.Of(0L))
#pragma warning disable CA1422 // Before Android 13 only.
            : PackageManager!.GetPackageInfo(PackageName!, 0);
#pragma warning restore CA1422
        return info?.VersionName ?? string.Empty;
    }

    private int Dp(int value) => (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, value, Resources!.DisplayMetrics);

    private void Open(SavedServer server)
    {
        var intent = new Intent(this, typeof(WebActivity));
        intent.PutExtra(WebActivity.OriginExtra, server.Origin);
        StartActivity(intent);
    }

    private void AskForAddress()
    {
        var field = new EditText(this)
        {
            Hint = GetString(Resource.String.add_hint),
            InputType = InputTypes.ClassText | InputTypes.TextVariationUri,
        };
        field.SetSingleLine(true);
        var help = new TextView(this) { Text = GetString(Resource.String.add_help) };
        var error = new TextView(this) { Visibility = ViewStates.Gone };
        error.SetTextColor(new Android.Graphics.Color(GetColor(Resource.Color.danger)));
        var body = new LinearLayout(this) { Orientation = Orientation.Vertical };
        body.SetPadding(Dp(24), Dp(8), Dp(24), 0);
        body.AddView(field);
        body.AddView(help);
        body.AddView(error);

        var dialog = new AlertDialog.Builder(this)
            .SetTitle(Resource.String.add_title)!
            .SetView(body)!
            .SetPositiveButton(Resource.String.add_action, (IDialogInterfaceOnClickListener?)null)!
            .SetNegativeButton(Resource.String.cancel, (IDialogInterfaceOnClickListener?)null)!
            .Create()!;
        dialog.Show();
        field.RequestFocus();

        // Set after Show: the dialog stays open while the server is checked, and on a refusal.
        var confirm = dialog.GetButton((int)DialogButtonType.Positive)!;
        confirm.Click += async (_, _) =>
        {
            confirm.Enabled = false;
            confirm.Text = GetString(Resource.String.add_checking);
            var added = await _store.AddAsync(field.Text ?? string.Empty, CancellationToken.None);
            confirm.Enabled = true;
            confirm.Text = GetString(Resource.String.add_action);
            if (added.IsSuccess)
            {
                dialog.Dismiss();
                Show(_store.Load().Servers);
                Open(added.Value);
            }
            else
            {
                error.Text = MessageOf(added.Error);
                error.Visibility = ViewStates.Visible;
            }
        };
    }

    private void ConfirmForget(SavedServer server)
    {
        new AlertDialog.Builder(this)
            .SetTitle(GetString(Resource.String.forget_title, new Java.Lang.String(server.Host)))!
            .SetMessage(Resource.String.forget_message)!
            .SetPositiveButton(Resource.String.forget_action, (_, _) =>
            {
                _store.Forget(server);
                Show(_store.Load().Servers);
            })!
            .SetNegativeButton(Resource.String.cancel, (IDialogInterfaceOnClickListener?)null)!
            .Show();
    }

    private string MessageOf(DomainError error) => GetString(error.Code switch
    {
        ServerErrors.InvalidUrl => Resource.String.error_invalid,
        ServerErrors.NotErable => Resource.String.error_not_erable,
        ServerErrors.NotServerMode => Resource.String.error_not_server,
        _ => Resource.String.error_unreachable,
    });
}
