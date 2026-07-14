// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — scanning, rescanning, cancellation, ctime, headings, progress events.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;
use std::collections::BTreeMap;
use std::path::Path;
use std::time::SystemTime;

#[test]
fn open_creates_cache_database() {
    let tmp = tempfile::tempdir().unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let cache_db = tmp.path().join(".slate").join("cache.sqlite");
    assert!(cache_db.exists(), "cache.sqlite should be created on open");
    drop(session);
}

#[test]
fn open_is_idempotent() {
    let tmp = tempfile::tempdir().unwrap();
    let _s1 = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    drop(_s1);
    let _s2 = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    // Should not panic or fail — schema is at v1 and stays there.
}

#[test]
fn scan_initial_propagates_database_contention_instead_of_returning_partial_index() {
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider.write_file("note.md", b"# Note\n").unwrap();

    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session
        .conn
        .lock()
        .unwrap()
        .busy_timeout(std::time::Duration::from_millis(10))
        .unwrap();

    let cache_db = tmp.path().join(".slate").join("cache.sqlite");
    let blocker = crate::db::open_database(&cache_db, 512).unwrap();
    blocker.execute_batch("BEGIN IMMEDIATE").unwrap();

    let result = session.scan_initial(&CancelToken::new());
    assert!(
        matches!(result, Err(VaultError::Db(_))),
        "database contention must abort the scan instead of exposing a partial index: {result:?}"
    );

    blocker.execute_batch("ROLLBACK").unwrap();
}

#[test]
fn migration_026_reindexes_typed_lists_when_file_mtime_is_the_epoch() {
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("Note.md", b"---\ndates: [2026-01-02]\n---\n# Epoch mtime\n")
        .unwrap();
    std::fs::File::open(tmp.path().join("Note.md"))
        .unwrap()
        .set_modified(SystemTime::UNIX_EPOCH)
        .unwrap();

    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let first = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(first.files_indexed, 1);
    {
        let conn = session.conn.lock().unwrap();
        let cached_mtime: i64 = conn
            .query_row(
                "SELECT mtime_ms FROM files WHERE path = 'Note.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(
            cached_mtime, 0,
            "the regression requires a real epoch-mtime file"
        );
    }
    drop(session);

    let db_path = tmp.path().join(".slate").join("cache.sqlite");
    let conn = crate::db::open_database(&db_path, 512).unwrap();
    conn.execute(
        "UPDATE properties SET value_text = ?1, value_text_norm = '' WHERE key = 'dates'",
        rusqlite::params![r#"["2026-01-02"]"#],
    )
    .unwrap();
    // Unwind every post-025 migration (their DDL isn't re-runnable),
    // so the pre-026 fixture can replay 026+ from scratch: 027's
    // index + column and 028's open_marks table.
    conn.execute("DELETE FROM schema_version WHERE version >= 26", [])
        .unwrap();
    conn.execute("DROP INDEX files_oplog_name_unique", [])
        .unwrap();
    conn.execute("ALTER TABLE files DROP COLUMN oplog_name", [])
        .unwrap();
    conn.execute("DROP TABLE open_marks", []).unwrap();
    conn.execute("DROP TABLE oplog_events", []).unwrap();
    conn.execute("DROP TABLE oplog_events_stale", []).unwrap();
    conn.execute("DROP INDEX idx_files_birthtime", []).unwrap();
    conn.execute("ALTER TABLE files DROP COLUMN birthtime_ms", [])
        .unwrap();
    conn.execute("DROP TABLE file_meta", []).unwrap();
    let version: i64 = conn
        .query_row("SELECT MAX(version) FROM schema_version", [], |row| {
            row.get(0)
        })
        .unwrap();
    assert_eq!(
        version, 25,
        "fixture must represent the pre-migration cache"
    );
    drop(conn);

    let upgraded = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let report = upgraded.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        report.files_indexed, 1,
        "migration 026 must force a slow path"
    );
    assert_eq!(report.files_skipped, 0);

    let conn = upgraded.conn.lock().unwrap();
    let encoded: String = conn
        .query_row(
            "SELECT value_text FROM properties WHERE key = 'dates'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    let encoded: serde_json::Value = serde_json::from_str(&encoded).unwrap();
    let decoded = crate::properties_db::tagged_list_element_to_property_value(&encoded[0]);
    assert_eq!(
        decoded,
        Some(crate::frontmatter::PropertyValue::Date(
            "2026-01-02".to_string()
        )),
        "the forced scan must rewrite the erased list element type"
    );
}

#[test]
fn migration_031_backfills_epoch_mtime_file_instead_of_taking_fast_path() {
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let source = b"# Epoch note\n\nbody";
    provider.write_file("epoch.md", source).unwrap();
    std::fs::File::open(tmp.path().join("epoch.md"))
        .unwrap()
        .set_modified(SystemTime::UNIX_EPOCH)
        .unwrap();
    let stat = provider.stat("epoch.md").unwrap();
    assert_eq!(stat.mtime_ms, 0, "fixture requires an epoch-mtime file");

    let cache_dir = tmp.path().join(".slate");
    std::fs::create_dir_all(&cache_dir).unwrap();
    let mut conn = crate::db::open_database(&cache_dir.join("cache.sqlite"), 512).unwrap();
    crate::db::migrate_up_to(&mut conn, 30).unwrap();
    conn.execute(
        "INSERT INTO files
          (path, name, extension, size_bytes, mtime_ms, ctime_ms, birthtime_ms,
           content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
         VALUES ('epoch.md', 'epoch.md', 'md', ?1, 0, 0, ?2, ?3, 1, 1, 1, ?4)",
        rusqlite::params![
            stat.size_bytes as i64,
            stat.birthtime_ms,
            content_hash(source),
            String::from_utf8_lossy(source).as_ref(),
        ],
    )
    .unwrap();
    drop(conn);

    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        report.files_indexed, 1,
        "missing file_meta must force slow path"
    );
    assert_eq!(report.files_skipped, 0);

    let conn = session.conn.lock().unwrap();
    let row: (i64, i64, String) = conn
        .query_row(
            "SELECT fm.word_count, fm.char_count, fm.preview
             FROM file_meta fm
             JOIN files f ON f.id = fm.file_id
             WHERE f.path = 'epoch.md'",
            [],
            |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
        )
        .unwrap();
    assert_eq!(row, (4, 18, "Epoch note body".to_string()));
}

#[test]
fn scan_initial_indexes_markdown_and_non_markdown() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/a.md", b"# A").unwrap();
        p.write_file("notes/b.md", b"# B").unwrap();
        p.write_file("attachments/img.png", b"\x89PNG").unwrap();
        p.write_file("README.txt", b"hi").unwrap();
    });

    let cancel = CancelToken::new();
    let report = session.scan_initial(&cancel).unwrap();
    assert_eq!(report.files_indexed, 4);
    assert_eq!(report.errors.len(), 0);
    assert!(report.bytes_processed > 0);
}

#[test]
fn scan_slow_path_derives_file_meta_from_markdown_body() {
    let source = "---\ntitle: Ignored\n---\n# Heading\n\nHello [[Note#anchor|friend]] and [site](https://example.com).\n\n```rust\nhidden code\n```\n\n<div>\nhidden html\n</div>\n\n- [x] Done *now*.\n";
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("note.md", source.as_bytes()).unwrap();
    });

    session.scan_initial(&CancelToken::new()).unwrap();

    let body = crate::frontmatter::body_after_frontmatter(source);
    let conn = session.conn.lock().unwrap();
    let row: (i64, i64, String) = conn
        .query_row(
            "SELECT fm.word_count, fm.char_count, fm.preview
             FROM file_meta fm
             JOIN files f ON f.id = fm.file_id
             WHERE f.path = 'note.md'",
            [],
            |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
        )
        .unwrap();

    assert_eq!(row.0, body.split_whitespace().count() as i64);
    assert_eq!(row.1, body.chars().count() as i64);
    assert_eq!(row.2, "Heading Hello friend and site. Done now.");
}

#[test]
fn save_path_replaces_file_meta_in_the_save_transaction() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("note.md", b"one two").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .save_text("note.md", "# Updated\n\nthree four five", None)
        .unwrap();

    let conn = session.conn.lock().unwrap();
    let row: (i64, i64, String) = conn
        .query_row(
            "SELECT fm.word_count, fm.char_count, fm.preview
             FROM file_meta fm
             JOIN files f ON f.id = fm.file_id
             WHERE f.path = 'note.md'",
            [],
            |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
        )
        .unwrap();
    assert_eq!(row, (5, 26, "Updated three four five".to_string()));
}

#[test]
fn non_markdown_file_meta_is_empty_and_file_delete_cascades() {
    let (_tmp, session) = make_vault(|provider| {
        provider.write_file("note.md", b"body").unwrap();
        provider
            .write_file("attachment.txt", b"not markdown metadata")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let note_id: i64;
    {
        let conn = session.conn.lock().unwrap();
        let non_markdown: (i64, i64, String) = conn
            .query_row(
                "SELECT fm.word_count, fm.char_count, fm.preview
                 FROM file_meta fm
                 JOIN files f ON f.id = fm.file_id
                 WHERE f.path = 'attachment.txt'",
                [],
                |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
            )
            .unwrap();
        assert_eq!(non_markdown, (0, 0, String::new()));
        note_id = conn
            .query_row("SELECT id FROM files WHERE path = 'note.md'", [], |row| {
                row.get(0)
            })
            .unwrap();
    }

    session.delete_file("note.md").unwrap();

    let conn = session.conn.lock().unwrap();
    let remaining: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM file_meta WHERE file_id = ?1",
            [note_id],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(remaining, 0, "deleting files row must cascade file_meta");
}

#[test]
fn scan_and_save_rebuild_dql_inline_fields_without_changing_frontmatter_properties() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "note.md",
            b"---\nfront: keep\n---\npage:: 1\n\n- list:: 2\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    {
        let conn = session.conn.lock().unwrap();
        let keys: Vec<String> = conn
            .prepare(
                "SELECT i.key FROM dql_inline_fields i
                 JOIN files f ON f.id = i.file_id
                 WHERE f.path = 'note.md' ORDER BY i.ordinal",
            )
            .unwrap()
            .query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap();
        assert_eq!(keys, ["list", "page"]);
        let property_keys: Vec<String> = conn
            .prepare(
                "SELECT p.key FROM properties p JOIN files f ON f.id = p.file_id
                 WHERE f.path = 'note.md' ORDER BY p.ordinal",
            )
            .unwrap()
            .query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<_, _>>()
            .unwrap();
        assert_eq!(property_keys, ["front"]);
    }

    session.save_text("note.md", "updated:: 3\n", None).unwrap();
    let conn = session.conn.lock().unwrap();
    let keys: Vec<String> = conn
        .prepare(
            "SELECT i.key FROM dql_inline_fields i
             JOIN files f ON f.id = i.file_id
             WHERE f.path = 'note.md' ORDER BY i.ordinal",
        )
        .unwrap()
        .query_map([], |row| row.get(0))
        .unwrap()
        .collect::<Result<_, _>>()
        .unwrap();
    assert_eq!(keys, ["updated"]);
}

#[test]
fn scan_indexes_base_files_without_markdown_derivatives() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "Reading.base",
            br#"filters: "file.hasTag(\"reading\")"
views:
  - type: table
    name: "Reading"
"#,
        )
        .unwrap();
        p.write_file("Broken.base", b"- not\n- a mapping\n")
            .unwrap();
    });

    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.files_indexed, 2);

    let conn = session.conn.lock().unwrap();
    let bases: Vec<(String, String, i64, i64, String)> = {
        let mut stmt = conn
            .prepare(
                "SELECT f.path, bf.name, f.is_markdown, bf.warning_count, bf.parsed_query_json
                 FROM files f
                 JOIN bases_files bf ON bf.file_id = f.id
                 ORDER BY f.path",
            )
            .unwrap();
        stmt.query_map([], |row| {
            Ok((
                row.get::<_, String>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, i64>(2)?,
                row.get::<_, i64>(3)?,
                row.get::<_, String>(4)?,
            ))
        })
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap()
    };

    assert_eq!(bases.len(), 2);
    assert_eq!(bases[0].0, "Broken.base");
    assert_eq!(bases[0].1, "Broken");
    assert_eq!(bases[0].2, 0, ".base files are not markdown");
    assert!(bases[0].3 > 0, "degraded parse still rows with warnings");
    assert!(bases[0].4.contains("\"degraded\":true"));
    assert_eq!(bases[1].0, "Reading.base");
    assert_eq!(bases[1].1, "Reading");
    assert!(bases[1].4.contains("\"name\":\"Reading\""));
    drop(conn);
    assert_eq!(fts_match_count(&session, "reading"), 0);
}

#[test]
fn scan_discovers_base_slate_query_and_dataview_fences_only() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "note.md",
            b"# Note\n\n```base\nviews:\n  - type: table\n```\n\n```slate-query\nTABLE file.name\n```\n\n```dataview\nTABLE file.name\n```\n\n```dataviewjs\nconsole.log('nope')\n```\n",
        )
        .unwrap();
    });

    session.scan_initial(&CancelToken::new()).unwrap();

    let conn = session.conn.lock().unwrap();
    let rows: Vec<(i64, String, i64, i64)> = {
        let mut stmt = conn
            .prepare(
                "SELECT fence_kind, source_text, line, byte_offset
                 FROM bases_blocks
                 ORDER BY byte_offset",
            )
            .unwrap();
        stmt.query_map([], |row| {
            Ok((
                row.get::<_, i64>(0)?,
                row.get::<_, String>(1)?,
                row.get::<_, i64>(2)?,
                row.get::<_, i64>(3)?,
            ))
        })
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap()
    };

    assert_eq!(rows.len(), 3);
    assert_eq!(rows.iter().map(|row| row.0).collect::<Vec<_>>(), [0, 1, 2]);
    assert!(rows[0].1.starts_with("views:\n"));
    assert_eq!(rows[0].2, 3);
    assert_eq!(rows[0].3, 8);
    assert!(rows[1].1.contains("TABLE file.name"));
    assert!(rows[2].1.contains("TABLE file.name"));
}

#[test]
fn indexed_base_fence_body_is_original_bytes() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"```base\r\nviews: []\r\n```\r\n")
            .unwrap();
    });

    session.scan_initial(&CancelToken::new()).unwrap();

    let conn = session.conn.lock().unwrap();
    let source_text: String = conn
        .query_row("SELECT source_text FROM bases_blocks", [], |row| row.get(0))
        .unwrap();
    assert_eq!(source_text.as_bytes(), b"views: []\r\n");
}

#[test]
fn census_bases_scan_incremental() {
    const SEED: u64 = 0x5ca1_2026_0709_0001;

    let full = std::env::var("SLATE_CENSUS_FULL").is_ok_and(|value| value == "1");
    let file_count = if full { 10_000 } else { 256 };
    let mutation_count = if full { 1_000 } else { 64 };
    let checkpoint_stride = if full { 100 } else { 8 };
    let tmp = tempfile::tempdir()
        .unwrap_or_else(|error| panic!("scanner census seed={SEED}: create vault: {error}"));
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let mut files = BTreeMap::new();

    for case_index in 0..file_count {
        let generated = scanner_census_case(case_index, SEED);
        provider
            .write_file(&generated.path, generated.content.as_bytes())
            .unwrap_or_else(|error| {
                panic!(
                    "scanner census seed={SEED} case={case_index} write failed: {error}\npath={}\nsource:\n{}",
                    generated.path, generated.content
                )
            });
        let replaced = files.insert(generated.path.clone(), generated);
        assert!(
            replaced.is_none(),
            "scanner census seed={SEED} case={case_index}: duplicate generated path\npath={}\nsource:\n{}",
            replaced
                .as_ref()
                .map_or("<missing>", |file| file.path.as_str()),
            replaced
                .as_ref()
                .map_or("<missing>", |file| file.content.as_str()),
        );
    }

    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap_or_else(|error| {
        panic!("scanner census seed={SEED}: open incremental session: {error}")
    });
    scan_census_checkpoint(
        &session,
        tmp.path(),
        &files,
        SEED,
        &format!("initial files={file_count}"),
        &[],
    );

    let mut rng = ScannerCensusRng::new(SEED);
    let mut operation_prefix = Vec::with_capacity(mutation_count);
    for mutation_index in 0..mutation_count {
        let operation = apply_scanner_census_mutation(
            &provider,
            tmp.path(),
            &mut files,
            &mut rng,
            SEED,
            file_count,
            mutation_index,
            &operation_prefix,
        );
        operation_prefix.push(operation);

        if (mutation_index + 1) % checkpoint_stride == 0 || mutation_index + 1 == mutation_count {
            scan_census_checkpoint(
                &session,
                tmp.path(),
                &files,
                SEED,
                &format!("after mutation {mutation_index}"),
                &operation_prefix,
            );
        }
    }
}

#[derive(Clone)]
struct ScannerCensusFile {
    case_index: usize,
    path: String,
    content: String,
}

fn scanner_census_case(case_index: usize, seed: u64) -> ScannerCensusFile {
    let bucket = (case_index.wrapping_mul(37).wrapping_add(seed as usize)) % 31;
    let stem = format!("case-{case_index:05}");
    let (extension, content) = match case_index % 8 {
        0 => (
            "base",
            format!(
                "filters: 'file.extension == \"md\"'\nviews:\n  - type: table\n    name: Generated {case_index}\n    order:\n      - file.name\n"
            ),
        ),
        1 => (
            "base",
            format!(
                "filters:\n  and:\n    - 'file.hasTag(\"census\")'\n    - 'file.name != \"\"'\nviews:\n  - type: cards\n    name: Plugin {case_index}\n    plugin-option: {case_index}\nunknown-root-{case_index}: true\n"
            ),
        ),
        2 => (
            "md",
            format!(
                "---\nkind: base-fence\nordinal: {case_index}\ntags: [census, base]\n---\n# Base fence {case_index}\n\n- [ ] inspect generated base {case_index}\n\n```base\nfilters: 'file.name != \"\"'\nviews:\n  - type: table\n    name: Embedded {case_index}\n```\n\n[[case-00000]] #census\n"
            ),
        ),
        3 => (
            "md",
            format!(
                "# Slate query {case_index}\n\n```slate-query\nTABLE file.name, file.mtime\nWHERE file.name != \"\"\n```\n\nParagraph {case_index} ^block-{case_index}\n"
            ),
        ),
        4 => (
            "md",
            format!(
                "# Dataview {case_index}\n\n```dataview\nTABLE file.name\nFROM #census\n```\n\n[external {case_index}](https://example.com/{case_index})\n"
            ),
        ),
        5 => (
            "md",
            format!(
                "# Ignored fences {case_index}\n\n```dataviewjs\ndv.paragraph(\"ignored {case_index}\")\n```\n\n```javascript\nconst ignored = {case_index};\n```\n"
            ),
        ),
        6 => (
            "md",
            format!(
                "---\ntitle: Ordinary {case_index}\npublished: {}\nscore: {}\ntags:\n  - census\n  - ordinary\n---\n# Ordinary {case_index}\n\nBody token-{case_index} with [[case-00002|a link]].\n\n- [{}] task {case_index} 📅 2026-07-09\n",
                case_index.is_multiple_of(2),
                case_index % 97,
                if case_index.is_multiple_of(3) {
                    "x"
                } else {
                    " "
                },
            ),
        ),
        _ => (
            "md",
            format!(
                "# Ordinary with ignored query {case_index}\n\n```query\ntag:#census\n```\n\nNo supported fence here; unicode café 雪 {case_index}.\n"
            ),
        ),
    };

    ScannerCensusFile {
        case_index,
        path: format!("generated/bucket-{bucket:02}/{stem}.{extension}"),
        content,
    }
}

// The explicit inputs keep every mutation and its reproduction context visible
// at the call site; bundling them into mutable test state would obscure failures.
#[allow(clippy::too_many_arguments)]
fn apply_scanner_census_mutation(
    provider: &FsVaultProvider,
    vault_root: &Path,
    files: &mut BTreeMap<String, ScannerCensusFile>,
    rng: &mut ScannerCensusRng,
    seed: u64,
    initial_file_count: usize,
    mutation_index: usize,
    operation_prefix: &[String],
) -> String {
    let prior_operations = || scanner_census_operation_prefix(operation_prefix);

    match mutation_index % 5 {
        0 => {
            let case_index = initial_file_count + mutation_index;
            let mut generated = scanner_census_case(case_index, seed);
            generated.path = format!(
                "generated/created/created-{mutation_index:04}.{}",
                generated.path.rsplit_once('.').map_or("md", |(_, ext)| ext)
            );
            provider
                .write_file(&generated.path, generated.content.as_bytes())
                .unwrap_or_else(|error| {
                    panic!(
                        "scanner census seed={seed} mutation={mutation_index} create failed: {error}\npath={}\nsource:\n{}\noperation prefix:\n{}",
                        generated.path,
                        generated.content,
                        prior_operations(),
                    )
                });
            assert!(
                files
                    .insert(generated.path.clone(), generated.clone())
                    .is_none(),
                "scanner census seed={seed} mutation={mutation_index}: create collided at {}\noperation prefix:\n{}",
                generated.path,
                prior_operations(),
            );
            format!(
                "mutation={mutation_index} create case={case_index} path={} source={:?}",
                generated.path, generated.content
            )
        }
        1 => {
            let path = scanner_census_existing_path(
                files,
                rng,
                seed,
                mutation_index,
                operation_prefix,
                |_| true,
            );
            let file = files.get_mut(&path).unwrap_or_else(|| {
                panic!(
                    "scanner census seed={seed} mutation={mutation_index}: selected edit path vanished: {path}\noperation prefix:\n{}",
                    prior_operations(),
                )
            });
            let previous = file.content.clone();
            file.content.push_str(&format!(
                "\n<!-- scanner-census edit seed={seed} mutation={mutation_index} {} -->\n",
                "x".repeat(1 + mutation_index % 23)
            ));
            provider
                .write_file(&path, file.content.as_bytes())
                .unwrap_or_else(|error| {
                    panic!(
                        "scanner census seed={seed} mutation={mutation_index} edit failed: {error}\npath={path}\nold source:\n{previous}\nnew source:\n{}\noperation prefix:\n{}",
                        file.content,
                        prior_operations(),
                    )
                });
            format!(
                "mutation={mutation_index} edit case={} path={path} old={previous:?} new={:?}",
                file.case_index, file.content
            )
        }
        2 => {
            let from = scanner_census_existing_path(
                files,
                rng,
                seed,
                mutation_index,
                operation_prefix,
                |_| true,
            );
            let mut file = files.remove(&from).unwrap_or_else(|| {
                panic!(
                    "scanner census seed={seed} mutation={mutation_index}: selected rename path vanished: {from}\noperation prefix:\n{}",
                    prior_operations(),
                )
            });
            let extension = from.rsplit_once('.').map_or("md", |(_, ext)| ext);
            let to = format!("generated/renamed/rename-{mutation_index:04}.{extension}");
            provider.rename(&from, &to).unwrap_or_else(|error| {
                panic!(
                    "scanner census seed={seed} mutation={mutation_index} rename failed: {error}\nfrom={from}\nto={to}\nsource:\n{}\noperation prefix:\n{}",
                    file.content,
                    prior_operations(),
                )
            });
            file.path.clone_from(&to);
            assert!(
                files.insert(to.clone(), file.clone()).is_none(),
                "scanner census seed={seed} mutation={mutation_index}: rename collided at {to}\noperation prefix:\n{}",
                prior_operations(),
            );
            format!(
                "mutation={mutation_index} rename case={} from={from} to={to} source={:?}",
                file.case_index, file.content
            )
        }
        3 => {
            let path = scanner_census_existing_path(
                files,
                rng,
                seed,
                mutation_index,
                operation_prefix,
                |_| true,
            );
            let file = files.remove(&path).unwrap_or_else(|| {
                panic!(
                    "scanner census seed={seed} mutation={mutation_index}: selected delete path vanished: {path}\noperation prefix:\n{}",
                    prior_operations(),
                )
            });
            std::fs::remove_file(vault_root.join(&path)).unwrap_or_else(|error| {
                panic!(
                    "scanner census seed={seed} mutation={mutation_index} delete failed: {error}\npath={path}\nsource:\n{}\noperation prefix:\n{}",
                    file.content,
                    prior_operations(),
                )
            });
            format!(
                "mutation={mutation_index} delete case={} path={path} source={:?}",
                file.case_index, file.content
            )
        }
        _ => {
            let convert_to_supported = (mutation_index / 5).is_multiple_of(2);
            let path = scanner_census_existing_path(
                files,
                rng,
                seed,
                mutation_index,
                operation_prefix,
                |file| {
                    file.path.ends_with(".md")
                        && scanner_census_has_supported_fence(&file.content) != convert_to_supported
                },
            );
            let file = files.get_mut(&path).unwrap_or_else(|| {
                panic!(
                    "scanner census seed={seed} mutation={mutation_index}: selected fence path vanished: {path}\noperation prefix:\n{}",
                    prior_operations(),
                )
            });
            let previous = file.content.clone();
            file.content = if convert_to_supported {
                format!(
                    "# Fence conversion {mutation_index}\n\n```base\nfilters: 'file.name != \"\"'\nviews:\n  - type: list\n    name: Converted {mutation_index}\n```\n\n```dataviewjs\nignored after conversion {mutation_index}\n```\n"
                )
            } else {
                format!(
                    "# Fence conversion {mutation_index}\n\n```dataviewjs\ndv.paragraph(\"ignored {mutation_index}\")\n```\n\n```query\ntag:#ignored-{mutation_index}\n```\n"
                )
            };
            provider
                .write_file(&path, file.content.as_bytes())
                .unwrap_or_else(|error| {
                    panic!(
                        "scanner census seed={seed} mutation={mutation_index} fence conversion failed: {error}\npath={path}\nold source:\n{previous}\nnew source:\n{}\noperation prefix:\n{}",
                        file.content,
                        prior_operations(),
                    )
                });
            format!(
                "mutation={mutation_index} fence-conversion case={} path={path} old={previous:?} new={:?}",
                file.case_index, file.content
            )
        }
    }
}

fn scanner_census_existing_path(
    files: &BTreeMap<String, ScannerCensusFile>,
    rng: &mut ScannerCensusRng,
    seed: u64,
    mutation_index: usize,
    operation_prefix: &[String],
    predicate: impl Fn(&ScannerCensusFile) -> bool,
) -> String {
    let candidates: Vec<&String> = files
        .iter()
        .filter_map(|(path, file)| predicate(file).then_some(path))
        .collect();
    assert!(
        !candidates.is_empty(),
        "scanner census seed={seed} mutation={mutation_index}: no mutation candidate\noperation prefix:\n{}",
        scanner_census_operation_prefix(operation_prefix),
    );
    candidates[rng.below(candidates.len())].clone()
}

fn scanner_census_has_supported_fence(content: &str) -> bool {
    content.contains("```base\n")
        || content.contains("```slate-query\n")
        || content.contains("```dataview\n")
}

fn scan_census_checkpoint(
    incremental: &VaultSession,
    vault_root: &Path,
    files: &BTreeMap<String, ScannerCensusFile>,
    seed: u64,
    checkpoint: &str,
    operation_prefix: &[String],
) {
    let repro = scanner_census_repro(seed, checkpoint, operation_prefix);
    let report = incremental
        .scan_initial(&CancelToken::new())
        .unwrap_or_else(|error| panic!("{repro}\nincremental scan failed: {error}"));
    assert!(
        report.errors.is_empty(),
        "{repro}\nincremental scan errors: {:?}",
        report.errors
    );
    let incremental_snapshot = scanner_census_index_snapshot(incremental, &repro);

    let fresh_cache = tempfile::tempdir()
        .unwrap_or_else(|error| panic!("{repro}\ncreate fresh cache root: {error}"));
    let fresh = VaultSession::open(
        Arc::new(FsVaultProvider::new(vault_root.to_path_buf())),
        SessionConfig::new(fresh_cache.path().join("cache")),
    )
    .unwrap_or_else(|error| panic!("{repro}\nopen genuinely fresh session: {error}"));
    let fresh_report = fresh
        .scan_initial(&CancelToken::new())
        .unwrap_or_else(|error| panic!("{repro}\nfresh scan failed: {error}"));
    assert!(
        fresh_report.errors.is_empty(),
        "{repro}\nfresh scan errors: {:?}",
        fresh_report.errors
    );
    let fresh_snapshot = scanner_census_index_snapshot(&fresh, &repro);

    if incremental_snapshot != fresh_snapshot {
        let mismatch_index = incremental_snapshot
            .iter()
            .zip(&fresh_snapshot)
            .position(|(left, right)| left != right)
            .unwrap_or_else(|| incremental_snapshot.len().min(fresh_snapshot.len()));
        let incremental_row = incremental_snapshot.get(mismatch_index);
        let fresh_row = fresh_snapshot.get(mismatch_index);
        let source = incremental_row
            .or(fresh_row)
            .and_then(|row| scanner_census_source_for_row(row, files))
            .map_or_else(
                || "source path could not be derived from mismatched row".to_string(),
                |file| {
                    format!(
                        "case={} path={} source:\n{}",
                        file.case_index, file.path, file.content
                    )
                },
            );
        panic!(
            "{repro}\nindex mismatch at row {mismatch_index}\nincremental={incremental_row:?}\nfresh={fresh_row:?}\n{source}"
        );
    }
}

fn scanner_census_repro(seed: u64, checkpoint: &str, operation_prefix: &[String]) -> String {
    format!(
        "scanner census seed={seed} checkpoint={checkpoint}\noperation prefix:\n{}",
        scanner_census_operation_prefix(operation_prefix)
    )
}

fn scanner_census_operation_prefix(operation_prefix: &[String]) -> String {
    if operation_prefix.is_empty() {
        "<initial generated corpus; case index is encoded in every path>".to_string()
    } else {
        operation_prefix.join("\n")
    }
}

fn scanner_census_source_for_row<'a>(
    row: &str,
    files: &'a BTreeMap<String, ScannerCensusFile>,
) -> Option<&'a ScannerCensusFile> {
    files.values().find(|file| row.contains(file.path.as_str()))
}

fn scanner_census_index_snapshot(session: &VaultSession, repro: &str) -> Vec<String> {
    let conn = session
        .conn
        .lock()
        .unwrap_or_else(|error| panic!("{repro}\nlock scanner index for snapshot: {error}"));
    let mut rows = Vec::new();

    conn.execute_batch(
        "CREATE VIRTUAL TABLE IF NOT EXISTS temp.scanner_census_fts_vocab
         USING fts5vocab(main, files_fts, instance);",
    )
    .unwrap_or_else(|error| panic!("{repro}\ncreate FTS snapshot vocabulary: {error}"));

    for query in [
        "SELECT json_array('files', path, name, extension, size_bytes, mtime_ms, ctime_ms, content_hash, parser_version, is_markdown, body_text) FROM files ORDER BY path",
        "SELECT json_array('dirs', path, parent_path, name) FROM dirs ORDER BY path",
        "SELECT json_array('headings', f.path, h.ordinal, h.level, h.text, h.anchor_id, h.byte_offset) FROM headings h JOIN files f ON f.id = h.file_id ORDER BY f.path, h.ordinal",
        "SELECT json_array('links', f.path, l.ordinal, l.target_path, l.target_raw, l.target_anchor, l.kind, l.is_embed, l.is_external, l.snippet, l.span_start, l.span_end, l.display_text) FROM links l JOIN files f ON f.id = l.source_file_id ORDER BY f.path, l.ordinal",
        "SELECT json_array('properties', f.path, p.ordinal, p.key, p.value_kind, p.value_text, p.value_text_norm) FROM properties p JOIN files f ON f.id = p.file_id ORDER BY f.path, p.ordinal",
        "SELECT json_array('property-list', f.path, p.key, p.value_norm) FROM properties_list_values p JOIN files f ON f.id = p.file_id ORDER BY f.path, p.key, p.value_norm",
        "SELECT json_array('tasks', f.path, t.ordinal, t.text, t.status_char, t.completed, t.due_ms, t.scheduled_ms, t.priority, t.recurrence, t.line, t.byte_offset) FROM tasks t JOIN files f ON f.id = t.file_id ORDER BY f.path, t.ordinal",
        "SELECT json_array('blocks', f.path, b.ordinal, b.block_id, b.kind, b.line_start, b.line_end, b.byte_start, b.byte_end, b.text_preview) FROM blocks b JOIN files f ON f.id = b.file_id ORDER BY f.path, b.ordinal",
        "SELECT json_array('tags', f.path, t.tag_norm) FROM file_tags t JOIN files f ON f.id = t.file_id ORDER BY f.path, t.tag_norm",
        "SELECT json_array('bases-files', f.path, b.name, b.parsed_query_json, b.warning_count, b.parser_version) FROM bases_files b JOIN files f ON f.id = b.file_id ORDER BY f.path",
        "SELECT json_array('bases-blocks', f.path, b.fence_kind, b.source_text, b.line, b.byte_offset) FROM bases_blocks b JOIN files f ON f.id = b.file_id ORDER BY f.path, b.byte_offset, b.fence_kind",
        "SELECT json_array('fts', f.path, v.term, v.col, v.offset) FROM scanner_census_fts_vocab v JOIN files f ON f.id = v.doc ORDER BY f.path, v.term, v.col, v.offset",
    ] {
        let mut statement = conn
            .prepare(query)
            .unwrap_or_else(|error| panic!("{repro}\nprepare snapshot query {query:?}: {error}"));
        let query_rows = statement
            .query_map([], |row| row.get::<_, String>(0))
            .unwrap_or_else(|error| panic!("{repro}\nrun snapshot query {query:?}: {error}"));
        for row in query_rows {
            rows.push(
                row.unwrap_or_else(|error| {
                    panic!("{repro}\nread snapshot query {query:?}: {error}")
                }),
            );
        }
    }

    rows
}

struct ScannerCensusRng(u64);

impl ScannerCensusRng {
    fn new(seed: u64) -> Self {
        Self(seed)
    }

    fn next(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9e37_79b9_7f4a_7c15);
        let mut value = self.0;
        value = (value ^ (value >> 30)).wrapping_mul(0xbf58_476d_1ce4_e5b9);
        value = (value ^ (value >> 27)).wrapping_mul(0x94d0_49bb_1331_11eb);
        value ^ (value >> 31)
    }

    fn below(&mut self, upper: usize) -> usize {
        (self.next() % upper as u64) as usize
    }
}

#[test]
fn list_openable_documents_includes_base_files() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"# note").unwrap();
        p.write_file("board.canvas", br#"{"nodes":[],"edges":[]}"#)
            .unwrap();
        p.write_file(
            "Queries/Reading.base",
            br#"views:
  - type: table
    name: Reading
    order:
      - file.name
"#,
        )
        .unwrap();
        p.write_file("data.txt", b"not openable").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .list_files(FileFilter::OpenableDocuments, Paging::first(100))
        .unwrap();
    let paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
    assert_eq!(
        paths,
        vec!["Queries/Reading.base", "board.canvas", "note.md"],
        "quick-open's openable set is markdown notes plus canvas and base files"
    );
}

#[test]
fn scan_initial_skips_hidden_directories() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("real.md", b"# real").unwrap();
        // Synthetic Obsidian-style hidden config that must not be indexed.
        p.write_file(".obsidian/workspace.json", b"{}").unwrap();
        p.write_file(".obsidian/plugins/foo/main.js", b"// js")
            .unwrap();
    });

    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.files_indexed, 1, "only real.md should be indexed");

    let page = session
        .list_files(FileFilter::All, Paging::first(100))
        .unwrap();
    let paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
    assert_eq!(paths, vec!["real.md"]);
}

#[test]
fn scan_initial_does_not_index_its_own_cache_directory() {
    // The .slate cache dir is created on session open. Re-scanning
    // must not pick up the cache.sqlite or any other internal file.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // Re-scan after the cache exists. With the mtime/size skip,
    // a.md goes through the fast path on the second pass, so the
    // assertion isn't about files_indexed specifically — it's
    // about "we accounted for exactly one user file, no .slate
    // entries leaked into the scan."
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.files_seen, 1);
    assert_eq!(report.files_indexed + report.files_skipped, 1);
}

#[test]
fn rescan_with_changed_mtime_falls_through_to_rehash() {
    // Same byte count, different content. We poll until the FS
    // actually advances mtime past the original value rather than
    // assuming a fixed sleep duration is enough — coarse-
    // resolution filesystems (FAT, HFS+, some SMB mounts) would
    // make a fixed sleep flaky.
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"ABCDE").unwrap();
    });
    let first = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(first.files_indexed, 1);
    assert_eq!(first.files_skipped, 0);

    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let original_mtime = provider.stat("a.md").unwrap().mtime_ms;
    rewrite_until_mtime_advances(&provider, "a.md", b"XYZWQ", original_mtime);

    let second = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        second.files_indexed, 1,
        "mtime changed → must re-hash even though size matched"
    );
    assert_eq!(second.files_skipped, 0);
    assert!(second.bytes_processed > 0);
}

#[cfg(unix)]
#[test]
fn rescan_with_mtime_preserved_but_ctime_changed_rehashes() {
    // The mtime/size heuristic alone misses mtime-preserving
    // writers like `cp -p` and `rsync -a`. ctime catches them
    // because the inode change time always bumps when content
    // (or any inode field) is touched, even if the writer
    // restores the original mtime afterward. This test simulates
    // that scenario by writing a same-size payload and then
    // restoring the original mtime via utimensat — mtime + size
    // both look unchanged, but ctime advances.
    use std::os::unix::fs::MetadataExt;

    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"ABCDE").unwrap();
    });
    let _ = session.scan_initial(&CancelToken::new()).unwrap();

    // Capture the original mtime as a (sec, nsec) pair so we can
    // restore it after the second write.
    let abs_path = tmp.path().join("a.md");
    let original_meta = std::fs::metadata(&abs_path).unwrap();
    let original_atime = filetime_from(original_meta.atime(), original_meta.atime_nsec());
    let original_mtime = filetime_from(original_meta.mtime(), original_meta.mtime_nsec());
    let original_ctime_ms = original_meta
        .ctime()
        .saturating_mul(1_000)
        .saturating_add(original_meta.ctime_nsec() / 1_000_000);

    // Wait long enough that ctime is guaranteed to advance past
    // the original on the next write (1 s = 1_000 ms, well above
    // any reasonable ctime resolution on the test filesystem).
    std::thread::sleep(std::time::Duration::from_millis(1_100));
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider.write_file("a.md", b"XYZWQ").unwrap();
    // Restore the original atime/mtime via utimes. ctime cannot
    // be set from userspace — that's precisely why this test
    // proves the optimization is robust.
    set_atime_mtime(&abs_path, original_atime, original_mtime);

    let after_meta = std::fs::metadata(&abs_path).unwrap();
    let after_mtime_ms = after_meta
        .mtime()
        .saturating_mul(1_000)
        .saturating_add(after_meta.mtime_nsec() / 1_000_000);
    let after_ctime_ms = after_meta
        .ctime()
        .saturating_mul(1_000)
        .saturating_add(after_meta.ctime_nsec() / 1_000_000);
    assert_eq!(
        after_mtime_ms,
        original_mtime
            .0
            .saturating_mul(1_000)
            .saturating_add(original_mtime.1 as i64 / 1_000_000),
        "test precondition: mtime should be restored to its original value",
    );
    assert!(
        after_ctime_ms > original_ctime_ms,
        "test precondition: ctime should have advanced past the original"
    );

    let second = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        second.files_indexed, 1,
        "ctime changed → must re-hash even though mtime and size match"
    );
    assert_eq!(second.files_skipped, 0);
}

#[test]
fn fast_path_backfills_ctime_for_pre_migration_rows() {
    // Simulate the upgrade path: a vault scanned before migration
    // 002 has rows with `ctime_ms = 0`. After migration runs and
    // the rescan hits the fast path, we want ctime to be
    // backfilled from the current stat — otherwise these rows
    // would degrade to mtime+size-only forever and miss mtime-
    // preserving writes that the ctime optimization is supposed
    // to catch.
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"ABCDE").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Force the row into the pre-migration shape.
    let db_path = tmp.path().join(".slate").join("cache.sqlite");
    {
        let conn = rusqlite::Connection::open(&db_path).unwrap();
        conn.execute("UPDATE files SET ctime_ms = 0", []).unwrap();
        let zeroed: i64 = conn
            .query_row(
                "SELECT ctime_ms FROM files WHERE path = 'a.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(zeroed, 0, "test precondition: row is in legacy shape");
    }

    // Unchanged file → fast path hits → backfill should write the
    // real ctime even though we skipped the read+hash.
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.files_skipped, 1);
    assert_eq!(report.files_indexed, 0);

    let conn = rusqlite::Connection::open(&db_path).unwrap();
    let ctime_ms: i64 = conn
        .query_row(
            "SELECT ctime_ms FROM files WHERE path = 'a.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert!(
        ctime_ms > 0,
        "fast-path UPDATE should backfill ctime_ms from stat, got {ctime_ms}"
    );
}

#[test]
fn fast_path_does_not_clobber_known_ctime_when_stat_returns_zero() {
    // Scenario: a vault scanned on a platform that supports ctime
    // (rows carry real ctime_ms values) is later reopened by a
    // provider that returns ctime_ms = 0 from stat — the path the
    // Windows / no-ctime build would take. The fast-path UPDATE
    // must NOT zero out the known-good column.
    let tmp = tempfile::tempdir().unwrap();
    let cache_dir = tmp.path().join(".slate");
    let db_path = cache_dir.join("cache.sqlite");

    // First pass: populate the index using the real provider so
    // ctime_ms ends up non-zero (on Unix).
    {
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        provider.write_file("a.md", b"alpha").unwrap();
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        drop(session);
    }

    let conn = rusqlite::Connection::open(&db_path).unwrap();
    let initial_ctime_ms: i64 = conn
        .query_row(
            "SELECT ctime_ms FROM files WHERE path = 'a.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    drop(conn);
    // Skip on platforms where the real provider also returns 0
    // — the assertion would be vacuous.
    if initial_ctime_ms == 0 {
        return;
    }

    // Second pass: re-open with a provider that hands back
    // ctime_ms = 0. The fast-path should hit (mtime+size match)
    // and refresh indexed_at_ms WITHOUT zeroing ctime_ms.
    let session = VaultSession::open(
        Arc::new(ZeroCtimeProvider {
            inner: FsVaultProvider::new(tmp.path().to_path_buf()),
        }),
        SessionConfig::new(cache_dir.clone()),
    )
    .unwrap();
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.files_skipped, 1);
    assert_eq!(report.files_indexed, 0);
    drop(session);

    let conn = rusqlite::Connection::open(&db_path).unwrap();
    let after_ctime_ms: i64 = conn
        .query_row(
            "SELECT ctime_ms FROM files WHERE path = 'a.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(
        after_ctime_ms, initial_ctime_ms,
        "fast-path UPDATE must not clobber a known-good ctime_ms with a 0 sentinel"
    );
}

#[test]
fn rescan_unchanged_files_skips_rehashing() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"alpha").unwrap();
        p.write_file("b.md", b"beta").unwrap();
    });
    let first = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(first.files_indexed, 2);
    assert_eq!(first.files_skipped, 0);

    // Nothing on disk changed. The fast path should hit for every
    // file and bytes_processed should be zero — we never re-read
    // file content, so no IO bytes accumulate.
    let second = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(second.files_seen, 2);
    assert_eq!(second.files_indexed, 0);
    assert_eq!(second.files_skipped, 2);
    assert_eq!(second.bytes_processed, 0);
}

#[test]
fn rescan_prunes_files_deleted_out_of_band() {
    // #641 (codex adversarial round 4): a note deleted outside Slate
    // must fall out of the index on the next scan — otherwise existence
    // checks lie: `slate write` reports a misleading conflict instead
    // of "no such note", and `--create` anchors to the stale hash and
    // can't recreate the note.
    let (tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"# Keep\n\nsurvivor").unwrap();
        p.write_file("sub/gone.md", b"# Gone\n\ndoomedtoken")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        session.get_file_metadata("sub/gone.md").unwrap().is_some(),
        "indexed before deletion"
    );
    assert_eq!(fts_match_count(&session, "doomedtoken"), 1);

    // Out-of-band deletion (not through the session).
    std::fs::remove_file(tmp.path().join("sub/gone.md")).unwrap();

    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        session.get_file_metadata("sub/gone.md").unwrap().is_none(),
        "stale row pruned on rescan"
    );
    assert!(
        session.get_file_metadata("keep.md").unwrap().is_some(),
        "surviving file untouched"
    );
    // The FTS row cascaded away with the files row (migration-006
    // DELETE trigger) — no ghost search hits.
    assert_eq!(fts_match_count(&session, "doomedtoken"), 0);
    let page = session
        .list_files(FileFilter::All, Paging::first(100))
        .unwrap();
    assert_eq!(page.total_filtered, 1);
}

/// Wraps `FsVaultProvider` but fails `list_dir` for one specific
/// directory — simulates a transient walk error (permissions, IO).
struct FailingListDirProvider {
    inner: FsVaultProvider,
    fail_dir: String,
}

impl crate::VaultProvider for FailingListDirProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        if relative == self.fail_dir {
            return Err(VaultError::Io(std::io::Error::other(
                "simulated transient listing failure",
            )));
        }
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        self.inner.read_file(relative)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
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

/// Wraps `FsVaultProvider` but fails `stat`/`read_file` for one chosen
/// path with a chosen `io::ErrorKind` — simulates a file vanishing (or
/// becoming unreadable) between the directory listing and the per-file
/// stat (#641 codex round 5).
struct FlakyStatProvider {
    inner: FsVaultProvider,
    fail_path: String,
    kind: std::io::ErrorKind,
}

impl crate::VaultProvider for FlakyStatProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        if relative == self.fail_path {
            return Err(VaultError::Io(std::io::Error::new(self.kind, "simulated")));
        }
        self.inner.read_file(relative)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
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
        if relative == self.fail_path {
            return Err(VaultError::Io(std::io::Error::new(self.kind, "simulated")));
        }
        self.inner.stat(relative)
    }
    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

#[test]
fn file_vanishing_between_list_and_stat_is_pruned_same_scan() {
    // #641 codex round 5: a file deleted between `list_dir` returning
    // it and `index_file` stat'ing it must not survive as a stale row —
    // the NotFound un-sees it so the same scan's prune drops the row.
    let tmp = tempfile::tempdir().unwrap();
    let clean = FsVaultProvider::new(tmp.path().to_path_buf());
    clean.write_file("keep.md", b"# Keep").unwrap();
    clean.write_file("ghost.md", b"# Ghost").unwrap();

    // First index normally.
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.get_file_metadata("ghost.md").unwrap().is_some());
    drop(session);

    // Rescan through a provider where ghost.md is still LISTED (it's
    // on disk, so list_dir returns it) but stat reports NotFound — the
    // list-then-vanish window made deterministic.
    let flaky = Arc::new(FlakyStatProvider {
        inner: FsVaultProvider::new(tmp.path().to_path_buf()),
        fail_path: "ghost.md".to_string(),
        kind: std::io::ErrorKind::NotFound,
    });
    let session = VaultSession::open(flaky, SessionConfig::new(tmp.path().join(".slate"))).unwrap();
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        report.errors.iter().any(|e| e.contains("ghost.md")),
        "the vanish is reported: {:?}",
        report.errors
    );
    assert!(
        session.get_file_metadata("ghost.md").unwrap().is_none(),
        "vanished file's row pruned in the SAME scan"
    );
    assert!(session.get_file_metadata("keep.md").unwrap().is_some());
}

#[test]
fn file_erroring_for_other_reasons_keeps_its_row() {
    // The counterweight: a per-file error that is NOT NotFound
    // (permissions, transient IO) means the file still exists — its
    // row must survive the prune.
    let tmp = tempfile::tempdir().unwrap();
    let clean = FsVaultProvider::new(tmp.path().to_path_buf());
    clean.write_file("locked.md", b"# Locked").unwrap();

    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.get_file_metadata("locked.md").unwrap().is_some());
    drop(session);

    let flaky = Arc::new(FlakyStatProvider {
        inner: FsVaultProvider::new(tmp.path().to_path_buf()),
        fail_path: "locked.md".to_string(),
        kind: std::io::ErrorKind::PermissionDenied,
    });
    let session = VaultSession::open(flaky, SessionConfig::new(tmp.path().join(".slate"))).unwrap();
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        report.errors.iter().any(|e| e.contains("locked.md")),
        "the error is reported: {:?}",
        report.errors
    );
    assert!(
        session.get_file_metadata("locked.md").unwrap().is_some(),
        "an existing-but-unreadable file keeps its row"
    );
}

#[test]
fn incomplete_walk_never_prunes_file_rows() {
    // The conservative half of the prune: a failed directory listing
    // hides its whole subtree, and pruning on that partial view would
    // evict live rows wholesale. With `sub/` unlistable, its indexed
    // file must survive the rescan.
    let tmp = tempfile::tempdir().unwrap();
    let clean = FsVaultProvider::new(tmp.path().to_path_buf());
    clean.write_file("root.md", b"# Root").unwrap();
    clean.write_file("sub/nested.md", b"# Nested").unwrap();

    // First index with a healthy provider.
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        session
            .get_file_metadata("sub/nested.md")
            .unwrap()
            .is_some()
    );
    drop(session);

    // Rescan through a provider whose `sub` listing fails.
    let failing = Arc::new(FailingListDirProvider {
        inner: FsVaultProvider::new(tmp.path().to_path_buf()),
        fail_dir: "sub".to_string(),
    });
    let config = SessionConfig::new(tmp.path().join(".slate"));
    let session = VaultSession::open(failing, config).unwrap();
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        report.errors.iter().any(|e| e.contains("list_dir")),
        "the failed listing is reported: {:?}",
        report.errors
    );
    assert!(
        session
            .get_file_metadata("sub/nested.md")
            .unwrap()
            .is_some(),
        "rows under the unlistable directory survive"
    );
}

#[test]
fn scan_can_be_cancelled() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"").unwrap();
    });

    let cancel = CancelToken::new();
    cancel.cancel();

    match session.scan_initial(&cancel) {
        Err(VaultError::Cancelled) => { /* expected */ }
        other => panic!("expected Cancelled, got {other:?}"),
    }
}

#[test]
fn cancel_after_transaction_opens_rolls_back_inserts() {
    // Triggers cancellation from inside the provider so the cancel
    // fires *after* scan_vault has opened the write transaction.
    // This is the case Codoki flagged as needing dedicated coverage
    // beyond the pre-tx cancel path.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider.write_file("a/one.md", b"a").unwrap();
    provider.write_file("a/two.md", b"a").unwrap();
    provider.write_file("b/three.md", b"b").unwrap();

    let cancel = CancelToken::new();
    // Trigger cancellation on the third list_dir call. By then the
    // scan has already opened its transaction, listed the root,
    // descended into one subdirectory, and inserted that subdir's
    // single file — so the assertion "index is empty after cancel"
    // proves the transaction was rolled back, not just bailed
    // before doing any work.
    let cancelling = Arc::new(CancellingProvider::new(provider, cancel.clone(), 3));
    let cache_dir = tmp.path().join(".slate");
    let config = SessionConfig::new(cache_dir);
    let session = VaultSession::open(cancelling, config).unwrap();

    match session.scan_initial(&cancel) {
        Err(VaultError::Cancelled) => {}
        other => panic!("expected Cancelled, got {other:?}"),
    }

    let page = session
        .list_files(FileFilter::All, Paging::first(100))
        .unwrap();
    assert!(
        page.items.is_empty(),
        "mid-transaction cancel must roll back any in-progress inserts"
    );
    assert_eq!(page.total_filtered, 0);
}

#[test]
fn cancelled_scan_leaves_index_empty() {
    // Cancel before scan_vault opens the write transaction; nothing
    // partially-applied should land in `files`. Re-scanning after
    // clearing the cancel flag must produce a fully populated index.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a").unwrap();
        p.write_file("notes/b.md", b"b").unwrap();
    });

    let cancel = CancelToken::new();
    cancel.cancel();
    match session.scan_initial(&cancel) {
        Err(VaultError::Cancelled) => {}
        other => panic!("expected Cancelled, got {other:?}"),
    }

    // No files indexed: the transaction was rolled back (in practice,
    // never opened because the pre-tx check fires first).
    let page = session
        .list_files(FileFilter::All, Paging::first(100))
        .unwrap();
    assert!(page.items.is_empty(), "cancel should leave no rows behind");
    assert_eq!(page.total_filtered, 0);

    // Fresh token: scan succeeds and the index populates.
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.files_indexed, 2);
}

#[test]
fn rescan_updates_existing_rows_via_on_conflict() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("evolving.md", b"v1").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let p1 = session
        .list_files(FileFilter::All, Paging::first(10))
        .unwrap();
    let v1_size = p1.items[0].size_bytes;

    // Modify the file on disk; rescan; size should update.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("evolving.md", b"this is a longer version")
        .unwrap();
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.files_indexed, 1);

    let p2 = session
        .list_files(FileFilter::All, Paging::first(10))
        .unwrap();
    assert!(
        p2.items[0].size_bytes > v1_size,
        "size should update on re-scan"
    );
    assert_eq!(p2.items.len(), 1, "no duplicate row");
}

#[test]
fn from_filesystem_rejects_nonexistent_root() {
    let parent = tempfile::tempdir().unwrap();
    let bogus = parent.path().join("not-a-vault");
    assert!(!bogus.exists(), "precondition: path must not exist yet");

    match VaultSession::from_filesystem(bogus.clone()) {
        Ok(_) => panic!("from_filesystem should reject a nonexistent root"),
        Err(VaultError::InvalidPath { .. }) => {}
        Err(other) => panic!("expected InvalidPath, got {other:?}"),
    }

    // Regression: open must not have silently created the vault.
    assert!(
        !bogus.exists(),
        "from_filesystem must not materialize a missing vault root"
    );
}

#[test]
fn from_filesystem_rejects_file_as_root() {
    let tmp = tempfile::tempdir().unwrap();
    let file_root = tmp.path().join("vault-is-a-file");
    std::fs::write(&file_root, b"oops").unwrap();

    match VaultSession::from_filesystem(file_root) {
        Ok(_) => panic!("from_filesystem should reject a regular file as root"),
        Err(VaultError::InvalidPath { .. }) => {}
        Err(other) => panic!("expected InvalidPath, got {other:?}"),
    }
}

#[test]
fn case_insensitive_markdown_extensions_are_detected() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("UPPER.MD", b"# upper").unwrap();
        p.write_file("Mixed.Markdown", b"# mixed").unwrap();
        p.write_file("lower.md", b"# lower").unwrap();
        p.write_file("not-md.TXT", b"plain").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .list_files(FileFilter::MarkdownOnly, Paging::first(100))
        .unwrap();
    let mut names: Vec<&str> = page.items.iter().map(|f| f.name.as_str()).collect();
    names.sort();
    assert_eq!(names, vec!["Mixed.Markdown", "UPPER.MD", "lower.md"]);
    assert_eq!(page.total_filtered, 3);
    for item in &page.items {
        assert!(item.is_markdown);
    }
}

#[test]
fn files_seen_counts_only_files_not_directories() {
    let (_tmp, session) = make_vault(|p| {
        // Five files spread across three subdirectories.
        p.write_file("a.md", b"a").unwrap();
        p.write_file("notes/b.md", b"b").unwrap();
        p.write_file("notes/c.md", b"c").unwrap();
        p.write_file("notes/sub/d.md", b"d").unwrap();
        p.write_file("attachments/e.png", b"\x89PNG").unwrap();
    });

    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        report.files_seen, 5,
        "files_seen should count only files, not the three directories"
    );
    assert_eq!(report.files_indexed, 5);
}

#[cfg(unix)]
#[test]
fn symlinks_pointing_out_of_vault_are_not_indexed() {
    // Sentinel file outside the vault.
    let outside_dir = tempfile::tempdir().unwrap();
    let secret = outside_dir.path().join("secret.txt");
    std::fs::write(&secret, b"SECRET").unwrap();

    let (vault_tmp, session) = make_vault(|p| {
        p.write_file("real.md", b"# real").unwrap();
    });
    // Symlink inside the vault pointing at the outside sentinel.
    std::os::unix::fs::symlink(&secret, vault_tmp.path().join("leak.md")).unwrap();

    let report = session.scan_initial(&CancelToken::new()).unwrap();

    // The vault has one real file; the symlink should be skipped.
    assert_eq!(report.files_indexed, 1);

    let page = session
        .list_files(FileFilter::All, Paging::first(100))
        .unwrap();
    let paths: Vec<&str> = page.items.iter().map(|f| f.path.as_str()).collect();
    assert_eq!(paths, vec!["real.md"]);
}

#[test]
fn scan_persists_headings_in_document_order() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/example.md",
            b"# Top\n\nIntro.\n\n## Sub one\n\n## Sub two\n\n### Deeper\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let md = session
        .get_file_metadata("notes/example.md")
        .unwrap()
        .expect("note should be indexed");
    let summary: Vec<(u32, u8, &str, &str)> = md
        .headings
        .iter()
        .map(|h| (h.ordinal, h.level, h.text.as_str(), h.anchor_id.as_str()))
        .collect();
    assert_eq!(
        summary,
        vec![
            (0, 1, "Top", "top"),
            (1, 2, "Sub one", "sub-one"),
            (2, 2, "Sub two", "sub-two"),
            (3, 3, "Deeper", "deeper"),
        ]
    );
    assert!(md.is_markdown);
    assert!(!md.content_hash.is_empty());
}

#[test]
fn editing_a_note_replaces_its_heading_rows_no_orphans() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"# Old\n\n## Stale\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Force mtime to advance so the fast path doesn't skip the rescan.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let original_mtime = provider.stat("a.md").unwrap().mtime_ms;
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);
    loop {
        provider.write_file("a.md", b"# Brand new\n").unwrap();
        if provider.stat("a.md").unwrap().mtime_ms != original_mtime {
            break;
        }
        assert!(
            std::time::Instant::now() < deadline,
            "mtime did not advance on rewrite — FS resolution too coarse"
        );
        std::thread::sleep(std::time::Duration::from_millis(50));
    }
    session.scan_initial(&CancelToken::new()).unwrap();

    let md = session.get_file_metadata("a.md").unwrap().unwrap();
    let texts: Vec<&str> = md.headings.iter().map(|h| h.text.as_str()).collect();
    assert_eq!(texts, vec!["Brand new"], "stale headings must be cleared");
}

#[test]
fn fast_path_rescan_does_not_touch_headings_table() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"# Stable\n").unwrap();
    });
    let first = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(first.files_indexed, 1);

    // Snapshot the heading rows before the rescan so we can prove
    // identity, not just equivalence, after the fast path runs.
    let before: Vec<(i64, u32, u8, String, String)> = {
        let conn = session.conn.lock().unwrap();
        let mut stmt = conn
            .prepare(
                "SELECT file_id, ordinal, level, text, anchor_id
                 FROM headings ORDER BY file_id, ordinal",
            )
            .unwrap();
        stmt.query_map([], |row| {
            Ok((
                row.get::<_, i64>(0)?,
                row.get::<_, i64>(1)? as u32,
                row.get::<_, i64>(2)? as u8,
                row.get::<_, String>(3)?,
                row.get::<_, String>(4)?,
            ))
        })
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap()
    };

    // Unchanged file → fast path → no heading rewrites.
    let second = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(second.files_skipped, 1);
    assert_eq!(second.files_indexed, 0);

    let after: Vec<(i64, u32, u8, String, String)> = {
        let conn = session.conn.lock().unwrap();
        let mut stmt = conn
            .prepare(
                "SELECT file_id, ordinal, level, text, anchor_id
                 FROM headings ORDER BY file_id, ordinal",
            )
            .unwrap();
        stmt.query_map([], |row| {
            Ok((
                row.get::<_, i64>(0)?,
                row.get::<_, i64>(1)? as u32,
                row.get::<_, i64>(2)? as u8,
                row.get::<_, String>(3)?,
                row.get::<_, String>(4)?,
            ))
        })
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap()
    };
    assert_eq!(before, after);
}

#[test]
fn headings_survive_non_utf8_bytes_via_lossy_decode() {
    // Construct a payload with a valid Markdown heading followed
    // by an invalid UTF-8 continuation byte. Without lossy
    // decode, str::from_utf8 would fail and we'd silently lose
    // the heading.
    let mut bytes = b"# Heading survives\n\nBody line.\n".to_vec();
    bytes.push(0xFF); // invalid as the start of a UTF-8 sequence
    bytes.extend_from_slice(b"\n## Also here\n");

    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", &bytes).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let md = session.get_file_metadata("a.md").unwrap().unwrap();
    let texts: Vec<&str> = md.headings.iter().map(|h| h.text.as_str()).collect();
    assert_eq!(texts, vec!["Heading survives", "Also here"]);
}

struct RecordingListener {
    events: std::sync::Mutex<Vec<ScanProgress>>,
}

impl RecordingListener {
    fn new() -> Arc<Self> {
        Arc::new(Self {
            events: std::sync::Mutex::new(Vec::new()),
        })
    }

    fn snapshot(&self) -> Vec<ScanProgress> {
        self.events.lock().unwrap().clone()
    }
}

impl ScanProgressListener for RecordingListener {
    fn on_progress(&self, event: ScanProgress) {
        self.events.lock().unwrap().push(event);
    }
}

#[test]
fn scan_progress_emits_started_one_per_file_and_finished() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"# a").unwrap();
        p.write_file("notes/b.md", b"# b").unwrap();
        p.write_file("notes/c.md", b"# c").unwrap();
    });

    let listener = RecordingListener::new();
    let report = session
        .scan_initial_with_progress(
            &CancelToken::new(),
            Some(listener.clone() as Arc<dyn ScanProgressListener>),
        )
        .unwrap();
    assert_eq!(report.files_indexed, 3);

    let events = listener.snapshot();
    // First: Started with total=3.
    match &events[0] {
        ScanProgress::Started { total_files } => assert_eq!(*total_files, 3),
        other => panic!("expected Started first, got {other:?}"),
    }
    // Last: Finished.
    match events.last().unwrap() {
        ScanProgress::Finished { report: r } => {
            assert_eq!(r.files_indexed, 3);
        }
        other => panic!("expected Finished last, got {other:?}"),
    }
    // Middle: exactly 3 FileIndexed in order with monotonic counter.
    let file_events: Vec<(u64, u64, &str)> = events[1..events.len() - 1]
        .iter()
        .map(|e| match e {
            ScanProgress::FileIndexed {
                path,
                indexed,
                total,
            } => (*indexed, *total, path.as_str()),
            _ => panic!("unexpected non-FileIndexed event in middle: {e:?}"),
        })
        .collect();
    assert_eq!(file_events.len(), 3);
    assert_eq!(file_events[0].0, 1);
    assert_eq!(file_events[2].0, 3);
    for (_, t, _) in &file_events {
        assert_eq!(*t, 3);
    }
}

#[test]
fn scan_progress_pre_started_cancel_emits_no_events() {
    // A token that's already cancelled at scan_initial entry
    // means the stream never starts — listener observes no
    // events at all, just the Err(Cancelled) return value.
    // This is the deliberate "Started gates the contract" shape
    // documented on ScanProgress.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"# a").unwrap();
    });
    let listener = RecordingListener::new();
    let cancel = CancelToken::new();
    cancel.cancel();
    let err = session
        .scan_initial_with_progress(
            &cancel,
            Some(listener.clone() as Arc<dyn ScanProgressListener>),
        )
        .unwrap_err();
    assert!(matches!(err, VaultError::Cancelled));
    assert!(
        listener.snapshot().is_empty(),
        "pre-Started cancel must not emit any listener events"
    );
}

#[test]
fn scan_progress_emits_cancelled_when_cancelled_mid_scan() {
    // Uses the existing CancellingProvider to trigger cancel
    // from inside list_dir while the main scan is in flight.
    // Listener should still see Started + (some FileIndexed)? +
    // Cancelled, with no Finished event ever firing.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider.write_file("a/one.md", b"a").unwrap();
    provider.write_file("a/two.md", b"a").unwrap();
    provider.write_file("b/three.md", b"b").unwrap();

    let cancel = CancelToken::new();
    // First list_dir call inside scan_vault is the root (because
    // count_files runs first and exhausts its own walk). After
    // count_files the main scan also does list_dir; cancel on
    // the first scan-side list_dir call. count_files does N
    // list_dir calls; we need to count past those.
    //
    // For 3 files spread across 2 subdirs, count_files does 3
    // list_dirs (root, a/, b/). Then scan does root again (call
    // 4). Trigger on call 4 so cancel fires mid-scan but after
    // Started has been emitted.
    let cancelling = Arc::new(CancellingProvider::new(provider, cancel.clone(), 4));
    let cache_dir = tmp.path().join(".slate");
    let config = SessionConfig::new(cache_dir);
    let session = VaultSession::open(cancelling, config).unwrap();

    let listener = RecordingListener::new();
    let err = session
        .scan_initial_with_progress(
            &cancel,
            Some(listener.clone() as Arc<dyn ScanProgressListener>),
        )
        .unwrap_err();
    assert!(matches!(err, VaultError::Cancelled));

    let events = listener.snapshot();
    // Started fires once before count completes.
    assert!(matches!(events.first(), Some(ScanProgress::Started { .. })));
    // Terminal event is Cancelled, never Finished.
    assert!(matches!(events.last(), Some(ScanProgress::Cancelled)));
    assert!(
        !events
            .iter()
            .any(|e| matches!(e, ScanProgress::Finished { .. })),
        "Finished must NOT fire on a cancelled scan, got {events:?}"
    );
}

#[test]
fn scan_initial_without_listener_is_unchanged() {
    // The no-listener path is the original scan_initial contract:
    // returns the report, doesn't call anything (no listener
    // implementation needed). Smoke-test that the new overload
    // didn't accidentally regress the listener-less case.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"# a").unwrap();
        p.write_file("b.md", b"# b").unwrap();
    });
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(report.files_indexed, 2);
}

#[test]
fn scan_skips_body_indexing_for_files_past_refuse_threshold() {
    // Audit-#88-B2 regression: previously the slow path read
    // the full file into memory regardless of size. A multi-GB
    // file would blow process memory and overflow SQLite's
    // TEXT cap. The fix surfaces a per-file scan error and
    // upserts metadata with an empty body so the row still
    // exists in the index but FTS / body queries return
    // nothing.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("huge.md", b"more than ten bytes here please")
        .unwrap();

    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 10;
    let session = VaultSession::open(Arc::new(provider), config).unwrap();
    let report = session.scan_initial(&CancelToken::new()).unwrap();

    // The file is still indexed (counts toward files_indexed)
    // but the report carries an error and the body is empty.
    assert_eq!(report.files_indexed, 1);
    assert!(
        report
            .errors
            .iter()
            .any(|e| e.contains("exceeds large-file refuse threshold")),
        "expected refuse error in report.errors, got {:?}",
        report.errors
    );
    // Body should be empty in the DB → FTS matches against a
    // distinct token must come back empty.
    let result = session
        .full_text_search(
            "distincttokennomatchexpected",
            &crate::SearchScope::Vault,
            &CancelToken::new(),
        )
        .unwrap();
    assert!(result.rows.is_empty());

    // The file row exists in get_file_metadata (sidebar still
    // shows it) — confirming this is a "skip body" not "skip
    // row".
    let md = session.get_file_metadata("huge.md").unwrap();
    assert!(md.is_some(), "file row should still be indexed");
}

#[test]
fn file_growing_past_refuse_threshold_purges_derivatives() {
    // Regression: if a file is indexed once under the large-file
    // refuse threshold (so its headings, outgoing links, and
    // frontmatter properties are persisted) and then grows past
    // the threshold, the next scan must drop those derivative
    // rows. Otherwise the sidebar / backlinks panel / properties
    // query keep surfacing stale data that points into a body we
    // no longer index.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    // Link target so the outgoing link resolves on the first scan.
    provider.write_file("target.md", b"# target").unwrap();
    // Small source file with one heading, one outgoing link, and
    // one frontmatter property. Total is well under the 300-byte
    // cap configured below.
    let small_body = "---\ntag: important\n---\n# H1\nSee [[target]]\nbody:: 1\n";
    provider
        .write_file("src.md", small_body.as_bytes())
        .unwrap();

    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 300;
    let session = VaultSession::open(Arc::new(provider), config).unwrap();

    // First scan: file is under the cap → full markdown indexing.
    session.scan_initial(&CancelToken::new()).unwrap();
    let md_before = session.get_file_metadata("src.md").unwrap().unwrap();
    assert!(
        !md_before.headings.is_empty(),
        "headings should be indexed: {:?}",
        md_before.headings
    );
    assert!(
        !md_before.properties.is_empty(),
        "frontmatter properties should be indexed: {:?}",
        md_before.properties
    );
    let outgoing_before = session.outgoing_links("src.md").unwrap();
    assert!(
        !outgoing_before.is_empty(),
        "outgoing link should be indexed: {outgoing_before:?}"
    );
    {
        let conn = session.conn.lock().unwrap();
        let (field_count, incomplete): (i64, i64) = conn
            .query_row(
                "SELECT
                   (SELECT COUNT(*) FROM dql_inline_fields i WHERE i.file_id = f.id),
                   s.incomplete
                 FROM files f
                 JOIN dql_inline_field_state s ON s.file_id = f.id
                 WHERE f.path = 'src.md'",
                [],
                |row| Ok((row.get(0)?, row.get(1)?)),
            )
            .unwrap();
        assert_eq!(field_count, 1, "body inline field should be indexed");
        assert_eq!(incomplete, 0, "small-file projection should be complete");
    }

    // Grow the file past the cap. Use the FsVaultProvider directly
    // so we don't have to thread the session's provider out.
    let big_provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let big_body = "x".repeat(500);
    big_provider
        .write_file("src.md", big_body.as_bytes())
        .unwrap();

    // Second scan: file is now over the cap → large-file branch.
    let report = session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        report
            .errors
            .iter()
            .any(|e| e.contains("exceeds large-file refuse threshold")),
        "expected refuse error in report.errors, got {:?}",
        report.errors
    );

    // The file row should still exist (sidebar visibility) ...
    let md_after = session.get_file_metadata("src.md").unwrap().unwrap();
    // ... but every derivative table should be empty for it.
    assert!(
        md_after.headings.is_empty(),
        "stale heading rows must be purged when file crosses the refuse threshold, got {:?}",
        md_after.headings
    );
    assert!(
        md_after.properties.is_empty(),
        "stale property rows must be purged, got {:?}",
        md_after.properties
    );
    let outgoing_after = session.outgoing_links("src.md").unwrap();
    assert!(
        outgoing_after.is_empty(),
        "stale outgoing link rows must be purged, got {outgoing_after:?}"
    );
    let conn = session.conn.lock().unwrap();
    let (field_count, incomplete): (i64, i64) = conn
        .query_row(
            "SELECT
               (SELECT COUNT(*) FROM dql_inline_fields i WHERE i.file_id = f.id),
               s.incomplete
             FROM files f
             JOIN dql_inline_field_state s ON s.file_id = f.id
             WHERE f.path = 'src.md'",
            [],
            |row| Ok((row.get(0)?, row.get(1)?)),
        )
        .unwrap();
    assert_eq!(field_count, 0, "stale body inline fields must be purged");
    assert_eq!(
        incomplete, 1,
        "large-file projection must fail loud instead of claiming an empty body"
    );
}

#[test]
fn non_markdown_files_have_no_headings() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/note.md", b"# Heading\n").unwrap();
        p.write_file("notes/img.png", b"\x89PNG\x0d\x0a\x1a\x0a")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let md_note = session.get_file_metadata("notes/note.md").unwrap().unwrap();
    assert_eq!(md_note.headings.len(), 1);

    let md_img = session.get_file_metadata("notes/img.png").unwrap().unwrap();
    assert!(
        md_img.headings.is_empty(),
        "non-markdown files should never carry headings"
    );
    assert!(!md_img.is_markdown);
}
