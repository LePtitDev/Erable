import { useTranslation } from "react-i18next";
import { initialsOf } from "../../shared/api/projects";
import { useCurrentUserId } from "../project/useCurrentUserId";
import { useOtherUsers } from "./useOtherUsers";
import type { ElementId } from "../../shared/ids/elementId";
import {
  editorsOf,
  usePresenceStore,
  type PresenceUser,
} from "../../shared/realtime/presenceStore";
import styles from "./Presence.module.css";

function Avatar({ user }: { readonly user: PresenceUser }) {
  const { t } = useTranslation();
  const label = t(user.mode === "edit" ? "presence.editingLabel" : "presence.readingLabel", {
    name: user.name,
  });
  return (
    <li
      role="img"
      aria-label={label}
      title={label}
      className={`${styles["avatar"] ?? ""} ${user.mode === "edit" ? (styles["editing"] ?? "") : ""}`}
    >
      {initialsOf(user.name)}
    </li>
  );
}

/** Avatars (initials) of the others in the project, in the top bar; « +N » beyond the third (mockup « Page — lecture »). */
export function PresenceAvatars({ max = 3 }: { readonly max?: number }) {
  const { t } = useTranslation();
  const others = useOtherUsers();
  if (others.length === 0) return null;
  const shown = others.slice(0, max);
  const hidden = others.length - shown.length;
  return (
    <ul className={styles["avatars"]} aria-label={t("presence.online")}>
      {shown.map((user) => (
        <Avatar key={user.userId} user={user} />
      ))}
      {hidden > 0 && (
        <li
          className={`${styles["avatar"] ?? ""} ${styles["more"] ?? ""}`}
          aria-label={t("presence.more", { count: hidden })}
          title={t("presence.more", { count: hidden })}
        >
          +{hidden}
        </li>
      )}
    </ul>
  );
}

/** Avatars of the others who are on this very element, in its header. */
export function ElementPresence({ elementId }: { readonly elementId: ElementId }) {
  const { t } = useTranslation();
  const here = useOtherUsers().filter((u) => u.elementId === elementId);
  if (here.length === 0) return null;
  return (
    <ul className={styles["avatars"]} aria-label={t("presence.onThisPage")}>
      {here.map((user) => (
        <Avatar key={user.userId} user={user} />
      ))}
    </ul>
  );
}

/**
 * « Camille R. modifie aussi cette page — section « Histoire ». Vos changements seront fusionnés à l’enregistrement. »
 * (v1 wording). Shown to whoever is on a page that somebody else is editing, reader or editor.
 */
export function EditingBanner({ elementId }: { readonly elementId: ElementId }) {
  const { t } = useTranslation();
  const me = useCurrentUserId();
  const users = usePresenceStore((s) => s.users);
  const editors = editorsOf(users, elementId, me);
  const first = editors[0];
  if (first === undefined) return null;
  const section =
    editors.length === 1 && first.section !== null
      ? t("presence.section", { section: first.section })
      : "";
  return (
    <p role="status" className={styles["banner"]}>
      {t("presence.editing", {
        count: editors.length,
        names: editors.map((u) => u.name).join(", "),
        section,
      })}
    </p>
  );
}
