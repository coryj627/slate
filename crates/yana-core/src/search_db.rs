//! Full-text search over the `files_fts` virtual table.
//!
//! Exposes a single entry point — `full_text_search` — that runs an
//! FTS5 MATCH query, snippets each hit, and returns a result-set
//! shape that matches `docs/plans/05` §8.4 so #58's UI and the
//! future Bases / query-engine layer can both consume it without
//! adapter shims.
//!
//! ## Line numbers — deferred to the UI
//!
//! `QueryHit` deliberately does NOT carry a `line_number`. The
//! previous shape selected `files.body_text` for every hit just so
//! the Rust side could scan it for a query-token offset and emit a
//! 1-based line — for a 10MB note matching a broad term, that was
//! ~10MB pulled through SQLite plus a `to_lowercase()` allocation
//! per row (#92 item 1). The body never needed to make the trip:
//! the line is only consumed by the UI at result-activation time
//! (scroll target + post-activation announcement), and the UI
//! loads the note body anyway when the user opens the hit. The
//! Swift side now derives the line from its loaded note text at
//! click time, using the same first-token-occurrence heuristic.

use rusqlite::Connection;

use crate::session::CancelToken;
use crate::VaultError;

/// What corner of the vault to search.
///
/// V1.E ships `Vault` and `Folder`. `File` and `Tag` are reserved
/// for later milestones (single-file find-in-page, tag-scoped
/// search) and return `VaultError::Cancelled` today.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SearchScope {
    /// All files in the index.
    Vault,
    /// Files whose `path` starts with the given vault-relative
    /// directory prefix (`notes` matches `notes/foo.md` and
    /// `notes/sub/bar.md` but not `archive/foo.md`).
    Folder(String),
    /// Reserved: single-file find-in-page.
    File(String),
    /// Reserved: tag-scoped search (frontmatter `tags:` list).
    Tag(String),
}

/// One result row.
///
/// Manual `PartialEq` (no `Eq`) because `score` is an `f64` and
/// f64 doesn't implement `Eq`. Test paths compare via `assert_eq`
/// on the derived PartialEq.
///
/// No `line_number` field — see the module-level docs for why the
/// line is derived UI-side at activation time instead.
#[derive(Debug, Clone, PartialEq)]
pub struct QueryHit {
    pub path: String,
    /// FTS5 `snippet()` output: ±60 chars around the match with
    /// `\u{0002}` / `\u{0003}` (ASCII STX/ETX) wrapping the
    /// matched tokens. The UI replaces these with attributed-
    /// string emphasis runs. STX/ETX picked because they don't
    /// occur in legitimate Markdown content.
    pub snippet: String,
    /// FTS5 BM25 score (lower = more relevant). Surfaced for
    /// callers that want to re-rank; the API returns rows sorted
    /// by score already.
    pub score: f64,
}

/// Result set returned by `full_text_search`. Shape matches
/// `docs/plans/05` §8.4 so a Bases-style query engine can consume
/// it without translation.
#[derive(Debug, Clone, PartialEq)]
pub struct QueryResultSet {
    pub rows: Vec<QueryHit>,
    /// Pre-rendered audio summary string for VoiceOver
    /// announcements (`"Search returned N results"`).
    pub summary: String,
}

/// Hit-marker delimiters used in `snippet`. Public so the UI can
/// import the same constants when parsing the snippet for
/// attributed-string ranges.
pub const SNIPPET_HIT_START: &str = "\u{0002}";
pub const SNIPPET_HIT_END: &str = "\u{0003}";

/// Run a full-text search over the vault.
///
/// Honors cancellation: each row collected from the FTS cursor
/// checks the cancel token before continuing.
pub fn full_text_search(
    conn: &Connection,
    query: &str,
    scope: &SearchScope,
    cancel: &CancelToken,
) -> Result<QueryResultSet, VaultError> {
    if cancel.is_cancelled() {
        return Err(VaultError::Cancelled);
    }

    // Reserved scopes — surface `Unsupported` (not `Cancelled`) so
    // a caller with retry-on-cancel logic doesn't loop forever and
    // so log lines for "feature not landed yet" don't conflate with
    // "user pressed Esc" (#93 item 2).
    match scope {
        SearchScope::File(_) => {
            return Err(VaultError::Unsupported {
                feature: "search scope: File".to_string(),
            });
        }
        SearchScope::Tag(_) => {
            return Err(VaultError::Unsupported {
                feature: "search scope: Tag".to_string(),
            });
        }
        _ => {}
    }

    let trimmed = query.trim();
    if trimmed.is_empty() {
        return Ok(QueryResultSet {
            rows: Vec::new(),
            summary: "Search returned no results.".to_string(),
        });
    }

    // Two flavours of SQL: with and without a folder filter. We
    // can't use `?N IS NULL OR …` for the path-prefix branch
    // because SQLite's LIKE optimizer drops the index when the
    // pattern is a bound parameter — duplicating the SQL is the
    // tractable way to keep both paths covered by the
    // `idx_files_path` index.
    let (sql, folder_prefix) = match scope {
        SearchScope::Folder(folder) => {
            // Folder pattern: `folder/%` for the path-prefix match.
            // Escape SQLite LIKE metacharacters (`%`, `_`, `\`) in
            // the folder name so a vault with a directory named
            // `sales_q1` doesn't over-match `sales-q1` etc. The `\`
            // ESCAPE clause in the SQL below makes the escape
            // explicit.
            let mut prefix = escape_like(folder.trim_matches('/'));
            prefix.push('/');
            prefix.push('%');
            (
                "SELECT files.path,
                        snippet(files_fts, 0, ?2, ?3, '…', 12) AS sn,
                        bm25(files_fts) AS score
                 FROM files_fts
                 JOIN files ON files.id = files_fts.rowid
                 WHERE files_fts MATCH ?1
                   AND files.path LIKE ?4 ESCAPE '\\'
                 ORDER BY score ASC",
                Some(prefix),
            )
        }
        SearchScope::Vault => (
            "SELECT files.path,
                    snippet(files_fts, 0, ?2, ?3, '…', 12) AS sn,
                    bm25(files_fts) AS score
             FROM files_fts
             JOIN files ON files.id = files_fts.rowid
             WHERE files_fts MATCH ?1
             ORDER BY score ASC",
            None,
        ),
        // Already short-circuited above.
        SearchScope::File(_) | SearchScope::Tag(_) => unreachable!(),
    };

    let mut stmt = conn.prepare_cached(sql)?;

    let mut rows = Vec::new();
    // Map FTS5 query-syntax errors to a dedicated `InvalidQuery`
    // variant so the UI can render \"bad query\" guidance without
    // conflating with a corrupt-cache `Db` error. The actual rusqlite
    // surface for FTS5 errors is `SqliteFailure` with code `Unknown`
    // and a message starting with `fts5:` — we sniff the message
    // string since rusqlite doesn't expose a typed error for it.
    let cursor_result = match folder_prefix.as_ref() {
        Some(prefix) => stmt.query(rusqlite::params![
            trimmed,
            SNIPPET_HIT_START,
            SNIPPET_HIT_END,
            prefix,
        ]),
        None => stmt.query(rusqlite::params![
            trimmed,
            SNIPPET_HIT_START,
            SNIPPET_HIT_END,
        ]),
    };
    let mut cursor = match cursor_result {
        Ok(c) => c,
        Err(err) => return Err(map_fts5_error(err, trimmed)),
    };
    loop {
        let row = match cursor.next() {
            Ok(Some(r)) => r,
            Ok(None) => break,
            Err(err) => return Err(map_fts5_error(err, trimmed)),
        };
        // Periodic cancel check — same cooperative pattern as
        // `scan_initial`. Bail before allocating the next row.
        if cancel.is_cancelled() {
            return Err(VaultError::Cancelled);
        }
        let path: String = row.get(0)?;
        let snippet: String = row.get(1)?;
        let score: f64 = row.get(2)?;
        rows.push(QueryHit {
            path,
            snippet,
            score,
        });
    }

    let summary = match rows.len() {
        0 => "Search returned no results.".to_string(),
        1 => "Search returned 1 result.".to_string(),
        n => format!("Search returned {n} results."),
    };
    Ok(QueryResultSet { rows, summary })
}

/// Distinguish FTS5 query-syntax errors from real database errors.
///
/// SQLite surfaces FTS5 parse failures as `rusqlite::Error::
/// SqliteFailure` with code `Unknown` and one of several message
/// strings depending on the failure mode (`fts5: ...`,
/// `syntax error near ...`, `unterminated string`, `unknown
/// special query`, etc.). There's no typed variant, so we sniff
/// the message. A false negative (mapping a real DB error to
/// InvalidQuery) is far less harmful than a false positive
/// (mapping a syntax error to Db and confusing the UI), so the
/// matcher is intentionally permissive on the FTS5-marker side.
fn map_fts5_error(err: rusqlite::Error, query: &str) -> VaultError {
    let message = err.to_string();
    // Known FTS5 / SQL-parser failure markers. Broaden carefully
    // if new edge cases show up — but err on the side of treating
    // any `SqliteFailure` during a MATCH-bound cursor as a query
    // problem, since prepared-stmt binding errors and connection
    // failures shouldn't arrive here in practice.
    let looks_like_query_problem = message.contains("fts5:")
        || message.contains("syntax error")
        || message.contains("unterminated")
        || message.contains("unknown special query");
    if looks_like_query_problem {
        VaultError::InvalidQuery {
            message: format!("FTS5 could not parse {query:?}: {message}"),
        }
    } else {
        VaultError::from(err)
    }
}

/// Escape SQLite LIKE metacharacters (`%`, `_`, `\`) so a literal
/// folder name compares as a path prefix, not a glob. Paired with
/// `LIKE ? ESCAPE '\'` in the SQL above.
fn escape_like(s: &str) -> String {
    let mut out = String::with_capacity(s.len());
    for c in s.chars() {
        match c {
            '\\' | '%' | '_' => {
                out.push('\\');
                out.push(c);
            }
            c => out.push(c),
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn escape_like_handles_percent_underscore_backslash() {
        assert_eq!(escape_like("plain"), "plain");
        assert_eq!(escape_like("sales_q1"), "sales\\_q1");
        assert_eq!(escape_like("100%"), "100\\%");
        assert_eq!(
            escape_like(r"path\with\backslash"),
            r"path\\with\\backslash"
        );
    }
}
