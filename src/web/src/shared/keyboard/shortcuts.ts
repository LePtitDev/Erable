/**
 * Keyboard shortcuts as data (docs/architecture.md §11.3): one entry per action, a default per platform, in
 * CodeMirror key notation (`Mod` = Ctrl, or Cmd on macOS). Made editable in the preferences by M5-01, which will
 * store overrides next to the other preferences and detect conflicts (F15.R4).
 */
export interface Shortcut {
  /** Windows and Linux. */
  readonly other: string;
  readonly mac: string;
}

export const shortcuts = {
  /** Not Alt+Space (window menu, PowerToys, non-breaking space on macOS): §A-11.3. */
  openPalette: { other: "Ctrl-Space", mac: "Mod-Shift-k" },
  save: { other: "Mod-s", mac: "Mod-s" },
} as const satisfies Record<string, Shortcut>;

export function isMac(): boolean {
  return /Mac|iPhone|iPad/.test(navigator.platform) || navigator.userAgent.includes("Mac OS X");
}

export function platformKey(shortcut: Shortcut): string {
  return isMac() ? shortcut.mac : shortcut.other;
}
