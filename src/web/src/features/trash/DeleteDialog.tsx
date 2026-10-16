import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { queryKeys } from "../../shared/api/keys";
import { deleteElement, deleteImpactQuery, type DeleteImpactView } from "../../shared/api/trash";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import styles from "./DeleteDialog.module.css";

/**
 * Confirmation before an element goes to the trash (F12.R1–R2): what mentions it, what goes with it. Nothing is
 * deleted until the user confirms; the element can be restored afterwards.
 */
export function DeleteDialog({
  projectId,
  id,
  onClose,
  onDeleted,
}: {
  readonly projectId: ElementId;
  /** The element to delete; null while the dialog is closed. */
  readonly id: ElementId | null;
  readonly onClose: () => void;
  readonly onDeleted: (id: ElementId) => void;
}) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const impact = useQuery({
    ...deleteImpactQuery(projectId, id ?? projectId),
    enabled: id !== null,
  });
  const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);

  const close = () => {
    setError(null);
    onClose();
  };

  const confirm = async () => {
    if (id === null) return;
    setDeleting(true);
    setError(null);
    try {
      await deleteElement(projectId, id);
      await client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });
      onDeleted(id);
      onClose();
    } catch (failure) {
      setError(errorMessage(t, failure));
    } finally {
      setDeleting(false);
    }
  };

  return (
    <Dialog
      open={id !== null}
      onOpenChange={(open) => {
        if (!open) close();
      }}
      alert
      title={
        impact.data === undefined
          ? t("trash.deleteTitleLoading")
          : t("trash.deleteTitle", { name: impact.data.name })
      }
      footer={
        <>
          <Button onClick={close} disabled={deleting}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="danger"
            disabled={impact.data === undefined || deleting}
            onClick={() => {
              void confirm();
            }}
          >
            {t("trash.deleteConfirm")}
          </Button>
        </>
      }
    >
      <div className={styles["body"]}>
        {impact.isPending && id !== null && <p aria-busy="true">{t("common.loading")}</p>}
        {impact.data !== undefined && <Consequences impact={impact.data} />}
        <p className={styles["hint"]}>{t("trash.deleteHint")}</p>
        {error !== null && (
          <p role="alert" className={styles["error"]}>
            {error}
          </p>
        )}
      </div>
    </Dialog>
  );
}

function Consequences({ impact }: { readonly impact: DeleteImpactView }) {
  const { t } = useTranslation();
  const names = impact.referencedBy.map((r) => r.name).join(", ");
  const lines: string[] = [];
  if (impact.referencedBy.length > 0) {
    lines.push(
      impact.kind === "attachment"
        ? t("trash.usedElsewhere", { count: impact.referencedBy.length, names })
        : t("trash.referencedBy", { count: impact.referencedBy.length, names }),
    );
  }
  if (impact.subPages > 0) {
    lines.push(t("trash.cascade", { count: impact.subPages, name: impact.name }));
  } else if (impact.descendants > 0) {
    lines.push(t("trash.contents", { count: impact.descendants, name: impact.name }));
  }
  if (impact.links > 0) {
    lines.push(
      impact.kind === "linkType"
        ? t("trash.linksOfType", { count: impact.links })
        : t("trash.linksOfPage", { count: impact.links }),
    );
  }
  if (impact.pages > 0) lines.push(t("trash.categoryPages", { count: impact.pages }));
  if (impact.values > 0) lines.push(t("trash.propertyValues", { count: impact.values }));
  return (
    <ul className={styles["consequences"]} aria-label={t("trash.consequences")}>
      {lines.map((line) => (
        <li key={line}>{line}</li>
      ))}
    </ul>
  );
}
