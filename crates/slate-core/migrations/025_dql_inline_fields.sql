-- Migration 025: body inline metadata for Dataview DQL compatibility.
--
-- Frontmatter remains in the migration-005 `properties` projection. These
-- scanner-owned rows contain only page fields authored in Markdown body text,
-- ordered exactly as Dataview merges them: non-task list fields first, then
-- ordinary page fields. Duplicate exact keys intentionally remain separate;
-- the DQL load boundary coalesces frontmatter + ordered body values before
-- canonical aliases are synthesized.

CREATE TABLE dql_inline_fields (
    file_id    INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    ordinal    INTEGER NOT NULL,
    key        TEXT NOT NULL,
    value_json TEXT NOT NULL,
    PRIMARY KEY (file_id, ordinal)
);

CREATE INDEX idx_dql_inline_fields_file ON dql_inline_fields(file_id, ordinal);

-- One state row per scanned Markdown file. `incomplete = 1` means Slate could
-- not reproduce Obsidian metadata routing or a valid typed value exactly; DQL
-- note-property access must fail loud rather than execute over partial fields.
CREATE TABLE dql_inline_field_state (
    file_id    INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
    incomplete INTEGER NOT NULL CHECK (incomplete IN (0, 1))
);

-- Existing vaults need one bounded slow-path pass to populate both tables.
UPDATE files SET mtime_ms = 0;
