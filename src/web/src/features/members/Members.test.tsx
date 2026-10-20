import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { requireElementId } from "../../shared/ids/elementId";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { Members } from "./MembersView";

const project = requireElementId("3F2A9C14E07B5D21");

interface Member {
  userId: string;
  name: string;
  role: "reader" | "editor" | "admin";
  disabled: boolean;
  isMe: boolean;
}

function mockMembers(initial: Member[], isServerAdmin = false) {
  const members = [...initial];
  const puts: { userId: string; role: string }[] = [];
  server.use(
    http.get("/api/v1/me", ({ response }) =>
      response(200).json({
        id: "admin",
        name: "Admin",
        defaultProjectsFolder: "",
        isServerAdmin,
      }),
    ),
    http.get("/api/v1/projects/{projectId}/members", ({ response }) => response(200).json(members)),
    http.put(
      "/api/v1/projects/{projectId}/members/{userId}",
      async ({ params, request, response }) => {
        const body = await request.json();
        puts.push({ userId: params["userId"], role: body.role });
        const found = members.find((m) => m.userId === params["userId"]) ?? admin;
        found.role = body.role;
        return response(200).json(found);
      },
    ),
    http.delete("/api/v1/projects/{projectId}/members/{userId}", ({ response }) =>
      response.untyped(
        HttpResponse.json({ status: 409, code: "member.lastAdmin" }, { status: 409 }),
      ),
    ),
  );
  return puts;
}

const admin: Member = {
  userId: "admin",
  name: "Admin",
  role: "admin",
  disabled: false,
  isMe: true,
};
const lea: Member = { userId: "lea", name: "Léa M.", role: "reader", disabled: false, isMe: false };

describe("Members", () => {
  it("lists the members with their role", async () => {
    mockMembers([admin, lea]);

    renderWithProviders(<Members projectId={project} />);

    expect(await screen.findByRole("combobox", { name: "Rôle de Léa M." })).toHaveValue("reader");
    expect(screen.getByRole("combobox", { name: "Rôle de Admin" })).toHaveValue("admin");
  });

  it("changes a role through the select", async () => {
    const puts = mockMembers([admin, lea]);
    const user = userEvent.setup();
    renderWithProviders(<Members projectId={project} />);

    await user.selectOptions(
      await screen.findByRole("combobox", { name: "Rôle de Léa M." }),
      "editor",
    );

    await waitFor(() => {
      expect(puts).toEqual([{ userId: "lea", role: "editor" }]);
    });
  });

  it("says why the last administrator cannot be removed", async () => {
    mockMembers([admin]);
    const user = userEvent.setup();
    renderWithProviders(<Members projectId={project} />);

    await user.click(await screen.findByRole("button", { name: "Retirer Admin du projet" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("au moins un administrateur");
  });

  it("offers the creation of an account to server administrators only", async () => {
    mockMembers([admin]);
    const { unmount } = renderWithProviders(<Members projectId={project} />);
    await screen.findByRole("combobox", { name: "Rôle de Admin" });
    expect(screen.queryByRole("button", { name: "Créer un compte" })).not.toBeInTheDocument();
    unmount();

    mockMembers([admin], true);
    renderWithProviders(<Members projectId={project} />);
    expect(await screen.findByRole("button", { name: "Créer un compte" })).toBeInTheDocument();
  });
});
