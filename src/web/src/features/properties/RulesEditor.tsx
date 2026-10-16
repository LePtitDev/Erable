import { useQuery } from "@tanstack/react-query";
import { Plus, X } from "lucide-react";
import { useId } from "react";
import { useTranslation } from "react-i18next";
import { categoriesQuery } from "../../shared/api/structure";
import type { ElementId } from "../../shared/ids/elementId";
import { availableRules, type RuleOp } from "../../shared/properties/rules";
import { parseSafeRegex } from "../../shared/properties/safeRegex";
import type { EnumOption, PropertyType } from "../../shared/properties/types";
import { newRuleDraft, ruleOf, type RuleDraft } from "./ruleDrafts";
import styles from "./RulesEditor.module.css";

/** Operators whose value is a number; the others have their own control. */
const numeric: readonly RuleOp[] = [
  "greater_than",
  "greater_or_equal",
  "less_than",
  "less_or_equal",
  "not_equal",
  "min_length",
  "max_length",
  "min_count",
  "max_count",
];

/**
 * Rules of a definition (F08.R2): one line per rule with the control its operator needs, and « Ajouter : + … » with
 * the operators of the type. A type without operator (`bool`) says so.
 */
export function RulesEditor({
  projectId,
  type,
  options,
  rules,
  onChange,
}: {
  readonly projectId: ElementId;
  readonly type: PropertyType;
  readonly options: readonly EnumOption[];
  readonly rules: readonly RuleDraft[];
  readonly onChange: (rules: readonly RuleDraft[]) => void;
}) {
  const { t } = useTranslation();
  const available = availableRules(type);
  if (available.length === 0) {
    return <p className={styles["muted"]}>{t("property.noRulesBool")}</p>;
  }
  const opLabel = (op: RuleOp) => t(`property.op.${op}`);
  const replace = (rule: RuleDraft, next: RuleDraft) => {
    onChange(rules.map((r) => (r === rule ? next : r)));
  };
  return (
    <div className={styles["editor"]}>
      {rules.length > 0 && (
        <ul className={styles["list"]} aria-label={t("property.rules")}>
          {rules.map((rule) => (
            <li key={rule.uid}>
              <div role="group" aria-label={opLabel(rule.op)} className={styles["line"]}>
                <span className={styles["op"]}>{opLabel(rule.op)}</span>
                <RuleValue
                  projectId={projectId}
                  rule={rule}
                  type={type}
                  options={options}
                  onChange={(next) => {
                    replace(rule, next);
                  }}
                />
                <button
                  type="button"
                  className={styles["remove"]}
                  aria-label={t("property.removeRule", { op: opLabel(rule.op) })}
                  onClick={() => {
                    onChange(rules.filter((r) => r !== rule));
                  }}
                >
                  <X aria-hidden size={14} />
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <div className={styles["add"]} role="group" aria-label={t("property.addRule")}>
        <span className={styles["addLabel"]}>{t("property.addRule")}</span>
        {available.map((op) => (
          <button
            key={op}
            type="button"
            className={styles["addButton"]}
            onClick={() => {
              onChange([...rules, newRuleDraft(op, options)]);
            }}
          >
            <Plus aria-hidden size={12} />
            {opLabel(op)}
          </button>
        ))}
      </div>
      {rules.some((r) => ruleOf(r, type, options) === null) && (
        <p className={styles["hint"]}>{t("property.rulesHint")}</p>
      )}
    </div>
  );
}

function RuleValue({
  projectId,
  rule,
  type,
  options,
  onChange,
}: {
  readonly projectId: ElementId;
  readonly rule: RuleDraft;
  readonly type: PropertyType;
  readonly options: readonly EnumOption[];
  readonly onChange: (rule: RuleDraft) => void;
}) {
  const { t } = useTranslation();
  const invalid = ruleOf(rule, type, options) === null;
  const common = { className: styles["input"], "aria-invalid": invalid || undefined };
  if (rule.op === "not_empty") return null;
  if (numeric.includes(rule.op)) {
    return (
      <input
        {...common}
        inputMode="decimal"
        aria-label={t("property.ruleValue")}
        value={rule.value}
        onChange={(event) => {
          onChange({ ...rule, value: event.target.value });
        }}
      />
    );
  }
  switch (rule.op) {
    case "between":
      return (
        <>
          <input
            {...common}
            inputMode="decimal"
            aria-label={t("property.ruleMin")}
            value={rule.value}
            onChange={(event) => {
              onChange({ ...rule, value: event.target.value });
            }}
          />
          <input
            {...common}
            inputMode="decimal"
            aria-label={t("property.ruleMax")}
            value={rule.value2}
            onChange={(event) => {
              onChange({ ...rule, value2: event.target.value });
            }}
          />
        </>
      );
    case "match":
      return (
        <input
          {...common}
          className={`${styles["input"] ?? ""} ${styles["mono"] ?? ""}`}
          aria-label={t("property.rulePattern")}
          aria-invalid={rule.value === "" || !parseSafeRegex(rule.value).ok || undefined}
          value={rule.value}
          onChange={(event) => {
            onChange({ ...rule, value: event.target.value });
          }}
        />
      );
    case "after":
    case "before":
      return (
        <input
          {...common}
          type="date"
          aria-label={t("property.ruleDate")}
          value={rule.value}
          onChange={(event) => {
            onChange({ ...rule, value: event.target.value });
          }}
        />
      );
    case "kind":
      return (
        <select
          className={styles["input"]}
          aria-label={t("property.ruleKind")}
          value={rule.value}
          onChange={(event) => {
            onChange({ ...rule, value: event.target.value });
          }}
        >
          <option value="page">{t("kinds.page")}</option>
          <option value="category">{t("kinds.category")}</option>
        </select>
      );
    case "in_category":
      return <CategoryChoice projectId={projectId} rule={rule} onChange={onChange} />;
    case "in":
      return (
        <fieldset className={styles["keys"]}>
          <legend className={styles["visuallyHidden"]}>{t("property.ruleOptions")}</legend>
          {options.map((option) => (
            <label key={option.key} className={styles["key"]}>
              <input
                type="checkbox"
                checked={rule.keys.includes(option.key)}
                onChange={(event) => {
                  onChange({
                    ...rule,
                    keys: event.target.checked
                      ? [...rule.keys, option.key]
                      : rule.keys.filter((k) => k !== option.key),
                  });
                }}
              />
              {option.label}
            </label>
          ))}
        </fieldset>
      );
    default:
      return null;
  }
}

function CategoryChoice({
  projectId,
  rule,
  onChange,
}: {
  readonly projectId: ElementId;
  readonly rule: RuleDraft;
  readonly onChange: (rule: RuleDraft) => void;
}) {
  const { t } = useTranslation();
  const id = useId();
  const categories = useQuery(categoriesQuery(projectId));
  return (
    <select
      id={id}
      className={styles["input"]}
      aria-label={t("property.ruleCategory")}
      aria-invalid={rule.value === "" || undefined}
      value={rule.value}
      onChange={(event) => {
        onChange({ ...rule, value: event.target.value });
      }}
    >
      <option value="">{t("property.empty")}</option>
      {(categories.data ?? []).map((c) => (
        <option key={c.id} value={c.id}>
          {c.name}
        </option>
      ))}
    </select>
  );
}
