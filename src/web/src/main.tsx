import "./shared/theme/fonts";
import "./shared/theme/tokens.css";
import "./shared/theme/global.css";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./app/App";
import { createI18n, initialLanguage } from "./shared/i18n/i18n";
import { applyTheme, readInitialTheme } from "./shared/theme/theme";

// Normally already set by the backend in index.html; also covers the Vite dev server.
applyTheme(readInitialTheme());

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root element #root not found");
}

const i18n = await createI18n(initialLanguage());

createRoot(container).render(
  <StrictMode>
    <App i18n={i18n} />
  </StrictMode>,
);
