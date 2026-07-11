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
        ReadingBlockKind::Paragraph
        | ReadingBlockKind::BlockQuote { .. }
        | ReadingBlockKind::Html => Coarse::Paragraph,
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
    // Single pass (Codoki, PR #792): find the byte cut for `max` chars
    // and only then decide whether the ellipsis is needed.
    match text.char_indices().nth(max) {
        None => text.to_string(),
        Some((cut, _)) => format!("{}…", &text[..cut]),
    }
}

/// A one-line, whitespace-normalized excerpt of a block for
/// descriptions (streaming — no intermediate Vec; Codoki, PR #792).
fn excerpt(source: &str, max: usize) -> String {
    let mut normalized = String::with_capacity(source.len().min(4 * max));
    for word in source.split_whitespace() {
        if !normalized.is_empty() {
            normalized.push(' ');
        }
        normalized.push_str(word);
        if normalized.chars().count() > max {
            break; // enough for the truncation below
        }
    }
    truncate_chars(&normalized, max)
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

/// Newline positions of a source, built once — per-block line lookups
/// are then O(log n) instead of rescanning the prefix (which made
/// large diffs quadratic in total size; adversarial-review stress
/// test).
struct LineIndex(Vec<usize>);

impl LineIndex {
    fn new(source: &str) -> Self {
        Self(
            source
                .bytes()
                .enumerate()
                .filter_map(|(i, b)| (b == b'\n').then_some(i))
                .collect(),
        )
    }
    /// 1-based line containing `byte`.
    fn line_of(&self, byte: usize) -> u32 {
        self.0.partition_point(|&newline| newline < byte) as u32 + 1
    }
}

/// `(line, line_end)` of a block within its source.
fn block_lines(index: &LineIndex, block: &ReadingBlock) -> (u32, u32) {
    let line = index.line_of(block.byte_start as usize);
    let inner_newlines = block
        .source
        .trim_end_matches('\n')
        .bytes()
        .filter(|b| *b == b'\n')
        .count() as u32;
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
///
/// The status bracket is parsed from the FIRST line only — the task
/// marker lives there, and a `[` on a continuation line must never be
/// mistaken for it (Codoki, PR #792). Continuation lines ride along
/// unchanged in the normalized comparison.
fn only_status_differs(from: &str, to: &str) -> bool {
    let strip = |s: &str| -> Option<(String, char)> {
        let (first, rest) = match s.split_once('\n') {
            Some((first, rest)) => (first, Some(rest)),
            None => (s, None),
        };
        let open = first.find('[')?;
        let mut chars = first[open + 1..].chars();
        let status = chars.next()?;
        let after = chars.as_str().strip_prefix(']')?;
        let mut normalized = format!("{}{}", &first[..open], after);
        if let Some(rest) = rest {
            normalized.push('\n');
            normalized.push_str(rest);
        }
        Some((normalized, status))
    };
    match (strip(from), strip(to)) {
        (Some((f_rest, f_status)), Some((t_rest, t_status))) => {
            f_rest == t_rest && f_status != t_status
        }
        _ => false,
    }
}

/// Indentation-aware line lookup for a flattened dotted property path
/// within a source's frontmatter. Matches each path component at
/// strictly increasing indentation under its parent; returns the LEAF
/// component's own 1-based line. Duplicate leaf names under different
/// parents resolve to the right branch. `None` when the path can't be
/// walked (fall back to line 1 — display-only data).
fn key_path_line(source: &str, dotted: &str) -> Option<u32> {
    let components: Vec<&str> = dotted.split('.').collect();
    // Stack of matched-ancestor indents; stack.len() == matched depth.
    let mut indents: Vec<usize> = Vec::new();
    let mut in_frontmatter = false;
    for (i, raw_line) in source.lines().enumerate() {
        let line = raw_line.trim_end_matches('\r');
        if i == 0 {
            if line.trim() != "---" {
                return None; // no frontmatter block
            }
            in_frontmatter = true;
            continue;
        }
        if !in_frontmatter {
            break;
        }
        if line.trim() == "---" {
            break; // closing delimiter — path not found
        }
        let indent = line.len() - line.trim_start().len();
        let trimmed = line.trim_start();
        // Leaving matched scopes: pop ancestors at or beyond this
        // indent (a sibling or shallower key ends their branches).
        while indents.last().is_some_and(|&parent| indent <= parent) {
            indents.pop();
        }
        let expected = components.get(indents.len());
        if let Some(component) = expected
            && trimmed
                .strip_prefix(component)
                .is_some_and(|rest| rest.trim_start().starts_with(':'))
        {
            indents.push(indent);
            if indents.len() == components.len() {
                return Some(i as u32 + 1);
            }
        }
    }
    // Bare fallback: first occurrence of the leaf anywhere in the
    // frontmatter (kept for tag-list shapes the walker can't see).
    let leaf = dotted.rsplit('.').next()?;
    for (i, raw_line) in source.lines().enumerate() {
        let line = raw_line.trim_end_matches('\r');
        if line.trim() == "---" && i > 0 {
            break;
        }
        if line
            .trim_start()
            .strip_prefix(leaf)
            .is_some_and(|rest| rest.trim_start().starts_with(':'))
        {
            return Some(i as u32 + 1);
        }
    }
    None
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
    let from_map: std::collections::BTreeMap<&str, &PropertyValue> = from_props
        .iter()
        .map(|p| (p.key.as_str(), &p.value))
        .collect();
    let to_map: std::collections::BTreeMap<&str, &PropertyValue> = to_props
        .iter()
        .map(|p| (p.key.as_str(), &p.value))
        .collect();

    // Best-effort 1-based line of a property within a source's
    // frontmatter: a full dotted-path walk with an indentation stack,
    // so `second.status` anchors at the `status:` under `second:` even
    // when another branch has its own `status:` (adversarial review —
    // duplicate leaf names). Falls back to a bare leaf scan, then 1.
    let key_line = |source: &str, key: &str| -> u32 { key_path_line(source, key).unwrap_or(1) };

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
    let from_index = LineIndex::new(from);
    let to_index = LineIndex::new(to);

    // Trim the common prefix/suffix first (typical edits touch a small
    // region; this keeps the LCS table tiny).
    let same = |a: &ReadingBlock, b: &ReadingBlock| {
        coarse(&a.kind) == coarse(&b.kind) && a.source == b.source
    };
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
    // Global similarity work budget (adversarial review): pairing cost
    // is quadratic per pair (capped Levenshtein), so a pathological
    // diff with thousands of long unmatched blocks — especially on the
    // LCS-bailout path — must not go CPU-pathological. Once the budget
    // is spent, remaining pairs read as remove + add: coarser, still
    // total and deterministic. ~100M char-cells ≈ tens of ms.
    let mut similarity_budget: u64 = 100_000_000;
    let mut emit_run =
        |operations: &mut Vec<DiffOperation>, removed: &[ReadingBlock], added: &[ReadingBlock]| {
            let pairs = removed.len().min(added.len());
            for i in 0..pairs {
                let from_block = &removed[i];
                let to_block = &added[i];
                let cost = (from_block.source.len().min(2048) as u64)
                    * (to_block.source.len().min(2048) as u64);
                let affordable = cost <= similarity_budget;
                if affordable {
                    similarity_budget -= cost;
                }
                let pairable = affordable
                    && coarse(&from_block.kind) == coarse(&to_block.kind)
                    && similarity(&from_block.source, &to_block.source) > 0.6;
                if pairable {
                    operations.push(edited_op(&to_index, from_block, to_block));
                } else {
                    operations.push(removed_op(&from_index, from_block));
                    operations.push(added_op(&to_index, to_block));
                }
            }
            for from_block in &removed[pairs..] {
                operations.push(removed_op(&from_index, from_block));
            }
            for to_block in &added[pairs..] {
                operations.push(added_op(&to_index, to_block));
            }
        };
    for (anchor_from, anchor_to) in anchors
        .iter()
        .copied()
        .chain(std::iter::once((from_mid.len(), to_mid.len())))
    {
        emit_run(
            &mut operations,
            &from_mid[fi..anchor_from],
            &to_mid[ti..anchor_to],
        );
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

fn added_op(to_index: &LineIndex, block: &ReadingBlock) -> DiffOperation {
    let (line, line_end) = block_lines(to_index, block);
    let (kind, noun) = add_class(&block.kind);
    let semantic_description = match &block.kind {
        ReadingBlockKind::Heading { .. } => {
            format!(
                "Added heading '{}' at line {line}",
                heading_text(&block.source)
            )
        }
        ReadingBlockKind::ListItem {
            task: Some(status), ..
        } => {
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

fn removed_op(from_index: &LineIndex, block: &ReadingBlock) -> DiffOperation {
    let (line, line_end) = block_lines(from_index, block);
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

fn edited_op(
    to_index: &LineIndex,
    from_block: &ReadingBlock,
    to_block: &ReadingBlock,
) -> DiffOperation {
    let (line, line_end) = block_lines(to_index, to_block);
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

#[cfg(test)]
mod tests {
    use super::*;

    fn diff(from: &str, to: &str) -> StructuredDiff {
        structured_diff("note.md", "from-hash", "to-hash", from, to)
    }

    // --- The §7.3 walkthrough, verbatim ------------------------------

    #[test]
    fn spec_walkthrough_heading_property_paragraph() {
        // A heading added at line 10, a property set, a paragraph
        // added — the 05 §7.3 sequential-walkthrough example.
        let from = "---\nstatus: draft\n---\nintro\n\none\n\ntwo\n";
        let to =
            "---\nstatus: final\n---\nintro\n\none\n\ntwo\n\n# Goals\n\na new closing paragraph\n";
        let d = diff(from, to);
        let descriptions: Vec<&str> = d
            .operations
            .iter()
            .map(|op| op.semantic_description.as_str())
            .collect();
        assert!(
            descriptions.contains(&"Added heading 'Goals' at line 10"),
            "the §7.3 example copy, verbatim; got {descriptions:?}"
        );
        assert!(
            descriptions.contains(&"Set property 'status' to 'final'"),
            "got {descriptions:?}"
        );
        assert!(
            descriptions.contains(&"Added paragraph at line 12"),
            "got {descriptions:?}"
        );
        assert_eq!(
            d.audio_summary,
            "3 changes: 1 added heading, 1 added paragraph, 1 property change.",
        );
    }

    // --- One fixture per class family ---------------------------------

    #[test]
    fn heading_add_remove_edit() {
        let d = diff(
            "# Project Goals\n\nbody\n",
            "# Project Goals 2026\n\nbody\n",
        );
        assert_eq!(d.operations.len(), 1);
        assert_eq!(d.operations[0].kind, DiffOpClass::HeadingEdited);
        assert_eq!(
            d.operations[0].semantic_description,
            "Edited heading 'Project Goals 2026' at line 1"
        );
        // Below the 0.6 similarity gate, a rewrite reads as
        // remove + add (the pinned pairing rule).
        let d = diff("# One\n\nbody\n", "# Two\n\nbody\n");
        let kinds: Vec<DiffOpClass> = d.operations.iter().map(|op| op.kind).collect();
        assert_eq!(
            kinds,
            vec![DiffOpClass::HeadingRemoved, DiffOpClass::HeadingAdded]
        );

        let d = diff("body\n", "# New\n\nbody\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::HeadingAdded);
        let d = diff("# Gone\n\nbody\n", "body\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::HeadingRemoved);
        assert_eq!(
            d.operations[0].semantic_description,
            "Removed heading 'Gone' at line 1"
        );
    }

    #[test]
    fn paragraph_add_remove_edit() {
        let d = diff("alpha\n", "alpha\n\nbeta\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::ParagraphAdded);
        assert_eq!(d.operations[0].detail.as_deref(), Some("beta"));

        let d = diff("alpha\n\nbeta\n", "alpha\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::ParagraphRemoved);
        assert_eq!(
            d.operations[0].semantic_description,
            "Removed paragraph at line 3"
        );

        let d = diff(
            "a shared paragraph about goats\n",
            "a shared paragraph about stoats\n",
        );
        assert_eq!(d.operations[0].kind, DiffOpClass::ParagraphEdited);
    }

    #[test]
    fn list_items_and_all_three_task_status_arms() {
        let d = diff("- one\n", "- one\n- two\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::ListItemAdded);
        let d = diff("- one\n- two\n", "- one\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::ListItemRemoved);
        let d = diff("- long enough item text\n", "- long enough item text!\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::ListItemEdited);
        assert_eq!(
            d.operations[0].semantic_description,
            "Edited list item at line 1"
        );

        // Task copy, all three arms.
        let d = diff("- [ ] ship the milestone\n", "- [x] ship the milestone\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::TaskStatusChanged);
        assert_eq!(
            d.operations[0].semantic_description,
            "Completed task 'ship the milestone'"
        );
        let d = diff("- [x] ship the milestone\n", "- [ ] ship the milestone\n");
        assert_eq!(
            d.operations[0].semantic_description,
            "Reopened task 'ship the milestone'"
        );
        let d = diff("- [ ] ship the milestone\n", "- [/] ship the milestone\n");
        assert_eq!(
            d.operations[0].semantic_description,
            "Changed task 'ship the milestone' status to '/'"
        );
        // Text AND status changed → an edit, not a status change.
        let d = diff(
            "- [ ] alpha beta gamma delta\n",
            "- [x] alpha beta gamma DELTA\n",
        );
        assert_eq!(d.operations[0].kind, DiffOpClass::ListItemEdited);
    }

    #[test]
    fn multiline_task_with_continuation_bracket_classifies_correctly() {
        // Codoki (PR #792): a '[' on a continuation line must not be
        // parsed as the status marker. Status-only flip on a
        // multi-line item → TaskStatusChanged…
        let from = "- [ ] review the notes\n  see [reference] for context\n";
        let to = "- [x] review the notes\n  see [reference] for context\n";
        let d = diff(from, to);
        assert_eq!(d.operations[0].kind, DiffOpClass::TaskStatusChanged);
        assert_eq!(
            d.operations[0].semantic_description,
            "Completed task 'review the notes'"
        );
        // …while a continuation-line edit is an EDIT even when the
        // status also flipped (never misread as status-only).
        let to = "- [x] review the notes\n  see [other source] for context\n";
        let d = diff(from, to);
        assert_eq!(d.operations[0].kind, DiffOpClass::ListItemEdited);
    }

    #[test]
    fn specialized_blocks_map_to_their_edit_classes() {
        let code_from = "```rust\nfn a() {}\nfn shared() {}\n```\n";
        let code_to = "```rust\nfn b() {}\nfn shared() {}\n```\n";
        let d = diff(code_from, code_to);
        assert_eq!(d.operations[0].kind, DiffOpClass::CodeBlockEdited);

        let d = diff("$$\na + b\n$$\n", "$$\na + b + c\n$$\n");
        assert_eq!(d.operations[0].kind, DiffOpClass::MathBlockEdited);

        let d = diff(
            "```mermaid\ngraph TD; A-->B;\n```\n",
            "```mermaid\ngraph TD; A-->C;\n```\n",
        );
        assert_eq!(d.operations[0].kind, DiffOpClass::DiagramEdited);

        let d = diff(
            "| a | b |\n|---|---|\n| 1 | 2 |\n",
            "| a | b |\n|---|---|\n| 1 | 3 |\n",
        );
        assert_eq!(d.operations[0].kind, DiffOpClass::TableEdited);
    }

    #[test]
    fn property_set_and_removed() {
        let d = diff(
            "---\ndraft: true\nkeep: 1\n---\nbody\n",
            "---\nkeep: 1\nstatus: final\n---\nbody\n",
        );
        let kinds: Vec<DiffOpClass> = d.operations.iter().map(|op| op.kind).collect();
        assert!(kinds.contains(&DiffOpClass::PropertySet));
        assert!(kinds.contains(&DiffOpClass::PropertyRemoved));
        let descriptions: Vec<&str> = d
            .operations
            .iter()
            .map(|op| op.semantic_description.as_str())
            .collect();
        assert!(descriptions.contains(&"Set property 'status' to 'final'"));
        assert!(descriptions.contains(&"Removed property 'draft'"));
    }

    // --- Pairing rules --------------------------------------------------

    #[test]
    fn moved_block_reads_as_remove_plus_add() {
        // No reordering detection (documented): a moved paragraph is a
        // removal at its old position and an addition at its new one.
        let from = "# H\n\nmover paragraph\n\nanchor one\n\nanchor two\n";
        let to = "# H\n\nanchor one\n\nanchor two\n\nmover paragraph\n";
        let d = diff(from, to);
        let kinds: Vec<DiffOpClass> = d.operations.iter().map(|op| op.kind).collect();
        assert!(kinds.contains(&DiffOpClass::ParagraphRemoved), "{kinds:?}");
        assert!(kinds.contains(&DiffOpClass::ParagraphAdded), "{kinds:?}");
        assert!(
            !kinds.contains(&DiffOpClass::ParagraphEdited),
            "a move must not read as an edit: {kinds:?}"
        );
    }

    #[test]
    fn pairing_is_in_order_same_kind_and_similarity_gated() {
        // Two removed + two added in one run, mixed kinds: the i-th
        // removed pairs with the i-th added only when the kind matches
        // and the text is similar.
        let from = "anchor\n\nfirst paragraph body text here\n\n- a list item\n\ntail\n";
        let to = "anchor\n\nfirst paragraph body text HERE\n\n- a really different item entirely\n\ntail\n";
        let d = diff(from, to);
        let kinds: Vec<DiffOpClass> = d.operations.iter().map(|op| op.kind).collect();
        // Paragraph pairs (same kind, similar) → edit; list items are
        // dissimilar → remove + add.
        assert!(kinds.contains(&DiffOpClass::ParagraphEdited), "{kinds:?}");
        assert!(kinds.contains(&DiffOpClass::ListItemRemoved), "{kinds:?}");
        assert!(kinds.contains(&DiffOpClass::ListItemAdded), "{kinds:?}");

        // Dissimilar same-kind blocks stay remove + add.
        let d = diff(
            "anchor\n\ncompletely original words\n\ntail\n",
            "anchor\n\nnothing shared whatsoever!!\n\ntail\n",
        );
        let kinds: Vec<DiffOpClass> = d.operations.iter().map(|op| op.kind).collect();
        assert!(kinds.contains(&DiffOpClass::ParagraphRemoved), "{kinds:?}");
        assert!(kinds.contains(&DiffOpClass::ParagraphAdded), "{kinds:?}");
    }

    #[test]
    fn empty_diff_and_line_ranges() {
        let d = diff("same\n", "same\n");
        assert!(d.operations.is_empty());
        assert_eq!(d.audio_summary, "No changes.");

        // Multi-line block: line..line_end covers it.
        let d = diff("one\n", "one\n\n```rust\nfn x() {}\nfn y() {}\n```\n");
        let op = &d.operations[0];
        assert_eq!((op.line, op.line_end), (3, 6));
    }

    // --- Totality census -----------------------------------------------

    struct SplitMix64(u64);
    impl SplitMix64 {
        fn next(&mut self) -> u64 {
            self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
            let mut z = self.0;
            z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
            z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
            z ^ (z >> 31)
        }
        fn below(&mut self, n: usize) -> usize {
            (self.next() % n as u64) as usize
        }
    }

    fn census_scale() -> u64 {
        if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
            2_000
        } else {
            150
        }
    }

    fn random_doc(rng: &mut SplitMix64) -> String {
        const PIECES: &[&str] = &[
            "# Heading\n\n",
            "plain paragraph text\n\n",
            "- [ ] a task\n",
            "- bullet\n",
            "```rust\ncode();\n```\n\n",
            "$$\nx^2\n$$\n\n",
            "| a |\n|---|\n| 1 |\n\n",
            "> quoted\n\n",
            "---\n\n",
            "中文段落😀\r\n\r\n",
            "",
        ];
        let mut out = String::new();
        if rng.below(3) == 0 {
            out.push_str("---\nstatus: draft\ncount: 3\n---\n");
        }
        for _ in 0..rng.below(30) {
            out.push_str(PIECES[rng.below(PIECES.len())]);
        }
        out
    }

    /// Random document pairs (unicode, CRLF, frontmatter mix,
    /// pathological shapes): never panics; every changed line (from a
    /// plain line-diff reference) falls within some operation's
    /// [line, line_end] range; descriptions non-empty; deterministic.
    #[test]
    fn census_structured_diff_total() {
        for seed in 0..census_scale() {
            let mut rng = SplitMix64(seed.wrapping_mul(0xABCD_EF01).wrapping_add(3));
            let from = random_doc(&mut rng);
            let to = if rng.below(5) == 0 {
                from.clone() // identical pair
            } else {
                random_doc(&mut rng)
            };

            let d1 = diff(&from, &to);
            let d2 = diff(&from, &to);
            assert_eq!(d1, d2, "seed {seed}: nondeterministic output");
            for op in &d1.operations {
                assert!(
                    !op.semantic_description.is_empty(),
                    "seed {seed}: empty description"
                );
                assert!(op.line_end >= op.line, "seed {seed}: inverted range");
            }
            assert!(!d1.audio_summary.is_empty());

            // Coverage: changed TO-side lines (excluding the
            // frontmatter region, whose ops anchor at the key line)
            // must fall inside some op range.
            let from_lines: std::collections::HashSet<&str> = from.lines().collect();
            let body_start_line = {
                let parts = crate::split_note(&to);
                let offset = to.len() - parts.body.len();
                to[..offset].bytes().filter(|b| *b == b'\n').count() as u32
            };
            for (i, line) in to.lines().enumerate() {
                let line_number = i as u32 + 1;
                if line_number <= body_start_line {
                    continue; // frontmatter — property ops cover it
                }
                if line.trim().is_empty() || from_lines.contains(line) {
                    continue;
                }
                let covered = d1
                    .operations
                    .iter()
                    .any(|op| op.line <= line_number && line_number <= op.line_end);
                assert!(
                    covered,
                    "seed {seed}: changed line {line_number} ({line:?}) not covered by any \
                     operation; ops: {:?}",
                    d1.operations
                        .iter()
                        .map(|op| (op.line, op.line_end, op.kind))
                        .collect::<Vec<_>>()
                );
            }
        }
    }

    #[test]
    fn nested_property_anchors_at_the_leaf_key_line() {
        // Adversarial review: `parent.child` must anchor at `child:`,
        // not collapse onto `parent:`.
        let from = "---\nparent:\n  child: old\n---\nbody\n";
        let to = "---\nparent:\n  child: new\n---\nbody\n";
        let d = diff(from, to);
        let op = d
            .operations
            .iter()
            .find(|op| op.kind == DiffOpClass::PropertySet)
            .expect("a property change");
        assert_eq!(op.line, 3, "the leaf key's own line, not the parent's");
    }

    #[test]
    fn duplicate_leaf_keys_anchor_to_the_right_branch() {
        // Adversarial review round 2: two branches with the same leaf
        // name — set + removed operations must anchor to the correct
        // branch's own line, in both LF and CRLF sources.
        let from =
            "---\nfirst:\n  status: keep\nsecond:\n  status: old\nthird:\n  gone: yes\n---\nbody\n";
        let to = "---\nfirst:\n  status: keep\nsecond:\n  status: new\nthird: {}\n---\nbody\n";
        let d = diff(from, to);
        let set = d
            .operations
            .iter()
            .find(|op| {
                op.kind == DiffOpClass::PropertySet
                    && op.semantic_description.contains("second.status")
            })
            .expect("second.status set");
        assert_eq!(set.line, 5, "anchors at second's status:, not first's");
        let removed = d
            .operations
            .iter()
            .find(|op| op.kind == DiffOpClass::PropertyRemoved)
            .expect("third.gone removed");
        assert_eq!(removed.line, 7, "removal anchors in the FROM source");

        // CRLF variant.
        let from_crlf = from.replace('\n', "\r\n");
        let to_crlf = to.replace('\n', "\r\n");
        let d = diff(&from_crlf, &to_crlf);
        let set = d
            .operations
            .iter()
            .find(|op| {
                op.kind == DiffOpClass::PropertySet
                    && op.semantic_description.contains("second.status")
            })
            .expect("second.status set (CRLF)");
        assert_eq!(set.line, 5);
    }

    #[test]
    fn crlf_sources_report_correct_lines() {
        let from = "line one\r\n\r\nline two\r\n";
        let to = "line one\r\n\r\nline two\r\n\r\nline three\r\n";
        let d = diff(from, to);
        assert_eq!(d.operations.len(), 1);
        assert_eq!(d.operations[0].kind, DiffOpClass::ParagraphAdded);
        assert_eq!(d.operations[0].line, 5);
    }

    #[test]
    fn lcs_bailout_with_long_unmatched_blocks_stays_bounded() {
        // Adversarial review: >4M-cell middles skip LCS anchoring, and
        // the similarity work budget keeps per-pair Levenshtein from
        // going CPU-pathological (billions of cells). 2,100 unmatched
        // ~2KB paragraphs per side would cost ~8.8B cells unbudgeted.
        let from: String = (0..2_100)
            .map(|i| format!("left {i} {}\n\n", "a".repeat(2000)))
            .collect();
        let to: String = (0..2_100)
            .map(|i| format!("right {i} {}\n\n", "b".repeat(2000)))
            .collect();
        let started = std::time::Instant::now();
        let d = diff(&from, &to);
        assert!(
            started.elapsed() < std::time::Duration::from_secs(20),
            "bailout diff must stay work-bounded; took {:?}",
            started.elapsed()
        );
        assert!(!d.operations.is_empty());
        // Deterministic across runs even with the budget in play.
        assert_eq!(d, diff(&from, &to));
    }

    #[test]
    fn pathological_sizes_stay_bounded() {
        // 2k blocks + a single 1 MB paragraph: total, no panic.
        let big_para = "x".repeat(1024 * 1024);
        let many: String = (0..2000).map(|i| format!("para {i}\n\n")).collect();
        let from = format!("{many}{big_para}\n");
        let to = format!("{many}{big_para}y\n");
        let d = diff(&from, &to);
        assert!(!d.operations.is_empty());
    }
}
