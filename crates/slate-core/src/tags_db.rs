// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite write path for the per-file tag dimension (`file_tags`).
//!
//! Schema lives in `migrations/019_file_tags.sql`. This module owns
//! only the WRITE side; the scope query is inlined in
//! [`crate::search_db`] so its planning stays visible alongside the
//! FTS query it intersects with.
//!
//! ## Two tag dimensions, one honest union
//!
//! A note carries tags in two places and `SearchScope::Tag` must see
//! both (the reading view activates INLINE tags, so a frontmatter-only
//! index would miss inline-only notes entirely):
//!
//!   a) INLINE body `#tag`s.
//!   b) FRONTMATTER `tags:` list values.
//!
//! ## Reuse, never re-derive
//!
//! The inline set comes from [`crate::editor_spans::highlight_spans`]
//! filtered to [`EditorSpanKind::Tag`] — NOT from the raw tag scanner.
//! `highlight_spans` runs the overlap sweep that suppresses `#tag`
//! tokens inside code fences, inline code, `%%comments%%`, and
//! frontmatter. Calling the scanner directly would reintroduce a
//! second classifier that skips that suppression, so a `#tag` written
//! inside a code block would leak into search. The frontmatter set
//! comes from [`crate::frontmatter::extract_frontmatter`], the same
//! extraction `properties_db` uses.
//!
//! Rows are scanner-managed: rebuilt wholesale per file on the slow
//! path, DELETE-then-INSERT keyed by `file_id`, mirroring
//! `properties_db::replace_properties_for_file`.

use rusqlite::{Transaction, params};
use std::collections::BTreeSet;

use crate::VaultError;
use crate::editor_spans::{EditorSpanKind, highlight_spans};
use crate::frontmatter::{PropertyValue, extract_frontmatter};

/// Atomically replace the tag rows for `file_id` with the distinct
/// normalized tag set parsed from `markdown_source` (inline `#tag`s +
/// frontmatter `tags:` values). Called by the scanner's slow path.
///
/// An empty source clears the file's rows (the DELETE runs, the INSERT
/// loop is skipped) — the canonical "purge but keep the files row"
/// idiom used by the delete/large-file path.
pub(crate) fn replace_tags_for_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    tx.execute("DELETE FROM file_tags WHERE file_id = ?1", params![file_id])?;

    // BTreeSet: dedup a tag that appears both inline and in
    // frontmatter (or twice inline) down to one row, and give the
    // INSERT a stable deterministic order (nice for tests; the query
    // side never relies on row order).
    let tags = collect_tags(markdown_source);
    if tags.is_empty() {
        return Ok(());
    }
    let mut stmt =
        tx.prepare_cached("INSERT INTO file_tags (file_id, tag_norm) VALUES (?1, ?2)")?;
    for tag in &tags {
        stmt.execute(params![file_id, tag])?;
    }
    Ok(())
}

/// The distinct, normalized tag set for `source` — the union of the
/// suppression-aware inline scan and the frontmatter `tags:` list.
fn collect_tags(source: &str) -> BTreeSet<String> {
    let mut out = BTreeSet::new();

    // (a) INLINE. highlight_spans already suppresses tags inside code
    // / comments / frontmatter via its overlap sweep; the Tag span
    // covers the leading `#` (e.g. `#project`), so strip it.
    for span in highlight_spans(source) {
        if span.kind != EditorSpanKind::Tag {
            continue;
        }
        let start = span.start_byte as usize;
        let end = span.end_byte as usize;
        // Defensive: a span whose bytes fall outside the source (never
        // expected — highlight_spans derives them from this same
        // source) is skipped rather than panicking on a bad slice.
        if end > source.len() || start > end {
            continue;
        }
        if let Some(tag) = normalize_tag(&source[start..end]) {
            out.insert(tag);
        }
    }

    // (b) FRONTMATTER. Only the `tags:` key produces a TagList today,
    // and its elements already arrive `#`-stripped from
    // `frontmatter::classify_list`; normalize_tag re-strips defensively
    // and lowercases to match the inline path.
    let (props, _warnings) = extract_frontmatter(source);
    for prop in props {
        if let PropertyValue::TagList(tags) = prop.value {
            for tag in tags {
                if let Some(tag) = normalize_tag(&tag) {
                    out.insert(tag);
                }
            }
        }
    }

    out
}

/// Normalize a raw tag token to its stored form: trim, strip one
/// leading `#`, then Unicode-lowercase. Returns `None` for an empty
/// result so blank tags never reach the table.
///
/// Lowercasing uses Rust's `to_lowercase` (full Unicode fold) to match
/// `properties_db`'s `properties_list_values` normalization exactly —
/// a scope query lowercases the caller's tag the same way, so
/// `#Project` and a scope of `project` meet in the middle.
pub(crate) fn normalize_tag(raw: &str) -> Option<String> {
    let trimmed = raw.trim();
    let stripped = trimmed.strip_prefix('#').unwrap_or(trimmed);
    let norm = stripped.to_lowercase();
    if norm.is_empty() { None } else { Some(norm) }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::db::migrate;
    use rusqlite::Connection;

    fn tags_for(source: &str) -> Vec<String> {
        collect_tags(source).into_iter().collect()
    }

    #[test]
    fn normalize_strips_hash_and_lowercases() {
        assert_eq!(normalize_tag("#Project").as_deref(), Some("project"));
        assert_eq!(normalize_tag("  #a/b  ").as_deref(), Some("a/b"));
        assert_eq!(normalize_tag("bare").as_deref(), Some("bare"));
    }

    #[test]
    fn normalize_rejects_empty() {
        assert_eq!(normalize_tag(""), None);
        assert_eq!(normalize_tag("#"), None);
        assert_eq!(normalize_tag("   "), None);
    }

    #[test]
    fn normalize_folds_full_unicode() {
        // Matches properties_list_values' Rust to_lowercase (not SQL's
        // ASCII-only lower), so a frontmatter `PROJET` and inline
        // `#Projet` collapse to one row.
        assert_eq!(normalize_tag("#CAFÉ").as_deref(), Some("café"));
    }

    #[test]
    fn collects_inline_tags() {
        let tags = tags_for("intro #alpha and #beta here\n");
        assert_eq!(tags, vec!["alpha", "beta"]);
    }

    #[test]
    fn collects_frontmatter_tags() {
        let src = "---\ntags: [science, math]\n---\nbody\n";
        assert_eq!(tags_for(src), vec!["math", "science"]);
    }

    #[test]
    fn unions_and_dedups_inline_plus_frontmatter() {
        // #alpha appears both inline and in frontmatter → one row.
        let src = "---\ntags: [alpha, gamma]\n---\nbody #alpha #beta\n";
        assert_eq!(tags_for(src), vec!["alpha", "beta", "gamma"]);
    }

    #[test]
    fn suppresses_tags_in_code_and_comments() {
        // The highlight_spans overlap sweep masks these; collecting via
        // that path (not the raw scanner) inherits the suppression.
        let src = "```\n#nottag\n```\n%% #alsonot %%\n`#inlinecode` #real\n";
        assert_eq!(tags_for(src), vec!["real"]);
    }

    #[test]
    fn nested_tag_stored_verbatim() {
        // The slash form is preserved; the scope query does the
        // prefix matching, not the writer.
        assert_eq!(tags_for("see #area/subarea\n"), vec!["area/subarea"]);
    }

    #[test]
    fn replace_clears_on_empty_source() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_tags(&conn, 1, "body #keep\n");
        assert_eq!(rows_for(&conn, 1), vec!["keep"]);
        write_tags(&conn, 1, "");
        assert!(rows_for(&conn, 1).is_empty());
    }

    #[test]
    fn replace_updates_on_change() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_tags(&conn, 1, "body #old\n");
        assert_eq!(rows_for(&conn, 1), vec!["old"]);
        write_tags(&conn, 1, "body #new\n");
        assert_eq!(rows_for(&conn, 1), vec!["new"]);
    }

    // --- helpers ---

    fn migrated_conn() -> Connection {
        let mut conn = Connection::open_in_memory().unwrap();
        migrate(&mut conn).unwrap();
        conn
    }

    fn seed_file(conn: &Connection, id: i64) {
        conn.execute(
            "INSERT INTO files (id, path, name, extension, mtime_ms, ctime_ms, size_bytes,
                 content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
             VALUES (?1, ?2, ?2, 'md', 0, 0, 0, '', 1, 0, 1, '')",
            params![id, format!("n{id}.md")],
        )
        .unwrap();
    }

    fn write_tags(conn: &Connection, id: i64, source: &str) {
        let tx = conn.unchecked_transaction().unwrap();
        replace_tags_for_file(&tx, id, source).unwrap();
        tx.commit().unwrap();
    }

    fn rows_for(conn: &Connection, id: i64) -> Vec<String> {
        let mut stmt = conn
            .prepare("SELECT tag_norm FROM file_tags WHERE file_id = ?1 ORDER BY tag_norm")
            .unwrap();
        stmt.query_map(params![id], |r| r.get::<_, String>(0))
            .unwrap()
            .map(Result::unwrap)
            .collect()
    }
}
