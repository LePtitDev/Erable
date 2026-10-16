import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import { chroniquesNodes, ids, mockProject } from "../../test/projectMocks";
import { renderWithProviders } from "../../test/render";
import {
  ilsaStructure,
  mockStructure,
  structureIds,
  structureNodes,
} from "../../test/structureMocks";
import {
  draftOfStructure,
  toPageProperty,
  type PageProperty,
  type StructureDraft,
} from "../properties/pageProperties";
import { PageStructurePanel } from "./PageStructurePanel";

function Harness({
  properties,
  onDraft,
}: {
  readonly properties: readonly PageProperty[];
  readonly onDraft: (draft: StructureDraft) => void;
}) {
  const [draft, setDraft] = useState(draftOfStructure(ilsaStructure().categories, properties));
  return (
    <PageStructurePanel
      projectId={ids.project}
      pageId={ids.ilsa}
      properties={properties}
      draft={draft}
      attachments={[]}
      onDraft={(next) => {
        setDraft(next);
        onDraft(next);
      }}
    />
  );
}

function setup(properties = ilsaStructure().properties.map(toPageProperty)) {
  mockProject({ nodes: [...chroniquesNodes(), ...structureNodes()] });
  mockStructure();
  const drafts: StructureDraft[] = [];
  const user = userEvent.setup();
  renderWithProviders(
    <Harness
      properties={properties}
      onDraft={(draft) => {
        drafts.push(draft);
      }}
    />,
  );
  return { user, drafts };
}

describe("PageStructurePanel · properties", () => {
  it("shows an inherited value with its origin, and defines it on the page when edited (E2E-07-05)", async () => {
    const { user, drafts } = setup();
    const hostile = screen.getByRole("group", { name: "Hostile" });

    expect(within(hostile).getByRole("switch", { name: "Hostile" })).toHaveAttribute(
      "aria-checked",
      "false",
    );
    expect(hostile).toHaveTextContent("Valeur héritée de la catégorie PNJ");

    await user.click(within(hostile).getByRole("switch", { name: "Hostile" }));

    expect(drafts.at(-1)?.values.get(structureIds.hostile)).toBe(true);
  });

  it("restores the inherited value of a value the page overrides", async () => {
    const { user, drafts } = setup();
    const niveau = screen.getByRole("group", { name: "Niveau" });

    await user.click(within(niveau).getByRole("button", { name: "Rétablir la valeur héritée" }));

    expect(drafts.at(-1)?.values.has(structureIds.niveau)).toBe(false);
  });

  it("validates a typed value instantly and never blocks (E2E-05-12)", async () => {
    const { user } = setup();
    const niveau = screen.getByRole("group", { name: "Niveau" });
    expect(niveau).toHaveTextContent("Doit être inférieur ou égal à 60");

    const field = within(niveau).getByRole("textbox", { name: "Niveau" });
    await user.clear(field);
    await user.type(field, "55");

    expect(niveau).not.toHaveTextContent("Doit être inférieur ou égal à 60");
  });

  it("offers the candidates of a conflict and settles it with the chosen one (E2E-07-06)", async () => {
    const properties = ilsaStructure().properties.map((p) =>
      p.property.id === structureIds.hostile
        ? {
            ...p,
            value: null,
            origin: "conflict" as const,
            sources: [],
            candidates: [
              { categoryId: structureIds.pnj, categoryName: "PNJ", value: false },
              { categoryId: structureIds.garde, categoryName: "Montagnard", value: true },
            ],
          }
        : p,
    );
    const { user, drafts } = setup(properties.map(toPageProperty));
    const hostile = screen.getByRole("group", { name: "Hostile" });

    expect(hostile).toHaveTextContent("En conflit : PNJ (Non) / Montagnard (Oui)");
    await user.click(within(hostile).getByRole("button", { name: "Non" }));

    expect(drafts.at(-1)?.values.get(structureIds.hostile)).toBe(false);
  });
});

describe("PageStructurePanel · categories", () => {
  it("adds and removes a category of the page (E2E-07-04)", async () => {
    const { user, drafts } = setup();
    await user.click(screen.getByRole("tab", { name: "Catégories" }));

    await user.selectOptions(
      screen.getByRole("combobox", { name: "Ajouter une catégorie" }),
      "Garde",
    );
    await user.click(screen.getByRole("button", { name: "Ajouter la catégorie" }));

    expect(drafts.at(-1)?.categories).toEqual([structureIds.pnj, structureIds.garde]);

    await user.click(screen.getByRole("button", { name: "Retirer la catégorie PNJ" }));

    expect(drafts.at(-1)?.categories).toEqual([structureIds.garde]);
  });
});
