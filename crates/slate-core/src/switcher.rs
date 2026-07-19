// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Quick-switcher ranking + recency blending (W0.5-2, #718).
//!
//! Everything `QuickSwitcherModel` previously decided in Swift lives
//! here so both hosts rank files identically from identical inputs:
//! the display-name derivation, the name-over-path score bias, and the
//! recency-blended orderings for the empty and non-empty query.
//!
//! **The one-ranking-engine decision (spec §W0.5-2, made side-by-side
//! in this PR):** the palette and the switcher genuinely share the base
//! matcher — the switcher's Swift `fuzzyScore` was a verbatim copy of
//! the palette's — so there is exactly one engine:
//! [`crate::palette::fuzzy_score`]. What differs is the domain layer on
//! top, and that split is deliberate: the palette blends label + hint
//! and groups into sections; the switcher blends display-name / full
//! name / path with a name bias and orders by score-then-recency. Both
//! layers live core-side; neither host re-implements any of it.
//!
//! Hosts feed this module their candidate files and the vault's
//! file-recents order (`FileRecentsStore` on mac); the display cap on
//! rendered rows stays a host/view concern (virtualization policy, not
//! ranking — the full ranked list is what result-count announcements
//! report).

use crate::palette::{MatchSpan, fuzzy_score};

/// Bonus added to a row's score when the QUERY subsequence-matched the
/// file's NAME (either the display name or the full name, not merely
/// its path). Biases `foo` toward `foo.md` over
/// `notes/foo-archive/bar.md`.
pub const NAME_MATCH_BONUS: i32 = 20;

/// One rankable file, as the host's file list provides it. `path` is
/// vault-relative (`notes/foo.md`); `name` is the display name WITH
/// extension (`foo.md`), as `FileSummary` carries it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SwitcherFile {
    pub path: String,
    pub name: String,
}

/// One ranked switcher row. `display_name` is the canonical row label
/// (extension-stripped); `score` is the winning blended score (0 on an
/// empty query); `display_name_match_spans` are the matched byte ranges
/// inside `display_name` when the display name itself matched (empty
/// otherwise — a path- or full-name-only match has nothing to bold in
/// the visible label).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SwitcherRow {
    pub path: String,
    pub name: String,
    pub display_name: String,
    pub score: i32,
    pub display_name_match_spans: Vec<MatchSpan>,
}

/// The name with its markdown extension stripped, for the primary row
/// label ("foo", not "foo.md"). Only the trailing `.md`/`.markdown` is
/// removed (case-insensitively); a dotted stem like `2026.01.notes.md`
/// keeps everything but the final extension, and non-markdown names
/// pass through untouched.
pub fn display_name(name: &str) -> String {
    let lower = name.to_lowercase();
    for ext in [".md", ".markdown"] {
        if lower.ends_with(ext) {
            return name[..name.len() - ext.len()].to_owned();
        }
    }
    name.to_owned()
}

/// Score a file for `query`, biased toward name matches. Returns `None`
/// when neither the name (either form) nor the path
/// subsequence-matches.
///
/// Runs the shared matcher over three targets — the extension-stripped
/// display name, the full name, and the full path — and takes the max.
/// When a NAME target (either form) matched, [`NAME_MATCH_BONUS`] is
/// added so a name hit outranks a same-strength path-only hit;
/// path-only matches still score, just below an equivalent name hit.
/// The returned spans are the display-name match ranges (empty when
/// only the full name or the path matched).
pub fn score(query: &str, file: &SwitcherFile) -> Option<(i32, Vec<MatchSpan>)> {
    let display = display_name(&file.name);
    let display_match = fuzzy_score(query, &display);
    let name_match = fuzzy_score(query, &file.name);
    let path_match = fuzzy_score(query, &file.path);

    let best_name = match (
        display_match.as_ref().map(|(s, _)| *s),
        name_match.map(|(s, _)| s),
    ) {
        (Some(d), Some(n)) => Some(d.max(n)),
        (Some(d), None) => Some(d),
        (None, Some(n)) => Some(n),
        (None, None) => None,
    };
    let best = match (
        best_name.map(|s| s + NAME_MATCH_BONUS),
        path_match.map(|(s, _)| s),
    ) {
        (Some(n), Some(p)) => n.max(p),
        (Some(n), None) => n,
        (None, Some(p)) => p,
        (None, None) => return None,
    };
    let spans = display_match.map(|(_, spans)| spans).unwrap_or_default();
    Some((best, spans))
}

/// Rank `files` for `query` with the vault's recency order blended in.
///
/// - **Empty query**: every file — the still-present recents first (in
///   recency order; recents whose file no longer exists are pruned),
///   then the remaining files in their incoming order. Scores are 0.
/// - **Non-empty query**: files whose name or path fuzzy-matches,
///   sorted by descending score; ties broken by recency rank (opened
///   files ahead of never-opened), then by path for a fully
///   deterministic order. Recency only ever breaks ties — a materially
///   better fuzzy score always wins.
pub fn switcher_rank(
    files: &[SwitcherFile],
    query: &str,
    recent_paths: &[String],
) -> Vec<SwitcherRow> {
    if query.is_empty() {
        let present: std::collections::HashSet<&str> =
            files.iter().map(|f| f.path.as_str()).collect();
        let recent: Vec<&str> = recent_paths
            .iter()
            .map(String::as_str)
            .filter(|p| present.contains(p))
            .collect();
        let recent_set: std::collections::HashSet<&str> = recent.iter().copied().collect();
        let recent_rows = recent
            .iter()
            .filter_map(|path| files.iter().find(|f| f.path == *path).map(unranked_row));
        let rest = files
            .iter()
            .filter(|f| !recent_set.contains(f.path.as_str()))
            .map(unranked_row);
        return recent_rows.chain(rest).collect();
    }

    // Rank of each path in the (pruned) recency list; absent = never
    // opened, which sorts after every ranked entry.
    let present: std::collections::HashSet<&str> = files.iter().map(|f| f.path.as_str()).collect();
    let rank: std::collections::HashMap<&str, usize> = recent_paths
        .iter()
        .filter(|p| present.contains(p.as_str()))
        .enumerate()
        .map(|(i, p)| (p.as_str(), i))
        .collect();

    let mut matched: Vec<SwitcherRow> = files
        .iter()
        .filter_map(|file| {
            score(query, file).map(|(score, spans)| SwitcherRow {
                path: file.path.clone(),
                name: file.name.clone(),
                display_name: display_name(&file.name),
                score,
                display_name_match_spans: spans,
            })
        })
        .collect();
    matched.sort_by(|l, r| {
        r.score
            .cmp(&l.score)
            .then_with(|| {
                let lr = rank.get(l.path.as_str()).copied().unwrap_or(usize::MAX);
                let rr = rank.get(r.path.as_str()).copied().unwrap_or(usize::MAX);
                lr.cmp(&rr)
            })
            .then_with(|| l.path.cmp(&r.path))
    });
    matched
}

fn unranked_row(file: &SwitcherFile) -> SwitcherRow {
    SwitcherRow {
        path: file.path.clone(),
        name: file.name.clone(),
        display_name: display_name(&file.name),
        score: 0,
        display_name_match_spans: Vec::new(),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn file(path: &str, name: &str) -> SwitcherFile {
        SwitcherFile {
            path: path.to_owned(),
            name: name.to_owned(),
        }
    }

    fn paths(rows: &[SwitcherRow]) -> Vec<&str> {
        rows.iter().map(|r| r.path.as_str()).collect()
    }

    // --- display_name: the mac unit tests, moved (goldens preserved) ---

    #[test]
    fn display_name_strips_markdown_extensions() {
        assert_eq!(display_name("foo.md"), "foo");
        assert_eq!(display_name("foo.markdown"), "foo");
    }

    #[test]
    fn display_name_keeps_interior_dots() {
        assert_eq!(display_name("2026.01.notes.md"), "2026.01.notes");
    }

    #[test]
    fn display_name_leaves_non_markdown_untouched() {
        assert_eq!(display_name("diagram.png"), "diagram.png");
    }

    #[test]
    fn display_name_strips_case_insensitively_keeping_stem_case() {
        assert_eq!(display_name("FOO.MD"), "FOO");
        assert_eq!(display_name("Readme.Markdown"), "Readme");
    }

    // --- score: the mac unit tests, moved (goldens preserved) ---

    #[test]
    fn score_biases_name_over_path_match() {
        let name = score("foo", &file("foo.md", "foo.md")).unwrap().0;
        let path_only = score("foo", &file("notes/foo-archive/bar.md", "bar.md"))
            .unwrap()
            .0;
        assert!(name > path_only, "{name} vs {path_only}");
    }

    #[test]
    fn score_adds_name_bonus_on_top_of_name_fuzzy_score() {
        // The name ("foo" after stripping) is a prefix hit; adding the
        // bonus is what the blend does. The path also matches
        // ("dir/foo.md" contains "foo"), but the boosted name score
        // wins the max.
        let bare_name = fuzzy_score("foo", "foo").unwrap().0;
        let blended = score("foo", &file("dir/foo.md", "foo.md")).unwrap().0;
        assert_eq!(blended, bare_name + NAME_MATCH_BONUS);
    }

    #[test]
    fn score_matches_via_path_when_name_does_not() {
        assert!(score("dir", &file("dir/bar.md", "bar.md")).is_some());
    }

    #[test]
    fn score_is_case_insensitive() {
        let lower = score("foo", &file("A/Foo.md", "Foo.md")).map(|(s, _)| s);
        let upper = score("FOO", &file("A/Foo.md", "Foo.md")).map(|(s, _)| s);
        assert!(lower.is_some());
        assert_eq!(lower, upper);
    }

    #[test]
    fn score_returns_none_for_non_match() {
        assert_eq!(score("zzz", &file("a/foo.md", "foo.md")), None);
    }

    #[test]
    fn score_spans_cover_the_display_name_match() {
        let (_, spans) = score("foo", &file("dir/foo.md", "foo.md")).unwrap();
        assert_eq!(
            spans,
            vec![MatchSpan {
                start_byte: 0,
                end_byte: 3
            }]
        );
        // Full-name-only match (the ".md" suffix): nothing to bold in
        // the visible label.
        let (_, spans) = score("md", &file("dir/foo.md", "foo.md")).unwrap();
        assert!(spans.is_empty());
    }

    // --- switcher_rank: recency blending goldens ---

    #[test]
    fn empty_query_orders_recents_first_then_incoming_order() {
        let files = [
            file("a.md", "a.md"),
            file("b.md", "b.md"),
            file("c.md", "c.md"),
            file("d.md", "d.md"),
        ];
        let rows = switcher_rank(&files, "", &["c.md".into(), "a.md".into()]);
        assert_eq!(paths(&rows), vec!["c.md", "a.md", "b.md", "d.md"]);
        assert!(rows.iter().all(|r| r.score == 0));
    }

    #[test]
    fn empty_query_prunes_recents_missing_from_files() {
        let files = [file("a.md", "a.md"), file("b.md", "b.md")];
        let rows = switcher_rank(&files, "", &["gone.md".into(), "b.md".into()]);
        assert_eq!(paths(&rows), vec!["b.md", "a.md"]);
    }

    #[test]
    fn ranked_query_sorts_by_score_then_recency_tiebreak() {
        // Two files with an identical name → identical fuzzy score for
        // "note"; recency breaks the tie.
        let files = [
            file("alpha/note.md", "note.md"),
            file("beta/note.md", "note.md"),
        ];
        let rows = switcher_rank(&files, "note", &["beta/note.md".into()]);
        assert_eq!(paths(&rows), vec!["beta/note.md", "alpha/note.md"]);
    }

    #[test]
    fn recency_does_not_beat_a_materially_better_fuzzy_score() {
        // "meeting-notes.md" is a far weaker match for "notes" than
        // "notes.md", yet it was opened recently. Score must still win.
        let files = [
            file("notes.md", "notes.md"),
            file("archive/meeting-notes.md", "meeting-notes.md"),
        ];
        let rows = switcher_rank(&files, "notes", &["archive/meeting-notes.md".into()]);
        assert_eq!(rows[0].path, "notes.md");
    }

    #[test]
    fn ranked_query_excludes_non_matches() {
        let files = [file("foo.md", "foo.md"), file("bar.md", "bar.md")];
        let rows = switcher_rank(&files, "foo", &[]);
        assert_eq!(paths(&rows), vec!["foo.md"]);
    }

    #[test]
    fn ranked_ties_without_recency_break_by_path() {
        let files = [file("z/note.md", "note.md"), file("a/note.md", "note.md")];
        let rows = switcher_rank(&files, "note", &[]);
        assert_eq!(paths(&rows), vec!["a/note.md", "z/note.md"]);
    }
}
