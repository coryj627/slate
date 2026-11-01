// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Windows no-follow entry resolution: the handle walk beneath the vault root
//! that forward moves, saves, creations and the Trash handoff take, and the
//! identity-conditional rename undo and redo use. File IDs are
//! filesystem-issued values, not permanent incarnation IDs: a filesystem may
//! eventually reuse one after deletion. Comparison and rename use one live
//! handle.
//!
//! ## The walk (#1450, #1451)
//!
//! A path-addressed call (`MoveFileExW`, `CreateDirectoryW`, a rename of a
//! staged file onto its target) resolves the whole `\\?\` path again, and
//! NTFS follows a junction or a directory symbolic link met on the way: the
//! `\\?\` prefix turns off Win32 path parsing, not reparse processing. So a
//! vault folder replaced by a junction after Slate saw it redirected a move,
//! a save or a new folder across the vault boundary, and creating a junction
//! needs no privilege. [`PinnedRoot`] is that walk without a path:
//!
//! - The vault root is opened once as a directory handle, following its own
//!   reparse point, so a vault whose root is a junction or a symbolic link
//!   keeps working: the handle is the root.
//! - Every folder beneath it is opened relative to the handle before it,
//!   one component at a time, with `FILE_OPEN_REPARSE_POINT`
//!   ([`open_relative`]), so no reparse point below the root is ever
//!   followed: a link's own entry is what opens.
//! - A folder that is a junction, a symbolic link or any other name
//!   surrogate, or that is not a folder at all, is the typed refusal
//!   (`VaultError::InvalidPath`) `pinned_root` gives on macOS and Linux,
//!   naming it and ending with what did not happen ([`Mutation`]).
//! - A missing folder is created relative to its pinned parent
//!   (`NtCreateFile` with `FILE_CREATE` and `FILE_DIRECTORY_FILE`), and the
//!   call acts relative to the last handle only: a rename by
//!   `FileRenameInformation` whose `RootDirectory` is the pinned folder, a
//!   new folder made in it.
//!
//! **Which reparse points refuse.** A name surrogate (MS-FSCC 2.1.2.1: the
//! tag's N bit) stands for another named entity; that is what a junction
//! (`IO_REPARSE_TAG_MOUNT_POINT`) and a symbolic link are, and what redirects
//! a path. A reparse point without that bit does not redirect anything: it is
//! a folder whose storage a filter manages, such as a Cloud Files placeholder.
//! Every folder of a OneDrive vault with Files On-Demand is one, and Windows
//! shows it to applications by default (rust-lang/rust#46484 measured such a
//! folder's attributes as `0x410`, directory plus reparse point). Refusing it
//! would refuse every save beneath a OneDrive vault's folders, so forward
//! moves, saves and creations admit it ([`Folders::NotLinks`]), the line
//! Rust's own `FileType::is_symlink` draws (rust-lang/rust#47956). The Trash
//! handoff keeps #1450's rule and refuses every reparse point
//! ([`Folders::PlainOnly`]).
//!
//! Every folder on the path stays open until the call ends, shared for
//! reading and writing but not for deletion, so none of them can be renamed
//! or removed in between. Sharing does not stop an empty folder from being
//! converted into a junction in place (`FSCTL_SET_REPARSE_POINT` through a
//! handle holding only attribute access); a call made in such a folder fails
//! instead of following it, and [`PinnedFolder::failed`] turns that failure
//! into the typed refusal.
//!
//! The final component is still a name: a link or a junction there is
//! renamed as itself, never followed, and binding the leaf's identity for a
//! forward move is a #1125 follow-up (#1469).

use super::fs::Mutation;
use crate::VaultError;
use std::os::windows::{
    ffi::OsStrExt,
    fs::{MetadataExt, OpenOptionsExt},
    io::{AsRawHandle, FromRawHandle},
};
use std::{
    ffi::OsStr,
    fs, io,
    path::{Component, Path, PathBuf},
};
use windows_sys::{
    Wdk::{
        Foundation::OBJECT_ATTRIBUTES,
        Storage::FileSystem::{
            FILE_CREATE, FILE_DELETE_ON_CLOSE, FILE_DIRECTORY_FILE, FILE_NON_DIRECTORY_FILE,
            FILE_OPEN, FILE_OPEN_IF, FILE_OPEN_REPARSE_POINT, FILE_OVERWRITE, FILE_OVERWRITE_IF,
            FILE_RENAME_INFORMATION, FILE_SYNCHRONOUS_IO_NONALERT, FileRenameInformation,
            NtCreateFile, NtSetInformationFile,
        },
    },
    Win32::{
        Foundation::{OBJ_CASE_INSENSITIVE, RtlNtStatusToDosError, UNICODE_STRING},
        Storage::FileSystem::*,
        System::IO::IO_STATUS_BLOCK,
    },
};

/// `IO_REPARSE_TAG_MOUNT_POINT` (MS-FSCC 2.1.2.1): a junction, or a volume
/// mounted on a folder. These tags are named here rather than by enabling
/// another `windows-sys` feature for three constants.
const IO_REPARSE_TAG_MOUNT_POINT: u32 = 0xA000_0003;

/// `IO_REPARSE_TAG_SYMLINK`: a symbolic link.
const IO_REPARSE_TAG_SYMLINK: u32 = 0xA000_000C;

/// `IO_REPARSE_TAG_LX_SYMLINK`: a symbolic link made by WSL.
const IO_REPARSE_TAG_LX_SYMLINK: u32 = 0xA000_001D;

/// A reparse tag's name-surrogate bit (MS-FSCC 2.1.2.1, the N bit that
/// `IsReparseTagNameSurrogate` tests): the entry stands for another named
/// entity, which is how a junction or a link redirects a path.
const REPARSE_TAG_NAME_SURROGATE: u32 = 0x2000_0000;

fn simple_name(name: &[u16]) -> bool {
    !(name.is_empty()
        || name == [46]
        || name == [46, 46]
        || name.iter().any(|c| matches!(*c, 0 | 47 | 58 | 92)))
}

/// `NtCreateFile` for the single component `name` relative to the open
/// directory `parent`. A single component plus `FILE_OPEN_REPARSE_POINT`
/// never follows a child link; converted parent reparses fail instead of
/// re-resolving a path.
pub(super) fn create_relative(
    parent: &fs::File,
    name: &OsStr,
    access: u32,
    share: u32,
    disposition: u32,
    options: u32,
) -> io::Result<fs::File> {
    // A new folder takes the attributes `CreateDirectoryW` gives one; an
    // open ignores them.
    let file_attributes = if disposition == FILE_CREATE {
        FILE_ATTRIBUTE_NORMAL
    } else {
        0
    };
    create_relative_with(
        parent,
        name,
        access,
        share,
        disposition,
        options,
        file_attributes,
    )
}

/// [`create_relative`] with the attributes a created entry takes.
fn create_relative_with(
    parent: &fs::File,
    name: &OsStr,
    access: u32,
    share: u32,
    disposition: u32,
    options: u32,
    file_attributes: u32,
) -> io::Result<fs::File> {
    let mut name: Vec<u16> = name.encode_wide().collect();
    if !simple_name(&name) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Expected a simple entry name",
        ));
    }
    nt_create_relative(
        parent,
        &mut name,
        access | SYNCHRONIZE,
        share,
        disposition,
        options | FILE_SYNCHRONOUS_IO_NONALERT,
        file_attributes,
    )
}

/// A new handle with a file object of its own on the folder `dir` is open
/// on: `NtCreateFile` relative to `dir` with an empty name (plan 44 WB-28).
/// A duplicated handle shares the original's file object, and with it a
/// listing's position; this one does not.
pub(super) fn reopen(dir: &fs::File, access: u32, share: u32) -> io::Result<fs::File> {
    nt_create_relative(
        dir,
        &mut Vec::new(),
        access | SYNCHRONIZE,
        share,
        FILE_OPEN,
        FILE_SYNCHRONOUS_IO_NONALERT,
        0,
    )
}

/// The entry at the vault-relative `components` beneath `parent`, resolved
/// in ONE `NtCreateFile` call whose name is the components joined by `\`,
/// relative to `parent` (plan 44 WB-28, codex round 3). The system parses
/// the folders on the way as it parses a path, so a folder the caller may
/// only pass (traverse checking bypassed, as for every path open) or a
/// reparse point among them behaves exactly as for a path open; only the
/// starting point is the held handle. `options` may carry
/// `FILE_OPEN_REPARSE_POINT` (the leaf opens as itself, `symlink_metadata`'s
/// rule) or `FILE_DIRECTORY_FILE`. `synchronous` adds `SYNCHRONIZE` and
/// synchronous I/O for a handle that is read; a metadata probe passes
/// `access` 0 and no synchronous I/O, as `std::fs::metadata` opens.
pub(super) fn open_beneath_path(
    parent: &fs::File,
    components: &[&OsStr],
    access: u32,
    options: u32,
    synchronous: bool,
) -> io::Result<fs::File> {
    let mut name: Vec<u16> = Vec::new();
    for (index, component) in components.iter().enumerate() {
        let wide: Vec<u16> = component.encode_wide().collect();
        if !simple_name(&wide) {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Expected a simple entry name",
            ));
        }
        if index > 0 {
            name.push(u16::from(b'\\'));
        }
        name.extend(wide);
    }
    let (access, options) = if synchronous {
        (access | SYNCHRONIZE, options | FILE_SYNCHRONOUS_IO_NONALERT)
    } else {
        (access, options)
    };
    nt_create_relative(
        parent,
        &mut name,
        access,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        FILE_OPEN,
        options,
        0,
    )
}

/// The `NtCreateFile` call itself, for a name already checked (or empty: the
/// parent itself), with exactly the access and options given.
fn nt_create_relative(
    parent: &fs::File,
    name: &mut Vec<u16>,
    access: u32,
    share: u32,
    disposition: u32,
    options: u32,
    file_attributes: u32,
) -> io::Result<fs::File> {
    let length = u16::try_from(name.len() * 2)
        .map_err(|_| io::Error::new(io::ErrorKind::InvalidInput, "Entry name is too long"))?;
    let mut unicode = UNICODE_STRING {
        Length: length,
        MaximumLength: length,
        Buffer: name.as_mut_ptr(),
    };
    let attributes = OBJECT_ATTRIBUTES {
        Length: std::mem::size_of::<OBJECT_ATTRIBUTES>() as u32,
        RootDirectory: parent.as_raw_handle(),
        ObjectName: &mut unicode,
        Attributes: OBJ_CASE_INSENSITIVE,
        ..Default::default()
    };
    let mut handle = std::ptr::null_mut();
    let mut status_block: IO_STATUS_BLOCK = unsafe { std::mem::zeroed() };
    // SAFETY: the synchronous call borrows live name/attribute/status buffers and
    // the parent handle.
    let status = unsafe {
        NtCreateFile(
            &mut handle,
            access,
            &attributes,
            &mut status_block,
            std::ptr::null(),
            file_attributes,
            share,
            disposition,
            options,
            std::ptr::null(),
            0,
        )
    };
    if status < 0 {
        return Err(io::Error::from_raw_os_error(
            unsafe { RtlNtStatusToDosError(status) } as i32,
        ));
    }
    // SAFETY: a successful NtCreateFile transfers a newly owned file handle.
    Ok(unsafe { fs::File::from_raw_handle(handle) })
}

/// `CreateFileW`'s arguments, as SQLite passes them, for the single
/// component `name` relative to `parent` (the SQLite hook, `db::anchored`,
/// plan 44 WB-28): the disposition is translated to its `NtCreateFile`
/// twin, `FILE_FLAG_DELETE_ON_CLOSE` becomes `FILE_DELETE_ON_CLOSE` with
/// `DELETE` access, and the entry itself opens (`FILE_OPEN_REPARSE_POINT`,
/// `FILE_NON_DIRECTORY_FILE`). The caller refuses a reparse point.
pub(crate) fn sqlite_create_relative(
    parent: &fs::File,
    name: &OsStr,
    access: u32,
    share: u32,
    disposition: u32,
    flags_and_attributes: u32,
) -> io::Result<fs::File> {
    let disposition = match disposition {
        CREATE_NEW => FILE_CREATE,
        CREATE_ALWAYS => FILE_OVERWRITE_IF,
        OPEN_EXISTING => FILE_OPEN,
        OPEN_ALWAYS => FILE_OPEN_IF,
        TRUNCATE_EXISTING => FILE_OVERWRITE,
        _ => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Unknown creation disposition",
            ));
        }
    };
    let mut access = access;
    let mut options = FILE_NON_DIRECTORY_FILE | FILE_OPEN_REPARSE_POINT;
    if flags_and_attributes & FILE_FLAG_DELETE_ON_CLOSE != 0 {
        access |= DELETE;
        options |= FILE_DELETE_ON_CLOSE;
    }
    // The low 16 bits are attributes; the flags above them are translated
    // or (like `FILE_FLAG_RANDOM_ACCESS`, a hint) dropped.
    let attributes = flags_and_attributes & 0xFFFF;
    create_relative_with(
        parent,
        name,
        access,
        share,
        disposition,
        options,
        attributes,
    )
}

/// Open the entry `name` itself (a link or a junction is opened, never
/// followed) relative to `parent`, shared for reading and writing.
pub(super) fn open_relative(parent: &fs::File, name: &OsStr, access: u32) -> io::Result<fs::File> {
    create_relative(
        parent,
        name,
        access,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        FILE_OPEN,
        FILE_OPEN_REPARSE_POINT,
    )
}

/// Open the entry `name` itself in `parent` for a forward rename: `DELETE`
/// access, and shared for deletion as well, as `MoveFileExW` opens its source
/// (#1451's sharing decision). Another process holding the entry open for
/// deletion, sharing deletion, does not make the move fail; the guarded
/// inverse ([`rename`]) keeps refusing it.
pub(super) fn open_for_rename(parent: &fs::File, name: &OsStr) -> io::Result<fs::File> {
    create_relative(
        parent,
        name,
        FILE_READ_ATTRIBUTES | DELETE,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        FILE_OPEN,
        FILE_OPEN_REPARSE_POINT,
    )
}

/// Whether an entry of any kind is at `name` in `parent`, looked at without
/// following it (a dangling link is an entry). Attribute-only access takes
/// part in no sharing check.
pub(super) fn entry_exists(parent: &fs::File, name: &OsStr) -> io::Result<bool> {
    match create_relative(
        parent,
        name,
        FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        FILE_OPEN,
        FILE_OPEN_REPARSE_POINT,
    ) {
        Ok(_) => Ok(true),
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(false),
        Err(error) => Err(error),
    }
}

/// Make the folder `name` in `parent` and return it open as a walk handle.
/// `FILE_CREATE` makes an entry already there `AlreadyExists`, never opened.
/// `FILE_OPEN_REPARSE_POINT` is load-bearing: without it, an existing
/// reparse point at `name` (a junction or a symbolic link) would make the
/// Object Manager reparse to the target and create a directory THERE, outside
/// the vault; with it, the reparse point is the existing object the collision
/// names, and the walk refuses it. A fresh name has no reparse data, so the
/// flag is a no-op for the directory actually created.
pub(super) fn create_folder_in(parent: &fs::File, name: &OsStr) -> io::Result<fs::File> {
    create_relative(
        parent,
        name,
        FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        FILE_CREATE,
        FILE_DIRECTORY_FILE | FILE_OPEN_REPARSE_POINT,
    )
}

/// What the walk found at a name, by the entry's own attributes. Every entry
/// is opened with `FILE_OPEN_REPARSE_POINT`, so this never describes a
/// link's target.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum Found {
    /// A folder that is not a reparse point.
    Folder,
    /// A folder that is a reparse point whose tag is not a name surrogate:
    /// it redirects nothing (a Cloud Files placeholder, such as a OneDrive
    /// Files On-Demand folder).
    ReparseFolder,
    /// A junction: a mount-point reparse point.
    Junction,
    /// A symbolic link, to a folder or to a file.
    SymbolicLink,
    /// Any other name surrogate.
    Link,
    /// Anything else where a folder should be, such as a file.
    NotFolder,
}

/// The entry `file` is open on. The reparse tag is asked for only when the
/// attributes show a reparse point, as `std::fs::File::metadata` does, so a
/// volume without reparse points (FAT, exFAT) is never asked for one.
pub(super) fn found(file: &fs::File) -> io::Result<Found> {
    let attributes = file.metadata()?.file_attributes();
    let folder = attributes & FILE_ATTRIBUTE_DIRECTORY != 0;
    if attributes & FILE_ATTRIBUTE_REPARSE_POINT == 0 {
        return Ok(if folder {
            Found::Folder
        } else {
            Found::NotFolder
        });
    }
    let mut info: FILE_ATTRIBUTE_TAG_INFO = unsafe { std::mem::zeroed() };
    // SAFETY: the buffer matches FileAttributeTagInfo and the handle is live.
    if unsafe {
        GetFileInformationByHandleEx(
            file.as_raw_handle(),
            FileAttributeTagInfo,
            (&mut info as *mut FILE_ATTRIBUTE_TAG_INFO).cast(),
            std::mem::size_of::<FILE_ATTRIBUTE_TAG_INFO>() as u32,
        )
    } == 0
    {
        return Err(io::Error::last_os_error());
    }
    if info.ReparseTag & REPARSE_TAG_NAME_SURROGATE != 0 {
        return Ok(match info.ReparseTag {
            IO_REPARSE_TAG_MOUNT_POINT => Found::Junction,
            IO_REPARSE_TAG_SYMLINK | IO_REPARSE_TAG_LX_SYMLINK => Found::SymbolicLink,
            _ => Found::Link,
        });
    }
    Ok(if folder {
        Found::ReparseFolder
    } else {
        Found::NotFolder
    })
}

/// Which folders on a path the walk lets through.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(super) enum Folders {
    /// A folder that redirects nothing: a plain one, or a reparse point
    /// whose tag is not a name surrogate ([`Found::ReparseFolder`]). Forward
    /// moves, saves and creations (#1451).
    NotLinks,
    /// A folder that is not a reparse point of any kind: the Trash handoff,
    /// whose rule is #1450's.
    PlainOnly,
}

impl Folders {
    fn admit(self, found: Found) -> bool {
        match found {
            Found::Folder => true,
            Found::ReparseFolder => self == Self::NotLinks,
            _ => false,
        }
    }
}

/// The typed refusal for `found` at the vault-relative `prefix`, in
/// `pinned_root`'s words (#1449, #1452), or `Ok` when `folders` lets it
/// through. The reason names the folder and ends with what `mutation` did
/// not do; core renders it for both hosts.
pub(super) fn check(
    found: Found,
    folders: Folders,
    prefix: &Path,
    original: &str,
    mutation: Mutation,
) -> Result<(), VaultError> {
    if folders.admit(found) {
        return Ok(());
    }
    let folder = prefix.to_string_lossy().replace('\\', "/");
    let nothing = mutation.nothing_done();
    let reason = match found {
        Found::Junction => {
            format!("\"{folder}\" is a junction, not a folder in this vault, {nothing}")
        }
        Found::SymbolicLink => {
            format!("\"{folder}\" is a symbolic link, not a folder in this vault, {nothing}")
        }
        Found::Link => format!("\"{folder}\" is a link, not a folder in this vault, {nothing}"),
        Found::ReparseFolder => {
            format!("\"{folder}\" is a reparse point, not a plain folder, {nothing}")
        }
        Found::Folder | Found::NotFolder => format!("\"{folder}\" is not a folder, {nothing}"),
    };
    Err(VaultError::InvalidPath {
        path: original.to_string(),
        reason,
    })
}

fn invalid(original: &str, reason: &str) -> VaultError {
    VaultError::InvalidPath {
        path: original.to_string(),
        reason: reason.to_string(),
    }
}

/// The vault root, held open as one directory handle for a forward move, a
/// save, a creation or the Trash handoff. Everything beneath it is opened
/// relative to it without following a reparse point.
#[derive(Debug)]
pub(super) struct PinnedRoot {
    dir: fs::File,
}

impl PinnedRoot {
    /// Open the vault root. Its own reparse point is followed, so a vault
    /// whose root is a junction or a symbolic link keeps working: the handle
    /// is the root.
    pub(super) fn open(root: &Path) -> Result<Self, VaultError> {
        let dir = fs::OpenOptions::new()
            .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
            .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
            .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
            .open(root)
            .map_err(VaultError::Io)?;
        Ok(Self { dir })
    }

    /// Adopt the root a filesystem session holds (plan 44 WB-28, C2): the
    /// walk starts at a duplicate of the held handle, never at a path, so
    /// a spelling retargeted while the session lives cannot hand a move,
    /// a save, a creation or the Trash handoff another vault. The twin of
    /// the Unix `PinnedRoot::adopt`.
    pub(super) fn adopt(held: &fs::File) -> Result<Self, VaultError> {
        Ok(Self {
            dir: held.try_clone().map_err(VaultError::Io)?,
        })
    }

    /// The root handle itself: the Trash handoff takes the vault root's
    /// current path from it (#1450).
    pub(super) fn handle(&self) -> &fs::File {
        &self.dir
    }

    /// Pin the folder at the vault-relative `parent`, one component at a time
    /// relative to the folder before it. A missing folder is
    /// `VaultError::Io(NotFound)` unless `create_missing`, which makes it in
    /// its pinned parent. A folder `folders` does not let through is the
    /// typed refusal, in `mutation`'s words, for the caller's `original`.
    pub(super) fn folder(
        &self,
        parent: &Path,
        original: &str,
        create_missing: bool,
        mutation: Mutation,
        folders: Folders,
    ) -> Result<PinnedFolder, VaultError> {
        let mut pinned = PinnedFolder {
            root: self.dir.try_clone().map_err(VaultError::Io)?,
            chain: Vec::new(),
            original: original.to_string(),
            mutation,
            folders,
        };
        let mut prefix = PathBuf::new();
        for component in parent.components() {
            let Component::Normal(name) = component else {
                return Err(invalid(original, "the path is not vault-relative"));
            };
            prefix.push(name);
            let access = FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY;
            let next = match open_relative(pinned.handle(), name, access) {
                Ok(next) => next,
                Err(error) if create_missing && error.kind() == io::ErrorKind::NotFound => {
                    match create_folder_in(pinned.handle(), name) {
                        Ok(created) => created,
                        // Made a moment ago by someone else: what is it?
                        Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
                            open_relative(pinned.handle(), name, access)
                                .map_err(|error| pinned.failed(error))?
                        }
                        Err(error) => return Err(pinned.failed(error)),
                    }
                }
                Err(error) => return Err(pinned.failed(error)),
            };
            check(
                found(&next).map_err(VaultError::Io)?,
                folders,
                &prefix,
                original,
                mutation,
            )?;
            pinned.chain.push((prefix.clone(), next));
        }
        Ok(pinned)
    }
}

/// A folder beneath the vault root, pinned by [`PinnedRoot::folder`], with
/// every folder above it: all of them stay open until this value drops.
#[derive(Debug)]
pub(super) struct PinnedFolder {
    /// The root handle, duplicated, for a folder that is the root itself.
    root: fs::File,
    /// Each folder beneath the root, with its vault-relative path.
    chain: Vec<(PathBuf, fs::File)>,
    original: String,
    mutation: Mutation,
    folders: Folders,
}

impl PinnedFolder {
    /// The pinned folder's own handle.
    pub(super) fn handle(&self) -> &fs::File {
        self.chain.last().map_or(&self.root, |(_, dir)| dir)
    }

    /// `error`, from a call made in or beneath this folder, as the typed
    /// refusal when a folder pinned here is no longer one the walk lets
    /// through (an empty folder converted into a junction in place after the
    /// walk), or as itself.
    pub(super) fn failed(&self, error: io::Error) -> VaultError {
        for (prefix, dir) in &self.chain {
            if let Ok(found) = found(dir)
                && let Err(refusal) =
                    check(found, self.folders, prefix, &self.original, self.mutation)
            {
                return refusal;
            }
        }
        VaultError::Io(error)
    }
}

/// `FileRenameInformation` on the open `source`: it becomes `name` in the
/// open folder `parent` (the `RootDirectory`), so neither end is resolved by
/// path. `replace` is `ReplaceIfExists`: a save replaces its target, every
/// other rename refuses an occupied name (`AlreadyExists`).
pub(super) fn rename_into(
    source: &fs::File,
    parent: &fs::File,
    name: &OsStr,
    replace: bool,
) -> io::Result<()> {
    rename_with(
        source,
        parent,
        name,
        FileRenameInformation,
        u32::from(replace),
    )
}

/// `NtSetInformationFile` with a rename `class`: `FileRenameInformation`
/// takes `flags` as its `ReplaceIfExists` byte, `FileRenameInformationEx`
/// as its `Flags` (the union's two readings).
fn rename_with(
    source: &fs::File,
    parent: &fs::File,
    name: &OsStr,
    class: windows_sys::Wdk::Storage::FileSystem::FILE_INFORMATION_CLASS,
    flags: u32,
) -> io::Result<()> {
    let name: Vec<u16> = name.encode_wide().collect();
    if !simple_name(&name) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Expected a simple entry name",
        ));
    }
    let too_long = || io::Error::new(io::ErrorKind::InvalidInput, "Entry name is too long");
    let name_bytes = name.len().checked_mul(2).ok_or_else(too_long)?;
    let name_length = u32::try_from(name_bytes).map_err(|_| too_long())?;
    let name_offset = std::mem::offset_of!(FILE_RENAME_INFORMATION, FileName);
    let length = std::mem::size_of::<FILE_RENAME_INFORMATION>().max(name_offset + name_bytes);
    let info_length = u32::try_from(length).map_err(|_| too_long())?;
    let mut buffer = vec![0u64; length.div_ceil(8)];
    // SAFETY: aligned initialized buffer holds the fixed header and full UTF-16
    // name. Both handles outlive the synchronous call.
    let status = unsafe {
        let info = buffer.as_mut_ptr().cast::<FILE_RENAME_INFORMATION>();
        (*info).Anonymous.Flags = flags;
        (*info).RootDirectory = parent.as_raw_handle();
        (*info).FileNameLength = name_length;
        std::ptr::copy_nonoverlapping(
            name.as_ptr(),
            buffer
                .as_mut_ptr()
                .cast::<u8>()
                .add(name_offset)
                .cast::<u16>(),
            name.len(),
        );
        let mut status_block: IO_STATUS_BLOCK = std::mem::zeroed();
        NtSetInformationFile(
            source.as_raw_handle(),
            &mut status_block,
            info.cast(),
            info_length,
            class,
        )
    };
    if status < 0 {
        return Err(io::Error::from_raw_os_error(
            unsafe { RtlNtStatusToDosError(status) } as i32,
        ));
    }
    Ok(())
}

/// The interior's replacing rename (`fs_root::sys::rename_at`), as
/// `std::fs::rename` makes it (plan 44 WB-28): `FileRenameInformationEx`
/// with POSIX semantics, so a target another handle holds open, sharing
/// deletion, is still replaced. A volume without POSIX rename (FAT) gets
/// the classic replacing rename.
pub(super) fn rename_replacing(
    source: &fs::File,
    parent: &fs::File,
    name: &OsStr,
) -> io::Result<()> {
    use windows_sys::Wdk::Storage::FileSystem::{
        FILE_RENAME_POSIX_SEMANTICS, FILE_RENAME_REPLACE_IF_EXISTS, FileRenameInformationEx,
    };
    match rename_with(
        source,
        parent,
        name,
        FileRenameInformationEx,
        FILE_RENAME_REPLACE_IF_EXISTS | FILE_RENAME_POSIX_SEMANTICS,
    ) {
        // ERROR_INVALID_PARAMETER, ERROR_NOT_SUPPORTED: no POSIX rename here.
        Err(error) if matches!(error.raw_os_error(), Some(87) | Some(50)) => {
            rename_into(source, parent, name, true)
        }
        other => other,
    }
}

struct PinnedEntry {
    file: fs::File,
    _ancestors: Vec<fs::File>,
}

fn refused() -> VaultError {
    VaultError::InvalidArgument {
        message:
            "Cannot undo or redo: the files have changed or their identities cannot be verified"
                .into(),
    }
}

fn directory(file: &fs::File) -> Result<(), VaultError> {
    let metadata = file.metadata()?;
    if !metadata.is_dir() || metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
        return Err(refused());
    }
    Ok(())
}

/// The vault root for an undo or a redo when no session holds one (a bare
/// provider): opened by its path as itself, and refused when it is not a
/// plain folder. A session's provider hands its held root instead (WB-28,
/// C4), which follows the root's own reparse point at the open, so a vault
/// opened through a junction undoes too.
pub(super) fn open_unheld_root(root: &Path) -> Result<fs::File, VaultError> {
    let root = fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT)
        .open(root)?;
    directory(&root)?;
    Ok(root)
}

fn open(root: &fs::File, relative: &Path, access: u32) -> Result<PinnedEntry, VaultError> {
    let mut ancestors = vec![root.try_clone()?];
    let components = relative.components().collect::<Vec<_>>();
    if components.is_empty() {
        return Err(refused());
    }
    for (index, component) in components.iter().enumerate() {
        let Component::Normal(name) = component else {
            return Err(refused());
        };
        let last = index + 1 == components.len();
        let child = open_relative(
            ancestors.last().unwrap(),
            name,
            if last {
                access
            } else {
                FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY
            },
        )?;
        if last {
            if child.metadata()?.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
                return Err(refused());
            }
            return Ok(PinnedEntry {
                file: child,
                _ancestors: ancestors,
            });
        }
        directory(&child)?;
        ancestors.push(child);
    }
    Err(refused())
}

fn identity(file: &fs::File) -> Result<String, VaultError> {
    let mut info: FILE_ID_INFO = unsafe { std::mem::zeroed() };
    // SAFETY: live handle and the exact initialized output structure.
    if unsafe {
        GetFileInformationByHandleEx(
            file.as_raw_handle(),
            FileIdInfo,
            (&mut info as *mut FILE_ID_INFO).cast(),
            std::mem::size_of::<FILE_ID_INFO>() as u32,
        )
    } == 0
    {
        return Err(io::Error::last_os_error().into());
    }
    Ok(format!(
        "{:016x}-{:032x}",
        info.VolumeSerialNumber,
        u128::from_le_bytes(info.FileId.Identifier)
    ))
}

pub(super) fn capture(root: &fs::File, relative: &Path) -> Result<String, VaultError> {
    identity(&open(root, relative, FILE_READ_ATTRIBUTES)?.file)
}

pub(super) fn rename(
    root: &fs::File,
    from: &Path,
    to: &Path,
    expected: &str,
) -> Result<(), VaultError> {
    let source = open(root, from, FILE_READ_ATTRIBUTES | DELETE)?;
    if expected.is_empty() || identity(&source.file)? != expected {
        return Err(refused());
    }
    let destination_parent = to.parent().ok_or_else(refused)?;
    let destination = if destination_parent.as_os_str().is_empty() {
        PinnedEntry {
            file: root.try_clone()?,
            _ancestors: Vec::new(),
        }
    } else {
        open(
            root,
            destination_parent,
            FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY,
        )?
    };
    directory(&destination.file)?;
    rename_opened(
        &source.file,
        &destination.file,
        to.file_name().ok_or_else(refused)?,
    )
}

/// The identity inverse's rename: [`rename_into`] without replacement, with
/// a name that is not one component refused in the inverse's own words.
fn rename_opened(source: &fs::File, parent: &fs::File, name: &OsStr) -> Result<(), VaultError> {
    if !simple_name(&name.encode_wide().collect::<Vec<u16>>()) {
        return Err(refused());
    }
    rename_into(source, parent, name, false).map_err(VaultError::Io)
}

#[cfg(test)]
#[path = "windows_entry_tests.rs"]
mod tests;
