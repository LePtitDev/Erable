import { EditorState } from "@codemirror/state";
import { describe, expect, it } from "vitest";
import { parseElementId, type ElementId } from "../../shared/ids/elementId";
import {
  addUpload,
  completeUpload,
  progressUpload,
  removeUpload,
  uploadPos,
  uploads,
  uploadsField,
} from "./uploads";

const id = parseElementId("5B19C0E2A4D7F311") as ElementId;

function stateWith(doc: string, pos: number) {
  const state = EditorState.create({ doc, extensions: uploads((name) => name) });
  return state.update({ effects: addUpload.of({ key: "a", pos, name: "carte.png" }) }).state;
}

describe("uploads", () => {
  it("addUpload_LeavesTheDocumentUntouched", () => {
    const state = stateWith("Avant après", 6);

    expect(state.doc.toString()).toBe("Avant après");
    expect(state.field(uploadsField)).toEqual([
      { key: "a", pos: 6, name: "carte.png", fraction: 0 },
    ]);
  });

  it("placeholder_TextTypedBefore_FollowsTheEdit", () => {
    const state = stateWith("Avant après", 6);

    const next = state.update({ changes: { from: 0, insert: "Tout " } }).state;

    expect(uploadPos(next, "a")).toBe(11);
  });

  it("progressUpload_UpdatesTheFraction", () => {
    const state = stateWith("x", 1).update({
      effects: progressUpload.of({ key: "a", fraction: 0.5 }),
    }).state;

    expect(state.field(uploadsField)[0]?.fraction).toBe(0.5);
  });

  it("completeUpload_InsertsTheEmbedWhereThePlaceholderIs", () => {
    const state = stateWith("Avant après", 6);

    const spec = completeUpload(state, "a", id, false);
    const next = state.update(spec ?? {}).state;

    expect(next.doc.toString()).toBe(`Avant ![[${id}]]après`);
    expect(next.field(uploadsField)).toEqual([]);
  });

  it("completeUpload_SeveralFiles_SeparatesThemWithALineBreak", () => {
    const state = stateWith("", 0);

    const next = state.update(completeUpload(state, "a", id, true) ?? {}).state;

    expect(next.doc.toString()).toBe(`![[${id}]]\n`);
  });

  it("removeUpload_FailedUpload_LeavesNoTrace", () => {
    const state = stateWith("Avant après", 6);

    const next = state.update({ effects: removeUpload.of("a") }).state;

    expect(next.doc.toString()).toBe("Avant après");
    expect(next.field(uploadsField)).toEqual([]);
    expect(completeUpload(next, "a", id, false)).toBeNull();
  });
});
