import { Check, Copy } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatDisplay, type ElementId } from "../ids/elementId";
import styles from "./IdChip.module.css";

/** The ID shown `3F2A 9C14 E07B 5D21`, copied in canonical form in one click with a brief confirmation (F03.R1–R2). */
export function IdChip({ id }: { readonly id: ElementId }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const timer = setTimeout(() => {
      setCopied(false);
    }, 2000);
    return () => {
      clearTimeout(timer);
    };
  }, [copied]);

  return (
    <span className={styles["wrapper"]}>
      <button
        type="button"
        className={styles["chip"]}
        aria-label={t("element.copyId", { id })}
        onClick={() => {
          void navigator.clipboard.writeText(id).then(() => {
            setCopied(true);
          });
        }}
      >
        <span className={styles["label"]}>{t("element.id")}</span>
        {formatDisplay(id)}
        {copied ? <Check aria-hidden size={14} /> : <Copy aria-hidden size={14} />}
      </button>
      <span role="status" className={styles["status"]}>
        {copied ? t("element.idCopied") : ""}
      </span>
    </span>
  );
}
