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
  /** `?edit=true` opens a page in the editor (a page just created, E2E-02-01). */
  validateSearch: (search: Record<string, unknown>): { edit?: boolean } =>
    search["edit"] === true || search["edit"] === "true" ? { edit: true } : {},
  component: ElementRoute,
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
  projectRoute.addChildren([projectIndexRoute, elementRoute]),
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
