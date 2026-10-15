-- Schema v2 (M2-03): categories, property definitions and values, derived values and validation issues, link types and
-- links (docs/architecture.md §5.3–§5.5, §7.4). Same conventions as 0001_initial.sql.

-- Inheritance of a category: one optional parent category (F07.R1). parent_category_id has no foreign key: a parent in
-- the trash (or emptied from it) leaves the chain broken but restorable, never invalid.
CREATE TABLE categories (
    id                  INTEGER PRIMARY KEY REFERENCES elements (id),
    parent_category_id  INTEGER,
    description         TEXT    NOT NULL DEFAULT ''
) STRICT;

CREATE INDEX ix_categories_parent ON categories (parent_category_id);

-- Categories of a page, in their order (F07.R3). category_id has no foreign key, like refs.target_id.
CREATE TABLE page_categories (
    page_id     INTEGER NOT NULL REFERENCES elements (id),
    category_id INTEGER NOT NULL,
    sort_order  INTEGER NOT NULL,
    PRIMARY KEY (page_id, category_id)
) STRICT;

CREATE INDEX ix_page_categories_category ON page_categories (category_id);

-- Property definitions (§5.4). The display name is the element name; key is unique and renamable.
CREATE TABLE property_defs (
    id            INTEGER PRIMARY KEY REFERENCES elements (id),
    key           TEXT    NOT NULL UNIQUE,
    description   TEXT    NOT NULL DEFAULT '',
    type          TEXT    NOT NULL,
    default_json  TEXT,
    rules_json    TEXT    NOT NULL DEFAULT '[]',
    enum_json     TEXT    NOT NULL DEFAULT '[]'
) STRICT;

-- Values defined on a page or a category (the default of a category). Typed columns allow SQL filters (M3). A scalar
-- is one row with idx 0; a list has one row per item (idx 0..n-1), an empty list one row with idx -1 and no value.
-- bool and int go to v_int, float to v_real, string, text, enum (key) and date (yyyy-MM-dd) to v_text, ref to v_ref.
CREATE TABLE property_values (
    owner_id     INTEGER NOT NULL REFERENCES elements (id),
    property_id  INTEGER NOT NULL,
    idx          INTEGER NOT NULL,
    v_int        INTEGER,
    v_real       REAL,
    v_text       TEXT,
    v_ref        INTEGER,
    PRIMARY KEY (owner_id, property_id, idx)
) STRICT;

CREATE INDEX ix_property_values_property ON property_values (property_id);

-- Derived (decision P-07): the effective value of each property of each live page after inheritance, maintained at
-- every write that can change it and rebuilt by `erable doctor --fix`. Same layout as property_values; origin is
-- own, inherited, default or conflict (no value columns); source_id is the providing category of an inherited value.
CREATE TABLE resolved_values (
    page_id      INTEGER NOT NULL REFERENCES elements (id),
    property_id  INTEGER NOT NULL,
    idx          INTEGER NOT NULL,
    v_int        INTEGER,
    v_real       REAL,
    v_text       TEXT,
    v_ref        INTEGER,
    origin       TEXT    NOT NULL,
    source_id    INTEGER,
    PRIMARY KEY (page_id, property_id, idx)
) STRICT;

CREATE INDEX ix_resolved_values_property ON resolved_values (property_id, v_int, v_real, v_text, v_ref);

-- Derived: rules the effective value of a live page does not satisfy (§5.4, F08.R3). They warn, never block.
-- rule_index is the position of the rule in the definition; item_index the failing item of a list.
CREATE TABLE validation_issues (
    owner_id     INTEGER NOT NULL REFERENCES elements (id),
    property_id  INTEGER NOT NULL,
    rule_index   INTEGER NOT NULL,
    rule_op      TEXT    NOT NULL,
    rule_json    TEXT    NOT NULL,
    item_index   INTEGER
) STRICT;

CREATE INDEX ix_validation_issues_owner ON validation_issues (owner_id);
CREATE INDEX ix_validation_issues_property ON validation_issues (property_id);

-- Link types and links (§5.5, F09), filled from M2-07. Same element space: the name is the element name.
CREATE TABLE link_types (
    id            INTEGER PRIMARY KEY REFERENCES elements (id),
    description   TEXT    NOT NULL DEFAULT '',
    inverse_name  TEXT
) STRICT;

CREATE TABLE links (
    id            INTEGER PRIMARY KEY,
    source_id     INTEGER NOT NULL REFERENCES elements (id),
    link_type_id  INTEGER NOT NULL REFERENCES elements (id),
    target_id     INTEGER NOT NULL REFERENCES elements (id),
    created_at    TEXT    NOT NULL,
    created_by    TEXT    NOT NULL,
    UNIQUE (source_id, link_type_id, target_id)
) STRICT;

CREATE INDEX ix_links_target ON links (target_id);
CREATE INDEX ix_links_type ON links (link_type_id);

-- Format v1 had bare category, property and link type elements: give each its row. A v1 property becomes a string
-- property with a key derived from its ID (unique; renamable afterwards).
INSERT INTO categories (id, parent_category_id, description)
    SELECT id, NULL, '' FROM elements WHERE kind = 'category';
INSERT INTO property_defs (id, key, description, type, default_json, rules_json, enum_json)
    SELECT id, 'p_' || lower(printf('%016X', id)), '', 'string', NULL, '[]', '[]' FROM elements WHERE kind = 'propertyDefinition';
INSERT INTO link_types (id, description, inverse_name)
    SELECT id, '', NULL FROM elements WHERE kind = 'linkType';
