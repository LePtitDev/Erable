import { useId, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import {
  inspectFolder,
  useOpenProject,
  type FolderInspection,
  type ProjectSummary,
} from "../../shared/api/projects";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import field from "../../shared/ui/Field.module.css";
import { NewProjectDialog } from "./NewProjectDialog";
import { baseName } from "./projectPath";

interface OpenFolderDialogProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly onOpened: (project: ProjectSummary) => void;
}

/**
 * « Ouvrir un dossier… » (Ctrl+O). The folder is inspected first, without writing anything: a project opens; a
 * folder without manifest offers to create a project, only after confirmation (F01.R1); a project of a newer format
 * is refused with a clear message (F01.R3). The native folder picker of the desktop arrives with M1-17.
 */
export function OpenFolderDialog({ open, onOpenChange, onOpened }: OpenFolderDialogProps) {
  const { t } = useTranslation();
  const id = useId();
  const openProject = useOpenProject();
  const [folder, setFolder] = useState("");
  const [inspection, setInspection] = useState<FolderInspection | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [createIn, setCreateIn] = useState<string | null>(null);

  const reset = () => {
    setInspection(null);
    setFailure(null);
    openProject.reset();
  };

  const submit = async (event: SyntheticEvent) => {
    event.preventDefault();
    reset();
    const path = folder.trim();
    if (path === "") return;
    try {
      const result = await inspectFolder(path);
      setInspection(result);
      if (result.status === "project") {
        openProject.mutate(path, { onSuccess: onOpened });
      } else if (result.status === "notAProject") {
        setCreateIn(path);
      }
    } catch (error) {
      setFailure(errorMessage(t, error));
    }
  };

  const message =
    failure ??
    (openProject.isError ? errorMessage(t, openProject.error) : null) ??
    (inspection === null || inspection.status === "project" || inspection.status === "notAProject"
      ? null
      : t(`home.open.status.${inspection.status}`, { version: inspection.formatVersion ?? "?" }));

  return (
    <>
      <Dialog
        open={open && createIn === null}
        onOpenChange={(next) => {
          if (!next) reset();
          onOpenChange(next);
        }}
        title={t("home.open.dialogTitle")}
        description={t("home.open.dialogDescription")}
      >
        <form className={field["form"]} onSubmit={(e) => void submit(e)}>
          <div className={field["field"]}>
            <label htmlFor={id} className={field["label"]}>
              {t("home.open.folder")}
            </label>
            <input
              id={id}
              className={`${field["input"] ?? ""} ${field["mono"] ?? ""}`}
              value={folder}
              autoFocus
              onChange={(e) => {
                setFolder(e.target.value);
                reset();
              }}
            />
          </div>
          {message !== null && (
            <p role="alert" className={field["error"]}>
              {message}
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
            <Button
              type="submit"
              variant="primary"
              disabled={folder.trim() === "" || openProject.isPending}
            >
              {t("home.open.submit")}
            </Button>
          </div>
        </form>
      </Dialog>
      {createIn !== null && (
        <NewProjectDialog
          key={createIn}
          open
          folder={createIn}
          defaultName={baseName(createIn)}
          onOpenChange={(next) => {
            if (!next) {
              setCreateIn(null);
              reset();
            }
          }}
          onCreated={(project) => {
            setCreateIn(null);
            onOpened(project);
          }}
        />
      )}
    </>
  );
}
