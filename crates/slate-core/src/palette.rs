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
//!   host-pinned within-Sidebar catalog order, and score-ranked
//!   non-empty-query grouping;
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
/// label and hint scores (0 on an empty query) so hosts can derive the
/// global ranked order across sections without re-scoring.
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
/// matched byte spans in `target` (merged over adjacent characters).
///
/// Scoring (the mac palette's semantics, now the reference):
/// - +10 per matched character
/// - +5 when the match lands at a word boundary (start of string or
///   after space / `.` / `-` / `:` / `_`)
/// - +3 when the match is consecutive with the previous match
/// - +50 when the query is a strict case-insensitive prefix of the
///   target
///
/// Comparison is case-insensitive over Unicode scalars using simple
/// one-to-one folding (the first scalar of `char::to_lowercase`).
/// Multi-scalar expansions (ß → ss) intentionally fold to their first
/// scalar — a deliberate semantic pin now that core is the reference;
/// the goldens below capture it. An empty query scores 0 with no spans.
pub fn fuzzy_score(query: &str, target: &str) -> Option<(i32, Vec<MatchSpan>)> {
    if query.is_empty() {
        return Some((0, Vec::new()));
    }
    let q: Vec<char> = query.chars().map(fold).collect();

    let mut qi = 0usize;
    let mut consecutive = 0u32;
    let mut score = 0i32;
    let mut spans: Vec<MatchSpan> = Vec::new();
    let mut prev: Option<char> = None;
    let mut target_chars = 0usize;
    let mut folded_prefix_matches = true;

    for (ti, (byte, ch)) in target.char_indices().enumerate() {
        target_chars += 1;
        let folded = fold(ch);
        if ti < q.len() && folded_prefix_matches && folded != q[ti] {
            folded_prefix_matches = false;
        }
        if qi < q.len() && folded == q[qi] {
            score += 10;
            let boundary = match prev {
                None => true,
                Some(p) => WORD_BOUNDARY.contains(&p),
            };
            if boundary {
                score += 5;
            }
            if consecutive > 0 {
                score += 3;
            }
            consecutive += 1;
            qi += 1;

            let end = byte + ch.len_utf8();
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
        prev = Some(ch);
    }

    if qi != q.len() {
        return None;
    }
    if folded_prefix_matches && q.len() <= target_chars {
        score += 50;
    }
    Some((score, spans))
}

/// Word-boundary characters for the +5 bonus: the punctuation a command
/// label or scraped hint id might reasonably use.
const WORD_BOUNDARY: [char; 5] = [' ', '.', '-', ':', '_'];

/// Simple one-to-one case fold (first scalar of the full lowercase
/// expansion).
fn fold(c: char) -> char {
    c.to_lowercase().next().unwrap_or(c)
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
/// hint (best of the two scores; a row survives if either matches),
/// globally sorted by descending score with id as the stable
/// tiebreaker, grouped into native sections in declared order. No
/// Recent section, no Sidebar catalog reorder: score order is the
/// user's answer.
pub fn palette_sections(
    commands: &[Command],
    query: &str,
    recent_ids: &[String],
    sidebar_pinned_order: &[String],
) -> Vec<PaletteSection> {
    if query.is_empty() {
        return empty_query_sections(commands, recent_ids, sidebar_pinned_order);
    }

    let mut matched: Vec<(i32, PaletteRow)> = commands
        .iter()
        .filter_map(|command| {
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
            let label_match_spans = label.map(|(_, spans)| spans).unwrap_or_default();
            Some((
                best,
                PaletteRow {
                    command: command.clone(),
                    label_match_spans,
                    score: best,
                },
            ))
        })
        .collect();
    matched.sort_by(|(ls, lrow), (rs, rrow)| {
        rs.cmp(ls)
            .then_with(|| lrow.command.id.cmp(&rrow.command.id))
    });

    SECTION_ORDER
        .iter()
        .filter_map(|&section| {
            let rows: Vec<PaletteRow> = matched
                .iter()
                .filter(|(_, row)| row.command.section == section)
                .map(|(_, row)| row.clone())
                .collect();
            if rows.is_empty() {
                return None;
            }
            Some(PaletteSection {
                title: section_title(section).to_owned(),
                kind: Some(section),
                rows,
            })
        })
        .collect()
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

    #[test]
    fn ranked_query_groups_by_section_in_declared_order() {
        let sections = palette_sections(&corpus(), "ne", &[], &[]);
        // "ne" matches New Note (File), Rename…? r-e-n... "ne"
        // subsequence: New Note prefix "Ne" strongly; sidebar New Note;
        // Rename… (n at 2? "Rename…": R,e,n,a,m,e — 'n' at idx 2, 'e'
        // at idx 5); Quick Open ('n' in Open? O-p-e-n: n at end, then
        // needs 'e' after — none → no match); Rescan Vault hint? "Walk
        // the vault and refresh the index" — 'n' in "and", 'e' in
        // "refresh" → matches via hint.
        let got = ids(&sections);
        // Sections present in declared order: File, Vault (hint match),
        // Sidebar.
        assert_eq!(
            sections
                .iter()
                .map(|s| s.title.as_str())
                .collect::<Vec<_>>(),
            vec!["File", "Vault", "Sidebar"],
        );
        // File: only "New Note" (Save has no n).
        assert_eq!(got[0], vec!["slate.file.newNote"]);
        // Sidebar: prefix-scored New Note first, then Rename….
        assert_eq!(
            got[2],
            vec!["slate.sidebar.newNote", "slate.sidebar.rename"]
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
    fn scores_expose_the_global_ranked_order_across_sections() {
        // The display is section-grouped, but hosts also need "strongest
        // match overall" (the mac model's filteredCommands contract): a
        // weak hint-only match in an early section must not outrank a
        // strong label match in a later one once rows are re-sorted by
        // the exposed score.
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
            vec![vec!["a.file.print"], vec!["z.editor.save"]],
            "display stays section-grouped",
        );
        let mut rows: Vec<&PaletteRow> = sections.iter().flat_map(|s| s.rows.iter()).collect();
        rows.sort_by(|l, r| {
            r.score
                .cmp(&l.score)
                .then_with(|| l.command.id.cmp(&r.command.id))
        });
        assert_eq!(rows[0].command.id, "z.editor.save");
        assert!(
            rows[0].score > rows[1].score,
            "{} vs {}",
            rows[0].score,
            rows[1].score
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
