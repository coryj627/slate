// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Cooperative waits used only before a Trash provider call. Once physical
//! mutation starts, ordinary waits finish its bookkeeping and recovery.

use super::{CancelToken, VaultError, VaultStructuralLock, db, process_structural_locks};
use std::path::Path;
use std::sync::{Mutex, MutexGuard, TryLockError};
use std::time::{Duration, Instant};

const WAIT_SLICE: Duration = Duration::from_millis(20);

pub(super) fn lock_cancellable<'a, T>(
    mutex: &'a Mutex<T>,
    cancel: &CancelToken,
) -> Result<MutexGuard<'a, T>, VaultError> {
    loop {
        cancel.check()?;
        match mutex.try_lock() {
            Ok(guard) => {
                cancel.check()?;
                return Ok(guard);
            }
            Err(TryLockError::WouldBlock) => std::thread::sleep(WAIT_SLICE),
            Err(TryLockError::Poisoned(_)) => {
                return Err(VaultError::InvalidArgument {
                    message:
                        "an interrupted structural operation requires close-and-reopen recovery"
                            .into(),
                });
            }
        }
    }
}

impl VaultStructuralLock {
    pub(super) fn acquire_cancellable(
        cache_dir: &Path,
        cancel: &CancelToken,
    ) -> Result<Self, VaultError> {
        cancel.check()?;
        let lock_path = std::fs::canonicalize(cache_dir)?.join("structural.lock");
        let registry = process_structural_locks();
        let mut held = lock_cancellable(&registry.held, cancel)?;
        while held.contains(&lock_path) {
            cancel.check()?;
            held = registry
                .available
                .wait_timeout(held, WAIT_SLICE)
                .unwrap_or_else(std::sync::PoisonError::into_inner)
                .0;
        }
        cancel.check()?;
        held.insert(lock_path.clone());
        drop(held);

        // Own the registry slot before fallible work, so cancellation and real
        // I/O errors both release it through the existing Drop implementation.
        let mut guard = Self {
            file: None,
            lock_path,
        };
        cancel.check()?;
        let file = std::fs::OpenOptions::new()
            .create(true)
            .truncate(false)
            .read(true)
            .write(true)
            .open(&guard.lock_path)?;
        loop {
            cancel.check()?;
            match file.try_lock() {
                Ok(()) => {
                    guard.file = Some(file);
                    cancel.check()?;
                    return Ok(guard);
                }
                Err(std::fs::TryLockError::WouldBlock) => std::thread::sleep(WAIT_SLICE),
                Err(std::fs::TryLockError::Error(error)) => return Err(error.into()),
            }
        }
    }
}

pub(super) fn begin_fenced_cancellable<'a>(
    conn: &'a rusqlite::Connection,
    cancel: &CancelToken,
) -> Result<rusqlite::Transaction<'a>, VaultError> {
    cancel.check()?;
    let timeout_ms: u32 = conn.query_row("PRAGMA busy_timeout", [], |row| row.get(0))?;
    struct RestoreTimeout<'a>(&'a rusqlite::Connection, Duration);
    impl Drop for RestoreTimeout<'_> {
        fn drop(&mut self) {
            let _ = self.0.busy_timeout(self.1);
        }
    }
    let timeout = Duration::from_millis(u64::from(timeout_ms));
    let _restore = RestoreTimeout(conn, timeout);
    conn.busy_timeout(Duration::ZERO)?;
    let started = Instant::now();
    loop {
        cancel.check()?;
        match db::begin_fenced(conn) {
            Ok(tx) => {
                cancel.check()?;
                return Ok(tx);
            }
            Err(db::DbError::Sqlite(rusqlite::Error::SqliteFailure(error, _)))
                if matches!(
                    error.code,
                    rusqlite::ErrorCode::DatabaseBusy | rusqlite::ErrorCode::DatabaseLocked
                ) && started.elapsed() < timeout =>
            {
                std::thread::sleep(WAIT_SLICE.min(timeout.saturating_sub(started.elapsed())));
            }
            Err(error) => return Err(error.into()),
        }
    }
}

/// A provider can report cancellation after dispatching a platform operation.
/// It is then an uncertain provider failure, never proof that no bytes moved.
pub(super) fn after_dispatch_error(error: VaultError) -> VaultError {
    if matches!(error, VaultError::Cancelled) {
        VaultError::Trash {
            message: "The system Trash operation reported cancellation after it started; its physical outcome is unconfirmed. Refresh the vault to reconcile".into(),
        }
    } else {
        error
    }
}
