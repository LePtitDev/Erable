import { useTranslation } from "react-i18next";
import { themePreferences, useThemeStore } from "./theme";
import styles from "./ThemeSwitcher.module.css";

export function ThemeSwitcher() {
  const { t } = useTranslation();
  const preference = useThemeStore((s) => s.preference);
  const setPreference = useThemeStore((s) => s.setPreference);
  return (
    <fieldset className={styles["switcher"]}>
      <legend className={styles["legend"]}>{t("preferences.theme.label")}</legend>
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
          {t(`preferences.theme.${value}`)}
        </label>
      ))}
    </fieldset>
  );
}
