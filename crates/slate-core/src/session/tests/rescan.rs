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
    /// Round 5 (codex PR 7 round 4, finding 4): once armed, every read of
    /// a path with this extension fails. Armed at the canvas pass, it fails
    /// only the pass's reads: the walk fast-paths an unchanged board (its
    /// tuple and completion marker match) and never reads it, so the pass
    /// is the one reader — codex round 5 corrected "the walk read it".
    armed_read_fails: Option<(&'static str, Arc<std::sync::atomic::AtomicBool>)>,
}

impl FaultingProvider {
    fn over(root: &std::path::Path) -> Self {
        Self {
            inner: FsVaultProvider::new(root.to_path_buf()),
            list_dir_fails: None,
            stat_fails: None,
            stat_not_found: None,
            read_fails: None,
            armed_read_fails: None,
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
        if let Some((extension, armed)) = &self.armed_read_fails
            && relative.ends_with(extension)
            && armed.load(std::sync::atomic::Ordering::SeqCst)
        {
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
        .list_files(
            FileFilter::OpenableDocuments,
            Paging::first(100),
            &CancelToken::new(),
        )
        .unwrap()
        .items
        .into_iter()
        .map(|summary| summary.path)
        .collect();
    let all: Vec<String> = session
        .list_files(FileFilter::All, Paging::first(100), &CancelToken::new())
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

// --- the re-sync's reopens (codex AR-18 review round 2, findings 2 and 4) -------

const NOTES_BASE: &[u8] =
    b"filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n";

/// Finding 2: a base handle carries the hash of the exact definition its
/// open parsed — and, after an edit, of the bytes the edit wrote — never a
/// separate index read that could see other bytes.
#[test]
fn a_base_open_returns_the_hash_of_the_definition_it_opened() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"# A\n").unwrap();
        p.write_file("Notes.base", NOTES_BASE).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // The file moves past the index before the open: the open's hash is
    // the bytes it parsed, not the index's.
    let rewritten: &[u8] =
        b"filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Rewritten\n";
    std::fs::write(tmp.path().join("Notes.base"), rewritten).unwrap();

    let opened = session
        .open_base_cancellable("Notes.base", &CancelToken::new())
        .unwrap();

    assert_eq!(opened.content_hash, content_hash(rewritten));
    assert_ne!(
        indexed_hash(&session, "Notes.base").as_deref(),
        Some(opened.content_hash.as_str()),
        "the index still holds the scanned bytes"
    );
    assert_eq!(
        session.base_views(opened.handle).unwrap()[0].name,
        "Rewritten"
    );
    assert_eq!(
        session.base_definition_hash(opened.handle).unwrap(),
        Some(opened.content_hash.clone())
    );

    session
        .base_apply_edit(
            opened.handle,
            crate::bases::BaseEdit::RenameView {
                view: 0,
                name: "Renamed".to_string(),
            },
        )
        .unwrap();
    let written = std::fs::read(tmp.path().join("Notes.base")).unwrap();
    assert_eq!(
        session.base_definition_hash(opened.handle).unwrap(),
        Some(content_hash(&written))
    );

    let inline = session.open_base_inline("views: []\n", None).unwrap();
    assert_eq!(session.base_definition_hash(inline).unwrap(), None);
}

/// Finding 4: a base open whose token is cancelled registers no handle.
#[test]
fn a_cancelled_base_open_registers_no_handle() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Notes.base", NOTES_BASE).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let cancel = CancelToken::new();
    cancel.cancel();

    let opened = session.open_base_cancellable("Notes.base", &cancel);

    assert!(matches!(opened, Err(VaultError::Cancelled)), "{opened:?}");
    assert!(session.bases.lock().unwrap().is_empty());
    // The tokenless form is the wrapper, unchanged.
    let handle = session.open_base("Notes.base").unwrap();
    assert!(session.bases.lock().unwrap().contains_key(&handle));
}

// --- the scan's tail honours the token (codex PR 7 round 3, finding 2) -----------

#[derive(Default)]
struct ProgressLog(Mutex<Vec<String>>);

impl ScanProgressListener for ProgressLog {
    fn on_progress(&self, event: ScanProgress) {
        let tag = match event {
            ScanProgress::Started { .. } => "started",
            ScanProgress::FileIndexed { .. } => "file",
            ScanProgress::Finished { .. } => "finished",
            ScanProgress::Cancelled => "cancelled",
            ScanProgress::Failed { .. } => "failed",
        };
        self.0.lock().unwrap().push(tag.to_string());
    }
}

/// A vault whose rescan has work at every tail point: a new note (`c.md`)
/// that resolves `a.md`'s link, a removed note (`b.md`) and a removed
/// folder (`dir1`) for both prunes, and a board for the canvas pass.
fn tail_vault() -> (tempfile::TempDir, VaultSession) {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[c]]\n").unwrap();
        p.write_file("b.md", b"# B\n").unwrap();
        p.create_dir("dir1").unwrap();
        p.write_file("dir1/x.md", b"# X\n").unwrap();
        p.write_file(
            "board.canvas",
            br#"{"nodes":[{"id":"n1","type":"file","file":"a.md","x":0,"y":0,"width":10,"height":10}],"edges":[]}"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    std::fs::write(tmp.path().join("c.md"), b"# C\n").unwrap();
    std::fs::remove_file(tmp.path().join("b.md")).unwrap();
    std::fs::remove_dir_all(tmp.path().join("dir1")).unwrap();
    (tmp, session)
}

fn a_links_to_c(session: &VaultSession) -> bool {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT links.target_path FROM links JOIN files ON files.id = links.source_file_id
         WHERE files.path = 'a.md'",
        [],
        |row| row.get::<_, Option<String>>(0),
    )
    .unwrap()
    .as_deref()
        == Some("c.md")
}

/// A rescan cancelled at the `nth` firing of a named point.
struct CancelledAt {
    result: Result<ScanReport, VaultError>,
    /// The progress stream's events.
    events: Vec<String>,
    /// Whether the point fired `nth` times.
    fired: bool,
    /// Every named point that fired AFTER the cancelling one — a scan that
    /// stops at its point fires none.
    after: Vec<String>,
}

/// Cancel the rescan's token at the `nth` firing of `point`.
fn rescan_cancelled_at(session: &VaultSession, point: &'static str, nth: usize) -> CancelledAt {
    let cancel = CancelToken::new();
    let trip = cancel.clone();
    let seen = Arc::new(Mutex::new((0usize, Vec::<String>::new())));
    let record = seen.clone();
    crate::session::scan_point_test_hook::install(Box::new(move |at| {
        let mut state = record.lock().unwrap();
        if trip.is_cancelled() {
            state.1.push(at.to_string());
        } else if at == point {
            state.0 += 1;
            if state.0 == nth {
                trip.cancel();
            }
        }
    }));
    let log = Arc::new(ProgressLog::default());
    let listener: Arc<dyn ScanProgressListener> = log.clone();
    let result = session.rescan_with_progress(&cancel, Some(listener));
    crate::session::scan_point_test_hook::clear();
    let events = log.0.lock().unwrap().clone();
    let (count, after) = seen.lock().unwrap().clone();
    CancelledAt {
        result,
        events,
        fired: count >= nth,
        after,
    }
}

/// Finding 2: a cancel that lands after the walk's last file, or at any
/// point of the tail — the file_meta flush, either prune (and inside the
/// file prune), link re-resolution (and inside it), the canvas pass (and
/// inside it), the commit — ends the scan THERE: no later point runs, the
/// stream's terminal event is `Cancelled`, and nothing the scan did is
/// committed.
#[test]
fn a_cancel_anywhere_in_the_scans_tail_rolls_the_scan_back() {
    // a.md, c.md, board.canvas: the last file indexed is the third.
    let points: [(&'static str, usize); 10] = [
        ("file", 3),
        ("meta flush", 1),
        ("prune dirs", 1),
        ("prune files", 1),
        ("prune file", 1),
        ("re-resolve", 1),
        ("re-resolve row", 1),
        ("canvas", 1),
        ("canvas file", 1),
        ("commit", 1),
    ];
    for (point, nth) in points {
        let (_tmp, session) = tail_vault();

        let CancelledAt {
            result,
            events,
            fired,
            after,
        } = rescan_cancelled_at(&session, point, nth);

        assert!(fired, "{point}: the point never fired");
        assert!(
            matches!(result, Err(VaultError::Cancelled)),
            "{point}: {result:?}"
        );
        assert!(
            after.is_empty(),
            "{point}: the scan ran on past its cancel: {after:?}"
        );
        assert_eq!(
            events.last().map(String::as_str),
            Some("cancelled"),
            "{point}: {events:?}"
        );
        assert!(
            indexed_hash(&session, "c.md").is_none(),
            "{point}: c.md was committed"
        );
        assert!(
            indexed_hash(&session, "b.md").is_some(),
            "{point}: b.md was pruned"
        );
        assert!(
            dir_rows(&session).contains(&"dir1".to_string()),
            "{point}: dir1 was pruned"
        );
        assert!(
            !a_links_to_c(&session),
            "{point}: the re-resolution was committed"
        );

        // The next clean rescan commits all of it.
        let report = rescan(&session);
        assert!(report.complete, "{point}");
        assert!(indexed_hash(&session, "c.md").is_some(), "{point}");
        assert!(indexed_hash(&session, "b.md").is_none(), "{point}");
        assert!(!dir_rows(&session).contains(&"dir1".to_string()), "{point}");
        assert!(a_links_to_c(&session), "{point}");
    }
}

/// Finding 2: a cancel that lands after the commit keeps the committed
/// scan (`Ok`, `Finished`) and skips the best-effort maintenance that
/// follows — the op-log reconcile, the events rebuild, the age-out and the
/// compaction sweep each rerun on the next scan — so a closing host is not
/// held behind it.
#[test]
fn a_cancel_after_the_commit_keeps_the_scan_and_skips_its_maintenance() {
    let (_tmp, session) = tail_vault();
    let recorder = Arc::new(ChangeRecorder::default());
    session.register_event_listener(recorder.clone());

    let CancelledAt {
        result,
        events,
        fired,
        ..
    } = rescan_cancelled_at(&session, "maintenance", 1);

    assert!(fired);
    assert!(result.is_ok(), "{result:?}");
    assert_eq!(events.last().map(String::as_str), Some("finished"));
    assert!(indexed_hash(&session, "c.md").is_some());
    let phases = recorder.phases.lock().unwrap().clone();
    assert!(
        !phases.contains(&IndexPhase::ReconcileStarted),
        "{phases:?}"
    );
    assert!(phases.contains(&IndexPhase::ScanFinished), "{phases:?}");
}

/// Codex PR 7 round 4, finding 4: a board the walk saw that the post-walk
/// canvas pass cannot read (unreadable between the walk and its tail) is a
/// counted error — the scan is incomplete, never "complete" over stale
/// card rows — with the exact count and at most the bounded samples.
#[test]
fn a_board_the_canvas_pass_cannot_read_makes_the_scan_incomplete() {
    let (tmp, session) = make_vault(|p| {
        for index in 0..7 {
            p.write_file(
                &format!("board{index}.canvas"),
                br#"{"nodes":[{"id":"n1","type":"text","text":"Card","x":0,"y":0,"width":10,"height":10}],"edges":[]}"#,
            )
            .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    drop(session);

    let armed = Arc::new(std::sync::atomic::AtomicBool::new(false));
    let mut provider = FaultingProvider::over(tmp.path());
    provider.armed_read_fails = Some((".canvas", armed.clone()));
    let session = reopen_through(&tmp, provider);
    let arm = armed.clone();
    crate::session::scan_point_test_hook::install(Box::new(move |point| {
        if point == "canvas" {
            arm.store(true, std::sync::atomic::Ordering::SeqCst);
        }
    }));
    let report = rescan(&session);
    crate::session::scan_point_test_hook::clear();

    assert!(
        armed.load(std::sync::atomic::Ordering::SeqCst),
        "the canvas pass never ran"
    );
    assert!(!report.complete, "{report:?}");
    assert_eq!(report.error_count, 7, "{report:?}");
    assert_eq!(
        report.error_samples.len(),
        crate::session::SCAN_ERROR_SAMPLES
    );
    assert!(
        report
            .error_samples
            .iter()
            .all(|sample| sample.contains(".canvas")),
        "{report:?}"
    );
}

const ONE_CARD: &[u8] =
    br#"{"nodes":[{"id":"n1","type":"text","text":"Card","x":0,"y":0,"width":10,"height":10}],"edges":[]}"#;

/// Codex PR 7 round 5 (fix 2): a board whose new bytes the walk cannot read
/// — the walk's slow path fails on it, the canvas pass fails on it again —
/// is ONE error: the pass counts only boards the walk refreshed this scan.
#[test]
fn a_persistently_unreadable_board_is_one_error() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("board.canvas", ONE_CARD).unwrap();
        p.write_file("n.md", b"n\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    drop(session);
    std::fs::write(
        tmp.path().join("board.canvas"),
        br#"{"nodes":[{"id":"n1","type":"text","text":"Card, rewritten","x":0,"y":0,"width":10,"height":10}],"edges":[]}"#,
    )
    .unwrap();

    let mut provider = FaultingProvider::over(tmp.path());
    provider.read_fails = Some("board.canvas".into());
    let session = reopen_through(&tmp, provider);
    let report = rescan(&session);

    assert!(!report.complete, "{report:?}");
    assert_eq!(report.error_count, 1, "{report:?}");
}

/// Codex PR 7 round 5 (fix 2): a board deleted outside Slate during a scan
/// that is partial for another reason (an unreadable note) keeps its row
/// — a partial scan prunes nothing — and the canvas pass adds no phantom
/// NotFound for it: the note is the ONE error.
#[test]
fn a_board_deleted_during_a_partial_scan_adds_no_error() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("board.canvas", ONE_CARD).unwrap();
        p.write_file("n.md", b"n\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    drop(session);
    std::fs::remove_file(tmp.path().join("board.canvas")).unwrap();
    std::fs::write(tmp.path().join("n.md"), b"n, rewritten\n").unwrap();

    let mut provider = FaultingProvider::over(tmp.path());
    provider.read_fails = Some("n.md".into());
    let session = reopen_through(&tmp, provider);
    let report = rescan(&session);

    assert!(!report.complete, "{report:?}");
    assert_eq!(report.error_count, 1, "{report:?}");
    assert!(
        report
            .error_samples
            .iter()
            .all(|sample| sample.contains("n.md")),
        "{report:?}"
    );
}
