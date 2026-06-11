-- Migration 014: headings gain a byte_offset column (#431).
--
-- The outline panel scrolled by SEARCHING the buffer for the
-- heading's rendered text, which silently fails for headings
-- containing inline markup (`## A **bold** heading` renders as
-- "A bold heading" and never matches the raw `**bold**` bytes) —
-- while the activation announcement claimed success. Storing the
-- heading's start offset in the original source lets the UI scroll
-- and park by position.
--
-- Same invalidation shape as migration 012: rows are
-- scanner-managed and only rewritten on the slow path, so existing
-- caches would keep byte_offset = 0 for unchanged files. Wipe the
-- table and zero every file's mtime so the next scan takes the slow
-- path once per file and rewrites rows with real offsets.

ALTER TABLE headings ADD COLUMN byte_offset INTEGER NOT NULL DEFAULT 0;
UPDATE files SET mtime_ms = 0;
DELETE FROM headings;
