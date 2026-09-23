// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! W7-7 PR 7 (#1252, contract R-9): the rescan of an open session and
//! the delta ledger it leaves for the host (`crate::scan_delta`).
//!
//! Every fact here drives the REAL scan (`rescan_with_progress`) over a
//! real vault on disk; the host's side of the protocol — read a page,
//! apply its effects, report it applied, release — is played by the
//! helpers below exactly as `VaultLifecycleViewModel.ReconcileScanDelta`
//! plays it.

use std::sync::{Arc, Mutex};

use super::common::*;
use super::*;
use crate::scan_delta::{MAX_SCAN_DELTA_PAGE_LIMIT, ScanDeltaKind};

fn rescan(session: &VaultSession) -> ScanReport {
    session
        .rescan_with_progress(&CancelToken::new(), None)
        .expect("rescan")
}

type Entry = (ScanDeltaKind, String);

fn entry(kind: ScanDeltaKind, path: &str) -> Entry {
    (kind, path.to_string())
}

/// Play the host: page the Pending generation from its stored cursor,
/// report each page applied after "applying" it, and return every entry
/// in page order.
fn apply_pending(session: &VaultSession, limit: u32) -> Vec<Entry> {
    let pending = session
        .scan_delta_pending()
        .unwrap()
        .expect("a pending generation");
    let mut cursor = pending.cursor.clone();
    let mut seen = Vec::new();
    loop {
        let page = session
            .scan_delta_page(
                pending.generation,
                Paging {
                    cursor: cursor.clone(),
                    limit,
                },
            )
            .unwrap();
        seen.extend(page.entries.iter().map(|e| (e.kind, e.path.clone())));
        session
            .scan_delta_page_applied(pending.generation, page.next_cursor.as_deref())
            .unwrap();
        match page.next_cursor {
            Some(next) => cursor = Some(next),
            None => return seen,
        }
    }
}

/// Apply everything pending and release: the entries and the spoken
/// outcome of one complete, uninterrupted rescan.
fn settle(session: &VaultSession) -> (Vec<Entry>, ScanDeltaOutcome) {
    let entries = apply_pending(session, 100);
    (entries, session.scan_delta_release().unwrap())
}

/// `(state, generations)` straight from the TEMP table — the invariant
/// the ledger's own summary could not show a violation of.
fn raw_generations_by_state(session: &VaultSession) -> Vec<(i64, i64)> {
    let conn = session.conn.lock().unwrap();
    let mut stmt = conn
        .prepare(
            "SELECT state, COUNT(*) FROM temp.scan_delta_generation GROUP BY state ORDER BY state",
        )
        .unwrap();
    stmt.query_map([], |row| Ok((row.get(0)?, row.get(1)?)))
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap()
}

fn raw_retained_rows(session: &VaultSession) -> i64 {
    let conn = session.conn.lock().unwrap();
    conn.query_row("SELECT COUNT(*) FROM temp.scan_delta_row", [], |row| {
        row.get(0)
    })
    .unwrap()
}

fn assert_ledger_bounded(session: &VaultSession, step: &str) {
    for (state, count) in raw_generations_by_state(session) {
        assert!(
            count <= 1,
            "{step}: the ledger holds {count} generations in state {state}; \
             at most one Pending and one Applied may exist"
        );
    }
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

fn retained_hashes(session: &VaultSession, path: &str) -> Option<(Option<String>, Option<String>)> {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT prior_hash, new_hash FROM temp.scan_delta_row WHERE path = ?1",
        rusqlite::params![path],
        |row| Ok((row.get(0)?, row.get(1)?)),
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

// --- (a)–(d): what a rescan finds ------------------------------------------

#[test]
fn a_rescan_reports_a_file_created_outside_slate_as_new() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"# Keep\n").unwrap();
    });
    let first = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        first.delta_generation, None,
        "the open scan retains nothing"
    );

    std::fs::write(tmp.path().join("late.md"), b"# Late\n").unwrap();
    let report = rescan(&session);

    assert_eq!(report.files_seen, 2);
    assert_eq!(report.files_changed, 1);
    assert_eq!(report.files_removed, 0);
    assert!(report.complete, "{:?}", report.errors);
    assert!(report.delta_generation.is_some());
    assert!(session.get_file_metadata("late.md").unwrap().is_some());
    let (entries, outcome) = settle(&session);
    assert_eq!(entries, vec![entry(ScanDeltaKind::Created, "late.md")]);
    assert_eq!(
        outcome,
        ScanDeltaOutcome {
            changed: 1,
            removed: 0
        }
    );
    assert_eq!(
        raw_retained_rows(&session),
        0,
        "a release leaves nothing retained"
    );
}

#[test]
fn a_rescan_reports_changed_bytes_as_modified_with_both_hashes() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"alpha\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = indexed_hash(&session, "note.md").unwrap();

    edit_same_size(&tmp, "note.md", b"omega\n");
    let report = rescan(&session);

    assert_eq!(report.files_changed, 1);
    assert_eq!(report.files_indexed, 1);
    let after = indexed_hash(&session, "note.md").unwrap();
    assert_eq!(after, crate::content_hash(b"omega\n"));
    assert_eq!(
        retained_hashes(&session, "note.md"),
        Some((Some(before), Some(after))),
        "the retained row carries its prior and new hash"
    );
    let (entries, outcome) = settle(&session);
    assert_eq!(entries, vec![entry(ScanDeltaKind::Modified, "note.md")]);
    assert_eq!(outcome.changed, 1);
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
    let (entries, outcome) = settle(&session);
    assert!(entries.is_empty(), "{entries:?}");
    assert_eq!(outcome, ScanDeltaOutcome::default());
}

/// `ACtimeZeroProviderBehavesTheSame`: Windows reports `ctime_ms = 0`;
/// the delta never used ctime, so created / modified / touched read the
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
    let (entries, _) = settle(&session);
    assert_eq!(
        entries,
        vec![
            entry(ScanDeltaKind::Modified, "edit.md"),
            entry(ScanDeltaKind::Created, "new.md"),
        ]
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
    let (entries, _) = settle(&session);
    assert!(entries.is_empty(), "{entries:?}");
}

#[test]
fn a_rescan_removes_the_row_of_a_file_deleted_outside_slate() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"keep\n").unwrap();
        p.write_file("gone.md", b"gone\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = indexed_hash(&session, "gone.md");

    std::fs::remove_file(tmp.path().join("gone.md")).unwrap();
    let report = rescan(&session);

    assert_eq!(report.files_removed, 1);
    assert_eq!(report.files_changed, 0);
    assert!(session.get_file_metadata("gone.md").unwrap().is_none());
    assert_eq!(
        retained_hashes(&session, "gone.md"),
        Some((before, None)),
        "the removed row keeps the hash the live index no longer has"
    );
    let (entries, outcome) = settle(&session);
    assert_eq!(entries, vec![entry(ScanDeltaKind::Removed, "gone.md")]);
    assert_eq!(
        outcome,
        ScanDeltaOutcome {
            changed: 0,
            removed: 1
        }
    );
}

/// AR-8: a real filesystem rename through the scan is Deleted + Created
/// — the index has no filesystem identity to correlate them with.
#[test]
fn a_filesystem_rename_is_a_removal_plus_a_creation() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("before.md", b"body\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::rename(tmp.path().join("before.md"), tmp.path().join("after.md")).unwrap();
    rescan(&session);

    let (entries, outcome) = settle(&session);
    assert_eq!(
        entries,
        vec![
            entry(ScanDeltaKind::Removed, "before.md"),
            entry(ScanDeltaKind::Created, "after.md"),
        ]
    );
    assert_eq!(
        outcome,
        ScanDeltaOutcome {
            changed: 1,
            removed: 1
        }
    );
    // Exhaustive by construction: a kind added to the delta must be
    // declared here, and `Renamed` is not one (AR-8).
    for (kind, _) in &entries {
        match kind {
            ScanDeltaKind::Removed | ScanDeltaKind::Created | ScanDeltaKind::Modified => {}
        }
    }
}

/// `AUniqueSameHashDeleteCreateIsNotARename`: an external delete of A
/// and an unrelated B with identical bytes retarget nothing.
#[test]
fn a_unique_same_hash_delete_create_is_not_a_rename() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"identical\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::remove_file(tmp.path().join("a.md")).unwrap();
    std::fs::write(tmp.path().join("b.md"), b"identical\n").unwrap();
    rescan(&session);

    let (entries, outcome) = settle(&session);
    assert_eq!(
        entries,
        vec![
            entry(ScanDeltaKind::Removed, "a.md"),
            entry(ScanDeltaKind::Created, "b.md"),
        ]
    );
    assert_eq!(
        outcome,
        ScanDeltaOutcome {
            changed: 1,
            removed: 1
        }
    );
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

// --- (h): completeness ------------------------------------------------------

/// Fails `list_dir` for one directory, and `stat` / `read_file` for one
/// path — the walk-level and file-level faults of R-9's completeness rule.
struct FaultingProvider {
    inner: FsVaultProvider,
    list_dir_fails: Option<String>,
    stat_fails: Option<String>,
    read_fails: Option<String>,
}

impl FaultingProvider {
    fn over(root: &std::path::Path) -> Self {
        Self {
            inner: FsVaultProvider::new(root.to_path_buf()),
            list_dir_fails: None,
            stat_fails: None,
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
/// vanish from the refreshed tree (`prune_unseen_dirs` used to run
/// unconditionally after a `list_dir` failure).
#[test]
fn an_unreadable_subtree_prunes_neither_files_nor_directories() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("root.md", b"root\n").unwrap();
        p.write_file("sub/nested.md", b"nested\n").unwrap();
        p.write_file("sub/deeper/leaf.md", b"leaf\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(dir_rows(&session), vec!["sub", "sub/deeper"]);
    drop(session);

    let mut provider = FaultingProvider::over(tmp.path());
    provider.list_dir_fails = Some("sub".into());
    let session = reopen_through(&tmp, provider);
    let report = rescan(&session);

    assert!(!report.complete, "a partial walk is never complete");
    assert!(!report.errors.is_empty());
    assert_eq!(
        dir_rows(&session),
        vec!["sub", "sub/deeper"],
        "a folder under the unreadable subtree vanished"
    );
    assert!(
        session
            .get_file_metadata("sub/nested.md")
            .unwrap()
            .is_some()
    );
    assert!(
        session
            .get_file_metadata("sub/deeper/leaf.md")
            .unwrap()
            .is_some()
    );
    assert_eq!(report.files_removed, 0);
    let (entries, _) = settle(&session);
    assert!(entries.is_empty(), "{entries:?}");
}

/// `complete` is false for a single file whose stat, read or index
/// fails through the provider — never the walk flag alone.
#[test]
fn a_single_file_failure_makes_the_rescan_incomplete() {
    // stat
    let (tmp, session) = make_vault(|p| {
        p.write_file("ok.md", b"ok\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    drop(session);
    std::fs::write(tmp.path().join("locked.md"), b"locked\n").unwrap();
    let mut provider = FaultingProvider::over(tmp.path());
    provider.stat_fails = Some("locked.md".into());
    let report = rescan(&reopen_through(&tmp, provider));
    assert!(!report.complete, "a stat failure left the rescan complete");
    assert_eq!(report.errors.len(), 1, "{:?}", report.errors);

    // read
    let (tmp, session) = make_vault(|p| {
        p.write_file("ok.md", b"ok\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    drop(session);
    std::fs::write(tmp.path().join("unreadable.md"), b"unreadable\n").unwrap();
    let mut provider = FaultingProvider::over(tmp.path());
    provider.read_fails = Some("unreadable.md".into());
    let report = rescan(&reopen_through(&tmp, provider));
    assert!(!report.complete, "a read failure left the rescan complete");
    assert_eq!(report.errors.len(), 1, "{:?}", report.errors);

    // index: a file past the refuse threshold is recorded, not indexed.
    let (tmp, session) = make_vault(|p| {
        p.write_file("ok.md", b"ok\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    drop(session);
    std::fs::write(tmp.path().join("huge.md"), vec![b'x'; 64]).unwrap();
    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 32;
    let session = VaultSession::open(
        Arc::new(FsVaultProvider::new(tmp.path().to_path_buf())),
        config,
    )
    .unwrap();
    let report = rescan(&session);
    assert!(
        !report.complete,
        "an index refusal left the rescan complete"
    );
    assert_eq!(report.errors.len(), 1, "{:?}", report.errors);

    // And a clean rescan is complete.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("ok.md", b"ok\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(rescan(&session).complete);
}

// --- (e): paging --------------------------------------------------------------

#[test]
fn delta_pages_are_bounded_progress_and_terminate() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("seed.md", b"seed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    for n in 1..=5 {
        std::fs::write(tmp.path().join(format!("n{n}.md")), format!("{n}\n")).unwrap();
    }
    let report = rescan(&session);
    let generation = report.delta_generation.unwrap();

    for limit in [0, MAX_SCAN_DELTA_PAGE_LIMIT + 1] {
        assert!(
            matches!(
                session.scan_delta_page(generation, Paging::first(limit)),
                Err(VaultError::InvalidArgument { .. })
            ),
            "limit {limit} was accepted"
        );
    }

    let mut cursor: Option<String> = None;
    let mut sizes = Vec::new();
    let mut cursors = Vec::new();
    loop {
        let page = session
            .scan_delta_page(
                generation,
                Paging {
                    cursor: cursor.clone(),
                    limit: 2,
                },
            )
            .unwrap();
        assert!(page.entries.len() <= 2, "a page exceeded its limit");
        sizes.push(page.entries.len());
        // Reading never moves the stored cursor — only applying does.
        assert_eq!(
            session.scan_delta_pending().unwrap().unwrap().cursor,
            cursor,
            "reading a page moved the pending cursor"
        );
        session
            .scan_delta_page_applied(generation, page.next_cursor.as_deref())
            .unwrap();
        match page.next_cursor {
            Some(next) => {
                assert!(!cursors.contains(&next), "the cursor did not progress");
                cursors.push(next.clone());
                assert_eq!(
                    session.scan_delta_pending().unwrap().unwrap().cursor,
                    Some(next.clone()),
                    "an applied page did not advance the pending cursor"
                );
                cursor = Some(next);
            }
            None => break,
        }
        assert!(sizes.len() <= 3, "paging did not terminate");
    }
    assert_eq!(sizes, vec![2, 2, 1]);
    assert!(
        session.scan_delta_pending().unwrap().is_none(),
        "the last page's report made the generation Applied"
    );
    assert!(matches!(
        session.scan_delta_page(generation, Paging::first(2)),
        Err(VaultError::InvalidArgument { .. })
    ));
}

#[test]
fn a_cursor_from_another_generation_fails_closed() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("seed.md", b"seed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    for n in 1..=3 {
        std::fs::write(tmp.path().join(format!("a{n}.md")), format!("{n}\n")).unwrap();
    }
    let first = rescan(&session).delta_generation.unwrap();
    let stale = session
        .scan_delta_page(first, Paging::first(1))
        .unwrap()
        .next_cursor
        .unwrap();
    settle(&session);
    std::fs::write(tmp.path().join("b.md"), b"b\n").unwrap();
    std::fs::write(tmp.path().join("c.md"), b"c\n").unwrap();
    let second = rescan(&session).delta_generation.unwrap();

    assert!(matches!(
        session.scan_delta_page(second, Paging::after(stale.clone(), 1)),
        Err(VaultError::InvalidArgument { .. })
    ));
    assert!(matches!(
        session.scan_delta_page_applied(second, Some(stale.as_str())),
        Err(VaultError::InvalidArgument { .. })
    ));
}

/// The generation is INTRINSICALLY removal-first: with a limit of one,
/// every removal pages before any creation or modification, even when
/// the removed paths sort last.
#[test]
fn a_generation_pages_every_removal_before_any_creation() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("m.md", b"alpha\n").unwrap();
        p.write_file("z1.md", b"z1\n").unwrap();
        p.write_file("z2.md", b"z2\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::remove_file(tmp.path().join("z1.md")).unwrap();
    std::fs::remove_file(tmp.path().join("z2.md")).unwrap();
    std::fs::write(tmp.path().join("a1.md"), b"a1\n").unwrap();
    std::fs::write(tmp.path().join("a2.md"), b"a2\n").unwrap();
    edit_same_size(&tmp, "m.md", b"omega\n");
    rescan(&session);

    assert_eq!(
        apply_pending(&session, 1),
        vec![
            entry(ScanDeltaKind::Removed, "z1.md"),
            entry(ScanDeltaKind::Removed, "z2.md"),
            entry(ScanDeltaKind::Created, "a1.md"),
            entry(ScanDeltaKind::Created, "a2.md"),
            entry(ScanDeltaKind::Modified, "m.md"),
        ]
    );
}

// --- atomic commit --------------------------------------------------------------

/// The index mutations and the retained generation are one SQLite
/// transaction: a fault at ANY commit boundary of a rescan leaves both
/// or neither — never an indexed change the ledger does not carry (the
/// host would never reconcile it) nor a generation over an index that
/// never changed.
#[test]
fn the_index_and_its_generation_commit_together_or_not_at_all() {
    fn fixture() -> (tempfile::TempDir, VaultSession) {
        let (tmp, session) = make_vault(|p| {
            p.write_file("seed.md", b"seed\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        std::fs::write(tmp.path().join("late.md"), b"late\n").unwrap();
        (tmp, session)
    }
    fn install(session: &VaultSession, fail_at: Option<usize>) -> Arc<Mutex<usize>> {
        let commits = Arc::new(Mutex::new(0usize));
        let counter = commits.clone();
        session
            .conn
            .lock()
            .unwrap()
            .commit_hook(Some(move || {
                let mut n = counter.lock().unwrap();
                *n += 1;
                // `true` turns this COMMIT into a ROLLBACK.
                fail_at == Some(*n)
            }))
            .unwrap();
        commits
    }
    fn uninstall(session: &VaultSession) {
        session
            .conn
            .lock()
            .unwrap()
            .commit_hook(None::<fn() -> bool>)
            .unwrap();
    }

    // How many commits one clean rescan makes — every one is a boundary.
    let (_tmp, session) = fixture();
    let commits = install(&session, None);
    rescan(&session);
    uninstall(&session);
    let boundaries = *commits.lock().unwrap();
    assert!(boundaries >= 1);

    for fail_at in 1..=boundaries {
        let (_tmp, session) = fixture();
        install(&session, Some(fail_at));
        let result = session.rescan_with_progress(&CancelToken::new(), None);
        uninstall(&session);
        let indexed = session.get_file_metadata("late.md").unwrap().is_some();
        let retained = session
            .scan_delta_pending()
            .unwrap()
            .is_some_and(|pending| pending.rows == 1);
        assert_eq!(
            indexed,
            retained,
            "a fault at commit {fail_at} of {boundaries} left the index {} but the ledger {} \
             ({result:?})",
            if indexed { "changed" } else { "unchanged" },
            if retained { "holding it" } else { "empty" },
        );
    }
}

// --- the ledger (retained generations) ---------------------------------------

#[test]
fn a_pending_generation_refuses_a_new_scan_until_its_effects_are_applied() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("seed.md", b"seed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    std::fs::write(tmp.path().join("late.md"), b"late\n").unwrap();
    rescan(&session);

    assert!(matches!(
        session.rescan_with_progress(&CancelToken::new(), None),
        Err(VaultError::InvalidArgument { .. })
    ));
    assert!(
        matches!(
            session.scan_delta_release(),
            Err(VaultError::InvalidArgument { .. })
        ),
        "a release while a generation is pending would split the retry's outcome"
    );
    apply_pending(&session, 10);
    rescan(&session);
}

/// Coordinator witness (b) and (a)'s core half: A fails on page 2 and is
/// recovered on retry 1, whose new generation B fails mid-page; retry 2
/// recovers B and its scan C succeeds. At no step does the ledger hold
/// more than one Pending and one Applied generation, the release speaks
/// A+B+C net per path since the last release, and nothing is retained
/// afterwards. (Mutation: skip the coalesce, keep two Applied rows.)
#[test]
fn the_ledger_never_holds_more_than_one_pending_and_one_applied_generation() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"h0h0\n").unwrap();
        p.write_file("b.md", b"bbbb\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // A: n1 created, a.md h0 → h1. Page 1 applied, page 2 "fails".
    std::fs::write(tmp.path().join("n1.md"), b"n1\n").unwrap();
    edit_same_size(&tmp, "a.md", b"h1h1\n");
    let a = rescan(&session).delta_generation.unwrap();
    let page = session.scan_delta_page(a, Paging::first(1)).unwrap();
    session
        .scan_delta_page_applied(a, page.next_cursor.as_deref())
        .unwrap();
    assert_ledger_bounded(&session, "A failed on page 2");
    assert_eq!(session.scan_delta_ledger().unwrap().applied, None);

    // Retry 1: A resumes from its cursor and becomes Applied; B (b.md
    // removed, a.md h1 → h2) is scanned and fails mid-page.
    let resumed = apply_pending(&session, 1);
    assert_eq!(resumed.len(), 1, "A resumed from its stored cursor");
    assert_ledger_bounded(&session, "A recovered");
    std::fs::remove_file(tmp.path().join("b.md")).unwrap();
    edit_same_size(&tmp, "a.md", b"h2h2\n");
    let b = rescan(&session).delta_generation.unwrap();
    let page = session.scan_delta_page(b, Paging::first(1)).unwrap();
    session
        .scan_delta_page_applied(b, page.next_cursor.as_deref())
        .unwrap();
    let ledger = session.scan_delta_ledger().unwrap();
    assert!(ledger.pending.is_some() && ledger.applied.is_some());
    assert_ledger_bounded(&session, "B failed mid-page beside Applied A");

    // Retry 2: B resumes, coalesces into A; C (n2 created) succeeds.
    apply_pending(&session, 1);
    assert_ledger_bounded(&session, "B coalesced into A");
    std::fs::write(tmp.path().join("n2.md"), b"n2\n").unwrap();
    rescan(&session);
    apply_pending(&session, 1);
    assert_ledger_bounded(&session, "C coalesced into A");

    // n1 created, a.md h0 → h2 (one modification), b.md removed, n2
    // created: 3 new or changed, 1 removed.
    assert_eq!(
        session.scan_delta_release().unwrap(),
        ScanDeltaOutcome {
            changed: 3,
            removed: 1
        }
    );
    assert_eq!(raw_retained_rows(&session), 0);
    assert!(raw_generations_by_state(&session).is_empty());
}

/// The ledger read is the hosts' view of the bound, so it fails closed on
/// a violation instead of answering with whichever generation a query
/// happened to return — the host twin of the bound fact
/// (`TheLedgerNeverHoldsMoreThanOnePendingAndOneAppliedGeneration`)
/// observes the invariant through it.
#[test]
fn a_ledger_read_refuses_a_second_generation_in_one_state() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    std::fs::write(tmp.path().join("b.md"), b"b\n").unwrap();
    rescan(&session);
    apply_pending(&session, 10);
    let applied = session.scan_delta_ledger().unwrap().applied.unwrap();

    let forge = |generation: u64, state: i64| {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT INTO temp.scan_delta_generation (generation, state) VALUES (?1, ?2)",
            rusqlite::params![generation as i64, state],
        )
        .unwrap();
    };
    forge(applied.generation + 100, 1);
    assert!(
        matches!(
            session.scan_delta_ledger(),
            Err(VaultError::InvalidArgument { .. })
        ),
        "two Applied generations read as a bounded ledger"
    );
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "DELETE FROM temp.scan_delta_generation WHERE generation != ?1",
            rusqlite::params![applied.generation as i64],
        )
        .unwrap();
    }
    assert!(session.scan_delta_ledger().is_ok());

    forge(applied.generation + 101, 0);
    forge(applied.generation + 102, 0);
    assert!(
        matches!(
            session.scan_delta_ledger(),
            Err(VaultError::InvalidArgument { .. })
        ),
        "two Pending generations read as a bounded ledger"
    );
}

fn raw_rows_of(
    session: &VaultSession,
    generation: u64,
) -> Vec<(String, Option<String>, Option<String>)> {
    let conn = session.conn.lock().unwrap();
    let mut stmt = conn
        .prepare(
            "SELECT path, prior_hash, new_hash FROM temp.scan_delta_row
             WHERE generation = ?1 ORDER BY seq",
        )
        .unwrap();
    stmt.query_map(rusqlite::params![generation as i64], |row| {
        Ok((row.get(0)?, row.get(1)?, row.get(2)?))
    })
    .unwrap()
    .collect::<Result<Vec<_>, _>>()
    .unwrap()
}

/// Round-19 amendment (`TheAppliedMarkAndTheCoalesceCommitTogether`):
/// the applied mark and the coalesce into the retained Applied generation
/// are ONE transaction. A fault between them lands neither — the
/// generation stays Pending at the cursor of its last page and the
/// Applied generation is untouched — and the next retry re-applies that
/// last page and finishes both, idempotently. Checked at both fault
/// sites: inside the transaction, between the two statements, and at its
/// commit boundary. (Mutation: commit the mark before the coalesce.)
#[test]
fn the_applied_mark_and_the_coalesce_commit_together() {
    for fault in [
        "between the mark and the coalesce",
        "at the commit boundary",
    ] {
        let (tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"h0h0\n").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        // An Applied generation an earlier retry left retained: a.md h0 → h1.
        edit_same_size(&tmp, "a.md", b"h1h1\n");
        rescan(&session);
        apply_pending(&session, 10);
        let applied_before = session.scan_delta_ledger().unwrap().applied.unwrap();
        let rows_before = raw_rows_of(&session, applied_before.generation);

        // The next generation: three creations, paged two at a time. The
        // first page's effects land; the last page's report faults.
        for n in 1..=3 {
            std::fs::write(tmp.path().join(format!("c{n}.md")), format!("{n}\n")).unwrap();
        }
        let generation = rescan(&session).delta_generation.unwrap();
        let first = session
            .scan_delta_page(generation, Paging::first(2))
            .unwrap();
        session
            .scan_delta_page_applied(generation, first.next_cursor.as_deref())
            .unwrap();
        let last_page_cursor = first.next_cursor.clone();
        let last = session
            .scan_delta_page(
                generation,
                Paging {
                    cursor: last_page_cursor.clone(),
                    limit: 2,
                },
            )
            .unwrap();
        assert!(
            last.next_cursor.is_none(),
            "{fault}: the second page is the last"
        );

        let result = if fault == "at the commit boundary" {
            session
                .conn
                .lock()
                .unwrap()
                .commit_hook(Some(|| true))
                .unwrap();
            let result = session.scan_delta_page_applied(generation, None);
            session
                .conn
                .lock()
                .unwrap()
                .commit_hook(None::<fn() -> bool>)
                .unwrap();
            result
        } else {
            crate::scan_delta::FAULT_BETWEEN_MARK_AND_COALESCE.with(|armed| armed.set(true));
            session.scan_delta_page_applied(generation, None)
        };

        assert!(result.is_err(), "{fault}: the fault did not surface");
        let ledger = session.scan_delta_ledger().unwrap();
        assert_eq!(
            ledger.pending,
            Some(ScanDeltaPending {
                generation,
                cursor: last_page_cursor.clone(),
                rows: 3,
            }),
            "{fault}: the generation left Pending or lost its cursor"
        );
        assert_eq!(
            ledger.applied,
            Some(applied_before.clone()),
            "{fault}: the Applied ledger changed"
        );
        assert_eq!(
            raw_rows_of(&session, applied_before.generation),
            rows_before,
            "{fault}: the Applied rows changed"
        );
        assert_ledger_bounded(&session, fault);

        // The retry resumes at the last page's cursor, re-applies that
        // page, and finishes the mark and the coalesce.
        assert_eq!(
            apply_pending(&session, 2),
            vec![entry(ScanDeltaKind::Created, "c3.md")],
            "{fault}: the retry did not resume at the last page"
        );
        let ledger = session.scan_delta_ledger().unwrap();
        assert_eq!(ledger.pending, None);
        assert_eq!(
            ledger.applied.map(|applied| applied.rows),
            Some(4),
            "{fault}: a.md plus three creations"
        );
        assert_eq!(
            session.scan_delta_release().unwrap(),
            ScanDeltaOutcome {
                changed: 4,
                removed: 0
            }
        );
    }
}

/// Coordinator witness (c): per-path composition across an Applied and a
/// newer generation, against the last released state — through real
/// rescans. (Mutation: release at the applied mark → every case speaks
/// the second generation alone.)
#[test]
fn coalescing_composes_each_path_against_the_last_release() {
    // Run `first`, apply it WITHOUT releasing (a later step failed), run
    // `second`, apply it, release: the spoken outcome of the retry.
    fn composed(
        seed: &[(&str, &str)],
        first: impl FnOnce(&std::path::Path),
        second: impl FnOnce(&std::path::Path),
    ) -> ScanDeltaOutcome {
        let tmp = tempfile::tempdir().unwrap();
        for (path, bytes) in seed {
            std::fs::write(tmp.path().join(path), bytes).unwrap();
        }
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        first(tmp.path());
        rescan(&session);
        apply_pending(&session, 1);
        second(tmp.path());
        rescan(&session);
        apply_pending(&session, 1);
        let outcome = session.scan_delta_release().unwrap();
        assert_eq!(raw_retained_rows(&session), 0);
        outcome
    }
    fn write_newer(root: &std::path::Path, path: &str, bytes: &[u8]) {
        let provider = FsVaultProvider::new(root.to_path_buf());
        let before = provider.stat(path).map(|s| s.mtime_ms).unwrap_or(-1);
        rewrite_until_mtime_advances(&provider, path, bytes, before);
    }
    let none = ScanDeltaOutcome::default();
    let modified = ScanDeltaOutcome {
        changed: 1,
        removed: 0,
    };
    let removed = ScanDeltaOutcome {
        changed: 0,
        removed: 1,
    };

    // h0 → h1, then h1 → h0: the effects happened, the speech nets away.
    assert_eq!(
        composed(
            &[("a.md", "h0h0\n")],
            |root| write_newer(root, "a.md", b"h1h1\n"),
            |root| write_newer(root, "a.md", b"h0h0\n"),
        ),
        none,
        "h0 -> h1 -> h0"
    );
    // h0 → h1, then h1 → h2: one modification.
    assert_eq!(
        composed(
            &[("a.md", "h0h0\n")],
            |root| write_newer(root, "a.md", b"h1h1\n"),
            |root| write_newer(root, "a.md", b"h2h2\n"),
        ),
        modified,
        "h0 -> h1 -> h2"
    );
    // create, then remove: nothing.
    assert_eq!(
        composed(
            &[("seed.md", "seed\n")],
            |root| std::fs::write(root.join("new.md"), b"new\n").unwrap(),
            |root| std::fs::remove_file(root.join("new.md")).unwrap(),
        ),
        none,
        "create -> remove"
    );
    // remove, then create with a new hash: modified.
    assert_eq!(
        composed(
            &[("a.md", "old\n")],
            |root| std::fs::remove_file(root.join("a.md")).unwrap(),
            |root| std::fs::write(root.join("a.md"), b"new bytes\n").unwrap(),
        ),
        modified,
        "remove -> create (new hash)"
    );
    // remove, then create with the same hash: nothing.
    assert_eq!(
        composed(
            &[("a.md", "same\n")],
            |root| std::fs::remove_file(root.join("a.md")).unwrap(),
            |root| std::fs::write(root.join("a.md"), b"same\n").unwrap(),
        ),
        none,
        "remove -> create (same hash)"
    );
    // modify, then remove: removed.
    assert_eq!(
        composed(
            &[("a.md", "h0h0\n")],
            |root| write_newer(root, "a.md", b"h1h1\n"),
            |root| std::fs::remove_file(root.join("a.md")).unwrap(),
        ),
        removed,
        "modify -> remove"
    );
}

fn temp_ledger_exists(session: &VaultSession) -> bool {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT EXISTS(SELECT 1 FROM temp.sqlite_master WHERE name = 'scan_delta_generation')",
        [],
        |row| row.get(0),
    )
    .unwrap()
}

/// The ledger's TEMP tables are created by the first rescan, never at open:
/// opening a vault never depends on the temp store, and a session that
/// never rescans (the mac host, the CLI) never touches it. Every read of
/// an absent ledger answers "nothing retained" without creating it.
#[test]
fn the_ledger_is_created_by_the_first_rescan_not_at_open() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(!temp_ledger_exists(&session), "the open created the ledger");

    assert_eq!(
        session.scan_delta_ledger().unwrap(),
        ScanDeltaLedger::default()
    );
    assert_eq!(session.scan_delta_pending().unwrap(), None);
    assert_eq!(
        session.scan_delta_release().unwrap(),
        ScanDeltaOutcome::default()
    );
    assert!(matches!(
        session.scan_delta_page(1, Paging::first(1)),
        Err(VaultError::InvalidArgument { .. })
    ));
    assert!(matches!(
        session.scan_delta_page_applied(1, None),
        Err(VaultError::InvalidArgument { .. })
    ));
    assert!(!temp_ledger_exists(&session), "a read created the ledger");

    std::fs::write(tmp.path().join("b.md"), b"b\n").unwrap();
    assert_eq!(rescan(&session).files_changed, 1);
    assert!(temp_ledger_exists(&session), "the first rescan creates it");
}

// --- restart protocol --------------------------------------------------------------

/// The initial-open path discards whatever this session retained before
/// it scans: the rebuilt index has no workspace state to reconcile.
#[test]
fn the_initial_open_scan_discards_retained_generations_before_it_scans() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("seed.md", b"seed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    std::fs::write(tmp.path().join("late.md"), b"late\n").unwrap();
    rescan(&session);
    assert!(session.scan_delta_pending().unwrap().is_some());

    let report = session.scan_initial(&CancelToken::new()).unwrap();

    assert_eq!(report.delta_generation, None);
    assert_eq!(
        session.scan_delta_ledger().unwrap(),
        ScanDeltaLedger::default()
    );
    assert_eq!(raw_retained_rows(&session), 0);
}

/// Crash-equivalent: a session abandoned WITHOUT close/dispose cleanup
/// leaves an unacknowledged generation behind; a fresh session on the
/// same vault sees no orphan — before it scans and after — so nothing
/// stale is ever paged, applied or spoken against the rebuilt index.
#[test]
fn a_crash_orphaned_generation_never_reaches_a_fresh_session() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("seed.md"), b"seed\n").unwrap();
    let abandoned = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    abandoned.scan_initial(&CancelToken::new()).unwrap();
    std::fs::write(tmp.path().join("late.md"), b"late\n").unwrap();
    rescan(&abandoned);
    assert!(abandoned.scan_delta_pending().unwrap().is_some());
    // No Drop, no close: the process "died" with the generation unacknowledged.
    std::mem::forget(abandoned);

    let fresh = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    assert_eq!(
        fresh.scan_delta_ledger().unwrap(),
        ScanDeltaLedger::default(),
        "an orphan reached the fresh session before its open scan"
    );
    let report = fresh.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.delta_generation, None);
    assert_eq!(
        fresh.scan_delta_ledger().unwrap(),
        ScanDeltaLedger::default()
    );
    assert_eq!(
        fresh.scan_delta_release().unwrap(),
        ScanDeltaOutcome::default()
    );
    // And the fresh session's own rescans are unblocked.
    std::fs::write(tmp.path().join("later.md"), b"later\n").unwrap();
    assert_eq!(rescan(&fresh).files_changed, 1);
}

// --- the report --------------------------------------------------------------------

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
