// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1429 (plan 43 PR 20.0; contract 40 AR-32's scan side): a snapshot of
//! `files` — the scan's, rename's and the dangling-link pass's — offers each
//! link only the paths its target could name, looked up by lowercased final
//! component or whole path, instead of every indexed path. The pass that
//! every scan runs (`links_db::re_resolve_unresolved_links_under`) then
//! costs per unresolved link rather than unresolved links × files, and
//! still resolves exactly what offering every path resolves.

use super::common::*;
use super::*;

use crate::link_resolver::census_fixtures::{resolve_by_full_offer, winner};

/// A note naming targets nobody has written yet: bare names in three cases,
/// an anchor and an alias, folder, vault-root and `./` paths, a Markdown
/// link, a basename three folders will carry, composed Unicode, an
/// attachment, and one target that stays missing until a create.
const WAITING_BODY: &str = "# Waiting\n\n\
[[Ghost]] [[ghost]] [[GHOST.md]] [[ghost#Intro]] [[ghost|later]]\n\
[[later/ghost]] [[Later/Ghost.md]] [[/later/ghost]] [[./later/ghost]] [g](later/ghost.md)\n\
[[shared]] [[\u{dc}ber]] [[\u{fc}BER]] ![[pic.png]] [[still-missing]] [site](https://example.com)\n";

const WAITING_HOSTS: [&str; 3] = ["waiting.md", "notes/waiting.md", "notes/deep/waiting.md"];

/// The `(target_raw, target_path)` of every internal link `path` holds, in
/// order, read straight from the table.
fn internal_links(session: &VaultSession, path: &str) -> Vec<(String, Option<String>)> {
    session
        .outgoing_links(path)
        .unwrap()
        .into_iter()
        .filter(|link| !link.is_external)
        .map(|link| (link.target_raw, link.target_path))
        .collect()
}

/// What offering every indexed path resolves each of `path`'s internal
/// links to — the oracle, over the index as it stands.
fn full_offer_answers(session: &VaultSession, path: &str) -> Vec<(String, Option<String>)> {
    let every_path = {
        let conn = session.conn.lock().unwrap();
        crate::links_db::indexed_paths_in_path_order(&conn).unwrap()
    };
    internal_links(session, path)
        .into_iter()
        .map(|(target_raw, _)| {
            let resolved = resolve_by_full_offer(&target_raw, None, path, &every_path);
            let target = winner(&resolved).map(str::to_string);
            (target_raw, target)
        })
        .collect()
}

fn target_of(links: &[(String, Option<String>)], target_raw: &str) -> Option<String> {
    links
        .iter()
        .find(|(raw, _)| raw == target_raw)
        .unwrap_or_else(|| panic!("no link to {target_raw:?}"))
        .1
        .clone()
}

/// #1429: a file that appears after the links naming it — written beside
/// the vault and picked up by a rescan, or made by a create — still
/// resolves them, every link exactly as offering it every indexed path
/// would, from three depths; and a link to a file that is still missing
/// stays unresolved until that file exists.
#[test]
fn a_created_file_still_resolves_a_previously_unresolved_link() {
    let (tmp, session) = make_vault(|provider| {
        for host in WAITING_HOSTS {
            provider.write_file(host, WAITING_BODY.as_bytes()).unwrap();
        }
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    for host in WAITING_HOSTS {
        let links = internal_links(&session, host);
        assert_eq!(links.len(), 15, "{host}: {links:?}");
        assert!(
            links.iter().all(|(_, target)| target.is_none()),
            "{host}: a link resolved before its target existed: {links:?}"
        );
    }

    // Written beside the vault: the rescan's dangling-link pass heals them.
    for (path, body) in [
        ("later/ghost.md", "# Ghost\n\n## Intro\n"),
        ("a/shared.md", "# a\n"),
        ("notes/shared.md", "# notes\n"),
        ("z/shared.md", "# z\n"),
        ("\u{fc}ber.md", "# \u{dc}ber\n"),
    ] {
        let file = tmp.path().join(path);
        std::fs::create_dir_all(file.parent().unwrap()).unwrap();
        std::fs::write(file, body).unwrap();
    }
    std::fs::write(tmp.path().join("later/pic.png"), b"\x89PNG\r\n\x1a\n").unwrap();
    let report = session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();
    assert!(report.complete, "{report:?}");
    for host in WAITING_HOSTS {
        let links = internal_links(&session, host);
        assert_eq!(links, full_offer_answers(&session, host), "{host}");
        for raw in [
            "Ghost",
            "ghost",
            "GHOST.md",
            "later/ghost",
            "Later/Ghost.md",
            "/later/ghost",
            "./later/ghost",
            "later/ghost.md",
        ] {
            assert_eq!(
                target_of(&links, raw).as_deref(),
                Some("later/ghost.md"),
                "{host}: {raw:?}"
            );
        }
        assert_eq!(
            target_of(&links, "\u{dc}ber").as_deref(),
            Some("\u{fc}ber.md"),
            "{host}"
        );
        assert_eq!(
            target_of(&links, "\u{fc}BER").as_deref(),
            Some("\u{fc}ber.md"),
            "{host}"
        );
        assert_eq!(
            target_of(&links, "pic.png").as_deref(),
            Some("later/pic.png"),
            "{host}"
        );
        assert_eq!(target_of(&links, "still-missing"), None, "{host}");
    }
    // A basename three folders carry goes to the nearest, then the first.
    let shared = |host: &str| target_of(&internal_links(&session, host), "shared");
    assert_eq!(shared("waiting.md").as_deref(), Some("a/shared.md"));
    assert_eq!(
        shared("notes/waiting.md").as_deref(),
        Some("notes/shared.md")
    );
    assert_eq!(
        shared("notes/deep/waiting.md").as_deref(),
        Some("notes/shared.md")
    );
    let backlinks: Vec<String> = session
        .backlinks("later/ghost.md", Paging::first(200))
        .unwrap()
        .items
        .into_iter()
        .map(|backlink| backlink.source_path)
        .collect();
    for host in WAITING_HOSTS {
        assert_eq!(
            backlinks.iter().filter(|source| *source == host).count(),
            10,
            "{host}: {backlinks:?}"
        );
    }

    // Made by a create: the create's heal resolves the last one.
    session
        .create_exclusive("still-missing.md", "# Here now\n")
        .unwrap();
    for host in WAITING_HOSTS {
        let links = internal_links(&session, host);
        assert_eq!(links, full_offer_answers(&session, host), "{host}");
        assert_eq!(
            target_of(&links, "still-missing").as_deref(),
            Some("still-missing.md"),
            "{host}"
        );
    }
    assert!(
        session
            .list_unresolved_links(Paging::first(10))
            .unwrap()
            .items
            .is_empty()
    );
}

/// #1429 (never silent): the snapshot the dangling-link pass resolves over
/// is a read of `files`; when that read fails the rescan says so — it is
/// incomplete, its report names the pass — and it resolves nothing it could
/// not see. The next clean rescan heals the link.
#[test]
fn a_failed_snapshot_read_leaves_the_rescan_incomplete_and_resolves_nothing() {
    let (tmp, session) = make_vault(|provider| {
        provider
            .write_file("source.md", b"see [[later]]\n")
            .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    std::fs::write(tmp.path().join("later.md"), b"# Later\n").unwrap();

    // Arm the fault for the pass's own read only: the scan's start-of-scan
    // snapshot has been read by the time the pass's point fires.
    crate::session::scan_point_test_hook::install(Box::new(|point| {
        if point == "re-resolve" {
            crate::links_db::SNAPSHOT_FAULT.with(|fault| fault.set(true));
        }
    }));
    let outcome = session.rescan_with_progress(&CancelToken::new(), None);
    crate::session::scan_point_test_hook::clear();
    crate::links_db::SNAPSHOT_FAULT.with(|fault| fault.set(false));

    let report = outcome.unwrap();
    assert!(!report.complete, "{report:?}");
    assert!(
        report
            .error_samples
            .iter()
            .any(|error| error.starts_with("re-resolve unresolved links:")),
        "{report:?}"
    );
    assert_eq!(
        internal_links(&session, "source.md"),
        [("later".to_string(), None)]
    );

    let report = session
        .rescan_with_progress(&CancelToken::new(), None)
        .unwrap();
    assert!(report.complete, "{report:?}");
    assert_eq!(
        internal_links(&session, "source.md"),
        [("later".to_string(), Some("later.md".to_string()))]
    );
}

/// The paths `operation` materialized into snapshots on this thread
/// (`link_resolver::SNAPSHOT_PATHS`), and how many of them the path-order
/// helper read (`links_db::PATH_ORDER_SNAPSHOT_PATHS`).
fn snapshot_reads(operation: impl FnOnce()) -> (usize, usize) {
    let counters = || {
        (
            crate::link_resolver::SNAPSHOT_PATHS.with(std::cell::Cell::get),
            crate::links_db::PATH_ORDER_SNAPSHOT_PATHS.with(std::cell::Cell::get),
        )
    };
    let (materialized_before, helper_before) = counters();
    operation();
    let (materialized_after, helper_after) = counters();
    (
        materialized_after - materialized_before,
        helper_after - helper_before,
    )
}

/// #1429: every snapshot a link resolution reads comes through
/// `links_db::indexed_paths_in_path_order` — the one statement whose path
/// order `links_db::tests::the_whole_table_snapshot_reads_in_path_order`
/// pins — and through nothing else: a cold scan and its dangling-link pass,
/// a rescan with a changed linked note, the one-path repair (which adds its
/// own path), `wikilink_for_path`, a Bases query whose rows carry a
/// link-valued property, a rename's heal and link rewrite, and a rename
/// that reclassifies a note. The save lookup reads a path target's rows in
/// the same order through its own statement (`links_db::candidate_rows_sql`);
/// the embed lookup's unordered rows are #1428.
#[test]
fn every_resolution_snapshot_is_read_through_the_path_order_helper() {
    let (tmp, session) = make_vault(|provider| {
        provider
            .write_file("Projects/Target.md", b"# Target\n")
            .unwrap();
        provider
            .write_file(
                "Projects/Note.md",
                b"---\nauthor: \"[[Target]]\"\n---\n# Note\n\nSee [[Target]] and [[later]].\n",
            )
            .unwrap();
        provider
            .write_file("Elsewhere/Host.md", b"# Host\n\n[[Note]]\n")
            .unwrap();
    });
    let read_through_the_helper = |what: &str, (materialized, helper): (usize, usize), extra| {
        assert!(helper > 0, "{what}: read no snapshot through the helper");
        assert_eq!(
            materialized,
            helper + extra,
            "{what}: a snapshot came from somewhere else"
        );
    };

    read_through_the_helper(
        "a cold scan",
        snapshot_reads(|| {
            session.scan_initial(&CancelToken::new()).unwrap();
        }),
        0,
    );
    std::fs::write(
        tmp.path().join("Elsewhere/Host.md"),
        "# Host\n\n[[Note]] [[Target]]\n",
    )
    .unwrap();
    read_through_the_helper(
        "a rescan",
        snapshot_reads(|| {
            let report = session
                .rescan_with_progress(&CancelToken::new(), None)
                .unwrap();
            assert!(report.complete, "{report:?}");
        }),
        0,
    );
    read_through_the_helper(
        "the one-path repair",
        snapshot_reads(|| session.reindex_path("Projects/Note.md").unwrap()),
        1,
    );
    read_through_the_helper(
        "wikilink_for_path",
        snapshot_reads(|| {
            assert_eq!(
                session.wikilink_for_path("Projects/Target.md").unwrap(),
                Some("[[Target]]".to_string())
            );
        }),
        0,
    );
    let handle = session
        .open_base_inline(
            "views:\n  - type: table\n    name: All\n    order:\n      - file.path\n      - author\n",
            None,
        )
        .unwrap();
    read_through_the_helper(
        "a Bases query",
        snapshot_reads(|| {
            let result = session
                .base_execute(handle, 0, None, None, &CancelToken::new())
                .unwrap();
            assert_eq!(result.view_error, None, "{result:?}");
        }),
        0,
    );
    read_through_the_helper(
        "a rename",
        snapshot_reads(|| {
            let report = session
                .rename_file("Projects/Target.md", "Renamed.md")
                .unwrap();
            assert!(report.failed.is_empty(), "{:?}", report.failed);
        }),
        0,
    );
    read_through_the_helper(
        "a reclassifying rename",
        snapshot_reads(|| {
            let report = session
                .rename_file("Elsewhere/Host.md", "Host.txt")
                .unwrap();
            assert!(report.failed.is_empty(), "{:?}", report.failed);
        }),
        0,
    );
}

/// Notes in each perf-guard vault.
const GUARD_NOTES: usize = 10_000;

/// A perf-guard vault: [`GUARD_NOTES`] notes of about 1 KiB with
/// frontmatter across 50 folders (the scan bench's layout and the vault
/// watcher design's §3.4 spike), each linking `dangling` notes nobody has
/// written, indexed by a cold scan whose session is closed again.
struct GuardVault {
    dir: tempfile::TempDir,
}

impl GuardVault {
    fn new(dangling: usize) -> Self {
        let dir = tempfile::tempdir().unwrap();
        for folder in 0..50 {
            std::fs::create_dir_all(dir.path().join(format!("notes/{folder:03}"))).unwrap();
        }
        for n in 0..GUARD_NOTES {
            let mut body = format!(
                "---\ntitle: Note {n}\ncreated: 2026-10-18\ntags: [guard, d{}]\n---\n# Note {n}\n\n",
                n % 7
            );
            for link in 0..dangling {
                body.push_str(&format!("Later: [[ghost-{n:05}-{link}]]. "));
            }
            while body.len() < 1024 {
                body.push_str("lorem ipsum dolor sit amet consectetur adipiscing elit ");
            }
            std::fs::write(
                dir.path()
                    .join(format!("notes/{:03}/note-{n:05}.md", n % 50)),
                body,
            )
            .unwrap();
        }
        let session = VaultSession::from_filesystem(dir.path().to_path_buf()).unwrap();
        let report = session.scan_initial(&CancelToken::new()).unwrap();
        assert!(report.complete, "{report:?}");
        assert_eq!(
            session
                .list_unresolved_links(Paging::first(1))
                .unwrap()
                .total_filtered,
            (dangling * GUARD_NOTES) as u64
        );
        Self { dir }
    }

    /// The faster of two no-change rescans on a session of its own, the only
    /// one open: while a second session is open in the process, the older
    /// one's rescans miss its page cache and run slower (seen on macOS while
    /// this guard was written), which would bias the comparison by which
    /// vault opened first.
    fn best_of_two_rescans(&self) -> std::time::Duration {
        let session = VaultSession::from_filesystem(self.dir.path().to_path_buf()).unwrap();
        (0..2)
            .map(|_| {
                let start = std::time::Instant::now();
                let report = session
                    .rescan_with_progress(&CancelToken::new(), None)
                    .unwrap();
                let elapsed = start.elapsed();
                assert!(report.complete, "{report:?}");
                assert_eq!(
                    report.files_changed, 0,
                    "the rescan was not a no-change one"
                );
                elapsed
            })
            .min()
            .expect("two samples")
    }
}

/// #1429's performance guard, isolated in CI like the root-listing guard:
/// a no-change rescan of 10k notes carrying 20,000 links to notes not yet
/// written stays within twice the link-free vault's. Before the fix the
/// dangling-link pass offered every indexed path to every unresolved link
/// (4.6 s against 86 ms on the Mac Studio), so the bound trips on any
/// return to a links x files pass while leaving room for the per-link work.
/// Four rounds alternate the two vaults, so drift over the run reaches
/// both; each side keeps its best.
#[test]
fn perf_guard_unresolved_link_rescan_within_twice_the_link_free_one() {
    let link_free = GuardVault::new(0);
    let dangling = GuardVault::new(2);
    let mut link_free_best = std::time::Duration::MAX;
    let mut dangling_best = std::time::Duration::MAX;
    for _ in 0..4 {
        link_free_best = link_free_best.min(link_free.best_of_two_rescans());
        dangling_best = dangling_best.min(dangling.best_of_two_rescans());
    }
    assert!(
        dangling_best <= link_free_best * 2,
        "a no-change rescan with {} dangling links took {dangling_best:?} (best of 8), over \
         twice the link-free rescan's {link_free_best:?}: the dangling-link pass must cost per \
         link, not links x files (#1429)",
        2 * GUARD_NOTES
    );
}
