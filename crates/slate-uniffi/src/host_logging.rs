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
/// Idempotent: `log::set_logger` only succeeds once per process, and a
/// second call — or a lost race with a concurrent installer — returns
/// `Err`, which we swallow. The first successful install's `verbose`
/// choice wins; a later call does not lower or raise the level. This lets
/// a host call it unconditionally at startup.
pub(crate) fn init(verbose: bool) {
    let level = if verbose {
        LevelFilter::Debug
    } else {
        LevelFilter::Warn
    };
    // Set the logger first; only raise the max level if *we* won the race,
    // so a second (e.g. verbose) call can't retroactively unmute debug
    // records for a sink some other installer owns.
    if log::set_logger(&SINK).is_ok() {
        log::set_max_level(level);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    // `enabled` is a `log::Log` method; bring the trait into scope so the
    // tests can call it on the sink directly.
    use log::Log;

    /// The sink's level gate honours the configured max level: a `warn`
    /// record passes at the default (`Warn`) level; a `debug` record does
    /// not. Uses `enabled` directly (a public `Log` method) so the test
    /// doesn't depend on process-global install state, which
    /// `set_logger`'s once-per-process contract makes un-resettable.
    #[test]
    fn sink_gates_debug_below_warn() {
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
        init(false);
        init(true);
        init(false);
        // Reaching here without a panic is the assertion.
    }
}
