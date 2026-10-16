import { useTranslation } from "react-i18next";
import type { MigrationNeed } from "../../shared/api/projects";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";

/**
 * An older project must be converted before it opens (F01.R3, M2-09): says what happens, and does nothing until the
 * user confirms. The backup is made first and put back if the migration fails.
 */
export function MigrationDialog({
  need,
  busy,
  onConfirm,
  onCancel,
}: {
  /** The migration to confirm; null while there is none. */
  readonly need: MigrationNeed | null;
  readonly busy: boolean;
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Dialog
      open={need !== null}
      onOpenChange={(open) => {
        if (!open && !busy) onCancel();
      }}
      alert
      title={t("home.migrate.title")}
      description={
        need === null
          ? ""
          : t("home.migrate.description", { from: need.fromVersion, to: need.toVersion })
      }
      footer={
        <>
          <Button onClick={onCancel} disabled={busy}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" onClick={onConfirm} disabled={busy}>
            {t("home.migrate.confirm")}
          </Button>
        </>
      }
    >
      <p>{t("home.migrate.backup")}</p>
    </Dialog>
  );
}
