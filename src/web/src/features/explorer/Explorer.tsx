import { useQuery } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "@tanstack/react-router";
import { useVirtualizer } from "@tanstack/react-virtual";
import {
  ChevronDown,
  ChevronRight,
  ChevronsDownUp,
  Copy,
  FilePlus,
  Folder,
  FolderPlus,
  Search,
  Trash2,
  Users,
} from "lucide-react";
import { DropdownMenu } from "radix-ui";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type DragEvent,
  type KeyboardEvent,
  type MouseEvent,
} from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { treeQuery, type ElementKind, type TreeNodeItem } from "../../shared/api/tree";
import { trashQuery } from "../../shared/api/trash";
import { useUiState } from "../../shared/api/uiState";
import { parseElementId, type ElementId } from "../../shared/ids/elementId";
import { roots } from "../../shared/ids/roots";
import { KindBadge } from "../../shared/ui/KindBadge";
import { useIsNarrowScreen } from "../../shared/ui/media";
import { RecentViewedPanel } from "../recent/RecentViewedPanel";
import { DeleteDialog } from "../trash/DeleteDialog";
import styles from "./Explorer.module.css";
import {
  ancestorsOf,
  canContain,
  checkMove,
  dropDestination,
  indexTree,
  normalize,
  visibleRows,
  type DropPosition,
  type TreeIndex,
  type TreeRow,
} from "./treeModel";
import { useTreeMutations, type CreatableKind } from "./useTreeMutations";
import { useCanEdit, useProjectAccess } from "../project/access";
import { apiInfoQuery } from "../../shared/api/queries";
import { useProjectServer } from "../../shared/api/servers";

/** Above this many visible rows, only the rows on screen are rendered (budget: 10 000 elements, docs/testing.md §11). */
const virtualizeFrom = 200;
const rowHeight = 30;
const dragType = "application/x-erable-element";
const recentHeadingId = "recent-viewed";

type Editing =
  | { readonly mode: "rename"; readonly id: ElementId }
  | { readonly mode: "create"; readonly kind: CreatableKind; readonly parentId: ElementId };

interface DragState {
  readonly id: ElementId;
  readonly target: ElementId | null;
  readonly position: DropPosition;
  readonly allowed: boolean;
}

const rootIds = [roots.pages, roots.categories, roots.properties, roots.links];
const rowDomId = (id: ElementId) => `tree-row-${id}`;
const duplicateHintId = "tree-duplicate-hint";
const dropClass = { before: "dropBefore", inside: "dropInside", after: "dropAfter" } as const;

/** The project explorer (F02, E2E-17-01): an IDE-like tree, at the mouse as on the keyboard. */
export function Explorer({ projectId }: { readonly projectId: ElementId }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const params = useParams({ strict: false });
  const currentId = typeof params.elementId === "string" ? parseElementId(params.elementId) : null;
  const tree = useQuery(treeQuery(projectId));
  const state = useUiState<{ expanded: string[] }>(`tree-${projectId}`, { expanded: rootIds });
  const { update, create } = useTreeMutations(projectId);
  const canEdit = useCanEdit();
  // In the drawer of a phone, opening a folder would close the drawer (F18.R1): a tap unfolds it instead.
  const narrow = useIsNarrowScreen();
  // Members exist where accounts do: on a server, or for a project hosted by a connected one.
  const hosted = useQuery(apiInfoQuery).data?.folderProjects === false;
  const remote = useProjectServer(projectId) !== undefined;
  const showMembers = useProjectAccess().isAdmin && (hosted || remote);

  const index = useMemo(() => indexTree(tree.data?.nodes ?? []), [tree.data]);
  const expanded = useMemo(
    () => new Set(state.value.expanded as ElementId[]),
    [state.value.expanded],
  );
  const [filter, setFilter] = useState("");
  const [focusedId, setFocusedId] = useState<ElementId | null>(null);
  const [editing, setEditing] = useState<Editing | null>(null);
  const [cut, setCut] = useState<ElementId | null>(null);
  const [drag, setDrag] = useState<DragState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [menu, setMenu] = useState<{ x: number; y: number; id: ElementId } | null>(null);
  const [deleting, setDeleting] = useState<ElementId | null>(null);
  const trash = useQuery(trashQuery(projectId));
  const typeahead = useRef({ text: "", at: 0 });
  const treeRef = useRef<HTMLDivElement>(null);
  /** Edit chosen in the context menu, started once the menu has given the focus back. */
  const afterMenu = useRef<(() => void) | null>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  const setExpanded = useCallback(
    (change: (current: Set<ElementId>) => void) => {
      state.update((current) => {
        const next = new Set(current.expanded as ElementId[]);
        change(next);
        return { expanded: [...next] };
      });
    },
    [state],
  );

  // Reveal the element shown in the work area: its ancestors open, its line selected.
  useEffect(() => {
    // Wait for the stored state: written before it arrives, the reveal would be overwritten by it.
    if (!state.loaded || currentId === null || !index.byId.has(currentId)) return;
    const closed = ancestorsOf(index, currentId).filter((a) => !expanded.has(a.id));
    if (closed.length > 0) {
      setExpanded((set) => {
        for (const a of closed) set.add(a.id);
      });
    }
    // Only when the element changes, not when the user collapses an ancestor afterwards.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentId, index, state.loaded]);

  const rows = useMemo(
    () => withCreateRow(visibleRows(index, expanded, filter), editing, index),
    [index, expanded, filter, editing],
  );
  // « Ouverture du projet → arbre affiché < 1 s » (docs/testing.md §11.1): the first time the tree has lines to show.
  const shownMarked = useRef(false);
  useEffect(() => {
    if (rows.length > 0 && !shownMarked.current) {
      shownMarked.current = true;
      performance.mark("erable:tree-shown");
    }
  }, [rows.length]);
  const focused = focusedId ?? currentId ?? rows[0]?.node.id ?? null;
  const focusedIndex = rows.findIndex((r) => r.node.id === focused);

  const virtual = rows.length > virtualizeFrom;
  // TanStack Virtual returns unstable functions: the React Compiler skips this component, as documented upstream.
  // eslint-disable-next-line react-hooks/incompatible-library
  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: () => rowHeight,
    overscan: 12,
    enabled: virtual,
  });

  const focusRow = (row: TreeRow | undefined) => {
    if (row === undefined) return;
    setFocusedId(row.node.id);
    const i = rows.indexOf(row);
    if (virtual) virtualizer.scrollToIndex(i);
    else document.getElementById(rowDomId(row.node.id))?.scrollIntoView({ block: "nearest" });
  };

  const open = (node: TreeNodeItem) => {
    void navigate({ to: "/p/$projectId/e/$elementId", params: { projectId, elementId: node.id } });
  };

  const toggle = (row: TreeRow) => {
    if (!row.expandable) return;
    setExpanded((set) => {
      if (set.has(row.node.id)) set.delete(row.node.id);
      else set.add(row.node.id);
    });
  };

  const fail = (e: unknown) => {
    setError(errorMessage(t, e));
  };

  const move = (id: ElementId, parentId: ElementId, at: number | null) => {
    setError(null);
    update.mutate(
      { id, parentId, index: at },
      {
        onSuccess: () => {
          setExpanded((set) => set.add(parentId));
        },
        onError: fail,
      },
    );
  };

  /** Where a new element of `kind` goes: the focused element if it can contain it, else its nearest container. */
  const createTarget = (kind: CreatableKind): ElementId => {
    const start = focused ?? roots.pages;
    for (const candidate of [start, ...ancestorsOf(index, start).map((a) => a.id)]) {
      if (canContain(index, candidate, elementKindOf(kind))) return candidate;
    }
    return roots.pages;
  };

  const startCreate = (kind: CreatableKind, parentId = createTarget(kind)) => {
    setError(null);
    setFilter("");
    setExpanded((set) => set.add(parentId));
    setEditing({ mode: "create", kind, parentId });
  };

  const commitEdit = (value: string) => {
    const name = value.trim();
    const current = editing;
    setEditing(null);
    treeRef.current?.focus();
    if (current === null || name === "") return;
    if (current.mode === "rename") {
      if (name !== index.byId.get(current.id)?.name) {
        update.mutate({ id: current.id, name }, { onError: fail });
      }
      return;
    }
    create.mutate(
      { kind: current.kind, parentId: current.parentId, name },
      {
        onSuccess: (created) => {
          setFocusedId(created.id);
          if (current.kind === "page") {
            void navigate({
              to: "/p/$projectId/e/$elementId",
              params: { projectId, elementId: created.id },
              search: { edit: true, focus: "title" },
            });
          } else if (current.kind !== "folder") {
            // A new category or property opens in edit mode: parent, description, defaults, type (E2E-07-02).
            void navigate({
              to: "/p/$projectId/e/$elementId",
              params: { projectId, elementId: created.id },
              search: { edit: true },
            });
          }
        },
        onError: fail,
      },
    );
  };

  const paste = (targetId: ElementId) => {
    if (cut === null) return;
    const refusal = checkMove(index, cut, targetId);
    if (refusal !== null) {
      setError(t(`explorer.refused.${refusal}`));
      return;
    }
    move(cut, targetId, null);
    setCut(null);
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (editing !== null) return;
    const row = rows[focusedIndex];
    const ctrl = event.ctrlKey || event.metaKey;
    const handled = () => {
      event.preventDefault();
      event.stopPropagation();
    };
    if (row === undefined) return;
    switch (event.key) {
      case "ArrowDown":
        handled();
        focusRow(rows[focusedIndex + 1]);
        return;
      case "ArrowUp":
        handled();
        focusRow(rows[focusedIndex - 1]);
        return;
      case "Home":
        handled();
        focusRow(rows[0]);
        return;
      case "End":
        handled();
        focusRow(rows.at(-1));
        return;
      case "ArrowRight":
        handled();
        if (row.expandable && !row.expanded) toggle(row);
        else if (row.expanded) focusRow(rows[focusedIndex + 1]);
        return;
      case "ArrowLeft":
        handled();
        if (row.expanded) toggle(row);
        else focusRow(rows.find((r) => r.node.id === row.node.parentId));
        return;
      case "Enter":
        handled();
        open(row.node);
        return;
      case "Delete":
        handled();
        if (canEdit && row.node.kind !== "root") setDeleting(row.node.id);
        return;
      case "F2":
        handled();
        if (canEdit && row.node.kind !== "root") setEditing({ mode: "rename", id: row.node.id });
        return;
      case "Escape":
        if (cut !== null) {
          handled();
          setCut(null);
        }
        return;
    }
    if (canEdit && ctrl && event.key.toLowerCase() === "x" && row.node.kind !== "root") {
      handled();
      setCut(row.node.id);
      return;
    }
    if (canEdit && ctrl && event.key.toLowerCase() === "v") {
      handled();
      paste(row.node.id);
      return;
    }
    if (!ctrl && !event.altKey && event.key.length === 1 && event.key !== " ") {
      // Type-ahead: the next line whose name starts with what was typed in the last half second.
      const now = performance.now();
      const ta = typeahead.current;
      ta.text = (now - ta.at < 500 ? ta.text : "") + normalize(event.key);
      ta.at = now;
      const order = [...rows.slice(focusedIndex + (ta.text.length === 1 ? 1 : 0)), ...rows];
      const match = order.find((r) => normalize(r.node.name).startsWith(ta.text));
      if (match !== undefined) {
        handled();
        focusRow(match);
      }
    }
  };

  // ─── Drag and drop (HTML5, WebKit checked by the M0-08 spike) ─────────────

  const onDragStart = (event: DragEvent, node: TreeNodeItem) => {
    if (node.kind === "root" || !canEdit) {
      event.preventDefault();
      return;
    }
    event.dataTransfer.setData(dragType, node.id);
    event.dataTransfer.effectAllowed = "move";
    setDrag({ id: node.id, target: null, position: "inside", allowed: false });
  };

  const onDragOver = (event: DragEvent<HTMLElement>, node: TreeNodeItem) => {
    if (drag === null) return;
    const position = positionOf(event);
    const destination = dropDestination(index, drag.id, node.id, position);
    const allowed =
      destination !== null && checkMove(index, drag.id, destination.parentId) === null;
    if (allowed) {
      // Accepting the drop; otherwise the browser shows the « not allowed » cursor (E2E-02-04).
      event.preventDefault();
      event.dataTransfer.dropEffect = "move";
    } else {
      event.dataTransfer.dropEffect = "none";
    }
    if (drag.target !== node.id || drag.position !== position || drag.allowed !== allowed) {
      setDrag({ ...drag, target: node.id, position, allowed });
    }
  };

  const onDrop = (event: DragEvent<HTMLElement>, node: TreeNodeItem) => {
    event.preventDefault();
    const current = drag;
    setDrag(null);
    if (current === null) return;
    // From the drop event itself: the state of the last dragover may not be rendered yet.
    const destination = dropDestination(index, current.id, node.id, positionOf(event));
    if (destination !== null && checkMove(index, current.id, destination.parentId) === null) {
      move(current.id, destination.parentId, destination.index);
    }
  };

  const onContextMenu = (event: MouseEvent, node: TreeNodeItem) => {
    event.preventDefault();
    setFocusedId(node.id);
    if (canEdit) setMenu({ x: event.clientX, y: event.clientY, id: node.id });
  };

  const counts = tree.data?.counts;
  const countOf = (id: ElementId) =>
    id === roots.pages
      ? counts?.pages
      : id === roots.categories
        ? counts?.categories
        : id === roots.properties
          ? counts?.properties
          : id === roots.links
            ? counts?.links
            : undefined;

  const renderRow = (row: TreeRow) => {
    const { node } = row;
    const isRoot = node.kind === "root";
    const isEditing =
      editing !== null &&
      ((editing.mode === "rename" && editing.id === node.id) ||
        (editing.mode === "create" && node.id === creatingId));
    const dropHere = drag?.target === node.id ? drag : null;
    const classes = [
      styles["row"],
      isRoot ? styles["rootRow"] : "",
      node.id === focused ? styles["focused"] : "",
      node.id === currentId ? styles["current"] : "",
      node.id === cut ? styles["cut"] : "",
      dropHere === null
        ? ""
        : dropHere.allowed
          ? styles[dropClass[dropHere.position]]
          : styles["refused"],
    ].join(" ");
    return (
      <div
        key={node.id}
        id={rowDomId(node.id)}
        role="treeitem"
        aria-level={row.level}
        aria-posinset={row.position}
        aria-setsize={row.siblings}
        aria-selected={node.id === currentId}
        {...(row.expandable ? { "aria-expanded": row.expanded } : {})}
        {...(node.duplicateName ? { "aria-describedby": duplicateHintId } : {})}
        className={classes}
        style={{
          paddingLeft: `calc(${String(row.level - 1)} * var(--space-4) + var(--space-1-5))`,
        }}
        draggable={canEdit && !isRoot && !isEditing}
        onClick={() => {
          setFocusedId(node.id);
          treeRef.current?.focus();
          if (node.id === creatingId) return;
          if (narrow && row.expandable && (isRoot || node.kind === "folder")) toggle(row);
          else open(node);
        }}
        onDoubleClick={() => {
          if (canEdit && !isRoot) setEditing({ mode: "rename", id: node.id });
        }}
        onContextMenu={(e) => {
          onContextMenu(e, node);
        }}
        onDragStart={(e) => {
          onDragStart(e, node);
        }}
        onDragOver={(e) => {
          onDragOver(e, node);
        }}
        onDragLeave={() => {
          if (drag?.target === node.id) setDrag({ ...drag, target: null });
        }}
        onDrop={(e) => {
          onDrop(e, node);
        }}
        onDragEnd={() => {
          setDrag(null);
        }}
      >
        <span
          className={styles["caret"]}
          aria-hidden
          onClick={(e) => {
            e.stopPropagation();
            setFocusedId(node.id);
            toggle(row);
          }}
        >
          {row.expandable ? (
            row.expanded ? (
              <ChevronDown size={14} />
            ) : (
              <ChevronRight size={14} />
            )
          ) : null}
        </span>
        {node.kind === "folder" ? (
          <Folder aria-hidden size={16} className={styles["folderIcon"]} />
        ) : (
          <KindBadge kind={rootKindBadge(node)} />
        )}
        {isEditing ? (
          <InlineName
            label={
              editing.mode === "rename"
                ? t("explorer.renameLabel")
                : t(`explorer.newName.${editing.kind}`)
            }
            initial={editing.mode === "rename" ? node.name : ""}
            onCommit={commitEdit}
            onCancel={() => {
              setEditing(null);
              treeRef.current?.focus();
            }}
          />
        ) : (
          <span className={styles["label"]}>{node.name}</span>
        )}
        {node.duplicateName && <Copy aria-hidden size={12} className={styles["duplicate"]} />}
        {isRoot && countOf(node.id) !== undefined && (
          <span className={styles["count"]}>{countOf(node.id)}</span>
        )}
      </div>
    );
  };

  const creatingId = editing?.mode === "create" ? createRowId : null;
  const items = virtual ? virtualizer.getVirtualItems() : null;

  return (
    <div className={styles["explorer"]}>
      <div className={styles["header"]}>
        <h2 className={styles["title"]}>{t("explorer.title")}</h2>
        <div className={styles["tools"]}>
          {canEdit && (
            <>
              <button
                type="button"
                className={styles["tool"]}
                aria-label={t("explorer.newPage")}
                title={t("explorer.newPage")}
                onClick={() => {
                  startCreate("page");
                }}
              >
                <FilePlus aria-hidden size={16} />
              </button>
              <button
                type="button"
                className={styles["tool"]}
                aria-label={t("explorer.newFolder")}
                title={t("explorer.newFolder")}
                onClick={() => {
                  startCreate("folder");
                }}
              >
                <FolderPlus aria-hidden size={16} />
              </button>
            </>
          )}
          <button
            type="button"
            className={styles["tool"]}
            aria-label={t("explorer.collapseAll")}
            title={t("explorer.collapseAll")}
            onClick={() => {
              state.update({ expanded: rootIds });
            }}
          >
            <ChevronsDownUp aria-hidden size={16} />
          </button>
        </div>
      </div>
      <label className={styles["filter"]}>
        <Search aria-hidden size={14} />
        <input
          type="text"
          placeholder={t("explorer.filter")}
          aria-label={t("explorer.filter")}
          value={filter}
          onChange={(e) => {
            setFilter(e.target.value);
          }}
        />
      </label>
      {error !== null && (
        <p role="alert" className={styles["error"]}>
          {error}
        </p>
      )}
      {cut !== null && (
        <p role="status" className={styles["status"]}>
          {t("explorer.cutHint", { name: index.byId.get(cut)?.name ?? "" })}
        </p>
      )}
      <div ref={scrollRef} className={styles["scroll"]}>
        <div
          ref={treeRef}
          role="tree"
          aria-label={t("explorer.title")}
          tabIndex={editing === null ? 0 : -1}
          aria-activedescendant={focused === null ? undefined : rowDomId(focused)}
          className={styles["tree"]}
          onKeyDown={onKeyDown}
          style={
            items === null
              ? undefined
              : { height: virtualizer.getTotalSize(), position: "relative" }
          }
        >
          {items === null
            ? rows.map(renderRow)
            : items.map((item) => {
                const row = rows[item.index];
                return row === undefined ? null : (
                  <div
                    key={row.node.id}
                    style={{ position: "absolute", top: item.start, left: 0, right: 0 }}
                  >
                    {renderRow(row)}
                  </div>
                );
              })}
        </div>
      </div>
      <span id={duplicateHintId} hidden>
        {t("explorer.duplicate")}
      </span>
      <Link
        to="/p/$projectId/trash"
        params={{ projectId }}
        className={styles["trashLink"]}
        activeProps={{ "aria-current": "page" }}
      >
        <Trash2 aria-hidden size={16} />
        {t("trash.title")}
        {(trash.data?.length ?? 0) > 0 && (
          <span className={styles["trashCount"]}>{trash.data?.length}</span>
        )}
      </Link>
      {showMembers && (
        <Link
          to="/p/$projectId/members"
          params={{ projectId }}
          className={styles["trashLink"]}
          activeProps={{ "aria-current": "page" }}
        >
          <Users aria-hidden size={16} />
          {t("members.title")}
        </Link>
      )}
      <DeleteDialog
        projectId={projectId}
        id={deleting}
        onClose={() => {
          setDeleting(null);
          treeRef.current?.focus();
        }}
        onDeleted={(id) => {
          // Showing the deleted element, or something below it: go back to the project.
          if (
            currentId !== null &&
            (currentId === id || ancestorsOf(index, currentId).some((a) => a.id === id))
          ) {
            void navigate({ to: "/p/$projectId", params: { projectId } });
          }
        }}
      />
      <section aria-labelledby={recentHeadingId} className={styles["recent"]}>
        <h2 id={recentHeadingId} className={styles["title"]}>
          {t("explorer.recent")}
        </h2>
        <RecentViewedPanel projectId={projectId} headingId={recentHeadingId} />
      </section>
      <ContextMenu
        menu={menu}
        index={index}
        cut={cut}
        onClose={() => {
          setMenu(null);
        }}
        onRestoreFocus={() => {
          // The name field appears only now: while the menu is open, it traps the focus and
          // would blur (and so commit) the field at once.
          const edit = afterMenu.current;
          afterMenu.current = null;
          if (edit === null) treeRef.current?.focus();
          else edit();
        }}
        onCreate={(kind, parentId) => {
          afterMenu.current = () => {
            startCreate(kind, parentId);
          };
        }}
        onRename={(id) => {
          afterMenu.current = () => {
            setEditing({ mode: "rename", id });
          };
        }}
        onCut={setCut}
        onPaste={paste}
        onDelete={setDeleting}
      />
    </div>
  );
}

/** Top quarter, middle, bottom quarter of the line under the pointer: before, inside, after. */
function positionOf(event: DragEvent<HTMLElement>): DropPosition {
  const box = event.currentTarget.getBoundingClientRect();
  const ratio = box.height > 0 ? (event.clientY - box.top) / box.height : 0.5;
  return ratio < 0.25 ? "before" : ratio > 0.75 ? "after" : "inside";
}

/** Pseudo ID of the line where a new element is named. */
const createRowId = "0000000000000000" as ElementId;

/** Inserts the « new element » line as the first child of its parent. */
function withCreateRow(rows: TreeRow[], editing: Editing | null, index: TreeIndex): TreeRow[] {
  if (editing?.mode !== "create") return rows;
  const at = rows.findIndex((r) => r.node.id === editing.parentId);
  if (at < 0) return rows;
  const parent = rows[at];
  if (parent === undefined) return rows;
  const placeholder: TreeRow = {
    node: {
      id: createRowId,
      kind: elementKindOf(editing.kind),
      parentId: editing.parentId,
      name: "",
      sortOrder: 0,
      duplicateName: false,
    },
    level: parent.level + 1,
    expandable: false,
    expanded: false,
    position: 1,
    siblings: (index.children.get(editing.parentId)?.length ?? 0) + 1,
  };
  return [...rows.slice(0, at + 1), placeholder, ...rows.slice(at + 1)];
}

/** Roots show the badge of the kind they hold. */
function elementKindOf(kind: CreatableKind): ElementKind {
  return kind === "property" ? "propertyDefinition" : kind;
}

function rootKindBadge(node: TreeNodeItem): TreeNodeItem["kind"] {
  if (node.kind !== "root") return node.kind;
  return node.id === roots.categories
    ? "category"
    : node.id === roots.properties
      ? "propertyDefinition"
      : node.id === roots.links
        ? "linkType"
        : "page";
}

function InlineName({
  label,
  initial,
  onCommit,
  onCancel,
}: {
  readonly label: string;
  readonly initial: string;
  readonly onCommit: (value: string) => void;
  readonly onCancel: () => void;
}) {
  const [value, setValue] = useState(initial);
  const done = useRef(false);
  return (
    <input
      aria-label={label}
      className={styles["input"]}
      value={value}
      maxLength={200}
      autoFocus
      onFocus={(e) => {
        e.currentTarget.select();
      }}
      onClick={(e) => {
        e.stopPropagation();
      }}
      onChange={(e) => {
        setValue(e.target.value);
      }}
      onKeyDown={(e) => {
        e.stopPropagation();
        if (e.key === "Enter") {
          e.preventDefault();
          done.current = true;
          onCommit(value);
        } else if (e.key === "Escape") {
          e.preventDefault();
          done.current = true;
          onCancel();
        }
      }}
      onBlur={() => {
        if (!done.current) onCommit(value);
      }}
    />
  );
}

function ContextMenu({
  menu,
  index,
  cut,
  onClose,
  onRestoreFocus,
  onCreate,
  onRename,
  onCut,
  onPaste,
  onDelete,
}: {
  readonly menu: { x: number; y: number; id: ElementId } | null;
  readonly index: TreeIndex;
  readonly cut: ElementId | null;
  readonly onClose: () => void;
  readonly onRestoreFocus: () => void;
  readonly onCreate: (kind: CreatableKind, parentId: ElementId) => void;
  readonly onRename: (id: ElementId) => void;
  readonly onCut: (id: ElementId) => void;
  readonly onPaste: (id: ElementId) => void;
  readonly onDelete: (id: ElementId) => void;
}) {
  const { t } = useTranslation();
  const node = menu === null ? undefined : index.byId.get(menu.id);
  return (
    <DropdownMenu.Root
      open={menu !== null}
      onOpenChange={(openMenu) => {
        if (!openMenu) onClose();
      }}
      modal
    >
      <DropdownMenu.Trigger asChild>
        <span
          className={styles["menuAnchor"]}
          style={menu === null ? undefined : { left: menu.x, top: menu.y }}
          aria-hidden
        />
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          className={styles["menu"]}
          align="start"
          sideOffset={2}
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            onRestoreFocus();
          }}
        >
          {node !== undefined && canContain(index, node.id, "page") && (
            <DropdownMenu.Item
              className={styles["menuItem"]}
              onSelect={() => {
                onCreate("page", node.id);
              }}
            >
              {node.kind === "page" ? t("explorer.newSubPage") : t("explorer.newPage")}
            </DropdownMenu.Item>
          )}
          {node !== undefined && canContain(index, node.id, "category") && (
            <DropdownMenu.Item
              className={styles["menuItem"]}
              onSelect={() => {
                onCreate("category", node.id);
              }}
            >
              {t("explorer.newCategory")}
            </DropdownMenu.Item>
          )}
          {node !== undefined && canContain(index, node.id, "propertyDefinition") && (
            <DropdownMenu.Item
              className={styles["menuItem"]}
              onSelect={() => {
                onCreate("property", node.id);
              }}
            >
              {t("explorer.newProperty")}
            </DropdownMenu.Item>
          )}
          {node !== undefined && canContain(index, node.id, "linkType") && (
            <DropdownMenu.Item
              className={styles["menuItem"]}
              onSelect={() => {
                onCreate("linkType", node.id);
              }}
            >
              {t("explorer.newLinkType")}
            </DropdownMenu.Item>
          )}
          {node !== undefined && canContain(index, node.id, "folder") && (
            <DropdownMenu.Item
              className={styles["menuItem"]}
              onSelect={() => {
                onCreate("folder", node.id);
              }}
            >
              {t("explorer.newFolder")}
            </DropdownMenu.Item>
          )}
          {node !== undefined && node.kind !== "root" && (
            <>
              <DropdownMenu.Item
                className={styles["menuItem"]}
                onSelect={() => {
                  onRename(node.id);
                }}
              >
                {t("explorer.rename")}
                <span className={styles["shortcut"]}>{t("help.keys.rename")}</span>
              </DropdownMenu.Item>
              <DropdownMenu.Item
                className={styles["menuItem"]}
                onSelect={() => {
                  onCut(node.id);
                }}
              >
                {t("explorer.cut")}
                <span className={styles["shortcut"]}>{t("explorer.cutShortcut")}</span>
              </DropdownMenu.Item>
              <DropdownMenu.Item
                className={styles["menuItem"]}
                onSelect={() => {
                  onDelete(node.id);
                }}
              >
                {t("trash.delete")}
                <span className={styles["shortcut"]}>{t("help.keys.delete")}</span>
              </DropdownMenu.Item>
            </>
          )}
          {node !== undefined && cut !== null && (
            <DropdownMenu.Item
              className={styles["menuItem"]}
              disabled={checkMove(index, cut, node.id) !== null}
              onSelect={() => {
                onPaste(node.id);
              }}
            >
              {t("explorer.paste")}
              <span className={styles["shortcut"]}>{t("explorer.pasteShortcut")}</span>
            </DropdownMenu.Item>
          )}
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  );
}
