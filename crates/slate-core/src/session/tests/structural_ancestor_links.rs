// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1449: a vault folder swapped for a symlink after Slate indexed it can no
//! longer redirect a structural move, through any session API the hosts
//! call: single move and rename, undo and redo, batch move and its undo, the
//! folder-note rename, and the crash-recovery replay. Before the fix each of
//! them reached `FsVaultProvider::rename`, which renamed by absolute path
//! and followed the link (#1430, probe P1b): files outside the vault moved
//! into it, and vault files moved out of it.
//!
//! #1451: the same facts run on Windows with a junction in the folder's
//! place (making one needs no privilege), where `MoveFileExW` followed it.

use super::*;
use crate::structural_batch::{BatchMoveRequest, BatchMoveState, StructuralBatchItem};
use std::collections::BTreeMap;
use std::path::Path;
use std::sync::Mutex;

/// Every entry beneath `dir` except the vault's own `.slate` cache, never
/// following a link: the kind, and the bytes or the link target.
pub(super) fn tree(dir: &Path) -> BTreeMap<String, String> {
    fn visit(base: &Path, dir: &Path, out: &mut BTreeMap<String, String>) {
        for entry in std::fs::read_dir(dir).unwrap() {
            let path = entry.unwrap().path();
            let name = path
                .strip_prefix(base)
                .unwrap()
                .to_string_lossy()
                .into_owned();
            if name == ".slate" {
                continue;
            }
            let meta = path.symlink_metadata().unwrap();
            if meta.file_type().is_symlink() {
                let target = std::fs::read_link(&path).unwrap();
                out.insert(name, format!("link -> {}", target.display()));
            } else if meta.is_dir() {
                out.insert(name, "folder".into());
                visit(base, &path, out);
            } else {
                out.insert(name, format!("file {:?}", std::fs::read(&path).unwrap()));
            }
        }
    }
    let mut out = BTreeMap::new();
    visit(dir, dir, &mut out);
    out
}

pub(super) fn write(root: &Path, path: &str, contents: &str) {
    let full = root.join(path);
    std::fs::create_dir_all(full.parent().unwrap()).unwrap();
    std::fs::write(full, contents).unwrap();
}

pub(super) fn indexed_vault(files: &[(&str, &str)]) -> (tempfile::TempDir, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    for (path, contents) in files {
        write(tmp.path(), path, contents);
    }
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

/// The external writer: park the real folder inside the vault, then put a
/// link to `target` (outside the vault) in its place: a symbolic link on
/// Unix, a junction on Windows (#1451).
pub(super) fn swap_for_link(vault: &Path, folder: &str, target: &Path) {
    std::fs::rename(vault.join(folder), vault.join(format!("{folder}-parked"))).unwrap();
    link_folder(&vault.join(folder), target);
}

/// Put a link to the folder `target` at `at`: a symbolic link on Unix, a
/// junction on Windows.
pub(super) fn link_folder(at: &Path, target: &Path) {
    #[cfg(unix)]
    std::os::unix::fs::symlink(target, at).unwrap();
    #[cfg(windows)]
    {
        std::fs::create_dir(at).unwrap();
        crate::vault::test_support::junction(at, target);
    }
}

/// How a refusal names a linked folder: `"<folder>" is a symbolic link` on
/// Unix, `"<folder>" is a junction` on Windows.
pub(super) fn linked(folder: &str) -> String {
    #[cfg(unix)]
    let kind = "a symbolic link";
    #[cfg(windows)]
    let kind = "a junction";
    format!("\"{folder}\" is {kind}")
}

fn assert_link_refusal(error: &VaultError, refused: &str, link: &str) {
    let needle = linked(link);
    assert!(
        matches!(error, VaultError::InvalidPath { path, reason }
            if path == refused && reason.contains(&needle)),
        "expected the #1449 refusal for {refused:?} naming {link:?}, got {error:?}"
    );
}

fn file(path: &str) -> StructuralBatchItem {
    StructuralBatchItem {
        path: path.to_string(),
        is_directory: false,
    }
}

#[test]
fn move_and_rename_refuse_a_folder_swapped_for_a_link_in_either_direction() {
    let (tmp, session) = indexed_vault(&[("sub/a.md", "vault a"), ("c.md", "vault c")]);
    let outside = tempfile::tempdir().unwrap();
    write(outside.path(), "a.md", "outside a");
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    // Out of the vault: a vault file moved into the swapped folder.
    let error = session.move_file("c.md", "sub").unwrap_err();
    assert_link_refusal(&error, "sub/c.md", "sub");
    // Into the vault: the swapped folder's (outside) file moved or renamed.
    // macOS may refuse these one step earlier, when binding the source to
    // its stored spelling finds it outside the vault (#1077 I7); either way
    // it is a typed `InvalidPath` and nothing moves.
    for error in [
        session.move_file("sub/a.md", "").unwrap_err(),
        session.rename_file("sub/a.md", "b.md").unwrap_err(),
    ] {
        assert!(matches!(error, VaultError::InvalidPath { .. }), "{error:?}");
    }

    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
}

fn assert_real_folders(root: &Path, folders: &[&str]) {
    for folder in folders {
        let meta = root.join(folder).symlink_metadata().unwrap();
        assert!(
            meta.is_dir() && !meta.file_type().is_symlink(),
            "{folder} is not a real folder in the vault"
        );
    }
}

/// #1449 keeps today's move into folders that do not exist yet: `move_file`
/// creates them inside the vault (each with `mkdirat` in its pinned parent),
/// and the undo, the redo and the hosts' inverse move the file back and
/// forth while the created folders stay, as on main. When an EXISTING
/// ancestor on either side is a symbolic link or a file, the same move
/// refuses and creates nothing, anywhere.
#[test]
fn a_move_into_missing_folders_creates_them_and_its_undo_keeps_them() {
    let (tmp, session) = indexed_vault(&[
        ("a.md", "vault a"),
        ("plain.md", "a file, not a folder"),
        ("sub/b.md", "vault b"),
    ]);

    let forward = session.move_file("a.md", "new/deeper").unwrap();
    assert_real_folders(tmp.path(), &["new", "new/deeper"]);
    assert_eq!(
        std::fs::read(tmp.path().join("new/deeper/a.md")).unwrap(),
        b"vault a"
    );

    let undo = session.undo_structural(forward.op_id).unwrap();
    assert_eq!(std::fs::read(tmp.path().join("a.md")).unwrap(), b"vault a");
    assert!(!tmp.path().join("new/deeper/a.md").exists());
    assert_real_folders(tmp.path(), &["new", "new/deeper"]);
    session.undo_structural(undo.op_id).unwrap();
    assert!(tmp.path().join("new/deeper/a.md").is_file(), "redo");
    session.move_file("new/deeper/a.md", "").unwrap();
    assert_eq!(std::fs::read(tmp.path().join("a.md")).unwrap(), b"vault a");
    assert_real_folders(tmp.path(), &["new", "new/deeper"]);

    let outside = tempfile::tempdir().unwrap();
    write(outside.path(), "b.md", "outside b");
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    // Destination through an existing link: the provider's refusal.
    let through_link = session.move_file("a.md", "sub/fresh").unwrap_err();
    assert_link_refusal(&through_link, "sub/fresh/a.md", "sub");
    // Destination beneath an existing file, and a source beneath the link:
    // refused (binding to the stored spelling may refuse first, typed).
    for error in [
        session.move_file("a.md", "plain.md/fresh").unwrap_err(),
        session.move_file("sub/b.md", "fresh").unwrap_err(),
    ] {
        assert!(
            matches!(error, VaultError::InvalidPath { .. } | VaultError::Io(_)),
            "{error:?}"
        );
    }

    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(
        tree(tmp.path()),
        vault_before,
        "a refused move created or moved something"
    );
}

#[test]
fn undo_of_a_move_refuses_to_move_a_file_out_through_a_swapped_folder() {
    let (tmp, session) = indexed_vault(&[("sub/a.md", "vault a"), ("sub/keep.md", "keep")]);
    let forward = session.move_file("sub/a.md", "").unwrap();
    let outside = tempfile::tempdir().unwrap();
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    // Core's undo endpoint, and a forward move back: the mac host's inverse
    // re-runs the forward move with the inverse arguments, and on Windows,
    // whose host undoes through the identity inverse, the same move back is
    // the user's own forward move (#1451's "host's forward move-back").
    let undo = session.undo_structural(forward.op_id).unwrap_err();
    assert_link_refusal(&undo, "sub/a.md", "sub");
    let inverse = session.move_file("a.md", "sub").unwrap_err();
    assert_link_refusal(&inverse, "sub/a.md", "sub");

    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    assert_eq!(std::fs::read(tmp.path().join("a.md")).unwrap(), b"vault a");
}

#[test]
fn redo_of_a_move_refuses_to_move_a_file_out_through_a_swapped_folder() {
    let (tmp, session) = indexed_vault(&[("a.md", "vault a"), ("sub/keep.md", "keep")]);
    let forward = session.move_file("a.md", "sub").unwrap();
    let undo = session.undo_structural(forward.op_id).unwrap();
    let outside = tempfile::tempdir().unwrap();
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    let redo = session.undo_structural(undo.op_id).unwrap_err();

    assert_link_refusal(&redo, "sub/a.md", "sub");
    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
}

#[test]
fn batch_move_into_a_swapped_folder_is_rejected_before_anything_moves() {
    let (tmp, session) = indexed_vault(&[("x.md", "x"), ("c.md", "c"), ("sub/keep.md", "keep")]);
    let outside = tempfile::tempdir().unwrap();
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("x.md"), file("c.md")],
            new_parent: "sub".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::Rejected, "{report:?}");
    let refused: Vec<_> = report
        .envelope
        .preflight_failures
        .iter()
        .filter(|failure| failure.message.contains(&linked("sub")))
        .filter_map(|failure| failure.item.as_ref().map(|item| item.path.as_str()))
        .collect();
    assert_eq!(refused, vec!["c.md", "x.md"], "{report:?}");
    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
}

#[test]
fn undo_of_a_batch_move_refuses_through_a_swapped_folder() {
    let (tmp, session) = indexed_vault(&[
        ("sub/a.md", "vault a"),
        ("sub/b.md", "vault b"),
        ("dest/keep.md", "keep"),
    ]);
    let forward = session
        .batch_move(BatchMoveRequest {
            items: vec![file("sub/a.md"), file("sub/b.md")],
            new_parent: "dest".into(),
        })
        .unwrap();
    assert_eq!(forward.state, BatchMoveState::Succeeded, "{forward:?}");
    let outside = tempfile::tempdir().unwrap();
    swap_for_link(tmp.path(), "sub", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    let undo = session.undo_batch_move(forward.op_id.unwrap()).unwrap();

    assert_eq!(undo.state, BatchMoveState::Rejected, "{undo:?}");
    let refused: Vec<_> = undo
        .envelope
        .preflight_failures
        .iter()
        .filter(|failure| failure.message.contains(&linked("sub")))
        .filter_map(|failure| failure.item.as_ref().map(|item| item.path.as_str()))
        .collect();
    assert_eq!(refused, vec!["dest/a.md", "dest/b.md"], "{undo:?}");
    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
}

#[test]
fn folder_note_rename_refuses_under_a_swapped_ancestor() {
    let (tmp, session) = indexed_vault(&[
        ("area/Proj/Proj.md", "vault note"),
        ("area/Proj/x.md", "vault x"),
    ]);
    let outside = tempfile::tempdir().unwrap();
    write(outside.path(), "Proj/Proj.md", "outside note");
    swap_for_link(tmp.path(), "area", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    let error = session
        .rename_folder_with_note("area/Proj", "Renamed")
        .unwrap_err();

    assert!(matches!(error, VaultError::InvalidPath { .. }), "{error:?}");
    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
}

/// The external writer's action, run once just before the first rename
/// whose source starts with the given prefix.
type SwapBeforeRename = (String, Box<dyn FnOnce() + Send>);

/// A provider that delegates everything to `FsVaultProvider` and adds two
/// seams: run a swap just before a chosen rename (the external writer acts
/// after preflight), and simulate a crash right after the first rename.
struct HookedProvider {
    inner: FsVaultProvider,
    before_rename: Mutex<Option<SwapBeforeRename>>,
    crash_after_first_rename: std::sync::atomic::AtomicBool,
}

impl HookedProvider {
    fn new(root: &Path) -> Self {
        Self {
            inner: FsVaultProvider::new(root.to_path_buf()),
            before_rename: Mutex::new(None),
            crash_after_first_rename: std::sync::atomic::AtomicBool::new(false),
        }
    }
}

impl VaultProvider for HookedProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }
    fn read_file_with_cap(&self, relative: &str, max_bytes: u64) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file_with_cap(relative, max_bytes)
    }
    fn read_file_range_with_cap(
        &self,
        relative: &str,
        offset: u64,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        self.inner
            .read_file_range_with_cap(relative, offset, max_bytes)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }
    fn write_file_if_absent(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file_if_absent(relative, contents)
    }
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(relative)
    }
    fn trash_snapshot(&self, relative: &str) -> Result<crate::vault::TrashSnapshot, VaultError> {
        self.inner.trash_snapshot(relative)
    }
    fn trash_snapshot_cancellable(
        &self,
        relative: &str,
        cancel: &CancelToken,
    ) -> Result<crate::vault::TrashSnapshot, VaultError> {
        self.inner.trash_snapshot_cancellable(relative, cancel)
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        let hook = {
            let mut armed = self.before_rename.lock().unwrap();
            match armed.as_ref() {
                Some((prefix, _)) if from.starts_with(prefix.as_str()) => armed.take(),
                _ => None,
            }
        };
        if let Some((_, swap)) = hook {
            swap();
        }
        self.inner.rename(from, to)?;
        assert!(
            !self
                .crash_after_first_rename
                .swap(false, std::sync::atomic::Ordering::SeqCst),
            "simulated process crash after the first rename"
        );
        Ok(())
    }
    fn preflight_rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.preflight_rename(from, to)
    }
    fn preflight_delete(&self, path: &str) -> Result<(), VaultError> {
        self.inner.preflight_delete(path)
    }
    fn mutation_path_kind(&self, path: &str) -> Result<Option<EntryKind>, VaultError> {
        self.inner.mutation_path_kind(path)
    }
    fn mutation_path_exists(&self, path: &str) -> Result<bool, VaultError> {
        self.inner.mutation_path_exists(path)
    }
    fn canonical_path(&self, path: &str) -> Result<Option<String>, VaultError> {
        self.inner.canonical_path(path)
    }
    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }
    fn create_dir_if_absent(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir_if_absent(relative)
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
    fn read_in_vault_with_cap(
        &self,
        relative: &str,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        self.inner.read_in_vault_with_cap(relative, max_bytes)
    }
}

fn hooked_vault(files: &[(&str, &str)]) -> (tempfile::TempDir, Arc<HookedProvider>, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    for (path, contents) in files {
        write(tmp.path(), path, contents);
    }
    let provider = Arc::new(HookedProvider::new(tmp.path()));
    let session = VaultSession::open(
        Arc::clone(&provider) as Arc<dyn VaultProvider>,
        SessionConfig::new(tmp.path().join(".slate")),
    )
    .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, provider, session)
}

/// The folder is swapped after the batch preflight passed and after the
/// first item moved: the second item's rename itself refuses, the first is
/// rolled back through the same hardened call, and nothing from outside the
/// vault is pulled in.
#[test]
fn batch_move_whose_folder_is_swapped_after_preflight_moves_nothing_through_it() {
    let (tmp, provider, session) = hooked_vault(&[
        ("c.md", "vault c"),
        ("sub/a.md", "vault a"),
        ("dest/keep.md", "keep"),
    ]);
    let outside = tempfile::tempdir().unwrap();
    write(outside.path(), "a.md", "outside a");
    let vault = tmp.path().to_path_buf();
    let target = outside.path().to_path_buf();
    *provider.before_rename.lock().unwrap() = Some((
        "sub/".into(),
        Box::new(move || swap_for_link(&vault, "sub", &target)),
    ));
    let outside_before = tree(outside.path());

    let report = session
        .batch_move(BatchMoveRequest {
            items: vec![file("c.md"), file("sub/a.md")],
            new_parent: "dest".into(),
        })
        .unwrap();

    assert_eq!(report.state, BatchMoveState::RolledBack, "{report:?}");
    let failure = report
        .failure
        .as_ref()
        .expect("the refused item is reported");
    assert!(failure.message.contains(&linked("sub")), "{failure:?}");
    assert!(report.rollback_failures.is_empty(), "{report:?}");
    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(std::fs::read(tmp.path().join("c.md")).unwrap(), b"vault c");
    assert!(!tmp.path().join("dest/a.md").exists());
    assert!(!tmp.path().join("dest/c.md").exists());
    assert_eq!(
        std::fs::read(tmp.path().join("sub-parked/a.md")).unwrap(),
        b"vault a"
    );
}

/// A batch crashed after its first rename; before the reopen, the
/// destination folder (holding the moved file) is swapped for a link to a
/// directory holding a same-named file. The recovery replay must not pull
/// that outside file into the vault: it fails closed instead.
#[test]
fn crash_recovery_replay_refuses_to_move_through_a_swapped_folder() {
    let (tmp, provider, session) = hooked_vault(&[("a.md", "vault a"), ("dest/keep.md", "keep")]);
    provider
        .crash_after_first_rename
        .store(true, std::sync::atomic::Ordering::SeqCst);
    let crashed = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let _ = session.batch_move(BatchMoveRequest {
            items: vec![file("a.md")],
            new_parent: "dest".into(),
        });
    }));
    assert!(crashed.is_err(), "the hook simulates process termination");
    drop(session);
    assert!(tmp.path().join("dest/a.md").is_file());
    let outside = tempfile::tempdir().unwrap();
    write(outside.path(), "a.md", "outside a");
    swap_for_link(tmp.path(), "dest", outside.path());
    let outside_before = tree(outside.path());
    let vault_before = tree(tmp.path());

    let reopened = VaultSession::open(
        Arc::new(FsVaultProvider::new(tmp.path().to_path_buf())),
        SessionConfig::new(tmp.path().join(".slate")),
    );

    let error = reopened.err().expect("recovery must fail closed");
    assert!(error.to_string().contains(&linked("dest")), "{error:?}");
    assert_eq!(tree(outside.path()), outside_before, "outside changed");
    assert_eq!(tree(tmp.path()), vault_before, "vault changed");
    assert!(!tmp.path().join("a.md").exists());
}
