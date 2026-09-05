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

-- `token` (adversarial round 22): every intent carries its writer's
-- unique token, and every clear — the save's own, or a repair's — is
-- token-conditional.  A repair that selected an abandoned row can
-- therefore never erase a NEWER writer's replacement registered in
-- the meantime; deleting by path alone reopened the exact uncovered
-- interval the table exists to close.
--
-- IF NOT EXISTS: migration replay must be safe (the migration-026
-- epoch-mtime test replays the tail of this list on an existing
-- schema, the migration-033 precedent).
-- (path, token) PRIMARY KEY (adversarial round 25): one row per
-- path let a second writer's registration REPLACE the first's — the
-- second completed and cleared its own token, leaving the first
-- writer's post-write failure unmarked.  Every in-flight writer owns
-- its own durable row; each clear names its own (path, token) pair.
-- `registered_epoch` (adversarial round 31): the path's
-- `files.index_epoch` at registration time.  Every SUCCESSFUL
-- full-file index commit bumps the epoch (migration 036), so a
-- marker whose registered epoch is BEHIND the file's current epoch
-- is provably superseded — some later commit re-read the whole file
-- — and can be deleted without a repair.  This is durable state,
-- not a sampled baseline: the containment path re-derives it under
-- the writer lock, closing the selection-to-containment races that
-- version sampling could not.
CREATE TABLE IF NOT EXISTS text_write_intents (
    path             TEXT NOT NULL,
    created_ms       INTEGER NOT NULL,
    token            INTEGER NOT NULL DEFAULT 0,
    registered_epoch INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (path, token)
) WITHOUT ROWID;
