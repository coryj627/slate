// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! WB-27 (plan 44): the Windows no-replace create and rename, run on the weak
//! volumes the capability matrix (plan 24 §3.3, §5, §6) lists from source:
//! FAT32, exFAT and an SMB share.
//!
//! The gated facts run only on the volumes named by `SLATE_WEAK_VOLUME_FAT32`,
//! `SLATE_WEAK_VOLUME_EXFAT` and `SLATE_WEAK_VOLUME_SMB`, each an existing
//! scratch directory on that volume, and only after [`check_weak_volume`]
//! confirms the volume is the kind its variable names, so an NTFS folder
//! exported under a FAT name never records NTFS's answers as FAT's. With none
//! set they return at once, so an `--ignored` run on a bare runner stays
//! green. Each fact prints what it observed (`OBSERVED <kind> …`); run them
//! with `--nocapture` to record the matrix rows.
//!
//! ```text
//! $env:SLATE_WEAK_VOLUME_SMB = '\\localhost\C$\…\scratch'
//! cargo test -p slate-core --locked weak_volume -- --ignored --test-threads=1 --nocapture
//! ```

use std::os::windows::ffi::OsStrExt;
use std::path::{Path, PathBuf};

use windows_sys::Win32::Storage::FileSystem::{
    GetDriveTypeW, GetVolumeInformationW, GetVolumePathNameW,
};

use super::{FsVaultProvider, VaultProvider};
use crate::VaultError;

/// `GetDriveTypeW`'s answer for a network drive.
const DRIVE_REMOTE: u32 = 4;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum VolumeKind {
    Fat32,
    Exfat,
    Smb,
}

impl VolumeKind {
    const ALL: [VolumeKind; 3] = [VolumeKind::Fat32, VolumeKind::Exfat, VolumeKind::Smb];

    fn variable(self) -> &'static str {
        match self {
            VolumeKind::Fat32 => "SLATE_WEAK_VOLUME_FAT32",
            VolumeKind::Exfat => "SLATE_WEAK_VOLUME_EXFAT",
            VolumeKind::Smb => "SLATE_WEAK_VOLUME_SMB",
        }
    }
}

fn wide(path: &Path) -> Vec<u16> {
    path.as_os_str().encode_wide().chain(Some(0)).collect()
}

fn from_wide(buffer: &[u16]) -> String {
    let end = buffer
        .iter()
        .position(|&unit| unit == 0)
        .unwrap_or(buffer.len());
    String::from_utf16_lossy(&buffer[..end])
}

/// The root of the volume `path` is on (`C:\`, or `\\server\share\`).
fn volume_root(path: &Path) -> Result<Vec<u16>, String> {
    let mut root = vec![0u16; 1024];
    // SAFETY: a NUL-terminated path and a buffer of the length passed.
    if unsafe { GetVolumePathNameW(wide(path).as_ptr(), root.as_mut_ptr(), root.len() as u32) } == 0
    {
        return Err(format!(
            "{} has no volume: {}",
            path.display(),
            std::io::Error::last_os_error()
        ));
    }
    let end = root
        .iter()
        .position(|&unit| unit == 0)
        .unwrap_or(root.len());
    root.truncate(end + 1);
    Ok(root)
}

/// The file-system name `GetVolumeInformationW` reports for `path`'s volume.
fn file_system_name(path: &Path) -> Result<String, String> {
    let root = volume_root(path)?;
    let mut name = [0u16; 64];
    // SAFETY: a NUL-terminated root, null for the outputs not asked for, and
    // a name buffer of the length passed.
    if unsafe {
        GetVolumeInformationW(
            root.as_ptr(),
            std::ptr::null_mut(),
            0,
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            name.as_mut_ptr(),
            name.len() as u32,
        )
    } == 0
    {
        return Err(format!(
            "{} reports no file system: {}",
            from_wide(&root),
            std::io::Error::last_os_error()
        ));
    }
    Ok(from_wide(&name))
}

/// The gate's one decision: `Ok` with what the volume reports when `path` is
/// on a volume of `kind` — exactly `FAT32`, exactly `exFAT`, or, for an SMB
/// share, a UNC path whose volume `GetDriveTypeW` reports as `DRIVE_REMOTE`
/// (the UNC root is returned) — and otherwise `Err` naming the kind expected
/// and what the volume reported.
fn check_weak_volume(kind: VolumeKind, path: &Path) -> Result<String, String> {
    let expected = match kind {
        VolumeKind::Fat32 => "FAT32",
        VolumeKind::Exfat => "exFAT",
        VolumeKind::Smb => {
            let text = path.as_os_str().to_string_lossy();
            let unc = text.starts_with(r"\\") && !text.starts_with(r"\\?\")
                || text.starts_with(r"\\?\UNC\");
            let root = volume_root(path)?;
            // SAFETY: a NUL-terminated root path.
            let remote = unsafe { GetDriveTypeW(root.as_ptr()) } == DRIVE_REMOTE;
            return if unc && remote {
                Ok(from_wide(&root))
            } else {
                Err(format!(
                    "expected an SMB share, but {} is not a UNC path on a remote drive",
                    path.display()
                ))
            };
        }
    };
    accept_file_system(expected, file_system_name(path)?, path)
}

/// The FAT half of [`check_weak_volume`], on the name the volume reported:
/// only the exact name passes, so neither FAT kind is taken for the other.
fn accept_file_system(expected: &str, reported: String, path: &Path) -> Result<String, String> {
    if reported == expected {
        Ok(reported)
    } else {
        Err(format!(
            "expected a {expected} volume, but {} is on {reported}",
            path.display()
        ))
    }
}

/// The volumes set in the environment, each checked against its kind.
fn weak_volumes() -> Vec<(VolumeKind, PathBuf)> {
    weak_volumes_from(|name| std::env::var_os(name))
}

/// [`weak_volumes`] over any lookup: an unset variable is skipped, and a set
/// one that names the wrong kind of volume panics, naming the variable.
fn weak_volumes_from(
    lookup: impl Fn(&str) -> Option<std::ffi::OsString>,
) -> Vec<(VolumeKind, PathBuf)> {
    VolumeKind::ALL
        .into_iter()
        .filter_map(|kind| {
            let path = PathBuf::from(lookup(kind.variable())?);
            if let Err(reason) = check_weak_volume(kind, &path) {
                panic!("{}: {reason}", kind.variable());
            }
            Some((kind, path))
        })
        .collect()
}

/// A fresh folder for one fact on one volume, removed when dropped.
struct Scratch(PathBuf);

impl Scratch {
    fn new(volume: &Path, fact: &str) -> Self {
        let stamp = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let path = volume.join(format!("wb27-{fact}-{}-{stamp}", std::process::id()));
        std::fs::create_dir(&path).unwrap_or_else(|error| panic!("{}: {error}", path.display()));
        Scratch(path)
    }

    fn provider(&self) -> FsVaultProvider {
        FsVaultProvider::new(self.0.clone())
    }

    fn read(&self, relative: &str) -> Vec<u8> {
        std::fs::read(self.0.join(relative)).unwrap_or_else(|error| panic!("{relative}: {error}"))
    }

    fn write(&self, relative: &str, bytes: &[u8]) {
        std::fs::write(self.0.join(relative), bytes)
            .unwrap_or_else(|error| panic!("{relative}: {error}"));
    }

    fn exists(&self, relative: &str) -> bool {
        self.0.join(relative).symlink_metadata().is_ok()
    }

    /// The names in the folder, `.slate` aside, sorted.
    fn listing(&self) -> Vec<String> {
        let mut names: Vec<String> = std::fs::read_dir(&self.0)
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .filter(|name| name != ".slate")
            .collect();
        names.sort();
        names
    }
}

impl Drop for Scratch {
    fn drop(&mut self) {
        let _ = std::fs::remove_dir_all(&self.0);
    }
}

fn destination_exists(error: &VaultError, expected: &str) -> bool {
    matches!(error, VaultError::DestinationExists { path } if path == expected)
}

fn create_lands_on_a_free_name_and_preserves_an_occupied_one(volumes: &[(VolumeKind, PathBuf)]) {
    for (kind, volume) in volumes {
        let scratch = Scratch::new(volume, "create");
        let provider = scratch.provider();
        provider
            .write_file_if_absent("new.md", b"created")
            .unwrap_or_else(|error| panic!("{kind:?}: free name: {error:?}"));
        assert_eq!(scratch.read("new.md"), b"created", "{kind:?}");

        // The create has no destination pre-check: this occupant meets the
        // kernel's no-replace publish itself.
        scratch.write("taken.md", b"occupant");
        let error = provider
            .write_file_if_absent("taken.md", b"intruder")
            .expect_err("an occupied name is refused");
        assert!(
            destination_exists(&error, "taken.md"),
            "{kind:?}: {error:?}"
        );
        assert_eq!(scratch.read("taken.md"), b"occupant", "{kind:?}");
        let staged: Vec<_> = std::fs::read_dir(scratch.0.join(".slate").join("tmp"))
            .map(|entries| entries.map(|entry| entry.unwrap().file_name()).collect())
            .unwrap_or_default();
        assert!(
            staged.is_empty(),
            "{kind:?}: the stage was left: {staged:?}"
        );
        println!(
            "OBSERVED {kind:?} create: free name landed; occupied name refused by the publish ({error:?})"
        );
    }
}

fn rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one(
    volumes: &[(VolumeKind, PathBuf)],
) {
    for (kind, volume) in volumes {
        let scratch = Scratch::new(volume, "rename");
        let provider = scratch.provider();
        scratch.write("a.md", b"source a");
        provider
            .rename("a.md", "b.md")
            .unwrap_or_else(|error| panic!("{kind:?}: rename to a free name: {error:?}"));
        assert_eq!(scratch.read("b.md"), b"source a", "{kind:?}");
        assert!(!scratch.exists("a.md"), "{kind:?}");
        std::fs::create_dir(scratch.0.join("sub")).unwrap();
        provider
            .rename("b.md", "sub/a.md")
            .unwrap_or_else(|error| panic!("{kind:?}: move to a free name: {error:?}"));
        assert_eq!(scratch.read("sub/a.md"), b"source a", "{kind:?}");
        assert!(!scratch.exists("b.md"), "{kind:?}");

        // The proof: the occupant is created AFTER the provider's own
        // destination pre-check, so only the kernel's no-replace refuses.
        for (from, to) in [("c.md", "b.md"), ("d.md", "sub/d.md")] {
            scratch.write(from, b"source");
            let raced = std::cell::Cell::new(false);
            let error = provider
                .rename_with_hook_for_tests(from, to, || {
                    std::fs::write(scratch.0.join(to), b"occupant")?;
                    raced.set(true);
                    Ok(())
                })
                .expect_err("a name occupied after the pre-check is refused");
            assert!(raced.get(), "{kind:?} {from} -> {to}: the hook ran");
            assert!(
                destination_exists(&error, to),
                "{kind:?} {from} -> {to}: {error:?}"
            );
            assert_eq!(scratch.read(from), b"source", "{kind:?} {from}");
            assert_eq!(scratch.read(to), b"occupant", "{kind:?} {to}");
        }

        // An occupant present before the call is refused too, by the
        // pre-check (asserted, but not the proof).
        let error = provider
            .rename("c.md", "sub/d.md")
            .expect_err("a pre-existing occupant is refused");
        assert!(
            destination_exists(&error, "sub/d.md"),
            "{kind:?}: {error:?}"
        );
        assert_eq!(scratch.read("c.md"), b"source", "{kind:?}");
        assert_eq!(scratch.read("sub/d.md"), b"occupant", "{kind:?}");
        println!(
            "OBSERVED {kind:?} rename/move: free names landed; occupants raced in after the pre-check refused by the kernel"
        );
    }
}

fn identity_rename_refuses_an_occupied_name_at_the_kernel(volumes: &[(VolumeKind, PathBuf)]) {
    for (kind, volume) in volumes {
        let scratch = Scratch::new(volume, "identity");
        let provider = scratch.provider();
        scratch.write("a.md", b"source");
        scratch.write("b.md", b"occupant");
        match provider.mutation_identity("a.md") {
            Ok(identity) => {
                // No pre-check: the kernel's no-replace is what refuses.
                let error = provider
                    .rename_if_identity("a.md", "b.md", &identity)
                    .expect_err("an occupied name is refused");
                assert!(
                    matches!(&error, VaultError::Io(io) if io.kind() == std::io::ErrorKind::AlreadyExists),
                    "{kind:?}: {error:?}"
                );
                println!(
                    "OBSERVED {kind:?} identity rename: identity answered; the kernel refused the occupied name"
                );
            }
            Err(unanswered) => {
                let error = provider
                    .rename_if_identity(
                        "a.md",
                        "b.md",
                        "0000000000000000-00000000000000000000000000000000",
                    )
                    .expect_err("with no identity, the inverse refuses");
                println!(
                    "OBSERVED {kind:?} identity rename: FileIdInfo did not answer ({unanswered:?}); the inverse refused before the kernel ({error:?})"
                );
            }
        }
        assert_eq!(scratch.read("a.md"), b"source", "{kind:?}");
        assert_eq!(scratch.read("b.md"), b"occupant", "{kind:?}");
    }
}

fn case_only_rename_keeps_the_entry(volumes: &[(VolumeKind, PathBuf)]) {
    for (kind, volume) in volumes {
        let scratch = Scratch::new(volume, "case");
        let provider = scratch.provider();
        scratch.write("Note.md", b"note");
        let before = provider.mutation_identity("Note.md").ok();
        provider
            .rename("Note.md", "note.md")
            .unwrap_or_else(|error| panic!("{kind:?}: case-only rename: {error:?}"));
        assert_eq!(scratch.listing(), ["note.md"], "{kind:?}");
        assert_eq!(scratch.read("note.md"), b"note", "{kind:?}");
        match before {
            Some(before) => {
                let after = provider
                    .mutation_identity("note.md")
                    .unwrap_or_else(|error| panic!("{kind:?}: identity after: {error:?}"));
                assert_eq!(
                    after, before,
                    "{kind:?}: the case-only rename changed the identity"
                );
                println!("OBSERVED {kind:?} case-only: one entry, new spelling, identity kept");
            }
            None => println!(
                "OBSERVED {kind:?} case-only: one entry, new spelling (no FileIdInfo, so the listing is the proof)"
            ),
        }
    }
}

fn stale_identity_refuses_where_ids_are_identities(volumes: &[(VolumeKind, PathBuf)]) {
    for (kind, volume) in volumes {
        let scratch = Scratch::new(volume, "stale");
        let provider = scratch.provider();
        scratch.write("a.md", b"first");
        let Ok(first) = provider.mutation_identity("a.md") else {
            let error = provider
                .rename_if_identity(
                    "a.md",
                    "c.md",
                    "0000000000000000-00000000000000000000000000000000",
                )
                .expect_err("with no identity, every identity-checked rename refuses");
            assert_eq!(scratch.read("a.md"), b"first", "{kind:?}");
            assert!(!scratch.exists("c.md"), "{kind:?}");
            println!(
                "OBSERVED {kind:?} stale identity: FileIdInfo did not answer; every identity-checked rename refuses ({error:?})"
            );
            continue;
        };
        // Whether the id survives an ordinary rename (asserted only where
        // ids are identities).
        provider.rename("a.md", "moved.md").unwrap();
        let moved = provider.mutation_identity("moved.md").unwrap();
        // A different file now holds the old name.
        scratch.write("a.md", b"second");
        let second = provider.mutation_identity("a.md").unwrap();
        let outcome = provider.rename_if_identity("a.md", "c.md", &first);
        match kind {
            VolumeKind::Smb => {
                assert_eq!(moved, first, "{kind:?}: the id did not survive the rename");
                assert!(outcome.is_err(), "{kind:?}: a stale identity was accepted");
                assert_eq!(scratch.read("a.md"), b"second", "{kind:?}");
                assert!(!scratch.exists("c.md"), "{kind:?}");
            }
            VolumeKind::Fat32 | VolumeKind::Exfat => {}
        }
        println!(
            "OBSERVED {kind:?} stale identity: id survives a rename: {}; the new file's id equals the old: {}; stale rename: {outcome:?}",
            moved == first,
            second == first
        );
    }
}

fn gate_names_the_file_system_it_ran_on(volumes: &[(VolumeKind, PathBuf)]) {
    for (kind, volume) in volumes {
        let reported =
            check_weak_volume(*kind, volume).unwrap_or_else(|reason| panic!("{kind:?}: {reason}"));
        match kind {
            VolumeKind::Fat32 => assert_eq!(reported, "FAT32"),
            VolumeKind::Exfat => assert_eq!(reported, "exFAT"),
            VolumeKind::Smb => assert!(reported.starts_with(r"\\"), "{reported}"),
        }
        for other in VolumeKind::ALL.into_iter().filter(|other| other != kind) {
            assert!(
                check_weak_volume(other, volume).is_err(),
                "{kind:?} volume {} was accepted as {other:?}",
                volume.display()
            );
        }
        println!("OBSERVED {kind:?} gate: reports {reported}; refused as the other two kinds");
    }
}

#[test]
#[ignore = "needs SLATE_WEAK_VOLUME_* set to a scratch directory on a mounted volume"]
fn weak_volume_create_lands_on_a_free_name_and_preserves_an_occupied_one() {
    create_lands_on_a_free_name_and_preserves_an_occupied_one(&weak_volumes());
}

#[test]
#[ignore = "needs SLATE_WEAK_VOLUME_* set to a scratch directory on a mounted volume"]
fn weak_volume_rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one() {
    rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one(&weak_volumes());
}

#[test]
#[ignore = "needs SLATE_WEAK_VOLUME_* set to a scratch directory on a mounted volume"]
fn weak_volume_identity_rename_refuses_an_occupied_name_at_the_kernel() {
    identity_rename_refuses_an_occupied_name_at_the_kernel(&weak_volumes());
}

#[test]
#[ignore = "needs SLATE_WEAK_VOLUME_* set to a scratch directory on a mounted volume"]
fn weak_volume_case_only_rename_keeps_the_entry() {
    case_only_rename_keeps_the_entry(&weak_volumes());
}

#[test]
#[ignore = "needs SLATE_WEAK_VOLUME_* set to a scratch directory on a mounted volume"]
fn weak_volume_stale_identity_refuses_where_ids_are_identities() {
    stale_identity_refuses_where_ids_are_identities(&weak_volumes());
}

#[test]
#[ignore = "needs SLATE_WEAK_VOLUME_* set to a scratch directory on a mounted volume"]
fn weak_volume_gate_names_the_file_system_it_ran_on() {
    gate_names_the_file_system_it_ran_on(&weak_volumes());
}

/// With no variable set, the gate finds no volume and every gated body
/// returns at once, so an `--ignored` run on a bare runner stays green.
#[test]
fn weak_volume_gate_is_quiet_when_no_volume_is_set() {
    let none = weak_volumes_from(|_| None);
    assert!(none.is_empty(), "{none:?}");
    create_lands_on_a_free_name_and_preserves_an_occupied_one(&none);
    rename_and_move_land_on_a_free_name_and_refuse_an_occupied_one(&none);
    identity_rename_refuses_an_occupied_name_at_the_kernel(&none);
    case_only_rename_keeps_the_entry(&none);
    stale_identity_refuses_where_ids_are_identities(&none);
    gate_names_the_file_system_it_ran_on(&none);
}

/// The deliberately mislabelled volume, on every machine: a folder on the
/// system temp directory's volume, read first and asserted to be neither
/// FAT kind, is refused as each of the three kinds, so a mislabelled
/// variable can never run the gated facts on the wrong file system.
#[test]
fn weak_volume_gate_refuses_a_volume_whose_file_system_does_not_match_its_kind() {
    let folder = tempfile::tempdir().unwrap();
    let reported = file_system_name(folder.path()).unwrap();
    assert!(
        reported != "FAT32" && reported != "exFAT",
        "the precondition: the temp directory is on {reported}"
    );
    for kind in [VolumeKind::Fat32, VolumeKind::Exfat] {
        let reason =
            check_weak_volume(kind, folder.path()).expect_err("an NTFS folder is not a FAT volume");
        assert!(
            reason.contains(&format!("is on {reported}")),
            "{kind:?}: {reason}"
        );
    }
    let reason = check_weak_volume(VolumeKind::Smb, folder.path())
        .expect_err("a local folder is not an SMB share");
    assert!(
        reason.contains("is not a UNC path on a remote drive"),
        "{reason}"
    );
    // And the lookup refuses the mislabelled variable by name.
    let panicked = std::panic::catch_unwind(|| {
        weak_volumes_from(|name| {
            (name == "SLATE_WEAK_VOLUME_EXFAT").then(|| folder.path().as_os_str().to_owned())
        })
    })
    .expect_err("a mislabelled variable panics");
    let message = panicked
        .downcast_ref::<String>()
        .cloned()
        .unwrap_or_default();
    assert!(
        message.starts_with("SLATE_WEAK_VOLUME_EXFAT: "),
        "{message}"
    );
}

/// The FAT names, on every machine: FAT32 and exFAT are told apart by
/// their exact names, which a mounted volume of each would otherwise be
/// the only witness to.
#[test]
fn weak_volume_gate_tells_the_two_fat_kinds_apart_by_their_exact_names() {
    let path = Path::new(r"X:\probe");
    assert_eq!(
        accept_file_system("FAT32", "FAT32".into(), path),
        Ok("FAT32".into())
    );
    assert_eq!(
        accept_file_system("exFAT", "exFAT".into(), path),
        Ok("exFAT".into())
    );
    for (expected, reported) in [
        ("FAT32", "exFAT"),
        ("exFAT", "FAT32"),
        ("FAT32", "FAT"),
        ("exFAT", "NTFS"),
    ] {
        let reason = accept_file_system(expected, reported.into(), path)
            .expect_err("only the exact name passes");
        assert_eq!(
            reason,
            format!(r"expected a {expected} volume, but X:\probe is on {reported}")
        );
    }
}
