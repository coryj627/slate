// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The handle-relative calls the held Windows interior is made of (plan 44
//! WB-28, 22W; `fs_root::sys` on Windows). The names are the Unix module's;
//! the signatures are Windows' own (`&OsStr` names, [`FileOptions`]).
//!
//! Every name is one component, opened relative to an open directory handle
//! with `NtCreateFile` and `FILE_OPEN_REPARSE_POINT`, so no reparse point
//! below the held root is ever followed: a junction or a symbolic link opens
//! as itself and is refused ([`STOPPED_ON_LINK`]). A folder whose reparse
//! tag is not a name surrogate redirects nothing (a Cloud Files placeholder,
//! such as a OneDrive folder) and is admitted, as the vault walk admits it
//! (`windows_entry::Folders::NotLinks`).
//!
//! The identity of a held folder ([`identity`]) is `FileIdInfo` where the
//! volume answers it. Only a volume whose file system names itself `FAT`,
//! `FAT32` or `exFAT` falls back to `GetFileInformationByHandle`'s weak
//! file index (WD24 amended; design item 6); any other failure refuses.

use std::ffi::{OsStr, OsString};
use std::os::windows::ffi::OsStringExt as _;
use std::os::windows::fs::{MetadataExt as _, OpenOptionsExt as _};
use std::os::windows::io::AsRawHandle as _;
use std::path::Path;
use std::{fs, io};

use windows_sys::Win32::Foundation::{
    ERROR_DIRECTORY, ERROR_INVALID_PARAMETER, ERROR_NO_MORE_FILES,
};
use windows_sys::Win32::Storage::FileSystem::{
    BY_HANDLE_FILE_INFORMATION, DELETE, FILE_APPEND_DATA, FILE_ATTRIBUTE_REPARSE_POINT,
    FILE_FLAG_BACKUP_SEMANTICS, FILE_FULL_DIR_INFO, FILE_GENERIC_READ, FILE_GENERIC_WRITE,
    FILE_ID_INFO, FILE_LIST_DIRECTORY, FILE_READ_ATTRIBUTES, FILE_SHARE_DELETE, FILE_SHARE_READ,
    FILE_SHARE_WRITE, FILE_TRAVERSE, FILE_WRITE_DATA, FileFullDirectoryInfo,
    FileFullDirectoryRestartInfo, FileIdInfo, GetFileInformationByHandle,
    GetFileInformationByHandleEx, GetVolumeInformationByHandleW, SYNCHRONIZE,
};

use super::super::windows_entry::{self, Found};
use super::{FileOptions, VaultRootIdentity};

/// `ERROR_STOPPED_ON_SYMLINK` (681): what an interior open answers when the
/// name is a junction, a symbolic link or another name surrogate. The entry
/// was opened as itself, never followed.
pub(crate) const STOPPED_ON_LINK: i32 = 681;

/// The file systems whose folders answer no `FileIdInfo`, so their vaults
/// open on the weak file index (WD24 amended; WB-28 step 0 recorded both
/// VHDs answering error 87 and naming themselves so).
const WEAK_FILE_SYSTEMS: [&str; 3] = ["FAT", "FAT32", "exFAT"];

/// What the folder walk asks of a held or walked folder: enough to list it
/// and to resolve names relative to it.
const FOLDER_ACCESS: u32 = FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY | FILE_TRAVERSE;

/// `name` as one component, refusing an empty name, `.`, `..`, a separator,
/// a colon (an alternate data stream) or an embedded NUL.
pub(crate) fn component(name: &str) -> io::Result<OsString> {
    if name.is_empty() || name == "." || name == ".." || name.contains(['/', '\\', ':', '\0']) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "an interior name must be one component",
        ));
    }
    Ok(OsString::from(name))
}

/// The folder holding `relative`'s leaf: the held directory itself, or one
/// child folder opened beneath it without following a link.
pub(crate) enum Parent {
    Held,
    Child(fs::File),
}

impl Parent {
    pub(crate) fn get<'a>(&'a self, held: &'a fs::File) -> &'a fs::File {
        match self {
            Self::Held => held,
            Self::Child(child) => child,
        }
    }
}

pub(crate) fn parent_and_leaf(held: &fs::File, relative: &str) -> io::Result<(Parent, OsString)> {
    match relative.split_once('/') {
        None => Ok((Parent::Held, component(relative)?)),
        Some((child, leaf)) => {
            let child = open_dir_at(held, &component(child)?)?;
            Ok((Parent::Child(child), component(leaf)?))
        }
    }
}

/// Refuse what an interior open must never go through: a name surrogate
/// (a junction, a symbolic link) is [`STOPPED_ON_LINK`].
fn refuse_links(file: fs::File, want_folder: bool) -> io::Result<fs::File> {
    match windows_entry::found(&file)? {
        Found::Folder | Found::ReparseFolder if want_folder => Ok(file),
        Found::NotFolder if !want_folder => Ok(file),
        Found::Junction | Found::SymbolicLink | Found::Link => {
            Err(io::Error::from_raw_os_error(STOPPED_ON_LINK))
        }
        // A folder where a file was asked for, or the reverse.
        _ => Err(io::Error::from_raw_os_error(ERROR_DIRECTORY as i32)),
    }
}

/// The folder `name` in `dir`, opened as itself: a junction or a link there
/// is [`STOPPED_ON_LINK`], a file `ERROR_DIRECTORY`.
pub(crate) fn open_dir_at(dir: &fs::File, name: &OsStr) -> io::Result<fs::File> {
    // Not FILE_DIRECTORY_FILE: a link to a file must open as itself so it can
    // be named as a link, not fail as "not a directory".
    let file = windows_entry::create_relative(
        dir,
        name,
        FOLDER_ACCESS,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        windows_sys::Wdk::Storage::FileSystem::FILE_OPEN,
        windows_sys::Wdk::Storage::FileSystem::FILE_OPEN_REPARSE_POINT,
    )?;
    refuse_links(file, true)
}

/// The access, disposition and options an interior file open asks for, the
/// mapping `std::fs::OpenOptions` makes for a path.
fn file_request(options: FileOptions) -> (u32, u32) {
    use windows_sys::Wdk::Storage::FileSystem::{
        FILE_CREATE, FILE_OPEN, FILE_OPEN_IF, FILE_OVERWRITE, FILE_OVERWRITE_IF,
    };
    // std's mapping: append wins over write (a true append, no
    // FILE_WRITE_DATA).
    let mut access = 0;
    if options.read {
        access |= FILE_GENERIC_READ;
    }
    if options.append {
        access |= (FILE_GENERIC_WRITE & !FILE_WRITE_DATA) | FILE_APPEND_DATA;
    } else if options.write {
        access |= FILE_GENERIC_WRITE;
    }
    let disposition = match (options.create_new, options.create, options.truncate) {
        (true, _, _) => FILE_CREATE,
        (false, true, true) => FILE_OVERWRITE_IF,
        (false, true, false) => FILE_OPEN_IF,
        (false, false, true) => FILE_OVERWRITE,
        (false, false, false) => FILE_OPEN,
    };
    (access, disposition)
}

/// The file `name` in `dir`, opened as itself and shared as `std::fs` shares
/// a file (reading, writing and deletion): a link there is
/// [`STOPPED_ON_LINK`], a folder `ERROR_DIRECTORY`.
pub(crate) fn open_at(dir: &fs::File, name: &OsStr, options: FileOptions) -> io::Result<fs::File> {
    use windows_sys::Wdk::Storage::FileSystem::{FILE_NON_DIRECTORY_FILE, FILE_OPEN_REPARSE_POINT};
    let (access, disposition) = file_request(options);
    let file = windows_entry::create_relative(
        dir,
        name,
        access | FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        disposition,
        FILE_NON_DIRECTORY_FILE | FILE_OPEN_REPARSE_POINT,
    )?;
    refuse_links(file, false)
}

/// Make the folder `name` in `dir` if it is missing; one that is there
/// already is fine (the open beneath it refuses a link or a file).
pub(crate) fn ensure_dir_at(dir: &fs::File, name: &OsStr) -> io::Result<()> {
    match windows_entry::create_folder_in(dir, name) {
        Ok(_) => Ok(()),
        Err(error) if error.kind() == io::ErrorKind::AlreadyExists => Ok(()),
        Err(error) => Err(error),
    }
}

/// `.slate` beneath the held root, made there when it is missing, never
/// through a link, and held for the session: shared for reading and writing
/// but not for deletion, so it can be neither renamed nor removed while the
/// session lives.
pub(crate) fn open_or_make_slate(root: &fs::File) -> io::Result<fs::File> {
    let name = OsStr::new(".slate");
    match open_dir_at(root, name) {
        Err(error) if error.kind() == io::ErrorKind::NotFound => {
            ensure_dir_at(root, name)?;
            open_dir_at(root, name)
        }
        other => other,
    }
}

/// Rename `from` in `from_dir` to `to` in `to_dir`, replacing what is at
/// `to` as `renameat` does: the interior's atomic replace of a file it
/// wrote beside its target.
pub(crate) fn rename_at(
    from_dir: &fs::File,
    from: &OsStr,
    to_dir: &fs::File,
    to: &OsStr,
) -> io::Result<()> {
    let source = windows_entry::open_for_rename(from_dir, from)?;
    windows_entry::rename_replacing(&source, to_dir, to)
}

/// Remove the file `name` in `dir`, as itself (a link is removed, never its
/// target).
pub(crate) fn unlink_at(dir: &fs::File, name: &OsStr) -> io::Result<()> {
    use windows_sys::Wdk::Storage::FileSystem::{
        FILE_DELETE_ON_CLOSE, FILE_NON_DIRECTORY_FILE, FILE_OPEN, FILE_OPEN_REPARSE_POINT,
    };
    let file = windows_entry::create_relative(
        dir,
        name,
        DELETE | FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        FILE_OPEN,
        FILE_NON_DIRECTORY_FILE | FILE_OPEN_REPARSE_POINT | FILE_DELETE_ON_CLOSE,
    )?;
    drop(file);
    Ok(())
}

/// The metadata of the entry `name` in `dir`, looked at as itself.
pub(crate) fn stat_at(dir: &fs::File, name: &OsStr) -> io::Result<fs::Metadata> {
    use windows_sys::Wdk::Storage::FileSystem::{FILE_OPEN, FILE_OPEN_REPARSE_POINT};
    windows_entry::create_relative(
        dir,
        name,
        FILE_READ_ATTRIBUTES,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        FILE_OPEN,
        FILE_OPEN_REPARSE_POINT,
    )?
    .metadata()
}

/// Whether the metadata is of an entry that is a reparse point.
pub(crate) fn is_reparse_point(metadata: &fs::Metadata) -> bool {
    metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0
}

/// A new staging file `name` in `dir` (the held `.slate\tmp`):
/// `FILE_CREATE`, so a name already there is `AlreadyExists` and is never
/// opened; opened for writing, attributes and deletion, so the save can
/// write it, publish it by handle and remove it through the same handle;
/// shared as `tempfile` shares its files.
pub(crate) fn create_staged(dir: &fs::File, name: &OsStr) -> io::Result<fs::File> {
    use windows_sys::Wdk::Storage::FileSystem::{
        FILE_CREATE, FILE_NON_DIRECTORY_FILE, FILE_OPEN_REPARSE_POINT,
    };
    use windows_sys::Win32::Storage::FileSystem::FILE_WRITE_ATTRIBUTES;
    windows_entry::create_relative(
        dir,
        name,
        FILE_GENERIC_READ | FILE_GENERIC_WRITE | FILE_WRITE_ATTRIBUTES | DELETE,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        FILE_CREATE,
        FILE_NON_DIRECTORY_FILE | FILE_OPEN_REPARSE_POINT,
    )
}

/// Mark the file `file` is open on (with `DELETE` access) for deletion,
/// by its handle: the entry goes when the last handle closes, and no
/// other entry that took its name is touched.
pub(crate) fn delete_by_handle(file: &fs::File) -> io::Result<()> {
    use windows_sys::Win32::Storage::FileSystem::{
        FILE_DISPOSITION_INFO, FileDispositionInfo, SetFileInformationByHandle,
    };
    let info = FILE_DISPOSITION_INFO { DeleteFile: true };
    // SAFETY: a live handle and an initialized FILE_DISPOSITION_INFO of the
    // size passed.
    let ok = unsafe {
        SetFileInformationByHandle(
            file.as_raw_handle(),
            FileDispositionInfo,
            (&info as *const FILE_DISPOSITION_INFO).cast(),
            std::mem::size_of::<FILE_DISPOSITION_INFO>() as u32,
        )
    };
    if ok == 0 {
        return Err(io::Error::last_os_error());
    }
    Ok(())
}

/// `relative`'s components, refusing anything but plain names.
fn components(relative: &Path) -> io::Result<Vec<&OsStr>> {
    use std::path::Component;
    let names: Vec<&OsStr> = relative
        .components()
        .map(|component| match component {
            Component::Normal(name) => Ok(name),
            _ => Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "the path is not vault-relative",
            )),
        })
        .collect::<io::Result<_>>()?;
    if names.is_empty() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "the vault root itself is the held handle",
        ));
    }
    Ok(names)
}

/// The entry at `relative` beneath `root`, opened to be read: one
/// `NtCreateFile` resolves the whole relative name as a path open would,
/// links among the folders followed. With `follow_leaf` false the leaf
/// opens as itself. The mutation walks, not these reads, refuse a link
/// among the folders, in their own words.
pub(crate) fn open_beneath(
    root: &fs::File,
    relative: &Path,
    follow_leaf: bool,
    access: u32,
) -> io::Result<fs::File> {
    use windows_sys::Wdk::Storage::FileSystem::FILE_OPEN_REPARSE_POINT;
    let options = if follow_leaf {
        0
    } else {
        FILE_OPEN_REPARSE_POINT
    };
    windows_entry::open_beneath_path(root, &components(relative)?, access, options, true)
}

/// The entry at `relative` beneath `root`, opened only to look at it:
/// attribute access alone and no synchronous I/O. `std::fs::metadata` asks
/// as much (`CreateFileW` adds `FILE_READ_ATTRIBUTES` and `SYNCHRONIZE` to
/// every open) and `SYNCHRONIZE` too; leaving that out means a leaf that
/// denies it, or one the reader may not read, still answers the look
/// (NTFS grants attribute reads to whoever may list the parent).
pub(crate) fn open_metadata_beneath(
    root: &fs::File,
    relative: &Path,
    follow_leaf: bool,
) -> io::Result<fs::File> {
    use windows_sys::Wdk::Storage::FileSystem::FILE_OPEN_REPARSE_POINT;
    let options = if follow_leaf {
        0
    } else {
        FILE_OPEN_REPARSE_POINT
    };
    windows_entry::open_beneath_path(
        root,
        &components(relative)?,
        FILE_READ_ATTRIBUTES,
        options,
        false,
    )
}

/// What a listing reports an entry as, the way `std::fs::FileType` does: a
/// name surrogate (a junction, a symbolic link) is a link whether it
/// stands for a folder or a file.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) enum ListedKind {
    File,
    Folder,
    Link,
}

/// The names in `dir` with their kinds, `.` and `..` excluded. `dir` is a
/// handle of its own, opened with list access.
pub(crate) fn read_dir_entries(dir: &fs::File) -> io::Result<Vec<(OsString, ListedKind)>> {
    const NAME_SURROGATE: u32 = 0x2000_0000;
    let mut buffer = vec![0u64; 8 * 1024];
    let mut entries = Vec::new();
    let mut class = FileFullDirectoryRestartInfo;
    loop {
        let bytes = u32::try_from(buffer.len() * 8).unwrap_or(u32::MAX);
        // SAFETY: a live handle and a writable, aligned buffer of `bytes`.
        let ok = unsafe {
            GetFileInformationByHandleEx(
                dir.as_raw_handle(),
                class,
                buffer.as_mut_ptr().cast(),
                bytes,
            )
        };
        if ok == 0 {
            let error = io::Error::last_os_error();
            if error.raw_os_error() == Some(ERROR_NO_MORE_FILES as i32) {
                return Ok(entries);
            }
            return Err(error);
        }
        class = FileFullDirectoryInfo;
        let base = buffer.as_ptr().cast::<u8>();
        let mut offset = 0usize;
        loop {
            // SAFETY: a record the call wrote, within the buffer.
            let entry = unsafe { &*base.add(offset).cast::<FILE_FULL_DIR_INFO>() };
            let length = entry.FileNameLength as usize / 2;
            // SAFETY: the record's name runs FileNameLength bytes from its
            // FileName field, inside the buffer; the pointer comes from the
            // buffer, not from the one-element field.
            let name = unsafe {
                std::slice::from_raw_parts(
                    base.add(offset + std::mem::offset_of!(FILE_FULL_DIR_INFO, FileName))
                        .cast::<u16>(),
                    length,
                )
            };
            if name != [46] && name != [46, 46] {
                // A reparse point's tag is in EaSize (FILE_FULL_DIR_INFO).
                let kind = if entry.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT != 0
                    && entry.EaSize & NAME_SURROGATE != 0
                {
                    ListedKind::Link
                } else if entry.FileAttributes
                    & windows_sys::Win32::Storage::FileSystem::FILE_ATTRIBUTE_DIRECTORY
                    != 0
                {
                    ListedKind::Folder
                } else {
                    ListedKind::File
                };
                entries.push((OsString::from_wide(name), kind));
            }
            if entry.NextEntryOffset == 0 {
                break;
            }
            offset += entry.NextEntryOffset as usize;
        }
    }
}

/// The file system's id of the entry `file` is open on, for telling two
/// entries apart at one moment: `FileIdInfo`, else the file index (on a
/// volume that answers only that, two entries that exist at once still
/// have different indexes).
pub(crate) fn entry_id(file: &fs::File) -> io::Result<VaultRootIdentity> {
    file_id_info(file).or_else(|_| file_index(file))
}

/// Whether `relative` (vault-relative, `/`-separated) names a folder
/// beneath `root`, resolved as a path open resolves it (links followed):
/// only the starting point is anchored (the open's templates detection).
pub(crate) fn is_dir_beneath(root: &fs::File, relative: &str) -> bool {
    use windows_sys::Wdk::Storage::FileSystem::FILE_DIRECTORY_FILE;
    let Ok(names) = relative
        .split('/')
        .map(component)
        .collect::<io::Result<Vec<_>>>()
    else {
        return false;
    };
    let names: Vec<&OsStr> = names.iter().map(OsString::as_os_str).collect();
    !names.is_empty()
        && windows_entry::open_beneath_path(
            root,
            &names,
            FILE_READ_ATTRIBUTES,
            FILE_DIRECTORY_FILE,
            false,
        )
        .is_ok()
}

/// Windows writes a folder's entries with the change that made them; there
/// is nothing to flush.
pub(crate) fn sync_dir(_dir: &fs::File) -> io::Result<()> {
    Ok(())
}

/// The names in `dir`, `.` and `..` excluded. `dir` is a handle of its own
/// (the listing's position is the handle's), opened with list access.
pub(crate) fn read_dir_names(dir: &fs::File) -> io::Result<Vec<OsString>> {
    Ok(read_dir_entries(dir)?
        .into_iter()
        .map(|(name, _)| name)
        .collect())
}

/// Open the vault root at `root` and hold it for the session. Its own
/// reparse point is followed, so a vault whose root is a junction or a
/// symbolic link keeps working (the handle is the root, as
/// `windows_entry::PinnedRoot::open` holds it). It is shared for reading and
/// writing but not for deletion: neither it nor any folder above it can be
/// renamed or removed while the session lives (design item 5).
pub(crate) fn open_root(root: &Path) -> io::Result<fs::File> {
    fs::OpenOptions::new()
        .access_mode(FOLDER_ACCESS | SYNCHRONIZE)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
        .open(super::super::windows_extended_path(root))
}

/// `FileIdInfo`: the 64-bit volume serial and the 128-bit file id, the
/// file system's own identity (`persistent`).
fn file_id_info(file: &fs::File) -> io::Result<VaultRootIdentity> {
    // SAFETY: zeroed storage of the type the call fills.
    let mut info: FILE_ID_INFO = unsafe { std::mem::zeroed() };
    // SAFETY: a live handle and a buffer of exactly FILE_ID_INFO's size.
    let ok = unsafe {
        GetFileInformationByHandleEx(
            file.as_raw_handle(),
            FileIdInfo,
            (&mut info as *mut FILE_ID_INFO).cast(),
            std::mem::size_of::<FILE_ID_INFO>() as u32,
        )
    };
    if ok == 0 {
        return Err(io::Error::last_os_error());
    }
    let id = info.FileId.Identifier;
    Ok(VaultRootIdentity {
        volume: info.VolumeSerialNumber,
        id_high: u64::from_le_bytes(id[8..].try_into().expect("eight bytes")),
        id_low: u64::from_le_bytes(id[..8].try_into().expect("eight bytes")),
        persistent: true,
    })
}

/// The name the volume's file system gives itself
/// (`GetVolumeInformationByHandleW`), the name the fallback gate reads.
fn file_system_name(file: &fs::File) -> io::Result<String> {
    let mut name = [0u16; 261];
    // SAFETY: a live handle; every optional out-pointer is null and the name
    // buffer holds its stated length.
    let ok = unsafe {
        GetVolumeInformationByHandleW(
            file.as_raw_handle(),
            std::ptr::null_mut(),
            0,
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            name.as_mut_ptr(),
            name.len() as u32,
        )
    };
    if ok == 0 {
        return Err(io::Error::last_os_error());
    }
    let length = name
        .iter()
        .position(|unit| *unit == 0)
        .unwrap_or(name.len());
    Ok(String::from_utf16_lossy(&name[..length]))
}

/// `GetFileInformationByHandle`: the 32-bit volume serial and the 64-bit
/// file index (the vault watcher's pair), weak (`persistent: false`).
fn file_index(file: &fs::File) -> io::Result<VaultRootIdentity> {
    // SAFETY: zeroed storage of the type the call fills.
    let mut info: BY_HANDLE_FILE_INFORMATION = unsafe { std::mem::zeroed() };
    // SAFETY: a live handle and writable storage of the right type.
    if unsafe { GetFileInformationByHandle(file.as_raw_handle(), &mut info) } == 0 {
        return Err(io::Error::last_os_error());
    }
    Ok(VaultRootIdentity {
        volume: u64::from(info.dwVolumeSerialNumber),
        id_high: 0,
        id_low: (u64::from(info.nFileIndexHigh) << 32) | u64::from(info.nFileIndexLow),
        persistent: false,
    })
}

/// The identity decision (design item 6), pure so its table is a fact:
/// `FileIdInfo` when it answers; otherwise the file index, but only on a
/// volume whose file system names itself `FAT`, `FAT32` or `exFAT` (ASCII,
/// any case). On any other name, or a name that cannot be read, the
/// `FileIdInfo` error stands and the index is never read: a refusal, not a
/// retry.
pub(crate) fn identity_from_reads(
    file_id_info: io::Result<VaultRootIdentity>,
    file_system_name: impl FnOnce() -> io::Result<String>,
    file_index: impl FnOnce() -> io::Result<VaultRootIdentity>,
) -> io::Result<VaultRootIdentity> {
    let error = match file_id_info {
        Ok(identity) => return Ok(identity),
        Err(error) => error,
    };
    match file_system_name() {
        Ok(name)
            if WEAK_FILE_SYSTEMS
                .iter()
                .any(|weak| name.eq_ignore_ascii_case(weak)) =>
        {
            log::debug!(
                "FileIdInfo did not answer on a {name} volume ({error}); reading its file index"
            );
            file_index()
        }
        _ => Err(error),
    }
}

/// The four identity seams any build can trip (design item 6), each a path
/// substring: a held folder beneath a root whose opened path contains the
/// value reads its identity through the profile. Each fakes only what
/// [`identity`] reads; nothing else sees a faked name.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub(crate) struct Seams {
    /// A FAT32 volume: the name reads `FAT32` and `FileIdInfo` fails with
    /// `ERROR_INVALID_PARAMETER`, so the fallback answers.
    file_id_info_unsupported: bool,
    /// That profile with the file-index read failing too: the open refuses.
    file_id_unavailable: bool,
    /// Only `FileIdInfo` fails, under the volume's real name (a transient
    /// NTFS failure): the open refuses without reading the index.
    file_id_info_fails: bool,
    /// The file-index read returns the real serial with a fixed index, so
    /// every held folder under the root collides.
    file_index_collides: bool,
}

/// The index every folder reads under `SLATE_TEST_ROOT_FILE_INDEX_COLLIDES`.
pub(crate) const COLLIDING_INDEX: u64 = 0x5EED_0000_5EED_0001;

impl Seams {
    /// The profile for a root opened as `root`.
    pub(crate) fn for_root(root: &Path) -> Self {
        let root = root.to_string_lossy();
        let set = |name: &str| {
            std::env::var_os(name).is_some_and(|trigger| {
                !trigger.is_empty() && root.contains(trigger.to_string_lossy().as_ref())
            })
        };
        Self {
            file_id_info_unsupported: set("SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED"),
            file_id_unavailable: set("SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE"),
            file_id_info_fails: set("SLATE_TEST_ROOT_FILE_ID_INFO_FAILS"),
            file_index_collides: set("SLATE_TEST_ROOT_FILE_INDEX_COLLIDES"),
        }
    }
}

/// The held folder's identity, through `seams` (the real reads when none is
/// set).
pub(crate) fn identity(file: &fs::File, seams: Seams) -> io::Result<VaultRootIdentity> {
    let unsupported = || io::Error::from_raw_os_error(ERROR_INVALID_PARAMETER as i32);
    let fat = seams.file_id_info_unsupported || seams.file_id_unavailable;
    let info = if fat || seams.file_id_info_fails {
        Err(unsupported())
    } else {
        file_id_info(file)
    };
    identity_from_reads(
        info,
        || {
            if fat {
                Ok("FAT32".to_string())
            } else {
                file_system_name(file)
            }
        },
        || {
            if seams.file_id_unavailable {
                return Err(unsupported());
            }
            let mut index = file_index(file)?;
            if seams.file_index_collides {
                index.id_low = COLLIDING_INDEX;
            }
            Ok(index)
        },
    )
}

/// The weak file-index pair of `file`, whatever its volume answers: the
/// read the vault watcher arms with, so the session can compare the watch
/// with what it holds in the watch's own terms.
pub(crate) fn watch_pair(file: &fs::File) -> io::Result<(u64, u64)> {
    let index = file_index(file)?;
    Ok((index.volume, index.id_low))
}
