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
use clap_complete::{Shell, generate};

use crate::Cli;

/// Run `slate completions <shell>`.
///
/// Writes the completion script for `shell` to `out` (stdout in `main`)
/// and returns. Unlike the vault commands this takes no `CancelToken`
/// and opens no session — it is pure code generation over clap's grammar.
///
/// `out` is injected (rather than hard-coding `io::stdout()`) so the
/// integration tests could capture the script directly; in `main` it is
/// always the process stdout. Write failures (e.g. a broken pipe from
/// `slate completions zsh | head`) propagate to the caller, which maps
/// them to exit 1 — the same broken-pipe discipline as [`crate::output`].
pub fn run(shell: Shell, out: &mut impl io::Write) -> io::Result<()> {
    // Build clap's `Command` from the derive grammar. `bin_name` is the
    // binary the completions install against — the `[[bin]]` name
    // `slate`, not the crate name `slate-cli` — so the generated
    // function/word list keys off the command the user actually types.
    let mut cmd = Cli::command();
    let bin_name = cmd.get_name().to_string();
    // `generate` writes the whole script in one shot; it can only fail on
    // the underlying writer, which we surface rather than swallow.
    generate(shell, &mut cmd, bin_name, out);
    // `generate` flushes nothing itself; force a flush so a broken pipe
    // surfaces here (as an `io::Error`) instead of silently at drop.
    out.flush()
}
