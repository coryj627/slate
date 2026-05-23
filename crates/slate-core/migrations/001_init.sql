-- Migration 001: initial schema.
--
-- Creates the `files` table per docs/plans/05_locked_architecture_decisions.md §4.5.
-- This is the only table needed for Milestone A (vault + file list); subsequent
-- migrations will add headings, links, properties, tasks, etc.

CREATE TABLE files (
    id              INTEGER PRIMARY KEY,
    path            TEXT NOT NULL UNIQUE,
    name            TEXT NOT NULL,
    extension       TEXT,
    size_bytes      INTEGER NOT NULL,
    mtime_ms        INTEGER NOT NULL,
    content_hash    TEXT NOT NULL,
    parser_version  INTEGER NOT NULL,
    indexed_at_ms   INTEGER NOT NULL,
    is_markdown     INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_files_extension ON files(extension);
CREATE INDEX idx_files_mtime ON files(mtime_ms);
