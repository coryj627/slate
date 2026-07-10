// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! End-to-end corpus gate for genuine Obsidian-authored Bases files.
//!
//! Unlike the parser/serializer corpus tests, this suite copies each raw
//! capture and its original synthetic notes into a real temporary vault, scans
//! through `VaultSession`, and executes every captured view. The raw capture
//! bytes are pinned here so executing the corpus can never normalize or rewrite
//! them unnoticed.

use std::collections::BTreeMap;
use std::fs;
use std::path::{Path, PathBuf};

use serde::Deserialize;
use slate_core::{CancelToken, VaultSession};

const OBSIDIAN_BASIC_RAW: &[u8] = b"views:\n  - type: table\n    name: Table\n    filters:\n      and:\n        - status == \"active\"\n    order:\n      - file.name\n      - category\n      - priority\n      - status\n    sort:\n      - property: priority\n        direction: DESC\n";

const OBSIDIAN_FORMULAS_RAW: &[u8] = b"formulas:\n  weighted_total: score * priority\nviews:\n  - type: table\n    name: Table\n    order:\n      - file.name\n      - formula.weighted_total\n  - type: table\n    name: View\n    filters:\n      and:\n        - status == \"active\"\n";

const OBSIDIAN_BASIC_SHA256: &str =
    "0ae6455a9b4c5a6e39e48aa3291bd80669ee8735254f3e0885b26178d3149fd5";
const OBSIDIAN_FORMULAS_SHA256: &str =
    "8127ab360d98b05fb85eea33b76e93c5ad9f8b25c6efd9255a603ec6f81ccbf8";

#[derive(Debug, Deserialize)]
struct CorpusGolden {
    captures: Vec<CaptureGolden>,
}

#[derive(Debug, Deserialize)]
struct CaptureGolden {
    file: String,
    sha256: String,
    views: Vec<ViewGolden>,
}

#[derive(Debug, Deserialize)]
struct ViewGolden {
    index: u32,
    name: String,
    columns: Vec<String>,
    rows: Vec<RowGolden>,
}

#[derive(Debug, Deserialize)]
struct RowGolden {
    path: String,
    cells: BTreeMap<String, String>,
}

fn fixture_root() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/bases/obsidian")
}

fn pinned_capture(file: &str) -> (&'static [u8], &'static str) {
    match file {
        "obsidian-basic.base" => (OBSIDIAN_BASIC_RAW, OBSIDIAN_BASIC_SHA256),
        "obsidian-formulas.base" => (OBSIDIAN_FORMULAS_RAW, OBSIDIAN_FORMULAS_SHA256),
        other => panic!("golden names an unpinned Obsidian capture: {other}"),
    }
}

fn copy_tree(source: &Path, destination: &Path) {
    fs::create_dir_all(destination).expect("create companion vault destination");
    let entries = fs::read_dir(source).unwrap_or_else(|error| {
        panic!(
            "companion vault fixture must exist at {}: {error}",
            source.display()
        )
    });
    for entry in entries {
        let entry = entry.expect("read companion vault entry");
        let destination_path = destination.join(entry.file_name());
        if entry
            .file_type()
            .expect("read companion entry type")
            .is_dir()
        {
            copy_tree(&entry.path(), &destination_path);
        } else {
            fs::copy(entry.path(), destination_path).expect("copy companion vault file");
        }
    }
}

fn load_golden(root: &Path) -> CorpusGolden {
    let source =
        fs::read_to_string(root.join("expected.json")).expect("read Obsidian E2E expected golden");
    serde_json::from_str(&source).expect("parse Obsidian E2E expected golden")
}

fn assert_capture_bytes(root: &Path, golden: &CorpusGolden) {
    for capture in &golden.captures {
        let (pinned_bytes, pinned_sha256) = pinned_capture(&capture.file);
        assert_eq!(
            capture.sha256, pinned_sha256,
            "{} golden must cite the provenance SHA-256",
            capture.file
        );
        assert_eq!(
            fs::read(root.join(&capture.file)).expect("read copied raw capture"),
            pinned_bytes,
            "{} raw capture bytes changed",
            capture.file
        );
    }
}

fn execute_corpus(root: &Path, golden: &CorpusGolden) {
    let session = VaultSession::from_filesystem(root.to_path_buf()).expect("open fixture vault");
    session
        .scan_initial(&CancelToken::new())
        .expect("scan fixture vault through the filesystem provider");

    for capture in &golden.captures {
        let handle = session
            .open_base(&capture.file)
            .unwrap_or_else(|error| panic!("open {} through VaultSession: {error}", capture.file));
        let actual_views = session
            .base_views(handle)
            .unwrap_or_else(|error| panic!("read {} views: {error}", capture.file));
        assert_eq!(
            actual_views.len(),
            capture.views.len(),
            "every captured view in {} needs a golden",
            capture.file
        );

        for expected_view in &capture.views {
            let actual_view = actual_views
                .get(expected_view.index as usize)
                .unwrap_or_else(|| {
                    panic!(
                        "{} is missing view index {}",
                        capture.file, expected_view.index
                    )
                });
            assert_eq!(actual_view.name, expected_view.name);

            let result = session
                .base_execute(handle, expected_view.index, None, None, &CancelToken::new())
                .unwrap_or_else(|error| {
                    panic!(
                        "execute {} view {}: {error}",
                        capture.file, expected_view.index
                    )
                });
            assert_eq!(
                result.view_error, None,
                "{} view {} must execute, not fall back",
                capture.file, expected_view.index
            );

            let actual_columns = result
                .columns
                .iter()
                .map(|column| column.id.as_str())
                .collect::<Vec<_>>();
            assert_eq!(
                actual_columns, expected_view.columns,
                "{} view {} columns",
                capture.file, expected_view.index
            );

            let actual_paths = result
                .rows
                .iter()
                .map(|row| row.file_path.as_str())
                .collect::<Vec<_>>();
            let expected_paths = expected_view
                .rows
                .iter()
                .map(|row| row.path.as_str())
                .collect::<Vec<_>>();
            assert_eq!(
                actual_paths, expected_paths,
                "{} view {} stable row paths",
                capture.file, expected_view.index
            );
            assert_eq!(result.total_count as usize, expected_view.rows.len());
            assert_eq!(result.shown_count as usize, expected_view.rows.len());

            for (actual_row, expected_row) in result.rows.iter().zip(&expected_view.rows) {
                assert_eq!(
                    expected_row.cells.len(),
                    expected_view.columns.len(),
                    "golden for {} in {} view {} must pin every displayed cell",
                    expected_row.path,
                    capture.file,
                    expected_view.index
                );
                for (column, value) in result.columns.iter().zip(&actual_row.values) {
                    assert_eq!(
                        Some(value.display.as_str()),
                        expected_row.cells.get(&column.id).map(String::as_str),
                        "{} view {} row {} cell {}",
                        capture.file,
                        expected_view.index,
                        expected_row.path,
                        column.id
                    );
                }
            }
        }

        session.close_base(handle);
    }

    session.close().expect("close fixture vault session");
}

#[test]
fn genuine_obsidian_captures_execute_every_view_without_mutating_raw_bytes() {
    let fixtures = fixture_root();
    let temporary_vault = tempfile::tempdir().expect("create temporary fixture vault");

    copy_tree(&fixtures.join("companion-vault"), temporary_vault.path());
    let golden = load_golden(&fixtures);
    for capture in &golden.captures {
        let (pinned_bytes, _) = pinned_capture(&capture.file);
        assert_eq!(
            fs::read(fixtures.join(&capture.file)).expect("read source raw capture"),
            pinned_bytes,
            "{} source capture no longer matches the pinned raw bytes",
            capture.file
        );
        fs::copy(
            fixtures.join(&capture.file),
            temporary_vault.path().join(&capture.file),
        )
        .expect("copy raw capture into temporary vault");
    }

    assert_capture_bytes(temporary_vault.path(), &golden);
    execute_corpus(temporary_vault.path(), &golden);
    assert_capture_bytes(temporary_vault.path(), &golden);

    // Cold reopen proves the golden does not depend on a warm in-memory base
    // handle or query cache from the first session.
    execute_corpus(temporary_vault.path(), &golden);
    assert_capture_bytes(temporary_vault.path(), &golden);

    for capture in &golden.captures {
        let (pinned_bytes, _) = pinned_capture(&capture.file);
        assert_eq!(
            fs::read(fixtures.join(&capture.file)).expect("re-read source raw capture"),
            pinned_bytes,
            "{} source capture changed while the E2E corpus ran",
            capture.file
        );
    }
}

#[test]
fn native_sort_property_treats_operator_punctuation_as_a_literal_identifier() {
    let temporary_vault = tempfile::tempdir().expect("create temporary sort vault");
    fs::create_dir_all(temporary_vault.path().join("Notes")).expect("create Notes directory");
    fs::write(
        temporary_vault.path().join("Hyphenated.base"),
        b"formulas:\n  total: note[\"foo-bar\"] + 10\nviews:\n  - type: table\n    name: Table\n    filters: \"file.inFolder(\\\"Notes\\\")\"\n    order: [file.name, foo-bar, \"project status\", \"true\", \"a+b\", \"dotted.key\", formula.total]\n    sort:\n      - property: foo-bar\n        direction: DESC\n",
    )
    .expect("write native-sort base");
    fs::write(
        temporary_vault.path().join("Notes/a.md"),
        b"---\nfoo-bar: 1\nproject status: \"first\"\ntrue: \"reserved-a\"\n\"a+b\": 7\n\"dotted.key\": \"dot-a\"\n---\n# A\n",
    )
    .expect("write lower-valued note");
    fs::write(
        temporary_vault.path().join("Notes/b.md"),
        b"---\nfoo-bar: 2\nproject status: \"second\"\ntrue: \"reserved-b\"\n\"a+b\": 8\n\"dotted.key\": \"dot-b\"\n---\n# B\n",
    )
    .expect("write higher-valued note");

    let session = VaultSession::from_filesystem(temporary_vault.path().to_path_buf())
        .expect("open native-sort fixture vault");
    session
        .scan_initial(&CancelToken::new())
        .expect("scan native-sort fixture vault");
    let handle = session
        .open_base("Hyphenated.base")
        .expect("open native-sort base");
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .expect("execute native-sort base");

    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        ["Notes/b.md", "Notes/a.md"]
    );
    assert_eq!(result.rows[0].values[1].display, "2");
    assert_eq!(result.rows[1].values[1].display, "1");
    assert_eq!(result.rows[0].values[2].display, "second");
    assert_eq!(result.rows[1].values[2].display, "first");
    assert_eq!(result.rows[0].values[3].display, "reserved-b");
    assert_eq!(result.rows[1].values[3].display, "reserved-a");
    assert_eq!(result.rows[0].values[4].display, "8");
    assert_eq!(result.rows[1].values[4].display, "7");
    assert_eq!(result.rows[0].values[5].display, "dot-b");
    assert_eq!(result.rows[1].values[5].display, "dot-a");
    assert_eq!(result.rows[0].values[6].display, "12");
    assert_eq!(result.rows[1].values[6].display, "11");

    session.close_base(handle);
    session.close().expect("close native-sort fixture vault");
}

#[test]
fn reserved_namespace_typos_fail_loud_in_columns_and_native_sort() {
    let temporary_vault = tempfile::tempdir().expect("create namespace test vault");
    fs::write(
        temporary_vault.path().join("ClosedNamespaces.base"),
        b"views:\n  - type: table\n    name: Columns\n    order: [file.typo, task.typo, this.file.typo]\n  - type: table\n    name: Sort\n    sort:\n      - property: file.typo\n        direction: ASC\n",
    )
    .expect("write closed-namespace base");
    fs::write(
        temporary_vault.path().join("Note.md"),
        b"---\n\"file.typo\": hidden\n\"task.typo\": hidden\n\"this.file.typo\": hidden\n---\n# Note\n",
    )
    .expect("write collision note");

    let session = VaultSession::from_filesystem(temporary_vault.path().to_path_buf())
        .expect("open namespace test vault");
    session
        .scan_initial(&CancelToken::new())
        .expect("scan namespace test vault");
    let handle = session
        .open_base("ClosedNamespaces.base")
        .expect("open namespace test base");

    let columns = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .expect("execute namespace columns view");
    assert_eq!(columns.rows.len(), 2);
    for row in &columns.rows {
        for value in &row.values {
            assert_eq!(value.raw_kind, "error");
            assert!(
                value
                    .error
                    .as_deref()
                    .is_some_and(|error| error.contains("unknown")),
                "reserved namespace typo must be a named cell error: {value:#?}"
            );
        }
    }

    let sorted = session
        .base_execute(handle, 1, None, None, &CancelToken::new())
        .expect("execute namespace sort view");
    assert!(sorted.rows.is_empty());
    assert!(
        sorted
            .view_error
            .as_deref()
            .is_some_and(|error| error.contains("unknown file field")),
        "reserved native-sort typo must fail the view loudly: {sorted:#?}"
    );

    session.close_base(handle);
    session.close().expect("close namespace test vault");
}
