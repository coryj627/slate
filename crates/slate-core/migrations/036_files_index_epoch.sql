-- Migration 036: per-file index epoch (W4-3 adversarial rounds
-- 31-32).
--
-- Stamped inside the SAME transaction as every SUCCESSFUL full-file
-- index commit (`index_file` and `index_saved_file`), so the epoch
-- advances exactly when the index provably reflects a fresh read of
-- the whole file.  A durable write intent (035) records the epoch it
-- registered against; a marker whose epoch is behind the file's
-- current epoch is provably superseded by a later successful commit
-- and may be cleared without a repair.  Because the stamp commits
-- atomically with the index rows it vouches for, there is no
-- rollback hazard: a failed save moves neither the rows nor the
-- epoch, keeping its marker suspect — exactly right.
--
-- The stamped values come from a GLOBAL monotonic clock (round 32),
-- not a per-file counter: a per-file counter resets when the files
-- row is deleted and recreated, so a marker from a prior incarnation
-- could compare EQUAL to or ABOVE the recreation's epoch — the
-- classic ABA — and vouch for quarantining the replacement file's
-- fresh rows.  The clock survives row deletion and never repeats, so
-- any full-file commit stamps strictly greater than every earlier
-- marker, in any incarnation.
ALTER TABLE files ADD COLUMN index_epoch INTEGER NOT NULL DEFAULT 0;

CREATE TABLE index_epoch_clock (
    clock INTEGER NOT NULL
);
INSERT INTO index_epoch_clock (clock) VALUES (0);
