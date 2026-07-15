// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Integration tests for the `slate` CLI (M-4, #535).
//!
//! Drives the built binary via `assert_cmd` against `tempfile` fixture
//! vaults, covering the m_spec §M-4 test list:
//! - `open` round-trips all three formats; second run reports warm;
//! - `sync-check` json on a LiveSync+iCloud fixture lists both + a
//!   non-null warning; a clean fixture yields empty providers, exit 0;
//! - nonexistent path → exit 1 (`not a vault directory`); unknown flag
//!   → exit 2;
//! - `--format json` stdout still parses when stderr carries noise;
//! - (unix) SIGINT during `open` on a 5k-file fixture → exit 130, and
//!   the vault reopens clean afterward (no cache corruption).

use std::fs;
use std::path::Path;

use assert_cmd::Command;
use predicates::prelude::*;
use serde_json::Value;
use slate_core::bases;
use slate_core::{CancelToken, SavedQuerySourceSyntax, VaultSession};
use tempfile::TempDir;

/// The binary under test.
fn slate() -> Command {
    Command::cargo_bin("slate").expect("slate binary builds")
}

/// A small seeded vault: two markdown notes + one non-markdown file.
fn seed_basic_vault() -> TempDir {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    fs::write(root.join("alpha.md"), "# Alpha\n\nSome body text.\n").unwrap();
    fs::write(root.join("beta.md"), "# Beta\n\nMore text.\n").unwrap();
    fs::write(root.join("data.txt"), "not markdown\n").unwrap();
    dir
}

/// Parse the stdout of a `slate --format json` run and assert the
/// envelope invariants (schema, command, vault). Returns `data`.
fn assert_envelope(stdout: &[u8], command: &str) -> Value {
    let text = std::str::from_utf8(stdout).expect("utf8 stdout");
    let v: Value = serde_json::from_str(text).expect("stdout is valid JSON");
    assert_eq!(v["schema"], "slate.cli.v1", "schema field");
    assert_eq!(v["command"], command, "command field");
    assert!(v["vault"].is_string(), "vault field present");
    v["data"].clone()
}

// --- `open` -----------------------------------------------------------

#[test]
fn open_human_reports_counts() {
    let vault = seed_basic_vault();
    slate()
        .arg("open")
        .arg(vault.path())
        .assert()
        .success()
        .stdout(predicate::str::contains("Files: 3 (2 markdown)"))
        .stdout(predicate::str::contains("Indexed: fresh"));
}

#[test]
fn open_json_parses_and_counts_match() {
    let vault = seed_basic_vault();
    let out = slate()
        .arg("open")
        .arg(vault.path())
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "open");
    assert_eq!(data["files_seen"], 3);
    assert_eq!(data["markdown_files"], 2);
    assert_eq!(data["cache"], "cold");
    assert!(data["scan_errors"].as_array().unwrap().is_empty());
}

#[test]
fn open_tsv_field_value_rows() {
    let vault = seed_basic_vault();
    slate()
        .arg("open")
        .arg(vault.path())
        .arg("--format")
        .arg("tsv")
        .assert()
        .success()
        .stdout(predicate::str::contains("field\tvalue"))
        .stdout(predicate::str::contains("files_seen\t3"))
        .stdout(predicate::str::contains("markdown_files\t2"))
        .stdout(predicate::str::contains("cache\tcold"));
}

#[test]
fn open_second_run_reports_warm_cache() {
    let vault = seed_basic_vault();
    // First run builds the cache (cold).
    slate().arg("open").arg(vault.path()).assert().success();

    // Second run reuses it (warm).
    let out = slate()
        .arg("open")
        .arg(vault.path())
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "open");
    assert_eq!(data["cache"], "warm");
}

// --- `sync-check` -----------------------------------------------------

/// Seed a vault with LiveSync markers (with planted credentials) and an
/// iCloud `.icloud` placeholder child.
fn seed_livesync_and_icloud_vault() -> TempDir {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    let plugin = root.join(".obsidian/plugins/obsidian-livesync");
    fs::create_dir_all(&plugin).unwrap();
    // manifest.json arms the LiveSync detector; data.json carries a
    // realistic config with planted credentials that must never leak.
    fs::write(plugin.join("manifest.json"), "{}").unwrap();
    fs::write(
        plugin.join("data.json"),
        r#"{
            "couchDB_URI": "https://user:pass@couch.example.com:5984/notes",
            "couchDB_DBNAME": "notes",
            "couchDB_USER": "alice",
            "couchDB_PASSWORD": "hunter2",
            "passphrase": "secret123",
            "liveSync": true,
            "syncOnSave": false,
            "encrypt": true
        }"#,
    )
    .unwrap();
    // An `.icloud` placeholder among the root's direct children arms
    // the iCloud detector without needing a real `$HOME` prefix/xattr.
    fs::write(root.join("Attachment.pdf.icloud"), "").unwrap();
    dir
}

#[test]
fn sync_check_lists_livesync_and_icloud_with_warning() {
    let vault = seed_livesync_and_icloud_vault();
    let out = slate()
        .arg("sync-check")
        .arg(vault.path())
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "sync-check");

    let providers = data["providers"].as_array().unwrap();
    let kinds: Vec<&str> = providers
        .iter()
        .map(|p| p["kind"].as_str().unwrap())
        .collect();
    assert!(kinds.contains(&"livesync"), "livesync detected: {kinds:?}");
    assert!(
        kinds.contains(&"icloud-drive"),
        "icloud-drive detected: {kinds:?}"
    );

    // Two providers of risk >= Medium → non-null multi-sync warning.
    assert!(
        data["multi_sync_warning"].is_string(),
        "warning populated: {:?}",
        data["multi_sync_warning"]
    );

    // Config parsed with the credential-free subset.
    assert_eq!(data["livesync_config"]["status"], "parsed");
    assert_eq!(
        data["livesync_config"]["server_host"],
        "couch.example.com:5984"
    );
}

/// The credential-safety gate: no planted secret ever appears in output.
#[test]
fn sync_check_never_leaks_credentials() {
    let vault = seed_livesync_and_icloud_vault();
    for format in ["json", "human", "tsv"] {
        let output = slate()
            .arg("sync-check")
            .arg(vault.path())
            .arg("--format")
            .arg(format)
            .assert()
            .success()
            .get_output()
            .clone();
        let combined = format!(
            "{}{}",
            String::from_utf8_lossy(&output.stdout),
            String::from_utf8_lossy(&output.stderr)
        );
        for secret in ["alice", "hunter2", "secret123", "user:pass"] {
            assert!(
                !combined.contains(secret),
                "format {format}: leaked credential {secret:?}"
            );
        }
    }
}

#[test]
fn sync_check_clean_vault_is_empty_and_exit_zero() {
    let vault = seed_basic_vault();
    let out = slate()
        .arg("sync-check")
        .arg(vault.path())
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "sync-check");
    assert!(
        data["providers"].as_array().unwrap().is_empty(),
        "no providers on a clean vault"
    );
    assert!(data["multi_sync_warning"].is_null());
    assert_eq!(data["livesync_config"]["status"], "not-present");
}

// --- error paths ------------------------------------------------------

#[test]
fn nonexistent_path_exits_one_with_message() {
    slate()
        .arg("open")
        .arg("/no/such/vault/path/anywhere")
        .assert()
        .code(1)
        .stderr(predicate::str::contains("not a vault directory"))
        .stderr(predicate::str::starts_with("slate: "));
}

#[test]
fn sync_check_nonexistent_path_exits_one() {
    slate()
        .arg("sync-check")
        .arg("/no/such/vault/path/anywhere")
        .assert()
        .code(1)
        .stderr(predicate::str::contains("not a vault directory"));
}

#[test]
fn unknown_flag_exits_two() {
    let vault = seed_basic_vault();
    slate()
        .arg("open")
        .arg(vault.path())
        .arg("--nonsense-flag")
        .assert()
        .code(2);
}

#[test]
fn no_subcommand_exits_two() {
    slate().assert().code(2);
}

// --- stdout/stderr separation ----------------------------------------

/// Even with progress/warnings possible on stderr, `--format json`
/// stdout is pure JSON. (Progress is TTY-gated and won't fire under the
/// test's piped stderr, but the invariant — stdout carries data only —
/// is what we assert.)
#[test]
fn json_stdout_parses_independently_of_stderr() {
    let vault = seed_basic_vault();
    let out = slate()
        .arg("open")
        .arg(vault.path())
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let text = std::str::from_utf8(&out).unwrap();
    serde_json::from_str::<Value>(text).expect("stdout is standalone valid JSON");
}

// --- SIGINT / cancellation (unix only) -------------------------------

/// Generate a vault with `n` markdown files, enough that the initial
/// scan takes long enough to interrupt mid-flight.
#[cfg(unix)]
fn seed_large_vault(n: usize) -> TempDir {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    // Spread across subdirectories so no single directory has 5k
    // entries (kinder to the filesystem, still one flat scan).
    for i in 0..n {
        let sub = root.join(format!("d{:02}", i % 50));
        if i < 50 {
            fs::create_dir_all(&sub).unwrap();
        }
        let body = format!("# Note {i}\n\nBody line one.\nBody line two.\nBody line three.\n");
        fs::write(sub.join(format!("note{i}.md")), body).unwrap();
    }
    dir
}

/// A vault reopens clean after a SIGINT'd scan — no cache corruption.
#[cfg(unix)]
fn assert_reopens_clean(vault: &Path) {
    slate()
        .arg("open")
        .arg(vault)
        .arg("--format")
        .arg("json")
        .assert()
        .success();
}

/// Outcome of one interrupted `slate open` attempt.
#[cfg(unix)]
enum SigintOutcome {
    /// The handler caught the signal and main exited 130 gracefully —
    /// the assertion target.
    Graceful130,
    /// The scan raced to completion before the signal landed (exit 0).
    /// Honest, but not what we want to assert on; retry with an earlier
    /// interrupt.
    RacedToCompletion,
    /// The signal landed in the tiny window *before* `ctrlc::set_handler`
    /// armed the handler, so SIGINT took its default disposition and
    /// killed the process by signal (not a product bug — a freshly
    /// spawned child can be CPU-starved under parallel test load). Retry
    /// with a later interrupt to let the handler arm.
    PreHandlerSignal,
}

/// Spawn `slate open <vault>`, interrupt it `delay_ms` in, and classify
/// the outcome. Factored out so the test can retry across the
/// handler-arming race without duplicating the plumbing.
#[cfg(unix)]
fn open_then_sigint(bin: &Path, vault: &Path, delay_ms: u64) -> SigintOutcome {
    use std::os::unix::process::ExitStatusExt;
    use std::process::{Command as StdCommand, Stdio};
    use std::thread::sleep;
    use std::time::Duration;

    let mut child = StdCommand::new(bin)
        .arg("open")
        .arg(vault)
        .arg("--format")
        .arg("json")
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .spawn()
        .expect("spawn slate open");

    sleep(Duration::from_millis(delay_ms));
    let killed = StdCommand::new("kill")
        .arg("-INT")
        .arg(child.id().to_string())
        .status()
        .expect("run kill -INT");
    assert!(killed.success(), "kill -INT delivered");

    let status = child.wait().expect("wait on slate open");
    match (status.code(), status.signal()) {
        (Some(130), _) => SigintOutcome::Graceful130,
        (Some(0), _) => SigintOutcome::RacedToCompletion,
        (None, Some(libc::SIGINT)) => SigintOutcome::PreHandlerSignal,
        (code, signal) => panic!("unexpected slate open exit: code={code:?} signal={signal:?}"),
    }
}

/// SIGINT during a slow `open` scan exits 130 via the graceful cancel
/// path, and the vault reopens clean afterward (no cache corruption).
///
/// The interrupt is delivered mid-scan. Two benign races are retried
/// rather than asserted against: the scan finishing before the signal
/// (exit 0), and the signal landing before the Ctrl-C handler arms
/// (signal-2 death — a freshly spawned child can be CPU-starved under
/// parallel test load, widening the pre-handler window). Each retry
/// nudges the interrupt timing; across a few attempts the graceful 130
/// path is reached deterministically. The load-independent invariant —
/// the cache is never corrupted by the interrupt — is asserted on every
/// attempt via the reopen.
#[cfg(unix)]
#[test]
fn sigint_during_open_exits_130_and_reopens_clean() {
    let vault = seed_large_vault(5_000);
    let bin = assert_cmd::cargo::cargo_bin("slate");

    // Escalate the interrupt delay across attempts: a longer delay lets
    // a starved child arm its handler (fixing PreHandlerSignal); the
    // 5k-file scan outlasts every delay here, so RacedToCompletion is
    // itself rare. The first delay honors the spec's "~50ms in".
    let delays = [50, 150, 300, 500, 750];
    let mut reached_graceful = false;
    for delay in delays {
        // Start each attempt from a cold cache so the scan is slow
        // enough to interrupt; a leftover warm cache would make the
        // "scan" a fast no-op and always race to completion.
        let _ = fs::remove_dir_all(vault.path().join(".slate"));
        match open_then_sigint(&bin, vault.path(), delay) {
            SigintOutcome::Graceful130 => {
                reached_graceful = true;
                // The interrupt must never corrupt the cache: reopen
                // against the INTERRUPTED cache (Codoki PR #637 High —
                // deleting it first would mask corruption and reduce
                // the assertion to "a fresh scan works"). The cache
                // file must still be present for this to prove
                // anything.
                assert!(
                    vault.path().join(".slate/cache.sqlite").exists(),
                    "interrupted open should leave the cache file behind"
                );
                assert_reopens_clean(vault.path());
                break;
            }
            // Benign races: the reopen still proves no corruption, then
            // retry with a later interrupt.
            SigintOutcome::RacedToCompletion | SigintOutcome::PreHandlerSignal => {
                assert_reopens_clean(vault.path());
            }
        }
    }

    assert!(
        reached_graceful,
        "no attempt reached the graceful exit-130 cancel path across delays {delays:?}"
    );
}

// =====================================================================
// M-5 query commands: search, read, list, links, properties (#536)
// =====================================================================

/// A fixture vault exercising links, properties, and searchable content.
///
/// Layout:
/// - `hub.md` — frontmatter (`author: Alice`, `tags: [x, y]`); links to
///   `notes/target.md` (wikilink), `missing` (unresolved wikilink),
///   `![[pic.png]]` (embed, unresolved), and an external markdown link.
///   Body contains the search term `kumquat`.
/// - `notes/target.md` — frontmatter (`author: Bob`, `status: done`);
///   the backlink target of `hub.md`.
/// - `orphan.md` — frontmatter only, no links either way (the "isolated
///   note" case for `links`).
/// - `data.txt` — non-markdown file (for the `--markdown-only` filter and
///   the `list` count).
fn seed_query_vault() -> TempDir {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    fs::create_dir_all(root.join("notes")).unwrap();
    fs::write(
        root.join("hub.md"),
        "---\nauthor: Alice\ntags:\n  - x\n  - y\n---\n# Hub\n\n\
         Body mentions kumquat. See [[notes/target]], [[missing]], \
         ![[pic.png]], and [ext](https://example.com).\n",
    )
    .unwrap();
    fs::write(
        root.join("notes/target.md"),
        "---\nauthor: Bob\nstatus: done\n---\n# Target\n\nPlain body.\n",
    )
    .unwrap();
    fs::write(
        root.join("orphan.md"),
        "---\nauthor: Carol\n---\n# Orphan\n\nNo links here.\n",
    )
    .unwrap();
    fs::write(root.join("data.txt"), "not markdown, no kumquat\n").unwrap();
    dir
}

// --- `search` ---------------------------------------------------------

#[test]
fn search_human_prints_path_colon_snippet() {
    let vault = seed_query_vault();
    slate()
        .arg("search")
        .arg(vault.path())
        .arg("kumquat")
        .assert()
        .success()
        .stdout(predicate::str::contains("hub.md:"))
        .stdout(predicate::str::contains("kumquat"));
}

#[test]
fn search_json_strips_markers_and_yields_ranges() {
    let vault = seed_query_vault();
    let out = slate()
        .arg("search")
        .arg(vault.path())
        .arg("kumquat")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "search");
    assert_eq!(data["truncated"], false);
    let hits = data["hits"].as_array().unwrap();
    assert!(!hits.is_empty(), "at least one hit");
    let hit = &hits[0];
    let snippet = hit["snippet"].as_str().unwrap();
    // STX/ETX markers never reach the consumer.
    assert!(
        !snippet.contains('\u{0002}') && !snippet.contains('\u{0003}'),
        "markers must be stripped: {snippet:?}"
    );
    // A match range points at `kumquat` in the plain snippet.
    let ranges = hit["match_ranges"].as_array().unwrap();
    assert!(!ranges.is_empty(), "at least one match range");
    let start = ranges[0]["start"].as_u64().unwrap() as usize;
    let end = ranges[0]["end"].as_u64().unwrap() as usize;
    assert_eq!(&snippet[start..end], "kumquat");
    assert!(hit["score"].is_number());
    assert_eq!(hit["path"], "hub.md");
}

#[test]
fn search_tsv_has_header_and_row() {
    let vault = seed_query_vault();
    slate()
        .arg("search")
        .arg(vault.path())
        .arg("kumquat")
        .arg("--format")
        .arg("tsv")
        .assert()
        .success()
        .stdout(predicate::str::contains("path\tsnippet\tscore"))
        .stdout(predicate::str::contains("hub.md\t"));
}

#[test]
fn search_limit_one_sets_truncated() {
    // Two files match, `--limit 1` keeps one and flags truncation.
    let dir = TempDir::new().unwrap();
    fs::write(dir.path().join("a.md"), "banana one\n").unwrap();
    fs::write(dir.path().join("b.md"), "banana two\n").unwrap();
    let out = slate()
        .arg("search")
        .arg(dir.path())
        .arg("banana")
        .arg("--limit")
        .arg("1")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "search");
    assert_eq!(data["truncated"], true);
    assert_eq!(data["hits"].as_array().unwrap().len(), 1);
}

#[test]
fn search_fts_syntax_error_exits_one_with_message() {
    let vault = seed_query_vault();
    slate()
        .arg("search")
        .arg(vault.path())
        // An unbalanced quote is an FTS5 syntax error → InvalidQuery.
        .arg("\"unbalanced")
        .assert()
        .code(1)
        .stderr(predicate::str::contains("invalid search query"))
        .stderr(predicate::str::starts_with("slate: "));
}

// =====================================================================
// Milestone N2-3: `query` runs .base views and saved queries (#701)
// =====================================================================

fn seed_bases_cli_vault() -> TempDir {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    fs::create_dir_all(root.join("Queries")).unwrap();
    fs::create_dir_all(root.join("Notes")).unwrap();
    fs::write(
        root.join("Queries/Reading.base"),
        r#"views:
  - type: table
    name: Reading
    filters: "file.inFolder(\"Notes\")"
    order:
      - file.name
      - status
  - type: table
    name: Done
    filters:
      and:
        - "file.inFolder(\"Notes\")"
        - "status == \"done\""
    order:
      - file.name
      - status
"#,
    )
    .unwrap();
    fs::write(
        root.join("Notes/Alpha.md"),
        "---\nstatus: active\n---\n# Alpha\n",
    )
    .unwrap();
    fs::write(
        root.join("Notes/Beta.md"),
        "---\nstatus: done\n---\n# Beta\n",
    )
    .unwrap();
    dir
}

fn seed_saved_done_query(vault: &Path) {
    let session = VaultSession::from_filesystem(vault.to_path_buf()).expect("open vault");
    session.scan_initial(&CancelToken::new()).unwrap();
    let source = fs::read_to_string(vault.join("Queries/Reading.base")).unwrap();
    let (base, warnings) = bases::parse_base(&source);
    assert!(warnings.is_empty(), "{warnings:?}");
    let query_json = serde_json::to_string(&bases::view_query(&base, 1)).unwrap();
    session
        .save_query(
            "Done notes",
            Some("CLI fixture"),
            &query_json,
            SavedQuerySourceSyntax::Builder,
        )
        .unwrap();
}

#[test]
fn query_base_defaults_to_json_first_view_rows_by_label() {
    let vault = seed_bases_cli_vault();
    let out = slate()
        .arg("query")
        .arg(vault.path())
        .arg("--base")
        .arg("Queries/Reading.base")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "query");
    let rows = data.as_array().expect("query data is a row array");
    assert_eq!(rows.len(), 2);
    assert_eq!(rows[0]["path"], "Notes/Alpha.md");
    assert_eq!(rows[0]["file.name"], "Alpha.md");
    assert_eq!(rows[0]["status"], "active");
    assert_eq!(rows[1]["path"], "Notes/Beta.md");
    assert_eq!(rows[1]["file.name"], "Beta.md");
    assert_eq!(rows[1]["status"], "done");
}

#[test]
fn query_base_csv_and_markdown_reuse_export_renderers() {
    let vault = seed_bases_cli_vault();
    slate()
        .arg("query")
        .arg(vault.path())
        .arg("--base")
        .arg("Queries/Reading.base")
        .arg("--view")
        .arg("Done")
        .arg("--format")
        .arg("csv")
        .assert()
        .success()
        .stdout("file.name,status\r\nBeta.md,done\r\n");

    slate()
        .arg("query")
        .arg(vault.path())
        .arg("--base")
        .arg("Queries/Reading.base")
        .arg("--view")
        .arg("Done")
        .arg("--format")
        .arg("markdown")
        .assert()
        .success()
        .stdout("| file.name | status |\n| --- | --- |\n| Beta.md | done |\n");
}

#[test]
fn query_saved_runs_query_by_name() {
    let vault = seed_bases_cli_vault();
    seed_saved_done_query(vault.path());

    let out = slate()
        .arg("query")
        .arg(vault.path())
        .arg("--saved")
        .arg("Done notes")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "query");
    let rows = data.as_array().expect("query data is a row array");
    assert_eq!(rows.len(), 1);
    assert_eq!(rows[0]["path"], "Notes/Beta.md");
    assert_eq!(rows[0]["file.name"], "Beta.md");
    assert_eq!(rows[0]["status"], "done");
}

#[test]
fn query_limit_and_this_context_shape_json_rows() {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    fs::create_dir_all(root.join("Queries")).unwrap();
    fs::create_dir_all(root.join("Notes")).unwrap();
    fs::write(root.join("Notes/Alpha.md"), "# Alpha\n").unwrap();
    fs::write(root.join("Notes/Beta.md"), "# Beta\n").unwrap();
    fs::write(
        root.join("Queries/This.base"),
        r#"formulas:
  current: "this.file.name"
views:
  - type: table
    name: This
    order:
      - file.name
      - formula.current
"#,
    )
    .unwrap();

    let out = slate()
        .arg("query")
        .arg(root)
        .arg("--base")
        .arg("Queries/This.base")
        .arg("--this")
        .arg("Notes/Alpha.md")
        .arg("--limit")
        .arg("1")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "query");
    let rows = data.as_array().expect("query data is a row array");
    assert_eq!(rows.len(), 1);
    assert_eq!(rows[0]["path"], "Notes/Alpha.md");
    assert_eq!(rows[0]["formula.current"], "Alpha.md");
}

#[test]
fn query_unknown_saved_name_exits_two_with_available_saved_queries() {
    let vault = seed_bases_cli_vault();
    seed_saved_done_query(vault.path());

    let output = slate()
        .arg("query")
        .arg(vault.path())
        .arg("--saved")
        .arg("Missing saved query")
        .arg("--format")
        .arg("json")
        .assert()
        .code(2)
        .get_output()
        .clone();
    assert!(
        output.stderr.is_empty(),
        "saved-query selection errors stay machine-readable on stdout"
    );
    let data = assert_envelope(&output.stdout, "query");
    assert_eq!(data["error"]["kind"], "unknown_saved_query");
    assert_eq!(
        data["available_saved_queries"],
        serde_json::json!(["Done notes"])
    );
}

#[test]
fn query_requires_base_or_saved_before_opening_vault() {
    let vault = seed_bases_cli_vault();
    slate()
        .arg("query")
        .arg(vault.path())
        .assert()
        .code(2)
        .stdout(predicate::str::is_empty())
        .stderr(predicate::str::contains("--base"));

    assert!(
        !vault.path().join(".slate/cache.sqlite").exists(),
        "usage errors must not open or scan the vault"
    );
}

#[test]
fn query_view_is_base_only_before_opening_vault() {
    let vault = seed_bases_cli_vault();
    slate()
        .arg("query")
        .arg(vault.path())
        .arg("--saved")
        .arg("Done notes")
        .arg("--view")
        .arg("Done")
        .assert()
        .code(2)
        .stdout(predicate::str::is_empty())
        .stderr(predicate::str::contains("--view"));

    assert!(
        !vault.path().join(".slate/cache.sqlite").exists(),
        "usage errors must not open or scan the vault"
    );
}

#[test]
fn query_unknown_view_exits_two_with_available_views_envelope() {
    let vault = seed_bases_cli_vault();
    let output = slate()
        .arg("query")
        .arg(vault.path())
        .arg("--base")
        .arg("Queries/Reading.base")
        .arg("--view")
        .arg("Missing")
        .arg("--format")
        .arg("json")
        .assert()
        .code(2)
        .get_output()
        .clone();
    assert!(
        output.stderr.is_empty(),
        "json query errors stay machine-readable on stdout"
    );
    let data = assert_envelope(&output.stdout, "query");
    assert_eq!(data["error"]["kind"], "unknown_view");
    assert!(
        data["error"]["message"]
            .as_str()
            .unwrap()
            .contains("Missing")
    );
    assert_eq!(
        data["available_views"],
        serde_json::json!(["Reading", "Done"])
    );
}

#[test]
fn query_view_error_exits_two_with_error_envelope() {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    fs::create_dir_all(root.join("Queries")).unwrap();
    fs::write(root.join("note.md"), "# Note\n").unwrap();
    fs::write(
        root.join("Queries/Broken.base"),
        r#"views:
  - type: table
    name: Broken
    filters: "unknownFn(1)"
    order:
      - file.name
"#,
    )
    .unwrap();

    let output = slate()
        .arg("query")
        .arg(root)
        .arg("--base")
        .arg("Queries/Broken.base")
        .arg("--format")
        .arg("json")
        .assert()
        .code(2)
        .get_output()
        .clone();
    assert!(
        output.stderr.is_empty(),
        "view errors stay machine-readable on stdout"
    );
    let data = assert_envelope(&output.stdout, "query");
    assert_eq!(data["error"]["kind"], "view_error");
    assert!(
        data["error"]["message"]
            .as_str()
            .unwrap()
            .contains("unknown function"),
        "{data:#?}"
    );
}

// --- `read` -----------------------------------------------------------

#[test]
fn read_human_prints_verbatim_content() {
    let vault = seed_query_vault();
    slate()
        .arg("read")
        .arg(vault.path())
        .arg("notes/target.md")
        .assert()
        .success()
        .stdout(predicate::str::contains("# Target"))
        .stdout(predicate::str::contains("Plain body."));
}

/// The §M-5 verbatim contract, byte-exact: a note that ends in `\n`
/// prints with exactly that `\n` — no extra blank line from a shared
/// line terminator (codex adversarial finding, round 1).
#[test]
fn read_human_stdout_is_byte_verbatim_with_trailing_newline() {
    let dir = TempDir::new().unwrap();
    let content = "# T\n\nbody line\n";
    fs::write(dir.path().join("n.md"), content).unwrap();
    let out = slate()
        .arg("read")
        .arg(dir.path())
        .arg("n.md")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    assert_eq!(
        std::str::from_utf8(&out).unwrap(),
        content,
        "human stdout must be the file bytes exactly"
    );
}

/// The §M-5 verbatim contract, byte-exact: a note WITHOUT a trailing
/// newline must not gain one.
#[test]
fn read_human_stdout_is_byte_verbatim_without_trailing_newline() {
    let dir = TempDir::new().unwrap();
    let content = "no trailing newline here";
    fs::write(dir.path().join("n.md"), content).unwrap();
    let out = slate()
        .arg("read")
        .arg(dir.path())
        .arg("n.md")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    assert_eq!(
        std::str::from_utf8(&out).unwrap(),
        content,
        "no terminator may be appended to a verbatim body"
    );
}

#[test]
fn read_json_has_path_and_content() {
    let vault = seed_query_vault();
    let out = slate()
        .arg("read")
        .arg(vault.path())
        .arg("notes/target.md")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "read");
    assert_eq!(data["path"], "notes/target.md");
    let content = data["content"].as_str().unwrap();
    assert!(content.starts_with("---\nauthor: Bob"));
    assert!(content.contains("# Target"));
}

#[test]
fn read_rejects_tsv_with_exit_two() {
    let vault = seed_query_vault();
    slate()
        .arg("read")
        .arg(vault.path())
        .arg("notes/target.md")
        .arg("--format")
        .arg("tsv")
        .assert()
        .code(2)
        .stderr(predicate::str::contains("tsv not supported for read"));
}

#[test]
fn read_missing_note_exits_one_with_no_such_note() {
    let vault = seed_query_vault();
    slate()
        .arg("read")
        .arg(vault.path())
        .arg("does/not/exist.md")
        .assert()
        .code(1)
        .stderr(predicate::str::contains("no such note: does/not/exist.md"));
}

// --- `list` -----------------------------------------------------------

#[test]
fn list_human_one_path_per_line() {
    let vault = seed_query_vault();
    slate()
        .arg("list")
        .arg(vault.path())
        .assert()
        .success()
        .stdout(predicate::str::contains("hub.md"))
        .stdout(predicate::str::contains("notes/target.md"))
        .stdout(predicate::str::contains("data.txt"));
}

#[test]
fn list_json_carries_slim_file_shape() {
    let vault = seed_query_vault();
    let out = slate()
        .arg("list")
        .arg(vault.path())
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "list");
    let files = data["files"].as_array().unwrap();
    // 4 files: hub.md, notes/target.md, orphan.md, data.txt.
    assert_eq!(files.len(), 4, "all files listed: {files:?}");
    let hub = files
        .iter()
        .find(|f| f["path"] == "hub.md")
        .expect("hub.md present");
    let mut keys = hub.as_object().unwrap().keys().cloned().collect::<Vec<_>>();
    keys.sort();
    assert_eq!(
        keys,
        vec!["mtime_ms", "name", "path", "size_bytes"],
        "the enriched core/FFI record must not widen slate.cli.v1's manual list shape"
    );
    assert_eq!(hub["name"], "hub.md");
    assert!(hub["size_bytes"].is_number());
    assert!(hub["mtime_ms"].is_number());
}

#[test]
fn list_markdown_only_excludes_non_markdown() {
    let vault = seed_query_vault();
    let out = slate()
        .arg("list")
        .arg(vault.path())
        .arg("--markdown-only")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "list");
    let paths: Vec<&str> = data["files"]
        .as_array()
        .unwrap()
        .iter()
        .map(|f| f["path"].as_str().unwrap())
        .collect();
    assert!(
        !paths.contains(&"data.txt"),
        "non-markdown excluded: {paths:?}"
    );
    assert!(paths.contains(&"hub.md"));
}

#[test]
fn list_tsv_has_columns() {
    let vault = seed_query_vault();
    slate()
        .arg("list")
        .arg(vault.path())
        .arg("--format")
        .arg("tsv")
        .assert()
        .success()
        .stdout(predicate::str::contains("path\tname\tsize_bytes\tmtime_ms"))
        .stdout(predicate::str::contains("hub.md\thub.md\t"));
}

// --- `links` ----------------------------------------------------------

#[test]
fn links_json_backlinks_and_outgoing_shapes() {
    let vault = seed_query_vault();
    // Backlinks of notes/target.md: hub.md links to it.
    let out = slate()
        .arg("links")
        .arg(vault.path())
        .arg("notes/target.md")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "links");
    assert_eq!(data["path"], "notes/target.md");
    let backlinks = data["backlinks"].as_array().unwrap();
    assert_eq!(backlinks.len(), 1, "hub.md backlinks target");
    assert_eq!(backlinks[0]["source_path"], "hub.md");
    assert!(
        backlinks[0]["snippet"].is_string(),
        "snippet is a non-optional String"
    );
}

#[test]
fn links_json_outgoing_flags_and_unresolved_and_embed() {
    let vault = seed_query_vault();
    // Outgoing links of hub.md carry the flag set.
    let out = slate()
        .arg("links")
        .arg(vault.path())
        .arg("hub.md")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "links");
    let outgoing = data["outgoing"].as_array().unwrap();

    // Resolved wikilink → notes/target.md.
    let resolved = outgoing
        .iter()
        .find(|o| o["resolved_path"] == "notes/target.md")
        .expect("resolved wikilink present");
    assert_eq!(resolved["kind"], "wikilink");
    assert_eq!(resolved["unresolved"], false);

    // Unresolved wikilink `[[missing]]`.
    let unresolved = outgoing
        .iter()
        .find(|o| o["target"] == "missing")
        .expect("unresolved wikilink present");
    assert_eq!(unresolved["unresolved"], true);
    assert!(unresolved["resolved_path"].is_null());

    // Embed `![[pic.png]]` carries the embed flag.
    let embed = outgoing
        .iter()
        .find(|o| o["embed"] == true)
        .expect("embed present");
    assert_eq!(embed["embed"], true);

    // External markdown link carries the external flag.
    let external = outgoing
        .iter()
        .find(|o| o["external"] == true)
        .expect("external link present");
    assert_eq!(external["external"], true);
}

#[test]
fn links_human_blocks_and_suffixes() {
    let vault = seed_query_vault();
    slate()
        .arg("links")
        .arg(vault.path())
        .arg("hub.md")
        .assert()
        .success()
        .stdout(predicate::str::contains("Backlinks (0):"))
        .stdout(predicate::str::contains("Outgoing links ("))
        .stdout(predicate::str::contains("→ unresolved"))
        .stdout(predicate::str::contains("(embed)"));
}

#[test]
fn links_tsv_direction_rows() {
    let vault = seed_query_vault();
    slate()
        .arg("links")
        .arg(vault.path())
        .arg("notes/target.md")
        .arg("--format")
        .arg("tsv")
        .assert()
        .success()
        .stdout(predicate::str::contains(
            "direction\tpath\tkind\tembed\texternal\tunresolved",
        ))
        // Backlink row: direction=in, path=the linking file, rest empty.
        .stdout(predicate::str::contains("in\thub.md\t\t\t\t"));
}

#[test]
fn links_isolated_note_empty_blocks_exit_zero() {
    let vault = seed_query_vault();
    let out = slate()
        .arg("links")
        .arg(vault.path())
        .arg("orphan.md")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "links");
    assert!(data["backlinks"].as_array().unwrap().is_empty());
    assert!(data["outgoing"].as_array().unwrap().is_empty());
}

#[test]
fn links_missing_note_exits_one_with_no_such_note() {
    let vault = seed_query_vault();
    slate()
        .arg("links")
        .arg(vault.path())
        .arg("ghost.md")
        .assert()
        .code(1)
        .stderr(predicate::str::contains("no such note: ghost.md"));
}

// --- `properties` -----------------------------------------------------

#[test]
fn properties_lists_keys_with_counts() {
    let vault = seed_query_vault();
    let out = slate()
        .arg("properties")
        .arg(vault.path())
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "properties");
    let keys = data["keys"].as_array().unwrap();
    // author (3 files), status (1), tags (1) — key-sorted.
    let author = keys
        .iter()
        .find(|k| k["key"] == "author")
        .expect("author key present");
    assert_eq!(author["file_count"], 3);
    let tags = keys.iter().find(|k| k["key"] == "tags").unwrap();
    assert_eq!(tags["file_count"], 1);
}

#[test]
fn properties_human_key_tab_count() {
    let vault = seed_query_vault();
    slate()
        .arg("properties")
        .arg(vault.path())
        .assert()
        .success()
        .stdout(predicate::str::contains("author\t3"))
        .stdout(predicate::str::contains("tags\t1"));
}

#[test]
fn properties_key_lists_files() {
    let vault = seed_query_vault();
    let out = slate()
        .arg("properties")
        .arg(vault.path())
        .arg("--key")
        .arg("status")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "properties");
    assert_eq!(data["key"], "status");
    let files: Vec<&str> = data["files"]
        .as_array()
        .unwrap()
        .iter()
        .map(|f| f.as_str().unwrap())
        .collect();
    assert_eq!(files, vec!["notes/target.md"]);
}

#[test]
fn properties_missing_key_is_empty_exit_zero() {
    let vault = seed_query_vault();
    let out = slate()
        .arg("properties")
        .arg(vault.path())
        .arg("--key")
        .arg("nonexistent")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "properties");
    assert_eq!(data["key"], "nonexistent");
    assert!(
        data["files"].as_array().unwrap().is_empty(),
        "absence is not an error"
    );
}

// --- SIGINT during `search` (unix only) ------------------------------

/// Spawn `slate search <vault> <query>`, interrupt it `delay_ms` in, and
/// classify the outcome. Mirrors `open_then_sigint`'s race-classification
/// pattern (m_spec §M-5 test list: "SIGINT during `slate search` … →
/// exit 130", covering the search-cancellation DoD claim).
#[cfg(unix)]
fn search_then_sigint(bin: &Path, vault: &Path, query: &str, delay_ms: u64) -> SigintOutcome {
    use std::os::unix::process::ExitStatusExt;
    use std::process::{Command as StdCommand, Stdio};
    use std::thread::sleep;
    use std::time::Duration;

    let mut child = StdCommand::new(bin)
        .arg("search")
        .arg(vault)
        .arg(query)
        .arg("--format")
        .arg("json")
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .spawn()
        .expect("spawn slate search");

    sleep(Duration::from_millis(delay_ms));
    let killed = StdCommand::new("kill")
        .arg("-INT")
        .arg(child.id().to_string())
        .status()
        .expect("run kill -INT");
    assert!(killed.success(), "kill -INT delivered");

    let status = child.wait().expect("wait on slate search");
    match (status.code(), status.signal()) {
        (Some(130), _) => SigintOutcome::Graceful130,
        (Some(0), _) => SigintOutcome::RacedToCompletion,
        (None, Some(libc::SIGINT)) => SigintOutcome::PreHandlerSignal,
        (code, signal) => {
            panic!("unexpected slate search exit: code={code:?} signal={signal:?}")
        }
    }
}

/// SIGINT during a `search` exits 130 via the graceful cancel path.
///
/// The interrupt must land while the process is still working. On a
/// large vault the initial scan itself dominates the runtime, so the
/// signal most often lands mid-scan; either way the shared cancel token
/// aborts the in-flight call (scan OR the per-row search loop) and main
/// exits 130. Two benign races are retried rather than asserted against,
/// exactly as the `open` SIGINT test does: the whole run finishing
/// before the signal (exit 0), and the signal landing before the Ctrl-C
/// handler arms (signal-2 death under CPU starvation).
#[cfg(unix)]
#[test]
fn sigint_during_search_exits_130() {
    let vault = seed_large_vault(5_000);
    let bin = assert_cmd::cargo::cargo_bin("slate");

    let delays = [50, 150, 300, 500, 750];
    let mut reached_graceful = false;
    for delay in delays {
        // Cold cache each attempt so the scan (and thus the window to
        // interrupt) is real rather than a warm no-op.
        let _ = fs::remove_dir_all(vault.path().join(".slate"));
        match search_then_sigint(&bin, vault.path(), "note", delay) {
            SigintOutcome::Graceful130 => {
                reached_graceful = true;
                break;
            }
            SigintOutcome::RacedToCompletion | SigintOutcome::PreHandlerSignal => {
                // Benign: retry with a later interrupt.
            }
        }
    }

    assert!(
        reached_graceful,
        "no attempt reached the graceful exit-130 cancel path across delays {delays:?}"
    );
}

// --- slate history (#799) ------------------------------------------------

/// Seed a vault whose note has real history: three saved versions
/// through the session (the same machinery the app uses).
fn seed_history_vault() -> (TempDir, Vec<String>) {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    fs::write(root.join("n.md"), "v0\n").unwrap();
    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let mut hashes = Vec::new();
    let mut expected: Option<String> = None;
    // Multi-line bodies so the later saves are genuine edit batches (a
    // tiny file's diff payload rivals the file size and forces a
    // snapshot — the known fixture trap).
    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    for suffix in ["first version\n", "second version\n", "third version\n"] {
        let report = session
            .save_text("n.md", &format!("{body}{suffix}"), expected.as_deref())
            .unwrap();
        hashes.push(report.new_content_hash.clone());
        expected = Some(report.new_content_hash);
    }
    drop(session);
    (dir, hashes)
}

/// The list drain pages at 200; 201 versions forces a second page, so
/// the cursor handoff (generation-stamped `next_cursor` → next
/// request) is exercised end to end (Codoki suggestion on #841).
#[test]
fn history_list_drains_across_page_boundary() {
    let dir = TempDir::new().expect("tempdir");
    let root = dir.path();
    fs::write(root.join("n.md"), "v0\n").unwrap();
    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let body: String = (0..12).map(|i| format!("keep line {i}\n")).collect();
    let mut expected: Option<String> = None;
    let mut newest = String::new();
    for i in 0..201 {
        let report = session
            .save_text(
                "n.md",
                &format!("{body}revision {i}\n"),
                expected.as_deref(),
            )
            .unwrap();
        newest = report.new_content_hash.clone();
        expected = Some(report.new_content_hash);
    }
    drop(session);

    let output = slate()
        .args([
            "history",
            root.to_str().unwrap(),
            "n.md",
            "--limit",
            "201",
            "--format",
            "json",
        ])
        .assert()
        .success();
    let data = assert_envelope(&output.get_output().stdout, "history");
    let versions = data["versions"].as_array().unwrap();
    assert_eq!(versions.len(), 201);
    assert_eq!(data["total"], 201);
    assert_eq!(versions[0]["hash"], newest.as_str());
    // `position_from_tail` is distance from the newest entry, so a
    // newest-first listing ascends 0..=200 with no gap — a cursor bug
    // (skip, repeat, restart) at the page-200 boundary breaks this.
    for (i, v) in versions.iter().enumerate() {
        assert_eq!(v["position"], i as u64);
    }
}

#[test]
fn history_lists_versions_newest_first_in_all_formats() {
    let (vault, hashes) = seed_history_vault();

    let output = slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--format",
            "json",
        ])
        .assert()
        .success();
    let data = assert_envelope(&output.get_output().stdout, "history");
    let versions = data["versions"].as_array().unwrap();
    assert_eq!(versions.len(), 3);
    assert_eq!(data["total"], 3);
    // Newest first, position identity, full hashes.
    assert_eq!(versions[0]["position"], 0);
    assert_eq!(versions[0]["hash"], hashes[2].as_str());
    assert_eq!(versions[2]["hash"], hashes[0].as_str());
    assert_eq!(versions[0]["kind"], "edits");
    assert_eq!(versions[2]["kind"], "snapshot", "cold first save anchors");
    assert!(versions[0]["summary"].is_string());

    // tsv: header + 3 rows.
    let tsv = slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--format",
            "tsv",
        ])
        .assert()
        .success();
    let text = String::from_utf8(tsv.get_output().stdout.clone()).unwrap();
    assert_eq!(text.lines().count(), 4);
    assert!(text.starts_with("position\thash\ttimestamp_ms\tkind\tsummary"));

    // Human: one row per version with the short hash.
    let human = slate()
        .args(["history", vault.path().to_str().unwrap(), "n.md"])
        .assert()
        .success();
    let text = String::from_utf8(human.get_output().stdout.clone()).unwrap();
    assert_eq!(text.lines().count(), 3);
    assert!(text.contains(&hashes[2][..12]));

    // --limit truncates from the newest side.
    let limited = slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--limit",
            "1",
            "--format",
            "json",
        ])
        .assert()
        .success();
    let data = assert_envelope(&limited.get_output().stdout, "history");
    assert_eq!(data["versions"].as_array().unwrap().len(), 1);
    assert_eq!(data["versions"][0]["hash"], hashes[2].as_str());
    // `total` reports the full history count regardless of truncation
    // (the count-only probe contract: --limit 1, read total).
    assert_eq!(data["total"], 3);

    // --limit 0 is a usage error (exit 2): the core clamps a zero page
    // limit to 1, so zero is not representable — rejecting it beats
    // emitting an envelope with fabricated contents.
    slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--limit",
            "0",
        ])
        .assert()
        .failure()
        .code(2);
}

#[test]
fn history_show_prints_verified_content_verbatim() {
    let (vault, hashes) = seed_history_vault();

    let output = slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--show",
            &hashes[0],
        ])
        .assert()
        .success();
    let expected_body: String = (0..12)
        .map(|i| format!("keep line {i}\n"))
        .collect::<String>()
        + "first version\n";
    assert_eq!(
        String::from_utf8(output.get_output().stdout.clone()).unwrap(),
        expected_body,
        "verbatim bytes, no framing, no appended terminator"
    );

    // tsv is rejected up front (exit 2, the read/render-template rule).
    slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--show",
            &hashes[0],
            "--format",
            "tsv",
        ])
        .assert()
        .code(2);

    // An unknown hash is exit 1 with an informative message.
    slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--show",
            "0000000000000000000000000000000000000000000000000000000000000000",
        ])
        .assert()
        .code(1);
}

#[test]
fn history_restore_round_trips_and_conflicts_surface() {
    let (vault, hashes) = seed_history_vault();

    let output = slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--restore",
            &hashes[0],
            "--format",
            "json",
        ])
        .assert()
        .success();
    let data = assert_envelope(&output.get_output().stdout, "history");
    assert_eq!(data["restored_hash"], hashes[0].as_str());
    let expected_body: String = (0..12)
        .map(|i| format!("keep line {i}\n"))
        .collect::<String>()
        + "first version\n";
    assert_eq!(
        fs::read_to_string(vault.path().join("n.md")).unwrap(),
        expected_body,
        "the restore landed on disk"
    );
    // The restore is itself a new version (restore appends — history
    // is never rewritten).
    let list = slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--format",
            "json",
        ])
        .assert()
        .success();
    let data = assert_envelope(&list.get_output().stdout, "history");
    assert_eq!(data["total"], 4);

    // --show and --restore are mutually exclusive (clap, exit 2).
    slate()
        .args([
            "history",
            vault.path().to_str().unwrap(),
            "n.md",
            "--show",
            &hashes[0],
            "--restore",
            &hashes[1],
        ])
        .assert()
        .code(2);

    // A typo'd note path is the pinned "no such note" exit 1.
    slate()
        .args(["history", vault.path().to_str().unwrap(), "nope.md"])
        .assert()
        .code(1)
        .stderr(predicate::str::contains("no such note"));
}
