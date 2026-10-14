import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef } from "react";
import { api } from "../../shared/api/client";
import type { ElementId } from "../../shared/ids/elementId";

export interface PageDraft {
  readonly markdown: string;
  /** Revision the edit started from. */
  readonly baseRevision: number;
  readonly savedAt: string;
}

const debounceMs = 600;

/**
 * The automatic draft of a page being edited (F05.R4), kept in the user profile through `me/ui-state` (the browser
 * stores nothing durable, decision Q7). Written shortly after each change, and right away when the page goes away.
 */
export function usePageDraft(projectId: ElementId, pageId: ElementId, now: () => Date) {
  const key = `draft-${projectId}-${pageId}`;
  const client = useQueryClient();
  const queryKey = ["me", "ui-state", key];
  const stored = useQuery({
    queryKey,
    queryFn: async ({ signal }) => {
      const { data, response } = await api.GET("/api/v1/me/ui-state/{key}", {
        params: { path: { key } },
        signal,
      });
      return response.ok ? (data as PageDraft) : null;
    },
    staleTime: Infinity,
    gcTime: 0,
  });
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const pending = useRef<PageDraft | null>(null);

  const write = useCallback(
    (draft: PageDraft, keepalive: boolean) =>
      fetch(`${globalThis.location.origin}/api/v1/me/ui-state/${key}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(draft),
        keepalive,
        credentials: "same-origin",
      }).catch(() => undefined),
    [key],
  );

  const flush = useCallback(
    (keepalive: boolean) => {
      clearTimeout(timer.current);
      const draft = pending.current;
      pending.current = null;
      if (draft === null) return Promise.resolve();
      return write(draft, keepalive).then(() => {
        client.setQueryData(queryKey, draft);
      });
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps -- queryKey derives from key
    [write, client, key],
  );

  useEffect(() => {
    const onHide = () => void flush(true);
    window.addEventListener("pagehide", onHide);
    return () => {
      window.removeEventListener("pagehide", onHide);
      void flush(true);
    };
  }, [flush]);

  /** Records the text being edited; written after a short pause. */
  const save = useCallback(
    (markdown: string, baseRevision: number) => {
      pending.current = { markdown, baseRevision, savedAt: now().toISOString() };
      clearTimeout(timer.current);
      timer.current = setTimeout(() => void flush(false), debounceMs);
    },
    [flush, now],
  );

  /** After a save, a cancel or « Ignorer »: the draft is gone. */
  const discard = useCallback(async () => {
    clearTimeout(timer.current);
    pending.current = null;
    client.setQueryData(queryKey, null);
    await api.DELETE("/api/v1/me/ui-state/{key}", { params: { path: { key } } });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- queryKey derives from key
  }, [client, key]);

  return { draft: stored.data ?? null, loaded: stored.isSuccess, save, discard };
}
