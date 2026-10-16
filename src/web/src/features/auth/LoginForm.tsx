import { useQueryClient } from "@tanstack/react-query";
import { useId, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { login } from "../../shared/api/auth";
import { errorMessage } from "../../shared/api/errors";
import { Button } from "../../shared/ui/Button";
import field from "../../shared/ui/Field.module.css";

/**
 * Login and password (E2E-14-01, E2E-14-02). A wrong login and a wrong password get the same message: the screen
 * never tells which one failed.
 */
export function LoginForm({ onSignedIn }: { readonly onSignedIn: () => void }) {
  const { t } = useTranslation();
  const client = useQueryClient();
  const ids = { login: useId(), password: useId() };
  const [loginName, setLoginName] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    setPending(true);
    setError(null);
    login(loginName, password)
      .then(async () => {
        // Whatever was cached belongs to the previous identity (or to nobody).
        await client.resetQueries();
        setPassword("");
        onSignedIn();
      })
      .catch((failure: unknown) => {
        setError(errorMessage(t, failure));
      })
      .finally(() => {
        setPending(false);
      });
  };

  return (
    <form className={field["form"]} onSubmit={submit} aria-label={t("auth.title")}>
      <div className={field["field"]}>
        <label htmlFor={ids.login} className={field["label"]}>
          {t("auth.login")}
        </label>
        <input
          id={ids.login}
          className={field["input"]}
          value={loginName}
          autoComplete="username"
          autoCapitalize="none"
          spellCheck={false}
          required
          autoFocus
          onChange={(e) => {
            setLoginName(e.target.value);
          }}
        />
      </div>
      <div className={field["field"]}>
        <label htmlFor={ids.password} className={field["label"]}>
          {t("auth.password")}
        </label>
        <input
          id={ids.password}
          className={field["input"]}
          type="password"
          value={password}
          autoComplete="current-password"
          required
          onChange={(e) => {
            setPassword(e.target.value);
          }}
        />
      </div>
      {error !== null && (
        <p role="alert" className={field["error"]}>
          {error}
        </p>
      )}
      <div className={field["actions"]}>
        <Button type="submit" variant="primary" disabled={pending || loginName.trim() === ""}>
          {t("auth.submit")}
        </Button>
      </div>
    </form>
  );
}
