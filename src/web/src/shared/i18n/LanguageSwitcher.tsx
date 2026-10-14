import { useId } from "react";
import { useTranslation } from "react-i18next";
import { availableLanguages, changeLanguage } from "./i18n";
import { isInterfaceLanguage, nativeNames } from "./languages";
import styles from "./LanguageSwitcher.module.css";

export function LanguageSwitcher() {
  const { t, i18n } = useTranslation();
  const id = useId();
  return (
    <div className={styles["field"]}>
      <label htmlFor={id}>{t("preferences.language")}</label>
      <select
        id={id}
        className={styles["select"]}
        value={i18n.language}
        onChange={(event) => {
          void changeLanguage(event.target.value, i18n);
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
