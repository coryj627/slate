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
//! 3. **Fenced-code internals**: reuse [`crate::code`]'s tree-sitter
//!    token stream — *added in a later commit*.
//! 4. **Ranged `highlights(in: range)`** exposed over FFI — *later*.
//!
//! ## Coordinates
//!
//! All offsets are **UTF-8 byte offsets** into the host source, the same
//! space [`crate::code::SyntaxToken`] and [`crate::code::CodeBlock`] use.
//! The Swift consumer converts to a UTF-16 `NSRange` at the boundary (it
//! already performs byte↔UTF-16 conversion for cursor placement).

use crate::citations::extract_citations;
use crate::links::{extract_links, LinkKind};
use pulldown_cmark::{Event, HeadingLevel, Options, Parser, Tag};

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
/// structure plus the Slate-specific tokens, with overlaps resolved by
/// priority into a flat, non-overlapping, document-ordered list ready
/// for the UI to stamp.
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
    let mut spans: Vec<EditorSpan> = markdown_spans(source)
        .into_iter()
        .filter(|s| {
            !matches!(
                s.kind,
                EditorSpanKind::Link | EditorSpanKind::Image | EditorSpanKind::BlockQuote
            )
        })
        .collect();
    // #384: emit one high-priority span over the YAML frontmatter block.
    // `markdown_spans` runs pulldown on the raw source, so it produces
    // spurious structure spans (Heading/CodeFence) over the YAML; the
    // overlap sweep masks them — and any tag/comment the scanners find
    // inside frontmatter — beneath this Frontmatter span.
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
    resolve_overlaps(source, spans)
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

/// Wikilink / embed spans, reused from the canonical link extractor
/// (which already spans the full `[[` … `]]` syntax and suppresses
/// matches inside code spans). Markdown links/images are skipped here —
/// they come from [`markdown_spans`].
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

/// Scan inline `#tag`s: `#` + ASCII letter + `[A-Za-z0-9_/-]*`, not
/// preceded by a word byte or another `#` (so `# heading`, `##`, and
/// `word#x` don't match). Rust's `regex` has no lookbehind, so the
/// preceding-char guard is explicit. Suppression inside code/comments
/// is handled by the overlap sweep, not here.
fn scan_tags(source: &str) -> Vec<EditorSpan> {
    let bytes = source.as_bytes();
    let mut out = Vec::new();
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] == b'#' {
            let prev_ok = i == 0 || !(is_word_byte(bytes[i - 1]) || bytes[i - 1] == b'#');
            let next_alpha =
                matches!(bytes.get(i + 1), Some(&b) if b.is_ascii_alphabetic());
            if prev_ok && next_alpha {
                let start = i;
                i += 1; // consume the `#`
                while i < bytes.len() && is_tag_byte(bytes[i]) {
                    i += 1;
                }
                out.push(EditorSpan {
                    start_byte: start as u32,
                    end_byte: i as u32,
                    kind: EditorSpanKind::Tag,
                });
                continue;
            }
        }
        i += 1;
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

fn is_word_byte(b: u8) -> bool {
    b.is_ascii_alphanumeric() || b == b'_'
}

fn is_tag_byte(b: u8) -> bool {
    b.is_ascii_alphanumeric() || matches!(b, b'_' | b'-' | b'/')
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
            slice(src, &first(&spans, &EditorSpanKind::Wikilink).expect("wikilink")),
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
        let tags: Vec<&EditorSpan> =
            spans.iter().filter(|s| s.kind == EditorSpanKind::Tag).collect();
        assert_eq!(tags.len(), 1, "exactly one tag, got {tags:?}");
        assert_eq!(slice(src, tags[0]), "#project");
    }

    #[test]
    fn citation_span_covers_the_full_bracket() {
        let src = "as shown [@smith2020] elsewhere\n";
        let spans = highlight_spans(src);
        assert_eq!(
            slice(src, &first(&spans, &EditorSpanKind::Citation).expect("citation")),
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
            assert!(
                w[0].end_byte <= w[1].start_byte,
                "spans overlap or are unordered: {:?} then {:?}",
                w[0],
                w[1]
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
}
