// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The handoff from a validated Trash inventory to the system Trash (#1450).
//!
//! The inventory descends from the vault root one directory at a time
//! without following links (`trash_snapshot`). Every system Trash API takes
//! a path (or a URL made from one) and resolves it again, following links,
//! in code Slate does not control: trash-rs canonicalizes the parent, and
//! Finder, `NSFileManager` and the Windows Shell resolve what they are
//! handed. Until
//! #1450 the provider handed over the lexical vault path, so a folder
//! replaced by a link after the last check redirected the Trash to an object
//! outside the vault (#1430's probes P12.6 and P16).
//!
//! The path handed over is now derived from the pinned parent: the
//! no-follow walk's parent descriptor (handle, on Windows) reports its own
//! current path, which must still name that descriptor and lie beneath the
//! vault root's current path, taken the same way. A folder replaced by a link
//! refuses at the walk; a pinned folder moved out of the vault refuses at the
//! derivation. Both refusals are [`moved`], typed as the checked-Trash
//! refusal the hosts already render, and happen before any filesystem
//! mutation.
//!
//! What remains is the race between this derivation and the Trash call's own
//! resolution of the path. Only a handle-bound Trash API could close it, and
//! no platform has one (#1430 T1). On macOS the path becomes a file reference
//! URL before `trashItemAtURL:`, so a substitution after that point no longer
//! redirects the call (P12.2); P13 measured the race left inside the call.

use std::fmt;
use std::path::Path;
#[cfg(any(unix, windows))]
use std::path::PathBuf;
#[cfg(unix)]
use std::{ffi::CStr, fs, io};

use crate::VaultError;

/// The one step of a Trash Slate does not control: the platform's own Trash
/// call, handed the absolute path derived from the pinned parent an instant
/// earlier. Production uses [`PlatformTrash`]; tests use `ScratchTrash`.
pub(crate) trait SystemTrash: fmt::Debug + Send + Sync {
    fn trash(&self, path: &Path) -> Result<(), VaultError>;
}

/// The platform's system Trash: `NSFileManager` on macOS (a file reference
/// URL, no Finder script, no Automation grant), trash-rs elsewhere (the
/// Recycle Bin on Windows, the freedesktop Trash on Linux).
#[derive(Debug, Default)]
pub(crate) struct PlatformTrash;

impl SystemTrash for PlatformTrash {
    fn trash(&self, path: &Path) -> Result<(), VaultError> {
        #[cfg(target_os = "macos")]
        {
            macos::trash_item(path)
        }
        #[cfg(not(target_os = "macos"))]
        {
            trash::delete(path).map_err(|error| VaultError::Trash {
                message: error.to_string(),
            })
        }
    }
}

/// The refusal when the item's checked location no longer holds: a folder on
/// its path is now a link, a junction or not a folder, or a pinned folder now
/// sits elsewhere (out of the vault, or no longer at the path it reports).
/// Nothing was mutated; the host speaks the message as it speaks every
/// checked-Trash refusal.
fn moved() -> VaultError {
    VaultError::TrashConfirmationChanged {
        message: "A folder in this item's path was replaced or moved, so it was not moved to the \
                  Trash. Request deletion again"
            .into(),
    }
}

/// The refusal when this platform cannot report a pinned folder's current
/// path (Linux without `/proc`, or an unsupported Unix): the handoff could
/// not be checked, so nothing is handed to the Trash.
#[cfg(unix)]
fn unverifiable() -> VaultError {
    VaultError::TrashConfirmationChanged {
        message: "Slate could not confirm where this item is, so it was not moved to the Trash"
            .into(),
    }
}

/// Map a failure of the no-follow walk to the parent: an ancestor that is now
/// a link (`ELOOP`, or `ENOTDIR` where the walk also demands a directory) or
/// no longer a folder (`ENOTDIR`) is the substitution this handoff refuses.
/// Anything else, such as a missing ancestor, keeps its own error.
#[cfg(unix)]
pub(super) fn walk_refusal(error: VaultError) -> VaultError {
    match &error {
        VaultError::Io(io)
            if matches!(io.raw_os_error(), Some(libc::ELOOP) | Some(libc::ENOTDIR)) =>
        {
            moved()
        }
        _ => error,
    }
}

/// The path to hand the system Trash for `leaf` inside the pinned `parent`:
/// the parent's current path joined with the leaf, refused unless that path
/// still names the pinned parent and lies beneath the vault root's current
/// path (both taken from their descriptors, so a symlinked vault root, which
/// the root descriptor followed when it was opened, keeps working).
#[cfg(unix)]
pub(super) fn pinned_path(
    root: &fs::File,
    parent: &fs::File,
    leaf: &CStr,
) -> Result<PathBuf, VaultError> {
    use std::os::unix::ffi::OsStrExt as _;

    let root_now = current_path(root).map_err(|_| unverifiable())?;
    let parent_now = current_path(parent).map_err(|_| unverifiable())?;
    // A reported path that no longer names its descriptor (a folder removed
    // or moved since, or Linux's "(deleted)" suffix) is not a location.
    if !names(&root_now, root) || !names(&parent_now, parent) {
        return Err(moved());
    }
    if !parent_now.starts_with(&root_now) {
        return Err(moved());
    }
    Ok(parent_now.join(std::ffi::OsStr::from_bytes(leaf.to_bytes())))
}

/// Does `path` resolve to the object `descriptor` is open on right now?
#[cfg(unix)]
fn names(path: &Path, descriptor: &fs::File) -> bool {
    use std::os::unix::fs::MetadataExt as _;
    match (fs::metadata(path), descriptor.metadata()) {
        (Ok(at_path), Ok(pinned)) => at_path.dev() == pinned.dev() && at_path.ino() == pinned.ino(),
        _ => false,
    }
}

/// The current absolute path of the directory `descriptor` is open on, from
/// the kernel (`F_GETPATH`), not from the spelling that opened it.
#[cfg(target_vendor = "apple")]
fn current_path(descriptor: &fs::File) -> io::Result<PathBuf> {
    use std::os::fd::AsRawFd as _;
    use std::os::unix::ffi::OsStringExt as _;

    let mut buffer = vec![0u8; libc::PATH_MAX as usize];
    // SAFETY: the descriptor stays open for the call, and F_GETPATH writes a
    // NUL-terminated path of at most MAXPATHLEN (PATH_MAX) bytes.
    if unsafe { libc::fcntl(descriptor.as_raw_fd(), libc::F_GETPATH, buffer.as_mut_ptr()) } == -1 {
        return Err(io::Error::last_os_error());
    }
    let length = buffer
        .iter()
        .position(|byte| *byte == 0)
        .ok_or_else(|| io::Error::other("F_GETPATH returned an unterminated path"))?;
    buffer.truncate(length);
    Ok(PathBuf::from(std::ffi::OsString::from_vec(buffer)))
}

/// Linux reports a descriptor's path through `/proc/self/fd`; without
/// `/proc` the handoff refuses rather than trusting the spelling.
#[cfg(any(target_os = "linux", target_os = "android"))]
fn current_path(descriptor: &fs::File) -> io::Result<PathBuf> {
    use std::os::fd::AsRawFd as _;
    let path = fs::read_link(format!("/proc/self/fd/{}", descriptor.as_raw_fd()))?;
    if !path.is_absolute() {
        return Err(io::Error::other("descriptor path is not absolute"));
    }
    Ok(path)
}

#[cfg(all(
    unix,
    not(target_vendor = "apple"),
    not(any(target_os = "linux", target_os = "android"))
))]
fn current_path(_descriptor: &fs::File) -> io::Result<PathBuf> {
    Err(io::Error::new(
        io::ErrorKind::Unsupported,
        "this platform cannot report a descriptor's path",
    ))
}

/// Windows: the same derivation through handles. The configured root is
/// followed (a linked vault keeps working); every folder beneath it is opened
/// relative to its parent's handle without following a reparse point, the way
/// the Trash inventory walks (`trash_snapshot::windows`). The handed path is
/// the parent handle's final path joined with the leaf, refused unless it lies
/// beneath the root handle's final path.
#[cfg(windows)]
pub(super) fn pinned_path(root: &Path, vault_relative: &Path) -> Result<PathBuf, VaultError> {
    use std::os::windows::fs::{MetadataExt as _, OpenOptionsExt as _};
    use std::path::Component;
    use windows_sys::Win32::Storage::FileSystem::{
        FILE_ATTRIBUTE_REPARSE_POINT, FILE_FLAG_BACKUP_SEMANTICS, FILE_LIST_DIRECTORY,
        FILE_READ_ATTRIBUTES, FILE_SHARE_READ, FILE_SHARE_WRITE,
    };

    let leaf = vault_relative.file_name().ok_or_else(moved)?;
    let root_directory = std::fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
        .open(root)?;
    let mut parent: Option<std::fs::File> = None;
    for component in vault_relative
        .parent()
        .unwrap_or_else(|| Path::new(""))
        .components()
    {
        let Component::Normal(name) = component else {
            return Err(moved());
        };
        let child = super::windows_entry::open_relative(
            parent.as_ref().unwrap_or(&root_directory),
            name,
            FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY,
        )?;
        let metadata = child.metadata()?;
        if !metadata.is_dir() || metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
            return Err(moved());
        }
        parent = Some(child);
    }
    let root_now = final_path(&root_directory)?;
    let parent_now = match &parent {
        Some(parent) => final_path(parent)?,
        None => root_now.clone(),
    };
    if !parent_now.starts_with(&root_now) {
        return Err(moved());
    }
    Ok(parent_now.join(leaf))
}

/// `GetFinalPathNameByHandleW` for an open handle: the normalized path of the
/// object the handle is open on, with its drive letter (`\\?\C:\…`).
#[cfg(windows)]
fn final_path(file: &std::fs::File) -> std::io::Result<PathBuf> {
    use std::os::windows::{ffi::OsStringExt as _, io::AsRawHandle as _};
    use windows_sys::Win32::Storage::FileSystem::{
        FILE_NAME_NORMALIZED, GetFinalPathNameByHandleW, VOLUME_NAME_DOS,
    };

    let mut buffer = vec![0u16; 512];
    loop {
        let capacity = u32::try_from(buffer.len()).unwrap_or(u32::MAX);
        // SAFETY: the handle stays open for the call and the buffer holds
        // `capacity` UTF-16 units. The result is the length written without
        // the terminator, the size needed (with it) when too small, or 0.
        let length = unsafe {
            GetFinalPathNameByHandleW(
                file.as_raw_handle(),
                buffer.as_mut_ptr(),
                capacity,
                FILE_NAME_NORMALIZED | VOLUME_NAME_DOS,
            )
        } as usize;
        if length == 0 {
            return Err(std::io::Error::last_os_error());
        }
        if length < buffer.len() {
            buffer.truncate(length);
            return Ok(PathBuf::from(std::ffi::OsString::from_wide(&buffer)));
        }
        buffer.resize(length, 0);
    }
}

#[cfg(target_os = "macos")]
mod macos {
    use std::path::{Path, PathBuf};
    use std::time::{Duration, Instant};
    use std::{fs, io};

    use objc2_foundation::{NSFileManager, NSURL};

    use crate::VaultError;

    /// `NSFileManager trashItemAtURL:resultingItemURL:error:` for the derived
    /// path, as a file reference URL minted from it at once. Unlike the Finder
    /// script trash-rs runs by default, this needs no Automation grant, sends
    /// no Apple event (so a busy Finder cannot stall it for AppleScript's two
    /// minutes, #1448), still writes the volume's Put Back records from a
    /// long-lived process (#1430 P15), and references a symbolic link itself,
    /// never its target. A volume that cannot mint a reference URL keeps the
    /// path URL, which is still the pinned parent's. Calls are serialized
    /// across Slate's processes ([`SerializedTrash`]).
    pub(super) fn trash_item(path: &Path) -> Result<(), VaultError> {
        let _serialized = SerializedTrash::acquire();
        objc2::rc::autoreleasepool(|_| {
            let url = NSURL::from_file_path(path).ok_or_else(|| VaultError::Trash {
                message: "The item's location cannot be expressed as a file URL".into(),
            })?;
            let url = url.fileReferenceURL().unwrap_or(url);
            NSFileManager::defaultManager()
                .trashItemAtURL_resultingItemURL_error(&url, None)
                .map_err(|error| VaultError::Trash {
                    message: error.to_string(),
                })
        })
    }

    /// One Trash call at a time across every Slate process of this user.
    ///
    /// `NSFileManager` picks the item's name in the Trash and then moves it
    /// without excluding another process doing the same. Measured on a
    /// scratch APFS volume: 24 processes trashing same-named items at one
    /// instant all reported success, yet only 11 and 12 of the 24 items
    /// remained in the Trash; the rest were overwritten there and lost. On the
    /// boot volume, concurrent calls from the mac suite's parallel workers
    /// failed instead ("couldn't be moved to the trash because you don't have
    /// permission to access it"). Threads of one process did not race (24 of
    /// 24 landed), Finder serializes its own deletes (12 concurrent Finder
    /// deletes all landed), and under this lock 24 of 24 landed.
    ///
    /// The lock is an exclusive `flock` on a file in the per-user temporary
    /// directory from `confstr`, not `$TMPDIR`, so every process of the user
    /// agrees on it. It cannot exclude another application's concurrent Trash
    /// call. It never blocks a Trash for long: if the lock cannot be opened,
    /// or another holder keeps it past the wait below, the call goes ahead
    /// unserialized, which is how every call behaved before the lock.
    pub(super) struct SerializedTrash {
        /// Held (and so locked) until the guard drops after the call.
        _lock: Option<fs::File>,
    }

    const WAIT: Duration = Duration::from_secs(10);

    impl SerializedTrash {
        pub(super) fn acquire() -> Self {
            Self {
                _lock: Self::lock().ok(),
            }
        }

        fn lock() -> io::Result<fs::File> {
            use std::os::unix::fs::OpenOptionsExt as _;

            let file = fs::OpenOptions::new()
                .create(true)
                .truncate(false)
                .read(true)
                .write(true)
                .mode(0o600)
                .custom_flags(libc::O_NOFOLLOW | libc::O_CLOEXEC)
                .open(user_temp_dir()?.join("com.startingblind.slate.trash.lock"))?;
            let deadline = Instant::now() + WAIT;
            loop {
                match file.try_lock() {
                    Ok(()) => return Ok(file),
                    Err(fs::TryLockError::WouldBlock) if Instant::now() < deadline => {
                        std::thread::sleep(Duration::from_millis(5));
                    }
                    Err(fs::TryLockError::WouldBlock) => {
                        return Err(io::Error::from(io::ErrorKind::TimedOut));
                    }
                    Err(fs::TryLockError::Error(error)) => return Err(error),
                }
            }
        }
    }

    /// `confstr(_CS_DARWIN_USER_TEMP_DIR)`: the user's own temporary
    /// directory, the same for every process the user runs (`$TMPDIR` can
    /// differ per process).
    fn user_temp_dir() -> io::Result<PathBuf> {
        use std::os::unix::ffi::OsStringExt as _;

        let mut buffer = vec![0u8; libc::PATH_MAX as usize];
        // SAFETY: the buffer is writable for its whole length; confstr writes
        // at most that many bytes, NUL included, and returns the length needed.
        let needed = unsafe {
            libc::confstr(
                libc::_CS_DARWIN_USER_TEMP_DIR,
                buffer.as_mut_ptr().cast(),
                buffer.len(),
            )
        };
        if needed == 0 || needed > buffer.len() {
            return Err(io::Error::other("no per-user temporary directory"));
        }
        buffer.truncate(needed - 1);
        Ok(PathBuf::from(std::ffi::OsString::from_vec(buffer)))
    }
}

/// Test double for the system Trash. It moves the entry into a scratch bin the
/// way a path-addressed Trash call acts, resolving every folder on `path`
/// with links followed (the leaf itself is moved, not its target), so a
/// handoff that passes a substituted path moves the substitute. It records
/// every path it was handed and never touches a real Trash.
#[cfg(test)]
#[derive(Debug)]
pub(crate) struct ScratchTrash {
    bin: tempfile::TempDir,
    handed: std::sync::Mutex<Vec<std::path::PathBuf>>,
}

#[cfg(test)]
impl ScratchTrash {
    pub(crate) fn new() -> std::sync::Arc<Self> {
        std::sync::Arc::new(Self {
            bin: tempfile::tempdir().expect("scratch Trash bin"),
            handed: std::sync::Mutex::new(Vec::new()),
        })
    }

    /// Every path the provider handed to the Trash, in order.
    pub(crate) fn handed(&self) -> Vec<std::path::PathBuf> {
        self.handed.lock().unwrap().clone()
    }

    /// The bin's entry names, sorted.
    pub(crate) fn contents(&self) -> Vec<String> {
        let mut names: Vec<String> = std::fs::read_dir(self.bin.path())
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .collect();
        names.sort();
        names
    }

    pub(crate) fn bin(&self) -> &Path {
        self.bin.path()
    }
}

#[cfg(test)]
impl SystemTrash for ScratchTrash {
    fn trash(&self, path: &Path) -> Result<(), VaultError> {
        self.handed.lock().unwrap().push(path.to_path_buf());
        let name = path.file_name().ok_or_else(|| VaultError::Trash {
            message: "nothing to trash".into(),
        })?;
        let mut destination = self.bin.path().join(name);
        let mut collision = 1;
        while destination.symlink_metadata().is_ok() {
            destination = self
                .bin
                .path()
                .join(format!("{} {collision}", name.to_string_lossy()));
            collision += 1;
        }
        std::fs::rename(path, &destination).map_err(|error| VaultError::Trash {
            message: error.to_string(),
        })
    }
}

#[cfg(test)]
#[path = "trash_handoff_tests.rs"]
mod tests;
