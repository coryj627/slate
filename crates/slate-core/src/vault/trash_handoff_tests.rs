// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1450: the Trash is handed a path derived from the pinned parent, never
//! the lexical vault path. Every fact here trashes into a `ScratchTrash`
//! bin, which acts on its path the way a path-addressed system Trash does
//! (folders on the way resolved with links followed), so handing it the
//! lexical path again moves the decoy and fails these facts.

use super::*;
use crate::vault::{FsVaultProvider, VaultProvider};
use std::sync::Arc;

/// A vault holding `sub/note.md`, and a folder outside it holding a decoy
/// of the same name: what a substituted `sub` would point the Trash at.
struct Fixture {
    vault: tempfile::TempDir,
    outside: tempfile::TempDir,
    trash: Arc<ScratchTrash>,
    provider: FsVaultProvider,
}

fn fixture() -> Fixture {
    let vault = tempfile::tempdir().unwrap();
    let outside = tempfile::tempdir().unwrap();
    std::fs::create_dir(vault.path().join("sub")).unwrap();
    std::fs::write(vault.path().join("sub/note.md"), b"vault note").unwrap();
    std::fs::write(outside.path().join("note.md"), b"outside decoy").unwrap();
    let trash = ScratchTrash::new();
    let provider = FsVaultProvider::with_system_trash(vault.path().to_path_buf(), trash.clone());
    Fixture {
        vault,
        outside,
        trash,
        provider,
    }
}

impl Fixture {
    /// #1430 P12.6: after the inventory, `sub` is replaced by a symlink to the
    /// outside folder. The real folder stays in the vault as `sub.real`.
    #[cfg(unix)]
    fn replace_sub_with_a_link(&self) {
        std::fs::rename(
            self.vault.path().join("sub"),
            self.vault.path().join("sub.real"),
        )
        .unwrap();
        std::os::unix::fs::symlink(self.outside.path(), self.vault.path().join("sub")).unwrap();
    }

    fn assert_nothing_moved(&self) {
        assert_eq!(
            std::fs::read(self.outside.path().join("note.md")).unwrap(),
            b"outside decoy",
            "the decoy outside the vault must never reach the Trash"
        );
        assert_eq!(
            std::fs::read(self.vault.path().join("sub.real/note.md")).unwrap(),
            b"vault note",
            "the refused vault note stays where it is"
        );
        assert!(self.trash.handed().is_empty(), "{:?}", self.trash.handed());
        assert!(
            self.trash.contents().is_empty(),
            "{:?}",
            self.trash.contents()
        );
    }
}

fn is_refusal(error: &VaultError) -> bool {
    matches!(error, VaultError::TrashConfirmationChanged { message } if message == &moved().to_string())
}

#[test]
fn an_ordinary_trash_hands_over_the_pinned_parents_current_path() {
    let f = fixture();
    f.provider.delete("sub/note.md").unwrap();
    let root = std::fs::canonicalize(f.vault.path()).unwrap();
    assert_eq!(f.trash.handed(), vec![root.join("sub").join("note.md")]);
    assert!(!f.vault.path().join("sub/note.md").exists());
    assert_eq!(
        std::fs::read(f.trash.bin().join("note.md")).unwrap(),
        b"vault note"
    );
    assert_eq!(
        std::fs::read(f.outside.path().join("note.md")).unwrap(),
        b"outside decoy"
    );
}

#[test]
fn a_folder_and_a_root_level_file_trash_through_the_same_handoff() {
    let f = fixture();
    std::fs::write(f.vault.path().join("top.md"), b"top").unwrap();
    f.provider.delete("top.md").unwrap();
    f.provider.delete("sub").unwrap();
    let root = std::fs::canonicalize(f.vault.path()).unwrap();
    assert_eq!(
        f.trash.handed(),
        vec![root.join("top.md"), root.join("sub")]
    );
    assert_eq!(f.trash.contents(), vec!["sub", "top.md"]);
    assert_eq!(
        std::fs::read(f.trash.bin().join("sub/note.md")).unwrap(),
        b"vault note"
    );
}

#[test]
fn a_missing_folder_or_entry_stays_not_found_rather_than_a_refusal() {
    let f = fixture();
    for missing in ["nope/note.md", "sub/nope.md"] {
        match f.provider.delete(missing).unwrap_err() {
            VaultError::Io(error) => assert_eq!(error.kind(), std::io::ErrorKind::NotFound),
            other => panic!("{missing}: expected Io NotFound, got {other:?}"),
        }
    }
    assert!(f.trash.handed().is_empty());
}

/// The P12.6 regression: Slate's own inventory, then the substitution, then
/// the Trash. Before #1450 the lexical path reached the Trash and the decoy
/// outside the vault was trashed.
#[cfg(unix)]
#[test]
fn delete_refuses_a_folder_replaced_by_a_link_after_the_inventory() {
    let f = fixture();
    let inventory = f.provider.trash_snapshot("sub/note.md").unwrap();
    assert!(!inventory.is_directory);
    f.replace_sub_with_a_link();
    let error = f.provider.delete("sub/note.md").unwrap_err();
    assert!(is_refusal(&error), "{error:?}");
    f.assert_nothing_moved();
}

#[cfg(unix)]
#[test]
fn delete_refuses_a_folder_replaced_by_a_file() {
    let f = fixture();
    std::fs::rename(f.vault.path().join("sub"), f.vault.path().join("sub.real")).unwrap();
    std::fs::write(f.vault.path().join("sub"), b"not a folder").unwrap();
    let error = f.provider.delete("sub/note.md").unwrap_err();
    assert!(is_refusal(&error), "{error:?}");
    f.assert_nothing_moved();
}

/// A dangling link is unlinked, not trashed. Before #1450 that unlink went
/// through the lexical path too, so a substituted folder made it delete, for
/// good, an entry outside the vault.
#[cfg(unix)]
#[test]
fn delete_never_unlinks_a_dangling_link_outside_the_vault_through_a_swapped_folder() {
    let f = fixture();
    std::os::unix::fs::symlink(
        f.vault.path().join("gone"),
        f.vault.path().join("sub/dangling.md"),
    )
    .unwrap();
    std::os::unix::fs::symlink(
        f.outside.path().join("gone"),
        f.outside.path().join("dangling.md"),
    )
    .unwrap();
    f.replace_sub_with_a_link();
    let error = f.provider.delete("sub/dangling.md").unwrap_err();
    assert!(is_refusal(&error), "{error:?}");
    assert!(
        f.outside
            .path()
            .join("dangling.md")
            .symlink_metadata()
            .is_ok(),
        "the outside link must not be unlinked"
    );
    assert!(
        f.vault
            .path()
            .join("sub.real/dangling.md")
            .symlink_metadata()
            .is_ok()
    );
    f.assert_nothing_moved();
}

#[cfg(unix)]
#[test]
fn a_dangling_link_inside_the_vault_is_unlinked_through_the_pinned_parent() {
    let f = fixture();
    std::os::unix::fs::symlink(
        f.vault.path().join("gone"),
        f.vault.path().join("sub/dangling.md"),
    )
    .unwrap();
    f.provider.delete("sub/dangling.md").unwrap();
    assert!(
        f.vault
            .path()
            .join("sub/dangling.md")
            .symlink_metadata()
            .is_err()
    );
    assert!(
        f.trash.handed().is_empty(),
        "a dangling link is unlinked, not trashed"
    );
}

#[cfg(unix)]
#[test]
fn a_live_link_leaf_is_handed_over_as_the_link_and_its_target_stays() {
    let f = fixture();
    std::os::unix::fs::symlink(
        f.outside.path().join("note.md"),
        f.vault.path().join("link.md"),
    )
    .unwrap();
    f.provider.delete("link.md").unwrap();
    let root = std::fs::canonicalize(f.vault.path()).unwrap();
    assert_eq!(f.trash.handed(), vec![root.join("link.md")]);
    assert!(
        f.trash
            .bin()
            .join("link.md")
            .symlink_metadata()
            .unwrap()
            .file_type()
            .is_symlink()
    );
    assert_eq!(
        std::fs::read(f.outside.path().join("note.md")).unwrap(),
        b"outside decoy"
    );
}

/// The race the walk alone cannot see: the folder is substituted after the
/// walk pinned it. The pinned descriptor still names the real folder, now
/// outside the vault, so the derivation refuses.
#[cfg(unix)]
#[test]
fn a_pinned_folder_moved_out_of_the_vault_is_refused() {
    let f = fixture();
    let elsewhere = tempfile::tempdir().unwrap();
    let root = std::fs::File::open(f.vault.path()).unwrap();
    let parent = std::fs::File::open(f.vault.path().join("sub")).unwrap();
    std::fs::rename(f.vault.path().join("sub"), elsewhere.path().join("sub")).unwrap();
    std::os::unix::fs::symlink(f.outside.path(), f.vault.path().join("sub")).unwrap();
    let error = pinned_path(&root, &parent, c"note.md").unwrap_err();
    assert!(is_refusal(&error), "{error:?}");
}

/// A pinned folder moved within the vault is still the confirmed object: the
/// handed path follows the descriptor to its new location, never the link now
/// at its old name.
#[cfg(unix)]
#[test]
fn a_pinned_folder_moved_within_the_vault_hands_over_its_new_location() {
    let f = fixture();
    let root = std::fs::File::open(f.vault.path()).unwrap();
    let parent = std::fs::File::open(f.vault.path().join("sub")).unwrap();
    std::fs::rename(f.vault.path().join("sub"), f.vault.path().join("moved")).unwrap();
    std::os::unix::fs::symlink(f.outside.path(), f.vault.path().join("sub")).unwrap();
    let handed = pinned_path(&root, &parent, c"note.md").unwrap();
    let vault = std::fs::canonicalize(f.vault.path()).unwrap();
    assert_eq!(handed, vault.join("moved").join("note.md"));
}

#[cfg(unix)]
#[test]
fn a_pinned_folder_removed_after_the_walk_is_refused() {
    let f = fixture();
    std::fs::create_dir(f.vault.path().join("empty")).unwrap();
    let root = std::fs::File::open(f.vault.path()).unwrap();
    let parent = std::fs::File::open(f.vault.path().join("empty")).unwrap();
    std::fs::remove_dir(f.vault.path().join("empty")).unwrap();
    let error = pinned_path(&root, &parent, c"note.md").unwrap_err();
    assert!(
        matches!(error, VaultError::TrashConfirmationChanged { .. }),
        "{error:?}"
    );
}

#[cfg(unix)]
#[test]
fn a_symlinked_vault_root_still_trashes_beneath_its_target() {
    let real = tempfile::tempdir().unwrap();
    let links = tempfile::tempdir().unwrap();
    std::fs::create_dir(real.path().join("sub")).unwrap();
    std::fs::write(real.path().join("sub/note.md"), b"note").unwrap();
    let root = links.path().join("vault");
    std::os::unix::fs::symlink(real.path(), &root).unwrap();
    let trash = ScratchTrash::new();
    let provider = FsVaultProvider::with_system_trash(root, trash.clone());
    provider.delete("sub/note.md").unwrap();
    let target = std::fs::canonicalize(real.path()).unwrap();
    assert_eq!(trash.handed(), vec![target.join("sub").join("note.md")]);
    assert!(!real.path().join("sub/note.md").exists());
}

#[cfg(windows)]
#[test]
fn delete_refuses_a_folder_turned_into_a_junction_after_the_inventory() {
    let f = fixture();
    let inventory = f.provider.trash_snapshot("sub/note.md").unwrap();
    assert!(!inventory.is_directory);
    std::fs::rename(f.vault.path().join("sub"), f.vault.path().join("sub.real")).unwrap();
    std::fs::create_dir(f.vault.path().join("sub")).unwrap();
    crate::vault::windows_entry::tests::junction(&f.vault.path().join("sub"), f.outside.path());
    let error = f.provider.delete("sub/note.md").unwrap_err();
    assert!(is_refusal(&error), "{error:?}");
    f.assert_nothing_moved();
    std::fs::remove_dir(f.vault.path().join("sub")).unwrap();
}

#[cfg(windows)]
#[test]
fn a_junction_vault_root_still_trashes_beneath_its_target() {
    let real = tempfile::tempdir().unwrap();
    let links = tempfile::tempdir().unwrap();
    std::fs::create_dir(real.path().join("sub")).unwrap();
    std::fs::write(real.path().join("sub/note.md"), b"note").unwrap();
    let root = links.path().join("vault");
    std::fs::create_dir(&root).unwrap();
    crate::vault::windows_entry::tests::junction(&root, real.path());
    let trash = ScratchTrash::new();
    let provider = FsVaultProvider::with_system_trash(root.clone(), trash.clone());
    provider.delete("sub/note.md").unwrap();
    let target = std::fs::canonicalize(real.path()).unwrap();
    assert_eq!(trash.handed(), vec![target.join("sub").join("note.md")]);
    assert!(!real.path().join("sub/note.md").exists());
    std::fs::remove_dir(&root).unwrap();
}

/// The real macOS back end, on a scratch volume only: never a developer's
/// or a runner's own Trash. Create and attach one first:
///
/// ```text
/// hdiutil create -size 40m -fs APFS -volname SlateTrashProbe probe.dmg
/// hdiutil attach -nobrowse -mountpoint "$PWD/mnt" probe.dmg
/// SLATE_TRASH_PROBE_VOLUME="$PWD/mnt" cargo test -p slate-core -- --ignored \
///     real_trash_on_a_scratch_volume
/// hdiutil detach "$PWD/mnt"
/// ```
///
/// Each item must land in the volume's own `.Trashes/<uid>` as the same object
/// (a reference URL to the confirmed item; a link as the link), and Finder's
/// Put Back records (`ptbL`, the original parent; `ptbN`, the original name)
/// must appear in that folder's `.DS_Store` while this process stays alive
/// (#1430 P15). The record format is private; the check reads only those two.
#[cfg(target_os = "macos")]
#[test]
#[ignore = "needs a scratch volume in SLATE_TRASH_PROBE_VOLUME; never a real user's Trash"]
fn real_trash_on_a_scratch_volume_lands_in_its_trashes_with_put_back_records() {
    use std::os::unix::fs::MetadataExt as _;

    let volume = std::path::PathBuf::from(
        std::env::var_os("SLATE_TRASH_PROBE_VOLUME")
            .expect("set SLATE_TRASH_PROBE_VOLUME to a scratch volume's mount point"),
    );
    let volume = std::fs::canonicalize(volume).unwrap();
    // SAFETY: getuid has no preconditions.
    let trashes = volume
        .join(".Trashes")
        .join(unsafe { libc::getuid() }.to_string());
    let name = format!("vault-{}", std::process::id());
    let vault = volume.join(&name);
    std::fs::create_dir_all(vault.join("sub/folder")).unwrap();
    std::fs::write(vault.join("sub/note.md"), b"note").unwrap();
    std::fs::write(vault.join("sub/folder/inner.md"), b"inner").unwrap();
    std::fs::write(vault.join("target.md"), b"target").unwrap();
    std::os::unix::fs::symlink(vault.join("target.md"), vault.join("sub/link.md")).unwrap();
    let identity = |path: &Path| {
        let metadata = path.symlink_metadata().unwrap();
        (metadata.dev(), metadata.ino())
    };
    let items = ["sub/note.md", "sub/folder", "sub/link.md"];
    let identities: Vec<_> = items
        .iter()
        .map(|item| identity(&vault.join(item)))
        .collect();

    let provider = FsVaultProvider::new(vault.clone());
    for item in items {
        provider.delete(item).unwrap();
        assert!(
            vault.join(item).symlink_metadata().is_err(),
            "{item} left the vault"
        );
    }
    assert_eq!(std::fs::read(vault.join("target.md")).unwrap(), b"target");

    let landed: Vec<String> = identities
        .iter()
        .map(|wanted| {
            std::fs::read_dir(&trashes)
                .unwrap()
                .map(|entry| entry.unwrap().path())
                .find(|path| identity(path) == *wanted)
                .unwrap_or_else(|| panic!("no entry in {trashes:?} is the trashed object"))
                .file_name()
                .unwrap()
                .to_string_lossy()
                .into_owned()
        })
        .collect();

    let utf16 =
        |text: &str| -> Vec<u8> { text.encode_utf16().flat_map(u16::to_be_bytes).collect() };
    let record = |entry: &str, code: &[u8; 4], value: &str| -> Vec<u8> {
        let mut bytes = (entry.encode_utf16().count() as u32).to_be_bytes().to_vec();
        bytes.extend(utf16(entry));
        bytes.extend(code);
        bytes.extend(b"ustr");
        bytes.extend((value.encode_utf16().count() as u32).to_be_bytes());
        bytes.extend(utf16(value));
        bytes
    };
    let parent = format!("/{name}/sub/");
    let wanted: Vec<Vec<u8>> = landed
        .iter()
        .zip(items)
        .flat_map(|(entry, item)| {
            let original = item.rsplit('/').next().unwrap();
            [
                record(entry, b"ptbL", &parent),
                record(entry, b"ptbN", original),
            ]
        })
        .collect();
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(60);
    loop {
        let store = std::fs::read(trashes.join(".DS_Store")).unwrap_or_default();
        let missing: Vec<_> = wanted
            .iter()
            .filter(|record| {
                !store
                    .windows(record.len())
                    .any(|window| window == record.as_slice())
            })
            .collect();
        if missing.is_empty() {
            break;
        }
        assert!(
            std::time::Instant::now() < deadline,
            "{} of {} Put Back records never appeared for {landed:?}",
            missing.len(),
            wanted.len()
        );
        std::thread::sleep(std::time::Duration::from_millis(500));
    }
}
