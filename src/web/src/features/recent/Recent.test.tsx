import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderHook, screen, waitFor, within } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { viewDelayMs, type RecentActivity } from "../../shared/api/recent";
import { http, server } from "../../test/msw";
import { ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";
import { useCountView } from "./useCountView";

function mockRecent(recent: RecentActivity) {
  server.use(
    http.get("/api/v1/projects/{projectId}/me/recent", ({ response }) =>
      response(200).json(recent),
    ),
  );
}

describe("useCountView", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  function setup(initial: { enabled: boolean; elementId: typeof ids.ilsa }) {
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
    const views: unknown[] = [];
    server.use(
      http.post("/api/v1/projects/{projectId}/me/views", async ({ request, response }) => {
        views.push(await request.json());
        return response(204).empty();
      }),
    );
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );
    const hook = renderHook(
      (props: { enabled: boolean; elementId: typeof ids.ilsa }) => {
        useCountView(ids.project, props.elementId, props.enabled);
      },
      { wrapper, initialProps: initial },
    );
    return { views, ...hook };
  }

  it("counts a view only after two seconds on screen (F11.R1)", async () => {
    const { views } = setup({ enabled: true, elementId: ids.ilsa });

    await vi.advanceTimersByTimeAsync(viewDelayMs - 1);
    expect(views).toEqual([]);

    await vi.advanceTimersByTimeAsync(1);
    await vi.waitFor(() => {
      expect(views).toEqual([{ elementId: ids.ilsa }]);
    });
  });

  it("does not count an element left before two seconds", async () => {
    const { views, rerender } = setup({ enabled: true, elementId: ids.ilsa });

    await vi.advanceTimersByTimeAsync(1500);
    rerender({ enabled: true, elementId: ids.hautbois });
    await vi.advanceTimersByTimeAsync(1500);
    expect(views).toEqual([]);

    await vi.advanceTimersByTimeAsync(500);
    await vi.waitFor(() => {
      expect(views).toEqual([{ elementId: ids.hautbois }]);
    });
  });

  it("counts nothing while disabled", async () => {
    const { views } = setup({ enabled: false, elementId: ids.ilsa });

    await vi.advanceTimersByTimeAsync(viewDelayMs * 3);

    expect(views).toEqual([]);
  });

  it("counts an element once per display, not on every render", async () => {
    const { views, rerender } = setup({ enabled: true, elementId: ids.ilsa });

    rerender({ enabled: true, elementId: ids.ilsa });
    await vi.advanceTimersByTimeAsync(viewDelayMs * 3);

    await vi.waitFor(() => {
      expect(views).toHaveLength(1);
    });
  });
});

describe("Recently viewed under the tree", () => {
  it("lists the viewed elements with their type and how long ago (E2E-11-01)", async () => {
    mockProject();
    mockRecent({
      viewed: [
        {
          id: ids.categoryPnj,
          kind: "category",
          name: "PNJ",
          viewedAt: "2026-10-13T07:58:00.000Z",
        },
        { id: ids.hautbois, kind: "page", name: "Hautbois", viewedAt: "2026-10-13T06:00:00.000Z" },
      ],
      edited: [],
    });

    await renderApp(`/p/${ids.project}`);

    const heading = await screen.findByRole("heading", { name: "Récemment consultées" });
    const list = await within(heading.parentElement as HTMLElement).findByRole("list");
    const items = within(list).getAllByRole("link");
    expect(items.map((i) => i.textContent)).toEqual([
      expect.stringContaining("PNJ"),
      expect.stringContaining("Hautbois"),
    ]);
    expect(items[0]).toHaveAttribute("title", "Catégorie");
    expect(items[0]).toHaveTextContent("il y a 2 min");
    expect(items[1]).toHaveAttribute("title", "Page");
    expect(items[1]).toHaveTextContent("il y a 2 h");
  });

  it("says so when nothing was viewed yet", async () => {
    mockProject();

    await renderApp(`/p/${ids.project}`);

    await waitFor(() => {
      expect(
        screen.getByText("Les éléments consultés récemment apparaîtront ici."),
      ).toBeInTheDocument();
    });
  });
});
