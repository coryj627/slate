// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Shared helpers for the `session::tests::*` sub-modules.
//!
//! Each helper is `pub(super)` so the sibling sub-modules can pull
//! them in via `use super::common::*`.

#![allow(dead_code)]

use super::*;
use std::sync::atomic::AtomicU32;

/// Serializes tests that mutate the process-global fault-seam env
/// vars (`SLATE_TEST_FAULT_AFTER_WRITE` and friends): two parallel
/// tests setting different trigger values overwrite each other and
/// the loser's fault never fires. Taken only through [`fault_guard`].
///
/// The guard does not isolate the many tests that never take it, and
/// every session in the process matches a trigger as a path substring.
/// Give each faulted fixture paths that no other test uses and set the
/// trigger to one of them; a generic name such as `b.md` also fails
/// unrelated batch moves running alongside the faulted open.
static FAULT_MUTEX: std::sync::Mutex<()> = std::sync::Mutex::new(());

/// Every fault-seam variable a guarded fact may set (#1481). Private, so
/// the guard's own facts write the list out rather than read it here.
const FAULT_VARIABLES: [&str; 15] = [
    "SLATE_TEST_FAULT_AFTER_WRITE",
    "SLATE_TEST_FAULT_PRE_WRITE",
    "SLATE_TEST_FAULT_REINDEX",
    "SLATE_TEST_FAULT_REINDEX_DB",
    "SLATE_TEST_FAULT_INTENT_CLEAR",
    "SLATE_TEST_FAULT_PLANT_MARKERS",
    "SLATE_TEST_FAULT_RECOVERY_FINALIZE",
    "SLATE_TEST_INTENT_ABANDON_ZERO_FOR",
    "SLATE_TEST_COMMIT_AFTER_INTENT",
    "SLATE_TEST_SWEEP_AFTER_INTENT",
    "SLATE_TEST_RESOLVE_BEFORE_QUARANTINE",
    "SLATE_TEST_SECOND_WRITER_BEFORE_QUARANTINE",
    "SLATE_TEST_STALL_ORPHAN_REPAIR",
    "SLATE_TEST_TORN_FILE_TASKS",
    "SLATE_TEST_TORN_NOTE_READ",
];

/// The one place the fault mutex is taken: a holder that panicked poisoned
/// it, and that one real failure must not fail every guarded fact after it
/// (#1481).
fn lock_recovering() -> std::sync::MutexGuard<'static, ()> {
    FAULT_MUTEX
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
}

/// The fault mutex, held, with the fault-seam variables its holder sets.
/// Taking it clears every [`FAULT_VARIABLES`] name a holder that unwound
/// may have left behind; dropping it — on return or while unwinding —
/// removes every name it set, while the lock is still held.
pub(super) struct FaultGuard {
    _lock: std::sync::MutexGuard<'static, ()>,
    set: Vec<&'static str>,
}

impl FaultGuard {
    /// Wraps a held lock, first clearing every fault-seam variable.
    pub(super) fn clearing(lock: std::sync::MutexGuard<'static, ()>) -> FaultGuard {
        for name in FAULT_VARIABLES {
            // SAFETY: the fault mutex is held; every fact that writes a
            // fault-seam variable does so under it, as before.
            unsafe { std::env::remove_var(name) };
        }
        FaultGuard {
            _lock: lock,
            set: Vec::new(),
        }
    }

    /// Sets a fault-seam variable for the length of the guard.
    pub(super) fn set(&mut self, name: &'static str, value: &str) {
        if !self.set.contains(&name) {
            self.set.push(name);
        }
        // SAFETY: the fault mutex is held (see `clearing`).
        unsafe { std::env::set_var(name, value) };
    }

    /// Clears a variable mid-body, to prove its fault is off.
    pub(super) fn clear(&mut self, name: &'static str) {
        // SAFETY: the fault mutex is held (see `clearing`).
        unsafe { std::env::remove_var(name) };
    }
}

impl Drop for FaultGuard {
    fn drop(&mut self) {
        for name in &self.set {
            // SAFETY: `drop` runs before the lock field drops, so the
            // fault mutex is still held, on return and while unwinding.
            unsafe { std::env::remove_var(name) };
        }
    }
}

/// Takes the fault guard: the mutex, recovered if a holder panicked, with
/// every fault-seam variable cleared.
pub(super) fn fault_guard() -> FaultGuard {
    FaultGuard::clearing(lock_recovering())
}

/// The guard's own facts only: the lock, without clearing anything.
pub(super) fn lock_fault_mutex_without_clearing() -> std::sync::MutexGuard<'static, ()> {
    lock_recovering()
}

/// The guard's own facts only: whether a holder's panic poisoned the lock.
pub(super) fn fault_mutex_is_poisoned() -> bool {
    FAULT_MUTEX.is_poisoned()
}

pub(super) fn make_vault(
    setup: impl FnOnce(&FsVaultProvider),
) -> (tempfile::TempDir, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    setup(&provider);
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    (tmp, session)
}

pub(super) struct CancellingProvider {
    inner: FsVaultProvider,
    cancel: CancelToken,
    list_dir_calls: AtomicU32,
    cancel_after_list_dirs: u32,
}

impl CancellingProvider {
    pub(super) fn new(inner: FsVaultProvider, cancel: CancelToken, cancel_after: u32) -> Self {
        Self {
            inner,
            cancel,
            list_dir_calls: AtomicU32::new(0),
            cancel_after_list_dirs: cancel_after,
        }
    }
}

impl crate::VaultProvider for CancellingProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        let result = self.inner.list_dir(relative);
        let n = self
            .list_dir_calls
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst)
            + 1;
        if n == self.cancel_after_list_dirs {
            self.cancel.cancel();
        }
        result
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(relative)
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }
    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }
    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        self.inner.stat(relative)
    }
    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

pub(super) struct ZeroCtimeProvider {
    pub(super) inner: FsVaultProvider,
}

impl crate::VaultProvider for ZeroCtimeProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(relative)
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }
    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }
    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        let mut stat = self.inner.stat(relative)?;
        stat.ctime_ms = 0;
        Ok(stat)
    }
    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

pub(super) fn rewrite_until_mtime_advances(
    provider: &FsVaultProvider,
    relative: &str,
    content: &[u8],
    original: i64,
) {
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);
    loop {
        provider.write_file(relative, content).unwrap();
        let now = provider.stat(relative).unwrap().mtime_ms;
        if now != original {
            return;
        }
        if std::time::Instant::now() >= deadline {
            panic!(
                "mtime did not advance past {original} within 5 s — \
                 filesystem mtime resolution too coarse for this test"
            );
        }
        std::thread::sleep(std::time::Duration::from_millis(50));
    }
}

#[cfg(unix)]
pub(super) fn filetime_from(secs: i64, nanos: i64) -> (i64, u32) {
    (secs, nanos as u32)
}

#[cfg(unix)]
pub(super) fn set_atime_mtime(path: &std::path::Path, atime: (i64, u32), mtime: (i64, u32)) {
    // Use the libc `utimensat` syscall directly so we don't pull
    // in a `filetime` dev-dependency just for one Unix-only test.
    use std::ffi::CString;
    let cpath = CString::new(path.as_os_str().as_encoded_bytes()).unwrap();
    let times = [
        libc::timespec {
            tv_sec: atime.0 as libc::time_t,
            tv_nsec: atime.1 as libc::c_long,
        },
        libc::timespec {
            tv_sec: mtime.0 as libc::time_t,
            tv_nsec: mtime.1 as libc::c_long,
        },
    ];
    // SAFETY: cpath is a NUL-terminated path; times is a fixed
    // two-element array as the API requires; flags=0 means "follow
    // symlinks", consistent with the rest of FsVaultProvider.
    let rc = unsafe { libc::utimensat(libc::AT_FDCWD, cpath.as_ptr(), times.as_ptr(), 0) };
    assert_eq!(
        rc,
        0,
        "utimensat failed: {}",
        std::io::Error::last_os_error()
    );
}

pub(super) fn fts_match_count(session: &VaultSession, term: &str) -> i64 {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT COUNT(*) FROM files_fts WHERE files_fts MATCH ?1",
        rusqlite::params![term],
        |row| row.get::<_, i64>(0),
    )
    .unwrap()
}
