import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { roots } from "../../shared/ids/roots";
import type { DeleteImpactView, TrashEntryView } from "../../shared/api/trash";
import { http, server } from "../../test/msw";
import { ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";

const impactOf = (overrides: Partial<DeleteImpactView>): DeleteImpactView => ({
  id: ids.orsen,
  kind: "page",
  name: "Capitaine Orsen",
  referencedBy: [],
  subPages: 0,
  descendants: 0,
  links: 0,
  pages: 0,
  values: 0,
  ...overrides,
});

function mockDeletion(impact: DeleteImpactView) {
  const writes = { deleted: [] as string[] };
  server.use(
    http.get("/api/v1/projects/{projectId}/elements/{id}/impact", ({ response }) =>
      response(200).json(impact),
    ),
    http.delete("/api/v1/projects/{projectId}/elements/{id}", ({ params, response }) => {
      writes.deleted.push(params.id);
      return response(204).empty();
    }),
  );
  return writes;
}

async function openOrsenAndDelete(impact: DeleteImpactView) {
  mockProject();
  const writes = mockDeletion(impact);
  const user = userEvent.setup();
  await renderApp(`/p/${ids.project}/e/${ids.orsen}`);
  const tree = await screen.findByRole("tree", { name: "Explorateur" });
  const item = await within(tree).findByRole("treeitem", { name: "Capitaine Orsen" });
  await user.click(item);
  await user.keyboard("{Delete}");
  const dialog = await screen.findByRole("alertdialog");
  return { user, writes, dialog };
}

describe("DeleteDialog", () => {
  it("lists the pages that mention the element before deleting it (E2E-12-01)", async () => {
    const { dialog } = await openOrsenAndDelete(
      impactOf({ referencedBy: [{ id: ids.ilsa, name: "Ilsa Varenne" }] }),
    );

    expect(
      await within(dialog).findByText("1 page fait référence à cet élément : Ilsa Varenne"),
    ).toBeInTheDocument();
    expect(dialog).toHaveAccessibleName("Supprimer « Capitaine Orsen » ?");
  });

  it("mentions the sub-pages that go with the page (E2E-12-03)", async () => {
    const { dialog } = await openOrsenAndDelete(
      impactOf({ id: ids.ilsa, name: "Ilsa Varenne", subPages: 2, descendants: 2 }),
    );

    expect(
      await within(dialog).findByText(
        "« Ilsa Varenne » et 2 sous-pages seront placées dans la corbeille.",
      ),
    ).toBeInTheDocument();
  });

  it("tells how many links use a link type (E2E-09-05)", async () => {
    const { dialog } = await openOrsenAndDelete(
      impactOf({ kind: "linkType", name: "Situé à", links: 1 }),
    );

    expect(
      await within(dialog).findByText("1 lien utilise ce type et sera supprimé."),
    ).toBeInTheDocument();
  });

  it("says where an attachment is used besides its own page (E2E-06-06)", async () => {
    const { dialog } = await openOrsenAndDelete(
      impactOf({
        kind: "attachment",
        name: "carte_hautbois.webp",
        referencedBy: [{ id: ids.ilsa, name: "Ilsa Varenne" }],
      }),
    );

    expect(
      await within(dialog).findByText("Utilisée dans 1 autre page : Ilsa Varenne"),
    ).toBeInTheDocument();
  });

  it("deletes only when confirmed", async () => {
    const { user, writes, dialog } = await openOrsenAndDelete(impactOf({}));
    expect(writes.deleted).toEqual([]);

    await user.click(await within(dialog).findByRole("button", { name: "Mettre à la corbeille" }));

    await waitFor(() => {
      expect(writes.deleted).toEqual([ids.orsen]);
    });
    await waitFor(() => {
      expect(screen.queryByRole("alertdialog")).toBeNull();
    });
  });

  it("deletes nothing when cancelled", async () => {
    const { user, writes, dialog } = await openOrsenAndDelete(impactOf({}));

    await user.click(within(dialog).getByRole("button", { name: "Annuler" }));

    expect(writes.deleted).toEqual([]);
  });
});

const entry = (overrides: Partial<TrashEntryView>): TrashEntryView => ({
  id: ids.orsen,
  kind: "page",
  name: "Capitaine Orsen",
  deletedAt: "2026-10-13T07:00:00.000Z",
  deletedBy: "Camille",
  descendants: 0,
  path: [
    { id: roots.pages, kind: "root", name: "Pages" },
    { id: ids.pnj, kind: "folder", name: "PNJ" },
  ],
  parentAvailable: true,
  ...overrides,
});

async function openTrash(entries: TrashEntryView[]) {
  mockProject();
  const writes = { restored: [] as string[], purged: [] as string[], emptied: 0 };
  server.use(
    http.get("/api/v1/projects/{projectId}/trash", ({ response }) => response(200).json(entries)),
    http.get("/api/v1/projects/{projectId}", ({ response }) =>
      response(200).json({
        id: ids.project,
        name: "Chroniques d’Aube",
        folder: "/w/chroniques",
        formatVersion: 3,
        contentLanguage: "fr",
        createdAt: "2026-03-10T09:00:00.000Z",
        myRole: "admin",
      }),
    ),
    http.post("/api/v1/projects/{projectId}/trash/{id}/restore", ({ params, response }) => {
      writes.restored.push(params.id);
      return response(200).json({
        id: ids.orsen,
        kind: "page",
        name: "Capitaine Orsen",
        parentId: roots.pages,
        parentName: "Pages",
        relocated: true,
        restored: 1,
      });
    }),
    http.delete("/api/v1/projects/{projectId}/trash/{id}", ({ params, response }) => {
      writes.purged.push(params.id);
      return response(200).json({ removed: 1 });
    }),
    http.delete("/api/v1/projects/{projectId}/trash", ({ response }) => {
      writes.emptied += 1;
      return response(200).json({ removed: 1 });
    }),
  );
  const user = userEvent.setup();
  await renderApp(`/p/${ids.project}/trash`);
  const view = await screen.findByRole("region", { name: "Corbeille" });
  return { user, writes, view };
}

describe("TrashView", () => {
  it("lists the deletions with their place and what came along (E2E-12-03)", async () => {
    const { view } = await openTrash([entry({ descendants: 2 })]);

    const list = await within(view).findByRole("list", { name: "Éléments supprimés" });
    const row = within(list).getByRole("listitem");
    expect(row).toHaveTextContent("Capitaine Orsen");
    expect(row).toHaveTextContent("avec 2 éléments");
    expect(row).toHaveTextContent("Emplacement d’origine : Pages / PNJ");
  });

  it("says the trash is empty", async () => {
    const { view } = await openTrash([]);

    expect(await within(view).findByText("La corbeille est vide.")).toBeInTheDocument();
    expect(within(view).getByRole("button", { name: "Vider la corbeille" })).toBeDisabled();
  });

  it("restores an entry and says when it went to the root (E2E-12-02, E2E-12-05)", async () => {
    const { user, writes, view } = await openTrash([entry({ parentAvailable: false })]);

    expect(
      await within(view).findByText(/n’existe plus : il sera replacé à la racine/),
    ).toBeInTheDocument();
    await user.click(
      await within(view).findByRole("button", { name: "Restaurer Capitaine Orsen" }),
    );

    expect(await within(view).findByRole("status")).toHaveTextContent(
      "« Capitaine Orsen » a été restauré sous « Pages » : son emplacement d’origine n’existe plus.",
    );
    expect(writes.restored).toEqual([ids.orsen]);
  });

  it("asks to type the name of the project before emptying (E2E-12-06)", async () => {
    const { user, writes, view } = await openTrash([entry({})]);

    await user.click(await within(view).findByRole("button", { name: "Vider la corbeille" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Vider la corbeille ?" });
    const confirm = within(dialog).getByRole("button", { name: "Vider définitivement" });
    expect(confirm).toBeDisabled();
    await user.type(within(dialog).getByRole("textbox"), "Chroniques d’Aub");
    expect(confirm).toBeDisabled();
    await user.type(within(dialog).getByRole("textbox"), "e");
    await user.click(confirm);

    await waitFor(() => {
      expect(writes.emptied).toBe(1);
    });
  });

  it("deletes one entry for good after a confirmation", async () => {
    const { user, writes, view } = await openTrash([entry({})]);

    await user.click(
      await within(view).findByRole("button", { name: "Supprimer définitivement Capitaine Orsen" }),
    );
    const dialog = await screen.findByRole("alertdialog");
    expect(writes.purged).toEqual([]);
    await user.click(within(dialog).getByRole("button", { name: "Supprimer définitivement" }));

    await waitFor(() => {
      expect(writes.purged).toEqual([ids.orsen]);
    });
  });
});
