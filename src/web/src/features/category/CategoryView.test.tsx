import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { chroniquesNodes, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";
import { mockStructure, pnjView, structureIds, structureNodes } from "../../test/structureMocks";
import { ids } from "../../test/projectMocks";
import { loopOf } from "./inheritance";
import { categoryList } from "../../test/structureMocks";

const nodes = () => [...chroniquesNodes(), ...structureNodes()];

async function openPnj(path = "") {
  const { nodes: all } = mockProject({ nodes: nodes() });
  const writes = mockStructure({ view: pnjView(all) });
  const user = userEvent.setup();
  await renderApp(`/p/${ids.project}/e/${structureIds.pnj}${path}`);
  const article = await screen.findByRole("article", { name: "PNJ" });
  return { user, writes, article };
}

describe("loopOf", () => {
  it("returns the loop a descendant parent would create (E2E-07-03)", () => {
    expect(loopOf(categoryList, structureIds.personnage, structureIds.marchand)).toEqual([
      structureIds.personnage,
      structureIds.pnj,
      structureIds.marchand,
      structureIds.personnage,
    ]);
  });

  it("returns a two-step loop for the category itself", () => {
    expect(loopOf(categoryList, structureIds.pnj, structureIds.pnj)).toEqual([
      structureIds.pnj,
      structureIds.pnj,
    ]);
  });

  it("accepts an unrelated parent or none", () => {
    expect(loopOf(categoryList, structureIds.region, structureIds.pnj)).toBeNull();
    expect(loopOf(categoryList, structureIds.pnj, null)).toBeNull();
  });
});

describe("CategoryView", () => {
  it("shows the description, the inheritance and the sub-categories (E2E-07-01)", async () => {
    const { article } = await openPnj();

    expect(within(article).getByText("Personnage non joueur.")).toBeInTheDocument();
    const lineage = within(article).getByRole("navigation", { name: "Chaîne d’héritage" });
    expect(within(lineage).getByRole("link", { name: /Entité/ })).toBeInTheDocument();
    expect(within(lineage).getByRole("link", { name: /Personnage/ })).toBeInTheDocument();
    expect(within(article).getByRole("link", { name: "Marchand" })).toBeInTheDocument();
    expect(within(article).getByRole("link", { name: "Garde" })).toBeInTheDocument();
  });

  it("lists the properties with their source (E2E-07-01)", async () => {
    const { article } = await openPnj();

    const table = within(article).getByRole("table");
    const hostile = within(table).getByRole("row", { name: /Hostile/ });
    expect(hostile).toHaveTextContent("Non");
    expect(hostile).toHaveTextContent("PNJ");
    const niveau = within(table).getByRole("row", { name: /Niveau/ });
    expect(niveau).toHaveTextContent("≥ 1, ≤ 60");
    expect(niveau).toHaveTextContent("redéfinie");
    const role = within(table).getByRole("row", { name: /Rôle/ });
    expect(role).toHaveTextContent("Garde");
    expect(role).toHaveTextContent("Personnage");
  });

  it("lists the pages with or without the sub-categories (E2E-07-07)", async () => {
    const { user, article } = await openPnj();

    const pages = within(article).getByRole("list", { name: "Pages de la catégorie" });
    expect(
      await within(pages).findByRole("link", { name: /Ilsa Varenne\s*Marchand/ }),
    ).toBeInTheDocument();
    expect(
      within(pages).getByRole("link", { name: /Capitaine Orsen\s*Garde/ }),
    ).toBeInTheDocument();

    await user.click(
      within(article).getByRole("checkbox", { name: "Inclure les sous-catégories" }),
    );

    await waitFor(() => {
      expect(within(pages).queryByRole("link")).toBeNull();
    });
  });

  it("refuses a parent that would create a loop and keeps the previous one (E2E-07-03)", async () => {
    const { user, article } = await openPnj();
    await user.click(within(article).getByRole("button", { name: "Modifier" }));

    const parent = within(article).getByRole("combobox", { name: "Catégorie parente" });
    await user.selectOptions(parent, "Marchand");

    expect(within(article).getByRole("alert")).toHaveTextContent(
      "Créerait une boucle : PNJ › Marchand › PNJ",
    );
    expect(parent).toHaveValue(structureIds.personnage);
  });

  it("saves the description, the parent and the changed defaults", async () => {
    const { user, writes, article } = await openPnj();
    await user.click(within(article).getByRole("button", { name: "Modifier" }));

    const description = within(article).getByRole("textbox", { name: "Description" });
    await user.clear(description);
    await user.type(description, "PNJ du monde.");
    const niveau = within(article).getByRole("textbox", { name: "Valeur par défaut de Niveau" });
    await user.clear(niveau);
    await user.type(niveau, "15");
    await user.click(within(article).getByRole("button", { name: "Enregistrer" }));

    await waitFor(() => {
      expect(writes.values).toEqual([
        { id: structureIds.pnj, propertyId: structureIds.niveau, body: { value: 15 } },
      ]);
    });
    expect(writes.categories).toEqual([
      {
        id: structureIds.pnj,
        body: { description: "PNJ du monde.", parentCategoryId: structureIds.personnage },
      },
    ]);
    expect(await within(article).findByRole("button", { name: "Modifier" })).toBeInTheDocument();
  });

  it("overrides an inherited value and adds a property", async () => {
    const { user, writes, article } = await openPnj();
    await user.click(within(article).getByRole("button", { name: "Modifier" }));

    await user.click(
      within(within(article).getByRole("row", { name: /Rôle/ })).getByRole("button", {
        name: "Redéfinir",
      }),
    );
    await user.selectOptions(
      within(article).getByRole("combobox", { name: "Valeur par défaut de Rôle" }),
      "Marchande",
    );
    await user.click(within(article).getByRole("button", { name: "Ajouter" }));
    const dialog = await screen.findByRole("dialog", { name: "Ajouter une propriété" });
    await user.selectOptions(within(dialog).getByRole("combobox", { name: "Propriété" }), "Région");
    await user.click(within(dialog).getByRole("button", { name: "Ajouter" }));
    expect(within(dialog).getByRole("button", { name: "Ajouter" })).toBeDisabled();
    await user.keyboard("{Escape}");
    await user.click(within(article).getByRole("button", { name: "Enregistrer" }));

    await waitFor(() => {
      expect(writes.values).toEqual([
        { id: structureIds.pnj, propertyId: structureIds.role, body: { value: "marchande" } },
      ]);
    });
  });
});
