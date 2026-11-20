// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! `VaultSession` tests — full_text_search and FTS index lifecycle.
//!
//! Extracted from `crates/slate-core/src/session.rs` as part of #272.

#![allow(clippy::too_many_lines)]

use super::common::*;
use super::*;

/// Core's sentence for a search its full-text index refuses as a query
/// (the owner, 2026-11-20: "Plain words. Please change it."). The facts
/// below hold core to this literal; `a11y::SEARCH_QUERY_UNREADABLE` is
/// pinned to it in a11y's own tests.
const REFUSED: &str = "Check the quotes, symbols and operators in your search.";

/// The message of the `InvalidQuery` a search is refused with, or a panic
/// that names the query, the scope and what came back instead.
fn refusal_message(session: &VaultSession, query: &str, scope: &crate::SearchScope) -> String {
    match session.full_text_search(query, scope, &CancelToken::new()) {
        Err(VaultError::InvalidQuery { message }) => message,
        other => panic!("{query:?} at {scope:?}: expected InvalidQuery, got {other:?}"),
    }
}

/// A vault whose one note sits under `notes/` and carries `#project`, so
/// the Vault, Folder and Tag scopes all reach the full-text index.
fn refusal_vault() -> (tempfile::TempDir, VaultSession) {
    let (tmp, session) = make_vault(|p| {
        p.write_file("notes/a.md", b"alpha beta gamma #project")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

fn every_search_scope() -> [crate::SearchScope; 3] {
    [
        crate::SearchScope::Vault,
        crate::SearchScope::Folder("notes".into()),
        crate::SearchScope::Tag("project".into()),
    ]
}

#[test]
fn full_text_search_maps_fts5_syntax_errors_to_invalid_query() {
    // Audit-#88-B3 regression: previously a user-supplied FTS5
    // syntax error surfaced as `VaultError::Db`, which is
    // indistinguishable from a corrupt cache.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("notes/a.md", b"some content").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    // Unbalanced quote: FTS5's parser refuses it ("unterminated
    // string"), and the reader hears core's sentence, not SQLite's text.
    assert_eq!(
        refusal_message(&session, "unbalanced\"", &crate::SearchScope::Vault),
        REFUSED
    );
}

/// Every kind of query plan 47's step 0 saw FTS5 refuse as a query says
/// core's one plain sentence, at every scope: SQLite's own refusal text
/// (and the query it quoted) never reaches the reader.
#[test]
fn every_query_fts5_refuses_says_cores_sentence() {
    let (_tmp, session) = refusal_vault();
    // FTS5's parser stack holds fewer than 100 nested groups, and its
    // expression tree is at most 256 deep (step 0: 50 and 200 run).
    let nested = format!("{}alpha{}", "(".repeat(100), ")".repeat(100));
    let chain = format!("alpha{}", " NOT beta".repeat(300));
    let queries: Vec<&str> = vec![
        // An operator with nothing to join, an open NEAR, a NEAR distance
        // below zero.
        "AND",
        "foo AND",
        "NOT foo",
        "foo AND OR bar",
        "NEAR(alpha beta",
        "NEAR(alpha beta, -1)",
        // A quote left open, at either end.
        "unbalanced\"",
        "\"unbalanced",
        // A special query and the other operator symbols.
        "*",
        "^",
        "+",
        "(foo",
        "foo)",
        // Ordinary text with one symbol in it.
        "#project",
        "don't",
        "e.g.",
        "what?",
        "foo, bar",
        "[[alpha]]",
        "C++",
        "**bold**",
        "email@example.com",
        // Past the parser's depth, and past the expression tree's.
        &nested,
        &chain,
    ];
    for scope in every_search_scope() {
        for query in &queries {
            assert_eq!(
                refusal_message(&session, query, &scope),
                REFUSED,
                "{query:?} at {scope:?}"
            );
        }
    }
}

/// A colon, a hyphen, a date, a link or a NEAR distance that is not a
/// number: FTS5 refuses each as a query ("no such column: known",
/// "expected integer, got …"), so each says core's sentence, not a
/// database error.
#[test]
fn a_column_filter_hyphen_date_link_or_near_distance_is_refused_as_a_query() {
    let (_tmp, session) = refusal_vault();
    for scope in every_search_scope() {
        for query in [
            "foo:bar",
            "title:foo",
            "-x",
            "TODO:",
            "well-known",
            "2024-01-01",
            "https://example.com",
            "NEAR(alpha, beta)",
            "NEAR(alpha beta, x)",
        ] {
            assert_eq!(
                refusal_message(&session, query, &scope),
                REFUSED,
                "{query:?} at {scope:?}"
            );
        }
    }
}

/// A damaged full-text index is a database error, never a refused query:
/// FTS5's corruption texts begin "fts5:" as some of its parse failures do,
/// and a reader told to check a search that is fine would edit it forever.
#[test]
fn a_damaged_full_text_index_is_a_database_error_not_a_refused_query() {
    let (_tmp, session) = refusal_vault();
    assert_eq!(fts_match_count(&session, "alpha"), 1, "premise: indexed");
    {
        let conn = session.conn.lock().unwrap();
        // FTS5 keeps its averages at id 1 and its structure record at id
        // 10 (FTS5_AVERAGES_ROWID, FTS5_STRUCTURE_ROWID); the leaves have
        // larger ids.
        let zeroed = conn
            .execute(
                "UPDATE files_fts_data SET block = X'0000000000000000' WHERE id > 10",
                [],
            )
            .unwrap();
        assert!(zeroed > 0, "premise: the index has leaves to damage");
    }
    for scope in every_search_scope() {
        match session.full_text_search("alpha", &scope, &CancelToken::new()) {
            Err(error @ VaultError::Db(_)) => {
                let detail = crate::a11y::vault_error_detail(&error);
                assert!(detail.contains("corruption"), "{scope:?}: {detail}");
            }
            other => panic!("{scope:?}: a damaged index must be Db, got {other:?}"),
        }
    }
}

/// A damaged database schema is a database error, never a refused query,
/// though SQLite's message for it says "syntax error": the cached search
/// statement is re-prepared inside the step after the schema changes, and
/// the damaged `sqlite_schema` row fails that with SQLITE_CORRUPT
/// ("malformed database schema (extra) - near ",": syntax error").
#[test]
fn a_damaged_database_schema_is_a_database_error_not_a_refused_query() {
    let (_tmp, session) = refusal_vault();
    for scope in every_search_scope() {
        assert!(
            session
                .full_text_search("alpha", &scope, &CancelToken::new())
                .is_ok(),
            "premise: {scope:?} runs and its statement is cached"
        );
    }
    {
        let conn = session.conn.lock().unwrap();
        conn.execute_batch("CREATE TABLE extra(x)").unwrap();
        let version: i64 = conn
            .query_row("PRAGMA schema_version", [], |row| row.get(0))
            .unwrap();
        conn.execute_batch(&format!(
            "PRAGMA writable_schema=ON;
             UPDATE sqlite_schema SET sql = 'CREATE TABLE extra(x,,y)' WHERE name = 'extra';
             PRAGMA schema_version={};
             PRAGMA writable_schema=OFF;",
            version + 1
        ))
        .unwrap();
    }
    for scope in every_search_scope() {
        match session.full_text_search("alpha", &scope, &CancelToken::new()) {
            Err(error @ VaultError::Db(_)) => {
                let detail = crate::a11y::vault_error_detail(&error);
                assert!(detail.contains("malformed"), "{scope:?}: {detail}");
            }
            other => panic!("{scope:?}: a damaged schema must be Db, got {other:?}"),
        }
    }
}

/// FTS5 runs these queries, which step 0 saw it read: an operator used as
/// FTS5 means it, a prefix, a phrase, a word, the parser's and the
/// expression tree's depth within their limits. A refused text runs once
/// its words are in double quotes.
#[test]
fn every_query_fts5_reads_still_runs() {
    let (_tmp, session) = refusal_vault();
    let nested = format!("{}alpha{}", "(".repeat(50), ")".repeat(50));
    let chain = format!("alpha{}", " NOT beta".repeat(200));
    for query in [
        "NEAR",
        "NEAR(alpha beta, 99999999999)",
        "foo*",
        "^alpha",
        "foo + bar",
        "foo_bar",
        "caf\u{e9}",
        "\u{201c}curly\u{201d}",
        "\"alpha beta\"",
        // A refused text with its words in double quotes runs.
        "\"#project\"",
        "\"don't\"",
        "\"well-known\"",
        "\"https://example.com\"",
        "alpha AND beta",
        "alpha OR beta",
        "alpha NOT beta",
        "and",
        "near",
        "or alpha",
        &nested,
        &chain,
    ] {
        for scope in every_search_scope() {
            assert!(
                session
                    .full_text_search(query, &scope, &CancelToken::new())
                    .is_ok(),
                "{query:?} at {scope:?}"
            );
        }
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

    // A DIFFERENT-length rewrite: the scanner's fast path compares
    // (mtime, size, ctime), Windows reports no portable ctime, and a
    // same-length rewrite landing in the same coarse-mtime granule
    // matched the tuple and skipped the re-read — a rare CI-only
    // flake in this test (observed 2026-08-02; the W4-3 rounds 13-15
    // coarse-mtime analysis explains it). Changing the size keeps
    // the test about FTS replacement, not stat granularity.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("notes/n.md", b"newmarkertext now longer")
        .unwrap();
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
    // The new body has the ORIGINAL's byte length, so the re-scan's fast
    // path (mtime_ms and size unchanged → skip) would keep the stale
    // file_tags rows if the rewrite landed inside the first write's
    // file-time tick (Windows file times move with the ~16 ms system
    // tick; PR #1180's run 33805992482 hit it). Rewrite until the mtime
    // advances — the remedy the other slow-path tests use.
    let original = provider.stat("notes/n.md").unwrap().mtime_ms;
    rewrite_until_mtime_advances(&provider, "notes/n.md", b"no more tag here\n", original);
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
