import { useQuery } from "@tanstack/react-query";
import { DropdownMenu } from "radix-ui";
import { useId, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { categoriesQuery, propertiesQuery } from "../../shared/api/structure";
import type { ElementKind } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { KindBadge } from "../../shared/ui/KindBadge";
import { toDefinition } from "../properties/definitions";
import { ConditionRow } from "./ConditionRow";
import { newCondition } from "./conditions";
import styles from "./SearchView.module.css";
import {
  isFiltered,
  modifiedWindows,
  searchKinds,
  toggleCategory,
  toggleKind,
  type ModifiedWindow,
  type SearchState,
} from "./searchState";

/** Categories shown as chips before « + Autre » (mockup « Recherche »): the chosen ones, then the first by name. */
const visibleCategories = 6;

/**
 * The filters of the search screen (F10.R3, mockup « Recherche »): type with counters, categories, conditions on
 * properties, period of modification, « hors règles ». Every change goes to the URL through `onChange`.
 */
export function FiltersPanel({
  projectId,
  state,
  facets,
  onChange,
  onReset,
}: {
  readonly projectId: ElementId;
  readonly state: SearchState;
  readonly facets: ReadonlyMap<ElementKind, number> | undefined;
  readonly onChange: (next: SearchState) => void;
  readonly onReset: () => void;
}) {
  const { t } = useTranslation();
  return (
    <aside className={styles["filters"]} aria-label={t("search.filters")}>
      <div className={styles["filtersHead"]}>
        <h2 className={styles["filtersTitle"]}>{t("search.filters")}</h2>
        <Button variant="ghost" size="sm" onClick={onReset} disabled={!isFiltered(state)}>
          {t("search.reset")}
        </Button>
      </div>
      <TypeFilter state={state} facets={facets} onChange={onChange} />
      <CategoryFilter projectId={projectId} state={state} onChange={onChange} />
      <PropertyFilter projectId={projectId} state={state} onChange={onChange} />
      <ModifiedFilter state={state} onChange={onChange} />
      <label className={styles["check"]}>
        <input
          type="checkbox"
          checked={state.outOfRules}
          onChange={(event) => {
            onChange({ ...state, outOfRules: event.target.checked });
          }}
        />
        {t("search.outOfRules")}
      </label>
    </aside>
  );
}

function TypeFilter({
  state,
  facets,
  onChange,
}: {
  readonly state: SearchState;
  readonly facets: ReadonlyMap<ElementKind, number> | undefined;
  readonly onChange: (next: SearchState) => void;
}) {
  const { t } = useTranslation();
  return (
    <fieldset className={styles["group"]}>
      <legend className={styles["groupTitle"]}>{t("search.type")}</legend>
      {searchKinds.map((kind) => (
        <label key={kind} className={styles["kind"]}>
          <input
            type="checkbox"
            checked={state.kinds.length === 0 || state.kinds.includes(kind)}
            onChange={() => {
              onChange(toggleKind(state, kind));
            }}
          />
          <KindBadge kind={kind} />
          <span className={styles["kindLabel"]}>{t(`search.kinds.${kind}`)}</span>
          {facets !== undefined && (
            <>
              {" "}
              <span className={styles["count"]}>{facets.get(kind) ?? 0}</span>
            </>
          )}
        </label>
      ))}
    </fieldset>
  );
}

function CategoryFilter({
  projectId,
  state,
  onChange,
}: {
  readonly projectId: ElementId;
  readonly state: SearchState;
  readonly onChange: (next: SearchState) => void;
}) {
  const { t } = useTranslation();
  const categories = useQuery(categoriesQuery(projectId));
  const subId = useId();
  const [chips, others] = useMemo(() => {
    const all = [...(categories.data ?? [])].sort((a, b) => a.name.localeCompare(b.name));
    const chosen = all.filter((c) => state.categories.includes(c.id));
    const rest = all.filter((c) => !state.categories.includes(c.id));
    const room = Math.max(visibleCategories - chosen.length, 0);
    return [[...chosen, ...rest.slice(0, room)], rest.slice(room)] as const;
  }, [categories.data, state.categories]);

  return (
    <div className={styles["group"]}>
      <span className={styles["groupTitle"]} id={`${subId}-title`}>
        {t("search.categories")}
      </span>
      <div className={styles["chips"]} role="group" aria-labelledby={`${subId}-title`}>
        {chips.map((category) => {
          const pressed = state.categories.includes(category.id);
          return (
            <button
              key={category.id}
              type="button"
              aria-pressed={pressed}
              className={`${styles["chip"] ?? ""} ${pressed ? (styles["chipOn"] ?? "") : ""}`}
              onClick={() => {
                onChange(toggleCategory(state, category.id));
              }}
            >
              {category.name}
            </button>
          );
        })}
        {others.length > 0 && (
          <DropdownMenu.Root>
            <DropdownMenu.Trigger
              className={`${styles["chip"] ?? ""} ${styles["chipOther"] ?? ""}`}
            >
              {t("search.otherCategory")}
            </DropdownMenu.Trigger>
            <DropdownMenu.Portal>
              <DropdownMenu.Content
                className={styles["menu"]}
                align="start"
                sideOffset={4}
                aria-label={t("search.otherCategories")}
              >
                {others.map((category) => (
                  <DropdownMenu.Item
                    key={category.id}
                    className={styles["menuItem"]}
                    onSelect={() => {
                      onChange(toggleCategory(state, category.id));
                    }}
                  >
                    {category.name}
                  </DropdownMenu.Item>
                ))}
              </DropdownMenu.Content>
            </DropdownMenu.Portal>
          </DropdownMenu.Root>
        )}
      </div>
      {state.categories.length > 0 && (
        <label className={styles["check"]}>
          <input
            type="checkbox"
            checked={state.subCategories}
            onChange={(event) => {
              onChange({ ...state, subCategories: event.target.checked });
            }}
          />
          {t("search.includeSub")}
        </label>
      )}
    </div>
  );
}

function PropertyFilter({
  projectId,
  state,
  onChange,
}: {
  readonly projectId: ElementId;
  readonly state: SearchState;
  readonly onChange: (next: SearchState) => void;
}) {
  const { t } = useTranslation();
  const properties = useQuery(propertiesQuery(projectId));
  const definitions = useMemo(
    () =>
      [...(properties.data ?? [])].map(toDefinition).sort((a, b) => a.name.localeCompare(b.name)),
    [properties.data],
  );
  const first = definitions[0];

  return (
    <div className={styles["group"]}>
      <span className={styles["groupTitle"]}>{t("search.properties")}</span>
      {state.conditions.map((condition, index) => (
        <ConditionRow
          // The position is the identity of a condition: two lines may have the same content.
          key={index}
          projectId={projectId}
          index={index}
          condition={condition}
          definition={definitions.find((d) => d.id === condition.property)}
          definitions={definitions}
          onChange={(next) => {
            onChange({
              ...state,
              conditions: state.conditions.map((c, i) => (i === index ? next : c)),
            });
          }}
          onRemove={() => {
            onChange({ ...state, conditions: state.conditions.filter((_, i) => i !== index) });
          }}
        />
      ))}
      <Button
        variant="ghost"
        size="sm"
        className={styles["addCondition"]}
        disabled={first === undefined}
        onClick={() => {
          if (first !== undefined) {
            onChange({ ...state, conditions: [...state.conditions, newCondition(first)] });
          }
        }}
      >
        {t("search.addCondition")}
      </Button>
    </div>
  );
}

function ModifiedFilter({
  state,
  onChange,
}: {
  readonly state: SearchState;
  readonly onChange: (next: SearchState) => void;
}) {
  const { t } = useTranslation();
  const options: readonly (ModifiedWindow | null)[] = [...modifiedWindows, null];
  return (
    <div className={styles["group"]}>
      <span className={styles["groupTitle"]}>{t("search.modified.label")}</span>
      <div className={styles["segments"]} role="group" aria-label={t("search.modified.group")}>
        {options.map((option) => (
          <button
            key={option ?? "all"}
            type="button"
            aria-pressed={state.modified === option}
            className={styles["segment"]}
            onClick={() => {
              onChange({ ...state, modified: option });
            }}
          >
            {t(`search.modified.${option ?? "all"}`)}
          </button>
        ))}
      </div>
    </div>
  );
}
