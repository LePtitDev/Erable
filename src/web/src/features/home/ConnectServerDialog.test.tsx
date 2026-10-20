import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { ConnectServerDialog } from "./ConnectServerDialog";

const probe = {
  url: "https://wiki.example.org",
  host: "wiki.example.org",
  compatible: true,
  serverAppVersion: "0.1.0",
  serverApiVersion: "v1",
  serverFormatVersion: 4,
  appVersion: "0.1.0",
  appApiVersion: "v1",
  appFormatVersion: 4,
};

describe("ConnectServerDialog (E2E-01-06, E2E-16-06)", () => {
  it("checks the server first, then signs in", async () => {
    const user = userEvent.setup();
    const onConnected = vi.fn();
    const login = vi.fn<(body: unknown) => void>();
    server.use(
      http.post("/api/v1/servers/probe", ({ response }) => response(200).json(probe)),
      http.post("/api/v1/servers/connect", async ({ request, response }) => {
        login(await request.json());
        return response(201).json({
          id: "A1B2C3D4E5F60718",
          name: "wiki.example.org",
          url: probe.url,
          userId: "camille",
          userName: "Camille R.",
          connected: true,
          lastUsedAt: "2026-10-13T08:00:00.000Z",
          projectIds: [],
        });
      }),
    );
    renderWithProviders(
      <ConnectServerDialog open onOpenChange={() => undefined} onConnected={onConnected} />,
    );

    await user.type(await screen.findByLabelText("Adresse du serveur"), "wiki.example.org");
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    await user.type(await screen.findByLabelText("Identifiant"), "camille");
    await user.type(screen.getByLabelText("Mot de passe"), "mot-de-passe");
    await user.click(screen.getByRole("button", { name: "Se connecter" }));

    await vi.waitFor(() => {
      expect(onConnected).toHaveBeenCalledOnce();
    });
    expect(login).toHaveBeenCalledWith({
      url: probe.url,
      login: "camille",
      password: "mot-de-passe",
    });
  });

  it("explains a server of another version and goes no further", async () => {
    const user = userEvent.setup();
    const connect = vi.fn();
    server.use(
      http.post("/api/v1/servers/probe", ({ response }) =>
        response(200).json({
          ...probe,
          compatible: false,
          serverApiVersion: "v9",
          serverAppVersion: "9.0.0",
        }),
      ),
      http.post("/api/v1/servers/connect", ({ response }) => {
        connect();
        return response.untyped(HttpResponse.json({}, { status: 500 }));
      }),
    );
    renderWithProviders(
      <ConnectServerDialog open onOpenChange={() => undefined} onConnected={() => undefined} />,
    );

    await user.type(await screen.findByLabelText("Adresse du serveur"), "wiki.example.org");
    await user.click(screen.getByRole("button", { name: "Continuer" }));

    const message = await screen.findByRole("alert");
    expect(message).toHaveTextContent("l’API v9 (Erable 9.0.0)");
    expect(message).toHaveTextContent("l’API v1 (Erable 0.1.0)");
    expect(screen.queryByLabelText("Mot de passe")).not.toBeInTheDocument();
    expect(connect).not.toHaveBeenCalled();
  });

  it("says when the address is not a server", async () => {
    const user = userEvent.setup();
    server.use(
      http.post("/api/v1/servers/probe", ({ response }) =>
        response.untyped(
          HttpResponse.json({ status: 502, code: "server.unreachable" }, { status: 502 }),
        ),
      ),
    );
    renderWithProviders(
      <ConnectServerDialog open onOpenChange={() => undefined} onConnected={() => undefined} />,
    );

    await user.type(await screen.findByLabelText("Adresse du serveur"), "nulle-part.example");
    await user.click(screen.getByRole("button", { name: "Continuer" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Ce serveur ne répond pas");
  });
});
