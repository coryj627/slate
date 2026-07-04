// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Model derivation tests (#360): fixture expectations plus the
//! adversarial censuses gating the normative reading-order/containment
//! rules. Default scale finishes in seconds; `SLATE_CENSUS_FULL=1`
//! (release mode) runs the full adversarial scale.

use std::collections::{HashMap, HashSet};

use super::*;
use crate::canvas::{EdgeId, NodeId, NodeKind, parse};

const SAMPLE: &str = include_str!("../../tests/fixtures/canvas/sample.canvas");
const GROUPS_NESTED: &str = include_str!("../../tests/fixtures/canvas/groups_nested.canvas");
const MALFORMED: &str = include_str!("../../tests/fixtures/canvas/malformed.canvas");
const LARGE: &str = include_str!("../../tests/fixtures/canvas/large_2000.canvas");

fn id(s: &str) -> NodeId {
    NodeId(s.to_string())
}

fn order_of(model: &CanvasModel) -> Vec<&str> {
    model.reading_order.iter().map(|n| n.0.as_str()).collect()
}

#[test]
fn sample_reading_order_and_tree() {
    let (canvas, _) = parse(SAMPLE);
    let model = derive(&canvas);

    assert_eq!(
        order_of(&model),
        [
            "grp-research",
            "card-question",
            "card-evidence",
            "card-notes",
            "card-spec",
            "grp-inspiration",
            "card-jsoncanvas",
            "card-diagram",
            "card-loose",
        ]
    );
    assert_eq!(
        model.tree.roots,
        [id("grp-research"), id("grp-inspiration"), id("card-loose")]
    );
    assert_eq!(model.tree.parent[&id("card-question")], id("grp-research"));
    assert_eq!(
        model.tree.parent[&id("card-diagram")],
        id("grp-inspiration")
    );
    assert!(!model.tree.parent.contains_key(&id("card-loose")));
}

#[test]
fn sample_summaries() {
    let (canvas, _) = parse(SAMPLE);
    let model = derive(&canvas);
    let s = |name: &str| &model.summaries[&id(name)];

    // t0 §1.1 title derivations.
    let q = s("card-question");
    assert_eq!(q.display_title, "Core question"); // heading marker stripped
    assert_eq!(q.kind_label, "text");
    assert_eq!(q.group_path, ["Research"]);
    assert_eq!((q.position_in_container, q.container_size), (1, 4));
    assert_eq!(q.color_name.as_deref(), Some("red"));

    assert_eq!(s("card-notes").display_title, "canvas research"); // humanized, never a raw path
    assert_eq!(
        s("card-spec").display_title,
        "interaction › Announcement grammar"
    );
    assert_eq!(s("card-jsoncanvas").display_title, "jsoncanvas.org"); // sole host: unambiguous
    assert_eq!(s("card-jsoncanvas").kind_label, "link");
    assert_eq!(
        s("card-diagram").display_title,
        "Image: architecture diagram"
    );
    assert_eq!(s("card-diagram").kind_label, "image");
    assert_eq!(s("grp-inspiration").display_title, "Inspiration");
    assert_eq!(
        s("card-loose").color_name.as_deref(),
        Some("purple (custom)")
    );

    // Root-level positional context counts all roots.
    let loose = s("card-loose");
    assert_eq!(loose.container, None);
    assert_eq!((loose.position_in_container, loose.container_size), (3, 3));

    // Connection counts (edge-q-evidence, edge-q-notes out; edge-loose in).
    let q = s("card-question");
    assert_eq!(q.connection_count, 3);
    assert_eq!((q.in_count, q.out_count), (1, 2));
    // Bidirectional counts on both sides (edge-notes-spec).
    let notes = s("card-notes");
    assert_eq!(notes.connection_count, 2);
    assert_eq!((notes.in_count, notes.out_count), (2, 1));
}

#[test]
fn sample_adjacency_directions() {
    let (canvas, _) = parse(SAMPLE);
    let model = derive(&canvas);

    let q = &model.adjacency[&id("card-question")];
    let by_edge: HashMap<&str, &Neighbor> = q.iter().map(|n| (n.edge.0.as_str(), n)).collect();

    let e = by_edge["edge-q-evidence"];
    assert_eq!(e.direction, EdgeDirection::Outgoing);
    assert_eq!(e.other, id("card-evidence"));
    assert_eq!(e.self_side, Some(crate::canvas::Side::Right));
    assert_eq!(e.other_side, Some(crate::canvas::Side::Left));
    assert_eq!(e.label.as_deref(), Some("supports"));
    assert!(e.self_is_from);

    // Same edge from the other endpoint: incoming, sides swapped.
    let ev = &model.adjacency[&id("card-evidence")];
    let e2 = ev.iter().find(|n| n.edge.0 == "edge-q-evidence").unwrap();
    assert_eq!(e2.direction, EdgeDirection::Incoming);
    assert_eq!(e2.self_side, Some(crate::canvas::Side::Left));
    assert!(!e2.self_is_from);

    // Bidirectional + undirected phrasing data.
    let notes = &model.adjacency[&id("card-notes")];
    let bidir = notes
        .iter()
        .find(|n| n.edge.0 == "edge-notes-spec")
        .unwrap();
    assert_eq!(bidir.direction, EdgeDirection::Bidirectional);
    let ev_json = &model.adjacency[&id("card-evidence")];
    let undirected = ev_json
        .iter()
        .find(|n| n.edge.0 == "edge-evidence-json")
        .unwrap();
    assert_eq!(undirected.direction, EdgeDirection::Undirected);
}

#[test]
fn nested_groups_containment_rules() {
    let (canvas, _) = parse(GROUPS_NESTED);
    let model = derive(&canvas);

    // Depth chain via smallest-area rule.
    assert_eq!(model.tree.parent[&id("in-deep")], id("deep"));
    assert_eq!(model.tree.parent[&id("deep")], id("inner-a"));
    assert_eq!(model.tree.parent[&id("inner-a")], id("outer"));
    assert_eq!(model.tree.parent[&id("in-b")], id("inner-b"));
    assert_eq!(model.tree.parent[&id("in-outer")], id("outer"));

    // Center exactly on the boundary is NOT contained (rule 1).
    assert!(!model.tree.parent.contains_key(&id("boundary")));
    assert!(!model.tree.parent.contains_key(&id("free")));

    // Ancestor path uses derived group titles (unlabeled → ordinal).
    let deep_card = &model.summaries[&id("in-deep")];
    assert_eq!(deep_card.group_path, ["Quarter", "Q3", "Week 1"]);
    let in_b = &model.summaries[&id("in-b")];
    assert_eq!(in_b.group_path.len(), 2);
    assert_eq!(in_b.group_path[0], "Quarter");
    assert!(in_b.group_path[1].starts_with("Untitled"));
}

#[test]
fn coincident_groups_are_acyclic_and_deterministic() {
    let input = r#"{
        "nodes":[
            {"id":"ga","type":"group","x":0,"y":0,"width":100,"height":100},
            {"id":"gb","type":"group","x":0,"y":0,"width":100,"height":100},
            {"id":"card","type":"text","text":"inside both","x":40,"y":40,"width":10,"height":10}
        ],
        "edges":[]
    }"#;
    let (canvas, warnings) = parse(input);
    assert!(warnings.is_empty());
    let model = derive(&canvas);

    // Equal areas → later group in document order wins for the card;
    // the earlier group nests under the later one (cycle-safe order).
    assert_eq!(model.tree.parent[&id("card")], id("gb"));
    assert_eq!(model.tree.parent[&id("ga")], id("gb"));
    assert!(!model.tree.parent.contains_key(&id("gb")));
    assert_eq!(model.reading_order.len(), 3);
}

#[test]
fn dangling_and_self_edges() {
    let (canvas, _) = parse(MALFORMED);
    let model = derive(&canvas);

    // edge-dangling points at a skipped node: absent from adjacency.
    for neighbors in model.adjacency.values() {
        assert!(neighbors.iter().all(|n| n.edge.0 != "edge-dangling"));
    }
    let g2 = &model.summaries[&id("good-2")];
    assert_eq!(g2.connection_count, 2); // edge-good in, edge-odd-side out

    // Self-edge: exactly one adjacency entry.
    let (canvas, _) = parse(
        r#"{"nodes":[{"id":"a","type":"text","text":"a","x":0,"y":0,"width":10,"height":10}],
            "edges":[{"id":"self","fromNode":"a","toNode":"a"}]}"#,
    );
    let model = derive(&canvas);
    let a = &model.adjacency[&id("a")];
    assert_eq!(a.len(), 1);
    assert_eq!(a[0].other, id("a"));
    assert_eq!(model.summaries[&id("a")].connection_count, 1);
}

#[test]
fn untitled_ordinals_follow_document_order() {
    let input = r#"{
        "nodes":[
            {"id":"t1","type":"text","text":"","x":0,"y":0,"width":10,"height":10},
            {"id":"titled","type":"text","text":"Named","x":0,"y":20,"width":10,"height":10},
            {"id":"g1","type":"group","x":500,"y":500,"width":50,"height":50},
            {"id":"t2","type":"text","text":"   \n  ","x":0,"y":40,"width":10,"height":10}
        ],
        "edges":[]
    }"#;
    let (canvas, _) = parse(input);
    let model = derive(&canvas);
    assert_eq!(model.summaries[&id("t1")].display_title, "Untitled 1");
    assert_eq!(model.summaries[&id("g1")].display_title, "Untitled 2");
    assert_eq!(model.summaries[&id("t2")].display_title, "Untitled 3");
    assert_eq!(model.summaries[&id("titled")].display_title, "Named");
}

#[test]
fn link_titles_disambiguate_shared_hosts() {
    let input = r#"{
        "nodes":[
            {"id":"l1","type":"link","url":"https://example.com/docs/intro","x":0,"y":0,"width":10,"height":10},
            {"id":"l2","type":"link","url":"https://example.com/blog?utm=1","x":0,"y":20,"width":10,"height":10},
            {"id":"l3","type":"link","url":"https://unique.dev/page","x":0,"y":40,"width":10,"height":10},
            {"id":"l4","type":"link","url":"https://user@example.com","x":0,"y":60,"width":10,"height":10}
        ],
        "edges":[]
    }"#;
    let (canvas, _) = parse(input);
    let model = derive(&canvas);
    // Shared host → host + first path segment; no segment → host alone.
    assert_eq!(model.summaries[&id("l1")].display_title, "example.com/docs");
    assert_eq!(model.summaries[&id("l2")].display_title, "example.com/blog");
    assert_eq!(model.summaries[&id("l4")].display_title, "example.com");
    // Unique host → host only; userinfo never leaks.
    assert_eq!(model.summaries[&id("l3")].display_title, "unique.dev");
}

#[test]
fn file_titles_resolve_through_source() {
    struct Titles;
    impl FileTitleSource for Titles {
        fn title_for(&self, path: &str) -> Option<String> {
            (path == "notes/canvas research.md").then(|| "Canvas Research Log".to_string())
        }
    }
    let (canvas, _) = parse(SAMPLE);
    let model = derive_with(&canvas, &Titles);
    assert_eq!(
        model.summaries[&id("card-notes")].display_title,
        "Canvas Research Log"
    );
    // Unresolved files still humanize.
    assert_eq!(
        model.summaries[&id("card-spec")].display_title,
        "interaction › Announcement grammar"
    );
}

#[test]
fn spatial_index_queries() {
    let (canvas, _) = parse(SAMPLE);
    let model = derive(&canvas);

    // Overlapping card-question's rect: itself excluded, group ignored.
    let hits = model.spatial.overlapping(
        Rect::new(0.0, 0.0, 240.0, 140.0),
        &[id("card-question")],
        false,
    );
    assert!(hits.is_empty());
    // Including groups reports the containing group frame.
    let hits = model.spatial.overlapping(
        Rect::new(0.0, 0.0, 240.0, 140.0),
        &[id("card-question")],
        true,
    );
    assert_eq!(hits, [id("grp-research")]);
    // Touching edges is not overlap (card-evidence starts at x=260... use
    // a rect that exactly abuts it at x=260).
    assert!(!model.spatial.any_overlap(
        Rect::new(240.0, 0.0, 20.0, 140.0),
        &[id("card-question")],
        false
    ));
    assert!(model.spatial.bounds().is_some());
}

#[test]
fn large_fixture_derives_with_invariants() {
    let (canvas, warnings) = parse(LARGE);
    assert!(warnings.is_empty());
    let model = derive(&canvas);
    assert_invariants(&canvas, &model);
    // Grouped cards are parented to their group.
    assert_eq!(model.tree.parent[&id("g0c0")], id("grp0"));
    assert_eq!(model.summaries[&id("g0c0")].group_path, ["Group 0"]);
}

#[test]
fn derivation_is_deterministic() {
    for input in [SAMPLE, GROUPS_NESTED, MALFORMED, LARGE] {
        let (c1, _) = parse(input);
        let (c2, _) = parse(input);
        let (m1, m2) = (derive(&c1), derive(&c2));
        assert_eq!(m1, m2);
        assert_eq!(c1, c2);
    }
}

// --- Red-team regression cases (2026-07-04 pass) --------------------------

/// Midpoint overflow: a card around ±1.7e308 must still resolve its
/// true center for containment (`(x0+x1)/2` used to overflow to inf).
#[test]
fn redteam_center_overflow_containment() {
    let input = r#"{"nodes":[
        {"id":"g","type":"group","x":0,"y":0,"width":1.6e308,"height":1.6e308},
        {"id":"c","type":"text","text":"t","x":1.0e308,"y":1.0e308,"width":0.5e308,"height":0.5e308}
    ],"edges":[]}"#;
    let (canvas, _) = parse(input);
    let model = derive(&canvas);
    assert_eq!(model.tree.parent[&id("c")], id("g"));
    assert_invariants(&canvas, &model);
}

/// Area overflow: 4e320 vs 4e400 must not tie at `inf` and fall through
/// to the document-order tiebreak — the smaller group wins.
#[test]
fn redteam_area_overflow_smallest_wins() {
    let input = r#"{"nodes":[
        {"id":"b","type":"group","x":-1e160,"y":-1e160,"width":2e160,"height":2e160},
        {"id":"a","type":"group","x":-1e200,"y":-1e200,"width":2e200,"height":2e200},
        {"id":"c","type":"text","text":"t","x":-5,"y":-5,"width":10,"height":10}
    ],"edges":[]}"#;
    let (canvas, _) = parse(input);
    let model = derive(&canvas);
    assert_eq!(model.tree.parent[&id("c")], id("b"));
    assert_eq!(model.tree.parent[&id("b")], id("a"));
    assert_invariants(&canvas, &model);
}

/// "Next free ordinal": generated placeholders skip over a card that is
/// literally titled "Untitled 2" (Voice Control name uniqueness).
#[test]
fn redteam_untitled_ordinal_skips_taken_titles() {
    let input = r#"{"nodes":[
        {"id":"b1","type":"text","text":"","x":0,"y":0,"width":10,"height":10},
        {"id":"lit","type":"text","text":"Untitled 2","x":0,"y":20,"width":10,"height":10},
        {"id":"b2","type":"text","text":"","x":0,"y":40,"width":10,"height":10}
    ],"edges":[]}"#;
    let (canvas, _) = parse(input);
    let model = derive(&canvas);
    let titles: Vec<&str> = ["b1", "lit", "b2"]
        .iter()
        .map(|n| model.summaries[&id(n)].display_title.as_str())
        .collect();
    assert_eq!(titles, ["Untitled 1", "Untitled 2", "Untitled 3"]);
    // All distinct despite the literal collision candidate.
    let unique: HashSet<&str> = titles.iter().copied().collect();
    assert_eq!(unique.len(), 3);
}

/// Degenerate file paths never produce a bare "› Heading" or empty
/// title; extensionless basenames are not media.
#[test]
fn redteam_degenerate_file_titles() {
    let input = r##"{"nodes":[
        {"id":"slash","type":"file","file":"notes/","subpath":"#Head","x":0,"y":0,"width":10,"height":10},
        {"id":"empty","type":"file","file":"","x":0,"y":20,"width":10,"height":10},
        {"id":"mov","type":"file","file":"mov","x":0,"y":40,"width":10,"height":10},
        {"id":"dot","type":"file","file":".png","x":0,"y":60,"width":10,"height":10}
    ],"edges":[]}"##;
    let (canvas, _) = parse(input);
    let model = derive(&canvas);
    // Path ends in '/': heading alone, no dangling separator.
    assert_eq!(model.summaries[&id("slash")].display_title, "Head");
    // Empty path: Untitled fallback.
    assert!(
        model.summaries[&id("empty")]
            .display_title
            .starts_with("Untitled")
    );
    // Extensionless basename literally named "mov" is a file, not video.
    assert_eq!(model.summaries[&id("mov")].display_title, "mov");
    assert_eq!(model.summaries[&id("mov")].kind_label, "file");
    // A dotfile isn't media either.
    assert_eq!(model.summaries[&id("dot")].kind_label, "file");
}

/// A self-loop counts as both incoming and outgoing.
#[test]
fn redteam_self_edge_counts_both_ways() {
    let (canvas, _) = parse(
        r#"{"nodes":[{"id":"a","type":"text","text":"a","x":0,"y":0,"width":10,"height":10}],
            "edges":[{"id":"self","fromNode":"a","toNode":"a"}]}"#,
    );
    let model = derive(&canvas);
    let s = &model.summaries[&id("a")];
    assert_eq!((s.in_count, s.out_count, s.connection_count), (1, 1, 1));
}

// --- Census machinery ----------------------------------------------------

/// Deterministic xorshift* PRNG — reproducible failures, no new deps.
struct Rng(u64);

impl Rng {
    fn new(seed: u64) -> Rng {
        Rng(seed.max(1))
    }
    fn next(&mut self) -> u64 {
        let mut x = self.0;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        self.0 = x;
        x.wrapping_mul(0x2545F4914F6CDD1D)
    }
    fn below(&mut self, n: u64) -> u64 {
        self.next() % n.max(1)
    }
    fn pick_i(&mut self, lo: i64, hi: i64) -> i64 {
        lo + self.below((hi - lo + 1) as u64) as i64
    }
}

fn census_scale() -> (u64, usize, usize) {
    // (random canvases, max size, big-canvas size)
    if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        (1500, 400, 2000)
    } else {
        (150, 120, 1200)
    }
}

/// Straight-line re-statement of the t1 containment rule, used as the
/// oracle against the production derivation. Area comparisons go
/// through `Rect::area_cmp` (the overflow/NaN-robust total order the
/// red-team pass pinned) so oracle and production agree on semantics
/// while differing in search structure.
fn oracle_parent(canvas: &Canvas, idx: usize) -> Option<NodeId> {
    use std::cmp::Ordering;
    let node = &canvas.nodes[idx];
    let node_rect = Rect::from_node(node);
    let (cx, cy) = node_rect.center();
    let node_is_group = matches!(node.kind, NodeKind::Group { .. });

    let mut best: Option<(Rect, usize)> = None;
    for (j, g) in canvas.nodes.iter().enumerate() {
        if j == idx || !matches!(g.kind, NodeKind::Group { .. }) {
            continue;
        }
        let rect = Rect::from_node(g);
        if !rect.contains_point_strict(cx, cy) {
            continue;
        }
        if node_is_group {
            let greater = match Rect::area_cmp(&rect, &node_rect) {
                Ordering::Greater => true,
                Ordering::Equal => j > idx,
                Ordering::Less => false,
            };
            if !greater {
                continue;
            }
        }
        best = match best {
            None => Some((rect, j)),
            Some((brect, bj)) => match Rect::area_cmp(&rect, &brect) {
                Ordering::Less => Some((rect, j)),
                Ordering::Equal if j > bj => Some((rect, j)),
                _ => Some((brect, bj)),
            },
        };
    }
    best.map(|(_, j)| canvas.nodes[j].id.clone())
}

fn assert_invariants(canvas: &Canvas, model: &CanvasModel) {
    let n = canvas.nodes.len();

    // 1. Every node appears in reading_order exactly once.
    assert_eq!(model.reading_order.len(), n, "reading order length");
    let unique: HashSet<&NodeId> = model.reading_order.iter().collect();
    assert_eq!(unique.len(), n, "reading order duplicates");
    for node in &canvas.nodes {
        assert!(unique.contains(&node.id), "missing {:?}", node.id);
    }

    // 2. Parenting matches the oracle rule.
    for (idx, node) in canvas.nodes.iter().enumerate() {
        assert_eq!(
            model.tree.parent.get(&node.id),
            oracle_parent(canvas, idx).as_ref(),
            "parent mismatch for {:?}",
            node.id
        );
    }

    // 3. A group precedes each of its children in reading order, and
    //    siblings are ordered by (y, x, document order).
    let pos: HashMap<&NodeId, usize> = model
        .reading_order
        .iter()
        .enumerate()
        .map(|(i, id)| (id, i))
        .collect();
    let doc: HashMap<&NodeId, usize> = canvas
        .nodes
        .iter()
        .enumerate()
        .map(|(i, node)| (&node.id, i))
        .collect();
    let check_sorted = |ids: &[NodeId]| {
        for pair in ids.windows(2) {
            let (a, b) = (&canvas.nodes[doc[&pair[0]]], &canvas.nodes[doc[&pair[1]]]);
            let ka = (a.y, a.x, doc[&pair[0]]);
            let kb = (b.y, b.x, doc[&pair[1]]);
            assert!(
                ka.0.total_cmp(&kb.0)
                    .then(ka.1.total_cmp(&kb.1))
                    .then(ka.2.cmp(&kb.2))
                    .is_lt(),
                "sibling order violated: {:?} vs {:?}",
                pair[0],
                pair[1]
            );
        }
    };
    check_sorted(&model.tree.roots);
    for (group, kids) in &model.tree.children {
        check_sorted(kids);
        for kid in kids {
            assert!(pos[group] < pos[kid], "group after child");
            assert_eq!(model.tree.parent.get(kid), Some(group));
        }
    }

    // 4. Adjacency: symmetric, dangling excluded, direction complementary.
    let node_ids: HashSet<&str> = canvas.nodes.iter().map(|nd| nd.id.0.as_str()).collect();
    let mut expected_edges: HashMap<&NodeId, Vec<&EdgeId>> = HashMap::new();
    for edge in &canvas.edges {
        let live =
            node_ids.contains(edge.from.0.0.as_str()) && node_ids.contains(edge.to.0.0.as_str());
        if !live {
            continue;
        }
        expected_edges
            .entry(&edge.from.0)
            .or_default()
            .push(&edge.id);
        if edge.from.0 != edge.to.0 {
            expected_edges.entry(&edge.to.0).or_default().push(&edge.id);
        }
    }
    for node in &canvas.nodes {
        let got: Vec<&EdgeId> = model.adjacency[&node.id]
            .iter()
            .map(|nb| &nb.edge)
            .collect();
        let want = expected_edges.remove(&node.id).unwrap_or_default();
        assert_eq!(got, want, "adjacency for {:?}", node.id);
        for nb in &model.adjacency[&node.id] {
            // The mirror entry exists and directions complement.
            if nb.other == node.id {
                continue;
            }
            let mirror = model.adjacency[&nb.other]
                .iter()
                .find(|m| m.edge == nb.edge)
                .expect("mirror neighbor");
            assert_eq!(mirror.other, node.id);
            use EdgeDirection::*;
            let ok = matches!(
                (nb.direction, mirror.direction),
                (Outgoing, Incoming)
                    | (Incoming, Outgoing)
                    | (Bidirectional, Bidirectional)
                    | (Undirected, Undirected)
            );
            assert!(ok, "direction complement {:?}", nb.edge);
        }
    }

    // 5. Summaries agree with the tree and adjacency.
    for node in &canvas.nodes {
        let s = &model.summaries[&node.id];
        let siblings = match &s.container {
            Some(g) => &model.tree.children[g],
            None => &model.tree.roots,
        };
        assert_eq!(siblings.len(), s.container_size);
        assert_eq!(siblings[s.position_in_container - 1], node.id);
        assert_eq!(s.connection_count, model.adjacency[&node.id].len());
        assert!(!s.display_title.is_empty(), "empty display title");
    }

    // 6. Determinism under re-derivation.
    assert_eq!(&derive(canvas), model);
}

/// Exhaustive small-topology census: every kind × rect-palette
/// combination for canvases of 1–4 nodes (t1: "all containment
/// topologies ≤ 4 nodes"). The palette covers nesting, overlap,
/// disjointness, coincidence, boundary contact, and zero size.
#[test]
fn census_exhaustive_small_topologies() {
    // (x, y, w, h)
    const PALETTE: &[(f64, f64, f64, f64)] = &[
        (0.0, 0.0, 100.0, 100.0),   // big
        (10.0, 10.0, 30.0, 30.0),   // nested in big
        (50.0, 50.0, 100.0, 100.0), // overlaps big
        (200.0, 0.0, 50.0, 50.0),   // disjoint
        (0.0, 0.0, 100.0, 100.0),   // coincident duplicate
        (25.0, 25.0, 0.0, 0.0),     // zero size
    ];
    const KINDS: usize = 2; // text card | group
    let configs = PALETTE.len() * KINDS;

    let mut total = 0usize;
    for count in 1..=4usize {
        let mut selector = vec![0usize; count];
        loop {
            // Build the canvas for this selector.
            let mut nodes = Vec::new();
            for (i, sel) in selector.iter().enumerate() {
                let (kind, rect_i) = (sel % KINDS, sel / KINDS);
                let (x, y, w, h) = PALETTE[rect_i];
                let node = if kind == 0 {
                    serde_json::json!({
                        "id": format!("n{i}"), "type": "text",
                        "text": format!("card {i}"),
                        "x": x, "y": y, "width": w, "height": h
                    })
                } else {
                    serde_json::json!({
                        "id": format!("n{i}"), "type": "group",
                        "x": x, "y": y, "width": w, "height": h
                    })
                };
                nodes.push(node);
            }
            let doc = serde_json::json!({ "nodes": nodes, "edges": [] }).to_string();
            let (canvas, warnings) = parse(&doc);
            assert!(warnings.is_empty(), "{doc}");
            let model = derive(&canvas);
            assert_invariants(&canvas, &model);
            total += 1;

            // Odometer increment.
            let mut i = 0;
            loop {
                selector[i] += 1;
                if selector[i] < configs {
                    break;
                }
                selector[i] = 0;
                i += 1;
                if i == count {
                    break;
                }
            }
            if selector.iter().all(|&s| s == 0) {
                break;
            }
        }
    }
    assert_eq!(total, 12 + 144 + 1728 + 20736);
}

fn random_canvas(rng: &mut Rng, size: usize) -> String {
    let mut nodes = Vec::new();
    for i in 0..size {
        // Coarse grid + small palette of sizes forces coincidences,
        // shared coordinates, containment and boundary contact.
        let x = (rng.pick_i(-20, 20) * 25) as f64;
        let y = (rng.pick_i(-20, 20) * 25) as f64;
        let (w, h) = match rng.below(6) {
            0 => (0.0, 0.0),
            1 => (50.0, 50.0),
            2 => (100.0, 75.0),
            3 => (250.0, 200.0),
            4 => (600.0, 450.0),
            _ => (-100.0, 120.0), // negative size: legal, normalized
        };
        let node = match rng.below(8) {
            0 | 1 => serde_json::json!({
                "id": format!("n{i}"), "type": "group",
                "x": x, "y": y, "width": w, "height": h,
                "label": if rng.below(3) == 0 { String::new() } else { format!("G{i}") }
            }),
            2 => serde_json::json!({
                "id": format!("n{i}"), "type": "file",
                "file": format!("notes/f{}.md", rng.below(10)),
                "x": x, "y": y, "width": w, "height": h
            }),
            3 => serde_json::json!({
                "id": format!("n{i}"), "type": "link",
                "url": format!("https://host{}.dev/p{}", rng.below(4), i),
                "x": x, "y": y, "width": w, "height": h
            }),
            _ => serde_json::json!({
                "id": format!("n{i}"), "type": "text",
                "text": if rng.below(5) == 0 { String::new() } else { format!("Card {i}") },
                "x": x, "y": y, "width": w, "height": h
            }),
        };
        nodes.push(node);
    }

    let edge_count = if size == 0 { 0 } else { (size as u64 * 3) / 2 };
    let mut edges = Vec::new();
    for e in 0..edge_count {
        let from = if rng.below(20) == 0 {
            "ghost".to_string() // dangling
        } else {
            format!("n{}", rng.below(size as u64))
        };
        let to = if rng.below(20) == 1 {
            "ghost2".to_string()
        } else {
            format!("n{}", rng.below(size as u64))
        };
        let mut edge = serde_json::json!({
            "id": format!("e{e}"), "fromNode": from, "toNode": to
        });
        let obj = edge.as_object_mut().unwrap();
        match rng.below(4) {
            0 => {
                obj.insert("fromEnd".into(), serde_json::json!("arrow"));
            }
            1 => {
                obj.insert("toEnd".into(), serde_json::json!("none"));
            }
            2 => {
                obj.insert("label".into(), serde_json::json!(format!("L{e}")));
                obj.insert("fromSide".into(), serde_json::json!("top"));
                obj.insert("toSide".into(), serde_json::json!("bottom"));
            }
            _ => {}
        }
        edges.push(edge);
    }
    serde_json::json!({ "nodes": nodes, "edges": edges }).to_string()
}

/// Random adversarial census over degenerate geometry (t1 §rules census).
#[test]
fn census_random_canvases() {
    let (rounds, max_size, big) = census_scale();
    let mut rng = Rng::new(0x5EED_CA9A_5CAF_F01D);
    for round in 0..rounds {
        let size = rng.below(max_size as u64 + 1) as usize;
        let doc = random_canvas(&mut rng, size);
        let (canvas, _) = parse(&doc);
        let model = derive(&canvas);
        assert_invariants(&canvas, &model);
        // Stability under re-parse (rule 4).
        if round % 10 == 0 {
            let (canvas2, _) = parse(&doc);
            assert_eq!(derive(&canvas2), model, "unstable under re-parse");
        }
    }
    // A couple of large canvases at the scale budget.
    for seed in [7u64, 8u64] {
        let mut rng = Rng::new(seed);
        let doc = random_canvas(&mut rng, big);
        let (canvas, _) = parse(&doc);
        let model = derive(&canvas);
        assert_invariants(&canvas, &model);
    }
}
