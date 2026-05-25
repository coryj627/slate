-- Migration 011: per-file `^block-id` anchors.
--
-- Powers `resolve_embed`'s `![[note^block-id]]` path in Milestone J.
-- One row per `^block-id` discovered in a markdown file's body
-- (paragraph / list item / blockquote), in document order. The
-- `(file_id, block_id)` index lets `resolve_embed` look up a block
-- by name as a single btree probe.
--
-- Columns:
--   file_id      — owning file (cascades on delete).
--   ordinal      — 0-based document order within the file. Stable
--                  across saves for a given parser version.
--   block_id     — the `^id` slug, without the caret.
--   kind         — 'paragraph' | 'list_item' | 'blockquote'. The
--                  resolver renders each kind slightly differently
--                  in the embed UI (list items get a leading bullet,
--                  blockquotes get the `> ` prefix re-applied).
--   line_start   — 1-based start line in source.
--   line_end     — 1-based end line (inclusive).
--   byte_start   — byte offset of the block's first character.
--   byte_end     — exclusive byte offset.
--   text_preview — first ~120 chars of the block, for the AT label
--                  on the embed disclosure group. Stored alongside
--                  byte range so the bundle fetch doesn't have to
--                  re-read disk to populate the panel header.
--
-- Duplicate `^id` values within the same file keep the first
-- occurrence — Obsidian's behavior. Subsequent dupes drop silently
-- (the conflict would otherwise hit the PRIMARY KEY uniqueness on
-- `(file_id, ordinal)` from a different angle and need its own
-- error path; cheaper to dedupe at extraction time).
--
-- Rows are managed exclusively by the scanner's slow path + the
-- `save_text_locked` reindex transaction — same lifecycle as
-- headings / links / properties / tasks.

CREATE TABLE blocks (
    file_id      INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    ordinal      INTEGER NOT NULL,
    block_id     TEXT NOT NULL,
    kind         TEXT NOT NULL CHECK (kind IN ('paragraph', 'list_item', 'blockquote')),
    line_start   INTEGER NOT NULL,
    line_end     INTEGER NOT NULL,
    byte_start   INTEGER NOT NULL,
    byte_end     INTEGER NOT NULL,
    text_preview TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (file_id, ordinal)
);

-- Resolver lookup: `resolve_embed("note^block-id")` hits this index.
CREATE INDEX idx_blocks_lookup ON blocks(file_id, block_id);
