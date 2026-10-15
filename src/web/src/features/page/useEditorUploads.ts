import { EditorView } from "@codemirror/view";
import { useCallback, useEffect, useMemo, useRef, useState, type RefObject } from "react";
import { useTranslation } from "react-i18next";
import type { ElementId } from "../../shared/ids/elementId";
import {
  addUpload,
  completeUpload,
  progressUpload,
  removeUpload,
  uploads,
} from "../editor/uploads";
import { useUploader } from "./useUploader";

let nextKey = 0;

type Start = (view: EditorView, files: readonly File[], pos: number) => void;

/** Paste and drop of files, calling the latest `start` (read when files arrive, never while rendering). */
function dropHandlers(start: RefObject<Start>) {
  return EditorView.domEventHandlers({
    paste(event, view) {
      const files = [...(event.clipboardData?.files ?? [])];
      if (files.length === 0) return false;
      event.preventDefault();
      start.current(view, files, view.state.selection.main.head);
      return true;
    },
    drop(event, view) {
      const files = [...(event.dataTransfer?.files ?? [])];
      if (files.length === 0) return false;
      event.preventDefault();
      const pos =
        view.posAtCoords({ x: event.clientX, y: event.clientY }) ?? view.state.selection.main.head;
      start.current(view, files, pos);
      return true;
    },
  });
}

/**
 * Pasting or dropping files in the editor (F06.R1, E2E-06-01/02): each file is uploaded with a progress placeholder
 * at the cursor (or the drop point), then `![[ID]]` takes its place. A failed upload removes its placeholder and
 * leaves the text untouched. Leaving the editor cancels the uploads still running.
 */
export function useEditorUploads(projectId: ElementId, pageId: ElementId) {
  const { t } = useTranslation();
  const upload = useUploader(projectId, pageId);
  const [pending, setPending] = useState(0);
  const [errors, setErrors] = useState<readonly string[]>([]);
  const lifetime = useRef(new AbortController());

  useEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    return () => {
      controller.abort();
    };
  }, []);

  const start = useCallback<Start>(
    (view, files, pos) => {
      const signal = lifetime.current.signal;
      setErrors([]);
      for (const file of files) {
        const key = `upload-${String(nextKey++)}`;
        view.dispatch({ effects: addUpload.of({ key, pos, name: file.name }) });
        setPending((n) => n + 1);
        void upload(
          file,
          (fraction) => {
            if (!signal.aborted) view.dispatch({ effects: progressUpload.of({ key, fraction }) });
          },
          signal,
        ).then((result) => {
          if (signal.aborted) return;
          setPending((n) => n - 1);
          if (result.ok) {
            const spec = completeUpload(view.state, key, result.attachment.id, files.length > 1);
            if (spec !== null) view.dispatch(spec);
          } else {
            view.dispatch({ effects: removeUpload.of(key) });
            const message = result.message;
            if (message !== null) setErrors((list) => [...list, message]);
          }
        });
      }
    },
    [upload],
  );

  const startRef = useRef(start);
  useEffect(() => {
    startRef.current = start;
  }, [start]);

  // The ref is only read when files are pasted or dropped (see dropHandlers), never while rendering.
  // eslint-disable-next-line react-hooks/refs
  const [handlers] = useState(() => dropHandlers(startRef));
  const extension = useMemo(
    () => [uploads((name) => t("attachments.uploading", { name })), handlers],
    [t, handlers],
  );

  /** Files chosen with the toolbar button: inserted at the cursor. */
  const insertFiles = useCallback(
    (view: EditorView | null, files: readonly File[]) => {
      if (view === null || files.length === 0) return;
      start(view, files, view.state.selection.main.head);
    },
    [start],
  );

  return {
    extension,
    pending,
    errors,
    dismissErrors: useCallback(() => {
      setErrors([]);
    }, []),
    insertFiles,
  };
}
