import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { HelloPage } from "./HelloPage";

describe("HelloPage", () => {
  it("renders the application name as main heading", () => {
    server.use(
      http.get("/api/info", ({ response }) =>
        response(200).json({
          appVersion: "1.2.3",
          apiVersion: "v1",
          formatVersion: 1,
          mode: "desktop",
        }),
      ),
    );
    renderWithProviders(<HelloPage />);
    expect(screen.getByRole("heading", { level: 1, name: "Erable" })).toBeInTheDocument();
  });

  it("shows the backend version from /api/info", async () => {
    server.use(
      http.get("/api/info", ({ response }) =>
        response(200).json({
          appVersion: "1.2.3",
          apiVersion: "v1",
          formatVersion: 1,
          mode: "server",
        }),
      ),
    );
    renderWithProviders(<HelloPage />);
    expect(await screen.findByText("v1.2.3 · API v1 · server")).toBeInTheDocument();
  });
});
