import { useId } from "react";
import { useTranslation } from "react-i18next";
import { themePreferences, useThemeStore } from "./theme";
import { useSetTheme } from "./useSetTheme";
import styles from "./ThemeSwitcher.module.css";

export function ThemeSwitcher() {
  const { t } = useTranslation();
  const preference = useThemeStore((s) => s.preference);
  const setPreference = useSetTheme();
  // Unique per instance: the home screen shows the switcher both in its sidebar and in the preferences dialog.
  const name = useId();
  return (
    <fieldset className={styles["switcher"]}>
      <legend className={styles["legend"]}>{t("preferences.theme.label")}</legend>
      {themePreferences.map((value) => (
        <label key={value} className={styles["option"]}>
          <input
            type="radio"
            name={name}
            value={value}
            checked={preference === value}
            onChange={() => {
              setPreference(value);
            }}
          />
          {t(`preferences.theme.${value}`)}
        </label>
      ))}
    </fieldset>
  );
}
