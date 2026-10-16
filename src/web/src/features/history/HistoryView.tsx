import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams, useSearch } from "@tanstack/react-router";
import { ArrowLeft, RotateCcw } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import {
  restoreRevision,
  revisionDiffQuery,
  revisionsQuery,
  type RevisionItem,
  type RevisionRestored,
} from "../../shared/api/history";
import { queryKeys } from "../../shared/api/keys";
import { elementQuery } from "../../shared/api/tree";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { useAuthorName } from "../project/authors";
import { NotFoundScreen } from "../project/ErrorScreens";
import { DiffView } from "./DiffView";
import { useCanEdit } from "../project/access";
import styles from "./History.module.css";

/** `/p/:projectId/e/:elementId/history`: the revisions of a page, compared two by two and restorable (F13). */
export function HistoryView() {
  const { projectId, elementId } = useParams({ from: "/p/$projectId/e/$elementId/history" });
  if (projectId === null || elementId === null) return <NotFoundScreen />;
  return <History projectId={projectId} elementId={elementId} />;
}

function History({
  projectId,
  elementId,
}: {
  readonly projectId: ElementId;
  readonly elementId: ElementId;
}) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const format = useFormatters();
  const authorName = useAuthorName();
  const canEdit = useCanEdit();
  const navigate = useNavigate({ from: "/p/$projectId/e/$elementId/history" });
  const { from, to } = useSearch({ from: "/p/$projectId/e/$elementId/history" });
  const element = useQuery(elementQuery(projectId, elementId));
  const revisions = useQuery(revisionsQuery(projectId, elementId));
  const compared = from !== undefined && to !== undefined;
  const diff = useQuery({
    ...revisionDiffQuery(projectId, elementId, from ?? 0, to ?? 0),
    enabled: compared,
  });
  const [restored, setRestored] = useState<RevisionRestored | null>(null);
  const [error, setError] = useState<string | null>(null);
  // One revision ticked is not in the URL yet: only a pair is a comparison.
  const [picked, setPicked] = useState<number[]>(() =>
    [from, to].filter((n): n is number => n !== undefined),
  );

  if (element.isError || revisions.isError) return <NotFoundScreen />;
  const list = revisions.data ?? [];
  const selected = picked;
  const latest = list[0]?.revision;
  const isPage = element.data?.kind === "page";

  const select = (revision: number, checked: boolean) => {
    const next = checked
      ? [...selected, revision].slice(-2)
      : selected.filter((n) => n !== revision);
    setPicked(next);
    const [a, b] = next.toSorted((x, y) => x - y);
    void navigate({
      to: ".",
      search: a !== undefined && b !== undefined ? { from: a, to: b } : {},
      replace: true,
    });
  };

  const restore = (revision: number) => {
    setError(null);
    setRestored(null);
    restoreRevision(projectId, elementId, revision)
      .then(async (result) => {
        setRestored(result);
        await client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });
      })
      .catch((failure: unknown) => {
        setError(errorMessage(t, failure));
      });
  };

  // The kinds of change come from the API as words; each has its translation (history.change.*).
  const change = t as unknown as (key: string) => string;
  const describe = (revision: RevisionItem): string =>
    revision.restoredFrom === null
      ? revision.changes.map((c) => change(`history.change.${c}`)).join(" · ")
      : t("history.restoration", { revision: revision.restoredFrom });

  return (
    <section className={styles["view"]} aria-labelledby="history-title">
      <header className={styles["header"]}>
        <Link
          to="/p/$projectId/e/$elementId"
          params={{ projectId, elementId }}
          className={styles["back"]}
        >
          <ArrowLeft aria-hidden size={16} />
          {t("history.back")}
        </Link>
        <h1 id="history-title" className={styles["title"]}>
          {t("history.title", { name: element.data?.name ?? "" })}
        </h1>
        <p className={styles["muted"]}>{t("history.hint")}</p>
      </header>
      {restored !== null && (
        <p role="status" className={styles["notice"]}>
          {t("history.restored", { revision: restored.revision })}
          {restored.skippedCategories + restored.skippedValues > 0 &&
            ` ${t("history.skipped", { count: restored.skippedCategories + restored.skippedValues })}`}
        </p>
      )}
      {error !== null && (
        <p role="alert" className={styles["error"]}>
          {error}
        </p>
      )}
      {revisions.isSuccess && (
        <ol className={styles["list"]} aria-label={t("history.list")}>
          {list.map((revision) => (
            <li key={revision.revision} className={styles["entry"]}>
              <label className={styles["pick"]}>
                <input
                  type="checkbox"
                  checked={selected.includes(revision.revision)}
                  onChange={(event) => {
                    select(revision.revision, event.target.checked);
                  }}
                  aria-label={t("history.compare", { revision: revision.revision })}
                />
              </label>
              <span className={styles["number"]}>
                {t("history.revision", { revision: revision.revision })}
              </span>
              <span className={styles["text"]}>
                <span className={styles["what"]}>{describe(revision)}</span>
                <span className={styles["who"]}>
                  {authorName(revision.author, revision.authorName)} ·{" "}
                  <time dateTime={revision.at}>{format.dateTime(new Date(revision.at))}</time>
                </span>
              </span>
              {canEdit && isPage && revision.revision !== latest && (
                <Button
                  size="sm"
                  onClick={() => {
                    restore(revision.revision);
                  }}
                  aria-label={t("history.restore", { revision: revision.revision })}
                >
                  <RotateCcw aria-hidden size={14} />
                  {t("history.restoreShort")}
                </Button>
              )}
            </li>
          ))}
        </ol>
      )}
      {!compared && <p className={styles["muted"]}>{t("history.pickTwo")}</p>}
      {compared && diff.isSuccess && <DiffView diff={diff.data} />}
      {compared && diff.isError && (
        <p role="alert" className={styles["error"]}>
          {errorMessage(t, diff.error)}
        </p>
      )}
    </section>
  );
}
