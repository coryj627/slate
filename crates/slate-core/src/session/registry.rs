// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-31 (#1484): an idle session releases its page cache while
//! another session works.
//!
//! Every SQLite connection in the process draws pages from one pool
//! (`SQLITE_ENABLE_MEMORY_MANAGEMENT`, which the bundled libsqlite3-sys
//! sets). The WB-31 bench measured the cost on Windows: with a second
//! 10k-note session open, the first one's no-change rescan took 1.5x as
//! long, its cache having fallen from 17 MiB to 4 KiB while it sat idle;
//! releasing the idle session's cache brought it back to 1.0x.
//!
//! Sessions whose `SessionConfig::idle_cache_release_ms` is nonzero register
//! here. One `slate-cache-release` thread, started with the second
//! registration and joined after the last leaves, wakes every interval and
//! runs [`release_pass`]: while two or more sessions are registered, each
//! connection nobody holds, with no operation in progress and idle a full
//! interval since its last use ENDED, has its cache released, once per idle
//! period. A lone session is never released, so the one-session case is
//! untouched.
//!
//! SQLite's shrink (`sqlite3_db_release_memory`) empties the whole shared
//! pool, not only the released connection's pages: WB-31's facts measured
//! the other session's cache falling with it. So a release costs the working
//! session one cold pass, and a slot is released once per idle period, not
//! at every pass while it stays idle.
//!
//! A pass runs under the registry lock and drops every handle it upgraded
//! before releasing it, so a closing session, which unregisters first,
//! never has a connection outlive it on this thread.

use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Condvar, LazyLock, Mutex, MutexGuard, Weak};
use std::thread::JoinHandle;
use std::time::Duration;

use rusqlite::Connection;

use super::conn_slot::{ConnSlot, now_ms};

/// One registered session's two connections and its idle interval.
pub(crate) struct SessionCacheHandle {
    pub(crate) main: Arc<ConnSlot<Connection>>,
    pub(crate) worker: Arc<ConnSlot<Option<Connection>>>,
    pub(crate) idle_ms: i64,
}

#[derive(Default)]
struct Registry {
    sessions: Vec<Weak<SessionCacheHandle>>,
    thread: Option<ReleaseThread>,
}

/// The running release thread and its own stop flag. The flag is the
/// thread's, never the registry's: a later registration that starts a new
/// thread cannot reset it, so the thread the last session stopped always
/// ends and that session's join returns (Codex on WB-31).
struct ReleaseThread {
    handle: JoinHandle<()>,
    stop: Arc<AtomicBool>,
}

static REGISTRY: LazyLock<(Mutex<Registry>, Condvar)> =
    LazyLock::new(|| (Mutex::new(Registry::default()), Condvar::new()));

fn registry() -> MutexGuard<'static, Registry> {
    REGISTRY
        .0
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
}

/// Registers a session; the second registration starts the thread.
pub(crate) fn register(handle: &Arc<SessionCacheHandle>) {
    let mut registry = registry();
    registry.sessions.retain(|weak| weak.strong_count() > 0);
    registry.sessions.push(Arc::downgrade(handle));
    if registry.sessions.len() >= 2 && registry.thread.is_none() {
        let stop = Arc::new(AtomicBool::new(false));
        let thread_stop = Arc::clone(&stop);
        let handle = std::thread::Builder::new()
            .name("slate-cache-release".into())
            .spawn(move || release_thread(&thread_stop))
            .expect("spawn the cache release thread");
        registry.thread = Some(ReleaseThread { handle, stop });
    }
    REGISTRY.1.notify_all();
}

/// Unregisters a closing session, waiting out a pass in progress; the last
/// one stops and joins the thread.
pub(crate) fn unregister(handle: &Arc<SessionCacheHandle>) {
    let finished = {
        let mut registry = registry();
        registry.sessions.retain(|weak| {
            weak.strong_count() > 0 && !std::ptr::eq(weak.as_ptr(), Arc::as_ptr(handle))
        });
        if registry.sessions.is_empty() {
            let thread = registry.thread.take();
            if let Some(thread) = &thread {
                thread.stop.store(true, Ordering::SeqCst);
                #[cfg(test)]
                hold_if_armed(thread.handle.thread().id());
            }
            REGISTRY.1.notify_all();
            thread
        } else {
            None
        }
    };
    if let Some(thread) = finished {
        #[cfg(test)]
        if let Some(hook) = BEFORE_JOIN_FOR_TESTS
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
            .take()
        {
            hook();
        }
        let _ = thread.handle.join();
    }
}

/// Test seam: runs once, in the last session's unregister, after the
/// registry lock is released and before the join, where a fact registers new
/// sessions to race the restart.
#[cfg(test)]
pub(super) static BEFORE_JOIN_FOR_TESTS: Mutex<Option<Box<dyn FnOnce() + Send>>> = Mutex::new(None);

/// Test seam: when armed, the thread the last close stops waits, after it
/// wakes and before it reads its stop flag, until the fact releases it, so
/// the close's new registrations run first, deterministically.
#[cfg(test)]
pub(super) static HOLD_STOPPED_THREAD_FOR_TESTS: AtomicBool = AtomicBool::new(false);
#[cfg(test)]
static HELD_THREAD: Mutex<Option<std::thread::ThreadId>> = Mutex::new(None);
#[cfg(test)]
static HELD_THREAD_RELEASED: (Mutex<bool>, Condvar) = (Mutex::new(false), Condvar::new());

#[cfg(test)]
fn hold_if_armed(thread: std::thread::ThreadId) {
    if HOLD_STOPPED_THREAD_FOR_TESTS.swap(false, Ordering::SeqCst) {
        *HELD_THREAD
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner) = Some(thread);
        *HELD_THREAD_RELEASED
            .0
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner) = false;
    }
}

#[cfg(test)]
fn is_held(thread: std::thread::ThreadId) -> bool {
    *HELD_THREAD
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner)
        == Some(thread)
}

#[cfg(test)]
fn wait_until_released() {
    let (released, signal) = &HELD_THREAD_RELEASED;
    let mut released = released
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner);
    while !*released {
        released = signal
            .wait(released)
            .unwrap_or_else(std::sync::PoisonError::into_inner);
    }
    *HELD_THREAD
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner) = None;
}

/// Releases the thread [`HOLD_STOPPED_THREAD_FOR_TESTS`] held.
#[cfg(test)]
pub(super) fn release_held_thread_for_tests() {
    let (released, signal) = &HELD_THREAD_RELEASED;
    *released
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner) = true;
    signal.notify_all();
}

fn release_thread(stop: &AtomicBool) {
    let mut registry = registry();
    loop {
        if stop.load(Ordering::SeqCst) {
            return;
        }
        let interval = registry
            .sessions
            .iter()
            .filter_map(Weak::upgrade)
            .map(|handle| handle.idle_ms)
            .min()
            .unwrap_or(1_000)
            .max(1);
        let (woken, _) = REGISTRY
            .1
            .wait_timeout(registry, Duration::from_millis(interval as u64))
            .unwrap_or_else(std::sync::PoisonError::into_inner);
        registry = woken;
        #[cfg(test)]
        if is_held(std::thread::current().id()) {
            drop(registry);
            wait_until_released();
            registry = self::registry();
        }
        if stop.load(Ordering::SeqCst) {
            return;
        }
        release_pass_locked(&registry);
    }
}
/// One pass over the registered sessions, as the thread runs it: the facts'
/// door, with the clock pinned.
#[cfg(test)]
pub(super) fn release_pass() {
    let registry = registry();
    release_pass_locked(&registry);
}

fn release_pass_locked(registry: &Registry) {
    let handles: Vec<Arc<SessionCacheHandle>> =
        registry.sessions.iter().filter_map(Weak::upgrade).collect();
    if handles.len() < 2 {
        return;
    }
    for handle in &handles {
        if let Some(conn) = handle.main.try_lock_without_activity()
            && is_due(&handle.main, handle.idle_ms)
        {
            crate::db::release_connection_memory(&conn);
            handle.main.mark_released();
        }
        if let Some(slot) = handle.worker.try_lock_without_activity()
            && let Some(conn) = slot.as_ref()
            && is_due(&handle.worker, handle.idle_ms)
        {
            crate::db::release_connection_memory(conn);
            handle.worker.mark_released();
        }
    }
}

/// Idle a full interval since its last use ended, and not yet released in
/// this idle period. SQLite's shrink empties the whole shared pool, every
/// connection's unused pages included (WB-31 measured it), so a release costs
/// the working session one cold pass: once per idle period, not every pass.
/// Read under the slot's own lock: a use that ended stamped before it
/// unlocked, so this sees it.
fn is_due<C>(slot: &ConnSlot<C>, idle_ms: i64) -> bool {
    !slot.is_active()
        && !slot.released_this_idle_period()
        && now_ms().saturating_sub(slot.idle_since_ms()) >= idle_ms
}

/// `VaultSession::release_idle_cache`: this session's two connections,
/// released now whatever their idle time, a held one still skipped.
pub(super) fn release_now(main: &ConnSlot<Connection>, worker: &ConnSlot<Option<Connection>>) {
    if let Some(conn) = main.try_lock_without_activity() {
        crate::db::release_connection_memory(&conn);
        main.mark_released();
    }
    if let Some(slot) = worker.try_lock_without_activity()
        && let Some(conn) = slot.as_ref()
    {
        crate::db::release_connection_memory(conn);
        worker.mark_released();
    }
}

/// Whether the release thread is running (the facts' window on its life).
#[cfg(test)]
pub(super) fn thread_running_for_tests() -> bool {
    registry().thread.is_some()
}

#[cfg(test)]
mod tests {
    //! The registry, its thread and the test clock are process-wide, so each
    //! fact that pins the clock or runs passes runs its body in a `_child`
    //! alone in its own process (`run_alone`); the thread fact serialises on
    //! `REGISTRY_FACTS`. SQLite's shrink empties the whole shared pool, so a
    //! slot's own release count, not any connection's `used`, says which slot
    //! a pass released.

    use super::*;
    use crate::CancelToken;
    use crate::session::conn_slot::set_test_clock_ms;
    use crate::session::{SessionConfig, VaultSession, is_alone_child, run_alone};
    use crate::vault::{FsVaultProvider, VaultProvider};

    static REGISTRY_FACTS: Mutex<()> = Mutex::new(());

    fn serial() -> MutexGuard<'static, ()> {
        REGISTRY_FACTS
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner)
    }

    fn opted_in(notes: usize, idle_ms: u32) -> (tempfile::TempDir, VaultSession) {
        let tmp = tempfile::tempdir().unwrap();
        let root = tmp.path().to_path_buf();
        let writer = FsVaultProvider::new(root.clone());
        for index in 0..notes {
            writer
                .write_file(
                    &format!("note{index}.md"),
                    format!("# Note {index}\n\nBody {index} #tag{}\n", index % 5).as_bytes(),
                )
                .unwrap();
        }
        let mut config = SessionConfig::new(root.join(".slate"));
        config.idle_cache_release_ms = idle_ms;
        let session = VaultSession::open(Arc::new(FsVaultProvider::new(root)), config).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        (tmp, session)
    }

    /// Unpins the clock however the child ends.
    struct ClockReset;

    impl Drop for ClockReset {
        fn drop(&mut self) {
            set_test_clock_ms(None);
        }
    }

    fn pin(at: i64) {
        set_test_clock_ms(Some(at));
    }

    fn main_releases(session: &VaultSession) -> u64 {
        session.conn.releases_for_tests()
    }

    fn worker_releases(session: &VaultSession) -> u64 {
        session.worker_conn.releases_for_tests()
    }

    fn use_main(session: &VaultSession) {
        let conn = session.conn.lock().unwrap();
        let _: i64 = conn
            .query_row("SELECT count(*) FROM files", [], |row| row.get(0))
            .unwrap();
    }

    #[test]
    fn the_release_thread_starts_with_the_second_session_and_stops_with_the_last() {
        let _serial = serial();
        let (_a_tmp, a) = opted_in(5, 60_000);
        assert!(
            !thread_running_for_tests(),
            "the thread started with one session"
        );
        let (_b_tmp, b) = opted_in(5, 60_000);
        assert!(
            thread_running_for_tests(),
            "the thread did not start with two sessions"
        );
        drop(a);
        assert!(
            thread_running_for_tests(),
            "the thread stopped with a session left"
        );
        drop(b);
        assert!(
            !thread_running_for_tests(),
            "the thread outlived the last session"
        );
    }

    /// Codex on WB-31: new sessions registering while the last close joins
    /// its stopped thread start a new thread, and must not keep the stopped
    /// one alive (a shared stop flag they reset did: the close then waited on
    /// it until they closed too).
    #[test]
    fn the_last_close_ends_its_thread_though_new_sessions_register_before_its_join() {
        let _serial = serial();
        let (_a_tmp, a) = opted_in(5, 60_000);
        let (_b_tmp, b) = opted_in(5, 60_000);
        assert!(thread_running_for_tests());
        type Opened = Vec<(tempfile::TempDir, VaultSession)>;
        let restarted: Arc<Mutex<Opened>> = Arc::default();
        let into = Arc::clone(&restarted);
        HOLD_STOPPED_THREAD_FOR_TESTS.store(true, Ordering::SeqCst);
        *BEFORE_JOIN_FOR_TESTS.lock().unwrap() = Some(Box::new(move || {
            let mut opened = into.lock().unwrap();
            opened.push(opted_in(5, 60_000));
            opened.push(opted_in(5, 60_000));
            // Only now may the stopped thread read its flag.
            release_held_thread_for_tests();
        }));
        drop(a);
        let (closed, closing) = std::sync::mpsc::channel();
        std::thread::spawn(move || {
            drop(b);
            let _ = closed.send(());
        });
        assert!(
            closing.recv_timeout(Duration::from_secs(10)).is_ok(),
            "the last close hung joining its stopped thread"
        );
        assert!(
            thread_running_for_tests(),
            "the sessions that registered during the close have no thread"
        );
        let opened = std::mem::take(&mut *restarted.lock().unwrap());
        drop(opened);
        assert!(
            !thread_running_for_tests(),
            "the restarted thread outlived its sessions"
        );
    }

    #[test]
    fn a_lone_session_is_never_released() {
        run_alone("session::registry::tests::a_lone_session_is_never_released_child");
    }

    #[test]
    fn a_lone_session_is_never_released_child() {
        if !is_alone_child("session::registry::tests::a_lone_session_is_never_released_child") {
            return;
        }
        let _serial = serial();
        let _reset = ClockReset;
        pin(0);
        let (_tmp, a) = opted_in(50, 1_000);
        let used = a.conn.cache_counters().used;
        pin(10_000);
        release_pass();
        assert_eq!(main_releases(&a), 0, "a lone session was released");
        assert_eq!(
            a.conn.cache_counters().used,
            used,
            "a lone session's cache fell"
        );
    }

    #[test]
    fn an_idle_slot_is_released_once_per_idle_period() {
        run_alone("session::registry::tests::an_idle_slot_is_released_once_per_idle_period_child");
    }

    #[test]
    fn an_idle_slot_is_released_once_per_idle_period_child() {
        if !is_alone_child(
            "session::registry::tests::an_idle_slot_is_released_once_per_idle_period_child",
        ) {
            return;
        }
        let _serial = serial();
        let _reset = ClockReset;
        pin(0);
        let (_a_tmp, a) = opted_in(50, 1_000);
        let (_b_tmp, _b) = opted_in(50, 1_000);
        pin(2_000);
        release_pass();
        assert_eq!(main_releases(&a), 1, "an idle slot was not released");
        pin(3_000);
        release_pass();
        assert_eq!(main_releases(&a), 1, "a still-idle slot was released again");
        use_main(&a);
        pin(3_999);
        release_pass();
        assert_eq!(
            main_releases(&a),
            1,
            "released before a full interval after its use"
        );
        pin(4_000);
        release_pass();
        assert_eq!(main_releases(&a), 2, "a new idle period was not released");
    }

    #[test]
    fn a_direct_lock_public_read_resets_the_idle_deadline() {
        run_alone(
            "session::registry::tests::a_direct_lock_public_read_resets_the_idle_deadline_child",
        );
    }

    #[test]
    fn a_direct_lock_public_read_resets_the_idle_deadline_child() {
        if !is_alone_child(
            "session::registry::tests::a_direct_lock_public_read_resets_the_idle_deadline_child",
        ) {
            return;
        }
        let _serial = serial();
        let _reset = ClockReset;
        pin(0);
        let (_a_tmp, a) = opted_in(50, 1_000);
        let (_b_tmp, _b) = opted_in(50, 1_000);

        pin(500);
        // A public read that locks the connection directly (`self.conn.lock()`).
        a.indexed_content_hashes(&["note0.md".to_string()], &CancelToken::new())
            .unwrap();
        assert_eq!(
            a.conn.idle_since_ms(),
            500,
            "the read did not record its use"
        );

        pin(1_499);
        release_pass();
        assert_eq!(
            main_releases(&a),
            0,
            "released before a full interval after the read"
        );
        pin(1_500);
        release_pass();
        assert_eq!(
            main_releases(&a),
            1,
            "not released a full interval after the read"
        );
    }

    #[test]
    fn a_held_connection_is_never_released_under_its_user() {
        run_alone(
            "session::registry::tests::a_held_connection_is_never_released_under_its_user_child",
        );
    }

    #[test]
    fn a_held_connection_is_never_released_under_its_user_child() {
        if !is_alone_child(
            "session::registry::tests::a_held_connection_is_never_released_under_its_user_child",
        ) {
            return;
        }
        let _serial = serial();
        let _reset = ClockReset;
        pin(0);
        let (_a_tmp, a) = opted_in(50, 1_000);
        let (_b_tmp, _b) = opted_in(50, 1_000);
        pin(5_000);
        {
            let held = a.conn.lock().unwrap();
            // Long past the interval, but in use: the pass must skip it.
            release_pass();
            drop(held);
        }
        assert_eq!(main_releases(&a), 0, "a held connection was released");
    }

    #[test]
    fn a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection() {
        run_alone(
            "session::registry::tests::a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection_child",
        );
    }

    #[test]
    fn a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection_child() {
        if !is_alone_child(
            "session::registry::tests::a_long_operation_earns_a_full_idle_interval_after_it_releases_its_connection_child",
        ) {
            return;
        }
        const I: i64 = 1_000;
        let _serial = serial();
        let _reset = ClockReset;
        pin(0);
        let (_a_tmp, a) = opted_in(50, I as u32);
        let (_b_tmp, _b) = opted_in(50, I as u32);
        a.open_worker_connection_for_tests();

        let use_worker = |session: &VaultSession| {
            let slot = session.worker_conn.lock().unwrap();
            let conn = slot.as_ref().expect("the compactor's connection is open");
            let _: i64 = conn
                .query_row("SELECT count(*) FROM files", [], |row| row.get(0))
                .unwrap();
        };

        // (a) the main connection, held from T to T + 5I.
        let t = 0;
        pin(t);
        use_main(&a);
        let before = main_releases(&a);
        {
            let held = a.conn.lock().unwrap();
            pin(t + 3 * I);
            release_pass();
            pin(t + 5 * I);
            drop(held);
        }
        assert_eq!(main_releases(&a), before, "(a) released under its hold");
        release_pass();
        pin(t + 6 * I - 1);
        release_pass();
        assert_eq!(
            main_releases(&a),
            before,
            "(a) released before a full interval after the hold"
        );
        pin(t + 6 * I);
        release_pass();
        assert_eq!(
            main_releases(&a),
            before + 1,
            "(a) not released a full interval after the hold"
        );

        // (b) the compactor's connection, held from T to T + 5I.
        let t = 10 * I;
        pin(t);
        use_worker(&a);
        let before = worker_releases(&a);
        {
            let held = a.worker_conn.lock().unwrap();
            pin(t + 3 * I);
            release_pass();
            pin(t + 5 * I);
            drop(held);
        }
        assert_eq!(worker_releases(&a), before, "(b) released under its hold");
        release_pass();
        pin(t + 6 * I - 1);
        release_pass();
        assert_eq!(
            worker_releases(&a),
            before,
            "(b) released before a full interval after the hold"
        );
        pin(t + 6 * I);
        release_pass();
        assert_eq!(
            worker_releases(&a),
            before + 1,
            "(b) not released a full interval after the hold"
        );

        // (c) an operation in progress without the connection, T to T + 5I.
        let t = 20 * I;
        pin(t);
        use_main(&a);
        let before = main_releases(&a);
        {
            let active = a.conn.activity();
            pin(t + 3 * I);
            release_pass();
            pin(t + 5 * I);
            drop(active);
        }
        assert_eq!(main_releases(&a), before, "(c) released while in progress");
        release_pass();
        pin(t + 6 * I - 1);
        release_pass();
        assert_eq!(
            main_releases(&a),
            before,
            "(c) released before a full interval after it ended"
        );
        pin(t + 6 * I);
        release_pass();
        assert_eq!(
            main_releases(&a),
            before + 1,
            "(c) not released a full interval after it ended"
        );
    }

    /// The type system is the census for every other door (`inner` is
    /// private to `conn_slot.rs`); this pins the one that records nothing.
    #[test]
    fn only_the_release_path_locks_without_recording_activity() {
        let src = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("src");
        let mut callers = Vec::new();
        let mut stack = vec![src.clone()];
        while let Some(dir) = stack.pop() {
            for entry in std::fs::read_dir(&dir).unwrap() {
                let path = entry.unwrap().path();
                if path.is_dir() {
                    stack.push(path);
                } else if path.extension().is_some_and(|ext| ext == "rs") {
                    let text = std::fs::read_to_string(&path).unwrap();
                    if text.contains("try_lock_without_activity(") {
                        callers.push(
                            path.strip_prefix(&src)
                                .unwrap()
                                .to_string_lossy()
                                .replace('\\', "/"),
                        );
                    }
                }
            }
        }
        callers.sort();
        assert_eq!(callers, ["session/conn_slot.rs", "session/registry.rs"]);
        let slot = std::fs::read_to_string(src.join("session/conn_slot.rs")).unwrap();
        assert_eq!(
            slot.matches("try_lock_without_activity(").count(),
            1,
            "conn_slot.rs holds only the definition"
        );
    }

    /// A lone registered session is never released, so the registry costs
    /// the one-session case nothing beyond its stamps. Runs only alone
    /// (`--release`, by name): every bulk run skips `perf_guard_`.
    #[test]
    fn perf_guard_the_only_session_in_a_process_is_not_slowed_by_the_release() {
        let _serial = serial();
        const NOTES: usize = 3_000;
        const RESCANS: usize = 6;
        let median = |session: &VaultSession| -> f64 {
            let mut times: Vec<f64> = (0..RESCANS)
                .map(|_| {
                    let started = std::time::Instant::now();
                    session
                        .rescan_with_progress(&CancelToken::new(), None)
                        .unwrap();
                    started.elapsed().as_secs_f64()
                })
                .collect();
            times.sort_by(|x, y| x.partial_cmp(y).unwrap());
            (times[RESCANS / 2 - 1] + times[RESCANS / 2]) / 2.0
        };
        let alone = {
            let (_tmp, plain) = opted_in(NOTES, 0);
            median(&plain)
        };
        let (_tmp, registered) = opted_in(NOTES, 50);
        let with_registry = median(&registered);
        let ratio = with_registry / alone;
        assert!(
            ratio <= 1.10,
            "the lone registered session rescanned {ratio:.2}x slower"
        );
    }
}
