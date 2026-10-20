import { infiniteQueryOptions, keepPreviousData, queryOptions } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

// Full-text search and filters (docs/architecture.md §8.1 `search`, F10). The query is built by
// features/search/searchState.ts, which owns the names of the parameters.

export type SearchResultPage = Rebrand<Schemas["SearchResultPage"]>;
export type SearchHit = SearchResultPage["hits"][number];
export type TextSegment = SearchHit["title"][number];
export type SearchIssue = SearchHit["issues"][number];

/** Query parameters of `GET search` (what `toApiQuery` produces). */
export interface SearchQuery {
  q?: string;
  nameOnly?: boolean;
  kind?: string[];
  category?: string[];
  subCategories?: boolean;
  where?: string[];
  modified?: string;
  outOfRules?: boolean;
  sort?: string;
  cursor?: string;
  limit?: number;
}

const pageSize = 25;

function fetchPage(projectId: ElementId, query: SearchQuery, signal: AbortSignal) {
  return unwrap(
    api.GET("/api/v1/projects/{projectId}/search", {
      params: { path: { projectId }, query },
      signal,
      // One repeated parameter per value (`kind=page&kind=category`), as the backend reads them.
      querySerializer: { array: { style: "form", explode: true } },
    }),
  );
}

/** A page of results (the search screen loads the next ones with the cursor). */
export function searchInfiniteQuery(projectId: ElementId, query: SearchQuery) {
  return infiniteQueryOptions({
    queryKey: queryKeys.search(projectId, query),
    queryFn: ({ pageParam, signal }) =>
      fetchPage(
        projectId,
        { ...query, limit: pageSize, ...(pageParam === undefined ? {} : { cursor: pageParam }) },
        signal,
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (last) => last.nextCursor ?? undefined,
    // Keep showing the previous results while the next ones load: typing does not blank the screen.
    placeholderData: keepPreviousData,
  });
}

/** One page, for the quick search (names only, the first results). */
export function quickSearchQuery(projectId: ElementId, text: string, limit = 8) {
  const query: SearchQuery = { q: text, nameOnly: true, limit };
  return queryOptions({
    queryKey: queryKeys.search(projectId, query),
    queryFn: ({ signal }) => fetchPage(projectId, query, signal),
    placeholderData: keepPreviousData,
  });
}
