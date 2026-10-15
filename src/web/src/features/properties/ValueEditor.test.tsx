import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it } from "vitest";
import type { PropertyValue } from "../../shared/properties/types";
import { chroniquesNodes, ids, mockProject } from "../../test/projectMocks";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { definitions, structureIds, structureNodes } from "../../test/structureMocks";
import { toDefinition, type Definition } from "./definitions";
import { ValueEditor } from "./ValueEditor";

function Harness({
  definition,
  initial,
  onValue,
}: {
  readonly definition: Definition;
  readonly initial?: PropertyValue;
  readonly onValue: (value: PropertyValue | undefined) => void;
}) {
  const [value, setValue] = useState<PropertyValue | undefined>(initial);
  return (
    <ValueEditor
      projectId={ids.project}
      definition={definition}
      label="Valeur"
      value={value}
      onChange={(next) => {
        setValue(next);
        onValue(next);
      }}
    />
  );
}

function setup(definition: Definition, initial?: PropertyValue) {
  mockProject({ nodes: [...chroniquesNodes(), ...structureNodes()] });
  const values: (PropertyValue | undefined)[] = [];
  const user = userEvent.setup();
  renderWithProviders(
    <Harness
      definition={definition}
      {...(initial === undefined ? {} : { initial })}
      onValue={(v) => {
        values.push(v);
      }}
    />,
  );
  return { user, values, last: () => values.at(-1) };
}

const def = (overrides: Partial<Definition>): Definition => ({
  ...toDefinition(definitions.niveau),
  rules: [],
  ...overrides,
});

describe("ValueEditor (E2E-08-06)", () => {
  it("edits a bool with a switch", async () => {
    const { user, last } = setup(toDefinition(definitions.hostile), false);

    const toggle = screen.getByRole("switch", { name: "Valeur" });
    expect(toggle).toHaveAttribute("aria-checked", "false");
    await user.click(toggle);

    expect(toggle).toHaveAttribute("aria-checked", "true");
    expect(toggle).toHaveTextContent("Oui");
    expect(last()).toBe(true);
  });

  it("refuses letters in an int", async () => {
    const { user, last } = setup(def({ type: { scalar: "int", list: false } }));

    const input = screen.getByRole("textbox", { name: "Valeur" });
    await user.type(input, "4a2");

    expect(input).toHaveValue("42");
    expect(last()).toBe(42);
  });

  it("accepts a negative int and an empty field", async () => {
    const { user, last } = setup(def({ type: { scalar: "int", list: false } }), 3);

    const input = screen.getByRole("textbox", { name: "Valeur" });
    await user.clear(input);
    expect(last()).toBeUndefined();
    await user.type(input, "-7");

    expect(last()).toBe(-7);
  });

  it("accepts a decimal comma in a float", async () => {
    const { user, last } = setup(def({ type: { scalar: "float", list: false } }));

    await user.type(screen.getByRole("textbox", { name: "Valeur" }), "1,5x");

    expect(last()).toBe(1.5);
  });

  it("edits an enum with a list of its options", async () => {
    const { user, last } = setup(toDefinition(definitions.role));

    await user.selectOptions(screen.getByRole("combobox", { name: "Valeur" }), "Garde");

    expect(last()).toBe("garde");
  });

  it("edits strings, texts and dates", async () => {
    const string = setup(def({ type: { scalar: "string", list: false } }));
    await string.user.type(screen.getByRole("textbox", { name: "Valeur" }), "abc");
    expect(string.last()).toBe("abc");
  });

  it("edits a date", async () => {
    const { user, last } = setup(def({ type: { scalar: "date", list: false } }));

    const input = screen.getByLabelText("Valeur");
    await user.type(input, "2026-10-13");

    expect(last()).toBe("2026-10-13");
  });

  it("edits a multi-line text", async () => {
    const { user, last } = setup(def({ type: { scalar: "text", list: false } }));

    await user.type(screen.getByRole("textbox", { name: "Valeur" }), "a{Enter}b");

    expect(last()).toBe("a\nb");
  });

  it("offers only the pages of the in_category category for a ref", async () => {
    server.use(
      http.get("/api/v1/projects/{projectId}/categories/{id}/pages", ({ params, response }) =>
        response(200).json(
          params.id === structureIds.region
            ? [
                {
                  id: ids.hautbois,
                  name: "Hautbois",
                  path: [],
                  via: { id: structureIds.region, name: "Région" },
                },
                {
                  id: ids.colGele,
                  name: "Col gelé",
                  path: [],
                  via: { id: structureIds.region, name: "Région" },
                },
              ]
            : [],
        ),
      ),
    );
    const { user, last } = setup(toDefinition(definitions.region));

    const picker = screen.getByRole("combobox", { name: "Valeur" });
    await screen.findByRole("option", { name: "Hautbois" });
    expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual([
      "—",
      "Col gelé",
      "Hautbois",
    ]);
    await user.selectOptions(picker, "Hautbois");

    expect(last()).toBe(ids.hautbois);
  });

  it("edits a list item by item", async () => {
    const { user, last } = setup(def({ type: { scalar: "int", list: true } }), [1]);

    await user.click(screen.getByRole("button", { name: "Ajouter un élément" }));
    expect(last()).toEqual([1, 0]);
    await user.click(screen.getByRole("button", { name: "Retirer l’élément 1" }));

    expect(last()).toEqual([0]);
    expect(screen.getByRole("group", { name: "Valeur" })).toBeInTheDocument();
  });
});
