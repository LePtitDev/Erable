import { screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { requireElementId } from "../../shared/ids/elementId";
import {
  clearPresence,
  setPresenceUsers,
  type PresenceUser,
} from "../../shared/realtime/presenceStore";
import { http, server } from "../../test/msw";
import { renderWithProviders } from "../../test/render";
import { EditingBanner, ElementPresence, PresenceAvatars } from "./Presence";

// The components are rendered outside any router: no project in the address, so « moi » is the user of the machine.
vi.mock("@tanstack/react-router", async (importOriginal) => {
  const original: object = await importOriginal();
  return { ...original, useParams: () => ({}) };
});

const page = requireElementId("3F2A9C14E07B5D21");
const other = requireElementId("5B19C0E2A4D7F311");

function user(id: string, name: string, over: Partial<PresenceUser> = {}): PresenceUser {
  return { userId: id, name, elementId: page, mode: "read", section: null, ...over };
}

beforeEach(() => {
  server.use(
    // A server: no connected servers to ask about.
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
      response(200).json({
        id: "malo",
        name: "Malo L.",
        defaultProjectsFolder: "",
        isServerAdmin: false,
      }),
    ),
  );
});

afterEach(clearPresence);

describe("presence (M4-04, F14.R3)", () => {
  it("tells who edits the page and which section", async () => {
    setPresenceUsers([
      user("camille", "Camille R.", { mode: "edit", section: "Histoire" }),
      user("malo", "Malo L."),
    ]);

    renderWithProviders(<EditingBanner elementId={page} />);

    expect(await screen.findByRole("status")).toHaveTextContent(
      "Camille R. modifie aussi cette page — section « Histoire ». Vos changements seront fusionnés à l’enregistrement.",
    );
  });

  it("names several editors without a section", async () => {
    setPresenceUsers([
      user("camille", "Camille R.", { mode: "edit", section: "Histoire" }),
      user("lea", "Léa M.", { mode: "edit" }),
      user("malo", "Malo L.", { mode: "edit" }),
    ]);

    renderWithProviders(<EditingBanner elementId={page} />);

    // Malo is the user: the banner names the two others once /me has answered.
    await waitFor(() => {
      expect(screen.getByRole("status")).toHaveTextContent(
        "Camille R., Léa M. modifient aussi cette page.",
      );
    });
  });

  it("says nothing when only the user edits, or when the others edit another page", async () => {
    setPresenceUsers([
      user("malo", "Malo L.", { mode: "edit" }),
      user("camille", "Camille R.", { mode: "edit", elementId: other }),
    ]);

    renderWithProviders(<EditingBanner elementId={page} />);

    // Until the user is known their own edit counts as another person's: the banner goes once /me answered.
    await waitFor(() => {
      expect(screen.queryByRole("status")).not.toBeInTheDocument();
    });
  });

  it("shows the others on the page, never the user", async () => {
    setPresenceUsers([user("camille", "Camille R."), user("malo", "Malo L.")]);

    renderWithProviders(<ElementPresence elementId={page} />);

    expect(await screen.findByRole("img", { name: "Camille R. consulte" })).toHaveTextContent("CR");
    await waitFor(() => {
      expect(screen.queryByRole("img", { name: /Malo/ })).not.toBeInTheDocument();
    });
  });

  it("shows three avatars in the top bar and counts the rest", async () => {
    setPresenceUsers([
      user("malo", "Malo L."),
      ...["Anne B.", "Bruno C.", "Chloé D.", "Denis E.", "Éva F."].map((name, i) =>
        user("u" + String(i), name),
      ),
    ]);

    renderWithProviders(<PresenceAvatars />);

    // « +2 » only once the user is known: until then everybody counts as another person.
    expect(await screen.findByText("+2")).toBeInTheDocument();
    expect(screen.getAllByRole("img")).toHaveLength(3);
  });
});
