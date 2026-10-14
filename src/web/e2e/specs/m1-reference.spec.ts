import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";

// Infrastructure test (M1-07, no scenario ID): every test gets its own copy of the reference project.
test("smoke · Reset opens the reference project with its frozen IDs @pr", async ({
  reference,
  page,
}) => {
  const response = await page.request.get(
    `/api/v1/projects/${reference.id}/pages/${chroniques.ilsa}`,
  );
  const view = (await response.json()) as {
    element: { name: string; updatedByName: string };
    subPages: { name: string }[];
  };

  expect(reference.name).toBe(chroniques.name);
  expect(view.element.name).toBe("Ilsa Varenne");
  expect(view.element.updatedByName).toBe("Camille R.");
  expect(view.subPages.map((p) => p.name)).toEqual(["Dialogues", "Inventaire"]);
});
