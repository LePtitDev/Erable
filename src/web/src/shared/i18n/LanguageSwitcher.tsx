import { useId } from "react";
import { useTranslation } from "react-i18next";
import { useSavePreferences } from "../api/preferences";
import { availableLanguages, changeLanguage } from "./i18n";
import { isInterfaceLanguage, nativeNames } from "./languages";
import styles from "./LanguageSwitcher.module.css";

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  const id = useId();
  const save = useSavePreferences();
  return (
    <div className={styles["field"]}>
      <label htmlFor={id}>{t("preferences.language")}</label>
      <select
        id={id}
        className={styles["select"]}
        value={i18n.language}
        onChange={(event) => {
          const language = event.target.value;
          void changeLanguage(language, i18n);
          // The pseudo-locale (development only) is not an interface language the backend keeps.
          if (isInterfaceLanguage(language)) {
            void save({ language }).catch((error: unknown) => {
              console.error("The language could not be saved", error);
            });
          }
        }}
      >
        {availableLanguages().map((language) => (
          <option key={language} value={language}>
            {isInterfaceLanguage(language) ? nativeNames[language] : language}
          </option>
        ))}
      </select>
    </div>
  );
}
