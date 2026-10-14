import { QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { i18n as I18n } from "i18next";
import { useState } from "react";
import { I18nextProvider } from "react-i18next";
import { createQueryClient } from "../shared/api/queryClient";
import { router } from "./router";

export function App({ i18n }: { i18n: I18n }) {
  const [queryClient] = useState(() => createQueryClient());
  return (
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={queryClient}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>
  );
}
