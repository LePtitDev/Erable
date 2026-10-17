import { defaultKeymap, history, historyKeymap, indentWithTab } from "@codemirror/commands";
import {
  Compartment,
  EditorState,
  Prec,
  type Extension,
  type TransactionSpec,
} from "@codemirror/state";
import {
  EditorView,
  highlightActiveLine,
  highlightActiveLineGutter,
  type KeyBinding,
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
  /** Combination of « Enregistrer » in CodeMirror notation (the user's, F15.R4); empty for none. */
  readonly saveKey: string;
  /** Other bindings configurable by the user (opening the insert palette). */
  readonly bindings?: readonly KeyBinding[];
  /** Every update of the editor (the insert palette follows the cursor and the typed query). */
  readonly onUpdate?: (view: EditorView) => void;
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
    const bindingsSlot = useRef(new Compartment());
    callbacks.current = props;

    /** Save, then the bindings of other features; an empty combination means no shortcut. */
    function bindingsOf(current: MarkdownEditorProps): Extension {
      const save: KeyBinding[] =
        current.saveKey === ""
          ? []
          : [
              {
                key: current.saveKey,
                preventDefault: true,
                run: () => {
                  callbacks.current.onSave();
                  return true;
                },
              },
            ];
      return Prec.highest(keymap.of([...save, ...(current.bindings ?? [])]));
    }

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
            // tabindex: focusable anyway, but axe only counts it with the attribute (scrollable-region-focusable).
            EditorView.contentAttributes.of({
              "aria-label": callbacks.current.label,
              "aria-multiline": "true",
              tabindex: "0",
            }),
            // The user's own combinations (save, open the palette), reconfigured when they change them (F15.R4).
            bindingsSlot.current.of(bindingsOf(callbacks.current)),
            keymap.of([...defaultKeymap, ...historyKeymap, indentWithTab]),
            EditorView.updateListener.of((update) => {
              callbacks.current.onUpdate?.(update.view);
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

    useEffect(() => {
      view.current?.dispatch({ effects: bindingsSlot.current.reconfigure(bindingsOf(props)) });
    }, [props.saveKey, props.bindings]); // eslint-disable-line react-hooks/exhaustive-deps

    return <div ref={host} style={{ height: "100%" }} />;
  },
);
