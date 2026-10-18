// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The handoff from a validated Trash inventory to the system Trash (#1450).
//!
//! The inventory descends from the vault root one directory at a time
//! without following links (`trash_snapshot`). Every system Trash API takes
//! a path (or a URL made from one) and resolves it again, following links,
//! in code Slate does not control: trash-rs canonicalizes the parent, and
//! Finder, `NSFileManager` and the Windows Shell resolve what they are
//! handed. Until #1450 the provider handed over the lexical vault path, so a
//! folder replaced by a link after the last check redirected the Trash to an
//! object outside the vault (#1430's probes P12.6 and P16).
//!
//! The path handed over is now derived from the pinned parent. On Unix the
//! provider pins the item's parent with the structural rename's walk
//! (`fs::pinned_root`, #1449), which refuses a link or a non-folder among the
//! ancestors; on Windows [`pinned_path`] walks handles itself. The parent's
//! descriptor (handle) then reports its own current path, which must still
//! name that descriptor and lie beneath the vault root's current path, taken
//! the same way, or the handoff refuses: a pinned folder that moved out of
//! the vault is caught here. Every refusal is `VaultError::InvalidPath` with
//! a reason core writes, the shape the rename refusal uses, and happens
//! before any filesystem mutation.
//!
//! On macOS and Linux every item reaches the Trash, and a symbolic link goes
//! as the link itself, whether or not its target resolves: Move to Trash
//! never deletes for good (#1477, #1495). Windows alone still unlinks a link
//! whose target is truly absent instead, until the Recycle Bin is checked
//! with one ([`link_target_is_absent`], #1499).
//!
//! What remains is the race between this derivation and the Trash call's own
//! resolution of the path. Only a handle-bound Trash API could close it, and
//! no platform has one (#1430 T1). On macOS the path becomes a file reference
//! URL before `trashItemAtURL:`, so a substitution after that point no longer
//! redirects the call (P12.2); P13 measured the race left inside the call.
//!
//! Tests never reach the system Trash of whoever runs them (#1455). In core's
//! own test build every `FsVaultProvider::new` hands its deletions to a
//! [`ScratchTrash`] with a bin of its own ([`default_trash`]), and the system
//! Trash refuses any path off the scratch volume of the gated real-Trash
//! facts. A host's test process routes every session it opens into a scratch
//! bin of its own ([`route_trash_to_scratch_bin_for_tests`]). A shipping
//! build hands every deletion to the system Trash, as before.

use std::fmt;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex, PoisonError};
#[cfg(unix)]
use std::{ffi::CStr, fs, io};

use crate::VaultError;

/// The one step of a Trash Slate does not control: the platform's own Trash
/// call, handed the absolute path derived from the pinned parent an instant
/// earlier. Shipping builds use [`PlatformTrash`]; tests use [`ScratchTrash`].
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
        #[cfg(test)]
        refuse_a_trash_that_is_not_scratch(path);
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

/// The environment variable that names the scratch volume of the gated
/// real-Trash facts (`trash_handoff_tests`). Only a path on it, whose Trash
/// is that volume's own, may reach the system Trash from a test build.
#[cfg(test)]
pub(crate) const PROBE_VOLUME: &str = "SLATE_TRASH_PROBE_VOLUME";

/// #1455: core's tests reach the system Trash only from the gated real-Trash
/// facts, and only for a path on the scratch volume they name in
/// [`PROBE_VOLUME`], whose Trash belongs to that volume and not to whoever
/// runs the suite. Any other path is a test about to move its fixture into a
/// developer's `~/.Trash` or a runner's Trash or Recycle Bin, so the test
/// panics here, before the call. Every other test trashes into a
/// [`ScratchTrash`].
#[cfg(test)]
fn refuse_a_trash_that_is_not_scratch(path: &Path) {
    let refusal = |why: String| -> ! {
        panic!(
            "#1455: a test handed {path:?} to the system Trash ({why}). Core's tests trash \
             into a ScratchTrash bin; only the gated real-Trash facts reach the system Trash, \
             for a path on the scratch volume named in {PROBE_VOLUME}."
        )
    };
    let Some(volume) = std::env::var_os(PROBE_VOLUME) else {
        refusal(format!("{PROBE_VOLUME} is not set"));
    };
    let volume = std::fs::canonicalize(&volume)
        .unwrap_or_else(|error| refusal(format!("{PROBE_VOLUME} cannot be resolved: {error}")));
    if !path.starts_with(&volume) {
        refusal(format!("the path is not on {}", volume.display()));
    }
    // On the home volume, macOS's Trash for the item is the user's own
    // `~/.Trash`; on a scratch volume it is that volume's `.Trashes/<uid>`.
    #[cfg(target_os = "macos")]
    {
        use std::os::unix::fs::MetadataExt as _;
        let device = |at: &Path| std::fs::metadata(at).map(|metadata| metadata.dev());
        let home = std::env::var_os("HOME").unwrap_or_default();
        match (device(&volume), device(Path::new(&home))) {
            (Ok(scratch), Ok(home)) if scratch != home => {}
            _ => refusal(format!(
                "{} is not a volume of its own, so its Trash is ~/.Trash",
                volume.display()
            )),
        }
    }
    // trash-rs moves an item on the home Trash's own mount into
    // `$XDG_DATA_HOME/Trash`, so that must be scratch too.
    #[cfg(any(target_os = "linux", target_os = "android"))]
    {
        let scratch_home = std::env::var_os("XDG_DATA_HOME")
            .and_then(|home| std::fs::canonicalize(home).ok())
            .is_some_and(|home| home.starts_with(&volume));
        if !scratch_home {
            refusal(format!("XDG_DATA_HOME is not on {}", volume.display()));
        }
    }
    // No real-Trash fact runs on Windows: the Recycle Bin is never a test's.
    #[cfg(windows)]
    refusal("Windows has no gated real-Trash fact".into());
}

/// The refusal: `original` is not handed to the Trash, for `reason`, which
/// core writes and both hosts render ("Invalid path …: …").
fn refused(original: &str, reason: impl Into<String>) -> VaultError {
    VaultError::InvalidPath {
        path: original.to_string(),
        reason: reason.into(),
    }
}

/// A folder pinned by the walk moved, here or out of the vault, or was
/// removed, before the Trash call: its location no longer holds.
fn moved(original: &str) -> VaultError {
    refused(
        original,
        "a folder in this path moved after it was checked, so nothing was moved to the Trash",
    )
}

/// Windows only: whether following a symbolic link failed because its
/// target is truly absent (#1477). Only then does `delete` unlink the link
/// rather than hand it to the Recycle Bin, which may reject a dangling link;
/// nobody has checked yet (#1499). Absent means the system reports nothing
/// there: `ERROR_FILE_NOT_FOUND`, `ERROR_PATH_NOT_FOUND`, or `ERROR_DIRECTORY`
/// (`ENOTDIR`'s counterpart; std maps both to `NotADirectory`).
///
/// Every other failure leaves the target unproven absent, so the link goes to
/// the Recycle Bin as the link: a target Slate cannot open
/// (`ERROR_ACCESS_DENIED`), a drive or share that is missing or unreachable
/// now (`ERROR_INVALID_DRIVE`, `ERROR_BAD_NETPATH`, `ERROR_BAD_NET_NAME`,
/// which std also calls `NotFound`), a loop (`ERROR_CANT_RESOLVE_FILENAME`),
/// and any I/O or network failure. Before #1477 every failure counted as
/// absent, so each such link was unlinked for good, with no error.
///
/// macOS and Linux no longer ask (#1495): every link goes to their Trash as
/// the link. On a scratch APFS volume `NSFileManager` moved every kind, a
/// dangling link and a link through a file included, into the Trash as the
/// link with its Put Back records, and trash-rs renames the link itself into
/// the freedesktop Trash.
#[cfg(windows)]
pub(super) fn link_target_is_absent(error: &std::io::Error) -> bool {
    use windows_sys::Win32::Foundation::{
        ERROR_DIRECTORY, ERROR_FILE_NOT_FOUND, ERROR_PATH_NOT_FOUND,
    };
    error.raw_os_error().is_some_and(|code| {
        matches!(
            u32::try_from(code),
            Ok(ERROR_FILE_NOT_FOUND | ERROR_PATH_NOT_FOUND | ERROR_DIRECTORY)
        )
    })
}

/// The path to hand the system Trash for `leaf` inside the pinned `parent`:
/// the parent's current path joined with the leaf, refused unless that path
/// still names the pinned parent and lies beneath the vault root's current
/// path (both taken from their descriptors, so a symlinked vault root, which
/// the root descriptor followed when it was opened, keeps working). A
/// platform that cannot report a descriptor's path (Linux without `/proc`)
/// refuses rather than trust the spelling.
#[cfg(unix)]
pub(super) fn pinned_path(
    root: &fs::File,
    parent: &fs::File,
    leaf: &CStr,
    original: &str,
) -> Result<PathBuf, VaultError> {
    use std::os::unix::ffi::OsStrExt as _;

    let unverifiable = |_| {
        refused(
            original,
            "Slate could not confirm where this path is now, so nothing was moved to the Trash",
        )
    };
    let root_now = current_path(root).map_err(unverifiable)?;
    let parent_now = current_path(parent).map_err(unverifiable)?;
    // A reported path that no longer names its descriptor (a folder removed
    // or moved since, or Linux's "(deleted)" suffix) is not a location.
    if !names(&root_now, root) || !names(&parent_now, parent) {
        return Err(moved(original));
    }
    if !parent_now.starts_with(&root_now) {
        return Err(moved(original));
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
/// beneath the root handle's final path. A reparse point (junction or
/// symbolic link) or a non-folder among the ancestors refuses naming that
/// folder, as the Unix walk's refusal does.
#[cfg(windows)]
pub(super) fn pinned_path(
    root: &Path,
    vault_relative: &Path,
    original: &str,
) -> Result<PathBuf, VaultError> {
    use std::os::windows::fs::{MetadataExt as _, OpenOptionsExt as _};
    use std::path::Component;
    use windows_sys::Win32::Storage::FileSystem::{
        FILE_ATTRIBUTE_REPARSE_POINT, FILE_FLAG_BACKUP_SEMANTICS, FILE_LIST_DIRECTORY,
        FILE_READ_ATTRIBUTES, FILE_SHARE_READ, FILE_SHARE_WRITE,
    };

    let leaf = vault_relative
        .file_name()
        .ok_or_else(|| refused(original, "the path has no final component"))?;
    let root_directory = std::fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
        .open(root)?;
    let mut parent: Option<std::fs::File> = None;
    let mut prefix = PathBuf::new();
    for component in vault_relative
        .parent()
        .unwrap_or_else(|| Path::new(""))
        .components()
    {
        let Component::Normal(name) = component else {
            return Err(refused(original, "the path is not vault-relative"));
        };
        prefix.push(name);
        let child = super::windows_entry::open_relative(
            parent.as_ref().unwrap_or(&root_directory),
            name,
            FILE_READ_ATTRIBUTES | FILE_LIST_DIRECTORY,
        )?;
        let metadata = child.metadata()?;
        let folder = prefix.to_string_lossy().replace('\\', "/");
        if metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
            return Err(refused(
                original,
                format!(
                    "\"{folder}\" is a junction or symbolic link, not a folder in this vault, so \
                     nothing was moved"
                ),
            ));
        }
        if !metadata.is_dir() {
            return Err(refused(
                original,
                format!("\"{folder}\" is not a folder, so nothing was moved"),
            ));
        }
        parent = Some(child);
    }
    let root_now = final_path(&root_directory)?;
    let parent_now = match &parent {
        Some(parent) => final_path(parent)?,
        None => root_now.clone(),
    };
    if !parent_now.starts_with(&root_now) {
        return Err(moved(original));
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

/// A Trash that moves each entry into a scratch bin, a folder of its own,
/// instead of the system Trash: what every test trashes into (#1455). It
/// acts on its path the way a path-addressed system Trash does, resolving
/// every folder on `path` with links followed (the leaf itself is moved, not
/// its target), so a handoff that passes a substituted path moves the
/// substitute. A same-named entry gains a number (`note.md 1`). It records
/// every path it was handed and never touches a real Trash.
///
/// Core's own tests give each provider one with a bin of its own
/// ([`default_trash`]); a host's test process routes every session it opens
/// into one ([`route_trash_to_scratch_bin_for_tests`]). Shipping code never
/// builds one.
#[derive(Debug)]
pub(crate) struct ScratchTrash {
    bin: ScratchBin,
    /// Every path this bin was handed, in order. Its lock also covers the
    /// name check and the move, so same-named entries trashed at one moment
    /// from two threads both stay in the bin.
    handed: Mutex<Vec<PathBuf>>,
}

#[derive(Debug)]
enum ScratchBin {
    /// A temporary folder of the bin's own, made when the first entry
    /// arrives and removed with the bin: a core test's provider.
    #[cfg(test)]
    Owned(std::sync::OnceLock<tempfile::TempDir>),
    /// A folder the test process made and keeps: a host's test bootstrap.
    Given(PathBuf),
}

impl ScratchTrash {
    /// A bin of its own in the temporary directory, made when the first
    /// entry arrives.
    #[cfg(test)]
    pub(crate) fn new() -> Arc<Self> {
        Arc::new(Self::with_bin(
            ScratchBin::Owned(std::sync::OnceLock::new()),
        ))
    }

    fn in_folder(folder: PathBuf) -> Self {
        Self::with_bin(ScratchBin::Given(folder))
    }

    fn with_bin(bin: ScratchBin) -> Self {
        Self {
            bin,
            handed: Mutex::new(Vec::new()),
        }
    }

    /// The bin's folder.
    pub(crate) fn bin(&self) -> &Path {
        match &self.bin {
            #[cfg(test)]
            ScratchBin::Owned(folder) => folder
                .get_or_init(|| tempfile::tempdir().expect("scratch Trash bin"))
                .path(),
            ScratchBin::Given(folder) => folder,
        }
    }

    /// Every path the provider handed to the Trash, in order.
    #[cfg(test)]
    pub(crate) fn handed(&self) -> Vec<PathBuf> {
        self.handed
            .lock()
            .unwrap_or_else(PoisonError::into_inner)
            .clone()
    }

    /// The bin's entry names, sorted.
    #[cfg(test)]
    pub(crate) fn contents(&self) -> Vec<String> {
        let mut names: Vec<String> = std::fs::read_dir(self.bin())
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .collect();
        names.sort();
        names
    }
}

impl SystemTrash for ScratchTrash {
    fn trash(&self, path: &Path) -> Result<(), VaultError> {
        #[cfg(test)]
        SCRATCH_HANDOFFS
            .lock()
            .unwrap_or_else(PoisonError::into_inner)
            .push(path.to_path_buf());
        let mut handed = self.handed.lock().unwrap_or_else(PoisonError::into_inner);
        handed.push(path.to_path_buf());
        let name = path.file_name().ok_or_else(|| VaultError::Trash {
            message: "nothing to trash".into(),
        })?;
        let bin = self.bin();
        let mut destination = bin.join(name);
        let mut collision = 1;
        while destination.symlink_metadata().is_ok() {
            destination = bin.join(format!("{} {collision}", name.to_string_lossy()));
            collision += 1;
        }
        std::fs::rename(path, &destination).map_err(|error| VaultError::Trash {
            message: format!(
                "could not move the item into the scratch Trash bin {}: {error}",
                bin.display()
            ),
        })
    }
}

/// Every path any [`ScratchTrash`] in this test process was handed, so a fact
/// can show that a provider it did not build (a session's own) trashed into
/// a scratch bin (#1455).
#[cfg(test)]
static SCRATCH_HANDOFFS: Mutex<Vec<PathBuf>> = Mutex::new(Vec::new());

/// The paths every [`ScratchTrash`] in this process was handed, in order.
#[cfg(test)]
pub(crate) fn scratch_handoffs() -> Vec<PathBuf> {
    SCRATCH_HANDOFFS
        .lock()
        .unwrap_or_else(PoisonError::into_inner)
        .clone()
}

/// The Trash a provider built by `FsVaultProvider::new` hands its deletions
/// to (#1455). A shipping build hands them to the system Trash, unless the
/// process routed its Trash into a scratch bin, which only a host's test
/// process does ([`route_trash_to_scratch_bin_for_tests`]). Core's own test
/// build gives every provider a [`ScratchTrash`] with a bin of its own, so no
/// test moves a fixture into the Trash of whoever runs the suite.
pub(crate) fn default_trash() -> Arc<dyn SystemTrash> {
    #[cfg(test)]
    {
        ScratchTrash::new()
    }
    #[cfg(not(test))]
    {
        match ROUTED.get() {
            Some(scratch) => scratch.clone(),
            None => Arc::new(PlatformTrash),
        }
    }
}

/// The scratch bin this process routed its Trash into, once it has.
static ROUTED: std::sync::OnceLock<Arc<ScratchTrash>> = std::sync::OnceLock::new();

/// **Test processes only (#1455).** Routes the Trash of every filesystem
/// provider this process builds afterwards into `bin` instead of the system
/// Trash: every `VaultSession::from_filesystem` session, and so every session
/// a host opens. `bin` is a folder the caller made and keeps, on the volume
/// that holds its test vaults (an entry moves there with `rename`). Each
/// deleted entry lands in it under its own name, a same-named one with a
/// number added.
///
/// A host's test bundle calls this once, as it loads and before any test
/// runs: the mac suite's `SlateMacTestBootstrap`, the Windows suite's module
/// initializer. Shipping code never calls it, and a source census in core and
/// in each host fails if any does. The routing cannot be undone or moved: a
/// second call naming the same folder changes nothing, one naming another is
/// refused, and a provider built before the first call keeps the system
/// Trash.
pub fn route_trash_to_scratch_bin_for_tests(bin: &Path) -> Result<(), VaultError> {
    if !bin.is_absolute() || !bin.is_dir() {
        return Err(VaultError::InvalidPath {
            path: bin.display().to_string(),
            reason: "a scratch Trash bin must be an existing folder named by an absolute path"
                .into(),
        });
    }
    let routed = ROUTED.get_or_init(|| Arc::new(ScratchTrash::in_folder(bin.to_path_buf())));
    if routed.bin() == bin {
        Ok(())
    } else {
        Err(VaultError::InvalidArgument {
            message: format!(
                "this process already routes its Trash into {}",
                routed.bin().display()
            ),
        })
    }
}

/// The folder this process routed its Trash into
/// ([`route_trash_to_scratch_bin_for_tests`]), or `None` while it uses the
/// system Trash. The hosts' test censuses ask before they trash anything.
pub fn scratch_trash_bin_for_tests() -> Option<PathBuf> {
    ROUTED.get().map(|routed| routed.bin().to_path_buf())
}

#[cfg(test)]
#[path = "trash_handoff_tests.rs"]
mod tests;
