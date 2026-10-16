import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import type { SearchHit, SearchResultPage } from "../../shared/api/search";
import { requireElementId } from "../../shared/ids/elementId";
import { http, server } from "../../test/msw";
import { ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";
import { definitions, mockStructure, structureIds } from "../../test/structureMocks";

const quete = requireElementId("2D8F5A7C1B9E0463");

function hit(overrides: Partial<SearchHit> & Pick<SearchHit, "id" | "name">): SearchHit {
  return {
    kind: "page",
    path: [
      { id: requireElementId("8000000000000001"), kind: "root", name: "Pages" },
      { id: ids.lieux, kind: "folder", name: "Lieux" },
    ],
    title: [{ text: overrides.name, hit: false }],
    excerpt: [],
    categories: [],
    updatedAt: "2026-10-13T06:00:00.000Z",
    updatedBy: "camille",
    updatedByName: "Camille R.",
    issues: [],
    attachment: null,
    ...overrides,
  };
}

const colGele: SearchHit = hit({
  id: ids.colGele,
  name: "Col gelé",
  title: [{ text: "Col gelé", hit: true }],
  excerpt: [
    { text: "Passage fermé en hiver : le ", hit: false },
    { text: "Col gelé", hit: true },
    { text: " est impraticable.", hit: false },
  ],
  categories: [{ id: structureIds.region, name: "Région" }],
});

const ilsa: SearchHit = hit({
  id: ids.ilsa,
  name: "Ilsa Varenne",
  excerpt: [
    { text: "Au pied du ", hit: false },
    { text: "Col gelé", hit: true },
  ],
  categories: [{ id: structureIds.pnj, name: "PNJ" }],
});

function page(hits: SearchHit[], overrides: Partial<SearchResultPage> = {}): SearchResultPage {
  return {
    hits,
    total: hits.length,
    facets: [{ kind: "page", count: hits.length }],
    nextCursor: null,
    ...overrides,
  };
}

/** Answers every search with `answer` and records the parameters of each request. */
function mockSearch(answer: (params: URLSearchParams) => SearchResultPage) {
  const requests: URLSearchParams[] = [];
  server.use(
    http.get("/api/v1/projects/{projectId}/search", ({ request, response }) => {
      const params = new URL(request.url).searchParams;
      requests.push(params);
      return response(200).json(answer(params));
    }),
  );
  return requests;
}

async function openSearch(search = "") {
  mockProject();
  mockStructure();
  return renderApp(`/p/${ids.project}/search${search}`);
}

describe("SearchView", () => {
  it("lists the results with the matching passages marked (E2E-10-02)", async () => {
    mockSearch(() => page([colGele, ilsa]));
    await openSearch("?q=col%20gele");

    expect(
      await screen.findByRole("heading", { name: "2 résultats pour « col gele »" }),
    ).toBeInTheDocument();
    const results = screen.getByRole("list", { name: "Recherche" });
    const cards = within(results).getAllByRole("listitem");
    expect(cards).toHaveLength(2);
    expect(within(cards[0] as HTMLElement).getByRole("link")).toHaveAccessibleName(
      /Col gelé.*Pages \/ Lieux/,
    );
    const marked = within(results)
      .getAllByText("Col gelé", { selector: "mark" })
      .map((m) => m.tagName);
    expect(marked).toHaveLength(3); // the title of the first result, its excerpt, the excerpt of the second
    expect(within(cards[0] as HTMLElement).getByText("Région")).toBeInTheDocument();
    expect(within(cards[1] as HTMLElement).getByText("PNJ")).toBeInTheDocument();
  });

  it("never renders markup found in a page as HTML", async () => {
    mockSearch(() =>
      page([
        hit({
          id: ids.hautbois,
          name: "Hautbois",
          excerpt: [{ text: '<img src="x" onerror="alert(1)"> et <b>gras</b>', hit: true }],
        }),
      ]),
    );
    await openSearch("?q=img");

    const results = await screen.findByRole("list", { name: "Recherche" });

    expect(results.querySelector("img")).toBeNull();
    expect(results.querySelector("b")).toBeNull();
    expect(
      within(results).getByText(/<img src="x" onerror="alert\(1\)"> et <b>gras<\/b>/),
    ).toBeInTheDocument();
  });

  it("reads its whole state from the URL and sends it to the backend (F10.R4)", async () => {
    const requests = mockSearch(() => page([ilsa]));
    await openSearch(
      `?q=ilsa&kind=%5B%22page%22%5D&cat=%5B%22${structureIds.pnj}%22%5D&sub=true&mod=week&oor=true&sort=name&where=%5B%22${structureIds.niveau}%3Agt%3A50%22%5D`,
    );

    await screen.findByRole("heading", { name: "1 résultat pour « ilsa »" });

    const params = requests.at(-1) as URLSearchParams;
    expect(params.get("q")).toBe("ilsa");
    expect(params.getAll("kind")).toEqual(["page"]);
    expect(params.getAll("category")).toEqual([structureIds.pnj]);
    expect(params.get("subCategories")).toBe("true");
    expect(params.get("modified")).toBe("week");
    expect(params.get("outOfRules")).toBe("true");
    expect(params.get("sort")).toBe("name");
    expect(params.getAll("where")).toEqual([`${structureIds.niveau}:gt:50`]);
    // The controls show the same state.
    expect(screen.getByRole("button", { name: "PNJ" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("checkbox", { name: "Inclure les sous-catégories" })).toBeChecked();
    expect(screen.getByRole("button", { name: "7 j" })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("checkbox", { name: "Hors règles" })).toBeChecked();
    expect(screen.getByRole("combobox", { name: "Trier par" })).toHaveValue("name");
    expect(screen.getByRole("searchbox", { name: "Rechercher dans le projet" })).toHaveValue(
      "ilsa",
    );
    expect(screen.getByRole("combobox", { name: "Propriété" })).toHaveValue(structureIds.niveau);
    expect(screen.getByRole("combobox", { name: "Opérateur" })).toHaveValue("gt");
    expect(screen.getByRole("textbox", { name: "Valeur" })).toHaveValue("50");
  });

  it("shows the counters of the types and filters by type (E2E-10-03)", async () => {
    const requests = mockSearch(() =>
      page([ilsa], {
        facets: [
          { kind: "page", count: 3 },
          { kind: "category", count: 1 },
          { kind: "attachment", count: 1 },
        ],
      }),
    );
    const user = userEvent.setup();
    const { router } = await openSearch("?q=col");
    const pages = await screen.findByRole("checkbox", { name: "Pages 3" });

    expect(pages).toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Catégories 1" })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Propriétés 0" })).toBeChecked();

    await user.click(screen.getByRole("checkbox", { name: "Catégories 1" }));
    await user.click(screen.getByRole("checkbox", { name: "Pièces jointes 1" }));

    await waitFor(() => {
      expect(router.state.location.search).toMatchObject({
        kind: ["page", "folder", "propertyDefinition", "linkType"],
      });
    });
    await waitFor(() => {
      expect((requests.at(-1) as URLSearchParams).getAll("kind")).toEqual([
        "page",
        "folder",
        "propertyDefinition",
        "linkType",
      ]);
    });
    expect(screen.getByRole("checkbox", { name: "Catégories 1" })).not.toBeChecked();
  });

  it("adds a condition on a property with a value suited to its type (E2E-10-04, E2E-10-05)", async () => {
    const requests = mockSearch(() => page([ilsa]));
    const user = userEvent.setup();
    const { router } = await openSearch();
    await screen.findByRole("heading", { name: "1 résultat" });

    await user.click(screen.getByRole("button", { name: "+ Ajouter une condition" }));

    // Properties are listed by name: the first is Hostile, a switch between Oui and Non.
    const property = await screen.findByRole("combobox", { name: "Propriété" });
    expect(property).toHaveValue(structureIds.hostile);
    expect(screen.getByRole("combobox", { name: "Valeur" })).toHaveValue("false");
    await waitFor(() => {
      expect((requests.at(-1) as URLSearchParams).getAll("where")).toEqual([
        `${structureIds.hostile}:eq:false`,
      ]);
    });

    await user.selectOptions(property, structureIds.niveau);
    await user.selectOptions(screen.getByRole("combobox", { name: "Opérateur" }), "gt");
    const value = await screen.findByRole("textbox", { name: "Valeur" });
    await user.clear(value);
    await user.type(value, "50");

    await waitFor(() => {
      expect((requests.at(-1) as URLSearchParams).getAll("where")).toEqual([
        `${structureIds.niveau}:gt:50`,
      ]);
    });
    expect(router.state.location.search).toMatchObject({ where: [`${structureIds.niveau}:gt:50`] });
    expect(
      within(screen.getByRole("combobox", { name: "Opérateur" }))
        .getAllByRole("option")
        .map((o) => o.textContent),
    ).toEqual(["=", "≠", "<", "≤", ">", "≥", "est renseignée", "n’est pas renseignée"]);

    await user.click(screen.getByRole("button", { name: "Retirer la condition sur Niveau" }));
    expect(screen.queryByRole("combobox", { name: "Propriété" })).not.toBeInTheDocument();
  });

  it("filters by category chip, with the sub-categories (E2E-10-03)", async () => {
    const requests = mockSearch(() => page([ilsa]));
    const user = userEvent.setup();
    await openSearch();
    await screen.findByRole("heading", { name: "1 résultat" });

    expect(
      screen.queryByRole("checkbox", { name: "Inclure les sous-catégories" }),
    ).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "PNJ" }));
    await user.click(await screen.findByRole("checkbox", { name: "Inclure les sous-catégories" }));

    await waitFor(() => {
      const params = requests.at(-1) as URLSearchParams;
      expect(params.getAll("category")).toEqual([structureIds.pnj]);
      expect(params.get("subCategories")).toBe("true");
    });
  });

  it("reaches the categories beyond the chips through « + Autre »", async () => {
    mockSearch(() => page([ilsa]));
    mockProject();
    mockStructure();
    const user = userEvent.setup();
    server.use(
      http.get("/api/v1/projects/{projectId}/categories", ({ response }) =>
        response(200).json(
          Array.from({ length: 9 }, (_, i) => ({
            id: requireElementId(`80000000000000A${String(i)}`),
            name: `Catégorie ${String(i + 1)}`,
            parentCategoryId: null,
          })),
        ),
      ),
    );
    await renderApp(`/p/${ids.project}/search`);

    await user.click(await screen.findByRole("button", { name: "+ Autre" }));
    await user.click(await screen.findByRole("menuitem", { name: "Catégorie 9" }));

    expect(await screen.findByRole("button", { name: "Catégorie 9" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("filters by period and « hors règles », and shows the broken rule (E2E-10-06, E2E-10-07)", async () => {
    const requests = mockSearch(() =>
      page([
        hit({
          id: ids.ilsa,
          name: "Ilsa Varenne",
          issues: [
            {
              propertyId: structureIds.niveau,
              propertyName: "Niveau",
              rule: { op: "less_or_equal", value: 60 },
              index: null,
            },
          ],
        }),
      ]),
    );
    const user = userEvent.setup();
    await openSearch();
    await screen.findByRole("heading", { name: "1 résultat" });

    await user.click(screen.getByRole("button", { name: "24 h" }));
    await user.click(screen.getByRole("checkbox", { name: "Hors règles" }));

    await waitFor(() => {
      const params = requests.at(-1) as URLSearchParams;
      expect(params.get("modified")).toBe("day");
      expect(params.get("outOfRules")).toBe("true");
    });
    expect(
      await screen.findByText("Niveau : Doit être inférieur ou égal à 60"),
    ).toBeInTheDocument();
    expect(definitions.niveau.name).toBe("Niveau");

    await user.click(screen.getByRole("button", { name: "Tout" }));
    await waitFor(() => {
      expect((requests.at(-1) as URLSearchParams).has("modified")).toBe(false);
    });
  });

  it("keeps the text when the filters are reset (E2E-10-10)", async () => {
    const user = userEvent.setup();
    const { router } = await openSearch("?q=ilsa&mod=week&oor=true&kind=%5B%22page%22%5D");
    mockSearch(() => page([ilsa]));
    const reset = await screen.findByRole("button", { name: "Réinitialiser" });
    expect(reset).toBeEnabled();

    await user.click(reset);

    await waitFor(() => {
      expect(router.state.location.search).toEqual({ q: "ilsa" });
    });
    expect(screen.getByRole("searchbox", { name: "Rechercher dans le projet" })).toHaveValue(
      "ilsa",
    );
    expect(screen.getByRole("button", { name: "Réinitialiser" })).toBeDisabled();
  });

  it("proposes to reset the filters when nothing is found (E2E-10-09)", async () => {
    mockSearch((params) => (params.has("kind") ? page([]) : page([ilsa])));
    const user = userEvent.setup();
    const { router } = await openSearch("?q=zzzz&kind=%5B%22category%22%5D");

    expect(await screen.findByText("Aucun résultat")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Réinitialiser les filtres" }));

    await waitFor(() => {
      expect(router.state.location.search).toEqual({ q: "zzzz" });
    });
    expect(
      await screen.findByRole("heading", { name: "1 résultat pour « zzzz »" }),
    ).toBeInTheDocument();
  });

  it("does not offer to reset when there is nothing to reset", async () => {
    mockSearch(() => page([]));
    await openSearch("?q=zzzz");

    expect(await screen.findByText("Aucun résultat")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Réinitialiser les filtres" }),
    ).not.toBeInTheDocument();
  });

  it("searches once the typing rests and writes the text to the URL", async () => {
    const requests = mockSearch(() => page([ilsa]));
    const user = userEvent.setup();
    const { router } = await openSearch();
    const field = await screen.findByRole("searchbox", { name: "Rechercher dans le projet" });

    await user.type(field, "ilsa");

    await waitFor(() => {
      expect(router.state.location.search).toEqual({ q: "ilsa" });
    });
    await waitFor(() => {
      expect((requests.at(-1) as URLSearchParams).get("q")).toBe("ilsa");
    });
    // One request per pause, not one per key.
    expect(requests.filter((r) => r.has("q")).length).toBeLessThanOrEqual(2);
  });

  it("sorts the results and loads the next page with the cursor", async () => {
    const requests = mockSearch((params) =>
      params.get("cursor") === "next"
        ? page([ilsa], { total: 2 })
        : page([colGele], { total: 2, nextCursor: "next" }),
    );
    const user = userEvent.setup();
    await openSearch();

    await user.click(await screen.findByRole("button", { name: "Afficher plus de résultats" }));
    await waitFor(() => {
      expect(
        within(screen.getByRole("list", { name: "Recherche" })).getAllByRole("listitem"),
      ).toHaveLength(2);
    });
    expect(
      screen.queryByRole("button", { name: "Afficher plus de résultats" }),
    ).not.toBeInTheDocument();

    await user.selectOptions(screen.getByRole("combobox", { name: "Trier par" }), "modified");
    await waitFor(() => {
      expect((requests.at(-1) as URLSearchParams).get("sort")).toBe("modified");
    });
    expect((requests.at(-1) as URLSearchParams).has("cursor")).toBe(false);
  });

  it("shows the attachment type and size instead of an author", async () => {
    mockSearch(() =>
      page([
        hit({
          id: quete,
          name: "col_gele_concept.jpg",
          kind: "attachment",
          attachment: {
            id: quete,
            pageId: ids.colGele,
            fileName: "col_gele_concept.jpg",
            mime: "image/jpeg",
            size: 3_565_158,
            display: "image",
          },
        }),
      ]),
    );
    await openSearch("?q=concept");

    expect(await screen.findByText("image/jpeg · 3,4 Mo")).toBeInTheDocument();
  });

  it("lists the recently viewed and edited elements on the right (E2E-11-02)", async () => {
    mockSearch(() => page([ilsa]));
    mockProject();
    mockStructure();
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
          edited: [
            {
              id: ids.hautbois,
              kind: "page",
              name: "Hautbois",
              editedAt: "2026-10-13T07:59:30.000Z",
              editedBy: "local",
              editedByName: "Arthur",
            },
            {
              id: ids.ilsa,
              kind: "page",
              name: "Ilsa Varenne",
              editedAt: "2026-10-13T06:00:00.000Z",
              editedBy: "camille",
              editedByName: "Camille R.",
            },
          ],
        }),
      ),
    );
    await renderApp(`/p/${ids.project}/search`);
    const panel = await screen.findByRole("complementary", { name: "Activité récente" });

    expect(await within(panel).findByRole("link", { name: /Hautbois/ })).toHaveTextContent(
      "il y a 2 min",
    );
    await user.click(within(panel).getByRole("tab", { name: "Modifiées" }));

    const edited = within(panel).getAllByRole("link");
    expect(edited[0]).toHaveTextContent("Hautbois");
    expect(edited[0]).toHaveTextContent("moi");
    expect(edited[0]).toHaveTextContent("à l’instant");
    expect(edited[1]).toHaveTextContent("Camille R.");
  });
});

describe("Category screen link", () => {
  it("opens the search filtered on the category and its sub-categories (E2E-07-07)", async () => {
    mockSearch(() => page([ilsa]));
    // Covered end to end by the Category tests: here, only the URL contract of the link.
    const { router } = await openSearch(`?cat=%5B%22${structureIds.pnj}%22%5D&sub=true`);

    await screen.findByRole("heading", { name: "1 résultat" });

    expect(router.state.location.search).toEqual({ cat: [structureIds.pnj], sub: true });
    expect(screen.getByRole("button", { name: "PNJ" })).toHaveAttribute("aria-pressed", "true");
  });
});
