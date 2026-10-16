import { useState, type ReactNode } from "react";
import {
  migrationRequired,
  useOpenProject,
  type MigrationNeed,
  type ProjectSummary,
} from "../../shared/api/projects";
import { MigrationDialog } from "./MigrationDialog";

interface Asked {
  readonly folder: string;
  readonly need: MigrationNeed;
  readonly onOpened: (project: ProjectSummary) => void;
}

/**
 * Opens a project, asking first when its format is older (F01.R3, M2-09): the backend answers « migration required »
 * and nothing is written until the user confirms in the dialog this hook provides.
 */
export function useMigratingOpen() {
  const mutation = useOpenProject();
  const [asked, setAsked] = useState<Asked | null>(null);

  const run = (
    folder: string,
    onOpened: (project: ProjectSummary) => void,
    confirmMigration: boolean,
  ) => {
    mutation.mutate(
      { folder, confirmMigration },
      {
        onSuccess: (project) => {
          setAsked(null);
          onOpened(project);
        },
        onError: (error) => {
          const need = migrationRequired(error);
          setAsked(need === null ? null : { folder, need, onOpened });
        },
      },
    );
  };

  const dialog: ReactNode = (
    <MigrationDialog
      need={asked?.need ?? null}
      busy={mutation.isPending}
      onConfirm={() => {
        if (asked !== null) run(asked.folder, asked.onOpened, true);
      }}
      onCancel={() => {
        setAsked(null);
        mutation.reset();
      }}
    />
  );

  return {
    open: (folder: string, onOpened: (project: ProjectSummary) => void) => {
      run(folder, onOpened, false);
    },
    pending: mutation.isPending && asked === null,
    /** A failure to show; the request for a confirmation is not one. */
    error: mutation.isError && migrationRequired(mutation.error) === null ? mutation.error : null,
    reset: () => {
      setAsked(null);
      mutation.reset();
    },
    dialog,
  };
}
