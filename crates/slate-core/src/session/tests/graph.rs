// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! GraphIndex tests (Milestone P #550, p0_spec §P0-1).
//!
//! Everything here drives the REAL session mutation APIs so the
//! incremental hooks are exercised end-to-end; after each mutation the
//! maintained index must `deep_equals` a fresh rebuild from SQLite,
//! and `graph_is_built()` must stay true — proving the incremental
//! path stayed live rather than silently falling back to rebuilds.
//! The scaled adversarial censuses land with P0-4 (#553).

use super::*;
use crate::graph::{EdgeKind, NodeKey, NodeKind};

use super::common::make_vault;

/// Build the index (first graph query), returning its canonical node
/// list for assertions.
fn built_nodes(session: &VaultSession) -> Vec<(NodeKey, NodeKind, String)> {
    session.with_graph(|g| g.canonical_nodes()).unwrap()
}

fn built_edges(session: &VaultSession) -> Vec<(NodeKey, NodeKey, EdgeKind, u32)> {
    session
        .with_graph(|g| {
            g.canonical_edges()
                .into_iter()
                .map(|(s, t, k, c, _variants)| (s, t, k, c))
                .collect()
        })
        .unwrap()
}

/// The census seam in miniature: the incrementally-maintained index
/// must structurally equal a fresh build, and must still BE the
/// incrementally-maintained index (never dropped to `None`).
fn assert_matches_rebuild(session: &VaultSession, context: &str) {
    assert!(
        session.graph_is_built(),
        "graph index was dropped (incremental path went defensive) after: {context}"
    );
    let fresh = session.graph_rebuild_reference().unwrap();
    let equal = session.with_graph(|g| g.deep_equals(&fresh)).unwrap();
    if !equal {
        let live = session
            .with_graph(|g| (g.canonical_nodes(), g.canonical_edges()))
            .unwrap();
        panic!(
            "incremental graph diverged from rebuild after: {context}\n\
             live nodes: {:?}\nfresh nodes: {:?}\nlive edges: {:?}\nfresh edges: {:?}",
            live.0,
            fresh.canonical_nodes(),
            live.1,
            fresh.canonical_edges(),
        );
    }
}

fn path_key(p: &str) -> NodeKey {
    NodeKey::Path(p.to_string())
}

fn ghost(g: &str) -> NodeKey {
    NodeKey::Ghost(g.to_string())
}

#[test]
fn build_fixture_vault_exact_node_and_edge_sets() {
    // Fixture: resolved + unresolved + external + embed + parallel
    // references + an attachment target.
    let (_tmp, session) = make_vault(|p| {
        p.write_file(
            "a.md",
            b"[[b]] and [[b]] again, embed ![[b]], ghost [[Missing Note]], \
              image ![[pic.png]], external [x](https://example.com)",
        )
        .unwrap();
        p.write_file("b.md", b"back to [[a]]").unwrap();
        p.write_file("pic.png", b"\x89PNG").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let nodes = built_nodes(&session);
    assert_eq!(
        nodes,
        vec![
            (path_key("a.md"), NodeKind::Note, "a".to_string()),
            (path_key("b.md"), NodeKind::Note, "b".to_string()),
            (
                path_key("pic.png"),
                NodeKind::Attachment,
                "pic.png".to_string()
            ),
            (
                ghost("missing note"),
                NodeKind::Ghost,
                "Missing Note".to_string()
            ),
        ],
    );

    let edges = built_edges(&session);
    // Canonical order: (source key, target key, kind) with
    // Path < Ghost and Link < Embed.
    assert_eq!(
        edges,
        vec![
            // Parallel [[b]] references collapse with count 2; the
            // embed is a separate edge of its own kind.
            (path_key("a.md"), path_key("b.md"), EdgeKind::Link, 2),
            (path_key("a.md"), path_key("b.md"), EdgeKind::Embed, 1),
            (path_key("a.md"), path_key("pic.png"), EdgeKind::Embed, 1),
            (path_key("a.md"), ghost("missing note"), EdgeKind::Link, 1),
            (path_key("b.md"), path_key("a.md"), EdgeKind::Link, 1),
        ],
    );
}

#[test]
fn incremental_save_edit_links_matches_rebuild() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[b]]").unwrap();
        p.write_file("b.md", b"").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session); // build the index

    // Edit links: drop [[b]], add a ghost + an embed.
    session
        .save_text("a.md", "now [[nowhere]] and ![[b]]", None)
        .unwrap();
    assert_matches_rebuild(&session, "save that rewrites a linkset");

    // Ghost removal on last-reference removal.
    session.save_text("a.md", "no links at all", None).unwrap();
    assert_matches_rebuild(&session, "save that empties a linkset");
    let nodes = built_nodes(&session);
    assert!(
        !nodes.iter().any(|(k, _, _)| matches!(k, NodeKey::Ghost(_))),
        "ghost must vanish with its last reference: {nodes:?}"
    );
}

#[test]
fn create_note_becomes_node_without_resolving_old_ghosts() {
    // SQLite only re-resolves unresolved rows at scan / move time —
    // creating a file does NOT heal existing ghost rows, and the
    // graph replays SQLite rather than improving on it.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[Fresh Note]]").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session);

    session.create_exclusive("Fresh Note.md", "hello").unwrap();
    assert_matches_rebuild(&session, "create_exclusive of a ghost's namesake");
    let nodes = built_nodes(&session);
    assert!(
        nodes.iter().any(|(k, _, _)| *k == ghost("fresh note")),
        "ghost persists until a scan/move re-resolves: {nodes:?}"
    );

    // The next scan heals: rows re-resolve, ghost merges into the
    // Path node (ghost merge on file-materialize).
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_matches_rebuild(&session, "scan re-resolve after materialize");
    let nodes = built_nodes(&session);
    assert!(
        !nodes.iter().any(|(k, _, _)| *k == ghost("fresh note")),
        "ghost must merge into the materialized note after re-resolve: {nodes:?}"
    );
    let edges = built_edges(&session);
    assert!(
        edges.contains(&(
            path_key("a.md"),
            path_key("Fresh Note.md"),
            EdgeKind::Link,
            1
        )),
        "reference must now point at the real note: {edges:?}"
    );
}

#[test]
fn rename_keeps_edges_and_reresolve_heals_matching_ghosts() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"see [[target]]").unwrap();
        p.write_file("b.md", b"see [[other]]").unwrap();
        p.write_file("other.md", b"[[a]]").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session);

    // Rename other.md -> target.md: inbound rows repoint (bulk
    // UPDATE), a.md's ghost "target" re-resolves in the same tx1, and
    // b.md's reference to the OLD name goes ghost only when b.md is
    // rewritten (link-rewrite re-save) — all replayed through hooks.
    session.rename_file("other.md", "target.md").unwrap();
    assert_matches_rebuild(&session, "rename_file with ghost heal + rewrites");

    let edges = built_edges(&session);
    assert!(
        edges.contains(&(path_key("a.md"), path_key("target.md"), EdgeKind::Link, 1)),
        "ghost 'target' must heal onto the renamed note: {edges:?}"
    );
    assert!(
        edges.contains(&(path_key("target.md"), path_key("a.md"), EdgeKind::Link, 1)),
        "renamed note keeps its outgoing edge: {edges:?}"
    );
}

#[test]
fn folder_move_replays_every_contained_file() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("dir/x.md", b"[[y]]").unwrap();
        p.write_file("dir/y.md", b"[[x]] and [[outside]]").unwrap();
        p.write_file("outside.md", b"[[dir/x]]").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session);

    session.rename_folder("dir", "newdir").unwrap();
    assert_matches_rebuild(&session, "rename_folder replay");
}

#[test]
fn delete_leaves_dangling_inbound_as_per_row_ghosts() {
    // Rule 1a: deleting b.md leaves a.md's rows resolved-but-dangling
    // in SQLite; both build and incremental map them to a ghost keyed
    // on each row's own target_raw.
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[b]] and ![[B]]").unwrap();
        p.write_file("b.md", b"").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session);

    session.delete_file("b.md").unwrap();
    assert_matches_rebuild(&session, "delete_file with inbound references");

    let nodes = built_nodes(&session);
    assert!(
        nodes.contains(&(ghost("b"), NodeKind::Ghost, "B".to_string())),
        "dangling rows become a ghost; label = lexicographically \
         smallest authored variant ('B' < 'b'): {nodes:?}"
    );
    assert!(
        !nodes.iter().any(|(k, _, _)| *k == path_key("b.md")),
        "deleted file's node must go: {nodes:?}"
    );

    // Delete-then-recreate: dangling rows still name b.md, so the
    // reborn file reclaims them (FileAdded resolved_inbound heal).
    session.create_exclusive("b.md", "reborn").unwrap();
    assert_matches_rebuild(&session, "recreate over dangling rows");
    let edges = built_edges(&session);
    assert!(
        edges.contains(&(path_key("a.md"), path_key("b.md"), EdgeKind::Link, 1))
            && edges.contains(&(path_key("a.md"), path_key("b.md"), EdgeKind::Embed, 1)),
        "recreated file reclaims its dangling inbound rows: {edges:?}"
    );
}

#[test]
fn delete_folder_with_interlinked_files_matches_rebuild() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("keep.md", b"[[gone/a]] [[gone/b]]").unwrap();
        p.write_file("gone/a.md", b"[[gone/b]] [[keep]]").unwrap();
        p.write_file("gone/b.md", b"[[gone/a]]").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session);

    session.delete_folder("gone").unwrap();
    assert_matches_rebuild(&session, "delete_folder of interlinked subtree");

    let nodes = built_nodes(&session);
    // keep.md's two references dangle into per-raw ghosts; the
    // deleted files' own edges (including to keep.md) are gone.
    assert!(nodes.iter().any(|(k, _, _)| *k == ghost("gone/a")));
    assert!(nodes.iter().any(|(k, _, _)| *k == ghost("gone/b")));
}

#[test]
fn task_toggle_rides_the_save_hooks() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"- [ ] task with [[b]]").unwrap();
        p.write_file("b.md", b"").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session);

    // Task toggle rides save_text_locked -> index_saved_file.
    session.toggle_task_status("a.md", 0, 'x', None).unwrap();
    assert_matches_rebuild(&session, "toggle_task_status save path");
}

#[test]
fn ghost_label_is_smallest_current_variant() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[Zeta]]").unwrap();
        p.write_file("c.md", b"[[zeta]]").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let nodes = built_nodes(&session);
    assert!(
        nodes.contains(&(ghost("zeta"), NodeKind::Ghost, "Zeta".to_string())),
        "one ghost, label = min variant ('Zeta' < 'zeta' byte-wise): {nodes:?}"
    );

    // Removing the smallest variant's reference relabels the ghost —
    // deterministically, from the surviving variants.
    session.save_text("a.md", "no more link", None).unwrap();
    assert_matches_rebuild(&session, "variant removal relabel");
    let nodes = built_nodes(&session);
    assert!(
        nodes.contains(&(ghost("zeta"), NodeKind::Ghost, "zeta".to_string())),
        "label falls to the next-smallest surviving variant: {nodes:?}"
    );
}

#[test]
fn permutation_of_insertion_order_yields_identical_graph() {
    // Same file set, two creation orders (=> different file ids), one
    // graph. Ghost labels included: the lexicographic-min rule is
    // insertion-order independent by construction.
    let files: Vec<(&str, &str)> = vec![
        ("a.md", "[[b]] [[Ghost One]] ![[pic.png]]"),
        ("b.md", "[[a]] [[ghost one]]"),
        ("c.md", "[[a]] [[b]] [[c]]"),
        ("pic.png", "png"),
    ];
    let (_t1, s1) = make_vault(|p| {
        for (path, body) in &files {
            p.write_file(path, body.as_bytes()).unwrap();
        }
    });
    s1.scan_initial(&CancelToken::new()).unwrap();

    let (t2, s2) = make_vault(|_| {});
    // Reverse creation order through the session API (distinct ids +
    // distinct hook sequence), then scan to index the attachment.
    let mut rev = files.clone();
    rev.reverse();
    for (path, body) in &rev {
        if path.ends_with(".md") {
            s2.create_exclusive(path, body).unwrap();
        }
    }
    // Attachment arrives on disk after the notes.
    std::fs::write(t2.path().join("pic.png"), b"png").unwrap();
    s2.scan_initial(&CancelToken::new()).unwrap();

    let n1 = s1.with_graph(|g| g.canonical_nodes()).unwrap();
    let n2 = s2.with_graph(|g| g.canonical_nodes()).unwrap();
    assert_eq!(n1, n2, "node sets must be insertion-order invariant");
    let e1 = s1.with_graph(|g| g.canonical_edges()).unwrap();
    let e2 = s2.with_graph(|g| g.canonical_edges()).unwrap();
    assert_eq!(e1, e2, "edge sets must be insertion-order invariant");
}

#[test]
fn counts_match_direct_sql_aggregation() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[b]] [[b]] ![[b]] [[nope]] [e](https://e.com)")
            .unwrap();
        p.write_file("b.md", b"[[a]]").unwrap();
        p.write_file("pic.png", b"x").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let (nodes, edge_refs) = session
        .with_graph(|g| {
            (
                g.node_count(),
                g.canonical_edges()
                    .iter()
                    .map(|(_, _, _, c, _)| *c as u64)
                    .sum::<u64>(),
            )
        })
        .unwrap();

    // Nodes = files + distinct ghost keys among internal-unresolved
    // rows; edge references = internal links rows (external excluded).
    // Tests are session sub-modules, so the private conn is reachable.
    let (file_count, internal_rows, ghost_keys): (i64, i64, i64) = {
        let conn = session.conn.lock().unwrap();
        let files: i64 = conn
            .query_row("SELECT COUNT(*) FROM files", [], |r| r.get(0))
            .unwrap();
        let internal: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM links WHERE is_external = 0",
                [],
                |r| r.get(0),
            )
            .unwrap();
        // SQL lower() is ASCII-only, which suffices for this fixture's
        // ASCII ghost names; the Rust-side ghost_key does the real
        // Unicode folding.
        let ghosts: i64 = conn
            .query_row(
                "SELECT COUNT(DISTINCT lower(trim(target_raw))) FROM links
                 WHERE is_external = 0 AND target_path IS NULL",
                [],
                |r| r.get(0),
            )
            .unwrap();
        (files, internal, ghosts)
    };
    assert_eq!(nodes as i64, file_count + ghost_keys);
    assert_eq!(edge_refs as i64, internal_rows);
}

// --- property tests (links_roundtrip.rs pattern, graph flavor) ----------

mod properties {
    use super::*;
    use proptest::prelude::*;

    /// One generated note: a file stem plus the stems it references.
    /// Stems are drawn from a small pool so links resolve, dangle, and
    /// collide (parallel references, shared ghosts) with high
    /// probability. `embed_mask` bit i makes reference i an embed.
    #[derive(Debug, Clone)]
    struct NoteSpec {
        stem: usize,
        refs: Vec<usize>,
        embed_mask: u32,
    }

    fn note_strategy() -> impl Strategy<Value = NoteSpec> {
        (
            0usize..8,
            proptest::collection::vec(0usize..12, 0..6),
            any::<u32>(),
        )
            .prop_map(|(stem, refs, embed_mask)| NoteSpec {
                stem,
                refs,
                embed_mask,
            })
    }

    fn body(spec: &NoteSpec) -> String {
        let mut out = String::new();
        for (i, r) in spec.refs.iter().enumerate() {
            let embed = spec.embed_mask & (1 << (i % 32)) != 0;
            if embed {
                out.push_str(&format!("![[note{r}]] "));
            } else {
                out.push_str(&format!("[[note{r}]] "));
            }
        }
        out
    }

    /// Dedup by stem, keeping the LAST spec per stem (mirrors "the
    /// file's final contents win").
    fn dedup(notes: Vec<NoteSpec>) -> Vec<NoteSpec> {
        let mut by_stem: std::collections::BTreeMap<usize, NoteSpec> =
            std::collections::BTreeMap::new();
        for n in notes {
            by_stem.insert(n.stem, n);
        }
        by_stem.into_values().collect()
    }

    proptest! {
        #![proptest_config(ProptestConfig { cases: 24, ..ProptestConfig::default() })]

        /// Random vault: build's node/edge totals match direct SQL
        /// aggregation, and a fully incremental construction (create
        /// files one-by-one with the index LIVE) deep-equals the
        /// rebuild at the end.
        #[test]
        fn random_vault_counts_and_incremental_match(notes in proptest::collection::vec(note_strategy(), 1..10)) {
            let notes = dedup(notes);
            let (_tmp, session) = make_vault(|_| {});
            session.scan_initial(&CancelToken::new()).unwrap();
            let _ = built_nodes(&session); // index live from the start

            for n in &notes {
                session
                    .create_exclusive(&format!("note{}.md", n.stem), &body(n))
                    .unwrap();
                assert_matches_rebuild(&session, "proptest create");
            }
            // A scan re-resolves ghost rows whose targets materialized
            // later in the sequence.
            session.scan_initial(&CancelToken::new()).unwrap();
            assert_matches_rebuild(&session, "proptest post-create scan");

            // Totals vs SQL.
            let (nodes, edge_refs) = session
                .with_graph(|g| {
                    (
                        g.node_count() as i64,
                        g.canonical_edges().iter().map(|(_, _, _, c, _)| *c as i64).sum::<i64>(),
                    )
                })
                .unwrap();
            let (files, internal, ghosts): (i64, i64, i64) = {
                let conn = session.conn.lock().unwrap();
                (
                    conn.query_row("SELECT COUNT(*) FROM files", [], |r| r.get(0)).unwrap(),
                    conn.query_row("SELECT COUNT(*) FROM links WHERE is_external = 0", [], |r| r.get(0)).unwrap(),
                    conn.query_row(
                        "SELECT COUNT(DISTINCT lower(trim(target_raw))) FROM links
                         WHERE is_external = 0 AND target_path IS NULL",
                        [],
                        |r| r.get(0),
                    ).unwrap(),
                )
            };
            prop_assert_eq!(nodes, files + ghosts);
            prop_assert_eq!(edge_refs, internal);
        }

        /// Permutation of file-insertion order yields identical sorted
        /// node/edge lists (§P-C).
        #[test]
        fn insertion_order_permutation_invariance(
            notes in proptest::collection::vec(note_strategy(), 2..8),
            seed in any::<u64>(),
        ) {
            let notes = dedup(notes);
            let mut shuffled = notes.clone();
            // Deterministic Fisher-Yates off the proptest-provided seed.
            let mut state = seed | 1;
            for i in (1..shuffled.len()).rev() {
                state = state.wrapping_mul(6364136223846793005).wrapping_add(1442695040888963407);
                let j = (state >> 33) as usize % (i + 1);
                shuffled.swap(i, j);
            }

            let build = |ordered: &[NoteSpec]| {
                let (tmp, session) = make_vault(|_| {});
                for n in ordered {
                    session
                        .create_exclusive(&format!("note{}.md", n.stem), &body(n))
                        .unwrap();
                }
                session.scan_initial(&CancelToken::new()).unwrap();
                let nodes = session.with_graph(|g| g.canonical_nodes()).unwrap();
                let edges = session.with_graph(|g| g.canonical_edges()).unwrap();
                drop(tmp);
                (nodes, edges)
            };
            let (n1, e1) = build(&notes);
            let (n2, e2) = build(&shuffled);
            prop_assert_eq!(n1, n2);
            prop_assert_eq!(e1, e2);
        }
    }
}

// --- P0-2: metrics (#551) ------------------------------------------------

mod metrics {
    use super::*;

    #[test]
    fn golden_metrics_on_the_p0_1_fixture() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file(
                "a.md",
                b"[[b]] and [[b]] again, embed ![[b]], ghost [[Missing Note]], \
                  image ![[pic.png]], external [x](https://example.com)",
            )
            .unwrap();
            p.write_file("b.md", b"back to [[a]]").unwrap();
            p.write_file("pic.png", b"\x89PNG").unwrap();
            p.write_file("loner.md", b"no links here").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let m = session.graph_metrics_snapshot().unwrap();
        assert_eq!(m.note_count, 3);
        assert_eq!(m.attachment_count, 1);
        assert_eq!(m.ghost_count, 1);
        assert_eq!(m.edge_count, 6); // 2+1 to b, 1 pic, 1 ghost, 1 back
        assert_eq!(m.orphan_count, 1); // loner.md
        assert_eq!(m.component_count, 2); // {a,b,pic,ghost} + {loner}

        let a = m.get(&path_key("a.md")).unwrap();
        assert_eq!(
            (a.in_links, a.out_links, a.in_embeds, a.out_embeds),
            (1, 3, 0, 2),
            "degrees are reference-distinct sums of counts"
        );
        assert!(!a.is_orphan);

        let b = m.get(&path_key("b.md")).unwrap();
        // [[b]] x2 are Link-kind; ![[b]] lands in in_embeds.
        assert_eq!(
            (b.in_links, b.out_links, b.in_embeds, b.out_embeds),
            (2, 1, 1, 0)
        );

        let pic = m.get(&path_key("pic.png")).unwrap();
        assert_eq!((pic.in_links, pic.in_embeds), (0, 1));
        assert!(!pic.is_orphan, "attachments are never orphans");

        let loner = m.get(&path_key("loner.md")).unwrap();
        assert!(loner.is_orphan);
        assert_ne!(a.component, loner.component);
        assert_eq!(a.component, b.component);

        // PageRank: sums to 1 within 1e-9 across every node.
        let sum: f64 = [
            path_key("a.md"),
            path_key("b.md"),
            path_key("pic.png"),
            path_key("loner.md"),
            ghost("missing note"),
        ]
        .iter()
        .map(|k| m.get(k).unwrap().pagerank)
        .sum();
        assert!((sum - 1.0).abs() < 1e-9, "pagerank sum {sum}");
    }

    #[test]
    fn orphan_iff_zero_link_degree_embeds_dont_rescue() {
        let (_tmp, session) = make_vault(|p| {
            // embedded-only note: embeds don't rescue.
            p.write_file("a.md", b"![[b]]").unwrap();
            p.write_file("b.md", b"").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let m = session.graph_metrics_snapshot().unwrap();
        let a = m.get(&path_key("a.md")).unwrap();
        let b = m.get(&path_key("b.md")).unwrap();
        assert!(a.is_orphan, "embed-only out-degree doesn't rescue");
        assert!(b.is_orphan, "embed-only in-degree doesn't rescue");
        assert_eq!(m.orphan_count, 2);
        // ...but the pair still shares a component via the embed edge.
        assert_eq!(a.component, b.component);
    }

    #[test]
    fn component_labels_are_permutation_invariant_and_pagerank_bit_identical() {
        let files: Vec<(&str, &str)> = vec![
            ("a.md", "[[b]]"),
            ("b.md", "[[a]]"),
            ("c.md", "[[d]] [[ghosty]]"),
            ("d.md", ""),
            ("e.md", ""),
        ];
        let snapshot = |ordered: &[(&str, &str)]| {
            let (tmp, session) = make_vault(|_| {});
            for (path, body) in ordered {
                session.create_exclusive(path, body).unwrap();
            }
            session.scan_initial(&CancelToken::new()).unwrap();
            let m = session.graph_metrics_snapshot().unwrap();
            drop(tmp);
            m
        };
        let m1 = snapshot(&files);
        let mut rev = files.clone();
        rev.reverse();
        let m2 = snapshot(&rev);

        for key in [
            path_key("a.md"),
            path_key("b.md"),
            path_key("c.md"),
            path_key("d.md"),
            path_key("e.md"),
            ghost("ghosty"),
        ] {
            let x = m1.get(&key).unwrap();
            let y = m2.get(&key).unwrap();
            assert_eq!(x.component, y.component, "component label for {key:?}");
            assert_eq!(
                x.pagerank.to_bits(),
                y.pagerank.to_bits(),
                "pagerank must be bit-identical for {key:?}"
            );
        }
        assert_eq!(m1.component_count, m2.component_count);
        // Deterministic labeling rule: components ordered by their
        // lexicographically-smallest member key. {a,b} < {c,d,ghosty} < {e}.
        assert_eq!(m1.get(&path_key("a.md")).unwrap().component, 0);
        assert_eq!(m1.get(&path_key("c.md")).unwrap().component, 1);
        assert_eq!(m1.get(&path_key("e.md")).unwrap().component, 2);
    }

    #[test]
    fn delete_then_measure_survives_index_holes() {
        // The petgraph page_rank-on-holes hazard class: metrics after
        // node removals must equal metrics on a fresh build.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"[[b]] [[c]]").unwrap();
            p.write_file("b.md", b"[[c]]").unwrap();
            p.write_file("c.md", b"[[a]]").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let _ = session.graph_metrics_snapshot().unwrap(); // build + warm cache

        session.delete_file("b.md").unwrap();
        assert_matches_rebuild(&session, "delete before metrics");
        let live = session.graph_metrics_snapshot().unwrap();
        let fresh_index = session.graph_rebuild_reference().unwrap();
        let fresh = crate::graph_metrics::MetricsSnapshot::compute(&fresh_index);
        assert_eq!(
            *live, fresh,
            "metrics with holes must equal fresh-build metrics"
        );
    }

    #[test]
    fn metrics_cache_invalidates_on_generation_change() {
        let (_tmp, session) = make_vault(|p| {
            p.write_file("a.md", b"[[b]]").unwrap();
            p.write_file("b.md", b"").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let m1 = session.graph_metrics_snapshot().unwrap();
        let m1_again = session.graph_metrics_snapshot().unwrap();
        assert!(
            std::sync::Arc::ptr_eq(&m1, &m1_again),
            "same generation must hit the cache"
        );

        session.save_text("a.md", "[[b]] [[b]]", None).unwrap();
        let m2 = session.graph_metrics_snapshot().unwrap();
        assert!(
            !std::sync::Arc::ptr_eq(&m1, &m2),
            "generation bump must recompute"
        );
        assert_eq!(m2.get(&path_key("b.md")).unwrap().in_links, 2);
    }
}
