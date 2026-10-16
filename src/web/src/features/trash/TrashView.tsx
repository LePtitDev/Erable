import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "@tanstack/react-router";
import { RotateCcw, Trash2 } from "lucide-react";
import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { queryKeys } from "../../shared/api/keys";
import { projectQuery } from "../../shared/api/projects";
import {
  emptyTrash,
  purgeEntry,
  restoreEntry,
  trashQuery,
  type RestoreView,
  type TrashEntryView,
} from "../../shared/api/trash";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import { KindBadge } from "../../shared/ui/KindBadge";
import { useCanEdit } from "../project/access";
import styles from "./TrashView.module.css";

/**
 * The trash (F12): every deletion with what came along, restore (F12.R3), delete for good and empty (F12.R5, which
 * asks to type the name of the project).
 */
export function TrashView() {
  const { t } = useTranslation();
  const canEdit = useCanEdit();
  const client = useQueryClient();
  const format = useFormatters();
  const { projectId } = useParams({ from: "/p/$projectId/trash" });
  const trash = useQuery({
    ...trashQuery(projectId ?? ("" as ElementId)),
    enabled: projectId !== null,
  });
  const [restored, setRestored] = useState<RestoreView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [purging, setPurging] = useState<TrashEntryView | null>(null);
  const [emptying, setEmptying] = useState(false);
  const headingId = useId();

  if (projectId === null) return null;
  if (trash.isError) throw trash.error;
  const entries = trash.data ?? [];

  const refresh = () => client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });

  const restore = (entry: TrashEntryView) => {
    setError(null);
    setRestored(null);
    restoreEntry(projectId, entry.id)
      .then((result) => {
        setRestored(result);
        return refresh();
      })
      .catch((failure: unknown) => {
        setError(errorMessage(t, failure));
      });
  };

  return (
    <section className={styles["view"]} aria-labelledby={headingId}>
      <header className={styles["header"]}>
        <div>
          <h1 id={headingId} className={styles["title"]}>
            {t("trash.title")}
          </h1>
          <p className={styles["muted"]}>{t("trash.intro")}</p>
        </div>
        {canEdit && (
          <Button
            variant="danger"
            disabled={entries.length === 0}
            onClick={() => {
              setEmptying(true);
            }}
          >
            <Trash2 aria-hidden size={16} />
            {t("trash.emptyAction")}
          </Button>
        )}
      </header>
      {restored !== null && (
        <p role="status" className={styles["notice"]}>
          {restored.relocated
            ? t("trash.relocated", { name: restored.name, parent: restored.parentName })
            : t("trash.restored", { name: restored.name, parent: restored.parentName })}{" "}
          <Link
            to="/p/$projectId/e/$elementId"
            params={{ projectId, elementId: restored.id }}
            className={styles["link"]}
          >
            {t("trash.open")}
          </Link>
        </p>
      )}
      {error !== null && (
        <p role="alert" className={styles["error"]}>
          {error}
        </p>
      )}
      {trash.isSuccess && entries.length === 0 ? (
        <p className={styles["empty"]}>{t("trash.empty")}</p>
      ) : (
        <ul className={styles["list"]} aria-label={t("trash.list")}>
          {entries.map((entry) => (
            <li key={entry.id} className={styles["entry"]}>
              <KindBadge kind={entry.kind} />
              <div className={styles["text"]}>
                <span className={styles["name"]}>
                  {entry.name}
                  {entry.descendants > 0 && (
                    <span className={styles["with"]}>
                      {" "}
                      {t("trash.withDescendants", { count: entry.descendants })}
                    </span>
                  )}
                </span>
                <span className={styles["meta"]}>
                  {t("trash.deleted", {
                    when: format.relative(new Date(entry.deletedAt)),
                    who: entry.deletedBy,
                  })}
                  {" · "}
                  {entry.parentAvailable
                    ? t("trash.originalPlace", { path: entry.path.map((p) => p.name).join(" / ") })
                    : t("trash.parentGone")}
                </span>
              </div>
              {canEdit && (
                <>
                  <Button
                    size="sm"
                    aria-label={t("trash.restoreLabel", { name: entry.name })}
                    onClick={() => {
                      restore(entry);
                    }}
                  >
                    <RotateCcw aria-hidden size={14} />
                    {t("trash.restore")}
                  </Button>
                  <Button
                    size="sm"
                    variant="danger"
                    aria-label={t("trash.purgeLabel", { name: entry.name })}
                    onClick={() => {
                      setPurging(entry);
                    }}
                  >
                    {t("trash.purge")}
                  </Button>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
      <PurgeDialog
        projectId={projectId}
        entry={purging}
        onClose={() => {
          setPurging(null);
        }}
        onDone={() => {
          setPurging(null);
          void refresh();
        }}
      />
      <EmptyDialog
        projectId={projectId}
        open={emptying}
        count={entries.length}
        onClose={() => {
          setEmptying(false);
        }}
        onDone={() => {
          setEmptying(false);
          setRestored(null);
          void refresh();
        }}
      />
    </section>
  );
}

function PurgeDialog({
  projectId,
  entry,
  onClose,
  onDone,
}: {
  readonly projectId: ElementId;
  readonly entry: TrashEntryView | null;
  readonly onClose: () => void;
  readonly onDone: () => void;
}) {
  const { t } = useTranslation();
  const [error, setError] = useState<string | null>(null);
  return (
    <Dialog
      open={entry !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      alert
      size="sm"
      title={t("trash.purgeTitle", { name: entry?.name ?? "" })}
      description={t("trash.purgeDescription")}
      footer={
        <>
          <Button onClick={onClose}>{t("common.cancel")}</Button>
          <Button
            variant="danger"
            onClick={() => {
              if (entry === null) return;
              purgeEntry(projectId, entry.id)
                .then(onDone)
                .catch((failure: unknown) => {
                  setError(errorMessage(t, failure));
                });
            }}
          >
            {t("trash.purge")}
          </Button>
        </>
      }
    >
      {error !== null && (
        <p role="alert" className={styles["error"]}>
          {error}
        </p>
      )}
    </Dialog>
  );
}

/** « Vider la corbeille »: definitive, so the user types the name of the project (F12.R5, E2E-12-06). */
function EmptyDialog({
  projectId,
  open,
  count,
  onClose,
  onDone,
}: {
  readonly projectId: ElementId;
  readonly open: boolean;
  readonly count: number;
  readonly onClose: () => void;
  readonly onDone: () => void;
}) {
  const { t } = useTranslation();
  const project = useQuery(projectQuery(projectId));
  const fieldId = useId();
  const [typed, setTyped] = useState("");
  const [error, setError] = useState<string | null>(null);
  const name = project.data?.name ?? "";
  const close = () => {
    setTyped("");
    setError(null);
    onClose();
  };
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) close();
      }}
      alert
      title={t("trash.emptyTitle")}
      description={t("trash.emptyDescription", { count })}
      footer={
        <>
          <Button onClick={close}>{t("common.cancel")}</Button>
          <Button
            variant="danger"
            disabled={name === "" || typed !== name}
            onClick={() => {
              emptyTrash(projectId)
                .then(() => {
                  setTyped("");
                  onDone();
                })
                .catch((failure: unknown) => {
                  setError(errorMessage(t, failure));
                });
            }}
          >
            {t("trash.emptyConfirm")}
          </Button>
        </>
      }
    >
      <div className={styles["dialogBody"]}>
        <label htmlFor={fieldId} className={styles["fieldLabel"]}>
          {t("trash.emptyType", { name })}
        </label>
        <input
          id={fieldId}
          className={styles["input"]}
          autoComplete="off"
          value={typed}
          onChange={(event) => {
            setTyped(event.target.value);
          }}
        />
        {error !== null && (
          <p role="alert" className={styles["error"]}>
            {error}
          </p>
        )}
      </div>
    </Dialog>
  );
}
