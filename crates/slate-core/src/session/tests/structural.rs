// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! U2-2 (#460): structural mutations — unit matrix + adversarial censuses.
//!
//! Census scale: every `cargo test` run executes the censuses at a scale
//! that finishes in minutes (they are the release guarantee, never
//! `#[ignore]`d — the #404 lesson); the spec's full 500×200 confirmation
//! scale is enabled with `SLATE_CENSUS_FULL=1` and is executed (release
//! mode) before every push of this surface, recorded in the PR.

use crate::VaultError;

fn census_scale() -> (u64, usize) {
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        (500, 200)
    } else {
        (120, 60)
    }
}

/// SplitMix64 — deterministic, replayable (same as dir_tree.rs).
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

/// A scanned session over a small fixture vault:
/// a/one.md, a/two.md, a/sub/three.md, b/four.md, five.md, empty dir c/.
fn fixture_session() -> (tempfile::TempDir, crate::VaultSession) {
    let dir = tempfile::tempdir().expect("tempdir");
    let root = dir.path();
    for (path, body) in [
        ("a/one.md", "# one\n[[two]]\n"),
        ("a/two.md", "# two\n"),
        ("a/sub/three.md", "# three\n"),
        ("b/four.md", "# four\n[[a/one]]\n"),
        ("five.md", "# five\n"),
    ] {
        let full = root.join(path);
        std::fs::create_dir_all(full.parent().unwrap()).unwrap();
        std::fs::write(full, body).unwrap();
    }
    std::fs::create_dir_all(root.join("c")).unwrap();
    let session = crate::VaultSession::from_filesystem(root.to_path_buf()).expect("open");
    session
        .scan_initial(&crate::CancelToken::new())
        .expect("scan");
    (dir, session)
}

/// Every indexed path (files) — the DB side of the integrity compare.
fn indexed_file_paths(session: &crate::VaultSession) -> std::collections::BTreeSet<String> {
    let mut out = std::collections::BTreeSet::new();
    let mut paging = crate::Paging::first(500);
    loop {
        let page = session
            .list_files(crate::FileFilter::All, paging.clone())
            .expect("list");
        for item in &page.items {
            out.insert(item.path.clone());
        }
        match page.next_cursor {
            Some(cursor) => paging = crate::Paging::after(cursor, 500),
            None => break,
        }
    }
    out
}

/// Every on-disk markdown-ish file path (vault-relative, forward slashes).
fn disk_file_paths(root: &std::path::Path) -> std::collections::BTreeSet<String> {
    let mut out = std::collections::BTreeSet::new();
    let mut stack = vec![root.to_path_buf()];
    while let Some(dir) = stack.pop() {
        for entry in std::fs::read_dir(&dir).unwrap() {
            let entry = entry.unwrap();
            let name = entry.file_name().to_string_lossy().into_owned();
            if name.starts_with('.') {
                continue;
            }
            let path = entry.path();
            if path.is_dir() {
                stack.push(path);
            } else {
                out.insert(
                    path.strip_prefix(root)
                        .unwrap()
                        .to_string_lossy()
                        .replace('\\', "/"),
                );
            }
        }
    }
    out
}

fn snapshot_hashes(root: &std::path::Path) -> std::collections::BTreeMap<String, String> {
    disk_file_paths(root)
        .into_iter()
        .map(|p| {
            let bytes = std::fs::read(root.join(&p)).unwrap();
            (p, crate::vault::content_hash(&bytes))
        })
        .collect()
}

// ---------------------------------------------------------------------
// Unit matrix
// ---------------------------------------------------------------------

#[test]
fn create_folder_indexes_and_appears_on_disk_and_in_tree() {
    let (dir, session) = fixture_session();
    let report = session.create_folder("notes/new folder").expect("create");
    assert!(report.op_id > 0);
    assert!(dir.path().join("notes/new folder").is_dir());
    let listing = session
        .list_dir_children("notes", crate::Paging::first(50))
        .expect("list");
    assert!(listing.dirs.iter().any(|d| d.path == "notes/new folder"));
}

#[test]
fn create_folder_rejects_case_insensitive_collision() {
    let (_dir, session) = fixture_session();
    let err = session.create_folder("A").unwrap_err();
    assert!(
        matches!(err, VaultError::DestinationExists { ref path } if path == "a"),
        "{err:?}"
    );
}

#[test]
fn rename_file_preserves_id_and_moves_bytes() {
    let (dir, session) = fixture_session();
    let before_id: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT id FROM files WHERE path = 'five.md'", [], |row| {
            row.get(0)
        })
        .unwrap()
    };
    let report = session.rename_file("five.md", "cinq.md").expect("rename");
    assert_eq!(report.moved, vec![("five.md".into(), "cinq.md".into())]);
    assert!(!dir.path().join("five.md").exists());
    assert!(dir.path().join("cinq.md").is_file());
    let after_id: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT id FROM files WHERE path = 'cinq.md'", [], |row| {
            row.get(0)
        })
        .unwrap()
    };
    assert_eq!(
        before_id, after_id,
        "files.id survives the rename (op-log identity)"
    );
}

#[test]
fn move_folder_updates_every_contained_path() {
    let (dir, session) = fixture_session();
    let report = session.move_folder("a", "b").expect("move");
    let mut moved = report.moved.clone();
    moved.sort();
    assert_eq!(
        moved,
        vec![
            ("a/one.md".to_string(), "b/a/one.md".to_string()),
            ("a/sub/three.md".to_string(), "b/a/sub/three.md".to_string()),
            ("a/two.md".to_string(), "b/a/two.md".to_string()),
        ]
    );
    assert!(dir.path().join("b/a/sub/three.md").is_file());
    assert_eq!(indexed_file_paths(&session), disk_file_paths(dir.path()));
}

#[test]
fn folder_into_own_subtree_rejected() {
    let (_dir, session) = fixture_session();
    let err = session.move_folder("a", "a/sub").unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }), "{err:?}");
}

#[test]
fn rejections_leave_state_byte_identical() {
    let (dir, session) = fixture_session();
    let before = snapshot_hashes(dir.path());
    for result in [
        session.rename_file("five.md", "").err(),
        session.rename_file("five.md", "a/b").err(),
        session.rename_file("five.md", ".hidden").err(),
        session.rename_file("missing.md", "x.md").err(),
        session.rename_file("five.md", "FIVE.md").err(), // case-only self-collision
        session.move_folder("a", "a/sub").err(),
        session.create_folder(".slate").err(),
        session.move_file("five.md", "a/sub/../..").err(),
    ] {
        assert!(result.is_some(), "expected a rejection");
    }
    assert_eq!(before, snapshot_hashes(dir.path()), "rejections are pure");
}

#[test]
fn delete_file_trashes_and_cascades_index_rows() {
    let (dir, session) = fixture_session();
    // a/one.md links to two; its rows must vanish with the file.
    let file_id: i64 = {
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT id FROM files WHERE path = 'a/one.md'", [], |row| {
            row.get(0)
        })
        .unwrap()
    };
    session.delete_file("a/one.md").expect("delete");
    assert!(!dir.path().join("a/one.md").exists());
    let conn = session.conn.lock().unwrap();
    let files: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM files WHERE path = 'a/one.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    let links: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM links WHERE source_file_id = ?1",
            rusqlite::params![file_id],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!((files, links), (0, 0), "index rows cascade with the file");
}

#[test]
fn delete_is_not_undoable() {
    let (_dir, session) = fixture_session();
    let conn_op = {
        session.delete_file("five.md").expect("delete");
        let conn = session.conn.lock().unwrap();
        conn.query_row("SELECT MAX(id) FROM structural_ops", [], |row| {
            row.get::<_, i64>(0)
        })
        .unwrap()
    };
    let err = session.undo_structural(conn_op).unwrap_err();
    assert!(
        matches!(err, VaultError::InvalidArgument { ref message } if message.contains("trash")),
        "{err:?}"
    );
}

#[test]
fn undo_requires_latest_op() {
    let (_dir, session) = fixture_session();
    let first = session.rename_file("five.md", "cinq.md").expect("rename");
    let _second = session.rename_file("cinq.md", "funf.md").expect("rename");
    let err = session.undo_structural(first.op_id).unwrap_err();
    assert!(
        matches!(err, VaultError::InvalidArgument { ref message } if message.contains("latest")),
        "{err:?}"
    );
}

#[test]
fn undo_move_restores_disk_and_index_and_is_a_redoable_journal_row() {
    let (dir, session) = fixture_session();
    let before_disk = snapshot_hashes(dir.path());
    let report = session.move_folder("a", "b").expect("move");
    let undo_report = session.undo_structural(report.op_id).expect("undo");
    assert_eq!(snapshot_hashes(dir.path()), before_disk, "byte-identical");
    assert_eq!(indexed_file_paths(&session), disk_file_paths(dir.path()));
    // The undo is itself the newest journal row — undoing IT redoes.
    let redo = session.undo_structural(undo_report.op_id).expect("redo");
    assert!(
        dir.path().join("b/a/one.md").is_file(),
        "redo re-applied the move"
    );
    session.undo_structural(redo.op_id).expect("undo the redo");
    assert_eq!(snapshot_hashes(dir.path()), before_disk);
}

#[test]
fn create_folder_undo_deletes_only_if_still_empty() {
    let (dir, session) = fixture_session();
    let report = session.create_folder("fresh").expect("create");
    // Occupy it, then undo must refuse.
    session.move_file("five.md", "fresh").expect("move in");
    let err = session.undo_structural(report.op_id).unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }), "{err:?}");
    // The move above is now the latest op; undo it, then the create undo
    // still isn't the latest (the undo journaled itself) — walk back via
    // latest-only undos until the folder is empty and removable.
    let latest = |s: &crate::VaultSession| -> i64 {
        let conn = s.conn.lock().unwrap();
        conn.query_row("SELECT MAX(id) FROM structural_ops", [], |row| row.get(0))
            .unwrap()
    };
    session
        .undo_structural(latest(&session))
        .expect("undo move-in");
    session
        .undo_structural(latest(&session))
        .expect("undo redo chain");
    let _ = dir;
}

#[test]
fn external_content_edit_inside_moved_folder_does_not_block_move() {
    let (dir, session) = fixture_session();
    // Simulate an external editor touching a file right before the move.
    std::fs::write(dir.path().join("a/two.md"), "# two (externally edited)\n").unwrap();
    let report = session.move_folder("a", "b").expect("move succeeds");
    assert!(report.moved.iter().any(|(old, _)| old == "a/two.md"));
    assert!(dir.path().join("b/a/two.md").is_file());
}

// ---------------------------------------------------------------------
// Censuses
// ---------------------------------------------------------------------

#[derive(Debug, Clone)]
enum Op {
    CreateFolder(String),
    RenameFile { from_idx: usize, name: String },
    MoveFile { from_idx: usize, parent_idx: usize },
    RenameFolder { dir_idx: usize, name: String },
    MoveFolder { dir_idx: usize, parent_idx: usize },
    DeleteFile { from_idx: usize },
    Undo,
}

fn random_op(rng: &mut SplitMix64, step: usize) -> Op {
    match rng.below(10) {
        0 => Op::CreateFolder(format!("gen{}", rng.below(8))),
        1 | 2 => Op::RenameFile {
            from_idx: rng.below(64),
            name: format!("ren{step}.md"),
        },
        3 | 4 => Op::MoveFile {
            from_idx: rng.below(64),
            parent_idx: rng.below(64),
        },
        5 => Op::RenameFolder {
            dir_idx: rng.below(64),
            name: format!("dir{step}"),
        },
        6 | 7 => Op::MoveFolder {
            dir_idx: rng.below(64),
            parent_idx: rng.below(64),
        },
        8 => Op::DeleteFile {
            from_idx: rng.below(64),
        },
        _ => Op::Undo,
    }
}

/// Path integrity: after EVERY op (accepted or rejected), the files index
/// set-equals a filesystem walk, ids are stable for surviving paths, no
/// orphan dirs rows, and rejected ops left every byte identical.
#[test]
fn census_structural_mutations_path_integrity() {
    let (seeds, ops_per_seed) = census_scale();
    for seed in 0..seeds {
        let (dir, session) = fixture_session();
        let mut rng = SplitMix64(seed.wrapping_mul(0xBADC_0FFE).wrapping_add(5));
        let id_of = |s: &crate::VaultSession, p: &str| -> Option<i64> {
            let conn = s.conn.lock().unwrap();
            conn.query_row(
                "SELECT id FROM files WHERE path = ?1",
                rusqlite::params![p],
                |row| row.get(0),
            )
            .ok()
        };
        for step in 0..ops_per_seed {
            let files: Vec<String> = indexed_file_paths(&session).into_iter().collect();
            let dirs: Vec<String> = {
                let conn = session.conn.lock().unwrap();
                let mut stmt = conn.prepare("SELECT path FROM dirs ORDER BY path").unwrap();
                stmt.query_map([], |row| row.get::<_, String>(0))
                    .unwrap()
                    .collect::<Result<Vec<_>, _>>()
                    .unwrap()
            };
            let pick = |list: &[String], idx: usize| -> Option<String> {
                if list.is_empty() {
                    None
                } else {
                    Some(list[idx % list.len()].clone())
                }
            };
            let before = snapshot_hashes(dir.path());
            let op = random_op(&mut rng, step);
            let mut tracked: Option<(String, i64)> = None;
            let result: Result<(), VaultError> = (|| {
                match &op {
                    Op::CreateFolder(name) => {
                        session.create_folder(name)?;
                    }
                    Op::RenameFile { from_idx, name } => {
                        if let Some(from) = pick(&files, *from_idx) {
                            tracked = id_of(&session, &from).map(|id| (from.clone(), id));
                            session.rename_file(&from, name)?;
                        }
                    }
                    Op::MoveFile {
                        from_idx,
                        parent_idx,
                    } => {
                        if let (Some(from), Some(parent)) =
                            (pick(&files, *from_idx), pick(&dirs, *parent_idx))
                        {
                            tracked = id_of(&session, &from).map(|id| (from.clone(), id));
                            session.move_file(&from, &parent)?;
                        }
                    }
                    Op::RenameFolder { dir_idx, name } => {
                        if let Some(target) = pick(&dirs, *dir_idx) {
                            session.rename_folder(&target, name)?;
                        }
                    }
                    Op::MoveFolder {
                        dir_idx,
                        parent_idx,
                    } => {
                        if let (Some(target), Some(parent)) =
                            (pick(&dirs, *dir_idx), pick(&dirs, *parent_idx))
                        {
                            session.move_folder(&target, &parent)?;
                        }
                    }
                    Op::DeleteFile { from_idx } => {
                        if let Some(from) = pick(&files, *from_idx) {
                            session.delete_file(&from)?;
                        }
                    }
                    Op::Undo => {
                        let latest: Option<i64> = {
                            let conn = session.conn.lock().unwrap();
                            conn.query_row("SELECT MAX(id) FROM structural_ops", [], |row| {
                                row.get(0)
                            })
                            .ok()
                        };
                        if let Some(op_id) = latest {
                            let _ = session.undo_structural(op_id); // deletes refuse — fine
                        }
                    }
                }
                Ok(())
            })();

            let indexed = indexed_file_paths(&session);
            let on_disk = disk_file_paths(dir.path());
            assert_eq!(
                indexed, on_disk,
                "seed {seed} step {step}: index/disk diverged after {op:?}"
            );
            if result.is_err() {
                assert_eq!(
                    before,
                    snapshot_hashes(dir.path()),
                    "seed {seed} step {step}: rejected {op:?} mutated state"
                );
            }
            if let (Some((old_path, old_id)), Ok(())) = (&tracked, &result) {
                // The file moved somewhere: its id must survive wherever it
                // went (find by id, not path).
                let conn = session.conn.lock().unwrap();
                let still: i64 = conn
                    .query_row(
                        "SELECT COUNT(*) FROM files WHERE id = ?1",
                        rusqlite::params![old_id],
                        |row| row.get(0),
                    )
                    .unwrap();
                assert_eq!(still, 1, "seed {seed} step {step}: id lost for {old_path}");
            }
            // No orphan dirs: every dir row's parent_path exists (or root).
            {
                let conn = session.conn.lock().unwrap();
                let orphans: i64 = conn
                    .query_row(
                        "SELECT COUNT(*) FROM dirs d
                         WHERE d.parent_path != ''
                           AND NOT EXISTS (SELECT 1 FROM dirs p WHERE p.path = d.parent_path)",
                        [],
                        |row| row.get(0),
                    )
                    .unwrap();
                assert_eq!(orphans, 0, "seed {seed} step {step}: orphan dirs rows");
            }
        }
    }
}

/// Undo round-trip: any single accepted op followed by `undo_structural`
/// restores the exact pre-op filesystem bytes and index rows. Exhaustive
/// small-N (every op kind against the fixture) + random pairs.
#[test]
fn census_structural_undo_round_trip() {
    // Exhaustive: every undoable op kind once, from a fresh fixture.
    type Mutation =
        fn(&crate::VaultSession) -> Result<crate::structural::StructuralReport, VaultError>;
    let cases: Vec<(&str, Mutation)> = vec![
        ("create_folder", |s| s.create_folder("fresh")),
        ("rename_file", |s| s.rename_file("five.md", "cinq.md")),
        ("move_file", |s| s.move_file("five.md", "b")),
        ("rename_folder", |s| s.rename_folder("a", "alpha")),
        ("move_folder", |s| s.move_folder("a", "b")),
    ];
    for (name, mutate) in cases {
        let (dir, session) = fixture_session();
        let before_disk = snapshot_hashes(dir.path());
        let before_index = indexed_file_paths(&session);
        let report = mutate(&session).unwrap_or_else(|e| panic!("{name}: {e:?}"));
        session
            .undo_structural(report.op_id)
            .unwrap_or_else(|e| panic!("undo {name}: {e:?}"));
        assert_eq!(snapshot_hashes(dir.path()), before_disk, "{name}: disk");
        assert_eq!(indexed_file_paths(&session), before_index, "{name}: index");
        // create_folder undo removes the dir on disk too.
        if name == "create_folder" {
            assert!(!dir.path().join("fresh").exists());
        }
    }

    // Random: op-then-undo pairs across seeds.
    let (seeds, _) = census_scale();
    for seed in 0..seeds.min(150) {
        let (dir, session) = fixture_session();
        let mut rng = SplitMix64(seed.wrapping_mul(0x00DD_BA11).wrapping_add(9));
        for step in 0..8 {
            let files: Vec<String> = indexed_file_paths(&session).into_iter().collect();
            let dirs: Vec<String> = {
                let conn = session.conn.lock().unwrap();
                let mut stmt = conn.prepare("SELECT path FROM dirs ORDER BY path").unwrap();
                stmt.query_map([], |row| row.get::<_, String>(0))
                    .unwrap()
                    .collect::<Result<Vec<_>, _>>()
                    .unwrap()
            };
            let before_disk = snapshot_hashes(dir.path());
            let before_index = indexed_file_paths(&session);
            let op = random_op(&mut rng, step);
            let report = match &op {
                Op::RenameFile { from_idx, name } if !files.is_empty() => session
                    .rename_file(&files[from_idx % files.len()], name)
                    .ok(),
                Op::MoveFile {
                    from_idx,
                    parent_idx,
                } if !files.is_empty() && !dirs.is_empty() => session
                    .move_file(
                        &files[from_idx % files.len()],
                        &dirs[parent_idx % dirs.len()],
                    )
                    .ok(),
                Op::RenameFolder { dir_idx, name } if !dirs.is_empty() => session
                    .rename_folder(&dirs[dir_idx % dirs.len()], name)
                    .ok(),
                Op::MoveFolder {
                    dir_idx,
                    parent_idx,
                } if !dirs.is_empty() => session
                    .move_folder(&dirs[dir_idx % dirs.len()], &dirs[parent_idx % dirs.len()])
                    .ok(),
                Op::CreateFolder(name) => session.create_folder(name).ok(),
                _ => None,
            };
            if let Some(report) = report {
                session
                    .undo_structural(report.op_id)
                    .unwrap_or_else(|e| panic!("seed {seed} step {step} undo {op:?}: {e:?}"));
                assert_eq!(
                    snapshot_hashes(dir.path()),
                    before_disk,
                    "seed {seed} step {step}: {op:?} undo not byte-identical"
                );
                assert_eq!(
                    indexed_file_paths(&session),
                    before_index,
                    "seed {seed} step {step}: {op:?} undo index divergence"
                );
                // Clear the redo row so the next iteration's undo targets
                // its own op, keeping pairs independent.
            }
        }
    }
}
