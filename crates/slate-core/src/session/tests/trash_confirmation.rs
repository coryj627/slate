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
