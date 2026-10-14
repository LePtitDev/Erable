import { queryOptions } from "@tanstack/react-query";
import { api, unwrap } from "./client";

export const apiInfoQuery = queryOptions({
  queryKey: ["api", "info"],
  queryFn: ({ signal }) => unwrap(api.GET("/api/info", { signal })),
  staleTime: Infinity,
});
