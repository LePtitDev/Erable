import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { apiInfoQuery } from "../../shared/api/queries";
import { useFormatters } from "../../shared/i18n/format";
import { LanguageSwitcher } from "../../shared/i18n/LanguageSwitcher";
import { ThemeSwitcher } from "../../shared/theme/ThemeSwitcher";
import styles from "./HelloPage.module.css";

/** Temporary home page of the M0 skeleton: proves API, i18n and theming end to end. */
export function HelloPage() {
  const { t } = useTranslation();
  const format = useFormatters();
  const info = useQuery(apiInfoQuery);
  // Sample values that exercise plurals and Intl formatting until real data exists.
  const sampleDate = new Date(Date.UTC(2026, 2, 12, 9, 0));

  return (
    <main className={styles["page"]}>
      <header className={styles["header"]}>
        <h1 className={styles["title"]}>{t("app.name")}</h1>
        <div className={styles["controls"]}>
          <LanguageSwitcher />
          <ThemeSwitcher />
        </div>
      </header>
      <p className={styles["tagline"]}>{t("hello.tagline")}</p>
      <ul className={styles["facts"]}>
        <li>{t("hello.pages", { count: 1280 })}</li>
        <li>{t("hello.lastOpened", { when: format.date(sampleDate) })}</li>
      </ul>
      {info.isPending && <p className={styles["meta"]}>{t("common.loading")}</p>}
      {info.isSuccess && (
        <p className={styles["meta"]}>
          {t("app.version", { version: info.data.appVersion, apiVersion: info.data.apiVersion })} ·{" "}
          {t(`app.mode.${info.data.mode}`)}
        </p>
      )}
      {info.isError && (
        <p role="alert" className={styles["error"]}>
          {t("common.error", { message: info.error.message })}
        </p>
      )}
    </main>
  );
}
