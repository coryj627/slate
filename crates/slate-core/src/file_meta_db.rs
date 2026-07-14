// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Host-independent derivation and persistence for the files-sidebar metadata.
//!
//! `file_meta` is a regenerable projection. The scanner and save path call the
//! single replacement entry point in their existing write transaction, after
//! frontmatter properties. Frontmatter boundaries and link syntax stay owned by
//! their existing parsers; this module only owns the preview projection.

use pulldown_cmark::{CodeBlockKind, Event, Options, Parser, Tag, TagEnd};
use rusqlite::{Transaction, params};

use crate::{LinkKind, VaultError};

const PREVIEW_CHAR_LIMIT: usize = 300;

#[derive(Debug, Clone, PartialEq, Eq)]
struct DerivedFileMeta {
    word_count: i64,
    char_count: i64,
    preview: String,
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
    tx.execute(
        "INSERT INTO file_meta (file_id, word_count, char_count, preview)
         VALUES (?1, ?2, ?3, ?4)
         ON CONFLICT(file_id) DO UPDATE SET
            word_count = excluded.word_count,
            char_count = excluded.char_count,
            preview = excluded.preview",
        params![file_id, meta.word_count, meta.char_count, meta.preview],
    )?;
    Ok(())
}

fn derive_meta(contents: &str) -> DerivedFileMeta {
    let body = crate::frontmatter::body_after_frontmatter(contents);
    DerivedFileMeta {
        word_count: body.split_whitespace().count() as i64,
        char_count: body.chars().count() as i64,
        preview: normalize_preview(body),
    }
}

/// Normalize Markdown into a deterministic plain-text excerpt.
///
/// The authoritative wikilink/source-span machinery rewrites wikilinks first;
/// pulldown-cmark then removes Markdown presentation markers and native link
/// destinations. Fenced code and HTML blocks are omitted while inline and
/// indented code remain text, matching FL0-1's distinct count/preview rules.
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
    let links: Vec<_> = crate::extract_links(body)
        .into_iter()
        .filter(|link| link.kind == LinkKind::Wikilink)
        .collect();
    if links.is_empty() {
        return body.to_string();
    }

    let mut output = String::with_capacity(body.len());
    let mut cursor = 0usize;
    for link in links {
        if link.span_start < cursor || link.span_end > body.len() {
            continue;
        }
        output.push_str(&body[cursor..link.span_start]);
        output.push_str(link.display_text.as_deref().unwrap_or(&link.target_raw));
        cursor = link.span_end;
    }
    output.push_str(&body[cursor..]);
    output
}

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

    #[test]
    fn frontmatter_only_file_has_empty_body_metadata() {
        let meta = derive_meta("---\ntitle: Only metadata\n---\n");
        assert_eq!(meta.word_count, 0);
        assert_eq!(meta.char_count, 0);
        assert_eq!(meta.preview, "");
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

    proptest! {
        #[test]
        fn counts_and_preview_invariants_hold_for_arbitrary_unicode(contents in ".{0,1000}") {
            let body = crate::frontmatter::body_after_frontmatter(&contents);
            let meta = derive_meta(&contents);
            prop_assert_eq!(meta.word_count, body.split_whitespace().count() as i64);
            prop_assert_eq!(meta.char_count, body.chars().count() as i64);
            prop_assert!(!meta.preview.contains('\n'));
            prop_assert!(meta.preview.chars().count() <= PREVIEW_CHAR_LIMIT);
        }
    }
}
