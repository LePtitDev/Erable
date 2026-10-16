import { useQuery } from "@tanstack/react-query";
import { LogOut } from "lucide-react";
import { DropdownMenu } from "radix-ui";
import { useTranslation } from "react-i18next";
import { logout } from "../../shared/api/auth";
import { initialsOf, meQuery } from "../../shared/api/projects";
import { apiInfoQuery } from "../../shared/api/queries";
import styles from "./UserMenu.module.css";

/**
 * The avatar of the signed-in user (initials). On a server it opens a menu with « Se déconnecter » (E2E-14-01); on
 * the desktop there is nobody to sign out.
 */
export function UserMenu() {
  const { t } = useTranslation();
  const info = useQuery(apiInfoQuery);
  const me = useQuery(meQuery);
  if (!me.isSuccess) return null;

  const initials = initialsOf(me.data.name);
  if (info.data?.mode !== "server") {
    return (
      <span className={styles["avatar"]} title={me.data.name}>
        {initials}
      </span>
    );
  }

  const signOut = () => {
    // The session ends on the server, then the page is loaded afresh: no cached data of this user survives in the
    // browser, and the gate shows the login screen.
    logout()
      .catch(() => undefined)
      .finally(() => {
        globalThis.location.assign("/");
      });
  };

  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger asChild>
        <button
          type="button"
          className={`${styles["avatar"] ?? ""} ${styles["trigger"] ?? ""}`}
          aria-label={t("auth.userMenu", { name: me.data.name })}
          title={me.data.name}
        >
          {initials}
        </button>
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content className={styles["menu"]} align="end" sideOffset={6}>
          <DropdownMenu.Label className={styles["who"]}>
            {t("auth.signedInAs", { name: me.data.name })}
          </DropdownMenu.Label>
          <DropdownMenu.Item className={styles["item"]} onSelect={signOut}>
            <LogOut aria-hidden size={16} />
            {t("auth.logout")}
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
