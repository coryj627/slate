-- Migration 035: durable cross-process text-write intents (W4-3
-- adversarial round 21).
--
-- `save_text` writes the FILE before committing its index update, so a
-- post-write failure rolls the index back while disk keeps the new
-- bytes.  In-process the host's repair quarantine covers that interval,
-- but the cache is shared across processes and a rollback moves no
-- committed state another process could observe (`data_version` only
-- advances on commits).  A row here is committed BEFORE the file write
-- and deleted inside the index transaction (or by a later repair), so a
-- surviving row marks the path's index as suspect for EVERY process.
-- Rows abandoned past a liveness threshold are self-healed by the task
-- query paths via a single-file reindex.

-- IF NOT EXISTS: migration replay must be safe (the migration-026
-- epoch-mtime test replays the tail of this list on an existing
-- schema, the migration-033 precedent).
CREATE TABLE IF NOT EXISTS text_write_intents (
    path       TEXT PRIMARY KEY,
    created_ms INTEGER NOT NULL
) WITHOUT ROWID;
