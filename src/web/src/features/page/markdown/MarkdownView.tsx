import type { Definition, Heading, Root, RootContent } from "mdast";
import { Fragment, type ReactNode } from "react";
import type { ResolvedItem } from "../../../shared/api/pages";
import type { ElementId } from "../../../shared/ids/elementId";
import { Embed } from "./Embed";
import { safeUrl } from "./markdownSupport";
import styles from "./MarkdownView.module.css";
import { Reference } from "./Reference";

export interface RenderContext {
  readonly projectId: ElementId;
  /** The page being shown: an embed of one of its own attachments has no « Pièce jointe de … » caption. */
  readonly pageId: ElementId;
  readonly resolved: ReadonlyMap<ElementId, ResolvedItem>;
  readonly anchors: ReadonlyMap<Heading, string>;
  readonly definitions: ReadonlyMap<string, Definition>;
}

/** No link inside a link (docs/markdown-syntax.md R8). */
interface RenderState {
  readonly inLink: boolean;
}

const externalRel = "noopener noreferrer nofollow";
const outside: RenderState = { inLink: false };
const inside: RenderState = { inLink: true };

/**
 * Renders the syntax tree of a page with React elements only: text is always escaped by React, raw HTML is shown as
 * text (F04.R5), URLs go through {@link safeUrl}. Nothing is ever injected as HTML, so no sanitizer is needed.
 */
export function MarkdownView({
  tree,
  context,
}: {
  readonly tree: Root;
  readonly context: RenderContext;
}) {
  return <>{all(tree.children, context, outside)}</>;
}

function all(nodes: readonly RootContent[], context: RenderContext, state: RenderState): ReactNode {
  return nodes.map((node, i) => <Fragment key={i}>{render(node, context, state)}</Fragment>);
}

function render(node: RootContent, context: RenderContext, state: RenderState): ReactNode {
  switch (node.type) {
    case "paragraph":
      // A paragraph made of embeds only holds figures, which cannot live in a <p>.
      return node.children.every(
        (c) => c.type === "erableEmbed" || (c.type === "text" && c.value.trim() === ""),
      ) ? (
        <div className={styles["embeds"]}>
          {all(
            node.children.filter((c) => c.type === "erableEmbed"),
            context,
            state,
          )}
        </div>
      ) : (
        <p>{all(node.children, context, state)}</p>
      );
    case "heading": {
      const Tag = `h${String(node.depth)}` as "h2";
      return (
        <Tag id={context.anchors.get(node)} className={styles["heading"]}>
          {all(withoutExplicitAnchor(node), context, state)}
        </Tag>
      );
    }
    case "thematicBreak":
      return <hr />;
    case "blockquote":
      return (
        <blockquote className={styles["quote"]}>{all(node.children, context, state)}</blockquote>
      );
    case "list": {
      const items = all(node.children, context, state);
      if (node.ordered === true) {
        return (
          <ol
            {...(typeof node.start === "number" && node.start !== 1 ? { start: node.start } : {})}
          >
            {items}
          </ol>
        );
      }
      const tasks = node.children.some((c) => typeof c.checked === "boolean");
      return <ul className={tasks ? styles["tasks"] : undefined}>{items}</ul>;
    }
    case "listItem":
      return (
        <li>
          {typeof node.checked === "boolean" && (
            <input
              type="checkbox"
              checked={node.checked}
              disabled
              readOnly
              className={styles["task"]}
            />
          )}
          {node.spread === true
            ? all(node.children, context, state)
            : node.children.map((child, i) => (
                <Fragment key={i}>
                  {child.type === "paragraph"
                    ? all(child.children, context, state)
                    : render(child, context, state)}
                </Fragment>
              ))}
        </li>
      );
    case "table": {
      const [head, ...body] = node.children;
      const align = (i: number) => node.align?.[i] ?? undefined;
      return (
        <div className={styles["tableWrap"]} data-scroll-x>
          <table>
            {head !== undefined && (
              <thead>
                <tr>
                  {head.children.map((cell, i) => (
                    <th key={i} style={align(i) ? { textAlign: align(i) ?? undefined } : undefined}>
                      {all(cell.children, context, state)}
                    </th>
                  ))}
                </tr>
              </thead>
            )}
            <tbody>
              {body.map((row, r) => (
                <tr key={r}>
                  {row.children.map((cell, i) => (
                    <td key={i} style={align(i) ? { textAlign: align(i) ?? undefined } : undefined}>
                      {all(cell.children, context, state)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
    }
    case "code":
      return (
        <pre className={styles["code"]} data-scroll-x>
          <code>{node.value}</code>
        </pre>
      );
    case "html":
      // Raw HTML is disabled: shown as the text it is (F04.R5, docs/architecture.md §9).
      return <span className={styles["rawHtml"]}>{node.value}</span>;
    case "text":
      return node.value;
    case "emphasis":
      return <em>{all(node.children, context, state)}</em>;
    case "strong":
      return <strong>{all(node.children, context, state)}</strong>;
    case "delete":
      return <del>{all(node.children, context, state)}</del>;
    case "inlineCode":
      return <code className={styles["inlineCode"]}>{node.value}</code>;
    case "break":
      return <br />;
    case "link":
      return link(node.url, all(node.children, context, inside), state);
    case "linkReference": {
      const definition = context.definitions.get(node.identifier.toLowerCase());
      const children = all(node.children, context, definition === undefined ? state : inside);
      return definition === undefined ? children : link(definition.url, children, state);
    }
    case "image":
      return image(node.url, node.alt);
    case "imageReference": {
      const definition = context.definitions.get(node.identifier.toLowerCase());
      return definition === undefined ? (node.alt ?? "") : image(definition.url, node.alt);
    }
    case "footnoteReference":
      return <sup>{node.label ?? node.identifier}</sup>;
    case "erableReference":
      return <Reference node={node} context={context} inLink={state.inLink} />;
    case "erableEmbed":
      return <Embed node={node} context={context} />;
    case "definition":
    case "footnoteDefinition":
    case "yaml":
    case "tableRow":
    case "tableCell":
      return null;
  }
}

/** The ` {#anchor}` suffix of an explicit anchor is not displayed (docs/markdown-syntax.md §3). */
function withoutExplicitAnchor(node: Heading): Heading["children"] {
  const last = node.children.at(-1);
  if (last?.type !== "text") return node.children;
  const value = last.value.replace(/\s+\{#[A-Za-z0-9_-]+\}$/, "");
  return value === last.value ? node.children : [...node.children.slice(0, -1), { ...last, value }];
}

function link(url: string, children: ReactNode, state: RenderState): ReactNode {
  const href = safeUrl(url, "link");
  if (state.inLink || href === null) return <span className={styles["deadLink"]}>{children}</span>;
  const external = !href.startsWith("#") && !href.startsWith("/");
  return (
    <a
      href={href}
      className={styles["link"]}
      {...(external ? { target: "_blank", rel: externalRel } : {})}
    >
      {children}
    </a>
  );
}

function image(url: string, alt: string | null | undefined): ReactNode {
  const src = safeUrl(url, "image");
  return src === null ? (
    (alt ?? "")
  ) : (
    <img
      src={src}
      alt={alt ?? ""}
      loading="lazy"
      referrerPolicy="no-referrer"
      className={styles["image"]}
    />
  );
}
