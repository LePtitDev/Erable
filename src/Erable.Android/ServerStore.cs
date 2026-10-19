using Android.Content;
using Android.Webkit;
using Erable.Core;
using Erable.Core.Servers;
using Erable.Mobile.Shell;

namespace Erable.Mobile;

/// <summary>
/// The servers of the application (F19.R1, F19.R6): the list in the private preferences, the check before adding one,
/// and forgetting one with its cookies. The session itself is the cookie of the server, kept by the WebView.
/// </summary>
internal sealed class ServerStore(Context context)
{
    private const string PreferencesName = "erable";
    private const string ServersKey = "servers";

    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    private ISharedPreferences Preferences =>
        context.GetSharedPreferences(PreferencesName, FileCreationMode.Private)
        ?? throw new InvalidOperationException("The preferences of the application are not available.");

    public ServerList Load() => ServerList.Read(Preferences.GetString(ServersKey, null));

    private void Save(ServerList list)
    {
        using var editor = Preferences.Edit() ?? throw new InvalidOperationException("The preferences cannot be written.");
        editor.PutString(ServersKey, list.Write());
        editor.Apply();
    }

    /// <summary><c>GET /api/info</c>, then the server joins the list (F19.R1). Nothing is saved for a refused address.</summary>
    public async Task<Result<SavedServer>> AddAsync(string typed, CancellationToken cancellationToken)
    {
        var origin = ServerAddress.Normalize(typed);
        if (!origin.IsSuccess)
        {
            return origin.Error;
        }

        int status;
        string body;
        try
        {
            using var response = await Http.GetAsync(new Uri(origin.Value + "/api/info"), cancellationToken).ConfigureAwait(false);
            status = (int)response.StatusCode;
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or Java.IO.IOException)
        {
            return new DomainError(ServerErrors.Unreachable, $"{origin.Value} cannot be reached: {ex.Message}");
        }

        var handshake = ServerErrors.Check(origin.Value, status, body);
        if (!handshake.IsSuccess)
        {
            return handshake.Error;
        }

        var server = new SavedServer(origin.Value, handshake.Value.AppVersion, DateTimeOffset.UtcNow);
        Save(Load().Add(server));
        return server;
    }

    /// <summary>« Oublier ce serveur » (F19.R6): out of the list, and its cookies expired, so its session ends here.</summary>
    public void Forget(SavedServer server)
    {
        Save(Load().Remove(server.Origin));
        var cookies = CookieManager.Instance;
        if (cookies?.GetCookie(server.Origin) is { } header)
        {
            foreach (var pair in header.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var name = pair.Split('=', 2)[0];
                cookies.SetCookie(server.Origin, $"{name}=; Max-Age=0; Path=/");
            }

            cookies.Flush();
        }
    }
}
