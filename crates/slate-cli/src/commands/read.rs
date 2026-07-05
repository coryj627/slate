// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate read <vault-path> <note-path>` (M-5, #536).
//!
//! Existence check first, then verbatim content (m_spec §M-5):
//!   - `get_file_metadata(path)` → `None` → exit 1 `no such note: <path>`.
//!     The check runs before the read so a typo'd path gives the "no
//!     such note" message rather than a lower-level read error.
//!   - Then `read_text(path)` to stdout **verbatim** (human format).
//!   - json: `data: { "path": String, "content": String }`.
//!   - tsv: unsupported → exit 2 (a document body is not a table; same
//!     rule as `render-template`). Enforced in `main`'s dispatch, which
//!     rejects `--format tsv` for `read` before we run.
//!   - `FileTooLarge` surfaces as exit 1 with the session's message (the
//!     standard `VaultError` Display path).

use slate_core::session::CancelToken;

use crate::output::CommandOutput;
use crate::session::{CliError, map_vault_error, open_and_scan};

/// Run `slate read`. `note_path` is vault-relative (the path as indexed).
///
/// Returns the rendered output on success. tsv rejection is handled by
/// the caller (`main`) — this function only produces the json + human
/// bodies; the tsv body is left empty and is never emitted because the
/// dispatch layer exits 2 first.
pub fn run(
    raw_path: &std::path::Path,
    note_path: &str,
    cancel: &CancelToken,
) -> Result<(String, CommandOutput), CliError> {
    let (session, abs_path) = open_and_scan(raw_path, cancel)?;

    // Existence check via the index. `None` means the scanner never
    // saw this path — a typo, or a file outside the vault. Exit 1 with
    // the pinned message so the user knows it's the path, not the read.
    if session
        .get_file_metadata(note_path)
        .map_err(map_vault_error)?
        .is_none()
    {
        return Err(CliError::NoSuchNote {
            path: note_path.to_string(),
        });
    }

    // Verbatim read. `FileTooLarge` / `InvalidUtf8` surface via the
    // standard Display path (exit 1, informative message).
    let content = session.read_text(note_path).map_err(map_vault_error)?;

    let data = serde_json::json!({
        "path": note_path,
        "content": content,
    });
    // Human format is the content byte-for-byte — no framing and NO
    // appended terminator (`human_verbatim`). The §M-5 contract is
    // "`read_text` to stdout verbatim": a note without a trailing
    // newline must not gain one, and a note with one must not gain a
    // blank line. Fidelity matters for piping to `diff`, hashing, and
    // copy/paste (codex adversarial finding, round 1).
    let human = content;
    // tsv is never emitted for `read` (dispatch exits 2 first), so the
    // body is empty by contract.
    let tsv = String::new();

    Ok((
        abs_path,
        CommandOutput {
            data,
            human,
            tsv,
            human_verbatim: true,
        },
    ))
}
