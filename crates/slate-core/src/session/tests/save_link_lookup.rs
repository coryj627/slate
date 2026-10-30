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

// --- Plan 44 WB-19 (#1426): a create heals only the dangling links its
// path could resolve. ---

fn heal_rows_visited() -> usize {
    crate::links_db::HEAL_ROWS_VISITED.with(std::cell::Cell::get)
}

/// How many `links` rows whose raw target satisfies `predicate` (a SQL
/// expression over `l`) hold `target_path` `resolved` (or NULL).
fn link_rows_where(session: &VaultSession, predicate: &str, resolved: Option<&str>) -> i64 {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        &format!("SELECT COUNT(*) FROM links l WHERE ({predicate}) AND l.target_path IS ?1"),
        [resolved],
        |row| row.get(0),
    )
    .unwrap()
}

/// #1426: a create reads only the dangling rows whose authored target could
/// name the created path — never a qualified or rooted target in another
/// folder that shares its final component, never an unrelated bare name —
/// and no snapshot of `files`; it heals exactly the rows that name it.
#[test]
fn a_create_heals_only_the_dangling_rows_that_could_name_it() {
    const UNRELATED: usize = 50;
    const SHARING_THE_NAME: usize = 200;
    let naming = [
        "f\u{f6}o",
        "f\u{f6}o.md",
        "F\u{d6}O",
        "/notes/f\u{f6}o",
        "./notes/f\u{f6}o.md",
        "notes/f\u{f6}o",
        "Notes/F\u{f6}o.md",
    ];
    let (_tmp, session) = make_vault(|provider| {
        let links = |targets: &mut dyn Iterator<Item = String>| -> String {
            targets.map(|target| format!("[[{target}]]\n")).collect()
        };
        provider
            .write_file(
                "src/naming.md",
                links(&mut naming.iter().map(|target| target.to_string())).as_bytes(),
            )
            .unwrap();
        provider
            .write_file(
                "src/unrelated.md",
                links(&mut (1..=UNRELATED).map(|n| format!("bar_{n}"))).as_bytes(),
            )
            .unwrap();
        provider
            .write_file(
                "src/sharing.md",
                links(&mut (1..=SHARING_THE_NAME).map(|n| {
                    if n <= SHARING_THE_NAME / 2 {
                        format!("missing_{n}/f\u{f6}o")
                    } else {
                        format!("/missing_{n}/f\u{f6}o.md")
                    }
                }))
                .as_bytes(),
            )
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        link_rows_where(&session, "1", None),
        (naming.len() + UNRELATED + SHARING_THE_NAME) as i64,
        "premise: every link dangles before the create"
    );

    reset_counters();
    crate::links_db::PATH_ORDER_SNAPSHOT_PATHS.with(|paths| paths.set(0));
    crate::links_db::HEAL_ROWS_VISITED.with(|rows| rows.set(0));
    session
        .create_exclusive("notes/f\u{f6}o.md", "# f\u{f6}o\n")
        .unwrap();

    assert_eq!(
        link_rows_where(&session, "1", Some("notes/f\u{f6}o.md")),
        naming.len() as i64,
        "the create healed other than the rows that name it"
    );
    assert_eq!(
        link_rows_where(&session, "l.target_raw LIKE '%missing%'", None),
        SHARING_THE_NAME as i64,
        "a target in another folder resolved to the created file"
    );
    assert_eq!(snapshot_paths(), 0, "the heal read a snapshot of the vault");
    assert_eq!(
        crate::links_db::PATH_ORDER_SNAPSHOT_PATHS.with(std::cell::Cell::get),
        0,
        "the heal read every path through the path-order helper"
    );
    assert_eq!(
        heal_rows_visited(),
        naming.len(),
        "the heal read rows beyond the {} that name the created path",
        naming.len()
    );
}

/// #1426: lowercasing reads context, so a created path's folded form and
/// its stem folded alone can differ — `ΟΔΟΣ.md` folds its sigma medial
/// (`οδοσ.md`), the bare stem `ΟΔΟΣ` final (`οδος`) — and an extension can
/// fold to a Markdown one (`marKdown` with the Kelvin sign). The heal still
/// reaches every row the selector would match, as the vault-wide heal does.
#[test]
fn a_create_heals_a_target_whose_stem_folds_apart_from_its_path() {
    for (created, target) in [
        ("kelvin/note.mar\u{212a}down", "kelvin/note"),
        (
            "\u{39f}\u{394}\u{39f}\u{3a3}.md",
            "\u{3bf}\u{3b4}\u{3bf}\u{3c3}",
        ),
    ] {
        let (_tmp, session) = make_vault(|provider| {
            provider
                .write_file("src/source.md", format!("# s\n\n[[{target}]]\n").as_bytes())
                .unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        crate::links_db::HEAL_ROWS_VISITED.with(|rows| rows.set(0));
        session.create_exclusive(created, "# t\n").unwrap();
        assert_eq!(
            outgoing_targets(&session, "src/source.md"),
            [Some(created.to_string())],
            "{target:?} does not name {created:?} after its create"
        );
        assert_eq!(heal_rows_visited(), 1, "{created:?}");
    }
}

/// The `links` rows of every census source note, in path order.
fn census_source_rows(session: &VaultSession, sources: &[String]) -> Vec<(String, Vec<LinkRow>)> {
    sources
        .iter()
        .map(|source| (source.clone(), link_rows(session, source)))
        .collect()
}

/// #1426 (the equivalence census): with every shared fixture target
/// dangling, one source note each, creating every fixture path in turn
/// heals exactly what the vault-wide heal heals — two vaults, one per heal,
/// compared row for row after each create.
#[test]
fn a_create_heals_exactly_what_the_vault_wide_heal_heals() {
    use crate::link_resolver::census_fixtures::{INDEXED_PATHS, TARGETS};
    let sources: Vec<String> = (0..TARGETS.len())
        .map(|n| format!("src/t{n:02}.md"))
        .collect();
    let build = || {
        let (tmp, session) = make_vault(|provider| {
            for (source, target) in sources.iter().zip(TARGETS) {
                provider
                    .write_file(source, format!("# s\n\n[[{target}]]\n").as_bytes())
                    .unwrap();
            }
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        (tmp, session)
    };
    let (_targeted_tmp, targeted) = build();
    let (_vault_wide_tmp, vault_wide) = build();
    let mut healed = 0;
    for path in INDEXED_PATHS {
        targeted.create_exclusive(path, "# t\n").unwrap();
        crate::links_db::HEAL_ROWS_VISITED.with(|rows| rows.set(0));
        crate::links_db::CREATE_HEAL_VAULT_WIDE.with(|on| on.set(true));
        let created = vault_wide.create_exclusive(path, "# t\n");
        crate::links_db::CREATE_HEAL_VAULT_WIDE.with(|on| on.set(false));
        created.unwrap();
        assert_eq!(
            heal_rows_visited(),
            0,
            "premise: the vault-wide arm ran the targeted heal ({path})"
        );
        let expected = census_source_rows(&vault_wide, &sources);
        assert_eq!(
            census_source_rows(&targeted, &sources),
            expected,
            "after creating {path}"
        );
        healed = expected
            .iter()
            .flat_map(|(_, rows)| rows)
            .filter(|row| row.target_path.is_some())
            .count();
    }
    assert!(
        healed > 20,
        "premise: the creates healed only {healed} links"
    );
}

/// #1426 (never silent): a heal whose lookup fails fails the create after
/// its bytes landed — `PublishedUnindexed`, the file on disk, no `files`
/// row, and every dangling row it would have healed still dangling: the
/// whole transaction, heal included, rolled back.
#[test]
fn a_create_whose_heal_fails_is_published_unindexed_and_commits_nothing() {
    let build = || {
        let (tmp, session) = make_vault(|provider| {
            for n in 1..=3 {
                provider
                    .write_file(&format!("ghosts/g{n}.md"), b"# g\n\n[[fresh]]\n")
                    .unwrap();
            }
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        (tmp, session)
    };
    let fresh_rows = |session: &VaultSession, resolved: Option<&str>| {
        link_rows_where(session, "l.target_raw = 'fresh'", resolved)
    };

    let (_control_tmp, control) = build();
    match control.create_exclusive_reporting("fresh.md", "# fresh\n") {
        Ok(CreateExclusiveOutcome::Committed(_)) => {}
        other => panic!("control: expected Committed, got {other:?}"),
    }
    assert_eq!(
        fresh_rows(&control, Some("fresh.md")),
        3,
        "control: the heal reaches the rows"
    );

    let (tmp, session) = build();
    crate::links_db::LOOKUP_FAULT.with(|fault| fault.set(true));
    let outcome = session.create_exclusive_reporting("fresh.md", "# fresh\n");
    crate::links_db::LOOKUP_FAULT.with(|fault| fault.set(false));

    match outcome {
        Ok(CreateExclusiveOutcome::PublishedUnindexed {
            path, content_hash, ..
        }) => {
            assert_eq!(path, "fresh.md");
            assert_eq!(content_hash, crate::content_hash(b"# fresh\n"));
        }
        other => panic!("expected PublishedUnindexed, got {other:?}"),
    }
    assert_eq!(
        std::fs::read_to_string(tmp.path().join("fresh.md")).unwrap(),
        "# fresh\n",
        "the bytes landed"
    );
    let conn = session.conn.lock().unwrap();
    let rows: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM files WHERE path = 'fresh.md'",
            [],
            |row| row.get(0),
        )
        .unwrap();
    drop(conn);
    assert_eq!(rows, 0, "the failed create committed its files row");
    assert_eq!(
        fresh_rows(&session, None),
        3,
        "a heal UPDATE survived the rollback"
    );
}

/// A vault of 1,000 notes in 20 folders, each with 20 dangling links (10
/// bare names, 10 qualified targets) — 20,000 nothing resolves.
fn dangling_vault(scan: bool) -> (tempfile::TempDir, VaultSession) {
    let (tmp, session) = make_vault(|provider| {
        for n in 0..1_000 {
            let body: String = (0..10)
                .map(|i| format!("[[ghost_{n}_{i}]] [[gone/ghost_{n}_{i}]]\n"))
                .collect();
            provider
                .write_file(&format!("notes/d{}/n{n:04}.md", n % 20), body.as_bytes())
                .unwrap();
        }
    });
    if scan {
        session.scan_initial(&CancelToken::new()).unwrap();
    }
    (tmp, session)
}

fn median_ms(mut values: Vec<f64>) -> f64 {
    values.sort_by(|a, b| a.partial_cmp(b).unwrap());
    values[values.len() / 2]
}

/// The `BENCHMARKS.md` "Plan 44 WB-19" harness (#1426), not a fact: run it
/// in release with `cargo test -p slate-core --release --lib
/// create_heal_timing -- --ignored --nocapture`. Beside 20,000 dangling
/// links it times a create healed vault-wide (the old heal, through the
/// test seam) and targeted, alternately; a cold scan with migration 041's
/// two indexes and without them, alternately on fresh vaults; and the
/// migration's index build on the scanned cache.
#[test]
#[ignore = "timing harness for BENCHMARKS.md (#1426); run in release with --ignored --nocapture"]
fn create_heal_timing() {
    let (_tmp, session) = dangling_vault(true);
    let (mut vault_wide, mut targeted, mut targeted_rows) = (Vec::new(), Vec::new(), Vec::new());
    for round in 0..9 {
        crate::links_db::CREATE_HEAL_VAULT_WIDE.with(|on| on.set(true));
        let start = std::time::Instant::now();
        session
            .create_exclusive(&format!("new/w{round}.md"), "# new\n")
            .unwrap();
        vault_wide.push(start.elapsed().as_secs_f64() * 1000.0);
        crate::links_db::CREATE_HEAL_VAULT_WIDE.with(|on| on.set(false));
        crate::links_db::HEAL_ROWS_VISITED.with(|rows| rows.set(0));
        let start = std::time::Instant::now();
        session
            .create_exclusive(&format!("new/t{round}.md"), "# new\n")
            .unwrap();
        targeted.push(start.elapsed().as_secs_f64() * 1000.0);
        targeted_rows.push(heal_rows_visited());
    }
    println!(
        "TIMING create: vault_wide median {:.2} ms {vault_wide:.2?}; targeted median {:.2} ms {targeted:.2?}; targeted heal rows {targeted_rows:?}",
        median_ms(vault_wide.clone()),
        median_ms(targeted.clone()),
    );

    let drop_indexes = |session: &VaultSession| {
        session
            .conn
            .lock()
            .unwrap()
            .execute_batch(
                "DROP INDEX idx_links_target_path_fold; DROP INDEX idx_links_target_name_fold;",
            )
            .unwrap();
    };
    let (mut with_indexes, mut without_indexes) = (Vec::new(), Vec::new());
    for _ in 0..5 {
        for indexed in [true, false] {
            let (_tmp, session) = dangling_vault(false);
            if !indexed {
                drop_indexes(&session);
            }
            let start = std::time::Instant::now();
            session.scan_initial(&CancelToken::new()).unwrap();
            let ms = start.elapsed().as_secs_f64() * 1000.0;
            if indexed {
                with_indexes.push(ms);
            } else {
                without_indexes.push(ms);
            }
        }
    }
    println!(
        "TIMING cold scan: with the indexes median {:.0} ms {with_indexes:.0?}; without median {:.0} ms {without_indexes:.0?}",
        median_ms(with_indexes.clone()),
        median_ms(without_indexes.clone()),
    );

    let mut builds = Vec::new();
    for _ in 0..5 {
        drop_indexes(&session);
        let start = std::time::Instant::now();
        session
            .conn
            .lock()
            .unwrap()
            .execute_batch(include_str!(
                "../../../migrations/041_links_target_fold_index.sql"
            ))
            .unwrap();
        builds.push(start.elapsed().as_secs_f64() * 1000.0);
    }
    let dangling: i64 = session
        .conn
        .lock()
        .unwrap()
        .query_row(
            "SELECT COUNT(*) FROM links WHERE target_path IS NULL",
            [],
            |row| row.get(0),
        )
        .unwrap();
    println!(
        "TIMING migration 041 index build over {dangling} dangling rows: median {:.2} ms {builds:.2?}",
        median_ms(builds.clone()),
    );
}
