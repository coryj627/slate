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
    assert_eq!(tree.audio_summary, "1 tag, 2 untagged notes.");
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
    assert_eq!(report.audio_summary, "Tagged 1 file with #project.");
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
        "Removed #project from 2 files. 1 still has it inline."
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
    assert_eq!(report.audio_summary, "Removed #reading from 1 file.");
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
        "Removed #project from 0 files. 1 still has it inline."
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

// MARK: FL5-2/3b · untagged scope + selection tags (FL-11 core support)

#[test]
fn untagged_files_shares_the_filter_pipeline_semantics() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "tagged.md", "#a body\n");
    write(
        tmp.path(),
        "titled.md",
        "---\ntitle: Zebra Title\n---\nplain\n",
    );
    write(tmp.path(), "alpha.md", "plain body\n");
    write(tmp.path(), "asset.pdf", "%PDF-1.4 fake\n");
    let session = open(tmp.path());
    let page = session
        .untagged_files(slate_core::Paging {
            cursor: None,
            limit: 10,
        })
        .unwrap();
    // Markdown-only, tagged excluded, effective-name order (alpha <
    // Zebra Title), FL4-2 summary shape.
    assert_eq!(
        page.files
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        vec!["alpha.md", "titled.md"]
    );
    assert_eq!(page.total, 2);
    assert_eq!(page.audio_summary, "2 results.");

    // Pagination cursor round-trips like any filter page.
    let first = session
        .untagged_files(slate_core::Paging {
            cursor: None,
            limit: 1,
        })
        .unwrap();
    assert_eq!(first.files[0].path, "alpha.md");
    let rest = session
        .untagged_files(slate_core::Paging {
            cursor: first.next_cursor.clone(),
            limit: 1,
        })
        .unwrap();
    assert_eq!(rest.files[0].path, "titled.md");
    assert!(rest.next_cursor.is_none());
}

#[test]
fn tags_for_files_counts_distinct_files_alphabetically() {
    let tmp = tempfile::tempdir().unwrap();
    write(
        tmp.path(),
        "one.md",
        "---\ntags:\n  - beta\n---\n#alpha #alpha body\n",
    );
    write(tmp.path(), "two.md", "#alpha\n");
    write(tmp.path(), "three.md", "#zulu\n");
    let session = open(tmp.path());
    let counts = session
        .tags_for_files(vec!["one.md".into(), "two.md".into()])
        .unwrap();
    let shaped: Vec<(&str, u32)> = counts
        .iter()
        .map(|c| (c.tag.as_str(), c.file_count))
        .collect();
    // zulu excluded (three.md not selected); duplicate inline mentions
    // count one file; alphabetical order.
    assert_eq!(shaped, vec![("alpha", 2), ("beta", 1)]);
    assert!(session.tags_for_files(vec![]).unwrap().is_empty());
}

// MARK: WB-25 (#1290) · the paged, cancellable tag tree

/// Walk every page at `limit` from no cursor, passing each `next_cursor`
/// back; fails if the walk takes more pages than the tree has rows, plus
/// one (a resume that repeats cannot loop forever). Every page carries the
/// same `total_entries`, `untagged_count` and `audio_summary`.
fn walk(session: &VaultSession, limit: u32) -> Vec<slate_core::TagTreePageEntry> {
    let rows = slate_core::tag_tree::flatten_preorder(&session.tag_tree().unwrap()).len();
    let mut out = Vec::new();
    let mut cursor: Option<String> = None;
    let mut shared: Option<(u32, u32, String)> = None;
    for _ in 0..=rows + 1 {
        let page = session
            .tag_tree_page(
                slate_core::Paging {
                    cursor: cursor.clone(),
                    limit,
                },
                &CancelToken::new(),
            )
            .unwrap();
        assert!(page.entries.len() <= limit as usize);
        assert!(
            !page.entries.is_empty() || rows == 0,
            "an empty page in a non-empty tree"
        );
        let header = (
            page.total_entries,
            page.untagged_count,
            page.audio_summary.clone(),
        );
        assert_eq!(
            shared.get_or_insert_with(|| header.clone()),
            &header,
            "a page's header changed"
        );
        out.extend(page.entries);
        cursor = page.next_cursor;
        if cursor.is_none() {
            assert_eq!(header.0 as usize, rows, "total_entries");
            return out;
        }
    }
    panic!("the walk at limit {limit} exceeded its page bound");
}

/// Each row's segment path, rebuilt from `(depth, segment)` with the host's
/// ancestor stack.
fn segment_paths(entries: &[slate_core::TagTreePageEntry]) -> Vec<String> {
    let mut stack: Vec<String> = Vec::new();
    entries
        .iter()
        .map(|entry| {
            stack.truncate(entry.depth as usize);
            stack.push(entry.segment.clone());
            stack.join("/")
        })
        .collect()
}

fn tiny_fixture(vault: &std::path::Path) {
    write(vault, "a.md", "#a\n");
    write(vault, "az.md", "#a/z\n");
    write(vault, "ab.md", "#a-b\n");
    write(vault, "pqr.md", "#p/q/r\n");
    write(vault, "under.md", "#a_b/child\n");
    write(vault, "x.md", "#axb/child\n");
    write(vault, "slash.md", "---\ntags: [\"/a\"]\n---\nbody\n");
    write(vault, "slashes.md", "---\ntags: [\"//a\"]\n---\nbody\n");
    write(vault, "plain.md", "untagged\n");
}

#[test]
fn tag_tree_page_with_a_tiny_limit_walks_every_entry_in_pre_order() {
    let tmp = tempfile::tempdir().unwrap();
    tiny_fixture(tmp.path());
    let session = open(tmp.path());
    let expected = slate_core::tag_tree::flatten_preorder(&session.tag_tree().unwrap());
    let fulls: Vec<&str> = expected
        .iter()
        .take(5)
        .map(|entry| entry.full.as_str())
        .collect();
    assert_eq!(
        fulls,
        ["", "", "a", "a", "a"],
        "premise: the leading-empty-segment nodes lead"
    );
    for limit in [1, 2] {
        let walked = walk(&session, limit);
        assert_eq!(walked, expected, "limit {limit}");
        let paths = segment_paths(&walked);
        let distinct: std::collections::BTreeSet<&String> = paths.iter().collect();
        assert_eq!(
            distinct.len(),
            paths.len(),
            "a segment path repeats at limit {limit}"
        );
    }
}

#[test]
fn tag_tree_page_keeps_tags_holding_characters_below_the_separator_apart_at_every_page_boundary() {
    let tmp = tempfile::tempdir().unwrap();
    let tags = [
        "a",
        "a/z",
        "\"a\\x01z\"",
        "\"a\\x01z/k\"",
        "\"a\\0b\"",
        "\"a\\0b/c\"",
        "a-b",
        "a.b/c",
        "\"/a\"",
        "\"//a\"",
    ];
    for (i, tag) in tags.iter().enumerate() {
        write(
            tmp.path(),
            &format!("n{i}.md"),
            &format!("---\ntags: [{tag}]\n---\nbody\n"),
        );
    }
    let session = open(tmp.path());
    let expected = slate_core::tag_tree::flatten_preorder(&session.tag_tree().unwrap());
    for limit in [1, 2, 3] {
        let walked = walk(&session, limit);
        assert_eq!(walked, expected, "limit {limit}");
        let paths = segment_paths(&walked);
        let distinct: std::collections::BTreeSet<&String> = paths.iter().collect();
        assert_eq!(
            distinct.len(),
            paths.len(),
            "a segment path repeats at limit {limit}"
        );
        assert!(
            paths.iter().any(|p| p == "a/z"),
            "a/z is missing at limit {limit}"
        );
        assert!(
            paths.iter().any(|p| p == "a\u{1}z"),
            "a\\x01z is missing at limit {limit}"
        );
    }
}

#[test]
fn tag_tree_page_resumes_after_a_cursor_inside_a_subtree() {
    let tmp = tempfile::tempdir().unwrap();
    tiny_fixture(tmp.path());
    let session = open(tmp.path());
    // One row a page: every row after the first is the answer to the
    // cursor its predecessor's page handed back.
    let entries = walk(&session, 1);
    let keys = segment_paths(&entries);
    let first_after = |key: &str| {
        let at = keys.iter().position(|k| k == key).unwrap();
        let entry = &entries[at + 1];
        (entry.full.clone(), entry.depth)
    };
    assert_eq!(
        first_after("a"),
        ("a/z".to_string(), 1),
        "after a comes a/z, not a-b"
    );
    assert_eq!(first_after("p/q"), ("p/q/r".to_string(), 2));
    assert_eq!(
        first_after("/a"),
        ("a".to_string(), 0),
        "after /a's leaf comes the root a"
    );
    assert_eq!(
        first_after("/"),
        ("a".to_string(), 2),
        "after the implied \"\" under the root comes //a's leaf"
    );
}

fn page(
    session: &VaultSession,
    cursor: Option<String>,
    limit: u32,
) -> Result<slate_core::TagTreePage, slate_core::VaultError> {
    session.tag_tree_page(slate_core::Paging { cursor, limit }, &CancelToken::new())
}

fn assert_refused(result: Result<slate_core::TagTreePage, slate_core::VaultError>, why: &str) {
    match result {
        Err(slate_core::VaultError::InvalidArgument { message }) => {
            assert!(message.contains(why), "{message}")
        }
        other => panic!("expected a refusal ({why}), got {other:?}"),
    }
}

/// WB-25 (codex round 1): a continuation is bound to the index its page
/// read. After a write — a tag removed, one added before the cursor, or a
/// count changed — the next page is refused as stale instead of joining two
/// index states, and a walk from page one reads the new index whole.
#[test]
fn tag_tree_page_refuses_a_continuation_after_the_index_changed() {
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "abc.md", "#a/b/c\n");
    write(tmp.path(), "z.md", "#z\n");
    let session = open(tmp.path());
    let first = page(&session, None, 1).unwrap();
    assert_eq!(first.total_entries, 4);
    let cursor = first.next_cursor.clone().unwrap();
    // Unchanged, the continuation is honoured.
    assert_eq!(
        page(&session, Some(cursor.clone()), 1).unwrap().entries[0].full,
        "a/b"
    );

    session.create_exclusive("m.md", "#m\n").unwrap();
    assert_refused(page(&session, Some(cursor), 1), "stale");

    let fresh = walk(&session, 1);
    assert_eq!(
        segment_paths(&fresh),
        ["a", "a/b", "a/b/c", "m", "z"],
        "the new index, whole"
    );
}

/// A continuation that is not one this session handed out is refused.
#[test]
fn tag_tree_page_refuses_a_foreign_or_malformed_cursor() {
    let tmp = tempfile::tempdir().unwrap();
    tiny_fixture(tmp.path());
    let session = open(tmp.path());
    let other = open(tmp.path());
    let foreign = page(&other, None, 1).unwrap().next_cursor.unwrap();
    assert_refused(page(&session, Some(foreign), 1), "different session");
    for malformed in [
        "a",
        "t1:zz:0:0:a",
        "t1:0000000000000000:x:0:a",
        "t1:0000000000000000:0:0",
    ] {
        assert_refused(page(&session, Some(malformed.to_string()), 1), "malformed");
    }
}

#[test]
fn tag_tree_page_counts_match_the_unpaged_tree() {
    let tmp = tempfile::tempdir().unwrap();
    tiny_fixture(tmp.path());
    let session = open(tmp.path());
    let paged = walk(&session, 500);
    let unpaged = slate_core::tag_tree::flatten_preorder(&session.tag_tree().unwrap());
    let counts = |entries: &[slate_core::TagTreePageEntry]| -> Vec<(u32, u32)> {
        entries
            .iter()
            .map(|entry| (entry.file_count, entry.direct_count))
            .collect()
    };
    assert_eq!(counts(&paged), counts(&unpaged));
    // The "" root counts the two notes whose tags begin with `/`; /a's leaf
    // counts only its own note.
    assert_eq!((paged[0].full.as_str(), paged[0].file_count), ("", 2));
    let slash_a = paged
        .iter()
        .zip(segment_paths(&paged))
        .find(|(_, path)| path == "/a")
        .unwrap()
        .0;
    assert_eq!(slash_a.file_count, 1);
}

#[test]
fn tag_tree_page_like_metachars_do_not_over_match() {
    let tmp = tempfile::tempdir().unwrap();
    tiny_fixture(tmp.path());
    let session = open(tmp.path());
    let paged = walk(&session, 500);
    let a_b = paged.iter().find(|entry| entry.full == "a_b").unwrap();
    assert_eq!(
        a_b.file_count, 1,
        "a_b counts its own child only, never axb/child"
    );
}
