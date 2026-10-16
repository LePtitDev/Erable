import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { Search } from "lucide-react";
import { Dialog } from "radix-ui";
import { useEffect, useId, useState, type KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { recentQuery } from "../../shared/api/recent";
import { quickSearchQuery } from "../../shared/api/search";
import type { ElementKind } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { KindBadge } from "../../shared/ui/KindBadge";
import { Highlighted } from "./Highlighted";
import styles from "./QuickSearch.module.css";
import { emptySearch, toSearchParams } from "./searchState";

/** How long the typing rests before the names are searched. */
const typingDelayMs = 120;

interface Option {
  readonly id: ElementId;
  readonly kind: ElementKind;
  readonly name: string;
  readonly title: readonly { readonly text: string; readonly hit: boolean }[];
  readonly path: string;
}

/**
 * Quick search (F10.R1, Ctrl+K): a palette that finds an element of any type by its name and opens it. Empty, it offers
 * the elements viewed last. A combobox: the focus stays in the field, the active option is announced through
 * `aria-activedescendant`, the number of results through a live region.
 */
export function QuickSearch({
  projectId,
  open,
  onOpenChange,
}: {
  readonly projectId: ElementId;
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
}) {
  const { t } = useTranslation();
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className={styles["overlay"]} />
        <Dialog.Content className={styles["content"]} aria-describedby={undefined}>
          <Dialog.Title className={styles["hidden"]}>{t("search.quick.title")}</Dialog.Title>
          {/* Mounted only while open: every opening starts with an empty field. */}
          <Palette
            projectId={projectId}
            onClose={() => {
              onOpenChange(false);
            }}
          />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

function Palette({
  projectId,
  onClose,
}: {
  readonly projectId: ElementId;
  readonly onClose: () => void;
}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [text, setText] = useState("");
  const [typed, setTyped] = useState("");
  const [active, setActive] = useState(0);
  const listId = useId();

  useEffect(() => {
    const timer = setTimeout(() => {
      setTyped(text);
    }, typingDelayMs);
    return () => {
      clearTimeout(timer);
    };
  }, [text]);

  const searching = typed.trim() !== "";
  const found = useQuery({ ...quickSearchQuery(projectId, typed), enabled: searching });
  const recent = useQuery({ ...recentQuery(projectId, 8), enabled: !searching });

  const options: readonly Option[] = searching
    ? (found.data?.hits ?? []).map((hit) => ({
        id: hit.id,
        kind: hit.kind,
        name: hit.name,
        title: hit.title,
        path: hit.path
          .slice(1)
          .map((step) => step.name)
          .join(" / "),
      }))
    : (recent.data?.viewed ?? []).map((v) => ({
        id: v.id,
        kind: v.kind,
        name: v.name,
        title: [{ text: v.name, hit: false }],
        path: "",
      }));
  const pending = searching ? text !== typed || found.isPending : recent.isPending;
  const current = options[Math.min(active, options.length - 1)];

  const close = onClose;
  const openElement = (option: Option) => {
    close();
    void navigate({
      to: "/p/$projectId/e/$elementId",
      params: { projectId, elementId: option.id },
    });
  };
  const openAdvanced = () => {
    close();
    void navigate({
      to: "/p/$projectId/search",
      params: { projectId },
      search: toSearchParams({ ...emptySearch, q: text.trim() }),
    });
  };

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      const step = event.key === "ArrowDown" ? 1 : -1;
      if (options.length > 0) setActive((a) => (a + step + options.length) % options.length);
    } else if (event.key === "Enter") {
      event.preventDefault();
      if (event.ctrlKey || event.metaKey || event.altKey) openAdvanced();
      else if (current !== undefined) openElement(current);
      else if (searching) openAdvanced();
    }
  };

  const optionId = (id: ElementId) => `${listId}-${id}`;
  const status = pending
    ? t("search.loading")
    : searching
      ? t("search.results", { count: options.length })
      : "";

  return (
    <>
      <label className={styles["field"]}>
        <Search aria-hidden size={16} />
        <input
          role="combobox"
          aria-label={t("search.quick.field")}
          aria-expanded
          aria-controls={listId}
          aria-autocomplete="list"
          {...(current === undefined ? {} : { "aria-activedescendant": optionId(current.id) })}
          className={styles["input"]}
          value={text}
          autoFocus
          autoComplete="off"
          onChange={(event) => {
            setText(event.target.value);
            setActive(0);
          }}
          onKeyDown={onKeyDown}
        />
        <kbd className={styles["kbd"]}>{t("search.quick.escape")}</kbd>
      </label>
      <div role="status" className={styles["hidden"]}>
        {status}
      </div>
      {!searching && options.length > 0 && (
        <p className={styles["heading"]}>{t("explorer.recent")}</p>
      )}
      <div
        id={listId}
        role="listbox"
        aria-label={t("search.quick.results")}
        className={styles["list"]}
      >
        {options.map((option, index) => (
          <div
            key={option.id}
            id={optionId(option.id)}
            role="option"
            aria-selected={option === current}
            className={styles["option"]}
            data-active={option === current}
            onMouseMove={() => {
              setActive(index);
            }}
            onClick={() => {
              openElement(option);
            }}
          >
            <KindBadge kind={option.kind} />
            <span className={styles["name"]}>
              <Highlighted segments={option.title} />
            </span>
            <span className={styles["path"]}>{option.path}</span>
          </div>
        ))}
        {!pending && options.length === 0 && (
          <p className={styles["empty"]}>
            {searching ? t("search.none.title") : t("recent.emptyViewed")}
          </p>
        )}
      </div>
      <button type="button" className={styles["advanced"]} onClick={openAdvanced}>
        {t("search.quick.advanced")}
      </button>
    </>
  );
}
