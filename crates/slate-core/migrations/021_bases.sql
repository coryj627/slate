-- Migration 021: Bases scanner indexes (Milestone N, #693).
--
-- SQLite is an index, not the source of truth: `.base` YAML stays on
-- disk and is re-read on open. These rows are derived during scan so
-- UI surfaces can list bases and embedded query fences without
-- rescanning every Markdown body.

CREATE TABLE bases_files (
    file_id           INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
    name              TEXT    NOT NULL,
    parsed_query_json TEXT    NOT NULL,
    warning_count     INTEGER NOT NULL,
    parser_version    INTEGER NOT NULL,
    indexed_at_ms     INTEGER NOT NULL
);

CREATE INDEX idx_bases_files_name ON bases_files(name);

CREATE TABLE bases_blocks (
    file_id     INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    fence_kind  INTEGER NOT NULL CHECK (fence_kind IN (0, 1, 2)),
    source_text TEXT    NOT NULL,
    line        INTEGER NOT NULL,
    byte_offset INTEGER NOT NULL
);

CREATE INDEX idx_bases_blocks_file ON bases_blocks(file_id);

-- Existing files may be unchanged after upgrading, so force the next
-- scan onto the slow path to backfill both derived tables.
UPDATE files SET mtime_ms = 0;
