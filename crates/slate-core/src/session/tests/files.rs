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
