// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! WB-25 stage 2 (#1290; locked decision 05 §9.3.1): Quick Open ranks
//! against the index. The oracle is `switcher_rank_top` over the host's
//! list for the same vault — the `list_files(OpenableDocuments)` pages
//! concatenated, as the Windows host built it before this change.

use slate_core::switcher::{SwitcherFile, SwitcherRankPage, switcher_rank, switcher_rank_top};
use slate_core::{CancelToken, FileFilter, Paging, VaultError, VaultSession};

fn write(vault: &std::path::Path, rel: &str, content: &str) {
    let path = vault.join(rel);
    std::fs::create_dir_all(path.parent().unwrap()).unwrap();
    std::fs::write(path, content).unwrap();
}

fn open(vault: &std::path::Path) -> VaultSession {
    let session = VaultSession::from_filesystem(vault.to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session
}

/// The host's old list: every openable document, page by page.
fn host_list(session: &VaultSession) -> Vec<SwitcherFile> {
    let mut files = Vec::new();
    let mut cursor = None;
    loop {
        let page = session
            .list_files(
                FileFilter::OpenableDocuments,
                Paging { cursor, limit: 500 },
                &CancelToken::new(),
            )
            .unwrap();
        files.extend(page.items.into_iter().map(|file| SwitcherFile {
            path: file.path,
            name: file.name,
        }));
        cursor = page.next_cursor;
        if cursor.is_none() {
            return files;
        }
    }
}

fn indexed(
    session: &VaultSession,
    query: &str,
    recents: &[String],
    limit: usize,
) -> SwitcherRankPage {
    session
        .switcher_rank_top_indexed(query, recents, limit, &CancelToken::new())
        .unwrap()
}

fn paths(page: &SwitcherRankPage) -> Vec<&str> {
    page.rows.iter().map(|row| row.path.as_str()).collect()
}

fn thousand_file_vault(vault: &std::path::Path) {
    for index in 0..1_000 {
        let bucket = index % 23;
        let rel = match index % 50 {
            7 => format!("folder-{bucket:02}/board-{index:04}.canvas"),
            11 => format!("folder-{bucket:02}/table-{index:04}.base"),
            13 => format!("folder-{bucket:02}/image-{index:04}.png"),
            _ => format!("folder-{bucket:02}/note-{index:04}.md"),
        };
        write(vault, &rel, "body\n");
    }
    write(vault, "x/todo.md", "a\n");
    write(vault, "y/todo.md", "b\n");
    write(vault, "z/todo.md", "c\n");
}

#[test]
fn switcher_rank_top_indexed_matches_switcher_rank_top_over_the_same_files() {
    let tmp = tempfile::tempdir().unwrap();
    thousand_file_vault(tmp.path());
    let session = open(tmp.path());
    let files = host_list(&session);
    assert!(
        files.len() > 950,
        "the fixture indexed {} documents",
        files.len()
    );
    let recent_lists = [
        vec![],
        vec![
            "z/todo.md".to_owned(),
            "gone.md".to_owned(),
            "folder-03/note-0003.md".to_owned(),
            "z/todo.md".to_owned(),
        ],
    ];
    for recents in &recent_lists {
        for query in [
            "", "note", "n1", "todo", "folder-3", "board", "table", "image", "e", "missing",
        ] {
            for limit in [0, 1, 7, 50, 2_000] {
                assert_eq!(
                    indexed(&session, query, recents, limit),
                    switcher_rank_top(&files, query, recents, limit),
                    "query={query:?} limit={limit} recents={recents:?}"
                );
            }
        }
    }
}

#[test]
fn switcher_rank_top_indexed_keeps_recency_and_canonical_path_tie_breaks() {
    let tmp = tempfile::tempdir().unwrap();
    for rel in [
        "x/todo.md",
        "y/todo.md",
        "z/todo.md",
        "f/note.md",
        "e\u{301}/note.md",
    ] {
        write(tmp.path(), rel, "body\n");
    }
    #[cfg(not(target_os = "macos"))]
    write(tmp.path(), "\u{e9}/note.md", "body\n");
    let session = open(tmp.path());
    let files = host_list(&session);
    let r: Vec<String> = ["z/todo.md", "gone.md", "x/todo.md", "z/todo.md"]
        .iter()
        .map(|path| path.to_string())
        .collect();
    let mut r_precomposed = r.clone();
    r_precomposed.push("\u{e9}/note.md".to_owned());
    for recents in [&r, &r_precomposed] {
        for query in ["todo", "note", ""] {
            for limit in [1, 2, 50] {
                assert_eq!(
                    indexed(&session, query, recents, limit),
                    switcher_rank_top(&files, query, recents, limit),
                    "query={query:?} limit={limit} recents={recents:?}"
                );
            }
        }
    }
    // Recency breaks the score tie, and a duplicated recent's last index
    // wins: x (2) before z (3), then y, never opened.
    assert_eq!(
        paths(&indexed(&session, "todo", &r, 50)),
        ["x/todo.md", "z/todo.md", "y/todo.md"]
    );
    // Canonical order puts é after f; once the precomposed spelling is a
    // recent it finds the decomposed file, which leads.
    let note = indexed(&session, "note", &r, 50);
    assert_eq!(note.rows[0].path, "f/note.md");
    assert_eq!(note.rows[1].path, "e\u{301}/note.md");
    let note = indexed(&session, "note", &r_precomposed, 50);
    assert_eq!(note.rows[0].path, "e\u{301}/note.md");
}

#[test]
fn switcher_rank_top_indexed_marshals_at_most_limit_rows_with_the_exact_total() {
    let tmp = tempfile::tempdir().unwrap();
    thousand_file_vault(tmp.path());
    let session = open(tmp.path());
    let files = host_list(&session);
    for query in ["", "note", "n1"] {
        let matches = switcher_rank(&files, query, &[]).len();
        let page = indexed(&session, query, &[], 7);
        assert_eq!(page.rows.len(), 7.min(matches), "{query:?}");
        assert_eq!(page.total, matches, "{query:?}");
    }
    let over = session.switcher_rank_top_indexed(
        "note",
        &[],
        slate_core::session::MAX_SWITCHER_RANK_LIMIT as usize + 1,
        &CancelToken::new(),
    );
    assert!(
        matches!(over, Err(VaultError::InvalidArgument { .. })),
        "{over:?}"
    );
}

#[test]
fn switcher_rank_top_indexed_sees_a_file_the_index_just_gained() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "old.md", "a\n");
    let session = open(tmp.path());
    assert_eq!(indexed(&session, "brand", &[], 50).total, 0);

    session.create_exclusive("brand-new.md", "b\n").unwrap();

    let page = indexed(&session, "brand", &[], 50);
    assert_eq!(paths(&page), ["brand-new.md"]);
    assert_eq!(page.total, 1);
}
