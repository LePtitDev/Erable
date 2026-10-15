import type { APIRequestContext } from "@playwright/test";
import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { AttachmentsPanel } from "../pages/AttachmentsPanel";
import { EditorPage } from "../pages/EditorPage";
import { PageViewPage } from "../pages/PageViewPage";
import { t } from "../support/i18n";
import { blob, pdf, png, zip } from "../support/samples";

interface PageData {
  markdown: string;
  revision: number;
  attachments: { id: string; fileName: string }[];
}

async function pageData(request: APIRequestContext, projectId: string, pageId: string) {
  const response = await request.get(`/api/v1/projects/${projectId}/pages/${pageId}`);
  const page = (await response.json()) as PageData & { element: { revision: number } };
  const content = await request.get(`/api/v1/projects/${projectId}/pages/${pageId}/content`);
  const { markdown, revision } = (await content.json()) as PageData;
  return { ...page, markdown, revision };
}

/** Adds `![[ID]]` of an attachment of the page at the end of its text, through the API. */
async function embed(request: APIRequestContext, projectId: string, pageId: string, file: string) {
  const data = await pageData(request, projectId, pageId);
  const attachment = data.attachments.find((a) => a.fileName === file);
  if (attachment === undefined) throw new Error(`No attachment ${file}.`);
  const response = await request.put(`/api/v1/projects/${projectId}/pages/${pageId}/content`, {
    headers: { "If-Match": `"${String(data.revision)}"` },
    data: { markdown: `${data.markdown}\n\n![[${attachment.id}]]\n` },
  });
  expect(response.ok()).toBe(true);
  return attachment.id;
}

test.describe("F06 · Attachments", () => {
  test("E2E-06-01 · Paste an image @pr @desktop", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    const panel = new AttachmentsPanel(page);
    await page.goto(reference.url(chroniques.hautbois));
    await editor.open();
    await editor.toEnd();

    await editor.pasteFiles(png("croquis.png"));

    await expect(editor.pill("croquis.png")).toBeVisible();
    await expect(editor.uploads).toHaveCount(0);
    await editor.save();
    await expect(panel.item("croquis.png")).toBeVisible();
    await expect(
      new PageViewPage(page).article.getByRole("img", { name: "croquis.png" }),
    ).toBeVisible();
  });

  test("E2E-06-02 · Drop several files @pr", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    const view = new PageViewPage(page);
    // Held back until the progress is checked: on the loopback, an upload is otherwise over before it is seen.
    let release!: () => void;
    const held = new Promise<void>((resolve) => {
      release = resolve;
    });
    await page.route("**/attachments", async (route) => {
      await held;
      await route.continue();
    });
    await page.goto(reference.url(chroniques.hautbois));
    await editor.open();

    await editor.dropFiles(png("vue.png"), pdf("registre.pdf"), zip("archives.zip"));

    await expect(editor.uploads).toHaveCount(3);
    release();
    await expect(editor.uploads).toHaveCount(0);
    for (const name of ["vue.png", "registre.pdf", "archives.zip"]) {
      await expect(editor.pill(name)).toBeVisible();
    }
    await editor.content.focus();
    await editor.save();

    await expect(view.article.getByRole("img", { name: "vue.png" })).toBeVisible();
    await expect(view.article.locator("iframe[title='registre.pdf']")).toBeVisible();
    await expect(
      view.article.getByRole("link", {
        name: t("attachments.downloadNamed", { name: "archives.zip" }),
      }),
    ).toBeVisible();
  });

  test("E2E-06-03 · Add from the panel @pr", async ({ page, reference }) => {
    const panel = new AttachmentsPanel(page);
    const before = await pageData(page.request, reference.id, chroniques.hautbois);
    await page.goto(reference.url(chroniques.hautbois));

    await panel.add(pdf("registre.pdf"));

    const item = panel.item("registre.pdf");
    await expect(item).toBeVisible();
    await expect(item).toContainText("PDF");
    await expect(item).toContainText(/\d+\s+o$/);
    const after = await pageData(page.request, reference.id, chroniques.hautbois);
    expect(after.markdown).toBe(before.markdown);
    expect(after.attachments).toHaveLength(before.attachments.length + 1);
  });

  test("E2E-06-04 · Streamed video playback @nightly", async ({ page, reference, browserName }) => {
    await embed(page.request, reference.id, chroniques.hautbois, "bande_annonce.mp4");
    const responses: { status: number; ranges: string | null }[] = [];
    page.on("response", (response) => {
      if (response.url().includes("/attachments/"))
        responses.push({
          status: response.status(),
          ranges: response.headers()["accept-ranges"] ?? null,
        });
    });
    await page.goto(reference.url(chroniques.hautbois));

    const video = new PageViewPage(page).article.getByRole("figure").locator("video");
    await expect(video).toBeVisible();
    await expect.poll(() => video.evaluate((v: HTMLVideoElement) => v.duration)).toBeGreaterThan(0);
    const target = await video.evaluate(
      (v: HTMLVideoElement) =>
        new Promise<number>((resolve) => {
          v.addEventListener("seeked", () => {
            resolve(v.currentTime / v.duration);
          });
          v.currentTime = v.duration * 0.8;
        }),
    );

    expect(target).toBeCloseTo(0.8, 1);
    if (browserName === "webkit") {
      // WebKit's Windows media stack reads a file this small whole, without Range: the backend still offers it.
      expect(responses.map((r) => r.ranges)).toContain("bytes");
    } else {
      expect(responses.map((r) => r.status)).toContain(206);
    }
  });

  test("E2E-06-05 · SVG never rendered inline @pr", async ({ page, reference }) => {
    const id = await embed(page.request, reference.id, chroniques.hautbois, "blason.svg");
    await page.goto(reference.url(chroniques.hautbois));

    const view = new PageViewPage(page);
    const figure = view.content.getByRole("figure").filter({ hasText: "blason.svg" });
    await expect(
      figure.getByRole("link", { name: t("attachments.downloadNamed", { name: "blason.svg" }) }),
    ).toBeVisible();
    await expect(figure.locator("img, object, embed, iframe")).toHaveCount(0);
    // Only the decorative icons of the card are SVG, never the file itself.
    await expect(figure.locator("svg:not([aria-hidden='true'])")).toHaveCount(0);
    await expect(view.article.locator(`[src*='${id}'], [data*='${id}']`)).toHaveCount(0);
  });

  test("E2E-06-07 · File too large @nightly", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    const before = await pageData(page.request, reference.id, chroniques.hautbois);
    await page.request.post("/_test/limits", { data: { maxAttachmentSize: 10 * 1024 * 1024 } });
    try {
      await page.goto(reference.url(chroniques.hautbois));
      await editor.open();

      await editor.dropFiles(blob("enorme.bin", 11 * 1024 * 1024));

      const message = t("attachments.tooLarge", { name: "enorme.bin", max: "10 Mo" });
      await expect(page.getByRole("alert").filter({ hasText: message })).toBeVisible();
      await expect(editor.uploads).toHaveCount(0);
      const after = await pageData(page.request, reference.id, chroniques.hautbois);
      expect(after.attachments).toHaveLength(before.attachments.length);
    } finally {
      await page.request.post("/_test/limits", { data: { maxAttachmentSize: null } });
    }
  });

  test("E2E-06-08 · Identical file deduplicated @nightly", async ({ page, reference }) => {
    const blobs = async () => {
      const response = await page.request.get(`/_test/blobs?projectId=${reference.id}`);
      return ((await response.json()) as { hashes: string[] }).hashes;
    };
    const before = await blobs();
    const panel = new AttachmentsPanel(page);

    for (const pageId of [chroniques.hautbois, chroniques.colGeleLieu]) {
      await page.goto(reference.url(pageId));
      await panel.add(png("emblème.png", "unique"));
      await expect(panel.item("emblème.png")).toBeVisible();
    }

    expect(await blobs()).toHaveLength(before.length + 1);
  });

  test("M1-14 · A failed upload leaves no pill in the text @pr", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    await page.route("**/attachments", (route) => route.abort("connectionreset"));
    await page.goto(reference.url(chroniques.hautbois));
    await editor.open();
    await expect(editor.pill("Ilsa Varenne")).toBeVisible();
    const text = await editor.shownText();

    await editor.pasteFiles(png("perdu.png"));

    await expect(page.getByRole("alert").filter({ hasText: "perdu.png" })).toBeVisible();
    await expect(editor.uploads).toHaveCount(0);
    expect(await editor.shownText()).toBe(text);
  });
});
