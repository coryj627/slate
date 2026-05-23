-- Migration 003: per-file Markdown headings.
--
-- Used by the outline panel + heading-rotor navigation in Milestone B.
-- Each row is one heading from a Markdown file, in document order.
-- `ordinal` is the heading's index within its file (0-based), so paging
-- and re-ordering stay cheap. `anchor_id` is a stable slug derived from
-- the heading text — outline activation and future deep-link URLs both
-- need a deterministic id per heading.
--
-- Rows are managed exclusively by the scanner: on a content change
-- (slow path), all existing rows for the file are deleted and rewritten
-- in one transaction. The mtime+size+ctime fast path skips this work,
-- which is the whole point of the optimization.

CREATE TABLE headings (
    file_id    INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    ordinal    INTEGER NOT NULL,
    level      INTEGER NOT NULL,
    text       TEXT NOT NULL,
    anchor_id  TEXT NOT NULL,
    PRIMARY KEY (file_id, ordinal)
);

CREATE INDEX idx_headings_file ON headings(file_id);
