import { HttpResponse, http as rawHttp } from "msw";
import { describe, expect, it } from "vitest";
import { server } from "../../test/msw";
import { api, ApiError, unwrap } from "./client";

describe("unwrap", () => {
  it("throws an ApiError carrying the ProblemDetails of a failed call", async () => {
    server.use(
      rawHttp.get("*/api/info", () =>
        HttpResponse.json(
          { title: "Service Unavailable", status: 503 },
          { status: 503, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );

    const error = await unwrap(api.GET("/api/info")).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 503, problem: { title: "Service Unavailable" } });
  });
});
