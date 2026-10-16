import { Fragment } from "react";
import type { TextSegment } from "../../shared/api/search";
import styles from "./SearchView.module.css";

/**
 * Text in which the matching passages are marked. The backend sends segments, never HTML: the passages are rendered as
 * React text inside `<mark>`, so that nothing a page contains can inject markup (M3-03). Segments have no identity of
 * their own, their position is their key.
 */
export function Highlighted({ segments }: { readonly segments: readonly TextSegment[] }) {
  return (
    <>
      {segments.map((segment, index) =>
        segment.hit ? (
          <mark key={index} className={styles["mark"]}>
            {segment.text}
          </mark>
        ) : (
          <Fragment key={index}>{segment.text}</Fragment>
        ),
      )}
    </>
  );
}
