// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Minimal stderr sink for slate-core's `log` facade (#507).
//!
//! slate-core routes its non-fatal diagnostics through `log::warn!` /
//! `log::debug!`, which do nothing unless a host installs a [`log::Log`]
//! sink. This module is that sink for the desktop host: a dependency-free
//! writer that formats each record onto `stderr`.
//!
//! Kept out of `lib.rs` so the sink mechanics (the `Log` impl, the level
//! gate, the one-shot install) sit in one place; `lib.rs` just re-exports
//! the `init` entry point through `init_host_logging`.
//!
//! An `os_log` bridge (a `Log` impl that forwards to the macOS unified
//! logging system with a subsystem/category and privacy qualifiers, rather
//! than raw stderr) is explicitly deferred — see #507. Because slate-core
//! already keeps vault paths off `warn`-level messages, raw stderr does not
//! leak note titles at the default level, and the deferral is safe.

use log::{Level, LevelFilter, Metadata, Record};
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};

/// The sink's own ceiling, set by [`init`], independent of the mutable
/// process-global `log::max_level`.
///
/// **Why a sink-local cap and not just the global filter (#507).** The
/// global `log::max_level` is writable by *any* code in the process at any
/// time. If we relied on it alone, something calling
/// `log::set_max_level(Debug)` *after* a release `init(false)` would
/// re-open the gate and let slate-core's path-bearing `debug!` records
/// reach stderr — defeating the release privacy guarantee. So the sink
/// enforces its *own* ceiling too: `enabled` admits a record only when its
/// level is within **both** the global max and this cap. A release install
/// pins this at `Warn` and nothing can widen it back.
///
/// Stored as a `usize` (the `LevelFilter` discriminant, `Off = 0` …
/// `Trace = 5`) so it lives in an `AtomicUsize`. Starts at `Off` — before
/// `init` runs the sink admits nothing.
static SINK_CAP: AtomicUsize = AtomicUsize::new(LevelFilter::Off as usize);

/// Set once a release (`verbose == false`) install has pinned the cap at
/// `Warn`. Makes the release floor **monotonic**: after it, a later
/// `init_host_logging(verbose: true)` (the fn is exported to the host)
/// cannot widen the cap back to `Debug` in this process. Without this, the
/// "nothing can widen the cap back" guarantee would be a lie — a second
/// verbose call would re-open the path-bearing debug records (#507).
static RELEASE_PINNED: AtomicBool = AtomicBool::new(false);

/// Test-only reset of the monotonic release pin, so tests can exercise the
/// "no release install yet" path deterministically regardless of order.
/// Never compiled into shipped code — the production guarantee (a release
/// pin can't be undone) holds because nothing outside `#[cfg(test)]` clears
/// it.
#[cfg(test)]
fn reset_release_pin_for_test() {
    RELEASE_PINNED.store(false, Ordering::Relaxed);
    SINK_CAP.store(LevelFilter::Off as usize, Ordering::Relaxed);
}

/// Read the sink-local cap back into a `LevelFilter`.
fn sink_cap() -> LevelFilter {
    match SINK_CAP.load(Ordering::Relaxed) {
        x if x == LevelFilter::Error as usize => LevelFilter::Error,
        x if x == LevelFilter::Warn as usize => LevelFilter::Warn,
        x if x == LevelFilter::Info as usize => LevelFilter::Info,
        x if x == LevelFilter::Debug as usize => LevelFilter::Debug,
        x if x == LevelFilter::Trace as usize => LevelFilter::Trace,
        _ => LevelFilter::Off,
    }
}

/// The single shared sink instance. `log::set_logger` needs a
/// `&'static dyn Log`, so the sink is a zero-sized unit; its ceiling lives
/// in [`SINK_CAP`], keeping the value `'static`-friendly.
struct StderrSink;

impl log::Log for StderrSink {
    fn enabled(&self, metadata: &Metadata<'_>) -> bool {
        // Admit only within BOTH the global max AND the sink's own ceiling.
        // The sink-local cap is the load-bearing privacy floor (#507): even
        // if the global level is later widened, a release install stays
        // capped at Warn, so path-bearing debug records never reach stderr.
        metadata.level() <= log::max_level() && metadata.level() <= sink_cap()
    }

    fn log(&self, record: &Record<'_>) {
        if !self.enabled(record.metadata()) {
            return;
        }
        // `warn:` / `debug:` prefix + target so a host reader can tell
        // slate-core diagnostics apart from any other stderr noise. The
        // record args come from slate-core, which is responsible for the
        // privacy split (no vault paths at warn level — see #507).
        let level_tag = match record.level() {
            Level::Error => "error",
            Level::Warn => "warn",
            Level::Info => "info",
            Level::Debug => "debug",
            Level::Trace => "trace",
        };
        eprintln!("slate[{level_tag}] {}: {}", record.target(), record.args());
    }

    fn flush(&self) {
        // `eprintln!` writes to a line-buffered/unbuffered stderr; nothing
        // to flush.
    }
}

static SINK: StderrSink = StderrSink;

/// Install the stderr sink at `warn` level (or `debug` when `verbose`).
///
/// The privacy guarantee (#507) — slate-core's path-bearing `debug!`
/// records never reach a shipped-release log — is enforced on two
/// independent levels, because the process-global `log::max_level` is
/// mutable by any code at any time and can't be trusted alone:
///
/// 1. **Sink-local cap ([`SINK_CAP`]).** Set here to `Warn` for a release
///    (`verbose == false`) install and `Debug` for a developer install.
///    The sink's `enabled` honours it, so even if something later widens
///    `log::max_level` back to `Debug`, *our* sink stays capped and the
///    path-bearing records are still dropped. This is the load-bearing
///    guarantee.
/// 2. **Global-level floor.** For `verbose == false` we also lower
///    `log::set_max_level(Warn)` **unconditionally** — even when
///    `set_logger` fails — so a *foreign* logger installed first at `Debug`
///    doesn't keep slate-core's path lines flowing at install time either.
///    (Lowering the global level only *suppresses* records; it can't route
///    ours to a foreign sink.)
///
/// The release cap is **monotonic**: once a `verbose == false` call pins it
/// at `Warn` (setting [`RELEASE_PINNED`]), a later `verbose == true` call —
/// the fn is exported to the host, so a second call is reachable — will
/// **not** widen the sink cap back to `Debug`. This makes "nothing can
/// widen the cap back" literally true within a process. (Widening is only
/// honoured when no release install has happened yet, i.e. a debug build
/// that only ever calls `init(true)`.)
///
/// `log::set_logger` only succeeds once per process; a second call — or a
/// lost race — returns `Err`, which we swallow. For `verbose == true` we
/// raise the *global* level to `Debug` only if we won `set_logger` **and**
/// the release floor isn't pinned: if another logger owns the sink, not
/// unmuting `Debug` avoids surfacing slate-core's path records into an
/// unknown foreign sink.
pub(crate) fn init(verbose: bool) {
    if verbose {
        // Developer opt-in. Honour it only if a release floor hasn't already
        // been pinned this process — otherwise the release cap wins and we
        // leave everything at Warn.
        if !RELEASE_PINNED.load(Ordering::Relaxed) {
            SINK_CAP.store(LevelFilter::Debug as usize, Ordering::Relaxed);
            // Widen the *global* gate to Debug only when our sink owns it.
            if log::set_logger(&SINK).is_ok() {
                log::set_max_level(LevelFilter::Debug);
            }
        }
        // If the release floor is pinned we intentionally do nothing: the
        // Warn cap + Warn global floor already set by the release init stand.
    } else {
        // Release install. Pin the privacy floor monotonically.
        RELEASE_PINNED.store(true, Ordering::Relaxed);
        SINK_CAP.store(LevelFilter::Warn as usize, Ordering::Relaxed);
        // Register our sink if no one has (return value unused — the cap and
        // the global floor below are what enforce privacy).
        let _ = log::set_logger(&SINK);
        // Global privacy floor: cap at Warn regardless of who owns the logger.
        log::set_max_level(LevelFilter::Warn);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    // `enabled` is a `log::Log` method; bring the trait into scope so the
    // tests can call it on the sink directly.
    use log::Log;
    use std::sync::Mutex;

    /// Serialises tests that mutate the process-global `log::max_level`, so a
    /// parallel test can't observe another's transient level change.
    static LEVEL_LOCK: Mutex<()> = Mutex::new(());

    /// The sink's gate is the intersection of the global max level and the
    /// sink-local cap: a `warn` record passes when both allow it; a `debug`
    /// record needs *both* raised to `Debug`. Uses `enabled` directly (a
    /// public `Log` method).
    #[test]
    fn sink_gates_debug_below_warn() {
        let _guard = LEVEL_LOCK.lock().unwrap();
        log::set_max_level(LevelFilter::Warn);
        SINK_CAP.store(LevelFilter::Warn as usize, Ordering::Relaxed);
        let warn_md = Metadata::builder().level(Level::Warn).build();
        let debug_md = Metadata::builder().level(Level::Debug).build();
        assert!(SINK.enabled(&warn_md), "warn should pass at Warn level");
        assert!(
            !SINK.enabled(&debug_md),
            "debug should be gated out at Warn level"
        );

        // Raising BOTH gates admits debug.
        log::set_max_level(LevelFilter::Debug);
        SINK_CAP.store(LevelFilter::Debug as usize, Ordering::Relaxed);
        assert!(
            SINK.enabled(&debug_md),
            "debug should pass once both the global max and sink cap are Debug"
        );

        // Raising only the global level does NOT admit debug — the sink cap
        // still gates it. This is the release privacy guarantee.
        SINK_CAP.store(LevelFilter::Warn as usize, Ordering::Relaxed);
        assert!(
            !SINK.enabled(&debug_md),
            "debug must stay gated when the sink cap is Warn even if the global \
             max is Debug"
        );
    }

    /// `init` is safe to call more than once (the second is a no-op that
    /// must not panic). We can't assert on which call "won" because the
    /// global logger may already be installed by another test binary
    /// linking this crate, so this only pins the idempotency contract.
    #[test]
    fn init_is_idempotent() {
        let _guard = LEVEL_LOCK.lock().unwrap();
        init(false);
        init(true);
        init(false);
        // Reaching here without a panic is the assertion.
    }

    /// Privacy-floor regression (adversarial review): `init(false)` must cap
    /// the process max level at `Warn` even when it *loses* the `set_logger`
    /// race — otherwise a logger installed first at `Debug` would keep
    /// slate-core's path-bearing `debug!` records flowing despite the host
    /// requesting release-style (`verbose: false`) logging.
    ///
    /// We force the lost-race path deterministically: install a logger first
    /// (so the `init(false)` under test cannot win `set_logger`), widen the
    /// level to `Debug` (the state a foreign `Debug` logger would leave), and
    /// assert `init(false)` still forces the cap back to `Warn`. With the old
    /// "only cap on `set_logger` success" logic this assertion fails, because
    /// the losing `init` would leave `Debug` in force.
    #[test]
    fn init_non_verbose_caps_level_even_when_it_loses_the_logger_race() {
        let _guard = LEVEL_LOCK.lock().unwrap();

        // Ensure a logger is installed so the init(false) below cannot win
        // set_logger. (Installing our own SINK is harmless; `init` would
        // install the same one. Ignore the result — another test in this
        // binary may already have installed it.)
        let _ = log::set_logger(&SINK);
        assert!(
            log::set_logger(&SINK).is_err(),
            "a logger must be installed so the init(false) below loses the race"
        );

        // Stand in for a foreign logger having widened the level to Debug.
        log::set_max_level(LevelFilter::Debug);
        assert_eq!(log::max_level(), LevelFilter::Debug, "precondition");

        init(false);

        assert_eq!(
            log::max_level(),
            LevelFilter::Warn,
            "init(false) must force the max level down to Warn even when it \
             loses the set_logger race, so path-bearing debug records stay \
             suppressed"
        );
    }

    /// Sink-cap regression (adversarial review): after a release
    /// `init(false)`, the sink must keep suppressing `Debug` records even if
    /// the *global* `log::max_level` is later widened back to `Debug` by
    /// unrelated code. The sink-local cap is the load-bearing floor; the
    /// global level alone can't be trusted because anyone can raise it.
    #[test]
    fn sink_cap_survives_a_later_global_level_widening() {
        let _guard = LEVEL_LOCK.lock().unwrap();
        reset_release_pin_for_test();

        // Release install pins the sink cap at Warn.
        init(false);

        // Simulate unrelated code re-opening the global gate to Debug after
        // the release sink is installed.
        log::set_max_level(LevelFilter::Debug);
        assert_eq!(log::max_level(), LevelFilter::Debug, "precondition");

        // The sink must still drop Debug (its own cap is Warn), so
        // slate-core's path-bearing debug records never reach stderr.
        let debug_md = Metadata::builder().level(Level::Debug).build();
        let warn_md = Metadata::builder().level(Level::Warn).build();
        assert!(
            !SINK.enabled(&debug_md),
            "sink must suppress Debug after a release install even when the \
             global max was widened back to Debug"
        );
        assert!(
            SINK.enabled(&warn_md),
            "warn records must still pass a release install"
        );
    }

    /// Monotonic-floor regression (adversarial review): a release
    /// `init(false)` must permanently pin the cap — a later
    /// `init(true)` (the fn is exported to the host, so a second verbose
    /// call is reachable) must NOT re-open `Debug`, even if the global level
    /// is also widened. This is the "nothing can widen the cap back"
    /// guarantee made literal.
    #[test]
    fn release_pin_blocks_a_later_verbose_init_from_reopening_debug() {
        let _guard = LEVEL_LOCK.lock().unwrap();
        reset_release_pin_for_test();

        init(false); // pin the release floor
        log::set_max_level(LevelFilter::Debug); // unrelated widening
        init(true); // attempt to re-open Debug — must be ignored

        let debug_md = Metadata::builder().level(Level::Debug).build();
        assert!(
            !SINK.enabled(&debug_md),
            "a verbose init AFTER a release init must not re-admit Debug — the \
             release cap is monotonic"
        );
        assert_eq!(
            sink_cap(),
            LevelFilter::Warn,
            "the sink cap must stay pinned at Warn after a release install"
        );
    }

    /// The verbose path still works when it runs *first* (a debug build that
    /// only ever calls `init(true)`): the cap widens to `Debug`.
    #[test]
    fn verbose_init_widens_when_no_release_floor_is_pinned() {
        let _guard = LEVEL_LOCK.lock().unwrap();
        reset_release_pin_for_test();

        init(true);
        log::set_max_level(LevelFilter::Debug);

        let debug_md = Metadata::builder().level(Level::Debug).build();
        assert!(
            SINK.enabled(&debug_md),
            "a verbose install with no prior release pin must admit Debug"
        );
        assert_eq!(sink_cap(), LevelFilter::Debug);
    }
}
