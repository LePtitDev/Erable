import { useQuery } from "@tanstack/react-query";
import { Plus, RotateCcw, X } from "lucide-react";
import { Tabs } from "radix-ui";
import { useId, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { AttachmentItem } from "../../shared/api/pages";
import { categoriesQuery, propertiesQuery } from "../../shared/api/structure";
import type { ElementId } from "../../shared/ids/elementId";
import type { PropertyValue } from "../../shared/properties/types";
import { Button } from "../../shared/ui/Button";
import { AddPropertyDialog } from "../properties/AddPropertyDialog";
import { toDefinition } from "../properties/definitions";
import { useConflictText, usePropertyFormat } from "../properties/format";
import {
  editedProperty,
  type EditedProperty,
  type Fallback,
  type PageProperty,
  type StructureDraft,
} from "../properties/pageProperties";
import { ValueEditor } from "../properties/ValueEditor";
import { PageLinksPanel } from "../links/PageLinksPanel";
import { AttachmentsPanel } from "./AttachmentsPanel";
import styles from "./PageStructurePanel.module.css";

const tabs = ["properties", "categories", "links", "files"] as const;

interface PanelProps {
  readonly projectId: ElementId;
  readonly pageId: ElementId;
  /** The properties as the backend resolves the draft (or the saved page while nothing changed). */
  readonly properties: readonly PageProperty[];
  readonly draft: StructureDraft;
  readonly onDraft: (draft: StructureDraft) => void;
  readonly attachments: readonly AttachmentItem[];
  /** Hidden but kept (the narrow editor shows the text or the panel, F18.R6). */
  readonly hidden?: boolean;
}

/** What a property would be without the value the page defines, whatever the preview already knows. */
function fallbackOf(row: EditedProperty): Fallback | null {
  if (row.own !== undefined) return row.fallback;
  if (row.origin === "own") return null;
  return {
    value: row.value,
    origin: row.origin,
    sources: row.sources,
    candidates: row.candidates,
  };
}

/** Side panel of the editor (mockup « Page — édition »): properties, categories, files. */
export function PageStructurePanel(props: PanelProps) {
  const { t } = useTranslation();
  return (
    <Tabs.Root defaultValue={tabs[0]} className={styles["panel"]} hidden={props.hidden}>
      <Tabs.List className={styles["tabs"]} aria-label={t("page.tabs.label")}>
        {tabs.map((tab) => (
          <Tabs.Trigger key={tab} value={tab} className={styles["tab"]}>
            {t(`page.tabs.${tab}`)}
          </Tabs.Trigger>
        ))}
      </Tabs.List>
      <Tabs.Content value="properties" className={styles["content"]}>
        <PropertiesTab {...props} />
      </Tabs.Content>
      <Tabs.Content value="categories" className={styles["content"]}>
        <CategoriesTab {...props} />
      </Tabs.Content>
      <Tabs.Content value="links" className={styles["content"]}>
        <PageLinksPanel projectId={props.projectId} pageId={props.pageId} />
      </Tabs.Content>
      <Tabs.Content value="files" className={styles["content"]}>
        <AttachmentsPanel
          projectId={props.projectId}
          pageId={props.pageId}
          attachments={props.attachments}
        />
      </Tabs.Content>
    </Tabs.Root>
  );
}

function PropertiesTab({ projectId, properties, draft, onDraft }: PanelProps) {
  const { t } = useTranslation();
  const [adding, setAdding] = useState(false);
  const all = useQuery(propertiesQuery(projectId));
  const offered = useMemo(
    () => (all.data ?? []).map(toDefinition).filter((d) => !draft.values.has(d.id)),
    [all.data, draft.values],
  );
  const rows = properties.map((p) => editedProperty(p, draft));

  const setValue = (id: ElementId, value: PropertyValue | undefined) => {
    onDraft({ ...draft, values: new Map(draft.values).set(id, value) });
  };
  const unsetValue = (id: ElementId) => {
    const values = new Map(draft.values);
    values.delete(id);
    onDraft({ ...draft, values });
  };

  return (
    <div className={styles["tabBody"]}>
      {rows.length === 0 && <p className={styles["muted"]}>{t("page.noProperties")}</p>}
      <ul className={styles["fields"]} aria-label={t("page.propertiesTitle")}>
        {rows.map((row) => (
          <li key={row.definition.id}>
            <PropertyField
              projectId={projectId}
              row={row}
              onValue={(value) => {
                setValue(row.definition.id, value);
              }}
              onUnset={() => {
                unsetValue(row.definition.id);
              }}
            />
          </li>
        ))}
      </ul>
      <Button
        onClick={() => {
          setAdding(true);
        }}
      >
        <Plus aria-hidden size={16} />
        {t("page.addProperty")}
      </Button>
      <AddPropertyDialog
        projectId={projectId}
        open={adding}
        definitions={offered}
        valueLabel={(name) => t("property.valueOf", { name })}
        onClose={() => {
          setAdding(false);
        }}
        onAdd={(id, value) => {
          setValue(id, value);
          setAdding(false);
        }}
      />
    </div>
  );
}

function PropertyField({
  projectId,
  row,
  onValue,
  onUnset,
}: {
  readonly projectId: ElementId;
  readonly row: EditedProperty;
  readonly onValue: (value: PropertyValue | undefined) => void;
  /** The page stops defining the property: it falls back to its categories or its default. */
  readonly onUnset: () => void;
}) {
  const { t } = useTranslation();
  const format = usePropertyFormat(projectId);
  const conflictText = useConflictText(projectId);
  const messageId = useId();
  const { definition } = row;
  const fallback = fallbackOf(row);
  const own = row.touched;
  // While the preview of a removed value is on its way, show what the page falls back to.
  const shown = own ? row.draftOwn : row.own !== undefined ? fallback?.value : row.value;
  const source = own ? null : row.own !== undefined ? fallback : row;
  const conflict = !own && source?.origin === "conflict";
  const messages = row.shownViolations.map((v) => format.violation(definition, v));

  return (
    <div className={styles["field"]} role="group" aria-label={definition.name}>
      <div className={styles["fieldHead"]}>
        <span className={styles["fieldName"]}>{definition.name}</span>
        <span className={styles["fieldType"]}>
          {definition.type.list ? `list<${definition.type.scalar}>` : definition.type.scalar}
        </span>
        {own && fallback === null && (
          <button
            type="button"
            className={styles["iconButton"]}
            aria-label={t("page.removeValue", { name: definition.name })}
            onClick={onUnset}
          >
            <X aria-hidden size={14} />
          </button>
        )}
      </div>
      {conflict ? (
        <>
          <p className={styles["conflict"]}>{conflictText(definition, source.candidates)}</p>
          <div
            role="group"
            aria-label={t("page.resolveConflict", { name: definition.name })}
            className={styles["choices"]}
          >
            {distinctValues(source.candidates.map((c) => c.value)).map((value) => (
              <Button
                key={JSON.stringify(value)}
                size="sm"
                onClick={() => {
                  onValue(value);
                }}
              >
                {format.value(definition, value)}
              </Button>
            ))}
          </div>
        </>
      ) : (
        <ValueEditor
          projectId={projectId}
          definition={definition}
          label={definition.name}
          value={shown}
          invalid={messages.length > 0}
          {...(messages.length > 0 ? { describedBy: messageId } : {})}
          onChange={onValue}
        />
      )}
      {messages.length > 0 && (
        <div id={messageId} className={styles["violations"]}>
          {messages.map((message) => (
            <p key={message}>{message}</p>
          ))}
        </div>
      )}
      {!own && !conflict && source !== null && (
        <p className={styles["caption"]}>
          {source.origin === "inherited" && source.sources.length > 0
            ? t("page.inheritedFrom", { categories: source.sources.map((s) => s.name).join(", ") })
            : source.origin === "default"
              ? t("page.fromDefault")
              : ""}
        </p>
      )}
      {own && fallback !== null && (
        <button type="button" className={styles["restore"]} onClick={onUnset}>
          <RotateCcw aria-hidden size={12} />
          {t("page.restoreInherited")}
        </button>
      )}
    </div>
  );
}

function distinctValues(values: readonly (PropertyValue | undefined)[]): PropertyValue[] {
  const seen = new Set<string>();
  const result: PropertyValue[] = [];
  for (const value of values) {
    if (value === undefined) continue;
    const key = JSON.stringify(value);
    if (!seen.has(key)) {
      seen.add(key);
      result.push(value);
    }
  }
  return result;
}

function CategoriesTab({ projectId, draft, onDraft }: PanelProps) {
  const { t } = useTranslation();
  const categories = useQuery(categoriesQuery(projectId));
  const selectId = useId();
  const [choice, setChoice] = useState("");
  const names = new Map((categories.data ?? []).map((c) => [c.id, c.name] as const));
  const available = (categories.data ?? []).filter((c) => !draft.categories.includes(c.id));

  return (
    <div className={styles["tabBody"]}>
      {draft.categories.length === 0 ? (
        <p className={styles["muted"]}>{t("page.noCategories")}</p>
      ) : (
        <ul className={styles["categories"]} aria-label={t("page.categoriesTitle")}>
          {draft.categories.map((id) => (
            <li key={id} className={styles["category"]}>
              <span>{names.get(id) ?? id}</span>
              <button
                type="button"
                className={styles["iconButton"]}
                aria-label={t("page.removeCategory", { name: names.get(id) ?? id })}
                onClick={() => {
                  onDraft({ ...draft, categories: draft.categories.filter((c) => c !== id) });
                }}
              >
                <X aria-hidden size={14} />
              </button>
            </li>
          ))}
        </ul>
      )}
      <label htmlFor={selectId} className={styles["fieldName"]}>
        {t("page.addCategory")}
      </label>
      <div className={styles["addRow"]}>
        <select
          id={selectId}
          className={styles["select"]}
          value={choice}
          onChange={(event) => {
            setChoice(event.target.value);
          }}
        >
          <option value="">{t("page.chooseCategory")}</option>
          {available.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        <Button
          disabled={choice === ""}
          onClick={() => {
            onDraft({ ...draft, categories: [...draft.categories, choice as ElementId] });
            setChoice("");
          }}
        >
          {t("page.addCategoryButton")}
        </Button>
      </div>
    </div>
  );
}
