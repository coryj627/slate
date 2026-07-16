-- Migration 032: durable recovery intent for structural batch moves.
--
-- A batch move spans filesystem, index, link-rewrite, and journal writes.
-- SQLite transactions cannot make those stores atomic, so the complete
-- rollback plan is committed here before the first physical rename.  There
-- can be at most one structural batch in flight because every structural
-- writer holds the vault-wide structural sidecar lock.

CREATE TABLE structural_batch_inflight (
    id                     INTEGER PRIMARY KEY CHECK (id = 1),
    started_ms             INTEGER NOT NULL,
    payload                TEXT NOT NULL,
    renames_completed      INTEGER NOT NULL DEFAULT 0 CHECK (renames_completed >= 0),
    index_committed        INTEGER NOT NULL DEFAULT 0 CHECK (index_committed IN (0, 1)),
    path_markers_completed INTEGER NOT NULL DEFAULT 0 CHECK (path_markers_completed >= 0)
);

-- Append-only per-file content intent. Keeping these rows separate from the
-- immutable batch plan avoids reserializing an O(N) JSON payload after every
-- rename in a 10,000-item batch.
CREATE TABLE structural_batch_inflight_rewrites (
    ordinal      INTEGER PRIMARY KEY,
    inflight_id  INTEGER NOT NULL DEFAULT 1 CHECK (inflight_id = 1)
                 REFERENCES structural_batch_inflight(id) ON DELETE CASCADE,
    path         TEXT NOT NULL,
    hash_before  TEXT NOT NULL,
    hash_after   TEXT NOT NULL
);
