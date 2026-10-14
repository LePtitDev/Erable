import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useCallback, useEffect, useRef } from "react";
import { api, ApiError } from "./client";

const debounceMs = 400;

async function readUiState<T>(key: string, fallback: T, signal: AbortSignal): Promise<T> {
  const { data, response } = await api.GET("/api/v1/me/ui-state/{key}", {
    params: { path: { key } },
    signal,
  });
  if (response.status === 404) return fallback;
  if (!response.ok) throw new ApiError(response.status, undefined);
  return { ...fallback, ...(data as Partial<T>) };
}

/** Writes a document; `keepalive` lets the request finish while the page unloads. */
function writeUiState(key: string, value: unknown, keepalive = false) {
  void fetch(`${globalThis.location.origin}/api/v1/me/ui-state/${encodeURIComponent(key)}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(value),
    keepalive,
    credentials: "same-origin",
  }).catch(() => undefined);
}

/**
 * A small document of interface state (panel sizes, expanded nodes…) stored in the user profile through the API,
 * never in the browser (decision Q7). Updates apply at once and are written back debounced; a pending write is
 * sent right away when the page goes away (reload, closed tab) or the component unmounts.
 */
export function useUiState<T extends object>(key: string, fallback: T) {
  const client = useQueryClient();
  const query = useQuery({
    queryKey: ["me", "ui-state", key],
    queryFn: ({ signal }) => readUiState(key, fallback, signal),
    staleTime: Infinity,
  });
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const pending = useRef<{ value: T } | null>(null);

  const flush = useCallback(
    (keepalive: boolean) => {
      clearTimeout(timer.current);
      if (pending.current !== null) {
        writeUiState(key, pending.current.value, keepalive);
        pending.current = null;
      }
    },
    [key],
  );

  useEffect(() => {
    const onHide = () => {
      flush(true);
    };
    window.addEventListener("pagehide", onHide);
    return () => {
      window.removeEventListener("pagehide", onHide);
      flush(true);
    };
  }, [flush]);

  const update = useCallback(
    (change: Partial<T> | ((current: T) => Partial<T>)) => {
      const queryKey = ["me", "ui-state", key];
      const current = client.getQueryData<T>(queryKey) ?? fallback;
      const next = { ...current, ...(typeof change === "function" ? change(current) : change) };
      client.setQueryData(queryKey, next);
      pending.current = { value: next };
      clearTimeout(timer.current);
      timer.current = setTimeout(() => {
        flush(false);
      }, debounceMs);
    },
    // The fallback is a constant default; only the key identifies the document.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [client, key, flush],
  );

  return { value: query.data ?? fallback, loaded: query.isSuccess, update };
}
