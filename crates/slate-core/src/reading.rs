// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Ordered whole-document block segmentation for the reading view
//! (U3-1, #465 · gap_analysis G6).
//!
//! The editor renders inline highlight spans ([`crate::editor_spans`]);
//! the specialized pipelines ([`crate::math`] / [`crate::code`] /
//! [`crate::diagram`]) each extract *their own* kind of block. Neither
//! gives the reading view what it needs: **one ordered pass over the
//! whole body** that names every top-level block — paragraphs, headings,
//! lists, quotes, tables, thematic breaks, HTML — interleaved with the
//! specialized kinds, so a SwiftUI `VStack` can render each block and
//! VoiceOver can enumerate them top-to-bottom.
//!
//! ## Body only, whole-source offsets
//!
//! Frontmatter never renders in the reading view — the properties widget
//! owns it (U3-3). So the walk operates on [`crate::frontmatter::body_after_frontmatter`],
//! but every returned [`ReadingBlock::byte_start`]/[`ReadingBlock::byte_end`]
//! is rebased onto the **whole source** (frontmatter offset added back)
//! so an editor can map a block back to a caret position. `source` on
//! each block is the exact `full_source[byte_start..byte_end]` slice —
//! the census pins that equality.
//!
//! ## Flattening (the linear-reading rationale)
//!
//! List items and blockquote children are **flattened into document
//! order**, each carrying a `depth`. VoiceOver reads linearly; nesting
//! is conveyed by the AX value ("list item, level 2"), not by view
//! nesting. So `- a\n  - b` yields two `ListItem` blocks (depth 0, then
//! depth 1), not one nested tree. A list item that is *also* inside a
//! quote is a `ListItem` (innermost container names the leaf) whose
//! `depth` counts list nesting; the quote nesting it sits in is not lost
//! to the reader because the quote's own leaf paragraphs still emit
//! `BlockQuote` blocks around it in document order.
//!
//! ## No second classifier
//!
//! The specialized-kind rules are **reused, never re-derived**:
//! - A fenced block whose language tag is `mermaid` (case-insensitive,
//!   trimmed) is a [`ReadingBlockKind::Diagram`] — the exact rule
//!   [`crate::diagram::extract_diagram_blocks`] uses. Any other fenced or
//!   indented block is a [`ReadingBlockKind::CodeFence`] carrying the
//!   trimmed language (matching [`crate::code`]).
//! - A top-level paragraph that is exactly one display-math (`$$…$$`)
//!   block becomes a [`ReadingBlockKind::MathBlock`]. "Is this display
//!   math" is answered by [`crate::math::extract_math_blocks`] — the same
//!   delimiter scanner the math pipeline uses — so the reading view and
//!   the math pipeline can never disagree about what counts as a block.
//! - A list item's task status char comes from
//!   [`crate::tasks::task_status_char`] (the Tasks-panel grammar), NOT
//!   from pulldown-cmark's `TaskListMarker` (which only knows `[ ]` /
//!   `[x]` / `[X]` and would drop every project-specific status char).

use pulldown_cmark::{CodeBlockKind, Event, HeadingLevel, Options, Parser, Tag, TagEnd};

/// The single pulldown-cmark option set both the block walk and the
/// table-cell segmentation ([`reading_table_cells`]) parse with. Factored
/// to a const so the two entry points can never diverge — a table the walk
/// classifies as [`ReadingBlockKind::Table`] parses identically when its
/// source is fed back for cell extraction.
const READING_PARSE_OPTIONS: Options = Options::ENABLE_TABLES
    .union(Options::ENABLE_STRIKETHROUGH)
    .union(Options::ENABLE_TASKLISTS);

/// The kind of one reading block, in document order.
///
/// Payload variants carry exactly what the SwiftUI renderer needs to
/// dispatch + label: heading level, list-item depth / ordered-ness /
/// task status char, quote depth, code language, diagram dialect. Raw
/// blocks (`Table`, `Html`) carry no payload — the block's `source`
/// slice carries the bytes and the renderer treats them as opaque
/// (monospace source / styled grid), never re-interpreting them.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ReadingBlockKind {
    /// ATX (`#`..`######`) or setext heading. `level` is 1..=6.
    Heading { level: u8 },
    /// A top-level paragraph (inline content rendered by the Swift
    /// inline pipeline: wikilinks / embeds / tags / citations).
    Paragraph,
    /// One flattened list item. `depth` is 0 at the top level and
    /// increments per nesting level. `ordered` reflects the enclosing
    /// list's kind (`1.` / `1)` → ordered). `task` is the single
    /// status char (`' '`, `'x'`, `'/'`, …) when the item is a task
    /// line, else `None` — derived from [`crate::tasks::task_status_char`].
    ListItem {
        depth: u8,
        ordered: bool,
        task: Option<char>,
    },
    /// One blockquote leaf block. `depth` is 1 for a top-level quote
    /// and increments per nesting level (`>` → 1, `> >` → 2).
    BlockQuote { depth: u8 },
    /// A fenced or indented code block. `language` is the trimmed fence
    /// tag, or `""` for an untagged fence / indented block. `mermaid`
    /// fences are [`ReadingBlockKind::Diagram`] instead, never here.
    CodeFence { language: String },
    /// A display-math (`$$…$$`) block occupying a whole top-level
    /// paragraph.
    MathBlock,
    /// A diagram fence. `dialect` is the fence tag lowercased
    /// (`"mermaid"` today).
    Diagram { dialect: String },
    /// A GFM table (raw block — the `source` slice carries the pipes).
    Table,
    /// A thematic break (`---` / `***` / `___` rule).
    ThematicBreak,
    /// An HTML block (raw — rendered as monospace source, never
    /// interpreted).
    Html,
}

/// One top-level block of a note body, in document order.
///
/// `byte_start`/`byte_end` are UTF-8 byte offsets into the **whole
/// source** (frontmatter offset included), half-open (`byte_end`
/// exclusive). `source` equals `full_source[byte_start..byte_end]`
/// exactly — the census guarantees it, so a consumer that already has
/// the block never has to re-slice the file.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReadingBlock {
    pub kind: ReadingBlockKind,
    pub byte_start: u64,
    pub byte_end: u64,
    pub source: String,
}

/// Segment `source` into ordered top-level reading blocks.
///
/// Pure — no IO. Frontmatter is skipped ([`crate::frontmatter::body_after_frontmatter`])
/// but offsets are rebased onto the whole source. One pulldown-cmark walk
/// over the body drives the segmentation; list items and quote children
/// are flattened in document order carrying `depth`. See the module docs
/// for the (reused, never re-derived) specialized-kind rules.
///
/// This is the U3-2 live-buffer entry point: reading mode renders the
/// editor's in-memory body directly, with no disk round-trip.
pub fn reading_blocks_source(source: &str) -> Vec<ReadingBlock> {
    let body = crate::frontmatter::body_after_frontmatter(source);
    // Frontmatter offset: the body slice is a suffix of `source`, so the
    // number of bytes ahead of it is exactly the rebase amount. Every
    // pulldown offset (into `body`) gets this added back so the returned
    // ranges index the whole source.
    let fm_offset = source.len() - body.len();

    // Display-math block starts (byte offsets into `body`). Reusing
    // math.rs's scanner is the single source of truth for "is this a
    // display-math block"; a top-level paragraph whose span contains one
    // of these AND whose trimmed text opens with `$$` is a MathBlock.
    let math_block_starts: Vec<usize> = crate::math::extract_math_blocks(body)
        .into_iter()
        .filter(|m| m.display_style == crate::math::MathDisplayStyle::Block)
        .map(|m| m.byte_offset as usize)
        .collect();

    let opts = READING_PARSE_OPTIONS;

    // --- Pass 1: record a cut point per emitted block --------------------
    //
    // Each block is `(cut_start, kind)`; the block's END is filled in
    // during pass 2 as the NEXT block's cut (so blocks tile the body edge
    // to edge, then get trailing blank space trimmed). The cut model is
    // what makes the census's "non-overlapping, covers every non-blank
    // byte" hold *by construction* for nested lists/quotes: a list item's
    // cut is its marker column, and the next (possibly nested-child) cut
    // trims the parent so children don't double-cover.
    //
    // `pending_container_start` absorbs container "chrome" — the `>` of a
    // quote, the `-`/`1.` marker + indentation of a list — into the FIRST
    // leaf emitted after entering that container run, so those non-blank
    // marker bytes are covered without a separate block. It's set on the
    // outermost freshly-entered List/BlockQuote and consumed by the next
    // block.
    let mut cuts: Vec<Cut> = Vec::new();
    let mut stack: Vec<Container> = Vec::new();
    let mut pending_container_start: Option<usize> = None;

    for (event, range) in Parser::new_ext(body, opts).into_offset_iter() {
        match event {
            // --- Containers -------------------------------------------
            Event::Start(Tag::List(first_number)) => {
                if pending_container_start.is_none() {
                    pending_container_start = Some(range.start);
                }
                stack.push(Container::List {
                    ordered: first_number.is_some(),
                });
            }
            Event::End(TagEnd::List(_)) => {
                stack.pop();
            }
            Event::Start(Tag::Item) => {
                // One block per list item. `depth` counts enclosing lists
                // (0 at top level); `ordered` from the innermost list; the
                // task status char from the tasks-panel grammar (NOT
                // pulldown's TaskListMarker, which only knows `[ ]`/`[x]`).
                let depth = clamp_depth(list_depth(&stack).saturating_sub(1));
                let ordered = innermost_list_ordered(&stack);
                let task = crate::tasks::task_status_char(first_line(&body[range.clone()]));
                let cut = pending_container_start.take().unwrap_or(range.start);
                cuts.push(Cut {
                    start: cut,
                    kind: ReadingBlockKind::ListItem {
                        depth,
                        ordered,
                        task,
                    },
                });
                stack.push(Container::Item);
            }
            Event::End(TagEnd::Item) => {
                stack.pop();
            }
            Event::Start(Tag::BlockQuote(_)) => {
                if pending_container_start.is_none() {
                    pending_container_start = Some(range.start);
                }
                stack.push(Container::Quote);
            }
            Event::End(TagEnd::BlockQuote(_)) => {
                stack.pop();
            }

            // --- Leaves -----------------------------------------------
            Event::Start(Tag::Heading { level, .. }) => {
                let cut = pending_container_start.take().unwrap_or(range.start);
                cuts.push(Cut {
                    start: cut,
                    kind: ReadingBlockKind::Heading {
                        level: heading_level(level),
                    },
                });
            }
            Event::Start(Tag::Paragraph) => {
                // Inside a list item, the paragraph is the item's own
                // inline content — already covered by the item's block.
                if inside_item(&stack) {
                    continue;
                }
                // A top-level paragraph that is exactly one display-math
                // block → MathBlock (math.rs decides "is display math").
                // Inside a quote, a paragraph is a BlockQuote leaf; else a
                // plain Paragraph. (Display math nested inside a quote
                // stays a BlockQuote leaf — MathBlock is a top-level call.)
                let kind = if quote_depth(&stack).is_none()
                    && paragraph_is_display_math(body, &range, &math_block_starts)
                {
                    ReadingBlockKind::MathBlock
                } else if let Some(depth) = quote_depth(&stack) {
                    ReadingBlockKind::BlockQuote { depth }
                } else {
                    ReadingBlockKind::Paragraph
                };
                let cut = pending_container_start.take().unwrap_or(range.start);
                cuts.push(Cut { start: cut, kind });
            }
            Event::Start(Tag::CodeBlock(kind)) => {
                let language = match &kind {
                    CodeBlockKind::Fenced(tag) => tag.trim().to_string(),
                    CodeBlockKind::Indented => String::new(),
                };
                // Mermaid fence → Diagram, per diagram.rs's classify rule
                // (case-insensitive, trimmed). Everything else → CodeFence.
                let block_kind = if language.eq_ignore_ascii_case("mermaid") {
                    ReadingBlockKind::Diagram {
                        dialect: language.to_ascii_lowercase(),
                    }
                } else {
                    ReadingBlockKind::CodeFence { language }
                };
                let cut = pending_container_start.take().unwrap_or(range.start);
                cuts.push(Cut {
                    start: cut,
                    kind: block_kind,
                });
            }
            Event::Start(Tag::Table(_)) => {
                let cut = pending_container_start.take().unwrap_or(range.start);
                cuts.push(Cut {
                    start: cut,
                    kind: ReadingBlockKind::Table,
                });
            }
            Event::Start(Tag::HtmlBlock) => {
                let cut = pending_container_start.take().unwrap_or(range.start);
                cuts.push(Cut {
                    start: cut,
                    kind: ReadingBlockKind::Html,
                });
            }
            Event::Rule => {
                let cut = pending_container_start.take().unwrap_or(range.start);
                cuts.push(Cut {
                    start: cut,
                    kind: ReadingBlockKind::ThematicBreak,
                });
            }
            _ => {}
        }
    }

    // --- Pass 2: fill each block's end from the next cut, trim trailing
    // blank bytes, and rebase onto the whole source. -----------------------
    //
    // Cuts are already non-decreasing (pulldown is in document order and a
    // pending container start is always >= the previous cut). Defensive
    // dedup: if two cuts share a start (shouldn't happen), keep the first
    // so a zero-width block never ships.
    let mut out: Vec<ReadingBlock> = Vec::with_capacity(cuts.len());
    for (i, cut) in cuts.iter().enumerate() {
        let next = cuts.get(i + 1).map(|c| c.start).unwrap_or(body.len());
        let start = cut.start;
        if next <= start {
            // Degenerate/duplicate cut — skip rather than emit an empty or
            // reversed range.
            continue;
        }
        // Trim trailing ASCII whitespace so inter-block blank lines become
        // gaps (the census allows blank gaps); the block's `source` stays
        // exactly `full_source[byte_start..byte_end]`.
        let mut end = next;
        while end > start && body.as_bytes()[end - 1].is_ascii_whitespace() {
            end -= 1;
        }
        if end <= start {
            continue;
        }
        out.push(ReadingBlock {
            kind: cut.kind.clone(),
            byte_start: (start + fm_offset) as u64,
            byte_end: (end + fm_offset) as u64,
            source: body[start..end].to_string(),
        });
    }

    out
}

/// One recorded block boundary: where the block starts (byte offset into
/// the body) and what kind it is. The block's end is derived in pass 2.
struct Cut {
    start: usize,
    kind: ReadingBlockKind,
}

/// A block container we can be nested inside during the walk.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Container {
    List { ordered: bool },
    Item,
    Quote,
}

/// Number of `List` containers currently on the stack.
fn list_depth(stack: &[Container]) -> usize {
    stack
        .iter()
        .filter(|c| matches!(c, Container::List { .. }))
        .count()
}

/// `ordered` flag of the innermost enclosing list, or `false` if none.
fn innermost_list_ordered(stack: &[Container]) -> bool {
    stack
        .iter()
        .rev()
        .find_map(|c| match c {
            Container::List { ordered } => Some(*ordered),
            _ => None,
        })
        .unwrap_or(false)
}

/// `Some(depth)` (1-based) when inside at least one blockquote, counting
/// quote nesting; `None` at top level.
fn quote_depth(stack: &[Container]) -> Option<u8> {
    let n = stack
        .iter()
        .filter(|c| matches!(c, Container::Quote))
        .count();
    if n == 0 { None } else { Some(clamp_depth(n)) }
}

/// True when the innermost container is a list `Item` (its paragraph
/// child is inline content, already covered by the emitted `ListItem`).
fn inside_item(stack: &[Container]) -> bool {
    matches!(stack.last(), Some(Container::Item))
}

/// Clamp a nesting depth to `u8` — a note nested past 255 levels is
/// already pathological (pulldown itself caps recursion far below this),
/// and saturating keeps the AX "level N" honest rather than wrapping.
fn clamp_depth(depth: usize) -> u8 {
    depth.min(u8::MAX as usize) as u8
}

/// The first line of `slice` (up to the first `\n`, `\r` trimmed), for
/// task-status detection on a list item's source.
fn first_line(slice: &str) -> &str {
    let line = slice.split('\n').next().unwrap_or(slice);
    line.strip_suffix('\r').unwrap_or(line)
}

/// True when the top-level paragraph at `range` is exactly one display-
/// math block: its trimmed text opens with `$$` AND math.rs reported a
/// display-math block starting within the paragraph's span. Reusing the
/// math scanner keeps "is this display math" defined in exactly one place.
fn paragraph_is_display_math(
    body: &str,
    range: &std::ops::Range<usize>,
    math_block_starts: &[usize],
) -> bool {
    if !body[range.clone()].trim_start().starts_with("$$") {
        return false;
    }
    math_block_starts
        .iter()
        .any(|&m| m >= range.start && m < range.end)
}

fn heading_level(level: HeadingLevel) -> u8 {
    match level {
        HeadingLevel::H1 => 1,
        HeadingLevel::H2 => 2,
        HeadingLevel::H3 => 3,
        HeadingLevel::H4 => 4,
        HeadingLevel::H5 => 5,
        HeadingLevel::H6 => 6,
    }
}

// --- Table cell segmentation (#510) -----------------------------------

/// The cells of one GFM table, segmented by pulldown-cmark's table events.
///
/// `header` is the head row's cells left-to-right; `rows` is the body rows,
/// each a `Vec<String>` of the SAME length as `header` — pulldown normalizes
/// ragged body rows against the header per the GFM spec, and
/// [`reading_table_cells`] pads/truncates defensively so the width holds by
/// construction (the Swift grid indexes `rows[r][c]` without a bounds risk).
///
/// Cell text is the flattened inline content (emphasis/code/links reduced to
/// their text), so `**b**` → `"b"`, `` `code` `` → `"code"`, `[t](u)` → `"t"`.
/// The block's raw pipes never reach the consumer — this is the honest,
/// no-second-classifier alternative to rendering the table as monospace source.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReadingTableCells {
    pub header: Vec<String>,
    pub rows: Vec<Vec<String>>,
}

/// Segment a GFM table's `source` slice into header + body cells.
///
/// Input is exactly what [`ReadingBlock::source`] carries for a
/// [`ReadingBlockKind::Table`] block. Parsing reuses [`READING_PARSE_OPTIONS`]
/// (the block walk's option set) so the two entry points can never disagree
/// about what is a table.
///
/// Returns `None` when the first top-level block of the parse is not a table
/// — the API stays total, so a caller handing arbitrary text (or the Swift
/// side falling back) gets `None` rather than a panic. Any blocks trailing the
/// table are ignored (they can't occur from a segmented Table block's slice).
pub fn reading_table_cells(source: &str) -> Option<ReadingTableCells> {
    let mut parser = Parser::new_ext(source, READING_PARSE_OPTIONS);

    // The API is defensive: only a leading Table drives extraction. Scan to
    // the first block-level Start; if it is not a Table, bail.
    let alignments_len = loop {
        match parser.next()? {
            Event::Start(Tag::Table(aligns)) => break aligns.len(),
            // A non-table leading block → not our input; stay total.
            Event::Start(_) => return None,
            _ => {}
        }
    };

    let mut header: Vec<String> = Vec::new();
    let mut rows: Vec<Vec<String>> = Vec::new();
    // Cells accumulate here per row/head; the head fills `header`, each body
    // TableRow flushes to `rows`.
    let mut current: Vec<String> = Vec::new();

    while let Some(event) = parser.next() {
        match event {
            Event::End(TagEnd::TableHead) => {
                header = std::mem::take(&mut current);
            }
            // Only body rows fire TableRow; the head uses TableHead directly,
            // so a TableRow's cells are always a body row.
            Event::Start(Tag::TableRow) => current = Vec::new(),
            Event::End(TagEnd::TableRow) => {
                rows.push(std::mem::take(&mut current));
            }
            Event::Start(Tag::TableCell) => {
                // Drain the cell's inline run up to its matching End and flatten.
                current.push(collect_cell_text(&mut parser));
            }
            Event::End(TagEnd::Table) => break,
            _ => {}
        }
    }
    // A malformed input might not fire TableHead; fall back to the alignment
    // count so the width is still well-defined.
    let width = header.len().max(alignments_len);

    // Normalize every body row to the header width: pad short rows with "",
    // truncate long ones. pulldown already does this per GFM, but pinning it
    // here makes the Swift grid's row[c] indexing safe by construction.
    for row in &mut rows {
        row.resize(width, String::new());
    }

    Some(ReadingTableCells { header, rows })
}

/// Flatten the current `TableCell`'s inline events into plain text, draining
/// `parser` up to (and including) the cell's matching `End(TableCell)`. The
/// caller has already consumed the opening `Start(TableCell)`.
///
/// Mirrors [`crate::links::collect_inline_text`] semantics — Text/Code append,
/// SoftBreak/HardBreak → space, other events ignored — implemented locally
/// because links.rs's helper is private and typed to its own parser; a small
/// copy here avoids widening that API for one call site. Depth tracking stops
/// at the End that closes the cell (nested emphasis/code/links emit their own
/// Start/End pairs).
fn collect_cell_text<'a, I>(parser: &mut I) -> String
where
    I: Iterator<Item = Event<'a>>,
{
    let mut out = String::new();
    let mut depth = 1usize;
    for event in parser.by_ref() {
        match &event {
            Event::Start(_) => depth += 1,
            Event::End(_) => {
                depth -= 1;
                if depth == 0 {
                    break;
                }
            }
            Event::Text(s) | Event::Code(s) => out.push_str(s),
            Event::SoftBreak | Event::HardBreak => out.push(' '),
            _ => {}
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Convenience: the kinds in document order.
    fn kinds(source: &str) -> Vec<ReadingBlockKind> {
        reading_blocks_source(source)
            .into_iter()
            .map(|b| b.kind)
            .collect()
    }

    /// Every block's `source` must equal the whole-source slice at its
    /// offsets. This is the per-fixture form of the census invariant.
    fn assert_slices_match(source: &str) {
        for b in reading_blocks_source(source) {
            assert_eq!(
                &source[b.byte_start as usize..b.byte_end as usize],
                b.source,
                "slice mismatch for block {:?}",
                b.kind
            );
        }
    }

    // --- empty / trivial ---

    #[test]
    fn empty_source_yields_no_blocks() {
        assert!(reading_blocks_source("").is_empty());
    }

    #[test]
    fn whitespace_only_yields_no_blocks() {
        assert!(reading_blocks_source("\n\n   \n").is_empty());
    }

    // --- headings: ATX + setext, every level ---

    #[test]
    fn atx_headings_every_level() {
        let src = "# h1\n\n## h2\n\n### h3\n\n#### h4\n\n##### h5\n\n###### h6\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::Heading { level: 1 },
                ReadingBlockKind::Heading { level: 2 },
                ReadingBlockKind::Heading { level: 3 },
                ReadingBlockKind::Heading { level: 4 },
                ReadingBlockKind::Heading { level: 5 },
                ReadingBlockKind::Heading { level: 6 },
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn setext_headings_h1_and_h2() {
        let src = "Title\n=====\n\nSection\n-------\n\nbody\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::Heading { level: 1 },
                ReadingBlockKind::Heading { level: 2 },
                ReadingBlockKind::Paragraph,
            ]
        );
        assert_slices_match(src);
    }

    // --- paragraphs, adjacency ---

    #[test]
    fn adjacent_heading_and_paragraph_no_blank_line() {
        let src = "# H\npara immediately after\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::Heading { level: 1 },
                ReadingBlockKind::Paragraph,
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn two_adjacent_paragraphs() {
        let src = "first para\n\nsecond para\n";
        assert_eq!(
            kinds(src),
            vec![ReadingBlockKind::Paragraph, ReadingBlockKind::Paragraph]
        );
    }

    // --- lists: unordered, ordered, nested, tasks with every status char ---

    #[test]
    fn unordered_list_flattens_with_depth() {
        let src = "- a\n- b\n  - c\n    - d\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: None
                },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: None
                },
                ReadingBlockKind::ListItem {
                    depth: 1,
                    ordered: false,
                    task: None
                },
                ReadingBlockKind::ListItem {
                    depth: 2,
                    ordered: false,
                    task: None
                },
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn ordered_list_marks_ordered_true() {
        let src = "1. one\n2. two\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: true,
                    task: None
                },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: true,
                    task: None
                },
            ]
        );
    }

    #[test]
    fn task_items_carry_every_status_char() {
        // Space (open), x/X (done), and project-specific `/` (in
        // progress) and `-` (cancelled) — pulldown's TaskListMarker only
        // knows the first three, so this asserts we go through
        // tasks::task_status_char instead.
        let src = "- [ ] open\n- [x] done\n- [X] done caps\n- [/] doing\n- [-] dropped\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: Some(' ')
                },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: Some('x')
                },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: Some('X')
                },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: Some('/')
                },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: Some('-')
                },
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn non_task_list_item_has_no_task_char() {
        let src = "- just a bullet\n";
        assert_eq!(
            kinds(src),
            vec![ReadingBlockKind::ListItem {
                depth: 0,
                ordered: false,
                task: None
            }]
        );
    }

    #[test]
    fn nested_task_under_plain_item_tracks_depth_and_status() {
        let src = "- parent\n  - [ ] child task\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: None
                },
                ReadingBlockKind::ListItem {
                    depth: 1,
                    ordered: false,
                    task: Some(' ')
                },
            ]
        );
        assert_slices_match(src);
    }

    // --- blockquotes: nested depth, quote children ---

    #[test]
    fn blockquote_leaf_carries_depth_one() {
        let src = "> quoted paragraph\n";
        assert_eq!(kinds(src), vec![ReadingBlockKind::BlockQuote { depth: 1 }]);
        assert_slices_match(src);
    }

    #[test]
    fn nested_blockquote_increments_depth() {
        let src = "> outer\n> > inner\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::BlockQuote { depth: 1 },
                ReadingBlockKind::BlockQuote { depth: 2 },
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn list_inside_quote_is_list_item() {
        // The innermost container (list) names the leaf; the item's
        // depth counts list nesting (0 here — one list level).
        let src = "> - item in quote\n> - item2\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: None
                },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: None
                },
            ]
        );
        assert_slices_match(src);
    }

    // --- code fences, mermaid, dashes-inside-fence ---

    #[test]
    fn fenced_code_carries_language() {
        let src = "```rust\nfn main() {}\n```\n";
        assert_eq!(
            kinds(src),
            vec![ReadingBlockKind::CodeFence {
                language: "rust".to_string()
            }]
        );
        assert_slices_match(src);
    }

    #[test]
    fn untagged_fence_has_empty_language() {
        let src = "```\nplain\n```\n";
        assert_eq!(
            kinds(src),
            vec![ReadingBlockKind::CodeFence {
                language: String::new()
            }]
        );
    }

    #[test]
    fn indented_code_block_is_codefence_empty_language() {
        let src = "para\n\n    indented code\n    line two\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::Paragraph,
                ReadingBlockKind::CodeFence {
                    language: String::new()
                },
            ]
        );
    }

    #[test]
    fn mermaid_fence_is_diagram() {
        let src = "```mermaid\nflowchart LR\nA --> B\n```\n";
        assert_eq!(
            kinds(src),
            vec![ReadingBlockKind::Diagram {
                dialect: "mermaid".to_string()
            }]
        );
        assert_slices_match(src);
    }

    #[test]
    fn mermaid_fence_case_insensitive() {
        let src = "```Mermaid\nflowchart LR\nA --> B\n```\n";
        assert_eq!(
            kinds(src),
            vec![ReadingBlockKind::Diagram {
                dialect: "mermaid".to_string()
            }]
        );
    }

    #[test]
    fn code_fence_with_dashes_inside_does_not_break() {
        // A `---` line inside a fenced block must stay part of the code
        // block — not become a ThematicBreak or a setext underline.
        let src = "```\n---\nnot a break\n---\n```\n";
        assert_eq!(
            kinds(src),
            vec![ReadingBlockKind::CodeFence {
                language: String::new()
            }]
        );
        assert_slices_match(src);
    }

    // --- math blocks ---

    #[test]
    fn display_math_paragraph_is_math_block() {
        let src = "before\n\n$$\n\\sum_{i=0}^n i\n$$\n\nafter\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::Paragraph,
                ReadingBlockKind::MathBlock,
                ReadingBlockKind::Paragraph,
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn single_line_display_math_is_math_block() {
        let src = "$$x^2 + y^2 = z^2$$\n";
        assert_eq!(kinds(src), vec![ReadingBlockKind::MathBlock]);
        assert_slices_match(src);
    }

    #[test]
    fn inline_math_in_prose_stays_paragraph() {
        // `$x$` inline math does not make the paragraph a MathBlock.
        let src = "the value $x$ is small\n";
        assert_eq!(kinds(src), vec![ReadingBlockKind::Paragraph]);
    }

    #[test]
    fn dollar_price_paragraph_is_not_math() {
        let src = "it costs $50 and $100 total\n";
        assert_eq!(kinds(src), vec![ReadingBlockKind::Paragraph]);
    }

    // --- tables, HTML, thematic breaks (raw blocks) ---

    #[test]
    fn table_is_raw_block() {
        let src = "| a | b |\n|---|---|\n| 1 | 2 |\n";
        assert_eq!(kinds(src), vec![ReadingBlockKind::Table]);
        assert_slices_match(src);
    }

    #[test]
    fn html_block_is_raw_and_not_interpreted() {
        let src = "<div>\n<p>hi</p>\n</div>\n";
        assert_eq!(kinds(src), vec![ReadingBlockKind::Html]);
        assert_slices_match(src);
    }

    #[test]
    fn details_html_block_is_html() {
        let src = "<details>\n<summary>more</summary>\nbody\n</details>\n";
        assert_eq!(kinds(src), vec![ReadingBlockKind::Html]);
    }

    #[test]
    fn thematic_break_between_paragraphs() {
        let src = "a\n\n---\n\nb\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::Paragraph,
                ReadingBlockKind::ThematicBreak,
                ReadingBlockKind::Paragraph,
            ]
        );
        assert_slices_match(src);
    }

    // --- frontmatter: skipped, offsets rebased onto whole source ---

    #[test]
    fn frontmatter_is_skipped_but_offsets_are_whole_source() {
        let src = "---\ntitle: x\ntags: [a, b]\n---\n# Heading\n\nbody\n";
        let blocks = reading_blocks_source(src);
        assert_eq!(
            blocks.iter().map(|b| b.kind.clone()).collect::<Vec<_>>(),
            vec![
                ReadingBlockKind::Heading { level: 1 },
                ReadingBlockKind::Paragraph,
            ]
        );
        // The heading's byte_start must land on `# Heading` in the WHOLE
        // source, not in the frontmatter-stripped body.
        let h = &blocks[0];
        assert_eq!(&src[h.byte_start as usize..h.byte_end as usize], h.source);
        assert!(h.source.contains("# Heading"));
        // Offset is past the frontmatter block.
        let fm_end = src.find("# Heading").unwrap();
        assert_eq!(h.byte_start as usize, fm_end);
        assert_slices_match(src);
    }

    #[test]
    fn no_frontmatter_offsets_start_at_zero() {
        let src = "# Heading\n\nbody\n";
        let blocks = reading_blocks_source(src);
        assert_eq!(blocks[0].byte_start, 0);
        assert_slices_match(src);
    }

    #[test]
    fn crlf_frontmatter_body_offsets_correct() {
        let src = "---\r\ntitle: x\r\n---\r\n# H\r\n\r\nbody\r\n";
        assert_slices_match(src);
        let blocks = reading_blocks_source(src);
        assert!(matches!(
            blocks[0].kind,
            ReadingBlockKind::Heading { level: 1 }
        ));
    }

    // --- unicode ---

    #[test]
    fn unicode_content_slices_on_char_boundaries() {
        let src = "# 見出し\n\n段落のテキスト émojis 🎉 more\n\n- リスト項目\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::Heading { level: 1 },
                ReadingBlockKind::Paragraph,
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: None
                },
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn unicode_in_frontmatter_rebases_correctly() {
        let src = "---\nタイトル: 値\n---\n# 見出し\n\n本文\n";
        assert_slices_match(src);
        let blocks = reading_blocks_source(src);
        assert!(blocks[0].source.contains("見出し"));
    }

    // --- mixed adjacency (specialized + generic, no blank lines where legal) ---

    #[test]
    fn mixed_document_in_order() {
        let src = "\
# Title
intro para
## Sub

- item one
- [ ] task two

> a quote

```python
print('hi')
```

$$
E = mc^2
$$

| x | y |
|---|---|
| 1 | 2 |

---

final para
";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::Heading { level: 1 },
                ReadingBlockKind::Paragraph,
                ReadingBlockKind::Heading { level: 2 },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: None
                },
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: Some(' ')
                },
                ReadingBlockKind::BlockQuote { depth: 1 },
                ReadingBlockKind::CodeFence {
                    language: "python".to_string()
                },
                ReadingBlockKind::MathBlock,
                ReadingBlockKind::Table,
                ReadingBlockKind::ThematicBreak,
                ReadingBlockKind::Paragraph,
            ]
        );
        assert_slices_match(src);
    }

    // --- adjacency edge cases (no blank line between blocks) ---

    #[test]
    fn two_adjacent_fences_are_two_blocks() {
        // A single newline (not a blank line) between two fences: pulldown
        // treats them as separate code blocks; the cut model must too.
        let src = "```\na\n```\n```\nb\n```\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::CodeFence {
                    language: String::new()
                },
                ReadingBlockKind::CodeFence {
                    language: String::new()
                },
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn quote_immediately_followed_by_heading_no_blank() {
        let src = "> q\n# H\n";
        assert_eq!(
            kinds(src),
            vec![
                ReadingBlockKind::BlockQuote { depth: 1 },
                ReadingBlockKind::Heading { level: 1 },
            ]
        );
        assert_slices_match(src);
    }

    #[test]
    fn loose_list_item_with_nested_list() {
        // A loose item (its text is wrapped in a paragraph) that also has a
        // nested list: the item's own block covers its paragraph, the
        // nested item is its own block at depth 1, and nothing overlaps.
        let src = "- first para\n\n  second para\n\n  - nested\n";
        let blocks = reading_blocks_source(src);
        assert_eq!(
            blocks.iter().map(|b| b.kind.clone()).collect::<Vec<_>>(),
            vec![
                ReadingBlockKind::ListItem {
                    depth: 0,
                    ordered: false,
                    task: None
                },
                ReadingBlockKind::ListItem {
                    depth: 1,
                    ordered: false,
                    task: None
                },
            ]
        );
        // Non-overlap + the parent item's block includes both its
        // paragraphs (its inline content) but stops before the nested item.
        assert!(blocks[0].byte_end <= blocks[1].byte_start);
        assert!(blocks[0].source.contains("first para"));
        assert!(blocks[0].source.contains("second para"));
        assert!(!blocks[0].source.contains("nested"));
        assert_slices_match(src);
    }

    // --- ordering + non-overlap invariant (fixture-level) ---

    #[test]
    fn blocks_are_ordered_and_non_overlapping() {
        let src = "# H\n\npara\n\n- a\n- b\n\n> q\n\n```\ncode\n```\n";
        let blocks = reading_blocks_source(src);
        for w in blocks.windows(2) {
            assert!(
                w[0].byte_end <= w[1].byte_start,
                "overlap: {:?} then {:?}",
                w[0],
                w[1]
            );
        }
    }

    // --- table cell segmentation (#510) ---

    #[test]
    fn table_cells_basic_2x2() {
        let src = "| a | b |\n|---|---|\n| 1 | 2 |\n";
        let cells = reading_table_cells(src).expect("a table");
        assert_eq!(cells.header, vec!["a", "b"]);
        assert_eq!(cells.rows, vec![vec!["1", "2"]]);
    }

    #[test]
    fn table_cells_alignment_row_does_not_leak() {
        // The `:---` / `:--:` / `---:` delimiter row is table CHROME, never a
        // body row — it must not appear in header or rows.
        let src = "| left | center | right |\n|:---|:--:|---:|\n| a | b | c |\n";
        let cells = reading_table_cells(src).expect("a table");
        assert_eq!(cells.header, vec!["left", "center", "right"]);
        assert_eq!(cells.rows, vec![vec!["a", "b", "c"]]);
    }

    #[test]
    fn table_cells_flatten_inline_content() {
        // Emphasis, inline code, and links reduce to their text content.
        let src = "| x | y | z |\n|---|---|---|\n| **b** | `code` | [t](https://u) |\n";
        let cells = reading_table_cells(src).expect("a table");
        assert_eq!(cells.rows, vec![vec!["b", "code", "t"]]);
    }

    #[test]
    fn table_cells_escaped_pipe_stays_in_cell() {
        // GFM: `\|` is a literal pipe inside a cell, not a column separator.
        let src = "| a | b |\n|---|---|\n| x \\| y | z |\n";
        let cells = reading_table_cells(src).expect("a table");
        assert_eq!(cells.rows, vec![vec!["x | y", "z"]]);
    }

    #[test]
    fn table_cells_ragged_rows_normalized_to_header_width() {
        // A short body row and a long one both come out at header width:
        // pulldown normalizes per GFM, and reading_table_cells pins it.
        let src = "| a | b | c |\n|---|---|---|\n| 1 | 2 |\n| 4 | 5 | 6 | 7 |\n";
        let cells = reading_table_cells(src).expect("a table");
        assert_eq!(cells.header.len(), 3);
        for row in &cells.rows {
            assert_eq!(row.len(), 3, "every body row must equal header width");
        }
        assert_eq!(cells.rows[0], vec!["1", "2", ""]);
        assert_eq!(cells.rows[1], vec!["4", "5", "6"]);
    }

    #[test]
    fn table_cells_unicode_slices_cleanly() {
        let src = "| 見出し | émoji |\n|---|---|\n| 値 🎉 | café |\n";
        let cells = reading_table_cells(src).expect("a table");
        assert_eq!(cells.header, vec!["見出し", "émoji"]);
        assert_eq!(cells.rows, vec![vec!["値 🎉", "café"]]);
    }

    #[test]
    fn table_cells_not_a_table_is_none() {
        assert!(reading_table_cells("just a paragraph\n").is_none());
        assert!(reading_table_cells("# heading\n").is_none());
        assert!(reading_table_cells("").is_none());
        assert!(reading_table_cells("- list item\n").is_none());
    }

    #[test]
    fn table_cells_header_only_has_empty_rows() {
        let src = "| a | b |\n|---|---|\n";
        let cells = reading_table_cells(src).expect("a table");
        assert_eq!(cells.header, vec!["a", "b"]);
        assert!(cells.rows.is_empty());
    }

    /// Integration of the two APIs: feed a segmented Table block's `source`
    /// straight into `reading_table_cells` — the round-trip the Swift consumer
    /// performs.
    #[test]
    fn table_block_source_round_trips_into_cells() {
        let src = "intro\n\n| h1 | h2 |\n|---|---|\n| a | b |\n| c | d |\n\nafter\n";
        let table_block = reading_blocks_source(src)
            .into_iter()
            .find(|b| b.kind == ReadingBlockKind::Table)
            .expect("fixture has a table block");
        let cells = reading_table_cells(&table_block.source).expect("cells from block source");
        assert_eq!(cells.header, vec!["h1", "h2"]);
        assert_eq!(cells.rows, vec![vec!["a", "b"], vec!["c", "d"]]);
    }
}
