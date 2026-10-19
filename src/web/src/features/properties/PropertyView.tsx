import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { Pencil, Plus, X } from "lucide-react";
import { useId, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { ApiError } from "../../shared/api/client";
import { errorCode, errorMessage } from "../../shared/api/errors";
import { queryKeys } from "../../shared/api/keys";
import {
  blockingPages,
  propertyQuery,
  renameElement,
  updateProperty,
  type BlockingPage,
  type PropertyView as PropertyData,
} from "../../shared/api/structure";
import type { ElementDetails } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import {
  availableRules,
  evaluateRules,
  parseRule,
  type RuleViolation,
  type ValidationRule,
} from "../../shared/properties/rules";
import { keyFromName, uniqueKey } from "../../shared/properties/keys";
import {
  formatPropertyType,
  scalarTypes,
  type EnumOption,
  type PropertyType,
  type PropertyValue,
  type ScalarType,
} from "../../shared/properties/types";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import { KindBadge } from "../../shared/ui/KindBadge";
import categoryStyles from "../category/CategoryView.module.css";
import { ElementHeader } from "../project/ElementHeader";
import { useCanEdit } from "../project/access";
import { readValue, toDefinition, typeName, type Definition } from "./definitions";
import { rawRule, usePropertyFormat } from "./format";
import { RulesEditor } from "./RulesEditor";
import { draftOfRule, keepApplicable, ruleOf, type RuleDraft } from "./ruleDrafts";
import { ValueEditor } from "./ValueEditor";
import styles from "./PropertyView.module.css";

/** An option of the form: `key` is undefined for an option added in this draft (derived from its label on save). */
interface OptionDraft {
  readonly uid: number;
  readonly key: string | undefined;
  readonly label: string;
}

interface Draft {
  readonly name: string;
  readonly key: string;
  readonly description: string;
  readonly scalar: ScalarType;
  readonly list: boolean;
  readonly defaultValue: PropertyValue | undefined;
  readonly rules: readonly RuleDraft[];
  readonly options: readonly OptionDraft[];
}

let nextOptionUid = 1;

function draftOf(data: PropertyData, definition: Definition): Draft {
  return {
    name: data.element.name,
    key: definition.key,
    description: definition.description,
    scalar: definition.type.scalar,
    list: definition.type.list,
    defaultValue: definition.default,
    rules: definition.rules.map(draftOfRule),
    options: definition.options.map((o) => ({ uid: nextOptionUid++, key: o.key, label: o.label })),
  };
}

function typeOfDraft(draft: Draft): PropertyType {
  return { scalar: draft.scalar, list: draft.list };
}

/**
 * Key of an option within the draft. A new option gets a provisional one, never a valid key, so that the default
 * value and the rules can already name it; it is replaced by the saved key in `savedKeys`.
 */
function draftKey(option: OptionDraft): string {
  return option.key ?? `#${String(option.uid)}`;
}

/** The options of the draft, under their draft keys (the ones the default value and the rules refer to). */
function knownOptions(draft: Draft): EnumOption[] {
  return draft.options.map((o) => ({ key: draftKey(o), label: o.label }));
}

/** Draft key → saved key: a new option gets a key derived from its label, as the backend does for a keyless one. */
function savedKeys(options: readonly OptionDraft[]): ReadonlyMap<string, string> {
  const taken = new Set(options.flatMap((o) => (o.key === undefined ? [] : [o.key])));
  const keys = new Map<string, string>();
  for (const option of options) {
    const key = option.key ?? uniqueKey(keyFromName(option.label.trim()), taken);
    taken.add(key);
    keys.set(draftKey(option), key);
  }
  return keys;
}

function withSavedKeys(value: PropertyValue, keys: ReadonlyMap<string, string>): PropertyValue {
  if (Array.isArray(value)) return (value as readonly string[]).map((k) => keys.get(k) ?? k);
  return typeof value === "string" ? (keys.get(value) ?? value) : value;
}

/** The definition the draft describes, for the editors (default value, tester) and the preview of the rules. */
function definitionOfDraft(base: Definition, draft: Draft): Definition {
  const type = typeOfDraft(draft);
  const options = knownOptions(draft);
  return {
    ...base,
    name: draft.name,
    key: draft.key,
    type,
    options,
    default: draft.defaultValue,
    rules: draft.rules.flatMap((r) => {
      const rule = ruleOf(r, type, options);
      return rule === null ? [] : [rule];
    }),
  };
}

/** What the backend refused when saving, with what the user must decide. */
type Refusal =
  | { readonly kind: "optionInUse"; readonly pages: readonly BlockingPage[] }
  | { readonly kind: "notConvertible"; readonly pages: readonly BlockingPage[] };

/** The property screen (F08, mockup « Propriété — définition et règles »). */
export function PropertyView({
  projectId,
  element,
  edit = false,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
  /** Opens in edit mode (a property just created from the tree). */
  readonly edit?: boolean;
}) {
  const { t } = useTranslation();
  const canEdit = useCanEdit();
  const view = useQuery(propertyQuery(projectId, element.id));
  const titleId = useId();
  const [draft, setDraft] = useState<Draft | null>(null);
  const [wantEdit, setWantEdit] = useState(edit);
  const definition = useMemo(
    () => (view.isSuccess ? toDefinition(view.data.definition) : undefined),
    [view.data, view.isSuccess],
  );
  if (wantEdit && view.isSuccess && definition !== undefined && draft === null) {
    setWantEdit(false);
    setDraft(draftOf(view.data, definition));
  }

  if (view.isError) throw view.error;
  if (view.isPending || definition === undefined) {
    return <p aria-busy="true">{t("common.loading")}</p>;
  }
  const data = view.data;

  return (
    <article
      className={styles["view"]}
      {...(draft === null ? { "aria-labelledby": titleId } : { "aria-label": data.element.name })}
    >
      <ElementHeader
        projectId={projectId}
        element={data.element}
        kind
        titleId={titleId}
        title={
          draft === null ? undefined : (
            <h1 className={styles["heading"]}>
              <input
                className={styles["titleInput"]}
                aria-label={t("property.name")}
                value={draft.name}
                onChange={(event) => {
                  setDraft({ ...draft, name: event.target.value });
                }}
              />
            </h1>
          )
        }
        actions={
          draft === null && canEdit ? (
            <Button
              variant="primary"
              onClick={() => {
                setDraft(draftOf(data, definition));
              }}
            >
              <Pencil aria-hidden size={16} />
              {t("page.edit")}
            </Button>
          ) : undefined
        }
      />
      {draft === null ? (
        <PropertyReading projectId={projectId} definition={definition} />
      ) : (
        <PropertyEditing
          projectId={projectId}
          data={data}
          definition={definition}
          draft={draft}
          onChange={setDraft}
          onDone={() => {
            setDraft(null);
          }}
        />
      )}
      <Usage projectId={projectId} data={data} definition={definition} />
    </article>
  );
}

function PropertyReading({
  projectId,
  definition,
}: {
  readonly projectId: ElementId;
  readonly definition: Definition;
}) {
  const { t } = useTranslation();
  const format = usePropertyFormat(projectId);
  return (
    <>
      <div className={categoryStyles["cards"]}>
        <section className={categoryStyles["card"]} aria-labelledby="property-identity">
          <h2 id="property-identity" className={categoryStyles["cardTitle"]}>
            {t("property.identity")}
          </h2>
          <dl className={styles["facts"]}>
            <dt>{t("property.key")}</dt>
            <dd className={styles["mono"]}>{definition.key}</dd>
            <dt>{t("property.description")}</dt>
            <dd>
              {definition.description === "" ? (
                <span className={categoryStyles["muted"]}>{t("property.noDescription")}</span>
              ) : (
                definition.description
              )}
            </dd>
          </dl>
        </section>
        <section className={categoryStyles["card"]} aria-labelledby="property-type">
          <h2 id="property-type" className={categoryStyles["cardTitle"]}>
            {t("property.type")}
          </h2>
          <dl className={styles["facts"]}>
            <dt>{t("property.type")}</dt>
            <dd className={styles["mono"]}>{typeName(definition)}</dd>
            <dt>{t("property.default")}</dt>
            <dd>{format.value(definition, definition.default)}</dd>
            {definition.type.scalar === "enum" && (
              <>
                <dt>{t("property.options")}</dt>
                <dd>
                  <ul className={styles["chips"]} aria-label={t("property.options")}>
                    {definition.options.map((o) => (
                      <li key={o.key} className={styles["chip"]}>
                        {o.label}
                      </li>
                    ))}
                  </ul>
                </dd>
              </>
            )}
          </dl>
        </section>
      </div>
      <section className={categoryStyles["section"]} aria-labelledby="property-rules">
        <h2 id="property-rules" className={categoryStyles["sectionTitle"]}>
          {t("property.rules")}
        </h2>
        {definition.rules.length === 0 ? (
          <p className={categoryStyles["muted"]}>
            {availableRules(definition.type).length === 0
              ? t("property.noRulesBool")
              : t("property.noRules")}
          </p>
        ) : (
          <ul className={styles["chips"]} aria-label={t("property.rules")}>
            {definition.rules.map((rule) => (
              <li key={rawRule(rule)} className={styles["chip"]}>
                {format.rule(definition, rule)}
              </li>
            ))}
          </ul>
        )}
      </section>
      <RuleTester projectId={projectId} definition={definition} />
    </>
  );
}

/** « Tester une valeur »: the rules of the definition applied to a value typed here, instantly (E2E-08-04). */
function RuleTester({
  projectId,
  definition,
}: {
  readonly projectId: ElementId;
  readonly definition: Definition;
}) {
  const { t } = useTranslation();
  const [value, setValue] = useState<PropertyValue | undefined>(undefined);
  const labelId = useId();
  const refs = definition.type.scalar === "ref";
  const failures: RuleViolation[] =
    value === undefined ? [] : evaluateRules(definition.type, definition.rules, value);
  return (
    <section className={categoryStyles["section"]} aria-labelledby={labelId}>
      <h2 id={labelId} className={categoryStyles["sectionTitle"]}>
        {t("property.test.title")}
      </h2>
      {refs ? (
        <p className={categoryStyles["muted"]}>{t("property.test.ref")}</p>
      ) : (
        <div className={styles["tester"]}>
          <ValueEditor
            projectId={projectId}
            definition={definition}
            label={t("property.test.label")}
            value={value}
            onChange={setValue}
          />
          <p role="status" aria-label={t("property.test.result")} className={styles["testResult"]}>
            {value === undefined
              ? t("property.test.empty")
              : failures.length === 0
                ? t("property.test.valid")
                : t("property.test.fails", {
                    rules: failures.map((f) => rawRule(f.rule)).join(", "),
                  })}
          </p>
        </div>
      )}
    </section>
  );
}

/** « Utilisée par », « Renseignée sur N pages », « N pages hors règles » (E2E-08-01). */
function Usage({
  projectId,
  data,
  definition,
}: {
  readonly projectId: ElementId;
  readonly data: PropertyData;
  readonly definition: Definition;
}) {
  const { t } = useTranslation();
  const format = usePropertyFormat(projectId);
  return (
    <section className={categoryStyles["section"]} aria-labelledby="property-usage">
      <h2 id="property-usage" className={categoryStyles["sectionTitle"]}>
        {t("property.usage")}
      </h2>
      <div className={categoryStyles["cards"]}>
        <div className={categoryStyles["card"]}>
          <h3 className={categoryStyles["cardTitle"]}>{t("property.usedBy")}</h3>
          {data.usedBy.length === 0 ? (
            <p className={categoryStyles["muted"]}>{t("property.notUsed")}</p>
          ) : (
            <ul className={styles["chips"]} aria-label={t("property.usedBy")}>
              {data.usedBy.map((category) => (
                <li key={category.id}>
                  <Link
                    to="/p/$projectId/e/$elementId"
                    params={{ projectId, elementId: category.id }}
                    className={styles["chipLink"]}
                  >
                    <KindBadge kind="category" size="sm" />
                    {category.name}
                  </Link>
                </li>
              ))}
            </ul>
          )}
          <p className={styles["filled"]}>{t("property.filled", { count: data.filledCount })}</p>
        </div>
        <div className={categoryStyles["card"]}>
          <h3 className={categoryStyles["cardTitle"]}>
            {t("property.outOfRules", { count: data.violations.length })}
          </h3>
          {data.violations.length === 0 ? (
            <p className={categoryStyles["muted"]}>{t("property.allRespect")}</p>
          ) : (
            <ul className={styles["violations"]} aria-label={t("property.outOfRulesList")}>
              {data.violations.map((page) => (
                <li key={page.pageId}>
                  <Link
                    to="/p/$projectId/e/$elementId"
                    params={{ projectId, elementId: page.pageId }}
                    className={styles["pageLink"]}
                  >
                    {page.pageName}
                  </Link>{" "}
                  <span className={styles["badValue"]}>
                    ({format.value(definition, readValue(definition, page.value))})
                  </span>
                  <span className={styles["messages"]}>
                    {page.violations
                      .flatMap((v) => {
                        const rule = parseRule(v.rule, definition.type, definition.options);
                        return rule === null
                          ? []
                          : [
                              format.violation(definition, {
                                rule,
                                ...(typeof v.index === "number" ? { index: v.index } : {}),
                              }),
                            ];
                      })
                      .join(" · ")}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </section>
  );
}

function PropertyEditing({
  projectId,
  data,
  definition,
  draft,
  onChange,
  onDone,
}: {
  readonly projectId: ElementId;
  readonly data: PropertyData;
  readonly definition: Definition;
  readonly draft: Draft;
  readonly onChange: (draft: Draft) => void;
  readonly onDone: () => void;
}) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const keyId = useId();
  const descriptionId = useId();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [refusal, setRefusal] = useState<Refusal | null>(null);

  const type = typeOfDraft(draft);
  const options = knownOptions(draft);
  const current = definitionOfDraft(definition, draft);
  const incomplete = draft.rules.some((r) => ruleOf(r, type, options) === null);
  const unnamedOption = draft.scalar === "enum" && draft.options.some((o) => o.label.trim() === "");
  const removed = definition.options.filter((o) => !draft.options.some((d) => d.key === o.key));

  const changeType = (scalar: ScalarType, list: boolean) => {
    const next: PropertyType = { scalar, list };
    const available = availableRules(next);
    onChange({
      ...draft,
      scalar,
      list,
      rules: keepApplicable(draft.rules, available),
      // The default is kept only if it is still a value of the new type.
      defaultValue: undefined,
    });
  };

  const body = (replacements?: Record<string, string>) => {
    const enumKeys = draft.scalar === "enum" ? savedKeys(draft.options) : new Map<string, string>();
    const rules: ValidationRule[] = draft.rules.flatMap((r) => {
      const rule = ruleOf(r, type, options);
      if (rule === null) return [];
      return [
        rule.op === "in" ? { ...rule, value: rule.value.map((k) => enumKeys.get(k) ?? k) } : rule,
      ];
    });
    return {
      key: draft.key.trim(),
      description: draft.description,
      type: formatPropertyType(type),
      default:
        draft.defaultValue === undefined ? null : withSavedKeys(draft.defaultValue, enumKeys),
      rules,
      options:
        draft.scalar === "enum"
          ? draft.options.map((o) => ({
              key: enumKeys.get(draftKey(o)) ?? null,
              label: o.label.trim(),
            }))
          : [],
      ...(replacements === undefined ? {} : { replacements }),
    };
  };

  const save = async (replacements?: Record<string, string>) => {
    setSaving(true);
    setError(null);
    try {
      await updateProperty(projectId, data.element.id, body(replacements));
      const name = draft.name.trim();
      if (name !== "" && name !== data.element.name) {
        await renameElement(projectId, data.element.id, name);
      }
      await client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });
      setRefusal(null);
      onDone();
    } catch (failure) {
      const code = errorCode(failure);
      const pages = failure instanceof ApiError ? blockingPages(failure.problem) : [];
      if (code === "property.option.inUse") {
        setRefusal({ kind: "optionInUse", pages });
      } else if (code === "property.value.notConvertible") {
        setRefusal({ kind: "notConvertible", pages });
      } else {
        setError(errorMessage(t, failure));
      }
    } finally {
      setSaving(false);
    }
  };

  return (
    <form
      className={styles["editing"]}
      aria-label={t("property.editing")}
      onSubmit={(event) => {
        event.preventDefault();
        void save();
      }}
    >
      <div className={categoryStyles["cards"]}>
        <section className={categoryStyles["card"]} aria-labelledby="property-identity">
          <h2 id="property-identity" className={categoryStyles["cardTitle"]}>
            {t("property.identity")}
          </h2>
          <label htmlFor={keyId} className={categoryStyles["label"]}>
            {t("property.key")}
          </label>
          <input
            id={keyId}
            className={`${categoryStyles["select"] ?? ""} ${styles["mono"] ?? ""}`}
            value={draft.key}
            onChange={(event) => {
              onChange({ ...draft, key: event.target.value });
            }}
          />
          <p className={categoryStyles["muted"]}>{t("property.keyHint")}</p>
          <label htmlFor={descriptionId} className={categoryStyles["label"]}>
            {t("property.description")}
          </label>
          <textarea
            id={descriptionId}
            className={categoryStyles["textarea"]}
            rows={3}
            value={draft.description}
            onChange={(event) => {
              onChange({ ...draft, description: event.target.value });
            }}
          />
        </section>
        <section className={categoryStyles["card"]} aria-labelledby="property-type">
          <h2 id="property-type" className={categoryStyles["cardTitle"]}>
            {t("property.type")}
          </h2>
          <TypePicker draft={draft} onChange={changeType} />
          {draft.scalar === "enum" && <OptionsEditor draft={draft} onChange={onChange} />}
          <div className={styles["defaultField"]}>
            <span className={categoryStyles["label"]}>{t("property.default")}</span>
            <ValueEditor
              projectId={projectId}
              definition={current}
              label={t("property.defaultLabel")}
              value={draft.defaultValue}
              onChange={(value) => {
                onChange({ ...draft, defaultValue: value });
              }}
            />
          </div>
        </section>
      </div>
      <section className={categoryStyles["section"]} aria-labelledby="property-rules">
        <h2 id="property-rules" className={categoryStyles["sectionTitle"]}>
          {t("property.rules")}
        </h2>
        <RulesEditor
          projectId={projectId}
          type={type}
          options={options}
          rules={draft.rules}
          onChange={(rules) => {
            onChange({ ...draft, rules });
          }}
        />
        {incomplete && (
          <p role="alert" className={categoryStyles["error"]}>
            {t("property.rulesIncomplete")}
          </p>
        )}
      </section>
      <RuleTester projectId={projectId} definition={current} />
      {error !== null && (
        <p role="alert" className={categoryStyles["error"]}>
          {error}
        </p>
      )}
      <div className={categoryStyles["formActions"]}>
        <Button onClick={onDone} disabled={saving}>
          {t("common.cancel")}
        </Button>
        <Button
          type="submit"
          variant="primary"
          disabled={saving || incomplete || unnamedOption || draft.name.trim() === ""}
        >
          {t("editor.save")}
        </Button>
      </div>
      <RefusalDialog
        refusal={refusal}
        removed={removed}
        remaining={draft.options.flatMap((o) => (o.key === undefined ? [] : [o]))}
        onClose={() => {
          setRefusal(null);
        }}
        onReplace={(replacements) => {
          void save(replacements);
        }}
      />
    </form>
  );
}

function TypePicker({
  draft,
  onChange,
}: {
  readonly draft: Draft;
  readonly onChange: (scalar: ScalarType, list: boolean) => void;
}) {
  const { t } = useTranslation();
  const listId = useId();
  return (
    <div>
      <div role="radiogroup" aria-label={t("property.type")} className={styles["typeCards"]}>
        {scalarTypes.map((scalar) => (
          <label
            key={scalar}
            className={`${styles["typeCard"] ?? ""} ${scalar === draft.scalar ? (styles["typeCardOn"] ?? "") : ""}`}
          >
            <input
              type="radio"
              name="property-type"
              className={styles["typeRadio"]}
              checked={scalar === draft.scalar}
              onChange={() => {
                onChange(scalar, draft.list);
              }}
            />
            <span className={styles["typeName"]}>{scalar}</span>
            <span className={styles["typeHint"]}>{t(`property.typeLabel.${scalar}`)}</span>
          </label>
        ))}
      </div>
      <label htmlFor={listId} className={categoryStyles["checkbox"]}>
        <input
          id={listId}
          type="checkbox"
          checked={draft.list}
          onChange={(event) => {
            onChange(draft.scalar, event.target.checked);
          }}
        />
        {t("property.typeList")}
      </label>
    </div>
  );
}

function OptionsEditor({
  draft,
  onChange,
}: {
  readonly draft: Draft;
  readonly onChange: (draft: Draft) => void;
}) {
  const { t } = useTranslation();
  const update = (options: readonly OptionDraft[]) => {
    const keys = new Set(options.map(draftKey));
    onChange({
      ...draft,
      options,
      // A rule « parmi » cannot name an option that no longer exists.
      rules: draft.rules.map((r) =>
        r.op === "in" ? { ...r, keys: r.keys.filter((k) => keys.has(k)) } : r,
      ),
      defaultValue: stillValid(draft.defaultValue, keys),
    });
  };
  return (
    <div className={styles["options"]}>
      <span className={categoryStyles["label"]}>{t("property.options")}</span>
      <ul className={styles["optionList"]} aria-label={t("property.options")}>
        {draft.options.map((option, index) => (
          <li key={option.uid} className={styles["optionRow"]}>
            <input
              className={styles["optionInput"]}
              aria-label={t("property.option", { index: index + 1 })}
              value={option.label}
              onChange={(event) => {
                update(
                  draft.options.map((o) =>
                    o === option ? { ...o, label: event.target.value } : o,
                  ),
                );
              }}
            />
            <button
              type="button"
              className={categoryStyles["iconButton"]}
              aria-label={t("property.removeOption", { label: option.label })}
              onClick={() => {
                update(draft.options.filter((o) => o !== option));
              }}
            >
              <X aria-hidden size={14} />
            </button>
          </li>
        ))}
      </ul>
      <Button
        size="sm"
        onClick={() => {
          update([...draft.options, { uid: nextOptionUid++, key: undefined, label: "" }]);
        }}
      >
        <Plus aria-hidden size={14} />
        {t("property.addOption")}
      </Button>
    </div>
  );
}

/** A default value naming a removed option is dropped. */
function stillValid(value: PropertyValue | undefined, keys: ReadonlySet<string>) {
  if (value === undefined) return undefined;
  if (Array.isArray(value))
    return (value as readonly string[]).every((k) => keys.has(k)) ? value : undefined;
  return typeof value === "string" && keys.has(value) ? value : undefined;
}

/**
 * The backend refused the save and the user must decide: a replacement option for the pages still using a removed
 * option (F08.R5, E2E-08-09), or nothing but the list of values that block a change of type (F08.R6, E2E-08-10).
 */
function RefusalDialog({
  refusal,
  removed,
  remaining,
  onClose,
  onReplace,
}: {
  readonly refusal: Refusal | null;
  readonly removed: readonly EnumOption[];
  readonly remaining: readonly OptionDraft[];
  readonly onClose: () => void;
  readonly onReplace: (replacements: Record<string, string>) => void;
}) {
  const { t } = useTranslation();
  const [choice, setChoice] = useState<Record<string, string>>({});
  const kind = refusal?.kind;
  const complete = removed.every((o) => (choice[o.key] ?? "") !== "");
  return (
    <Dialog
      open={refusal !== null}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
      alert
      title={kind === "optionInUse" ? t("property.replace.title") : t("property.blocked.title")}
      description={
        kind === "optionInUse" ? t("property.replace.explain") : t("property.blocked.explain")
      }
      footer={
        kind === "optionInUse" ? (
          <>
            <Button onClick={onClose}>{t("common.cancel")}</Button>
            <Button
              variant="primary"
              disabled={!complete}
              onClick={() => {
                onReplace(
                  Object.fromEntries(removed.map((o) => [o.key, choice[o.key] ?? ""] as const)),
                );
              }}
            >
              {t("property.replace.confirm")}
            </Button>
          </>
        ) : (
          <Button onClick={onClose}>{t("common.close")}</Button>
        )
      }
    >
      <div className={styles["dialogBody"]}>
        {kind === "optionInUse" &&
          removed.map((option) => (
            <label key={option.key} className={styles["replaceRow"]}>
              <span>{t("property.replace.for", { label: option.label })}</span>
              <select
                className={categoryStyles["select"]}
                value={choice[option.key] ?? ""}
                onChange={(event) => {
                  setChoice({ ...choice, [option.key]: event.target.value });
                }}
              >
                <option value="">{t("property.empty")}</option>
                {remaining.map((o) => (
                  <option key={o.uid} value={o.key}>
                    {o.label}
                  </option>
                ))}
              </select>
            </label>
          ))}
        <ul className={styles["blockingList"]} aria-label={t("property.blocked.pages")}>
          {refusal?.pages.map((page) => (
            <li key={page.id}>
              <strong>{page.name}</strong>
              {page.value !== "" && <span className={styles["badValue"]}> ({page.value})</span>}
            </li>
          ))}
        </ul>
      </div>
    </Dialog>
  );
}
