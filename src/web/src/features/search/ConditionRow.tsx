import { X } from "lucide-react";
import { useTranslation } from "react-i18next";
import type { ElementId } from "../../shared/ids/elementId";
import type { Definition } from "../properties/definitions";
import { ValueEditor } from "../properties/ValueEditor";
import { itemDefinition, newCondition, operandOf, operandValue } from "./conditions";
import styles from "./SearchView.module.css";
import { needsOperand, operatorsFor, type ConditionOp, type SearchCondition } from "./searchState";

/**
 * One line of the « Propriétés » filter: property, operator and a value editor suited to the type of the property
 * (mockup « Recherche »).
 */
export function ConditionRow({
  projectId,
  index,
  condition,
  definition,
  definitions,
  onChange,
  onRemove,
}: {
  readonly projectId: ElementId;
  readonly index: number;
  readonly condition: SearchCondition;
  readonly definition: Definition | undefined;
  readonly definitions: readonly Definition[];
  readonly onChange: (condition: SearchCondition) => void;
  readonly onRemove: () => void;
}) {
  const { t } = useTranslation();
  const name = definition?.name ?? condition.property;
  const item = definition === undefined ? undefined : itemDefinition(definition);
  const operators = item === undefined ? [] : operatorsFor(item.type.scalar);
  const suffix = String(index);

  const changeProperty = (id: ElementId) => {
    const next = definitions.find((d) => d.id === id);
    if (next !== undefined) onChange(newCondition(next));
  };
  const changeOperator = (op: ConditionOp) => {
    onChange({ ...condition, op, value: needsOperand(op) ? condition.value : "" });
  };

  return (
    <div className={styles["condition"]} role="group" aria-label={name}>
      <select
        className={styles["select"]}
        aria-label={t("search.property")}
        value={condition.property}
        onChange={(event) => {
          changeProperty(event.target.value as ElementId);
        }}
      >
        {definition === undefined && (
          <option value={condition.property}>{condition.property}</option>
        )}
        {definitions.map((d) => (
          <option key={d.id} value={d.id}>
            {d.name}
          </option>
        ))}
      </select>
      <select
        className={`${styles["select"] ?? ""} ${styles["operator"] ?? ""}`}
        aria-label={t("search.operator")}
        value={condition.op}
        onChange={(event) => {
          changeOperator(event.target.value as ConditionOp);
        }}
      >
        {operators.map((op) => (
          <option key={op} value={op}>
            {t(`search.ops.${op}`)}
          </option>
        ))}
      </select>
      {item !== undefined && needsOperand(condition.op) && (
        <div className={styles["operand"]}>
          <Operand
            projectId={projectId}
            definition={item}
            id={`condition-${suffix}`}
            value={condition.value}
            onChange={(value) => {
              onChange({ ...condition, value });
            }}
          />
        </div>
      )}
      <button
        type="button"
        className={styles["removeCondition"]}
        aria-label={t("search.removeCondition", { name })}
        onClick={onRemove}
      >
        <X aria-hidden size={14} />
      </button>
    </div>
  );
}

function Operand({
  projectId,
  definition,
  id,
  value,
  onChange,
}: {
  readonly projectId: ElementId;
  readonly definition: Definition;
  readonly id: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
}) {
  const { t } = useTranslation();
  if (definition.type.scalar === "bool") {
    return (
      <select
        className={styles["select"]}
        aria-label={t("search.value")}
        value={value === "true" ? "true" : "false"}
        onChange={(event) => {
          onChange(event.target.value);
        }}
      >
        <option value="false">{t("property.no")}</option>
        <option value="true">{t("property.yes")}</option>
      </select>
    );
  }
  return (
    <ValueEditor
      projectId={projectId}
      definition={definition}
      label={t("search.value")}
      id={id}
      value={operandValue(definition, value)}
      onChange={(next) => {
        onChange(operandOf(next));
      }}
    />
  );
}
