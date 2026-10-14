import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryHistory, RouterProvider } from "@tanstack/react-router";
import { render, type RenderOptions } from "@testing-library/react";
import type { ReactElement, ReactNode } from "react";
import { I18nextProvider } from "react-i18next";
import { ClockContext, type Clock } from "../shared/i18n/format";
import { testI18n } from "./i18n";

/** Default test clock: 13 October 2026, 10:00 Europe/Paris (docs/features.md §1.4). */
export const testNow = new Date("2026-10-13T08:00:00Z");
const testClock: Clock = () => testNow;

/** Renders a component with the providers the app installs (fresh query cache, no retries, frozen clock). */
export function renderWithProviders(ui: ReactElement, options?: RenderOptions) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <I18nextProvider i18n={testI18n}>
      <ClockContext value={testClock}>
        <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
      </ClockContext>
    </I18nextProvider>
  );
  return render(ui, { wrapper, ...options });
}

/** Renders the whole application at `path` (real routes, memory history). Returns the router to inspect the URL. */
export async function renderApp(path: string) {
  const { createAppRouter } = await import("../app/router");
  const router = createAppRouter(createMemoryHistory({ initialEntries: [path] }));
  const result = renderWithProviders(<RouterProvider router={router} />);
  return { ...result, router };
}
