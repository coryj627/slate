-- Migration 004: per-file outgoing links.
--
-- Powers the backlinks panel, outgoing-links panel, and unresolved-
-- links audit in Milestone C. One row per link reference in a note's
-- source, in document order.
--
-- Columns:
--   source_file_id  — the file the link appears in (foreign key to `files`).
--   target_path     — the resolved vault-relative target path. NULL for
--                     unresolved internal links and for externals.
--   target_raw      — the link's authored target string (pre-resolution),
--                     useful for unresolved/external rendering.
--   target_anchor   — wikilink `#heading` or `^block` suffix (or NULL).
--   kind            — 'wikilink' | 'markdown'.
--   is_embed        — true for `![[...]]` and `![alt](...)`.
--   is_external     — true for URL/mailto/fragment-only links.
--   snippet         — ±60 char context cached at scan time so backlinks
--                     don't re-read disk.
--   ordinal         — 0-based index within the source file.
--   span_start      — byte offset of the link in source.
--   span_end        — exclusive byte offset.
--
-- Rows are managed exclusively by the scanner: on a content change
-- (slow path), all rows for the source file are deleted and rewritten
-- in one transaction. The mtime+size+ctime fast path skips this work.

CREATE TABLE links (
    source_file_id  INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    ordinal         INTEGER NOT NULL,
    target_path     TEXT,
    target_raw      TEXT NOT NULL,
    target_anchor   TEXT,
    kind            TEXT NOT NULL CHECK (kind IN ('wikilink', 'markdown')),
    is_embed        INTEGER NOT NULL DEFAULT 0,
    is_external     INTEGER NOT NULL DEFAULT 0,
    snippet         TEXT NOT NULL DEFAULT '',
    span_start      INTEGER NOT NULL,
    span_end        INTEGER NOT NULL,
    PRIMARY KEY (source_file_id, ordinal)
);

-- Outgoing-links queries scan by source.
CREATE INDEX idx_links_source ON links(source_file_id);
-- Backlinks queries scan by target.
CREATE INDEX idx_links_target ON links(target_path);
