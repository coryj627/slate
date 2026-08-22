// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1123: the typed post-publish outcome of `create_exclusive`. A
//! create-if-absent write has two failure classes a single `Err` could
//! not tell apart — REFUSED before any byte landed, and FAILED AFTER the
//! no-replace publish (index/commit) with the bytes left on disk for the
//! next scan. Hosts that saw one error for both re-presented a committed
//! file as "nothing was written" and then created duplicates under other
//! names. These facts drive the boundary with the `SLATE_TEST_FAULT_AFTER_WRITE`
//! seam (a path substring; this test's paths carry a unique token so no
//! other save in the process can trip it).

use slate_core::{
    CancelToken, CreateExclusiveOutcome, FileFilter, Paging, VaultError, VaultSession,
};

fn open(vault: &std::path::Path) -> VaultSession {
    let session = VaultSession::from_filesystem(vault.to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session
}

/// The seam is a process-global env var; Rust 2024 makes mutating the
/// environment `unsafe` because a concurrent `getenv` on POSIX can read
/// a torn value. Every trigger here is a unique token only this test's
/// paths contain, so a concurrent test's save cannot match it — and the
/// guard removes it on every exit path.
struct AfterWriteFault;

impl AfterWriteFault {
    fn arm(trigger: &str) -> Self {
        // SAFETY: see the struct doc — unique trigger, removed on drop.
        unsafe { std::env::set_var("SLATE_TEST_FAULT_AFTER_WRITE", trigger) };
        Self
    }
}

impl Drop for AfterWriteFault {
    fn drop(&mut self) {
        // SAFETY: see the struct doc.
        unsafe { std::env::remove_var("SLATE_TEST_FAULT_AFTER_WRITE") };
    }
}

#[test]
fn a_clean_create_is_committed_and_a_refusal_lands_nothing() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("taken.md"), "occupied\n").unwrap();
    let session = open(tmp.path());

    let outcome = session
        .create_exclusive_reporting("fresh.md", "hello\n")
        .unwrap();
    let CreateExclusiveOutcome::Committed(report) = outcome else {
        panic!("a clean create must commit: {outcome:?}");
    };
    assert_eq!(
        report.new_content_hash,
        session
            .read_text("fresh.md")
            .map(|_| report.new_content_hash.clone())
            .unwrap(),
        "the committed file reads back through the index"
    );

    // Refusals are `Err` — nothing landed, nothing to finish.
    let refused = session
        .create_exclusive_reporting("taken.md", "clobber?\n")
        .unwrap_err();
    assert!(
        matches!(refused, VaultError::DestinationExists { .. }),
        "got {refused:?}"
    );
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("taken.md")).unwrap(),
        "occupied\n"
    );
}

#[test]
fn a_post_publish_failure_reports_the_landed_bytes_and_the_next_scan_indexes_them() {
    let tmp = tempfile::tempdir().unwrap();
    let session = open(tmp.path());
    let token = format!("outcome-fault-{}", uuid_like());
    let path = format!("notes/{token}.md");

    let outcome = {
        let _fault = AfterWriteFault::arm(&token);
        session
            .create_exclusive_reporting(&path, "landed\n")
            .unwrap()
    };
    let CreateExclusiveOutcome::PublishedUnindexed {
        path: reported,
        content_hash,
        error,
    } = outcome
    else {
        panic!("the seam must produce the post-publish arm: {outcome:?}");
    };
    assert_eq!(reported, path);
    assert!(
        matches!(error, VaultError::InvalidArgument { ref message } if message.contains("test fault")),
        "got {error:?}"
    );
    // The bytes ARE on disk, byte-exact, and the hash names them — the
    // same bytes created cleanly elsewhere commit with the same hash.
    let on_disk = std::fs::read_to_string(tmp.path().join(&path)).unwrap();
    assert_eq!(on_disk, "landed\n");
    let CreateExclusiveOutcome::Committed(twin) = session
        .create_exclusive_reporting("notes/twin.md", "landed\n")
        .unwrap()
    else {
        panic!("the clean twin must commit");
    };
    assert_eq!(content_hash, twin.new_content_hash);
    // ...and NOT indexed: the listing does not know the row — while a
    // read still serves the bytes from disk, so a host CAN open the
    // note it just created (the consumer contract this pins).
    assert!(
        !indexed_paths(&session).contains(&path),
        "unindexed until the next scan"
    );
    assert_eq!(session.read_text(&path).unwrap(), "landed\n");

    // The legacy shape for the same failure is one error — unchanged for
    // callers that have not adopted the outcome.
    let legacy_token = format!("legacy-fault-{}", uuid_like());
    let legacy_path = format!("notes/{legacy_token}.md");
    let legacy = {
        let _fault = AfterWriteFault::arm(&legacy_token);
        session.create_exclusive(&legacy_path, "also landed\n")
    };
    assert!(legacy.is_err());
    assert!(tmp.path().join(&legacy_path).exists());

    // Idempotent retry: the file is real, so a retry is REFUSED (the disk
    // gate), never a duplicate — and a rescan indexes what landed.
    let retry = session
        .create_exclusive_reporting(&path, "retry\n")
        .unwrap_err();
    assert!(
        matches!(retry, VaultError::DestinationExists { .. }),
        "got {retry:?}"
    );
    assert_eq!(
        std::fs::read_to_string(tmp.path().join(&path)).unwrap(),
        "landed\n"
    );

    let reopened = open(tmp.path());
    let indexed = indexed_paths(&reopened);
    assert!(
        indexed.contains(&path),
        "the rescan indexes the landed file"
    );
    assert!(indexed.contains(&legacy_path));
    assert_eq!(reopened.read_text(&path).unwrap(), "landed\n");
    assert_eq!(reopened.read_text(&legacy_path).unwrap(), "also landed\n");
}

fn indexed_paths(session: &VaultSession) -> Vec<String> {
    session
        .list_files(
            FileFilter::All,
            Paging {
                cursor: None,
                limit: 1000,
            },
        )
        .unwrap()
        .items
        .into_iter()
        .map(|file| file.path)
        .collect()
}

/// Unique-enough token without pulling a uuid dependency into the test.
fn uuid_like() -> String {
    let nanos = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    format!("{nanos:x}-{:x}", std::process::id())
}
