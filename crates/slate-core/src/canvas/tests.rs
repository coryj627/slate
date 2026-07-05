// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Parser tests (#359). Fixtures live in `tests/fixtures/canvas/` and are
//! shared with the Wave-1 siblings (#360/#517/#366) and the #365 E2E suite.

use super::*;

const SAMPLE: &str = include_str!("../../tests/fixtures/canvas/sample.canvas");
const UNKNOWN_FIELDS: &str = include_str!("../../tests/fixtures/canvas/unknown_fields.canvas");
const MALFORMED: &str = include_str!("../../tests/fixtures/canvas/malformed.canvas");
const GROUPS_NESTED: &str = include_str!("../../tests/fixtures/canvas/groups_nested.canvas");
const EMPTY: &str = include_str!("../../tests/fixtures/canvas/empty.canvas");
const LARGE: &str = include_str!("../../tests/fixtures/canvas/large_2000.canvas");

fn node<'c>(canvas: &'c Canvas, id: &str) -> &'c Node {
    canvas
        .nodes
        .iter()
        .find(|n| n.id.0 == id)
        .unwrap_or_else(|| panic!("node {id} not parsed"))
}

fn edge<'c>(canvas: &'c Canvas, id: &str) -> &'c Edge {
    canvas
        .edges
        .iter()
        .find(|e| e.id.0 == id)
        .unwrap_or_else(|| panic!("edge {id} not parsed"))
}

#[test]
fn sample_parses_every_node_kind() {
    let (canvas, warnings) = parse(SAMPLE);
    assert!(warnings.is_empty(), "unexpected warnings: {warnings:?}");
    assert_eq!(canvas.nodes.len(), 9);
    assert_eq!(canvas.edges.len(), 5);
    assert!(canvas.skipped.is_empty());

    // Text card with preset color.
    let question = node(&canvas, "card-question");
    assert!(
        matches!(&question.kind, NodeKind::Text { text } if text.starts_with("# Core question"))
    );
    assert_eq!(question.color, Some(CanvasColor::Preset(1)));
    assert_eq!(color_name(question.color.as_ref().unwrap()), "red");
    assert_eq!((question.x, question.y), (0.0, 0.0));
    assert_eq!((question.width, question.height), (240.0, 140.0));

    // File card without and with subpath.
    let notes = node(&canvas, "card-notes");
    assert!(
        matches!(&notes.kind, NodeKind::File { file, subpath } if file == "notes/canvas research.md" && subpath.is_none())
    );
    let spec = node(&canvas, "card-spec");
    assert!(
        matches!(&spec.kind, NodeKind::File { subpath: Some(s), .. } if s == "#Announcement grammar")
    );

    // Link card.
    let link = node(&canvas, "card-jsoncanvas");
    assert!(
        matches!(&link.kind, NodeKind::Link { url } if url == "https://jsoncanvas.org/spec/1.0")
    );

    // Groups: labelled, and labelled with background.
    let research = node(&canvas, "grp-research");
    assert!(
        matches!(&research.kind, NodeKind::Group { label: Some(l), background: None } if l == "Research")
    );
    let inspiration = node(&canvas, "grp-inspiration");
    let NodeKind::Group {
        background: Some(bg),
        ..
    } = &inspiration.kind
    else {
        panic!("inspiration group should carry a background");
    };
    assert_eq!(bg.image.as_deref(), Some("assets/corkboard.png"));
    assert_eq!(bg.style, Some(BackgroundStyle::Cover));

    // Hex is carried verbatim and phrases as nearest preset + custom
    // (t5 G7 pin, #370).
    let loose = node(&canvas, "card-loose");
    assert_eq!(loose.color, Some(CanvasColor::Hex("#8850c8".into())));
    assert_eq!(color_name(loose.color.as_ref().unwrap()), "purple (custom)");

    // Document order is preserved (reading-order tiebreak input for #360).
    let ids: Vec<&str> = canvas.nodes.iter().map(|n| n.id.0.as_str()).collect();
    assert_eq!(ids[0], "grp-research");
    assert_eq!(ids[8], "card-loose");
}

#[test]
fn sample_edge_variants() {
    let (canvas, _) = parse(SAMPLE);

    // Sides + label.
    let e = edge(&canvas, "edge-q-evidence");
    assert_eq!(e.from.1, Some(Side::Right));
    assert_eq!(e.to.1, Some(Side::Left));
    assert_eq!(e.label.as_deref(), Some("supports"));
    // Spec defaults: fromEnd = none, toEnd = arrow.
    assert_eq!(e.from_end, EndStyle::None);
    assert_eq!(e.to_end, EndStyle::Arrow);

    // Unlabelled, default ends.
    let e = edge(&canvas, "edge-q-notes");
    assert!(e.label.is_none());
    assert_eq!((e.from_end, e.to_end), (EndStyle::None, EndStyle::Arrow));

    // Bidirectional (both arrows).
    let e = edge(&canvas, "edge-notes-spec");
    assert_eq!((e.from_end, e.to_end), (EndStyle::Arrow, EndStyle::Arrow));

    // Undirected (no arrows).
    let e = edge(&canvas, "edge-evidence-json");
    assert_eq!((e.from_end, e.to_end), (EndStyle::None, EndStyle::None));

    // Colored edge.
    let e = edge(&canvas, "edge-loose");
    assert_eq!(e.color, Some(CanvasColor::Preset(2)));
}

#[test]
fn unknown_fields_are_retained_in_order() {
    let (canvas, warnings) = parse(UNKNOWN_FIELDS);
    assert!(warnings.is_empty(), "unexpected warnings: {warnings:?}");

    // Root-level unknown keys, original order, nodes/edges excluded.
    let root_unknown: Vec<&str> = canvas.unknown.keys().map(String::as_str).collect();
    assert_eq!(root_unknown, ["slateMeta", "trailingRootKey"]);
    assert_eq!(
        canvas.root_key_order,
        ["slateMeta", "nodes", "edges", "trailingRootKey"]
    );

    // Node-level unknown keys in original order, values intact.
    let n1 = node(&canvas, "n1");
    let unknown: Vec<(&str, &Value)> = n1.unknown().collect();
    let keys: Vec<&str> = unknown.iter().map(|(k, _)| *k).collect();
    assert_eq!(keys, ["zIndex", "futureFlag", "nested"]);
    assert_eq!(unknown[0].1, &Value::from(3));

    // Nested unknown objects keep their own key order (preserve_order).
    let nested = n1.raw.get("nested").and_then(Value::as_object).unwrap();
    let nested_keys: Vec<&str> = nested.keys().map(String::as_str).collect();
    assert_eq!(nested_keys, ["b", "a", "c"]);

    // The full original object is retained, interleaved order intact.
    let raw_keys: Vec<&str> = n1.raw.keys().map(String::as_str).collect();
    assert_eq!(
        raw_keys,
        [
            "id",
            "zIndex",
            "type",
            "text",
            "x",
            "y",
            "width",
            "height",
            "futureFlag",
            "nested"
        ]
    );

    // Edge-level unknown keys.
    let e1 = edge(&canvas, "e1");
    let edge_unknown: Vec<&str> = e1.unknown().map(|(k, _)| k).collect();
    assert_eq!(edge_unknown, ["waypoints"]);
}

#[test]
fn malformed_entries_are_skipped_and_retained() {
    let (canvas, warnings) = parse(MALFORMED);
    assert!(!is_load_degraded(&warnings));

    // Only the two good nodes are modeled.
    let ids: Vec<&str> = canvas.nodes.iter().map(|n| n.id.0.as_str()).collect();
    assert_eq!(ids, ["good-1", "good-2"]);

    // Skipped node entries: retained verbatim, in place, with cause.
    let node_skips: Vec<&SkippedEntry> = canvas
        .skipped
        .iter()
        .filter(|s| s.section == Section::Nodes)
        .collect();
    assert_eq!(node_skips.len(), 4);
    assert_eq!(node_skips[0].position, 1);
    assert!(matches!(
        &node_skips[0].warning,
        CanvasWarning::MalformedNode { index: 1, reason } if reason.contains("\"x\"")
    ));
    assert_eq!(node_skips[1].position, 2);
    assert_eq!(node_skips[1].raw, Value::from(42));
    assert!(matches!(
        &node_skips[2].warning,
        CanvasWarning::UnknownNodeType { node_type, .. } if node_type == "video"
    ));
    // Unknown-type entry keeps its full content for re-emission.
    assert_eq!(
        node_skips[2].raw.get("src").and_then(Value::as_str),
        Some("clip.mp4")
    );
    assert!(matches!(
        &node_skips[3].warning,
        CanvasWarning::DuplicateId { section: Section::Nodes, id, .. } if id == "good-1"
    ));

    // Non-preset color string parses verbatim as a custom color.
    let good2 = node(&canvas, "good-2");
    assert_eq!(good2.color, Some(CanvasColor::Hex("totally-custom".into())));

    // Edges: the entry missing its target is skipped + retained…
    let edge_ids: Vec<&str> = canvas.edges.iter().map(|e| e.id.0.as_str()).collect();
    assert_eq!(edge_ids, ["edge-good", "edge-dangling", "edge-odd-side"]);
    let edge_skips: Vec<&SkippedEntry> = canvas
        .skipped
        .iter()
        .filter(|s| s.section == Section::Edges)
        .collect();
    assert_eq!(edge_skips.len(), 1);
    assert!(matches!(
        &edge_skips[0].warning,
        CanvasWarning::MalformedEdge { index: 1, reason } if reason.contains("toNode")
    ));

    // …the edge into a skipped node parses but is flagged dangling…
    assert!(warnings.iter().any(|w| matches!(
        w,
        CanvasWarning::DanglingEdge { edge_id, missing_node }
            if edge_id.0 == "edge-dangling" && missing_node == "missing-x"
    )));

    // …and unusable optional values degrade to defaults with a warning,
    // never data loss (originals stay in `raw`).
    let odd = edge(&canvas, "edge-odd-side");
    assert_eq!(odd.from.1, None);
    assert_eq!(odd.to_end, EndStyle::Arrow);
    assert!(warnings.iter().any(|w| matches!(
        w,
        CanvasWarning::IgnoredValue { key, .. } if key == "fromSide"
    )));
    assert!(warnings.iter().any(|w| matches!(
        w,
        CanvasWarning::IgnoredValue { key, .. } if key == "toEnd"
    )));
    assert_eq!(
        odd.raw.get("fromSide").and_then(Value::as_str),
        Some("diagonal")
    );
}

#[test]
fn nested_groups_parse() {
    let (canvas, warnings) = parse(GROUPS_NESTED);
    assert!(warnings.is_empty());
    let groups: Vec<&Node> = canvas
        .nodes
        .iter()
        .filter(|n| matches!(n.kind, NodeKind::Group { .. }))
        .collect();
    assert_eq!(groups.len(), 4);
    // Unlabelled group is legal.
    assert!(matches!(
        &node(&canvas, "inner-b").kind,
        NodeKind::Group { label: None, .. }
    ));
}

#[test]
fn empty_and_degenerate_inputs() {
    // Empty / whitespace input = new empty canvas, no complaints.
    for input in ["", "   \n\t"] {
        let (canvas, warnings) = parse(input);
        assert!(canvas.nodes.is_empty() && canvas.edges.is_empty());
        assert!(warnings.is_empty());
    }

    // `{}` = valid empty canvas.
    let (canvas, warnings) = parse(EMPTY);
    assert!(canvas.nodes.is_empty() && warnings.is_empty());

    // Whole-file failures degrade to an empty, read-only canvas.
    for input in [
        "not json",
        "[]",
        "{\"nodes\":{}}",
        "{\"nodes\":[],\"edges\":42}",
    ] {
        let (canvas, warnings) = parse(input);
        assert!(canvas.nodes.is_empty(), "input {input:?}");
        assert!(is_load_degraded(&warnings), "input {input:?}");
    }
}

#[test]
fn color_names_are_pinned() {
    let expected = [
        (1, "red"),
        (2, "orange"),
        (3, "yellow"),
        (4, "green"),
        (5, "cyan"),
        (6, "purple"),
    ];
    for (preset, name) in expected {
        assert_eq!(color_name(&CanvasColor::Preset(preset)), name);
    }
    // Nearest-preset mapping for customs (#370): family + "(custom)".
    assert_eq!(
        color_name(&CanvasColor::Hex("#aabbcc".into())),
        "purple (custom)"
    );
    assert_eq!(
        color_name(&CanvasColor::Hex("#fb464c".into())),
        "red (custom)"
    );
    assert_eq!(color_name(&CanvasColor::Hex("#f00".into())), "red (custom)");
    assert_eq!(
        color_name(&CanvasColor::Hex("bogus".into())),
        "custom color"
    );
}

#[test]
fn multi_and_self_edges_parse_without_warnings() {
    let input = r#"{
        "nodes":[
            {"id":"a","type":"text","text":"a","x":0,"y":0,"width":10,"height":10},
            {"id":"b","type":"text","text":"b","x":20,"y":0,"width":10,"height":10}
        ],
        "edges":[
            {"id":"e1","fromNode":"a","toNode":"b"},
            {"id":"e2","fromNode":"a","toNode":"b","label":"second"},
            {"id":"e3","fromNode":"a","toNode":"a"}
        ]
    }"#;
    let (canvas, warnings) = parse(input);
    assert_eq!(canvas.edges.len(), 3);
    assert!(warnings.is_empty(), "unexpected warnings: {warnings:?}");
}

// --- 2,000-node fixture -------------------------------------------------
//
// The large fixture is generated deterministically so it can be committed
// and regenerated at will (t1: "checked-in generator script"). The
// non-ignored test asserts the committed file matches the generator, so
// the two can never drift.

const LARGE_LOOSE: usize = 1500;
const LARGE_GROUPS: usize = 100;
const LARGE_PER_GROUP: usize = 4;

fn build_large_fixture() -> String {
    use serde_json::json;

    let mut nodes = Vec::new();
    let mut edges = Vec::new();

    for g in 0..LARGE_GROUPS {
        let (gx, gy) = ((g % 10) as f64, (g / 10) as f64);
        let (x, y) = (gx * 1300.0, gy * 1100.0);
        nodes.push(json!({
            "id": format!("grp{g}"),
            "type": "group",
            "x": x, "y": y, "width": 1000.0, "height": 800.0,
            "label": format!("Group {g}")
        }));
        for k in 0..LARGE_PER_GROUP {
            let cx = x + 50.0 + (k % 2) as f64 * 450.0;
            let cy = y + 50.0 + (k / 2) as f64 * 350.0;
            nodes.push(json!({
                "id": format!("g{g}c{k}"),
                "type": "text",
                "text": format!("Group {g} card {k}"),
                "x": cx, "y": cy, "width": 400.0, "height": 250.0
            }));
        }
    }

    for i in 0..LARGE_LOOSE {
        // A sprinkling of degenerate-but-legal geometry: negative
        // coordinates on the first row, a zero-size node every 400th.
        let x = ((i % 50) as f64) * 220.0 - 400.0;
        let y = 12000.0 + ((i / 50) as f64) * 160.0;
        let (w, h) = if i % 400 == 399 {
            (0.0, 0.0)
        } else {
            (200.0, 100.0)
        };
        let id = format!("n{i}");
        let mut node = if i % 17 == 0 {
            json!({
                "id": id, "type": "link",
                "url": format!("https://example.com/page/{i}"),
                "x": x, "y": y, "width": w, "height": h
            })
        } else if i % 10 == 0 {
            let mut v = json!({
                "id": id, "type": "file",
                "file": format!("notes/note {i}.md"),
                "x": x, "y": y, "width": w, "height": h
            });
            if i % 30 == 0 {
                v.as_object_mut()
                    .unwrap()
                    .insert("subpath".into(), json!(format!("#Section {i}")));
            }
            v
        } else {
            json!({
                "id": id, "type": "text",
                "text": format!("Card {i}\nGenerated fixture body {i}."),
                "x": x, "y": y, "width": w, "height": h
            })
        };
        if i % 3 == 0 {
            let color = if i % 21 == 0 {
                "#136f92".to_string()
            } else {
                format!("{}", i % 6 + 1)
            };
            node.as_object_mut()
                .unwrap()
                .insert("color".into(), json!(color));
        }
        nodes.push(node);
    }

    for i in 0..LARGE_LOOSE - 1 {
        let mut e = json!({
            "id": format!("ce{i}"),
            "fromNode": format!("n{i}"),
            "toNode": format!("n{}", i + 1)
        });
        let obj = e.as_object_mut().unwrap();
        if i % 5 == 0 {
            obj.insert("label".into(), json!(format!("step {i}")));
        }
        match i % 4 {
            0 => {
                obj.insert("fromSide".into(), json!("right"));
                obj.insert("toSide".into(), json!("left"));
            }
            1 => {
                obj.insert("fromEnd".into(), json!("arrow"));
            }
            2 => {
                obj.insert("toEnd".into(), json!("none"));
            }
            _ => {}
        }
        edges.push(e);
    }
    for g in 0..LARGE_GROUPS {
        for k in 0..LARGE_PER_GROUP {
            let j = g * LARGE_PER_GROUP + k;
            edges.push(json!({
                "id": format!("ge{j}"),
                "fromNode": format!("g{g}c{k}"),
                "toNode": format!("n{}", (j * 37) % LARGE_LOOSE)
            }));
        }
    }

    // Emit through the canonical serializer (#366) so the committed
    // fixture is also a byte-stability test subject.
    let compact = serde_json::to_string(&json!({ "nodes": nodes, "edges": edges }))
        .expect("fixture serialization cannot fail");
    let (canvas, warnings) = parse(&compact);
    assert!(warnings.is_empty(), "generator produced warnings");
    super::serialize::serialize(&canvas)
}

/// Regenerate the committed fixture: `cargo test -p slate-core
/// regenerate_large_fixture -- --ignored`.
#[test]
#[ignore = "writes the committed large_2000.canvas fixture"]
fn regenerate_large_fixture() {
    let path = concat!(
        env!("CARGO_MANIFEST_DIR"),
        "/tests/fixtures/canvas/large_2000.canvas"
    );
    std::fs::write(path, build_large_fixture()).expect("write fixture");
}

#[test]
fn large_fixture_matches_generator() {
    assert!(
        LARGE == build_large_fixture(),
        "large_2000.canvas is out of sync with build_large_fixture(); \
         run `cargo test -p slate-core regenerate_large_fixture -- --ignored`"
    );
}

#[test]
fn large_fixture_parses_clean() {
    let (canvas, warnings) = parse(LARGE);
    assert_eq!(
        canvas.nodes.len(),
        LARGE_GROUPS * (1 + LARGE_PER_GROUP) + LARGE_LOOSE
    );
    assert_eq!(canvas.nodes.len(), 2000);
    assert_eq!(
        canvas.edges.len(),
        (LARGE_LOOSE - 1) + LARGE_GROUPS * LARGE_PER_GROUP
    );
    assert!(warnings.is_empty(), "unexpected warnings: {warnings:?}");
    assert!(canvas.skipped.is_empty());
}
