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
use clap_complete::Shell;

use commands::render_template::parse_prompt_kv;
use commands::tasks::TaskFilterChoice;
use output::{CommandOutput, OutputFormat, emit};
use session::CliError;
use slate_core::session::CancelToken;

/// Exit code for a Ctrl-C cancellation (128 + SIGINT).
const EXIT_CANCELLED: u8 = 130;
/// Exit code for a runtime error.
const EXIT_RUNTIME: u8 = 1;
/// Exit code for a usage error surfaced after clap parsing (matches
/// clap's own usage exit code — see the global contract in §M-4).
const EXIT_USAGE: u8 = 2;

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
    /// List a vault's Markdown tasks, filtered by a due-date window.
    ///
    /// Due-date windows use UTC calendar days, matching how Slate stores
    /// and displays due dates.
    Tasks {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// Which due-date window to show (default: all).
        #[arg(long, value_enum, default_value_t = TaskFilterChoice::default())]
        filter: TaskFilterChoice,
        /// Include completed tasks (ignored for --filter overdue, which
        /// always excludes completed tasks).
        #[arg(long)]
        include_completed: bool,
        /// Output format.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// Render a vault template and print the resulting note body.
    RenderTemplate {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// Vault-relative path to the template (e.g. Templates/Daily.md).
        template_path: String,
        /// A prompt response as key=value (split at the first '='; repeat
        /// for multiple). The key is the template's prompt slug.
        #[arg(long = "prompt", value_parser = parse_prompt_kv)]
        prompts: Vec<(String, String)>,
        /// Title for {{title}} (defaults to the template's file stem).
        #[arg(long)]
        title: Option<String>,
        /// Fail (exit 1) if any {{prompt:…}} marker is left unfilled,
        /// instead of warning and rendering the marker literally.
        #[arg(long)]
        strict: bool,
        /// Output format (tsv is rejected — a document body is not a
        /// table).
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// Full-text search the vault; prints ranked hits with snippets.
    Search {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// The FTS5 query.
        query: String,
        /// Maximum number of hits to return (client-side truncation).
        #[arg(long, default_value_t = 50)]
        limit: usize,
        /// Output format.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// Write a note from stdin, with cross-process conflict detection.
    ///
    /// Content is read from stdin to EOF and written verbatim (no added
    /// trailing newline). By default the note must already exist; pass
    /// --create to make a new one.
    ///
    /// Concurrent writers ("the app wins"): a vault can be open in the
    /// Slate app and this CLI at once. `write` observes the note's
    /// current content hash and passes it as a compare-and-swap
    /// precondition to the save. If the app changed the note since that
    /// observation, the write is refused (exit 1) instead of clobbering
    /// the app's edit. A running app won't see this CLI write in an
    /// already-open buffer until it reloads; the app's own save then
    /// conflict-detects the same way. So CLI writes never silently
    /// overwrite app edits, and app edits never silently overwrite CLI
    /// writes. Use --expect-hash <blake3> to pin the precondition
    /// explicitly (a scriptable compare-and-swap across invocations).
    #[command(long_about)]
    Write {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// Vault-relative path of the note to write.
        note_path: String,
        /// Require the note's current content hash to equal this blake3
        /// hash before writing (compare-and-swap). Without it, `write`
        /// uses the note's current indexed hash for an existing note, so
        /// a concurrent app edit still conflict-detects. A missing note
        /// still requires --create — an expect-hash never creates a file
        /// by itself.
        #[arg(long)]
        expect_hash: Option<String>,
        /// Create the note if it does not exist (with an empty-expected
        /// compare-and-swap, so a race to create it is refused rather
        /// than clobbered). On an existing note this flag is a no-op —
        /// the write is the same conditional write either way.
        #[arg(long)]
        create: bool,
        /// Output format.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// Print one note's contents verbatim.
    Read {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// Vault-relative path of the note to read.
        note_path: String,
        /// Output format. `tsv` is unsupported (a note body is not a
        /// table) and exits 2.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// List every indexed file in the vault.
    List {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// Restrict to Markdown notes.
        #[arg(long)]
        markdown_only: bool,
        /// Output format.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// Show one note's backlinks and outgoing links.
    Links {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// Vault-relative path of the note to inspect.
        note_path: String,
        /// Output format.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// List frontmatter property keys, or the files carrying one key.
    Properties {
        /// Path to the vault directory.
        vault_path: PathBuf,
        /// When given, list the files carrying this property key instead
        /// of the vault-wide key summary.
        #[arg(long)]
        key: Option<String>,
        /// Output format.
        #[arg(long, value_enum, default_value_t = OutputFormat::default())]
        format: OutputFormat,
    },
    /// Print a shell-completion script to stdout (#639).
    ///
    /// A meta-command like `--help`: it reads no vault, so it takes **no
    /// `<vault-path>`** — the §M-4 "vault-path first" rule is for the
    /// vault-reading commands only (see `commands::completions`). The
    /// generated script comes straight from this CLI's clap grammar, so
    /// it never drifts from the real command set.
    ///
    /// Install by redirecting stdout to your shell's completion path.
    ///
    /// zsh: `slate completions zsh > ~/.zfunc/_slate` (with `~/.zfunc`
    /// on your `fpath`).
    ///
    /// bash: `slate completions bash > ~/.local/share/bash-completion/completions/slate`.
    ///
    /// fish: `slate completions fish > ~/.config/fish/completions/slate.fish`.
    Completions {
        /// The shell to generate completions for. Unknown shells are a
        /// usage error (exit 2) — the choices are clap_complete's own
        /// `Shell` set (bash, zsh, fish, elvish, powershell).
        #[arg(value_enum)]
        shell: Shell,
    },
}

impl Command {
    /// The sub-command's wire name, stamped into the json envelope.
    fn name(&self) -> &'static str {
        match self {
            Command::Open { .. } => "open",
            Command::SyncCheck { .. } => "sync-check",
            Command::Tasks { .. } => "tasks",
            Command::RenderTemplate { .. } => "render-template",
            Command::Search { .. } => "search",
            Command::Write { .. } => "write",
            Command::Read { .. } => "read",
            Command::List { .. } => "list",
            Command::Links { .. } => "links",
            Command::Properties { .. } => "properties",
            // `completions` never reaches the json envelope (it is
            // handled before dispatch and writes a raw script), so this
            // name is only for exhaustiveness; keep it truthful anyway.
            Command::Completions { .. } => "completions",
        }
    }
}

fn main() -> ExitCode {
    // clap handles `--help`/`--version`/usage errors itself, exiting 2
    // on a usage error (the contract's exit code) before we get here.
    let cli = Cli::parse();

    // `completions` is a meta-command (like `--help`): it opens no
    // session, has no vault, and emits a raw script — not the json
    // envelope — so it short-circuits here, before the Ctrl-C handler
    // and the dispatch/emit machinery. See `commands::completions`.
    if let Command::Completions { shell } = cli.command {
        let stdout = std::io::stdout();
        return match commands::completions::run(shell, &mut stdout.lock()) {
            Ok(()) => ExitCode::SUCCESS,
            // A write failure (e.g. a broken pipe from `… | head`) maps
            // to exit 1 with the standard `slate: ` prefix, matching the
            // emit-error path below.
            Err(e) => fail(EXIT_RUNTIME, &CliError::Io(e)),
        };
    }

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
        // A post-parse usage error (e.g. `read --format tsv` or
        // `render-template --format tsv`) exits 2, matching clap's own
        // usage exit code.
        Err(e @ CliError::Usage { .. }) => fail(EXIT_USAGE, &e),
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
        Command::Tasks {
            vault_path,
            filter,
            include_completed,
            format,
        } => {
            let (vault, output) =
                commands::tasks::run(&vault_path, filter, include_completed, cancel)?;
            Ok((vault, format, output))
        }
        Command::RenderTemplate {
            vault_path,
            template_path,
            prompts,
            title,
            strict,
            format,
        } => {
            // `format` is passed into `run` so the tsv rejection is a
            // usage error (exit 2) decided before any work; it's also
            // returned for `emit` (json/human only reach the emit step).
            let (vault, output) = commands::render_template::run(
                &vault_path,
                &template_path,
                &prompts,
                title.as_deref(),
                strict,
                format,
            )?;
            Ok((vault, format, output))
        }
        Command::Search {
            vault_path,
            query,
            limit,
            format,
        } => {
            let (vault, output) = commands::search::run(&vault_path, &query, limit, cancel)?;
            Ok((vault, format, output))
        }
        Command::Write {
            vault_path,
            note_path,
            expect_hash,
            create,
            format,
        } => {
            let (vault, output) = commands::write::run(
                &vault_path,
                &note_path,
                expect_hash.as_deref(),
                create,
                cancel,
            )?;
            Ok((vault, format, output))
        }
        Command::Read {
            vault_path,
            note_path,
            format,
        } => {
            // A note body is a document, not a table: reject tsv up
            // front (exit 2) before doing any vault work — the same
            // `Usage` path as `render-template --format tsv`.
            if format == OutputFormat::Tsv {
                return Err(CliError::Usage {
                    message: "tsv not supported for read".to_string(),
                });
            }
            let (vault, output) = commands::read::run(&vault_path, &note_path, cancel)?;
            Ok((vault, format, output))
        }
        Command::List {
            vault_path,
            markdown_only,
            format,
        } => {
            let (vault, output) = commands::list::run(&vault_path, markdown_only, cancel)?;
            Ok((vault, format, output))
        }
        Command::Links {
            vault_path,
            note_path,
            format,
        } => {
            let (vault, output) = commands::links::run(&vault_path, &note_path, cancel)?;
            Ok((vault, format, output))
        }
        Command::Properties {
            vault_path,
            key,
            format,
        } => {
            let (vault, output) = commands::properties::run(&vault_path, key.as_deref(), cancel)?;
            Ok((vault, format, output))
        }
        // `completions` is intercepted in `main` (it emits a raw script,
        // not a vault envelope) and never reaches `dispatch`. It is only
        // named here to keep the match exhaustive over `Command`.
        Command::Completions { .. } => unreachable!(
            "completions is handled in main before dispatch and never produces a vault envelope"
        ),
    }
}

/// Print `slate: <error>` to stderr and return the given exit code.
fn fail(code: u8, err: &CliError) -> ExitCode {
    eprintln!("slate: {err}");
    ExitCode::from(code)
}
