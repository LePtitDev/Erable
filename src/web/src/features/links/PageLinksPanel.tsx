import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { Plus, X } from "lucide-react";
import { useId, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { queryKeys } from "../../shared/api/keys";
import {
  backlinksQuery,
  createLink,
  deleteLink,
  linkTypesQuery,
  pageLinksQuery,
  type PageLinkItem,
} from "../../shared/api/links";
import { treeQuery } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import styles from "./PageLinksPanel.module.css";

/**
 * « Liens » of a page (F09.R2, F04.R4): the typed links it starts, those pointing to it with the inverse label, and
 * apart from them the implicit references (pages that mention it in their text, E2E-09-03). « + Lier » links the page
 * to another one (E2E-09-02); links are saved at once, they are not part of the page edit.
 */
export function PageLinksPanel({
  projectId,
  pageId,
}: {
  readonly projectId: ElementId;
  readonly pageId: ElementId;
}) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const links = useQuery(pageLinksQuery(projectId, pageId));
  const backlinks = useQuery(backlinksQuery(projectId, pageId));
  const [linking, setLinking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = () => client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });

  const remove = (link: PageLinkItem) => {
    setError(null);
    deleteLink(projectId, link.id)
      .then(refresh)
      .catch((failure: unknown) => {
        setError(errorMessage(t, failure));
      });
  };

  const outgoing = links.data?.outgoing ?? [];
  const incoming = links.data?.incoming ?? [];
  const mentions = (backlinks.data ?? []).filter((b) => b.sourceKind === "page");

  return (
    <section className={styles["panel"]} aria-labelledby="page-links">
      <div className={styles["head"]}>
        <h2 id="page-links" className={styles["title"]}>
          {t("links.title")}
        </h2>
        <Button
          size="sm"
          onClick={() => {
            setLinking(true);
          }}
        >
          <Plus aria-hidden size={14} />
          {t("links.link")}
        </Button>
      </div>
      {error !== null && (
        <p role="alert" className={styles["error"]}>
          {error}
        </p>
      )}
      <LinkList
        projectId={projectId}
        label={t("links.outgoing")}
        empty={t("links.noOutgoing")}
        links={outgoing}
        onRemove={remove}
      />
      <LinkList
        projectId={projectId}
        label={t("links.incoming", { count: incoming.length })}
        empty={t("links.noIncoming")}
        links={incoming}
        onRemove={remove}
      />
      <h3 className={styles["subtitle"]}>{t("links.mentions", { count: mentions.length })}</h3>
      {mentions.length === 0 ? (
        <p className={styles["muted"]}>{t("links.noMentions")}</p>
      ) : (
        <ul className={styles["list"]} aria-label={t("links.mentionsList")}>
          {mentions.map((mention) => (
            <li key={mention.sourceId}>
              <Link
                to="/p/$projectId/e/$elementId"
                params={{ projectId, elementId: mention.sourceId }}
                className={styles["page"]}
              >
                {mention.sourceName}
              </Link>
            </li>
          ))}
        </ul>
      )}
      <LinkDialog
        projectId={projectId}
        pageId={pageId}
        open={linking}
        onClose={() => {
          setLinking(false);
        }}
        onLinked={() => {
          setLinking(false);
          void refresh();
        }}
      />
    </section>
  );
}

function LinkList({
  projectId,
  label,
  empty,
  links,
  onRemove,
}: {
  readonly projectId: ElementId;
  readonly label: string;
  readonly empty: string;
  readonly links: readonly PageLinkItem[];
  readonly onRemove: (link: PageLinkItem) => void;
}) {
  const { t } = useTranslation();
  const headingId = useId();
  return (
    <>
      <h3 id={headingId} className={styles["subtitle"]}>
        {label}
      </h3>
      {links.length === 0 ? (
        <p className={styles["muted"]}>{empty}</p>
      ) : (
        <ul className={styles["list"]} aria-labelledby={headingId}>
          {links.map((link) => (
            <li key={link.id} className={styles["row"]}>
              <span>
                <span className={styles["label"]}>{link.label}</span>
                {" · "}
                <Link
                  to="/p/$projectId/e/$elementId"
                  params={{ projectId, elementId: link.pageId }}
                  className={styles["page"]}
                >
                  {link.pageName}
                </Link>
              </span>
              <button
                type="button"
                className={styles["remove"]}
                aria-label={t("links.remove", { label: link.label, name: link.pageName })}
                onClick={() => {
                  onRemove(link);
                }}
              >
                <X aria-hidden size={14} />
              </button>
            </li>
          ))}
        </ul>
      )}
    </>
  );
}

/** « + Lier »: the type, then the page at the other end (E2E-09-02). */
function LinkDialog({
  projectId,
  pageId,
  open,
  onClose,
  onLinked,
}: {
  readonly projectId: ElementId;
  readonly pageId: ElementId;
  readonly open: boolean;
  readonly onClose: () => void;
  readonly onLinked: () => void;
}) {
  const { t } = useTranslation();
  const types = useQuery(linkTypesQuery(projectId));
  const tree = useQuery(treeQuery(projectId));
  const typeId = useId();
  const pageField = useId();
  const [type, setType] = useState("");
  const [target, setTarget] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const pages = useMemo(
    () =>
      (tree.data?.nodes ?? [])
        .filter((n) => n.kind === "page" && n.id !== pageId)
        .sort((a, b) => a.name.localeCompare(b.name)),
    [tree.data, pageId],
  );

  const close = () => {
    setType("");
    setTarget("");
    setError(null);
    onClose();
  };

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await createLink(projectId, pageId, type as ElementId, target as ElementId);
      setType("");
      setTarget("");
      onLinked();
    } catch (failure) {
      setError(errorMessage(t, failure));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) close();
      }}
      title={t("links.linkTitle")}
      footer={
        <>
          <Button onClick={close}>{t("common.cancel")}</Button>
          <Button
            variant="primary"
            disabled={type === "" || target === "" || saving}
            onClick={() => {
              void save();
            }}
          >
            {t("links.confirm")}
          </Button>
        </>
      }
    >
      <div className={styles["dialogBody"]}>
        <label htmlFor={typeId} className={styles["fieldLabel"]}>
          {t("links.type")}
        </label>
        <select
          id={typeId}
          className={styles["select"]}
          value={type}
          onChange={(event) => {
            setType(event.target.value);
          }}
        >
          <option value="">{t("property.empty")}</option>
          {(types.data ?? []).map((item) => (
            <option key={item.id} value={item.id}>
              {item.name}
            </option>
          ))}
        </select>
        <label htmlFor={pageField} className={styles["fieldLabel"]}>
          {t("links.target")}
        </label>
        <select
          id={pageField}
          className={styles["select"]}
          value={target}
          onChange={(event) => {
            setTarget(event.target.value);
          }}
        >
          <option value="">{t("property.empty")}</option>
          {pages.map((page) => (
            <option key={page.id} value={page.id}>
              {page.name}
            </option>
          ))}
        </select>
        {error !== null && (
          <p role="alert" className={styles["error"]}>
            {error}
          </p>
        )}
      </div>
    </Dialog>
  );
}
