// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The vault watcher's platform half (plan 43 PR 20a, #1267; design
//! `docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md` §4.1,
//! §4.3, §4.4): one recursive `notify` watch on the vault root, its events
//! turned into vault-relative [`FileEvent`]s and filtered by the scanner's own
//! hidden-path rule, and the root anchor the watch is verified against.
//!
//! - **Backend.** `notify` 9.0.0-rc.5's recommended watcher: FSEvents on
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
//!   inotify's `IN_Q_OVERFLOW`) becomes `FileEvent::Rescan`; a backend error
//!   becomes `FileEvent::Ended { cause: Failed }`, which makes one catch-up
//!   callback before the watch ends.
//! - **The root (§4.4).** An event on the root itself that removes or renames
//!   it, or FSEvents' "root changed", ends the watch without a callback
//!   (`FileEvent::Ended { cause: RootChanged }`). Right after arming, the root
//!   path's identity is observed into the handle's [`RootAnchor`]; the session
//!   compares it with its own anchor, and the coalescer re-verifies it before
//!   every callback. Events never carry content: they only ask the host to
//!   rescan through the session's provider.
//!
//! notify's thread runs [`translate`] and the sink and nothing else: the sink
//! only inserts into the coalescer's bounded burst set (§4.3), so this thread
//! never waits on SQLite, the session's connection or the host — blocking it
//! is what makes FSEvents drop events (design §3.2).

use std::path::{Component, Path, PathBuf};
use std::sync::Arc;

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

/// What a watch is anchored to (design §4.4): today the root path and its
/// identity observed right after arming. **The seam for PR 22** (#940, 22a):
/// the session's held root descriptor (`FsRoot`) replaces this, verification
/// becomes `fstat` of the descriptor against `stat` of the path, and 22a
/// decides whether a legitimate rename re-arms the stream instead of ending
/// the watch (`session/watch.rs` `WatchCoalescer::deliver` is that one policy
/// point). PR 22 keeps PR 20's root facts green.
#[derive(Debug, Clone)]
pub(crate) enum RootAnchor {
    /// A directory on disk, by the exact path the session opened.
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
    pub(crate) fn observe(path: &Path) -> Option<Self> {
        Some(Self::Directory {
            path: path.to_path_buf(),
            identity: observe_identity(path)?,
        })
    }

    /// The identity observed at arm time, for the session's arm-time check
    /// against its own anchor (§4.4 step 1).
    pub(crate) fn identity(&self) -> Option<RootIdentity> {
        match self {
            Self::Directory { identity, .. } => Some(*identity),
            #[cfg(test)]
            Self::Scripted { identity, .. } => *identity,
        }
    }

    /// Whether the root path still names the anchored directory (§4.4 step
    /// 2). A failed observation is a mismatch.
    pub(crate) fn still_anchored(&self) -> bool {
        match self {
            Self::Directory { path, identity } => observe_identity(path) == Some(*identity),
            #[cfg(test)]
            Self::Scripted { intact, .. } => intact.load(std::sync::atomic::Ordering::SeqCst),
        }
    }
}

#[cfg(unix)]
fn observe_identity(path: &Path) -> Option<RootIdentity> {
    use std::os::unix::fs::MetadataExt;
    // Follows a symlinked root, as the session's own anchor does
    // (`pin_root_identity` opens the root path).
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
/// 1). `Ok(None)` on a target without one of the three chosen backends; an
/// error when the backend refuses (an inotify watch limit, an unsupported
/// network redirector) or the root cannot be observed right after arming.
pub(super) fn watch_root(
    root: &Path,
    sink: Arc<dyn FileEventSink>,
) -> Result<Option<WatchHandle>, VaultError> {
    #[cfg(any(target_os = "macos", target_os = "windows", target_os = "linux"))]
    {
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
        let anchor = RootAnchor::observe(root).ok_or_else(|| {
            VaultError::Io(std::io::Error::new(
                std::io::ErrorKind::NotFound,
                "the vault root could not be observed after its watch was armed",
            ))
        })?;
        Ok(Some(WatchHandle::new(Box::new(watcher), anchor)))
    }
    #[cfg(not(any(target_os = "macos", target_os = "windows", target_os = "linux")))]
    {
        let _ = (root, sink);
        Ok(None)
    }
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

/// Whether an event on the root itself means the root went away or moved.
fn is_root_change(kind: &EventKind) -> bool {
    matches!(
        kind,
        EventKind::Remove(_) | EventKind::Modify(ModifyKind::Name(_))
    )
}

/// One notify delivery as the session's [`FileEvent`]s (design §4.1). Pure,
/// so the platform rules are pinned without a platform.
pub(super) fn translate(root: &Path, result: notify::Result<notify::Event>) -> Vec<FileEvent> {
    let event = match result {
        Ok(event) => event,
        // A backend error — a failed re-arm, an unreadable queue, a watch
        // limit reached for a new folder: one catch-up, then the watch ends
        // (§4.6). The error's detail is notify's to log.
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
        let handle = watch_root(tmp.path(), sink.clone())
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
    /// its arrival proves the hidden writes were seen and dropped.
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
                    !name.split('/').any(|part| part.starts_with('.')),
                    "a hidden path reached the sink: {event:?}"
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
        let handle = watch_root(&root, sink).unwrap().unwrap();
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
}
