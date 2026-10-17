import { useQuery } from "@tanstack/react-query";
import { useEffect, useLayoutEffect, useMemo, useRef } from "react";
import { preferencesQuery } from "../api/preferences";
import { isRecordingShortcut } from "./capture";
import {
  matchesEvent,
  resolveShortcuts,
  type ResolvedShortcuts,
  type ShortcutAction,
} from "./shortcuts";

/** The combination of every action for the current user; the defaults until the preferences are loaded (or if they fail). */
export function useShortcuts(): ResolvedShortcuts {
  const preferences = useQuery(preferencesQuery);
  const overrides = preferences.data?.shortcuts;
  return useMemo(() => resolveShortcuts(overrides), [overrides]);
}

/**
 * Runs `handler` when the user's combination for `action` is pressed anywhere in the window. Listens during the capture
 * phase so that the editor (which has its own keymap) cannot swallow it.
 */
export function useShortcut(action: ShortcutAction, handler: () => void, enabled = true) {
  const combo = useShortcuts()[action];
  const latest = useRef(handler);
  // Written after each render, read only when a key is pressed.
  useLayoutEffect(() => {
    latest.current = handler;
  });
  useEffect(() => {
    if (!enabled || combo === "") return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (isRecordingShortcut() || !matchesEvent(event, combo)) return;
      event.preventDefault();
      event.stopPropagation();
      latest.current();
    };
    window.addEventListener("keydown", onKeyDown, true);
    return () => {
      window.removeEventListener("keydown", onKeyDown, true);
    };
  }, [combo, enabled]);
}
