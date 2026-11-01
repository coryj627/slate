// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The vault watcher's session half (plan 43 PR 20a, #1267; design
//! `docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md`
//! §4.1–§4.6): the coalescer that turns the provider's events into at most
//! one `VaultEventListener::on_external_change` call per burst, and
//! `VaultSession::start_watching` / `stop_watching`.
//!
//! Three threads, one direction (§4.3):
//!
//! 1. **The platform's thread** (notify's FSEvents run loop,
//!    ReadDirectoryChangesW completion routine or inotify loop) runs the
//!    provider's translation and [`CoalescerSink`], which only inserts into
//!    the bounded burst set under the coalescer's own lock and signals it.
//! 2. **The coalescer's thread** ("slate-watch"), owned by the session like
//!    the compaction worker and joined by `stop_watching` and `Drop`, holds
//!    `Arc`s to the provider and the listener registry, never to the session.
//!    It sleeps on a condvar with a deadline (no polling), closes bursts,
//!    waits out the spacing, runs the echo test through the provider (no
//!    session lock), re-verifies the root anchor, then calls every listener
//!    once with no core lock held.
//! 3. **The host's rescan worker** runs the Watcher-reason rescan under its
//!    own token, exactly as for Refresh (PRs 20b, 20c).
//!
//! The callback carries no paths. External edits never surface as
//! file-change events (locked 05 §4.4 as amended by PR 20): the watcher only
//! asks the host to reconcile, and the reconciliation is the rescan.
//!
//! The timing, all pinned by facts (§4.1, §5): a burst closes [`WATCH_QUIET`]
//! after its last event or [`WATCH_CEILING`] after its first; its callback
//! waits until at least [`WATCH_SPACING`] after this session's last scan
//! ended, stretched to [`WATCH_DUTY_FACTOR`] times that scan's length, and at
//! least [`WATCH_SPACING`] after the previous callback; a burst that touches
//! more than [`WATCH_TOUCHED_PATH_BOUND`] paths overflows into one callback
//! with no echo test. A closed, uncalled burst whose last event came before
//! the start of a scan that then committed with a complete walk is dropped:
//! that walk saw the change, so a Refresh leaves no redundant watcher run
//! behind it.
//!
//! Never silent, never noisy (§4.6): a watch that cannot start, overflows or
//! ends leaves Refresh and the foreground rescan as the baseline, logs a
//! diagnostic with no path (the #507 privacy rule) and tells the user
//! nothing.

use std::collections::{BTreeSet, HashMap};
use std::panic::AssertUnwindSafe;
use std::sync::{Arc, Condvar, Mutex, MutexGuard, PoisonError};
use std::thread::JoinHandle;
use std::time::{Duration, Instant};

#[cfg(test)]
use std::sync::Weak;

use super::{VaultEventListener, VaultSession};
use crate::VaultError;
use crate::vault::{
    FileEvent, FileEventSink, Relocation, RootIdentity, VaultProvider, WatchEnd, WatchHandle,
};

/// A burst closes this long after its last event: the quiet window (§4.1).
pub(crate) const WATCH_QUIET: Duration = Duration::from_secs(1);

/// ...or this long after its first, however busy it stays: the ceiling a
/// pure trailing debounce lacks under continuous churn (#638's finding on
/// the sync-marker watchers).
pub(crate) const WATCH_CEILING: Duration = Duration::from_secs(10);

/// A callback waits until at least this long after this session's last scan
/// ended and after the previous callback (the foreground cooldown's figure).
pub(crate) const WATCH_SPACING: Duration = Duration::from_secs(5);

/// ...stretched to this multiple of the last scan's length, so watcher scans
/// hold the writer lock at most a third of the time on a slow vault.
pub(crate) const WATCH_DUTY_FACTOR: u32 = 2;

/// A burst that touches more paths than this overflows: one callback, no
/// echo test (§4.1). With the echo ledger's bound it keeps the watcher's
/// memory fixed whatever a sync client does (locked 05 §4 principle 4).
pub(crate) const WATCH_TOUCHED_PATH_BOUND: usize = 4096;

/// A session re-arms its watch after a legitimate rename of the vault folder
/// at most this many times (plan 43 PR 22a, #940; design §4.3), so a storm
/// of renames cannot loop the watcher; the next one ends the watch. It
/// counts only while [`WATCH_REARM_ENABLED`] is on.
pub(crate) const WATCH_REARM_BOUND: u32 = 4;

/// Whether the watch re-arms after a legitimate rename of the vault folder
/// (plan 43 PR 22a, #940; design §4.3, owner decision Q3). The re-arm is
/// built, and the owner's decision on #1518 (2026-10-19) keeps it off until
/// #1513 moves the provider's reads beneath the held root. Until then every
/// rescan a re-armed watch started would read the vault by the path it was
/// opened at and fail, and the host would say "Files refreshed with errors"
/// once a minute for no gain.
///
/// While it is off, a renamed vault folder ends the watch, as PR 20 decided
/// (§4.4), on every platform:
/// - the root-change event ends it at once;
/// - a root found moved when a burst closes ends it with no callback;
/// - Refresh and the foreground rescan remain.
///
/// This is the one switch. Turning it on with #1513 brings back:
/// - the burst-close decision (`WatchCoalescer::relocate`);
/// - the re-arm;
/// - `a_legitimate_root_rename_rearms_at_the_new_path`;
/// - mac's `testARenamedVaultFolderRearmsTheWatcherAtItsNewPath`, which reads
///   the switch through the FFI (`vault_watch_rearm_enabled`).
pub const WATCH_REARM_ENABLED: bool = false;

/// At most one watcher sentence per this many milliseconds per vault (§4.5,
/// owner decision Q2: one leading sentence a minute). The hosts' speech
/// policy reads it through the FFI (`watcher_sentence_spacing_ms`) so both
/// use one number; core itself never speaks for the watcher.
pub const WATCHER_SENTENCE_SPACING_MS: u64 = 60_000;

/// `start_watching` returns false while this is set to anything but empty or
/// `0` (design §4.7, the `SLATE_TEST_FAULT_*` precedent): the hosts' unit
/// harnesses and Refresh journeys set it so a Refresh's counts stay
/// deterministic, and the watcher facts leave it unset.
pub(crate) const DISABLE_VAULT_WATCHER_ENV: &str = "SLATE_TEST_DISABLE_VAULT_WATCHER";

fn watcher_disabled_by_test_switch() -> bool {
    std::env::var_os(DISABLE_VAULT_WATCHER_ENV)
        .is_some_and(|value| !value.is_empty() && value != "0")
}

/// The coalescer's timing. Production runs [`WatchTiming::DEFAULT`]; the
/// facts on the real backend shorten it, the fake-clock facts pin it.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) struct WatchTiming {
    pub(crate) quiet: Duration,
    pub(crate) ceiling: Duration,
    pub(crate) spacing: Duration,
    pub(crate) duty_factor: u32,
    pub(crate) touched_path_bound: usize,
}

impl WatchTiming {
    pub(crate) const DEFAULT: Self = Self {
        quiet: WATCH_QUIET,
        ceiling: WATCH_CEILING,
        spacing: WATCH_SPACING,
        duty_factor: WATCH_DUTY_FACTOR,
        touched_path_bound: WATCH_TOUCHED_PATH_BOUND,
    };
}

/// Time as the coalescer reads it: the monotonic clock in production, a
/// fake one in the deterministic facts.
pub(crate) trait WatchClock: Send + Sync {
    fn now(&self) -> Instant;
}

/// The monotonic clock.
pub(crate) struct SystemClock;

impl WatchClock for SystemClock {
    fn now(&self) -> Instant {
        Instant::now()
    }
}

/// A clock a fact advances by hand.
#[cfg(test)]
pub(crate) struct FakeClock {
    base: Instant,
    elapsed: Mutex<Duration>,
}

#[cfg(test)]
impl FakeClock {
    pub(crate) fn new() -> Arc<Self> {
        Arc::new(Self {
            base: Instant::now(),
            elapsed: Mutex::new(Duration::ZERO),
        })
    }

    pub(crate) fn advance(&self, by: Duration) {
        *self.elapsed.lock().unwrap() += by;
    }

    /// The fake time since the clock was made.
    pub(crate) fn elapsed(&self) -> Duration {
        *self.elapsed.lock().unwrap()
    }
}

#[cfg(test)]
impl WatchClock for FakeClock {
    fn now(&self) -> Instant {
        self.base + *self.elapsed.lock().unwrap()
    }
}

/// How the coalescer runs: on its own thread, or stepped by a fact.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum WatchDrive {
    Thread,
    #[cfg(test)]
    Manual,
}

/// What the coalescer decided, kept for the facts.
#[cfg(test)]
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum WatchDecision {
    /// The listeners were called once; `paths` are the non-echo paths.
    CalledBack {
        paths: Vec<String>,
        overflowed: bool,
    },
    /// Every touched path was Slate's own echo: nothing was called.
    DroppedAsEchoes { paths: Vec<String> },
    /// A scan that started after the burst's last event committed with a
    /// complete walk: it saw the change.
    DroppedSeenByScan,
    /// The root no longer named the anchored folder at burst close.
    RootMismatch,
    /// The watch ended on the platform's word.
    Ended(WatchEnd),
    /// The vault folder was renamed and the watch re-armed at the held
    /// root's verified current path (plan 43 PR 22a, design §4.3).
    Rearmed { at: std::path::PathBuf },
}

type ListenerRegistry = Arc<Mutex<HashMap<u64, Arc<dyn VaultEventListener>>>>;

/// One burst of touched paths.
#[derive(Debug)]
struct Burst {
    first: Instant,
    last: Instant,
    paths: BTreeSet<String>,
    /// The path set is incomplete (the platform dropped events, a path could
    /// not be named, or the bound was passed): the echo test is skipped.
    overflowed: bool,
    /// The platform said the root changed during this burst (plan 43 PR
    /// 22a): when it closes, the held root decides between a re-arm and the
    /// end of the watch (`WatchCoalescer::relocate`). Set only while the
    /// watch follows a renamed root ([`WatchCoalescer::follows_a_renamed_root`]).
    root_changed: bool,
}

impl Burst {
    fn new(at: Instant) -> Self {
        Self {
            first: at,
            last: at,
            paths: BTreeSet::new(),
            overflowed: false,
            root_changed: false,
        }
    }

    fn closes_at(&self, timing: &WatchTiming) -> Instant {
        (self.last + timing.quiet).min(self.first + timing.ceiling)
    }

    fn touch(&mut self, at: Instant, path: Option<String>, bound: usize) {
        self.last = self.last.max(at);
        match path {
            _ if self.overflowed => {}
            Some(path) if self.paths.len() < bound || self.paths.contains(&path) => {
                self.paths.insert(path);
            }
            _ => self.overflow(),
        }
    }

    fn overflow(&mut self) {
        self.overflowed = true;
        self.paths = BTreeSet::new();
    }

    /// Fold a later-closed burst in: bursts that close while a callback waits
    /// out the spacing merge into it (§4.2 step 1).
    fn absorb(&mut self, other: Burst, bound: usize) {
        self.first = self.first.min(other.first);
        self.last = self.last.max(other.last);
        self.root_changed |= other.root_changed;
        if other.overflowed {
            self.overflow();
            return;
        }
        for path in other.paths {
            if self.overflowed {
                break;
            }
            self.touch(self.last, Some(path), bound);
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum Phase {
    Live,
    /// The watch ended itself (a root swapped, lost or changed and back, a
    /// backend error after its catch-up). An ended watch is final for the
    /// session; only a live one re-arms after a rename (plan 43 PR 22a).
    Ended(WatchEnd),
    /// The host stopped it, or the session closed.
    Stopped,
}

struct State {
    phase: Phase,
    /// The burst still collecting events.
    open: Option<Burst>,
    /// Closed bursts waiting for the spacing or a running scan.
    pending: Option<Burst>,
    /// A backend error arrived: its catch-up is delivered, then the watch
    /// ends; later events are ignored (the catch-up's full rescan covers
    /// them).
    failing: bool,
    scan_started: Option<Instant>,
    last_scan_end: Option<Instant>,
    last_scan_duration: Duration,
    last_callback: Option<Instant>,
    #[cfg(test)]
    decisions: Vec<WatchDecision>,
}

impl State {
    fn touch(&mut self, at: Instant, path: Option<String>, bound: usize) {
        self.open
            .get_or_insert_with(|| Burst::new(at))
            .touch(at, path, bound);
    }
}

/// A closed burst on its way to the listeners.
struct Delivery {
    paths: Vec<String>,
    overflowed: bool,
    /// The catch-up of a backend error: the watch ends after it.
    ending: bool,
}

enum Step {
    Deliver(Delivery),
    /// A closed burst saw the root change: decide its fate first.
    Relocate,
    Wait(Option<Instant>),
    Exit,
}

/// The session-owned coalescer (§4.3).
pub(crate) struct WatchCoalescer {
    timing: WatchTiming,
    clock: Arc<dyn WatchClock>,
    provider: Arc<dyn VaultProvider>,
    listeners: ListenerRegistry,
    /// The session's re-arms so far (shared across its watches, design §4.3's
    /// bound of [`WATCH_REARM_BOUND`] per session).
    rearms: Arc<std::sync::atomic::AtomicU32>,
    /// The platform watch and its root anchor; released (and the platform
    /// watch stopped) when the watch ends or stops, never under `state`.
    handle: Mutex<Option<WatchHandle>>,
    state: Mutex<State>,
    wake: Condvar,
}

/// The provider's sink: records into the coalescer and returns. Holds the
/// coalescer weakly, so a platform thread that outlives the session's watch
/// (inotify and ReadDirectoryChangesW stop theirs asynchronously) records
/// nothing.
struct CoalescerSink(std::sync::Weak<WatchCoalescer>);

impl FileEventSink for CoalescerSink {
    fn on_event(&self, event: FileEvent) {
        if let Some(coalescer) = self.0.upgrade() {
            coalescer.accept(event);
        }
    }
}

fn earliest(a: Option<Instant>, b: Option<Instant>) -> Option<Instant> {
    match (a, b) {
        (Some(a), Some(b)) => Some(a.min(b)),
        (a, b) => a.or(b),
    }
}

impl WatchCoalescer {
    fn new(
        timing: WatchTiming,
        clock: Arc<dyn WatchClock>,
        provider: Arc<dyn VaultProvider>,
        listeners: ListenerRegistry,
        rearms: Arc<std::sync::atomic::AtomicU32>,
    ) -> Arc<Self> {
        Arc::new(Self {
            timing,
            clock,
            provider,
            listeners,
            rearms,
            handle: Mutex::new(None),
            state: Mutex::new(State {
                phase: Phase::Live,
                open: None,
                pending: None,
                failing: false,
                scan_started: None,
                last_scan_end: None,
                last_scan_duration: Duration::ZERO,
                last_callback: None,
                #[cfg(test)]
                decisions: Vec::new(),
            }),
            wake: Condvar::new(),
        })
    }

    fn sink(self: &Arc<Self>) -> Arc<dyn FileEventSink> {
        Arc::new(CoalescerSink(Arc::downgrade(self)))
    }

    fn lock_state(&self) -> MutexGuard<'_, State> {
        self.state.lock().unwrap_or_else(PoisonError::into_inner)
    }

    fn is_live(&self) -> bool {
        self.lock_state().phase == Phase::Live
    }

    fn has_ended(&self) -> bool {
        matches!(self.lock_state().phase, Phase::Ended(_))
    }

    /// Whether a root change waits for its burst to close, so the held root
    /// can decide between a re-arm and the end of the watch (plan 43 PR 22a,
    /// §4.3). It needs an anchored target and [`WATCH_REARM_ENABLED`]. When it
    /// is false, a root change ends the watch as PR 20 decided (§4.4).
    fn follows_a_renamed_root() -> bool {
        crate::vault::fs_root::ANCHORED && WATCH_REARM_ENABLED
    }

    /// The platform's thread: record and return (§4.3 step 1).
    fn accept(&self, event: FileEvent) {
        let mut state = self.lock_state();
        if state.phase != Phase::Live || state.failing {
            return;
        }
        let now = self.clock.now();
        let bound = self.timing.touched_path_bound;
        let mut root_changed = false;
        match event {
            FileEvent::Created { relative }
            | FileEvent::Modified { relative }
            | FileEvent::Deleted { relative } => state.touch(now, Some(relative), bound),
            FileEvent::Renamed { from, to } => {
                state.touch(now, Some(from), bound);
                state.touch(now, Some(to), bound);
            }
            FileEvent::Rescan { .. } => state.touch(now, None, bound),
            FileEvent::Ended {
                cause: WatchEnd::RootChanged,
            } if Self::follows_a_renamed_root() => {
                // Plan 43 PR 22a (§4.3): the root was renamed, removed or
                // replaced. Whether this is the session's own vault folder
                // moving (re-arm) or another folder at its path (end) is
                // decided against the held root once the burst closes, on
                // the coalescer's thread, never here.
                state.touch(now, None, bound);
                if let Some(open) = state.open.as_mut() {
                    open.root_changed = true;
                }
            }
            FileEvent::Ended {
                cause: WatchEnd::RootChanged,
            } => {
                // §4.4: a root that moved or was replaced is no longer the
                // session's; end without a callback. This is Windows until
                // 22W, and macOS and Linux while the re-arm is off.
                state.phase = Phase::Ended(WatchEnd::RootChanged);
                state.open = None;
                state.pending = None;
                #[cfg(test)]
                state
                    .decisions
                    .push(WatchDecision::Ended(WatchEnd::RootChanged));
                root_changed = true;
            }
            FileEvent::Ended {
                cause: WatchEnd::Failed,
            } => {
                // §4.6: one catch-up callback, then the watch ends.
                state.touch(now, None, bound);
                state.failing = true;
            }
        }
        drop(state);
        self.wake.notify_all();
        if root_changed {
            log::warn!(
                "vault watch ended: the vault root was renamed, removed or replaced; \
                 Refresh and the foreground rescan remain"
            );
        }
    }

    /// A scan of this session took the connection (§4.3).
    fn scan_started(&self) {
        let now = self.clock.now();
        self.lock_state().scan_started = Some(now);
    }

    /// The scan released the connection; `complete` when it committed with a
    /// complete walk. Such a walk saw every change made before it started, so
    /// a closed or open burst whose last event preceded the start is dropped.
    /// An incomplete walk vouches for nothing: its bursts are delivered.
    fn scan_finished(&self, complete: bool) {
        let mut state = self.lock_state();
        let now = self.clock.now();
        let Some(started) = state.scan_started.take() else {
            return;
        };
        state.last_scan_end = Some(now);
        state.last_scan_duration = now.saturating_duration_since(started);
        if complete {
            let seen = |burst: &Option<Burst>| burst.as_ref().is_some_and(|b| b.last < started);
            if seen(&state.pending) {
                state.pending = None;
                #[cfg(test)]
                state.decisions.push(WatchDecision::DroppedSeenByScan);
            }
            if seen(&state.open) {
                state.open = None;
                #[cfg(test)]
                state.decisions.push(WatchDecision::DroppedSeenByScan);
            }
        }
        drop(state);
        self.wake.notify_all();
    }

    /// When the pending callback may go (§4.1): the spacing after the last
    /// scan, stretched by the duty factor, and the spacing after the last
    /// callback.
    fn due(&self, state: &State) -> Option<Instant> {
        let after_scan = state.last_scan_end.map(|end| {
            end + self.timing.spacing.max(
                state
                    .last_scan_duration
                    .saturating_mul(self.timing.duty_factor),
            )
        });
        let after_callback = state.last_callback.map(|at| at + self.timing.spacing);
        after_scan.max(after_callback)
    }

    fn poll(&self, state: &mut State, now: Instant) -> Step {
        if state.phase != Phase::Live {
            return Step::Exit;
        }
        let bound = self.timing.touched_path_bound;
        if let Some(open) = &state.open
            && (state.failing || now >= open.closes_at(&self.timing))
        {
            let closed = state.open.take().expect("the open burst was just seen");
            match &mut state.pending {
                Some(pending) => pending.absorb(closed, bound),
                None => state.pending = Some(closed),
            }
        }
        let open_closes = state.open.as_ref().map(|open| open.closes_at(&self.timing));
        if state
            .pending
            .as_ref()
            .is_some_and(|pending| pending.root_changed)
        {
            // The root's fate comes first: neither a running scan nor the
            // spacing delays a re-arm or the end of the watch.
            return Step::Relocate;
        }
        if state.pending.is_some() {
            if state.scan_started.is_some() {
                // A scan holds the connection: its end decides (it may have
                // seen the burst), and the spacing then counts from it.
                return Step::Wait(open_closes);
            }
            let due = self.due(state);
            if due.is_none_or(|due| now >= due) {
                let burst = state.pending.take().expect("pending was just seen");
                return Step::Deliver(Delivery {
                    paths: burst.paths.into_iter().collect(),
                    overflowed: burst.overflowed,
                    ending: state.failing,
                });
            }
            return Step::Wait(earliest(due, open_closes));
        }
        if state.failing {
            // The catch-up was seen by a complete scan: end without one.
            state.phase = Phase::Ended(WatchEnd::Failed);
            #[cfg(test)]
            state.decisions.push(WatchDecision::Ended(WatchEnd::Failed));
            return Step::Exit;
        }
        Step::Wait(open_closes)
    }

    /// The coalescer's thread, with no lock held: a burst that saw the root
    /// change re-arms at the held root's verified current path, when the
    /// vault folder was renamed, or ends the watch (plan 43 PR 22a, design
    /// §4.3). The pending burst then carries one catch-up callback: events
    /// in the moved folder between the rename and the re-arm reached no
    /// stream. Reached only while the watch follows a renamed root
    /// ([`Self::follows_a_renamed_root`]); otherwise the root-change event
    /// has already ended the watch.
    fn relocate(self: &Arc<Self>) {
        let relocation = {
            let handle = self.handle.lock().unwrap_or_else(PoisonError::into_inner);
            match handle.as_ref() {
                // Released: the watch stopped meanwhile.
                None => return,
                Some(handle) => handle.relocation(),
            }
        };
        let relocation = match relocation {
            Relocation::Moved(at) if Self::follows_a_renamed_root() => match self.rearm(&at) {
                Ok(()) => return,
                Err(relocation) => relocation,
            },
            other => other,
        };
        #[cfg(test)]
        self.lock_state()
            .decisions
            .push(if relocation == Relocation::Intact {
                WatchDecision::Ended(WatchEnd::RootChanged)
            } else {
                WatchDecision::RootMismatch
            });
        #[cfg(not(test))]
        let _ = relocation;
        log::warn!(
            "vault watch ended: the vault root was removed, replaced, or changed and changed \
             back; Refresh and the foreground rescan remain"
        );
        self.end(WatchEnd::RootChanged);
    }

    /// Re-arm at `at`, the held root's verified current path, within the
    /// session's bound. The old platform watch is dropped outside every
    /// coalescer lock (FSEvents joins its thread, which may be in the sink).
    /// `Err` carries why the watch must end instead.
    fn rearm(self: &Arc<Self>, at: &std::path::Path) -> Result<(), Relocation> {
        use std::sync::atomic::Ordering;
        if self.rearms.load(Ordering::SeqCst) >= WATCH_REARM_BOUND {
            log::warn!("vault watch re-arm bound reached");
            return Err(Relocation::Lost);
        }
        let rearmed = {
            let handle = self.handle.lock().unwrap_or_else(PoisonError::into_inner);
            match handle.as_ref() {
                None => return Ok(()),
                Some(handle) => handle.rearm(at, self.sink()),
            }
        };
        let rearmed = match rearmed {
            Ok(rearmed) => rearmed,
            Err(error) => {
                log::warn!(
                    "vault watch could not re-arm after the vault folder moved ({})",
                    error_kind(&error)
                );
                log::debug!("vault watch re-arm failure detail: {error}");
                return Err(Relocation::Lost);
            }
        };
        let previous = self
            .handle
            .lock()
            .unwrap_or_else(PoisonError::into_inner)
            .replace(rearmed);
        drop(previous);
        self.rearms.fetch_add(1, Ordering::SeqCst);
        {
            let mut state = self.lock_state();
            if let Some(pending) = state.pending.as_mut() {
                pending.root_changed = false;
                pending.overflow();
            }
            #[cfg(test)]
            state.decisions.push(WatchDecision::Rearmed {
                at: at.to_path_buf(),
            });
        }
        log::info!("vault watch re-armed: the vault folder was renamed");
        log::debug!("vault watch re-armed at {}", at.display());
        self.wake.notify_all();
        Ok(())
    }

    /// The coalescer's thread, with no lock held: the echo test, the root
    /// check, then one call per listener (§4.2, §4.3, §4.4).
    fn deliver(self: &Arc<Self>, delivery: Delivery) {
        let Delivery {
            mut paths,
            overflowed,
            ending,
        } = delivery;
        // Slate's own writes are echoes (§4.2). An overflowed burst's path
        // set is incomplete, so it skips the test and calls back.
        if !overflowed {
            #[cfg(test)]
            let touched = paths.clone();
            paths.retain(|path| !self.provider.is_own_echo(path));
            if paths.is_empty() {
                #[cfg(test)]
                self.lock_state()
                    .decisions
                    .push(WatchDecision::DroppedAsEchoes { paths: touched });
                return;
            }
        }
        // §4.4 step 2: the root still names the anchored folder, or the
        // watch ends with no callback. While the watch follows a renamed
        // root (plan 43 PR 22a, §4.3), a vault folder renamed meanwhile
        // re-arms at the held root's verified current path first, and the
        // burst is still delivered. Off, a renamed root ends it here too.
        let relocation = {
            let handle = self.handle.lock().unwrap_or_else(PoisonError::into_inner);
            match handle.as_ref() {
                // Released: the watch stopped meanwhile.
                None => return,
                Some(handle) => handle.relocation(),
            }
        };
        let anchored = match relocation {
            Relocation::Intact => true,
            Relocation::Moved(at) if Self::follows_a_renamed_root() => self.rearm(&at).is_ok(),
            Relocation::Moved(_) | Relocation::Swapped | Relocation::Lost => false,
        };
        if !anchored {
            #[cfg(test)]
            self.lock_state()
                .decisions
                .push(WatchDecision::RootMismatch);
            log::warn!(
                "vault watch ended: the vault root no longer names the folder this session \
                 opened; Refresh and the foreground rescan remain"
            );
            self.end(WatchEnd::RootChanged);
            return;
        }
        if !self.is_live() {
            return;
        }
        // One call per listener, no core lock held (locked 05 §4 principle
        // 5, the compaction worker's `on_error` precedent): the registry is
        // snapshotted first.
        let listeners: Vec<Arc<dyn VaultEventListener>> = self
            .listeners
            .lock()
            .unwrap_or_else(PoisonError::into_inner)
            .values()
            .cloned()
            .collect();
        for listener in listeners {
            let called =
                std::panic::catch_unwind(AssertUnwindSafe(|| listener.on_external_change()));
            if called.is_err() {
                log::warn!("a vault event listener panicked in on_external_change");
            }
        }
        {
            let now = self.clock.now();
            let mut state = self.lock_state();
            state.last_callback = Some(now);
            #[cfg(test)]
            state
                .decisions
                .push(WatchDecision::CalledBack { paths, overflowed });
        }
        if ending {
            #[cfg(test)]
            self.lock_state()
                .decisions
                .push(WatchDecision::Ended(WatchEnd::Failed));
            log::warn!(
                "vault watch ended after a platform watcher error; Refresh and the foreground \
                 rescan remain"
            );
            self.end(WatchEnd::Failed);
        }
    }

    /// The watch ended itself: no further callback, and the platform watch
    /// stops.
    fn end(&self, cause: WatchEnd) {
        {
            let mut state = self.lock_state();
            if state.phase == Phase::Live {
                state.phase = Phase::Ended(cause);
            }
            state.open = None;
            state.pending = None;
        }
        self.wake.notify_all();
        self.release_handle();
    }

    /// The host stopped the watch, or the session is closing.
    fn stop(&self) {
        {
            let mut state = self.lock_state();
            state.phase = Phase::Stopped;
            state.open = None;
            state.pending = None;
        }
        self.wake.notify_all();
        self.release_handle();
    }

    /// Drop the platform watch outside every coalescer lock: FSEvents joins
    /// its run-loop thread, which may be waiting in the sink for `state`.
    fn release_handle(&self) {
        let handle = self
            .handle
            .lock()
            .unwrap_or_else(PoisonError::into_inner)
            .take();
        drop(handle);
    }

    /// The "slate-watch" thread: sleep on the condvar until the next
    /// deadline or a signal, never polling.
    fn run(self: Arc<Self>) {
        let mut state = self.lock_state();
        loop {
            let now = self.clock.now();
            match self.poll(&mut state, now) {
                Step::Exit => break,
                Step::Deliver(delivery) => {
                    drop(state);
                    self.deliver(delivery);
                    state = self.lock_state();
                }
                Step::Relocate => {
                    drop(state);
                    self.relocate();
                    state = self.lock_state();
                }
                Step::Wait(deadline) => {
                    state = match deadline {
                        Some(at) => {
                            self.wake
                                .wait_timeout(state, at.saturating_duration_since(now))
                                .unwrap_or_else(PoisonError::into_inner)
                                .0
                        }
                        None => self
                            .wake
                            .wait(state)
                            .unwrap_or_else(PoisonError::into_inner),
                    };
                }
            }
        }
        drop(state);
        self.release_handle();
    }

    /// A fact's step: run until the coalescer would wait; its next deadline.
    #[cfg(test)]
    pub(crate) fn drive(self: &Arc<Self>) -> Option<Instant> {
        loop {
            let mut state = self.lock_state();
            let now = self.clock.now();
            match self.poll(&mut state, now) {
                Step::Exit => {
                    drop(state);
                    self.release_handle();
                    return None;
                }
                Step::Deliver(delivery) => {
                    drop(state);
                    self.deliver(delivery);
                }
                Step::Relocate => {
                    drop(state);
                    self.relocate();
                }
                Step::Wait(deadline) => return deadline,
            }
        }
    }
}

/// A live watch: its coalescer and the thread that runs it.
struct ActiveWatch {
    coalescer: Arc<WatchCoalescer>,
    thread: Option<JoinHandle<()>>,
}

impl ActiveWatch {
    /// Stop the platform watch first, then signal and join the coalescer
    /// (§4.3): no callback runs after this returns. A coalescer thread that
    /// is itself dropping the session (a listener held the last reference)
    /// is not joined; it exits on its own once its callback returns.
    fn shut_down(self) {
        self.coalescer.stop();
        if let Some(thread) = self.thread
            && thread.thread().id() != std::thread::current().id()
        {
            let _ = thread.join();
        }
    }
}

/// The session's watch seat.
#[derive(Default)]
pub(crate) struct WatchSeat {
    active: Option<ActiveWatch>,
    /// The watch ended itself; it is not restarted in this session.
    ended: bool,
    /// Re-arms after a rename of the vault folder, across the session's
    /// watches (plan 43 PR 22a, bounded by [`WATCH_REARM_BOUND`]).
    rearms: Arc<std::sync::atomic::AtomicU32>,
}

/// The watcher's view of one scan (§4.3): its start once it holds the
/// connection, its end and outcome when it lets go — on every exit, a
/// failed or cancelled scan included (`Drop`).
pub(crate) struct WatchScan {
    coalescer: Option<Arc<WatchCoalescer>>,
    complete: bool,
}

impl WatchScan {
    pub(crate) fn begin(session: &VaultSession) -> Self {
        let coalescer = session.live_watch();
        if let Some(coalescer) = &coalescer {
            coalescer.scan_started();
        }
        Self {
            coalescer,
            complete: false,
        }
    }

    /// The scan committed; `complete` is its report's completeness.
    pub(crate) fn committed(&mut self, complete: bool) {
        self.complete = complete;
    }
}

impl Drop for WatchScan {
    fn drop(&mut self) {
        if let Some(coalescer) = self.coalescer.take() {
            coalescer.scan_finished(self.complete);
        }
    }
}

/// A `VaultError`'s kind for a `warn!` line, never its message (which can
/// name a path, #507).
fn error_kind(error: &VaultError) -> String {
    match error {
        VaultError::Io(io) => format!("{:?}", io.kind()),
        _ => "provider error".to_owned(),
    }
}

impl VaultSession {
    /// Plan 43 PR 20a (#1267): arm the vault watcher. Files created, changed
    /// or removed outside this session then reach every registered
    /// listener's `on_external_change` — at most once per coalesced burst,
    /// with no session lock held — and the host answers with a Watcher-reason
    /// rescan (`RescanReason::Watcher`). Slate's own writes through this
    /// session's provider are echoes and call nothing. A rename of the vault
    /// folder ends the watch, as any root change does (§4.4). Plan 43 PR 22a
    /// (#940) built a re-arm on macOS and Linux: at the held root's verified
    /// current path, up to [`WATCH_REARM_BOUND`] times a session. It sits
    /// behind [`WATCH_REARM_ENABLED`], which is off until #1513.
    ///
    /// Hosts call it on their open worker right before the initial scan
    /// (the arm-then-probe rule of #638: a change during that scan is either
    /// seen by its walk or arrives as a burst), and `stop_watching` first
    /// when closing. Idempotent while the watch is live. Returns false, and
    /// changes nothing the user hears or sees, when the provider has no
    /// watch (`Ok(None)`: host providers), the platform refuses one (an
    /// inotify limit, an unsupported redirector), the root no longer names
    /// the folder this session opened, a watch of this session already ended
    /// itself (an ended watch is not restarted), or `SLATE_TEST_DISABLE_VAULT_WATCHER`
    /// is set. Refresh and the foreground rescan stay the baseline whether or
    /// not a watch is live (design §4.6).
    pub fn start_watching(&self) -> bool {
        if watcher_disabled_by_test_switch() {
            log::debug!("vault watch not started: {DISABLE_VAULT_WATCHER_ENV} is set");
            return false;
        }
        self.start_watching_with(
            WatchTiming::DEFAULT,
            Arc::new(SystemClock),
            WatchDrive::Thread,
        )
    }

    /// `start_watching` with its timing, clock and drive given (the facts'
    /// seam; production passes the defaults).
    pub(crate) fn start_watching_with(
        &self,
        timing: WatchTiming,
        clock: Arc<dyn WatchClock>,
        drive: WatchDrive,
    ) -> bool {
        let mut seat = self.lock_watch_seat();
        if let Some(active) = &seat.active {
            if active.coalescer.is_live() {
                return true;
            }
            // The watch ended itself (a root swapped, lost or changed and
            // back, a backend error after its catch-up): it is final.
            let ended = seat.active.take().expect("the active watch was just seen");
            seat.ended = true;
            drop(seat);
            ended.shut_down();
            return false;
        }
        if seat.ended {
            return false;
        }

        let coalescer = WatchCoalescer::new(
            timing,
            clock,
            Arc::clone(&self.provider),
            Arc::clone(&self.event_listeners),
            Arc::clone(&seat.rearms),
        );
        let handle = match self.provider.watch(coalescer.sink()) {
            Ok(Some(handle)) => handle,
            Ok(None) => {
                log::debug!(
                    "vault watch unavailable for this provider; Refresh and the foreground \
                     rescan remain"
                );
                return false;
            }
            Err(error) => {
                log::warn!(
                    "vault watch could not start ({}); Refresh and the foreground rescan remain",
                    error_kind(&error)
                );
                log::debug!("vault watch start failure detail: {error}");
                return false;
            }
        };
        // §4.4 step 1: arm, then verify the root the watch observed against
        // the root this session holds, in the watch's own terms (Unix: the
        // held root's `(st_dev, st_ino)`; Windows: the held handle's volume
        // serial and file index, never its `FileIdInfo` record — WB-28, C9).
        if let Some((volume, file)) = self.watch_anchor
            && handle.arm_identity() != Some(RootIdentity::from_device_inode(volume, file))
        {
            log::warn!(
                "vault watch refused: the vault root no longer names the folder this session \
                 opened; Refresh and the foreground rescan remain"
            );
            return false;
        }
        *coalescer
            .handle
            .lock()
            .unwrap_or_else(PoisonError::into_inner) = Some(handle);

        let thread = match drive {
            WatchDrive::Thread => {
                let runner = Arc::clone(&coalescer);
                match std::thread::Builder::new()
                    .name("slate-watch".into())
                    .spawn(move || runner.run())
                {
                    Ok(thread) => Some(thread),
                    Err(error) => {
                        log::warn!(
                            "vault watch could not start its thread ({:?})",
                            error.kind()
                        );
                        coalescer.stop();
                        return false;
                    }
                }
            }
            #[cfg(test)]
            WatchDrive::Manual => None,
        };
        seat.active = Some(ActiveWatch { coalescer, thread });
        true
    }

    /// Plan 43 PR 20a (#1267): disarm the vault watcher. The platform watch
    /// stops, then the coalescer is signalled and joined, so no
    /// `on_external_change` call runs after this returns. Idempotent; a
    /// session that closes stops its watch itself. Hosts call it first when
    /// closing a vault.
    pub fn stop_watching(&self) {
        let active = {
            let mut seat = self.lock_watch_seat();
            let active = seat.active.take();
            if active
                .as_ref()
                .is_some_and(|active| active.coalescer.has_ended())
            {
                seat.ended = true;
            }
            active
        };
        if let Some(active) = active {
            active.shut_down();
        }
    }

    fn lock_watch_seat(&self) -> MutexGuard<'_, WatchSeat> {
        self.watch.lock().unwrap_or_else(PoisonError::into_inner)
    }

    /// The coalescer while a watch is armed, for a scan's notices.
    fn live_watch(&self) -> Option<Arc<WatchCoalescer>> {
        self.lock_watch_seat()
            .active
            .as_ref()
            .map(|active| Arc::clone(&active.coalescer))
    }

    /// Run a manually driven coalescer until it would wait; its next
    /// deadline.
    #[cfg(test)]
    pub(crate) fn drive_watch_for_tests(&self) -> Option<Instant> {
        self.live_watch().and_then(|coalescer| coalescer.drive())
    }

    #[cfg(test)]
    pub(crate) fn watch_decisions_for_tests(&self) -> Vec<WatchDecision> {
        self.live_watch()
            .map(|coalescer| coalescer.lock_state().decisions.clone())
            .unwrap_or_default()
    }

    #[cfg(test)]
    pub(crate) fn reset_watch_decisions_for_tests(&self) {
        if let Some(coalescer) = self.live_watch() {
            coalescer.lock_state().decisions.clear();
        }
    }

    #[cfg(test)]
    pub(crate) fn watch_is_live_for_tests(&self) -> bool {
        self.live_watch()
            .is_some_and(|coalescer| coalescer.is_live())
    }

    /// Live, with no burst collecting and none waiting.
    #[cfg(test)]
    pub(crate) fn watch_is_idle_for_tests(&self) -> bool {
        self.live_watch().is_some_and(|coalescer| {
            let state = coalescer.lock_state();
            state.phase == Phase::Live && state.open.is_none() && state.pending.is_none()
        })
    }

    #[cfg(test)]
    pub(crate) fn watch_has_open_burst_for_tests(&self) -> bool {
        self.live_watch()
            .is_some_and(|coalescer| coalescer.lock_state().open.is_some())
    }

    #[cfg(test)]
    pub(crate) fn watch_coalescer_for_tests(&self) -> Option<Weak<WatchCoalescer>> {
        self.live_watch()
            .map(|coalescer| Arc::downgrade(&coalescer))
    }
}
