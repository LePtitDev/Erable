import { act, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { narrowQuery, useIsNarrowScreen, useMediaQuery } from "./media";

/** A controllable `matchMedia`: one list per query, `set` changes it and notifies. */
function fakeMatchMedia(initial: Record<string, boolean>) {
  const lists = new Map<string, { matches: boolean; listeners: Set<() => void> }>();
  const list = (query: string) => {
    let entry = lists.get(query);
    if (entry === undefined) {
      entry = { matches: initial[query] ?? false, listeners: new Set() };
      lists.set(query, entry);
    }
    return entry;
  };
  vi.stubGlobal("matchMedia", (query: string) => {
    const entry = list(query);
    return {
      get matches() {
        return entry.matches;
      },
      media: query,
      addEventListener: (_: string, listener: () => void) => entry.listeners.add(listener),
      removeEventListener: (_: string, listener: () => void) => entry.listeners.delete(listener),
    };
  });
  return {
    set(query: string, matches: boolean) {
      const entry = list(query);
      entry.matches = matches;
      for (const listener of entry.listeners) listener();
    },
  };
}

describe("useMediaQuery", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("is false without matchMedia: the wide layout is the default", () => {
    vi.stubGlobal("matchMedia", undefined);
    const { result } = renderHook(() => useIsNarrowScreen());
    expect(result.current).toBe(false);
  });

  it("follows the changes of the query", () => {
    const media = fakeMatchMedia({ [narrowQuery]: true });
    const { result } = renderHook(() => useMediaQuery(narrowQuery));
    expect(result.current).toBe(true);
    act(() => {
      media.set(narrowQuery, false);
    });
    expect(result.current).toBe(false);
  });
});
