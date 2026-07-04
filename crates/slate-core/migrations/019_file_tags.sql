-- Migration 019: the file → tag dimension backing SearchScope::Tag
-- (#508).
--
-- Before this migration Slate had no queryable tag index. Two tag
-- sources existed, neither searchable as a unit:
--   - FRONTMATTER `tags:` lists lived in `properties_list_values`
--     (key='tags', migration 007) — searchable, but only the
--     frontmatter half.
--   - INLINE body `#tag`s were scanned by `editor_spans.rs` for
--     editor highlighting ONLY and indexed nowhere.
-- The reading view activates INLINE tags, so a frontmatter-only scope
-- would return zero results for a note that tags purely inline. This
-- table is the honest union of both dimensions.
--
-- `file_tags` holds one row per (file, distinct normalized tag). Rows
-- are scanner-managed, rebuilt wholesale per file on the slow path by
-- `tags_db::replace_tags_for_file` (DELETE-then-INSERT keyed by
-- file_id), exactly like `properties_list_values`. No primary key:
-- the writer inserts the already-deduplicated distinct set, so a
-- (file_id, tag_norm) uniqueness constraint would only cost index
-- maintenance for a guarantee the writer already provides.
--
-- Indexes:
--   idx_file_tags_tag  — (tag_norm, file_id) serves the scope lookup
--     (exact `tag_norm = ?` and the nested-child `tag_norm LIKE ?/%`
--     prefix probe) and keeps file_id on the leaf so the EXISTS /
--     DISTINCT join needs no table fetch.
--   idx_file_tags_file — (file_id) makes the per-file DELETE on
--     re-index an index probe, not a scan.

CREATE TABLE file_tags (
    file_id  INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    tag_norm TEXT NOT NULL
);

CREATE INDEX idx_file_tags_tag ON file_tags(tag_norm, file_id);
CREATE INDEX idx_file_tags_file ON file_tags(file_id);

-- Backfill: inline tags can only be recovered by re-running the Rust
-- scanner (there is no SQL that re-derives them from stored columns —
-- unlike migration 007's frontmatter backfill via json_each). Force
-- the scanner's slow path on the next scan the same way migrations
-- 012 / 018 do: `session.rs::index_file_slow_path` short-circuits when
-- a file's `(mtime_ms, size_bytes, ctime_ms)` triple matches the
-- cache, so zeroing mtime_ms makes that comparison false for every
-- real file and `replace_tags_for_file` runs once per file. The slow
-- path writes the real stat value back, bounding the cost to one
-- re-scan per file.
--
-- Kept even though migration 018 just zeroed mtime_ms: migrations must
-- be self-sufficient. A vault could migrate straight from a pre-018
-- schema (skipping 018's zeroing entirely), and a fresh vault that
-- jumps here has no files to re-scan anyway, so this is a no-op there.
UPDATE files SET mtime_ms = 0;
