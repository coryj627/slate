// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite through the held `.slate` handle on Windows: plan 44 WB-28 (22W;
//! design `docs/plans/44_windows_backlog/specs/wb28_descriptor_relative_session_on_windows.md`
//! item 3), the twin of the unix body in `anchored.rs`.
//!
//! SQLite's win32 VFS resolves its database path itself, through the calls
//! in its system-call table (`os_win.c`'s `aSyscall`), which
//! `sqlite3_vfs.xSetSystemCall` can replace. Once per process [`install`]
//! overrides `CreateFileW`, `GetFileAttributesW`, `GetFileAttributesExW`
//! and `DeleteFileW`, the four calls that take a path. A path of the form
//! `C:\|slate-fd\<token>\<leaf>` resolves `<leaf>` with `NtCreateFile`
//! relative to the directory handle registered under `<token>`
//! ([`SqliteAnchor`]), and `C:\|slate-fd\<token>` is that directory. Every
//! other path goes to the call SQLite had before, untouched.
//!
//! **The prefix.** `|` is not allowed in a Windows file name, so no real
//! path is ever this one, and without the hook every call on it fails with
//! `ERROR_INVALID_NAME` instead of reaching a folder. It is absolute, so
//! `GetFullPathNameW` (not overridden) returns it as it is, and SQLite opens
//! the path it was given. It does not start with `\\`: SQLite takes such a
//! path for a network share and shares one lock handle among a process's
//! connections (`winIsUNCPath`, `bUseSharedLockHandle`), which failed
//! racing first writes with `SQLITE_PROTOCOL` when the spike used
//! `\\.slate-fd\…`.
//!
//! The `-wal`, `-shm` and `-journal` siblings are named by appending to
//! that path, so they resolve through the same handle. `CreateFileMappingW` (the WAL index) takes the
//! `-shm` file's handle, not a path. SQLite opens no directory on Windows.
//!
//! Every anchored open adds `FILE_OPEN_REPARSE_POINT` and refuses an entry
//! that is a reparse point, so a linked database or sibling is never opened
//! through; an unknown or retired token is `ERROR_PATH_NOT_FOUND`, never a
//! pathname.

use std::collections::HashMap;
use std::ffi::{OsString, c_void};
use std::os::windows::ffi::{OsStrExt as _, OsStringExt as _};
use std::os::windows::fs::MetadataExt as _;
use std::os::windows::io::{AsRawHandle as _, IntoRawHandle as _};
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, OnceLock, PoisonError, RwLock};
use std::{fs, io};

use rusqlite::ffi;
use windows_sys::Win32::Foundation::{
    ERROR_ACCESS_DENIED, ERROR_CANT_ACCESS_FILE, ERROR_INVALID_PARAMETER, ERROR_PATH_NOT_FOUND,
    HANDLE, INVALID_HANDLE_VALUE, SetLastError,
};
use windows_sys::Win32::Storage::FileSystem::{
    BY_HANDLE_FILE_INFORMATION, CREATE_ALWAYS, CREATE_NEW, DELETE, FILE_ATTRIBUTE_REPARSE_POINT,
    FILE_FLAG_DELETE_ON_CLOSE, FILE_READ_ATTRIBUTES, FILE_SHARE_DELETE, FILE_SHARE_READ,
    FILE_SHARE_WRITE, GetFileExInfoStandard, GetFileInformationByHandle, INVALID_FILE_ATTRIBUTES,
    OPEN_ALWAYS, OPEN_EXISTING, TRUNCATE_EXISTING, WIN32_FILE_ATTRIBUTE_DATA,
};

/// The synthetic prefix, `C:\|slate-fd`. No file system has this path: the
/// hook answers for it.
const PREFIX: &str = r"C:\|slate-fd";

/// The VFS anchored connections open through: SQLite's default, `win32`,
/// whose table the hook changes.
pub(crate) const VFS: Option<&str> = None;

type CreateFileFn =
    unsafe extern "system" fn(*const u16, u32, u32, *const c_void, u32, u32, HANDLE) -> HANDLE;
type GetAttributesFn = unsafe extern "system" fn(*const u16) -> u32;
type GetAttributesExFn = unsafe extern "system" fn(*const u16, i32, *mut c_void) -> i32;
type DeleteFileFn = unsafe extern "system" fn(*const u16) -> i32;

/// The calls SQLite made before the hook, for every path that is not ours.
struct Previous {
    create_file: CreateFileFn,
    get_attributes: GetAttributesFn,
    get_attributes_ex: GetAttributesExFn,
    delete_file: DeleteFileFn,
}

static PREVIOUS: OnceLock<Previous> = OnceLock::new();
static INSTALLED: OnceLock<Result<(), String>> = OnceLock::new();
static NEXT_TOKEN: AtomicU64 = AtomicU64::new(1);

/// A registered directory: a duplicate of the session's held `.slate`
/// handle, so a call in flight keeps it open even while its anchor is being
/// retired.
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
/// retires the token: a later call through it fails, never reaching another
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
        PathBuf::from(format!(r"{PREFIX}\{}\{leaf}", self.token))
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

/// The real-path spelling a synthetic SQLite path stands for: a diagnostic
/// that quotes `sqlite3_db_filename` maps it back before a person reads it.
/// Any other path is returned as it is.
pub(crate) fn display_path(path: &Path) -> PathBuf {
    let wide: Vec<u16> = path.as_os_str().encode_wide().collect();
    if let Some((token, leaf)) = split_synthetic(&wide)
        && let Some(registered) = lookup(token)
    {
        return match leaf {
            Some(leaf) => registered.display.join(OsString::from_wide(leaf)),
            None => registered.display.clone(),
        };
    }
    path.to_path_buf()
}

/// Whether `path` is an anchored synthetic path (registered or not).
pub(crate) fn is_synthetic(path: &Path) -> bool {
    let wide: Vec<u16> = path.as_os_str().encode_wide().collect();
    split_synthetic(&wide).is_some()
}

fn prefix() -> &'static [u16] {
    static WIDE: OnceLock<Vec<u16>> = OnceLock::new();
    WIDE.get_or_init(|| PREFIX.encode_utf16().collect())
}

const BACKSLASH: u16 = b'\\' as u16;

/// `(token, leaf)` for `C:\|slate-fd\<token>[\<leaf>]`, `None` for anything
/// else, including a leaf that is not one plain component.
fn split_synthetic(wide: &[u16]) -> Option<(u64, Option<&[u16]>)> {
    let rest = wide.strip_prefix(prefix())?.strip_prefix(&[BACKSLASH])?;
    let (token, leaf) = match rest.iter().position(|unit| *unit == BACKSLASH) {
        None => (rest, None),
        Some(slash) => (&rest[..slash], Some(&rest[slash + 1..])),
    };
    if token.is_empty()
        || !token
            .iter()
            .all(|unit| (b'0' as u16..=b'9' as u16).contains(unit))
    {
        return None;
    }
    let token = String::from_utf16(token).ok()?.parse().ok()?;
    match leaf {
        Some(leaf) if !is_plain_component(leaf) => None,
        leaf => Some((token, leaf)),
    }
}

fn is_plain_component(leaf: &[u16]) -> bool {
    let dot = b'.' as u16;
    !leaf.is_empty()
        && leaf != [dot]
        && leaf != [dot, dot]
        && !leaf.iter().any(|unit| matches!(*unit, 0 | 47 | 58 | 92))
}

/// What a path the hook sees is.
enum Target {
    /// Not ours: the previous call handles it.
    Elsewhere,
    /// Ours, but no live registration answers it.
    Missing,
    /// The registered directory itself.
    Dir(Arc<Registered>),
    /// One name in the registered directory.
    Leaf(Arc<Registered>, OsString),
}

/// # Safety
/// `path` is null or a NUL-terminated UTF-16 string valid for the call
/// (SQLite's own argument).
unsafe fn classify(path: *const u16) -> Target {
    if path.is_null() {
        return Target::Elsewhere;
    }
    // SAFETY: the caller's contract: the string runs to its NUL.
    let wide = unsafe {
        let mut length = 0;
        while *path.add(length) != 0 {
            length += 1;
        }
        std::slice::from_raw_parts(path, length)
    };
    let Some(rest) = wide.strip_prefix(prefix()) else {
        return Target::Elsewhere;
    };
    if !rest.is_empty() && rest[0] != BACKSLASH {
        // `C:\|slate-fdX\…` is not ours.
        return Target::Elsewhere;
    }
    let Some((token, leaf)) = split_synthetic(wide) else {
        return Target::Missing;
    };
    let Some(registered) = lookup(token) else {
        return Target::Missing;
    };
    match leaf {
        None => Target::Dir(registered),
        Some(leaf) => Target::Leaf(registered, OsString::from_wide(leaf)),
    }
}

fn set_last_error(code: u32) {
    // SAFETY: sets this thread's last-error value; no other effect.
    unsafe { SetLastError(code) };
}

fn set_last_error_from(error: &io::Error) {
    set_last_error(
        error
            .raw_os_error()
            .map_or(ERROR_ACCESS_DENIED, |code| code as u32),
    );
}

/// Open `leaf` beneath the registered directory, never through a reparse
/// point: the entry itself opens, and one that is a reparse point is
/// refused with `ERROR_CANT_ACCESS_FILE`.
fn open_leaf(
    registered: &Registered,
    leaf: &OsString,
    access: u32,
    share: u32,
    disposition: u32,
    attributes: u32,
) -> io::Result<fs::File> {
    let file = crate::vault::sqlite_create_relative(
        &registered.dir,
        leaf,
        access,
        share,
        disposition,
        attributes,
    )?;
    if file.metadata()?.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0 {
        return Err(io::Error::from_raw_os_error(ERROR_CANT_ACCESS_FILE as i32));
    }
    Ok(file)
}

/// The `CreateFileW` override.
unsafe extern "system" fn anchored_create_file(
    path: *const u16,
    access: u32,
    share: u32,
    security: *const c_void,
    disposition: u32,
    flags_and_attributes: u32,
    template: HANDLE,
) -> HANDLE {
    // SAFETY: SQLite passes a NUL-terminated path valid for the call.
    match unsafe { classify(path) } {
        Target::Elsewhere => match PREVIOUS.get() {
            // SAFETY: the previous call SQLite itself installed, with its
            // own arguments.
            Some(previous) => unsafe {
                (previous.create_file)(
                    path,
                    access,
                    share,
                    security,
                    disposition,
                    flags_and_attributes,
                    template,
                )
            },
            None => {
                set_last_error(ERROR_INVALID_PARAMETER);
                INVALID_HANDLE_VALUE
            }
        },
        Target::Missing => {
            set_last_error(ERROR_PATH_NOT_FOUND);
            INVALID_HANDLE_VALUE
        }
        // SQLite opens no directory on Windows; nothing here writes one.
        Target::Dir(_) => {
            set_last_error(ERROR_ACCESS_DENIED);
            INVALID_HANDLE_VALUE
        }
        Target::Leaf(registered, leaf) => {
            if !matches!(
                disposition,
                CREATE_NEW | CREATE_ALWAYS | OPEN_EXISTING | OPEN_ALWAYS | TRUNCATE_EXISTING
            ) {
                set_last_error(ERROR_INVALID_PARAMETER);
                return INVALID_HANDLE_VALUE;
            }
            match open_leaf(
                &registered,
                &leaf,
                access,
                share,
                disposition,
                flags_and_attributes,
            ) {
                Ok(file) => file.into_raw_handle(),
                Err(error) => {
                    set_last_error_from(&error);
                    INVALID_HANDLE_VALUE
                }
            }
        }
    }
}

/// The attributes of the entry `target` names, opened without following it.
fn attributes_of(target: &Target) -> io::Result<BY_HANDLE_FILE_INFORMATION> {
    let read = |file: &fs::File| {
        // SAFETY: zeroed storage of the type the call fills.
        let mut info: BY_HANDLE_FILE_INFORMATION = unsafe { std::mem::zeroed() };
        // SAFETY: a live handle and writable storage of the right type.
        if unsafe { GetFileInformationByHandle(file.as_raw_handle(), &mut info) } == 0 {
            return Err(io::Error::last_os_error());
        }
        Ok(info)
    };
    match target {
        Target::Dir(registered) => read(&registered.dir),
        Target::Leaf(registered, leaf) => read(&crate::vault::sqlite_create_relative(
            &registered.dir,
            leaf,
            FILE_READ_ATTRIBUTES,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            OPEN_EXISTING,
            0,
        )?),
        Target::Elsewhere | Target::Missing => {
            Err(io::Error::from_raw_os_error(ERROR_PATH_NOT_FOUND as i32))
        }
    }
}

/// The `GetFileAttributesW` override (SQLite's delete checks with it).
unsafe extern "system" fn anchored_get_attributes(path: *const u16) -> u32 {
    // SAFETY: SQLite passes a NUL-terminated path valid for the call.
    let target = unsafe { classify(path) };
    if let Target::Elsewhere = target {
        return match PREVIOUS.get() {
            // SAFETY: SQLite's own previous call, with its own argument.
            Some(previous) => unsafe { (previous.get_attributes)(path) },
            None => {
                set_last_error(ERROR_INVALID_PARAMETER);
                INVALID_FILE_ATTRIBUTES
            }
        };
    }
    match attributes_of(&target) {
        Ok(info) => info.dwFileAttributes,
        Err(error) => {
            set_last_error_from(&error);
            INVALID_FILE_ATTRIBUTES
        }
    }
}

/// The `GetFileAttributesExW` override (SQLite's directory and access
/// checks); only `GetFileExInfoStandard` is answered for an anchored path.
unsafe extern "system" fn anchored_get_attributes_ex(
    path: *const u16,
    level: i32,
    out: *mut c_void,
) -> i32 {
    // SAFETY: SQLite passes a NUL-terminated path valid for the call.
    let target = unsafe { classify(path) };
    if let Target::Elsewhere = target {
        return match PREVIOUS.get() {
            // SAFETY: SQLite's own previous call, with its own arguments.
            Some(previous) => unsafe { (previous.get_attributes_ex)(path, level, out) },
            None => {
                set_last_error(ERROR_INVALID_PARAMETER);
                0
            }
        };
    }
    if level != GetFileExInfoStandard || out.is_null() {
        set_last_error(ERROR_INVALID_PARAMETER);
        return 0;
    }
    match attributes_of(&target) {
        Ok(info) => {
            let data = WIN32_FILE_ATTRIBUTE_DATA {
                dwFileAttributes: info.dwFileAttributes,
                ftCreationTime: info.ftCreationTime,
                ftLastAccessTime: info.ftLastAccessTime,
                ftLastWriteTime: info.ftLastWriteTime,
                nFileSizeHigh: info.nFileSizeHigh,
                nFileSizeLow: info.nFileSizeLow,
            };
            // SAFETY: for `GetFileExInfoStandard` the caller's buffer is a
            // `WIN32_FILE_ATTRIBUTE_DATA`.
            unsafe { out.cast::<WIN32_FILE_ATTRIBUTE_DATA>().write(data) };
            1
        }
        Err(error) => {
            set_last_error_from(&error);
            0
        }
    }
}

/// The `DeleteFileW` override: the entry is opened beneath the directory,
/// as itself, and deleted when that handle closes. The directory itself is
/// never deleted.
unsafe extern "system" fn anchored_delete_file(path: *const u16) -> i32 {
    // SAFETY: SQLite passes a NUL-terminated path valid for the call.
    match unsafe { classify(path) } {
        Target::Elsewhere => match PREVIOUS.get() {
            // SAFETY: SQLite's own previous call, with its own argument.
            Some(previous) => unsafe { (previous.delete_file)(path) },
            None => {
                set_last_error(ERROR_INVALID_PARAMETER);
                0
            }
        },
        Target::Missing => {
            set_last_error(ERROR_PATH_NOT_FOUND);
            0
        }
        Target::Dir(_) => {
            set_last_error(ERROR_ACCESS_DENIED);
            0
        }
        Target::Leaf(registered, leaf) => match crate::vault::sqlite_create_relative(
            &registered.dir,
            &leaf,
            DELETE | FILE_READ_ATTRIBUTES,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            OPEN_EXISTING,
            FILE_FLAG_DELETE_ON_CLOSE,
        ) {
            Ok(file) => {
                drop(file);
                1
            }
            Err(error) => {
                set_last_error_from(&error);
                0
            }
        },
    }
}

/// Install the overrides on the win32 VFS, once per process. Fails, and
/// every anchored open with it, when the VFS cannot be hooked: the interior
/// never falls back to a pathname silently.
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
    let previous = |name: &std::ffi::CStr| -> Result<unsafe extern "C" fn(), String> {
        // SAFETY: `vfs` is valid and `name` is NUL-terminated.
        unsafe { get(vfs, name.as_ptr()) }
            .ok_or_else(|| format!("SQLite's win32 VFS has no {name:?} call"))
    };
    // SAFETY: each pointer SQLite returns is the function its table holds
    // for that name, whose real signature is the one transmuted to (see
    // `os_win.c`'s `aSyscall`: kernel32's own `CreateFileW`,
    // `GetFileAttributesW`, `GetFileAttributesExW` and `DeleteFileW`).
    let saved = unsafe {
        Previous {
            create_file: std::mem::transmute::<unsafe extern "C" fn(), CreateFileFn>(previous(
                c"CreateFileW",
            )?),
            get_attributes: std::mem::transmute::<unsafe extern "C" fn(), GetAttributesFn>(
                previous(c"GetFileAttributesW")?,
            ),
            get_attributes_ex: std::mem::transmute::<unsafe extern "C" fn(), GetAttributesExFn>(
                previous(c"GetFileAttributesExW")?,
            ),
            delete_file: std::mem::transmute::<unsafe extern "C" fn(), DeleteFileFn>(previous(
                c"DeleteFileW",
            )?),
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
    let overrides: [(&std::ffi::CStr, ffi::sqlite3_syscall_ptr); 4] = unsafe {
        [
            (
                c"CreateFileW",
                Some(std::mem::transmute::<CreateFileFn, unsafe extern "C" fn()>(
                    anchored_create_file,
                )),
            ),
            (
                c"GetFileAttributesW",
                Some(
                    std::mem::transmute::<GetAttributesFn, unsafe extern "C" fn()>(
                        anchored_get_attributes,
                    ),
                ),
            ),
            (
                c"GetFileAttributesExW",
                Some(std::mem::transmute::<
                    GetAttributesExFn,
                    unsafe extern "C" fn(),
                >(anchored_get_attributes_ex)),
            ),
            (
                c"DeleteFileW",
                Some(std::mem::transmute::<DeleteFileFn, unsafe extern "C" fn()>(
                    anchored_delete_file,
                )),
            ),
        ]
    };
    for (name, call) in overrides {
        // SAFETY: as the unix body: once, behind `INSTALLED`, before any
        // anchored connection exists; an aligned pointer store is atomic,
        // and the old and new calls agree on every path that is not ours.
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

    fn wide(text: &str) -> Vec<u16> {
        text.encode_utf16().collect()
    }

    /// A folder held as the session holds `.slate`, but sharing deletion so
    /// a fact can rename it once nothing inside is open.
    fn open_dir(path: &Path) -> fs::File {
        use std::os::windows::fs::OpenOptionsExt as _;
        use windows_sys::Win32::Storage::FileSystem::FILE_FLAG_BACKUP_SEMANTICS;
        fs::OpenOptions::new()
            .read(true)
            .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
            .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
            .open(path)
            .unwrap()
    }

    #[test]
    fn only_a_plain_leaf_under_a_numeric_token_is_synthetic() {
        assert_eq!(
            split_synthetic(&wide(r"C:\|slate-fd\7\cache.sqlite")),
            Some((7, Some(&wide("cache.sqlite")[..])))
        );
        assert_eq!(split_synthetic(&wide(r"C:\|slate-fd\7")), Some((7, None)));
        for not_ours in [
            r"C:\|slate-fd",
            r"C:\|slate-fd\",
            r"C:\|slate-fdx\7\cache.sqlite",
            r"C:\|slate-fd\x7\cache.sqlite",
            r"C:\|slate-fd\7\",
            r"C:\|slate-fd\7\..",
            r"C:\|slate-fd\7\a\b",
            r"C:\|slate-fd\7\a/b",
            r"C:\|slate-fd\7\c:x",
            r"C:\tmp\.slate-fd\7\cache.sqlite",
        ] {
            assert_eq!(split_synthetic(&wide(not_ours)), None, "{not_ours:?}");
        }
    }

    /// No file system answers the synthetic path: kernel32's own
    /// `CreateFileW`, which every other path still reaches, refuses its name.
    /// So a connection the hook somehow missed fails, never opening a folder.
    #[test]
    fn without_the_hook_the_synthetic_path_names_nothing() {
        use windows_sys::Win32::Foundation::ERROR_INVALID_NAME;
        use windows_sys::Win32::Storage::FileSystem::CreateFileW;
        let mut path = wide(r"C:\|slate-fd\7\cache.sqlite");
        path.push(0);
        // SAFETY: a NUL-terminated path; a failed open returns no handle.
        let handle = unsafe {
            CreateFileW(
                path.as_ptr(),
                0x8000_0000,
                FILE_SHARE_READ,
                std::ptr::null(),
                OPEN_ALWAYS,
                0,
                std::ptr::null_mut(),
            )
        };
        assert_eq!(handle, INVALID_HANDLE_VALUE);
        assert_eq!(
            io::Error::last_os_error().raw_os_error(),
            Some(ERROR_INVALID_NAME as i32)
        );
    }

    /// The spike's question (WB-28 design item 3): does `GetFullPathNameW`
    /// leave the synthetic path as it is? SQLite opens what it returns.
    #[test]
    fn the_full_path_of_a_synthetic_path_is_itself() {
        use windows_sys::Win32::Storage::FileSystem::GetFullPathNameW;
        for path in [
            r"C:\|slate-fd\7\cache.sqlite",
            r"C:\|slate-fd\7\cache.sqlite-wal",
        ] {
            let mut input = wide(path);
            input.push(0);
            let mut buffer = vec![0u16; 512];
            // SAFETY: a NUL-terminated input and a buffer of its stated size.
            let written = unsafe {
                GetFullPathNameW(
                    input.as_ptr(),
                    buffer.len() as u32,
                    buffer.as_mut_ptr(),
                    std::ptr::null_mut(),
                )
            } as usize;
            assert_eq!(String::from_utf16_lossy(&buffer[..written]), path);
        }
    }

    /// The spike end to end: a WAL database through the hook lands in the
    /// registered folder and a second connection shares it. Then, with every
    /// connection closed (Windows refuses to rename a folder with a file open
    /// inside), the folder is renamed (this fact holds it sharing deletion,
    /// so it can be); a new connection through the same token still reaches
    /// it, and nothing appears at the old path. A by-path open would fail or
    /// make a new database there.
    #[test]
    fn an_anchored_database_follows_its_folder_and_never_reaches_a_path() {
        let tmp = tempfile::tempdir().unwrap();
        let first = tmp.path().join("first");
        std::fs::create_dir(&first).unwrap();
        let anchor = SqliteAnchor::register(&open_dir(&first), &first).unwrap();
        let path = anchor.database_path("cache.sqlite");
        let conn = crate::db::open_database(&path, 64).unwrap();
        conn.execute_batch("CREATE TABLE t(x); INSERT INTO t VALUES (1);")
            .unwrap();
        let mode: String = conn
            .query_row("PRAGMA journal_mode", [], |row| row.get(0))
            .unwrap();
        assert_eq!(mode, "wal");
        assert_eq!(conn.path(), Some(path.to_str().unwrap()));
        for leaf in ["cache.sqlite", "cache.sqlite-wal", "cache.sqlite-shm"] {
            assert!(first.join(leaf).is_file(), "{leaf} lands in the folder");
        }
        let second = crate::db::open_connection(&path).unwrap();
        let count: i64 = second
            .query_row("SELECT count(*) FROM t", [], |row| row.get(0))
            .unwrap();
        assert_eq!(count, 1, "a second connection shares the database");
        drop((conn, second));

        let moved = tmp.path().join("moved");
        std::fs::rename(&first, &moved).unwrap();
        let third = crate::db::open_connection(&path).unwrap();
        third.execute("INSERT INTO t VALUES (2)", []).unwrap();
        let count: i64 = third
            .query_row("SELECT count(*) FROM t", [], |row| row.get(0))
            .unwrap();
        assert_eq!(
            count, 2,
            "the new connection reached the moved folder's database"
        );
        drop(third);
        assert!(!first.exists(), "nothing is recreated at the old path");
        assert!(moved.join("cache.sqlite").is_file());
        assert_eq!(
            display_path(&path),
            first.join("cache.sqlite"),
            "a diagnostic maps the path back"
        );
        drop(anchor);
        let error = crate::db::open_connection(&path)
            .and_then(|conn| {
                conn.query_row("SELECT count(*) FROM t", [], |row| row.get::<_, i64>(0))
            })
            .unwrap_err();
        assert!(
            error.to_string().contains("unable to open"),
            "a retired token opens nothing: {error}"
        );
    }

    /// A leaf that is a reparse point (here a file symbolic link, when this
    /// account may make one) is never opened through, and the directory is
    /// never opened or deleted.
    #[test]
    fn a_linked_leaf_and_the_directory_itself_are_refused() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(tmp.path().join("real.sqlite"), b"x").unwrap();
        // A file symbolic link where this account may make one, else a
        // junction (no privilege needed): a reparse point either way, so the
        // refusal below is always exercised.
        let symlinked = std::os::windows::fs::symlink_file(
            tmp.path().join("real.sqlite"),
            tmp.path().join("linked.sqlite"),
        )
        .is_ok();
        if !symlinked {
            std::fs::create_dir(tmp.path().join("elsewhere")).unwrap();
            let out = std::process::Command::new("cmd")
                .args(["/C", "mklink", "/J"])
                .arg(tmp.path().join("linked.sqlite"))
                .arg(tmp.path().join("elsewhere"))
                .output()
                .unwrap();
            assert!(out.status.success(), "mklink /J: {out:?}");
        }
        let anchor = SqliteAnchor::register(&open_dir(tmp.path()), tmp.path()).unwrap();
        let c_wide = |path: PathBuf| {
            let mut wide: Vec<u16> = path.as_os_str().encode_wide().collect();
            wide.push(0);
            wide
        };
        let dir = c_wide(anchor.database_path("x").parent().unwrap().to_path_buf());
        // SAFETY: NUL-terminated paths, as SQLite passes them.
        unsafe {
            let handle = anchored_create_file(
                dir.as_ptr(),
                0x8000_0000,
                FILE_SHARE_READ,
                std::ptr::null(),
                OPEN_EXISTING,
                0,
                std::ptr::null_mut(),
            );
            assert_eq!(handle, INVALID_HANDLE_VALUE);
            assert_eq!(anchored_delete_file(dir.as_ptr()), 0);
        }
        {
            let link = c_wide(anchor.database_path("linked.sqlite"));
            // SAFETY: as above.
            let handle = unsafe {
                anchored_create_file(
                    link.as_ptr(),
                    0x8000_0000,
                    FILE_SHARE_READ,
                    std::ptr::null(),
                    OPEN_EXISTING,
                    0,
                    std::ptr::null_mut(),
                )
            };
            assert_eq!(
                handle, INVALID_HANDLE_VALUE,
                "a linked leaf is never opened"
            );
            if symlinked {
                assert_eq!(
                    io::Error::last_os_error().raw_os_error(),
                    Some(ERROR_CANT_ACCESS_FILE as i32)
                );
            }
        }
        let real = c_wide(anchor.database_path("real.sqlite"));
        // SAFETY: as above.
        assert_eq!(unsafe { anchored_delete_file(real.as_ptr()) }, 1);
        assert!(!tmp.path().join("real.sqlite").exists());
    }

    /// The create race through the hook: SQLite opens the database, its WAL
    /// and its shared memory with `OPEN_ALWAYS`, and quietly retries
    /// read-only when that fails, so racing first opens must all be
    /// writable.
    #[test]
    fn racing_first_opens_of_an_anchored_database_all_succeed() {
        use std::sync::Barrier;
        let tmp = tempfile::tempdir().unwrap();
        let race = |path: &Path, round: usize, write: bool| {
            let start = Arc::new(Barrier::new(4));
            let openers: Vec<_> = (0..4)
                .map(|_| {
                    let path = path.to_path_buf();
                    let start = Arc::clone(&start);
                    std::thread::spawn(move || {
                        start.wait();
                        let conn = crate::db::open_connection(&path)?;
                        let readonly = conn.is_readonly(rusqlite::MAIN_DB)?;
                        let wrote = if write {
                            conn.execute("INSERT INTO t VALUES (1)", [])?
                        } else {
                            1
                        };
                        Ok::<_, rusqlite::Error>((readonly, wrote))
                    })
                })
                .collect();
            for opener in openers {
                let outcome = opener
                    .join()
                    .unwrap()
                    .unwrap_or_else(|error| panic!("round {round}, write {write}: {error}"));
                assert_eq!(outcome, (false, 1), "round {round}, write {write}");
            }
        };
        for round in 0..30 {
            let folder = tmp.path().join(format!("d{round}"));
            std::fs::create_dir(&folder).unwrap();
            let anchor = SqliteAnchor::register(&open_dir(&folder), &folder).unwrap();
            let path = anchor.database_path("cache.sqlite");
            race(&path, round, false);
            {
                let conn = crate::db::open_connection(&path).unwrap();
                conn.execute_batch("PRAGMA journal_mode=WAL; CREATE TABLE t(x);")
                    .unwrap();
            }
            race(&path, round, true);
        }
    }
}
