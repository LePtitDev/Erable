import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { meQuery } from "../../shared/api/projects";

/** Display name of an author: « moi » for the current user (E2E-05-01), the name given by the API otherwise. */
export function useAuthorName() {
  const { t } = useTranslation();
  const me = useQuery(meQuery);
  return (id: string, name: string) => (me.data?.id === id ? t("element.me") : name);
}
