import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Backend URL used by the dev server proxy (`dotnet run --project src/Erable.Server`).
const backend = process.env["ERABLE_BACKEND_URL"] ?? "http://localhost:5080";

export default defineConfig({
  plugins: [react()],
  // The SPA is served from the backend root in every mode; override for a sub-path deployment.
  base: process.env["ERABLE_BASE"] ?? "/",
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      "/api": backend,
      "/_test": backend,
      "/hubs": { target: backend, ws: true },
    },
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    sourcemap: true,
  },
});
