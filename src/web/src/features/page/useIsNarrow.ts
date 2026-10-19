import { useEffect, useState, type RefObject } from "react";

/**
 * Whether the content box of `ref` is narrower than `width`, updated as it resizes (the explorer, the window). False
 * where `ResizeObserver` does not exist (jsdom): the wide layout is the default.
 */
export function useIsNarrow(ref: RefObject<HTMLElement | null>, width: number): boolean {
  const [narrow, setNarrow] = useState(false);
  useEffect(() => {
    const element = ref.current;
    if (element === null || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver((entries) => {
      const entry = entries[entries.length - 1];
      if (entry !== undefined) setNarrow(entry.contentRect.width < width);
    });
    observer.observe(element);
    return () => {
      observer.disconnect();
    };
  }, [ref, width]);
  return narrow;
}
