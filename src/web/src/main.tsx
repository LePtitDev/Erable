import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./shared/theme/fonts";
import "./shared/theme/tokens.css";
import "./shared/theme/global.css";
import { App } from "./app/App";
import { applyTheme, readInitialTheme } from "./shared/theme/theme";

// Normally already set by the backend in index.html; also covers the Vite dev server.
applyTheme(readInitialTheme());

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root element #root not found");
}

createRoot(container).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
