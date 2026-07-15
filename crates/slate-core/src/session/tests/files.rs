// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — list_files, get_file_metadata, read_text.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

#[test]
fn list_files_markdown_only() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"").unwrap();
        p.write_file("b.txt", b"").unwrap();
        p.write_file("c.markdown", b"").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let page = session
        .list_files(FileFilter::MarkdownOnly, Paging::first(100))
        .unwrap();
    let names: Vec<&str> = page.items.iter().map(|f| f.name.as_str()).collect();
    assert_eq!(names, vec!["a.md", "c.markdown"]);
    assert_eq!(page.total_filtered, 2);
    for item in &page.items {
        assert!(item.is_markdown);
    }
}

#[test]
fn list_files_paginates() {
    let (_tmp, session) = make_vault(|p| {
        for i in 0..10 {
            p.write_file(&format!("note-{i:02}.md"), b"").unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // First page of 4
    let page1 = session
        .list_files(FileFilter::All, Paging::first(4))
        .unwrap();
    assert_eq!(page1.items.len(), 4);
    assert_eq!(page1.total_filtered, 10);
    let cursor1 = page1.next_cursor.clone().expect("should have next cursor");

    // Second page of 4
    let page2 = session
        .list_files(FileFilter::All, Paging::after(cursor1, 4))
        .unwrap();
    assert_eq!(page2.items.len(), 4);
    let cursor2 = page2.next_cursor.clone().expect("should have next cursor");

    // Third (final) page: remaining 2
    let page3 = session
        .list_files(FileFilter::All, Paging::after(cursor2, 4))
        .unwrap();
    assert_eq!(page3.items.len(), 2);
    assert!(page3.next_cursor.is_none(), "no more pages");

    // No overlap, in alphabetical order.
    let mut all_names: Vec<String> = Vec::new();
    all_names.extend(page1.items.iter().map(|f| f.name.clone()));
    all_names.extend(page2.items.iter().map(|f| f.name.clone()));
    all_names.extend(page3.items.iter().map(|f| f.name.clone()));
    assert_eq!(all_names.len(), 10);
    let mut sorted = all_names.clone();
    sorted.sort();
    assert_eq!(all_names, sorted);
}

#[test]
fn list_files_empty_vault() {
    let tmp = tempfile::tempdir().unwrap();
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    let page = session
        .list_files(FileFilter::All, Paging::first(10))
        .unwrap();
    assert!(page.items.is_empty());
    assert_eq!(page.total_filtered, 0);
    assert!(page.next_cursor.is_none());
}

#[test]
fn file_summary_enrichment_is_typed_deterministic_and_stable_across_listing_apis() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "a-authored.md",
            br#"---
title: "  Authored title  "
created: 2024-02-29
category: Work
---
# Heading

- [ ] open task
- [x] done task

Hello [[World|friend]].
"#,
        )
        .unwrap();
        p.write_file(
            "b-offset.md",
            b"---\ncreated: 2024-03-01T12:30:45+02:00\n---\nOffset body\n",
        )
        .unwrap();
        p.write_file(
            "c-naive.md",
            b"---\ncreated: 2024-03-01T10:30:45\n---\nNaive body\n",
        )
        .unwrap();
        p.write_file(
            "d-invalid.md",
            b"---\ncreated: 2023-02-29\n---\nInvalid date\n",
        )
        .unwrap();
        p.write_file(
            "e-list-title.md",
            b"---\ntitle: [not, scalar]\n---\nList title\n",
        )
        .unwrap();
        p.write_file(
            "f-empty-title.md",
            b"---\ntitle: \"   \"\n---\nEmpty title\n",
        )
        .unwrap();
        p.write_file("g-missing-meta.md", b"Missing replay projection\n")
            .unwrap();
        p.write_file("h-data.txt", b"plain text\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    const AUTHORED_BIRTHTIME: i64 = 1_700_000_000_123;
    const INVALID_BIRTHTIME: i64 = 1_710_000_000_456;
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET birthtime_ms = ?1 WHERE path = 'a-authored.md'",
            [AUTHORED_BIRTHTIME],
        )
        .unwrap();
        conn.execute(
            "UPDATE files SET birthtime_ms = ?1 WHERE path = 'd-invalid.md'",
            [INVALID_BIRTHTIME],
        )
        .unwrap();
        conn.execute(
            "DELETE FROM file_meta WHERE file_id =
             (SELECT id FROM files WHERE path = 'g-missing-meta.md')",
            [],
        )
        .unwrap();

        // A second title/created row must neither multiply the file row nor
        // displace the deterministic first document ordinal.
        let authored_id: i64 = conn
            .query_row(
                "SELECT id FROM files WHERE path = 'a-authored.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        conn.execute(
            "INSERT INTO properties
             (file_id, ordinal, key, value_kind, value_text, value_text_norm)
             VALUES (?1, 100, 'title', 'text', '\"Later title\"', 'later title')",
            [authored_id],
        )
        .unwrap();
        conn.execute(
            "INSERT INTO properties
             (file_id, ordinal, key, value_kind, value_text, value_text_norm)
             VALUES (?1, 101, 'created', 'date', '\"1999-01-01\"', '1999-01-01')",
            [authored_id],
        )
        .unwrap();
    }

    let page = session
        .list_files(FileFilter::All, Paging::first(100))
        .unwrap();
    assert_eq!(page.total_filtered, 8);
    assert_eq!(page.items.len(), 8, "property joins must not multiply rows");
    assert_eq!(
        page.items
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        vec![
            "a-authored.md",
            "b-offset.md",
            "c-naive.md",
            "d-invalid.md",
            "e-list-title.md",
            "f-empty-title.md",
            "g-missing-meta.md",
            "h-data.txt",
        ],
        "enrichment must preserve path order"
    );

    let authored = page
        .items
        .iter()
        .find(|f| f.path == "a-authored.md")
        .unwrap();
    assert_eq!(authored.display_name.as_deref(), Some("Authored title"));
    assert_eq!(authored.created_date.as_deref(), Some("2024-02-29"));
    assert_eq!(authored.created_ms, Some(AUTHORED_BIRTHTIME));
    assert!(authored.word_count.unwrap() > 0);
    assert!(
        authored
            .preview
            .as_deref()
            .unwrap()
            .contains("Hello friend")
    );
    assert_eq!((authored.task_total, authored.task_open), (2, 1));

    let offset = page.items.iter().find(|f| f.path == "b-offset.md").unwrap();
    assert_eq!(offset.created_date, None);
    assert_eq!(
        offset.created_ms,
        Some(
            chrono::DateTime::parse_from_rfc3339("2024-03-01T12:30:45+02:00")
                .unwrap()
                .timestamp_millis()
        )
    );

    let naive = page.items.iter().find(|f| f.path == "c-naive.md").unwrap();
    assert_eq!(naive.created_date, None);
    assert_eq!(
        naive.created_ms,
        Some(
            chrono::NaiveDateTime::parse_from_str("2024-03-01T10:30:45", "%Y-%m-%dT%H:%M:%S")
                .unwrap()
                .and_utc()
                .timestamp_millis()
        )
    );

    let invalid = page
        .items
        .iter()
        .find(|f| f.path == "d-invalid.md")
        .unwrap();
    assert_eq!(invalid.created_date, None);
    assert_eq!(invalid.created_ms, Some(INVALID_BIRTHTIME));
    assert_eq!(
        page.items
            .iter()
            .find(|f| f.path == "e-list-title.md")
            .unwrap()
            .display_name,
        None
    );
    assert_eq!(
        page.items
            .iter()
            .find(|f| f.path == "f-empty-title.md")
            .unwrap()
            .display_name,
        None
    );

    let missing = page
        .items
        .iter()
        .find(|f| f.path == "g-missing-meta.md")
        .unwrap();
    assert_eq!(
        (missing.word_count, missing.preview.as_deref()),
        (None, None)
    );
    assert_eq!((missing.task_total, missing.task_open), (0, 0));
    let non_markdown = page.items.iter().find(|f| f.path == "h-data.txt").unwrap();
    assert_eq!(
        (non_markdown.word_count, non_markdown.preview.as_deref()),
        (None, None)
    );

    let tree = session.list_dir_children("", Paging::first(100)).unwrap();
    assert_eq!(tree.files.items, page.items);

    let by_value = session
        .files_with_property("category", "work", Paging::first(10))
        .unwrap();
    assert_eq!(by_value.total_filtered, 1);
    assert_eq!(by_value.items, vec![authored.clone()]);

    let by_key = session
        .files_with_property_key("title", Paging::first(10))
        .unwrap();
    assert_eq!(by_key.total_filtered, 3);
    assert_eq!(
        by_key
            .items
            .iter()
            .filter(|f| f.path == "a-authored.md")
            .count(),
        1,
        "duplicate property shapes must still yield one enriched file"
    );
    assert_eq!(
        by_key
            .items
            .iter()
            .find(|f| f.path == "a-authored.md")
            .unwrap(),
        authored
    );
}

#[test]
fn get_file_summary_matches_list_files_enriched_projection() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/target.md",
            br#"---
title: "  Target title  "
created: 2026-07-14
---
# Heading

- [ ] open task
- [x] done task

Target preview words.
"#,
        )
        .unwrap();
        p.write_file("notes/sibling.md", b"Sibling content\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let listed = session
        .list_files(FileFilter::All, Paging::first(100))
        .unwrap()
        .items
        .into_iter()
        .find(|summary| summary.path == "notes/target.md")
        .unwrap();
    let targeted = session
        .get_file_summary("notes/target.md")
        .unwrap()
        .unwrap();

    assert_eq!(targeted, listed);
    assert_eq!(targeted.display_name.as_deref(), Some("Target title"));
    assert_eq!(targeted.created_date.as_deref(), Some("2026-07-14"));
    assert!(targeted.word_count.unwrap() > 0);
    assert!(
        targeted
            .preview
            .as_deref()
            .unwrap()
            .contains("Target preview")
    );
    assert_eq!((targeted.task_total, targeted.task_open), (2, 1));
}

#[test]
fn get_file_summary_returns_none_for_unknown_path() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("present.md", b"Present\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    assert_eq!(session.get_file_summary("missing.md").unwrap(), None);
}

#[test]
fn get_file_summary_rejects_parent_traversal() {
    let (_tmp, session) = make_vault(|_| {});

    match session.get_file_summary("notes/../../outside.md") {
        Err(VaultError::InvalidPath { path, .. }) => {
            assert_eq!(path, "notes/../../outside.md");
        }
        other => panic!("expected InvalidPath, got {other:?}"),
    }
}

#[test]
fn get_file_summary_returns_latest_enrichment_after_save() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "note.md",
            b"---\ntitle: Before\ncreated: 2024-01-01\n---\nOld body\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let before = session.get_file_summary("note.md").unwrap().unwrap();
    session
        .save_text(
            "note.md",
            "---\ntitle: After\ncreated: 2025-02-03\n---\nFresh preview words here.\n- [ ] first\n- [x] second\n",
            None,
        )
        .unwrap();
    let after = session.get_file_summary("note.md").unwrap().unwrap();
    let listed_after = session
        .list_files(FileFilter::All, Paging::first(10))
        .unwrap()
        .items
        .into_iter()
        .find(|summary| summary.path == "note.md")
        .unwrap();

    assert_ne!(after, before);
    assert_eq!(after, listed_after);
    assert_eq!(after.display_name.as_deref(), Some("After"));
    assert_eq!(after.created_date.as_deref(), Some("2025-02-03"));
    assert!(after.word_count.unwrap() > before.word_count.unwrap());
    assert!(after.preview.as_deref().unwrap().contains("Fresh preview"));
    assert_eq!((after.task_total, after.task_open), (2, 1));
}

#[test]
fn file_summary_enrichment_preserves_pagination_totals_and_order() {
    let (_tmp, session) = make_vault(|p| {
        for name in ["A.md", "b.md", "C.md", "d.md", "e.md"] {
            p.write_file(
                name,
                format!("---\ntitle: {name}\n---\n{name}\n").as_bytes(),
            )
            .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let first = session
        .list_files(FileFilter::All, Paging::first(2))
        .unwrap();
    assert_eq!(first.total_filtered, 5);
    assert_eq!(
        first
            .items
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        vec!["A.md", "C.md"]
    );
    let second = session
        .list_files(
            FileFilter::All,
            Paging::after(first.next_cursor.clone().unwrap(), 2),
        )
        .unwrap();
    assert_eq!(second.total_filtered, 5);
    assert_eq!(
        second
            .items
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        vec!["b.md", "d.md"]
    );
    let third = session
        .list_files(
            FileFilter::All,
            Paging::after(second.next_cursor.clone().unwrap(), 2),
        )
        .unwrap();
    assert_eq!(third.total_filtered, 5);
    assert_eq!(third.items[0].path, "e.md");
    assert!(third.next_cursor.is_none());

    let past_end = session
        .list_files(FileFilter::All, Paging::after("z.md".into(), 2))
        .unwrap();
    assert!(past_end.items.is_empty());
    assert_eq!(past_end.total_filtered, 5);
}

#[test]
fn created_civil_dates_use_the_shared_proleptic_year_0001_through_9999_contract() {
    for value in [
        "0001-01-01",
        "1582-10-04",
        "1582-10-10",
        "1582-10-15",
        "9999-12-31",
    ] {
        assert!(
            canonical_gregorian_date(value),
            "{value} must be accepted by the Rust half of the shared civil-date contract"
        );
        assert_eq!(
            resolve_created_value(Some("date"), Some(&format!("\"{value}\"")), None),
            (Some(value.to_string()), None)
        );
    }

    assert!(
        !canonical_gregorian_date("0000-01-01"),
        "Foundation has no safe year-zero mapping, so Rust must reject it too"
    );
    assert_eq!(
        resolve_created_value(Some("date"), Some("\"0000-01-01\""), None),
        (None, None)
    );
}

#[test]
fn get_file_metadata_returns_none_for_unknown_path() {
    let (_tmp, session) = make_vault(|_| {});
    assert!(session.get_file_metadata("missing.md").unwrap().is_none());
}

#[test]
fn read_text_round_trips_utf8_content() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/hello.md", "# Hello, vault! 🦀\n".as_bytes())
            .unwrap();
    });
    let text = session.read_text("notes/hello.md").unwrap();
    assert_eq!(text, "# Hello, vault! 🦀\n");
}

#[test]
fn read_text_rejects_invalid_utf8_typed() {
    // 0xFF can't start a valid UTF-8 sequence. read_text must
    // surface this as InvalidUtf8 rather than silently producing
    // replacement characters — the editor / reader path is
    // user-facing and shouldn't lie about file contents.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("bad.md", &[b'#', b' ', 0xFF, b'\n']).unwrap();
    });
    match session.read_text("bad.md") {
        Err(VaultError::InvalidUtf8 { path }) => assert_eq!(path, "bad.md"),
        other => panic!("expected InvalidUtf8 for bad.md, got {other:?}"),
    }
}

#[test]
fn read_text_rejects_absolute_paths() {
    // Provider-level path validation is reused — read_text must
    // not accept absolutes / parent traversal / Windows prefixes.
    let (_tmp, session) = make_vault(|_| {});
    match session.read_text("/etc/passwd") {
        Err(VaultError::InvalidPath { .. }) => {}
        other => panic!("expected InvalidPath, got {other:?}"),
    }
}

#[test]
fn read_text_refuses_files_grown_after_stat_toctou() {
    // Provider that lies: stat() reports 1 byte, but
    // read_file_with_cap() returns more bytes than the cap.
    // Reproduces the TOCTOU window where a file grows between
    // the size pre-check and the read. The session must refuse
    // via the over-cap signal without buffering arbitrarily-
    // large bytes — `read_file_with_cap` allocates at most
    // `cap + 1` regardless of the file's true size on disk.
    struct GrowingProvider {
        inner: FsVaultProvider,
    }
    impl crate::VaultProvider for GrowingProvider {
        fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
            self.inner.list_dir(relative)
        }
        fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
            self.inner.read_file(relative)
        }
        fn read_file_with_cap(
            &self,
            relative: &str,
            max_bytes: u64,
        ) -> Result<Vec<u8>, VaultError> {
            // Simulate the grown file: return exactly the
            // over-cap sentinel length. The real (`inner`)
            // read_file_with_cap would do this naturally if the
            // file grew past max_bytes; we synthesize it here so
            // the test doesn't have to race the filesystem.
            let _ = self.inner.read_file_with_cap(relative, max_bytes)?;
            Ok(vec![b'x'; (max_bytes + 1) as usize])
        }
        fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
            self.inner.write_file(relative, contents)
        }
        fn delete(&self, relative: &str) -> Result<(), VaultError> {
            self.inner.delete(relative)
        }
        fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
            self.inner.rename(from, to)
        }
        fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
            self.inner.create_dir(relative)
        }
        fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
            // Lie about the size: report 1 byte.
            let mut stat = self.inner.stat(relative)?;
            stat.size_bytes = 1;
            Ok(stat)
        }
        fn watch(
            &self,
            sink: Arc<dyn crate::FileEventSink>,
        ) -> Result<Option<crate::WatchHandle>, VaultError> {
            self.inner.watch(sink)
        }
    }

    let tmp = tempfile::tempdir().unwrap();
    let real = FsVaultProvider::new(tmp.path().to_path_buf());
    real.write_file("a.md", b"tiny").unwrap();

    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 100;
    let session = VaultSession::open(
        Arc::new(GrowingProvider {
            inner: FsVaultProvider::new(tmp.path().to_path_buf()),
        }),
        config,
    )
    .unwrap();

    match session.read_text("a.md") {
        Err(VaultError::FileTooLarge { path, size }) => {
            assert_eq!(path, "a.md");
            // size is the sentinel length we synthesized: cap + 1.
            assert!(
                size > 100,
                "size should exceed the configured cap, got {size}"
            );
        }
        other => panic!("expected FileTooLarge from over-cap signal, got {other:?}"),
    }
}

#[test]
fn read_text_at_exact_limit_succeeds() {
    // Boundary: a file whose size equals `large_file_refuse_bytes`
    // is *within* the limit and must succeed. The `>` comparison
    // makes this the on-boundary case.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    // 10 bytes of valid ASCII.
    provider.write_file("edge.md", b"0123456789").unwrap();

    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 10;
    let session = VaultSession::open(Arc::new(provider), config).unwrap();

    assert_eq!(session.read_text("edge.md").unwrap(), "0123456789");
}

#[test]
fn read_file_with_cap_returns_at_most_cap_plus_one_on_real_provider() {
    // Sanity check on the FsVaultProvider override: a file
    // genuinely larger than the cap must produce a buffer with
    // exactly `cap + 1` bytes (the over-cap sentinel), not the
    // file's full size. This is the heart of the OOM guard.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let payload = vec![b'a'; 5_000];
    provider.write_file("big.md", &payload).unwrap();

    let cap = 100u64;
    let bytes = provider.read_file_with_cap("big.md", cap).unwrap();
    assert_eq!(bytes.len() as u64, cap + 1);
}

#[test]
fn read_text_refuses_files_over_large_file_threshold() {
    // Custom SessionConfig with a small refuse threshold so we
    // can write a tiny file that still trips it. Default
    // `large_file_refuse_bytes` is 50 MB which would make this
    // test prohibitive.
    let tmp = tempfile::tempdir().unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("big.md", b"more than ten bytes please")
        .unwrap();

    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 10;
    let session = VaultSession::open(Arc::new(provider), config).unwrap();

    match session.read_text("big.md") {
        Err(VaultError::FileTooLarge { path, size }) => {
            assert_eq!(path, "big.md");
            assert!(size > 10, "size should be the actual file size, got {size}");
        }
        other => panic!("expected FileTooLarge, got {other:?}"),
    }
}

#[test]
fn get_file_metadata_returns_properties_in_document_order() {
    let body = "---\ntitle: My Note\ntags:\n  - alpha\n  - beta\npublished: true\n---\n# body";
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/note.md", body.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let md = session.get_file_metadata("notes/note.md").unwrap().unwrap();
    let keys: Vec<&str> = md.properties.iter().map(|p| p.key.as_str()).collect();
    assert_eq!(keys, vec!["title", "tags", "published"]);
}
