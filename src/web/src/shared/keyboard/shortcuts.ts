/**
 * Keyboard shortcuts as data (docs/architecture.md §11.3): one entry per action, a default per platform, in
 * CodeMirror key notation (`Mod` = Ctrl, or Cmd on macOS). The user's overrides live in the preferences
 * (`UserPreferences.shortcuts`, action → combination); an empty combination means « no shortcut » (F15.R4).
 */
export interface Shortcut {
  /** Windows and Linux. */
  readonly other: string;
  readonly mac: string;
}

export const shortcuts = {
  /** Not Alt+Space (window menu, PowerToys, non-breaking space on macOS): §A-11.3. */
  openPalette: { other: "Ctrl-Space", mac: "Mod-Shift-k" },
  /** Quick search (F10.R1); the same on every platform: ⌘K on macOS. */
  quickSearch: { other: "Mod-k", mac: "Mod-k" },
  save: { other: "Mod-s", mac: "Mod-s" },
  openFolder: { other: "Mod-o", mac: "Mod-o" },
} as const satisfies Record<string, Shortcut>;

export type ShortcutAction = keyof typeof shortcuts;

export const shortcutActions = Object.keys(shortcuts) as readonly ShortcutAction[];

export function isShortcutAction(value: string): value is ShortcutAction {
  return Object.hasOwn(shortcuts, value);
}

/**
 * The platform as CodeMirror sees it (`navigator.platform`), so that `Mod` is the same key in the editor and in the
 * window. Not the user agent: WebKit announces « Mac OS X » on every system (Playwright's WebKit on Linux among them).
 */
export function isMac(): boolean {
  return /Mac|iPhone|iPad/.test(navigator.platform);
}

export function platformKey(shortcut: Shortcut, mac: boolean = isMac()): string {
  return mac ? shortcut.mac : shortcut.other;
}

/** The combination of every action: the user's override when there is one, else the default of the platform. */
export type ResolvedShortcuts = Readonly<Record<ShortcutAction, string>>;

export function resolveShortcuts(
  overrides: Readonly<Record<string, string>> | null | undefined,
  mac: boolean = isMac(),
): ResolvedShortcuts {
  const resolved = {} as Record<ShortcutAction, string>;
  for (const action of shortcutActions) {
    resolved[action] = overrides?.[action] ?? platformKey(shortcuts[action], mac);
  }
  return resolved;
}

interface Parsed {
  readonly ctrl: boolean;
  readonly alt: boolean;
  readonly shift: boolean;
  readonly meta: boolean;
  /** Lower-cased, `space` for the space bar. */
  readonly key: string;
}

/** Reads a CodeMirror combination (`Mod-Shift-k`, `Ctrl-Space`, `Alt--`): modifiers, then the key. */
function parse(combo: string, mac: boolean): Parsed | null {
  if (combo === "") return null;
  const dash =
    combo.length > 1 && combo.endsWith("-") ? combo.length - 1 : combo.lastIndexOf("-") + 1;
  const key = combo.slice(dash === 0 ? 0 : dash);
  const modifiers = (dash === 0 ? "" : combo.slice(0, dash - 1)).split("-").filter((m) => m !== "");
  let ctrl = false;
  let alt = false;
  let shift = false;
  let meta = false;
  for (const modifier of modifiers) {
    switch (modifier.toLowerCase()) {
      case "ctrl":
      case "control":
        ctrl = true;
        break;
      case "alt":
      case "option":
        alt = true;
        break;
      case "shift":
        shift = true;
        break;
      case "meta":
      case "cmd":
      case "command":
        meta = true;
        break;
      case "mod":
        if (mac) meta = true;
        else ctrl = true;
        break;
      default:
        return null;
    }
  }
  return { ctrl, alt, shift, meta, key: key.toLowerCase() };
}

/** A form that two spellings of the same combination share, for the comparison (`Mod-k` = `Ctrl-k` off macOS). */
export function canonicalCombo(combo: string, mac: boolean = isMac()): string {
  const p = parse(combo, mac);
  if (p === null) return "";
  return [p.ctrl && "ctrl", p.alt && "alt", p.shift && "shift", p.meta && "meta", p.key]
    .filter((part) => part !== false)
    .join("+");
}

const modifierKeys = new Set(["Control", "Shift", "Alt", "Meta", "AltGraph", "OS"]);

/**
 * The combination a key press stands for, in CodeMirror notation; null while only modifiers are down, or when the
 * press has no modifier and is not a function key (a bare letter would make typing impossible).
 */
export function comboFromEvent(
  event: Pick<KeyboardEvent, "key" | "code" | "ctrlKey" | "altKey" | "shiftKey" | "metaKey">,
): string | null {
  if (modifierKeys.has(event.key)) return null;
  let key = event.key;
  if (key === " ") key = "Space";
  else if (/^Key[A-Z]$/.test(event.code)) key = event.code.slice(3).toLowerCase();
  else if (/^Digit\d$/.test(event.code)) key = event.code.slice(5);
  else if (key.length === 1) key = key.toLowerCase();
  const functionKey = /^F\d{1,2}$/.test(key);
  if (!functionKey && !event.ctrlKey && !event.altKey && !event.metaKey) return null;
  return [
    event.ctrlKey && "Ctrl",
    event.altKey && "Alt",
    event.shiftKey && "Shift",
    event.metaKey && "Meta",
    key,
  ]
    .filter((part) => part !== false)
    .join("-");
}

/** Whether a key press is the combination (the listeners of the window use it; CodeMirror has its own keymap). */
export function matchesEvent(
  event: Pick<KeyboardEvent, "key" | "code" | "ctrlKey" | "altKey" | "shiftKey" | "metaKey">,
  combo: string,
  mac: boolean = isMac(),
): boolean {
  const pressed = comboFromEvent(event);
  return pressed !== null && canonicalCombo(pressed, mac) === canonicalCombo(combo, mac);
}

/** The other action that already has this combination, if any (F15.R4, E2E-15-04). */
export function findConflict(
  action: ShortcutAction,
  combo: string,
  resolved: ResolvedShortcuts,
  mac: boolean = isMac(),
): ShortcutAction | null {
  const wanted = canonicalCombo(combo, mac);
  if (wanted === "") return null;
  return (
    shortcutActions.find(
      (other) => other !== action && canonicalCombo(resolved[other], mac) === wanted,
    ) ?? null
  );
}

/** Names of the keys shown to the user, translated (« Strg » in German). */
export interface KeyNames {
  readonly ctrl: string;
  readonly alt: string;
  readonly shift: string;
  readonly meta: string;
  readonly space: string;
}

const macSymbols = { ctrl: "⌃", alt: "⌥", shift: "⇧", meta: "⌘" } as const;

/** The combination as shown: `Ctrl+Shift+L`, or `⌃⇧L` / `⌘⇧K` on macOS (the symbols are the same in every language). */
export function formatCombo(combo: string, names: KeyNames, mac: boolean = isMac()): string {
  const p = parse(combo, mac);
  if (p === null) return "";
  const key =
    p.key === "space" ? names.space : p.key.length === 1 ? p.key.toUpperCase() : titleCase(p.key);
  if (mac) {
    return `${p.ctrl ? macSymbols.ctrl : ""}${p.alt ? macSymbols.alt : ""}${p.shift ? macSymbols.shift : ""}${p.meta ? macSymbols.meta : ""}${key}`;
  }
  return [
    p.ctrl && names.ctrl,
    p.alt && names.alt,
    p.shift && names.shift,
    p.meta && names.meta,
    key,
  ]
    .filter((part) => part !== false)
    .join("+");
}

function titleCase(key: string): string {
  return key.charAt(0).toUpperCase() + key.slice(1);
}
