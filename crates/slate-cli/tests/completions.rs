// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Integration tests for `slate completions <shell>` (#639).
//!
//! Drives the built binary via `assert_cmd`. `completions` is a
//! meta-command (like `--help`): it reads no vault, so — unlike every
//! test in `cli.rs` — these runs pass no `<vault-path>` and open no
//! fixture. The contract under test (see `commands::completions`):
//!   - each supported shell emits a non-empty script naming the binary
//!     (`slate`) and at least one real sub-command, exit 0, stderr empty;
//!   - an unknown shell is a clap ValueEnum parse error → exit 2;
//!   - `completions` with no shell arg is a clap usage error → exit 2.

use assert_cmd::Command;
use predicates::prelude::*;

/// The binary under test.
fn slate() -> Command {
    Command::cargo_bin("slate").expect("slate binary builds")
}

/// The five shells clap_complete's `Shell` ValueEnum offers. Kept as the
/// single source of truth for the per-shell assertions below; if
/// clap_complete adds a shell, this list (and the coverage) grows with a
/// one-line edit.
const SUPPORTED_SHELLS: &[&str] = &["bash", "zsh", "fish", "elvish", "powershell"];

/// A real sub-command name that must appear in a correct completion
/// script. `sync-check` is chosen because its hyphen makes it a
/// distinctive token unlikely to collide with shell-script boilerplate —
/// a script that names it genuinely enumerated this CLI's grammar rather
/// than emitting an empty stub.
const REAL_SUBCOMMAND: &str = "sync-check";

#[test]
fn each_supported_shell_emits_a_usable_script() {
    for shell in SUPPORTED_SHELLS {
        let out = slate()
            .arg("completions")
            .arg(shell)
            .assert()
            .success() // exit 0
            .stderr(predicate::str::is_empty()) // nothing on stderr
            .get_output()
            .stdout
            .clone();

        let script = String::from_utf8(out).expect("completion script is utf8");
        assert!(
            !script.trim().is_empty(),
            "{shell}: script must be non-empty",
        );
        // The generated script drives completion for the `slate` binary,
        // so its name must appear (every generator embeds it — as
        // `#compdef slate`, `_slate()`, `complete -c slate`, …).
        assert!(
            script.contains("slate"),
            "{shell}: script must name the `slate` binary",
        );
        // And it must have enumerated the real grammar, not emitted a
        // bare stub — a genuine sub-command name proves that.
        assert!(
            script.contains(REAL_SUBCOMMAND),
            "{shell}: script must list the `{REAL_SUBCOMMAND}` sub-command",
        );
    }
}

#[test]
fn unknown_shell_is_a_usage_error() {
    // `notashell` is not in clap_complete's `Shell` set, so clap rejects
    // it at parse time with its ValueEnum error and exits 2 — the same
    // usage-error code as any bad flag (m_spec §M-4 exit codes).
    slate()
        .arg("completions")
        .arg("notashell")
        .assert()
        .code(2)
        .stdout(predicate::str::is_empty());
}

#[test]
fn missing_shell_arg_is_a_usage_error() {
    // The `<SHELL>` positional is required; omitting it is a clap usage
    // error → exit 2, with nothing on stdout (no partial script).
    slate()
        .arg("completions")
        .assert()
        .code(2)
        .stdout(predicate::str::is_empty());
}

/// A closed stdout pipe must NOT panic the process. `clap_complete`'s
/// `generate` `.expect(...)`s on writer errors, so the naive
/// implementation exited 101 with a Rust backtrace on stderr when the
/// reader closed early (`slate completions zsh | head`); `run` now
/// buffers + writes fallibly, mapping a broken pipe to the exit-1
/// `slate: ` path. This process-level guard complements the in-crate
/// unit test on `run` with a failing writer.
///
/// Unix-only: it relies on spawning the child with its stdout wired to a
/// pipe we drop immediately, and on POSIX broken-pipe (`EPIPE`)
/// semantics. The unit test in `commands::completions` covers the
/// writer-error path portably.
#[cfg(unix)]
#[test]
fn closed_stdout_pipe_does_not_panic() {
    use std::process::{Command as StdCommand, Stdio};

    let bin = assert_cmd::cargo::cargo_bin("slate");

    // Spawn with a piped stdout, then drop our read end at once so the
    // child's writes hit a broken pipe. `elvish` is picked because its
    // script is among the larger ones, maximizing the chance the write
    // is still in flight when the reader vanishes.
    let mut child = StdCommand::new(&bin)
        .args(["completions", "elvish"])
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .expect("spawn slate completions");

    // Drop the read handle immediately: subsequent child writes → EPIPE.
    drop(child.stdout.take());

    let output = child.wait_with_output().expect("wait on child");

    // The exact exit code depends on the write/close race (0 if the
    // whole buffered script landed before we closed; 1 if the write hit
    // the broken pipe). Either is acceptable — the contract is only that
    // it must NOT be the panic code 101, and stderr must never carry a
    // Rust panic/backtrace.
    let code = output.status.code();
    assert_ne!(code, Some(101), "must not panic on a broken stdout pipe");
    let stderr = String::from_utf8_lossy(&output.stderr);
    assert!(
        !stderr.contains("panicked"),
        "stderr must not carry a Rust panic; got: {stderr}",
    );
}
