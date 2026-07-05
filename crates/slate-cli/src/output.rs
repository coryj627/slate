// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! The output-format layer for the `slate` CLI (M-4, #535).
//!
//! The global output contract from `docs/plans/09_sync_cli/m_spec.md`
//! §M-4 is encoded **once**, here — every command produces its data as
//! a [`serde_json::Value`] plus a human/TSV renderer and hands both to
//! [`emit`], which applies the selected [`OutputFormat`]:
//!
//! - **json**: a single pretty-printed object
//!   `{"schema":"slate.cli.v1","command":"<cmd>","vault":"<abs path>",
//!   "data":{…}}` with a trailing newline. The `data` shapes are the
//!   `slate.cli.v1` **stability contract** (additive evolution only).
//! - **tsv**: a header row then data rows, tab-separated, `\n`-
//!   terminated. Any literal tab or newline inside a value is flattened
//!   to a single space (documented lossy flattening — TSV is the
//!   cut/awk format; use json for fidelity).
//! - **human**: plain lines, screen-reader-friendly — no box-drawing,
//!   no column art, no color, ever.
//!
//! stdout carries data only; stderr carries diagnostics/progress only
//! (see `main.rs` and `session.rs`).

use std::io::{self, Write};

/// The wire schema tag stamped into every json envelope. Breaking
/// changes to any command's `data` shape bump this to `slate.cli.v2`;
/// additive fields do not.
pub const SCHEMA: &str = "slate.cli.v1";

/// The three output formats, selected by `--format` (default `human`).
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, clap::ValueEnum)]
pub enum OutputFormat {
    /// Plain, screen-reader-friendly lines. The default.
    #[default]
    Human,
    /// The `slate.cli.v1` json envelope — the machine-readable format.
    Json,
    /// Header + tab-separated rows for `cut`/`awk`.
    Tsv,
}

/// A fully-rendered command result, format-agnostic. Each command
/// builds one of these; [`emit`] renders it in the selected format.
///
/// `human` and `tsv` are pre-rendered strings the command owns, because
/// their layout is command-specific; only the json envelope is uniform
/// enough to assemble here.
pub struct CommandOutput {
    /// The `data` object for the json envelope — the stability contract.
    pub data: serde_json::Value,
    /// The human-format body (no trailing newline; [`emit`] adds one).
    pub human: String,
    /// The tsv-format body: header row then data rows, already
    /// tab-joined and value-flattened via [`tsv_cell`]. Empty when the
    /// command documents "use --format json for the full report".
    pub tsv: String,
}

/// Render `output` in `format` to stdout, applying the json envelope
/// for [`OutputFormat::Json`].
///
/// `command` is the sub-command name (`"open"`, `"sync-check"`, …) and
/// `vault` is the absolute vault path — both stamped into the json
/// envelope. Returns the underlying write error so `main` can map it to
/// exit 1 rather than panicking on a broken pipe mid-write.
pub fn emit(
    format: OutputFormat,
    command: &str,
    vault: &str,
    output: &CommandOutput,
) -> io::Result<()> {
    let stdout = io::stdout();
    let mut w = stdout.lock();
    match format {
        OutputFormat::Human => {
            w.write_all(output.human.as_bytes())?;
            w.write_all(b"\n")?;
        }
        OutputFormat::Tsv => {
            w.write_all(output.tsv.as_bytes())?;
            w.write_all(b"\n")?;
        }
        OutputFormat::Json => {
            let envelope = serde_json::json!({
                "schema": SCHEMA,
                "command": command,
                "vault": vault,
                "data": output.data,
            });
            // `to_string_pretty` never fails on a value we built, but
            // propagate defensively rather than unwrap.
            let text = serde_json::to_string_pretty(&envelope).map_err(io::Error::other)?;
            w.write_all(text.as_bytes())?;
            w.write_all(b"\n")?;
        }
    }
    w.flush()
}

/// Flatten one TSV cell: replace every literal tab or newline (`\t`,
/// `\n`, `\r`) with a single space so the value can never break the
/// row/column framing. Runs of the replaced characters collapse
/// individually (each becomes one space); this is the documented lossy
/// flattening — callers wanting fidelity use `--format json`.
pub fn tsv_cell(value: &str) -> String {
    value
        .chars()
        .map(|c| match c {
            '\t' | '\n' | '\r' => ' ',
            other => other,
        })
        .collect()
}

/// Join TSV cells with a tab into one row (each cell flattened via
/// [`tsv_cell`]).
pub fn tsv_row<I, S>(cells: I) -> String
where
    I: IntoIterator<Item = S>,
    S: AsRef<str>,
{
    cells
        .into_iter()
        .map(|c| tsv_cell(c.as_ref()))
        .collect::<Vec<_>>()
        .join("\t")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn tsv_cell_flattens_tabs_and_newlines_to_single_space() {
        assert_eq!(tsv_cell("a\tb"), "a b");
        assert_eq!(tsv_cell("line1\nline2"), "line1 line2");
        assert_eq!(tsv_cell("a\r\nb"), "a  b"); // \r and \n each -> space
        assert_eq!(tsv_cell("clean"), "clean");
    }

    #[test]
    fn tsv_row_joins_with_tab_and_flattens_each_cell() {
        assert_eq!(tsv_row(["a", "b", "c"]), "a\tb\tc");
        assert_eq!(tsv_row(["a\tx", "b"]), "a x\tb");
    }

    #[test]
    fn default_format_is_human() {
        assert_eq!(OutputFormat::default(), OutputFormat::Human);
    }
}
