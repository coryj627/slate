// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1295 (contract 40 AR-32; locked decision 05 §4 and §9.3.1): a Markdown
//! save — `save_text`, the create path and `open_base`'s repair — resolves
//! the saved note's own link targets through the folded-name lookup, each
//! distinct target once, instead of reading every `files` row; scan and
//! rename keep their snapshot. The counters are test seams:
//! `links_db::RESOLUTION_ROWS_VISITED` (the rows a save's own resolution
//! offers to its selectors) and `link_resolver::SNAPSHOT_PATHS` (the paths
//! materialized into a whole-index snapshot).

use super::common::*;
use super::*;

/// The notes nothing links to, beside the targets below: none shares a name
/// or a path with any target.
const UNRELATED_NOTES: usize = 300;

/// A scanned vault holding the targets [`linking_body`] names and
/// [`UNRELATED_NOTES`] notes nothing names; `extra` writes before the scan.
fn vault_with_targets(extra: impl FnOnce(&FsVaultProvider)) -> (tempfile::TempDir, VaultSession) {
    let (tmp, session) = make_vault(|provider| {
        for (path, body) in [
            ("alpha.md", "# Alpha\n\n## Intro\n"),
            ("notes/beta.md", "# Beta\n"),
            ("gamma.md", "# Gamma\n"),
            ("a/shared.md", "# a\n"),
            ("notes/shared.md", "# notes\n"),
            ("z/shared.md", "# z\n"),
        ] {
            provider.write_file(path, body.as_bytes()).unwrap();
        }
        provider
            .write_file("img/pic.png", b"\x89PNG\r\n\x1a\n")
            .unwrap();
        for n in 0..UNRELATED_NOTES {
            provider
                .write_file(&format!("bulk/d{}/u{n:03}.md", n % 10), b"# unrelated\n")
                .unwrap();
        }
        extra(provider);
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

/// Ten internal links over eight distinct targets — `alpha` three times
/// (once with an anchor), `Alpha` with an alias, a folder path, a
/// vault-root path, a basename three folders carry, a ghost, an attachment
/// and the note itself — and an external link, which needs no lookup.
fn linking_body(own_stem: &str) -> String {
    format!(
        "# Host\n\n[[alpha]] [[alpha]] [[alpha#Intro]] [[Alpha|the first]]\n\
         [[notes/beta]] [[/gamma]] [[shared]] [[ghost]]\n\
         ![[pic.png]] [[{own_stem}]] [a site](https://example.com)\n"
    )
}

/// The candidate rows of [`linking_body`]'s eight distinct targets: one
/// each, three for `shared`, none for `ghost`.
const LINKING_BODY_CANDIDATE_ROWS: usize = 9;

/// What [`linking_body`]'s links resolve to, in order, from `own_path`.
fn linking_body_targets(own_path: &str) -> Vec<Option<String>> {
    [
        Some("alpha.md"),
        Some("alpha.md"),
        Some("alpha.md"),
        Some("alpha.md"),
        Some("notes/beta.md"),
        Some("gamma.md"),
        Some("notes/shared.md"),
        None,
        Some("img/pic.png"),
        Some(own_path),
        None,
    ]
    .into_iter()
    .map(|target| target.map(str::to_string))
    .collect()
}

fn outgoing_targets(session: &VaultSession, path: &str) -> Vec<Option<String>> {
    session
        .outgoing_links(path)
        .unwrap()
        .into_iter()
        .map(|link| link.target_path)
        .collect()
}

fn reset_counters() {
    crate::links_db::RESOLUTION_ROWS_VISITED.with(|rows| rows.set(0));
    crate::link_resolver::SNAPSHOT_PATHS.with(|paths| paths.set(0));
}

fn resolution_rows_visited() -> usize {
    crate::links_db::RESOLUTION_ROWS_VISITED.with(std::cell::Cell::get)
}

fn snapshot_paths() -> usize {
    crate::link_resolver::SNAPSHOT_PATHS.with(std::cell::Cell::get)
}

/// #1295: a save's link resolution visits only the candidate rows of the
/// note's own distinct targets — none of the vault's other notes, and a
/// repeated target once — and the save materializes no snapshot of `files`.
#[test]
fn a_save_resolves_its_links_from_its_own_targets_rows_only() {
    let (_tmp, session) = vault_with_targets(|provider| {
        provider.write_file("notes/host.md", b"# Host\n").unwrap();
    });

    reset_counters();
    session
        .save_text("notes/host.md", &linking_body("host"), None)
        .unwrap();

    assert_eq!(
        resolution_rows_visited(),
        LINKING_BODY_CANDIDATE_ROWS,
        "the save visited rows beyond its targets' candidates"
    );
    assert_eq!(snapshot_paths(), 0, "the save read a snapshot of the vault");
    assert_eq!(
        outgoing_targets(&session, "notes/host.md"),
        linking_body_targets("notes/host.md")
    );
}

/// #1295: the create path's own link resolution is bounded the same way.
/// (The create's ghost-link heal, `re_resolve_unresolved_links`, is a
/// separate pass over OTHER notes' dangling links — not this resolution —
/// and is not bounded by this change.)
#[test]
fn a_create_resolves_its_links_from_its_own_targets_rows_only() {
    let (_tmp, session) = vault_with_targets(|_| {});

    reset_counters();
    session
        .create_exclusive("notes/created.md", &linking_body("created"))
        .unwrap();

    assert_eq!(
        resolution_rows_visited(),
        LINKING_BODY_CANDIDATE_ROWS,
        "the create visited rows beyond its targets' candidates"
    );
    assert_eq!(
        outgoing_targets(&session, "notes/created.md"),
        linking_body_targets("notes/created.md")
    );
}

/// #1295: `open_base`'s repair — a `.base` opened before its row is
/// indexed — indexes the row without materializing a snapshot of every
/// `files` row.
#[test]
fn an_open_base_repair_reads_no_snapshot_of_the_vault() {
    let (_tmp, session) = vault_with_targets(|_| {});
    session
        .provider
        .write_file(
            "views/all.base",
            b"views:\n  - type: table\n    name: All\n",
        )
        .unwrap();

    reset_counters();
    session.open_base("views/all.base").unwrap();

    assert_eq!(
        snapshot_paths(),
        0,
        "the repair read a snapshot of the vault"
    );
    let conn = session.conn.lock().unwrap();
    let indexed: bool = conn
        .query_row(
            "SELECT EXISTS(SELECT 1 FROM files f JOIN file_meta fm ON fm.file_id = f.id
                           WHERE f.path = 'views/all.base')",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert!(indexed, "the repair indexed the base's row");
}

/// The `(target_raw, target_path)` pairs the links table holds for `path`,
/// read directly (no query sweep can repair them first).
fn indexed_links(session: &VaultSession, path: &str) -> Vec<(String, Option<String>)> {
    let conn = session.conn.lock().unwrap();
    let mut statement = conn
        .prepare(
            "SELECT l.target_raw, l.target_path FROM links l
             JOIN files f ON f.id = l.source_file_id
             WHERE f.path = ?1 ORDER BY l.ordinal",
        )
        .unwrap();
    statement
        .query_map([path], |row| Ok((row.get(0)?, row.get(1)?)))
        .unwrap()
        .map(|row| row.unwrap())
        .collect()
}

/// #1295 (never silent): a lookup that fails fails the save with the
/// save's existing typed error — `SavedButUnindexed`, the bytes on disk —
/// and the index keeps the note's previous links: no link is dropped and
/// none is written unresolved because its lookup failed.
#[test]
fn a_failed_lookup_fails_the_save_and_drops_no_link() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("target.md", b"# t\n").unwrap();
        provider.write_file("note.md", b"[[target]]\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    crate::links_db::LOOKUP_FAULT.with(|fault| fault.set(true));
    let outcome = session.save_text("note.md", "[[target]] and [[other]]\n", None);
    crate::links_db::LOOKUP_FAULT.with(|fault| fault.set(false));

    match outcome {
        Err(VaultError::SavedButUnindexed { .. }) => {}
        other => panic!("expected SavedButUnindexed, got {other:?}"),
    }
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("note.md")).unwrap(),
        "[[target]] and [[other]]\n",
        "the bytes landed"
    );
    assert_eq!(
        indexed_links(&session, "note.md"),
        [("target".to_string(), Some("target.md".to_string()))],
        "the failed save changed the note's links"
    );
}

/// #1295 (never silent): on the create path the same failure is the
/// create's post-publish outcome — the file is on disk, unindexed — and
/// no row, link or otherwise, was committed for it.
#[test]
fn a_failed_lookup_leaves_a_create_published_but_unindexed() {
    let (tmp, session) = make_vault(|provider| {
        provider.write_file("target.md", b"# t\n").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    crate::links_db::LOOKUP_FAULT.with(|fault| fault.set(true));
    let outcome = session.create_exclusive_reporting("fresh.md", "[[target]]\n");
    crate::links_db::LOOKUP_FAULT.with(|fault| fault.set(false));

    match outcome {
        Ok(CreateExclusiveOutcome::PublishedUnindexed { path, .. }) => {
            assert_eq!(path, "fresh.md");
        }
        other => panic!("expected PublishedUnindexed, got {other:?}"),
    }
    assert!(tmp.path().join("fresh.md").is_file(), "the bytes landed");
    let conn = session.conn.lock().unwrap();
    let rows: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM files WHERE path = 'fresh.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    assert_eq!(rows, 0, "the failed create committed an index row");
}

/// One note naming every target shape: bare names, folder paths,
/// vault-root and `./` paths, relative `../` paths, targets without an
/// extension, anchors and aliases, embeds and attachments, Markdown links,
/// external targets, case and Unicode forms, and ghosts.
const CENSUS_BODY: &str = "# Census\n\n\
[[alpha]] [[Alpha]] [[ALPHA.md]] [[alpha#Intro]] [[alpha^blk]] [[alpha|Alias]]\n\
[[notes/beta]] [[Notes/Beta.md]] [[./notes/beta]] [[/notes/beta]] [[/gamma]] [[/alpha.md]]\n\
[[shared]] [[a/shared]] [[/shared]] [[deep/shared]] [[ghost]] [[notes/ghost]] [[../alpha]]\n\
![[pic.png]] ![[img/pic.png]] ![[PIC.PNG]] ![[report.pdf]] ![[beta]]\n\
[b](notes/beta.md) [up](../alpha.md) [here](./gamma.md) [sp](space%20name.md) [pdf](docs/report.pdf)\n\
[ext](https://example.com) [mail](mailto:a@example.com) [frag](#top) [[#Local]]\n\
[[\u{fc}ber]] [[\u{dc}BER]] [[e\u{301}cole]] [[\u{e9}cole]] [[stra\u{df}e]] [[folder \u{e4}/stra\u{df}e]]\n\
[[\u{130}stanbul]] [[i\u{307}stanbul]] [[istanbul]] [[\u{e6}on]] [[notes/\u{e6}on]] [[\u{212a}elvin]]\n\
[[other]] [[c]] [[d]] [[x.y]] [[x.y.md]] [[foo.txt]] [[foo]] [[space name]] [[mixed case]] [[index]]\n";

/// A scanned vault of the shared census fixtures' files
/// (`link_resolver::census_fixtures::INDEXED_PATHS`), with [`CENSUS_BODY`]
/// at each of `census_notes`.
fn census_vault(census_notes: &[&str]) -> (tempfile::TempDir, VaultSession) {
    let (tmp, session) = make_vault(|provider| {
        for path in crate::link_resolver::census_fixtures::INDEXED_PATHS {
            provider.write_file(path, b"# t\n\n## Intro\n").unwrap();
        }
        for path in census_notes {
            provider.write_file(path, CENSUS_BODY.as_bytes()).unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    (tmp, session)
}

/// One `links` row, every column.
#[derive(Debug, PartialEq, Eq)]
struct LinkRow {
    ordinal: i64,
    target_path: Option<String>,
    target_raw: String,
    target_anchor: Option<String>,
    kind: String,
    is_embed: i64,
    is_external: i64,
    snippet: String,
    span_start: i64,
    span_end: i64,
    display_text: Option<String>,
}

fn link_rows(session: &VaultSession, path: &str) -> Vec<LinkRow> {
    let conn = session.conn.lock().unwrap();
    let mut statement = conn
        .prepare(
            "SELECT l.ordinal, l.target_path, l.target_raw, l.target_anchor, l.kind,
                    l.is_embed, l.is_external, l.snippet, l.span_start, l.span_end,
                    l.display_text
             FROM links l JOIN files f ON f.id = l.source_file_id
             WHERE f.path = ?1 ORDER BY l.ordinal",
        )
        .unwrap();
    statement
        .query_map([path], |row| {
            Ok(LinkRow {
                ordinal: row.get(0)?,
                target_path: row.get(1)?,
                target_raw: row.get(2)?,
                target_anchor: row.get(3)?,
                kind: row.get(4)?,
                is_embed: row.get(5)?,
                is_external: row.get(6)?,
                snippet: row.get(7)?,
                span_start: row.get(8)?,
                span_end: row.get(9)?,
                display_text: row.get(10)?,
            })
        })
        .unwrap()
        .map(|row| row.unwrap())
        .collect()
}

const CENSUS_HOSTS: [&str; 3] = ["census.md", "notes/census.md", "notes/deep/census.md"];

/// #1295 (the equivalence census): the links a save writes are the links a
/// scan writes for the same note — every column of every row, from three
/// depths (the basename tiebreak is by distance from the source).
#[test]
fn a_save_writes_the_links_a_scan_writes() {
    let (_tmp, session) = census_vault(&CENSUS_HOSTS);
    for host in CENSUS_HOSTS {
        let scanned = link_rows(&session, host);
        assert!(
            scanned.iter().any(|row| row.target_path.is_some()),
            "{host}: the scan resolved nothing"
        );
        session.save_text(host, CENSUS_BODY, None).unwrap();
        assert_eq!(link_rows(&session, host), scanned, "{host}");
    }
}

/// #1295 (the equivalence census, create path): the links a create writes
/// are the links a scan of the same vault writes for the created note.
#[test]
fn a_create_writes_the_links_a_scan_writes() {
    let created = ["fresh/census.md", "made.md", "notes/deep/made.md"];
    let (_tmp, session) = census_vault(&CENSUS_HOSTS);
    for path in created {
        session.create_exclusive(path, CENSUS_BODY).unwrap();
    }
    let every_note: Vec<&str> = CENSUS_HOSTS.iter().chain(created.iter()).copied().collect();
    let (_scanned_tmp, scanned) = census_vault(&every_note);
    for path in created {
        let expected = link_rows(&scanned, path);
        assert!(
            expected.iter().any(|row| row.target_path.is_some()),
            "{path}: the scan resolved nothing"
        );
        assert_eq!(link_rows(&session, path), expected, "{path}");
    }
}

/// Write `path` straight into `files`, as only a case-sensitive volume's
/// scan could (no file behind it: the lookup reads rows, not disk).
fn insert_index_only_row(conn: &rusqlite::Connection, path: &str) {
    let name = path.rsplit('/').next().unwrap();
    let extension = name
        .rsplit_once('.')
        .map(|(_, extension)| extension.to_ascii_lowercase());
    conn.execute(
        "INSERT INTO files
            (path, name, extension, size_bytes, mtime_ms, ctime_ms, birthtime_ms,
             content_hash, parser_version, indexed_at_ms, is_markdown, body_text)
         VALUES (?1, ?2, ?3, 0, 0, 0, 0, '', 1, 1, 1, '')",
        rusqlite::params![path, name, extension],
    )
    .unwrap();
}

/// #1295: the save's folded-name lookup resolves exactly as a resolution
/// over the whole index, across the shared resolver fixtures
/// (`link_resolver::census_fixtures`: bare names, folder paths, vault-root
/// paths, relative `../` paths, targets without an extension, attachments,
/// the same name in many folders, case and Unicode folding, and the twins
/// only a case-sensitive volume holds), from six sources, anchors carried
/// through, its memo cold and warm — and the scan's snapshot, drawn through
/// the same seam, answers alike.
#[test]
fn the_save_lookup_resolves_exactly_as_the_whole_index_does() {
    use crate::link_resolver::census_fixtures::{
        DECISIVE_CASES, INDEX_ONLY_TWINS, SOURCES, TARGETS, resolve_by_full_offer, winner,
    };
    use crate::links_db::{FoldedNameLookup, resolve_remembered};
    use std::collections::HashMap;

    let (_tmp, session) = census_vault(&[]);
    let conn = session.conn.lock().unwrap();
    for path in INDEX_ONLY_TWINS {
        insert_index_only_row(&conn, path);
    }
    // The whole index as a whole-table snapshot reads it — what scan and
    // rename resolve over, and saves did — in path order.
    let whole = crate::links_db::indexed_paths_in_path_order(&conn).unwrap();
    let mut in_path_order = whole.clone();
    in_path_order.sort();
    assert_eq!(whole, in_path_order, "the snapshot reads in path order");
    let snapshot = crate::InMemoryVaultIndex::new(whole.clone());

    let anchors = [
        None,
        Some(crate::LinkAnchor::Heading("Intro".into())),
        Some(crate::LinkAnchor::Block("blk".into())),
    ];
    for source in SOURCES {
        let mut looked_up_winners = HashMap::new();
        let mut snapshot_winners = HashMap::new();
        for pass in ["cold memo", "warm memo"] {
            for (n, target) in TARGETS.iter().enumerate() {
                let anchor = anchors[n % anchors.len()].clone();
                let expected = resolve_by_full_offer(target, anchor.clone(), source, &whole);
                let looked_up = resolve_remembered(
                    &FoldedNameLookup,
                    &mut looked_up_winners,
                    &conn,
                    target,
                    anchor.clone(),
                    source,
                )
                .unwrap();
                assert_eq!(
                    looked_up, expected,
                    "{target:?} from {source:?}: lookup, {pass}"
                );
                let drawn = resolve_remembered(
                    &snapshot,
                    &mut snapshot_winners,
                    &conn,
                    target,
                    anchor.clone(),
                    source,
                )
                .unwrap();
                assert_eq!(
                    drawn, expected,
                    "{target:?} from {source:?}: snapshot, {pass}"
                );
                assert_eq!(
                    crate::resolve_link(target, anchor, source, &snapshot),
                    expected,
                    "{target:?} from {source:?}: resolve_link"
                );
            }
        }
    }

    // The census reaches the cases that decide equivalence.
    for (target, source, expected) in DECISIVE_CASES {
        let resolved = resolve_remembered(
            &FoldedNameLookup,
            &mut HashMap::new(),
            &conn,
            target,
            None,
            source,
        )
        .unwrap();
        assert_eq!(winner(&resolved), *expected, "{target:?} from {source:?}");
    }
}

fn path_order_snapshot_paths() -> usize {
    crate::links_db::PATH_ORDER_SNAPSHOT_PATHS.with(std::cell::Cell::get)
}

/// #1427: `reindex_path`'s repair resolves the note's links through the
/// folded-name lookup, as a save does: only its own targets' candidate
/// rows, and no snapshot of `files` — neither materialized nor read
/// through the path-order helper.
#[test]
fn a_repair_through_reindex_path_reads_no_snapshot_of_the_vault() {
    let (_tmp, session) = vault_with_targets(|provider| {
        provider
            .write_file("notes/host.md", linking_body("host").as_bytes())
            .unwrap();
    });

    reset_counters();
    crate::links_db::PATH_ORDER_SNAPSHOT_PATHS.with(|paths| paths.set(0));
    session.reindex_path("notes/host.md").unwrap();

    assert_eq!(
        snapshot_paths(),
        0,
        "the repair read a snapshot of the vault"
    );
    assert_eq!(
        path_order_snapshot_paths(),
        0,
        "the repair read every path through the path-order helper"
    );
    assert!(
        resolution_rows_visited() <= LINKING_BODY_CANDIDATE_ROWS,
        "the repair visited {} rows, beyond its targets' {LINKING_BODY_CANDIDATE_ROWS} candidates",
        resolution_rows_visited()
    );
    assert_eq!(
        outgoing_targets(&session, "notes/host.md"),
        linking_body_targets("notes/host.md")
    );
}

/// #1427 (the equivalence census, repair path): the links a repair writes
/// are the links a scan writes for the same note — every column of every
/// row, from three depths.
#[test]
fn a_repair_writes_the_links_a_scan_writes() {
    let (_tmp, session) = census_vault(&CENSUS_HOSTS);
    for host in CENSUS_HOSTS {
        let scanned = link_rows(&session, host);
        assert!(
            scanned.iter().any(|row| row.target_path.is_some()),
            "{host}: the scan resolved nothing"
        );
        session.reindex_path(host).unwrap();
        assert_eq!(link_rows(&session, host), scanned, "{host}");
    }
}
