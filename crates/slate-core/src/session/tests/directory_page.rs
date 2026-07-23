// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! W1-RT-14 goldens for the bounded, dirs-first directory page contract.

use std::path::Path;

use super::super::directory_page::{
    DirectoryPageTestPhase, directory_query, file_query, install_directory_page_test_hook,
};
use super::common::*;
use super::*;

fn mkdir(root: &Path, relative: &str) {
    std::fs::create_dir_all(root.join(relative)).unwrap();
}

fn page_names(page: &DirListingPage) -> Vec<String> {
    page.dirs
        .iter()
        .map(|dir| format!("d:{}", dir.name))
        .chain(page.files.iter().map(|file| format!("f:{}", file.name)))
        .collect()
}

#[test]
fn directory_only_overflow_is_bounded_and_keyset_paged() {
    let (tmp, session) = make_vault(|_| {});
    for name in ["Zulu", "alpha", "Echo", "bravo", "delta"] {
        mkdir(tmp.path(), name);
    }
    session.scan_initial(&CancelToken::new()).unwrap();

    let cancel = CancelToken::new();
    let first = session
        .list_dir_children_page("", Paging::first(2), &cancel)
        .unwrap();
    assert_eq!(page_names(&first), ["d:alpha", "d:bravo"]);
    assert!(first.truncated);
    let snapshot = first.snapshot_id.clone();

    let second = session
        .list_dir_children_page("", Paging::after(first.next_cursor.unwrap(), 2), &cancel)
        .unwrap();
    assert_eq!(page_names(&second), ["d:delta", "d:Echo"]);
    assert!(second.truncated);
    assert_eq!(second.snapshot_id, snapshot);

    let third = session
        .list_dir_children_page("", Paging::after(second.next_cursor.unwrap(), 2), &cancel)
        .unwrap();
    assert_eq!(page_names(&third), ["d:Zulu"]);
    assert!(!third.truncated);
    assert!(third.next_cursor.is_none());
    assert_eq!(third.snapshot_id, snapshot);
}

#[test]
fn mixed_page_crosses_the_dirs_first_boundary_without_order_drift() {
    let (tmp, session) = make_vault(|provider| {
        for name in ["Zulu.md", "alpha.md", "bravo.md"] {
            provider.write_file(name, b"# note\n").unwrap();
        }
    });
    mkdir(tmp.path(), "Zulu");
    mkdir(tmp.path(), "beta");
    session.scan_initial(&CancelToken::new()).unwrap();

    let cancel = CancelToken::new();
    let first = session
        .list_dir_children_page("", Paging::first(3), &cancel)
        .unwrap();
    assert_eq!(page_names(&first), ["d:beta", "d:Zulu", "f:alpha.md"]);
    assert!(first.truncated);

    let second = session
        .list_dir_children_page("", Paging::after(first.next_cursor.unwrap(), 3), &cancel)
        .unwrap();
    assert_eq!(page_names(&second), ["f:bravo.md", "f:Zulu.md"]);
    assert!(!second.truncated);
}

#[test]
fn page_local_directory_summaries_keep_counts_and_folder_note_semantics() {
    let (tmp, session) = make_vault(|provider| {
        provider
            .write_file("alpha/alpha.md", b"# folder note\n")
            .unwrap();
        provider.write_file("alpha/child.md", b"# child\n").unwrap();
        provider
            .write_file("alpha/nested/deep.md", b"# deep\n")
            .unwrap();
        provider.write_file("zulu/only.md", b"# only\n").unwrap();
    });
    mkdir(tmp.path(), "alpha/empty");
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .list_dir_children_page("", Paging::first(1), &CancelToken::new())
        .unwrap();
    let [alpha] = page.dirs.as_slice() else {
        panic!("expected exactly one bounded directory row");
    };
    assert_eq!(alpha.name, "alpha");
    assert_eq!(alpha.child_dir_count, 2);
    assert_eq!(alpha.child_file_count, 2);
    assert!(alpha.has_folder_note);
    assert!(page.truncated);
}

#[test]
fn mutation_between_pages_fails_closed_for_same_and_other_connections() {
    let (tmp, session) = make_vault(|_| {});
    for name in ["alpha", "beta", "gamma"] {
        mkdir(tmp.path(), name);
    }
    session.scan_initial(&CancelToken::new()).unwrap();
    let cancel = CancelToken::new();

    let first = session
        .list_dir_children_page("", Paging::first(1), &cancel)
        .unwrap();
    session.create_folder("delta").unwrap();
    let same_session =
        session.list_dir_children_page("", Paging::after(first.next_cursor.unwrap(), 1), &cancel);
    assert!(matches!(
        same_session,
        Err(VaultError::InvalidArgument { message }) if message.contains("stale")
    ));

    let restarted = session
        .list_dir_children_page("", Paging::first(1), &cancel)
        .unwrap();
    let other = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    other.create_folder("epsilon").unwrap();
    let other_connection = session.list_dir_children_page(
        "",
        Paging::after(restarted.next_cursor.unwrap(), 1),
        &cancel,
    );
    assert!(matches!(
        other_connection,
        Err(VaultError::InvalidArgument { message }) if message.contains("stale")
    ));
}

#[test]
fn cursor_parent_limit_and_cancellation_fail_closed() {
    let (tmp, session) = make_vault(|_| {});
    mkdir(tmp.path(), "alpha/child");
    mkdir(tmp.path(), "beta");
    session.scan_initial(&CancelToken::new()).unwrap();

    let cancel = CancelToken::new();
    let first = session
        .list_dir_children_page("", Paging::first(1), &cancel)
        .unwrap();
    assert!(matches!(
        session.list_dir_children_page(
            "alpha",
            Paging::after(first.next_cursor.unwrap(), 1),
            &cancel,
        ),
        Err(VaultError::InvalidArgument { message })
            if message.contains("different parent")
    ));
    assert!(matches!(
        session.list_dir_children_page("", Paging::first(0), &cancel),
        Err(VaultError::InvalidArgument { .. })
    ));
    assert!(matches!(
        session.list_dir_children_page("", Paging::first(10_001), &cancel),
        Err(VaultError::InvalidArgument { .. })
    ));

    let cancelled = CancelToken::new();
    cancelled.cancel();
    assert!(matches!(
        session.list_dir_children_page("", Paging::first(1), &cancelled),
        Err(VaultError::Cancelled)
    ));

    for malformed in ["not-a-cursor".to_string(), "x".repeat(300_000)] {
        assert!(matches!(
            session.list_dir_children_page("", Paging::after(malformed, 1), &CancelToken::new(),),
            Err(VaultError::InvalidArgument { .. })
        ));
    }
}

#[test]
fn files_only_pages_seek_past_directories_and_scope_bind_the_cursor() {
    let (tmp, session) = make_vault(|provider| {
        for name in ["alpha.md", "beta.md", "gamma.md"] {
            provider.write_file(name, b"# note\n").unwrap();
        }
    });
    for name in ["a-dir", "b-dir", "c-dir"] {
        mkdir(tmp.path(), name);
    }
    session.scan_initial(&CancelToken::new()).unwrap();

    let first = session
        .list_dir_files_page("", Paging::first(2), &CancelToken::new())
        .unwrap();
    assert!(first.dirs.is_empty());
    assert_eq!(
        first
            .files
            .iter()
            .map(|file| file.name.as_str())
            .collect::<Vec<_>>(),
        ["alpha.md", "beta.md"]
    );
    let cursor = first.next_cursor.unwrap();
    assert!(matches!(
        session.list_dir_children_page(
            "",
            Paging::after(cursor.clone(), 2),
            &CancelToken::new(),
        ),
        Err(VaultError::InvalidArgument { message }) if message.contains("scope")
    ));
    let second = session
        .list_dir_files_page("", Paging::after(cursor, 2), &CancelToken::new())
        .unwrap();
    assert_eq!(second.files.len(), 1);
    assert!(!second.truncated);
}

#[test]
fn cursor_is_session_bound_even_when_the_database_snapshot_matches() {
    let (tmp, first_session) = make_vault(|_| {});
    for name in ["alpha", "beta"] {
        mkdir(tmp.path(), name);
    }
    first_session.scan_initial(&CancelToken::new()).unwrap();
    let first = first_session
        .list_dir_children_page("", Paging::first(1), &CancelToken::new())
        .unwrap();
    let other_session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    assert!(matches!(
        other_session.list_dir_children_page(
            "",
            Paging::after(first.next_cursor.unwrap(), 1),
            &CancelToken::new(),
        ),
        Err(VaultError::InvalidArgument { message }) if message.contains("session")
    ));
}

#[test]
fn equal_unicode_sort_keys_continue_by_binary_path_without_loss() {
    let (_tmp, session) = make_vault(|_| {});
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "INSERT INTO dirs (path, parent_path, name) VALUES ('a-path', '', ?1)",
            ["E\u{0301}TUDE"],
        )
        .unwrap();
        conn.execute(
            "INSERT INTO dirs (path, parent_path, name) VALUES ('z-path', '', ?1)",
            ["ÉTUDE"],
        )
        .unwrap();
    }
    let first = session
        .list_dir_children_page("", Paging::first(1), &CancelToken::new())
        .unwrap();
    assert_eq!(first.dirs[0].path, "a-path");
    let second = session
        .list_dir_children_page(
            "",
            Paging::after(first.next_cursor.unwrap(), 1),
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(second.dirs[0].path, "z-path");
}

#[test]
fn in_flight_external_commit_fails_the_whole_page() {
    let (tmp, session) = make_vault(|_| {});
    for name in ["race/alpha", "race/beta", "race/gamma"] {
        mkdir(tmp.path(), name);
    }
    session.scan_initial(&CancelToken::new()).unwrap();
    let first = session
        .list_dir_children_page("race", Paging::first(1), &CancelToken::new())
        .unwrap();

    let (entered_tx, entered_rx) = std::sync::mpsc::channel();
    let (committed_tx, committed_rx) = std::sync::mpsc::channel();
    let committed_rx = std::sync::Arc::new(std::sync::Mutex::new(committed_rx));
    let hook_committed_rx = committed_rx.clone();
    install_directory_page_test_hook(
        "race".into(),
        DirectoryPageTestPhase::AfterDirectoryQuery,
        std::sync::Arc::new(move || {
            entered_tx.send(()).expect("signal query admission");
            hook_committed_rx
                .lock()
                .unwrap()
                .recv_timeout(std::time::Duration::from_secs(10))
                .expect("external writer did not finish");
        }),
    );
    let root = tmp.path().to_path_buf();
    let writer = std::thread::spawn(move || {
        let outcome = (|| {
            entered_rx
                .recv_timeout(std::time::Duration::from_secs(10))
                .expect("directory query did not reach the hook");
            let other = VaultSession::from_filesystem(root)?;
            other.create_folder("race/delta")
        })();
        // Never strand the query hook if opening or writing fails.
        let _ = committed_tx.send(());
        outcome
    });

    let result = session.list_dir_children_page(
        "race",
        Paging::after(first.next_cursor.unwrap(), 1),
        &CancelToken::new(),
    );
    writer.join().unwrap().unwrap();
    assert!(matches!(
        result,
        Err(VaultError::InvalidArgument { message }) if message.contains("stale")
    ));
}

#[test]
fn cancellation_after_admission_interrupts_sqlite_work() {
    let (tmp, session) = make_vault(|_| {});
    mkdir(tmp.path(), "cancel/alpha");
    for index in 0..2_000 {
        mkdir(tmp.path(), &format!("cancel/alpha/child-{index:04}"));
    }
    session.scan_initial(&CancelToken::new()).unwrap();
    let cancel = CancelToken::new();
    let hook_cancel = cancel.clone();
    install_directory_page_test_hook(
        "cancel".into(),
        DirectoryPageTestPhase::BeforeDirectoryQuery,
        std::sync::Arc::new(move || hook_cancel.cancel()),
    );
    assert!(matches!(
        session.list_dir_children_page("cancel", Paging::first(1), &cancel),
        Err(VaultError::Cancelled)
    ));
}

#[test]
fn continuation_queries_range_seek_the_parent_tree_indexes() {
    let (_tmp, session) = make_vault(|_| {});
    let conn = session.conn.lock().unwrap();
    let explain = |query: String| -> Vec<String> {
        let mut stmt = conn
            .prepare(&format!("EXPLAIN QUERY PLAN {query}"))
            .unwrap();
        stmt.query_map(rusqlite::params!["", "middle", "middle", 20_i64], |row| {
            row.get::<_, String>(3)
        })
        .unwrap()
        .map(Result::unwrap)
        .collect()
    };
    let directory_plan = explain(directory_query(true));
    assert!(
        directory_plan
            .iter()
            .any(|detail| detail.contains("idx_dirs_parent_tree") && detail.contains(">?")),
        "directory continuation must be a range seek: {directory_plan:?}"
    );
    let file_plan = explain(file_query(true));
    assert!(
        file_plan
            .iter()
            .any(|detail| detail.contains("idx_files_parent_tree") && detail.contains(">?")),
        "file continuation must be a range seek: {file_plan:?}"
    );
}

#[test]
fn bounded_page_indexes_are_present() {
    let (_tmp, session) = make_vault(|_| {});
    let conn = session.conn.lock().unwrap();
    let mut stmt = conn
        .prepare("SELECT name FROM sqlite_master WHERE type = 'index'")
        .unwrap();
    let indexes: std::collections::HashSet<String> = stmt
        .query_map([], |row| row.get(0))
        .unwrap()
        .map(Result::unwrap)
        .collect();
    assert!(indexes.contains("idx_dirs_parent_tree"));
    assert!(indexes.contains("idx_files_parent_tree"));
}
