import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import { changeLanguage } from "../../shared/i18n/i18n";
import { testI18n } from "../../test/i18n";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { HelloPage } from "./HelloPage";

function mockInfo(mode: "desktop" | "server" = "desktop") {
  server.use(
    http.get("/api/info", ({ response }) =>
      response(200).json({ appVersion: "1.2.3", apiVersion: "v1", formatVersion: 1, mode }),
    ),
  );
}

describe("HelloPage", () => {
  afterEach(async () => {
    await changeLanguage("fr", testI18n);
  });

  it("renders the application name as main heading", () => {
    mockInfo();
    renderWithProviders(<HelloPage />);
    expect(screen.getByRole("heading", { level: 1, name: "Erable" })).toBeInTheDocument();
  });

  it("shows the backend version from /api/info", async () => {
    mockInfo("server");
    renderWithProviders(<HelloPage />);
    expect(await screen.findByText("Version 1.2.3 · API v1 · Serveur")).toBeInTheDocument();
  });

  it("formats plurals and numbers in French", () => {
    mockInfo();
    renderWithProviders(<HelloPage />);
    expect(screen.getByText("1 280 pages")).toBeInTheDocument();
  });

  it("switches the interface language without reloading", async () => {
    mockInfo();
    const user = userEvent.setup();
    renderWithProviders(<HelloPage />);

    await user.selectOptions(screen.getByLabelText("Langue de l'interface"), "English");

    await waitFor(() => {
      expect(screen.getByLabelText("Interface language")).toBeInTheDocument();
    });
    expect(screen.getByText("1,280 pages")).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Dark" })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe("en");
  });
});
