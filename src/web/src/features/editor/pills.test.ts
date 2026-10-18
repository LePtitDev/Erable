import { ensureSyntaxTree } from "@codemirror/language";
import { EditorState } from "@codemirror/state";
import type { DecorationSet } from "@codemirror/view";
import { describe, expect, it } from "vitest";
import { requireElementId } from "../../shared/ids/elementId";
import {
  findPills,
  markdownWithReferences,
  pillDecorations,
  pills,
  referencedIds,
  setRawIds,
  setTargets,
  type PillTarget,
} from "./pills";

const orsen = requireElementId("7A1C4E90B3F2D615");
const hautbois = requireElementId("5B19C0E2A4D7F311");

function state(doc: string, targets: [string, PillTarget][] = []) {
  let s = EditorState.create({ doc, extensions: [markdownWithReferences(), pills()] });
  ensureSyntaxTree(s, s.doc.length, 5000);
  s = s.update({
    effects: setTargets.of(new Map(targets.map(([id, t]) => [requireElementId(id), t]))),
  }).state;
  return s;
}

function ranges(set: DecorationSet): [number, number][] {
  const result: [number, number][] = [];
  set.between(0, Number.MAX_SAFE_INTEGER, (from, to) => {
    result.push([from, to]);
  });
  return result;
}

const all = (s: EditorState) => [{ from: 0, to: s.doc.length }];

describe("findPills", () => {
  it("finds references and embeds with their exact range", () => {
    const s = state(`Voir [[${orsen}]] et ![[${hautbois}|carte]]{width=320}.`);

    expect(findPills(s).map((p) => [p.from, p.to, p.match.embed])).toEqual([
      [5, 25, false],
      [29, 67, true],
    ]);
  });

  it("ignores references in inline code and code blocks (R7)", () => {
    const s = state(
      `\`[[${orsen}]]\`\n\n\`\`\`\n[[${orsen}]]\n\`\`\`\n\n    [[${orsen}]]\n\n[[${hautbois}]]`,
    );

    expect(findPills(s).map((p) => p.match.id)).toEqual([hautbois]);
  });

  it("ignores an escaped bracket (R6) and an ID of the wrong length (R1)", () => {
    const s = state(`\\[[${orsen}]] [[7A1C4E90B3F2D61]] [[${orsen}0]]`);

    expect(findPills(s)).toEqual([]);
  });

  it("lists each referenced ID once", () => {
    expect(referencedIds(state(`[[${orsen}]] [[${hautbois}]] [[${orsen}|lui]]`))).toEqual([
      orsen,
      hautbois,
    ]);
  });
});

describe("pillDecorations (F05.R1, F05.R2)", () => {
  it("replaces each reference by one decoration covering it", () => {
    const s = state(`Le [[${orsen}]] garde [[${hautbois}#lieux|le port]].`, [
      [orsen, { name: "Capitaine Orsen", kind: "page", state: "existing" }],
    ]);

    expect(ranges(pillDecorations(s, all(s)))).toEqual([
      [3, 23],
      [30, 64],
    ]);
  });

  it("shows the label, else the current name, else the ID", () => {
    const s = state(`[[${orsen}]] [[${hautbois}|le port]] [[0123456789ABCDEF]]`, [
      [orsen, { name: "Capitaine Orsen", kind: "page", state: "existing" }],
    ]);
    const texts: string[] = [];
    pillDecorations(s, all(s)).between(0, s.doc.length, (_from, _to, deco) => {
      const spec = deco.spec as { widget: { text: string } };
      texts.push(spec.widget.text);
    });

    expect(texts).toEqual(["Capitaine Orsen", "le port", "0123456789ABCDEF"]);
  });

  it("only decorates the requested (visible) ranges", () => {
    const doc = Array.from({ length: 100 }, (_, i) => `Ligne ${String(i)} [[${orsen}]]`).join("\n");
    const s = state(doc);
    const line = s.doc.line(50);

    expect(ranges(pillDecorations(s, [{ from: line.from, to: line.to }]))).toHaveLength(1);
  });

  it("shows nothing but the source when the raw IDs are shown, without changing the document", () => {
    const s = state(`[[${orsen}]]`);
    const raw = s.update({ effects: setRawIds.of(true) }).state;

    expect(ranges(pillDecorations(raw, all(raw)))).toEqual([]);
    expect(raw.doc.toString()).toBe(s.doc.toString());
  });

  it("decorates only the visible screen of a page of 5 000 lines", () => {
    const doc = Array.from(
      { length: 5000 },
      (_, i) => `Ligne ${String(i)} avec [[${orsen}]] et du texte.`,
    ).join("\n");
    const s = state(doc);
    const screen = { from: s.doc.line(2500).from, to: s.doc.line(2560).to };

    // The time budget is measured in a real browser by the @perf tests (docs/testing.md §11.1), never here (§T-1.6).
    const set = pillDecorations(s, [screen]);

    expect(ranges(set)).toHaveLength(61);
  });
});
