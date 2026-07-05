// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate list <vault-path> [--markdown-only]` (M-5, #536).
//!
//! Drains `list_files(All | MarkdownOnly, …)` to exhaustion and prints
//! the file summaries (m_spec §M-5). The CLI is the one consumer allowed
//! to page a whole listing into memory (bounded by vault size).
//!
//! `data` shape (the `slate.cli.v1` stability contract):
//! ```json
//! { "files": [{ "path": String, "name": String,
//!              "size_bytes": u64, "mtime_ms": i64 }] }
//! ```
//! Fields are the `FileSummary` slim shape (m_spec §M-5). tsv columns:
//! `path name size_bytes mtime_ms`. Human: one path per line.

use slate_core::session::{CancelToken, FileFilter, FileSummary, Paging};

use crate::output::{CommandOutput, tsv_row};
use crate::session::{CliError, map_vault_error, open_and_scan};

/// Page size for the drain loop. Large enough to keep the round-trip
/// count low on big vaults, small enough to bound peak memory per page.
const PAGE_SIZE: u32 = 1000;

/// Run `slate list`. `markdown_only` selects the `MarkdownOnly` filter
/// (the `--markdown-only` flag); otherwise every indexed file is listed.
pub fn run(
    raw_path: &std::path::Path,
    markdown_only: bool,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let (session, abs_path) = open_and_scan(raw_path, cancel)?;

    let filter = if markdown_only {
        FileFilter::MarkdownOnly
    } else {
        FileFilter::All
    };

    // Drain every page: feed `next_cursor` back until it's `None`. The
    // CLI is the sanctioned drain consumer (m_spec §M-5).
    let mut files: Vec<FileSummary> = Vec::new();
    let mut cursor: Option<String> = None;
    loop {
        let paging = match cursor.take() {
            Some(c) => Paging::after(c, PAGE_SIZE),
            None => Paging::first(PAGE_SIZE),
        };
        let page = session
            .list_files(filter, paging)
            .map_err(map_vault_error)?;
        files.extend(page.items);
        cursor = page.next_cursor;
        if cursor.is_none() {
            break;
        }
    }

    let data = serde_json::json!({
        "files": files.iter().map(file_json).collect::<Vec<_>>(),
    });
    let human = render_human(&files);
    let tsv = render_tsv(&files);

    Ok((
        abs_path,
        CommandOutput {
            data,
            human,
            tsv,
            human_verbatim: false,
        },
    ))
}

// --- json shaping ----------------------------------------------------

fn file_json(f: &FileSummary) -> serde_json::Value {
    serde_json::json!({
        "path": f.path,
        "name": f.name,
        "size_bytes": f.size_bytes,
        "mtime_ms": f.mtime_ms,
    })
}

// --- human format ----------------------------------------------------

/// Human format (m_spec §M-5): one path per line. An empty vault prints
/// nothing.
fn render_human(files: &[FileSummary]) -> String {
    files
        .iter()
        .map(|f| f.path.clone())
        .collect::<Vec<_>>()
        .join("\n")
}

// --- tsv format ------------------------------------------------------

/// TSV format (m_spec §M-5): header `path name size_bytes mtime_ms`,
/// one row per file.
fn render_tsv(files: &[FileSummary]) -> String {
    let mut rows = vec![tsv_row(["path", "name", "size_bytes", "mtime_ms"])];
    for f in files {
        rows.push(tsv_row([
            f.path.as_str(),
            f.name.as_str(),
            &f.size_bytes.to_string(),
            &f.mtime_ms.to_string(),
        ]));
    }
    rows.join("\n")
}
