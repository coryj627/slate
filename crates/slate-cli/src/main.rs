// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate` — the command-line interface to a Slate vault (M-4, #535).
//!
//! This file is parsing + dispatch **only** (m_spec §M-4 crate layout):
//! it defines the clap grammar for the global contract
//! (`slate <command> <vault-path> [args…] [--format json|tsv|human]`),
//! installs the Ctrl-C handler, dispatches to the per-command modules,
//! and maps the result to the exit-code contract:
//!
//! | code | meaning |
//! |------|---------|
//! | 0    | success |
//! | 1    | runtime error (message on stderr, `slate: ` prefix) |
//! | 2    | usage error (clap's default) |
//! | 130  | cancelled by Ctrl-C |
//!
//! All the actual work lives in [`commands`]; the format contract lives
//! in [`output`]; the open-vault + error-mapping helpers live in
//! [`session`].

mod commands;
mod output;
mod progress;
mod session;

use std::path::PathBuf;
use std::process::ExitCode;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, Ordering};

use clap::{Parser, Subcommand};

use output::{CommandOutput, OutputFormat, emit};
use session::CliError;
use slate_core::session::CancelToken;

/// Exit code for a Ctrl-C cancellation (128 + SIGINT).
const EXIT_CANCELLED: u8 = 130;
/// Exit code for a runtime error.
const EXIT_RUNTIME: u8 = 1;

/// The `slate` command-line interface to a Slate vault.
///
/// Reads a vault and prints results in one of three formats; writes
/// only the `.slate/` cache, never vault content.
#[derive(Debug, Parser)]
#[command(name = "slate", version, about, long_about = None)]
struct Cli {
    #[command(subcommand)]
    command: Command,
}

#[derive(Debug, Subcommand)]
enum Command {
    /// Open a vault, build/refresh its index, and print a scan summary.
    Open {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// Output format.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// Detect external sync systems managing a vault (no index built).
    SyncCheck {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// Output format.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
}

impl Command {
    /// The sub-command's wire name, stamped into the json envelope.
    fn name(&self) -> &'static str {
        match self {
            Command::Open { .. } => "open",
            Command::SyncCheck { .. } => "sync-check",
        }
    }
}

fn main() -> ExitCode {
    // clap handles `--help`/`--version`/usage errors itself, exiting 2
    // on a usage error (the contract's exit code) before we get here.
    let cli = Cli::parse();

    // --- Ctrl-C handling (installed BEFORE any session open) ---------
    //
    // The first Ctrl-C flips the shared cancel token, so the in-flight
    // scan/search returns `VaultError::Cancelled` and we exit 130
    // cleanly. A second Ctrl-C (handler re-entry) hard-exits
    // immediately — the user asked twice; don't make them wait for a
    // graceful unwind.
    let cancel = CancelToken::new();
    let already_cancelled = Arc::new(AtomicBool::new(false));
    {
        let cancel = cancel.clone();
        let already = Arc::clone(&already_cancelled);
        // A failure to install the handler is not fatal: the CLI still
        // works, it just can't cancel gracefully. Report and continue.
        if let Err(e) = ctrlc::set_handler(move || {
            if already.swap(true, Ordering::SeqCst) {
                // Second Ctrl-C: hard exit.
                std::process::exit(EXIT_CANCELLED as i32);
            }
            cancel.cancel();
        }) {
            eprintln!("slate: warning: could not install Ctrl-C handler: {e}");
        }
    }

    let command_name = cli.command.name();
    let result = dispatch(cli.command, &cancel);

    match result {
        Ok((vault, format, output)) => match emit(format, command_name, &vault, &output) {
            Ok(()) => ExitCode::SUCCESS,
            Err(e) => fail(EXIT_RUNTIME, &CliError::Io(e)),
        },
        Err(CliError::Cancelled) => {
            // Cancelled by Ctrl-C: exit 130. No `slate: ` message — the
            // interrupt is the user's own action, not a fault.
            ExitCode::from(EXIT_CANCELLED)
        }
        Err(e) => fail(EXIT_RUNTIME, &e),
    }
}

/// Parse-then-run a single command. Returns the vault path (for the
/// envelope), the selected format, and the rendered output.
fn dispatch(
    command: Command,
    cancel: &CancelToken,
) -> Result<(String, OutputFormat, CommandOutput), CliError> {
    match command {
        Command::Open { vault_path, format } => {
            let (vault, output) = commands::open::run(&vault_path, cancel)?;
            Ok((vault, format, output))
        }
        Command::SyncCheck { vault_path, format } => {
            let (vault, output) = commands::sync_check::run(&vault_path)?;
            Ok((vault, format, output))
        }
    }
}

/// Print `slate: <error>` to stderr and return the given exit code.
fn fail(code: u8, err: &CliError) -> ExitCode {
    eprintln!("slate: {err}");
    ExitCode::from(code)
}
