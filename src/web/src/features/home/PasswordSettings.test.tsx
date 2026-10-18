import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { PasswordSettings } from "./PasswordSettings";

function mockServer(mode: "server" | "desktop") {
  const sent: unknown[] = [];
  const info = { served: false };
  server.use(
    http.get("/api/info", ({ response }) => {
      info.served = true;
      return response(200).json({
        appVersion: "0.1.0",
        apiVersion: "v1",
        formatVersion: 4,
        mode,
        folderProjects: mode === "desktop",
      });
    }),
    http.put("/api/v1/auth/password", async ({ request, response }) => {
      const body = await request.json();
      sent.push(body);
      if (body.currentPassword !== "camille-pass-1") {
        return response.untyped(
          HttpResponse.json(
            { status: 403, code: "auth.wrongCurrentPassword" },
            { status: 403, headers: { "Content-Type": "application/problem+json" } },
          ),
        );
      }
      return response(200).json({
        id: "camille",
        name: "Camille R.",
        isServerAdmin: false,
        expiresAt: "2026-10-13T20:00:00Z",
        token: null,
      });
    }),
  );
  return { sent, info };
}

async function fill(
  user: ReturnType<typeof userEvent.setup>,
  current: string,
  next: string,
  confirm = next,
) {
  await user.type(await screen.findByLabelText("Mot de passe actuel"), current);
  await user.type(screen.getByLabelText("Nouveau mot de passe"), next);
  await user.type(screen.getByLabelText("Confirmer le nouveau mot de passe"), confirm);
}

describe("PasswordSettings", () => {
  it("changes the password with the current one", async () => {
    const { sent } = mockServer("server");
    const user = userEvent.setup();
    renderWithProviders(<PasswordSettings />);

    await fill(user, "camille-pass-1", "camille-pass-2");
    await user.click(screen.getByRole("button", { name: "Changer le mot de passe" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Mot de passe changé.");
    expect(sent).toEqual([{ currentPassword: "camille-pass-1", newPassword: "camille-pass-2" }]);
    expect(screen.getByLabelText("Mot de passe actuel")).toHaveValue("");
  });

  it("says when the current password is wrong", async () => {
    mockServer("server");
    const user = userEvent.setup();
    renderWithProviders(<PasswordSettings />);

    await fill(user, "not-mine", "camille-pass-2");
    await user.click(screen.getByRole("button", { name: "Changer le mot de passe" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Le mot de passe actuel est incorrect.",
    );
  });

  it("cannot submit when the confirmation differs", async () => {
    mockServer("server");
    const user = userEvent.setup();
    renderWithProviders(<PasswordSettings />);

    await fill(user, "camille-pass-1", "camille-pass-2", "camille-pass-3");

    expect(screen.getByRole("alert")).toHaveTextContent(
      "Les deux mots de passe ne correspondent pas.",
    );
    expect(screen.getByRole("button", { name: "Changer le mot de passe" })).toBeDisabled();
  });

  it("is not shown on the desktop, which has no account", async () => {
    const { info } = mockServer("desktop");
    const { container } = renderWithProviders(<PasswordSettings />);

    await waitFor(() => {
      expect(info.served).toBe(true);
    });

    expect(container).toBeEmptyDOMElement();
  });
});
