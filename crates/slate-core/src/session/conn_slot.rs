// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-31 (#1484): the one way to a session's SQLite connections,
//! recording when each use ENDS.
//!
//! With `SQLITE_ENABLE_MEMORY_MANAGEMENT` every connection in the process
//! draws pages from one pool, so an idle session's cache holds pages a
//! working session then misses (the WB-31 bench: 1.5x on a 10k-note
//! rescan). The registry ([`super::registry`]) releases an idle session's
//! cache, and "idle" must start when a use completes, not when it began: an
//! operation longer than the interval would otherwise be releasable the
//! moment it lets go, discarding the cache it just warmed. So a guard stamps
//! the slot's activity time when it is taken and again when it drops,
//! before the inner lock unlocks, and an operation that works between
//! connection uses (a scan) holds an [`ActivityGuard`] for its whole run.
//!
//! `inner` is private: nothing reaches a connection except through these
//! methods, and only [`ConnSlot::try_lock_without_activity`], the release
//! path's door, takes it without stamping.

use std::ops::{Deref, DerefMut};
use std::sync::atomic::{AtomicI64, AtomicU64, AtomicUsize, Ordering};
use std::sync::{LazyLock, LockResult, Mutex, MutexGuard, PoisonError};
use std::time::Instant;

use rusqlite::Connection;

use super::{CancelToken, VaultError, trash_cancellation};

static PROCESS_START: LazyLock<Instant> = LazyLock::new(Instant::now);

#[cfg(test)]
static TEST_CLOCK_MS: Mutex<Option<i64>> = Mutex::new(None);

/// Milliseconds since this process first asked: monotonic, process-wide.
pub(crate) fn now_ms() -> i64 {
    #[cfg(test)]
    if let Some(pinned) = *TEST_CLOCK_MS.lock().expect("test clock mutex") {
        return pinned;
    }
    i64::try_from(PROCESS_START.elapsed().as_millis()).unwrap_or(i64::MAX)
}

/// Pins [`now_ms`] for the registry facts; `None` lets it run again.
#[cfg(test)]
pub(crate) fn set_test_clock_ms(pinned: Option<i64>) {
    *TEST_CLOCK_MS.lock().expect("test clock mutex") = pinned;
}

/// A connection (or the compactor's optional one) with its last use's end
/// and its count of operations in progress.
pub(crate) struct ConnSlot<C = Connection> {
    inner: Mutex<C>,
    last_activity_ms: AtomicI64,
    active: AtomicUsize,
    /// The `last_activity_ms` of the idle period last released, so a slot is
    /// released once per idle period: SQLite's shrink empties the whole
    /// shared pool, every connection's unused pages included (measured), so
    /// releasing a still-idle slot again would wipe a working session's warm
    /// cache every pass.
    released_idle_since_ms: AtomicI64,
    releases: AtomicU64,
}

/// A held connection. Its drop stamps the slot before the inner guard
/// unlocks, so a release that takes the lock next always sees this use's end.
pub(crate) struct ConnGuard<'a, C = Connection> {
    guard: MutexGuard<'a, C>,
    slot: &'a ConnSlot<C>,
}

/// An operation in progress that does not hold the connection the whole
/// time (a scan between its uses): the slot counts as in use until it drops,
/// and the drop stamps the slot.
pub(crate) struct ActivityGuard<'a, C = Connection> {
    slot: &'a ConnSlot<C>,
}

impl<C> ConnSlot<C> {
    pub(crate) fn new(inner: C) -> Self {
        Self {
            inner: Mutex::new(inner),
            last_activity_ms: AtomicI64::new(now_ms()),
            active: AtomicUsize::new(0),
            released_idle_since_ms: AtomicI64::new(i64::MIN),
            releases: AtomicU64::new(0),
        }
    }

    /// Whether this idle period has already been released (the registry's
    /// release-once rule).
    pub(super) fn released_this_idle_period(&self) -> bool {
        self.released_idle_since_ms.load(Ordering::SeqCst) == self.idle_since_ms()
    }

    /// Records a release of the current idle period.
    pub(super) fn mark_released(&self) {
        self.released_idle_since_ms
            .store(self.idle_since_ms(), Ordering::SeqCst);
        self.releases.fetch_add(1, Ordering::SeqCst);
    }

    /// How many times this slot's cache has been released.
    #[cfg(test)]
    pub(crate) fn releases_for_tests(&self) -> u64 {
        self.releases.load(Ordering::SeqCst)
    }

    fn stamp(&self) {
        self.last_activity_ms.store(now_ms(), Ordering::SeqCst);
    }

    /// The connection, recorded as used when taken and when released; the
    /// `LockResult` shape `Mutex::lock` has.
    pub(crate) fn lock(&self) -> LockResult<ConnGuard<'_, C>> {
        self.stamp();
        match self.inner.lock() {
            Ok(guard) => Ok(ConnGuard { guard, slot: self }),
            Err(poisoned) => Err(PoisonError::new(ConnGuard {
                guard: poisoned.into_inner(),
                slot: self,
            })),
        }
    }

    /// [`Self::lock`], waiting cooperatively under `cancel`.
    pub(crate) fn lock_cancellable(
        &self,
        cancel: &CancelToken,
    ) -> Result<ConnGuard<'_, C>, VaultError> {
        self.stamp();
        let guard = trash_cancellation::lock_cancellable(&self.inner, cancel)?;
        Ok(ConnGuard { guard, slot: self })
    }

    /// Marks an operation in progress until the guard drops.
    pub(crate) fn activity(&self) -> ActivityGuard<'_, C> {
        self.active.fetch_add(1, Ordering::SeqCst);
        self.stamp();
        ActivityGuard { slot: self }
    }

    /// The release path's only door: `None` while anyone holds the
    /// connection, which is then in use and skipped rather than waited for.
    pub(super) fn try_lock_without_activity(&self) -> Option<MutexGuard<'_, C>> {
        self.inner.try_lock().ok()
    }

    /// Whether someone holds the connection now (the watch facts' check that
    /// a callback runs with no session lock held).
    #[cfg(test)]
    pub(crate) fn is_held_for_tests(&self) -> bool {
        matches!(
            self.inner.try_lock(),
            Err(std::sync::TryLockError::WouldBlock)
        )
    }

    /// Whether an [`ActivityGuard`] is alive.
    pub(crate) fn is_active(&self) -> bool {
        self.active.load(Ordering::SeqCst) > 0
    }

    /// When the last use ended (or began, while it runs).
    pub(crate) fn idle_since_ms(&self) -> i64 {
        self.last_activity_ms.load(Ordering::SeqCst)
    }
}

impl ConnSlot<Connection> {
    /// The connection's counters, read without stamping: observing a slot
    /// never makes it look busy.
    pub(crate) fn cache_counters(&self) -> crate::db::DbCacheCounters {
        let conn = self
            .inner
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner);
        crate::db::connection_cache_counters(&conn)
    }
}

impl ConnSlot<Option<Connection>> {
    /// The compactor's counters once its connection is open, read without
    /// stamping.
    pub(crate) fn cache_counters(&self) -> Option<crate::db::DbCacheCounters> {
        let slot = self
            .inner
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner);
        slot.as_ref().map(crate::db::connection_cache_counters)
    }
}

impl<C> Deref for ConnGuard<'_, C> {
    type Target = C;

    fn deref(&self) -> &C {
        &self.guard
    }
}

impl<C> DerefMut for ConnGuard<'_, C> {
    fn deref_mut(&mut self) -> &mut C {
        &mut self.guard
    }
}

impl<C> Drop for ConnGuard<'_, C> {
    fn drop(&mut self) {
        // Runs before the `guard` field drops, so before the unlock.
        self.slot.stamp();
    }
}

impl<C> Drop for ActivityGuard<'_, C> {
    fn drop(&mut self) {
        self.slot.stamp();
        self.slot.active.fetch_sub(1, Ordering::SeqCst);
    }
}
