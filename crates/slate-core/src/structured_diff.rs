// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Structured, screen-reader-first diffs between two versions of a
//! note (O-4 #542) — the `05` §7.3/§7.4 data plumbing, single-timeline
//! form (the conflict-specific local/remote split stays V2).
//!
//! The engine is **pure** (two strings in, one [`StructuredDiff`]
//! out), **total** (every input pair produces a diff without
//! panicking — worst case one `Other` per unmatched block), and
//! **deterministic** (same inputs → identical output; ordered
//! structures only).
//!
//! Pipeline:
//! 1. Frontmatter: key-level compare of the two parsed property sets →
//!    `PropertySet` / `PropertyRemoved` operations.
//! 2. Body: segment both sides with
//!    [`crate::reading::reading_blocks_source`] (the pure walker),
//!    align by LCS over `(coarse kind, content)`.
//! 3. Deterministic pairing for unmatched runs: between consecutive
//!    LCS anchors, the i-th removed block pairs with the i-th added
//!    block **iff** same coarse kind and normalized-edit-distance
//!    similarity > 0.6 (pinned by fixtures) → `Edited`; everything
//!    unpaired → `Removed`/`Added`. No cross-run pairing, no
//!    reordering detection — a moved block reads as remove + add
//!    (documented).
//! 4. Class mapping + normative description copy (§7.3: named
//!    operations, "Added heading 'Goals' at line 10" — never a
//!    side-by-side text dump).

use crate::frontmatter::{PropertyValue, extract_frontmatter};
use crate::reading::{ReadingBlock, ReadingBlockKind, reading_blocks_source};

/// One version-to-version structured diff.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct StructuredDiff {
    pub file_path: String,
    pub from_hash: String,
    pub to_hash: String,
    /// Document order.
    pub operations: Vec<DiffOperation>,
    /// "5 changes: 2 property changes, 2 added paragraphs, 1 heading
    /// edit." — count first, then by-class breakdown, largest first.
    pub audio_summary: String,
}

/// One named operation.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DiffOperation {
    pub kind: DiffOpClass,
    /// 1-based first line of the affected block — in the TO version;
    /// FROM for pure removals.
    pub line: u32,
    /// 1-based last line (inclusive); == `line` for one-liners.
    pub line_end: u32,
    /// e.g. "Added heading 'Goals' at line 10".
    pub semantic_description: String,
    /// e.g. the inserted text, truncated to 200 chars.
    pub detail: Option<String>,
}

/// Operation classes (the icon/tint families for O-5).
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum DiffOpClass {
    HeadingAdded,
    HeadingRemoved,
    HeadingEdited,
    PropertySet,
    PropertyRemoved,
    ParagraphAdded,
    ParagraphRemoved,
    ParagraphEdited,
    ListItemAdded,
    ListItemRemoved,
    ListItemEdited,
    TaskStatusChanged,
    CodeBlockEdited,
    MathBlockEdited,
    DiagramEdited,
    TableEdited,
    Other,
}

/// Coarse block families for LCS keys and pairing ("same kind"
/// ignores parameters like heading level or list depth, so a level
/// change reads as an edit, not remove+add).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Coarse {
    Heading,
    Paragraph,
    ListItem,
    Code,
    Math,
    Diagram,
    Table,
    Break,
}

fn coarse(kind: &ReadingBlockKind) -> Coarse {
    match kind {
        ReadingBlockKind::Heading { .. } => Coarse::Heading,
        ReadingBlockKind::Paragraph | ReadingBlockKind::BlockQuote { .. } | ReadingBlockKind::Html => {
            Coarse::Paragraph
        }
        ReadingBlockKind::ListItem { .. } => Coarse::ListItem,
        ReadingBlockKind::CodeFence { .. } => Coarse::Code,
        ReadingBlockKind::MathBlock => Coarse::Math,
        ReadingBlockKind::Diagram { .. } => Coarse::Diagram,
        ReadingBlockKind::Table => Coarse::Table,
        ReadingBlockKind::ThematicBreak => Coarse::Break,
    }
}

/// Truncate to at most `max` chars on a char boundary, appending `…`
/// when shortened.
fn truncate_chars(text: &str, max: usize) -> String {
    if text.chars().count() <= max {
        return text.to_string();
    }
    let cut: String = text.chars().take(max).collect();
    format!("{cut}…")
}

/// A one-line, whitespace-normalized excerpt of a block for
/// descriptions.
fn excerpt(source: &str, max: usize) -> String {
    truncate_chars(source.split_whitespace().collect::<Vec<_>>().join(" ").trim(), max)
}

/// Heading text sans marker characters.
fn heading_text(source: &str) -> String {
    let line = source.lines().next().unwrap_or("");
    excerpt(line.trim_start_matches('#').trim(), 60)
}

/// Task text sans the `- [x] ` prefix.
fn task_text(source: &str) -> String {
    let line = source.lines().next().unwrap_or("").trim_start();
    let after = line
        .trim_start_matches(['-', '*', '+'])
        .trim_start()
        .strip_prefix('[')
        .and_then(|rest| rest.char_indices().nth(1).map(|(i, _)| &rest[i..]))
        .and_then(|rest| rest.strip_prefix(']'))
        .map(str::trim_start)
        .unwrap_or(line);
    truncate_chars(after, 60)
}

/// Display form of a property value for descriptions.
fn display_value(value: &PropertyValue) -> String {
    match value {
        PropertyValue::Text(s)
        | PropertyValue::Date(s)
        | PropertyValue::Datetime(s)
        | PropertyValue::Wikilink(s) => s.clone(),
        PropertyValue::Integer(n) => n.to_string(),
        PropertyValue::Float(f) => f.to_string(),
        PropertyValue::Boolean(b) => b.to_string(),
        PropertyValue::List(items) => {
            let parts: Vec<String> = items.iter().map(display_value).collect();
            format!("[{}]", parts.join(", "))
        }
        PropertyValue::TagList(tags) => format!("[{}]", tags.join(", ")),
    }
}

/// Normalized similarity in `[0, 1]`: `1 − levenshtein/max_len`.
/// Inputs are capped at 2048 chars (prefix sample) so pathological
/// single-block edits stay bounded — deterministic either way, and the
/// 0.6 pairing threshold is pinned by fixtures.
fn similarity(a: &str, b: &str) -> f64 {
    const CAP: usize = 2048;
    let a: Vec<char> = a.chars().take(CAP).collect();
    let b: Vec<char> = b.chars().take(CAP).collect();
    let max_len = a.len().max(b.len());
    if max_len == 0 {
        return 1.0;
    }
    // Single-row Levenshtein.
    let mut prev: Vec<usize> = (0..=b.len()).collect();
    let mut current = vec![0usize; b.len() + 1];
    for (i, ca) in a.iter().enumerate() {
        current[0] = i + 1;
        for (j, cb) in b.iter().enumerate() {
            let substitution = prev[j] + usize::from(ca != cb);
            current[j + 1] = substitution.min(prev[j + 1] + 1).min(current[j] + 1);
        }
        std::mem::swap(&mut prev, &mut current);
    }
    1.0 - prev[b.len()] as f64 / max_len as f64
}

/// 1-based line of a byte offset in `source`.
fn line_of(source: &str, byte: usize) -> u32 {
    let byte = byte.min(source.len());
    source[..byte].bytes().filter(|b| *b == b'\n').count() as u32 + 1
}

/// `(line, line_end)` of a block within its source.
fn block_lines(source: &str, block: &ReadingBlock) -> (u32, u32) {
    let line = line_of(source, block.byte_start as usize);
    let inner_newlines = block.source.trim_end_matches('\n').bytes().filter(|b| *b == b'\n').count() as u32;
    (line, line + inner_newlines)
}

/// The status char of a task list item, if any.
fn task_status(kind: &ReadingBlockKind) -> Option<char> {
    match kind {
        ReadingBlockKind::ListItem { task, .. } => *task,
        _ => None,
    }
}

/// Do two task sources differ ONLY in the status character?
fn only_status_differs(from: &str, to: &str) -> bool {
    let strip = |s: &str| -> Option<(String, char)> {
        let open = s.find('[')?;
        let mut chars = s[open + 1..].chars();
        let status = chars.next()?;
        let rest = chars.as_str().strip_prefix(']')?;
        Some((format!("{}{}", &s[..open], rest), status))
    };
    match (strip(from), strip(to)) {
        (Some((f_rest, f_status)), Some((t_rest, t_status))) => {
            f_rest == t_rest && f_status != t_status
        }
        _ => false,
    }
}

/// Compute the structured diff between two full note sources.
pub fn structured_diff(
    file_path: &str,
    from_hash: &str,
    to_hash: &str,
    from: &str,
    to: &str,
) -> StructuredDiff {
    let mut operations: Vec<DiffOperation> = Vec::new();

    // --- 1. Frontmatter: key-level compare -------------------------
    let (from_props, _) = extract_frontmatter(from);
    let (to_props, _) = extract_frontmatter(to);
    let from_map: std::collections::BTreeMap<&str, &PropertyValue> =
        from_props.iter().map(|p| (p.key.as_str(), &p.value)).collect();
    let to_map: std::collections::BTreeMap<&str, &PropertyValue> =
        to_props.iter().map(|p| (p.key.as_str(), &p.value)).collect();

    // Best-effort 1-based line of `key:` within a source's frontmatter.
    let key_line = |source: &str, key: &str| -> u32 {
        let top = key.split('.').next().unwrap_or(key);
        for (i, line) in source.lines().enumerate() {
            let trimmed = line.trim_start();
            if trimmed
                .strip_prefix(top)
                .is_some_and(|rest| rest.trim_start().starts_with(':'))
            {
                return i as u32 + 1;
            }
        }
        1
    };

    for (key, to_value) in &to_map {
        let changed = from_map.get(key) != Some(to_value);
        if changed {
            let value = display_value(to_value);
            let line = key_line(to, key);
            operations.push(DiffOperation {
                kind: DiffOpClass::PropertySet,
                line,
                line_end: line,
                semantic_description: format!(
                    "Set property '{key}' to '{}'",
                    truncate_chars(&value, 60)
                ),
                detail: Some(truncate_chars(&value, 200)),
            });
        }
    }
    for key in from_map.keys() {
        if !to_map.contains_key(key) {
            let line = key_line(from, key);
            operations.push(DiffOperation {
                kind: DiffOpClass::PropertyRemoved,
                line,
                line_end: line,
                semantic_description: format!("Removed property '{key}'"),
                detail: None,
            });
        }
    }

    // --- 2. Body: segment + LCS align -------------------------------
    let from_blocks = reading_blocks_source(from);
    let to_blocks = reading_blocks_source(to);

    // Trim the common prefix/suffix first (typical edits touch a small
    // region; this keeps the LCS table tiny).
    let same = |a: &ReadingBlock, b: &ReadingBlock| coarse(&a.kind) == coarse(&b.kind) && a.source == b.source;
    let mut prefix = 0usize;
    while prefix < from_blocks.len()
        && prefix < to_blocks.len()
        && same(&from_blocks[prefix], &to_blocks[prefix])
    {
        prefix += 1;
    }
    let mut suffix = 0usize;
    while suffix < from_blocks.len() - prefix
        && suffix < to_blocks.len() - prefix
        && same(
            &from_blocks[from_blocks.len() - 1 - suffix],
            &to_blocks[to_blocks.len() - 1 - suffix],
        )
    {
        suffix += 1;
    }
    let from_mid = &from_blocks[prefix..from_blocks.len() - suffix];
    let to_mid = &to_blocks[prefix..to_blocks.len() - suffix];

    // LCS anchors over the middle (bounded: beyond ~4M cells, skip
    // anchoring entirely — the whole middle becomes one removed run +
    // one added run; total, deterministic, just coarser).
    let anchors: Vec<(usize, usize)> = if from_mid.len() * to_mid.len() <= 4_000_000 {
        lcs_pairs(from_mid, to_mid, same)
    } else {
        Vec::new()
    };

    // Walk runs between anchors.
    let mut fi = 0usize;
    let mut ti = 0usize;
    let mut emit_run = |operations: &mut Vec<DiffOperation>,
                        removed: &[ReadingBlock],
                        added: &[ReadingBlock]| {
        let pairs = removed.len().min(added.len());
        for i in 0..pairs {
            let from_block = &removed[i];
            let to_block = &added[i];
            let pairable = coarse(&from_block.kind) == coarse(&to_block.kind)
                && similarity(&from_block.source, &to_block.source) > 0.6;
            if pairable {
                operations.push(edited_op(to, from_block, to_block));
            } else {
                operations.push(removed_op(from, from_block));
                operations.push(added_op(to, to_block));
            }
        }
        for from_block in &removed[pairs..] {
            operations.push(removed_op(from, from_block));
        }
        for to_block in &added[pairs..] {
            operations.push(added_op(to, to_block));
        }
    };
    for (anchor_from, anchor_to) in anchors.iter().copied().chain(std::iter::once((
        from_mid.len(),
        to_mid.len(),
    ))) {
        emit_run(&mut operations, &from_mid[fi..anchor_from], &to_mid[ti..anchor_to]);
        fi = (anchor_from + 1).min(from_mid.len());
        ti = (anchor_to + 1).min(to_mid.len());
    }

    // Document order: sort by line (stable — properties first at equal
    // lines, preserving emit order otherwise).
    operations.sort_by_key(|op| op.line);

    let audio_summary = audio_summary(&operations);
    StructuredDiff {
        file_path: file_path.to_string(),
        from_hash: from_hash.to_string(),
        to_hash: to_hash.to_string(),
        operations,
        audio_summary,
    }
}

/// LCS over the two block slices, returning matched index pairs in
/// order. Classic DP; the caller bounds the table size.
fn lcs_pairs(
    from: &[ReadingBlock],
    to: &[ReadingBlock],
    same: impl Fn(&ReadingBlock, &ReadingBlock) -> bool,
) -> Vec<(usize, usize)> {
    let n = from.len();
    let m = to.len();
    if n == 0 || m == 0 {
        return Vec::new();
    }
    let mut table = vec![0u32; (n + 1) * (m + 1)];
    let idx = |i: usize, j: usize| i * (m + 1) + j;
    for i in (0..n).rev() {
        for j in (0..m).rev() {
            table[idx(i, j)] = if same(&from[i], &to[j]) {
                table[idx(i + 1, j + 1)] + 1
            } else {
                table[idx(i + 1, j)].max(table[idx(i, j + 1)])
            };
        }
    }
    let mut pairs = Vec::new();
    let (mut i, mut j) = (0usize, 0usize);
    while i < n && j < m {
        if same(&from[i], &to[j]) {
            pairs.push((i, j));
            i += 1;
            j += 1;
        } else if table[idx(i + 1, j)] >= table[idx(i, j + 1)] {
            i += 1;
        } else {
            j += 1;
        }
    }
    pairs
}

fn added_op(to_source: &str, block: &ReadingBlock) -> DiffOperation {
    let (line, line_end) = block_lines(to_source, block);
    let (kind, noun) = add_class(&block.kind);
    let semantic_description = match &block.kind {
        ReadingBlockKind::Heading { .. } => {
            format!("Added heading '{}' at line {line}", heading_text(&block.source))
        }
        ReadingBlockKind::ListItem { task: Some(status), .. } => {
            return DiffOperation {
                kind,
                line,
                line_end,
                semantic_description: format!(
                    "Added task '{}' at line {line}",
                    task_text(&block.source)
                ),
                detail: Some(truncate_chars(&block.source, 200)),
            }
            .with_task_add_kind(*status);
        }
        _ => format!("Added {noun} at line {line}"),
    };
    DiffOperation {
        kind,
        line,
        line_end,
        semantic_description,
        detail: Some(truncate_chars(&block.source, 200)),
    }
}

impl DiffOperation {
    /// Added tasks stay `ListItemAdded` — the status char rides the
    /// detail; this hook exists so the add path reads uniformly.
    fn with_task_add_kind(self, _status: char) -> DiffOperation {
        self
    }
}

fn removed_op(from_source: &str, block: &ReadingBlock) -> DiffOperation {
    let (line, line_end) = block_lines(from_source, block);
    let (kind, noun) = remove_class(&block.kind);
    let semantic_description = match &block.kind {
        ReadingBlockKind::Heading { .. } => {
            format!(
                "Removed heading '{}' at line {line}",
                heading_text(&block.source)
            )
        }
        _ => format!("Removed {noun} at line {line}"),
    };
    DiffOperation {
        kind,
        line,
        line_end,
        semantic_description,
        detail: None,
    }
}

fn edited_op(to_source: &str, from_block: &ReadingBlock, to_block: &ReadingBlock) -> DiffOperation {
    let (line, line_end) = block_lines(to_source, to_block);
    // Task-status special case: same text, different status char.
    if let (Some(_), Some(to_status)) = (task_status(&from_block.kind), task_status(&to_block.kind))
        && only_status_differs(&from_block.source, &to_block.source)
    {
        let text = task_text(&to_block.source);
        let semantic_description = match to_status {
            'x' | 'X' => format!("Completed task '{text}'"),
            ' ' => format!("Reopened task '{text}'"),
            other => format!("Changed task '{text}' status to '{other}'"),
        };
        return DiffOperation {
            kind: DiffOpClass::TaskStatusChanged,
            line,
            line_end,
            semantic_description,
            detail: None,
        };
    }
    let (kind, noun) = edit_class(&to_block.kind);
    let semantic_description = match &to_block.kind {
        ReadingBlockKind::Heading { .. } => {
            format!(
                "Edited heading '{}' at line {line}",
                heading_text(&to_block.source)
            )
        }
        _ => format!("Edited {noun} at line {line}"),
    };
    DiffOperation {
        kind,
        line,
        line_end,
        semantic_description,
        detail: Some(truncate_chars(&to_block.source, 200)),
    }
}

fn add_class(kind: &ReadingBlockKind) -> (DiffOpClass, &'static str) {
    match coarse(kind) {
        Coarse::Heading => (DiffOpClass::HeadingAdded, "heading"),
        Coarse::Paragraph => (DiffOpClass::ParagraphAdded, "paragraph"),
        Coarse::ListItem => (DiffOpClass::ListItemAdded, "list item"),
        Coarse::Code => (DiffOpClass::CodeBlockEdited, "code block"),
        Coarse::Math => (DiffOpClass::MathBlockEdited, "math block"),
        Coarse::Diagram => (DiffOpClass::DiagramEdited, "diagram"),
        Coarse::Table => (DiffOpClass::TableEdited, "table"),
        Coarse::Break => (DiffOpClass::Other, "thematic break"),
    }
}

fn remove_class(kind: &ReadingBlockKind) -> (DiffOpClass, &'static str) {
    match coarse(kind) {
        Coarse::Heading => (DiffOpClass::HeadingRemoved, "heading"),
        Coarse::Paragraph => (DiffOpClass::ParagraphRemoved, "paragraph"),
        Coarse::ListItem => (DiffOpClass::ListItemRemoved, "list item"),
        Coarse::Code => (DiffOpClass::CodeBlockEdited, "code block"),
        Coarse::Math => (DiffOpClass::MathBlockEdited, "math block"),
        Coarse::Diagram => (DiffOpClass::DiagramEdited, "diagram"),
        Coarse::Table => (DiffOpClass::TableEdited, "table"),
        Coarse::Break => (DiffOpClass::Other, "thematic break"),
    }
}

fn edit_class(kind: &ReadingBlockKind) -> (DiffOpClass, &'static str) {
    match coarse(kind) {
        Coarse::Heading => (DiffOpClass::HeadingEdited, "heading"),
        Coarse::Paragraph => (DiffOpClass::ParagraphEdited, "paragraph"),
        Coarse::ListItem => (DiffOpClass::ListItemEdited, "list item"),
        Coarse::Code => (DiffOpClass::CodeBlockEdited, "code block"),
        Coarse::Math => (DiffOpClass::MathBlockEdited, "math block"),
        Coarse::Diagram => (DiffOpClass::DiagramEdited, "diagram"),
        Coarse::Table => (DiffOpClass::TableEdited, "table"),
        Coarse::Break => (DiffOpClass::Other, "thematic break"),
    }
}

/// Group label for the audio summary.
fn summary_label(kind: DiffOpClass) -> &'static str {
    match kind {
        DiffOpClass::PropertySet | DiffOpClass::PropertyRemoved => "property change",
        DiffOpClass::HeadingAdded => "added heading",
        DiffOpClass::HeadingRemoved => "removed heading",
        DiffOpClass::HeadingEdited => "heading edit",
        DiffOpClass::ParagraphAdded => "added paragraph",
        DiffOpClass::ParagraphRemoved => "removed paragraph",
        DiffOpClass::ParagraphEdited => "paragraph edit",
        DiffOpClass::ListItemAdded => "added list item",
        DiffOpClass::ListItemRemoved => "removed list item",
        DiffOpClass::ListItemEdited => "list item edit",
        DiffOpClass::TaskStatusChanged => "task status change",
        DiffOpClass::CodeBlockEdited => "code block change",
        DiffOpClass::MathBlockEdited => "math block change",
        DiffOpClass::DiagramEdited => "diagram change",
        DiffOpClass::TableEdited => "table change",
        DiffOpClass::Other => "other change",
    }
}

fn plural(label: &str, count: usize) -> String {
    if count == 1 {
        return format!("{count} {label}");
    }
    // "added paragraph" → "added paragraphs"; "heading edit" →
    // "heading edits"; "property change" → "property changes".
    format!("{count} {label}s")
}

/// §7.3 pattern: count first, then by-class breakdown, largest class
/// first (ties alphabetical for determinism).
fn audio_summary(operations: &[DiffOperation]) -> String {
    if operations.is_empty() {
        return "No changes.".to_string();
    }
    let mut counts: std::collections::BTreeMap<&'static str, usize> =
        std::collections::BTreeMap::new();
    for op in operations {
        *counts.entry(summary_label(op.kind)).or_insert(0) += 1;
    }
    let mut groups: Vec<(&'static str, usize)> = counts.into_iter().collect();
    groups.sort_by(|a, b| b.1.cmp(&a.1).then_with(|| a.0.cmp(b.0)));
    let breakdown: Vec<String> = groups
        .into_iter()
        .map(|(label, count)| plural(label, count))
        .collect();
    format!(
        "{} change{}: {}.",
        operations.len(),
        if operations.len() == 1 { "" } else { "s" },
        breakdown.join(", ")
    )
}
