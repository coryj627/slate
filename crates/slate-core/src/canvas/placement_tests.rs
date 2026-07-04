// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Placement engine tests (#517): unit expectations plus the
//! non-overlap census (random canvases × random anchors × exhaustive
//! direction hints). `SLATE_CENSUS_FULL=1` (release) runs full scale.

use super::*;
use crate::canvas::model::{CanvasModel, Rect, derive};
use crate::canvas::{NodeId, parse};

const SAMPLE: &str = include_str!("../../tests/fixtures/canvas/sample.canvas");

fn id(s: &str) -> NodeId {
    NodeId(s.to_string())
}

fn model_of(input: &str) -> CanvasModel {
    let (canvas, _) = parse(input);
    derive(&canvas)
}

const SIZE: (f64, f64) = (DEFAULT_CARD_WIDTH, DEFAULT_CARD_HEIGHT);

fn grid_aligned(v: f64) -> bool {
    (v / GRID_STEP).fract() == 0.0
}

#[test]
fn empty_canvas_places_at_origin() {
    let model = model_of("{}");
    let p = place_new(&model, None, SIZE, None, &[]);
    assert_eq!((p.x, p.y), (0.0, 0.0));
    assert_eq!(p.relative, RelativeDesc::AtOrigin);
}

#[test]
fn prefers_below_the_anchor() {
    let model = model_of(SAMPLE);
    // card-loose (0,460,200,100) has free space below it.
    let anchor = id("card-loose");
    let p = place_new(&model, Some(&anchor), SIZE, None, &[]);
    assert_eq!(p.relative, RelativeDesc::Below("Unfiled thought".into()));
    let anchor_rect = model.spatial.rect_of(&anchor).unwrap();
    assert!(p.y >= anchor_rect.y1 + DEFAULT_GAP);
    assert!(grid_aligned(p.x) && grid_aligned(p.y));
    assert!(
        !model
            .spatial
            .any_overlap(Rect::new(p.x, p.y, SIZE.0, SIZE.1), &[], false)
    );
}

#[test]
fn honors_direction_hint() {
    let model = model_of(SAMPLE);
    let anchor = id("card-loose");
    let p = place_new(
        &model,
        Some(&anchor),
        SIZE,
        Some(PlaceDirection::RightOf),
        &[],
    );
    assert_eq!(p.relative, RelativeDesc::RightOf("Unfiled thought".into()));
    let anchor_rect = model.spatial.rect_of(&anchor).unwrap();
    assert!(p.x >= anchor_rect.x1 + DEFAULT_GAP);
}

#[test]
fn missing_anchor_falls_back_to_last_in_reading_order() {
    let model = model_of(SAMPLE);
    let ghost = id("no-such-node");
    let with_ghost = place_new(&model, Some(&ghost), SIZE, None, &[]);
    let with_none = place_new(&model, None, SIZE, None, &[]);
    assert_eq!(with_ghost, with_none);
    // Last in reading order is card-loose.
    assert!(matches!(&with_none.relative, RelativeDesc::Below(t) if t == "Unfiled thought"));
}

#[test]
fn dense_ring_expands_until_free() {
    // Anchor at origin, completely walled in by a 7×7 block of cards
    // (200×200 each, no gaps) centred on it.
    let mut nodes = Vec::new();
    for gy in -3i64..=3 {
        for gx in -3i64..=3 {
            let idn = format!("w{}_{}", gx + 3, gy + 3);
            nodes.push(serde_json::json!({
                "id": if (gx, gy) == (0, 0) { "anchor".to_string() } else { idn },
                "type": "text", "text": "wall",
                "x": (gx * 200) as f64, "y": (gy * 200) as f64,
                "width": 200.0, "height": 200.0
            }));
        }
    }
    let doc = serde_json::json!({"nodes": nodes, "edges": []}).to_string();
    let model = model_of(&doc);
    let anchor = id("anchor");
    let p = place_new(&model, Some(&anchor), SIZE, None, &[]);
    assert!(
        !model
            .spatial
            .any_overlap(Rect::new(p.x, p.y, SIZE.0, SIZE.1), &[], false),
        "dense placement overlaps"
    );
    assert!(grid_aligned(p.x) && grid_aligned(p.y));
}

#[test]
fn exclude_lets_a_card_be_replaced_next_to_its_anchor() {
    let model = model_of(SAMPLE);
    // Re-place card-evidence next to card-question ("Place below X",
    // #522): its own current rect must not block the search. Below and
    // ring-2 slots are occupied (card-notes, card-loose), so the search
    // lands right of the anchor — in exactly the slot the moving card
    // currently occupies, which only works because it is excluded.
    let anchor = id("card-question");
    let moving = id("card-evidence");
    let p = place_new(
        &model,
        Some(&anchor),
        (220.0, 140.0),
        None,
        std::slice::from_ref(&moving),
    );
    assert!(matches!(&p.relative, RelativeDesc::RightOf(t) if t == "Core question"));
    assert_eq!((p.x, p.y), (280.0, 0.0));
    assert!(
        !model
            .spatial
            .any_overlap(Rect::new(p.x, p.y, 220.0, 140.0), &[moving], false)
    );
}

#[test]
fn place_set_preserves_pairwise_offsets() {
    let model = model_of(SAMPLE);
    let boxes = [
        Rect::new(1000.0, 1000.0, 100.0, 50.0),
        Rect::new(1150.0, 1030.0, 80.0, 40.0),
        Rect::new(1000.0, 1100.0, 200.0, 60.0),
    ];
    let anchor = id("card-loose");
    let sp = place_set(&model, Some(&anchor), &boxes, None, &[]);
    assert_eq!(sp.origins.len(), 3);
    // Pairwise offsets preserved exactly.
    for (i, j) in [(0usize, 1usize), (0, 2), (1, 2)] {
        let dx_in = boxes[j].x0 - boxes[i].x0;
        let dy_in = boxes[j].y0 - boxes[i].y0;
        let dx_out = sp.origins[j].0 - sp.origins[i].0;
        let dy_out = sp.origins[j].1 - sp.origins[i].1;
        assert_eq!((dx_in, dy_in), (dx_out, dy_out));
    }
    // Every placed box is free of existing cards.
    for (orig, b) in sp.origins.iter().zip(&boxes) {
        let r = Rect::new(orig.0, orig.1, b.width(), b.height());
        assert!(!model.spatial.any_overlap(r, &[], false));
    }
    assert!(matches!(sp.relative, RelativeDesc::Below(_)));

    // Empty set: no origins, no panic.
    let empty = place_set(&model, Some(&anchor), &[], None, &[]);
    assert!(empty.origins.is_empty());
}

#[test]
fn deterministic() {
    let model = model_of(SAMPLE);
    let anchor = id("card-question");
    for hint in [
        None,
        Some(PlaceDirection::Below),
        Some(PlaceDirection::LeftOf),
    ] {
        let a = place_new(&model, Some(&anchor), SIZE, hint, &[]);
        let b = place_new(&model, Some(&anchor), SIZE, hint, &[]);
        assert_eq!(a, b);
    }
}

// --- Census ---------------------------------------------------------------

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

/// Random canvases × random anchors × exhaustive hints: the placement
/// never overlaps a card, is grid-aligned, and is deterministic.
#[test]
fn census_placement_never_overlaps() {
    let (rounds, max_nodes) = if std::env::var("SLATE_CENSUS_FULL").as_deref() == Ok("1") {
        (400u64, 250usize)
    } else {
        (60u64, 80usize)
    };
    let mut rng = Rng(0x9E37_79B9_7F4A_7C15);
    let hints = [
        None,
        Some(PlaceDirection::Below),
        Some(PlaceDirection::RightOf),
        Some(PlaceDirection::Above),
        Some(PlaceDirection::LeftOf),
    ];
    for _ in 0..rounds {
        let n = rng.below(max_nodes as u64 + 1) as usize;
        let mut nodes = Vec::new();
        for i in 0..n {
            let x = (rng.pick_i(-30, 30) * 20) as f64;
            let y = (rng.pick_i(-30, 30) * 20) as f64;
            let (w, h) = match rng.below(5) {
                0 => (0.0, 0.0),
                1 => (60.0, 60.0),
                2 => (200.0, 100.0),
                3 => (400.0, 600.0),
                _ => (-140.0, 90.0),
            };
            let ty = if rng.below(5) == 0 { "group" } else { "text" };
            nodes.push(serde_json::json!({
                "id": format!("n{i}"), "type": ty,
                "text": if ty == "text" { serde_json::json!(format!("c{i}")) } else { serde_json::Value::Null },
                "x": x, "y": y, "width": w, "height": h
            }));
        }
        // Remove nulls (group nodes carry no text key).
        for node in &mut nodes {
            let obj = node.as_object_mut().unwrap();
            if obj.get("text") == Some(&serde_json::Value::Null) {
                obj.shift_remove("text");
            }
        }
        let doc = serde_json::json!({"nodes": nodes, "edges": []}).to_string();
        let model = model_of(&doc);

        let anchor = if n == 0 {
            None
        } else {
            Some(id(&format!("n{}", rng.below(n as u64))))
        };
        let sizes = [SIZE, (60.0, 40.0), (500.0, 400.0)];
        for hint in hints {
            for size in sizes {
                let p = place_new(&model, anchor.as_ref(), size, hint, &[]);
                let rect = Rect::new(p.x, p.y, size.0, size.1);
                assert!(
                    !model.spatial.any_overlap(rect, &[], false),
                    "overlap: n={n} hint={hint:?} size={size:?} at ({}, {})",
                    p.x,
                    p.y
                );
                assert!(grid_aligned(p.x) && grid_aligned(p.y), "off-grid");
                assert_eq!(p, place_new(&model, anchor.as_ref(), size, hint, &[]));
                if n == 0 {
                    assert_eq!(p.relative, RelativeDesc::AtOrigin);
                } else {
                    assert_ne!(p.relative, RelativeDesc::AtOrigin);
                }
            }
        }

        // Rigid sets: random 1–4 boxes.
        let count = 1 + rng.below(4) as usize;
        let boxes: Vec<Rect> = (0..count)
            .map(|_| {
                Rect::new(
                    rng.pick_i(-400, 400) as f64,
                    rng.pick_i(-400, 400) as f64,
                    (10 + rng.below(300)) as f64,
                    (10 + rng.below(300)) as f64,
                )
            })
            .collect();
        let sp = place_set(&model, anchor.as_ref(), &boxes, None, &[]);
        for (i, (orig, b)) in sp.origins.iter().zip(&boxes).enumerate() {
            let r = Rect::new(orig.0, orig.1, b.width(), b.height());
            assert!(
                !model.spatial.any_overlap(r, &[], false),
                "set member {i} overlaps"
            );
            // Offsets preserved relative to member 0.
            assert_eq!(orig.0 - sp.origins[0].0, b.x0 - boxes[0].x0);
            assert_eq!(orig.1 - sp.origins[0].1, b.y0 - boxes[0].y0);
        }
    }
}
