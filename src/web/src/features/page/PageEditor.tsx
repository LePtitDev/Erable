import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useBlocker, useNavigate } from "@tanstack/react-router";
import { Bold, Code, FileText, Heading, Italic, List, Paperclip } from "lucide-react";
import {
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
} from "react";
import { useTranslation } from "react-i18next";
import { api, unwrap } from "../../shared/api/client";
import { errorCode, errorMessage } from "../../shared/api/errors";
import { mergeConflictOf, type MergeConflict } from "../../shared/api/mergeConflict";
import { useNoticeStore } from "../../shared/ui/notices";
import { usePresenceStore } from "../../shared/realtime/presenceStore";
import { queryKeys } from "../../shared/api/keys";
import {
  pageContentQuery,
  pageQuery,
  resolveQuery,
  savePageContent,
  type PageStructure,
  type PageView as PageData,
} from "../../shared/api/pages";
import type { ElementDetails } from "../../shared/api/tree";
import { ClockContext, useFormatters } from "../../shared/i18n/format";
import { parseElementId, type ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import { insertText, setHeading, toggleInline } from "../editor/formatting";
import { MarkdownEditor, type MarkdownEditorHandle } from "../editor/MarkdownEditor";
import type { PillTarget } from "../editor/pills";
import { usePageDraft } from "../editor/usePageDraft";
import { PalettePopup } from "../palette/PalettePopup";
import { useInsertPalette } from "../palette/useInsertPalette";
import { approveClose, useWindowStore } from "../../shared/native/native";
import { Breadcrumb } from "../../shared/ui/Breadcrumb";
import { useShellStore } from "../project/shellStore";
import { EditingBanner } from "../presence/Presence";
import { ConflictResolution, type Resolution } from "./ConflictResolution";
import { sectionAt } from "../editor/section";
import { CategoryChips } from "../properties/PagePropertiesView";
import styles from "./PageEditor.module.css";
import { PageStructurePanel } from "./PageStructurePanel";
import { structureOf, usePageStructure } from "./usePageStructure";
import { useEditorUploads } from "./useEditorUploads";

interface PageEditorProps {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
  /** Focus and select the title (a page just created, E2E-02-01). */
  readonly focusTitle: boolean;
}

/** Editing a page (F05, mockup « Page — édition »). Loads the content, then hands it to {@link EditorScreen}. */
export function PageEditor({ projectId, element, focusTitle }: PageEditorProps) {
  const { t } = useTranslation();
  const content = useQuery(pageContentQuery(projectId, element.id));
  // The categories and values come with the page view: wait for a fresh copy, the editor keeps its own draft after.
  const page = useQuery({ ...pageQuery(projectId, element.id), refetchOnMount: "always" });
  const now = useContext(ClockContext);
  const draft = usePageDraft(projectId, element.id, now);
  if (!content.isSuccess || !page.isFetchedAfterMount || !page.isSuccess || !draft.loaded) {
    return <p aria-busy="true">{t("common.loading")}</p>;
  }
  return (
    <EditorScreen
      key={element.id}
      projectId={projectId}
      element={element}
      focusTitle={focusTitle}
      saved={content.data.markdown}
      revision={content.data.revision}
      draft={draft}
      page={page.data}
    />
  );
}

/** Accessible shortcut of « Enregistrer » (key names of the ARIA specification, not displayed). */
const saveKeys = "Control+S";
const idPattern = /\[\[([0-9A-Fa-f]{16})/g;

function EditorScreen({
  projectId,
  element,
  focusTitle,
  saved: initialSaved,
  revision: initialRevision,
  draft,
  page,
}: PageEditorProps & {
  readonly saved: string;
  readonly revision: number;
  readonly draft: ReturnType<typeof usePageDraft>;
  readonly page: PageData;
}) {
  const { t } = useTranslation();
  const format = useFormatters();
  const client = useQueryClient();
  const navigate = useNavigate();
  const editor = useRef<MarkdownEditorHandle>(null);
  const palette = useInsertPalette(projectId, editor);
  const uploads = useEditorUploads(projectId, element.id);
  const extensions = useMemo(
    () => [palette.extension, uploads.extension],
    [palette.extension, uploads.extension],
  );
  const filePicker = useRef<HTMLInputElement>(null);
  const setMode = useShellStore((s) => s.setMode);
  const setCursor = useShellStore((s) => s.setCursor);
  const setSection = useShellStore((s) => s.setSection);
  const cursorLine = useShellStore((s) => s.cursor?.line ?? null);

  const [saved, setSaved] = useState({
    markdown: initialSaved,
    revision: initialRevision,
    name: element.name,
  });
  const [doc, setDoc] = useState(initialSaved);
  const [title, setTitle] = useState(element.name);
  const [rawIds, setRawIds] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  // A save that could not be merged: both versions of the passages in conflict, to settle (M4-05).
  const [conflict, setConflict] = useState<MergeConflict | null>(null);
  const notify = useNoticeStore((n) => n.push);
  const remoteChange = useShellStore((st) => st.remoteChange);
  const presence = usePresenceStore((p) => p.users);
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [offerDraft, setOfferDraft] = useState(
    draft.draft !== null && draft.draft.markdown !== initialSaved,
  );
  const [draftSavedAt, setDraftSavedAt] = useState<Date | null>(null);
  const leaving = useRef(false);

  const structure = usePageStructure(projectId, element.id, page);
  const dirty = doc !== saved.markdown || title.trim() !== saved.name || structure.dirty;

  // F05.R6: the status bar counts the validation errors of the values, never blocking the save.
  const setValidationErrors = useShellStore((s) => s.setValidationErrors);
  useEffect(() => {
    setValidationErrors(structure.errors);
  }, [structure.errors, setValidationErrors]);
  useEffect(
    () => () => {
      setValidationErrors(0);
    },
    [setValidationErrors],
  );

  // F16.R3: the desktop shell asks before closing the window while there are unsaved changes.
  const setUnsavedChanges = useWindowStore((s) => s.setUnsavedChanges);
  useEffect(() => {
    setUnsavedChanges(dirty);
  }, [dirty, setUnsavedChanges]);
  useEffect(
    () => () => {
      setUnsavedChanges(false);
    },
    [setUnsavedChanges],
  );
  const closeRequests = useWindowStore((s) => s.closeRequests);
  const [confirmClose, setConfirmClose] = useState(false);
  const handledCloseRequests = useRef(closeRequests);
  useEffect(() => {
    if (closeRequests === handledCloseRequests.current) return;
    handledCloseRequests.current = closeRequests;
    setConfirmClose(true);
  }, [closeRequests]);

  useEffect(() => {
    setMode("edit", element.id);
    return () => {
      setMode("read");
    };
  }, [setMode, element.id]);

  // The section under the cursor is told to the others (« modifie aussi cette page — section « Histoire » »): a short
  // delay, so that moving about the text does not flood the network.
  useEffect(() => {
    const timer = setTimeout(() => {
      setSection(cursorLine === null ? null : sectionAt(doc, cursorLine));
    }, 300);
    return () => {
      clearTimeout(timer);
    };
  }, [doc, cursorLine, setSection]);

  // Resolve the names of every ID of the text, in one call, refreshed when the set of IDs changes.
  const ids = useMemo(() => {
    const found = new Set<ElementId>();
    for (const m of doc.matchAll(idPattern)) {
      const id = parseElementId(m[1] ?? "");
      if (id !== null) found.add(id);
    }
    return [...found];
  }, [doc]);
  const resolved = useQuery({
    ...resolveQuery(projectId, ids),
    placeholderData: (previous) => previous,
  });
  const targets = useMemo(() => {
    const map = new Map<ElementId, PillTarget>();
    for (const [id, item] of resolved.data ?? [])
      map.set(id, { name: item.name, kind: item.kind, state: item.state });
    return map;
  }, [resolved.data]);

  const onChange = useCallback(
    (next: string) => {
      setDoc(next);
      setError(null);
      if (next !== saved.markdown) {
        draft.save(next, saved.revision);
        setDraftSavedAt(new Date());
      }
    },
    [draft, saved],
  );

  const finish = useCallback(() => {
    leaving.current = true;
    void navigate({
      to: "/p/$projectId/e/$elementId",
      params: { projectId, elementId: element.id },
      search: {},
    });
  }, [navigate, projectId, element.id]);

  /**
   * Saves a page against a revision (If-Match), then the title; true when everything is saved. A save that arrives
   * after another one is merged by the server (« Fusionné avec les modifications de … »); when the two edits touch the
   * same passages nothing is written and the resolution view opens with both versions (M4-05, F14.R5).
   */
  const persist = useCallback(
    async (plan: {
      readonly markdown: string;
      readonly revision: number;
      readonly structure: PageStructure | undefined;
      readonly force: boolean;
    }): Promise<boolean> => {
      // Placeholders of running uploads are not text: saving now would lose their `![[ID]]`.
      if (saving || uploads.pending > 0) return false;
      setSaving(true);
      setError(null);
      try {
        let revision = plan.revision;
        let text = plan.markdown;
        if (plan.force || plan.markdown !== saved.markdown || plan.structure !== undefined) {
          const result = await savePageContent(
            projectId,
            element.id,
            plan.markdown,
            revision,
            plan.structure,
          );
          revision = result.revision;
          if (result.merged) {
            text = result.merged.markdown;
            notify(
              t("conflict.merged", {
                names: result.merged.mergedWith.map((a) => a.name).join(", "),
              }),
            );
          }
          structure.markSaved();
        }
        const name = title.trim();
        if (name !== "" && name !== saved.name) {
          revision = (
            await unwrap(
              api.PATCH("/api/v1/projects/{projectId}/elements/{id}", {
                params: { path: { projectId, id: element.id } },
                body: { name },
              }),
            )
          ).revision;
        }
        setSaved({ markdown: text, revision, name: name === "" ? saved.name : name });
        setConflict(null);
        useShellStore.getState().setRemoteChange(null);
        await draft.discard();
        await client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });
        return true;
      } catch (e) {
        const refused = mergeConflictOf(e);
        if (refused !== null) {
          setConflict(refused);
        } else {
          setError(
            errorCode(e) === "page.revisionConflict" ? t("editor.conflict") : errorMessage(t, e),
          );
        }
        return false;
      } finally {
        setSaving(false);
      }
    },
    [
      saving,
      uploads.pending,
      saved,
      title,
      structure,
      projectId,
      element.id,
      draft,
      client,
      notify,
      t,
    ],
  );

  const save = useCallback(
    () =>
      persist({
        markdown: doc,
        revision: saved.revision,
        structure: structure.dirty ? structureOf(structure.draft) : undefined,
        force: false,
      }),
    [persist, doc, saved.revision, structure],
  );

  const saveAndClose = useCallback(async () => {
    if (await save()) finish();
  }, [save, finish]);

  /** The user settled every passage in conflict: the resolved page is saved against the revision it conflicted with. */
  const saveResolved = useCallback(
    async (resolution: Resolution) => {
      const settled =
        resolution.categories === null || resolution.properties === null
          ? undefined
          : {
              categories: [...resolution.categories],
              properties: resolution.properties.map((p) => ({
                propertyId: p.propertyId,
                value: p.value,
              })),
            };
      if (
        await persist({
          markdown: resolution.markdown,
          revision: resolution.revision,
          structure: settled,
          force: true,
        })
      ) {
        finish();
      }
    },
    [persist, finish],
  );

  // F05.R5: leaving with unsaved changes asks first (in-app navigation and closing the tab).
  const blocker = useBlocker({
    shouldBlockFn: () => dirty && !leaving.current,
    enableBeforeUnload: () => dirty && !leaving.current,
    withResolver: true,
  });

  const onKeyDown = (event: KeyboardEvent) => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
      event.preventDefault();
      void saveAndClose();
    }
  };

  const tool = (
    label: string,
    icon: React.ReactNode,
    run: Parameters<MarkdownEditorHandle["run"]>[0],
  ) => (
    <button
      type="button"
      className={styles["tool"]}
      aria-label={label}
      title={label}
      onMouseDown={(e) => {
        e.preventDefault();
      }}
      onClick={() => {
        editor.current?.run(run);
      }}
    >
      {icon}
    </button>
  );

  return (
    <div className={styles["editor"]} onKeyDown={onKeyDown}>
      <header className={styles["header"]}>
        <Breadcrumb projectId={projectId} path={element.path} current={saved.name} />
        <div className={styles["titleRow"]}>
          <input
            className={styles["title"]}
            aria-label={t("editor.title")}
            value={title}
            maxLength={200}
            autoFocus={focusTitle}
            onFocus={(e) => {
              if (focusTitle) e.currentTarget.select();
            }}
            onChange={(e) => {
              setTitle(e.target.value);
            }}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                editor.current?.focus();
              }
            }}
          />
          <span className={styles["chip"]}>{t("shell.mode.edit")}</span>
          <CategoryChips projectId={projectId} categories={structure.categories} />
          <span className={styles["draftState"]} role="status">
            {draftSavedAt !== null && dirty
              ? t("editor.draftSaved", { when: format.relative(draftSavedAt) })
              : ""}
          </span>
          <Button
            onClick={() => {
              if (dirty) setConfirmCancel(true);
              else finish();
            }}
          >
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            onClick={() => void saveAndClose()}
            disabled={saving || uploads.pending > 0}
            aria-keyshortcuts={saveKeys}
          >
            {t("editor.save")}
            <kbd className={styles["kbd"]}>{t("editor.saveShortcut")}</kbd>
          </Button>
        </div>
      </header>

      <EditingBanner elementId={element.id} />
      {remoteChange !== null && (
        <p role="status" className={styles["banner"]}>
          {t("conflict.remoteChanged", {
            names: presence.find((u) => u.userId === remoteChange)?.name ?? remoteChange,
          })}
        </p>
      )}
      {conflict !== null && (
        <ConflictResolution
          projectId={projectId}
          conflict={conflict}
          busy={saving}
          onResolve={(resolution) => void saveResolved(resolution)}
          onCancel={() => {
            setConflict(null);
          }}
        />
      )}
      {offerDraft && draft.draft !== null && (
        <div role="alert" className={styles["banner"]}>
          <span>
            {t("editor.draftFound", { when: format.relative(new Date(draft.draft.savedAt)) })}
          </span>
          <Button
            size="sm"
            variant="primary"
            onClick={() => {
              const markdown = draft.draft?.markdown ?? doc;
              editor.current?.run((state) => ({
                changes: { from: 0, to: state.doc.length, insert: markdown },
              }));
              setOfferDraft(false);
            }}
          >
            {t("editor.restore")}
          </Button>
          <Button
            size="sm"
            onClick={() => {
              void draft.discard();
              setOfferDraft(false);
            }}
          >
            {t("editor.ignore")}
          </Button>
        </div>
      )}

      {error !== null && (
        <p role="alert" className={styles["error"]}>
          {error}
        </p>
      )}
      {uploads.errors.length > 0 && (
        <div role="alert" className={styles["error"]}>
          {uploads.errors.map((message) => (
            <p key={message}>{message}</p>
          ))}
          <Button size="sm" onClick={uploads.dismissErrors}>
            {t("common.close")}
          </Button>
        </div>
      )}

      <div role="toolbar" aria-label={t("editor.toolbar")} className={styles["toolbar"]}>
        {tool(t("editor.heading"), <Heading aria-hidden size={16} />, (s) => setHeading(s, 2))}
        {tool(t("editor.bold"), <Bold aria-hidden size={16} />, (s) => toggleInline(s, "**"))}
        {tool(t("editor.italic"), <Italic aria-hidden size={16} />, (s) => toggleInline(s, "*"))}
        {tool(t("editor.list"), <List aria-hidden size={16} />, (s) => insertText(s, "- "))}
        {tool(t("editor.code"), <Code aria-hidden size={16} />, (s) => toggleInline(s, "`"))}
        <span className={styles["separator"]} aria-hidden />
        <button
          type="button"
          className={styles["textTool"]}
          onMouseDown={(e) => {
            e.preventDefault();
          }}
          onClick={() => {
            // Typed like the user would: the palette opens (F05.R3).
            editor.current?.run((s) => ({ ...insertText(s, "[["), userEvent: "input.type" }));
          }}
        >
          <FileText aria-hidden size={16} />
          {t("editor.reference")}
        </button>
        <button
          type="button"
          className={styles["textTool"]}
          onMouseDown={(e) => {
            e.preventDefault();
          }}
          onClick={() => filePicker.current?.click()}
        >
          <Paperclip aria-hidden size={16} />
          {t("editor.attachment")}
        </button>
        <input
          ref={filePicker}
          type="file"
          multiple
          hidden
          onChange={(e) => {
            uploads.insertFiles(editor.current?.view ?? null, [...(e.target.files ?? [])]);
            e.target.value = "";
          }}
        />
        <span className={styles["toolbarEnd"]}>
          <label className={styles["rawIds"]}>
            <input
              type="checkbox"
              checked={rawIds}
              onChange={(e) => {
                setRawIds(e.target.checked);
              }}
            />
            {t("editor.rawIds")}
          </label>
          <span className={styles["hint"]}>
            {t("editor.insertHint")}{" "}
            <kbd className={styles["kbd"]}>{t("help.keys.paletteShortcut")}</kbd>
          </span>
        </span>
      </div>

      <div className={styles["body"]}>
        <section className={styles["surface"]} aria-label={t("editor.content")}>
          <MarkdownEditor
            ref={editor}
            initial={initialSaved}
            label={t("editor.content")}
            placeholderText={t("editor.placeholder")}
            targets={targets}
            rawIds={rawIds}
            onChange={onChange}
            onCursor={(line, column) => {
              setCursor({ line, column });
            }}
            onSave={() => void saveAndClose()}
            onUpdate={palette.onUpdate}
            extensions={extensions}
          />
          {palette.popup !== null && <PalettePopup {...palette.popup} />}
        </section>
        <PageStructurePanel
          projectId={projectId}
          pageId={element.id}
          properties={structure.properties}
          draft={structure.draft}
          onDraft={structure.setDraft}
          attachments={page.attachments}
        />
      </div>

      <Dialog
        open={confirmCancel}
        onOpenChange={setConfirmCancel}
        alert
        size="sm"
        title={t("editor.cancelTitle")}
        description={t("editor.cancelDescription")}
        footer={
          <>
            <Button
              onClick={() => {
                setConfirmCancel(false);
              }}
            >
              {t("editor.keepEditing")}
            </Button>
            <Button
              variant="danger"
              onClick={() => {
                setConfirmCancel(false);
                void draft.discard().then(finish);
              }}
            >
              {t("editor.discard")}
            </Button>
          </>
        }
      />

      <Dialog
        open={confirmClose}
        onOpenChange={setConfirmClose}
        alert
        size="sm"
        title={t("editor.closeTitle")}
        description={t("editor.closeDescription")}
        footer={
          <>
            <Button
              onClick={() => {
                setConfirmClose(false);
              }}
            >
              {t("common.cancel")}
            </Button>
            <Button
              variant="danger"
              onClick={() => {
                setConfirmClose(false);
                leaving.current = true;
                void draft.discard().then(approveClose);
              }}
            >
              {t("editor.discard")}
            </Button>
            <Button
              variant="primary"
              onClick={() => {
                void save().then((ok) => {
                  setConfirmClose(false);
                  if (ok) void approveClose();
                });
              }}
            >
              {t("editor.save")}
            </Button>
          </>
        }
      />

      <Dialog
        open={blocker.status === "blocked"}
        onOpenChange={(open) => {
          if (!open) blocker.reset?.();
        }}
        alert
        size="sm"
        title={t("editor.leaveTitle")}
        description={t("editor.leaveDescription")}
        footer={
          <>
            <Button onClick={() => blocker.reset?.()}>{t("common.cancel")}</Button>
            <Button
              variant="danger"
              onClick={() => {
                leaving.current = true;
                void draft.discard().then(() => blocker.proceed?.());
              }}
            >
              {t("editor.discard")}
            </Button>
            <Button
              variant="primary"
              onClick={() => {
                void save().then((ok) => {
                  if (ok) {
                    leaving.current = true;
                    blocker.proceed?.();
                  } else {
                    blocker.reset?.();
                  }
                });
              }}
            >
              {t("editor.save")}
            </Button>
          </>
        }
      />
    </div>
  );
}
