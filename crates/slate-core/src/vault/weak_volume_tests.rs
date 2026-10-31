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
use std::os::windows::fs::OpenOptionsExt;
use std::os::windows::io::AsRawHandle;
use std::path::{Path, PathBuf};

use windows_sys::Win32::Storage::FileSystem::{
    FILE_FLAG_BACKUP_SEMANTICS, FILE_REMOTE_PROTOCOL_INFO, FileRemoteProtocolInfo, GetDriveTypeW,
    GetFileInformationByHandleEx, GetVolumeInformationByHandleW, GetVolumeInformationW,
    GetVolumePathNameW,
};

use super::{FsVaultProvider, VaultProvider};
use crate::VaultError;

/// What a gated fact saw on a real volume, printed as its record for the
/// matrix rows (run with `--nocapture`). The only output these facts make,
/// and it names only the scratch entries they created.
#[expect(
    clippy::disallowed_macros,
    reason = "the gated facts' observations are their record for the capability matrix (WB-27)"
)]
fn observe(line: std::fmt::Arguments<'_>) {
    println!("{line}");
}

macro_rules! observed {
    ($($arg:tt)*) => {
        observe(format_args!($($arg)*))
    };
}

/// `GetDriveTypeW`'s answer for a network drive.
const DRIVE_REMOTE: u32 = 4;
/// `GetDriveTypeW`'s answer for a fixed local disk.
const DRIVE_FIXED: u32 = 3;
/// `FILE_REMOTE_PROTOCOL_INFO.Protocol` for SMB (`WNNC_NET_SMB`).
const WNNC_NET_SMB: u32 = 0x0002_0000;
/// The Plan 9 redirector WSL's `\\wsl.localhost` shares answer with
/// (`WNNC_NET_9P`): remote and UNC, but not SMB.
const WNNC_NET_9P: u32 = 0x0048_0000;
/// An identity no file has, for the calls made where none answers.
const NO_IDENTITY: &str = "0000000000000000-00000000000000000000000000000000";

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

/// What a volume reports about itself, read once by [`read_volume`] and
/// judged by [`decide`].
#[derive(Clone, Debug)]
struct VolumeReport {
    /// The volume's root (`C:\`, or `\\server\share\`).
    root: String,
    /// The path names the volume as a UNC path (`\\server\share\…` or
    /// `\\?\UNC\server\share\…`), not through a drive letter.
    unc: bool,
    /// `GetDriveTypeW` for the root.
    drive_type: u32,
    /// `GetVolumeInformationW`'s file-system name (a share reports its
    /// server's).
    file_system: String,
    /// `FileRemoteProtocolInfo`'s protocol, for a remote volume that answers.
    protocol: Option<u32>,
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

/// The folder `path` opened for its attributes (a directory needs
/// `FILE_FLAG_BACKUP_SEMANTICS`).
fn open_folder(path: &Path) -> std::io::Result<std::fs::File> {
    std::fs::OpenOptions::new()
        .read(true)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
        .open(path)
}

/// The network protocol serving `path`, or `None` where the volume does not
/// say.
fn remote_protocol(path: &Path) -> Option<u32> {
    /// The query answers `ERROR_NOACCESS` into a buffer aligned only as the
    /// structure's fields are (4 bytes); 8 is what it takes.
    #[repr(C, align(8))]
    struct Aligned(FILE_REMOTE_PROTOCOL_INFO);

    let folder = open_folder(path).ok()?;
    let mut info = Aligned(FILE_REMOTE_PROTOCOL_INFO::default());
    // SAFETY: a live handle and the exact, initialized, 8-aligned output
    // structure.
    let answered = unsafe {
        GetFileInformationByHandleEx(
            folder.as_raw_handle(),
            FileRemoteProtocolInfo,
            (&mut info.0 as *mut FILE_REMOTE_PROTOCOL_INFO).cast(),
            std::mem::size_of::<FILE_REMOTE_PROTOCOL_INFO>() as u32,
        )
    } != 0;
    answered.then_some(info.0.Protocol)
}

/// Everything the gate judges, read from the volume `path` is on.
fn read_volume(path: &Path) -> Result<VolumeReport, String> {
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
    // SAFETY: a NUL-terminated root path.
    let drive_type = unsafe { GetDriveTypeW(root.as_ptr()) };
    let text = path.as_os_str().to_string_lossy();
    let unc =
        text.starts_with(r"\\") && !text.starts_with(r"\\?\") || text.starts_with(r"\\?\UNC\");
    Ok(VolumeReport {
        root: from_wide(&root),
        unc,
        drive_type,
        file_system: from_wide(&name),
        protocol: (drive_type == DRIVE_REMOTE)
            .then(|| remote_protocol(path))
            .flatten(),
    })
}

/// The file-system name a FAT kind must report exactly.
fn file_system_for(kind: VolumeKind) -> Option<&'static str> {
    match kind {
        VolumeKind::Fat32 => Some("FAT32"),
        VolumeKind::Exfat => Some("exFAT"),
        VolumeKind::Smb => None,
    }
}

/// The gate's one decision, on what the volume reported: `Ok` with what it
/// reports when it is the kind asked for, otherwise `Err` naming the kind
/// expected and what the volume reported.
/// - A FAT kind: a local volume (no UNC path, not `DRIVE_REMOTE`, which would
///   pass a share's SERVER file system through) reporting exactly `FAT32` or
///   `exFAT`.
/// - SMB: a UNC path on a `DRIVE_REMOTE` volume served by the SMB protocol, so
///   another redirector (WSL's 9P, WebDAV) is never taken for SMB. The UNC
///   root is returned.
fn decide(kind: VolumeKind, report: &VolumeReport, path: &Path) -> Result<String, String> {
    let remote = report.drive_type == DRIVE_REMOTE;
    match file_system_for(kind) {
        Some(expected) if report.unc || remote => Err(format!(
            "expected a local {expected} volume, but {} is on a remote drive (drive type {}, {})",
            path.display(),
            report.drive_type,
            report.file_system
        )),
        Some(expected) if report.file_system == expected => Ok(report.file_system.clone()),
        Some(expected) => Err(format!(
            "expected a {expected} volume, but {} is on {}",
            path.display(),
            report.file_system
        )),
        None if !(report.unc && remote) => Err(format!(
            "expected an SMB share, but {} is not a UNC path on a remote drive (drive type {})",
            path.display(),
            report.drive_type
        )),
        None if report.protocol == Some(WNNC_NET_SMB) => Ok(report.root.clone()),
        None => Err(format!(
            "expected an SMB share, but {} is served by network protocol {}, not SMB",
            path.display(),
            report
                .protocol
                .map_or_else(|| "(unreported)".to_string(), |p| format!("{p:#x}"))
        )),
    }
}

/// [`decide`] on the volume `path` is on.
fn check_weak_volume(kind: VolumeKind, path: &Path) -> Result<String, String> {
    decide(kind, &read_volume(path)?, path)
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

/// The identity inverse's refusal (`windows_entry::refused`).
fn identity_refused(error: &VaultError) -> bool {
    matches!(error, VaultError::InvalidArgument { .. })
}

/// The entry's `FILE_ID_INFO` identity. SMB must answer (the facts' identity
/// claims rest on it); a FAT volume may not, which is recorded and answered
/// `None`.
fn identity_on(kind: VolumeKind, provider: &FsVaultProvider, relative: &str) -> Option<String> {
    match provider.mutation_identity(relative) {
        Ok(identity) => Some(identity),
        Err(error) if kind == VolumeKind::Smb => {
            panic!("{kind:?}: FILE_ID_INFO must answer on an SMB share: {error:?}")
        }
        Err(error) => {
            observed!("OBSERVED {kind:?} FILE_ID_INFO did not answer for {relative}: {error:?}");
            None
        }
    }
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
            .unwrap_or_else(|error| panic!("{kind:?}: the stage folder: {error}"))
            .map(|entry| entry.unwrap().file_name())
            .collect();
        assert!(
            staged.is_empty(),
            "{kind:?}: the stage was left: {staged:?}"
        );
        observed!(
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
        observed!(
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
        match identity_on(*kind, &provider, "a.md") {
            Some(identity) => {
                // No pre-check: the kernel's no-replace is what refuses.
                let error = provider
                    .rename_if_identity("a.md", "b.md", &identity)
                    .expect_err("an occupied name is refused");
                assert!(
                    matches!(&error, VaultError::Io(io) if io.kind() == std::io::ErrorKind::AlreadyExists),
                    "{kind:?}: {error:?}"
                );
                observed!(
                    "OBSERVED {kind:?} identity rename: identity answered; the kernel refused the occupied name"
                );
            }
            None => {
                let error = provider
                    .rename_if_identity("a.md", "b.md", NO_IDENTITY)
                    .expect_err("with no identity, the inverse refuses");
                observed!(
                    "OBSERVED {kind:?} identity rename: no identity to check; the inverse refused ({error:?})"
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
        let before = identity_on(*kind, &provider, "Note.md");
        provider
            .rename("Note.md", "note.md")
            .unwrap_or_else(|error| panic!("{kind:?}: case-only rename: {error:?}"));
        assert_eq!(scratch.listing(), ["note.md"], "{kind:?}");
        assert_eq!(scratch.read("note.md"), b"note", "{kind:?}");
        let Some(before) = before else {
            observed!(
                "OBSERVED {kind:?} case-only: one entry, new spelling (no FileIdInfo, so the listing is the proof)"
            );
            continue;
        };
        let after = identity_on(*kind, &provider, "note.md").expect("the identity answered before");
        assert_eq!(
            after, before,
            "{kind:?}: the case-only rename changed the identity"
        );

        // The provider's rename may retry a case-only alias WITH replacement
        // when the volume reports the alias occupied; the identity inverse
        // makes only the no-replace call. That it performs the same case-only
        // rename proves the no-replace call alone does it on this volume.
        scratch.write("Other.md", b"other");
        let other = identity_on(*kind, &provider, "Other.md").expect("the identity answered");
        provider
            .rename_if_identity("Other.md", "other.md", &other)
            .unwrap_or_else(|error| {
                panic!("{kind:?}: the no-replace call refused a case-only rename: {error:?}")
            });
        assert_eq!(scratch.listing(), ["note.md", "other.md"], "{kind:?}");
        assert_eq!(
            identity_on(*kind, &provider, "other.md").as_deref(),
            Some(other.as_str()),
            "{kind:?}"
        );
        observed!(
            "OBSERVED {kind:?} case-only: one entry, new spelling, identity kept; the no-replace call alone renames the alias"
        );
    }
}

fn stale_identity_refuses_where_ids_are_identities(volumes: &[(VolumeKind, PathBuf)]) {
    for (kind, volume) in volumes {
        let scratch = Scratch::new(volume, "stale");
        let provider = scratch.provider();
        scratch.write("a.md", b"first");
        let Some(first) = identity_on(*kind, &provider, "a.md") else {
            let error = provider
                .rename_if_identity("a.md", "c.md", NO_IDENTITY)
                .expect_err("with no identity, every identity-checked rename refuses");
            assert_eq!(scratch.read("a.md"), b"first", "{kind:?}");
            assert!(!scratch.exists("c.md"), "{kind:?}");
            observed!(
                "OBSERVED {kind:?} stale identity: no identity answers, so every identity-checked rename refuses ({error:?})"
            );
            continue;
        };
        provider.rename("a.md", "moved.md").unwrap();
        let moved = identity_on(*kind, &provider, "moved.md").expect("the identity answered");
        // A different file now holds the old name.
        scratch.write("a.md", b"second");
        let second = identity_on(*kind, &provider, "a.md").expect("the identity answered");
        let outcome = provider.rename_if_identity("a.md", "c.md", &first);
        observed!(
            "OBSERVED {kind:?} stale identity: id survives a rename: {}; the new file's id equals the old: {}; stale rename: {outcome:?}",
            moved == first,
            second == first
        );
        // On EVERY volume whose identity answers: an undo that renamed a
        // different file under the old name is the hazard the inverse exists
        // to stop, so an accepted stale identity fails here (a blocking
        // finding for PR 38), not a printed observation.
        assert!(
            outcome.as_ref().is_err_and(identity_refused),
            "{kind:?}: a stale identity was not refused: {outcome:?}"
        );
        assert_eq!(scratch.read("a.md"), b"second", "{kind:?}");
        assert!(!scratch.exists("c.md"), "{kind:?}");
        if *kind == VolumeKind::Smb {
            assert_eq!(moved, first, "{kind:?}: the id did not survive the rename");
        }
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
        observed!("OBSERVED {kind:?} gate: reports {reported}; refused as the other two kinds");
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

/// `path`'s file system read through a handle on the folder itself
/// (`GetVolumeInformationByHandleW`), independently of the gate's
/// root-path read.
fn file_system_by_handle(path: &Path) -> String {
    let folder = open_folder(path).unwrap();
    let mut name = [0u16; 64];
    // SAFETY: a live handle, null for the outputs not asked for, and a name
    // buffer of the length passed.
    let answered = unsafe {
        GetVolumeInformationByHandleW(
            folder.as_raw_handle(),
            std::ptr::null_mut(),
            0,
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            name.as_mut_ptr(),
            name.len() as u32,
        )
    } != 0;
    assert!(
        answered,
        "{}: {}",
        path.display(),
        std::io::Error::last_os_error()
    );
    from_wide(&name)
}

/// The deliberately mislabelled volume, on every machine: a folder on the
/// system temp directory's volume, read first and asserted to be neither
/// FAT kind, is refused as each of the three kinds, so a mislabelled
/// variable can never run the gated facts on the wrong file system.
#[test]
fn weak_volume_gate_refuses_a_volume_whose_file_system_does_not_match_its_kind() {
    let folder = tempfile::tempdir().unwrap();
    let reported = file_system_by_handle(folder.path());
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
        reason.contains("is not a UNC path on a remote drive (drive type"),
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

/// The gate's decision table, on every machine, over what volumes report:
/// the volumes this probe could not mount (FAT32, exFAT, a share served by
/// another redirector, a FAT volume reached through a share) would otherwise
/// be its only witnesses.
#[test]
fn weak_volume_gate_decides_each_kind_from_what_the_volume_reports() {
    let path = Path::new(r"X:\probe");
    let report =
        |unc: bool, drive_type: u32, file_system: &str, protocol: Option<u32>| VolumeReport {
            root: if unc {
                r"\\host\share\".into()
            } else {
                r"X:\".into()
            },
            unc,
            drive_type,
            file_system: file_system.into(),
            protocol,
        };
    let local = |file_system: &str| report(false, DRIVE_FIXED, file_system, None);
    let share = |protocol: Option<u32>| report(true, DRIVE_REMOTE, "NTFS", protocol);

    // The FAT kinds: the exact name, on a local volume only.
    assert_eq!(
        decide(VolumeKind::Fat32, &local("FAT32"), path),
        Ok("FAT32".into())
    );
    assert_eq!(
        decide(VolumeKind::Exfat, &local("exFAT"), path),
        Ok("exFAT".into())
    );
    for (kind, reported) in [
        (VolumeKind::Fat32, "exFAT"),
        (VolumeKind::Exfat, "FAT32"),
        (VolumeKind::Fat32, "FAT"),
        (VolumeKind::Exfat, "NTFS"),
    ] {
        let expected = file_system_for(kind).unwrap();
        assert_eq!(
            decide(kind, &local(reported), path),
            Err(format!(
                r"expected a {expected} volume, but X:\probe is on {reported}"
            ))
        );
    }
    // A FAT volume through a share reports its server's file system: never
    // taken for local FAT, whether named by UNC path or a mapped letter.
    for remote_fat in [
        report(true, DRIVE_REMOTE, "FAT32", Some(WNNC_NET_SMB)),
        report(false, DRIVE_REMOTE, "FAT32", Some(WNNC_NET_SMB)),
        report(true, DRIVE_FIXED, "FAT32", None),
    ] {
        let reason =
            decide(VolumeKind::Fat32, &remote_fat, path).expect_err("a share is not local");
        assert!(reason.contains("expected a local FAT32 volume"), "{reason}");
    }

    // SMB: a UNC path, a remote drive and the SMB protocol, all three.
    assert_eq!(
        decide(VolumeKind::Smb, &share(Some(WNNC_NET_SMB)), path),
        Ok(r"\\host\share\".into())
    );
    let mapped_letter = report(false, DRIVE_REMOTE, "NTFS", Some(WNNC_NET_SMB));
    let unc_but_local = report(true, DRIVE_FIXED, "NTFS", Some(WNNC_NET_SMB));
    for not_a_share in [&mapped_letter, &unc_but_local, &local("NTFS")] {
        let reason = decide(VolumeKind::Smb, not_a_share, path).expect_err("not a UNC remote path");
        assert!(
            reason.contains("is not a UNC path on a remote drive"),
            "{reason}"
        );
    }
    for other in [Some(WNNC_NET_9P), None] {
        let reason = decide(VolumeKind::Smb, &share(other), path).expect_err("not SMB");
        assert!(reason.contains("not SMB"), "{reason}");
    }
}
