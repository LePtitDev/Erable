import { HighlightStyle, syntaxHighlighting } from "@codemirror/language";
import type { Extension } from "@codemirror/state";
import { EditorView } from "@codemirror/view";
import { tags } from "@lezer/highlight";

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
