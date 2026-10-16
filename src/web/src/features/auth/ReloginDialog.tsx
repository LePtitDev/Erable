import { useTranslation } from "react-i18next";
import { useQueryClient } from "@tanstack/react-query";
import { finishRelogin, useAuthStore } from "../../shared/api/auth";
import { meQuery } from "../../shared/api/projects";
import { Dialog } from "../../shared/ui/Dialog";
import { LoginForm } from "./LoginForm";

/**
 * The session ended while the user was working (E2E-14-09): a dialog over the screen, which stays where it is. The
 * call that failed is replayed once the user is back, so an unsaved page is saved with its changes intact.
 */
export function ReloginDialog() {
  const { t } = useTranslation();
  const open = useAuthStore((s) => s.reloginOpen);
  const client = useQueryClient();
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
        // The same user back on the same screen: nothing is reloaded, an unsaved page is still there.
        resetCache={false}
        onSignedIn={(session) => {
          if (client.getQueryData(meQuery.queryKey)?.id === session.id) {
            finishRelogin(true);
          } else {
            // Somebody else signed in: nothing of the screen belongs to them.
            globalThis.location.reload();
          }
        }}
      />
    </Dialog>
  );
}
