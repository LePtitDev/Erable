import "./shared/theme/fonts";
import "./shared/theme/tokens.css";
import "./shared/theme/global.css";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./app/App";
import { establishDesktopSession } from "./shared/api/session";
import { createI18n, initialLanguage } from "./shared/i18n/i18n";
import { applyTheme, readInitialTheme } from "./shared/theme/theme";

// Normally already set by the backend in index.html; also covers the Vite dev server.
applyTheme(readInitialTheme());

const container = document.getElementById("root");
if (!container) {
  throw new Error("Root element #root not found");
}

const [i18n, launch] = await Promise.all([
  createI18n(initialLanguage()),
  // Never block the first render: if this fails, API calls answer 401 and the UI shows the error.
  establishDesktopSession().catch((error: unknown) => {
    console.error("Desktop session could not be established", error);
    return { selfTest: false };
  }),
]);

createRoot(container).render(
  <StrictMode>
    <App i18n={i18n} />
  </StrictMode>,
);

if (launch.selfTest) {
  // Loaded only on demand: the smoke journey is not part of the main bundle.
  void import("./selftest/run").then(({ runSelfTest }) => runSelfTest(i18n));
}
