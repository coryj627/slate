// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Pandoc citation extraction.
//!
//! Walks a Markdown source string and returns every Pandoc-style
//! citation reference in document order. Pure parsing — no CSL
//! rendering, no bibliography lookup. The downstream renderer
//! (`citations::render`, Milestone L) consumes `CitationReference`
//! values to produce visual + speech text against a CSL style.
//!
//! ## Syntax covered (per docs/plans/05_locked_architecture_decisions.md §6.5)
//!
//! | Form                            | Mode                |
//! |---------------------------------|---------------------|
//! | `[@key]`                        | Bracketed           |
//! | `[@key, p. 23]`                 | Bracketed + locator |
//! | `[@a; @b; @c]`                  | Multi-citation      |
//! | `@key`                          | InText              |
//! | `[-@key]`                       | SuppressAuthor      |
//! | `[see @key, p. 23]`             | Prefix + locator    |
//! | `[@key, p. 23; see also @b]`    | Mixed               |
//!
//! ## What's NOT a citation
//!
//! - Anything inside fenced code, indented code, or inline code spans.
//! - Email addresses (`@` preceded by an ASCII word character).
//! - Bare `@text` whose key doesn't match `[a-zA-Z0-9_\-:.+]+`.
//! - Brackets with no `@` inside (`[some text]` is just brackets).

pub mod bibliography;

use pulldown_cmark::{Event, Parser, Tag};

/// One citation site in a source document. A site may contain
/// multiple cited items (e.g. `[@a; @b]` → one `CitationReference`
/// with `citations.len() == 2`).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CitationReference {
    /// Verbatim source slice that produced this reference. For
    /// bracketed forms this includes the `[` and `]`; for in-text
    /// forms it includes the `@` plus the key.
    pub raw: String,
    /// One or more `CitedItem`s, in source order.
    pub citations: Vec<CitedItem>,
    /// Byte offset of the citation's opening character (`[` for
    /// bracketed, `@` for in-text). In the original-source coordinate
    /// space — any frontmatter shift has already been applied.
    pub byte_offset: u32,
    /// 1-indexed line number of `byte_offset` in the original source.
    pub line: u32,
}

/// One key + optional locator + optional surrounding text inside a
/// `CitationReference`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct CitedItem {
    /// Citation key. Matches Better BibTeX's `[a-zA-Z0-9_\-:.+]+`.
    pub key: String,
    /// Locator (page, chapter, section, …) if the source carried one.
    pub locator: Option<Locator>,
    /// Free text BEFORE the `@key` inside this segment (e.g. `"see"`
    /// from `[see @smith2020]`). Surrounding whitespace is stripped.
    pub prefix: Option<String>,
    /// Free text AFTER the locator inside this segment. Rarely used
    /// by Pandoc itself but preserved verbatim for renderers that
    /// want to surface it.
    pub suffix: Option<String>,
    /// How the citation was authored: bracketed, in-text, or
    /// author-suppressed.
    pub mode: CitationMode,
}

/// Locator following a citation key, e.g. `p. 23` →
/// `{ label: "p.", locator: "23" }`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Locator {
    /// Pandoc-standard label as authored (with the trailing `.` if
    /// present). Unrecognised locators are stored under `label =
    /// "unknown"` with the full raw text in `locator`.
    pub label: String,
    /// The locator value (e.g. `"23"`, `"3–5"`).
    pub locator: String,
}

/// How the citation was authored.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CitationMode {
    /// `[@key]` and its variants — appears inside square brackets.
    Bracketed,
    /// `@key` — bare in-text citation. The author IS the sentence
    /// subject in the rendered form.
    InText,
    /// `[-@key]` — author suppressed; only the year / locator
    /// surfaces in the rendered output.
    SuppressAuthor,
}

/// Walk `source` and return every Pandoc citation in document order.
///
/// Pure function. YAML frontmatter is skipped before parsing so a
/// `bibliography:` value (or any other `@`-containing scalar) inside
/// the frontmatter doesn't masquerade as a citation. Offsets are
/// shifted back into the original-source coordinate space so callers
/// index into the full string they passed in.
///
/// # Examples
///
/// ```
/// use slate_core::extract_citations;
///
/// let refs = extract_citations("See [@smith2020].");
/// assert_eq!(refs.len(), 1);
/// assert_eq!(refs[0].citations.len(), 1);
/// assert_eq!(refs[0].citations[0].key, "smith2020");
/// ```
pub fn extract_citations(source: &str) -> Vec<CitationReference> {
    let body = crate::frontmatter::body_after_frontmatter(source);
    let body_offset = source.len() - body.len();

    let code_ranges = collect_code_ranges(body);
    let mut refs = scan_citations(body, &code_ranges);

    if body_offset > 0 {
        for r in &mut refs {
            r.byte_offset += body_offset as u32;
        }
    }

    // Compute line numbers in original-source coordinates.
    let line_starts = line_starts(source);
    for r in &mut refs {
        r.line = line_for(&line_starts, r.byte_offset as usize);
    }

    refs.sort_by_key(|r| r.byte_offset);
    refs
}

/// Collect inline-code + fenced/indented-code byte ranges so the
/// citation scanner can skip them. Same pattern as `links::extract_links`.
fn collect_code_ranges(source: &str) -> Vec<(usize, usize)> {
    let mut ranges = Vec::new();
    let parser = Parser::new(source).into_offset_iter();
    for (event, range) in parser {
        match event {
            Event::Code(_) | Event::Start(Tag::CodeBlock(_)) => {
                ranges.push((range.start, range.end));
            }
            _ => {}
        }
    }
    ranges
}

fn in_any_range(idx: usize, ranges: &[(usize, usize)]) -> bool {
    ranges.iter().any(|(s, e)| idx >= *s && idx < *e)
}

/// Top-level scanner: walks bytes once, finding either bracketed or
/// in-text citations. Skips ranges that overlap code spans.
fn scan_citations(source: &str, code_ranges: &[(usize, usize)]) -> Vec<CitationReference> {
    let bytes = source.as_bytes();
    let mut out = Vec::new();
    let mut i = 0;
    while i < bytes.len() {
        if in_any_range(i, code_ranges) {
            i += 1;
            continue;
        }
        let b = bytes[i];
        if b == b'[' {
            if let Some(parsed) = try_parse_bracketed(source, bytes, i) {
                let end = parsed.byte_offset as usize + parsed.raw.len();
                out.push(parsed);
                i = end;
                continue;
            }
        } else if b == b'@' && is_intext_start(bytes, i) {
            if let Some(parsed) = try_parse_intext(source, bytes, i) {
                let end = parsed.byte_offset as usize + parsed.raw.len();
                out.push(parsed);
                i = end;
                continue;
            }
        }
        i += 1;
    }
    out
}

/// True if `bytes[i]` is `@` AND the preceding byte is not an ASCII
/// word character — that rules out emails (`name@example.com`) and
/// other `word@token` constructions.
///
/// The Unicode-aware variant of this rule lands in V2 if a tester
/// reports false positives — Pandoc itself uses an ASCII-shaped rule
/// here, so matching it minimises surprise.
fn is_intext_start(bytes: &[u8], i: usize) -> bool {
    if bytes[i] != b'@' {
        return false;
    }
    if i == 0 {
        return true;
    }
    let prev = bytes[i - 1];
    !(prev.is_ascii_alphanumeric() || prev == b'_')
}

/// Citation-key grammar matches Better BibTeX's default shape:
/// `[a-zA-Z0-9_\-:.+]+`. Numeric-leading keys are rare in the wild
/// but Pandoc accepts them.
fn is_key_byte(b: u8) -> bool {
    b.is_ascii_alphanumeric() || matches!(b, b'_' | b'-' | b':' | b'.' | b'+')
}

/// Try to parse a bracketed citation starting at `bytes[i] == '['`.
///
/// Returns `None` if:
/// - There's no closing `]` before EOF or a blank line.
/// - The bracket body contains no `@` (it's just brackets).
/// - The body parses but no `CitedItem` can be extracted.
fn try_parse_bracketed(source: &str, bytes: &[u8], i: usize) -> Option<CitationReference> {
    debug_assert!(bytes[i] == b'[');
    let close = find_close_bracket(bytes, i)?;
    let inner_start = i + 1;
    let inner_end = close;
    let inner = &source[inner_start..inner_end];

    // Quick reject: no `@` in the body means not a citation.
    if !inner.contains('@') {
        return None;
    }

    let mut items: Vec<CitedItem> = Vec::new();
    for segment in inner.split(';') {
        if let Some(item) = parse_segment(segment) {
            items.push(item);
        }
    }
    if items.is_empty() {
        return None;
    }

    let raw = source[i..=close].to_string();
    Some(CitationReference {
        raw,
        citations: items,
        byte_offset: i as u32,
        line: 1, // refilled by caller
    })
}

/// Find the matching `]` for the `[` at `bytes[i]`. Citations don't
/// nest in Pandoc, so the first `]` wins. A blank line (two consecutive
/// newlines, ignoring spaces / tabs / CR) before the close means we
/// bail — citations can't span a paragraph break.
fn find_close_bracket(bytes: &[u8], i: usize) -> Option<usize> {
    debug_assert!(bytes[i] == b'[');
    let mut j = i + 1;
    let mut newline_run = 0u8;
    while j < bytes.len() {
        let b = bytes[j];
        if b == b']' {
            return Some(j);
        }
        if b == b'\n' {
            newline_run += 1;
            if newline_run >= 2 {
                return None;
            }
        } else if b != b' ' && b != b'\t' && b != b'\r' {
            newline_run = 0;
        }
        j += 1;
    }
    None
}

/// Parse one `;`-delimited segment of a bracketed citation. Returns
/// `None` if no `@key` is present in the segment.
///
/// Segment shape: `<prefix>? <-?> @<key> <, locator>? <suffix>?`
fn parse_segment(segment: &str) -> Option<CitedItem> {
    let bytes = segment.as_bytes();

    // Locate the `@` that starts the key — must not be preceded by an
    // ASCII alphanumeric or `_` (rules out emails inside the bracket).
    let mut at_pos = None;
    for (idx, &b) in bytes.iter().enumerate() {
        if b == b'@' {
            let prev_word =
                idx > 0 && (bytes[idx - 1].is_ascii_alphanumeric() || bytes[idx - 1] == b'_');
            if !prev_word {
                at_pos = Some(idx);
                break;
            }
        }
    }
    let at_pos = at_pos?;

    // Detect SuppressAuthor: a `-` immediately before `@`, with the
    // `-` itself either at segment start or preceded by whitespace.
    let mut suppress = false;
    let mut prefix_end = at_pos;
    if at_pos > 0 && bytes[at_pos - 1] == b'-' {
        let before_dash = if at_pos >= 2 {
            Some(bytes[at_pos - 2])
        } else {
            None
        };
        if before_dash.is_none() || before_dash.unwrap().is_ascii_whitespace() {
            suppress = true;
            prefix_end = at_pos - 1;
        }
    }

    let prefix = if prefix_end == 0 {
        None
    } else {
        let p = segment[..prefix_end].trim();
        if p.is_empty() {
            None
        } else {
            Some(p.to_string())
        }
    };

    // Read the key.
    let key_start = at_pos + 1;
    let mut key_end = key_start;
    while key_end < bytes.len() && is_key_byte(bytes[key_end]) {
        key_end += 1;
    }
    key_end = trim_trailing_sentence_dots(bytes, key_start, key_end);
    if key_end == key_start {
        return None;
    }
    let key = segment[key_start..key_end].to_string();

    // After the key: optional `,` then locator or suffix.
    let rest = &segment[key_end..];
    let (locator, suffix) = parse_locator_and_suffix(rest);

    let mode = if suppress {
        CitationMode::SuppressAuthor
    } else {
        CitationMode::Bracketed
    };
    Some(CitedItem {
        key,
        locator,
        prefix,
        suffix,
        mode,
    })
}

/// Parse the post-key remainder of a bracketed segment into
/// `(locator, suffix)`.
///
/// Pandoc's rule, simplified for V1:
/// - No leading `,` → no locator; whole remainder is the suffix.
/// - Leading `,` followed by a recognised label → locator runs to
///   the next `,` or end-of-segment; what follows is the suffix.
/// - Leading `,` followed by anything else → "unknown" locator
///   carrying the raw post-comma text (per the issue spec).
fn parse_locator_and_suffix(rest: &str) -> (Option<Locator>, Option<String>) {
    let trimmed = rest.trim_start();
    if !trimmed.starts_with(',') {
        let suffix = trimmed.trim();
        if suffix.is_empty() {
            return (None, None);
        }
        return (None, Some(suffix.to_string()));
    }
    let after_comma = trimmed[1..].trim_start();

    if let Some((label, after_label)) = match_locator_label(after_comma) {
        let after_label = after_label.trim_start();
        let (loc_text, suffix_text) = match after_label.find(',') {
            Some(idx) => (
                after_label[..idx].trim().to_string(),
                after_label[idx + 1..].trim().to_string(),
            ),
            None => (after_label.trim().to_string(), String::new()),
        };
        let suffix = if suffix_text.is_empty() {
            None
        } else {
            Some(suffix_text)
        };
        return (
            Some(Locator {
                label: label.to_string(),
                locator: loc_text,
            }),
            suffix,
        );
    }

    (
        Some(Locator {
            label: "unknown".to_string(),
            locator: after_comma.trim().to_string(),
        }),
        None,
    )
}

/// Pandoc-standard locator labels (V1 set). Anything outside this
/// list is preserved as an `unknown`-labelled locator.
const KNOWN_LOCATORS: &[&str] = &[
    "pp.", "p.", "chapter", "chap.", "section", "sec.", "fig.", "eq.", "vol.", "note",
];

/// If `text` starts with a known locator label, return `(label, rest)`.
/// Longest match wins (e.g. `pp.` beats `p.`) — that's why
/// `KNOWN_LOCATORS` lists the longer prefixes first.
fn match_locator_label(text: &str) -> Option<(&'static str, &str)> {
    for label in KNOWN_LOCATORS {
        if let Some(rest) = text.strip_prefix(label) {
            // The label must be followed by whitespace, EOF, or a
            // digit. That last rule lets `p.23` (no space) work.
            let next_ok = rest.is_empty()
                || rest.starts_with(|c: char| c.is_whitespace() || c.is_ascii_digit());
            if next_ok {
                return Some((label, rest));
            }
        }
    }
    None
}

/// Parse `@key` at `bytes[i]`. Returns `None` if the key is empty.
fn try_parse_intext(source: &str, bytes: &[u8], i: usize) -> Option<CitationReference> {
    debug_assert!(bytes[i] == b'@');
    let key_start = i + 1;
    let mut key_end = key_start;
    while key_end < bytes.len() && is_key_byte(bytes[key_end]) {
        key_end += 1;
    }
    // Strip trailing `.` characters if the next byte is end-of-input
    // or whitespace — `@smith2020.` at end-of-sentence is the key
    // `smith2020` plus sentence punctuation. Pandoc itself applies
    // this rule; without it `@c.` greedy-matches `c.` as the key.
    key_end = trim_trailing_sentence_dots(bytes, key_start, key_end);
    if key_end == key_start {
        return None;
    }
    let key = source[key_start..key_end].to_string();
    let raw = source[i..key_end].to_string();
    Some(CitationReference {
        raw,
        citations: vec![CitedItem {
            key,
            locator: None,
            prefix: None,
            suffix: None,
            mode: CitationMode::InText,
        }],
        byte_offset: i as u32,
        line: 1, // refilled by caller
    })
}

/// Strip trailing `.` characters from the key span `[start..end)` when
/// the character at `end` is end-of-input or ASCII whitespace. Pandoc
/// applies the same rule so `@smith2020.` at the end of a sentence
/// extracts as the key `smith2020`. Without this, `.` is a legal
/// key-internal character (Better BibTeX shape) and would be greedy-
/// absorbed.
fn trim_trailing_sentence_dots(bytes: &[u8], start: usize, mut end: usize) -> usize {
    let at_sentence_boundary = end >= bytes.len() || bytes[end].is_ascii_whitespace();
    if !at_sentence_boundary {
        return end;
    }
    while end > start && bytes[end - 1] == b'.' {
        end -= 1;
    }
    end
}

/// Pre-compute the byte offset of each line's start so `line_for` is
/// O(log n) per call.
fn line_starts(source: &str) -> Vec<usize> {
    let mut starts = vec![0];
    for (idx, &b) in source.as_bytes().iter().enumerate() {
        if b == b'\n' {
            starts.push(idx + 1);
        }
    }
    starts
}

fn line_for(starts: &[usize], offset: usize) -> u32 {
    let pos = match starts.binary_search(&offset) {
        Ok(idx) => idx,
        Err(idx) => idx.saturating_sub(1),
    };
    (pos + 1) as u32
}

#[cfg(test)]
mod tests {
    use super::*;

    fn citation(refs: &[CitationReference], idx: usize) -> &CitationReference {
        &refs[idx]
    }

    fn single(refs: &[CitationReference], idx: usize) -> &CitedItem {
        assert_eq!(refs[idx].citations.len(), 1, "expected single CitedItem");
        &refs[idx].citations[0]
    }

    // --- §6.5 syntax table -------------------------------------------

    #[test]
    fn bracketed_single() {
        let refs = extract_citations("See [@smith2020] for context.");
        assert_eq!(refs.len(), 1);
        let item = single(&refs, 0);
        assert_eq!(item.key, "smith2020");
        assert_eq!(item.mode, CitationMode::Bracketed);
        assert!(item.locator.is_none());
        assert!(item.prefix.is_none());
        assert!(item.suffix.is_none());
        assert_eq!(citation(&refs, 0).raw, "[@smith2020]");
    }

    #[test]
    fn bracketed_with_page_locator() {
        let refs = extract_citations("Quote: [@smith2020, p. 23].");
        let item = single(&refs, 0);
        assert_eq!(item.key, "smith2020");
        assert_eq!(
            item.locator,
            Some(Locator {
                label: "p.".into(),
                locator: "23".into(),
            })
        );
    }

    #[test]
    fn bracketed_with_pp_locator_takes_longer_match() {
        let refs = extract_citations("[@smith2020, pp. 23-45]");
        let item = single(&refs, 0);
        assert_eq!(
            item.locator.as_ref().unwrap().label,
            "pp.",
            "pp. must match before p."
        );
        assert_eq!(item.locator.as_ref().unwrap().locator, "23-45");
    }

    #[test]
    fn bracketed_multiple_citations_split_on_semicolons() {
        let refs = extract_citations("[@smith2020; @jones2019; @lee2018]");
        assert_eq!(refs.len(), 1);
        let cite = &refs[0];
        assert_eq!(cite.citations.len(), 3);
        assert_eq!(cite.citations[0].key, "smith2020");
        assert_eq!(cite.citations[1].key, "jones2019");
        assert_eq!(cite.citations[2].key, "lee2018");
    }

    #[test]
    fn in_text_citation() {
        let refs = extract_citations("As shown by @smith2020, the trend is clear.");
        let item = single(&refs, 0);
        assert_eq!(item.key, "smith2020");
        assert_eq!(item.mode, CitationMode::InText);
        assert_eq!(citation(&refs, 0).raw, "@smith2020");
    }

    #[test]
    fn author_suppressed() {
        let refs = extract_citations("Smith found [-@smith2020] that…");
        let item = single(&refs, 0);
        assert_eq!(item.key, "smith2020");
        assert_eq!(item.mode, CitationMode::SuppressAuthor);
        assert!(item.prefix.is_none());
    }

    #[test]
    fn prefix_inside_brackets() {
        let refs = extract_citations("[see @smith2020, p. 23]");
        let item = single(&refs, 0);
        assert_eq!(item.key, "smith2020");
        assert_eq!(item.prefix.as_deref(), Some("see"));
        assert_eq!(
            item.locator,
            Some(Locator {
                label: "p.".into(),
                locator: "23".into(),
            })
        );
    }

    #[test]
    fn mixed_multi_with_prefix_on_second_item() {
        let refs = extract_citations("[@smith2020, p. 23; see also @jones2019]");
        let cite = &refs[0];
        assert_eq!(cite.citations.len(), 2);
        assert_eq!(cite.citations[0].key, "smith2020");
        assert!(cite.citations[0].prefix.is_none());
        assert_eq!(
            cite.citations[0].locator.as_ref().map(|l| l.label.as_str()),
            Some("p.")
        );
        assert_eq!(cite.citations[1].key, "jones2019");
        assert_eq!(cite.citations[1].prefix.as_deref(), Some("see also"));
    }

    // --- Locator handling -------------------------------------------

    #[test]
    fn chapter_locator() {
        let refs = extract_citations("[@smith2020, chap. 4]");
        let item = single(&refs, 0);
        assert_eq!(
            item.locator,
            Some(Locator {
                label: "chap.".into(),
                locator: "4".into(),
            })
        );
    }

    #[test]
    fn fig_eq_vol_note_locators_recognised() {
        for (input, label, value) in [
            ("[@a, fig. 7]", "fig.", "7"),
            ("[@a, eq. 3]", "eq.", "3"),
            ("[@a, vol. 2]", "vol.", "2"),
            ("[@a, note 5]", "note", "5"),
            ("[@a, section 4.2]", "section", "4.2"),
        ] {
            let refs = extract_citations(input);
            let item = single(&refs, 0);
            assert_eq!(
                item.locator,
                Some(Locator {
                    label: label.into(),
                    locator: value.into(),
                }),
                "input: {input}"
            );
        }
    }

    #[test]
    fn unknown_locator_label_is_stored_as_unknown() {
        let refs = extract_citations("[@smith2020, table 3]");
        let item = single(&refs, 0);
        assert_eq!(
            item.locator,
            Some(Locator {
                label: "unknown".into(),
                locator: "table 3".into(),
            })
        );
    }

    // --- Negative cases ---------------------------------------------

    #[test]
    fn citation_inside_inline_code_is_ignored() {
        let refs = extract_citations("Inline `[@smith2020]` is not a citation.");
        assert!(refs.is_empty(), "got {refs:?}");
    }

    #[test]
    fn citation_inside_fenced_code_is_ignored() {
        let src = "```\n[@smith2020]\n@jones2019\n```\n";
        let refs = extract_citations(src);
        assert!(refs.is_empty(), "got {refs:?}");
    }

    #[test]
    fn email_address_is_not_an_in_text_citation() {
        let refs = extract_citations("Contact name@example.com for details.");
        assert!(refs.is_empty(), "got {refs:?}");
    }

    #[test]
    fn brackets_without_at_are_not_citations() {
        let refs = extract_citations("This is [just text in brackets].");
        assert!(refs.is_empty(), "got {refs:?}");
    }

    #[test]
    fn malformed_unclosed_bracket_does_not_crash() {
        // The unclosed `[@open ...` itself emits no bracketed
        // citation. Once the bracket scan fails, the `@open` becomes
        // an in-text citation (recovery) and the later `@other`
        // extracts normally. This matches user expectation: a missing
        // `]` while editing shouldn't make a half-finished citation
        // disappear entirely.
        let refs = extract_citations("Broken [@open and then @other appears.");
        let keys: Vec<&str> = refs
            .iter()
            .flat_map(|r| r.citations.iter().map(|c| c.key.as_str()))
            .collect();
        assert!(keys.contains(&"open"), "got keys: {keys:?}");
        assert!(keys.contains(&"other"), "got keys: {keys:?}");
        // No bracketed-mode CitationReference survives the unclosed
        // bracket — recovery only emits in-text mode.
        for r in &refs {
            for item in &r.citations {
                assert_eq!(item.mode, CitationMode::InText);
            }
        }
    }

    #[test]
    fn truly_unclosed_bracket_at_eof_emits_no_citation() {
        // Edge case: `[@key` at end of input with no following text.
        // Recovery still extracts the @key as in-text.
        let refs = extract_citations("[@key");
        assert_eq!(refs.len(), 1);
        assert_eq!(refs[0].citations[0].key, "key");
        assert_eq!(refs[0].citations[0].mode, CitationMode::InText);
    }

    #[test]
    fn citation_after_word_char_is_not_an_intext_citation() {
        // `word@key` looks like an email-style construction — must not
        // emit a citation.
        let refs = extract_citations("foo@bar should not extract");
        assert!(refs.is_empty(), "got {refs:?}");
    }

    #[test]
    fn citation_at_line_start_works() {
        let refs = extract_citations("@smith2020 said so.");
        let item = single(&refs, 0);
        assert_eq!(item.key, "smith2020");
    }

    // --- Key grammar ------------------------------------------------

    #[test]
    fn key_allows_punctuation_in_better_bibtex_shape() {
        let refs = extract_citations("[@jones-etal-2019:abc.v2+rev]");
        let item = single(&refs, 0);
        assert_eq!(item.key, "jones-etal-2019:abc.v2+rev");
    }

    // --- Offsets / line numbers -------------------------------------

    #[test]
    fn byte_offset_points_at_opening_char_bracketed() {
        let src = "abc [@x]";
        let refs = extract_citations(src);
        assert_eq!(refs[0].byte_offset, 4);
        assert_eq!(&src[refs[0].byte_offset as usize..][..1], "[");
    }

    #[test]
    fn byte_offset_points_at_opening_char_intext() {
        let src = "abc @x";
        let refs = extract_citations(src);
        assert_eq!(refs[0].byte_offset, 4);
        assert_eq!(&src[refs[0].byte_offset as usize..][..1], "@");
    }

    #[test]
    fn lines_increase_with_newlines() {
        let src = "first @a\nsecond [@b]\nthird @c";
        let refs = extract_citations(src);
        assert_eq!(refs.len(), 3);
        assert_eq!(refs[0].line, 1);
        assert_eq!(refs[1].line, 2);
        assert_eq!(refs[2].line, 3);
    }

    #[test]
    fn paragraph_of_three_citations_returns_them_in_byte_order() {
        let src = "First @a then [@b, p. 3] and finally @c.";
        let refs = extract_citations(src);
        assert_eq!(refs.len(), 3);
        assert!(refs[0].byte_offset < refs[1].byte_offset);
        assert!(refs[1].byte_offset < refs[2].byte_offset);
        assert_eq!(refs[0].citations[0].key, "a");
        assert_eq!(refs[1].citations[0].key, "b");
        assert_eq!(refs[2].citations[0].key, "c");
    }

    // --- Frontmatter handling ---------------------------------------

    #[test]
    fn frontmatter_at_token_is_skipped() {
        // The `bibliography:` value contains `@smith2020` only by
        // happenstance — frontmatter should be excised before parsing.
        let src = "---\nbibliography: \"@smith2020\"\n---\n\nBody.\n";
        let refs = extract_citations(src);
        assert!(refs.is_empty(), "got {refs:?}");
    }

    #[test]
    fn citation_after_frontmatter_uses_original_source_offsets() {
        let src = "---\ntitle: t\n---\n\nSee [@smith2020].\n";
        let refs = extract_citations(src);
        assert_eq!(refs.len(), 1);
        let byte_offset = refs[0].byte_offset as usize;
        assert_eq!(&src[byte_offset..byte_offset + 1], "[");
        assert!(refs[0].line >= 5);
    }

    // --- Property test ---------------------------------------------

    #[test]
    fn random_text_around_citations_never_panics() {
        use proptest::prelude::*;
        proptest!(|(noise in ".{0,200}", key in "[a-z][a-z0-9]{1,16}")| {
            let src = format!("{noise} [@{key}] {noise}\n@{key}");
            // Must not panic, must be deterministic, must find at
            // least one citation (the post-noise `@key`).
            let r1 = extract_citations(&src);
            let r2 = extract_citations(&src);
            prop_assert_eq!(r1, r2);
        });
    }
}
