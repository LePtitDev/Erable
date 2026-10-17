import { describe, expect, it } from "vitest";
import {
  canonicalCombo,
  comboFromEvent,
  findConflict,
  formatCombo,
  matchesEvent,
  resolveShortcuts,
  type KeyNames,
} from "./shortcuts";

const names: KeyNames = { ctrl: "Ctrl", alt: "Alt", shift: "Maj", meta: "Méta", space: "Espace" };

function press(
  key: string,
  modifiers: Partial<Record<"ctrlKey" | "altKey" | "shiftKey" | "metaKey", boolean>>,
  code = "",
) {
  return {
    key,
    code,
    ctrlKey: false,
    altKey: false,
    shiftKey: false,
    metaKey: false,
    ...modifiers,
  };
}

describe("resolveShortcuts", () => {
  it("uses the defaults of the platform without overrides", () => {
    expect(resolveShortcuts(null, false).openPalette).toBe("Ctrl-Space");
    expect(resolveShortcuts(null, true).openPalette).toBe("Mod-Shift-k");
  });

  it("prefers an override, an empty one included", () => {
    const resolved = resolveShortcuts({ openPalette: "Ctrl-Shift-l", quickSearch: "" }, false);
    expect(resolved.openPalette).toBe("Ctrl-Shift-l");
    expect(resolved.quickSearch).toBe("");
  });
});

describe("canonicalCombo", () => {
  it("treats Mod as Ctrl off macOS and as Meta on macOS", () => {
    expect(canonicalCombo("Mod-k", false)).toBe("ctrl+k");
    expect(canonicalCombo("Mod-k", true)).toBe("meta+k");
  });

  it("ignores the order of the modifiers and the case of the key", () => {
    expect(canonicalCombo("Shift-Ctrl-L", false)).toBe(canonicalCombo("Ctrl-Shift-l", false));
  });
});

describe("comboFromEvent", () => {
  it("ignores a modifier alone and a bare letter", () => {
    expect(comboFromEvent(press("Control", { ctrlKey: true }))).toBeNull();
    expect(comboFromEvent(press("a", {}, "KeyA"))).toBeNull();
  });

  it("reads the letter from the physical key, whatever Shift does to it", () => {
    expect(comboFromEvent(press("L", { ctrlKey: true, shiftKey: true }, "KeyL"))).toBe(
      "Ctrl-Shift-l",
    );
  });

  it("names the space bar", () => {
    expect(comboFromEvent(press(" ", { ctrlKey: true }, "Space"))).toBe("Ctrl-Space");
  });

  it("accepts a function key without modifier", () => {
    expect(comboFromEvent(press("F2", {}, "F2"))).toBe("F2");
  });
});

describe("matchesEvent", () => {
  it("matches the default save combination", () => {
    expect(matchesEvent(press("s", { ctrlKey: true }, "KeyS"), "Mod-s", false)).toBe(true);
    expect(matchesEvent(press("s", { metaKey: true }, "KeyS"), "Mod-s", true)).toBe(true);
    expect(
      matchesEvent(press("s", { ctrlKey: true, shiftKey: true }, "KeyS"), "Mod-s", false),
    ).toBe(false);
  });
});

describe("findConflict", () => {
  it("names the action that already has the combination (E2E-15-04)", () => {
    const resolved = resolveShortcuts(null, false);
    expect(findConflict("openPalette", "Ctrl-k", resolved, false)).toBe("quickSearch");
  });

  it("finds none for a free combination, for the action's own one or for an empty one", () => {
    const resolved = resolveShortcuts(null, false);
    expect(findConflict("openPalette", "Ctrl-Shift-l", resolved, false)).toBeNull();
    expect(findConflict("quickSearch", "Ctrl-k", resolved, false)).toBeNull();
    expect(findConflict("quickSearch", "", resolved, false)).toBeNull();
  });
});

describe("formatCombo", () => {
  it("spells the keys out off macOS", () => {
    expect(formatCombo("Ctrl-Shift-l", names, false)).toBe("Ctrl+Maj+L");
    expect(formatCombo("Ctrl-Space", names, false)).toBe("Ctrl+Espace");
    expect(formatCombo("Mod-k", names, false)).toBe("Ctrl+K");
  });

  it("uses the symbols of macOS", () => {
    expect(formatCombo("Mod-Shift-k", names, true)).toBe("⇧⌘K");
  });
});
