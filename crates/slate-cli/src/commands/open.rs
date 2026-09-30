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
//!   "scan_errors": [String], "scan_error_count": u64, "cache": "warm"|"cold" }
//! ```
//! `scan_errors` is every error the scan met (collected as the scan records
//! them — the scan report itself keeps only a bounded sample);
//! `scan_error_count` (additive, W7-7 PR 7) is their count.
//! `markdown_files` = `list_files(MarkdownOnly, first(1)).total_filtered`;
//! `cache` = `"cold"` iff `.slate/cache.sqlite` did not exist before
//! this run, else `"warm"` — decided in [`crate::session::open_vault`].

use slate_core::session::{CancelToken, FileFilter, Paging, VaultSession};

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
    let (report, scan_errors) = scan_reporting_every_error(&session, cancel)?;

    // Markdown count is the total across all pages of the
    // MarkdownOnly filter — we only need the total, so ask for the
    // smallest possible page.
    let markdown_files = markdown_file_count(&session, cancel)?;

    let cache = if cache_was_warm { "warm" } else { "cold" };

    let data = open_data(&report, &scan_errors, markdown_files, cache);

    let human = render_human(&abs_path, &report, &scan_errors, markdown_files, cache);
    let tsv = render_tsv(&report, &scan_errors, markdown_files, cache);

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
    scan_errors: &[String],
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
    for err in scan_errors {
        lines.push(format!("Scan error: {err}"));
    }
    lines.join("\n")
}

/// TSV format (m_spec §M-4): two columns, `field<TAB>value`, one row
/// per scalar field; `scan_errors` joined with `"; "` into one row.
fn render_tsv(
    report: &slate_core::session::ScanReport,
    scan_errors: &[String],
    markdown_files: u64,
    cache: &str,
) -> String {
    let errors_joined = scan_errors.join("; ");
    let rows = [
        tsv_row(["field", "value"]),
        tsv_row(["files_seen", &report.files_seen.to_string()]),
        tsv_row(["files_indexed", &report.files_indexed.to_string()]),
        tsv_row(["files_skipped", &report.files_skipped.to_string()]),
        tsv_row(["bytes_processed", &report.bytes_processed.to_string()]),
        tsv_row(["markdown_files", &markdown_files.to_string()]),
        tsv_row(["scan_errors", &errors_joined]),
        tsv_row(["scan_error_count", &report.error_count.to_string()]),
        tsv_row(["cache", cache]),
    ];
    rows.join("\n")
}

/// The scan, with EVERY error it met (W7-7 PR 7, codex PR 7 round 4
/// finding 7): the report keeps an exact count and at most five samples,
/// but `slate.cli.v1`'s `scan_errors` is the complete list, so the CLI
/// collects each error as the scan records it.
fn scan_reporting_every_error(
    session: &VaultSession,
    cancel: &CancelToken,
) -> Result<(slate_core::session::ScanReport, Vec<String>), CliError> {
    let errors = std::rc::Rc::new(std::cell::RefCell::new(Vec::<String>::new()));
    let sink = std::rc::Rc::clone(&errors);
    let report = slate_core::session::with_scan_error_sink(
        move |message| sink.borrow_mut().push(message.to_string()),
        || session.scan_initial_with_progress(cancel, Some(StderrProgress::listener())),
    )
    .map_err(map_vault_error)?;
    let errors = errors.take();
    Ok((report, errors))
}

/// The `slate.cli.v1` `data` object.
fn open_data(
    report: &slate_core::session::ScanReport,
    scan_errors: &[String],
    markdown_files: u64,
    cache: &str,
) -> serde_json::Value {
    serde_json::json!({
        "files_seen": report.files_seen,
        "files_indexed": report.files_indexed,
        "files_skipped": report.files_skipped,
        "bytes_processed": report.bytes_processed,
        "markdown_files": markdown_files,
        "scan_errors": scan_errors,
        "scan_error_count": report.error_count,
        "cache": cache,
    })
}

/// The Markdown count: the total across all pages of the `MarkdownOnly`
/// filter — only the total is needed, so the smallest page is asked for.
fn markdown_file_count(session: &VaultSession, cancel: &CancelToken) -> Result<u64, CliError> {
    Ok(session
        .list_files(FileFilter::MarkdownOnly, Paging::first(1), cancel)
        .map_err(map_vault_error)?
        .total_filtered)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::Arc;

    /// A vault whose Markdown files cannot be read: one scan error each.
    struct UnreadableMarkdown(slate_core::FsVaultProvider);

    fn denied(path: &str) -> slate_core::VaultError {
        slate_core::VaultError::Io(std::io::Error::new(
            std::io::ErrorKind::PermissionDenied,
            format!("injected fault at {path}"),
        ))
    }

    impl slate_core::VaultProvider for UnreadableMarkdown {
        fn list_dir(
            &self,
            relative: &str,
        ) -> Result<Vec<slate_core::DirEntry>, slate_core::VaultError> {
            self.0.list_dir(relative)
        }
        fn read_file(&self, relative: &str) -> Result<Vec<u8>, slate_core::VaultError> {
            if relative.ends_with(".md") {
                return Err(denied(relative));
            }
            self.0.read_file(relative)
        }
        fn write_file(
            &self,
            relative: &str,
            contents: &[u8],
        ) -> Result<(), slate_core::VaultError> {
            self.0.write_file(relative, contents)
        }
        fn delete(&self, relative: &str) -> Result<(), slate_core::VaultError> {
            self.0.delete(relative)
        }
        fn rename(&self, from: &str, to: &str) -> Result<(), slate_core::VaultError> {
            self.0.rename(from, to)
        }
        fn create_dir(&self, relative: &str) -> Result<(), slate_core::VaultError> {
            self.0.create_dir(relative)
        }
        fn stat(&self, relative: &str) -> Result<slate_core::FileStat, slate_core::VaultError> {
            self.0.stat(relative)
        }
        fn watch(
            &self,
            sink: Arc<dyn slate_core::FileEventSink>,
        ) -> Result<Option<slate_core::WatchHandle>, slate_core::VaultError> {
            self.0.watch(sink)
        }
    }

    /// W7-7 PR 7 (codex PR 7 round 4, finding 7): `slate.cli.v1` is
    /// additive-only (m_spec). `scan_errors` keeps its meaning — EVERY scan
    /// error, not the report's bounded samples — and the count arrives as
    /// the new `scan_error_count` field beside it.
    #[test]
    fn v1_scan_errors_keep_every_error_beyond_the_reports_samples() {
        let tmp = tempfile::tempdir().unwrap();
        for index in 0..7 {
            std::fs::write(tmp.path().join(format!("n{index}.md")), "# Note\n").unwrap();
        }
        let session = VaultSession::open(
            Arc::new(UnreadableMarkdown(slate_core::FsVaultProvider::new(
                tmp.path().to_path_buf(),
            ))),
            slate_core::SessionConfig::new(tmp.path().join(".slate")),
        )
        .unwrap();

        let (report, errors) = scan_reporting_every_error(&session, &CancelToken::new()).unwrap();
        let data = open_data(&report, &errors, 0, "cold");

        assert_eq!(report.error_count, 7);
        assert_eq!(data["scan_errors"].as_array().unwrap().len(), 7, "{data}");
        assert_eq!(data["scan_error_count"], 7);
    }

    /// W7-7 PR 7 (codex AR-18 review round 2, finding 8): Ctrl-C after the
    /// scan aborts the Markdown count to `Cancelled` (exit 130).
    #[test]
    fn markdown_count_honours_a_token_cancelled_after_the_scan() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(tmp.path().join("a.md"), "# A\n").unwrap();
        let (session, _) = crate::session::open_and_scan(tmp.path(), &CancelToken::new()).unwrap();
        let cancel = CancelToken::new();
        cancel.cancel();

        let result = markdown_file_count(&session, &cancel);

        assert!(matches!(result, Err(CliError::Cancelled)), "{result:?}");
    }
}
