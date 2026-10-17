import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { analyzeMarkdown } from "../../shared/markdown/analyze";
import { checkMarkdownLimits } from "../../shared/markdown/limits";
import styles from "./PageView.module.css";
import { subPagesAnchor } from "./anchors";

/**
 * « Sommaire »: headings of levels 2 and 3 (F04.R3). A click scrolls to the heading and puts its anchor in the URL;
 * the entry of the section being read is marked (`aria-current`), on click and while scrolling.
 */
export function Toc({
  markdown,
  hasSubPages,
}: {
  readonly markdown: string;
  readonly hasSubPages: boolean;
}) {
  const { t } = useTranslation();
  const entries = useMemo(() => {
    // A page over the safety limits is shown as plain text: no heading to list.
    const found = (checkMarkdownLimits(markdown) !== null ? [] : analyzeMarkdown(markdown).headings)
      .filter((h) => h.level === 2 || h.level === 3)
      .map((h) => ({ anchor: h.anchor, text: h.text, level: h.level }));
    return hasSubPages
      ? [...found, { anchor: subPagesAnchor, text: t("page.subPages"), level: 2 }]
      : found;
  }, [markdown, hasSubPages, t]);
  // After a click, the scroll it causes must not move the mark to another entry.
  const clickedAt = useRef(0);
  const [active, setActive] = useState<string | null>(
    () => decodeURIComponent(window.location.hash.slice(1)) || null,
  );

  // Scroll spy: the last heading above the top third of the reading area is the current section.
  useEffect(() => {
    const root = document.getElementById("main");
    if (root === null || entries.length === 0) return;
    const onScroll = () => {
      if (performance.now() - clickedAt.current < 1000) return;
      const limit = root.getBoundingClientRect().top + root.clientHeight / 3;
      let current: string | null = null;
      for (const entry of entries) {
        const heading = document.getElementById(entry.anchor);
        if (heading !== null && heading.getBoundingClientRect().top <= limit)
          current = entry.anchor;
      }
      if (current !== null) setActive(current);
    };
    root.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      root.removeEventListener("scroll", onScroll);
    };
  }, [entries]);

  if (entries.length === 0) return null;
  return (
    <nav aria-labelledby="toc-title" className={styles["panelSection"]}>
      <h2 id="toc-title" className={styles["panelTitle"]}>
        {t("page.toc")}
      </h2>
      <ol className={styles["toc"]}>
        {entries.map((entry) => (
          <li key={entry.anchor}>
            <a
              href={`#${entry.anchor}`}
              className={`${styles["tocLink"] ?? ""} ${entry.level === 3 ? (styles["tocSub"] ?? "") : ""}`}
              {...(active === entry.anchor ? { "aria-current": "location" as const } : {})}
              onClick={(event) => {
                event.preventDefault();
                clickedAt.current = performance.now();
                setActive(entry.anchor);
                document.getElementById(entry.anchor)?.scrollIntoView({ block: "start" });
                history.replaceState(
                  history.state,
                  "",
                  `${location.pathname}${location.search}#${entry.anchor}`,
                );
              }}
            >
              {entry.text}
            </a>
          </li>
        ))}
      </ol>
    </nav>
  );
}
