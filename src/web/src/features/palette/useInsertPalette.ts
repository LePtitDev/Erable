import { useQuery } from "@tanstack/react-query";
import type { EditorView } from "@codemirror/view";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type RefObject,
} from "react";
import { api, unwrap } from "../../shared/api/client";
import { queryKeys } from "../../shared/api/keys";
import { treeQuery } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import type { MarkdownEditorHandle } from "../editor/MarkdownEditor";
import {
  closePalette,
  insertPalette,
  insertReference,
  paletteField,
  paletteQuery,
  setActiveOption,
} from "./palette";
import {
  buildPaletteIndex,
  paletteTabs,
  searchPalette,
  type PaletteItem,
  type PaletteTab,
} from "./paletteSearch";
import {
  groupByKind,
  nextTab,
  optionId,
  type PalettePopupProps,
  type Snapshot,
} from "./paletteView";

interface Handlers {
  move: (delta: number) => void;
  pick: (embed: boolean) => boolean;
  changeTab: (delta: number) => void;
}

/** The palette extension, whose keys call the latest handlers (read when a key is pressed, never while rendering). */
function paletteExtension(handlers: RefObject<Handlers>) {
  return insertPalette({
    move: (delta) => {
      handlers.current.move(delta);
    },
    pick: (embed) => handlers.current.pick(embed),
    tab: (delta) => {
      handlers.current.changeTab(delta);
    },
  });
}

function snapshotOf(view: EditorView): Snapshot {
  const p = view.state.field(paletteField);
  const coords = p.open ? view.coordsAtPos(p.from) : null;
  return {
    ...p,
    query: paletteQuery(view.state),
    at: coords === null ? null : { left: coords.left, top: coords.bottom + 4 },
  };
}

/**
 * The insert palette (F05.R3, E2E-05-03 to 05-06): searches the tree and the attachments already loaded in the client
 * (no request per keystroke; < 50 ms on 10 000 elements), five tabs cycled with Tab, Enter for a link, Shift+Enter for
 * an embed, Escape closes. Returns the editor extension, the update callback and the props of `PalettePopup`.
 */
export function useInsertPalette(
  projectId: ElementId,
  editor: RefObject<MarkdownEditorHandle | null>,
) {
  const tree = useQuery(treeQuery(projectId));
  const attachments = useQuery({
    queryKey: [...queryKeys.projectContent(projectId), "attachments"],
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/attachments", {
          params: { path: { projectId } },
          signal,
        }),
      ),
    staleTime: 30_000,
  });
  const index = useMemo(
    () => buildPaletteIndex(tree.data?.nodes ?? [], attachments.data ?? []),
    [tree.data, attachments.data],
  );

  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [tab, setTab] = useState<PaletteTab>("all");
  const [ownQuery, setOwnQuery] = useState("");
  // The highlighted line, back to the first one whenever the query changes.
  const [selection, setSelection] = useState({ query: "", index: 0 });

  const open = snapshot?.open === true;
  const query = snapshot?.trigger === "shortcut" ? ownQuery : (snapshot?.query ?? "");
  // In « Tout », grouped by kind: the keyboard order is the order on screen.
  const results = useMemo(() => {
    if (!open) return [];
    const found = searchPalette(index, query, tab);
    return tab === "all" ? groupByKind(found).flatMap((g) => g.items) : found;
  }, [open, index, query, tab]);
  const selected = selection.query === query ? selection.index : 0;
  const active = results[Math.min(selected, results.length - 1)] ?? null;

  const select = (indexOf: number) => {
    setSelection({ query, index: indexOf });
  };
  const move = (delta: number) => {
    if (results.length > 0) select((selected + delta + results.length) % results.length);
  };
  const pick = (embed: boolean, entry?: PaletteItem | null) => {
    const chosen = entry ?? active;
    if (chosen === null) return false;
    editor.current?.run((state) => insertReference(state, chosen.id, embed));
    return true;
  };
  const changeTab = (delta: number) => {
    setTab((current) => nextTab(paletteTabs, current, delta));
    setSelection({ query, index: 0 });
  };

  // The editor extension lives as long as the editor: its keys call the latest handlers through a ref.
  const handlers = useRef<Handlers>({ move, pick, changeTab });
  useLayoutEffect(() => {
    handlers.current = { move, pick, changeTab };
  });
  // The ref is only read when a key is pressed (see paletteExtension), never while rendering.
  // eslint-disable-next-line react-hooks/refs
  const [extension] = useState(() => paletteExtension(handlers));

  const onUpdate = useCallback((view: EditorView) => {
    const next = snapshotOf(view);
    setSnapshot((previous) => {
      if (!next.open && previous?.open !== true) return previous;
      if (next.open && previous?.open !== true) {
        setTab("all");
        setSelection({ query: next.query, index: 0 });
        setOwnQuery("");
      }
      return next;
    });
  }, []);

  // The highlighted option, announced through the editor (aria-activedescendant).
  const activeId = open && active !== null ? optionId(active.id) : null;
  useEffect(() => {
    const view = editor.current?.view;
    if (
      view !== null &&
      view !== undefined &&
      view.state.field(paletteField).activeOption !== activeId
    ) {
      view.dispatch({ effects: setActiveOption.of(activeId) });
    }
  }, [activeId, editor]);

  const popup: PalettePopupProps | null =
    snapshot !== null && open
      ? {
          snapshot,
          tab,
          results,
          active,
          ownQuery,
          onOwnQuery: setOwnQuery,
          onTab: (next) => {
            setTab(next);
            select(0);
          },
          onSelect: (entry) => {
            select(results.indexOf(entry));
          },
          onPick: pick,
          onClose: () => {
            editor.current?.run(() => ({ effects: closePalette.of(null) }));
          },
          onMove: move,
        }
      : null;

  return { extension, onUpdate, popup };
}
