// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate properties <vault-path> [--key <key>]` (M-5, #536).
//!
//! Two modes (m_spec §M-5):
//!   - Without `--key`: `list_property_keys` → `data: { "keys": [{ "key",
//!     "file_count" }] }`. Human prints `key<TAB>count` lines (a
//!     two-column list even in human — it's inherently tabular).
//!   - With `--key`: `files_with_property_key` drained → `data: { "key",
//!     "files": [String] }`. Human prints one path per line. A key that
//!     matches no files yields an empty list, exit 0 — absence is not an
//!     error.

use slate_core::PropertyKeySummary;
use slate_core::session::{CancelToken, Paging};

use crate::output::{CommandOutput, tsv_row};
use crate::session::{CliError, map_vault_error, open_and_scan};

/// Page size for the `--key` drain loop.
const PAGE_SIZE: u32 = 1000;

/// Run `slate properties`. `key = Some(k)` selects the "files carrying
/// key `k`" mode; `None` lists every key with its file count.
pub fn run(
    raw_path: &std::path::Path,
    key: Option<&str>,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let (session, abs_path) = open_and_scan(raw_path, cancel)?;

    match key {
        None => run_list_keys(&session, abs_path),
        Some(k) => run_files_for_key(&session, abs_path, k),
    }
}

/// No `--key`: the vault's distinct property keys + per-key file counts.
fn run_list_keys(
    session: &slate_core::session::VaultSession,
    abs_path: String,
) -> Result<(String, CommandOutput), CliError> {
    let keys = session.list_property_keys().map_err(map_vault_error)?;

    let data = serde_json::json!({
        "keys": keys.iter().map(key_json).collect::<Vec<_>>(),
    });
    let human = render_keys_human(&keys);
    let tsv = render_keys_tsv(&keys);

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

/// `--key <k>`: every file carrying property `k`, drained to exhaustion.
fn run_files_for_key(
    session: &slate_core::session::VaultSession,
    abs_path: String,
    key: &str,
) -> Result<(String, CommandOutput), CliError> {
    let mut files: Vec<String> = Vec::new();
    let mut cursor: Option<String> = None;
    loop {
        let paging = match cursor.take() {
            Some(c) => Paging::after(c, PAGE_SIZE),
            None => Paging::first(PAGE_SIZE),
        };
        let page = session
            .files_with_property_key(key, paging)
            .map_err(map_vault_error)?;
        files.extend(page.items.into_iter().map(|f| f.path));
        cursor = page.next_cursor;
        if cursor.is_none() {
            break;
        }
    }

    let data = serde_json::json!({
        "key": key,
        "files": files,
    });
    let human = files.join("\n");
    let tsv = render_files_tsv(key, &files);

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

fn key_json(k: &PropertyKeySummary) -> serde_json::Value {
    serde_json::json!({
        "key": k.key,
        "file_count": k.file_count,
    })
}

// --- human / tsv formats ---------------------------------------------

/// Human format for the key list (m_spec §M-5): `key<TAB>count` lines.
fn render_keys_human(keys: &[PropertyKeySummary]) -> String {
    keys.iter()
        .map(|k| format!("{}\t{}", k.key, k.file_count))
        .collect::<Vec<_>>()
        .join("\n")
}

/// TSV format for the key list: header `key file_count`.
fn render_keys_tsv(keys: &[PropertyKeySummary]) -> String {
    let mut rows = vec![tsv_row(["key", "file_count"])];
    for k in keys {
        rows.push(tsv_row([k.key.as_str(), &k.file_count.to_string()]));
    }
    rows.join("\n")
}

/// TSV format for the `--key` file list: header `path`, one path per
/// row. Single-column, but a header keeps it consistent with the other
/// verbs (and lets a script skip line 1 unconditionally).
fn render_files_tsv(_key: &str, files: &[String]) -> String {
    let mut rows = vec![tsv_row(["path"])];
    for f in files {
        rows.push(tsv_row([f.as_str()]));
    }
    rows.join("\n")
}
