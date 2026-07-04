// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — full_text_search and FTS index lifecycle.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

#[test]
fn full_text_search_maps_fts5_syntax_errors_to_invalid_query() {
    // Audit-#88-B3 regression: previously a user-supplied FTS5
    // syntax error surfaced as `VaultError::Db`, which is
    // indistinguishable from a corrupt cache.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/a.md", b"some content").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Unbalanced quote — FTS5 parser rejects with "syntax
    // error" in the message.
    match session.full_text_search(
        "unbalanced\"",
        &crate::SearchScope::Vault,
        &CancelToken::new(),
    ) {
        Err(VaultError::InvalidQuery { message }) => {
            assert!(
                message.contains("unbalanced"),
                "InvalidQuery message should include the query: {message}"
            );
        }
        other => panic!("expected InvalidQuery, got {other:?}"),
    }
}

#[test]
fn slow_path_insert_populates_fts() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/alpha.md", b"hello world uniquetokenalpha")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(fts_match_count(&session, "uniquetokenalpha"), 1);
    assert_eq!(fts_match_count(&session, "hello"), 1);
    assert_eq!(fts_match_count(&session, "totallyabsenttokenxyz"), 0);
}

#[test]
fn slow_path_update_replaces_fts_tokens() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/n.md", b"oldmarkertext").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(fts_match_count(&session, "oldmarkertext"), 1);

    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider.write_file("notes/n.md", b"newmarkertext").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    assert_eq!(
        fts_match_count(&session, "oldmarkertext"),
        0,
        "stale token survived a content change"
    );
    assert_eq!(fts_match_count(&session, "newmarkertext"), 1);
}

#[test]
fn delete_from_files_removes_fts_row() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/drop.md", b"droppablecontenttoken")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(fts_match_count(&session, "droppablecontenttoken"), 1);

    // Simulate the future stale-row sweep by deleting directly.
    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "DELETE FROM files WHERE path = ?1",
            rusqlite::params!["notes/drop.md"],
        )
        .unwrap();
    }
    assert_eq!(fts_match_count(&session, "droppablecontenttoken"), 0);
}

#[test]
fn fast_path_does_not_touch_fts_index() {
    // Drives the AFTER UPDATE OF body_text trigger discipline:
    // a rescan with no on-disk changes must skip the body decode
    // AND the FTS rewrite. The check is functional rather than
    // structural — we assert the token is searchable before AND
    // after, and that no duplicate result rows show up (an
    // over-eager trigger would re-insert the same body and
    // produce two MATCHing rows for one file).
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/stable.md", b"stablecontentmarker")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = fts_match_count(&session, "stablecontentmarker");
    assert_eq!(before, 1);

    session.scan_initial(&CancelToken::new()).unwrap();
    let after = fts_match_count(&session, "stablecontentmarker");
    assert_eq!(after, 1, "fast path duplicated the FTS row");
    assert_eq!(after, before);
}

#[test]
fn non_markdown_files_have_empty_fts_body() {
    // We deliberately skip body decode for non-markdown files —
    // they shouldn't appear in keyword searches over text.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/note.md", b"searchableprose").unwrap();
        p.write_file("notes/img.png", b"binarynotsearchable")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(fts_match_count(&session, "searchableprose"), 1);
    assert_eq!(fts_match_count(&session, "binarynotsearchable"), 0);
}

#[test]
fn fts_indexes_only_markdown_bodies() {
    // Codoki PR-84 invariant rendered functionally: distinct
    // tokens in markdown files match, distinct tokens in
    // non-markdown files do not. (We can't use
    // `COUNT(*) FROM files_fts` because external-content FTS5
    // returns content-table rows from a bare SELECT, not the
    // indexed-row count.)
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/a.md", b"alphacontent").unwrap();
        p.write_file("notes/b.md", b"betacontent").unwrap();
        p.write_file("notes/c.png", b"binarygammacontent").unwrap();
        p.write_file("notes/d.txt", b"plaindeltacontent").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(fts_match_count(&session, "alphacontent"), 1);
    assert_eq!(fts_match_count(&session, "betacontent"), 1);
    assert_eq!(fts_match_count(&session, "binarygammacontent"), 0);
    assert_eq!(fts_match_count(&session, "plaindeltacontent"), 0);
}

#[test]
fn flipping_is_markdown_to_zero_removes_fts_row() {
    // Exercises the AFTER UPDATE OF body_text trigger's
    // is_markdown-gated branches. Simulates an external rename
    // where notes/swap.md becomes a non-markdown file under the
    // same path: is_markdown flips 1 → 0, body_text empties,
    // and the FTS row must come out.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/swap.md", b"transitiontoken").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(fts_match_count(&session, "transitiontoken"), 1);

    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET is_markdown = 0, body_text = '' WHERE path = ?1",
            rusqlite::params!["notes/swap.md"],
        )
        .unwrap();
    }
    assert_eq!(
        fts_match_count(&session, "transitiontoken"),
        0,
        "FTS row should be removed when is_markdown flips to 0"
    );
}

#[test]
fn full_text_search_finds_plain_term() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/alpha.md", b"hello uniquetokenalpha world")
            .unwrap();
        p.write_file("notes/beta.md", b"unrelated content").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session
        .full_text_search(
            "uniquetokenalpha",
            &crate::SearchScope::Vault,
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(result.rows.len(), 1);
    assert_eq!(result.rows[0].path, "notes/alpha.md");
    assert!(result.rows[0].snippet.contains("uniquetokenalpha"));
    assert!(result.rows[0].snippet.contains(crate::SNIPPET_HIT_START));
    assert_eq!(result.summary, "Search returned 1 result.");
}

#[test]
fn full_text_search_finds_phrase_match() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/phrase.md",
            b"foreground task: write integration tests today",
        )
        .unwrap();
        p.write_file("notes/wrong-order.md", b"tests write today integration")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // FTS5 phrase queries use double quotes.
    let result = session
        .full_text_search(
            "\"write integration tests\"",
            &crate::SearchScope::Vault,
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(result.rows.len(), 1);
    assert_eq!(result.rows[0].path, "notes/phrase.md");
}

#[test]
fn full_text_search_scope_folder_filters_results() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/inside.md", b"sharedtoken in notes")
            .unwrap();
        p.write_file("archive/outside.md", b"sharedtoken in archive")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let vault_all = session
        .full_text_search(
            "sharedtoken",
            &crate::SearchScope::Vault,
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(vault_all.rows.len(), 2);

    let notes_only = session
        .full_text_search(
            "sharedtoken",
            &crate::SearchScope::Folder("notes".to_string()),
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(notes_only.rows.len(), 1);
    assert_eq!(notes_only.rows[0].path, "notes/inside.md");

    let archive_only = session
        .full_text_search(
            "sharedtoken",
            &crate::SearchScope::Folder("archive".to_string()),
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(archive_only.rows.len(), 1);
    assert_eq!(archive_only.rows[0].path, "archive/outside.md");
}

#[test]
fn full_text_search_folder_scope_escapes_like_meta_chars() {
    // Codoki PR-85 callout: a folder name with `_` must compare
    // literally, not as a LIKE wildcard. `sales_q1` and
    // `salesXq1` overlap under un-escaped LIKE matching; the
    // escape must keep them distinct.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("sales_q1/note.md", b"underscoretoken")
            .unwrap();
        p.write_file("salesXq1/note.md", b"underscoretoken")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session
        .full_text_search(
            "underscoretoken",
            &crate::SearchScope::Folder("sales_q1".to_string()),
            &CancelToken::new(),
        )
        .unwrap();
    let paths: Vec<&str> = result.rows.iter().map(|r| r.path.as_str()).collect();
    assert_eq!(paths, vec!["sales_q1/note.md"]);
}

#[test]
fn full_text_search_pre_cancelled_returns_immediately() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/anything.md", b"contenttoken").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let cancel = CancelToken::new();
    cancel.cancel();
    match session.full_text_search("contenttoken", &crate::SearchScope::Vault, &cancel) {
        Err(VaultError::Cancelled) => {}
        other => panic!("expected Cancelled, got {other:?}"),
    }
}

#[test]
fn full_text_search_reserved_scopes_return_unsupported() {
    // #93 item 2: File used to return `Cancelled` as a placeholder,
    // confusing retry-on-cancel callers. It now returns
    // `Unsupported`. Tag is no longer reserved (#508) — it has its
    // own live tests below.
    let (_tmp, session) = make_vault(|_| {});
    let cancel = CancelToken::new();
    match session.full_text_search(
        "anything",
        &crate::SearchScope::File("notes/x.md".to_string()),
        &cancel,
    ) {
        Err(VaultError::Unsupported { feature }) => {
            assert!(
                feature.contains("File"),
                "expected feature label to identify File, got {feature:?}"
            );
        }
        other => panic!("expected Unsupported for File scope, got {other:?}"),
    }
}

// --- SearchScope::Tag (#508) ---

fn tag_scope(tag: &str) -> crate::SearchScope {
    crate::SearchScope::Tag(tag.to_string())
}

#[test]
fn tag_scope_empty_query_lists_all_tagged_files_frontmatter_and_inline() {
    let (_tmp, session) = make_vault(|p| {
        // Frontmatter tag only.
        p.write_file("notes/fm.md", b"---\ntags: [project]\n---\nbody\n")
            .unwrap();
        // Inline tag only.
        p.write_file("notes/inline.md", b"see #project here\n")
            .unwrap();
        // Neither — must be excluded.
        p.write_file("notes/none.md", b"nothing tagged\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let result = session
        .full_text_search("", &tag_scope("project"), &CancelToken::new())
        .unwrap();
    let paths: Vec<&str> = result.rows.iter().map(|r| r.path.as_str()).collect();
    // Path order, both dimensions, none excluded.
    assert_eq!(paths, vec!["notes/fm.md", "notes/inline.md"]);
    // Empty-query tag rows carry no snippet and score 0.
    for row in &result.rows {
        assert_eq!(row.snippet, "");
        assert_eq!(row.score, 0.0);
    }
    assert_eq!(result.summary, "Search returned 2 results.");
}

#[test]
fn tag_scope_nonempty_query_intersects_fts_with_tag() {
    let (_tmp, session) = make_vault(|p| {
        // Has the tag AND the word.
        p.write_file("notes/both.md", b"#project sharedword here\n")
            .unwrap();
        // Has the tag but NOT the word.
        p.write_file("notes/tag_only.md", b"#project unrelated text\n")
            .unwrap();
        // Has the word but NOT the tag.
        p.write_file("notes/word_only.md", b"sharedword no tag\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let result = session
        .full_text_search("sharedword", &tag_scope("project"), &CancelToken::new())
        .unwrap();
    let paths: Vec<&str> = result.rows.iter().map(|r| r.path.as_str()).collect();
    assert_eq!(paths, vec!["notes/both.md"]);
    assert!(result.rows[0].snippet.contains("sharedword"));
}

#[test]
fn tag_scope_suppresses_tags_in_code_and_comments() {
    // The inline dimension reuses highlight_spans, so a `#tag` inside
    // a code fence / inline code / comment is NOT indexed.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/fenced.md", b"```\n#project inside code\n```\n")
            .unwrap();
        p.write_file("notes/comment.md", b"%% #project in a comment %%\n")
            .unwrap();
        p.write_file("notes/inlinecode.md", b"`#project` in code span\n")
            .unwrap();
        p.write_file("notes/real.md", b"a genuine #project tag\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let result = session
        .full_text_search("", &tag_scope("project"), &CancelToken::new())
        .unwrap();
    let paths: Vec<&str> = result.rows.iter().map(|r| r.path.as_str()).collect();
    assert_eq!(paths, vec!["notes/real.md"]);
}

#[test]
fn tag_scope_nested_child_matches_parent_but_not_sibling() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/nested.md", b"work #area/subarea done\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Parent scope matches the nested child.
    let parent = session
        .full_text_search("", &tag_scope("area"), &CancelToken::new())
        .unwrap();
    assert_eq!(
        parent
            .rows
            .iter()
            .map(|r| r.path.as_str())
            .collect::<Vec<_>>(),
        vec!["notes/nested.md"]
    );
    // Exact scope of the full nested tag also matches.
    let exact = session
        .full_text_search("", &tag_scope("area/subarea"), &CancelToken::new())
        .unwrap();
    assert_eq!(exact.rows.len(), 1);
    // A sibling / unrelated leaf does NOT match.
    let sibling = session
        .full_text_search("", &tag_scope("subarea"), &CancelToken::new())
        .unwrap();
    assert!(sibling.rows.is_empty());
}

#[test]
fn tag_scope_is_case_insensitive() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/cap.md", b"a #Project here\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session
        .full_text_search("", &tag_scope("project"), &CancelToken::new())
        .unwrap();
    assert_eq!(result.rows.len(), 1);
    assert_eq!(result.rows[0].path, "notes/cap.md");
}

#[test]
fn tag_scope_like_metachars_do_not_over_match() {
    // A tag containing `_` must compare literally in the prefix arm,
    // not as a LIKE wildcard: `a_b`'s nested-child pattern is
    // `a\_b/%`, so a file tagged `aXb` never matches scope `a_b`.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/underscore.md", b"tag #a_b/child here\n")
            .unwrap();
        p.write_file("notes/other.md", b"tag #aXb/child here\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session
        .full_text_search("", &tag_scope("a_b"), &CancelToken::new())
        .unwrap();
    let paths: Vec<&str> = result.rows.iter().map(|r| r.path.as_str()).collect();
    assert_eq!(paths, vec!["notes/underscore.md"]);
}

#[test]
fn tag_scope_dedups_inline_and_frontmatter_into_one_row() {
    // A file tagged both inline and in frontmatter with the same tag
    // appears once (file_tags stores the distinct set per file).
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "notes/dup.md",
            b"---\ntags: [project]\n---\nbody #project inline\n",
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session
        .full_text_search("", &tag_scope("project"), &CancelToken::new())
        .unwrap();
    assert_eq!(result.rows.len(), 1);
    assert_eq!(result.rows[0].path, "notes/dup.md");
}

#[test]
fn tag_scope_rows_clear_when_tag_removed() {
    // Update path: removing the tag from a file drops its file_tags
    // rows on the next slow-path scan.
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/n.md", b"has #project tag\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        session
            .full_text_search("", &tag_scope("project"), &CancelToken::new())
            .unwrap()
            .rows
            .len(),
        1
    );

    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/n.md", b"no more tag here\n")
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        session
            .full_text_search("", &tag_scope("project"), &CancelToken::new())
            .unwrap()
            .rows
            .is_empty(),
        "file_tags row survived tag removal"
    );
}

#[test]
fn tag_scope_empty_normalized_tag_returns_no_results() {
    // A scope tag that normalizes to empty (`#` alone) can't match.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/n.md", b"#project here\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session
        .full_text_search("", &tag_scope("#"), &CancelToken::new())
        .unwrap();
    assert!(result.rows.is_empty());
    assert_eq!(result.summary, "Search returned no results.");
}

#[test]
fn full_text_search_returns_the_matching_file_with_snippet() {
    // Previous shape asserted on `line_number`, but #92 item 1
    // moved line derivation out of full_text_search (it pulled
    // body_text per hit just to compute the line). The
    // user-visible contract for this test is still meaningful:
    // a vault containing one note with a rare token, queried
    // for that token, returns exactly that one hit with a
    // non-empty snippet.
    let (_tmp, session) = make_vault(|p| {
        let body = b"line one\nline two\nline three has thetargettoken in it\nline four";
        p.write_file("notes/multi.md", body).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session
        .full_text_search(
            "thetargettoken",
            &crate::SearchScope::Vault,
            &CancelToken::new(),
        )
        .unwrap();
    assert_eq!(result.rows.len(), 1);
    assert_eq!(result.rows[0].path, "notes/multi.md");
    assert!(!result.rows[0].snippet.is_empty());
}

#[test]
fn full_text_search_empty_query_returns_empty_set() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/a.md", b"content").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let result = session
        .full_text_search("   ", &crate::SearchScope::Vault, &CancelToken::new())
        .unwrap();
    assert!(result.rows.is_empty());
    assert_eq!(result.summary, "Search returned no results.");
}

#[test]
fn flipping_is_markdown_to_one_adds_fts_row() {
    // Inverse transition: a previously non-markdown file (no
    // FTS row) becomes markdown with body content. The trigger
    // must insert.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/binary.png", b"originalbinaryjunk")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // The non-markdown body never made it into FTS in the first
    // place — its tokens shouldn't match.
    assert_eq!(fts_match_count(&session, "originalbinaryjunk"), 0);

    {
        let conn = session.conn.lock().unwrap();
        conn.execute(
            "UPDATE files SET is_markdown = 1, body_text = 'newlysearchabletext' WHERE path = ?1",
            rusqlite::params!["notes/binary.png"],
        )
        .unwrap();
    }
    assert_eq!(fts_match_count(&session, "newlysearchabletext"), 1);
}
