-- Migration 005: per-file frontmatter properties.
--
-- Powers the Properties Panel in Milestone D. One row per top-level
-- property (the parser's dotted-key flattening means nested objects
-- have already been split into individual rows by the time we get
-- here). Lists and tag_lists are stored as a single row with the
-- value_text holding a JSON array — `files_with_property` uses
-- SQLite's json_each to search inside them.
--
-- Columns:
--   file_id      — owning file (cascades on delete).
--   ordinal      — 0-based document order within the file's
--                  frontmatter, so the Properties Panel can render
--                  in the same order the user authored.
--   key          — full dotted key path (`person.name`, `tags`, etc.).
--   value_kind   — type tag from `yana_core::PropertyValue`:
--                  'text' | 'number' | 'boolean' | 'date' |
--                  'datetime' | 'wikilink' | 'list' | 'tag_list'.
--                  Numbers conflate integer + float; JSON in
--                  value_text preserves the distinction.
--   value_text   — JSON-encoded value. Atomic kinds store the
--                  obvious literal ("foo", 42, true, "2024-01-02").
--                  list / tag_list store a JSON array; the
--                  files_with_property query uses json_each to
--                  expand them at search time.
--
-- Rows are managed exclusively by the scanner's slow path; the fast
-- path (mtime+size+ctime match) never touches this table.

CREATE TABLE properties (
    file_id      INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    ordinal      INTEGER NOT NULL,
    key          TEXT NOT NULL,
    value_kind   TEXT NOT NULL CHECK (value_kind IN (
        'text', 'number', 'boolean', 'date', 'datetime',
        'wikilink', 'list', 'tag_list'
    )),
    value_text   TEXT NOT NULL,
    PRIMARY KEY (file_id, ordinal)
);

CREATE INDEX idx_properties_file ON properties(file_id);
-- files_with_property scans on key + value_text. The composite index
-- handles atomic lookups; list/tag_list queries fall back to a
-- file_id-narrow scan + json_each, which is fine at vault scales.
CREATE INDEX idx_properties_key_value ON properties(key, value_text);
