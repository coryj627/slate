// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! FL2-2a: one-request structural Move/Trash planning and execution.

use super::*;
use crate::structural_batch::{
    BatchMoveRequest, BatchMoveState, BatchTrashRequest, BatchTrashState,
    MAX_STRUCTURAL_BATCH_ITEMS, StructuralBatchItem,
};
use crate::{DirEntry, FileEventSink, FileStat, WatchHandle};
use std::collections::{BTreeMap, BTreeSet};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex, Weak, mpsc};
use std::time::Duration;

#[derive(Debug, Clone, PartialEq, Eq)]
enum ProviderCall {
    PreflightRename(String, String),
    PreflightDelete(String),
    Rename(String, String),
    Delete(String),
}

#[derive(Debug, Default)]
struct FaultState {
    calls: Vec<ProviderCall>,
    fail_preflight_renames: BTreeSet<(String, String)>,
    fail_preflight_deletes: BTreeSet<String>,
    fail_renames: BTreeSet<(String, String)>,
    fail_renames_after_mutation: BTreeSet<(String, String)>,
    panic_after_rename_numbers: BTreeSet<usize>,
    duplicate_then_fail_renames: BTreeSet<(String, String)>,
    remove_then_fail_renames: BTreeSet<(String, String)>,
    fail_delete_numbers: BTreeSet<usize>,
    fail_deletes_after_mutation: BTreeSet<usize>,
    replace_after_delete_with_kind: BTreeMap<String, EntryKind>,
    fail_existence_after_delete: BTreeSet<String>,
    delete_attempts: BTreeSet<String>,
    fail_stat_after_write: BTreeMap<String, usize>,
    pending_stat_failures: BTreeSet<String>,
    fail_read_after_write: BTreeMap<String, usize>,
    pending_read_failures: BTreeSet<String>,
    delete_number: usize,
    rename_number: usize,
}

#[derive(Debug)]
struct FaultInjectingProvider {
    inner: FsVaultProvider,
    root: std::path::PathBuf,
    state: Arc<Mutex<FaultState>>,
}

#[derive(Debug, Default)]
struct TestBatchFaults {
    points: BTreeSet<BatchFaultPoint>,
}

#[derive(Debug)]
struct NthBatchFault {
    point: BatchFaultPoint,
    fail_on: usize,
    calls: Mutex<usize>,
}

struct BlockingBatchFault {
    point: BatchFaultPoint,
    entered: Mutex<Option<mpsc::Sender<()>>>,
    release: Mutex<mpsc::Receiver<()>>,
}

impl StructuralBatchFaultHook for BlockingBatchFault {
    fn check(&self, point: BatchFaultPoint) -> Result<(), VaultError> {
        if point != self.point {
            return Ok(());
        }
        if let Some(entered) = self.entered.lock().unwrap().take() {
            let _ = entered.send(());
        }
        self.release
            .lock()
            .unwrap()
            .recv_timeout(Duration::from_secs(5))
            .map_err(|_| FaultInjectingProvider::injected("batch test release timed out"))?;
        Ok(())
    }
}

struct PanicBatchFault(BatchFaultPoint);

impl StructuralBatchFaultHook for PanicBatchFault {
    fn check(&self, point: BatchFaultPoint) -> Result<(), VaultError> {
        assert_ne!(point, self.0, "simulated process crash at {point:?}");
        Ok(())
    }
}

struct BlockingRecoveryBarrierFault {
    entered: Mutex<Option<mpsc::Sender<()>>>,
    release: Mutex<mpsc::Receiver<()>>,
}

impl StructuralBatchFaultHook for BlockingRecoveryBarrierFault {
    fn check(&self, point: BatchFaultPoint) -> Result<(), VaultError> {
        if point != BatchFaultPoint::RecoveryBarrier {
            return Ok(());
        }
        if let Some(entered) = self.entered.lock().unwrap().take() {
            let _ = entered.send(());
        }
        self.release
            .lock()
            .unwrap()
            .recv_timeout(Duration::from_secs(5))
            .map_err(|_| FaultInjectingProvider::injected("barrier test release timed out"))?;
        Err(FaultInjectingProvider::injected(
            "injected recovery barrier failure after blocking",
        ))
    }
}

struct ReentrantStructuralListener {
    session: Weak<VaultSession>,
    fired: AtomicBool,
}

impl VaultEventListener for ReentrantStructuralListener {
    fn on_error(&self, _code: EventErrorCode, _path: String, _message: String) {}

    fn on_file_change(&self, _event: FileChangeEvent) {
        if !self.fired.swap(true, Ordering::SeqCst)
            && let Some(session) = self.session.upgrade()
        {
            session
                .create_folder("created-from-listener")
                .expect("structural callbacks run after releasing the operation guard");
        }
    }
}

impl NthBatchFault {
    fn new(point: BatchFaultPoint, fail_on: usize) -> Self {
        Self {
            point,
            fail_on,
            calls: Mutex::new(0),
        }
    }
}

impl StructuralBatchFaultHook for NthBatchFault {
    fn check(&self, point: BatchFaultPoint) -> Result<(), VaultError> {
        if point != self.point {
            return Ok(());
        }
        let mut calls = self.calls.lock().unwrap();
        *calls += 1;
        if *calls == self.fail_on {
            Err(VaultError::InvalidArgument {
                message: format!("injected batch fault at {point:?} call {calls}"),
            })
        } else {
            Ok(())
        }
    }
}

impl TestBatchFaults {
    fn at(point: BatchFaultPoint) -> Self {
        Self {
            points: BTreeSet::from([point]),
        }
    }

    fn at_all(points: impl IntoIterator<Item = BatchFaultPoint>) -> Self {
        Self {
            points: points.into_iter().collect(),
        }
    }
}

impl StructuralBatchFaultHook for TestBatchFaults {
    fn check(&self, point: BatchFaultPoint) -> Result<(), VaultError> {
        if self.points.contains(&point) {
            Err(VaultError::InvalidArgument {
                message: format!("injected batch fault at {point:?}"),
            })
        } else {
            Ok(())
        }
    }
}

impl FaultInjectingProvider {
    fn new(root: std::path::PathBuf) -> (Arc<Self>, Arc<Mutex<FaultState>>) {
        let state = Arc::new(Mutex::new(FaultState::default()));
        (
            Arc::new(Self {
                inner: FsVaultProvider::new(root.clone()),
                root,
                state: Arc::clone(&state),
            }),
            state,
        )
    }

    fn injected(message: &str) -> VaultError {
        VaultError::InvalidArgument {
            message: message.to_string(),
        }
    }
}

impl VaultProvider for FaultInjectingProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }

    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }

    fn read_file_with_cap(&self, relative: &str, max_bytes: u64) -> Result<Vec<u8>, VaultError> {
        if self
            .state
            .lock()
            .unwrap()
            .pending_read_failures
            .remove(relative)
        {
            return Err(Self::injected(
                "injected physical verification failure after write",
            ));
        }
        self.inner.read_file_with_cap(relative, max_bytes)
    }

    fn read_in_vault_with_cap(
        &self,
        relative: &str,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        self.inner.read_in_vault_with_cap(relative, max_bytes)
    }

    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)?;
        let mut state = self.state.lock().unwrap();
        let arm_stat = state
            .fail_stat_after_write
            .get_mut(relative)
            .is_some_and(|remaining| {
                *remaining = remaining.saturating_sub(1);
                *remaining == 0
            });
        if arm_stat {
            state.fail_stat_after_write.remove(relative);
            state.pending_stat_failures.insert(relative.to_string());
        }
        let arm_read = state
            .fail_read_after_write
            .get_mut(relative)
            .is_some_and(|remaining| {
                *remaining = remaining.saturating_sub(1);
                *remaining == 0
            });
        if arm_read {
            state.fail_read_after_write.remove(relative);
            state.pending_read_failures.insert(relative.to_string());
        }
        Ok(())
    }

    fn write_file_if_absent(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file_if_absent(relative, contents)
    }

    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        let mut state = self.state.lock().unwrap();
        state.delete_number += 1;
        let number = state.delete_number;
        state.delete_attempts.insert(relative.to_string());
        state.calls.push(ProviderCall::Delete(relative.to_string()));
        let fail = state.fail_delete_numbers.contains(&number);
        let fail_after = state.fail_deletes_after_mutation.contains(&number);
        let replacement_kind = state.replace_after_delete_with_kind.get(relative).copied();
        drop(state);
        if let Some(replacement_kind) = replacement_kind {
            let path = self.root.join(relative);
            let metadata = path.symlink_metadata().map_err(VaultError::Io)?;
            if metadata.is_dir() {
                std::fs::remove_dir_all(&path).map_err(VaultError::Io)?;
            } else {
                std::fs::remove_file(&path).map_err(VaultError::Io)?;
            }
            match replacement_kind {
                EntryKind::File => {
                    std::fs::write(path, b"opposite-kind replacement").map_err(VaultError::Io)?
                }
                EntryKind::Directory => std::fs::create_dir(path).map_err(VaultError::Io)?,
                EntryKind::Symlink => unreachable!("batch replacement fixtures use file/folder"),
            }
            Ok(())
        } else if fail {
            Err(Self::injected("injected delete failure"))
        } else if fail_after {
            self.inner.delete(relative)?;
            Err(Self::injected("injected post-mutation delete failure"))
        } else {
            self.inner.delete(relative)
        }
    }

    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let pair = (from.to_string(), to.to_string());
        let mut state = self.state.lock().unwrap();
        state.rename_number += 1;
        let rename_number = state.rename_number;
        state
            .calls
            .push(ProviderCall::Rename(pair.0.clone(), pair.1.clone()));
        let fail = state.fail_renames.contains(&pair);
        let fail_after = state.fail_renames_after_mutation.contains(&pair);
        let duplicate_then_fail = state.duplicate_then_fail_renames.contains(&pair);
        let remove_then_fail = state.remove_then_fail_renames.contains(&pair);
        let panic_after = state.panic_after_rename_numbers.contains(&rename_number);
        drop(state);
        if fail {
            Err(Self::injected("injected rename failure"))
        } else if remove_then_fail {
            let path = self.root.join(from);
            if path.is_dir() {
                std::fs::remove_dir_all(path).map_err(VaultError::Io)?;
            } else {
                std::fs::remove_file(path).map_err(VaultError::Io)?;
            }
            Err(Self::injected("injected disappearing rename failure"))
        } else if duplicate_then_fail {
            let bytes = self.inner.read_file(from)?;
            self.inner.write_file_if_absent(to, &bytes)?;
            Err(Self::injected("injected ambiguous rename failure"))
        } else if fail_after {
            self.inner.rename(from, to)?;
            Err(Self::injected("injected post-mutation rename failure"))
        } else {
            self.inner.rename(from, to)?;
            assert!(
                !panic_after,
                "simulated process crash after rename {rename_number}"
            );
            Ok(())
        }
    }

    fn preflight_rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let pair = (from.to_string(), to.to_string());
        let mut state = self.state.lock().unwrap();
        state.calls.push(ProviderCall::PreflightRename(
            pair.0.clone(),
            pair.1.clone(),
        ));
        let fail = state.fail_preflight_renames.contains(&pair);
        drop(state);
        if fail {
            Err(Self::injected("injected rename preflight failure"))
        } else {
            self.inner.preflight_rename(from, to)
        }
    }

    fn preflight_delete(&self, path: &str) -> Result<(), VaultError> {
        let mut state = self.state.lock().unwrap();
        state
            .calls
            .push(ProviderCall::PreflightDelete(path.to_string()));
        let fail = state.fail_preflight_deletes.contains(path);
        drop(state);
        if fail {
            Err(Self::injected("injected delete preflight failure"))
        } else {
            self.inner.preflight_delete(path)
        }
    }

    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }

    fn stat(&self, relative: &str) -> Result<FileStat, VaultError> {
        let mut state = self.state.lock().unwrap();
        if state.pending_stat_failures.remove(relative) {
            return Err(Self::injected("injected stat failure after write"));
        }
        let fail = state.fail_existence_after_delete.contains(relative)
            && state.delete_attempts.contains(relative);
        drop(state);
        if fail {
            return Err(Self::injected(
                "injected post-delete existence probe failure",
            ));
        }
        self.inner.stat(relative)
    }

    fn mutation_path_exists(&self, relative: &str) -> Result<bool, VaultError> {
        let state = self.state.lock().unwrap();
        let fail = state.fail_existence_after_delete.contains(relative)
            && state.delete_attempts.contains(relative);
        drop(state);
        if fail {
            Err(Self::injected(
                "injected post-delete existence probe failure",
            ))
        } else {
            self.inner.mutation_path_exists(relative)
        }
    }

    fn mutation_path_kind(&self, relative: &str) -> Result<Option<EntryKind>, VaultError> {
        let state = self.state.lock().unwrap();
        let fail = state.fail_existence_after_delete.contains(relative)
            && state.delete_attempts.contains(relative);
        drop(state);
        if fail {
            Err(Self::injected(
                "injected post-delete existence probe failure",
            ))
        } else {
            self.inner.mutation_path_kind(relative)
        }
    }

    fn watch(&self, sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

fn write_fixture(root: &std::path::Path, path: &str, contents: &str) {
    let full = root.join(path);
    std::fs::create_dir_all(full.parent().unwrap()).unwrap();
    std::fs::write(full, contents).unwrap();
}

fn fixture(
    files: &[(&str, &str)],
    directories: &[&str],
) -> (tempfile::TempDir, VaultSession, Arc<Mutex<FaultState>>) {
    let tmp = tempfile::tempdir().unwrap();
    for (path, contents) in files {
        write_fixture(tmp.path(), path, contents);
    }
    for path in directories {
        std::fs::create_dir_all(tmp.path().join(path)).unwrap();
    }
    let (provider, state) = FaultInjectingProvider::new(tmp.path().to_path_buf());
    let session = VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate")))
        .expect("open fixture");
    session.scan_initial(&CancelToken::new()).expect("scan");
    state.lock().unwrap().calls.clear();
    (tmp, session, state)
}

fn file(path: &str) -> StructuralBatchItem {
    StructuralBatchItem {
        path: path.to_string(),
        is_directory: false,
    }
}

fn folder(path: &str) -> StructuralBatchItem {
    StructuralBatchItem {
        path: path.to_string(),
        is_directory: true,
    }
}

fn path_markers(session: &VaultSession, path: &str) -> Vec<(String, String)> {
    session
        .read_oplog(path)
        .unwrap()
        .into_iter()
        .flat_map(|entry| {
            if entry.op_kind != crate::oplog::OpKind::Annotated {
                return Vec::new();
            }
            crate::oplog::decode_annotated(&entry.payload_bytes)
                .unwrap()
                .2
                .into_iter()
                .filter_map(|annotation| match annotation {
                    crate::oplog::OpAnnotation::PathChanged { from, to } => Some((from, to)),
                    _ => None,
                })
                .collect()
        })
        .collect()
}

fn shared_filesystem_sessions(
    files: &[(&str, &str)],
    directories: &[&str],
) -> (tempfile::TempDir, Arc<VaultSession>, Arc<VaultSession>) {
    let tmp = tempfile::tempdir().unwrap();
    for (path, contents) in files {
        write_fixture(tmp.path(), path, contents);
    }
    for path in directories {
        std::fs::create_dir_all(tmp.path().join(path)).unwrap();
    }
    let first = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    first.scan_initial(&CancelToken::new()).unwrap();
    let second = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    (tmp, Arc::new(first), Arc::new(second))
}

fn structural_inflight_count(root: &std::path::Path) -> i64 {
    let conn = rusqlite::Connection::open(root.join(".slate/cache.sqlite")).unwrap();
    conn.query_row(
        "SELECT COUNT(*) FROM structural_batch_inflight",
        [],
        |row| row.get(0),
    )
    .unwrap()
}

fn latest_structural_kind(root: &std::path::Path) -> Option<String> {
    let conn = rusqlite::Connection::open(root.join(".slate/cache.sqlite")).unwrap();
    conn.query_row(
        "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
        [],
        |row| row.get(0),
    )
    .optional()
    .unwrap()
}

#[test]
fn two_sessions_serialize_child_and_parent_moves_before_preflight() {
    let (tmp, child_session, folder_session) = shared_filesystem_sessions(
        &[("folder/child.md", "child")],
        &["folder", "child-dest", "folder-dest"],
    );
    let (entered_tx, entered_rx) = mpsc::channel();
    let (release_tx, release_rx) = mpsc::channel();
    let (child_tx, child_rx) = mpsc::channel();
    let moving_child = Arc::clone(&child_session);
    std::thread::spawn(move || {
        let result = moving_child.batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("folder/child.md")],
                new_parent: "child-dest".into(),
            },
            &BlockingBatchFault {
                point: BatchFaultPoint::MoveIndex,
                entered: Mutex::new(Some(entered_tx)),
                release: Mutex::new(release_rx),
            },
        );
        let _ = child_tx.send(result);
    });
    entered_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("child move reached the post-rename barrier");

    let (folder_tx, folder_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let result = folder_session.batch_move(BatchMoveRequest {
            items: vec![folder("folder")],
            new_parent: "folder-dest".into(),
        });
        let _ = folder_tx.send(result);
    });

    let early_folder = folder_rx.recv_timeout(Duration::from_millis(200));
    let serialized = early_folder.is_err();
    release_tx.send(()).unwrap();
    let child_report = child_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("child move completed after release")
        .unwrap();
    let folder_report = match early_folder {
        Ok(result) => result.unwrap(),
        Err(_) => folder_rx
            .recv_timeout(Duration::from_secs(2))
            .expect("folder move completed after child")
            .unwrap(),
    };

    assert!(
        serialized,
        "a second VaultSession for the same vault must wait before taking its preflight snapshot"
    );
    assert_eq!(child_report.state, BatchMoveState::Succeeded);
    assert_eq!(folder_report.state, BatchMoveState::Succeeded);
    assert!(tmp.path().join("child-dest/child.md").is_file());
    assert!(tmp.path().join("folder-dest/folder").is_dir());
    assert!(!tmp.path().join("folder-dest/folder/child.md").exists());
    let indexed: Vec<String> = child_session
        .conn
        .lock()
        .unwrap()
        .prepare("SELECT path FROM files ORDER BY path")
        .unwrap()
        .query_map([], |row| row.get(0))
        .unwrap()
        .collect::<Result<_, _>>()
        .unwrap();
    assert_eq!(indexed, vec!["child-dest/child.md"]);
}

#[test]
fn two_sessions_serialize_batch_and_legacy_folder_move_before_preflight() {
    let (tmp, batch_session, legacy_session) = shared_filesystem_sessions(
        &[("folder/child.md", "child")],
        &["folder", "child-dest", "legacy-dest"],
    );
    let (entered_tx, entered_rx) = mpsc::channel();
    let (release_tx, release_rx) = mpsc::channel();
    let (batch_tx, batch_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let result = batch_session.batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("folder/child.md")],
                new_parent: "child-dest".into(),
            },
            &BlockingBatchFault {
                point: BatchFaultPoint::MoveIndex,
                entered: Mutex::new(Some(entered_tx)),
                release: Mutex::new(release_rx),
            },
        );
        let _ = batch_tx.send(result);
    });
    entered_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("batch reached the post-rename barrier");

    let (legacy_tx, legacy_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let _ = legacy_tx.send(legacy_session.move_folder("folder", "legacy-dest"));
    });
    let early_legacy = legacy_rx.recv_timeout(Duration::from_millis(200));
    let serialized = early_legacy.is_err();
    release_tx.send(()).unwrap();
    assert_eq!(
        batch_rx
            .recv_timeout(Duration::from_secs(2))
            .unwrap()
            .unwrap()
            .state,
        BatchMoveState::Succeeded
    );
    let legacy_report = match early_legacy {
        Ok(report) => report.unwrap(),
        Err(_) => legacy_rx
            .recv_timeout(Duration::from_secs(2))
            .unwrap()
            .unwrap(),
    };

    assert!(
        serialized,
        "legacy structural writers must wait on the same vault-wide gate"
    );
    assert_eq!(
        legacy_report.moved,
        Vec::<(String, String)>::new(),
        "the child moved before the now-empty folder"
    );
    assert!(tmp.path().join("child-dest/child.md").is_file());
    assert!(tmp.path().join("legacy-dest/folder").is_dir());
}

#[test]
fn structural_operations_in_different_vaults_remain_independent() {
    let (_first_tmp, first, _first_state) = fixture(&[("a.md", "a")], &["dest"]);
    let (second_tmp, second, _second_state) = fixture(&[("b.md", "b")], &["dest"]);
    let first = Arc::new(first);
    let (entered_tx, entered_rx) = mpsc::channel();
    let (release_tx, release_rx) = mpsc::channel();
    let moving_first = Arc::clone(&first);
    let (first_tx, first_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let result = moving_first.batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("a.md")],
                new_parent: "dest".into(),
            },
            &BlockingBatchFault {
                point: BatchFaultPoint::MoveIndex,
                entered: Mutex::new(Some(entered_tx)),
                release: Mutex::new(release_rx),
            },
        );
        let _ = first_tx.send(result);
    });
    entered_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("first vault reached its barrier");

    let (second_tx, second_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let _ = second_tx.send(second.batch_move(BatchMoveRequest {
            items: vec![file("b.md")],
            new_parent: "dest".into(),
        }));
    });
    let second_report = second_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("a different vault must not wait on the first vault's sidecar")
        .unwrap();
    assert_eq!(second_report.state, BatchMoveState::Succeeded);
    assert!(second_tmp.path().join("dest/b.md").is_file());

    release_tx.send(()).unwrap();
    assert_eq!(
        first_rx
            .recv_timeout(Duration::from_secs(2))
            .unwrap()
            .unwrap()
            .state,
        BatchMoveState::Succeeded
    );
}

#[test]
fn two_sessions_serialize_conflicting_batch_undo_latest_checks() {
    let (_tmp, first, second) = shared_filesystem_sessions(&[("a.md", "a")], &["dest"]);
    let forward = first
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    let op_id = forward.op_id.unwrap();
    let (entered_tx, entered_rx) = mpsc::channel();
    let (release_tx, release_rx) = mpsc::channel();
    let (first_tx, first_rx) = mpsc::channel();
    let first_undo = Arc::clone(&first);
    std::thread::spawn(move || {
        let result = first_undo.undo_batch_move_with_faults(
            op_id,
            &BlockingBatchFault {
                point: BatchFaultPoint::MoveIndex,
                entered: Mutex::new(Some(entered_tx)),
                release: Mutex::new(release_rx),
            },
        );
        let _ = first_tx.send(result);
    });
    entered_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("first inverse reached the post-rename barrier");

    let (second_tx, second_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let _ = second_tx.send(second.undo_batch_move(op_id));
    });
    let early_second = second_rx.recv_timeout(Duration::from_millis(200));
    let serialized = early_second.is_err();
    release_tx.send(()).unwrap();
    let first_report = first_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("first inverse completed after release")
        .unwrap();
    let second_result = match early_second {
        Ok(result) => result,
        Err(_) => second_rx
            .recv_timeout(Duration::from_secs(2))
            .expect("second inverse completed after the latest row changed"),
    };

    assert!(
        serialized,
        "latest-row validation and the inverse mutation must be one vault-wide critical section"
    );
    assert_eq!(first_report.state, BatchMoveState::Succeeded);
    let error = second_result.expect_err("the old op id is no longer latest");
    assert!(error.to_string().contains("only the latest"));
}

#[test]
fn spawned_process_structural_lock_child() {
    let Ok(root) = std::env::var("SLATE_STRUCTURAL_LOCK_CHILD_ROOT") else {
        return;
    };
    let ready = std::env::var("SLATE_STRUCTURAL_LOCK_CHILD_READY").unwrap();
    // Signal process startup before `open`: open itself takes the same
    // sidecar so it can recover an interrupted batch before exposing a
    // session.
    std::fs::write(ready, b"ready").unwrap();
    let session = VaultSession::from_filesystem(root.into()).unwrap();
    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![folder("folder")],
            new_parent: "folder-dest".into(),
        })
        .unwrap();
    assert_eq!(report.state, BatchMoveState::Succeeded, "{report:?}");
}

#[test]
fn spawned_process_waits_for_same_vault_structural_operation() {
    let tmp = tempfile::tempdir().unwrap();
    write_fixture(tmp.path(), "folder/child.md", "child");
    for path in ["folder", "child-dest", "folder-dest"] {
        std::fs::create_dir_all(tmp.path().join(path)).unwrap();
    }
    let session = Arc::new(VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap());
    session.scan_initial(&CancelToken::new()).unwrap();
    let (entered_tx, entered_rx) = mpsc::channel();
    let (release_tx, release_rx) = mpsc::channel();
    let (move_tx, move_rx) = mpsc::channel();
    let moving = Arc::clone(&session);
    std::thread::spawn(move || {
        let result = moving.batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("folder/child.md")],
                new_parent: "child-dest".into(),
            },
            &BlockingBatchFault {
                point: BatchFaultPoint::MoveIndex,
                entered: Mutex::new(Some(entered_tx)),
                release: Mutex::new(release_rx),
            },
        );
        let _ = move_tx.send(result);
    });
    entered_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("parent process reached the post-rename barrier");

    let ready = tmp.path().join("child-ready");
    let mut child = std::process::Command::new(std::env::current_exe().unwrap())
        .arg("spawned_process_structural_lock_child")
        .arg("--nocapture")
        .env("SLATE_STRUCTURAL_LOCK_CHILD_ROOT", tmp.path())
        .env("SLATE_STRUCTURAL_LOCK_CHILD_READY", &ready)
        .spawn()
        .unwrap();
    let deadline = std::time::Instant::now() + Duration::from_secs(2);
    while !ready.exists() && std::time::Instant::now() < deadline {
        std::thread::sleep(Duration::from_millis(10));
    }
    assert!(
        ready.exists(),
        "child process reached the shared-vault open"
    );
    std::thread::sleep(Duration::from_millis(150));
    let serialized = child.try_wait().unwrap().is_none();

    release_tx.send(()).unwrap();
    let parent_report = move_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("parent move completed after release")
        .unwrap();
    let status = child.wait().unwrap();
    assert!(status.success());
    assert!(
        serialized,
        "a separately spawned process must wait on the same vault-scoped structural lock"
    );
    assert_eq!(parent_report.state, BatchMoveState::Succeeded);
    assert!(tmp.path().join("child-dest/child.md").is_file());
    assert!(tmp.path().join("folder-dest/folder").is_dir());
}

#[test]
fn crash_after_first_batch_rename_is_durably_recovered_on_reopen() {
    let (tmp, session, state) = fixture(&[("a.md", "a")], &["dest"]);
    let provider = Arc::clone(&session.provider);
    state.lock().unwrap().panic_after_rename_numbers.insert(1);

    let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        });
    }));
    assert!(crashed.is_err(), "fault hook simulates process termination");
    assert!(tmp.path().join("dest/a.md").is_file());
    assert_eq!(
        structural_inflight_count(tmp.path()),
        1,
        "the full recovery plan must predate the first physical rename"
    );
    drop(session);

    let reopened = VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate")))
        .expect("reopen rolls the interrupted batch back under the vault lock");
    assert!(tmp.path().join("a.md").is_file());
    assert!(!tmp.path().join("dest/a.md").exists());
    assert_eq!(structural_inflight_count(tmp.path()), 0);
    let indexed: String = reopened
        .conn
        .lock()
        .unwrap()
        .query_row("SELECT path FROM files", [], |row| row.get(0))
        .unwrap();
    assert_eq!(indexed, "a.md");
    assert_eq!(latest_structural_kind(tmp.path()), None);
}

#[test]
fn caught_crash_intent_blocks_all_further_structural_writers_until_reopen() {
    let (_tmp, session, state) = fixture(&[("a.md", "a"), ("keep.md", "keep")], &["dest"]);
    state.lock().unwrap().panic_after_rename_numbers.insert(1);
    let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        });
    }));
    assert!(crashed.is_err());

    for error in [
        session.create_folder("must-wait").unwrap_err(),
        session.move_file("keep.md", "dest").unwrap_err(),
        session
            .batch_trash(BatchTrashRequest {
                items: vec![file("keep.md")],
            })
            .unwrap_err(),
    ] {
        assert!(
            error.to_string().contains("close-and-reopen recovery"),
            "unexpected structural writer error: {error}"
        );
    }
}

#[test]
fn crash_recovery_restores_multiple_renames_in_reverse_order() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b")], &["dest"]);
    let provider = Arc::clone(&session.provider);
    state.lock().unwrap().panic_after_rename_numbers.insert(2);
    let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.batch_move(BatchMoveRequest {
            items: vec![file("a.md"), file("b.md")],
            new_parent: "dest".into(),
        });
    }));
    assert!(crashed.is_err());
    assert!(tmp.path().join("dest/a.md").is_file());
    assert!(tmp.path().join("dest/b.md").is_file());
    drop(session);
    state.lock().unwrap().calls.clear();

    let _reopened =
        VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate"))).unwrap();
    let recovery_renames = state
        .lock()
        .unwrap()
        .calls
        .iter()
        .filter_map(|call| match call {
            ProviderCall::Rename(from, to) => Some((from.as_str(), to.as_str())),
            _ => None,
        })
        .map(|(from, to)| (from.to_string(), to.to_string()))
        .collect::<Vec<_>>();
    assert_eq!(
        recovery_renames,
        vec![
            ("dest/b.md".into(), "b.md".into()),
            ("dest/a.md".into(), "a.md".into()),
        ]
    );
    assert_eq!(structural_inflight_count(tmp.path()), 0);
}

#[test]
fn crash_after_index_and_link_commit_restores_paths_index_links_and_history() {
    let (tmp, session, _state) = fixture(
        &[("left/a.md", "# A\n"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    // A PathChanged marker is meaningful only for a file that already has
    // content history; seed that ordinary production precondition.
    session.save_text("left/a.md", "# A\n", None).unwrap();
    let provider = Arc::clone(&session.provider);
    let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("left/a.md")],
                new_parent: "dest".into(),
            },
            &PanicBatchFault(BatchFaultPoint::MoveJournal),
        );
    }));
    assert!(crashed.is_err());
    assert!(tmp.path().join("dest/a.md").is_file());
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("refs.md")).unwrap(),
        "[[dest/a]]\n"
    );
    assert_eq!(structural_inflight_count(tmp.path()), 1);
    drop(session);

    let reopened = VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate")))
        .expect("late interrupted batch is rolled back completely");
    assert!(tmp.path().join("left/a.md").is_file());
    assert!(!tmp.path().join("dest/a.md").exists());
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("refs.md")).unwrap(),
        "[[left/a]]\n"
    );
    let indexed: Vec<String> = reopened
        .conn
        .lock()
        .unwrap()
        .prepare("SELECT path FROM files ORDER BY path")
        .unwrap()
        .query_map([], |row| row.get(0))
        .unwrap()
        .collect::<Result<_, _>>()
        .unwrap();
    assert_eq!(indexed, vec!["left/a.md", "refs.md"]);
    assert_eq!(
        path_markers(&reopened, "left/a.md"),
        vec![
            ("left/a.md".into(), "dest/a.md".into()),
            ("dest/a.md".into(), "left/a.md".into()),
        ]
    );
    assert_eq!(structural_inflight_count(tmp.path()), 0);
    assert_eq!(latest_structural_kind(tmp.path()), None);
}

#[test]
fn crash_immediately_after_canvas_rewrite_write_restores_byte_exact_and_index_truth() {
    let canvas = concat!(
        "{\"nodes\":[{\"id\":\"n\",\"type\":\"file\",",
        "\"file\":\"left/a.md\",\"x\":0,\"y\":0,\"width\":100,\"height\":100}],",
        "\"edges\":[]}\n"
    );
    let (tmp, session, _state) = fixture(
        &[("left/a.md", "# A\n"), ("board.canvas", canvas)],
        &["left", "dest"],
    );
    let original = std::fs::read(tmp.path().join("board.canvas")).unwrap();
    let provider = Arc::clone(&session.provider);
    let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("left/a.md")],
                new_parent: "dest".into(),
            },
            &PanicBatchFault(BatchFaultPoint::MoveLinkRewritePostWrite),
        );
    }));
    assert!(crashed.is_err());
    assert!(
        std::fs::read_to_string(tmp.path().join("board.canvas"))
            .unwrap()
            .contains("\"file\":\"dest/a.md\"")
    );
    assert_eq!(structural_inflight_count(tmp.path()), 1);
    drop(session);

    let reopened = VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate")))
        .expect("the verified canvas preimage restores on reopen");
    assert_eq!(
        std::fs::read(tmp.path().join("board.canvas")).unwrap(),
        original
    );
    assert!(tmp.path().join("left/a.md").is_file());
    assert!(!tmp.path().join("dest/a.md").exists());
    let target: String = reopened
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT target FROM canvas_nodes WHERE node_id = 'n'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(target, "left/a.md");
    assert_eq!(structural_inflight_count(tmp.path()), 0);
    assert_eq!(latest_structural_kind(tmp.path()), None);
}

#[test]
fn crash_after_rewriting_a_moved_document_restores_its_original_path_and_bytes() {
    let original = b"[[left/b]]\n".to_vec();
    let (tmp, session, _state) = fixture(
        &[("left/a.md", "[[left/b]]\n"), ("left/b.md", "# B\n")],
        &["left", "dest"],
    );
    let provider = Arc::clone(&session.provider);
    let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.batch_move_with_faults(
            BatchMoveRequest {
                items: vec![folder("left")],
                new_parent: "dest".into(),
            },
            &PanicBatchFault(BatchFaultPoint::MoveLinkRewritePostWrite),
        );
    }));
    assert!(crashed.is_err());
    assert_ne!(
        std::fs::read(tmp.path().join("dest/left/a.md")).unwrap(),
        original,
        "the fault must land after the moved source itself was rewritten"
    );
    drop(session);

    let reopened = VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate")))
        .expect("reopen restores a rewritten moved source before reversing its path");
    assert_eq!(
        std::fs::read(tmp.path().join("left/a.md")).unwrap(),
        original
    );
    assert!(!tmp.path().join("dest/left").exists());
    let target: Option<String> = reopened
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT l.target_path FROM links l JOIN files f ON f.id = l.source_file_id
             WHERE f.path = 'left/a.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(target.as_deref(), Some("left/b.md"));
    assert_eq!(structural_inflight_count(tmp.path()), 0);
}

#[test]
fn successful_batch_finalizes_one_undo_row_and_no_inflight_residue() {
    let (tmp, session, _state) = fixture(&[("a.md", "a"), ("b.md", "b")], &["dest"]);
    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md"), file("b.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    assert_eq!(report.state, BatchMoveState::Succeeded);
    assert_eq!(structural_inflight_count(tmp.path()), 0);
    let rows: i64 = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT COUNT(*) FROM structural_ops WHERE kind = 'move_batch'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(rows, 1);
}

#[test]
fn ambiguous_crash_topology_appends_barrier_and_fails_reopen_closed() {
    for topology in ["both", "neither"] {
        let (tmp, session, state) = fixture(&[("a.md", "a")], &["dest"]);
        let provider = Arc::clone(&session.provider);
        state.lock().unwrap().panic_after_rename_numbers.insert(1);
        let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
            let _ = session.batch_move(BatchMoveRequest {
                items: vec![file("a.md")],
                new_parent: "dest".into(),
            });
        }));
        assert!(crashed.is_err());
        drop(session);
        if topology == "both" {
            std::fs::write(tmp.path().join("a.md"), "external replacement").unwrap();
        } else {
            std::fs::remove_file(tmp.path().join("dest/a.md")).unwrap();
        }

        let reopened = VaultSession::open(
            Arc::clone(&provider),
            SessionConfig::new(tmp.path().join(".slate")),
        );
        let error = match reopened {
            Ok(_) => panic!("{topology} topology must not be guessed"),
            Err(error) => error,
        };
        assert!(error.to_string().contains("structural batch recovery"));
        assert_eq!(
            latest_structural_kind(tmp.path()).as_deref(),
            Some("recovery_barrier")
        );
        assert_eq!(structural_inflight_count(tmp.path()), 0);
    }
}

#[test]
fn failed_crash_rollback_appends_barrier_and_surfaces_reopen_error() {
    let (tmp, session, state) = fixture(&[("a.md", "a")], &["dest"]);
    let provider = Arc::clone(&session.provider);
    state.lock().unwrap().panic_after_rename_numbers.insert(1);
    let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        });
    }));
    assert!(crashed.is_err());
    drop(session);
    state
        .lock()
        .unwrap()
        .fail_renames
        .insert(("dest/a.md".into(), "a.md".into()));

    let reopened = VaultSession::open(provider, SessionConfig::new(tmp.path().join(".slate")));
    let error = match reopened {
        Ok(_) => panic!("a failed rollback cannot be hidden by a successful reopen"),
        Err(error) => error,
    };
    assert!(error.to_string().contains("structural batch recovery"));
    assert_eq!(
        latest_structural_kind(tmp.path()).as_deref(),
        Some("recovery_barrier")
    );
    assert_eq!(structural_inflight_count(tmp.path()), 0);
    assert!(tmp.path().join("dest/a.md").is_file());
}

#[test]
fn batch_item_limit_rejects_before_provider_access() {
    let (_tmp, session, state) = fixture(&[("a.md", "a")], &[]);
    let report = session
        .batch_move(BatchMoveRequest {
            items: (0..=MAX_STRUCTURAL_BATCH_ITEMS)
                .map(|index| file(&format!("{index}.md")))
                .collect(),
            new_parent: String::new(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::Rejected);
    assert_eq!(report.envelope.preflight_failures.len(), 1);
    assert_eq!(report.envelope.preflight_failures[0].item, None);
    assert!(state.lock().unwrap().calls.is_empty());
}

#[test]
fn empty_batch_is_request_failure_without_an_invented_item() {
    let (_tmp, session, state) = fixture(&[("a.md", "a")], &[]);
    let report = session
        .batch_trash(BatchTrashRequest { items: Vec::new() })
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Rejected);
    assert_eq!(report.envelope.preflight_failures.len(), 1);
    assert_eq!(report.envelope.preflight_failures[0].item, None);
    assert!(state.lock().unwrap().calls.is_empty());
}

#[test]
fn all_already_direct_children_is_noop_without_a_journal_row() {
    let (_tmp, session, state) = fixture(&[("dest/a.md", "a")], &["dest"]);
    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("dest/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::NoOp);
    assert!(report.envelope.planned.is_empty());
    assert_eq!(report.envelope.skipped.len(), 1);
    assert_eq!(report.op_id, None);
    assert!(state.lock().unwrap().calls.is_empty());
    let rows: i64 = session
        .conn
        .lock()
        .unwrap()
        .query_row("SELECT COUNT(*) FROM structural_ops", [], |row| row.get(0))
        .unwrap();
    assert_eq!(rows, 0);
}

#[test]
fn batch_move_existing_case_insensitive_collision_mutates_nothing() {
    let (tmp, session, state) = fixture(
        &[("source/a.md", "a"), ("dest/A.md", "occupied")],
        &["source", "dest"],
    );
    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("source/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::Rejected);
    assert!(!report.envelope.preflight_failures.is_empty());
    assert!(tmp.path().join("source/a.md").is_file());
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("dest/A.md")).unwrap(),
        "occupied"
    );
    assert_eq!(
        state
            .lock()
            .unwrap()
            .calls
            .iter()
            .filter(|call| matches!(call, ProviderCall::PreflightRename(_, _)))
            .count(),
        1
    );
    assert!(
        state
            .lock()
            .unwrap()
            .calls
            .iter()
            .all(|call| !matches!(call, ProviderCall::Rename(_, _)))
    );
}

#[test]
fn batch_move_intra_plan_case_insensitive_collision_mutates_nothing() {
    let (tmp, session, state) = fixture(
        &[("a/Foo.md", "upper"), ("b/foo.md", "lower")],
        &["a", "b", "dest"],
    );
    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("b/foo.md"), file("a/Foo.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::Rejected);
    assert_eq!(report.envelope.preflight_failures.len(), 2);
    assert!(tmp.path().join("a/Foo.md").is_file());
    assert!(tmp.path().join("b/foo.md").is_file());
    assert_eq!(
        state
            .lock()
            .unwrap()
            .calls
            .iter()
            .filter(|call| matches!(call, ProviderCall::PreflightRename(_, _)))
            .count(),
        2
    );
    assert!(
        state
            .lock()
            .unwrap()
            .calls
            .iter()
            .all(|call| !matches!(call, ProviderCall::Rename(_, _)))
    );
}

#[test]
fn batch_move_subtree_and_invalid_parent_fail_before_first_rename() {
    let (tmp, session, state) = fixture(&[("folder/a.md", "a")], &["folder"]);
    let subtree = session
        .batch_move(BatchMoveRequest {
            items: vec![folder("folder")],
            new_parent: "folder/child".into(),
        })
        .unwrap();
    let invalid_parent = session
        .batch_move(BatchMoveRequest {
            items: vec![file("folder/a.md")],
            new_parent: "missing".into(),
        })
        .unwrap();

    assert_eq!(subtree.state, BatchMoveState::Rejected);
    assert_eq!(invalid_parent.state, BatchMoveState::Rejected);
    assert_eq!(
        state
            .lock()
            .unwrap()
            .calls
            .iter()
            .filter(|call| matches!(call, ProviderCall::PreflightRename(_, _)))
            .count(),
        2,
        "both otherwise-addressable rejected items are still permission-probed"
    );
    assert!(tmp.path().join("folder/a.md").is_file());
    assert!(
        state
            .lock()
            .unwrap()
            .calls
            .iter()
            .all(|call| !matches!(call, ProviderCall::Rename(_, _)))
    );
}

#[test]
fn batch_move_folder_into_itself_is_rejected_after_complete_preflight() {
    let (tmp, session, state) = fixture(&[("dest/a.md", "a")], &["dest"]);

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![folder("dest")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::Rejected);
    assert!(
        report
            .envelope
            .preflight_failures
            .iter()
            .any(|failure| failure.message.contains("own subtree"))
    );
    let calls = &state.lock().unwrap().calls;
    assert_eq!(
        calls
            .iter()
            .filter(|call| matches!(call, ProviderCall::PreflightRename(_, _)))
            .count(),
        1
    );
    assert!(
        !calls
            .iter()
            .any(|call| matches!(call, ProviderCall::Rename(_, _)))
    );
    assert!(tmp.path().join("dest/a.md").is_file());
    assert!(!tmp.path().join("dest/dest").exists());
}

#[test]
fn stale_index_file_replaced_by_directory_rejects_move_and_trash_without_mutation() {
    let (tmp, session, state) = fixture(&[("a.md", "a")], &["dest"]);
    std::fs::remove_file(tmp.path().join("a.md")).unwrap();
    std::fs::create_dir(tmp.path().join("a.md")).unwrap();

    let moved = session
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    let trashed = session
        .batch_trash(BatchTrashRequest {
            items: vec![file("a.md")],
        })
        .unwrap();

    for report in [&moved.envelope, &trashed.envelope] {
        assert!(
            report
                .preflight_failures
                .iter()
                .any(|failure| failure.message.contains("kind changed on disk"))
        );
    }
    assert_eq!(moved.state, BatchMoveState::Rejected);
    assert_eq!(trashed.state, BatchTrashState::Rejected);
    assert!(tmp.path().join("a.md").is_dir());
    assert!(!tmp.path().join("dest/a.md").exists());
    let calls = &state.lock().unwrap().calls;
    assert!(
        !calls
            .iter()
            .any(|call| { matches!(call, ProviderCall::Rename(_, _) | ProviderCall::Delete(_)) })
    );
}

#[test]
fn batch_move_permission_probe_failure_reports_all_and_mutates_nothing() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b"), ("c.md", "c")], &["dest"]);
    state
        .lock()
        .unwrap()
        .fail_preflight_renames
        .insert(("b.md".into(), "dest/b.md".into()));

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("c.md"), file("a.md"), file("b.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::Rejected);
    assert_eq!(report.envelope.preflight_failures.len(), 1);
    let calls = &state.lock().unwrap().calls;
    assert_eq!(
        calls
            .iter()
            .filter(|call| matches!(call, ProviderCall::PreflightRename(_, _)))
            .count(),
        3
    );
    assert!(
        calls
            .iter()
            .all(|call| !matches!(call, ProviderCall::Rename(_, _)))
    );
    assert!(tmp.path().join("a.md").is_file());
    assert!(tmp.path().join("b.md").is_file());
    assert!(tmp.path().join("c.md").is_file());
}

#[test]
fn batch_trash_permission_probe_failure_calls_system_trash_zero_times() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b")], &[]);
    state
        .lock()
        .unwrap()
        .fail_preflight_deletes
        .insert("a.md".into());

    let report = session
        .batch_trash(BatchTrashRequest {
            items: vec![file("b.md"), file("a.md")],
        })
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Rejected);
    assert_eq!(report.envelope.preflight_failures.len(), 1);
    let calls = &state.lock().unwrap().calls;
    assert_eq!(
        calls
            .iter()
            .filter(|call| matches!(call, ProviderCall::PreflightDelete(_)))
            .count(),
        2
    );
    assert!(
        calls
            .iter()
            .all(|call| !matches!(call, ProviderCall::Delete(_)))
    );
    assert!(tmp.path().join("a.md").is_file());
    assert!(tmp.path().join("b.md").is_file());
}

#[test]
fn batch_move_mixed_file_folder_succeeds_in_deterministic_order_and_one_row() {
    let (tmp, session, state) = fixture(
        &[("z.md", "z"), ("folder/child.md", "child")],
        &["folder", "dest"],
    );
    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("z.md"), folder("folder")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::Succeeded);
    assert!(report.op_id.is_some());
    assert_eq!(
        report
            .standing
            .iter()
            .map(|change| (&change.old_path, &change.new_path, change.is_directory))
            .collect::<Vec<_>>(),
        vec![
            (&"folder".to_string(), &"dest/folder".to_string(), true),
            (&"z.md".to_string(), &"dest/z.md".to_string(), false),
        ]
    );
    assert!(tmp.path().join("dest/folder/child.md").is_file());
    assert!(tmp.path().join("dest/z.md").is_file());
    let calls = &state.lock().unwrap().calls;
    assert_eq!(
        calls
            .iter()
            .filter_map(|call| match call {
                ProviderCall::Rename(from, to) => Some((from.as_str(), to.as_str())),
                _ => None,
            })
            .collect::<Vec<_>>(),
        vec![("folder", "dest/folder"), ("z.md", "dest/z.md")]
    );
    let conn = session.conn.lock().unwrap();
    let rows: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM structural_ops WHERE kind = 'move_batch'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(rows, 1);
    let indexed: Vec<String> = {
        let mut stmt = conn
            .prepare("SELECT path FROM files ORDER BY path")
            .unwrap();
        stmt.query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap()
    };
    assert_eq!(indexed, vec!["dest/folder/child.md", "dest/z.md"]);
}

#[test]
fn batch_move_runtime_failure_rolls_back_in_reverse_order() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b"), ("c.md", "c")], &["dest"]);
    state
        .lock()
        .unwrap()
        .fail_renames
        .insert(("c.md".into(), "dest/c.md".into()));

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("c.md"), file("a.md"), file("b.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RolledBack);
    assert!(report.standing.is_empty());
    assert_eq!(
        report
            .rolled_back
            .iter()
            .map(|change| change.old_path.as_str())
            .collect::<Vec<_>>(),
        vec!["a.md", "b.md"]
    );
    assert!(tmp.path().join("a.md").is_file());
    assert!(tmp.path().join("b.md").is_file());
    let rename_calls = state
        .lock()
        .unwrap()
        .calls
        .iter()
        .filter_map(|call| match call {
            ProviderCall::Rename(from, to) => Some((from.clone(), to.clone())),
            _ => None,
        })
        .collect::<Vec<_>>();
    assert_eq!(
        rename_calls,
        vec![
            ("a.md".into(), "dest/a.md".into()),
            ("b.md".into(), "dest/b.md".into()),
            ("c.md".into(), "dest/c.md".into()),
            ("dest/b.md".into(), "b.md".into()),
            ("dest/a.md".into(), "a.md".into()),
        ]
    );
}

#[test]
fn batch_move_forward_error_after_mutation_is_detected_and_rolled_back() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b")], &["dest"]);
    state
        .lock()
        .unwrap()
        .fail_renames_after_mutation
        .insert(("b.md".into(), "dest/b.md".into()));

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md"), file("b.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RolledBack);
    assert!(report.standing.is_empty());
    assert_eq!(
        report
            .rolled_back
            .iter()
            .map(|change| change.old_path.as_str())
            .collect::<Vec<_>>(),
        vec!["a.md", "b.md"]
    );
    assert!(tmp.path().join("a.md").is_file());
    assert!(tmp.path().join("b.md").is_file());
    assert!(!tmp.path().join("dest/b.md").exists());
}

#[test]
fn batch_move_ambiguous_physical_topology_requires_rescan() {
    let (tmp, session, state) = fixture(&[("a.md", "a")], &["dest"]);
    let _ = session.with_graph(|graph| graph.canonical_edges()).unwrap();
    let graph_generation_before = session.graph_generation();
    let bases_generation_before = session.bases_generation();
    state
        .lock()
        .unwrap()
        .duplicate_then_fail_renames
        .insert(("a.md".into(), "dest/a.md".into()));

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert!(report.requires_rescan);
    assert!(tmp.path().join("a.md").is_file());
    assert!(tmp.path().join("dest/a.md").is_file());
    assert!(report.standing.is_empty());
    assert!(report.rolled_back.is_empty());
    assert!(!session.graph_is_built());
    assert!(session.graph_generation() > graph_generation_before);
    assert!(session.bases_generation() > bases_generation_before);
}

#[test]
fn batch_move_unlocatable_topology_drops_graph_and_bumps_refresh_generation() {
    let (_tmp, session, state) = fixture(&[("a.md", "a")], &["dest"]);
    let _ = session.with_graph(|graph| graph.canonical_edges()).unwrap();
    let graph_generation_before = session.graph_generation();
    let bases_generation_before = session.bases_generation();
    state
        .lock()
        .unwrap()
        .remove_then_fail_renames
        .insert(("a.md".into(), "dest/a.md".into()));

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert!(report.requires_rescan);
    assert!(report.standing.is_empty());
    assert!(report.rolled_back.is_empty());
    assert!(!session.graph_is_built());
    assert!(session.graph_generation() > graph_generation_before);
    assert!(session.bases_generation() > bases_generation_before);
}

#[test]
fn batch_move_rollback_failure_reconciles_exact_mixed_topology_and_barrier() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b"), ("c.md", "c")], &["dest"]);
    {
        let mut state = state.lock().unwrap();
        state
            .fail_renames
            .insert(("c.md".into(), "dest/c.md".into()));
        state
            .fail_renames
            .insert(("dest/a.md".into(), "a.md".into()));
    }

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("c.md"), file("b.md"), file("a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert_eq!(
        report
            .standing
            .iter()
            .map(|change| change.old_path.as_str())
            .collect::<Vec<_>>(),
        vec!["a.md"]
    );
    assert_eq!(
        report
            .rolled_back
            .iter()
            .map(|change| change.old_path.as_str())
            .collect::<Vec<_>>(),
        vec!["b.md"]
    );
    assert!(!report.requires_rescan);
    assert!(tmp.path().join("dest/a.md").is_file());
    assert!(tmp.path().join("b.md").is_file());
    let conn = session.conn.lock().unwrap();
    let indexed: Vec<String> = {
        let mut stmt = conn
            .prepare("SELECT path FROM files ORDER BY path")
            .unwrap();
        stmt.query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap()
    };
    assert_eq!(indexed, vec!["b.md", "c.md", "dest/a.md"]);
    let latest: String = conn
        .query_row(
            "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(latest, "recovery_barrier");
}

#[test]
fn batch_move_index_failure_rolls_back_filesystem_and_index() {
    let (tmp, session, _state) = fixture(&[("a.md", "a"), ("b.md", "b")], &["dest"]);
    let report = session
        .batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("b.md"), file("a.md")],
                new_parent: "dest".into(),
            },
            &TestBatchFaults::at(BatchFaultPoint::MoveIndex),
        )
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RolledBack);
    assert_eq!(
        report.failure.as_ref().unwrap().stage,
        crate::BatchFailureStage::Index
    );
    assert!(tmp.path().join("a.md").is_file());
    assert!(tmp.path().join("b.md").is_file());
    let conn = session.conn.lock().unwrap();
    let indexed: Vec<String> = {
        let mut stmt = conn
            .prepare("SELECT path FROM files ORDER BY path")
            .unwrap();
        stmt.query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap()
    };
    assert_eq!(indexed, vec!["a.md", "b.md"]);
}

#[test]
fn batch_move_journal_failure_restores_paths_and_rewritten_bytes() {
    let (tmp, session, _state) = fixture(
        &[("left/a.md", "# A"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    let report = session
        .batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("left/a.md")],
                new_parent: "dest".into(),
            },
            &TestBatchFaults::at(BatchFaultPoint::MoveJournal),
        )
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RolledBack);
    assert!(tmp.path().join("left/a.md").is_file());
    assert!(!tmp.path().join("dest/a.md").exists());
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("refs.md")).unwrap(),
        "[[left/a]]\n"
    );
    let conn = session.conn.lock().unwrap();
    let rows: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM structural_ops WHERE kind IN ('move_batch', 'recovery_barrier')",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(rows, 0);
}

#[test]
fn batch_move_landed_markdown_rewrite_is_journaled_and_undoable_after_save_error() {
    let (tmp, session, state) = fixture(
        &[("left/a.md", "# A\n"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    let original = std::fs::read(tmp.path().join("refs.md")).unwrap();
    let _ = session.with_graph(|graph| graph.canonical_edges()).unwrap();
    state
        .lock()
        .unwrap()
        .fail_stat_after_write
        .insert("refs.md".into(), 1);

    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("left/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(forward.state, BatchMoveState::Succeeded);
    assert!(forward.requires_rescan);
    assert!(
        forward
            .rewritten
            .iter()
            .any(|rewrite| rewrite.path == "refs.md")
    );
    assert!(
        forward
            .rewrite_failures
            .iter()
            .any(|failure| failure.path == "refs.md")
    );
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("refs.md")).unwrap(),
        "[[dest/a]]\n"
    );
    let forward_bytes = std::fs::read(tmp.path().join("refs.md")).unwrap();
    assert!(!session.graph_is_built());

    let undo = session.undo_batch_move(forward.op_id.unwrap()).unwrap();
    assert_eq!(undo.state, BatchMoveState::Succeeded);
    assert_eq!(std::fs::read(tmp.path().join("refs.md")).unwrap(), original);
    let redo = session.undo_batch_move(undo.op_id.unwrap()).unwrap();
    assert_eq!(redo.state, BatchMoveState::Succeeded);
    assert_eq!(
        std::fs::read(tmp.path().join("refs.md")).unwrap(),
        forward_bytes
    );
}

#[test]
fn batch_move_landed_canvas_rewrite_is_journaled_and_undoable_after_save_error() {
    let canvas = concat!(
        "{\"nodes\":[{\"id\":\"n\",\"type\":\"file\",",
        "\"file\":\"left/a.md\",\"x\":0,\"y\":0,\"width\":100,\"height\":100}],",
        "\"edges\":[]}\n"
    );
    let (tmp, session, state) = fixture(
        &[("left/a.md", "# A\n"), ("board.canvas", canvas)],
        &["left", "dest"],
    );
    let original = std::fs::read(tmp.path().join("board.canvas")).unwrap();
    state
        .lock()
        .unwrap()
        .fail_stat_after_write
        .insert("board.canvas".into(), 1);

    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("left/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(forward.state, BatchMoveState::Succeeded);
    assert!(forward.requires_rescan);
    assert!(
        forward
            .rewritten
            .iter()
            .any(|rewrite| rewrite.path == "board.canvas")
    );
    assert!(
        forward
            .rewrite_failures
            .iter()
            .any(|failure| failure.path == "board.canvas")
    );
    assert!(
        std::fs::read_to_string(tmp.path().join("board.canvas"))
            .unwrap()
            .contains("\"file\":\"dest/a.md\"")
    );
    let forward_bytes = std::fs::read(tmp.path().join("board.canvas")).unwrap();

    let undo = session.undo_batch_move(forward.op_id.unwrap()).unwrap();
    assert_eq!(undo.state, BatchMoveState::Succeeded);
    assert_eq!(
        std::fs::read(tmp.path().join("board.canvas")).unwrap(),
        original
    );
    let redo = session.undo_batch_move(undo.op_id.unwrap()).unwrap();
    assert_eq!(redo.state, BatchMoveState::Succeeded);
    assert_eq!(
        std::fs::read(tmp.path().join("board.canvas")).unwrap(),
        forward_bytes
    );
}

#[test]
fn batch_undo_landed_restore_is_journaled_and_redo_restores_forward_bytes() {
    let (tmp, session, state) = fixture(
        &[("left/a.md", "# A\n"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    let original = std::fs::read(tmp.path().join("refs.md")).unwrap();
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("left/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    let forward_bytes = std::fs::read(tmp.path().join("refs.md")).unwrap();
    state
        .lock()
        .unwrap()
        .fail_stat_after_write
        .insert("refs.md".into(), 1);

    let undo = session.undo_batch_move(forward.op_id.unwrap()).unwrap();
    assert_eq!(undo.state, BatchMoveState::Succeeded);
    assert!(undo.requires_rescan);
    assert!(
        undo.rewritten
            .iter()
            .any(|rewrite| rewrite.path == "refs.md")
    );
    assert!(
        undo.rewrite_failures
            .iter()
            .any(|failure| failure.path == "refs.md")
    );
    assert_eq!(std::fs::read(tmp.path().join("refs.md")).unwrap(), original);

    let redo = session.undo_batch_move(undo.op_id.unwrap()).unwrap();
    assert_eq!(redo.state, BatchMoveState::Succeeded);
    assert_eq!(
        std::fs::read(tmp.path().join("refs.md")).unwrap(),
        forward_bytes
    );
}

#[test]
fn batch_undo_unknown_restore_is_barriered_without_an_inverse_move_row() {
    let (_tmp, session, state) = fixture(
        &[("left/a.md", "# A\n"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("left/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    {
        let mut state = state.lock().unwrap();
        state.fail_stat_after_write.insert("refs.md".into(), 1);
        state.fail_read_after_write.insert("refs.md".into(), 1);
    }

    let undo = session.undo_batch_move(forward.op_id.unwrap()).unwrap();
    assert_eq!(undo.state, BatchMoveState::RollbackIncomplete);
    assert!(undo.op_id.is_none());
    assert!(undo.requires_rescan);
    let conn = session.conn.lock().unwrap();
    let move_rows: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM structural_ops WHERE kind = 'move_batch'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(move_rows, 1, "only the original forward move is journaled");
    let latest: String = conn
        .query_row(
            "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(latest, "recovery_barrier");
}

#[test]
fn batch_undo_journal_recovery_classifies_landed_restore_and_keeps_forward_bytes() {
    let (tmp, session, state) = fixture(
        &[("left/a.md", "# A\n"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("left/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    let forward_bytes = std::fs::read(tmp.path().join("refs.md")).unwrap();
    state
        .lock()
        .unwrap()
        .fail_stat_after_write
        .insert("refs.md".into(), 2);

    let report = session
        .undo_batch_move_with_faults(
            forward.op_id.unwrap(),
            &TestBatchFaults::at(BatchFaultPoint::MoveJournal),
        )
        .unwrap();
    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert!(report.requires_rescan);
    assert!(tmp.path().join("dest/a.md").is_file());
    assert_eq!(
        std::fs::read(tmp.path().join("refs.md")).unwrap(),
        forward_bytes
    );
    assert!(!session.graph_is_built());
}

#[test]
fn batch_undo_journal_recovery_unknown_restore_is_barriered() {
    let (tmp, session, state) = fixture(
        &[("left/a.md", "# A\n"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("left/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    let forward_bytes = std::fs::read(tmp.path().join("refs.md")).unwrap();
    {
        let mut state = state.lock().unwrap();
        state.fail_stat_after_write.insert("refs.md".into(), 2);
        state.fail_read_after_write.insert("refs.md".into(), 2);
    }

    let report = session
        .undo_batch_move_with_faults(
            forward.op_id.unwrap(),
            &TestBatchFaults::at(BatchFaultPoint::MoveJournal),
        )
        .unwrap();
    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert!(report.requires_rescan);
    assert!(tmp.path().join("dest/a.md").is_file());
    assert_eq!(
        std::fs::read(tmp.path().join("refs.md")).unwrap(),
        forward_bytes
    );
    let latest: String = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(latest, "recovery_barrier");
}

#[test]
fn batch_move_journal_mixed_recovery_preserves_forward_rescan_requirement() {
    let (_tmp, session, state) = fixture(
        &[
            ("left/a.md", "# A\n"),
            ("right/b.md", "# B\n"),
            ("refs.md", "[[right/b]]\n"),
        ],
        &["left", "right", "dest"],
    );
    let _ = session.with_graph(|graph| graph.canonical_edges()).unwrap();
    {
        let mut state = state.lock().unwrap();
        state.fail_stat_after_write.insert("refs.md".into(), 1);
        state
            .fail_renames
            .insert(("dest/b.md".into(), "right/b.md".into()));
    }

    let report = session
        .batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("left/a.md"), file("right/b.md")],
                new_parent: "dest".into(),
            },
            &TestBatchFaults::at(BatchFaultPoint::MoveJournal),
        )
        .unwrap();
    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert!(report.requires_rescan);
    assert_eq!(report.standing[0].old_path, "right/b.md");
    assert!(!session.graph_is_built());
}

#[test]
fn batch_move_unknown_post_write_bytes_are_barriered_not_journaled_as_undoable() {
    let (tmp, session, state) = fixture(
        &[("left/a.md", "# A\n"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    {
        let mut state = state.lock().unwrap();
        state.fail_stat_after_write.insert("refs.md".into(), 1);
        state.fail_read_after_write.insert("refs.md".into(), 1);
    }

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("left/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert!(report.op_id.is_none());
    assert!(report.requires_rescan);
    assert!(tmp.path().join("left/a.md").is_file());
    assert!(
        report
            .rewrite_failures
            .iter()
            .any(|failure| failure.path == "refs.md")
    );
    let conn = session.conn.lock().unwrap();
    let move_rows: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM structural_ops WHERE kind = 'move_batch'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(move_rows, 0);
    let latest: String = conn
        .query_row(
            "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(latest, "recovery_barrier");
}

#[test]
fn legacy_unknown_rewrite_and_failed_barrier_invalidates_structural_history() {
    let (_tmp, session, state) = fixture(
        &[("left/a.md", "# A\n"), ("refs.md", "[[left/a]]\n")],
        &["left", "dest"],
    );
    let prior = session.create_folder("prior").unwrap();
    session
        .conn
        .lock()
        .unwrap()
        .execute_batch(
            "CREATE TEMP TRIGGER reject_recovery_barrier
             BEFORE INSERT ON structural_ops
             WHEN NEW.kind = 'recovery_barrier'
             BEGIN
               SELECT RAISE(FAIL, 'injected recovery barrier write failure');
             END;",
        )
        .unwrap();
    {
        let mut state = state.lock().unwrap();
        state.fail_stat_after_write.insert("refs.md".into(), 1);
        state.fail_read_after_write.insert("refs.md".into(), 1);
    }

    let error = session.move_file("left/a.md", "dest").unwrap_err();
    assert!(
        error
            .to_string()
            .contains("injected recovery barrier write failure")
    );
    let undo_error = session.undo_structural(prior.op_id).unwrap_err();
    assert!(undo_error.to_string().contains("history is unavailable"));
}

#[test]
fn marker_failure_tracks_exact_successes_during_partial_rollback() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b")], &["dest"]);
    session.save_text("a.md", "a saved\n", None).unwrap();
    session.save_text("b.md", "b saved\n", None).unwrap();
    state
        .lock()
        .unwrap()
        .fail_renames
        .insert(("dest/b.md".into(), "b.md".into()));

    let report = session
        .batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("a.md"), file("b.md")],
                new_parent: "dest".into(),
            },
            &NthBatchFault::new(BatchFaultPoint::MovePathMarker, 2),
        )
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert_eq!(report.standing[0].old_path, "b.md");
    assert!(tmp.path().join("a.md").is_file());
    assert!(tmp.path().join("dest/b.md").is_file());
    assert_eq!(
        path_markers(&session, "a.md"),
        vec![
            ("a.md".into(), "dest/a.md".into()),
            ("dest/a.md".into(), "a.md".into()),
        ]
    );
    assert_eq!(
        path_markers(&session, "dest/b.md"),
        vec![("b.md".into(), "dest/b.md".into())]
    );
}

#[test]
fn post_write_marker_error_appends_authoritative_final_path_marker() {
    let (tmp, session, _state) = fixture(&[("a.md", "a")], &["dest"]);
    session.save_text("a.md", "a saved\n", None).unwrap();

    let report = session
        .batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("a.md")],
                new_parent: "dest".into(),
            },
            &TestBatchFaults::at(BatchFaultPoint::MovePathMarkerPostWrite),
        )
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RolledBack);
    assert!(tmp.path().join("a.md").is_file());
    assert_eq!(
        path_markers(&session, "a.md"),
        vec![
            ("a.md".into(), "dest/a.md".into()),
            ("dest/a.md".into(), "a.md".into()),
        ]
    );
}

#[test]
fn partial_journal_rollback_keeps_standing_links_and_rebuilds_live_graph() {
    let (tmp, session, state) = fixture(
        &[
            ("left/a.md", "# A"),
            ("right/b.md", "# B"),
            ("refs.md", "[[left/a]] [[right/b]]\n"),
        ],
        &["left", "right", "dest"],
    );
    let generation_before = session.graph_generation();
    let _ = session.with_graph(|graph| graph.canonical_edges()).unwrap();
    state
        .lock()
        .unwrap()
        .fail_renames
        .insert(("dest/b.md".into(), "right/b.md".into()));

    let report = session
        .batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("right/b.md"), file("left/a.md")],
                new_parent: "dest".into(),
            },
            &TestBatchFaults::at(BatchFaultPoint::MoveJournal),
        )
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert_eq!(report.standing[0].old_path, "right/b.md");
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("refs.md")).unwrap(),
        "[[left/a]] [[dest/b]]\n"
    );
    let targets: Vec<Option<String>> = {
        let conn = session.conn.lock().unwrap();
        let mut stmt = conn
            .prepare(
                "SELECT l.target_path FROM links l JOIN files f ON f.id = l.source_file_id
                 WHERE f.path = 'refs.md' ORDER BY l.ordinal",
            )
            .unwrap();
        stmt.query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap()
    };
    assert_eq!(
        targets,
        vec![Some("left/a.md".into()), Some("dest/b.md".into())]
    );
    assert!(
        !session.graph_is_built(),
        "post-commit recovery must drop the live graph"
    );
    let fresh = session.graph_rebuild_reference().unwrap();
    assert!(
        session
            .with_graph(|graph| graph.deep_equals(&fresh))
            .unwrap()
    );
    assert!(session.graph_generation() > generation_before);
}

#[test]
fn batch_move_undo_restores_multiple_parents_and_empty_folder_then_redoes() {
    let (tmp, session, _state) = fixture(
        &[("left/a.md", "a"), ("right/b.md", "b")],
        &["left", "right", "empty", "dest"],
    );
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![folder("empty"), file("right/b.md"), file("left/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    let undo = session.undo_batch_move(forward.op_id.unwrap()).unwrap();
    assert_eq!(undo.state, BatchMoveState::Succeeded);
    assert!(tmp.path().join("left/a.md").is_file());
    assert!(tmp.path().join("right/b.md").is_file());
    assert!(tmp.path().join("empty").is_dir());
    assert!(!tmp.path().join("dest/empty").exists());

    let redo = session.undo_batch_move(undo.op_id.unwrap()).unwrap();
    assert_eq!(redo.state, BatchMoveState::Succeeded);
    assert!(tmp.path().join("dest/a.md").is_file());
    assert!(tmp.path().join("dest/b.md").is_file());
    assert!(tmp.path().join("dest/empty").is_dir());
    let conn = session.conn.lock().unwrap();
    let payload: String = conn
        .query_row(
            "SELECT payload FROM structural_ops WHERE id = ?1",
            rusqlite::params![undo.op_id],
            |row| row.get(0),
        )
        .unwrap();
    let parsed = crate::structural::StructuralOpPayload::from_json(&payload).unwrap();
    assert_eq!(parsed.batch_entries.len(), 3);
    assert!(
        parsed.batch_entries.iter().any(|entry| {
            entry.from == "dest/empty" && entry.to == "empty" && entry.is_directory
        })
    );
}

#[test]
fn batch_move_undo_then_redo_restores_moved_and_bystander_link_bytes() {
    let (tmp, session, _state) = fixture(
        &[
            ("left/a.md", "[[right/b]]\n"),
            ("right/b.md", "# B\n"),
            ("refs.md", "[[left/a]] [[right/b]]\n"),
        ],
        &["left", "right", "dest"],
    );
    let originals = ["left/a.md", "right/b.md", "refs.md"]
        .into_iter()
        .map(|path| {
            (
                path.to_string(),
                std::fs::read(tmp.path().join(path)).unwrap(),
            )
        })
        .collect::<std::collections::BTreeMap<_, _>>();
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("left/a.md"), file("right/b.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    let forward_bytes = ["dest/a.md", "dest/b.md", "refs.md"]
        .into_iter()
        .map(|path| {
            (
                path.to_string(),
                std::fs::read(tmp.path().join(path)).unwrap(),
            )
        })
        .collect::<std::collections::BTreeMap<_, _>>();

    let undo = session.undo_batch_move(forward.op_id.unwrap()).unwrap();
    assert_eq!(undo.state, BatchMoveState::Succeeded);
    for (path, bytes) in &originals {
        assert_eq!(
            &std::fs::read(tmp.path().join(path)).unwrap(),
            bytes,
            "{path}"
        );
    }

    let redo = session.undo_batch_move(undo.op_id.unwrap()).unwrap();
    assert_eq!(redo.state, BatchMoveState::Succeeded);
    for (path, bytes) in &forward_bytes {
        assert_eq!(
            &std::fs::read(tmp.path().join(path)).unwrap(),
            bytes,
            "{path}"
        );
    }
}

#[test]
fn batch_move_out_of_order_undo_is_rejected() {
    let (_tmp, session, _state) = fixture(&[("a.md", "a")], &["dest"]);
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    session.create_folder("later").unwrap();

    let error = session.undo_batch_move(forward.op_id.unwrap()).unwrap_err();
    assert!(error.to_string().contains("only the latest"));
}

#[test]
fn batch_move_undo_rejects_component_overlap_across_raw_order_sibling() {
    let (tmp, session, state) = fixture(
        &[("a/child.md", "child"), ("a.md", "sibling")],
        &["a", "back"],
    );
    let op_id = {
        let mut conn = session.conn.lock().unwrap();
        let tx = conn.transaction().unwrap();
        let op_id = journal_append(
            &tx,
            crate::structural::StructuralOpKind::MoveBatch,
            &crate::structural::StructuralOpPayload {
                batch_entries: vec![
                    crate::structural::StructuralBatchJournalEntry {
                        from: "back/a".into(),
                        to: "a".into(),
                        is_directory: true,
                    },
                    crate::structural::StructuralBatchJournalEntry {
                        from: "back/a.md".into(),
                        to: "a.md".into(),
                        is_directory: false,
                    },
                    crate::structural::StructuralBatchJournalEntry {
                        from: "back/child.md".into(),
                        to: "a/child.md".into(),
                        is_directory: false,
                    },
                ],
                ..Default::default()
            },
        )
        .unwrap();
        tx.commit().unwrap();
        op_id
    };

    let report = session.undo_batch_move(op_id).unwrap();

    assert_eq!(report.state, BatchMoveState::Rejected);
    assert!(
        report
            .envelope
            .preflight_failures
            .iter()
            .any(|failure| failure.message.contains("overlapping top-level"))
    );
    assert!(tmp.path().join("a/child.md").is_file());
    assert!(tmp.path().join("a.md").is_file());
    assert!(
        state
            .lock()
            .unwrap()
            .calls
            .iter()
            .all(|call| !matches!(call, ProviderCall::Rename(_, _)))
    );
}

#[test]
fn batch_move_inverse_runtime_failure_fully_rolls_back_and_keeps_command() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b")], &["dest"]);
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md"), file("b.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    state
        .lock()
        .unwrap()
        .fail_renames
        .insert(("dest/b.md".into(), "b.md".into()));

    let report = session.undo_batch_move(forward.op_id.unwrap()).unwrap();
    assert_eq!(report.state, BatchMoveState::RolledBack);
    assert!(tmp.path().join("dest/a.md").is_file());
    assert!(tmp.path().join("dest/b.md").is_file());
    let latest: i64 = session
        .conn
        .lock()
        .unwrap()
        .query_row("SELECT MAX(id) FROM structural_ops", [], |row| row.get(0))
        .unwrap();
    assert_eq!(latest, forward.op_id.unwrap());
}

#[test]
fn batch_move_inverse_incomplete_rollback_is_truthful_and_barriered() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b")], &["dest"]);
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("a.md"), file("b.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    {
        let mut state = state.lock().unwrap();
        state
            .fail_renames
            .insert(("dest/b.md".into(), "b.md".into()));
        state
            .fail_renames
            .insert(("a.md".into(), "dest/a.md".into()));
    }

    let report = session.undo_batch_move(forward.op_id.unwrap()).unwrap();
    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert_eq!(report.standing[0].old_path, "dest/a.md");
    assert!(tmp.path().join("a.md").is_file());
    assert!(tmp.path().join("dest/b.md").is_file());
    let latest: String = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(latest, "recovery_barrier");
}

#[test]
fn failed_recovery_barrier_invalidates_both_undo_endpoints_in_session() {
    let (_tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b"), ("c.md", "c")], &["dest"]);
    let prior = session.create_folder("prior").unwrap();
    {
        let mut state = state.lock().unwrap();
        state
            .fail_renames
            .insert(("c.md".into(), "dest/c.md".into()));
        state
            .fail_renames
            .insert(("dest/a.md".into(), "a.md".into()));
    }
    let failed = session
        .batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("a.md"), file("b.md"), file("c.md")],
                new_parent: "dest".into(),
            },
            &TestBatchFaults::at_all([BatchFaultPoint::RecoveryBarrier]),
        )
        .unwrap();
    assert_eq!(failed.state, BatchMoveState::RollbackIncomplete);
    assert!(
        failed
            .rollback_failures
            .iter()
            .any(|failure| failure.stage == crate::BatchFailureStage::RecoveryBarrier)
    );

    let legacy = session.undo_structural(prior.op_id).unwrap_err();
    assert!(legacy.to_string().contains("history is unavailable"));
    let batch = session.undo_batch_move(prior.op_id).unwrap_err();
    assert!(batch.to_string().contains("history is unavailable"));
}

#[test]
fn structural_undo_waits_for_failed_recovery_barrier_then_fails_closed() {
    let (_tmp, session, state) = fixture(
        &[("prior.md", "p"), ("a.md", "a"), ("b.md", "b")],
        &["prior-dest", "dest"],
    );
    let session = Arc::new(session);
    let prior = session
        .batch_move(BatchMoveRequest {
            items: vec![file("prior.md")],
            new_parent: "prior-dest".into(),
        })
        .unwrap();
    {
        let mut state = state.lock().unwrap();
        state
            .fail_renames
            .insert(("b.md".into(), "dest/b.md".into()));
        state
            .fail_renames
            .insert(("dest/a.md".into(), "a.md".into()));
    }

    let (entered_tx, entered_rx) = mpsc::channel();
    let (release_tx, release_rx) = mpsc::channel();
    let barrier = Arc::new(BlockingRecoveryBarrierFault {
        entered: Mutex::new(Some(entered_tx)),
        release: Mutex::new(release_rx),
    });
    let failing_session = Arc::clone(&session);
    let failing_barrier = Arc::clone(&barrier);
    let (failed_tx, failed_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let result = failing_session.batch_move_with_faults(
            BatchMoveRequest {
                items: vec![file("a.md"), file("b.md")],
                new_parent: "dest".into(),
            },
            failing_barrier.as_ref(),
        );
        let _ = failed_tx.send(result);
    });
    entered_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("recovery reached the blocked barrier");

    let undo_session = Arc::clone(&session);
    let prior_op = prior.op_id.unwrap();
    let (undo_tx, undo_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let _ = undo_tx.send(undo_session.undo_batch_move(prior_op));
    });
    assert!(
        undo_rx.recv_timeout(Duration::from_millis(100)).is_err(),
        "undo must wait behind the in-flight structural recovery"
    );

    release_tx.send(()).unwrap();
    let failed = failed_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("failing batch completed")
        .unwrap();
    assert_eq!(failed.state, BatchMoveState::RollbackIncomplete);
    assert!(
        failed
            .rollback_failures
            .iter()
            .any(|failure| failure.stage == crate::BatchFailureStage::RecoveryBarrier)
    );
    let undo_error = undo_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("waiting undo completed after recovery")
        .unwrap_err();
    assert!(undo_error.to_string().contains("history is unavailable"));
}

#[test]
fn legacy_move_undo_completes_while_holding_structural_serialization() {
    let (tmp, session, _state) = fixture(&[("a.md", "a")], &["dest"]);
    let session = Arc::new(session);
    let forward = session.move_file("a.md", "dest").unwrap();
    let undo_session = Arc::clone(&session);
    let (result_tx, result_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let _ = result_tx.send(undo_session.undo_structural(forward.op_id));
    });

    let undo = result_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("legacy inverse must not recursively acquire the structural mutex")
        .unwrap();
    assert_eq!(undo.moved, vec![("dest/a.md".into(), "a.md".into())]);
    assert!(tmp.path().join("a.md").is_file());
}

#[test]
fn batch_final_rename_callback_runs_after_structural_guard_is_released() {
    let (_tmp, session, _state) = fixture(&[("a.md", "a")], &["dest"]);
    let session = Arc::new(session);
    let listener = Arc::new(ReentrantStructuralListener {
        session: Arc::downgrade(&session),
        fired: AtomicBool::new(false),
    });
    session.register_event_listener(listener.clone());
    let moving_session = Arc::clone(&session);
    let (result_tx, result_rx) = mpsc::channel();
    std::thread::spawn(move || {
        let result = moving_session.batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        });
        let _ = result_tx.send(result);
    });

    let report = result_rx
        .recv_timeout(Duration::from_secs(2))
        .expect("final structural notification must run after its guard is released")
        .unwrap();
    assert_eq!(report.state, BatchMoveState::Succeeded);
    assert!(listener.fired.load(Ordering::SeqCst));
    assert!(
        session
            .conn
            .lock()
            .unwrap()
            .query_row(
                "SELECT EXISTS(SELECT 1 FROM dirs WHERE path = 'created-from-listener')",
                [],
                |row| row.get::<_, bool>(0),
            )
            .unwrap()
    );
}

#[test]
fn batch_trash_success_uses_system_trash_and_is_not_undoable() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b")], &[]);

    let report = session
        .batch_trash(BatchTrashRequest {
            items: vec![file("b.md"), file("a.md")],
        })
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Succeeded, "{report:?}");
    assert_eq!(report.trashed, vec![file("a.md"), file("b.md")]);
    assert!(report.untrashed.is_empty());
    assert!(!tmp.path().join("a.md").exists());
    assert!(!tmp.path().join("b.md").exists());
    let delete_calls = state
        .lock()
        .unwrap()
        .calls
        .iter()
        .filter_map(|call| match call {
            ProviderCall::Delete(path) => Some(path.clone()),
            _ => None,
        })
        .collect::<Vec<_>>();
    assert_eq!(delete_calls, vec!["a.md", "b.md"]);
    let error = session.undo_structural(report.op_id.unwrap()).unwrap_err();
    assert!(error.to_string().contains("not undoable"));
}

#[test]
fn batch_trash_failure_after_one_success_continues_and_reports_exact_sets() {
    let (tmp, session, state) = fixture(&[("a.md", "a"), ("b.md", "b"), ("c.md", "c")], &[]);
    state.lock().unwrap().fail_delete_numbers.insert(2);

    let report = session
        .batch_trash(BatchTrashRequest {
            items: vec![file("c.md"), file("a.md"), file("b.md")],
        })
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Partial);
    assert_eq!(report.trashed, vec![file("a.md"), file("c.md")]);
    assert_eq!(report.untrashed.len(), 1);
    assert_eq!(report.untrashed[0].item, file("b.md"));
    assert!(!tmp.path().join("a.md").exists());
    assert!(tmp.path().join("b.md").is_file());
    assert!(!tmp.path().join("c.md").exists());
    let calls = &state.lock().unwrap().calls;
    assert_eq!(
        calls
            .iter()
            .filter_map(|call| match call {
                ProviderCall::Delete(path) => Some(path.as_str()),
                _ => None,
            })
            .collect::<Vec<_>>(),
        vec!["a.md", "b.md", "c.md"]
    );
    assert!(
        !calls
            .iter()
            .any(|call| matches!(call, ProviderCall::Rename(..)))
    );
}

#[test]
fn batch_trash_error_after_delete_uses_physical_truth_for_index_and_audit() {
    let (tmp, session, state) = fixture(&[("a.md", "a")], &[]);
    session
        .save_text("a.md", "saved through Slate\n", None)
        .unwrap();
    state.lock().unwrap().fail_deletes_after_mutation.insert(1);

    let report = session
        .batch_trash(BatchTrashRequest {
            items: vec![file("a.md")],
        })
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Succeeded);
    assert_eq!(report.trashed, vec![file("a.md")]);
    assert!(report.untrashed.is_empty());
    assert!(report.op_id.is_some());
    assert!(
        report
            .bookkeeping_failures
            .iter()
            .any(|failure| failure.stage == crate::BatchFailureStage::Trash)
    );
    assert!(!tmp.path().join("a.md").exists());
    let indexed: i64 = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT COUNT(*) FROM files WHERE path = 'a.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(indexed, 0);
    let payload_json: String = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT payload FROM structural_ops WHERE id = ?1",
            rusqlite::params![report.op_id],
            |row| row.get(0),
        )
        .unwrap();
    let payload = crate::structural::StructuralOpPayload::from_json(&payload_json).unwrap();
    assert_eq!(payload.deleted_oplogs.len(), 1);
    assert_eq!(payload.deleted_oplogs[0].path, "a.md");
    assert!(!payload.deleted_oplogs[0].oplog_name.is_empty());
    session.scan_initial(&CancelToken::new()).unwrap();
    let deleted = session.list_deleted_files(Paging::first(10)).unwrap();
    let entry = deleted
        .items
        .iter()
        .find(|entry| entry.path == "a.md")
        .expect("saved trashed file remains discoverable by its op-log");
    assert!(entry.deleted_at_ms.is_some());
}

#[test]
fn batch_trash_file_replaced_by_directory_reports_the_original_file_trashed() {
    let (tmp, session, state) = fixture(&[("a.md", "original")], &[]);
    state
        .lock()
        .unwrap()
        .replace_after_delete_with_kind
        .insert("a.md".into(), EntryKind::Directory);

    let report = session
        .batch_trash(BatchTrashRequest {
            items: vec![file("a.md")],
        })
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Succeeded, "{report:?}");
    assert_eq!(report.trashed, vec![file("a.md")]);
    assert!(report.untrashed.is_empty());
    assert!(report.unknown.is_empty());
    assert!(
        report.requires_rescan,
        "the replacement needs indexing even though the original outcome is known"
    );
    assert!(report.bookkeeping_failures.iter().any(|failure| {
        failure.stage == crate::BatchFailureStage::Reconciliation
            && failure.message.contains("opposite-kind replacement")
    }));
    assert!(tmp.path().join("a.md").is_dir());
    let indexed: i64 = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT COUNT(*) FROM files WHERE path = 'a.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(
        indexed, 0,
        "the deleted file row must not describe its replacement"
    );
    session.scan_initial(&CancelToken::new()).unwrap();
    let replacement_indexed: bool = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT EXISTS(SELECT 1 FROM dirs WHERE path = 'a.md')",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert!(replacement_indexed);
}

#[test]
fn batch_trash_folder_replaced_by_file_reports_the_original_folder_trashed() {
    let (tmp, session, state) = fixture(&[("folder/child.md", "original")], &["folder"]);
    state
        .lock()
        .unwrap()
        .replace_after_delete_with_kind
        .insert("folder".into(), EntryKind::File);

    let report = session
        .batch_trash(BatchTrashRequest {
            items: vec![folder("folder")],
        })
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Succeeded, "{report:?}");
    assert_eq!(report.trashed, vec![folder("folder")]);
    assert!(report.untrashed.is_empty());
    assert!(report.unknown.is_empty());
    assert!(
        report.requires_rescan,
        "the replacement needs indexing even though the original outcome is known"
    );
    assert!(report.bookkeeping_failures.iter().any(|failure| {
        failure.stage == crate::BatchFailureStage::Reconciliation
            && failure.message.contains("opposite-kind replacement")
    }));
    assert!(tmp.path().join("folder").is_file());
    let indexed: i64 = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT COUNT(*) FROM files WHERE path = 'folder/child.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(indexed, 0, "the deleted subtree must leave the index");
    session.scan_initial(&CancelToken::new()).unwrap();
    let replacement_indexed: bool = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT EXISTS(SELECT 1 FROM files WHERE path = 'folder')",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert!(replacement_indexed);
}

#[test]
fn batch_trash_unknown_post_call_truth_requires_rescan_and_barrier() {
    let (tmp, session, state) = fixture(&[("a.md", "a")], &[]);
    let prior = session.create_folder("prior").unwrap();
    let _ = session.with_graph(|graph| graph.canonical_edges()).unwrap();
    {
        let mut state = state.lock().unwrap();
        state.fail_delete_numbers.insert(1);
        state.fail_existence_after_delete.insert("a.md".into());
    }

    let report = session
        .batch_trash(BatchTrashRequest {
            items: vec![file("a.md")],
        })
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Failed);
    assert!(report.trashed.is_empty());
    assert!(
        report.untrashed.is_empty(),
        "an unverifiable physical outcome must not be presented as definitely not moved"
    );
    assert_eq!(report.unknown.len(), 1);
    assert_eq!(report.unknown[0].item, file("a.md"));
    assert!(
        report.unknown[0]
            .failure
            .message
            .contains("outcome is unknown")
    );
    assert!(report.requires_rescan);
    assert!(tmp.path().join("a.md").is_file());
    assert!(!session.graph_is_built());
    let latest: String = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(latest, "recovery_barrier");
    let error = session.undo_structural(prior.op_id).unwrap_err();
    assert!(error.to_string().contains("only the latest"));
}

#[test]
fn batch_trash_index_retry_preserves_physical_truth() {
    let (tmp, session, _state) = fixture(&[("a.md", "a")], &[]);

    let report = session
        .batch_trash_with_faults(
            BatchTrashRequest {
                items: vec![file("a.md")],
            },
            &TestBatchFaults::at(BatchFaultPoint::TrashIndex),
        )
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Succeeded);
    assert_eq!(report.trashed, vec![file("a.md")]);
    assert!(!report.requires_rescan);
    assert!(report.op_id.is_some());
    assert!(!tmp.path().join("a.md").exists());
    assert!(session.read_text("a.md").is_err());
}

#[test]
fn batch_trash_failed_reconciliation_keeps_physical_truth_and_drops_graph() {
    let (tmp, session, _state) = fixture(&[("a.md", "a")], &[]);
    let _ = session.with_graph(|graph| graph.canonical_edges()).unwrap();
    let generation_before = session.graph_generation();

    let report = session
        .batch_trash_with_faults(
            BatchTrashRequest {
                items: vec![file("a.md")],
            },
            &TestBatchFaults::at_all([
                BatchFaultPoint::TrashIndex,
                BatchFaultPoint::TrashReconciliation,
            ]),
        )
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Succeeded);
    assert_eq!(report.trashed, vec![file("a.md")]);
    assert!(report.untrashed.is_empty());
    assert!(report.requires_rescan);
    assert!(
        report
            .bookkeeping_failures
            .iter()
            .any(|failure| failure.stage == crate::BatchFailureStage::Reconciliation)
    );
    assert!(!tmp.path().join("a.md").exists());
    let indexed: i64 = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT COUNT(*) FROM files WHERE path = 'a.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(indexed, 1, "SQLite divergence is reported, not relabeled");
    assert!(!session.graph_is_built());
    assert!(session.graph_generation() > generation_before);
}

#[test]
fn batch_trash_audit_failure_appends_barrier_without_relabeling_bytes() {
    let (tmp, session, _state) = fixture(&[("a.md", "a")], &[]);

    let report = session
        .batch_trash_with_faults(
            BatchTrashRequest {
                items: vec![file("a.md")],
            },
            &TestBatchFaults::at(BatchFaultPoint::TrashJournal),
        )
        .unwrap();

    assert_eq!(report.state, BatchTrashState::Succeeded);
    assert_eq!(report.trashed, vec![file("a.md")]);
    assert!(report.op_id.is_none());
    assert!(!tmp.path().join("a.md").exists());
    assert!(
        report
            .bookkeeping_failures
            .iter()
            .any(|failure| failure.stage == crate::BatchFailureStage::Journal)
    );
    let latest: String = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(latest, "recovery_barrier");
}

#[test]
fn batch_trash_audit_and_barrier_failure_invalidates_both_undo_endpoints() {
    let (_tmp, session, _state) = fixture(&[("a.md", "a")], &[]);
    let prior = session.create_folder("prior").unwrap();

    let report = session
        .batch_trash_with_faults(
            BatchTrashRequest {
                items: vec![file("a.md")],
            },
            &TestBatchFaults::at_all([
                BatchFaultPoint::TrashJournal,
                BatchFaultPoint::RecoveryBarrier,
            ]),
        )
        .unwrap();

    assert_eq!(report.trashed, vec![file("a.md")]);
    assert!(report.op_id.is_none());
    assert!(
        report
            .bookkeeping_failures
            .iter()
            .any(|failure| failure.stage == crate::BatchFailureStage::RecoveryBarrier)
    );
    let legacy = session.undo_structural(prior.op_id).unwrap_err();
    assert!(legacy.to_string().contains("history is unavailable"));
    let batch = session.undo_batch_move(prior.op_id).unwrap_err();
    assert!(batch.to_string().contains("history is unavailable"));
}

#[test]
fn batch_trash_saved_file_and_folder_descendant_keep_deleted_timestamps() {
    let (_tmp, session, state) = fixture(&[], &["folder"]);
    session.save_text("single.md", "single\n", None).unwrap();
    session
        .save_text("folder/child.md", "child\n", None)
        .unwrap();
    session.save_text("keep.md", "keep\n", None).unwrap();
    state.lock().unwrap().calls.clear();
    state.lock().unwrap().fail_delete_numbers.insert(2);

    let report = session
        .batch_trash(BatchTrashRequest {
            items: vec![folder("folder"), file("keep.md"), file("single.md")],
        })
        .unwrap();
    assert_eq!(report.state, BatchTrashState::Partial);
    assert_eq!(report.trashed, vec![folder("folder"), file("single.md")]);
    assert_eq!(report.untrashed[0].item, file("keep.md"));

    session.scan_initial(&CancelToken::new()).unwrap();
    let deleted = session.list_deleted_files(Paging::first(10)).unwrap();
    let rows = deleted
        .items
        .iter()
        .map(|entry| (entry.path.as_str(), entry.deleted_at_ms))
        .collect::<std::collections::BTreeMap<_, _>>();
    assert!(rows.get("single.md").copied().flatten().is_some());
    assert!(rows.get("folder/child.md").copied().flatten().is_some());
    assert!(!rows.contains_key("keep.md"));
}
