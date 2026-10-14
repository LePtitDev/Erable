import { useTranslation } from "react-i18next";
import { LanguageSwitcher } from "../../shared/i18n/LanguageSwitcher";
import { ThemeSwitcher } from "../../shared/theme/ThemeSwitcher";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import field from "../../shared/ui/Field.module.css";

/** Preferences reachable from the home screen and from a project (F15). Shortcuts join with M5-01. */
export function PreferencesDialog({
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
      title={t("preferences.title")}
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
      <div className={field["form"]}>
        <LanguageSwitcher />
        <ThemeSwitcher />
      </div>
    </Dialog>
  );
}
