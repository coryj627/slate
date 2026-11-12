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

/// The typed refusal for `sub/…` (the #1449 shape): `InvalidPath` for that
/// path, with core's reason containing `needle`.
fn assert_refused(error: &VaultError, path: &str, needle: &str) {
    assert!(
        matches!(error, VaultError::InvalidPath { path: refused, reason }
            if refused == path && reason.contains(needle)),
        "expected the refusal of {path:?} saying {needle:?}, got {error:?}"
    );
}

/// The derivation's refusal: a pinned folder moved after the walk.
#[cfg(unix)]
const MOVED: &str = "a folder in this path moved after it was checked, so nothing was moved to the \
                     Trash";

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
    assert_refused(&error, "sub/note.md", "\"sub\" is a symbolic link");
    f.assert_nothing_moved();
}

#[cfg(unix)]
#[test]
fn delete_refuses_a_folder_replaced_by_a_file() {
    let f = fixture();
    std::fs::rename(f.vault.path().join("sub"), f.vault.path().join("sub.real")).unwrap();
    std::fs::write(f.vault.path().join("sub"), b"not a folder").unwrap();
    let error = f.provider.delete("sub/note.md").unwrap_err();
    assert_refused(&error, "sub/note.md", "\"sub\" is not a folder");
    f.assert_nothing_moved();
}

/// A dangling link behind a folder swapped for a link refuses like any other
/// item, and nothing outside the vault is trashed or removed. Before #1450 a
/// dangling link was unlinked through the lexical path, so a substituted
/// folder made it delete, for good, an entry outside the vault. Since #1495 it
/// goes to the Trash through the same pinned walk as every item.
#[cfg(unix)]
#[test]
fn delete_never_reaches_a_dangling_link_outside_the_vault_through_a_swapped_folder() {
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
    assert_refused(&error, "sub/dangling.md", "\"sub\" is a symbolic link");
    assert!(
        f.outside
            .path()
            .join("dangling.md")
            .symlink_metadata()
            .is_ok(),
        "the outside link stays where it is"
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

/// What following `link` reports, so each fact's fixture is the kind it
/// names. Before #1495, `delete` took this look on macOS and Linux to decide
/// between unlinking a link and trashing it; now it trashes every link.
#[cfg(unix)]
fn follow_errno(link: &Path) -> Option<i32> {
    std::fs::metadata(link)
        .err()
        .and_then(|error| error.raw_os_error())
}

/// A folder locked to mode 000 until this drops, so a fact that fails still
/// leaves a temporary directory its cleanup can remove.
#[cfg(unix)]
struct Locked(PathBuf);

#[cfg(unix)]
impl Locked {
    fn new(folder: &Path) -> Self {
        use std::os::unix::fs::PermissionsExt as _;
        std::fs::set_permissions(folder, std::fs::Permissions::from_mode(0o000)).unwrap();
        Self(folder.to_path_buf())
    }
}

#[cfg(unix)]
impl Drop for Locked {
    fn drop(&mut self) {
        use std::os::unix::fs::PermissionsExt as _;
        let _ = std::fs::set_permissions(&self.0, std::fs::Permissions::from_mode(0o700));
    }
}

/// A chain of `links` symbolic links in `folder`, `chain0.md` to
/// `chain1.md` and on, the last pointing at `end`: longer than the kernel
/// follows (32 links on macOS, 40 on Linux) once `links` exceeds that.
#[cfg(unix)]
fn link_chain(folder: &Path, links: usize, end: &str) {
    for index in 0..links {
        let target = if index + 1 == links {
            end.to_string()
        } else {
            format!("chain{}.md", index + 1)
        };
        std::os::unix::fs::symlink(target, folder.join(format!("chain{index}.md"))).unwrap();
    }
}

/// The scratch bin holds `name` as the link itself: a symbolic link whose
/// text is still `target`, so Put Back would restore the link it was.
fn assert_in_the_bin_as_the_link(trash: &ScratchTrash, name: &str, target: &Path) {
    let landed = trash.bin().join(name);
    let metadata = landed.symlink_metadata().unwrap_or_else(|error| {
        panic!("{name} is not in the bin ({error}): {:?}", trash.contents())
    });
    assert!(
        metadata.file_type().is_symlink(),
        "{name} reached the bin, but not as a link"
    );
    assert_eq!(std::fs::read_link(&landed).unwrap(), target, "{name}");
}

/// #1477: a link whose target exists behind a folder Slate cannot search is
/// not dangling. Following it fails with `EACCES`, and before #1477 every
/// failure counted as dangling, so the link was unlinked for good, with
/// nothing for Put Back to restore and no error. Now it goes to the Trash,
/// which moves the link itself and never touches the target.
#[cfg(unix)]
#[test]
fn a_link_whose_target_cannot_be_searched_goes_to_the_trash_as_the_link() {
    let f = fixture();
    let locked = f.outside.path().join("locked");
    let to_file = locked.join("target.md");
    let to_folder = locked.join("folder");
    std::fs::create_dir_all(&to_folder).unwrap();
    std::fs::write(&to_file, b"behind a locked folder").unwrap();
    std::os::unix::fs::symlink(&to_file, f.vault.path().join("sub/unreachable.md")).unwrap();
    std::os::unix::fs::symlink(&to_folder, f.vault.path().join("unreachable-folder")).unwrap();
    let lock = Locked::new(&locked);
    for link in ["sub/unreachable.md", "unreachable-folder"] {
        assert_eq!(
            follow_errno(&f.vault.path().join(link)),
            Some(libc::EACCES),
            "{link} must fail to resolve with EACCES"
        );
    }

    f.provider.delete("sub/unreachable.md").unwrap();
    f.provider.delete("unreachable-folder").unwrap();

    let root = std::fs::canonicalize(f.vault.path()).unwrap();
    assert_eq!(
        f.trash.handed(),
        vec![
            root.join("sub").join("unreachable.md"),
            root.join("unreachable-folder")
        ],
        "both links are handed to the Trash, never unlinked"
    );
    assert_in_the_bin_as_the_link(&f.trash, "unreachable.md", &to_file);
    assert_in_the_bin_as_the_link(&f.trash, "unreachable-folder", &to_folder);
    drop(lock);
    assert_eq!(std::fs::read(&to_file).unwrap(), b"behind a locked folder");
    assert!(to_folder.is_dir());
}

/// #1495: Move to Trash never deletes for good on macOS or Linux. A link
/// whose target is truly absent (following it fails with `ENOENT`, nothing is
/// there, or `ENOTDIR`, its path runs through a file) used to be unlinked
/// inside its pinned parent (#1450, #1477), with nothing for Put Back to
/// restore, although a missing target can be only out of reach, a file on a
/// volume that is not mounted now. It now goes to the Trash like every other
/// item, handed over as the pinned parent's own current path with its name,
/// and lands there as the link.
#[cfg(unix)]
#[test]
fn a_link_whose_target_is_truly_absent_goes_to_the_trash_as_the_link() {
    let f = fixture();
    let vault = f.vault.path();
    let missing = vault.join("gone.md");
    let through_a_file = vault.join("sub/note.md/x");
    std::os::unix::fs::symlink(&missing, vault.join("sub/missing.md")).unwrap();
    std::os::unix::fs::symlink(&through_a_file, vault.join("through-a-file.md")).unwrap();
    assert_eq!(
        follow_errno(&vault.join("sub/missing.md")),
        Some(libc::ENOENT)
    );
    assert_eq!(
        follow_errno(&vault.join("through-a-file.md")),
        Some(libc::ENOTDIR)
    );

    for link in ["sub/missing.md", "through-a-file.md"] {
        f.provider.delete(link).unwrap();
        assert!(
            vault.join(link).symlink_metadata().is_err(),
            "{link} left the vault"
        );
    }

    let root = std::fs::canonicalize(vault).unwrap();
    assert_eq!(
        f.trash.handed(),
        vec![
            root.join("sub").join("missing.md"),
            root.join("through-a-file.md")
        ],
        "both links are handed to the Trash through their pinned parent, never unlinked"
    );
    assert_in_the_bin_as_the_link(&f.trash, "missing.md", &missing);
    assert_in_the_bin_as_the_link(&f.trash, "through-a-file.md", &through_a_file);
    assert_eq!(
        std::fs::read(vault.join("sub/note.md")).unwrap(),
        b"vault note"
    );
}

/// #1477, decided: a link that fails to resolve with `ELOOP` goes to the
/// Trash. The kernel reports `ELOOP` for a cycle, which never resolves, but
/// also for a chain of links longer than it follows (32 on macOS, 40 on
/// Linux), and such a chain can end at a real file. So `ELOOP` does not prove
/// the target absent. The Trash moves the link itself, cycle or chain, and
/// keeps it restorable.
#[cfg(unix)]
#[test]
fn a_link_cycle_or_a_chain_too_long_to_follow_goes_to_the_trash_as_the_link() {
    const CHAIN: usize = 50;
    let f = fixture();
    let vault = f.vault.path();
    std::os::unix::fs::symlink("self.md", vault.join("self.md")).unwrap();
    std::os::unix::fs::symlink("cycle-b.md", vault.join("cycle-a.md")).unwrap();
    std::os::unix::fs::symlink("cycle-a.md", vault.join("cycle-b.md")).unwrap();
    std::fs::write(vault.join("end.md"), b"the chain's end").unwrap();
    link_chain(vault, CHAIN, "end.md");
    for link in ["self.md", "cycle-a.md", "chain0.md"] {
        assert_eq!(
            follow_errno(&vault.join(link)),
            Some(libc::ELOOP),
            "{link} must fail to resolve with ELOOP"
        );
    }
    assert_eq!(
        std::fs::read(vault.join(format!("chain{}.md", CHAIN - 20))).unwrap(),
        b"the chain's end",
        "the chain ends at a real file: twenty links from its end it resolves"
    );

    for link in ["self.md", "cycle-a.md", "chain0.md"] {
        f.provider.delete(link).unwrap();
    }

    let root = std::fs::canonicalize(vault).unwrap();
    assert_eq!(
        f.trash.handed(),
        vec![
            root.join("self.md"),
            root.join("cycle-a.md"),
            root.join("chain0.md")
        ]
    );
    assert_in_the_bin_as_the_link(&f.trash, "self.md", Path::new("self.md"));
    assert_in_the_bin_as_the_link(&f.trash, "cycle-a.md", Path::new("cycle-b.md"));
    assert_in_the_bin_as_the_link(&f.trash, "chain0.md", Path::new("chain1.md"));
    assert!(
        vault
            .join("cycle-b.md")
            .symlink_metadata()
            .unwrap()
            .file_type()
            .is_symlink(),
        "the cycle's other link stays in the vault"
    );
    assert_eq!(
        std::fs::read(vault.join("end.md")).unwrap(),
        b"the chain's end"
    );
}

/// #1477: any other failure to follow a link leaves its target unproven
/// absent, so the link goes to the Trash too. A target whose name is longer
/// than a filesystem allows fails with `ENAMETOOLONG`. Since #1495 macOS and
/// Linux do not follow a link at all before the Trash: every link goes there,
/// whatever following it would report (`EIO`, `ESTALE`, `ETIMEDOUT`, which no
/// fixture can provoke, included).
#[cfg(unix)]
#[test]
fn a_link_that_fails_to_resolve_for_any_other_reason_goes_to_the_trash_as_the_link() {
    let f = fixture();
    let target = "x".repeat(300);
    std::os::unix::fs::symlink(&target, f.vault.path().join("sub/overlong.md")).unwrap();
    assert_eq!(
        follow_errno(&f.vault.path().join("sub/overlong.md")),
        Some(libc::ENAMETOOLONG)
    );

    f.provider.delete("sub/overlong.md").unwrap();

    let root = std::fs::canonicalize(f.vault.path()).unwrap();
    assert_eq!(f.trash.handed(), vec![root.join("sub").join("overlong.md")]);
    assert_in_the_bin_as_the_link(&f.trash, "overlong.md", Path::new(&target));
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
    let error = pinned_path(&root, &parent, c"note.md", "sub/note.md").unwrap_err();
    assert_refused(&error, "sub/note.md", MOVED);
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
    let handed = pinned_path(&root, &parent, c"note.md", "sub/note.md").unwrap();
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
    let error = pinned_path(&root, &parent, c"note.md", "empty/note.md").unwrap_err();
    // `F_GETPATH` may still report the removed folder's last path (then it
    // no longer names the descriptor) or fail (then nothing can be checked).
    assert!(
        matches!(&error, VaultError::InvalidPath { path, reason }
            if path == "empty/note.md"
                && (reason == MOVED || reason.contains("could not confirm"))),
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
    crate::vault::test_support::junction(&f.vault.path().join("sub"), f.outside.path());
    let error = f.provider.delete("sub/note.md").unwrap_err();
    assert_refused(
        &error,
        "sub/note.md",
        "\"sub\" is a junction, not a folder in this vault, so nothing was moved",
    );
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
    crate::vault::test_support::junction(&root, real.path());
    let trash = ScratchTrash::new();
    let provider = FsVaultProvider::with_system_trash(root.clone(), trash.clone());
    provider.delete("sub/note.md").unwrap();
    let target = std::fs::canonicalize(real.path()).unwrap();
    assert_eq!(trash.handed(), vec![target.join("sub").join("note.md")]);
    assert!(!real.path().join("sub/note.md").exists());
    std::fs::remove_dir(&root).unwrap();
}

/// Windows' rule (#1477), error by error: only the codes for an absent target
/// make a link dangling, so Windows unlinks it until the Recycle Bin is
/// checked with one (#1499). Every other failure to follow a link, including
/// the I/O and network failures no fixture can provoke, leaves its target
/// unproven absent, and the link goes to the Recycle Bin. macOS and Linux
/// trash every link (#1495).
#[cfg(windows)]
#[test]
fn only_a_target_windows_reports_absent_makes_a_link_dangling() {
    use windows_sys::Win32::Foundation::{
        ERROR_ACCESS_DENIED, ERROR_BAD_NET_NAME, ERROR_BAD_NETPATH, ERROR_CANT_RESOLVE_FILENAME,
        ERROR_DIRECTORY, ERROR_FILE_NOT_FOUND, ERROR_INVALID_DRIVE, ERROR_INVALID_NAME,
        ERROR_NOT_READY, ERROR_PATH_NOT_FOUND, ERROR_SHARING_VIOLATION,
        ERROR_SYMLINK_CLASS_DISABLED,
    };
    let absent = [
        ERROR_FILE_NOT_FOUND as i32,
        ERROR_PATH_NOT_FOUND as i32,
        ERROR_DIRECTORY as i32,
    ];
    let unproven = [
        ERROR_ACCESS_DENIED as i32,
        ERROR_SHARING_VIOLATION as i32,
        ERROR_CANT_RESOLVE_FILENAME as i32,
        ERROR_INVALID_NAME as i32,
        ERROR_INVALID_DRIVE as i32,
        ERROR_BAD_NETPATH as i32,
        ERROR_BAD_NET_NAME as i32,
        ERROR_NOT_READY as i32,
        ERROR_SYMLINK_CLASS_DISABLED as i32,
    ];
    for code in absent {
        let error = std::io::Error::from_raw_os_error(code);
        assert!(link_target_is_absent(&error), "{error}: an absent target");
    }
    for code in unproven {
        let error = std::io::Error::from_raw_os_error(code);
        assert!(
            !link_target_is_absent(&error),
            "{error}: not proof of absence"
        );
    }
    assert!(!link_target_is_absent(&std::io::Error::other(
        "a failure with no OS code"
    )));
}

/// Denies everyone every right on `file` until this drops (`icacls`), so
/// nothing can open it, and its owner can still lift the denial.
#[cfg(windows)]
struct Denied(PathBuf);

#[cfg(windows)]
impl Denied {
    fn new(file: &Path) -> Self {
        let denied = Self(file.to_path_buf());
        denied.icacls(&["/deny", "*S-1-1-0:(F)"]);
        denied
    }

    fn icacls(&self, arguments: &[&str]) {
        let output = std::process::Command::new("icacls")
            .arg(&self.0)
            .args(arguments)
            .output()
            .unwrap();
        assert!(
            output.status.success(),
            "icacls {arguments:?}: {}",
            String::from_utf8_lossy(&output.stdout)
        );
    }
}

#[cfg(windows)]
impl Drop for Denied {
    fn drop(&mut self) {
        let _ = std::process::Command::new("icacls")
            .arg(&self.0)
            .args(["/remove:d", "*S-1-1-0"])
            .output();
    }
}

/// #1477 on Windows: a link whose target is missing is still unlinked, a
/// file link with `DeleteFile` and a folder link with `RemoveDirectory`, and
/// nothing reaches the Recycle Bin. macOS and Linux trash such a link (#1495);
/// Windows keeps unlinking it until the Recycle Bin is checked with one on a
/// scratch VM (#1499).
#[cfg(windows)]
#[test]
fn windows_a_link_whose_target_is_missing_is_still_unlinked() {
    let f = fixture();
    let vault = f.vault.path();
    std::os::windows::fs::symlink_file(vault.join("gone.md"), vault.join("sub/missing.md"))
        .unwrap();
    std::os::windows::fs::symlink_dir(vault.join("gone"), vault.join("missing-folder")).unwrap();
    for link in ["sub/missing.md", "missing-folder"] {
        let error = std::fs::metadata(vault.join(link)).unwrap_err();
        assert!(link_target_is_absent(&error), "{link}: {error:?}");
        f.provider.delete(link).unwrap();
        assert!(
            vault.join(link).symlink_metadata().is_err(),
            "{link} is unlinked"
        );
    }
    assert!(f.trash.handed().is_empty(), "{:?}", f.trash.handed());
}

/// #1477 on Windows: a link whose target exists but cannot be opened goes to
/// the Recycle Bin as the link. Once the target denies everyone, following
/// the link fails with `ERROR_ACCESS_DENIED`; `Path::exists()` was false for
/// it, so the link was unlinked for good.
#[cfg(windows)]
#[test]
fn windows_a_link_whose_target_cannot_be_opened_goes_to_the_recycle_bin_as_the_link() {
    use windows_sys::Win32::Foundation::ERROR_ACCESS_DENIED;

    let f = fixture();
    let target = f.outside.path().join("denied.md");
    std::fs::write(&target, b"denied to everyone").unwrap();
    let link = f.vault.path().join("sub/unreachable.md");
    std::os::windows::fs::symlink_file(&target, &link).unwrap();
    let text = std::fs::read_link(&link).unwrap();
    let denied = Denied::new(&target);
    let error = std::fs::metadata(&link).unwrap_err();
    assert_eq!(
        error.raw_os_error(),
        Some(ERROR_ACCESS_DENIED as i32),
        "{error:?}"
    );

    f.provider.delete("sub/unreachable.md").unwrap();

    let root = std::fs::canonicalize(f.vault.path()).unwrap();
    assert_eq!(
        f.trash.handed(),
        vec![root.join("sub").join("unreachable.md")],
        "the link is handed to the Recycle Bin, never unlinked"
    );
    assert_in_the_bin_as_the_link(&f.trash, "unreachable.md", &text);
    drop(denied);
    assert_eq!(std::fs::read(&target).unwrap(), b"denied to everyone");
}

/// #1477 on Windows, as `ELOOP` on Unix: following a link cycle fails with
/// `ERROR_CANT_RESOLVE_FILENAME`, which does not prove a target absent, so
/// the link goes to the Recycle Bin as the link.
#[cfg(windows)]
#[test]
fn windows_a_link_cycle_goes_to_the_recycle_bin_as_the_link() {
    use windows_sys::Win32::Foundation::ERROR_CANT_RESOLVE_FILENAME;

    let f = fixture();
    let vault = f.vault.path();
    std::os::windows::fs::symlink_file("cycle-b.md", vault.join("cycle-a.md")).unwrap();
    std::os::windows::fs::symlink_file("cycle-a.md", vault.join("cycle-b.md")).unwrap();
    let text = std::fs::read_link(vault.join("cycle-a.md")).unwrap();
    let error = std::fs::metadata(vault.join("cycle-a.md")).unwrap_err();
    assert_eq!(
        error.raw_os_error(),
        Some(ERROR_CANT_RESOLVE_FILENAME as i32),
        "{error:?}"
    );

    f.provider.delete("cycle-a.md").unwrap();

    let root = std::fs::canonicalize(vault).unwrap();
    assert_eq!(f.trash.handed(), vec![root.join("cycle-a.md")]);
    assert_in_the_bin_as_the_link(&f.trash, "cycle-a.md", &text);
    assert!(
        vault
            .join("cycle-b.md")
            .symlink_metadata()
            .unwrap()
            .file_type()
            .is_symlink(),
        "the cycle's other link stays in the vault"
    );
}

/// The one way a test builds a provider whose Trash is the real system Trash
/// (#1455), for the gated real-Trash set below and nowhere else
/// (`vault::test_trash_census` names the set and fails on any other caller).
/// Every other fact in core trashes into a `ScratchTrash`, which
/// `FsVaultProvider::new` gives every provider in a test build. These facts
/// run only on the scratch volume they name in `SLATE_TRASH_PROBE_VOLUME`,
/// and in a test build the system Trash refuses any path off it.
#[cfg(any(target_os = "macos", target_os = "linux", windows))]
fn real_system_trash_provider(root: std::path::PathBuf) -> FsVaultProvider {
    FsVaultProvider::with_system_trash(root, Arc::new(PlatformTrash))
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
    let (volume, trashes) = probe_volume_and_its_trashes();
    let name = format!("vault-{}", std::process::id());
    let vault = volume.join(&name);
    std::fs::create_dir_all(vault.join("sub/folder")).unwrap();
    std::fs::write(vault.join("sub/note.md"), b"note").unwrap();
    std::fs::write(vault.join("sub/folder/inner.md"), b"inner").unwrap();
    std::fs::write(vault.join("target.md"), b"target").unwrap();
    std::os::unix::fs::symlink(vault.join("target.md"), vault.join("sub/link.md")).unwrap();
    let items = ["sub/note.md", "sub/folder", "sub/link.md"];
    let identities: Vec<_> = items
        .iter()
        .map(|item| identity(&vault.join(item)))
        .collect();

    let provider = real_system_trash_provider(vault.clone());
    for item in items {
        provider.delete(item).unwrap();
        assert!(
            vault.join(item).symlink_metadata().is_err(),
            "{item} left the vault"
        );
    }
    assert_eq!(std::fs::read(vault.join("target.md")).unwrap(), b"target");

    let landed = trashed_entries(&trashes, &identities);
    await_put_back_records(&trashes, &format!("/{name}/sub/"), &landed, &items);
}

/// The scratch volume the gated macOS facts run on, and its Trash for this
/// user, `.Trashes/<uid>`.
#[cfg(target_os = "macos")]
fn probe_volume_and_its_trashes() -> (PathBuf, PathBuf) {
    let volume = std::fs::canonicalize(
        std::env::var_os("SLATE_TRASH_PROBE_VOLUME")
            .expect("set SLATE_TRASH_PROBE_VOLUME to a scratch volume's mount point"),
    )
    .unwrap();
    // SAFETY: getuid has no preconditions.
    let trashes = volume
        .join(".Trashes")
        .join(unsafe { libc::getuid() }.to_string());
    (volume, trashes)
}

/// An entry's identity, never following a final link.
#[cfg(target_os = "macos")]
fn identity(path: &Path) -> (u64, u64) {
    use std::os::unix::fs::MetadataExt as _;
    let metadata = path.symlink_metadata().unwrap();
    (metadata.dev(), metadata.ino())
}

/// The name in `trashes` of each object in `identities`: every trashed item
/// must land there as the same object.
#[cfg(target_os = "macos")]
fn trashed_entries(trashes: &Path, identities: &[(u64, u64)]) -> Vec<String> {
    identities
        .iter()
        .map(|wanted| {
            std::fs::read_dir(trashes)
                .unwrap()
                .map(|entry| entry.unwrap().path())
                .find(|path| identity(path) == *wanted)
                .unwrap_or_else(|| panic!("no entry in {trashes:?} is the trashed object"))
                .file_name()
                .unwrap()
                .to_string_lossy()
                .into_owned()
        })
        .collect()
}

/// Waits up to a minute for Finder's Put Back records of each `landed` entry,
/// trashed from `items` in the volume-relative folder `parent`: `ptbL` (the
/// original parent) and `ptbN` (the original name) in `trashes/.DS_Store`,
/// which appear while this process stays alive (#1430 P15). The record
/// format is private; the check reads only those two.
#[cfg(target_os = "macos")]
fn await_put_back_records(trashes: &Path, parent: &str, landed: &[String], items: &[&str]) {
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
    let wanted: Vec<Vec<u8>> = landed
        .iter()
        .zip(items)
        .flat_map(|(entry, item)| {
            let original = item.rsplit('/').next().unwrap();
            [
                record(entry, b"ptbL", parent),
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

/// One link of each unresolvable kind, planted in `folder`: one to a file in
/// `locked`, which the caller locks (`EACCES`); one to itself and the head of
/// a chain of 50 links that ends at a real file (`ELOOP`); one whose target's
/// name is too long (`ENAMETOOLONG`); one to a missing file (`ENOENT`); and
/// one whose target's path runs through a file (`ENOTDIR`). macOS and Linux
/// trash every one as the link (#1477, #1495). Each name, with its text.
#[cfg(any(target_os = "macos", target_os = "linux"))]
fn plant_unresolvable_links(folder: &Path, locked: &Path) -> Vec<(&'static str, PathBuf)> {
    std::fs::create_dir_all(locked).unwrap();
    std::fs::write(locked.join("target.md"), b"behind a locked folder").unwrap();
    std::os::unix::fs::symlink(locked.join("target.md"), folder.join("unreachable.md")).unwrap();
    std::os::unix::fs::symlink("cycle.md", folder.join("cycle.md")).unwrap();
    std::fs::write(folder.join("end.md"), b"the chain's end").unwrap();
    link_chain(folder, 50, "end.md");
    std::os::unix::fs::symlink("x".repeat(300), folder.join("overlong.md")).unwrap();
    std::os::unix::fs::symlink("gone.md", folder.join("dangling.md")).unwrap();
    std::os::unix::fs::symlink("end.md/x", folder.join("through-a-file.md")).unwrap();
    [
        "unreachable.md",
        "cycle.md",
        "chain0.md",
        "overlong.md",
        "dangling.md",
        "through-a-file.md",
    ]
    .into_iter()
    .map(|name| (name, std::fs::read_link(folder.join(name)).unwrap()))
    .collect()
}

/// #1477 on the real macOS back end, on a scratch volume only (made and
/// attached as for
/// `real_trash_on_a_scratch_volume_lands_in_its_trashes_with_put_back_records`,
/// and run by the same filter). `delete` hands `NSFileManager` each link
/// of every unresolvable kind (`plant_unresolvable_links`), the truly
/// dangling ones included since #1495: each lands in the volume's
/// `.Trashes/<uid>` as the same object, a link whose text is unchanged, with
/// its Put Back records, and the target behind the locked folder is
/// untouched.
#[cfg(target_os = "macos")]
#[test]
#[ignore = "needs a scratch volume in SLATE_TRASH_PROBE_VOLUME; never a real user's Trash"]
fn real_trash_on_a_scratch_volume_takes_each_unresolvable_link_as_the_link() {
    let (volume, trashes) = probe_volume_and_its_trashes();
    let name = format!("links-{}", std::process::id());
    let vault = volume.join(&name);
    let sub = vault.join("sub");
    std::fs::create_dir_all(&sub).unwrap();
    let locked = volume.join(format!("locked-{}", std::process::id()));
    let links = plant_unresolvable_links(&sub, &locked);
    let lock = Locked::new(&locked);
    for (link, errno) in [
        ("unreachable.md", libc::EACCES),
        ("dangling.md", libc::ENOENT),
        ("through-a-file.md", libc::ENOTDIR),
    ] {
        assert_eq!(follow_errno(&sub.join(link)), Some(errno), "{link}");
    }
    let items: Vec<String> = links
        .iter()
        .map(|(link, _)| format!("sub/{link}"))
        .collect();
    let items: Vec<&str> = items.iter().map(String::as_str).collect();
    let identities: Vec<_> = items
        .iter()
        .map(|item| identity(&vault.join(item)))
        .collect();

    let provider = real_system_trash_provider(vault.clone());
    for item in items.iter().copied() {
        provider.delete(item).unwrap();
        assert!(
            vault.join(item).symlink_metadata().is_err(),
            "{item} left the vault"
        );
    }

    let landed = trashed_entries(&trashes, &identities);
    for (entry, (link, text)) in landed.iter().zip(&links) {
        let path = trashes.join(entry);
        assert!(
            path.symlink_metadata().unwrap().file_type().is_symlink(),
            "{link} reached the Trash as the link"
        );
        assert_eq!(&std::fs::read_link(&path).unwrap(), text, "{link}");
    }
    await_put_back_records(&trashes, &format!("/{name}/sub/"), &landed, &items);
    drop(lock);
    assert_eq!(
        std::fs::read(locked.join("target.md")).unwrap(),
        b"behind a locked folder"
    );
}

/// The child half of `real_trash_from_many_processes_at_once_loses_nothing`:
/// at the instant in `SLATE_TRASH_RACE_START` (Unix milliseconds) it trashes
/// `Ghost.md` from the vault in `SLATE_TRASH_RACE_VAULT`. Without those it
/// does nothing.
#[cfg(target_os = "macos")]
#[test]
fn real_trash_race_child() {
    let Some(vault) = std::env::var_os("SLATE_TRASH_RACE_VAULT") else {
        return;
    };
    let start: u128 = std::env::var("SLATE_TRASH_RACE_START")
        .unwrap()
        .parse()
        .unwrap();
    let provider = real_system_trash_provider(vault.into());
    while std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_millis()
        < start
    {
        std::thread::sleep(std::time::Duration::from_micros(200));
    }
    provider.delete("Ghost.md").unwrap();
}

/// Many Slate processes trashing same-named items at one instant, on a
/// scratch volume (made as for the fact above). `NSFileManager` alone lost
/// about half of such items in the Trash; `SerializedTrash` must keep every
/// one.
#[cfg(target_os = "macos")]
#[test]
#[ignore = "needs a scratch volume in SLATE_TRASH_PROBE_VOLUME; never a real user's Trash"]
fn real_trash_from_many_processes_at_once_loses_nothing() {
    use std::os::unix::fs::MetadataExt as _;

    const PROCESSES: usize = 16;
    let volume = std::fs::canonicalize(
        std::env::var_os("SLATE_TRASH_PROBE_VOLUME")
            .expect("set SLATE_TRASH_PROBE_VOLUME to a scratch volume's mount point"),
    )
    .unwrap();
    // SAFETY: getuid has no preconditions.
    let trashes = volume
        .join(".Trashes")
        .join(unsafe { libc::getuid() }.to_string());
    let identity = |path: &Path| {
        let metadata = path.symlink_metadata().unwrap();
        (metadata.dev(), metadata.ino())
    };
    let vaults: Vec<_> = (0..PROCESSES)
        .map(|index| {
            let vault = volume.join(format!("race-{}-{index}", std::process::id()));
            std::fs::create_dir_all(&vault).unwrap();
            std::fs::write(vault.join("Ghost.md"), format!("ghost {index}")).unwrap();
            vault
        })
        .collect();
    let identities: Vec<_> = vaults
        .iter()
        .map(|vault| identity(&vault.join("Ghost.md")))
        .collect();
    let start = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_millis()
        + 2_000;
    let children: Vec<_> = vaults
        .iter()
        .map(|vault| {
            std::process::Command::new(std::env::current_exe().unwrap())
                .args([
                    "vault::trash_handoff::tests::real_trash_race_child",
                    "--exact",
                    "--nocapture",
                ])
                .env("SLATE_TRASH_RACE_VAULT", vault)
                .env("SLATE_TRASH_RACE_START", start.to_string())
                .spawn()
                .unwrap()
        })
        .collect();
    for mut child in children {
        assert!(child.wait().unwrap().success(), "a child's Trash failed");
    }
    for vault in &vaults {
        assert!(!vault.join("Ghost.md").exists(), "{vault:?} kept its item");
    }
    let in_trash: std::collections::HashSet<_> = std::fs::read_dir(&trashes)
        .unwrap()
        .map(|entry| identity(&entry.unwrap().path()))
        .collect();
    let lost = identities
        .iter()
        .filter(|wanted| !in_trash.contains(wanted))
        .count();
    assert_eq!(
        lost, 0,
        "{lost} of {PROCESSES} trashed items are no longer in the Trash"
    );
}

/// The child half of the Linux real-Trash facts below: it trashes each item
/// in `SLATE_TRASH_LINUX_ITEMS` (comma-separated;
/// `sub/note.md` when unset) from the vault in `SLATE_TRASH_LINUX_VAULT`
/// through the real system Trash, with the environment its parent set.
/// Without the vault variable it does nothing.
#[cfg(target_os = "linux")]
#[test]
fn real_trash_linux_child() {
    let Some(vault) = std::env::var_os("SLATE_TRASH_LINUX_VAULT") else {
        return;
    };
    let items =
        std::env::var("SLATE_TRASH_LINUX_ITEMS").unwrap_or_else(|_| "sub/note.md".to_string());
    let provider = real_system_trash_provider(vault.into());
    for item in items.split(',') {
        provider.delete(item).unwrap();
    }
}

/// The real Linux back end, trash-rs's freedesktop Trash, on every Linux run
/// and still never a runner's own Trash (#1455). A child process trashes with
/// `XDG_DATA_HOME` inside a scratch directory on the vault's own mount, so
/// trash-rs takes that directory's `Trash` for the home Trash: the item lands
/// in its `files` and its record in its `info`, not in `~/.local/share/Trash`.
#[cfg(target_os = "linux")]
#[test]
fn real_trash_on_linux_lands_in_a_scratch_home_trash() {
    let scratch = tempfile::tempdir().unwrap();
    let scratch_path = std::fs::canonicalize(scratch.path()).unwrap();
    let vault = scratch_path.join("vault");
    std::fs::create_dir_all(vault.join("sub")).unwrap();
    std::fs::write(vault.join("sub/note.md"), b"linux note").unwrap();
    let data_home = scratch_path.join("data");
    std::fs::create_dir(&data_home).unwrap();

    let status = std::process::Command::new(std::env::current_exe().unwrap())
        .args([
            "vault::trash_handoff::tests::real_trash_linux_child",
            "--exact",
            "--nocapture",
        ])
        .env("SLATE_TRASH_LINUX_VAULT", &vault)
        .env("XDG_DATA_HOME", &data_home)
        .env(PROBE_VOLUME, &scratch_path)
        .status()
        .unwrap();
    assert!(status.success(), "the child's Trash failed");

    assert!(
        vault.join("sub/note.md").symlink_metadata().is_err(),
        "the note left the vault"
    );
    let trash = data_home.join("Trash");
    assert_eq!(
        std::fs::read(trash.join("files/note.md")).unwrap(),
        b"linux note"
    );
    let record = std::fs::read_to_string(trash.join("info/note.md.trashinfo")).unwrap();
    assert!(record.starts_with("[Trash Info]"), "{record}");
    assert!(record.contains("/vault/sub/note.md"), "{record}");
}

/// #1477 on the real Linux back end, trash-rs's freedesktop Trash, through
/// `real_trash_linux_child` with a scratch home Trash, on every Linux run.
/// A link of every unresolvable kind (`plant_unresolvable_links`), the truly
/// dangling ones included since #1495, lands in that Trash's `files` as the
/// link, its text unchanged, with the `.trashinfo` record that restores it,
/// and the target behind the locked folder is untouched.
#[cfg(target_os = "linux")]
#[test]
fn real_trash_on_linux_takes_each_unresolvable_link_as_the_link() {
    let scratch = tempfile::tempdir().unwrap();
    let scratch_path = std::fs::canonicalize(scratch.path()).unwrap();
    let sub = scratch_path.join("vault/sub");
    std::fs::create_dir_all(&sub).unwrap();
    let locked = scratch_path.join("locked");
    let links = plant_unresolvable_links(&sub, &locked);
    let data_home = scratch_path.join("data");
    std::fs::create_dir(&data_home).unwrap();
    let lock = Locked::new(&locked);
    for (link, errno) in [
        ("unreachable.md", libc::EACCES),
        ("dangling.md", libc::ENOENT),
        ("through-a-file.md", libc::ENOTDIR),
    ] {
        assert_eq!(follow_errno(&sub.join(link)), Some(errno), "{link}");
    }
    let items: Vec<String> = links
        .iter()
        .map(|(link, _)| format!("sub/{link}"))
        .collect();

    let status = std::process::Command::new(std::env::current_exe().unwrap())
        .args([
            "vault::trash_handoff::tests::real_trash_linux_child",
            "--exact",
            "--nocapture",
        ])
        .env("SLATE_TRASH_LINUX_VAULT", scratch_path.join("vault"))
        .env("SLATE_TRASH_LINUX_ITEMS", items.join(","))
        .env("XDG_DATA_HOME", &data_home)
        .env(PROBE_VOLUME, &scratch_path)
        .status()
        .unwrap();
    assert!(status.success(), "the child's Trash failed");

    let trash = data_home.join("Trash");
    for (link, text) in &links {
        assert!(
            sub.join(link).symlink_metadata().is_err(),
            "{link} left the vault"
        );
        let landed = trash.join("files").join(link);
        assert!(
            landed.symlink_metadata().unwrap().file_type().is_symlink(),
            "{link} reached the Trash as the link"
        );
        assert_eq!(&std::fs::read_link(&landed).unwrap(), text, "{link}");
        let record =
            std::fs::read_to_string(trash.join("info").join(format!("{link}.trashinfo"))).unwrap();
        assert!(record.contains(&format!("/vault/sub/{link}")), "{record}");
    }
    drop(lock);
    assert_eq!(
        std::fs::read(locked.join("target.md")).unwrap(),
        b"behind a locked folder"
    );
}

/// WB-24 (#1480): in a test build the Recycle Bin is reached only on the
/// scratch volume itself. An item goes to its own volume's `$RECYCLE.BIN`, so
/// a probe folder on the system drive would be the Recycle Bin of whoever
/// runs the suite, and a path on any other volume would be that volume's.
#[cfg(windows)]
#[test]
fn the_windows_guard_refuses_the_system_drive_and_any_other_volume() {
    let system = scratch_volume_refusal(r"C:\", r"C:\", r"C:\")
        .expect("a scratch volume that is the system drive is refused");
    assert!(system.contains("system drive"), "{system}");
    let elsewhere = scratch_volume_refusal(r"D:\", r"T:\", r"C:\")
        .expect("a path on another volume is refused");
    assert!(
        elsewhere.contains(r"D:\") && elsewhere.contains(r"T:\"),
        "the refusal names both volumes: {elsewhere}"
    );
    assert_eq!(scratch_volume_refusal(r"\\?\T:\", r"T:\", r"C:\"), None);
    assert_eq!(scratch_volume_refusal(r"t:", r"\\?\T:\", r"c:\"), None);

    // This machine's own volumes: a probe folder on the system drive.
    let windows = PathBuf::from(std::env::var_os("SystemRoot").expect("SystemRoot"));
    let refused = windows_scratch_refusal(&windows, &windows)
        .expect("a probe folder on the system drive is refused");
    assert!(refused.contains("system drive"), "{refused}");
}

/// The scratch volume the gated Windows facts run on (`SLATE_TRASH_PROBE_VOLUME`),
/// checked against the guard's own rule first so a misconfigured run stops
/// before it trashes anything.
#[cfg(windows)]
fn probe_volume() -> PathBuf {
    let volume = std::fs::canonicalize(
        std::env::var_os("SLATE_TRASH_PROBE_VOLUME")
            .expect("set SLATE_TRASH_PROBE_VOLUME to a scratch volume's root, such as T:\\"),
    )
    .unwrap();
    if let Some(why) = windows_scratch_refusal(&volume, &volume) {
        panic!("{} is not a scratch volume: {why}", volume.display());
    }
    volume
}

/// An entry's identity (volume serial and 128-bit file id), never following
/// a link or a junction at the end.
#[cfg(windows)]
fn entry_identity(path: &Path) -> String {
    use std::os::windows::fs::OpenOptionsExt as _;
    use windows_sys::Win32::Storage::FileSystem::{
        FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OPEN_REPARSE_POINT, FILE_READ_ATTRIBUTES,
        FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE,
    };
    let file = std::fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(path)
        .unwrap_or_else(|error| panic!("open {path:?} as itself: {error}"));
    crate::vault::windows_entry::identity(&file).unwrap()
}

/// What the walk sees at `path` itself: a folder, a junction, a symbolic link…
#[cfg(windows)]
fn entry_kind(path: &Path) -> crate::vault::windows_entry::Found {
    use std::os::windows::fs::OpenOptionsExt as _;
    use windows_sys::Win32::Storage::FileSystem::{
        FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OPEN_REPARSE_POINT, FILE_READ_ATTRIBUTES,
        FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE,
    };
    let file = std::fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(path)
        .unwrap_or_else(|error| panic!("open {path:?} as itself: {error}"));
    crate::vault::windows_entry::found(&file).unwrap()
}

/// One item in a volume's Recycle Bin: the original path its `$I<id>` record
/// names and the `$R<id>` entry that holds it.
#[cfg(windows)]
#[derive(Debug)]
struct Recycled {
    original: String,
    entry: PathBuf,
}

/// Every item in `volume`'s `$RECYCLE.BIN`, for every user's folder in it.
/// A version-2 `$I` record is the version (8 bytes), the size (8), the
/// deletion time (8), the path's length in UTF-16 units with its terminator
/// (4), then the path; a version-1 record holds the path at offset 24 in 260
/// units.
#[cfg(windows)]
fn recycled_entries(volume: &Path) -> Vec<Recycled> {
    let mut found = Vec::new();
    let Ok(users) = std::fs::read_dir(volume.join("$RECYCLE.BIN")) else {
        return found;
    };
    for user in users {
        let user = user.unwrap().path();
        let Ok(entries) = std::fs::read_dir(&user) else {
            continue;
        };
        for record in entries {
            let record = record.unwrap();
            let name = record.file_name().to_string_lossy().into_owned();
            let Some(id) = name.strip_prefix("$I") else {
                continue;
            };
            let bytes = std::fs::read(record.path()).unwrap();
            let units = |from: usize, count: usize| -> String {
                let wide: Vec<u16> = bytes[from..(from + count * 2).min(bytes.len())]
                    .as_chunks::<2>()
                    .0
                    .iter()
                    .map(|pair| u16::from_le_bytes(*pair))
                    .take_while(|unit| *unit != 0)
                    .collect();
                String::from_utf16_lossy(&wide)
            };
            let version = u64::from_le_bytes(bytes[0..8].try_into().unwrap());
            let original = if version >= 2 {
                let count = u32::from_le_bytes(bytes[24..28].try_into().unwrap()) as usize;
                units(28, count)
            } else {
                units(24, 260)
            };
            found.push(Recycled {
                original,
                entry: user.join(format!("$R{id}")),
            });
        }
    }
    found
}

/// `path` as the Recycle Bin records an original location: no verbatim
/// prefix, compared without regard to case.
#[cfg(windows)]
fn as_recorded(path: &Path) -> String {
    let text = path.to_string_lossy();
    text.strip_prefix(r"\\?\").unwrap_or(&text).to_lowercase()
}

/// The Recycle Bin item whose record names `original`, waiting up to ten
/// seconds for it.
#[cfg(windows)]
fn await_recycled(volume: &Path, original: &Path) -> Option<Recycled> {
    let wanted = as_recorded(original);
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(10);
    loop {
        let found = recycled_entries(volume)
            .into_iter()
            .find(|item| item.original.to_lowercase() == wanted);
        if found.is_some() || std::time::Instant::now() >= deadline {
            return found;
        }
        std::thread::sleep(std::time::Duration::from_millis(200));
    }
}

/// #1480 on the real Windows back end (trash-rs, `IFileOperation::DeleteItem`
/// with `FOF_ALLOWUNDO`), on a scratch volume only: never the Recycle Bin of
/// whoever runs it. CI's `rust-real-trash` job makes and attaches one:
///
/// ```text
/// diskpart: create vdisk file=<vhd> maximum=2048 type=expandable; attach vdisk;
///           create partition primary; format fs=ntfs quick; assign letter=T
/// $env:SLATE_TRASH_PROBE_VOLUME = 'T:\'
/// cargo test -p slate-core --lib real_trash_on_windows -- --ignored --test-threads=1
/// ```
///
/// A note, a folder and a link trashed through `delete` each land in the
/// volume's own `$RECYCLE.BIN` as the same object (`$R`), with a `$I` record
/// naming its original path; the link's target stays.
#[cfg(windows)]
#[test]
#[ignore = "needs a scratch volume in SLATE_TRASH_PROBE_VOLUME; never a real user's Recycle Bin"]
// The fact's output is its probe record (WB-24); #507's no-stdout rule is for slate-core itself.
#[allow(clippy::disallowed_macros)]
fn real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records() {
    let volume = probe_volume();
    let vault = volume.join(format!("vault-{}", std::process::id()));
    std::fs::create_dir_all(vault.join("sub/folder")).unwrap();
    std::fs::write(vault.join("sub/note.md"), b"note").unwrap();
    std::fs::write(vault.join("sub/folder/inner.md"), b"inner").unwrap();
    std::fs::write(vault.join("target.md"), b"target").unwrap();
    std::os::windows::fs::symlink_file(vault.join("target.md"), vault.join("sub/link.md")).unwrap();
    let items = ["sub/note.md", "sub/folder", "sub/link.md"];
    let identities: Vec<String> = items
        .iter()
        .map(|item| entry_identity(&vault.join(item)))
        .collect();
    let link_text = std::fs::read_link(vault.join("sub/link.md")).unwrap();

    let provider = real_system_trash_provider(vault.clone());
    for item in items {
        provider.delete(item).unwrap();
        assert!(
            vault.join(item).symlink_metadata().is_err(),
            "{item} left the vault"
        );
    }
    assert_eq!(std::fs::read(vault.join("target.md")).unwrap(), b"target");

    for (item, identity) in items.iter().zip(&identities) {
        let original = vault.join(item.replace('/', "\\"));
        let recycled = await_recycled(&volume, &original).unwrap_or_else(|| {
            panic!(
                "no $I record names {original:?}: {:?}",
                recycled_entries(&volume)
            )
        });
        assert_eq!(
            &entry_identity(&recycled.entry),
            identity,
            "{item} is in the Recycle Bin as the same object"
        );
        println!(
            "WB-24 RECYCLED | {item} | $I {} | {}",
            recycled.original,
            recycled.entry.display()
        );
    }
    let landed = await_recycled(&volume, &vault.join(r"sub\link.md")).unwrap();
    assert_eq!(
        std::fs::read_link(&landed.entry).unwrap(),
        link_text,
        "the link is in the Recycle Bin as the link"
    );
    assert_eq!(
        entry_kind(&landed.entry),
        crate::vault::windows_entry::Found::SymbolicLink
    );
}

/// #1499's probe on the real Windows back end, on the scratch volume of
/// `real_trash_on_windows_lands_in_the_volumes_recycle_bin_with_its_records`
/// (run by the same filter). Each kind of link whose target cannot be
/// followed is handed to the Recycle Bin directly, past `delete`'s own rule
/// for dangling links, and the fact prints one row per kind: whether
/// `DeleteItem` took it, the `$I` record's original path, and whether the
/// `$R` entry is the same object, of the same kind, with the same text. It
/// fails only when a taken link landed as anything else, a refused link left
/// the vault, or a target changed. CI restores what it took afterwards and
/// prints what came back.
#[cfg(windows)]
#[test]
#[ignore = "needs a scratch volume in SLATE_TRASH_PROBE_VOLUME; never a real user's Recycle Bin"]
// The fact's output is its probe record (WB-24); #507's no-stdout rule is for slate-core itself.
#[allow(clippy::disallowed_macros)]
fn real_trash_on_windows_takes_each_dangling_link_kind_as_the_link() {
    let volume = probe_volume();
    let vault = volume.join(format!("links-{}", std::process::id()));
    let links = vault.join("links");
    std::fs::create_dir_all(&links).unwrap();
    std::fs::write(vault.join("end.md"), b"the file a path runs through").unwrap();
    std::fs::create_dir(vault.join("denied")).unwrap();
    std::fs::write(vault.join(r"denied\denied.md"), b"denied to everyone").unwrap();

    // #1499's kinds.
    std::os::windows::fs::symlink_file(vault.join("gone.md"), links.join("missing.md")).unwrap();
    std::os::windows::fs::symlink_dir(vault.join("gone-folder"), links.join("missing-folder"))
        .unwrap();
    std::os::windows::fs::symlink_file(vault.join(r"end.md\x"), links.join("through-a-file.md"))
        .unwrap();
    std::fs::create_dir(vault.join("doomed")).unwrap();
    std::fs::create_dir(links.join("junction")).unwrap();
    crate::vault::test_support::junction(&links.join("junction"), &vault.join("doomed"));
    std::fs::remove_dir(vault.join("doomed")).unwrap();
    std::os::windows::fs::symlink_file(
        vault.join(r"denied\denied.md"),
        links.join("unreachable.md"),
    )
    .unwrap();
    std::os::windows::fs::symlink_file("cycle-b.md", links.join("cycle-a.md")).unwrap();
    std::os::windows::fs::symlink_file("cycle-a.md", links.join("cycle-b.md")).unwrap();
    let denied = Denied::new(&vault.join(r"denied\denied.md"));

    let kinds = [
        ("a file link to a missing file", "missing.md"),
        ("a folder link to a missing folder", "missing-folder"),
        (
            "a link whose target runs through a file",
            "through-a-file.md",
        ),
        ("a junction to a missing folder", "junction"),
        ("a link to a file no one may open", "unreachable.md"),
        ("a link cycle", "cycle-a.md"),
    ];
    let provider = real_system_trash_provider(vault.clone());
    println!(
        "WB-24 PROBE | kind | following it fails with | DeleteItem | left the vault | $I original | same object | same kind | same text"
    );
    let mut wrong = Vec::new();
    for (kind, name) in kinds {
        let link = links.join(name);
        let follow =
            std::fs::metadata(&link).map_or_else(|error| format!("{error}"), |_| "nothing".into());
        let identity = entry_identity(&link);
        let found = entry_kind(&link);
        let text = std::fs::read_link(&link).unwrap();
        let taken = provider.system_trash().trash(&link);
        let left = link.symlink_metadata().is_err();
        let recycled = if left {
            await_recycled(&volume, &link)
        } else {
            None
        };
        let (same_object, same_kind, same_text) =
            recycled.as_ref().map_or((false, false, false), |item| {
                (
                    entry_identity(&item.entry) == identity,
                    entry_kind(&item.entry) == found,
                    std::fs::read_link(&item.entry).is_ok_and(|landed| landed == text),
                )
            });
        println!(
            "WB-24 PROBE | {kind} | {follow} | {} | {left} | {} | {same_object} | {same_kind} | {same_text}",
            taken
                .as_ref()
                .map_or_else(|error| format!("refused: {error}"), |()| "taken".into()),
            recycled
                .as_ref()
                .map_or("none", |item| item.original.as_str()),
        );
        match (&taken, left) {
            (Ok(()), true) if same_object && same_kind && same_text => {}
            (Err(_), false) => {}
            _ => wrong.push(kind),
        }
    }
    drop(denied);
    assert!(
        !vault.join("gone.md").exists(),
        "a missing target stays missing"
    );
    assert!(
        !vault.join("gone-folder").exists(),
        "a missing folder stays missing"
    );
    assert!(
        !vault.join("doomed").exists(),
        "a junction's missing folder stays missing"
    );
    assert_eq!(
        std::fs::read(vault.join("end.md")).unwrap(),
        b"the file a path runs through"
    );
    assert_eq!(
        std::fs::read(vault.join(r"denied\denied.md")).unwrap(),
        b"denied to everyone"
    );
    assert!(
        links
            .join("cycle-b.md")
            .symlink_metadata()
            .unwrap()
            .file_type()
            .is_symlink(),
        "the cycle's other link stays"
    );
    assert!(
        wrong.is_empty(),
        "taken but not in the Recycle Bin as the same link, or refused yet gone: {wrong:?}"
    );
}
