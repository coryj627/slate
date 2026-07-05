// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The open-vault helper for the `slate` CLI (M-4, #535).
//!
//! Centralizes the two obligations every session-opening command shares
//! (m_spec §M-4 "Session opening"):
//!
//! 1. Validate the vault path is a directory **before** opening, so a
//!    typo exits 1 with `not a vault directory: <path>` rather than
//!    letting anything materialize a fresh empty vault on disk.
//! 2. Record whether `.slate/cache.sqlite` existed *before* this run,
//!    so `open` can report `cache: "cold"` (index built fresh) vs
//!    `"warm"` (index reused) truthfully — the empty-vault and
//!    no-changes cases both read correctly.
//!
//! SQLite contention with a concurrently-open Slate app needs no new
//! plumbing: rusqlite applies a 5-second `busy_timeout` on every
//! connection open. The CLI's only obligation is mapping a
//! *post-timeout* `SQLITE_BUSY`/locked error to a friendly message
//! ([`is_busy_error`]).

use std::path::{Path, PathBuf};

use slate_core::VaultError;
use slate_core::db::DbError;
use slate_core::session::VaultSession;

/// A validated, canonicalized vault handle plus the pre-open cache
/// state. Produced by [`open_vault`]; consumed by the `open` command.
pub struct OpenedVault {
    /// The live session.
    pub session: VaultSession,
    /// Absolute vault path, for the json envelope's `vault` field.
    pub abs_path: String,
    /// `false` when `.slate/cache.sqlite` did NOT exist before this run
    /// (the `open` command reports `cache: "cold"`); `true` otherwise
    /// (`"warm"`).
    pub cache_was_warm: bool,
}

/// The CLI's runtime-failure type. Every fallible command path returns
/// this; `main` maps it to the exit code + `slate: `-prefixed stderr
/// message contract (m_spec §M-4 exit codes).
#[derive(Debug)]
pub enum CliError {
    /// The vault path is not an existing directory (exit 1).
    NotAVaultDirectory { path: String },
    /// The `.slate` cache is locked by another process after the
    /// built-in 5s busy_timeout elapsed (exit 1, friendly retry copy).
    CacheBusy,
    /// The operation was cancelled by Ctrl-C (exit 130).
    Cancelled,
    /// Any other `VaultError` — surfaced with its `Display` text (exit
    /// 1). Every command's message is informative because `VaultError`
    /// already carries a specific message per variant.
    Vault(VaultError),
    /// A stdout write failure (e.g. broken pipe) while emitting output
    /// (exit 1).
    Io(std::io::Error),
}

impl std::fmt::Display for CliError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            CliError::NotAVaultDirectory { path } => {
                write!(f, "not a vault directory: {path}")
            }
            CliError::CacheBusy => write!(
                f,
                "vault cache is busy (is Slate open?) — retry in a moment"
            ),
            CliError::Cancelled => write!(f, "cancelled"),
            CliError::Vault(e) => write!(f, "{e}"),
            CliError::Io(e) => write!(f, "{e}"),
        }
    }
}

impl std::error::Error for CliError {}

impl From<std::io::Error> for CliError {
    fn from(e: std::io::Error) -> Self {
        CliError::Io(e)
    }
}

/// Map a `VaultError` into the CLI error space, splitting out the two
/// variants that get special exit-code / message handling
/// (`Cancelled` → 130; a post-timeout busy `Db` error → the friendly
/// retry copy) from the informative-Display fallback.
pub fn map_vault_error(e: VaultError) -> CliError {
    match e {
        VaultError::Cancelled => CliError::Cancelled,
        VaultError::Db(ref db) if is_busy_error(db) => CliError::CacheBusy,
        other => CliError::Vault(other),
    }
}

/// True when `db` is a `SQLITE_BUSY`/`SQLITE_LOCKED` failure surfaced
/// *after* rusqlite's built-in 5-second `busy_timeout` gave up — the
/// "another process (probably the Slate app) holds the cache" case.
///
/// Matches the typed `rusqlite::ErrorCode` rather than sniffing the
/// Display string, so a message-format change upstream can't silently
/// break the mapping.
pub fn is_busy_error(db: &DbError) -> bool {
    match db {
        DbError::Sqlite(rusqlite::Error::SqliteFailure(ffi_err, _)) => matches!(
            ffi_err.code,
            rusqlite::ErrorCode::DatabaseBusy | rusqlite::ErrorCode::DatabaseLocked
        ),
        _ => false,
    }
}

/// Validate `raw_path` is a directory and open a filesystem-rooted
/// session on it, capturing the pre-open cache state.
///
/// Does **not** scan — callers run `scan_initial[_with_progress]`
/// themselves so they can wire the shared `CancelToken` and progress
/// listener. Returns [`CliError::NotAVaultDirectory`] before touching
/// `VaultSession::from_filesystem`, so a typo can never
/// `create_dir_all` a fresh vault into existence.
pub fn open_vault(raw_path: &Path) -> Result<OpenedVault, CliError> {
    if !raw_path.is_dir() {
        return Err(CliError::NotAVaultDirectory {
            path: raw_path.display().to_string(),
        });
    }

    let abs_path = abs_display(raw_path);

    // Cold/warm is decided by whether the cache DB existed BEFORE we
    // open (which itself creates it). Check first (m_spec §M-4:
    // `cache` = "cold" iff `.slate/cache.sqlite` did not exist).
    let cache_was_warm = raw_path.join(".slate").join("cache.sqlite").exists();

    let session = VaultSession::from_filesystem(raw_path.to_path_buf()).map_err(map_vault_error)?;

    Ok(OpenedVault {
        session,
        abs_path,
        cache_was_warm,
    })
}

/// Best-effort absolute path for the `vault` envelope field.
/// `canonicalize` resolves symlinks and `.`/`..`; on failure (path
/// vanished mid-run, permission), fall back to the raw display string
/// — the field is informational, never load-bearing.
pub fn abs_display(path: &Path) -> String {
    canonicalize_best_effort(path).display().to_string()
}

fn canonicalize_best_effort(path: &Path) -> PathBuf {
    std::fs::canonicalize(path).unwrap_or_else(|_| path.to_path_buf())
}
