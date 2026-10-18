// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The echo ledger (plan 43 PR 20a, #1267; design
//! `docs/plans/43_mac_backlog_delivery/20_vault_watcher_design.md` §4.2): what
//! this provider's own latest mutation of a path left behind, so the vault
//! watcher can tell Slate's own writes from everyone else's.
//!
//! `FsVaultProvider` records an expected post-state after every mutation it
//! completes — present with the size, mtime, ctime and kind it stats right
//! after the write, or absent — for the mutated paths and the folders above
//! them. At burst close the watch coalescer asks
//! [`VaultProvider::is_own_echo`](super::VaultProvider::is_own_echo) for each
//! touched path, and a path whose current state equals its newest entry is
//! Slate's own echo. The test is "this provider wrote it", never "the index
//! already has it": another session's write is in the shared index but not on
//! this host's surfaces, so it must still call back (AR-30).
//!
//! Bounded (locked 05 §4 principle 4): at most [`ECHO_LEDGER_CAPACITY`]
//! entries, each kept for [`ECHO_LEDGER_TTL`]. A miss — an evicted entry, a
//! spelling the ledger did not record, a write that did not go through the
//! provider — costs one silent rescan, never a wrong result.

use std::collections::{BTreeMap, HashMap};
use std::time::{Duration, Instant};

use super::EntryKind;

/// The most paths the ledger remembers (design §5's constants).
pub(crate) const ECHO_LEDGER_CAPACITY: usize = 4096;

/// How long an entry stays an echo after its mutation (design §5).
pub(crate) const ECHO_LEDGER_TTL: Duration = Duration::from_secs(60);

/// A path's observable state, as the echo test compares it: present with
/// these attributes, or absent. Windows has no portable ctime (`0` on both
/// sides of the comparison), so there an external write in the same
/// millisecond at the same size counts as an echo — the residual the
/// baseline's own fast path shares (design §9 item 4, AR-7's class).
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum EchoState {
    Present {
        size_bytes: u64,
        mtime_ms: i64,
        ctime_ms: i64,
        kind: EntryKind,
    },
    Absent,
}

#[derive(Debug)]
struct Entry {
    seq: u64,
    recorded_at: Instant,
    state: EchoState,
}

/// Newest expected post-state per vault-relative path, oldest first for
/// eviction.
#[derive(Debug, Default)]
pub(crate) struct EchoLedger {
    entries: HashMap<String, Entry>,
    /// Insertion order: `seq` → path. Every live entry has exactly one row.
    order: BTreeMap<u64, String>,
    next_seq: u64,
}

impl EchoLedger {
    /// Record `path`'s state after this provider's mutation of it. `None`
    /// forgets the path: its post-state could not be observed, so nothing may
    /// claim the next event at it as an echo.
    pub(crate) fn record(&mut self, now: Instant, path: String, state: Option<EchoState>) {
        if let Some(previous) = self.entries.remove(&path) {
            self.order.remove(&previous.seq);
        }
        if let Some(state) = state {
            let seq = self.next_seq;
            self.next_seq += 1;
            self.order.insert(seq, path.clone());
            self.entries.insert(
                path,
                Entry {
                    seq,
                    recorded_at: now,
                    state,
                },
            );
        }
        self.prune(now);
    }

    /// The state the newest live entry for `path` expects, or `None` when the
    /// ledger holds none (never recorded, evicted, or older than the TTL).
    pub(crate) fn expected(&self, now: Instant, path: &str) -> Option<&EchoState> {
        let entry = self.entries.get(path)?;
        (now.saturating_duration_since(entry.recorded_at) <= ECHO_LEDGER_TTL)
            .then_some(&entry.state)
    }

    /// Drop the oldest entries while the ledger is over its capacity or they
    /// are past the TTL. Entries are stamped under the provider's ledger lock,
    /// so `seq` order is time order.
    fn prune(&mut self, now: Instant) {
        while let Some((&seq, path)) = self.order.first_key_value() {
            let expired = self.entries.get(path).is_none_or(|entry| {
                now.saturating_duration_since(entry.recorded_at) > ECHO_LEDGER_TTL
            });
            if !expired && self.entries.len() <= ECHO_LEDGER_CAPACITY {
                break;
            }
            let path = self.order.remove(&seq).expect("first key is present");
            self.entries.remove(&path);
        }
    }

    #[cfg(test)]
    pub(crate) fn len(&self) -> usize {
        self.entries.len()
    }
}

/// The ledger's key for a vault-relative spelling: its components joined with
/// `/`, `.` components and empty segments dropped — the shape the watcher
/// builds from an event path (`super::watch`). `None` for the vault root or a
/// spelling with a parent-directory component, which no mutation records.
/// A backslash separates components only on Windows: elsewhere it is a legal
/// character of a name, and splitting there would record (and stat) another
/// path than the one mutated.
pub(crate) fn ledger_key(relative: &str) -> Option<String> {
    let separator = |c: char| c == '/' || (cfg!(windows) && c == '\\');
    let mut key = String::with_capacity(relative.len());
    for part in relative.split(separator) {
        match part {
            "" | "." => continue,
            ".." => return None,
            part => {
                if !key.is_empty() {
                    key.push('/');
                }
                key.push_str(part);
            }
        }
    }
    (!key.is_empty()).then_some(key)
}

/// `key`'s folders, innermost first, the vault root excluded: `a/b/c.md` →
/// `a/b`, `a`.
pub(crate) fn ancestor_keys(key: &str) -> impl Iterator<Item = &str> {
    let mut rest = key;
    std::iter::from_fn(move || {
        let cut = rest.rfind('/')?;
        rest = &rest[..cut];
        Some(rest)
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn present(size_bytes: u64) -> EchoState {
        EchoState::Present {
            size_bytes,
            mtime_ms: 1,
            ctime_ms: 1,
            kind: EntryKind::File,
        }
    }

    /// Design §5: the ledger keeps at most 4,096 entries for 60 s. The
    /// 4,097th entry evicts the oldest; an entry older than 60 s is no echo;
    /// the bound holds whatever a sync client does (principle 4).
    #[test]
    fn the_echo_ledger_keeps_4096_entries_for_60_seconds() {
        assert_eq!(ECHO_LEDGER_CAPACITY, 4096);
        assert_eq!(ECHO_LEDGER_TTL, Duration::from_secs(60));

        let start = Instant::now();
        let mut ledger = EchoLedger::default();
        for index in 0..ECHO_LEDGER_CAPACITY {
            ledger.record(start, format!("n{index}.md"), Some(present(1)));
        }
        assert_eq!(ledger.len(), ECHO_LEDGER_CAPACITY);
        assert_eq!(ledger.expected(start, "n0.md"), Some(&present(1)));

        ledger.record(start, "one-more.md".into(), Some(present(1)));
        assert_eq!(ledger.len(), ECHO_LEDGER_CAPACITY, "the bound holds");
        assert_eq!(ledger.expected(start, "n0.md"), None, "the oldest went");
        assert_eq!(ledger.expected(start, "n1.md"), Some(&present(1)));

        let at_ttl = start + ECHO_LEDGER_TTL;
        assert_eq!(
            ledger.expected(at_ttl, "one-more.md"),
            Some(&present(1)),
            "still an echo at exactly 60 s"
        );
        let past_ttl = at_ttl + Duration::from_millis(1);
        assert_eq!(ledger.expected(past_ttl, "one-more.md"), None);

        // A later record prunes everything past the TTL.
        ledger.record(past_ttl, "fresh.md".into(), Some(present(2)));
        assert_eq!(ledger.len(), 1);
        assert_eq!(ledger.expected(past_ttl, "fresh.md"), Some(&present(2)));
    }

    /// The newest mutation of a path is the one its echo test compares
    /// against, and an unobservable post-state forgets the path.
    #[test]
    fn the_newest_entry_for_a_path_wins_and_an_unobserved_one_forgets_it() {
        let now = Instant::now();
        let mut ledger = EchoLedger::default();
        ledger.record(now, "a.md".into(), Some(present(1)));
        ledger.record(now, "a.md".into(), Some(present(2)));
        assert_eq!(ledger.len(), 1);
        assert_eq!(ledger.expected(now, "a.md"), Some(&present(2)));
        ledger.record(now, "a.md".into(), Some(EchoState::Absent));
        assert_eq!(ledger.expected(now, "a.md"), Some(&EchoState::Absent));
        ledger.record(now, "a.md".into(), None);
        assert_eq!(ledger.expected(now, "a.md"), None);
        assert_eq!(ledger.len(), 0);
        assert_eq!(ledger.order.len(), 0, "no orphaned order rows");
    }

    #[test]
    fn ledger_keys_match_the_watchers_spelling() {
        assert_eq!(ledger_key("a.md").as_deref(), Some("a.md"));
        assert_eq!(ledger_key("./a/b.md").as_deref(), Some("a/b.md"));
        assert_eq!(ledger_key("a//b.md/").as_deref(), Some("a/b.md"));
        // A backslash is a separator only where the platform says so.
        let backslashed = if cfg!(windows) { "a/b.md" } else { r"a\b.md" };
        assert_eq!(ledger_key(r"a\b.md").as_deref(), Some(backslashed));
        assert_eq!(ledger_key(""), None);
        assert_eq!(ledger_key("."), None);
        assert_eq!(ledger_key("a/../b.md"), None);
        assert_eq!(
            ancestor_keys("a/b/c.md").collect::<Vec<_>>(),
            vec!["a/b", "a"]
        );
        assert_eq!(ancestor_keys("c.md").count(), 0);
    }
}
