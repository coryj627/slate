// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! FL4-1 (#662): execution semantics of the sidebar filter against a
//! real scanned vault — every term class, combinations, negation,
//! ordering, pagination, scoped listing, and hostile inputs.

use proptest::prelude::*;
use slate_core::{Paging, SidebarFilterDateWindow, VaultError, VaultSession};

fn write(vault: &std::path::Path, rel: &str, content: &str) {
    let path = vault.join(rel);
    std::fs::create_dir_all(path.parent().unwrap()).unwrap();
    std::fs::write(path, content).unwrap();
}

fn set_mtime(vault: &std::path::Path, rel: &str, ms: i64) {
    let file = std::fs::File::options()
        .write(true)
        .open(vault.join(rel))
        .unwrap();
    file.set_modified(
        std::time::SystemTime::UNIX_EPOCH + std::time::Duration::from_millis(ms as u64),
    )
    .unwrap();
}

/// A fixture with names, titles, tags, tasks, extensions, folders, and
/// controlled mtimes (all instants far apart, expressed in UTC ms).
fn fixture() -> (tempfile::TempDir, VaultSession) {
    let tmp = tempfile::tempdir().unwrap();
    let vault = tmp.path();
    write(
        vault,
        "research/alpha.md",
        "---\ntitle: Zebra Study\n---\n#project/deep\n- [ ] open item\n",
    );
    write(vault, "research/beta.md", "#project\nplain body\n");
    write(vault, "notes/Café Plan.md", "#other\n- [x] done item\n");
    write(vault, "notes/gamma.md", "gamma body café mention\n");
    write(vault, "assets/scan.pdf", "%PDF-1.4 fake\n");
    write(vault, "top.md", "top-level note\n");
    set_mtime(vault, "research/alpha.md", 1_000_000);
    set_mtime(vault, "research/beta.md", 2_000_000);
    set_mtime(vault, "notes/Café Plan.md", 3_000_000);
    set_mtime(vault, "notes/gamma.md", 4_000_000);
    set_mtime(vault, "assets/scan.pdf", 5_000_000);
    set_mtime(vault, "top.md", 6_000_000);
    let session = VaultSession::from_filesystem(vault.to_path_buf()).unwrap();
    session
        .scan_initial(&slate_core::CancelToken::new())
        .unwrap();
    (tmp, session)
}

fn paths(session: &VaultSession, query: &str) -> Vec<String> {
    paths_windowed(session, query, &[])
}

fn paths_windowed(
    session: &VaultSession,
    query: &str,
    windows: &[SidebarFilterDateWindow],
) -> Vec<String> {
    session
        .filter_files(query, None, windows, Paging::first(100))
        .unwrap()
        .files
        .into_iter()
        .map(|summary| summary.path)
        .collect()
}

#[test]
fn every_term_class_filters_and_orders_deterministically() {
    let (_tmp, session) = fixture();

    // Name term matches the EFFECTIVE name: alpha.md's title is
    // "Zebra Study", so "zebra" finds it and "alpha" does not.
    assert_eq!(paths(&session, "zebra"), ["research/alpha.md"]);
    assert_eq!(paths(&session, "alpha"), Vec::<String>::new());
    // Case-insensitive via the casefold convention.
    assert_eq!(paths(&session, "CAFÉ"), ["notes/Café Plan.md"]);

    // Tag term includes nested children (#project ⊇ project/deep).
    assert_eq!(
        paths(&session, "#project"),
        ["research/beta.md", "research/alpha.md"],
        "effective-name order: beta (stem) before alpha (title 'Zebra Study')"
    );
    assert_eq!(paths(&session, "#project/deep"), ["research/alpha.md"]);

    // has:task = at least one OPEN task (the done item doesn't count).
    assert_eq!(paths(&session, "has:task"), ["research/alpha.md"]);

    // ext is case-insensitive.
    assert_eq!(paths(&session, "ext:PDF"), ["assets/scan.pdf"]);

    // path: prefix scopes to the folder.
    assert_eq!(
        paths(&session, "path:notes/"),
        ["notes/Café Plan.md", "notes/gamma.md"]
    );

    // Combination ANDs; negation subtracts.
    assert_eq!(
        paths(&session, "#project -#project/deep"),
        ["research/beta.md"]
    );
    assert_eq!(
        paths(&session, "path:research/ has:task"),
        ["research/alpha.md"]
    );

    // Ordering is effective-name casefold asc (Café Plan < gamma <
    // scan < top < beta's "beta" … full-vault scoped listing shows the
    // total order; Zebra Study sorts under 'z').
    let all = session
        .filter_files("", Some("research"), &[], Paging::first(10))
        .unwrap();
    assert_eq!(
        all.files
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        ["research/beta.md", "research/alpha.md"],
        "beta (stem) precedes alpha (title 'Zebra Study') in name order"
    );
}

#[test]
fn date_windows_are_exact_half_open_and_negatable() {
    let (_tmp, session) = fixture();
    let window = |s: i64, e: i64| SidebarFilterDateWindow {
        term: "@2026-01-01".into(),
        start_ms: s,
        end_ms: e,
    };
    // [2_000_000, 4_000_000) captures beta (2M) and Café Plan (3M) —
    // exact half-open: gamma at exactly 4M is excluded.
    assert_eq!(
        paths_windowed(&session, "@2026-01-01", &[window(2_000_000, 4_000_000)]),
        ["research/beta.md", "notes/Café Plan.md"],
        "half-open: gamma at exactly end_ms is excluded; name order beta < café"
    );
    assert_eq!(
        paths_windowed(&session, "-@2026-01-01", &[window(2_000_000, 4_000_000)]),
        [
            "notes/gamma.md",
            "assets/scan.pdf",
            "top.md",
            "research/alpha.md"
        ]
    );

    // DST-shaped windows pass through exactly (America/New_York
    // 2026-03-08 spring-forward: local day = 23 hours). Boundaries are
    // host-computed; core must treat them as opaque exact instants.
    // 2026-03-08T00:00:00-05:00 = 1772946000000; next local midnight
    // 2026-03-09T00:00:00-04:00 = 1773028800000 (82,800,000 ms = 23 h).
    let spring = SidebarFilterDateWindow {
        term: "@2026-03-08".into(),
        start_ms: 1_772_946_000_000,
        end_ms: 1_773_028_800_000,
    };
    assert_eq!(spring.end_ms - spring.start_ms, 23 * 3_600_000);
    assert_eq!(
        paths_windowed(&session, "@2026-03-08", &[spring]),
        Vec::<String>::new(),
        "no fixture file lives in that window; the window itself is honored"
    );
    // Fall-back day (2026-11-01, 25 hours): 2026-11-01T00:00:00-04:00 =
    // 1793505600000 → 2026-11-02T00:00:00-05:00 = 1793595600000.
    let fall = SidebarFilterDateWindow {
        term: "@2026-11-01".into(),
        start_ms: 1_793_505_600_000,
        end_ms: 1_793_595_600_000,
    };
    assert_eq!(fall.end_ms - fall.start_ms, 25 * 3_600_000);
    assert!(
        slate_core::validate_date_windows(
            &slate_core::parse_sidebar_filter("@2026-11-01").unwrap(),
            &[fall]
        )
        .is_ok()
    );
}

#[test]
fn window_pairing_is_validated_before_sql() {
    let (_tmp, session) = fixture();
    let err = session
        .filter_files("@today", None, &[], Paging::first(10))
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidQuery { .. }));

    let err = session
        .filter_files(
            "zebra",
            None,
            &[SidebarFilterDateWindow {
                term: "@today".into(),
                start_ms: 0,
                end_ms: 1,
            }],
            Paging::first(10),
        )
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidQuery { .. }));
}

#[test]
fn term_order_permutations_are_identical() {
    let (_tmp, session) = fixture();
    let terms = ["#project", "path:research/", "-has:task"];
    let mut expected: Option<Vec<String>> = None;
    // All 6 permutations of three terms.
    for a in 0..3 {
        for b in 0..3 {
            if b == a {
                continue;
            }
            let c = 3 - a - b;
            let query = format!("{} {} {}", terms[a], terms[b], terms[c]);
            let result = paths(&session, &query);
            match &expected {
                None => expected = Some(result),
                Some(expected) => assert_eq!(&result, expected, "{query}"),
            }
        }
    }
    assert_eq!(expected.unwrap(), ["research/beta.md"]);
}

#[test]
fn hostile_inputs_bind_as_parameters_never_sql() {
    let (_tmp, session) = fixture();
    for hostile in [
        "'; DROP TABLE files;--",
        "%",
        "_",
        "\\",
        "ext:pdf'--",
        "#a'or'1'='1",
    ] {
        // Whatever the term parses as, execution must not error and the
        // index must survive.
        let _ = session.filter_files(hostile, None, &[], Paging::first(10));
    }
    assert_eq!(paths(&session, "zebra"), ["research/alpha.md"]);
    // LIKE metacharacters in path terms are literal.
    assert_eq!(paths(&session, "path:res%arch/"), Vec::<String>::new());
}

#[test]
fn scoped_listing_contract() {
    let (_tmp, session) = fixture();
    // Unscoped empty query is invalid.
    assert!(matches!(
        session.filter_files("", None, &[], Paging::first(10)),
        Err(VaultError::InvalidQuery { .. })
    ));
    // Scoped empty query lists deterministically with the scoped summary.
    let page = session
        .filter_files("", Some("notes/"), &[], Paging::first(10))
        .unwrap();
    assert_eq!(page.total, 2);
    assert_eq!(page.audio_summary, "2 results in notes.");
    // Traversal/escape scopes fail before SQL.
    for scope in ["../up", "/abs", "a//b", "a/../b", ""] {
        assert!(matches!(
            session.filter_files("", Some(scope), &[], Paging::first(10)),
            Err(VaultError::InvalidQuery { .. })
        ));
    }
    // Scope composes with a query as an implicit path term.
    assert_eq!(
        session
            .filter_files("gamma", Some("notes"), &[], Paging::first(10))
            .unwrap()
            .files
            .len(),
        1
    );
}

#[test]
fn pagination_is_stable_across_the_total_order() {
    let (_tmp, session) = fixture();
    let mut collected = Vec::new();
    // "." is rejected as a scope — walk two real scopes instead.
    assert!(
        session
            .filter_files("", Some("."), &[], Paging::first(2))
            .is_err()
    );
    // Page through `research` + `notes` (2 each at limit 1).
    for scope in ["research", "notes"] {
        let mut cursor: Option<String> = None;
        let mut pages = 0;
        loop {
            let page = session
                .filter_files(
                    "",
                    Some(scope),
                    &[],
                    Paging {
                        cursor: cursor.clone(),
                        limit: 1,
                    },
                )
                .unwrap();
            assert_eq!(page.total, 2, "{scope}");
            for file in &page.files {
                collected.push(file.path.clone());
            }
            pages += 1;
            match page.next_cursor {
                Some(next) => cursor = Some(next),
                None => break,
            }
        }
        assert_eq!(pages, 2, "{scope}: one row per page, exact");
    }
    assert_eq!(
        collected,
        [
            "research/beta.md",
            "research/alpha.md",
            "notes/Café Plan.md",
            "notes/gamma.md"
        ]
    );
}

#[test]
fn audio_summary_is_normative() {
    let (_tmp, session) = fixture();
    assert_eq!(
        session
            .filter_files("zebra", None, &[], Paging::first(10))
            .unwrap()
            .audio_summary,
        "1 results."
    );
    assert_eq!(
        session
            .filter_files("nosuchword", None, &[], Paging::first(10))
            .unwrap()
            .audio_summary,
        "No results."
    );
}

#[test]
fn scope_and_path_bounds_are_binary_not_like_folded() {
    // Review round: LIKE is ASCII-case-insensitive on this connection —
    // scoping must use binary subtree bounds so a case-distinct sibling
    // directory can never leak into a scope.
    let tmp = tempfile::tempdir().unwrap();
    write(tmp.path(), "Research/Case.md", "cased\n");
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session
        .scan_initial(&slate_core::CancelToken::new())
        .unwrap();
    let lower = session
        .filter_files("", Some("research"), &[], Paging::first(10))
        .unwrap();
    assert_eq!(lower.total, 0, "lowercase scope must not match 'Research/'");
    let exact = session
        .filter_files("", Some("Research"), &[], Paging::first(10))
        .unwrap();
    assert_eq!(exact.total, 1);
    assert_eq!(
        session
            .filter_files("path:research/", None, &[], Paging::first(10))
            .unwrap()
            .total,
        0,
        "path: terms use the same binary bounds"
    );
}

#[test]
fn title_effective_name_matches_the_shared_decoder_rules() {
    // Review round: blank text titles and non-text titles fall back to
    // the stem for BOTH matching and ordering, exactly like
    // FileSummary.display_name.
    let tmp = tempfile::tempdir().unwrap();
    write(
        tmp.path(),
        "blankish.md",
        "---\ntitle: \"   \"\n---\nbody\n",
    );
    write(tmp.path(), "numeric.md", "---\ntitle: 42\n---\nbody\n");
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session
        .scan_initial(&slate_core::CancelToken::new())
        .unwrap();
    let by_stem = session
        .filter_files("blankish", None, &[], Paging::first(10))
        .unwrap();
    assert_eq!(by_stem.total, 1, "a blank title falls back to the stem");
    assert_eq!(
        session
            .filter_files("numeric", None, &[], Paging::first(10))
            .unwrap()
            .total,
        1,
        "a non-text title falls back to the stem"
    );
    // Ordering uses the stem too: blankish < numeric alphabetically.
    let page = session
        .filter_files("ext:md", None, &[], Paging::first(10))
        .unwrap();
    assert_eq!(
        page.files
            .iter()
            .map(|f| f.path.as_str())
            .collect::<Vec<_>>(),
        ["blankish.md", "numeric.md"]
    );
}

#[test]
fn cursors_are_length_prefixed_and_malformed_ones_are_rejected() {
    let (_tmp, session) = fixture();
    // Round-trip: page one, then resume — no repeats, no skips, even
    // though sort keys and paths are arbitrary user-authored text.
    let first = session
        .filter_files("", Some("notes"), &[], Paging::first(1))
        .unwrap();
    let cursor = first.next_cursor.clone().expect("more pages");
    let second = session
        .filter_files(
            "",
            Some("notes"),
            &[],
            Paging {
                cursor: Some(cursor),
                limit: 1,
            },
        )
        .unwrap();
    let mut both: Vec<String> = first
        .files
        .iter()
        .chain(second.files.iter())
        .map(|f| f.path.clone())
        .collect();
    both.dedup();
    assert_eq!(both.len(), 2, "no repeated or skipped rows across pages");
    // Malformed cursors are loud errors, never a silent restart.
    for bad in ["", "short", "zzzzzzzzrest", "ffffffff"] {
        assert!(
            matches!(
                session.filter_files(
                    "",
                    Some("notes"),
                    &[],
                    Paging {
                        cursor: Some(bad.to_string()),
                        limit: 1,
                    },
                ),
                Err(VaultError::InvalidQuery { .. })
            ),
            "{bad:?}"
        );
    }
}

proptest! {
    #![proptest_config(ProptestConfig::with_cases(24))]
    /// Positive-term monotonicity: filter(q) ⊆ filter(q minus one term).
    #[test]
    fn dropping_a_positive_term_only_widens(
        selector in proptest::collection::vec(0usize..5, 2..4),
        dropped in 0usize..3,
    ) {
        let pool = ["zebra", "#project", "has:task", "ext:md", "path:research/"];
        let (_tmp, session) = fixture();
        let terms: Vec<&str> = selector.iter().map(|&i| pool[i]).collect();
        let full = paths(&session, &terms.join(" "));
        let dropped_index = dropped % terms.len();
        let reduced: Vec<&str> = terms
            .iter()
            .enumerate()
            .filter(|(index, _)| *index != dropped_index)
            .map(|(_, term)| *term)
            .collect();
        if reduced.is_empty() {
            return Ok(());
        }
        let wider = paths(&session, &reduced.join(" "));
        for path in &full {
            prop_assert!(wider.contains(path), "{path} missing after drop");
        }
    }
}
