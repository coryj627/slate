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
//! file-recents order (`FileRecentsStore` on mac). Hosts pass their
//! display cap to [`switcher_rank_top`], which returns that bounded row
//! page plus the exact total count used by result announcements.

use std::cmp::Ordering;
use std::collections::{BinaryHeap, HashMap, HashSet};

use unicode_normalization::UnicodeNormalization;

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

/// A bounded ranked page plus the exact number of matches. Keeping the
/// count separate lets hosts announce the full result set without
/// allocating, sorting, or marshalling rows they cannot display.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SwitcherRankPage {
    pub rows: Vec<SwitcherRow>,
    pub total: usize,
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
    score_with_display(query, file).map(|(_, score, spans)| (score, spans))
}

/// Internal scoring shape that also returns the already-derived display
/// name. Ranked callers can carry it into the result row instead of
/// allocating and normalizing the label a second time.
fn score_with_display(query: &str, file: &SwitcherFile) -> Option<(String, i32, Vec<MatchSpan>)> {
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
    Some((display, best, spans))
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
    // Path identity is canonical, not byte-wise: the deleted Swift used
    // `Set`/`Dictionary`/`<` over `String`, which compare canonically
    // equivalent forms as equal and order by normalized text. A recents
    // file that spells a path precomposed must keep matching the same
    // file enumerated decomposed (or vice versa), and tie-order must
    // not depend on which spelling a filesystem happened to hand us.
    let file_keys: Vec<String> = files.iter().map(|f| path_key(&f.path)).collect();

    if query.is_empty() {
        let present: HashSet<&str> = file_keys.iter().map(String::as_str).collect();
        let recent_keys: Vec<String> = recent_paths
            .iter()
            .map(|p| path_key(p))
            .filter(|key| present.contains(key.as_str()))
            .collect();
        let recent_set: HashSet<&str> = recent_keys.iter().map(String::as_str).collect();
        let recent_rows = recent_keys.iter().filter_map(|key| {
            files
                .iter()
                .zip(&file_keys)
                .find(|(_, file_key)| *file_key == key)
                .map(|(file, _)| unranked_row(file))
        });
        let rest = files
            .iter()
            .zip(&file_keys)
            .filter(|(_, key)| !recent_set.contains(key.as_str()))
            .map(|(file, _)| unranked_row(file));
        return recent_rows.chain(rest).collect();
    }

    // Rank of each path in the (pruned) recency list; absent = never
    // opened, which sorts after every ranked entry.
    let present: HashSet<&str> = file_keys.iter().map(String::as_str).collect();
    let rank: HashMap<String, usize> = recent_paths
        .iter()
        .map(|p| path_key(p))
        .filter(|key| present.contains(key.as_str()))
        .enumerate()
        .map(|(i, key)| (key, i))
        .collect();

    let mut matched: Vec<(SwitcherRow, String)> = files
        .iter()
        .zip(&file_keys)
        .filter_map(|(file, key)| {
            score_with_display(query, file).map(|(display_name, score, spans)| {
                (
                    SwitcherRow {
                        path: file.path.clone(),
                        name: file.name.clone(),
                        display_name,
                        score,
                        display_name_match_spans: spans,
                    },
                    key.clone(),
                )
            })
        })
        .collect();
    matched.sort_by(|(l, l_key), (r, r_key)| {
        r.score
            .cmp(&l.score)
            .then_with(|| {
                let lr = rank.get(l_key).copied().unwrap_or(usize::MAX);
                let rr = rank.get(r_key).copied().unwrap_or(usize::MAX);
                lr.cmp(&rr)
            })
            // Canonical path order (the stable sort preserves incoming
            // order for fully equivalent paths, like Swift's).
            .then_with(|| l_key.cmp(r_key))
    });
    matched.into_iter().map(|(row, _)| row).collect()
}

/// Rank only the first `limit` rows while still returning the exact
/// number of matches.
///
/// The ordering is byte-for-byte equivalent to [`switcher_rank`]. A
/// non-empty query retains at most `limit` candidates in a binary heap,
/// reducing row retention and sorting from O(N) space / O(N log N) time
/// to O(limit) space / O(N log limit) time. The scan remains O(N), as it
/// must to produce an exact total. An empty query streams the same
/// recents-then-input order directly into the bounded page.
pub fn switcher_rank_top(
    files: &[SwitcherFile],
    query: &str,
    recent_paths: &[String],
    limit: usize,
) -> SwitcherRankPage {
    let file_keys: Vec<String> = files.iter().map(|file| path_key(&file.path)).collect();
    let present: HashSet<&str> = file_keys.iter().map(String::as_str).collect();

    if query.is_empty() {
        let recent_keys: Vec<String> = recent_paths
            .iter()
            .map(|path| path_key(path))
            .filter(|key| present.contains(key.as_str()))
            .collect();
        let recent_set: HashSet<&str> = recent_keys.iter().map(String::as_str).collect();
        let rest_total = file_keys
            .iter()
            .filter(|key| !recent_set.contains(key.as_str()))
            .count();
        let total = recent_keys.len().saturating_add(rest_total);
        let mut rows = Vec::with_capacity(limit.min(total));

        for key in &recent_keys {
            if rows.len() == limit {
                break;
            }
            if let Some((file, _)) = files
                .iter()
                .zip(&file_keys)
                .find(|(_, file_key)| *file_key == key)
            {
                rows.push(unranked_row(file));
            }
        }
        if rows.len() < limit {
            rows.extend(
                files
                    .iter()
                    .zip(&file_keys)
                    .filter(|(_, key)| !recent_set.contains(key.as_str()))
                    .take(limit - rows.len())
                    .map(|(file, _)| unranked_row(file)),
            );
        }
        return SwitcherRankPage { rows, total };
    }

    // Match the compatibility implementation's duplicate-recents
    // behavior: collecting retains the last rank for a repeated key.
    let rank: HashMap<String, usize> = recent_paths
        .iter()
        .map(|path| path_key(path))
        .filter(|key| present.contains(key.as_str()))
        .enumerate()
        .map(|(index, key)| (key, index))
        .collect();
    let mut retained: BinaryHeap<RankedCandidate> =
        BinaryHeap::with_capacity(limit.min(files.len()));
    let mut total = 0usize;

    for (input_index, (file, key)) in files.iter().zip(&file_keys).enumerate() {
        let Some((display_name, score, spans)) = score_with_display(query, file) else {
            continue;
        };
        total = total.saturating_add(1);
        if limit == 0 {
            continue;
        }

        let recency_rank = rank.get(key).copied().unwrap_or(usize::MAX);
        let should_retain = retained.len() < limit
            || retained.peek().is_some_and(|worst| {
                RankedCandidate::ranking_order_values(
                    RankOrderKey {
                        score,
                        recency_rank,
                        path_key: key,
                        input_index,
                    },
                    worst.order_key(),
                ) == Ordering::Less
            });
        if !should_retain {
            continue;
        }

        let candidate = RankedCandidate {
            row: SwitcherRow {
                path: file.path.clone(),
                name: file.name.clone(),
                display_name,
                score,
                display_name_match_spans: spans,
            },
            path_key: key.clone(),
            recency_rank,
            input_index,
        };
        if retained.len() < limit {
            retained.push(candidate);
        } else {
            retained.pop();
            retained.push(candidate);
        }
    }

    let mut retained = retained.into_vec();
    retained.sort_by(RankedCandidate::ranking_order);
    SwitcherRankPage {
        rows: retained
            .into_iter()
            .map(|candidate| candidate.row)
            .collect(),
        total,
    }
}

/// Heap ordering intentionally places the worst retained candidate at
/// the top, so a better incoming row can replace it in O(log limit).
#[derive(Debug)]
struct RankedCandidate {
    row: SwitcherRow,
    path_key: String,
    recency_rank: usize,
    input_index: usize,
}

#[derive(Clone, Copy)]
struct RankOrderKey<'a> {
    score: i32,
    recency_rank: usize,
    path_key: &'a str,
    input_index: usize,
}

impl RankedCandidate {
    fn order_key(&self) -> RankOrderKey<'_> {
        RankOrderKey {
            score: self.row.score,
            recency_rank: self.recency_rank,
            path_key: &self.path_key,
            input_index: self.input_index,
        }
    }

    /// `Less` means `left` appears earlier in the final ranking.
    fn ranking_order(left: &Self, right: &Self) -> Ordering {
        Self::ranking_order_values(left.order_key(), right.order_key())
    }

    fn ranking_order_values(left: RankOrderKey<'_>, right: RankOrderKey<'_>) -> Ordering {
        right
            .score
            .cmp(&left.score)
            .then_with(|| left.recency_rank.cmp(&right.recency_rank))
            .then_with(|| left.path_key.cmp(right.path_key))
            // `switcher_rank` uses a stable sort. Preserve incoming
            // order when canonical path keys are equivalent.
            .then_with(|| left.input_index.cmp(&right.input_index))
    }
}

impl PartialEq for RankedCandidate {
    fn eq(&self, other: &Self) -> bool {
        Self::ranking_order(self, other) == Ordering::Equal
    }
}

impl Eq for RankedCandidate {}

impl PartialOrd for RankedCandidate {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}

impl Ord for RankedCandidate {
    fn cmp(&self, other: &Self) -> Ordering {
        Self::ranking_order(self, other)
    }
}

/// Canonical (NFC) identity key for a vault-relative path — the Rust
/// spelling of Swift `String` equality/hashing/ordering for the
/// switcher's path bookkeeping. Case-SENSITIVE, unlike the matcher's
/// grapheme keys: Swift compared paths canonically but never
/// case-folded them.
fn path_key(path: &str) -> String {
    path.chars().nfc().collect()
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

    // --- canonical path identity (Swift String parity) ---

    #[test]
    fn recents_match_canonically_equivalent_path_spellings() {
        // The recents file spells the path precomposed; the file list
        // enumerates it decomposed. Swift Set/Dictionary treated those
        // as the same path — so must we, in both query modes. The
        // decomposed file deliberately comes SECOND in incoming order:
        // a raw-byte impl that pruned the recent would fall through to
        // incoming order and fail the recency-first assertion below.
        let files = [
            file("other/plain.md", "plain.md"),
            file("e\u{301}/note.md", "note.md"),
        ];
        let recents = vec!["\u{e9}/note.md".to_owned()];

        let rows = switcher_rank(&files, "", &recents);
        assert_eq!(
            paths(&rows),
            vec!["e\u{301}/note.md", "other/plain.md"],
            "composed recent must recognize the decomposed file (recency-first), not be pruned",
        );

        // Ranked mode: the recency tie-break must see the same identity.
        let files = [
            file("alpha/note.md", "note.md"),
            file("e\u{301}/note.md", "note.md"),
        ];
        let rows = switcher_rank(&files, "note", &recents);
        assert_eq!(
            paths(&rows),
            vec!["e\u{301}/note.md", "alpha/note.md"],
            "recency rank lookup is canonical, not byte-wise",
        );
    }

    #[test]
    fn ranked_path_tiebreak_orders_canonically() {
        // Equal scores, no recency: Swift String's `<` orders by
        // normalized text, so decomposed "e\u{301}" sorts as "é" —
        // AFTER "f" — even though its raw bytes start with plain "e".
        let files = [
            file("f/note.md", "note.md"),
            file("e\u{301}/note.md", "note.md"),
        ];
        let rows = switcher_rank(&files, "note", &[]);
        assert_eq!(paths(&rows), vec!["f/note.md", "e\u{301}/note.md"]);
        // Returned rows carry the ORIGINAL spelling, not the key.
        assert_eq!(rows[1].path, "e\u{301}/note.md");
    }

    #[test]
    fn ranked_ties_without_recency_break_by_path() {
        let files = [file("z/note.md", "note.md"), file("a/note.md", "note.md")];
        let rows = switcher_rank(&files, "note", &[]);
        assert_eq!(paths(&rows), vec!["a/note.md", "z/note.md"]);
    }

    #[test]
    fn bounded_rank_matches_full_order_and_exact_total() {
        let files: Vec<SwitcherFile> = (0..2_000)
            .map(|index| {
                let bucket = index % 17;
                file(
                    &format!("folder-{bucket:02}/note-{index:04}.md"),
                    &format!("note-{index:04}.md"),
                )
            })
            .collect();
        let recents = vec![
            "folder-02/note-0002.md".to_owned(),
            "folder-01/note-0001.md".to_owned(),
        ];

        for query in ["", "note", "n19", "missing"] {
            let full = switcher_rank(&files, query, &recents);
            for limit in [0, 1, 7, 50, 5_000] {
                let page = switcher_rank_top(&files, query, &recents, limit);
                assert_eq!(page.total, full.len(), "query={query:?}, limit={limit}");
                assert_eq!(
                    page.rows,
                    full.iter().take(limit).cloned().collect::<Vec<_>>(),
                    "query={query:?}, limit={limit}",
                );
            }
        }
    }

    #[test]
    fn bounded_rank_preserves_duplicate_recents_and_stable_canonical_ties() {
        let files = [
            file("other/note.md", "note.md"),
            file("e\u{301}/note.md", "note.md"),
            file("\u{e9}/note.md", "note.md"),
        ];
        let recents = vec!["\u{e9}/note.md".to_owned(), "\u{e9}/note.md".to_owned()];

        for query in ["", "note"] {
            let full = switcher_rank(&files, query, &recents);
            let page = switcher_rank_top(&files, query, &recents, 2);
            assert_eq!(page.total, full.len());
            assert_eq!(page.rows, full[..2]);
        }
    }
}
