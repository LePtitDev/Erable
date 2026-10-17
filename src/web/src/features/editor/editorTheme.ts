import { HighlightStyle, syntaxHighlighting } from "@codemirror/language";
import type { Extension } from "@codemirror/state";
import { EditorView } from "@codemirror/view";
import { tags } from "@lezer/highlight";

/** The « image » glyph (Lucide) used as a CSS mask, so that it takes the colour of the pill. */
const embedGlyph =
  "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='none' stroke='black' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Crect width='18' height='18' x='3' y='3' rx='2'/%3E%3Ccircle cx='9' cy='9' r='2'/%3E%3Cpath d='m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21'/%3E%3C/svg%3E\")";

/**
 * Editor look of the mockup « Page — édition »: IBM Plex Mono 14/26, line numbers, pills in the UI font with the
 * colours of their kind. Only design tokens: light and dark come from the CSS variables (CLAUDE.md §6.8).
 */
export const editorTheme: Extension = [
  EditorView.theme({
    "&": {
      height: "100%",
      backgroundColor: "var(--color-editor-bg)",
      color: "var(--color-text)",
      fontSize: "14px",
    },
    "&.cm-focused": { outline: "none" },
    ".cm-scroller": { fontFamily: "var(--font-mono)", lineHeight: "26px", padding: "20px 0 40px" },
    ".cm-content": { caretColor: "var(--color-accent)", paddingRight: "24px" },
    ".cm-cursor, .cm-dropCursor": {
      borderLeftColor: "var(--color-accent)",
      borderLeftWidth: "2px",
    },
    ".cm-gutters": {
      backgroundColor: "var(--color-editor-bg)",
      color: "var(--color-text-faint)",
      border: "none",
      minWidth: "56px",
    },
    ".cm-lineNumbers .cm-gutterElement": { paddingRight: "16px" },
    ".cm-activeLine, .cm-activeLineGutter": { backgroundColor: "var(--color-editor-line)" },
    "&.cm-focused .cm-selectionBackground, .cm-selectionBackground, ::selection": {
      backgroundColor: "var(--color-accent-soft)",
    },
    ".cm-pill": {
      display: "inline-flex",
      alignItems: "center",
      height: "22px",
      padding: "0 7px",
      margin: "0 1px",
      borderRadius: "5px",
      verticalAlign: "1px",
      whiteSpace: "nowrap",
      fontFamily: "var(--font-ui)",
      fontSize: "13px",
      fontWeight: "500",
      lineHeight: "1",
      background: "var(--color-kind-page-soft)",
      color: "var(--color-kind-page)",
      cursor: "default",
    },
    ".cm-pill[data-kind='category']": {
      background: "var(--color-kind-category-soft)",
      color: "var(--color-kind-category)",
    },
    ".cm-pill[data-kind='propertyDefinition']": {
      background: "var(--color-kind-property-soft)",
      color: "var(--color-kind-property)",
    },
    ".cm-pill[data-kind='linkType']": {
      background: "var(--color-kind-link-soft)",
      color: "var(--color-kind-link)",
    },
    ".cm-pill[data-kind='attachment'], .cm-pill-embed": {
      background: "var(--color-kind-attachment-soft)",
      color: "var(--color-kind-attachment)",
    },
    // `![[ID]]` (shown in the page) against `[[ID]]` (a link): the embed pill carries an image glyph and a frame, the
    // « ! » of the source being swallowed by the pill.
    ".cm-pill-embed": {
      boxShadow: "inset 0 0 0 1px currentColor",
    },
    ".cm-pill-embed::before": {
      content: '""',
      display: "inline-block",
      width: "13px",
      height: "13px",
      marginRight: "5px",
      verticalAlign: "-2px",
      backgroundColor: "currentColor",
      mask: `${embedGlyph} center / contain no-repeat`,
      WebkitMask: `${embedGlyph} center / contain no-repeat`,
    },
    ".cm-pill-broken": {
      background: "var(--color-danger-soft)",
      color: "var(--color-danger)",
      textDecoration: "line-through",
    },
    ".cm-pill-pending": {
      background: "var(--color-sunken)",
      color: "var(--color-text-muted)",
      fontStyle: "italic",
    },
  }),
  syntaxHighlighting(
    HighlightStyle.define([
      { tag: tags.heading, fontWeight: "600", color: "var(--color-text)" },
      { tag: tags.strong, fontWeight: "700" },
      { tag: tags.emphasis, fontStyle: "italic" },
      { tag: tags.strikethrough, textDecoration: "line-through" },
      {
        tag: [tags.processingInstruction, tags.punctuation, tags.contentSeparator],
        color: "var(--color-text-faint)",
      },
      { tag: [tags.link, tags.url], color: "var(--color-kind-page)" },
      { tag: tags.monospace, color: "var(--color-kind-property)" },
      { tag: tags.quote, color: "var(--color-text-muted)" },
      { tag: tags.list, color: "var(--color-accent-text)" },
    ]),
  ),
];
