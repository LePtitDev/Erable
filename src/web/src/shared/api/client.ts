import createClient from "openapi-fetch";
import type { components, paths } from "../../api/schema";

export type Schemas = components["schemas"];
export type ApiInfo = Schemas["ApiInfo"];

/** RFC 9457 problem details, as returned by every backend error. */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  readonly [extension: string]: unknown;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem: ProblemDetails | undefined) {
    super(problem?.title ?? `HTTP ${status}`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}

function isProblem(value: unknown): value is ProblemDetails {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/** The single API client of the SPA. Same origin in every mode: the backend serves the SPA. */
export const api = createClient<paths>({
  baseUrl: globalThis.location.origin,
  // Resolved at call time so that interceptors installed later (MSW in tests) are honoured.
  fetch: (request) => globalThis.fetch(request),
});

/**
 * Awaits an `api.GET/POST/…` call and returns its data, or throws an {@link ApiError}
 * carrying the backend's ProblemDetails.
 */
export async function unwrap<T>(
  call: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await call;
  if (!response.ok) {
    throw new ApiError(response.status, isProblem(error) ? error : undefined);
  }
  // Undefined only for bodiless responses (204), whose schema type is `never` or `undefined`.
  return data as T;
}
