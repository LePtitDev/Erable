import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { ChevronRight, Pencil, Plus, Search, X } from "lucide-react";
import { useId, useMemo, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { queryKeys } from "../../shared/api/keys";
import {
  categoriesQuery,
  categoryPagesQuery,
  categoryQuery,
  propertiesQuery,
  setCategoryValue,
  updateCategory,
  type CategoryPropertyItem,
  type CategoryView as CategoryData,
} from "../../shared/api/structure";
import type { ElementDetails } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import type { PropertyValue } from "../../shared/properties/types";
import { Button } from "../../shared/ui/Button";
import { emptySearch, toSearchParams } from "../search/searchState";
import { KindBadge } from "../../shared/ui/KindBadge";
import { ElementHeader } from "../project/ElementHeader";
import { readValue, toDefinition, typeName, type Definition } from "../properties/definitions";
import { usePropertyFormat } from "../properties/format";
import { AddPropertyDialog } from "../properties/AddPropertyDialog";
import { ValueEditor } from "../properties/ValueEditor";
import { loopOf } from "./inheritance";
import styles from "./CategoryView.module.css";

interface Draft {
  readonly description: string;
  readonly parentId: ElementId | null;
  /** Own values edited in the draft (undefined while the field is empty: removed on save). */
  readonly values: ReadonlyMap<ElementId, PropertyValue | undefined>;
  /** Own values removed in the draft. */
  readonly removed: ReadonlySet<ElementId>;
}

/** The category screen (F07, mockup « Catégorie — définition »). */
export function CategoryView({
  projectId,
  element,
  edit = false,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
  /** Opens in edit mode (a category just created from the tree). */
  readonly edit?: boolean;
}) {
  const { t } = useTranslation();
  const view = useQuery(categoryQuery(projectId, element.id));
  const titleId = useId();
  const [draft, setDraft] = useState<Draft | null>(null);
  const [wantEdit, setWantEdit] = useState(edit);
  if (wantEdit && view.isSuccess && draft === null) {
    setWantEdit(false);
    setDraft(draftOf(view.data));
  }

  if (view.isError) throw view.error;
  if (view.isPending) return <p aria-busy="true">{t("common.loading")}</p>;
  const data = view.data;

  return (
    <article className={styles["view"]} aria-labelledby={titleId}>
      <ElementHeader
        projectId={projectId}
        element={data.element}
        kind
        titleId={titleId}
        actions={
          draft === null ? (
            <Button
              variant="primary"
              onClick={() => {
                setDraft(draftOf(data));
              }}
            >
              <Pencil aria-hidden size={16} />
              {t("page.edit")}
            </Button>
          ) : undefined
        }
      />
      {draft === null ? (
        <CategoryReading projectId={projectId} data={data} />
      ) : (
        <CategoryEditing
          projectId={projectId}
          data={data}
          draft={draft}
          onChange={setDraft}
          onDone={() => {
            setDraft(null);
          }}
        />
      )}
      <CategoryPages projectId={projectId} data={data} />
    </article>
  );
}

function draftOf(data: CategoryData): Draft {
  return {
    description: data.description,
    parentId: data.parent?.id ?? null,
    values: new Map(),
    removed: new Set(),
  };
}

function CategoryReading({
  projectId,
  data,
}: {
  readonly projectId: ElementId;
  readonly data: CategoryData;
}) {
  const { t } = useTranslation();
  return (
    <>
      <div className={styles["cards"]}>
        <section className={styles["card"]} aria-labelledby="category-description">
          <h2 id="category-description" className={styles["cardTitle"]}>
            {t("category.description")}
          </h2>
          {data.description === "" ? (
            <p className={styles["muted"]}>{t("category.noDescription")}</p>
          ) : (
            <p className={styles["description"]}>{data.description}</p>
          )}
        </section>
        <Inheritance projectId={projectId} data={data} />
      </div>
      <PropertyTable projectId={projectId} data={data} draft={null} onDraft={() => undefined} />
    </>
  );
}

function Inheritance({
  projectId,
  data,
  children,
}: {
  readonly projectId: ElementId;
  readonly data: CategoryData;
  readonly children?: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <section className={styles["card"]} aria-labelledby="category-inheritance">
      <h2 id="category-inheritance" className={styles["cardTitle"]}>
        {t("category.inheritance")}
      </h2>
      <nav aria-label={t("category.lineage")}>
        <ol className={styles["lineage"]}>
          {data.lineage.map((ancestor) => (
            <li key={ancestor.id} className={styles["step"]}>
              <Link
                to="/p/$projectId/e/$elementId"
                params={{ projectId, elementId: ancestor.id }}
                className={styles["chip"]}
              >
                <KindBadge kind="category" size="sm" />
                {ancestor.name}
              </Link>
              <ChevronRight aria-hidden size={14} className={styles["arrow"]} />
            </li>
          ))}
          <li className={styles["step"]}>
            <span className={styles["current"]} aria-current="page">
              <KindBadge kind="category" size="sm" />
              {data.element.name}
            </span>
          </li>
        </ol>
      </nav>
      {children}
      <p className={styles["subCategories"]}>
        {data.subCategories.length === 0 ? (
          t("category.noSubCategories")
        ) : (
          <>
            {t("category.subCategories")}{" "}
            {data.subCategories.map((sub, index) => (
              <span key={sub.id}>
                {index > 0 && ", "}
                <Link
                  to="/p/$projectId/e/$elementId"
                  params={{ projectId, elementId: sub.id }}
                  className={styles["subLink"]}
                >
                  {sub.name}
                </Link>
              </span>
            ))}
          </>
        )}
      </p>
    </section>
  );
}

function CategoryEditing({
  projectId,
  data,
  draft,
  onChange,
  onDone,
}: {
  readonly projectId: ElementId;
  readonly data: CategoryData;
  readonly draft: Draft;
  readonly onChange: (draft: Draft) => void;
  readonly onDone: () => void;
}) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const categories = useQuery(categoriesQuery(projectId));
  const descriptionId = useId();
  const parentId = useId();
  const [refusal, setRefusal] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const names = useMemo(
    () => new Map((categories.data ?? []).map((c) => [c.id, c.name] as const)),
    [categories.data],
  );

  const chooseParent = (value: string) => {
    const parent = value === "" ? null : (value as ElementId);
    const loop = loopOf(categories.data ?? [], data.element.id, parent);
    if (loop !== null) {
      // F07.R2: the choice is refused, the previous parent stays.
      setRefusal(t("category.loop", { loop: loop.map((id) => names.get(id) ?? id).join(" › ") }));
      return;
    }
    setRefusal(null);
    onChange({ ...draft, parentId: parent });
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      if (draft.description !== data.description || draft.parentId !== (data.parent?.id ?? null)) {
        await updateCategory(projectId, data.element.id, draft.description, draft.parentId);
      }
      for (const [propertyId, value] of draft.values) {
        await setCategoryValue(projectId, data.element.id, propertyId, value);
      }
      for (const propertyId of draft.removed) {
        await setCategoryValue(projectId, data.element.id, propertyId, undefined);
      }
      await client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });
      onDone();
    } catch (failure) {
      setError(errorMessage(t, failure));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form
      className={styles["editing"]}
      aria-label={t("category.editing")}
      onSubmit={(event) => {
        event.preventDefault();
        void save();
      }}
    >
      <div className={styles["cards"]}>
        <section className={styles["card"]} aria-labelledby="category-description">
          <h2 id="category-description" className={styles["cardTitle"]}>
            <label htmlFor={descriptionId}>{t("category.description")}</label>
          </h2>
          <textarea
            id={descriptionId}
            className={styles["textarea"]}
            rows={4}
            value={draft.description}
            onChange={(event) => {
              onChange({ ...draft, description: event.target.value });
            }}
          />
        </section>
        <Inheritance projectId={projectId} data={data}>
          <div className={styles["parentField"]}>
            <label htmlFor={parentId} className={styles["label"]}>
              {t("category.parent")}
            </label>
            <select
              id={parentId}
              className={styles["select"]}
              value={draft.parentId ?? ""}
              aria-describedby={refusal === null ? undefined : `${parentId}-refusal`}
              onChange={(event) => {
                chooseParent(event.target.value);
              }}
            >
              <option value="">{t("category.noParent")}</option>
              {(categories.data ?? [])
                .filter((c) => c.id !== data.element.id)
                .map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
            </select>
            {refusal !== null && (
              <p id={`${parentId}-refusal`} role="alert" className={styles["error"]}>
                {refusal}
              </p>
            )}
          </div>
        </Inheritance>
      </div>
      <PropertyTable projectId={projectId} data={data} draft={draft} onDraft={onChange} />
      {error !== null && (
        <p role="alert" className={styles["error"]}>
          {error}
        </p>
      )}
      <div className={styles["formActions"]}>
        <Button onClick={onDone} disabled={saving}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" variant="primary" disabled={saving}>
          {t("editor.save")}
        </Button>
      </div>
    </form>
  );
}

/** A row of the table: what the server says, overlaid by the draft. */
interface Row {
  readonly definition: Definition;
  readonly item: CategoryPropertyItem | undefined;
  readonly value: PropertyValue | undefined;
  readonly own: boolean;
}

function rowsOf(
  data: CategoryData,
  definitions: readonly Definition[],
  draft: Draft | null,
): Row[] {
  const rows: Row[] = data.properties.map((item) => {
    const definition = toDefinition(item.property);
    if (draft?.removed.has(definition.id) === true) {
      return { definition, item, value: undefined, own: false };
    }
    const edited = draft !== null && draft.values.has(definition.id);
    const value = edited ? draft.values.get(definition.id) : readValue(definition, item.value);
    return { definition, item, value, own: edited || item.origin === "own" };
  });
  for (const [id, value] of draft?.values ?? []) {
    const definition = definitions.find((d) => d.id === id);
    if (definition !== undefined && !rows.some((r) => r.definition.id === id)) {
      rows.push({ definition, item: undefined, value, own: true });
    }
  }
  return rows;
}

function PropertyTable({
  projectId,
  data,
  draft,
  onDraft,
}: {
  readonly projectId: ElementId;
  readonly data: CategoryData;
  readonly draft: Draft | null;
  readonly onDraft: (draft: Draft) => void;
}) {
  const { t } = useTranslation();
  const format = usePropertyFormat(projectId);
  const properties = useQuery({ ...propertiesQuery(projectId), enabled: draft !== null });
  const definitions = useMemo(() => (properties.data ?? []).map(toDefinition), [properties.data]);
  const [adding, setAdding] = useState(false);
  const rows = rowsOf(data, definitions, draft);
  const editing = draft !== null;

  const setValue = (id: ElementId, value: PropertyValue | undefined) => {
    if (draft === null) return;
    const values = new Map(draft.values);
    values.set(id, value);
    const removed = new Set(draft.removed);
    removed.delete(id);
    onDraft({ ...draft, values, removed });
  };

  const remove = (id: ElementId) => {
    if (draft === null) return;
    const values = new Map(draft.values);
    values.delete(id);
    const removed = new Set(draft.removed);
    if (data.properties.some((p) => p.property.id === id && p.origin === "own")) removed.add(id);
    onDraft({ ...draft, values, removed });
  };

  return (
    <section className={styles["section"]} aria-labelledby="category-properties">
      <div className={styles["sectionHead"]}>
        <div>
          <h2 id="category-properties" className={styles["sectionTitle"]}>
            {t("category.properties")}
          </h2>
          <p className={styles["muted"]}>{t("category.propertiesHint")}</p>
        </div>
        {editing && (
          <Button
            onClick={() => {
              setAdding(true);
            }}
          >
            <Plus aria-hidden size={16} />
            {t("category.addProperty")}
          </Button>
        )}
      </div>
      {rows.length === 0 ? (
        <p className={styles["muted"]}>{t("category.noProperties")}</p>
      ) : (
        <div className={styles["tableWrap"]}>
          <table className={styles["table"]}>
            <thead>
              <tr>
                <th scope="col">{t("category.column.property")}</th>
                <th scope="col">{t("category.column.type")}</th>
                <th scope="col">{t("category.column.default")}</th>
                <th scope="col">{t("category.column.rules")}</th>
                <th scope="col">{t("category.column.source")}</th>
                {editing && (
                  <th scope="col">
                    <span className={styles["visuallyHidden"]}>{t("category.column.actions")}</span>
                  </th>
                )}
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => (
                <tr key={row.definition.id}>
                  <th scope="row">
                    <Link
                      to="/p/$projectId/e/$elementId"
                      params={{ projectId, elementId: row.definition.id }}
                      className={styles["propertyLink"]}
                    >
                      <KindBadge kind="propertyDefinition" size="sm" />
                      {row.definition.name}
                    </Link>
                  </th>
                  <td className={styles["mono"]}>{typeName(row.definition)}</td>
                  <td>
                    {editing && row.own ? (
                      <ValueEditor
                        projectId={projectId}
                        definition={row.definition}
                        label={t("category.valueOf", { name: row.definition.name })}
                        value={row.value}
                        onChange={(value) => {
                          setValue(row.definition.id, value);
                        }}
                      />
                    ) : (
                      <>
                        {format.value(row.definition, row.value)}
                        {row.item?.overrides === true && (
                          <span className={styles["override"]}>{t("category.overrides")}</span>
                        )}
                      </>
                    )}
                  </td>
                  <td className={styles["mono"]}>
                    {row.definition.rules.length === 0
                      ? t("property.empty")
                      : row.definition.rules.map((r) => format.rule(row.definition, r)).join(", ")}
                  </td>
                  <td>
                    <span className={row.own ? styles["sourceOwn"] : styles["source"]}>
                      {row.own
                        ? data.element.name
                        : (row.item?.source?.name ?? t("category.definitionDefault"))}
                    </span>
                  </td>
                  {editing && (
                    <td>
                      {row.own ? (
                        <button
                          type="button"
                          className={styles["iconButton"]}
                          aria-label={t("category.removeValue", { name: row.definition.name })}
                          onClick={() => {
                            remove(row.definition.id);
                          }}
                        >
                          <X aria-hidden size={14} />
                        </button>
                      ) : (
                        <Button
                          size="sm"
                          onClick={() => {
                            setValue(row.definition.id, row.value ?? row.definition.default);
                          }}
                        >
                          {t("category.override")}
                        </Button>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {editing && (
        <AddPropertyDialog
          projectId={projectId}
          valueLabel={(name) => t("category.valueOf", { name })}
          open={adding}
          definitions={definitions.filter(
            (d) => !rows.some((r) => r.definition.id === d.id && r.own),
          )}
          onClose={() => {
            setAdding(false);
          }}
          onAdd={(id, value) => {
            setValue(id, value);
            setAdding(false);
          }}
        />
      )}
    </section>
  );
}

/** « Pages de la catégorie · N », with or without the sub-categories (F07.R4, E2E-07-07). */
function CategoryPages({
  projectId,
  data,
}: {
  readonly projectId: ElementId;
  readonly data: CategoryData;
}) {
  const { t } = useTranslation();
  const [includeSub, setIncludeSub] = useState(true);
  const [filter, setFilter] = useState("");
  const pages = useQuery(categoryPagesQuery(projectId, data.element.id, includeSub));
  const count = includeSub ? data.pageCountWithSubCategories : data.pageCount;
  const needle = filter.trim().toLocaleLowerCase();
  const shown = (pages.data ?? []).filter((p) => p.name.toLocaleLowerCase().includes(needle));
  return (
    <section className={styles["section"]} aria-labelledby="category-pages">
      <div className={styles["sectionHead"]}>
        <h2 id="category-pages" className={styles["sectionTitle"]}>
          {t("category.pages")} <span className={styles["count"]}>· {count}</span>
        </h2>
        <div className={styles["pageTools"]}>
          <label className={styles["checkbox"]}>
            <input
              type="checkbox"
              checked={includeSub}
              onChange={(event) => {
                setIncludeSub(event.target.checked);
              }}
            />
            {t("category.includeSubCategories")}
          </label>
          <label className={styles["filter"]}>
            <Search aria-hidden size={14} />
            <input
              type="search"
              placeholder={t("category.filter")}
              aria-label={t("category.filterLabel")}
              value={filter}
              onChange={(event) => {
                setFilter(event.target.value);
              }}
            />
          </label>
        </div>
      </div>
      {pages.isSuccess && shown.length === 0 && (
        <p className={styles["muted"]}>{t("category.noPages")}</p>
      )}
      <ul className={styles["pageGrid"]} aria-label={t("category.pages")}>
        {shown.map((page) => (
          <li key={page.id}>
            <Link
              to="/p/$projectId/e/$elementId"
              params={{ projectId, elementId: page.id }}
              className={styles["pageCard"]}
            >
              <KindBadge kind="page" />
              <span className={styles["pageText"]}>
                <span className={styles["pageName"]}>{page.name}</span>
                <span className={styles["pageVia"]}>{page.via.name}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
      <Link
        to="/p/$projectId/search"
        params={{ projectId }}
        search={toSearchParams({
          ...emptySearch,
          categories: [data.element.id],
          subCategories: includeSub,
        })}
        className={styles["searchLink"]}
      >
        {t("category.seeInSearch", { count })}
      </Link>
    </section>
  );
}
