// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-28 (22W; spec
//! `docs/plans/44_windows_backlog/specs/wb28_descriptor_relative_session_on_windows.md`):
//! a Windows filesystem session holds its vault root and `.slate` for its
//! lifetime, reads their identity from the held handles, and opens its
//! SQLite cache through the held `.slate`.
//!
//! #940's A→B→A race cannot be driven here: with the root held without
//! sharing deletion, Windows refuses to rename it or any folder above it
//! while the session lives (design item 5). So these facts pin the hold
//! itself, the identity decision and its seams, and what the open refuses.

#![cfg(windows)]

use super::common::*;
use super::*;
use std::path::Path;

fn session_at(root: &Path) -> Result<VaultSession, VaultError> {
    VaultSession::from_filesystem(root.to_path_buf())
}

/// The error an open that must refuse gives.
fn refused_open(root: &Path, why: &str) -> VaultError {
    match session_at(root) {
        Ok(_) => panic!("the open succeeded: {why}"),
        Err(error) => error,
    }
}

/// A junction at `link` pointing at the folder `target` (no privilege
/// needed, unlike a symbolic link).
fn junction(link: &Path, target: &Path) {
    let status = std::process::Command::new("cmd")
        .args(["/C", "mklink", "/J"])
        .arg(link)
        .arg(target)
        .output()
        .unwrap();
    assert!(status.status.success(), "mklink /J: {status:?}");
}

/// Design item 5, the handle-pinning family: while the session lives the
/// held root, the folder above it and the held `.slate` cannot be renamed;
/// once it closes they can, and nothing is made again at the old path.
#[test]
fn the_held_root_its_ancestors_and_slate_stay_put_while_the_session_lives() {
    let tmp = tempfile::tempdir().unwrap();
    let outer = tmp.path().join("outer");
    let vault = outer.join("vault");
    std::fs::create_dir_all(&vault).unwrap();
    let session = session_at(&vault).unwrap();
    assert!(
        vault.join(".slate").is_dir(),
        "the open made .slate beneath the held root"
    );

    for (from, to) in [
        (vault.clone(), outer.join("renamed")),
        (outer.clone(), tmp.path().join("outer-renamed")),
        (vault.join(".slate"), vault.join(".slate-renamed")),
    ] {
        let error = std::fs::rename(&from, &to).expect_err("held: the rename is refused");
        assert!(
            matches!(error.raw_os_error(), Some(5) | Some(32)),
            "{} refused with a sharing or access error, not {error:?}",
            from.display()
        );
    }

    drop(session);
    std::fs::rename(&vault, outer.join("renamed")).unwrap();
    assert!(!vault.exists(), "nothing is made again at the old path");
}

/// The root's own reparse point is followed (a vault opened through a
/// junction keeps working, as `PinnedRoot::open` follows it): the session's
/// identity is the target folder's, persistent on NTFS.
#[test]
fn a_root_opened_through_a_junction_holds_the_target_folder() {
    let tmp = tempfile::tempdir().unwrap();
    let target = tmp.path().join("target");
    std::fs::create_dir(&target).unwrap();
    let link = tmp.path().join("link");
    junction(&link, &target);

    let through_link = session_at(&link).unwrap();
    let identity = through_link
        .root_identity()
        .expect("Windows reads the held root");
    assert!(identity.persistent, "NTFS answers FileIdInfo");
    assert!(
        target.join(".slate").is_dir(),
        ".slate is made in the target"
    );
    drop(through_link);

    let direct = session_at(&target).unwrap();
    assert_eq!(
        direct.root_identity(),
        Some(identity),
        "two spellings of one folder read one identity"
    );
}

/// Design item 6's decision, as a table: `FileIdInfo` when it answers; the
/// file index only after a `FileIdInfo` failure on a volume named `FAT`,
/// `FAT32` or `exFAT` (ASCII, any case); every other failure refuses with
/// the `FileIdInfo` error and never reads the index.
#[test]
fn the_identity_takes_the_file_index_only_on_a_fat_volume() {
    use crate::vault::fs_root::sys::identity_from_reads;
    use std::cell::Cell;
    let persistent = VaultRootIdentity {
        volume: 1,
        id_high: 2,
        id_low: 3,
        persistent: true,
    };
    let weak = VaultRootIdentity {
        volume: 4,
        id_high: 0,
        id_low: 5,
        persistent: false,
    };
    let unsupported = || std::io::Error::from_raw_os_error(87);

    let index_read = Cell::new(false);
    let answer = identity_from_reads(
        Ok(persistent),
        || panic!("the name is never read when FileIdInfo answers"),
        || panic!("the index is never read when FileIdInfo answers"),
    );
    assert_eq!(answer.unwrap(), persistent);

    for name in ["FAT", "FAT32", "exFAT", "fat32", "EXFAT"] {
        let answer = identity_from_reads(Err(unsupported()), || Ok(name.to_string()), || Ok(weak));
        assert_eq!(answer.unwrap(), weak, "{name}: the fallback answers");
    }

    let answer = identity_from_reads(
        Err(unsupported()),
        || Ok("FAT32".to_string()),
        || Err(std::io::Error::from_raw_os_error(5)),
    );
    assert_eq!(
        answer.unwrap_err().raw_os_error(),
        Some(5),
        "a FAT volume whose index fails too refuses with that error"
    );

    let name_unreadable: fn() -> std::io::Result<String> =
        || Err(std::io::Error::from_raw_os_error(1));
    for name in [
        Box::new(|| Ok("NTFS".to_string())) as Box<dyn FnOnce() -> std::io::Result<String>>,
        Box::new(|| Ok("ReFS".to_string())),
        Box::new(|| Ok("FAT32 ".to_string())),
        Box::new(|| Ok(String::new())),
        Box::new(name_unreadable),
    ] {
        let answer = identity_from_reads(Err(unsupported()), name, || {
            index_read.set(true);
            Ok(weak)
        });
        assert_eq!(
            answer.unwrap_err().raw_os_error(),
            Some(87),
            "the FileIdInfo error stands"
        );
        assert!(
            !index_read.get(),
            "the index is never read off a FAT volume"
        );
    }
}

/// `SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED` presents a FAT32 volume: the
/// fallback answers and the session opens on a weak identity, its `.slate`
/// held and its cache working.
#[test]
fn a_root_whose_file_id_info_is_unsupported_opens_with_a_weak_file_index_identity() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("wb28-weak-open");
    std::fs::create_dir(&vault).unwrap();
    std::fs::write(vault.join("note.md"), "# Note\n").unwrap();
    let mut env = fault_guard();
    env.set("SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED", "wb28-weak-open");

    let session = session_at(&vault).unwrap();
    let identity = session.root_identity().expect("the fallback answers");
    assert!(!identity.persistent, "a FAT32 profile's identity is weak");
    assert_eq!(identity.id_high, 0);
    session.scan_initial(&CancelToken::new()).unwrap();
    session.rename_file("note.md", "renamed.md").unwrap();
    assert!(vault.join("renamed.md").is_file(), "the session works");
}

/// `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE`: neither read answers, so the open
/// refuses with core's sentence, and no `.slate` is made.
#[test]
fn a_root_whose_volume_gives_no_identity_refuses_the_open_and_makes_nothing() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("wb28-no-identity");
    std::fs::create_dir(&vault).unwrap();
    let mut env = fault_guard();
    env.set("SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE", "wb28-no-identity");

    let error = refused_open(&vault, "no identity, no session");
    assert_eq!(
        crate::a11y::vault_error_detail(&error),
        crate::a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE
    );
    assert!(!vault.join(".slate").exists(), "no .slate is made");
}

/// `SLATE_TEST_ROOT_FILE_ID_INFO_FAILS`: `FileIdInfo` fails under the
/// volume's real name (NTFS here), so the open refuses rather than falling
/// back (the table above proves the index is never read).
#[test]
fn a_file_id_info_failure_off_a_fat_volume_refuses_the_open() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("wb28-info-fails");
    std::fs::create_dir(&vault).unwrap();
    let mut env = fault_guard();
    env.set("SLATE_TEST_ROOT_FILE_ID_INFO_FAILS", "wb28-info-fails");

    let error = refused_open(&vault, "a transient failure refuses");
    assert_eq!(
        crate::a11y::vault_error_detail(&error),
        crate::a11y::VAULT_ROOT_IDENTITY_UNAVAILABLE
    );
    assert!(!vault.join(".slate").exists());
}

/// Design item 4: weak identities can collide (two cloned FAT volumes), and
/// a collision only makes two sessions take turns at the in-process gate:
/// each still locks `structural.lock` beneath its own held `.slate`, so a
/// structural change in each lands in its own vault.
#[test]
fn colliding_weak_identities_take_turns_and_reach_only_their_own_vault() {
    let tmp = tempfile::tempdir().unwrap();
    let first = tmp.path().join("wb28-collide-a");
    let second = tmp.path().join("wb28-collide-b");
    for vault in [&first, &second] {
        std::fs::create_dir(vault).unwrap();
        std::fs::write(vault.join("note.md"), "# Note\n").unwrap();
    }
    let mut env = fault_guard();
    env.set("SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED", "wb28-collide-");
    env.set("SLATE_TEST_ROOT_FILE_INDEX_COLLIDES", "wb28-collide-");

    let a = session_at(&first).unwrap();
    let b = session_at(&second).unwrap();
    assert_eq!(
        a.root_identity(),
        b.root_identity(),
        "premise: the two weak identities collide"
    );
    a.scan_initial(&CancelToken::new()).unwrap();
    b.scan_initial(&CancelToken::new()).unwrap();
    a.rename_file("note.md", "from-a.md").unwrap();
    b.rename_file("note.md", "from-b.md").unwrap();
    assert!(first.join("from-a.md").is_file() && !first.join("from-b.md").exists());
    assert!(second.join("from-b.md").is_file() && !second.join("from-a.md").exists());
}

/// A `.slate` that is a junction is refused with core's typed sentence, in
/// the vault walk's words; nothing is followed.
#[test]
fn a_slate_that_is_a_junction_refuses_the_open() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("vault");
    let elsewhere = tmp.path().join("elsewhere");
    std::fs::create_dir(&vault).unwrap();
    std::fs::create_dir(&elsewhere).unwrap();
    junction(&vault.join(".slate"), &elsewhere);

    let error = refused_open(&vault, "a linked .slate is refused");
    let VaultError::InvalidPath { reason, .. } = &error else {
        panic!("the typed refusal, not {error:?}");
    };
    assert_eq!(
        reason,
        "\".slate\" is a junction, not a folder in this vault, so the vault was not opened"
    );
    assert_eq!(
        std::fs::read_dir(&elsewhere).unwrap().count(),
        0,
        "nothing is written through the junction"
    );
}

/// A `.slate` that is a file is refused with core's typed sentence.
#[test]
fn a_slate_that_is_a_file_refuses_the_open() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join(".slate"), b"not a folder").unwrap();
    let error = refused_open(tmp.path(), "a .slate file is refused");
    let VaultError::InvalidPath { reason, .. } = &error else {
        panic!("the typed refusal, not {error:?}");
    };
    assert_eq!(
        reason,
        "\".slate\" is not a folder, so the vault was not opened"
    );
}

/// The hook's own evidence (design item 5): the session's connection opened
/// the synthetic path, which the hook resolves through the held `.slate`;
/// the cache, its WAL and its shared memory are in the vault's `.slate`.
#[test]
fn the_session_cache_opens_through_the_held_slate() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("note.md"), "# Note\n").unwrap();
    let session = session_at(tmp.path()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let opened = session
        .conn
        .lock()
        .unwrap()
        .path()
        .map(str::to_string)
        .unwrap();
    assert!(
        opened.starts_with(r"C:\|slate-fd\") && opened.ends_with(r"\cache.sqlite"),
        "{opened}"
    );
    for leaf in ["cache.sqlite", "cache.sqlite-wal", "cache.sqlite-shm"] {
        assert!(tmp.path().join(".slate").join(leaf).is_file(), "{leaf}");
    }
}

/// The open's pathname re-verification reads the held root on Windows too
/// (C3): a plain open succeeds. The watch's arm check compares the watch
/// with the held root in the watch's own terms (C9), so it has an anchor.
#[test]
fn a_plain_open_passes_its_reverification_and_has_a_watch_anchor() {
    let tmp = tempfile::tempdir().unwrap();
    let session = session_at(tmp.path()).unwrap();
    assert!(session.root_identity().is_some());
    assert!(
        session.watch_anchor.is_some(),
        "the watch anchor is the held root's"
    );
}
