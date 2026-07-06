// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate write <vault-path> <note-path> [--expect-hash <blake3>] [--create]`
//! (#641).
//!
//! The one CLI verb that mutates vault *content* (every other verb only
//! writes the `.slate/` cache). Content comes from **stdin**, read to
//! EOF; there is no `--content` flag in v1. The write is a thin wrapper
//! over [`VaultSession::save_text`] — no bespoke write plumbing lives
//! here; the compare-and-swap discipline the Mac editor uses is reused
//! verbatim (m_spec §M-4 "no business logic in the CLI layer", #641
//! "do NOT invent parallel write plumbing").
//!
//! # The concurrent-writer story ("the app wins")
//!
//! A vault can be open in the Slate app and the `slate` CLI at the same
//! time. Both writers go through `save_text`'s `expected_content_hash`
//! compare-and-swap:
//!
//! - **CLI-side.** For an existing note, `write` observes the note's
//!   current indexed content hash (via `get_file_metadata`, the same
//!   observation the app uses) and passes it as `expected_content_hash`.
//!   `save_text` then re-reads the file *on disk* and hashes it fresh
//!   inside a cross-process critical section: an IMMEDIATE transaction
//!   on the shared `.slate/cache.sqlite`, whose one-writer lock is
//!   file-based and therefore excludes the app's process too, held from
//!   before the rehash through the atomic rename + index commit. If the
//!   disk hash doesn't match what we observed, `save_text` returns
//!   `WriteConflict` and leaves the file untouched. So even though our
//!   observation is a plain index read (a classic TOCTOU shape), the
//!   race is **benign**: the authoritative rehash and the write are one
//!   atomic unit — a racing writer either lands wholly before it (we
//!   see its hash and refuse) or blocks on the lock until we're done
//!   (it sees ours and refuses). Never a silent clobber in either
//!   order. `--expect-hash` lets a script pin the anchor explicitly,
//!   turning `write` into an idempotent compare-and-swap across separate
//!   invocations.
//!
//! - **App-side.** A running app won't see the CLI's write in an already-
//!   open buffer until it reloads. When the app then saves, *its* own
//!   `expected_content_hash` (captured when it opened the note) no longer
//!   matches the disk hash the CLI produced, so the app's save conflict-
//!   detects and prompts the user. Neither writer can silently overwrite
//!   the other.
//!
//! # `data` shape (the `slate.cli.v1` stability contract, additive)
//!
//! ```json
//! { "path": String, "bytes_written": u64, "content_hash": String }
//! ```
//!
//! `bytes_written` and `content_hash` are the real post-save state from
//! [`SaveReport`] (`new_size_bytes`, `new_content_hash`).

use std::io::Read;

use slate_core::VaultError;
use slate_core::session::{CancelToken, SaveReport};

use crate::output::{CommandOutput, tsv_row};
use crate::session::{CliError, map_vault_error, open_and_scan};

/// Run `slate write`, reading the note body from the process's stdin.
///
/// `note_path` is vault-relative (the path as indexed). `expect_hash` is
/// the user's `--expect-hash` (a blake3 hex hash, used verbatim as the
/// compare-and-swap anchor when present). `create` is `--create`.
pub fn run(
    raw_path: &std::path::Path,
    note_path: &str,
    expect_hash: Option<&str>,
    create: bool,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    // Read stdin up front, before opening the vault, so a too-large or
    // non-UTF-8 payload is rejected without ever touching the index or
    // the file on disk.
    let stdin = std::io::stdin();
    let contents = read_capped_utf8(stdin.lock(), STDIN_REFUSE_BYTES)?;
    run_with_contents(raw_path, note_path, &contents, expect_hash, create, cancel)
}

/// The stdin refuse threshold, mirroring the session's default
/// `large_file_refuse_bytes` (50 MiB). `save_text` enforces its own copy
/// of this cap against the decoded string; reading stdin with the same
/// bound (plus one byte to detect overflow) means we never buffer an
/// unbounded pipe into memory and we fail with an informative,
/// nothing-written message before opening the vault.
pub const STDIN_REFUSE_BYTES: u64 = 50 * 1024 * 1024;

/// The write path with the note body already in hand. Split out from
/// [`run`] so tests can drive the save semantics in-process without a
/// real stdin (the stdin read + cap + UTF-8 checks are tested via
/// [`read_capped_utf8`] directly).
pub fn run_with_contents(
    raw_path: &std::path::Path,
    note_path: &str,
    contents: &str,
    expect_hash: Option<&str>,
    create: bool,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let (session, abs_path) = open_and_scan(raw_path, cancel)?;

    // Observe the note's current state through the index — the same
    // observation discipline the app uses (`get_file_metadata`).
    let existing = session
        .get_file_metadata(note_path)
        .map_err(map_vault_error)?;

    // Decide the compare-and-swap anchor (`expected_content_hash`):
    //
    // - missing note, no --create → exit 1 "no such note", UNCONDITIONALLY
    //   — even when --expect-hash was given. Core hashes a missing file
    //   to `""`, so honoring an empty --expect-hash here would mint the
    //   file; in automation an empty/failed hash-lookup variable plus a
    //   typo'd path must surface as "no such note", never a new note
    //   (codex adversarial round 2). Same stable copy `read`/`links` use.
    // - missing note, --create   → the user's --expect-hash verbatim if
    //   given, else `Some("")`: an empty expected-hash is a CAS against
    //   "no file exists". If another process creates the file first, the
    //   disk hash is non-empty and the write is refused — a safe, atomic
    //   create rather than a blind overwrite.
    // - existing note            → --expect-hash verbatim if given (the
    //   cross-invocation CAS), else the note's current indexed hash, so
    //   an app edit between our observation and the save conflict-checks.
    let expected: Option<String> = match (&existing, create, expect_hash) {
        (None, false, _) => {
            return Err(CliError::NoSuchNote {
                path: note_path.to_string(),
            });
        }
        (None, true, Some(h)) => Some(h.to_string()),
        (None, true, None) => Some(String::new()),
        (Some(_), _, Some(h)) => Some(h.to_string()),
        (Some(meta), _, None) => Some(meta.content_hash.clone()),
    };

    let report = session
        .save_text(note_path, contents, expected.as_deref())
        .map_err(|e| map_write_error(e, note_path))?;

    Ok((abs_path, build_output(note_path, &report)))
}

/// Read `reader` to EOF, refusing at `limit` bytes and rejecting non-
/// UTF-8, returning the decoded body.
///
/// Reads at most `limit + 1` bytes: if the reader yields more than
/// `limit`, we stop and return [`CliError::StdinTooLarge`] without
/// buffering the rest (so a runaway pipe can't exhaust memory). A
/// non-UTF-8 payload is rejected with [`CliError::StdinNotUtf8`] — note
/// bodies are text and `save_text` takes `&str`.
pub fn read_capped_utf8<R: Read>(reader: R, limit: u64) -> Result<String, CliError> {
    // Cap the source at limit+1 bytes; if the +1 byte materializes, the
    // input is over the limit.
    let mut capped = reader.take(limit + 1);
    let mut buf = Vec::new();
    capped.read_to_end(&mut buf).map_err(CliError::Io)?;
    if buf.len() as u64 > limit {
        return Err(CliError::StdinTooLarge { limit });
    }
    String::from_utf8(buf).map_err(|_| CliError::StdinNotUtf8)
}

/// Map a `save_text` failure into the CLI error space. The one variant
/// that needs the note path — `WriteConflict` — is folded into the
/// scriptable "the app wins" message here; everything else falls through
/// to the shared [`map_vault_error`] (busy-cache, cancelled, informative
/// `Display`).
fn map_write_error(e: VaultError, note_path: &str) -> CliError {
    match e {
        VaultError::WriteConflict { .. } => CliError::WriteConflict {
            path: note_path.to_string(),
        },
        other => map_vault_error(other),
    }
}

/// Build the format-agnostic output from the post-save report.
///
/// json `data` is the additive `slate.cli.v1` shape; human is one
/// confirmation line; tsv is `field<TAB>value` rows (same shape as
/// `open`).
fn build_output(note_path: &str, report: &SaveReport) -> CommandOutput {
    let data = serde_json::json!({
        "path": note_path,
        "bytes_written": report.new_size_bytes,
        "content_hash": report.new_content_hash,
    });

    let human = format!(
        "Wrote {} ({} bytes, hash {}).",
        note_path, report.new_size_bytes, report.new_content_hash
    );

    let tsv = [
        tsv_row(["field", "value"]),
        tsv_row(["path", note_path]),
        tsv_row(["bytes_written", &report.new_size_bytes.to_string()]),
        tsv_row(["content_hash", &report.new_content_hash]),
    ]
    .join("\n");

    CommandOutput {
        data,
        human,
        tsv,
        human_verbatim: false,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn read_capped_utf8_accepts_exactly_at_limit() {
        let body = "abcde"; // 5 bytes
        let got = read_capped_utf8(body.as_bytes(), 5).expect("at-limit input accepted");
        assert_eq!(got, body);
    }

    #[test]
    fn read_capped_utf8_refuses_over_limit() {
        let body = "abcdef"; // 6 bytes, limit 5
        let err = read_capped_utf8(body.as_bytes(), 5).expect_err("over-limit refused");
        assert!(matches!(err, CliError::StdinTooLarge { limit: 5 }));
    }

    #[test]
    fn read_capped_utf8_rejects_non_utf8() {
        // 0xFF is never valid UTF-8.
        let bytes: &[u8] = &[0x68, 0x69, 0xFF];
        let err = read_capped_utf8(bytes, 1024).expect_err("non-utf8 refused");
        assert!(matches!(err, CliError::StdinNotUtf8));
    }

    #[test]
    fn read_capped_utf8_preserves_bytes_verbatim() {
        // No trailing-newline munging: the body is passed through exactly.
        let body = "line one\nline two"; // no trailing \n
        let got = read_capped_utf8(body.as_bytes(), 1024).unwrap();
        assert_eq!(got, body);
    }
}
