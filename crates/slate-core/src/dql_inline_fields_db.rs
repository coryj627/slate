// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Scanner-owned body inline-field projection for Dataview DQL compatibility.

use std::{ops::Range, sync::LazyLock};

use chrono::NaiveDate;
use pulldown_cmark::{Event, Options, Parser, Tag, TagEnd};
use regex::Regex;
use rusqlite::{Connection, OptionalExtension, Transaction, params};
use serde::{Deserialize, Serialize};

use crate::VaultError;

const MAX_INLINE_FIELD_LINE_UTF16: usize = 32_768;

#[cfg(test)]
thread_local! {
    static MARKDOWN_STRUCTURE_CALLS: std::cell::Cell<usize> = const { std::cell::Cell::new(0) };
}

static UNICODE_LETTER: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"^\p{Letter}$").expect("Unicode Letter is a valid regex property")
});
static EMOJI_PRESENTATION: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"^\p{Emoji_Presentation}$").expect("Emoji_Presentation is a valid regex property")
});
static POSSIBLE_EMOJI: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"^(?:\p{Emoji}|\p{Extended_Pictographic}|\p{Emoji_Component})$")
        .expect("emoji properties are valid regex properties")
});

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
#[serde(tag = "kind", content = "value", rename_all = "snake_case")]
pub(crate) enum DqlInlineValue {
    Null,
    Boolean(bool),
    Number(f64),
    Text(String),
    Tag(String),
    Date(String),
    Duration(String),
    Link(DqlInlineLink),
    List(Vec<DqlInlineValue>),
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub(crate) struct DqlInlineLink {
    pub(crate) target: String,
    pub(crate) display: Option<String>,
    pub(crate) embed: bool,
    pub(crate) link_type: DqlInlineLinkType,
    pub(crate) subpath: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub(crate) enum DqlInlineLinkType {
    File,
    Header,
    Block,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub(crate) struct DqlInlineField {
    pub(crate) key: String,
    pub(crate) value: DqlInlineValue,
}

#[derive(Debug, Clone, Default, PartialEq)]
pub(crate) struct DqlInlineProjection {
    pub(crate) fields: Vec<DqlInlineField>,
    pub(crate) incomplete: bool,
}

/// Atomically rebuild the body-only Dataview inline-field projection for one
/// Markdown file. Frontmatter remains owned exclusively by `properties_db`.
pub(crate) fn replace_dql_inline_fields_for_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    write_dql_inline_fields_for_file(tx, file_id, markdown_source, true)
}

/// Populate the projection immediately after inserting its owning `files`
/// row. No derived rows can exist yet, so issuing DELETEs only adds SQLite
/// B-tree work to cold scans.
pub(crate) fn insert_dql_inline_fields_for_new_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    write_dql_inline_fields_for_file(tx, file_id, markdown_source, false)
}

fn write_dql_inline_fields_for_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
    clear_existing: bool,
) -> Result<(), VaultError> {
    let projection = parse_inline_projection(markdown_source);
    if clear_existing {
        tx.execute(
            "DELETE FROM dql_inline_fields WHERE file_id = ?1",
            params![file_id],
        )?;
    }

    if !projection.fields.is_empty() {
        let mut insert = tx.prepare_cached(
            "INSERT INTO dql_inline_fields (file_id, ordinal, key, value_json)
             VALUES (?1, ?2, ?3, ?4)",
        )?;
        for (ordinal, field) in projection.fields.iter().enumerate() {
            let value_json = serde_json::to_string(&field.value).map_err(inline_json_error)?;
            insert.execute(params![file_id, ordinal as i64, field.key, value_json])?;
        }
    }
    if clear_existing {
        tx.prepare_cached(
            "INSERT INTO dql_inline_field_state (file_id, incomplete) VALUES (?1, ?2)
             ON CONFLICT(file_id) DO UPDATE SET incomplete = excluded.incomplete",
        )?
        .execute(params![file_id, i64::from(projection.incomplete)])?;
    } else {
        tx.prepare_cached(
            "INSERT INTO dql_inline_field_state (file_id, incomplete) VALUES (?1, ?2)",
        )?
        .execute(params![file_id, i64::from(projection.incomplete)])?;
    }
    Ok(())
}

/// Remove any stale body inline fields and record that the scanner could not
/// build an honest projection for this file (for example, because its body is
/// above the configured indexing limit).
pub(crate) fn mark_dql_inline_fields_incomplete_for_file(
    tx: &Transaction,
    file_id: i64,
) -> Result<(), VaultError> {
    tx.execute(
        "DELETE FROM dql_inline_fields WHERE file_id = ?1",
        params![file_id],
    )?;
    tx.prepare_cached(
        "INSERT INTO dql_inline_field_state (file_id, incomplete) VALUES (?1, 1)
         ON CONFLICT(file_id) DO UPDATE SET incomplete = excluded.incomplete",
    )?
    .execute(params![file_id])?;
    Ok(())
}

/// Load body inline fields in Dataview merge order. Exact-key coalescing with
/// frontmatter is intentionally left to the DQL engine, which owns the final
/// `frontmatter -> list -> page` scalar/list aggregation contract.
pub(crate) fn load_dql_inline_fields_for_path(
    conn: &Connection,
    path: &str,
) -> Result<DqlInlineProjection, VaultError> {
    let file_id = conn
        .query_row(
            "SELECT id FROM files WHERE path = ?1",
            params![path],
            |row| row.get::<_, i64>(0),
        )
        .optional()?;
    let Some(file_id) = file_id else {
        return Ok(DqlInlineProjection::default());
    };
    // A file row without scanner state is an upgrade/backfill gap. Fail-loud
    // is safer than treating its body fields as an honestly empty set.
    let incomplete = conn
        .query_row(
            "SELECT incomplete FROM dql_inline_field_state WHERE file_id = ?1",
            params![file_id],
            |row| row.get::<_, i64>(0),
        )
        .optional()?
        != Some(0);
    let mut statement = conn.prepare_cached(
        "SELECT key, value_json
         FROM dql_inline_fields
         WHERE file_id = ?1
         ORDER BY ordinal ASC",
    )?;
    let rows = statement.query_map(params![file_id], |row| {
        Ok((row.get::<_, String>(0)?, row.get::<_, String>(1)?))
    })?;
    let mut fields = Vec::new();
    for row in rows {
        let (key, value_json) = row?;
        let value = serde_json::from_str(&value_json).map_err(inline_json_error)?;
        fields.push(DqlInlineField { key, value });
    }
    Ok(DqlInlineProjection { fields, incomplete })
}

fn inline_json_error(error: serde_json::Error) -> VaultError {
    VaultError::InvalidArgument {
        message: format!("invalid derived DQL inline-field JSON: {error}"),
    }
}

#[derive(Debug, Clone)]
struct RawInlineField {
    key: String,
    value: String,
    start: usize,
    end: usize,
}

#[derive(Debug, Default)]
struct ExtractedInlineFields {
    fields: Vec<RawInlineField>,
    incomplete: bool,
}

#[derive(Debug)]
enum FullLineFieldParse {
    Match(RawInlineField),
    NoMatch,
    Incomplete,
}

#[derive(Debug)]
enum FullLineKeyParse {
    Match(String),
    NoMatch,
    Incomplete,
}

#[derive(Debug, Default)]
struct MarkdownStructure {
    hard_excluded: Vec<Range<usize>>,
    uncertain_excluded: Vec<Range<usize>>,
    list_items: Vec<Range<usize>>,
    list_covered: Vec<Range<usize>>,
    block_quotes: Vec<Range<usize>>,
}

#[derive(Debug, Clone, Copy)]
struct SourceLine<'a> {
    start: usize,
    end: usize,
    text: &'a str,
}

#[derive(Debug)]
struct ParsedListLine<'a> {
    content: &'a str,
    task: bool,
}

#[derive(Debug)]
enum AtomParse {
    Match(DqlInlineValue),
    NoMatch,
    Incomplete,
}

pub(crate) fn parse_inline_projection(source: &str) -> DqlInlineProjection {
    // Most notes have no Dataview inline metadata. Avoid a full CommonMark
    // structure pass and line-index allocation when neither ordinary `::`
    // fields nor task emoji fields can possibly be present.
    if !has_inline_candidate(source, true) {
        return DqlInlineProjection::default();
    }

    let structure = markdown_structure(source);
    let lines = source_lines(source);
    let mut projection = DqlInlineProjection::default();

    let mut list_items = structure.list_items.clone();
    list_items.sort_by_key(|range| range.start);

    // Dataview merges non-task list fields before ordinary section fields,
    // regardless of their interleaving in the source document.
    for item in &list_items {
        if range_intersects_any(item, &structure.hard_excluded) {
            continue;
        }
        if range_intersects_any(item, &structure.uncertain_excluded) {
            if range_contains_candidate(source, item, true) {
                projection.incomplete = true;
            }
            continue;
        }
        let Some(first_line) = lines
            .iter()
            .find(|line| line.start <= item.start && item.start < line.end)
        else {
            if range_contains_candidate(source, item, true) {
                projection.incomplete = true;
            }
            continue;
        };
        let Some(parsed) = parse_list_first_line(first_line.text) else {
            if range_contains_candidate(source, item, true) {
                projection.incomplete = true;
            }
            continue;
        };
        if parsed.task {
            continue;
        }

        let item_text = source
            .get(item.clone())
            .unwrap_or_default()
            .trim_end_matches(['\r', '\n']);
        if item_text.contains('\n') {
            if has_inline_candidate(item_text, true) {
                projection.incomplete = true;
            }
            continue;
        }
        append_line_fields(parsed.content, true, &mut projection);
    }

    for line in &lines {
        let line_range = line.start..line.end;
        if range_intersects_any(&line_range, &structure.hard_excluded)
            || range_intersects_any(&line_range, &structure.list_covered)
        {
            continue;
        }
        if !line.text.contains("::") {
            continue;
        }
        if range_intersects_any(&line_range, &structure.uncertain_excluded) {
            projection.incomplete = true;
            continue;
        }
        if utf16_len_exceeds(line.text, MAX_INLINE_FIELD_LINE_UTF16) {
            continue;
        }

        // A list-shaped line which CommonMark did not route as an item is not
        // a safe stand-in for Obsidian metadata.listItems.
        if looks_like_unrouted_list(line.text) {
            projection.incomplete = true;
            continue;
        }

        if let Some(quote) = containing_range(line.start, &structure.block_quotes)
            && source
                .get(quote.clone())
                .is_some_and(|text| text.trim_end_matches(['\r', '\n']).contains('\n'))
        {
            projection.incomplete = true;
            continue;
        }
        let trimmed_quote = line.text.trim_start();
        if trimmed_quote.starts_with("> [!") {
            projection.incomplete = true;
            continue;
        }

        append_line_fields(line.text.trim(), false, &mut projection);
    }

    projection
}

fn append_line_fields(line: &str, include_task_fields: bool, projection: &mut DqlInlineProjection) {
    let extracted = extract_line_fields(line, include_task_fields);
    projection.incomplete |= extracted.incomplete;
    for raw in extracted.fields {
        let (value, incomplete) = parse_inline_value(&raw.value);
        projection.incomplete |= incomplete;
        projection.fields.push(DqlInlineField {
            key: raw.key,
            value,
        });
    }
}

fn extract_line_fields(line: &str, include_task_fields: bool) -> ExtractedInlineFields {
    let mut fields = extract_wrapped_fields(line);
    if include_task_fields {
        fields.extend(extract_emoji_fields(line));
    }
    fields.sort_by_key(|field| field.start);

    let mut filtered = Vec::with_capacity(fields.len());
    for field in fields {
        if filtered
            .last()
            .is_none_or(|previous: &RawInlineField| previous.end < field.start)
        {
            filtered.push(field);
        }
    }
    if !filtered.is_empty() {
        return ExtractedInlineFields {
            fields: filtered,
            incomplete: false,
        };
    }
    match extract_full_line_field(line) {
        FullLineFieldParse::Match(field) => ExtractedInlineFields {
            fields: vec![field],
            incomplete: false,
        },
        FullLineFieldParse::NoMatch => ExtractedInlineFields::default(),
        FullLineFieldParse::Incomplete => ExtractedInlineFields {
            fields: Vec::new(),
            incomplete: true,
        },
    }
}

fn extract_wrapped_fields(line: &str) -> Vec<RawInlineField> {
    let mut fields = Vec::new();
    for (open, close) in [(b'[', b']'), (b'(', b')')] {
        let mut search = 0usize;
        while let Some(relative) = line[search..].find(open as char) {
            let start = search + relative;
            match find_wrapped_field(line, start, open, close) {
                Some(field) => {
                    search = field.end;
                    fields.push(field);
                }
                None => search = start + 1,
            }
        }
    }
    fields
}

fn find_wrapped_field(line: &str, start: usize, open: u8, close: u8) -> Option<RawInlineField> {
    let separator = line.get(start + 1..)?.find("::")? + start + 1;
    let key = line.get(start + 1..separator)?.trim();
    if key
        .bytes()
        .any(|byte| matches!(byte, b'[' | b']' | b'(' | b')'))
    {
        return None;
    }
    let value_start = separator + 2;
    let (value, end) = find_closing(line, value_start, open, close)?;
    Some(RawInlineField {
        key: key.to_string(),
        value,
        start,
        end,
    })
}

fn find_closing(line: &str, start: usize, open: u8, close: u8) -> Option<(String, usize)> {
    let bytes = line.as_bytes();
    let mut nesting = 0i32;
    let mut escaped = false;
    for (index, &byte) in bytes.iter().enumerate().skip(start) {
        if byte == b'\\' {
            escaped = !escaped;
            continue;
        }
        if escaped {
            escaped = false;
            continue;
        }
        if byte == open {
            nesting += 1;
        } else if byte == close {
            nesting -= 1;
        }
        if nesting < 0 {
            return Some((line.get(start..index)?.trim().to_string(), index + 1));
        }
        escaped = false;
    }
    None
}

fn extract_full_line_field(line: &str) -> FullLineFieldParse {
    let Some(separator) = line.find("::") else {
        return FullLineFieldParse::NoMatch;
    };
    let Some(raw_key) = line.get(..separator) else {
        return FullLineFieldParse::NoMatch;
    };
    let key = match strip_full_line_key(raw_key.trim()) {
        FullLineKeyParse::Match(key) => key,
        FullLineKeyParse::NoMatch => return FullLineFieldParse::NoMatch,
        FullLineKeyParse::Incomplete => return FullLineFieldParse::Incomplete,
    };
    let Some(value) = line.get(separator + 2..) else {
        return FullLineFieldParse::NoMatch;
    };
    FullLineFieldParse::Match(RawInlineField {
        key,
        value: value.trim().to_string(),
        start: 0,
        end: line.len(),
    })
}

fn strip_full_line_key(key: &str) -> FullLineKeyParse {
    let mut middle_start = None;
    for (index, ch) in key.char_indices() {
        if is_full_key_start(ch) {
            middle_start = Some(index);
            break;
        }
        if is_unproven_emoji(ch) {
            return FullLineKeyParse::Incomplete;
        }
    }
    let Some(start) = middle_start else {
        return FullLineKeyParse::Match(String::new());
    };
    let mut end = key.len();
    for (relative, ch) in key[start..].char_indices() {
        if !is_full_key_middle(ch) {
            if is_unproven_emoji(ch) {
                return FullLineKeyParse::Incomplete;
            }
            end = start + relative;
            break;
        }
    }
    for ch in key[end..].chars() {
        if is_unproven_emoji(ch) {
            return FullLineKeyParse::Incomplete;
        }
        if !matches!(ch, '_' | '*' | '~' | '`') {
            return FullLineKeyParse::NoMatch;
        }
    }
    FullLineKeyParse::Match(key[start..end].to_string())
}

fn is_full_key_start(ch: char) -> bool {
    is_unicode_letter(ch) || ch.is_ascii_digit() || ch == '_' || is_proven_emoji(ch)
}

fn is_full_key_middle(ch: char) -> bool {
    is_full_key_start(ch) || ch.is_whitespace() || matches!(ch, '-' | '/')
}

fn is_unicode_letter(ch: char) -> bool {
    char_matches(&UNICODE_LETTER, ch)
}

fn is_proven_emoji(ch: char) -> bool {
    char_matches(&EMOJI_PRESENTATION, ch)
}

fn is_unproven_emoji(ch: char) -> bool {
    !ch.is_ascii() && !is_proven_emoji(ch) && char_matches(&POSSIBLE_EMOJI, ch)
}

fn char_matches(regex: &Regex, ch: char) -> bool {
    let mut encoded = [0u8; 4];
    regex.is_match(ch.encode_utf8(&mut encoded))
}

fn extract_emoji_fields(line: &str) -> Vec<RawInlineField> {
    let specs: [(&str, &[&str]); 5] = [
        ("created", &["➕"]),
        ("start", &["🛫"]),
        ("scheduled", &["⏳", "⌛"]),
        ("due", &["📅", "📆", "🗓️", "🗓"]),
        ("completion", &["✅"]),
    ];
    let mut fields = Vec::new();
    for (key, markers) in specs {
        let Some((start, marker)) = markers
            .iter()
            .filter_map(|marker| line.find(marker).map(|start| (start, *marker)))
            .min_by_key(|(start, _)| *start)
        else {
            continue;
        };
        let mut value_start = start + marker.len();
        while let Some(ch) = line[value_start..].chars().next()
            && ch.is_whitespace()
        {
            value_start += ch.len_utf8();
        }
        let Some(candidate) = line.get(value_start..value_start.saturating_add(10)) else {
            continue;
        };
        if !looks_like_ymd(candidate) {
            continue;
        }
        fields.push(RawInlineField {
            key: key.to_string(),
            value: candidate.to_string(),
            start,
            end: value_start + 10,
        });
    }
    fields
}

fn parse_inline_value(raw: &str) -> (DqlInlineValue, bool) {
    let value = raw.trim();
    if value.is_empty() {
        return (DqlInlineValue::Null, false);
    }
    match parse_atom(value) {
        AtomParse::Match(value) => return (value, false),
        AtomParse::Incomplete => return (DqlInlineValue::Text(value.to_string()), true),
        AtomParse::NoMatch => {}
    }
    if let Some(parts) = split_inline_list(value) {
        let mut values = Vec::with_capacity(parts.len());
        for part in parts {
            match parse_atom(&part) {
                AtomParse::Match(value) => values.push(value),
                AtomParse::Incomplete => {
                    return (DqlInlineValue::Text(value.to_string()), true);
                }
                AtomParse::NoMatch => return (DqlInlineValue::Text(value.to_string()), false),
            }
        }
        return (DqlInlineValue::List(values), false);
    }
    (DqlInlineValue::Text(value.to_string()), false)
}

fn parse_atom(value: &str) -> AtomParse {
    match parse_date(value) {
        AtomParse::NoMatch => {}
        result => return result,
    }
    if is_duration(value) {
        return AtomParse::Match(DqlInlineValue::Duration(value.to_string()));
    }
    if let Some(text) = parse_quoted(value) {
        return AtomParse::Match(DqlInlineValue::Text(text));
    }
    if is_tag(value) {
        return AtomParse::Match(DqlInlineValue::Tag(value.to_string()));
    }
    match parse_link(value) {
        Ok(Some(link)) => return AtomParse::Match(DqlInlineValue::Link(link)),
        Ok(None) => {}
        Err(()) => return AtomParse::Incomplete,
    }
    match value {
        "true" | "True" => return AtomParse::Match(DqlInlineValue::Boolean(true)),
        "false" | "False" => return AtomParse::Match(DqlInlineValue::Boolean(false)),
        "null" => return AtomParse::Match(DqlInlineValue::Null),
        _ => {}
    }
    if is_number(value) {
        return value
            .parse::<f64>()
            .ok()
            .filter(|number| number.is_finite())
            .map(|number| AtomParse::Match(DqlInlineValue::Number(number)))
            .unwrap_or(AtomParse::Incomplete);
    }
    AtomParse::NoMatch
}

fn parse_date(value: &str) -> AtomParse {
    let bytes = value.as_bytes();
    if bytes.len() < 7
        || !bytes[..4].iter().all(u8::is_ascii_digit)
        || bytes[4] != b'-'
        || !bytes[5..7].iter().all(u8::is_ascii_digit)
    {
        return AtomParse::NoMatch;
    }
    let year = value[..4].parse::<i32>().ok();
    let month = value[5..7].parse::<u32>().ok();
    let mut index = 7usize;
    let mut day = 1u32;
    let mut day_seen = false;
    let mut hour = 0u32;
    let mut minute = 0u32;
    let mut second = 0u32;
    let mut seconds_seen = false;
    let mut named_zone = false;

    if bytes.get(index) == Some(&b'-') {
        let Some(slice) = value.get(index + 1..index + 3) else {
            return AtomParse::NoMatch;
        };
        if !slice.bytes().all(|byte| byte.is_ascii_digit()) {
            return AtomParse::NoMatch;
        }
        day = slice.parse().unwrap_or(0);
        day_seen = true;
        index += 3;
    }
    if day_seen && bytes.get(index) == Some(&b'T') {
        let Some(slice) = value.get(index + 1..index + 3) else {
            return AtomParse::NoMatch;
        };
        if !slice.bytes().all(|byte| byte.is_ascii_digit()) {
            return AtomParse::NoMatch;
        }
        hour = slice.parse().unwrap_or(99);
        index += 3;
        if bytes.get(index) == Some(&b':') {
            let Some(slice) = value.get(index + 1..index + 3) else {
                return AtomParse::NoMatch;
            };
            if !slice.bytes().all(|byte| byte.is_ascii_digit()) {
                return AtomParse::NoMatch;
            }
            minute = slice.parse().unwrap_or(99);
            index += 3;
            if bytes.get(index) == Some(&b':') {
                let Some(slice) = value.get(index + 1..index + 3) else {
                    return AtomParse::NoMatch;
                };
                if !slice.bytes().all(|byte| byte.is_ascii_digit()) {
                    return AtomParse::NoMatch;
                }
                second = slice.parse().unwrap_or(99);
                seconds_seen = true;
                index += 3;
                if bytes.get(index) == Some(&b'.') {
                    let Some(slice) = value.get(index + 1..index + 4) else {
                        return AtomParse::NoMatch;
                    };
                    if !slice.bytes().all(|byte| byte.is_ascii_digit()) {
                        return AtomParse::NoMatch;
                    }
                    index += 4;
                }
            }
        }
    }

    if day_seen && seconds_seen && bytes.get(index) == Some(&b'Z') {
        index += 1;
    } else if day_seen && seconds_seen && matches!(bytes.get(index), Some(b'+') | Some(b'-')) {
        index += 1;
        let start = index;
        while index < bytes.len() && bytes[index].is_ascii_digit() && index - start < 2 {
            index += 1;
        }
        if index == start {
            return AtomParse::NoMatch;
        }
        let zone_hour = value[start..index].parse::<u32>().unwrap_or(99);
        let mut zone_minute = 0u32;
        if bytes.get(index) == Some(&b':') {
            let Some(slice) = value.get(index + 1..index + 3) else {
                return AtomParse::NoMatch;
            };
            if !slice.bytes().all(|byte| byte.is_ascii_digit()) {
                return AtomParse::NoMatch;
            }
            zone_minute = slice.parse().unwrap_or(99);
            index += 3;
        }
        if zone_hour > 23 || zone_minute > 59 {
            return AtomParse::NoMatch;
        }
    } else if day_seen && seconds_seen && bytes.get(index) == Some(&b'[') {
        let Some(close) = value[index + 1..].find(']') else {
            return AtomParse::NoMatch;
        };
        let zone = &value[index + 1..index + 1 + close];
        if zone.is_empty()
            || !zone
                .chars()
                .all(|ch| ch.is_ascii_alphanumeric() || matches!(ch, '+' | '-' | '/'))
            || index + close + 2 != value.len()
        {
            return AtomParse::NoMatch;
        }
        index = value.len();
        named_zone = true;
    }

    if index != value.len()
        || NaiveDate::from_ymd_opt(year.unwrap_or(0), month.unwrap_or(0), day).is_none()
        || hour > 23
        || minute > 59
        || second > 59
    {
        return AtomParse::NoMatch;
    }
    if named_zone {
        AtomParse::Incomplete
    } else {
        AtomParse::Match(DqlInlineValue::Date(value.to_string()))
    }
}

fn is_duration(value: &str) -> bool {
    const UNITS: &[&str] = &[
        "months", "minutes", "seconds", "years", "weeks", "hours", "month", "minute", "second",
        "year", "week", "hour", "days", "yrs", "mos", "wks", "hrs", "mins", "secs", "day", "yr",
        "mo", "wk", "hr", "min", "sec", "w", "d", "h", "m", "s",
    ];
    let bytes = value.as_bytes();
    let mut index = 0usize;
    let mut count = 0usize;
    while index < bytes.len() {
        let number_start = index;
        if bytes.get(index) == Some(&b'-') {
            index += 1;
        }
        let digit_start = index;
        while index < bytes.len() && bytes[index].is_ascii_digit() {
            index += 1;
        }
        if index == digit_start {
            return false;
        }
        if bytes.get(index) == Some(&b'.') {
            index += 1;
            let fraction_start = index;
            while index < bytes.len() && bytes[index].is_ascii_digit() {
                index += 1;
            }
            if index == fraction_start {
                return false;
            }
        }
        if value[number_start..index]
            .parse::<f64>()
            .ok()
            .is_none_or(|number| !number.is_finite())
        {
            return false;
        }
        while index < bytes.len() && bytes[index].is_ascii_whitespace() {
            index += 1;
        }
        let Some(unit) = UNITS.iter().find(|unit| value[index..].starts_with(**unit)) else {
            return false;
        };
        index += unit.len();
        count += 1;
        if index == bytes.len() {
            break;
        }
        while index < bytes.len() && bytes[index].is_ascii_whitespace() {
            index += 1;
        }
        if bytes.get(index) == Some(&b',') {
            index += 1;
            while index < bytes.len() && bytes[index].is_ascii_whitespace() {
                index += 1;
            }
        }
        if index == bytes.len() {
            return false;
        }
    }
    count > 0
}

fn parse_quoted(value: &str) -> Option<String> {
    let bytes = value.as_bytes();
    if bytes.first() != Some(&b'"') || bytes.len() < 2 {
        return None;
    }
    let mut out = String::new();
    let mut index = 1usize;
    while index < bytes.len() {
        let ch = value[index..].chars().next()?;
        if ch == '"' {
            return (index + 1 == bytes.len()).then_some(out);
        }
        if ch == '\\' {
            let next_index = index + 1;
            if next_index >= bytes.len() {
                return None;
            }
            let next = value[next_index..].chars().next()?;
            if next == '"' || next == '\\' {
                out.push(next);
            } else {
                out.push('\\');
                out.push(next);
            }
            index = next_index + next.len_utf8();
        } else {
            out.push(ch);
            index += ch.len_utf8();
        }
    }
    None
}

fn is_tag(value: &str) -> bool {
    value.starts_with('#') && value[1..].chars().all(valid_tag_char)
}

fn valid_tag_char(ch: char) -> bool {
    !ch.is_whitespace()
        && !matches!(ch as u32, 0x2000..=0x206f | 0x2e00..=0x2e7f)
        && !"'!\"#$%&()*+,.:;<=>?@^`{|}~[]\\".contains(ch)
}

fn parse_link(value: &str) -> Result<Option<DqlInlineLink>, ()> {
    let (embed, rest) = value
        .strip_prefix('!')
        .map_or((false, value), |rest| (true, rest));
    let Some(inner) = rest
        .strip_prefix("[[")
        .and_then(|rest| rest.strip_suffix("]]"))
    else {
        return Ok(None);
    };
    if inner.contains('[') || inner.contains(']') {
        return Ok(None);
    }
    let mut pipe = None;
    for (index, _) in inner.match_indices('|') {
        if index > 0 && inner.as_bytes()[index - 1] == b'\\' {
            continue;
        }
        pipe = Some(index);
        break;
    }
    let (target, display) = pipe.map_or((inner, None), |index| {
        (&inner[..index], Some(inner[index + 1..].to_string()))
    });
    let target = target.replace("\\|", "|");
    let (target, link_type, subpath) = if let Some((path, block)) = target.split_once("#^") {
        (
            path.to_string(),
            DqlInlineLinkType::Block,
            Some(block.split("#^").next().unwrap_or(block).to_string()),
        )
    } else if let Some((path, heading)) = target.split_once('#') {
        (
            path.to_string(),
            DqlInlineLinkType::Header,
            Some(normalize_header_for_link(
                heading.split('#').next().unwrap_or(heading),
            )?),
        )
    } else {
        (target, DqlInlineLinkType::File, None)
    };
    Ok(Some(DqlInlineLink {
        target,
        display,
        embed,
        link_type,
        subpath,
    }))
}

fn normalize_header_for_link(header: &str) -> Result<String, ()> {
    let mut normalized = String::new();
    for ch in header.chars() {
        if is_unicode_letter(ch)
            || ch.is_ascii_digit()
            || matches!(ch, '_' | '-')
            || is_proven_emoji(ch)
        {
            normalized.push(ch);
        } else if is_unproven_emoji(ch) {
            return Err(());
        } else {
            normalized.push(' ');
        }
    }
    Ok(normalized.split_whitespace().collect::<Vec<_>>().join(" "))
}

fn is_number(value: &str) -> bool {
    let bytes = value.as_bytes();
    let mut index = usize::from(bytes.first() == Some(&b'-'));
    let start = index;
    while index < bytes.len() && bytes[index].is_ascii_digit() {
        index += 1;
    }
    if index == start {
        return false;
    }
    if bytes.get(index) == Some(&b'.') {
        index += 1;
        let fraction = index;
        while index < bytes.len() && bytes[index].is_ascii_digit() {
            index += 1;
        }
        if index == fraction {
            return false;
        }
    }
    index == bytes.len()
}

fn split_inline_list(value: &str) -> Option<Vec<String>> {
    let bytes = value.as_bytes();
    let mut parts = Vec::new();
    let mut start = 0usize;
    let mut index = 0usize;
    let mut quoted = false;
    let mut escaped = false;
    let mut link_depth = 0usize;
    while index < bytes.len() {
        let byte = bytes[index];
        if quoted {
            if escaped {
                escaped = false;
            } else if byte == b'\\' {
                escaped = true;
            } else if byte == b'"' {
                quoted = false;
            }
            index += 1;
            continue;
        }
        if byte == b'"' {
            quoted = true;
            index += 1;
            continue;
        }
        if index + 1 < bytes.len() && &bytes[index..index + 2] == b"[[" {
            link_depth += 1;
            index += 2;
            continue;
        }
        if link_depth > 0 && index + 1 < bytes.len() && &bytes[index..index + 2] == b"]]" {
            link_depth -= 1;
            index += 2;
            continue;
        }
        if byte == b',' && link_depth == 0 {
            let part = value[start..index].trim();
            if part.is_empty() {
                return None;
            }
            parts.push(part.to_string());
            start = index + 1;
        }
        index += 1;
    }
    if parts.is_empty() {
        return None;
    }
    let tail = value[start..].trim();
    if tail.is_empty() {
        return None;
    }
    parts.push(tail.to_string());

    // Dataview's duration parser consumes the longest consecutive run of
    // duration components before the outer comma-list parser advances. Keep
    // those runs as one atom (for example `1d, 2d, true` is a two-item list).
    let mut grouped = Vec::with_capacity(parts.len());
    let mut index = 0usize;
    while index < parts.len() {
        if is_duration(&parts[index]) {
            let mut end = index + 1;
            while end < parts.len() {
                let candidate = parts[index..=end].join(", ");
                if !is_duration(&candidate) {
                    break;
                }
                end += 1;
            }
            grouped.push(parts[index..end].join(", "));
            index = end;
        } else {
            grouped.push(parts[index].clone());
            index += 1;
        }
    }
    Some(grouped)
}

fn markdown_structure(source: &str) -> MarkdownStructure {
    #[cfg(test)]
    MARKDOWN_STRUCTURE_CALLS.with(|calls| calls.set(calls.get() + 1));

    let mut structure = MarkdownStructure::default();
    let fm_end = source.len() - crate::frontmatter::body_after_frontmatter(source).len();
    if fm_end > 0 {
        structure.hard_excluded.push(0..fm_end);
    }

    let mut code = Vec::new();
    let mut html = Vec::new();
    let mut tables = Vec::new();
    let mut items = Vec::new();
    let mut quotes = Vec::new();
    let options =
        Options::ENABLE_TABLES | Options::ENABLE_TASKLISTS | Options::ENABLE_STRIKETHROUGH;
    for (event, range) in Parser::new_ext(source, options).into_offset_iter() {
        match event {
            Event::Start(Tag::CodeBlock(_)) => code.push(range.start),
            Event::End(TagEnd::CodeBlock) => {
                close_range(&mut code, range.end, &mut structure.uncertain_excluded)
            }
            Event::Start(Tag::HtmlBlock) => html.push(range.start),
            Event::End(TagEnd::HtmlBlock) => {
                close_range(&mut html, range.end, &mut structure.uncertain_excluded)
            }
            Event::Start(Tag::Table(_)) => tables.push(range.start),
            Event::End(TagEnd::Table) => {
                close_range(&mut tables, range.end, &mut structure.uncertain_excluded)
            }
            Event::Start(Tag::Item) => items.push(range.start),
            Event::End(TagEnd::Item) => {
                close_range(&mut items, range.end, &mut structure.list_items)
            }
            Event::Start(Tag::BlockQuote(_)) => quotes.push(range.start),
            Event::End(TagEnd::BlockQuote(_)) => {
                close_range(&mut quotes, range.end, &mut structure.block_quotes)
            }
            Event::Rule => structure.hard_excluded.push(range),
            Event::InlineHtml(_) => structure.uncertain_excluded.push(range),
            _ => {}
        }
    }
    structure
        .uncertain_excluded
        .extend(scan_percent_comments(source));
    structure.hard_excluded = coalesce_ranges(structure.hard_excluded);
    structure.uncertain_excluded = coalesce_ranges(structure.uncertain_excluded);
    structure.list_items.sort_by_key(|range| range.start);
    structure.list_covered = coalesce_ranges(structure.list_items.clone());
    structure.block_quotes = coalesce_ranges(structure.block_quotes);
    structure
}

fn close_range(stack: &mut Vec<usize>, end: usize, output: &mut Vec<Range<usize>>) {
    if let Some(start) = stack.pop() {
        output.push(start..end);
    }
}

fn scan_percent_comments(source: &str) -> Vec<Range<usize>> {
    let mut ranges = Vec::new();
    let mut search = 0usize;
    while let Some(open) = source[search..].find("%%") {
        let start = search + open;
        let value_start = start + 2;
        let Some(close) = source[value_start..].find("%%") else {
            ranges.push(start..source.len());
            break;
        };
        let end = value_start + close + 2;
        ranges.push(start..end);
        search = end;
    }
    ranges
}

fn parse_list_first_line(line: &str) -> Option<ParsedListLine<'_>> {
    let mut index = 0usize;
    let bytes = line.as_bytes();
    while index < bytes.len() && (bytes[index].is_ascii_whitespace() || bytes[index] == b'>') {
        index += 1;
    }
    if index >= bytes.len() {
        return None;
    }
    if bytes[index].is_ascii_digit() {
        while index < bytes.len() && bytes[index].is_ascii_digit() {
            index += 1;
        }
        if !matches!(bytes.get(index), Some(b'.') | Some(b')')) {
            return None;
        }
        index += 1;
    } else if matches!(bytes[index], b'*' | b'-' | b'+') {
        index += 1;
    } else {
        return None;
    }
    while index < bytes.len() && bytes[index].is_ascii_whitespace() {
        index += 1;
    }
    let mut task = false;
    if bytes.get(index) == Some(&b'[') {
        if bytes.get(index + 1) == Some(&b']') {
            task = true;
            index += 2;
        } else if bytes.get(index + 2) == Some(&b']') {
            task = true;
            index += 3;
        }
        if task {
            while index < bytes.len() && bytes[index].is_ascii_whitespace() {
                index += 1;
            }
        }
    }
    Some(ParsedListLine {
        content: &line[index..],
        task,
    })
}

fn looks_like_unrouted_list(line: &str) -> bool {
    let bytes = line.as_bytes();
    let mut index = 0usize;
    while index < bytes.len() && (bytes[index].is_ascii_whitespace() || bytes[index] == b'>') {
        index += 1;
    }
    if bytes.get(index).is_some_and(u8::is_ascii_digit) {
        while bytes.get(index).is_some_and(u8::is_ascii_digit) {
            index += 1;
        }
        if !matches!(bytes.get(index), Some(b'.') | Some(b')')) {
            return false;
        }
        index += 1;
    } else if bytes
        .get(index)
        .is_some_and(|byte| matches!(byte, b'*' | b'-' | b'+'))
    {
        index += 1;
    } else {
        return false;
    }
    bytes.get(index).is_some_and(u8::is_ascii_whitespace)
}

fn source_lines(source: &str) -> Vec<SourceLine<'_>> {
    let mut lines = Vec::new();
    let mut start = 0usize;
    for chunk in source.split_inclusive('\n') {
        let end = start + chunk.len();
        let without_lf = chunk.strip_suffix('\n').unwrap_or(chunk);
        let text = without_lf.strip_suffix('\r').unwrap_or(without_lf);
        lines.push(SourceLine { start, end, text });
        start = end;
    }
    if source.is_empty() {
        lines.push(SourceLine {
            start: 0,
            end: 0,
            text: "",
        });
    }
    lines
}

fn range_intersects_any(range: &Range<usize>, others: &[Range<usize>]) -> bool {
    let index = others.partition_point(|other| other.end <= range.start);
    others
        .get(index)
        .is_some_and(|other| other.start < range.end)
}

fn containing_range(byte: usize, ranges: &[Range<usize>]) -> Option<&Range<usize>> {
    let index = ranges.partition_point(|range| range.end <= byte);
    ranges.get(index).filter(|range| range.contains(&byte))
}

fn coalesce_ranges(mut ranges: Vec<Range<usize>>) -> Vec<Range<usize>> {
    ranges.sort_by_key(|range| range.start);
    let mut output: Vec<Range<usize>> = Vec::with_capacity(ranges.len());
    for range in ranges {
        match output.last_mut() {
            Some(previous) if range.start <= previous.end => {
                previous.end = previous.end.max(range.end);
            }
            _ => output.push(range),
        }
    }
    output
}

fn range_contains_candidate(source: &str, range: &Range<usize>, include_task: bool) -> bool {
    source
        .get(range.clone())
        .is_some_and(|text| has_inline_candidate(text, include_task))
}

fn has_inline_candidate(text: &str, include_task: bool) -> bool {
    text.contains("::")
        || (include_task
            && ["➕", "🛫", "⏳", "⌛", "📅", "📆", "🗓", "✅"]
                .iter()
                .any(|marker| text.contains(marker)))
}

fn utf16_len_exceeds(value: &str, limit: usize) -> bool {
    value.encode_utf16().take(limit + 1).count() > limit
}

fn looks_like_ymd(value: &str) -> bool {
    let bytes = value.as_bytes();
    bytes.len() == 10
        && bytes[..4].iter().all(u8::is_ascii_digit)
        && bytes[4] == b'-'
        && bytes[5..7].iter().all(u8::is_ascii_digit)
        && bytes[7] == b'-'
        && bytes[8..].iter().all(u8::is_ascii_digit)
}

#[cfg(test)]
mod tests {
    use super::*;
    use rusqlite::{Connection, params};

    fn field(key: &str, value: DqlInlineValue) -> DqlInlineField {
        DqlInlineField {
            key: key.to_string(),
            value,
        }
    }

    #[test]
    fn candidate_free_pages_skip_markdown_structure() {
        MARKDOWN_STRUCTURE_CALLS.with(|calls| calls.set(0));
        let source = "ordinary prose without inline metadata\n\n".repeat(256);

        assert_eq!(
            parse_inline_projection(&source),
            DqlInlineProjection::default()
        );
        assert_eq!(MARKDOWN_STRUCTURE_CALLS.with(std::cell::Cell::get), 0);
    }

    #[test]
    fn page_projection_orders_non_task_list_fields_before_body_fields() {
        let source = "Paragraph [page:: 2]\n\n- list:: 1\n- [ ] task:: 99\n\nLater [other:: 3]\n";
        let projection = parse_inline_projection(source);

        assert!(!projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![
                field("list", DqlInlineValue::Number(1.0)),
                field("page", DqlInlineValue::Number(2.0)),
                field("other", DqlInlineValue::Number(3.0)),
            ]
        );
    }

    #[test]
    fn wrapped_fields_handle_nesting_escapes_order_and_adjacency() {
        let source = concat!(
            "full:: ignored [x:: 1] (y:: [nested (ok)])\n",
            r"[odd:: before \] after]",
            "\n",
            r"[even:: before \\] after]",
            "\n",
            "[first:: 1][adjacent:: 2] [spaced:: 3]\n",
        );
        let projection = parse_inline_projection(source);

        assert!(!projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![
                field("x", DqlInlineValue::Number(1.0)),
                field("y", DqlInlineValue::Text("[nested (ok)]".into())),
                field("odd", DqlInlineValue::Text(r"before \] after".into())),
                field("even", DqlInlineValue::Text(r"before \\".into())),
                field("first", DqlInlineValue::Number(1.0)),
                field("spaced", DqlInlineValue::Number(3.0)),
            ]
        );
    }

    #[test]
    fn full_line_parser_strips_markdown_header_and_quote_decorations() {
        let projection = parse_inline_projection(
            "**Rating**:: 5\n# Header:: true\n> Quote:: null\n\nInvalid!:: raw\n",
        );

        assert!(!projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![
                field("Rating", DqlInlineValue::Number(5.0)),
                field("Header", DqlInlineValue::Boolean(true)),
                field("Quote", DqlInlineValue::Null),
            ]
        );
    }

    #[test]
    fn non_task_list_emoji_fields_are_page_fields_but_task_fields_are_not() {
        let projection = parse_inline_projection(
            "- book [rating:: 5] 📅 2026-01-02\n- [x] done [ignored:: 9] 📅 2026-01-03\n",
        );

        assert!(!projection.incomplete);
        assert_eq!(
            projection.fields,
            vec![
                field("rating", DqlInlineValue::Number(5.0)),
                field("due", DqlInlineValue::Date("2026-01-02".into())),
            ]
        );
    }

    #[test]
    fn frontmatter_fences_and_comments_are_excluded_but_inline_code_is_raw_text() {
        let source = concat!(
            "---\nfront:: 1\n---\n",
            "```text\ncode:: 2\n```\n",
            "%% [comment:: 3] %%\n",
            "inline-code:: `still scanned`\n",
        );
        let projection = parse_inline_projection(source);

        assert!(projection.incomplete);
        assert_eq!(
            projection.fields,
            vec![field(
                "inline-code",
                DqlInlineValue::Text("`still scanned`".into()),
            )]
        );
    }

    #[test]
    fn table_and_html_candidates_are_omitted_and_fail_loud() {
        let projection = parse_inline_projection(concat!(
            "| key |\n| --- |\n| table:: 1 |\n\n",
            "<div>\nhtml:: 2\n</div>\n\n",
            "inline <span>markup</span> [candidate:: 4]\n",
            "safe:: 3\n",
        ));

        assert!(projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![field("safe", DqlInlineValue::Number(3.0))]
        );
    }

    #[test]
    fn unclosed_percent_comment_candidate_fails_loud() {
        let projection = parse_inline_projection("safe:: 1\n%% comment:: 2\n");

        assert!(projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![field("safe", DqlInlineValue::Number(1.0))]
        );
    }

    #[test]
    fn values_cover_null_atoms_and_raw_text_fallback() {
        let projection = parse_inline_projection(concat!(
            "empty::\n",
            "atoms:: 1, 2026-01-02, [[Target|Shown]]\n",
            "duration:: 1yr, 2mo\n",
            "quoted:: \"hello, world\"\n",
            "tagged:: #Project/Sub\n",
            "embedded:: ![[Image.png]]\n",
            "raw:: not, a, valid, atom list\n",
        ));

        assert!(!projection.incomplete);
        assert_eq!(projection.fields[0], field("empty", DqlInlineValue::Null));
        assert_eq!(
            projection.fields[1],
            field(
                "atoms",
                DqlInlineValue::List(vec![
                    DqlInlineValue::Number(1.0),
                    DqlInlineValue::Date("2026-01-02".into()),
                    DqlInlineValue::Link(DqlInlineLink {
                        target: "Target".into(),
                        display: Some("Shown".into()),
                        embed: false,
                        link_type: DqlInlineLinkType::File,
                        subpath: None,
                    }),
                ]),
            )
        );
        assert_eq!(
            projection.fields[2],
            field("duration", DqlInlineValue::Duration("1yr, 2mo".into()))
        );
        assert_eq!(
            projection.fields[3],
            field("quoted", DqlInlineValue::Text("hello, world".into()))
        );
        assert_eq!(
            projection.fields[4],
            field("tagged", DqlInlineValue::Tag("#Project/Sub".into()))
        );
        assert_eq!(
            projection.fields[5],
            field(
                "embedded",
                DqlInlineValue::Link(DqlInlineLink {
                    target: "Image.png".into(),
                    display: None,
                    embed: true,
                    link_type: DqlInlineLinkType::File,
                    subpath: None,
                }),
            )
        );
        assert_eq!(
            projection.fields[6],
            field(
                "raw",
                DqlInlineValue::Text("not, a, valid, atom list".into()),
            )
        );
    }

    #[test]
    fn mixed_atom_lists_group_maximal_duration_runs() {
        let projection = parse_inline_projection("mixed:: 1d, 2d, true\n");

        assert!(!projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![field(
                "mixed",
                DqlInlineValue::List(vec![
                    DqlInlineValue::Duration("1d, 2d".into()),
                    DqlInlineValue::Boolean(true),
                ]),
            )]
        );
    }

    #[test]
    fn date_zones_require_full_seconds() {
        let projection = parse_inline_projection(concat!(
            "partial:: 2026-07Z\n",
            "hour:: 2026-07-10T12Z\n",
            "no-day:: 2026-07T12:00:00Z\n",
            "full:: 2026-07-10T12:00:00Z\n",
        ));

        assert!(!projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![
                field("partial", DqlInlineValue::Text("2026-07Z".into())),
                field("hour", DqlInlineValue::Text("2026-07-10T12Z".into())),
                field("no-day", DqlInlineValue::Text("2026-07T12:00:00Z".into()),),
                field("full", DqlInlineValue::Date("2026-07-10T12:00:00Z".into()),),
            ]
        );
    }

    #[test]
    fn proven_emoji_keys_are_preserved_and_uncertain_emoji_fail_loud() {
        let projection = parse_inline_projection(concat!(
            "📚 Shelf:: 1\n",
            "☀︎ Weather:: 2\n",
            "link:: [[Target#☀︎ Weather]]\n",
        ));

        assert!(projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![
                field("📚 Shelf", DqlInlineValue::Number(1.0)),
                field("link", DqlInlineValue::Text("[[Target#☀︎ Weather]]".into()),),
            ]
        );
    }

    #[test]
    fn links_preserve_file_header_block_subpaths_and_display() {
        let projection = parse_inline_projection(concat!(
            "header:: [[Target#  Heading *Name  |Shown]]\n",
            "block:: ![[Target#^block-id]]\n",
        ));
        assert_eq!(
            projection.fields,
            vec![
                field(
                    "header",
                    DqlInlineValue::Link(DqlInlineLink {
                        target: "Target".into(),
                        display: Some("Shown".into()),
                        embed: false,
                        link_type: DqlInlineLinkType::Header,
                        subpath: Some("Heading Name".into()),
                    }),
                ),
                field(
                    "block",
                    DqlInlineValue::Link(DqlInlineLink {
                        target: "Target".into(),
                        display: None,
                        embed: true,
                        link_type: DqlInlineLinkType::Block,
                        subpath: Some("block-id".into()),
                    }),
                ),
            ]
        );
    }

    #[test]
    fn duplicate_exact_keys_keep_list_then_page_order() {
        let projection = parse_inline_projection("same:: 1\n\n- same:: 2\n\nsame:: 3\n");
        assert_eq!(
            projection.fields,
            vec![
                field("same", DqlInlineValue::Number(2.0)),
                field("same", DqlInlineValue::Number(1.0)),
                field("same", DqlInlineValue::Number(3.0)),
            ]
        );
    }

    #[test]
    fn ambiguous_multiline_list_and_named_zone_date_mark_page_incomplete() {
        let projection = parse_inline_projection(concat!(
            "- first line\n  continuation:: 1\n",
            "zone:: 2026-07-10T12:00[America/Toronto]\n",
        ));
        assert!(projection.incomplete);
    }

    #[test]
    fn overlong_lines_are_safely_skipped() {
        let source = format!("{} [late:: 1]\nkept:: 2\n", "x".repeat(32_769));
        let projection = parse_inline_projection(&source);
        assert!(!projection.incomplete);
        assert_eq!(
            projection.fields,
            vec![field("kept", DqlInlineValue::Number(2.0))]
        );
    }

    #[test]
    fn overlong_non_task_list_lines_are_still_indexed() {
        let source = format!("- {} [late:: 1]\n", "x".repeat(32_769));
        let projection = parse_inline_projection(&source);

        assert!(!projection.incomplete, "{projection:?}");
        assert_eq!(
            projection.fields,
            vec![field("late", DqlInlineValue::Number(1.0))]
        );
    }

    #[test]
    fn writer_and_loader_round_trip_order_and_incomplete_state() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        let source = "page:: 1\n\n- list:: 2\n\nzone:: 2026-07-10T12:00:00[America/Toronto]\n";
        assert!(parse_inline_projection(source).incomplete);
        write_projection(&conn, 1, source);

        let stored_incomplete: i64 = conn
            .query_row(
                "SELECT incomplete FROM dql_inline_field_state WHERE file_id = 1",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(stored_incomplete, 1);

        let loaded = load_dql_inline_fields_for_path(&conn, "n1.md").unwrap();
        assert!(loaded.incomplete);
        assert_eq!(
            loaded.fields,
            vec![
                field("list", DqlInlineValue::Number(2.0)),
                field("page", DqlInlineValue::Number(1.0)),
                field(
                    "zone",
                    DqlInlineValue::Text("2026-07-10T12:00:00[America/Toronto]".into()),
                ),
            ]
        );
        assert_eq!(
            load_dql_inline_fields_for_path(&conn, "missing.md").unwrap(),
            DqlInlineProjection::default()
        );
    }

    #[test]
    fn writer_reindexes_and_purges_fields_and_state() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_projection(&conn, 1, "old:: 1\n");
        assert_eq!(raw_keys(&conn, 1), vec!["old"]);

        write_projection(&conn, 1, "new:: 2\nother:: 3\n");
        assert_eq!(raw_keys(&conn, 1), vec!["new", "other"]);

        write_projection(&conn, 1, "");
        assert!(raw_keys(&conn, 1).is_empty());
        let incomplete: i64 = conn
            .query_row(
                "SELECT incomplete FROM dql_inline_field_state WHERE file_id = 1",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(incomplete, 0);
    }

    #[test]
    fn new_file_writer_does_not_issue_redundant_deletes() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        conn.execute_batch(
            "CREATE TRIGGER reject_new_file_field_delete
             BEFORE DELETE ON dql_inline_fields
             BEGIN SELECT RAISE(ABORT, 'unexpected field delete'); END;
             CREATE TRIGGER reject_new_file_state_delete
             BEFORE DELETE ON dql_inline_field_state
             BEGIN SELECT RAISE(ABORT, 'unexpected state delete'); END;",
        )
        .unwrap();

        let tx = conn.unchecked_transaction().unwrap();
        insert_dql_inline_fields_for_new_file(&tx, 1, "field:: 1\n").unwrap();
        tx.commit().unwrap();

        assert_eq!(raw_keys(&conn, 1), vec!["field"]);
        assert!(
            !load_dql_inline_fields_for_path(&conn, "n1.md")
                .unwrap()
                .incomplete
        );
    }

    #[test]
    fn scanner_limit_purge_removes_stale_fields_and_marks_incomplete() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_projection(&conn, 1, "stale:: 1\n");

        let tx = conn.unchecked_transaction().unwrap();
        mark_dql_inline_fields_incomplete_for_file(&tx, 1).unwrap();
        tx.commit().unwrap();

        let loaded = load_dql_inline_fields_for_path(&conn, "n1.md").unwrap();
        assert!(loaded.incomplete);
        assert!(loaded.fields.is_empty());
    }

    #[test]
    fn deleting_file_cascades_inline_fields_and_state() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_projection(&conn, 1, "one:: 1\n");
        conn.execute("DELETE FROM files WHERE id = 1", []).unwrap();

        for table in ["dql_inline_fields", "dql_inline_field_state"] {
            let count: i64 = conn
                .query_row(
                    &format!("SELECT COUNT(*) FROM {table} WHERE file_id = 1"),
                    [],
                    |row| row.get(0),
                )
                .unwrap();
            assert_eq!(count, 0, "{table}");
        }
    }

    #[test]
    fn failed_rebuild_rolls_back_fields_and_state_together() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        write_projection(&conn, 1, "old:: 1\n");
        conn.execute_batch(
            "CREATE TRIGGER fail_inline_field_insert
             BEFORE INSERT ON dql_inline_fields
             WHEN NEW.key = 'boom'
             BEGIN
               SELECT RAISE(ABORT, 'forced inline-field failure');
             END;",
        )
        .unwrap();

        let tx = conn.unchecked_transaction().unwrap();
        let error = replace_dql_inline_fields_for_file(&tx, 1, "new:: 2\nboom:: 3\n").unwrap_err();
        assert!(error.to_string().contains("forced inline-field failure"));
        tx.rollback().unwrap();

        assert_eq!(raw_keys(&conn, 1), vec!["old"]);
        assert!(
            !load_dql_inline_fields_for_path(&conn, "n1.md")
                .unwrap()
                .incomplete
        );
    }

    #[test]
    fn body_projection_does_not_change_frontmatter_properties() {
        let conn = migrated_conn();
        seed_file(&conn, 1);
        let source = "---\nfront: keep\n---\nbody:: 2\n";
        let tx = conn.unchecked_transaction().unwrap();
        crate::properties_db::replace_properties_for_file(&tx, 1, source).unwrap();
        replace_dql_inline_fields_for_file(&tx, 1, source).unwrap();
        tx.commit().unwrap();

        let property_keys: Vec<String> = conn
            .prepare("SELECT key FROM properties WHERE file_id = 1 ORDER BY ordinal")
            .unwrap()
            .query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap();
        assert_eq!(property_keys, vec!["front"]);
        assert_eq!(raw_keys(&conn, 1), vec!["body"]);
    }

    fn migrated_conn() -> Connection {
        let mut conn = crate::db::open_in_memory(512).unwrap();
        crate::db::migrate(&mut conn).unwrap();
        conn
    }

    fn seed_file(conn: &Connection, id: i64) {
        conn.execute(
            "INSERT INTO files (id, path, name, extension, mtime_ms, ctime_ms, size_bytes,
                 content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
             VALUES (?1, ?2, ?2, 'md', 1, 1, 0, '', 1, 0, 1, '')",
            params![id, format!("n{id}.md")],
        )
        .unwrap();
    }

    fn write_projection(conn: &Connection, id: i64, source: &str) {
        let tx = conn.unchecked_transaction().unwrap();
        replace_dql_inline_fields_for_file(&tx, id, source).unwrap();
        tx.commit().unwrap();
    }

    fn raw_keys(conn: &Connection, id: i64) -> Vec<String> {
        conn.prepare("SELECT key FROM dql_inline_fields WHERE file_id = ?1 ORDER BY ordinal ASC")
            .unwrap()
            .query_map(params![id], |row| row.get(0))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap()
    }
}
