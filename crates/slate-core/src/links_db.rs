// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite storage + query surface for note-to-note links.
//!
//! Schema lives in `migrations/004_links.sql`. Bulk rows are written
//! by [`replace_links_for_file`] (save path, scan slow path, purge);
//! two narrower mutations exist beside it: [`re_resolve_unresolved_links`]
//! flips NULL `target_path`s to resolved, and the move flow's bulk
//! inbound repoint (`finish_structural_move`) rewrites `target_path`
//! old → new. FK CASCADE additionally erases a deleted file's rows.
//! The query side serves the backlinks panel, outgoing-links panel,
//! unresolved-links audit (issues #51, #52), and the graph mirror's
//! replay hooks (Milestone P #550).

use rusqlite::{Connection, Transaction, params};

use crate::VaultError;
use crate::file_meta_db::{FileMetaParseArtifact, FileMetaPreviewObserver};
use crate::graph::{GraphLinkRow, InboundRow};
use crate::link_resolver::{InMemoryVaultIndex, ResolvedLink, resolve_link};
use crate::links::{LinkAnchor, LinkKind, extract_links_with_event_sink};
use crate::session::{Page, Paging};

/// One outgoing link from a source file (the file being queried) —
/// includes resolved, unresolved, and external links so the UI can
/// render them all in one list with kind flags.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct OutgoingLink {
    /// Resolved vault-relative path, or `None` for unresolved internal
    /// links and external links.
    pub target_path: Option<String>,
    /// Authored target string (what the user typed). Always populated.
    pub target_raw: String,
    /// Wikilink-style anchor (`#heading` / `^block`), serialized as
    /// `Some(("heading", text))` or `Some(("block", text))`.
    pub target_anchor: Option<(String, String)>,
    pub kind: String,
    pub is_embed: bool,
    pub is_external: bool,
    pub is_unresolved: bool,
    pub snippet: String,
    pub ordinal: u32,
    /// Exact authored source range in whole-file UTF-8 bytes.
    pub span_start: u32,
    pub span_end: u32,
    /// The link's display text — for `![alt](src)` image embeds this
    /// is the author's alt text (#433). `None` when not authored.
    pub display_text: Option<String>,
}

/// One backlink — a file that links TO the path we queried.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Backlink {
    /// Vault-relative path of the source file containing the link.
    pub source_path: String,
    /// Snippet of ±60 chars around the link site, from cached content.
    pub snippet: String,
    /// Source-file ordinal (0-based) of the link within its file.
    pub ordinal: u32,
    pub kind: String,
    pub is_embed: bool,
}

/// One unresolved internal link — a `[[target]]` or `[link](rel)` that
/// doesn't point to any indexed file.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct UnresolvedLink {
    pub source_path: String,
    pub target_raw: String,
    pub ordinal: u32,
    pub snippet: String,
}

pub(crate) struct LinkReplaceResult {
    pub(crate) rows: Vec<GraphLinkRow>,
    pub(crate) file_meta_artifact: FileMetaParseArtifact,
}

/// Atomically replace every row for `source_file_id` with links
/// extracted from `markdown_source`. Resolution runs against
/// `index` (the snapshot of vault paths captured at scan start).
///
/// Called by the scanner's slow path and the save/purge paths; the
/// scan fast path never touches this table so unchanged files don't
/// churn link rows.
///
/// Returns both the graph-relevant projection of exactly the rows it wrote and
/// the owned metadata artifact observed during that same authoritative parse.
/// The graph hook can replay without a read (Milestone P #550), and file-meta
/// finalization avoids a second Markdown parse when no valid wikilink rewrite
/// is required.
//
// 5 params is one over clippy's default ceiling but the bundling
// options (a borrow-laden struct, or splitting tx out) hurt
// readability more than they help: each param carries a distinct
// piece of shared scanner state that the caller already has on the
// stack.
#[allow(clippy::too_many_arguments)]
pub(crate) fn replace_links_for_file(
    tx: &Transaction,
    source_file_id: i64,
    source_path: &str,
    markdown_source: &str,
    index: &InMemoryVaultIndex,
) -> Result<LinkReplaceResult, VaultError> {
    tx.execute(
        "DELETE FROM links WHERE source_file_id = ?1",
        params![source_file_id],
    )?;
    let mut preview_observer = FileMetaPreviewObserver::new();
    let parsed = extract_links_with_event_sink(markdown_source, &mut preview_observer);
    let file_meta_artifact = preview_observer.into_artifact();
    if parsed.is_empty() {
        return Ok(LinkReplaceResult {
            rows: Vec::new(),
            file_meta_artifact,
        });
    }
    let mut stmt = tx.prepare_cached(
        "INSERT INTO links (
            source_file_id, ordinal, target_path, target_raw, target_anchor,
            kind, is_embed, is_external, snippet, span_start, span_end,
            display_text
        ) VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12)",
    )?;
    let mut written = Vec::with_capacity(parsed.len());
    for (ordinal, link) in parsed.into_iter().enumerate() {
        let resolved = resolve_link(&link.target_raw, link.anchor.clone(), source_path, index);
        let (target_path, is_external) = match &resolved {
            ResolvedLink::Resolved { target_path, .. } => (Some(target_path.clone()), false),
            ResolvedLink::Unresolved { .. } => (None, false),
            ResolvedLink::External => (None, true),
        };
        let anchor_str = serialize_anchor(link.anchor.as_ref());
        let kind = link_kind_to_str(link.kind);
        let snippet = snippet_around(markdown_source, link.span_start, link.span_end);
        stmt.execute(params![
            source_file_id,
            ordinal as i64,
            target_path,
            link.target_raw,
            anchor_str,
            kind,
            link.is_embed as i64,
            is_external as i64,
            snippet,
            link.span_start as i64,
            link.span_end as i64,
            link.display_text,
        ])?;
        written.push(GraphLinkRow {
            target_path,
            target_raw: link.target_raw,
            is_embed: link.is_embed,
            is_external,
        });
    }
    Ok(LinkReplaceResult {
        rows: written,
        file_meta_artifact,
    })
}

/// The graph-relevant projection of one source's current rows, in
/// ordinal order — the replay payload after `re_resolve_unresolved_links`
/// touches a source the graph hook didn't just rewrite itself.
pub(crate) fn graph_linkset_for(
    tx: &Transaction,
    source_path: &str,
) -> Result<Vec<GraphLinkRow>, VaultError> {
    let mut stmt = tx.prepare_cached(
        "SELECT links.target_path, links.target_raw, links.is_embed, links.is_external
         FROM links
         JOIN files ON files.id = links.source_file_id
         WHERE files.path = ?1
         ORDER BY links.ordinal ASC",
    )?;
    let rows = stmt.query_map(params![source_path], |row| {
        Ok(GraphLinkRow {
            target_path: row.get::<_, Option<String>>(0)?,
            target_raw: row.get::<_, String>(1)?,
            is_embed: row.get::<_, i64>(2)? != 0,
            is_external: row.get::<_, i64>(3)? != 0,
        })
    })?;
    rows.collect::<Result<Vec<_>, _>>()
        .map_err(VaultError::from)
}

/// Every internal row pointing AT `target_path`, in deterministic
/// `(source path, ordinal)` order. Captured by the graph hooks
/// in-transaction — before a CASCADE delete erases the target's
/// `files` row, or right after a file (re)appears at a path that
/// dangling rows still name (p0_spec P0-1 rule 1a).
pub(crate) fn graph_inbound_rows(
    conn: &Connection,
    target_path: &str,
) -> Result<Vec<InboundRow>, VaultError> {
    let mut stmt = conn.prepare_cached(
        "SELECT files.path, links.target_raw, links.is_embed
         FROM links
         JOIN files ON files.id = links.source_file_id
         WHERE links.target_path = ?1 AND links.is_external = 0
         ORDER BY files.path ASC, links.ordinal ASC",
    )?;
    let rows = stmt.query_map(params![target_path], |row| {
        Ok(InboundRow {
            source_path: row.get::<_, String>(0)?,
            target_raw: row.get::<_, String>(1)?,
            is_embed: row.get::<_, i64>(2)? != 0,
        })
    })?;
    rows.collect::<Result<Vec<_>, _>>()
        .map_err(VaultError::from)
}

/// After the initial scan completes, re-resolve any link whose
/// `target_path` was NULL because the target file hadn't been
/// inserted yet at the time the source file was scanned.
///
/// Pure SQL pass — reads every unresolved internal link, runs the
/// resolver against the now-complete vault index, and updates rows
/// that newly resolve.
///
/// Returns the sorted, distinct source paths whose rows actually
/// changed, so the graph hook can replay each affected linkset
/// (Milestone P #550). Empty when nothing resolved.
pub(crate) fn re_resolve_unresolved_links(tx: &Transaction) -> Result<Vec<String>, VaultError> {
    let paths: Vec<String> = tx
        .prepare("SELECT path FROM files")?
        .query_map([], |row| row.get::<_, String>(0))?
        .collect::<Result<Vec<_>, _>>()?;
    let index = InMemoryVaultIndex::new(paths);

    let mut select = tx.prepare(
        "SELECT links.rowid, files.path, links.target_raw, links.target_anchor
         FROM links
         JOIN files ON files.id = links.source_file_id
         WHERE links.target_path IS NULL AND links.is_external = 0",
    )?;
    let rows: Vec<(i64, String, String, Option<String>)> = select
        .query_map([], |row| {
            Ok((
                row.get::<_, i64>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, String>(2)?,
                row.get::<_, Option<String>>(3)?,
            ))
        })?
        .collect::<Result<Vec<_>, _>>()?;

    if rows.is_empty() {
        return Ok(Vec::new());
    }

    let mut affected: Vec<String> = Vec::new();
    let mut update = tx.prepare("UPDATE links SET target_path = ?1 WHERE rowid = ?2")?;
    for (rowid, source_path, target_raw, anchor_str) in rows {
        let anchor = deserialize_anchor(anchor_str.as_deref());
        let resolved = resolve_link(&target_raw, anchor, &source_path, &index);
        if let ResolvedLink::Resolved { target_path, .. } = resolved {
            update.execute(params![target_path, rowid])?;
            affected.push(source_path);
        }
    }
    affected.sort();
    affected.dedup();
    Ok(affected)
}

/// All outgoing links from `source_path`, in document order.
pub(crate) fn outgoing_links_for(
    conn: &Connection,
    source_path: &str,
) -> Result<Vec<OutgoingLink>, VaultError> {
    let mut stmt = conn.prepare_cached(
        "SELECT links.target_path, links.target_raw, links.target_anchor,
                links.kind, links.is_embed, links.is_external, links.snippet,
                links.ordinal, links.span_start, links.span_end, links.display_text
         FROM links
         JOIN files ON files.id = links.source_file_id
         WHERE files.path = ?1
         ORDER BY links.ordinal ASC",
    )?;
    let rows = stmt.query_map(params![source_path], |row| {
        Ok(OutgoingLink {
            target_path: row.get::<_, Option<String>>(0)?,
            target_raw: row.get::<_, String>(1)?,
            target_anchor: deserialize_anchor_pair(row.get::<_, Option<String>>(2)?.as_deref()),
            kind: row.get::<_, String>(3)?,
            is_embed: row.get::<_, i64>(4)? != 0,
            is_external: row.get::<_, i64>(5)? != 0,
            // Unresolved when the link is internal (not external) AND the
            // resolver couldn't pick a target. The fast-to-derive flag
            // keeps consumers from re-implementing the "internal +
            // null target" check at every call site.
            is_unresolved: row.get::<_, Option<String>>(0)?.is_none() && row.get::<_, i64>(5)? == 0,
            snippet: row.get::<_, String>(6)?,
            ordinal: row.get::<_, i64>(7)? as u32,
            span_start: row.get::<_, i64>(8)? as u32,
            span_end: row.get::<_, i64>(9)? as u32,
            display_text: row.get::<_, Option<String>>(10)?,
        })
    })?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r?);
    }
    Ok(out)
}

/// Paged backlinks for a target path — every file with a resolved
/// link pointing at it. External links are excluded by construction
/// (their target_path is NULL). Cursor is the encoded
/// `(source_path, ordinal)` pair so identical-source files with
/// multiple inbound links page deterministically.
pub(crate) fn backlinks_for(
    conn: &Connection,
    target_path: &str,
    paging: Paging,
) -> Result<Page<Backlink>, VaultError> {
    let limit = paging.limit.clamp(1, 1000);
    let (after_path, after_ordinal): (Option<String>, Option<i64>) =
        if let Some(cursor) = paging.cursor.as_deref() {
            decode_backlink_cursor(cursor)
        } else {
            (None, None)
        };

    let sql = "SELECT files.path, links.snippet, links.ordinal, links.kind, links.is_embed
               FROM links
               JOIN files ON files.id = links.source_file_id
               WHERE links.target_path = ?1
                 AND (
                     ?2 IS NULL
                     OR files.path > ?2
                     OR (files.path = ?2 AND links.ordinal > ?3)
                 )
               ORDER BY files.path ASC, links.ordinal ASC
               LIMIT ?4";
    // SQLite needs a real integer parameter for the ordinal even when
    // path is NULL; use -1 as a sentinel that the path-comparison
    // branch makes unreachable.
    let ordinal_param = after_ordinal.unwrap_or(-1);
    let mut stmt = conn.prepare_cached(sql)?;
    let rows: Vec<Backlink> = stmt
        .query_map(
            params![target_path, after_path, ordinal_param, limit as i64 + 1],
            |row| {
                Ok(Backlink {
                    source_path: row.get::<_, String>(0)?,
                    snippet: row.get::<_, String>(1)?,
                    ordinal: row.get::<_, i64>(2)? as u32,
                    kind: row.get::<_, String>(3)?,
                    is_embed: row.get::<_, i64>(4)? != 0,
                })
            },
        )?
        .collect::<Result<Vec<_>, _>>()?;

    let total_filtered: u64 = conn.query_row(
        "SELECT COUNT(*) FROM links WHERE target_path = ?1",
        params![target_path],
        |row| row.get::<_, i64>(0),
    )? as u64;

    // Cursor derives from the LAST returned item (not the lookahead
    // row at index `limit`). The next page's WHERE clause is
    // `path > ? OR (path = ? AND ordinal > ?)` — exclusive of the
    // cursor's row — so encoding the lookahead row would skip it.
    let has_more = rows.len() > limit as usize;
    let items: Vec<Backlink> = rows.into_iter().take(limit as usize).collect();
    let next_cursor = if has_more {
        items
            .last()
            .map(|row| encode_backlink_cursor(&row.source_path, row.ordinal))
    } else {
        None
    };
    Ok(Page {
        items,
        next_cursor,
        total_filtered,
    })
}

/// Paged audit of every unresolved internal link in the vault. The
/// UI surfaces this in #52's unresolved-links panel.
pub(crate) fn unresolved_links(
    conn: &Connection,
    paging: Paging,
) -> Result<Page<UnresolvedLink>, VaultError> {
    let limit = paging.limit.clamp(1, 1000);
    let (after_path, after_ordinal): (Option<String>, Option<i64>) =
        if let Some(cursor) = paging.cursor.as_deref() {
            decode_backlink_cursor(cursor)
        } else {
            (None, None)
        };
    let sql = "SELECT files.path, links.target_raw, links.ordinal, links.snippet
               FROM links
               JOIN files ON files.id = links.source_file_id
               WHERE links.target_path IS NULL AND links.is_external = 0
                 AND (
                     ?1 IS NULL
                     OR files.path > ?1
                     OR (files.path = ?1 AND links.ordinal > ?2)
                 )
               ORDER BY files.path ASC, links.ordinal ASC
               LIMIT ?3";
    let ordinal_param = after_ordinal.unwrap_or(-1);
    let mut stmt = conn.prepare_cached(sql)?;
    let rows: Vec<UnresolvedLink> = stmt
        .query_map(
            params![after_path, ordinal_param, limit as i64 + 1],
            |row| {
                Ok(UnresolvedLink {
                    source_path: row.get::<_, String>(0)?,
                    target_raw: row.get::<_, String>(1)?,
                    ordinal: row.get::<_, i64>(2)? as u32,
                    snippet: row.get::<_, String>(3)?,
                })
            },
        )?
        .collect::<Result<Vec<_>, _>>()?;
    let total_filtered: u64 = conn.query_row(
        "SELECT COUNT(*) FROM links WHERE target_path IS NULL AND is_external = 0",
        [],
        |row| row.get::<_, i64>(0),
    )? as u64;

    // Cursor derives from the LAST returned item, not the lookahead
    // row — see backlinks_for for the off-by-one rationale.
    let has_more = rows.len() > limit as usize;
    let items: Vec<UnresolvedLink> = rows.into_iter().take(limit as usize).collect();
    let next_cursor = if has_more {
        items
            .last()
            .map(|row| encode_backlink_cursor(&row.source_path, row.ordinal))
    } else {
        None
    };
    Ok(Page {
        items,
        next_cursor,
        total_filtered,
    })
}

/// Build a ±60-char snippet centered on the link site. Falls back to
/// less than 60 chars if the source isn't that long on one side; we
/// trim to char boundaries so the snippet stays valid UTF-8 even on
/// multibyte input.
fn snippet_around(source: &str, span_start: usize, span_end: usize) -> String {
    const RADIUS: usize = 60;
    let start = span_start.saturating_sub(RADIUS);
    let end = span_end.saturating_add(RADIUS).min(source.len());
    let safe_start = round_down_to_char_boundary(source, start);
    let safe_end = round_up_to_char_boundary(source, end);
    let raw = &source[safe_start..safe_end];
    // Collapse interior line breaks to spaces — the snippet renders
    // inline in the backlinks panel, so a literal `\n` would push
    // the cursor's spoken position off the line VoiceOver expected.
    raw.replace(['\n', '\r'], " ")
}

fn round_down_to_char_boundary(s: &str, mut idx: usize) -> usize {
    while idx > 0 && !s.is_char_boundary(idx) {
        idx -= 1;
    }
    idx
}

fn round_up_to_char_boundary(s: &str, mut idx: usize) -> usize {
    while idx < s.len() && !s.is_char_boundary(idx) {
        idx += 1;
    }
    idx
}

fn link_kind_to_str(kind: LinkKind) -> &'static str {
    match kind {
        LinkKind::Wikilink => "wikilink",
        LinkKind::Markdown => "markdown",
    }
}

fn serialize_anchor(anchor: Option<&LinkAnchor>) -> Option<String> {
    anchor.map(|a| match a {
        LinkAnchor::Heading(t) => format!("h:{}", t),
        LinkAnchor::Block(t) => format!("b:{}", t),
    })
}

fn deserialize_anchor(stored: Option<&str>) -> Option<LinkAnchor> {
    let s = stored?;
    if let Some(rest) = s.strip_prefix("h:") {
        Some(LinkAnchor::Heading(rest.to_string()))
    } else {
        s.strip_prefix("b:")
            .map(|rest| LinkAnchor::Block(rest.to_string()))
    }
}

/// Same as `deserialize_anchor` but returns the `("heading"|"block",
/// text)` shape that the FFI expects.
fn deserialize_anchor_pair(stored: Option<&str>) -> Option<(String, String)> {
    let s = stored?;
    if let Some(rest) = s.strip_prefix("h:") {
        Some(("heading".to_string(), rest.to_string()))
    } else {
        s.strip_prefix("b:")
            .map(|rest| ("block".to_string(), rest.to_string()))
    }
}

fn encode_backlink_cursor(path: &str, ordinal: u32) -> String {
    // Path can't contain ASCII unit-separator (0x1F) per our path
    // policy elsewhere, so use it as a delimiter that stays
    // base64-safe-looking without bringing in a crate. Fits in
    // SQLite TEXT.
    format!("{}\x1f{}", path, ordinal)
}

fn decode_backlink_cursor(cursor: &str) -> (Option<String>, Option<i64>) {
    match cursor.split_once('\x1f') {
        Some((p, ord)) => (Some(p.to_string()), ord.parse::<i64>().ok()),
        // Malformed cursor — fall through as if there were no cursor.
        // Better degraded paging than 500ing the caller.
        None => (None, None),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn snippet_around_centers_on_span_with_radius() {
        let text = "before before before before before [[Target]] after after after after after";
        let span_start = text.find("[[").unwrap();
        let span_end = text.find("]]").unwrap() + 2;
        let s = snippet_around(text, span_start, span_end);
        assert!(s.contains("[[Target]]"));
        // Snippet is no longer than 2 * RADIUS + (span_end - span_start).
        assert!(s.len() <= 60 + 60 + (span_end - span_start), "got {:?}", s);
    }

    #[test]
    fn snippet_collapses_newlines_to_spaces() {
        let text = "intro line\n[[Target]]\ntrailing line";
        let span_start = text.find("[[").unwrap();
        let span_end = text.find("]]").unwrap() + 2;
        let s = snippet_around(text, span_start, span_end);
        assert!(!s.contains('\n'), "newlines should collapse, got {:?}", s);
        assert!(s.contains("[[Target]]"));
    }

    #[test]
    fn snippet_respects_utf8_boundaries() {
        // Multibyte char before/after the span — naive byte slicing
        // would split it and produce invalid UTF-8. The boundary
        // rounding prevents that.
        let text = "プレフィックス [[Target]] サフィックス";
        let span_start = text.find("[[").unwrap();
        let span_end = text.find("]]").unwrap() + 2;
        let s = snippet_around(text, span_start, span_end);
        assert!(s.contains("[[Target]]"));
        // The fact that snippet_around returned a String at all means
        // we didn't panic on a non-boundary cut; assert the surrounding
        // chars survived intact.
        assert!(s.contains('プ') || s.contains('サ'));
    }

    #[test]
    fn anchor_round_trip_through_storage_form() {
        let h = Some(LinkAnchor::Heading("Intro".to_string()));
        let serialized = serialize_anchor(h.as_ref());
        assert_eq!(serialized.as_deref(), Some("h:Intro"));
        assert_eq!(deserialize_anchor(serialized.as_deref()), h);

        let b = Some(LinkAnchor::Block("abc".to_string()));
        let serialized = serialize_anchor(b.as_ref());
        assert_eq!(serialized.as_deref(), Some("b:abc"));
        assert_eq!(deserialize_anchor(serialized.as_deref()), b);

        assert!(deserialize_anchor(None).is_none());
        assert!(deserialize_anchor(Some("garbage")).is_none());
    }

    #[test]
    fn cursor_round_trip_handles_subpaths_with_dashes() {
        let encoded = encode_backlink_cursor("notes/sub-folder/file.md", 7);
        let (path, ord) = decode_backlink_cursor(&encoded);
        assert_eq!(path.as_deref(), Some("notes/sub-folder/file.md"));
        assert_eq!(ord, Some(7));
    }

    #[test]
    fn cursor_decode_tolerates_malformed_input() {
        let (path, ord) = decode_backlink_cursor("no-separator-here");
        assert!(path.is_none());
        assert!(ord.is_none());
    }
}
