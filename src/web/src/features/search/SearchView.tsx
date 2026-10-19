import { useInfiniteQuery } from "@tanstack/react-query";
import { useNavigate, useParams, useSearch } from "@tanstack/react-router";
import { Search, SlidersHorizontal } from "lucide-react";
import { Dialog, VisuallyHidden } from "radix-ui";
import { useEffect, useId, useMemo, useRef, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { searchInfiniteQuery } from "../../shared/api/search";
import type { ElementKind } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { useIsNarrowScreen } from "../../shared/ui/media";
import { ActivityPanel } from "./ActivityPanel";
import { FiltersPanel } from "./FiltersPanel";
import { ResultCard } from "./ResultCard";
import styles from "./SearchView.module.css";
import {
  isFiltered,
  parseSearchParams,
  resetFilters,
  searchSorts,
  toApiQuery,
  toSearchParams,
  type SearchSort,
  type SearchState,
} from "./searchState";

/** How long the text rests before it is searched (and written to the URL). */
const typingDelayMs = 250;

/**
 * The search screen (F10, mockup « Recherche »): filters on the left, results in the middle, recent activity on the
 * right. The whole state is in the URL, so a search can be shared and the browser history walks through searches.
 */
export function SearchView() {
  const { projectId } = useParams({ from: "/p/$projectId/search" });
  if (projectId === null) return null;
  return <SearchScreen projectId={projectId} />;
}

function SearchScreen({ projectId }: { readonly projectId: ElementId }) {
  const { t } = useTranslation();
  const raw = useSearch({ from: "/p/$projectId/search" });
  const navigate = useNavigate({ from: "/p/$projectId/search" });
  const state = useMemo(() => parseSearchParams(raw), [raw]);
  const sortId = useId();
  // Narrow layout (F18.R7): the filters open in a full-screen panel, to combine several before looking.
  const narrow = useIsNarrowScreen();
  const [filtersOpen, setFiltersOpen] = useState(false);

  const setState = (next: SearchState) => {
    void navigate({ to: ".", search: toSearchParams(next), replace: true });
  };

  const results = useInfiniteQuery(searchInfiniteQuery(projectId, toApiQuery(state)));
  const first = results.data?.pages[0];
  const hits = results.data?.pages.flatMap((page) => page.hits) ?? [];
  const facets = useMemo(
    () =>
      first === undefined
        ? undefined
        : new Map<ElementKind, number>(first.facets.map((f) => [f.kind, f.count])),
    [first],
  );

  if (results.isError && results.data === undefined) throw results.error;
  const total = first?.total ?? 0;
  const searched = state.q.trim() !== "";
  const filters = (
    <FiltersPanel
      projectId={projectId}
      state={state}
      facets={facets}
      onChange={setState}
      onReset={() => {
        setState(resetFilters(state));
      }}
    />
  );

  return (
    <div className={styles["screen"]}>
      <h1 className={styles["visuallyHidden"]}>{t("search.title")}</h1>
      {narrow ? (
        <Dialog.Root open={filtersOpen} onOpenChange={setFiltersOpen}>
          <Dialog.Portal>
            <Dialog.Overlay className={styles["sheetOverlay"]} />
            <Dialog.Content className={styles["sheet"]} aria-describedby={undefined}>
              <VisuallyHidden.Root asChild>
                <Dialog.Title>{t("search.filters")}</Dialog.Title>
              </VisuallyHidden.Root>
              <div className={styles["sheetBody"]}>{filters}</div>
              <div className={styles["sheetFooter"]}>
                <Button
                  variant="primary"
                  onClick={() => {
                    setFiltersOpen(false);
                  }}
                >
                  {t("search.showResults", { count: total })}
                </Button>
              </div>
            </Dialog.Content>
          </Dialog.Portal>
        </Dialog.Root>
      ) : (
        filters
      )}
      <section
        className={styles["results"]}
        aria-label={t("search.title")}
        aria-busy={results.isFetching}
      >
        <TextField
          q={state.q}
          onCommit={(q) => {
            setState({ ...state, q });
          }}
        />
        <div className={styles["resultsHead"]}>
          <div role="status">
            <h2 className={styles["total"]}>
              {first === undefined
                ? t("search.loading")
                : searched
                  ? t("search.resultsFor", { count: total, query: state.q.trim() })
                  : t("search.results", { count: total })}
            </h2>
          </div>
          {narrow && (
            <Button
              onClick={() => {
                setFiltersOpen(true);
              }}
            >
              <SlidersHorizontal aria-hidden size={16} />
              {isFiltered(state) ? t("search.filtersActive") : t("search.filters")}
            </Button>
          )}
          <label className={styles["sort"]} htmlFor={sortId}>
            {t("search.sortBy")}
            <select
              id={sortId}
              className={styles["select"]}
              value={state.sort}
              onChange={(event) => {
                setState({ ...state, sort: event.target.value as SearchSort });
              }}
            >
              {searchSorts.map((sort) => (
                <option key={sort} value={sort}>
                  {t(`search.sort.${sort}`)}
                </option>
              ))}
            </select>
          </label>
        </div>
        {results.isError && (
          <p role="alert" className={styles["error"]}>
            {errorMessage(t, results.error)}
          </p>
        )}
        {first !== undefined && hits.length === 0 && (
          <div className={styles["none"]}>
            <p className={styles["noneTitle"]}>{t("search.none.title")}</p>
            <p>{t("search.none.hint")}</p>
            {isFiltered(state) && (
              <Button
                onClick={() => {
                  setState(resetFilters(state));
                }}
              >
                {t("search.none.reset")}
              </Button>
            )}
          </div>
        )}
        {hits.length > 0 && (
          <ul className={styles["list"]} aria-label={t("search.title")}>
            {hits.map((hit) => (
              <ResultCard key={hit.id} projectId={projectId} hit={hit} />
            ))}
          </ul>
        )}
        {results.hasNextPage && (
          <Button
            className={styles["more"]}
            disabled={results.isFetchingNextPage}
            onClick={() => {
              void results.fetchNextPage();
            }}
          >
            {t("search.loadMore")}
          </Button>
        )}
      </section>
      <ActivityPanel projectId={projectId} />
    </div>
  );
}

/**
 * The search field: it follows the URL (a link, the history, the quick search) and writes to it once the typing rests,
 * or at once on Enter.
 */
function TextField({
  q,
  onCommit,
}: {
  readonly q: string;
  readonly onCommit: (q: string) => void;
}) {
  const { t } = useTranslation();
  const [text, setText] = useState(q);
  const committed = useRef(q);
  const commit = useRef(onCommit);
  useEffect(() => {
    commit.current = onCommit;
  });

  // The URL changed from elsewhere: show it.
  useEffect(() => {
    if (q !== committed.current) {
      committed.current = q;
      setText(q);
    }
  }, [q]);

  useEffect(() => {
    if (text === committed.current) return;
    const timer = setTimeout(() => {
      committed.current = text;
      commit.current(text);
    }, typingDelayMs);
    return () => {
      clearTimeout(timer);
    };
  }, [text]);

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    committed.current = text;
    commit.current(text);
  };

  return (
    <form role="search" className={styles["field"]} onSubmit={submit}>
      <Search aria-hidden size={16} />
      <input
        type="search"
        className={styles["fieldInput"]}
        aria-label={t("search.field")}
        value={text}
        autoFocus
        onChange={(event) => {
          setText(event.target.value);
        }}
      />
    </form>
  );
}
