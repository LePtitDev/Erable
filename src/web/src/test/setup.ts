import "@testing-library/jest-dom/vitest";
import { cleanup, configure } from "@testing-library/react";
import { afterAll, afterEach, beforeAll } from "vitest";
import { server } from "./msw";

// Same reason as `testTimeout` (vitest.config.ts): `findBy*` and `waitFor` wait up to 1 s by default.
configure({ asyncUtilTimeout: 5000 });

// jsdom has no layout: the router's scroll restoration calls window.scrollTo.
window.scrollTo = () => undefined;
Element.prototype.scrollIntoView = () => undefined;

beforeAll(() => {
  server.listen({ onUnhandledFrame: "error" });
});

afterEach(() => {
  cleanup();
  server.resetHandlers();
});

afterAll(() => {
  server.close();
});
