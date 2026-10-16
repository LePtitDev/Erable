-- Schema v4 (M3-01): the full-text index search_fts (declared empty since v1) is kept up to date by the database itself
-- (docs/architecture.md §7.4, F10). Same conventions as 0001_initial.sql.
--
-- search_source is the single definition of what is searchable: every live element but the roots, with its name and its
-- text (plain text of a page, description of a category, a property or a link type; empty for folders and attachments).
-- The triggers keep search_fts equal to it inside the transaction of every write, whichever code path performs it
-- (creation, rename, save, trash, restore); erable doctor compares the two and rebuilds the index with --fix.
CREATE VIEW search_source AS
SELECT e.id AS id,
       e.name AS name,
       COALESCE(c.plain_text, k.description, p.description, l.description, '') AS body
FROM elements e
    LEFT JOIN page_contents c ON c.page_id = e.id
    LEFT JOIN categories k ON k.id = e.id
    LEFT JOIN property_defs p ON p.id = e.id
    LEFT JOIN link_types l ON l.id = e.id
WHERE e.deleted_at IS NULL AND e.kind <> 'root';

CREATE TRIGGER search_elements_insert AFTER INSERT ON elements
BEGIN
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.id;
END;

CREATE TRIGGER search_elements_update AFTER UPDATE OF name, kind, deleted_at ON elements
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.id;
END;

CREATE TRIGGER search_page_contents_insert AFTER INSERT ON page_contents
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.page_id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.page_id;
END;

CREATE TRIGGER search_page_contents_update AFTER UPDATE OF plain_text ON page_contents
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.page_id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.page_id;
END;

CREATE TRIGGER search_page_contents_delete AFTER DELETE ON page_contents
BEGIN
    DELETE FROM search_fts WHERE rowid = OLD.page_id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = OLD.page_id;
END;

CREATE TRIGGER search_categories_insert AFTER INSERT ON categories
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.id;
END;

CREATE TRIGGER search_categories_update AFTER UPDATE OF description ON categories
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.id;
END;

CREATE TRIGGER search_property_defs_insert AFTER INSERT ON property_defs
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.id;
END;

CREATE TRIGGER search_property_defs_update AFTER UPDATE OF description ON property_defs
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.id;
END;

CREATE TRIGGER search_link_types_insert AFTER INSERT ON link_types
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.id;
END;

CREATE TRIGGER search_link_types_update AFTER UPDATE OF description ON link_types
BEGIN
    DELETE FROM search_fts WHERE rowid = NEW.id;
    INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source WHERE id = NEW.id;
END;

-- Projects of format v3 or earlier have an empty index: fill it.
DELETE FROM search_fts;
INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source;
