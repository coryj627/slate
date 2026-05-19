-- Migration 006: FTS5 full-text search index.
--
-- Adds a denormalized `body_text` column to `files` (cached text of
-- the file, populated by the scanner's slow path) and an
-- external-content FTS5 virtual table over it. Used by the
-- `full_text_search` API in #E2.
--
-- ## Design choice: external-content FTS5
--
-- The alternative is a standalone FTS5 table that owns its own
-- copy of the body. We pick external-content so the body lives in
-- one place (the `files` table) — easier to keep consistent and
-- saves the ~doubled storage. FTS5 reads `files.body_text` for
-- snippet reconstruction during searches.
--
-- ## Sync strategy: triggers gated on body_text changes
--
-- We attach AFTER INSERT / UPDATE OF body_text / DELETE triggers
-- to `files`. The `UPDATE OF body_text` form is critical: the
-- scanner's fast path runs `UPDATE files SET indexed_at_ms = ?,
-- ctime_ms = ?` (no body_text in the SET clause), which would
-- otherwise force an FTS rebuild on every unchanged file. Gating
-- the trigger on `body_text` means a 10k-file vault scan with
-- zero content changes does zero FTS work.
--
-- The slow path's `INSERT … ON CONFLICT DO UPDATE SET body_text =
-- excluded.body_text, …` does mention body_text in the SET clause,
-- so the trigger fires and the FTS row is rewritten.
--
-- ## Tokenizer: unicode61
--
-- Default unicode61 with no extra options for V1.E. Tester
-- feedback decides whether to add `remove_diacritics 2` or other
-- configuration in a follow-up.

ALTER TABLE files ADD COLUMN body_text TEXT NOT NULL DEFAULT '';

CREATE VIRTUAL TABLE files_fts USING fts5(
    body_text,
    content='files',
    content_rowid='id',
    tokenize='unicode61'
);

-- AFTER INSERT (markdown only): mirror new row's body_text into
-- the FTS index. Non-markdown files don't appear in keyword
-- searches over text, so creating empty FTS rows for them would
-- just bloat the index.
CREATE TRIGGER files_ai_fts AFTER INSERT ON files
WHEN new.is_markdown = 1
BEGIN
    INSERT INTO files_fts(rowid, body_text) VALUES (new.id, new.body_text);
END;

-- AFTER UPDATE OF body_text: scoped to that column so the fast
-- path (indexed_at_ms / ctime_ms only) doesn't trigger an FTS
-- rewrite. Delete + insert each gated on the relevant
-- is_markdown side so we handle every transition cleanly:
--   md  → md  (content changed) → delete + insert
--   md  → non (file got renamed off .md) → delete only
--   non → md  (.txt renamed to .md) → insert only
--   non → non → trigger body is a no-op
CREATE TRIGGER files_au_fts AFTER UPDATE OF body_text ON files
WHEN old.is_markdown = 1 OR new.is_markdown = 1
BEGIN
    INSERT INTO files_fts(files_fts, rowid, body_text)
        SELECT 'delete', old.id, old.body_text WHERE old.is_markdown = 1;
    INSERT INTO files_fts(rowid, body_text)
        SELECT new.id, new.body_text WHERE new.is_markdown = 1;
END;

-- AFTER DELETE (markdown only): drop the FTS row so a future
-- stale-row sweep doesn't leak tokens. Non-markdown files never
-- got an FTS row inserted, so deleting one would be a no-op at
-- best and an FTS5 internal-error at worst.
CREATE TRIGGER files_ad_fts AFTER DELETE ON files
WHEN old.is_markdown = 1
BEGIN
    INSERT INTO files_fts(files_fts, rowid, body_text)
        VALUES ('delete', old.id, old.body_text);
END;
