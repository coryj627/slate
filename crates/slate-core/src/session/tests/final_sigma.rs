// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1705 (plan 47 CB-32, the owner's CD22): a name whose capital sigma
//! lowercases to the final form `ς` in one spelling and to `σ` in another
//! is one name to every match core makes: links on every route, the
//! rename's rewrite, Quick Open, the Files sidebar filter, the collision
//! gate and the batch move's preflight. One fold, `tree_sort_key`, is
//! stored in the indexes and rebuilt once by migration 042.

use super::common::*;
use super::*;

use crate::structural_batch::{
    BatchFailureStage, BatchMoveRequest, BatchMoveState, StructuralBatchItem,
};

fn targets(session: &VaultSession, path: &str) -> Vec<(String, Option<String>)> {
    session
        .outgoing_links(path)
        .unwrap()
        .into_iter()
        .map(|link| (link.target_raw, link.target_path))
        .collect()
}

fn scanned(setup: impl FnOnce(&FsVaultProvider)) -> (tempfile::TempDir, VaultSession) {
    let (tmp, session) = make_vault(setup);
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

fn quick_open(session: &VaultSession, query: &str) -> Vec<String> {
    session
        .switcher_rank_top_indexed(query, &[], 50, &CancelToken::new())
        .unwrap()
        .rows
        .into_iter()
        .map(|row| row.path)
        .collect()
}

fn filtered(session: &VaultSession, query: &str) -> Vec<String> {
    session
        .filter_files(query, None, None, &[], Paging::first(50))
        .unwrap()
        .files
        .into_iter()
        .map(|file| file.path)
        .collect()
}

const EVERY_SPELLING: &str = "[[ΚΟΣΜΟΣ]] [[κοσμος]] [[Κοσμος]] [[κοσμοσ]] [[ΚΟΣΜΟΣ.md]] \
     [[/ΚΟΣΜΟΣ]] ![[ΚΟΣΜΟΣ]] [[Οδος]] [[ΟΔΟΣ]] [[οδοσ]]\n";

/// Every spelling resolves through a save's SQL pre-filter (the fold
/// indexes of migrations 039 and 037), the embed's row included.
#[test]
fn a_save_resolves_a_final_sigma_target_through_the_fold_indexes() {
    let (_tmp, session) = scanned(|provider| {
        provider
            .write_file("ΚΟΣΜΟΣ.md", "# k\n".as_bytes())
            .unwrap();
        provider.write_file("ΟΔΟΣ.md", "# o\n".as_bytes()).unwrap();
    });
    session.save_text("A.md", EVERY_SPELLING, None).unwrap();
    for (target, resolved) in targets(&session, "A.md") {
        let expected = if target.to_lowercase().contains("κοσμο") {
            "ΚΟΣΜΟΣ.md"
        } else {
            "ΟΔΟΣ.md"
        };
        assert_eq!(
            resolved.as_deref(),
            Some(expected),
            "[[{target}]] after a save"
        );
    }
}

/// A cold scan resolves every spelling, and a row an older build left
/// dangling (the state an upgrade leaves) heals at the next scan's tail.
#[test]
fn a_scan_heals_a_final_sigma_link_left_dangling() {
    let (_tmp, session) = scanned(|provider| {
        provider
            .write_file("ΚΟΣΜΟΣ.md", "# k\n".as_bytes())
            .unwrap();
        provider
            .write_file("A.md", "[[ΚΟΣΜΟΣ]] [[κοσμος]]\n".as_bytes())
            .unwrap();
    });
    assert_eq!(
        targets(&session, "A.md"),
        vec![
            ("ΚΟΣΜΟΣ".to_string(), Some("ΚΟΣΜΟΣ.md".to_string())),
            ("κοσμος".to_string(), Some("ΚΟΣΜΟΣ.md".to_string())),
        ],
        "after the cold scan"
    );
    session
        .conn
        .lock()
        .unwrap()
        .execute("UPDATE links SET target_path = NULL", [])
        .unwrap();
    session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();
    assert!(
        targets(&session, "A.md")
            .iter()
            .all(|(_, resolved)| resolved.as_deref() == Some("ΚΟΣΜΟΣ.md")),
        "the scan's tail heals the rows: {:?}",
        targets(&session, "A.md")
    );
}

/// A create heals a dangling row whose target spells the sigma the other
/// way (CB-31's heal, through migration 041's folded target indexes).
#[test]
fn a_create_heals_a_final_sigma_target() {
    let (_tmp, session) = scanned(|provider| {
        provider
            .write_file("A.md", "[[Κοσμος]] [[dir/ΚΟΣΜΟΣ]]\n".as_bytes())
            .unwrap();
    });
    session.create_exclusive("ΚΟΣΜΟΣ.md", "# k\n").unwrap();
    session.create_exclusive("dir/κοσμος.md", "# d\n").unwrap();
    assert_eq!(
        targets(&session, "A.md"),
        vec![
            ("Κοσμος".to_string(), Some("ΚΟΣΜΟΣ.md".to_string())),
            ("dir/ΚΟΣΜΟΣ".to_string(), Some("dir/κοσμος.md".to_string())),
        ]
    );
}

/// An embed's preview resolves its target through the same fold.
#[test]
fn an_embed_of_a_final_sigma_target_renders() {
    let (_tmp, session) = scanned(|provider| {
        provider
            .write_file("ΚΟΣΜΟΣ.md", "# k\n\nbody\n".as_bytes())
            .unwrap();
        provider
            .write_file("A.md", "![[κοσμος]]\n".as_bytes())
            .unwrap();
    });
    for target in ["κοσμος", "ΚΟΣΜΟΣ", "Κοσμος"] {
        let resolution = session
            .resolve_embed("A.md", target, None, &CancelToken::new())
            .unwrap();
        match resolution {
            crate::EmbedResolution::FullNote { target_path, .. } => {
                assert_eq!(target_path, "ΚΟΣΜΟΣ.md", "![[{target}]]");
            }
            other => panic!("![[{target}]] did not resolve: {other:?}"),
        }
    }
}

/// A rename rewrites every spelling that names the renamed note, including
/// a source whose only link spells the stem with a medial sigma (it was no
/// rewrite candidate while the candidate scan lowered stems alone).
#[test]
fn renaming_a_final_sigma_note_rewrites_every_link_that_names_it() {
    let (tmp, session) = scanned(|provider| {
        provider.write_file("ΟΔΟΣ.md", "# o\n".as_bytes()).unwrap();
        provider
            .write_file("C.md", "[[ΟΔΟΣ]] [[Οδος]] [[οδοσ]]\n".as_bytes())
            .unwrap();
        provider
            .write_file("D.md", "Only [[οδοσ]] here.\n".as_bytes())
            .unwrap();
    });
    session.rename_file("ΟΔΟΣ.md", "Δρομος.md").unwrap();
    for source in ["C.md", "D.md"] {
        let text = std::fs::read_to_string(tmp.path().join(source)).unwrap();
        assert!(
            !text.to_lowercase().contains("οδο"),
            "{source} still names the old stem: {text:?}"
        );
        assert!(
            targets(&session, source)
                .iter()
                .all(|(_, resolved)| resolved.as_deref() == Some("Δρομος.md")),
            "{source}: {:?}",
            targets(&session, source)
        );
    }
}

/// A rename of a note whose own name carries an authored final sigma
/// rewrites the links that spell it with a capital or a medial sigma: the
/// rewrite's old-to-new mapping keys the old path by the same fold.
#[test]
fn renaming_a_note_named_with_a_final_sigma_rewrites_every_spelling() {
    let (tmp, session) = scanned(|provider| {
        provider
            .write_file("κοσμος.md", "# k\n".as_bytes())
            .unwrap();
        provider
            .write_file("E.md", "[[ΚΟΣΜΟΣ]] [[κοσμοσ]] [[Κοσμος]]\n".as_bytes())
            .unwrap();
    });
    session.rename_file("κοσμος.md", "Νεος.md").unwrap();
    let text = std::fs::read_to_string(tmp.path().join("E.md")).unwrap();
    assert!(
        !text.to_lowercase().contains("κοσμο"),
        "E.md still names the old stem: {text:?}"
    );
    assert!(
        targets(&session, "E.md")
            .iter()
            .all(|(_, resolved)| resolved.as_deref() == Some("Νεος.md")),
        "{:?}",
        targets(&session, "E.md")
    );
}

/// A move keeps every spelling resolved to the moved note.
#[test]
fn moving_a_final_sigma_note_keeps_every_link_that_names_it() {
    let (_tmp, session) = scanned(|provider| {
        provider.write_file("ΟΔΟΣ.md", "# o\n".as_bytes()).unwrap();
        provider
            .write_file("dir/keep.md", "# keep\n".as_bytes())
            .unwrap();
        provider
            .write_file("C.md", "[[ΟΔΟΣ]] [[Οδος]] [[οδοσ]]\n".as_bytes())
            .unwrap();
    });
    session.move_file("ΟΔΟΣ.md", "dir").unwrap();
    assert!(
        targets(&session, "C.md")
            .iter()
            .all(|(_, resolved)| resolved.as_deref() == Some("dir/ΟΔΟΣ.md")),
        "{:?}",
        targets(&session, "C.md")
    );
}

/// Quick Open finds a name whichever sigma the reader typed or the name
/// carries.
#[test]
fn quick_open_finds_a_final_sigma_name_whichever_sigma_was_typed() {
    let (_tmp, session) = scanned(|provider| {
        provider
            .write_file("ΚΟΣΜΟΣ.md", "# k\n".as_bytes())
            .unwrap();
        provider
            .write_file("κοσμος2.md", "# k2\n".as_bytes())
            .unwrap();
        provider.write_file("ΟΔΟΣ.md", "# o\n".as_bytes()).unwrap();
    });
    for query in ["κοσμος", "ΚΟΣΜΟΣ", "κοσμοσ", "Κοσμος"] {
        let rows = quick_open(&session, query);
        assert!(
            rows.contains(&"ΚΟΣΜΟΣ.md".to_string()),
            "{query:?}: {rows:?}"
        );
        assert!(
            rows.contains(&"κοσμος2.md".to_string()),
            "{query:?}: {rows:?}"
        );
    }
    assert!(quick_open(&session, "ΚΟΣΜΟΣ2").contains(&"κοσμος2.md".to_string()));
    assert!(quick_open(&session, "οδος").contains(&"ΟΔΟΣ.md".to_string()));
}

/// The Files sidebar filter folds the typed word and every name the same
/// way, so a partial word and either sigma find the note.
#[test]
fn the_sidebar_filter_finds_a_final_sigma_name_whichever_sigma_was_typed() {
    let (_tmp, session) = scanned(|provider| {
        provider
            .write_file("ΚΟΣΜΟΣ.md", "# k\n".as_bytes())
            .unwrap();
        provider.write_file("Λογος.md", "# l\n".as_bytes()).unwrap();
        provider
            .write_file("unrelated.md", "# u\n".as_bytes())
            .unwrap();
    });
    for (query, expected) in [
        ("κοσμοσ", "ΚΟΣΜΟΣ.md"),
        ("κοσμος", "ΚΟΣΜΟΣ.md"),
        ("ΚΟΣ", "ΚΟΣΜΟΣ.md"),
        ("λογοσ", "Λογος.md"),
        ("ΛΟΓΟΣ", "Λογος.md"),
    ] {
        assert_eq!(
            filtered(&session, query),
            vec![expected.to_string()],
            "{query:?}"
        );
    }
}

/// A `ς`/`σ` pair already in a vault (NTFS and a case-sensitive volume
/// both hold one) behaves as case variants do: a link naming either takes
/// the resolver's tiebreak, the byte-smaller path, and the collision gate
/// refuses a new twin for portability (APFS folds the two, by reading).
#[test]
fn a_final_sigma_pair_behaves_like_case_variants() {
    let (tmp, session) = scanned(|provider| {
        provider
            .write_file("οδος.md", "# final\n".as_bytes())
            .unwrap();
        provider
            .write_file("A.md", "[[οδοσ]] [[ΟΔΟΣ]] [[οδος]]\n".as_bytes())
            .unwrap();
        provider
            .write_file("κοσμος.md", "# k\n".as_bytes())
            .unwrap();
    });
    // The twin arrives outside Slate (the gate would refuse it), as it
    // can on NTFS or a case-sensitive volume.
    let pair_written = std::fs::write(tmp.path().join("οδοσ.md"), "# medial\n").is_ok()
        && std::fs::read_to_string(tmp.path().join("οδος.md")).unwrap() == "# final\n";
    if pair_written {
        session
            .rescan_with_progress(&CancelToken::new(), None)
            .unwrap();
        // A rescan does not revisit an unchanged source's resolved rows, so
        // the source is saved again to resolve against the pair.
        session
            .save_text("A.md", "[[οδοσ]] [[ΟΔΟΣ]] [[οδος]]\n", None)
            .unwrap();
        // `ς` (U+03C2) sorts before `σ` (U+03C3): the byte-smaller path wins.
        assert!(
            targets(&session, "A.md")
                .iter()
                .all(|(_, resolved)| resolved.as_deref() == Some("οδος.md")),
            "{:?}",
            targets(&session, "A.md")
        );
    }
    let refused = session.create_exclusive("κοσμοσ.md", "# twin\n");
    assert!(
        refused.is_err(),
        "the gate admitted a twin of κοσμος.md: {refused:?}"
    );
    let names: Vec<String> = std::fs::read_dir(tmp.path())
        .unwrap()
        .map(|entry| entry.unwrap().file_name().to_string_lossy().into_owned())
        .collect();
    assert!(
        !names.iter().any(|name| name == "κοσμοσ.md"),
        "the twin was written: {names:?}"
    );
}

/// The batch move's preflight folds as the single-item gate does, so a
/// `ς`/`σ` twin and an NFD twin of an occupied destination are refused at
/// preflight, not landed beside it.
#[test]
fn a_batch_move_refuses_a_final_sigma_twin_at_preflight() {
    let (tmp, session) = scanned(|provider| {
        provider
            .write_file("dest/κοσμος.md", "# k\n".as_bytes())
            .unwrap();
        provider
            .write_file("dest/caf\u{e9}.md", "# nfc\n".as_bytes())
            .unwrap();
        provider
            .write_file("κοσμοσ.md", "# twin\n".as_bytes())
            .unwrap();
        provider
            .write_file("cafe\u{301}.md", "# nfd\n".as_bytes())
            .unwrap();
    });
    for name in ["κοσμοσ.md", "cafe\u{301}.md"] {
        let report = session
            .batch_move(BatchMoveRequest {
                items: vec![StructuralBatchItem {
                    path: name.to_string(),
                    is_directory: false,
                }],
                new_parent: "dest".into(),
            })
            .unwrap();
        assert_ne!(report.state, BatchMoveState::Succeeded, "{name:?} landed");
        assert!(
            report
                .envelope
                .preflight_failures
                .iter()
                .any(|failure| failure.stage == BatchFailureStage::Preflight),
            "{name:?} was not refused at preflight: {report:?}"
        );
        assert!(tmp.path().join(name).exists(), "{name:?} left its source");
    }
}

/// Two selected items whose destinations are a `ς`/`σ` or an NFD/NFC pair
/// collide at preflight, as they would on APFS.
#[test]
fn a_batch_move_refuses_two_items_whose_destinations_fold_together() {
    let (tmp, session) = scanned(|provider| {
        for path in [
            "a/κοσμος.md",
            "b/κοσμοσ.md",
            "a/caf\u{e9}.md",
            "b/cafe\u{301}.md",
        ] {
            provider.write_file(path, b"# x\n").unwrap();
        }
        provider.write_file("dest/keep.md", b"# keep\n").unwrap();
    });
    for (one, two) in [
        ("a/κοσμος.md", "b/κοσμοσ.md"),
        ("a/caf\u{e9}.md", "b/cafe\u{301}.md"),
    ] {
        let report = session
            .batch_move(BatchMoveRequest {
                items: [one, two]
                    .into_iter()
                    .map(|path| StructuralBatchItem {
                        path: path.to_string(),
                        is_directory: false,
                    })
                    .collect(),
                new_parent: "dest".into(),
            })
            .unwrap();
        assert_ne!(
            report.state,
            BatchMoveState::Succeeded,
            "{one} and {two} landed"
        );
        assert_eq!(
            report.envelope.preflight_failures.len(),
            2,
            "{one} and {two}: {report:?}"
        );
        assert!(tmp.path().join(one).exists() && tmp.path().join(two).exists());
    }
}

/// Undoing a batch move whose original place now holds a `ς`/`σ` twin is
/// refused at the undo's preflight, not landed beside the twin.
#[test]
fn undoing_a_batch_move_onto_a_final_sigma_twin_is_refused_at_preflight() {
    let (tmp, session) = scanned(|provider| {
        provider.write_file("κοσμος.md", b"# moved\n").unwrap();
        provider.write_file("dest/keep.md", b"# keep\n").unwrap();
    });
    let moved = session
        .batch_move(BatchMoveRequest {
            items: vec![StructuralBatchItem {
                path: "κοσμος.md".to_string(),
                is_directory: false,
            }],
            new_parent: "dest".into(),
        })
        .unwrap();
    assert_eq!(moved.state, BatchMoveState::Succeeded, "{moved:?}");
    // The twin arrives outside Slate, which NTFS allows.
    std::fs::write(tmp.path().join("κοσμοσ.md"), "# twin\n").unwrap();
    session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();
    let undone = session
        .undo_batch_move(moved.op_id.expect("the move's op id"))
        .unwrap();
    assert_ne!(undone.state, BatchMoveState::Succeeded, "{undone:?}");
    assert!(
        undone
            .envelope
            .preflight_failures
            .iter()
            .any(|failure| failure.stage == BatchFailureStage::Preflight),
        "{undone:?}"
    );
    assert!(tmp.path().join("dest").join("κοσμος.md").exists());
}

/// Stand a vault's cache back at version 41 with every index over the fold
/// written by the old fold (NFC + `to_lowercase`), as a build before
/// migration 042 left it; `old_rows` then writes rows as that build would.
fn stand_back_at_41(root: &std::path::Path, old_rows: impl FnOnce(&rusqlite::Connection)) {
    let conn = rusqlite::Connection::open(root.join(".slate").join("cache.sqlite")).unwrap();
    crate::db::register_connection_functions(&conn).unwrap();
    conn.create_scalar_function(
        "slate_tree_sort_key",
        1,
        rusqlite::functions::FunctionFlags::SQLITE_UTF8
            | rusqlite::functions::FunctionFlags::SQLITE_DETERMINISTIC,
        |context| {
            use unicode_normalization::UnicodeNormalization;
            let name = context.get::<Option<String>>(0)?;
            Ok(name.map(|name| name.nfc().collect::<String>().to_lowercase()))
        },
    )
    .unwrap();
    let indexes: Vec<(String, String)> = {
        let mut stmt = conn
            .prepare(
                "SELECT name, sql FROM sqlite_master WHERE type = 'index' \
                 AND sql LIKE '%slate_tree_sort_key%' ORDER BY name",
            )
            .unwrap();
        stmt.query_map([], |row| Ok((row.get(0)?, row.get(1)?)))
            .unwrap()
            .map(Result::unwrap)
            .collect()
    };
    assert_eq!(indexes.len(), 7, "{indexes:?}");
    for (name, sql) in &indexes {
        conn.execute_batch(&format!("DROP INDEX {name}; {sql};"))
            .unwrap();
    }
    old_rows(&conn);
    conn.execute("DELETE FROM schema_version WHERE version > 41", [])
        .unwrap();
}

/// A link an older build resolved to one of a `ς`/`σ` pair by its exact
/// spelling takes the pair's tiebreak after the upgrade with no save of
/// its source: migration 042 clears the resolution of every internal link
/// whose target carries a sigma, and the open's scan resolves them again
/// with the new fold (Codex round 1).
#[test]
fn an_upgrade_resolves_a_sigma_link_again_without_a_save() {
    let (tmp, session) = scanned(|provider| {
        provider
            .write_file("A.md", "[[οδοσ]] [[other]]\n".as_bytes())
            .unwrap();
        provider.write_file("other.md", "# o\n".as_bytes()).unwrap();
        provider
            .write_file("οδος.md", "# final\n".as_bytes())
            .unwrap();
    });
    let pair_written = std::fs::write(tmp.path().join("οδοσ.md"), "# medial\n").is_ok()
        && std::fs::read_to_string(tmp.path().join("οδος.md")).unwrap() == "# final\n";
    if !pair_written {
        // A volume that folds the two sigmas (APFS, by reading) cannot
        // hold the pair, so there is nothing to resolve again.
        return;
    }
    session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();
    drop(session);
    // The older build resolved `[[οδοσ]]` to the exact spelling.
    stand_back_at_41(tmp.path(), |conn| {
        let changed = conn
            .execute(
                "UPDATE links SET target_path = 'οδοσ.md' WHERE target_raw = 'οδοσ'",
                [],
            )
            .unwrap();
        assert_eq!(changed, 1);
    });
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        targets(&session, "A.md"),
        vec![
            ("οδοσ".to_string(), Some("οδος.md".to_string())),
            ("other".to_string(), Some("other.md".to_string())),
        ]
    );
}

/// An index an older build wrote under the old fold is rebuilt by
/// migration 042 at the next open, so with no rescan a save resolves
/// `[[ΚΟΣΜΟΣ]]` to `κοσμος.md` through the rebuilt name index, and a create
/// heals a dangling `[[Οδος]]` through the rebuilt target index. The two
/// names carry an authored final sigma, so their old keys differ from the
/// new ones: without the rebuild both lookups miss silently.
#[test]
fn an_upgraded_index_resolves_a_final_sigma_link_without_a_rescan() {
    let (tmp, session) = scanned(|provider| {
        provider
            .write_file("κοσμος.md", "# k\n".as_bytes())
            .unwrap();
        provider
            .write_file("A.md", "[[Οδος]]\n".as_bytes())
            .unwrap();
    });
    drop(session);
    stand_back_at_41(tmp.path(), |_| {});
    let session = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    session
        .save_text("B.md", "[[ΚΟΣΜΟΣ]] [[κοσμος]]\n", None)
        .unwrap();
    assert_eq!(
        targets(&session, "B.md"),
        vec![
            ("ΚΟΣΜΟΣ".to_string(), Some("κοσμος.md".to_string())),
            ("κοσμος".to_string(), Some("κοσμος.md".to_string())),
        ],
        "a save resolves through the rebuilt name index"
    );
    session.create_exclusive("ΟΔΟΣ.md", "# o\n").unwrap();
    assert_eq!(
        targets(&session, "A.md"),
        vec![("Οδος".to_string(), Some("ΟΔΟΣ.md".to_string()))],
        "a create heals through the rebuilt target index"
    );
    let integrity: String = session
        .conn
        .lock()
        .unwrap()
        .query_row("PRAGMA integrity_check", [], |row| row.get(0))
        .unwrap();
    assert_eq!(integrity, "ok");
}
