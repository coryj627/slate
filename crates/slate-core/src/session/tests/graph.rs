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

// --- P0-3: query surface (#552) -------------------------------------------

mod surface {
    use super::*;
    use crate::graph::GraphFilter;

    fn fixture() -> (tempfile::TempDir, VaultSession) {
        let (tmp, session) = make_vault(|p| {
            p.write_file(
                "a.md",
                b"[[b]] and [[b]] again, embed ![[b]], ghost [[Missing Note]], \
                  image ![[pic.png]], external [x](https://example.com)",
            )
            .unwrap();
            p.write_file("b.md", b"back to [[a]]").unwrap();
            p.write_file("pic.png", b"\x89PNG").unwrap();
            p.write_file("loner.md", b"nothing").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        (tmp, session)
    }

    #[test]
    fn snapshot_default_filter_shape_and_summary() {
        let (_tmp, session) = fixture();
        let snap = session.graph_snapshot(GraphFilter::default()).unwrap();

        // Defaults: attachments OFF (pic.png and its edge drop),
        // ghosts ON.
        let labels: Vec<&str> = snap.nodes.iter().map(|n| n.label.as_str()).collect();
        assert_eq!(labels, vec!["a", "b", "loner", "Missing Note"]);
        // a->b Link(2), a->b Embed(1), a->ghost Link(1), b->a Link(1);
        // the pic.png embed dropped with its excluded node.
        assert_eq!(snap.edges.len(), 4);
        // Summary counts describe the FILTERED payload; 5 references
        // survive (2+1 to b, 1 ghost, 1 back).
        assert_eq!(
            snap.audio_summary,
            "3 notes, 5 links. 1 orphans, 1 unresolved targets."
        );
        let ghost_node = snap.nodes.iter().find(|n| n.path.is_none()).unwrap();
        assert!(matches!(ghost_node.kind, crate::graph::NodeKind::Ghost));
        assert!(ghost_node.modified_ms.is_none());
        let a = snap.nodes.iter().find(|n| n.label == "a").unwrap();
        assert!(a.modified_ms.is_some(), "real files carry mtime");
        assert!(a.pagerank > 0.0);
    }

    #[test]
    fn snapshot_edge_count_expectation_pinned() {
        let (_tmp, session) = fixture();
        let snap = session.graph_snapshot(GraphFilter::default()).unwrap();
        // Collapsed edges under the default filter: a->b (Link,2),
        // a->b (Embed,1), a->ghost (Link,1), b->a (Link,1). The
        // pic.png embed dropped with its node.
        assert_eq!(snap.edges.len(), 4);
        let refs: u64 = snap.edges.iter().map(|e| u64::from(e.count)).sum();
        assert_eq!(refs, 5);
    }

    #[test]
    fn filters_compose_attachments_ghosts_orphans() {
        let (_tmp, session) = fixture();

        let with_attachments = session
            .graph_snapshot(GraphFilter {
                include_attachments: true,
                ..GraphFilter::default()
            })
            .unwrap();
        assert!(
            with_attachments.nodes.iter().any(|n| n.label == "pic.png"),
            "attachments included on demand"
        );
        assert!(
            with_attachments.audio_summary.ends_with(" Filtered."),
            "non-default filter appends ' Filtered.': {}",
            with_attachments.audio_summary
        );

        let no_ghosts = session
            .graph_snapshot(GraphFilter {
                include_ghosts: false,
                ..GraphFilter::default()
            })
            .unwrap();
        assert!(
            no_ghosts.nodes.iter().all(|n| n.path.is_some()),
            "ghost nodes drop with their incident edges"
        );
        let refs: u64 = no_ghosts.edges.iter().map(|e| u64::from(e.count)).sum();
        assert_eq!(refs, 4, "ghost edge dropped");

        let orphans = session
            .graph_snapshot(GraphFilter {
                orphans_only: true,
                ..GraphFilter::default()
            })
            .unwrap();
        let labels: Vec<&str> = orphans.nodes.iter().map(|n| n.label.as_str()).collect();
        assert_eq!(
            labels,
            vec!["loner"],
            "orphans_only keeps orphan notes only"
        );
        assert!(orphans.edges.is_empty());
        assert_eq!(
            orphans.audio_summary,
            "1 notes, 0 links. 1 orphans, 0 unresolved targets. Filtered."
        );
    }

    #[test]
    fn neighborhood_depth_clamp_and_traversal_gating() {
        // Chain: left -> mid.png <- right (embeds through an
        // attachment). With attachments on, right is 2 away from
        // left THROUGH mid; with attachments off, unreachable.
        let (_tmp, session) = make_vault(|p| {
            p.write_file("left.md", b"![[mid.png]]").unwrap();
            p.write_file("right.md", b"![[mid.png]]").unwrap();
            p.write_file("mid.png", b"x").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();

        let through = session
            .graph_neighborhood(
                "left.md",
                2,
                GraphFilter {
                    include_attachments: true,
                    ..GraphFilter::default()
                },
            )
            .unwrap();
        assert_eq!(through.nodes.len(), 3);

        let gated = session
            .graph_neighborhood("left.md", 2, GraphFilter::default())
            .unwrap();
        assert_eq!(
            gated.nodes.len(),
            1,
            "filter applies BEFORE traversal — excluded nodes are never walked through"
        );

        // Depth clamps into 1..=3 (0 -> 1, 99 -> 3).
        let clamped_low = session
            .graph_neighborhood(
                "left.md",
                0,
                GraphFilter {
                    include_attachments: true,
                    ..GraphFilter::default()
                },
            )
            .unwrap();
        assert_eq!(clamped_low.depth, 1);
        assert_eq!(clamped_low.nodes.len(), 2, "depth 1 reaches mid only");
        let clamped_high = session
            .graph_neighborhood("left.md", 99, GraphFilter::default())
            .unwrap();
        assert_eq!(clamped_high.depth, 3);
    }

    #[test]
    fn neighborhood_summary_verbatim_and_unknown_path_errors() {
        let (_tmp, session) = fixture();
        let hood = session
            .graph_neighborhood("b.md", 1, GraphFilter::default())
            .unwrap();
        assert_eq!(
            hood.audio_summary,
            "b: 2 links in, 1 links out. Showing 2 notes within 1 links."
        );
        assert_eq!(
            hood.center_id,
            session
                .graph_snapshot(GraphFilter::default())
                .unwrap()
                .nodes
                .iter()
                .find(|n| n.label == "b")
                .unwrap()
                .id
        );

        let err = session
            .graph_neighborhood("nope.md", 1, GraphFilter::default())
            .unwrap_err();
        assert!(matches!(err, VaultError::InvalidPath { .. }));
    }

    #[test]
    fn generation_probe_is_zero_cold_and_bumps_per_batch() {
        let (_tmp, session) = fixture();
        assert_eq!(session.graph_generation(), 0, "0 before first build");
        let g0 = session
            .graph_snapshot(GraphFilter::default())
            .unwrap()
            .generation;
        session.save_text("loner.md", "[[a]]", None).unwrap();
        let g1 = session.graph_generation();
        assert_eq!(g1, g0 + 1, "one bump per applied batch");
        // Non-mutating queries don't bump.
        let _ = session.graph_snapshot(GraphFilter::default()).unwrap();
        assert_eq!(session.graph_generation(), g1);
    }
}

// --- P0-4: censuses (#553) — the Wave-1 gate ------------------------------

/// Adversarial censuses per the standing methodology: random walks plus
/// an exhaustive small-vault sweep, `SLATE_CENSUS_FULL=1` (release) as
/// the pre-push confirmation scale.
mod census {
    use super::*;
    use crate::graph::GraphFilter;

    fn census_scale() -> u64 {
        if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
            300
        } else {
            60
        }
    }

    struct SplitMix64(u64);
    impl SplitMix64 {
        fn next(&mut self) -> u64 {
            self.0 = self.0.wrapping_add(0x9E37_79B9_7F4A_7C15);
            let mut z = self.0;
            z = (z ^ (z >> 30)).wrapping_mul(0xBF58_476D_1CE4_E5B9);
            z = (z ^ (z >> 27)).wrapping_mul(0x94D0_49BB_1331_11EB);
            z ^ (z >> 31)
        }
        fn below(&mut self, n: usize) -> usize {
            (self.next() % n.max(1) as u64) as usize
        }
    }

    /// Name pool: small enough that collisions (parallel references,
    /// shared ghosts, materialize-a-ghost) happen constantly.
    fn stem(i: usize) -> String {
        format!("note{i}")
    }

    fn random_body(rng: &mut SplitMix64) -> String {
        let mut body = String::from("# body\n");
        for _ in 0..rng.below(6) {
            let target = stem(rng.below(14)); // half the pool never exists
            match rng.below(5) {
                0 => body.push_str(&format!("![[{target}]] ")),
                1 => body.push_str(&format!("[[{target}]] [[{target}]] ")), // parallel
                2 => body.push_str(&format!("[[dir/{target}]] ")),
                3 => body.push_str("![[pic.png]] "),
                _ => body.push_str(&format!("[[{target}]] ")),
            }
        }
        body
    }

    fn existing_md(session: &VaultSession) -> Vec<String> {
        let conn = session.conn.lock().unwrap();
        let mut stmt = conn
            .prepare("SELECT path FROM files WHERE is_markdown = 1 ORDER BY path")
            .unwrap();
        let rows = stmt.query_map([], |r| r.get::<_, String>(0)).unwrap();
        rows.map(|r| r.unwrap()).collect()
    }

    /// One random op over the real session APIs. Collisions and
    /// missing targets are expected — errors that the API surfaces
    /// (DestinationExists etc.) are fine; what matters is that after
    /// every SUCCESSFUL mutation the incremental graph still equals a
    /// rebuild.
    fn apply_random_op(
        session: &VaultSession,
        root: &std::path::Path,
        rng: &mut SplitMix64,
    ) -> &'static str {
        let files = existing_md(session);
        match rng.below(11) {
            0 => {
                let path = format!("note{}.md", rng.below(14));
                let _ = session.create_exclusive(&path, &random_body(rng));
                "create"
            }
            1 => {
                if let Some(path) = files.get(rng.below(files.len().max(1))) {
                    let _ = session.save_text(path, &random_body(rng), None);
                }
                "edit-links"
            }
            2 => {
                if let Some(path) = files.get(rng.below(files.len().max(1))) {
                    let _ = session.rename_file(path, &format!("note{}.md", rng.below(14)));
                }
                "rename"
            }
            3 => {
                // Ensure the destination folder can exist so moves
                // genuinely succeed some of the time (review round 1
                // finding 4: silent-failure ops are no coverage).
                let _ = session.create_folder("dir");
                if let Some(path) = files.get(rng.below(files.len().max(1))) {
                    let dest = if rng.below(2) == 0 { "dir" } else { "" };
                    let _ = session.move_file(path, dest);
                }
                "move"
            }
            4 => {
                if let Some(path) = files.get(rng.below(files.len().max(1))) {
                    let _ = session.delete_file(path);
                }
                "delete"
            }
            5 => {
                // Materialize a ghost by name, then scan so SQLite
                // re-resolves (create alone must NOT merge the ghost —
                // the graph replays SQLite).
                let path = format!("note{}.md", rng.below(14));
                let _ = session.create_exclusive(&path, "materialized");
                let _ = session.scan_initial(&CancelToken::new());
                "materialize+scan"
            }
            6 => {
                // Attachment arrives out-of-band; scan indexes it.
                let _ = std::fs::write(root.join("pic.png"), b"png");
                let _ = session.scan_initial(&CancelToken::new());
                "attachment+scan"
            }
            7 => {
                // Folder mutations: rename the populated folder away
                // and back, or delete it outright.
                match rng.below(3) {
                    0 => {
                        let _ = session.rename_folder("dir", "dir2");
                    }
                    1 => {
                        let _ = session.rename_folder("dir2", "dir");
                    }
                    _ => {
                        let _ = session.delete_folder("dir");
                    }
                }
                "folder-op"
            }
            8 => {
                // Markdown -> .base reclassification rename (purges
                // text derivatives through the reclassification path).
                if let Some(path) = files.get(rng.below(files.len().max(1))) {
                    let stem = path.rsplit('/').next().unwrap().trim_end_matches(".md");
                    let _ = session.rename_file(path, &format!("{stem}.base"));
                }
                "reclassify"
            }
            9 => {
                // Open-before-scan heals, both flavors (each inserts a
                // files row outside scan/save — distinct hooks).
                if rng.below(2) == 0 {
                    let name = format!("board{}.canvas", rng.below(3));
                    let _ = std::fs::write(root.join(&name), r#"{"nodes":[],"edges":[]}"#);
                    let _ = session.open_canvas(&name);
                } else {
                    let name = format!("view{}.base", rng.below(3));
                    let _ = std::fs::write(
                        root.join(&name),
                        "filters:\n  and: []\nviews:\n  - type: table\n    name: All\n",
                    );
                    let _ = session.open_base(&name);
                }
                "open-heal"
            }
            _ => {
                // Out-of-band delete; the scan reconcile prunes it.
                if let Some(path) = files.get(rng.below(files.len().max(1))) {
                    let _ = std::fs::remove_file(root.join(path));
                    let _ = session.scan_initial(&CancelToken::new());
                }
                "external-delete+scan"
            }
        }
    }

    /// Census 1a: adversarial random walk — after EVERY op the
    /// incrementally-maintained index deep-equals a fresh build, and
    /// the incremental path never went defensive.
    #[test]
    fn census_graph_matches_rebuild() {
        for seed in 0..4u64 {
            let mut rng = SplitMix64(0xC0FFEE ^ seed);
            let (tmp, session) = make_vault(|p| {
                p.write_file("note0.md", b"[[note1]] ![[note2]]").unwrap();
                p.write_file("note1.md", b"[[note0]]").unwrap();
            });
            session.scan_initial(&CancelToken::new()).unwrap();
            let _ = session.with_graph(|g| g.node_count()).unwrap(); // go live

            for op_index in 0..census_scale() {
                let op = apply_random_op(&session, tmp.path(), &mut rng);
                assert!(
                    session.graph_is_built(),
                    "seed {seed} op {op_index} ({op}): incremental path went defensive"
                );
                let fresh = session.graph_rebuild_reference().unwrap();
                let equal = session.with_graph(|g| g.deep_equals(&fresh)).unwrap();
                assert!(
                    equal,
                    "seed {seed} op {op_index} ({op}): incremental graph diverged from rebuild"
                );
            }
        }
    }

    /// Census 1b: exhaustive op-pair sweep over a 4-file vault — every
    /// ordered pair of templated ops, fresh vault per pair, checked
    /// after each op.
    #[test]
    fn census_graph_matches_rebuild_exhaustive_pairs() {
        type Op = (&'static str, fn(&VaultSession, &std::path::Path));
        let ops: Vec<Op> = vec![
            ("create-linked", |s, _| {
                let _ = s.create_exclusive("note9.md", "[[note0]] [[ghosty]] ![[note1]]");
            }),
            ("edit-links", |s, _| {
                let _ = s.save_text("note0.md", "[[note2]] [[note2]] [[GHOSTY]]", None);
            }),
            ("empty-links", |s, _| {
                let _ = s.save_text("note1.md", "plain text", None);
            }),
            ("rename", |s, _| {
                let _ = s.rename_file("note2.md", "ghosty.md");
            }),
            ("move", |s, _| {
                let _ = s.move_file("note0.md", "dir");
            }),
            ("delete", |s, _| {
                let _ = s.delete_file("note1.md");
            }),
            ("recreate-deleted", |s, _| {
                let _ = s.delete_file("note0.md");
                let _ = s.create_exclusive("note0.md", "reborn [[note3]]");
            }),
            ("materialize+scan", |s, _| {
                let _ = s.create_exclusive("ghosty.md", "arrived");
                let _ = s.scan_initial(&CancelToken::new());
            }),
            ("external-delete+scan", |s, root| {
                let _ = std::fs::remove_file(root.join("note3.md"));
                let _ = s.scan_initial(&CancelToken::new());
            }),
            ("folder-populate", |s, _| {
                let _ = s.create_folder("dir");
                let _ = s.move_file("note0.md", "dir");
            }),
            ("folder-rename", |s, _| {
                let _ = s.create_folder("dir");
                let _ = s.move_file("note1.md", "dir");
                let _ = s.rename_folder("dir", "dir2");
            }),
            ("folder-delete", |s, _| {
                let _ = s.create_folder("dir");
                let _ = s.move_file("note2.md", "dir");
                let _ = s.delete_folder("dir");
            }),
            ("reclassify-to-base", |s, _| {
                let _ = s.rename_file("note2.md", "note2.base");
            }),
            ("canvas-heal", |s, root| {
                let _ = std::fs::write(root.join("board.canvas"), r#"{"nodes":[],"edges":[]}"#);
                let _ = s.open_canvas("board.canvas");
            }),
            ("base-heal", |s, root| {
                let _ = std::fs::write(
                    root.join("view.base"),
                    "filters:\n  and: []\nviews:\n  - type: table\n    name: All\n",
                );
                let _ = s.open_base("view.base");
            }),
        ];

        for (i, (name_a, op_a)) in ops.iter().enumerate() {
            for (j, (name_b, op_b)) in ops.iter().enumerate() {
                let (tmp, session) = make_vault(|p| {
                    p.write_file("note0.md", b"[[note1]] [[ghosty]]").unwrap();
                    p.write_file("note1.md", b"![[note0]] [[note3]]").unwrap();
                    p.write_file("note2.md", b"[[note0]] [[note2]]").unwrap();
                    p.write_file("note3.md", b"[[ghosty]]").unwrap();
                });
                session.scan_initial(&CancelToken::new()).unwrap();
                let _ = session.with_graph(|g| g.node_count()).unwrap();

                for (step, (name, op)) in [(name_a, op_a), (name_b, op_b)].into_iter().enumerate() {
                    op(&session, tmp.path());
                    assert!(
                        session.graph_is_built(),
                        "pair ({i},{j}) step {step} ({name}): went defensive"
                    );
                    let fresh = session.graph_rebuild_reference().unwrap();
                    let equal = session.with_graph(|g| g.deep_equals(&fresh)).unwrap();
                    assert!(equal, "pair ({i},{j}) step {step} ({name}): diverged");
                }
            }
        }
    }

    /// Census 2: same file set, shuffled insertion orders — identical
    /// sorted node/edge/metric lists.
    #[test]
    fn census_graph_permutation_invariance() {
        let mut rng = SplitMix64(0xDECAF);
        let base: Vec<(String, String)> = (0..8)
            .map(|i| {
                (format!("note{i}.md"), {
                    let mut rng_local = SplitMix64(0xABCD + i as u64);
                    random_body(&mut rng_local)
                })
            })
            .collect();

        let build = |ordered: &[(String, String)]| {
            let (tmp, session) = make_vault(|_| {});
            session.scan_initial(&CancelToken::new()).unwrap();
            // Build the index BEFORE the creates so every op below
            // exercises the INCREMENTAL path — a cold-graph run would
            // only test build-order invariance (review round 1
            // finding 4).
            let _ = session.with_graph(|g| g.node_count()).unwrap();
            for (path, body) in ordered {
                session.create_exclusive(path, body).unwrap();
            }
            // Attachment + scan so ![[pic.png]] references resolve
            // in every ordering.
            std::fs::write(tmp.path().join("pic.png"), b"png").unwrap();
            session.scan_initial(&CancelToken::new()).unwrap();
            assert!(
                session.graph_is_built(),
                "permutation census must stay on the incremental path"
            );
            let nodes = session.with_graph(|g| g.canonical_nodes()).unwrap();
            let edges = session.with_graph(|g| g.canonical_edges()).unwrap();
            let metrics = session.graph_metrics_snapshot().unwrap();
            let metric_rows: Vec<_> = nodes
                .iter()
                .map(|(k, _, _)| {
                    let m = metrics.get(k).unwrap();
                    (
                        k.clone(),
                        m.in_links,
                        m.out_links,
                        m.in_embeds,
                        m.out_embeds,
                        m.component,
                        m.is_orphan,
                        m.pagerank.to_bits(),
                    )
                })
                .collect();
            drop(tmp);
            (nodes, edges, metric_rows)
        };

        let reference = build(&base);
        let shuffles = (census_scale() / 20).max(3);
        for _ in 0..shuffles {
            let mut shuffled = base.clone();
            for i in (1..shuffled.len()).rev() {
                let j = rng.below(i + 1);
                shuffled.swap(i, j);
            }
            let permuted = build(&shuffled);
            assert_eq!(reference.0, permuted.0, "node lists must match");
            assert_eq!(reference.1, permuted.1, "edge lists must match");
            assert_eq!(reference.2, permuted.2, "metric lists must match");
        }
    }

    /// Census 3: degree/orphan/component from MetricsSnapshot ≡ a
    /// naive recomputation straight from SQLite rows (independent
    /// implementation: no GraphIndex code paths).
    #[test]
    fn census_metrics_match_naive() {
        let mut rng = SplitMix64(0xFEED);
        let (tmp, session) = make_vault(|p| {
            p.write_file("note0.md", b"[[note1]]").unwrap();
        });
        session.scan_initial(&CancelToken::new()).unwrap();
        let _ = session.with_graph(|g| g.node_count()).unwrap();

        for op_index in 0..census_scale() / 2 {
            let op = apply_random_op(&session, tmp.path(), &mut rng);
            assert!(
                session.graph_is_built(),
                "op {op_index} ({op}): metrics census must stay on the \
                 incremental path, not silently rebuild"
            );
            let metrics = session.graph_metrics_snapshot().unwrap();

            // Naive recomputation from raw SQL rows.
            type NaiveFiles = Vec<(String, bool)>;
            type NaiveRows = Vec<(String, Option<String>, String, bool)>;
            let (files, rows): (NaiveFiles, NaiveRows) = {
                let conn = session.conn.lock().unwrap();
                let files = conn
                    .prepare("SELECT path, is_markdown FROM files ORDER BY path")
                    .unwrap()
                    .query_map([], |r| {
                        Ok((r.get::<_, String>(0)?, r.get::<_, i64>(1)? != 0))
                    })
                    .unwrap()
                    .map(|r| r.unwrap())
                    .collect();
                let rows = conn
                    .prepare(
                        "SELECT f.path, l.target_path, l.target_raw, l.is_embed
                         FROM links l JOIN files f ON f.id = l.source_file_id
                         WHERE l.is_external = 0",
                    )
                    .unwrap()
                    .query_map([], |r| {
                        Ok((
                            r.get::<_, String>(0)?,
                            r.get::<_, Option<String>>(1)?,
                            r.get::<_, String>(2)?,
                            r.get::<_, i64>(3)? != 0,
                        ))
                    })
                    .unwrap()
                    .map(|r| r.unwrap())
                    .collect();
                (files, rows)
            };
            let file_set: std::collections::HashSet<&str> =
                files.iter().map(|(p, _)| p.as_str()).collect();
            let node_key = |target_path: &Option<String>, raw: &str| -> crate::graph::NodeKey {
                match target_path {
                    Some(tp) if file_set.contains(tp.as_str()) => {
                        crate::graph::NodeKey::Path(tp.clone())
                    }
                    _ => crate::graph::NodeKey::Ghost(crate::graph::ghost_key(raw)),
                }
            };

            use std::collections::HashMap;
            let mut naive: HashMap<crate::graph::NodeKey, (u32, u32, u32, u32)> = HashMap::new();
            for (path, _) in &files {
                naive.insert(crate::graph::NodeKey::Path(path.clone()), (0, 0, 0, 0));
            }
            let mut adjacency: Vec<(crate::graph::NodeKey, crate::graph::NodeKey)> = Vec::new();
            for (source, target_path, raw, is_embed) in &rows {
                let source_key = crate::graph::NodeKey::Path(source.clone());
                let target_key = node_key(target_path, raw);
                naive.entry(target_key.clone()).or_insert((0, 0, 0, 0));
                adjacency.push((source_key.clone(), target_key.clone()));
                if *is_embed {
                    naive.get_mut(&source_key).unwrap().3 += 1; // out_embeds
                    naive.get_mut(&target_key).unwrap().2 += 1; // in_embeds
                } else {
                    naive.get_mut(&source_key).unwrap().1 += 1; // out_links
                    naive.get_mut(&target_key).unwrap().0 += 1; // in_links
                }
            }

            assert_eq!(
                metrics.len(),
                naive.len(),
                "op {op_index} ({op}): node universe mismatch"
            );
            for (key, (in_l, out_l, in_e, out_e)) in &naive {
                let m = metrics
                    .get(key)
                    .unwrap_or_else(|| panic!("op {op_index} ({op}): missing {key:?}"));
                assert_eq!(
                    (m.in_links, m.out_links, m.in_embeds, m.out_embeds),
                    (*in_l, *out_l, *in_e, *out_e),
                    "op {op_index} ({op}): degrees for {key:?}"
                );
                let is_md_note = matches!(key, crate::graph::NodeKey::Path(p)
                    if files.iter().any(|(fp, md)| fp == p && *md));
                assert_eq!(
                    m.is_orphan,
                    is_md_note && *in_l == 0 && *out_l == 0,
                    "op {op_index} ({op}): orphan for {key:?}"
                );
            }

            // Components: naive undirected BFS grouping compared as
            // partitions (same members together), then label rule
            // checked via smallest members.
            let mut keys: Vec<_> = naive.keys().cloned().collect();
            keys.sort();
            let pos: HashMap<_, _> = keys.iter().cloned().zip(0..).collect();
            let mut adj: Vec<Vec<usize>> = vec![Vec::new(); keys.len()];
            for (a, b) in &adjacency {
                let (pa, pb): (usize, usize) = (pos[a], pos[b]);
                adj[pa].push(pb);
                adj[pb].push(pa);
            }
            let mut comp = vec![usize::MAX; keys.len()];
            let mut next = 0usize;
            for start in 0..keys.len() {
                if comp[start] != usize::MAX {
                    continue;
                }
                let mut queue = vec![start];
                comp[start] = next;
                while let Some(v) = queue.pop() {
                    for &w in &adj[v] {
                        if comp[w] == usize::MAX {
                            comp[w] = next;
                            queue.push(w);
                        }
                    }
                }
                next += 1;
            }
            for (i, key) in keys.iter().enumerate() {
                let expected = comp[i] as u32;
                assert_eq!(
                    metrics.get(key).unwrap().component,
                    expected,
                    "op {op_index} ({op}): component label for {key:?}"
                );
            }
        }
        let _ = session.graph_snapshot(GraphFilter::default()).unwrap();
    }
}

/// `open_canvas`'s open-before-first-scan heal inserts a files row
/// outside the scan/save paths — the graph must mirror it (#550 hook
/// completeness). The sibling `open_base` heal has its own test below.
#[test]
fn open_canvas_before_scan_mirrors_the_healed_row() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[b]]").unwrap();
        p.write_file("b.md", b"").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session); // index live

    // Canvas lands on disk AFTER the scan; open_canvas heals the
    // missing files row inside its own transaction.
    std::fs::write(
        tmp.path().join("board.canvas"),
        r#"{"nodes":[],"edges":[]}"#,
    )
    .unwrap();
    session.open_canvas("board.canvas").unwrap();
    assert_matches_rebuild(&session, "open_canvas heal of an unscanned file");
    let nodes = built_nodes(&session);
    assert!(
        nodes.contains(&(
            path_key("board.canvas"),
            NodeKind::Attachment,
            "board.canvas".to_string()
        )),
        "healed canvas row becomes an Attachment node: {nodes:?}"
    );
}

/// `open_base`'s open-before-scan heal (`ensure_open_base_indexed`)
/// also inserts a files row outside scan/save — the graph must mirror
/// it (round 2 finding 1: this path was previously uncovered).
#[test]
fn open_base_before_scan_mirrors_the_healed_row() {
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[b]]").unwrap();
        p.write_file("b.md", b"").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = built_nodes(&session); // index live

    // A .base lands after the scan; open_base heals the missing files
    // row inside ensure_open_base_indexed's own transaction.
    std::fs::write(
        tmp.path().join("view.base"),
        "filters:\n  and: []\nviews:\n  - type: table\n    name: All\n",
    )
    .unwrap();
    session.open_base("view.base").unwrap();
    assert_matches_rebuild(&session, "open_base heal of an unscanned file");
    let nodes = built_nodes(&session);
    assert!(
        nodes
            .iter()
            .any(|(k, kind, _)| *k == path_key("view.base") && *kind == NodeKind::Attachment),
        "healed .base row becomes an Attachment node: {nodes:?}"
    );
}

/// Pin a file's mtime. `write(true)` matters: Windows `SetFileTime`
/// needs a write-attributes handle; a read-only open is EACCES there.
fn set_mtime(path: &std::path::Path, t: std::time::SystemTime) {
    std::fs::File::options()
        .write(true)
        .open(path)
        .unwrap()
        .set_modified(t)
        .unwrap();
}

/// The generation discriminator bumps on a genuine `modified_ms`
/// (mtime) move but NOT on a slow-path rescan that leaves both the
/// linkset and `modified_ms` untouched (review rounds 2–4). Driven
/// through the scan path with `File::set_modified`-pinned mtimes so
/// slow-path entry and the no-bump/must-bump split are deterministic,
/// not wall-clock-dependent.
#[test]
fn generation_tracks_modified_ms_not_slow_path_entry() {
    use std::time::{Duration, SystemTime};

    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"![[pic.png]]").unwrap();
        p.write_file("pic.png", b"binary").unwrap();
    });
    let pic = tmp.path().join("pic.png");
    let mtime_ms = |t: SystemTime| {
        t.duration_since(SystemTime::UNIX_EPOCH)
            .unwrap()
            .as_millis() as i64
    };
    // Pin the attachment's mtime to a fixed, distinctly-old value.
    let old = SystemTime::UNIX_EPOCH + Duration::from_secs(1_000_000);
    set_mtime(&pic, old);
    session.scan_initial(&CancelToken::new()).unwrap();
    let attachments = || {
        session
            .graph_snapshot(crate::graph::GraphFilter {
                include_attachments: true,
                ..Default::default()
            })
            .unwrap()
    };
    let g0 = attachments().generation;

    // Slow path, but modified_ms UNCHANGED: rewrite the attachment's
    // CONTENT (size differs → the fast-path (mtime,size,ctime) key
    // differs on size → slow path is entered deterministically), then
    // re-pin mtime to the same old value. A non-markdown attachment
    // has no links, so nothing structural changes and modified_ms is
    // unchanged — the generation must hold regardless of scan path.
    std::fs::write(&pic, b"different bytes, larger").unwrap();
    set_mtime(&pic, old);
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        session.graph_generation(),
        g0,
        "a slow-path rescan with unchanged modified_ms must not bump"
    );
    assert!(
        session.graph_is_built(),
        "the no-op scan must not drop the index"
    );

    // A GENUINE mtime move: the discriminator must advance and the
    // surfaced modified_ms must reflect the new mtime.
    let newer = old + Duration::from_secs(60);
    set_mtime(&pic, newer);
    session.scan_initial(&CancelToken::new()).unwrap();
    let snap = attachments();
    assert!(
        snap.generation > g0,
        "a real modified_ms change must bump the generation"
    );
    assert_eq!(
        snap.nodes
            .iter()
            .find(|n| n.label == "pic.png")
            .and_then(|n| n.modified_ms),
        Some(mtime_ms(newer)),
        "the surfaced modified_ms reflects the new mtime"
    );
}

/// A structural link change bumps the generation even when
/// `modified_ms` is held fixed, and a prose-only slow-path rescan
/// with identical links AND fixed mtime does not — the two bump
/// sources (structural vs modified_ms) are independent (review round
/// 4). Scan-path driven with a pinned mtime for determinism.
#[test]
fn structural_change_bumps_independent_of_mtime() {
    use std::time::{Duration, SystemTime};
    let (tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[b]]").unwrap();
        p.write_file("b.md", b"").unwrap();
        p.write_file("d.md", b"").unwrap();
    });
    let a = tmp.path().join("a.md");
    let old = SystemTime::UNIX_EPOCH + Duration::from_secs(2_000_000);
    set_mtime(&a, old);
    session.scan_initial(&CancelToken::new()).unwrap();
    let g0 = session
        .graph_snapshot(crate::graph::GraphFilter::default())
        .unwrap()
        .generation;

    // Prose-only change, mtime pinned: links identical, modified_ms
    // unchanged → no bump (size differs → slow path is entered, so
    // this proves the structural-no-op gate, not the fast path).
    std::fs::write(&a, b"[[b]] with extra prose that changes size").unwrap();
    set_mtime(&a, old);
    session.scan_initial(&CancelToken::new()).unwrap();
    assert_eq!(
        session.graph_generation(),
        g0,
        "prose-only rescan with identical links + fixed mtime must not bump"
    );

    // Link change, mtime STILL pinned: b → d is a real structural
    // delta, so it bumps even though modified_ms didn't move.
    std::fs::write(&a, b"[[d]] with extra prose that changes size!").unwrap();
    set_mtime(&a, old);
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(
        session.graph_generation() > g0,
        "a structural link change must bump even with mtime fixed"
    );
    let edges = built_edges(&session);
    assert!(
        edges.contains(&(path_key("a.md"), path_key("d.md"), EdgeKind::Link, 1))
            && !edges.contains(&(path_key("a.md"), path_key("b.md"), EdgeKind::Link, 1)),
        "the link actually moved b → d: {edges:?}"
    );
}

/// Oversized-file purge rides the hooks (finding 4: the census op
/// pools couldn't afford default-threshold bodies, so the purge path
/// gets its own census with a tiny refuse threshold).
#[test]
fn census_oversize_purge_matches_rebuild() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("a.md"), "[[big]] [[b]]").unwrap();
    std::fs::write(tmp.path().join("b.md"), "[[a]]").unwrap();
    std::fs::write(tmp.path().join("big.md"), "[[a]] small for now").unwrap();
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    let mut config = SessionConfig::new(tmp.path().join(".slate"));
    config.large_file_refuse_bytes = 256;
    let session = VaultSession::open(std::sync::Arc::new(provider), config).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = session.with_graph(|g| g.node_count()).unwrap(); // live

    // Grow big.md past the threshold out-of-band; the scan's slow
    // path takes the oversized branch and purges its derivatives
    // (out-edges empty; the node itself stays).
    let huge = format!("[[a]] {}", "x".repeat(512));
    std::fs::write(tmp.path().join("big.md"), &huge).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.graph_is_built(), "oversize purge went defensive");
    let fresh = session.graph_rebuild_reference().unwrap();
    assert!(
        session.with_graph(|g| g.deep_equals(&fresh)).unwrap(),
        "oversize purge diverged from rebuild"
    );
    let edges = built_edges(&session);
    assert!(
        !edges.iter().any(|(s, _, _, _)| *s == path_key("big.md")),
        "oversized file keeps its node but loses its out-edges: {edges:?}"
    );

    // Shrink back under the threshold: derivatives reindex.
    std::fs::write(tmp.path().join("big.md"), "[[a]] back").unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();
    assert!(session.graph_is_built());
    let fresh = session.graph_rebuild_reference().unwrap();
    assert!(session.with_graph(|g| g.deep_equals(&fresh)).unwrap());
}

/// Review round 1 finding 5: with the graph LIVE, a save still emits
/// exactly one Modified through the #802 seam — graph hooks stage
/// in-memory ops only and never touch the listener fan-out.
#[test]
fn live_graph_save_emits_exactly_one_modified() {
    use std::sync::Mutex as StdMutex;
    struct Recorder(StdMutex<Vec<FileChangeEvent>>);
    impl VaultEventListener for Recorder {
        fn on_error(&self, _c: EventErrorCode, _p: String, _m: String) {}
        fn on_file_change(&self, event: FileChangeEvent) {
            self.0.lock().unwrap().push(event);
        }
    }

    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"[[b]]").unwrap();
        p.write_file("b.md", b"").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let _ = session.with_graph(|g| g.node_count()).unwrap(); // hooks LIVE

    let recorder = std::sync::Arc::new(Recorder(StdMutex::new(Vec::new())));
    let token = session.register_event_listener(recorder.clone());
    session
        .save_text("a.md", "[[b]] and [[ghost]]", None)
        .unwrap();
    let events = recorder.0.lock().unwrap().clone();
    session.unregister_event_listener(token);

    assert_eq!(
        events.len(),
        1,
        "exactly one event per save with the graph live: {events:?}"
    );
    assert_eq!(events[0].kind, FileChangeKind::Modified);
    assert_eq!(events[0].path, "a.md");
}
