-- Migration 002: add ctime_ms to the files table.
--
-- ctime ("inode change time") updates whenever the inode's content or
-- metadata changes, including in cases where mtime is preserved (e.g.
-- `cp -p`, `rsync -a`, restoring from a snapshot). Including ctime in
-- the scanner's fast-path key lets us catch those mtime-preserving
-- writes that the mtime+size pair alone would miss.
--
-- Existing rows get 0 — interpreted by the scanner as "ctime unknown,
-- fall back to mtime+size only." New writes populate the real ctime
-- on Unix; on platforms where std::fs doesn't expose ctime cleanly
-- (Windows), the column stays at 0 and the fast-path keeps its
-- mtime+size semantics.

ALTER TABLE files ADD COLUMN ctime_ms INTEGER NOT NULL DEFAULT 0;
