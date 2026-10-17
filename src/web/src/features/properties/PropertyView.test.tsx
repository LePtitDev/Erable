import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { delay } from "msw";
import { describe, expect, it } from "vitest";
import { chroniquesNodes, ids, mockProject } from "../../test/projectMocks";
import { http, server } from "../../test/msw";
import { renderApp } from "../../test/render";
import {
  definitions,
  mockProperty,
  mockStructure,
  niveauView,
  structureIds,
  structureNodes,
} from "../../test/structureMocks";

const nodes = () => [...chroniquesNodes(), ...structureNodes()];

async function open(
  which: "niveau" | "role",
  refuse?: { code: string; pages: unknown[] },
  path = "",
) {
  const { nodes: all } = mockProject({ nodes: nodes() });
  mockStructure();
  const base = niveauView(all);
  const id = structureIds[which];
  const node = all.find((n) => n.id === id);
  const view =
    which === "niveau"
      ? base
      : {
          ...base,
          element: { ...base.element, id, name: node?.name ?? "" },
          definition: definitions.role,
        };
  const writes = mockProperty(view, refuse === undefined ? {} : { refuse });
  const user = userEvent.setup();
  await renderApp(`/p/${ids.project}/e/${id}${path}`);
  const article = await screen.findByRole("article", { name: view.element.name });
  return { user, writes, article };
}

describe("PropertyView", () => {
  it("shows the type, the rules, the usage and the pages out of rules (E2E-08-01)", async () => {
    const { article } = await open("niveau");

    expect(within(article).getByText("int")).toBeInTheDocument();
    const rules = within(article).getByRole("list", { name: "Règles de validation" });
    expect(
      within(rules)
        .getAllByRole("listitem")
        .map((li) => li.textContent),
    ).toEqual(["≥ 1", "≤ 60"]);
    const usedBy = within(article).getByRole("list", { name: "Utilisée par" });
    expect(within(usedBy).getByRole("link", { name: "PNJ" })).toBeInTheDocument();
    expect(within(article).getByText("Renseignée sur 4 pages")).toBeInTheDocument();
    expect(within(article).getByText("2 pages hors règles")).toBeInTheDocument();
    const out = within(article).getByRole("list", { name: "Pages hors règles" });
    expect(within(out).getByText("Ilsa Varenne").closest("li")).toHaveTextContent(
      "Ilsa Varenne (72)Doit être inférieur ou égal à 60",
    );
    expect(within(out).getByText("Capitaine Orsen").closest("li")).toHaveTextContent("(99)");
  });

  it("tests a value against the rules (E2E-08-04)", async () => {
    const { user, article } = await open("niveau");
    const field = within(article).getByRole("textbox", { name: "Valeur à tester" });

    await user.type(field, "72");
    expect(within(article).getByRole("status", { name: "Résultat du test" })).toHaveTextContent(
      "Échoue : less_or_equal 60",
    );

    await user.clear(field);
    await user.type(field, "30");
    expect(within(article).getByRole("status", { name: "Résultat du test" })).toHaveTextContent(
      "Valide",
    );
  });

  it("offers the rules of the type and says there is none for a boolean (E2E-08-03)", async () => {
    const { user, article } = await open("niveau", undefined, "?edit=true");
    const picker = within(article).getByRole("radiogroup", { name: "Type de valeur" });

    await user.click(within(picker).getByRole("radio", { name: /^string/ }));
    const add = within(article).getByRole("group", { name: "Ajouter :" });
    expect(
      within(add)
        .getAllByRole("button")
        .map((b) => b.textContent),
    ).toEqual(["longueur min.", "longueur max.", "motif", "non vide"]);

    await user.click(within(picker).getByRole("radio", { name: /^bool/ }));
    expect(
      within(article).getAllByText("Aucune règle disponible pour un booléen.").length,
    ).toBeGreaterThan(0);
    expect(within(article).queryByRole("group", { name: "Ajouter :" })).toBeNull();
  });

  it("saves a changed rule and renames the property (E2E-08-05)", async () => {
    const { user, writes, article } = await open("niveau", undefined, "?edit=true");

    const line = within(article).getByRole("group", { name: "≤" });
    const value = within(line).getByRole("textbox", { name: "Valeur" });
    await user.clear(value);
    await user.type(value, "80");
    const name = within(article).getByRole("textbox", { name: "Nom de la propriété" });
    await user.clear(name);
    await user.type(name, "Niveau requis");
    await user.click(within(article).getByRole("button", { name: "Enregistrer" }));

    await waitFor(() => {
      expect(writes.puts).toHaveLength(1);
    });
    expect(writes.puts[0]?.body).toMatchObject({
      key: "niveau",
      type: "int",
      rules: [
        { op: "greater_or_equal", value: 1 },
        { op: "less_or_equal", value: 80 },
      ],
    });
    await waitFor(() => {
      expect(writes.renames).toEqual([{ name: "Niveau requis" }]);
    });
  });

  it("blocks saving while a rule is incomplete", async () => {
    const { user, article } = await open("niveau", undefined, "?edit=true");

    await user.click(
      within(within(article).getByRole("group", { name: "Ajouter :" })).getByRole("button", {
        name: "entre",
      }),
    );

    expect(within(article).getByRole("button", { name: "Enregistrer" })).toBeDisabled();
    expect(within(article).getByRole("alert")).toHaveTextContent("Complétez ou retirez");
  });

  it("lists the pages blocking a change of type and keeps the editor open (E2E-08-10)", async () => {
    const { user, article } = await open(
      "niveau",
      {
        code: "property.value.notConvertible",
        pages: [{ id: ids.orsen, name: "Capitaine Orsen", kind: "page", value: "Garde" }],
      },
      "?edit=true",
    );

    await user.click(within(article).getByRole("button", { name: "Enregistrer" }));

    const dialog = await screen.findByRole("alertdialog", {
      name: "Changement de type impossible",
    });
    expect(within(dialog).getByText("Capitaine Orsen")).toBeInTheDocument();
    await user.click(within(dialog).getByRole("button", { name: "Fermer" }));
    expect(within(article).getByRole("button", { name: "Enregistrer" })).toBeInTheDocument();
  });

  it("asks for a replacement option before removing a used one (E2E-08-09)", async () => {
    const { user, writes, article } = await open(
      "role",
      {
        code: "property.option.inUse",
        pages: [{ id: ids.orsen, name: "Capitaine Orsen", kind: "page", value: "Garde" }],
      },
      "?edit=true",
    );

    await user.click(within(article).getByRole("button", { name: "Retirer l’option Garde" }));
    await user.click(within(article).getByRole("button", { name: "Enregistrer" }));

    const dialog = await screen.findByRole("alertdialog", {
      name: "Remplacer l’option supprimée",
    });
    expect(within(dialog).getByText("Capitaine Orsen")).toBeInTheDocument();
    const confirm = within(dialog).getByRole("button", { name: "Remplacer et enregistrer" });
    expect(confirm).toBeDisabled();
    await user.selectOptions(
      within(dialog).getByRole("combobox", { name: /Remplacer « Garde » par/ }),
      "Marchande",
    );
    await user.click(confirm);

    await waitFor(() => {
      expect(writes.puts).toHaveLength(2);
    });
    expect(writes.puts[1]?.body).toMatchObject({
      options: [{ key: "marchande", label: "Marchande" }],
      replacements: { garde: "marchande" },
    });
  });
  it("opens in edit mode even when the role of the user arrives after the property", async () => {
    const { nodes: all } = mockProject({ nodes: nodes() });
    mockStructure();
    mockProperty(niveauView(all));
    // The project (hence the role) answers last: the view must not settle in reading mode meanwhile.
    server.use(
      http.get("/api/v1/projects/{projectId}", async ({ response }) => {
        await delay(300);
        return response(200).json({
          id: ids.project,
          name: "Chroniques d'Aube",
          folder: "/w/chroniques",
          contentLanguage: "fr",
          formatVersion: 1,
          createdAt: "2026-03-10T09:00:00.000Z",
          myRole: "admin",
        });
      }),
    );
    await renderApp(`/p/${ids.project}/e/${structureIds.niveau}?edit=true`);

    expect(
      await screen.findByRole("radiogroup", { name: "Type de valeur" }, { timeout: 3000 }),
    ).toBeInTheDocument();
  });
});
