-- Schema v1 (docs/architecture.md §7.4). Applied in one transaction; PRAGMA user_version is set by the runner.
--
-- Conventions:
--   * IDs are ElementId values reinterpreted as signed 64-bit INTEGER (lossless, docs/architecture.md §4).
--   * Dates are UTC ISO 8601 TEXT with milliseconds: 2026-10-13T08:00:00.000Z (sortable, readable).
--   * Kinds are the camelCase names used in JSON: root, folder, page, category, propertyDefinition, linkType, attachment.

CREATE TABLE elements (
    id          INTEGER PRIMARY KEY,
    kind        TEXT    NOT NULL,
    parent_id   INTEGER REFERENCES elements (id),
    name        TEXT    NOT NULL,
    sort_order  INTEGER NOT NULL,
    revision    INTEGER NOT NULL DEFAULT 1,
    created_at  TEXT    NOT NULL,
    created_by  TEXT    NOT NULL,
    updated_at  TEXT    NOT NULL,
    updated_by  TEXT    NOT NULL,
    deleted_at  TEXT,
    CHECK ((kind = 'root') = (parent_id IS NULL))
) STRICT;

CREATE INDEX ix_elements_parent ON elements (parent_id, sort_order);
CREATE INDEX ix_elements_updated ON elements (updated_at);

-- Markdown source of pages; plain_text feeds full-text search (M3).
CREATE TABLE page_contents (
    page_id     INTEGER PRIMARY KEY REFERENCES elements (id),
    markdown    TEXT    NOT NULL,
    plain_text  TEXT    NOT NULL
) STRICT;

-- One row per saved change: compressed JSON snapshot of the element (F13.R1). blob_sha256 keeps the blob of an
-- attachment revision alive for the garbage collector.
CREATE TABLE revisions (
    element_id  INTEGER NOT NULL REFERENCES elements (id),
    revision    INTEGER NOT NULL,
    author      TEXT    NOT NULL,
    at          TEXT    NOT NULL,
    snapshot    BLOB    NOT NULL,
    blob_sha256 TEXT,
    PRIMARY KEY (element_id, revision)
) STRICT;

CREATE INDEX ix_revisions_at ON revisions (at);
CREATE INDEX ix_revisions_blob ON revisions (blob_sha256) WHERE blob_sha256 IS NOT NULL;

-- Derived index of the [[ID]] / ![[ID]] references of each page (docs/architecture.md §5.6). target_id has no
-- foreign key: a reference to a deleted or unknown element is legitimate (broken link).
CREATE TABLE refs (
    source_id   INTEGER NOT NULL REFERENCES elements (id),
    target_id   INTEGER NOT NULL,
    anchor      TEXT,
    is_embed    INTEGER NOT NULL CHECK (is_embed IN (0, 1)),
    position    INTEGER NOT NULL
) STRICT;

CREATE INDEX ix_refs_source ON refs (source_id);
CREATE INDEX ix_refs_target ON refs (target_id);

-- Attachment metadata; the owning page is the element's parent. The content is the blob named by sha256.
CREATE TABLE attachments (
    id          INTEGER PRIMARY KEY REFERENCES elements (id),
    file_name   TEXT    NOT NULL,
    mime        TEXT    NOT NULL,
    size        INTEGER NOT NULL,
    sha256      TEXT    NOT NULL
) STRICT;

CREATE INDEX ix_attachments_sha256 ON attachments (sha256);

-- "Recently viewed" per user (F11, filled in M3).
CREATE TABLE user_activity (
    user_id         TEXT    NOT NULL,
    element_id      INTEGER NOT NULL REFERENCES elements (id),
    last_viewed_at  TEXT    NOT NULL,
    PRIMARY KEY (user_id, element_id)
) STRICT;

-- Full-text index (filled in M3). rowid = element id.
CREATE VIRTUAL TABLE search_fts USING fts5 (name, plain_text, tokenize = 'unicode61 remove_diacritics 2');
