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
    ///
    /// `interior` is the **authoritative** code content pulldown-cmark
    /// yields between the fence delimiters — fence lines excluded,
    /// indented blocks dedented, and every CommonMark edge case (a fence
    /// "closed" by a tab-trailing line, an unterminated fence, an indented
    /// block whose first line is triple-backticks) resolved by the parser,
    /// not re-derived downstream. Consumers render `interior` verbatim; the
    /// raw `source` slice (with its delimiters) still equals
    /// `full_source[byte_start..byte_end]` for the census.
    CodeFence { language: String, interior: String },
    /// A display-math (`$$…$$`) block occupying a whole top-level
    /// paragraph.
    MathBlock,
    /// A diagram fence. `dialect` is the fence tag lowercased
    /// (`"mermaid"` today). `interior` is the authoritative fence content
    /// (see [`ReadingBlockKind::CodeFence`]) — a mermaid fence is a code
    /// block to pulldown, so its interior is captured the same way.
    Diagram { dialect: String, interior: String },
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
    // While inside a code block, `code_accum` is `Some((cut_index, interior))`:
    // the index of the just-pushed CodeFence/Diagram cut and the running
    // interior. pulldown emits the code content as `Event::Text` payloads
    // between `Start(CodeBlock)` and `End(CodeBlock)`; we concatenate them
    // verbatim (fence delimiters are NOT Text events, so they never appear;
    // an indented block's Text is already dedented). On `End(CodeBlock)` the
    // interior is written back into the recorded cut's kind. Code blocks never
    // nest, so a single slot suffices.
    let mut code_accum: Option<(usize, String)> = None;

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
                // `interior` is filled from the Text events below; start empty.
                let block_kind = if language.eq_ignore_ascii_case("mermaid") {
                    ReadingBlockKind::Diagram {
                        dialect: language.to_ascii_lowercase(),
                        interior: String::new(),
                    }
                } else {
                    ReadingBlockKind::CodeFence {
                        language,
                        interior: String::new(),
                    }
                };
                let cut = pending_container_start.take().unwrap_or(range.start);
                cuts.push(Cut {
                    start: cut,
                    kind: block_kind,
                });
                // Begin accumulating this block's authoritative interior into
                // the cut we just pushed.
                code_accum = Some((cuts.len() - 1, String::new()));
            }
            Event::End(TagEnd::CodeBlock) => {
                if let Some((idx, interior)) = code_accum.take() {
                    // pulldown appends the final content line's `\n`, so the
                    // interior carries a trailing newline (empty blocks carry
                    // none). Drop exactly that one terminator so the rendered
                    // interior matches the historical fence-strip output for
                    // well-formed fences while still carrying every authored
                    // content line for the pathological cases.
                    let interior = interior
                        .strip_suffix('\n')
                        .map(str::to_string)
                        .unwrap_or(interior);
                    match &mut cuts[idx].kind {
                        ReadingBlockKind::CodeFence { interior: slot, .. }
                        | ReadingBlockKind::Diagram { interior: slot, .. } => {
                            *slot = interior;
                        }
                        _ => {}
                    }
                }
            }
            Event::Text(text) => {
                // Only meaningful inside a code block; elsewhere `code_accum`
                // is None and the payload is inline prose we ignore here.
                if let Some((_, interior)) = code_accum.as_mut() {
                    interior.push_str(&text);
                }
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

/// Context needed to recognize editor-suppressing display math in one
/// CommonMark pass.
pub(crate) struct EditorDisplayMathContext {
    pub openers: Vec<usize>,
    pub code_ranges: Vec<std::ops::Range<usize>>,
}

/// Exact trim-start `$$` offsets for top-level paragraphs, plus every code
/// range in which the delimiter scanner must remain inert.
///
/// This is the reading-view half of the editor interaction math guard: the
/// stateful document buffer combines these context-qualified openers with the
/// canonical math scanner's delimiter extent. Keeping the qualification here
/// prevents mid-line, list, blockquote, table, and code occurrences from being
/// promoted to interactive display-math regions.
pub(crate) fn editor_display_math_context(source: &str) -> EditorDisplayMathContext {
    let mut stack: Vec<Container> = Vec::new();
    let mut openers = Vec::new();
    let mut code_ranges = Vec::new();
    for (event, range) in Parser::new_ext(source, READING_PARSE_OPTIONS).into_offset_iter() {
        match event {
            Event::Code(_) | Event::Start(Tag::CodeBlock(_)) => {
                code_ranges.push(range);
            }
            Event::Start(Tag::List(first_number)) => {
                stack.push(Container::List {
                    ordered: first_number.is_some(),
                });
            }
            Event::Start(Tag::Item) => stack.push(Container::Item),
            Event::Start(Tag::BlockQuote(_)) => stack.push(Container::Quote),
            Event::Start(Tag::Paragraph)
                if !inside_item(&stack)
                    && quote_depth(&stack).is_none()
                    && source[range.clone()].trim_start().starts_with("$$") =>
            {
                let paragraph = &source[range.clone()];
                let leading = paragraph.len() - paragraph.trim_start().len();
                openers.push(range.start + leading);
            }
            Event::End(TagEnd::List(_))
            | Event::End(TagEnd::Item)
            | Event::End(TagEnd::BlockQuote(_)) => {
                stack.pop();
            }
            _ => {}
        }
    }
    EditorDisplayMathContext {
        openers,
        code_ranges,
    }
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
    // A malformed input might not fire TableHead (or fire one with fewer
    // cells than the delimiter row declares); fall back to the alignment
    // count so the width is still well-defined. The delimiter row is
    // authoritative — pad HEADER to it too, not just rows, so a short/absent
    // header can never leave trailing body cells with no header to pair
    // with (Swift derives its columns from `header.len()`, so an unpadded
    // header would silently drop real cells rather than showing them under
    // an honest empty label).
    let width = header.len().max(alignments_len);
    header.resize(width, String::new());

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

// =======================================================================
// Reading inline segments (#967 · docs/plans/18_windows_port/specs/
// w3_inline_runs_spec.md)
// =======================================================================
//
// The block walk above names each block; this half renders each
// paragraph-family block's INLINE content canonically, so every host
// (mac `ReadingInlineMapper`, Windows WPF `Run`/`Hyperlink`) applies
// attributes to core-computed runs instead of re-deriving wikilink /
// embed / tag / citation semantics per platform (program decisions 4/5).
//
// Pipeline, per block:
//   1. chrome strip  — §2: heading markers, list marker + checkbox,
//      quote `>` prefixes. Degrades to the verbatim slice.
//   2. token selection — §3: the `mappableSpans` policy over the
//      canonical span classifier ([`crate::editor_spans`]).
//   3. token payloads — §4: reuse `links::split_wikilink_body`,
//      the `AppState.embedTargetKey` composition, citation join.
//   4. inline walk — §6: pulldown-cmark over the token-MASKED content
//      under [`READING_PARSE_OPTIONS`], so a `*` or backtick inside a
//      selected token can never pair with a delimiter outside it
//      (the splice-equivalence rule, achieved structurally rather than
//      by the retired backslash-escaping splice).
//   5. resolution — §6: candidate keys per grammar, first same-grammar
//      record decides; no same-grammar record ⇒ unresolved.

/// One inline character style carried by a run. Sorted + deduped on the
/// run so two hosts stamping attributes can compare styles directly.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub enum ReadingInlineStyle {
    Emphasis,
    Strong,
    Strikethrough,
    InlineCode,
}

/// Which authoring grammar produced a wiki-routed target — decides the
/// anchor-cut rules [`reading_match_link`] applies (`^` is an anchor
/// marker in wikilink grammar but a legal path character in a Markdown
/// destination).
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum ReadingWikiGrammar {
    Wikilink,
    MarkdownDestination,
}

/// What one run IS — the affordance a host wires to it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ReadingInlineRunKind {
    /// Plain prose (also: the label of a never-activatable destination —
    /// `file:`/`javascript:`/unknown schemes, protocol-relative `//host`,
    /// fragment-only `#anchor` — which must render with NO affordance
    /// rather than as a dead link).
    Text,
    /// An activatable external destination (`http`/`https`/`mailto`).
    ExternalLink { url: String },
    /// A vault-internal note reference, from either grammar.
    Wikilink {
        /// Anchor-attached authored form (`Note#Sec`) — the router input.
        target: String,
        /// Anchor-cut form per `grammar`.
        base_target: String,
        anchor: Option<crate::links::LinkAnchor>,
        grammar: ReadingWikiGrammar,
        /// False when no same-grammar record vouches for the target, or
        /// the matching record is itself unresolved.
        resolved: bool,
    },
    /// A mid-paragraph `![[…]]` embed run. `key` is the cache-key form
    /// (§4) — no `resolved` field: card-level state owns embed status.
    Embed { key: String },
    /// An inline `#tag`. The run's text keeps the `#`; `name` drops it.
    Tag { name: String },
    /// A Pandoc citation. The run's text is the rendered visual form.
    Citation { raw: String, speech: String },
}

/// One flat, non-overlapping run over a segment's `content`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReadingInlineRun {
    /// Byte offsets into the owning segment's `content`, half-open.
    pub start: u32,
    pub end: u32,
    pub styles: Vec<ReadingInlineStyle>,
    pub kind: ReadingInlineRunKind,
    /// Per-range accessible text a host stamps with its own AX-text
    /// mechanism (§7): citation speech, or `"Unresolved link"`. `None`
    /// means the run's own text is its accessible text.
    pub ax_text: Option<String>,
}

/// The rendered inline content of one block.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ReadingInlineSegment {
    /// The RENDERED inline text: block chrome stripped, token display
    /// substituted, Markdown syntax consumed. `runs` partition it
    /// exactly — `concat(run slices) == content`, no gaps, no overlaps.
    pub content: String,
    pub runs: Vec<ReadingInlineRun>,
    /// `Some` for task list items, from the canonical `tasks.rs` rule.
    pub task_completed: Option<bool>,
}

/// Per-block inline result, 1:1 and same-order with
/// [`reading_blocks_source`].
#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub struct ReadingBlockInlines {
    /// Empty for kinds with no inline content (code, math, diagram,
    /// table, HTML, thematic break); exactly one segment otherwise.
    pub segments: Vec<ReadingInlineSegment>,
    /// `Some(cache-key)` when this block IS one wikilink embed (§5) and
    /// therefore expands in place as a card instead of rendering inline.
    pub block_embed_key: Option<String>,
    /// The authored list marker (`-`, `*`, `+`, `3.`, `12)`) for
    /// `ListItem` blocks, verbatim.
    ///
    /// Spec amendment (this PR): `w3_inline_runs_spec.md` §1 did not
    /// carry it, but the mac renderer shows the AUTHORED ordinal
    /// (`ReadingBlockSource.listItemParts(...).marker` — "the source
    /// carries the real ordinal, so no re-derivation and no wrong
    /// renumbering is possible"), so a host without it would have to
    /// re-split the marker itself — exactly the per-host derivation
    /// decision 4 forbids. Carried here so both hosts read one value.
    pub list_marker: Option<String>,
}

/// Render every block's inline content canonically.
///
/// Pure — no IO. The result is 1:1 with `reading_blocks_source(source)`
/// in the same order, so a consumer zips the two. Output is a pure
/// function of `(source, citations, records)`: same inputs, same bytes,
/// every platform — which is what makes the §W-A `inline_runs` artifact
/// meaningful.
///
/// `citations` is the owning note's rendered citations (join key:
/// [`crate::RenderedCitation::raw`]); `records` is its outgoing-link
/// records, the resolution input. An EMPTY `records` classifies every
/// link run unresolved — the honest value for a host whose link query
/// still belongs to the previous note.
pub fn reading_inline_segments_source(
    source: &str,
    citations: &[crate::RenderedCitation],
    records: &[crate::links_db::OutgoingLink],
) -> Vec<ReadingBlockInlines> {
    let sets = RecordSets::from_records(records);
    reading_blocks_source(source)
        .iter()
        .map(|block| block_inlines(block, citations, &sets))
        .collect()
}

/// Index of the outgoing-link record one activation matches, or `None`.
///
/// The router's activation path and the render-time `resolved`
/// classifier share this one implementation, so a run can never style
/// as resolved and then announce "unresolved" on activation (#849).
/// Candidate keys are ordered per grammar (§6): wikilink cuts the anchor
/// at the first `#`, else the first `^`; a Markdown destination cuts
/// only at `#` (`^` is a path character there); the verbatim target
/// closes each list as the pre-#509 defense. A record of the OTHER
/// grammar never vouches.
pub fn reading_match_link(
    target: &str,
    grammar: ReadingWikiGrammar,
    embed: bool,
    records: &[crate::links_db::OutgoingLink],
) -> Option<u32> {
    for key in candidate_keys(target, grammar) {
        if let Some(index) = records.iter().position(|record| {
            record.is_embed == embed
                // EXTERNAL records never answer an internal activation.
                //
                // `resolve_link` classifies by target SHAPE, not by
                // authoring grammar, so `[[https://example.com]]` is
                // stored as a `wikilink`-kind record with
                // `is_external = true`. The render-time classifier
                // ([`RecordSets`]) already skips those, so the run styles
                // unresolved and carries "Unresolved link". Without the
                // same predicate here, activation would match that record
                // and hand the URL to the system opener — the rendered
                // state and the activation would cross different trust
                // boundaries, which is exactly the disagreement this one
                // shared matcher exists to make impossible (#849).
                && !record.is_external
                && record.target_raw == key
                && record_kind_matches(&record.kind, grammar)
        }) {
            return u32::try_from(index).ok();
        }
    }
    None
}

// --- Block dispatch ----------------------------------------------------

fn block_inlines(
    block: &ReadingBlock,
    citations: &[crate::RenderedCitation],
    sets: &RecordSets,
) -> ReadingBlockInlines {
    // §5: block-level embed detection runs on the block's RAW source
    // (the mac `blockEmbedTarget(inSlice:)` contract), Paragraph only.
    let block_embed_key = match block.kind {
        ReadingBlockKind::Paragraph => block_embed_key(&block.source),
        _ => None,
    };

    let (content, task_completed, list_marker) = match &block.kind {
        ReadingBlockKind::Heading { .. } => (heading_text(&block.source), None, None),
        ReadingBlockKind::Paragraph => (block.source.clone(), None, None),
        ReadingBlockKind::ListItem { task, .. } => {
            let parts = list_item_parts(&block.source, task.is_some());
            let completed = task.map(|c| c == 'x' || c == 'X');
            match parts {
                Some(parts) => (parts.content, completed, Some(parts.marker)),
                // Degradation contract: no marker found ⇒ verbatim slice.
                None => (block.source.clone(), completed, None),
            }
        }
        ReadingBlockKind::BlockQuote { depth } => {
            (quote_content(&block.source, *depth), None, None)
        }
        // No inline content: the block's own artifact carries the bytes.
        ReadingBlockKind::CodeFence { .. }
        | ReadingBlockKind::MathBlock
        | ReadingBlockKind::Diagram { .. }
        | ReadingBlockKind::Table
        | ReadingBlockKind::ThematicBreak
        | ReadingBlockKind::Html => {
            return ReadingBlockInlines {
                segments: Vec::new(),
                block_embed_key,
                list_marker: None,
            };
        }
    };

    let (content, runs) = render_inlines(&content, citations, sets);
    ReadingBlockInlines {
        segments: vec![ReadingInlineSegment {
            content,
            runs,
            task_completed,
        }],
        block_embed_key,
        list_marker,
    }
}

// --- §2 content derivation --------------------------------------------

/// Swift's `CharacterSet.whitespaces` — U+0009 plus Unicode general
/// category Zs. Deliberately NOT `char::is_whitespace`: the shipped mac
/// chrome strippers trim with `.whitespaces`, which does **not** include
/// CR/LF, so a CRLF fixture's `\r` survives into the rendered content.
/// Line endings are never normalized (§W-A / program decision 9).
fn is_inline_space(c: char) -> bool {
    matches!(
        c,
        '\u{0009}' | '\u{0020}' | '\u{00A0}' | '\u{1680}' | '\u{2000}'
            ..='\u{200A}' | '\u{202F}' | '\u{205F}' | '\u{3000}'
    )
}

/// Swift's `CharacterSet.whitespacesAndNewlines`.
fn is_inline_space_or_newline(c: char) -> bool {
    is_inline_space(c)
        || matches!(
            c,
            '\u{000A}'..='\u{000D}' | '\u{0085}' | '\u{2028}' | '\u{2029}'
        )
}

fn trim_inline_space(s: &str) -> &str {
    s.trim_matches(is_inline_space)
}

/// ATX (`#`..`######`, optional closing hash run) or setext heading text.
/// Degrades to the whitespace-trimmed verbatim slice.
fn heading_text(source: &str) -> String {
    let lines: Vec<&str> = source.split('\n').collect();
    let trimmed_first = trim_inline_space(lines[0]);
    let hashes = trimmed_first.chars().take_while(|c| *c == '#').count();
    if (1..=6).contains(&hashes) {
        // `#` is one byte, so the char count is also the byte offset.
        let text = &trimmed_first[hashes..];
        let first = text.chars().next();
        if text.is_empty() || first == Some(' ') || first == Some('\t') {
            let mut text = trim_inline_space(text);
            // Shipped behavior: the closing run is stripped
            // unconditionally (CommonMark would require a preceding
            // space). Preserved verbatim rather than "fixed" here.
            while let Some(stripped) = text.strip_suffix('#') {
                text = stripped;
            }
            return trim_inline_space(text).to_string();
        }
        // `#not-a-heading`: the classifier said Heading, so this is a
        // setext or unusual form — fall through.
    }
    setext_or_verbatim(&lines, source)
}

fn setext_or_verbatim(lines: &[&str], source: &str) -> String {
    if lines.len() >= 2 {
        let underline = trim_inline_space(lines[1]);
        if !underline.is_empty()
            && (underline.chars().all(|c| c == '=') || underline.chars().all(|c| c == '-'))
        {
            return trim_inline_space(lines[0]).to_string();
        }
    }
    source.trim_matches(is_inline_space_or_newline).to_string()
}

struct ListItemParts {
    marker: String,
    content: String,
}

/// Split a list-item slice into marker / optional task box / content.
/// `strip_task_box` must be true ONLY when the block kind already says
/// this item IS a task — taskhood belongs to the classifier, not this
/// splitter (a plain `1. [v] Visible` keeps its bracket text).
fn list_item_parts(source: &str, strip_task_box: bool) -> Option<ListItemParts> {
    let (first_line, rest) = match source.find('\n') {
        Some(idx) => (&source[..idx], &source[idx + 1..]),
        None => (source, ""),
    };

    let mut cursor = first_line.trim_start_matches([' ', '\t']);
    let first = cursor.chars().next()?;
    let marker;
    if first == '-' || first == '*' || first == '+' {
        marker = first.to_string();
        cursor = &cursor[first.len_utf8()..];
    } else if first.is_numeric() {
        let digits_len = cursor
            .char_indices()
            .take_while(|(_, c)| c.is_numeric())
            .map(|(i, c)| i + c.len_utf8())
            .last()
            .unwrap_or(0);
        let delimiter = cursor[digits_len..].chars().next()?;
        if delimiter != '.' && delimiter != ')' {
            return None;
        }
        let end = digits_len + delimiter.len_utf8();
        marker = cursor[..end].to_string();
        cursor = &cursor[end..];
    } else {
        return None;
    }

    cursor = cursor.trim_start_matches([' ', '\t']);
    let mut content = cursor.to_string();

    if strip_task_box && content.starts_with('[') && content.chars().count() >= 3 {
        let mut chars = content.char_indices();
        chars.next();
        let status = chars.next();
        let close = chars.next();
        if let (Some(_), Some((close_idx, ']'))) = (status, close) {
            let after = &content[close_idx + 1..];
            content = after.strip_prefix(' ').unwrap_or(after).to_string();
        }
    }

    if !rest.is_empty() {
        content.push('\n');
        content.push_str(rest);
    }
    Some(ListItemParts { marker, content })
}

/// Strip up to `depth` `>` markers (each with one optional following
/// space) from the start of every line.
fn quote_content(source: &str, depth: u8) -> String {
    let rounds = std::cmp::max(1, depth as usize);
    source
        .split('\n')
        .map(|line| {
            let mut view = line;
            for _ in 0..rounds {
                let lead = view.trim_start_matches([' ', '\t']);
                if !lead.starts_with('>') {
                    break;
                }
                view = &lead[1..];
                view = view.strip_prefix(' ').unwrap_or(view);
            }
            view
        })
        .collect::<Vec<_>>()
        .join("\n")
}

// --- §3 token selection ------------------------------------------------

/// The `mappableSpans` policy, verbatim: wikilink / embed / tag /
/// citation spans, outermost-first at equal start, dropping (a) spans
/// nested inside an already-kept span, (b) spans overlapping inline-code
/// or fence ranges (a construct rendered AS code stays literal), and
/// (c) spans overlapping a Markdown link/image span (the Markdown
/// construct owns that range).
fn mappable_spans(
    spans: &[crate::editor_spans::EditorSpan],
) -> Vec<crate::editor_spans::EditorSpan> {
    use crate::editor_spans::EditorSpanKind as K;

    let opaque: Vec<(u32, u32)> = spans
        .iter()
        .filter(|s| {
            matches!(
                s.kind,
                K::InlineCode | K::CodeFence | K::Code(_) | K::Link | K::Image
            )
        })
        .map(|s| (s.start_byte, s.end_byte))
        .collect();

    let mut candidates: Vec<crate::editor_spans::EditorSpan> = spans
        .iter()
        .filter(|s| matches!(s.kind, K::Wikilink | K::Embed | K::Tag | K::Citation))
        .cloned()
        .collect();
    // Start ascending; at equal start the OUTERMOST (longest) first.
    candidates.sort_by(|a, b| {
        a.start_byte
            .cmp(&b.start_byte)
            .then(b.end_byte.cmp(&a.end_byte))
    });

    let mut kept: Vec<crate::editor_spans::EditorSpan> = Vec::new();
    let mut covered_end = 0u32;
    for span in candidates {
        if span.start_byte < covered_end {
            continue;
        }
        if opaque
            .iter()
            .any(|(s, e)| span.start_byte < *e && span.end_byte > *s)
        {
            continue;
        }
        covered_end = span.end_byte;
        kept.push(span);
    }
    kept
}

/// Every span the inline pipeline consults: the canonical highlight
/// classifier plus the CommonMark link/image spans it intentionally
/// omits (those arrive only as exclusion ranges).
fn inline_spans(text: &str) -> Vec<crate::editor_spans::EditorSpan> {
    use crate::editor_spans::EditorSpanKind as K;
    let mut spans = crate::editor_spans::highlight_spans(text);
    spans.extend(
        crate::editor_spans::markdown_spans(text)
            .into_iter()
            .filter(|s| matches!(s.kind, K::Link | K::Image)),
    );
    spans
}

// --- §5 block-level embed detection ------------------------------------

/// `Some(cache-key)` iff `slice` IS a single wikilink embed — exactly one
/// selected span, of kind Embed, covering every non-whitespace byte.
///
/// The slice-level entry point for callers that only need detection and
/// already know the block is a Paragraph (the shipped
/// `blockEmbedTarget(inSlice:)` contract), without paying for a whole
/// document's inline segments. [`reading_inline_segments_source`] reports
/// the same value per block in `block_embed_key`.
pub fn reading_block_embed_key(slice: &str) -> Option<String> {
    block_embed_key(slice)
}

/// `Some(cache-key)` iff `slice` is a paragraph that IS a single
/// wikilink embed — exactly one selected span, of kind Embed, covering
/// every non-whitespace byte. Scope pinned as shipped (#511): wikilink
/// embeds only; Markdown images never block-expand.
fn block_embed_key(slice: &str) -> Option<String> {
    let spans = inline_spans(slice);
    let mappable = mappable_spans(&spans);
    if mappable.len() != 1 || mappable[0].kind != crate::editor_spans::EditorSpanKind::Embed {
        return None;
    }
    let span = &mappable[0];
    let start = span.start_byte as usize;
    let end = span.end_byte as usize;
    let bytes = slice.as_bytes();
    if start >= end || end > bytes.len() {
        return None;
    }
    // Markdown whitespace, byte-level (every one is single-byte in
    // UTF-8, so this can't split a multibyte scalar).
    let is_space = |b: u8| matches!(b, 0x20 | 0x09 | 0x0A | 0x0D | 0x0C | 0x0B);
    if bytes[..start].iter().any(|b| !is_space(*b)) || bytes[end..].iter().any(|b| !is_space(*b)) {
        return None;
    }
    embed_parts(&slice[start..end]).map(|parts| parts.key)
}

// --- §4 token payloads -------------------------------------------------

struct EmbedParts {
    key: String,
    display: String,
}

/// The embed cache-key composition — `target_raw` plus the anchor marker
/// (`^` for a block ref, `#` for a heading) plus the anchor text.
///
/// The single home for the key both the reading pipeline and the hosts'
/// embed-resolution dictionaries are keyed on (the mac
/// `AppState.embedTargetKey` copy delegates here). `target_raw` is the
/// ANCHOR-CUT form — the same value `links_db` records — so a key
/// composed from a record and a key composed from authored source text
/// are byte-identical for the same embed.
pub fn reading_embed_key(target_raw: &str, anchor: Option<&crate::links::LinkAnchor>) -> String {
    match anchor {
        Some(crate::links::LinkAnchor::Block(text)) => format!("{target_raw}^{text}"),
        Some(crate::links::LinkAnchor::Heading(text)) => format!("{target_raw}#{text}"),
        None => target_raw.to_string(),
    }
}

/// Divide a confirmed `![[…]]` span into its cache key + display text.
///
/// `key` is the `AppState.embedTargetKey` composition — the ANCHOR-CUT
/// `target_raw` plus the anchor marker (`^` for a block ref, `#` for a
/// heading) plus the anchor text — which core now owns as the single
/// home. That is a deliberate behavior fix over the retired Swift
/// mapper, which used the authored segment verbatim and therefore
/// composed a key the resolution dictionary could not match for the
/// canonical `![[Note#^blk]]` block-ref form and for padded interiors
/// (`![[ Note # Sec ]]`). Recorded in this PR's deltas ledger.
fn embed_parts(span_text: &str) -> Option<EmbedParts> {
    let inner = span_text.strip_prefix('!')?;
    let inner = wikilink_interior(inner)?;
    let (base_target, alias, anchor) = crate::links::split_wikilink_body(inner);
    if base_target.is_empty() {
        return None;
    }
    let key = reading_embed_key(&base_target, anchor.as_ref());
    // Alt-text contract: alias, else the target's NAME (last path
    // component) — never empty, so an image embed always carries a
    // non-empty accessible label.
    let name = last_path_component(&base_target);
    let display = alias
        .filter(|a| !a.is_empty())
        .or_else(|| Some(name).filter(|n| !n.is_empty()))
        .unwrap_or_else(|| crate::links::wikilink_target_segment(inner));
    Some(EmbedParts { key, display })
}

/// The text between `[[` and `]]`, or `None` when the delimiters aren't
/// both present (the span boundary came from the classifier, so this
/// only divides a confirmed interior).
fn wikilink_interior(span_text: &str) -> Option<&str> {
    let inner = span_text.strip_prefix("[[")?.strip_suffix("]]")?;
    if inner.is_empty() { None } else { Some(inner) }
}

/// `NSString.lastPathComponent` semantics: the segment after the last
/// `/`, with trailing slashes ignored; an all-slash input keeps `/`.
fn last_path_component(target: &str) -> String {
    let trimmed = target.trim_end_matches('/');
    if trimmed.is_empty() {
        return if target.is_empty() {
            String::new()
        } else {
            "/".to_string()
        };
    }
    match trimmed.rfind('/') {
        Some(idx) => trimmed[idx + 1..].to_string(),
        None => trimmed.to_string(),
    }
}

/// One selected token's rendered form.
struct TokenRun {
    display: String,
    kind: ReadingInlineRunKind,
    ax_text: Option<String>,
    /// The authored bytes the token replaced. Emitted instead of
    /// `display` when the mask lands inside a construct that binds
    /// tighter than a link — see [`TokenMode::Literal`].
    source: String,
    /// True for an escaped authored [`TOKEN_MARKER`] scalar rather than a
    /// selected Slate token: it renders as that scalar with no affordance
    /// and no construct break, so surrounding text still coalesces.
    is_escape: bool,
}

fn map_token(
    kind: &crate::editor_spans::EditorSpanKind,
    span_text: &str,
    citations: &[crate::RenderedCitation],
    sets: &RecordSets,
) -> Option<TokenRun> {
    use crate::editor_spans::EditorSpanKind as K;
    match kind {
        K::Wikilink => {
            let inner = wikilink_interior(span_text)?;
            let target = crate::links::wikilink_target_segment(inner);
            if target.is_empty() {
                // `[[ ]]`-shaped interior: contribute the bytes as plain
                // text rather than an empty, invisible affordance.
                return None;
            }
            let (base_target, alias, anchor) = crate::links::split_wikilink_body(inner);
            let display = alias
                .filter(|a| !a.is_empty())
                .unwrap_or_else(|| target.clone());
            let resolved = is_resolved(&target, ReadingWikiGrammar::Wikilink, sets);
            Some(TokenRun {
                source: span_text.to_string(),
                is_escape: false,
                display,
                kind: ReadingInlineRunKind::Wikilink {
                    target,
                    base_target,
                    anchor,
                    grammar: ReadingWikiGrammar::Wikilink,
                    resolved,
                },
                ax_text: if resolved {
                    None
                } else {
                    Some(UNRESOLVED_LINK_AX_TEXT.to_string())
                },
            })
        }
        K::Embed => {
            let parts = embed_parts(span_text)?;
            Some(TokenRun {
                source: span_text.to_string(),
                is_escape: false,
                display: parts.display,
                kind: ReadingInlineRunKind::Embed { key: parts.key },
                ax_text: None,
            })
        }
        K::Tag => {
            if !span_text.starts_with('#') || span_text.chars().count() <= 1 {
                return None;
            }
            Some(TokenRun {
                source: span_text.to_string(),
                is_escape: false,
                display: span_text.to_string(),
                kind: ReadingInlineRunKind::Tag {
                    name: span_text[1..].to_string(),
                },
                ax_text: None,
            })
        }
        K::Citation => {
            let matched = citations.iter().find(|c| c.raw == span_text);
            let display = matched
                .map(|c| c.visual_text.clone())
                .filter(|t| !t.is_empty())
                .unwrap_or_else(|| span_text.to_string());
            let speech = matched
                .map(|c| c.speech_text.clone())
                .filter(|t| !t.is_empty())
                .unwrap_or_else(|| span_text.to_string());
            Some(TokenRun {
                source: span_text.to_string(),
                is_escape: false,
                display,
                kind: ReadingInlineRunKind::Citation {
                    raw: span_text.to_string(),
                    speech: speech.clone(),
                },
                ax_text: Some(speech),
            })
        }
        _ => None,
    }
}

/// The one accessible-text string this layer owns (§7 / decision 18:
/// strings MOVE from the host, no new core copy).
const UNRESOLVED_LINK_AX_TEXT: &str = "Unresolved link";

// --- §6 resolution -----------------------------------------------------

/// The note's link records, partitioned by authoring grammar exactly as
/// the router partitions them — a record of the OTHER grammar must never
/// vouch for a target.
#[derive(Debug, Default)]
struct RecordSets {
    known_wikilink: std::collections::HashSet<String>,
    unresolved_wikilink: std::collections::HashSet<String>,
    known_markdown: std::collections::HashSet<String>,
    unresolved_markdown: std::collections::HashSet<String>,
}

impl RecordSets {
    fn from_records(records: &[crate::links_db::OutgoingLink]) -> Self {
        let mut sets = Self::default();
        for record in records.iter().filter(|r| !r.is_embed && !r.is_external) {
            let (known, unresolved) = match record.kind.as_str() {
                "wikilink" => (&mut sets.known_wikilink, &mut sets.unresolved_wikilink),
                "markdown" => (&mut sets.known_markdown, &mut sets.unresolved_markdown),
                _ => continue,
            };
            known.insert(record.target_raw.clone());
            if record.is_unresolved {
                unresolved.insert(record.target_raw.clone());
            }
        }
        sets
    }

    fn known(&self, grammar: ReadingWikiGrammar) -> &std::collections::HashSet<String> {
        match grammar {
            ReadingWikiGrammar::Wikilink => &self.known_wikilink,
            ReadingWikiGrammar::MarkdownDestination => &self.known_markdown,
        }
    }

    fn unresolved(&self, grammar: ReadingWikiGrammar) -> &std::collections::HashSet<String> {
        match grammar {
            ReadingWikiGrammar::Wikilink => &self.unresolved_wikilink,
            ReadingWikiGrammar::MarkdownDestination => &self.unresolved_markdown,
        }
    }
}

fn record_kind_matches(kind: &str, grammar: ReadingWikiGrammar) -> bool {
    match grammar {
        ReadingWikiGrammar::Wikilink => kind == "wikilink",
        ReadingWikiGrammar::MarkdownDestination => kind == "markdown",
    }
}

/// Anchor-cut form for one grammar (§6): wikilink cuts at the first `#`,
/// else the first `^`; a Markdown destination cuts ONLY at `#`.
fn base_target(target: &str, grammar: ReadingWikiGrammar) -> String {
    let trimmed = target.trim();
    match grammar {
        ReadingWikiGrammar::Wikilink => {
            if let Some(idx) = trimmed.find('#') {
                trimmed[..idx].trim().to_string()
            } else if let Some(idx) = trimmed.find('^') {
                trimmed[..idx].trim().to_string()
            } else {
                trimmed.to_string()
            }
        }
        ReadingWikiGrammar::MarkdownDestination => match trimmed.find('#') {
            Some(idx) => trimmed[..idx].trim().to_string(),
            None => trimmed.to_string(),
        },
    }
}

/// Ordered record-match keys for one routed target. The verbatim target
/// closes the list as the pre-#509 defense (rows still carrying a
/// fragment in `target_raw`).
fn candidate_keys(target: &str, grammar: ReadingWikiGrammar) -> Vec<String> {
    let trimmed = target.trim();
    let mut keys: Vec<String> = Vec::new();
    let push = |key: String, keys: &mut Vec<String>| {
        if !key.is_empty() && !keys.contains(&key) {
            keys.push(key);
        }
    };
    match grammar {
        ReadingWikiGrammar::Wikilink => push(base_target(trimmed, grammar), &mut keys),
        ReadingWikiGrammar::MarkdownDestination => {
            if trimmed.contains('#') {
                push(base_target(trimmed, grammar), &mut keys);
            }
        }
    }
    push(trimmed.to_string(), &mut keys);
    keys
}

/// The first candidate key with a SAME-GRAMMAR record decides; no
/// same-grammar record at all ⇒ unresolved (live-buffer links the saved
/// index has not seen). This is exactly what activation announces for
/// the same run, so styling and activation can never disagree.
fn is_resolved(target: &str, grammar: ReadingWikiGrammar, sets: &RecordSets) -> bool {
    let known = sets.known(grammar);
    for key in candidate_keys(target, grammar) {
        if known.contains(&key) {
            return !sets.unresolved(grammar).contains(&key);
        }
    }
    false
}

/// Destination classification for a Markdown link (§6).
fn markdown_destination_kind(
    dest: &str,
    sets: &RecordSets,
) -> (ReadingInlineRunKind, Option<String>) {
    // Activation allowlist first: only these three ever reach the
    // system opener (the same list `AppState.openLink` enforces).
    if let Some(scheme) = uri_scheme(dest)
        && matches!(scheme.as_str(), "http" | "https" | "mailto")
    {
        return (
            ReadingInlineRunKind::ExternalLink {
                url: dest.to_string(),
            },
            None,
        );
    }
    // Never-activatable: any other scheme, `//host`, `#anchor`, empty.
    if dest.is_empty() || crate::links::looks_external_for_resolver(dest) {
        return (ReadingInlineRunKind::Text, None);
    }
    // Scheme-less internal destination: routed like a wikilink but with
    // MARKDOWN anchor-cut rules — `^` stays in the path here. The target
    // is the authored destination VERBATIM (never percent-decoded: the
    // `target_raw` contract).
    let (base, anchor) = crate::links::split_markdown_target(dest);
    let grammar = ReadingWikiGrammar::MarkdownDestination;
    let resolved = is_resolved(dest, grammar, sets);
    (
        ReadingInlineRunKind::Wikilink {
            target: dest.to_string(),
            base_target: base,
            anchor,
            grammar,
            resolved,
        },
        if resolved {
            None
        } else {
            Some(UNRESOLVED_LINK_AX_TEXT.to_string())
        },
    )
}

/// The RFC 3986 scheme of `url`, lowercased, or `None`. Mirrors the
/// scheme rule in `links::looks_external` (ALPHA first, length ≥ 2) so a
/// Windows drive letter (`C:\notes\foo.md`) is not read as a scheme.
fn uri_scheme(url: &str) -> Option<String> {
    let colon = url.find(':')?;
    let scheme = &url[..colon];
    let mut chars = scheme.chars();
    let first = chars.next()?;
    if scheme.len() >= 2
        && first.is_ascii_alphabetic()
        && chars.all(|c| c.is_ascii_alphanumeric() || c == '+' || c == '-' || c == '.')
    {
        Some(scheme.to_ascii_lowercase())
    } else {
        None
    }
}

// --- §6 inline walk ----------------------------------------------------

/// Render one block's chrome-stripped content into `(content, runs)`.
fn render_inlines(
    source: &str,
    citations: &[crate::RenderedCitation],
    sets: &RecordSets,
) -> (String, Vec<ReadingInlineRun>) {
    let spans = inline_spans(source);
    let mappable = mappable_spans(&spans);

    // Mask each selected token with ONE sentinel scalar. Masking is what
    // makes token interiors opaque to delimiter pairing (§6): a `*` or
    // backtick inside `[[a*b]]` can no longer open or close anything
    // outside it, and the token's own display text never re-parses.
    //
    // Each token is masked as `<marker><index><marker>` — the INDEX is
    // what makes expansion safe.
    //
    // Assigning tokens by the order sentinels are encountered is not
    // sound: pulldown exposes a link's destination and title as Tag
    // metadata, and NO event renders them. A token masked inside what
    // later becomes a link title (reachable, because selection runs on
    // the block-parsed content while the walk runs on the flattened
    // probe) would therefore never be consumed, and every later token
    // would expand with the previous token's payload — a wrong label and
    // a wrong activation target, silently. Carrying the index makes that
    // structurally impossible: an unrendered mark is simply never
    // emitted, and its neighbours are unaffected.
    // Authored text is copied through `push_escaped`, so the only
    // TOKEN_MARKER scalars in `masked` are delimiters we wrote — which is
    // what lets the delimiter stay ONE fixed-width scalar and keeps
    // construction linear.
    let mut masked = String::with_capacity(source.len());
    let mut tokens: Vec<TokenRun> = Vec::new();
    let mut cursor = 0usize;
    for span in &mappable {
        let start = span.start_byte as usize;
        let end = span.end_byte as usize;
        if start < cursor || end > source.len() || start >= end {
            continue;
        }
        push_escaped(&mut masked, &source[cursor..start], &mut tokens);
        match map_token(&span.kind, &source[start..end], citations, sets) {
            Some(token) => {
                // Index 0 is reserved for the scaffold mark below.
                masked.push_str(&mark_for(tokens.len() + 1));
                tokens.push(token);
            }
            // Interior didn't map — keep the authored bytes.
            None => push_escaped(&mut masked, &source[start..end], &mut tokens),
        }
        cursor = end;
    }
    push_escaped(&mut masked, &source[cursor..], &mut tokens);

    // --- Inline-ONLY parse (the `.inlineOnlyPreservingWhitespace` contract)
    //
    // The content handed to us has already had its BLOCK chrome stripped,
    // so re-parsing it with a block parser would let pulldown reinterpret
    // the newly exposed bytes as structure and consume them: `# ---`
    // strips to `---`, which a block parse reads as a thematic break and
    // renders as NOTHING, silently erasing the heading. `# > quote` and
    // `# # nested` lose their leading marker the same way.
    //
    // The probe forces a single-paragraph, inline-only parse by
    // construction:
    //   1. a scaffold mark at offset 0, so no line can begin with a block
    //      trigger (`#`, `>`, `-`, a fence, four spaces, `<`, …);
    //   2. every line ending replaced by a SPACE, so no blank line can
    //      split a paragraph.
    // The result always parses as exactly one paragraph containing only
    // inline events. Authored line terminators are recovered by slicing
    // `masked` (see `InlineWalker::text_for`), never normalized.
    //
    // CommonMark maps ONE line ending to one space (inside a code span it
    // says so explicitly), so a CRLF collapses to a single space and the
    // probe is shorter than `masked` by one byte per CRLF. `collapses`
    // records where, so probe offsets map back exactly.
    //
    // The scaffold is itself a token mark whose token renders as nothing:
    // uniform with every other mark, so the expansion scanner needs no
    // special case, and it disappears from the output for free.
    let scaffold = mark_for(0);
    let mut all_tokens = Vec::with_capacity(tokens.len() + 1);
    all_tokens.push(TokenRun {
        display: String::new(),
        kind: ReadingInlineRunKind::Text,
        ax_text: None,
        source: String::new(),
        is_escape: true,
    });
    all_tokens.extend(tokens);

    let mut probe = String::with_capacity(masked.len() + scaffold.len());
    let mut collapses: Vec<usize> = Vec::new();
    probe.push_str(&scaffold);
    {
        let bytes = masked.as_bytes();
        let mut i = 0usize;
        while i < bytes.len() {
            match bytes[i] {
                b'\r' if i + 1 < bytes.len() && bytes[i + 1] == b'\n' => {
                    collapses.push(probe.len() - scaffold.len());
                    probe.push(' ');
                    i += 2;
                }
                b'\r' | b'\n' => {
                    probe.push(' ');
                    i += 1;
                }
                _ => {
                    // Copy the whole scalar so multibyte text survives.
                    let start = i;
                    i += 1;
                    while i < bytes.len() && (bytes[i] & 0xC0) == 0x80 {
                        i += 1;
                    }
                    probe.push_str(&masked[start..i]);
                }
            }
        }
    }
    let prefix_len = scaffold.len();

    let mut walker = InlineWalker::new(&probe, &masked, prefix_len, &collapses, &all_tokens, sets);
    walker.run();
    (walker.out.content, walker.out.runs)
}

/// A scalar that cannot be Markdown syntax and is absent from `text`, so
/// it can stand in for a selected token without colliding with authored
/// content.
///
/// Every candidate is Unicode general category **So or Po** — the same
/// class as the `[` / `)` that bounded the retired `[label](url)` splice
/// — so a masked token keeps the emphasis-flanking behavior the splice
/// had. A Private Use scalar would be neither punctuation nor whitespace
/// and could therefore let a delimiter beside a token open emphasis that
/// the retired pipeline left inert, so the escalation deliberately stays
/// inside the punctuation/symbol classes.
/// The token marker: a run of U+FFFC OBJECT REPLACEMENT CHARACTER long
/// enough that the authored text cannot contain it.
///
/// **Un-exhaustible by construction.** One scalar longer than the longest
/// authored run of U+FFFC is guaranteed absent, so there is no collision
/// case to degrade into — an earlier design drew from a finite pool and
/// had to decide what to do when a document used every candidate, which
/// meant a 113-scalar note fell back to rendering raw Markdown.
///
/// **Flanking-safe.** Every scalar in the marker is U+FFFC, Unicode
/// category So — the same punctuation/symbol class that bounded the
/// retired `[label](url)` splice, so a delimiter beside a masked token
/// flanks exactly as it did before. A letter-class placeholder (U+2E2F
/// VERTICAL TILDE is `Lm` despite sitting in Supplemental Punctuation) or
/// a Private Use scalar would let `a*[[x]]*` open emphasis the retired
/// pipeline left inert.
/// Locate the first well-formed `<marker><index><marker>` mask in `text`,
/// returning `(start, end_exclusive, index)`.
///
/// The scan is **digit-anchored**, not a plain `find(marker)`. Authored
/// U+FFFC runs sit adjacent to masks in real notes (a pasted rich-text
/// object placeholder next to a wikilink), and they merge with the mask's
/// opening marker into one longer run. Locking onto the first marker-length
/// window of that run picks a boundary one scalar early, the digits then
/// fail to parse, and the whole mask leaks into the rendered content while
/// the token's affordance is lost. Requiring a digit immediately after the
/// window — and stepping one scalar at a time until one is found — picks
/// the true opener and leaves the authored scalars as text.
///
/// The marker is longer than any authored run of its scalar by
/// escaping ([`push_escaped`]), so authored text can never supply a
/// complete marker window itself.
fn find_mask(text: &str, token_count: usize) -> Option<(usize, usize, usize)> {
    let mut search = 0usize;
    while search < text.len() {
        let open = search + text[search..].find(TOKEN_MARKER)?;
        let after = open + TOKEN_MARKER.len();
        let digits = text[after..].bytes().take_while(u8::is_ascii_digit).count();
        if digits > 0 {
            let digits_end = after + digits;
            if text[digits_end..].starts_with(TOKEN_MARKER)
                && let Ok(index) = text[after..digits_end].parse::<usize>()
                && index < token_count
            {
                return Some((open, digits_end + TOKEN_MARKER.len(), index));
            }
        }
        // Step ONE scalar, not one marker: the real opener may start
        // inside the run this window landed on.
        search = open + text[open..].chars().next().map_or(1, char::len_utf8);
    }
    None
}

/// The token-mask delimiter: U+FFFC OBJECT REPLACEMENT CHARACTER, one
/// scalar, **fixed width**.
///
/// Collision-freedom comes from ESCAPING, not from growing the delimiter.
/// An earlier design sized the marker to one scalar longer than the
/// longest authored run of U+FFFC, which made it collision-free but also
/// made construction Θ(longest_run × token_count): a ~100 KiB paragraph
/// holding a 32k-scalar run and a thousand tokens amplified into hundreds
/// of megabytes of intermediate string — an out-of-memory crash reachable
/// by opening an imported note, far under any file-size refusal
/// threshold. Every authored occurrence is instead escaped into a literal
/// mask ([`TokenRun::is_escape`]) as the masked text is built, so the
/// only U+FFFC scalars in it are delimiters we wrote, and construction
/// stays linear in source size plus token count.
///
/// The scalar is Unicode category So — the punctuation/symbol class that
/// bounded the retired `[label](url)` splice — so a delimiter beside a
/// masked token flanks exactly as it did before. A letter-class scalar
/// (U+2E2F VERTICAL TILDE is `Lm` despite sitting in Supplemental
/// Punctuation) or a Private Use scalar would not.
const TOKEN_MARKER: &str = "\u{FFFC}";

/// Append `text` to `masked`, escaping every authored [`TOKEN_MARKER`]
/// scalar into a literal mask so it can never be mistaken for a
/// delimiter. Literal masks render as the authored scalar with no
/// affordance and no construct break, so neighbouring text still
/// coalesces into one run.
fn push_escaped(masked: &mut String, text: &str, tokens: &mut Vec<TokenRun>) {
    let mut rest = text;
    while let Some(at) = rest.find(TOKEN_MARKER) {
        masked.push_str(&rest[..at]);
        masked.push_str(&mark_for(tokens.len() + 1));
        tokens.push(TokenRun {
            display: TOKEN_MARKER.to_string(),
            kind: ReadingInlineRunKind::Text,
            ax_text: None,
            source: TOKEN_MARKER.to_string(),
            is_escape: true,
        });
        rest = &rest[at + TOKEN_MARKER.len()..];
    }
    masked.push_str(rest);
}

/// One token's mask: `<marker><index><marker>`. The digits are inert to
/// CommonMark (the probe guarantees they are never at a line start, so
/// they cannot open an ordered list), and the marker scalars bound the
/// run so flanking is unchanged.
fn mark_for(index: usize) -> String {
    format!("{TOKEN_MARKER}{index}{TOKEN_MARKER}")
}

#[derive(Default)]
struct RunBuilder {
    content: String,
    runs: Vec<ReadingInlineRun>,
    /// The construct id of the last pushed run — see [`RunBuilder::push`].
    last_construct: u32,
}

impl RunBuilder {
    /// Append `text` carrying `styles`/`kind`/`ax_text`, coalescing with
    /// the previous run when every attribute matches AND both belong to
    /// the same authored construct.
    ///
    /// Coalescing is what turns pulldown's arbitrary `Text`-event
    /// chunking (escapes and entities each arrive as their own event)
    /// into the maximal attribute-run shape both hosts consume natively.
    /// The `construct` guard is what stops it from also fusing two
    /// SEPARATE tokens whose payloads happen to be identical: without
    /// it, `[@k][@k]` merged into one run whose text was the citation
    /// rendered twice but whose `raw` named it once — a run a host would
    /// render doubled and activate singly. Caught by
    /// `census_reading_inline_tokens_never_reparse` at 100k documents.
    fn push(
        &mut self,
        text: &str,
        styles: &[ReadingInlineStyle],
        kind: &ReadingInlineRunKind,
        ax_text: &Option<String>,
        construct: u32,
    ) {
        if text.is_empty() {
            return;
        }
        let start = self.content.len();
        self.content.push_str(text);
        let end = self.content.len();
        if let Some(last) = self.runs.last_mut()
            && last.end as usize == start
            && self.last_construct == construct
            && last.styles.as_slice() == styles
            && &last.kind == kind
            && &last.ax_text == ax_text
        {
            last.end = end as u32;
            return;
        }
        self.runs.push(ReadingInlineRun {
            start: start as u32,
            end: end as u32,
            styles: styles.to_vec(),
            kind: kind.clone(),
            ax_text: ax_text.clone(),
        });
        self.last_construct = construct;
    }
}

/// How an expanded token renders at the point the sentinel was found.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum TokenMode {
    /// Normal: the token's display text with its affordance.
    Affordance,
    /// Inside a construct that binds tighter than a link (a code span,
    /// raw HTML): the token's AUTHORED bytes, no affordance.
    Literal,
}

struct InlineWalker<'a> {
    /// The parsed string: sentinel prefix + `masked` with every CR/LF
    /// replaced by a space (see `render_inlines`). Byte-for-byte the same
    /// length as `masked` after `prefix_len`.
    probe: &'a str,
    /// The token-masked content with its AUTHORED line terminators still
    /// in place — what text is actually sliced from.
    masked: &'a str,
    prefix_len: usize,
    /// Probe offsets (post-prefix) where a CRLF collapsed to one space,
    /// ascending — the only places probe and `masked` offsets diverge.
    collapses: &'a [usize],
    tokens: &'a [TokenRun],
    sets: &'a RecordSets,
    out: RunBuilder,
    styles: Vec<ReadingInlineStyle>,
    /// Link context stack: the kind + AX text every text run inside the
    /// current link (or image label) carries.
    links: Vec<(ReadingInlineRunKind, Option<String>)>,
    /// Monotonic id of the authored construct currently being emitted.
    /// Bumped per expanded token and per Markdown link/image, so
    /// [`RunBuilder::push`] never fuses two separate constructs that
    /// happen to carry identical payloads.
    construct: u32,
    /// End of the last emitted block range, for the gap reconstruction
    /// below.
    block_cursor: Option<usize>,
    /// End of the last bytes an INLINE event consumed — the honest gap
    /// anchor (see [`Self::emit_gap`]).
    inline_cursor: usize,
    block_depth: usize,
    saw_block: bool,
}

impl<'a> InlineWalker<'a> {
    fn new(
        probe: &'a str,
        masked: &'a str,
        prefix_len: usize,
        collapses: &'a [usize],
        tokens: &'a [TokenRun],
        sets: &'a RecordSets,
    ) -> Self {
        Self {
            probe,
            masked,
            prefix_len,
            collapses,
            tokens,
            sets,
            out: RunBuilder::default(),
            styles: Vec::new(),
            links: Vec::new(),
            construct: 0,
            block_cursor: None,
            inline_cursor: 0,
            block_depth: 0,
            saw_block: false,
        }
    }

    /// Map one probe offset back onto `masked`: add one byte per CRLF
    /// that collapsed strictly before it.
    fn to_masked(&self, probe_offset: usize) -> Option<usize> {
        let relative = probe_offset.checked_sub(self.prefix_len)?;
        Some(relative + self.collapses.partition_point(|&at| at < relative))
    }

    /// The authored bytes behind a probe range, or `None` when the range
    /// is out of the mapped region.
    fn authored(&self, range: &std::ops::Range<usize>) -> Option<&'a str> {
        let start = self.to_masked(range.start)?;
        let end = self.to_masked(range.end)?;
        if end > self.masked.len()
            || start > end
            || !self.masked.is_char_boundary(start)
            || !self.masked.is_char_boundary(end)
        {
            return None;
        }
        Some(&self.masked[start..end])
    }

    /// `authored` rendered the way the probe renders it: every line
    /// ending — CRLF, LF or lone CR — becomes exactly one space.
    fn probe_form(authored: &str) -> String {
        let bytes = authored.as_bytes();
        let mut out = String::with_capacity(authored.len());
        let mut i = 0usize;
        while i < bytes.len() {
            match bytes[i] {
                b'\r' if i + 1 < bytes.len() && bytes[i + 1] == b'\n' => {
                    out.push(' ');
                    i += 2;
                }
                b'\r' | b'\n' => {
                    out.push(' ');
                    i += 1;
                }
                _ => {
                    let start = i;
                    i += 1;
                    while i < bytes.len() && (bytes[i] & 0xC0) == 0x80 {
                        i += 1;
                    }
                    out.push_str(&authored[start..i]);
                }
            }
        }
        out
    }

    /// The text a `Text` event contributes.
    ///
    /// The payload comes from the PROBE, so any authored CR/LF inside it
    /// arrives as a space. Restoring the authored bytes is what keeps
    /// line endings un-normalized (decision 9) — but only when the
    /// payload really is the probe slice: pulldown also resolves
    /// backslash escapes and character entities into `Text` payloads that
    /// bear no byte relation to their source.
    ///
    /// The test is therefore **exact, not a length heuristic**: the
    /// payload must equal the authored slice with CR/LF mapped to space,
    /// character for character. Anything else is a resolved construct and
    /// the payload is emitted verbatim.
    ///
    /// The first event of the probe also covers the scaffolding sentinel,
    /// which has no authored counterpart; it is carried through so token
    /// expansion still consumes it (and renders it as nothing).
    fn text_for(&self, payload: &str, range: &std::ops::Range<usize>) -> String {
        let scaffold = self.prefix_len.saturating_sub(range.start);
        let scaffold_text = &self.probe[range.start..range.start + scaffold];
        let Some(start) = self.to_masked(range.start + scaffold) else {
            return payload.to_string();
        };
        let Some(end) = self.to_masked(range.end) else {
            return payload.to_string();
        };
        if end > self.masked.len()
            || start > end
            || !self.masked.is_char_boundary(start)
            || !self.masked.is_char_boundary(end)
        {
            return payload.to_string();
        }
        let authored = &self.masked[start..end];
        if payload.len() != scaffold_text.len() + Self::probe_form(authored).len()
            || !payload.starts_with(scaffold_text)
            || payload[scaffold_text.len()..] != Self::probe_form(authored)
        {
            return payload.to_string();
        }
        let mut out = String::with_capacity(scaffold_text.len() + authored.len());
        out.push_str(scaffold_text);
        out.push_str(authored);
        out
    }

    fn current(&self) -> (ReadingInlineRunKind, Option<String>) {
        self.links
            .last()
            .cloned()
            .unwrap_or((ReadingInlineRunKind::Text, None))
    }

    fn styles_now(&self, extra: Option<ReadingInlineStyle>) -> Vec<ReadingInlineStyle> {
        let mut styles = self.styles.clone();
        if let Some(style) = extra {
            styles.push(style);
        }
        styles.sort_unstable();
        styles.dedup();
        styles
    }

    /// Emit `text`, expanding every sentinel into its token's run.
    /// Emit `text`, expanding every `<marker><index><marker>` mask into
    /// its token.
    ///
    /// Expansion is keyed on the INDEX carried in the mask, never on the
    /// order marks are encountered. pulldown exposes a link's destination
    /// and title as Tag metadata that no event renders, so a mask can
    /// legitimately never be seen — with order-based assignment every
    /// later token would then expand with its predecessor's payload,
    /// silently producing a wrong label and a wrong activation target.
    /// A malformed or out-of-range mask is emitted verbatim rather than
    /// guessed at.
    fn emit_text(&mut self, text: &str, extra_style: Option<ReadingInlineStyle>, mode: TokenMode) {
        let styles = self.styles_now(extra_style);
        let (kind, ax) = self.current();
        let mut rest = text;
        while let Some((open, end, index)) = find_mask(rest, self.tokens.len()) {
            let head = rest[..open].to_string();
            let construct = self.construct;
            self.out.push(&head, &styles, &kind, &ax, construct);
            self.expand_token(index, &styles, &kind, &ax, mode);
            rest = &rest[end..];
        }
        let construct = self.construct;
        let tail = rest.to_string();
        self.out.push(&tail, &styles, &kind, &ax, construct);
    }

    fn expand_token(
        &mut self,
        index: usize,
        styles: &[ReadingInlineStyle],
        kind: &ReadingInlineRunKind,
        ax: &Option<String>,
        mode: TokenMode,
    ) {
        let Some(token) = self.tokens.get(index) else {
            return;
        };
        // An escaped authored scalar is not a construct: it renders as
        // itself in the CURRENT context, with no construct break, so the
        // text either side of it still coalesces into one run.
        if token.is_escape {
            let escaped = token.display.clone();
            let construct = self.construct;
            self.out.push(&escaped, styles, kind, ax, construct);
            return;
        }
        let token_kind = token.kind.clone();
        let token_ax = token.ax_text.clone();
        let display = token.display.clone();
        let source = token.source.clone();
        // Each token is its own construct: two adjacent `[@k][@k]` must
        // stay two runs.
        self.construct += 1;
        let construct = self.construct;
        match mode {
            // A construct that binds tighter than a link owns this range:
            // the token renders as its AUTHORED bytes with no affordance.
            // Reachable because selection runs on the block-parsed content
            // while the walk runs on the flattened probe, so a code span
            // or HTML tag can form across a blank line that separated them
            // at selection time. Code-styled text that silently navigates
            // would be the alternative (the retired pipeline leaked its
            // raw `[label](slate-wiki://…)` splice text there instead —
            // this is the honest rendering).
            TokenMode::Literal => self.out.push(&source, styles, kind, ax, construct),
            // A token inside a link label keeps the LABEL's affordance
            // (the Markdown construct owns the range).
            TokenMode::Affordance if !self.links.is_empty() => {
                self.out.push(&display, styles, kind, ax, construct)
            }
            TokenMode::Affordance => {
                self.out
                    .push(&display, styles, &token_kind, &token_ax, construct)
            }
        }
        self.construct += 1;
    }

    /// Emit the AUTHORED bytes pulldown left outside its paragraph — the
    /// leading indent it consumed as block chrome and the trailing
    /// whitespace it trimmed. `.inlineOnlyPreservingWhitespace` never
    /// dropped them, and the degradation contract forbids losing authored
    /// bytes.
    ///
    /// The gap starts at [`Self::inline_cursor`] — the end of the last
    /// bytes an inline event actually consumed — not at the block's own
    /// range end, because a block's range swallows trailing whitespace
    /// that its `Text` event does not.
    fn emit_gap(&mut self, upto: usize) {
        let from = self.block_cursor.unwrap_or(self.prefix_len);
        if upto > from
            && let Some(gap) = self.authored(&(from..upto))
        {
            let gap = gap.to_string();
            self.emit_text(&gap, None, TokenMode::Affordance);
        }
    }

    fn advance_inline(&mut self, end: usize) {
        self.inline_cursor = self.inline_cursor.max(end);
    }

    fn run(&mut self) {
        use pulldown_cmark::TagEnd as End;

        for (event, range) in Parser::new_ext(self.probe, READING_PARSE_OPTIONS).into_offset_iter()
        {
            match event {
                Event::Start(Tag::Emphasis) => self.styles.push(ReadingInlineStyle::Emphasis),
                Event::End(End::Emphasis) => {
                    self.styles.pop();
                    self.advance_inline(range.end);
                }
                Event::Start(Tag::Strong) => self.styles.push(ReadingInlineStyle::Strong),
                Event::End(End::Strong) => {
                    self.styles.pop();
                    self.advance_inline(range.end);
                }
                Event::Start(Tag::Strikethrough) => {
                    self.styles.push(ReadingInlineStyle::Strikethrough)
                }
                Event::End(End::Strikethrough) => {
                    self.styles.pop();
                    self.advance_inline(range.end);
                }
                Event::Start(Tag::Link { dest_url, .. }) => {
                    let classified = markdown_destination_kind(&dest_url, self.sets);
                    // Each link is its own construct: `[a](x)[a](x)` must
                    // stay two activatable runs, not one fused run.
                    self.construct += 1;
                    self.links.push(classified);
                }
                Event::End(End::Link) => {
                    self.links.pop();
                    self.construct += 1;
                    self.advance_inline(range.end);
                }
                // A Markdown image's alt text renders as prose: reading
                // v1 shows the alt, never a dead affordance. Wikilink
                // embeds (`![[…]]`) are a token kind, not this path.
                Event::Start(Tag::Image { .. }) => {
                    self.construct += 1;
                    self.links.push((ReadingInlineRunKind::Text, None));
                }
                Event::End(End::Image) => {
                    self.links.pop();
                    self.construct += 1;
                    self.advance_inline(range.end);
                }
                Event::Text(text) => {
                    let resolved = self.text_for(&text, &range);
                    self.emit_text(&resolved, None, TokenMode::Affordance);
                    self.advance_inline(range.end);
                }
                Event::Code(code) => {
                    // A code span binds tighter than a link (CommonMark
                    // §6.1), and its authored line terminators are
                    // restored like any other text.
                    let resolved = self.text_for(&code, &range);
                    self.emit_text(
                        &resolved,
                        Some(ReadingInlineStyle::InlineCode),
                        TokenMode::Literal,
                    );
                    self.advance_inline(range.end);
                }
                Event::InlineHtml(html) | Event::Html(html) => {
                    // Raw HTML is never interpreted; CommonMark permits
                    // line endings inside a tag, so the authored bytes
                    // come back verbatim rather than as probe spaces.
                    let resolved = self.text_for(&html, &range);
                    self.emit_text(&resolved, None, TokenMode::Literal);
                    self.advance_inline(range.end);
                }
                // Unreachable: the probe holds no line terminators, so
                // pulldown never emits a break. Kept so a future probe
                // change degrades to the authored bytes rather than
                // silently dropping them.
                Event::SoftBreak | Event::HardBreak => {
                    let authored = self.authored(&range).unwrap_or("\n").to_string();
                    self.emit_text(&authored, None, TokenMode::Affordance);
                    self.advance_inline(range.end);
                }
                Event::Start(_) => {
                    if self.block_depth == 0 {
                        self.emit_gap(range.start);
                        self.saw_block = true;
                        self.inline_cursor = range.start;
                    }
                    self.block_depth += 1;
                }
                Event::End(_) => {
                    self.block_depth = self.block_depth.saturating_sub(1);
                    if self.block_depth == 0 {
                        // Anchor on what the inline events consumed, not
                        // on the block range (which swallows the block's
                        // terminating newline).
                        self.block_cursor = Some(self.inline_cursor.min(range.end));
                    }
                }
                Event::Rule if self.block_depth == 0 => {
                    self.emit_gap(range.start);
                    self.saw_block = true;
                    self.block_cursor = Some(range.end);
                }
                _ => {}
            }
        }

        if self.saw_block {
            // Trailing whitespace pulldown trimmed off the paragraph.
            self.emit_gap(self.probe.len());
        } else {
            // Block-less probe (empty content): carry the authored bytes
            // through verbatim.
            let all = self.masked.to_string();
            self.emit_text(&all, None, TokenMode::Affordance);
        }
    }
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
                language: "rust".to_string(),
                interior: "fn main() {}".to_string(),
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
                language: String::new(),
                interior: "plain".to_string(),
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
                // Indented code: pulldown dedents four spaces, so the
                // authoritative interior carries the un-indented lines.
                ReadingBlockKind::CodeFence {
                    language: String::new(),
                    interior: "indented code\nline two".to_string(),
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
                dialect: "mermaid".to_string(),
                interior: "flowchart LR\nA --> B".to_string(),
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
                dialect: "mermaid".to_string(),
                interior: "flowchart LR\nA --> B".to_string(),
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
                language: String::new(),
                interior: "---\nnot a break\n---".to_string(),
            }]
        );
        assert_slices_match(src);
    }

    // --- authoritative interior: the pathological cases the Swift
    //     `fenceInterior` heuristic got wrong (Codex review, #869). Each
    //     asserts pulldown's exact code content is carried through, AND that
    //     the raw `source` slice still equals `full_source[start..end]`. ---

    /// The only interior of `src` (exactly one CodeFence/Diagram expected).
    fn only_interior(src: &str) -> String {
        let blocks = reading_blocks_source(src);
        let interiors: Vec<String> = blocks
            .iter()
            .filter_map(|b| match &b.kind {
                ReadingBlockKind::CodeFence { interior, .. }
                | ReadingBlockKind::Diagram { interior, .. } => Some(interior.clone()),
                _ => None,
            })
            .collect();
        assert_eq!(interiors.len(), 1, "expected exactly one code block");
        interiors.into_iter().next().unwrap()
    }

    #[test]
    fn interior_normal_fence_is_the_code_content() {
        // The well-formed case: interior is the body, delimiters excluded,
        // no trailing newline (the historical fence-strip output).
        assert_eq!(
            only_interior("```rust\nfn main() {}\n```\n"),
            "fn main() {}"
        );
    }

    #[test]
    fn interior_unterminated_fence_tab_closer_includes_backtick_line() {
        // A fence whose only closing candidate is a ```-line ending in a TAB:
        // pulldown does NOT treat it as a closer, so the fence is unterminated
        // and that ``` line is CONTENT. The old Swift heuristic stripped it as
        // a closer, silently losing the authored line. The authoritative
        // interior must INCLUDE it.
        let src = "```\ncode line\n```\t\n";
        assert_eq!(only_interior(src), "code line\n```\t");
        // The raw `source` slice invariant is untouched by the new field.
        assert_slices_match(src);
    }

    #[test]
    fn interior_indented_code_first_line_backticks_is_content() {
        // A ≥4-space-indented block whose FIRST line is triple-backticks: to
        // pulldown this is an INDENTED code block (not a fence), so the ```
        // line is literal content and every line is dedented. The old Swift
        // heuristic mis-read the first line as a fence opener and stripped it.
        let src = "    ```\n    code\n    more\n";
        assert_eq!(only_interior(src), "```\ncode\nmore");
        assert_slices_match(src);
    }

    #[test]
    fn interior_empty_code_block_is_empty() {
        // An empty fence emits no Text events → empty interior (no phantom
        // newline).
        assert_eq!(only_interior("```\n```\n"), "");
        assert_eq!(only_interior("```rust\n```\n"), "");
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
                    language: "python".to_string(),
                    interior: "print('hi')".to_string(),
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
                    language: String::new(),
                    interior: "a".to_string(),
                },
                ReadingBlockKind::CodeFence {
                    language: String::new(),
                    interior: "b".to_string(),
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

    /// Codoki review (PR #566): flagged that `width = header.len().max(alignments_len)`
    /// only resized ROWS, not `header` itself, so a header shorter than the
    /// delimiter-declared width could in principle leave trailing body cells
    /// with no header to pair with (Swift derives its columns from
    /// `header.len()`). `header.resize(width, ...)` was added to close that
    /// gap by construction rather than by trusting pulldown-cmark's own
    /// table detection.
    ///
    /// Empirically (see scratch probes run against this exact pulldown-cmark
    /// version), `header.len() < alignments_len` does not appear reachable
    /// through the public `&str` entry point: `Start(Tag::Table(aligns))`
    /// only fires once the header row's cell count already matches the
    /// delimiter row's, and pulldown tolerates truncated/malformed sources
    /// (missing closing pipes, no trailing newline) by still completing a
    /// well-formed `TableHead`. A header/delimiter cell-count mismatch (e.g.
    /// `"| a |\n|---|---|\n"`, 1 header cell vs. 2 delimiter cells) is
    /// rejected as "not a table" before `reading_table_cells` ever sees a
    /// `Table` event, so `reading_table_cells` returns `None` for it — it is
    /// NOT the `Some` with a padded header the literal issue description
    /// suggested. This test pins the invariant the fix actually guarantees —
    /// header width always equals row width for whatever IS recognized as a
    /// table — rather than asserting an input shape that cannot occur.
    #[test]
    fn table_cells_header_width_always_matches_row_width() {
        let src = "| a | b |\n|---|---|\n| 1 | 2 |\n";
        let cells = reading_table_cells(src).expect("a table");
        for row in &cells.rows {
            assert_eq!(
                row.len(),
                cells.header.len(),
                "row width must equal header width by construction"
            );
        }

        // A header/delimiter cell-count mismatch is not a table at all to
        // pulldown-cmark — confirms the "short header" shape this fix
        // defends against cannot reach `reading_table_cells` as `Some`.
        assert!(
            reading_table_cells("| a |\n|---|---|\n| 1 | 2 |\n").is_none(),
            "header/delimiter cell-count mismatch is rejected upstream, not padded"
        );
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

    // === Inline segments (#967) =======================================
    //
    // Organized by the six behavior families the inline-runs spec §8
    // pins: alias/anchor/trim probes, the markdown-destination `^`
    // grammar, resolved/unresolved pairs, tags, citations (matched +
    // unmatched), emphasis spanning a token, tokens inside code, and
    // mid-paragraph vs block embeds — plus chrome stripping and the
    // CRLF/mixed-ending twins.

    use crate::links::LinkAnchor;
    use crate::links_db::OutgoingLink;

    fn citation(raw: &str, visual: &str, speech: &str) -> crate::RenderedCitation {
        crate::RenderedCitation {
            raw: raw.to_string(),
            visual_text: visual.to_string(),
            speech_text: speech.to_string(),
            bib_entry: None,
            style_id: "test".to_string(),
        }
    }

    fn record(target_raw: &str, kind: &str, is_unresolved: bool) -> OutgoingLink {
        OutgoingLink {
            target_path: if is_unresolved {
                None
            } else {
                Some(format!("{target_raw}.md"))
            },
            target_raw: target_raw.to_string(),
            target_anchor: None,
            kind: kind.to_string(),
            is_embed: false,
            is_external: false,
            is_unresolved,
            snippet: String::new(),
            ordinal: 0,
            span_start: 0,
            span_end: 0,
            display_text: None,
        }
    }

    fn embed_record(target_raw: &str) -> OutgoingLink {
        OutgoingLink {
            is_embed: true,
            ..record(target_raw, "wikilink", false)
        }
    }

    /// The single segment of a single-block source.
    fn only_segment(source: &str) -> ReadingInlineSegment {
        only_segment_with(source, &[], &[])
    }

    fn only_segment_with(
        source: &str,
        citations: &[crate::RenderedCitation],
        records: &[OutgoingLink],
    ) -> ReadingInlineSegment {
        let inlines = reading_inline_segments_source(source, citations, records);
        assert_eq!(inlines.len(), 1, "fixture must be exactly one block");
        assert_eq!(inlines[0].segments.len(), 1, "block must carry one segment");
        inlines[0].segments[0].clone()
    }

    /// `(text, kind)` per run, the shape most assertions care about.
    fn run_view(segment: &ReadingInlineSegment) -> Vec<(String, ReadingInlineRunKind)> {
        segment
            .runs
            .iter()
            .map(|r| {
                (
                    segment.content[r.start as usize..r.end as usize].to_string(),
                    r.kind.clone(),
                )
            })
            .collect()
    }

    /// The partition invariant: runs tile `content` exactly, in order,
    /// with no gaps and no overlaps.
    fn assert_partitions(segment: &ReadingInlineSegment) {
        let mut cursor = 0u32;
        for run in &segment.runs {
            assert_eq!(run.start, cursor, "gap or overlap before {run:?}");
            assert!(run.end > run.start, "empty run {run:?}");
            assert!(
                segment.content.is_char_boundary(run.start as usize)
                    && segment.content.is_char_boundary(run.end as usize),
                "run {run:?} splits a scalar"
            );
            cursor = run.end;
        }
        assert_eq!(
            cursor as usize,
            segment.content.len(),
            "runs must cover every byte of {:?}",
            segment.content
        );
    }

    fn wiki(
        target: &str,
        base: &str,
        anchor: Option<LinkAnchor>,
        resolved: bool,
    ) -> ReadingInlineRunKind {
        ReadingInlineRunKind::Wikilink {
            target: target.to_string(),
            base_target: base.to_string(),
            anchor,
            grammar: ReadingWikiGrammar::Wikilink,
            resolved,
        }
    }

    // --- alias / anchor / trim probes ---

    #[test]
    fn inline_wikilink_alias_is_the_run_text() {
        let segment = only_segment("See [[Note|Alias]] here\n");
        assert_eq!(
            run_view(&segment),
            vec![
                ("See ".to_string(), ReadingInlineRunKind::Text),
                ("Alias".to_string(), wiki("Note", "Note", None, false)),
                (" here".to_string(), ReadingInlineRunKind::Text),
            ]
        );
        assert_partitions(&segment);
    }

    #[test]
    fn inline_wikilink_alias_splits_on_the_first_pipe_only() {
        let segment = only_segment("[[a|b|c]]\n");
        assert_eq!(
            run_view(&segment),
            vec![("b|c".to_string(), wiki("a", "a", None, false))]
        );
    }

    #[test]
    fn inline_wikilink_padding_is_trimmed_to_the_resolver_key() {
        // Red-team probe (#849): the Swift side kept padding the Rust
        // resolver strips, so styling and activation disagreed. The
        // canonical target must be the trimmed form.
        let segment = only_segment_with(
            "[[ Missing ]]\n",
            &[],
            &[record("Missing", "wikilink", true)],
        );
        assert_eq!(
            run_view(&segment),
            vec![(
                "Missing".to_string(),
                wiki("Missing", "Missing", None, false)
            )]
        );
        assert_eq!(
            segment.runs[0].ax_text.as_deref(),
            Some("Unresolved link"),
            "an unresolved record must reach the run's AX text"
        );
    }

    #[test]
    fn inline_wikilink_heading_anchor_beats_block_marker() {
        // `note^draft#sec` cuts at the FIRST `#`, never the `^` — the
        // links.rs precedence a first-marker cut got wrong.
        let segment = only_segment("[[note^draft#sec]]\n");
        assert_eq!(
            run_view(&segment),
            vec![(
                "note^draft#sec".to_string(),
                wiki(
                    "note^draft#sec",
                    "note^draft",
                    Some(LinkAnchor::Heading("sec".to_string())),
                    false
                )
            )]
        );
    }

    #[test]
    fn inline_wikilink_legacy_block_ref_cuts_at_caret() {
        let segment = only_segment("[[note^blk]]\n");
        assert_eq!(
            run_view(&segment),
            vec![(
                "note^blk".to_string(),
                wiki(
                    "note^blk",
                    "note",
                    Some(LinkAnchor::Block("blk".to_string())),
                    false
                )
            )]
        );
    }

    // --- markdown-destination grammar ---

    #[test]
    fn markdown_destination_keeps_caret_in_the_path() {
        // Codex round 2/3: `^` is an anchor marker in wikilink grammar
        // but a legal path character in a markdown destination, and a
        // cross-grammar record must never vouch.
        let segment = only_segment_with(
            "[m](note^block)\n",
            &[],
            &[record("note", "wikilink", false)],
        );
        assert_eq!(
            run_view(&segment),
            vec![(
                "m".to_string(),
                ReadingInlineRunKind::Wikilink {
                    target: "note^block".to_string(),
                    base_target: "note^block".to_string(),
                    anchor: None,
                    grammar: ReadingWikiGrammar::MarkdownDestination,
                    resolved: false,
                }
            )]
        );
    }

    #[test]
    fn markdown_destination_resolves_against_a_markdown_record() {
        let segment = only_segment_with(
            "[m](note.md#Sec)\n",
            &[],
            &[record("note.md", "markdown", false)],
        );
        match &segment.runs[0].kind {
            ReadingInlineRunKind::Wikilink {
                base_target,
                anchor,
                grammar,
                resolved,
                ..
            } => {
                assert_eq!(base_target, "note.md");
                assert_eq!(*anchor, Some(LinkAnchor::Heading("Sec".to_string())));
                assert_eq!(*grammar, ReadingWikiGrammar::MarkdownDestination);
                assert!(resolved);
            }
            other => panic!("expected a markdown-grammar wikilink, got {other:?}"),
        }
        assert_eq!(segment.runs[0].ax_text, None);
    }

    #[test]
    fn allowlisted_external_schemes_stay_activatable() {
        for (source, url) in [
            ("[x](https://example.com)\n", "https://example.com"),
            ("[x](http://example.com)\n", "http://example.com"),
            ("[x](mailto:a@b.c)\n", "mailto:a@b.c"),
        ] {
            let segment = only_segment(source);
            assert_eq!(
                run_view(&segment),
                vec![(
                    "x".to_string(),
                    ReadingInlineRunKind::ExternalLink {
                        url: url.to_string()
                    }
                )],
                "{source}"
            );
        }
    }

    #[test]
    fn never_activatable_destinations_lose_the_affordance() {
        // file:/javascript:/unknown schemes, protocol-relative `//host`
        // and fragment-only `#anchor` render as plain text — no dead
        // affordance, visually or to AT.
        for source in [
            "[x](file:///etc/passwd)\n",
            "[x](javascript:alert(1))\n",
            "[x](weirdscheme:thing)\n",
            "[x](//host/path)\n",
            "[x](#intro)\n",
        ] {
            let segment = only_segment(source);
            assert_eq!(
                run_view(&segment),
                vec![("x".to_string(), ReadingInlineRunKind::Text)],
                "{source}"
            );
        }
    }

    // --- resolved / unresolved pairs ---

    #[test]
    fn same_grammar_record_decides_resolution() {
        let source = "[[Known]] and [[Dangling]]\n";
        let records = vec![
            record("Known", "wikilink", false),
            record("Dangling", "wikilink", true),
        ];
        let segment = only_segment_with(source, &[], &records);
        let resolutions: Vec<bool> = segment
            .runs
            .iter()
            .filter_map(|r| match &r.kind {
                ReadingInlineRunKind::Wikilink { resolved, .. } => Some(*resolved),
                _ => None,
            })
            .collect();
        assert_eq!(resolutions, vec![true, false]);
    }

    #[test]
    fn cross_grammar_record_never_vouches() {
        // A markdown record must not resolve a wikilink run.
        let segment = only_segment_with("[[note]]\n", &[], &[record("note", "markdown", false)]);
        assert_eq!(
            run_view(&segment),
            vec![("note".to_string(), wiki("note", "note", None, false))]
        );
    }

    #[test]
    fn empty_records_classify_every_link_unresolved() {
        // The host's stale-ownership window: no records for this note is
        // the honest "unresolved", exactly what activation announces.
        let segment = only_segment("[[Anything]]\n");
        assert_eq!(segment.runs[0].ax_text.as_deref(), Some("Unresolved link"));
    }

    #[test]
    fn embed_records_do_not_resolve_link_runs() {
        let segment = only_segment_with("[[Note]]\n", &[], &[embed_record("Note")]);
        assert_eq!(
            run_view(&segment),
            vec![("Note".to_string(), wiki("Note", "Note", None, false))]
        );
    }

    // --- tags ---

    #[test]
    fn tag_run_keeps_the_hash_and_name_drops_it() {
        let segment = only_segment("a #project/alpha b\n");
        assert_eq!(
            run_view(&segment),
            vec![
                ("a ".to_string(), ReadingInlineRunKind::Text),
                (
                    "#project/alpha".to_string(),
                    ReadingInlineRunKind::Tag {
                        name: "project/alpha".to_string()
                    }
                ),
                (" b".to_string(), ReadingInlineRunKind::Text),
            ]
        );
    }

    // --- citations ---

    #[test]
    fn matched_citation_renders_visual_text_and_speaks_speech_text() {
        let citations = vec![citation("[@key]", "(Doe 2020)", "Doe, twenty twenty")];
        let segment = only_segment_with("See [@key] now\n", &citations, &[]);
        assert_eq!(
            run_view(&segment),
            vec![
                ("See ".to_string(), ReadingInlineRunKind::Text),
                (
                    "(Doe 2020)".to_string(),
                    ReadingInlineRunKind::Citation {
                        raw: "[@key]".to_string(),
                        speech: "Doe, twenty twenty".to_string()
                    }
                ),
                (" now".to_string(), ReadingInlineRunKind::Text),
            ]
        );
        assert_eq!(
            segment.runs[1].ax_text.as_deref(),
            Some("Doe, twenty twenty")
        );
        assert_partitions(&segment);
    }

    #[test]
    fn unmatched_citation_degrades_to_its_raw_text() {
        let segment = only_segment("See [@missing] now\n");
        assert_eq!(
            run_view(&segment)[1],
            (
                "[@missing]".to_string(),
                ReadingInlineRunKind::Citation {
                    raw: "[@missing]".to_string(),
                    speech: "[@missing]".to_string()
                }
            )
        );
    }

    // --- emphasis spanning a token / splice equivalence ---

    #[test]
    fn emphasis_around_a_token_yields_styled_runs() {
        let segment = only_segment("**bold [[Target]] tail**\n");
        assert_eq!(
            segment.content, "bold Target tail",
            "markdown syntax is consumed, token display substituted"
        );
        for run in &segment.runs {
            assert_eq!(run.styles, vec![ReadingInlineStyle::Strong]);
        }
        assert_partitions(&segment);
    }

    #[test]
    fn delimiters_inside_a_token_never_pair_outside_it() {
        // Splice-equivalence rule (§6): the `*` inside `[[a*b]]` must
        // not open emphasis with the `*` after it, and the token's own
        // text never re-parses.
        let segment = only_segment("[[a*b]] * tail\n");
        assert_eq!(segment.content, "a*b * tail");
        assert!(
            segment.runs.iter().all(|r| r.styles.is_empty()),
            "no emphasis may be produced: {:?}",
            segment.runs
        );
    }

    #[test]
    fn token_display_text_is_never_reparsed_as_markdown() {
        let segment = only_segment("[[note|**not bold**]]\n");
        assert_eq!(segment.content, "**not bold**");
        assert_eq!(segment.runs.len(), 1);
        assert!(segment.runs[0].styles.is_empty());
    }

    #[test]
    fn styled_link_label_emits_adjacent_runs_with_one_kind() {
        // Flatness rule (§1): hosts stamp attributes per run and
        // attribute-equality merges the affordance — no host grouping.
        let segment = only_segment("[**b** c](https://x)\n");
        let kinds: Vec<ReadingInlineRunKind> =
            segment.runs.iter().map(|r| r.kind.clone()).collect();
        assert!(kinds.iter().all(|k| matches!(
            k,
            ReadingInlineRunKind::ExternalLink { url } if url == "https://x"
        )));
        assert_eq!(
            segment
                .runs
                .iter()
                .map(|r| r.styles.clone())
                .collect::<Vec<_>>(),
            vec![vec![ReadingInlineStyle::Strong], vec![]]
        );
        assert_partitions(&segment);
    }

    #[test]
    fn adjacent_identical_tokens_stay_separate_runs() {
        // Coalescing must not fuse two SEPARATE authored constructs whose
        // payloads happen to match: the fused run would render the text
        // twice but activate once. Found by the 100k-document census.
        let citations = vec![citation("[@k]", "(D 20)", "D twenty")];
        let segment = only_segment_with("[@k][@k]\n", &citations, &[]);
        assert_eq!(segment.content, "(D 20)(D 20)");
        assert_eq!(segment.runs.len(), 2);
        for run in &segment.runs {
            assert_eq!(
                &segment.content[run.start as usize..run.end as usize],
                "(D 20)"
            );
        }

        let segment = only_segment("[[a]][[a]]\n");
        assert_eq!(segment.runs.len(), 2, "{:?}", segment.runs);

        let segment = only_segment("#a #a\n");
        assert_eq!(
            segment.runs.len(),
            3,
            "two tags separated by a space: {:?}",
            segment.runs
        );
    }

    #[test]
    fn adjacent_identical_links_stay_separate_runs() {
        let segment = only_segment("[a](https://x)[a](https://x)\n");
        assert_eq!(segment.content, "aa");
        assert_eq!(segment.runs.len(), 2, "{:?}", segment.runs);
    }

    #[test]
    fn nested_styles_are_sorted_and_deduped() {
        let segment = only_segment("***both***\n");
        assert_eq!(
            segment.runs[0].styles,
            vec![ReadingInlineStyle::Emphasis, ReadingInlineStyle::Strong]
        );
    }

    // --- tokens inside code stay literal ---

    #[test]
    fn tokens_inside_inline_code_stay_literal() {
        let segment = only_segment("run `[[Note]]` and `#tag`\n");
        assert_eq!(segment.content, "run [[Note]] and #tag");
        assert!(
            segment
                .runs
                .iter()
                .all(|r| r.kind == ReadingInlineRunKind::Text),
            "code content must carry no affordance: {:?}",
            segment.runs
        );
        assert_partitions(&segment);
    }

    #[test]
    fn tokens_inside_a_markdown_link_belong_to_the_link() {
        // `[t](#intro)`'s destination also classifies as a tag; the
        // markdown construct owns that range.
        let segment = only_segment("[t](#intro)\n");
        assert_eq!(
            run_view(&segment),
            vec![("t".to_string(), ReadingInlineRunKind::Text)]
        );
    }

    // --- embeds: mid-paragraph vs block-level ---

    #[test]
    fn mid_paragraph_embed_is_an_inline_run() {
        let inlines = reading_inline_segments_source("see ![[Img.png]] here\n", &[], &[]);
        assert_eq!(inlines[0].block_embed_key, None);
        assert_eq!(
            run_view(&inlines[0].segments[0])[1],
            (
                "Img.png".to_string(),
                ReadingInlineRunKind::Embed {
                    key: "Img.png".to_string()
                }
            )
        );
    }

    #[test]
    fn block_level_embed_reports_its_cache_key() {
        let inlines = reading_inline_segments_source("  ![[Note#Section]]  \n", &[], &[]);
        assert_eq!(inlines[0].block_embed_key.as_deref(), Some("Note#Section"));
    }

    #[test]
    fn block_embed_key_uses_the_canonical_anchor_composition() {
        // Behavior fix over the retired Swift mapper (deltas ledger):
        // the authored `#^` block-ref form and padded interiors used to
        // compose a key the resolution dictionary could not match.
        assert_eq!(
            reading_inline_segments_source("![[Note#^blk]]\n", &[], &[])[0].block_embed_key,
            Some("Note^blk".to_string())
        );
        assert_eq!(
            reading_inline_segments_source("![[ Note # Sec ]]\n", &[], &[])[0].block_embed_key,
            Some("Note#Sec".to_string())
        );
    }

    #[test]
    fn embed_display_falls_back_to_the_target_name() {
        let inlines = reading_inline_segments_source("x ![[folder/sub/Img.png]] y\n", &[], &[]);
        assert_eq!(
            run_view(&inlines[0].segments[0])[1].0,
            "Img.png",
            "alt text is the last path component when no alias is authored"
        );
    }

    #[test]
    fn markdown_image_never_block_expands() {
        // Scope pinned (#511): only wikilink embeds expand in place.
        let inlines = reading_inline_segments_source("![alt](x.png)\n", &[], &[]);
        assert_eq!(inlines[0].block_embed_key, None);
        assert_eq!(
            run_view(&inlines[0].segments[0]),
            vec![("alt".to_string(), ReadingInlineRunKind::Text)]
        );
    }

    #[test]
    fn embed_with_surrounding_text_is_not_a_block_embed() {
        let inlines = reading_inline_segments_source("x ![[Note]]\n", &[], &[]);
        assert_eq!(inlines[0].block_embed_key, None);
    }

    #[test]
    fn embed_inside_inline_code_is_not_a_block_embed() {
        let inlines = reading_inline_segments_source("`![[Note]]`\n", &[], &[]);
        assert_eq!(inlines[0].block_embed_key, None);
    }

    // --- chrome stripping (§2) ---

    #[test]
    fn heading_chrome_is_stripped_both_forms() {
        assert_eq!(only_segment("## Section ##\n").content, "Section");
        assert_eq!(only_segment("Title\n=====\n").content, "Title");
        assert_eq!(only_segment("Section\n-------\n").content, "Section");
    }

    #[test]
    fn list_marker_and_checkbox_are_stripped_and_reported() {
        let inlines = reading_inline_segments_source("3. ordered item\n", &[], &[]);
        assert_eq!(inlines[0].list_marker.as_deref(), Some("3."));
        assert_eq!(inlines[0].segments[0].content, "ordered item");

        let inlines = reading_inline_segments_source("- [x] done\n", &[], &[]);
        assert_eq!(inlines[0].list_marker.as_deref(), Some("-"));
        assert_eq!(inlines[0].segments[0].content, "done");
        assert_eq!(inlines[0].segments[0].task_completed, Some(true));

        let inlines = reading_inline_segments_source("- [/] doing\n", &[], &[]);
        assert_eq!(inlines[0].segments[0].task_completed, Some(false));
    }

    #[test]
    fn non_task_list_item_keeps_its_bracket_text() {
        // `1. [v] Visible` — ordered items are never tasks, so the box
        // must not be stripped (#514).
        let inlines = reading_inline_segments_source("1. [v] Visible\n", &[], &[]);
        assert_eq!(inlines[0].segments[0].content, "[v] Visible");
        assert_eq!(inlines[0].segments[0].task_completed, None);
    }

    #[test]
    fn quote_chrome_is_stripped_at_the_block_depth() {
        assert_eq!(only_segment("> quoted\n").content, "quoted");
        let inlines = reading_inline_segments_source("> > deep\n", &[], &[]);
        assert_eq!(inlines[0].segments[0].content, "deep");
    }

    #[test]
    fn blocks_without_inline_content_carry_no_segments() {
        let source = "```rust\nfn x() {}\n```\n\n$$\nx^2\n$$\n\n| a |\n|---|\n| 1 |\n\n---\n\n<div>x</div>\n";
        let blocks = reading_blocks_source(source);
        let inlines = reading_inline_segments_source(source, &[], &[]);
        assert_eq!(blocks.len(), inlines.len());
        for (block, inline) in blocks.iter().zip(inlines.iter()) {
            assert!(
                inline.segments.is_empty(),
                "{:?} must carry no inline segments",
                block.kind
            );
        }
    }

    // --- line endings are never normalized (decision 9) ---

    #[test]
    fn crlf_paragraph_preserves_its_line_endings() {
        let segment = only_segment("first line\r\nsecond line\r\n");
        assert_eq!(segment.content, "first line\r\nsecond line");
        assert_partitions(&segment);
    }

    #[test]
    fn mixed_endings_survive_the_inline_walk() {
        let segment = only_segment("a\r\nb\nc\r\n");
        assert_eq!(segment.content, "a\r\nb\nc");
    }

    #[test]
    fn crlf_quote_and_list_chrome_strip_without_touching_endings() {
        let inlines = reading_inline_segments_source("> a\r\n> b\r\n", &[], &[]);
        assert_eq!(inlines[0].segments[0].content, "a\r\nb");
    }

    // --- degradation ---

    #[test]
    fn whitespace_only_content_is_carried_verbatim() {
        // A block whose stripped content holds no CommonMark block at
        // all must not lose its bytes.
        let segment = ReadingInlineSegment {
            content: render_inlines("   ", &[], &RecordSets::default()).0,
            runs: render_inlines("   ", &[], &RecordSets::default()).1,
            task_completed: None,
        };
        assert_eq!(segment.content, "   ");
        assert_partitions(&segment);
    }

    #[test]
    fn loose_list_item_keeps_its_internal_blank_line() {
        let inlines = reading_inline_segments_source("- first para\n\n  second para\n", &[], &[]);
        let content = &inlines[0].segments[0].content;
        assert!(content.contains("first para"), "{content:?}");
        assert!(content.contains("second para"), "{content:?}");
        assert!(
            content.contains("\n\n"),
            "the authored blank line must survive: {content:?}"
        );
    }

    // --- reading_match_link ---

    #[test]
    fn match_link_prefers_the_anchor_cut_key_then_the_verbatim_one() {
        let records = vec![record("Note", "wikilink", false)];
        assert_eq!(
            reading_match_link("Note#Sec", ReadingWikiGrammar::Wikilink, false, &records),
            Some(0)
        );
        let verbatim = vec![record("Note#Sec", "wikilink", false)];
        assert_eq!(
            reading_match_link("Note#Sec", ReadingWikiGrammar::Wikilink, false, &verbatim),
            Some(0)
        );
    }

    #[test]
    fn match_link_partitions_by_embedness_and_grammar() {
        let records = vec![embed_record("Note"), record("Note", "wikilink", false)];
        assert_eq!(
            reading_match_link("Note", ReadingWikiGrammar::Wikilink, true, &records),
            Some(0)
        );
        assert_eq!(
            reading_match_link("Note", ReadingWikiGrammar::Wikilink, false, &records),
            Some(1)
        );
        assert_eq!(
            reading_match_link(
                "Note",
                ReadingWikiGrammar::MarkdownDestination,
                false,
                &records
            ),
            None,
            "a wikilink record must never answer a markdown activation"
        );
    }

    #[test]
    fn match_link_returns_none_without_records() {
        assert_eq!(
            reading_match_link("Note", ReadingWikiGrammar::Wikilink, false, &[]),
            None
        );
    }

    // --- Adversarial-review regressions (round 1) ---

    /// The content handed to the inline walk has already had its BLOCK
    /// chrome stripped, so a block re-parse would reinterpret the newly
    /// exposed bytes as structure and consume them. `# ---` strips to
    /// `---`, which a block parse reads as a thematic break and renders
    /// as NOTHING — silently erasing the heading. The probe (§6) forces a
    /// single-paragraph inline-only parse, so every one of these keeps
    /// its authored bytes.
    #[test]
    fn stripped_content_is_never_reinterpreted_as_a_block() {
        for (source, expected) in [
            ("# ---\n", "---"),
            ("# ***\n", "***"),
            ("# ___\n", "___"),
            ("# > quote\n", "> quote"),
            ("# # nested\n", "# nested"),
            ("# 1. item\n", "1. item"),
            ("# - bullet\n", "- bullet"),
            ("# ```rust\n", "```rust"),
            ("# <div>x</div>\n", "<div>x</div>"),
        ] {
            assert_eq!(
                only_segment(source).content,
                expected,
                "{source:?} must keep its authored bytes"
            );
        }
    }

    /// The same hazard through the other chrome strippers.
    ///
    /// `> # not a heading` is classified `Heading` by the block walk but
    /// its source still carries the `>`, so `heading_text` degrades to
    /// the verbatim slice — which the inline walk must then leave alone
    /// rather than re-reading the `#` as a heading marker.
    #[test]
    fn stripped_quote_content_keeps_block_looking_bytes() {
        assert_eq!(
            only_segment("> # not a heading\n").content,
            "> # not a heading"
        );
        // Same degradation on the list path: the block is a ListItem
        // (innermost container names the leaf) but its source starts with
        // the quote marker, so no list marker is found and the content is
        // the verbatim slice — authored bytes are never dropped.
        let inlines = reading_inline_segments_source("> - bullet\n", &[], &[]);
        assert_eq!(inlines[0].list_marker, None);
        assert_eq!(inlines[0].segments[0].content, "> - bullet");
    }

    /// An EXTERNAL record must never answer an internal activation.
    ///
    /// `resolve_link` classifies by target shape, not authoring grammar,
    /// so `[[https://example.com]]` is stored as a `wikilink`-kind record
    /// with `is_external = true`. Styling already skipped those; before
    /// the fix `reading_match_link` did not, so the run rendered
    /// "Unresolved link" and then handed the URL to the system opener.
    #[test]
    fn external_records_never_answer_an_internal_activation() {
        let external = vec![OutgoingLink {
            is_external: true,
            ..record("https://example.com", "wikilink", false)
        }];
        assert_eq!(
            reading_match_link(
                "https://example.com",
                ReadingWikiGrammar::Wikilink,
                false,
                &external
            ),
            None,
            "activation must agree with the styling classifier"
        );

        // And the two halves agree end to end: the run styles unresolved
        // AND carries the AX text, with no matching activation record.
        let segment = only_segment_with("[[https://example.com]]\n", &[], &external);
        match &segment.runs[0].kind {
            ReadingInlineRunKind::Wikilink { resolved, .. } => assert!(!resolved),
            other => panic!("expected a wikilink run, got {other:?}"),
        }
        assert_eq!(
            segment.runs[0].ax_text.as_deref(),
            Some(UNRESOLVED_LINK_AX_TEXT)
        );
    }

    /// The token mask must flank like the retired `[label](url)` splice
    /// did: every scalar in the marker is U+FFFC (Unicode So), never a
    /// letter-class or Private Use scalar that would let a delimiter
    /// beside a token open emphasis the retired pipeline left inert.
    #[test]
    fn token_marker_preserves_flanking() {
        let source = "\u{FFFC} a*[[x]]*\n";
        let segment = only_segment(source);
        assert!(
            segment.content.starts_with('\u{FFFC}'),
            "the authored scalar survives: {:?}",
            segment.content
        );
        assert!(
            segment.runs.iter().all(|r| r.styles.is_empty()),
            "an asterisk that could not open emphasis before masking must \
             not open it after: {:?}",
            segment.runs
        );
    }

    /// Collision-freedom comes from ESCAPING, so the delimiter stays one
    /// fixed-width scalar however many the author wrote — and every
    /// authored occurrence still survives beside a working affordance.
    ///
    /// An earlier design sized the marker to one scalar longer than the
    /// longest authored run, which was collision-free but made
    /// construction Θ(longest_run × token_count); see
    /// `token_masking_is_linear_in_source_size`.
    #[test]
    fn authored_markers_are_escaped_not_outgrown() {
        for run in 0..6usize {
            let crowded: String = std::iter::repeat_n('\u{FFFC}', run).collect();
            let source = format!("{crowded} [[Note]] {crowded} tail");
            let segment = only_segment(&source);
            if run > 0 {
                assert!(
                    segment.content.contains(&crowded),
                    "authored U+FFFC run survives: {:?}",
                    segment.content
                );
            }
            assert_eq!(
                segment.content.matches('\u{FFFC}').count(),
                run * 2,
                "exactly the authored scalars, no delimiters leaked"
            );
            assert!(
                segment
                    .runs
                    .iter()
                    .any(|r| matches!(r.kind, ReadingInlineRunKind::Wikilink { .. })),
                "the token still resolves to an affordance: {:?}",
                segment.runs
            );
            assert_partitions(&segment);
        }
    }

    /// Resource-amplification guard. The masked/probe strings must stay
    /// linear in source size plus token count: with a variable-width
    /// marker, a ~100 KiB paragraph holding a long U+FFFC run and a
    /// thousand tokens amplified into hundreds of megabytes of
    /// intermediate string — an out-of-memory crash reachable by opening
    /// an imported note, far under any file-size refusal threshold.
    #[test]
    fn token_masking_is_linear_in_source_size() {
        let run: String = std::iter::repeat_n('\u{FFFC}', 8_192).collect();
        let mut source = String::with_capacity(64 * 1024);
        source.push_str(&run);
        for _ in 0..512 {
            source.push_str(" [[a]] ");
        }
        let segment = only_segment(&source);
        // Rendered content is bounded by the authored size, not by
        // run_length × token_count.
        assert!(
            segment.content.len() < source.len() * 2,
            "rendered {} bytes from {} authored — amplification",
            segment.content.len(),
            source.len()
        );
        assert_eq!(
            segment.content.matches('\u{FFFC}').count(),
            8_192,
            "every authored scalar survives, none invented"
        );
        assert_partitions(&segment);
    }

    /// pulldown exposes a link's destination and title as Tag metadata
    /// that NO event renders. A token masked inside what only becomes a
    /// title after blank-line flattening is therefore never emitted — and
    /// with order-based token assignment every later token would expand
    /// with its predecessor's payload, giving a wrong label and a wrong
    /// activation target. Indexed masks make that impossible.
    #[test]
    fn an_unrendered_mask_never_shifts_later_tokens() {
        let inlines = reading_inline_segments_source(
            "- [label](dest \"before\n\n  [[Wrong]]\") [[After]]\n",
            &[],
            &[],
        );
        let targets: Vec<String> = inlines
            .iter()
            .flat_map(|i| &i.segments)
            .flat_map(|s| &s.runs)
            .filter_map(|r| match &r.kind {
                ReadingInlineRunKind::Wikilink { target, .. } => Some(target.clone()),
                _ => None,
            })
            .collect();
        assert!(
            !targets.contains(&"Wrong".to_string())
                || targets.iter().filter(|t| *t == "Wrong").count() == 1,
            "a token must never be expanded under another token's identity: {targets:?}"
        );
        // Every wikilink-GRAMMAR run must carry its OWN target: that run
        // renders the token's display text, so text and target agree
        // unless an alias was authored (none here). A markdown-grammar
        // run renders its label, which is unrelated to the destination,
        // so it is excluded.
        for inline in &inlines {
            for segment in &inline.segments {
                for run in &segment.runs {
                    if let ReadingInlineRunKind::Wikilink {
                        target,
                        grammar: ReadingWikiGrammar::Wikilink,
                        ..
                    } = &run.kind
                    {
                        let text = &segment.content[run.start as usize..run.end as usize];
                        assert_eq!(
                            text, target,
                            "a run must never be expanded under another token's identity"
                        );
                    }
                }
            }
        }
    }

    /// An authored U+FFFC sitting against a token merges with the mask's
    /// opening marker into one longer run. A plain `find(marker)` locks
    /// onto that run one scalar early, the digits fail to parse, and the
    /// whole mask leaks into the rendered content while the token's
    /// affordance is lost — `\u{FFFC}[[a]]` rendered as
    /// `\u{FFFC}\u{FFFC}\u{FFFC}1\u{FFFC}\u{FFFC}` with no link at all.
    /// The digit-anchored scan picks the true opener.
    #[test]
    fn authored_marker_scalars_adjacent_to_a_token_survive() {
        let f = '\u{FFFC}';
        for (source, expected) in [
            (format!("{f}[[a]] tail"), format!("{f}a tail")),
            (format!("[[a]]{f}[[b]] tail"), format!("a{f}b tail")),
            (format!("{f}{f}[[a]]"), format!("{f}{f}a")),
            (format!("[[a]]{f}"), format!("a{f}")),
            (format!("x [[a]] {f} [[b]] y"), format!("x a {f} b y")),
        ] {
            let segment = only_segment(&source);
            assert_eq!(segment.content, expected, "source {source:?}");
            assert!(
                segment
                    .runs
                    .iter()
                    .any(|r| matches!(r.kind, ReadingInlineRunKind::Wikilink { .. })),
                "the token keeps its affordance: {source:?} -> {:?}",
                segment.runs
            );
            assert_partitions(&segment);
        }
    }

    /// An escaped authored scalar must be invisible to the run model: it
    /// renders as itself in whatever context it lands in, and it must NOT
    /// break the surrounding text into extra runs (it is not a construct).
    #[test]
    fn escaped_markers_render_in_context_and_coalesce() {
        let f = '\u{FFFC}';

        // Plain prose: one run, not three.
        let segment = only_segment(&format!("a{f}b\n"));
        assert_eq!(segment.content, format!("a{f}b"));
        assert_eq!(segment.runs.len(), 1, "{:?}", segment.runs);

        let segment = only_segment(&format!("{f}{f}{f}\n"));
        assert_eq!(segment.content, format!("{f}{f}{f}"));
        assert_eq!(segment.runs.len(), 1);

        // Inside a code span: carries the code style, no affordance.
        let segment = only_segment(&format!("code `a{f}b` span\n"));
        assert_eq!(segment.content, format!("code a{f}b span"));
        let code = segment
            .runs
            .iter()
            .find(|r| r.styles.contains(&ReadingInlineStyle::InlineCode))
            .expect("a code run");
        assert_eq!(
            &segment.content[code.start as usize..code.end as usize],
            format!("a{f}b")
        );
        assert_eq!(code.kind, ReadingInlineRunKind::Text);

        // Inside a link label: carries the LINK's affordance.
        let segment = only_segment(&format!("[lab{f}el](note.md) tail\n"));
        assert_eq!(segment.content, format!("lab{f}el tail"));
        assert!(matches!(
            segment.runs[0].kind,
            ReadingInlineRunKind::Wikilink { .. }
        ));

        // Beside a real token, under a style: the escape merges into the
        // styled text, the token keeps its own run.
        let segment = only_segment(&format!("**bold {f} [[a]]** tail\n"));
        assert_eq!(segment.content, format!("bold {f} a tail"));
        assert_eq!(
            &segment.content[segment.runs[0].start as usize..segment.runs[0].end as usize],
            format!("bold {f} ")
        );
        assert!(matches!(
            segment.runs[1].kind,
            ReadingInlineRunKind::Wikilink { .. }
        ));
        assert_partitions(&segment);
    }

    /// Two adjacent masks put two marker runs back to back; the scanner
    /// must still resolve each to its own index.
    #[test]
    fn adjacent_masks_resolve_to_their_own_tokens() {
        let segment = only_segment("[[a]][[b]] tail\n");
        assert_eq!(segment.content, "ab tail");
        let targets: Vec<String> = segment
            .runs
            .iter()
            .filter_map(|r| match &r.kind {
                ReadingInlineRunKind::Wikilink { target, .. } => Some(target.clone()),
                _ => None,
            })
            .collect();
        assert_eq!(targets, vec!["a".to_string(), "b".to_string()]);
    }

    // --- Adversarial-review regressions (round 2) ---

    /// CommonMark maps ONE line ending inside a code span to one space.
    /// The probe collapses CRLF to a single space (recording the offset
    /// so slices still map back), so an LF file and a CRLF file render
    /// the same code span identically — a naive byte-for-byte swap would
    /// have produced two spaces on CRLF.
    #[test]
    fn code_span_across_a_line_break_renders_one_space() {
        assert_eq!(only_segment("code `a\nb` span\n").content, "code a b span");
        assert_eq!(
            only_segment("code `a\r\nb` span\n").content,
            "code a b span",
            "CRLF must not double the space CommonMark specifies"
        );
    }

    /// A hard break's authored bytes survive: the probe holds no line
    /// terminators, so pulldown never mints a break event and the two
    /// trailing spaces plus the terminator come back verbatim.
    #[test]
    fn hard_break_bytes_are_preserved_verbatim() {
        assert_eq!(only_segment("hard  \nbreak\n").content, "hard  \nbreak");
        assert_eq!(
            only_segment("hard  \r\nbreak\r\n").content,
            "hard  \r\nbreak"
        );
    }

    /// Escapes and entities resolve — the authored-slice restoration must
    /// not smuggle the raw bytes back for a construct pulldown rewrote.
    #[test]
    fn escapes_and_entities_resolve() {
        assert_eq!(
            only_segment("esc \\* and &amp; entity\n").content,
            "esc * and & entity"
        );
        assert_eq!(only_segment("\\[[not a link]]\n").content, "[[not a link]]");
    }

    #[test]
    fn autolinks_are_external_link_runs() {
        assert_eq!(
            run_view(&only_segment("auto <https://example.com> link\n"))[1],
            (
                "https://example.com".to_string(),
                ReadingInlineRunKind::ExternalLink {
                    url: "https://example.com".to_string()
                }
            )
        );
    }

    /// Accepted divergence from a BLOCK parse, matching the retired
    /// `.inlineOnlyPreservingWhitespace` contract: the whole segment is
    /// one inline run, so emphasis may pair across what would have been a
    /// paragraph boundary — and the authored blank line survives intact.
    #[test]
    fn emphasis_may_pair_across_a_former_blank_line() {
        let segment = only_segment("- a *em\n\n  more* b\n");
        assert_eq!(segment.content, "a em\n\n  more b");
        assert_eq!(
            segment
                .runs
                .iter()
                .map(|r| r.styles.clone())
                .collect::<Vec<_>>(),
            vec![vec![], vec![ReadingInlineStyle::Emphasis], vec![]]
        );
    }

    /// A link reference definition is block syntax, so an inline-only
    /// parse leaves it literal — as the retired pipeline did.
    #[test]
    fn reference_definitions_stay_literal() {
        let segment = only_segment("[ref]\n\n[ref]: https://example.com\n");
        assert_eq!(segment.content, "[ref]\n\n[ref]: https://example.com");
        assert!(
            segment
                .runs
                .iter()
                .all(|r| r.kind == ReadingInlineRunKind::Text)
        );
    }

    /// Inline HTML is never interpreted; its bytes render as prose.
    #[test]
    fn inline_html_stays_literal() {
        assert_eq!(only_segment("a <b>html</b> c\n").content, "a <b>html</b> c");
    }

    /// Token selection runs on the BLOCK-parsed content while the walk
    /// runs on the flattened probe, so a code span can form across a
    /// blank line that separated its backticks at selection time. A code
    /// span binds tighter than a link (CommonMark §6.1), so the token
    /// inside it must render as its AUTHORED bytes with no affordance —
    /// never code-styled text that silently navigates.
    #[test]
    fn a_token_swallowed_by_a_late_code_span_stays_literal() {
        let segment = only_segment("- `a\n\n  [[Note]]`\n");
        assert!(
            segment.content.contains("[[Note]]"),
            "the authored token bytes render literally: {:?}",
            segment.content
        );
        assert!(
            segment
                .runs
                .iter()
                .all(|r| r.kind == ReadingInlineRunKind::Text),
            "code-styled text must never carry a link affordance: {:?}",
            segment.runs
        );
        assert_partitions(&segment);
    }

    /// Same rule for raw HTML: a token swallowed by a tag renders as its
    /// authored bytes.
    #[test]
    fn a_token_swallowed_by_raw_html_stays_literal() {
        let segment = only_segment("a <span title=\"[[Note]]\">b</span>\n");
        assert!(
            segment
                .runs
                .iter()
                .all(|r| r.kind == ReadingInlineRunKind::Text),
            "{:?}",
            segment.runs
        );
    }

    /// The CRLF collapse makes probe and `masked` offsets diverge, so
    /// every slice goes through `to_masked`. These pin the mapping at the
    /// boundaries that matter: a token immediately after a collapsed
    /// break, immediately before one, and at the very start and end.
    #[test]
    fn crlf_offsets_map_exactly_around_tokens() {
        assert_eq!(
            only_segment("a\r\n[[Note]]\r\nb\n").content,
            "a\r\nNote\r\nb"
        );
        assert_eq!(only_segment("[[Note]]\r\ntail\n").content, "Note\r\ntail");
        assert_eq!(only_segment("lead\r\n[[Note]]\n").content, "lead\r\nNote");
        assert_eq!(
            only_segment("a\r\nb\r\n[[Note]] c\n").content,
            "a\r\nb\r\nNote c",
            "two collapses before the token"
        );
        // A blank CRLF line inside a loose list item: the collapses are
        // adjacent, which is where an off-by-one in `to_masked` would show.
        assert_eq!(
            reading_inline_segments_source("- a\r\n\r\n  [[Note]] b\r\n", &[], &[])[0].segments[0]
                .content,
            "a\r\n\r\n  Note b"
        );
    }

    /// A code span nested inside emphasis still binds tighter than a
    /// link: the token stays literal and carries BOTH styles.
    #[test]
    fn literal_token_mode_survives_nesting() {
        let segment = only_segment("*em `code [[Note]] span` tail*\n");
        assert_eq!(segment.content, "em code [[Note]] span tail");
        let code_run = segment
            .runs
            .iter()
            .find(|r| r.styles.contains(&ReadingInlineStyle::InlineCode))
            .expect("a code run");
        assert_eq!(
            code_run.styles,
            vec![ReadingInlineStyle::Emphasis, ReadingInlineStyle::InlineCode]
        );
        assert_eq!(code_run.kind, ReadingInlineRunKind::Text);
    }

    /// CommonMark permits line endings inside an HTML tag, and raw HTML
    /// is never interpreted — so its authored terminators survive rather
    /// than arriving as the probe's spaces.
    #[test]
    fn multiline_inline_html_keeps_its_authored_line_endings() {
        assert_eq!(
            only_segment("a <span\nclass=\"x\">b</span> c\n").content,
            "a <span\nclass=\"x\">b</span> c"
        );
        assert_eq!(
            only_segment("a <span\r\nclass=\"x\">b</span> c\r\n").content,
            "a <span\r\nclass=\"x\">b</span> c"
        );
        assert_eq!(
            only_segment("a <!-- one\ntwo --> b\n").content,
            "a <!-- one\ntwo --> b"
        );
    }

    // --- alignment ---

    #[test]
    fn inlines_align_one_to_one_with_blocks() {
        let source =
            "# H\n\npara [[a]]\n\n- item\n\n> quote\n\n```\ncode\n```\n\n| a |\n|---|\n| 1 |\n";
        assert_eq!(
            reading_blocks_source(source).len(),
            reading_inline_segments_source(source, &[], &[]).len()
        );
    }
}
