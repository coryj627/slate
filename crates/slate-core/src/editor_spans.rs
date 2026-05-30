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
//! 1. **Markdown structure** (this layer): walk the same `pulldown-cmark`
//!    parse that already feeds the render view and the link/task/block
//!    extractors, emitting spans for headings, emphasis, inline code,
//!    fenced blocks, links, etc.
//! 2. **Slate-specific tokens** (`[[wikilink]]`, `![[embed]]`, `#tag`,
//!    `[@cite]`, `%%comment%%`): folded in from the existing custom
//!    extractors — added in a later commit.
//! 3. **Fenced-code internals**: reuse [`crate::code`]'s tree-sitter
//!    token stream — added in a later commit.
//! 4. **Ranged `highlights(in: range)`** exposed over FFI — later.
//!
//! ## Coordinates
//!
//! All offsets are **UTF-8 byte offsets** into the host source, the same
//! space [`crate::code::SyntaxToken`] and [`crate::code::CodeBlock`] use.
//! The Swift consumer converts to a UTF-16 `NSRange` at the boundary (it
//! already performs byte↔UTF-16 conversion for cursor placement).

use pulldown_cmark::{Event, HeadingLevel, Options, Parser, Tag};

/// Classifies one editor span. This commit populates the
/// markdown-structure kinds; Slate-specific kinds and an inner-code
/// kind are added by their respective layers (see module docs).
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
/// - Slate-specific tokens (`[[…]]`, `![[…]]`, `#tag`, `[@cite]`,
///   `%%…%%`) and code-block internals are **not** emitted here; they
///   compose on top in later layers.
pub fn markdown_spans(source: &str) -> Vec<EditorSpan> {
    let mut out: Vec<EditorSpan> = Vec::new();
    let parser =
        Parser::new_ext(source, Options::ENABLE_STRIKETHROUGH).into_offset_iter();
    for (event, range) in parser {
        let kind = match event {
            Event::Start(Tag::Heading { level, .. }) => {
                EditorSpanKind::Heading(heading_level(level))
            }
            Event::Start(Tag::Emphasis) => EditorSpanKind::Emphasis,
            Event::Start(Tag::Strong) => EditorSpanKind::Strong,
            Event::Start(Tag::Strikethrough) => EditorSpanKind::Strikethrough,
            Event::Code(_) => EditorSpanKind::InlineCode,
            Event::Start(Tag::CodeBlock(_)) => EditorSpanKind::CodeFence,
            Event::Start(Tag::Link { .. }) => EditorSpanKind::Link,
            Event::Start(Tag::Image { .. }) => EditorSpanKind::Image,
            Event::Start(Tag::BlockQuote) => EditorSpanKind::BlockQuote,
            _ => continue,
        };
        out.push(EditorSpan::new(trim_trailing_newline(source, range), kind));
    }
    out
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
            slice(src, &first(&spans, &EditorSpanKind::Strong).expect("strong")),
            "**bold**"
        );
        assert_eq!(
            slice(src, &first(&spans, &EditorSpanKind::Emphasis).expect("emphasis")),
            "*italic*"
        );
    }

    #[test]
    fn strikethrough_requires_the_option_and_is_emitted() {
        let src = "~~gone~~\n";
        let spans = markdown_spans(src);
        assert_eq!(
            slice(src, &first(&spans, &EditorSpanKind::Strikethrough).expect("strike")),
            "~~gone~~"
        );
    }

    #[test]
    fn inline_code_and_fenced_block_are_separate_kinds() {
        let src = "use `x` then\n\n```rust\nlet y = 1;\n```\n";
        let spans = markdown_spans(src);
        assert_eq!(
            slice(src, &first(&spans, &EditorSpanKind::InlineCode).expect("inline")),
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
}
