import { useQuery } from "@tanstack/react-query";
import { useId, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { meQuery, useCreateProject, type ProjectSummary } from "../../shared/api/projects";
import { apiInfoQuery } from "../../shared/api/queries";
import { interfaceLanguages, nativeNames, pickLanguage } from "../../shared/i18n/languages";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import field from "../../shared/ui/Field.module.css";
import { folderNameOf, joinPath } from "./projectPath";

interface NewProjectDialogProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly onCreated: (project: ProjectSummary) => void;
  /** Creating in an existing folder that is not yet a project (F01.R1): the location is fixed. */
  readonly folder?: string;
  readonly defaultName?: string;
}

/**
 * New project: name, content language (fixed at creation, F01.R4) and location. The folder is created under the
 * location from the name; nothing is written before « Créer ».
 */
export function NewProjectDialog({
  open,
  onOpenChange,
  onCreated,
  folder,
  defaultName = "",
}: NewProjectDialogProps) {
  const { t, i18n } = useTranslation();
  const ids = { name: useId(), language: useId(), location: useId() };
  const me = useQuery(meQuery);
  // A server hosts the project itself: the user gives a name, not a place.
  const hosted = useQuery(apiInfoQuery).data?.folderProjects === false;
  const create = useCreateProject();
  const [name, setName] = useState(defaultName);
  const [language, setLanguage] = useState<string>(pickLanguage([i18n.language]));
  const [location, setLocation] = useState<string | null>(null);

  const parent = location ?? me.data?.defaultProjectsFolder ?? "";
  const target = folder ?? joinPath(parent, folderNameOf(name));
  const valid =
    name.trim() !== "" &&
    (hosted || folder !== undefined || (parent.trim() !== "" && folderNameOf(name) !== ""));

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    if (!valid) return;
    create.mutate(
      { folder: hosted ? null : target, name: name.trim(), contentLanguage: language },
      {
        onSuccess: (project) => {
          setName("");
          create.reset();
          onCreated(project);
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
      title={folder === undefined ? t("home.new.dialogTitle") : t("home.new.inFolderTitle")}
      {...(folder === undefined ? {} : { description: t("home.new.inFolderDescription") })}
    >
      <form className={field["form"]} onSubmit={submit} aria-label={t("home.new.dialogTitle")}>
        <div className={field["field"]}>
          <label htmlFor={ids.name} className={field["label"]}>
            {t("home.new.name")}
          </label>
          <input
            id={ids.name}
            className={field["input"]}
            value={name}
            maxLength={200}
            required
            autoFocus
            onChange={(e) => {
              setName(e.target.value);
            }}
          />
        </div>
        <div className={field["field"]}>
          <label htmlFor={ids.language} className={field["label"]}>
            {t("home.new.language")}
          </label>
          <select
            id={ids.language}
            className={field["input"]}
            value={language}
            onChange={(e) => {
              setLanguage(e.target.value);
            }}
          >
            {interfaceLanguages.map((code) => (
              <option key={code} value={code}>
                {nativeNames[code]}
              </option>
            ))}
          </select>
          <p className={field["hint"]}>{t("home.new.languageHint")}</p>
        </div>
        {folder === undefined && !hosted && (
          <div className={field["field"]}>
            <label htmlFor={ids.location} className={field["label"]}>
              {t("home.new.location")}
            </label>
            <input
              id={ids.location}
              className={`${field["input"] ?? ""} ${field["mono"] ?? ""}`}
              value={parent}
              onChange={(e) => {
                setLocation(e.target.value);
              }}
            />
          </div>
        )}
        <p className={field["hint"]}>
          {hosted ? t("home.new.serverHint") : t("home.new.folder", { folder: target })}
        </p>
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
          <Button type="submit" variant="primary" disabled={!valid || create.isPending}>
            {t("home.new.create")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
