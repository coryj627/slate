// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Integration tests for `slate write` (#641).
//!
//! Drives the built binary via `assert_cmd` against `tempfile` fixture
//! vaults, and — crucially for the cross-process conflict story — opens
//! the *same* vault with a second, in-process `VaultSession` that stands
//! in for the running Slate app. `slate-cli` depends on `slate-core`, so
//! the test can hold both writers open at once and prove the
//! compare-and-swap guard is real across processes.
//!
//! Coverage (the #641 test list):
//! 1. Round-trip: write via CLI → read back via CLI → bytes identical
//!    (no added trailing newline).
//! 2. Conflict matrix: (a) stale `--expect-hash` → conflict; (b) the app
//!    modifies the file after the CLI observed its hash → conflict; (c)
//!    the app holds hash H, the CLI writes, the app's `save_text` with
//!    expected H raises `WriteConflict` (app-side guard is cross-process
//!    real).
//! 3. `--create` on a missing note; missing without `--create` → exit 1.
//! 4. Op-log attribution: the newest entry after a CLI write is `"cli"`.
//! 5. json envelope schema; tsv/human shapes.
//! 6. stdin over the size cap → exit 1 informative, file unchanged.

use std::fs;
use std::path::Path;

use assert_cmd::Command;
use serde_json::Value;
use slate_core::content_hash;
use slate_core::session::{CancelToken, VaultSession};
use tempfile::TempDir;

/// The binary under test.
fn slate() -> Command {
    Command::cargo_bin("slate").expect("slate binary builds")
}

/// A vault with one existing note.
fn seed_vault_with_note(rel: &str, body: &str) -> TempDir {
    let dir = TempDir::new().expect("tempdir");
    let path = dir.path().join(rel);
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).unwrap();
    }
    fs::write(&path, body).unwrap();
    dir
}

/// An empty vault (no notes yet).
fn seed_empty_vault() -> TempDir {
    TempDir::new().expect("tempdir")
}

/// Open a second, in-process session on `root` and scan it — the
/// stand-in for the running Slate app that shares the vault. Scanning is
/// what populates the index the app-side `get_file_metadata` /
/// conflict-check discipline reads.
fn open_app_session(root: &Path) -> VaultSession {
    let session =
        VaultSession::from_filesystem(root.to_path_buf()).expect("app session opens the vault");
    session
        .scan_initial(&CancelToken::new())
        .expect("app session scans");
    session
}

/// Parse a `--format json` stdout into the `data` object after asserting
/// the envelope invariants.
fn assert_envelope(stdout: &[u8], command: &str) -> Value {
    let text = std::str::from_utf8(stdout).expect("utf8 stdout");
    let v: Value = serde_json::from_str(text).expect("stdout is valid JSON");
    assert_eq!(v["schema"], "slate.cli.v1", "schema field");
    assert_eq!(v["command"], command, "command field");
    assert!(v["vault"].is_string(), "vault field present");
    v["data"].clone()
}

// --- 1. Round-trip ----------------------------------------------------

#[test]
fn write_then_read_round_trips_verbatim_no_added_newline() {
    // Deliberately NO trailing newline: the write must be byte-exact.
    let body = "# Title\n\nBody line without trailing newline";
    let vault = seed_vault_with_note("note.md", "old contents\n");

    // Write the new body via stdin.
    let out = slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .arg("--format")
        .arg("json")
        .write_stdin(body)
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "write");
    assert_eq!(data["path"], "note.md");
    assert_eq!(data["bytes_written"], body.len() as u64);
    assert_eq!(data["content_hash"], content_hash(body.as_bytes()));

    // Read it back through the CLI (json content field is exact).
    let read_out = slate()
        .arg("read")
        .arg(vault.path())
        .arg("note.md")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let read_data = assert_envelope(&read_out, "read");
    assert_eq!(read_data["content"], body, "content round-trips exactly");

    // And the file on disk is byte-identical (no trailing-newline munge).
    let on_disk = fs::read(vault.path().join("note.md")).unwrap();
    assert_eq!(on_disk, body.as_bytes(), "on-disk bytes are verbatim");
}

// --- 2. Conflict matrix ----------------------------------------------

#[test]
fn write_with_stale_expect_hash_conflicts() {
    // (a) A stale --expect-hash → the CAS fails → exit 1 write conflict.
    let vault = seed_vault_with_note("note.md", "current\n");
    let stale = "0".repeat(64); // never the real hash
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .arg("--expect-hash")
        .arg(&stale)
        .write_stdin("new body")
        .assert()
        .code(1)
        .stderr(predicates_str_contains("write conflict"));

    // The file is untouched.
    assert_eq!(
        fs::read_to_string(vault.path().join("note.md")).unwrap(),
        "current\n"
    );
}

#[test]
fn write_conflicts_when_app_modifies_after_observation() {
    // (b) The "app changed the file after the CLI observed its hash"
    // case, made deterministic: capture the note's ORIGINAL hash, let the
    // app-side session save new content (bumping the disk hash), then run
    // the CLI with --expect-hash = the pre-modification hash. save_text
    // re-reads disk, sees the app's newer hash, and refuses.
    let original = "original body\n";
    let vault = seed_vault_with_note("note.md", original);
    let pre_hash = content_hash(original.as_bytes());

    // The app writes new content through the SAME vault (different
    // process/session).
    let app = open_app_session(vault.path());
    app.save_text("note.md", "app edited this\n", None)
        .expect("app write succeeds");
    drop(app);

    // The CLI, anchored to the stale pre-modification hash, is refused.
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .arg("--expect-hash")
        .arg(&pre_hash)
        .write_stdin("cli body")
        .assert()
        .code(1)
        .stderr(predicates_str_contains("write conflict"));

    // The app's content survived; the CLI never clobbered it.
    assert_eq!(
        fs::read_to_string(vault.path().join("note.md")).unwrap(),
        "app edited this\n"
    );
}

#[test]
fn app_side_save_conflicts_after_cli_writes_cross_process() {
    // (c) The app-side protection, proven cross-process: the app opens
    // the note and captures hash H. The CLI writes successfully. The app
    // then saves with expected = H and gets WriteConflict — the CLI's
    // write is visible to the app's conflict-check, so the app can't
    // silently clobber it either.
    let original = "shared note\n";
    let vault = seed_vault_with_note("note.md", original);

    // App opens the note and captures its hash H (via the index).
    let app = open_app_session(vault.path());
    let h = app
        .get_file_metadata("note.md")
        .expect("metadata query")
        .expect("note is indexed")
        .content_hash;
    assert_eq!(h, content_hash(original.as_bytes()));

    // The CLI writes successfully (no --expect-hash: uses the note's
    // then-current indexed hash, which still matches disk).
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .write_stdin("cli rewrote it\n")
        .assert()
        .success();

    // Now the app tries to save with its stale hash H — it must conflict.
    let err = app
        .save_text("note.md", "app trying to save\n", Some(&h))
        .expect_err("app save conflict-detects the CLI write");
    assert!(
        matches!(err, slate_core::VaultError::WriteConflict { .. }),
        "expected WriteConflict, got {err:?}"
    );

    // The CLI's content is what's on disk; the app did not overwrite it.
    assert_eq!(
        fs::read_to_string(vault.path().join("note.md")).unwrap(),
        "cli rewrote it\n"
    );
}

#[test]
fn concurrent_cli_writers_exactly_one_wins_cross_process() {
    // The genuinely-two-processes race (codex adversarial round 1): two
    // `slate write` children launched simultaneously against the same
    // vault, both anchored to the same --expect-hash. save_text's
    // IMMEDIATE-transaction critical section (cross-process, via the
    // shared cache.sqlite one-writer lock) must let exactly one win per
    // round; the loser re-hashes after the winner's rename and exits 1
    // with the write-conflict message. Without the critical section both
    // children could pass the check in the check-to-rename window and
    // the later rename would silently win (both exiting 0).
    use std::io::Write as _;
    use std::process::{Command as StdCommand, Stdio};

    let original = "shared base\n";
    let vault = seed_vault_with_note("note.md", original);
    let h = content_hash(original.as_bytes());
    let bin = assert_cmd::cargo::cargo_bin("slate");

    for round in 0..5 {
        // Reset the note so both children's --expect-hash anchor holds
        // at spawn time (an external reset; the children re-scan).
        fs::write(vault.path().join("note.md"), original).unwrap();

        let children: Vec<_> = (0..2)
            .map(|i| {
                let mut child = StdCommand::new(&bin)
                    .arg("write")
                    .arg(vault.path())
                    .arg("note.md")
                    .arg("--expect-hash")
                    .arg(&h)
                    .stdin(Stdio::piped())
                    .stdout(Stdio::null())
                    .stderr(Stdio::piped())
                    .spawn()
                    .expect("spawn slate write");
                child
                    .stdin
                    .take()
                    .expect("piped stdin")
                    .write_all(format!("writer {i}, round {round}\n").as_bytes())
                    .expect("feed stdin");
                child
            })
            .collect();

        let outcomes: Vec<_> = children
            .into_iter()
            .map(|c| c.wait_with_output().expect("child exits"))
            .collect();

        let wins = outcomes.iter().filter(|o| o.status.success()).count();
        assert_eq!(
            wins,
            1,
            "round {round}: exactly one CLI writer must win; stderr: {:?}",
            outcomes
                .iter()
                .map(|o| String::from_utf8_lossy(&o.stderr).into_owned())
                .collect::<Vec<_>>()
        );
        let loser = outcomes.iter().find(|o| !o.status.success()).unwrap();
        assert_eq!(loser.status.code(), Some(1), "round {round}: loser exits 1");
        assert!(
            String::from_utf8_lossy(&loser.stderr).contains("write conflict"),
            "round {round}: loser's stderr names the conflict"
        );
    }
}

// --- 3. --create / missing note --------------------------------------

#[test]
fn create_missing_note_succeeds_and_is_indexed() {
    let vault = seed_empty_vault();

    slate()
        .arg("write")
        .arg(vault.path())
        .arg("sub/new.md")
        .arg("--create")
        .write_stdin("fresh content\n")
        .assert()
        .success();

    // The file exists on disk.
    assert_eq!(
        fs::read_to_string(vault.path().join("sub/new.md")).unwrap(),
        "fresh content\n"
    );

    // And it's indexed: `slate read` round-trips it (read runs its own
    // existence check against the index).
    let out = slate()
        .arg("read")
        .arg(vault.path())
        .arg("sub/new.md")
        .arg("--format")
        .arg("json")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "read");
    assert_eq!(data["content"], "fresh content\n");
}

#[test]
fn missing_note_without_create_exits_1() {
    let vault = seed_empty_vault();
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("nope.md")
        .write_stdin("body")
        .assert()
        .code(1)
        .stderr(predicates_str_contains("no such note: nope.md"));

    // Nothing was created.
    assert!(!vault.path().join("nope.md").exists());
}

#[test]
fn empty_expect_hash_on_missing_without_create_exits_1_no_file() {
    // Codex adversarial round 2 (MEDIUM): core hashes a missing file to
    // "", so an empty --expect-hash would pass the CAS and mint the
    // file. The missing-note gate must win over --expect-hash: in
    // automation an empty/failed hash-lookup variable plus a typo'd
    // path must surface as "no such note", never create a note.
    let vault = seed_empty_vault();
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("typo.md")
        .arg("--expect-hash")
        .arg("")
        .write_stdin("accidental content")
        .assert()
        .code(1)
        .stderr(predicates_str_contains("no such note: typo.md"));

    assert!(
        !vault.path().join("typo.md").exists(),
        "an expect-hash must never create a file without --create"
    );
}

#[test]
fn stale_expect_hash_on_missing_without_create_is_no_such_note() {
    // Precedence pin: on a missing note without --create the answer is
    // "no such note" (the path is wrong), not "write conflict" (the
    // hash is wrong) — regardless of what --expect-hash says.
    let vault = seed_empty_vault();
    let stale = "0".repeat(64);
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("gone.md")
        .arg("--expect-hash")
        .arg(&stale)
        .write_stdin("body")
        .assert()
        .code(1)
        .stderr(predicates_str_contains("no such note: gone.md"));
    assert!(!vault.path().join("gone.md").exists());
}

#[test]
fn create_with_explicit_empty_expect_hash_succeeds_on_missing() {
    // With --create the user's empty --expect-hash is the explicit
    // "no file exists" assertion — honored verbatim.
    let vault = seed_empty_vault();
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("new.md")
        .arg("--create")
        .arg("--expect-hash")
        .arg("")
        .write_stdin("fresh\n")
        .assert()
        .success();
    assert_eq!(
        fs::read_to_string(vault.path().join("new.md")).unwrap(),
        "fresh\n"
    );
}

#[test]
fn create_with_wrong_expect_hash_on_missing_conflicts() {
    // --create with a non-empty --expect-hash asserts a pre-state that
    // doesn't hold for a missing file (its hash is "") → conflict, no
    // file minted.
    let vault = seed_empty_vault();
    let wrong = "1".repeat(64);
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("new.md")
        .arg("--create")
        .arg("--expect-hash")
        .arg(&wrong)
        .write_stdin("body")
        .assert()
        .code(1)
        .stderr(predicates_str_contains("write conflict"));
    assert!(!vault.path().join("new.md").exists());
}

#[test]
fn create_on_existing_note_is_plain_conditional_write() {
    // --create is idempotent-friendly: on an existing note it's just a
    // conditional write (uses the note's indexed hash).
    let vault = seed_vault_with_note("note.md", "v1\n");
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .arg("--create")
        .write_stdin("v2\n")
        .assert()
        .success();
    assert_eq!(
        fs::read_to_string(vault.path().join("note.md")).unwrap(),
        "v2\n"
    );
}

// --- 4. Op-log attribution -------------------------------------------

#[test]
fn cli_write_attributes_oplog_entry_to_cli_actor() {
    let vault = seed_vault_with_note("note.md", "before\n");

    slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .write_stdin("after the cli wrote\n")
        .assert()
        .success();

    // Read the op-log back through the core API in-process. The newest
    // entry must carry the "cli" actor label (all CLI sessions open with
    // user_actor_id = "cli").
    let reader = open_app_session(vault.path());
    let entries = reader.read_oplog("note.md").expect("oplog readable");
    let newest = entries.last().expect("at least one op-log entry");
    assert_eq!(
        newest.user_actor_id, "cli",
        "newest op-log entry is attributed to the CLI actor"
    );
}

// --- 5. Envelope + tsv + human shapes --------------------------------

#[test]
fn write_json_envelope_schema_and_data_shape() {
    let vault = seed_vault_with_note("note.md", "old\n");
    let body = "new body\n";
    let out = slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .arg("--format")
        .arg("json")
        .write_stdin(body)
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let data = assert_envelope(&out, "write");
    assert_eq!(data["path"], "note.md");
    assert_eq!(data["bytes_written"], body.len() as u64);
    assert_eq!(data["content_hash"], content_hash(body.as_bytes()));
}

#[test]
fn write_tsv_field_value_rows() {
    let vault = seed_vault_with_note("note.md", "old\n");
    let out = slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .arg("--format")
        .arg("tsv")
        .write_stdin("hi\n")
        .assert()
        .success()
        .get_output()
        .stdout
        .clone();
    let text = String::from_utf8(out).unwrap();
    assert!(text.contains("field\tvalue"), "tsv header row: {text:?}");
    assert!(text.contains("path\tnote.md"), "path row: {text:?}");
    assert!(text.contains("bytes_written\t3"), "bytes row: {text:?}");
}

#[test]
fn write_human_is_one_confirmation_line() {
    let vault = seed_vault_with_note("note.md", "old\n");
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .write_stdin("hi\n")
        .assert()
        .success()
        .stdout(predicates_str_contains("Wrote note.md"))
        .stdout(predicates_str_contains("3 bytes"));
}

// --- 6. stdin size cap -----------------------------------------------

#[test]
fn stdin_over_size_cap_refuses_and_leaves_file_unchanged() {
    // A payload one byte over the 50 MiB refuse threshold is rejected
    // before the vault is opened, with an informative message, and the
    // existing note is left untouched. Generating 50 MiB + 1 in memory is
    // cheap and the child's read is capped, so it refuses promptly
    // without buffering the whole pipe.
    let vault = seed_vault_with_note("note.md", "keep me\n");
    let over = vec![b'a'; (50 * 1024 * 1024) + 1];
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .write_stdin(over)
        .assert()
        .code(1)
        .stderr(predicates_str_contains("refuse threshold"));

    assert_eq!(
        fs::read_to_string(vault.path().join("note.md")).unwrap(),
        "keep me\n",
        "over-cap write leaves the file untouched"
    );
}

#[test]
fn stdin_non_utf8_refuses_and_leaves_file_unchanged() {
    // save_text takes text; a binary/garbled stdin payload is rejected on
    // the same pre-open path as the size cap, and the note is untouched.
    let vault = seed_vault_with_note("note.md", "keep me\n");
    slate()
        .arg("write")
        .arg(vault.path())
        .arg("note.md")
        .write_stdin(vec![0x68u8, 0x69, 0xFF]) // "hi" + invalid byte
        .assert()
        .code(1)
        .stderr(predicates_str_contains("not valid UTF-8"));

    assert_eq!(
        fs::read_to_string(vault.path().join("note.md")).unwrap(),
        "keep me\n",
        "refused write leaves the file untouched"
    );
}

// --- small predicate helper ------------------------------------------

/// `predicates::str::contains` wrapper, kept local so the test file
/// doesn't need the `predicates::prelude` glob (mirrors `cli.rs`).
fn predicates_str_contains(needle: &'static str) -> predicates::str::ContainsPredicate {
    predicates::str::contains(needle)
}
