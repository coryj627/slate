// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canonical editor syntax spans (#377).
//!
//! Produces the syntax/semantic spans the editor highlights, in the
//! backend, per `05` §1.1/§1.2 — *"accessibility is owned by the data
//! model and the Rust backend, not the UI layer; the UI consumes
//! artifacts it does not generate."* This replaces the ~21 regex passes
//! that currently live in the Swift layer (`EditorSyntaxSpans.swift`),
//! which both cost ~182 ms/keystroke at 2 MB and violate that doctrine.
//!
//! ## Layered design (composition, not a new parser)
//!
//! 1. **Markdown structure** ([`markdown_spans`]): walk the same
//!    `pulldown-cmark` parse that already feeds the render view and the
//!    link/task/block extractors.
//! 2. **Slate-specific tokens** ([`highlight_spans`] folds these in):
//!    `[[wikilink]]` / `![[embed]]` (reusing [`crate::links`], already
//!    code-suppressed), `[@cite]` (reusing [`crate::citations`]), and
//!    dependency-free scanners for `#tag` and `%%comment%%`.
//! 3. **Fenced-code internals** ([`highlight_spans`] overlays these):
//!    reuse [`crate::code`]'s tree-sitter tokens, mapped to host offsets
//!    and nested inside the `CodeFence` span.
//! 4. **Ranged `highlights(in: range)`** exposed over FFI — *later*.
//!
//! ## Coordinates
//!
//! All offsets are **UTF-8 byte offsets** into the host source, the same
//! space [`crate::code::SyntaxToken`] and [`crate::code::CodeBlock`] use.
//! The Swift consumer converts to a UTF-16 `NSRange` at the boundary (it
//! already performs byte↔UTF-16 conversion for cursor placement).

use crate::code::{highlight_code, RawCodeBlock, TokenKind};
use crate::links::LinkKind;
// extract_links / extract_citations are used only by the #[cfg(test)]
// differential oracles (wikilink_spans / citation_spans); production fuses
// their work via collect_code_ranges + scan_wikilinks + scan_citations (#388).
#[cfg(test)]
use crate::citations::extract_citations;
#[cfg(test)]
use crate::links::extract_links;
use pulldown_cmark::{CodeBlockKind, Event, HeadingLevel, Options, Parser, Tag};

/// Classifies one editor span. Markdown-structure kinds come from the
/// CommonMark parse; the Slate-specific kinds come from their
/// extractors/scanners; an inner-code kind is added by a later layer.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum EditorSpanKind {
    /// ATX (`#`..`######`) or setext heading. Carries the level 1..=6.
    Heading(u8),
    /// `*italic*` / `_italic_` (whole run, markers included — see note
    /// on [`markdown_spans`]).
    Emphasis,
    /// `**bold**` / `__bold__`.
    Strong,
    /// `~~struck~~` (requires `Options::ENABLE_STRIKETHROUGH`).
    Strikethrough,
    /// `` `inline code` ``.
    InlineCode,
    /// A fenced or indented code block — the container. Highlighting of
    /// the code *inside* is a later layer via [`crate::code`].
    CodeFence,
    /// `[text](url)`, reference links, and autolinks.
    Link,
    /// `![alt](url)`.
    Image,
    /// `> quote`.
    BlockQuote,

    // --- Slate-specific (Obsidian / Pandoc) tokens, not CommonMark ---
    /// `[[target]]` wikilink — full syntax including the `[[` … `]]`.
    Wikilink,
    /// `![[target]]` embed — full syntax including the leading `!`.
    Embed,
    /// Inline `#tag` (not a heading).
    Tag,
    /// `[@key]` / `@key` Pandoc citation — full syntax.
    Citation,
    /// `%% … %%` Obsidian comment (inline or multi-line).
    Comment,
    /// YAML frontmatter block (`---` … `---`) at the start of the note,
    /// emitted as one span over the whole block.
    Frontmatter,
    /// A token *inside* a fenced/indented code block, from `code.rs`'s
    /// tree-sitter highlighting. An overlay that nests within
    /// [`EditorSpanKind::CodeFence`] — the one intentional overlap in
    /// [`highlight_spans`].
    Code(TokenKind),
}

/// One classified span over the host source, in UTF-8 byte offsets.
/// Mirrors [`crate::code::SyntaxToken`]'s shape so structure spans and
/// code-internal tokens share one representation across the FFI.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EditorSpan {
    pub start_byte: u32,
    pub end_byte: u32,
    pub kind: EditorSpanKind,
}

impl EditorSpan {
    fn new(range: std::ops::Range<usize>, kind: EditorSpanKind) -> Self {
        EditorSpan {
            start_byte: range.start as u32,
            end_byte: range.end as u32,
            kind,
        }
    }
}

/// The full editor highlight span set for `source`: CommonMark
/// structure, the Slate-specific tokens, and per-token code-block
/// internals. Overlaps are resolved by priority into a document-ordered
/// list; the result is non-overlapping **except** that
/// [`EditorSpanKind::Code`] tokens nest inside their
/// [`EditorSpanKind::CodeFence`] container (the apply layer stamps the
/// fence's base style, then the token colours on top).
///
/// Overlap policy mirrors the prior Swift `findEditorSyntaxSpans`
/// coverage scheme: higher-priority spans win, and any span that
/// intersects an already-accepted one is dropped — so a `#tag` inside a
/// fenced code block, or any token inside a `%%comment%%`, is not
/// separately highlighted. Wikilinks and citations already arrive
/// code-suppressed from their canonical extractors; the tag/comment
/// scanners rely on this sweep for the same suppression.
///
/// `Link`, `Image`, and `BlockQuote` are intentionally excluded: the
/// prior Swift editor never coloured them, and treating a multi-line
/// blockquote as one flat span conflicts with highlighting its
/// contents. They remain available from [`markdown_spans`] as raw
/// structure for other consumers.
pub fn highlight_spans(source: &str) -> Vec<EditorSpan> {
    // #388: two pulldown passes, not four. One RAW pass yields the markdown
    // structure spans + the per-token code internals; one BODY pass feeds
    // both the wikilink and citation scanners.
    let (structure, code) = raw_structure_and_code(source);
    let mut spans: Vec<EditorSpan> = structure
        .into_iter()
        .filter(|s| {
            !matches!(
                s.kind,
                EditorSpanKind::Link | EditorSpanKind::Image | EditorSpanKind::BlockQuote
            )
        })
        .collect();
    // #384: emit one high-priority span over the YAML frontmatter block.
    // `raw_structure_and_code` runs pulldown on the raw source, so it
    // produces spurious structure spans (Heading/CodeFence) over the YAML;
    // the overlap sweep masks them — and any tag/comment the scanners find
    // inside frontmatter — beneath this Frontmatter span.
    let fm_end = source.len() - crate::frontmatter::body_after_frontmatter(source).len();
    if fm_end > 0 {
        spans.push(EditorSpan {
            start_byte: 0,
            end_byte: fm_end as u32,
            kind: EditorSpanKind::Frontmatter,
        });
    }
    let (wikilinks, citations) = body_link_citation_spans(source);
    spans.extend(wikilinks);
    spans.extend(citations);
    spans.extend(scan_tags(source));
    spans.extend(scan_comments(source));
    let mut resolved = resolve_overlaps(source, spans);
    // Slice 3: overlay per-token code-block internals. Added *after* the
    // sweep so the CodeFence span still masks markdown/tags elsewhere in
    // the block, while the tokens themselves nest inside it (the one
    // intentional overlap) for the apply layer to stamp on top.
    resolved.extend(code);
    resolved.sort_by_key(|s| s.start_byte);
    resolved
}

/// Result of a range-scoped re-highlight (#379). `spans` are in
/// **whole-document byte offsets** and authoritatively cover **all** of
/// `applied_range` — the consumer removes its prior temporary attributes
/// over `applied_range` (and any earlier window's leftovers) and re-adds
/// from `spans`. A whole-document **fallback** is signalled by
/// `applied_range == 0..source.len()` (with `spans == highlight_spans`),
/// so the apply path is uniform for ranged and full.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RangedHighlight {
    pub applied_range: std::ops::Range<usize>,
    pub spans: Vec<EditorSpan>,
}

/// Re-highlight only a **window around `dirty`**, falling back to a
/// whole-document parse whenever the window can't be parsed in isolation
/// (#379). The expensive span work (pulldown + tree-sitter + the scanners
/// + overlap resolution) then scales with the window, not the document.
///
/// Correctness rests on **substring-parse equivalence**: for every
/// non-fallback case, `highlight_spans(&source[window])` offset by
/// `window.start` equals the slice of `highlight_spans(source)` that lies
/// in `applied_range`. That holds only when the window is a
/// context-independent unit, because [`highlight_spans`] is **not**
/// context-free in its input — it composes [`crate::links::extract_links`]
/// / [`crate::citations::extract_citations`], which re-derive YAML
/// frontmatter from **byte 0 of their input** and suppress tokens inside
/// it, and it emits a `Frontmatter` span from the same `fm_end`. So the
/// algorithm snaps to whole lines, extends to **blank-line boundaries**
/// (unconditional CommonMark block separators), and **falls back** on any
/// construct whose classification can reach outside such a window: a
/// `---`-shaped line at the window head, the real top-of-document
/// frontmatter region, a `` ``` ``/`~~~` fence touch or a window that
/// opens inside an unclosed fence, or a `%%…%%` comment touch. Fence /
/// comment detection is **one-sided conservative** — it over-falls-back
/// rather than ever window-wrong inside a code block or comment. The
/// `editor_spans` test proptest (random `---`/wikilink/fence sources) is
/// the arbiter of this invariant.
///
/// The fallback structural scan is O(window-prefix) light (a `\n`/marker
/// byte scan, no pulldown/tree-sitter); the heavy parse is O(window).
/// True O(edit) (incremental structure) would need a cached parse and is
/// deferred.
pub fn highlight_spans_in_range(source: &str, dirty: std::ops::Range<usize>) -> RangedHighlight {
    highlight_spans_in_range_with(source, dirty, &StructureSnapshot::from_source(source))
}

/// [`highlight_spans_in_range`] with the raw-source block structure injected
/// — the #404 seam. The free function (and the differential oracle) pass a
/// freshly parsed [`StructureSnapshot`]; the stateful `DocumentBuffer` will
/// pass its incrementally maintained one. Behaviour is identical for an equal
/// snapshot, so the existing invariant proptests gate this refactor.
pub(crate) fn highlight_spans_in_range_with(
    source: &str,
    dirty: std::ops::Range<usize>,
    raw_structure: &StructureSnapshot,
) -> RangedHighlight {
    let whole = || RangedHighlight {
        applied_range: 0..source.len(),
        spans: highlight_spans(source),
    };

    // 1. Clamp + snap the dirty range to char boundaries.
    let d_start = floor_char_boundary(source, dirty.start);
    let d_end = ceil_char_boundary(source, dirty.end.max(dirty.start));

    // 2. Snap to whole lines, then 3. extend to blank-line boundaries so
    // the window holds only whole CommonMark blocks.
    let win_start = extend_up_to_blank(source, line_start(source, d_start));
    let win_end = extend_down_to_blank(source, line_end(source, d_end));

    // --- Conservative fallbacks (any "yes" → whole-document) ---

    // Real top-of-document frontmatter: any edit in/touching it. (Its
    // internal lines can be blank, so blank-extension alone can't bound
    // it; and editing a `---` delimiter reclassifies the boundary.)
    let body = crate::frontmatter::body_after_frontmatter(source);
    let fm_end = source.len() - body.len();
    if fm_end > 0 && win_start < fm_end {
        return whole();
    }
    // A `---`-shaped line at the window head would be re-read as a
    // frontmatter open by the composed extractors (byte-0 anchored), or
    // could itself be creating frontmatter at byte 0.
    if line_is_dashes_delim(source, win_start) {
        return whole();
    }
    // The window can be parsed in isolation only when its opaque-block
    // (code/HTML) structure exactly matches the whole document's within the
    // window — neither CUT (#1 an unclosed HTML block above the window), nor
    // GROW (#4 a ≤3-space list-content fence whose list-dedent close is
    // lost), nor INVENT one (#3 an indented continuation that's top-level
    // code alone) — and (RAW framing only) when an indented window head
    // isn't list/quote content that would reparse top-level and invent a
    // setext `Heading` the document lacks (#5). `markdown_spans` + the
    // tag/comment scanners run on the RAW source, so check both there.
    if window_diverges(raw_structure, source, win_start, win_end, true) {
        return whole();
    }
    // The link/citation extractors run on the frontmatter-STRIPPED body
    // (byte-0 anchored). When real frontmatter is present the strip can
    // change the body's block structure relative to raw — e.g. it un-pairs a
    // `~~~`/`` ``` ``/`<!--` so a window the raw parse proves block-clean is
    // actually inside an open block in the body framing (#379 review,
    // CRITICAL #2). Only the opaque-block half applies (container nesting
    // doesn't change wikilink/citation suppression). `win_start >= fm_end`
    // here (we returned above otherwise), so the offsets never underflow.
    if fm_end > 0
        && window_diverges(
            &StructureSnapshot::from_source(body),
            body,
            win_start - fm_end,
            win_end - fm_end,
            false,
        )
    {
        return whole();
    }
    // `%%…%%` comments: a `%%` typed in the window, or a window that
    // intersects an existing comment.
    if source[win_start..win_end].contains("%%")
        || scan_comments(source)
            .iter()
            .any(|c| (c.start_byte as usize) < win_end && win_start < (c.end_byte as usize))
    {
        return whole();
    }

    // 4. Parse the window in isolation and shift into document space.
    let mut spans = highlight_spans(&source[win_start..win_end]);
    let offset = win_start as u32;
    for s in &mut spans {
        s.start_byte += offset;
        s.end_byte += offset;
    }
    RangedHighlight {
        applied_range: win_start..win_end,
        spans,
    }
}

// --- Range-scope helpers (#379) ---------------------------------------

fn floor_char_boundary(source: &str, byte: usize) -> usize {
    let mut b = byte.min(source.len());
    while b > 0 && !source.is_char_boundary(b) {
        b -= 1;
    }
    b
}

fn ceil_char_boundary(source: &str, byte: usize) -> usize {
    let mut b = byte.min(source.len());
    while b < source.len() && !source.is_char_boundary(b) {
        b += 1;
    }
    b
}

/// Byte offset of the start of the line containing `byte` (just past the
/// previous `\n`, or 0). `byte` must be a char boundary.
fn line_start(source: &str, byte: usize) -> usize {
    source[..byte].rfind('\n').map_or(0, |i| i + 1)
}

/// Byte offset just past the end of the line containing `byte` — the byte
/// after the next `\n`, or `source.len()`. `byte` must be a char boundary.
fn line_end(source: &str, byte: usize) -> usize {
    let byte = byte.min(source.len());
    source[byte..]
        .find('\n')
        .map_or(source.len(), |i| byte + i + 1)
}

/// A blank line is empty or whitespace-only (a CommonMark block
/// separator). `line_start_byte` must be a line start.
fn line_is_blank(source: &str, line_start_byte: usize) -> bool {
    let le = line_end(source, line_start_byte);
    source[line_start_byte..le].trim().is_empty()
}

/// Walk `start` (a line start) up to a block boundary — the first line of
/// the contiguous non-blank run, i.e. just after the nearest blank line
/// above (or BOF).
fn extend_up_to_blank(source: &str, start: usize) -> usize {
    let mut ls = start;
    while ls > 0 {
        let prev = line_start(source, ls - 1);
        if line_is_blank(source, prev) {
            break;
        }
        ls = prev;
    }
    ls
}

/// Walk `end` (a line end) down to a block boundary — the start of the
/// nearest blank line below (or EOF).
fn extend_down_to_blank(source: &str, end: usize) -> usize {
    let mut le = end;
    while le < source.len() {
        if line_is_blank(source, le) {
            break;
        }
        le = line_end(source, le);
    }
    le
}

/// Frontmatter-delimiter-shaped line: trims to exactly `---`. Conservative
/// (ignores leading indentation, which real frontmatter forbids) — a
/// false positive only over-falls-back.
fn line_is_dashes_delim(source: &str, line_start_byte: usize) -> bool {
    let le = line_end(source, line_start_byte);
    source[line_start_byte..le].trim() == "---"
}

/// A discriminant for the **opaque block** kinds whose presence/extent the
/// window must reproduce: indented code, fenced code, and HTML block. The
/// kind matters, not just the range: an *empty* fenced block emits a
/// `CodeFence` but no inner `Code` token, while an indented block over the
/// same bytes feeds its body to tree-sitter and DOES emit `Code` tokens; an
/// HTML block emits no span at all. So a window that flips Fenced↔Indented
/// (or code↔HTML) over the identical byte range still diverges (#379
/// review, CRITICAL #6 — a `\t`/4-space `` ``` `` that's empty list-content
/// fence whole-doc but top-level indented code in the window).
fn opaque_block_kind(tag: &Tag) -> Option<u8> {
    match tag {
        Tag::CodeBlock(CodeBlockKind::Indented) => Some(0),
        Tag::CodeBlock(CodeBlockKind::Fenced(_)) => Some(1),
        Tag::HtmlBlock => Some(2),
        _ => None,
    }
}

/// Every opaque block in `source`'s own CommonMark parse as `(start, end,
/// kind)`, in document order. Both code and HTML blocks make their interior
/// opaque to the inline parse, the scanners' overlap sweep, and the
/// code-internals overlay. HTML blocks are included because CommonMark
/// types 1–5 (`<!--`, `<script>`/`<style>`/`<pre>`, `<?`, `<![CDATA[`)
/// aren't closed by a blank line — only by their specific closer — so an
/// unclosed one above the window changes whether an in-window `` ``` `` is a
/// fence (#379 review, CRITICAL #1). Light (block structure only; no
/// tree-sitter).
fn opaque_blocks(source: &str) -> Vec<(usize, usize, u8)> {
    Parser::new_ext(source, Options::ENABLE_STRIKETHROUGH)
        .into_offset_iter()
        .filter_map(|(event, range)| match event {
            Event::Start(tag) => opaque_block_kind(&tag).map(|k| (range.start, range.end, k)),
            _ => None,
        })
        .collect()
}

/// The whole-document block structure that [`window_diverges`] consumes:
/// every opaque block (kind-tagged, see [`opaque_blocks`]) and every
/// `List`/`Item`/`BlockQuote` container range, in document order. One
/// CommonMark parse builds it. #404 Slice A rebuilds it per call (the
/// behaviour-preserving oracle); Slice B's `DocumentBuffer` will maintain it
/// incrementally so the keystroke path never reparses the whole document.
#[derive(Debug, Clone, PartialEq, Eq, Default)]
pub(crate) struct StructureSnapshot {
    blocks: Vec<(usize, usize, u8)>,
    containers: Vec<(usize, usize)>,
    /// Length of the source this was built from. A block/container whose end
    /// equals `len` is **open** (an unclosed fence/HTML/list ran to EOF) and
    /// can extend when text is appended — so a clean break can't sit at or
    /// after its start even though its recorded `end` looks like a boundary.
    len: usize,
}

impl StructureSnapshot {
    /// Build from a whole-document CommonMark parse (the oracle / fallback
    /// path; also the suffix re-parse in [`updated`](Self::updated)).
    pub(crate) fn from_source(source: &str) -> Self {
        let mut blocks = Vec::new();
        let mut containers = Vec::new();
        for (event, range) in
            Parser::new_ext(source, Options::ENABLE_STRIKETHROUGH).into_offset_iter()
        {
            if let Event::Start(tag) = &event {
                if let Some(kind) = opaque_block_kind(tag) {
                    blocks.push((range.start, range.end, kind));
                } else if matches!(tag, Tag::List(_) | Tag::Item | Tag::BlockQuote) {
                    containers.push((range.start, range.end));
                }
            }
        }
        Self {
            blocks,
            containers,
            len: source.len(),
        }
    }

    /// A blank line at `r` is a **clean break** — a fresh-parser-state point,
    /// so parsing `source[r..]` reproduces the whole-document parse restricted
    /// to `[r..]` even after an edit just below `r` — iff:
    /// - `r` isn't strictly inside a cached block/container (e.g. a blank line
    ///   inside a fence), AND
    /// - the nearest non-blank line above `r` isn't part of an **absorbing**
    ///   construct: indented code (`k == 0`), an open block (`end == len`, ran
    ///   to EOF and can extend), or a list/quote container. Those re-absorb a
    ///   following blank + compatible content, so an edit below `r` could
    ///   extend them across it; a paragraph / heading / thematic break /
    ///   closed fence / HTML block does not. (`%%` comments are a slate
    ///   overlay that doesn't affect pulldown's block parse, so they're
    ///   irrelevant here and aren't cached.)
    fn break_is_clean(&self, source: &str, r: usize) -> bool {
        if self.blocks.iter().any(|&(s, e, _)| s < r && r < e)
            || self.containers.iter().any(|&(s, e)| s < r && r < e)
        {
            return false;
        }
        let mut ls = r;
        while ls > 0 {
            let prev = line_start(source, ls - 1);
            if !line_is_blank(source, prev) {
                let pe = line_end(source, prev);
                let hits = |s: usize, e: usize| s < pe && prev < e;
                let absorbing_block = self
                    .blocks
                    .iter()
                    .any(|&(s, e, k)| (k == 0 || e == self.len) && hits(s, e));
                let absorbing_container = self.containers.iter().any(|&(s, e)| hits(s, e));
                return !(absorbing_block || absorbing_container);
            }
            ls = prev;
        }
        true
    }

    /// The latest **clean break** at or before `at`: the start of a blank
    /// line that no cached block / container / comment covers — a point
    /// where pulldown's block state is fresh, so parsing `source[r..]`
    /// reproduces the whole-document parse restricted to `[r..]`. Returns 0
    /// (the document start — always a clean break) if none is found above.
    ///
    /// Soundness: blank lines are paragraph boundaries, so a clean break is
    /// never *inside* a paragraph — which is what makes it safe against
    /// setext underlines (the affected paragraph always starts after the
    /// break) and lazy continuation. `source[..at]` must equal the source
    /// `self` was built from (the incremental contract: the prefix above the
    /// edit is unchanged), so an old clean break is a new clean break.
    fn clean_break_before(&self, source: &str, at: usize) -> usize {
        let mut ls = line_start(source, at.min(source.len()));
        loop {
            if ls == 0 {
                return 0;
            }
            if line_is_blank(source, ls) && self.break_is_clean(source, ls) {
                return ls;
            }
            ls = line_start(source, ls - 1);
        }
    }

    /// Shift every cached range by `by` (suffix re-parse → document space).
    fn shifted(mut self, by: usize) -> Self {
        for b in &mut self.blocks {
            b.0 += by;
            b.1 += by;
        }
        for c in &mut self.containers {
            c.0 += by;
            c.1 += by;
        }
        self
    }

    /// Incrementally produce the structure of `new_source` from `self` (the
    /// pre-edit structure), given the edit began at byte `edit_start` with
    /// `new_source[..edit_start]` byte-identical to the source `self` was
    /// built from. #404 Slice B.
    ///
    /// Re-parses only the document SUFFIX from a clean break at or before the
    /// edit and reuses the untouched prefix. **Correct by construction** — the
    /// suffix part IS a `from_source` parse of `new_source[r..]`, and a clean
    /// break guarantees that equals the whole-document parse there; the prefix
    /// is unchanged. The buffer `debug_assert!`s `updated == from_source` on
    /// every edit. (Phase 1 re-parses to EOF — O(suffix); the reconvergence
    /// stop that bounds it to O(edit) is layered on next.)
    pub(crate) fn updated(&self, new_source: &str, edit_start: usize) -> Self {
        let r = self.clean_break_before(new_source, edit_start);
        if r == 0 {
            return Self::from_source(new_source);
        }
        let suffix = Self::from_source(&new_source[r..]).shifted(r);
        // A clean break splits every cached range cleanly (nothing covers r),
        // so "start < r" keeps exactly the prefix ranges; the suffix re-parse
        // owns `[r, len)`. Block order stays document order (prefix < r ≤
        // suffix), which `window_diverges`' equality compare relies on.
        let mut blocks: Vec<(usize, usize, u8)> = self
            .blocks
            .iter()
            .copied()
            .filter(|&(s, ..)| s < r)
            .collect();
        blocks.extend(suffix.blocks);
        let mut containers: Vec<(usize, usize)> = self
            .containers
            .iter()
            .copied()
            .filter(|&(s, _)| s < r)
            .collect();
        containers.extend(suffix.containers);
        Self {
            blocks,
            containers,
            len: new_source.len(),
        }
    }
}

/// True when the window `[win_start, win_end)` can't be parsed in isolation
/// — its result would differ from the whole document's spans within the
/// window — so it must fall back. This is the general form of all five
/// #379-review CRITICALs, which split into two divergence families:
///
/// **Opaque-block mismatch.** The window's own opaque-block (code/HTML)
/// ranges, shifted to document space, must EQUAL the whole-doc blocks that
/// intersect the window. A pure straddle test (`intersects && !contained`)
/// catches only a block the window *cuts*; equality also catches a block
/// the window *grows* (CRITICAL #4 — a `≤3`-space list-content fence whose
/// list-dedent close the window loses) or *invents* (CRITICAL #3 — an
/// indented continuation that's top-level code alone), and an HTML block
/// the window reads as a fence (CRITICAL #1). A non-contained whole-doc
/// block can't equal a window block, so equality subsumes the straddle
/// test. Genuine top-level code that fits the window reproduces exactly, so
/// it still windows.
///
/// **Lost container context** (`check_container`). When the window's first
/// non-blank line is an indented continuation that, in the whole-doc parse,
/// sits inside a list item or block quote opened ABOVE the window, that
/// line loses its container in isolation and reparses as a top-level block:
/// a continuation paragraph + a following `===`/`---` becomes an invented
/// setext `Heading` (CRITICAL #5 — invisible to the block check), an
/// indented continuation becomes indented code (#3), a list-content fence
/// grows (#4) or flips Fenced→Indented (#6). It's the window's first
/// *non-blank* line that matters, not `win_start` itself: a loose list puts
/// blank lines between items, so the window can open on a blank line with
/// the indented content just below it (#6). A non-indented first line (a
/// list-marker line, a column-0 paragraph that ends the list) reparses the
/// same alone, so ordinary list edits still window; genuine top-level
/// indented code isn't in a container, so it windows. Only the RAW framing
/// needs this (the setext `Heading` comes from `markdown_spans` on the raw
/// source); the stripped-body framing governs only wikilink/citation
/// suppression, which list/quote nesting doesn't change. Both checks read the
/// same precomputed [`StructureSnapshot`] (#404): Slice A rebuilds it per call
/// (the oracle), Slice B maintains it incrementally across edits.
fn window_diverges(
    structure: &StructureSnapshot,
    source: &str,
    win_start: usize,
    win_end: usize,
    check_container: bool,
) -> bool {
    // Lost container context: the window's first non-blank line is an indented
    // continuation inside a list/quote opened above `win_start`.
    if check_container
        && structure
            .containers
            .iter()
            .any(|&(start, end)| start < win_start && win_start < end)
        && window_first_nonblank_is_indented(source, win_start, win_end)
    {
        return true;
    }
    // Opaque-block mismatch: the window's own (code/HTML) blocks, shifted to
    // document space, must EQUAL the whole-doc blocks intersecting the window.
    let whole_blocks: Vec<(usize, usize, u8)> = structure
        .blocks
        .iter()
        .copied()
        .filter(|&(start, end, _)| start < win_end && win_start < end)
        .collect();
    let win_blocks: Vec<(usize, usize, u8)> = opaque_blocks(&source[win_start..win_end])
        .into_iter()
        .map(|(bs, be, k)| (bs + win_start, be + win_start, k))
        .collect();
    win_blocks != whole_blocks
}

/// True when the first non-blank line in the window `[win_start, win_end)`
/// begins with whitespace (a space or tab) — an indented continuation, not
/// a list-marker line or column-0 block. Skipping leading blank lines is
/// what makes the container guard catch a window that opens on a loose
/// list's inter-item blank with the indented content just below (#6).
fn window_first_nonblank_is_indented(source: &str, win_start: usize, win_end: usize) -> bool {
    let mut ls = win_start;
    while ls < win_end {
        let le = line_end(source, ls);
        let line = &source[ls..le.min(win_end)];
        if !line.trim().is_empty() {
            return line.starts_with(' ') || line.starts_with('\t');
        }
        ls = le;
    }
    false
}

/// Walk the CommonMark structure of `source` and emit syntax spans in
/// document order.
///
/// Uses the same parser options as the rest of the backend
/// (`ENABLE_STRIKETHROUGH`) so the editor highlight agrees with the
/// render view and the existing extractors.
///
/// Notes:
/// - Emphasis / strong / strikethrough spans cover the full run
///   *including* the markers (what `into_offset_iter` reports). The
///   Swift highlight today colours only the markers; reconciling
///   marker-only vs. whole-run is a deliberate follow-up at the apply
///   layer (#377).
/// - Slate-specific tokens (`[[…]]`, `#tag`, `[@cite]`, `%%…%%`) are
///   **not** emitted here; [`highlight_spans`] composes them on top.
pub fn markdown_spans(source: &str) -> Vec<EditorSpan> {
    let mut out: Vec<EditorSpan> = Vec::new();
    for (event, range) in Parser::new_ext(source, Options::ENABLE_STRIKETHROUGH).into_offset_iter()
    {
        if let Some(kind) = structure_span_kind(&event) {
            out.push(EditorSpan::new(trim_trailing_newline(source, range), kind));
        }
    }
    out
}

/// The structure-span kind for one CommonMark event, or `None` if it maps
/// to no editor span. Factored out so [`markdown_spans`] and the fused
/// [`raw_structure_and_code`] share one event→kind mapping and can't drift
/// (#388).
fn structure_span_kind(event: &Event) -> Option<EditorSpanKind> {
    Some(match event {
        Event::Start(Tag::Heading { level, .. }) => EditorSpanKind::Heading(heading_level(*level)),
        Event::Start(Tag::Emphasis) => EditorSpanKind::Emphasis,
        Event::Start(Tag::Strong) => EditorSpanKind::Strong,
        Event::Start(Tag::Strikethrough) => EditorSpanKind::Strikethrough,
        Event::Code(_) => EditorSpanKind::InlineCode,
        Event::Start(Tag::CodeBlock(_)) => EditorSpanKind::CodeFence,
        Event::Start(Tag::Link { .. }) => EditorSpanKind::Link,
        Event::Start(Tag::Image { .. }) => EditorSpanKind::Image,
        Event::Start(Tag::BlockQuote) => EditorSpanKind::BlockQuote,
        _ => return None,
    })
}

/// One raw pulldown pass producing BOTH the markdown structure spans and
/// the per-token code-block internals — fusing what [`markdown_spans`] and
/// `code_internal_spans` did in two separate passes (#388). The structure
/// half shares [`structure_span_kind`] with `markdown_spans`; the
/// code-internal half is the same segment-table token mapping as the
/// standalone version. Both outputs are pinned byte-identical to the
/// originals by the `consolidated_matches_reference` proptest.
fn raw_structure_and_code(source: &str) -> (Vec<EditorSpan>, Vec<EditorSpan>) {
    use pulldown_cmark::{CodeBlockKind, TagEnd};
    let mut structure: Vec<EditorSpan> = Vec::new();
    let mut code: Vec<EditorSpan> = Vec::new();
    let mut in_code = false;
    let mut language: Option<String> = None;
    let mut content = String::new();
    // (content_start, host_start, len) per Text event — a 1:1 run.
    let mut segs: Vec<(usize, usize, usize)> = Vec::new();
    for (event, range) in Parser::new_ext(source, Options::ENABLE_STRIKETHROUGH).into_offset_iter()
    {
        if let Some(kind) = structure_span_kind(&event) {
            structure.push(EditorSpan::new(
                trim_trailing_newline(source, range.clone()),
                kind,
            ));
        }
        match event {
            Event::Start(Tag::CodeBlock(kind)) => {
                in_code = true;
                content.clear();
                segs.clear();
                language = match kind {
                    CodeBlockKind::Fenced(tag) => {
                        let t = tag.into_string().trim().to_string();
                        if t.is_empty() {
                            None
                        } else {
                            Some(t)
                        }
                    }
                    CodeBlockKind::Indented => None,
                };
            }
            Event::Text(s) if in_code => {
                segs.push((content.len(), range.start, s.len()));
                content.push_str(&s);
            }
            Event::End(TagEnd::CodeBlock) if in_code => {
                in_code = false;
                if content.is_empty() {
                    language = None;
                    continue;
                }
                let raw = RawCodeBlock {
                    source: std::mem::take(&mut content),
                    language: language.take(),
                    line: 0,
                    byte_offset: segs.first().map_or(0, |&(_, h, _)| h as u32),
                };
                for tok in highlight_code(&raw).tokens {
                    let (Some(s), Some(e)) = (
                        map_content_to_host(&segs, tok.start_byte as usize),
                        map_content_to_host(&segs, tok.end_byte as usize),
                    ) else {
                        continue;
                    };
                    if s < e && source.is_char_boundary(s) && source.is_char_boundary(e) {
                        code.push(EditorSpan {
                            start_byte: s as u32,
                            end_byte: e as u32,
                            kind: EditorSpanKind::Code(tok.kind),
                        });
                    }
                }
                segs.clear();
            }
            _ => {}
        }
    }
    (structure, code)
}

/// One body pulldown pass feeding BOTH the wikilink and citation scanners —
/// fusing what `wikilink_spans` (via `extract_links`) and `citation_spans`
/// (via `extract_citations`) did in two separate body passes (#388). The
/// shared `collect_code_ranges` suppresses tokens inside code identically
/// for both. Output is byte-identical to those two functions, pinned by the
/// `consolidated_matches_reference` proptest.
fn body_link_citation_spans(source: &str) -> (Vec<EditorSpan>, Vec<EditorSpan>) {
    let body = crate::frontmatter::body_after_frontmatter(source);
    let body_offset = (source.len() - body.len()) as u32;
    let code_ranges = crate::citations::collect_code_ranges(body);
    let wikilinks = crate::links::scan_wikilinks(body, &code_ranges)
        .into_iter()
        .filter(|l| l.kind == LinkKind::Wikilink)
        .map(|l| EditorSpan {
            start_byte: l.span_start as u32 + body_offset,
            end_byte: l.span_end as u32 + body_offset,
            kind: if l.is_embed {
                EditorSpanKind::Embed
            } else {
                EditorSpanKind::Wikilink
            },
        })
        .collect();
    let citations = crate::citations::scan_citations(body, &code_ranges)
        .into_iter()
        .map(|c| EditorSpan {
            start_byte: c.byte_offset + body_offset,
            end_byte: c.byte_offset + body_offset + c.raw.len() as u32,
            kind: EditorSpanKind::Citation,
        })
        .collect();
    (wikilinks, citations)
}

/// Wikilink / embed spans, reused from the canonical link extractor
/// (which already spans the full `[[` … `]]` syntax and suppresses
/// matches inside code spans). Markdown links/images are skipped here.
///
/// #388: superseded in production by [`body_link_citation_spans`] (which
/// shares one body pass with citations); kept as the differential oracle
/// for `consolidated_matches_reference`.
#[cfg(test)]
fn wikilink_spans(source: &str) -> Vec<EditorSpan> {
    extract_links(source)
        .into_iter()
        .filter(|l| l.kind == LinkKind::Wikilink)
        .map(|l| EditorSpan {
            start_byte: l.span_start as u32,
            end_byte: l.span_end as u32,
            kind: if l.is_embed {
                EditorSpanKind::Embed
            } else {
                EditorSpanKind::Wikilink
            },
        })
        .collect()
}

/// Citation spans, reused from the canonical citation extractor (which
/// skips code spans). The span covers the verbatim `raw` slice.
///
/// #388: superseded in production by [`body_link_citation_spans`]; kept as
/// the differential oracle for `consolidated_matches_reference`.
#[cfg(test)]
fn citation_spans(source: &str) -> Vec<EditorSpan> {
    extract_citations(source)
        .into_iter()
        .map(|c| EditorSpan {
            start_byte: c.byte_offset,
            end_byte: c.byte_offset + c.raw.len() as u32,
            kind: EditorSpanKind::Citation,
        })
        .collect()
}

/// Scan inline `#tag`s: `#` + ASCII letter + Unicode word chars / `-` /
/// `/`, not preceded by a word char or another `#` (so `# heading`,
/// `##`, `word#x`, and `café#x` don't match). Mirrors the Swift
/// `(?<![\w#])#[A-Za-z][\w/-]*` with Unicode-aware `\w` (#385) — Rust's
/// `regex` has no lookbehind, so the guard is explicit, and we iterate
/// chars (not bytes) so non-ASCII tag bodies aren't truncated and a
/// non-ASCII preceding letter still suppresses the match. Suppression
/// inside code/comments is handled by the overlap sweep, not here.
fn scan_tags(source: &str) -> Vec<EditorSpan> {
    let chars: Vec<(usize, char)> = source.char_indices().collect();
    let mut out = Vec::new();
    let mut k = 0;
    while k < chars.len() {
        let (idx, c) = chars[k];
        if c == '#' {
            let prev_ok = k == 0 || {
                let p = chars[k - 1].1;
                !(is_word_char(p) || p == '#')
            };
            // First tag char must be an ASCII letter (Swift `[A-Za-z]`).
            let first_ok = matches!(chars.get(k + 1), Some(&(_, fc)) if fc.is_ascii_alphabetic());
            if prev_ok && first_ok {
                let mut j = k + 1;
                while j < chars.len() && is_tag_char(chars[j].1) {
                    j += 1;
                }
                let end = chars.get(j).map_or(source.len(), |&(b, _)| b);
                out.push(EditorSpan {
                    start_byte: idx as u32,
                    end_byte: end as u32,
                    kind: EditorSpanKind::Tag,
                });
                k = j;
                continue;
            }
        }
        k += 1;
    }
    out
}

/// Scan `%% … %%` comments (inline or multi-line). Non-overlapping:
/// scanning resumes past each close. An unterminated `%%` is not
/// emitted (mirrors the prior Swift behaviour).
fn scan_comments(source: &str) -> Vec<EditorSpan> {
    let bytes = source.as_bytes();
    let mut out = Vec::new();
    let mut i = 0;
    while i + 1 < bytes.len() {
        if bytes[i] == b'%' && bytes[i + 1] == b'%' {
            let start = i;
            let mut j = i + 2;
            let mut close = None;
            while j + 1 < bytes.len() {
                if bytes[j] == b'%' && bytes[j + 1] == b'%' {
                    close = Some(j + 2);
                    break;
                }
                j += 1;
            }
            match close {
                Some(end) => {
                    out.push(EditorSpan {
                        start_byte: start as u32,
                        end_byte: end as u32,
                        kind: EditorSpanKind::Comment,
                    });
                    i = end;
                    continue;
                }
                None => break, // unterminated `%%` — stop
            }
        }
        i += 1;
    }
    out
}

/// Per-token spans for the *internals* of fenced / indented code blocks,
/// from `code.rs`'s tree-sitter highlighting. An overlay: each token
/// nests inside its `CodeFence` span (see [`highlight_spans`]).
///
/// `highlight_code` returns token offsets relative to the block's
/// *content* — the concatenation of the block's `Event::Text` payloads.
/// pulldown **normalizes** that content (CRLF→LF, indentation stripped),
/// so it is NOT a contiguous copy of the host bytes: a global
/// `content_start + offset` mapping drifts and can land mid-codepoint on
/// CRLF/indented blocks. Instead we record a per-Text-event segment
/// table — each event is a 1:1 run, with the stripped `\r`/indent as
/// gaps between runs in host space — and translate every token offset
/// through it, dropping any token whose host range isn't a char boundary.
///
/// #388: the same logic now runs inside [`raw_structure_and_code`] (fused
/// with the structure pass); this standalone copy is kept as the
/// differential oracle for `consolidated_matches_reference`.
#[cfg(test)]
fn code_internal_spans(source: &str) -> Vec<EditorSpan> {
    use pulldown_cmark::{CodeBlockKind, TagEnd};
    let mut out = Vec::new();
    let mut in_code = false;
    let mut language: Option<String> = None;
    let mut content = String::new();
    // (content_start, host_start, len) per Text event — a 1:1 run.
    let mut segs: Vec<(usize, usize, usize)> = Vec::new();
    let parser = Parser::new_ext(source, Options::ENABLE_STRIKETHROUGH).into_offset_iter();
    for (event, range) in parser {
        match event {
            Event::Start(Tag::CodeBlock(kind)) => {
                in_code = true;
                content.clear();
                segs.clear();
                language = match kind {
                    CodeBlockKind::Fenced(tag) => {
                        let t = tag.into_string().trim().to_string();
                        if t.is_empty() {
                            None
                        } else {
                            Some(t)
                        }
                    }
                    CodeBlockKind::Indented => None,
                };
            }
            Event::Text(s) if in_code => {
                segs.push((content.len(), range.start, s.len()));
                content.push_str(&s);
            }
            Event::End(TagEnd::CodeBlock) if in_code => {
                in_code = false;
                if content.is_empty() {
                    language = None;
                    continue;
                }
                let raw = RawCodeBlock {
                    source: std::mem::take(&mut content),
                    language: language.take(),
                    line: 0,
                    byte_offset: segs.first().map_or(0, |&(_, h, _)| h as u32),
                };
                for tok in highlight_code(&raw).tokens {
                    let (Some(s), Some(e)) = (
                        map_content_to_host(&segs, tok.start_byte as usize),
                        map_content_to_host(&segs, tok.end_byte as usize),
                    ) else {
                        continue;
                    };
                    // Guard: only emit sliceable, char-boundary ranges so a
                    // mapping edge case can never panic a downstream slice.
                    if s < e && source.is_char_boundary(s) && source.is_char_boundary(e) {
                        out.push(EditorSpan {
                            start_byte: s as u32,
                            end_byte: e as u32,
                            kind: EditorSpanKind::Code(tok.kind),
                        });
                    }
                }
                segs.clear();
            }
            _ => {}
        }
    }
    out
}

/// Translate a content-space byte offset to a host-space byte offset via
/// the per-Text-event segment table (each `(content_start, host_start,
/// len)` is a 1:1 run; gaps between runs are pulldown-stripped bytes).
/// Offsets at the very end of the content map to the last run's host end.
fn map_content_to_host(segs: &[(usize, usize, usize)], off: usize) -> Option<usize> {
    for &(cstart, hstart, len) in segs {
        if off < cstart + len {
            return Some(hstart + off.saturating_sub(cstart));
        }
    }
    segs.last().map(|&(_, hstart, len)| hstart + len)
}

/// The pre-#388 four-pass implementation of [`highlight_spans`], kept as
/// the differential oracle: `consolidated_matches_reference` proptests that
/// the fused two-pass [`highlight_spans`] returns byte-identical output.
/// Because #388 is a pure perf refactor, any divergence is a bug.
#[cfg(test)]
fn highlight_spans_reference(source: &str) -> Vec<EditorSpan> {
    let mut spans: Vec<EditorSpan> = markdown_spans(source)
        .into_iter()
        .filter(|s| {
            !matches!(
                s.kind,
                EditorSpanKind::Link | EditorSpanKind::Image | EditorSpanKind::BlockQuote
            )
        })
        .collect();
    let fm_end = source.len() - crate::frontmatter::body_after_frontmatter(source).len();
    if fm_end > 0 {
        spans.push(EditorSpan {
            start_byte: 0,
            end_byte: fm_end as u32,
            kind: EditorSpanKind::Frontmatter,
        });
    }
    spans.extend(wikilink_spans(source));
    spans.extend(citation_spans(source));
    spans.extend(scan_tags(source));
    spans.extend(scan_comments(source));
    let mut resolved = resolve_overlaps(source, spans);
    resolved.extend(code_internal_spans(source));
    resolved.sort_by_key(|s| s.start_byte);
    resolved
}

/// Resolve overlaps by priority (Swift `covered`-set parity): accept
/// spans highest-priority first, dropping any that intersect an
/// already-accepted span. Returns survivors in document order.
fn resolve_overlaps(source: &str, mut spans: Vec<EditorSpan>) -> Vec<EditorSpan> {
    spans.sort_by(|a, b| {
        priority(&a.kind)
            .cmp(&priority(&b.kind))
            .then(a.start_byte.cmp(&b.start_byte))
    });
    let mut covered = vec![false; source.len()];
    let mut accepted: Vec<EditorSpan> = Vec::with_capacity(spans.len());
    for span in spans {
        let (s, e) = (span.start_byte as usize, span.end_byte as usize);
        if s >= e || e > source.len() {
            continue; // defensive: degenerate or out-of-bounds
        }
        if covered[s..e].iter().any(|&c| c) {
            continue; // intersects a higher-priority span
        }
        covered[s..e].fill(true);
        accepted.push(span);
    }
    accepted.sort_by_key(|s| s.start_byte);
    accepted
}

/// Priority for overlap resolution (lower = wins). Mirrors the append
/// order of the prior Swift `findEditorSyntaxSpans`. `Link` / `Image` /
/// `BlockQuote` are filtered before this runs; they get a low priority
/// only for match exhaustiveness.
fn priority(kind: &EditorSpanKind) -> u8 {
    match kind {
        EditorSpanKind::Frontmatter => 0,
        EditorSpanKind::CodeFence => 1,
        EditorSpanKind::Comment => 2,
        EditorSpanKind::InlineCode => 3,
        EditorSpanKind::Wikilink | EditorSpanKind::Embed => 4,
        EditorSpanKind::Heading(_) => 5,
        EditorSpanKind::Citation => 6,
        EditorSpanKind::Tag => 7,
        EditorSpanKind::Emphasis | EditorSpanKind::Strong | EditorSpanKind::Strikethrough => 8,
        EditorSpanKind::Link | EditorSpanKind::Image | EditorSpanKind::BlockQuote => 9,
        // Overlaid after the sweep, so this is never consulted; present
        // only for match exhaustiveness.
        EditorSpanKind::Code(_) => 10,
    }
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

/// Trim trailing `\r`/`\n` from a span range. `pulldown-cmark` includes
/// the trailing newline in block-level tag ranges (headings, code
/// blocks); the editor highlight wants the visible run only — which
/// also matches the prior Swift behaviour. A no-op for inline spans.
fn trim_trailing_newline(
    source: &str,
    mut range: std::ops::Range<usize>,
) -> std::ops::Range<usize> {
    let bytes = source.as_bytes();
    while range.end > range.start && matches!(bytes[range.end - 1], b'\n' | b'\r') {
        range.end -= 1;
    }
    range
}

fn is_word_char(c: char) -> bool {
    c.is_alphanumeric() || c == '_'
}

fn is_tag_char(c: char) -> bool {
    c.is_alphanumeric() || matches!(c, '_' | '-' | '/')
}

#[cfg(test)]
mod tests {
    use super::*;

    /// Convenience: the source slice a span covers.
    fn slice<'a>(source: &'a str, span: &EditorSpan) -> &'a str {
        &source[span.start_byte as usize..span.end_byte as usize]
    }

    fn first(spans: &[EditorSpan], kind: &EditorSpanKind) -> Option<EditorSpan> {
        spans.iter().find(|s| &s.kind == kind).cloned()
    }

    // --- markdown_spans (layer 1) -------------------------------------

    #[test]
    fn atx_heading_carries_level_and_covers_the_line() {
        let src = "# Title\n\nbody\n";
        let spans = markdown_spans(src);
        let h = first(&spans, &EditorSpanKind::Heading(1)).expect("heading span");
        assert_eq!(slice(src, &h), "# Title");
    }

    #[test]
    fn setext_heading_maps_to_level_two() {
        let src = "Title\n-----\n";
        let spans = markdown_spans(src);
        assert!(
            spans.iter().any(|s| s.kind == EditorSpanKind::Heading(2)),
            "expected a setext H2, got {spans:?}"
        );
    }

    #[test]
    fn strong_and_emphasis_are_distinguished() {
        let src = "a **bold** and *italic* here\n";
        let spans = markdown_spans(src);
        assert_eq!(
            slice(
                src,
                &first(&spans, &EditorSpanKind::Strong).expect("strong")
            ),
            "**bold**"
        );
        assert_eq!(
            slice(
                src,
                &first(&spans, &EditorSpanKind::Emphasis).expect("emphasis")
            ),
            "*italic*"
        );
    }

    #[test]
    fn strikethrough_requires_the_option_and_is_emitted() {
        let src = "~~gone~~\n";
        let spans = markdown_spans(src);
        assert_eq!(
            slice(
                src,
                &first(&spans, &EditorSpanKind::Strikethrough).expect("strike")
            ),
            "~~gone~~"
        );
    }

    #[test]
    fn inline_code_and_fenced_block_are_separate_kinds() {
        let src = "use `x` then\n\n```rust\nlet y = 1;\n```\n";
        let spans = markdown_spans(src);
        assert_eq!(
            slice(
                src,
                &first(&spans, &EditorSpanKind::InlineCode).expect("inline")
            ),
            "`x`"
        );
        let fence = first(&spans, &EditorSpanKind::CodeFence).expect("fence");
        assert!(slice(src, &fence).starts_with("```rust"));
    }

    #[test]
    fn links_and_images_are_classified() {
        let src = "[t](u) and ![a](i)\n";
        let spans = markdown_spans(src);
        assert!(first(&spans, &EditorSpanKind::Link).is_some());
        assert!(first(&spans, &EditorSpanKind::Image).is_some());
    }

    #[test]
    fn empty_source_yields_no_spans() {
        assert!(markdown_spans("").is_empty());
        assert!(highlight_spans("").is_empty());
    }

    #[test]
    fn spans_are_in_document_order() {
        let src = "# H\n\ntext `c` more **b**\n";
        let spans = markdown_spans(src);
        let starts: Vec<u32> = spans.iter().map(|s| s.start_byte).collect();
        let mut sorted = starts.clone();
        sorted.sort_unstable();
        assert_eq!(starts, sorted, "spans should be emitted in document order");
    }

    // --- highlight_spans (Slate tokens + overlap resolution) ----------

    #[test]
    fn wikilink_and_embed_are_distinguished() {
        let src = "see [[Note]] and ![[img.png]] here\n";
        let spans = highlight_spans(src);
        assert_eq!(
            slice(
                src,
                &first(&spans, &EditorSpanKind::Wikilink).expect("wikilink")
            ),
            "[[Note]]"
        );
        assert_eq!(
            slice(src, &first(&spans, &EditorSpanKind::Embed).expect("embed")),
            "![[img.png]]"
        );
    }

    #[test]
    fn bare_hash_is_a_tag_but_word_hash_is_not() {
        let src = "#project here, but a#b is not a tag\n";
        let spans = highlight_spans(src);
        let tags: Vec<&EditorSpan> = spans
            .iter()
            .filter(|s| s.kind == EditorSpanKind::Tag)
            .collect();
        assert_eq!(tags.len(), 1, "exactly one tag, got {tags:?}");
        assert_eq!(slice(src, tags[0]), "#project");
    }

    #[test]
    fn citation_span_covers_the_full_bracket() {
        let src = "as shown [@smith2020] elsewhere\n";
        let spans = highlight_spans(src);
        assert_eq!(
            slice(
                src,
                &first(&spans, &EditorSpanKind::Citation).expect("citation")
            ),
            "[@smith2020]"
        );
    }

    #[test]
    fn tag_inside_fenced_code_is_suppressed() {
        let src = "```\n#nottag inside code\n```\n";
        let spans = highlight_spans(src);
        assert!(
            !spans.iter().any(|s| s.kind == EditorSpanKind::Tag),
            "tag inside a code fence must be masked, got {spans:?}"
        );
    }

    #[test]
    fn comment_masks_inner_tokens() {
        let src = "%% a **b** and [[c]] %%\n";
        let spans = highlight_spans(src);
        let comment = first(&spans, &EditorSpanKind::Comment).expect("comment");
        assert_eq!(slice(src, &comment), "%% a **b** and [[c]] %%");
        assert!(
            !spans
                .iter()
                .any(|s| matches!(s.kind, EditorSpanKind::Strong | EditorSpanKind::Wikilink)),
            "tokens inside a comment must be masked, got {spans:?}"
        );
    }

    #[test]
    fn highlight_spans_are_non_overlapping_and_ordered() {
        let src = "# Title with [[link]]\n\n`code` and #tag and *i* and [@k]\n";
        let spans = highlight_spans(src);
        for w in spans.windows(2) {
            // Code-internal tokens intentionally nest inside CodeFence;
            // every other span is strictly non-overlapping and ordered.
            if matches!(w[0].kind, EditorSpanKind::Code(_))
                || matches!(w[1].kind, EditorSpanKind::Code(_))
            {
                continue;
            }
            assert!(
                w[0].end_byte <= w[1].start_byte,
                "spans overlap or are unordered: {:?} then {:?}",
                w[0],
                w[1]
            );
        }
    }

    // --- code-block internals (#377 slice 3) -------------------------

    #[test]
    fn code_block_internals_get_token_spans_at_host_offsets() {
        let src = "intro\n\n```rust\nlet x = 1;\n```\n";
        let spans = highlight_spans(src);
        // The CodeFence container is present...
        assert!(spans.iter().any(|s| s.kind == EditorSpanKind::CodeFence));
        // ...and per-token Code spans overlay it at correct host offsets.
        let code: Vec<&EditorSpan> = spans
            .iter()
            .filter(|s| matches!(s.kind, EditorSpanKind::Code(_)))
            .collect();
        assert!(
            !code.is_empty(),
            "expected code-internal tokens, got {spans:?}"
        );
        let slices: Vec<&str> = code.iter().map(|s| slice(src, s)).collect();
        assert!(
            slices.contains(&"let"),
            "expected a `let` token at its host offset, got {slices:?}"
        );
    }

    #[test]
    fn code_tokens_nest_inside_the_fence_span() {
        let src = "```rust\nfn f() {}\n```\n";
        let spans = highlight_spans(src);
        let fence = spans
            .iter()
            .find(|s| s.kind == EditorSpanKind::CodeFence)
            .expect("fence");
        for s in spans
            .iter()
            .filter(|s| matches!(s.kind, EditorSpanKind::Code(_)))
        {
            assert!(
                s.start_byte >= fence.start_byte && s.end_byte <= fence.end_byte,
                "code token {s:?} should nest inside fence {fence:?}"
            );
        }
    }

    #[test]
    fn code_tokens_handle_crlf_and_non_ascii_without_panicking() {
        // Red-team #377: pulldown normalizes CRLF→LF, so token offsets
        // must map through per-line segments, not one global base.
        let src = "```rust\r\nlet a = 1;\r\nlet café = 2;\r\n```\r\n";
        let spans = highlight_spans(src);
        let slices: Vec<&str> = spans
            .iter()
            .filter(|s| matches!(s.kind, EditorSpanKind::Code(_)))
            .map(|s| slice(src, s)) // panics if a span isn't a char boundary
            .collect();
        assert!(
            slices.contains(&"café"),
            "the café identifier should map to its real host bytes, got {slices:?}"
        );
    }

    #[test]
    fn indented_code_block_tokens_are_char_boundary_safe() {
        let src = "intro\n\n    let x = \"中\";\n    let y = 2;\n";
        let spans = highlight_spans(src);
        for s in spans
            .iter()
            .filter(|s| matches!(s.kind, EditorSpanKind::Code(_)))
        {
            let _ = slice(src, s); // must be sliceable — no panic
            assert!(
                src.is_char_boundary(s.start_byte as usize)
                    && src.is_char_boundary(s.end_byte as usize),
                "code span {s:?} is not on char boundaries"
            );
        }
    }

    #[test]
    fn unterminated_comment_is_not_emitted() {
        let src = "text %% open but never closed\n";
        let spans = highlight_spans(src);
        assert!(!spans.iter().any(|s| s.kind == EditorSpanKind::Comment));
    }

    // --- frontmatter (#384) ------------------------------------------

    #[test]
    fn frontmatter_emits_one_span_and_masks_internals() {
        // Red-team #384: `title: Hello` + the closing `---` were mis-read
        // as a setext heading by the raw-source pulldown pass.
        let src = "---\ntitle: Hello\nstatus: draft\n---\n\nbody text\n";
        let spans = highlight_spans(src);
        let fm: Vec<&EditorSpan> = spans
            .iter()
            .filter(|s| s.kind == EditorSpanKind::Frontmatter)
            .collect();
        assert_eq!(fm.len(), 1, "exactly one frontmatter span, got {spans:?}");
        assert_eq!(fm[0].start_byte, 0);
        let fm_end = fm[0].end_byte;
        assert!(
            !spans
                .iter()
                .any(|s| s.kind != EditorSpanKind::Frontmatter && s.start_byte < fm_end),
            "no non-frontmatter span may start inside the frontmatter block, got {spans:?}"
        );
    }

    #[test]
    fn frontmatter_does_not_mask_body_tokens() {
        let src = "---\ntitle: T\n---\n\nbody [[Link]] and **bold**\n";
        let spans = highlight_spans(src);
        let fm_end = spans
            .iter()
            .find(|s| s.kind == EditorSpanKind::Frontmatter)
            .expect("fm")
            .end_byte;
        let wl = spans
            .iter()
            .find(|s| s.kind == EditorSpanKind::Wikilink)
            .expect("body wikilink");
        let strong = spans
            .iter()
            .find(|s| s.kind == EditorSpanKind::Strong)
            .expect("body bold");
        assert!(
            wl.start_byte >= fm_end && strong.start_byte >= fm_end,
            "body tokens must survive outside frontmatter, got {spans:?}"
        );
    }

    // --- Unicode-aware tag scanner (#385) -----------------------------

    #[test]
    fn unicode_tag_body_is_not_truncated() {
        let src = "a #café and #naïve here\n";
        let tags: Vec<String> = highlight_spans(src)
            .iter()
            .filter(|s| s.kind == EditorSpanKind::Tag)
            .map(|s| slice(src, s).to_string())
            .collect();
        assert!(tags.contains(&"#café".to_string()), "got {tags:?}");
        assert!(tags.contains(&"#naïve".to_string()), "got {tags:?}");
    }

    #[test]
    fn tag_after_non_ascii_word_is_suppressed() {
        // `café#x`: the `#` is preceded by `é` (a word char), so — like
        // Swift's Unicode `(?<!\w)` — it is not a tag.
        let src = "café#x is not a tag\n";
        let spans = highlight_spans(src);
        assert!(
            !spans.iter().any(|s| s.kind == EditorSpanKind::Tag),
            "tag after a non-ASCII word must be suppressed, got {spans:?}"
        );
    }

    #[test]
    fn consolidated_matches_reference_exhaustive() {
        // #388 differential arbiter, deterministic: the fused two-pass
        // `highlight_spans` must equal the four-pass `highlight_spans_reference`
        // on every 1–3 line doc over an alphabet that stresses both fused
        // passes — the code-internal segment mapping (fenced / indented /
        // CRLF / multibyte) and the shared body code-ranges (inline code +
        // strikethrough next to wikilinks/citations), plus list nesting.
        let alphabet = [
            "",
            "# H",
            "```rust",
            "let x = 1;",
            "```",
            "    indented code",
            "\tlet y = 2;",
            "prose [[L]] and [@k]",
            "`inline [[no]]` and ~~x [@no]~~",
            "- a",
            "中 café 😀 [[Ω]]",
            "text\r", // becomes CRLF when joined with '\n'
            "~~~",
            "%% c %%",
        ];
        let mut docs = 0usize;
        for &a in &alphabet {
            for &b in &alphabet {
                for &c in &alphabet {
                    let doc = format!("{a}\n{b}\n{c}\n");
                    assert_eq!(
                        highlight_spans(&doc),
                        highlight_spans_reference(&doc),
                        "fused != reference for {doc:?}"
                    );
                    docs += 1;
                }
            }
        }
        assert_eq!(docs, 14 * 14 * 14);
    }

    // --- Range-scoped highlighting (#379) -----------------------------

    /// Sort key giving a total order over spans (Code(_) tokens nest in
    /// CodeFence, so (start,end) can tie — break ties on the kind's debug
    /// form so the comparison is deterministic).
    fn span_sort_key(s: &EditorSpan) -> (u32, u32, String) {
        (s.start_byte, s.end_byte, format!("{:?}", s.kind))
    }

    /// The load-bearing invariant: `highlight_spans_in_range`'s spans
    /// equal exactly the whole-document spans that fall within its
    /// `applied_range` (a fallback is the whole document, so the same
    /// assertion covers it).
    fn assert_ranged_matches_whole(source: &str, dirty: std::ops::Range<usize>) {
        let ranged = highlight_spans_in_range(source, dirty.clone());
        let (a, b) = (ranged.applied_range.start, ranged.applied_range.end);
        let mut expected: Vec<EditorSpan> = highlight_spans(source)
            .into_iter()
            .filter(|s| (s.start_byte as usize) >= a && (s.end_byte as usize) <= b)
            .collect();
        let mut got = ranged.spans.clone();
        expected.sort_by_key(span_sort_key);
        got.sort_by_key(span_sort_key);
        assert_eq!(
            got, expected,
            "ranged != whole-doc slice; dirty={dirty:?} applied={:?}\nsource={source:?}",
            ranged.applied_range
        );
    }

    fn is_fallback(source: &str, dirty: std::ops::Range<usize>) -> bool {
        highlight_spans_in_range(source, dirty).applied_range == (0..source.len())
    }

    #[test]
    fn ordinary_prose_edit_windows_and_matches() {
        let src = "# Title\n\nFirst para with [[Link]] and #tag.\n\nSecond para with **bold** word.\n\nThird para here.\n";
        // Edit in the second paragraph (well away from any construct).
        let at = src.find("bold").unwrap();
        let r = highlight_spans_in_range(src, at..at + 4);
        assert!(
            r.applied_range != (0..src.len()),
            "an ordinary prose edit should window, not fall back: {:?}",
            r.applied_range
        );
        assert_ranged_matches_whole(src, at..at + 4);
    }

    #[test]
    fn dashes_line_at_window_head_falls_back() {
        // A `---` block mid-document with a wikilink inside it: a window
        // starting on the `---` would be re-read as frontmatter and DROP
        // the wikilink. Must fall back. (The CRITICAL the plan red-team
        // found.)
        let src = "intro paragraph\n\n---\nnot: yaml [[Trap]]\n---\n\noutro\n";
        let trap = src.find("Trap").unwrap();
        assert!(
            is_fallback(src, trap..trap + 4),
            "edit inside a mid-doc --- block must fall back"
        );
        assert_ranged_matches_whole(src, trap..trap + 4);
    }

    #[test]
    fn edit_inside_a_blank_bounded_fence_matches() {
        // The whole fence is blank-bounded, so the window captures it and
        // parses it correctly — a win, no fallback needed.
        let src = "prose\n\n```rust\nlet x = 1;\nlet y = 2;\n```\n\nmore prose\n";
        let inside = src.find("y = 2").unwrap();
        assert_ranged_matches_whole(src, inside..inside + 1);
    }

    #[test]
    fn edit_in_a_fence_with_an_internal_blank_falls_back() {
        // An internal blank line stops blank-extension inside the fence,
        // so the window would cut the code block → fall back.
        let src = "prose\n\n```rust\nfn a() {}\n\nfn b() {}\n```\n\ntail\n";
        let inside = src.find("fn b").unwrap();
        assert!(
            is_fallback(src, inside..inside + 1),
            "a window cutting a code block must fall back"
        );
        assert_ranged_matches_whole(src, inside..inside + 1);
    }

    #[test]
    fn typing_a_fence_delimiter_recolors_correctly() {
        // Acceptance (b): typing/editing a ``` must recolor correctly,
        // whether the new fence is captured by the window or straddles it
        // (→ whole-doc fallback). The invariant holds either way.
        for src in [
            "alpha\n\n```\ncode\n```\n\nbeta\n", // blank-bounded → window
            "alpha\n\n```\nnow fenced [[L]] #tag\nmore\n", // unclosed → window to EOF
            "a\n\n```\nx\n\ny\n```\n\nb\n",      // internal blank → straddle → fallback
        ] {
            let fence = src.find("```").unwrap();
            assert_ranged_matches_whole(src, fence..fence + 3);
        }
    }

    #[test]
    fn fence_inside_blockquote_or_indented_never_windows_wrong() {
        // A `> ```` fence (blockquote-nested) and a 4-space-indented ```:
        // whichever the conservative scan does, the result must still
        // match the whole document.
        let bq = "para\n\n> ```\n> code here\n> ```\n\nafter [[L]] text\n";
        let at = bq.find("after").unwrap();
        assert_ranged_matches_whole(bq, at..at + 1);
        let indented = "para\n\n    ```\n    literal\n\ntail [[L]] here\n";
        let at2 = indented.find("tail").unwrap();
        assert_ranged_matches_whole(indented, at2..at2 + 1);
    }

    #[test]
    fn frontmatter_content_and_delimiter_edits_match() {
        let src = "---\ntitle: Hello\nstatus: draft\n---\n\nbody [[Link]] here\n";
        // Edit YAML content.
        let val = src.find("Hello").unwrap();
        assert!(
            is_fallback(src, val..val + 5),
            "frontmatter region edit falls back (conservative V1)"
        );
        assert_ranged_matches_whole(src, val..val + 5);
        // Edit the closing delimiter.
        let close = src.rfind("---").unwrap();
        assert_ranged_matches_whole(src, close..close + 3);
        // An edit in the body still windows + matches.
        let body = src.find("here").unwrap();
        assert_ranged_matches_whole(src, body..body + 4);
    }

    #[test]
    fn comment_open_close_and_inside_match() {
        let src = "before\n\n%% a note with [[L]] %%\n\nafter\n";
        let inside = src.find("note").unwrap();
        assert!(is_fallback(src, inside..inside + 4));
        assert_ranged_matches_whole(src, inside..inside + 4);
    }

    #[test]
    fn blank_line_delete_merging_paragraphs_matches() {
        // The window is computed on the post-edit source; merging two
        // paragraphs must still reconstruct correctly.
        let merged = "one para line\nNOW MERGED second line with [[L]]\n\ntail\n";
        let at = merged.find("MERGED").unwrap();
        assert_ranged_matches_whole(merged, at..at + 6);
    }

    #[test]
    fn setext_under_multiline_paragraph_matches() {
        let src = "intro\n\nHeading line one\ncontinued line two\n=====\n\nbody\n";
        let at = src.find("=====").unwrap();
        assert_ranged_matches_whole(src, at..at + 5);
    }

    #[test]
    fn multibyte_and_crlf_window_edges_match_without_panic() {
        let mb = "a中b\n\n😀 para with é and [[Lïnk]]\n\nend\n";
        for i in 0..mb.len() {
            // dirty at every byte (incl. mid-scalar) must clamp + match.
            assert_ranged_matches_whole(mb, i..i + 1);
        }
        let crlf = "para one\r\n\r\nsecond [[L]] para\r\n\r\nthird\r\n";
        let at = crlf.find("second").unwrap();
        assert_ranged_matches_whole(crlf, at..at + 1);
    }

    #[test]
    fn edge_cases_clamp_and_match() {
        for (src, dirty) in [
            ("", 0..0),
            ("hello\n", 0..0),
            ("hello\n", 5..5),
            ("hello\n", 3..999), // past EOF
            ("a\n\nb\n", 0..5),  // whole tiny doc
        ] {
            assert_ranged_matches_whole(src, dirty);
        }
    }

    #[test]
    fn unclosed_html_block_above_window_falls_back() {
        // #379 review, CRITICAL #1. `<!--` opens a CommonMark HTML block
        // (type 2) that a blank line does NOT close, so in the whole-doc
        // parse the `` ``` `` and `#tag` below are inert HTML text (no
        // CodeFence; the tag scanner still fires). Parsed alone the window
        // would read `` ``` `` as a real fence that masks the tag. The
        // opaque-block guard must see the HTML block straddle and fall back.
        let src = "<!-- x\n\n```\n#tag\n";
        let dirty = src.find("#tag").unwrap();
        assert!(
            is_fallback(src, dirty..dirty),
            "a window straddling an unclosed HTML block must fall back"
        );
        assert_ranged_matches_whole(src, dirty..dirty);
        // The whole HTML-block opener family, each with an edit below it.
        for opener in [
            "<!-- c",
            "<script>",
            "<style>",
            "<pre>",
            "<?php",
            "<![CDATA[",
        ] {
            let src = format!("{opener}\n\n```\n#tag here\n");
            let dirty = src.find("#tag").unwrap();
            assert_ranged_matches_whole(&src, dirty..dirty);
        }
    }

    #[test]
    fn exhaustive_small_docs_over_breaker_alphabet_match() {
        // Deterministic, COMPLETE coverage of the small-doc space where the
        // #379-review CRITICALs lived: every 1–3 line document over a
        // curated alphabet of the breakers (blank lines, `---`, both fence
        // kinds, an HTML-block opener + closer, a wikilink/blockquote line,
        // a list marker + an indented continuation, `#tag`, `%%`), checked
        // at every dirty byte. This is the red-team's own finding
        // methodology, frozen as a regression guard — all three CRITICALs
        // (HTML block, frontmatter fence parity, list-continuation indented
        // code) would have failed it.
        let alphabet = [
            "",
            "---",
            "```",
            "~~~",
            "<!--",
            "-->",
            "x [[L]]",
            "#t",
            "%% c %%",
            "> [[Q]]",
            "- a",
            "    [[L]]",
            "  ```",
            "  x\n===", // 2-col list-content line + setext underline (#5)
            "\n\t```",  // a blank + a tab fence → loose-list double-blank head (#6)
        ];
        let mut checked = 0usize;
        let mut docs = Vec::new();
        for &a in &alphabet {
            docs.push(format!("{a}\n"));
            for &b in &alphabet {
                docs.push(format!("{a}\n{b}\n"));
                for &c in &alphabet {
                    docs.push(format!("{a}\n{b}\n{c}\n"));
                }
            }
        }
        for doc in &docs {
            for d in 0..=doc.len() {
                assert_ranged_matches_whole(doc, d..d);
                checked += 1;
            }
        }
        // 15 + 225 + 3375 = 3615 docs; guard the loop actually ran.
        assert_eq!(docs.len(), 3615);
        assert!(
            checked > 25_000,
            "expected a full per-byte sweep, ran {checked}"
        );
    }

    #[test]
    fn frontmatter_unpairs_a_body_fence_and_window_falls_back() {
        // #379 review, CRITICAL #2. `---\n~~~\n---` is frontmatter, so the
        // link/citation extractors parse the body `~~~\n\n> q [[Q]]\n`,
        // where `~~~` is an UNCLOSED fence that swallows the wikilink. The
        // raw parse instead sees `~~~…~~~` as a *closed* fence and the
        // blockquote as top-level (wikilink present). The body-framing
        // opaque-block check must catch the straddle the raw check misses.
        let src = "---\n~~~\n---\n~~~\n\n> q [[Q]]\n";
        let dirty = src.find("[[Q]]").unwrap();
        assert!(
            is_fallback(src, dirty..dirty),
            "a window inside a body fence the frontmatter strip un-paired must fall back"
        );
        assert_ranged_matches_whole(src, dirty..dirty);
    }

    #[test]
    fn list_continuation_indented_line_does_not_invent_code() {
        // #379 review, CRITICAL #3. An indented line under a loose list is
        // a list-item continuation in context (a blank line doesn't close
        // the list), so the whole-doc parse has NO code block there; parsed
        // alone the window would read it as a top-level indented code block
        // and fabricate a CodeFence + tree-sitter tokens. Must fall back.
        for src in [
            "- a\n\n    code\n",
            "1. a\n\n    code [[L]]\n",
            "- a\n\n    ```\n    body\n    ```\n",
            "- a\n  - b\n\n        code\n", // nested list, 8-indent
            "- a\n\n    <div>x</div>\n",
            "# Setup\n\n1. Install.\n2. Build:\n\n        cargo build\n\n3. Done.\n",
        ] {
            let dirty = src.rfind("    ").unwrap() + 4; // inside the indented line
            assert!(
                is_fallback(src, dirty..dirty),
                "a list-continuation indented line must fall back, not invent code: {src:?}"
            );
            assert_ranged_matches_whole(src, dirty..dirty);
        }
        // Genuine *top-level* indented code (no list/quote container) is NOT
        // container content, so it windows — its block reproduces exactly in
        // isolation (the structure check confirms), no over-fallback.
        let genuine = "para text here\n\n    real_code()\n\nmore prose follows\n";
        let at = genuine.find("real_code").unwrap();
        assert!(
            !is_fallback(genuine, at..at),
            "genuine top-level indented code should window"
        );
        assert_ranged_matches_whole(genuine, at..at);
        // A 2-space indent is NOT indent-code-shaped, so an ordinary
        // lightly-indented prose line still windows (no over-fallback creep).
        let shallow = "intro line\n\n  two-space [[L]] line\n\ntail line\n";
        let at = shallow.find("two-space").unwrap();
        assert!(
            !is_fallback(shallow, at..at),
            "2-space indent must still window"
        );
        assert_ranged_matches_whole(shallow, at..at);
    }

    #[test]
    fn list_content_paragraph_window_does_not_invent_setext_heading() {
        // #379 review, CRITICAL #5 — the setext analogue of #3. A 2–3-column
        // line under a loose list is list-item content, so a following
        // `===`/`---` is also list content, NOT a setext underline → no
        // heading. The isolated window loses the list opener, so the line
        // becomes a top-level paragraph and the underline invents a Heading.
        // Invisible to the opaque-block check (a Heading is not a code/HTML
        // block); caught by the lost-container guard.
        for src in [
            "- a\n\n  x\n===\n",
            "- a\n\n  x\n-----\n",
            "1. a\n\n   x\n===\n",
            "* a\n\n  hi [[L]]\n===\n",
            "---\nt: x\n---\n\n- a\n\n  y\n===\n", // body framing carries the list too
        ] {
            let at = src.rfind("\n=").or_else(|| src.rfind("\n-")).unwrap();
            assert_ranged_matches_whole(src, at..at);
        }
    }

    #[test]
    fn list_content_fence_window_does_not_grow_past_the_list() {
        // #379 review, CRITICAL #4 — the GROW dual of #3. A 2–3-column
        // indented fence is list-item content; the dedented line below ends
        // the list (and the unclosed fence) in the whole document, but the
        // window — which loses the list opener above the blank — lets the
        // fence swallow that line (a heading/wikilink gets recolored as code
        // and dropped). Sub-4 indent dodges any head-indent gate, and the
        // whole-doc block is window-CONTAINED so a straddle test stays
        // silent; only structure equality catches the grow.
        for src in [
            "- a\n\n  ```\n- b\n",
            "- a\n\n  ```\n# h\n", // heading masked
            "1. a\n\n   ```\n2. b\n",
            "- Step one.\n\n  ```\n  cargo build\n## Done [[Wrap]]\n", // wikilink dropped
            "> q\n\n  ~~~\n# h\n",                                     // blockquote content fence
        ] {
            let at = src.find("```").or_else(|| src.find("~~~")).unwrap() + 3;
            assert_ranged_matches_whole(src, at..at);
        }
    }

    #[test]
    fn window_opening_on_a_loose_list_blank_does_not_invent_code_or_heading() {
        // #379 review, CRITICAL #6 (+ its setext sibling). A loose list puts
        // blank lines between items, so an edit on the blank makes the window
        // OPEN on a blank line with the indented list content just below it —
        // `win_start` is the blank, so a head check at `win_start` alone is
        // blind to the indented continuation. A tab/4-space `` ``` `` there is
        // an empty Fenced block (no inner token) in the document but a
        // top-level Indented block (invented tree-sitter `Code`) in the
        // window — same byte range, so only a KIND-aware block compare or the
        // first-non-blank container check catches it. The `===` variant
        // invents a setext heading the same way.
        for src in [
            "- a\n\n\n\t```\n",          // tab fence after a double blank
            "- a\n\n\n    ```\n",        // 4-space fence after a double blank
            "- a\n\n\n\t```\ncode\n",    // non-empty: tree-sitter content also flips
            "- a\n\n\n  x\n===\n",       // setext sibling: invented Heading via blank head
            "1. a\n\n\n    fn x() {}\n", // ordered list, invented indented code
        ] {
            for d in 0..src.len() {
                assert_ranged_matches_whole(src, d..d);
            }
        }
    }
}

#[cfg(test)]
mod range_proptests {
    use super::*;
    use proptest::prelude::*;

    /// Generate `source` rich in the shapes that break naive windowing —
    /// `---` lines, `[[wikilinks]]` / `[@cites]` mid-document, `` ``` ``
    /// fences, `%%` comments, headings, blanks — then assert
    /// `highlight_spans_in_range` for an arbitrary dirty range always
    /// equals the whole-document spans within its `applied_range`. This is
    /// the arbiter of the substring-parse-equivalence invariant.
    fn liney_source() -> impl Strategy<Value = String> {
        let line = prop_oneof![
            Just("".to_string()),
            Just("---".to_string()),
            Just("```".to_string()),
            Just("```rust".to_string()),
            Just("~~~".to_string()),
            Just("# Heading".to_string()),
            Just("prose with [[Link]] here".to_string()),
            Just("cite [@key2020] inline".to_string()),
            Just("bare @key2020 cite".to_string()),
            Just("%% a comment %%".to_string()),
            Just("%%".to_string()),
            Just("a #tag and **bold**".to_string()),
            Just("    indented line".to_string()),
            Just("    [[L]] indented".to_string()),
            Just("\ttab indented".to_string()),
            // A 2-space-indented prose line — list-item content that, paired
            // with a list marker + blank + a `===`/`---` underline, forms the
            // "invent a setext heading" shape (#379 review, CRITICAL #5).
            Just("  two col [[L]]".to_string()),
            // Inline code + strikethrough next to a wikilink/citation — the
            // #388 body fusion shares one `collect_code_ranges` (default
            // opts) between wikilinks and citations, where the link
            // extractor previously used strikethrough-enabled parsing; this
            // stresses that the code-range suppression agrees regardless.
            Just("`code [[L]]` and ~~struck [@k]~~".to_string()),
            // Indented fence lines — under a list marker these are empty
            // Fenced blocks whole-doc but top-level Indented code in a window,
            // flipping the block kind at the same range (#379 review,
            // CRITICAL #6).
            Just("    ```".to_string()),
            Just("\t```".to_string()),
            // A 2-space-indented fence — list-item content that, paired with
            // a list marker + blank, forms the "grow" shape (#379 review,
            // CRITICAL #4): the window loses the list opener and lets the
            // fence swallow the dedented line.
            Just("  ```".to_string()),
            Just("> quoted [[Q]]".to_string()),
            // List markers — paired with the indented lines above, these
            // generate the loose-list + indented-continuation shape whose
            // window would invent an indented code block (#379 review,
            // CRITICAL #3).
            Just("- item".to_string()),
            Just("1. step".to_string()),
            Just("=====".to_string()),
            Just("===".to_string()),
            Just("plain text line".to_string()),
            // HTML-block openers (CommonMark types 1–5 aren't closed by a
            // blank line) + a closer, so the generator can both open an
            // opaque HTML block above an edit and close one (#379 review).
            Just("<!-- comment".to_string()),
            Just("-->".to_string()),
            Just("<script>".to_string()),
            Just("<style>".to_string()),
            Just("<pre>".to_string()),
            Just("<div>".to_string()),
        ];
        proptest::collection::vec(line, 0..16).prop_map(|lines| {
            let mut s = lines.join("\n");
            s.push('\n');
            s
        })
    }

    /// Prepend a YAML frontmatter block to a body. The frontmatter strip
    /// re-anchors the body parse at byte 0 of the body, which can flip the
    /// fence/HTML parity of everything below it relative to the raw parse
    /// — the CRITICAL #2 shape (#379 review). The interior lines include a
    /// lone `~~~`/`` ``` `` so a body fence can be left unclosed.
    fn frontmatter_prefixed_source() -> impl Strategy<Value = String> {
        let fm_line = prop_oneof![
            Just("title: x".to_string()),
            Just("~~~".to_string()),
            Just("```".to_string()),
            Just("tags: [a, b]".to_string()),
            Just("".to_string()),
        ];
        (proptest::collection::vec(fm_line, 0..4), liney_source())
            .prop_map(|(fm, body)| format!("---\n{}\n---\n{body}", fm.join("\n")))
    }

    proptest! {
        #![proptest_config(ProptestConfig::with_cases(600))]

        #[test]
        fn ranged_always_matches_whole_doc_slice(src in liney_source(), a in 0usize..400, b in 0usize..400) {
            assert_ranged_invariant(&src, a, b)?;
        }

        #[test]
        fn ranged_matches_with_frontmatter_prefix(src in frontmatter_prefixed_source(), a in 0usize..400, b in 0usize..400) {
            assert_ranged_invariant(&src, a, b)?;
        }

        /// #388: the fused two-pass `highlight_spans` must return
        /// byte-identical output to the pre-refactor four-pass
        /// `highlight_spans_reference` — a pure perf refactor, so any
        /// divergence (incl. the body code-range equivalence between the
        /// shared `collect_code_ranges` and the link extractor's
        /// strikethrough-enabled pass) is a bug.
        #[test]
        fn consolidated_matches_reference(src in liney_source()) {
            prop_assert_eq!(highlight_spans(&src), highlight_spans_reference(&src), "src={:?}", src);
        }

        #[test]
        fn consolidated_matches_reference_with_frontmatter(src in frontmatter_prefixed_source()) {
            prop_assert_eq!(highlight_spans(&src), highlight_spans_reference(&src), "src={:?}", src);
        }
    }

    /// A keystroke-shaped insertion/deletion, biased toward the structural
    /// breakers (fences, `---`/`===`, `%%`, HTML openers/closers, blanks,
    /// indents) that flip block classification.
    fn edit_insert() -> impl Strategy<Value = String> {
        prop_oneof![
            Just(String::new()),
            Just("x".to_string()),
            Just("\n".to_string()),
            Just("\n\n".to_string()),
            Just("```\n".to_string()),
            Just("```rust\n".to_string()),
            Just("~~~\n".to_string()),
            Just("---\n".to_string()),
            Just("===\n".to_string()),
            Just("%%".to_string()),
            Just("%% c %%".to_string()),
            Just("<!-- ".to_string()),
            Just("-->\n".to_string()),
            Just("<div>\n".to_string()),
            Just("# H\n".to_string()),
            Just("- item\n".to_string()),
            Just("    indented\n".to_string()),
            Just("> quote\n".to_string()),
            Just("[[L]] and [@k]".to_string()),
        ]
    }

    /// Snap `byte` up to the next char boundary of `s` (clamped to the end).
    fn snap_up(s: &str, byte: usize) -> usize {
        let mut b = byte.min(s.len());
        while b < s.len() && !s.is_char_boundary(b) {
            b += 1;
        }
        b
    }

    proptest! {
        #![proptest_config(ProptestConfig::with_cases(500))]

        /// #404 Slice B census: maintain a `StructureSnapshot` incrementally
        /// across a SEQUENCE of edits (carrying the incremental result
        /// forward, so errors compound) and assert it stays byte-identical to
        /// a from-scratch `from_source` after every edit. This is the gate on
        /// the clean-break / suffix-reparse / splice machinery — a divergence
        /// is a structure bug that would mis-window a keystroke.
        #[test]
        fn incremental_structure_matches_from_source_over_a_sequence(
            seed in liney_source(),
            edits in prop::collection::vec((0usize..=100, 0usize..6, edit_insert()), 1..10),
        ) {
            let mut source = seed;
            let mut structure = StructureSnapshot::from_source(&source);
            for (pct, del, ins) in edits {
                let pos = snap_up(&source, source.len() * pct / 100);
                let del_end = snap_up(&source, pos + del);
                let new_source = format!("{}{}{}", &source[..pos], ins, &source[del_end..]);
                let incremental = structure.updated(&new_source, pos);
                let from_scratch = StructureSnapshot::from_source(&new_source);
                prop_assert_eq!(
                    &incremental, &from_scratch,
                    "incremental != from_source\n edit: pos={} del={}..{} ins={:?}\n old={:?}\n new={:?}",
                    pos, pos, del_end, ins, source, new_source
                );
                source = new_source;
                structure = incremental;
            }
        }

        /// The same census seeded with frontmatter — the raw structure must
        /// still match (the buffer keeps a separate body-framing snapshot;
        /// this gates the raw one through frontmatter-bearing edits).
        #[test]
        fn incremental_structure_matches_with_frontmatter(
            seed in frontmatter_prefixed_source(),
            edits in prop::collection::vec((0usize..=100, 0usize..6, edit_insert()), 1..10),
        ) {
            let mut source = seed;
            let mut structure = StructureSnapshot::from_source(&source);
            for (pct, del, ins) in edits {
                let pos = snap_up(&source, source.len() * pct / 100);
                let del_end = snap_up(&source, pos + del);
                let new_source = format!("{}{}{}", &source[..pos], ins, &source[del_end..]);
                let incremental = structure.updated(&new_source, pos);
                prop_assert_eq!(
                    &incremental, &StructureSnapshot::from_source(&new_source),
                    "incremental != from_source (fm)\n edit: pos={} del={}..{} ins={:?}\n old={:?}\n new={:?}",
                    pos, pos, del_end, ins, source, new_source
                );
                source = new_source;
                structure = incremental;
            }
        }
    }

    /// The invariant both proptests assert: `highlight_spans_in_range` for
    /// an arbitrary dirty range equals the whole-document spans within its
    /// `applied_range` (a Code-kind-aware sort makes the comparison stable
    /// across the one intentional CodeFence/Code overlap).
    fn assert_ranged_invariant(src: &str, a: usize, b: usize) -> Result<(), TestCaseError> {
        let dirty = a.min(b)..a.max(b);
        let ranged = highlight_spans_in_range(src, dirty);
        let (lo, hi) = (ranged.applied_range.start, ranged.applied_range.end);
        let mut expected: Vec<EditorSpan> = highlight_spans(src)
            .into_iter()
            .filter(|s| (s.start_byte as usize) >= lo && (s.end_byte as usize) <= hi)
            .collect();
        let mut got = ranged.spans;
        expected.sort_by_key(|s| (s.start_byte, s.end_byte, format!("{:?}", s.kind)));
        got.sort_by_key(|s| (s.start_byte, s.end_byte, format!("{:?}", s.kind)));
        prop_assert_eq!(got, expected, "applied={}..{} src={:?}", lo, hi, src);
        Ok(())
    }
}
