// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate completions <shell>` — emit a shell-completion script (#639).
//!
//! This is a **meta-command**, in the same family as `--help` and
//! `--version`: it does not read a vault and therefore has **no
//! `<vault-path>` positional**. The m_spec §M-4 global rule that
//! "`<vault-path>` is always the first positional" is a contract for the
//! *vault-reading* commands (`open`, `read`, `search`, …); a
//! completion-script generator has nothing to read, so requiring a vault
//! path would be user-hostile (`slate completions zsh` must work with no
//! vault in sight). This exception is deliberate and matches how every
//! other CLI ships completions.
//!
//! Contract, matching the global one in [`crate::output`]:
//!   - **stdout**: the generated script, and nothing else. No json
//!     envelope, no framing — the output is meant to be redirected into
//!     a completion file verbatim (`slate completions zsh > _slate`), so
//!     a wrapper would corrupt it. `--format` does not apply here.
//!   - **stderr**: empty on success.
//!   - **exit 0** on success; an unknown shell is rejected by clap's
//!     `Shell` ValueEnum parse (exit 2) before this module ever runs.
//!
//! The generated script is produced by `clap_complete::generate` from
//! clap's own live [`clap::Command`], so it always reflects the real
//! sub-command/flag surface — no hand-maintained completion list to
//! drift out of sync with the derive grammar.

use std::io;

use clap::CommandFactory;
use clap_complete::{Generator, Shell};

use crate::Cli;

/// Run `slate completions <shell>`.
///
/// Writes the completion script for `shell` to `out` (stdout in `main`)
/// and returns. Unlike the vault commands this takes no `CancelToken`
/// and opens no session — it is pure code generation over clap's grammar.
///
/// `out` is injected (rather than hard-coding `io::stdout()`) so the
/// integration tests can capture the script — and drive a failing writer
/// — directly; in `main` it is always the process stdout. Write failures
/// (e.g. a broken pipe from `slate completions zsh | head`) propagate to
/// the caller as an `io::Error`, which `main` maps to exit 1 with the
/// `slate: ` prefix — the same broken-pipe discipline as [`crate::output`].
///
/// **Why we buffer, and why `try_generate` and not `generate`:**
/// `clap_complete`'s free `generate()` (and the `Shell::generate` trait
/// method it calls) `.expect(...)`s on a writer error — so a broken pipe
/// mid-write *panics* the process (exit 101, a Rust backtrace on stderr),
/// which would violate the exit-code contract (a broken pipe must be
/// exit 1 with a `slate: ` message, not a panic). Two guards, belt and
/// braces: (1) we render into an in-memory `Vec<u8>`, which can never
/// fail a write, so generation itself is panic-proof regardless of the
/// downstream pipe; (2) we still call the fallible [`Generator::try_generate`]
/// rather than the panicking `generate`, so even a future writer swap
/// stays on the `Result` path. The single real pipe write is our own
/// `write_all` below, whose `io::Error` is catchable. Buffering into
/// one write also *minimizes* partial output — though it cannot
/// guarantee none: a pipe can still break mid-write beyond `PIPE_BUF`,
/// leaving a truncated script (Codoki PR #648). The contract here is
/// no-panic + clean exit 1, not write atomicity. (Codex adversarial
/// finding, round 1; wording tightened per Codoki.)
pub fn run(shell: Shell, out: &mut impl io::Write) -> io::Result<()> {
    // Build clap's `Command` from the derive grammar. `bin_name` is the
    // binary the completions install against — the `[[bin]]` name
    // `slate`, not the crate name `slate-cli` — so the generated
    // function/word list keys off the command the user actually types.
    // `try_generate` doesn't set the bin name for us (the free
    // `generate` does), so set it explicitly before rendering.
    let mut cmd = Cli::command();
    let bin_name = cmd.get_name().to_string();
    cmd.set_bin_name(bin_name);
    // `try_generate` requires a built `Command`; the free `generate`
    // calls `build()` internally, so replicate that here.
    cmd.build();

    // Render into memory — infallible, and keeps the eventual stdout
    // write atomic. `try_generate` on a `Vec<u8>` cannot error, but we
    // propagate defensively rather than unwrap.
    let mut script: Vec<u8> = Vec::new();
    shell.try_generate(&cmd, &mut script)?;

    // The one real (possibly-piped) write. A broken pipe here is a
    // normal `io::Error`, not a panic, so `main` maps it to exit 1.
    out.write_all(&script)?;
    out.flush()
}

#[cfg(test)]
mod tests {
    use super::*;

    /// A writer that returns a `BrokenPipe` error on the first `write`,
    /// standing in for a downstream reader that closed the pipe (the
    /// `slate completions zsh | head` / `| true` case). Proves `run`
    /// returns that error rather than panicking — the regression guard
    /// for the codex round-1 finding that `clap_complete::generate`
    /// `.expect(...)`s on writer errors and would panic (exit 101).
    struct BrokenPipeWriter;

    impl io::Write for BrokenPipeWriter {
        fn write(&mut self, _buf: &[u8]) -> io::Result<usize> {
            Err(io::Error::new(io::ErrorKind::BrokenPipe, "broken pipe"))
        }
        fn flush(&mut self) -> io::Result<()> {
            Err(io::Error::new(io::ErrorKind::BrokenPipe, "broken pipe"))
        }
    }

    #[test]
    fn run_propagates_a_writer_error_instead_of_panicking() {
        // Any shell exercises the same write path; bash is representative.
        let err = run(Shell::Bash, &mut BrokenPipeWriter)
            .expect_err("a failing writer must surface as Err, not a panic");
        assert_eq!(
            err.kind(),
            io::ErrorKind::BrokenPipe,
            "the underlying writer error is propagated verbatim",
        );
    }

    #[test]
    fn run_writes_a_usable_script_to_a_good_writer() {
        // Sanity companion to the failing-writer case: the buffered
        // render + write produces a non-empty script naming the binary
        // and a real sub-command, with no error.
        let mut buf: Vec<u8> = Vec::new();
        run(Shell::Zsh, &mut buf).expect("generation to an in-memory buffer succeeds");
        let script = String::from_utf8(buf).expect("utf8 script");
        assert!(script.contains("slate"), "names the `slate` binary");
        assert!(
            script.contains("sync-check"),
            "enumerates a real sub-command",
        );
    }
}
