// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests -- Bases handle API.
//!
//! Census scale follows the existing session convention: default runs are
//! quick enough for normal CI; `SLATE_CENSUS_FULL=1` expands the generated
//! permutation corpus for pre-push confirmation evidence.

use super::common::*;
use super::*;
use std::path::{Path, PathBuf};

type BasesResultSignature = (
    Vec<(String, Vec<String>)>,
    Vec<(String, u64, u64)>,
    u64,
    u64,
);

fn bases_census_seed_count() -> u64 {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        120
    } else {
        24
    }
}

struct SplitMix64(u64);
impl SplitMix64 {
    fn next(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
        z ^ (z >> 31)
    }

    fn below(&mut self, n: usize) -> usize {
        (self.next() % n as u64) as usize
    }
}

#[test]
fn bases_list_open_views_execute_and_close_handle() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Reading.base",
            br#"views:
  - type: table
    name: Reading
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
"#,
        )
        .unwrap();
        p.write_file(
            "Notes/Alpha.md",
            br#"---
status: active
---
# Alpha
"#,
        )
        .unwrap();
        p.write_file(
            "Notes/Beta.md",
            br#"---
status: done
---
# Beta
"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let summaries = session.bases_list().unwrap();
    assert_eq!(summaries.len(), 1);
    assert_eq!(summaries[0].path, "Queries/Reading.base");
    assert_eq!(summaries[0].name, "Reading");
    assert_eq!(summaries[0].view_count, 1);
    assert_eq!(summaries[0].warning_count, 0);

    let handle = session.open_base("Queries/Reading.base").unwrap();
    let views = session.base_views(handle).unwrap();
    assert_eq!(views.len(), 1);
    assert_eq!(views[0].name, "Reading");
    assert_eq!(views[0].view_type, "table");
    assert_eq!(views[0].source, "files");
    assert_eq!(views[0].status, BaseViewStatus::Executable);

    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(result.total_count, 2);
    assert_eq!(result.shown_count, 2);
    assert_eq!(
        result
            .columns
            .iter()
            .map(|c| c.label.as_str())
            .collect::<Vec<_>>(),
        vec!["file.name", "status"]
    );
    assert_eq!(
        result
            .rows
            .iter()
            .map(|r| r.file_path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Alpha.md", "Notes/Beta.md"]
    );
    assert_eq!(result.rows[0].values[0].display, "Alpha.md");
    assert_eq!(result.rows[0].values[1].display, "active");
    assert_eq!(result.rows[1].values[0].display, "Beta.md");
    assert_eq!(result.rows[1].values[1].display, "done");
    assert!(result.view_error.is_none());
    assert!(result.audio_summary.contains("2 notes"));

    session.close_base(handle);
    let err = session.base_views(handle).unwrap_err();
    assert!(err.to_string().contains("unknown base handle"));
    session.close_base(handle);
}

#[test]
fn base_execute_quick_filter_and_export_use_displayed_values() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Reading.base",
            br#"views:
  - type: table
    name: Reading
    limit: 1
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
    summaries:
      status: count
"#,
        )
        .unwrap();
        p.write_file(
            "Notes/Alpha.md",
            br#"---
status: active
---
# Alpha
"#,
        )
        .unwrap();
        p.write_file(
            "Notes/Beta.md",
            br#"---
status: done
---
# Beta
"#,
        )
        .unwrap();
        p.write_file(
            "Notes/Gamma.md",
            "---\nstatus: café\n---\n# Gamma\n".as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session.open_base("Queries/Reading.base").unwrap();

    let filtered = session
        .base_execute(
            handle,
            0,
            None,
            Some("DONE".to_string()),
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(filtered.total_count, 1);
    assert_eq!(filtered.shown_count, 1);
    assert_eq!(filtered.rows[0].file_path, "Notes/Beta.md");
    assert_eq!(filtered.rows[0].values[0].display, "Beta.md");
    assert_eq!(filtered.rows[0].values[1].display, "done");
    assert_eq!(filtered.summaries[0].column_id, "status");
    assert_eq!(filtered.summaries[0].summary, "count");
    assert_eq!(filtered.summaries[0].value.display, "1");
    assert_eq!(filtered.audio_summary, "1 note.");

    let accent_filtered = session
        .base_execute(
            handle,
            0,
            None,
            Some("CAFE".to_string()),
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(accent_filtered.total_count, 1);
    assert_eq!(accent_filtered.shown_count, 1);
    assert_eq!(accent_filtered.rows[0].file_path, "Notes/Gamma.md");
    assert_eq!(accent_filtered.rows[0].values[1].display, "café");

    let csv = session
        .base_export(handle, 0, ExportFormat::Csv, Some("done".to_string()))
        .unwrap();
    assert_eq!(csv, "file.name,status\r\nBeta.md,done\r\n");

    let markdown = session
        .base_export(handle, 0, ExportFormat::Markdown, Some("done".to_string()))
        .unwrap();
    assert_eq!(
        markdown,
        "| file.name | status |\n| --- | --- |\n| Beta.md | done |\n"
    );
}

#[test]
fn open_query_and_open_dql_execute_ephemeral_handles_with_this_precedence() {
    let (_tmp, session) = make_vault(|p| {
        p.create_dir("Queries").unwrap();
        p.write_file("Notes/Alpha.md", b"# Alpha\n").unwrap();
        p.write_file("Notes/Beta.md", b"# Beta\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let (base, warnings) = crate::bases::parse_base(
        r#"formulas:
  selfName: "this.file.name"
views:
  - type: table
    name: Preview
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - formula.selfName
"#,
    );
    assert!(warnings.is_empty(), "{warnings:?}");
    let query_json = serde_json::to_string(&crate::bases::view_query(&base, 0)).unwrap();
    let query_handle = session
        .open_query(&query_json, Some("Notes/Alpha.md".to_string()))
        .unwrap();

    let default_this = session
        .base_execute(query_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(default_this.rows[0].values[1].display, "Alpha.md");

    let override_this = session
        .base_execute(
            query_handle,
            0,
            Some("Notes/Beta.md".to_string()),
            None,
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(override_this.rows[0].values[1].display, "Beta.md");

    let dql_handle = session
        .open_dql(
            "TABLE WITHOUT ID file.name AS \"Name\"\nFROM \"Notes\"\n",
            None,
        )
        .unwrap();
    let dql = session
        .base_execute(dql_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        dql.columns
            .iter()
            .map(|c| c.label.as_str())
            .collect::<Vec<_>>(),
        vec!["Name"]
    );
    assert_eq!(
        dql.rows
            .iter()
            .map(|r| r.values[0].display.as_str())
            .collect::<Vec<_>>(),
        vec!["Alpha.md", "Beta.md"]
    );
}

#[test]
fn base_apply_edit_saves_base_file_and_refreshes_open_handle() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Reading.base",
            br#"views:
  - type: table
    name: Reading
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
"#,
        )
        .unwrap();
        p.write_file("Notes/Alpha.md", b"# Alpha\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Reading.base").unwrap();
    session
        .base_apply_edit(
            handle,
            crate::bases::BaseEdit::RenameView {
                view: 0,
                name: "Renamed".to_string(),
            },
        )
        .unwrap();

    assert!(
        session
            .read_text("Queries/Reading.base")
            .unwrap()
            .contains("Renamed")
    );
    assert_eq!(session.base_views(handle).unwrap()[0].name, "Renamed");
    assert_eq!(
        session
            .bases_list()
            .unwrap()
            .into_iter()
            .find(|summary| summary.path == "Queries/Reading.base")
            .unwrap()
            .view_count,
        1
    );
}

#[test]
fn save_query_as_base_and_dql_as_base_write_canonical_executable_base() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Notes/Alpha.md", b"# Alpha\n").unwrap();
        p.write_file("Notes/Beta.md", b"# Beta\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let dql = "TABLE WITHOUT ID file.name AS \"Name\"\nFROM \"Notes\"\n";
    let (query, warnings) = crate::bases::dql::parse_dql(dql);
    assert_eq!(warnings, []);
    let query_json = serde_json::to_string(&query).unwrap();

    session
        .save_query_as_base(&query_json, "Queries/Saved.base")
        .unwrap();
    let saved_handle = session.open_base("Queries/Saved.base").unwrap();
    let saved = session
        .base_execute(saved_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(saved.columns[0].label, "Name");
    assert_eq!(
        saved
            .rows
            .iter()
            .map(|r| r.values[0].display.as_str())
            .collect::<Vec<_>>(),
        vec!["Alpha.md", "Beta.md"]
    );

    let converted = session.dql_as_base(dql).unwrap();
    assert!(converted.contains("displayName: Name"));
    let inline_handle = session.open_base_inline(&converted, None).unwrap();
    let inline = session
        .base_execute(inline_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(inline.columns[0].label, "Name");
    assert_eq!(inline.rows.len(), 2);

    let err = session
        .dql_as_base("TABLE file.name\nGROUP BY status\n")
        .unwrap_err();
    assert!(err.to_string().contains("GROUP BY"), "{err}");

    let (cards_base, cards_warnings) = crate::bases::parse_base(
        r#"views:
  - type: cards
    name: Cards
    order:
      - file.name
"#,
    );
    assert!(cards_warnings.is_empty(), "{cards_warnings:?}");
    let cards_query_json =
        serde_json::to_string(&crate::bases::view_query(&cards_base, 0)).unwrap();
    let fallback_err = session
        .save_query_as_base(&cards_query_json, "Queries/Cards.base")
        .unwrap_err();
    assert!(
        fallback_err.to_string().contains("fallback"),
        "{fallback_err}"
    );

    let mut random_query = query.clone();
    random_query.formulas = vec![(
        "dice".to_string(),
        crate::bases::expr::parse_expr("random()").unwrap(),
    )];
    random_query.columns = vec![crate::bases::ColumnSelection {
        id: "formula.dice".to_string(),
        display_name: None,
    }];
    let random_err = session
        .save_query_as_base(
            &serde_json::to_string(&random_query).unwrap(),
            "Queries/Random.base",
        )
        .unwrap_err();
    assert!(random_err.to_string().contains("random()"), "{random_err}");

    let mut median_query = query.clone();
    median_query.summaries = vec![(
        "file.name".to_string(),
        crate::bases::SummaryRef::Builtin("median".to_string()),
    )];
    let median_err = session
        .save_query_as_base(
            &serde_json::to_string(&median_query).unwrap(),
            "Queries/Median.base",
        )
        .unwrap_err();
    assert!(median_err.to_string().contains("median"), "{median_err}");
}

#[test]
fn census_bases_roundtrip() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Notes/Alpha.md", b"---\nstatus: active\n---\n# Alpha\n")
            .unwrap();
        p.write_file("Notes/Beta.md", b"---\nstatus: archived\n---\n# Beta\n")
            .unwrap();
        p.write_file("Notes/Gamma.md", b"---\nstatus: done\n---\n# Gamma\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let (base, warnings) = crate::bases::parse_base(
        r#"filters:
  and:
    - "file.inFolder(\"Notes\")"
    - or:
        - "status == \"active\""
        - "status == \"done\""
properties:
  status:
    displayName: Status
views:
  - type: table
    name: Roundtrip
    order:
      - file.name
      - status
"#,
    );
    assert!(warnings.is_empty(), "{warnings:?}");
    let query = crate::bases::view_query(&base, 0);
    let query_json = serde_json::to_string(&query).unwrap();

    let query_handle = session.open_query(&query_json, None).unwrap();
    let query_result = session
        .base_execute(query_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    session
        .save_query_as_base(&query_json, "Queries/Roundtrip.base")
        .unwrap();
    let saved_handle = session.open_base("Queries/Roundtrip.base").unwrap();
    let saved_result = session
        .base_execute(saved_handle, 0, None, None, &CancelToken::new())
        .unwrap();

    assert_eq!(result_matrix(&saved_result), result_matrix(&query_result));
    assert_eq!(
        saved_result
            .columns
            .iter()
            .map(|column| column.label.as_str())
            .collect::<Vec<_>>(),
        vec!["file.name", "Status"]
    );
    assert_eq!(
        saved_result
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Alpha.md", "Notes/Gamma.md"]
    );
}

#[test]
fn census_bases_determinism() {
    let base = br#"views:
  - type: table
    name: ByStatus
    filters: "file.inFolder(\"Notes\")"
    groupBy:
      property: status
      direction: ASC
    order:
      - file.name
      - status
    slate:
      sort:
        - expr: file.name
          direction: asc
"#;
    let notes = [
        ("Notes/Zeta.md", "---\nstatus: later\n---\n# Zeta\n"),
        ("Notes/Alpha.md", "---\nstatus: now\n---\n# Alpha\n"),
        ("Notes/Beta.md", "---\nstatus: now\n---\n# Beta\n"),
        ("Notes/Eta.md", "---\nstatus: later\n---\n# Eta\n"),
        ("Notes/Delta.md", "---\nstatus: done\n---\n# Delta\n"),
    ];
    let mut expected: Option<BasesResultSignature> = None;
    for seed in 0..bases_census_seed_count() {
        let mut rng = SplitMix64(seed);
        let mut order = (0..notes.len()).collect::<Vec<_>>();
        for i in (1..order.len()).rev() {
            let j = rng.below(i + 1);
            order.swap(i, j);
        }
        let (_tmp, session) = make_vault(|p| {
            p.write_file("Queries/ByStatus.base", base).unwrap();
            for index in order {
                let (path, source) = notes[index];
                p.write_file(path, source.as_bytes()).unwrap();
            }
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let handle = session.open_base("Queries/ByStatus.base").unwrap();
        let result = session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap();
        let signature = (
            result_matrix(&result),
            group_signature(&result),
            result.total_count,
            result.shown_count,
        );
        if let Some(expected) = &expected {
            assert_eq!(&signature, expected);
        } else {
            expected = Some(signature);
        }
    }
}

#[test]
fn census_bases_cache_fresh() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Reading.base",
            br#"views:
  - type: table
    name: Reading
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
"#,
        )
        .unwrap();
        p.write_file("Notes/Alpha.md", b"---\nstatus: active\n---\n# Alpha\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Reading.base").unwrap();
    let first = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(first.rows[0].values[1].display, "active");

    session
        .save_text("Notes/Alpha.md", "---\nstatus: done\n---\n# Alpha\n", None)
        .unwrap();
    let fresh = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(fresh.rows[0].values[1].display, "done");

    session.rename_file("Notes/Alpha.md", "Omega.md").unwrap();
    let renamed = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(renamed.rows[0].file_path, "Notes/Omega.md");
    assert_eq!(renamed.rows[0].values[0].display, "Omega.md");
}

#[test]
fn census_bases_fail_loud() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Notes/Alpha.md", b"# Alpha\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let err = session.base_views(9_999).unwrap_err();
    assert!(err.to_string().contains("unknown base handle"), "{err}");

    let saved_query_err = session.open_saved_query("missing").unwrap_err();
    assert!(
        saved_query_err
            .to_string()
            .contains("saved_queries storage"),
        "{saved_query_err}"
    );

    let unsupported = crate::bases::SlateQuery {
        source: crate::bases::QuerySource::Unsupported("future source".to_string()),
        row_source: crate::bases::RowSource::Files,
        filters: None,
        formulas: Vec::new(),
        custom_summaries: Vec::new(),
        group_by: None,
        sort: Vec::new(),
        columns: vec![crate::bases::ColumnSelection {
            id: "file.name".to_string(),
            display_name: None,
        }],
        summaries: Vec::new(),
        limit: None,
        view: crate::bases::ViewSpec::Table {
            fallback_from: None,
        },
    };
    let unsupported_handle = session
        .open_query(&serde_json::to_string(&unsupported).unwrap(), None)
        .unwrap();
    let unsupported_result = session
        .base_execute(unsupported_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        unsupported_result.view_error.as_deref(),
        Some("future source")
    );

    let edit_err = session
        .base_apply_edit(
            unsupported_handle,
            crate::bases::BaseEdit::RenameView {
                view: 0,
                name: "Nope".to_string(),
            },
        )
        .unwrap_err();
    assert!(
        edit_err.to_string().contains("ephemeral query handles"),
        "{edit_err}"
    );

    let conversion_err = session
        .dql_as_base("TABLE file.name\nGROUP BY status\n")
        .unwrap_err();
    assert!(
        conversion_err
            .to_string()
            .contains("DQL conversion is lossy"),
        "{conversion_err}"
    );

    for (source, expected) in [
        ("TABLE upper(file.name)\nFROM \"Notes\"\n", "upper"),
        ("TABLE file.name\nGROUP BY status\n", "GROUP BY"),
        ("TASK\nWHERE line > 0\n", "task field line"),
    ] {
        let err = session.dql_as_base(source).unwrap_err();
        assert!(err.to_string().contains(expected), "{err}");
        let handle = session.open_dql(source, None).unwrap();
        let result = session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap();
        assert!(
            result.view_error.is_some()
                || !result.warnings.is_empty()
                || result
                    .rows
                    .iter()
                    .flat_map(|row| row.values.iter())
                    .any(|value| value.error.is_some()),
            "unsupported DQL fixture should surface loudly: {source:?}"
        );
    }
}

#[test]
fn census_bases_read_only() {
    let (tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Reading.base",
            br#"views:
  - type: table
    name: Reading
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
"#,
        )
        .unwrap();
        p.write_file("Notes/Alpha.md", b"---\nstatus: active\n---\n# Alpha\n")
            .unwrap();
        p.write_file("Notes/Beta.md", b"---\nstatus: done\n---\n# Beta\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = vault_content_snapshot(tmp.path());

    let summaries = session.bases_list().unwrap();
    assert_eq!(summaries.len(), 1);
    let handle = session.open_base("Queries/Reading.base").unwrap();
    session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    session
        .base_export(handle, 0, ExportFormat::Markdown, Some("done".to_string()))
        .unwrap();
    let dql_handle = session
        .open_dql(
            "TABLE WITHOUT ID file.name AS \"Name\"\nFROM \"Notes\"\n",
            None,
        )
        .unwrap();
    session
        .base_execute(dql_handle, 0, None, None, &CancelToken::new())
        .unwrap();

    assert_eq!(vault_content_snapshot(tmp.path()), before);
}

fn result_matrix(result: &BasesResultSet) -> Vec<(String, Vec<String>)> {
    result
        .rows
        .iter()
        .map(|row| {
            (
                row.file_path.clone(),
                row.values
                    .iter()
                    .map(|value| value.display.clone())
                    .collect::<Vec<_>>(),
            )
        })
        .collect()
}

fn group_signature(result: &BasesResultSet) -> Vec<(String, u64, u64)> {
    result
        .groups
        .iter()
        .map(|group| (group.label.clone(), group.row_start, group.row_count))
        .collect()
}

fn vault_content_snapshot(root: &Path) -> Vec<(String, Vec<u8>)> {
    let mut out = Vec::new();
    collect_vault_content(root, root, &mut out);
    out.sort_by(|lhs, rhs| lhs.0.cmp(&rhs.0));
    out
}

fn collect_vault_content(root: &Path, dir: &Path, out: &mut Vec<(String, Vec<u8>)>) {
    let mut entries = std::fs::read_dir(dir)
        .unwrap()
        .map(|entry| entry.unwrap().path())
        .collect::<Vec<PathBuf>>();
    entries.sort();
    for path in entries {
        let relative = path.strip_prefix(root).unwrap();
        if relative.starts_with(".slate") {
            continue;
        }
        if path.is_dir() {
            collect_vault_content(root, &path, out);
        } else {
            out.push((
                relative.to_string_lossy().replace('\\', "/"),
                std::fs::read(&path).unwrap(),
            ));
        }
    }
}
