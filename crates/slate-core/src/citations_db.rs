// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite storage + query surface for citation indexing.
//!
//! Two surfaces in one module:
//!
//! 1. **`file_citations`** is per-file — one row per `CitedItem`
//!    extracted from a Markdown body. Managed via the standard
//!    DELETE-by-file_id + bulk INSERT pattern shared with
//!    `links_db`, `tasks_db`, etc. Scanner-owned.
//!
//! 2. **`bibliography_entries`** is global per-vault — the merged
//!    bibliography. Rewritten in one transaction on every
//!    bibliography reload (driven by the file-watch debouncer from
//!    #276). Session-owned, not scanner-owned.
//!
//! Schema lives in `migrations/013_citations.sql`. Mode enum
//! encoding is `0=Bracketed`, `1=InText`, `2=SuppressAuthor` —
//! mirrored in `mode_to_int` / `mode_from_int`.

use rusqlite::{Connection, Transaction, params};

use crate::VaultError;
use crate::citations::bibliography::{Author, BibEntry};
use crate::citations::{CitationMode, CitationReference, CitedItem, Locator, extract_citations};

/// Extract citations from `markdown_source` and replace every cached
/// row for `source_file_id` in one transaction. Empty source body
/// (`""`) leaves the file row in place but purges its citation rows
/// — the canonical "this file is now large / binary / vanished"
/// idiom shared with the other slow-path indexers.
pub(crate) fn replace_citations_for_file(
    tx: &Transaction,
    source_file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    tx.execute(
        "DELETE FROM file_citations WHERE file_id = ?1",
        params![source_file_id],
    )?;
    if markdown_source.is_empty() {
        return Ok(());
    }
    let refs = extract_citations(markdown_source);
    if refs.is_empty() {
        return Ok(());
    }
    let mut stmt = tx.prepare(
        "INSERT INTO file_citations \
         (file_id, citation_key, locator_label, locator_text, mode, line, byte_offset) \
         VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)",
    )?;
    for r in &refs {
        for item in &r.citations {
            let (loc_label, loc_text) = match &item.locator {
                Some(l) => (Some(l.label.as_str()), Some(l.locator.as_str())),
                None => (None, None),
            };
            stmt.execute(params![
                source_file_id,
                item.key,
                loc_label,
                loc_text,
                mode_to_int(item.mode),
                r.line,
                r.byte_offset
            ])?;
        }
    }
    Ok(())
}

/// Path-based wrapper used by `VaultSession::list_citations_in_file`.
/// Returns an empty vec when the file isn't indexed yet — same
/// shape as `tasks_for_file`.
pub fn list_citations_in_file(
    conn: &Connection,
    path: &str,
) -> Result<Vec<CitationReference>, VaultError> {
    let file_id: Option<i64> = match conn.query_row(
        "SELECT id FROM files WHERE path = ?1",
        params![path],
        |row| row.get(0),
    ) {
        Ok(id) => Some(id),
        Err(rusqlite::Error::QueryReturnedNoRows) => None,
        Err(other) => return Err(other.into()),
    };
    let Some(file_id) = file_id else {
        return Ok(Vec::new());
    };
    list_citations_for_file(conn, file_id)
}

/// Read every `CitationReference` for `file_id` back out of the
/// index. Reconstructs the parsed shape that the renderer expects
/// without re-parsing the file body — `list_citations_in_file` is
/// the in-app callsite for this.
///
/// Rows are returned in document order (line then byte_offset). A
/// `CitationReference`'s `citations` vec contains every `CitedItem`
/// the original syntactic site contained — the index groups rows
/// by `(line, byte_offset)` since that's the on-disk identity of a
/// citation site.
pub fn list_citations_for_file(
    conn: &Connection,
    file_id: i64,
) -> Result<Vec<CitationReference>, VaultError> {
    let mut stmt = conn.prepare(
        "SELECT citation_key, locator_label, locator_text, mode, line, byte_offset \
         FROM file_citations WHERE file_id = ?1 \
         ORDER BY line, byte_offset",
    )?;
    let rows = stmt.query_map(params![file_id], |r| {
        let key: String = r.get(0)?;
        let locator_label: Option<String> = r.get(1)?;
        let locator_text: Option<String> = r.get(2)?;
        let mode_int: i64 = r.get(3)?;
        let line: u32 = r.get::<_, i64>(4)? as u32;
        let byte_offset: u32 = r.get::<_, i64>(5)? as u32;
        Ok(RawRow {
            key,
            locator_label,
            locator_text,
            mode: mode_from_int(mode_int),
            line,
            byte_offset,
        })
    })?;
    let mut by_site: std::collections::BTreeMap<(u32, u32), Vec<RawRow>> =
        std::collections::BTreeMap::new();
    for row in rows {
        let row = row?;
        by_site
            .entry((row.line, row.byte_offset))
            .or_default()
            .push(row);
    }
    let mut out = Vec::with_capacity(by_site.len());
    for ((line, byte_offset), rows) in by_site {
        let citations: Vec<CitedItem> = rows
            .into_iter()
            .map(|r| CitedItem {
                key: r.key,
                locator: match (r.locator_label, r.locator_text) {
                    (Some(label), Some(text)) => Some(Locator {
                        label,
                        locator: text,
                    }),
                    _ => None,
                },
                prefix: None,
                suffix: None,
                mode: r.mode,
            })
            .collect();
        // `raw` isn't preserved in the index (the source is the
        // markdown file itself); we reconstruct a plausible textual
        // form for downstream consumers that only need to know the
        // shape. The renderer uses `citations` + `mode`, not `raw`.
        let raw = reconstruct_raw(&citations);
        out.push(CitationReference {
            raw,
            citations,
            byte_offset,
            line,
        });
    }
    Ok(out)
}

struct RawRow {
    key: String,
    locator_label: Option<String>,
    locator_text: Option<String>,
    mode: CitationMode,
    line: u32,
    byte_offset: u32,
}

fn reconstruct_raw(items: &[CitedItem]) -> String {
    if items.is_empty() {
        return String::new();
    }
    let mode = items[0].mode;
    if items.len() == 1 && matches!(mode, CitationMode::InText) {
        return format!("@{}", items[0].key);
    }
    let inner = items
        .iter()
        .map(|item| {
            let prefix = if matches!(item.mode, CitationMode::SuppressAuthor) {
                "-"
            } else {
                ""
            };
            match &item.locator {
                Some(l) => format!("{prefix}@{}, {} {}", item.key, l.label, l.locator),
                None => format!("{prefix}@{}", item.key),
            }
        })
        .collect::<Vec<_>>()
        .join("; ");
    format!("[{inner}]")
}

pub(crate) fn mode_to_int(mode: CitationMode) -> i64 {
    match mode {
        CitationMode::Bracketed => 0,
        CitationMode::InText => 1,
        CitationMode::SuppressAuthor => 2,
    }
}

pub(crate) fn mode_from_int(n: i64) -> CitationMode {
    match n {
        1 => CitationMode::InText,
        2 => CitationMode::SuppressAuthor,
        _ => CitationMode::Bracketed,
    }
}

// =====================================================================
// bibliography_entries — global per-vault table
// =====================================================================

/// Replace every row in `bibliography_entries` with `entries`, in one
/// transaction. Source path is recorded per-entry so the UI can
/// surface "this came from `<path>`" when needed.
pub fn replace_bibliography_entries(
    conn: &mut Connection,
    entries: &[BibEntry],
    source_path_for_key: &dyn Fn(&str) -> String,
    now_ms: i64,
) -> Result<(), VaultError> {
    let tx = conn.transaction()?;
    tx.execute("DELETE FROM bibliography_entries", [])?;
    {
        let mut stmt = tx.prepare(
            "INSERT INTO bibliography_entries \
             (key, item_type, title, authors_json, year, journal, doi, url, publisher, \
              raw_csl_json, source_path, last_updated_ms) \
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12)",
        )?;
        for entry in entries {
            let authors_json = serialize_authors(&entry.authors);
            let source_path: String = source_path_for_key(&entry.key);
            stmt.execute(params![
                entry.key,
                entry.item_type,
                entry.title,
                authors_json,
                entry.year,
                entry.journal,
                entry.doi,
                entry.url,
                entry.publisher,
                entry.raw_csl_json,
                source_path,
                now_ms,
            ])?;
        }
    }
    tx.commit()?;
    Ok(())
}

/// Serialise the author list to a CSL-JSON-shaped array. Stored in
/// `bibliography_entries.authors_json` for fast bibliography-view
/// rendering without re-parsing `raw_csl_json`.
fn serialize_authors(authors: &[Author]) -> String {
    let arr: Vec<serde_json::Value> = authors
        .iter()
        .map(|a| {
            serde_json::json!({
                "family": a.family,
                "given": a.given,
            })
        })
        .collect();
    serde_json::to_string(&arr).unwrap_or_else(|_| "[]".to_string())
}

/// Fetch every bibliography entry from the cache, ordered by year
/// desc then title asc — the bibliography view's default sort.
pub fn list_bibliography_entries(conn: &Connection) -> Result<Vec<BibEntry>, VaultError> {
    let mut stmt = conn.prepare(
        "SELECT key, item_type, title, authors_json, year, journal, doi, url, publisher, \
                raw_csl_json \
         FROM bibliography_entries \
         ORDER BY year DESC, title ASC",
    )?;
    let rows = stmt.query_map([], row_to_entry)?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r?);
    }
    Ok(out)
}

/// Fetch one bibliography entry by key.
pub fn get_bibliography_entry(
    conn: &Connection,
    key: &str,
) -> Result<Option<BibEntry>, VaultError> {
    let mut stmt = conn.prepare(
        "SELECT key, item_type, title, authors_json, year, journal, doi, url, publisher, \
                raw_csl_json \
         FROM bibliography_entries WHERE key = ?1",
    )?;
    let mut rows = stmt.query_map(params![key], row_to_entry)?;
    // `transpose()` turns `Option<Result<_>>` into `Result<Option<_>>`
    // so `?` surfaces a row-decode error. Written without `if let` to
    // sidestep the Rust 2024 if-let temporary-scope rescope: `rows`
    // borrows `stmt`, and the explicit form keeps the drop timing
    // obvious regardless of edition.
    Ok(rows.next().transpose()?)
}

/// Case-insensitive substring search on title + authors_json. No
/// FTS5 in V1 — bibliographies are small (<10k entries typically).
pub fn search_bibliography(conn: &Connection, query: &str) -> Result<Vec<BibEntry>, VaultError> {
    let needle = format!("%{}%", query.to_lowercase());
    let mut stmt = conn.prepare(
        "SELECT key, item_type, title, authors_json, year, journal, doi, url, publisher, \
                raw_csl_json \
         FROM bibliography_entries \
         WHERE lower(title) LIKE ?1 OR lower(authors_json) LIKE ?1 \
         ORDER BY year DESC, title ASC",
    )?;
    let rows = stmt.query_map(params![needle], row_to_entry)?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r?);
    }
    Ok(out)
}

fn row_to_entry(r: &rusqlite::Row<'_>) -> rusqlite::Result<BibEntry> {
    let authors_json: Option<String> = r.get(3)?;
    let authors = authors_from_json(authors_json.as_deref());
    Ok(BibEntry {
        key: r.get(0)?,
        item_type: r.get(1)?,
        title: r.get::<_, Option<String>>(2)?.unwrap_or_default(),
        authors,
        year: r.get::<_, Option<i64>>(4)?.map(|y| y as i32),
        journal: r.get(5)?,
        doi: r.get(6)?,
        url: r.get(7)?,
        publisher: r.get(8)?,
        abstract_text: None,
        raw_csl_json: r.get(9)?,
    })
}

fn authors_from_json(s: Option<&str>) -> Vec<Author> {
    let Some(s) = s else {
        return Vec::new();
    };
    let parsed: serde_json::Result<Vec<serde_json::Value>> = serde_json::from_str(s);
    match parsed {
        Ok(arr) => arr
            .into_iter()
            .filter_map(|v| {
                let o = v.as_object()?;
                let family = o.get("family")?.as_str()?.to_string();
                let given = o.get("given").and_then(|g| g.as_str()).map(str::to_string);
                Some(Author { family, given })
            })
            .collect(),
        Err(_) => Vec::new(),
    }
}

// =====================================================================
// Cross-table queries
// =====================================================================

/// Return one row per file that cites `citation_key`, plus the
/// citation row's line + byte_offset. Used by `list_files_citing`.
pub fn list_files_citing(
    conn: &Connection,
    citation_key: &str,
) -> Result<Vec<FileCiting>, VaultError> {
    let mut stmt = conn.prepare(
        "SELECT DISTINCT f.path \
         FROM file_citations c JOIN files f ON f.id = c.file_id \
         WHERE c.citation_key = ?1 \
         ORDER BY f.path",
    )?;
    let rows = stmt.query_map(params![citation_key], |r| {
        Ok(FileCiting { path: r.get(0)? })
    })?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r?);
    }
    Ok(out)
}

/// Lightweight "this file cites X" row. Distinct from
/// `crate::session::FileSummary` because the renderer doesn't need
/// the full file metadata — just the path. The session-layer
/// `list_files_citing` upgrades these to `FileSummary` via the
/// existing per-file getter.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct FileCiting {
    pub path: String,
}

/// Return every `(file_path, citation_key)` pair where the cited
/// key has no matching `bibliography_entries` row — i.e. citations
/// pointing at a key the user hasn't yet added to the bibliography.
///
/// Returned distinct on `(file_path, citation_key)` (a file citing
/// the same missing key three times appears once).
pub fn list_unresolved_citations(conn: &Connection) -> Result<Vec<(String, String)>, VaultError> {
    let mut stmt = conn.prepare(
        "SELECT DISTINCT f.path, c.citation_key \
         FROM file_citations c \
         JOIN files f ON f.id = c.file_id \
         LEFT JOIN bibliography_entries b ON b.key = c.citation_key \
         WHERE b.key IS NULL \
         ORDER BY f.path, c.citation_key",
    )?;
    let rows = stmt.query_map([], |r| {
        let path: String = r.get(0)?;
        let key: String = r.get(1)?;
        Ok((path, key))
    })?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r?);
    }
    Ok(out)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::db::{migrate, open_in_memory};
    use rusqlite::Connection;

    fn open_test_db() -> Connection {
        let mut conn = open_in_memory(64).unwrap();
        migrate(&mut conn).unwrap();
        conn
    }

    fn insert_file(conn: &Connection, path: &str) -> i64 {
        let name = path.rsplit('/').next().unwrap_or(path);
        conn.execute(
            "INSERT INTO files (path, name, size_bytes, mtime_ms, content_hash, parser_version, indexed_at_ms, is_markdown) \
             VALUES (?1, ?2, 0, 0, '', 1, 0, 1)",
            params![path, name],
        )
        .unwrap();
        conn.last_insert_rowid()
    }

    #[test]
    fn replace_citations_for_file_writes_rows_in_order() {
        let mut conn = open_test_db();
        let file_id = insert_file(&conn, "test.md");
        let body = "First [@a, p. 1] and [@b; @c]. Also @d.";
        let tx = conn.transaction().unwrap();
        replace_citations_for_file(&tx, file_id, body).unwrap();
        tx.commit().unwrap();
        let refs = list_citations_for_file(&conn, file_id).unwrap();
        // 3 sites: [@a, p. 1] (1 item), [@b; @c] (2 items), @d (1 item).
        assert_eq!(refs.len(), 3);
        assert_eq!(refs[0].citations[0].key, "a");
        assert_eq!(refs[0].citations[0].locator.as_ref().unwrap().label, "p.");
        assert_eq!(refs[1].citations.len(), 2);
        assert_eq!(refs[2].citations[0].key, "d");
        assert_eq!(refs[2].citations[0].mode, CitationMode::InText);
    }

    #[test]
    fn replace_citations_for_file_is_idempotent_across_rewrites() {
        let mut conn = open_test_db();
        let file_id = insert_file(&conn, "test.md");
        let tx = conn.transaction().unwrap();
        replace_citations_for_file(&tx, file_id, "[@a]").unwrap();
        replace_citations_for_file(&tx, file_id, "[@a]").unwrap();
        tx.commit().unwrap();
        let refs = list_citations_for_file(&conn, file_id).unwrap();
        assert_eq!(refs.len(), 1);
    }

    #[test]
    fn replace_citations_for_file_empty_body_purges_rows() {
        let mut conn = open_test_db();
        let file_id = insert_file(&conn, "test.md");
        let tx = conn.transaction().unwrap();
        replace_citations_for_file(&tx, file_id, "[@a; @b]").unwrap();
        tx.commit().unwrap();
        assert_eq!(list_citations_for_file(&conn, file_id).unwrap().len(), 1);
        let tx = conn.transaction().unwrap();
        replace_citations_for_file(&tx, file_id, "").unwrap();
        tx.commit().unwrap();
        assert!(list_citations_for_file(&conn, file_id).unwrap().is_empty());
    }

    fn mk_entry(key: &str, family: &str, year: i32) -> BibEntry {
        BibEntry {
            key: key.to_string(),
            item_type: "article-journal".to_string(),
            title: format!("Title for {key}"),
            authors: vec![Author {
                family: family.to_string(),
                given: None,
            }],
            year: Some(year),
            journal: None,
            doi: None,
            url: None,
            publisher: None,
            abstract_text: None,
            raw_csl_json: format!("{{\"id\":\"{key}\"}}"),
        }
    }

    #[test]
    fn replace_bibliography_entries_round_trips() {
        let mut conn = open_test_db();
        let entries = vec![
            mk_entry("a", "Anderson", 2020),
            mk_entry("b", "Brown", 2019),
        ];
        let source = |_: &str| "library.bib".to_string();
        replace_bibliography_entries(&mut conn, &entries, &source, 12345).unwrap();
        let listed = list_bibliography_entries(&conn).unwrap();
        // Sort is year desc then title asc.
        assert_eq!(listed.len(), 2);
        assert_eq!(listed[0].key, "a"); // 2020 first
        assert_eq!(listed[1].key, "b");
        let one = get_bibliography_entry(&conn, "a").unwrap().unwrap();
        assert_eq!(one.title, "Title for a");
        assert_eq!(one.authors.len(), 1);
        assert_eq!(one.authors[0].family, "Anderson");
    }

    #[test]
    fn replace_bibliography_entries_rewrites_whole_table() {
        let mut conn = open_test_db();
        let entries_v1 = vec![mk_entry("a", "A", 2020), mk_entry("b", "B", 2019)];
        let source = |_: &str| "x.bib".to_string();
        replace_bibliography_entries(&mut conn, &entries_v1, &source, 1).unwrap();
        let entries_v2 = vec![mk_entry("c", "C", 2021)];
        replace_bibliography_entries(&mut conn, &entries_v2, &source, 2).unwrap();
        let listed = list_bibliography_entries(&conn).unwrap();
        // Old `a` + `b` gone; only `c` survives.
        let keys: Vec<&str> = listed.iter().map(|e| e.key.as_str()).collect();
        assert_eq!(keys, vec!["c"]);
    }

    #[test]
    fn search_bibliography_matches_title_case_insensitive() {
        let mut conn = open_test_db();
        let entries = vec![
            mk_entry("smith2020", "Smith", 2020),
            mk_entry("jones2019", "Jones", 2019),
        ];
        let source = |_: &str| "x.bib".to_string();
        replace_bibliography_entries(&mut conn, &entries, &source, 0).unwrap();
        // Title contains the key — search for "for smith" (case-insensitive)
        let hits = search_bibliography(&conn, "FOR SMITH").unwrap();
        assert_eq!(hits.len(), 1);
        assert_eq!(hits[0].key, "smith2020");
    }

    #[test]
    fn search_bibliography_matches_author_family() {
        let mut conn = open_test_db();
        let entries = vec![
            mk_entry("smith2020", "Smith", 2020),
            mk_entry("jones2019", "Jones", 2019),
        ];
        let source = |_: &str| "x.bib".to_string();
        replace_bibliography_entries(&mut conn, &entries, &source, 0).unwrap();
        let hits = search_bibliography(&conn, "jones").unwrap();
        assert_eq!(hits.len(), 1);
        assert_eq!(hits[0].key, "jones2019");
    }

    #[test]
    fn list_files_citing_returns_distinct_paths() {
        let mut conn = open_test_db();
        let f1 = insert_file(&conn, "a.md");
        let f2 = insert_file(&conn, "b.md");
        let tx = conn.transaction().unwrap();
        replace_citations_for_file(&tx, f1, "[@smith2020; @smith2020]").unwrap(); // dup key
        replace_citations_for_file(&tx, f2, "[@smith2020]").unwrap();
        replace_citations_for_file(&tx, f2, "[@smith2020] and [@jones2019]").unwrap();
        tx.commit().unwrap();
        let citing = list_files_citing(&conn, "smith2020").unwrap();
        let paths: Vec<&str> = citing.iter().map(|f| f.path.as_str()).collect();
        assert_eq!(paths, vec!["a.md", "b.md"]);
    }

    #[test]
    fn list_unresolved_citations_skips_resolved_keys() {
        let mut conn = open_test_db();
        let file_id = insert_file(&conn, "test.md");
        let tx = conn.transaction().unwrap();
        replace_citations_for_file(&tx, file_id, "[@known; @missing]").unwrap();
        tx.commit().unwrap();
        let entries = vec![mk_entry("known", "Known", 2020)];
        let source = |_: &str| "x.bib".to_string();
        replace_bibliography_entries(&mut conn, &entries, &source, 0).unwrap();
        let unresolved = list_unresolved_citations(&conn).unwrap();
        assert_eq!(unresolved.len(), 1);
        assert_eq!(unresolved[0].0, "test.md");
        assert_eq!(unresolved[0].1, "missing");
    }
}
