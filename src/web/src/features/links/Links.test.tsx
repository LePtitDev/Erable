import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { http, server } from "../../test/msw";
import { chroniquesNodes, ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";
import {
  ilsaStructure,
  linkIds,
  mentorView,
  mockLinks,
  structureNodes,
} from "../../test/structureMocks";

const nodes = () => [...chroniquesNodes(), ...structureNodes()];

const orsenLink = {
  id: ids.project,
  label: "Allié de",
  linkTypeId: linkIds.ally,
  pageId: ids.orsen,
  pageName: "Capitaine Orsen",
  typeName: "Allié de",
};

async function openIlsa(links: Parameters<typeof mockLinks>[0] = {}) {
  mockProject({ nodes: nodes(), structure: ilsaStructure() });
  const writes = mockLinks(links);
  const user = userEvent.setup();
  await renderApp(`/p/${ids.project}/e/${ids.ilsa}`);
  // The first render of the app in a file loads the lazy chunks: allow for it.
  const panel = await screen.findByRole("region", { name: "Liens" }, { timeout: 5000 });
  return { user, writes, panel };
}

describe("PageLinksPanel", () => {
  it("lists the outgoing links as « type · page » (E2E-09-02)", async () => {
    const { panel } = await openIlsa({ outgoing: [orsenLink] });

    const list = await within(panel).findByRole("list", { name: "Liens sortants" });
    expect(within(list).getByRole("listitem")).toHaveTextContent("Allié de · Capitaine Orsen");
    expect(within(list).getByRole("link", { name: "Capitaine Orsen" })).toBeInTheDocument();
  });

  it("lists the incoming links with the inverse label, apart from the implicit references (E2E-09-03)", async () => {
    const { panel } = await openIlsa({
      incoming: [{ ...orsenLink, label: "Élève de", typeName: "Mentor de" }],
      mentions: [
        {
          sourceId: ids.hautbois,
          sourceKind: "page",
          sourceName: "Hautbois",
          path: [],
          count: 1,
          embedded: false,
        },
      ],
    });

    const incoming = await within(panel).findByRole("list", { name: "1 lien entrant" });
    expect(incoming).toHaveTextContent("Élève de · Capitaine Orsen");
    const mentions = await within(panel).findByRole("list", {
      name: "Pages qui mentionnent cette page",
    });
    expect(within(mentions).getByRole("link", { name: "Hautbois" })).toBeInTheDocument();
    expect(incoming).not.toHaveTextContent("Hautbois");
  });

  it("links the page to another one with a type (E2E-09-02)", async () => {
    const { user, writes, panel } = await openIlsa();

    await user.click(within(panel).getByRole("button", { name: "Lier" }));
    const dialog = await screen.findByRole("dialog", { name: "Lier cette page" });
    expect(within(dialog).getByRole("button", { name: "Lier" })).toBeDisabled();
    await user.selectOptions(
      within(dialog).getByRole("combobox", { name: "Type de lien" }),
      "Mentor de",
    );
    await user.selectOptions(
      within(dialog).getByRole("combobox", { name: "Page liée" }),
      "Capitaine Orsen",
    );
    await user.click(within(dialog).getByRole("button", { name: "Lier" }));

    await waitFor(() => {
      expect(writes.created).toEqual([
        { sourceId: ids.ilsa, linkTypeId: linkIds.mentor, targetId: ids.orsen },
      ]);
    });
  });

  it("does not offer the page itself as a target", async () => {
    const { user, panel } = await openIlsa();

    await user.click(within(panel).getByRole("button", { name: "Lier" }));
    const dialog = await screen.findByRole("dialog", { name: "Lier cette page" });

    const targets = within(
      within(dialog).getByRole("combobox", { name: "Page liée" }),
    ).getAllByRole("option");
    expect(targets.map((o) => o.textContent)).not.toContain("Ilsa Varenne");
    expect(targets.map((o) => o.textContent)).toContain("Capitaine Orsen");
  });

  it("shows the reason when the backend refuses the link", async () => {
    const { user, panel } = await openIlsa();
    server.use(
      http.post("/api/v1/projects/{projectId}/links", ({ response }) =>
        response.untyped(Response.json({ status: 409, code: "link.duplicate" }, { status: 409 })),
      ),
    );

    await user.click(within(panel).getByRole("button", { name: "Lier" }));
    const dialog = await screen.findByRole("dialog", { name: "Lier cette page" });
    await user.selectOptions(
      within(dialog).getByRole("combobox", { name: "Type de lien" }),
      "Allié de",
    );
    await user.selectOptions(
      within(dialog).getByRole("combobox", { name: "Page liée" }),
      "Capitaine Orsen",
    );
    await user.click(within(dialog).getByRole("button", { name: "Lier" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("Ce lien existe déjà.");
  });

  it("deletes a link (E2E-09-04)", async () => {
    const { user, writes, panel } = await openIlsa({ outgoing: [orsenLink] });

    await user.click(
      await within(panel).findByRole("button", {
        name: "Supprimer le lien Allié de · Capitaine Orsen",
      }),
    );

    await waitFor(() => {
      expect(writes.deleted).toEqual([orsenLink.id]);
    });
  });
});

describe("LinkTypeView", () => {
  async function openMentor(path = "") {
    const { nodes: all } = mockProject({ nodes: nodes() });
    const writes = { puts: [] as unknown[] };
    server.use(
      http.get("/api/v1/projects/{projectId}/link-types/{id}", ({ response }) =>
        response(200).json(mentorView(all)),
      ),
      http.put("/api/v1/projects/{projectId}/link-types/{id}", async ({ request, response }) => {
        writes.puts.push(await request.json());
        return response(200).json(mentorView(all));
      }),
    );
    const user = userEvent.setup();
    await renderApp(`/p/${ids.project}/e/${linkIds.mentor}${path}`);
    const article = await screen.findByRole("article", { name: "Mentor de" });
    return { user, writes, article };
  }

  it("shows the description, the inverse label and the use (F09.R1)", async () => {
    const { article } = await openMentor();

    expect(
      await within(article).findByText("Un personnage qui en forme un autre."),
    ).toBeInTheDocument();
    expect(within(article).getByText("Élève de")).toBeInTheDocument();
    expect(within(article).getByText("0 lien utilise ce type.")).toBeInTheDocument();
  });

  it("saves the inverse label, an empty one meaning none (E2E-09-01)", async () => {
    const { user, writes, article } = await openMentor("?edit=true");

    const inverse = await within(article).findByRole("textbox", { name: "Libellé inverse" });
    await user.clear(inverse);
    await user.click(within(article).getByRole("button", { name: "Enregistrer" }));

    await waitFor(() => {
      expect(writes.puts).toEqual([
        { description: "Un personnage qui en forme un autre.", inverseName: null },
      ]);
    });
  });
});
