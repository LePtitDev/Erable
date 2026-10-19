import { Folder } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import field from "../../shared/ui/Field.module.css";
import { KindBadge } from "../../shared/ui/KindBadge";
import styles from "./MoveDialog.module.css";
import { moveDestinations, normalize, type TreeIndex } from "./treeModel";

/**
 * « Déplacer vers… » (F18.R5): moving an element without dragging it, on a touch screen as with a mouse. Lists the
 * valid destinations only; choosing one does what a drop inside it does.
 */
export function MoveDialog({
  index,
  id,
  onMove,
  onClose,
}: {
  readonly index: TreeIndex;
  /** The element to move; null while the dialog is closed. */
  readonly id: ElementId | null;
  readonly onMove: (id: ElementId, parentId: ElementId) => void;
  readonly onClose: () => void;
}) {
  const { t } = useTranslation();
  const [filter, setFilter] = useState("");
  const node = id === null ? undefined : index.byId.get(id);
  const needle = normalize(filter.trim());
  const destinations = id === null ? [] : moveDestinations(index, id);
  const shown =
    needle === ""
      ? destinations
      : destinations.filter((d) => normalize(d.node.name).includes(needle));

  const close = () => {
    setFilter("");
    onClose();
  };

  return (
    <Dialog
      open={id !== null}
      onOpenChange={(open) => {
        if (!open) close();
      }}
      title={t("explorer.moveDialog.title", { name: node?.name ?? "" })}
      footer={<Button onClick={close}>{t("common.cancel")}</Button>}
    >
      {destinations.length > 5 && (
        <input
          type="search"
          className={field["input"]}
          aria-label={t("explorer.moveDialog.filter")}
          placeholder={t("explorer.moveDialog.filter")}
          value={filter}
          onChange={(e) => {
            setFilter(e.target.value);
          }}
        />
      )}
      {destinations.length === 0 ? (
        <p className={styles["none"]}>{t("explorer.moveDialog.none")}</p>
      ) : (
        <ul className={styles["list"]} aria-label={t("explorer.moveDialog.destinations")}>
          {shown.map((destination) => (
            <li key={destination.node.id}>
              <button
                type="button"
                className={styles["destination"]}
                onClick={() => {
                  if (id !== null) onMove(id, destination.node.id);
                  close();
                }}
              >
                {destination.node.kind === "folder" ? (
                  <Folder aria-hidden size={16} className={styles["folder"]} />
                ) : destination.node.kind === "root" ? null : (
                  <KindBadge kind={destination.node.kind} />
                )}
                <span className={styles["text"]}>
                  <span className={styles["name"]}>{destination.node.name}</span>
                  {destination.path.length > 0 && (
                    <span className={styles["path"]}>{destination.path.join(" / ")}</span>
                  )}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </Dialog>
  );
}
