// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests -- Bases handle API.
//!
//! Census scale follows the existing session convention: default runs are
//! quick enough for normal CI; `SLATE_CENSUS_FULL=1` expands the generated
//! permutation corpus for pre-push confirmation evidence.

use super::common::*;
use super::*;
use serde::Deserialize;
use std::collections::BTreeSet;
use std::path::{Path, PathBuf};
use std::sync::Arc;

type BasesResultSignature = (
    Vec<(String, Vec<String>)>,
    Vec<(String, u64, u64)>,
    u64,
    u64,
);

const DQL_CENSUS_NOW_MS: i64 = 1_783_656_000_000;

fn bases_census_seed_count() -> u64 {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        128
    } else {
        32
    }
}

fn bases_cache_census_seed_count() -> u64 {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        64
    } else {
        16
    }
}

const DETERMINISM_BASES: [&[u8]; 4] = [
    br#"views:
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
"#,
    br#"views:
  - type: list
    name: Limited
    filters: "file.inFolder(\"Notes\")"
    limit: 3
    order:
      - file.path
      - status
    slate:
      sort:
        - expr: status
          direction: desc
        - expr: file.name
          direction: asc
"#,
    br#"views:
  - type: table
    name: Filtered
    filters:
      or:
        - "status == \"now\""
        - "status == \"done\""
    order:
      - status
      - file.name
"#,
    br#"views:
  - type: plugin-grid
    name: Fallback
    filters: "file.inFolder(\"Notes\")"
    groupBy:
      property: status
      direction: DESC
    order:
      - file.name
      - status
"#,
];

const SESSION_BASE_CORPUS: [(&str, &[u8]); 10] = [
    (
        "brief_example.base",
        include_bytes!("../../../tests/fixtures/bases/brief_example.base"),
    ),
    (
        "comments_key_order.base",
        include_bytes!("../../../tests/fixtures/bases/comments_key_order.base"),
    ),
    (
        "edit_adversarial.base",
        include_bytes!("../../../tests/fixtures/bases/edit_adversarial.base"),
    ),
    (
        "every_documented_key.base",
        include_bytes!("../../../tests/fixtures/bases/every_documented_key.base"),
    ),
    (
        "filter_only.base",
        include_bytes!("../../../tests/fixtures/bases/filter_only.base"),
    ),
    (
        "plugin_view_state.base",
        include_bytes!("../../../tests/fixtures/bases/plugin_view_state.base"),
    ),
    (
        "quoted_unknown_keys.base",
        include_bytes!("../../../tests/fixtures/bases/quoted_unknown_keys.base"),
    ),
    (
        "tasks_and_comments.base",
        include_bytes!("../../../tests/fixtures/bases/tasks_and_comments.base"),
    ),
    (
        "obsidian/obsidian-basic.base",
        include_bytes!("../../../tests/fixtures/bases/obsidian/obsidian-basic.base"),
    ),
    (
        "obsidian/obsidian-formulas.base",
        include_bytes!("../../../tests/fixtures/bases/obsidian/obsidian-formulas.base"),
    ),
];

const SESSION_DQL_CORPORA: [(&str, &str); 3] = [
    (
        "closure",
        include_str!("../../../tests/fixtures/dql/closure_corpus.json"),
    ),
    (
        "field",
        include_str!("../../../tests/fixtures/dql/field_corpus.json"),
    ),
    (
        "function",
        include_str!("../../../tests/fixtures/dql/function_corpus.json"),
    ),
];

#[derive(Debug, Clone, Copy, PartialEq, Eq, Deserialize)]
#[serde(rename_all = "snake_case")]
enum SessionDqlDisposition {
    Supported,
    Unsupported,
    RuntimeFailLoud,
}

#[derive(Debug, Clone, Deserialize)]
struct SessionDqlCase {
    name: String,
    disposition: SessionDqlDisposition,
    source: String,
    #[serde(default)]
    this_path: Option<String>,
    #[serde(default)]
    warning_message: Option<String>,
    #[serde(default)]
    error_contains: Option<String>,
}

/// The N2 wave-gate's blanket "view_error" shorthand is refined by the
/// earlier N1 engine contract: membership-affecting positions fail the view,
/// while column/summary errors retain membership and render named markers.
#[derive(Debug, Clone, Copy)]
enum FailLoudMutation {
    Source,
    Filter,
    FormulaInert,
    Sort,
    Group,
    Columns,
    Summary,
}

impl FailLoudMutation {
    const ALL: [Self; 7] = [
        Self::Source,
        Self::Filter,
        Self::FormulaInert,
        Self::Sort,
        Self::Group,
        Self::Columns,
        Self::Summary,
    ];

    fn label(self) -> &'static str {
        match self {
            Self::Source => "source",
            Self::Filter => "filter",
            Self::FormulaInert => "formula",
            Self::Sort => "sort",
            Self::Group => "group",
            Self::Columns => "columns",
            Self::Summary => "summary",
        }
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

struct UnlinkingTestProvider {
    inner: FsVaultProvider,
    root: PathBuf,
}

impl UnlinkingTestProvider {
    fn new(root: PathBuf) -> Self {
        Self {
            inner: FsVaultProvider::new(root.clone()),
            root,
        }
    }
}

impl crate::VaultProvider for UnlinkingTestProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }

    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }

    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }

    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        let relative_path = Path::new(relative);
        if relative_path.is_absolute()
            || relative_path
                .components()
                .any(|component| !matches!(component, std::path::Component::Normal(_)))
        {
            return Err(VaultError::InvalidPath {
                path: relative.to_string(),
                reason: "test unlink path must be clean and vault-relative".to_string(),
            });
        }
        let target = self.root.join(relative_path);
        let metadata = target.symlink_metadata().map_err(VaultError::Io)?;
        if metadata.is_dir() {
            std::fs::remove_dir_all(target).map_err(VaultError::Io)
        } else {
            std::fs::remove_file(target).map_err(VaultError::Io)
        }
    }

    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }

    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }

    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        self.inner.stat(relative)
    }

    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

struct CountingWriteProvider {
    inner: FsVaultProvider,
    writes: Arc<std::sync::atomic::AtomicUsize>,
}

impl crate::VaultProvider for CountingWriteProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }

    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }

    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.writes
            .fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        self.inner.write_file(relative, contents)
    }

    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(relative)
    }

    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }

    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }

    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        self.inner.stat(relative)
    }

    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

fn make_unlinking_vault(setup: impl FnOnce(&FsVaultProvider)) -> (tempfile::TempDir, VaultSession) {
    let tmp = tempfile::tempdir().expect("create unlinking test vault");
    let root = tmp.path().to_path_buf();
    let setup_provider = FsVaultProvider::new(root.clone());
    setup(&setup_provider);
    let session = VaultSession::open(
        Arc::new(UnlinkingTestProvider::new(root.clone())),
        SessionConfig::new(root.join(".slate")),
    )
    .expect("open unlinking test vault");
    (tmp, session)
}

fn make_counting_write_vault(
    setup: impl FnOnce(&FsVaultProvider),
) -> (
    tempfile::TempDir,
    VaultSession,
    Arc<std::sync::atomic::AtomicUsize>,
) {
    let tmp = tempfile::tempdir().expect("create counting-write test vault");
    let root = tmp.path().to_path_buf();
    let setup_provider = FsVaultProvider::new(root.clone());
    setup(&setup_provider);
    let writes = Arc::new(std::sync::atomic::AtomicUsize::new(0));
    let session = VaultSession::open(
        Arc::new(CountingWriteProvider {
            inner: FsVaultProvider::new(root.clone()),
            writes: writes.clone(),
        }),
        SessionConfig::new(root.join(".slate")),
    )
    .expect("open counting-write test vault");
    (tmp, session, writes)
}

fn session_dql_cases() -> Vec<SessionDqlCase> {
    SESSION_DQL_CORPORA
        .iter()
        .flat_map(|(corpus, source)| {
            serde_json::from_str::<Vec<SessionDqlCase>>(source)
                .unwrap_or_else(|error| panic!("parse checked-in DQL {corpus} corpus: {error}"))
        })
        .collect()
}

fn make_dql_corpus_vault() -> (tempfile::TempDir, VaultSession) {
    let (tmp, session) = make_vault(|provider| {
        provider
            .write_file(
                "Hub.md",
                "---\ntags: [project/subtag]\nstatus: active\n---\n[[Target]]\n- [ ] Open task 📅 2099-01-02 ⏳ 2099-01-03\n- [x] Done task 📅 2099-01-02 ⏳ 2099-01-03\n"
                    .as_bytes(),
            )
            .unwrap();
        provider
            .write_file(
                "Target.md",
                "- [/] Waiting task 📅 2099-01-04 ⏳ 2099-01-05\n".as_bytes(),
            )
            .unwrap();
        provider.write_file("Other.md", b"[[Hub]]\n").unwrap();
        provider
            .write_file(
                "123.md",
                b"---\ntags: [reading, a/b]\naliases: [One,Two]\nstatus: reading\nscheduled: 2026-07-10\npages: 120\nminutesPerPage: 3\nrating: 4.5\nscores: [1,2]\nvalue: x\nindex: 10\nkey: name\n---\n",
            )
            .unwrap();
        provider
            .write_file("Notes/Hub.md", b"[[Target]]\n")
            .unwrap();
        provider
            .write_file("Notes/Target.md", b"# Target\n")
            .unwrap();
        for (path, source) in [
            (
                "Notes/Alpha.md",
                include_bytes!(
                    "../../../tests/fixtures/bases/obsidian/companion-vault/Notes/Alpha.md"
                )
                .as_slice(),
            ),
            (
                "Notes/Beta.md",
                include_bytes!(
                    "../../../tests/fixtures/bases/obsidian/companion-vault/Notes/Beta.md"
                )
                .as_slice(),
            ),
            (
                "Notes/Gamma.md",
                include_bytes!(
                    "../../../tests/fixtures/bases/obsidian/companion-vault/Notes/Gamma.md"
                )
                .as_slice(),
            ),
        ] {
            provider.write_file(path, source).unwrap();
        }
        provider
            .write_file(
                "Notes/View.base",
                b"views:\n  - type: table\n    name: View\n",
            )
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

fn unsupported_census_expr(reason: &str) -> crate::bases::expr::Expr {
    crate::bases::expr::Expr {
        span: crate::bases::expr::Span { start: 0, end: 0 },
        kind: crate::bases::expr::ExprKind::Unsupported {
            raw: reason.to_string(),
            reason: reason.to_string(),
        },
    }
}

fn mutate_query_for_fail_loud(
    query: &crate::bases::SlateQuery,
    mutation: FailLoudMutation,
) -> (crate::bases::SlateQuery, String) {
    let mut mutated = query.clone();
    let reason = format!("N2 census unsupported at {}", mutation.label());
    match mutation {
        FailLoudMutation::Source => {
            mutated.source = crate::bases::QuerySource::Unsupported(reason.clone());
        }
        FailLoudMutation::Filter => {
            mutated.filters = Some(crate::bases::FilterNode::Stmt(unsupported_census_expr(
                &reason,
            )));
        }
        FailLoudMutation::FormulaInert => {
            mutated
                .formulas
                .push(("__n2_inert".to_string(), unsupported_census_expr(&reason)));
        }
        FailLoudMutation::Sort => {
            mutated.sort.push(crate::bases::SortKey {
                expr: unsupported_census_expr(&reason),
                ascending: true,
            });
        }
        FailLoudMutation::Group => {
            mutated
                .formulas
                .push(("__n2_group".to_string(), unsupported_census_expr(&reason)));
            mutated.group_by = Some(crate::bases::GroupBy {
                property: crate::bases::expr::PropertyRef::Formula("__n2_group".to_string()),
                ascending: true,
            });
        }
        FailLoudMutation::Columns => {
            mutated
                .formulas
                .push(("__n2_columns".to_string(), unsupported_census_expr(&reason)));
            mutated.columns.push(crate::bases::ColumnSelection {
                id: "formula.__n2_columns".to_string(),
                display_name: Some("Unsupported census column".to_string()),
            });
        }
        FailLoudMutation::Summary => {
            let column = mutated
                .columns
                .first()
                .map(|column| column.id.clone())
                .unwrap_or_else(|| {
                    mutated.columns.push(crate::bases::ColumnSelection {
                        id: "file.path".to_string(),
                        display_name: None,
                    });
                    "file.path".to_string()
                });
            mutated
                .custom_summaries
                .push(("__n2_summary".to_string(), unsupported_census_expr(&reason)));
            mutated.summaries.push((
                column,
                crate::bases::SummaryRef::Custom("__n2_summary".to_string()),
            ));
        }
    }
    (mutated, reason)
}

fn result_membership(result: &BasesResultSet) -> Vec<(String, Option<u64>)> {
    result
        .rows
        .iter()
        .map(|row| (row.file_path.clone(), row.task_ordinal))
        .collect()
}

fn assert_named_dql_failure(result: &BasesResultSet, expected: &str, context: &str) {
    if let Some(error) = result.view_error.as_deref() {
        assert!(
            error.contains(expected),
            "{context}: expected view error containing {expected:?}, got {error:?}"
        );
        assert!(
            result.rows.is_empty(),
            "{context}: filter/source failure leaked partial rows"
        );
        return;
    }

    assert!(
        !result.rows.is_empty()
            && result.rows.iter().all(|row| {
                row.values.iter().any(|value| {
                    value
                        .error
                        .as_deref()
                        .is_some_and(|error| error.contains(expected))
                })
            }),
        "{context}: expected a named error marker containing {expected:?}, got {result:?}"
    );
}

fn checked_in_base_fixture_names() -> BTreeSet<String> {
    fn collect(root: &Path, dir: &Path, names: &mut BTreeSet<String>) {
        for entry in std::fs::read_dir(dir).unwrap() {
            let path = entry.unwrap().path();
            if path.is_dir() {
                collect(root, &path, names);
            } else if path.extension().and_then(|extension| extension.to_str()) == Some("base") {
                names.insert(
                    path.strip_prefix(root)
                        .unwrap()
                        .to_string_lossy()
                        .replace('\\', "/"),
                );
            }
        }
    }

    let root = Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/bases");
    let mut names = BTreeSet::new();
    collect(&root, &root, &mut names);
    names
}

fn notes_query_json() -> String {
    let (base, warnings) = crate::bases::parse_base(
        r#"views:
  - type: table
    name: Reading
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
"#,
    );
    assert!(warnings.is_empty(), "{warnings:?}");
    serde_json::to_string(&crate::bases::view_query(&base, 0)).unwrap()
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
    limit: 2
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
    assert_eq!(filtered.unfiltered_shown_count, 2);
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
    assert_eq!(accent_filtered.unfiltered_shown_count, 2);
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

    let unfiltered_csv = session
        .base_export(handle, 0, ExportFormat::Csv, None)
        .unwrap();
    assert_eq!(
        unfiltered_csv,
        "file.name,status\r\nAlpha.md,active\r\nBeta.md,done\r\n"
    );
}

#[test]
fn transient_sort_uses_typed_order_for_table_list_and_export() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Typed.base",
            br#"views:
  - type: table
    name: Table
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score, due]
  - type: list
    name: List
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score, due]
"#,
        )
        .unwrap();
        p.write_file(
            "Notes/Aardvark.md",
            b"---\nscore: 10\ndue: 2026-03-01\n---\n# Aardvark\n",
        )
        .unwrap();
        p.write_file(
            "Notes/Alpha.md",
            b"---\nscore: 10\ndue: 2026-03-01\n---\n# Alpha\n",
        )
        .unwrap();
        p.write_file(
            "Notes/Beta.md",
            b"---\nscore: 2\ndue: 2026-02-01\n---\n# Beta\n",
        )
        .unwrap();
        p.write_file("Notes/Null.md", b"# Null\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session.open_base("Queries/Typed.base").unwrap();

    let natural = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        natural
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        [
            "Notes/Aardvark.md",
            "Notes/Alpha.md",
            "Notes/Beta.md",
            "Notes/Null.md",
        ]
    );

    session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap();
    let numeric = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    let numeric_paths = numeric
        .rows
        .iter()
        .map(|row| row.file_path.as_str())
        .collect::<Vec<_>>();
    assert_eq!(
        numeric_paths,
        [
            "Notes/Beta.md",
            "Notes/Aardvark.md",
            "Notes/Alpha.md",
            "Notes/Null.md",
        ]
    );
    let csv = session
        .base_export(handle, 0, ExportFormat::Csv, None)
        .unwrap();
    assert_eq!(
        csv.lines()
            .skip(1)
            .map(|line| line.split(',').next().unwrap_or_default())
            .collect::<Vec<_>>(),
        ["Beta.md", "Aardvark.md", "Alpha.md", "Null.md"]
    );

    session
        .base_set_transient_sort(handle, 1, Some("due".to_string()), false)
        .unwrap();
    let list = session
        .base_execute(handle, 1, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        list.rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        [
            "Notes/Aardvark.md",
            "Notes/Alpha.md",
            "Notes/Beta.md",
            "Notes/Null.md",
        ]
    );
    let list_csv = session
        .base_export(handle, 1, ExportFormat::Csv, None)
        .unwrap();
    assert_eq!(
        list_csv
            .lines()
            .skip(1)
            .map(|line| line.split(',').next().unwrap_or_default())
            .collect::<Vec<_>>(),
        ["Aardvark.md", "Alpha.md", "Beta.md", "Null.md"]
    );
}

#[test]
fn transient_sort_validates_and_clears_on_reset_save_and_close() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Sort.base",
            br#"views:
  - type: table
    name: Sort
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score]
"#,
        )
        .unwrap();
        p.write_file("Notes/Ten.md", b"---\nscore: 10\n---\n# Ten\n")
            .unwrap();
        p.write_file("Notes/Two.md", b"---\nscore: 2\n---\n# Two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session.open_base("Queries/Sort.base").unwrap();

    let invalid_view = session
        .base_set_transient_sort(handle, 9, Some("score".to_string()), true)
        .unwrap_err();
    assert!(invalid_view.to_string().contains("out of range"));
    let invalid_column = session
        .base_set_transient_sort(handle, 0, Some("missing".to_string()), true)
        .unwrap_err();
    assert!(invalid_column.to_string().contains("not displayed"));

    session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap();
    let sorted = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(sorted.rows[0].file_path, "Notes/Two.md");

    session
        .base_set_transient_sort(handle, 0, None, true)
        .unwrap();
    let reset = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(reset.rows[0].file_path, "Notes/Ten.md");

    session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap();
    session
        .base_apply_edit(
            handle,
            crate::bases::BaseEdit::SetSlateState {
                view: 0,
                yaml: Some(
                    "slate:\n  sort:\n    - property: score\n      direction: DESC".to_string(),
                ),
            },
        )
        .unwrap();
    let persisted = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(persisted.rows[0].file_path, "Notes/Ten.md");

    session.close_base(handle);
    let closed = session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap_err();
    assert!(closed.to_string().contains("unknown base handle"));
}

#[test]
fn transient_sort_clears_when_removing_sorted_view_reuses_view_zero() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/RemoveView.base",
            br#"views:
  - type: table
    name: Sorted view
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score]
  - type: table
    name: New view zero
    filters: "file.inFolder(\"Notes\")"
    order: [file.name]
"#,
        )
        .unwrap();
        p.write_file("Notes/Ten.md", b"---\nscore: 10\n---\n# Ten\n")
            .unwrap();
        p.write_file("Notes/Two.md", b"---\nscore: 2\n---\n# Two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session.open_base("Queries/RemoveView.base").unwrap();

    session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap();
    assert_eq!(
        session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap()
            .rows[0]
            .file_path,
        "Notes/Two.md"
    );

    session
        .base_apply_edit(handle, crate::bases::BaseEdit::RemoveView { view: 0 })
        .unwrap();

    assert_eq!(session.base_views(handle).unwrap()[0].name, "New view zero");
    assert_eq!(
        session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap()
            .rows[0]
            .file_path,
        "Notes/Ten.md",
        "the removed view's numeric-index sort must not leak into the replacement view 0"
    );
}

#[test]
fn transient_sort_clears_when_saved_order_hides_sorted_column() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/HiddenColumn.base",
            br#"views:
  - type: table
    name: Columns
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score]
"#,
        )
        .unwrap();
        p.write_file("Notes/Ten.md", b"---\nscore: 10\n---\n# Ten\n")
            .unwrap();
        p.write_file("Notes/Two.md", b"---\nscore: 2\n---\n# Two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session.open_base("Queries/HiddenColumn.base").unwrap();

    session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap();
    session
        .base_apply_edit(
            handle,
            crate::bases::BaseEdit::SetViewKey {
                view: 0,
                key: "order".to_string(),
                value: "[file.name]".to_string(),
            },
        )
        .unwrap();

    assert_eq!(
        session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap()
            .rows[0]
            .file_path,
        "Notes/Ten.md",
        "a transient key for a now-hidden column must not keep sorting the view"
    );
}

fn assert_structural_edit_clears_transient_sort(edit: crate::bases::BaseEdit) {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Structural.base",
            br#"views:
  - type: table
    name: Structural
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score]
"#,
        )
        .unwrap();
        p.write_file("Notes/Ten.md", b"---\nscore: 10\n---\n# Ten\n")
            .unwrap();
        p.write_file("Notes/Two.md", b"---\nscore: 2\n---\n# Two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session.open_base("Queries/Structural.base").unwrap();
    session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap();

    session.base_apply_edit(handle, edit.clone()).unwrap();

    assert_eq!(
        session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap()
            .rows[0]
            .file_path,
        "Notes/Ten.md",
        "structural edit must clear the transient sort: {edit:?}"
    );
}

#[test]
fn transient_sort_clears_when_a_view_is_added() {
    assert_structural_edit_clears_transient_sort(crate::bases::BaseEdit::AddView {
        yaml: "type: table\nname: Added\norder: [file.name]".to_string(),
    });
}

#[test]
fn transient_sort_clears_when_the_same_view_type_changes() {
    assert_structural_edit_clears_transient_sort(crate::bases::BaseEdit::SetViewKey {
        view: 0,
        key: "type".to_string(),
        value: "list".to_string(),
    });
}

#[test]
fn transient_sort_clears_when_the_same_view_source_changes() {
    assert_structural_edit_clears_transient_sort(crate::bases::BaseEdit::SetViewKey {
        view: 0,
        key: "source".to_string(),
        value: "files".to_string(),
    });
}

#[test]
fn transient_sort_clears_when_the_same_view_slate_state_changes() {
    assert_structural_edit_clears_transient_sort(crate::bases::BaseEdit::SetSlateState {
        view: 0,
        yaml: Some("slate:\n  density: compact".to_string()),
    });
}

#[test]
fn transient_sort_clears_when_formula_is_removed_and_readded() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/FormulaSort.base",
            br#"formulas:
  rank: score
views:
  - type: table
    name: Formula sort
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, formula.rank]
"#,
        )
        .unwrap();
        p.write_file("Notes/Ten.md", b"---\nscore: 10\n---\n# Ten\n")
            .unwrap();
        p.write_file("Notes/Two.md", b"---\nscore: 2\n---\n# Two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session.open_base("Queries/FormulaSort.base").unwrap();

    session
        .base_set_transient_sort(handle, 0, Some("formula.rank".to_string()), true)
        .unwrap();
    session
        .base_apply_edits(
            handle,
            vec![
                crate::bases::BaseEdit::RemoveFormula {
                    name: "rank".to_string(),
                },
                crate::bases::BaseEdit::SetFormula {
                    name: "rank".to_string(),
                    expression: "score".to_string(),
                },
            ],
        )
        .unwrap();

    assert_eq!(
        session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap()
            .rows[0]
            .file_path,
        "Notes/Ten.md"
    );
}

#[test]
fn transient_sort_survives_unrelated_scalar_and_filter_edits() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/PreserveSort.base",
            br#"views:
  - type: table
    name: Original
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score]
"#,
        )
        .unwrap();
        p.write_file("Notes/Ten.md", b"---\nscore: 10\n---\n# Ten\n")
            .unwrap();
        p.write_file("Notes/Two.md", b"---\nscore: 2\n---\n# Two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session.open_base("Queries/PreserveSort.base").unwrap();

    session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap();
    session
        .base_apply_edits(
            handle,
            vec![
                crate::bases::BaseEdit::RenameView {
                    view: 0,
                    name: "Renamed".to_string(),
                },
                crate::bases::BaseEdit::SetViewFilters {
                    view: 0,
                    yaml: "filters: 'file.inFolder(\"Notes\")'".to_string(),
                },
                crate::bases::BaseEdit::SetTopLevelFilters {
                    yaml: "filters: 'score > 0'".to_string(),
                },
            ],
        )
        .unwrap();

    assert_eq!(session.base_views(handle).unwrap()[0].name, "Renamed");
    assert_eq!(
        session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap()
            .rows[0]
            .file_path,
        "Notes/Two.md"
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
        vec!["Alpha", "Beta"]
    );
}

#[test]
fn dql_date_shorthand_session_cache_does_not_cross_new_york_midnight() {
    const CHILD: &str = "SLATE_DQL_SESSION_CACHE_TZ_CHILD";
    if std::env::var(CHILD).as_deref() != Ok("1") {
        let test_name = std::thread::current()
            .name()
            .expect("session test thread has a name")
            .to_string();
        let status = std::process::Command::new(
            std::env::current_exe().expect("locate Slate core test binary"),
        )
        .arg(test_name)
        .arg("--exact")
        .arg("--nocapture")
        .env("TZ", "America/New_York")
        .env(CHILD, "1")
        .status()
        .expect("run DQL session-cache test in America/New_York");
        assert!(status.success(), "DQL session-cache timezone child failed");
        return;
    }

    // 2026-07-10T04:00:00Z is local midnight in America/New_York.
    const LOCAL_MIDNIGHT_MS: i64 = 1_783_656_000_000;
    let before_midnight_ms = LOCAL_MIDNIGHT_MS - 1;
    let after_midnight_ms = LOCAL_MIDNIGHT_MS + 1;
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("Notes/Alpha.md", b"# Alpha\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let handle = session
        .open_dql(
            "TABLE WITHOUT ID string(date(today)) AS \"Day\"\nWHERE file.path = \"Notes/Alpha.md\"\n",
            None,
        )
        .unwrap();

    let before = session
        .base_execute_at(
            handle,
            0,
            None,
            None,
            before_midnight_ms,
            &CancelToken::new(),
        )
        .unwrap();
    let after = session
        .base_execute_at(
            handle,
            0,
            None,
            None,
            after_midnight_ms,
            &CancelToken::new(),
        )
        .unwrap();

    assert_eq!(before.rows[0].values[0].display, "July 09, 2026");
    assert_eq!(after.rows[0].values[0].display, "July 10, 2026");
    assert_eq!(before.executed_at_ms, before_midnight_ms);
    assert_eq!(after.executed_at_ms, after_midnight_ms);
}

#[test]
fn dql_outgoing_executes_explicit_and_dynamic_source_membership() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Hub.md", b"[[Target]]\n").unwrap();
        p.write_file("Target.md", b"# Target\n").unwrap();
        p.write_file("Other.md", b"# Other\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let cases = [
        (
            include_str!("../../../tests/fixtures/dql/outgoing.dql"),
            None,
        ),
        ("LIST\nFROM outgoing([[]])\n", Some("Hub.md".to_string())),
    ];
    for (source, this_path) in cases {
        let handle = session.open_dql(source, this_path).unwrap();
        let result = session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap();
        assert_eq!(
            result
                .rows
                .iter()
                .map(|row| row.file_path.as_str())
                .collect::<Vec<_>>(),
            ["Target.md"],
            "source was {source:?}"
        );
        assert_eq!(result.view_error, None);
    }
}

#[test]
fn dql_outgoing_embed_membership_survives_conversion_save_and_reopen() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Hub.md", b"![[Target]]\n").unwrap();
        p.write_file("Target.md", b"# Target\n").unwrap();
        p.write_file("Other.md", b"# Other\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let source = "LIST WITHOUT ID file.path\nFROM outgoing([[Hub]])\n";
    let live_handle = session.open_dql(source, None).unwrap();
    let live = session
        .base_execute(live_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        live.rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        ["Target.md"]
    );

    let converted = session.dql_as_base(source).unwrap();
    session
        .save_text("Outgoing.base", &converted, None)
        .unwrap();
    let saved_handle = session.open_base("Outgoing.base").unwrap();
    let saved = session
        .base_execute(saved_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        saved
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        ["Target.md"]
    );
}

#[test]
fn dql_regex_and_trunc_survive_base_conversion() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Note.md", b"# Note\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let converted = session
        .dql_as_base(include_str!("../../../tests/fixtures/dql/functions.dql"))
        .unwrap();
    let handle = session.open_base_inline(&converted, None).unwrap();
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();

    assert_eq!(result.view_error, None);
    assert_eq!(
        result.rows[0]
            .values
            .iter()
            .map(|value| value.display.as_str())
            .collect::<Vec<_>>(),
        ["true", "-1"]
    );
}

#[test]
fn base_view_query_json_returns_the_view_ast_for_builder_loading() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Reading.base",
            br#"filters: "file.inFolder(\"Projects\")"
views:
  - type: table
    name: Reading
    filters:
      and:
        - "status == \"active\""
        - "priority >= 2"
    order:
      - file.name
      - status
"#,
        )
        .unwrap();
        p.write_file(
            "Projects/Alpha.md",
            br#"---
status: active
priority: 3
---
# Alpha
"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Reading.base").unwrap();
    let json = session.base_view_query_json(handle, 0).unwrap();
    let query: crate::bases::SlateQuery = serde_json::from_str(&json).unwrap();

    assert_eq!(query.source, crate::bases::QuerySource::All);
    assert_eq!(query.row_source, crate::bases::RowSource::Files);
    assert_eq!(
        query
            .columns
            .iter()
            .map(|c| c.id.as_str())
            .collect::<Vec<_>>(),
        ["file.name", "status"]
    );
    let Some(crate::bases::FilterNode::And(nodes)) = query.filters else {
        panic!("expected base-wide and view filters to compose into top-level AND");
    };
    assert_eq!(
        nodes.len(),
        2,
        "builder loading must see both base-wide and active-view filters"
    );

    let bad_view = session.base_view_query_json(handle, 1).unwrap_err();
    assert!(
        bad_view.to_string().contains("base view 1 is out of range"),
        "{bad_view}"
    );
}

#[test]
fn base_view_edit_query_json_returns_only_active_view_filters() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Reading.base",
            br#"filters: "file.inFolder(\"Projects\")"
views:
  - type: table
    name: Reading
    filters:
      and:
        - "status == \"active\""
        - "priority >= 2"
    order:
      - file.name
      - status
"#,
        )
        .unwrap();
        p.write_file(
            "Projects/Alpha.md",
            br#"---
status: active
priority: 3
---
# Alpha
"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Reading.base").unwrap();
    let json = session.base_view_edit_query_json(handle, 0).unwrap();
    let query: crate::bases::SlateQuery = serde_json::from_str(&json).unwrap();

    assert_eq!(query.source, crate::bases::QuerySource::All);
    let Some(crate::bases::FilterNode::And(nodes)) = query.filters else {
        panic!("expected view filters to remain an editable top-level AND");
    };
    assert_eq!(
        nodes.len(),
        2,
        "edit loading must exclude the base-wide filter so save-to-view does not duplicate it"
    );

    let bad_view = session.base_view_edit_query_json(handle, 1).unwrap_err();
    assert!(
        bad_view.to_string().contains("base view 1 is out of range"),
        "{bad_view}"
    );
}

#[test]
fn list_tags_returns_distinct_indexed_tags_for_builder_inventory() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Projects/Alpha.md",
            br#"---
tags: [Project, Shared]
---
# Alpha
#Inline
"#,
        )
        .unwrap();
        p.write_file("Projects/Beta.md", b"# Beta\n#shared #area/sub\n")
            .unwrap();
        p.write_file("Scratch.md", b"```\n#ignored\n```\n%% #ignored-too %%\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    assert_eq!(
        session.list_tags().unwrap(),
        vec!["area/sub", "inline", "project", "shared"]
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
fn base_apply_edits_rejects_stale_open_handle_without_clobbering_external_bytes() {
    let (tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Conflict.base",
            br#"views:
  - type: table
    name: Original
    order: [file.name]
"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Conflict.base").unwrap();
    session
        .base_set_transient_sort(handle, 0, Some("file.name".to_string()), false)
        .unwrap();
    let view_before = session.base_views(handle).unwrap();
    let query_before = session.base_view_edit_query_json(handle, 0).unwrap();
    let external = b"views:\n  - type: table\n    name: External\n    order: [file.path]\n";
    std::fs::write(tmp.path().join("Queries/Conflict.base"), external).unwrap();

    let error = session
        .base_apply_edits(
            handle,
            vec![crate::bases::BaseEdit::SetViewKey {
                view: 0,
                key: "order".to_string(),
                value: "[file.path]".to_string(),
            }],
        )
        .unwrap_err();

    assert!(
        matches!(error, VaultError::WriteConflict { .. }),
        "{error:?}"
    );
    assert_eq!(
        std::fs::read(tmp.path().join("Queries/Conflict.base")).unwrap(),
        external
    );
    assert_eq!(session.base_views(handle).unwrap(), view_before);
    assert_eq!(
        session.base_view_edit_query_json(handle, 0).unwrap(),
        query_before
    );
    assert!(
        session
            .bases
            .lock()
            .expect("base registry mutex")
            .get(&handle)
            .and_then(|state| state.transient_sort.as_ref())
            .is_some(),
        "a failed stale save must not clear ephemeral handle state"
    );
}

#[test]
fn base_apply_edits_refreshes_expected_hash_after_each_successful_save() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Sequential.base",
            br#"views:
  - type: table
    name: Original
    order: [file.name]
"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Sequential.base").unwrap();
    session
        .base_apply_edit(
            handle,
            crate::bases::BaseEdit::RenameView {
                view: 0,
                name: "First".to_string(),
            },
        )
        .unwrap();
    session
        .base_apply_edit(
            handle,
            crate::bases::BaseEdit::RenameView {
                view: 0,
                name: "Second".to_string(),
            },
        )
        .unwrap();

    assert_eq!(session.base_views(handle).unwrap()[0].name, "Second");
    assert!(
        session
            .read_text("Queries/Sequential.base")
            .unwrap()
            .contains("name: Second")
    );
}

#[test]
fn base_apply_edits_rejects_later_invalid_edit_without_mutating_session_or_disk() {
    let (tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Atomic.base",
            br#"views:
  - type: table
    name: Original
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score]
"#,
        )
        .unwrap();
        p.write_file("Notes/Ten.md", b"---\nscore: 10\n---\n# Ten\n")
            .unwrap();
        p.write_file("Notes/Two.md", b"---\nscore: 2\n---\n# Two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Atomic.base").unwrap();
    let disk_before = std::fs::read(tmp.path().join("Queries/Atomic.base")).unwrap();
    let views_before = session.base_views(handle).unwrap();
    let query_before = session.base_view_edit_query_json(handle, 0).unwrap();
    let result_before = result_matrix(
        &session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap(),
    );
    let indexed_before: String = session
        .conn
        .lock()
        .expect("session connection mutex")
        .query_row(
            "SELECT bf.parsed_query_json
             FROM bases_files bf
             JOIN files f ON f.id = bf.file_id
             WHERE f.path = ?1",
            ["Queries/Atomic.base"],
            |row| row.get(0),
        )
        .unwrap();
    let generation_before = session.bases_generation();
    let oplog_before = session.read_oplog("Queries/Atomic.base").unwrap().len();

    let error = session
        .base_apply_edits(
            handle,
            vec![
                crate::bases::BaseEdit::RenameView {
                    view: 0,
                    name: "Must not persist".to_string(),
                },
                crate::bases::BaseEdit::RenameView {
                    view: 99,
                    name: "Invalid".to_string(),
                },
            ],
        )
        .unwrap_err();
    assert!(error.to_string().contains("base edit rejected"), "{error}");

    assert_eq!(
        std::fs::read(tmp.path().join("Queries/Atomic.base")).unwrap(),
        disk_before
    );
    assert_eq!(session.base_views(handle).unwrap(), views_before);
    assert_eq!(
        session.base_view_edit_query_json(handle, 0).unwrap(),
        query_before
    );
    assert_eq!(
        result_matrix(
            &session
                .base_execute(handle, 0, None, None, &CancelToken::new())
                .unwrap()
        ),
        result_before
    );
    let indexed_after: String = session
        .conn
        .lock()
        .expect("session connection mutex")
        .query_row(
            "SELECT bf.parsed_query_json
             FROM bases_files bf
             JOIN files f ON f.id = bf.file_id
             WHERE f.path = ?1",
            ["Queries/Atomic.base"],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(indexed_after, indexed_before);
    assert_eq!(session.bases_generation(), generation_before);
    assert_eq!(
        session.read_oplog("Queries/Atomic.base").unwrap().len(),
        oplog_before
    );

    let fresh = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    fresh.scan_initial(&CancelToken::new()).unwrap();
    let fresh_handle = fresh.open_base("Queries/Atomic.base").unwrap();
    assert_eq!(fresh.base_views(fresh_handle).unwrap(), views_before);
    assert_eq!(
        fresh.base_view_edit_query_json(fresh_handle, 0).unwrap(),
        query_before
    );
    assert_eq!(
        result_matrix(
            &fresh
                .base_execute(fresh_handle, 0, None, None, &CancelToken::new())
                .unwrap()
        ),
        result_before
    );
}

#[test]
fn base_apply_edits_applies_dependent_edits_with_one_save_and_final_handle_refresh() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Dependent.base",
            br#"views:
  - type: table
    name: Original
    filters: "file.inFolder(\"Notes\")"
    order: [file.name]
"#,
        )
        .unwrap();
        p.write_file("Notes/Alpha.md", b"# Alpha\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Dependent.base").unwrap();
    let generation_before = session.bases_generation();
    assert!(
        session
            .read_oplog("Queries/Dependent.base")
            .unwrap()
            .is_empty()
    );

    session
        .base_apply_edits(
            handle,
            vec![
                crate::bases::BaseEdit::AddView {
                    yaml: "type: table\nname: Added\nfilters: 'file.inFolder(\"Notes\")'\norder: [file.name]"
                        .to_string(),
                },
                crate::bases::BaseEdit::RenameView {
                    view: 1,
                    name: "Final".to_string(),
                },
            ],
        )
        .unwrap();

    assert_eq!(session.bases_generation(), generation_before + 1);
    assert_eq!(
        session.read_oplog("Queries/Dependent.base").unwrap().len(),
        1
    );
    assert_eq!(
        session
            .base_views(handle)
            .unwrap()
            .into_iter()
            .map(|view| view.name)
            .collect::<Vec<_>>(),
        ["Original", "Final"]
    );
    assert_eq!(
        session
            .base_execute(handle, 1, None, None, &CancelToken::new())
            .unwrap()
            .rows[0]
            .file_path,
        "Notes/Alpha.md"
    );
    assert_eq!(
        session
            .bases_list()
            .unwrap()
            .into_iter()
            .find(|summary| summary.path == "Queries/Dependent.base")
            .unwrap()
            .view_count,
        2
    );
}

#[test]
fn base_apply_edits_empty_batch_is_a_true_no_op() {
    let (tmp, session) = make_vault(|p| {
        p.write_file(
            "Queries/Empty.base",
            br#"views:
  - type: table
    name: Empty
    filters: "file.inFolder(\"Notes\")"
    order: [file.name, score]
"#,
        )
        .unwrap();
        p.write_file("Notes/Ten.md", b"---\nscore: 10\n---\n# Ten\n")
            .unwrap();
        p.write_file("Notes/Two.md", b"---\nscore: 2\n---\n# Two\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let handle = session.open_base("Queries/Empty.base").unwrap();
    session
        .base_set_transient_sort(handle, 0, Some("score".to_string()), true)
        .unwrap();
    let disk_before = std::fs::read(tmp.path().join("Queries/Empty.base")).unwrap();
    let generation_before = session.bases_generation();
    let query_before = session.base_view_edit_query_json(handle, 0).unwrap();

    session.base_apply_edits(handle, Vec::new()).unwrap();

    assert_eq!(
        std::fs::read(tmp.path().join("Queries/Empty.base")).unwrap(),
        disk_before
    );
    assert_eq!(session.bases_generation(), generation_before);
    assert!(session.read_oplog("Queries/Empty.base").unwrap().is_empty());
    assert_eq!(
        session.base_view_edit_query_json(handle, 0).unwrap(),
        query_before
    );
    assert_eq!(
        session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap()
            .rows[0]
            .file_path,
        "Notes/Two.md"
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
        vec!["Alpha", "Beta"]
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
fn save_query_as_base_round_trips_unsafe_note_and_formula_property_references() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Notes/Alpha.md",
            b"---\nproject-status: active\ntrue: yes\nbase-value: 2\n---\n# Alpha\n",
        )
        .unwrap();
        p.write_file(
            "Notes/Beta.md",
            b"---\nproject-status: archived\ntrue: yes\nbase-value: 2\n---\n# Beta\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let query = crate::bases::SlateQuery {
        source: crate::bases::QuerySource::Folder("Notes".to_string()),
        row_source: crate::bases::RowSource::Files,
        filters: Some(crate::bases::FilterNode::Stmt(
            crate::bases::expr::parse_expr(
                r#"note["project-status"] == "active" && note["true"] == "yes" && formula["score-total"] == 3 && formula["true"] == 7"#,
            )
            .unwrap(),
        )),
        formulas: vec![
            (
                "score-total".to_string(),
                crate::bases::expr::parse_expr(r#"note["base-value"] + 1"#).unwrap(),
            ),
            (
                "true".to_string(),
                crate::bases::expr::parse_expr(r#"note["base-value"] + 5"#).unwrap(),
            ),
        ],
        custom_summaries: Vec::new(),
        group_by: None,
        sort: Vec::new(),
        columns: [
            "note[\"project-status\"]",
            "note[\"true\"]",
            "formula[\"score-total\"]",
            "formula[\"true\"]",
        ]
        .into_iter()
        .map(|id| crate::bases::ColumnSelection {
            id: id.to_string(),
            display_name: None,
        })
        .collect(),
        summaries: Vec::new(),
        limit: None,
        view: crate::bases::ViewSpec::Table {
            fallback_from: None,
        },
    };
    let original_handle = session
        .open_query(&serde_json::to_string(&query).unwrap(), None)
        .unwrap();
    let original = session
        .base_execute(original_handle, 0, None, None, &CancelToken::new())
        .unwrap();

    session
        .save_query_as_base(
            &serde_json::to_string(&query).unwrap(),
            "Queries/UnsafeNames.base",
        )
        .unwrap();
    let exported = session.read_text("Queries/UnsafeNames.base").unwrap();
    assert!(
        exported.contains(r#"note[\"project-status\"]"#),
        "{exported}"
    );
    assert!(exported.contains(r#"note[\"true\"]"#), "{exported}");
    assert!(
        exported.contains(r#"formula[\"score-total\"]"#),
        "{exported}"
    );
    assert!(exported.contains(r#"formula[\"true\"]"#), "{exported}");

    let reopened_handle = session.open_base("Queries/UnsafeNames.base").unwrap();
    let reopened = session
        .base_execute(reopened_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        result_matrix(&reopened),
        result_matrix(&original),
        "exported .base:\n{exported}\nreopened query:\n{}",
        session.base_view_query_json(reopened_handle, 0).unwrap()
    );
}

#[test]
fn save_query_as_base_quotes_yaml_native_formula_expression_scalars() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Notes/Alpha.md", b"---\nnull: 11\nyes: 12\n---\n# Alpha\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let query = crate::bases::SlateQuery {
        source: crate::bases::QuerySource::Folder("Notes".to_string()),
        row_source: crate::bases::RowSource::Files,
        filters: None,
        formulas: [
            ("integer", "7"),
            ("decimal", "1.5"),
            ("boolean", "true"),
            ("nullLike", "null"),
            ("yamlBooleanLike", "yes"),
            ("dateLike", r#""2024-01-01""#),
        ]
        .into_iter()
        .map(|(name, expression)| {
            (
                name.to_string(),
                crate::bases::expr::parse_expr(expression).unwrap(),
            )
        })
        .collect(),
        custom_summaries: Vec::new(),
        group_by: None,
        sort: Vec::new(),
        columns: [
            "formula.integer",
            "formula.decimal",
            "formula.boolean",
            "formula.nullLike",
            "formula.yamlBooleanLike",
            "formula.dateLike",
        ]
        .into_iter()
        .map(|id| crate::bases::ColumnSelection {
            id: id.to_string(),
            display_name: None,
        })
        .collect(),
        summaries: Vec::new(),
        limit: None,
        view: crate::bases::ViewSpec::Table {
            fallback_from: None,
        },
    };
    let original_handle = session
        .open_query(&serde_json::to_string(&query).unwrap(), None)
        .unwrap();
    let original = session
        .base_execute(original_handle, 0, None, None, &CancelToken::new())
        .unwrap();

    session
        .save_query_as_base(
            &serde_json::to_string(&query).unwrap(),
            "Queries/ScalarFormulas.base",
        )
        .unwrap();
    let exported = session.read_text("Queries/ScalarFormulas.base").unwrap();
    for expected in [
        r#"  integer: "7""#,
        r#"  decimal: "1.5""#,
        r#"  boolean: "true""#,
        r#"  nullLike: "null""#,
        r#"  yamlBooleanLike: "yes""#,
        r#"  dateLike: "\"2024-01-01\"""#,
    ] {
        assert!(
            exported.contains(expected),
            "missing {expected:?}:\n{exported}"
        );
    }

    let reopened_handle = session.open_base("Queries/ScalarFormulas.base").unwrap();
    let reopened = session
        .base_execute(reopened_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(result_matrix(&reopened), result_matrix(&original));
}

#[test]
fn save_query_as_base_quotes_yaml_native_custom_summary_expression_scalars() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("Notes/Alpha.md", b"# Alpha\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    for (name, expression, expected) in [
        ("integer", "7", r#"  integer: "7""#),
        ("boolean", "true", r#"  boolean: "true""#),
        (
            "dateLike",
            r#""2024-01-01""#,
            r#"  dateLike: "\"2024-01-01\"""#,
        ),
    ] {
        let query = crate::bases::SlateQuery {
            source: crate::bases::QuerySource::Folder("Notes".to_string()),
            row_source: crate::bases::RowSource::Files,
            filters: None,
            formulas: Vec::new(),
            custom_summaries: vec![(
                name.to_string(),
                crate::bases::expr::parse_expr(expression).unwrap(),
            )],
            group_by: None,
            sort: Vec::new(),
            columns: vec![crate::bases::ColumnSelection {
                id: "file.name".to_string(),
                display_name: None,
            }],
            summaries: vec![(
                "file.name".to_string(),
                crate::bases::SummaryRef::Custom(name.to_string()),
            )],
            limit: None,
            view: crate::bases::ViewSpec::Table {
                fallback_from: None,
            },
        };
        let query_json = serde_json::to_string(&query).unwrap();
        let original_handle = session.open_query(&query_json, None).unwrap();
        let original = session
            .base_execute(original_handle, 0, None, None, &CancelToken::new())
            .unwrap();
        let path = format!("Queries/{name}.base");

        session.save_query_as_base(&query_json, &path).unwrap();
        let exported = session.read_text(&path).unwrap();
        assert!(
            exported.contains(expected),
            "missing {expected:?} in {name}:\n{exported}"
        );

        let reopened_handle = session.open_base(&path).unwrap();
        let reopened = session
            .base_execute(reopened_handle, 0, None, None, &CancelToken::new())
            .unwrap();
        assert_eq!(
            reopened.summaries, original.summaries,
            "custom summary {name} changed after export/reopen:\n{exported}"
        );
    }
}

#[test]
fn save_query_as_base_round_trips_unsafe_and_safe_this_note_properties() {
    use crate::bases::expr::{BinaryOp, Expr, ExprKind, Lit, PropertyRef, Span};

    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Context.md",
            b"---\nfoo-bar: active\nsafeName: ready\n---\n# Context\n",
        )
        .unwrap();
        p.write_file("Notes/Alpha.md", b"# Alpha\n").unwrap();
        p.write_file("Notes/Beta.md", b"# Beta\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let span = Span { start: 0, end: 0 };
    let comparison = |property: PropertyRef, expected: &str| Expr {
        span,
        kind: ExprKind::Binary {
            op: BinaryOp::Eq,
            lhs: Box::new(Expr {
                span,
                kind: ExprKind::Prop(property),
            }),
            rhs: Box::new(Expr {
                span,
                kind: ExprKind::Lit(Lit::String(expected.to_string())),
            }),
        },
    };
    let query = crate::bases::SlateQuery {
        source: crate::bases::QuerySource::Folder("Notes".to_string()),
        row_source: crate::bases::RowSource::Files,
        filters: Some(crate::bases::FilterNode::Stmt(Expr {
            span,
            kind: ExprKind::Binary {
                op: BinaryOp::And,
                lhs: Box::new(comparison(
                    PropertyRef::ThisNote("foo-bar".to_string()),
                    "active",
                )),
                rhs: Box::new(comparison(
                    PropertyRef::ThisNote("safeName".to_string()),
                    "ready",
                )),
            },
        })),
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
    let query_json = serde_json::to_string(&query).unwrap();
    let original_handle = session.open_query(&query_json, None).unwrap();
    let original = session
        .base_execute(
            original_handle,
            0,
            Some("Context.md".to_string()),
            None,
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(original.rows.len(), 2);

    session
        .save_query_as_base(&query_json, "Queries/ThisProperties.base")
        .unwrap();
    let exported = session.read_text("Queries/ThisProperties.base").unwrap();
    assert!(exported.contains(r#"this[\"foo-bar\"]"#), "{exported}");
    assert!(exported.contains("this.safeName"), "{exported}");

    let reopened_handle = session.open_base("Queries/ThisProperties.base").unwrap();
    let reopened = session
        .base_execute(
            reopened_handle,
            0,
            Some("Context.md".to_string()),
            None,
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(result_matrix(&reopened), result_matrix(&original));
}

#[test]
fn session_column_kinds_skip_leading_nulls_and_errors() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Notes/Alpha.md",
            b"---\nwhen: [bad]\nbad: [x]\n---\n# Alpha\n",
        )
        .unwrap();
        p.write_file(
            "Notes/Bravo.md",
            b"---\nscore: 3\nwhen: 1000\nbad: [x]\n---\n# Bravo\n",
        )
        .unwrap();
        p.write_file(
            "Notes/Charlie.md",
            b"---\nscore: high\nwhen: 2024-01-01\nbad: [x]\n---\n# Charlie\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let query = crate::bases::SlateQuery {
        source: crate::bases::QuerySource::Folder("Notes".to_string()),
        row_source: crate::bases::RowSource::Files,
        filters: None,
        formulas: Vec::new(),
        custom_summaries: Vec::new(),
        group_by: None,
        sort: Vec::new(),
        columns: ["score", "date(when)", "missing", "date(bad)"]
            .into_iter()
            .map(|id| crate::bases::ColumnSelection {
                id: id.to_string(),
                display_name: None,
            })
            .collect(),
        summaries: Vec::new(),
        limit: None,
        view: crate::bases::ViewSpec::Table {
            fallback_from: None,
        },
    };
    let handle = session
        .open_query(&serde_json::to_string(&query).unwrap(), None)
        .unwrap();
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();

    assert_eq!(
        result
            .columns
            .iter()
            .map(|column| column.value_kind.as_str())
            .collect::<Vec<_>>(),
        vec!["number", "date", "null", "null"]
    );
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.values[0].raw_kind.as_str())
            .collect::<Vec<_>>(),
        vec!["null", "number", "text"]
    );
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.values[1].raw_kind.as_str())
            .collect::<Vec<_>>(),
        vec!["error", "date", "date"]
    );
    assert!(
        result
            .rows
            .iter()
            .all(|row| row.values[2].raw_kind == "null")
    );
    assert!(
        result
            .rows
            .iter()
            .all(|row| row.values[3].raw_kind == "error")
    );
}

#[test]
fn recent_export_reopen_preserves_exact_cutoff_membership() {
    const DAY_MS: i64 = 86_400_000;
    const NOW_MS: i64 = DAY_MS * 2;
    const CUTOFF_MS: i64 = NOW_MS - DAY_MS;

    let (_tmp, session) = make_vault(|p| {
        p.write_file("Notes/Exact.md", b"# Exact\n").unwrap();
        p.write_file("Notes/Older.md", b"# Older\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session
        .conn
        .lock()
        .expect("session connection mutex")
        .execute(
            "UPDATE files
             SET mtime_ms = CASE path
                 WHEN 'Notes/Exact.md' THEN ?1
                 WHEN 'Notes/Older.md' THEN ?2
                 ELSE mtime_ms
             END",
            rusqlite::params![CUTOFF_MS, CUTOFF_MS - 1],
        )
        .unwrap();

    let query = crate::bases::SlateQuery {
        source: crate::bases::QuerySource::Recent { days: 1 },
        row_source: crate::bases::RowSource::Files,
        filters: Some(crate::bases::FilterNode::Stmt(
            crate::bases::expr::parse_expr(r#"file.ext == "md""#).unwrap(),
        )),
        formulas: Vec::new(),
        custom_summaries: Vec::new(),
        group_by: None,
        sort: Vec::new(),
        columns: vec![crate::bases::ColumnSelection {
            id: "file.path".to_string(),
            display_name: None,
        }],
        summaries: Vec::new(),
        limit: None,
        view: crate::bases::ViewSpec::Table {
            fallback_from: None,
        },
    };
    let execute_at_fixed_time = |query: &crate::bases::SlateQuery| {
        let conn = session.conn.lock().expect("session connection mutex");
        crate::bases::engine::execute(
            query,
            &conn,
            &crate::bases::engine::EngineCtx {
                now_ms: NOW_MS,
                ..crate::bases::engine::EngineCtx::default()
            },
            &CancelToken::new(),
        )
        .unwrap()
    };

    let before_export = execute_at_fixed_time(&query);
    assert_eq!(
        before_export
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Exact.md"]
    );

    let query_json = serde_json::to_string(&query).unwrap();
    session
        .save_query_as_base(&query_json, "Queries/Recent.base")
        .unwrap();
    let exported = session.read_text("Queries/Recent.base").unwrap();
    assert!(
        exported.contains("file.mtime >= now() - duration"),
        "{exported}"
    );

    let reopened_handle = session.open_base("Queries/Recent.base").unwrap();
    let reopened_query: crate::bases::SlateQuery =
        serde_json::from_str(&session.base_view_query_json(reopened_handle, 0).unwrap()).unwrap();
    let after_reopen = execute_at_fixed_time(&reopened_query);
    assert_eq!(
        after_reopen
            .rows
            .iter()
            .map(|row| row.path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Exact.md"]
    );
}

#[test]
fn census_bases_roundtrip() {
    assert_eq!(
        SESSION_BASE_CORPUS
            .iter()
            .map(|(name, _)| (*name).to_string())
            .collect::<BTreeSet<_>>(),
        checked_in_base_fixture_names(),
        "the session round-trip corpus must enumerate every checked-in .base fixture"
    );
    let (tmp, session, writes) = make_counting_write_vault(|provider| {
        for (fixture, source) in SESSION_BASE_CORPUS {
            provider
                .write_file(&format!("Corpus/{fixture}"), source)
                .unwrap_or_else(|error| panic!("write checked-in Base fixture {fixture}: {error}"));
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    writes.store(0, std::sync::atomic::Ordering::SeqCst);

    assert_eq!(
        session.bases_list().unwrap().len(),
        SESSION_BASE_CORPUS.len(),
        "the session/provider census must include every checked-in .base fixture"
    );

    for (fixture, source) in SESSION_BASE_CORPUS {
        let path = format!("Corpus/{fixture}");
        let before = vault_content_snapshot(tmp.path());
        let writes_before = writes.load(std::sync::atomic::Ordering::SeqCst);
        let handle = session
            .open_base(&path)
            .unwrap_or_else(|error| panic!("open checked-in Base fixture {fixture}: {error}"));
        assert_eq!(
            writes.load(std::sync::atomic::Ordering::SeqCst),
            writes_before,
            "opening checked-in fixture {fixture} must not write through the provider"
        );
        let views = session.base_views(handle).unwrap();
        let edit = if let Some(view) = views.first() {
            crate::bases::BaseEdit::RenameView {
                view: 0,
                name: view.name.clone(),
            }
        } else {
            crate::bases::BaseEdit::SetTopLevelFilters {
                yaml: String::from_utf8_lossy(source).trim_end().to_string(),
            }
        };
        session
            .base_apply_edit(handle, edit)
            .unwrap_or_else(|error| panic!("save checked-in Base fixture {fixture}: {error}"));
        session.close_base(handle);

        assert_eq!(
            writes.load(std::sync::atomic::Ordering::SeqCst),
            writes_before + 1,
            "open→save must traverse provider write I/O for {fixture}"
        );
        assert_eq!(
            std::fs::read(tmp.path().join(&path)).unwrap(),
            source,
            "session open→save changed checked-in fixture bytes for {fixture}"
        );
        assert_eq!(
            vault_content_snapshot(tmp.path()),
            before,
            "session open→save changed vault bytes for {fixture}"
        );
    }
}

#[test]
fn census_bases_determinism() {
    let notes = [
        ("Notes/Zeta.md", "---\nstatus: later\n---\n# Zeta\n"),
        ("Notes/Alpha.md", "---\nstatus: now\n---\n# Alpha\n"),
        ("Notes/Beta.md", "---\nstatus: now\n---\n# Beta\n"),
        ("Notes/Eta.md", "---\nstatus: later\n---\n# Eta\n"),
        ("Notes/Delta.md", "---\nstatus: done\n---\n# Delta\n"),
    ];
    let mut expected = vec![None::<BasesResultSignature>; DETERMINISM_BASES.len()];
    for seed in 0..bases_census_seed_count() {
        let shape = seed as usize % DETERMINISM_BASES.len();
        let base = DETERMINISM_BASES[shape];
        let base_source = String::from_utf8_lossy(base);
        let mut rng = SplitMix64(seed);
        let mut order = (0..notes.len()).collect::<Vec<_>>();
        for i in (1..order.len()).rev() {
            let j = rng.below(i + 1);
            order.swap(i, j);
        }
        let (_tmp, session) = make_vault(|p| {
            p.write_file("Queries/ByStatus.base", base)
                .unwrap_or_else(|error| {
                    panic!(
                        "write determinism base failed seed={seed} shape={shape}: {error}\nbase={base_source}"
                    )
                });
            for index in order {
                let (path, source) = notes[index];
                p.write_file(path, source.as_bytes())
                    .unwrap_or_else(|error| {
                        panic!(
                            "write determinism note failed seed={seed} shape={shape} path={path}: {error}\nbase={base_source}"
                        )
                    });
            }
        });
        session
            .scan_initial(&CancelToken::new())
            .unwrap_or_else(|error| {
                panic!(
                    "determinism scan failed seed={seed} shape={shape}: {error}\nbase={base_source}"
                )
            });
        let handle = session
            .open_base("Queries/ByStatus.base")
            .unwrap_or_else(|error| {
                panic!(
                    "determinism handle open failed seed={seed} shape={shape}: {error}\nbase={base_source}"
                )
            });
        let result = session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap_or_else(|error| {
                panic!(
                    "determinism execute failed seed={seed} shape={shape}: {error}\nbase={base_source}"
                )
            });
        let signature = (
            result_matrix(&result),
            group_signature(&result),
            result.total_count,
            result.shown_count,
        );
        if let Some(expected) = &expected[shape] {
            assert_eq!(
                &signature, expected,
                "insertion-order determinism mismatch seed={seed} shape={shape}\nbase={}",
                base_source
            );
        } else {
            expected[shape] = Some(signature);
        }
    }
    assert!(
        expected.iter().all(Option::is_some),
        "every determinism query shape must run"
    );
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
fn delete_file_via_session_invalidates_a_warm_bases_handle() {
    let (tmp, session) = make_unlinking_vault(|provider| {
        provider
            .write_file(
                "Queries/Delete.base",
                br#"views:
  - type: table
    name: Delete
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
"#,
            )
            .unwrap();
        provider.write_file("Notes/Keep.md", b"# Keep\n").unwrap();
        provider
            .write_file("Notes/Delete.md", b"# Delete\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let warm_handle = session.open_base("Queries/Delete.base").unwrap();
    let before = session
        .base_execute(warm_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(before.total_count, 2);
    session
        .base_execute(warm_handle, 0, None, None, &CancelToken::new())
        .expect("populate per-handle cache");

    session
        .delete_file("Notes/Delete.md")
        .expect("public session delete succeeds through unlinking test provider");
    assert!(!tmp.path().join("Notes/Delete.md").exists());

    let warm_after = session
        .base_execute(warm_handle, 0, None, None, &CancelToken::new())
        .expect("generation bump invalidates warm result");
    let cold_handle = session.open_base("Queries/Delete.base").unwrap();
    let cold_after = session
        .base_execute(cold_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(warm_after.total_count, 1);
    assert_eq!(warm_after.rows[0].file_path, "Notes/Keep.md");
    assert_eq!(
        normalize_bases_result_time(warm_after),
        normalize_bases_result_time(cold_after)
    );
}

#[derive(Debug, Clone, Copy)]
enum CacheCensusOp {
    Save,
    SetProperty,
    DeleteProperty,
    Rename,
    DeleteViaSession,
    CacheReadA,
    CacheReadB,
}

fn normalize_bases_result_time(mut result: BasesResultSet) -> BasesResultSet {
    result.executed_at_ms = 0;
    result
}

#[test]
fn census_bases_warm_handle_matches_cold_after_generated_mutations() {
    const BASE: &[u8] = br#"views:
  - type: table
    name: Mutation census
    filters: "file.inFolder(\"Notes\")"
    groupBy:
      property: status
      direction: ASC
    order:
      - file.name
      - status
      - score
    slate:
      sort:
        - expr: status
          direction: asc
        - expr: file.name
          direction: asc
"#;
    let seeds = bases_cache_census_seed_count();
    let base_source = String::from_utf8_lossy(BASE);

    for seed in 0..seeds {
        let (tmp, session) = make_unlinking_vault(|provider| {
            provider.write_file("Queries/Mutation.base", BASE).unwrap();
            for (path, status, score) in [
                ("Notes/Save.md", "active", 1),
                ("Notes/Set.md", "waiting", 2),
                ("Notes/DropProperty.md", "done", 3),
                ("Notes/Rename.md", "active", 4),
                ("Notes/Delete.md", "archived", 5),
                ("Notes/Keep.md", "active", 6),
            ] {
                provider
                    .write_file(
                        path,
                        format!(
                            "---\nstatus: {status}\nscore: {score}\n---\n# {path}\nseed {seed}\n"
                        )
                        .as_bytes(),
                    )
                    .unwrap();
            }
        });
        session
            .scan_initial(&CancelToken::new())
            .unwrap_or_else(|error| {
                panic!("initial scan failed seed={seed}: {error}\nbase={base_source}")
            });
        let warm_handle = session
            .open_base("Queries/Mutation.base")
            .unwrap_or_else(|error| {
                panic!("warm handle open failed seed={seed}: {error}\nbase={base_source}")
            });
        session
            .base_execute(warm_handle, 0, None, None, &CancelToken::new())
            .unwrap_or_else(|error| {
                panic!("initial warm execution failed seed={seed}: {error}\nbase={base_source}")
            });

        let mut operations = [
            CacheCensusOp::Save,
            CacheCensusOp::SetProperty,
            CacheCensusOp::DeleteProperty,
            CacheCensusOp::Rename,
            CacheCensusOp::DeleteViaSession,
            CacheCensusOp::CacheReadA,
            CacheCensusOp::CacheReadB,
        ];
        let mut rng = SplitMix64(seed ^ 0xCA_C4_EF_12_5E_ED);
        for index in (1..operations.len()).rev() {
            let other = rng.below(index + 1);
            operations.swap(index, other);
        }

        for (op_index, operation) in operations.iter().copied().enumerate() {
            let prefix = &operations[..=op_index];
            match operation {
                CacheCensusOp::Save => {
                    session
                        .save_text(
                            "Notes/Save.md",
                            &format!(
                                "---\nstatus: saved-{seed}\nscore: 11\n---\n# Save\nseed {seed}\n"
                            ),
                            None,
                        )
                        .unwrap_or_else(|error| {
                            panic!(
                                "save failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                            )
                        });
                }
                CacheCensusOp::SetProperty => {
                    session
                        .set_property(
                            "Notes/Set.md",
                            "status",
                            crate::PropertyValue::Text(format!("set-{seed}")),
                            None,
                        )
                        .unwrap_or_else(|error| {
                            panic!(
                                "property set failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                            )
                        });
                }
                CacheCensusOp::DeleteProperty => {
                    session
                        .delete_property("Notes/DropProperty.md", "status", None)
                        .unwrap_or_else(|error| {
                            panic!(
                                "property delete failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                            )
                        });
                }
                CacheCensusOp::Rename => {
                    session
                        .rename_file("Notes/Rename.md", &format!("Renamed-{seed}.md"))
                        .unwrap_or_else(|error| {
                            panic!(
                                "rename failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                            )
                        });
                }
                CacheCensusOp::DeleteViaSession => {
                    session.delete_file("Notes/Delete.md").unwrap_or_else(|error| {
                        panic!(
                            "session delete failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                        )
                    });
                }
                CacheCensusOp::CacheReadA | CacheCensusOp::CacheReadB => {}
            }

            let before_read_only = vault_content_snapshot(tmp.path());
            let warm_after_mutation = session
                .base_execute(warm_handle, 0, None, None, &CancelToken::new())
                .unwrap_or_else(|error| {
                    panic!(
                        "warm execution failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                    )
                });
            let warm_cached = session
                .base_execute(warm_handle, 0, None, None, &CancelToken::new())
                .unwrap_or_else(|error| {
                    panic!(
                        "warm cache execution failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                    )
                });
            let cold_handle = session
                .open_base("Queries/Mutation.base")
                .unwrap_or_else(|error| {
                    panic!(
                        "cold handle open failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                    )
                });
            let cold = session
                .base_execute(cold_handle, 0, None, None, &CancelToken::new())
                .unwrap_or_else(|error| {
                    panic!(
                        "cold execution failed seed={seed} op_index={op_index} prefix={prefix:?}: {error}\nbase={base_source}"
                    )
                });
            session.close_base(cold_handle);

            assert_eq!(
                normalize_bases_result_time(warm_after_mutation),
                normalize_bases_result_time(warm_cached.clone()),
                "warm cache changed result seed={seed} op_index={op_index} prefix={prefix:?}\nbase={base_source}"
            );
            assert_eq!(
                normalize_bases_result_time(warm_cached),
                normalize_bases_result_time(cold),
                "warm handle diverged from cold handle seed={seed} op_index={op_index} prefix={prefix:?}\nbase={base_source}"
            );
            assert_eq!(
                vault_content_snapshot(tmp.path()),
                before_read_only,
                "Bases execution wrote vault bytes seed={seed} op_index={op_index} prefix={prefix:?}\nbase={base_source}"
            );
        }
    }
}

#[test]
fn census_bases_fail_loud() {
    let (tmp, session) = make_dql_corpus_vault();
    let cases = session_dql_cases();
    let unique_names = cases
        .iter()
        .map(|case| case.name.as_str())
        .collect::<BTreeSet<_>>();
    assert_eq!(
        unique_names.len(),
        cases.len(),
        "checked-in DQL corpus case names must be globally unique"
    );
    assert!(
        cases
            .iter()
            .any(|case| case.disposition == SessionDqlDisposition::Supported),
        "session DQL corpus must include supported cases"
    );
    assert!(
        cases
            .iter()
            .any(|case| case.disposition == SessionDqlDisposition::Unsupported),
        "session DQL corpus must include unsupported cases"
    );

    let err = session.base_views(9_999).unwrap_err();
    assert!(err.to_string().contains("unknown base handle"), "{err}");

    let saved_query_err = session.open_saved_query("missing").unwrap_err();
    assert!(
        saved_query_err
            .to_string()
            .contains("unknown saved query id"),
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

    for case in &cases {
        let context = format!("DQL corpus case {} source={:?}", case.name, case.source);
        let before = vault_content_snapshot(tmp.path());
        let handle = session
            .open_dql(&case.source, case.this_path.clone())
            .unwrap_or_else(|error| panic!("{context}: open_dql failed: {error}"));
        let result = session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap_or_else(|error| panic!("{context}: execute failed: {error}"));
        session.close_base(handle);
        assert_eq!(
            vault_content_snapshot(tmp.path()),
            before,
            "{context}: execution changed vault bytes"
        );

        match case.disposition {
            SessionDqlDisposition::Supported => {
                assert!(
                    result.warnings.is_empty(),
                    "{context}: conversion warnings: {:?}",
                    result.warnings
                );
                assert_eq!(result.view_error, None, "{context}: failed loud");
                assert!(
                    result
                        .rows
                        .iter()
                        .flat_map(|row| &row.values)
                        .all(|value| value.error.is_none()),
                    "{context}: supported query produced an error marker: {result:?}"
                );
                session
                    .dql_as_base(&case.source)
                    .unwrap_or_else(|error| panic!("{context}: conversion failed: {error}"));
            }
            SessionDqlDisposition::Unsupported => {
                let warning = case.warning_message.as_deref().unwrap_or_else(|| {
                    panic!("{context}: unsupported fixture lacks warning_message")
                });
                let expected = case.error_contains.as_deref().unwrap_or_else(|| {
                    panic!("{context}: unsupported fixture lacks error_contains")
                });
                assert!(
                    result.warnings.iter().any(|actual| actual == warning),
                    "{context}: expected exact warning {warning:?}, got {:?}",
                    result.warnings
                );
                assert_named_dql_failure(&result, expected, &context);
                let error = session
                    .dql_as_base(&case.source)
                    .expect_err("unsupported DQL conversion must fail loud");
                assert!(
                    error.to_string().contains(warning),
                    "{context}: converter error did not name {warning:?}: {error}"
                );
            }
            SessionDqlDisposition::RuntimeFailLoud => {
                assert!(
                    result.warnings.is_empty(),
                    "{context}: conversion warnings: {:?}",
                    result.warnings
                );
                let expected = case.error_contains.as_deref().unwrap_or_else(|| {
                    panic!("{context}: runtime fail-loud fixture lacks error_contains")
                });
                assert_named_dql_failure(&result, expected, &context);
                session
                    .dql_as_base(&case.source)
                    .unwrap_or_else(|error| panic!("{context}: conversion failed: {error}"));
            }
        }
    }

    for case in cases
        .iter()
        .filter(|case| case.disposition == SessionDqlDisposition::Supported)
    {
        let baseline_handle = session
            .open_dql(&case.source, case.this_path.clone())
            .unwrap();
        let baseline = session
            .base_execute_at(
                baseline_handle,
                0,
                None,
                None,
                DQL_CENSUS_NOW_MS,
                &CancelToken::new(),
            )
            .unwrap();
        let query: crate::bases::SlateQuery =
            serde_json::from_str(&session.base_view_query_json(baseline_handle, 0).unwrap())
                .unwrap();
        session.close_base(baseline_handle);
        assert!(
            !baseline.rows.is_empty(),
            "supported DQL fixture {} must exercise row-position mutations",
            case.name
        );

        for mutation in FailLoudMutation::ALL {
            let (mutated, reason) = mutate_query_for_fail_loud(&query, mutation);
            let context = format!(
                "DQL mutation case={} position={} reason={reason:?}",
                case.name,
                mutation.label()
            );
            let before = vault_content_snapshot(tmp.path());
            let handle = session
                .open_query(
                    &serde_json::to_string(&mutated).unwrap(),
                    case.this_path.clone(),
                )
                .unwrap();
            let result = session
                .base_execute_at(
                    handle,
                    0,
                    None,
                    None,
                    DQL_CENSUS_NOW_MS,
                    &CancelToken::new(),
                )
                .unwrap_or_else(|error| panic!("{context}: execute failed: {error}"));
            session.close_base(handle);
            assert_eq!(
                vault_content_snapshot(tmp.path()),
                before,
                "{context}: query execution changed vault bytes"
            );

            match mutation {
                FailLoudMutation::FormulaInert => {
                    assert_eq!(
                        result.view_error, None,
                        "{context}: inert formula failed view"
                    );
                    assert_eq!(
                        result_membership(&result),
                        result_membership(&baseline),
                        "{context}: unreferenced unsupported formula changed membership"
                    );
                    assert_eq!(
                        result_matrix(&result),
                        result_matrix(&baseline),
                        "{context}: unreferenced unsupported formula changed visible values"
                    );
                }
                FailLoudMutation::Columns => {
                    assert_eq!(
                        result.view_error, None,
                        "{context}: column error must stay in its error-marker cells"
                    );
                    assert_eq!(
                        result_membership(&result),
                        result_membership(&baseline),
                        "{context}: column error changed membership"
                    );
                    assert!(
                        result.rows.iter().all(|row| {
                            row.values
                                .last()
                                .and_then(|value| value.error.as_deref())
                                .is_some_and(|error| error.contains(&reason))
                        }),
                        "{context}: referenced unsupported column lacked named markers: {result:?}"
                    );
                }
                FailLoudMutation::Summary => {
                    assert_eq!(
                        result.view_error, None,
                        "{context}: summary error must stay in its error-marker cell"
                    );
                    assert_eq!(
                        result_membership(&result),
                        result_membership(&baseline),
                        "{context}: summary error changed membership"
                    );
                    assert!(
                        result.summaries.iter().any(|summary| {
                            summary
                                .value
                                .error
                                .as_deref()
                                .is_some_and(|error| error.contains(&reason))
                        }),
                        "{context}: unsupported summary lacked a named marker: {result:?}"
                    );
                }
                FailLoudMutation::Source
                | FailLoudMutation::Filter
                | FailLoudMutation::Sort
                | FailLoudMutation::Group => {
                    let error = result.view_error.as_deref().unwrap_or_else(|| {
                        panic!("{context}: membership-affecting error did not fail the view")
                    });
                    assert!(error.contains(&reason), "{context}: got {error:?}");
                    assert!(result.rows.is_empty(), "{context}: leaked partial rows");
                }
            }
        }
    }
}

#[test]
fn census_bases_read_only() {
    let (tmp, session) = make_dql_corpus_vault();
    for (fixture, source) in SESSION_BASE_CORPUS {
        session
            .save_text(
                &format!("Corpus/{fixture}"),
                &String::from_utf8_lossy(source),
                None,
            )
            .unwrap_or_else(|error| panic!("install Base fixture {fixture}: {error}"));
    }
    let cases = session_dql_cases();
    assert_eq!(
        session.bases_list().unwrap().len(),
        SESSION_BASE_CORPUS.len() + 1,
        "the read-only vault includes the Base corpus plus Notes/View.base"
    );

    for (fixture, _) in SESSION_BASE_CORPUS {
        let path = format!("Corpus/{fixture}");
        let before_open = vault_content_snapshot(tmp.path());
        let handle = session.open_base(&path).unwrap();
        let views = session.base_views(handle).unwrap();
        assert_eq!(
            vault_content_snapshot(tmp.path()),
            before_open,
            "open_base changed vault bytes for {fixture}"
        );
        for view in 0..views.len() {
            let before = vault_content_snapshot(tmp.path());
            session
                .base_execute(handle, view as u32, None, None, &CancelToken::new())
                .unwrap_or_else(|error| {
                    panic!("execute Base fixture {fixture} view {view}: {error}")
                });
            session
                .base_export(handle, view as u32, ExportFormat::Markdown, None)
                .unwrap_or_else(|error| {
                    panic!("export Base fixture {fixture} view {view}: {error}")
                });
            assert_eq!(
                vault_content_snapshot(tmp.path()),
                before,
                "Base fixture {fixture} view {view} changed vault bytes"
            );
        }
        session.close_base(handle);
        assert_eq!(
            vault_content_snapshot(tmp.path()),
            before_open,
            "closing Base fixture {fixture} changed vault bytes"
        );
    }

    for case in &cases {
        let context = format!("read-only DQL case {} source={:?}", case.name, case.source);
        let before = vault_content_snapshot(tmp.path());
        let handle = session
            .open_dql(&case.source, case.this_path.clone())
            .unwrap_or_else(|error| panic!("{context}: open_dql failed: {error}"));
        session
            .base_execute(handle, 0, None, None, &CancelToken::new())
            .unwrap_or_else(|error| panic!("{context}: execute failed: {error}"));
        session.close_base(handle);
        assert_eq!(
            vault_content_snapshot(tmp.path()),
            before,
            "{context}: query changed vault bytes"
        );
    }
}

#[test]
fn n4_closeout_fixture_vault_e2e() {
    let fixture = include_str!("../../../tests/fixtures/bases/comments_key_order.base");
    let (tmp, session) = make_vault(|p| {
        p.write_file("Queries/Reading.base", fixture.as_bytes())
            .unwrap();
        p.write_file(
            "Notes/Alpha.md",
            b"---\ntags: [reading]\nstatus: active\n---\n# Alpha\n",
        )
        .unwrap();
        p.write_file(
            "Notes/Beta.md",
            b"---\ntags: [reading]\nstatus: waiting\n---\n# Beta\nLinks to [[Notes/Alpha.md]].\n",
        )
        .unwrap();
        p.write_file(
            "Notes/Hidden.md",
            b"---\ntags: [private]\nstatus: hidden\n---\n# Hidden\n",
        )
        .unwrap();
        p.write_file(
            "Notes/Host.md",
            b"# Host\n\n```dataview\nTABLE WITHOUT ID file.name AS \"Name\"\nFROM \"Notes\"\n```\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let (parsed, warnings) = crate::bases::parse_base(fixture);
    assert!(warnings.is_empty(), "{warnings:?}");
    assert_eq!(
        crate::bases::serialize_base(&parsed, &[]).unwrap(),
        fixture,
        "builder/no-op save path must preserve the fixture bytes"
    );

    let handle = session.open_base("Queries/Reading.base").unwrap();
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Alpha.md", "Notes/Beta.md"]
    );
    assert_eq!(result.view_error, None);

    let before_quick_filter = vault_content_snapshot(tmp.path());
    let filtered = session
        .base_execute(
            handle,
            0,
            None,
            Some("beta".to_string()),
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(
        filtered
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Beta.md"]
    );
    assert_eq!(
        vault_content_snapshot(tmp.path()),
        before_quick_filter,
        "quick filter must not dirty vault-authored files"
    );

    session
        .set_property(
            "Notes/Alpha.md",
            "status",
            crate::PropertyValue::Text("done".to_string()),
            None,
        )
        .unwrap();
    assert_eq!(
        session.read_text("Notes/Alpha.md").unwrap(),
        "---\ntags:\n  - reading\nstatus: done\n---\n# Alpha\n"
    );

    let backlinks_base = r#"views:
  - type: table
    name: Better backlinks
    filters: "file.hasLink(this.file)"
    order:
      - file.name
"#;
    let outgoing = session.outgoing_links("Notes/Beta.md").unwrap();
    assert_eq!(outgoing.len(), 1);
    assert_eq!(outgoing[0].target_path.as_deref(), Some("Notes/Alpha.md"));
    let inline = session
        .open_base_inline(backlinks_base, Some("Notes/Alpha.md".to_string()))
        .unwrap();
    let backlinks = session
        .base_execute(inline, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(backlinks.view_error, None);
    assert_eq!(backlinks.warnings, Vec::<String>::new());
    assert_eq!(
        backlinks
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Beta.md"]
    );

    let converted = session
        .dql_as_base("TABLE WITHOUT ID file.name AS \"Name\"\nFROM \"Notes\"\n")
        .unwrap();
    assert_eq!(
        converted,
        "filters: \"file.file.inFolder(\\\"Notes\\\")\"\nformulas:\n  dql_column_1: \"object(\\\"\u{f8ff}slate.dql.file-name\\\", file.path).toString()\"\nproperties:\n  formula.dql_column_1:\n    displayName: Name\nviews:\n  - type: table\n    name: Query\n    order:\n      - formula.dql_column_1\n"
    );
    let converted_handle = session.open_base_inline(&converted, None).unwrap();
    let converted_result = session
        .base_execute(converted_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(converted_result.columns[0].label, "Name");

    let query_json = serde_json::to_string(&crate::bases::view_query(&parsed, 0)).unwrap();
    let saved_id = session
        .save_query(
            "Reading saved",
            Some("N4 close-out relaunch fixture"),
            &query_json,
            SavedQuerySourceSyntax::Builder,
        )
        .unwrap();
    drop(session);

    let reloaded = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    reloaded.scan_initial(&CancelToken::new()).unwrap();
    let saved = reloaded.open_saved_query(&saved_id).unwrap();
    let saved_result = reloaded
        .base_execute(saved, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        saved_result
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Alpha.md", "Notes/Beta.md"]
    );
}

#[test]
fn saved_queries_crud_open_export_and_delete() {
    let (_tmp, session) = make_vault(|p| {
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

    let query_json = notes_query_json();
    let id = session
        .save_query(
            "Reading",
            Some("Status by note"),
            &query_json,
            SavedQuerySourceSyntax::Builder,
        )
        .unwrap();
    assert_eq!(id.len(), 36, "ids should be UUID-shaped");
    assert_eq!(id.chars().filter(|c| *c == '-').count(), 4);

    let duplicate = session
        .save_query(
            "Reading",
            None,
            &query_json,
            SavedQuerySourceSyntax::Builder,
        )
        .unwrap_err();
    assert!(
        duplicate.to_string().contains("saved query name"),
        "{duplicate}"
    );

    let summaries = session.list_saved_queries().unwrap();
    assert_eq!(summaries.len(), 1);
    assert_eq!(summaries[0].id, id);
    assert_eq!(summaries[0].name, "Reading");
    assert_eq!(summaries[0].description.as_deref(), Some("Status by note"));
    assert_eq!(summaries[0].source_syntax, SavedQuerySourceSyntax::Builder);
    assert!(summaries[0].warning.is_none());

    let saved = session.get_saved_query(&id).unwrap();
    assert_eq!(saved.name, "Reading");
    assert_eq!(saved.description.as_deref(), Some("Status by note"));
    assert_eq!(saved.source_syntax, SavedQuerySourceSyntax::Builder);
    assert!(saved.created_at_ms <= saved.modified_at_ms);
    assert!(saved.warning.is_none());
    let envelope: serde_json::Value = serde_json::from_str(&saved.query_json).unwrap();
    assert_eq!(envelope["v"], 1);
    assert!(envelope.get("query").is_some());
    let envelope_id = session
        .save_query(
            "Already enveloped",
            None,
            &saved.query_json,
            SavedQuerySourceSyntax::Base,
        )
        .unwrap();
    let envelope_saved = session.get_saved_query(&envelope_id).unwrap();
    let envelope_round_trip: serde_json::Value =
        serde_json::from_str(&envelope_saved.query_json).unwrap();
    assert_eq!(envelope_round_trip["v"], 1);
    assert!(envelope_saved.warning.is_none());
    let rename_collision = session
        .rename_saved_query(&envelope_id, "Reading")
        .unwrap_err();
    assert!(
        rename_collision.to_string().contains("saved query name"),
        "{rename_collision}"
    );

    let (active_only_base, warnings) = crate::bases::parse_base(
        r#"views:
  - type: table
    name: Active
    filters:
      and:
        - "file.inFolder(\"Notes\")"
        - "status == \"active\""
    order:
      - file.name
      - status
"#,
    );
    assert!(warnings.is_empty(), "{warnings:?}");
    let active_only_json =
        serde_json::to_string(&crate::bases::view_query(&active_only_base, 0)).unwrap();
    session
        .update_saved_query(
            &id,
            Some("Only active notes"),
            &active_only_json,
            SavedQuerySourceSyntax::Builder,
        )
        .unwrap();
    let updated = session.get_saved_query(&id).unwrap();
    assert_eq!(updated.name, "Reading");
    assert_eq!(updated.description.as_deref(), Some("Only active notes"));
    assert_eq!(updated.source_syntax, SavedQuerySourceSyntax::Builder);

    let handle = session.open_saved_query(&id).unwrap();
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(
        result
            .rows
            .iter()
            .map(|row| row.file_path.as_str())
            .collect::<Vec<_>>(),
        vec!["Notes/Alpha.md"]
    );

    session
        .export_saved_query_as_base(&id, "Queries/Reading.base")
        .unwrap();
    let exported = session.read_text("Queries/Reading.base").unwrap();
    assert!(exported.contains("views:\n"));
    assert!(exported.contains("filters:"));
    session.scan_initial(&CancelToken::new()).unwrap();
    let exported_handle = session.open_base("Queries/Reading.base").unwrap();
    let exported_result = session
        .base_execute(exported_handle, 0, None, None, &CancelToken::new())
        .unwrap();
    assert_eq!(result.rows, exported_result.rows);

    session.rename_saved_query(&id, "Reading renamed").unwrap();
    assert_eq!(
        session.get_saved_query(&id).unwrap().name,
        "Reading renamed"
    );

    session.delete_saved_query(&id).unwrap();
    let err = session.open_saved_query(&id).unwrap_err();
    assert!(err.to_string().contains("saved query"), "{err}");
    assert!(
        session.read_text("Queries/Reading.base").is_ok(),
        "deleting a saved query must not delete exported durable forms"
    );
}

#[test]
fn saved_queries_and_dashboards_persist_across_relaunch() {
    let (tmp, session) = make_vault(|p| {
        p.write_file(
            "Notes/Alpha.md",
            br#"---
status: active
---
# Alpha
"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let query_json = notes_query_json();
    let first_id = session
        .save_query("First", None, &query_json, SavedQuerySourceSyntax::Builder)
        .unwrap();
    let second_id = session
        .save_query(
            "Second",
            Some("Converted from DQL"),
            &query_json,
            SavedQuerySourceSyntax::Dql,
        )
        .unwrap();
    let dashboard_id = session
        .save_dashboard(
            "Overview",
            vec![
                DashboardSection {
                    saved_query_id: first_id.clone(),
                    heading_override: Some("Pinned first".to_string()),
                    view_override: None,
                },
                DashboardSection {
                    saved_query_id: second_id.clone(),
                    heading_override: None,
                    view_override: Some("Reading".to_string()),
                },
            ],
        )
        .unwrap();
    drop(session);

    let reopened = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let summaries = reopened.list_saved_queries().unwrap();
    assert_eq!(
        summaries
            .iter()
            .map(|summary| summary.name.as_str())
            .collect::<Vec<_>>(),
        vec!["First", "Second"]
    );

    let dashboards = reopened.list_dashboards().unwrap();
    assert_eq!(dashboards.len(), 1);
    assert_eq!(dashboards[0].id, dashboard_id);
    assert_eq!(dashboards[0].section_count, 2);

    let dashboard = reopened.get_dashboard(&dashboard_id).unwrap();
    assert_eq!(dashboard.name, "Overview");
    assert_eq!(dashboard.sections.len(), 2);
    assert_eq!(
        dashboard.sections[0].saved_query_name.as_deref(),
        Some("First")
    );
    assert_eq!(
        dashboard.sections[0].heading_override.as_deref(),
        Some("Pinned first")
    );
    assert!(!dashboard.sections[0].missing);
    assert_eq!(
        dashboard.sections[1].saved_query_name.as_deref(),
        Some("Second")
    );
    assert_eq!(
        dashboard.sections[1].view_override.as_deref(),
        Some("Reading")
    );
    assert!(!dashboard.sections[1].missing);
}

#[test]
fn dashboards_preserve_dangling_refs_and_do_not_own_saved_queries() {
    let (_tmp, session) = make_vault(|_| {});
    let query_json = notes_query_json();
    let query_id = session
        .save_query(
            "Live query",
            None,
            &query_json,
            SavedQuerySourceSyntax::Base,
        )
        .unwrap();
    let missing_id = "00000000-0000-4000-8000-000000000000".to_string();
    let dashboard_id = session
        .save_dashboard(
            "Dashboard",
            vec![
                DashboardSection {
                    saved_query_id: query_id.clone(),
                    heading_override: None,
                    view_override: None,
                },
                DashboardSection {
                    saved_query_id: missing_id.clone(),
                    heading_override: Some("Missing section".to_string()),
                    view_override: Some("Alt".to_string()),
                },
            ],
        )
        .unwrap();

    let dashboard = session.get_dashboard(&dashboard_id).unwrap();
    assert_eq!(dashboard.sections.len(), 2);
    assert_eq!(
        dashboard.sections[0].saved_query_name.as_deref(),
        Some("Live query")
    );
    assert!(!dashboard.sections[0].missing);
    assert_eq!(dashboard.sections[1].saved_query_id, missing_id);
    assert_eq!(
        dashboard.sections[1].heading_override.as_deref(),
        Some("Missing section")
    );
    assert!(dashboard.sections[1].saved_query_name.is_none());
    assert!(dashboard.sections[1].missing);

    session
        .rename_dashboard(&dashboard_id, "Renamed dashboard")
        .unwrap();
    assert_eq!(
        session.get_dashboard(&dashboard_id).unwrap().name,
        "Renamed dashboard"
    );
    let other_dashboard_id = session.save_dashboard("Other dashboard", vec![]).unwrap();
    let rename_collision = session
        .rename_dashboard(&other_dashboard_id, "Renamed dashboard")
        .unwrap_err();
    assert!(
        rename_collision.to_string().contains("dashboard name"),
        "{rename_collision}"
    );

    session
        .update_dashboard_sections(
            &dashboard_id,
            vec![DashboardSection {
                saved_query_id: missing_id.clone(),
                heading_override: Some("Still missing".to_string()),
                view_override: None,
            }],
        )
        .unwrap();
    let updated = session.get_dashboard(&dashboard_id).unwrap();
    assert_eq!(updated.sections.len(), 1);
    assert_eq!(
        updated.sections[0].heading_override.as_deref(),
        Some("Still missing")
    );
    assert!(updated.sections[0].missing);

    session.delete_dashboard(&dashboard_id).unwrap();
    assert!(session.get_dashboard(&dashboard_id).is_err());
    assert_eq!(
        session.get_saved_query(&query_id).unwrap().name,
        "Live query"
    );
}

#[test]
fn future_saved_query_envelopes_load_as_inert_entries() {
    let (_tmp, session) = make_vault(|_| {});
    let now = now_ms();
    session
        .conn
        .lock()
        .expect("session connection mutex")
        .execute(
            "INSERT INTO saved_queries
             (id, name, description, query_json, source_syntax, created_at_ms, modified_at_ms)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7)",
            rusqlite::params![
                "future-query",
                "Future query",
                Option::<String>::None,
                r#"{"v":99,"query":{"future":true}}"#,
                0_i64,
                now,
                now,
            ],
        )
        .unwrap();

    let summary = session.list_saved_queries().unwrap().pop().unwrap();
    assert_eq!(summary.id, "future-query");
    assert!(
        summary
            .warning
            .as_deref()
            .unwrap()
            .contains("unsupported query_json envelope version 99")
    );

    let saved = session.get_saved_query("future-query").unwrap();
    assert!(saved.warning.unwrap().contains("unsupported"));

    let open_err = session.open_saved_query("future-query").unwrap_err();
    assert!(open_err.to_string().contains("unsupported"), "{open_err}");
    let export_err = session
        .export_saved_query_as_base("future-query", "Queries/Future.base")
        .unwrap_err();
    assert!(
        export_err.to_string().contains("unsupported"),
        "{export_err}"
    );
}

#[test]
fn malformed_dashboard_sections_fail_loud_in_list_and_get() {
    let (_tmp, session) = make_vault(|_| {});
    let now = now_ms();
    session
        .conn
        .lock()
        .expect("session connection mutex")
        .execute(
            "INSERT INTO dashboards
             (id, name, sections_json, created_at_ms, modified_at_ms)
             VALUES (?1, ?2, ?3, ?4, ?5)",
            rusqlite::params!["bad-dashboard", "Bad dashboard", "{not json", now, now],
        )
        .unwrap();

    let list_err = session.list_dashboards().unwrap_err();
    assert!(
        list_err.to_string().contains("invalid dashboard sections"),
        "{list_err}"
    );

    let get_err = session.get_dashboard("bad-dashboard").unwrap_err();
    assert!(
        get_err.to_string().contains("invalid dashboard sections"),
        "{get_err}"
    );
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
