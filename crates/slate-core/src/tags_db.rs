// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite write/read path for the per-file native and DQL tag projections.
//!
//! Native schema lives in `migrations/019_file_tags.sql`; Dataview's ordered,
//! case-preserving projection lives in `migrations/024_dql_file_tags.sql`.
//! The native scope query remains in [`crate::search_db`] so its planning stays
//! visible alongside the FTS query it intersects with. This module also owns
//! the narrow DQL tag loader because row order is part of that contract.
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
//! Both row sets are scanner-managed: rebuilt wholesale per file on the slow
//! path, DELETE-then-INSERT keyed by `file_id`, mirroring
//! `properties_db::replace_properties_for_file`. They are derived together so
//! one parse and one transaction produce a coherent snapshot.

use rusqlite::{Connection, Transaction, params};
use std::collections::{BTreeSet, HashSet};
use yaml_rust2::Yaml;

use crate::VaultError;
use crate::editor_spans::{EditorSpanKind, highlight_spans};
use crate::frontmatter::{PropertyValue, extract_frontmatter_with_root};

/// Atomically replace both tag projections for `file_id` from one parse of
/// `markdown_source`. Called by the scanner's slow path.
///
/// An empty source clears the file's rows (the DELETE runs, the INSERT
/// loop is skipped) — the canonical "purge but keep the files row"
/// idiom used by the delete/large-file path.
pub(crate) fn replace_tags_for_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    // The native and DQL projections intentionally have different value
    // contracts, but they must be derived from the same parser pass and land
    // in the same transaction. That keeps one edit from exposing mixed-era
    // tag state to either query surface.
    let projections = collect_tag_projections(markdown_source);

    tx.execute("DELETE FROM file_tags WHERE file_id = ?1", params![file_id])?;
    tx.execute(
        "DELETE FROM dql_file_tags WHERE file_id = ?1",
        params![file_id],
    )?;

    // BTreeSet: dedup a tag that appears both inline and in
    // frontmatter (or twice inline) down to one row, and give the
    // INSERT a stable deterministic order (nice for tests; the query
    // side never relies on row order).
    if !projections.native.is_empty() {
        let mut stmt =
            tx.prepare_cached("INSERT INTO file_tags (file_id, tag_norm) VALUES (?1, ?2)")?;
        for tag in &projections.native {
            stmt.execute(params![file_id, tag])?;
        }
    }

    if !projections.dql_raw.is_empty() {
        let mut stmt = tx.prepare_cached(
            "INSERT INTO dql_file_tags (file_id, ordinal, tag_raw) VALUES (?1, ?2, ?3)",
        )?;
        for (ordinal, tag) in projections.dql_raw.iter().enumerate() {
            stmt.execute(params![file_id, ordinal as i64, tag])?;
        }
    }
    Ok(())
}

/// Load Dataview's explicit (non-parent-expanded) tag sequence for one file.
///
/// The rows are authored-order data, not a membership set: callers must keep
/// the `ordinal` ordering. Missing files and files without tags both return an
/// empty vector.
pub(crate) fn load_dql_tags_for_path(
    conn: &Connection,
    path: &str,
) -> Result<Vec<String>, VaultError> {
    let mut stmt = conn.prepare_cached(
        "SELECT t.tag_raw
         FROM dql_file_tags t
         JOIN files f ON f.id = t.file_id
         WHERE f.path = ?1
         ORDER BY t.ordinal ASC",
    )?;
    let rows = stmt.query_map(params![path], |row| row.get::<_, String>(0))?;
    rows.collect::<Result<Vec<_>, _>>().map_err(Into::into)
}

#[derive(Debug, Default, PartialEq, Eq)]
struct TagProjections {
    /// Existing native SearchScope::Tag contract: normalized, sorted set.
    native: BTreeSet<String>,
    /// Dataview explicit tags: case-sensitive, insertion-ordered set.
    dql_raw: Vec<String>,
}

/// Derive both tag projections with one editor-span pass and one frontmatter
/// parse. Native behavior remains byte-for-byte equivalent to migration 019;
/// the DQL projection follows Dataview's separate ordered Set contract.
fn collect_tag_projections(source: &str) -> TagProjections {
    let mut projections = TagProjections::default();
    let mut dql_seen = HashSet::new();

    // Dataview appends metadata-cache inline tags before frontmatter tags.
    // highlight_spans is source-ordered and already suppresses frontmatter,
    // code spans/fences, and comments.
    for span in highlight_spans(source) {
        if span.kind != EditorSpanKind::Tag {
            continue;
        }
        let start = span.start_byte as usize;
        let end = span.end_byte as usize;
        if end <= source.len() && start <= end {
            let raw = &source[start..end];
            if let Some(tag) = normalize_tag(raw) {
                projections.native.insert(tag);
            }
            push_dql_tag_tokens(raw, &mut projections.dql_raw, &mut dql_seen);
        }
    }

    // Parse frontmatter once. Native behavior deliberately accepts every
    // TagList, including a hash-prefixed list under another key. DQL is
    // narrower: Dataview recognizes only case-insensitive `tag` / `tags`,
    // then recursively tokenizes scalar values on commas and whitespace.
    let (props, _warnings, _) = extract_frontmatter_with_root(source, |root| {
        push_dql_frontmatter_root(root, &mut projections.dql_raw, &mut dql_seen);
    });
    for prop in props {
        if let PropertyValue::TagList(tags) = &prop.value {
            for tag in tags {
                if let Some(tag) = normalize_tag(tag) {
                    projections.native.insert(tag);
                }
            }
        }
    }

    projections
}

fn push_dql_frontmatter_value(value: &Yaml, out: &mut Vec<String>, seen: &mut HashSet<String>) {
    match value {
        Yaml::Array(values) => {
            for value in values {
                push_dql_frontmatter_value(value, out, seen);
            }
        }
        Yaml::String(value) if !value.is_empty() => push_dql_tag_tokens(value, out, seen),
        Yaml::Integer(value) if *value != 0 => {
            push_dql_tag_tokens(&value.to_string(), out, seen);
        }
        Yaml::Real(value) => {
            if let Ok(number) = value.parse::<f64>()
                && number != 0.0
                && !number.is_nan()
            {
                push_dql_tag_tokens(&number.to_string(), out, seen);
            }
        }
        Yaml::Boolean(true) => push_dql_tag_tokens("true", out, seen),
        // Dataview filters falsy array/scalar values before coercion. YAML
        // objects and unresolved aliases are not scalar tag inputs.
        Yaml::String(_)
        | Yaml::Integer(_)
        | Yaml::Boolean(false)
        | Yaml::Null
        | Yaml::BadValue
        | Yaml::Hash(_)
        | Yaml::Alias(_) => {}
    }
}

fn push_dql_frontmatter_root(root: &Yaml, out: &mut Vec<String>, seen: &mut HashSet<String>) {
    let Yaml::Hash(map) = root else {
        return;
    };
    for (key, value) in map {
        let Yaml::String(key) = key else {
            continue;
        };
        if key.eq_ignore_ascii_case("tag") || key.eq_ignore_ascii_case("tags") {
            push_dql_frontmatter_value(value, out, seen);
        }
    }
}

fn push_dql_tag_tokens(raw: &str, out: &mut Vec<String>, seen: &mut HashSet<String>) {
    for token in raw.split(|ch: char| ch == ',' || ch.is_whitespace()) {
        let token = token
            .trim()
            .strip_prefix('#')
            .unwrap_or(token.trim())
            .trim();
        if !token.is_empty() && seen.insert(token.to_string()) {
            out.push(token.to_string());
        }
    }
}

#[cfg(test)]
fn collect_raw_tags(source: &str) -> Vec<String> {
    collect_tag_projections(source).dql_raw
}

#[cfg(test)]
fn collect_tags(source: &str) -> BTreeSet<String> {
    collect_tag_projections(source).native
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

    fn raw_tags_for(source: &str) -> Vec<String> {
        collect_raw_tags(source)
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
    fn dql_tags_keep_inline_order_case_and_exact_first_occurrence() {
        let src = "---\ntags: [Front, front, '#Body', A/B]\n---\n#Body #Case #case #A/B #Body\n";
        assert_eq!(
            raw_tags_for(src),
            vec!["Body", "Case", "case", "A/B", "Front", "front"]
        );
    }

    #[test]
    fn dql_tags_accept_tag_and_tags_scalar_and_numeric_forms() {
        let src = "---\nTAG: Alpha, Beta gamma\ntags: 42\n---\n#Inline\n";
        assert_eq!(
            raw_tags_for(src),
            vec!["Inline", "Alpha", "Beta", "gamma", "42"]
        );
    }

    #[test]
    fn dql_tags_flatten_nested_frontmatter_arrays_and_skip_falsy_values() {
        let src = "---\ntags: [[A, B], [C, [D]], 0, false, null, '', E F, '0']\n---\n";
        assert_eq!(raw_tags_for(src), vec!["A", "B", "C", "D", "E", "F", "0"]);
    }

    #[test]
    fn dql_tags_skip_falsy_scalar_frontmatter_values() {
        assert!(raw_tags_for("---\ntag: 0\n---\n").is_empty());
        assert!(raw_tags_for("---\nTAG: false\n---\n").is_empty());
        assert!(raw_tags_for("---\ntags: null\n---\n").is_empty());
    }

    #[test]
    fn dql_tags_ignore_hash_lists_under_unrelated_frontmatter_keys() {
        let src = "---\ncategories: ['#not-dql']\ntags: [Real]\n---\n";
        assert_eq!(raw_tags_for(src), vec!["Real"]);

        // This is intentionally the pre-existing native behavior. Migration
        // 024 must not change the normalized SearchScope::Tag projection.
        assert_eq!(tags_for(src), vec!["not-dql", "real"]);
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

    #[test]
    fn replace_rebuilds_native_and_dql_rows_together() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        let src = "---\ntags: [Front, '#Inline']\n---\n#Inline #Case #case\n";
        write_tags(&conn, 1, src);

        assert_eq!(rows_for(&conn, 1), vec!["case", "front", "inline"]);
        assert_eq!(
            raw_rows_for(&conn, 1),
            vec!["Inline", "Case", "case", "Front"]
        );
        assert_eq!(
            load_dql_tags_for_path(&conn, "n1.md").unwrap(),
            vec!["Inline", "Case", "case", "Front"]
        );
        assert!(
            load_dql_tags_for_path(&conn, "missing.md")
                .unwrap()
                .is_empty()
        );
    }

    #[test]
    fn replace_reindexes_and_purges_both_tag_projections() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_tags(&conn, 1, "#Old #old\n");
        assert_eq!(rows_for(&conn, 1), vec!["old"]);
        assert_eq!(raw_rows_for(&conn, 1), vec!["Old", "old"]);

        write_tags(&conn, 1, "---\ntag: New\n---\n#Fresh\n");
        assert_eq!(rows_for(&conn, 1), vec!["fresh"]);
        assert_eq!(raw_rows_for(&conn, 1), vec!["Fresh", "New"]);

        write_tags(&conn, 1, "");
        assert!(rows_for(&conn, 1).is_empty());
        assert!(raw_rows_for(&conn, 1).is_empty());
    }

    #[test]
    fn file_delete_cascades_both_tag_projections() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_tags(&conn, 1, "#One #Two\n");

        conn.execute("DELETE FROM files WHERE id = 1", []).unwrap();

        assert!(rows_for(&conn, 1).is_empty());
        assert!(raw_rows_for(&conn, 1).is_empty());
    }

    #[test]
    fn failed_dual_projection_rebuild_rolls_back_as_one_transaction() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_tags(&conn, 1, "#Old\n");
        conn.execute_batch(
            "CREATE TRIGGER fail_dql_tag_insert
             BEFORE INSERT ON dql_file_tags
             WHEN NEW.tag_raw = 'boom'
             BEGIN
               SELECT RAISE(ABORT, 'forced raw-tag failure');
             END;",
        )
        .unwrap();

        let tx = conn.unchecked_transaction().unwrap();
        let error = replace_tags_for_file(&tx, 1, "#New #boom\n").unwrap_err();
        assert!(error.to_string().contains("forced raw-tag failure"));
        tx.rollback().unwrap();

        assert_eq!(rows_for(&conn, 1), vec!["old"]);
        assert_eq!(raw_rows_for(&conn, 1), vec!["Old"]);
    }

    // --- helpers ---

    fn migrated_conn() -> Connection {
        let mut conn = crate::db::open_in_memory(512).unwrap();
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

    fn raw_rows_for(conn: &Connection, id: i64) -> Vec<String> {
        let mut stmt = conn
            .prepare(
                "SELECT tag_raw FROM dql_file_tags
                 WHERE file_id = ?1 ORDER BY ordinal ASC",
            )
            .unwrap();
        stmt.query_map(params![id], |r| r.get::<_, String>(0))
            .unwrap()
            .map(Result::unwrap)
            .collect()
    }
}
