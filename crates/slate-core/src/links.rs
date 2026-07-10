// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Link extraction for note content.
//!
//! Pulls every link out of a Markdown source string and normalizes
//! them into a single `ParsedLink` shape so downstream resolution
//! (issue #49) and storage (issue #50) can work uniformly across
//! Obsidian wikilinks and CommonMark/GFM Markdown links.
//!
//! ## What's a "link" here
//!
//! Two syntactic families:
//!
//! - **Wikilink** (Obsidian): `[[target]]`, `[[target|display]]`,
//!   `[[target#heading]]`, `[[target^block]]`, embed variant
//!   `![[target]]`. Targets are vault-relative file references (no
//!   path-resolution at this layer; that's #49).
//! - **Markdown** (pulldown-cmark): `[text](relative.md)` and
//!   `[text](https://example.com)`. We flag the latter as external so
//!   the link table never builds a phantom backlink to a URL. An
//!   internal destination's `#fragment` splits into `anchor` exactly
//!   like a wikilink anchor (`note.md#sec`, `note.md#^blk`), so the
//!   base resolves and the anchor rides the shared `LinkAnchor`
//!   plumbing (#509).
//!
//! ## What's NOT a link
//!
//! - Anything inside fenced code blocks, indented code blocks, or
//!   inline code spans (pulldown-cmark surfaces those as their own
//!   events; we suppress wikilink scanning inside their byte ranges).
//! - Backslash-escaped brackets (`\[\[not-a-link\]\]`). The wikilink
//!   scanner counts preceding backslashes and only opens a link when
//!   the count is even.
//! - Unbalanced brackets that don't close before a hard line break.
//!
//! ## Why a custom wikilink scanner
//!
//! pulldown-cmark doesn't recognize wikilink syntax — it would emit
//! `[[foo]]` as Markdown reference-link soup. We scan the raw source
//! ourselves and use pulldown-cmark only for code-span ranges and for
//! native Markdown links / images.

use pulldown_cmark::{Event, Options, Parser, Tag};

/// Categorical kind of a parsed link.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum LinkKind {
    /// Obsidian-style wikilink (`[[target]]` family).
    Wikilink,
    /// CommonMark / GFM link (`[text](url)`) — also covers images
    /// when `is_embed` is set.
    Markdown,
}

/// Anchor suffix on a link target.
///
/// Held separately from `target_raw` so #49's resolver can match the
/// target's note independently from the anchor (which is checked
/// against the resolved file's parsed headings or block IDs). Carried
/// by both wikilinks (`[[note#sec]]`) and internal Markdown links
/// (`[t](note.md#sec)`, #509).
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LinkAnchor {
    /// `[[target#heading]]` — heading text as authored, pre-slugify.
    Heading(String),
    /// `[[target^block]]` — block-ref ID (Obsidian convention).
    Block(String),
}

/// Single extracted link reference.
///
/// `span_start` / `span_end` are byte offsets into the original source
/// so renderers and editors can map back to the on-disk text exactly.
/// They span the full link syntax including delimiters (e.g. the `[[`
/// through the `]]`, or the `[` through the closing `)`).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ParsedLink {
    pub kind: LinkKind,
    /// Raw target as authored, with anchor stripped. For wikilinks
    /// this is the part before `|`, `#`, or `^`. For internal Markdown
    /// links it's the destination before the first `#`, verbatim
    /// otherwise (no URL-decoding); external destinations keep any
    /// `#fragment`.
    pub target_raw: String,
    /// Optional display text:
    /// - Wikilink: the segment after `|`, if any.
    /// - Markdown: the bracketed text (always present for Markdown
    ///   links, but we mirror the wikilink convention of `None` when
    ///   the display matches `target_raw` so callers don't have to
    ///   special-case).
    pub display_text: Option<String>,
    /// Anchor suffix when present, for wikilinks and internal Markdown
    /// links alike (#509).
    pub anchor: Option<LinkAnchor>,
    /// Inclusive start byte offset of the link in source.
    pub span_start: usize,
    /// Exclusive end byte offset of the link in source.
    pub span_end: usize,
    /// True for `![[target]]` (wikilink embed) or Markdown image
    /// syntax `![alt](src)`. Embeds aren't navigation links; the
    /// backlinks table (issue #50) excludes them.
    pub is_embed: bool,
    /// True when the target is an absolute URL (`http`, `https`,
    /// `mailto:`, etc.) rather than a vault-relative path. Set only
    /// for Markdown links — wikilinks are always vault-internal.
    pub is_external: bool,
}

/// Walk `source` and return every link reference in document order.
///
/// Pure function — no IO, no allocation beyond the returned vec and
/// per-link owned strings. Safe to call on huge inputs (sizing is
/// driven by the number of links, not the source length).
///
/// YAML frontmatter is skipped before parsing (#235): wikilink values
/// inside YAML scalars (`related: "[[Other Note]]"`) reach the link
/// graph through the property mechanism (`PropertyValue::Wikilink`
/// in `frontmatter::extract_frontmatter`), and Markdown links inside
/// YAML strings are pure noise. Emitted `span_start` / `span_end`
/// are shifted back into the original-source coordinate space so
/// callers can keep indexing into the full source they passed in.
pub fn extract_links(source: &str) -> Vec<ParsedLink> {
    let body = crate::frontmatter::body_after_frontmatter(source);
    let body_offset = source.len() - body.len();

    let (md_links, code_ranges) = walk_markdown(body);
    let wiki_links = scan_wikilinks(body, &code_ranges);

    let mut out: Vec<ParsedLink> = md_links.into_iter().chain(wiki_links).collect();
    if body_offset > 0 {
        // Shift offsets back into the original-source coordinate
        // space. No-op for the common no-frontmatter path because
        // `body_offset == 0`.
        for link in &mut out {
            link.span_start += body_offset;
            link.span_end += body_offset;
        }
    }
    // Document order so callers (and tests) can rely on positional
    // semantics without sorting at the call site.
    out.sort_by_key(|link| link.span_start);
    out
}

/// Walks the pulldown-cmark event stream once to:
/// - Emit `ParsedLink`s for Markdown link / image events.
/// - Collect byte ranges of code blocks + inline code so the wikilink
///   scanner can skip them.
fn walk_markdown(source: &str) -> (Vec<ParsedLink>, Vec<(usize, usize)>) {
    let mut links = Vec::new();
    let mut code_ranges = Vec::new();

    let mut parser = Parser::new_ext(source, Options::ENABLE_STRIKETHROUGH).into_offset_iter();
    while let Some((event, range)) = parser.next() {
        match event {
            Event::Start(Tag::Link { dest_url, .. }) => {
                let display = collect_inline_text(&mut parser);
                let url_string = dest_url.into_string();
                let is_external = looks_external(&url_string);
                let display_text = if display.is_empty() || display == url_string {
                    None
                } else {
                    Some(display)
                };
                // Fragment splits into `anchor` only for internal
                // destinations; external URLs keep their `#y` verbatim.
                let (target_raw, anchor) = if is_external {
                    (url_string, None)
                } else {
                    split_markdown_target(&url_string)
                };
                links.push(ParsedLink {
                    kind: LinkKind::Markdown,
                    target_raw,
                    display_text,
                    anchor,
                    span_start: range.start,
                    span_end: range.end,
                    is_embed: false,
                    is_external,
                });
            }
            Event::Start(Tag::Image { dest_url, .. }) => {
                let display = collect_inline_text(&mut parser);
                let url_string = dest_url.into_string();
                let is_external = looks_external(&url_string);
                let display_text = if display.is_empty() {
                    None
                } else {
                    Some(display)
                };
                let (target_raw, anchor) = if is_external {
                    (url_string, None)
                } else {
                    split_markdown_target(&url_string)
                };
                links.push(ParsedLink {
                    kind: LinkKind::Markdown,
                    target_raw,
                    display_text,
                    anchor,
                    span_start: range.start,
                    span_end: range.end,
                    is_embed: true,
                    is_external,
                });
            }
            Event::Code(_) => {
                // Inline code: `foo bar`. Range covers the backticks +
                // body, so a wikilink in `[[foo]]` (inline code) won't
                // be picked up.
                code_ranges.push((range.start, range.end));
            }
            Event::Start(Tag::CodeBlock(_)) => {
                // CodeBlock event covers the fenced or indented block.
                // pulldown-cmark emits Text events for the body
                // separately, but the outer range covers everything;
                // collect that single range.
                code_ranges.push((range.start, range.end));
            }
            _ => {}
        }
    }
    (links, code_ranges)
}

/// Drain inline events up to the matching closing `End`, joining the
/// textual content into a single string. Used to recover the bracketed
/// display text of `[display](url)` or `![alt](src)`.
///
/// Tracks nesting depth because inline emphasis / code / nested links
/// can also emit their own End events; we stop at the End that
/// matches the outer Start the caller already consumed.
fn collect_inline_text<'a, I>(parser: &mut I) -> String
where
    I: Iterator<Item = (Event<'a>, std::ops::Range<usize>)>,
{
    let mut out = String::new();
    let mut depth = 1usize;
    for (event, _) in parser.by_ref() {
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

/// Split a non-external Markdown destination into `(base, anchor)` by
/// the FIRST `#`, mirroring the wikilink anchor rules so anchored
/// destinations (`note.md#sec`) resolve on their base and carry the
/// fragment through `LinkAnchor` (#509).
///
/// The full destination is otherwise verbatim — no percent-decoding,
/// no trimming (the resolver reads destination bytes as authored).
/// Unlike wikilinks, `^` is a legal path character in a Markdown
/// destination, so a bare `^` (no `#`) is NOT an anchor.
///
/// Callers must have already ruled the destination internal:
/// `looks_external` runs on the FULL authored destination so a
/// fragment-only `#intro` or a `https://x#y` URL stays external and
/// untouched.
fn split_markdown_target(url: &str) -> (String, Option<LinkAnchor>) {
    match url.find('#') {
        Some(idx) => (
            url[..idx].to_string(),
            parse_anchor_after_hash(&url[idx + 1..]),
        ),
        None => (url.to_string(), None),
    }
}

/// `pub(crate)` alias so the link resolver (#49) can short-circuit
/// to `External` without reimplementing the same heuristic. Kept
/// out of the public API because the rule is an implementation
/// detail of the link layer, not a stable contract.
pub(crate) fn looks_external_for_resolver(url: &str) -> bool {
    looks_external(url)
}

/// Heuristic: treat URLs with a scheme (or `mailto:` / `tel:` /
/// fragment-only / `//host` references) as external. Bare relative
/// paths fall through as internal so #49 can resolve them against
/// the vault index.
///
/// "External" here means "not a note-to-note link" — URLs, mailto,
/// tel, and in-document fragments (`#intro`) all qualify, because
/// none of them should produce backlinks against another vault file.
fn looks_external(url: &str) -> bool {
    // In-document anchors (`#section`) aren't note-to-note links.
    if url.starts_with('#') {
        return true;
    }
    // Protocol-relative URLs.
    if url.starts_with("//") {
        return true;
    }
    if let Some(colon_idx) = url.find(':') {
        let scheme = &url[..colon_idx];
        // RFC 3986: scheme = ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )
        // We also require length >= 2 so Windows drive letters
        // (`C:\notes\foo.md`, `D:/projects/...`) stay internal. The
        // ALPHA-first rule additionally rules out filenames whose
        // pre-colon segment is digit-leading like `2024:report.md`.
        let mut scheme_chars = scheme.chars();
        if let Some(first) = scheme_chars.next()
            && scheme.len() >= 2
            && first.is_ascii_alphabetic()
            && scheme_chars.all(|c| c.is_ascii_alphanumeric() || c == '+' || c == '-' || c == '.')
        {
            return true;
        }
    }
    false
}

/// Scan the raw source for `[[…]]` wikilinks, skipping any whose
/// opening `[[` falls inside a code range.
pub(crate) fn scan_wikilinks(source: &str, code_ranges: &[(usize, usize)]) -> Vec<ParsedLink> {
    let bytes = source.as_bytes();
    let mut links = Vec::new();
    let mut i = 0;
    while i + 1 < bytes.len() {
        if bytes[i] == b'['
            && bytes[i + 1] == b'['
            && !is_escaped_at(bytes, i)
            && !in_any_range(i, code_ranges)
            && let Some(parsed) = try_parse_wikilink(source, bytes, i)
        {
            let span_end = parsed.span_end;
            links.push(parsed);
            i = span_end;
            continue;
        }
        i += 1;
    }
    links
}

/// `true` when offset `pos` is preceded by an odd number of `\`
/// backslashes (so the bracket is CommonMark-escaped).
fn is_escaped_at(bytes: &[u8], pos: usize) -> bool {
    let mut count = 0usize;
    let mut j = pos;
    while j > 0 && bytes[j - 1] == b'\\' {
        count += 1;
        j -= 1;
    }
    count % 2 == 1
}

fn in_any_range(pos: usize, ranges: &[(usize, usize)]) -> bool {
    ranges.iter().any(|(s, e)| pos >= *s && pos < *e)
}

/// Attempt to parse a wikilink starting at the `[` at index `start`.
/// Returns `None` when there's no balanced `]]` before the next hard
/// line break or end-of-source — unbalanced brackets are deliberately
/// left as plain text rather than swallowing the rest of the file.
fn try_parse_wikilink(source: &str, bytes: &[u8], start: usize) -> Option<ParsedLink> {
    // `start` points at the first `[`. The second `[` is `start + 1`.
    let body_start = start + 2;
    let mut i = body_start;
    while i + 1 < bytes.len() {
        // Hard line break interrupts an open wikilink. This matches
        // the way Obsidian's renderer + index treat unclosed brackets.
        if bytes[i] == b'\n' {
            return None;
        }
        if bytes[i] == b']' && bytes[i + 1] == b']' {
            let body_end = i;
            let span_end = i + 2;
            // Embed prefix: an UNESCAPED `!` immediately before the
            // opening `[[`.
            let is_embed =
                start > 0 && bytes[start - 1] == b'!' && !is_escaped_at(bytes, start - 1);
            let span_start = if is_embed { start - 1 } else { start };

            let body = &source[body_start..body_end];
            let (target, display, anchor) = split_wikilink_body(body);
            // Empty target is treated as "not a real wikilink" — `[[]]`
            // shouldn't show up in the link graph. The text stays in
            // the source unchanged because we just skip emitting it.
            if target.is_empty() {
                return None;
            }
            return Some(ParsedLink {
                kind: LinkKind::Wikilink,
                target_raw: target,
                display_text: display,
                anchor,
                span_start,
                span_end,
                is_embed,
                is_external: false,
            });
        }
        i += 1;
    }
    None
}

/// Split a wikilink body (text between `[[` and `]]`) into
/// `(target, display, anchor)`.
///
/// Order of split: first `|` separates display from target; then `#`
/// or `^` on the target side separates the anchor. Anchor `#`/`^` on
/// the display side stays as literal display text.
fn split_wikilink_body(body: &str) -> (String, Option<String>, Option<LinkAnchor>) {
    let (target_segment, display) = match body.find('|') {
        Some(idx) => (
            body[..idx].trim().to_string(),
            Some(body[idx + 1..].trim().to_string()),
        ),
        None => (body.trim().to_string(), None),
    };

    // Heading-style anchor takes priority over block-ref because
    // Obsidian treats `[[note#heading^block]]` as a heading anchor
    // with a literal `^block` suffix — we mirror that by checking
    // `#` first. The one exception: `#` immediately followed by `^`
    // is Obsidian's canonical block-ref syntax (`[[note#^block]]`,
    // #413) and parses as a Block anchor, same as the bare
    // `[[note^block]]` legacy form.
    let (target, anchor) = if let Some(idx) = target_segment.find('#') {
        let target = target_segment[..idx].trim().to_string();
        let anchor = parse_anchor_after_hash(&target_segment[idx + 1..]);
        (target, anchor)
    } else if let Some(idx) = target_segment.find('^') {
        let target = target_segment[..idx].trim().to_string();
        let anchor_raw = target_segment[idx + 1..].trim().to_string();
        let anchor = if anchor_raw.is_empty() {
            None
        } else {
            Some(LinkAnchor::Block(anchor_raw))
        };
        (target, anchor)
    } else {
        (target_segment, None)
    };

    // Normalize empty display to None so downstream callers don't have
    // to distinguish "no pipe" from "empty after pipe".
    let display = display.filter(|d| !d.is_empty());
    (target, display, anchor)
}

/// Parse the anchor text that follows a `#` into a `LinkAnchor`.
///
/// `anchor_raw` is everything after the `#` (the `#` itself already
/// stripped). Shared by wikilink and Markdown extraction so both
/// families split fragments identically:
/// - `sec` → `Heading("sec")`
/// - `^blk` → `Block("blk")` (Obsidian's canonical block-ref syntax,
///   #413)
/// - empty (`#`) or bare `^` (`#^`) → `None`
fn parse_anchor_after_hash(anchor_raw: &str) -> Option<LinkAnchor> {
    let anchor_raw = anchor_raw.trim();
    if anchor_raw.is_empty() {
        None
    } else if let Some(block_raw) = anchor_raw.strip_prefix('^') {
        let block = block_raw.trim();
        if block.is_empty() {
            None
        } else {
            Some(LinkAnchor::Block(block.to_string()))
        }
    } else {
        Some(LinkAnchor::Heading(anchor_raw.to_string()))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn target(link: &ParsedLink) -> &str {
        &link.target_raw
    }

    // --- Wikilink shapes ---

    #[test]
    fn plain_wikilink() {
        let links = extract_links("see [[Alpha]] for context");
        assert_eq!(links.len(), 1);
        let link = &links[0];
        assert_eq!(link.kind, LinkKind::Wikilink);
        assert_eq!(target(link), "Alpha");
        assert!(link.display_text.is_none());
        assert!(link.anchor.is_none());
        assert!(!link.is_embed);
        assert!(!link.is_external);
        assert_eq!(
            &"see [[Alpha]] for context"[link.span_start..link.span_end],
            "[[Alpha]]"
        );
    }

    #[test]
    fn wikilink_with_display_text() {
        let links = extract_links("[[Alpha|the first one]] arrives");
        assert_eq!(links.len(), 1);
        assert_eq!(target(&links[0]), "Alpha");
        assert_eq!(links[0].display_text.as_deref(), Some("the first one"));
    }

    #[test]
    fn wikilink_with_heading_anchor() {
        let links = extract_links("[[Alpha#Introduction]]");
        assert_eq!(links.len(), 1);
        assert_eq!(target(&links[0]), "Alpha");
        assert_eq!(
            links[0].anchor,
            Some(LinkAnchor::Heading("Introduction".to_string()))
        );
    }

    #[test]
    fn wikilink_with_block_anchor() {
        let links = extract_links("[[Alpha^abc123]]");
        assert_eq!(links.len(), 1);
        assert_eq!(target(&links[0]), "Alpha");
        assert_eq!(
            links[0].anchor,
            Some(LinkAnchor::Block("abc123".to_string()))
        );
    }

    #[test]
    fn wikilink_with_obsidian_block_anchor() {
        // `[[note#^block]]` is Obsidian's canonical block-ref syntax
        // (#413) — must parse as a Block anchor, never
        // Heading("^block").
        let links = extract_links("[[Whipped cream#^method-step-2]]");
        assert_eq!(links.len(), 1);
        assert_eq!(target(&links[0]), "Whipped cream");
        assert_eq!(
            links[0].anchor,
            Some(LinkAnchor::Block("method-step-2".to_string()))
        );
    }

    #[test]
    fn embed_with_obsidian_block_anchor() {
        let links = extract_links("![[Whipped cream#^method-step-2]]");
        assert_eq!(links.len(), 1);
        assert!(links[0].is_embed);
        assert_eq!(
            links[0].anchor,
            Some(LinkAnchor::Block("method-step-2".to_string()))
        );
    }

    #[test]
    fn wikilink_heading_with_caret_suffix_stays_heading() {
        // Obsidian: `#heading^x` is a heading anchor with a literal
        // `^x` suffix. Only an immediate `#^` is a block ref.
        let links = extract_links("[[Alpha#Intro^tail]]");
        assert_eq!(
            links[0].anchor,
            Some(LinkAnchor::Heading("Intro^tail".to_string()))
        );
    }

    #[test]
    fn wikilink_bare_hash_caret_parses_as_no_anchor() {
        // Degenerate `[[Alpha#^]]` — empty block id collapses to no
        // anchor, mirroring the existing empty-heading behavior.
        let links = extract_links("[[Alpha#^]]");
        assert_eq!(target(&links[0]), "Alpha");
        assert_eq!(links[0].anchor, None);
    }

    #[test]
    fn wikilink_with_anchor_and_display() {
        let links = extract_links("[[Alpha#Intro|the intro]]");
        assert_eq!(target(&links[0]), "Alpha");
        assert_eq!(links[0].display_text.as_deref(), Some("the intro"));
        assert_eq!(
            links[0].anchor,
            Some(LinkAnchor::Heading("Intro".to_string()))
        );
    }

    #[test]
    fn wikilink_embed_prefix_sets_flag() {
        let links = extract_links("![[Diagram]]");
        assert_eq!(links.len(), 1);
        assert!(links[0].is_embed);
        assert_eq!(target(&links[0]), "Diagram");
        assert_eq!(
            &"![[Diagram]]"[links[0].span_start..links[0].span_end],
            "![[Diagram]]"
        );
    }

    #[test]
    fn escaped_brackets_do_not_produce_a_link() {
        let links = extract_links(r"this is \[\[not-a-link\]\] really");
        assert!(links.is_empty(), "expected no links, got {:?}", links);
    }

    #[test]
    fn unbalanced_brackets_at_line_break_are_skipped() {
        // The scanner gives up at a newline so an unclosed `[[` doesn't
        // swallow the rest of the file.
        let links = extract_links("[[unclosed\nnext paragraph [[Alpha]]");
        assert_eq!(links.len(), 1);
        assert_eq!(target(&links[0]), "Alpha");
    }

    #[test]
    fn empty_target_is_not_a_link() {
        let links = extract_links("here [[]] and [[ ]] should both vanish");
        assert!(links.is_empty(), "expected no links, got {:?}", links);
    }

    // --- Markdown shapes ---

    #[test]
    fn markdown_relative_path_link() {
        let links = extract_links("see [intro](notes/intro.md)");
        assert_eq!(links.len(), 1);
        let link = &links[0];
        assert_eq!(link.kind, LinkKind::Markdown);
        assert_eq!(target(link), "notes/intro.md");
        assert_eq!(link.display_text.as_deref(), Some("intro"));
        assert!(!link.is_external);
    }

    #[test]
    fn markdown_url_is_flagged_external() {
        let links = extract_links("[example](https://example.com)");
        assert_eq!(links.len(), 1);
        assert!(links[0].is_external);
        assert_eq!(target(&links[0]), "https://example.com");
    }

    #[test]
    fn markdown_mailto_is_external() {
        let links = extract_links("ping me: [you](mailto:me@example.com)");
        assert_eq!(links.len(), 1);
        assert!(links[0].is_external);
    }

    #[test]
    fn markdown_fragment_only_is_external() {
        // `#intro` is an in-document anchor, not a vault file. The
        // backlinks table doesn't want a phantom edge from a note to
        // itself.
        let links = extract_links("jump to [intro](#intro)");
        assert_eq!(links.len(), 1);
        assert!(links[0].is_external);
        assert_eq!(target(&links[0]), "#intro");
    }

    #[test]
    fn markdown_windows_drive_letter_is_internal() {
        // `C:\foo\bar.md` is a filesystem path, not a URL scheme.
        // looks_external requires scheme length >= 2 so single-letter
        // drive references stay internal for #49's resolver.
        let links = extract_links("[file](C:\\notes\\intro.md)");
        assert_eq!(links.len(), 1);
        assert!(!links[0].is_external, "got {:?}", links[0]);
    }

    #[test]
    fn markdown_numeric_leading_filename_with_colon_is_internal() {
        // `2024:report.md` is a valid POSIX filename. RFC 3986 says
        // scheme must start with ALPHA, so the digit-leading segment
        // before the colon doesn't count as a scheme and the link
        // stays internal for #49's resolver.
        let links = extract_links("[file](2024:report.md)");
        assert_eq!(links.len(), 1);
        assert!(!links[0].is_external, "got {:?}", links[0]);
    }

    #[test]
    fn markdown_alpha_leading_scheme_filename_is_external() {
        // Locks in the deliberate trade-off: `notes:2024.md` looks
        // syntactically like a URL with scheme `notes`, so we treat
        // it as external. POSIX allows this as a filename but it's
        // ambiguous; users who want a vault-relative link should
        // write `notes/2024.md` or `./notes:2024.md`. Documenting
        // the behavior here so future refactors don't silently flip
        // the classification.
        let links = extract_links("[file](notes:2024.md)");
        assert_eq!(links.len(), 1);
        assert!(links[0].is_external);
    }

    #[test]
    fn escaped_embed_prefix_does_not_set_embed_flag() {
        // `\!` escapes the bang, so what follows is a plain wikilink,
        // not an embed — and the span shouldn't include the escaped
        // bang either.
        let source = r"\![[Alpha]]";
        let links = extract_links(source);
        assert_eq!(links.len(), 1);
        assert!(!links[0].is_embed);
        // span starts at the `[` after the escaped `\!`, not at the
        // `\` or `!`.
        assert_eq!(&source[links[0].span_start..links[0].span_end], "[[Alpha]]");
    }

    #[test]
    fn markdown_heading_fragment_splits_into_anchor() {
        let links = extract_links("see [t](note.md#sec)");
        assert_eq!(links.len(), 1);
        assert_eq!(links[0].kind, LinkKind::Markdown);
        assert_eq!(target(&links[0]), "note.md");
        assert_eq!(
            links[0].anchor,
            Some(LinkAnchor::Heading("sec".to_string()))
        );
        assert!(!links[0].is_external);
    }

    #[test]
    fn markdown_block_fragment_splits_into_anchor() {
        let links = extract_links("see [t](note.md#^blk)");
        assert_eq!(links.len(), 1);
        assert_eq!(target(&links[0]), "note.md");
        assert_eq!(links[0].anchor, Some(LinkAnchor::Block("blk".to_string())));
    }

    #[test]
    fn markdown_image_fragment_splits_into_anchor() {
        let links = extract_links("![a](note.md#sec)");
        assert_eq!(links.len(), 1);
        assert!(links[0].is_embed);
        assert_eq!(target(&links[0]), "note.md");
        assert_eq!(
            links[0].anchor,
            Some(LinkAnchor::Heading("sec".to_string()))
        );
    }

    #[test]
    fn markdown_empty_fragment_yields_no_anchor() {
        let links = extract_links("see [t](note.md#)");
        assert_eq!(links.len(), 1);
        assert_eq!(target(&links[0]), "note.md");
        assert_eq!(links[0].anchor, None);
    }

    #[test]
    fn markdown_external_url_with_fragment_stays_verbatim() {
        // The fragment splitter only runs on internal destinations, so a
        // URL keeps its `#y` in target_raw and gets no anchor.
        let links = extract_links("[t](https://x.com#y)");
        assert_eq!(links.len(), 1);
        assert!(links[0].is_external);
        assert_eq!(target(&links[0]), "https://x.com#y");
        assert_eq!(links[0].anchor, None);
    }

    #[test]
    fn markdown_caret_in_path_is_not_split() {
        // `^` is a legal path character in a Markdown destination — only
        // `#` opens an anchor.
        let links = extract_links("[t](notes/a^b.md)");
        assert_eq!(links.len(), 1);
        assert_eq!(target(&links[0]), "notes/a^b.md");
        assert_eq!(links[0].anchor, None);
    }

    #[test]
    fn markdown_image_is_embed() {
        let links = extract_links("![alt](images/cover.png)");
        assert_eq!(links.len(), 1);
        let link = &links[0];
        assert_eq!(link.kind, LinkKind::Markdown);
        assert!(link.is_embed);
        assert_eq!(target(link), "images/cover.png");
        assert_eq!(link.display_text.as_deref(), Some("alt"));
    }

    // --- Mixed + code-block exclusion ---

    #[test]
    fn mixed_wikilink_and_markdown_in_same_file() {
        let source = "intro [[Alpha]] and a [link](Beta.md) here.";
        let links = extract_links(source);
        assert_eq!(links.len(), 2);
        // Document order: Alpha's [[ comes before Beta's [.
        assert_eq!(links[0].kind, LinkKind::Wikilink);
        assert_eq!(target(&links[0]), "Alpha");
        assert_eq!(links[1].kind, LinkKind::Markdown);
        assert_eq!(target(&links[1]), "Beta.md");
    }

    #[test]
    fn mixed_returned_in_document_order_by_span_start() {
        let source = "[[Alpha]] then [link](Beta.md)";
        let links = extract_links(source);
        assert_eq!(links.len(), 2);
        assert!(links[0].span_start < links[1].span_start);
        assert_eq!(target(&links[0]), "Alpha");
        assert_eq!(target(&links[1]), "Beta.md");
    }

    #[test]
    fn fenced_code_block_suppresses_wikilink_scan() {
        let source = "before\n\n```\n[[NotALink]]\n```\n\nafter [[Real]]";
        let links = extract_links(source);
        assert_eq!(links.len(), 1, "got {:?}", links);
        assert_eq!(target(&links[0]), "Real");
    }

    #[test]
    fn inline_code_suppresses_wikilink_scan() {
        let source = "use `[[NotALink]]` syntax — see [[Real]]";
        let links = extract_links(source);
        assert_eq!(links.len(), 1, "got {:?}", links);
        assert_eq!(target(&links[0]), "Real");
    }

    #[test]
    fn indented_code_block_suppresses_wikilink_scan() {
        let source = "intro\n\n    [[NotALink]]\n\nafter [[Real]]";
        let links = extract_links(source);
        assert_eq!(links.len(), 1, "got {:?}", links);
        assert_eq!(target(&links[0]), "Real");
    }

    #[test]
    fn span_offsets_round_trip_to_source() {
        let source = "alpha [[Beta]] and ![[Embed]] and [md](Gamma.md)";
        let links = extract_links(source);
        assert_eq!(links.len(), 3);
        for link in &links {
            let slice = &source[link.span_start..link.span_end];
            match link.kind {
                LinkKind::Wikilink if link.is_embed => assert!(slice.starts_with("![[")),
                LinkKind::Wikilink => assert!(slice.starts_with("[[")),
                LinkKind::Markdown => assert!(slice.starts_with("[") || slice.starts_with("!")),
            }
        }
    }

    // --- Frontmatter handling (#235) -------------------------------

    #[test]
    fn extract_links_skips_yaml_frontmatter() {
        // A wikilink value inside a YAML scalar would have been
        // emitted as a ParsedLink pre-fix, feeding the backlinks
        // graph from the property block. With the skip, only the
        // body's link counts.
        let source = "---\n\
            related: \"[[Other Note]]\"\n\
            url: \"[md-in-yaml](https://example.com)\"\n\
            ---\n\n\
            body [[Real]] paragraph\n";
        let links = extract_links(source);
        assert_eq!(links.iter().map(target).collect::<Vec<_>>(), vec!["Real"]);
    }

    #[test]
    fn extract_links_span_offsets_remain_in_source_coordinates_after_skip() {
        // Critical: callers (links_db's `snippet_around`) index into
        // the original source using the emitted offsets. The
        // frontmatter-skip rewrite must shift offsets back into the
        // full source's coordinate space, not the body slice's.
        let source = "---\nkey: value\n---\n\nbody [[Alpha]] suffix\n";
        let links = extract_links(source);
        assert_eq!(links.len(), 1);
        let link = &links[0];
        assert_eq!(
            &source[link.span_start..link.span_end],
            "[[Alpha]]",
            "span_start/span_end must point into the full source after frontmatter skip"
        );
    }

    #[test]
    fn extract_links_passthrough_with_no_frontmatter() {
        // Fast-path regression: a plain body should produce the
        // same links as before with no offset shift.
        let source = "alpha [[Beta]] gamma";
        let links = extract_links(source);
        assert_eq!(links.len(), 1);
        assert_eq!(links[0].span_start, 6);
        assert_eq!(&source[links[0].span_start..links[0].span_end], "[[Beta]]");
    }

    #[test]
    fn extract_links_passthrough_with_open_frontmatter_without_close() {
        // Mid-edit shape: leading `---` but no closing `---`.
        // body_after_frontmatter is a no-op, so the link inside
        // the unterminated YAML still gets emitted. Today's
        // pulldown-cmark behaviour stands.
        let source = "---\nrelated: \"[[Other Note]]\"\n\n[[Real]]\n";
        let links = extract_links(source);
        // Both links present because no frontmatter was detected.
        let targets: Vec<&str> = links.iter().map(target).collect();
        assert!(targets.contains(&"Other Note"));
        assert!(targets.contains(&"Real"));
    }
}
