// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Plan 44 WB-18 (#1485): a rename plans its link rewrites from the
//! candidate rows of each link's target, not by offering every indexed path
//! to every link's pre-move resolution.

use super::common::*;
use super::*;

/// Notes nothing links to: none shares a name or a path with a target.
const UNRELATED_NOTES: usize = 300;

/// The notes linking the renamed note.
const SOURCES: usize = 5;

/// Each source's distinct targets — a qualified path and a bare name, both
/// the renamed note, and a helper nothing moves — one candidate row each.
const SOURCE_BODY: &str = "# Source\n\n[[notes/target]] [[target]] [[helper]]\n";
const DISTINCT_TARGETS: usize = 3;

/// The resolutions a rename runs per link it plans — before the move and
/// after — with headroom for its heal and the links table's own pass.
const RESOLUTIONS_PER_TARGET: usize = 4;

fn paths_offered() -> usize {
    crate::link_resolver::PATHS_OFFERED.with(std::cell::Cell::get)
}

/// #1485: renaming a note linked by several sources offers the resolver at
/// most each source's distinct targets' candidates per resolution — bounded
/// by the links, not by the vault's size.
#[test]
fn a_rename_plans_its_rewrites_from_candidate_rows_only() {
    let (tmp, session) = make_vault(|provider| {
        provider
            .write_file("notes/target.md", b"# Target\n")
            .unwrap();
        provider.write_file("helper.md", b"# Helper\n").unwrap();
        for n in 0..SOURCES {
            provider
                .write_file(&format!("src/s{n}.md"), SOURCE_BODY.as_bytes())
                .unwrap();
        }
        for n in 0..UNRELATED_NOTES {
            provider
                .write_file(&format!("bulk/d{}/u{n:03}.md", n % 10), b"# unrelated\n")
                .unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let before = paths_offered();
    session
        .rename_file("notes/target.md", "Renamed.md")
        .unwrap();
    let offered = paths_offered() - before;
    assert!(
        offered > 0,
        "premise: the rename's resolutions ran on another thread; the counter saw none"
    );

    let bound = SOURCES * DISTINCT_TARGETS * RESOLUTIONS_PER_TARGET;
    assert!(
        offered <= bound,
        "the rename offered {offered} paths, beyond {bound} (sources x distinct targets x \
         resolutions); the vault holds {} files",
        UNRELATED_NOTES + SOURCES + 2
    );
    let rewritten = std::fs::read_to_string(tmp.path().join("src/s0.md")).unwrap();
    assert!(
        !rewritten.contains("[[notes/target]]") && !rewritten.contains("[[target]]"),
        "premise: the rename planned no rewrite: {rewritten:?}"
    );
}
