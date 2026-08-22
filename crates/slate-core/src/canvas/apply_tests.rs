// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Mutation-engine tests (#361 write surface): per-op apply + invert
//! round-trips (byte-equal serialization, the t1 test contract), error
//! atomicity, and a seeded random-sequence census.

use super::*;
use crate::canvas::serialize::serialize;
use crate::canvas::{EndStyle, Side, parse};

const SAMPLE: &str = include_str!("../../tests/fixtures/canvas/sample.canvas");
const MALFORMED: &str = include_str!("../../tests/fixtures/canvas/malformed.canvas");

fn action(name: &str, ops: Vec<CanvasOp>) -> CanvasAction {
    CanvasAction {
        name: name.to_string(),
        ops,
    }
}

/// Apply, then apply the inverse: the file must serialize byte-equal
/// to the original, and the inverse's inverse must redo cleanly.
fn assert_invertible(input: &str, act: &CanvasAction) {
    let (original, _) = parse(input);
    let baseline = serialize(&original);

    let mut canvas = original.clone();
    let inverse = apply(&mut canvas, act).expect("action applies");
    let mutated = serialize(&canvas);
    assert_ne!(baseline, mutated, "action {:?} was a no-op", act.name);

    let redo = apply(&mut canvas, &inverse).expect("inverse applies");
    assert_eq!(
        serialize(&canvas),
        baseline,
        "undo of {:?} not byte-equal",
        act.name
    );

    // Redo: applying the inverse-of-the-inverse restores the mutation.
    let mut canvas2 = canvas.clone();
    apply(&mut canvas2, &redo).expect("redo applies");
    assert_eq!(
        serialize(&canvas2),
        mutated,
        "redo of {:?} drifted",
        act.name
    );
}

#[test]
fn every_op_kind_inverts_byte_equal() {
    let cases: Vec<(&str, Vec<CanvasOp>)> = vec![
        (
            "create text card",
            vec![CanvasOp::CreateNode {
                id: "new-card".into(),
                content: CanvasNodeContent::Text {
                    text: "fresh".into(),
                },
                x: 0.0,
                y: 700.0,
                width: 260.0,
                height: 140.0,
                color: Some("3".into()),
            }],
        ),
        (
            "create group",
            vec![CanvasOp::CreateGroup {
                id: "new-group".into(),
                label: Some("Q4".into()),
                x: 1000.0,
                y: 1000.0,
                width: 400.0,
                height: 300.0,
                color: None,
            }],
        ),
        (
            "move card",
            vec![CanvasOp::UpdateNodeGeometry {
                id: "card-loose".into(),
                x: 40.0,
                y: 480.0,
                width: 200.0,
                height: 100.0,
            }],
        ),
        (
            "set color",
            vec![CanvasOp::SetNodeColor {
                id: "card-evidence".into(),
                color: Some("5".into()),
            }],
        ),
        (
            "clear color",
            vec![CanvasOp::SetNodeColor {
                id: "card-question".into(),
                color: None,
            }],
        ),
        (
            "edit text",
            vec![CanvasOp::SetNodeContent {
                id: "card-evidence".into(),
                content: CanvasNodeContent::Text {
                    text: "Rewritten evidence".into(),
                },
            }],
        ),
        (
            "convert text card to file card",
            vec![CanvasOp::SetNodeContent {
                id: "card-loose".into(),
                content: CanvasNodeContent::File {
                    file: "notes/unfiled.md".into(),
                    subpath: None,
                },
            }],
        ),
        (
            "delete card with connections",
            vec![CanvasOp::DeleteNode {
                id: "card-question".into(), // three incident edges
            }],
        ),
        (
            "connect",
            vec![CanvasOp::AddEdge {
                id: "new-edge".into(),
                from_node: "card-loose".into(),
                from_side: Some(Side::Right),
                to_node: "card-notes".into(),
                to_side: None,
                from_end: EndStyle::None,
                to_end: EndStyle::Arrow,
                label: Some("relates".into()),
                color: None,
            }],
        ),
        (
            "edit connection",
            vec![CanvasOp::UpdateEdge {
                id: "edge-q-evidence".into(),
                from_side: None,
                to_side: Some(Side::Top),
                from_end: EndStyle::Arrow,
                to_end: EndStyle::Arrow,
                label: None, // clears the "supports" label
                color: Some("2".into()),
            }],
        ),
        (
            "delete connection",
            vec![CanvasOp::DeleteEdge {
                id: "edge-notes-spec".into(),
            }],
        ),
        (
            "rename group",
            vec![CanvasOp::RenameGroup {
                id: "grp-research".into(),
                label: Some("Findings".into()),
            }],
        ),
        (
            "clear group label",
            vec![CanvasOp::RenameGroup {
                id: "grp-research".into(),
                label: None,
            }],
        ),
        (
            "ungroup",
            vec![CanvasOp::Ungroup {
                id: "grp-inspiration".into(),
            }],
        ),
        (
            "bulk: move + color + connect as one action",
            vec![
                CanvasOp::UpdateNodeGeometry {
                    id: "card-loose".into(),
                    x: 0.0,
                    y: 640.0,
                    width: 200.0,
                    height: 100.0,
                },
                CanvasOp::SetNodeColor {
                    id: "card-loose".into(),
                    color: Some("6".into()),
                },
                CanvasOp::AddEdge {
                    id: "bulk-edge".into(),
                    from_node: "card-loose".into(),
                    from_side: None,
                    to_node: "card-evidence".into(),
                    to_side: None,
                    from_end: EndStyle::None,
                    to_end: EndStyle::Arrow,
                    label: None,
                    color: None,
                },
            ],
        ),
    ];
    for (name, ops) in cases {
        assert_invertible(SAMPLE, &action(name, ops));
    }
}

/// Deleting a node with tolerated-garbage/unknown fields restores them
/// intact (Restore ops carry the full original JSON).
#[test]
fn delete_restore_preserves_unknown_fields() {
    let input = include_str!("../../tests/fixtures/canvas/unknown_fields.canvas");
    assert_invertible(
        input,
        &action(
            "delete node with unknown fields",
            vec![CanvasOp::DeleteNode { id: "n1".into() }],
        ),
    );
    // Also across malformed-fixture edges with odd retained values.
    assert_invertible(
        MALFORMED,
        &action(
            "delete node touching odd edges",
            vec![CanvasOp::DeleteNode {
                id: "good-2".into(),
            }],
        ),
    );
}

#[test]
fn invalid_ops_reject_whole_action_atomically() {
    let (original, _) = parse(SAMPLE);
    let baseline = serialize(&original);
    let mut canvas = original.clone();

    // Second op is invalid: the first must not stick.
    let err = apply(
        &mut canvas,
        &action(
            "partial",
            vec![
                CanvasOp::SetNodeColor {
                    id: "card-loose".into(),
                    color: Some("2".into()),
                },
                CanvasOp::DeleteNode {
                    id: "no-such-node".into(),
                },
            ],
        ),
    )
    .unwrap_err();
    assert_eq!(err, ApplyError::UnknownNode("no-such-node".into()));
    assert_eq!(serialize(&canvas), baseline, "partial application leaked");

    // Assorted validation errors.
    let cases = vec![
        (
            CanvasOp::CreateNode {
                id: "card-loose".into(), // taken
                content: CanvasNodeContent::Text { text: "x".into() },
                x: 0.0,
                y: 0.0,
                width: 10.0,
                height: 10.0,
                color: None,
            },
            ApplyError::DuplicateId("card-loose".into()),
        ),
        (
            CanvasOp::AddEdge {
                id: "e-ghost".into(),
                from_node: "card-loose".into(),
                from_side: None,
                to_node: "ghost".into(),
                to_side: None,
                from_end: EndStyle::None,
                to_end: EndStyle::Arrow,
                label: None,
                color: None,
            },
            ApplyError::MissingEndpoint("ghost".into()),
        ),
        (
            CanvasOp::RenameGroup {
                id: "card-loose".into(),
                label: Some("nope".into()),
            },
            ApplyError::NotAGroup("card-loose".into()),
        ),
        (
            CanvasOp::SetNodeContent {
                id: "grp-research".into(),
                content: CanvasNodeContent::Text { text: "x".into() },
            },
            ApplyError::IsAGroup("grp-research".into()),
        ),
    ];
    for (op, expected) in cases {
        let mut canvas = original.clone();
        let err = apply(&mut canvas, &action("bad", vec![op])).unwrap_err();
        assert_eq!(err, expected);
        assert_eq!(serialize(&canvas), baseline);
    }
}

// --- Random-sequence census ------------------------------------------------

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
}

/// Random action sequences apply, then unwind via their inverses in
/// reverse order back to a byte-equal file — the mutation pipeline's
/// core undo guarantee at depth (single steps are covered above).
#[test]
fn census_random_action_sequences_unwind_byte_equal() {
    let rounds = if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        400
    } else {
        60
    };
    let (original, _) = parse(SAMPLE);
    let baseline = serialize(&original);

    for round in 0..rounds {
        let mut rng = Rng(0xA11C_0DE5 ^ (round as u64 + 1));
        let mut canvas = original.clone();
        let mut undo_stack: Vec<CanvasAction> = Vec::new();
        let mut created = 0usize;

        for step in 0..12 {
            let node_ids: Vec<String> = canvas.nodes.iter().map(|n| n.id.0.clone()).collect();
            let edge_ids: Vec<String> = canvas.edges.iter().map(|e| e.id.0.clone()).collect();
            let pick = |rng: &mut Rng, v: &[String]| v[rng.below(v.len() as u64) as usize].clone();

            let op = match rng.below(8) {
                0 => {
                    created += 1;
                    CanvasOp::CreateNode {
                        id: format!("r{round}s{step}c{created}"),
                        content: CanvasNodeContent::Text {
                            text: format!("card {step}"),
                        },
                        x: (rng.below(50) as f64) * 20.0,
                        y: (rng.below(50) as f64) * 20.0,
                        width: 200.0,
                        height: 100.0,
                        color: (rng.below(2) == 0).then(|| format!("{}", rng.below(6) + 1)),
                    }
                }
                1 if !node_ids.is_empty() => CanvasOp::UpdateNodeGeometry {
                    id: pick(&mut rng, &node_ids),
                    x: (rng.below(60) as f64) * 20.0 - 400.0,
                    y: (rng.below(60) as f64) * 20.0 - 400.0,
                    width: 100.0 + (rng.below(20) as f64) * 20.0,
                    height: 60.0 + (rng.below(10) as f64) * 20.0,
                },
                2 if !node_ids.is_empty() => CanvasOp::SetNodeColor {
                    id: pick(&mut rng, &node_ids),
                    color: (rng.below(3) != 0).then(|| format!("{}", rng.below(6) + 1)),
                },
                3 if !node_ids.is_empty() => CanvasOp::DeleteNode {
                    id: pick(&mut rng, &node_ids),
                },
                4 if node_ids.len() >= 2 => {
                    created += 1;
                    CanvasOp::AddEdge {
                        id: format!("r{round}s{step}e{created}"),
                        from_node: pick(&mut rng, &node_ids),
                        from_side: None,
                        to_node: pick(&mut rng, &node_ids),
                        to_side: None,
                        from_end: EndStyle::None,
                        to_end: EndStyle::Arrow,
                        label: (rng.below(2) == 0).then(|| format!("l{step}")),
                        color: None,
                    }
                }
                5 if !edge_ids.is_empty() => CanvasOp::DeleteEdge {
                    id: pick(&mut rng, &edge_ids),
                },
                6 if !edge_ids.is_empty() => CanvasOp::UpdateEdge {
                    id: pick(&mut rng, &edge_ids),
                    from_side: (rng.below(2) == 0).then_some(Side::Left),
                    to_side: None,
                    from_end: EndStyle::None,
                    to_end: EndStyle::Arrow,
                    label: (rng.below(2) == 0).then(|| "relabel".to_string()),
                    color: None,
                },
                _ if !node_ids.is_empty() => CanvasOp::SetNodeColor {
                    id: pick(&mut rng, &node_ids),
                    color: None,
                },
                _ => continue,
            };
            let act = action(&format!("census step {step}"), vec![op]);
            match apply(&mut canvas, &act) {
                Ok(inverse) => undo_stack.push(inverse),
                Err(_) => continue, // e.g. deleted both endpoints already — fine
            }
        }

        // Unwind everything: back to the exact original bytes.
        while let Some(inverse) = undo_stack.pop() {
            apply(&mut canvas, &inverse).expect("inverse always applies");
        }
        assert_eq!(
            serialize(&canvas),
            baseline,
            "round {round} did not unwind byte-equal"
        );
    }
}

/// #372 journal codec: every op kind round-trips through JSON.
#[test]
fn action_json_codec_round_trips_every_op() {
    let (original, _) = parse(SAMPLE);
    let mut canvas = original.clone();
    // Build a real inverse-bearing set: delete (Restore ops) + all
    // attribute mutations (InPlace ops) + creations.
    let act = action(
        "codec exercise",
        vec![
            CanvasOp::CreateNode {
                id: "codec-n".into(),
                content: CanvasNodeContent::File {
                    file: "notes/x.md".into(),
                    subpath: Some("#H".into()),
                },
                x: 0.0,
                y: 900.0,
                width: 100.0,
                height: 50.0,
                color: Some("2".into()),
            },
            CanvasOp::AddEdge {
                id: "codec-e".into(),
                from_node: "codec-n".into(),
                from_side: Some(Side::Left),
                to_node: "card-loose".into(),
                to_side: None,
                from_end: EndStyle::Arrow,
                to_end: EndStyle::Arrow,
                label: Some("L".into()),
                color: None,
            },
            CanvasOp::SetNodeColor {
                id: "card-question".into(),
                color: None,
            },
            CanvasOp::DeleteNode {
                id: "card-notes".into(),
            },
            CanvasOp::RenameGroup {
                id: "grp-research".into(),
                label: Some("Renamed".into()),
            },
        ],
    );
    let inverse = apply(&mut canvas, &act).unwrap();

    for action_value in [&act, &inverse] {
        let encoded = action_to_json(action_value);
        // Survives a serialize→parse cycle (what the journal stores).
        let reparsed: serde_json::Value = serde_json::from_str(&encoded.to_string()).unwrap();
        let decoded = action_from_json(&reparsed).unwrap();
        assert_eq!(&decoded, action_value);
    }
}

/// #960: JSON Canvas array order is z-order — "the first node in the
/// array should be displayed below all other nodes". A group created
/// around existing cards (the `canvasGroupMarked` shape: members'
/// bounding box + 40 pad) must precede them in the serialized array,
/// or every spec-faithful renderer — Slate's own opaque-fill renderer
/// included — paints the group frame over its members. Here the two
/// cards already sit inside the Research group, so the new group must
/// land ABOVE that outer group and BELOW its members.
#[test]
fn group_created_around_existing_cards_precedes_them() {
    // card-notes (0,180,240×140) + card-spec (260,180,220×140): bbox
    // (0,180)–(480,320), padded 40 → origin (-40,140), 560×220. The
    // Research group's center (240,160) falls inside that rect too; it
    // is LARGER, so it is the enclosing group, not a member.
    let act = action(
        "group 2 cards",
        vec![CanvasOp::CreateGroup {
            id: "grp-new".into(),
            label: Some("Pair".into()),
            x: -40.0,
            y: 140.0,
            width: 560.0,
            height: 220.0,
            color: None,
        }],
    );
    let (mut canvas, _) = parse(SAMPLE);
    apply(&mut canvas, &act).expect("applies");
    let idx = |id: &str| {
        canvas
            .nodes
            .iter()
            .position(|n| n.id.0 == id)
            .unwrap_or_else(|| panic!("{id} missing"))
    };
    assert!(
        idx("grp-new") < idx("card-notes") && idx("grp-new") < idx("card-spec"),
        "the group must sit below its members: order = {:?}",
        canvas
            .nodes
            .iter()
            .map(|n| n.id.0.as_str())
            .collect::<Vec<_>>()
    );
    assert!(
        idx("grp-research") < idx("grp-new"),
        "the enclosing outer group stays below the new inner group"
    );
    assert!(
        idx("card-evidence") < idx("grp-new"),
        "cards the group does not contain keep their place"
    );
    // The serialized file carries that order — it is what other
    // readers see.
    let out = serialize(&canvas);
    assert!(out.find("\"grp-new\"").unwrap() < out.find("\"card-notes\"").unwrap());
    // Inverse bookkeeping: DeleteNode undoes it, and RestoreNode's
    // recorded position redoes it byte-equal at the same index.
    assert_invertible(SAMPLE, &act);
}

/// #960: a group created around an existing GROUP (and its cards) goes
/// beneath that group — the sub-group is a member too.
#[test]
fn group_created_around_an_existing_group_goes_beneath_it() {
    // Inspiration (600,-40,320×400) padded 40 → (560,-80), 400×480.
    let act = action(
        "group the inspiration group",
        vec![CanvasOp::CreateGroup {
            id: "grp-outer".into(),
            label: None,
            x: 560.0,
            y: -80.0,
            width: 400.0,
            height: 480.0,
            color: None,
        }],
    );
    let (mut canvas, _) = parse(SAMPLE);
    apply(&mut canvas, &act).expect("applies");
    let idx = |id: &str| canvas.nodes.iter().position(|n| n.id.0 == id).unwrap();
    assert!(idx("grp-outer") < idx("grp-inspiration"));
    assert!(idx("grp-outer") < idx("card-jsoncanvas"));
    assert!(
        idx("grp-research") < idx("grp-outer"),
        "unrelated earlier nodes stay put"
    );
    assert_invertible(SAMPLE, &act);
}

/// #960: a group that contains nothing appends — there is nothing to
/// bury, and cards created inside it later append after it (above it).
#[test]
fn group_with_no_members_is_appended() {
    let act = action(
        "empty group",
        vec![CanvasOp::CreateGroup {
            id: "grp-empty".into(),
            label: Some("Later".into()),
            x: 5000.0,
            y: 5000.0,
            width: 300.0,
            height: 200.0,
            color: None,
        }],
    );
    let (mut canvas, _) = parse(SAMPLE);
    let before = canvas.nodes.len();
    apply(&mut canvas, &act).expect("applies");
    assert_eq!(canvas.nodes.last().unwrap().id.0, "grp-empty");
    assert_eq!(canvas.nodes.len(), before + 1);
    assert_invertible(SAMPLE, &act);
}
