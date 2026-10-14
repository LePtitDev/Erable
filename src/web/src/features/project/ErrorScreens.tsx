import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";

/** « Élément introuvable » (E2E-03-03): no technical detail, a way back. Completed with M1-09. */
export function NotFoundScreen() {
  const { t } = useTranslation();
  return (
    <main>
      <h1>{t("notFound.title")}</h1>
      <Link to="/">{t("notFound.home")}</Link>
    </main>
  );
}

export function ErrorScreen() {
  const { t } = useTranslation();
  return (
    <main role="alert">
      <h1>{t("errorScreen.title")}</h1>
      <Link to="/">{t("notFound.home")}</Link>
    </main>
  );
}
