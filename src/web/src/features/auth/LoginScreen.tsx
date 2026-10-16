import { useTranslation } from "react-i18next";
import { LanguageSwitcher } from "../../shared/i18n/LanguageSwitcher";
import { ThemeSwitcher } from "../../shared/theme/ThemeSwitcher";
import { Logo } from "../../shared/ui/Logo";
import { LoginForm } from "./LoginForm";
import styles from "./LoginScreen.module.css";

/** Shown instead of the application when the server wants a signed-in user (E2E-14-01). */
export function LoginScreen() {
  const { t } = useTranslation();
  return (
    <main className={styles["screen"]}>
      <section className={styles["card"]} aria-labelledby="login-title">
        <div className={styles["brand"]}>
          <span className={styles["logo"]}>
            <Logo />
          </span>
          <span className={styles["name"]}>{t("app.name")}</span>
        </div>
        <h1 id="login-title" className={styles["title"]}>
          {t("auth.title")}
        </h1>
        <p className={styles["subtitle"]}>{t("auth.subtitle")}</p>
        {/* The gate refetches the user once signed in and shows the application where the URL points. */}
        <LoginForm onSignedIn={() => undefined} />
      </section>
      <div className={styles["settings"]}>
        <LanguageSwitcher />
        <ThemeSwitcher />
      </div>
    </main>
  );
}
