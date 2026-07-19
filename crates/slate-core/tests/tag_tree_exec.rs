// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! FL5-1 (#664) + FL5-3a (refs #666): tag-tree queries and batch tag
//! edits against a real scanned vault — nested/intermediate assembly,
//! distinct-file counts, untagged, determinism, frontmatter-only edits
//! with honest inline remainders, conflict skips, and normative
//! summaries.

use slate_core::{CancelToken, VaultError, VaultSession};

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

/// (full, direct, nested) triples in pre-order, for compact assertions.
fn flatten(tree: &slate_core::TagTree) -> Vec<(String, u32, u32)> {
    fn walk(node: &slate_core::TagTreeNode, out: &mut Vec<(String, u32, u32)>) {
        out.push((node.full.clone(), node.direct_count, node.file_count));
        for child in &node.children {
            walk(child, out);
        }
    }
    let mut out = Vec::new();
    for root in &tree.roots {
        walk(root, &mut out);
    }
    out
}

// MARK: FL5-1 · tag tree

#[test]
fn deep_tag_materializes_intermediate_nodes_with_zero_direct() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "only.md", "#a/b/c body\n");
    let session = open(tmp.path());
    let tree = session.tag_tree().unwrap();
    assert_eq!(
        flatten(&tree),
        vec![
            ("a".into(), 0, 1),
            ("a/b".into(), 0, 1),
            ("a/b/c".into(), 1, 1),
        ]
    );
}

#[test]
fn counts_are_distinct_files_across_nested_semantics() {
    let tmp = tempfile::tempdir().unwrap();
    // one file carries BOTH the parent and a child: parent nested count
    // must not double-count it.
    write(tmp.path(), "both.md", "#a #a/b\n");
    write(tmp.path(), "childonly.md", "#a/c\n");
    write(tmp.path(), "parentonly.md", "#a\n");
    let session = open(tmp.path());
    let tree = session.tag_tree().unwrap();
    assert_eq!(
        flatten(&tree),
        vec![
            ("a".into(), 2, 3),
            ("a/b".into(), 1, 1),
            ("a/c".into(), 1, 1),
        ]
    );
}

#[test]
fn frontmatter_and_inline_tags_meet_under_one_normalization() {
    let tmp = tempfile::tempdir().unwrap();
    write(
        tmp.path(),
        "front.md",
        "---\ntags:\n  - Reading\n---\nbody\n",
    );
    write(tmp.path(), "inline.md", "#reading body\n");
    let session = open(tmp.path());
    let tree = session.tag_tree().unwrap();
    assert_eq!(flatten(&tree), vec![("reading".into(), 2, 2)]);
}

#[test]
fn untagged_counts_markdown_without_tags_only() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "tagged.md", "#a\n");
    write(tmp.path(), "plain.md", "no tags here\n");
    write(tmp.path(), "also-plain.md", "none\n");
    write(tmp.path(), "binary.pdf", "%PDF-1.4 fake\n");
    let session = open(tmp.path());
    let tree = session.tag_tree().unwrap();
    assert_eq!(tree.untagged_count, 2, "the pdf is not an untagged note");
    assert_eq!(tree.audio_summary, "1 tags, 2 untagged notes.");
}

#[test]
fn summary_omits_untagged_clause_when_zero() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "a.md", "#x\n");
    write(tmp.path(), "b.md", "#y/z\n");
    let session = open(tmp.path());
    let tree = session.tag_tree().unwrap();
    // Two REAL tags — the synthesized intermediate `y` is not a tag.
    assert_eq!(tree.audio_summary, "2 tags.");
}

#[test]
fn sibling_order_is_alphabetical_and_permutation_invariant() {
    let build = |names: &[(&str, &str)]| {
        let tmp = tempfile::tempdir().unwrap();
        for (rel, content) in names {
            write(tmp.path(), rel, content);
        }
        let session = open(tmp.path());
        flatten(&session.tag_tree().unwrap())
    };
    let forward = build(&[
        ("1.md", "#zebra\n"),
        ("2.md", "#apple/pie\n"),
        ("3.md", "#apple\n"),
        ("4.md", "#mango\n"),
    ]);
    let reversed = build(&[
        ("1.md", "#mango\n"),
        ("2.md", "#apple\n"),
        ("3.md", "#apple/pie\n"),
        ("4.md", "#zebra\n"),
    ]);
    assert_eq!(
        forward
            .iter()
            .map(|(full, _, _)| full.clone())
            .collect::<Vec<_>>(),
        vec!["apple", "apple/pie", "mango", "zebra"]
    );
    assert_eq!(forward, reversed);
}

// MARK: FL5-3a · batch add

#[test]
fn add_creates_minimal_frontmatter_when_absent() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "plain.md", "just a body\n");
    let session = open(tmp.path());
    let report = session
        .add_tag_to_files(vec!["plain.md".into()], "#Project".into())
        .unwrap();
    assert_eq!(report.changed, 1);
    assert!(report.skipped.is_empty());
    assert_eq!(report.inline_remainder, 0);
    assert_eq!(report.audio_summary, "Tagged 1 files with #project.");
    let content = read(tmp.path(), "plain.md");
    assert!(
        content.starts_with("---\n"),
        "frontmatter created: {content}"
    );
    assert!(
        content.contains("project"),
        "normalized tag stored: {content}"
    );
    assert!(
        content.ends_with("just a body\n"),
        "body untouched: {content}"
    );
    // The index refreshed through the normal save path.
    let tree = session.tag_tree().unwrap();
    assert_eq!(flatten(&tree), vec![("project".into(), 1, 1)]);
}

#[test]
fn add_is_idempotent_under_normalization_and_preserves_bytes() {
    let tmp = tempfile::tempdir().unwrap();
    write(
        tmp.path(),
        "note.md",
        "---\ntitle: Keep Me\ntags:\n  - Reading\n---\nbody\n",
    );
    let session = open(tmp.path());
    let before = read(tmp.path(), "note.md");
    let report = session
        .add_tag_to_files(vec!["note.md".into()], "#reading".into())
        .unwrap();
    assert_eq!(report.changed, 0, "normalized duplicate is a no-op");
    assert!(report.skipped.is_empty());
    assert_eq!(report.audio_summary, "Tagged 0 files with #reading.");
    assert_eq!(read(tmp.path(), "note.md"), before, "no-op writes nothing");
}

#[test]
fn add_preserves_other_frontmatter_and_existing_key_spelling() {
    let tmp = tempfile::tempdir().unwrap();
    write(
        tmp.path(),
        "note.md",
        "---\ntitle: Keep Me\nTags:\n  - existing\n---\nbody\n",
    );
    let session = open(tmp.path());
    let report = session
        .add_tag_to_files(vec!["note.md".into()], "fresh".into())
        .unwrap();
    assert_eq!(report.changed, 1);
    let content = read(tmp.path(), "note.md");
    assert!(content.contains("title: Keep Me"), "{content}");
    assert!(content.contains("existing"), "{content}");
    assert!(content.contains("fresh"), "{content}");
    assert_eq!(
        content.matches("existing").count(),
        1,
        "one tags list, under the original key: {content}"
    );
    assert_eq!(
        content.to_lowercase().matches("tags:").count(),
        1,
        "no duplicate tags key: {content}"
    );
}

#[test]
fn add_batch_reports_multiple_files() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "a.md", "one\n");
    write(tmp.path(), "b.md", "two\n");
    write(tmp.path(), "c.md", "#batch three\n");
    let session = open(tmp.path());
    let report = session
        .add_tag_to_files(
            vec!["a.md".into(), "b.md".into(), "c.md".into()],
            "batch".into(),
        )
        .unwrap();
    // c.md carries #batch inline only — the frontmatter list still
    // gains it (frontmatter and inline are distinct carriers; the
    // file_tags dimension already merged them, so add is by
    // NORMALIZED MEMBERSHIP of the frontmatter list alone).
    assert_eq!(report.changed, 3);
    assert_eq!(report.audio_summary, "Tagged 3 files with #batch.");
}

#[test]
fn invalid_tag_errors_before_touching_files() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "a.md", "body\n");
    let session = open(tmp.path());
    let before = read(tmp.path(), "a.md");
    for bad in ["", "#", "   "] {
        let err = session
            .add_tag_to_files(vec!["a.md".into()], bad.into())
            .unwrap_err();
        assert!(
            matches!(err, VaultError::InvalidQuery { .. }),
            "got {err:?}"
        );
    }
    assert_eq!(read(tmp.path(), "a.md"), before);
}

#[test]
fn add_skips_files_changed_on_disk_since_indexing() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "stale.md", "indexed body\n");
    write(tmp.path(), "fresh.md", "fine\n");
    let session = open(tmp.path());
    // Mutate BEHIND the index: the session's selection knowledge is
    // stale, so the batch must refuse to write over the unseen edit.
    write(tmp.path(), "stale.md", "edited outside the app\n");
    let report = session
        .add_tag_to_files(vec!["stale.md".into(), "fresh.md".into()], "t".into())
        .unwrap();
    assert_eq!(report.changed, 1);
    assert_eq!(report.skipped.len(), 1);
    assert_eq!(report.skipped[0].path, "stale.md");
    assert_eq!(
        read(tmp.path(), "stale.md"),
        "edited outside the app\n",
        "conflicted file untouched"
    );
}

// MARK: FL5-3a · batch remove

#[test]
fn remove_edits_frontmatter_only_and_counts_inline_remainder() {
    let tmp = tempfile::tempdir().unwrap();
    write(
        tmp.path(),
        "both.md",
        "---\ntags:\n  - project\n---\nbody #project inline\n",
    );
    write(
        tmp.path(),
        "front-only.md",
        "---\ntags:\n  - project\n  - keep\n---\nbody\n",
    );
    let session = open(tmp.path());
    let report = session
        .remove_tag_from_files(
            vec!["both.md".into(), "front-only.md".into()],
            "project".into(),
        )
        .unwrap();
    assert_eq!(report.changed, 2);
    assert_eq!(report.inline_remainder, 1);
    assert_eq!(
        report.audio_summary,
        "Removed #project from 2 files. 1 still have it inline."
    );
    let both = read(tmp.path(), "both.md");
    assert!(
        both.contains("#project inline"),
        "inline body untouched: {both}"
    );
    assert!(
        !both.contains("---"),
        "emptied tags list removes the key and the empty shell: {both}"
    );
    let front = read(tmp.path(), "front-only.md");
    assert!(front.contains("keep"), "{front}");
    assert!(!front.contains("project"), "{front}");
}

#[test]
fn remove_matches_under_normalization() {
    let tmp = tempfile::tempdir().unwrap();
    write(
        tmp.path(),
        "note.md",
        "---\ntags:\n  - \"#Reading\"\n---\nbody\n",
    );
    let session = open(tmp.path());
    let report = session
        .remove_tag_from_files(vec!["note.md".into()], "reading".into())
        .unwrap();
    assert_eq!(report.changed, 1);
    assert_eq!(report.audio_summary, "Removed #reading from 1 files.");
    assert!(
        !read(tmp.path(), "note.md")
            .to_lowercase()
            .contains("reading")
    );
}

#[test]
fn remove_missing_tag_is_a_noop_not_a_skip() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "note.md", "---\ntags:\n  - other\n---\nbody\n");
    let session = open(tmp.path());
    let before = read(tmp.path(), "note.md");
    let report = session
        .remove_tag_from_files(vec!["note.md".into()], "absent".into())
        .unwrap();
    assert_eq!(report.changed, 0);
    assert!(report.skipped.is_empty());
    assert_eq!(report.audio_summary, "Removed #absent from 0 files.");
    assert_eq!(read(tmp.path(), "note.md"), before);
}

#[test]
fn remove_counts_inline_remainder_on_unchanged_files_too() {
    let tmp = tempfile::tempdir().unwrap();
    // The tag lives ONLY inline: frontmatter removal is a no-op, but
    // the user asked for the tag to be gone — the honest report says
    // the file still carries it.
    write(tmp.path(), "inline-only.md", "body #project inline\n");
    let session = open(tmp.path());
    let report = session
        .remove_tag_from_files(vec!["inline-only.md".into()], "project".into())
        .unwrap();
    assert_eq!(report.changed, 0);
    assert_eq!(report.inline_remainder, 1);
    assert_eq!(
        report.audio_summary,
        "Removed #project from 0 files. 1 still have it inline."
    );
}

#[test]
fn edits_reindex_so_the_tree_reflects_the_batch() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "a.md", "---\ntags:\n  - old\n---\nbody\n");
    let session = open(tmp.path());
    session
        .add_tag_to_files(vec!["a.md".into()], "new".into())
        .unwrap();
    session
        .remove_tag_from_files(vec!["a.md".into()], "old".into())
        .unwrap();
    let tree = session.tag_tree().unwrap();
    assert_eq!(flatten(&tree), vec![("new".into(), 1, 1)]);
}

// MARK: FL5-3a · review-round hardening

#[test]
fn edits_refuse_non_markdown_and_unindexed_files() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "diagram.canvas", "{\"nodes\":[]}\n");
    write(tmp.path(), "note.md", "body\n");
    let session = open(tmp.path());
    // Created AFTER the scan: not indexed yet.
    write(tmp.path(), "late.md", "late body\n");
    let report = session
        .add_tag_to_files(
            vec!["diagram.canvas".into(), "late.md".into(), "note.md".into()],
            "t".into(),
        )
        .unwrap();
    assert_eq!(report.changed, 1);
    let reasons: Vec<(&str, &str)> = report
        .skipped
        .iter()
        .map(|s| (s.path.as_str(), s.reason.as_str()))
        .collect();
    assert_eq!(
        reasons,
        vec![
            ("diagram.canvas", "not a Markdown note."),
            ("late.md", "not an indexed file."),
        ]
    );
    assert_eq!(
        read(tmp.path(), "diagram.canvas"),
        "{\"nodes\":[]}\n",
        "structured formats are never rewritten"
    );
    assert_eq!(read(tmp.path(), "late.md"), "late body\n");
}

#[test]
fn edits_refuse_hostile_tags_shapes_without_touching_content() {
    let tmp = tempfile::tempdir().unwrap();
    write(
        tmp.path(),
        "mapping.md",
        "---\ntags:\n  owner: alice\n---\nbody\n",
    );
    write(tmp.path(), "scalar.md", "---\ntags: solo\n---\nbody\n");
    write(
        tmp.path(),
        "duplicate.md",
        "---\ntags:\n  - one\nTags:\n  - two\n---\nbody\n",
    );
    let session = open(tmp.path());
    let before_mapping = read(tmp.path(), "mapping.md");
    let before_scalar = read(tmp.path(), "scalar.md");
    let before_duplicate = read(tmp.path(), "duplicate.md");
    let report = session
        .add_tag_to_files(
            vec![
                "duplicate.md".into(),
                "mapping.md".into(),
                "scalar.md".into(),
            ],
            "t".into(),
        )
        .unwrap();
    assert_eq!(report.changed, 0);
    let reasons: Vec<(&str, &str)> = report
        .skipped
        .iter()
        .map(|s| (s.path.as_str(), s.reason.as_str()))
        .collect();
    assert_eq!(
        reasons,
        vec![
            ("duplicate.md", "multiple tags properties."),
            ("mapping.md", "the tags property is not a tag list."),
            ("scalar.md", "the tags property is not a tag list."),
        ]
    );
    assert_eq!(read(tmp.path(), "mapping.md"), before_mapping);
    assert_eq!(read(tmp.path(), "scalar.md"), before_scalar);
    assert_eq!(read(tmp.path(), "duplicate.md"), before_duplicate);
    // Remove refuses the same shapes.
    let report = session
        .remove_tag_from_files(vec!["duplicate.md".into()], "one".into())
        .unwrap();
    assert_eq!(report.changed, 0);
    assert_eq!(report.skipped.len(), 1);
    assert_eq!(read(tmp.path(), "duplicate.md"), before_duplicate);
}
