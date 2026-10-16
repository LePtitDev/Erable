import { useTranslation } from "react-i18next";
import { useCurrentUserId } from "./useCurrentUserId";

/** Display name of an author: « moi » for the current user (E2E-05-01), the name given by the API otherwise. */
export function useAuthorName() {
  const { t } = useTranslation();
  const me = useCurrentUserId();
  return (id: string, name: string) => (me === id ? t("element.me") : name);
}
