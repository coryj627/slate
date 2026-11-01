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

/// A full scan that saw every entry: a scan reports a listing it could not
/// make as an error sample, not a failure, so a fact that relies on the
/// index checks it (a root listing that failed once passed silently).
fn scanned(session: &VaultSession) {
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        report.complete && report.error_count == 0,
        "the scan saw everything: {report:?}"
    );
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

    // What the hold itself guarantees, apart from the open cache inside:
    // nobody else can open the root or `.slate` for deletion (a rename
    // needs that access), because neither handle shares it.
    for held in [vault.clone(), vault.join(".slate")] {
        use std::os::windows::fs::OpenOptionsExt as _;
        let error = std::fs::OpenOptions::new()
            .access_mode(0x0001_0000) // DELETE
            .share_mode(7)
            .custom_flags(0x0200_0000) // FILE_FLAG_BACKUP_SEMANTICS
            .open(&held)
            .expect_err("held without sharing deletion");
        assert_eq!(
            error.raw_os_error(),
            Some(32),
            "{}: {error:?}",
            held.display()
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
    scanned(&session);
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
    scanned(&a);
    scanned(&b);
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
    scanned(&session);
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

/// Every entry below `root`, by its root-relative path: a file with its
/// bytes, a folder (its path ending in `/`) with none, so a folder made in
/// the decoy (a staging `.slate\tmp`) changes the tree too.
fn tree(root: &Path) -> std::collections::BTreeMap<String, Vec<u8>> {
    let mut files = std::collections::BTreeMap::new();
    let mut folders = vec![root.to_path_buf()];
    while let Some(folder) = folders.pop() {
        for entry in std::fs::read_dir(&folder).unwrap() {
            let entry = entry.unwrap();
            let path = entry.path();
            if entry.file_type().unwrap().is_dir() {
                let relative = path
                    .strip_prefix(root)
                    .unwrap()
                    .to_string_lossy()
                    .into_owned();
                files.insert(format!("{relative}/"), Vec::new());
                folders.push(path);
            } else {
                let relative = path
                    .strip_prefix(root)
                    .unwrap()
                    .to_string_lossy()
                    .into_owned();
                files.insert(relative, std::fs::read(&path).unwrap());
            }
        }
    }
    files
}

/// Design item 5's interior-resolution family, the Windows stand-in for
/// every Unix live rename: the vault is opened through a junction
/// `outer\vault` → `outer\A`, then, with the session live, the junction is
/// remade to point at a decoy `outer\B` with its own `.slate`, `slate.json`
/// and notes. Every interior writer and reader must still reach `A` — the
/// staged save and its publish, an exclusive create, a structural rename
/// (the structural lock), the op-log, the cache with its WAL and shared
/// memory, the provider's reads, `canonical_path` and `root_display_path()`
/// — and `B`'s tree stays byte-identical. A by-path implementation writes
/// into, or reads from, `B`.
#[test]
fn no_interior_writer_or_reader_follows_a_retargeted_spelling() {
    let tmp = tempfile::tempdir().unwrap();
    let outer = tmp.path().join("outer");
    let a = outer.join("A");
    let b = outer.join("B");
    std::fs::create_dir_all(&a).unwrap();
    std::fs::write(a.join("note.md"), "# A\n").unwrap();
    std::fs::create_dir_all(b.join(".slate")).unwrap();
    std::fs::write(b.join("note.md"), "# B, the decoy\n").unwrap();
    std::fs::write(
        b.join("slate.json"),
        r#"{ "templates": { "directory": "T" } }"#,
    )
    .unwrap();
    std::fs::write(b.join(".slate").join("prefs.json"), "{}").unwrap();
    let spelling = outer.join("vault");
    junction(&spelling, &a);

    let session = session_at(&spelling).unwrap();
    scanned(&session);
    let decoy = tree(&b);

    std::fs::remove_dir(&spelling).unwrap();
    junction(&spelling, &b);
    assert_eq!(
        std::fs::read_to_string(spelling.join("note.md")).unwrap(),
        "# B, the decoy\n",
        "premise: the spelling now names the decoy"
    );

    let read = session.read_note_parts("note.md").unwrap();
    assert_eq!(read.body.trim(), "# A", "the provider reads A");
    session
        .save_text("note.md", "# A, saved\n", Some(read.content_hash.as_str()))
        .unwrap();
    session.create_exclusive("new.md", "# New\n").unwrap();
    session.rename_file("new.md", "moved.md").unwrap();
    assert_eq!(session.read_text("note.md").unwrap(), "# A, saved\n");
    assert_eq!(
        session.canonical_path("moved.md").unwrap().as_deref(),
        Some("moved.md"),
        "canonical_path resolves beneath A"
    );
    let shown = session.root_display_path().unwrap();
    assert!(
        shown.ends_with(Path::new("outer").join("A")),
        "the display path names the held folder: {}",
        shown.display()
    );

    assert_eq!(
        std::fs::read_to_string(a.join("note.md")).unwrap(),
        "# A, saved\n"
    );
    assert!(a.join("moved.md").is_file() && !a.join("new.md").exists());
    for leaf in [
        "cache.sqlite",
        "cache.sqlite-wal",
        "cache.sqlite-shm",
        "structural.lock",
    ] {
        assert!(a.join(".slate").join(leaf).is_file(), "A holds {leaf}");
    }
    drop(session);
    assert_eq!(tree(&b), decoy, "the decoy is byte-identical");
}

/// The open's pathname re-verification reads the held root (C3): a spelling
/// retargeted at another folder after the interior opened, and left so at
/// the end of the open, refuses it, as on Unix.
#[test]
fn a_spelling_retargeted_during_the_open_refuses_it() {
    let tmp = tempfile::tempdir().unwrap();
    let a = tmp.path().join("A");
    let b = tmp.path().join("B");
    std::fs::create_dir_all(&a).unwrap();
    std::fs::create_dir_all(&b).unwrap();
    let spelling = tmp.path().join("vault");
    junction(&spelling, &a);
    let (seam_spelling, seam_b) = (spelling.clone(), b.clone());
    let _seam = install_open_swap_seam(move |point| {
        if point == OpenSwapPoint::InteriorOpened {
            std::fs::remove_dir(&seam_spelling).unwrap();
            junction(&seam_spelling, &seam_b);
        }
    });
    let error = refused_open(&spelling, "a retargeted spelling refuses the open");
    let VaultError::InvalidPath { reason, .. } = &error else {
        panic!("the re-verification's refusal, not {error:?}");
    };
    assert_eq!(reason, "vault root changed while the session was opening");
}

/// A read that must stay in the vault (templates, `read_in_vault_with_cap`)
/// follows a link only to an entry beneath the held root: a templates folder
/// that is a junction to a folder outside the vault is refused.
#[test]
fn an_in_vault_read_through_a_link_that_leaves_the_vault_is_refused() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("vault");
    let outside = tmp.path().join("outside");
    std::fs::create_dir_all(vault.join("inside")).unwrap();
    std::fs::create_dir_all(&outside).unwrap();
    std::fs::write(outside.join("secret.md"), "outside").unwrap();
    std::fs::write(vault.join("inside").join("ok.md"), "inside").unwrap();
    junction(&vault.join("Templates"), &outside);
    junction(&vault.join("Linked"), &vault.join("inside"));
    let session = session_at(&vault).unwrap();

    let error = session
        .provider
        .read_in_vault_with_cap("Templates/secret.md", 1024)
        .unwrap_err();
    assert!(matches!(error, VaultError::InvalidPath { .. }), "{error:?}");
    assert_eq!(
        session
            .provider
            .read_in_vault_with_cap("Linked/ok.md", 1024)
            .unwrap(),
        b"inside",
        "a link that stays in the vault is followed"
    );
}

/// The identity is `FileIdInfo` itself: the 64-bit volume serial and the
/// 128-bit file id, its low half first (the host's `UInt128(high, low)`),
/// compared with an independent read, not with core's own.
#[test]
fn the_identity_is_the_volume_serial_and_the_whole_128_bit_file_id() {
    use std::os::windows::fs::OpenOptionsExt as _;
    use std::os::windows::io::AsRawHandle as _;
    use windows_sys::Win32::Storage::FileSystem::{
        FILE_ID_INFO, FileIdInfo, GetFileInformationByHandleEx,
    };
    let tmp = tempfile::tempdir().unwrap();
    let session = session_at(tmp.path()).unwrap();
    let identity = session.root_identity().unwrap();

    let dir = std::fs::OpenOptions::new()
        .read(true)
        .custom_flags(0x0200_0000) // FILE_FLAG_BACKUP_SEMANTICS
        .open(tmp.path())
        .unwrap();
    // SAFETY: zeroed storage of the type the call fills.
    let mut info: FILE_ID_INFO = unsafe { std::mem::zeroed() };
    // SAFETY: a live handle and a buffer of exactly FILE_ID_INFO's size.
    let ok = unsafe {
        GetFileInformationByHandleEx(
            dir.as_raw_handle(),
            FileIdInfo,
            (&mut info as *mut FILE_ID_INFO).cast(),
            std::mem::size_of::<FILE_ID_INFO>() as u32,
        )
    };
    assert_ne!(ok, 0, "NTFS answers FileIdInfo");
    let id = u128::from_le_bytes(info.FileId.Identifier);
    assert_eq!(identity.volume, info.VolumeSerialNumber);
    assert_eq!(identity.id_high, (id >> 64) as u64);
    assert_eq!(identity.id_low, id as u64);
    assert!(identity.persistent);
}

/// Design item 4: the structural-lock registry is keyed by the held
/// `.slate`'s identity, so two spellings of one vault (a junction and the
/// folder itself) share one slot, and two vaults do not.
#[test]
fn two_spellings_of_one_vault_share_one_structural_lock_slot() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("vault");
    let other = tmp.path().join("other");
    std::fs::create_dir_all(&vault).unwrap();
    std::fs::create_dir_all(&other).unwrap();
    let link = tmp.path().join("link");
    junction(&link, &vault);
    let key = |session: &VaultSession| {
        StructuralLockKey::of(session.held_root.as_ref().unwrap().cache_dir()).unwrap()
    };
    let direct = session_at(&vault).unwrap();
    let through_link = session_at(&link).unwrap();
    let elsewhere = session_at(&other).unwrap();
    assert!(matches!(key(&direct), StructuralLockKey::Identity(_)));
    assert_eq!(key(&direct), key(&through_link), "one vault, one slot");
    assert_ne!(key(&direct), key(&elsewhere), "two vaults, two slots");
}

/// An interior folder that is a junction is refused, never written through:
/// a save stages beneath the held `.slate`, and `.slate\tmp` planted as a
/// junction to a folder outside the vault fails the save.
#[test]
fn a_junctioned_interior_folder_is_refused() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("vault");
    let outside = tmp.path().join("outside");
    std::fs::create_dir_all(&vault).unwrap();
    std::fs::create_dir_all(&outside).unwrap();
    std::fs::write(vault.join("note.md"), "# Note\n").unwrap();
    let session = session_at(&vault).unwrap();
    scanned(&session);
    let _ = std::fs::remove_dir(vault.join(".slate").join("tmp"));
    junction(&vault.join(".slate").join("tmp"), &outside);

    let hash = session.read_note_parts("note.md").unwrap().content_hash;
    session
        .save_text("note.md", "# Saved\n", Some(hash.as_str()))
        .expect_err("a junctioned staging folder refuses the save");
    assert_eq!(
        std::fs::read_dir(&outside).unwrap().count(),
        0,
        "nothing staged outside"
    );
    assert_eq!(
        std::fs::read_to_string(vault.join("note.md")).unwrap(),
        "# Note\n"
    );
}

/// Design item 5's seam: a `.slate` planted as a junction after the root is
/// held and before `.slate` is, is refused, and nothing reaches its target.
#[test]
fn a_slate_junction_planted_before_the_hold_never_receives_the_interior() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("vault");
    let outside = tmp.path().join("outside");
    std::fs::create_dir_all(&vault).unwrap();
    std::fs::create_dir_all(&outside).unwrap();
    let (seam_slate, seam_outside) = (vault.join(".slate"), outside.clone());
    let _seam = install_open_swap_seam(move |point| {
        if point == OpenSwapPoint::BeforeSlateHeld {
            junction(&seam_slate, &seam_outside);
        }
    });
    let error = refused_open(&vault, "a planted .slate junction refuses the open");
    assert!(matches!(error, VaultError::InvalidPath { .. }), "{error:?}");
    assert_eq!(
        std::fs::read_dir(&outside).unwrap().count(),
        0,
        "the target got nothing"
    );
}

/// The duplicate the host's stores will use names the held folder after the
/// spelling is retargeted, and is a file object of its own (it lists while
/// the session's root is untouched).
#[test]
fn the_duplicated_root_handle_names_the_held_root_after_its_spelling_is_retargeted() {
    let tmp = tempfile::tempdir().unwrap();
    let a = tmp.path().join("A");
    let b = tmp.path().join("B");
    std::fs::create_dir_all(&a).unwrap();
    std::fs::create_dir_all(&b).unwrap();
    std::fs::write(a.join("only-in-a.md"), "a").unwrap();
    let spelling = tmp.path().join("vault");
    junction(&spelling, &a);
    let session = session_at(&spelling).unwrap();
    std::fs::remove_dir(&spelling).unwrap();
    junction(&spelling, &b);

    let duplicate = std::fs::File::from(session.duplicate_root_handle().unwrap());
    let named = crate::vault::trash_handoff_final_path_for_tests(&duplicate).unwrap();
    assert!(named.ends_with("A"), "{}", named.display());
    // A file object of its own: list the session's held handle to its end,
    // then a CONTINUING listing on the duplicate still starts at the
    // beginning (a duplicated handle would share the position and be done).
    use std::os::windows::io::AsRawHandle as _;
    use windows_sys::Win32::Storage::FileSystem::{
        FileFullDirectoryInfo, FileFullDirectoryRestartInfo, GetFileInformationByHandleEx,
    };
    let held = session.held_root.as_ref().unwrap().descriptor();
    let mut buffer = vec![0u64; 1024];
    let list = |file: &std::fs::File, class, buffer: &mut Vec<u64>| {
        // SAFETY: a live handle and a writable, aligned buffer of its size.
        unsafe {
            GetFileInformationByHandleEx(
                file.as_raw_handle(),
                class,
                buffer.as_mut_ptr().cast(),
                (buffer.len() * 8) as u32,
            )
        }
    };
    let mut class = FileFullDirectoryRestartInfo;
    while list(held, class, &mut buffer) != 0 {
        class = FileFullDirectoryInfo;
    }
    assert_ne!(
        list(&duplicate, FileFullDirectoryInfo, &mut buffer),
        0,
        "the duplicate keeps its own listing position"
    );
    let names = crate::vault::fs_root::sys::read_dir_names(&duplicate).unwrap();
    assert!(names.iter().any(|name| name == "only-in-a.md"), "{names:?}");
}

/// A move onto a hard link of the source is not a case-only rename: the
/// two names are two entries, so the provider refuses the destination and
/// both stay (the file-id compare alone would have replaced `b.md`).
#[test]
fn a_move_onto_a_hard_link_of_the_source_is_refused() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("a.md"), "# A\n").unwrap();
    std::fs::hard_link(tmp.path().join("a.md"), tmp.path().join("b.md")).unwrap();
    let session = session_at(tmp.path()).unwrap();
    // The provider's own rename, where the case-only rule lives (the
    // session refuses an indexed destination before it gets there).
    let error = session
        .provider
        .rename("a.md", "b.md")
        .expect_err("b.md is occupied");
    assert!(
        matches!(error, VaultError::DestinationExists { .. }),
        "{error:?}"
    );
    assert!(tmp.path().join("a.md").is_file() && tmp.path().join("b.md").is_file());
}

/// The stored spelling through a junction ancestor that stays in the vault
/// is the target's (main's Windows rule, kept; recorded in the spec's PR 1
/// record): `Linked/x.md` → `inside/x.md`.
#[test]
fn canonical_path_through_an_in_vault_junction_ancestor_is_the_targets_spelling() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::create_dir_all(tmp.path().join("inside")).unwrap();
    std::fs::write(tmp.path().join("inside").join("x.md"), "x").unwrap();
    junction(&tmp.path().join("Linked"), &tmp.path().join("inside"));
    let session = session_at(tmp.path()).unwrap();
    assert_eq!(
        session.canonical_path("Linked/x.md").unwrap().as_deref(),
        Some("inside/x.md")
    );
}

/// The interior's replacing rename keeps `std::fs::rename`'s POSIX
/// semantics: a target another handle holds open, sharing deletion (as an
/// indexer or another reader does), is still replaced.
#[test]
fn an_interior_rename_replaces_a_target_another_handle_holds_open() {
    use crate::vault::fs_root::{FileOptions, InteriorDir as _};
    let tmp = tempfile::tempdir().unwrap();
    let session = session_at(tmp.path()).unwrap();
    let cache = session.held_root.as_ref().unwrap().cache_dir().clone();
    std::fs::write(tmp.path().join(".slate").join("prefs.json"), "old").unwrap();
    let held_open = std::fs::File::open(tmp.path().join(".slate").join("prefs.json")).unwrap();
    {
        use std::io::Write as _;
        let mut staged = cache
            .open_file(
                "prefs.json.tmp",
                FileOptions::new().write(true).create(true),
            )
            .unwrap();
        staged.write_all(b"new").unwrap();
    }
    cache.rename_file("prefs.json.tmp", "prefs.json").unwrap();
    drop(held_open);
    assert_eq!(
        std::fs::read_to_string(tmp.path().join(".slate").join("prefs.json")).unwrap(),
        "new"
    );
}
/// The Trash confirmation counts the tree the Trash handoff sends (red
/// team, WB-28): both walk from the held root. A vault opened through a
/// junction stages its Trash (main refused: "through a symbolic link"),
/// and after the spelling is retargeted at a decoy the stage still counts
/// the held vault's folder, not the decoy's.
#[test]
fn a_retargeted_spelling_stages_the_trash_of_the_held_vault() {
    use crate::{BatchTrashRequest, StructuralBatchItem};
    let tmp = tempfile::tempdir().unwrap();
    let a = tmp.path().join("A");
    let b = tmp.path().join("B");
    std::fs::create_dir_all(a.join("Folder")).unwrap();
    std::fs::create_dir_all(b.join("Folder")).unwrap();
    for name in ["one.md", "two.md"] {
        std::fs::write(a.join("Folder").join(name), "a").unwrap();
    }
    for name in ["1.md", "2.md", "3.md", "4.md", "5.md"] {
        std::fs::write(b.join("Folder").join(name), "b").unwrap();
    }
    let spelling = tmp.path().join("vault");
    junction(&spelling, &a);
    let session = session_at(&spelling).unwrap();
    scanned(&session);
    std::fs::remove_dir(&spelling).unwrap();
    junction(&spelling, &b);

    let staged = session
        .stage_trash(BatchTrashRequest {
            items: vec![StructuralBatchItem {
                path: "Folder".into(),
                is_directory: true,
            }],
        })
        .unwrap();
    assert_eq!(staged.items[0].item_count, 2, "the held vault's Folder");
}

/// The session's path-based readers (bibliography, CSL styles, sync
/// detection, the LiveSync config) read the held folder after the spelling
/// is retargeted (codex round 1): `fs_root()` is the held root's own final
/// path on Windows.
#[test]
fn the_session_path_readers_read_the_held_folder_after_a_retarget() {
    use crate::citations::bibliography::{BibFormat, BibliographySource};
    let tmp = tempfile::tempdir().unwrap();
    let a = tmp.path().join("A");
    let b = tmp.path().join("B");
    std::fs::create_dir_all(&a).unwrap();
    std::fs::create_dir_all(b.join(".obsidian/plugins/obsidian-livesync")).unwrap();
    std::fs::write(
        a.join("refs.bib"),
        "@book{inA, title = {In A}, year = {2001}}\n",
    )
    .unwrap();
    std::fs::write(
        b.join("refs.bib"),
        "@book{inB, title = {In B}, year = {2002}}\n",
    )
    .unwrap();
    std::fs::write(
        b.join(".obsidian/plugins/obsidian-livesync/data.json"),
        "{}",
    )
    .unwrap();
    let spelling = tmp.path().join("vault");
    junction(&spelling, &a);
    let session = session_at(&spelling).unwrap();
    std::fs::remove_dir(&spelling).unwrap();
    junction(&spelling, &b);

    session
        .set_bibliography_sources(vec![BibliographySource {
            path: "refs.bib".into(),
            format: BibFormat::BibTeX,
            watch: false,
        }])
        .unwrap();
    let keys: Vec<String> = session
        .get_bibliography_entries()
        .unwrap()
        .into_iter()
        .map(|entry| entry.key)
        .collect();
    assert_eq!(keys, ["inA"], "the held vault's refs.bib");
    let report = session.detect_sync().unwrap();
    assert!(
        report.providers.is_empty(),
        "B's LiveSync marker is not ours: {report:?}"
    );
    assert!(matches!(
        session.livesync_config().unwrap(),
        crate::sync_detect::LiveSyncConfigStatus::NotPresent
    ));
}
/// Removes the deny entries `deny` added, before the temp folder goes.
struct DenyGuard(Vec<std::path::PathBuf>);

impl Drop for DenyGuard {
    fn drop(&mut self) {
        for path in &self.0 {
            let _ = std::process::Command::new("icacls")
                .arg(path)
                .args(["/remove:d", "*S-1-1-0"])
                .output();
        }
    }
}

/// Deny Everyone `rights` (icacls abbreviations) on `path` itself, not
/// inherited.
fn deny(guard: &mut DenyGuard, path: &Path, rights: &str) {
    let out = std::process::Command::new("icacls")
        .arg(path)
        .arg("/deny")
        .arg(format!("*S-1-1-0:({rights})"))
        .output()
        .unwrap();
    assert!(out.status.success(), "icacls /deny: {out:?}");
    guard.0.push(path.to_path_buf());
}

/// A folder on the way that the reader may only traverse (no list, no
/// attribute rights) still lets a read and a stat beneath it through, as a
/// path open does (codex round 1): the walk asks no rights of the folders
/// it passes.
#[test]
fn a_traverse_only_folder_on_the_way_still_lets_reads_through() {
    // Two levels: Windows grants read-attributes on a folder to whoever may
    // list its parent, so only a folder below a denied one is truly
    // traverse-only.
    let tmp = tempfile::tempdir().unwrap();
    let inner = tmp.path().join("locked").join("inner");
    std::fs::create_dir_all(&inner).unwrap();
    std::fs::write(inner.join("note.md"), "# Note\n").unwrap();
    let session = session_at(tmp.path()).unwrap();
    let mut guard = DenyGuard(Vec::new());
    // SYNCHRONIZE too (codex round 2): a folder passed on the way is never
    // waited on, so its walk handle asks only to traverse it.
    deny(&mut guard, &tmp.path().join("locked"), "RD,RA,S");
    deny(&mut guard, &inner, "RD,RA,S");

    // The control: a pathname read passes these folders.
    assert_eq!(
        std::fs::read_to_string(inner.join("note.md")).unwrap(),
        "# Note\n"
    );
    assert_eq!(
        session.read_text("locked/inner/note.md").unwrap(),
        "# Note\n"
    );
    assert!(session.provider.stat("locked/inner/note.md").is_ok());
    drop(guard);
}

/// An entry the reader may rename but not read keeps its stored spelling
/// and can still be renamed (codex round 1): the spelling lookup asks only
/// for attribute access.
#[test]
fn an_unreadable_entry_keeps_its_spelling_and_can_be_renamed() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("Secret.md"), "x").unwrap();
    let session = session_at(tmp.path()).unwrap();
    let mut guard = DenyGuard(Vec::new());
    deny(&mut guard, &tmp.path().join("Secret.md"), "RD");

    assert_eq!(
        session.canonical_path("secret.md").unwrap().as_deref(),
        Some("Secret.md")
    );
    session.provider.rename("Secret.md", "Renamed.md").unwrap();
    drop(guard);
    assert!(tmp.path().join("Renamed.md").is_file());
}

/// A folder on the way that denies traverse itself still lets a read, a
/// stat and the templates detection through, as a path open does (codex
/// round 3): the whole relative name is one `NtCreateFile`, so the system's
/// traverse-check bypass applies as it does for a path.
#[test]
fn a_folder_that_denies_traverse_still_lets_reads_and_template_detection_through() {
    let tmp = tempfile::tempdir().unwrap();
    let locked = tmp.path().join("locked");
    std::fs::create_dir_all(locked.join("Templates")).unwrap();
    std::fs::write(locked.join("note.md"), "# Note\n").unwrap();
    std::fs::write(
        tmp.path().join("slate.json"),
        r#"{ "templates": { "directory": "locked/Templates" } }"#,
    )
    .unwrap();
    let mut guard = DenyGuard(Vec::new());
    deny(&mut guard, &locked, "X");
    // The control: a pathname read passes the folder.
    assert_eq!(
        std::fs::read_to_string(locked.join("note.md")).unwrap(),
        "# Note\n"
    );

    let session = session_at(tmp.path()).unwrap();
    assert_eq!(
        session.config.templates_dir.as_deref(),
        Some("locked/Templates"),
        "the templates folder is detected beneath the traverse-denied folder"
    );
    assert_eq!(session.read_text("locked/note.md").unwrap(), "# Note\n");
    assert!(session.provider.stat("locked/note.md").is_ok());
    drop(session);
    drop(guard);
}

/// A leaf that denies `SYNCHRONIZE` still answers the looks (codex round
/// 3): a stat and the mutation probes open it with attribute access alone,
/// no synchronous I/O.
#[test]
fn a_leaf_that_denies_synchronize_still_answers_stats_and_probes() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("note.md"), "# Note\n").unwrap();
    let session = session_at(tmp.path()).unwrap();
    let mut guard = DenyGuard(Vec::new());
    deny(&mut guard, &tmp.path().join("note.md"), "S");

    assert!(session.provider.stat("note.md").is_ok());
    assert_eq!(
        session.provider.mutation_path_kind("note.md").unwrap(),
        Some(crate::vault::EntryKind::File)
    );
    assert_eq!(
        session.canonical_path("NOTE.md").unwrap().as_deref(),
        Some("note.md")
    );
    drop(guard);
}

/// A listing asks the listed folder for list access alone (codex round 4):
/// a folder that denies traverse lists, and so does one that denies
/// attribute reads beneath a parent that denies listing; a pathname
/// listing is the control for each.
#[test]
fn listings_ask_only_for_list_access() {
    let tmp = tempfile::tempdir().unwrap();
    let no_traverse = tmp.path().join("no-traverse");
    let outer = tmp.path().join("outer");
    let inner = outer.join("inner");
    std::fs::create_dir_all(&no_traverse).unwrap();
    std::fs::create_dir_all(&inner).unwrap();
    std::fs::write(no_traverse.join("a.md"), "a").unwrap();
    std::fs::write(inner.join("b.md"), "b").unwrap();
    let session = session_at(tmp.path()).unwrap();
    let mut guard = DenyGuard(Vec::new());
    deny(&mut guard, &no_traverse, "X");
    deny(&mut guard, &outer, "RD");
    deny(&mut guard, &inner, "RA");

    for (relative, path, name) in [
        ("no-traverse", &no_traverse, "a.md"),
        ("outer/inner", &inner, "b.md"),
    ] {
        let control: Vec<String> = std::fs::read_dir(path)
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .collect();
        assert_eq!(control, [name], "the pathname listing works");
        let listed: Vec<String> = session
            .provider
            .list_dir(relative)
            .unwrap()
            .into_iter()
            .map(|entry| entry.name)
            .collect();
        assert_eq!(listed, [name], "{relative}");
    }
    drop(guard);
}

/// The held root asks what main's per-operation root open asked (codex
/// round 4): a vault whose root denies traverse still opens and reads.
#[test]
fn a_vault_root_that_denies_traverse_still_opens() {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path().join("vault");
    std::fs::create_dir_all(vault.join(".slate")).unwrap();
    std::fs::write(vault.join("note.md"), "# Note\n").unwrap();
    let mut guard = DenyGuard(Vec::new());
    deny(&mut guard, &vault, "X");
    deny(&mut guard, &vault.join(".slate"), "X");
    assert_eq!(
        std::fs::read_to_string(vault.join("note.md")).unwrap(),
        "# Note\n"
    );

    let session = session_at(&vault).unwrap();
    scanned(&session);
    assert_eq!(session.read_text("note.md").unwrap(), "# Note\n");
    drop(session);
    drop(guard);
}

/// A templates folder configured with Windows separators (`\`, a trailing
/// one) is detected, as the open's own path validation accepts it (codex
/// round 4).
#[test]
fn a_templates_folder_with_windows_separators_is_detected() {
    for configured in [
        r"Notes\\Templates",
        "Notes/Templates/",
        r"Notes\\\\Templates",
    ] {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::create_dir_all(tmp.path().join("Notes").join("Templates")).unwrap();
        std::fs::write(
            tmp.path().join("slate.json"),
            format!(r#"{{ "templates": {{ "directory": "{configured}" }} }}"#),
        )
        .unwrap();
        let session = session_at(tmp.path()).unwrap();
        assert!(
            session.config.templates_dir.is_some(),
            "{configured}: the templates folder is detected"
        );
    }
}

/// The held path falls back to the volume-GUID form, for a volume mounted
/// without a drive letter (codex round 4): that form names the held folder,
/// passes the identity check, and the file APIs read through it.
#[test]
fn the_held_paths_volume_guid_form_names_the_held_folder() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("note.md"), "# Note\n").unwrap();
    let session = session_at(tmp.path()).unwrap();
    let held = session.held_root.as_ref().unwrap();
    let guid = crate::vault::final_path_guid_for_tests(held.descriptor()).unwrap();
    assert!(
        guid.to_string_lossy().starts_with(r"\\?\Volume{"),
        "{}",
        guid.display()
    );
    assert!(held.names_held_for_tests(&guid));
    assert_eq!(
        std::fs::read_to_string(guid.join("note.md")).unwrap(),
        "# Note\n"
    );
}

/// A followed stat of a reparse point that is no link (an AF_UNIX socket)
/// reports the entry itself, as `std::fs::metadata` does
/// (rust-lang/rust#109106; codex round 4).
#[test]
fn a_followed_stat_of_an_opaque_reparse_point_reports_the_entry() {
    use std::os::windows::fs::OpenOptionsExt as _;
    use std::os::windows::io::AsRawHandle as _;
    use windows_sys::Win32::System::IO::DeviceIoControl;
    const FSCTL_SET_REPARSE_POINT: u32 = 0x0009_00A4;
    const IO_REPARSE_TAG_AF_UNIX: u32 = 0x8000_0023;
    let tmp = tempfile::tempdir().unwrap();
    let socket = tmp.path().join("socket");
    let file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .custom_flags(0x0020_0000) // FILE_FLAG_OPEN_REPARSE_POINT
        .open(&socket)
        .unwrap();
    // The reparse header with no data: tag, data length 0, reserved.
    let mut header = [0u8; 8];
    header[..4].copy_from_slice(&IO_REPARSE_TAG_AF_UNIX.to_le_bytes());
    let mut returned = 0u32;
    // SAFETY: a live handle and an input buffer of the size passed.
    let ok = unsafe {
        DeviceIoControl(
            file.as_raw_handle(),
            FSCTL_SET_REPARSE_POINT,
            header.as_ptr().cast(),
            header.len() as u32,
            std::ptr::null_mut(),
            0,
            &mut returned,
            std::ptr::null_mut(),
        )
    };
    assert_ne!(
        ok,
        0,
        "set the AF_UNIX tag: {:?}",
        std::io::Error::last_os_error()
    );
    drop(file);
    assert!(
        std::fs::metadata(&socket).is_ok(),
        "the control: std's followed stat"
    );

    let session = session_at(tmp.path()).unwrap();
    assert!(session.provider.stat("socket").is_ok());
}
