import { screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { chroniquesNodes, ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";
import { ilsaStructure, structureNodes } from "../../test/structureMocks";

async function openIlsa(structure = ilsaStructure()) {
  mockProject({ nodes: [...chroniquesNodes(), ...structureNodes()], structure });
  await renderApp(`/p/${ids.project}/e/${ids.ilsa}`);
  return screen.findByRole("article");
}

describe("PageView · categories and properties", () => {
  it("shows the pills of the categories and the out-of-rules badge (F07.R3, F08.R3)", async () => {
    const article = await openIlsa();

    const pills = await within(article).findByRole("list", { name: "Catégories de la page" });
    expect(within(pills).getByRole("link", { name: "PNJ" })).toBeInTheDocument();
    expect(await within(article).findByText("hors règles")).toBeInTheDocument();
  });

  it("has no badge when every value respects its rules", async () => {
    const structure = ilsaStructure();
    const article = await openIlsa({
      ...structure,
      properties: structure.properties.map((p) => ({ ...p, violations: [] })),
    });

    await within(article).findByRole("list", { name: "Catégories de la page" });
    expect(within(article).queryByText("hors règles")).toBeNull();
  });

  it("lists the properties with their value, origin and violations (F04.R4, E2E-05-12)", async () => {
    await openIlsa();

    const panel = await screen.findByRole("complementary", { name: "Panneau de la page" });
    expect(
      within(panel).getByRole("heading", { name: /Propriétés · 2 valeurs/ }),
    ).toBeInTheDocument();
    const list = within(panel).getByRole("list", { name: "Propriétés" });
    const hostile = within(list).getByRole("link", { name: "Hostile" }).closest("li");
    expect(hostile).toHaveTextContent("Non via PNJ");
    const niveau = within(list).getByRole("link", { name: "Niveau" }).closest("li");
    expect(niveau).toHaveTextContent("72");
    expect(niveau).not.toHaveTextContent("via");
    expect(niveau).toHaveTextContent("Doit être inférieur ou égal à 60");
  });
});
