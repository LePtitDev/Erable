import { QueryClient } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";
import { queryKeys } from "../api/keys";
import { requireElementId } from "../ids/elementId";
import { invalidateElementChanged, invalidateTreeChanged } from "./invalidation";
import { retryDelay } from "./projectConnection";

const project = requireElementId("CA0B0E5EC0A5D1E1");
const other = requireElementId("CA0B0E5EC0A5D1E2");
const page = requireElementId("3F2A9C14E07B5D21");
const orsen = requireElementId("7A1C4E90B3F2D615");

function seeded() {
  const client = new QueryClient();
  const keys = {
    tree: queryKeys.tree(project),
    page: queryKeys.page(project, page),
    content: queryKeys.pageContent(project, page),
    orsen: queryKeys.page(project, orsen),
    recent: queryKeys.recent(project),
    otherProject: queryKeys.page(other, page),
  };
  for (const key of Object.values(keys)) client.setQueryData(key, "x");
  const stale = (key: readonly unknown[]) => client.getQueryState(key)?.isInvalidated === true;
  return { client, keys, stale };
}

describe("invalidation by events", () => {
  it("refreshes what shows the element but neither the tree nor the other projects", async () => {
    const { client, keys, stale } = seeded();

    await invalidateElementChanged(client, project, page, null);

    expect(stale(keys.page)).toBe(true);
    expect(stale(keys.content)).toBe(true);
    expect(stale(keys.orsen)).toBe(true);
    expect(stale(keys.recent)).toBe(true);
    expect(stale(keys.tree)).toBe(false);
    expect(stale(keys.otherProject)).toBe(false);
  });

  it("leaves alone the page being edited", async () => {
    const { client, keys, stale } = seeded();

    await invalidateElementChanged(client, project, page, page);

    expect(stale(keys.page)).toBe(false);
    expect(stale(keys.content)).toBe(false);
    expect(stale(keys.orsen)).toBe(true);
  });

  it("refreshes the whole project when the tree changed", async () => {
    const { client, keys, stale } = seeded();

    await invalidateTreeChanged(client, project, null);

    expect(stale(keys.tree)).toBe(true);
    expect(stale(keys.page)).toBe(true);
    expect(stale(keys.otherProject)).toBe(false);
  });

  it("still leaves the edited page alone when the tree changed", async () => {
    const { client, keys, stale } = seeded();

    await invalidateTreeChanged(client, project, page);

    expect(stale(keys.tree)).toBe(true);
    expect(stale(keys.page)).toBe(false);
  });
});

describe("reconnection delay", () => {
  it("retries at once, then backs off up to ten seconds", () => {
    expect([0, 1, 2, 3, 4, 5, 6, 20].map(retryDelay)).toEqual([
      0, 1000, 2000, 4000, 8000, 10000, 10000, 10000,
    ]);
  });
});
