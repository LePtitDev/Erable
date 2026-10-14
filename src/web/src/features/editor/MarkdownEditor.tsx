import { defaultKeymap, history, historyKeymap, indentWithTab } from "@codemirror/commands";
import { Compartment, EditorState, type Extension, type TransactionSpec } from "@codemirror/state";
import {
  EditorView,
  highlightActiveLine,
  highlightActiveLineGutter,
  keymap,
  lineNumbers,
  placeholder,
} from "@codemirror/view";
import { forwardRef, useEffect, useImperativeHandle, useRef } from "react";
import type { ElementId } from "../../shared/ids/elementId";
import { editorTheme } from "./editorTheme";
import { markdownWithReferences, pills, setRawIds, setTargets, type PillTarget } from "./pills";

export interface MarkdownEditorHandle {
  readonly view: EditorView | null;
  /** Applies a transaction built from the current state (toolbar commands), then gives the focus back. */
  run: (build: (state: EditorState) => TransactionSpec) => void;
  focus: () => void;
}

interface MarkdownEditorProps {
  readonly initial: string;
  readonly label: string;
  readonly placeholderText: string;
  readonly targets: ReadonlyMap<ElementId, PillTarget>;
  readonly rawIds: boolean;
  readonly onChange: (doc: string) => void;
  readonly onCursor: (line: number, column: number) => void;
  readonly onSave: () => void;
  /** Extensions added by other features (insert palette, attachments). */
  readonly extensions?: Extension;
}

/**
 * CodeMirror 6 with the Markdown language, the pills of references (F05.R1–R2) and the design tokens. The text is
 * the document, always with raw IDs: pills only change how it looks.
 */
export const MarkdownEditor = forwardRef<MarkdownEditorHandle, MarkdownEditorProps>(
  function MarkdownEditor(props, ref) {
    const host = useRef<HTMLDivElement>(null);
    const view = useRef<EditorView | null>(null);
    const callbacks = useRef(props);
    const extra = useRef(new Compartment());
    callbacks.current = props;

    useImperativeHandle(ref, () => ({
      get view() {
        return view.current;
      },
      run: (build) => {
        const v = view.current;
        if (v === null) return;
        v.dispatch(build(v.state));
        v.focus();
      },
      focus: () => view.current?.focus(),
    }));

    // Created once: the editor owns its state; the parent reads it through onChange.
    useEffect(() => {
      if (host.current === null) return;
      const v = new EditorView({
        parent: host.current,
        state: EditorState.create({
          doc: callbacks.current.initial,
          extensions: [
            lineNumbers(),
            highlightActiveLine(),
            highlightActiveLineGutter(),
            history(),
            EditorView.lineWrapping,
            markdownWithReferences(),
            pills(),
            editorTheme,
            placeholder(callbacks.current.placeholderText),
            EditorView.contentAttributes.of({
              "aria-label": callbacks.current.label,
              "aria-multiline": "true",
            }),
            keymap.of([
              {
                key: "Mod-s",
                preventDefault: true,
                run: () => {
                  callbacks.current.onSave();
                  return true;
                },
              },
              ...defaultKeymap,
              ...historyKeymap,
              indentWithTab,
            ]),
            EditorView.updateListener.of((update) => {
              if (update.docChanged) callbacks.current.onChange(update.state.doc.toString());
              if (update.selectionSet || update.docChanged) {
                const head = update.state.selection.main.head;
                const line = update.state.doc.lineAt(head);
                callbacks.current.onCursor(line.number, head - line.from + 1);
              }
            }),
            extra.current.of(callbacks.current.extensions ?? []),
          ],
        }),
      });
      view.current = v;
      return () => {
        v.destroy();
        view.current = null;
      };
    }, []);

    useEffect(() => {
      view.current?.dispatch({ effects: setTargets.of(props.targets) });
    }, [props.targets]);

    useEffect(() => {
      view.current?.dispatch({ effects: setRawIds.of(props.rawIds) });
    }, [props.rawIds]);

    useEffect(() => {
      view.current?.dispatch({ effects: extra.current.reconfigure(props.extensions ?? []) });
    }, [props.extensions]);

    return <div ref={host} style={{ height: "100%" }} />;
  },
);
