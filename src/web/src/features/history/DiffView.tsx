import { Fragment } from "react";
import { useTranslation } from "react-i18next";
import type { DiffRow, RevisionDiff } from "../../shared/api/history";
import { foldRows } from "./foldRows";
import styles from "./History.module.css";

function Pieces({
  line,
  tag,
}: {
  readonly line: NonNullable<DiffRow["old"]>;
  readonly tag: "ins" | "del";
}) {
  const Changed = tag;
  return (
    <>
      {line.pieces.map((piece, index) =>
        piece.changed ? (
          <Changed key={index} className={styles[tag]}>
            {piece.text}
          </Changed>
        ) : (
          <Fragment key={index}>{piece.text}</Fragment>
        ),
      )}
    </>
  );
}

/**
 * The comparison of two revisions (F13.R2, E2E-13-02): the text line by line with the changed words marked, then the
 * name, the categories and the properties that differ. Added and removed text is told by `<ins>`/`<del>` and by a sign,
 * never by colour alone.
 */
export function DiffView({ diff }: { readonly diff: RevisionDiff }) {
  const { t } = useTranslation();
  const rows = foldRows(diff.text);
  const textChanged = diff.text.some((r) => r.kind !== "unchanged");
  const nothing =
    !textChanged &&
    !diff.nameChanged &&
    diff.categoriesAdded.length === 0 &&
    diff.categoriesRemoved.length === 0 &&
    diff.values.length === 0;
  return (
    <section
      className={styles["diff"]}
      aria-label={t("history.diffLabel", { from: diff.from.revision, to: diff.to.revision })}
    >
      <h2 className={styles["diffTitle"]}>
        {t("history.diffLabel", { from: diff.from.revision, to: diff.to.revision })}
      </h2>
      {nothing && <p className={styles["muted"]}>{t("history.noDifference")}</p>}
      {diff.nameChanged && (
        <p className={styles["change"]}>
          {t("history.name")} <del className={styles["del"]}>{diff.fromName}</del>{" "}
          <ins className={styles["ins"]}>{diff.toName}</ins>
        </p>
      )}
      {(diff.categoriesAdded.length > 0 || diff.categoriesRemoved.length > 0) && (
        <p className={styles["change"]}>
          {t("history.categories")}{" "}
          {diff.categoriesRemoved.map((c) => (
            <del key={c.id} className={styles["del"]}>
              {c.name}
            </del>
          ))}{" "}
          {diff.categoriesAdded.map((c) => (
            <ins key={c.id} className={styles["ins"]}>
              {c.name}
            </ins>
          ))}
        </p>
      )}
      {diff.values.length > 0 && (
        <ul className={styles["values"]} aria-label={t("history.properties")}>
          {diff.values.map((value) => (
            <li key={value.propertyId} className={styles["change"]}>
              {value.propertyName}{" "}
              {value.old !== null && (
                <del className={styles["del"]}>{JSON.stringify(value.old)}</del>
              )}{" "}
              {value.new !== null ? (
                <ins className={styles["ins"]}>{JSON.stringify(value.new)}</ins>
              ) : (
                <span className={styles["muted"]}>{t("history.valueRemoved")}</span>
              )}
            </li>
          ))}
        </ul>
      )}
      {textChanged && (
        <div className={styles["diffText"]} role="group" aria-label={t("history.text")}>
          {rows.map((row, index) =>
            row === null ? (
              <div key={`gap-${String(index)}`} className={styles["gap"]} aria-hidden>
                …
              </div>
            ) : (
              <RowView
                key={`${String(row.old?.number ?? "")}-${String(row.new?.number ?? "")}-${String(index)}`}
                row={row}
              />
            ),
          )}
        </div>
      )}
    </section>
  );
}

function RowView({ row }: { readonly row: DiffRow }) {
  const { t } = useTranslation();
  switch (row.kind) {
    case "unchanged":
      return <div className={styles["line"]}>{row.new?.text}</div>;
    case "added":
      return (
        <div className={`${styles["line"] ?? ""} ${styles["lineAdded"] ?? ""}`}>
          <span className={styles["sign"]} aria-label={t("history.added")}>
            +
          </span>
          <ins className={styles["ins"]}>{row.new?.text}</ins>
        </div>
      );
    case "removed":
      return (
        <div className={`${styles["line"] ?? ""} ${styles["lineRemoved"] ?? ""}`}>
          <span className={styles["sign"]} aria-label={t("history.removed")}>
            −
          </span>
          <del className={styles["del"]}>{row.old?.text}</del>
        </div>
      );
    case "modified":
      return (
        <>
          <div className={`${styles["line"] ?? ""} ${styles["lineRemoved"] ?? ""}`}>
            <span className={styles["sign"]} aria-label={t("history.removed")}>
              −
            </span>
            <span>{row.old !== null && <Pieces line={row.old} tag="del" />}</span>
          </div>
          <div className={`${styles["line"] ?? ""} ${styles["lineAdded"] ?? ""}`}>
            <span className={styles["sign"]} aria-label={t("history.added")}>
              +
            </span>
            <span>{row.new !== null && <Pieces line={row.new} tag="ins" />}</span>
          </div>
        </>
      );
  }
}
