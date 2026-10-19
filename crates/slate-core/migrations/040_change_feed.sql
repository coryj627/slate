-- Migration 040: the change feed (plan 43 PR 21a, #1293; contract 40
-- AR-30 and AR-31; design
-- docs/plans/43_mac_backlog_delivery/21_change_feed_design.md §4.1, §4.2
-- and §5).
--
-- The index records what is current; this migration makes it record what
-- changed, and who changed it, so a host's rescan can count and apply the
-- changes another Slate session (another window, `slate-cli`) already
-- indexed, exactly.
--
-- ## The table
--
-- One row per changed path of `files` or `dirs`. `seq` is AUTOINCREMENT:
-- SQLite allocates it under its one-writer lock, in commit order, densely
-- (a rolled-back statement or transaction leaves no gap) and never reuses
-- a value after a deletion. The feed's head is AUTOINCREMENT's high-water
-- mark — `COALESCE((SELECT seq FROM sqlite_sequence WHERE name =
-- 'change_feed'), 0)` — not the largest surviving `seq`, so trimming every
-- row never moves it back.
--
-- `writer` is `slate_feed_writer()`, `"<actor>:<pid>:<nonce>"` on a
-- session's connection and `"unattributed"` on every other Slate
-- connection; `origin` is `slate_feed_origin()`, 1 while the writing
-- session is inside one of its own announced (Slate-owned) write
-- transactions and 0 otherwise (a scan, a lazy first index, open-time
-- recovery). Both are connection functions registered by
-- `db::register_connection_functions`, so a raw connection without them
-- cannot change a watched column (§9 residual 4).
--
-- ## The triggers
--
-- Six persistent triggers, the migration-006 FTS precedent: AFTER INSERT,
-- AFTER UPDATE OF path, content_hash and AFTER DELETE on `files`; AFTER
-- INSERT, AFTER UPDATE OF path and AFTER DELETE on `dirs`. Each runs inside
-- the mutating statement, so a feed row commits or rolls back with its
-- index mutation, on every connection of every process. The update
-- triggers name their columns, so the scan's fast path
-- (`UPDATE files SET indexed_at_ms, ctime_ms, birthtime_ms`) and its
-- `dirs` upsert of an existing directory fire nothing, and each fires only
-- when a watched value really changed (`OLD.x IS NOT NEW.x`): a same-hash
-- upsert writes no row.
--
-- Every trigger is gated on `EXISTS (SELECT 1 FROM change_feed_cursors)`:
-- while no host has registered a cursor, nothing is written, and the feed
-- costs one gate check per mutation.
--
-- There is no backfill. `IF NOT EXISTS` keeps a replay safe (the migration
-- 035 precedent).

CREATE TABLE IF NOT EXISTS change_feed (
    seq           INTEGER PRIMARY KEY AUTOINCREMENT,
    kind          INTEGER NOT NULL,  -- 1 created, 2 modified, 3 deleted, 4 renamed
    entry         INTEGER NOT NULL,  -- 0 file, 1 directory
    path          TEXT NOT NULL,
    previous_path TEXT,              -- renamed: the path before
    hash_before   TEXT,              -- files.content_hash before (files only)
    hash_after    TEXT,              -- files.content_hash after (files only)
    writer        TEXT NOT NULL,     -- slate_feed_writer()
    origin        INTEGER NOT NULL,  -- slate_feed_origin(): 0 reconcile, 1 write
    written_ms    INTEGER NOT NULL   -- the statement time, for diagnostics
);

-- One row per consuming session (a host registers after its open scan).
-- A cursor whose heartbeat is older than 24 hours is reaped by the next
-- registration or acknowledgment of any session; `resync_pending` marks a
-- cursor a gap re-created, until the host's full re-sync is acknowledged.
CREATE TABLE IF NOT EXISTS change_feed_cursors (
    writer         TEXT PRIMARY KEY,
    seq            INTEGER NOT NULL,
    heartbeat_ms   INTEGER NOT NULL,
    resync_pending INTEGER NOT NULL DEFAULT 0
) WITHOUT ROWID;

-- `trimmed_through`: the highest `seq` any trim deleted. A cursor below it
-- has lost rows it needed (a gap).
CREATE TABLE IF NOT EXISTS change_feed_meta (
    id              INTEGER PRIMARY KEY CHECK (id = 1),
    trimmed_through INTEGER NOT NULL
);
INSERT OR IGNORE INTO change_feed_meta (id, trimmed_through) VALUES (1, 0);

CREATE TRIGGER IF NOT EXISTS files_ai_feed AFTER INSERT ON files
WHEN EXISTS (SELECT 1 FROM change_feed_cursors)
BEGIN
    INSERT INTO change_feed (kind, entry, path, previous_path, hash_before,
                             hash_after, writer, origin, written_ms)
    VALUES (1, 0, NEW.path, NULL, NULL, NEW.content_hash, slate_feed_writer(),
            slate_feed_origin(), CAST(unixepoch('subsec') * 1000 AS INTEGER));
END;

CREATE TRIGGER IF NOT EXISTS files_au_feed AFTER UPDATE OF path, content_hash ON files
WHEN (OLD.path IS NOT NEW.path OR OLD.content_hash IS NOT NEW.content_hash)
    AND EXISTS (SELECT 1 FROM change_feed_cursors)
BEGIN
    INSERT INTO change_feed (kind, entry, path, previous_path, hash_before,
                             hash_after, writer, origin, written_ms)
    VALUES (CASE WHEN OLD.path IS NOT NEW.path THEN 4 ELSE 2 END, 0, NEW.path,
            CASE WHEN OLD.path IS NOT NEW.path THEN OLD.path END,
            OLD.content_hash, NEW.content_hash, slate_feed_writer(),
            slate_feed_origin(), CAST(unixepoch('subsec') * 1000 AS INTEGER));
END;

CREATE TRIGGER IF NOT EXISTS files_ad_feed AFTER DELETE ON files
WHEN EXISTS (SELECT 1 FROM change_feed_cursors)
BEGIN
    INSERT INTO change_feed (kind, entry, path, previous_path, hash_before,
                             hash_after, writer, origin, written_ms)
    VALUES (3, 0, OLD.path, NULL, OLD.content_hash, NULL, slate_feed_writer(),
            slate_feed_origin(), CAST(unixepoch('subsec') * 1000 AS INTEGER));
END;

CREATE TRIGGER IF NOT EXISTS dirs_ai_feed AFTER INSERT ON dirs
WHEN EXISTS (SELECT 1 FROM change_feed_cursors)
BEGIN
    INSERT INTO change_feed (kind, entry, path, previous_path, hash_before,
                             hash_after, writer, origin, written_ms)
    VALUES (1, 1, NEW.path, NULL, NULL, NULL, slate_feed_writer(),
            slate_feed_origin(), CAST(unixepoch('subsec') * 1000 AS INTEGER));
END;

CREATE TRIGGER IF NOT EXISTS dirs_au_feed AFTER UPDATE OF path ON dirs
WHEN OLD.path IS NOT NEW.path
    AND EXISTS (SELECT 1 FROM change_feed_cursors)
BEGIN
    INSERT INTO change_feed (kind, entry, path, previous_path, hash_before,
                             hash_after, writer, origin, written_ms)
    VALUES (4, 1, NEW.path, OLD.path, NULL, NULL, slate_feed_writer(),
            slate_feed_origin(), CAST(unixepoch('subsec') * 1000 AS INTEGER));
END;

CREATE TRIGGER IF NOT EXISTS dirs_ad_feed AFTER DELETE ON dirs
WHEN EXISTS (SELECT 1 FROM change_feed_cursors)
BEGIN
    INSERT INTO change_feed (kind, entry, path, previous_path, hash_before,
                             hash_after, writer, origin, written_ms)
    VALUES (3, 1, OLD.path, NULL, NULL, NULL, slate_feed_writer(),
            slate_feed_origin(), CAST(unixepoch('subsec') * 1000 AS INTEGER));
END;
