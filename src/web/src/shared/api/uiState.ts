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

/**
 * A small document of interface state (panel sizes, expanded nodes…) stored in the user profile through the API,
 * never in the browser (decision Q7). Updates apply at once and are written back debounced.
 */
export function useUiState<T extends object>(key: string, fallback: T) {
  const client = useQueryClient();
  const queryKey = ["me", "ui-state", key];
  const query = useQuery({
    queryKey,
    queryFn: ({ signal }) => readUiState(key, fallback, signal),
    staleTime: Infinity,
  });
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(
    () => () => {
      clearTimeout(timer.current);
    },
    [],
  );

  const update = useCallback(
    (change: Partial<T> | ((current: T) => Partial<T>)) => {
      const current = client.getQueryData<T>(queryKey) ?? fallback;
      const next = { ...current, ...(typeof change === "function" ? change(current) : change) };
      client.setQueryData(queryKey, next);
      clearTimeout(timer.current);
      timer.current = setTimeout(() => {
        void api.PUT("/api/v1/me/ui-state/{key}", {
          params: { path: { key } },
          body: next as never,
        });
      }, debounceMs);
    },
    // queryKey is derived from key
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [client, key],
  );

  return { value: query.data ?? fallback, loaded: query.isSuccess, update };
}
