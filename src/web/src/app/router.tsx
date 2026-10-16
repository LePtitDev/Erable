import {
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  type RouterHistory,
} from "@tanstack/react-router";
import { lazy } from "react";
import { HomeScreen } from "../features/home/HomeScreen";
import { ProjectShell } from "../features/project/ProjectShell";
import { ProjectIndex } from "../features/project/ProjectIndex";
import { ElementRoute } from "../features/project/ElementRoute";
import { SearchView } from "../features/search/SearchView";
import {
  parseSearchParams,
  toSearchParams,
  type SearchParams,
} from "../features/search/searchState";
import { TrashView } from "../features/trash/TrashView";
import { NotFoundScreen, ErrorScreen } from "../features/project/ErrorScreens";
import { parseElementId, type ElementId } from "../shared/ids/elementId";

const rootRoute = createRootRoute({
  component: Outlet,
  notFoundComponent: () => <NotFoundScreen />,
  errorComponent: ErrorScreen,
});

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: HomeScreen,
});

/** An ID in the URL is validated once here: components receive the branded type (docs/architecture.md §4). */
function idParam(value: string): ElementId | null {
  return parseElementId(value);
}

export const projectRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/p/$projectId",
  params: {
    parse: (raw) => ({ projectId: idParam(raw.projectId) }),
    stringify: (params) => ({ projectId: params.projectId ?? "" }),
  },
  component: ProjectShell,
});

export const projectIndexRoute = createRoute({
  getParentRoute: () => projectRoute,
  path: "/",
  component: ProjectIndex,
});

export const elementRoute = createRoute({
  getParentRoute: () => projectRoute,
  path: "/e/$elementId",
  params: {
    parse: (raw) => ({ elementId: idParam(raw.elementId) }),
    stringify: (params) => ({ elementId: params.elementId ?? "" }),
  },
  /** `?edit=true` opens a page in the editor; `&focus=title` selects its title (a page just created, E2E-02-01). */
  validateSearch: (search: Record<string, unknown>): { edit?: boolean; focus?: "title" } => ({
    ...(search["edit"] === true || search["edit"] === "true" ? { edit: true } : {}),
    ...(search["focus"] === "title" ? { focus: "title" as const } : {}),
  }),
  component: ElementRoute,
});

/** The trash of the project (F12). */
export const trashRoute = createRoute({
  getParentRoute: () => projectRoute,
  path: "/trash",
  component: TrashView,
});

/**
 * The search screen (F10): its whole state is in the URL, so that a search is a link (F10.R4). Whatever the URL
 * says is read, then written back in its short form: a link with an unknown parameter opens a valid search.
 */
export const searchRoute = createRoute({
  getParentRoute: () => projectRoute,
  path: "/search",
  validateSearch: (search: Record<string, unknown>): SearchParams =>
    toSearchParams(parseSearchParams(search)),
  component: SearchView,
});

// Development-only pages: the condition is statically false in production builds, so they are tree-shaken.
const devRoutes = import.meta.env.DEV
  ? [
      createRoute({
        getParentRoute: () => rootRoute,
        path: "/_dev/tokens",
        component: lazy(() => import("../dev/TokensPage").then((m) => ({ default: m.TokensPage }))),
      }),
    ]
  : [];

const routeTree = rootRoute.addChildren([
  indexRoute,
  projectRoute.addChildren([projectIndexRoute, trashRoute, searchRoute, elementRoute]),
  ...devRoutes,
]);

/** The application router; tests give it a memory history. */
export function createAppRouter(history?: RouterHistory) {
  return createRouter({ routeTree, scrollRestoration: true, ...(history ? { history } : {}) });
}

export const router = createAppRouter();

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
