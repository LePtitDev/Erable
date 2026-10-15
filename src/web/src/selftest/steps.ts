import type { i18n as I18n } from "i18next";
import { uploadAttachment } from "../shared/api/attachments";
import { api, unwrap } from "../shared/api/client";
import type { LaunchOptions } from "../shared/api/session";
import type { ElementId } from "../shared/ids/elementId";
import { roots } from "../shared/ids/roots";

/**
 * Scripted desktop smoke journey (docs/testing.md §10, E2E-16-01). Each milestone appends steps here: M1 creates a
 * project, two pages, a reference and an image, then checks the page renders them; M3 adds the search.
 */
export interface SelfTestContext {
  readonly i18n: I18n;
  readonly document: Document;
  readonly launch: LaunchOptions;
  /** SPA clock when the home screen became visible (E2E-16-01: < 3 s). */
  readyAtMs?: number;
  projectId?: ElementId;
  pageId?: ElementId;
}

export interface SelfTestStepDefinition {
  readonly name: string;
  readonly run: (context: SelfTestContext) => Promise<string | undefined>;
}

const timeoutMs = 15_000;

/**
 * Resolves once `predicate` holds, re-checked on every DOM change and every 100 ms (an image that finishes loading
 * changes no DOM node).
 */
export function waitFor(document: Document, predicate: () => boolean, what: string): Promise<void> {
  return new Promise((resolve, reject) => {
    if (predicate()) {
      resolve();
      return;
    }
    const check = () => {
      if (predicate()) {
        observer.disconnect();
        clearInterval(poll);
        clearTimeout(timer);
        resolve();
      }
    };
    const observer = new MutationObserver(check);
    const poll = setInterval(check, 100);
    const timer = setTimeout(() => {
      observer.disconnect();
      clearInterval(poll);
      reject(new Error(`Timed out waiting for ${what}`));
    }, timeoutMs);
    observer.observe(document.body, { childList: true, subtree: true, characterData: true });
  });
}

export const selfTestSteps: readonly SelfTestStepDefinition[] = [
  {
    name: "api-authenticated",
    run: async () => {
      const info = await unwrap(api.GET("/api/info"));
      return `${info.appVersion} · ${info.mode}`;
    },
  },
  {
    name: "home-visible",
    run: async (context) => {
      const { document, i18n } = context;
      await waitFor(
        document,
        () =>
          document.body.textContent.includes(i18n.t("home.subtitle")) &&
          document.body.textContent.includes(i18n.t("app.mode.desktop")),
        "the home screen",
      );
      context.readyAtMs = performance.now();
      return `ready at ${Math.round(context.readyAtMs)} ms`;
    },
  },
  {
    name: "project-created",
    run: async (context) => {
      const folder = context.launch.selfTestFolder;
      if (folder === null) throw new Error("The shell gave no folder for the smoke project");
      const project = await unwrap(
        api.POST("/api/v1/projects", {
          body: { folder, name: "Fumée", contentLanguage: "fr" },
        }),
      );
      context.projectId = project.id;
      return project.id;
    },
  },
  {
    name: "pages-and-reference",
    run: async (context) => {
      const projectId = required(context.projectId);
      const target = await unwrap(
        api.POST("/api/v1/projects/{projectId}/pages", {
          params: { path: { projectId } },
          body: { parentId: roots.pages, name: "Destination" },
        }),
      );
      const page = await unwrap(
        api.POST("/api/v1/projects/{projectId}/pages", {
          params: { path: { projectId } },
          body: { parentId: roots.pages, name: "Départ", markdown: `Vers [[${target.id}]].` },
        }),
      );
      context.pageId = page.id;
      return page.id;
    },
  },
  {
    name: "image-attached",
    run: async (context) => {
      const projectId = required(context.projectId);
      const pageId = required(context.pageId);
      const image = await uploadAttachment(projectId, pageId, await smallPng(), () => undefined);
      const content = await unwrap(
        api.GET("/api/v1/projects/{projectId}/pages/{id}/content", {
          params: { path: { projectId, id: pageId } },
        }),
      );
      await unwrap(
        api.PUT("/api/v1/projects/{projectId}/pages/{id}/content", {
          params: {
            path: { projectId, id: pageId },
            header: { "If-Match": `"${String(content.revision)}"` },
          },
          body: {
            markdown: `${content.markdown}

![[${image.id}]]
`,
          },
        }),
      );
      return image.id;
    },
  },
  {
    name: "page-rendered",
    run: async (context) => {
      const { document } = context;
      // In-app navigation, as a click on a link does: the router follows the history.
      history.pushState(
        null,
        "",
        `/p/${required(context.projectId)}/e/${required(context.pageId)}`,
      );
      window.dispatchEvent(new PopStateEvent("popstate"));
      await waitFor(
        document,
        () =>
          [...document.querySelectorAll("article a")].some(
            (a) => a.textContent === "Destination",
          ) &&
          [...document.querySelectorAll<HTMLImageElement>("article img")].some(
            (img) => img.complete && img.naturalWidth > 0,
          ),
        "the page with its reference and its image",
      );
      return "reference and image shown";
    },
  },
];

function required(id: ElementId | undefined): ElementId {
  if (id === undefined) throw new Error("A previous step did not run");
  return id;
}

/** A 16 × 16 PNG drawn on a canvas: no binary fixture in the bundle. */
async function smallPng(): Promise<File> {
  const canvas = document.createElement("canvas");
  canvas.width = 16;
  canvas.height = 16;
  const context = canvas.getContext("2d");
  if (context === null) throw new Error("No 2D canvas");
  context.fillStyle = "#b5442e";
  context.fillRect(0, 0, 16, 16);
  const blob = await new Promise<Blob | null>((resolve) => {
    canvas.toBlob(resolve, "image/png");
  });
  if (blob === null) throw new Error("The canvas gave no PNG");
  return new File([blob], "fumee.png", { type: "image/png" });
}
