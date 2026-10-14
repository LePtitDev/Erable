using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Erable.Server.Hosting;

namespace Erable.Server.Projects;

/// <summary>
/// Chooses the <see cref="UserProfile"/> of a request. One profile per backend until accounts exist (M4), except in
/// test mode: the <c>erable-test-slot</c> cookie set by <c>POST /_test/reset</c> gives each e2e test file its own
/// profile (recent projects, preferences, drafts), so that test files run in parallel against one backend.
/// </summary>
public sealed partial class UserProfiles
{
    public const string TestSlotCookie = "erable-test-slot";

    private readonly ConcurrentDictionary<string, UserProfile> _testProfiles = new(StringComparer.Ordinal);
    private readonly string _testRoot;
    private readonly bool _testMode;

    public UserProfiles(ErableHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Default = new UserProfile(ErableHost.ProfileDirectory(options));
        _testRoot = Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "test-profiles");
        _testMode = ErableHost.TestEndpointsCompiled && options.TestMode;
    }

    public UserProfile Default { get; }

    public UserProfile For(HttpContext? context)
    {
        if (_testMode && context?.Request.Cookies[TestSlotCookie] is { } slot && IsValidSlot(slot))
        {
            return ForTestSlot(slot);
        }

        return Default;
    }

    /// <summary>Profile of an e2e test slot (test mode only).</summary>
    public UserProfile ForTestSlot(string slot)
    {
        if (!_testMode || !IsValidSlot(slot))
        {
            throw new InvalidOperationException("Test profiles exist only in test mode.");
        }

        return _testProfiles.GetOrAdd(slot, s => new UserProfile(Path.Combine(_testRoot, s)));
    }

    public static bool IsValidSlot(string slot) => SlotPattern().IsMatch(slot);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SlotPattern();
}
