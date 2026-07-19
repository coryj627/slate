// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! FL6-1 (#667): folder-note detection in the listing and the atomic
//! folder+note compound rename — exact-stem convention, preflight,
//! rollback, merged report, and backlink repair.

use slate_core::{CancelToken, Paging, VaultError, VaultSession};

fn write(vault: &std::path::Path, rel: &str, content: &str) {
    let path = vault.join(rel);
    std::fs::create_dir_all(path.parent().unwrap()).unwrap();
    std::fs::write(path, content).unwrap();
}

fn read(vault: &std::path::Path, rel: &str) -> String {
    std::fs::read_to_string(vault.join(rel)).unwrap()
}

fn open(vault: &std::path::Path) -> VaultSession {
    let session = VaultSession::from_filesystem(vault.to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    session
}

fn dir_flags(session: &VaultSession, parent: &str) -> Vec<(String, bool)> {
    session
        .list_dir_children(
            parent,
            Paging {
                cursor: None,
                limit: 100,
            },
        )
        .unwrap()
        .dirs
        .into_iter()
        .map(|d| (d.path, d.has_folder_note))
        .collect()
}

// MARK: detection

#[test]
fn detection_requires_exact_stem_case_and_markdown() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "Projects/Projects.md", "note\n");
    write(tmp.path(), "Case/case.md", "wrong case\n");
    write(tmp.path(), "Binary/Binary.pdf", "%PDF fake\n");
    write(tmp.path(), "Partial/Partials.md", "wrong stem\n");
    write(tmp.path(), "Empty/other.md", "not the stem\n");
    let session = open(tmp.path());
    assert_eq!(
        dir_flags(&session, ""),
        vec![
            ("Binary".into(), false),
            ("Case".into(), false),
            ("Empty".into(), false),
            ("Partial".into(), false),
            ("Projects".into(), true),
        ]
    );
}

#[test]
fn detection_is_boundary_anchored_and_depth_correct() {
    let tmp = tempfile::tempdir().unwrap();
    // "XA/A.md" must NOT flag any folder named "A" (suffix compare is
    // slash-anchored), and nested folder notes flag their OWN level.
    write(tmp.path(), "XA/A.md", "not a folder note\n");
    write(tmp.path(), "outer/inner/inner.md", "nested folder note\n");
    write(tmp.path(), "outer/outer.md", "outer folder note\n");
    let session = open(tmp.path());
    assert_eq!(
        dir_flags(&session, ""),
        vec![("outer".into(), true), ("XA".into(), false)]
    );
    assert_eq!(
        dir_flags(&session, "outer"),
        vec![("outer/inner".into(), true)]
    );
}

#[test]
fn folder_note_still_counts_in_child_file_count() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "P/P.md", "folder note\n");
    write(tmp.path(), "P/other.md", "sibling\n");
    let session = open(tmp.path());
    let listing = session
        .list_dir_children(
            "",
            Paging {
                cursor: None,
                limit: 10,
            },
        )
        .unwrap();
    // Hiding the represented row is the UI's job; counts stay honest
    // (spec rule 2).
    assert_eq!(listing.dirs[0].child_file_count, 2);
    assert!(listing.dirs[0].has_folder_note);
}

#[test]
fn rename_in_and_out_updates_presence_via_normal_listing() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "P/note.md", "plain\n");
    let session = open(tmp.path());
    assert_eq!(dir_flags(&session, ""), vec![("P".into(), false)]);
    session.rename_file("P/note.md", "P.md").unwrap();
    assert_eq!(dir_flags(&session, ""), vec![("P".into(), true)]);
    session.rename_file("P/P.md", "away.md").unwrap();
    assert_eq!(dir_flags(&session, ""), vec![("P".into(), false)]);
}

// MARK: compound rename

#[test]
fn compound_rename_renames_note_rewrites_backlinks_and_merges_report() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "Projects/Projects.md", "# The folder note\n");
    write(tmp.path(), "Projects/sibling.md", "sibling body\n");
    write(
        tmp.path(),
        "refs.md",
        "Links: [[Projects/Projects]] and [[Projects/sibling]]\n",
    );
    let session = open(tmp.path());
    let report = session.rename_folder_with_note("Projects", "Work").unwrap();

    // One merged report: the note maps ORIGINAL → FINAL (no interim
    // hop), the sibling maps through the folder rename.
    assert!(
        report
            .moved
            .contains(&("Projects/Projects.md".into(), "Work/Work.md".into())),
        "collapsed note mapping, got {:?}",
        report.moved
    );
    assert!(
        report
            .moved
            .contains(&("Projects/sibling.md".into(), "Work/sibling.md".into()))
    );
    assert!(
        !report.moved.iter().any(|(old, _)| old.starts_with("Work/")),
        "no interim-hop pairs leak into the report: {:?}",
        report.moved
    );
    assert!(report.failed.is_empty());

    // Filesystem end-state + backlinks repaired to the FINAL paths.
    assert!(tmp.path().join("Work/Work.md").exists());
    assert!(!tmp.path().join("Work/Projects.md").exists());
    let refs = read(tmp.path(), "refs.md");
    assert!(refs.contains("[[Work/Work]]"), "{refs}");
    assert!(refs.contains("[[Work/sibling]]"), "{refs}");
}

#[test]
fn compound_preflight_refuses_note_collision_before_any_mutation() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "P/P.md", "folder note\n");
    write(tmp.path(), "P/New.md", "already here\n");
    let session = open(tmp.path());
    let err = session.rename_folder_with_note("P", "New").unwrap_err();
    assert!(
        matches!(err, VaultError::DestinationExists { .. }),
        "got {err:?}"
    );
    // Nothing moved: complete preflight before step 1.
    assert!(tmp.path().join("P/P.md").exists());
    assert!(!tmp.path().join("New").exists());
}

#[test]
fn compound_preflight_requires_an_indexed_folder_note() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "P/other.md", "no folder note here\n");
    let session = open(tmp.path());
    let err = session.rename_folder_with_note("P", "Q").unwrap_err();
    assert!(matches!(err, VaultError::InvalidPath { .. }), "got {err:?}");
    assert!(tmp.path().join("P/other.md").exists());
}

#[test]
fn compound_second_step_failure_rolls_the_folder_rename_back() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "P/P.md", "folder note\n");
    let session = open(tmp.path());
    // An UNINDEXED directory squatting on the post-rename note path:
    // index-based preflight cannot see it, the note's os-level rename
    // then fails (destination is a directory), and the compound rolls
    // the folder rename back.
    std::fs::create_dir_all(tmp.path().join("P/Q.md")).unwrap();
    let err = session.rename_folder_with_note("P", "Q").unwrap_err();
    assert!(
        matches!(err, VaultError::InvalidArgument { ref message }
            if message.contains("rolled back")),
        "got {err:?}"
    );
    // Pre-operation state restored.
    assert!(tmp.path().join("P/P.md").exists());
    assert!(!tmp.path().join("Q").exists());
    assert_eq!(dir_flags(&session, ""), vec![("P".into(), true)]);
}

#[test]
fn compound_case_only_rename_is_refused_like_the_plain_structural_path() {
    // Parity: the shipped folder-rename preflight refuses a
    // case-insensitive self-collision (case-preserving filesystems);
    // the compound inherits exactly that semantics rather than
    // inventing a second rename dialect.
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "notes/notes.md", "folder note\n");
    let session = open(tmp.path());
    let compound = session
        .rename_folder_with_note("notes", "Notes")
        .unwrap_err();
    assert!(
        matches!(compound, VaultError::DestinationExists { .. }),
        "got {compound:?}"
    );
    let plain = session.rename_folder("notes", "Notes").unwrap_err();
    assert!(
        matches!(plain, VaultError::DestinationExists { .. }),
        "got {plain:?}"
    );
    assert!(tmp.path().join("notes/notes.md").exists());
}
