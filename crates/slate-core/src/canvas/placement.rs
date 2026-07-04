// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canvas auto-placement engine — Milestone T, Wave 1 (#517).
//!
//! Computes non-overlapping positions for new/duplicated/moved cards so
//! a non-visual author never has to see the 2D plane (interview
//! decision 1): place adjacent to the anchor card, preference order
//! below → right → above → left, on an empty canvas at the canonical
//! origin `(0, 0)`; every placement returns a typed [`RelativeDesc`] so
//! the announcement layer (#518) phrases "Created text card below
//! 'Research'" without re-deriving geometry.
//!
//! ## Grid constants (single source of truth)
//!
//! Exported once, consumed by move/resize nudge steps (#521), the
//! serializer's coordinate discipline (#366), and default card sizing
//! (#368). Values are integers so committed coordinates stay integral
//! (matching what Obsidian writes).
//!
//! ## Algorithm
//!
//! Candidate slots are grid-aligned and start one [`DEFAULT_GAP`] from
//! the anchor. Ring 1 tries the four adjacent slots in preference
//! order (an explicit `direction_hint` — from create-connected-card,
//! #525 — is tried first); ring *r* pushes each direction *r − 1*
//! further slots out. If a pathological canvas defeats the ring search,
//! the fallback places the card just below the global bounding box —
//! guaranteed free. All checks are positive-area overlap against
//! *cards* (group frames don't block: placing inside a group area is
//! how cards join groups). Deterministic: a pure function of the model
//! and arguments.

use super::NodeId;
use super::model::{CanvasModel, Rect};

/// Nudge step for move/resize modes (#521) and the placement grid unit.
pub const GRID_STEP: f64 = 20.0;
/// Large (⇧) nudge step for move/resize modes (#521).
pub const GRID_STEP_LARGE: f64 = 100.0;
/// Default new-card size (#368).
pub const DEFAULT_CARD_WIDTH: f64 = 260.0;
/// Default new-card size (#368).
pub const DEFAULT_CARD_HEIGHT: f64 = 140.0;
/// Gap between a placed card and its anchor.
pub const DEFAULT_GAP: f64 = 40.0;

/// Placement directions in canonical preference order.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum PlaceDirection {
    Below,
    RightOf,
    Above,
    LeftOf,
}

const PREFERENCE: [PlaceDirection; 4] = [
    PlaceDirection::Below,
    PlaceDirection::RightOf,
    PlaceDirection::Above,
    PlaceDirection::LeftOf,
];

/// Typed relative-position description; the payload is the anchor's
/// display title. Phrasing/localization is UI-side (#518).
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum RelativeDesc {
    Below(String),
    RightOf(String),
    Above(String),
    LeftOf(String),
    AtOrigin,
}

impl RelativeDesc {
    fn new(direction: PlaceDirection, anchor_title: &str) -> RelativeDesc {
        let t = anchor_title.to_string();
        match direction {
            PlaceDirection::Below => RelativeDesc::Below(t),
            PlaceDirection::RightOf => RelativeDesc::RightOf(t),
            PlaceDirection::Above => RelativeDesc::Above(t),
            PlaceDirection::LeftOf => RelativeDesc::LeftOf(t),
        }
    }
}

/// A computed position for one new card.
#[derive(Debug, Clone, PartialEq)]
pub struct Placement {
    pub x: f64,
    pub y: f64,
    pub relative: RelativeDesc,
}

/// A computed position for a rigid set: one origin per input box, in
/// input order, pairwise offsets preserved exactly.
#[derive(Debug, Clone, PartialEq)]
pub struct SetPlacement {
    pub origins: Vec<(f64, f64)>,
    pub relative: RelativeDesc,
}

fn round_to_grid(v: f64) -> f64 {
    (v / GRID_STEP).round() * GRID_STEP
}

fn ceil_to_grid(v: f64) -> f64 {
    (v / GRID_STEP).ceil() * GRID_STEP
}

fn floor_to_grid(v: f64) -> f64 {
    (v / GRID_STEP).floor() * GRID_STEP
}

/// Slot origin for `direction` at ring distance `ring` (1-based) from
/// the anchor. Alignment always moves *away* from the anchor so the
/// gap is never rounded into an overlap.
fn slot(anchor: Rect, size: (f64, f64), direction: PlaceDirection, ring: usize) -> (f64, f64) {
    let (w, h) = size;
    let step_out_y = ceil_to_grid(h + DEFAULT_GAP);
    let step_out_x = ceil_to_grid(w + DEFAULT_GAP);
    let extra = (ring - 1) as f64;
    match direction {
        PlaceDirection::Below => (
            round_to_grid(anchor.x0),
            ceil_to_grid(anchor.y1 + DEFAULT_GAP) + extra * step_out_y,
        ),
        PlaceDirection::RightOf => (
            ceil_to_grid(anchor.x1 + DEFAULT_GAP) + extra * step_out_x,
            round_to_grid(anchor.y0),
        ),
        PlaceDirection::Above => (
            round_to_grid(anchor.x0),
            floor_to_grid(anchor.y0 - DEFAULT_GAP - h) - extra * step_out_y,
        ),
        PlaceDirection::LeftOf => (
            floor_to_grid(anchor.x0 - DEFAULT_GAP - w) - extra * step_out_x,
            round_to_grid(anchor.y0),
        ),
    }
}

const RING_LIMIT: usize = 512;

fn search(
    model: &CanvasModel,
    anchor: Rect,
    size: (f64, f64),
    hint: Option<PlaceDirection>,
    exclude: &[NodeId],
) -> ((f64, f64), PlaceDirection) {
    let mut order: Vec<PlaceDirection> = Vec::with_capacity(4);
    if let Some(h) = hint {
        order.push(h);
    }
    order.extend(PREFERENCE.iter().copied().filter(|d| Some(*d) != hint));

    let free = |x: f64, y: f64| {
        !model
            .spatial
            .any_overlap(Rect::new(x, y, size.0, size.1), exclude, false)
    };

    for ring in 1..=RING_LIMIT {
        for &dir in &order {
            let (x, y) = slot(anchor, size, dir, ring);
            if free(x, y) {
                return ((x, y), dir);
            }
        }
    }

    // Pathological fallback: just below the global bounding box —
    // nothing exists there, so it is always free.
    let bounds = model
        .spatial
        .bounds()
        .expect("search is only called on non-empty canvases");
    (
        (
            round_to_grid(anchor.x0),
            ceil_to_grid(bounds.y1 + DEFAULT_GAP),
        ),
        PlaceDirection::Below,
    )
}

fn anchor_title(model: &CanvasModel, id: &NodeId) -> String {
    model
        .summaries
        .get(id)
        .map(|s| s.display_title.clone())
        .unwrap_or_default()
}

/// Effective anchor: the given one if it exists, else the last node in
/// reading order (deterministic default when nothing is selected).
fn resolve_anchor<'m>(model: &'m CanvasModel, anchor: Option<&'m NodeId>) -> Option<&'m NodeId> {
    anchor
        .filter(|id| model.spatial.rect_of(id).is_some())
        .or_else(|| model.reading_order.last())
}

/// Compute a non-overlapping, grid-aligned position for one new card.
///
/// `exclude` removes nodes from collision checks — pass the moving
/// card's own id when re-placing an existing card (#522).
pub fn place_new(
    model: &CanvasModel,
    anchor: Option<&NodeId>,
    size: (f64, f64),
    hint: Option<PlaceDirection>,
    exclude: &[NodeId],
) -> Placement {
    let Some(anchor_id) = resolve_anchor(model, anchor) else {
        return Placement {
            x: 0.0,
            y: 0.0,
            relative: RelativeDesc::AtOrigin,
        };
    };
    let anchor_rect = model
        .spatial
        .rect_of(anchor_id)
        .expect("resolve_anchor verified presence");
    let ((x, y), dir) = search(model, anchor_rect, size, hint, exclude);
    Placement {
        x,
        y,
        relative: RelativeDesc::new(dir, &anchor_title(model, anchor_id)),
    }
}

/// Rigid-set placement (#522/#524/#525): the set's bounding box is
/// placed by the same slot search, then each box gets its origin back
/// with pairwise offsets preserved exactly.
pub fn place_set(
    model: &CanvasModel,
    anchor: Option<&NodeId>,
    boxes: &[Rect],
    hint: Option<PlaceDirection>,
    exclude: &[NodeId],
) -> SetPlacement {
    if boxes.is_empty() {
        return SetPlacement {
            origins: Vec::new(),
            relative: RelativeDesc::AtOrigin,
        };
    }
    let bbox = boxes.iter().skip(1).fold(boxes[0], |acc, b| Rect {
        x0: acc.x0.min(b.x0),
        y0: acc.y0.min(b.y0),
        x1: acc.x1.max(b.x1),
        y1: acc.y1.max(b.y1),
    });
    let size = (bbox.width(), bbox.height());

    let (bbox_origin, relative) = match resolve_anchor(model, anchor) {
        None => ((0.0, 0.0), RelativeDesc::AtOrigin),
        Some(anchor_id) => {
            let anchor_rect = model
                .spatial
                .rect_of(anchor_id)
                .expect("resolve_anchor verified presence");
            let ((x, y), dir) = search(model, anchor_rect, size, hint, exclude);
            (
                (x, y),
                RelativeDesc::new(dir, &anchor_title(model, anchor_id)),
            )
        }
    };

    let origins = boxes
        .iter()
        .map(|b| {
            (
                bbox_origin.0 + (b.x0 - bbox.x0),
                bbox_origin.1 + (b.y0 - bbox.y0),
            )
        })
        .collect();
    SetPlacement { origins, relative }
}

#[cfg(test)]
#[path = "placement_tests.rs"]
mod tests;
