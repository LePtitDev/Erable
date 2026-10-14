import { afterEach, describe, expect, it } from "vitest";
import { http, server } from "../../test/msw";
import { establishDesktopSession } from "./session";

const token = "0123456789ABCDEF0123456789ABCDEF";

describe("establishDesktopSession", () => {
  afterEach(() => {
    history.replaceState(null, "", "/");
  });

  it("exchanges the token from the fragment and removes it from the address bar", async () => {
    const received: (string | null)[] = [];
    server.use(
      http.post("/api/session", ({ request, response }) => {
        received.push(request.headers.get("X-Erable-Session"));
        return response(204).empty();
      }),
    );
    history.replaceState(null, "", `/p/1?x=1#session=${token}`);

    const launch = await establishDesktopSession();

    expect(received).toEqual([token]);
    expect(launch.selfTest).toBe(false);
    expect(window.location.hash).toBe("");
    expect(window.location.pathname + window.location.search).toBe("/p/1?x=1");
  });

  it("detects the self-test flag", async () => {
    server.use(http.post("/api/session", ({ response }) => response(204).empty()));
    history.replaceState(null, "", `/#session=${token}&selftest=1`);

    expect(await establishDesktopSession()).toEqual({ selfTest: true });
  });

  it("does nothing without a session fragment", async () => {
    history.replaceState(null, "", "/#other");

    // MSW fails the test on any unhandled request, so no call may happen.
    await establishDesktopSession();

    expect(window.location.hash).toBe("#other");
  });
});
