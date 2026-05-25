//! Block-anchor (`^block-id`) extraction.
//!
//! Obsidian's convention for "embed this specific paragraph / list
//! item / blockquote" is to mark the source with `^block-id`. Two
//! shapes:
//!
//! - **Trailing on the last line of a block**:
//!   ```text
//!   paragraph text spans multiple
//!   lines and ends with the anchor ^my-block
//!   ```
//!   The block is the paragraph; the anchor lives in the trailing
//!   whitespace of its last line.
//!
//! - **Standalone on a line of its own** immediately after a block:
//!   ```text
//!   paragraph text
//!   ^my-block
//!   ```
//!   The block is the paragraph above; the anchor line is consumed
//!   into the block's byte range so the `resolve_embed` reader
//!   re-emits the anchor when it returns the block's text.
//!
//! ## Block kinds
//!
//! `paragraph` / `list_item` / `blockquote`. Classified via
//! `pulldown-cmark`'s offset iterator. List items + blockquotes get
//! the same anchor handling as paragraphs — the resolver re-applies
//! the bullet / quote prefix when it renders the embed.
//!
//! ## What's NOT a block anchor
//!
//! - Anchors inside fenced code blocks (`^id` is just text in a
//!   code example).
//! - Anchors inside HTML blocks.
//! - Anchors inside YAML frontmatter (where `^` is sometimes used
//!   as a YAML anchor marker — different concept).
//! - Duplicate `^id` within one file: the first occurrence wins;
//!   subsequent dupes drop silently (matches Obsidian's behavior).
//!
//! ## Block ID grammar
//!
//! `^` followed by 1+ `[a-zA-Z0-9_-]` characters. Underscore and
//! hyphen allowed for stable hand-authored IDs; everything else
//! (including Unicode letters) is rejected to match Obsidian's
//! auto-generated ID shape.

use pulldown_cmark::{Event, Parser, Tag, TagEnd};

/// One block anchor discovered in a source file. `ordinal` is the
/// 0-based document-order index, stable across saves for a given
/// parser version. `byte_start..byte_end` covers the block's
/// bytes including the trailing or standalone `^id` text — the
/// resolver returns the byte slice directly.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BlockAnchor {
    pub ordinal: u32,
    pub block_id: String,
    pub kind: BlockKind,
    /// 1-based line number where the block's first character sits.
    pub line_start: u32,
    /// 1-based line number where the block's last character sits
    /// (inclusive). For an anchor on a standalone line this is the
    /// anchor's line; for a trailing anchor this is the same line
    /// as the block's last content.
    pub line_end: u32,
    pub byte_start: u32,
    /// Exclusive.
    pub byte_end: u32,
    /// First ~120 chars of the block, trimmed and space-normalised.
    /// Stored alongside the bounds so the embed UI's AT label
    /// doesn't have to re-read the source slice.
    pub text_preview: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BlockKind {
    Paragraph,
    ListItem,
    BlockQuote,
}

impl BlockKind {
    pub fn as_str(self) -> &'static str {
        match self {
            BlockKind::Paragraph => "paragraph",
            BlockKind::ListItem => "list_item",
            BlockKind::BlockQuote => "blockquote",
        }
    }
}

/// Extract block anchors from a Markdown source string in document
/// order. Empty Vec for sources with no anchor markers.
pub fn extract_blocks(source: &str) -> Vec<BlockAnchor> {
    // Cheap prefilter: zero-anchor documents short-circuit before
    // the full pulldown-cmark walk. `^` followed by ASCII alphanum
    // / `_` / `-` covers every valid anchor; if no such pair
    // exists, there can't be any anchors.
    if !might_contain_anchor(source) {
        return Vec::new();
    }

    let excluded = crate::tasks::excluded_byte_ranges(source);

    // First pass: collect every `^block-id` candidate position in
    // the source via a byte walk. Cheaper than asking pulldown-cmark
    // to find them inside Text events because the candidate set is
    // small even on a 100 KB doc.
    let mut anchors_by_byte: Vec<(usize, String)> = find_anchor_candidates(source, &excluded);
    if anchors_by_byte.is_empty() {
        return Vec::new();
    }

    // Second pass: walk pulldown-cmark events with offset info so
    // we can classify each block range (paragraph / list item /
    // blockquote). For each top-level block, attach any anchor
    // whose byte position falls inside its range (trailing anchor)
    // or on the line immediately following its end (standalone).
    let blocks = collect_block_ranges(source);
    if blocks.is_empty() {
        return Vec::new();
    }

    // For standalone-anchor detection we need to map "byte position
    // of an anchor on its own line" to "block above". Walk anchors
    // in order; for each, either match an enclosing block or the
    // nearest preceding block whose end is on the previous line.
    let mut out: Vec<BlockAnchor> = Vec::new();
    let mut seen_ids: std::collections::HashSet<String> = std::collections::HashSet::new();

    anchors_by_byte.sort_by_key(|(b, _)| *b);

    for (anchor_byte, raw_id) in anchors_by_byte {
        // Deduplicate by id — first occurrence wins (Obsidian).
        if !seen_ids.insert(raw_id.clone()) {
            continue;
        }

        let Some(block) = find_owning_block(&blocks, source, anchor_byte) else {
            // Anchor not associated with any block (e.g. inside a
            // setext-underlined heading or some other shape the
            // parser didn't classify). Drop silently rather than
            // try to invent a block range.
            continue;
        };

        let (line_start, line_end) = line_range(source, block.byte_start, block.byte_end);
        let text_preview = preview_for(&source[block.byte_start..block.byte_end]);
        out.push(BlockAnchor {
            ordinal: out.len() as u32,
            block_id: raw_id,
            kind: block.kind,
            line_start,
            line_end,
            byte_start: block.byte_start as u32,
            byte_end: block.byte_end as u32,
            text_preview,
        });
    }
    out
}

#[derive(Debug, Clone, Copy)]
struct BlockRange {
    kind: BlockKind,
    byte_start: usize,
    byte_end: usize,
}

fn collect_block_ranges(source: &str) -> Vec<BlockRange> {
    // Track only the OUTERMOST block of each kind so a paragraph
    // nested in a list item doesn't show up as both kinds. The
    // outer kind (list_item) wins because it's what the user
    // typically means when they say "embed this".
    let mut depth: i32 = 0;
    let mut current_kind: Option<BlockKind> = None;
    let mut current_start: usize = 0;
    let mut out: Vec<BlockRange> = Vec::new();
    for (event, range) in Parser::new(source).into_offset_iter() {
        match event {
            Event::Start(Tag::Paragraph) => {
                if depth == 0 {
                    current_kind = Some(BlockKind::Paragraph);
                    current_start = range.start;
                }
                depth += 1;
            }
            Event::Start(Tag::Item) => {
                if depth == 0 {
                    current_kind = Some(BlockKind::ListItem);
                    current_start = range.start;
                }
                depth += 1;
            }
            Event::Start(Tag::BlockQuote) => {
                if depth == 0 {
                    current_kind = Some(BlockKind::BlockQuote);
                    current_start = range.start;
                }
                depth += 1;
            }
            Event::End(TagEnd::Paragraph)
            | Event::End(TagEnd::Item)
            | Event::End(TagEnd::BlockQuote) => {
                depth -= 1;
                if depth == 0 {
                    if let Some(kind) = current_kind.take() {
                        out.push(BlockRange {
                            kind,
                            byte_start: current_start,
                            byte_end: range.end,
                        });
                    }
                }
            }
            _ => {}
        }
    }
    out
}

/// Locate the block that owns `anchor_byte`. Two cases:
///   - The anchor sits inside the block's range → trailing anchor.
///   - The anchor sits on a line immediately following the block's
///     end → standalone anchor.
fn find_owning_block(
    blocks: &[BlockRange],
    source: &str,
    anchor_byte: usize,
) -> Option<BlockRange> {
    // Trailing-anchor case: linear scan; the per-file block count
    // is small enough not to need a btree.
    if let Some(inner) = blocks
        .iter()
        .find(|b| anchor_byte >= b.byte_start && anchor_byte < b.byte_end)
    {
        // Extend the block to include the rest of the line so the
        // anchor's text falls inside the returned bounds — the
        // resolver's byte slice should include the `^id`.
        let mut extended = *inner;
        extended.byte_end = end_of_line(source, anchor_byte);
        return Some(extended);
    }

    // Standalone-anchor case: the anchor is on its own line; the
    // line immediately above is either blank or the last content
    // line of a preceding block. Find the most recent block whose
    // end is on the same line as the line above the anchor (or one
    // line above, accommodating the trailing `\n` between the
    // block's end and the anchor line).
    let anchor_line_start = start_of_line(source, anchor_byte);
    if anchor_line_start == 0 {
        return None;
    }
    let preceding_line_end = anchor_line_start - 1; // the `\n` byte itself
    blocks
        .iter()
        .rev()
        .find(|b| b.byte_end <= anchor_line_start && b.byte_end >= preceding_line_end)
        .map(|b| {
            let mut extended = *b;
            extended.byte_end = end_of_line(source, anchor_byte);
            extended
        })
}

/// Byte-walk over `source` looking for `^[A-Za-z0-9_-]+` candidates
/// outside excluded ranges. Returns `(byte_position_of_caret, id)`
/// pairs.
fn find_anchor_candidates(source: &str, excluded: &[(usize, usize)]) -> Vec<(usize, String)> {
    let bytes = source.as_bytes();
    let mut out: Vec<(usize, String)> = Vec::new();
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] != b'^' {
            i += 1;
            continue;
        }
        // Must be at start of a token: preceded by start-of-file,
        // whitespace, or the line-leading position. This rules out
        // `42^17` (a stray `^` in arithmetic) but accepts both
        // trailing-on-block (`text ^id`) and standalone-line (`^id`)
        // shapes.
        let preceded_ok = i == 0 || matches!(bytes[i - 1], b' ' | b'\t' | b'\n' | b'\r');
        if !preceded_ok {
            i += 1;
            continue;
        }
        if crate::tasks::byte_in_excluded_range(i, excluded) {
            i += 1;
            continue;
        }
        // Consume the id.
        let id_start = i + 1;
        let mut id_end = id_start;
        while id_end < bytes.len() && is_id_byte(bytes[id_end]) {
            id_end += 1;
        }
        if id_end == id_start {
            i += 1;
            continue;
        }
        // Must be followed by end-of-line or whitespace — `^foo!`
        // isn't a clean anchor token.
        if id_end < bytes.len() {
            let next = bytes[id_end];
            if !matches!(next, b'\n' | b'\r' | b' ' | b'\t') {
                i = id_end;
                continue;
            }
        }
        let id = std::str::from_utf8(&bytes[id_start..id_end])
            .expect("ASCII id bytes")
            .to_string();
        out.push((i, id));
        i = id_end;
    }
    out
}

fn might_contain_anchor(source: &str) -> bool {
    // Two bytes minimum (`^x`); fast scan for `^` followed by an
    // id byte.
    let bytes = source.as_bytes();
    let mut i = 0;
    while i + 1 < bytes.len() {
        if bytes[i] == b'^' && is_id_byte(bytes[i + 1]) {
            return true;
        }
        i += 1;
    }
    false
}

#[inline]
fn is_id_byte(b: u8) -> bool {
    b.is_ascii_alphanumeric() || b == b'_' || b == b'-'
}

fn start_of_line(source: &str, byte: usize) -> usize {
    let bytes = source.as_bytes();
    let mut i = byte;
    while i > 0 && bytes[i - 1] != b'\n' {
        i -= 1;
    }
    i
}

fn end_of_line(source: &str, byte: usize) -> usize {
    let bytes = source.as_bytes();
    let mut i = byte;
    while i < bytes.len() && bytes[i] != b'\n' {
        i += 1;
    }
    i
}

fn line_range(source: &str, byte_start: usize, byte_end: usize) -> (u32, u32) {
    let bytes = source.as_bytes();
    let mut line: u32 = 1;
    let mut start_line: u32 = 1;
    for (idx, b) in bytes.iter().enumerate() {
        if idx == byte_start {
            start_line = line;
        }
        if idx == byte_end.saturating_sub(1) {
            return (start_line, line);
        }
        if *b == b'\n' {
            line += 1;
        }
    }
    (start_line, line)
}

fn preview_for(text: &str) -> String {
    const CAP: usize = 120;
    let trimmed = text.trim();
    let normalised: String = trimmed.split_whitespace().collect::<Vec<_>>().join(" ");
    if normalised.chars().count() <= CAP {
        normalised
    } else {
        let mut out: String = normalised.chars().take(CAP).collect();
        out.push('…');
        out
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn ids(source: &str) -> Vec<String> {
        extract_blocks(source)
            .into_iter()
            .map(|b| b.block_id)
            .collect()
    }

    #[test]
    fn empty_source_returns_no_blocks() {
        assert!(extract_blocks("").is_empty());
    }

    #[test]
    fn plain_text_with_no_anchors_short_circuits() {
        let src = "Just a paragraph.\n\nAnother paragraph.\n";
        assert!(extract_blocks(src).is_empty());
    }

    #[test]
    fn trailing_anchor_on_paragraph_is_extracted() {
        let src = "Hello world ^my-block\n";
        let blocks = extract_blocks(src);
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].block_id, "my-block");
        assert_eq!(blocks[0].kind, BlockKind::Paragraph);
        assert_eq!(blocks[0].line_start, 1);
    }

    #[test]
    fn standalone_anchor_below_paragraph_is_extracted() {
        let src = "Hello world\n^standalone-id\n";
        let blocks = extract_blocks(src);
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].block_id, "standalone-id");
        assert_eq!(blocks[0].kind, BlockKind::Paragraph);
    }

    #[test]
    fn anchor_inside_code_fence_is_ignored() {
        let src = "```\nfn main() { ^not-a-block; }\n```\n\nReal ^real-id\n";
        assert_eq!(ids(src), vec!["real-id"]);
    }

    #[test]
    fn anchor_inside_frontmatter_is_ignored() {
        let src = "---\nfoo: bar\nanchor: ^ignored\n---\n\nBody ^kept\n";
        assert_eq!(ids(src), vec!["kept"]);
    }

    #[test]
    fn duplicate_anchor_keeps_the_first() {
        let src = "First ^dup\n\nSecond ^dup\n";
        let blocks = extract_blocks(src);
        assert_eq!(blocks.len(), 1);
        // The first occurrence's block is what survived.
        assert!(blocks[0].text_preview.contains("First"));
    }

    #[test]
    fn list_item_anchor_kind_is_list_item() {
        let src = "- alpha\n- beta ^bee\n- gamma\n";
        let blocks = extract_blocks(src);
        let bee = blocks.iter().find(|b| b.block_id == "bee").unwrap();
        assert_eq!(bee.kind, BlockKind::ListItem);
    }

    #[test]
    fn blockquote_anchor_kind_is_blockquote() {
        let src = "> quoted line one\n> quoted line two ^bq\n";
        let blocks = extract_blocks(src);
        assert_eq!(blocks.len(), 1);
        assert_eq!(blocks[0].block_id, "bq");
        assert_eq!(blocks[0].kind, BlockKind::BlockQuote);
    }

    #[test]
    fn anchor_at_eof_without_trailing_newline_parses() {
        let src = "Hello ^trailing";
        assert_eq!(ids(src), vec!["trailing"]);
    }

    #[test]
    fn caret_in_middle_of_word_is_ignored() {
        let src = "x42^17 is not a block.\n";
        assert!(extract_blocks(src).is_empty());
    }

    #[test]
    fn byte_range_includes_trailing_anchor() {
        let src = "Hello world ^my-block\n\nNext para.\n";
        let blocks = extract_blocks(src);
        let b = &blocks[0];
        let slice = &src[b.byte_start as usize..b.byte_end as usize];
        assert!(slice.contains("^my-block"), "got: {slice:?}");
    }

    #[test]
    fn preview_truncates_at_120_chars() {
        let long_text = "x".repeat(200);
        let src = format!("{long_text} ^id\n");
        let blocks = extract_blocks(&src);
        assert_eq!(blocks.len(), 1);
        let preview = &blocks[0].text_preview;
        // 120 chars + the ellipsis.
        assert_eq!(preview.chars().count(), 121);
        assert!(preview.ends_with('…'));
    }
}
