import createClient from "openapi-fetch";
import type { components, paths } from "../../api/schema";
import type { ElementId } from "../ids/elementId";

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

/**
 * Called when a signed-in session turns out to have expired (a 401 on an ordinary call). Resolves to true once the
 * user signed in again: the call is then replayed, so that a save in progress is not lost (E2E-14-09).
 */
export type UnauthorizedHandler = (request: Request) => Promise<boolean>;

let onUnauthorized: UnauthorizedHandler | null = null;

export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  onUnauthorized = handler;
}

/** A request whose body can be sent twice: no body, or a small one. Uploads (multipart) are not replayed. */
function canReplay(request: Request): boolean {
  return (
    request.body === null || !(request.headers.get("content-type") ?? "").startsWith("multipart/")
  );
}

async function apiFetch(request: Request): Promise<Response> {
  const handler = onUnauthorized;
  const replay = handler !== null && canReplay(request) ? request.clone() : null;
  const response = await globalThis.fetch(request);
  if (response.status !== 401 || handler === null || replay === null) return response;
  return (await handler(request)) ? globalThis.fetch(replay) : response;
}

/** The single API client of the SPA. Same origin in every mode: the backend serves the SPA. */
export const api = createClient<paths>({
  baseUrl: globalThis.location.origin,
  // Resolved at call time so that interceptors installed later (MSW in tests) are honoured.
  fetch: apiFetch,
});

/**
 * openapi-fetch passes response types through `Readable<T>`, a deep mapped type that also maps the branded
 * `ElementId` (a string intersected with an object) into a plain object type. This undoes it.
 */
export type Rebrand<T> = T extends { readonly __brand: "ElementId" }
  ? ElementId
  : T extends (infer E)[]
    ? Rebrand<E>[]
    : T extends object
      ? { [K in keyof T]: Rebrand<T[K]> }
      : T;

/**
 * Awaits an `api.GET/POST/…` call and returns its data, or throws an {@link ApiError}
 * carrying the backend's ProblemDetails.
 */
export async function unwrap<T>(
  call: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<Rebrand<T>> {
  const { data, error, response } = await call;
  if (!response.ok) {
    throw new ApiError(response.status, isProblem(error) ? error : undefined);
  }
  // Undefined only for bodiless responses (204), whose schema type is `never` or `undefined`.
  return data as Rebrand<T>;
}
