// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Command-palette ranking, sectioning, and recents policy (W0.5-1,
//! #717).
//!
//! Everything the mac palette previously decided in Swift lives here so
//! both hosts render identical palettes from identical inputs:
//!
//! - the fuzzy matcher ([`fuzzy_score`]) — subsequence-with-boost
//!   scoring ported from `CommandPaletteModel.fuzzyScore`, extended to
//!   report the matched label byte ranges for per-platform bolding;
//! - section layout ([`palette_sections`]) — Recent-first empty-query
//!   grouping with recent-id exclusion, declared section order,
//!   host-pinned within-Sidebar catalog order, and best-match
//!   non-empty-query ordering, sections included (#1834);
//! - ordered-recents policy ([`recents_decode`] / [`recents_encode`] /
//!   [`recents_add`] / [`recents_remove`]) — the LRU transitions, the
//!   dedupe + cap rules, the malformed-tolerant decode, and the
//!   oversized-file guard.
//!
//! **Storage boundary (issue #717):** hosts own platform path discovery
//! and atomic file I/O for the recents file; this module owns the byte
//! format and every state transition. The on-disk shape (format v1,
//! grandfathered from the mac store) is a bare JSON array of command id
//! strings — any future evolution of that shape happens here, not in a
//! host.
//!
//! The section headers ("Recent", "File", …) are canonical strings both
//! hosts render verbatim (plain en-US in V1; localisation lands with
//! #264's scheme).

use serde::Serialize;
use unicode_normalization::UnicodeNormalization;
use unicode_segmentation::UnicodeSegmentation;

use crate::commands::{Command, CommandSection};

/// Hard cap on persisted recents (mirrors the palette UI's Recent
/// section length).
pub const RECENTS_MAX_ENTRIES: usize = 10;

/// Upper bound on a recents file [`recents_decode`] will accept. A
/// well-formed file holds at most [`RECENTS_MAX_ENTRIES`] ids of ~50
/// bytes; 64 KiB is generously above that while refusing a malformed or
/// hand-crafted huge file. Strictly larger inputs decode as empty.
pub const RECENTS_MAX_FILE_BYTES: usize = 1 << 16; // 64 KiB

/// One matched byte range inside a command label, for host-side
/// bolding. Half-open `[start_byte, end_byte)` over the label's UTF-8
/// bytes; adjacent matched characters merge into one span.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct MatchSpan {
    pub start_byte: u32,
    pub end_byte: u32,
}

/// One ranked palette row: the command, the label byte ranges the
/// query matched, and the winning fuzzy score. Spans are empty when the
/// query is empty or when only the accessibility hint matched (there is
/// nothing in the visible label to bold); `score` is the max of the
/// label and hint scores (0 on an empty query). It is the fuzzy part of
/// the ranking only: [`palette_sections`] already returns the rows in
/// best-match order, an exact name before a higher score, so the first
/// row is the strongest match and no host re-sorts by `score`.
#[derive(Debug, Clone, PartialEq)]
pub struct PaletteRow {
    pub command: Command,
    pub label_match_spans: Vec<MatchSpan>,
    pub score: i32,
}

/// One renderable palette section. `kind == None` is the synthetic
/// Recent section; every other section maps 1:1 to a
/// [`CommandSection`].
#[derive(Debug, Clone, PartialEq)]
pub struct PaletteSection {
    pub title: String,
    pub kind: Option<CommandSection>,
    pub rows: Vec<PaletteRow>,
}

/// Declared palette section order. Deliberately NOT the
/// `CommandSection` discriminant order: structural sections (Canvas,
/// Bases, Graph, Sidebar) render between Editor and Tasks. A Rust-side
/// enum reorder must not silently change palette layout — this list is
/// the layout contract.
const SECTION_ORDER: [CommandSection; 12] = [
    CommandSection::File,
    CommandSection::Navigation,
    CommandSection::View,
    CommandSection::Vault,
    CommandSection::Editor,
    CommandSection::Canvas,
    CommandSection::Bases,
    CommandSection::Graph,
    CommandSection::Sidebar,
    CommandSection::Tasks,
    CommandSection::Settings,
    CommandSection::Plugins,
];

/// Canonical human-readable header for a section. Both hosts render
/// these verbatim.
pub fn section_title(section: CommandSection) -> &'static str {
    match section {
        CommandSection::File => "File",
        CommandSection::Navigation => "Navigation",
        CommandSection::View => "View",
        CommandSection::Vault => "Vault",
        CommandSection::Editor => "Editor",
        CommandSection::Tasks => "Tasks",
        CommandSection::Settings => "Settings",
        CommandSection::Plugins => "Plugins",
        CommandSection::Canvas => "Canvas",
        CommandSection::Bases => "Bases",
        CommandSection::Graph => "Graph",
        CommandSection::Sidebar => "Sidebar",
    }
}

/// Synthetic Recent section header.
pub const RECENT_SECTION_TITLE: &str = "Recent";

/// Subsequence-with-boost fuzzy matcher. Returns `None` when `query`
/// does not subsequence-match `target`, otherwise the score plus the
/// matched byte spans in `target` (merged over adjacent clusters).
///
/// Scoring (the mac palette's semantics, now the reference):
/// - +10 per matched character
/// - +5 when the match lands at a word boundary (start of string or
///   after space / `.` / `-` / `:` / `_`)
/// - +3 when the match is consecutive with the previous match
/// - +50 when the query is a strict case-insensitive prefix of the
///   target
///
/// "Character" means what it meant in the Swift originals: an
/// **extended grapheme cluster compared under canonical equivalence**
/// (Swift `Character` equality), case-insensitively. Both sides are
/// segmented into grapheme clusters and compared by NFC-normalized
/// lowercase keys, so a precomposed `é` (U+00E9) matches a decomposed
/// `e` + U+0301 in either direction, while a bare `e` matches neither —
/// exactly as the mac scorers behaved. Spans index the ORIGINAL
/// target's bytes. An empty query scores 0 with no spans.
pub fn fuzzy_score(query: &str, target: &str) -> Option<(i32, Vec<MatchSpan>)> {
    if query.is_empty() {
        return Some((0, Vec::new()));
    }
    let q: Vec<String> = query.graphemes(true).map(grapheme_key).collect();

    let mut qi = 0usize;
    let mut consecutive = 0u32;
    let mut score = 0i32;
    let mut spans: Vec<MatchSpan> = Vec::new();
    let mut prev_key: Option<String> = None;
    let mut target_graphemes = 0usize;
    let mut folded_prefix_matches = true;

    for (ti, (byte, grapheme)) in target.grapheme_indices(true).enumerate() {
        target_graphemes += 1;
        let key = grapheme_key(grapheme);
        if ti < q.len() && folded_prefix_matches && key != q[ti] {
            folded_prefix_matches = false;
        }
        if qi < q.len() && key == q[qi] {
            score += 10;
            let boundary = match &prev_key {
                None => true,
                Some(p) => WORD_BOUNDARY.contains(&p.as_str()),
            };
            if boundary {
                score += 5;
            }
            if consecutive > 0 {
                score += 3;
            }
            consecutive += 1;
            qi += 1;

            let end = byte + grapheme.len();
            match spans.last_mut() {
                Some(last) if last.end_byte as usize == byte => {
                    last.end_byte = end as u32;
                }
                _ => spans.push(MatchSpan {
                    start_byte: byte as u32,
                    end_byte: end as u32,
                }),
            }
        } else {
            consecutive = 0;
        }
        prev_key = Some(key);
    }

    if qi != q.len() {
        return None;
    }
    if folded_prefix_matches && q.len() <= target_graphemes {
        score += 50;
    }
    Some((score, spans))
}

/// Word-boundary cluster keys for the +5 bonus: the punctuation a
/// command label or scraped hint id might reasonably use.
const WORD_BOUNDARY: [&str; 5] = [" ", ".", "-", ":", "_"];

/// Comparison key for one extended grapheme cluster: canonical (NFC)
/// form, lowercased, re-normalized. Two clusters compare equal exactly
/// when their keys do — the Rust spelling of Swift's canonical
/// `Character` equality over `lowercased()` strings. The final sigma folds
/// to sigma (#1705, plan 47 CB-32), so `κοσμος`, `ΚΟΣΜΟΣ` and `κοσμοσ` all
/// meet a name however its sigma lowercases, as every name match in core
/// does ([`crate::name_fold`]).
fn grapheme_key(grapheme: &str) -> String {
    grapheme
        .chars()
        .nfc()
        .flat_map(char::to_lowercase)
        .map(crate::name_fold::fold_sigma)
        .nfc()
        .collect()
}

/// Rank and group `commands` for rendering.
///
/// **Empty query** — Recent section first (in `recent_ids` order,
/// skipping ids no longer registered), then every non-empty native
/// section in declared order with the Recent-shown ids excluded
/// (deduped flat display order). Input order is preserved within each
/// native section, except Sidebar, which reorders to
/// `sidebar_pinned_order` (the host's task-oriented catalog order —
/// host-supplied data, core-owned placement) with stragglers keeping
/// input order at the end.
///
/// **Non-empty query** — fuzzy-filter over label and accessibility
/// hint (best of the two scores; a row survives if either matches), in
/// best-match order (#1834, plan 47 CB-34): a row whose whole label is
/// the query comes first, then one whose name after the label's first
/// ": " is the query ("show outline" names "Canvas: Show Outline"), then
/// the higher score, then a label match before a hint-only match, then
/// the declared section, then the id. "Is the query" compares the
/// matcher's own grapheme keys, so case, canonical equivalence and the
/// final sigma fold as in [`fuzzy_score`], and a label's one trailing
/// `…` may be left off. Sections come in the order of their best row,
/// so two sections whose best rows tie on everything above keep the
/// declared order. No Recent section, no Sidebar catalog reorder: the
/// best match is the user's answer, and it is the first row.
pub fn palette_sections(
    commands: &[Command],
    query: &str,
    recent_ids: &[String],
    sidebar_pinned_order: &[String],
) -> Vec<PaletteSection> {
    if query.is_empty() {
        return empty_query_sections(commands, recent_ids, sidebar_pinned_order);
    }

    let query_keys = grapheme_keys(query);
    // Every tier is computed once per matched row, never inside the sort.
    let mut matched: Vec<RankedRow> = commands
        .iter()
        .filter_map(|command| {
            let section_rank = SECTION_ORDER
                .iter()
                .position(|&section| section == command.section)?;
            let label = fuzzy_score(query, &command.label);
            let hint = command
                .accessibility_hint
                .as_deref()
                .and_then(|hint| fuzzy_score(query, hint));
            let label_score = label.as_ref().map(|(s, _)| *s);
            let best = match (label_score, hint.map(|(s, _)| s)) {
                (Some(l), Some(h)) => l.max(h),
                (Some(l), None) => l,
                (None, Some(h)) => h,
                (None, None) => return None,
            };
            let label_matches = label.is_some();
            let label_match_spans = label.map(|(_, spans)| spans).unwrap_or_default();
            Some(RankedRow {
                names_label: names_exactly(&query_keys, &command.label),
                names_tail: command
                    .label
                    .split_once(": ")
                    .is_some_and(|(_, tail)| names_exactly(&query_keys, tail)),
                label_matches,
                section_rank,
                row: PaletteRow {
                    command: command.clone(),
                    label_match_spans,
                    score: best,
                },
            })
        })
        .collect();
    matched.sort_by(|l, r| {
        r.names_label
            .cmp(&l.names_label)
            .then_with(|| r.names_tail.cmp(&l.names_tail))
            .then_with(|| r.row.score.cmp(&l.row.score))
            .then_with(|| r.label_matches.cmp(&l.label_matches))
            .then_with(|| l.section_rank.cmp(&r.section_rank))
            .then_with(|| l.row.command.id.cmp(&r.row.command.id))
    });

    // Sections in the order their first row appears in that sort.
    let mut sections: Vec<PaletteSection> = Vec::new();
    for ranked in matched {
        let section = ranked.row.command.section;
        match sections.iter_mut().find(|s| s.kind == Some(section)) {
            Some(existing) => existing.rows.push(ranked.row),
            None => sections.push(PaletteSection {
                title: section_title(section).to_owned(),
                kind: Some(section),
                rows: vec![ranked.row],
            }),
        }
    }
    sections
}

/// One matched row with the tiers [`palette_sections`] sorts it by.
struct RankedRow {
    names_label: bool,
    names_tail: bool,
    label_matches: bool,
    section_rank: usize,
    row: PaletteRow,
}

/// The query's grapheme keys, as [`fuzzy_score`] compares them.
fn grapheme_keys(text: &str) -> Vec<String> {
    text.graphemes(true).map(grapheme_key).collect()
}

/// Whether the query (as `query_keys`) is `name` exactly, with `name`'s
/// one trailing `…` optional: "rename" names "Rename…".
fn names_exactly(query_keys: &[String], name: &str) -> bool {
    let name_keys = grapheme_keys(name);
    if name_keys == query_keys {
        return true;
    }
    match name_keys.split_last() {
        Some((last, rest)) if last == "\u{2026}" => rest == query_keys,
        _ => false,
    }
}

fn empty_query_sections(
    commands: &[Command],
    recent_ids: &[String],
    sidebar_pinned_order: &[String],
) -> Vec<PaletteSection> {
    let mut sections = Vec::new();

    // Recent first — invocation order, ids missing from the registry
    // skipped gracefully (a command id may have been removed across app
    // updates).
    let recent_rows: Vec<PaletteRow> = recent_ids
        .iter()
        .filter_map(|id| commands.iter().find(|c| &c.id == id))
        .map(|command| PaletteRow {
            command: command.clone(),
            label_match_spans: Vec::new(),
            score: 0,
        })
        .collect();
    if !recent_rows.is_empty() {
        sections.push(PaletteSection {
            title: RECENT_SECTION_TITLE.to_owned(),
            kind: None,
            rows: recent_rows,
        });
    }

    // Native sections — commands already shown in Recent are excluded
    // so the flat display order stays deduped. Exclusion uses the full
    // recent-id set (even ids the registry no longer knows).
    for &section in &SECTION_ORDER {
        let mut native: Vec<&Command> = commands
            .iter()
            .filter(|c| c.section == section && !recent_ids.contains(&c.id))
            .collect();
        if native.is_empty() {
            continue;
        }
        if section == CommandSection::Sidebar && !sidebar_pinned_order.is_empty() {
            let mut pinned: Vec<&Command> = sidebar_pinned_order
                .iter()
                .filter_map(|id| native.iter().find(|c| &c.id == id).copied())
                .collect();
            let in_pinned = |c: &&Command| sidebar_pinned_order.contains(&c.id);
            pinned.extend(native.iter().filter(|c| !in_pinned(c)).copied());
            native = pinned;
        }
        sections.push(PaletteSection {
            title: section_title(section).to_owned(),
            kind: Some(section),
            rows: native
                .into_iter()
                .map(|command| PaletteRow {
                    command: command.clone(),
                    label_match_spans: Vec::new(),
                    score: 0,
                })
                .collect(),
        });
    }
    sections
}

// ---------------------------------------------------------------------
// Ordered recents (format + transitions)
// ---------------------------------------------------------------------

/// Decode a recents file's bytes into the normalized id list.
///
/// Malformed-tolerant by contract: a corrupt, oversized (>
/// [`RECENTS_MAX_FILE_BYTES`]), or non-array file decodes as the empty
/// list — a bad file must never block the palette opening. Duplicate
/// ids (possible in a hand-edited file) dedupe to first-seen order so
/// the most-recent-first invariant survives external edits; the result
/// is capped at [`RECENTS_MAX_ENTRIES`].
pub fn recents_decode(bytes: &[u8]) -> Vec<String> {
    if bytes.len() > RECENTS_MAX_FILE_BYTES {
        log::warn!(
            "palette recents input exceeds the {RECENTS_MAX_FILE_BYTES}-byte \
             threshold; treating as malformed"
        );
        return Vec::new();
    }
    let Ok(decoded) = serde_json::from_slice::<Vec<String>>(bytes) else {
        return Vec::new();
    };
    let mut seen = std::collections::HashSet::new();
    let mut deduped = Vec::with_capacity(RECENTS_MAX_ENTRIES);
    for id in decoded {
        if seen.insert(id.clone()) {
            deduped.push(id);
            if deduped.len() >= RECENTS_MAX_ENTRIES {
                break;
            }
        }
    }
    deduped
}

/// Encode the id list in the on-disk format (v1: pretty-printed JSON
/// array — the shape the mac store has always written, so old and new
/// builds read each other's files).
pub fn recents_encode(ids: &[String]) -> Vec<u8> {
    let mut buf = Vec::new();
    let mut serializer = serde_json::Serializer::with_formatter(
        &mut buf,
        serde_json::ser::PrettyFormatter::with_indent(b"  "),
    );
    ids.serialize(&mut serializer)
        .expect("serializing Vec<String> to a Vec<u8> buffer cannot fail");
    buf
}

/// LRU add: any prior occurrence of `id` is removed, `id` moves to the
/// front, and the list is capped at [`RECENTS_MAX_ENTRIES`].
pub fn recents_add(ids: &[String], id: &str) -> Vec<String> {
    let mut next = Vec::with_capacity(ids.len() + 1);
    next.push(id.to_owned());
    next.extend(ids.iter().filter(|existing| *existing != id).cloned());
    next.truncate(RECENTS_MAX_ENTRIES);
    next
}

/// Remove every occurrence of `id` (no-op when absent).
pub fn recents_remove(ids: &[String], id: &str) -> Vec<String> {
    ids.iter()
        .filter(|existing| *existing != id)
        .cloned()
        .collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn cmd(id: &str, label: &str, section: CommandSection) -> Command {
        Command {
            id: id.to_owned(),
            label: label.to_owned(),
            accessibility_hint: None,
            hotkey_hint: None,
            section,
        }
    }

    fn cmd_hint(id: &str, label: &str, hint: &str, section: CommandSection) -> Command {
        Command {
            accessibility_hint: Some(hint.to_owned()),
            ..cmd(id, label, section)
        }
    }

    fn ids(sections: &[PaletteSection]) -> Vec<Vec<&str>> {
        sections
            .iter()
            .map(|s| s.rows.iter().map(|r| r.command.id.as_str()).collect())
            .collect()
    }

    // --- fuzzy_score: the mac unit tests, moved (goldens preserved) ---

    #[test]
    fn no_subsequence_match_returns_none() {
        assert_eq!(fuzzy_score("xyz", "Save"), None);
        assert_eq!(fuzzy_score("vault", "Save"), None);
    }

    #[test]
    fn matching_is_case_insensitive() {
        let a = fuzzy_score("save", "Save").map(|(s, _)| s);
        let b = fuzzy_score("SAVE", "save").map(|(s, _)| s);
        assert!(a.is_some());
        assert_eq!(a, b);
    }

    #[test]
    fn prefix_match_outranks_scattered_match() {
        let (prefix, _) = fuzzy_score("save", "Save").unwrap();
        let (scattered, _) = fuzzy_score("save", "Citations Are Visible Embeds").unwrap();
        assert!(prefix > scattered, "{prefix} vs {scattered}");
    }

    #[test]
    fn consecutive_match_outranks_split_match() {
        let (consecutive, _) = fuzzy_score("sa", "Save").unwrap();
        let (split, _) = fuzzy_score("sa", "Slate Add").unwrap();
        assert!(consecutive > split, "{consecutive} vs {split}");
    }

    #[test]
    fn word_boundary_match_outranks_mid_word_match() {
        let (boundary, _) = fuzzy_score("ts", "Tasks Review").unwrap();
        let (mid, _) = fuzzy_score("ts", "Citations Review").unwrap();
        assert!(boundary > mid, "{boundary} vs {mid}");
    }

    #[test]
    fn empty_query_scores_zero() {
        assert_eq!(fuzzy_score("", "Anything"), Some((0, Vec::new())));
    }

    // --- fuzzy_score: span reporting (new surface, pinned here) ---

    #[test]
    fn prefix_match_spans_cover_the_prefix() {
        let (_, spans) = fuzzy_score("save", "Save Note").unwrap();
        assert_eq!(
            spans,
            vec![MatchSpan {
                start_byte: 0,
                end_byte: 4
            }]
        );
    }

    #[test]
    fn scattered_match_spans_merge_adjacent_runs() {
        // "sa" over "Slate Add": the matcher is greedy (single forward
        // pass, same as the mac original) — S(0), then the first 'a'
        // available is the one inside "Slate" (2), NOT Add's 'A'.
        let (_, spans) = fuzzy_score("sa", "Slate Add").unwrap();
        assert_eq!(
            spans,
            vec![
                MatchSpan {
                    start_byte: 0,
                    end_byte: 1
                },
                MatchSpan {
                    start_byte: 2,
                    end_byte: 3
                },
            ]
        );
    }

    #[test]
    fn multibyte_labels_report_byte_offsets() {
        // "写" is 3 UTF-8 bytes; matching the following ascii char must
        // account for it.
        let (_, spans) = fuzzy_score("x", "写x").unwrap();
        assert_eq!(
            spans,
            vec![MatchSpan {
                start_byte: 3,
                end_byte: 4
            }]
        );
    }

    // --- canonical equivalence (Swift Character parity) ---

    #[test]
    fn composed_and_decomposed_forms_match_both_directions() {
        // Precomposed é (U+00E9) vs decomposed e + U+0301: Swift
        // Character equality treats them as equal; so does the key.
        let composed = "caf\u{e9}.md";
        let decomposed = "cafe\u{301}.md";
        assert!(fuzzy_score("caf\u{e9}", decomposed).is_some());
        assert!(fuzzy_score("cafe\u{301}", composed).is_some());
        // Scores agree regardless of which side is decomposed.
        assert_eq!(
            fuzzy_score("caf\u{e9}", composed).map(|(s, _)| s),
            fuzzy_score("caf\u{e9}", decomposed).map(|(s, _)| s),
        );
        // Case-insensitivity composes with canonical equivalence.
        assert!(fuzzy_score("CAF\u{c9}", decomposed).is_some());
    }

    #[test]
    fn decomposed_spans_cover_the_full_cluster_in_original_bytes() {
        // "e" + U+0301 is one grapheme, 3 bytes in the original target;
        // the span must cover all of it (bolding half a cluster would
        // corrupt the rendered label).
        let (_, spans) = fuzzy_score("\u{e9}", "e\u{301}x").unwrap();
        assert_eq!(
            spans,
            vec![MatchSpan {
                start_byte: 0,
                end_byte: 3
            }]
        );
    }

    #[test]
    fn bare_letter_does_not_match_an_accented_cluster() {
        // Swift parity: Character "e" != "é" in either composed form.
        assert_eq!(fuzzy_score("e", "\u{e9}"), None);
        assert_eq!(fuzzy_score("e", "e\u{301}"), None);
        // And the multi-scalar lowercase of İ (i + U+0307) is not a
        // bare "i" — the pre-fix scalar fold got this wrong.
        assert_eq!(fuzzy_score("i", "\u{130}"), None);
    }

    /// One segment of a random query/target, in canonically equivalent
    /// composed and decomposed spellings (identical for the ASCII arms).
    fn canonical_segment() -> impl proptest::strategy::Strategy<Value = (&'static str, &'static str)>
    {
        use proptest::prelude::*;
        prop_oneof![
            Just(("a", "a")),
            Just(("B", "B")),
            Just((" ", " ")),
            Just((".", ".")),
            Just(("_", "_")),
            Just(("\u{e9}", "e\u{301}")), // é composed / decomposed
            Just(("\u{c9}", "E\u{301}")), // É composed / decomposed
            Just(("\u{f1}", "n\u{303}")), // ñ composed / decomposed
            Just(("写", "写")),
        ]
    }

    /// A random string in both canonical spellings simultaneously.
    fn canonical_pair() -> impl proptest::strategy::Strategy<Value = (String, String)> {
        use proptest::prelude::*;
        proptest::collection::vec(canonical_segment(), 0..10).prop_map(|segments| {
            (
                segments.iter().map(|(c, _)| *c).collect(),
                segments.iter().map(|(_, d)| *d).collect(),
            )
        })
    }

    proptest::proptest! {
        #![proptest_config(proptest::prelude::ProptestConfig::with_cases(512))]

        /// Canonical equivalence is total for the matcher: any mix of
        /// composed/decomposed spellings on either side scores
        /// identically (match or non-match alike) — the property behind
        /// the Swift-parity goldens above.
        #[test]
        fn canonical_spellings_score_identically(
            (query_composed, query_decomposed) in canonical_pair(),
            (target_composed, target_decomposed) in canonical_pair(),
        ) {
            let reference = fuzzy_score(&query_composed, &target_composed).map(|(s, _)| s);
            for (q, t) in [
                (&query_composed, &target_decomposed),
                (&query_decomposed, &target_composed),
                (&query_decomposed, &target_decomposed),
            ] {
                proptest::prop_assert_eq!(fuzzy_score(q, t).map(|(s, _)| s), reference);
            }
        }
    }

    // --- exact-score goldens: the algorithm's arithmetic, pinned ---

    #[test]
    fn score_arithmetic_golden() {
        // "save" on "Save": 4×10 (chars) + 5 (boundary at start)
        // + 3×3 (consecutive) + 50 (prefix) = 104.
        assert_eq!(fuzzy_score("save", "Save").unwrap().0, 104);
        // "sa" on "Slate Add": greedy pass — S start-boundary (10+5),
        // then the mid-word 'a' of "Slate" (10) = 25; no prefix, no
        // consecutive, and Add's word-boundary 'A' is never reached.
        assert_eq!(fuzzy_score("sa", "Slate Add").unwrap().0, 25);
        // "sa" on "Save": 10+5 + 10+3 + 50 = 78.
        assert_eq!(fuzzy_score("sa", "Save").unwrap().0, 78);
    }

    // --- palette_sections: ranked (non-empty query) ---

    fn corpus() -> Vec<Command> {
        vec![
            cmd("slate.file.newNote", "New Note", CommandSection::File),
            cmd("slate.file.save", "Save", CommandSection::File),
            cmd(
                "slate.nav.quickOpen",
                "Quick Open",
                CommandSection::Navigation,
            ),
            cmd_hint(
                "slate.vault.rescan",
                "Rescan Vault",
                "Walk the vault and refresh the index",
                CommandSection::Vault,
            ),
            cmd("slate.editor.bold", "Toggle Bold", CommandSection::Editor),
            cmd("slate.sidebar.open", "Open", CommandSection::Sidebar),
            cmd("slate.sidebar.newNote", "New Note", CommandSection::Sidebar),
            cmd("slate.sidebar.rename", "Rename…", CommandSection::Sidebar),
            cmd("slate.tasks.review", "Tasks Review", CommandSection::Tasks),
        ]
    }

    fn titles(sections: &[PaletteSection]) -> Vec<&str> {
        sections.iter().map(|s| s.title.as_str()).collect()
    }

    fn score(query: &str, target: &str) -> i32 {
        fuzzy_score(query, target)
            .unwrap_or_else(|| panic!("premise: {query:?} matches {target:?}"))
            .0
    }

    #[test]
    fn ranked_query_sections_follow_their_best_row() {
        let sections = palette_sections(&corpus(), "ne", &[], &[]);
        // "ne" matches File's and the Sidebar's New Note by prefix (78
        // each), the Sidebar's Rename… by scattered letters, and Rescan
        // Vault by its hint ("and", "refresh"). The two New Notes tie, so
        // File keeps its declared place before the Sidebar; Vault's best
        // row is a weak hint match, so Vault comes last (#1834).
        assert_eq!(score("ne", "New Note"), 78);
        let got = ids(&sections);
        assert_eq!(titles(&sections), vec!["File", "Sidebar", "Vault"]);
        assert_eq!(got[0], vec!["slate.file.newNote"]);
        assert_eq!(
            got[1],
            vec!["slate.sidebar.newNote", "slate.sidebar.rename"]
        );
        assert_eq!(got[2], vec!["slate.vault.rescan"]);
    }

    #[test]
    fn an_exact_label_match_is_the_first_row_whatever_its_section() {
        // #1834's own case: "settings" also matches Close Tab's hint, and
        // View is declared long before Settings.
        let commands = vec![
            cmd_hint(
                "a.view.closeTab",
                "Close Tab",
                "Close the active tab. Prompts if it has unsaved changes.",
                CommandSection::View,
            ),
            cmd_hint(
                "z.settings.open",
                "Settings\u{2026}",
                "Open Settings.",
                CommandSection::Settings,
            ),
        ];
        let sections = palette_sections(&commands, "settings", &[], &[]);
        assert_eq!(titles(&sections), vec!["Settings", "View"]);
        assert_eq!(sections[0].rows[0].command.id, "z.settings.open");
    }

    #[test]
    fn sections_come_in_the_order_of_their_best_row() {
        let commands = vec![
            cmd("a.file.recent", "Recent Overview", CommandSection::File),
            cmd("b.editor.reveal", "Toggle Reveal", CommandSection::Editor),
            cmd("c.tasks.review", "Review Tasks", CommandSection::Tasks),
        ];
        // Premise: the best rows score in the reverse of the declared order.
        assert!(score("rev", "Review Tasks") > score("rev", "Toggle Reveal"));
        assert!(score("rev", "Toggle Reveal") > score("rev", "Recent Overview"));
        let sections = palette_sections(&commands, "rev", &[], &[]);
        assert_eq!(titles(&sections), vec!["Tasks", "Editor", "File"]);
    }

    #[test]
    fn sections_whose_best_rows_tie_keep_the_declared_order() {
        // Equal rows in Tasks and File, with ids that would put Tasks
        // first: the tie keeps the declared order, File before Tasks.
        let commands = vec![
            cmd("a.tasks.save", "Save", CommandSection::Tasks),
            cmd("z.file.save", "Save", CommandSection::File),
        ];
        let sections = palette_sections(&commands, "sav", &[], &[]);
        assert_eq!(titles(&sections), vec!["File", "Tasks"]);
    }

    #[test]
    fn an_exact_label_outranks_a_longer_label_with_the_same_prefix_score() {
        let commands = vec![
            cmd("a.vault.open", "Open Vault\u{2026}", CommandSection::File),
            cmd("b.file.open", "Open", CommandSection::File),
        ];
        assert_eq!(score("open", "Open Vault\u{2026}"), score("open", "Open"));
        let sections = palette_sections(&commands, "open", &[], &[]);
        assert_eq!(ids(&sections), vec![vec!["b.file.open", "a.vault.open"]]);
    }

    #[test]
    fn the_name_after_the_first_colon_is_an_exact_match() {
        // "show outline" is a prefix of the View row's whole label and only
        // a scattered match of the Canvas row's, so by score the View row
        // leads; the Canvas row's name after its "Canvas: " lead is the
        // query exactly.
        let commands = vec![
            cmd("a.view.outline", "Show Outline Panel", CommandSection::View),
            cmd(
                "z.canvas.outline",
                "Canvas: Show Outline",
                CommandSection::Canvas,
            ),
        ];
        assert!(
            score("show outline", "Show Outline Panel")
                > score("show outline", "Canvas: Show Outline")
        );
        let sections = palette_sections(&commands, "show outline", &[], &[]);
        assert_eq!(sections[0].rows[0].command.id, "z.canvas.outline");

        // A whole label that is the query still comes before a tail.
        let commands = vec![
            cmd(
                "a.canvas.duplicate",
                "Canvas: Duplicate",
                CommandSection::Canvas,
            ),
            cmd("z.sidebar.duplicate", "Duplicate", CommandSection::Sidebar),
        ];
        let sections = palette_sections(&commands, "duplicate", &[], &[]);
        assert_eq!(sections[0].rows[0].command.id, "z.sidebar.duplicate");
    }

    #[test]
    fn a_trailing_ellipsis_and_the_matchers_fold_do_not_stop_an_exact_match() {
        // Each competitor scores the same as the named row and has the
        // smaller id, so only the exact tier puts the named row first.
        let cases: [(&str, &str, &str); 4] = [
            ("rename", "Rename Group\u{2026}", "Rename\u{2026}"),
            ("RENAME", "Rename Group\u{2026}", "Rename\u{2026}"),
            // Canonical equivalence: a decomposed é names a composed one.
            ("cafe\u{301}", "Caf\u{e9} Menu", "Caf\u{e9}"),
            // The final sigma (CB-32): ΚΟΣΜΟΣ names κοσμος.
            (
                "\u{39a}\u{39f}\u{3a3}\u{39c}\u{39f}\u{3a3}",
                "\u{3ba}\u{3bf}\u{3c3}\u{3bc}\u{3bf}\u{3c3} \u{3c7}",
                "\u{3ba}\u{3bf}\u{3c3}\u{3bc}\u{3bf}\u{3c2}",
            ),
        ];
        for (query, competitor, named) in cases {
            assert_eq!(
                score(query, competitor),
                score(query, named),
                "premise: {query:?} scores {competitor:?} and {named:?} alike"
            );
            let commands = vec![
                cmd("a.competitor", competitor, CommandSection::File),
                cmd("z.named", named, CommandSection::File),
            ];
            let sections = palette_sections(&commands, query, &[], &[]);
            assert_eq!(
                sections[0].rows[0].command.id, "z.named",
                "{query:?} names {named:?}"
            );
        }
    }

    #[test]
    fn a_label_match_outranks_a_hint_only_match_of_equal_score() {
        let commands = vec![
            cmd_hint(
                "a.file.archive",
                "Archive",
                "Notes in the archive",
                CommandSection::File,
            ),
            cmd("b.file.notesIndex", "Notes Index", CommandSection::File),
        ];
        assert_eq!(
            score("notes", "Notes in the archive"),
            score("notes", "Notes Index")
        );
        assert!(fuzzy_score("notes", "Archive").is_none());
        let sections = palette_sections(&commands, "notes", &[], &[]);
        assert_eq!(
            ids(&sections),
            vec![vec!["b.file.notesIndex", "a.file.archive"]]
        );
    }

    #[test]
    fn ranked_ties_break_by_id_ascending() {
        // Identical labels in the same section score identically.
        let commands = vec![
            cmd("b.second", "Same Label", CommandSection::File),
            cmd("a.first", "Same Label", CommandSection::File),
        ];
        let sections = palette_sections(&commands, "same", &[], &[]);
        assert_eq!(ids(&sections), vec![vec!["a.first", "b.second"]]);
    }

    #[test]
    fn hint_only_match_survives_with_empty_label_spans() {
        let sections = palette_sections(&corpus(), "walk", &[], &[]);
        assert_eq!(ids(&sections), vec![vec!["slate.vault.rescan"]]);
        assert!(sections[0].rows[0].label_match_spans.is_empty());
    }

    #[test]
    fn label_spans_populated_when_label_matches() {
        let sections = palette_sections(&corpus(), "save", &[], &[]);
        let row = &sections[0].rows[0];
        assert_eq!(row.command.id, "slate.file.save");
        assert_eq!(
            row.label_match_spans,
            vec![MatchSpan {
                start_byte: 0,
                end_byte: 4
            }]
        );
    }

    #[test]
    fn the_display_leads_with_the_strongest_match() {
        // #1834: a weak hint-only match in an early section no longer comes
        // before a strong label match in a later one. The display itself is
        // the best-match order, so no host re-sorts by score.
        let commands = vec![
            cmd_hint(
                "a.file.print",
                "Print Note",
                "Print or save the note",
                CommandSection::File,
            ),
            cmd("z.editor.save", "Save", CommandSection::Editor),
        ];
        let sections = palette_sections(&commands, "save", &[], &[]);
        assert_eq!(
            ids(&sections),
            vec![vec!["z.editor.save"], vec!["a.file.print"]],
        );
        assert!(
            sections[0].rows[0].score > sections[1].rows[0].score,
            "{} vs {}",
            sections[0].rows[0].score,
            sections[1].rows[0].score
        );
    }

    #[test]
    fn ranked_query_ignores_recents_and_sidebar_pinning() {
        let recents = vec!["slate.tasks.review".to_owned()];
        let pinned = vec!["slate.sidebar.rename".to_owned()];
        let sections = palette_sections(&corpus(), "ne", &recents, &pinned);
        assert!(
            sections.iter().all(|s| s.kind.is_some()),
            "no Recent section"
        );
        // Sidebar order still score-ranked, not catalog-pinned.
        let sidebar = sections.iter().find(|s| s.title == "Sidebar").unwrap();
        assert_eq!(sidebar.rows[0].command.id, "slate.sidebar.newNote");
    }

    // --- palette_sections: empty query ---

    #[test]
    fn empty_query_recent_first_with_native_exclusion() {
        let recents = vec![
            "slate.editor.bold".to_owned(),
            "slate.file.save".to_owned(),
            "gone.command".to_owned(), // removed across app updates: skipped
        ];
        let sections = palette_sections(&corpus(), "", &recents, &[]);
        assert_eq!(sections[0].title, "Recent");
        assert_eq!(sections[0].kind, None);
        assert_eq!(
            ids(&sections)[0],
            vec!["slate.editor.bold", "slate.file.save"],
        );
        // Excluded from their native sections; Editor section vanishes
        // entirely (bold was its only command).
        assert!(!sections.iter().any(|s| s.title == "Editor"));
        let file = sections.iter().find(|s| s.title == "File").unwrap();
        assert_eq!(
            file.rows
                .iter()
                .map(|r| r.command.id.as_str())
                .collect::<Vec<_>>(),
            vec!["slate.file.newNote"],
        );
    }

    #[test]
    fn empty_query_without_recents_has_no_recent_section() {
        let sections = palette_sections(&corpus(), "", &[], &[]);
        assert_eq!(sections[0].title, "File");
        assert!(sections.iter().all(|s| s.kind.is_some()));
    }

    #[test]
    fn empty_query_preserves_input_order_within_sections() {
        // Deliberately not (section, id)-sorted input: the caller's
        // snapshot order is the display contract (mirrors the mac
        // model's Dictionary(grouping:) semantics).
        let commands = vec![
            cmd("z.last", "Zed", CommandSection::File),
            cmd("a.first", "Aye", CommandSection::File),
        ];
        let sections = palette_sections(&commands, "", &[], &[]);
        assert_eq!(ids(&sections), vec![vec!["z.last", "a.first"]]);
    }

    #[test]
    fn empty_query_sidebar_respects_pinned_catalog_order() {
        let pinned = vec![
            "slate.sidebar.open".to_owned(),
            "slate.sidebar.newNote".to_owned(),
            "slate.sidebar.rename".to_owned(),
            "slate.sidebar.notRegistered".to_owned(), // ignored
        ];
        let sections = palette_sections(&corpus(), "", &[], &pinned);
        let sidebar = sections.iter().find(|s| s.title == "Sidebar").unwrap();
        assert_eq!(
            sidebar
                .rows
                .iter()
                .map(|r| r.command.id.as_str())
                .collect::<Vec<_>>(),
            vec![
                "slate.sidebar.open",
                "slate.sidebar.newNote",
                "slate.sidebar.rename"
            ],
        );

        // Stragglers not in the catalog keep input order at the end.
        let partial = vec!["slate.sidebar.rename".to_owned()];
        let sections = palette_sections(&corpus(), "", &[], &partial);
        let sidebar = sections.iter().find(|s| s.title == "Sidebar").unwrap();
        assert_eq!(
            sidebar
                .rows
                .iter()
                .map(|r| r.command.id.as_str())
                .collect::<Vec<_>>(),
            vec![
                "slate.sidebar.rename",
                "slate.sidebar.open",
                "slate.sidebar.newNote"
            ],
        );
    }

    #[test]
    fn empty_query_recent_exclusion_applies_before_sidebar_pinning() {
        let recents = vec!["slate.sidebar.open".to_owned()];
        let pinned = vec![
            "slate.sidebar.open".to_owned(),
            "slate.sidebar.newNote".to_owned(),
            "slate.sidebar.rename".to_owned(),
        ];
        let sections = palette_sections(&corpus(), "", &recents, &pinned);
        let sidebar = sections.iter().find(|s| s.title == "Sidebar").unwrap();
        assert_eq!(
            sidebar
                .rows
                .iter()
                .map(|r| r.command.id.as_str())
                .collect::<Vec<_>>(),
            vec!["slate.sidebar.newNote", "slate.sidebar.rename"],
        );
    }

    // --- recents: decode / encode / transitions ---

    #[test]
    fn decode_malformed_and_oversized_inputs_yield_empty() {
        assert!(recents_decode(b"not json").is_empty());
        assert!(recents_decode(b"{\"wrong\": \"shape\"}").is_empty());
        assert!(recents_decode(b"[1, 2, 3]").is_empty());
        let oversized = vec![b' '; RECENTS_MAX_FILE_BYTES + 1];
        assert!(recents_decode(&oversized).is_empty());
        // Exactly at the cap is NOT oversized (boundary pinned).
        let mut at_cap = b"[\"a\"]".to_vec();
        at_cap.resize(RECENTS_MAX_FILE_BYTES, b' ');
        assert_eq!(recents_decode(&at_cap), vec!["a".to_owned()]);
    }

    #[test]
    fn decode_dedupes_first_seen_and_caps() {
        let input = br#"["a", "b", "a", "c", "b", "d"]"#;
        assert_eq!(recents_decode(input), vec!["a", "b", "c", "d"]);

        let many: Vec<String> = (0..20).map(|i| format!("id{i}")).collect();
        let bytes = serde_json::to_vec(&many).unwrap();
        let decoded = recents_decode(&bytes);
        assert_eq!(decoded.len(), RECENTS_MAX_ENTRIES);
        assert_eq!(decoded[0], "id0");
        assert_eq!(decoded[9], "id9");
    }

    #[test]
    fn encode_round_trips_and_reads_the_mac_store_shape() {
        let ids = vec![
            "slate.file.save".to_owned(),
            "slate.nav.quickOpen".to_owned(),
        ];
        let bytes = recents_encode(&ids);
        assert_eq!(recents_decode(&bytes), ids);
        // The mac store's JSONEncoder(.prettyPrinted) shape parses too.
        let swift_shape = b"[\n  \"slate.file.save\",\n  \"slate.nav.quickOpen\"\n]";
        assert_eq!(recents_decode(swift_shape), ids);
    }

    #[test]
    fn add_moves_to_front_dedupes_and_caps() {
        let start: Vec<String> = (0..RECENTS_MAX_ENTRIES).map(|i| format!("id{i}")).collect();
        let bumped = recents_add(&start, "id7");
        assert_eq!(bumped.len(), RECENTS_MAX_ENTRIES);
        assert_eq!(bumped[0], "id7");
        assert_eq!(bumped.iter().filter(|id| *id == "id7").count(), 1);

        let grown = recents_add(&start, "fresh");
        assert_eq!(grown.len(), RECENTS_MAX_ENTRIES);
        assert_eq!(grown[0], "fresh");
        assert!(!grown.contains(&format!("id{}", RECENTS_MAX_ENTRIES - 1)));
    }

    #[test]
    fn remove_is_total_and_tolerates_absent_ids() {
        let start = vec!["a".to_owned(), "b".to_owned(), "a".to_owned()];
        assert_eq!(recents_remove(&start, "a"), vec!["b"]);
        assert_eq!(recents_remove(&start, "zzz"), start);
    }
}
