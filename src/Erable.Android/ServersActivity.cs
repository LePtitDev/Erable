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
/// button adds one after checking it. Built in code: a list and a button do not need a layout file.
/// </summary>
[Activity(Label = "@string/app_name", MainLauncher = true, Exported = true, Theme = "@style/ErableTheme.Web",
    LaunchMode = Android.Content.PM.LaunchMode.SingleTop)]
public sealed class ServersActivity : Activity
{
    private ServerStore _store = null!;
    private ServerAdapter _adapter = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _store = new ServerStore(this);
        _adapter = new ServerAdapter(this);

        var padding = Dp(16);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(padding, padding, padding, padding);

        var title = new TextView(this) { Text = GetString(Resource.String.servers_title) };
        title.SetTextSize(ComplexUnitType.Sp, 26);
        title.SetPadding(0, Dp(8), 0, Dp(16));
        root.AddView(title);

        var empty = new TextView(this) { Text = GetString(Resource.String.servers_empty) };
        empty.SetTextSize(ComplexUnitType.Sp, 16);
        var list = new ListView(this) { Adapter = _adapter, EmptyView = empty };
        list.ItemClick += (_, e) => Open(_adapter[e.Position]);
        list.ItemLongClick += (_, e) => ConfirmForget(_adapter[e.Position]);
        root.AddView(empty, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        root.AddView(list, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

        var add = new Button(this) { Text = GetString(Resource.String.servers_add) };
        add.SetMinimumHeight(Dp(48));
        add.Click += (_, _) => AskForAddress();
        root.AddView(add, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        SetContentView(root);
        SystemBars.Pad(Window!, root);
    }

    protected override void OnResume()
    {
        base.OnResume();
        _adapter.Show(_store.Load().Servers);
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
        error.SetTextColor(Android.Graphics.Color.ParseColor("#b3261e"));
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
                _adapter.Show(_store.Load().Servers);
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
                _adapter.Show(_store.Load().Servers);
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

    /// <summary>Two lines per server: its host, then its version (and « Non chiffré » for plain HTTP, F19.R3).</summary>
    private sealed class ServerAdapter(Activity activity) : BaseAdapter<SavedServer>
    {
        private IReadOnlyList<SavedServer> _servers = [];

        public void Show(IReadOnlyList<SavedServer> servers)
        {
            _servers = servers;
            NotifyDataSetChanged();
        }

        public override SavedServer this[int position] => _servers[position];

        public override int Count => _servers.Count;

        public override long GetItemId(int position) => position;

        public override View GetView(int position, View? convertView, ViewGroup? parent)
        {
            var view = convertView
                ?? activity.LayoutInflater.Inflate(global::Android.Resource.Layout.SimpleListItem2, parent, false)!;
            var server = _servers[position];
            var details = activity.GetString(Resource.String.server_version, new Java.Lang.String(server.AppVersion));
            if (server.IsCleartext)
            {
                details += " · " + activity.GetString(Resource.String.cleartext);
            }

            view.FindViewById<TextView>(global::Android.Resource.Id.Text1)!.Text = server.Host;
            view.FindViewById<TextView>(global::Android.Resource.Id.Text2)!.Text = details;
            view.SetMinimumHeight((int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 56, activity.Resources!.DisplayMetrics));
            return view;
        }
    }
}
