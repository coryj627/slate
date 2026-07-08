// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite persistence for Bases scanner indexes.
//!
//! `bases_files` summarizes standalone `.base` files; `bases_blocks`
//! records supported query fences in Markdown. Both tables are derived
//! cache state and are replaced wholesale per owning file on the scanner
//! slow path.

use rusqlite::{Transaction, params};
use serde_json::json;

use crate::VaultError;
use crate::bases::{BaseFile, BaseWarningKind, RowSource, ViewDef, ViewType, parse_base};
use crate::code::extract_code_blocks;

const FENCE_BASE: i64 = 0;
const FENCE_SLATE_QUERY: i64 = 1;
const FENCE_DATAVIEW: i64 = 2;

pub(crate) fn replace_base_file_for_file(
    tx: &Transaction,
    file_id: i64,
    name: &str,
    source: &str,
    parser_version: u32,
    indexed_at_ms: i64,
) -> Result<(), VaultError> {
    let (base, warnings) = parse_base(source);
    let display_name = base_display_name(name);
    let summary = base_summary_json(
        &base,
        warnings
            .iter()
            .any(|w| matches!(w.kind, BaseWarningKind::ParseFailed)),
    );
    tx.execute(
        "INSERT INTO bases_files
            (file_id, name, parsed_query_json, warning_count, parser_version, indexed_at_ms)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6)
         ON CONFLICT(file_id) DO UPDATE SET
            name              = excluded.name,
            parsed_query_json = excluded.parsed_query_json,
            warning_count     = excluded.warning_count,
            parser_version    = excluded.parser_version,
            indexed_at_ms     = excluded.indexed_at_ms",
        params![
            file_id,
            display_name,
            summary,
            warnings.len() as i64,
            parser_version as i64,
            indexed_at_ms,
        ],
    )?;
    Ok(())
}

fn base_display_name(file_name: &str) -> String {
    std::path::Path::new(file_name)
        .file_stem()
        .map(|stem| stem.to_string_lossy().into_owned())
        .filter(|stem| !stem.is_empty())
        .unwrap_or_else(|| file_name.to_string())
}

pub(crate) fn delete_base_file_for_file(tx: &Transaction, file_id: i64) -> Result<(), VaultError> {
    tx.execute(
        "DELETE FROM bases_files WHERE file_id = ?1",
        params![file_id],
    )?;
    Ok(())
}

pub(crate) fn replace_base_blocks_for_file(
    tx: &Transaction,
    file_id: i64,
    markdown_source: &str,
) -> Result<(), VaultError> {
    tx.execute(
        "DELETE FROM bases_blocks WHERE file_id = ?1",
        params![file_id],
    )?;

    let blocks = extract_code_blocks(markdown_source);
    if blocks.is_empty() {
        return Ok(());
    }

    let mut stmt = tx.prepare_cached(
        "INSERT INTO bases_blocks (file_id, fence_kind, source_text, line, byte_offset)
         VALUES (?1, ?2, ?3, ?4, ?5)",
    )?;
    for block in blocks {
        let Some(kind) = block.language.as_deref().and_then(fence_kind) else {
            continue;
        };
        stmt.execute(params![
            file_id,
            kind,
            block.source,
            block.line as i64,
            block.byte_offset as i64,
        ])?;
    }
    Ok(())
}

fn fence_kind(language: &str) -> Option<i64> {
    match language
        .split_whitespace()
        .next()
        .unwrap_or_default()
        .to_ascii_lowercase()
        .as_str()
    {
        "base" => Some(FENCE_BASE),
        "slate-query" => Some(FENCE_SLATE_QUERY),
        "dataview" => Some(FENCE_DATAVIEW),
        "dataviewjs" => None,
        _ => None,
    }
}

fn base_summary_json(base: &BaseFile, degraded: bool) -> String {
    serde_json::to_string(&json!({
        "degraded": degraded,
        "views": base.views.iter().map(view_summary).collect::<Vec<_>>(),
    }))
    .unwrap_or_else(|_| "{\"degraded\":true,\"views\":[]}".to_string())
}

fn view_summary(view: &ViewDef) -> serde_json::Value {
    json!({
        "name": view.name,
        "type": view_type_name(&view.view_type),
        "source": row_source_name(&view.source),
    })
}

fn view_type_name(view_type: &ViewType) -> &str {
    match view_type {
        ViewType::Table => "table",
        ViewType::List => "list",
        ViewType::Cards => "cards",
        ViewType::Map => "map",
        ViewType::Other(other) => other.as_str(),
    }
}

fn row_source_name(source: &RowSource) -> &'static str {
    match source {
        RowSource::Files => "files",
        RowSource::Tasks => "tasks",
    }
}
