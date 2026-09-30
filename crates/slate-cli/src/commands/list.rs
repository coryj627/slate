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

use slate_core::session::{CancelToken, FileFilter, FileSummary, Paging, VaultSession};

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

    let files = list_all_files(&session, filter, cancel, PAGE_SIZE, &mut |_| {})?;

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

/// Drain every page: feed `next_cursor` back until it's `None`. The CLI is
/// the sanctioned drain consumer (m_spec §M-5). `after_page` runs after
/// each page with the number of files drained so far (a test seam; `run`
/// passes a no-op).
fn list_all_files(
    session: &VaultSession,
    filter: FileFilter,
    cancel: &CancelToken,
    page_size: u32,
    after_page: &mut dyn FnMut(usize),
) -> Result<Vec<FileSummary>, CliError> {
    let mut files: Vec<FileSummary> = Vec::new();
    let mut cursor: Option<String> = None;
    loop {
        let paging = match cursor.take() {
            Some(c) => Paging::after(c, page_size),
            None => Paging::first(page_size),
        };
        let page = session
            .list_files(filter, paging, cancel)
            .map_err(map_vault_error)?;
        files.extend(page.items);
        after_page(files.len());
        cursor = page.next_cursor;
        if cursor.is_none() {
            break;
        }
    }
    Ok(files)
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

#[cfg(test)]
mod tests {
    use super::*;
    use crate::session::open_and_scan;

    /// A vault of `count` notes, opened and scanned with a live token.
    fn scanned_vault(count: usize) -> (tempfile::TempDir, VaultSession) {
        let tmp = tempfile::tempdir().unwrap();
        for i in 0..count {
            std::fs::write(tmp.path().join(format!("n{i:02}.md")), "# Note\n").unwrap();
        }
        let (session, _) = open_and_scan(tmp.path(), &CancelToken::new()).unwrap();
        (tmp, session)
    }

    /// W7-7 PR 7 (codex AR-18 review round 2, finding 8): Ctrl-C AFTER the
    /// scan — the shared token cancelled before the listing — aborts the
    /// listing to `Cancelled` (exit 130), never a complete result.
    #[test]
    fn listing_honours_a_token_cancelled_after_the_scan() {
        let (_tmp, session) = scanned_vault(5);
        let cancel = CancelToken::new();
        cancel.cancel();

        let result = list_all_files(&session, FileFilter::All, &cancel, 2, &mut |_| {});

        assert!(matches!(result, Err(CliError::Cancelled)), "{result:?}");
    }

    /// Finding 8: Ctrl-C DURING page retrieval — after the first page — the
    /// next page's query takes the shared token and aborts to `Cancelled`.
    #[test]
    fn listing_stops_when_cancelled_during_page_retrieval() {
        let (_tmp, session) = scanned_vault(5);
        let cancel = CancelToken::new();
        let mut pages = 0;

        let result = list_all_files(&session, FileFilter::All, &cancel, 2, &mut |_| {
            pages += 1;
            cancel.cancel();
        });

        assert!(matches!(result, Err(CliError::Cancelled)), "{result:?}");
        assert_eq!(pages, 1, "a page after the cancel was drained");
    }
}
