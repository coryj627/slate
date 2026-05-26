// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Shared helpers for the `session::tests::*` sub-modules.
//!
//! Each helper is `pub(super)` so the sibling sub-modules can pull
//! them in via `use super::common::*`.

#![allow(dead_code)]

use super::*;
use std::sync::atomic::AtomicU32;

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

pub(super) fn filetime_from(secs: i64, nanos: i64) -> (i64, u32) {
    (secs, nanos as u32)
}

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
