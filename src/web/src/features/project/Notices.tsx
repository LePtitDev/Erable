import { X } from "lucide-react";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useNoticeStore, type Notice } from "../../shared/ui/notices";
import styles from "./Notices.module.css";

const lifetimeMs = 10_000;

function NoticeItem({ notice }: { readonly notice: Notice }) {
  const { t } = useTranslation();
  const dismiss = useNoticeStore((s) => s.dismiss);
  useEffect(() => {
    const timer = setTimeout(() => {
      dismiss(notice.id);
    }, lifetimeMs);
    return () => {
      clearTimeout(timer);
    };
  }, [dismiss, notice.id]);
  return (
    <li role="status" className={styles["notice"]}>
      <span>{notice.text}</span>
      <button
        type="button"
        className={styles["close"]}
        aria-label={t("common.close")}
        onClick={() => {
          dismiss(notice.id);
        }}
      >
        <X aria-hidden size={14} />
      </button>
    </li>
  );
}

/** The messages of {@link useNoticeStore}, bottom right of the project. */
export function Notices() {
  const notices = useNoticeStore((s) => s.notices);
  if (notices.length === 0) return null;
  return (
    <ul className={styles["list"]}>
      {notices.map((notice) => (
        <NoticeItem key={notice.id} notice={notice} />
      ))}
    </ul>
  );
}
