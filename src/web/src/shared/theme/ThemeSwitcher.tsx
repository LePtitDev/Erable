import { themePreferences, useThemeStore, type ThemePreference } from "./theme";
import styles from "./ThemeSwitcher.module.css";

const labels: Record<ThemePreference, string> = {
  light: "Clair",
  dark: "Sombre",
  system: "Système",
};

export function ThemeSwitcher() {
  const preference = useThemeStore((s) => s.preference);
  const setPreference = useThemeStore((s) => s.setPreference);
  return (
    <fieldset className={styles["switcher"]}>
      <legend className={styles["legend"]}>Thème</legend>
      {themePreferences.map((value) => (
        <label key={value} className={styles["option"]}>
          <input
            type="radio"
            name="theme"
            value={value}
            checked={preference === value}
            onChange={() => {
              setPreference(value);
            }}
          />
          {labels[value]}
        </label>
      ))}
    </fieldset>
  );
}
