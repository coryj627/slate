// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite through the held `.slate` descriptor: plan 43 PR 22a (#940; design
//! `docs/plans/43_mac_backlog_delivery/22_descriptor_relative_session_design.md`
//! §3.2, §4.1, owner decisions Q1 and Q2).
//!
//! SQLite resolves its database path itself, so the descriptor-relative
//! interior reaches it through the unix VFS's own system-call hook
//! (`sqlite3_vfs.xSetSystemCall`), not through `/dev/fd/<rootfd>/…`, which
//! is not traversable for a directory on macOS (§3.1). Once per process
//! [`install`] overrides the unix VFS's `open`, `stat`, `lstat`, `access`,
//! `unlink` and `readlink`. A path of the form `/.slate-fd/<token>/<leaf>`
//! resolves `<leaf>` with `openat`, `fstatat`, `faccessat` or `unlinkat`
//! against the directory registered under `<token>` ([`SqliteAnchor`]), and
//! `/.slate-fd/<token>` itself is that directory (SQLite opens a database's
//! directory to sync it after a WAL commit). Every other path goes to the
//! call SQLite had before, untouched, so an in-memory database or any other
//! file keeps its ordinary behaviour.
//!
//! The `-wal`, `-shm` and `-journal` siblings are named by appending to the
//! database path, so they resolve through the same descriptor; a second
//! connection on the same synthetic path shares the database; and another
//! process reaching the same file by its real path shares it too, because
//! SQLite's locks and its WAL index are per file, not per name (§3.2, §3.3).
//!
//! Hardening, beyond the spike: every anchored open adds `O_NOFOLLOW`, the
//! `stat`s do not follow, `readlink` of an anchored name fails (so SQLite's
//! own symlink resolution can never leave the held directory), and an
//! unknown token is `ENOENT`, never a pathname. On macOS anchored connections
//! use the `unix-posix` variant of the unix VFS: the default `unix` variant
//! chooses its locking by `statfs` of the path, which is not an overridable
//! call and cannot see the synthetic path (it would read an uninitialized
//! result); `unix-posix` is the POSIX locking it picks for APFS and HFS+
//! anyway, and shares this hook table.
//!
//! The hook is process-global (owner decision Q2): one `OnceLock` installs
//! it, and per-session state is the token table.

use std::collections::HashMap;
use std::ffi::{CStr, c_char, c_int};
use std::os::fd::AsRawFd as _;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, OnceLock, PoisonError, RwLock};
use std::{fs, io};

use rusqlite::ffi;

/// The synthetic prefix. Nothing at this path on disk is ever consulted:
/// the hook answers for it.
const PREFIX: &[u8] = b"/.slate-fd";

/// The unix VFS variant anchored connections open through.
#[cfg(target_os = "macos")]
pub(crate) const VFS: Option<&str> = Some("unix-posix");
#[cfg(not(target_os = "macos"))]
pub(crate) const VFS: Option<&str> = None;

type OpenFn = unsafe extern "C" fn(*const c_char, c_int, c_int) -> c_int;
type StatFn = unsafe extern "C" fn(*const c_char, *mut libc::stat) -> c_int;
type AccessFn = unsafe extern "C" fn(*const c_char, c_int) -> c_int;
type UnlinkFn = unsafe extern "C" fn(*const c_char) -> c_int;
type ReadlinkFn = unsafe extern "C" fn(*const c_char, *mut c_char, libc::size_t) -> libc::ssize_t;

/// The calls SQLite made before the hook, for every path that is not ours.
struct Previous {
    open: OpenFn,
    stat: StatFn,
    lstat: StatFn,
    access: AccessFn,
    unlink: UnlinkFn,
    readlink: Option<ReadlinkFn>,
}

static PREVIOUS: OnceLock<Previous> = OnceLock::new();
static INSTALLED: OnceLock<Result<(), String>> = OnceLock::new();
static NEXT_TOKEN: AtomicU64 = AtomicU64::new(1);

/// A registered directory: a duplicate of the session's held `.slate`
/// descriptor, so a call in flight keeps it open even while its anchor is
/// being retired.
struct Registered {
    dir: fs::File,
    display: PathBuf,
}

fn registry() -> &'static RwLock<HashMap<u64, Arc<Registered>>> {
    static REGISTRY: OnceLock<RwLock<HashMap<u64, Arc<Registered>>>> = OnceLock::new();
    REGISTRY.get_or_init(|| RwLock::new(HashMap::new()))
}

fn lookup(token: u64) -> Option<Arc<Registered>> {
    registry()
        .read()
        .unwrap_or_else(PoisonError::into_inner)
        .get(&token)
        .cloned()
}

/// One session's registration of its `.slate` with the hook. Dropping it
/// retires the token: a later call through it is `ENOENT`, never another
/// directory, because tokens are never reused.
#[derive(Debug)]
pub(crate) struct SqliteAnchor {
    token: u64,
}

impl SqliteAnchor {
    /// Register `dir` (the held `.slate`, spelled `display` in messages),
    /// installing the hook first if this is the process's first anchor.
    pub(crate) fn register(dir: &fs::File, display: &Path) -> io::Result<Self> {
        install().map_err(io::Error::other)?;
        let registered = Arc::new(Registered {
            dir: dir.try_clone()?,
            display: display.to_path_buf(),
        });
        let token = NEXT_TOKEN.fetch_add(1, Ordering::Relaxed);
        registry()
            .write()
            .unwrap_or_else(PoisonError::into_inner)
            .insert(token, registered);
        Ok(Self { token })
    }

    /// The synthetic path SQLite opens `leaf` of the registered directory by.
    pub(crate) fn database_path(&self, leaf: &str) -> PathBuf {
        PathBuf::from(format!(
            "{}/{}/{leaf}",
            String::from_utf8_lossy(PREFIX),
            self.token
        ))
    }
}

impl Drop for SqliteAnchor {
    fn drop(&mut self) {
        registry()
            .write()
            .unwrap_or_else(PoisonError::into_inner)
            .remove(&self.token);
    }
}

/// The real-path spelling a synthetic SQLite path stands for (design §4.2):
/// a diagnostic that quotes `sqlite3_db_filename` maps it back before a
/// person reads it. Any other path is returned as it is.
pub(crate) fn display_path(path: &Path) -> PathBuf {
    use std::os::unix::ffi::OsStrExt as _;
    let bytes = path.as_os_str().as_bytes();
    if let Some((token, leaf)) = split_synthetic(bytes)
        && let Some(registered) = lookup(token)
    {
        return match leaf {
            Some(leaf) => registered.display.join(std::ffi::OsStr::from_bytes(leaf)),
            None => registered.display.clone(),
        };
    }
    path.to_path_buf()
}

/// Whether `path` is an anchored synthetic path (registered or not).
pub(crate) fn is_synthetic(path: &Path) -> bool {
    use std::os::unix::ffi::OsStrExt as _;
    split_synthetic(path.as_os_str().as_bytes()).is_some()
}

/// `(token, leaf)` for `/.slate-fd/<token>[/<leaf>]`, `None` for anything
/// else, including a leaf that is not one plain component.
fn split_synthetic(bytes: &[u8]) -> Option<(u64, Option<&[u8]>)> {
    let rest = bytes.strip_prefix(PREFIX)?.strip_prefix(b"/")?;
    let (token, leaf) = match rest.iter().position(|byte| *byte == b'/') {
        None => (rest, None),
        Some(slash) => (&rest[..slash], Some(&rest[slash + 1..])),
    };
    if token.is_empty() || !token.iter().all(u8::is_ascii_digit) {
        return None;
    }
    let token = std::str::from_utf8(token).ok()?.parse().ok()?;
    match leaf {
        Some(leaf) if !is_plain_component(leaf) => None,
        leaf => Some((token, leaf)),
    }
}

fn is_plain_component(leaf: &[u8]) -> bool {
    !leaf.is_empty() && leaf != b"." && leaf != b".." && !leaf.contains(&b'/')
}

/// What a path the hook sees is.
enum Target {
    /// Not ours: the previous call handles it.
    Elsewhere,
    /// Ours, but no live registration answers it (the bare prefix, a
    /// retired or unknown token, a leaf that is not one component): `ENOENT`.
    Missing,
    /// The registered directory itself.
    Dir(Arc<Registered>),
    /// One name in the registered directory; the pointer is the leaf's
    /// first byte inside the caller's NUL-terminated path.
    Leaf(Arc<Registered>, *const c_char),
}

/// # Safety
/// `path` is null or a NUL-terminated string that outlives the returned
/// target (SQLite's own argument, valid for the call).
unsafe fn classify(path: *const c_char) -> Target {
    if path.is_null() {
        return Target::Elsewhere;
    }
    // SAFETY: the caller's contract.
    let bytes = unsafe { CStr::from_ptr(path) }.to_bytes();
    let Some(rest) = bytes.strip_prefix(PREFIX) else {
        return Target::Elsewhere;
    };
    if !rest.is_empty() && !rest.starts_with(b"/") {
        // `/.slate-fdX…` is somebody's real path.
        return Target::Elsewhere;
    }
    let Some((token, leaf)) = split_synthetic(bytes) else {
        return Target::Missing;
    };
    let Some(registered) = lookup(token) else {
        return Target::Missing;
    };
    match leaf {
        None => Target::Dir(registered),
        Some(leaf) => {
            let offset = bytes.len() - leaf.len();
            // SAFETY: `offset` is within the string, and the leaf runs to
            // its NUL terminator.
            Target::Leaf(registered, unsafe { path.add(offset) })
        }
    }
}

fn fail(errno: c_int) -> c_int {
    crate::vault::fs_root::sys::set_errno(errno);
    -1
}

/// The `open` override: `openat` beneath the registered directory, never
/// following a link.
unsafe extern "C" fn anchored_open(path: *const c_char, flags: c_int, mode: c_int) -> c_int {
    // SAFETY: SQLite passes a NUL-terminated path valid for the call.
    match unsafe { classify(path) } {
        Target::Elsewhere => match PREVIOUS.get() {
            // SAFETY: the previous call SQLite itself installed, with its
            // own arguments.
            Some(previous) => unsafe { (previous.open)(path, flags, mode) },
            None => fail(libc::ENOSYS),
        },
        Target::Missing => fail(libc::ENOENT),
        Target::Dir(registered) => {
            if flags & (libc::O_WRONLY | libc::O_RDWR | libc::O_CREAT) != 0 {
                return fail(libc::EISDIR);
            }
            // SAFETY: the registered descriptor is open (the `Arc` holds
            // it) and "." is NUL-terminated.
            unsafe {
                libc::openat(
                    registered.dir.as_raw_fd(),
                    c".".as_ptr(),
                    flags | libc::O_DIRECTORY | libc::O_CLOEXEC,
                )
            }
        }
        // SAFETY: as above; `leaf` points into SQLite's string.
        Target::Leaf(registered, leaf) => unsafe {
            libc::openat(
                registered.dir.as_raw_fd(),
                leaf,
                flags | crate::vault::fs_root::sys::NO_FOLLOW | libc::O_CLOEXEC,
                mode as libc::c_uint,
            )
        },
    }
}

/// `stat` and `lstat` beneath the registered directory: neither follows a
/// final link, so a linked database or sibling reports itself as a link
/// and SQLite refuses it.
unsafe fn anchored_stat_with(
    path: *const c_char,
    buf: *mut libc::stat,
    previous: fn(&Previous) -> StatFn,
) -> c_int {
    // SAFETY: SQLite passes a NUL-terminated path and a writable buffer of
    // its own `struct stat`, valid for the call (the layouts match on the
    // 64-bit targets this module builds for).
    match unsafe { classify(path) } {
        Target::Elsewhere => match PREVIOUS.get() {
            Some(previous_calls) => unsafe { previous(previous_calls)(path, buf) },
            None => fail(libc::ENOSYS),
        },
        Target::Missing => fail(libc::ENOENT),
        Target::Dir(registered) => unsafe { libc::fstat(registered.dir.as_raw_fd(), buf) },
        Target::Leaf(registered, leaf) => unsafe {
            libc::fstatat(
                registered.dir.as_raw_fd(),
                leaf,
                buf,
                libc::AT_SYMLINK_NOFOLLOW,
            )
        },
    }
}

unsafe extern "C" fn anchored_stat(path: *const c_char, buf: *mut libc::stat) -> c_int {
    // SAFETY: forwarded unchanged from SQLite.
    unsafe { anchored_stat_with(path, buf, |previous| previous.stat) }
}

unsafe extern "C" fn anchored_lstat(path: *const c_char, buf: *mut libc::stat) -> c_int {
    // SAFETY: forwarded unchanged from SQLite.
    unsafe { anchored_stat_with(path, buf, |previous| previous.lstat) }
}

/// `access`: a name that is a link is `ELOOP` (it would be refused at the
/// open anyway); otherwise `faccessat` beneath the directory.
unsafe extern "C" fn anchored_access(path: *const c_char, mode: c_int) -> c_int {
    // SAFETY: SQLite passes a NUL-terminated path valid for the call.
    match unsafe { classify(path) } {
        Target::Elsewhere => match PREVIOUS.get() {
            Some(previous) => unsafe { (previous.access)(path, mode) },
            None => fail(libc::ENOSYS),
        },
        Target::Missing => fail(libc::ENOENT),
        Target::Dir(registered) => unsafe {
            libc::faccessat(registered.dir.as_raw_fd(), c".".as_ptr(), mode, 0)
        },
        Target::Leaf(registered, leaf) => {
            let mut stat = std::mem::MaybeUninit::<libc::stat>::uninit();
            // SAFETY: the directory is open, `leaf` is NUL-terminated and
            // `stat` is writable storage of the right type.
            let looked = unsafe {
                libc::fstatat(
                    registered.dir.as_raw_fd(),
                    leaf,
                    stat.as_mut_ptr(),
                    libc::AT_SYMLINK_NOFOLLOW,
                )
            };
            if looked != 0 {
                return -1;
            }
            // SAFETY: a successful fstatat initialized `stat`.
            if unsafe { stat.assume_init() }.st_mode & libc::S_IFMT == libc::S_IFLNK {
                return fail(libc::ELOOP);
            }
            if mode == libc::F_OK {
                return 0;
            }
            // SAFETY: as above.
            unsafe { libc::faccessat(registered.dir.as_raw_fd(), leaf, mode, 0) }
        }
    }
}

/// `unlink`: `unlinkat` beneath the directory; the directory itself is
/// never unlinked.
unsafe extern "C" fn anchored_unlink(path: *const c_char) -> c_int {
    // SAFETY: SQLite passes a NUL-terminated path valid for the call.
    match unsafe { classify(path) } {
        Target::Elsewhere => match PREVIOUS.get() {
            Some(previous) => unsafe { (previous.unlink)(path) },
            None => fail(libc::ENOSYS),
        },
        Target::Missing => fail(libc::ENOENT),
        Target::Dir(_) => fail(libc::EPERM),
        Target::Leaf(registered, leaf) => unsafe {
            libc::unlinkat(registered.dir.as_raw_fd(), leaf, 0)
        },
    }
}

/// `readlink`: SQLite resolves a link in a database path by reading it;
/// an anchored name never resolves anywhere else.
unsafe extern "C" fn anchored_readlink(
    path: *const c_char,
    buf: *mut c_char,
    size: libc::size_t,
) -> libc::ssize_t {
    // SAFETY: SQLite passes a NUL-terminated path and a buffer of `size`
    // bytes, valid for the call.
    match unsafe { classify(path) } {
        Target::Elsewhere => match PREVIOUS.get().and_then(|previous| previous.readlink) {
            Some(readlink) => unsafe { readlink(path, buf, size) },
            None => fail(libc::ENOSYS) as libc::ssize_t,
        },
        Target::Missing | Target::Dir(_) | Target::Leaf(..) => fail(libc::EINVAL) as libc::ssize_t,
    }
}

/// Install the overrides on the unix VFS, once per process. Fails, and
/// every anchored open with it, when the VFS cannot be hooked: the
/// interior never falls back to a pathname silently.
pub(crate) fn install() -> Result<(), String> {
    INSTALLED.get_or_init(install_once).clone()
}

fn install_once() -> Result<(), String> {
    // SAFETY: `sqlite3_vfs_find` initializes SQLite if needed and returns
    // the default VFS, a static object, or null.
    let vfs = unsafe { ffi::sqlite3_vfs_find(std::ptr::null()) };
    if vfs.is_null() {
        return Err("SQLite has no default VFS".into());
    }
    // SAFETY: a non-null VFS from SQLite is a valid, static `sqlite3_vfs`.
    let (version, get, set) = unsafe {
        (
            (*vfs).iVersion,
            (*vfs).xGetSystemCall,
            (*vfs).xSetSystemCall,
        )
    };
    let (Some(get), Some(set)) = (get, set) else {
        return Err("the default SQLite VFS has no system-call interface".into());
    };
    if version < 3 {
        return Err(format!(
            "the default SQLite VFS is version {version}, not 3"
        ));
    }
    let previous = |name: &CStr| -> Result<ffi::sqlite3_syscall_ptr, String> {
        // SAFETY: `vfs` is valid and `name` is NUL-terminated.
        let current = unsafe { get(vfs, name.as_ptr()) };
        if current.is_none() && name != c"readlink" {
            return Err(format!("SQLite's unix VFS has no {name:?} call"));
        }
        Ok(current)
    };
    // SAFETY: each pointer SQLite returns is the function its table holds
    // for that name, whose real signature is the one transmuted to (see
    // `os_unix.c`'s `aSyscall`: `posixOpen` for "open", libc's own `stat`,
    // `lstat`, `access`, `unlink` and `readlink`).
    let saved = unsafe {
        Previous {
            open: std::mem::transmute::<unsafe extern "C" fn(), OpenFn>(
                previous(c"open")?.expect("checked"),
            ),
            stat: std::mem::transmute::<unsafe extern "C" fn(), StatFn>(
                previous(c"stat")?.expect("checked"),
            ),
            lstat: std::mem::transmute::<unsafe extern "C" fn(), StatFn>(
                previous(c"lstat")?.expect("checked"),
            ),
            access: std::mem::transmute::<unsafe extern "C" fn(), AccessFn>(
                previous(c"access")?.expect("checked"),
            ),
            unlink: std::mem::transmute::<unsafe extern "C" fn(), UnlinkFn>(
                previous(c"unlink")?.expect("checked"),
            ),
            readlink: previous(c"readlink")?.map(|readlink| {
                std::mem::transmute::<unsafe extern "C" fn(), ReadlinkFn>(readlink)
            }),
        }
    };
    // Saved before any override is live, so a path that is not ours always
    // has somewhere to go.
    if PREVIOUS.set(saved).is_err() {
        return Err("the SQLite hook was installed twice".into());
    }
    // SAFETY: each override has exactly the signature SQLite calls through
    // that slot; erasing it to `sqlite3_syscall_ptr` is how the interface
    // takes it.
    let overrides: [(&CStr, ffi::sqlite3_syscall_ptr); 6] = unsafe {
        [
            (
                c"open",
                Some(std::mem::transmute::<OpenFn, unsafe extern "C" fn()>(
                    anchored_open,
                )),
            ),
            (
                c"stat",
                Some(std::mem::transmute::<StatFn, unsafe extern "C" fn()>(
                    anchored_stat,
                )),
            ),
            (
                c"lstat",
                Some(std::mem::transmute::<StatFn, unsafe extern "C" fn()>(
                    anchored_lstat,
                )),
            ),
            (
                c"access",
                Some(std::mem::transmute::<AccessFn, unsafe extern "C" fn()>(
                    anchored_access,
                )),
            ),
            (
                c"unlink",
                Some(std::mem::transmute::<UnlinkFn, unsafe extern "C" fn()>(
                    anchored_unlink,
                )),
            ),
            (
                c"readlink",
                Some(std::mem::transmute::<ReadlinkFn, unsafe extern "C" fn()>(
                    anchored_readlink,
                )),
            ),
        ]
    };
    for (name, call) in overrides {
        // SAFETY: `vfs` is valid, `name` is NUL-terminated and `call` has
        // the slot's signature. SQLite documents the interface as not
        // thread-safe; it runs once, behind `INSTALLED`, before any
        // anchored connection exists, and an aligned pointer store is
        // atomic on every target this module builds for, so a connection
        // elsewhere in the process sees the old call or the new one, which
        // agree on every path that is not ours.
        let rc = unsafe { set(vfs, name.as_ptr(), call) };
        if rc != ffi::SQLITE_OK {
            return Err(format!("SQLite refused the {name:?} override ({rc})"));
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn only_a_plain_leaf_under_a_numeric_token_is_synthetic() {
        assert_eq!(
            split_synthetic(b"/.slate-fd/7/cache.sqlite"),
            Some((7, Some(&b"cache.sqlite"[..])))
        );
        assert_eq!(split_synthetic(b"/.slate-fd/7"), Some((7, None)));
        for not_ours in [
            &b"/.slate-fd"[..],
            b"/.slate-fd/",
            b"/.slate-fdx/7/cache.sqlite",
            b"/.slate-fd/x7/cache.sqlite",
            b"/.slate-fd/7/",
            b"/.slate-fd/7/..",
            b"/.slate-fd/7/a/b",
            b"/tmp/.slate-fd/7/cache.sqlite",
        ] {
            assert_eq!(split_synthetic(not_ours), None, "{not_ours:?}");
        }
    }

    /// The overrides pass every path that is not ours to the call SQLite
    /// had before, and answer `ENOENT` for a retired or unknown token, never
    /// a pathname lookup.
    #[test]
    fn a_path_that_is_not_ours_is_untouched_and_an_unknown_token_is_missing() {
        install().unwrap();
        let tmp = tempfile::tempdir().unwrap();
        let file = tmp.path().join("plain.txt");
        std::fs::write(&file, b"x").unwrap();
        let c_file = std::ffi::CString::new(file.to_str().unwrap()).unwrap();
        let mut stat = std::mem::MaybeUninit::<libc::stat>::uninit();
        // SAFETY: a NUL-terminated path and writable stat storage.
        assert_eq!(
            unsafe { anchored_stat(c_file.as_ptr(), stat.as_mut_ptr()) },
            0
        );
        // SAFETY: as above.
        assert_eq!(unsafe { anchored_access(c_file.as_ptr(), libc::F_OK) }, 0);

        let anchor =
            SqliteAnchor::register(&std::fs::File::open(tmp.path()).unwrap(), tmp.path()).unwrap();
        let synthetic = anchor.database_path("plain.txt");
        assert_eq!(display_path(&synthetic), file, "a diagnostic maps it back");
        let c_synthetic = std::ffi::CString::new(synthetic.to_str().unwrap()).unwrap();
        // SAFETY: as above.
        assert_eq!(
            unsafe { anchored_access(c_synthetic.as_ptr(), libc::F_OK) },
            0
        );
        drop(anchor);
        // SAFETY: as above.
        assert_eq!(
            unsafe { anchored_access(c_synthetic.as_ptr(), libc::F_OK) },
            -1
        );
        assert_eq!(
            io::Error::last_os_error().raw_os_error(),
            Some(libc::ENOENT),
            "a retired token is missing, not a pathname"
        );
        assert_eq!(display_path(&synthetic), synthetic);
    }
}
