// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use super::*;
use crate::{BatchMoveRequest, BatchMoveState, StructuralBatchItem};
use std::collections::HashMap;

fn fixture() -> (tempfile::TempDir, VaultSession) {
    let (dir, session) = super::common::make_vault(|provider| {
        provider.create_dir("dest").unwrap();
        provider.create_dir("Folder").unwrap();
        provider
            .write_file("Folder/Folder.md", b"folder note")
            .unwrap();
        provider.write_file("a.md", b"a").unwrap();
        provider.write_file("b.md", b"b").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    (dir, session)
}

fn items() -> Vec<StructuralBatchItem> {
    ["a.md", "b.md"]
        .into_iter()
        .map(|path| StructuralBatchItem {
            path: path.into(),
            is_directory: false,
        })
        .collect()
}

fn moved_batch(session: &VaultSession) -> (i64, HashMap<String, String>) {
    let ids = session.capture_batch_move_identities(items()).unwrap();
    let report = session
        .batch_move(BatchMoveRequest {
            items: items(),
            new_parent: "dest".into(),
        })
        .unwrap();
    assert_eq!(report.state, BatchMoveState::Succeeded);
    (
        report.op_id.unwrap(),
        report
            .standing
            .iter()
            .map(|change| (change.new_path.clone(), ids[&change.old_path].clone()))
            .collect(),
    )
}

#[test]
fn conditional_inverse_refuses_replacement_but_preserves_in_place_edits() {
    let (dir, session) = fixture();
    let identity = session.capture_structural_identity("a.md", false).unwrap();
    session.rename_file("a.md", "renamed.md").unwrap();
    std::fs::write(dir.path().join("renamed.md"), b"edited in place").unwrap();
    session
        .rename_file_if_identity("renamed.md", "a.md", &identity.entry)
        .unwrap();
    assert_eq!(
        std::fs::read(dir.path().join("a.md")).unwrap(),
        b"edited in place"
    );
    session.rename_file("a.md", "renamed.md").unwrap();
    std::fs::rename(
        dir.path().join("renamed.md"),
        dir.path().join("original.md"),
    )
    .unwrap();
    std::fs::write(dir.path().join("renamed.md"), b"replacement").unwrap();
    assert!(
        session
            .rename_file_if_identity("renamed.md", "a.md", &identity.entry)
            .is_err()
    );
    assert_eq!(
        std::fs::read(dir.path().join("renamed.md")).unwrap(),
        b"replacement"
    );
    assert!(!dir.path().join("a.md").exists());
}

#[test]
fn conditional_inverse_compound_folder_note_round_trips_and_refuses_changed_note() {
    let (dir, session) = fixture();
    let identity = session.capture_structural_identity("Folder", true).unwrap();
    assert!(identity.folder_note.is_some());
    session
        .rename_folder_with_note("Folder", "Renamed")
        .unwrap();
    session
        .rename_folder_with_note_if_identity("Renamed", "Folder", &identity)
        .unwrap();
    assert!(dir.path().join("Folder/Folder.md").exists());
    session
        .rename_folder_with_note_if_identity("Folder", "Renamed", &identity)
        .unwrap();
    std::fs::rename(
        dir.path().join("Renamed/Renamed.md"),
        dir.path().join("original-note.md"),
    )
    .unwrap();
    std::fs::write(dir.path().join("Renamed/Renamed.md"), b"stranger").unwrap();
    assert!(
        session
            .rename_folder_with_note_if_identity("Renamed", "Folder", &identity)
            .is_err()
    );
    assert!(!dir.path().join("Folder").exists());
    assert_eq!(
        std::fs::read(dir.path().join("Renamed/Renamed.md")).unwrap(),
        b"stranger"
    );
}

#[test]
fn conditional_inverse_batch_rejects_incomplete_and_replaced_selections_before_mutation() {
    let _env_guard = super::common::ENV_FAULT_GUARD.lock().unwrap();
    let (dir, session) = fixture();
    let (op, ids) = moved_batch(&session);
    assert!(
        session
            .undo_batch_move_if_identities(op, HashMap::new())
            .is_err()
    );
    std::fs::rename(
        dir.path().join("dest/b.md"),
        dir.path().join("original-b.md"),
    )
    .unwrap();
    std::fs::write(dir.path().join("dest/b.md"), b"stranger").unwrap();
    assert!(session.undo_batch_move_if_identities(op, ids).is_err());
    assert_eq!(std::fs::read(dir.path().join("dest/a.md")).unwrap(), b"a");
    assert_eq!(
        std::fs::read(dir.path().join("dest/b.md")).unwrap(),
        b"stranger"
    );
    assert!(!dir.path().join("a.md").exists());
}

#[test]
fn conditional_inverse_batch_round_trips_with_the_original_identities() {
    let _env_guard = super::common::ENV_FAULT_GUARD.lock().unwrap();
    let (dir, session) = fixture();
    let (op, ids) = moved_batch(&session);
    let undone = session
        .undo_batch_move_if_identities(op, ids.clone())
        .unwrap();
    assert_eq!(undone.state, BatchMoveState::Succeeded);
    let redo_ids = undone
        .standing
        .iter()
        .map(|change| (change.new_path.clone(), ids[&change.old_path].clone()))
        .collect();
    let redone = session
        .undo_batch_move_if_identities(undone.op_id.unwrap(), redo_ids)
        .unwrap();
    assert_eq!(redone.state, BatchMoveState::Succeeded);
    assert_eq!(std::fs::read(dir.path().join("dest/a.md")).unwrap(), b"a");
}

struct ReplaceBeforeRollback(std::path::PathBuf);
impl StructuralBatchFaultHook for ReplaceBeforeRollback {
    fn check(&self, point: BatchFaultPoint) -> Result<(), VaultError> {
        if point == BatchFaultPoint::MoveIndex {
            std::fs::rename(self.0.join("a.md"), self.0.join("original-a.md"))?;
            std::fs::write(self.0.join("a.md"), b"stranger")?;
            return Err(VaultError::InvalidArgument {
                message: "injected index failure".into(),
            });
        }
        Ok(())
    }
}

#[test]
fn conditional_inverse_rollback_refuses_replacements_and_retains_recovery() {
    // Existing recovery tests install process-wide, path-matching fault seams.
    let _env_guard = super::common::ENV_FAULT_GUARD.lock().unwrap();
    let (dir, session) = fixture();
    let (op, ids) = moved_batch(&session);
    let report = session
        .undo_batch_move_checked(
            op,
            &ReplaceBeforeRollback(dir.path().to_path_buf()),
            Some(&ids),
        )
        .unwrap();
    assert_eq!(report.state, BatchMoveState::RollbackIncomplete);
    assert!(report.requires_rescan);
    assert_eq!(std::fs::read(dir.path().join("a.md")).unwrap(), b"stranger");
    assert!(!dir.path().join("dest/a.md").exists());
    assert_eq!(std::fs::read(dir.path().join("dest/b.md")).unwrap(), b"b");
    drop(session);
    let _reopened = VaultSession::from_filesystem(dir.path().to_path_buf());
    assert_eq!(std::fs::read(dir.path().join("a.md")).unwrap(), b"stranger");
    assert!(!dir.path().join("dest/a.md").exists());
}

#[test]
fn conditional_inverse_crash_recovery_keeps_identity_conditions() {
    // Existing recovery tests install process-wide, path-matching fault seams.
    let _env_guard = super::common::ENV_FAULT_GUARD.lock().unwrap();
    let (dir, session) = fixture();
    let identity = session.capture_structural_identity("a.md", false).unwrap();
    let plan = crate::structural_batch::PlannedBatchMove {
        item: items().remove(0),
        destination: "dest/a.md".into(),
        moved_files: vec![("a.md".into(), "dest/a.md".into())],
    };
    let mut inflight = StructuralBatchInflight::from_plans(&[plan]);
    inflight.version = 2;
    inflight.entries[0].expected_identity = Some(identity.entry.clone());
    structural_batch_insert_inflight(&session.conn.lock().unwrap(), &inflight).unwrap();
    session
        .provider
        .rename_if_identity("a.md", "dest/a.md", &identity.entry)
        .unwrap();
    drop(session);
    std::fs::rename(
        dir.path().join("dest/a.md"),
        dir.path().join("original-a.md"),
    )
    .unwrap();
    std::fs::write(dir.path().join("dest/a.md"), b"stranger").unwrap();
    let _reopened = VaultSession::from_filesystem(dir.path().to_path_buf());
    assert_eq!(
        std::fs::read(dir.path().join("dest/a.md")).unwrap(),
        b"stranger"
    );
    assert!(!dir.path().join("a.md").exists());
}

#[test]
fn conditional_inverse_defers_folder_note_rewrites_until_after_both_checked_renames() {
    let (dir, session) = fixture();
    std::fs::write(dir.path().join("Folder/child.md"), b"child").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let identity = session.capture_structural_identity("Folder", true).unwrap();
    session
        .rename_folder_with_note("Folder", "Renamed")
        .unwrap();
    // In-place content edits keep the ID. The inverse must not invalidate its
    // own pending note rename by atomically rewriting these links too early.
    std::fs::write(dir.path().join("Renamed/Renamed.md"), b"[[Renamed/child]]").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        session
            .capture_structural_identity("Renamed", true)
            .unwrap(),
        identity
    );
    let report = session
        .rename_folder_with_note_if_identity("Renamed", "Folder", &identity)
        .unwrap();
    assert!(report.failed.is_empty());
    assert_eq!(
        std::fs::read_to_string(dir.path().join("Folder/Folder.md")).unwrap(),
        "[[Folder/child]]"
    );
    assert!(dir.path().join("Folder/child.md").exists());
    // The completed inverse's own atomic rewrite invalidates a re-inverse.
    // Callers must discard that frame, never adopt a newly observed ID.
    assert_ne!(
        session.capture_structural_identity("Folder", true).unwrap(),
        identity
    );
}

#[test]
fn conditional_inverse_note_journal_failure_keeps_completed_topology_and_links() {
    let (dir, session) = fixture();
    std::fs::write(dir.path().join("Folder/child.md"), b"child").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let identity = session.capture_structural_identity("Folder", true).unwrap();
    session
        .rename_folder_with_note("Folder", "Renamed")
        .unwrap();
    std::fs::write(dir.path().join("Renamed/Renamed.md"), b"[[Renamed/child]]").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session.conn.lock().unwrap().execute_batch(
        "CREATE TRIGGER fail_note_journal BEFORE INSERT ON structural_ops WHEN NEW.kind = 'rename_file' BEGIN SELECT RAISE(FAIL, 'injected note journal failure'); END;"
    ).unwrap();
    let error = session
        .rename_folder_with_note_if_identity("Renamed", "Folder", &identity)
        .unwrap_err();
    assert!(
        matches!(error, VaultError::StructuralMutationIncomplete { ref path, .. } if path == "Folder")
    );
    assert_eq!(
        std::fs::read_to_string(dir.path().join("Folder/Folder.md")).unwrap(),
        "[[Folder/child]]"
    );
    assert!(dir.path().join("Folder/child.md").exists());
    assert!(!dir.path().join("Renamed").exists());
    assert!(session.capture_structural_identity("Folder", true).is_err());
    let kind: String = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT kind FROM structural_ops ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(kind, "recovery_barrier");
}

#[test]
fn conditional_inverse_first_folder_journal_failure_reports_moved_topology() {
    let (dir, session) = fixture();
    let identity = session.capture_structural_identity("Folder", true).unwrap();
    session
        .rename_folder_with_note("Folder", "Renamed")
        .unwrap();
    session.conn.lock().unwrap().execute_batch(
        "CREATE TRIGGER fail_folder_journal BEFORE INSERT ON structural_ops WHEN NEW.kind = 'rename_folder' BEGIN SELECT RAISE(FAIL, 'injected folder journal failure'); END;"
    ).unwrap();
    let error = session
        .rename_folder_with_note_if_identity("Renamed", "Folder", &identity)
        .unwrap_err();
    assert!(
        matches!(error, VaultError::StructuralMutationIncomplete { ref path, .. } if path == "Folder")
    );
    assert!(dir.path().join("Folder/Renamed.md").exists());
    assert!(!dir.path().join("Renamed").exists());
    assert!(session.capture_structural_identity("Folder", true).is_err());
}
