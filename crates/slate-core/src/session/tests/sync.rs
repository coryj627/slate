// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! M-1 (#532): `VaultSession::detect_sync` — the session-level wiring
//! over the pure detector in `sync_detect.rs`. The detector table
//! itself is exhaustively covered by the seam fixtures + census in
//! that module; these tests pin the `fs_root()` derivation and the
//! `supported = false` path for provider-abstracted sessions.

use super::common::*;
use super::*;

#[test]
fn detect_sync_on_filesystem_vault_probes_the_vault_root() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"# hi").unwrap();
    });
    // Plant a Git marker at the ROOT (the cache dir's parent), not in
    // the cache dir: a hit proves fs_root() derives `<root>` from
    // `<root>/.slate`.
    std::fs::create_dir(tmp.path().join(".git")).unwrap();

    let report = session.detect_sync().expect("detect_sync");
    assert!(report.supported);
    assert_eq!(report.providers.len(), 1);
    assert_eq!(
        report.providers[0].kind,
        crate::sync_detect::SyncProviderKind::Git
    );
    assert_eq!(report.providers[0].evidence_paths, vec![".git".to_string()]);
    assert_eq!(report.audio_summary, "1 sync system detected: Git.");
}

#[test]
fn detect_sync_clean_vault_reports_empty() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"# hi").unwrap();
    });
    let report = session.detect_sync().expect("detect_sync");
    assert!(report.supported);
    assert!(report.providers.is_empty());
    assert_eq!(report.multi_sync_warning, None);
    assert_eq!(report.audio_summary, "No sync systems detected.");
}

/// M-2 (#533): `livesync_config()` reads the plugin config off the
/// same `fs_root()` derivation.
#[test]
fn livesync_config_reads_from_vault_root() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"# hi").unwrap();
    });
    let plugin = tmp.path().join(".obsidian/plugins/obsidian-livesync");
    std::fs::create_dir_all(&plugin).unwrap();
    std::fs::write(
        plugin.join("data.json"),
        br#"{"couchDB_DBNAME": "notes", "liveSync": true}"#,
    )
    .unwrap();

    let status = session.livesync_config().expect("livesync_config");
    let crate::sync_detect::LiveSyncConfigStatus::Parsed(config) = status else {
        panic!("expected Parsed, got {status:?}");
    };
    assert_eq!(config.database.as_deref(), Some("notes"));
    assert_eq!(config.live_sync_enabled, Some(true));
}

#[test]
fn livesync_config_not_present_on_clean_vault() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("note.md", b"# hi").unwrap();
    });
    assert_eq!(
        session.livesync_config().expect("livesync_config"),
        crate::sync_detect::LiveSyncConfigStatus::NotPresent
    );
}

/// A session whose `cache_dir` is a bare relative path has no
/// filesystem root (`fs_root()` → `None`): detection is unsupported —
/// an empty report with `supported = false`, NOT an error. This is the
/// provider-abstracted-session shape (plan decision #5).
#[test]
fn detect_sync_without_fs_root_reports_unsupported() {
    // A single-component relative cache_dir materializes under the
    // test CWD (the crate root); the guard removes it on drop.
    struct CleanupGuard(std::path::PathBuf);
    impl Drop for CleanupGuard {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }
    let cache_dir = std::path::PathBuf::from("m1-unsupported-fs-root-test-cache");
    let _guard = CleanupGuard(cache_dir.clone());

    let tmp = tempfile::tempdir().unwrap();
    let provider = std::sync::Arc::new(FsVaultProvider::new(tmp.path().to_path_buf()));
    let session = VaultSession::open(provider, SessionConfig::new(cache_dir)).unwrap();

    let report = session.detect_sync().expect("detect_sync must not error");
    assert!(!report.supported);
    assert!(report.providers.is_empty());
    assert_eq!(report.multi_sync_warning, None);
    assert_eq!(
        report.audio_summary,
        "Sync detection isn't available for this vault type."
    );

    // M-2: same fs_root rule — no root reads no config.
    assert_eq!(
        session.livesync_config().expect("livesync_config"),
        crate::sync_detect::LiveSyncConfigStatus::NotPresent
    );
}
