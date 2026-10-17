namespace Erable.Server.Projects;

/// <summary>Interface theme chosen by the user (F15.R2).</summary>
public enum ThemePreference
{
    Light,
    Dark,
    System,
}

/// <summary>
/// Interface preferences of a user (F15): stored in the application profile in local mode (decision Q7), with the
/// account in server mode (M4). Written into <c>index.html</c> before the first paint (F15.R3).
/// </summary>
/// <param name="Language">Interface language (<c>fr</c>, <c>en</c>, <c>de</c>, <c>es</c>); null follows the browser.</param>
/// <param name="Theme">Light, dark, or following the OS.</param>
/// <param name="Shortcuts">Command → key combination overrides; an empty value means no shortcut (F15.R4, M5-01).</param>
public sealed record UserPreferences(string? Language, ThemePreference Theme, IReadOnlyDictionary<string, string>? Shortcuts)
{
    public const int MaxShortcuts = 200;
    public const int MaxShortcutLength = 64;

    /// <summary>Interface languages of the SPA (decision Q1); content languages are a separate list.</summary>
    public static IReadOnlyList<string> InterfaceLanguages { get; } = ["fr", "en", "de", "es"];

    public static UserPreferences Default { get; } = new(null, ThemePreference.System, new Dictionary<string, string>());

    /// <summary>Null when valid, else the business code of the first problem.</summary>
    public string? Validate()
    {
        if (Language is not null && !InterfaceLanguages.Contains(Language, StringComparer.Ordinal))
        {
            return "preferences.languageUnsupported";
        }

        if (!Enum.IsDefined(Theme))
        {
            return "preferences.themeUnsupported";
        }

        return Shortcuts is { Count: > MaxShortcuts }
            || Shortcuts?.Any(s => s.Key.Length is 0 or > MaxShortcutLength || s.Value.Length > MaxShortcutLength) == true
            ? "preferences.shortcutsInvalid"
            : null;
    }

    /// <summary>What a stored document means: an invalid part falls back to its default (a damaged profile must not block the app).</summary>
    public UserPreferences Normalize() =>
        new(
            Language is not null && InterfaceLanguages.Contains(Language, StringComparer.Ordinal) ? Language : null,
            Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
            Shortcuts ?? new Dictionary<string, string>());
}
