import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
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

describe("PageView · narrow page", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  /** A page too narrow for the side panel: every observed element is 600px wide. */
  function narrowPage() {
    vi.stubGlobal(
      "ResizeObserver",
      class {
        constructor(private readonly callback: ResizeObserverCallback) {}
        observe(target: Element) {
          this.callback(
            [{ target, contentRect: { width: 600 } } as unknown as ResizeObserverEntry],
            this,
          );
        }
        unobserve() {}
        disconnect() {}
      },
    );
  }

  it("folds the side panel between the header and the content, collapsed by default", async () => {
    narrowPage();
    const user = userEvent.setup();
    const article = await openIlsa();

    const expander = await within(article).findByRole("button", { name: /Panneau de la page/ });
    expect(expander).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("complementary", { name: "Panneau de la page" })).toBeNull();

    await user.click(expander);

    const panel = within(article).getByRole("complementary", { name: "Panneau de la page" });
    expect(within(panel).getByRole("list", { name: "Propriétés" })).toBeInTheDocument();
    expect(expander).toHaveAttribute("aria-expanded", "true");
  });

  it("has no side panel button, the expander toggles the panel", async () => {
    narrowPage();
    const article = await openIlsa();

    await within(article).findByRole("button", { name: /Panneau de la page/ });
    expect(within(article).queryByRole("button", { name: /panneau latéral/ })).toBeNull();
  });
});
