import { parseElementId, type ElementId } from "../ids/elementId";
import { ApiError } from "./client";

/** A part of the merged page: text both editors agree on, or a passage they changed differently. */
export type MergeBlock =
  | { readonly kind: "stable"; readonly text: string }
  | {
      readonly kind: "conflict";
      readonly base: string;
      readonly mine: string;
      readonly theirs: string;
    };

/** A property both editors changed, differently. Values are the JSON of the property (see src/shared/properties). */
export interface ValueConflict {
  readonly propertyId: ElementId;
  readonly mine: unknown;
  readonly theirs: unknown;
}

/**
 * The answer of a save that cannot be merged (409 `page.revisionConflict` with details): the page as the other
 * editor left it, the merged text in blocks, and the structure the page would have if the conflicts went their way.
 */
export interface MergeConflict {
  /** The revision to save against once the conflicts are settled. */
  readonly currentRevision: number;
  readonly blocks: readonly MergeBlock[];
  readonly valueConflicts: readonly ValueConflict[];
  /** Categories after the merge, or null when the save carried none. */
  readonly categories: readonly ElementId[] | null;
  /** Values after the merge (property ID → JSON), conflicts in the other editor's favour; null when none were sent. */
  readonly values: Readonly<Record<string, unknown>> | null;
  readonly mergedWith: readonly { readonly id: string; readonly name: string }[];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function text(value: unknown): string {
  return typeof value === "string" ? value : "";
}

/** The details of a refused merge, or null when the error is something else (or a conflict without details). */
export function mergeConflictOf(error: unknown): MergeConflict | null {
  if (!(error instanceof ApiError) || error.problem?.["code"] !== "page.revisionConflict")
    return null;
  const problem = error.problem;
  const current = problem["current"];
  const blocks = problem["blocks"];
  if (!isRecord(current) || typeof current["revision"] !== "number" || !Array.isArray(blocks))
    return null;

  const parsedBlocks: MergeBlock[] = [];
  for (const block of blocks as unknown[]) {
    if (!isRecord(block)) return null;
    parsedBlocks.push(
      block["kind"] === "conflict"
        ? {
            kind: "conflict",
            base: text(block["base"]),
            mine: text(block["mine"]),
            theirs: text(block["theirs"]),
          }
        : { kind: "stable", text: text(block["text"]) },
    );
  }

  const valueConflicts: ValueConflict[] = [];
  const rawConflicts = problem["valueConflicts"];
  for (const item of Array.isArray(rawConflicts) ? (rawConflicts as unknown[]) : []) {
    if (!isRecord(item)) continue;
    const propertyId = parseElementId(text(item["propertyId"]));
    if (propertyId !== null) {
      valueConflicts.push({
        propertyId,
        mine: item["mine"] ?? null,
        theirs: item["theirs"] ?? null,
      });
    }
  }

  const merged = problem["mergedStructure"];
  const categories =
    isRecord(merged) && Array.isArray(merged["categories"])
      ? (merged["categories"] as unknown[])
          .map((c) => parseElementId(text(c)))
          .filter((c): c is ElementId => c !== null)
      : null;
  const values = isRecord(merged) && isRecord(merged["values"]) ? merged["values"] : null;

  const authors = problem["mergedWith"];
  const mergedWith = (Array.isArray(authors) ? (authors as unknown[]) : [])
    .filter(isRecord)
    .map((a) => ({ id: text(a["id"]), name: text(a["name"]) }));

  return {
    currentRevision: current["revision"],
    blocks: parsedBlocks,
    valueConflicts,
    categories,
    values,
    mergedWith,
  };
}

/** The conflicting passages of a refused merge. */
export function conflictsOf(conflict: MergeConflict) {
  return conflict.blocks.flatMap((block, index) =>
    block.kind === "conflict" ? [{ block, index }] : [],
  );
}

/** How the user settled one passage. */
export type Choice =
  | { readonly kind: "mine" }
  | { readonly kind: "theirs" }
  | { readonly kind: "both" }
  | { readonly kind: "custom"; readonly text: string };

function endsLine(value: string): boolean {
  return value === "" || value.endsWith("\n");
}

/** The text a choice gives for a passage. « Both » keeps mine, then theirs, each on its own lines. */
export function textOf(block: Extract<MergeBlock, { kind: "conflict" }>, choice: Choice): string {
  switch (choice.kind) {
    case "mine":
      return block.mine;
    case "theirs":
      return block.theirs;
    case "both":
      return (endsLine(block.mine) ? block.mine : block.mine + "\n") + block.theirs;
    case "custom":
      return choice.text;
  }
}

/** The whole page once each conflicting passage (by index in the blocks) is settled; null while one is not. */
export function resolvedMarkdown(
  conflict: MergeConflict,
  choices: ReadonlyMap<number, Choice>,
): string | null {
  let result = "";
  for (const [index, block] of conflict.blocks.entries()) {
    if (block.kind === "stable") {
      result += block.text;
      continue;
    }
    const choice = choices.get(index);
    if (choice === undefined) return null;
    result += textOf(block, choice);
  }
  return result;
}
