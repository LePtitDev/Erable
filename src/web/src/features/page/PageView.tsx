import { useQuery } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { PanelRightClose, PanelRightOpen, Pencil, Plus } from "lucide-react";
import { useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { pageQuery, resolveQuery, type PageView as PageData } from "../../shared/api/pages";
import { projectQuery } from "../../shared/api/projects";
import type { ElementDetails } from "../../shared/api/tree";
import { useUiState } from "../../shared/api/uiState";
import { referenceLanguage } from "../../shared/i18n/languages";
import type { ElementId } from "../../shared/ids/elementId";
import { analyzeMarkdown } from "../../shared/markdown/analyze";
import { Button } from "../../shared/ui/Button";
import { KindBadge } from "../../shared/ui/KindBadge";
import { useTreeMutations } from "../explorer/useTreeMutations";
import { ElementHeader } from "../project/ElementHeader";
import { defaultLayout, type Layout } from "../project/layout";
import { AttachmentsPanel } from "./AttachmentsPanel";
import { anchorsOf, definitionsOf } from "./markdown/markdownSupport";
import { MarkdownView } from "./markdown/MarkdownView";
import contentStyles from "./markdown/MarkdownView.module.css";
import styles from "./PageView.module.css";
import { subPagesAnchor } from "./anchors";
import { Toc } from "./Toc";

/** Reading a page (F04, mockup « Page — lecture »). */
export function PageView({
  projectId,
  element,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
}) {
  const { t } = useTranslation();
  const page = useQuery(pageQuery(projectId, element.id));
  const project = useQuery(projectQuery(projectId));
  const layout = useUiState<Layout>("layout", defaultLayout);
  const navigate = useNavigate();

  useEffect(() => {
    performance.mark("erable:page-open");
  }, [element.id]);

  const sidePanelOpen = layout.value.sidePanelOpen;
  const toggleSidePanel = (
    <button
      type="button"
      className={styles["iconButton"]}
      aria-expanded={sidePanelOpen}
      aria-controls="page-side-panel"
      aria-label={sidePanelOpen ? t("shell.hideSidePanel") : t("shell.showSidePanel")}
      onClick={() => {
        layout.update((l) => ({ sidePanelOpen: !l.sidePanelOpen }));
      }}
    >
      {sidePanelOpen ? (
        <PanelRightClose aria-hidden size={18} />
      ) : (
        <PanelRightOpen aria-hidden size={18} />
      )}
    </button>
  );

  return (
    <div className={styles["page"]}>
      <article className={styles["article"]} aria-labelledby="page-title">
        <ElementHeader
          projectId={projectId}
          element={element}
          title={
            <h1 id="page-title" className={styles["title"]}>
              {element.name}
            </h1>
          }
          actions={
            <>
              {toggleSidePanel}
              <Button
                variant="primary"
                onClick={() => {
                  void navigate({
                    to: "/p/$projectId/e/$elementId",
                    params: { projectId, elementId: element.id },
                    search: { edit: true },
                  });
                }}
              >
                <Pencil aria-hidden size={16} />
                {t("page.edit")}
              </Button>
            </>
          }
        />
        {page.isSuccess ? (
          <PageBody
            projectId={projectId}
            data={page.data}
            contentLanguage={project.data?.contentLanguage ?? referenceLanguage}
          />
        ) : (
          <p aria-busy="true">{t("common.loading")}</p>
        )}
      </article>
      {sidePanelOpen && page.isSuccess && (
        <aside id="page-side-panel" className={styles["side"]} aria-label={t("page.sidePanel")}>
          <Toc markdown={page.data.markdown} hasSubPages={page.data.subPages.length > 0} />
          <AttachmentsPanel
            projectId={projectId}
            pageId={element.id}
            attachments={page.data.attachments}
          />
        </aside>
      )}
    </div>
  );
}

function PageBody({
  projectId,
  data,
  contentLanguage,
}: {
  readonly projectId: ElementId;
  readonly data: PageData;
  readonly contentLanguage: string;
}) {
  const analysis = useMemo(() => analyzeMarkdown(data.markdown), [data.markdown]);
  const ids = useMemo(() => analysis.references.map((r) => r.id), [analysis]);
  const resolved = useQuery(resolveQuery(projectId, ids));
  const context = useMemo(
    () => ({
      projectId,
      pageId: data.element.id,
      resolved: resolved.data ?? new Map(),
      anchors: anchorsOf(analysis.headings),
      definitions: definitionsOf(analysis.tree),
    }),
    [projectId, data.element.id, resolved.data, analysis],
  );

  // « Ouverture d'une page < 200 ms » (performance.mark, docs/testing.md §11): measured once the names are known.
  useEffect(() => {
    if (!resolved.isSuccess) return;
    performance.mark("erable:page-rendered");
    try {
      performance.measure("erable:page", "erable:page-open", "erable:page-rendered");
    } catch {
      // The start mark may be missing on a direct reload; the measure is only indicative.
    }
  }, [resolved.isSuccess, data.element.id]);

  // Opening `…/e/<id>#anchor`: scroll to the heading once the content exists.
  useEffect(() => {
    const anchor = decodeURIComponent(window.location.hash.slice(1));
    if (anchor !== "") document.getElementById(anchor)?.scrollIntoView();
  }, [data.element.id]);

  return (
    <>
      <div className={contentStyles["content"]} lang={contentLanguage}>
        <MarkdownView tree={analysis.tree} context={context} />
      </div>
      <SubPages projectId={projectId} pageId={data.element.id} subPages={data.subPages} />
    </>
  );
}

/** « Sous-pages »: the children pages, and a button that creates one and opens it in the editor (E2E-04-06). */
function SubPages({
  projectId,
  pageId,
  subPages,
}: {
  readonly projectId: ElementId;
  readonly pageId: ElementId;
  readonly subPages: PageData["subPages"];
}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { create } = useTreeMutations(projectId);
  return (
    <section aria-labelledby={subPagesAnchor} className={styles["subPages"]}>
      <h2
        id={subPagesAnchor}
        className={`${contentStyles["heading"] ?? ""} ${styles["sectionTitle"] ?? ""}`}
      >
        {t("page.subPages")}
      </h2>
      <div className={styles["subGrid"]}>
        {subPages.map((sub) => (
          <Link
            key={sub.id}
            to="/p/$projectId/e/$elementId"
            params={{ projectId, elementId: sub.id }}
            className={styles["subCard"]}
          >
            <KindBadge kind="page" />
            {sub.name}
          </Link>
        ))}
        <button
          type="button"
          className={styles["newSub"]}
          disabled={create.isPending}
          onClick={() => {
            create.mutate(
              { kind: "page", parentId: pageId, name: t("page.newSubPageName") },
              {
                onSuccess: (created) => {
                  void navigate({
                    to: "/p/$projectId/e/$elementId",
                    params: { projectId, elementId: created.id },
                    search: { edit: true, focus: "title" },
                  });
                },
              },
            );
          }}
        >
          <Plus aria-hidden size={18} />
          {t("page.newSubPage")}
        </button>
      </div>
    </section>
  );
}
