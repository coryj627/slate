//! Full-text search over the `files_fts` virtual table.
//!
//! Exposes a single entry point — `full_text_search` — that runs an
//! FTS5 MATCH query, snippets each hit, and returns a result-set
//! shape that matches `docs/plans/05` §8.4 so #58's UI and the
//! future Bases / query-engine layer can both consume it without
//! adapter shims.

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
#[derive(Debug, Clone, PartialEq)]
pub struct QueryHit {
    pub path: String,
    /// 1-based line number where the hit begins. Best-effort:
    /// computed by finding the first occurrence of any of the
    /// query's whitespace-split tokens inside `body_text`. Falls
    /// back to 1 when no token can be located (e.g. complex
    /// phrase queries with stemming where FTS5 matched but the
    /// raw tokens don't appear literally).
    pub line_number: u32,
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

    // Reserved scopes — surface an explicit cancelled error rather
    // than silently returning empty results so the UI can render
    // "not supported yet" guidance from the same error path it
    // already handles.
    match scope {
        SearchScope::File(_) | SearchScope::Tag(_) => {
            return Err(VaultError::Cancelled);
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
                "SELECT files.path, files.body_text,
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
            "SELECT files.path, files.body_text,
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
    let query_tokens = tokenize_query_for_line_lookup(trimmed);

    let mut rows = Vec::new();
    let mut cursor = match folder_prefix.as_ref() {
        Some(prefix) => stmt.query(rusqlite::params![
            trimmed,
            SNIPPET_HIT_START,
            SNIPPET_HIT_END,
            prefix,
        ])?,
        None => stmt.query(rusqlite::params![
            trimmed,
            SNIPPET_HIT_START,
            SNIPPET_HIT_END,
        ])?,
    };
    while let Some(row) = cursor.next()? {
        // Periodic cancel check — same cooperative pattern as
        // `scan_initial`. Bail before allocating the next row.
        if cancel.is_cancelled() {
            return Err(VaultError::Cancelled);
        }
        let path: String = row.get(0)?;
        let body_text: String = row.get(1)?;
        let snippet: String = row.get(2)?;
        let score: f64 = row.get(3)?;
        let line_number = find_first_token_line(&body_text, &query_tokens);
        rows.push(QueryHit {
            path,
            line_number,
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

/// Strip FTS5 query syntax to leave bare lookup tokens we can search
/// for inside `body_text` to derive a line number. This is a coarse
/// approximation — we drop quotes, operators (`AND`/`OR`/`NOT`/`+`/
/// `-`), and column filters, then split on whitespace.
fn tokenize_query_for_line_lookup(query: &str) -> Vec<String> {
    query
        .split(|c: char| !c.is_alphanumeric())
        .filter(|tok| !tok.is_empty())
        .map(|tok| tok.to_lowercase())
        .filter(|tok| !matches!(tok.as_str(), "and" | "or" | "not"))
        .collect()
}

/// Return the 1-based line number of the first occurrence (in
/// `body_text`) of any token from `tokens`. Falls back to 1 when no
/// token can be found — FTS5 may have matched through stemming or
/// punctuation collapse and the raw tokens needn't appear literally.
fn find_first_token_line(body_text: &str, tokens: &[String]) -> u32 {
    let body_lower = body_text.to_lowercase();
    let mut earliest: Option<usize> = None;
    for tok in tokens {
        if let Some(pos) = body_lower.find(tok.as_str()) {
            earliest = match earliest {
                None => Some(pos),
                Some(prev) => Some(prev.min(pos)),
            };
        }
    }
    match earliest {
        Some(pos) => {
            let line = body_text[..pos].bytes().filter(|b| *b == b'\n').count() as u32 + 1;
            line
        }
        None => 1,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tokenize_strips_operators_and_punctuation() {
        let toks = tokenize_query_for_line_lookup("foo AND \"bar baz\" OR -quux");
        assert_eq!(toks, vec!["foo", "bar", "baz", "quux"]);
    }

    #[test]
    fn find_first_token_line_returns_first_match_line() {
        let body = "line one\nline two with foo\nline three\nfoo again";
        let line = find_first_token_line(body, &["foo".to_string()]);
        assert_eq!(line, 2);
    }

    #[test]
    fn find_first_token_line_falls_back_to_one_when_token_missing() {
        let body = "this body has nothing matching";
        let line = find_first_token_line(body, &["absent".to_string()]);
        assert_eq!(line, 1);
    }

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

    #[test]
    fn find_first_token_line_picks_earliest_across_multiple_tokens() {
        let body = "alpha bravo\ncharlie delta\necho";
        let line = find_first_token_line(body, &["delta".to_string(), "alpha".to_string()]);
        // alpha is earlier than delta, so line 1.
        assert_eq!(line, 1);
    }
}
