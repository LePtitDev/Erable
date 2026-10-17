import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { formatCombo, isMac, type KeyNames } from "./shortcuts";

/** Formats a combination for display, with the names of the keys of the interface language (« Strg » in German). */
export function useFormatCombo(): (combo: string) => string {
  const { t } = useTranslation();
  return useCallback(
    (combo: string) => {
      const names: KeyNames = {
        ctrl: t("shortcuts.keyNames.ctrl"),
        alt: t("shortcuts.keyNames.alt"),
        shift: t("shortcuts.keyNames.shift"),
        meta: t("shortcuts.keyNames.meta"),
        space: t("shortcuts.keyNames.space"),
      };
      return formatCombo(combo, names, isMac());
    },
    [t],
  );
}

/** The combination in the key names of the ARIA specification (`Control+S`), for `aria-keyshortcuts`. */
export function ariaKeyShortcuts(combo: string): string | undefined {
  if (combo === "") return undefined;
  const mac = isMac();
  const mods = { ctrl: false, alt: false, shift: false, meta: false };
  const parts = combo.split("-");
  const key = combo.endsWith("-") ? "-" : (parts.pop() ?? "");
  for (const part of parts) {
    switch (part.toLowerCase()) {
      case "ctrl":
      case "control":
        mods.ctrl = true;
        break;
      case "alt":
        mods.alt = true;
        break;
      case "shift":
        mods.shift = true;
        break;
      case "meta":
      case "cmd":
        mods.meta = true;
        break;
      case "mod":
        if (mac) mods.meta = true;
        else mods.ctrl = true;
        break;
    }
  }
  const aria = key.toLowerCase() === "space" ? "Space" : key.length === 1 ? key.toUpperCase() : key;
  return [
    mods.ctrl && "Control",
    mods.alt && "Alt",
    mods.shift && "Shift",
    mods.meta && "Meta",
    aria,
  ]
    .filter((part) => part !== false)
    .join("+");
}
