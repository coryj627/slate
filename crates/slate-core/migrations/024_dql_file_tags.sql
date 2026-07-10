-- Migration 024: raw, ordered tag projection for Dataview DQL compatibility.
--
-- `file_tags` remains the normalized, sorted membership index used by native
-- Slate search. Dataview exposes a case-sensitive insertion-ordered Set. The
-- scanner stores explicit tags here (without the leading '#') in Dataview
-- insertion order: inline source tags first, then frontmatter `tag` / `tags`
-- values. Parent expansion (`A/B` -> `#A/B`, `#A`) happens at the DQL
-- evaluation boundary, leaving this table as the canonical explicit-tag
-- projection.
-- Keeping it separate preserves native lookup and both migration-019 indexes.

CREATE TABLE dql_file_tags (
    file_id  INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    ordinal  INTEGER NOT NULL,
    tag_raw  TEXT NOT NULL
);

CREATE INDEX idx_dql_file_tags_file ON dql_file_tags(file_id, ordinal);

-- Existing vaults need one slow-path pass to populate the new projection.
UPDATE files SET mtime_ms = 0;
