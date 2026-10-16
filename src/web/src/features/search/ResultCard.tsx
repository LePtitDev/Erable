import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import type { SearchHit } from "../../shared/api/search";
import { propertiesQuery } from "../../shared/api/structure";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import { parseRule } from "../../shared/properties/rules";
import { KindBadge } from "../../shared/ui/KindBadge";
import { useAuthorName } from "../project/authors";
import { toDefinition } from "../properties/definitions";
import { usePropertyFormat } from "../properties/format";
import { Highlighted } from "./Highlighted";
import styles from "./SearchView.module.css";

/** One result (mockup « Recherche »): type, title, path, marked excerpt, categories, and what changed last. */
export function ResultCard({
  projectId,
  hit,
}: {
  readonly projectId: ElementId;
  readonly hit: SearchHit;
}) {
  const { t } = useTranslation();
  const format = useFormatters();
  const authorName = useAuthorName();
  const issues = useIssueTexts(projectId, hit);
  const path = hit.path.map((step) => step.name).join(" / ");
  const meta =
    hit.attachment === null
      ? t("element.modified", {
          when: format.relative(new Date(hit.updatedAt)),
          who: authorName(hit.updatedBy, hit.updatedByName),
        })
      : `${hit.attachment.mime} · ${format.size(hit.attachment.size)}`;
  return (
    <li className={styles["result"]}>
      <Link
        to="/p/$projectId/e/$elementId"
        params={{ projectId, elementId: hit.id }}
        className={styles["resultLink"]}
      >
        <KindBadge kind={hit.kind} />
        <span className={styles["resultBody"]}>
          <span className={styles["resultHead"]}>
            <span className={styles["resultTitle"]} data-testid="result-title">
              <Highlighted segments={hit.title} />
            </span>
            <span className={styles["resultPath"]}>{path}</span>
          </span>
          {hit.excerpt.length > 0 && (
            <span className={styles["excerpt"]}>
              <Highlighted segments={hit.excerpt} />
            </span>
          )}
          {issues.length > 0 && (
            <span className={styles["issues"]}>
              {issues.map((issue) => (
                <span key={issue} className={styles["issue"]}>
                  {issue}
                </span>
              ))}
            </span>
          )}
          <span className={styles["meta"]}>
            {hit.categories.map((category) => (
              <span key={category.id} className={styles["category"]}>
                {category.name}
              </span>
            ))}
            {meta}
          </span>
        </span>
      </Link>
    </li>
  );
}

/** « Niveau : Doit être inférieur ou égal à 60 » for each rule the page breaks (filter « hors règles », E2E-10-06). */
function useIssueTexts(projectId: ElementId, hit: SearchHit): string[] {
  const { t } = useTranslation();
  const properties = useQuery({ ...propertiesQuery(projectId), enabled: hit.issues.length > 0 });
  const format = usePropertyFormat(projectId);
  return hit.issues.map((issue) => {
    const item = properties.data?.find((p) => p.id === issue.propertyId);
    const definition = item === undefined ? undefined : toDefinition(item);
    const rule =
      definition === undefined ? null : parseRule(issue.rule, definition.type, definition.options);
    if (definition === undefined || rule === null) return issue.propertyName;
    return t("search.violation", {
      property: issue.propertyName,
      message: format.violation(
        definition,
        issue.index === null ? { rule } : { rule, index: issue.index },
      ),
    });
  });
}
