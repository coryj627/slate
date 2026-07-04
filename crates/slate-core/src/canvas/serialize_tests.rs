// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Serializer tests (#366, pure emit): byte-stability on every
//! canonical fixture, structural round-trips, skipped-entry re-emission,
//! per-field drift protection, canonical ordering for new entries.

use super::*;
use crate::canvas::{CanvasColor, NodeId, parse};

const SAMPLE: &str = include_str!("../../tests/fixtures/canvas/sample.canvas");
const UNKNOWN_FIELDS: &str = include_str!("../../tests/fixtures/canvas/unknown_fields.canvas");
const MALFORMED: &str = include_str!("../../tests/fixtures/canvas/malformed.canvas");
const GROUPS_NESTED: &str = include_str!("../../tests/fixtures/canvas/groups_nested.canvas");
const EMPTY: &str = include_str!("../../tests/fixtures/canvas/empty.canvas");
const LARGE: &str = include_str!("../../tests/fixtures/canvas/large_2000.canvas");

const ALL: &[(&str, &str)] = &[
    ("sample", SAMPLE),
    ("unknown_fields", UNKNOWN_FIELDS),
    ("malformed", MALFORMED),
    ("groups_nested", GROUPS_NESTED),
    ("empty", EMPTY),
    ("large_2000", LARGE),
];

/// Untouched files in canonical form re-emit byte-identically — the
/// malformed fixture proves skipped entries (including the bare `42`
/// and the unknown `video` node) re-emit verbatim, in place.
#[test]
fn byte_stability_for_untouched_fixtures() {
    for (name, input) in ALL {
        let (canvas, _) = parse(input);
        assert_eq!(&serialize(&canvas), input, "fixture {name} not byte-stable");
    }
}

/// Emission is idempotent from any parseable input, canonical or not.
#[test]
fn serialization_is_idempotent() {
    let foreign = r#"{ "nodes": [ { "id": "a", "type": "text", "text": "spaced out",
        "x": 1.5, "y": -2, "width": 100, "height": 50 } ], "edges": [] }"#;
    for input in [SAMPLE, foreign] {
        let (c1, _) = parse(input);
        let once = serialize(&c1);
        let (c2, warnings) = parse(&once);
        assert!(warnings.is_empty());
        assert_eq!(serialize(&c2), once);
        // Structural equality across the round-trip.
        assert_eq!(c1, c2);
    }
}

#[test]
fn structural_round_trip_all_fixtures() {
    for (name, input) in ALL {
        let (c1, w1) = parse(input);
        let (c2, w2) = parse(&serialize(&c1));
        assert_eq!(c1, c2, "fixture {name}");
        assert_eq!(w1.len(), w2.len(), "fixture {name} warning drift");
    }
}

/// Editing one field of one node rewrites only that node's line; every
/// other byte of the file — including integer coordinates on the edited
/// node — is untouched (no float drift).
#[test]
fn per_field_reconciliation_prevents_drift() {
    let (mut canvas, _) = parse(SAMPLE);
    let idx = canvas
        .nodes
        .iter()
        .position(|n| n.id.0 == "card-evidence")
        .unwrap();
    canvas.nodes[idx].color = Some(CanvasColor::Preset(3));

    let out = serialize(&canvas);
    let before: Vec<&str> = SAMPLE.lines().collect();
    let after: Vec<&str> = out.lines().collect();
    assert_eq!(before.len(), after.len());
    for (b, a) in before.iter().zip(&after) {
        if b.contains("\"id\":\"card-evidence\"") {
            assert_ne!(a, b);
            assert!(a.contains("\"color\":\"3\""));
            // Untouched numeric fields keep their integer representation.
            assert!(a.contains("\"x\":260"));
            assert!(!a.contains("260.0"));
        } else {
            assert_eq!(a, b, "unrelated line changed");
        }
    }
}

/// Geometry updates emit integral coordinates as integers.
#[test]
fn changed_geometry_stays_integral() {
    let (mut canvas, _) = parse(SAMPLE);
    let idx = canvas
        .nodes
        .iter()
        .position(|n| n.id.0 == "card-loose")
        .unwrap();
    canvas.nodes[idx].x = 40.0;
    canvas.nodes[idx].y = 480.0;
    let out = serialize(&canvas);
    let line = out.lines().find(|l| l.contains("card-loose")).unwrap();
    assert!(line.contains("\"x\":40,"), "{line}");
    assert!(line.contains("\"y\":480,"), "{line}");
}

/// Brand-new nodes/edges (no retained map) emit in canonical key order;
/// spec-default end styles stay implicit.
#[test]
fn new_entries_use_canonical_order() {
    use crate::canvas::{Edge, EdgeId, EndStyle, Node, NodeKind};
    let (mut canvas, _) = parse(EMPTY);
    canvas.nodes.push(Node {
        id: NodeId("new1".into()),
        kind: NodeKind::Text {
            text: "hello".into(),
        },
        x: 0.0,
        y: 20.0,
        width: 260.0,
        height: 140.0,
        color: Some(CanvasColor::Preset(2)),
        raw: crate::canvas::RawExtra::new(),
    });
    canvas.nodes.push(Node {
        id: NodeId("new2".into()),
        kind: NodeKind::Text {
            text: "target".into(),
        },
        x: 0.0,
        y: 220.0,
        width: 260.0,
        height: 140.0,
        color: None,
        raw: crate::canvas::RawExtra::new(),
    });
    canvas.edges.push(Edge {
        id: EdgeId("e1".into()),
        from: (NodeId("new1".into()), None),
        to: (NodeId("new2".into()), None),
        from_end: EndStyle::None,
        to_end: EndStyle::Arrow,
        label: None,
        color: None,
        raw: crate::canvas::RawExtra::new(),
    });

    let out = serialize(&canvas);
    let expected = "{\n\
\t\"nodes\":[\n\
\t\t{\"id\":\"new1\",\"type\":\"text\",\"text\":\"hello\",\"x\":0,\"y\":20,\"width\":260,\"height\":140,\"color\":\"2\"},\n\
\t\t{\"id\":\"new2\",\"type\":\"text\",\"text\":\"target\",\"x\":0,\"y\":220,\"width\":260,\"height\":140}\n\
\t],\n\
\t\"edges\":[\n\
\t\t{\"id\":\"e1\",\"fromNode\":\"new1\",\"toNode\":\"new2\"}\n\
\t]\n\
}\n";
    assert_eq!(out, expected);

    // Round-trips cleanly.
    let (back, warnings) = parse(&out);
    assert!(warnings.is_empty());
    assert_eq!(back.nodes.len(), 2);
    assert_eq!(back.edges.len(), 1);
}

/// Deleting a modeled node keeps every skipped entry (positions may
/// have shifted; data is never dropped).
#[test]
fn skipped_entries_survive_deletions() {
    let (mut canvas, _) = parse(MALFORMED);
    let skipped_before = canvas.skipped.len();
    canvas.nodes.retain(|n| n.id.0 != "good-1");
    canvas.edges.clear();

    let out = serialize(&canvas);
    let (back, _) = parse(&out);
    assert_eq!(back.skipped.len() + back.nodes.len(), 1 + skipped_before);
    // The unknown-type node's payload is still present verbatim.
    assert!(out.contains("\"src\":\"clip.mp4\""));
    assert!(out.contains("42"));
}

/// An empty parse (`{}`) re-emits as `{}` until content exists.
#[test]
fn empty_document_stays_minimal() {
    let (canvas, _) = parse("{}");
    assert_eq!(serialize(&canvas), "{}\n");
    let (canvas, _) = parse("");
    assert_eq!(serialize(&canvas), "{}\n");
}
