import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { requireElementId } from "../../shared/ids/elementId";
import { ids, mockProject } from "../../test/projectMocks";
import { renderApp } from "../../test/render";

const portrait = requireElementId("A11A000000000001");
const carte = requireElementId("A11A000000000002");
const blason = requireElementId("A11A000000000003");

const ilsaMarkdown = `Ilsa tient le comptoir de [[${ids.hautbois}]], au pied du [[${ids.colGele}|col]].

## Présentation

Elle connaît le [[${ids.orsen}]].

![[${portrait}|Portrait par Malo L.]]

![[${carte}]]

## Histoire

### Les années de cartographe

Ses relevés restent fiables. Voir [[0123456789ABCDEF]] et [[${ids.dialogues}]].
`;

const attachments = [
  {
    id: portrait,
    pageId: ids.ilsa,
    fileName: "portrait_ilsa.png",
    mime: "image/png",
    display: "image" as const,
  },
  {
    id: carte,
    pageId: ids.hautbois,
    fileName: "carte_hautbois.webp",
    mime: "image/webp",
    display: "image" as const,
  },
  {
    id: blason,
    pageId: ids.hautbois,
    fileName: "blason.svg",
    mime: "image/svg+xml",
    display: "file" as const,
  },
];

async function openIlsa(markdown = ilsaMarkdown, deleted: (typeof ids.ilsa)[] = []) {
  mockProject({ pages: { [ids.ilsa]: markdown }, attachments, deleted });
  const result = await renderApp(`/p/${ids.project}/e/${ids.ilsa}`);
  const article = await screen.findByRole("article");
  return { ...result, article };
}

describe("PageView", () => {
  it("shows the breadcrumb, the title, the ID and the modification line (E2E-04-01)", async () => {
    const { article } = await openIlsa();

    expect(within(article).getByRole("navigation", { name: "Fil d’Ariane" })).toHaveTextContent(
      "Pages/Personnages/PNJ/Ilsa Varenne",
    );
    expect(
      within(article).getByRole("heading", { level: 1, name: "Ilsa Varenne" }),
    ).toBeInTheDocument();
    expect(
      within(article).getByRole("button", { name: `Copier l’identifiant ${ids.ilsa}` }),
    ).toHaveTextContent("3F2A 9C14 E07B 5D21");
    expect(
      within(article).getByText(/Modifiée il y a 2 h par Camille R\. · créée le 12 mars 2026/),
    ).toBeInTheDocument();
  });

  it("shows references with the current name of their target, or their own label (F04.R1)", async () => {
    const { article } = await openIlsa();

    expect((await within(article).findAllByRole("link", { name: "Hautbois" }))[0]).toHaveAttribute(
      "href",
      `/p/${ids.project}/e/${ids.hautbois}`,
    );
    expect(within(article).getByRole("link", { name: "Capitaine Orsen" })).toBeInTheDocument();
    expect(within(article).getByRole("link", { name: "col" })).toHaveAttribute(
      "href",
      `/p/${ids.project}/e/${ids.colGele}`,
    );
  });

  it("shows a reference to an unknown or deleted element as a broken link, never an error (F04.R2)", async () => {
    const { article } = await openIlsa(ilsaMarkdown, [ids.dialogues]);

    expect(
      await within(article).findByLabelText(/lien cassé : Élément introuvable/),
    ).toBeInTheDocument();
    expect(
      within(article).getByLabelText("Dialogues (lien cassé : Élément supprimé)"),
    ).toBeInTheDocument();
    const paragraph = within(article).getByText(/Ses relevés restent fiables/);
    expect(within(paragraph).queryByRole("link", { name: "Dialogues" })).not.toBeInTheDocument();
  });

  it("embeds images, with « Pièce jointe de » for the attachment of another page", async () => {
    const { article } = await openIlsa();

    const ownImage = await within(article).findByRole("img", { name: "Portrait par Malo L." });
    expect(ownImage).toHaveAttribute(
      "src",
      `/api/v1/projects/${ids.project}/attachments/${portrait}`,
    );
    const foreign = within(article)
      .getByRole("img", { name: "carte_hautbois.webp" })
      .closest("figure");
    expect(foreign).toHaveTextContent("Pièce jointe de Hautbois · carte_hautbois.webp");
    expect(
      within(ownImage.closest("figure") as HTMLElement).queryByText(/Pièce jointe de/),
    ).not.toBeInTheDocument();
  });

  it("never renders an SVG attachment inline: a download card instead (F06.R2)", async () => {
    const { article } = await openIlsa(`![[${blason}]]`);

    expect(
      await within(article).findByRole("link", { name: "Télécharger blason.svg" }),
    ).toHaveAttribute("download", "blason.svg");
    expect(article.querySelector("svg image, object, embed, iframe")).toBeNull();
    expect(within(article).queryByRole("img", { name: "blason.svg" })).not.toBeInTheDocument();
  });

  it("lists headings 2 and 3 in the table of contents and marks the clicked entry (F04.R3)", async () => {
    const user = userEvent.setup();
    await openIlsa();
    const toc = await screen.findByRole("navigation", { name: "Sommaire" });

    await user.click(within(toc).getByRole("link", { name: "Les années de cartographe" }));

    expect(
      within(toc)
        .getAllByRole("link")
        .map((l) => l.textContent),
    ).toEqual(["Présentation", "Histoire", "Les années de cartographe", "Sous-pages"]);
    expect(within(toc).getByRole("link", { name: "Les années de cartographe" })).toHaveAttribute(
      "aria-current",
      "location",
    );
    expect(window.location.hash).toBe("#les-annees-de-cartographe");
    expect(document.getElementById("les-annees-de-cartographe")).toHaveTextContent(
      "Les années de cartographe",
    );
  });

  it("lists the sub-pages and creates one in the editor (E2E-04-06)", async () => {
    const user = userEvent.setup();
    const { article, router } = await openIlsa();
    const section = await within(article).findByRole("region", { name: "Sous-pages" });

    expect(within(section).getByRole("link", { name: /Dialogues/ })).toBeInTheDocument();
    await user.click(within(section).getByRole("button", { name: "Nouvelle sous-page" }));

    await waitFor(() => {
      expect(router.state.location.search).toEqual({ edit: true });
    });
  });

  it("resolves all the references of a page in one call", async () => {
    const calls: string[] = [];
    const listener = (event: { request: Request }) => {
      if (event.request.url.includes("/resolve")) calls.push(event.request.url);
    };
    const { server } = await import("../../test/msw");
    server.events.on("request:start", listener);
    await openIlsa();
    await screen.findAllByRole("link", { name: "Hautbois" });
    server.events.removeListener("request:start", listener);

    expect(calls).toHaveLength(1);
  });
});

describe("PageView · XSS corpus (docs/testing.md §8.1, F04.R5)", () => {
  const corpus = [
    `<script>window.__xss = 1</script>`,
    `<img src=x onerror="window.__xss=1">`,
    `[clic](javascript:window.__xss=1)`,
    `[clic](  JaVaScRiPt:alert(1))`,
    `![x](javascript:alert(1))`,
    `[data](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)`,
    `<svg onload="window.__xss=1"><circle/></svg>`,
    `<iframe src="https://evil.example"></iframe>`,
    `<a href="javascript:alert(1)">a</a>`,
    `[ref]: javascript:alert(1)\n\n[voir][ref]`,
    "```html\n<script>alert(1)</script>\n```",
  ];

  it.each(corpus)("produces no executable element: %s", async (markdown) => {
    const { article } = await openIlsa(markdown);
    await waitFor(() => {
      expect(within(article).queryByText("Chargement…")).not.toBeInTheDocument();
    });

    expect(
      article.querySelector("script, iframe, object, embed, svg[onload], [onerror], [onload]"),
    ).toBeNull();
    for (const a of article.querySelectorAll("a[href]")) {
      expect(a.getAttribute("href")).not.toMatch(/^\s*(javascript|data|vbscript):/i);
    }
    for (const img of article.querySelectorAll("img[src]")) {
      expect(img.getAttribute("src")).not.toMatch(/^\s*(javascript|data):/i);
    }
    expect((window as { __xss?: number }).__xss).toBeUndefined();
  });

  it("shows raw HTML as escaped text", async () => {
    const { article } = await openIlsa(`<img src=x onerror="window.__xss=1">`);

    expect(
      await within(article).findByText(`<img src=x onerror="window.__xss=1">`),
    ).toBeInTheDocument();
  });
});
