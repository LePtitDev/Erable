// Development-only page for the visual review of the design tokens (M0-05). Not part of production builds.
import { useSyncExternalStore } from "react";
import { contrastRatio } from "../shared/theme/contrast";
import { ThemeSwitcher } from "../shared/theme/ThemeSwitcher";
import { useThemeStore } from "../shared/theme/theme";
import styles from "./TokensPage.module.css";

const colorGroups: Record<string, string[]> = {
  Surfaces: [
    "bg",
    "surface",
    "sunken",
    "hover",
    "border",
    "border-strong",
    "editor-bg",
    "editor-line",
  ],
  Text: ["text", "text-muted", "text-faint"],
  Accent: [
    "accent",
    "accent-soft",
    "accent-text",
    "on-accent",
    "danger",
    "danger-soft",
    "highlight",
  ],
  Kinds: [
    "kind-page",
    "kind-page-soft",
    "kind-category",
    "kind-category-soft",
    "kind-property",
    "kind-property-soft",
    "kind-link",
    "kind-link-soft",
    "kind-attachment",
    "kind-attachment-soft",
  ],
};

const kinds = [
  ["P", "kindPage", "Page"],
  ["C", "kindCategory", "Catégorie"],
  ["Pr", "kindProperty", "Propriété"],
  ["L", "kindLink", "Lien"],
  ["PJ", "kindAttachment", "Pièce jointe"],
] as const;

const colorNames = Object.values(colorGroups).flat();
const darkQuery = window.matchMedia("(prefers-color-scheme: dark)");

function subscribe(onChange: () => void) {
  darkQuery.addEventListener("change", onChange);
  return () => {
    darkQuery.removeEventListener("change", onChange);
  };
}

function snapshot() {
  const style = getComputedStyle(document.documentElement);
  return colorNames.map((n) => style.getPropertyValue(`--color-${n}`).trim()).join("|");
}

/** Resolved token values; re-read when the theme preference or the OS scheme changes. */
function useResolvedColors(): Record<string, string> {
  useThemeStore((s) => s.preference);
  const values = useSyncExternalStore(subscribe, snapshot).split("|");
  return Object.fromEntries(colorNames.map((n, i) => [n, values[i] ?? ""]));
}

export function TokensPage() {
  const colors = useResolvedColors();
  const surface = colors["surface"] ?? "";

  return (
    <main className={styles["page"]}>
      <header className={styles["header"]}>
        <h1 className={styles["title"]}>Design tokens</h1>
        <ThemeSwitcher />
      </header>

      {Object.entries(colorGroups).map(([group, names]) => (
        <section key={group}>
          <h2 className={styles["section"]}>{group}</h2>
          <ul className={styles["swatches"]}>
            {names.map((name) => {
              const value = colors[name] ?? "";
              const ratio =
                value.startsWith("#") && surface.startsWith("#")
                  ? contrastRatio(value, surface).toFixed(2)
                  : "–";
              return (
                <li key={name} className={styles["swatch"]}>
                  <span className={styles["chip"]} style={{ background: `var(--color-${name})` }} />
                  <code>--color-{name}</code>
                  <span className={styles["meta"]}>
                    {value} · {ratio}:1 / surface
                  </span>
                </li>
              );
            })}
          </ul>
        </section>
      ))}

      <section>
        <h2 className={styles["section"]}>Badges</h2>
        <div className={styles["badges"]}>
          {kinds.map(([letter, kind, label]) => (
            <span key={kind} className={styles["badgeRow"]}>
              <span className={`${styles["badge"] ?? ""} ${styles[kind] ?? ""}`}>{letter}</span>
              {label}
            </span>
          ))}
        </div>
      </section>

      <section>
        <h2 className={styles["section"]}>Typographie</h2>
        <p className={styles["h1"]}>Ilsa Varenne</p>
        <p className={styles["h2"]}>Histoire</p>
        <p className={styles["content"]}>
          Marchande itinérante du Col gelé, ancienne membre de l&apos;Ordre du Givre.
        </p>
        <p>Texte d&apos;interface 13 px · IBM Plex Sans</p>
        <p className={styles["muted"]}>Texte atténué (muted)</p>
        <p className={styles["faint"]}>Texte discret (faint) · modifiée il y a 2 h</p>
        <p className={styles["mono"]}>3F2A 9C14 E07B 5D21</p>
        <button type="button" className={styles["button"]}>
          Bouton principal
        </button>
      </section>
    </main>
  );
}
