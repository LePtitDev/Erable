export interface Layout {
  readonly explorerOpen: boolean;
  readonly explorerWidth: number;
  readonly sidePanelOpen: boolean;
}

export const defaultLayout: Layout = {
  explorerOpen: true,
  explorerWidth: 280,
  sidePanelOpen: true,
};
export const explorerBounds = { min: 200, max: 520 } as const;

export const explorerPanelId = "explorer";
