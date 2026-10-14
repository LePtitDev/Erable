import { useSyncExternalStore } from "react";

const query = "(prefers-color-scheme: dark)";

function media(): MediaQueryList | null {
  return typeof window.matchMedia === "function" ? window.matchMedia(query) : null;
}

/** Whether the OS is in dark mode, updated live (F15.R2). */
export function useSystemDark(): boolean {
  return useSyncExternalStore(
    (onChange) => {
      const list = media();
      list?.addEventListener("change", onChange);
      return () => list?.removeEventListener("change", onChange);
    },
    () => media()?.matches ?? false,
  );
}
