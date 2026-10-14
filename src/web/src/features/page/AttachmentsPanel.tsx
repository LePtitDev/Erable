import { useTranslation } from "react-i18next";
import { attachmentUrl, type AttachmentItem } from "../../shared/api/pages";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import { extensionOf } from "../../shared/ui/fileNames";
import styles from "./PageView.module.css";

/** « Pièces jointes » of the side panel (F04.R4): extension, name, size. Adding files arrives with M1-14. */
export function AttachmentsPanel({
  projectId,
  attachments,
}: {
  readonly projectId: ElementId;
  readonly pageId: ElementId;
  readonly attachments: readonly AttachmentItem[];
}) {
  const { t } = useTranslation();
  const format = useFormatters();
  return (
    <section aria-labelledby="attachments-title" className={styles["panelSection"]}>
      <div className={styles["panelHeader"]}>
        <h2 id="attachments-title" className={styles["panelTitle"]}>
          {t("attachments.title")}
        </h2>
      </div>
      {attachments.length === 0 ? (
        <p className={styles["panelEmpty"]}>{t("attachments.empty")}</p>
      ) : (
        <ul className={styles["attachments"]}>
          {attachments.map((a) => (
            <li key={a.id} className={styles["attachment"]}>
              <span className={styles["ext"]} aria-hidden>
                {extensionOf(a.fileName)}
              </span>
              <span className={styles["attachmentText"]}>
                <a
                  href={attachmentUrl(projectId, a.id)}
                  className={styles["attachmentName"]}
                  download={a.display === "file" ? a.fileName : undefined}
                  target={a.display === "file" ? undefined : "_blank"}
                  rel="noreferrer"
                >
                  {a.fileName}
                </a>
                <span className={styles["attachmentSize"]}>{format.size(a.size)}</span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
