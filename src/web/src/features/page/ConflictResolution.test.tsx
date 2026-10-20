import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { ApiError, type ProblemDetails } from "../../shared/api/client";
import {
  mergeConflictOf,
  resolvedMarkdown,
  textOf,
  type Choice,
  type MergeConflict,
} from "../../shared/api/mergeConflict";
import { requireElementId } from "../../shared/ids/elementId";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { ConflictResolution, type Resolution } from "./ConflictResolution";

const projectId = requireElementId("CA0B0E5EC0A5D1E1");
const niveau = requireElementId("44B107E9C2A63F58");

const problem = {
  status: 409,
  code: "page.revisionConflict",
  current: { revision: 7, markdown: "ignored" },
  blocks: [
    { kind: "stable", text: "## Histoire\n\n" },
    {
      kind: "conflict",
      base: "Née à Hautbois.\n",
      mine: "Ilsa rejoint l’Ordre.\n",
      theirs: "Ilsa est née à Hautbois.\n",
    },
    { kind: "stable", text: "\n## Rôle\n" },
  ],
  valueConflicts: [{ propertyId: niveau, base: 10, mine: 30, theirs: 40 }],
  mergedStructure: { categories: [], values: { [niveau]: 40 } },
  mergedWith: [{ id: "camille", name: "Camille R." }],
};

function conflictOf(body: ProblemDetails = problem): MergeConflict {
  const parsed = mergeConflictOf(new ApiError(409, body));
  if (parsed === null) throw new Error("not a conflict");
  return parsed;
}

describe("mergeConflictOf", () => {
  it("reads the blocks, the revision and the other editors", () => {
    const conflict = conflictOf();

    expect(conflict.currentRevision).toBe(7);
    expect(conflict.blocks).toHaveLength(3);
    expect(conflict.mergedWith).toEqual([{ id: "camille", name: "Camille R." }]);
    expect(conflict.valueConflicts).toEqual([{ propertyId: niveau, mine: 30, theirs: 40 }]);
    expect(conflict.categories).toEqual([]);
    expect(conflict.values).toEqual({ [niveau]: 40 });
  });

  it("is null for a conflict without details, and for other errors", () => {
    expect(mergeConflictOf(new ApiError(409, { code: "page.revisionConflict" }))).toBeNull();
    expect(mergeConflictOf(new ApiError(409, { ...problem, code: "other" }))).toBeNull();
    expect(mergeConflictOf(new Error("network"))).toBeNull();
  });

  it("builds the page from the choices, and only when every passage is settled", () => {
    const conflict = conflictOf();
    const mine = new Map<number, Choice>([[1, { kind: "mine" }]]);

    expect(resolvedMarkdown(conflict, new Map())).toBeNull();
    expect(resolvedMarkdown(conflict, mine)).toBe(
      "## Histoire\n\nIlsa rejoint l’Ordre.\n\n## Rôle\n",
    );
    expect(resolvedMarkdown(conflict, new Map([[1, { kind: "theirs" }]]))).toBe(
      "## Histoire\n\nIlsa est née à Hautbois.\n\n## Rôle\n",
    );
  });

  it("keeps both versions, each on its own lines", () => {
    const block = { kind: "conflict" as const, base: "", mine: "un", theirs: "deux\n" };

    expect(textOf(block, { kind: "both" })).toBe("un\ndeux\n");
    expect(textOf(block, { kind: "custom", text: "à la main" })).toBe("à la main");
  });
});

describe("ConflictResolution (E2E-14-08)", () => {
  function mock() {
    server.use(
      http.get("/api/v1/projects/{projectId}/properties", ({ response }) => response(200).json([])),
    );
  }

  it("shows both versions of the passage and saves the chosen one", async () => {
    mock();
    const user = userEvent.setup();
    const onResolve = vi.fn<(resolution: Resolution) => void>();
    renderWithProviders(
      <ConflictResolution
        projectId={projectId}
        conflict={conflictOf({ ...problem, valueConflicts: [] })}
        busy={false}
        onResolve={onResolve}
        onCancel={() => undefined}
      />,
    );

    expect(await screen.findByText("Ilsa rejoint l’Ordre.")).toBeInTheDocument();
    expect(screen.getByText("Ilsa est née à Hautbois.")).toBeInTheDocument();
    const save = screen.getByRole("button", { name: "Enregistrer" });
    expect(save).toBeDisabled();

    await user.click(screen.getByRole("radio", { name: "Garder la mienne" }));
    await user.click(save);

    expect(onResolve).toHaveBeenCalledOnce();
    expect(onResolve.mock.calls[0]?.[0]).toMatchObject({
      markdown: "## Histoire\n\nIlsa rejoint l’Ordre.\n\n## Rôle\n",
      revision: 7,
    });
  });

  it("asks for the properties changed on both sides too", async () => {
    mock();
    const user = userEvent.setup();
    const onResolve = vi.fn<(resolution: Resolution) => void>();
    renderWithProviders(
      <ConflictResolution
        projectId={projectId}
        conflict={conflictOf()}
        busy={false}
        onResolve={onResolve}
        onCancel={() => undefined}
      />,
    );

    await user.click(await screen.findByRole("radio", { name: "Garder la mienne" }));
    expect(screen.getByRole("button", { name: "Enregistrer" })).toBeDisabled();
    await user.click(screen.getByRole("radio", { name: /Garder ma valeur : 30/ }));
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));

    expect(onResolve.mock.calls[0]?.[0].properties).toEqual([{ propertyId: niveau, value: 30 }]);
  });

  it("lets the user write the passage by hand", async () => {
    mock();
    const user = userEvent.setup();
    const onResolve = vi.fn<(resolution: Resolution) => void>();
    renderWithProviders(
      <ConflictResolution
        projectId={projectId}
        conflict={conflictOf({ ...problem, valueConflicts: [] })}
        busy={false}
        onResolve={onResolve}
        onCancel={() => undefined}
      />,
    );

    await user.click(await screen.findByRole("radio", { name: "Modifier à la main" }));
    const field = screen.getByRole("textbox", { name: "Texte retenu pour le passage 1" });
    await user.clear(field);
    await user.type(field, "Texte commun.");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));

    expect(onResolve.mock.calls[0]?.[0].markdown).toBe("## Histoire\n\nTexte commun.\n## Rôle\n");
  });
});
