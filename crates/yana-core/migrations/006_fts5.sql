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

-- AFTER INSERT: when a new files row lands, mirror its body_text
-- into the FTS index. Triggered by the scanner's slow-path
-- `INSERT INTO files (…) VALUES (…)`.
CREATE TRIGGER files_ai_fts AFTER INSERT ON files BEGIN
    INSERT INTO files_fts(rowid, body_text) VALUES (new.id, new.body_text);
END;

-- AFTER UPDATE OF body_text: scoped to that column so the fast
-- path (which only touches indexed_at_ms / ctime_ms) doesn't
-- trigger an FTS rewrite.
CREATE TRIGGER files_au_fts AFTER UPDATE OF body_text ON files BEGIN
    INSERT INTO files_fts(files_fts, rowid, body_text)
        VALUES ('delete', old.id, old.body_text);
    INSERT INTO files_fts(rowid, body_text) VALUES (new.id, new.body_text);
END;

-- AFTER DELETE: ensures a future stale-row sweep doesn't leak FTS
-- tokens. (V1 doesn't sweep yet; landing the trigger now keeps the
-- sweep landing later from needing a separate migration.)
CREATE TRIGGER files_ad_fts AFTER DELETE ON files BEGIN
    INSERT INTO files_fts(files_fts, rowid, body_text)
        VALUES ('delete', old.id, old.body_text);
END;
