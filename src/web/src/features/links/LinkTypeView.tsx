import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Pencil } from "lucide-react";
import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { queryKeys } from "../../shared/api/keys";
import {
  linkTypeQuery,
  updateLinkType,
  type LinkTypeView as LinkTypeData,
} from "../../shared/api/links";
import type { ElementDetails } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import categoryStyles from "../category/CategoryView.module.css";
import { ElementHeader } from "../project/ElementHeader";
import { useCanEdit } from "../project/access";
import styles from "./LinkTypeView.module.css";

interface Draft {
  readonly description: string;
  readonly inverseName: string;
}

/** The screen of a link type: description, inverse label and use (F09.R1). */
export function LinkTypeView({
  projectId,
  element,
  edit = false,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
  /** Opens in edit mode (a type just created from the tree). */
  readonly edit?: boolean;
}) {
  const { t } = useTranslation();
  const canEdit = useCanEdit();
  const view = useQuery(linkTypeQuery(projectId, element.id));
  const titleId = useId();
  const [draft, setDraft] = useState<Draft | null>(null);
  const [wantEdit, setWantEdit] = useState(edit);
  if (wantEdit && view.isSuccess && draft === null) {
    setWantEdit(false);
    setDraft(draftOf(view.data));
  }

  if (view.isError) throw view.error;
  if (view.isPending) return <p aria-busy="true">{t("common.loading")}</p>;
  const data = view.data;

  return (
    <article className={styles["view"]} aria-labelledby={titleId}>
      <ElementHeader
        projectId={projectId}
        element={data.element}
        kind
        titleId={titleId}
        actions={
          draft === null && canEdit ? (
            <Button
              variant="primary"
              onClick={() => {
                setDraft(draftOf(data));
              }}
            >
              <Pencil aria-hidden size={16} />
              {t("page.edit")}
            </Button>
          ) : undefined
        }
      />
      {draft === null ? (
        <LinkTypeReading data={data} />
      ) : (
        <LinkTypeEditing
          projectId={projectId}
          data={data}
          draft={draft}
          onChange={setDraft}
          onDone={() => {
            setDraft(null);
          }}
        />
      )}
    </article>
  );
}

function draftOf(data: LinkTypeData): Draft {
  return { description: data.description, inverseName: data.inverseName ?? "" };
}

function LinkTypeReading({ data }: { readonly data: LinkTypeData }) {
  const { t } = useTranslation();
  return (
    <div className={categoryStyles["cards"]}>
      <section className={categoryStyles["card"]} aria-labelledby="link-description">
        <h2 id="link-description" className={categoryStyles["cardTitle"]}>
          {t("links.description")}
        </h2>
        {data.description === "" ? (
          <p className={categoryStyles["muted"]}>{t("links.noDescription")}</p>
        ) : (
          <p className={categoryStyles["description"]}>{data.description}</p>
        )}
      </section>
      <section className={categoryStyles["card"]} aria-labelledby="link-inverse">
        <h2 id="link-inverse" className={categoryStyles["cardTitle"]}>
          {t("links.inverse")}
        </h2>
        {data.inverseName === null ? (
          <p className={categoryStyles["muted"]}>{t("links.noInverse")}</p>
        ) : (
          <p className={styles["inverse"]}>{data.inverseName}</p>
        )}
        <p className={categoryStyles["muted"]}>{t("links.usage", { count: data.linkCount })}</p>
      </section>
    </div>
  );
}

function LinkTypeEditing({
  projectId,
  data,
  draft,
  onChange,
  onDone,
}: {
  readonly projectId: ElementId;
  readonly data: LinkTypeData;
  readonly draft: Draft;
  readonly onChange: (draft: Draft) => void;
  readonly onDone: () => void;
}) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const descriptionId = useId();
  const inverseId = useId();
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await updateLinkType(
        projectId,
        data.element.id,
        draft.description,
        draft.inverseName.trim() === "" ? null : draft.inverseName,
      );
      await client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });
      onDone();
    } catch (failure) {
      setError(errorMessage(t, failure));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form
      className={styles["editing"]}
      aria-label={t("links.editing")}
      onSubmit={(event) => {
        event.preventDefault();
        void save();
      }}
    >
      <div className={categoryStyles["cards"]}>
        <section className={categoryStyles["card"]}>
          <label htmlFor={descriptionId} className={categoryStyles["cardTitle"]}>
            {t("links.description")}
          </label>
          <textarea
            id={descriptionId}
            className={categoryStyles["textarea"]}
            rows={4}
            value={draft.description}
            onChange={(event) => {
              onChange({ ...draft, description: event.target.value });
            }}
          />
        </section>
        <section className={categoryStyles["card"]}>
          <label htmlFor={inverseId} className={categoryStyles["cardTitle"]}>
            {t("links.inverse")}
          </label>
          <input
            id={inverseId}
            className={categoryStyles["select"]}
            value={draft.inverseName}
            onChange={(event) => {
              onChange({ ...draft, inverseName: event.target.value });
            }}
          />
          <p className={categoryStyles["muted"]}>{t("links.inverseHint")}</p>
        </section>
      </div>
      {error !== null && (
        <p role="alert" className={categoryStyles["error"]}>
          {error}
        </p>
      )}
      <div className={categoryStyles["formActions"]}>
        <Button onClick={onDone} disabled={saving}>
          {t("common.cancel")}
        </Button>
        <Button type="submit" variant="primary" disabled={saving}>
          {t("editor.save")}
        </Button>
      </div>
    </form>
  );
}
