import { useTranslation } from "react-i18next";
import { finishRelogin, useAuthStore } from "../../shared/api/auth";
import { Dialog } from "../../shared/ui/Dialog";
import { LoginForm } from "./LoginForm";

/**
 * The session ended while the user was working (E2E-14-09): a dialog over the screen, which stays where it is. The
 * call that failed is replayed once the user is back, so an unsaved page is saved with its changes intact.
 */
export function ReloginDialog() {
  const { t } = useTranslation();
  const open = useAuthStore((s) => s.reloginOpen);
  return (
    <Dialog
      open={open}
      // Neither Escape nor a click outside closes it: the work is waiting for a session.
      onOpenChange={() => undefined}
      title={t("auth.expired.title")}
      description={t("auth.expired.description")}
      size="sm"
    >
      <LoginForm
        onSignedIn={() => {
          finishRelogin(true);
        }}
      />
    </Dialog>
  );
}
