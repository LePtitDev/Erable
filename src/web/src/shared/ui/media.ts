import { useSyncExternalStore } from "react";

/**
 * Width at and under which the narrow layout applies (F18): what the desktop shows side by side is stacked. The same
 * value is written in the `@media (width <= 720px)` of the CSS Modules, a custom property cannot be used there.
 */
export const narrowMaxWidth = 720;
export const narrowQuery = `(width <= ${String(narrowMaxWidth)}px)`;
/** Touch screens: 44 px targets and no action reachable only by hovering or right-clicking (F18.R4). */
export const coarseQuery = "(pointer: coarse)";

function media(query: string): MediaQueryList | null {
  return typeof window.matchMedia === "function" ? window.matchMedia(query) : null;
}

/** Whether `query` matches, updated live. False where `matchMedia` does not exist (jsdom): the wide layout. */
export function useMediaQuery(query: string): boolean {
  return useSyncExternalStore(
    (onChange) => {
      const list = media(query);
      list?.addEventListener("change", onChange);
      return () => list?.removeEventListener("change", onChange);
    },
    () => media(query)?.matches ?? false,
  );
}

export function useIsNarrowScreen(): boolean {
  return useMediaQuery(narrowQuery);
}

export function useIsCoarsePointer(): boolean {
  return useMediaQuery(coarseQuery);
}
