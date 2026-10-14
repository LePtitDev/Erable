namespace Erable.Server.Spa;

/// <summary>Interface theme chosen by the user (F15.R2).</summary>
public enum ThemePreference
{
    Light,
    Dark,
    System,
}

/// <summary>
/// Source of the theme written into <c>index.html</c> before the first paint (F15.R3, decision P-03).
/// The default returns <see cref="ThemePreference.System"/>; the preferences API (M1-15) replaces it.
/// </summary>
public interface IThemePreferenceSource
{
    ThemePreference GetTheme(HttpContext context);
}

internal sealed class SystemThemePreferenceSource : IThemePreferenceSource
{
    public ThemePreference GetTheme(HttpContext context) => ThemePreference.System;
}
