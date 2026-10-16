import { useEffect, useId, useRef, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import {
  inspectFolder,
  type FolderInspection,
  type ProjectSummary,
} from "../../shared/api/projects";
import { pickFolder, useIsDesktop } from "../../shared/native/native";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import field from "../../shared/ui/Field.module.css";
import { NewProjectDialog } from "./NewProjectDialog";
import { baseName } from "./projectPath";
import { useMigratingOpen } from "./useMigratingOpen";

interface OpenFolderDialogProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly onOpened: (project: ProjectSummary) => void;
  /** A folder to open at once instead of asking (dropped on the window, E2E-16-07). */
  readonly initialFolder?: string | null;
}

/**
 * « Ouvrir un dossier… » (Ctrl+O). The folder is inspected first, without writing anything: a project opens; a
 * folder without manifest offers to create a project, only after confirmation (F01.R1); a project of a newer format
 * is refused with a clear message (F01.R3). On the desktop, the OS folder picker opens at once (E2E-16-03); the
 * path field stays for typing or pasting a path.
 */
export function OpenFolderDialog({
  open,
  onOpenChange,
  onOpened,
  initialFolder = null,
}: OpenFolderDialogProps) {
  const { t } = useTranslation();
  const id = useId();
  const opener = useMigratingOpen();
  // Keyed by the dropped folder in HomeScreen: a new drop is a new dialog.
  const [folder, setFolder] = useState(initialFolder ?? "");
  const [inspection, setInspection] = useState<FolderInspection | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [createIn, setCreateIn] = useState<string | null>(null);

  const reset = () => {
    setInspection(null);
    setFailure(null);
    opener.reset();
  };

  const inspectAndOpen = async (path: string) => {
    reset();
    if (path === "") return;
    try {
      const result = await inspectFolder(path);
      setInspection(result);
      if (result.status === "project") {
        // An older project asks for its migration first (F01.R3).
        opener.open(path, (project) => {
          onOpened(project);
        });
      } else if (result.status === "notAProject") {
        setCreateIn(path);
      }
    } catch (error) {
      setFailure(errorMessage(t, error));
    }
  };

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    void inspectAndOpen(folder.trim());
  };

  const desktop = useIsDesktop();
  const browse = async (closeIfCancelled: boolean) => {
    try {
      const path = await pickFolder(t("home.open.pickTitle"));
      if (path === null) {
        if (closeIfCancelled) onOpenChange(false);
        return;
      }
      setFolder(path);
      await inspectAndOpen(path);
    } catch (error) {
      setFailure(errorMessage(t, error));
    }
  };
  // Opening the dialog on the desktop shows the OS picker straight away, once per opening.
  const browsed = useRef(false);
  useEffect(() => {
    if (!open) {
      browsed.current = false;
      return;
    }
    if (browsed.current || (initialFolder === null && !desktop)) return;
    browsed.current = true;
    // After this render: the inspection (of the dropped folder, or of the one picked) updates the dialog state.
    queueMicrotask(() => {
      void (initialFolder === null ? browse(true) : inspectAndOpen(initialFolder));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- once per opening, not on every render of `browse`
  }, [open, desktop, initialFolder]);

  const message =
    failure ??
    (opener.error === null ? null : errorMessage(t, opener.error)) ??
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
        <form className={field["form"]} onSubmit={submit}>
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
            {desktop && <Button onClick={() => void browse(false)}>{t("home.open.browse")}</Button>}
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
              disabled={folder.trim() === "" || opener.pending}
            >
              {t("home.open.submit")}
            </Button>
          </div>
        </form>
      </Dialog>
      {opener.dialog}
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
