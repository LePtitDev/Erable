import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "./client";

/**
 * The query cache of the application. A 4xx answer is final (an unknown element stays unknown): only network
 * failures and 5xx are retried, twice.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: (failures, error) =>
          !(error instanceof ApiError && error.status < 500) && failures < 2,
        refetchOnWindowFocus: false,
      },
    },
  });
}
