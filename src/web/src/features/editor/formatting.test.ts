import { EditorSelection, EditorState } from "@codemirror/state";
import { describe, expect, it } from "vitest";
import { insertText, setHeading, toggleInline } from "./formatting";

function at(doc: string, from: number, to = from) {
  return EditorState.create({ doc, selection: EditorSelection.single(from, to) });
}

function apply(state: EditorState, spec: ReturnType<typeof toggleInline>) {
  return state.update(spec).state;
}

describe("toggleInline", () => {
  it("wraps a selected word in ** (E2E-05-13)", () => {
    const s = apply(at("Elle parle peu.", 5, 10), toggleInline(at("Elle parle peu.", 5, 10), "**"));

    expect(s.doc.toString()).toBe("Elle **parle** peu.");
    expect(s.sliceDoc(s.selection.main.from, s.selection.main.to)).toBe("parle");
  });

  it("removes the markers when the selection is already wrapped", () => {
    const start = at("Elle **parle** peu.", 7, 12);

    expect(apply(start, toggleInline(start, "**")).doc.toString()).toBe("Elle parle peu.");
  });

  it("puts the cursor between an empty pair", () => {
    const start = at("ab", 1);
    const s = apply(start, toggleInline(start, "*"));

    expect(s.doc.toString()).toBe("a**b");
    expect(s.selection.main.head).toBe(2);
  });
});

describe("setHeading", () => {
  it("turns a line into a heading and changes its level", () => {
    const start = at("Histoire\nsuite", 2);
    const h2 = apply(start, setHeading(start, 2));
    const h3 = apply(h2, setHeading(h2, 3));

    expect(h2.doc.toString()).toBe("## Histoire\nsuite");
    expect(h3.doc.toString()).toBe("### Histoire\nsuite");
    expect(apply(h3, setHeading(h3, 0)).doc.toString()).toBe("Histoire\nsuite");
  });
});

describe("insertText", () => {
  it("replaces the selection and leaves the cursor after the text", () => {
    const start = at("Voir X.", 5, 6);
    const s = apply(start, insertText(start, "[[7A1C4E90B3F2D615]]"));

    expect(s.doc.toString()).toBe("Voir [[7A1C4E90B3F2D615]].");
    expect(s.selection.main.head).toBe(25);
  });
});
