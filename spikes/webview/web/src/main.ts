// Throwaway spike (M0-08): exercises the WebView features Erable depends on and records what happened in
// `window.spike`, so that run-checks.mjs (CDP) — or a human — can read the results.
import { defaultKeymap, history, historyKeymap } from "@codemirror/commands";
import { markdown } from "@codemirror/lang-markdown";
import { EditorState, RangeSetBuilder } from "@codemirror/state";
import {
  Decoration,
  type DecorationSet,
  EditorView,
  keymap,
  ViewPlugin,
  type ViewUpdate,
  WidgetType,
} from "@codemirror/view";

type Log = string[];
const spike = {
  ready: false,
  dnd: [] as Log,
  files: [] as Log,
  keys: [] as Log,
  video: [] as Log,
  editor: undefined as EditorView | undefined,
  metrics: {} as Record<string, number>,
};
(window as unknown as { spike: typeof spike }).spike = spike;

function log(target: Log, element: string, message: string) {
  target.push(message);
  const pre = document.getElementById(element);
  if (pre) pre.textContent = target.join("\n");
}

// 1 · CodeMirror: [[ID]] shown as an atomic pill with the element name.
const names: Record<string, string> = { "3F2A9C14E07B5D21": "Capitaine Orsen" };

class PillWidget extends WidgetType {
  constructor(readonly name: string) {
    super();
  }
  override eq(other: PillWidget) {
    return other.name === this.name;
  }
  toDOM() {
    const span = document.createElement("span");
    span.className = "pill";
    span.textContent = this.name;
    return span;
  }
}

function pills(view: EditorView): DecorationSet {
  const builder = new RangeSetBuilder<Decoration>();
  for (const { from, to } of view.visibleRanges) {
    const text = view.state.doc.sliceString(from, to);
    for (const m of text.matchAll(/\[\[([0-9A-F]{16})\]\]/g)) {
      const start = from + (m.index ?? 0);
      builder.add(start, start + m[0].length, Decoration.replace({ widget: new PillWidget(names[m[1] ?? ""] ?? "?") }));
    }
  }
  return builder.finish();
}

const pillPlugin = ViewPlugin.fromClass(
  class {
    decorations: DecorationSet;
    constructor(view: EditorView) {
      this.decorations = pills(view);
    }
    update(update: ViewUpdate) {
      if (update.docChanged || update.viewportChanged) this.decorations = pills(update.view);
    }
  },
  {
    decorations: (v) => v.decorations,
    provide: (plugin) => EditorView.atomicRanges.of((view) => view.plugin(plugin)?.decorations ?? Decoration.none),
  },
);

spike.editor = new EditorView({
  parent: document.getElementById("editor")!,
  state: EditorState.create({
    doc: "Allié de [[3F2A9C14E07B5D21]] depuis le Col gelé.",
    extensions: [history(), keymap.of([...defaultKeymap, ...historyKeymap]), markdown(), pillPlugin],
  }),
});

// 2 · HTML5 drag and drop in a tree, with a forbidden target (no preventDefault → "not allowed" cursor).
const tree = document.getElementById("tree")!;
let dragged: HTMLElement | null = null;
tree.addEventListener("dragstart", (e) => {
  dragged = e.target as HTMLElement;
  e.dataTransfer?.setData("text/plain", dragged.dataset["id"] ?? "");
  if (e.dataTransfer) e.dataTransfer.effectAllowed = "move";
  log(spike.dnd, "dnd-log", `dragstart ${dragged.dataset["id"]}`);
});
tree.addEventListener("dragover", (e) => {
  const target = (e.target as HTMLElement).closest("li");
  if (!target || target === dragged) return;
  if (target.dataset["forbidden"]) {
    if (e.dataTransfer) e.dataTransfer.dropEffect = "none";
    return; // drop refused
  }
  e.preventDefault();
  if (e.dataTransfer) e.dataTransfer.dropEffect = "move";
  target.classList.add("over");
});
tree.addEventListener("dragleave", (e) => (e.target as HTMLElement).closest("li")?.classList.remove("over"));
tree.addEventListener("drop", (e) => {
  e.preventDefault();
  const target = (e.target as HTMLElement).closest("li");
  target?.classList.remove("over");
  if (target && dragged && !target.dataset["forbidden"]) {
    target.after(dragged);
    log(spike.dnd, "dnd-log", `drop ${dragged.dataset["id"]} after ${target.dataset["id"]}`);
  }
});
tree.addEventListener("dragend", (e) => log(spike.dnd, "dnd-log", `dragend dropEffect=${e.dataTransfer?.dropEffect}`));

// 3 · Video: seeking needs HTTP Range requests from the WebView's media stack.
const video = document.getElementById("video") as HTMLVideoElement;
for (const type of ["loadedmetadata", "seeked", "error", "canplay"]) {
  video.addEventListener(type, () =>
    spike.video.push(`${type} t=${video.currentTime.toFixed(2)} ready=${video.readyState} err=${video.error?.code ?? "-"}`),
  );
}

// 4 · Paste an image / drop files from the OS.
const paste = document.getElementById("paste")!;
function describeFiles(source: string, files: FileList | undefined | null) {
  for (const f of Array.from(files ?? [])) log(spike.files, "files-log", `${source} ${f.name} ${f.type} ${f.size}`);
}
paste.addEventListener("paste", (e) => {
  e.preventDefault();
  describeFiles("paste", e.clipboardData?.files);
  if (!e.clipboardData?.files.length) log(spike.files, "files-log", `paste text: ${e.clipboardData?.getData("text/plain")}`);
});
document.addEventListener("dragover", (e) => {
  if (e.dataTransfer?.types.includes("Files")) e.preventDefault();
});
document.addEventListener("drop", (e) => {
  if (!e.dataTransfer?.types.includes("Files")) return;
  e.preventDefault();
  describeFiles("drop", e.dataTransfer.files);
});

// 5 · Shortcuts must reach the page, not the WebView.
window.addEventListener(
  "keydown",
  (e) => {
    const mod = e.ctrlKey || e.metaKey;
    const combo = `${e.ctrlKey ? "Ctrl+" : ""}${e.metaKey ? "Meta+" : ""}${e.shiftKey ? "Shift+" : ""}${e.key}`;
    if (mod && ["s", "k", " ", "K"].includes(e.key)) {
      e.preventDefault();
      log(spike.keys, "keys-log", `handled ${combo}`);
    }
  },
  { capture: true },
);

// 6 · Metrics.
const nav = performance.getEntriesByType("navigation")[0] as PerformanceNavigationTiming | undefined;
spike.metrics = {
  domContentLoaded: Math.round(nav?.domContentLoadedEventEnd ?? 0),
  load: Math.round(nav?.loadEventEnd ?? performance.now()),
};
document.getElementById("metrics")!.textContent = JSON.stringify(spike.metrics, null, 2);
spike.ready = true;
