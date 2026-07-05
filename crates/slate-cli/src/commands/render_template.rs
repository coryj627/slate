// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate render-template <vault-path> <template-path> [--prompt k=v …]
//! [--title T] [--strict]` (M-6, #537).
//!
//! Renders a vault template against a [`TemplateContext`] built from the
//! CLI flags and prints the resulting note body. A thin wrapper over
//! [`VaultSession::render_template`], whose path-resolution and error
//! text the CLI reuses verbatim (m_spec §M-6 "exactly the session's
//! `render_template` semantics; missing template → exit 1 with the
//! session's error text").
//!
//! **Unfilled prompts (normative, m_spec §M-6):** before rendering, the
//! template source is scanned for `{{prompt:Label}}` markers via
//! [`extract_template_metadata`] (`templates.rs:134`). Any marker whose
//! slug key has no `--prompt` value is reported as
//! `slate: warning: unfilled prompt 'Label'` on **stderr**, and the
//! render proceeds with the marker left literal (the engine's documented
//! behavior, `templates.rs:94-96`). Exit stays 0 — the literal marker in
//! the output makes the gap visible. `--strict` upgrades any unfilled
//! prompt to **exit 1 before any stdout output**.
//!
//! `data` shape (the `slate.cli.v1` stability contract):
//! ```json
//! { "body": String, "cursor_byte_offset": u64|null,
//!   "unfilled_prompts": [String] }
//! ```
//! `--format tsv` is rejected (exit 2) — a document body is not a table.

use std::collections::HashMap;
use std::path::Path;
use std::time::{SystemTime, UNIX_EPOCH};

use slate_core::{TemplateContext, extract_template_metadata};

use crate::output::{CommandOutput, OutputFormat};
use crate::session::{CliError, OpenedVault, map_vault_error, open_vault};

/// Parse one `--prompt key=value` argument, splitting at the **first**
/// `=` so values may themselves contain `=`. A missing `=` is a usage
/// error (exit 2) — wired as a clap `value_parser` so clap prints the
/// error and exits 2 itself, matching the global "usage → 2" contract.
///
/// The returned key is the slug the template engine keys
/// `TemplateContext::prompt_values` by (the CLI author supplies the slug
/// directly, e.g. `--prompt topic="Q1 review"`).
pub fn parse_prompt_kv(raw: &str) -> Result<(String, String), String> {
    match raw.split_once('=') {
        Some((key, value)) => Ok((key.to_string(), value.to_string())),
        None => Err(format!("expected key=value, got {raw:?} (no '=' found)")),
    }
}

/// Current wall-clock time as epoch millis for `{{date}}`/`{{time}}`
/// substitution. `SystemTime` is the only clock the CLI touches; a
/// before-epoch clock clamps to 0 rather than panicking.
fn now_epoch_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}

/// The vault root's basename, for `{{vault}}`. Falls back to the raw
/// display string when the path has no final component (e.g. `/`).
fn vault_basename(raw_path: &Path) -> String {
    raw_path
        .file_name()
        .map(|n| n.to_string_lossy().into_owned())
        .unwrap_or_else(|| raw_path.display().to_string())
}

/// The template's file stem, the default `{{title}}` when `--title` is
/// absent. Falls back to the raw path string if there's no stem.
fn template_stem(template_path: &str) -> String {
    Path::new(template_path)
        .file_stem()
        .map(|s| s.to_string_lossy().into_owned())
        .unwrap_or_else(|| template_path.to_string())
}

/// Run `slate render-template`.
///
/// `format` is consumed here (not in `main`'s emit step) so the tsv
/// rejection can be a usage error (exit 2) before any work — the body
/// is a document, never a table.
pub fn run(
    raw_path: &Path,
    template_path: &str,
    prompts: &[(String, String)],
    title: Option<&str>,
    strict: bool,
    format: OutputFormat,
) -> Result<(String, CommandOutput), CliError> {
    // tsv is meaningless for a document body — reject before opening the
    // vault (m_spec §M-6). Exit 2 (usage), not 1.
    if format == OutputFormat::Tsv {
        return Err(CliError::Usage {
            message: "tsv not supported for render-template".to_string(),
        });
    }

    let OpenedVault {
        session, abs_path, ..
    } = open_vault(raw_path)?;

    // The context's prompt map is keyed by the engine's slug key, which
    // is exactly what `--prompt key=value` supplies.
    let prompt_values: HashMap<String, String> = prompts.iter().cloned().collect();

    let title = title
        .map(str::to_string)
        .unwrap_or_else(|| template_stem(template_path));
    let context = TemplateContext {
        now_ms: now_epoch_ms(),
        title,
        vault_name: vault_basename(raw_path),
        prompt_values: prompt_values.clone(),
    };

    // Render via the session FIRST so the missing-template / bad-path
    // case surfaces the session's exact error text (m_spec §M-6:
    // "exactly the session's `render_template` semantics; missing
    // template → exit 1 with the session's error text"), before we touch
    // the source for the prompt scan.
    let rendered = session
        .render_template(template_path, context)
        .map_err(map_vault_error)?;

    // Re-read the (now known-resolvable) source to scan for
    // `{{prompt:Label}}` markers. One extra read is fine — the CLI
    // renders a single template, not a picker sweep — and doing it after
    // the render keeps `render_template` as the sole authority on path
    // resolution and error text. Any failure here would be a genuine
    // race (the template vanished between the two reads); surface it.
    let source = session.read_text(template_path).map_err(map_vault_error)?;

    // Collect the labels of every prompt whose slug key wasn't supplied.
    // Declaration order is preserved by `extract_template_metadata`.
    let unfilled: Vec<String> = extract_template_metadata(&source)
        .prompts
        .into_iter()
        .filter(|p| !prompt_values.contains_key(&p.key))
        .map(|p| p.label)
        .collect();

    // Warn (stderr) for every unfilled prompt — the output still shows
    // the literal marker, so the gap is visible either way.
    for label in &unfilled {
        eprintln!("slate: warning: unfilled prompt '{label}'");
    }

    // --strict upgrades unfilled prompts to a hard failure BEFORE any
    // stdout output (nothing is written until `run` returns and `main`
    // calls `emit`). Exit 1 (runtime), distinct from the usage errors.
    if strict && !unfilled.is_empty() {
        return Err(CliError::StrictUnfilledPrompts {
            count: unfilled.len(),
        });
    }

    let data = serde_json::json!({
        "body": rendered.body,
        // Widen the byte offset to u64 for the wire contract (source is
        // usize; the stability contract pins u64|null).
        "cursor_byte_offset": rendered.cursor_byte_offset.map(|o| o as u64),
        "unfilled_prompts": unfilled,
    });

    // Human/json only reach here (tsv rejected above). The human body is
    // the rendered note verbatim; `emit` appends the single trailing
    // newline the global contract mandates.
    let human = rendered.body;

    Ok((
        abs_path,
        CommandOutput {
            data,
            human,
            // tsv is never emitted for this command (rejected above); an
            // empty string keeps the struct total without inviting use.
            tsv: String::new(),
        },
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_prompt_splits_at_first_equals() {
        assert_eq!(
            parse_prompt_kv("topic=Q1 review"),
            Ok(("topic".into(), "Q1 review".into()))
        );
        // Value may itself contain '=' — split only at the first.
        assert_eq!(
            parse_prompt_kv("expr=a=b=c"),
            Ok(("expr".into(), "a=b=c".into()))
        );
        // Empty value is allowed (key present, nothing after '=').
        assert_eq!(parse_prompt_kv("k="), Ok(("k".into(), String::new())));
    }

    #[test]
    fn parse_prompt_missing_equals_is_error() {
        assert!(parse_prompt_kv("noequals").is_err());
    }

    #[test]
    fn template_stem_strips_dir_and_extension() {
        assert_eq!(template_stem("Templates/Daily.md"), "Daily");
        assert_eq!(template_stem("Daily.md"), "Daily");
        assert_eq!(template_stem("Templates/Sub/Note.md"), "Note");
    }

    #[test]
    fn vault_basename_is_final_component() {
        assert_eq!(vault_basename(Path::new("/home/me/MyVault")), "MyVault");
        assert_eq!(vault_basename(Path::new("MyVault")), "MyVault");
    }
}
