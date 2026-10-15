import { create } from "zustand";

export const themePreferences = ["light", "dark", "system"] as const;
export type ThemePreference = (typeof themePreferences)[number];

function isThemePreference(value: unknown): value is ThemePreference {
  return themePreferences.includes(value as ThemePreference);
}

/**
 * The backend writes the user's preference into `<html data-theme>` before serving index.html,
 * so the first paint already has the right theme (F15.R3, decision P-03).
 */
export function readInitialTheme(root: HTMLElement = document.documentElement): ThemePreference {
  const value = root.dataset["theme"];
  return isThemePreference(value) ? value : "system";
}

export function applyTheme(
  preference: ThemePreference,
  root: HTMLElement = document.documentElement,
) {
  root.dataset["theme"] = preference;
}

interface ThemeState {
  readonly preference: ThemePreference;
  setPreference: (preference: ThemePreference) => void;
}

/** UI-side theme state; the switcher also saves it through the preferences API (F15.R3). */
export const useThemeStore = create<ThemeState>()((set) => ({
  preference: readInitialTheme(),
  setPreference: (preference) => {
    applyTheme(preference);
    set({ preference });
  },
}));
