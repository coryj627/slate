// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The vault watcher's platform half (plan 43 PR 20a, #1267; design
//! `docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md` §4.1,
//! §4.3, §4.4): one recursive `notify` watch on the vault root, its events
//! turned into vault-relative [`FileEvent`]s and filtered by the scanner's own
//! hidden-path rule, and the root anchor the watch is verified against.
//!
//! - **Backend.** `notify` 9.0.0-rc.6's recommended watcher: FSEvents on
//!   macOS, ReadDirectoryChangesW on Windows, inotify on Linux (the rust CI
//!   lane; no Slate host runs there and the CLI never watches). Other targets
//!   return `Ok(None)`, and their hosts keep Refresh and the foreground rescan.
//!   Symbolic links are not followed below the root, because the scanner never
//!   follows them, and access events (opens, reads, closes) are not requested,
//!   so a scan's own reads can never start a rescan.
//! - **Filter.** An event path becomes vault-relative by stripping the exact
//!   watched path (notify keeps that spelling) and joining its components with
//!   `/`. It is dropped when it lies outside the root or any component starts
//!   with `.` — the scanner's rule (`session.rs` `scan_vault`), which covers
//!   `.slate/` (cache, WAL, `tmp/`, `oplog/`, prefs), `.obsidian/`, `.git/`,
//!   `.DS_Store` and editor swap files. A path that is not valid UTF-8 cannot
//!   be echo-tested, so it makes its burst call back (`FileEvent::Rescan`).
//! - **Kinds are informational.** FSEvents flags are cumulative per path and
//!   Windows does not tell data from metadata writes, so nothing downstream
//!   branches on a kind; the coalescer keeps the touched paths.
//! - **Overflow.** notify's `Flag::Rescan` (FSEvents `MustScanSubDirs`,
//!   ReadDirectoryChangesW's `ERROR_NOTIFY_ENUM_DIR` or zero-byte completion,
//!   inotify's `IN_Q_OVERFLOW`) becomes `FileEvent::Rescan`; any backend
//!   error becomes `FileEvent::Ended { cause: Failed }`, which makes one
//!   catch-up callback before the watch ends. On Linux that includes a
//!   folder inotify cannot watch, unreadable or past the watch limit, which
//!   rc.6 reports and walks past (#1563).
//! - **The root (§4.4; plan 43 PR 22a's §4.3).** An event on the root itself
//!   that removes or renames it, or FSEvents' "root changed", is
//!   `FileEvent::Ended { cause: RootChanged }`. On macOS and Linux the
//!   handle's [`RootAnchor`] is the root descriptor the session holds open
//!   (`FsRoot`, #940): right after arming, what the watched path names is
//!   observed for the session's arm-time check, and the coalescer compares
//!   the descriptor with the watched path before every callback
//!   ([`RootAnchor::relocation`]). A root whose descriptor is intact but
//!   whose path moved — the user renamed the vault folder — is re-armed once
//!   at the descriptor's verified current path ([`RootAnchor::rearm`]);
//!   another folder at the path, a root that cannot be found, or a root that
//!   changed and came back still ends the watch without a callback, as PR 20
//!   decided. Windows keeps PR 20's path anchor until 22W. Events never carry
//!   content: they only ask the host to rescan through the session's
//!   provider.
//!
//! notify's thread runs [`translate`] and the sink and nothing else: the sink
//! only inserts into the coalescer's bounded burst set (§4.3), so this thread
//! never waits on SQLite, the session's connection or the host — blocking it
//! is what makes FSEvents drop events (design §3.2).

use std::path::{Component, Path, PathBuf};
use std::sync::Arc;
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
use std::{fs, io};

use notify::event::{EventKindMask, ModifyKind};
use notify::{EventKind, RecursiveMode, Watcher};

use super::provider::{FileEvent, FileEventSink, WatchEnd, WatchHandle};
use crate::VaultError;

/// A directory's physical identity: the volume and the file on it (Unix:
/// `st_dev` and `st_ino`; Windows: the volume serial number and the file
/// index, read with `GetFileInformationByHandle`, design §4.4).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct RootIdentity {
    volume: u64,
    file: u128,
}

impl RootIdentity {
    /// The session's own Unix anchor (`VaultRootIdentity`) in this form.
    pub(crate) fn from_device_inode(device: u64, inode: u64) -> Self {
        Self {
            volume: device,
            file: u128::from(inode),
        }
    }
}

/// Where the held root is, against the path its watch is armed at (plan 43
/// PR 22a, #940; design §4.3).
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Relocation {
    /// The watched path still names the held root.
    Intact,
    /// Nothing is at the watched path any more, and the held root's verified
    /// current path is this one: the vault folder was renamed.
    ///
    /// Only [`RootAnchor::Held`] can find a renamed root, so nothing builds
    /// this variant where that anchor is compiled out (Windows until 22W).
    /// The re-arm chain that consumes it stays compiled everywhere, which
    /// keeps 22W a one-line change. The day such a host constructs it, the
    /// expectation goes unfulfilled and the `-D warnings` lint lanes fail
    /// (#1570).
    #[cfg_attr(
        not(all(
            any(target_os = "macos", target_os = "linux"),
            target_pointer_width = "64"
        )),
        expect(
            dead_code,
            reason = "only a held root (macOS and Linux, 64-bit) can be found renamed"
        )
    )]
    Moved(PathBuf),
    /// Something else is at the watched path: a swap.
    Swapped,
    /// The held root's current path cannot be verified: it was removed, or
    /// the path its descriptor reports names another object (§3.6(a)).
    Lost,
}

/// What a watch is anchored to (design §4.4; plan 43 PR 22a, #940, §4.3).
/// On macOS and Linux it is the root descriptor the session holds open
/// (`FsRoot`): verification is `fstat` of that descriptor against `stat` of
/// the watched path. A legitimate rename can re-arm at the descriptor's
/// verified current path; `session/watch.rs` `WatchCoalescer::relocate` is
/// the one policy point, and the re-arm is off until #1513
/// (`WATCH_REARM_ENABLED`). Windows keeps the root path and its arm-time
/// identity until 22W.
#[derive(Debug, Clone)]
pub(crate) enum RootAnchor {
    /// The held root descriptor and the path the watch is armed at.
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    Held {
        watched: PathBuf,
        root: Arc<fs::File>,
        /// `fstat` of `root`: the session's anchor.
        identity: RootIdentity,
        /// What `watched` named right after arming, for the session's
        /// arm-time check (§4.4 step 1).
        armed: Option<RootIdentity>,
    },
    /// A directory on disk, by the exact path the session opened.
    #[cfg(not(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    )))]
    Directory {
        path: PathBuf,
        identity: RootIdentity,
    },
    /// A test double's anchor: its arm-time identity, and whether it still
    /// holds (a fact flips it to stage a swap).
    #[cfg(test)]
    Scripted {
        identity: Option<RootIdentity>,
        intact: Arc<std::sync::atomic::AtomicBool>,
    },
}

impl RootAnchor {
    /// Observe `path` now and anchor to it; `None` when it cannot be
    /// observed or is not a directory.
    #[cfg(not(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    )))]
    pub(crate) fn observe(path: &Path) -> Option<Self> {
        Some(Self::Directory {
            path: path.to_path_buf(),
            identity: observe_identity(path)?,
        })
    }

    /// The identity the watched path named at arm time, for the session's
    /// arm-time check against its own anchor (§4.4 step 1).
    pub(crate) fn identity(&self) -> Option<RootIdentity> {
        match self {
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held { armed, .. } => *armed,
            #[cfg(not(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            )))]
            Self::Directory { identity, .. } => Some(*identity),
            #[cfg(test)]
            Self::Scripted { identity, .. } => *identity,
        }
    }

    /// Whether the watched path still names the anchored directory (§4.4
    /// step 2). A failed observation is a mismatch.
    #[cfg(test)]
    pub(crate) fn still_anchored(&self) -> bool {
        self.relocation() == Relocation::Intact
    }

    /// Where the anchored root is against the watched path (§4.3): the
    /// descriptor's `fstat` against the path's `stat`, and, when nothing is
    /// at the path any more, the descriptor's verified current path.
    pub(crate) fn relocation(&self) -> Relocation {
        match self {
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held {
                watched,
                root,
                identity,
                ..
            } => match std::fs::metadata(watched) {
                Ok(meta) if identity_of_metadata(&meta) == Some(*identity) => Relocation::Intact,
                Ok(_) => Relocation::Swapped,
                Err(error) if error.kind() == io::ErrorKind::NotFound => {
                    match super::fs_root::verified_current_path(root) {
                        Some(now) => Relocation::Moved(now),
                        None => Relocation::Lost,
                    }
                }
                Err(_) => Relocation::Swapped,
            },
            #[cfg(not(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            )))]
            Self::Directory { path, identity } => {
                if observe_identity(path) == Some(*identity) {
                    Relocation::Intact
                } else {
                    Relocation::Swapped
                }
            }
            #[cfg(test)]
            Self::Scripted { intact, .. } => {
                if intact.load(std::sync::atomic::Ordering::SeqCst) {
                    Relocation::Intact
                } else {
                    Relocation::Swapped
                }
            }
        }
    }

    /// A new watch on the same held root, armed at `at` (its verified
    /// current path after a legitimate rename, §4.3). Only a held anchor can
    /// re-arm.
    pub(crate) fn rearm(
        &self,
        at: &Path,
        sink: Arc<dyn FileEventSink>,
    ) -> Result<WatchHandle, VaultError> {
        match self {
            #[cfg(all(
                any(target_os = "macos", target_os = "linux"),
                target_pointer_width = "64"
            ))]
            Self::Held { root, .. } => arm_held(at, Arc::clone(root), sink),
            #[allow(unreachable_patterns)]
            _ => {
                let _ = (at, sink);
                Err(VaultError::Io(std::io::Error::new(
                    std::io::ErrorKind::Unsupported,
                    "this watch cannot follow its root",
                )))
            }
        }
    }
}

/// A directory's identity from metadata already read; `None` when it is not
/// a directory.
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
fn identity_of_metadata(meta: &std::fs::Metadata) -> Option<RootIdentity> {
    use std::os::unix::fs::MetadataExt;
    meta.is_dir()
        .then(|| RootIdentity::from_device_inode(meta.dev(), meta.ino()))
}

#[cfg(unix)]
fn observe_identity(path: &Path) -> Option<RootIdentity> {
    use std::os::unix::fs::MetadataExt;
    // Follows a symlinked root, as the session's own anchor does (`FsRoot`
    // opens the root path, following the root itself).
    let meta = std::fs::metadata(path).ok()?;
    meta.is_dir()
        .then(|| RootIdentity::from_device_inode(meta.dev(), meta.ino()))
}

#[cfg(windows)]
fn observe_identity(path: &Path) -> Option<RootIdentity> {
    use std::os::windows::{fs::OpenOptionsExt as _, io::AsRawHandle as _};
    use windows_sys::Win32::Storage::FileSystem::{
        BY_HANDLE_FILE_INFORMATION, FILE_ATTRIBUTE_DIRECTORY, FILE_FLAG_BACKUP_SEMANTICS,
        FILE_READ_ATTRIBUTES, FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE,
        GetFileInformationByHandle,
    };
    // Every share mode, so the observation never blocks a rename or a delete
    // of the root; backup semantics to open a directory at all.
    let file = std::fs::OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES)
        .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS)
        .open(path)
        .ok()?;
    let mut info = std::mem::MaybeUninit::<BY_HANDLE_FILE_INFORMATION>::uninit();
    // SAFETY: `file` owns a live handle and `info` is writable storage of the
    // exact type; a nonzero return initializes it.
    if unsafe { GetFileInformationByHandle(file.as_raw_handle(), info.as_mut_ptr()) } == 0 {
        return None;
    }
    // SAFETY: established by the successful call above.
    let info = unsafe { info.assume_init() };
    (info.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY != 0).then(|| RootIdentity {
        volume: u64::from(info.dwVolumeSerialNumber),
        file: (u128::from(info.nFileIndexHigh) << 32) | u128::from(info.nFileIndexLow),
    })
}

#[cfg(not(any(unix, windows)))]
fn observe_identity(_path: &Path) -> Option<RootIdentity> {
    None
}

/// Arm one recursive watch on `root` (design §4.1) and anchor it (§4.4 step
/// 1). On macOS and Linux the anchor is `held`, the root the session holds
/// open (plan 43 PR 22a, #940), or for a provider that holds none, the root
/// opened right after arming. `Ok(None)` on a target without one of the
/// three chosen backends; an error when the backend refuses (an inotify
/// watch limit, an unsupported network redirector) or the root cannot be
/// observed right after arming.
pub(super) fn watch_root(
    root: &Path,
    held: Option<&Arc<super::fs_root::FsRoot>>,
    sink: Arc<dyn FileEventSink>,
) -> Result<Option<WatchHandle>, VaultError> {
    #[cfg(all(
        any(target_os = "macos", target_os = "linux"),
        target_pointer_width = "64"
    ))]
    {
        let descriptor = match held {
            Some(held) => held.descriptor().try_clone().map_err(VaultError::Io)?,
            None => {
                use std::os::unix::fs::OpenOptionsExt as _;
                fs::OpenOptions::new()
                    .read(true)
                    .custom_flags(super::fs::directory_open_flags(false))
                    .open(root)
                    .map_err(VaultError::Io)?
            }
        };
        arm_held(root, Arc::new(descriptor), sink).map(Some)
    }
    #[cfg(target_os = "windows")]
    {
        let _ = held;
        let watcher = arm(root, sink)?;
        let anchor = RootAnchor::observe(root).ok_or_else(|| {
            VaultError::Io(std::io::Error::new(
                std::io::ErrorKind::NotFound,
                "the vault root could not be observed after its watch was armed",
            ))
        })?;
        Ok(Some(WatchHandle::new(Box::new(watcher), anchor)))
    }
    #[cfg(not(any(
        target_os = "windows",
        all(
            any(target_os = "macos", target_os = "linux"),
            target_pointer_width = "64"
        )
    )))]
    {
        let _ = (root, held, sink);
        Ok(None)
    }
}

/// One recursive notify watch on `root`, its events translated against the
/// exact spelling it is armed at.
#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
fn arm(
    root: &Path,
    sink: Arc<dyn FileEventSink>,
) -> Result<notify::RecommendedWatcher, VaultError> {
    let handler_root = root.to_path_buf();
    let handler = move |result: notify::Result<notify::Event>| {
        for event in translate(&handler_root, result) {
            sink.on_event(event);
        }
    };
    let config = notify::Config::default()
        .with_follow_symlinks(false)
        .with_event_kinds(EventKindMask::CORE);
    let mut watcher = notify::RecommendedWatcher::new(handler, config).map_err(watch_error)?;
    watcher
        .watch(root, RecursiveMode::Recursive)
        .map_err(watch_error)?;
    Ok(watcher)
}

/// Arm at `watched`, anchored to the held `root` (plan 43 PR 22a): the
/// arm-time observation of the path, and the descriptor's own identity.
#[cfg(all(
    any(target_os = "macos", target_os = "linux"),
    target_pointer_width = "64"
))]
fn arm_held(
    watched: &Path,
    root: Arc<fs::File>,
    sink: Arc<dyn FileEventSink>,
) -> Result<WatchHandle, VaultError> {
    use std::os::unix::fs::MetadataExt;
    let meta = root.metadata().map_err(VaultError::Io)?;
    let identity = RootIdentity::from_device_inode(meta.dev(), meta.ino());
    let watcher = arm(watched, sink)?;
    let armed = observe_identity(watched);
    if armed.is_none() {
        return Err(VaultError::Io(std::io::Error::new(
            std::io::ErrorKind::NotFound,
            "the vault root could not be observed after its watch was armed",
        )));
    }
    Ok(WatchHandle::new(
        Box::new(watcher),
        RootAnchor::Held {
            watched: watched.to_path_buf(),
            root,
            identity,
            armed,
        },
    ))
}

/// A backend refusal as a `VaultError`, keeping the I/O error where there is
/// one. notify's own `Display` can name paths, so it never becomes the
/// message; the session logs the kind at `warn` and the detail at `debug`
/// (the #507 privacy rule).
#[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
fn watch_error(error: notify::Error) -> VaultError {
    use notify::ErrorKind;
    let io = match error.kind {
        ErrorKind::Io(io) => io,
        ErrorKind::PathNotFound => std::io::Error::new(
            std::io::ErrorKind::NotFound,
            "the vault root to watch was not found",
        ),
        ErrorKind::MaxFilesWatch => {
            std::io::Error::other("the platform's file-watch limit is reached")
        }
        _ => std::io::Error::other("the platform watcher refused the vault root"),
    };
    VaultError::Io(io)
}

/// Where an event path lies relative to the watched root.
#[derive(Debug, PartialEq, Eq)]
enum Classified {
    /// Not beneath the root (never reported for a recursive watch, but notify
    /// is not trusted with the filter).
    Outside,
    /// The root itself.
    Root,
    /// Beneath a component that starts with `.`: never the scanner's.
    Hidden,
    /// A visible path whose name is not valid UTF-8.
    Unnamed,
    /// A visible vault-relative path, `/`-joined.
    Visible(String),
}

fn classify(root: &Path, path: &Path) -> Classified {
    let Ok(rest) = path.strip_prefix(root) else {
        return Classified::Outside;
    };
    // #1498: an 8.3 alias is read by its long name before the hidden rule.
    #[cfg(windows)]
    let long = long_spelling(root, rest);
    #[cfg(windows)]
    let rest = long.as_deref().unwrap_or(rest);
    let mut parts = Vec::new();
    for component in rest.components() {
        match component {
            Component::Normal(name) => {
                // The scanner's hidden rule, on the raw bytes so a hidden
                // name that is not UTF-8 is still hidden.
                if name.as_encoded_bytes().first() == Some(&b'.') {
                    return Classified::Hidden;
                }
                parts.push(name);
            }
            Component::CurDir => {}
            Component::ParentDir | Component::RootDir | Component::Prefix(_) => {
                return Classified::Outside;
            }
        }
    }
    if parts.is_empty() {
        return Classified::Root;
    }
    let mut relative = String::new();
    for part in parts {
        let Some(part) = part.to_str() else {
            return Classified::Unnamed;
        };
        if !relative.is_empty() {
            relative.push('/');
        }
        relative.push_str(part);
    }
    Classified::Visible(relative)
}

/// #1498 (plan 43 PR 20b): ReadDirectoryChangesW names an entry by the
/// spelling its writer opened it with — on the Windows lane a write through
/// the cache folder's 8.3 name arrived as `SLATE~1/…`, and Microsoft
/// documents either name as possible ("it is unspecified which one") — and an
/// 8.3 name has no leading dot, so the hidden rule would let a hidden
/// folder's writes through. A relative path one of whose components may be
/// an 8.3 alias (it holds `~`) is read by its long spelling first: the
/// longest prefix of it that still exists and covers its first alias is
/// expanded with `GetLongPathNameW`, so a deleted file's folders still
/// resolve, and each alias in that prefix takes its long name. A path that
/// cannot be resolved keeps its reported spelling — a visible change, which
/// costs one silent rescan, never a missed one — and a path with no `~` is
/// never looked up, so the ordinary event pays nothing. Slate itself always
/// names the cache `.slate`, and its own writes were reported so.
#[cfg(windows)]
fn long_spelling(root: &Path, rest: &Path) -> Option<PathBuf> {
    use std::ffi::OsStr;
    use std::os::windows::ffi::OsStrExt as _;

    let may_be_alias = |name: &OsStr| name.encode_wide().any(|unit| unit == u16::from(b'~'));
    let mut names = Vec::new();
    for component in rest.components() {
        match component {
            Component::Normal(name) => names.push(name),
            // classify's own rules decide anything else.
            _ => return None,
        }
    }
    let first_alias = names.iter().position(|name| may_be_alias(name))?;
    (first_alias + 1..=names.len()).rev().find_map(|end| {
        let prefix: PathBuf = names[..end].iter().collect();
        let long = long_path_name(&root.join(prefix))?;
        let long_names: Vec<&OsStr> = long
            .components()
            .filter_map(|component| match component {
                Component::Normal(name) => Some(name),
                _ => None,
            })
            .collect();
        let tail = long_names.get(long_names.len().checked_sub(end)?..)?;
        Some(
            names
                .iter()
                .enumerate()
                .map(|(index, name)| match tail.get(index) {
                    Some(long_name) if may_be_alias(name) => *long_name,
                    _ => *name,
                })
                .collect(),
        )
    })
}

/// `path` with every 8.3 alias in it expanded (`GetLongPathNameW`), or
/// `None` when it does not exist or cannot be read.
#[cfg(windows)]
fn long_path_name(path: &Path) -> Option<PathBuf> {
    use std::os::windows::ffi::{OsStrExt as _, OsStringExt as _};
    use windows_sys::Win32::Storage::FileSystem::GetLongPathNameW;

    let wide: Vec<u16> = path.as_os_str().encode_wide().chain(Some(0)).collect();
    let mut buffer = vec![0u16; 1024];
    for _ in 0..2 {
        let capacity = u32::try_from(buffer.len()).ok()?;
        // SAFETY: `wide` is NUL-terminated, and `buffer` is writable for the
        // `capacity` UTF-16 units passed with it.
        let written = unsafe { GetLongPathNameW(wide.as_ptr(), buffer.as_mut_ptr(), capacity) };
        let written = usize::try_from(written).ok()?;
        if written == 0 {
            return None;
        }
        if written < buffer.len() {
            buffer.truncate(written);
            return Some(PathBuf::from(std::ffi::OsString::from_wide(&buffer)));
        }
        // Too small: `written` is the size needed, its NUL included.
        buffer.resize(written, 0);
    }
    None
}

/// Whether an event on the root itself means the root went away or moved.
fn is_root_change(kind: &EventKind) -> bool {
    matches!(
        kind,
        EventKind::Remove(_) | EventKind::Modify(ModifyKind::Name(_))
    )
}

/// #1516: whether `path` names a directory now. A path that cannot be read
/// (gone, denied) is not proved a directory, so its event is kept.
fn is_existing_directory(path: &Path) -> bool {
    std::fs::symlink_metadata(path).is_ok_and(|metadata| metadata.is_dir())
}

/// One notify delivery as the session's [`FileEvent`]s (design §4.1). Pure
/// but for #1516's one `symlink_metadata` of a modified path, so the
/// platform rules are pinned without a platform.
pub(super) fn translate(root: &Path, result: notify::Result<notify::Event>) -> Vec<FileEvent> {
    let event = match result {
        Ok(event) => event,
        // Any backend error: one catch-up, then the watch ends (§4.6). That
        // includes a folder inotify cannot watch, which it reports and walks
        // past (notify-rs/notify#970): unreadable, or past the watch limit.
        // Kept live, the watch would never see that folder, even once it was
        // made readable (#1563). The error's detail is notify's to log.
        Err(_) => {
            return vec![FileEvent::Ended {
                cause: WatchEnd::Failed,
            }];
        }
    };

    if event.need_rescan() {
        // The platform dropped events. Only a rescan confined to hidden
        // folders (FSEvents may name `.slate/oplog`) drops nothing the
        // scanner reads, so it is dropped only when every path it names is
        // hidden. It keeps a `relative` only when exactly one visible path
        // remains; anything else (no path, the root, an unnamed or a second
        // path) is a rescan anywhere.
        let mut named = event
            .paths
            .iter()
            .map(|path| classify(root, path))
            .filter(|classified| *classified != Classified::Hidden);
        let relative = match (named.next(), named.next()) {
            (None, _) if !event.paths.is_empty() => return Vec::new(),
            (Some(Classified::Visible(relative)), None) => Some(relative),
            _ => None,
        };
        return vec![FileEvent::Rescan { relative }];
    }

    // FSEvents' WatchRoot flag: the watched path was renamed, removed or
    // replaced (design §3.2).
    if event.info() == Some("root changed") {
        return vec![FileEvent::Ended {
            cause: WatchEnd::RootChanged,
        }];
    }

    // Opens, reads and closes never change what the scanner sees; the config
    // does not ask for them, and this keeps any that arrive from starting a
    // rescan (the scan's own reads would otherwise feed back).
    if matches!(event.kind, EventKind::Access(_)) {
        return Vec::new();
    }

    let mut out = Vec::new();
    if let (EventKind::Modify(ModifyKind::Name(notify::event::RenameMode::Both)), [from, to]) =
        (&event.kind, event.paths.as_slice())
        && let (Classified::Visible(from), Classified::Visible(to)) =
            (classify(root, from), classify(root, to))
    {
        out.push(FileEvent::Renamed { from, to });
        return out;
    }
    for path in &event.paths {
        match classify(root, path) {
            Classified::Root => {
                if is_root_change(&event.kind) {
                    return vec![FileEvent::Ended {
                        cause: WatchEnd::RootChanged,
                    }];
                }
            }
            // #1516: NTFS updates a folder's own entry when a handle in it
            // closes; nothing the scanner reads changed, so that `Modify` is
            // dropped. A rename (`Name`, any side) is never dropped: it is
            // how the scanner hears of a folder renamed one side at a time.
            Classified::Visible(_)
                if matches!(
                    event.kind,
                    EventKind::Modify(
                        ModifyKind::Any
                            | ModifyKind::Data(_)
                            | ModifyKind::Metadata(_)
                            | ModifyKind::Other
                    )
                ) && is_existing_directory(path) => {}
            Classified::Visible(relative) => out.push(match event.kind {
                EventKind::Create(_) => FileEvent::Created { relative },
                EventKind::Remove(_) => FileEvent::Deleted { relative },
                _ => FileEvent::Modified { relative },
            }),
            Classified::Unnamed => out.push(FileEvent::Rescan { relative: None }),
            Classified::Hidden | Classified::Outside => {}
        }
    }
    out
}

/// #1498 (plan 43 PR 20b): `path`'s final component as its volume's 8.3
/// short name, when the volume made one that differs from the long name
/// (`.slate` is `SLATE~1`). `None` where the volume makes no 8.3 names, or
/// the path cannot be read.
#[cfg(all(test, windows))]
pub(crate) fn short_name_for_tests(path: &Path) -> Option<std::ffi::OsString> {
    use std::os::windows::ffi::{OsStrExt as _, OsStringExt as _};
    use windows_sys::Win32::Storage::FileSystem::GetShortPathNameW;
    let wide: Vec<u16> = path.as_os_str().encode_wide().chain(Some(0)).collect();
    let mut buffer = vec![0u16; 32 * 1024];
    let capacity = u32::try_from(buffer.len()).ok()?;
    // SAFETY: `wide` is NUL-terminated, and `buffer` is writable for the
    // `capacity` UTF-16 units passed with it.
    let written = unsafe { GetShortPathNameW(wide.as_ptr(), buffer.as_mut_ptr(), capacity) };
    let written = usize::try_from(written).ok()?;
    if written == 0 || written >= buffer.len() {
        return None;
    }
    let short = PathBuf::from(std::ffi::OsString::from_wide(&buffer[..written]));
    let name = short.file_name()?.to_os_string();
    (Some(name.as_os_str()) != path.file_name()).then_some(name)
}

/// #1498: a line of evidence for the Windows lane's log, written past
/// libtest's capture (which takes only the `print!` family) so it shows for a
/// passing fact too.
#[cfg(all(test, windows))]
pub(crate) fn report_evidence_for_tests(line: &str) {
    use std::io::Write as _;
    let _ = writeln!(std::io::stderr(), "{line}");
}

#[cfg(test)]
mod tests {
    use super::*;
    use notify::event::{AccessKind, CreateKind, DataChange, Flag, RemoveKind, RenameMode};
    use std::sync::{Condvar, Mutex};
    use std::time::{Duration, Instant};

    fn root() -> PathBuf {
        if cfg!(windows) {
            PathBuf::from(r"\\?\C:\vaults\garden")
        } else {
            PathBuf::from("/vaults/garden")
        }
    }

    fn at(relative: &str) -> PathBuf {
        let mut path = root();
        for part in relative.split('/') {
            path.push(part);
        }
        path
    }

    fn event(kind: EventKind, paths: &[PathBuf]) -> notify::Result<notify::Event> {
        let mut event = notify::Event::new(kind);
        for path in paths {
            event = event.add_path(path.clone());
        }
        Ok(event)
    }

    fn modified(relative: &str) -> FileEvent {
        FileEvent::Modified {
            relative: relative.into(),
        }
    }

    /// §4.1: event paths become vault-relative by stripping the watched
    /// spelling and joining with `/`; the scanner's hidden rule drops
    /// `.slate/`, `.obsidian/`, `.git/` and dot files at any depth.
    #[test]
    fn event_paths_become_vault_relative_and_hidden_paths_drop() {
        let root = root();
        let data = EventKind::Modify(ModifyKind::Data(DataChange::Any));
        assert_eq!(
            translate(&root, event(data, &[at("notes/a.md")])),
            vec![modified("notes/a.md")]
        );
        for hidden in [
            ".slate/cache.sqlite-wal",
            ".slate/tmp/.tmpXYZ",
            ".obsidian/app.json",
            ".hidden.md",
            "notes/.git/HEAD",
            "notes/.DS_Store",
        ] {
            assert_eq!(
                translate(&root, event(data, &[at(hidden)])),
                Vec::new(),
                "{hidden}"
            );
        }
        let elsewhere = if cfg!(windows) {
            PathBuf::from(r"\\?\C:\vaults\other\a.md")
        } else {
            PathBuf::from("/vaults/other/a.md")
        };
        assert_eq!(translate(&root, event(data, &[elsewhere])), Vec::new());
        // Kinds are informational, but keep their broad shape.
        assert_eq!(
            translate(
                &root,
                event(EventKind::Create(CreateKind::File), &[at("b.md")])
            ),
            vec![FileEvent::Created {
                relative: "b.md".into()
            }]
        );
        assert_eq!(
            translate(
                &root,
                event(EventKind::Remove(RemoveKind::Any), &[at("b.md")])
            ),
            vec![FileEvent::Deleted {
                relative: "b.md".into()
            }]
        );
    }

    /// The atomic save renames `.slate/tmp/…` onto the note: the hidden side
    /// drops and the note stays; a rename between two visible paths keeps
    /// both.
    #[test]
    fn a_rename_keeps_its_visible_sides() {
        let root = root();
        let both = EventKind::Modify(ModifyKind::Name(RenameMode::Both));
        assert_eq!(
            translate(&root, event(both, &[at(".slate/tmp/.tmpA1"), at("a.md")])),
            vec![modified("a.md")]
        );
        assert_eq!(
            translate(&root, event(both, &[at("a.md"), at("b/a.md")])),
            vec![FileEvent::Renamed {
                from: "a.md".into(),
                to: "b/a.md".into()
            }]
        );
    }

    /// §4.1: notify's `Flag::Rescan` is the overflow signal, whatever path it
    /// names; a rescan confined to a hidden folder drops nothing the scanner
    /// reads; a backend error ends the watch after one catch-up.
    #[test]
    fn a_rescan_flag_is_an_overflow_and_an_error_ends_the_watch() {
        let root = root();
        let rescan = |paths: &[PathBuf]| {
            let mut event = notify::Event::new(EventKind::Other).set_flag(Flag::Rescan);
            for path in paths {
                event = event.add_path(path.clone());
            }
            translate(&root, Ok(event))
        };
        assert_eq!(rescan(&[]), vec![FileEvent::Rescan { relative: None }]);
        assert_eq!(
            rescan(std::slice::from_ref(&root)),
            vec![FileEvent::Rescan { relative: None }]
        );
        assert_eq!(
            rescan(&[at("notes")]),
            vec![FileEvent::Rescan {
                relative: Some("notes".into())
            }]
        );
        assert_eq!(rescan(&[at(".slate/oplog")]), Vec::new());
        assert_eq!(
            rescan(&[at(".slate/oplog"), at(".obsidian")]),
            Vec::new(),
            "every named path hidden"
        );
        // Codoki on #1496: a hidden first path must not hide a visible one.
        assert_eq!(
            rescan(&[at(".slate/oplog"), at("notes")]),
            vec![FileEvent::Rescan {
                relative: Some("notes".into())
            }]
        );
        assert_eq!(
            rescan(&[at("notes"), at("journal")]),
            vec![FileEvent::Rescan { relative: None }],
            "two visible paths: anywhere"
        );
        assert_eq!(
            rescan(&[at(".slate/oplog"), root.clone()]),
            vec![FileEvent::Rescan { relative: None }]
        );
        assert_eq!(
            translate(&root, Err(notify::Error::generic("re-arm failed"))),
            vec![FileEvent::Ended {
                cause: WatchEnd::Failed
            }]
        );
    }

    /// §4.6 (#1563, the owner's option A): every backend error ends the watch
    /// after one catch-up. notify 9.0.0-rc.6 (notify-rs/notify#970) walks a
    /// recursive inotify watch past a folder it cannot watch and reports that
    /// folder as an error. Inside the root, outside it, on the root, or with
    /// no path at all, the error ends the watch. So does a watch limit. A
    /// `PathNotFound` would too, though rc.6 skips a folder that vanishes
    /// mid-walk without reporting it.
    #[test]
    fn every_backend_error_ends_the_watch() {
        let root = root();
        let denied = |paths: &[PathBuf]| {
            let mut error =
                notify::Error::io(std::io::Error::from(std::io::ErrorKind::PermissionDenied));
            for path in paths {
                error = error.add_path(path.clone());
            }
            translate(&root, Err(error))
        };
        let failed = vec![FileEvent::Ended {
            cause: WatchEnd::Failed,
        }];
        assert_eq!(denied(&[at("private")]), failed, "a folder in the vault");
        assert_eq!(denied(&[at("notes/private")]), failed, "a nested folder");
        assert_eq!(denied(&[at(".git/objects")]), failed, "hidden");
        assert_eq!(denied(&[]), failed, "no folder named");
        assert_eq!(denied(std::slice::from_ref(&root)), failed, "the root");
        let outside = root.parent().expect("root has a parent").join("other");
        assert_eq!(denied(&[outside]), failed, "outside the root");
        assert_eq!(
            denied(&[at("private"), root.clone()]),
            failed,
            "one named path is the root"
        );
        assert_eq!(
            translate(
                &root,
                Err(notify::Error::new(notify::ErrorKind::MaxFilesWatch).add_path(at("big")))
            ),
            failed,
            "the watch limit loses a folder the scanner reads"
        );
        assert_eq!(
            translate(
                &root,
                Err(notify::Error::path_not_found().add_path(at("gone")))
            ),
            failed
        );
    }

    /// §4.4: the root removed or renamed (inotify's DELETE_SELF/MOVE_SELF,
    /// ReadDirectoryChangesW's `Remove(Folder)` on the root) or FSEvents'
    /// "root changed" ends the watch; other events on the root itself are not
    /// vault paths.
    #[test]
    fn a_root_change_ends_the_watch() {
        let root = root();
        for kind in [
            EventKind::Remove(RemoveKind::Folder),
            EventKind::Modify(ModifyKind::Name(RenameMode::From)),
        ] {
            assert_eq!(
                translate(&root, event(kind, std::slice::from_ref(&root))),
                vec![FileEvent::Ended {
                    cause: WatchEnd::RootChanged
                }],
                "{kind:?}"
            );
        }
        let fsevents = notify::Event::new(EventKind::Remove(RemoveKind::Any))
            .add_path(root.clone())
            .set_info("root changed");
        assert_eq!(
            translate(&root, Ok(fsevents)),
            vec![FileEvent::Ended {
                cause: WatchEnd::RootChanged
            }]
        );
        assert_eq!(
            translate(
                &root,
                event(
                    EventKind::Create(CreateKind::Folder),
                    std::slice::from_ref(&root)
                )
            ),
            Vec::new()
        );
        assert_eq!(
            translate(
                &root,
                event(
                    EventKind::Modify(ModifyKind::Metadata(notify::event::MetadataKind::Any)),
                    std::slice::from_ref(&root)
                )
            ),
            Vec::new()
        );
    }

    #[test]
    fn access_events_never_reach_the_sink() {
        let root = root();
        for kind in [
            EventKind::Access(AccessKind::Open(notify::event::AccessMode::Read)),
            EventKind::Access(AccessKind::Close(notify::event::AccessMode::Write)),
            EventKind::Access(AccessKind::Read),
        ] {
            assert_eq!(translate(&root, event(kind, &[at("a.md")])), Vec::new());
        }
    }

    /// §4.1: a visible path that is not valid UTF-8 cannot be echo-tested,
    /// so it makes its burst call back; a hidden one still drops.
    #[cfg(unix)]
    #[test]
    fn a_non_utf8_path_is_an_unnamed_change() {
        use std::os::unix::ffi::OsStrExt as _;
        let root = root();
        let data = EventKind::Modify(ModifyKind::Data(DataChange::Any));
        let unnamed = root.join(std::ffi::OsStr::from_bytes(b"caf\xe9.md"));
        assert_eq!(
            translate(&root, event(data, &[unnamed])),
            vec![FileEvent::Rescan { relative: None }]
        );
        let hidden = root.join(std::ffi::OsStr::from_bytes(b".caf\xe9"));
        assert_eq!(translate(&root, event(data, &[hidden])), Vec::new());
    }

    // --- The real backend (inotify on the rust lane, ReadDirectoryChangesW
    // on the Windows lane, FSEvents locally; design §4.7). ---

    /// Records every event the provider's watch delivers.
    #[derive(Default)]
    struct RecordingSink {
        events: Mutex<Vec<FileEvent>>,
        arrived: Condvar,
    }

    impl FileEventSink for RecordingSink {
        fn on_event(&self, event: FileEvent) {
            self.events.lock().unwrap().push(event);
            self.arrived.notify_all();
        }
    }

    impl RecordingSink {
        fn names(event: &FileEvent) -> Vec<&str> {
            match event {
                FileEvent::Created { relative }
                | FileEvent::Modified { relative }
                | FileEvent::Deleted { relative } => vec![relative.as_str()],
                FileEvent::Renamed { from, to } => vec![from.as_str(), to.as_str()],
                FileEvent::Rescan { relative } => relative.as_deref().into_iter().collect(),
                FileEvent::Ended { .. } => Vec::new(),
            }
        }

        /// Wait up to `within` for an event naming `relative`.
        fn wait_for(&self, relative: &str, within: Duration) -> bool {
            let deadline = Instant::now() + within;
            let mut events = self.events.lock().unwrap();
            loop {
                if events
                    .iter()
                    .any(|event| Self::names(event).contains(&relative))
                {
                    return true;
                }
                let now = Instant::now();
                if now >= deadline {
                    return false;
                }
                events = self.arrived.wait_timeout(events, deadline - now).unwrap().0;
            }
        }

        fn clear(&self) {
            self.events.lock().unwrap().clear();
        }

        fn snapshot(&self) -> Vec<FileEvent> {
            self.events.lock().unwrap().clone()
        }
    }

    const DELIVERY: Duration = Duration::from_secs(10);

    fn watched() -> (tempfile::TempDir, Arc<RecordingSink>, WatchHandle) {
        watched_after(|_| {})
    }

    /// Arm after `seed` has shaped the vault. inotify adds a new folder's
    /// watch only after reporting the folder, so a file made in a folder
    /// created after arming can be missed; folders a fact writes into are
    /// seeded first.
    fn watched_after(
        seed: impl FnOnce(&Path),
    ) -> (tempfile::TempDir, Arc<RecordingSink>, WatchHandle) {
        let tmp = tempfile::tempdir().unwrap();
        seed(tmp.path());
        let sink = Arc::new(RecordingSink::default());
        let handle = watch_root(tmp.path(), None, sink.clone())
            .expect("the platform watch arms")
            .expect("a live handle on a local vault");
        (tmp, sink, handle)
    }

    /// §4.7: an external create, modify and delete each reach the sink as
    /// the vault-relative path, kind-agnostic, within 10 s.
    #[test]
    fn an_external_create_modify_and_delete_reach_the_sink() {
        let (tmp, sink, _handle) =
            watched_after(|root| std::fs::create_dir(root.join("notes")).unwrap());
        let note = tmp.path().join("notes").join("a.md");

        std::fs::write(&note, b"# A\n").unwrap();
        assert!(
            sink.wait_for("notes/a.md", DELIVERY),
            "create: {:?}",
            sink.snapshot()
        );

        sink.clear();
        std::fs::write(&note, b"# A\n\nMore.\n").unwrap();
        assert!(
            sink.wait_for("notes/a.md", DELIVERY),
            "modify: {:?}",
            sink.snapshot()
        );

        sink.clear();
        std::fs::remove_file(&note).unwrap();
        assert!(
            sink.wait_for("notes/a.md", DELIVERY),
            "delete: {:?}",
            sink.snapshot()
        );
    }

    /// §4.7: `.slate/`, `.obsidian/` and dot files never reach the sink. The
    /// sentinel written last arrives after anything written before it, so
    /// its arrival proves the hidden writes were seen and dropped. Only the
    /// two visible paths written may arrive at all, so a hidden folder
    /// reported by another spelling (ReadDirectoryChangesW may report an
    /// 8.3 short name, `SLATE~1`) fails this too.
    #[test]
    fn the_slate_cache_and_hidden_paths_never_reach_the_sink() {
        let (tmp, sink, _handle) = watched();
        let root = tmp.path();
        std::fs::create_dir_all(root.join(".slate").join("tmp")).unwrap();
        std::fs::write(root.join(".slate").join("cache.sqlite-wal"), b"wal").unwrap();
        std::fs::write(root.join(".slate").join("tmp").join(".tmpA1"), b"staged").unwrap();
        std::fs::create_dir(root.join(".obsidian")).unwrap();
        std::fs::write(root.join(".obsidian").join("app.json"), b"{}").unwrap();
        std::fs::write(root.join(".hidden.md"), b"hidden").unwrap();
        std::fs::create_dir_all(root.join("sub").join(".git")).unwrap();
        std::fs::write(root.join("sub").join(".git").join("HEAD"), b"ref").unwrap();
        std::fs::write(root.join("visible.md"), b"seen").unwrap();

        assert!(
            sink.wait_for("visible.md", DELIVERY),
            "{:?}",
            sink.snapshot()
        );
        std::thread::sleep(Duration::from_millis(300));
        for event in sink.snapshot() {
            for name in RecordingSink::names(&event) {
                assert!(
                    ["sub", "visible.md"].contains(&name),
                    "only the visible writes may reach the sink: {event:?}"
                );
            }
        }
    }

    /// #1498: a path named by an 8.3 alias is classified by its long
    /// spelling. The cache folder's alias is hidden — the folder itself, a
    /// file still in it and one already deleted, whose folder still
    /// resolves — and a visible folder's alias becomes the long,
    /// vault-relative path the scanner and the echo ledger use. A `~` name
    /// that is no alias, and an alias that resolves to nothing, keep their
    /// reported spelling.
    #[cfg(windows)]
    #[test]
    fn an_8_3_alias_is_classified_by_its_long_spelling() {
        let tmp = tempfile::tempdir().unwrap();
        let root = tmp.path();
        std::fs::create_dir_all(root.join(".slate").join("tmp")).unwrap();
        std::fs::write(root.join(".slate").join("tmp").join("staged.tmp"), b"x").unwrap();
        std::fs::create_dir(root.join("A long folder name")).unwrap();
        std::fs::write(root.join("A long folder name").join("note.md"), b"x").unwrap();
        std::fs::write(root.join("draft~2.md"), b"x").unwrap();
        let Some(cache) = short_name_for_tests(&root.join(".slate")) else {
            report_evidence_for_tests(
                "#1498 evidence: this volume makes no 8.3 names, so there is no alias to classify",
            );
            return;
        };
        let folder = short_name_for_tests(&root.join("A long folder name"))
            .expect("a volume that aliases the cache folder aliases a long folder name too");

        assert_eq!(classify(root, &root.join(&cache)), Classified::Hidden);
        assert_eq!(
            classify(root, &root.join(&cache).join("tmp").join("staged.tmp")),
            Classified::Hidden
        );
        assert_eq!(
            classify(root, &root.join(&cache).join("tmp").join("gone.tmp")),
            Classified::Hidden
        );
        assert_eq!(
            classify(root, &root.join(&folder).join("note.md")),
            Classified::Visible("A long folder name/note.md".into())
        );
        assert_eq!(
            classify(root, &root.join("draft~2.md")),
            Classified::Visible("draft~2.md".into())
        );
        assert_eq!(
            classify(root, &root.join("NOSUCH~1").join("x.md")),
            Classified::Visible("NOSUCH~1/x.md".into())
        );
    }

    /// #1498 (plan 43 PR 20b): ReadDirectoryChangesW names an entry by its
    /// long name or by its 8.3 short name ("it is unspecified which one"),
    /// and the cache folder's short name (`SLATE~1`) has no leading dot.
    /// Writes into the cache through that spelling — creates, a rewrite, a
    /// rename and deletes — never reach the sink: the sentinel written last
    /// proves they were reported and dropped. A volume that makes no 8.3
    /// names gives no writer that spelling, and the fact records so.
    #[cfg(windows)]
    #[test]
    fn the_slate_cache_is_hidden_under_its_short_name_too() {
        let (tmp, sink, _handle) =
            watched_after(|root| std::fs::create_dir_all(root.join(".slate").join("tmp")).unwrap());
        let root = tmp.path();
        let short = short_name_for_tests(&root.join(".slate"));
        report_evidence_for_tests(&format!(
            "#1498 evidence: the cache folder's 8.3 name on this volume: {short:?} (vault {root:?})"
        ));
        let Some(short) = short else {
            return;
        };
        let cache = root.join(&short);
        std::fs::write(cache.join("probe-1498.tmp"), b"by the short name").unwrap();
        std::fs::write(
            cache.join("probe-1498.tmp"),
            b"by the short name, again and longer",
        )
        .unwrap();
        std::fs::write(cache.join("tmp").join("staged-1498.tmp"), b"staged").unwrap();
        std::fs::rename(
            cache.join("tmp").join("staged-1498.tmp"),
            cache.join("renamed-1498.tmp"),
        )
        .unwrap();
        std::fs::remove_file(cache.join("probe-1498.tmp")).unwrap();
        std::fs::remove_file(cache.join("renamed-1498.tmp")).unwrap();
        std::fs::write(root.join("visible.md"), b"seen").unwrap();

        assert!(
            sink.wait_for("visible.md", DELIVERY),
            "{:?}",
            sink.snapshot()
        );
        std::thread::sleep(Duration::from_millis(300));
        let events = sink.snapshot();
        report_evidence_for_tests(&format!(
            "#1498 evidence: what reached the sink after the short-name writes: {events:?}"
        ));
        for event in &events {
            for name in RecordingSink::names(event) {
                assert_eq!(
                    name, "visible.md",
                    "a write into the cache by its short name reached the sink: {event:?}"
                );
            }
        }
    }

    /// §4.7: dropping the handle stops delivery (FSEvents joins its thread;
    /// inotify and ReadDirectoryChangesW stop theirs asynchronously, so a
    /// short settle precedes the write).
    #[test]
    fn dropping_the_handle_stops_delivery() {
        let (tmp, sink, handle) = watched();
        std::fs::write(tmp.path().join("before.md"), b"x").unwrap();
        assert!(
            sink.wait_for("before.md", DELIVERY),
            "{:?}",
            sink.snapshot()
        );

        drop(handle);
        std::thread::sleep(Duration::from_millis(500));
        sink.clear();
        std::fs::write(tmp.path().join("after.md"), b"y").unwrap();
        assert!(
            !sink.wait_for("after.md", Duration::from_secs(2)),
            "{:?}",
            sink.snapshot()
        );
    }

    /// §4.4 step 1: the handle is anchored to the root it armed, and the
    /// anchor follows the path — a directory swapped in at the path is not
    /// the anchored one.
    #[cfg(unix)]
    #[test]
    fn the_anchor_is_the_root_observed_at_arm_time() {
        let outer = tempfile::tempdir().unwrap();
        let root = outer.path().join("vault");
        std::fs::create_dir(&root).unwrap();
        let sink = Arc::new(RecordingSink::default());
        let handle = watch_root(&root, None, sink).unwrap().unwrap();
        let identity = observe_identity(&root).unwrap();
        assert_eq!(handle.arm_identity(), Some(identity));
        assert!(handle.root_still_anchored());

        std::fs::rename(&root, outer.path().join("moved")).unwrap();
        assert!(
            !handle.root_still_anchored(),
            "a missing root is a mismatch"
        );
        std::fs::create_dir(&root).unwrap();
        assert!(
            !handle.root_still_anchored(),
            "a swapped-in folder is not the anchor"
        );
        std::fs::remove_dir(&root).unwrap();
        std::fs::rename(outer.path().join("moved"), &root).unwrap();
        assert!(handle.root_still_anchored(), "the anchored folder is back");
    }

    // --- #1516 (WB-12): a folder's own non-rename `Modify` is dropped. ---

    /// Every `ModifyKind` that is not a rename, as notify spells them.
    fn non_rename_modifies() -> [EventKind; 4] {
        [
            EventKind::Modify(ModifyKind::Any),
            EventKind::Modify(ModifyKind::Data(DataChange::Any)),
            EventKind::Modify(ModifyKind::Metadata(notify::event::MetadataKind::Any)),
            EventKind::Modify(ModifyKind::Other),
        ]
    }

    /// Every one-sided rename shape, which reaches the per-path arm.
    fn one_sided_renames() -> [EventKind; 3] {
        [
            EventKind::Modify(ModifyKind::Name(RenameMode::From)),
            EventKind::Modify(ModifyKind::Name(RenameMode::To)),
            EventKind::Modify(ModifyKind::Name(RenameMode::Any)),
        ]
    }

    /// #1516: NTFS updates a folder's own entry lazily, when a handle in it
    /// closes, and ReadDirectoryChangesW reports that as the folder's
    /// `Modify`. Nothing the scanner reads changed, so `translate` drops a
    /// non-rename `Modify` of an existing directory, and the real watch never
    /// delivers the folder's `Modified` while the child's own events arrive.
    /// The sentinel is written last, after the folder's notification.
    #[test]
    fn a_directorys_own_modify_is_dropped() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::create_dir(tmp.path().join("Folder")).unwrap();
        for kind in non_rename_modifies() {
            assert_eq!(
                translate(tmp.path(), event(kind, &[tmp.path().join("Folder")])),
                Vec::new(),
                "{kind:?}"
            );
        }

        // #1516's shape: a child written just before the watch arms, then
        // the first open of its folder (the open scan's enumeration), which
        // is when NTFS updates the folder's own entry; then a child of the
        // watch's own, written and removed.
        let (tmp, sink, _handle) = watched_after(|root| {
            std::fs::create_dir(root.join("Folder")).unwrap();
            std::fs::write(root.join("Folder").join("seeded.md"), b"x").unwrap();
        });
        for entry in std::fs::read_dir(tmp.path().join("Folder")).unwrap() {
            std::fs::read(entry.unwrap().path()).unwrap();
        }
        let child = tmp.path().join("Folder").join("child.md");
        std::fs::write(&child, b"x").unwrap();
        std::fs::remove_file(&child).unwrap();
        std::thread::sleep(Duration::from_millis(300));
        std::fs::write(tmp.path().join("sentinel.md"), b"s").unwrap();
        assert!(
            sink.wait_for("sentinel.md", DELIVERY),
            "{:?}",
            sink.snapshot()
        );
        assert!(
            sink.wait_for("Folder/child.md", DELIVERY),
            "the child's own events arrive: {:?}",
            sink.snapshot()
        );
        std::thread::sleep(Duration::from_millis(300));
        assert!(
            !sink.snapshot().contains(&modified("Folder")),
            "the folder's own modify reached the sink: {:?}",
            sink.snapshot()
        );
    }

    /// #1516: every rename shape the `Both` arm does not take — `From`,
    /// `To`, `Any` — still names a visible folder as `Modified`: it is the
    /// signal that makes the scanner notice a directory rename.
    #[test]
    fn a_one_sided_directory_rename_is_still_modified() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::create_dir(tmp.path().join("Folder")).unwrap();
        for kind in one_sided_renames() {
            assert_eq!(
                translate(tmp.path(), event(kind, &[tmp.path().join("Folder")])),
                vec![modified("Folder")],
                "{kind:?}"
            );
        }
    }

    /// #1516: a `Both` rename whose other side is hidden is not a visible
    /// rename, so the per-path arm must keep the visible folder's `Modified`.
    #[test]
    fn a_directory_rename_whose_other_side_is_hidden_is_still_modified() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::create_dir(tmp.path().join("Folder")).unwrap();
        std::fs::create_dir(tmp.path().join(".slate")).unwrap();
        let both = EventKind::Modify(ModifyKind::Name(RenameMode::Both));
        assert_eq!(
            translate(
                tmp.path(),
                event(
                    both,
                    &[
                        tmp.path().join("Folder"),
                        tmp.path().join(".slate").join("x")
                    ]
                )
            ),
            vec![modified("Folder")]
        );
        assert_eq!(
            translate(
                tmp.path(),
                event(
                    both,
                    &[
                        tmp.path().join(".slate").join("x"),
                        tmp.path().join("Folder")
                    ]
                )
            ),
            vec![modified("Folder")]
        );
    }

    /// #1516: a `Modify` whose path no longer exists cannot be proved a
    /// folder, so it still asks for a rescan.
    #[test]
    fn a_modify_on_a_missing_path_is_still_modified() {
        let root = root();
        for kind in non_rename_modifies() {
            assert_eq!(
                translate(&root, event(kind, &[at("gone")])),
                vec![modified("gone")],
                "{kind:?}"
            );
        }
    }

    /// #1516: a file's `Modify` is its content or attributes, so it is kept,
    /// synthetic and through the real watch.
    #[test]
    fn a_modify_on_a_file_is_still_modified() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(tmp.path().join("a.md"), b"x").unwrap();
        for kind in non_rename_modifies() {
            assert_eq!(
                translate(tmp.path(), event(kind, &[tmp.path().join("a.md")])),
                vec![modified("a.md")],
                "{kind:?}"
            );
        }

        let (tmp, sink, _handle) =
            watched_after(|root| std::fs::write(root.join("a.md"), b"x").unwrap());
        std::fs::write(tmp.path().join("a.md"), b"changed").unwrap();
        assert!(
            sink.wait_for("a.md", DELIVERY),
            "a file's modify arrives: {:?}",
            sink.snapshot()
        );
    }
}
