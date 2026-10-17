// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1450: the session's Trash endpoints when a folder on an item's path is
//! substituted after the session's final check and before the provider's
//! own walk — the window the system Trash used to cross with the lexical
//! path (#1430 P12.6, P16). The provider trashes into a scratch bin, never a
//! real Trash.

use super::*;
use crate::vault::{ScratchTrash, TrashSnapshot};
use crate::{
    BatchFailureStage, BatchTrashRequest, BatchTrashState, DirEntry, FileEventSink, FileStat,
    StructuralBatchItem, WatchHandle,
};
use std::sync::Mutex;

type Substitution = Box<dyn FnOnce() + Send>;

/// Forwards to an `FsVaultProvider` that trashes into a scratch bin. Right
/// before the provider's `delete` of one chosen path it runs that path's
/// substitution: after the session's last validation, before the walk.
struct SubstitutingProvider {
    inner: FsVaultProvider,
    before_delete: Mutex<Option<(String, Substitution)>>,
}

impl crate::VaultProvider for SubstitutingProvider {
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        let substitution = {
            let mut slot = self.before_delete.lock().unwrap();
            if slot.as_ref().is_some_and(|(path, _)| path == relative) {
                slot.take().map(|(_, run)| run)
            } else {
                None
            }
        };
        if let Some(run) = substitution {
            run();
        }
        self.inner.delete(relative)
    }
    fn trash_snapshot(&self, relative: &str) -> Result<TrashSnapshot, VaultError> {
        self.inner.trash_snapshot(relative)
    }
    fn trash_snapshot_cancellable(
        &self,
        relative: &str,
        cancel: &CancelToken,
    ) -> Result<TrashSnapshot, VaultError> {
        self.inner.trash_snapshot_cancellable(relative, cancel)
    }
    fn list_dir(&self, relative: &str) -> Result<Vec<DirEntry>, VaultError> {
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
    fn read_in_vault_with_cap(
        &self,
        relative: &str,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        self.inner.read_in_vault_with_cap(relative, max_bytes)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }
    fn write_file_if_absent(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file_if_absent(relative, contents)
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }
    fn mutation_identity(&self, path: &str) -> Result<String, VaultError> {
        self.inner.mutation_identity(path)
    }
    fn rename_if_identity(&self, from: &str, to: &str, expected: &str) -> Result<(), VaultError> {
        self.inner.rename_if_identity(from, to, expected)
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
    fn stat(&self, relative: &str) -> Result<FileStat, VaultError> {
        self.inner.stat(relative)
    }
    fn watch(&self, sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

/// A vault with `sub/note.md`, `sub/inner/deep.md`, `keep.md` and
/// `other.md`, and a folder outside it holding decoys under the same names.
struct Fixture {
    vault: tempfile::TempDir,
    #[cfg_attr(not(unix), allow(dead_code))]
    outside: tempfile::TempDir,
    trash: Arc<ScratchTrash>,
    #[cfg_attr(not(unix), allow(dead_code))]
    provider: Arc<SubstitutingProvider>,
    session: VaultSession,
}

fn fixture_at(vault: tempfile::TempDir, root: std::path::PathBuf) -> Fixture {
    let outside = tempfile::tempdir().unwrap();
    for (path, contents) in [
        ("sub/note.md", "vault note"),
        ("sub/inner/deep.md", "vault deep"),
        ("sub/unique.md", "vault unique"),
        ("keep.md", "keep"),
        ("other.md", "other"),
    ] {
        let path = vault.path().join(path);
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        std::fs::write(path, contents).unwrap();
    }
    std::fs::create_dir(outside.path().join("inner")).unwrap();
    std::fs::write(outside.path().join("note.md"), "outside decoy").unwrap();
    std::fs::write(outside.path().join("inner/deep.md"), "outside deep").unwrap();
    let trash = ScratchTrash::new();
    let provider = Arc::new(SubstitutingProvider {
        inner: FsVaultProvider::with_system_trash(root.clone(), trash.clone()),
        before_delete: Mutex::new(None),
    });
    let session = VaultSession::open(provider.clone(), SessionConfig::new(root.join(".slate")))
        .expect("open fixture");
    session.scan_initial(&CancelToken::new()).expect("scan");
    Fixture {
        vault,
        outside,
        trash,
        provider,
        session,
    }
}

fn fixture() -> Fixture {
    let vault = tempfile::tempdir().unwrap();
    let root = vault.path().to_path_buf();
    fixture_at(vault, root)
}

fn item(path: &str, is_directory: bool) -> StructuralBatchItem {
    StructuralBatchItem {
        path: path.into(),
        is_directory,
    }
}

fn request(items: &[(&str, bool)]) -> BatchTrashRequest {
    BatchTrashRequest {
        items: items
            .iter()
            .map(|(path, is_directory)| item(path, *is_directory))
            .collect(),
    }
}

impl Fixture {
    /// Arm `substitution` to run right before the provider deletes `path`.
    #[cfg(unix)]
    fn before_delete_of(&self, path: &str, substitution: Substitution) {
        *self.provider.before_delete.lock().unwrap() = Some((path.into(), substitution));
    }

    /// #1430 P12.6: `sub` becomes a symlink to the outside folder; the real
    /// folder stays in the vault as `sub.real`.
    #[cfg(unix)]
    fn replace_sub_with_a_link(&self) -> Substitution {
        let vault = self.vault.path().to_path_buf();
        let outside = self.outside.path().to_path_buf();
        Box::new(move || {
            std::fs::rename(vault.join("sub"), vault.join("sub.real")).unwrap();
            std::os::unix::fs::symlink(&outside, vault.join("sub")).unwrap();
        })
    }

    #[cfg(unix)]
    fn assert_decoys_and_vault_items_untouched(&self) {
        for (outside, contents) in [
            ("note.md", "outside decoy"),
            ("inner/deep.md", "outside deep"),
        ] {
            assert_eq!(
                std::fs::read_to_string(self.outside.path().join(outside)).unwrap(),
                contents,
                "the decoy {outside} outside the vault must never reach the Trash"
            );
        }
        for (inside, contents) in [
            ("sub.real/note.md", "vault note"),
            ("sub.real/inner/deep.md", "vault deep"),
            ("sub.real/unique.md", "vault unique"),
        ] {
            assert_eq!(
                std::fs::read_to_string(self.vault.path().join(inside)).unwrap(),
                contents,
                "the refused {inside} stays in the vault"
            );
        }
    }

    fn indexed(&self, path: &str) -> bool {
        self.session
            .conn
            .lock()
            .unwrap()
            .query_row(
                "SELECT count(*) FROM files WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get::<_, i64>(0),
            )
            .unwrap()
            > 0
    }

    #[cfg(unix)]
    fn markers(&self, path: &str) -> i64 {
        self.session
            .conn
            .lock()
            .unwrap()
            .query_row(
                "SELECT count(*) FROM text_write_intents WHERE path = ?1",
                rusqlite::params![path],
                |row| row.get(0),
            )
            .unwrap()
    }
}

#[cfg(unix)]
fn refusal_message() -> String {
    "A folder in this item's path was replaced or moved, so it was not moved to the Trash. \
     Request deletion again"
        .to_string()
}

#[test]
fn an_ordinary_staged_trash_hands_over_the_pinned_path() {
    let f = fixture();
    let staged = f
        .session
        .stage_trash(request(&[("sub/note.md", false)]))
        .unwrap();
    f.session
        .delete_file_staged("sub/note.md", staged.token)
        .unwrap();
    let root = std::fs::canonicalize(f.vault.path()).unwrap();
    assert_eq!(f.trash.handed(), vec![root.join("sub").join("note.md")]);
    assert_eq!(f.trash.contents(), vec!["note.md"]);
    assert!(!f.indexed("sub/note.md"));
}

#[cfg(unix)]
#[test]
fn staged_trash_refuses_an_item_whose_folder_became_a_link_after_the_last_check() {
    let f = fixture();
    let staged = f
        .session
        .stage_trash(request(&[("sub/note.md", false)]))
        .unwrap();
    f.before_delete_of("sub/note.md", f.replace_sub_with_a_link());
    let error = f
        .session
        .delete_file_staged("sub/note.md", staged.token)
        .unwrap_err();
    assert!(
        matches!(&error, VaultError::TrashConfirmationChanged { message } if *message == refusal_message()),
        "{error:?}"
    );
    f.assert_decoys_and_vault_items_untouched();
    assert!(f.trash.handed().is_empty(), "{:?}", f.trash.handed());
    assert!(
        f.indexed("sub/note.md"),
        "a refused item is never reported gone"
    );
}

#[cfg(unix)]
#[test]
fn staged_folder_trash_refuses_when_its_parent_became_a_link_after_the_last_check() {
    let f = fixture();
    let staged = f
        .session
        .stage_trash(request(&[("sub/inner", true)]))
        .unwrap();
    f.before_delete_of("sub/inner", f.replace_sub_with_a_link());
    let error = f
        .session
        .delete_folder_staged("sub/inner", staged.token)
        .unwrap_err();
    assert!(
        matches!(&error, VaultError::TrashConfirmationChanged { message } if *message == refusal_message()),
        "{error:?}"
    );
    f.assert_decoys_and_vault_items_untouched();
    assert!(f.trash.handed().is_empty(), "{:?}", f.trash.handed());
    assert!(f.indexed("sub/inner/deep.md"));
}

/// A batch keeps going: the refused item is reported untrashed with core's
/// reason and keeps its recovery markers, and the rest reach the Trash.
#[cfg(unix)]
#[test]
fn batch_trash_reports_a_refused_item_untrashed_and_trashes_the_rest() {
    for staged in [true, false] {
        let f = fixture();
        let request = request(&[
            ("keep.md", false),
            ("sub/note.md", false),
            ("other.md", false),
        ]);
        let report = if staged {
            // The host executes exactly the request core staged (normalized).
            let staged = f.session.stage_trash(request).unwrap();
            let request = BatchTrashRequest {
                items: staged
                    .items
                    .iter()
                    .map(|staged| staged.item.clone())
                    .collect(),
            };
            f.before_delete_of("sub/note.md", f.replace_sub_with_a_link());
            f.session.batch_trash_staged(request, staged.token)
        } else {
            f.before_delete_of("sub/note.md", f.replace_sub_with_a_link());
            f.session.batch_trash(request)
        }
        .unwrap();
        assert_eq!(report.state, BatchTrashState::Partial, "staged {staged}");
        let mut trashed = report.trashed.clone();
        trashed.sort_by(|a, b| a.path.cmp(&b.path));
        assert_eq!(
            trashed,
            vec![item("keep.md", false), item("other.md", false)]
        );
        assert_eq!(report.untrashed.len(), 1, "{:?}", report.untrashed);
        let refused = &report.untrashed[0];
        assert_eq!(refused.item, item("sub/note.md", false));
        assert_eq!(refused.failure.stage, BatchFailureStage::Trash);
        assert_eq!(refused.failure.message, refusal_message());
        assert!(report.unknown.is_empty(), "{:?}", report.unknown);
        assert!(
            report.bookkeeping_failures.is_empty(),
            "{:?}",
            report.bookkeeping_failures
        );
        assert!(report.requires_rescan);
        assert_eq!(f.trash.contents(), vec!["keep.md", "other.md"]);
        f.assert_decoys_and_vault_items_untouched();
        assert!(
            f.indexed("sub/note.md"),
            "a refused item is never reported gone"
        );
        assert!(!f.indexed("keep.md") && !f.indexed("other.md"));
        assert!(
            f.markers("sub/note.md") > 0,
            "the refused item keeps its markers"
        );
        assert_eq!(f.markers("keep.md"), 0);
    }
}

/// The substitute need not hold an entry of the refused item's name. Then
/// the batch's post-call probe, which resolves the lexical path through the
/// link, finds nothing there; the item must still be reported untrashed and
/// keep its index row, never be counted trashed.
#[cfg(unix)]
#[test]
fn batch_trash_never_counts_a_refused_item_trashed_when_the_substitute_lacks_it() {
    let f = fixture();
    let staged = f
        .session
        .stage_trash(request(&[("keep.md", false), ("sub/unique.md", false)]))
        .unwrap();
    let request = BatchTrashRequest {
        items: staged
            .items
            .iter()
            .map(|staged| staged.item.clone())
            .collect(),
    };
    f.before_delete_of("sub/unique.md", f.replace_sub_with_a_link());
    let report = f.session.batch_trash_staged(request, staged.token).unwrap();
    assert_eq!(report.state, BatchTrashState::Partial);
    assert_eq!(report.trashed, vec![item("keep.md", false)]);
    assert_eq!(report.untrashed.len(), 1, "{:?}", report.untrashed);
    assert_eq!(report.untrashed[0].item, item("sub/unique.md", false));
    assert_eq!(report.untrashed[0].failure.message, refusal_message());
    assert!(
        report.bookkeeping_failures.is_empty(),
        "{:?}",
        report.bookkeeping_failures
    );
    assert!(
        f.indexed("sub/unique.md"),
        "a refused item is never reported gone"
    );
    assert_eq!(f.trash.contents(), vec!["keep.md"]);
    f.assert_decoys_and_vault_items_untouched();
}

#[cfg(unix)]
#[test]
fn a_symlinked_vault_root_still_trashes_through_the_session() {
    let real = tempfile::tempdir().unwrap();
    let links = tempfile::tempdir().unwrap();
    let root = links.path().join("vault");
    std::os::unix::fs::symlink(real.path(), &root).unwrap();
    let real_root = std::fs::canonicalize(real.path()).unwrap();
    let f = fixture_at(real, root);
    let staged = f
        .session
        .stage_trash(request(&[("sub/note.md", false)]))
        .unwrap();
    f.session
        .delete_file_staged("sub/note.md", staged.token)
        .unwrap();
    assert_eq!(
        f.trash.handed(),
        vec![real_root.join("sub").join("note.md")]
    );
    assert_eq!(f.trash.contents(), vec!["note.md"]);
    assert!(!f.indexed("sub/note.md"));
}
