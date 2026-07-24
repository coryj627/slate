// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Embed resolution: turn a `![[target]]` / `![alt](src)` reference
//! into the text or bytes the UI needs to render.
//!
//! Five resolution shapes — full note, section under a heading,
//! block under a `^id`, image, unresolved. Note embeds carry their
//! own nested embeds pre-resolved up to depth 3 so the UI doesn't
//! have to track recursion.
//!
//! See `docs/plans/05_locked_architecture_decisions.md` §4 for the
//! API surface this module exposes.

use std::path::Path;

/// Maximum embed-recursion depth. Each `FullNote` / `Section`
/// resolution can carry nested embeds resolved up to this depth;
/// anything deeper falls back to `EmbedResolution::Unresolved {
/// DepthLimitReached }`. Three is the same default Obsidian uses
/// for embedded-note rendering.
pub const MAX_EMBED_DEPTH: u32 = 3;

/// Resource bounds for transient editor previews. The normal reading-surface
/// resolver remains lossless; preview callers use a separate bounded entry
/// point so a broad nested graph cannot be materialized across FFI.
pub const MAX_EMBED_PREVIEW_TEXT_BYTES: usize = 64 * 1024;
pub const MAX_EMBED_PREVIEW_NODES: usize = 128;
pub const MAX_EMBED_PREVIEW_IMAGE_BYTES: u64 = 8 * 1024 * 1024;

#[derive(Debug, Clone, PartialEq)]
pub struct EmbedPreviewResolution {
    pub resolution: EmbedResolution,
    pub truncated: bool,
}

pub(crate) enum EmbedResolveBudget {
    Unlimited,
    Preview {
        remaining_text_bytes: usize,
        remaining_nodes: usize,
        remaining_image_bytes: u64,
        truncated: bool,
    },
}

impl EmbedResolveBudget {
    pub(crate) fn preview() -> Self {
        Self::Preview {
            remaining_text_bytes: MAX_EMBED_PREVIEW_TEXT_BYTES,
            remaining_nodes: MAX_EMBED_PREVIEW_NODES,
            remaining_image_bytes: MAX_EMBED_PREVIEW_IMAGE_BYTES,
            truncated: false,
        }
    }

    pub(crate) fn is_preview(&self) -> bool {
        matches!(self, Self::Preview { .. })
    }

    pub(crate) fn text_limit(&self, fallback: u64) -> u64 {
        match self {
            Self::Unlimited => fallback,
            Self::Preview {
                remaining_text_bytes,
                ..
            } => *remaining_text_bytes as u64,
        }
    }

    pub(crate) fn consume_text(&mut self, bytes: usize, was_truncated: bool) {
        if let Self::Preview {
            remaining_text_bytes,
            truncated,
            ..
        } = self
        {
            *remaining_text_bytes = remaining_text_bytes.saturating_sub(bytes);
            *truncated |= was_truncated;
        }
    }

    pub(crate) fn consume_node(&mut self) -> bool {
        match self {
            Self::Unlimited => true,
            Self::Preview {
                remaining_nodes, ..
            } if *remaining_nodes > 0 => {
                *remaining_nodes -= 1;
                true
            }
            Self::Preview { truncated, .. } => {
                *truncated = true;
                false
            }
        }
    }

    pub(crate) fn image_limit(&self, fallback: u64) -> u64 {
        match self {
            Self::Unlimited => fallback,
            Self::Preview {
                remaining_image_bytes,
                ..
            } => (*remaining_image_bytes).min(fallback),
        }
    }

    pub(crate) fn consume_image(&mut self, bytes: u64) {
        if let Self::Preview {
            remaining_image_bytes,
            ..
        } = self
        {
            *remaining_image_bytes = remaining_image_bytes.saturating_sub(bytes);
        }
    }

    pub(crate) fn mark_truncated(&mut self) {
        if let Self::Preview { truncated, .. } = self {
            *truncated = true;
        }
    }

    pub(crate) fn truncated(&self) -> bool {
        matches!(
            self,
            Self::Preview {
                truncated: true,
                ..
            }
        )
    }
}

/// The full resolution of one `![[…]]` reference.
#[derive(Debug, Clone, PartialEq)]
pub enum EmbedResolution {
    /// Embed of an entire note. `text` is the post-frontmatter body
    /// of the note; the UI re-renders it (and the pre-resolved
    /// `nested` embeds inline).
    FullNote {
        target_path: String,
        text: String,
        nested: Vec<NestedEmbed>,
    },
    /// Embed of one section (heading + body until the next same-or-
    /// higher heading).
    Section {
        target_path: String,
        heading: String,
        text: String,
        nested: Vec<NestedEmbed>,
    },
    /// Embed of one `^block-id`-marked block.
    Block {
        target_path: String,
        block_id: String,
        text: String,
    },
    /// Embed of an image attachment.
    Image {
        target_path: String,
        bytes: Vec<u8>,
        mime: String,
        alt: Option<String>,
    },
    Unresolved {
        reason: EmbedUnresolvedReason,
    },
}

/// One pre-resolved embed nested inside another `FullNote` or
/// `Section` resolution. The byte offset is relative to the parent
/// resolution's `text` so the UI can splice the rendered embed
/// into the right place when it re-renders the parent.
#[derive(Debug, Clone, PartialEq)]
pub struct NestedEmbed {
    pub raw_target: String,
    pub byte_offset_in_parent: u32,
    /// Exclusive exact authored embed span end in the parent UTF-8 source.
    pub byte_end_in_parent: u32,
    pub resolution: EmbedResolution,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum EmbedUnresolvedReason {
    TargetNotFound {
        target: String,
    },
    HeadingNotFound {
        target_path: String,
        heading: String,
    },
    BlockNotFound {
        target_path: String,
        block_id: String,
    },
    /// The recursion limit (`MAX_EMBED_DEPTH`) was reached. The UI
    /// renders a "Depth limit reached" marker; the underlying
    /// embed would resolve in isolation.
    DepthLimitReached,
    /// IO / size-limit / decoding failure. `message` is the
    /// human-readable cause; the UI surfaces it inline.
    ReadError {
        message: String,
    },
}

/// Raw bytes returned by `read_attachment`. The MIME type is
/// inferred from the extension first, with a magic-bytes fallback
/// for the common image formats.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct AttachmentBytes {
    pub bytes: Vec<u8>,
    pub mime: String,
}

/// Treat any vault-relative path with one of these extensions as
/// an image embed. Order doesn't matter; the match is
/// case-insensitive.
pub(crate) const IMAGE_EXTENSIONS: &[&str] = &[
    "png", "jpg", "jpeg", "gif", "svg", "webp", "heic", "bmp", "tiff", "tif",
];

/// `true` when `path`'s extension marks it as an image.
pub(crate) fn looks_like_image(path: &str) -> bool {
    let Some(ext) = Path::new(path).extension().and_then(|s| s.to_str()) else {
        return false;
    };
    let lower = ext.to_ascii_lowercase();
    IMAGE_EXTENSIONS.contains(&lower.as_str())
}

/// Infer the MIME type for a byte slice + its on-disk path. Looks
/// at the extension first (fast, no IO), falls back to magic bytes
/// for the common image formats, finally returns
/// `application/octet-stream`.
pub(crate) fn infer_mime(path: &str, bytes: &[u8]) -> String {
    if let Some(ext) = Path::new(path).extension().and_then(|s| s.to_str()) {
        match ext.to_ascii_lowercase().as_str() {
            "png" => return "image/png".to_string(),
            "jpg" | "jpeg" => return "image/jpeg".to_string(),
            "gif" => return "image/gif".to_string(),
            "svg" => return "image/svg+xml".to_string(),
            "webp" => return "image/webp".to_string(),
            "heic" => return "image/heic".to_string(),
            "bmp" => return "image/bmp".to_string(),
            "tiff" | "tif" => return "image/tiff".to_string(),
            _ => {}
        }
    }
    // Magic-bytes fallback for the formats whose signatures are
    // both short and unambiguous. Anything we can't classify lands
    // as `application/octet-stream`.
    if bytes.starts_with(&[0x89, b'P', b'N', b'G', 0x0D, 0x0A, 0x1A, 0x0A]) {
        return "image/png".to_string();
    }
    if bytes.starts_with(&[0xFF, 0xD8, 0xFF]) {
        return "image/jpeg".to_string();
    }
    if bytes.starts_with(b"GIF87a") || bytes.starts_with(b"GIF89a") {
        return "image/gif".to_string();
    }
    if bytes.len() >= 12 && &bytes[0..4] == b"RIFF" && &bytes[8..12] == b"WEBP" {
        return "image/webp".to_string();
    }
    // SVG is text; sniff for the opening `<svg` tag in the first
    // 1 KiB to avoid full XML parsing.
    if bytes.len() < 1024 {
        if bytes_contain_token(bytes, b"<svg") {
            return "image/svg+xml".to_string();
        }
    } else if bytes_contain_token(&bytes[..1024], b"<svg") {
        return "image/svg+xml".to_string();
    }
    "application/octet-stream".to_string()
}

fn bytes_contain_token(haystack: &[u8], needle: &[u8]) -> bool {
    haystack
        .windows(needle.len())
        .any(|w| w.eq_ignore_ascii_case(needle))
}

/// Parse the embed target string into the note prefix + optional
/// anchor. `Foo` → (`Foo`, None); `Foo#H` → (`Foo`, Some(Heading));
/// `Foo#^id` → (`Foo`, Some(Block)) — Obsidian's canonical block-ref
/// syntax (#413); `Foo^id` → (`Foo`, Some(Block)) — the bare legacy
/// form M10 shipped with. A `#` not immediately followed by `^` is a
/// heading anchor, including `Foo#H^x` (Obsidian reads that as a
/// heading with a literal `^x` suffix).
pub(crate) fn parse_embed_target(target: &str) -> (&str, Option<EmbedAnchor<'_>>) {
    if let Some(idx) = target.find('#') {
        let rest = &target[idx + 1..];
        if let Some(block_id) = rest.strip_prefix('^') {
            return (&target[..idx], Some(EmbedAnchor::Block(block_id)));
        }
        return (&target[..idx], Some(EmbedAnchor::Heading(rest)));
    }
    if let Some(idx) = target.find('^') {
        return (&target[..idx], Some(EmbedAnchor::Block(&target[idx + 1..])));
    }
    (target, None)
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum EmbedAnchor<'a> {
    Heading(&'a str),
    Block(&'a str),
}

/// Strip the YAML frontmatter block (if any) from `source`,
/// returning the body slice. Reads the embed's "content" exactly
/// as the user sees it in the note pane. Thin shim over the
/// general-purpose [`frontmatter::body_after_frontmatter`] so the
/// "what counts as frontmatter" definition stays in one place.
pub(crate) fn strip_frontmatter_for_embed(source: &str) -> &str {
    crate::frontmatter::body_after_frontmatter(source)
}

/// Extract the section of `source` headed by `heading_name`. The
/// section runs from the matching heading line to the next heading
/// at the same level or higher (or EOF).
///
/// `heading_name` matches the heading's plain text after pulldown-
/// cmark's text-event coalescing — case-insensitive, leading/
/// trailing whitespace trimmed. Returns `None` when no heading
/// matches.
#[cfg(test)]
pub(crate) fn extract_section(source: &str, heading_name: &str) -> Option<(String, String)> {
    use pulldown_cmark::{Event, Parser, Tag, TagEnd};

    let target_norm: String = heading_name.trim().to_lowercase();

    let mut headings: Vec<HeadingInfo> = Vec::new();
    let mut cur_text = String::new();
    let mut cur_level: Option<u8> = None;
    let mut cur_start: usize = 0;

    for (event, range) in Parser::new(source).into_offset_iter() {
        match event {
            Event::Start(Tag::Heading { level, .. }) => {
                cur_level = Some(heading_level_u8(level));
                cur_start = range.start;
                cur_text.clear();
            }
            Event::Text(s) | Event::Code(s) if cur_level.is_some() => {
                cur_text.push_str(&s);
            }
            Event::End(TagEnd::Heading(_)) => {
                if let Some(level) = cur_level.take() {
                    headings.push(HeadingInfo {
                        level,
                        text: cur_text.clone(),
                        byte_start: cur_start,
                    });
                    cur_text.clear();
                }
            }
            _ => {}
        }
    }

    // Find the matched heading (first one whose normalised text
    // equals `target_norm`).
    let matched_idx = headings
        .iter()
        .position(|h| h.text.trim().to_lowercase() == target_norm)?;
    let matched = &headings[matched_idx];

    // Section end: the next heading at same-or-higher level, or
    // EOF.
    let end_byte = headings[matched_idx + 1..]
        .iter()
        .find(|h| h.level <= matched.level)
        .map(|h| h.byte_start)
        .unwrap_or(source.len());

    let text = source[matched.byte_start..end_byte].to_string();
    Some((matched.text.clone(), text))
}

#[cfg(test)]
struct HeadingInfo {
    level: u8,
    text: String,
    byte_start: usize,
}

#[cfg(test)]
fn heading_level_u8(level: pulldown_cmark::HeadingLevel) -> u8 {
    use pulldown_cmark::HeadingLevel::*;
    match level {
        H1 => 1,
        H2 => 2,
        H3 => 3,
        H4 => 4,
        H5 => 5,
        H6 => 6,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn extension_is_used_first_for_mime() {
        assert_eq!(infer_mime("foo.png", &[0; 0]), "image/png");
        assert_eq!(infer_mime("foo.jpg", &[0; 0]), "image/jpeg");
        assert_eq!(infer_mime("FOO.JPEG", &[0; 0]), "image/jpeg");
    }

    #[test]
    fn magic_bytes_classify_png_without_extension() {
        let bytes = [0x89, b'P', b'N', b'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0];
        assert_eq!(infer_mime("noext", &bytes), "image/png");
    }

    #[test]
    fn magic_bytes_classify_jpeg_without_extension() {
        let bytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];
        assert_eq!(infer_mime("noext", &bytes), "image/jpeg");
    }

    #[test]
    fn magic_bytes_classify_gif() {
        let bytes = b"GIF89a";
        assert_eq!(infer_mime("noext", bytes), "image/gif");
    }

    #[test]
    fn magic_bytes_classify_webp() {
        let mut bytes = Vec::from(b"RIFF\0\0\0\0WEBP");
        bytes.extend_from_slice(&[0; 4]);
        assert_eq!(infer_mime("noext", &bytes), "image/webp");
    }

    #[test]
    fn magic_bytes_classify_svg_inline() {
        let bytes = b"<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>";
        assert_eq!(infer_mime("noext", bytes), "image/svg+xml");
    }

    #[test]
    fn unknown_extension_unknown_magic_falls_back_to_octet_stream() {
        assert_eq!(
            infer_mime("foo.bin", b"random junk"),
            "application/octet-stream"
        );
    }

    #[test]
    fn looks_like_image_recognises_common_extensions() {
        assert!(looks_like_image("photo.png"));
        assert!(looks_like_image("photo.PNG"));
        assert!(looks_like_image("attachments/cover.jpeg"));
        assert!(!looks_like_image("note.md"));
        assert!(!looks_like_image("notes/sub/foo"));
    }

    #[test]
    fn parse_target_splits_on_heading_anchor() {
        let (prefix, anchor) = parse_embed_target("Foo Bar#H1");
        assert_eq!(prefix, "Foo Bar");
        assert_eq!(anchor, Some(EmbedAnchor::Heading("H1")));
    }

    #[test]
    fn parse_target_splits_on_obsidian_block_anchor() {
        // `#^` is Obsidian's canonical block-ref form (#413) — it
        // must parse as a Block anchor, never Heading("^id").
        let (prefix, anchor) = parse_embed_target("Whipped cream#^method-step-2");
        assert_eq!(prefix, "Whipped cream");
        assert_eq!(anchor, Some(EmbedAnchor::Block("method-step-2")));
    }

    #[test]
    fn parse_target_heading_with_caret_suffix_stays_heading() {
        // Obsidian reads `note#heading^x` as a heading anchor with a
        // literal `^x` suffix — only an immediate `#^` is a block ref.
        let (prefix, anchor) = parse_embed_target("Note#Section^tail");
        assert_eq!(prefix, "Note");
        assert_eq!(anchor, Some(EmbedAnchor::Heading("Section^tail")));
    }

    #[test]
    fn parse_target_splits_on_block_anchor() {
        let (prefix, anchor) = parse_embed_target("Note^my-block");
        assert_eq!(prefix, "Note");
        assert_eq!(anchor, Some(EmbedAnchor::Block("my-block")));
    }

    #[test]
    fn parse_target_no_anchor() {
        let (prefix, anchor) = parse_embed_target("Just A Note");
        assert_eq!(prefix, "Just A Note");
        assert_eq!(anchor, None);
    }

    #[test]
    fn strip_frontmatter_removes_yaml_block() {
        let src = "---\ntitle: Hi\n---\n# Heading\nbody\n";
        assert_eq!(strip_frontmatter_for_embed(src), "# Heading\nbody\n");
    }

    #[test]
    fn strip_frontmatter_passthrough_when_no_block() {
        let src = "# Heading\nbody\n";
        assert_eq!(strip_frontmatter_for_embed(src), src);
    }

    #[test]
    fn extract_section_returns_body_until_next_same_level_heading() {
        let src = "# H1\nfirst\n\n## H2\ntext under H2\n\n## H2b\nmore\n\n# H1 second\nlast\n";
        let (name, text) = extract_section(src, "H2").unwrap();
        assert_eq!(name, "H2");
        assert!(text.starts_with("## H2"));
        assert!(text.contains("text under H2"));
        assert!(
            !text.contains("H2b"),
            "section must end at the next same-level heading; got {text:?}"
        );
    }

    #[test]
    fn extract_section_runs_to_eof_when_no_next_heading() {
        let src = "# H1\nbody\n## H2\nfinal section text\n";
        let (name, text) = extract_section(src, "H2").unwrap();
        assert_eq!(name, "H2");
        assert!(text.contains("final section text"));
    }

    #[test]
    fn extract_section_returns_none_for_missing_heading() {
        let src = "# H1\nbody\n";
        assert!(extract_section(src, "Nonexistent").is_none());
    }

    #[test]
    fn extract_section_matches_case_insensitively() {
        let src = "# Welcome\nbody\n";
        assert!(extract_section(src, "welcome").is_some());
        assert!(extract_section(src, "WELCOME").is_some());
    }
}
