import { Search } from "lucide-react";
import type { KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { KindBadge } from "../../shared/ui/KindBadge";
import { shortId } from "../page/markdown/text";
import styles from "./InsertPalette.module.css";
import { paletteListId } from "./palette";
import { paletteTabs } from "./paletteSearch";
import { groupByKind, nextTab, optionId, type PalettePopupProps } from "./paletteView";

const insertModes = ["link", "embed"] as const;

/**
 * The popup of the insert palette, at the cursor (mockup « Page — édition »). Opened by typing, the focus stays in the
 * editor (which announces the highlighted option); opened by the shortcut, its own search field takes the focus.
 */
export function PalettePopup({
  snapshot,
  tab,
  results,
  active,
  ownQuery,
  onOwnQuery,
  onTab,
  onSelect,
  onPick,
  onClose,
  onMove,
}: PalettePopupProps) {
  const { t } = useTranslation();
  const shortcut = snapshot.trigger === "shortcut";
  const style =
    snapshot.at === null
      ? undefined
      : {
          left: Math.max(8, Math.min(snapshot.at.left, window.innerWidth - 460)),
          top: Math.min(snapshot.at.top, window.innerHeight - 360),
        };

  const onInputKey = (event: KeyboardEvent) => {
    const run: Record<string, (() => void) | undefined> = {
      ArrowDown: () => {
        onMove(1);
      },
      ArrowUp: () => {
        onMove(-1);
      },
      Enter: () => {
        onPick(event.shiftKey);
      },
      Tab: () => {
        onTab(nextTab(paletteTabs, tab, event.shiftKey ? -1 : 1));
      },
      Escape: onClose,
    };
    const action = run[event.key];
    if (action !== undefined) {
      event.preventDefault();
      action();
    }
  };

  const groups = tab === "all" ? groupByKind(results) : [{ kind: null, items: [...results] }];

  return (
    <div
      role="dialog"
      aria-label={t("palette.title")}
      className={styles["palette"]}
      style={style}
      onMouseDown={(e) => {
        // Keep the focus (and the cursor) in the editor.
        if (!(e.target instanceof HTMLInputElement)) e.preventDefault();
      }}
    >
      <label className={styles["search"]}>
        <Search aria-hidden size={16} />
        <input
          aria-label={t("palette.filter")}
          value={shortcut ? ownQuery : snapshot.query}
          readOnly={!shortcut}
          tabIndex={shortcut ? 0 : -1}
          autoFocus={shortcut}
          onChange={(e) => {
            onOwnQuery(e.target.value);
          }}
          onKeyDown={onInputKey}
          {...(shortcut
            ? {
                "aria-controls": paletteListId,
                ...(active === null ? {} : { "aria-activedescendant": optionId(active.id) }),
              }
            : {})}
        />
      </label>
      <div role="tablist" aria-label={t("palette.tabs")} className={styles["tabs"]}>
        {paletteTabs.map((name) => (
          <button
            key={name}
            type="button"
            role="tab"
            tabIndex={-1}
            aria-selected={tab === name}
            className={styles["tab"]}
            onClick={() => {
              onTab(name);
            }}
          >
            {t(`palette.tab.${name}`)}
          </button>
        ))}
      </div>
      <div
        id={paletteListId}
        role="listbox"
        aria-label={t("palette.suggestions")}
        className={styles["list"]}
      >
        {results.length === 0 && <p className={styles["empty"]}>{t("palette.empty")}</p>}
        {groups.map((group) => (
          <div
            key={group.kind ?? "all"}
            role="group"
            aria-label={
              group.kind === null ? t(`palette.tab.${tab}`) : t(`palette.group.${group.kind}`)
            }
          >
            {group.kind !== null && (
              <span className={styles["groupTitle"]}>{t(`palette.group.${group.kind}`)}</span>
            )}
            {group.items.map((entry) => (
              <div
                key={entry.id}
                id={optionId(entry.id)}
                role="option"
                aria-selected={entry === active}
                className={styles["option"]}
                onMouseEnter={() => {
                  onSelect(entry);
                }}
                onClick={(e) => {
                  onPick(e.shiftKey, entry);
                }}
              >
                <KindBadge kind={entry.kind} />
                <span className={styles["optionText"]}>
                  <span className={styles["optionName"]}>{entry.label}</span>
                  <span className={styles["optionPath"]}>{entry.path}</span>
                </span>
                <span className={styles["optionId"]}>{shortId(entry.id)}</span>
              </div>
            ))}
          </div>
        ))}
      </div>
      <div className={styles["footer"]}>
        <span>
          <kbd>↑↓</kbd> {t("palette.keys.navigate")}
        </span>
        {insertModes.map((mode) => (
          // Mouse access to « link » and « embed » for the highlighted option, outside the listbox: an option holds a
          // single target (axe-core nested-interactive); the keyboard has Enter and Shift+Enter.
          <button
            key={mode}
            type="button"
            tabIndex={-1}
            className={styles["footerAction"]}
            disabled={active === null}
            onClick={() => {
              onPick(mode === "embed");
            }}
          >
            <kbd>{t(mode === "embed" ? "palette.keys.shiftEnter" : "palette.keys.enter")}</kbd>{" "}
            {t(`palette.keys.${mode}`)}
          </button>
        ))}
        <span>
          <kbd>{t("palette.keys.tab")}</kbd> {t("palette.keys.nextTab")}
        </span>
      </div>
      <span role="status" className={styles["live"]}>
        {t("palette.count", { count: results.length })}
      </span>
    </div>
  );
}
