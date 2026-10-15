import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { useThemeStore } from "./theme";
import { ThemeSwitcher } from "./ThemeSwitcher";

describe("ThemeSwitcher", () => {
  afterEach(() => {
    useThemeStore.getState().setPreference("system");
  });

  it("applies the theme at once and saves it with the other preferences", async () => {
    let saved: unknown = null;
    server.use(
      http.get("/api/v1/me/preferences", ({ response }) =>
        response(200).json({ language: "en", theme: "system", shortcuts: { search: "Ctrl+K" } }),
      ),
      http.put("/api/v1/me/preferences", async ({ request, response }) => {
        saved = await request.json();
        return response(200).json({ language: "en", theme: "dark", shortcuts: {} });
      }),
    );
    renderWithProviders(<ThemeSwitcher />);

    await userEvent.click(screen.getByRole("radio", { name: "Sombre" }));

    expect(document.documentElement.dataset["theme"]).toBe("dark");
    await waitFor(() => {
      expect(saved).toEqual({ language: "en", theme: "dark", shortcuts: { search: "Ctrl+K" } });
    });
  });
});
