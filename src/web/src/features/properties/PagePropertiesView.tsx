import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import type { CategorySummary } from "../../shared/api/structure";
import type { ElementId } from "../../shared/ids/elementId";
import { KindBadge } from "../../shared/ui/KindBadge";
import { useConflictText, usePropertyFormat } from "./format";
import type { PageProperty } from "./pageProperties";
import styles from "./PagePropertiesView.module.css";

/** Pills of the categories of a page, in its header (mockup « Page — lecture », F07.R3). */
export function CategoryChips({
  projectId,
  categories,
}: {
  readonly projectId: ElementId;
  readonly categories: readonly CategorySummary[];
}) {
  const { t } = useTranslation();
  if (categories.length === 0) return null;
  return (
    <ul className={styles["chips"]} aria-label={t("page.categoriesLabel")}>
      {categories.map((category) => (
        <li key={category.id}>
          <Link
            to="/p/$projectId/e/$elementId"
            params={{ projectId, elementId: category.id }}
            className={styles["chip"]}
          >
            <KindBadge kind="category" size="sm" />
            {category.name}
          </Link>
        </li>
      ))}
    </ul>
  );
}

/** « hors règles »: at least one value of the page breaks the rules of its property (F08.R3). */
export function OutOfRulesBadge() {
  const { t } = useTranslation();
  return (
    <span className={styles["badge"]} title={t("page.outOfRulesHint")}>
      {t("page.outOfRules")}
    </span>
  );
}

/**
 * « Propriétés · N valeurs » of the side panel: name, type, effective value and where it comes from (« via PNJ »),
 * with the violations of the rules (F04.R4, E2E-05-12).
 */
export function PagePropertiesView({
  projectId,
  properties,
}: {
  readonly projectId: ElementId;
  readonly properties: readonly PageProperty[];
}) {
  const { t } = useTranslation();
  const format = usePropertyFormat(projectId);
  const conflictText = useConflictText(projectId);
  const valued = properties.filter((p) => p.value !== undefined).length;
  return (
    <section className={styles["section"]} aria-labelledby="page-properties">
      <h2 id="page-properties" className={styles["title"]}>
        {t("page.propertiesTitle")}{" "}
        <span className={styles["count"]}>· {t("page.valueCount", { count: valued })}</span>
      </h2>
      {properties.length === 0 ? (
        <p className={styles["muted"]}>{t("page.noProperties")}</p>
      ) : (
        <ul className={styles["list"]} aria-label={t("page.propertiesTitle")}>
          {properties.map((property) => (
            <li key={property.definition.id} className={styles["row"]}>
              <div className={styles["head"]}>
                <Link
                  to="/p/$projectId/e/$elementId"
                  params={{ projectId, elementId: property.definition.id }}
                  className={styles["name"]}
                >
                  {property.definition.name}
                </Link>
                <span className={styles["type"]}>{property.definition.type.scalar}</span>
              </div>
              {property.origin === "conflict" ? (
                <p className={styles["conflict"]}>
                  {conflictText(property.definition, property.candidates)}
                </p>
              ) : (
                <p className={styles["value"]}>
                  {format.value(property.definition, property.value)}
                  {property.origin === "inherited" && property.sources.length > 0 && (
                    <span className={styles["via"]}>
                      {" "}
                      {t("page.via", {
                        categories: property.sources.map((s) => s.name).join(", "),
                      })}
                    </span>
                  )}
                </p>
              )}
              {property.violations.map((violation, index) => (
                <p key={index} className={styles["violation"]}>
                  {format.violation(property.definition, violation)}
                </p>
              ))}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
