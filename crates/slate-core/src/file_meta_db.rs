// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Host-independent derivation and persistence for the files-sidebar metadata.
//!
//! `file_meta` is a regenerable projection. Save/open/repair paths replace one
//! row immediately; the scanner queues the same derivation at the same
//! after-properties seam and writes bounded 200-row batches in its existing
//! transaction, with path-attributed rowwise fallback. Frontmatter boundaries
//! and link syntax stay owned by their existing parsers; this module only owns
//! the preview projection.

use pulldown_cmark::{CodeBlockKind, Event, Options, Parser, Tag, TagEnd};
use rusqlite::{Transaction, params};

use crate::links::{MarkdownEventSink, ParsedLink};
use crate::{LinkKind, VaultError};

const PREVIEW_CHAR_LIMIT: usize = 300;

#[derive(Debug, Clone, PartialEq, Eq)]
struct DerivedFileMeta {
    word_count: i64,
    char_count: i64,
    preview: String,
}

#[derive(Debug)]
struct WikilinkRewrite {
    span_start: usize,
    span_end: usize,
    replacement: String,
}

/// Owned result of observing the authoritative link parser. It is complete
/// even for a file with zero links and can cross the database boundary without
/// borrowing the source buffer.
#[derive(Debug)]
pub(crate) struct FileMetaParseArtifact {
    preview_without_wikilink_rewrites: String,
    wikilink_rewrites: Vec<WikilinkRewrite>,
}

impl FileMetaParseArtifact {
    pub(crate) fn empty() -> Self {
        Self {
            preview_without_wikilink_rewrites: String::new(),
            wikilink_rewrites: Vec::new(),
        }
    }
}

/// File-metadata-owned projection of pulldown-cmark events. Once the bounded
/// preview is complete, event observation becomes a no-op. Link parsing still
/// completes whenever an opener is possible; only the links-owned proven-
/// linkless path may stop with the satisfied observer.
pub(crate) struct FileMetaPreviewObserver {
    output: PreviewBuilder,
    code_stack: Vec<bool>,
    fenced_depth: usize,
    html_depth: usize,
    pending_item_text: Option<String>,
    wikilink_rewrites: Vec<WikilinkRewrite>,
}

impl FileMetaPreviewObserver {
    pub(crate) fn new() -> Self {
        Self {
            output: PreviewBuilder::new(),
            code_stack: Vec::new(),
            fenced_depth: 0,
            html_depth: 0,
            pending_item_text: None,
            wikilink_rewrites: Vec::new(),
        }
    }

    fn is_full(&self) -> bool {
        self.output.is_full()
    }

    pub(crate) fn into_artifact(self) -> FileMetaParseArtifact {
        FileMetaParseArtifact {
            preview_without_wikilink_rewrites: self.output.finish(),
            wikilink_rewrites: self.wikilink_rewrites,
        }
    }
}

impl MarkdownEventSink for FileMetaPreviewObserver {
    fn observe_event(&mut self, event: &Event<'_>) {
        if self.output.is_full() {
            return;
        }

        match event {
            Event::Start(Tag::CodeBlock(kind)) => {
                let fenced = matches!(kind, CodeBlockKind::Fenced(_));
                self.code_stack.push(fenced);
                if fenced {
                    self.fenced_depth += 1;
                }
            }
            Event::End(TagEnd::CodeBlock) => {
                if self.code_stack.pop().unwrap_or(false) {
                    self.fenced_depth = self.fenced_depth.saturating_sub(1);
                }
                self.output.separator();
            }
            Event::Start(Tag::HtmlBlock) => self.html_depth += 1,
            Event::End(TagEnd::HtmlBlock) => {
                self.html_depth = self.html_depth.saturating_sub(1);
                self.output.separator();
            }
            _ if self.fenced_depth > 0 || self.html_depth > 0 => {}
            Event::Start(Tag::Item) => self.pending_item_text = Some(String::new()),
            Event::TaskListMarker(_) => self.pending_item_text = None,
            Event::Text(text) => {
                append_streaming_item_text(&mut self.output, &mut self.pending_item_text, text)
            }
            Event::Code(text) => {
                flush_streaming_item_text(&mut self.output, &mut self.pending_item_text);
                self.output.push_text(text);
            }
            Event::SoftBreak | Event::HardBreak | Event::Rule => self.output.separator(),
            Event::End(TagEnd::Item) => {
                flush_streaming_item_text(&mut self.output, &mut self.pending_item_text);
                self.output.separator();
            }
            Event::End(end) if ends_text_block(end) => self.output.separator(),
            Event::InlineHtml(_) | Event::Html(_) => {}
            _ => {}
        }
    }

    fn observe_wikilinks(&mut self, links: &[ParsedLink]) {
        self.wikilink_rewrites.extend(links.iter().map(|link| {
            WikilinkRewrite {
                span_start: link.span_start,
                span_end: link.span_end,
                replacement: link
                    .display_text
                    .clone()
                    .unwrap_or_else(|| link.target_raw.clone()),
            }
        }));
    }

    fn is_satisfied(&self) -> bool {
        self.output.is_full()
    }
}

const SCAN_BATCH_ROWS: usize = 200;

#[derive(Debug)]
struct PendingFileMeta {
    file_id: i64,
    path: String,
    meta: DerivedFileMeta,
}

#[derive(Debug)]
pub(crate) struct FileMetaWriteFailure {
    pub(crate) path: String,
    pub(crate) error: VaultError,
}

/// Scan-only accumulator. Two hundred rows use 800 bind parameters, below
/// SQLite's historical 999-parameter floor. Interactive save/open/repair paths
/// continue to persist one completed projection immediately.
pub(crate) struct FileMetaScanBatch {
    rows: Vec<PendingFileMeta>,
}

impl FileMetaScanBatch {
    pub(crate) fn new() -> Self {
        Self {
            rows: Vec::with_capacity(SCAN_BATCH_ROWS),
        }
    }

    pub(crate) fn push(
        &mut self,
        tx: &Transaction<'_>,
        file_id: i64,
        path: String,
        contents: &str,
        artifact: FileMetaParseArtifact,
    ) -> Vec<FileMetaWriteFailure> {
        self.rows.push(PendingFileMeta {
            file_id,
            path,
            meta: derive_meta_from_artifact(contents, artifact),
        });
        if self.rows.len() == SCAN_BATCH_ROWS {
            self.flush(tx)
        } else {
            Vec::new()
        }
    }

    pub(crate) fn flush(&mut self, tx: &Transaction<'_>) -> Vec<FileMetaWriteFailure> {
        if self.rows.is_empty() {
            return Vec::new();
        }
        let rows = std::mem::take(&mut self.rows);
        self.rows.reserve(SCAN_BATCH_ROWS);
        if execute_meta_batch(tx, &rows).is_ok() {
            return Vec::new();
        }

        // One multi-row statement is atomic. If it fails, replay rows one at a
        // time so the scan keeps its existing best-effort behavior and reports
        // the exact path(s) that could not persist.
        let mut failures = Vec::new();
        for row in rows {
            if let Err(error) = write_derived_meta(tx, row.file_id, &row.meta) {
                failures.push(FileMetaWriteFailure {
                    path: row.path,
                    error,
                });
            }
        }
        failures
    }
}

fn execute_meta_batch(tx: &Transaction<'_>, rows: &[PendingFileMeta]) -> Result<(), VaultError> {
    use std::fmt::Write as _;

    let mut sql = String::with_capacity(192 + rows.len() * 24);
    sql.push_str("INSERT INTO file_meta (file_id, word_count, char_count, preview) VALUES ");
    for index in 0..rows.len() {
        if index > 0 {
            sql.push(',');
        }
        let parameter = index * 4 + 1;
        write!(
            sql,
            "(?{parameter},?{},?{},?{})",
            parameter + 1,
            parameter + 2,
            parameter + 3
        )
        .expect("writing SQL into String cannot fail");
    }
    sql.push_str(
        " ON CONFLICT(file_id) DO UPDATE SET
            word_count = excluded.word_count,
            char_count = excluded.char_count,
            preview = excluded.preview",
    );

    let mut parameters: Vec<&dyn rusqlite::ToSql> = Vec::with_capacity(rows.len() * 4);
    for row in rows {
        parameters.push(&row.file_id);
        parameters.push(&row.meta.word_count);
        parameters.push(&row.meta.char_count);
        parameters.push(&row.meta.preview);
    }
    tx.prepare_cached(&sql)?
        .execute(rusqlite::params_from_iter(parameters))?;
    Ok(())
}

/// Replace the derived metadata row for one indexed file.
///
/// Callers pass `""` for non-Markdown or deliberately unindexed oversized
/// files, yielding the uniform zero/empty row used by LEFT JOIN consumers.
pub fn replace_meta_for_file(
    tx: &Transaction<'_>,
    file_id: i64,
    contents: &str,
) -> Result<(), VaultError> {
    let meta = derive_meta(contents);
    write_derived_meta(tx, file_id, &meta)
}

pub(crate) fn replace_meta_for_file_from_artifact(
    tx: &Transaction<'_>,
    file_id: i64,
    contents: &str,
    artifact: FileMetaParseArtifact,
) -> Result<(), VaultError> {
    let meta = derive_meta_from_artifact(contents, artifact);
    write_derived_meta(tx, file_id, &meta)
}

fn write_derived_meta(
    tx: &Transaction<'_>,
    file_id: i64,
    meta: &DerivedFileMeta,
) -> Result<(), VaultError> {
    tx.prepare_cached(
        "INSERT INTO file_meta (file_id, word_count, char_count, preview)
         VALUES (?1, ?2, ?3, ?4)
         ON CONFLICT(file_id) DO UPDATE SET
            word_count = excluded.word_count,
            char_count = excluded.char_count,
            preview = excluded.preview",
    )?
    .execute(params![
        file_id,
        meta.word_count,
        meta.char_count,
        &meta.preview
    ])?;
    Ok(())
}

fn derive_meta(contents: &str) -> DerivedFileMeta {
    let body = crate::frontmatter::body_after_frontmatter(contents);
    let (word_count, char_count, has_wiki_opener) = count_body_chars_and_words(body);
    DerivedFileMeta {
        word_count,
        char_count,
        preview: normalize_preview_streaming(body, has_wiki_opener),
    }
}

fn derive_meta_from_artifact(contents: &str, artifact: FileMetaParseArtifact) -> DerivedFileMeta {
    let body = crate::frontmatter::body_after_frontmatter(contents);
    let (word_count, char_count, _) = count_body_chars_and_words(body);
    let preview = if artifact.wikilink_rewrites.is_empty() {
        artifact.preview_without_wikilink_rewrites
    } else {
        let rewritten = replace_wikilinks_from_records(body, &artifact.wikilink_rewrites);
        normalize_preview_events(&rewritten)
    };
    DerivedFileMeta {
        word_count,
        char_count,
        preview,
    }
}

fn count_body_chars_and_words(body: &str) -> (i64, i64, bool) {
    count_body_chars_and_words_ascii(body)
        .unwrap_or_else(|| count_body_chars_and_words_unicode(body))
}

fn count_body_chars_and_words_ascii(body: &str) -> Option<(i64, i64, bool)> {
    if !body.is_ascii() {
        return None;
    }

    let mut word_count = 0i64;
    let mut in_word = false;
    let mut previous_open_bracket = false;
    let mut has_wiki_opener = false;
    for byte in body.bytes() {
        has_wiki_opener |= previous_open_bracket && byte == b'[';
        previous_open_bracket = byte == b'[';
        // Match `char::is_whitespace` exactly for ASCII, including vertical
        // tab (0x0b), which `u8::is_ascii_whitespace` intentionally excludes.
        if matches!(byte, b'\t'..=b'\r' | b' ') {
            in_word = false;
        } else if !in_word {
            word_count += 1;
            in_word = true;
        }
    }
    Some((word_count, body.len() as i64, has_wiki_opener))
}

fn count_body_chars_and_words_unicode(body: &str) -> (i64, i64, bool) {
    let mut word_count = 0i64;
    let mut char_count = 0i64;
    let mut in_word = false;
    let mut previous_open_bracket = false;
    let mut has_wiki_opener = false;
    for character in body.chars() {
        char_count += 1;
        has_wiki_opener |= previous_open_bracket && character == '[';
        previous_open_bracket = character == '[';
        if character.is_whitespace() {
            in_word = false;
        } else if !in_word {
            word_count += 1;
            in_word = true;
        }
    }
    (word_count, char_count, has_wiki_opener)
}

/// Normalize Markdown into a deterministic plain-text excerpt.
///
/// Pulldown-cmark removes Markdown presentation markers and native link
/// destinations while this loop streams visible text directly into a bounded
/// whitespace-collapsing builder. Fenced code and HTML blocks are omitted;
/// inline and indented code remain text. Event projection stops once the exact
/// 300-character result is complete; pulldown-cmark's construction-time first
/// pass may still scan the full source. A body with a possible wikilink first
/// takes the authoritative full-source rewrite to preserve escaped/source-span
/// semantics, then uses the same bounded event projection.
fn normalize_preview_streaming(body: &str, has_wiki_opener: bool) -> String {
    // The overwhelmingly common path has no wikilink opener: avoid both the
    // authoritative link walk and a whole-body clone. Pulldown splits normal
    // wikilinks across several Text events and decodes escapes before those
    // events, so event-local extraction is not equivalent (`\[[x]]` is the
    // concrete counterexample). Keep the existing source-span authority for
    // the rare opener path, then stream events through the bounded builder.
    let rewritten = has_wiki_opener.then(|| replace_wikilinks(body));
    let source = rewritten.as_deref().unwrap_or(body);
    normalize_preview_events(source)
}

fn normalize_preview_events(source: &str) -> String {
    let options = Options::ENABLE_STRIKETHROUGH | Options::ENABLE_TASKLISTS;
    let mut observer = FileMetaPreviewObserver::new();

    for event in Parser::new_ext(source, options) {
        observer.observe_event(&event);
        if observer.is_full() {
            break;
        }
    }

    observer.into_artifact().preview_without_wikilink_rewrites
}

#[derive(Debug)]
struct PreviewBuilder {
    output: String,
    char_count: usize,
    pending_space: bool,
}

impl PreviewBuilder {
    fn new() -> Self {
        Self {
            output: String::with_capacity(PREVIEW_CHAR_LIMIT),
            char_count: 0,
            pending_space: false,
        }
    }

    fn push_text(&mut self, text: &str) {
        for character in text.chars() {
            if character.is_whitespace() {
                if self.char_count > 0 {
                    self.pending_space = true;
                }
                continue;
            }
            if self.pending_space {
                self.output.push(' ');
                self.char_count += 1;
                self.pending_space = false;
                if self.is_full() {
                    return;
                }
            }
            self.output.push(character);
            self.char_count += 1;
            if self.is_full() {
                return;
            }
        }
    }

    fn separator(&mut self) {
        if self.char_count > 0 {
            self.pending_space = true;
        }
    }

    fn is_full(&self) -> bool {
        self.char_count >= PREVIEW_CHAR_LIMIT
    }

    fn finish(self) -> String {
        self.output
    }
}

fn append_streaming_item_text(
    output: &mut PreviewBuilder,
    pending: &mut Option<String>,
    text: &str,
) {
    let Some(buffer) = pending.as_mut() else {
        output.push_text(text);
        return;
    };
    buffer.push_str(text);
    match task_prefix_end(buffer) {
        TaskPrefix::NeedMore => {}
        TaskPrefix::Absent => flush_streaming_item_text(output, pending),
        TaskPrefix::Present(end) => {
            let buffer = pending.take().expect("pending item text exists");
            output.push_text(&buffer[end..]);
        }
    }
}

fn flush_streaming_item_text(output: &mut PreviewBuilder, pending: &mut Option<String>) {
    if let Some(buffer) = pending.take() {
        output.push_text(&buffer);
    }
}

/// Test-only whole-body reference retained to prove the streaming projection
/// is byte-identical without shipping its full-source allocation in production.
#[cfg(test)]
fn normalize_preview(body: &str) -> String {
    let source = replace_wikilinks(body);
    let options = Options::ENABLE_STRIKETHROUGH | Options::ENABLE_TASKLISTS;
    let mut output = String::with_capacity(source.len().min(PREVIEW_CHAR_LIMIT * 2));
    let mut code_stack: Vec<bool> = Vec::new();
    let mut fenced_depth = 0usize;
    let mut html_depth = 0usize;
    let mut pending_item_text: Option<String> = None;

    for event in Parser::new_ext(&source, options) {
        match event {
            Event::Start(Tag::CodeBlock(kind)) => {
                let fenced = matches!(kind, CodeBlockKind::Fenced(_));
                code_stack.push(fenced);
                if fenced {
                    fenced_depth += 1;
                }
            }
            Event::End(TagEnd::CodeBlock) => {
                if code_stack.pop().unwrap_or(false) {
                    fenced_depth = fenced_depth.saturating_sub(1);
                }
                push_separator(&mut output);
            }
            Event::Start(Tag::HtmlBlock) => html_depth += 1,
            Event::End(TagEnd::HtmlBlock) => {
                html_depth = html_depth.saturating_sub(1);
                push_separator(&mut output);
            }
            _ if fenced_depth > 0 || html_depth > 0 => {}
            Event::Start(Tag::Item) => pending_item_text = Some(String::new()),
            Event::TaskListMarker(_) => pending_item_text = None,
            Event::Text(text) => {
                append_item_text(&mut output, &mut pending_item_text, &text);
            }
            Event::Code(text) => {
                flush_pending_item_text(&mut output, &mut pending_item_text);
                output.push_str(&text);
            }
            Event::SoftBreak | Event::HardBreak | Event::Rule => push_separator(&mut output),
            Event::End(TagEnd::Item) => {
                flush_pending_item_text(&mut output, &mut pending_item_text);
                push_separator(&mut output);
            }
            Event::End(end) if ends_text_block(&end) => push_separator(&mut output),
            // Inline HTML tags are presentation markup. Their textual children
            // arrive as ordinary Text events and remain in the preview.
            Event::InlineHtml(_) | Event::Html(_) => {}
            _ => {}
        }
    }

    output
        .split_whitespace()
        .flat_map(|word| [word, " "])
        .collect::<String>()
        .trim_end()
        .chars()
        .take(PREVIEW_CHAR_LIMIT)
        .collect()
}

fn replace_wikilinks(body: &str) -> String {
    let records: Vec<_> = crate::extract_links(body)
        .into_iter()
        .filter(|link| link.kind == LinkKind::Wikilink)
        .map(|link| WikilinkRewrite {
            span_start: link.span_start,
            span_end: link.span_end,
            replacement: link.display_text.unwrap_or(link.target_raw),
        })
        .collect();
    replace_wikilinks_from_records(body, &records)
}

fn replace_wikilinks_from_records(body: &str, records: &[WikilinkRewrite]) -> String {
    if records.is_empty() {
        return body.to_string();
    }

    let mut output = String::with_capacity(body.len());
    let mut cursor = 0usize;
    for record in records {
        if record.span_start < cursor || record.span_end > body.len() {
            continue;
        }
        output.push_str(&body[cursor..record.span_start]);
        output.push_str(&record.replacement);
        cursor = record.span_end;
    }
    output.push_str(&body[cursor..]);
    output
}

#[cfg(test)]
fn append_item_text(output: &mut String, pending: &mut Option<String>, text: &str) {
    let Some(buffer) = pending.as_mut() else {
        output.push_str(text);
        return;
    };
    buffer.push_str(text);
    match task_prefix_end(buffer) {
        TaskPrefix::NeedMore => {}
        TaskPrefix::Absent => flush_pending_item_text(output, pending),
        TaskPrefix::Present(end) => {
            output.push_str(&buffer[end..]);
            *pending = None;
        }
    }
}

#[cfg(test)]
fn flush_pending_item_text(output: &mut String, pending: &mut Option<String>) {
    if let Some(buffer) = pending.take() {
        output.push_str(&buffer);
    }
}

enum TaskPrefix {
    NeedMore,
    Absent,
    Present(usize),
}

fn task_prefix_end(text: &str) -> TaskPrefix {
    let mut chars = text.char_indices();
    let Some((_, '[')) = chars.next() else {
        return if text.is_empty() {
            TaskPrefix::NeedMore
        } else {
            TaskPrefix::Absent
        };
    };
    if chars.next().is_none() {
        return TaskPrefix::NeedMore;
    }
    let Some((_, ']')) = chars.next() else {
        return if text.chars().count() < 3 {
            TaskPrefix::NeedMore
        } else {
            TaskPrefix::Absent
        };
    };
    let Some((space_index, space)) = chars.next() else {
        return TaskPrefix::NeedMore;
    };
    if !space.is_whitespace() {
        return TaskPrefix::Absent;
    }
    let mut end = space_index + space.len_utf8();
    for (index, ch) in chars {
        if !ch.is_whitespace() {
            break;
        }
        end = index + ch.len_utf8();
    }
    TaskPrefix::Present(end)
}

fn ends_text_block(end: &TagEnd) -> bool {
    matches!(
        end,
        TagEnd::Paragraph
            | TagEnd::Heading(_)
            | TagEnd::BlockQuote(_)
            | TagEnd::List(_)
            | TagEnd::Table
            | TagEnd::TableHead
            | TagEnd::TableRow
            | TagEnd::TableCell
    )
}

#[cfg(test)]
fn push_separator(output: &mut String) {
    if !output.ends_with(char::is_whitespace) {
        output.push(' ');
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use proptest::prelude::*;
    use rusqlite::Connection;

    fn shared_link_artifact(contents: &str) -> FileMetaParseArtifact {
        let mut observer = FileMetaPreviewObserver::new();
        let _ = crate::links::extract_links_with_event_sink(contents, &mut observer);
        observer.into_artifact()
    }

    fn derive_via_shared_link_extraction(contents: &str) -> DerivedFileMeta {
        derive_meta_from_artifact(contents, shared_link_artifact(contents))
    }

    fn derive_via_forced_full_link_extraction(contents: &str) -> DerivedFileMeta {
        let mut observer = FileMetaPreviewObserver::new();
        let _ = crate::links::extract_links_with_event_sink_forced_full(contents, &mut observer);
        derive_meta_from_artifact(contents, observer.into_artifact())
    }

    #[test]
    fn frontmatter_only_file_has_empty_body_metadata() {
        let meta = derive_meta("---\ntitle: Only metadata\n---\n");
        assert_eq!(meta.word_count, 0);
        assert_eq!(meta.char_count, 0);
        assert_eq!(meta.preview, "");
    }

    #[test]
    fn shared_link_artifact_matches_authoritative_metadata_on_adversarial_markdown() {
        let boundary = format!("{}\u{2003}ignored", "雪".repeat(PREVIEW_CHAR_LIMIT));
        let fixtures = [
            "[**bold\nsoft** `code`](target.md) ![*alt*](img.png)",
            "- [ ] open\n- [x] done\n- [?] custom",
            "```rust\nhidden [[Fence]]\n```\n\n    indented `visible`\n\n<div>hidden</div>",
            "[[Target#Heading|Alias]] ![[Embed^block]] [[Bare]]",
            r"odd \[[Escaped]] even \\[[Visible|Alias]]",
            "`[[InlineCode]]`\n\n---\nnot frontmatter [[Body]]\n---\n\n[[unclosed",
            "---\nrelated: '[[Frontmatter]]'\n---\n\nBody [[Visible|雪]]",
            "Unicode café\u{2003}雪 and [label](note.md)",
            boundary.as_str(),
        ];

        for source in fixtures {
            assert_eq!(
                derive_via_shared_link_extraction(source),
                derive_meta(source),
                "source={source:?}"
            );
        }
    }

    #[test]
    fn proven_linkless_artifact_matches_forced_full_and_direct_authority_at_boundaries() {
        let fixtures = [
            "雪".repeat(PREVIEW_CHAR_LIMIT - 1),
            "雪".repeat(PREVIEW_CHAR_LIMIT),
            "雪".repeat(PREVIEW_CHAR_LIMIT + 1),
            format!(
                "Title\n=====\n\n{}\n\n```rust\nhidden tail\n```",
                "visible ".repeat(80)
            ),
            format!(
                "```rust\nhidden prefix\n```\n\nHeading\n-------\n\n{}",
                "café 雪 ".repeat(80)
            ),
        ];

        for source in fixtures {
            assert!(
                !source.contains(['[', '<']),
                "fixture must be proven linkless"
            );
            let fast = derive_via_shared_link_extraction(&source);
            assert_eq!(
                fast,
                derive_via_forced_full_link_extraction(&source),
                "forced-full source={source:?}"
            );
            assert_eq!(fast, derive_meta(&source), "direct source={source:?}");
        }
    }

    #[test]
    fn preview_normalizes_links_tasks_markup_and_unicode_whitespace() {
        let source = "# Heading\n\n> - [?] [[Target#Section|Alias]] and [[Bare#Anchor]], [label](url), *em* ~~strike~~ `code`.\n\u{2003}Snow 雪";
        let meta = derive_meta(source);
        assert_eq!(meta.word_count, source.split_whitespace().count() as i64);
        assert_eq!(meta.char_count, source.chars().count() as i64);
        assert_eq!(
            meta.preview,
            "Heading Alias and Bare, label, em strike code. Snow 雪"
        );
    }

    #[test]
    fn preview_drops_backtick_tilde_nested_and_unclosed_fences_and_html_blocks() {
        let source = "Visible one.\n\n```outer\nhidden\n~~~ nested marker\nstill hidden\n```\n\n~~~lang\nhidden tilde\n~~~\n\n<div>\nhidden html\n</div>\n\nVisible two.\n\n```\nunclosed hidden";
        assert_eq!(derive_meta(source).preview, "Visible one. Visible two.");
    }

    #[test]
    fn indented_and_inline_code_count_in_preview_but_markers_do_not() {
        let source = "Text `inline`.\n\n    indented code\n";
        assert_eq!(derive_meta(source).preview, "Text inline. indented code");
    }

    #[test]
    fn preview_truncates_to_300_unicode_chars_without_ellipsis() {
        let source = format!("{}雪", "a".repeat(300));
        let preview = derive_meta(&source).preview;
        assert_eq!(preview.chars().count(), PREVIEW_CHAR_LIMIT);
        assert_eq!(preview, "a".repeat(PREVIEW_CHAR_LIMIT));
        assert!(!preview.ends_with('…'));
    }

    #[test]
    fn streaming_preview_matches_the_authoritative_reference_on_adversarial_shapes() {
        let visible_prefix = "x".repeat(PREVIEW_CHAR_LIMIT);
        let fixtures = [
            "```rust\nhidden fence\n```\n<div>\nhidden html\n</div>\n\nVisible [[Target#Heading|Alias]].".to_string(),
            "- [ ] **bold** [[Target|Alias]] and `code`\n- [?] second task\n".to_string(),
            r"escaped \[[Target]] and \[\[Other\]\] stay literal".to_string(),
            "**[[Target|Alias]]** [[Target|*Emphasized alias*]] [[*Emphasized target*]]".to_string(),
            "`inline [[not-a-link]]` <span>hidden html</span> [[Shown|visible]]".to_string(),
            "alpha\u{2003}\t beta\n\n gamma   delta".to_string(),
            format!("{visible_prefix}\n```\ncontent after 300 chars\n```"),
            format!("{} next-word", "a".repeat(PREVIEW_CHAR_LIMIT - 1)),
        ];

        for source in fixtures {
            assert_eq!(
                normalize_preview_streaming(&source, source.contains("[[")),
                normalize_preview(&source),
                "source={source:?}"
            );
        }
    }

    #[test]
    fn complete_streaming_preview_is_independent_of_later_hidden_blocks() {
        let prefix = "p".repeat(PREVIEW_CHAR_LIMIT);
        let fenced_tail = format!("{prefix}\n```rust\nhidden one\n```");
        let html_tail = format!("{prefix}\n<div>hidden two</div>");
        assert_eq!(
            normalize_preview_streaming(&fenced_tail, fenced_tail.contains("[[")),
            prefix
        );
        assert_eq!(
            normalize_preview_streaming(&html_tail, html_tail.contains("[[")),
            prefix
        );
    }

    #[test]
    fn combined_count_pass_matches_the_normative_unicode_counts() {
        let body = "alpha\u{2003}beta\n\t雪 gamma  ";
        assert_eq!(
            count_body_chars_and_words(body),
            (
                body.split_whitespace().count() as i64,
                body.chars().count() as i64,
                false
            )
        );
        assert!(count_body_chars_and_words(r"escaped \[[Target]]").2);
    }

    #[test]
    fn ascii_count_fast_path_matches_normative_counts_and_opener_detection() {
        for body in [
            "",
            "alpha beta\n\tgamma\r\ndelta",
            "  leading and trailing  ",
            "\u{000e}\u{000b}\0",
            r"escaped \[[Target]] and [[Visible|Alias]]",
        ] {
            assert!(body.is_ascii());
            assert_eq!(
                count_body_chars_and_words_ascii(body),
                Some(count_body_chars_and_words_unicode(body)),
                "body={body:?}"
            );
        }
    }

    #[test]
    fn mixed_unicode_count_uses_the_normative_unicode_fallback() {
        let body = "ASCII prefix café\u{2003}雪 [[Target]]";
        assert_eq!(count_body_chars_and_words_ascii(body), None);
        assert_eq!(
            count_body_chars_and_words(body),
            count_body_chars_and_words_unicode(body)
        );
    }

    #[test]
    fn replacement_overwrites_the_single_row() {
        let mut conn = Connection::open_in_memory().unwrap();
        conn.execute_batch(
            "PRAGMA foreign_keys = ON;
             CREATE TABLE files (id INTEGER PRIMARY KEY);
             CREATE TABLE file_meta (
                file_id INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
                word_count INTEGER NOT NULL,
                char_count INTEGER NOT NULL,
                preview TEXT NOT NULL
             );
             INSERT INTO files (id) VALUES (1);",
        )
        .unwrap();
        let tx = conn.transaction().unwrap();
        replace_meta_for_file(&tx, 1, "one two").unwrap();
        replace_meta_for_file(&tx, 1, "three").unwrap();
        tx.commit().unwrap();

        let row: (i64, i64, String) = conn
            .query_row(
                "SELECT word_count, char_count, preview FROM file_meta WHERE file_id = 1",
                [],
                |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
            )
            .unwrap();
        assert_eq!(row, (1, 5, "three".to_string()));
    }

    #[test]
    fn scan_batch_flushes_two_hundred_rows_and_preserves_exact_metadata() {
        let mut conn = Connection::open_in_memory().unwrap();
        conn.execute_batch(
            "PRAGMA foreign_keys = ON;
             CREATE TABLE files (id INTEGER PRIMARY KEY);
             CREATE TABLE file_meta (
                file_id INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
                word_count INTEGER NOT NULL,
                char_count INTEGER NOT NULL,
                preview TEXT NOT NULL
             );
             WITH RECURSIVE ids(id) AS (
                VALUES(1) UNION ALL SELECT id + 1 FROM ids WHERE id < 201
             ) INSERT INTO files(id) SELECT id FROM ids;",
        )
        .unwrap();
        let tx = conn.transaction().unwrap();
        let mut batch = FileMetaScanBatch::new();
        for file_id in 1..=201 {
            let contents = format!("# Note {file_id}\n\nalpha beta");
            let artifact = shared_link_artifact(&contents);
            let failures = batch.push(
                &tx,
                file_id,
                format!("note-{file_id}.md"),
                &contents,
                artifact,
            );
            assert!(failures.is_empty(), "{failures:?}");
        }
        assert!(batch.flush(&tx).is_empty());
        tx.commit().unwrap();

        let count: i64 = conn
            .query_row("SELECT COUNT(*) FROM file_meta", [], |row| row.get(0))
            .unwrap();
        assert_eq!(count, 201);
        let last: (i64, i64, String) = conn
            .query_row(
                "SELECT word_count, char_count, preview FROM file_meta WHERE file_id = 201",
                [],
                |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
            )
            .unwrap();
        assert_eq!(last, (5, 22, "Note 201 alpha beta".to_string()));
    }

    #[test]
    fn scan_batch_falls_back_rowwise_and_attributes_the_bad_path() {
        let mut conn = Connection::open_in_memory().unwrap();
        conn.execute_batch(
            "CREATE TABLE files (id INTEGER PRIMARY KEY);
             CREATE TABLE file_meta (
                file_id INTEGER PRIMARY KEY REFERENCES files(id) ON DELETE CASCADE,
                word_count INTEGER NOT NULL,
                char_count INTEGER NOT NULL,
                preview TEXT NOT NULL
             );
             INSERT INTO files(id) VALUES (1), (2);
             CREATE TRIGGER reject_second_meta
             BEFORE INSERT ON file_meta WHEN NEW.file_id = 2
             BEGIN SELECT RAISE(ABORT, 'rejected second row'); END;",
        )
        .unwrap();
        let tx = conn.transaction().unwrap();
        let mut batch = FileMetaScanBatch::new();
        assert!(
            batch
                .push(
                    &tx,
                    1,
                    "good.md".to_string(),
                    "one",
                    shared_link_artifact("one")
                )
                .is_empty()
        );
        assert!(
            batch
                .push(
                    &tx,
                    2,
                    "bad.md".to_string(),
                    "two",
                    shared_link_artifact("two")
                )
                .is_empty()
        );
        let failures = batch.flush(&tx);
        assert_eq!(failures.len(), 1);
        assert_eq!(failures[0].path, "bad.md");
        assert!(
            failures[0]
                .error
                .to_string()
                .contains("rejected second row")
        );
        let good_count: i64 = tx
            .query_row(
                "SELECT COUNT(*) FROM file_meta WHERE file_id = 1",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(good_count, 1);
    }

    proptest! {
        #[test]
        fn ascii_fast_path_matches_normative_for_arbitrary_ascii(
            bytes in proptest::collection::vec(0u8..=0x7f, 0..1000)
        ) {
            let body = String::from_utf8(bytes).expect("ASCII is UTF-8");
            prop_assert_eq!(
                count_body_chars_and_words_ascii(&body),
                Some(count_body_chars_and_words_unicode(&body))
            );
        }

        #[test]
        fn unicode_fallback_matches_normative_for_arbitrary_mixed_text(
            prefix in ".{0,500}",
            marker in prop_oneof![Just('雪'), Just('é'), Just('\u{2003}')],
            suffix in ".{0,500}"
        ) {
            let body = format!("{prefix}{marker}{suffix}");
            prop_assert_eq!(count_body_chars_and_words_ascii(&body), None);
            prop_assert_eq!(
                count_body_chars_and_words(&body),
                count_body_chars_and_words_unicode(&body)
            );
        }

        #[test]
        fn counts_and_preview_invariants_hold_for_arbitrary_unicode(contents in ".{0,1000}") {
            let body = crate::frontmatter::body_after_frontmatter(&contents);
            let meta = derive_meta(&contents);
            prop_assert_eq!(meta.word_count, body.split_whitespace().count() as i64);
            prop_assert_eq!(meta.char_count, body.chars().count() as i64);
            prop_assert!(!meta.preview.contains('\n'));
            prop_assert!(meta.preview.chars().count() <= PREVIEW_CHAR_LIMIT);
            prop_assert_eq!(meta.preview, normalize_preview(body));
        }


        #[test]
        fn shared_link_artifact_matches_direct_derivation_for_arbitrary_unicode(
            contents in ".{0,1000}"
        ) {
            prop_assert_eq!(
                derive_via_shared_link_extraction(&contents),
                derive_meta(&contents)
            );
        }
    }
}
