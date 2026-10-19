import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { roots } from "../../shared/ids/roots";
import { mockStructure, pnjView } from "../../test/structureMocks";
import { ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";

async function openTree(path = `/p/${ids.project}/e/${roots.pages}`) {
  const result = await renderApp(path);
  const tree = await screen.findByRole("tree", { name: "Explorateur" });
  await within(tree).findByRole("treeitem", { name: /Personnages/ });
  return { ...result, tree };
}

describe("Explorer", () => {
  it("shows the four roots with their counts, and expanded children with ARIA levels (F02.R1, E2E-02-08)", async () => {
    mockProject();
    const { tree } = await openTree();

    const items = within(tree).getAllByRole("treeitem");
    expect(
      items.filter((i) => i.getAttribute("aria-level") === "1").map((i) => i.textContent),
    ).toEqual(["PPages5", "CCatégories1", "PrPropriétés0", "LLiens0"]);
    const personnages = within(tree).getByRole("treeitem", { name: "Personnages" });
    expect(personnages).toHaveAttribute("aria-level", "2");
    expect(personnages).toHaveAttribute("aria-expanded", "false");
  });

  it("navigates, expands and collapses with the arrow keys (E2E-17-01)", async () => {
    mockProject();
    const user = userEvent.setup();
    const { tree } = await openTree();

    tree.focus();
    await user.keyboard("{ArrowDown}{ArrowRight}");
    expect(within(tree).getByRole("treeitem", { name: "Personnages" })).toHaveAttribute(
      "aria-expanded",
      "true",
    );
    await user.keyboard("{ArrowRight}");
    expect(tree).toHaveAttribute("aria-activedescendant", `tree-row-${ids.pnj}`);
    await user.keyboard("{ArrowLeft}{ArrowLeft}");
    expect(tree).toHaveAttribute("aria-activedescendant", `tree-row-${ids.personnages}`);
    await user.keyboard("{ArrowLeft}");
    expect(within(tree).getByRole("treeitem", { name: "Personnages" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
  });

  it("opens the focused element with Enter", async () => {
    mockProject();
    const user = userEvent.setup();
    const { tree, router } = await openTree();

    tree.focus();
    await user.keyboard("{ArrowDown}{ArrowDown}{Enter}");

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/p/${ids.project}/e/${ids.lieux}`);
    });
  });

  it("renames with F2, at once and through the API (E2E-02-02)", async () => {
    const { writes } = mockProject();
    const user = userEvent.setup();
    const { tree } = await openTree(`/p/${ids.project}/e/${ids.orsen}`);

    await within(tree).findByRole("treeitem", { name: "Capitaine Orsen" });
    tree.focus();
    await user.keyboard("{F2}");
    const input = within(tree).getByRole("textbox", { name: "Nouveau nom" });
    await user.clear(input);
    await user.type(input, "Commandant Orsen{Enter}");

    expect(
      await within(tree).findByRole("treeitem", { name: "Commandant Orsen" }),
    ).toBeInTheDocument();
    await waitFor(() => {
      expect(writes.patches).toEqual([{ id: ids.orsen, body: { name: "Commandant Orsen" } }]);
    });
  });

  it("creates a folder named in place under the focused container (E2E-02-01)", async () => {
    const { writes } = mockProject();
    const user = userEvent.setup();
    const { tree } = await openTree();

    await user.click(screen.getByRole("button", { name: "Nouveau dossier" }));
    await user.type(
      within(tree).getByRole("textbox", { name: "Nom du nouveau dossier" }),
      "Objets{Enter}",
    );

    await waitFor(() => {
      expect(writes.created).toEqual([{ kind: "folder", parentId: roots.pages, name: "Objets" }]);
    });
    expect(await within(tree).findByRole("treeitem", { name: "Objets" })).toBeInTheDocument();
  });

  it("focuses the name of an element created from the context menu", async () => {
    mockProject();
    const user = userEvent.setup();
    const { tree } = await openTree();

    await user.pointer({
      keys: "[MouseRight]",
      target: within(tree).getByRole("treeitem", { name: /Propriétés/ }),
    });
    await user.click(await screen.findByRole("menuitem", { name: "Nouvelle propriété" }));

    const input = await within(tree).findByRole("textbox");
    await waitFor(() => {
      expect(input).toHaveFocus();
    });
  });

  it("refuses to move a category into Pages with the keyboard alternative (E2E-02-04)", async () => {
    const { writes, nodes } = mockProject({
      uiState: { [`tree-${ids.project}`]: { expanded: [roots.pages, roots.categories] } },
    });
    mockStructure({ view: pnjView(nodes) });
    const user = userEvent.setup();
    const { tree } = await openTree();

    await user.click(within(tree).getByRole("treeitem", { name: "PNJ" }));
    tree.focus();
    await user.keyboard("{Control>}x{/Control}{Home}{Control>}v{/Control}");

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Un élément reste sous sa propre racine.",
    );
    expect(writes.patches).toEqual([]);
  });

  it("moves a page under another folder by cut and paste", async () => {
    const { writes } = mockProject({
      uiState: { [`tree-${ids.project}`]: { expanded: [roots.pages, ids.lieux] } },
    });
    const user = userEvent.setup();
    const { tree } = await openTree();

    await user.click(within(tree).getByRole("treeitem", { name: "Hautbois" }));
    tree.focus();
    await user.keyboard("{Control>}x{/Control}");
    await user.click(within(tree).getByRole("treeitem", { name: "Personnages" }));
    tree.focus();
    await user.keyboard("{Control>}v{/Control}");

    await waitFor(() => {
      expect(writes.patches).toEqual([{ id: ids.hautbois, body: { parentId: ids.personnages } }]);
    });
  });

  it("finds a line by typing its first letters", async () => {
    mockProject();
    const user = userEvent.setup();
    const { tree } = await openTree();

    tree.focus();
    await user.keyboard("li");

    expect(tree).toHaveAttribute("aria-activedescendant", `tree-row-${ids.lieux}`);
  });

  it("reveals the element shown in the work area", async () => {
    mockProject();
    const { tree } = await openTree(`/p/${ids.project}/e/${ids.dialogues}`);

    const dialogues = await within(tree).findByRole("treeitem", { name: "Dialogues" });
    expect(dialogues).toHaveAttribute("aria-selected", "true");
    expect(dialogues).toHaveAttribute("aria-level", "5");
  });

  it("filters the tree ignoring accents", async () => {
    mockProject();
    const user = userEvent.setup();
    const { tree } = await openTree();

    await user.type(screen.getByRole("textbox", { name: "Filtrer l’arbre" }), "col gele");

    expect(
      within(tree)
        .getAllByRole("treeitem")
        .map((i) => i.textContent),
    ).toEqual(["PPages5", "Lieux", "PCol gelé"]);
  });
});
