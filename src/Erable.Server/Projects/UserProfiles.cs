using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Erable.Server.Hosting;
using Erable.Server.Security;

namespace Erable.Server.Projects;

/// <summary>
/// Chooses the <see cref="UserProfile"/> of a request. One profile per backend in desktop mode, one per account in server mode (<c>data/profiles</c>), and in
/// test mode: the <c>erable-test-slot</c> cookie set by <c>POST /_test/reset</c> gives each e2e test file its own
/// profile (recent projects, preferences, drafts), so that test files run in parallel against one backend.
/// </summary>
public sealed partial class UserProfiles
{
    public const string TestSlotCookie = "erable-test-slot";

    private readonly ConcurrentDictionary<string, UserProfile> _testProfiles = new(StringComparer.Ordinal);
    private readonly string _testRoot;
    private readonly bool _testMode;
    private readonly bool _accounts;
    private readonly string _profilesRoot;
    private readonly ConcurrentDictionary<string, UserProfile> _users = new(StringComparer.Ordinal);

    public UserProfiles(ErableHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Default = new UserProfile(ErableHost.ProfileDirectory(options));
        _testRoot = Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "test-profiles");
        _testMode = ErableHost.TestEndpointsCompiled && options.TestMode;
        _accounts = options.Mode == HostMode.Server;
        _profilesRoot = Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "profiles");
    }

    public UserProfile Default { get; }

    public UserProfile For(HttpContext? context)
    {
        // Accounts exist only in server mode; the desktop user has the default profile.
        var user = _accounts ? context?.User.AccountId() : null;
        if (TestSlotOf(context) is { } slot)
        {
            return ForTestSlot(slot, user);
        }

        return user is null ? Default : _users.GetOrAdd(user, id => new UserProfile(Path.Combine(_profilesRoot, "u-" + id)));
    }

    /// <summary>The e2e test slot of a request (test mode only), or null.</summary>
    public string? TestSlotOf(HttpContext? context) =>
        _testMode && context?.Request.Cookies[TestSlotCookie] is { } slot && IsValidSlot(slot) ? slot : null;

    /// <summary>Profile of an e2e test slot (test mode only).</summary>
    public UserProfile ForTestSlot(string slot, string? user = null)
    {
        if (!_testMode || !IsValidSlot(slot))
        {
            throw new InvalidOperationException("Test profiles exist only in test mode.");
        }

        var key = user is null ? slot : slot + "/" + user;
        return _testProfiles.GetOrAdd(key, _ => new UserProfile(user is null ? Path.Combine(_testRoot, slot) : Path.Combine(_testRoot, slot, "u-" + user)));
    }

    public static bool IsValidSlot(string slot) => SlotPattern().IsMatch(slot);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SlotPattern();
}
