// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — save_text + read_oplog.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

#[test]
fn save_text_round_trips_content_through_read_text() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"old contents").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let report = session
        .save_text("note.md", "# Brand new\n\nBody.\n", None)
        .unwrap();
    assert!(!report.new_content_hash.is_empty());
    assert_eq!(
        report.new_size_bytes as usize,
        "# Brand new\n\nBody.\n".len()
    );
    assert!(report.new_mtime_ms > 0);

    let back = session.read_text("note.md").unwrap();
    assert_eq!(back, "# Brand new\n\nBody.\n");
}

#[test]
fn save_text_updates_content_hash_in_index() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"v1").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .content_hash;

    session.save_text("note.md", "v2 contents", None).unwrap();

    let after = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .content_hash;
    assert_ne!(before, after, "save must refresh the cached content hash");
    assert_eq!(after, crate::vault::content_hash(b"v2 contents"));
}

#[test]
fn save_text_replaces_headings_in_index() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"# Old heading\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .headings;
    assert_eq!(before.len(), 1);
    assert_eq!(before[0].text, "Old heading");

    session
        .save_text("note.md", "# New heading\n\n## Sub\n\n### Deeper\n", None)
        .unwrap();

    let after = session
        .get_file_metadata("note.md")
        .unwrap()
        .unwrap()
        .headings;
    let texts: Vec<&str> = after.iter().map(|h| h.text.as_str()).collect();
    assert_eq!(texts, vec!["New heading", "Sub", "Deeper"]);
}

#[test]
fn save_text_creates_files_row_for_brand_new_path() {
    // No scan_initial, no pre-existing row: save_text should
    // insert one rather than failing.
    let (_tmp, session) = make_vault(|_| {});

    let report = session
        .save_text("brand-new.md", "# Hello\n", None)
        .unwrap();
    assert!(!report.new_content_hash.is_empty());

    let md = session.get_file_metadata("brand-new.md").unwrap().unwrap();
    assert!(md.is_markdown);
    assert_eq!(md.headings.len(), 1);
    assert_eq!(md.headings[0].text, "Hello");
}

#[test]
fn save_text_indexes_brand_new_base_file() {
    let (_tmp, session) = make_vault(|_| {});

    session
        .save_text(
            "queries/Reading.base",
            "views:\n  - type: table\n    name: Reading\n",
            None,
        )
        .unwrap();

    let conn = session.conn.lock().unwrap();
    let row: (String, i64, String) = conn
        .query_row(
            "SELECT bf.name, bf.warning_count, bf.parsed_query_json
             FROM bases_files bf
             JOIN files f ON f.id = bf.file_id
             WHERE f.path = 'queries/Reading.base'",
            [],
            |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
        )
        .unwrap();
    assert_eq!(row.0, "Reading");
    assert_eq!(row.1, 0);
    assert!(row.2.contains("\"name\":\"Reading\""));
}

#[test]
fn save_text_indexes_query_fences() {
    let (_tmp, session) = make_vault(|_| {});

    session
        .save_text(
            "note.md",
            "```slate-query\nTABLE file.name\n```\n```dataviewjs\nignored\n```\n",
            None,
        )
        .unwrap();

    let conn = session.conn.lock().unwrap();
    let row: (i64, String) = conn
        .query_row(
            "SELECT bb.fence_kind, bb.source_text
             FROM bases_blocks bb
             JOIN files f ON f.id = bb.file_id
             WHERE f.path = 'note.md'",
            [],
            |row| Ok((row.get(0)?, row.get(1)?)),
        )
        .unwrap();
    assert_eq!(row.0, 1);
    assert_eq!(row.1, "TABLE file.name\n");
}

#[test]
fn save_text_rejects_empty_path_even_with_expected_hash() {
    // The conflict-check path stats the file first, which would
    // otherwise return an IO error on the vault root. Validation
    // up-front makes empty-path saves uniformly InvalidPath.
    let (_tmp, session) = make_vault(|_| {});
    for expected in [None, Some("")] {
        match session.save_text("", "x", expected) {
            Err(VaultError::InvalidPath { .. }) => {}
            other => panic!("expected InvalidPath for empty path, got {other:?}"),
        }
    }
}

#[test]
fn save_text_rejects_dot_path() {
    let (_tmp, session) = make_vault(|_| {});
    match session.save_text(".", "x", None) {
        Err(VaultError::InvalidPath { .. }) => {}
        other => panic!("expected InvalidPath for '.', got {other:?}"),
    }
}

#[test]
fn save_text_rejects_parent_traversal() {
    let (_tmp, session) = make_vault(|_| {});
    match session.save_text("../escape.md", "x", None) {
        Err(VaultError::InvalidPath { .. }) => {}
        other => panic!("expected InvalidPath for '..', got {other:?}"),
    }
}

#[test]
fn save_text_rejects_absolute_path() {
    let (_tmp, session) = make_vault(|_| {});
    match session.save_text("/etc/passwd", "x", None) {
        Err(VaultError::InvalidPath { .. }) => {}
        other => panic!("expected InvalidPath for absolute path, got {other:?}"),
    }
}

#[test]
fn save_text_rejects_dot_prefixed_components() {
    // #641 codex adversarial round 3: dot-prefixed components are the
    // internal/tool namespaces (.slate, .obsidian) the scanner never
    // indexes — a content save must not be able to plant e.g. a
    // malformed `.slate/prefs.json` that the next open re-reads as
    // internal state. Same rule structural mutations enforce via
    // validate_leaf_component.
    let (_tmp, session) = make_vault(|_| {});
    for path in [
        ".slate/prefs.json",
        ".slate/tmp/foo.md",
        ".obsidian/workspace.json",
        ".hidden.md",
        "notes/.hidden.md",
        ".git/config",
    ] {
        match session.save_text(path, "x", Some("")) {
            Err(VaultError::InvalidPath { .. }) => {}
            other => panic!("expected InvalidPath for {path:?}, got {other:?}"),
        }
    }
}

#[test]
fn save_text_refuses_oversize_contents() {
    let tmp = tempfile::tempdir().unwrap();
    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 16;
    let provider = Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, config).unwrap();

    let big = "x".repeat(32);
    match session.save_text("big.md", &big, None) {
        Err(VaultError::FileTooLarge { path, size }) => {
            assert_eq!(path, "big.md");
            assert_eq!(size, 32);
        }
        other => panic!("expected FileTooLarge, got {other:?}"),
    }
}

#[test]
fn save_text_with_no_expected_hash_saves_unconditionally() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"v1").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("note.md", "v2", None).unwrap();
    assert_eq!(session.read_text("note.md").unwrap(), "v2");
}

#[test]
fn save_text_with_matching_expected_hash_proceeds() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"v1").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let on_disk_hash = crate::vault::content_hash(b"v1");
    session
        .save_text("note.md", "v2", Some(&on_disk_hash))
        .unwrap();
    assert_eq!(session.read_text("note.md").unwrap(), "v2");
}

#[test]
fn save_text_returns_write_conflict_on_stale_expected_hash() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"v1").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Simulate an external writer that changed the file behind our
    // back: rewrite v1 → external between the (caller-supplied)
    // expected hash and the save call.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider.write_file("note.md", b"external write").unwrap();

    // Caller still thinks the file holds "v1".
    let stale = crate::vault::content_hash(b"v1");
    let current_disk = crate::vault::content_hash(b"external write");
    match session.save_text("note.md", "my version", Some(&stale)) {
        Err(VaultError::WriteConflict {
            current_content_hash,
            expected_content_hash,
            current_mtime_ms,
        }) => {
            assert_eq!(current_content_hash, current_disk);
            assert_eq!(expected_content_hash, stale);
            assert!(current_mtime_ms > 0);
        }
        other => panic!("expected WriteConflict, got {other:?}"),
    }

    // File on disk is unchanged.
    assert_eq!(session.read_text("note.md").unwrap(), "external write");
}

#[test]
fn save_text_with_empty_expected_hash_creates_new_file() {
    // "I expect this file to NOT exist yet" → expected_hash="".
    // Should succeed and create the file.
    let (_tmp, session) = make_vault(|_| {});
    let report = session.save_text("new.md", "# Hello\n", Some("")).unwrap();
    assert!(!report.new_content_hash.is_empty());
    assert_eq!(session.read_text("new.md").unwrap(), "# Hello\n");
}

#[test]
fn save_text_with_expected_hash_against_missing_file_conflicts() {
    // Caller claims the file holds a known hash, but it doesn't
    // exist on disk → conflict (current_hash = "").
    let (_tmp, session) = make_vault(|_| {});
    let stale = crate::vault::content_hash(b"v1");
    match session.save_text("ghost.md", "v2", Some(&stale)) {
        Err(VaultError::WriteConflict {
            current_content_hash,
            expected_content_hash,
            current_mtime_ms,
        }) => {
            assert_eq!(current_content_hash, "");
            assert_eq!(expected_content_hash, stale);
            assert_eq!(current_mtime_ms, 0);
        }
        other => panic!("expected WriteConflict, got {other:?}"),
    }
}

#[test]
fn read_oplog_records_one_entry_per_save() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"v0").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session.save_text("note.md", "v1", None).unwrap();
    session.save_text("note.md", "v2", None).unwrap();
    session.save_text("note.md", "v3", None).unwrap();

    let entries = session.read_oplog("note.md").unwrap();
    assert_eq!(entries.len(), 3);
    let payloads: Vec<&[u8]> = entries.iter().map(|e| e.payload_bytes.as_slice()).collect();
    assert_eq!(payloads, vec![b"v1" as &[u8], b"v2", b"v3"]);
}

#[test]
fn oplog_entry_carries_hashes_and_actor_id() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"before").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let hash_before = crate::vault::content_hash(b"before");

    session.save_text("note.md", "after", None).unwrap();

    let entries = session.read_oplog("note.md").unwrap();
    assert_eq!(entries.len(), 1);
    let entry = &entries[0];
    assert_eq!(entry.user_actor_id, "local");
    assert!(matches!(entry.op_kind, crate::OpKind::WholeFileReplace));
    assert_eq!(entry.content_hash_before, hash_before);
    assert_eq!(
        entry.content_hash_after,
        crate::vault::content_hash(b"after")
    );
    assert_eq!(entry.payload_bytes, b"after");
    assert!(entry.timestamp_ms > 0);
}

#[test]
fn from_filesystem_with_actor_stamps_the_actor_into_the_oplog() {
    // The CLI opens sessions with `user_actor_id = "cli"` so a second
    // writer's op-log entries are attributable (#641). Prove the entry
    // point honors the override end-to-end: a save through such a session
    // labels its op-log entry `"cli"`, not the host default `"local"`.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider.write_file("note.md", b"before").unwrap();

    let session =
        VaultSession::from_filesystem_with_actor(tmp.path().to_path_buf(), "cli").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("note.md", "after", None).unwrap();

    let entries = session.read_oplog("note.md").unwrap();
    let newest = entries.last().expect("one op-log entry");
    assert_eq!(newest.user_actor_id, "cli");

    // And the historic entry point still defaults to "local" (host
    // behavior must not have drifted).
    let default_session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    assert_eq!(default_session.config.user_actor_id, "local");
}

/// Race two independent sessions (separate SQLite connections — the
/// same file-based one-writer lock two separate *processes* contend on)
/// through `save_text` and return the pair of outcomes.
fn race_two_writers(
    root: &std::path::Path,
    expected: &str,
    contents: [&str; 2],
) -> Vec<Result<SaveReport, VaultError>> {
    let s1 = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    s1.scan_initial(&CancelToken::new()).unwrap();
    let s2 = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    s2.scan_initial(&CancelToken::new()).unwrap();

    let barrier = std::sync::Barrier::new(2);
    let sessions = [&s1, &s2];
    std::thread::scope(|scope| {
        let handles: Vec<_> = sessions
            .iter()
            .zip(contents)
            .map(|(session, body)| {
                let barrier = &barrier;
                scope.spawn(move || {
                    barrier.wait();
                    session.save_text("note.md", body, Some(expected))
                })
            })
            .collect();
        handles
            .into_iter()
            .map(|h| h.join().expect("writer thread"))
            .collect()
    })
}

/// Assert the race invariant: exactly one writer won, the loser got
/// `WriteConflict`, and the winner's bytes are what's on disk.
fn assert_exactly_one_winner(
    round: usize,
    root: &std::path::Path,
    results: &[Result<SaveReport, VaultError>],
    bodies: [&str; 2],
) {
    let winners = results.iter().filter(|r| r.is_ok()).count();
    assert_eq!(
        winners, 1,
        "round {round}: exactly one writer must win, got {results:?}"
    );
    let loser = results.iter().find(|r| r.is_err()).unwrap();
    assert!(
        matches!(loser, Err(VaultError::WriteConflict { .. })),
        "round {round}: the loser must be WriteConflict, got {loser:?}"
    );
    let on_disk = std::fs::read_to_string(root.join("note.md")).unwrap();
    assert!(
        bodies.contains(&on_disk.as_str()),
        "round {round}: disk carries the winner's bytes, got {on_disk:?}"
    );
}

#[test]
fn census_concurrent_saves_same_expected_hash_exactly_one_wins() {
    // #641 adversarial review (codex round 1, HIGH): the expected-hash
    // compare-and-swap must be atomic ACROSS processes, not just within
    // one session's connection mutex. Two writers both observe hash H
    // and race `save_text(expected = H)`; without the IMMEDIATE-
    // transaction critical section both could pass the disk rehash in
    // the check-to-rename window and the later rename would silently
    // win. With it, the rehash runs under SQLite's file-based one-writer
    // lock, so exactly one writer wins every round and the loser gets
    // `WriteConflict`. Two sessions on separate connections exercise the
    // very same lock two separate processes contend on.
    for round in 0..20 {
        let tmp = tempfile::tempdir().unwrap();
        let provider = FsVaultProvider::new(tmp.path().to_path_buf());
        let original = format!("original {round}");
        provider.write_file("note.md", original.as_bytes()).unwrap();
        let h = crate::vault::content_hash(original.as_bytes());

        let bodies = ["writer one wrote this", "writer two wrote this"];
        let results = race_two_writers(tmp.path(), &h, bodies);
        assert_exactly_one_winner(round, tmp.path(), &results, bodies);
    }
}

#[test]
fn census_concurrent_creates_empty_expected_exactly_one_wins() {
    // The create-race twin: both writers CAS against "no file exists"
    // (expected = ""; a missing file hashes to the empty string). The
    // same critical section guarantees exactly one creator wins; the
    // loser sees the winner's fresh bytes and gets `WriteConflict`
    // instead of clobbering them. This is what makes the CLI's
    // `--create` safe against a create/create race with the app.
    for round in 0..20 {
        let tmp = tempfile::tempdir().unwrap();
        // No initial file: both sessions index an empty vault.
        let bodies = ["creator one content", "creator two content"];
        let results = race_two_writers(tmp.path(), "", bodies);
        assert_exactly_one_winner(round, tmp.path(), &results, bodies);
    }
}

#[test]
fn save_logs_fine_grained_edit_batch_and_reconstructs() {
    // A note big enough that a one-line edit's diff is smaller than the
    // whole file → the second (Some-path) save logs an `EditBatch`, and
    // replaying snapshot+batch reproduces the file byte-for-byte (#378).
    let (_tmp, session) = make_vault(|_| {});
    let v1 = "# Title\n\nLine one of the body text.\nLine two of the body text.\n\
              Line three of the body text.\nLine four of the body text.\n\
              Line five of the body text.\n";
    let r1 = session.save_text("note.md", v1, None).unwrap();
    let v2 = v1.replace("Line two of the body text.", "Line TWO has been changed.");
    session
        .save_text("note.md", &v2, Some(&r1.new_content_hash))
        .unwrap();

    let entries = session.read_oplog("note.md").unwrap();
    assert_eq!(entries.len(), 2);
    assert!(
        matches!(entries[0].op_kind, crate::OpKind::WholeFileReplace),
        "first save of the session is a snapshot"
    );
    assert!(
        matches!(entries[1].op_kind, crate::OpKind::EditBatch),
        "a small edit in a larger note logs a fine-grained batch, got {:?}",
        entries[1].op_kind
    );
    assert_eq!(
        crate::oplog::reconstruct_at_tail(&entries).unwrap(),
        v2,
        "replaying the log must reproduce the saved file"
    );
    assert_eq!(
        entries[1].content_hash_after,
        crate::vault::content_hash(v2.as_bytes())
    );
}

#[test]
fn save_identical_content_writes_no_oplog_entry() {
    let (_tmp, session) = make_vault(|_| {});
    let v1 = "alpha\nbeta\ngamma\ndelta\nepsilon\nzeta\neta\ntheta\n";
    let r1 = session.save_text("note.md", v1, None).unwrap();
    // Re-save identical content via the Some-path: the diff is empty, so
    // the op log must not grow.
    session
        .save_text("note.md", v1, Some(&r1.new_content_hash))
        .unwrap();
    assert_eq!(
        session.read_oplog("note.md").unwrap().len(),
        1,
        "an identical save must not append an op-log entry"
    );
}

#[test]
fn save_reseeds_snapshot_when_disk_diverged_from_the_logged_tail() {
    // Warm the per-file state with one save, then change the file
    // out-of-band. The next Some-save (whose caller re-read and passes the
    // new disk hash) finds cache.last_hash_after != hash_before, so it
    // must re-anchor with a snapshot rather than append a batch onto a
    // base it can't replay — and reconstruction still matches.
    let (tmp, session) = make_vault(|_| {});
    let v1 = "one\ntwo\nthree\nfour\nfive\nsix\nseven\neight\n";
    session.save_text("note.md", v1, None).unwrap();

    // External edit: write a different body straight to disk, bypassing
    // save_text (so the in-memory append state still thinks the tail is v1).
    let external = "one\nTWO-EXTERNAL\nthree\nfour\nfive\nsix\nseven\neight\n";
    std::fs::write(tmp.path().join("note.md"), external.as_bytes()).unwrap();
    let external_hash = crate::vault::content_hash(external.as_bytes());

    // The editor re-reads, sees `external`, and saves a further edit.
    let v3 = external.replace("three", "THREE");
    session
        .save_text("note.md", &v3, Some(&external_hash))
        .unwrap();

    let entries = session.read_oplog("note.md").unwrap();
    // [snapshot(v1), snapshot(v3 re-anchor)] — the divergence forced a
    // fresh snapshot, not a batch against the stale v1 base.
    assert!(
        matches!(
            entries.last().unwrap().op_kind,
            crate::OpKind::WholeFileReplace
        ),
        "a diverged base must re-anchor with a snapshot"
    );
    assert_eq!(crate::oplog::reconstruct_at_tail(&entries).unwrap(), v3);
}

#[test]
fn read_oplog_returns_empty_for_path_not_in_index() {
    let (_tmp, session) = make_vault(|_| {});
    let entries = session.read_oplog("never-saved.md").unwrap();
    assert!(entries.is_empty());
}

#[test]
fn save_report_fields_match_post_save_state() {
    let (_tmp, session) = make_vault(|_| {});
    let report = session
        .save_text("note.md", "exactly twelve", None)
        .unwrap();
    assert_eq!(report.new_size_bytes, "exactly twelve".len() as u64);
    assert_eq!(
        report.new_content_hash,
        crate::vault::content_hash(b"exactly twelve")
    );

    let md = session.get_file_metadata("note.md").unwrap().unwrap();
    assert_eq!(md.mtime_ms, report.new_mtime_ms);
    assert_eq!(md.size_bytes, report.new_size_bytes);
    assert_eq!(md.content_hash, report.new_content_hash);
}

#[test]
fn save_text_refreshes_links_and_properties_and_fts_in_one_transaction() {
    // Acceptance from #60: "Replace headings, properties, links
    // rows for that file in the same transaction so consumers
    // never observe a half-updated index." Verifies all four
    // tables (files, headings, links, properties) plus FTS5
    // reflect the new content after a single save_text call.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("target.md", b"# Target\n").unwrap();
        p.write_file("note.md", b"# Old\n\n[[target]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Sanity: pre-save state.
    assert_eq!(fts_match_count(&session, "rewritten"), 0);

    session
        .save_text(
            "note.md",
            // Frontmatter is intentionally separated from the
            // first ATX heading by a blank line so pulldown-cmark
            // doesn't fold the closing `---` into a setext header
            // — that pre-parse interaction lives in the
            // frontmatter-stripping work-stream, not save_text.
            "---\ntags:\n  - rewritten\n  - edited\nstatus: published\n---\n\n# Rewritten\n\nNew body mentions [[target]] twice and links to [[ghost]].\n",
            None,
        )
        .unwrap();

    // FTS5 picks up the new body.
    assert!(
        fts_match_count(&session, "rewritten") >= 1,
        "FTS row must reflect the new body_text"
    );

    // Headings replaced. The old "Old" heading must be gone and
    // the new "Rewritten" heading must be present; we don't
    // assert exact-list equality because the frontmatter parse
    // can interact with heading detection in ways unrelated to
    // save_text's contract.
    let md = session.get_file_metadata("note.md").unwrap().unwrap();
    let heading_texts: Vec<String> = md.headings.iter().map(|h| h.text.clone()).collect();
    assert!(
        heading_texts.iter().any(|t| t == "Rewritten"),
        "expected 'Rewritten' in {heading_texts:?}"
    );
    assert!(
        !heading_texts.iter().any(|t| t == "Old"),
        "stale 'Old' heading should be gone, got {heading_texts:?}"
    );

    // Properties refreshed.
    let conn = session.conn.lock().unwrap();
    let prop_keys: Vec<String> = conn
        .prepare(
            "SELECT key FROM properties p
             JOIN files f ON f.id = p.file_id
             WHERE f.path = ?1
             ORDER BY key",
        )
        .unwrap()
        .query_map(rusqlite::params!["note.md"], |row| row.get(0))
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap();
    assert!(prop_keys.contains(&"tags".to_string()));
    assert!(prop_keys.contains(&"status".to_string()));

    // Links refreshed. The new body links to "target" (resolves)
    // and "ghost" (unresolved); the old single link is gone.
    let link_targets: Vec<Option<String>> = conn
        .prepare(
            "SELECT target_path FROM links l
             JOIN files f ON f.id = l.source_file_id
             WHERE f.path = ?1
             ORDER BY ordinal",
        )
        .unwrap()
        .query_map(rusqlite::params!["note.md"], |row| {
            row.get::<_, Option<String>>(0)
        })
        .unwrap()
        .collect::<Result<Vec<_>, _>>()
        .unwrap();
    assert_eq!(link_targets.len(), 2, "one [[target]] + one [[ghost]]");
    // target.md is indexed → resolves; ghost.md doesn't exist → NULL.
    let resolved_count = link_targets.iter().filter(|t| t.is_some()).count();
    let unresolved_count = link_targets.iter().filter(|t| t.is_none()).count();
    assert_eq!(resolved_count, 1);
    assert_eq!(unresolved_count, 1);
}

#[test]
fn concurrent_saves_do_not_tear_oplog_entries() {
    // Two threads each issuing many saves against the same file.
    // The session's connection mutex serializes the SQL + op-log
    // append, so the final op-log should contain exactly the
    // expected number of well-formed entries with no torn frames.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("hot.md", b"seed").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let session = Arc::new(session);

    const SAVES_PER_THREAD: usize = 50;
    let s1 = Arc::clone(&session);
    let s2 = Arc::clone(&session);
    let t1 = std::thread::spawn(move || {
        for i in 0..SAVES_PER_THREAD {
            s1.save_text("hot.md", &format!("thread-1 iter-{i}"), None)
                .unwrap();
        }
    });
    let t2 = std::thread::spawn(move || {
        for i in 0..SAVES_PER_THREAD {
            s2.save_text("hot.md", &format!("thread-2 iter-{i}"), None)
                .unwrap();
        }
    });
    t1.join().unwrap();
    t2.join().unwrap();

    let entries = session.read_oplog("hot.md").unwrap();
    assert_eq!(
        entries.len(),
        SAVES_PER_THREAD * 2,
        "every save must produce exactly one intact op-log entry"
    );
    for entry in &entries {
        assert!(matches!(entry.op_kind, crate::OpKind::WholeFileReplace));
        assert!(!entry.content_hash_after.is_empty());
        // Payload string starts with "thread-1 " or "thread-2 ".
        let payload = std::str::from_utf8(&entry.payload_bytes).unwrap();
        assert!(
            payload.starts_with("thread-1 ") || payload.starts_with("thread-2 "),
            "unexpected payload {payload:?}"
        );
    }
}
