-- Schema v3 (M2-08): the trash (docs/architecture.md §7.4, F12). Same conventions as 0001_initial.sql.
--
-- deleted_root  the element at the top of the deletion the element belongs to (its own id for the element the user
--               deleted; its descendants carry the same value). One deletion = one entry of the trash, restored or
--               emptied as a whole; an element deleted earlier on its own keeps its own entry.
-- deleted_by    who deleted it.
-- deleted_orphan  1 when the parent of the entry left the trash for good: the entry now hangs under the root of its
--               kind and restoring it says so (F12.R3, E2E-12-05).
ALTER TABLE elements ADD COLUMN deleted_root INTEGER REFERENCES elements (id);
ALTER TABLE elements ADD COLUMN deleted_by TEXT;
ALTER TABLE elements ADD COLUMN deleted_orphan INTEGER NOT NULL DEFAULT 0;

-- Format v2 had no way to delete, but be safe with whatever is there: each deleted element is its own entry.
UPDATE elements SET deleted_root = id, deleted_by = updated_by WHERE deleted_at IS NOT NULL;

CREATE INDEX ix_elements_deleted_root ON elements (deleted_root) WHERE deleted_root IS NOT NULL;
