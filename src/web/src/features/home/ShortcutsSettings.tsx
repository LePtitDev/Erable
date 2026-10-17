import { RotateCcw } from "lucide-react";
import { useEffect, useState, type KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { useSavePreferences } from "../../shared/api/preferences";
import {
  comboFromEvent,
  findConflict,
  platformKey,
  shortcutActions,
  shortcuts,
  type ResolvedShortcuts,
  type ShortcutAction,
} from "../../shared/keyboard/shortcuts";
import { setRecordingShortcut } from "../../shared/keyboard/capture";
import { useFormatCombo } from "../../shared/keyboard/useFormatCombo";
import { useShortcuts } from "../../shared/keyboard/useShortcuts";
import { Button } from "../../shared/ui/Button";
import styles from "./ShortcutsSettings.module.css";

/** The overrides to store: only what differs from the defaults of the platform (an empty combination stays: no shortcut). */
function overridesOf(resolved: ResolvedShortcuts): Record<string, string> {
  const overrides: Record<string, string> = {};
  for (const action of shortcutActions) {
    if (resolved[action] !== platformKey(shortcuts[action])) overrides[action] = resolved[action];
  }
  return overrides;
}

interface Pending {
  readonly action: ShortcutAction;
  readonly combo: string;
  readonly conflict: ShortcutAction;
}

/**
 * Configuration of the shortcuts (F15.R4, E2E-15-04): press a combination to assign it; one that another action
 * already uses is reported, with the choice to take it over. The list is generated from the table of shortcuts.
 */
export function ShortcutsSettings() {
  const { t } = useTranslation();
  const resolved = useShortcuts();
  const save = useSavePreferences();
  const format = useFormatCombo();
  const [capturing, setCapturing] = useState<ShortcutAction | null>(null);
  const [pending, setPending] = useState<Pending | null>(null);

  useEffect(() => {
    setRecordingShortcut(capturing !== null);
    return () => {
      setRecordingShortcut(false);
    };
  }, [capturing]);

  const store = (next: ResolvedShortcuts) => {
    void save({ shortcuts: overridesOf(next) }).catch((error: unknown) => {
      console.error("The shortcuts could not be saved", error);
    });
  };

  const onKeyDown = (action: ShortcutAction, event: KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === "Escape") {
      event.preventDefault();
      event.stopPropagation();
      setCapturing(null);
      return;
    }
    if (event.key === "Tab") return;
    event.preventDefault();
    event.stopPropagation();
    const combo = comboFromEvent(event.nativeEvent);
    if (combo === null) return;
    setCapturing(null);
    const conflict = findConflict(action, combo, resolved);
    if (conflict !== null) {
      setPending({ action, combo, conflict });
      return;
    }
    store({ ...resolved, [action]: combo });
  };

  const replace = () => {
    if (pending === null) return;
    store({ ...resolved, [pending.conflict]: "", [pending.action]: pending.combo });
    setPending(null);
  };

  const modified = shortcutActions.some((a) => resolved[a] !== platformKey(shortcuts[a]));

  return (
    <fieldset className={styles["settings"]}>
      <legend className={styles["legend"]}>{t("shortcuts.title")}</legend>
      <p className={styles["hint"]}>{t("shortcuts.hint")}</p>
      <ul className={styles["list"]}>
        {shortcutActions.map((action) => {
          const combo = resolved[action];
          const isDefault = combo === platformKey(shortcuts[action]);
          return (
            <li key={action} className={styles["row"]}>
              <span className={styles["name"]}>{t(`shortcuts.actions.${action}`)}</span>
              <button
                type="button"
                className={styles["key"]}
                aria-label={t("shortcuts.change", { action: t(`shortcuts.actions.${action}`) })}
                onClick={() => {
                  setPending(null);
                  setCapturing(action);
                }}
                onKeyDown={
                  capturing === action
                    ? (event) => {
                        onKeyDown(action, event);
                      }
                    : undefined
                }
                onBlur={() => {
                  setCapturing((current) => (current === action ? null : current));
                }}
                ref={(node) => {
                  if (capturing === action) node?.focus();
                }}
              >
                {capturing === action ? (
                  <span className={styles["capturing"]}>{t("shortcuts.capturing")}</span>
                ) : combo === "" ? (
                  <span className={styles["none"]}>{t("shortcuts.none")}</span>
                ) : (
                  <kbd>{format(combo)}</kbd>
                )}
              </button>
              <button
                type="button"
                className={styles["reset"]}
                disabled={isDefault}
                aria-label={t("shortcuts.reset", { action: t(`shortcuts.actions.${action}`) })}
                onClick={() => {
                  setPending(null);
                  const next = { ...resolved, [action]: platformKey(shortcuts[action]) };
                  // Giving a default back may take a combination the user gave to another action: that one loses it.
                  const clash = findConflict(action, next[action], next);
                  store(clash === null ? next : { ...next, [clash]: "" });
                }}
              >
                <RotateCcw aria-hidden size={14} />
              </button>
            </li>
          );
        })}
      </ul>
      {pending !== null && (
        <div role="alert" className={styles["conflict"]}>
          <p>
            {t("shortcuts.conflict", {
              combo: format(pending.combo),
              action: t(`shortcuts.actions.${pending.conflict}`),
            })}
          </p>
          <div className={styles["conflictActions"]}>
            <Button variant="primary" onClick={replace}>
              {t("shortcuts.replace")}
            </Button>
            <Button
              onClick={() => {
                setPending(null);
              }}
            >
              {t("common.cancel")}
            </Button>
          </div>
        </div>
      )}
      <Button
        disabled={!modified}
        onClick={() => {
          setPending(null);
          store(
            Object.fromEntries(
              shortcutActions.map((a) => [a, platformKey(shortcuts[a])]),
            ) as ResolvedShortcuts,
          );
        }}
      >
        {t("shortcuts.resetAll")}
      </Button>
    </fieldset>
  );
}
