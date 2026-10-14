import { createRootRoute, createRoute, createRouter, Outlet } from "@tanstack/react-router";
import { lazy } from "react";
import { HelloPage } from "../features/hello/HelloPage";

const rootRoute = createRootRoute({ component: Outlet });

const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: "/",
  component: HelloPage,
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

const routeTree = rootRoute.addChildren([indexRoute, ...devRoutes]);

export const router = createRouter({ routeTree });

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
