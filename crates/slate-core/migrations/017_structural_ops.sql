-- Migration 017: structural-operation journal (#460, U2-2).
--
-- One row per folder/file create/rename/move/delete, appended in the same
-- transaction as the mutation's index updates. The CONTENT history of any
-- rewritten file lives in the per-file op-logs (keyed by files.id, which
-- structural ops preserve); this table carries the structural facts —
-- what moved where, and which files' link text was rewritten with which
-- pre/post hashes — so `undo_structural` can invert the latest op and
-- restore rewritten files byte-exactly through their op-logs.
--
-- Undo discipline: only MAX(id) is undoable (out-of-order undo would
-- re-introduce the multi-file consistency problem this journal exists to
-- avoid). Deletes are journaled for auditability but not undoable here
-- (bytes are in the system trash; restore-from-trash is a follow-up).
--
-- No mtime invalidation needed: rows are written going forward only; the
-- table starts empty on migration.

CREATE TABLE structural_ops (
    id           INTEGER PRIMARY KEY,
    timestamp_ms INTEGER NOT NULL,
    kind         TEXT NOT NULL,  -- structural::StructuralOpKind::as_str values
    payload      TEXT NOT NULL   -- JSON: structural::StructuralOpPayload
);
