// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The vault root held open for a session's lifetime: plan 43 PR 22a (#940;
//! design `docs/plans/43_mac_backlog_delivery/22_descriptor_relative_session_design.md`
//! §4.1, §4.2, §6).
//!
//! A filesystem session used to pin its root only for the length of its
//! constructor and resolve its interior — the structural lock, `slate.json`,
//! `.slate/prefs.json`, the op-log and the SQLite cache — by pathname. A root
//! swapped A→B after the anchor and restored before the re-verification then
//! left a session whose cache, config and lock were B's while every pathname
//! observation reported A (#940's residual schedule). [`FsRoot`] holds the
//! root and its `.slate` open for the session's lifetime, and every interior
//! path resolves against them:
//!
//! - **macOS and Linux.** The root is one directory descriptor. The root
//!   itself is followed, so a vault whose root is a symlink keeps working, as
//!   the structural rename's `pinned_root` does (#1449, option H, which now
//!   adopts this descriptor). `.slate` is opened beneath it with `O_NOFOLLOW`
//!   and made there with `mkdirat` when missing. The interior's own IO goes
//!   through [`CacheDir`]: `openat`, `mkdirat`, `renameat` and `unlinkat`
//!   against the held `.slate`, with `O_NOFOLLOW` on every component, so a
//!   link anywhere in the interior is refused rather than followed and no
//!   interior writer ever creates a folder by pathname (#1474). SQLite, whose
//!   path resolution Slate does not own, reaches the same descriptor through
//!   the unix VFS's system-call hook (`db::anchored`).
//! - **Everywhere else (Windows until 22W, Milestone W).** The same types
//!   carry the pathname and every call is the one the interior made before,
//!   so behaviour there is unchanged.
//!
//! What stays pathname-based is what the user reads (§4.2): error messages
//! quote the spelling the session opened, and [`FsRoot::display_path`] is the
//! root descriptor's current path, accepted only while that path still names
//! the held root (§3.6(a): an ungated `F_GETPATH` can name another object
//! after a rename-over, or a stale path after a removal).

use std::borrow::Cow;
use std::ffi::OsString;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::{fs, io};

use crate::VaultError;

/// Whether this target resolves the interior through held descriptors:
/// macOS and Linux on 64-bit (the unix VFS hook writes SQLite's
/// `struct stat`, whose layout matches `libc::stat` there).
pub(crate) const ANCHORED: bool = cfg!(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
));

/// What an interior open asks for, in the shape both forms understand
/// (`std::fs::OpenOptions` for a pathname, `openat` flags for a held
/// directory).
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub(crate) struct FileOptions {
    read: bool,
    write: bool,
    append: bool,
    create: bool,
    create_new: bool,
    truncate: bool,
}

impl FileOptions {
    pub(crate) fn new() -> Self {
        Self::default()
    }

    pub(crate) fn read(mut self, read: bool) -> Self {
        self.read = read;
        self
    }

    pub(crate) fn write(mut self, write: bool) -> Self {
        self.write = write;
        self
    }

    pub(crate) fn append(mut self, append: bool) -> Self {
        self.append = append;
        self
    }

    pub(crate) fn create(mut self, create: bool) -> Self {
        self.create = create;
        self
    }

    pub(crate) fn create_new(mut self, create_new: bool) -> Self {
        self.create_new = create_new;
        self
    }

    pub(crate) fn truncate(mut self, truncate: bool) -> Self {
        self.truncate = truncate;
        self
    }

    fn std(self) -> fs::OpenOptions {
        let mut options = fs::OpenOptions::new();
        options
            .read(self.read)
            .write(self.write)
            .append(self.append)
            .create(self.create)
            .create_new(self.create_new)
            .truncate(self.truncate);
        options
    }

    /// `openat` flags, std's mapping, plus `O_NOFOLLOW` and `O_CLOEXEC`: an
    /// interior file that is a link is refused, never followed.
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    fn flags(self) -> libc::c_int {
        let writes = self.write || self.append;
        let access = match (self.read, writes) {
            (true, true) => libc::O_RDWR,
            (false, true) => libc::O_WRONLY,
            _ => libc::O_RDONLY,
        };
        let append = if self.append { libc::O_APPEND } else { 0 };
        let creation = if self.create_new {
            libc::O_CREAT | libc::O_EXCL
        } else {
            (if self.create { libc::O_CREAT } else { 0 })
                | (if self.truncate { libc::O_TRUNC } else { 0 })
        };
        access | append | creation | sys::NO_FOLLOW | libc::O_CLOEXEC
    }
}

/// A directory the session's interior IO resolves against (#940): the held
/// `.slate` descriptor ([`CacheDir`] on macOS and Linux), or a pathname (the
/// interior's tests and the targets without an anchored interior). Names are
/// vault-interior and relative: a leaf, or `child/leaf` one level down.
pub(crate) trait InteriorDir {
    /// The directory's spelling, for messages and logs.
    fn display_dir(&self) -> Cow<'_, Path>;
    /// Make the directory itself if it is missing. A pathname makes every
    /// missing ancestor, as the interior always has; a held directory exists
    /// already, so nothing is ever recreated at a path it has left.
    fn ensure_self(&self) -> io::Result<()>;
    /// Make the child directory `name` if it is missing: one level, inside
    /// this directory (a pathname makes its missing ancestors too).
    fn ensure_child_dir(&self, name: &str) -> io::Result<()>;
    /// Open `relative`.
    fn open_file(&self, relative: &str, options: FileOptions) -> io::Result<fs::File>;
    /// Rename `from` to `to`, replacing `to`, both inside this directory.
    fn rename_file(&self, from: &str, to: &str) -> io::Result<()>;
    /// Remove the file `relative`.
    fn remove_file(&self, relative: &str) -> io::Result<()>;
    /// The length of the file `relative` (a held directory refuses a link).
    fn file_len(&self, relative: &str) -> io::Result<u64>;
    /// The names in the child directory `name`, `.` and `..` excluded.
    fn read_child_dir(&self, name: &str) -> io::Result<Vec<OsString>>;
    /// Flush the child directory `name`'s entries after a create or rename
    /// in it. Best effort, the op-log's directory fsync: on Unix it makes the
    /// just-added entries durable; on Windows NTFS commits them through its
    /// journal, so it does nothing, as before. Callers ignore its error: a
    /// failed sync still leaves the data durable.
    fn sync_child_dir(&self, name: &str) -> io::Result<()>;
}

/// `relative`'s contents as UTF-8, read through `dir`.
pub(crate) fn read_to_string_in<D: InteriorDir + ?Sized>(
    dir: &D,
    relative: &str,
) -> io::Result<String> {
    use std::io::Read as _;
    let mut text = String::new();
    dir.open_file(relative, FileOptions::new().read(true))?
        .read_to_string(&mut text)?;
    Ok(text)
}

/// `relative` joined onto `base` one component at a time, so a pathname is
/// spelled exactly as the interior always spelled it (`join("oplog")` then
/// `join(leaf)`).
fn joined(base: &Path, relative: &str) -> PathBuf {
    relative
        .split('/')
        .fold(base.to_path_buf(), |path, part| path.join(part))
}

impl InteriorDir for Path {
    fn display_dir(&self) -> Cow<'_, Path> {
        Cow::Borrowed(self)
    }

    fn ensure_self(&self) -> io::Result<()> {
        fs::create_dir_all(self)
    }

    fn ensure_child_dir(&self, name: &str) -> io::Result<()> {
        fs::create_dir_all(joined(self, name))
    }

    fn open_file(&self, relative: &str, options: FileOptions) -> io::Result<fs::File> {
        options.std().open(joined(self, relative))
    }

    fn rename_file(&self, from: &str, to: &str) -> io::Result<()> {
        fs::rename(joined(self, from), joined(self, to))
    }

    fn remove_file(&self, relative: &str) -> io::Result<()> {
        fs::remove_file(joined(self, relative))
    }

    fn file_len(&self, relative: &str) -> io::Result<u64> {
        Ok(fs::metadata(joined(self, relative))?.len())
    }

    fn read_child_dir(&self, name: &str) -> io::Result<Vec<OsString>> {
        // An entry that cannot be read is skipped, as the op-log enumeration
        // always did.
        Ok(fs::read_dir(joined(self, name))?
            .flatten()
            .map(|entry| entry.file_name())
            .collect())
    }

    fn sync_child_dir(&self, name: &str) -> io::Result<()> {
        #[cfg(unix)]
        {
            fs::File::open(joined(self, name))?.sync_all()
        }
        #[cfg(not(unix))]
        {
            let _ = name;
            Ok(())
        }
    }
}

impl InteriorDir for PathBuf {
    fn display_dir(&self) -> Cow<'_, Path> {
        self.as_path().display_dir()
    }

    fn ensure_self(&self) -> io::Result<()> {
        self.as_path().ensure_self()
    }

    fn ensure_child_dir(&self, name: &str) -> io::Result<()> {
        self.as_path().ensure_child_dir(name)
    }

    fn open_file(&self, relative: &str, options: FileOptions) -> io::Result<fs::File> {
        self.as_path().open_file(relative, options)
    }

    fn rename_file(&self, from: &str, to: &str) -> io::Result<()> {
        self.as_path().rename_file(from, to)
    }

    fn remove_file(&self, relative: &str) -> io::Result<()> {
        self.as_path().remove_file(relative)
    }

    fn file_len(&self, relative: &str) -> io::Result<u64> {
        self.as_path().file_len(relative)
    }

    fn read_child_dir(&self, name: &str) -> io::Result<Vec<OsString>> {
        self.as_path().read_child_dir(name)
    }

    fn sync_child_dir(&self, name: &str) -> io::Result<()> {
        self.as_path().sync_child_dir(name)
    }
}

/// The session's cache directory (`<root>/.slate` for a filesystem session,
/// `SessionConfig::cache_dir` for `VaultSession::open`), held open for the
/// session's lifetime on macOS and Linux. Cloned into the compaction worker;
/// the SQLite anchor it registers lives as long as the last clone.
#[derive(Clone)]
pub(crate) struct CacheDir(Arc<CacheDirInner>);

struct CacheDirInner {
    /// The configured spelling, for messages: never resolved again.
    display: PathBuf,
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    held: Held,
}

#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
struct Held {
    /// The SQLite registration, made by the first SQLite open and dropped
    /// with the last clone (before `dir`, which it duplicated anyway).
    sqlite: std::sync::OnceLock<crate::db::anchored::SqliteAnchor>,
    /// The directory, opened for search (`O_SEARCH`, or Linux's `O_PATH`).
    dir: fs::File,
    /// `(device, inode)` from `fstat` of `dir`: the structural-lock
    /// registry's key (design §6, owner decision Q5).
    identity: (u64, u64),
}

impl std::fmt::Debug for CacheDir {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("CacheDir")
            .field("display", &self.0.display)
            .field("held", &ANCHORED)
            .finish()
    }
}

impl CacheDir {
    /// The configured spelling, for messages and logs.
    pub(crate) fn display(&self) -> &Path {
        &self.0.display
    }
}

/// The held form: macOS and Linux.
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
impl CacheDir {
    /// `VaultSession::open`'s cache directory: made by its configured path if
    /// missing (as `open` always has), then held. The configured path is the
    /// caller's own spelling, so it is followed; everything beneath it is
    /// not.
    pub(crate) fn configured(path: &Path) -> io::Result<Self> {
        use std::os::unix::fs::OpenOptionsExt as _;
        fs::create_dir_all(path)?;
        let dir = fs::OpenOptions::new()
            .read(true)
            .custom_flags(super::fs::directory_open_flags(false))
            .open(path)?;
        Self::held(dir, path.to_path_buf())
    }

    /// Hold `dir`, already open, as the cache directory spelled `display`.
    fn held(dir: fs::File, display: PathBuf) -> io::Result<Self> {
        let identity = sys::identity(&dir)?;
        Ok(Self(Arc::new(CacheDirInner {
            display,
            held: Held {
                sqlite: std::sync::OnceLock::new(),
                dir,
                identity,
            },
        })))
    }

    /// The held directory's `(device, inode)`.
    pub(crate) fn identity(&self) -> Option<(u64, u64)> {
        Some(self.0.held.identity)
    }

    /// The held directory itself: the provider stages beneath it.
    pub(crate) fn descriptor(&self) -> &fs::File {
        &self.0.held.dir
    }

    /// The path SQLite opens `leaf` by: the synthetic
    /// `/.slate-fd/<token>/<leaf>` the VFS hook resolves against the held
    /// directory, registered on first use.
    pub(crate) fn sqlite_path(&self, leaf: &str) -> io::Result<PathBuf> {
        let held = &self.0.held;
        if let Some(anchor) = held.sqlite.get() {
            return Ok(anchor.database_path(leaf));
        }
        let anchor = crate::db::anchored::SqliteAnchor::register(&held.dir, &self.0.display)?;
        // A racing registration keeps whichever landed first; the other
        // unregisters as it drops.
        Ok(held.sqlite.get_or_init(|| anchor).database_path(leaf))
    }
}

/// The pathname form (Windows until 22W): every call is the one the interior
/// made before.
#[cfg(not(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
)))]
impl CacheDir {
    /// `VaultSession::open`'s cache directory, made by its configured path if
    /// missing, as `open` always has.
    pub(crate) fn configured(path: &Path) -> io::Result<Self> {
        fs::create_dir_all(path)?;
        Ok(Self::by_path(path.to_path_buf()))
    }

    /// A filesystem session's `.slate`, by its pathname.
    fn by_path(path: PathBuf) -> Self {
        Self(Arc::new(CacheDirInner { display: path }))
    }

    /// No held directory, so no identity: the registry keys the pathname.
    pub(crate) fn identity(&self) -> Option<(u64, u64)> {
        None
    }

    /// The real path.
    pub(crate) fn sqlite_path(&self, leaf: &str) -> io::Result<PathBuf> {
        Ok(self.0.display.join(leaf))
    }
}

/// Beneath the held directory: one component at a time, never following a
/// link, never making anything but a child folder inside it.
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
impl InteriorDir for CacheDir {
    fn display_dir(&self) -> Cow<'_, Path> {
        Cow::Borrowed(&self.0.display)
    }

    fn ensure_self(&self) -> io::Result<()> {
        // It is held open: it exists, wherever it is now.
        Ok(())
    }

    fn ensure_child_dir(&self, name: &str) -> io::Result<()> {
        sys::ensure_dir_at(self.descriptor(), &sys::component(name)?)
    }

    fn open_file(&self, relative: &str, options: FileOptions) -> io::Result<fs::File> {
        let held = self.descriptor();
        let (parent, leaf) = sys::parent_and_leaf(held, relative)?;
        sys::open_at(parent.get(held), &leaf, options.flags(), 0o666)
    }

    fn rename_file(&self, from: &str, to: &str) -> io::Result<()> {
        let held = self.descriptor();
        let (from_parent, from_leaf) = sys::parent_and_leaf(held, from)?;
        let (to_parent, to_leaf) = sys::parent_and_leaf(held, to)?;
        sys::rename_at(
            from_parent.get(held),
            &from_leaf,
            to_parent.get(held),
            &to_leaf,
        )
    }

    fn remove_file(&self, relative: &str) -> io::Result<()> {
        let held = self.descriptor();
        let (parent, leaf) = sys::parent_and_leaf(held, relative)?;
        sys::unlink_at(parent.get(held), &leaf)
    }

    fn file_len(&self, relative: &str) -> io::Result<u64> {
        let held = self.descriptor();
        let (parent, leaf) = sys::parent_and_leaf(held, relative)?;
        let stat = sys::stat_at(parent.get(held), &leaf, libc::AT_SYMLINK_NOFOLLOW)?;
        if stat.st_mode & libc::S_IFMT == libc::S_IFLNK {
            return Err(io::Error::from_raw_os_error(libc::ELOOP));
        }
        Ok(u64::try_from(stat.st_size).unwrap_or_default())
    }

    fn read_child_dir(&self, name: &str) -> io::Result<Vec<OsString>> {
        let child = super::fs::open_directory_at(self.descriptor(), &sys::component(name)?)?;
        sys::read_dir_names(&child)
    }

    fn sync_child_dir(&self, name: &str) -> io::Result<()> {
        let child = super::fs::open_directory_at(self.descriptor(), &sys::component(name)?)?;
        sys::sync_dir(&child)
    }
}

/// By the configured pathname, exactly as before.
#[cfg(not(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
)))]
impl InteriorDir for CacheDir {
    fn display_dir(&self) -> Cow<'_, Path> {
        Cow::Borrowed(&self.0.display)
    }

    fn ensure_self(&self) -> io::Result<()> {
        self.0.display.ensure_self()
    }

    fn ensure_child_dir(&self, name: &str) -> io::Result<()> {
        self.0.display.ensure_child_dir(name)
    }

    fn open_file(&self, relative: &str, options: FileOptions) -> io::Result<fs::File> {
        self.0.display.open_file(relative, options)
    }

    fn rename_file(&self, from: &str, to: &str) -> io::Result<()> {
        self.0.display.rename_file(from, to)
    }

    fn remove_file(&self, relative: &str) -> io::Result<()> {
        self.0.display.remove_file(relative)
    }

    fn file_len(&self, relative: &str) -> io::Result<u64> {
        self.0.display.file_len(relative)
    }

    fn read_child_dir(&self, name: &str) -> io::Result<Vec<OsString>> {
        self.0.display.read_child_dir(name)
    }

    fn sync_child_dir(&self, name: &str) -> io::Result<()> {
        self.0.display.sync_child_dir(name)
    }
}

/// The vault root a filesystem session holds open for its lifetime (#940;
/// design §6). Built once by `VaultSession::from_filesystem_with_actor` and
/// threaded to the provider, the structural lock, the config reads, the
/// op-log, the SQLite open and the watch.
pub struct FsRoot {
    /// The spelling the session opened, for messages and the display
    /// fallback.
    opened_as: PathBuf,
    /// `(device, inode)` of the held root (`fstat` of the descriptor), or a
    /// pathname observation where the root is not held (other Unixes, as
    /// before); `None` on Windows until 22W.
    identity: Option<(u64, u64)>,
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    root: fs::File,
    cache: CacheDir,
}

impl std::fmt::Debug for FsRoot {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("FsRoot")
            .field("opened_as", &self.opened_as)
            .field("identity", &self.identity)
            .field("cache", &self.cache)
            .finish()
    }
}

impl FsRoot {
    /// The held root's `(device, inode)` — the identity the interior bound.
    pub(crate) fn identity(&self) -> Option<(u64, u64)> {
        self.identity
    }

    /// The spelling the session opened.
    pub(crate) fn opened_as(&self) -> &Path {
        &self.opened_as
    }

    /// The held `.slate`.
    pub(crate) fn cache_dir(&self) -> &CacheDir {
        &self.cache
    }

    /// The one display spelling of the root (design §4.2, owner decision
    /// Q4): its verified current path, else the spelling the session opened.
    pub(crate) fn display_path(&self) -> PathBuf {
        self.verified_current_path()
            .unwrap_or_else(|| self.opened_as.clone())
    }
}

/// The held form: macOS and Linux.
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
impl FsRoot {
    /// Open and hold the vault root at `root` and its `.slate`, making
    /// `.slate` beneath the held root when it is missing. A `.slate` that is
    /// a symbolic link, or anything but a folder, is refused: the interior
    /// never follows a link (design §3.6(b)).
    pub(crate) fn open(root: &Path) -> Result<Self, VaultError> {
        use std::os::unix::fs::OpenOptionsExt as _;
        let held = fs::OpenOptions::new()
            .read(true)
            .custom_flags(super::fs::directory_open_flags(false))
            .open(root)
            .map_err(VaultError::Io)?;
        let identity = sys::identity(&held).map_err(VaultError::Io)?;
        let slate_display = root.join(".slate");
        let slate = sys::open_or_make_slate(&held)
            .map_err(|error| slate_refusal(&held, &slate_display, error))?;
        let cache = CacheDir::held(slate, slate_display).map_err(VaultError::Io)?;
        Ok(Self {
            opened_as: root.to_path_buf(),
            identity: Some(identity),
            root: held,
            cache,
        })
    }

    /// The held root descriptor: the structural rename, the saves, the
    /// creations and the Trash handoff resolve beneath it (#1449, #1450,
    /// #1452), and the watch is anchored to it.
    pub(crate) fn descriptor(&self) -> &fs::File {
        &self.root
    }

    /// The held root's current path, from the kernel (`F_GETPATH`, or
    /// `/proc/self/fd` on Linux), accepted only when that path still names
    /// the held root (§3.6(a), the rule PR 24's matrix §4 records); `None`
    /// when it cannot be verified.
    pub(crate) fn verified_current_path(&self) -> Option<PathBuf> {
        verified_current_path(&self.root)
    }

    /// The bytes of `name` in the root (`slate.json`), opened beneath the
    /// held root without following a link.
    pub(crate) fn read_root_file(&self, name: &str) -> io::Result<Vec<u8>> {
        use std::io::Read as _;
        let mut file = sys::open_at(
            &self.root,
            &sys::component(name)?,
            FileOptions::new().read(true).flags(),
            0,
        )?;
        let mut bytes = Vec::new();
        file.read_to_end(&mut bytes)?;
        Ok(bytes)
    }

    /// Whether `relative` (vault-relative, `/`-separated) names a folder,
    /// resolved beneath the held root: the templates folder the open
    /// detects. Links are followed, as `Path::is_dir` follows them; only the
    /// starting point is anchored.
    pub(crate) fn is_dir(&self, relative: &str) -> bool {
        let Ok(relative) = std::ffi::CString::new(relative) else {
            return false;
        };
        sys::stat_at(&self.root, &relative, 0)
            .is_ok_and(|stat| stat.st_mode & libc::S_IFMT == libc::S_IFDIR)
    }
}

/// The pathname form (Windows until 22W): every read is the one the open
/// made before.
#[cfg(not(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
)))]
impl FsRoot {
    /// Record the root by its pathname; `.slate` is made by
    /// `VaultSession::open`, as before.
    pub(crate) fn open(root: &Path) -> Result<Self, VaultError> {
        Ok(Self {
            opened_as: root.to_path_buf(),
            identity: observed_identity(root),
            cache: CacheDir::by_path(root.join(".slate")),
        })
    }

    /// Nothing is held, so nothing can be verified.
    pub(crate) fn verified_current_path(&self) -> Option<PathBuf> {
        None
    }

    /// `name` in the root, by path.
    pub(crate) fn read_root_file(&self, name: &str) -> io::Result<Vec<u8>> {
        fs::read(self.opened_as.join(name))
    }

    /// Whether `relative` names a folder, by path.
    pub(crate) fn is_dir(&self, relative: &str) -> bool {
        self.opened_as.join(relative).is_dir()
    }
}

/// `descriptor`'s current path, accepted only while it still names the
/// object `descriptor` is open on (the Trash handoff's own check, shared).
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
pub(crate) fn verified_current_path(descriptor: &fs::File) -> Option<PathBuf> {
    let path = super::trash_handoff::current_path(descriptor).ok()?;
    super::trash_handoff::names(&path, descriptor).then_some(path)
}

/// A `.slate` that is a link or not a folder, as core's typed refusal; any
/// other failure stays the IO error.
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
fn slate_refusal(root: &fs::File, slate: &Path, error: io::Error) -> VaultError {
    if !matches!(
        error.raw_os_error(),
        Some(libc::ELOOP) | Some(libc::ENOTDIR)
    ) {
        return VaultError::Io(error);
    }
    // Look at the entry itself: `O_NOFOLLOW` with a directory open reports a
    // link as `ENOTDIR` on macOS and `ELOOP` on Linux.
    let mode = sys::stat_at(root, c".slate", libc::AT_SYMLINK_NOFOLLOW)
        .map(|stat| stat.st_mode & libc::S_IFMT);
    let reason = match mode {
        Ok(libc::S_IFLNK) => {
            "\".slate\" is a symbolic link, not a folder in this vault, so the vault was not opened"
        }
        // It changed back into a folder, or vanished, since the open.
        Ok(libc::S_IFDIR) | Err(_) => return VaultError::Io(error),
        Ok(_) => "\".slate\" is not a folder, so the vault was not opened",
    };
    VaultError::InvalidPath {
        path: slate.display().to_string(),
        reason: reason.into(),
    }
}

/// The root's identity by one pathname observation, where the root is not
/// held: other Unixes keep the round-27 observation; Windows has none until
/// 22W (the host fails closed on `None`).
#[cfg(not(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
)))]
fn observed_identity(root: &Path) -> Option<(u64, u64)> {
    #[cfg(unix)]
    {
        use std::os::unix::fs::MetadataExt as _;
        let meta = fs::File::open(root).ok()?.metadata().ok()?;
        Some((meta.dev(), meta.ino()))
    }
    #[cfg(not(unix))]
    {
        let _ = root;
        None
    }
}

/// The `openat`-family calls the anchored interior is made of. Every name is
/// one component; every open adds `O_NOFOLLOW`.
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
pub(crate) mod sys {
    use std::ffi::{CStr, CString, OsString};
    use std::os::fd::{AsRawFd as _, FromRawFd as _};
    use std::os::unix::ffi::OsStrExt as _;
    use std::{fs, io};

    /// Added to every interior open: a link is refused, never followed.
    pub(crate) const NO_FOLLOW: libc::c_int = libc::O_NOFOLLOW;

    /// `name` as one NUL-terminated component, refusing an empty name, `.`,
    /// `..`, a `/` or an embedded NUL.
    pub(crate) fn component(name: &str) -> io::Result<CString> {
        if name.is_empty() || name == "." || name == ".." || name.contains('/') {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "an interior name must be one component",
            ));
        }
        CString::new(name).map_err(|error| io::Error::new(io::ErrorKind::InvalidInput, error))
    }

    /// The folder holding `relative`'s leaf: the held directory itself, or
    /// one child folder opened beneath it without following a link.
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

    pub(crate) fn parent_and_leaf(
        held: &fs::File,
        relative: &str,
    ) -> io::Result<(Parent, CString)> {
        match relative.split_once('/') {
            None => Ok((Parent::Held, component(relative)?)),
            Some((child, leaf)) => {
                let child = super::super::fs::open_directory_at(held, &component(child)?)?;
                Ok((Parent::Child(child), component(leaf)?))
            }
        }
    }

    pub(crate) fn identity(file: &fs::File) -> io::Result<(u64, u64)> {
        use std::os::unix::fs::MetadataExt as _;
        let meta = file.metadata()?;
        Ok((meta.dev(), meta.ino()))
    }

    pub(crate) fn open_at(
        dir: &fs::File,
        name: &CStr,
        flags: libc::c_int,
        mode: libc::c_uint,
    ) -> io::Result<fs::File> {
        // SAFETY: `dir` is an open directory and `name` is NUL-terminated;
        // both outlive the call. A successful descriptor is uniquely owned by
        // the returned `File`.
        let descriptor = unsafe { raw_open_at(dir.as_raw_fd(), name.as_ptr(), flags, mode) };
        if descriptor < 0 {
            return Err(io::Error::last_os_error());
        }
        Ok(unsafe { fs::File::from_raw_fd(descriptor) })
    }

    /// How many times a create beneath a directory descriptor goes round
    /// before it gives up. Each extra round needs another opener to have made
    /// and removed the name in between.
    const CREATE_ROUNDS: usize = 16;

    /// `openat`, with a create that cannot lose a race. macOS's `openat`
    /// with `O_CREAT` and no `O_EXCL`, relative to a directory descriptor,
    /// can fail with `ENOENT` when another opener makes the same name at
    /// that moment. On Darwin 27, about one open in four failed in a
    /// two-process race, while `open` by an absolute path never did. Two
    /// `slate write` processes on a fresh vault met it at `structural.lock`,
    /// and SQLite meets it as a quiet read-only retry.
    ///
    /// So such a create opens the name without `O_CREAT` first, and makes it
    /// with `O_EXCL` only when it is missing. An opener that loses that race
    /// opens what the winner made. The result is what `O_CREAT` promises:
    /// the one file, made if it was missing. Any other open is one `openat`.
    /// It returns the descriptor, or -1 with `errno` set, so the SQLite hook
    /// can call it as it would call `openat`.
    ///
    /// # Safety
    ///
    /// `dir` is an open directory descriptor and `name` a NUL-terminated
    /// string, both valid for the call, as `openat` requires.
    pub(crate) unsafe fn raw_open_at(
        dir: libc::c_int,
        name: *const libc::c_char,
        flags: libc::c_int,
        mode: libc::c_uint,
    ) -> libc::c_int {
        if flags & libc::O_CREAT == 0 || flags & libc::O_EXCL != 0 {
            // SAFETY: the caller's contract.
            return unsafe { libc::openat(dir, name, flags, mode) };
        }
        let errno = || io::Error::last_os_error().raw_os_error();
        let mut descriptor = -1;
        for _ in 0..CREATE_ROUNDS {
            // SAFETY: the caller's contract.
            descriptor = unsafe { libc::openat(dir, name, flags & !libc::O_CREAT) };
            if descriptor >= 0 || errno() != Some(libc::ENOENT) {
                return descriptor;
            }
            // SAFETY: the caller's contract.
            descriptor = unsafe { libc::openat(dir, name, flags | libc::O_EXCL, mode) };
            if descriptor >= 0 || errno() != Some(libc::EEXIST) {
                return descriptor;
            }
        }
        descriptor
    }

    /// Make the folder `name` in `dir` if it is missing; one that is there
    /// already is fine (the open beneath it refuses a link or a file).
    pub(crate) fn ensure_dir_at(dir: &fs::File, name: &CStr) -> io::Result<()> {
        match super::super::fs::create_directory_at(dir, name) {
            Err(error) if error.kind() == io::ErrorKind::AlreadyExists => Ok(()),
            other => other,
        }
    }

    /// `.slate` beneath the held root, made there with `mkdirat` when it is
    /// missing, never through a link.
    pub(crate) fn open_or_make_slate(root: &fs::File) -> io::Result<fs::File> {
        let name = c".slate";
        match super::super::fs::open_directory_at(root, name) {
            Err(error) if error.kind() == io::ErrorKind::NotFound => {
                ensure_dir_at(root, name)?;
                super::super::fs::open_directory_at(root, name)
            }
            other => other,
        }
    }

    pub(crate) fn rename_at(
        from_dir: &fs::File,
        from: &CStr,
        to_dir: &fs::File,
        to: &CStr,
    ) -> io::Result<()> {
        // SAFETY: both directories are open and both names are
        // NUL-terminated single components that outlive the call.
        let result = unsafe {
            libc::renameat(
                from_dir.as_raw_fd(),
                from.as_ptr(),
                to_dir.as_raw_fd(),
                to.as_ptr(),
            )
        };
        if result != 0 {
            return Err(io::Error::last_os_error());
        }
        Ok(())
    }

    pub(crate) fn unlink_at(dir: &fs::File, name: &CStr) -> io::Result<()> {
        // SAFETY: `dir` is open and `name` is a NUL-terminated component.
        let result = unsafe { libc::unlinkat(dir.as_raw_fd(), name.as_ptr(), 0) };
        if result != 0 {
            return Err(io::Error::last_os_error());
        }
        Ok(())
    }

    /// `fstatat(dir, name, flags)`.
    pub(crate) fn stat_at(
        dir: &fs::File,
        name: &CStr,
        flags: libc::c_int,
    ) -> io::Result<libc::stat> {
        let mut stat = std::mem::MaybeUninit::<libc::stat>::uninit();
        // SAFETY: `dir` is open, `name` is NUL-terminated and `stat` has the
        // size and alignment fstatat writes.
        let result =
            unsafe { libc::fstatat(dir.as_raw_fd(), name.as_ptr(), stat.as_mut_ptr(), flags) };
        if result != 0 {
            return Err(io::Error::last_os_error());
        }
        // SAFETY: a successful fstatat initialized `stat`.
        Ok(unsafe { stat.assume_init() })
    }

    /// Flush `dir`'s entries. A search-only descriptor cannot be synced, so
    /// the folder is reopened through it for reading.
    pub(crate) fn sync_dir(dir: &fs::File) -> io::Result<()> {
        open_at(
            dir,
            c".",
            libc::O_RDONLY | libc::O_DIRECTORY | libc::O_CLOEXEC,
            0,
        )?
        .sync_all()
    }

    /// The names in `dir`, `.` and `..` excluded.
    pub(crate) fn read_dir_names(dir: &fs::File) -> io::Result<Vec<OsString>> {
        // `fdopendir` owns the descriptor it is given, so it gets its own,
        // opened for reading (a search-only descriptor cannot list).
        let listing = open_at(
            dir,
            c".",
            libc::O_RDONLY | libc::O_DIRECTORY | libc::O_CLOEXEC,
            0,
        )?;
        let descriptor = std::os::fd::IntoRawFd::into_raw_fd(listing);
        // SAFETY: `descriptor` is an open directory we own; on success the
        // stream owns it and `closedir` below closes it.
        let stream = unsafe { libc::fdopendir(descriptor) };
        if stream.is_null() {
            let error = io::Error::last_os_error();
            // SAFETY: the stream did not take the descriptor; close it.
            unsafe { libc::close(descriptor) };
            return Err(error);
        }
        let mut names = Vec::new();
        let outcome = loop {
            set_errno(0);
            // SAFETY: `stream` is a live directory stream used by this
            // thread alone.
            let entry = unsafe { libc::readdir(stream) };
            if entry.is_null() {
                let error = io::Error::last_os_error();
                break match error.raw_os_error() {
                    Some(0) | None => Ok(()),
                    Some(_) => Err(error),
                };
            }
            // SAFETY: a non-null entry points at a dirent whose `d_name` is
            // NUL-terminated, valid until the next readdir on this stream.
            let name = unsafe { CStr::from_ptr((*entry).d_name.as_ptr()) };
            let bytes = name.to_bytes();
            if bytes != b"." && bytes != b".." {
                names.push(std::ffi::OsStr::from_bytes(bytes).to_os_string());
            }
        };
        // SAFETY: `stream` is live and closed exactly once.
        unsafe { libc::closedir(stream) };
        outcome.map(|()| names)
    }

    /// Set the calling thread's `errno`.
    pub(crate) fn set_errno(value: libc::c_int) {
        // SAFETY: the errno location is the calling thread's own and always
        // valid to write.
        #[cfg(target_os = "macos")]
        unsafe {
            *libc::__error() = value;
        }
        #[cfg(target_os = "linux")]
        unsafe {
            *libc::__errno_location() = value;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The interior's two forms make the same files: a pathname keeps the
    /// spelling the interior always used, and a held directory resolves the
    /// same names beneath its descriptor.
    #[test]
    fn both_forms_open_create_rename_list_and_remove_the_same_files() {
        let tmp = tempfile::tempdir().unwrap();
        let path = tmp.path().join(".slate");
        let held = CacheDir::configured(&path).unwrap();
        for dir in [&held as &dyn InteriorDir, &path as &dyn InteriorDir] {
            dir.ensure_child_dir("oplog").unwrap();
            dir.ensure_child_dir("oplog").unwrap();
            use std::io::Write as _;
            dir.open_file(
                "oplog/a.tmp",
                FileOptions::new().write(true).create(true).truncate(true),
            )
            .unwrap()
            .write_all(b"a")
            .unwrap();
            dir.rename_file("oplog/a.tmp", "oplog/a.oplog").unwrap();
            dir.sync_child_dir("oplog").unwrap();
            assert_eq!(
                dir.read_child_dir("oplog").unwrap(),
                [OsString::from("a.oplog")]
            );
            assert_eq!(
                std::fs::read(path.join("oplog").join("a.oplog")).unwrap(),
                b"a"
            );
            dir.remove_file("oplog/a.oplog").unwrap();
            assert!(dir.read_child_dir("oplog").unwrap().is_empty());
            assert!(
                dir.open_file("oplog/missing", FileOptions::new().read(true))
                    .is_err_and(|error| error.kind() == io::ErrorKind::NotFound)
            );
        }
    }

    /// A held cache directory keeps resolving the folder it opened after the
    /// folder is renamed, and never recreates the old path.
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    #[test]
    fn a_held_cache_directory_follows_its_folder_and_never_recreates_the_old_path() {
        let tmp = tempfile::tempdir().unwrap();
        let path = tmp.path().join("cache");
        let moved = tmp.path().join("moved");
        let held = CacheDir::configured(&path).unwrap();
        std::fs::rename(&path, &moved).unwrap();
        held.ensure_child_dir("oplog").unwrap();
        held.open_file(
            "oplog/x.oplog",
            FileOptions::new().write(true).create_new(true),
        )
        .unwrap();
        assert!(moved.join("oplog").join("x.oplog").is_file());
        assert!(!path.exists(), "the old path was recreated");
        assert_eq!(held.display(), path, "messages keep the opened spelling");
    }

    /// Every interior component is opened without following a link: a
    /// linked child folder or a linked file is refused, and the link's
    /// target is never written.
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    #[test]
    fn a_held_cache_directory_never_follows_a_link() {
        let tmp = tempfile::tempdir().unwrap();
        let elsewhere = tempfile::tempdir().unwrap();
        let path = tmp.path().join(".slate");
        let held = CacheDir::configured(&path).unwrap();
        std::os::unix::fs::symlink(elsewhere.path(), path.join("oplog")).unwrap();
        std::os::unix::fs::symlink(elsewhere.path().join("p.json"), path.join("prefs.json"))
            .unwrap();
        std::fs::write(elsewhere.path().join("p.json"), b"{}").unwrap();

        let refused = |result: io::Result<fs::File>| {
            let error = result.expect_err("a link was followed");
            assert!(
                matches!(
                    error.raw_os_error(),
                    Some(libc::ELOOP) | Some(libc::ENOTDIR)
                ),
                "{error:?}"
            );
        };
        refused(held.open_file("oplog/x.oplog", FileOptions::new().write(true).create(true)));
        refused(held.open_file("prefs.json", FileOptions::new().read(true)));
        assert!(held.read_child_dir("oplog").is_err());
        assert_eq!(std::fs::read_dir(elsewhere.path()).unwrap().count(), 1);
    }

    /// macOS's `openat` with `O_CREAT` and no `O_EXCL`, relative to a
    /// directory descriptor, can fail with `ENOENT` when another opener makes
    /// the same name at that moment. On this Mac (Darwin 27) about one open in
    /// four failed in a two-process race, while `open` and
    /// `openat(AT_FDCWD)` by an absolute path never did. Two `slate write`
    /// processes on a fresh vault met it at `structural.lock`. So a create
    /// beneath a held directory opens the name first and makes it with
    /// `O_EXCL` only when it is missing: racing openers all open the one
    /// file.
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    #[test]
    fn racing_creates_beneath_a_held_directory_all_open_the_one_file() {
        use std::os::unix::fs::MetadataExt as _;
        let tmp = tempfile::tempdir().unwrap();
        for round in 0..200 {
            let held = CacheDir::configured(&tmp.path().join(format!("d{round}"))).unwrap();
            let start = Arc::new(std::sync::Barrier::new(4));
            let openers: Vec<_> = (0..4)
                .map(|_| {
                    let held = held.clone();
                    let start = Arc::clone(&start);
                    std::thread::spawn(move || {
                        start.wait();
                        held.open_file(
                            "structural.lock",
                            FileOptions::new().read(true).write(true).create(true),
                        )
                    })
                })
                .collect();
            let inodes: Vec<u64> = openers
                .into_iter()
                .map(|opener| {
                    let file = opener
                        .join()
                        .unwrap()
                        .unwrap_or_else(|error| panic!("round {round}: {error:?}"));
                    file.metadata().unwrap().ino()
                })
                .collect();
            assert!(
                inodes.windows(2).all(|pair| pair[0] == pair[1]),
                "round {round}: {inodes:?}"
            );
        }
    }
}
