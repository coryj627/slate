// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! A vault whose ROOT fits under MAX_PATH while its cache does not. This
//! test binary has no longPathAware manifest, so the legacy limit applies
//! whatever the machine's LongPathsEnabled policy says: the failure seen
//! here is the one a policy-off machine sees in the app.

use super::*;

/// A not-yet-created vault root of exactly `length` characters below `tmp`.
fn root_of_length(tmp: &tempfile::TempDir, length: usize) -> std::path::PathBuf {
    let base = tmp.path().as_os_str().len();
    assert!(
        base + 2 < length,
        "the temp directory already spends {base} characters"
    );
    let root = tmp.path().join("v".repeat(length - base - 1));
    assert_eq!(root.as_os_str().len(), length);
    root
}

#[test]
fn sqlite_takes_the_verbatim_form_only_when_a_sibling_would_reach_max_path() {
    let tmp = tempfile::tempdir().unwrap();
    // `-journal` is the longest sibling SQLite derives: 251 + 8 = 259 fits.
    let fits = root_of_length(&tmp, 251);
    let reaches = root_of_length(&tmp, 252);
    assert_eq!(
        crate::db::sqlite_open_path(&fits).as_os_str(),
        fits.as_os_str()
    );
    let verbatim = crate::db::sqlite_open_path(&reaches);
    assert!(verbatim.to_string_lossy().starts_with(r"\\?\"));
    assert!(
        verbatim
            .to_string_lossy()
            .ends_with(&*reaches.to_string_lossy())
    );
}

#[test]
fn a_vault_whose_cache_crosses_max_path_opens_saves_and_reopens() {
    let tmp = tempfile::tempdir().unwrap();
    let root = root_of_length(&tmp, 240);
    std::fs::create_dir_all(&root).unwrap();
    std::fs::write(root.join("note.md"), b"# Long\n\nbody with zeta\n").unwrap();
    let cache = root.join(".slate").join("cache.sqlite");
    assert!(
        cache.as_os_str().len() >= 260,
        "fixture: <root>\\.slate\\cache.sqlite must reach MAX_PATH ({} characters)",
        cache.as_os_str().len()
    );

    let session =
        VaultSession::from_filesystem(root.clone()).expect("a 240-character vault root must open");
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(super::common::fts_match_count(&session, "zeta"), 1);
    session
        .save_text("note.md", "# Long\n\nbody with omega\n", None)
        .unwrap();
    session.close().unwrap();
    assert!(cache.is_file(), "the cache lives at <root>\\.slate");

    let reopened = VaultSession::from_filesystem(root)
        .expect("the same vault must reopen from its long-path cache");
    reopened.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(super::common::fts_match_count(&reopened, "omega"), 1);
    assert_eq!(
        crate::oplog::reconstruct_at_tail(&reopened.read_oplog("note.md").unwrap()).unwrap(),
        "# Long\n\nbody with omega\n"
    );
}

#[test]
fn compaction_opens_its_own_connection_under_a_long_cache_path() {
    let tmp = tempfile::tempdir().unwrap();
    let root = root_of_length(&tmp, 240);
    std::fs::create_dir_all(&root).unwrap();
    let mut config = SessionConfig::new(root.join(".slate"));
    config.oplog_compaction_threshold_bytes = 2048;
    let provider = Arc::new(FsVaultProvider::new(root));
    let session = VaultSession::open(provider, config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    // The worker marks the derived index stale through its OWN connection
    // before folding, and skips the fold when that connection cannot open,
    // so a shrunken log proves the worker opened the long-path cache.
    let mut content = String::new();
    for line in 0..12 {
        content = format!("{content}line {line} with some padding text to bulk the log\n");
        session.save_text("hot.md", &content, None).unwrap();
    }
    let log_size = || {
        let name: String = session
            .conn
            .lock()
            .unwrap()
            .query_row(
                "SELECT oplog_name FROM files WHERE path = 'hot.md'",
                [],
                |row| row.get(0),
            )
            .unwrap();
        std::fs::metadata(crate::oplog::oplog_path_for_name(
            &session.config.cache_dir,
            &name,
        ))
        .unwrap()
        .len()
    };
    let shrunk = (0..200).any(|_| {
        if log_size() <= 2048 {
            return true;
        }
        std::thread::sleep(std::time::Duration::from_millis(25));
        false
    });
    assert!(shrunk, "the log never compacted; size is {}", log_size());
    assert_eq!(
        crate::oplog::reconstruct_at_tail(&session.read_oplog("hot.md").unwrap()).unwrap(),
        content
    );
}
