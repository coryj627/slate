// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Structural-query tests (W6-1 PR 0b, #745): one behaviour table per
//! query over the committed fixtures, the hostile-geometry `redteam_*`
//! cases, and censuses over generated canvases for the invariants no
//! table can enumerate. `SLATE_CENSUS_FULL=1` (release) runs full scale.
//!
//! Every pinned tie-break and edge case in contracts §0b has a case
//! here, and the ones whose answer depends on document order are pinned
//! with a PAIR of fixtures differing only in node order — otherwise the
//! test passes for a reason it does not name.

use super::*;
use crate::canvas::model::{CanvasModel, derive};
use crate::canvas::parse;
use crate::canvas::placement::{DEFAULT_GAP, RelativeDesc};

const SAMPLE: &str = include_str!("../../tests/fixtures/canvas/sample.canvas");
const GROUPS_NESTED: &str = include_str!("../../tests/fixtures/canvas/groups_nested.canvas");
const CYCLE: &str = include_str!("../../tests/fixtures/canvas/cycle.canvas");
const EMPTY: &str = include_str!("../../tests/fixtures/canvas/empty.canvas");

fn model_of(input: &str) -> CanvasModel {
    let (canvas, _) = parse(input);
    derive(&canvas)
}

fn id(s: &str) -> NodeId {
    NodeId(s.to_string())
}

fn ids(model_ids: Vec<NodeId>) -> Vec<String> {
    model_ids.into_iter().map(|n| n.0).collect()
}

fn rect(x: f64, y: f64, w: f64, h: f64) -> Rect {
    Rect::new(x, y, w, h)
}

// ---------------------------------------------------------------------------
// Row C — auto sides (0b-3)

#[test]
fn auto_sides_picks_the_nearest_edge_pair() {
    let a = rect(0.0, 0.0, 100.0, 100.0);
    // Clearly to the right.
    assert_eq!(
        auto_sides(a, rect(500.0, 0.0, 100.0, 100.0)),
        (Side::Right, Side::Left)
    );
    // Clearly to the left.
    assert_eq!(
        auto_sides(a, rect(-500.0, 0.0, 100.0, 100.0)),
        (Side::Left, Side::Right)
    );
    // Clearly below (canvas space is +y down).
    assert_eq!(
        auto_sides(a, rect(0.0, 500.0, 100.0, 100.0)),
        (Side::Bottom, Side::Top)
    );
    // Clearly above.
    assert_eq!(
        auto_sides(a, rect(0.0, -500.0, 100.0, 100.0)),
        (Side::Top, Side::Bottom)
    );
}

#[test]
fn auto_sides_ties_resolve_vertical() {
    let a = rect(0.0, 0.0, 100.0, 100.0);
    // Exact diagonal: |dx| == |dy|, so horizontal's STRICT test fails
    // and the pair is vertical.
    assert_eq!(
        auto_sides(a, rect(300.0, 300.0, 100.0, 100.0)),
        (Side::Bottom, Side::Top)
    );
    assert_eq!(
        auto_sides(a, rect(-300.0, -300.0, 100.0, 100.0)),
        (Side::Top, Side::Bottom)
    );
    // Up-and-right diagonal still goes vertical, not horizontal.
    assert_eq!(
        auto_sides(a, rect(300.0, -300.0, 100.0, 100.0)),
        (Side::Top, Side::Bottom)
    );
}

#[test]
fn auto_sides_survives_a_self_loop() {
    // `canvasConnect` guards `originId != targetId`, but the renderer's
    // anchor arm is reachable for a self-edge parsed from disk, and
    // core's model permits one. `dy > 0` is false at zero, so the tie
    // rule answers (Top, Bottom).
    let a = rect(10.0, 20.0, 100.0, 100.0);
    assert_eq!(auto_sides(a, a), (Side::Top, Side::Bottom));
}

/// The renderer resolved ONE endpoint at a time (`anchorPoint`'s
/// `case nil`), so 0b-3 claims a caller can get that side as
/// `auto_sides(mine, theirs).0`. That is only true if the pair is
/// symmetric under swapping — which it is everywhere except exact
/// centre coincidence, where both directions answer `(Top, Bottom)`
/// and the swap therefore disagrees. Pinned, both halves.
#[test]
fn auto_sides_is_symmetric_under_swap_except_at_coincident_centres() {
    let mut coincident = 0usize;
    for dx in [-300i64, -100, -20, 0, 20, 100, 300] {
        for dy in [-300i64, -100, -20, 0, 20, 100, 300] {
            let a = rect(0.0, 0.0, 100.0, 100.0);
            let b = rect(dx as f64, dy as f64, 100.0, 100.0);
            let forward = auto_sides(a, b);
            let backward = auto_sides(b, a);
            if dx == 0 && dy == 0 {
                coincident += 1;
                assert_eq!(forward, (Side::Top, Side::Bottom));
                assert_eq!(backward, (Side::Top, Side::Bottom));
                continue;
            }
            assert_eq!(
                (forward.0, forward.1),
                (backward.1, backward.0),
                "auto_sides disagreed with its own swap at ({dx}, {dy})"
            );
        }
    }
    assert_eq!(coincident, 1, "the coincident case must be exercised once");
}

// ---------------------------------------------------------------------------
// Row D — containment (0b-8)

#[test]
fn parent_and_children_expose_the_group_tree() {
    let model = model_of(SAMPLE);
    assert_eq!(
        parent_of(&model, &id("card-question")),
        Some(id("grp-research"))
    );
    assert_eq!(parent_of(&model, &id("card-loose")), None);
    assert_eq!(parent_of(&model, &id("grp-research")), None);
    // Reading (sibling) order, not document order.
    assert_eq!(
        ids(children_of(&model, &id("grp-research"))),
        ["card-question", "card-evidence", "card-notes", "card-spec"]
    );
    // A card is not a group: no children, no error.
    assert!(children_of(&model, &id("card-question")).is_empty());
    // An id that is not in the canvas at all: the session layer turns
    // this into `bad_node`; the pure query is total.
    assert_eq!(parent_of(&model, &id("nope")), None);
    assert!(children_of(&model, &id("nope")).is_empty());
}

#[test]
fn children_of_nests() {
    let model = model_of(GROUPS_NESTED);
    assert_eq!(
        ids(children_of(&model, &id("outer"))),
        ["inner-a", "in-outer", "inner-b"]
    );
    assert_eq!(ids(children_of(&model, &id("deep"))), ["in-deep"]);
    // An empty group is empty, not absent.
    assert!(children_of(&model, &id("in-deep")).is_empty());
}

// ---------------------------------------------------------------------------
// Row E — trace path (0b-9)

fn hop_view(hops: &[TraceHop]) -> Vec<(&str, &str, &str, Option<&str>)> {
    hops.iter()
        .map(|h| {
            (
                h.edge.0.as_str(),
                h.node.0.as_str(),
                h.title.as_str(),
                h.label.as_deref(),
            )
        })
        .collect()
}

#[test]
fn trace_path_walks_a_cycle_once_and_stops() {
    let model = model_of(CYCLE);
    assert_eq!(
        hop_view(&trace_path(&model, &id("a"))),
        [
            ("e-ab", "b", "Beta", None),
            ("e-bc", "c", "Gamma", Some("then")),
        ]
    );
    // Entering the same cycle at a different node walks the same ring.
    assert_eq!(
        hop_view(&trace_path(&model, &id("c"))),
        [
            ("e-ca", "a", "Alpha", Some("back to the start")),
            ("e-ab", "b", "Beta", None),
        ]
    );
}

#[test]
fn trace_path_excludes_undirected_and_survives_a_self_loop() {
    let model = model_of(CYCLE);
    // `loop` has a self-edge (already seen) and an UNDIRECTED edge to
    // `same-1` — an unarrowed connection is not traversable.
    assert!(trace_path(&model, &id("loop")).is_empty());
    // A bidirectional edge IS traversable, from either end, and the hop
    // carries the DISPLAY title (`Same`), not the speakable one.
    assert_eq!(
        hop_view(&trace_path(&model, &id("same-1"))),
        [("e-bidirectional", "same-2", "Same", None)]
    );
    assert_eq!(
        hop_view(&trace_path(&model, &id("same-2"))),
        [("e-bidirectional", "same-1", "Same", None)]
    );
    // A node with no connections at all is a dead end, not an error.
    assert!(trace_path(&model, &id("same-real-2")).is_empty());
    // The start node is never a hop.
    assert!(
        trace_path(&model, &id("a"))
            .iter()
            .all(|h| h.node != id("a"))
    );
}

#[test]
fn trace_path_on_an_unknown_node_is_empty() {
    // Total by construction; the session layer answers `bad_node`
    // before it gets here.
    let model = model_of(SAMPLE);
    assert!(trace_path(&model, &id("ghost")).is_empty());
}

// ---------------------------------------------------------------------------
// Row F — reading-order projection (0b-10)

#[test]
fn order_nodes_projects_dedupes_and_drops_the_unknown() {
    let model = model_of(SAMPLE);
    assert_eq!(
        ids(order_nodes(
            &model,
            &[
                id("card-loose"),
                id("card-question"),
                id("ghost"),
                id("card-question"),
                id("grp-research"),
            ]
        )),
        ["grp-research", "card-question", "card-loose"]
    );
    assert!(order_nodes(&model, &[]).is_empty());
    assert!(order_nodes(&model, &[id("ghost")]).is_empty());
}

// ---------------------------------------------------------------------------
// Row B — relative description (0b-7)

/// Three neighbours EQUIDISTANT from the subject, so nothing but the
/// tie-break can decide the answer. `order` names the document order.
fn equidistant(order: [&str; 3]) -> String {
    let node = |which: &str| match which {
        "north" => serde_json::json!({"id":"north","type":"text","text":"North",
            "x":0,"y":-100,"width":10,"height":10}),
        "south" => serde_json::json!({"id":"south","type":"text","text":"South",
            "x":0,"y":100,"width":10,"height":10}),
        _ => serde_json::json!({"id":"east","type":"text","text":"East",
            "x":100,"y":0,"width":10,"height":10}),
    };
    serde_json::json!({
        "nodes": order.iter().map(|w| node(w)).collect::<Vec<_>>(),
        "edges": []
    })
    .to_string()
}

#[test]
fn describe_relative_inverts_the_sense_and_adds_an_axis_distinct_second() {
    let subject = rect(0.0, 0.0, 10.0, 10.0);
    // Document order north, south, east. All three are exactly 100 away
    // from the subject centre, so the nearest is the first in document
    // order — and a neighbour ABOVE the subject makes the subject
    // "below" it (the sense is inverted relative to the delta).
    let model = model_of(&equidistant(["north", "south", "east"]));
    assert_eq!(
        describe_relative(&model, subject, &[]),
        [
            RelativeDesc::Below("North".to_owned()),
            RelativeDesc::LeftOf("East".to_owned()),
        ]
    );
    // Same geometry, different document order: the tie-break is the
    // ONLY thing that changed, and the answer changes with it.
    let model = model_of(&equidistant(["east", "north", "south"]));
    assert_eq!(
        describe_relative(&model, subject, &[]),
        [
            RelativeDesc::LeftOf("East".to_owned()),
            RelativeDesc::Below("North".to_owned()),
        ]
    );
    // A neighbour to the LEFT of the subject makes the subject "right
    // of" it.
    let model = model_of(
        &serde_json::json!({"nodes":[
            {"id":"w","type":"text","text":"West","x":-100,"y":0,"width":10,"height":10}
        ],"edges":[]})
        .to_string(),
    );
    assert_eq!(
        describe_relative(&model, subject, &[]),
        [RelativeDesc::RightOf("West".to_owned())]
    );
}

#[test]
fn describe_relative_at_coincident_centres_is_above() {
    // |dy| >= |dx| holds at zero so the axis is vertical, and `dy < 0`
    // is false, so the phrase is "Above". Deterministic and arguably
    // wrong; mac ships it and core preserves it.
    let model = model_of(
        &serde_json::json!({"nodes":[
            {"id":"twin","type":"text","text":"Twin","x":0,"y":0,"width":10,"height":10}
        ],"edges":[]})
        .to_string(),
    );
    assert_eq!(
        describe_relative(&model, rect(0.0, 0.0, 10.0, 10.0), &[]),
        [RelativeDesc::Above("Twin".to_owned())]
    );
}

#[test]
fn describe_relative_skips_groups_and_the_excluded() {
    // Groups are never candidates, so a canvas of nothing but groups
    // answers with an empty list — which the announcement layer renders
    // as "Alone on the canvas". This query never invents copy.
    let model = model_of(GROUPS_NESTED);
    let groups: Vec<NodeId> = model
        .summaries
        .iter()
        .filter(|(_, s)| s.kind_label != "group")
        .map(|(id, _)| id.clone())
        .collect();
    assert!(describe_relative(&model, rect(0.0, 0.0, 10.0, 10.0), &groups).is_empty());

    // Excluding the only card leaves nothing to describe.
    let model = model_of(
        &serde_json::json!({"nodes":[
            {"id":"only","type":"text","text":"Only","x":300,"y":0,"width":10,"height":10}
        ],"edges":[]})
        .to_string(),
    );
    assert!(!describe_relative(&model, rect(0.0, 0.0, 10.0, 10.0), &[]).is_empty());
    assert!(describe_relative(&model, rect(0.0, 0.0, 10.0, 10.0), &[id("only")]).is_empty());

    // An empty canvas has no candidates either.
    assert!(describe_relative(&model_of(EMPTY), rect(0.0, 0.0, 10.0, 10.0), &[]).is_empty());
}

#[test]
fn redteam_describe_relative_ranks_non_finite_distances_last() {
    // A rect whose far corner overflows to +inf has a non-finite centre
    // and therefore a non-finite delta; it must never out-rank a real
    // neighbour, and must not make the order depend on NaN comparison.
    let model = model_of(
        &serde_json::json!({"nodes":[
            {"id":"huge","type":"text","text":"Huge",
             "x":1.7e308,"y":0,"width":1.7e308,"height":10},
            {"id":"near","type":"text","text":"Near","x":0,"y":-100,"width":10,"height":10}
        ],"edges":[]})
        .to_string(),
    );
    let descs = describe_relative(&model, rect(0.0, 0.0, 10.0, 10.0), &[]);
    assert_eq!(descs[0], RelativeDesc::Below("Near".to_owned()));
    // Deterministic across repeated derivations of the same document.
    assert_eq!(
        descs,
        describe_relative(&model, rect(0.0, 0.0, 10.0, 10.0), &[])
    );
}

// ---------------------------------------------------------------------------
// Row H — bounds and group geometry (0b-11)

#[test]
fn bounds_unions_every_node_including_group_frames() {
    let model = model_of(SAMPLE);
    let b = bounds(&model).expect("sample is not empty");
    // grp-research starts at (-40, -40); grp-inspiration ends at
    // x = 600 + 320; card-loose ends at y = 460 + 100.
    assert_eq!((b.x0, b.y0, b.x1, b.y1), (-40.0, -40.0, 920.0, 560.0));
    assert_eq!(bounds(&model_of(EMPTY)), None);
}

#[test]
fn group_rect_around_pads_the_union_by_the_default_gap() {
    let model = model_of(SAMPLE);
    // The pad is DEFAULT_GAP, not a literal 40: mac's `pad = 40.0` and
    // this constant are the same number, and that identity is the
    // contract (0b-11), so the expectation is written from the constant.
    let one = group_rect_around(&model, &[id("card-question")]).expect("member resolves");
    assert_eq!(
        (one.x0, one.y0, one.x1, one.y1),
        (
            0.0 - DEFAULT_GAP,
            0.0 - DEFAULT_GAP,
            240.0 + DEFAULT_GAP,
            140.0 + DEFAULT_GAP
        )
    );
    let two = group_rect_around(&model, &[id("card-question"), id("card-evidence")])
        .expect("members resolve");
    assert_eq!(
        (two.x0, two.y0, two.x1, two.y1),
        (
            0.0 - DEFAULT_GAP,
            0.0 - DEFAULT_GAP,
            480.0 + DEFAULT_GAP,
            140.0 + DEFAULT_GAP
        )
    );
    // Unresolvable members are skipped, not fatal…
    assert_eq!(
        group_rect_around(&model, &[id("ghost"), id("card-question")]),
        Some(one)
    );
    // …and a set with nothing resolvable is None, which is mac's silent
    // `guard minX.isFinite` typed.
    assert_eq!(group_rect_around(&model, &[id("ghost")]), None);
    assert_eq!(group_rect_around(&model, &[]), None);
}

// ---------------------------------------------------------------------------
// Row K — the filter (0b-13 / 0b-14)

fn filtered(model: &CanvasModel, query: &str) -> Vec<String> {
    ids(filter(model, query))
}

#[test]
fn filter_with_an_empty_query_matches_everything() {
    let model = model_of(SAMPLE);
    let all: Vec<String> = model.reading_order.iter().map(|n| n.0.clone()).collect();
    assert_eq!(filtered(&model, ""), all);
    assert_eq!(filtered(&model, "   "), all);
    assert_eq!(filtered(&model, "\t\n "), all);
    // …and on an empty canvas that is an empty list, not a panic.
    assert!(filtered(&model_of(EMPTY), "").is_empty());
}

#[test]
fn filter_matches_title_kind_group_path_and_target_in_reading_order() {
    let model = model_of(SAMPLE);
    // Title ("Research" the group, "canvas research" the file card) and
    // group path (every card inside Research), in reading order.
    assert_eq!(
        filtered(&model, "research"),
        [
            "grp-research",
            "card-question",
            "card-evidence",
            "card-notes",
            "card-spec"
        ]
    );
    // Case-folded, and the surrounding whitespace is trimmed off first.
    assert_eq!(filtered(&model, "ReSeArCh"), filtered(&model, "research"));
    assert_eq!(
        filtered(&model, "  research  "),
        filtered(&model, "research")
    );
    // The KIND type word is a field, so "group" selects every group.
    // Shipped mac behaviour, preserved deliberately (0b-13).
    assert_eq!(
        filtered(&model, "group"),
        ["grp-research", "grp-inspiration"]
    );
    // The target: a file path no title contains.
    assert_eq!(filtered(&model, ".png"), ["card-diagram"]);
    // A link card's URL is its target.
    assert_eq!(filtered(&model, "jsoncanvas.org/spec"), ["card-jsoncanvas"]);
    // No match is an empty list.
    assert!(filtered(&model, "zzz-nothing-matches").is_empty());
}

#[test]
fn filter_matches_a_group_path_element_but_not_across_the_separator() {
    let model = model_of(GROUPS_NESTED);
    // `in-deep`'s path is Quarter › Q3 › Week 1; each element matches…
    assert!(filtered(&model, "Week 1").contains(&"in-deep".to_owned()));
    assert!(filtered(&model, "Quarter").contains(&"in-deep".to_owned()));
    // …but a needle spanning the separator matches nothing, because the
    // path is matched per element and never joined.
    assert!(filtered(&model, "Q3 › Week 1").is_empty());
    assert!(filtered(&model, "Q3 Week").is_empty());
}

#[test]
fn filter_folds_case_the_unicode_way_not_the_turkish_way() {
    // CD-22, witnessed rather than asserted. Core folds with Rust's
    // locale-independent simple lowercase; mac used
    // `localizedCaseInsensitiveContains`, which in a Turkish locale
    // treats I/ı as a pair. Core does not.
    let model = model_of(
        &serde_json::json!({"nodes":[
            {"id":"dotless","type":"text","text":"ırmak","x":0,"y":0,"width":10,"height":10},
            {"id":"dotted","type":"text","text":"İstanbul","x":0,"y":100,"width":10,"height":10},
            {"id":"plain","type":"text","text":"Irmak","x":0,"y":200,"width":10,"height":10}
        ],"edges":[]})
        .to_string(),
    );
    // ASCII "I" folds to "i" and finds neither the dotless ı…
    assert_eq!(filtered(&model, "Irmak"), ["plain"]);
    assert_eq!(filtered(&model, "irmak"), ["plain"]);
    // …nor does the dotless needle find the ASCII title.
    assert_eq!(filtered(&model, "ırmak"), ["dotless"]);
    // `İ` (U+0130) lowercases to `i` + COMBINING DOT ABOVE, two
    // scalars, so a plain "istanbul" needle does NOT match it.
    assert!(filtered(&model, "istanbul").is_empty());
    assert_eq!(filtered(&model, "İstanbul"), ["dotted"]);
}

// ---------------------------------------------------------------------------
// Row M — id minting (0b-4)

#[test]
fn new_id_has_the_v4_shape_and_does_not_repeat() {
    let mut seen = std::collections::HashSet::new();
    for _ in 0..5_000 {
        let minted = new_id();
        assert_eq!(minted.len(), 16, "16 hex characters: {minted}");
        assert!(
            minted
                .chars()
                .all(|c| c.is_ascii_digit() || ('a'..='f').contains(&c)),
            "lowercase hex only: {minted}"
        );
        // Index 12 is a v4 UUID's version nibble, so the id carries 60
        // bits of entropy, not 64.
        assert_eq!(
            minted.as_bytes()[12],
            b'4',
            "the version nibble must be '4': {minted}"
        );
        assert!(seen.insert(minted), "a minted id repeated within one run");
    }
    // Every other position must actually vary, or the "entropy" claim
    // is decoration.
    let draws: Vec<String> = (0..256).map(|_| new_id()).collect();
    for position in 0..16 {
        if position == 12 {
            continue;
        }
        let distinct: std::collections::HashSet<u8> =
            draws.iter().map(|d| d.as_bytes()[position]).collect();
        assert!(
            distinct.len() > 1,
            "position {position} never varied across 256 draws"
        );
    }
}

// ---------------------------------------------------------------------------
// Census

struct Rng(u64);
impl Rng {
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

/// Edge-dense random canvases: the trace-path invariants need arrows,
/// which the placement census (cards only) never generates.
fn random_canvas(rng: &mut Rng, size: usize) -> String {
    let mut nodes = Vec::new();
    for i in 0..size {
        let x = (rng.pick_i(-20, 20) * 25) as f64;
        let y = (rng.pick_i(-20, 20) * 25) as f64;
        let (w, h) = match rng.below(5) {
            0 => (0.0, 0.0),
            1 => (60.0, 40.0),
            2 => (240.0, 140.0),
            3 => (600.0, 450.0),
            _ => (-120.0, 90.0),
        };
        // Repeated and blank titles on purpose: the speakable-name and
        // filter invariants are about collisions.
        let title = match rng.below(4) {
            0 => String::new(),
            1 => "Same".to_owned(),
            2 => "Same 2".to_owned(),
            _ => format!("Card {i}"),
        };
        let node = if rng.below(5) == 0 {
            serde_json::json!({"id": format!("n{i}"), "type":"group",
                "label": title, "x":x, "y":y, "width":w, "height":h})
        } else {
            serde_json::json!({"id": format!("n{i}"), "type":"text",
                "text": title, "x":x, "y":y, "width":w, "height":h})
        };
        nodes.push(node);
    }
    let mut edges = Vec::new();
    for e in 0..(size as u64 * 2) {
        let mut edge = serde_json::json!({
            "id": format!("e{e}"),
            "fromNode": format!("n{}", rng.below(size.max(1) as u64)),
            "toNode": format!("n{}", rng.below(size.max(1) as u64)),
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
                obj.insert("fromEnd".into(), serde_json::json!("arrow"));
                obj.insert("toEnd".into(), serde_json::json!("arrow"));
            }
            _ => {}
        }
        edges.push(edge);
    }
    serde_json::json!({"nodes": nodes, "edges": edges}).to_string()
}

#[test]
fn census_structural_queries_hold_their_invariants() {
    let (rounds, max_size) = if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        (400u64, 200usize)
    } else {
        (80u64, 60usize)
    };
    let mut rng = Rng(0xC0FF_EE15_600D_1234);
    for _ in 0..rounds {
        let size = rng.below(max_size as u64 + 1) as usize;
        let doc = random_canvas(&mut rng, size);
        let model = model_of(&doc);
        let all: Vec<NodeId> = model.reading_order.clone();

        // The empty query returns exactly the reading order, and every
        // non-empty result is a SUBSEQUENCE of it (a filter is a view,
        // never a reorder).
        assert_eq!(filter(&model, ""), all);
        for needle in ["same", "CARD", "group", "2", "zzz"] {
            let matched = filter(&model, needle);
            let mut walk = all.iter();
            for hit in &matched {
                assert!(
                    walk.any(|id| id == hit),
                    "filter result is not in reading order"
                );
            }
        }

        // `order_nodes` is exactly the reading-order projection of the
        // input SET — proved against a straight-line restatement.
        if !all.is_empty() {
            let mut picked: Vec<NodeId> = Vec::new();
            for _ in 0..(rng.below(6) + 1) {
                picked.push(all[rng.below(all.len() as u64) as usize].clone());
            }
            picked.push(id("ghost-not-in-canvas"));
            let wanted: std::collections::HashSet<&NodeId> = picked.iter().collect();
            let oracle: Vec<NodeId> = all
                .iter()
                .filter(|id| wanted.contains(id))
                .cloned()
                .collect();
            assert_eq!(order_nodes(&model, &picked), oracle);
        }

        // Speakable names are UNIQUE across the canvas.
        let names: std::collections::HashSet<&str> = model
            .summaries
            .values()
            .map(|s| s.speakable_name.as_str())
            .collect();
        assert_eq!(
            names.len(),
            model.summaries.len(),
            "speakable names collide"
        );

        // …and a GENERATED ordinal never spells some OTHER card's real
        // title. That is what the taken-guard buys, and mac's loop does
        // not have it: mac turns `A`, `A`, `A 2` into
        // `A`, `A 2`, `A 2 2`, so a Voice Control user reading `A 2` on
        // the third card selects the second. Uniqueness alone cannot
        // catch that — mac's names are unique too.
        let real_titles: std::collections::HashSet<&str> = model
            .summaries
            .values()
            .map(|s| s.display_title.as_str())
            .collect();
        for summary in model.summaries.values() {
            if summary.speakable_name != summary.display_title {
                assert!(
                    !real_titles.contains(summary.speakable_name.as_str()),
                    "the generated name {:?} is another card's real title",
                    summary.speakable_name
                );
            }
        }

        for start in &all {
            // The walk terminates, visits each node at most once, never
            // revisits the start, and every hop is a real neighbour
            // reached by an eligible edge.
            let hops = trace_path(&model, start);
            assert!(hops.len() < model.summaries.len().max(1));
            let mut seen: std::collections::HashSet<&NodeId> = std::collections::HashSet::new();
            seen.insert(start);
            let mut current = start;
            for hop in &hops {
                assert!(seen.insert(&hop.node), "trace path revisited a node");
                let neighbor = model.adjacency[current]
                    .iter()
                    .find(|n| n.edge == hop.edge && n.other == hop.node)
                    .expect("hop is a real neighbour of the previous node");
                assert!(
                    matches!(
                        neighbor.direction,
                        EdgeDirection::Outgoing | EdgeDirection::Bidirectional
                    ),
                    "trace path followed a non-traversable connection"
                );
                current = &hop.node;
            }

            // `parent_of` / `children_of` agree with each other.
            if let Some(parent) = parent_of(&model, start) {
                assert!(children_of(&model, &parent).contains(start));
            }
            for child in children_of(&model, start) {
                assert_eq!(parent_of(&model, &child).as_ref(), Some(start));
            }
        }

        // Describe-relative answers at most two phrases, and when it
        // answers two they classify on DIFFERENT axes.
        for probe in [(0.0, 0.0), (-500.0, 250.0), (1000.0, -1000.0)] {
            let descs = describe_relative(&model, rect(probe.0, probe.1, 240.0, 140.0), &[]);
            assert!(descs.len() <= 2);
            if descs.len() == 2 {
                let vertical =
                    |d: &RelativeDesc| matches!(d, RelativeDesc::Below(_) | RelativeDesc::Above(_));
                assert_ne!(vertical(&descs[0]), vertical(&descs[1]));
            }
        }
    }
}
