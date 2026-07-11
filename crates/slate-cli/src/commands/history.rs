// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate history <vault-path> <note-path>` (#799).
//!
//! Three modes, one verb (the O-3 version APIs through the
//! `slate.cli.v1` contract):
//!
//! - **list** (default): newest-first version rows, drained through the
//!   paging API up to `--limit`. `data`:
//!   ```json
//!   { "versions": [{ "position": u32, "hash": String,
//!                    "timestamp_ms": i64, "kind": String,
//!                    "op_count": u32, "byte_delta": i64,
//!                    "is_marker": bool, "summary": String,
//!                    "annotations": [String] }],
//!     "total": u64 }
//!   ```
//!   tsv columns: `position hash timestamp_ms kind summary`. Human: one
//!   row per version — position, ISO-ish UTC timestamp, short hash, and
//!   the audio summary fragment.
//! - **--show <hash>**: the verified bytes of one version, verbatim
//!   (the `read` discipline: no framing, no appended terminator). tsv
//!   is rejected in dispatch — a document body is not a table. json:
//!   `data: { "path", "hash", "content" }`.
//! - **--restore <hash>**: routes through `restore_version` with the
//!   expected-hash discipline — the CLI reads the CURRENT indexed hash
//!   and passes it as the compare-and-swap guard, so a racing app edit
//!   surfaces as the standard `WriteConflict` (exit 1, "the app wins")
//!   rather than a clobber. `HistoryUnavailable` (integrity) surfaces
//!   via the standard `VaultError` Display path: wrong bytes are never
//!   written. json: `data: { "path", "restored_hash",
//!   "new_content_hash" }`.

use slate_core::session::{CancelToken, Paging};

use crate::output::{CommandOutput, tsv_row};
use crate::session::{CliError, map_vault_error, open_and_scan};

/// Page size for the list drain. The CLI is allowed to page a bounded
/// listing into memory; `--limit` bounds the total.
const PAGE_SIZE: u32 = 200;

/// Which of the verb's three modes to run (clap enforces that `--show`
/// and `--restore` are mutually exclusive).
#[derive(Debug)]
pub enum HistoryMode {
    List { limit: u32 },
    Show { hash: String },
    Restore { hash: String },
}

/// Stable wire name for a version's op kind. `Annotated` never
/// surfaces here — `VersionSummary.op_kind` is the unwrapped inner
/// kind — but the arm stays truthful if that ever changes.
fn kind_name(kind: slate_core::OpKind) -> &'static str {
    match kind {
        slate_core::OpKind::WholeFileReplace => "snapshot",
        slate_core::OpKind::EditBatch => "edits",
        slate_core::OpKind::CanvasApply => "canvas",
        slate_core::OpKind::Annotated => "annotated",
    }
}

/// UTC timestamp for the human row: `YYYY-MM-DD HH:MM:SS`.
fn format_utc(ms: i64) -> String {
    let secs = ms.div_euclid(1000);
    let days = secs.div_euclid(86_400);
    let tod = secs.rem_euclid(86_400);
    // Civil-from-days (Howard Hinnant's algorithm), matching the tasks
    // verb's UTC-calendar-day convention.
    let z = days + 719_468;
    let era = z.div_euclid(146_097);
    let doe = z.rem_euclid(146_097);
    let yoe = (doe - doe / 1460 + doe / 36_524 - doe / 146_096) / 365;
    let y = yoe + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = doy - (153 * mp + 2) / 5 + 1;
    let m = if mp < 10 { mp + 3 } else { mp - 9 };
    let y = if m <= 2 { y + 1 } else { y };
    format!(
        "{:04}-{:02}-{:02} {:02}:{:02}:{:02}",
        y,
        m,
        d,
        tod / 3600,
        (tod % 3600) / 60,
        tod % 60
    )
}

/// Run `slate history`. `note_path` is vault-relative.
pub fn run(
    raw_path: &std::path::Path,
    note_path: &str,
    mode: HistoryMode,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let (session, abs_path) = open_and_scan(raw_path, cancel)?;

    // Existence check first (the m_spec §M-5 discipline): a typo'd
    // path gives "no such note", not an empty history or a
    // lower-level error.
    if session
        .get_file_metadata(note_path)
        .map_err(map_vault_error)?
        .is_none()
    {
        return Err(CliError::NoSuchNote {
            path: note_path.to_string(),
        });
    }

    match mode {
        HistoryMode::List { limit } => list(&session, &abs_path, note_path, limit, cancel),
        HistoryMode::Show { hash } => show(&session, &abs_path, note_path, &hash),
        HistoryMode::Restore { hash } => restore(&session, &abs_path, note_path, &hash),
    }
}

fn list(
    session: &slate_core::VaultSession,
    abs_path: &str,
    note_path: &str,
    limit: u32,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let mut versions = Vec::new();
    let mut cursor: Option<String> = None;
    let mut total: u64 = 0;
    loop {
        // Ctrl-C between pages: exit 130 like every other long-running
        // verb, instead of grinding through a large history.
        if cancel.is_cancelled() {
            return Err(CliError::Cancelled);
        }
        let remaining = limit.saturating_sub(versions.len() as u32);
        if remaining == 0 {
            break;
        }
        let page = session
            .list_versions(
                note_path,
                Paging {
                    cursor: cursor.clone(),
                    limit: remaining.min(PAGE_SIZE),
                },
            )
            .map_err(map_vault_error)?;
        total = page.total_filtered;
        versions.extend(page.items);
        cursor = page.next_cursor;
        if cursor.is_none() {
            break;
        }
    }

    let json_rows: Vec<serde_json::Value> = versions
        .iter()
        .map(|v| {
            serde_json::json!({
                "position": v.position_from_tail,
                "hash": v.content_hash_after,
                "timestamp_ms": v.timestamp_ms,
                "kind": kind_name(v.op_kind),
                "op_count": v.op_count,
                "byte_delta": v.byte_delta,
                "is_marker": v.is_marker,
                "summary": v.audio_fragment,
                "annotations": v.annotations.iter().map(|a| a.display.clone()).collect::<Vec<_>>(),
            })
        })
        .collect();
    let data = serde_json::json!({ "versions": json_rows, "total": total });

    let mut tsv_rows = vec![tsv_row([
        "position",
        "hash",
        "timestamp_ms",
        "kind",
        "summary",
    ])];
    let mut human_rows = Vec::new();
    for v in &versions {
        tsv_rows.push(tsv_row([
            v.position_from_tail.to_string(),
            v.content_hash_after.clone(),
            v.timestamp_ms.to_string(),
            kind_name(v.op_kind).to_string(),
            v.audio_fragment.clone(),
        ]));
        let marker = if v.is_marker { "  [marker]" } else { "" };
        human_rows.push(format!(
            "{:>4}  {}  {}  {}{}",
            v.position_from_tail,
            format_utc(v.timestamp_ms),
            &v.content_hash_after[..12.min(v.content_hash_after.len())],
            v.audio_fragment,
            marker,
        ));
    }
    if versions.is_empty() {
        human_rows.push("No versions recorded.".to_string());
    }
    let human = human_rows.join("\n");
    let tsv = tsv_rows.join("\n");

    Ok((
        abs_path.to_string(),
        CommandOutput {
            data,
            human,
            tsv,
            human_verbatim: false,
        },
    ))
}

fn show(
    session: &slate_core::VaultSession,
    abs_path: &str,
    note_path: &str,
    hash: &str,
) -> Result<(String, CommandOutput), CliError> {
    // Integrity-verified: wrong bytes are never served (O-3). Unknown
    // hashes / failed verification surface via the standard Display
    // path (exit 1).
    let content = session
        .version_content(note_path, hash)
        .map_err(map_vault_error)?;
    let data = serde_json::json!({
        "path": note_path,
        "hash": hash,
        "content": content,
    });
    Ok((
        abs_path.to_string(),
        CommandOutput {
            data,
            human: content,
            tsv: String::new(), // rejected in dispatch
            human_verbatim: true,
        },
    ))
}

fn restore(
    session: &slate_core::VaultSession,
    abs_path: &str,
    note_path: &str,
    hash: &str,
) -> Result<(String, CommandOutput), CliError> {
    // The expected-hash discipline: observe the CURRENT indexed hash
    // and pass it as the compare-and-swap guard. A racing writer
    // between the observation and the restore surfaces as the
    // standard WriteConflict — never a clobber.
    let current = session
        .get_file_metadata(note_path)
        .map_err(map_vault_error)?
        .map(|m| m.content_hash)
        .ok_or_else(|| CliError::NoSuchNote {
            path: note_path.to_string(),
        })?;
    let report = session
        .restore_version(note_path, hash, Some(&current))
        .map_err(|e| match e {
            slate_core::VaultError::WriteConflict { .. } => CliError::WriteConflict {
                path: note_path.to_string(),
            },
            other => map_vault_error(other),
        })?;

    let short: String = hash.chars().take(12).collect();
    let data = serde_json::json!({
        "path": note_path,
        "restored_hash": hash,
        "new_content_hash": report.new_content_hash,
    });
    Ok((
        abs_path.to_string(),
        CommandOutput {
            data,
            human: format!("Restored {note_path} to version {short}."),
            tsv: tsv_row(["path", "restored_hash", "new_content_hash"])
                + "\n"
                + &tsv_row([note_path, hash, report.new_content_hash.as_str()]),
            human_verbatim: false,
        },
    ))
}
