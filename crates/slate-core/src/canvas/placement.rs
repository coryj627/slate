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
/// Gap between a placed card and its anchor. Also the pad a group frame
/// keeps around the cards it was built from (W6-1 0b-11: mac's literal
/// `pad = 40.0` in `AppState+CanvasActions.swift` is this constant).
pub const DEFAULT_GAP: f64 = 40.0;
/// Default new-group size (#368; W6-1 §2 row H — mac spelled it twice
/// in `AppState+CanvasActions.swift`).
pub const DEFAULT_GROUP_WIDTH: f64 = 400.0;
/// Default new-group size (#368; W6-1 §2 row H).
pub const DEFAULT_GROUP_HEIGHT: f64 = 300.0;
/// Floor on either card dimension in resize mode (#521).
///
/// **Semantics are REFUSE, not clamp** (W6-1 contracts doc, "Mac details
/// recorded while reading"): a step that would take *either* dimension
/// below this leaves *both* unchanged and announces
/// [`crate::a11y::CanvasA11yEvent::CanvasResizeClamped`]. Core owns the
/// number; the refusal rule is the mode controller's.
pub const MIN_CARD_SIZE: f64 = 40.0;

/// The grid/sizing constants a host needs before it has opened anything
/// (W6-1 contract 0b-4). Every field IS the `pub const` of the same name
/// in this module — a host that re-types one of these numbers is the
/// duplication §W-G exists to delete, and `constants_are_the_module_constants`
/// asserts the identity field by field.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Constants {
    pub grid_step: f64,
    pub grid_step_large: f64,
    pub default_card_w: f64,
    pub default_card_h: f64,
    pub default_group_w: f64,
    pub default_group_h: f64,
    pub default_gap: f64,
    pub min_card_size: f64,
}

/// The constants, as data. Pure and handle-free (contract 0b-4 / CD-17):
/// a caller must not need an open canvas to read a number.
pub fn constants() -> Constants {
    Constants {
        grid_step: GRID_STEP,
        grid_step_large: GRID_STEP_LARGE,
        default_card_w: DEFAULT_CARD_WIDTH,
        default_card_h: DEFAULT_CARD_HEIGHT,
        default_group_w: DEFAULT_GROUP_WIDTH,
        default_group_h: DEFAULT_GROUP_HEIGHT,
        default_gap: DEFAULT_GAP,
        min_card_size: MIN_CARD_SIZE,
    }
}

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

/// Outcome of [`place_inside_group`] (W6-1 contract 0b-12). Three
/// outcomes rather than one point, so a host can never receive a
/// position outside the group it asked about — the failure mac ships,
/// where a full group pushes the card out and containment silently
/// un-parents it.
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum InsideGroupPlacement {
    /// A free, group-aligned slot fully inside the group frame.
    Placed { x: f64, y: f64 },
    /// No candidate slot fits inside the group at all. The point is the
    /// inset itself (mac's `(x + 20, y + 40)`), NOT checked for overlap
    /// — the caller decides whether to refuse.
    TooSmall { x: f64, y: f64 },
    /// Slots fit, but every one examined is occupied.
    Full,
}

/// Place a card of `size` inside `group_rect`, clipped to it (#521/#523,
/// W6-1 §2 row H).
///
/// Candidate slots are the lattice anchored at the group's inset
/// top-left `(x0 + GRID_STEP, y0 + 2 · GRID_STEP)` — mac's
/// `(x + 20, y + 40)` — stepping one slot-plus-gap at a time, and only
/// slots lying wholly inside the group frame are candidates. They are
/// visited COLUMN by column, each column top to bottom: that is
/// [`PREFERENCE`]'s `Below` before `RightOf`, applied to a lattice
/// instead of to a ring. `Above` and `LeftOf` are unreachable because
/// the lattice starts at the group's inset top-left corner, which is
/// the point of clipping to the group. Overlap is checked against
/// *cards* only, exactly as [`place_new`] does — a group frame never
/// blocks placement inside it.
///
/// At most [`RING_LIMIT`] candidates are examined, the same budget the
/// ring search spends, so a pathological group cannot make this
/// unbounded.
pub fn place_inside_group(
    model: &CanvasModel,
    group_rect: Rect,
    size: (f64, f64),
    exclude: &[NodeId],
) -> InsideGroupPlacement {
    let (w, h) = size;
    let inset = (group_rect.x0 + GRID_STEP, group_rect.y0 + 2.0 * GRID_STEP);
    let step_x = ceil_to_grid(w + DEFAULT_GAP);
    let step_y = ceil_to_grid(h + DEFAULT_GAP);

    // A slot must fit inside the frame, and the lattice must actually
    // advance. Non-finite geometry fails both tests and lands here
    // rather than looping.
    let fits = |x: f64, y: f64| {
        x >= group_rect.x0 && y >= group_rect.y0 && x + w <= group_rect.x1 && y + h <= group_rect.y1
    };
    if !fits(inset.0, inset.1)
        || !(step_x.is_finite() && step_x > 0.0)
        || !(step_y.is_finite() && step_y > 0.0)
    {
        return InsideGroupPlacement::TooSmall {
            x: inset.0,
            y: inset.1,
        };
    }

    let mut examined = 0usize;
    let mut x = inset.0;
    while fits(x, inset.1) {
        let mut y = inset.1;
        while fits(x, y) {
            if examined >= RING_LIMIT {
                return InsideGroupPlacement::Full;
            }
            examined += 1;
            if !model
                .spatial
                .any_overlap(Rect::new(x, y, w, h), exclude, false)
            {
                return InsideGroupPlacement::Placed { x, y };
            }
            y += step_y;
        }
        x += step_x;
    }
    InsideGroupPlacement::Full
}

#[cfg(test)]
#[path = "placement_tests.rs"]
mod tests;
