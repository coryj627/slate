// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Session-level integration tests for Milestone L (`#278`).
//!
//! Covers the full citations pipeline through `VaultSession`:
//! `from_filesystem` reads `.slate/prefs.json`,
//! `set_bibliography_sources` loads + indexes the entries,
//! the vault scan writes per-file citation rows, and the public
//! query + render methods round-trip cleanly.

use super::common::make_vault;
use super::*;
use std::fs;

fn bib_fixture() -> &'static str {
    r#"
@article{smith2020,
  title = {On the Nature of Reading},
  author = {Smith, Alice},
  journal = {Journal of Knowledge},
  year = {2020},
  doi = {10.1234/abc},
}

@book{jones2019,
  title = {A Survey of Surveys},
  author = {Jones, Robert and Lee, Hana},
  year = {2019},
  publisher = {Academic Press},
}
"#
}

#[test]
fn from_filesystem_reads_prefs_json_when_present() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    fs::create_dir_all(root.join(".slate")).unwrap();
    fs::write(
        root.join(".slate").join("prefs.json"),
        r#"{
          "bibliography": {
            "sources": [{ "path": "library.bib", "format": "BibTeX", "watch": false }],
            "default_style": "styles/apa.csl",
            "additional_styles": []
          }
        }"#,
    )
    .unwrap();

    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    let prefs = &session.config().citations_prefs;
    assert_eq!(prefs.sources.len(), 1);
    assert_eq!(prefs.sources[0].path, "library.bib");
    assert_eq!(prefs.default_style.as_deref(), Some("styles/apa.csl"));
}

#[test]
fn from_filesystem_reads_root_slate_json_when_no_prefs_json() {
    // #411: the demo-vault contract -- config shipped at the vault
    // root as `slate.json`, no `.slate/prefs.json` at all.
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    fs::write(
        root.join("slate.json"),
        r#"{
          "citations": {
            "bibliography": "library.bib",
            "cite_style": "ieee",
            "available_styles": ["ieee", "chicago-author-date", "apa"],
            "csl_directory": "csl"
          }
        }"#,
    )
    .unwrap();
    fs::write(root.join("library.bib"), bib_fixture()).unwrap();

    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    let prefs = &session.config().citations_prefs;
    assert_eq!(prefs.sources.len(), 1);
    assert_eq!(prefs.sources[0].path, "library.bib");
    assert_eq!(prefs.default_style.as_deref(), Some("csl/ieee.csl"));
    assert_eq!(prefs.additional_styles.len(), 2);

    // The whole point of #411: the bibliography actually loads.
    session
        .set_bibliography_sources(prefs.sources.clone())
        .unwrap();
    let entries = session.get_bibliography_entries().unwrap();
    assert_eq!(entries.len(), 2, "library.bib entries must be indexed");
}

#[test]
fn from_filesystem_returns_default_prefs_when_no_prefs_file() {
    let (_tmp, session) = make_vault(|_p| {});
    assert!(session.config().citations_prefs.sources.is_empty());
}

#[test]
fn from_filesystem_propagates_prefs_unreadable_for_bad_json() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    fs::create_dir_all(root.join(".slate")).unwrap();
    fs::write(root.join(".slate").join("prefs.json"), "{not valid json").unwrap();
    let err = match VaultSession::from_filesystem(root.to_path_buf()) {
        Ok(_) => panic!("expected PrefsUnreadable, got Ok"),
        Err(e) => e,
    };
    match err {
        VaultError::PrefsUnreadable { .. } => {}
        other => panic!("expected PrefsUnreadable, got {other:?}"),
    }
}

#[test]
fn set_bibliography_sources_loads_and_renders_citations() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    fs::write(root.join("library.bib"), bib_fixture()).unwrap();
    fs::write(
        root.join("paper.md"),
        "See [@smith2020, p. 23] and [-@jones2019].\n",
    )
    .unwrap();

    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    session
        .scan_initial(&crate::CancelToken::new())
        .expect("scan succeeds");

    // Load the bibliography.
    let warnings = session
        .set_bibliography_sources(vec![crate::BibliographySource {
            path: "library.bib".to_string(),
            format: crate::BibFormat::BibTeX,
            watch: false,
        }])
        .expect("set_bibliography_sources succeeds");
    assert!(warnings.is_empty(), "warnings: {warnings:?}");

    // `get_bibliography_entries` returns both entries.
    let entries = session.get_bibliography_entries().unwrap();
    let keys: Vec<&str> = entries.iter().map(|e| e.key.as_str()).collect();
    assert!(keys.contains(&"smith2020"));
    assert!(keys.contains(&"jones2019"));

    // `list_citations_in_file` returns two sites for paper.md.
    let refs = session.list_citations_in_file("paper.md").unwrap();
    assert_eq!(refs.len(), 2, "expected 2 citation sites, got {refs:?}");

    // `list_files_citing` finds paper.md for both keys.
    let citing_smith = session.list_files_citing("smith2020").unwrap();
    assert_eq!(citing_smith.len(), 1);
    assert_eq!(citing_smith[0].path, "paper.md");

    // `list_unresolved_citations` is empty when every key resolves.
    let unresolved = session.list_unresolved_citations().unwrap();
    assert!(
        unresolved.is_empty(),
        "expected no unresolved citations, got {unresolved:?}"
    );
}

#[test]
fn list_unresolved_citations_returns_missing_keys() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    fs::write(root.join("library.bib"), bib_fixture()).unwrap();
    fs::write(
        root.join("paper.md"),
        "Cites [@smith2020] and [@missing].\n",
    )
    .unwrap();
    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    session.scan_initial(&crate::CancelToken::new()).unwrap();
    session
        .set_bibliography_sources(vec![crate::BibliographySource {
            path: "library.bib".to_string(),
            format: crate::BibFormat::BibTeX,
            watch: false,
        }])
        .unwrap();
    let unresolved = session.list_unresolved_citations().unwrap();
    assert_eq!(unresolved.len(), 1);
    assert_eq!(unresolved[0].0, "paper.md");
    assert_eq!(unresolved[0].1, "missing");
}

#[test]
fn search_bibliography_matches_title_and_author() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    fs::write(root.join("library.bib"), bib_fixture()).unwrap();
    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    session
        .set_bibliography_sources(vec![crate::BibliographySource {
            path: "library.bib".to_string(),
            format: crate::BibFormat::BibTeX,
            watch: false,
        }])
        .unwrap();
    let title_hits = session.search_bibliography("Reading").unwrap();
    assert_eq!(title_hits.len(), 1);
    assert_eq!(title_hits[0].key, "smith2020");

    let author_hits = session.search_bibliography("Jones").unwrap();
    assert_eq!(author_hits.len(), 1);
    assert_eq!(author_hits[0].key, "jones2019");
}

#[test]
fn missing_bibliography_source_returns_bibsource_unreadable() {
    let (_tmp, session) = make_vault(|_p| {});
    let err = session
        .set_bibliography_sources(vec![crate::BibliographySource {
            path: "no-such.bib".to_string(),
            format: crate::BibFormat::BibTeX,
            watch: false,
        }])
        .unwrap_err();
    match err {
        VaultError::BibSourceUnreadable { .. } => {}
        other => panic!("expected BibSourceUnreadable, got {other:?}"),
    }
}

/// A seed that FAILS must not leave the previous session's entries
/// resolving as if they were current (#1082).
///
/// `bibliography_entries` is derived state — a cache of the configured
/// sources — and the in-memory `BibIndex` is built from it at open. So
/// a load error that returned before both were rewritten left the last
/// good data live: the host shows "bibliography failed to load" while
/// every key still renders resolved, and a reopen rebuilds the index
/// straight from the stale table. Contract 5 (degradation honesty),
/// docs/plans/23_citation_surfaces_contracts.md.
#[test]
fn a_failed_seed_does_not_leave_the_previous_entries_resolving() {
    let tmp = tempfile::tempdir().unwrap();
    let root = tmp.path();
    fs::write(root.join("library.bib"), bib_fixture()).unwrap();
    fs::write(
        root.join("paper.md"),
        "Cites [@smith2020].
",
    )
    .unwrap();

    let session = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    session
        .scan_initial(&crate::CancelToken::new())
        .expect("scan succeeds");
    let source = |path: &str| crate::BibliographySource {
        path: path.to_string(),
        format: crate::BibFormat::BibTeX,
        watch: false,
    };

    // One good seed, so there IS a previous session's data.
    session
        .set_bibliography_sources(vec![source("library.bib")])
        .expect("first seed succeeds");
    assert!(session.list_unresolved_citations().unwrap().is_empty());

    // The configured source becomes unreadable, and the next seed fails.
    fs::remove_file(root.join("library.bib")).unwrap();
    let err = session
        .set_bibliography_sources(vec![source("library.bib")])
        .unwrap_err();
    assert!(
        matches!(err, VaultError::BibSourceUnreadable { .. }),
        "expected BibSourceUnreadable, got {err:?}"
    );

    // Nothing stale survives the failure, in this session...
    assert!(
        session.get_bibliography_entries().unwrap().is_empty(),
        "a failed seed left the previous entries queryable"
    );
    let unresolved = session.list_unresolved_citations().unwrap();
    assert_eq!(
        unresolved.len(),
        1,
        "smith2020 no longer resolves and must be reported unresolved, got {unresolved:?}"
    );

    // ...nor across the reopen, which rebuilds the index from the table.
    drop(session);
    let reopened = VaultSession::from_filesystem(root.to_path_buf()).unwrap();
    reopened
        .scan_initial(&crate::CancelToken::new())
        .expect("rescan succeeds");
    assert!(
        reopened.get_bibliography_entries().unwrap().is_empty(),
        "the reopened session rebuilt its index from stale persisted entries"
    );
}
