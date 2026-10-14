import { Download } from "lucide-react";
import { useTranslation } from "react-i18next";
import { attachmentUrl, type AttachmentItem } from "../api/pages";
import { useFormatters } from "../i18n/format";
import type { ElementId } from "../ids/elementId";
import styles from "./FileCard.module.css";
import { extensionOf } from "./fileNames";

/** A downloadable file: what every non-displayable attachment (SVG and HTML included) is shown as (F06.R2). */
export function FileCard({
  projectId,
  attachment,
}: {
  readonly projectId: ElementId;
  readonly attachment: AttachmentItem;
}) {
  const { t } = useTranslation();
  const format = useFormatters();
  return (
    <span className={styles["card"]}>
      <span className={styles["ext"]} aria-hidden>
        {extensionOf(attachment.fileName)}
      </span>
      <span className={styles["text"]}>
        <span className={styles["name"]}>{attachment.fileName}</span>
        <span className={styles["size"]}>{format.size(attachment.size)}</span>
      </span>
      <a
        href={attachmentUrl(projectId, attachment.id)}
        download={attachment.fileName}
        className={styles["download"]}
        aria-label={t("attachments.downloadNamed", { name: attachment.fileName })}
      >
        <Download aria-hidden size={16} />
      </a>
    </span>
  );
}
