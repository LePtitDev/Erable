import { Link } from "@tanstack/react-router";
import { FileX } from "lucide-react";
import type { CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { attachmentUrl, type AttachmentItem } from "../../../shared/api/pages";
import type { ErableEmbed } from "../../../shared/markdown/mdastErable";
import { FileCard } from "../../../shared/ui/FileCard";
import type { RenderContext } from "./MarkdownView";
import styles from "./MarkdownView.module.css";
import { Reference } from "./Reference";

const alignClass = { left: "alignLeft", center: "alignCenter", right: "alignRight" } as const;

/**
 * `![[ID]]`: images, videos (streamed with Range, F06.R3), audio and PDF are shown; any other file, **SVG and HTML
 * included**, is a download card and never enters the DOM as markup (F06.R2, docs/architecture.md §9).
 */
export function Embed({
  node,
  context,
}: {
  readonly node: ErableEmbed;
  readonly context: RenderContext;
}) {
  const { t } = useTranslation();
  const item = context.resolved.get(node.id);
  if (item === undefined) return <span className={styles["embedPending"]} aria-busy="true" />;
  if (item.state !== "existing") {
    return (
      <span role="img" className={styles["embedMissing"]} aria-label={t("embed.missing")}>
        <FileX aria-hidden size={20} />
        {t("embed.missing")}
      </span>
    );
  }
  const attachment = item.attachment;
  if (item.kind !== "attachment" || attachment === null || attachment === undefined) {
    // Embedding a page or another element: shown as a reference.
    return (
      <Reference
        node={{ type: "erableReference", id: node.id, anchor: null, label: node.label }}
        context={context}
        inLink={false}
      />
    );
  }

  const owner = item.path.at(-1);
  const foreign = owner !== undefined && attachment.pageId !== context.pageId;
  const style: CSSProperties = {
    ...(node.attrs?.width === undefined || node.attrs.width === null
      ? {}
      : { width: `min(100%, ${String(node.attrs.width)}px)` }),
  };
  const align = node.attrs?.align ?? null;
  const caption = (
    <figcaption className={styles["caption"]}>
      {node.label !== null && <>{node.label} · </>}
      {foreign && (
        <>
          {t("embed.attachmentOf")}{" "}
          <Link
            to="/p/$projectId/e/$elementId"
            params={{ projectId: context.projectId, elementId: owner.id }}
            className={styles["reference"]}
          >
            {owner.name}
          </Link>{" "}
          ·{" "}
        </>
      )}
      <span className={styles["fileName"]}>{attachment.fileName}</span>
    </figcaption>
  );

  return (
    <figure
      className={`${styles["figure"] ?? ""} ${align === null ? "" : (styles[alignClass[align]] ?? "")}`}
      style={style}
    >
      <Media projectId={context.projectId} attachment={attachment} label={node.label} />
      {caption}
    </figure>
  );
}

function Media({
  projectId,
  attachment,
  label,
}: {
  readonly projectId: RenderContext["projectId"];
  readonly attachment: AttachmentItem;
  readonly label: string | null;
}) {
  const src = attachmentUrl(projectId, attachment.id);
  const alt = label ?? attachment.fileName;
  switch (attachment.display) {
    case "image":
      return <img src={src} alt={alt} loading="lazy" className={styles["media"]} />;
    case "video":
      return (
        <video src={src} controls preload="metadata" className={styles["media"]} aria-label={alt} />
      );
    case "audio":
      return (
        <audio src={src} controls preload="metadata" className={styles["audio"]} aria-label={alt} />
      );
    case "pdf":
      return <iframe src={src} title={alt} className={styles["pdf"]} />;
    case "file":
      return <FileCard projectId={projectId} attachment={attachment} />;
  }
}
