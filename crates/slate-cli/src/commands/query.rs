// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `slate query <vault-path> (--base <path> [--view <name>] | --saved <name>)`.
//!
//! N2-3's additive `slate.cli.v1` verb for Bases automation. Unlike the
//! older M verbs, this command has its own output menu: json (default),
//! csv, markdown. Successful json keeps the standard envelope and uses
//! `data` as the Obsidian-parity row array: each row is an object keyed
//! by column label plus the reserved `path` field. CSV and Markdown
//! reuse slate-core's Bases export renderer and are emitted as raw
//! document bytes.
//!
//! Selection/execution errors that are part of the query surface
//! (unknown view, unknown saved-query name, fail-loud `view_error`) are
//! machine-readable json envelopes on stdout with exit 2. I/O/session
//! failures still use the global CLI error path (stderr, exit 1).

use std::collections::BTreeMap;
use std::path::Path;

use serde_json::{Map, Value};
use slate_core::ExportFormat;
use slate_core::session::{BaseViewSummary, BasesResultSet, VaultSession, export_bases_result};

use crate::output::{CommandOutput, OutputFormat};
use crate::session::{CliError, map_vault_error, open_and_scan};
use slate_core::session::CancelToken;

/// Exit code for query-surface errors: bad view selection or a Bases
/// fail-loud `view_error`. This intentionally matches clap usage's `2`
/// because the command was understood but the requested view is not
/// executable as addressed.
pub const EXIT_QUERY_ERROR: u8 = 2;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default, clap::ValueEnum)]
pub enum QueryFormat {
    #[default]
    Json,
    Csv,
    Markdown,
}

pub struct QueryArgs<'a> {
    pub raw_path: &'a Path,
    pub base_path: Option<&'a str>,
    pub saved_name: Option<&'a str>,
    pub view_name: Option<&'a str>,
    pub format: QueryFormat,
    pub limit: Option<usize>,
    pub this_path: Option<&'a str>,
    pub cancel: &'a CancelToken,
}

pub fn run(args: QueryArgs<'_>) -> Result<(String, OutputFormat, CommandOutput, u8), CliError> {
    let source = resolve_source(&args)?;
    let (session, abs_path) = open_and_scan(args.raw_path, args.cancel)?;

    let (handle, view) = match source {
        QuerySource::Base(path) => {
            let handle = session.open_base(path).map_err(map_vault_error)?;
            let views = session.base_views(handle).map_err(map_vault_error)?;
            let view = match resolve_view(&views, args.view_name) {
                Ok(view) => view,
                Err(failure) => return Ok(query_failure(abs_path, failure)),
            };
            (handle, view)
        }
        QuerySource::Saved(name) => match open_saved_by_name(&session, name)? {
            Ok(handle) => (handle, 0),
            Err(failure) => return Ok(query_failure(abs_path, failure)),
        },
    };

    let mut result = session
        .base_execute(
            handle,
            view,
            args.this_path.map(str::to_string),
            None,
            args.cancel,
        )
        .map_err(map_vault_error)?;
    apply_limit(&mut result, args.limit);

    if let Some(message) = result.view_error.clone() {
        return Ok(query_failure(abs_path, QueryFailure::ViewError { message }));
    }

    let output = match args.format {
        QueryFormat::Json => CommandOutput {
            data: rows_json(&result),
            human: String::new(),
            tsv: String::new(),
            human_verbatim: false,
        },
        QueryFormat::Csv => raw_export_output(export_bases_result(&result, ExportFormat::Csv)),
        QueryFormat::Markdown => {
            raw_export_output(export_bases_result(&result, ExportFormat::Markdown))
        }
    };
    let output_format = match args.format {
        QueryFormat::Json => OutputFormat::Json,
        QueryFormat::Csv | QueryFormat::Markdown => OutputFormat::Human,
    };
    Ok((abs_path, output_format, output, 0))
}

enum QuerySource<'a> {
    Base(&'a str),
    Saved(&'a str),
}

fn resolve_source<'a>(args: &QueryArgs<'a>) -> Result<QuerySource<'a>, CliError> {
    match (args.base_path, args.saved_name, args.view_name) {
        (Some(path), None, _) => Ok(QuerySource::Base(path)),
        (None, Some(_), Some(_)) => Err(CliError::Usage {
            message: "--view requires --base".to_string(),
        }),
        (None, Some(name), None) => Ok(QuerySource::Saved(name)),
        _ => Err(CliError::Usage {
            message: "query requires exactly one of --base or --saved".to_string(),
        }),
    }
}

fn open_saved_by_name(
    session: &VaultSession,
    requested: &str,
) -> Result<Result<u64, QueryFailure>, CliError> {
    let saved = session.list_saved_queries().map_err(map_vault_error)?;
    if let Some(hit) = saved.iter().find(|summary| summary.name == requested) {
        return Ok(Ok(session
            .open_saved_query(&hit.id)
            .map_err(map_vault_error)?));
    }
    Ok(Err(QueryFailure::UnknownSavedQuery {
        requested: requested.to_string(),
        available: saved.into_iter().map(|summary| summary.name).collect(),
    }))
}

fn resolve_view(views: &[BaseViewSummary], requested: Option<&str>) -> Result<u32, QueryFailure> {
    match requested {
        None => {
            if views.is_empty() {
                Err(QueryFailure::UnknownView {
                    requested: "first view".to_string(),
                    available: Vec::new(),
                })
            } else {
                Ok(0)
            }
        }
        Some(name) => views
            .iter()
            .position(|view| view.name == name)
            .map(|idx| idx as u32)
            .ok_or_else(|| QueryFailure::UnknownView {
                requested: name.to_string(),
                available: views.iter().map(|view| view.name.clone()).collect(),
            }),
    }
}

fn apply_limit(result: &mut BasesResultSet, limit: Option<usize>) {
    if let Some(limit) = limit {
        result.rows.truncate(limit);
        result.shown_count = result.rows.len() as u64;
    }
}

fn rows_json(result: &BasesResultSet) -> Value {
    Value::Array(
        result
            .rows
            .iter()
            .map(|row| {
                let mut object = Map::new();
                object.insert("path".to_string(), Value::String(row.file_path.clone()));
                let mut used = BTreeMap::from([("path".to_string(), 1_usize)]);
                for (column, value) in result.columns.iter().zip(&row.values) {
                    let key = unique_key(&column.label, &mut used);
                    object.insert(key, Value::String(value.display.clone()));
                }
                Value::Object(object)
            })
            .collect(),
    )
}

fn unique_key(label: &str, used: &mut BTreeMap<String, usize>) -> String {
    let base = if label.is_empty() { "column" } else { label };
    let entry = used.entry(base.to_string()).or_insert(0);
    *entry += 1;
    if *entry == 1 {
        base.to_string()
    } else {
        format!("{base} ({entry})")
    }
}

fn raw_export_output(body: String) -> CommandOutput {
    CommandOutput {
        data: Value::Null,
        human: body,
        tsv: String::new(),
        human_verbatim: true,
    }
}

enum QueryFailure {
    UnknownView {
        requested: String,
        available: Vec<String>,
    },
    UnknownSavedQuery {
        requested: String,
        available: Vec<String>,
    },
    ViewError {
        message: String,
    },
}

fn query_failure(
    abs_path: String,
    failure: QueryFailure,
) -> (String, OutputFormat, CommandOutput, u8) {
    let data = match failure {
        QueryFailure::UnknownView {
            requested,
            available,
        } => serde_json::json!({
            "error": {
                "kind": "unknown_view",
                "message": format!("unknown view {requested:?}"),
            },
            "available_views": available,
        }),
        QueryFailure::UnknownSavedQuery {
            requested,
            available,
        } => serde_json::json!({
            "error": {
                "kind": "unknown_saved_query",
                "message": format!("unknown saved query {requested:?}"),
            },
            "available_saved_queries": available,
        }),
        QueryFailure::ViewError { message } => serde_json::json!({
            "error": {
                "kind": "view_error",
                "message": message,
            },
        }),
    };
    (
        abs_path,
        OutputFormat::Json,
        CommandOutput {
            data,
            human: String::new(),
            tsv: String::new(),
            human_verbatim: false,
        },
        EXIT_QUERY_ERROR,
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    fn args<'a>(raw_path: &'a Path, cancel: &'a CancelToken) -> QueryArgs<'a> {
        QueryArgs {
            raw_path,
            base_path: None,
            saved_name: None,
            view_name: None,
            format: QueryFormat::Json,
            limit: None,
            this_path: None,
            cancel,
        }
    }

    #[test]
    fn run_rejects_missing_source_before_opening_vault() {
        let vault = TempDir::new().unwrap();
        let cancel = CancelToken::new();
        let err = match run(args(vault.path(), &cancel)) {
            Err(err) => err,
            Ok(_) => panic!("missing query source should be rejected"),
        };
        assert!(matches!(err, CliError::Usage { .. }));
        assert!(
            !vault.path().join(".slate/cache.sqlite").exists(),
            "usage validation should run before open_and_scan"
        );
    }

    #[test]
    fn run_rejects_saved_view_before_opening_vault() {
        let vault = TempDir::new().unwrap();
        let cancel = CancelToken::new();
        let mut query_args = args(vault.path(), &cancel);
        query_args.saved_name = Some("Saved");
        query_args.view_name = Some("View");

        let err = match run(query_args) {
            Err(err) => err,
            Ok(_) => panic!("saved query with --view should be rejected"),
        };
        assert!(matches!(err, CliError::Usage { .. }));
        assert!(
            !vault.path().join(".slate/cache.sqlite").exists(),
            "usage validation should run before open_and_scan"
        );
    }

    #[test]
    fn unique_key_stabilizes_empty_duplicate_and_reserved_labels() {
        let mut used = BTreeMap::from([("path".to_string(), 1_usize)]);

        assert_eq!(unique_key("", &mut used), "column");
        assert_eq!(unique_key("label", &mut used), "label");
        assert_eq!(unique_key("label", &mut used), "label (2)");
        assert_eq!(unique_key("path", &mut used), "path (2)");
    }
}
