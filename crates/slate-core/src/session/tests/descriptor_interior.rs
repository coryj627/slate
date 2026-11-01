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
            OpenSwapPoint::CacheDirAnchored
            | OpenSwapPoint::CacheOpened
            | OpenSwapPoint::BeforeSlateHeld => {}
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
            OpenSwapPoint::RootAnchored
            | OpenSwapPoint::InteriorOpened
            | OpenSwapPoint::BeforeSlateHeld => {}
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

/// Design §3.2, §7: the session's connection opens through the anchored
/// synthetic path, in WAL, and a second connection through the same anchor
/// (the compaction worker's route) shares the database. The WAL and its
/// index are siblings in the held `.slate`; after the vault folder moves,
/// both connections, and one opened only then, keep reaching that folder's
/// cache, and nothing is recreated at the old path.
#[test]
fn sqlite_wal_two_connections_through_the_anchored_path() {
    let outer = tempfile::tempdir().unwrap();
    let vault = outer.path().join("vault");
    let moved = outer.path().join("vault-moved");
    std::fs::create_dir_all(&vault).unwrap();
    let session = VaultSession::from_filesystem(vault.clone()).unwrap();
    {
        let conn = session.conn.lock().unwrap();
        let path = conn.path().unwrap_or_default().to_string();
        assert!(
            path.starts_with("/.slate-fd/") && path.ends_with("/cache.sqlite"),
            "the session's connection opened by pathname: {path:?}"
        );
        let mode: String = conn
            .query_row("PRAGMA journal_mode", [], |row| row.get(0))
            .unwrap();
        assert_eq!(mode, "wal");
        conn.execute_batch(
            "CREATE TABLE sentinel (v TEXT NOT NULL); INSERT INTO sentinel VALUES ('S1');",
        )
        .unwrap();
    }
    let second = open_worker_connection(&session.interior).unwrap();
    assert_eq!(
        rows(&second),
        ["S1"],
        "the second connection shares the database"
    );
    second
        .execute("INSERT INTO sentinel (v) VALUES ('S2')", [])
        .unwrap();
    assert_eq!(session_rows(&session), ["S1", "S2"]);
    assert!(vault.join(".slate").join("cache.sqlite-wal").exists());
    assert!(vault.join(".slate").join("cache.sqlite-shm").exists());

    rename(&vault, &moved);
    second
        .execute("INSERT INTO sentinel (v) VALUES ('S3')", [])
        .unwrap();
    session
        .conn
        .lock()
        .unwrap()
        .execute("INSERT INTO sentinel (v) VALUES ('S4')", [])
        .unwrap();
    let third = open_worker_connection(&session.interior)
        .expect("a connection opened after the move still reaches the held cache");
    assert_eq!(rows(&third), ["S1", "S2", "S3", "S4"]);
    drop((second, third));
    drop(session);
    assert!(!vault.exists(), "nothing recreated the old path");
    assert_eq!(rows_on_disk(&moved), ["S1", "S2", "S3", "S4"]);
}

/// The environment variable that tells
/// [`child_of_a_second_process_on_the_textual_path`] which cache to open.
const SECOND_PROCESS_CACHE: &str = "SLATE_PR22A_SECOND_PROCESS_CACHE";

/// Design §3.3: anchoring one process leaves the cache shared with another
/// that reaches it by its real path — the `slate` CLI beside the app, or a
/// second window. The child sees the parent's committed row and writes its
/// own; the parent's open read transaction keeps its WAL snapshot until it
/// ends, then sees the child's row.
#[test]
fn a_second_process_on_the_textual_path_shares_the_anchored_cache() {
    let tmp = tempfile::tempdir().unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let conn = session.conn.lock().unwrap();
    conn.execute_batch(
        "CREATE TABLE sentinel (v TEXT NOT NULL); INSERT INTO sentinel VALUES ('P0');",
    )
    .unwrap();
    conn.execute_batch("BEGIN DEFERRED;").unwrap();
    assert_eq!(rows(&conn), ["P0"], "the read transaction's snapshot");

    let status = std::process::Command::new(std::env::current_exe().unwrap())
        .arg("--exact")
        .arg("session::tests::descriptor_interior::child_of_a_second_process_on_the_textual_path")
        .arg("--nocapture")
        .env(
            SECOND_PROCESS_CACHE,
            tmp.path().join(".slate").join("cache.sqlite"),
        )
        .status()
        .unwrap();
    assert!(status.success(), "the second process failed: {status:?}");

    assert_eq!(
        rows(&conn),
        ["P0"],
        "the open read transaction keeps its snapshot"
    );
    conn.execute_batch("COMMIT;").unwrap();
    assert_eq!(
        rows(&conn),
        ["P0", "C1"],
        "the child's row is the session's"
    );
}

/// The second process: plain SQLite on the cache's real path, no anchor.
/// Does nothing unless its parent fact set [`SECOND_PROCESS_CACHE`].
#[test]
fn child_of_a_second_process_on_the_textual_path() {
    let Some(cache) = std::env::var_os(SECOND_PROCESS_CACHE) else {
        return;
    };
    let conn = Connection::open(cache).unwrap();
    conn.busy_timeout(std::time::Duration::from_secs(10))
        .unwrap();
    let mode: String = conn
        .query_row("PRAGMA journal_mode", [], |row| row.get(0))
        .unwrap();
    assert_eq!(mode, "wal");
    assert_eq!(rows(&conn), ["P0"], "the parent's committed row");
    conn.execute("INSERT INTO sentinel (v) VALUES ('C1')", [])
        .unwrap();
}

/// Design §6, owner decision Q5: the process-local structural-lock
/// registry is keyed by the held `.slate`'s identity. A root swapped while
/// one session holds its lock leaves the vault swapped in at the path free
/// to open — its canonical lock pathname is the very string the first slot
/// was taken under — and the first session's lock stays on its own vault.
/// After a rename, the first session still takes its lock (the old pathname
/// no longer canonicalizes), and a session opened through the new spelling
/// shares its slot.
#[test]
fn the_structural_lock_registry_is_keyed_by_identity_not_the_canonical_path() {
    use std::fs::TryLockError;
    let outer = tempfile::tempdir().unwrap();
    let a = outer.path().join("vault");
    let b = outer.path().join("other");
    let moved = outer.path().join("vault-moved");
    std::fs::create_dir_all(a.join(".slate")).unwrap();
    std::fs::create_dir_all(b.join(".slate")).unwrap();
    let first = VaultSession::from_filesystem(a.clone()).unwrap();
    let held = VaultStructuralLock::acquire(&first.interior).unwrap();

    rename(&a, &moved);
    rename(&b, &a);
    let (opened_tx, opened_rx) = std::sync::mpsc::channel();
    let at = a.clone();
    std::thread::spawn(move || {
        let _ = opened_tx.send(VaultSession::from_filesystem(at).map(drop));
    });
    opened_rx
        .recv_timeout(std::time::Duration::from_secs(20))
        .expect("the vault swapped in at the path opened while the first held its lock")
        .unwrap();
    let lock_of = |root: &Path| {
        std::fs::OpenOptions::new()
            .write(true)
            .open(root.join(".slate").join("structural.lock"))
            .unwrap()
    };
    assert!(
        matches!(lock_of(&moved).try_lock(), Err(TryLockError::WouldBlock)),
        "the first session's lock is on its own vault"
    );
    lock_of(&a)
        .try_lock()
        .expect("the swapped-in vault's lock is its own, and free");
    drop(held);

    // Put things back, then rename the first session's vault for real.
    rename(&a, &b);
    rename(&moved, &a);
    let renamed = outer.path().join("vault-renamed");
    rename(&a, &renamed);
    let again = VaultStructuralLock::acquire(&first.interior)
        .expect("the held vault takes its lock under its new name");
    assert!(
        !VaultSession::structural_lock_is_free(&first.interior),
        "the slot is held"
    );
    drop(again);
    let through_new_spelling = VaultSession::open(
        Arc::new(FsVaultProvider::new(renamed.clone())),
        SessionConfig::new(renamed.join(".slate")),
    )
    .unwrap();
    let held = VaultStructuralLock::acquire(&first.interior).unwrap();
    assert!(
        !VaultSession::structural_lock_is_free(&through_new_spelling.interior),
        "one vault, one slot, whichever spelling opened it"
    );
    drop(held);
}

/// Design §4.2, owner decision Q4 (§3.6(a)'s hazard): the display path is
/// the held root's current path while that path still names it — after a
/// rename too — and never a reported path that names another object or
/// nothing.
#[test]
fn root_display_path_is_gated_by_the_anchor() {
    use std::os::unix::fs::MetadataExt as _;
    let identity = |path: &Path| {
        std::fs::metadata(path)
            .map(|meta| (meta.dev(), meta.ino()))
            .ok()
    };
    let outer = tempfile::tempdir().unwrap();
    let vault = outer.path().join("vault");
    let moved = outer.path().join("vault-moved");
    std::fs::create_dir_all(&vault).unwrap();
    let anchor = identity(&vault);
    let session = VaultSession::from_filesystem(vault.clone()).unwrap();
    let shown = session.root_display_path().unwrap();
    assert_eq!(identity(&shown), anchor, "{shown:?}");

    rename(&vault, &moved);
    let shown = session.root_display_path().unwrap();
    assert_eq!(shown.file_name(), moved.file_name(), "{shown:?}");
    assert_eq!(identity(&shown), anchor);
    std::fs::create_dir(&vault).unwrap();
    let shown = session.root_display_path().unwrap();
    assert_eq!(
        identity(&shown),
        anchor,
        "a folder at the old path is not where the vault is: {shown:?}"
    );

    // The gate itself. A held root another folder was renamed over: its
    // descriptor's reported path names the replacement.
    let replaced = outer.path().join("replaced");
    let replacement = outer.path().join("replacement");
    std::fs::create_dir(&replaced).unwrap();
    let held = crate::vault::fs_root::FsRoot::open(&replaced).unwrap();
    std::fs::remove_dir(replaced.join(".slate")).unwrap();
    std::fs::create_dir(&replacement).unwrap();
    rename(&replacement, &replaced);
    assert_eq!(held.verified_current_path(), None);
    assert_eq!(
        held.display_path(),
        replaced,
        "the opened spelling, as a fallback"
    );
    // A held root removed outright: its reported path names nothing.
    let removed = outer.path().join("removed");
    std::fs::create_dir(&removed).unwrap();
    let held = crate::vault::fs_root::FsRoot::open(&removed).unwrap();
    std::fs::remove_dir(removed.join(".slate")).unwrap();
    std::fs::remove_dir(&removed).unwrap();
    assert_eq!(held.verified_current_path(), None);
}

/// Design §3.6(b): the interior never follows a link. A linked `.slate`,
/// `.slate/prefs.json` or `.slate/cache.sqlite` refuses the open; a linked
/// `slate.json` is not read; a linked `.slate/tmp` refuses a save and a
/// linked `.slate/oplog` refuses the op-log append, and nothing is written
/// where any link points.
#[test]
fn a_symlinked_interior_component_is_refused() {
    use std::os::unix::fs::symlink;
    let outer = tempfile::tempdir().unwrap();
    let fresh = |name: &str| {
        let root = outer.path().join(name);
        std::fs::create_dir_all(&root).unwrap();
        root
    };
    let listing = |dir: &Path| {
        let mut names: Vec<String> = std::fs::read_dir(dir)
            .unwrap()
            .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
            .collect();
        names.sort();
        names
    };

    // `.slate` itself.
    let vault = fresh("linked-slate");
    let elsewhere = fresh("elsewhere");
    symlink(&elsewhere, vault.join(".slate")).unwrap();
    match VaultSession::from_filesystem(vault.clone()) {
        Err(VaultError::InvalidPath { path, reason }) => {
            assert_eq!(path, vault.join(".slate").display().to_string());
            assert_eq!(
                reason,
                "\".slate\" is a symbolic link, not a folder in this vault, so the vault was not opened"
            );
        }
        Err(other) => panic!("expected the linked-.slate refusal, got {other:?}"),
        Ok(_) => panic!("a linked .slate was followed"),
    }
    assert!(
        listing(&elsewhere).is_empty(),
        "the link's target was written"
    );

    // `.slate/prefs.json`.
    let vault = fresh("linked-prefs");
    let elsewhere = fresh("elsewhere-prefs");
    std::fs::create_dir(vault.join(".slate")).unwrap();
    std::fs::write(
        elsewhere.join("prefs.json"),
        r#"{ "history": { "retention_days": 33 } }"#,
    )
    .unwrap();
    symlink(
        elsewhere.join("prefs.json"),
        vault.join(".slate").join("prefs.json"),
    )
    .unwrap();
    assert!(
        matches!(
            VaultSession::from_filesystem(vault.clone()),
            Err(VaultError::PrefsUnreadable { .. })
        ),
        "a linked prefs.json was followed"
    );

    // `slate.json` is not read through a link.
    let vault = fresh("linked-config");
    let elsewhere = fresh("elsewhere-config");
    std::fs::create_dir(vault.join("TemplatesX")).unwrap();
    std::fs::write(
        elsewhere.join("slate.json"),
        r#"{ "templates": { "directory": "TemplatesX" } }"#,
    )
    .unwrap();
    symlink(elsewhere.join("slate.json"), vault.join("slate.json")).unwrap();
    let session = VaultSession::from_filesystem(vault.clone()).unwrap();
    assert_eq!(
        session.config.templates_dir, None,
        "a linked slate.json was read"
    );
    drop(session);

    // `.slate/cache.sqlite`.
    let vault = fresh("linked-cache");
    std::fs::create_dir(vault.join(".slate")).unwrap();
    let foreign = fresh("foreign-cache");
    seed_vault(&foreign, "F0", "TemplatesF", 5);
    symlink(
        foreign.join(".slate").join("cache.sqlite"),
        vault.join(".slate").join("cache.sqlite"),
    )
    .unwrap();
    assert!(
        matches!(
            VaultSession::from_filesystem(vault.clone()),
            Err(VaultError::Db(_))
        ),
        "a linked cache.sqlite was opened"
    );
    assert_eq!(rows_on_disk(&foreign), ["F0"]);
    assert!(!foreign.join(".slate").join("cache.sqlite-wal").exists());

    // `.slate/tmp` and `.slate/oplog` beneath an open session.
    let vault = fresh("linked-staging");
    let staging_target = fresh("staging-target");
    let oplog_target = fresh("oplog-target");
    let session = VaultSession::from_filesystem(vault.clone()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = std::fs::remove_dir(vault.join(".slate").join("tmp"));
    symlink(&staging_target, vault.join(".slate").join("tmp")).unwrap();
    let refused = session.save_text("note.md", "through the link", None);
    assert!(
        matches!(&refused, Err(VaultError::Io(error))
            if matches!(error.raw_os_error(), Some(libc::ELOOP) | Some(libc::ENOTDIR))),
        "{refused:?}"
    );
    assert!(
        listing(&staging_target).is_empty(),
        "staged through the link"
    );
    assert!(!vault.join("note.md").exists());
    std::fs::remove_file(vault.join(".slate").join("tmp")).unwrap();
    let _ = std::fs::remove_dir_all(vault.join(".slate").join("oplog"));
    symlink(&oplog_target, vault.join(".slate").join("oplog")).unwrap();
    session
        .save_text("note.md", "saved; its history is refused", None)
        .unwrap();
    assert!(
        listing(&oplog_target).is_empty(),
        "history written through the link"
    );
}

/// #1474: after the open vault's folder is renamed, no interior writer
/// recreates the old path. A save's bytes land in the renamed folder through
/// the held root; its index step still re-reads the file by the opened
/// pathname (the provider's reads are pathname-based), so it reports the
/// typed `SavedButUnindexed` rather than `Ok` — never `Ok` while writing
/// elsewhere. A retention change writes the renamed vault's prefs, an op-log
/// append lands in the renamed vault's history, and nothing exists at the
/// old path afterwards.
#[test]
fn after_a_live_rename_no_interior_writer_recreates_the_old_path() {
    let outer = tempfile::tempdir().unwrap();
    let vault = outer.path().join("Notes");
    let moved = outer.path().join("Moved");
    std::fs::create_dir_all(&vault).unwrap();
    std::fs::write(vault.join("note.md"), "old text\n").unwrap();
    let session = VaultSession::from_filesystem(vault.clone()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session.save_text("note.md", "first save\n", None).unwrap();

    rename(&vault, &moved);
    match session.save_text("note.md", "after the rename\n", None) {
        Ok(_) | Err(VaultError::SavedButUnindexed { .. }) => {}
        Err(other) => panic!("the save must land in the renamed vault: {other:?}"),
    }
    session
        .set_history_prefs(crate::history_prefs::HistoryPrefs { retention_days: 30 })
        .unwrap();
    let entry = crate::oplog::OpLogEntry {
        timestamp_ms: 1,
        user_actor_id: "local".into(),
        op_kind: crate::oplog::OpKind::WholeFileReplace,
        content_hash_before: String::new(),
        content_hash_after: crate::vault::content_hash(b"probe"),
        payload_bytes: b"probe".to_vec(),
    };
    crate::oplog::append_entry_in(&session.interior, "probe", "note.md", &entry).unwrap();

    assert!(!vault.exists(), "an interior writer recreated the old path");
    assert_eq!(
        std::fs::read_to_string(moved.join("note.md")).unwrap(),
        "after the rename\n"
    );
    assert_eq!(
        crate::history_prefs::read_history_prefs(&moved.join(".slate").join("prefs.json")).unwrap(),
        Some(crate::history_prefs::HistoryPrefs { retention_days: 30 })
    );
    assert_eq!(
        crate::oplog::read_oplog(&moved.join(".slate"), "probe").unwrap(),
        [entry]
    );
}

/// Design §4.2 (#1077's I1 and I7 under the root descriptor, macOS): the
/// stored spelling is read beneath the root the session holds, so a root
/// swapped after the open cannot answer for the session. Nothing of the
/// vault is beneath a folder that is a link; a link at the end keeps its own
/// spelling.
/// Linux keeps the trait's identity default (divergence ID-1) and Windows
/// its pathname form until 22W.
#[cfg(target_os = "macos")]
#[test]
fn canonical_path_is_computed_under_the_root_descriptor() {
    let outer = tempfile::tempdir().unwrap();
    let a = outer.path().join("vault");
    let b = outer.path().join("other");
    let hold = outer.path().join("vault-hold");
    let outside = outer.path().join("outside");
    for (root, note) in [(&a, "Notes/Alpha.md"), (&b, "Notes/Beta.md")] {
        let path = root.join(note);
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        std::fs::write(path, "x").unwrap();
    }
    std::fs::create_dir_all(&outside).unwrap();
    std::fs::write(outside.join("x.md"), "outside").unwrap();
    std::os::unix::fs::symlink(&outside, a.join("Linked")).unwrap();
    std::os::unix::fs::symlink(a.join("Notes").join("Alpha.md"), a.join("Link.md")).unwrap();
    let session = VaultSession::from_filesystem(a.clone()).unwrap();

    rename(&a, &hold);
    rename(&b, &a);
    assert_eq!(
        session.canonical_path("Notes/Alpha.md").unwrap().as_deref(),
        Some("Notes/Alpha.md"),
        "the held vault's note, though the path names another vault"
    );
    assert_eq!(
        session.canonical_path("Notes/Beta.md").unwrap(),
        None,
        "the vault at the path is not the session's"
    );
    if hold.join("notes").join("alpha.md").exists() {
        // An aliasing volume: the stored spelling, from the held root.
        assert_eq!(
            session.canonical_path("notes/alpha.md").unwrap().as_deref(),
            Some("Notes/Alpha.md")
        );
    }
    assert_eq!(
        session.canonical_path("Linked/x.md").unwrap(),
        None,
        "nothing of the vault is beneath a linked folder (I7)"
    );
    assert_eq!(
        session.canonical_path("Link.md").unwrap().as_deref(),
        Some("Link.md"),
        "a link keeps its own spelling"
    );
}
