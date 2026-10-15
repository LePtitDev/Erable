import { useQuery } from "@tanstack/react-query";
import { Plus, X } from "lucide-react";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { categoryPagesQuery } from "../../shared/api/structure";
import { treeQuery } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import type { PropertyValue, ScalarType, ScalarValue } from "../../shared/properties/types";
import type { Definition } from "./definitions";
import styles from "./ValueEditor.module.css";

interface EditorProps {
  readonly projectId: ElementId;
  readonly definition: Definition;
  /** Accessible name of the control (the property name, usually). */
  readonly label: string;
  /** For a `<label htmlFor>` outside. */
  readonly id?: string;
  readonly invalid?: boolean;
  readonly describedBy?: string;
}

interface ValueEditorProps extends EditorProps {
  readonly value: PropertyValue | undefined;
  /** `undefined` when the field is emptied (no value of its own). */
  readonly onChange: (value: PropertyValue | undefined) => void;
}

/**
 * Edits a value of any property type (E2E-08-06): switch for `bool`, list for `enum`, filtered numeric field for
 * `int`/`float` (letters cannot be typed), page picker filtered by the `in_category` rule for `ref`, date field,
 * text fields, and a list of item editors for `list<T>`.
 */
export function ValueEditor(props: ValueEditorProps) {
  const { definition, value, onChange } = props;
  if (!definition.type.list) {
    return <ScalarEditor {...props} value={value as ScalarValue | undefined} onChange={onChange} />;
  }
  return <ListEditor {...props} value={(value as readonly ScalarValue[] | undefined) ?? []} />;
}

function ListEditor({
  value,
  onChange,
  id,
  ...props
}: EditorProps & {
  readonly value: readonly ScalarValue[];
  readonly onChange: (value: PropertyValue | undefined) => void;
}) {
  const { t } = useTranslation();
  const { definition, label } = props;
  return (
    <div className={styles["list"]} role="group" aria-label={label} id={id}>
      {value.map((item, index) => (
        // Items have no identity of their own: their position is their key.
        <div className={styles["listItem"]} key={index}>
          <ScalarEditor
            {...props}
            label={t("property.listItem", { name: label, index: index + 1 })}
            value={item}
            onChange={(v) => {
              const next = [...value];
              if (v === undefined) next.splice(index, 1);
              else next[index] = v;
              onChange(next);
            }}
          />
          <button
            type="button"
            className={styles["remove"]}
            aria-label={t("property.removeItem", { index: index + 1 })}
            onClick={() => {
              onChange(value.filter((_, i) => i !== index));
            }}
          >
            <X aria-hidden size={14} />
          </button>
        </div>
      ))}
      <button
        type="button"
        className={styles["addItem"]}
        onClick={() => {
          onChange([...value, defaultItem(definition)]);
        }}
      >
        <Plus aria-hidden size={14} />
        {t("property.addItem")}
      </button>
    </div>
  );
}

function defaultItem(definition: Definition): ScalarValue {
  const defaults: Record<ScalarType, ScalarValue> = {
    bool: false,
    int: 0,
    float: 0,
    string: "",
    text: "",
    enum: definition.options[0]?.key ?? "",
    date: new Date().toISOString().slice(0, 10),
    ref: "",
  };
  return defaults[definition.type.scalar];
}

const integerPattern = /^-?\d*$/;
const decimalPattern = /^-?\d*([.,]\d*)?$/;

function ScalarEditor({
  projectId,
  definition,
  label,
  id,
  invalid = false,
  describedBy,
  value,
  onChange,
}: EditorProps & {
  readonly value: ScalarValue | undefined;
  readonly onChange: (value: ScalarValue | undefined) => void;
}) {
  const { t } = useTranslation();
  const common = {
    id,
    "aria-invalid": invalid || undefined,
    "aria-describedby": describedBy,
    className: styles["input"],
  };
  switch (definition.type.scalar) {
    case "bool":
      return (
        <button
          type="button"
          role="switch"
          aria-checked={value === true}
          aria-label={label}
          aria-describedby={describedBy}
          id={id}
          className={styles["switch"]}
          onClick={() => {
            onChange(value !== true);
          }}
        >
          <span className={styles["track"]} aria-hidden>
            <span className={styles["thumb"]} />
          </span>
          <span>{value === true ? t("property.yes") : t("property.no")}</span>
        </button>
      );
    case "int":
    case "float":
      return (
        <NumberInput
          {...common}
          label={label}
          integer={definition.type.scalar === "int"}
          value={typeof value === "number" ? value : undefined}
          onChange={onChange}
        />
      );
    case "string":
      return (
        <input
          {...common}
          type="text"
          aria-label={label}
          value={typeof value === "string" ? value : ""}
          onChange={(event) => {
            onChange(event.target.value);
          }}
        />
      );
    case "text":
      return (
        <textarea
          {...common}
          className={styles["textarea"]}
          aria-label={label}
          rows={3}
          value={typeof value === "string" ? value : ""}
          onChange={(event) => {
            onChange(event.target.value);
          }}
        />
      );
    case "enum":
      return (
        <select
          {...common}
          aria-label={label}
          value={typeof value === "string" ? value : ""}
          onChange={(event) => {
            onChange(event.target.value === "" ? undefined : event.target.value);
          }}
        >
          <option value="">{t("property.empty")}</option>
          {definition.options.map((option) => (
            <option key={option.key} value={option.key}>
              {option.label}
            </option>
          ))}
        </select>
      );
    case "date":
      return (
        <input
          {...common}
          type="date"
          aria-label={label}
          value={typeof value === "string" ? value : ""}
          onChange={(event) => {
            onChange(event.target.value === "" ? undefined : event.target.value);
          }}
        />
      );
    case "ref":
      return (
        <RefPicker
          {...common}
          projectId={projectId}
          definition={definition}
          label={label}
          value={typeof value === "string" ? (value as ElementId) : undefined}
          onChange={onChange}
        />
      );
  }
}

/**
 * A number typed as text: only digits, one sign and (for floats) one decimal separator get in, so letters are
 * refused as they are typed (E2E-08-06). Both « 1,5 » and « 1.5 » are accepted.
 */
function NumberInput({
  label,
  integer,
  value,
  onChange,
  ...rest
}: {
  readonly label: string;
  readonly integer: boolean;
  readonly value: number | undefined;
  readonly onChange: (value: number | undefined) => void;
  readonly id: string | undefined;
  readonly className: string | undefined;
  readonly "aria-invalid": true | undefined;
  readonly "aria-describedby": string | undefined;
}) {
  const [text, setText] = useState(value === undefined ? "" : String(value));
  const [shown, setShown] = useState(value);
  // The value changed from outside (reset, inherited value restored): show it.
  if (shown !== value) {
    setShown(value);
    if (parse(text, integer) !== value) setText(value === undefined ? "" : String(value));
  }
  return (
    <input
      {...rest}
      type="text"
      inputMode={integer ? "numeric" : "decimal"}
      aria-label={label}
      value={text}
      onChange={(event) => {
        const next = event.target.value.trim();
        if (!(integer ? integerPattern : decimalPattern).test(next)) return;
        setText(next);
        const parsed = parse(next, integer);
        setShown(parsed);
        onChange(parsed);
      }}
    />
  );
}

function parse(text: string, integer: boolean): number | undefined {
  if (text === "" || text === "-") return undefined;
  const number = Number(text.replace(",", "."));
  if (!Number.isFinite(number) || (integer && !Number.isSafeInteger(number))) return undefined;
  return number;
}

/** Pages (or categories, for a `kind category` rule), filtered by the `in_category` rule of the definition. */
function RefPicker({
  projectId,
  definition,
  label,
  value,
  onChange,
  ...rest
}: {
  readonly projectId: ElementId;
  readonly definition: Definition;
  readonly label: string;
  readonly value: ElementId | undefined;
  readonly onChange: (value: ElementId | undefined) => void;
  readonly id: string | undefined;
  readonly className: string | undefined;
  readonly "aria-invalid": true | undefined;
  readonly "aria-describedby": string | undefined;
}) {
  const { t } = useTranslation();
  const tree = useQuery(treeQuery(projectId));
  const inCategory = definition.rules.find((r) => r.op === "in_category");
  const kind = definition.rules.find((r) => r.op === "kind")?.value ?? "page";
  const members = useQuery({
    ...categoryPagesQuery(projectId, inCategory?.value ?? ("" as ElementId), true),
    enabled: inCategory !== undefined,
  });
  const candidates = useMemo(() => {
    const nodes = (tree.data?.nodes ?? []).filter((n) => n.kind === kind);
    const allowed =
      inCategory === undefined ? null : new Set((members.data ?? []).map((m) => m.id));
    return nodes
      .filter((n) => allowed === null || allowed.has(n.id))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [tree.data, kind, inCategory, members.data]);
  const current = value === undefined ? undefined : tree.data?.nodes.find((n) => n.id === value);
  return (
    <select
      {...rest}
      aria-label={label}
      value={value ?? ""}
      onChange={(event) => {
        onChange(event.target.value === "" ? undefined : (event.target.value as ElementId));
      }}
    >
      <option value="">{t("property.empty")}</option>
      {current !== undefined && !candidates.includes(current) && (
        <option value={current.id}>{current.name}</option>
      )}
      {candidates.map((node) => (
        <option key={node.id} value={node.id}>
          {node.name}
        </option>
      ))}
    </select>
  );
}
