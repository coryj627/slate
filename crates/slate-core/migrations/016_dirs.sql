-- Migration 016: first-class directories (#459, U2-1).
--
-- Files have always been indexed; directories were only ever an
-- implicit artifact of a file's path. That leaves empty folders — which
-- U2 lets the user create — invisible to a files-derived tree, and gives
-- the tree no stable node id for a directory across rescans. This table
-- makes directories first-class, mirroring how `files.path` already works
-- so a folder move stays a single-pass prefix update (gap_analysis G10).
--
-- `parent_path` (not `parent_id`) keeps prefix-updates on move single-pass
-- and mirrors `files.path`; referential integrity is enforced by the
-- census, and rows are regenerable from a rescan (SQLite-is-index-not-
-- source-of-truth, `docs/plans/05` §9.2). Rows are scanner-managed: the
-- directory walk upserts every non-dot directory it encounters and deletes
-- rows it didn't see this pass, so empty on-disk directories get rows and
-- vanished ones are cleaned up.
--
-- No mtime invalidation is needed here (unlike migrations 012/014/015): the
-- scanner walks every directory on every pass regardless of the per-file
-- fast path, so the first scan after this migration populates `dirs` in
-- full without a forced file re-read.

CREATE TABLE dirs (
    id          INTEGER PRIMARY KEY,
    path        TEXT NOT NULL UNIQUE,   -- vault-relative, forward slashes, no trailing /
    parent_path TEXT NOT NULL,          -- "" for root children
    name        TEXT NOT NULL
);

CREATE INDEX idx_dirs_parent ON dirs(parent_path);
