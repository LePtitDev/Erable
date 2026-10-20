import { useQuery } from "@tanstack/react-query";
import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { propertiesQuery } from "../../shared/api/structure";
import {
  conflictsOf,
  resolvedMarkdown,
  type Choice,
  type MergeConflict,
} from "../../shared/api/mergeConflict";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import styles from "./ConflictResolution.module.css";

/** What the user settled: the whole page, and the revision it is saved against. */
export interface Resolution {
  readonly markdown: string;
  readonly revision: number;
  readonly categories: readonly ElementId[] | null;
  readonly properties:
    readonly { readonly propertyId: ElementId; readonly value: unknown }[] | null;
}

interface ConflictResolutionProps {
  readonly projectId: ElementId;
  readonly conflict: MergeConflict;
  readonly busy: boolean;
  readonly onResolve: (resolution: Resolution) => void;
  /** Back to the editor, nothing saved: the page keeps the text the user wrote. */
  readonly onCancel: () => void;
}

/** The last lines of the text before a passage and the first ones after it: enough to place it on the page. */
function excerpt(text: string, where: "end" | "start", lines = 3): string {
  const all = text.replace(/\n$/, "").split("\n");
  if (all.length <= lines) return text;
  return where === "end"
    ? "…\n" + all.slice(-lines).join("\n")
    : all.slice(0, lines).join("\n") + "\n…";
}

const empty = "";
const sides = ["mine", "theirs"] as const;

/**
 * The resolution view (E2E-14-08): both versions of each passage both editors changed, a choice for each —
 * mine, theirs, both, or by hand — and the properties changed on both sides. Saving needs every passage settled; the
 * version that is not kept stays in the history of the page, nothing is ever lost.
 */
export function ConflictResolution({
  projectId,
  conflict,
  busy,
  onResolve,
  onCancel,
}: ConflictResolutionProps) {
  const { t } = useTranslation();
  const properties = useQuery(propertiesQuery(projectId));
  const [choices, setChoices] = useState<ReadonlyMap<number, Choice>>(new Map());
  const [valueChoices, setValueChoices] = useState<ReadonlyMap<string, "mine" | "theirs">>(
    new Map(),
  );
  const names = conflict.mergedWith.map((a) => a.name).join(", ");
  const passages = conflictsOf(conflict);
  const markdown = resolvedMarkdown(conflict, choices);
  const valuesSettled = conflict.valueConflicts.every((c) => valueChoices.has(c.propertyId));
  const nameOf = (id: ElementId) => properties.data?.find((p) => p.id === id)?.name ?? id;

  const choose = (index: number, choice: Choice) => {
    setChoices((current) => new Map(current).set(index, choice));
  };

  const save = () => {
    if (markdown === null || !valuesSettled) return;
    const values =
      conflict.values === null
        ? null
        : Object.entries(conflict.values).flatMap(([key, theirs]) => {
            const asked = conflict.valueConflicts.find((c) => c.propertyId === key);
            const value =
              asked !== undefined && valueChoices.get(key) === "mine" ? asked.mine : theirs;
            return value === null ? [] : [{ propertyId: key as ElementId, value }];
          });
    // A property I wanted and the other editor removed is not in their values: it comes back through my choice.
    const restored = conflict.valueConflicts
      .filter(
        (c) =>
          valueChoices.get(c.propertyId) === "mine" &&
          c.mine !== null &&
          !(values ?? []).some((v) => v.propertyId === c.propertyId),
      )
      .map((c) => ({ propertyId: c.propertyId, value: c.mine }));
    onResolve({
      markdown,
      revision: conflict.currentRevision,
      categories: conflict.categories,
      properties:
        values === null && restored.length === 0 ? null : [...(values ?? []), ...restored],
    });
  };

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onCancel();
      }}
      title={t("conflict.title")}
      description={t("conflict.description", { names })}
      size="lg"
      footer={
        <>
          <Button onClick={onCancel} disabled={busy}>
            {t("conflict.cancel")}
          </Button>
          <Button
            variant="primary"
            onClick={save}
            disabled={busy || markdown === null || !valuesSettled}
          >
            {t("conflict.save")}
          </Button>
        </>
      }
    >
      <div className={styles["passages"]}>
        {passages.map(({ block, index }, position) => {
          const before = conflict.blocks[index - 1];
          const after = conflict.blocks[index + 1];
          return (
            <Passage
              key={index}
              position={position + 1}
              total={passages.length}
              names={names}
              context={{
                before: before?.kind === "stable" ? excerpt(before.text, "end") : empty,
                after: after?.kind === "stable" ? excerpt(after.text, "start") : empty,
              }}
              mine={block.mine}
              theirs={block.theirs}
              choice={choices.get(index)}
              onChoose={(choice) => {
                choose(index, choice);
              }}
            />
          );
        })}

        {conflict.valueConflicts.length > 0 && (
          <section aria-labelledby="value-conflicts" className={styles["passage"]}>
            <h3 id="value-conflicts" className={styles["legend"]}>
              {t("conflict.values.title")}
            </h3>
            {conflict.valueConflicts.map((c) => (
              <fieldset key={c.propertyId} className={styles["choices"]}>
                <legend>{nameOf(c.propertyId)}</legend>
                {sides.map((side) => (
                  <label key={side} className={styles["choice"]}>
                    <input
                      type="radio"
                      name={`value-${c.propertyId}`}
                      checked={valueChoices.get(c.propertyId) === side}
                      onChange={() => {
                        setValueChoices((current) => new Map(current).set(c.propertyId, side));
                      }}
                    />
                    {t(`conflict.values.${side}`, {
                      value: JSON.stringify(side === "mine" ? c.mine : c.theirs),
                      names,
                    })}
                  </label>
                ))}
              </fieldset>
            ))}
          </section>
        )}
      </div>
    </Dialog>
  );
}

function Passage({
  position,
  total,
  names,
  context,
  mine,
  theirs,
  choice,
  onChoose,
}: {
  readonly position: number;
  readonly total: number;
  readonly names: string;
  readonly context: { readonly before: string; readonly after: string };
  readonly mine: string;
  readonly theirs: string;
  readonly choice: Choice | undefined;
  readonly onChoose: (choice: Choice) => void;
}) {
  const { t } = useTranslation();
  const group = useId();
  const [custom, setCustom] = useState(mine);
  const radio = (kind: Choice["kind"], label: string, onSelect: () => void) => (
    <label className={styles["choice"]}>
      <input type="radio" name={group} checked={choice?.kind === kind} onChange={onSelect} />
      {label}
    </label>
  );
  return (
    <section
      className={styles["passage"]}
      aria-label={t("conflict.item", { index: position, total })}
    >
      <h3 className={styles["legend"]}>{t("conflict.item", { index: position, total })}</h3>
      {context.before !== empty && <pre className={styles["context"]}>{context.before}</pre>}
      <div className={styles["versions"]}>
        <div>
          <h4 className={styles["versionTitle"]}>{t("conflict.mine")}</h4>
          <pre className={styles["version"]} data-version="mine">
            {mine === empty ? t("conflict.empty") : mine}
          </pre>
        </div>
        <div>
          <h4 className={styles["versionTitle"]}>{t("conflict.theirs", { names })}</h4>
          <pre className={styles["version"]} data-version="theirs">
            {theirs === empty ? t("conflict.empty") : theirs}
          </pre>
        </div>
      </div>
      {context.after !== empty && <pre className={styles["context"]}>{context.after}</pre>}
      <div
        role="radiogroup"
        aria-label={t("conflict.item", { index: position, total })}
        className={styles["choices"]}
      >
        {radio("mine", t("conflict.keepMine"), () => {
          onChoose({ kind: "mine" });
        })}
        {radio("theirs", t("conflict.keepTheirs"), () => {
          onChoose({ kind: "theirs" });
        })}
        {radio("both", t("conflict.keepBoth"), () => {
          onChoose({ kind: "both" });
        })}
        {radio("custom", t("conflict.custom"), () => {
          onChoose({ kind: "custom", text: custom });
        })}
      </div>
      {choice?.kind === "custom" && (
        <textarea
          className={styles["custom"]}
          aria-label={t("conflict.customLabel", { index: position })}
          rows={Math.min(10, custom.split("\n").length + 1)}
          value={custom}
          onChange={(e) => {
            setCustom(e.target.value);
            onChoose({ kind: "custom", text: e.target.value });
          }}
        />
      )}
    </section>
  );
}
