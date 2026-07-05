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
