import { useTranslation } from "react-i18next";
import { shortcutActions } from "../../shared/keyboard/shortcuts";
import { useFormatCombo } from "../../shared/keyboard/useFormatCombo";
import { useShortcuts } from "../../shared/keyboard/useShortcuts";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import styles from "./HelpDialog.module.css";

/** The shortcuts that cannot be changed; the others come from the table of shortcuts (M5-01). */
const fixed = ["rename", "palette", "close"] as const;

export function HelpDialog({
  open,
  onOpenChange,
}: {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
}) {
  const { t } = useTranslation();
  const resolved = useShortcuts();
  const format = useFormatCombo();
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
        {shortcutActions.map((action) => (
          <div key={action} className={styles["row"]}>
            <dt>
              {resolved[action] === "" ? (
                t("shortcuts.none")
              ) : (
                <kbd className={styles["kbd"]}>{format(resolved[action])}</kbd>
              )}
            </dt>
            <dd>{t(`shortcuts.actions.${action}`)} *</dd>
          </div>
        ))}
        {fixed.map((action) => (
          <div key={action} className={styles["row"]}>
            <dt>
              <kbd className={styles["kbd"]}>{t(`help.keys.${action}`)}</kbd>
            </dt>
            <dd>{t(`help.shortcuts.${action}`)}</dd>
          </div>
        ))}
      </dl>
      <p className={styles["note"]}>{t("help.customize")}</p>
    </Dialog>
  );
}
