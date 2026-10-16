import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import type { DiffRow, RevisionDiff, RevisionItem } from "../../shared/api/history";
import { http, server } from "../../test/msw";
import { ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";
import { foldRows } from "./foldRows";

const revision = (n: number, overrides: Partial<RevisionItem> = {}): RevisionItem => ({
  revision: n,
  author: "camille",
  authorName: "Camille R.",
  at: `2026-10-13T0${String(n)}:00:00.000Z`,
  changes: n === 1 ? ["created"] : ["text"],
  restoredFrom: null,
  ...overrides,
});

const line = (number: number, text: string, changed = false) => ({
  number,
  text,
  pieces: [{ text, changed }],
});

function mockHistory(revisions: RevisionItem[], diff?: RevisionDiff) {
  const restored: number[] = [];
  const diffs: string[] = [];
  server.use(
    http.get("/api/v1/projects/{projectId}/elements/{id}/revisions", ({ response }) =>
      response(200).json(revisions),
    ),
    http.get("/api/v1/projects/{projectId}/elements/{id}/revisions/diff", ({ query, response }) => {
      diffs.push(`${query.get("from")}-${query.get("to")}`);
      if (diff === undefined) throw new Error("No diff mocked.");
      return response(200).json(diff);
    }),
    http.post(
      "/api/v1/projects/{projectId}/elements/{id}/revisions/{revision}/restore",
      ({ params, response }) => {
        restored.push(Number(params.revision));
        return response(200).json({
          revision: revisions.length + 1,
          skippedCategories: 1,
          skippedValues: 0,
        });
      },
    ),
  );
  return { restored, diffs };
}

const sampleDiff: RevisionDiff = {
  from: revision(1),
  to: revision(4),
  nameChanged: true,
  fromName: "Hautbois",
  toName: "Brumeval",
  text: [
    { kind: "unchanged", old: line(1, "Bourg"), new: line(1, "Bourg") },
    { kind: "removed", old: line(2, "ancien"), new: null },
    {
      kind: "modified",
      old: { number: 3, text: "Un puits", pieces: [{ text: "Un puits", changed: false }] },
      new: {
        number: 2,
        text: "Un puits profond",
        pieces: [
          { text: "Un puits", changed: false },
          { text: " profond", changed: true },
        ],
      },
    },
    { kind: "added", old: null, new: line(3, "Un marché") },
  ],
  categoriesAdded: [{ id: ids.categoryPnj, name: "PNJ" }],
  categoriesRemoved: [],
  values: [{ propertyId: ids.ilsa, propertyName: "Niveau", old: 10, new: 15 }],
};

describe("foldRows", () => {
  const row = (kind: DiffRow["kind"], n: number): DiffRow => ({
    kind,
    old: line(n, String(n)),
    new: line(n, String(n)),
  });

  it("keeps the changes with two lines of context and folds the rest", () => {
    const rows = [
      ...Array.from({ length: 10 }, (_, i) => row("unchanged", i + 1)),
      row("added", 11),
      ...Array.from({ length: 10 }, (_, i) => row("unchanged", i + 12)),
    ];

    const folded = foldRows(rows);

    expect(folded.map((r) => (r === null ? "…" : r.old?.text))).toEqual([
      "…",
      "9",
      "10",
      "11",
      "12",
      "13",
      "…",
    ]);
  });

  it("shows everything when everything is near a change", () => {
    const rows = [row("unchanged", 1), row("removed", 2), row("unchanged", 3)];

    expect(foldRows(rows)).toEqual(rows);
  });
});

describe("HistoryView", () => {
  async function open(search = "") {
    mockProject();
    return renderApp(`/p/${ids.project}/e/${ids.hautbois}/history${search}`);
  }

  it("lists the revisions newest first with author and date (E2E-13-01)", async () => {
    mockHistory([revision(4), revision(3), revision(2), revision(1)]);
    await open();

    const list = await screen.findByRole("list", { name: "Révisions" });
    const items = within(list).getAllByRole("listitem");

    expect(items).toHaveLength(4);
    expect(items[0]).toHaveTextContent("Révision 4");
    expect(items[0]).toHaveTextContent("Texte");
    expect(items[0]).toHaveTextContent("Camille R.");
    expect(items[3]).toHaveTextContent("Création");
    // The newest revision is the current state: nothing to restore.
    expect(within(items[0] as HTMLElement).queryByRole("button")).not.toBeInTheDocument();
    expect(
      within(items[1] as HTMLElement).getByRole("button", { name: "Restaurer la révision 3" }),
    ).toBeInTheDocument();
  });

  it("marks a restoration with the revision it brings back (F13.R3)", async () => {
    mockHistory([revision(3, { restoredFrom: 1, changes: ["text"] }), revision(2), revision(1)]);
    await open();

    expect(await screen.findByText("Restauration de la révision 1")).toBeInTheDocument();
  });

  it("compares two ticked revisions and writes them in the URL (E2E-13-02)", async () => {
    const { diffs } = mockHistory([revision(4), revision(3), revision(2), revision(1)], sampleDiff);
    const user = userEvent.setup();
    const { router } = await open();

    await user.click(await screen.findByRole("checkbox", { name: "Comparer la révision 4" }));
    expect(
      screen.getByText("Cochez deux révisions pour voir leurs différences."),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("checkbox", { name: "Comparer la révision 1" }));

    const diff = await screen.findByRole("region", {
      name: "Différences entre les révisions 1 et 4",
    });
    expect(router.state.location.search).toEqual({ from: 1, to: 4 });
    expect(diffs).toEqual(["1-4"]);
    expect(within(diff).getByText("Brumeval", { selector: "ins" })).toBeInTheDocument();
    expect(within(diff).getByText("Hautbois", { selector: "del" })).toBeInTheDocument();
    expect(within(diff).getByText("PNJ", { selector: "ins" })).toBeInTheDocument();
    expect(within(diff).getByText("10", { selector: "del" })).toBeInTheDocument();
    expect(within(diff).getByText("15", { selector: "ins" })).toBeInTheDocument();
    expect(within(diff).getByText("ancien", { selector: "del" })).toBeInTheDocument();
    expect(within(diff).getByText("Un marché", { selector: "ins" })).toBeInTheDocument();
    expect(
      within(diff).getByText("profond", { selector: "ins", exact: false }),
    ).toBeInTheDocument();
    // The sign tells added from removed without the colour.
    expect(within(diff).getAllByLabelText("Ajouté").length).toBeGreaterThan(0);
    expect(within(diff).getAllByLabelText("Supprimé").length).toBeGreaterThan(0);
  });

  it("reads the compared revisions from the URL", async () => {
    mockHistory([revision(2), revision(1)], { ...sampleDiff, to: revision(2) });
    await open("?from=1&to=2");

    expect(
      await screen.findByRole("region", { name: "Différences entre les révisions 1 et 2" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("checkbox", { name: "Comparer la révision 1" })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: "Comparer la révision 2" })).toBeChecked();
  });

  it("restores a revision and says what could not come back (E2E-13-03)", async () => {
    const { restored } = mockHistory([revision(4), revision(3), revision(2), revision(1)]);
    const user = userEvent.setup();
    await open();

    await user.click(await screen.findByRole("button", { name: "Restaurer la révision 2" }));

    await waitFor(() => {
      expect(restored).toEqual([2]);
    });
    const status = await screen.findByRole("status");
    expect(status).toHaveTextContent("La révision 5 a été créée.");
    expect(status).toHaveTextContent("(1)");
  });

  it("opens the history from the page screen", async () => {
    mockProject();
    mockHistory([revision(1)]);
    const user = userEvent.setup();
    const { router } = await renderApp(`/p/${ids.project}/e/${ids.hautbois}`);

    await user.click(await screen.findByRole("button", { name: "Historique" }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe(`/p/${ids.project}/e/${ids.hautbois}/history`);
    });
  });
});
