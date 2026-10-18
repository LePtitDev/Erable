import { useMutation, useQuery } from "@tanstack/react-query";
import { useId, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { changeMyPassword } from "../../shared/api/auth";
import { errorMessage } from "../../shared/api/errors";
import { apiInfoQuery } from "../../shared/api/queries";
import { Button } from "../../shared/ui/Button";
import field from "../../shared/ui/Field.module.css";

/** Minimum length of a password, as the server checks it (AccountRules.MinPasswordLength). */
const minLength = 8;

/**
 * Changes the password of the signed-in user (audit S4). Only on a server: the desktop application has no account.
 * The current password is asked again, so that a session left open is not enough to take the account.
 */
export function PasswordSettings() {
  const { t } = useTranslation();
  const info = useQuery(apiInfoQuery);
  const change = useMutation({
    mutationFn: ({ current, next }: { current: string; next: string }) =>
      changeMyPassword(current, next),
  });
  const ids = { current: useId(), next: useId(), confirm: useId(), hint: useId() };
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");

  if (info.data?.mode !== "server") return null;

  const mismatch = confirm !== "" && confirm !== next;
  const ready = current !== "" && next.length >= minLength && confirm === next;

  const edit = (set: (value: string) => void) => (value: string) => {
    change.reset();
    set(value);
  };

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    if (!ready) return;
    change.mutate(
      { current, next },
      {
        onSuccess: () => {
          setCurrent("");
          setNext("");
          setConfirm("");
        },
      },
    );
  };

  return (
    <form className={field["field"]} onSubmit={submit} aria-label={t("preferences.password.title")}>
      <span className={field["label"]}>{t("preferences.password.title")}</span>
      <label htmlFor={ids.current} className={field["label"]}>
        {t("preferences.password.current")}
      </label>
      <input
        id={ids.current}
        type="password"
        autoComplete="current-password"
        className={field["input"]}
        value={current}
        onChange={(event) => {
          edit(setCurrent)(event.target.value);
        }}
      />
      <label htmlFor={ids.next} className={field["label"]}>
        {t("preferences.password.new")}
      </label>
      <input
        id={ids.next}
        type="password"
        autoComplete="new-password"
        minLength={minLength}
        className={field["input"]}
        aria-describedby={ids.hint}
        value={next}
        onChange={(event) => {
          edit(setNext)(event.target.value);
        }}
      />
      <label htmlFor={ids.confirm} className={field["label"]}>
        {t("preferences.password.confirm")}
      </label>
      <input
        id={ids.confirm}
        type="password"
        autoComplete="new-password"
        className={field["input"]}
        aria-invalid={mismatch}
        value={confirm}
        onChange={(event) => {
          edit(setConfirm)(event.target.value);
        }}
      />
      <p id={ids.hint} className={field["hint"]}>
        {t("preferences.password.hint", { min: minLength })}
      </p>
      {mismatch && (
        <p role="alert" className={field["error"]}>
          {t("preferences.password.mismatch")}
        </p>
      )}
      {change.isError && (
        <p role="alert" className={field["error"]}>
          {errorMessage(t, change.error)}
        </p>
      )}
      {change.isSuccess && (
        <p role="status" className={field["hint"]}>
          {t("preferences.password.saved")}
        </p>
      )}
      <div>
        <Button type="submit" disabled={!ready || change.isPending}>
          {t("preferences.password.save")}
        </Button>
      </div>
    </form>
  );
}
