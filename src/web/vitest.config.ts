import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: ["./src/test/setup.ts"],
    css: { include: [/tokens\.css/], modules: { classNameStrategy: "non-scoped" } },
    restoreMocks: true,
    // Whole-application renders are heavy for jsdom: under the load of a full parallel run the default 5 s was not enough.
    testTimeout: 20_000,
  },
});
