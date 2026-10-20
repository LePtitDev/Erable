import { useQuery } from "@tanstack/react-query";
import { useParams } from "@tanstack/react-router";
import { UserPlus } from "lucide-react";
import { useId, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import {
  membersQuery,
  useCreateAccount,
  useRemoveMember,
  useSetMember,
  type MemberItem,
  type ProjectRole,
} from "../../shared/api/members";
import { meQuery } from "../../shared/api/projects";
import { initialsOf } from "../../shared/api/projects";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import field from "../../shared/ui/Field.module.css";
import { AccessDeniedScreen } from "../project/ErrorScreens";
import { useProjectAccess } from "../project/access";
import styles from "./MembersView.module.css";

const roles: readonly ProjectRole[] = ["reader", "editor", "admin"];

/** « Membres » (E2E-14-10): who may do what in the project. For its administrators only. */
export function MembersView() {
  const { projectId } = useParams({ from: "/p/$projectId/members" });
  const access = useProjectAccess();
  if (projectId === null || !access.isAdmin) return <AccessDeniedScreen />;
  return <Members projectId={projectId} />;
}

export function Members({ projectId }: { readonly projectId: ElementId }) {
  const { t } = useTranslation();
  const me = useQuery(meQuery);
  const members = useQuery(membersQuery(projectId));
  const setMember = useSetMember(projectId);
  const removeMember = useRemoveMember(projectId);
  const [creating, setCreating] = useState(false);
  const ids = { login: useId(), role: useId() };
  const [login, setLogin] = useState("");
  const [role, setRole] = useState<ProjectRole>("reader");
  const failure = setMember.error ?? removeMember.error;

  const add = (event: SyntheticEvent) => {
    event.preventDefault();
    setMember.mutate(
      { userId: login.trim(), role },
      {
        onSuccess: () => {
          setLogin("");
        },
      },
    );
  };

  return (
    <section className={styles["view"]} aria-labelledby="members-title">
      <header className={styles["header"]}>
        <h1 id="members-title" className={styles["title"]}>
          {t("members.title")}
        </h1>
        <p className={styles["muted"]}>{t("members.intro")}</p>
      </header>

      {failure !== null && (
        <p role="alert" className={field["error"]}>
          {errorMessage(t, failure)}
        </p>
      )}

      {members.isPending && <p aria-busy="true">{t("common.loading")}</p>}
      {members.isError && (
        <p role="alert" className={field["error"]}>
          {errorMessage(t, members.error)}
        </p>
      )}
      {members.isSuccess && (
        <table className={styles["table"]}>
          <caption className={styles["caption"]}>
            {t("members.count", { count: members.data.length })}
          </caption>
          <thead>
            <tr>
              <th scope="col">{t("members.person")}</th>
              <th scope="col">{t("members.role")}</th>
              <th scope="col">
                <span className={styles["srOnly"]}>{t("members.actions")}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {members.data.map((member) => (
              <MemberRow
                key={member.userId}
                member={member}
                itself={member.userId === me.data?.id || member.isMe}
                busy={setMember.isPending || removeMember.isPending}
                onRole={(next) => {
                  setMember.mutate({ userId: member.userId, role: next });
                }}
                onRemove={() => {
                  removeMember.mutate(member.userId);
                }}
              />
            ))}
          </tbody>
        </table>
      )}

      <form className={styles["add"]} onSubmit={add} aria-label={t("members.add.title")}>
        <h2 className={styles["subtitle"]}>{t("members.add.title")}</h2>
        <div className={styles["addFields"]}>
          <div className={field["field"]}>
            <label htmlFor={ids.login} className={field["label"]}>
              {t("members.add.login")}
            </label>
            <input
              id={ids.login}
              className={field["input"]}
              value={login}
              autoCapitalize="none"
              spellCheck={false}
              onChange={(e) => {
                setLogin(e.target.value);
              }}
            />
          </div>
          <div className={field["field"]}>
            <label htmlFor={ids.role} className={field["label"]}>
              {t("members.role")}
            </label>
            <select
              id={ids.role}
              className={field["input"]}
              value={role}
              onChange={(e) => {
                setRole(e.target.value as ProjectRole);
              }}
            >
              {roles.map((r) => (
                <option key={r} value={r}>
                  {t(`roles.${r}`)}
                </option>
              ))}
            </select>
          </div>
          <Button
            type="submit"
            variant="primary"
            disabled={login.trim() === "" || setMember.isPending}
          >
            <UserPlus aria-hidden size={16} />
            {t("members.add.submit")}
          </Button>
          {me.data?.isServerAdmin === true && (
            <Button
              onClick={() => {
                setCreating(true);
              }}
            >
              {t("members.create.open")}
            </Button>
          )}
        </div>
      </form>

      <CreateAccountDialog
        open={creating}
        onOpenChange={setCreating}
        role={role}
        onCreated={(userId) => {
          setMember.mutate({ userId, role });
          setCreating(false);
        }}
      />
    </section>
  );
}

function MemberRow({
  member,
  itself,
  busy,
  onRole,
  onRemove,
}: {
  readonly member: MemberItem;
  readonly itself: boolean;
  readonly busy: boolean;
  readonly onRole: (role: ProjectRole) => void;
  readonly onRemove: () => void;
}) {
  const { t } = useTranslation();
  return (
    <tr>
      <th scope="row" className={styles["person"]}>
        <span className={styles["avatar"]} aria-hidden>
          {initialsOf(member.name)}
        </span>
        <span>
          <span className={styles["name"]}>{member.name}</span>
          {itself && <span className={styles["you"]}> {t("members.you")}</span>}
          <span className={styles["login"]}>{member.userId}</span>
          {member.disabled && <span className={styles["login"]}> · {t("members.disabled")}</span>}
        </span>
      </th>
      <td>
        <select
          className={field["input"]}
          aria-label={t("members.roleOf", { name: member.name })}
          value={member.role}
          disabled={busy}
          onChange={(e) => {
            onRole(e.target.value as ProjectRole);
          }}
        >
          {roles.map((r) => (
            <option key={r} value={r}>
              {t(`roles.${r}`)}
            </option>
          ))}
        </select>
      </td>
      <td>
        <Button
          size="sm"
          variant="danger"
          disabled={busy}
          aria-label={t("members.removeNamed", { name: member.name })}
          onClick={onRemove}
        >
          {t("members.remove")}
        </Button>
      </td>
    </tr>
  );
}

/** A server administrator creates the account of a colleague, who is then added with the chosen role. */
function CreateAccountDialog({
  open,
  onOpenChange,
  role,
  onCreated,
}: {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly role: ProjectRole;
  readonly onCreated: (login: string) => void;
}) {
  const { t } = useTranslation();
  const create = useCreateAccount();
  const ids = { login: useId(), name: useId(), password: useId() };
  const [login, setLogin] = useState("");
  const [name, setName] = useState("");
  const [password, setPassword] = useState("");

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    create.mutate(
      { login, name, password },
      {
        onSuccess: (account) => {
          setLogin("");
          setName("");
          setPassword("");
          onCreated(account.id);
        },
      },
    );
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        if (!next) create.reset();
        onOpenChange(next);
      }}
      title={t("members.create.title")}
      description={t("members.create.description", { role: t(`roles.${role}`) })}
      size="sm"
    >
      <form className={field["form"]} onSubmit={submit} aria-label={t("members.create.title")}>
        <div className={field["field"]}>
          <label htmlFor={ids.login} className={field["label"]}>
            {t("auth.login")}
          </label>
          <input
            id={ids.login}
            className={field["input"]}
            value={login}
            autoCapitalize="none"
            spellCheck={false}
            required
            autoFocus
            onChange={(e) => {
              setLogin(e.target.value);
            }}
          />
        </div>
        <div className={field["field"]}>
          <label htmlFor={ids.name} className={field["label"]}>
            {t("members.create.name")}
          </label>
          <input
            id={ids.name}
            className={field["input"]}
            value={name}
            required
            onChange={(e) => {
              setName(e.target.value);
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
            autoComplete="new-password"
            required
            onChange={(e) => {
              setPassword(e.target.value);
            }}
          />
          <p className={field["hint"]}>{t("members.create.passwordHint")}</p>
        </div>
        {create.isError && (
          <p role="alert" className={field["error"]}>
            {errorMessage(t, create.error)}
          </p>
        )}
        <div className={field["actions"]}>
          <Button
            onClick={() => {
              onOpenChange(false);
            }}
          >
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={create.isPending}>
            {t("members.create.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
