// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-29a (spec
//! `docs/plans/44_windows_backlog/specs/wb29a_hot_exit_shared_design_and_core_store.md`):
//! the session's hot-exit reads — the vault's store key, each note's file
//! identity, how a kept entry comes back, and the identity-checked save.

// The environment seams (`fault_guard`) only the Windows and macOS key
// facts set.
#[cfg(any(windows, target_os = "macos"))]
use super::common::*;
use super::*;
use crate::hot_exit::{HotExitEntry, HotExitKeyResult, HotExitRestore};
use std::path::Path;

fn session_at(root: &Path) -> VaultSession {
    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session
}

fn kept(
    path: &str,
    baseline_hash: Option<String>,
    baseline_file_id: Option<String>,
) -> HotExitEntry {
    HotExitEntry {
        schema: 1,
        entry_id: "tab1".into(),
        incarnation: "i".into(),
        generation: 1,
        path: path.into(),
        text: "unsaved".into(),
        baseline_hash,
        baseline_file_id,
        written_at: "2026-11-01T00:00:00.000Z".into(),
    }
}

fn hash_of(root: &Path, path: &str) -> String {
    crate::vault::content_hash(&std::fs::read(root.join(path)).unwrap())
}

#[test]
fn classify_decides_each_row() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("same.md"), "# Same\n").unwrap();
    std::fs::write(root.join("stale.md"), "# Before\n").unwrap();
    std::fs::write(root.join("replaced.md"), "").unwrap();
    std::fs::write(root.join("new-over.md"), "# There now\n").unwrap();
    std::fs::write(root.join("empty-a.md"), "").unwrap();
    std::fs::write(root.join("empty-b.md"), "").unwrap();
    let session = session_at(root);

    let same = kept(
        "same.md",
        Some(hash_of(root, "same.md")),
        session.note_file_identity("same.md").unwrap(),
    );
    let stale_hash = hash_of(root, "stale.md");
    let stale_id = session.note_file_identity("stale.md").unwrap();
    std::fs::write(root.join("stale.md"), "# After\n").unwrap();
    let stale = kept("stale.md", Some(stale_hash), stale_id);
    // An unrelated file with the same (empty) bytes put at the path.
    let replaced_hash = hash_of(root, "replaced.md");
    let replaced_id = session.note_file_identity("replaced.md").unwrap();
    std::fs::remove_file(root.join("replaced.md")).unwrap();
    std::fs::write(root.join("elsewhere.md"), "").unwrap();
    std::fs::write(root.join("replaced.md"), "").unwrap();
    let replaced = kept("replaced.md", Some(replaced_hash), replaced_id);
    // A buffer that was a new note, now over a file.
    let exists_now = kept("new-over.md", None, None);
    // An empty note deleted beside another empty note: never re-seated.
    let empty_hash = hash_of(root, "empty-a.md");
    let empty_id = session.note_file_identity("empty-a.md").unwrap();
    std::fs::remove_file(root.join("empty-a.md")).unwrap();
    let gone = kept("empty-a.md", Some(empty_hash), empty_id);
    let outside = kept("../outside.md", Some("h".into()), None);
    let slate = kept(".slate/cache.md", Some("h".into()), None);

    let decisions = session
        .classify_hot_exit_entries(&[same, stale, replaced, exists_now, gone, outside, slate]);
    let mut expected = vec![
        HotExitRestore::Restore {
            path: "same.md".into(),
        },
        HotExitRestore::RestoreStale {
            path: "stale.md".into(),
        },
    ];
    // The file-system provider gives every platform an identity, so the
    // replacement is told apart (with none on either side it would be the
    // recorded accepted risk, which this provider never reaches).
    assert!(session.note_file_identity("replaced.md").unwrap().is_some());
    expected.push(HotExitRestore::ExistsNow {
        path: "replaced.md".into(),
    });
    expected.extend([
        HotExitRestore::ExistsNow {
            path: "new-over.md".into(),
        },
        HotExitRestore::Gone {
            path: "empty-a.md".into(),
        },
        HotExitRestore::Recovered {
            path: "Recovered note.md".into(),
        },
        HotExitRestore::Recovered {
            path: "Recovered note 2.md".into(),
        },
    ]);
    assert_eq!(decisions, expected);
}

#[test]
fn a_recovered_name_never_names_a_file_in_the_vault() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("Recovered note.md"), "taken").unwrap();
    let session = session_at(tmp.path());
    assert_eq!(
        session.classify_hot_exit_entries(&[kept("../x.md", None, None)]),
        [HotExitRestore::Recovered {
            path: "Recovered note 2.md".into()
        }]
    );
}

#[test]
fn the_first_save_refuses_a_file_put_at_the_path_after_classification() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("note.md"), "# Baseline\n").unwrap();
    let session = session_at(root);
    let baseline = hash_of(root, "note.md");
    // The file-system provider gives every platform an identity; the fact
    // must exercise the refusal, never pass by skipping it.
    let identity = session
        .note_file_identity("note.md")
        .unwrap()
        .expect("a local note has a file identity");
    // A backup restore puts another file with the same bytes at the path.
    std::fs::remove_file(root.join("note.md")).unwrap();
    std::fs::write(root.join("other.md"), "keep the inode busy").unwrap();
    std::fs::write(root.join("note.md"), "# Baseline\n").unwrap();
    let refused = session.save_text_checked("note.md", "unsaved", Some(&baseline), Some(&identity));
    assert!(
        matches!(refused, Err(VaultError::WriteConflict { .. })),
        "{refused:?}"
    );
    assert_eq!(
        std::fs::read_to_string(root.join("note.md")).unwrap(),
        "# Baseline\n"
    );
    // The right file, and the same save lands.
    let now = session.note_file_identity("note.md").unwrap();
    session
        .save_text_checked("note.md", "unsaved", Some(&baseline), now.as_deref())
        .unwrap();
    assert_eq!(
        std::fs::read_to_string(root.join("note.md")).unwrap(),
        "unsaved"
    );
}

#[test]
fn save_text_still_saves_without_an_identity() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("note.md"), "a").unwrap();
    let session = session_at(tmp.path());
    let baseline = hash_of(tmp.path(), "note.md");
    session
        .save_text_checked("note.md", "b", Some(&baseline), None)
        .unwrap();
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("note.md")).unwrap(),
        "b"
    );
}

#[test]
fn an_identity_without_a_hash_is_refused_not_skipped() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("note.md"), "a").unwrap();
    let session = session_at(tmp.path());
    let refused = session.save_text_checked("note.md", "b", None, Some("any-identity"));
    assert!(
        matches!(refused, Err(VaultError::InvalidArgument { .. })),
        "{refused:?}"
    );
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("note.md")).unwrap(),
        "a"
    );
}

#[test]
fn a_recovered_name_never_takes_another_kept_entrys_path() {
    let tmp = tempfile::tempdir().unwrap();
    let session = session_at(tmp.path());
    // A kept note named "Recovered note.md" whose file is gone: its tab
    // will save there, so the recovered tab must not.
    let gone = kept("Recovered note.md", Some("h".into()), None);
    let outside = kept("../x.md", None, None);
    assert_eq!(
        session.classify_hot_exit_entries(&[gone, outside]),
        [
            HotExitRestore::Gone {
                path: "Recovered note.md".into()
            },
            HotExitRestore::Recovered {
                path: "Recovered note 2.md".into()
            }
        ]
    );
}

/// NTFS and APFS compare names without case: a kept `recovered NOTE.md`
/// takes `Recovered note.md` from a recovered tab too.
#[test]
fn a_recovered_name_never_takes_a_case_alias_of_a_kept_path() {
    let tmp = tempfile::tempdir().unwrap();
    let session = session_at(tmp.path());
    let alias = kept("recovered NOTE.md", Some("h".into()), None);
    let outside = kept("../x.md", None, None);
    assert_eq!(
        session.classify_hot_exit_entries(&[alias, outside])[1],
        HotExitRestore::Recovered {
            path: "Recovered note 2.md".into()
        }
    );
}

/// With every numbered name spoken for, the fallback is still unique among
/// the kept paths, including one that is the fallback's first spelling.
#[test]
fn the_recovered_fallback_name_is_unique_among_kept_paths() {
    let tmp = tempfile::tempdir().unwrap();
    let session = session_at(tmp.path());
    let mut entries: Vec<HotExitEntry> = (1..=1_000)
        .map(|n| {
            let path = if n == 1 {
                "Recovered note.md".to_string()
            } else {
                format!("Recovered note {n}.md")
            };
            kept(&path, Some("h".into()), None)
        })
        .collect();
    entries.push(kept("Recovered note tab1.md", Some("h".into()), None));
    entries.push(kept("../x.md", None, None));
    let decisions = session.classify_hot_exit_entries(&entries);
    assert_eq!(
        decisions.last().unwrap(),
        &HotExitRestore::Recovered {
            path: "Recovered note tab1 2.md".into()
        }
    );
}

/// macOS gives a volume a new `st_dev` when it remounts, so a note's
/// identity is its inode and birth time alone (the key names the volume).
#[cfg(unix)]
#[test]
fn a_notes_unix_identity_never_carries_st_dev() {
    use std::os::unix::fs::MetadataExt as _;
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("note.md"), "a").unwrap();
    let session = session_at(tmp.path());
    let identity = session.note_file_identity("note.md").unwrap().unwrap();
    let meta = std::fs::symlink_metadata(tmp.path().join("note.md")).unwrap();
    assert!(
        identity.starts_with(&format!("unix:{:x}-", meta.ino())),
        "{identity}"
    );
    assert_eq!(identity.matches('-').count(), 1, "{identity}");
}

#[cfg(windows)]
mod windows {
    use super::*;

    /// A note that is itself a reparse point (here a symbolic link; a
    /// cloud placeholder alike) has no identity to vouch for: `None`, not
    /// an error that would fail every restore and save beside it.
    #[test]
    fn a_reparse_point_note_has_no_identity_rather_than_an_error() {
        let tmp = tempfile::tempdir().unwrap();
        std::fs::write(tmp.path().join("target.md"), "a").unwrap();
        if std::os::windows::fs::symlink_file("target.md", tmp.path().join("link.md")).is_err() {
            // Creating a link needs Developer Mode or the privilege.
            return;
        }
        let session = session_at(tmp.path());
        assert_eq!(session.note_file_identity("link.md").unwrap(), None);
        assert!(session.note_file_identity("target.md").unwrap().is_some());
    }

    fn key_of(root: &Path) -> HotExitKeyResult {
        VaultSession::from_filesystem(root.to_path_buf())
            .unwrap()
            .hot_exit_key()
    }

    #[test]
    fn a_local_ntfs_vault_has_a_key_with_its_creation_time() {
        let tmp = tempfile::tempdir().unwrap();
        let HotExitKeyResult::Key(key) = key_of(tmp.path()) else {
            panic!("no key for a local NTFS folder");
        };
        let parts: Vec<&str> = key.text.split('-').collect();
        assert!(key.text.starts_with("win:"), "{}", key.text);
        assert_eq!(parts.len(), 3, "volume, id, creation time: {}", key.text);
        assert_eq!(parts[1].len(), 32, "the full 128-bit id: {}", key.text);
    }

    #[test]
    fn no_hot_exit_key_without_a_lasting_local_identity() {
        let tmp = tempfile::tempdir().unwrap();
        let mut env = fault_guard();
        for (seam, reason) in [
            (
                "SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED",
                crate::a11y::HOT_EXIT_REASON_WEAK_ROOT,
            ),
            (
                "SLATE_TEST_HOT_EXIT_SENTINEL_ID",
                crate::a11y::HOT_EXIT_REASON_WEAK_ROOT,
            ),
            (
                "SLATE_TEST_HOT_EXIT_REMOTE",
                crate::a11y::HOT_EXIT_REASON_REMOTE,
            ),
        ] {
            let vault = tmp.path().join(format!("wb29a-{}", seam.to_lowercase()));
            std::fs::create_dir(&vault).unwrap();
            env.set(seam, "wb29a-");
            assert_eq!(
                key_of(&vault),
                HotExitKeyResult::Unavailable { reason },
                "{seam}"
            );
            env.set(seam, "no-fixture-matches-this");
        }
    }

    #[test]
    fn the_key_includes_the_roots_creation_time() {
        let tmp = tempfile::tempdir().unwrap();
        let vault = tmp.path().join("wb29a-reused");
        let mut env = fault_guard();
        env.set("SLATE_TEST_HOT_EXIT_REUSED_ID", "wb29a-reused");
        std::fs::create_dir(&vault).unwrap();
        let first_result = key_of(&vault);
        let HotExitKeyResult::Key(first) = first_result else {
            panic!("no first key: {first_result:?}");
        };
        std::fs::remove_dir_all(&vault).unwrap();
        std::thread::sleep(std::time::Duration::from_millis(50));
        std::fs::create_dir(&vault).unwrap();
        let HotExitKeyResult::Key(second) = key_of(&vault) else {
            panic!("no second key");
        };
        assert_ne!(
            first, second,
            "a recreated root with a reused id keeps another key"
        );
    }
}

#[cfg(target_os = "linux")]
#[test]
fn linux_has_no_hot_exit_key() {
    let tmp = tempfile::tempdir().unwrap();
    assert_eq!(
        VaultSession::from_filesystem(tmp.path().to_path_buf())
            .unwrap()
            .hot_exit_key(),
        HotExitKeyResult::Unavailable {
            reason: crate::a11y::HOT_EXIT_REASON_WEAK_ROOT
        }
    );
}

#[cfg(target_os = "macos")]
mod macos {
    use super::*;

    fn key_of(root: &Path) -> HotExitKeyResult {
        VaultSession::from_filesystem(root.to_path_buf())
            .unwrap()
            .hot_exit_key()
    }

    #[test]
    fn the_mac_key_is_the_volume_uuid_inode_and_birth_never_st_dev() {
        use std::os::unix::fs::MetadataExt as _;
        let tmp = tempfile::tempdir().unwrap();
        let HotExitKeyResult::Key(key) = key_of(tmp.path()) else {
            panic!("no key for a local APFS folder");
        };
        assert!(key.text.starts_with("mac:"), "{}", key.text);
        let inode = std::fs::metadata(tmp.path()).unwrap().ino();
        assert!(key.text.contains(&format!("{inode:016x}")), "{}", key.text);
        // The birth component is the folder's own creation time, in
        // nanoseconds, not a constant.
        let born = std::fs::metadata(tmp.path())
            .unwrap()
            .created()
            .unwrap()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos() as u64;
        assert!(
            key.text.ends_with(&format!("-{born:016x}")),
            "{} (born {born:016x})",
            key.text
        );
        let dev = std::fs::metadata(tmp.path()).unwrap().dev();
        assert!(!key.text.contains(&format!("-{dev:x}-")), "{}", key.text);
        // Two folders on one volume share the UUID and differ in the inode.
        let other = tempfile::tempdir().unwrap();
        let HotExitKeyResult::Key(other_key) = key_of(other.path()) else {
            panic!("no second key");
        };
        assert_ne!(key, other_key);
        assert_eq!(key.text.split('-').next(), other_key.text.split('-').next());
    }

    #[test]
    fn no_hot_exit_key_on_an_excluded_or_remote_volume() {
        let tmp = tempfile::tempdir().unwrap();
        let mut env = fault_guard();
        for (seam, reason) in [
            (
                "SLATE_TEST_HOT_EXIT_EXCLUDED_VOLUME",
                crate::a11y::HOT_EXIT_REASON_WEAK_ROOT,
            ),
            (
                "SLATE_TEST_HOT_EXIT_REMOTE",
                crate::a11y::HOT_EXIT_REASON_REMOTE,
            ),
        ] {
            let vault = tmp.path().join(format!("wb29a-{}", seam.to_lowercase()));
            std::fs::create_dir(&vault).unwrap();
            env.set(seam, "wb29a-");
            assert_eq!(
                key_of(&vault),
                HotExitKeyResult::Unavailable { reason },
                "{seam}"
            );
            env.set(seam, "no-fixture-matches-this");
        }
    }
}

// ---- WB-29 §7: the identity of the file a read or a save touched.

/// Replaces `path` under `root` with a new file holding the same bytes (a
/// rename over it, so another file — a new identity — stands there).
#[cfg(any(windows, unix))]
fn replace_with_same_bytes(root: &Path, path: &str) {
    let target = root.join(path);
    let bytes = std::fs::read(&target).unwrap();
    let staged = root.join(".replacement.tmp");
    std::fs::write(&staged, &bytes).unwrap();
    std::fs::rename(&staged, &target).unwrap();
}

#[test]
fn the_identified_read_names_the_file_it_read() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "---\nk: v\n---\n# A\n").unwrap();
    let session = session_at(root);
    let read = session.read_text_identified("a.md").unwrap();
    assert_eq!(read.text, "---\nk: v\n---\n# A\n");
    assert_eq!(read.content_hash, hash_of(root, "a.md"));
    assert_eq!(
        read.file_identity,
        session.note_file_identity("a.md").unwrap()
    );
    let parts = session.read_note_parts("a.md").unwrap();
    assert_eq!(parts.content_hash, read.content_hash);
    assert_eq!(parts.file_identity, read.file_identity);
    #[cfg(any(windows, unix))]
    assert!(read.file_identity.is_some());
}

/// Codex round 6: a replacement between the bytes and the identity never
/// lends the read its identity: the handle that read and the entry now at
/// the path disagree, so the read reports none — never the replacement's.
#[cfg(any(windows, unix))]
#[test]
fn an_identified_read_names_its_own_handle_never_a_replacement() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path().to_path_buf();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = session_at(&root);
    let original = session.note_file_identity("a.md").unwrap();
    {
        let root = root.clone();
        crate::vault::AFTER_READ_BEFORE_IDENTITY.with(|cell| {
            *cell.borrow_mut() = Some(Box::new(move || replace_with_same_bytes(&root, "a.md")));
        });
    }
    let read = session.read_text_identified("a.md").unwrap();
    let replacement = session.note_file_identity("a.md").unwrap();
    assert_ne!(
        original, replacement,
        "premise: the replacement is another file"
    );
    assert_eq!(read.file_identity, None);
}

#[test]
fn a_save_reports_the_file_it_published() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = session_at(root);
    let report = session
        .save_text("a.md", "# A, edited\n", Some(&hash_of(root, "a.md")))
        .unwrap();
    assert_eq!(
        report.file_identity,
        session.note_file_identity("a.md").unwrap()
    );
    #[cfg(any(windows, unix))]
    assert!(report.file_identity.is_some());
    // A checked save, and a create, report theirs the same way.
    let checked = session
        .save_text_checked(
            "a.md",
            "# A, again\n",
            Some(&report.new_content_hash),
            report.file_identity.as_deref(),
        )
        .unwrap();
    assert_eq!(
        checked.file_identity,
        session.note_file_identity("a.md").unwrap()
    );
    let created = session.create_exclusive("b.md", "# B\n").unwrap();
    assert_eq!(
        created.file_identity,
        session.note_file_identity("b.md").unwrap()
    );
}

/// Codex round 6: a file put at the path after the publish never lends
/// the report its identity: the staged file the save renamed into place
/// and the entry now at the path disagree, so the report names none.
#[cfg(any(windows, unix))]
#[test]
fn a_save_report_names_the_published_file_never_a_replacement() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path().to_path_buf();
    std::fs::write(root.join("a.md"), "# A\n").unwrap();
    let session = session_at(&root);
    let published = std::rc::Rc::new(std::cell::RefCell::new(None));
    {
        let root = root.clone();
        let published = std::rc::Rc::clone(&published);
        crate::vault::AFTER_PUBLISH_BEFORE_REPORT.with(|cell| {
            *cell.borrow_mut() = Some(Box::new(move || {
                let target = root.join("a.md");
                *published.borrow_mut() = Some(std::fs::read(&target).unwrap());
                replace_with_same_bytes(&root, "a.md");
            }));
        });
    }
    let before = session.note_file_identity("a.md").unwrap();
    let report = session
        .save_text("a.md", "# A, edited\n", Some(&hash_of(&root, "a.md")))
        .unwrap();
    assert_eq!(
        published.borrow().as_deref(),
        Some("# A, edited\n".as_bytes()),
        "premise: the seam ran after the publish"
    );
    let replacement = session.note_file_identity("a.md").unwrap();
    assert!(replacement.is_some() && replacement != before);
    assert_eq!(report.file_identity, None);
}

/// A property delete that changes nothing reports the identity its own
/// read saw.
#[test]
fn an_unchanged_property_delete_reports_the_file_it_read() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("a.md"), "---\nk: v\n---\n# A\n").unwrap();
    let session = session_at(root);
    let report = session
        .delete_property("a.md", "absent", Some(&hash_of(root, "a.md")))
        .unwrap();
    assert_eq!(report.new_content_hash, hash_of(root, "a.md"));
    assert_eq!(
        report.file_identity,
        session.note_file_identity("a.md").unwrap()
    );
}

/// Red team and Codex (PR 1): a note that is itself a link reads through
/// the link, so its bytes are the target's while `note_file_identity` names
/// the entry (the link on Unix, none on Windows). The identified read names
/// neither: it reports none, and a checked save from that baseline lands.
#[cfg(any(windows, unix))]
#[test]
fn an_identified_read_of_a_link_names_no_identity_and_its_save_lands() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    std::fs::write(root.join("target.md"), "# Target\n").unwrap();
    #[cfg(windows)]
    let linked = std::os::windows::fs::symlink_file(root.join("target.md"), root.join("link.md"));
    #[cfg(unix)]
    let linked = std::os::unix::fs::symlink(root.join("target.md"), root.join("link.md"));
    if linked.is_err() {
        // Windows without the symbolic-link privilege or developer mode:
        // nothing to compare.
        return;
    }
    let session = session_at(root);
    let read = session.read_text_identified("link.md").unwrap();
    assert_eq!(read.text, "# Target\n");
    assert_eq!(read.file_identity, None);
    session
        .save_text_checked(
            "link.md",
            "# Saved\n",
            Some(&read.content_hash),
            read.file_identity.as_deref(),
        )
        .unwrap();
}
