import { queryOptions, useQueryClient } from "@tanstack/react-query";
import { useCallback } from "react";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

export type UserPreferences = Rebrand<Schemas["UserPreferences"]>;

export const preferencesQuery = queryOptions({
  queryKey: queryKeys.preferences,
  queryFn: ({ signal }) => unwrap(api.GET("/api/v1/me/preferences", { signal })),
  staleTime: Infinity,
});

/**
 * Saves a change of the user's preferences (F15.R3): merged into the stored ones, so that changing the theme never
 * resets the language. The caller applies the change at once; this only remembers it for the next launch.
 */
export function useSavePreferences() {
  const client = useQueryClient();
  return useCallback(
    async (change: Partial<UserPreferences>) => {
      const current = await client.query(preferencesQuery);
      const saved = await unwrap(
        api.PUT("/api/v1/me/preferences", { body: { ...current, ...change } }),
      );
      client.setQueryData(queryKeys.preferences, saved);
      return saved;
    },
    [client],
  );
}
