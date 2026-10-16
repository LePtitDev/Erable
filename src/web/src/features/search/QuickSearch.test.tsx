import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import type { SearchHit } from "../../shared/api/search";
import { http, server } from "../../test/msw";
import { ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";

function hit(id: SearchHit["id"], name: string): SearchHit {
  return {
    id,
    kind: "page",
    name,
    path: [
      { id: ids.lieux, kind: "root", name: "Pages" },
      { id: ids.lieux, kind: "folder", name: "Lieux" },
    ],
    title: [{ text: name, hit: false }],
    excerpt: [],
    categories: [],
    updatedAt: "2026-10-13T06:00:00.000Z",
    updatedBy: "camille",
    updatedByName: "Camille R.",
    issues: [],
    attachment: null,
  };
}

function mockNames(hits: SearchHit[]) {
  const requests: URLSearchParams[] = [];
  server.use(
    http.get("/api/v1/projects/{projectId}/search", ({ request, response }) => {
      requests.push(new URL(request.url).searchParams);
      return response(200).json({ hits, total: hits.length, facets: [], nextCursor: null });
    }),
  );
  return requests;
}

describe("QuickSearch", () => {
  it("opens with Ctrl+K, searches names, moves with the arrows and opens with Enter (E2E-10-01)", async () => {
    mockProject();
    const requests = mockNames([hit(ids.ilsa, "Ilsa Varenne"), hit(ids.orsen, "Capitaine Orsen")]);
    const user = userEvent.setup();
    const { router } = await renderApp(`/p/${ids.project}`);
    await screen.findByRole("tree", { name: "Explorateur" });

    await user.keyboard("{Control>}k{/Control}");
    const field = await screen.findByRole("combobox", { name: "Aller à un élément par son nom" });
    expect(field).toHaveFocus();
    await user.type(field, "ilsa");

    const options = await screen.findAllByRole("option");
    expect(options.map((o) => o.textContent)).toEqual([
      expect.stringContaining("Ilsa Varenne"),
      expect.stringContaining("Capitaine Orsen"),
    ]);
    expect(options[0]).toHaveAttribute("aria-selected", "true");
    expect(field).toHaveAttribute("aria-activedescendant", options[0]?.id);
    expect((requests.at(-1) as URLSearchParams).get("nameOnly")).toBe("true");
    expect(await screen.findByRole("status")).toHaveTextContent("2 résultats");

    await user.keyboard("{ArrowDown}");
    expect(options[1]).toHaveAttribute("aria-selected", "true");
    await user.keyboard("{ArrowDown}");
    expect(options[0]).toHaveAttribute("aria-selected", "true");
    await user.keyboard("{ArrowUp}{Enter}");

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/p/${ids.project}/e/${ids.orsen}`);
    });
    expect(screen.queryByRole("dialog", { name: "Recherche rapide" })).not.toBeInTheDocument();
  });

  it("closes with Escape and starts empty the next time", async () => {
    mockProject();
    mockNames([hit(ids.ilsa, "Ilsa Varenne")]);
    const user = userEvent.setup();
    await renderApp(`/p/${ids.project}`);
    await screen.findByRole("tree", { name: "Explorateur" });

    await user.keyboard("{Control>}k{/Control}");
    await user.type(await screen.findByRole("combobox"), "ils");
    await user.keyboard("{Escape}");
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    await user.keyboard("{Control>}k{/Control}");

    expect(await screen.findByRole("combobox")).toHaveValue("");
  });

  it("offers the recently viewed elements when the field is empty", async () => {
    mockProject();
    const user = userEvent.setup();
    server.use(
      http.get("/api/v1/projects/{projectId}/me/recent", ({ response }) =>
        response(200).json({
          viewed: [
            {
              id: ids.hautbois,
              kind: "page",
              name: "Hautbois",
              viewedAt: "2026-10-13T07:58:00.000Z",
            },
          ],
          edited: [],
        }),
      ),
    );
    await renderApp(`/p/${ids.project}`);

    await user.click(await screen.findByRole("button", { name: /Rechercher/ }));

    const dialog = await screen.findByRole("dialog", { name: "Recherche rapide" });
    expect(await within(dialog).findByRole("option", { name: /Hautbois/ })).toBeInTheDocument();
    expect(within(dialog).getByText("Récemment consultées")).toBeInTheDocument();
  });

  it("goes to the advanced search with the text (Ctrl+Enter or the link)", async () => {
    mockProject();
    mockNames([]);
    const user = userEvent.setup();
    const { router } = await renderApp(`/p/${ids.project}`);
    await screen.findByRole("tree", { name: "Explorateur" });

    await user.keyboard("{Control>}k{/Control}");
    await user.type(await screen.findByRole("combobox"), "zzz");
    expect(await screen.findByText("Aucun résultat")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Recherche avancée" }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/p/${ids.project}/search`);
    });
    expect(router.state.location.search).toEqual({ q: "zzz" });
  });
});
