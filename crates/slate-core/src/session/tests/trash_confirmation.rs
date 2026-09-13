// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

use super::*;
use crate::{BatchTrashRequest, StructuralBatchItem};

fn request(path: &str, is_directory: bool) -> BatchTrashRequest {
    BatchTrashRequest {
        items: vec![StructuralBatchItem {
            path: path.into(),
            is_directory,
        }],
    }
}

fn fixture() -> (tempfile::TempDir, VaultSession) {
    let (dir, session) = super::common::make_vault(|provider| {
        provider.create_dir("folder/empty").unwrap();
        provider.create_dir("empty").unwrap();
        provider.write_file("folder/a.md", b"original").unwrap();
        provider.write_file("keep.md", b"keep").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    (dir, session)
}

fn journal_size(session: &VaultSession) -> i64 {
    session
        .conn
        .lock()
        .unwrap()
        .query_row("SELECT count(*) FROM structural_ops", [], |row| row.get(0))
        .unwrap()
}

#[test]
fn cancellable_trash_pre_cancel_and_exact_discard_preserve_token_ownership() {
    let (_dir, session) = fixture();
    let cancel = CancelToken::new();
    cancel.cancel();
    assert!(matches!(
        session.stage_trash_cancellable(request("empty", true), &cancel),
        Err(VaultError::Cancelled)
    ));
    assert!(session.trash_confirmation.lock().unwrap().is_none());
    let old = session.stage_trash(request("empty", true)).unwrap();
    let fresh = session.stage_trash(request("empty", true)).unwrap();
    session.discard_staged_trash(old.token);
    assert!(
        session
            .trash_confirmation
            .lock()
            .unwrap()
            .as_ref()
            .is_some_and(|pending| pending.token == fresh.token)
    );
    assert!(matches!(
        session.delete_folder_staged_cancellable("empty", fresh.token, &cancel),
        Err(VaultError::Cancelled)
    ));
    assert!(matches!(
        session.delete_folder_staged("empty", fresh.token),
        Err(VaultError::TrashConfirmationChanged { .. })
    ));
    let fresh = session.stage_trash(request("empty", true)).unwrap();
    session.discard_staged_trash(fresh.token);
    assert!(session.trash_confirmation.lock().unwrap().is_none());
    assert_eq!(journal_size(&session), 0);
}

#[test]
fn cancellable_trash_waits_stop_without_releasing_the_blocker() {
    use std::sync::mpsc;
    use std::time::{Duration, Instant};
    for blocker in ["operation", "connection", "os_lock", "registry"] {
        let (_dir, session) = fixture();
        let operation =
            (blocker == "operation").then(|| session.structural_operation.lock().unwrap());
        let connection = (blocker == "connection").then(|| session.conn.lock().unwrap());
        let registry = (blocker == "registry")
            .then(|| VaultStructuralLock::acquire(&session.config.cache_dir).unwrap());
        // A separately opened OS handle bypasses the process registry, exercising
        // the second half of the lock protocol, not another local registry wait.
        let os_lock = (blocker == "os_lock").then(|| {
            let file = std::fs::OpenOptions::new()
                .read(true)
                .write(true)
                .open(session.config.cache_dir.join("structural.lock"))
                .unwrap();
            file.lock().unwrap();
            file
        });
        let cancel = CancelToken::new();
        let (started_tx, started_rx) = mpsc::channel();
        let (done_tx, done_rx) = mpsc::channel();
        std::thread::scope(|scope| {
            let worker = scope.spawn(|| {
                started_tx.send(()).unwrap();
                done_tx
                    .send(session.stage_trash_cancellable(request("empty", true), &cancel))
                    .unwrap();
            });
            started_rx.recv_timeout(Duration::from_secs(30)).unwrap();
            if blocker != "registry" {
                let deadline = Instant::now() + Duration::from_secs(30);
                while VaultSession::structural_lock_is_free(&session.config.cache_dir) {
                    assert!(
                        Instant::now() < deadline,
                        "worker did not claim registry: {blocker}"
                    );
                    std::thread::yield_now();
                }
            }
            assert!(done_rx.recv_timeout(Duration::from_millis(30)).is_err());
            cancel.cancel();
            let result = done_rx
                .recv_timeout(Duration::from_secs(30))
                .expect("cancel must not wait for the blocker to release");
            assert!(matches!(result, Err(VaultError::Cancelled)), "{blocker}");
            worker.join().unwrap();
        });
        drop((operation, connection, registry, os_lock));
        assert!(VaultSession::structural_lock_is_free(
            &session.config.cache_dir
        ));
        assert!(session.stage_trash(request("empty", true)).is_ok());
    }
}

#[test]
fn cancellable_trash_writer_wait_restores_busy_timeout() {
    use std::sync::mpsc;
    use std::time::Duration;
    let (_dir, session) = fixture();
    let other = Connection::open(session.config.cache_dir.join("cache.sqlite")).unwrap();
    let held = db::begin_fenced(&other).unwrap();
    let cancel = CancelToken::new();
    let (started_tx, started_rx) = mpsc::channel();
    let (done_tx, done_rx) = mpsc::channel();
    std::thread::scope(|scope| {
        let worker = scope.spawn(|| {
            let conn = session.conn.lock().unwrap();
            started_tx.send(()).unwrap();
            let result = begin_fenced_cancellable(&conn, &cancel).map(drop);
            let timeout: u32 = conn
                .query_row("PRAGMA busy_timeout", [], |r| r.get(0))
                .unwrap();
            done_tx.send((result, timeout)).unwrap();
        });
        started_rx.recv_timeout(Duration::from_secs(30)).unwrap();
        assert!(done_rx.recv_timeout(Duration::from_millis(30)).is_err());
        cancel.cancel();
        let (result, timeout) = done_rx.recv_timeout(Duration::from_secs(30)).unwrap();
        assert!(matches!(result, Err(VaultError::Cancelled)));
        assert_eq!(timeout, 5000);
        worker.join().unwrap();
    });
    drop(held);
    assert!(begin_fenced_cancellable(&session.conn.lock().unwrap(), &CancelToken::new()).is_ok());
}

#[test]
fn staged_trash_counts_hidden_and_unindexed_nested_entries() {
    let (dir, session) = fixture();
    std::fs::write(dir.path().join("folder/empty/.secret"), b"hidden").unwrap();
    let stage = session.stage_trash(request("folder", true)).unwrap();
    assert_eq!(stage.items[0].item_count, 3);
    // Reading and enumerating do not invalidate a snapshot by changing atime.
    session.provider.read_file("folder/a.md").unwrap();
    let confirmation = session
        .take_trash_confirmation(stage.token, &request("folder", true))
        .unwrap();
    confirmation
        .validate(session.provider.as_ref(), &request("folder", true))
        .unwrap();
}

#[test]
fn staged_trash_empty_folder_population_refuses_without_journal_or_index_changes() {
    let (dir, session) = fixture();
    let stage = session.stage_trash(request("empty", true)).unwrap();
    assert_eq!(stage.items[0].item_count, 0);
    let before = journal_size(&session);
    std::fs::write(dir.path().join("empty/late.md"), b"not confirmed").unwrap();
    assert!(session.delete_folder_staged("empty", stage.token).is_err());
    assert_eq!(
        std::fs::read(dir.path().join("empty/late.md")).unwrap(),
        b"not confirmed"
    );
    assert_eq!(journal_size(&session), before);
    assert_eq!(
        session
            .conn
            .lock()
            .unwrap()
            .query_row("SELECT count(*) FROM text_write_intents", [], |row| row
                .get::<_, i64>(0))
            .unwrap(),
        0
    );
}

#[test]
fn staged_trash_detects_same_count_replacement_and_nested_addition() {
    let (dir, session) = fixture();
    let stage = session.stage_trash(request("folder", true)).unwrap();
    std::fs::rename(
        dir.path().join("folder/a.md"),
        dir.path().join("original.md"),
    )
    .unwrap();
    std::fs::write(dir.path().join("folder/a.md"), b"replaced").unwrap();
    assert!(session.delete_folder_staged("folder", stage.token).is_err());
    assert_eq!(
        std::fs::read(dir.path().join("folder/a.md")).unwrap(),
        b"replaced"
    );
    let stage = session.stage_trash(request("folder", true)).unwrap();
    std::fs::write(dir.path().join("folder/empty/late.bin"), b"late").unwrap();
    assert!(session.delete_folder_staged("folder", stage.token).is_err());
    assert!(dir.path().join("folder/empty/late.bin").exists());
}

#[test]
fn staged_trash_token_is_single_use_session_and_request_bound() {
    let (dir, session) = fixture();
    let other = VaultSession::from_filesystem(dir.path().to_path_buf()).unwrap();
    let stage = session.stage_trash(request("empty", true)).unwrap();
    assert!(other.delete_folder_staged("empty", stage.token).is_err());
    // A token from another request never grants deletion, and a failed attempt
    // consumes the owning token so a later retry cannot silently reuse it.
    assert!(session.delete_file_staged("keep.md", stage.token).is_err());
    assert!(session.delete_folder_staged("empty", stage.token).is_err());
    assert!(dir.path().join("keep.md").exists());
    let old = session.stage_trash(request("empty", true)).unwrap();
    let fresh = session.stage_trash(request("empty", true)).unwrap();
    assert!(session.delete_folder_staged("empty", old.token).is_err());
    session
        .take_trash_confirmation(fresh.token, &request("empty", true))
        .unwrap();
    assert!(
        session
            .take_trash_confirmation(fresh.token, &request("empty", true))
            .is_err()
    );
}

#[test]
fn staged_trash_initial_stale_batch_does_not_delete_any_selection() {
    let (dir, session) = fixture();
    let request = BatchTrashRequest {
        items: vec![
            request("folder", true).items.remove(0),
            request("keep.md", false).items.remove(0),
        ],
    };
    let stage = session.stage_trash(request.clone()).unwrap();
    std::fs::write(dir.path().join("folder/.late"), b"unindexed").unwrap();
    assert!(session.batch_trash_staged(request, stage.token).is_err());
    assert_eq!(std::fs::read(dir.path().join("keep.md")).unwrap(), b"keep");
    assert_eq!(journal_size(&session), 0);
}

#[test]
fn staged_trash_rejects_wrong_kind_missing_root_and_conflicting_selection() {
    let (_dir, session) = fixture();
    for request in [
        request("", true),
        request("missing", true),
        request("keep.md", true),
        BatchTrashRequest {
            items: vec![
                request("folder", true).items.remove(0),
                request("folder", false).items.remove(0),
            ],
        },
    ] {
        assert!(session.stage_trash(request).is_err());
    }
}

#[cfg(unix)]
#[test]
fn staged_trash_counts_links_without_following_them_and_refuses_link_ancestors() {
    let (dir, session) = fixture();
    let outside = tempfile::tempdir().unwrap();
    std::fs::write(outside.path().join("private"), b"outside").unwrap();
    std::os::unix::fs::symlink(outside.path(), dir.path().join("folder/link")).unwrap();
    let stage = session.stage_trash(request("folder", true)).unwrap();
    assert_eq!(
        stage.items[0].item_count, 3,
        "link counts once; its target is never enumerated"
    );
    assert!(session.stage_trash(request("folder/link", true)).is_err());
    assert!(
        session
            .stage_trash(request("folder/link/private", false))
            .is_err()
    );
}
