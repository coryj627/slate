// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 43 PR 22a (#940; design
//! `docs/plans/43_mac_backlog_delivery/22_descriptor_relative_session_design.md`
//! §4.4, §7): a filesystem session's interior — the SQLite cache, the
//! structural lock, `slate.json` and `.slate/prefs.json` — binds the root it
//! anchored, not whatever the pathname names a moment later.
//!
//! The seam (`install_open_swap_seam`) drives #940's residual schedule
//! deterministically from inside the open: replace the root A→B right after
//! the anchor is captured, let the interior open, then restore A before the
//! pathname re-verification. Every pathname observation then reports A, so
//! on `main` the open succeeded with B's cache, B's config and B's lock.

#![cfg(unix)]

use super::*;
use std::path::Path;

/// One vault on disk: `.slate/cache.sqlite` migrated and carrying a
/// `sentinel` row, `slate.json` naming its own templates folder (which
/// exists), and `.slate/prefs.json` with its own retention. No session has
/// opened it, so neither `.slate/structural.lock` nor an op-log exists yet.
fn seed_vault(root: &Path, sentinel: &str, templates: &str, retention_days: u32) {
    std::fs::create_dir_all(root.join(".slate")).unwrap();
    std::fs::create_dir_all(root.join(templates)).unwrap();
    std::fs::write(
        root.join("slate.json"),
        format!(r#"{{ "templates": {{ "directory": "{templates}" }} }}"#),
    )
    .unwrap();
    std::fs::write(
        root.join(".slate").join("prefs.json"),
        format!(r#"{{ "history": {{ "retention_days": {retention_days} }} }}"#),
    )
    .unwrap();
    let mut conn = db::open_database(&root.join(".slate").join("cache.sqlite"), 64).unwrap();
    db::migrate(&mut conn).unwrap();
    conn.execute_batch("CREATE TABLE sentinel (v TEXT NOT NULL);")
        .unwrap();
    conn.execute("INSERT INTO sentinel (v) VALUES (?1)", [sentinel])
        .unwrap();
}

/// The `sentinel` rows a connection sees, in insertion order.
fn rows(conn: &Connection) -> Vec<String> {
    let mut statement = conn
        .prepare("SELECT v FROM sentinel ORDER BY rowid")
        .unwrap();
    statement
        .query_map([], |row| row.get(0))
        .unwrap()
        .collect::<Result<_, _>>()
        .unwrap()
}

/// The rows in the cache that lives at `root` now, read by its real path.
fn rows_on_disk(root: &Path) -> Vec<String> {
    rows(&Connection::open(root.join(".slate").join("cache.sqlite")).unwrap())
}

fn session_rows(session: &VaultSession) -> Vec<String> {
    rows(&session.conn.lock().unwrap())
}

fn rename(from: &Path, to: &Path) {
    std::fs::rename(from, to).unwrap_or_else(|error| panic!("rename {from:?} -> {to:?}: {error}"));
}

/// #940's schedule through `from_filesystem_with_actor`, with every
/// pathname observation reporting A: the session's connection, the config
/// it read and the lock it took are A's, and nothing of B was touched.
#[test]
fn the_interior_binds_the_anchored_root_through_an_a_b_a_swap() {
    let outer = tempfile::tempdir().unwrap();
    let a = outer.path().join("vault");
    let b = outer.path().join("other");
    let hold = outer.path().join("vault-hold");
    seed_vault(&a, "A0", "TemplatesA", 11);
    seed_vault(&b, "B0", "TemplatesB", 22);

    let seam = {
        let (a, b, hold) = (a.clone(), b.clone(), hold.clone());
        install_open_swap_seam(move |point| match point {
            OpenSwapPoint::RootAnchored => {
                rename(&a, &hold);
                rename(&b, &a);
            }
            OpenSwapPoint::InteriorOpened => {
                rename(&a, &b);
                rename(&hold, &a);
            }
            OpenSwapPoint::CacheDirAnchored | OpenSwapPoint::CacheOpened => {}
        })
    };
    let session = VaultSession::from_filesystem_with_actor(a.clone(), "local")
        .expect("the re-verification passes: A is back at its path");
    drop(seam);

    assert_eq!(session_rows(&session), ["A0"], "the connection is A's");
    session
        .conn
        .lock()
        .unwrap()
        .execute("INSERT INTO sentinel (v) VALUES ('X')", [])
        .unwrap();
    assert_eq!(
        session.config.templates_dir.as_deref(),
        Some("TemplatesA"),
        "slate.json is A's"
    );
    assert_eq!(
        session
            .retention_days
            .load(std::sync::atomic::Ordering::SeqCst),
        11,
        ".slate/prefs.json is A's"
    );
    assert!(
        a.join(".slate").join("structural.lock").exists(),
        "the structural lock is A's"
    );
    drop(session);

    assert_eq!(
        rows_on_disk(&a),
        ["A0", "X"],
        "the session's row landed in A"
    );
    assert_eq!(rows_on_disk(&b), ["B0"], "B's cache is untouched");
    assert!(
        !b.join(".slate").join("structural.lock").exists(),
        "B's lock was never taken"
    );
}

/// The pathname re-verification stays (design §7, now redundant but kept):
/// a root swapped during the open and left swapped still fails it, and the
/// failed open touched nothing of the folder now at the path.
#[test]
fn a_swapped_root_during_open_still_fails_the_reverification() {
    let outer = tempfile::tempdir().unwrap();
    let a = outer.path().join("vault");
    let b = outer.path().join("other");
    let hold = outer.path().join("vault-hold");
    seed_vault(&a, "A0", "TemplatesA", 11);
    seed_vault(&b, "B0", "TemplatesB", 22);

    let seam = {
        let (a, b, hold) = (a.clone(), b.clone(), hold.clone());
        install_open_swap_seam(move |point| {
            if point == OpenSwapPoint::RootAnchored {
                rename(&a, &hold);
                rename(&b, &a);
            }
        })
    };
    let opened = VaultSession::from_filesystem_with_actor(a.clone(), "local");
    drop(seam);

    match opened {
        Err(VaultError::InvalidPath { path, reason }) => {
            assert_eq!(path, a.display().to_string());
            assert_eq!(reason, "vault root changed while the session was opening");
        }
        Err(other) => panic!("expected the re-verification refusal, got {other:?}"),
        Ok(_) => panic!("a root left swapped must fail the open"),
    }
    // B now sits at A's path; the failed open never bound it.
    assert_eq!(rows_on_disk(&a), ["B0"]);
    assert!(!a.join(".slate").join("structural.lock").exists());
    assert_eq!(rows_on_disk(&hold), ["A0"]);
}

/// The same schedule one layer in (design §4.4's companion): `VaultSession::
/// open` with an explicit cache directory, the path the CLI, the benches and
/// every `open` caller share. The cache directory is anchored once; a swap
/// of it after that cannot move the connection.
#[test]
fn open_binds_the_anchored_cache_dir_through_an_a_b_a_swap() {
    let outer = tempfile::tempdir().unwrap();
    let vault = outer.path().join("vault");
    let a = outer.path().join("cache-a");
    let b = outer.path().join("cache-b");
    let hold = outer.path().join("cache-hold");
    std::fs::create_dir_all(&vault).unwrap();
    // `seed_vault` lays the cache out under `.slate`; open the cache
    // directories themselves.
    seed_vault(&a, "A0", "TemplatesA", 11);
    seed_vault(&b, "B0", "TemplatesB", 22);
    let (a_cache, b_cache, hold_cache) = (a.join(".slate"), b.join(".slate"), hold.clone());

    let seam = {
        let (a_cache, b_cache, hold_cache) = (a_cache.clone(), b_cache.clone(), hold_cache.clone());
        install_open_swap_seam(move |point| match point {
            OpenSwapPoint::CacheDirAnchored => {
                rename(&a_cache, &hold_cache);
                rename(&b_cache, &a_cache);
            }
            OpenSwapPoint::CacheOpened => {
                rename(&a_cache, &b_cache);
                rename(&hold_cache, &a_cache);
            }
            OpenSwapPoint::RootAnchored | OpenSwapPoint::InteriorOpened => {}
        })
    };
    let session = VaultSession::open(
        Arc::new(FsVaultProvider::new(vault.clone())),
        SessionConfig::new(a_cache.clone()),
    )
    .unwrap();
    drop(seam);

    assert_eq!(session_rows(&session), ["A0"], "the connection is A's");
    assert!(a_cache.join("structural.lock").exists(), "the lock is A's");
    drop(session);
    assert_eq!(rows_on_disk(&b), ["B0"], "B's cache is untouched");
    assert!(!b_cache.join("structural.lock").exists());
}
