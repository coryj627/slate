// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! O-1 (#539) — durable op-log identity, semantic annotations, and the
//! scan reconcile.
//!
//! The invariants under test:
//!
//! * **Never cross-attach**: a recycled `files.id` (delete-newest →
//!   recreate) must never inherit the dead file's history — the exact
//!   rowid-reuse hazard the `files.oplog_name` binding column exists
//!   to close (`census_history_never_cross_attaches`).
//! * **Cache-rebuild honesty**: deleting `cache.sqlite` and rescanning
//!   reattaches every live file's history (v2 header paths +
//!   `PathChanged` markers) and surfaces deleted files' logs as
//!   remnants (`census_reconcile_after_cache_rebuild`).
//! * **Marker hash rule / identity axiom**: a rename marker carries the
//!   log's TAIL hash even when the disk content has diverged (external
//!   edit), so every `hash_after` in a log prefix-reconstructs to bytes
//!   whose blake3 IS that hash.
//! * **Legacy no-lossier gate**: v1 `<id>.oplog` logs on an intact
//!   cache stay bound across the O-1 upgrade — with no hash
//!   precondition (externally-edited files keep their history).

use super::common::*;
use super::*;

/// SplitMix64 — deterministic, replayable (same as structural.rs).
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

/// `(seeds, steps)` for the identity censuses. Real sessions (fs +
/// SQLite per step) are heavy; default scale stays test-suite-friendly
/// and `SLATE_CENSUS_FULL=1` is the pre-push release run.
fn census_scale() -> (u64, usize) {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        (48, 80)
    } else {
        (10, 28)
    }
}

fn oplog_name_of(session: &VaultSession, path: &str) -> Option<String> {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT oplog_name FROM files WHERE path = ?1",
        rusqlite::params![path],
        |row| row.get(0),
    )
    .optional()
    .unwrap()
    .flatten()
}

fn disk_hash(root: &std::path::Path, path: &str) -> String {
    crate::vault::content_hash(&std::fs::read(root.join(path)).unwrap())
}

// --- Annotations ride the save (write-path hooks) ---------------------

#[test]
fn semantic_saves_carry_annotations() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"---\ndraft: true\n---\n- [ ] thing\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    session
        .set_property(
            "n.md",
            "status",
            crate::frontmatter::PropertyValue::Text("final".into()),
            None,
        )
        .unwrap();
    session.delete_property("n.md", "draft", None).unwrap();
    session
        .set_frontmatter_source("n.md", "status: done", None)
        .unwrap();

    let entries = session.read_oplog("n.md").unwrap();
    assert_eq!(entries.len(), 3);
    let anns: Vec<Vec<crate::oplog::OpAnnotation>> = entries
        .iter()
        .map(|e| {
            assert_eq!(e.op_kind, crate::OpKind::Annotated);
            crate::oplog::decode_annotated(&e.payload_bytes).unwrap().2
        })
        .collect();
    assert!(matches!(
        anns[0].as_slice(),
        [crate::oplog::OpAnnotation::SetProperty { key, .. }] if key == "status"
    ));
    assert!(matches!(
        anns[1].as_slice(),
        [crate::oplog::OpAnnotation::RemoveProperty { key }] if key == "draft"
    ));
    assert!(matches!(
        anns[2].as_slice(),
        [crate::oplog::OpAnnotation::FrontmatterReplace]
    ));
    // Plain saves stay unannotated (bare kinds).
    session.save_text("n.md", "plain edit\n", None).unwrap();
    let entries = session.read_oplog("n.md").unwrap();
    assert_ne!(entries.last().unwrap().op_kind, crate::OpKind::Annotated);
}

#[test]
fn no_op_semantic_save_writes_nothing() {
    // Setting a property to its current value produces identical
    // bytes → no entry, annotation dropped (documented G9 behavior).
    let (_tmp, session) = make_vault(|p| {
        p.write_file("n.md", b"---\nstatus: final\n---\nbody\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let report = session
        .save_text("n.md", "---\nstatus: final\n---\nbody\n", None)
        .unwrap();
    let before = session.read_oplog("n.md").unwrap().len();
    // The warm, hash-carrying path (the editor's shape): identical
    // output → empty diff → no entry. (The None-hash path snapshots
    // unconditionally — it never reads the old content to diff.)
    session
        .set_property(
            "n.md",
            "status",
            crate::frontmatter::PropertyValue::Text("final".into()),
            Some(&report.new_content_hash),
        )
        .unwrap();
    assert_eq!(
        session.read_oplog("n.md").unwrap().len(),
        before,
        "identical content must not grow the log even with intent attached"
    );
}

// --- PathChanged markers + the marker hash rule ------------------------

#[test]
fn rename_appends_pure_marker_with_tail_hash_after_external_edit() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("old.md", b"seed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session
        .save_text("old.md", "saved through slate\n", None)
        .unwrap();
    let tail_hash = crate::vault::content_hash(b"saved through slate\n");

    // External edit: disk hash now diverges from the log tail.
    std::fs::write(tmp.path().join("old.md"), b"external edit!\n").unwrap();
    assert_ne!(disk_hash(tmp.path(), "old.md"), tail_hash);

    session.rename_file("old.md", "new.md").unwrap();

    let entries = session.read_oplog("new.md").unwrap();
    let marker = entries.last().unwrap();
    assert_eq!(marker.op_kind, crate::OpKind::Annotated);
    let (inner_kind, inner_payload, anns) =
        crate::oplog::decode_annotated(&marker.payload_bytes).unwrap();
    assert_eq!(inner_kind, crate::OpKind::EditBatch);
    assert!(
        crate::oplog::decode_edit_batch(&inner_payload)
            .unwrap()
            .is_empty()
    );
    assert_eq!(
        anns,
        vec![crate::oplog::OpAnnotation::PathChanged {
            from: "old.md".into(),
            to: "new.md".into(),
        }]
    );
    // THE marker hash rule: tail hash, never the (diverged) disk hash.
    assert_eq!(marker.content_hash_before, tail_hash);
    assert_eq!(marker.content_hash_after, tail_hash);

    // Identity axiom over the whole log.
    for i in 0..entries.len() {
        let prefix = crate::oplog::reconstruct_at_tail(&entries[..=i]).unwrap();
        assert_eq!(
            crate::vault::content_hash(prefix.as_bytes()),
            entries[i].content_hash_after,
            "identity axiom broken at entry {i}"
        );
    }
}

#[test]
fn rename_of_never_saved_file_appends_no_marker() {
    // Scan-indexed but never saved through Slate: no log, no binding —
    // and the rename must not create either ("no history to re-path").
    let (_tmp, session) = make_vault(|p| {
        p.write_file("untouched.md", b"scanned only\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.rename_file("untouched.md", "moved.md").unwrap();
    assert!(session.read_oplog("moved.md").unwrap().is_empty());
    assert_eq!(oplog_name_of(&session, "moved.md"), None);
}

#[test]
fn folder_move_appends_markers_for_contained_files() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("dir/a.md", b"a\n").unwrap();
        p.write_file("dir/b.md", b"b\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("dir/a.md", "a saved\n", None).unwrap();
    session.save_text("dir/b.md", "b saved\n", None).unwrap();

    session.move_folder("dir", "elsewhere").unwrap();

    for path in ["elsewhere/dir/a.md", "elsewhere/dir/b.md"] {
        let entries = session.read_oplog(path).unwrap();
        let marker = entries.last().unwrap();
        let (_, _, anns) = crate::oplog::decode_annotated(&marker.payload_bytes).unwrap();
        assert!(
            matches!(
                anns.as_slice(),
                [crate::oplog::OpAnnotation::PathChanged { to, .. }] if to == path
            ),
            "expected a PathChanged marker onto {path}, got {anns:?}"
        );
    }
}

// --- Delete leaves the log + journals the stem --------------------------

#[test]
fn delete_file_leaves_oplog_in_place_and_journals_the_stem() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("doomed.md", b"seed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session
        .save_text("doomed.md", "last words\n", None)
        .unwrap();
    let stem = oplog_name_of(&session, "doomed.md").expect("bound after save");
    let log_path = crate::oplog::oplog_path_for_name(&session.config.cache_dir, &stem);
    assert!(log_path.is_file());

    session.delete_file("doomed.md").unwrap();

    // Pin: the log survives the delete (regression guard — this is the
    // deleted-file recovery substrate).
    assert!(
        log_path.is_file(),
        "delete_file must leave the .oplog in place"
    );
    // And the journal row carries the severed stem for O-3's join.
    let conn = session.conn.lock().unwrap();
    let payload: String = conn
        .query_row(
            "SELECT payload FROM structural_ops WHERE kind = 'delete_file'
             ORDER BY id DESC LIMIT 1",
            [],
            |row| row.get(0),
        )
        .unwrap();
    let parsed = crate::structural::StructuralOpPayload::from_json(&payload).unwrap();
    assert_eq!(parsed.oplog_name.as_deref(), Some(stem.as_str()));
    assert_eq!(parsed.from, "doomed.md");
}

// --- Legacy upgrade (migration 027 stamping), end to end ----------------

#[test]
fn legacy_v1_log_stays_bound_across_upgrade_even_with_external_edit() {
    // Manufacture the pre-O-1 world: a vault whose cache is at schema
    // 26 with an indexed file and a v1 `<id>.oplog`.
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("a.md"), b"legacy tail\n").unwrap();
    let cache_dir = tmp.path().join(".slate");
    std::fs::create_dir_all(&cache_dir).unwrap();
    let file_id: i64 = {
        let mut conn = crate::db::open_database(&cache_dir.join("cache.sqlite"), 512).unwrap();
        crate::db::migrate_up_to(&mut conn, 26).unwrap();
        conn.execute(
            "INSERT INTO files
              (path, name, extension, size_bytes, mtime_ms, ctime_ms,
               content_hash, parser_version, indexed_at_ms, is_markdown)
             VALUES ('a.md', 'a.md', 'md', 12, 0, 0, ?1, 1, 0, 1)",
            rusqlite::params![crate::vault::content_hash(b"legacy tail\n")],
        )
        .unwrap();
        conn.query_row("SELECT id FROM files WHERE path = 'a.md'", [], |r| r.get(0))
            .unwrap()
    };
    let legacy_entries = vec![crate::oplog::OpLogEntry {
        timestamp_ms: 1,
        user_actor_id: "legacy".into(),
        op_kind: crate::OpKind::WholeFileReplace,
        content_hash_before: String::new(),
        content_hash_after: crate::vault::content_hash(b"legacy tail\n"),
        payload_bytes: b"legacy tail\n".to_vec(),
    }];
    crate::oplog::write_v1_log_for_tests(&cache_dir, &file_id.to_string(), &legacy_entries);

    // External edit BEFORE the upgrade opens: disk ≠ log tail. The
    // no-lossier gate: adoption has no hash precondition.
    std::fs::write(tmp.path().join("a.md"), b"externally edited\n").unwrap();

    // Open with the O-1 build: migration 027 stamps the binding.
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    assert_eq!(
        oplog_name_of(&session, "a.md").as_deref(),
        Some(file_id.to_string().as_str()),
        "the legacy id-derived binding must be stamped at upgrade"
    );
    assert_eq!(
        session.read_oplog("a.md").unwrap(),
        legacy_entries,
        "history must survive the upgrade despite the diverged disk content"
    );

    // The next save re-anchors (diverged tail → snapshot), appending to
    // the SAME legacy log with its v1 header intact (no eager
    // migration).
    session
        .save_text("a.md", "post-upgrade save\n", None)
        .unwrap();
    let (header, entries) =
        crate::oplog::read_oplog_with_header(&cache_dir, &file_id.to_string()).unwrap();
    assert_eq!(header.version, 1);
    assert_eq!(entries.len(), 2);
    assert_eq!(entries[1].op_kind, crate::OpKind::WholeFileReplace);
}

// --- Reconcile: adoption, salvage, conflicts ----------------------------

#[test]
fn copied_log_is_quarantined_never_misbound() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"seed\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("a.md", "content\n", None).unwrap();
    let stem = oplog_name_of(&session, "a.md").unwrap();

    // A hand-copied duplicate of a live file's log.
    let src = crate::oplog::oplog_path_for_name(&session.config.cache_dir, &stem);
    let copy = crate::oplog::oplog_path_for_name(&session.config.cache_dir, "deadbeefcopy");
    std::fs::copy(&src, &copy).unwrap();

    session.scan_initial(&CancelToken::new()).unwrap();

    // The copy claims a's path, but a is already bound → quarantine:
    // binding unchanged, copy not adopted, and NOT presented as a
    // deleted file.
    assert_eq!(oplog_name_of(&session, "a.md").unwrap(), stem);
    assert!(
        session.remnant_logs().is_empty(),
        "a copied live-file log must never masquerade as a deleted file"
    );
    assert!(copy.is_file(), "quarantined, not deleted");
}

#[test]
fn content_salvage_binds_unique_match_and_refuses_ambiguous() {
    // Unique: a v2 log whose header path is gone but whose hash set
    // contains exactly one unbound live file's current hash → adopt.
    let (tmp, session) = make_vault(|p| {
        p.write_file("moved-target.md", b"unique content X\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(oplog_name_of(&session, "moved-target.md"), None);

    let cache_dir = &session.config.cache_dir;
    assert!(crate::oplog::try_create_log(cache_dir, "salvageme", "gone/old.md").unwrap());
    crate::oplog::append_entry(
        cache_dir,
        "salvageme",
        "gone/old.md",
        &crate::oplog::OpLogEntry {
            timestamp_ms: 1,
            user_actor_id: "t".into(),
            op_kind: crate::OpKind::WholeFileReplace,
            content_hash_before: String::new(),
            content_hash_after: crate::vault::content_hash(b"unique content X\n"),
            payload_bytes: b"unique content X\n".to_vec(),
        },
    )
    .unwrap();

    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        oplog_name_of(&session, "moved-target.md").as_deref(),
        Some("salvageme"),
        "unique content match must salvage the orphaned log"
    );

    // Ambiguous: two identical unbound files → neither adopted; the
    // log (with a known effective path naming no live file) stays a
    // remnant instead.
    std::fs::write(tmp.path().join("twin-1.md"), b"twin content\n").unwrap();
    std::fs::write(tmp.path().join("twin-2.md"), b"twin content\n").unwrap();
    assert!(crate::oplog::try_create_log(cache_dir, "ambiguous", "gone/twin.md").unwrap());
    crate::oplog::append_entry(
        cache_dir,
        "ambiguous",
        "gone/twin.md",
        &crate::oplog::OpLogEntry {
            // Recent: an epoch timestamp would be reclaimed by O-2's
            // retention sweep before it could be listed as a remnant.
            timestamp_ms: now_ms(),
            user_actor_id: "t".into(),
            op_kind: crate::OpKind::WholeFileReplace,
            content_hash_before: String::new(),
            content_hash_after: crate::vault::content_hash(b"twin content\n"),
            payload_bytes: b"twin content\n".to_vec(),
        },
    )
    .unwrap();

    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(oplog_name_of(&session, "twin-1.md"), None);
    assert_eq!(oplog_name_of(&session, "twin-2.md"), None);
    assert!(
        session
            .remnant_logs()
            .iter()
            .any(|r| r.stem == "ambiguous" && r.effective_path == "gone/twin.md"),
        "ambiguous salvage must fall through to the remnant list"
    );
}

#[test]
fn content_salvage_is_rebuild_only_never_on_an_intact_cache() {
    // The template-twin hazard: on an INTACT cache, delete a note,
    // then create a different note with byte-identical content
    // (scan-indexed, never saved). Content salvage must NOT attach the
    // dead note's history to it — the log stays a remnant.
    let tmp = tempfile::tempdir().unwrap();
    {
        let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();
        session
            .save_text("original.md", "template body\n", None)
            .unwrap();
        session.delete_file("original.md").unwrap();
    }
    // Second open: the cache EXISTS (intact), so salvage is gated off.
    std::fs::write(tmp.path().join("twin.md"), b"template body\n").unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    assert_eq!(
        oplog_name_of(&session, "twin.md"),
        None,
        "an intact cache must never content-salvage a dead note's log \
         onto a byte-identical newcomer"
    );
    assert!(
        session
            .remnant_logs()
            .iter()
            .any(|r| r.effective_path == "original.md"),
        "the dead note's log must surface as a deleted-file remnant instead"
    );
}

#[test]
fn bare_v1_orphan_after_rebuild_is_quarantined() {
    // A v1 log with no header path, no marker, and no content match:
    // nothing safe to do → invisible (quarantine), never guessed.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("bystander.md", b"innocent\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    crate::oplog::write_v1_log_for_tests(
        &session.config.cache_dir,
        "999",
        &[crate::oplog::OpLogEntry {
            timestamp_ms: 1,
            user_actor_id: "t".into(),
            op_kind: crate::OpKind::WholeFileReplace,
            content_hash_before: String::new(),
            content_hash_after: crate::vault::content_hash(b"whose?\n"),
            payload_bytes: b"whose?\n".to_vec(),
        }],
    );
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(oplog_name_of(&session, "bystander.md"), None);
    assert!(session.remnant_logs().is_empty());
}

#[test]
fn oplog_name_binding_is_unique_at_the_schema_level() {
    // Codoki (PR #790): the partial UNIQUE index makes a double-binding
    // (two files sharing one log) a constraint error — the never-cross-
    // attach invariant enforced by the schema itself.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"a\n").unwrap();
        p.write_file("b.md", b"b\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let conn = session.conn.lock().unwrap();
    conn.execute(
        "UPDATE files SET oplog_name = 'shared-stem' WHERE path = 'a.md'",
        [],
    )
    .unwrap();
    let err = conn
        .execute(
            "UPDATE files SET oplog_name = 'shared-stem' WHERE path = 'b.md'",
            [],
        )
        .unwrap_err();
    assert!(
        err.to_string().to_lowercase().contains("unique"),
        "binding a second file to the same stem must violate the index: {err}"
    );
}

#[test]
fn ensure_oplog_name_never_overwrites_an_existing_binding() {
    // Codoki (PR #790): a binding that appeared since the lookup (a
    // racing writer in another process) must win; the guarded UPDATE
    // returns the existing binding instead of clobbering it.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("raced.md", b"body\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET oplog_name = 'winner-stem' WHERE path = 'raced.md'",
            [],
        )
        .unwrap();
    }
    // A save resolves the binding through ensure_oplog_name — it must
    // append to the pre-existing binding, not mint a new stem.
    session.save_text("raced.md", "edited\n", None).unwrap();
    assert_eq!(
        oplog_name_of(&session, "raced.md").as_deref(),
        Some("winner-stem")
    );
    let entries = crate::oplog::read_oplog(&session.config.cache_dir, "winner-stem").unwrap();
    assert_eq!(entries.len(), 1, "the save landed in the winner's log");
}

// --- The two identity censuses ------------------------------------------

/// Randomized delete-newest → recreate → edit flows (the exact
/// rowid-recycling shape): the new file's history NEVER contains the
/// dead file's entries, and the dead file's log stays on disk as a
/// remnant candidate.
#[test]
fn census_history_never_cross_attaches() {
    let (seeds, steps) = census_scale();
    for seed in 0..seeds {
        let mut rng = SplitMix64(seed.wrapping_mul(0xC0FF_EE11).wrapping_add(3));
        let tmp = tempfile::tempdir().unwrap();
        let session = {
            let provider = FsVaultProvider::new(tmp.path().to_path_buf());
            provider.write_file("seed.md", b"seed\n").unwrap();
            let s = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
            s.scan_initial(&CancelToken::new()).unwrap();
            s
        };

        // Reference model: per-path expected hash chain; dead logs.
        let mut live: Vec<String> = Vec::new();
        let mut expected: std::collections::HashMap<String, Vec<String>> =
            std::collections::HashMap::new();
        let mut all_dead_hashes: std::collections::HashSet<String> =
            std::collections::HashSet::new();
        let mut dead_logs: Vec<std::path::PathBuf> = Vec::new();
        let mut counter = 0u64;

        for step in 0..steps {
            counter += 1;
            match rng.below(5) {
                // Create + first save (unique content per save so any
                // cross-attach is visible in the hash sets).
                0 | 1 => {
                    let path = format!("n{seed}-{counter}.md");
                    let content = format!("create {seed}/{counter}\n");
                    session.save_text(&path, &content, None).unwrap();
                    expected
                        .entry(path.clone())
                        .or_default()
                        .push(crate::vault::content_hash(content.as_bytes()));
                    live.push(path);
                }
                // Edit a random live file.
                2 | 3 if !live.is_empty() => {
                    let path = live[rng.below(live.len())].clone();
                    let content = format!("edit {seed}/{counter}\n");
                    session.save_text(&path, &content, None).unwrap();
                    expected
                        .get_mut(&path)
                        .unwrap()
                        .push(crate::vault::content_hash(content.as_bytes()));
                }
                // Delete the NEWEST live file (frees the max rowid —
                // the recycling flow), then immediately recreate a new
                // file, which SQLite gives the recycled id.
                _ if !live.is_empty() => {
                    let path = live.pop().unwrap();
                    let stem = oplog_name_of(&session, &path);
                    session.delete_file(&path).unwrap();
                    if let Some(stem) = stem {
                        let log =
                            crate::oplog::oplog_path_for_name(&session.config.cache_dir, &stem);
                        assert!(log.is_file(), "seed {seed} step {step}: dead log vanished");
                        dead_logs.push(log);
                    }
                    for h in expected.remove(&path).unwrap_or_default() {
                        all_dead_hashes.insert(h);
                    }

                    let new_path = format!("reborn{seed}-{counter}.md");
                    let content = format!("reborn {seed}/{counter}\n");
                    session.save_text(&new_path, &content, None).unwrap();
                    expected
                        .entry(new_path.clone())
                        .or_default()
                        .push(crate::vault::content_hash(content.as_bytes()));
                    live.push(new_path);
                }
                _ => {}
            }

            // Invariant sweep: every live file's log contains exactly
            // its own chain — never a dead file's hash.
            for path in &live {
                let entries = session.read_oplog(path).unwrap();
                let got: Vec<&str> = entries
                    .iter()
                    .map(|e| e.content_hash_after.as_str())
                    .collect();
                let want = &expected[path];
                assert_eq!(
                    got,
                    want.iter().map(String::as_str).collect::<Vec<_>>(),
                    "seed {seed} step {step}: {path} history diverged from the model"
                );
                for h in &got {
                    assert!(
                        !all_dead_hashes.contains(*h),
                        "seed {seed} step {step}: {path} inherited a dead file's entry"
                    );
                }
            }
        }

        // Dead logs are still on disk, untouched by later saves.
        for log in &dead_logs {
            assert!(log.is_file(), "seed {seed}: a dead log was removed");
        }
    }
}

/// Build a vault, edit + rename + delete across it, then delete
/// `cache.sqlite` and reopen: every live file's history reattaches
/// (verified by tail reconstruction against disk bytes) and every
/// deleted file's log lands in the remnant set with its correct
/// effective path.
#[test]
fn census_reconcile_after_cache_rebuild() {
    let (seeds, _) = census_scale();
    // Each seed builds a fresh vault; keep per-seed size fixed and let
    // the seed count scale.
    for seed in 0..seeds {
        let mut rng = SplitMix64(seed.wrapping_mul(0xBEEF_CAFE).wrapping_add(11));
        let tmp = tempfile::tempdir().unwrap();
        let root = tmp.path().to_path_buf();

        let mut expected_deleted: Vec<(String, String)> = Vec::new(); // (path, tail_hash)
        {
            let session = VaultSession::from_filesystem(root.clone()).unwrap();
            session.scan_initial(&CancelToken::new()).unwrap();

            let n_files = 4 + rng.below(4);
            let mut paths: Vec<String> = Vec::new();
            for i in 0..n_files {
                let path = format!("dir{}/f{seed}-{i}.md", i % 2);
                let mut content = format!("init {seed}/{i}\n");
                session.save_text(&path, &content, None).unwrap();
                for e in 0..(1 + rng.below(3)) {
                    content = format!("{content}edit {e}\n");
                    session.save_text(&path, &content, None).unwrap();
                }
                paths.push(path);
            }
            // Rename some (markers give the logs path continuity).
            for (i, path) in paths.iter_mut().enumerate() {
                if rng.below(3) == 0 {
                    let new_name = format!("renamed{seed}-{i}.md");
                    session.rename_file(path, &new_name).unwrap();
                    let parent = std::path::Path::new(path)
                        .parent()
                        .unwrap()
                        .to_string_lossy()
                        .to_string();
                    *path = if parent.is_empty() {
                        new_name
                    } else {
                        format!("{parent}/{new_name}")
                    };
                }
            }
            // Delete some (remnants).
            let deletions = 1 + rng.below(2);
            for _ in 0..deletions {
                let idx = rng.below(paths.len());
                let path = paths.remove(idx);
                let entries = session.read_oplog(&path).unwrap();
                let tail = entries.last().unwrap().content_hash_after.clone();
                session.delete_file(&path).unwrap();
                expected_deleted.push((path, tail));
            }
        } // session drops, flushing the DB

        // The rebuild: cache gone, logs remain.
        std::fs::remove_file(root.join(".slate/cache.sqlite")).unwrap();

        let session = VaultSession::from_filesystem(root.clone()).unwrap();
        session.scan_initial(&CancelToken::new()).unwrap();

        // Every live markdown file's history reattached and reconstructs
        // to its exact disk bytes.
        let conn = session.conn.lock().unwrap();
        let live_paths: Vec<String> = conn
            .prepare("SELECT path FROM files WHERE extension = 'md' ORDER BY path")
            .unwrap()
            .query_map([], |row| row.get(0))
            .unwrap()
            .collect::<Result<Vec<_>, _>>()
            .unwrap();
        drop(conn);
        for path in &live_paths {
            let entries = session.read_oplog(path).unwrap();
            assert!(
                !entries.is_empty(),
                "seed {seed}: {path} lost its history in the rebuild"
            );
            let reconstructed = crate::oplog::reconstruct_at_tail(&entries).unwrap();
            let on_disk = std::fs::read_to_string(root.join(path)).unwrap();
            assert_eq!(
                reconstructed, on_disk,
                "seed {seed}: {path} history reattached to the wrong bytes"
            );
        }

        // Every deletion surfaced as a remnant with its final path.
        let remnants = session.remnant_logs();
        for (path, tail) in &expected_deleted {
            let hit = remnants.iter().find(|r| &r.effective_path == path);
            let Some(hit) = hit else {
                panic!("seed {seed}: deleted {path} missing from the remnant set");
            };
            assert_eq!(
                &hit.tail_hash, tail,
                "seed {seed}: remnant for {path} carries the wrong tail"
            );
        }
    }
}
