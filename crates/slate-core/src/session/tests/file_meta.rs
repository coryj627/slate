// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Derived-file-metadata censuses for FL0-3.
//!
//! The oracle deliberately treats a fresh production scan as the reference.
//! It never duplicates frontmatter, task, date, or preview parsing in test
//! code. Every incremental checkpoint is compared with a separate SQLite
//! cache populated from the same filesystem bytes.

use super::*;
use std::collections::VecDeque;
use std::path::{Component, Path, PathBuf};

const RANDOM_WALK_OPERATIONS: [CensusOperation; 10] = [
    CensusOperation::Create,
    CensusOperation::ExternalEditAndScan,
    CensusOperation::CasSave,
    CensusOperation::FrontmatterAdd,
    CensusOperation::FrontmatterRemove,
    CensusOperation::FrontmatterRetype,
    CensusOperation::TaskEdit,
    CensusOperation::Rename,
    CensusOperation::Move,
    CensusOperation::Delete,
];

const PAIR_OPERATIONS: [PairOperation; 8] = [
    PairOperation::Create,
    PairOperation::ExternalEditAndScan,
    PairOperation::CasSave,
    PairOperation::Frontmatter,
    PairOperation::TaskEdit,
    PairOperation::Rename,
    PairOperation::Move,
    PairOperation::Delete,
];

#[test]
fn census_file_meta_matches_rescan() {
    let scale = census_scale();
    for seed_index in 0..scale.seeds {
        let seed = 0xf10_3202_6071_4000u64.wrapping_add(seed_index as u64);
        let vault = tempfile::tempdir().expect("random-walk vault");
        let cache = tempfile::tempdir().expect("random-walk cache");
        let provider = std::sync::Arc::new(CensusProvider::new(vault.path().to_path_buf()));
        seed_small_vault(provider.as_ref(), seed);
        let session = open_census_session(provider.clone(), cache.path().join("incremental"));
        session
            .scan_initial(&CancelToken::new())
            .expect("initial random-walk scan");

        let mut rng = CensusRng::new(seed);
        let mut prefix = Vec::with_capacity(scale.operations);
        for step in 0..scale.operations {
            let operation =
                RANDOM_WALK_OPERATIONS[(step + seed_index) % RANDOM_WALK_OPERATIONS.len()];
            let applied = apply_operation(
                &session,
                provider.as_ref(),
                operation,
                seed,
                step,
                &prefix,
                &mut rng,
            );
            prefix.push(applied.description);
            assert_cold_parity(&session, vault.path(), seed, step, &prefix, &applied.path);
        }
    }
}

#[test]
fn census_file_meta_scan_parity() {
    let scale = census_scale();
    let seed = 0xf10_3202_6071_4ca5;
    let vault = tempfile::tempdir().expect("scan-parity vault");
    let cache = tempfile::tempdir().expect("scan-parity cache");
    let provider = std::sync::Arc::new(CensusProvider::new(vault.path().to_path_buf()));
    let session = open_census_session(provider.clone(), cache.path().join("incremental"));
    let mut prefix = Vec::with_capacity(scale.corpus_files);

    let checkpoint_interval = if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        64
    } else {
        16
    };
    for index in 0..scale.corpus_files {
        let (path, bytes) = corpus_case(index, seed);
        let attempted_source = String::from_utf8_lossy(&bytes);
        let completed_prefix = prefix.join("\n");
        provider
            .write_file(&path, &bytes)
            .unwrap_or_else(|error| {
                panic!(
                    "scan-parity seed={seed} step={index}\ncompleted prefix:\n{completed_prefix}\npath={path}\nattempted source={attempted_source:?}: {error}"
                )
            });
        let current_source = provider
            .read_file(&path)
            .map(|current| String::from_utf8_lossy(&current).into_owned())
            .unwrap_or_else(|error| format!("<current source unavailable: {error}>"));
        session
            .scan_initial(&CancelToken::new())
            .unwrap_or_else(|error| {
                panic!(
                    "scan-parity seed={seed} step={index}\ncompleted prefix:\n{completed_prefix}\npath={path}\nattempted source={attempted_source:?}\ncurrent source={current_source:?}: {error}"
                )
            });
        prefix.push(format!("step={index} create+scan path={path}"));
        if (index + 1).is_multiple_of(checkpoint_interval) || index + 1 == scale.corpus_files {
            assert_cold_parity(&session, vault.path(), seed, index, &prefix, &path);
        }
    }
}

#[test]
fn census_file_meta_exhaustive_operation_pairs() {
    let base_seed: u64 = 0xf10_3202_6071_e8a8;
    for (left_index, left) in PAIR_OPERATIONS.iter().copied().enumerate() {
        for (right_index, right) in PAIR_OPERATIONS.iter().copied().enumerate() {
            let pair_index = left_index * PAIR_OPERATIONS.len() + right_index;
            let seed = base_seed.wrapping_add(pair_index as u64);
            let vault = tempfile::tempdir().expect("operation-pair vault");
            let cache = tempfile::tempdir().expect("operation-pair cache");
            let provider = std::sync::Arc::new(CensusProvider::new(vault.path().to_path_buf()));
            seed_small_vault(provider.as_ref(), seed);
            let session = open_census_session(provider.clone(), cache.path().join("incremental"));
            session
                .scan_initial(&CancelToken::new())
                .expect("initial operation-pair scan");
            let mut rng = CensusRng::new(seed);
            let mut prefix = Vec::with_capacity(2);

            for (step, operation) in [left, right].into_iter().enumerate() {
                let pair_step = pair_index * 2 + step;
                let applied = apply_pair_operation(
                    &session,
                    provider.as_ref(),
                    operation,
                    seed,
                    pair_step,
                    &prefix,
                    &mut rng,
                );
                prefix.push(format!(
                    "pair={left_index},{right_index} {}",
                    applied.description
                ));
                assert_cold_parity(
                    &session,
                    vault.path(),
                    seed,
                    pair_step,
                    &prefix,
                    &applied.path,
                );
            }
        }
    }
}

#[derive(Debug, Clone, Copy)]
struct CensusScale {
    seeds: usize,
    operations: usize,
    corpus_files: usize,
}

fn census_scale() -> CensusScale {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        CensusScale {
            seeds: 24,
            operations: 64,
            corpus_files: 512,
        }
    } else {
        CensusScale {
            seeds: 4,
            operations: 16,
            corpus_files: 64,
        }
    }
}

#[derive(Debug, Clone, Copy)]
enum CensusOperation {
    Create,
    ExternalEditAndScan,
    CasSave,
    FrontmatterAdd,
    FrontmatterRemove,
    FrontmatterRetype,
    TaskEdit,
    Rename,
    Move,
    Delete,
}

#[derive(Debug, Clone, Copy)]
enum PairOperation {
    Create,
    ExternalEditAndScan,
    CasSave,
    Frontmatter,
    TaskEdit,
    Rename,
    Move,
    Delete,
}

#[derive(Debug)]
struct AppliedOperation {
    path: String,
    description: String,
}

fn apply_pair_operation(
    session: &VaultSession,
    provider: &CensusProvider,
    operation: PairOperation,
    seed: u64,
    step: usize,
    operation_prefix: &[String],
    rng: &mut CensusRng,
) -> AppliedOperation {
    let expanded = match operation {
        PairOperation::Create => CensusOperation::Create,
        PairOperation::ExternalEditAndScan => CensusOperation::ExternalEditAndScan,
        PairOperation::CasSave => CensusOperation::CasSave,
        PairOperation::Frontmatter => match step % 3 {
            0 => CensusOperation::FrontmatterAdd,
            1 => CensusOperation::FrontmatterRemove,
            _ => CensusOperation::FrontmatterRetype,
        },
        PairOperation::TaskEdit => CensusOperation::TaskEdit,
        PairOperation::Rename => CensusOperation::Rename,
        PairOperation::Move => CensusOperation::Move,
        PairOperation::Delete => CensusOperation::Delete,
    };
    apply_operation(
        session,
        provider,
        expanded,
        seed,
        step,
        operation_prefix,
        rng,
    )
}

fn apply_operation(
    session: &VaultSession,
    provider: &CensusProvider,
    operation: CensusOperation,
    seed: u64,
    step: usize,
    operation_prefix: &[String],
    rng: &mut CensusRng,
) -> AppliedOperation {
    let context = census_operation_context(seed, step, operation, operation_prefix);
    match operation {
        CensusOperation::Create => {
            let extension = if step.is_multiple_of(3) { "txt" } else { "md" };
            // Create under an already-indexed directory. `create_exclusive`
            // creates one file, not an implicit structural folder operation;
            // manufacturing a brand-new parent here would violate the
            // operation's precondition and conflate this census with dir-tree
            // reconciliation.
            let path = format!("notes/seed-{seed:016x}-step-{step:04}.{extension}");
            let source = if extension == "md" {
                format!(
                    "---\ntitle: Created {step}\ncreated: 2026-07-14\n---\n# Created\n\n- [ ] task {step}\n"
                )
            } else {
                format!("non-markdown create seed={seed} step={step}\n")
            };
            session
                .create_exclusive(&path, &source)
                .unwrap_or_else(|error| {
                    panic!("{context}\ncreate path={path} source={source:?}: {error}")
                });
            AppliedOperation {
                path: path.clone(),
                description: format!("step={step} create path={path} source={source:?}"),
            }
        }
        CensusOperation::ExternalEditAndScan => {
            let path = choose_path(session, false, rng, &context);
            let mut bytes = provider
                .read_file(&path)
                .unwrap_or_else(|error| panic!("{context}\nread path={path}: {error}"));
            bytes.extend_from_slice(
                format!(
                    "\nout-of-band seed={seed} step={step} {}\n",
                    "x".repeat(step % 17 + 1)
                )
                .as_bytes(),
            );
            provider.write_file(&path, &bytes).unwrap_or_else(|error| {
                panic!(
                    "{context}\nedit path={path} source={:?}: {error}",
                    String::from_utf8_lossy(&bytes)
                )
            });
            session
                .scan_initial(&CancelToken::new())
                .unwrap_or_else(|error| {
                    panic!(
                        "{context}\nrescan path={path} source={:?}: {error}",
                        String::from_utf8_lossy(&bytes)
                    )
                });
            AppliedOperation {
                path: path.clone(),
                description: format!(
                    "step={step} out-of-band-edit+scan path={path} source={:?}",
                    String::from_utf8_lossy(&bytes)
                ),
            }
        }
        CensusOperation::CasSave => {
            let path = choose_path(session, true, rng, &context);
            let old = session
                .read_text(&path)
                .unwrap_or_else(|error| panic!("{context}\nread path={path}: {error}"));
            let expected = crate::vault::content_hash(old.as_bytes());
            let source = format!("{old}\nCAS save seed={seed} step={step}\n");
            session
                .save_text(&path, &source, Some(&expected))
                .unwrap_or_else(|error| {
                    panic!("{context}\nsave path={path} source={source:?}: {error}")
                });
            AppliedOperation {
                path: path.clone(),
                description: format!("step={step} CAS-save path={path} source={source:?}"),
            }
        }
        CensusOperation::FrontmatterAdd => {
            let path = choose_path(session, true, rng, &context);
            let context = operation_path_context(&context, session, &path);
            let first = session
                .set_property(
                    &path,
                    "title",
                    crate::PropertyValue::Text(format!("Census title {seed}-{step}")),
                    None,
                )
                .unwrap_or_else(|error| panic!("{context}\nset title path={path}: {error}"));
            let context = operation_path_context(&context, session, &path);
            session
                .set_property(
                    &path,
                    "created",
                    crate::PropertyValue::Date(format!("2026-07-{:02}", step % 28 + 1)),
                    Some(&first.new_content_hash),
                )
                .unwrap_or_else(|error| panic!("{context}\nset created path={path}: {error}"));
            applied_with_current_source(session, step, path, "frontmatter-add", &context)
        }
        CensusOperation::FrontmatterRemove => {
            let path = choose_path(session, true, rng, &context);
            let context = operation_path_context(&context, session, &path);
            let prepared = session
                .set_property(
                    &path,
                    "title",
                    crate::PropertyValue::Text("remove me".to_string()),
                    None,
                )
                .unwrap_or_else(|error| panic!("{context}\nprepare title path={path}: {error}"));
            let context = operation_path_context(&context, session, &path);
            let prepared = session
                .set_property(
                    &path,
                    "created",
                    crate::PropertyValue::Date("2024-02-29".to_string()),
                    Some(&prepared.new_content_hash),
                )
                .unwrap_or_else(|error| panic!("{context}\nprepare created path={path}: {error}"));
            let context = operation_path_context(&context, session, &path);
            let removed = session
                .delete_property(&path, "title", Some(&prepared.new_content_hash))
                .unwrap_or_else(|error| panic!("{context}\nremove title path={path}: {error}"));
            let context = operation_path_context(&context, session, &path);
            session
                .delete_property(&path, "created", Some(&removed.new_content_hash))
                .unwrap_or_else(|error| panic!("{context}\nremove created path={path}: {error}"));
            applied_with_current_source(session, step, path, "frontmatter-remove", &context)
        }
        CensusOperation::FrontmatterRetype => {
            let path = choose_path(session, true, rng, &context);
            let context = operation_path_context(&context, session, &path);
            let first = session
                .set_property(
                    &path,
                    "title",
                    crate::PropertyValue::Integer(step as i64),
                    None,
                )
                .unwrap_or_else(|error| panic!("{context}\nretype title path={path}: {error}"));
            let context = operation_path_context(&context, session, &path);
            session
                .set_property(
                    &path,
                    "created",
                    crate::PropertyValue::List(vec![
                        crate::PropertyValue::Text("2026-07-14".to_string()),
                        crate::PropertyValue::Text("not-a-scalar".to_string()),
                    ]),
                    Some(&first.new_content_hash),
                )
                .unwrap_or_else(|error| panic!("{context}\nretype created path={path}: {error}"));
            applied_with_current_source(session, step, path, "frontmatter-retype", &context)
        }
        CensusOperation::TaskEdit => {
            let path = choose_path(session, true, rng, &context);
            let context = operation_path_context(&context, session, &path);
            let old = session
                .read_text(&path)
                .unwrap_or_else(|error| panic!("{context}\nread path={path}: {error}"));
            let expected = crate::vault::content_hash(old.as_bytes());
            let with_task = format!("{old}\n- [ ] census task seed={seed} step={step}\n");
            let saved = session
                .save_text(&path, &with_task, Some(&expected))
                .unwrap_or_else(|error| {
                    panic!("{context}\nadd task path={path} source={with_task:?}: {error}")
                });
            let context = operation_path_context(&context, session, &path);
            let ordinal = session
                .tasks_for_file(&path)
                .unwrap_or_else(|error| panic!("{context}\nlist tasks path={path}: {error}"))
                .last()
                .unwrap_or_else(|| panic!("{context}\nmissing added task path={path}"))
                .ordinal;
            session
                .toggle_task_status(&path, ordinal, 'x', Some(&saved.new_content_hash))
                .unwrap_or_else(|error| panic!("{context}\ntoggle task path={path}: {error}"));
            applied_with_current_source(session, step, path, "task-edit", &context)
        }
        CensusOperation::Rename => {
            let from = choose_path(session, false, rng, &context);
            let context = operation_path_context(&context, session, &from);
            let extension = Path::new(&from)
                .extension()
                .and_then(|value| value.to_str())
                .unwrap_or("md");
            let new_name = format!("renamed-{seed:016x}-{step:04}.{extension}");
            session
                .rename_file(&from, &new_name)
                .unwrap_or_else(|error| {
                    panic!("{context}\nrename from={from} to={new_name}: {error}")
                });
            let parent = Path::new(&from)
                .parent()
                .and_then(|value| value.to_str())
                .filter(|value| !value.is_empty());
            let path =
                parent.map_or_else(|| new_name.clone(), |parent| format!("{parent}/{new_name}"));
            applied_with_current_source(session, step, path, "rename", &context)
        }
        CensusOperation::Move => {
            let from = choose_path(session, false, rng, &context);
            let context = operation_path_context(&context, session, &from);
            let new_parent = format!("moved/seed-{seed:016x}/step-{step:04}");
            session.create_folder(&new_parent).unwrap_or_else(|error| {
                panic!("{context}\ncreate move folder path={new_parent}: {error}")
            });
            session
                .move_file(&from, &new_parent)
                .unwrap_or_else(|error| {
                    panic!("{context}\nmove from={from} to={new_parent}: {error}")
                });
            let path = format!(
                "{new_parent}/{}",
                Path::new(&from)
                    .file_name()
                    .and_then(|value| value.to_str())
                    .expect("chosen path has a filename")
            );
            applied_with_current_source(session, step, path, "move", &context)
        }
        CensusOperation::Delete => {
            let paths = indexed_paths(session, false, &context);
            assert!(
                paths.len() > 2,
                "{context}\ndelete precondition requires >2 files"
            );
            let path = paths[rng.bounded(paths.len())].clone();
            let source = provider
                .read_file(&path)
                .map(|bytes| String::from_utf8_lossy(&bytes).into_owned())
                .unwrap_or_else(|error| format!("<read failed: {error}>"));
            assert_single_file_meta_child(session, &path, &context, &source);
            session.delete_file(&path).unwrap_or_else(|error| {
                panic!("{context}\ndelete path={path} source={source:?}: {error}")
            });
            AppliedOperation {
                path: path.clone(),
                description: format!("step={step} delete path={path} source={source:?}"),
            }
        }
    }
}

fn applied_with_current_source(
    session: &VaultSession,
    step: usize,
    path: String,
    operation: &str,
    context: &str,
) -> AppliedOperation {
    let source = session
        .provider
        .read_file(&path)
        .map(|bytes| String::from_utf8_lossy(&bytes).into_owned())
        .unwrap_or_else(|error| panic!("{context}\nread path={path}: {error}"));
    AppliedOperation {
        path: path.clone(),
        description: format!("step={step} {operation} path={path} source={source:?}"),
    }
}

fn census_operation_context(
    seed: u64,
    step: usize,
    operation: CensusOperation,
    operation_prefix: &[String],
) -> String {
    format!(
        "seed={seed} step={step} operation={operation:?}\noperation prefix:\n{}",
        operation_prefix.join("\n")
    )
}

fn operation_path_context(context: &str, session: &VaultSession, path: &str) -> String {
    let source = session
        .provider
        .read_file(path)
        .map(|bytes| String::from_utf8_lossy(&bytes).into_owned())
        .unwrap_or_else(|error| format!("<unavailable before operation: {error}>"));
    format!("{context}\npath={path}\nsource={source:?}")
}

fn assert_single_file_meta_child(session: &VaultSession, path: &str, context: &str, source: &str) {
    let conn = session.conn.lock().expect("session connection mutex");
    let child_count = conn
        .query_row(
            "SELECT COUNT(fm.file_id)
             FROM files f
             LEFT JOIN file_meta fm ON fm.file_id = f.id
             WHERE f.path = ?1",
            [path],
            |row| row.get::<_, i64>(0),
        )
        .unwrap_or_else(|error| {
            panic!("{context}\nquery delete path={path} source={source:?}: {error}")
        });
    assert_eq!(
        child_count, 1,
        "{context}\ndelete must exercise exactly one file_meta cascade child path={path} source={source:?}"
    );
}

fn choose_path(
    session: &VaultSession,
    markdown_only: bool,
    rng: &mut CensusRng,
    context: &str,
) -> String {
    let paths = indexed_paths(session, markdown_only, context);
    assert!(
        !paths.is_empty(),
        "census path precondition markdown_only={markdown_only}\n{context}"
    );
    paths[rng.bounded(paths.len())].clone()
}

fn indexed_paths(session: &VaultSession, markdown_only: bool, context: &str) -> Vec<String> {
    let conn = session.conn.lock().expect("session connection mutex");
    let mut stmt = conn
        .prepare(if markdown_only {
            "SELECT path FROM files WHERE is_markdown = 1 ORDER BY path"
        } else {
            "SELECT path FROM files ORDER BY path"
        })
        .unwrap_or_else(|error| {
            panic!("prepare indexed paths markdown_only={markdown_only}: {error}\n{context}")
        });
    stmt.query_map([], |row| row.get::<_, String>(0))
        .unwrap_or_else(|error| {
            panic!("query indexed paths markdown_only={markdown_only}: {error}\n{context}")
        })
        .collect::<Result<Vec<_>, _>>()
        .unwrap_or_else(|error| {
            panic!("collect indexed paths markdown_only={markdown_only}: {error}\n{context}")
        })
}

fn seed_small_vault(provider: &CensusProvider, seed: u64) {
    let files = [
        (
            "notes/alpha.md",
            format!(
                "---\ntitle: Alpha {seed}\ncreated: 2024-02-29\n---\n# Alpha\n\nBody [[beta|Beta]].\n\n- [ ] open alpha\n"
            )
            .into_bytes(),
        ),
        (
            "notes/beta.md",
            b"# Beta\n\nParagraph with **markup** and `code`.\n".to_vec(),
        ),
        (
            "archive/gamma.markdown",
            b"---\ncreated: 2026-07-14T12:34:56-04:00\n---\n# Gamma\n\n- [x] done\n"
                .to_vec(),
        ),
        (
            "assets/blob.bin",
            vec![0, 159, 146, 150, b'\n', (seed & 0xff) as u8],
        ),
    ];
    for (path, bytes) in files {
        provider
            .write_file(path, &bytes)
            .unwrap_or_else(|error| panic!("seed={seed} seed file {path}: {error}"));
    }
}

fn corpus_case(index: usize, seed: u64) -> (String, Vec<u8>) {
    let bucket = index % 7;
    match index % 6 {
        0 => (
            format!("corpus/{bucket}/date-{index:04}.md"),
            format!(
                "---\ntitle: Corpus {index}\ncreated: 2024-02-29\n---\n# Date\n\nWords {seed} {index}.\n- [ ] open\n"
            )
            .into_bytes(),
        ),
        1 => (
            format!("corpus/{bucket}/datetime-{index:04}.markdown"),
            format!(
                "---\ncreated: 2026-07-14T12:34:{:02}+05:30\n---\n> Quote {index}\n\n```rust\nignored preview {index}\n```\n",
                index % 60
            )
            .into_bytes(),
        ),
        2 => (
            format!("corpus/{bucket}/invalid-{index:04}.mdown"),
            format!(
                "---\ntitle: [{index}, noisy]\ncreated: 2026-02-30\n---\n# Invalid\n\n[[target#anchor|Alias]] café 雪\n"
            )
            .into_bytes(),
        ),
        3 => (
            format!("corpus/{bucket}/plain-{index:04}.txt"),
            format!("plain non-markdown seed={seed} index={index}\n").into_bytes(),
        ),
        4 => (
            format!("corpus/{bucket}/binary-{index:04}.bin"),
            vec![0, 255, 1, 2, 3, (index & 0xff) as u8],
        ),
        _ => (
            format!("corpus/{bucket}/tasks-{index:04}.mkd"),
            format!(
                "# Tasks {index}\n\n- [ ] open\n- [x] done\n- [-] cancelled\n\n{}\n",
                "unicode\u{2003}whitespace ".repeat(index % 5 + 1)
            )
            .into_bytes(),
        ),
    }
}

fn open_census_session(
    provider: std::sync::Arc<CensusProvider>,
    cache_dir: PathBuf,
) -> VaultSession {
    VaultSession::open(provider, SessionConfig::new(cache_dir)).expect("open census session")
}

fn assert_cold_parity(
    incremental: &VaultSession,
    vault_root: &Path,
    seed: u64,
    step: usize,
    operation_prefix: &[String],
    focus_path: &str,
) {
    let focus_source = source_at(vault_root, focus_path);
    let initial_repro = format!(
        "seed={seed} step={step}\noperation prefix:\n{}\nfocus path={focus_path}\nsource:\n{focus_source}",
        operation_prefix.join("\n")
    );
    let cold_cache = tempfile::tempdir().expect("cold oracle cache");
    let cold_provider = std::sync::Arc::new(CensusProvider::new(vault_root.to_path_buf()));
    let cold = open_census_session(cold_provider, cold_cache.path().join("cold"));
    cold.scan_initial(&CancelToken::new())
        .unwrap_or_else(|error| panic!("cold scan failed: {error}\n{initial_repro}"));

    let incremental_direct = direct_meta_snapshot(incremental);
    let cold_direct = direct_meta_snapshot(&cold);
    assert_eq!(
        cold_direct.orphan_meta_rows, 0,
        "cold scan produced orphan file_meta rows\n{initial_repro}"
    );
    if let Some(row) = cold_direct.rows.iter().find(|row| row.meta.is_none()) {
        panic!(
            "cold scan omitted file_meta child path={} source={:?}\n{initial_repro}",
            row.path,
            source_at(vault_root, &row.path)
        );
    }
    let mismatch_path = first_direct_mismatch_path(&incremental_direct, &cold_direct);
    let repro = mismatch_path.map_or(initial_repro.clone(), |path| {
        format!(
            "{initial_repro}\nfirst mismatching path={path}\nfirst mismatch source:\n{}",
            source_at(vault_root, path)
        )
    });
    assert_eq!(
        incremental_direct, cold_direct,
        "direct files LEFT JOIN file_meta mismatch\n{repro}"
    );

    let incremental_files = list_files_snapshot(incremental, &initial_repro);
    let cold_files = list_files_snapshot(&cold, &initial_repro);
    let files_repro = first_file_pages_mismatch(&incremental_files, &cold_files).map_or_else(
        || initial_repro.clone(),
        |mismatch| mismatch_repro(&initial_repro, vault_root, &mismatch),
    );
    assert_eq!(
        incremental_files, cold_files,
        "paged list_files mismatch\n{files_repro}"
    );

    let incremental_tree = dir_tree_snapshot(incremental, &initial_repro);
    let cold_tree = dir_tree_snapshot(&cold, &initial_repro);
    let tree_repro = first_dir_tree_mismatch(&incremental_tree, &cold_tree).map_or_else(
        || initial_repro.clone(),
        |mismatch| mismatch_repro(&initial_repro, vault_root, &mismatch),
    );
    assert_eq!(
        incremental_tree, cold_tree,
        "recursively paged/sorted list_dir_children mismatch\n{tree_repro}"
    );
}

#[derive(Debug)]
struct SnapshotMismatch {
    context: String,
    path: Option<String>,
}

fn mismatch_repro(initial: &str, vault_root: &Path, mismatch: &SnapshotMismatch) -> String {
    match mismatch.path.as_deref() {
        Some(path) => format!(
            "{initial}\nfirst mismatch context={}\nfirst mismatching path={path}\nfirst mismatch source:\n{}",
            mismatch.context,
            source_at(vault_root, path)
        ),
        None => format!("{initial}\nfirst mismatch context={}", mismatch.context),
    }
}

fn source_at(vault_root: &Path, path: &str) -> String {
    std::fs::read(vault_root.join(path))
        .map(|bytes| String::from_utf8_lossy(&bytes).into_owned())
        .unwrap_or_else(|error| format!("<unavailable after operation: {error}>"))
}

fn first_direct_mismatch_path<'a>(
    incremental: &'a DirectSnapshot,
    cold: &'a DirectSnapshot,
) -> Option<&'a str> {
    for (incremental_row, cold_row) in incremental.rows.iter().zip(&cold.rows) {
        if incremental_row != cold_row {
            return Some(if incremental_row.path <= cold_row.path {
                &incremental_row.path
            } else {
                &cold_row.path
            });
        }
    }
    if incremental.rows.len() != cold.rows.len() {
        return incremental
            .rows
            .get(cold.rows.len())
            .or_else(|| cold.rows.get(incremental.rows.len()))
            .map(|row| row.path.as_str());
    }
    None
}

#[derive(Debug, PartialEq, Eq)]
struct DirectSnapshot {
    rows: Vec<DirectMetaRow>,
    orphan_meta_rows: i64,
}

#[derive(Debug, PartialEq, Eq)]
struct DirectMetaRow {
    path: String,
    is_markdown: bool,
    meta: Option<MetaProjection>,
}

#[derive(Debug, PartialEq, Eq)]
struct MetaProjection {
    word_count: i64,
    char_count: i64,
    preview: String,
}

fn direct_meta_snapshot(session: &VaultSession) -> DirectSnapshot {
    let conn = session.conn.lock().expect("session connection mutex");
    let rows = {
        let mut stmt = conn
            .prepare(
                "SELECT f.path, f.is_markdown, fm.file_id,
                        fm.word_count, fm.char_count, fm.preview
                 FROM files f
                 LEFT JOIN file_meta fm ON fm.file_id = f.id
                 ORDER BY f.path COLLATE BINARY",
            )
            .expect("prepare direct file_meta snapshot");
        stmt.query_map([], |row| {
            let file_meta_id = row.get::<_, Option<i64>>(2)?;
            let meta = if file_meta_id.is_some() {
                Some(MetaProjection {
                    word_count: row.get(3)?,
                    char_count: row.get(4)?,
                    preview: row.get(5)?,
                })
            } else {
                None
            };
            Ok(DirectMetaRow {
                path: row.get(0)?,
                is_markdown: row.get(1)?,
                meta,
            })
        })
        .expect("query direct file_meta snapshot")
        .collect::<Result<Vec<_>, _>>()
        .expect("collect direct file_meta snapshot")
    };
    let orphan_meta_rows = conn
        .query_row(
            "SELECT COUNT(*)
             FROM file_meta fm
             LEFT JOIN files f ON f.id = fm.file_id
             WHERE f.id IS NULL",
            [],
            |row| row.get(0),
        )
        .expect("count orphan file_meta rows");
    DirectSnapshot {
        rows,
        orphan_meta_rows,
    }
}

#[derive(Debug, PartialEq, Eq)]
struct FilterPages {
    name: &'static str,
    pages: Vec<FilePageSnapshot>,
}

#[derive(Debug, PartialEq, Eq)]
struct FilePageSnapshot {
    items: Vec<FileSummary>,
    next_cursor: Option<String>,
    total_filtered: u64,
}

fn list_files_snapshot(session: &VaultSession, context: &str) -> Vec<FilterPages> {
    [
        ("all", FileFilter::All),
        ("markdown", FileFilter::MarkdownOnly),
        ("markdown-canvas", FileFilter::MarkdownAndCanvas),
        ("openable", FileFilter::OpenableDocuments),
    ]
    .into_iter()
    .map(|(name, filter)| FilterPages {
        name,
        pages: drain_file_pages(name, context, |paging| session.list_files(filter, paging)),
    })
    .collect()
}

fn drain_file_pages(
    filter: &str,
    context: &str,
    mut fetch: impl FnMut(Paging) -> Result<Page<FileSummary>, VaultError>,
) -> Vec<FilePageSnapshot> {
    let mut pages = Vec::new();
    let mut cursor = None;
    loop {
        let paging = Paging {
            cursor: cursor.clone(),
            limit: 3,
        };
        let page_index = pages.len();
        let page = fetch(paging.clone()).unwrap_or_else(|error| {
            panic!(
                "fetch census file page filter={filter} page={page_index} cursor={:?}: {error}\n{context}",
                paging.cursor
            )
        });
        let next = page.next_cursor.clone();
        pages.push(FilePageSnapshot {
            items: page.items,
            next_cursor: next.clone(),
            total_filtered: page.total_filtered,
        });
        match next {
            Some(next) => cursor = Some(next),
            None => break,
        }
    }
    pages
}

fn first_file_pages_mismatch(
    incremental: &[FilterPages],
    cold: &[FilterPages],
) -> Option<SnapshotMismatch> {
    for (incremental_filter, cold_filter) in incremental.iter().zip(cold) {
        if incremental_filter.name != cold_filter.name {
            return Some(SnapshotMismatch {
                context: format!(
                    "filter identity incremental={} cold={}",
                    incremental_filter.name, cold_filter.name
                ),
                path: None,
            });
        }
        if let Some(mismatch) = first_file_page_slice_mismatch(
            &incremental_filter.pages,
            &cold_filter.pages,
            &format!("filter={}", incremental_filter.name),
        ) {
            return Some(mismatch);
        }
    }
    if incremental.len() != cold.len() {
        return Some(SnapshotMismatch {
            context: format!(
                "filter count incremental={} cold={}",
                incremental.len(),
                cold.len()
            ),
            path: None,
        });
    }
    None
}

fn first_file_page_slice_mismatch(
    incremental: &[FilePageSnapshot],
    cold: &[FilePageSnapshot],
    scope: &str,
) -> Option<SnapshotMismatch> {
    for (page_index, (incremental_page, cold_page)) in incremental.iter().zip(cold).enumerate() {
        let context = format!("{scope} page={page_index}");
        if incremental_page.total_filtered != cold_page.total_filtered
            || incremental_page.next_cursor != cold_page.next_cursor
        {
            return Some(SnapshotMismatch {
                context: format!(
                    "{context} totals/cursor incremental=({}, {:?}) cold=({}, {:?})",
                    incremental_page.total_filtered,
                    incremental_page.next_cursor,
                    cold_page.total_filtered,
                    cold_page.next_cursor
                ),
                path: incremental_page
                    .items
                    .first()
                    .or_else(|| cold_page.items.first())
                    .map(|item| item.path.clone()),
            });
        }
        for (incremental_item, cold_item) in incremental_page.items.iter().zip(&cold_page.items) {
            if incremental_item != cold_item {
                return Some(SnapshotMismatch {
                    context,
                    path: Some(if incremental_item.path <= cold_item.path {
                        incremental_item.path.clone()
                    } else {
                        cold_item.path.clone()
                    }),
                });
            }
        }
        if incremental_page.items.len() != cold_page.items.len() {
            return Some(SnapshotMismatch {
                context: format!(
                    "{context} item count incremental={} cold={}",
                    incremental_page.items.len(),
                    cold_page.items.len()
                ),
                path: incremental_page
                    .items
                    .get(cold_page.items.len())
                    .or_else(|| cold_page.items.get(incremental_page.items.len()))
                    .map(|item| item.path.clone()),
            });
        }
    }
    if incremental.len() != cold.len() {
        return Some(SnapshotMismatch {
            context: format!(
                "{scope} page count incremental={} cold={}",
                incremental.len(),
                cold.len()
            ),
            path: None,
        });
    }
    None
}

#[derive(Debug, PartialEq, Eq)]
struct DirLevelSnapshot {
    parent: String,
    pages: Vec<DirPageSnapshot>,
}

#[derive(Debug, PartialEq, Eq)]
struct DirPageSnapshot {
    dirs: Vec<DirProjection>,
    files: FilePageSnapshot,
}

#[derive(Debug, PartialEq, Eq)]
struct DirProjection {
    path: String,
    name: String,
    child_dir_count: u32,
    child_file_count: u32,
}

fn dir_tree_snapshot(session: &VaultSession, context: &str) -> Vec<DirLevelSnapshot> {
    let mut queue = VecDeque::from([String::new()]);
    let mut levels = Vec::new();
    while let Some(parent) = queue.pop_front() {
        let mut cursor = None;
        let mut pages = Vec::new();
        loop {
            let listing = session
                .list_dir_children(
                    &parent,
                    Paging {
                        cursor: cursor.clone(),
                        limit: 2,
                    },
                )
                .unwrap_or_else(|error| {
                    panic!(
                        "list_dir_children parent={parent:?} page={} cursor={cursor:?}: {error}\n{context}",
                        pages.len()
                    )
                });
            let dirs = listing
                .dirs
                .into_iter()
                .map(|dir| DirProjection {
                    path: dir.path,
                    name: dir.name,
                    child_dir_count: dir.child_dir_count,
                    child_file_count: dir.child_file_count,
                })
                .collect::<Vec<_>>();
            if pages.is_empty() {
                queue.extend(dirs.iter().map(|dir| dir.path.clone()));
            }
            let next = listing.files.next_cursor.clone();
            pages.push(DirPageSnapshot {
                dirs,
                files: FilePageSnapshot {
                    items: listing.files.items,
                    next_cursor: next.clone(),
                    total_filtered: listing.files.total_filtered,
                },
            });
            match next {
                Some(next) => cursor = Some(next),
                None => break,
            }
        }
        levels.push(DirLevelSnapshot { parent, pages });
    }
    levels
}

fn first_dir_tree_mismatch(
    incremental: &[DirLevelSnapshot],
    cold: &[DirLevelSnapshot],
) -> Option<SnapshotMismatch> {
    for (level_index, (incremental_level, cold_level)) in incremental.iter().zip(cold).enumerate() {
        if incremental_level.parent != cold_level.parent {
            return Some(SnapshotMismatch {
                context: format!(
                    "tree level={level_index} parent incremental={:?} cold={:?}",
                    incremental_level.parent, cold_level.parent
                ),
                path: None,
            });
        }
        for (page_index, (incremental_page, cold_page)) in incremental_level
            .pages
            .iter()
            .zip(&cold_level.pages)
            .enumerate()
        {
            let scope = format!(
                "tree parent={:?} page={page_index}",
                incremental_level.parent
            );
            for (incremental_dir, cold_dir) in incremental_page.dirs.iter().zip(&cold_page.dirs) {
                if incremental_dir != cold_dir {
                    return Some(SnapshotMismatch {
                        context: format!("{scope} directory projection"),
                        path: Some(if incremental_dir.path <= cold_dir.path {
                            incremental_dir.path.clone()
                        } else {
                            cold_dir.path.clone()
                        }),
                    });
                }
            }
            if incremental_page.dirs.len() != cold_page.dirs.len() {
                return Some(SnapshotMismatch {
                    context: format!(
                        "{scope} directory count incremental={} cold={}",
                        incremental_page.dirs.len(),
                        cold_page.dirs.len()
                    ),
                    path: incremental_page
                        .dirs
                        .get(cold_page.dirs.len())
                        .or_else(|| cold_page.dirs.get(incremental_page.dirs.len()))
                        .map(|dir| dir.path.clone()),
                });
            }
            if let Some(mismatch) = first_file_page_slice_mismatch(
                std::slice::from_ref(&incremental_page.files),
                std::slice::from_ref(&cold_page.files),
                &scope,
            ) {
                return Some(mismatch);
            }
        }
        if incremental_level.pages.len() != cold_level.pages.len() {
            return Some(SnapshotMismatch {
                context: format!(
                    "tree parent={:?} page count incremental={} cold={}",
                    incremental_level.parent,
                    incremental_level.pages.len(),
                    cold_level.pages.len()
                ),
                path: Some(incremental_level.parent.clone()),
            });
        }
    }
    if incremental.len() != cold.len() {
        return Some(SnapshotMismatch {
            context: format!(
                "tree level count incremental={} cold={}",
                incremental.len(),
                cold.len()
            ),
            path: None,
        });
    }
    None
}

#[derive(Debug)]
struct CensusProvider {
    root: PathBuf,
    inner: FsVaultProvider,
}

impl CensusProvider {
    fn new(root: PathBuf) -> Self {
        Self {
            inner: FsVaultProvider::new(root.clone()),
            root,
        }
    }

    fn deletion_path(&self, relative: &str) -> Result<PathBuf, VaultError> {
        let path = Path::new(relative);
        if path.is_absolute()
            || path
                .components()
                .any(|component| !matches!(component, Component::Normal(_)))
        {
            return Err(VaultError::InvalidPath {
                path: relative.to_string(),
                reason: "census deletion requires a non-root normal relative path".to_string(),
            });
        }
        Ok(self.root.join(path))
    }
}

impl VaultProvider for CensusProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }

    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }

    fn read_file_with_cap(&self, relative: &str, max_bytes: u64) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file_with_cap(relative, max_bytes)
    }

    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }

    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        let path = self.deletion_path(relative)?;
        let metadata = std::fs::symlink_metadata(&path).map_err(VaultError::Io)?;
        if metadata.is_dir() {
            std::fs::remove_dir_all(path).map_err(VaultError::Io)
        } else {
            std::fs::remove_file(path).map_err(VaultError::Io)
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
        _sink: std::sync::Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        Ok(None)
    }
}

#[derive(Debug)]
struct CensusRng(u64);

impl CensusRng {
    fn new(seed: u64) -> Self {
        Self(seed.max(1))
    }

    fn next(&mut self) -> u64 {
        let mut value = self.0;
        value ^= value << 13;
        value ^= value >> 7;
        value ^= value << 17;
        self.0 = value;
        value
    }

    fn bounded(&mut self, upper: usize) -> usize {
        (self.next() as usize) % upper
    }
}
