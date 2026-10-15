import { useCallback } from "react";
import { useSavePreferences } from "../api/preferences";
import { useThemeStore, type ThemePreference } from "./theme";

/** Applies a theme at once and remembers it for the next launch (written into index.html, F15.R3). */
export function useSetTheme() {
  const setPreference = useThemeStore((s) => s.setPreference);
  const save = useSavePreferences();
  return useCallback(
    (theme: ThemePreference) => {
      setPreference(theme);
      void save({ theme }).catch((error: unknown) => {
        console.error("The theme could not be saved", error);
      });
    },
    [setPreference, save],
  );
}
