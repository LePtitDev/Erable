import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { accessOf, loadingAccess } from "../project/access";
import { AuthGate } from "./AuthGate";

function mockServer(signedIn: { current: boolean }) {
  server.use(
    http.get("/api/info", ({ response }) =>
      response(200).json({
        appVersion: "0.1.0",
        apiVersion: "v1",
        formatVersion: 4,
        mode: "server",
        folderProjects: false,
      }),
    ),
    http.get("/api/v1/me", ({ response }) =>
      signedIn.current
        ? response(200).json({
            id: "camille",
            name: "Camille R.",
            defaultProjectsFolder: "",
            isServerAdmin: false,
          })
        : response.untyped(
            HttpResponse.json({ status: 401, code: "auth.required" }, { status: 401 }),
          ),
    ),
    http.post("/api/v1/auth/login", async ({ request, response }) => {
      const body = (await request.json()) as { password: string };
      if (body.password !== "bon-mot-de-passe") {
        return response.untyped(
          HttpResponse.json({ status: 401, code: "auth.invalidCredentials" }, { status: 401 }),
        );
      }
      signedIn.current = true;
      return response(200).json({
        id: "camille",
        name: "Camille R.",
        isServerAdmin: false,
        expiresAt: "2026-10-13T20:00:00.000Z",
        token: null,
      });
    }),
  );
}

describe("AuthGate (M4-01)", () => {
  it("shows the login screen when the server answers 401", async () => {
    mockServer({ current: false });

    renderWithProviders(
      <AuthGate>
        <p>Application</p>
      </AuthGate>,
    );

    expect(await screen.findByRole("form", { name: "Connexion" })).toBeInTheDocument();
    expect(screen.queryByText("Application")).not.toBeInTheDocument();
  });

  it("says the same thing whatever was wrong (E2E-14-02)", async () => {
    mockServer({ current: false });
    const user = userEvent.setup();
    renderWithProviders(
      <AuthGate>
        <p>Application</p>
      </AuthGate>,
    );

    await user.type(await screen.findByLabelText("Identifiant"), "camille");
    await user.type(screen.getByLabelText("Mot de passe"), "mauvais");
    await user.click(screen.getByRole("button", { name: "Se connecter" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Identifiant ou mot de passe incorrect",
    );
  });

  it("shows the application once signed in", async () => {
    mockServer({ current: false });
    const user = userEvent.setup();
    renderWithProviders(
      <AuthGate>
        <p>Application</p>
      </AuthGate>,
    );

    await user.type(await screen.findByLabelText("Identifiant"), "camille");
    await user.type(screen.getByLabelText("Mot de passe"), "bon-mot-de-passe");
    await user.click(screen.getByRole("button", { name: "Se connecter" }));

    expect(await screen.findByText("Application")).toBeInTheDocument();
  });

  it("renders at once on the desktop, which has no accounts", async () => {
    server.use(
      http.get("/api/info", ({ response }) =>
        response(200).json({
          appVersion: "0.1.0",
          apiVersion: "v1",
          formatVersion: 4,
          mode: "desktop",
          folderProjects: true,
        }),
      ),
    );

    renderWithProviders(
      <AuthGate>
        <p>Application</p>
      </AuthGate>,
    );

    expect(await screen.findByText("Application")).toBeInTheDocument();
  });
});

describe("project access", () => {
  it("lets a reader read only", () => {
    expect(accessOf("reader")).toEqual({
      role: "reader",
      canEdit: false,
      isAdmin: false,
      roleKnown: true,
    });
  });

  it("lets an editor edit, and only an administrator manage the members", () => {
    expect(accessOf("editor")).toEqual({
      role: "editor",
      canEdit: true,
      isAdmin: false,
      roleKnown: true,
    });
    expect(accessOf("admin")).toEqual({
      role: "admin",
      canEdit: true,
      isAdmin: true,
      roleKnown: true,
    });
  });

  it("lets the user read while the role is loading", () => {
    expect(loadingAccess).toEqual({
      role: "reader",
      canEdit: false,
      isAdmin: false,
      roleKnown: false,
    });
  });
});
