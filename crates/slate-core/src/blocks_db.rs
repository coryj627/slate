//! SQLite persistence for `^block-id` anchors.
//!
//! Mirrors the lifecycle of `headings_db` / `links_db` / `properties_db` /
//! `tasks_db`: a `replace_blocks_for_file` entry point that DELETE-then-
//! INSERTs every block-anchor row for one file, run from the scanner's
//! slow path and from `save_text_locked`'s reindex transaction.
//!
//! Read access goes through `resolve_block` — `resolve_embed` uses it to
//! turn a `(target_file_id, block_id)` pair into the block's byte range
//! + kind, which the resolver then reads from disk.

use rusqlite::{params, Connection, OptionalExtension, Transaction};

use crate::{extract_blocks, BlockKind, VaultError};

/// Atomically replace every `blocks` row for `file_id` with the
/// anchors extracted from `markdown_source`.
///
/// Called on the scanner's slow path only — the fast path
/// (mtime+size+ctime match) skips the table entirely so unchanged
/// files don't churn it. The fast path is correct because the
/// table's `file_id` cascade on `DELETE` keeps it consistent with
/// the files table.
pub(crate) fn replace_blocks_for_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    tx.execute("DELETE FROM blocks WHERE file_id = ?1", params![file_id])?;
    let anchors = extract_blocks(markdown_source);
    if anchors.is_empty() {
        return Ok(());
    }
    let mut stmt = tx.prepare_cached(
        "INSERT INTO blocks
            (file_id, ordinal, block_id, kind, line_start, line_end,
             byte_start, byte_end, text_preview)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9)",
    )?;
    for anchor in anchors {
        stmt.execute(params![
            file_id,
            anchor.ordinal as i64,
            anchor.block_id,
            anchor.kind.as_str(),
            anchor.line_start as i64,
            anchor.line_end as i64,
            anchor.byte_start as i64,
            anchor.byte_end as i64,
            anchor.text_preview,
        ])?;
    }
    Ok(())
}

/// Look up one block anchor by `(file_id, block_id)`. Returns
/// `None` when no anchor with that id exists for the file (or the
/// file has no anchors at all).
///
/// Used by `resolve_embed` to materialize a `Block` resolution
/// from a `[[note^id]]` reference.
pub(crate) fn resolve_block(
    conn: &Connection,
    file_id: i64,
    block_id: &str,
) -> Result<Option<ResolvedBlock>, VaultError> {
    let mut stmt = conn.prepare_cached(
        "SELECT kind, line_start, line_end, byte_start, byte_end
         FROM blocks
         WHERE file_id = ?1 AND block_id = ?2
         LIMIT 1",
    )?;
    let row = stmt
        .query_row(params![file_id, block_id], |row| {
            let kind_str: String = row.get(0)?;
            let line_start: i64 = row.get(1)?;
            let line_end: i64 = row.get(2)?;
            let byte_start: i64 = row.get(3)?;
            let byte_end: i64 = row.get(4)?;
            Ok(ResolvedBlock {
                kind: BlockKind::from_str(&kind_str).unwrap_or(BlockKind::Paragraph),
                line_start: line_start as u32,
                line_end: line_end as u32,
                byte_start: byte_start as u32,
                byte_end: byte_end as u32,
            })
        })
        .optional()?;
    Ok(row)
}

/// Per-row result of `resolve_block`. Doesn't carry the source
/// text — the resolver reads `byte_start..byte_end` from disk to
/// keep this query a single btree probe. `kind` / `line_start` /
/// `line_end` aren't read by the resolver today but are kept so a
/// future "show me where this block lives" affordance has the
/// metadata without a second query.
#[derive(Debug, Clone)]
#[allow(dead_code)]
pub(crate) struct ResolvedBlock {
    pub kind: BlockKind,
    pub line_start: u32,
    pub line_end: u32,
    pub byte_start: u32,
    pub byte_end: u32,
}

impl BlockKind {
    pub(crate) fn from_str(s: &str) -> Option<BlockKind> {
        match s {
            "paragraph" => Some(BlockKind::Paragraph),
            "list_item" => Some(BlockKind::ListItem),
            "blockquote" => Some(BlockKind::BlockQuote),
            _ => None,
        }
    }
}
