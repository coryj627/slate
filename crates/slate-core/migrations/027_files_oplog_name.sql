-- O-1 (#539): durable op-log identity.
--
-- `oplog_name` is the log filename stem under `<cache_dir>/oplog/` and
-- is the ONLY file↔log binding after O-1 — log paths are never derived
-- from `files.id`, whose rowids SQLite recycles after a delete (a new
-- note must never inherit a dead note's history).
--
-- The UPDATE stamps the legacy `<id>.oplog` binding for every row that
-- exists at migration time. This is deliberately done here, not at
-- scan-reconcile time: at the moment this migration runs on an intact
-- cache, the ids are exactly the ids that named the logs, so the
-- stamping is provably correct — while a REBUILT cache (deleted
-- cache.sqlite) runs this migration on an EMPTY files table, stamps
-- nothing, and leaves the scan reconcile to re-bind logs by their v2
-- header paths / PathChanged markers / content salvage. Files that
-- never had a log get a stem pointing at a file that doesn't exist,
-- which reads as an empty history and is replaced by the same stem on
-- first save (append_entry creates it with a v2 header).
ALTER TABLE files ADD COLUMN oplog_name TEXT;
UPDATE files SET oplog_name = CAST(id AS TEXT);

-- One log, one file — structurally. The partial UNIQUE index makes a
-- double-binding (two files sharing one history) a constraint error
-- instead of a latent cross-attach: the milestone's "never attach
-- another file's history" invariant enforced by the schema, not just
-- by code discipline (Codoki review, PR #790). Legacy stamping above
-- writes distinct ids, so the index always builds cleanly.
CREATE UNIQUE INDEX files_oplog_name_unique
    ON files(oplog_name) WHERE oplog_name IS NOT NULL;
