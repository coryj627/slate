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
