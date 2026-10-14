import { useTranslation } from "react-i18next";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import styles from "./HelpDialog.module.css";

/** Default shortcuts (configurable with M5-01). Key labels are translated too (« Strg+O » in German). */
const shortcuts = ["openFolder", "save", "rename", "palette", "paletteShortcut", "close"] as const;

export function HelpDialog({
  open,
  onOpenChange,
}: {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
}) {
  const { t } = useTranslation();
  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("help.title")}
      footer={
        <Button
          variant="primary"
          onClick={() => {
            onOpenChange(false);
          }}
        >
          {t("common.close")}
        </Button>
      }
    >
      <dl className={styles["list"]}>
        {shortcuts.map((action) => (
          <div key={action} className={styles["row"]}>
            <dt>
              <kbd className={styles["kbd"]}>{t(`help.keys.${action}`)}</kbd>
            </dt>
            <dd>{t(`help.shortcuts.${action}`)}</dd>
          </div>
        ))}
      </dl>
    </Dialog>
  );
}
