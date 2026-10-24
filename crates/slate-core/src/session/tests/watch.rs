// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 43 PR 20a (#1267): the vault watcher's session facts (design
//! `docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md` §4.7).
//!
//! Two kinds. **The real backend** — inotify on the rust lane (Linux),
//! ReadDirectoryChangesW on the Windows lane's `cargo test -p slate-core`,
//! FSEvents locally on a Mac (CI's FSEvents witness is the mac host's
//! integration fact, PR 20c; owner decision Q7) — runs the coalescer on its
//! own thread with a shortened timing and waits on real time, bounded.
//! **The fake clock and a scripted event source** run the coalescer stepped
//! by the fact under the production timing, so the debounce, the ceiling,
//! the spacing, the duty factor, the overflow, the echo and the root rules
//! are deterministic.

use super::*;
use crate::session::watch::{
    FakeClock, SystemClock, WATCH_CEILING, WATCH_DUTY_FACTOR, WATCH_QUIET, WATCH_SPACING,
    WATCH_TOUCHED_PATH_BOUND, WATCHER_SENTENCE_SPACING_MS, WatchClock, WatchDecision, WatchDrive,
    WatchTiming,
};
// Read only by the two `#[cfg(unix)]` root-rename facts (#1513's switch).
#[cfg(unix)]
use crate::session::watch::WATCH_REARM_ENABLED;
use crate::vault::{FileEvent, FileEventSink, RootAnchor, RootIdentity, WatchEnd, WatchHandle};
use std::sync::atomic::AtomicUsize;
use std::sync::{RwLock, RwLockReadGuard, Weak};
use std::time::{Duration, Instant};

/// The test switch is process-global and libtest runs facts on parallel
/// threads (the Windows lane's `cargo test`), so the fact that sets it holds
/// this exclusively and every fact that needs a live watch holds it shared.
static WATCHER_SWITCH: RwLock<()> = RwLock::new(());

fn switch_shared() -> RwLockReadGuard<'static, ()> {
    WATCHER_SWITCH
        .read()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
}

/// A recording listener. It holds the session weakly, so a callback can
/// check which session locks are free without keeping the session alive.
#[derive(Default)]
struct Recorder {
    session: std::sync::OnceLock<Weak<VaultSession>>,
    external: AtomicUsize,
    calls: Mutex<Vec<Call>>,
    file_changes: Mutex<Vec<FileChangeEvent>>,
}

#[derive(Debug, Clone)]
struct Call {
    thread: String,
    /// The connection, the listener registry and the watch seat were all
    /// free inside the callback; `None` when no session was attached.
    session_locks_free: Option<bool>,
}

impl VaultEventListener for Recorder {
    fn on_error(&self, _code: EventErrorCode, _path: String, _message: String) {}

    fn on_file_change(&self, event: FileChangeEvent) {
        self.file_changes.lock().unwrap().push(event);
    }

    fn on_external_change(&self) {
        let thread = std::thread::current().name().unwrap_or_default().to_owned();
        let session_locks_free = self.session.get().and_then(Weak::upgrade).map(|session| {
            session.conn.try_lock().is_ok()
                && session.event_listeners.try_lock().is_ok()
                && session.watch.try_lock().is_ok()
        });
        self.calls.lock().unwrap().push(Call {
            thread,
            session_locks_free,
        });
        self.external.fetch_add(1, Ordering::SeqCst);
    }
}

impl Recorder {
    fn attach(session: &Arc<VaultSession>) -> Arc<Self> {
        let recorder = Arc::new(Self::default());
        recorder.session.set(Arc::downgrade(session)).unwrap();
        session.register_event_listener(recorder.clone());
        recorder
    }

    fn detached(session: &VaultSession) -> Arc<Self> {
        let recorder = Arc::new(Self::default());
        session.register_event_listener(recorder.clone());
        recorder
    }

    fn count(&self) -> usize {
        self.external.load(Ordering::SeqCst)
    }

    fn reset(&self) {
        self.external.store(0, Ordering::SeqCst);
        self.calls.lock().unwrap().clear();
        self.file_changes.lock().unwrap().clear();
    }
}

/// Poll `condition` every 20 ms for at most `within`.
fn eventually(within: Duration, mut condition: impl FnMut() -> bool) -> bool {
    let deadline = Instant::now() + within;
    loop {
        if condition() {
            return true;
        }
        if Instant::now() >= deadline {
            return false;
        }
        std::thread::sleep(Duration::from_millis(20));
    }
}

const DELIVERY: Duration = Duration::from_secs(10);

// --- The real backend -------------------------------------------------------

/// The production rules at a pace a fact can wait for.
const FAST: WatchTiming = WatchTiming {
    quiet: Duration::from_millis(150),
    ceiling: Duration::from_millis(1500),
    spacing: Duration::from_millis(100),
    duty_factor: WATCH_DUTY_FACTOR,
    touched_path_bound: WATCH_TOUCHED_PATH_BOUND,
};

/// A quiet window long enough for a fact to act mid-burst.
const MID_BURST: WatchTiming = WatchTiming {
    quiet: Duration::from_secs(2),
    ceiling: Duration::from_secs(10),
    ..FAST
};

/// A watched vault. Field order is drop order: the session (and its watch)
/// goes before the folder it watches.
struct Live {
    session: Arc<VaultSession>,
    recorder: Arc<Recorder>,
    tmp: tempfile::TempDir,
    _switch: RwLockReadGuard<'static, ()>,
}

fn write(root: &std::path::Path, relative: &str, contents: &str) {
    let path = root.join(relative);
    std::fs::create_dir_all(path.parent().unwrap()).unwrap();
    std::fs::write(path, contents).unwrap();
}

fn arm(session: &Arc<VaultSession>, timing: WatchTiming) -> Arc<Recorder> {
    let recorder = Recorder::attach(session);
    assert!(
        session.start_watching_with(timing, Arc::new(SystemClock), WatchDrive::Thread),
        "a local vault arms its watch"
    );
    // FSEvents can deliver the fixture's own creation just after arming
    // (design §3.2's first row); let it settle and start clean.
    std::thread::sleep(Duration::from_millis(400));
    assert!(eventually(DELIVERY, || session.watch_is_idle_for_tests()));
    recorder.reset();
    session.reset_watch_decisions_for_tests();
    recorder
}

fn live(seed: &[(&str, &str)]) -> Live {
    live_with(FAST, seed)
}

fn live_with(timing: WatchTiming, seed: &[(&str, &str)]) -> Live {
    let switch = switch_shared();
    let tmp = tempfile::tempdir().unwrap();
    for (relative, contents) in seed {
        write(tmp.path(), relative, contents);
    }
    let session = Arc::new(VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap());
    session.scan_initial(&CancelToken::new()).unwrap();
    let recorder = arm(&session, timing);
    Live {
        session,
        recorder,
        tmp,
        _switch: switch,
    }
}

/// §4.7: an external create reaches the listener as one `on_external_change`
/// call, and none of Slate's file-change events (locked 05 §4.4: external
/// edits never surface as file-change events).
#[test]
fn an_external_change_emits_no_file_change_event() {
    let live = live(&[]);
    write(live.tmp.path(), "outside.md", "# Made outside Slate\n");
    assert!(
        eventually(DELIVERY, || live.recorder.count() >= 1),
        "{:?}",
        live.session.watch_decisions_for_tests()
    );
    assert!(live.recorder.file_changes.lock().unwrap().is_empty());
}

/// §4.3: the callback runs on the coalescer's own thread with no session
/// lock held — the connection, the listener registry and the watch seat are
/// all free inside it.
#[test]
fn the_callback_runs_on_the_coalescer_thread_with_no_session_lock_held() {
    let live = live(&[]);
    write(live.tmp.path(), "outside.md", "# Made outside Slate\n");
    assert!(eventually(DELIVERY, || live.recorder.count() >= 1));
    let calls = live.recorder.calls.lock().unwrap().clone();
    for call in &calls {
        assert_eq!(call.thread, "slate-watch", "{calls:?}");
        assert_eq!(call.session_locks_free, Some(true), "{calls:?}");
    }
}

/// §4.2: a save, a create in a new folder, a new folder, a rename and a move
/// to the Trash through the session are Slate's own echoes: their events
/// reach the coalescer and are dropped, and nothing is called.
#[test]
fn slate_own_writes_are_echoes_and_never_call_back() {
    let live = live(&[("a.md", "# A\n"), ("Folder/b.md", "# B\n")]);
    let session = &live.session;
    session
        .save_text("a.md", "# A\n\nSaved by Slate.\n", None)
        .unwrap();
    session.create_exclusive("New/c.md", "# C\n").unwrap();
    session.create_folder("Empty").unwrap();
    session.rename_file("Folder/b.md", "d.md").unwrap();
    session.delete_file("a.md").unwrap();

    let dropped_a_md = || {
        session.watch_decisions_for_tests().iter().any(|decision| {
            matches!(decision, WatchDecision::DroppedAsEchoes { paths }
                    if paths.iter().any(|path| path == "a.md"))
        })
    };
    assert!(
        eventually(DELIVERY, || dropped_a_md()
            && session.watch_is_idle_for_tests()),
        "{:?}",
        session.watch_decisions_for_tests()
    );
    // Stragglers still settle as echoes.
    std::thread::sleep(FAST.quiet * 4);
    assert!(eventually(DELIVERY, || session.watch_is_idle_for_tests()));
    let decisions = session.watch_decisions_for_tests();
    assert_eq!(live.recorder.count(), 0, "{decisions:?}");
    assert!(
        !decisions
            .iter()
            .any(|decision| matches!(decision, WatchDecision::CalledBack { .. })),
        "{decisions:?}"
    );
}

/// #1498 (plan 43 PR 20b): before Windows arms the watch. ReadDirectoryChangesW
/// may name an entry by its 8.3 short name, and the cache folder's
/// (`SLATE~1`) has no leading dot, so the hidden rule would miss it and the
/// cache's own writes would start a silent rescan every few seconds. On a
/// real watch with the cache busy as a session makes it busy — a rescan
/// (the index, its WAL and journal), saves through the staging folder, a
/// create in a new folder, the template flow's create at the root (its
/// `create_exclusive_reporting`; the shell gate's Templates journey), a
/// rename and a Trash (the op-log), then writes under `.slate` by the long
/// name Slate always uses — nothing calls back, and no touched path names a
/// folder by an 8.3 spelling. No rescan follows the saves, because a
/// complete scan would drop their bursts unseen.
#[cfg(windows)]
#[test]
fn the_cache_a_busy_session_writes_never_calls_back() {
    let live = live(&[("a.md", "# A\n"), ("Folder/b.md", "# B\n")]);
    let session = &live.session;
    let root = live.tmp.path().to_path_buf();
    crate::vault::report_evidence_for_tests(&format!(
        "#1498 evidence: the cache folder's 8.3 name on this volume: {:?} (vault {root:?})",
        crate::vault::short_name_for_tests(&root.join(".slate"))
    ));

    session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();
    session
        .save_text("a.md", "# A\n\nSaved by Slate.\n", None)
        .unwrap();
    session
        .save_text("a.md", "# A\n\nSaved by Slate again, longer.\n", None)
        .unwrap();
    session.create_exclusive("New/c.md", "# C\n").unwrap();
    session
        .create_exclusive_reporting("Journey Note.md", "# Journey Note\n")
        .unwrap();
    session.rename_file("Folder/b.md", "d.md").unwrap();
    session.delete_file("New/c.md").unwrap();
    write(&root, ".slate/probe-1498.tmp", "by the long name");
    write(
        &root,
        ".slate/tmp/probe-1498.tmp",
        "staged by the long name",
    );
    std::fs::remove_file(root.join(".slate").join("probe-1498.tmp")).unwrap();
    std::fs::remove_file(root.join(".slate").join("tmp").join("probe-1498.tmp")).unwrap();

    let dropped_d_md = || {
        session.watch_decisions_for_tests().iter().any(|decision| {
            matches!(decision, WatchDecision::DroppedAsEchoes { paths }
                    if paths.iter().any(|path| path == "Folder/d.md"))
        })
    };
    assert!(
        eventually(DELIVERY, || dropped_d_md()
            && session.watch_is_idle_for_tests()),
        "{:?}",
        session.watch_decisions_for_tests()
    );
    // Stragglers, and SQLite's own late writes, settle too.
    std::thread::sleep(FAST.quiet * 4);
    assert!(eventually(DELIVERY, || session.watch_is_idle_for_tests()));
    let decisions = session.watch_decisions_for_tests();
    crate::vault::report_evidence_for_tests(&format!(
        "#1498 evidence: the coalescer's decisions for a busy cache: {decisions:?}"
    ));
    assert_eq!(live.recorder.count(), 0, "{decisions:?}");
    for decision in &decisions {
        let paths = match decision {
            WatchDecision::CalledBack { paths, .. } | WatchDecision::DroppedAsEchoes { paths } => {
                paths.as_slice()
            }
            _ => &[],
        };
        assert!(
            paths.iter().all(|path| !path.contains('~')),
            "a path reached the coalescer under an 8.3 spelling: {decision:?}"
        );
    }
}

/// §4.2: the echo test is "this session wrote this state", so an external
/// write right after Slate's own save of the same note still calls back.
#[test]
fn an_external_write_after_a_slate_write_still_calls_back() {
    let live = live(&[("a.md", "# A\n")]);
    live.session
        .save_text("a.md", "# A\n\nSaved by Slate.\n", None)
        .unwrap();
    write(
        live.tmp.path(),
        "a.md",
        "# A\n\nThen edited outside Slate, at another length.\n",
    );
    assert!(
        eventually(DELIVERY, || live.recorder.count() >= 1),
        "{:?}",
        live.session.watch_decisions_for_tests()
    );
}

/// §4.3: dropping the session mid-burst stops the platform watch, joins the
/// coalescer within a bound, and no callback follows.
#[test]
fn dropping_the_session_joins_the_coalescer_and_stops_the_watch() {
    let Live {
        session,
        recorder,
        tmp,
        _switch,
    } = live_with(MID_BURST, &[]);
    let coalescer = session.watch_coalescer_for_tests().unwrap();
    write(tmp.path(), "mid-burst.md", "# Mid-burst\n");
    assert!(eventually(DELIVERY, || session.watch_has_open_burst_for_tests()));

    let dropping = Instant::now();
    drop(session);
    assert!(
        dropping.elapsed() < Duration::from_secs(5),
        "the drop took {:?}",
        dropping.elapsed()
    );
    assert!(
        coalescer.upgrade().is_none(),
        "the coalescer thread was joined"
    );

    std::thread::sleep(MID_BURST.quiet + Duration::from_millis(500));
    write(tmp.path(), "after.md", "# After\n");
    std::thread::sleep(Duration::from_millis(500));
    assert_eq!(recorder.count(), 0, "no callback after the session closed");
}

/// `stop_watching` disarms mid-burst with no callback after it returns; a
/// later `start_watching` arms a new watch.
#[test]
fn stop_watching_stops_the_thread_and_no_callback_follows() {
    let live = live_with(MID_BURST, &[]);
    let coalescer = live.session.watch_coalescer_for_tests().unwrap();
    write(live.tmp.path(), "mid-burst.md", "# Mid-burst\n");
    assert!(eventually(DELIVERY, || live
        .session
        .watch_has_open_burst_for_tests()));

    live.session.stop_watching();
    assert!(
        coalescer.upgrade().is_none(),
        "the coalescer thread was joined"
    );
    assert!(!live.session.watch_is_live_for_tests());
    std::thread::sleep(MID_BURST.quiet + Duration::from_millis(500));
    write(live.tmp.path(), "after.md", "# After\n");
    std::thread::sleep(Duration::from_millis(500));
    assert_eq!(live.recorder.count(), 0, "no callback after stop_watching");
    live.session.stop_watching();

    assert!(
        live.session
            .start_watching_with(FAST, Arc::new(SystemClock), WatchDrive::Thread)
    );
    write(live.tmp.path(), "rearmed.md", "# Re-armed\n");
    assert!(eventually(DELIVERY, || live.recorder.count() >= 1));
}

/// §4.6 (#1563, the owner's option A): a folder the watcher may not read,
/// created in a watched vault, is reported by inotify as a permission error,
/// so the watch ends after its catch-up and a later outside edit calls back
/// no more. Root ignores permissions, so the fact proves nothing there and
/// skips.
#[cfg(target_os = "linux")]
#[test]
fn a_folder_the_watcher_may_not_read_ends_the_watch_after_its_catch_up() {
    use std::os::unix::fs::{DirBuilderExt, PermissionsExt};
    if unsafe { libc::geteuid() } == 0 {
        return;
    }
    let live = live(&[("a.md", "# A\n")]);
    // Created unreadable in place and in one call, so the watcher never sees
    // it readable. (A mode-000 folder cannot be moved in: a rename across
    // folders needs write access to the folder itself.)
    let locked = live.tmp.path().join("locked");
    std::fs::DirBuilder::new()
        .mode(0o000)
        .create(&locked)
        .unwrap();

    let called_back = eventually(DELIVERY, || live.recorder.count() >= 1);
    let decisions = live.session.watch_decisions_for_tests();
    let ended = eventually(DELIVERY, || !live.session.watch_is_live_for_tests());
    let at_end = live.recorder.count();
    write(live.tmp.path(), "after.md", "# After\n");
    std::thread::sleep(FAST.quiet + Duration::from_millis(500));
    let after = live.recorder.count();
    // Readable again, so the temp directory can be removed.
    std::fs::set_permissions(&locked, std::fs::Permissions::from_mode(0o755)).unwrap();
    assert!(called_back, "the catch-up: {decisions:?}");
    assert!(ended, "the watch ended: {decisions:?}");
    assert_eq!(after, at_end, "no callback after the watch ended");
}

/// §4.6 (#1563, the owner's option A): a folder that is unreadable when the
/// watch arms is reported by inotify as a permission error, and every backend
/// error ends the watch after one catch-up. Kept live, the watch would miss
/// that folder for good once it became readable: inotify adds watches only
/// for folders created or moved in, never on a permission change. Root
/// ignores permissions, so the fact proves nothing there and skips.
#[cfg(target_os = "linux")]
#[test]
fn a_folder_unreadable_when_the_watch_arms_ends_it_after_one_catch_up() {
    use std::os::unix::fs::{DirBuilderExt, PermissionsExt};
    if unsafe { libc::geteuid() } == 0 {
        return;
    }
    let _switch = switch_shared();
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "a.md", "# A\n");
    let locked = tmp.path().join("locked");
    std::fs::DirBuilder::new()
        .mode(0o000)
        .create(&locked)
        .unwrap();
    let session = Arc::new(VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap());
    session.scan_initial(&CancelToken::new()).unwrap();
    let recorder = Recorder::attach(&session);

    let armed = session.start_watching_with(FAST, Arc::new(SystemClock), WatchDrive::Thread);
    let called_back = eventually(DELIVERY, || recorder.count() >= 1);
    let decisions = session.watch_decisions_for_tests();
    let ended = eventually(DELIVERY, || !session.watch_is_live_for_tests());
    // Readable again, so the temp directory can be removed.
    std::fs::set_permissions(&locked, std::fs::Permissions::from_mode(0o755)).unwrap();
    assert!(armed, "the readable rest of the vault arms the watch");
    assert!(called_back, "one catch-up: {decisions:?}");
    assert!(ended, "the watch ended: {decisions:?}");
    assert_eq!(recorder.count(), 1, "one catch-up, then nothing");
    assert!(
        !session.start_watching_with(FAST, Arc::new(SystemClock), WatchDrive::Thread),
        "an ended watch stays ended until the vault is opened again"
    );
}

/// A vault in a folder of its own, so a fact can move it.
#[cfg(unix)]
fn live_in_a_movable_folder() -> (tempfile::TempDir, std::path::PathBuf, Live) {
    let switch = switch_shared();
    let outer = tempfile::tempdir().unwrap();
    let root = outer.path().join("vault");
    std::fs::create_dir(&root).unwrap();
    write(&root, "seed.md", "# Seed\n");
    let session = Arc::new(VaultSession::from_filesystem(root.clone()).unwrap());
    session.scan_initial(&CancelToken::new()).unwrap();
    let recorder = arm(&session, FAST);
    let live = Live {
        session,
        recorder,
        tmp: tempfile::tempdir().unwrap(),
        _switch: switch,
    };
    (outer, root, live)
}

/// §4.4 (Unix: rename the root away, put a directory at its path, write
/// into both): the watch ends without a callback, and writes into either
/// folder start nothing.
#[cfg(unix)]
#[test]
fn a_swapped_root_ends_the_watch_without_a_callback() {
    let (outer, root, live) = live_in_a_movable_folder();
    let moved = outer.path().join("moved");
    std::fs::rename(&root, &moved).unwrap();
    std::fs::create_dir(&root).unwrap();
    write(&root, "swapped-in.md", "# Not the session's vault\n");
    write(&moved, "original.md", "# The session's vault, moved away\n");

    assert!(
        eventually(DELIVERY, || !live.session.watch_is_live_for_tests()),
        "{:?}",
        live.session.watch_decisions_for_tests()
    );
    std::thread::sleep(FAST.quiet * 4);
    let decisions = live.session.watch_decisions_for_tests();
    assert_eq!(live.recorder.count(), 0, "{decisions:?}");
    assert!(
        decisions.iter().any(|decision| matches!(
            decision,
            WatchDecision::Ended(WatchEnd::RootChanged) | WatchDecision::RootMismatch
        )),
        "{decisions:?}"
    );
    assert!(
        !live.session.start_watching(),
        "an ended watch is not re-armed"
    );
}

/// §4.4: a root renamed away and straight back passes the identity check,
/// but the root-change event alone ends the watch.
#[cfg(unix)]
#[test]
fn a_root_change_event_ends_the_watch() {
    let (outer, root, live) = live_in_a_movable_folder();
    let moved = outer.path().join("moved");
    std::fs::rename(&root, &moved).unwrap();
    std::fs::rename(&moved, &root).unwrap();
    write(&root, "after.md", "# After the round trip\n");

    assert!(
        eventually(DELIVERY, || !live.session.watch_is_live_for_tests()),
        "{:?}",
        live.session.watch_decisions_for_tests()
    );
    std::thread::sleep(FAST.quiet * 4);
    let decisions = live.session.watch_decisions_for_tests();
    assert_eq!(live.recorder.count(), 0, "{decisions:?}");
    assert!(
        decisions.contains(&WatchDecision::Ended(WatchEnd::RootChanged)),
        "{decisions:?}"
    );
}

/// Plan 43 PR 22a (#940; design §4.3, owner decision Q3): a legitimate rename
/// of the vault folder — the held root intact, nothing left at the old path —
/// re-arms the watch once at the held root's verified current path. One
/// catch-up callback covers the moments no stream saw; then a write in the
/// renamed folder calls back with its vault-relative path, and nothing is
/// recreated at the old path.
///
/// Gated by the one switch, [`WATCH_REARM_ENABLED`], which is off until #1513
/// (the owner's decision on #1518). While it is off, this fact returns before
/// it opens a vault, and
/// `a_legitimate_root_rename_ends_the_watch_while_the_rearm_is_off` holds
/// instead. Turning the switch on brings this fact back unchanged.
#[cfg(unix)]
#[test]
fn a_legitimate_root_rename_rearms_at_the_new_path() {
    if !WATCH_REARM_ENABLED {
        return;
    }
    use std::os::unix::fs::MetadataExt as _;
    let (outer, root, live) = live_in_a_movable_folder();
    let moved = outer.path().join("moved");
    std::fs::rename(&root, &moved).unwrap();

    let rearmed = |decisions: &[WatchDecision]| {
        decisions.iter().find_map(|decision| match decision {
            WatchDecision::Rearmed { at } => Some(at.clone()),
            _ => None,
        })
    };
    assert!(
        eventually(DELIVERY, || rearmed(
            &live.session.watch_decisions_for_tests()
        )
        .is_some()),
        "{:?}",
        live.session.watch_decisions_for_tests()
    );
    let at = rearmed(&live.session.watch_decisions_for_tests()).unwrap();
    let identity = |path: &std::path::Path| {
        std::fs::metadata(path)
            .map(|meta| (meta.dev(), meta.ino()))
            .ok()
    };
    assert_eq!(identity(&at), identity(&moved), "re-armed at {at:?}");
    assert!(
        eventually(DELIVERY, || live.recorder.count() >= 1),
        "the catch-up callback: {:?}",
        live.session.watch_decisions_for_tests()
    );
    assert!(live.session.watch_is_live_for_tests());

    assert!(eventually(DELIVERY, || live
        .session
        .watch_is_idle_for_tests()));
    live.recorder.reset();
    live.session.reset_watch_decisions_for_tests();
    write(&moved, "after.md", "# Written in the renamed folder\n");
    assert!(
        eventually(DELIVERY, || live.recorder.count() >= 1),
        "{:?}",
        live.session.watch_decisions_for_tests()
    );
    assert!(
        live.session
            .watch_decisions_for_tests()
            .iter()
            .any(|decision| matches!(decision,
                WatchDecision::CalledBack { paths, .. } if paths.iter().any(|path| path == "after.md"))),
        "{:?}",
        live.session.watch_decisions_for_tests()
    );
    assert!(!root.exists(), "nothing recreated the old path");
}

/// The owner's decision on #1518 (2026-10-19): while the re-arm is off
/// ([`WATCH_REARM_ENABLED`], until #1513), a legitimate rename of the vault
/// folder ends the watch without a callback, as PR 20 decided for every root
/// change (§4.4). A write in the renamed folder starts nothing, the ended
/// watch is not restarted, and nothing is recreated at the old path. With the
/// switch on, this fact returns before it opens a vault, and
/// `a_legitimate_root_rename_rearms_at_the_new_path` holds instead.
#[cfg(unix)]
#[test]
fn a_legitimate_root_rename_ends_the_watch_while_the_rearm_is_off() {
    if WATCH_REARM_ENABLED {
        return;
    }
    let (outer, root, live) = live_in_a_movable_folder();
    let moved = outer.path().join("moved");
    std::fs::rename(&root, &moved).unwrap();
    write(&moved, "after.md", "# Written in the renamed folder\n");

    assert!(
        eventually(DELIVERY, || !live.session.watch_is_live_for_tests()),
        "{:?}",
        live.session.watch_decisions_for_tests()
    );
    std::thread::sleep(FAST.quiet * 4);
    let decisions = live.session.watch_decisions_for_tests();
    assert_eq!(live.recorder.count(), 0, "{decisions:?}");
    assert!(
        decisions.iter().any(|decision| matches!(
            decision,
            WatchDecision::Ended(WatchEnd::RootChanged) | WatchDecision::RootMismatch
        )),
        "{decisions:?}"
    );
    assert!(
        !decisions
            .iter()
            .any(|decision| matches!(decision, WatchDecision::Rearmed { .. })),
        "{decisions:?}"
    );
    assert!(
        !live.session.start_watching(),
        "an ended watch is not re-armed"
    );
    assert!(!root.exists(), "nothing recreated the old path");
}

// --- The fake clock and a scripted event source ----------------------------

#[derive(Clone, Copy, PartialEq, Eq)]
enum Arm {
    Live,
    Unavailable,
    Refused,
}

/// A filesystem provider whose watch is scripted: `watch` hands its sink to
/// the fact, and the fact decides what the anchor says.
struct ScriptedProvider {
    inner: FsVaultProvider,
    arm: Mutex<Arm>,
    identity: Mutex<Option<RootIdentity>>,
    intact: Arc<AtomicBool>,
    sink: Mutex<Option<Arc<dyn FileEventSink>>>,
    watch_calls: AtomicUsize,
    echo_tests: AtomicUsize,
}

impl ScriptedProvider {
    fn new(root: &std::path::Path) -> Arc<Self> {
        Arc::new(Self {
            inner: FsVaultProvider::new(root.to_path_buf()),
            arm: Mutex::new(Arm::Live),
            identity: Mutex::new(None),
            intact: Arc::new(AtomicBool::new(true)),
            sink: Mutex::new(None),
            watch_calls: AtomicUsize::new(0),
            echo_tests: AtomicUsize::new(0),
        })
    }

    fn emit(&self, event: FileEvent) {
        let sink = self
            .sink
            .lock()
            .unwrap()
            .clone()
            .expect("the watch is armed");
        sink.on_event(event);
    }

    fn touched(&self, relative: &str) {
        self.emit(FileEvent::Modified {
            relative: relative.into(),
        });
    }
}

impl crate::VaultProvider for ScriptedProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }
    fn write_file_if_absent(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file_if_absent(relative, contents)
    }
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(relative)
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }
    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }
    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        self.inner.stat(relative)
    }
    fn watch(&self, sink: Arc<dyn FileEventSink>) -> Result<Option<WatchHandle>, VaultError> {
        self.watch_calls.fetch_add(1, Ordering::SeqCst);
        match *self.arm.lock().unwrap() {
            Arm::Live => {
                *self.sink.lock().unwrap() = Some(sink);
                Ok(Some(WatchHandle::scripted(RootAnchor::Scripted {
                    identity: *self.identity.lock().unwrap(),
                    intact: Arc::clone(&self.intact),
                })))
            }
            Arm::Unavailable => Ok(None),
            Arm::Refused => Err(VaultError::Io(std::io::Error::other(
                "the platform refused the watch",
            ))),
        }
    }
    fn is_own_echo(&self, relative: &str) -> bool {
        self.echo_tests.fetch_add(1, Ordering::SeqCst);
        self.inner.is_own_echo(relative)
    }
}

/// A session over a scripted provider, its coalescer stepped by the fact on
/// a fake clock under the production timing.
struct Scripted {
    session: VaultSession,
    provider: Arc<ScriptedProvider>,
    recorder: Arc<Recorder>,
    clock: Arc<FakeClock>,
    _tmp: tempfile::TempDir,
}

fn scripted_vault(
    seed: &[(&str, &str)],
) -> (tempfile::TempDir, Arc<ScriptedProvider>, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    for (relative, contents) in seed {
        write(tmp.path(), relative, contents);
    }
    let provider = ScriptedProvider::new(tmp.path());
    let dyn_provider: Arc<dyn crate::VaultProvider> = provider.clone();
    let session =
        VaultSession::open(dyn_provider, SessionConfig::new(tmp.path().join(".slate"))).unwrap();
    (tmp, provider, session)
}

fn scripted(seed: &[(&str, &str)]) -> Scripted {
    let (tmp, provider, session) = scripted_vault(seed);
    let recorder = Recorder::detached(&session);
    let clock = FakeClock::new();
    assert!(session.start_watching_with(
        WatchTiming::DEFAULT,
        clock.clone() as Arc<dyn WatchClock>,
        WatchDrive::Manual
    ));
    Scripted {
        session,
        provider,
        recorder,
        clock,
        _tmp: tmp,
    }
}

impl Scripted {
    /// Advance the fake clock and step the coalescer.
    fn after(&self, by: Duration) -> usize {
        self.clock.advance(by);
        self.session.drive_watch_for_tests();
        self.recorder.count()
    }

    fn step(&self) -> usize {
        self.after(Duration::ZERO)
    }

    fn rescan(&self) -> ScanReport {
        self.session
            .rescan_with_progress(&CancelToken::new(), None)
            .unwrap()
    }
}

const MS: Duration = Duration::from_millis(1);

/// Design §5: each constant is pinned.
#[test]
fn the_watch_constants_are_pinned() {
    assert_eq!(WATCH_QUIET, Duration::from_secs(1));
    assert_eq!(WATCH_CEILING, Duration::from_secs(10));
    assert_eq!(WATCH_SPACING, Duration::from_secs(5));
    assert_eq!(WATCH_DUTY_FACTOR, 2);
    assert_eq!(WATCH_TOUCHED_PATH_BOUND, 4096);
    assert_eq!(WATCHER_SENTENCE_SPACING_MS, 60_000);
    assert_eq!(
        WatchTiming::DEFAULT,
        WatchTiming {
            quiet: WATCH_QUIET,
            ceiling: WATCH_CEILING,
            spacing: WATCH_SPACING,
            duty_factor: WATCH_DUTY_FACTOR,
            touched_path_bound: WATCH_TOUCHED_PATH_BOUND,
        }
    );
}

/// §4.1, §4.2 step 1: a burst of events closes 1 s after its last event
/// into ONE callback, and nothing follows it.
#[test]
fn a_burst_coalesces_into_one_callback() {
    let s = scripted(&[]);
    for path in ["a.md", "b.md", "notes/c.md"] {
        s.provider.touched(path);
        assert_eq!(s.step(), 0);
        assert_eq!(s.after(Duration::from_millis(400)), 0);
    }
    // The last event came at 800 ms; the burst closes at 1.8 s.
    assert_eq!(s.clock.elapsed(), Duration::from_millis(1200));
    assert_eq!(
        s.after(Duration::from_millis(599)),
        0,
        "inside the quiet window"
    );
    assert_eq!(s.after(MS), 1, "the quiet window closed the burst");
    assert_eq!(
        s.session.watch_decisions_for_tests(),
        vec![WatchDecision::CalledBack {
            paths: vec!["a.md".into(), "b.md".into(), "notes/c.md".into()],
            overflowed: false
        }]
    );
    assert_eq!(
        s.after(Duration::from_secs(60)),
        1,
        "one callback per burst"
    );
}

/// §4.1: a continuous stream never goes quiet, so the burst closes 10 s
/// after its first event; the next one waits out the spacing.
#[test]
fn a_continuous_stream_closes_at_the_ceiling() {
    let s = scripted(&[]);
    for tick in 0..20 {
        s.provider.touched(&format!("stream-{tick}.md"));
        assert_eq!(s.step(), 0, "at {:?}", s.clock.elapsed());
        assert_eq!(
            s.after(Duration::from_millis(499)),
            0,
            "at {:?}",
            s.clock.elapsed()
        );
        s.clock.advance(MS);
    }
    assert_eq!(s.clock.elapsed(), Duration::from_secs(10));
    assert_eq!(s.step(), 1, "the ceiling closed the burst at 10 s");

    // The stream goes on; its next burst closes at the quiet window
    // (13.5 s) and waits for the spacing after the callback (15 s).
    for tick in 0..5 {
        assert_eq!(s.after(Duration::from_millis(500)), 1);
        s.provider.touched(&format!("more-{tick}.md"));
    }
    assert_eq!(s.clock.elapsed(), Duration::from_millis(12_500));
    assert_eq!(
        s.after(Duration::from_millis(2499)),
        1,
        "the spacing holds it"
    );
    assert_eq!(s.after(MS), 2);
}

/// §4.1: a callback waits until at least 5 s after the previous callback
/// and after the last scan ended, stretched to twice that scan's length.
#[test]
fn callbacks_wait_for_the_spacing_and_the_duty_factor() {
    // The spacing after a callback.
    let s = scripted(&[]);
    s.provider.touched("one.md");
    assert_eq!(
        s.after(WATCH_QUIET),
        1,
        "no scan and no callback yet: at once"
    );
    s.provider.touched("two.md");
    assert_eq!(s.after(WATCH_QUIET), 1, "closed at 2 s, held to 6 s");
    assert_eq!(s.after(Duration::from_millis(3999)), 1);
    assert_eq!(s.after(MS), 2, "5 s after the first callback");

    // The duty factor after a 4 s scan: 8 s, not 5.
    let s = scripted(&[("seed.md", "# Seed\n")]);
    let clock = s.clock.clone();
    crate::session::scan_point_test_hook::install(Box::new(move |point| {
        if point == "maintenance" {
            clock.advance(Duration::from_secs(4));
        }
    }));
    s.rescan();
    crate::session::scan_point_test_hook::clear();
    assert_eq!(
        s.clock.elapsed(),
        Duration::from_secs(4),
        "the scan took 4 s"
    );
    s.provider.touched("after-a-slow-scan.md");
    assert_eq!(s.after(WATCH_QUIET), 0, "closed at 5 s");
    assert_eq!(s.after(Duration::from_millis(6999)), 0, "held to 4 s + 8 s");
    assert_eq!(s.after(MS), 1);

    // After a 1 s scan the 5 s spacing dominates its 2 s duty share.
    let s = scripted(&[("seed.md", "# Seed\n")]);
    let clock = s.clock.clone();
    crate::session::scan_point_test_hook::install(Box::new(move |point| {
        if point == "maintenance" {
            clock.advance(Duration::from_secs(1));
        }
    }));
    s.rescan();
    crate::session::scan_point_test_hook::clear();
    s.provider.touched("after-a-quick-scan.md");
    assert_eq!(s.after(WATCH_QUIET), 0);
    assert_eq!(s.after(Duration::from_millis(3999)), 0, "held to 1 s + 5 s");
    assert_eq!(s.after(MS), 1);
}

/// §4.3: a burst that arrives while a scan holds the connection is not
/// delivered during the scan, survives it (its event came after the scan
/// began), and is delivered after the spacing the scan's length sets.
#[test]
fn a_burst_during_a_scan_is_delivered_after_the_spacing() {
    let s = scripted(&[("seed.md", "# Seed\n")]);
    let coalescer = s
        .session
        .watch_coalescer_for_tests()
        .and_then(|weak| weak.upgrade())
        .unwrap();
    let (clock, provider, recorder) = (s.clock.clone(), s.provider.clone(), s.recorder.clone());
    let called_during_the_scan = Arc::new(AtomicBool::new(false));
    let witness = called_during_the_scan.clone();
    crate::session::scan_point_test_hook::install(Box::new(move |point| {
        if point == "file" {
            clock.advance(Duration::from_secs(1));
            provider.touched("during.md");
            clock.advance(Duration::from_secs(3));
            // Past the burst's quiet window, but the scan still runs.
            coalescer.drive();
            witness.fetch_or(recorder.count() > 0, Ordering::SeqCst);
        }
    }));
    let report = s.rescan();
    crate::session::scan_point_test_hook::clear();
    assert!(report.complete);
    assert!(!called_during_the_scan.load(Ordering::SeqCst));
    // The scan ran 0 s → 4 s: the callback waits to 4 s + 2 × 4 s.
    assert_eq!(s.clock.elapsed(), Duration::from_secs(4));
    assert_eq!(s.step(), 0);
    assert_eq!(s.after(Duration::from_millis(7999)), 0);
    assert_eq!(s.after(MS), 1, "delivered after the spacing");
    assert_eq!(
        s.session.watch_decisions_for_tests(),
        vec![WatchDecision::CalledBack {
            paths: vec!["during.md".into()],
            overflowed: false
        }]
    );
}

/// §4.3: a closed, uncalled burst whose last event came before a later
/// scan began is dropped once that scan commits with a complete walk; a
/// scan that does not commit keeps it.
#[test]
fn a_burst_seen_by_a_later_scan_is_dropped() {
    let s = scripted(&[("seed.md", "# Seed\n")]);
    s.rescan(); // at 0 s: the next callback waits until 5 s
    s.clock.advance(Duration::from_secs(1));
    s.provider.touched("seen.md");
    assert_eq!(
        s.after(Duration::from_millis(1500)),
        0,
        "closed at 2 s, pending"
    );
    s.clock.advance(Duration::from_millis(500));
    s.rescan(); // starts at 3 s, after the burst's last event
    assert_eq!(
        s.session.watch_decisions_for_tests(),
        vec![WatchDecision::DroppedSeenByScan]
    );
    assert_eq!(s.after(Duration::from_secs(60)), 0, "the scan saw it");

    // A scan that did not commit vouches for nothing.
    let s = scripted(&[("seed.md", "# Seed\n")]);
    s.rescan();
    s.clock.advance(Duration::from_secs(1));
    s.provider.touched("unseen.md");
    assert_eq!(s.after(Duration::from_millis(1500)), 0);
    s.clock.advance(Duration::from_millis(500));
    let cancelled = CancelToken::new();
    cancelled.cancel();
    assert!(s.session.rescan_with_progress(&cancelled, None).is_err());
    assert_eq!(
        s.after(Duration::from_millis(4999)),
        0,
        "spaced from that scan's end"
    );
    assert_eq!(s.after(MS), 1, "still delivered");
}

/// §4.1, §4.6: Slate's own save is an echo and calls nothing; the same
/// path in a burst the platform overflowed calls back once, with no echo
/// test (its path set is incomplete).
#[test]
fn an_overflow_calls_back_once_and_skips_the_echo_filter() {
    let s = scripted(&[("a.md", "# A\n")]);
    s.session
        .save_text("a.md", "# A\n\nSaved by Slate.\n", None)
        .unwrap();
    s.provider.touched("a.md");
    assert_eq!(s.after(WATCH_QUIET), 0, "Slate's own save");
    assert_eq!(
        s.session.watch_decisions_for_tests(),
        vec![WatchDecision::DroppedAsEchoes {
            paths: vec!["a.md".into()]
        }]
    );
    let echo_tests = s.provider.echo_tests.load(Ordering::SeqCst);

    s.provider.touched("a.md");
    s.provider.emit(FileEvent::Rescan { relative: None });
    assert_eq!(s.after(WATCH_QUIET), 1, "one catch-up callback");
    assert_eq!(
        s.provider.echo_tests.load(Ordering::SeqCst),
        echo_tests,
        "no echo test for an overflowed burst"
    );
    assert_eq!(s.after(Duration::from_secs(60)), 1, "once");
    assert!(
        s.session.watch_is_live_for_tests(),
        "an overflow does not end the watch"
    );
}

/// §4.6: a backend error makes one catch-up callback, then the watch ends:
/// later events call nothing and the session does not re-arm.
#[test]
fn a_backend_error_calls_back_once_then_ends_the_watch() {
    let s = scripted(&[]);
    s.provider.touched("a.md");
    s.provider.emit(FileEvent::Ended {
        cause: WatchEnd::Failed,
    });
    assert_eq!(
        s.step(),
        1,
        "the catch-up does not wait for the quiet window"
    );
    assert!(!s.session.watch_is_live_for_tests());
    assert_eq!(
        s.session.watch_decisions_for_tests(),
        vec![
            WatchDecision::CalledBack {
                paths: Vec::new(),
                overflowed: true
            },
            WatchDecision::Ended(WatchEnd::Failed)
        ]
    );
    s.provider.touched("b.md");
    assert_eq!(s.after(Duration::from_secs(60)), 1);
    let _switch = switch_shared();
    assert!(
        !s.session.start_watching(),
        "an ended watch is not re-armed"
    );
    assert_eq!(s.provider.watch_calls.load(Ordering::SeqCst), 1);
}

/// §4.1: 4,096 touched paths are one named burst, echo-tested path by path;
/// one more overflows it into one callback with no echo test.
#[test]
fn the_touched_path_bound_overflows_into_one_callback() {
    let s = scripted(&[]);
    for index in 0..WATCH_TOUCHED_PATH_BOUND {
        s.provider.touched(&format!("bound/{index}.md"));
    }
    assert_eq!(s.after(WATCH_QUIET), 1);
    assert_eq!(
        s.provider.echo_tests.load(Ordering::SeqCst),
        WATCH_TOUCHED_PATH_BOUND
    );
    assert!(matches!(
        s.session.watch_decisions_for_tests().as_slice(),
        [WatchDecision::CalledBack { paths, overflowed: false }] if paths.len() == WATCH_TOUCHED_PATH_BOUND
    ));

    s.clock.advance(WATCH_SPACING);
    s.session.reset_watch_decisions_for_tests();
    for index in 0..=WATCH_TOUCHED_PATH_BOUND {
        s.provider.touched(&format!("over/{index}.md"));
    }
    assert_eq!(
        s.after(WATCH_QUIET),
        2,
        "one callback for the overflowed burst"
    );
    assert_eq!(
        s.provider.echo_tests.load(Ordering::SeqCst),
        WATCH_TOUCHED_PATH_BOUND,
        "no echo test once the bound is passed"
    );
    assert_eq!(
        s.session.watch_decisions_for_tests(),
        vec![WatchDecision::CalledBack {
            paths: Vec::new(),
            overflowed: true
        }]
    );
}

/// §4.3, §4.6: `start_watching` is idempotent while live, re-arms after
/// `stop_watching`, and returns false — arming nothing — when the provider
/// has no watch or the platform refuses one.
#[test]
fn start_watching_is_idempotent_and_false_for_an_unavailable_provider() {
    let _switch = switch_shared();
    let (_tmp, provider, session) = scripted_vault(&[]);
    *provider.arm.lock().unwrap() = Arm::Unavailable;
    assert!(!session.start_watching());
    *provider.arm.lock().unwrap() = Arm::Refused;
    assert!(!session.start_watching());
    assert!(!session.watch_is_live_for_tests());
    assert_eq!(provider.watch_calls.load(Ordering::SeqCst), 2);

    *provider.arm.lock().unwrap() = Arm::Live;
    assert!(session.start_watching());
    assert!(session.start_watching(), "idempotent");
    assert_eq!(provider.watch_calls.load(Ordering::SeqCst), 3, "armed once");
    session.stop_watching();
    session.stop_watching();
    assert!(!session.watch_is_live_for_tests());
    assert!(session.start_watching(), "re-armed after a stop");
    assert_eq!(provider.watch_calls.load(Ordering::SeqCst), 4);

    // The platform provider on a local vault arms.
    let tmp = tempfile::tempdir().unwrap();
    let local = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    assert!(local.start_watching());
    drop(local);
}

/// §4.7: `SLATE_TEST_DISABLE_VAULT_WATCHER` keeps the watcher off — the
/// provider is never asked — so a host harness's Refresh counts stay
/// deterministic; unset, the same session arms.
#[test]
fn the_test_switch_keeps_the_watcher_off() {
    let _exclusive = WATCHER_SWITCH
        .write()
        .unwrap_or_else(std::sync::PoisonError::into_inner);
    let (_tmp, provider, session) = scripted_vault(&[]);
    // SAFETY (edition 2024): every fact that reads this variable holds
    // WATCHER_SWITCH, shared, so none reads it while it changes here.
    unsafe { std::env::set_var("SLATE_TEST_DISABLE_VAULT_WATCHER", "1") };
    let switched_off = session.start_watching();
    unsafe { std::env::set_var("SLATE_TEST_DISABLE_VAULT_WATCHER", "0") };
    let zero_is_off = session.start_watching();
    session.stop_watching();
    unsafe { std::env::remove_var("SLATE_TEST_DISABLE_VAULT_WATCHER") };
    assert!(!switched_off);
    assert!(zero_is_off, "\"0\" does not disable the watcher");
    assert_eq!(provider.watch_calls.load(Ordering::SeqCst), 1);
    assert!(session.start_watching());
}

/// §4.4 step 2: when the root no longer names the anchored folder at burst
/// close, the watch ends without a callback.
#[test]
fn a_root_mismatch_at_burst_close_ends_the_watch_without_a_callback() {
    let s = scripted(&[]);
    s.provider.touched("a.md");
    s.provider.intact.store(false, Ordering::SeqCst);
    assert_eq!(s.after(WATCH_QUIET), 0);
    assert_eq!(
        s.session.watch_decisions_for_tests(),
        vec![WatchDecision::RootMismatch]
    );
    assert!(!s.session.watch_is_live_for_tests());
    s.provider.intact.store(true, Ordering::SeqCst);
    s.provider.touched("b.md");
    assert_eq!(s.after(Duration::from_secs(60)), 0);
}

/// §4.4: a root-change event ends the watch, with no callback for the burst
/// it interrupts. While the re-arm is off (`WATCH_REARM_ENABLED`, until
/// #1513), the event ends it at once on every platform, as PR 20 decided.
/// With the switch on, macOS and Linux decide the end when that burst closes,
/// against the held root (plan 43 PR 22a). This root never moved, so the
/// change alone ends the watch either way.
#[test]
fn a_root_change_event_ends_the_watch_without_a_callback() {
    let s = scripted(&[]);
    s.provider.touched("a.md");
    s.provider.emit(FileEvent::Ended {
        cause: WatchEnd::RootChanged,
    });
    assert_eq!(s.after(Duration::from_secs(60)), 0);
    assert_eq!(
        s.session.watch_decisions_for_tests(),
        vec![WatchDecision::Ended(WatchEnd::RootChanged)]
    );
    assert!(!s.session.watch_is_live_for_tests());
}

/// §4.4 step 1: the watch arms, then the root it observed is compared with
/// the session's own anchor; a mismatch disarms and `start_watching` says
/// false.
#[test]
fn a_root_mismatch_at_arm_time_refuses_the_watch() {
    let _switch = switch_shared();
    let (_tmp, provider, mut session) = scripted_vault(&[]);
    session.root_identity = Some(VaultRootIdentity {
        device: 7,
        inode: 42,
    });
    *provider.identity.lock().unwrap() = Some(RootIdentity::from_device_inode(7, 43));
    assert!(
        !session.start_watching(),
        "another folder at the root's path"
    );
    *provider.identity.lock().unwrap() = Some(RootIdentity::from_device_inode(7, 42));
    assert!(session.start_watching(), "the folder the session opened");
}
