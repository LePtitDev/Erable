import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { HoverCard } from "radix-ui";
import { useTranslation } from "react-i18next";
import { pageQuery, type ResolvedItem } from "../../../shared/api/pages";
import type { ElementId } from "../../../shared/ids/elementId";
import type { ErableReference } from "../../../shared/markdown/mdastErable";
import { KindBadge } from "../../../shared/ui/KindBadge";
import type { RenderContext } from "./MarkdownView";
import styles from "./MarkdownView.module.css";
import { excerptOf, shortId } from "./text";

/**
 * `[[ID]]` shows the **current** name of its target, `[[ID|texte]]` the text (F04.R1); a deleted or unknown target is
 * a broken link with an explanation, never an error (F04.R2). Hovering shows a preview card (E2E-04-03).
 */
export function Reference({
  node,
  context,
  inLink,
}: {
  readonly node: ErableReference;
  readonly context: RenderContext;
  readonly inLink: boolean;
}) {
  const { t } = useTranslation();
  const item = context.resolved.get(node.id);
  if (item === undefined) {
    return <span className={styles["pending"]}>{node.label ?? shortId(node.id)}</span>;
  }
  const text = node.label ?? item.name ?? shortId(node.id);
  if (item.state !== "existing") {
    const reason = t(`reference.broken.${item.state}`);
    const label = t("reference.brokenLabel", { text, reason });
    if (item.state === "deleted") {
      // F04.R2: a deleted target is still in the trash: the tooltip offers to get it back.
      return (
        <Link
          to="/p/$projectId/trash"
          params={{ projectId: context.projectId }}
          className={styles["broken"]}
          title={t("reference.restore")}
          aria-label={label}
        >
          {text}
        </Link>
      );
    }
    return (
      <span className={styles["broken"]} title={reason} aria-label={label}>
        {text}
      </span>
    );
  }
  if (inLink) return <span className={styles["reference"]}>{text}</span>;
  return (
    <HoverCard.Root openDelay={350} closeDelay={150}>
      <HoverCard.Trigger asChild>
        <Link
          to="/p/$projectId/e/$elementId"
          params={{ projectId: context.projectId, elementId: node.id }}
          {...(node.anchor === null ? {} : { hash: node.anchor })}
          className={styles["reference"]}
        >
          {text}
        </Link>
      </HoverCard.Trigger>
      <HoverCard.Portal>
        <HoverCard.Content className={styles["preview"]} sideOffset={6} align="start">
          <Preview projectId={context.projectId} item={item} />
        </HoverCard.Content>
      </HoverCard.Portal>
    </HoverCard.Root>
  );
}

function Preview({
  projectId,
  item,
}: {
  readonly projectId: ElementId;
  readonly item: ResolvedItem;
}) {
  // Mounted only while the card is open: the page is fetched on the first hover, then cached.
  const page = useQuery({ ...pageQuery(projectId, item.id), enabled: item.kind === "page" });
  const excerpt = page.data === undefined ? null : excerptOf(page.data.markdown);
  return (
    <div className={styles["previewBody"]}>
      <span className={styles["previewTitle"]}>
        {item.kind !== null && <KindBadge kind={item.kind} />}
        <span className={styles["previewName"]}>{item.name}</span>
        <span className={styles["previewId"]}>{shortId(item.id)}</span>
      </span>
      {excerpt !== null && excerpt !== "" && (
        <span className={styles["previewExcerpt"]}>{excerpt}</span>
      )}
      <span className={styles["previewPath"]}>{item.path.map((p) => p.name).join(" / ")}</span>
    </div>
  );
}
