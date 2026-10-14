import { useTranslation } from "react-i18next";
import type { ElementKind } from "../api/tree";
import styles from "./KindBadge.module.css";

/** Letter badge of an element kind (P, C, Pr, L, F…), as in the mockup; its name is announced, not the letter. */
export function KindBadge({
  kind,
  size = "md",
}: {
  readonly kind: ElementKind;
  readonly size?: "sm" | "md";
}) {
  const { t } = useTranslation();
  return (
    <span
      className={`${styles["badge"] ?? ""} ${styles[kind] ?? ""} ${styles[size] ?? ""}`}
      title={t(`kinds.${kind}`)}
      aria-hidden
    >
      {t(`kinds.short.${kind}`)}
    </span>
  );
}
