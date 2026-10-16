import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ElementId } from "../../shared/ids/elementId";
import type { PropertyValue } from "../../shared/properties/types";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import styles from "../category/CategoryView.module.css";
import { initialValue, type Definition } from "./definitions";
import { ValueEditor } from "./ValueEditor";

/**
 * « Ajouter une propriété »: picks a definition and gives it a first value (a category's default, or a value of a
 * page). The caller decides which definitions are offered.
 */
export function AddPropertyDialog({
  projectId,
  open,
  definitions,
  valueLabel,
  onClose,
  onAdd,
}: {
  readonly projectId: ElementId;
  readonly open: boolean;
  readonly definitions: readonly Definition[];
  /** Accessible name of the value editor for a property name. */
  readonly valueLabel: (name: string) => string;
  readonly onClose: () => void;
  readonly onAdd: (id: ElementId, value: PropertyValue) => void;
}) {
  const { t } = useTranslation();
  const selectId = useId();
  const [selected, setSelected] = useState<ElementId | null>(null);
  const [value, setValue] = useState<PropertyValue | undefined>(undefined);
  const definition = definitions.find((d) => d.id === selected);
  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) onClose();
      }}
      title={t("category.addPropertyTitle")}
      footer={
        <>
          <Button onClick={onClose}>{t("common.cancel")}</Button>
          <Button
            variant="primary"
            disabled={definition === undefined || value === undefined}
            onClick={() => {
              if (definition !== undefined && value !== undefined) {
                onAdd(definition.id, value);
                setSelected(null);
                setValue(undefined);
              }
            }}
          >
            {t("category.addProperty")}
          </Button>
        </>
      }
    >
      <div className={styles["dialogBody"]}>
        <label htmlFor={selectId} className={styles["label"]}>
          {t("category.column.property")}
        </label>
        <select
          id={selectId}
          className={styles["select"]}
          value={selected ?? ""}
          onChange={(event) => {
            const id = event.target.value === "" ? null : (event.target.value as ElementId);
            setSelected(id);
            const next = definitions.find((d) => d.id === id);
            setValue(next === undefined ? undefined : initialValue(next));
          }}
        >
          <option value="">{t("property.empty")}</option>
          {definitions.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
        {definition !== undefined && (
          <ValueEditor
            projectId={projectId}
            definition={definition}
            label={valueLabel(definition.name)}
            value={value}
            onChange={setValue}
          />
        )}
      </div>
    </Dialog>
  );
}
