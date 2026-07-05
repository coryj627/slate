// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Coarse scan-progress reporting for the `slate` CLI (M-4, #535).
//!
//! Per the global contract (m_spec §M-4): when stderr is a TTY **and**
//! a scan takes longer than 1 second, print `Indexing… <n> files`
//! lines to stderr — coarse, at most one per second — driven by the
//! `scan_initial_with_progress` listener. Never on a non-TTY stderr
//! (piped/redirected output stays clean), and never on stdout (which
//! carries data only).

use std::io::{IsTerminal, Write};
use std::sync::Arc;
use std::sync::atomic::{AtomicU64, Ordering};
use std::time::{Duration, Instant};

use slate_core::session::{ScanProgress, ScanProgressListener};

/// A [`ScanProgressListener`] that writes throttled `Indexing… <n>
/// files` lines to stderr.
///
/// Suppression rules (all enforced here so the call site just installs
/// it unconditionally):
/// - No output at all when stderr is not a TTY.
/// - First line only after the scan has run > 1s (so fast scans stay
///   silent).
/// - At most one line per second thereafter.
pub struct StderrProgress {
    /// Whether stderr is a TTY — decided once at construction.
    tty: bool,
    /// When the scan started, for the > 1s gate.
    start: Instant,
    /// Epoch-millis of the last emitted line (0 = none yet), for the
    /// 1/s throttle. Atomic so the (`&self`) listener callback can
    /// update it without interior-mutability locks.
    last_emit_ms: AtomicU64,
}

/// Threshold before the first progress line (m_spec: "a scan takes
/// > 1s").
const FIRST_LINE_AFTER: Duration = Duration::from_secs(1);
/// Minimum spacing between progress lines (m_spec: "1/s max").
const MIN_INTERVAL: Duration = Duration::from_secs(1);

impl StderrProgress {
    /// Build a listener wrapped for `scan_initial_with_progress`. The
    /// TTY decision is captured now. Returns a trait object (not `Self`)
    /// because that is exactly the `Arc<dyn ScanProgressListener>` the
    /// scan API takes.
    pub fn listener() -> Arc<dyn ScanProgressListener> {
        Arc::new(Self {
            tty: std::io::stderr().is_terminal(),
            start: Instant::now(),
            last_emit_ms: AtomicU64::new(0),
        })
    }

    /// Emit `count` if the TTY + >1s + 1/s gates all pass.
    fn maybe_emit(&self, count: u64) {
        if !self.tty {
            return;
        }
        let elapsed = self.start.elapsed();
        if elapsed < FIRST_LINE_AFTER {
            return;
        }
        // Throttle to 1/s using elapsed-since-start millis as the clock
        // (monotonic; no wall-clock dependency).
        let now_ms = elapsed.as_millis() as u64;
        let last = self.last_emit_ms.load(Ordering::Relaxed);
        if last != 0 && now_ms.saturating_sub(last) < MIN_INTERVAL.as_millis() as u64 {
            return;
        }
        self.last_emit_ms.store(now_ms, Ordering::Relaxed);
        // Ignore write errors: progress is diagnostic, never
        // load-bearing, and a closed stderr must not abort the scan.
        let mut err = std::io::stderr().lock();
        let _ = writeln!(err, "Indexing… {count} files");
    }
}

impl ScanProgressListener for StderrProgress {
    fn on_progress(&self, event: ScanProgress) {
        if let ScanProgress::FileIndexed { indexed, .. } = event {
            self.maybe_emit(indexed);
        }
    }
}
