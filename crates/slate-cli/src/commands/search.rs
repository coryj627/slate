// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate search <vault-path> <query> [--limit N]` (M-5, #536).
//!
//! A thin wrapper over `full_text_search(query, SearchScope::Vault,
//! cancel)` (m_spec §M-5). The API returns rows already ranked by BM25;
//! `--limit` (default 50) truncates them **client-side**, and the
//! `truncated` flag records whether any rows were dropped.
//!
//! Snippet marker handling is the one subtle bit. `full_text_search`
//! wraps each matched token in STX/ETX (`\u{0002}`/`\u{0003}`) markers
//! ([`SNIPPET_HIT_START`]/[`SNIPPET_HIT_END`]). Per the §M-5 normative
//! rule those markers never reach the user:
//!   - human / tsv: the snippet is the **plain** text, markers stripped.
//!   - json: the snippet is the plain text AND `match_ranges` carries
//!     `[{start,end}]` byte ranges into that plain snippet, derived from
//!     where the markers sat. The consumer re-highlights from the ranges.
//!
//! `data` shape (the `slate.cli.v1` stability contract):
//! ```json
//! { "summary": String, "truncated": bool,
//!   "hits": [{ "path": String, "snippet": String, "score": f64,
//!              "match_ranges": [{ "start": u64, "end": u64 }] }] }
//! ```
//! `InvalidQuery` (FTS5 syntax error) surfaces as exit 1 with the query
//! error message verbatim (via the standard `VaultError` Display path in
//! `main`).

use slate_core::SearchScope;
use slate_core::search_db::{SNIPPET_HIT_END, SNIPPET_HIT_START};
use slate_core::session::CancelToken;

use crate::output::{CommandOutput, tsv_row};
use crate::session::{CliError, map_vault_error, open_and_scan};

/// One byte range `[start, end)` into a plain snippet, marking where a
/// matched token sat before the STX/ETX markers were stripped.
struct MatchRange {
    start: usize,
    end: usize,
}

/// A single hit with its markers already resolved into a plain snippet
/// plus the derived match ranges.
struct Hit {
    path: String,
    snippet: String,
    score: f64,
    ranges: Vec<MatchRange>,
}

/// Run `slate search`. `limit` is the resolved `--limit` (caller passes
/// the clap default of 50 when the flag is absent).
pub fn run(
    raw_path: &std::path::Path,
    query: &str,
    limit: usize,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let (session, abs_path) = open_and_scan(raw_path, cancel)?;

    // Vault-wide search. An FTS5 syntax error comes back as
    // `VaultError::InvalidQuery`, which `map_vault_error` leaves as a
    // plain `Vault(_)` → exit 1 with the message verbatim.
    let result = session
        .full_text_search(query, &SearchScope::Vault, cancel)
        .map_err(map_vault_error)?;

    // Client-side truncation: the API already ranked the rows, so
    // keeping the first `limit` keeps the most relevant. `truncated`
    // records whether we dropped any.
    let total_rows = result.rows.len();
    let truncated = total_rows > limit;
    let hits: Vec<Hit> = result
        .rows
        .into_iter()
        .take(limit)
        .map(|row| {
            let (snippet, ranges) = strip_markers(&row.snippet);
            Hit {
                path: row.path,
                snippet,
                score: row.score,
                ranges,
            }
        })
        .collect();

    let data = serde_json::json!({
        "summary": result.summary,
        "truncated": truncated,
        "hits": hits.iter().map(hit_json).collect::<Vec<_>>(),
    });
    let human = render_human(&hits);
    let tsv = render_tsv(&hits);

    Ok((
        abs_path,
        CommandOutput {
            data,
            human,
            tsv,
            human_verbatim: false,
        },
    ))
}

/// Strip every STX/ETX marker from `raw`, returning the plain snippet
/// and the byte ranges (into that plain snippet) where matched tokens
/// sat. The markers are single-codepoint control chars that never occur
/// in legitimate Markdown (that's why the core picked them), so a simple
/// scan is unambiguous.
///
/// Robustness: an unbalanced marker (ETX with no open STX, or an STX
/// left open at end-of-string) is tolerated — we drop the stray marker
/// and emit whatever ranges we *can* pair. A malformed snippet must
/// never panic the CLI.
fn strip_markers(raw: &str) -> (String, Vec<MatchRange>) {
    // Both markers are one byte in UTF-8 (U+0002 / U+0003 are ASCII
    // control codes), so byte offsets and char boundaries coincide and
    // `str::replace`-style slicing is safe.
    let start_marker = SNIPPET_HIT_START;
    let end_marker = SNIPPET_HIT_END;

    let mut plain = String::with_capacity(raw.len());
    let mut ranges: Vec<MatchRange> = Vec::new();
    let mut open: Option<usize> = None;

    let mut rest = raw;
    while !rest.is_empty() {
        // Find the next marker of either kind.
        let next_start = rest.find(start_marker);
        let next_end = rest.find(end_marker);
        let (idx, is_start) = match (next_start, next_end) {
            (Some(s), Some(e)) => {
                if s < e {
                    (s, true)
                } else {
                    (e, false)
                }
            }
            (Some(s), None) => (s, true),
            (None, Some(e)) => (e, false),
            (None, None) => {
                plain.push_str(rest);
                break;
            }
        };
        // Copy the text before the marker into the plain buffer.
        plain.push_str(&rest[..idx]);
        if is_start {
            // Open a match at the current plain-buffer length.
            open = Some(plain.len());
            rest = &rest[idx + start_marker.len()..];
        } else {
            // Close a match if one is open; a stray ETX is dropped.
            if let Some(s) = open.take() {
                ranges.push(MatchRange {
                    start: s,
                    end: plain.len(),
                });
            }
            rest = &rest[idx + end_marker.len()..];
        }
    }
    (plain, ranges)
}

// --- json shaping ----------------------------------------------------

fn hit_json(hit: &Hit) -> serde_json::Value {
    serde_json::json!({
        "path": hit.path,
        "snippet": hit.snippet,
        "score": hit.score,
        "match_ranges": hit
            .ranges
            .iter()
            .map(|r| serde_json::json!({ "start": r.start as u64, "end": r.end as u64 }))
            .collect::<Vec<_>>(),
    })
}

// --- human format ----------------------------------------------------

/// Human format (m_spec §M-5): `path: snippet` per line (the grep
/// convention), markers stripped. An empty result set prints nothing.
fn render_human(hits: &[Hit]) -> String {
    hits.iter()
        .map(|h| format!("{}: {}", h.path, h.snippet))
        .collect::<Vec<_>>()
        .join("\n")
}

// --- tsv format ------------------------------------------------------

/// TSV format: header `path snippet score`, one row per hit. Snippet
/// markers are stripped (plain text); `tsv_row` flattens any embedded
/// tab/newline. Match ranges are json-only (documented — use json for
/// the highlight offsets).
fn render_tsv(hits: &[Hit]) -> String {
    let mut rows = vec![tsv_row(["path", "snippet", "score"])];
    for h in hits {
        // Pre-format the score into a binding of its own (Codoki PR
        // #646 Medium): borrowing `&h.score.to_string()` inside the
        // array literal leans on temporary-lifetime extension — legal
        // today, but a refactor that hoists the array breaks it.
        let score = h.score.to_string();
        rows.push(tsv_row([h.path.as_str(), h.snippet.as_str(), &score]));
    }
    rows.join("\n")
}

#[cfg(test)]
mod tests {
    use super::*;

    fn s(x: &str) -> String {
        x.to_string()
    }

    #[test]
    fn strip_markers_pairs_single_match() {
        let raw = format!("a {SNIPPET_HIT_START}hit{SNIPPET_HIT_END} b");
        let (plain, ranges) = strip_markers(&raw);
        assert_eq!(plain, "a hit b");
        assert_eq!(ranges.len(), 1);
        // "hit" sits at bytes 2..5 in "a hit b".
        assert_eq!((ranges[0].start, ranges[0].end), (2, 5));
    }

    #[test]
    fn strip_markers_handles_multiple_matches() {
        let raw = format!(
            "{SNIPPET_HIT_START}one{SNIPPET_HIT_END} two {SNIPPET_HIT_START}three{SNIPPET_HIT_END}"
        );
        let (plain, ranges) = strip_markers(&raw);
        assert_eq!(plain, "one two three");
        assert_eq!(ranges.len(), 2);
        assert_eq!((ranges[0].start, ranges[0].end), (0, 3));
        assert_eq!((ranges[1].start, ranges[1].end), (8, 13));
    }

    #[test]
    fn strip_markers_tolerates_unbalanced_markers() {
        // Stray ETX with no open STX → dropped, no range, no panic.
        let raw = format!("x{SNIPPET_HIT_END}y");
        let (plain, ranges) = strip_markers(&raw);
        assert_eq!(plain, "xy");
        assert!(ranges.is_empty());
        // STX left open at end → dropped, no range.
        let raw2 = format!("x{SNIPPET_HIT_START}y");
        let (plain2, ranges2) = strip_markers(&raw2);
        assert_eq!(plain2, "xy");
        assert!(ranges2.is_empty());
    }

    #[test]
    fn strip_markers_no_markers_is_identity() {
        let (plain, ranges) = strip_markers("plain text");
        assert_eq!(plain, "plain text");
        assert!(ranges.is_empty());
    }

    #[test]
    fn render_human_uses_grep_convention() {
        let hits = vec![Hit {
            path: s("notes/a.md"),
            snippet: s("some snippet"),
            score: 1.0,
            ranges: vec![],
        }];
        assert_eq!(render_human(&hits), "notes/a.md: some snippet");
    }
}
