-- Migration 015: links gain a display_text column (#433).
--
-- `![alt](src)` carries the author's alt text as the link's display
-- text, but it was never persisted — so image-embed resolution had
-- to re-read and re-parse the HOST file per image just to recover
-- the alt (#419's interim fix). Persisting it lets alt ride the
-- outgoing-links query the app already runs, and the resolver can
-- take it as an argument instead of doing IO.
--
-- Same invalidation shape as migrations 012/014: rows are
-- scanner-managed (replace_links_for_file is DELETE+INSERT on the
-- slow path), so existing caches would keep NULL display_text for
-- unchanged files. Wipe and zero mtimes so the next scan's slow
-- path rewrites every file's rows.

ALTER TABLE links ADD COLUMN display_text TEXT;
UPDATE files SET mtime_ms = 0;
DELETE FROM links;
