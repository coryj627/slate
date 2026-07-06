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

/// The single shared sink instance. `log::set_logger` needs a
/// `&'static dyn Log`, so the sink is a zero-sized unit with its
/// verbosity carried out-of-band by the max-level filter (below), not
/// on the struct — keeping it `'static`-friendly without interior state.
struct StderrSink;

impl log::Log for StderrSink {
    fn enabled(&self, metadata: &Metadata<'_>) -> bool {
        // Honour the global max level `init` set. `log`'s macros already
        // check this before constructing a record, but a `Log` impl is
        // expected to gate too (e.g. for `log::logger().log(..)` callers).
        metadata.level() <= log::max_level()
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
/// `log::set_logger` only succeeds once per process, so a second call — or
/// a lost race with a concurrent installer — returns `Err`, which we
/// swallow. The interesting case is what happens to the process-global
/// `max_level` when we *lose* that race:
///
/// * **`verbose == false` (the release privacy floor).** We call
///   `set_max_level(Warn)` **unconditionally** — even when `set_logger`
///   fails. The privacy guarantee (#507) is that slate-core's path-bearing
///   `debug!` records never reach shipped logs; that must not depend on
///   *us* owning the global logger. If some other logger were installed
///   first at `Debug`, and we only capped the level on a successful
///   `set_logger`, those path/cache lines would flow to that foreign
///   logger despite the host asking for release-style logging. Forcing the
///   cap down closes that bypass. (Lowering the max level only *suppresses*
///   records; it can't route ours to a foreign sink.)
/// * **`verbose == true` (developer opt-in).** We raise to `Debug` only if
///   *we* won `set_logger`. If another logger already owns the sink, we
///   deliberately do **not** unmute `Debug` for it — surfacing slate-core's
///   path-bearing records into an unknown foreign sink is exactly what the
///   privacy rule guards against, and a lost race here is not the trusted
///   opt-in the caller intended.
pub(crate) fn init(verbose: bool) {
    let won = log::set_logger(&SINK).is_ok();
    if verbose {
        // Only widen to Debug when our sink is the one that will receive the
        // path-bearing records.
        if won {
            log::set_max_level(LevelFilter::Debug);
        }
    } else {
        // Privacy floor: cap at Warn regardless of who owns the logger.
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

    /// The sink's level gate honours the configured max level: a `warn`
    /// record passes at the default (`Warn`) level; a `debug` record does
    /// not. Uses `enabled` directly (a public `Log` method) so the test
    /// doesn't depend on process-global install state, which
    /// `set_logger`'s once-per-process contract makes un-resettable.
    #[test]
    fn sink_gates_debug_below_warn() {
        let _guard = LEVEL_LOCK.lock().unwrap();
        log::set_max_level(LevelFilter::Warn);
        let warn_md = Metadata::builder().level(Level::Warn).build();
        let debug_md = Metadata::builder().level(Level::Debug).build();
        assert!(SINK.enabled(&warn_md), "warn should pass at Warn level");
        assert!(
            !SINK.enabled(&debug_md),
            "debug should be gated out at Warn level"
        );

        log::set_max_level(LevelFilter::Debug);
        assert!(
            SINK.enabled(&debug_md),
            "debug should pass once max level is raised to Debug"
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
}
