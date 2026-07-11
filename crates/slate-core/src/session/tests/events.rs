// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #802: the broadened `VaultEventListener` — file-change events from
//! every Slate-originated write path, index-phase lifecycle, and the
//! additive-default contract (an `on_error`-only listener keeps
//! compiling and working untouched).

use std::sync::{Arc, Mutex};

use super::common::*;
use super::*;

/// Records every callback with its payload, in arrival order.
#[derive(Default)]
struct RecordingListener {
    file_changes: Mutex<Vec<FileChangeEvent>>,
    phases: Mutex<Vec<(IndexPhase, u64)>>,
}

impl VaultEventListener for RecordingListener {
    fn on_error(&self, _code: EventErrorCode, _path: String, _message: String) {}
    fn on_file_change(&self, event: FileChangeEvent) {
        self.file_changes.lock().unwrap().push(event);
    }
    fn on_index_phase(&self, phase: IndexPhase, files_seen: u64) {
        self.phases.lock().unwrap().push((phase, files_seen));
    }
}

/// The additive contract itself: a listener written against the O-2
/// trait (only `on_error`) still compiles and registers — the new
/// methods default to no-ops.
struct LegacyListener;
impl VaultEventListener for LegacyListener {
    fn on_error(&self, _code: EventErrorCode, _path: String, _message: String) {}
}

fn changes_of(listener: &RecordingListener) -> Vec<(FileChangeKind, String, Option<String>)> {
    listener
        .file_changes
        .lock()
        .unwrap()
        .iter()
        .map(|e| (e.kind, e.path.clone(), e.previous_path.clone()))
        .collect()
}

#[test]
fn every_write_path_emits_its_file_change_event() {
    let (_tmp, session) = make_vault(|p| {
        p.create_dir("Sub").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let listener = Arc::new(RecordingListener::default());
    let token = session.register_event_listener(listener.clone());
    // A legacy on_error-only listener rides along untouched (the
    // additive gate) — reaching the end without panicking IS the test.
    let legacy_token = session.register_event_listener(Arc::new(LegacyListener));

    // save_text: new file → Created, second save → Modified.
    let r = session.save_text("a.md", "one\n", None).unwrap();
    session
        .save_text("a.md", "two\n", Some(&r.new_content_hash))
        .unwrap();
    // create_exclusive → Created.
    session.create_exclusive("b.md", "b\n").unwrap();
    // restore_version rides save_text → Modified.
    session
        .restore_version("a.md", &r.new_content_hash, None)
        .unwrap();
    // rename_file → Renamed with previous_path.
    session.rename_file("b.md", "c.md").unwrap();
    // move_file → Renamed into the subfolder.
    session.move_file("c.md", "Sub").unwrap();
    // delete_file → Deleted.
    session.delete_file("Sub/c.md").unwrap();

    assert_eq!(
        changes_of(&listener),
        vec![
            (FileChangeKind::Created, "a.md".into(), None),
            (FileChangeKind::Modified, "a.md".into(), None),
            (FileChangeKind::Created, "b.md".into(), None),
            (FileChangeKind::Modified, "a.md".into(), None),
            (FileChangeKind::Renamed, "c.md".into(), Some("b.md".into())),
            (
                FileChangeKind::Renamed,
                "Sub/c.md".into(),
                Some("c.md".into())
            ),
            (FileChangeKind::Deleted, "Sub/c.md".into(), None),
        ]
    );

    session.unregister_event_listener(token);
    session.unregister_event_listener(legacy_token);
    session.save_text("d.md", "d\n", None).unwrap();
    assert_eq!(
        changes_of(&listener).len(),
        7,
        "unregistered listeners hear nothing"
    );
}

#[test]
fn folder_operations_emit_per_file_events() {
    let (_tmp, session) = make_vault(|p| {
        p.create_dir("Dir").unwrap();
        p.write_file("Dir/x.md", b"x\n").unwrap();
        p.write_file("Dir/y.md", b"y\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let listener = Arc::new(RecordingListener::default());
    session.register_event_listener(listener.clone());

    session.rename_folder("Dir", "Ren").unwrap();
    let renames: Vec<_> = changes_of(&listener);
    assert_eq!(renames.len(), 2, "one Renamed per contained file");
    assert!(
        renames
            .iter()
            .any(|(k, to, from)| *k == FileChangeKind::Renamed
                && to == "Ren/x.md"
                && from.as_deref() == Some("Dir/x.md"))
    );

    session.delete_folder("Ren").unwrap();
    let all = changes_of(&listener);
    let deletes: Vec<_> = all
        .iter()
        .filter(|(k, ..)| *k == FileChangeKind::Deleted)
        .collect();
    assert_eq!(deletes.len(), 2, "one Deleted per contained file");
}

#[test]
fn recovery_emits_created_at_the_destination() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("gone.md", "body\n", None).unwrap();
    session.delete_file("gone.md").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let listener = Arc::new(RecordingListener::default());
    session.register_event_listener(listener.clone());
    session.recover_deleted_file("gone.md").unwrap();
    let changes = changes_of(&listener);
    assert!(
        changes
            .iter()
            .any(|(k, p, _)| *k == FileChangeKind::Created && p == "gone.md"),
        "recovery emits Created: {changes:?}"
    );
}

#[test]
fn scan_emits_the_phase_lifecycle_in_order() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"n\n").unwrap();
    });
    let listener = Arc::new(RecordingListener::default());
    session.register_event_listener(listener.clone());

    session.scan_initial(&CancelToken::new()).unwrap();
    let phases = listener.phases.lock().unwrap().clone();
    assert_eq!(
        phases.iter().map(|(p, _)| *p).collect::<Vec<_>>(),
        vec![
            IndexPhase::ScanStarted,
            IndexPhase::ReconcileStarted,
            IndexPhase::ReconcileFinished,
            IndexPhase::ScanFinished,
        ]
    );
    let (_, files_seen) = phases[3];
    assert_eq!(files_seen, 1, "ScanFinished carries the file count");
    assert!(
        phases[..3].iter().all(|(_, n)| *n == 0),
        "counts ride only the terminal phase"
    );
}

/// The seam guarantee (codex round 1): every mutator that commits
/// through `save_text_locked` — property, task, and frontmatter edits
/// included — emits, because the emission lives IN the seam, not in
/// per-API wrappers.
#[test]
fn seam_mutators_emit_without_wrappers() {
    let (_tmp, session) = make_vault(|_| {});
    session.scan_initial(&CancelToken::new()).unwrap();
    session
        .save_text("n.md", "---\nstatus: a\n---\n- [ ] task\n", None)
        .unwrap();
    let listener = Arc::new(RecordingListener::default());
    session.register_event_listener(listener.clone());

    session
        .set_property(
            "n.md",
            "status",
            crate::PropertyValue::Text("b".into()),
            None,
        )
        .unwrap();
    session.toggle_task_status("n.md", 0, 'x', None).unwrap();
    session.delete_property("n.md", "status", None).unwrap();
    session
        .set_frontmatter_source("n.md", "kind: note\n", None)
        .unwrap();

    assert_eq!(
        changes_of(&listener)
            .iter()
            .map(|(k, p, _)| (*k, p.as_str()))
            .collect::<Vec<_>>(),
        vec![
            (FileChangeKind::Modified, "n.md"),
            (FileChangeKind::Modified, "n.md"),
            (FileChangeKind::Modified, "n.md"),
            (FileChangeKind::Modified, "n.md"),
        ],
        "every seam mutator emits exactly one Modified"
    );
}

/// Undo re-runs inverse moves outside the public wrappers and restores
/// rewrites through the seam — both halves must emit.
#[test]
fn undo_emits_renames_and_rewrite_modifications() {
    let (_tmp, session) = make_vault(|p| {
        p.create_dir("Dir").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("a.md", "see [[b]]\n", None).unwrap();
    session.save_text("b.md", "target\n", None).unwrap();
    let report = session.rename_file("b.md", "c.md").unwrap();

    let listener = Arc::new(RecordingListener::default());
    session.register_event_listener(listener.clone());
    session.undo_structural(report.op_id).unwrap();

    let changes = changes_of(&listener);
    assert!(
        changes
            .iter()
            .any(|(k, to, from)| *k == FileChangeKind::Renamed
                && to == "b.md"
                && from.as_deref() == Some("c.md")),
        "undo emits the inverse Renamed: {changes:?}"
    );
    assert!(
        changes
            .iter()
            .any(|(k, p, _)| *k == FileChangeKind::Modified && p == "a.md"),
        "the rewrite restore emits Modified through the seam: {changes:?}"
    );
}
