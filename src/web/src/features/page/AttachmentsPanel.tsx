import { Plus } from "lucide-react";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { attachmentUrl, type AttachmentItem } from "../../shared/api/pages";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import { extensionOf } from "../../shared/ui/fileNames";
import styles from "./PageView.module.css";
import { useUploader } from "./useUploader";

interface Running {
  readonly key: number;
  readonly name: string;
  readonly fraction: number;
}

let nextKey = 0;

/**
 * « Pièces jointes » of the side panel (F04.R4): extension, name, size. « + Ajouter » attaches files to the page
 * without inserting them in the text (E2E-06-03).
 */
export function AttachmentsPanel({
  projectId,
  pageId,
  attachments,
}: {
  readonly projectId: ElementId;
  readonly pageId: ElementId;
  readonly attachments: readonly AttachmentItem[];
}) {
  const { t } = useTranslation();
  const format = useFormatters();
  const upload = useUploader(projectId, pageId);
  const picker = useRef<HTMLInputElement>(null);
  const [running, setRunning] = useState<readonly Running[]>([]);
  const [errors, setErrors] = useState<readonly string[]>([]);

  const add = (files: readonly File[]) => {
    setErrors([]);
    for (const file of files) {
      const key = nextKey++;
      setRunning((list) => [...list, { key, name: file.name, fraction: 0 }]);
      void upload(file, (fraction) => {
        setRunning((list) => list.map((r) => (r.key === key ? { ...r, fraction } : r)));
      }).then((result) => {
        setRunning((list) => list.filter((r) => r.key !== key));
        if (!result.ok && result.message !== null) {
          const message = result.message;
          setErrors((list) => [...list, message]);
        }
      });
    }
  };

  return (
    <section aria-labelledby="attachments-title" className={styles["panelSection"]}>
      <div className={styles["panelHeader"]}>
        <h2 id="attachments-title" className={styles["panelTitle"]}>
          {t("attachments.title")}
        </h2>
        <button
          type="button"
          className={styles["panelAction"]}
          aria-label={t("attachments.addLabel")}
          onClick={() => picker.current?.click()}
        >
          <Plus aria-hidden size={14} />
          {t("attachments.add")}
        </button>
        <input
          ref={picker}
          type="file"
          multiple
          hidden
          onChange={(e) => {
            add([...(e.target.files ?? [])]);
            e.target.value = "";
          }}
        />
      </div>
      {errors.length > 0 && (
        <div role="alert" className={styles["panelError"]}>
          {errors.map((message) => (
            <p key={message}>{message}</p>
          ))}
        </div>
      )}
      {attachments.length === 0 && running.length === 0 ? (
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
          {running.map((r) => (
            <li key={`running-${String(r.key)}`} className={styles["attachment"]}>
              <span className={styles["ext"]} aria-hidden>
                {extensionOf(r.name)}
              </span>
              <span className={styles["attachmentText"]}>
                <span className={styles["attachmentName"]}>{r.name}</span>
                <progress
                  className={styles["progress"]}
                  max={100}
                  value={Math.round(r.fraction * 100)}
                  aria-label={t("attachments.uploading", { name: r.name })}
                />
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
