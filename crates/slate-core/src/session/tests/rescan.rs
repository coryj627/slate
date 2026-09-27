// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! W7-7 PR 7 (#1252, contract R-9, AR-18's fallback): the rescan of an
//! open session. A rescan is the incremental scan; it retains nothing —
//! the host re-synchronizes its surfaces from the index afterwards. These
//! facts pin what the scan itself owes: its hash-authoritative counts,
//! its completeness and the prunes that depend on it, its bounded error
//! report, and the index lookups the host's re-sync reads.

use std::sync::{Arc, Mutex};

use super::common::*;
use super::*;

fn rescan(session: &VaultSession) -> ScanReport {
    session
        .rescan_with_progress(&CancelToken::new(), None)
        .expect("rescan")
}

fn indexed_hash(session: &VaultSession, path: &str) -> Option<String> {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT content_hash FROM files WHERE path = ?1",
        rusqlite::params![path],
        |row| row.get(0),
    )
    .optional()
    .unwrap()
}

fn dir_rows(session: &VaultSession) -> Vec<String> {
    let conn = session.conn.lock().unwrap();
    let mut stmt = conn.prepare("SELECT path FROM dirs ORDER BY path").unwrap();
    stmt.query_map([], |row| row.get(0))
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap()
}

/// Same length, different bytes, and a strictly newer mtime — the edit
/// the short-circuit must NOT hide.
fn edit_same_size(tmp: &tempfile::TempDir, path: &str, bytes: &[u8]) {
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let before = provider.stat(path).unwrap().mtime_ms;
    rewrite_until_mtime_advances(&provider, path, bytes, before);
}

// --- (a)–(d): what a rescan finds, counted as it writes -------------------------

#[test]
fn a_rescan_reports_a_file_created_outside_slate_as_new() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"# Keep\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::write(tmp.path().join("late.md"), b"# Late\n").unwrap();
    let report = rescan(&session);

    assert_eq!(report.files_seen, 2);
    assert_eq!((report.files_changed, report.files_removed), (1, 0));
    assert!(report.complete, "{:?}", report.error_samples);
    assert!(session.get_file_metadata("late.md").unwrap().is_some());
}

#[test]
fn a_rescan_reports_changed_bytes_as_modified() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"alpha\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    edit_same_size(&tmp, "note.md", b"omega\n");
    let report = rescan(&session);

    assert_eq!((report.files_changed, report.files_indexed), (1, 1));
    assert_eq!(
        indexed_hash(&session, "note.md").unwrap(),
        crate::content_hash(b"omega\n")
    );
}

/// `ATouchedUnchangedFileIsNotModified`: a re-read of the same bytes
/// increments `files_indexed` and is not a change (R-9: "modified" is
/// the committed hash, never the read count).
#[test]
fn a_touched_unchanged_file_is_not_modified() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"same bytes\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    edit_same_size(&tmp, "note.md", b"same bytes\n");
    let report = rescan(&session);

    assert_eq!(report.files_indexed, 1, "the slow path re-read the file");
    assert_eq!(report.files_changed, 0, "a re-read is not a change");
}

/// `ACtimeZeroProviderBehavesTheSame`: Windows reports `ctime_ms = 0`;
/// the counts never used ctime, so created / modified / touched read the
/// same through a zero-ctime provider.
#[test]
fn a_ctime_zero_provider_behaves_the_same() {
    let tmp = tempfile::tempdir().unwrap();
    let fs = FsVaultProvider::new(tmp.path().to_path_buf());
    fs.write_file("edit.md", b"alpha\n").unwrap();
    fs.write_file("touch.md", b"touched\n").unwrap();
    let provider = Arc::new(ZeroCtimeProvider {
        inner: FsVaultProvider::new(tmp.path().to_path_buf()),
    });
    let session =
        VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate"))).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::write(tmp.path().join("new.md"), b"new\n").unwrap();
    edit_same_size(&tmp, "edit.md", b"omega\n");
    edit_same_size(&tmp, "touch.md", b"touched\n");
    let report = rescan(&session);

    assert_eq!(report.files_changed, 2);
    assert_eq!(
        report.files_indexed, 3,
        "new, edited and touched were all read"
    );
}

/// AR-7, pinned as the accepted risk it is: the `(mtime, size)`
/// short-circuit stays, so a same-size edit that preserves mtime is not
/// read — and on a platform without ctime (Windows) nothing else can see
/// it. It surfaces the next time the file is read.
#[test]
fn ar7_a_same_size_edit_that_preserves_mtime_stays_invisible() {
    let tmp = tempfile::tempdir().unwrap();
    FsVaultProvider::new(tmp.path().to_path_buf())
        .write_file("note.md", b"alpha\n")
        .unwrap();
    let provider = Arc::new(ZeroCtimeProvider {
        inner: FsVaultProvider::new(tmp.path().to_path_buf()),
    });
    let session =
        VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate"))).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let path = tmp.path().join("note.md");
    let modified = std::fs::metadata(&path).unwrap().modified().unwrap();

    std::fs::write(&path, b"omega\n").unwrap();
    std::fs::File::options()
        .write(true)
        .open(&path)
        .unwrap()
        .set_modified(modified)
        .unwrap();
    let report = rescan(&session);

    assert_eq!(report.files_changed, 0, "AR-7: the short-circuit hides it");
}

#[test]
fn a_rescan_removes_the_row_of_a_file_deleted_outside_slate() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"keep\n").unwrap();
        p.write_file("gone.md", b"gone\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::remove_file(tmp.path().join("gone.md")).unwrap();
    let report = rescan(&session);

    assert_eq!((report.files_changed, report.files_removed), (0, 1));
    assert!(session.get_file_metadata("gone.md").unwrap().is_none());
}

/// AR-8: a real filesystem rename through the scan is a removal plus a
/// creation — the index has no filesystem identity to correlate them with.
#[test]
fn a_filesystem_rename_is_a_removal_plus_a_creation() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("before.md", b"body\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::rename(tmp.path().join("before.md"), tmp.path().join("after.md")).unwrap();
    let report = rescan(&session);

    assert_eq!((report.files_changed, report.files_removed), (1, 1));
    assert!(session.get_file_metadata("before.md").unwrap().is_none());
    assert!(session.get_file_metadata("after.md").unwrap().is_some());
}

/// `AUniqueSameHashDeleteCreateIsNotARename`: an external delete of A
/// and an unrelated B with identical bytes are counted as what they are.
#[test]
fn a_unique_same_hash_delete_create_is_not_a_rename() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"identical\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::remove_file(tmp.path().join("a.md")).unwrap();
    std::fs::write(tmp.path().join("b.md"), b"identical\n").unwrap();
    let report = rescan(&session);

    assert_eq!((report.files_changed, report.files_removed), (1, 1));
}

#[derive(Default)]
struct ChangeRecorder {
    file_changes: Mutex<Vec<FileChangeEvent>>,
    phases: Mutex<Vec<IndexPhase>>,
}

impl VaultEventListener for ChangeRecorder {
    fn on_error(&self, _code: EventErrorCode, _path: String, _message: String) {}
    fn on_file_change(&self, event: FileChangeEvent) {
        self.file_changes.lock().unwrap().push(event);
    }
    fn on_index_phase(&self, phase: IndexPhase, _files_seen: u64) {
        self.phases.lock().unwrap().push(phase);
    }
}

/// Locked decision 05 (`:338-342`): file-change events cover Slate's own
/// writes; external edits surface at the next scan, never as events.
#[test]
fn no_file_change_event_fires_during_any_scan() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("edit.md", b"alpha\n").unwrap();
        p.write_file("gone.md", b"gone\n").unwrap();
    });
    let recorder = Arc::new(ChangeRecorder::default());
    session.register_event_listener(recorder.clone());
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::write(tmp.path().join("new.md"), b"new\n").unwrap();
    edit_same_size(&tmp, "edit.md", b"omega\n");
    std::fs::remove_file(tmp.path().join("gone.md")).unwrap();
    let report = rescan(&session);
    assert_eq!((report.files_changed, report.files_removed), (2, 1));

    assert!(
        recorder.file_changes.lock().unwrap().is_empty(),
        "a scan emitted file-change events: {:?}",
        recorder.file_changes.lock().unwrap()
    );
    assert_eq!(
        recorder
            .phases
            .lock()
            .unwrap()
            .iter()
            .filter(|phase| **phase == IndexPhase::ScanFinished)
            .count(),
        2,
        "both scans still bracket the index lifecycle"
    );
}

// --- (h): completeness and the prunes that depend on it ------------------------

/// Fails `list_dir` for one directory, and `stat` / `read_file` for one
/// path — or reports one listed path as NotFound (AR-26).
struct FaultingProvider {
    inner: FsVaultProvider,
    list_dir_fails: Option<String>,
    stat_fails: Option<String>,
    stat_not_found: Option<String>,
    read_fails: Option<String>,
}

impl FaultingProvider {
    fn over(root: &std::path::Path) -> Self {
        Self {
            inner: FsVaultProvider::new(root.to_path_buf()),
            list_dir_fails: None,
            stat_fails: None,
            stat_not_found: None,
            read_fails: None,
        }
    }
}

fn denied(path: &str) -> VaultError {
    VaultError::Io(std::io::Error::new(
        std::io::ErrorKind::PermissionDenied,
        format!("injected fault at {path}"),
    ))
}

impl crate::VaultProvider for FaultingProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        if self.list_dir_fails.as_deref() == Some(relative) {
            return Err(denied(relative));
        }
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        if self.read_fails.as_deref() == Some(relative) {
            return Err(denied(relative));
        }
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
        if self.stat_fails.as_deref() == Some(relative) {
            return Err(denied(relative));
        }
        if self.stat_not_found.as_deref() == Some(relative) {
            return Err(VaultError::Io(std::io::Error::new(
                std::io::ErrorKind::NotFound,
                format!("injected NotFound at {relative}"),
            )));
        }
        self.inner.stat(relative)
    }
    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

fn reopen_through(tmp: &tempfile::TempDir, provider: FaultingProvider) -> VaultSession {
    VaultSession::open(
        Arc::new(provider),
        SessionConfig::new(tmp.path().join(".slate")),
    )
    .unwrap()
}

/// An incomplete walk prunes NEITHER files nor directories: a subtree
/// that is transiently unreadable must not make live nested folders
/// vanish from the refreshed tree, and a file deleted elsewhere keeps its
/// row too until a clean scan proves its absence.
#[test]
fn an_unreadable_subtree_prunes_neither_files_nor_directories() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("root.md", b"root\n").unwrap();
        p.write_file("gone.md", b"gone\n").unwrap();
        p.write_file("sub/nested.md", b"nested\n").unwrap();
        p.write_file("sub/deeper/leaf.md", b"leaf\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(dir_rows(&session), vec!["sub", "sub/deeper"]);
    drop(session);
    std::fs::remove_file(tmp.path().join("gone.md")).unwrap();

    let mut provider = FaultingProvider::over(tmp.path());
    provider.list_dir_fails = Some("sub".into());
    let session = reopen_through(&tmp, provider);
    let report = rescan(&session);

    assert!(!report.complete, "a partial walk is never complete");
    assert!(report.error_count > 0);
    assert_eq!(
        dir_rows(&session),
        vec!["sub", "sub/deeper"],
        "a folder under the unreadable subtree vanished"
    );
    for path in ["sub/nested.md", "sub/deeper/leaf.md", "gone.md"] {
        assert!(
            session.get_file_metadata(path).unwrap().is_some(),
            "a partial walk pruned {path}"
        );
    }
    assert_eq!(report.files_removed, 0);
}

/// One arm of R-9's completeness rule (finding 7): what to inject, then
/// how to heal it.
#[derive(Clone, Copy, Debug)]
enum FailureArm {
    Stat,
    Read,
    Oversize,
    Derivative,
    FileMeta,
}

/// Every per-file failure — not only a listing failure — makes the scan
/// partial, and a partial scan prunes NOTHING: a file and a folder
/// deleted outside Slate keep their rows while any failure is recorded,
/// and the next clean scan removes them.
#[test]
fn a_per_file_failure_blocks_both_prunes_until_a_clean_scan() {
    for arm in [
        FailureArm::Stat,
        FailureArm::Read,
        FailureArm::Oversize,
        FailureArm::Derivative,
        FailureArm::FileMeta,
    ] {
        let (tmp, session) = make_vault(|p| {
            p.write_file("keep.md", b"keep\n").unwrap();
            p.write_file("gone.md", b"gone\n").unwrap();
            p.write_file("old/inside.md", b"inside\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        drop(session);
        std::fs::remove_file(tmp.path().join("gone.md")).unwrap();
        std::fs::remove_dir_all(tmp.path().join("old")).unwrap();
        std::fs::write(tmp.path().join("bad.md"), b"#tagged bad\n").unwrap();

        let mut provider = FaultingProvider::over(tmp.path());
        let mut config = SessionConfig::new(tmp.path().join(".slate"));
        match arm {
            FailureArm::Stat => provider.stat_fails = Some("bad.md".into()),
            FailureArm::Read => provider.read_fails = Some("bad.md".into()),
            FailureArm::Oversize => config.large_file_refuse_bytes = 8,
            FailureArm::Derivative | FailureArm::FileMeta => {}
        }
        let session = VaultSession::open(Arc::new(provider), config).unwrap();
        {
            let conn = session.conn.lock().unwrap();
            match arm {
                FailureArm::Derivative => conn
                    .execute_batch(
                        "CREATE TEMP TRIGGER reject_tags BEFORE INSERT ON main.file_tags
                         BEGIN SELECT RAISE(ABORT, 'injected tag failure'); END;",
                    )
                    .unwrap(),
                FailureArm::FileMeta => conn
                    .execute_batch(
                        "CREATE TEMP TRIGGER reject_meta BEFORE INSERT ON main.file_meta
                         BEGIN SELECT RAISE(ABORT, 'injected file_meta failure'); END;",
                    )
                    .unwrap(),
                _ => {}
            }
        }

        let report = rescan(&session);
        assert!(
            !report.complete,
            "{arm:?}: the failure left the scan complete"
        );
        assert!(report.error_count > 0, "{arm:?}: nothing was recorded");
        assert_eq!(report.files_removed, 0, "{arm:?}");
        assert!(
            session.get_file_metadata("gone.md").unwrap().is_some(),
            "{arm:?}: a partial scan pruned a file"
        );
        assert!(
            dir_rows(&session).contains(&"old".to_string()),
            "{arm:?}: a partial scan pruned a folder"
        );
        drop(session);

        // Healed: a clean scan proves the absence and prunes both.
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        let report = rescan(&session);
        assert!(report.complete, "{arm:?}: {:?}", report.error_samples);
        assert!(report.files_removed >= 1, "{arm:?}");
        assert!(
            session.get_file_metadata("gone.md").unwrap().is_none(),
            "{arm:?}"
        );
        assert!(!dir_rows(&session).contains(&"old".to_string()), "{arm:?}");
    }
}

/// AR-26: a listed file whose stat reports NotFound keeps its row — on
/// Windows a NotFound also comes from MAX_PATH or permissions on a LIVE
/// file, and un-seeing it would delete that file's row. The failure is
/// counted; a clean scan settles the truth either way.
#[test]
fn ar26_a_not_found_stat_keeps_the_row_until_a_clean_scan() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"keep\n").unwrap();
        p.write_file("live.md", b"live\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    drop(session);
    edit_same_size(&tmp, "live.md", b"LIVE\n");

    let mut provider = FaultingProvider::over(tmp.path());
    provider.stat_not_found = Some("live.md".into());
    let session = reopen_through(&tmp, provider);
    let report = rescan(&session);

    assert!(!report.complete);
    assert_eq!(report.error_count, 1, "{:?}", report.error_samples);
    assert_eq!(report.files_removed, 0);
    assert!(
        session.get_file_metadata("live.md").unwrap().is_some(),
        "AR-26: a NotFound stat deleted a live file's row"
    );
}

/// The canvas pass must see the post-prune index (its card titles come
/// from other files' rows), so it follows the prune: its failure makes
/// the scan incomplete but cannot un-prune what the complete walk proved
/// gone.
#[test]
fn a_canvas_index_failure_makes_the_scan_incomplete_after_the_prune() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("gone.md", b"gone\n").unwrap();
        p.write_file(
            "board.canvas",
            br#"{"nodes":[{"id":"a","type":"text","text":"A","x":0,"y":0,"width":10,"height":10}],"edges":[]}"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    std::fs::remove_file(tmp.path().join("gone.md")).unwrap();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute_batch(
            "CREATE TEMP TRIGGER reject_canvas_nodes BEFORE INSERT ON main.canvas_nodes
             BEGIN SELECT RAISE(ABORT, 'injected canvas failure'); END;",
        )
        .unwrap();
    }

    let report = rescan(&session);

    assert!(!report.complete, "a canvas failure left the scan complete");
    assert!(
        report
            .error_samples
            .iter()
            .any(|error| error.starts_with("canvas index")),
        "{:?}",
        report.error_samples
    );
    assert_eq!(report.files_removed, 1, "the complete walk's prune ran");
    assert!(session.get_file_metadata("gone.md").unwrap().is_none());
}

/// And a clean rescan is complete.
#[test]
fn a_clean_rescan_is_complete() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("ok.md", b"ok\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let report = rescan(&session);
    assert!(report.complete, "{:?}", report.error_samples);
    assert_eq!(report.error_count, 0);
}

// --- what the host's re-sync reads (AR-18's fallback) ----------------------------

/// The indexed hashes come back in the caller's order, `None` for a path
/// the index lacks; the lookup is bounded and honours its token.
#[test]
fn indexed_content_hashes_answer_in_order_bounded_and_cancellable() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"alpha\n").unwrap();
        p.write_file("b.md", b"beta\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let hashes = session
        .indexed_content_hashes(
            &["b.md".into(), "missing.md".into(), "a.md".into()],
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(
        hashes,
        vec![
            Some(crate::content_hash(b"beta\n")),
            None,
            Some(crate::content_hash(b"alpha\n")),
        ]
    );

    let too_many: Vec<String> = (0..=crate::MAX_INDEXED_HASH_PATHS)
        .map(|n| format!("n{n}.md"))
        .collect();
    assert!(matches!(
        session.indexed_content_hashes(&too_many, &CancelToken::new()),
        Err(VaultError::InvalidArgument { .. })
    ));
    let at_limit: Vec<String> = too_many[..crate::MAX_INDEXED_HASH_PATHS].to_vec();
    assert_eq!(
        session
            .indexed_content_hashes(&at_limit, &CancelToken::new())
            .unwrap()
            .len(),
        crate::MAX_INDEXED_HASH_PATHS
    );

    let cancelled = CancelToken::new();
    cancelled.cancel();
    assert!(matches!(
        session.indexed_content_hashes(&["a.md".into()], &cancelled),
        Err(VaultError::Cancelled)
    ));
}

/// AR-18's fallback, why it compares against the index: two sessions
/// share `.slate/cache.sqlite` (the app and the CLI, contract 33). What
/// session B writes lands in the SHARED index, so session A's next scan
/// finds it already current and counts none of it (AR-25) — but A's
/// index lookups see every one of B's changes, which is what the host's
/// re-sync reads.
#[test]
fn another_sessions_writes_are_in_the_shared_index_but_not_in_this_scans_counts() {
    let (tmp, a) = make_vault(|p| {
        p.write_file("edit.md", b"before\n").unwrap();
        p.write_file("gone.md", b"gone\n").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();

    let b = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    b.scan_initial(&CancelToken::new()).unwrap();
    b.create_exclusive("new.md", "new\n").unwrap();
    let before = b
        .get_file_metadata("edit.md")
        .unwrap()
        .unwrap()
        .content_hash;
    b.save_text("edit.md", "after, from B\n", Some(&before))
        .unwrap();
    b.delete_file("gone.md").unwrap();

    let report = rescan(&a);
    assert!(report.complete, "{:?}", report.error_samples);
    assert_eq!(
        (report.files_changed, report.files_removed),
        (0, 0),
        "AR-25: another session's already-indexed writes are not this scan's counts"
    );
    assert_eq!(
        a.indexed_content_hashes(
            &["new.md".into(), "edit.md".into(), "gone.md".into()],
            &CancelToken::new(),
        )
        .unwrap(),
        vec![
            Some(crate::content_hash(b"new\n")),
            Some(crate::content_hash(b"after, from B\n")),
            None,
        ],
        "session A's index lookups see all three of B's changes"
    );
}

/// The open scan reports the same hash-authoritative counts: a new
/// vault is all new, a reopened unchanged vault is "0 new or changed",
/// and a file deleted between sessions is removed.
#[test]
fn the_open_scan_reports_changed_and_removed_too() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("a.md"), b"a\n").unwrap();
    std::fs::write(tmp.path().join("b.md"), b"b\n").unwrap();
    let first = VaultSession::from_filesystem(tmp.path().to_path_buf())
        .unwrap()
        .scan_initial(&CancelToken::new())
        .unwrap();
    assert_eq!(
        (first.files_seen, first.files_changed, first.files_removed),
        (2, 2, 0)
    );
    assert!(first.complete);

    let reopened = VaultSession::from_filesystem(tmp.path().to_path_buf())
        .unwrap()
        .scan_initial(&CancelToken::new())
        .unwrap();
    assert_eq!(
        (
            reopened.files_seen,
            reopened.files_changed,
            reopened.files_removed
        ),
        (2, 0, 0)
    );

    std::fs::remove_file(tmp.path().join("b.md")).unwrap();
    let after_delete = VaultSession::from_filesystem(tmp.path().to_path_buf())
        .unwrap()
        .scan_initial(&CancelToken::new())
        .unwrap();
    assert_eq!(
        (
            after_delete.files_seen,
            after_delete.files_changed,
            after_delete.files_removed
        ),
        (1, 0, 1)
    );
}

// --- openability is core's (round 26) --------------------------------------------------

/// The path classifier core exports (`is_openable_document`, the host's
/// pinned set) is the `OpenableDocuments` filter itself: over indexed rows of every openable
/// extension (any case) and a few that are not, the paths the filter lists
/// are exactly the paths `is_openable_document` accepts.
#[test]
fn the_openable_classifier_is_the_openable_documents_filter() {
    let (_tmp, session) = make_vault(|p| {
        for name in [
            "a.md",
            "b.markdown",
            "c.mdown",
            "d.mkd",
            "e.canvas",
            "f.base",
            "G.MD",
            "H.Mdown",
            "i.txt",
            "j.png",
            "k.pdf",
            "l.md.bak",
        ] {
            p.write_file(name, b"x\n").unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let listed: std::collections::BTreeSet<String> = session
        .list_files(FileFilter::OpenableDocuments, Paging::first(100))
        .unwrap()
        .items
        .into_iter()
        .map(|summary| summary.path)
        .collect();
    let all: Vec<String> = session
        .list_files(FileFilter::All, Paging::first(100))
        .unwrap()
        .items
        .into_iter()
        .map(|summary| summary.path)
        .collect();
    let classified: std::collections::BTreeSet<String> = all
        .into_iter()
        .filter(|path| crate::is_openable_document(path))
        .collect();
    assert_eq!(listed, classified);
    assert!(listed.contains("c.mdown") && listed.contains("d.mkd") && listed.contains("H.Mdown"));
}

// --- a bounded report where it accumulates (round 27) ---------------------------------

/// Locked decision 05's memory-bounded rule, in core itself: thousands of
/// refused files leave the report with the EXACT count and at most
/// `SCAN_ERROR_SAMPLES` verbatim messages — at open and at a rescan —
/// because nothing collects one string per failure (each is logged as it
/// happens). The FFI projections are the uniffi facts'.
#[test]
fn the_core_scan_report_stays_bounded_under_thousands_of_failures() {
    const FAILING: usize = 3000;
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("ok.md"), b"ok\n").unwrap();
    for n in 0..FAILING {
        std::fs::write(tmp.path().join(format!("big-{n:04}.md")), [b'x'; 64]).unwrap();
    }
    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 32;
    let session = VaultSession::open(
        Arc::new(FsVaultProvider::new(tmp.path().to_path_buf())),
        config,
    )
    .unwrap();

    crate::session::SCAN_ERROR_STREAM.with(|stream| *stream.borrow_mut() = Some(Vec::new()));
    let opened = session.scan_initial(&CancelToken::new()).unwrap();
    let streamed_at_open = crate::session::SCAN_ERROR_STREAM
        .with(|stream| stream.borrow_mut().take())
        .unwrap();
    assert_eq!(opened.error_count, FAILING as u64);
    assert_eq!(opened.error_samples.len(), crate::SCAN_ERROR_SAMPLES);
    assert!(!opened.complete);
    assert_eq!(
        streamed_at_open.len(),
        FAILING,
        "the open scan's log missed errors"
    );
    assert_eq!(
        opened.error_samples,
        streamed_at_open[..crate::SCAN_ERROR_SAMPLES].to_vec(),
        "the open scan's samples are not the first errors streamed"
    );

    // A refused file keeps its (mtime, size) row: change every one.
    for n in 0..FAILING {
        std::fs::write(tmp.path().join(format!("big-{n:04}.md")), [b'y'; 65]).unwrap();
    }
    // The full stream the scan logs, read through the test seam: the
    // report bounds what crosses to the host, never what is logged.
    crate::session::SCAN_ERROR_STREAM.with(|stream| *stream.borrow_mut() = Some(Vec::new()));
    let rescanned = rescan(&session);
    let streamed = crate::session::SCAN_ERROR_STREAM
        .with(|stream| stream.borrow_mut().take())
        .unwrap();
    assert_eq!(rescanned.error_count, FAILING as u64);
    assert_eq!(rescanned.error_samples.len(), crate::SCAN_ERROR_SAMPLES);
    assert!(!rescanned.complete);

    // Every error reached the log — one per refused file, each naming its
    // own file — and the samples are the stream's first five, verbatim.
    assert_eq!(streamed.len(), FAILING, "the log missed errors");
    let named: std::collections::BTreeSet<usize> = streamed
        .iter()
        .map(|error| {
            assert!(
                error.contains("exceeds large-file refuse threshold"),
                "{error}"
            );
            (0..FAILING)
                .find(|n| error.contains(&format!("big-{n:04}.md")))
                .unwrap_or_else(|| panic!("an error names no refused file: {error}"))
        })
        .collect();
    assert_eq!(
        named.len(),
        FAILING,
        "some refused file never reached the log"
    );
    assert_eq!(
        rescanned.error_samples,
        streamed[..crate::SCAN_ERROR_SAMPLES].to_vec(),
        "the samples are not the first errors streamed"
    );
}
