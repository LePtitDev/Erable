import { setupServer } from "msw/node";
import { createOpenApiHttp } from "openapi-msw";
import type { paths } from "../api/schema";

/** MSW handlers typed by the generated client: a mock cannot drift from the real contract (docs/testing.md §8.1). */
export const http = createOpenApiHttp<paths>({ baseUrl: globalThis.location.origin });

export const server = setupServer();
