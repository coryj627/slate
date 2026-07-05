// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate open <vault-path>` (M-4, #535).
//!
//! Opens a vault, runs the initial scan, and prints a summary drawn
//! from `ScanReport` (session.rs) plus the `list_files` markdown total.
//! A thin wrapper over `VaultSession` — no business logic lives here
//! (m_spec §M-4 "No business logic in the CLI layer").
//!
//! `data` shape (the `slate.cli.v1` stability contract):
//! ```json
//! { "files_seen": u64, "files_indexed": u64, "files_skipped": u64,
//!   "bytes_processed": u64, "markdown_files": u64,
//!   "scan_errors": [String], "cache": "warm"|"cold" }
//! ```
//! `markdown_files` = `list_files(MarkdownOnly, first(1)).total_filtered`;
//! `cache` = `"cold"` iff `.slate/cache.sqlite` did not exist before
//! this run, else `"warm"` — decided in [`crate::session::open_vault`].

use slate_core::session::{CancelToken, FileFilter, Paging};

use crate::output::{CommandOutput, tsv_row};
use crate::progress::StderrProgress;
use crate::session::{CliError, OpenedVault, map_vault_error, open_vault};

/// Run `slate open`. Returns the absolute vault path (for the json
/// envelope) plus the rendered output, or a [`CliError`] mapped to an
/// exit code by `main`.
pub fn run(
    raw_path: &std::path::Path,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let OpenedVault {
        session,
        abs_path,
        cache_was_warm,
    } = open_vault(raw_path)?;

    // The one heavy call: scan the vault, wiring the shared cancel
    // token (so Ctrl-C aborts mid-scan) and the throttled stderr
    // progress listener.
    let report = session
        .scan_initial_with_progress(cancel, Some(StderrProgress::listener()))
        .map_err(map_vault_error)?;

    // Markdown count is the total across all pages of the
    // MarkdownOnly filter — we only need the total, so ask for the
    // smallest possible page.
    let markdown_files = session
        .list_files(FileFilter::MarkdownOnly, Paging::first(1))
        .map_err(map_vault_error)?
        .total_filtered;

    let cache = if cache_was_warm { "warm" } else { "cold" };

    let data = serde_json::json!({
        "files_seen": report.files_seen,
        "files_indexed": report.files_indexed,
        "files_skipped": report.files_skipped,
        "bytes_processed": report.bytes_processed,
        "markdown_files": markdown_files,
        "scan_errors": report.errors,
        "cache": cache,
    });

    let human = render_human(&abs_path, &report, markdown_files, cache);
    let tsv = render_tsv(&report, markdown_files, cache);

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

/// Human format (m_spec §M-4):
/// `Vault: <path>` / `Files: N (M markdown)` /
/// `Indexed: fresh|reused cache` / one line per scan error.
fn render_human(
    abs_path: &str,
    report: &slate_core::session::ScanReport,
    markdown_files: u64,
    cache: &str,
) -> String {
    let indexed = if cache == "warm" {
        "reused cache"
    } else {
        "fresh"
    };
    let mut lines = vec![
        format!("Vault: {abs_path}"),
        format!("Files: {} ({markdown_files} markdown)", report.files_seen),
        format!("Indexed: {indexed}"),
    ];
    for err in &report.errors {
        lines.push(format!("Scan error: {err}"));
    }
    lines.join("\n")
}

/// TSV format (m_spec §M-4): two columns, `field<TAB>value`, one row
/// per scalar field; `scan_errors` joined with `"; "` into one row.
fn render_tsv(
    report: &slate_core::session::ScanReport,
    markdown_files: u64,
    cache: &str,
) -> String {
    let errors_joined = report.errors.join("; ");
    let rows = [
        tsv_row(["field", "value"]),
        tsv_row(["files_seen", &report.files_seen.to_string()]),
        tsv_row(["files_indexed", &report.files_indexed.to_string()]),
        tsv_row(["files_skipped", &report.files_skipped.to_string()]),
        tsv_row(["bytes_processed", &report.bytes_processed.to_string()]),
        tsv_row(["markdown_files", &markdown_files.to_string()]),
        tsv_row(["scan_errors", &errors_joined]),
        tsv_row(["cache", cache]),
    ];
    rows.join("\n")
}
