import { useQuery } from "@tanstack/react-query";
import { useId, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { meQuery, useRenameMe } from "../../shared/api/projects";
import { Button } from "../../shared/ui/Button";
import field from "../../shared/ui/Field.module.css";
import styles from "./DisplayNameSettings.module.css";

/** The name the user is shown under: greeting of the home screen, authors, presence (F15). */
export function DisplayNameSettings() {
  const { t } = useTranslation();
  const me = useQuery(meQuery);
  const rename = useRenameMe();
  const ids = { input: useId(), hint: useId() };
  // null: the user has not typed yet, the field shows the current name.
  const [draft, setDraft] = useState<string | null>(null);
  const current = me.data?.name ?? "";
  const value = draft ?? current;
  const unchanged = value.trim() === current;

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    if (unchanged) return;
    rename.mutate(value, {
      onSuccess: () => {
        setDraft(null);
      },
    });
  };

  return (
    <form className={field["field"]} onSubmit={submit}>
      <label htmlFor={ids.input} className={field["label"]}>
        {t("preferences.displayName.label")}
      </label>
      <div className={styles["row"]}>
        <input
          id={ids.input}
          className={field["input"]}
          value={value}
          maxLength={80}
          required
          disabled={!me.isSuccess}
          aria-describedby={ids.hint}
          aria-invalid={rename.isError}
          onChange={(event) => {
            rename.reset();
            setDraft(event.target.value);
          }}
        />
        <Button type="submit" disabled={unchanged || rename.isPending}>
          {t("preferences.displayName.save")}
        </Button>
      </div>
      <p id={ids.hint} className={field["hint"]}>
        {t("preferences.displayName.hint")}
      </p>
      {rename.isError && (
        <p role="alert" className={field["error"]}>
          {errorMessage(t, rename.error)}
        </p>
      )}
      {rename.isSuccess && (
        <p role="status" className={field["hint"]}>
          {t("preferences.displayName.saved")}
        </p>
      )}
    </form>
  );
}
