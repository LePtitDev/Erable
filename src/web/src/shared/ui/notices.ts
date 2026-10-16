import { create } from "zustand";

export interface Notice {
  readonly id: number;
  readonly text: string;
}

interface NoticeStore {
  readonly notices: readonly Notice[];
  push: (text: string) => void;
  dismiss: (id: number) => void;
}

let next = 1;

/**
 * Short messages shown over the project (« Fusionné avec les modifications de Camille R. »): they survive the screen
 * that raised them, e.g. an editor that closes right after the save.
 */
export const useNoticeStore = create<NoticeStore>()((set) => ({
  notices: [],
  push: (text) => {
    set((state) => ({ notices: [...state.notices, { id: next++, text }] }));
  },
  dismiss: (id) => {
    set((state) => ({ notices: state.notices.filter((n) => n.id !== id) }));
  },
}));
