// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canvas structural queries — W6-1 PR 0b (#745), §W-G rows B–M.
//!
//! Every rule in here was derived in Swift, once or three times, inside
//! the mac canvas (`docs/plans/18_windows_port/specs/w6_1_canvas_spec.md`
//! §2). Each is now one pure function of the derived [`CanvasModel`], so
//! the two hosts answer the same question the same way and neither owns
//! a copy (R-D, "no re-derivation"). The contracts are
//! `docs/plans/34_canvas_contracts.md` §"PR 0b"; the algorithms and
//! their mac citations are `0b-3` and `0b-7` … `0b-14`.
//!
//! Nothing here touches SQLite or the session registry: these are
//! functions of the model, and the session layer is a handle lookup
//! away from each of them.

use super::model::{CanvasModel, EdgeDirection, Rect};
use super::placement::{DEFAULT_GAP, RelativeDesc};
use super::{EdgeId, NodeId, Side};

// ---------------------------------------------------------------------------
// Row C — auto-side selection (contract 0b-3)

/// The pair of attachment sides a new connection should use, from the
/// two endpoint rects (`AppState+CanvasConnect.swift:24–33`, and the
/// renderer's `anchorPoint` `case nil` arm, which this replaces too).
///
/// Rects rather than node ids because create-connected-card asks about
/// a card that does not exist yet (CD-16). Horizontal wins only on a
/// STRICT `|dx| > |dy|`, so the diagonal tie — and the self-loop, where
/// both deltas are zero — resolves vertical and answers
/// `(Top, Bottom)`.
pub fn auto_sides(from: Rect, to: Rect) -> (Side, Side) {
    let (fx, fy) = from.center();
    let (tx, ty) = to.center();
    let (dx, dy) = (tx - fx, ty - fy);
    if dx.abs() > dy.abs() {
        if dx > 0.0 {
            (Side::Right, Side::Left)
        } else {
            (Side::Left, Side::Right)
        }
    } else if dy > 0.0 {
        (Side::Bottom, Side::Top)
    } else {
        (Side::Top, Side::Bottom)
    }
}

// ---------------------------------------------------------------------------
// Row D — containment (contract 0b-8)

/// The node's containing group, if any. `None` is "at canvas level";
/// callers distinguish that from "no such node" by checking the model
/// first (the session layer answers `bad_node`).
pub fn parent_of(model: &CanvasModel, node: &NodeId) -> Option<NodeId> {
    model.tree.parent.get(node).cloned()
}

/// The group's direct children in sibling (reading) order. Empty for a
/// childless group and for a node that is not a group at all.
pub fn children_of(model: &CanvasModel, group: &NodeId) -> Vec<NodeId> {
    model
        .tree
        .children
        .get(group)
        .cloned()
        .unwrap_or_else(Vec::new)
}

// ---------------------------------------------------------------------------
// Row E — trace path (contract 0b-9)

/// One hop of a traced path: the connection followed and where it led.
#[derive(Debug, Clone, PartialEq)]
pub struct TraceHop {
    pub edge: EdgeId,
    pub node: NodeId,
    /// The arrived-at card's `display_title`.
    pub title: String,
    /// The connection's label, if it carries one.
    pub label: Option<String>,
}

/// Follow outgoing connections greedily from `start`, cycle-safe
/// (`AppState+CanvasNavigation.swift:129–156`).
///
/// Eligible neighbours are `Outgoing` and `Bidirectional`;
/// `Undirected` — a connection with no arrowhead at either end — is not
/// traversable and is skipped, exactly as mac has it. Neighbours are
/// visited in edge document order, the seen set is keyed by NODE, and
/// the walk stops at the first node all of whose eligible neighbours
/// are already seen, so it terminates on every canvas (a self-loop ends
/// it at once, since the node is seen before its own edges are read).
///
/// The hops EXCLUDE `start`: a dead end returns an empty list, and
/// mac's `visited.count` is `hops.len() + 1`.
pub fn trace_path(model: &CanvasModel, start: &NodeId) -> Vec<TraceHop> {
    let mut hops: Vec<TraceHop> = Vec::new();
    let mut seen: std::collections::HashSet<&NodeId> = std::collections::HashSet::new();
    seen.insert(start);
    let mut current = start;
    while let Some(neighbors) = model.adjacency.get(current) {
        let Some(next) = neighbors.iter().find(|n| {
            matches!(
                n.direction,
                EdgeDirection::Outgoing | EdgeDirection::Bidirectional
            ) && !seen.contains(&n.other)
        }) else {
            break;
        };
        hops.push(TraceHop {
            edge: next.edge.clone(),
            node: next.other.clone(),
            title: model
                .summaries
                .get(&next.other)
                .map(|s| s.display_title.clone())
                .unwrap_or_default(),
            label: next.label.clone(),
        });
        seen.insert(&next.other);
        current = &next.other;
    }
    hops
}

// ---------------------------------------------------------------------------
// Row F — reading-order projection (contract 0b-10)

/// Project a set of ids onto reading order
/// (`AppState+CanvasActions.swift:497–503, 727–729`).
///
/// Ids not in the canvas are dropped silently — mac drops stale marks
/// the same way, and erroring would make one stale mark fatal to a bulk
/// verb. Duplicates in the input collapse onto the single reading-order
/// position. One pass over the reading order, so O(N + M).
pub fn order_nodes(model: &CanvasModel, ids: &[NodeId]) -> Vec<NodeId> {
    let wanted: std::collections::HashSet<&NodeId> = ids.iter().collect();
    model
        .reading_order
        .iter()
        .filter(|id| wanted.contains(id))
        .cloned()
        .collect()
}

// ---------------------------------------------------------------------------
// Row B — relative description (contract 0b-7)

/// Describe where `rect` sits relative to its nearest non-excluded,
/// non-group neighbours (`AppState+CanvasModes.swift:299–339`).
///
/// Returns zero, one or two descriptions, in mac's order. Zero means
/// there were no candidates at all — the caller renders that as
/// `Alone on the canvas` through 0a's `CanvasMoveRelative`, which takes
/// this very list; this function never invents copy.
///
/// The sense is INVERTED relative to the centre delta, because the
/// phrase describes where the SUBJECT is: a neighbour below the subject
/// (`dy > 0`) makes the subject `Above` it. The axis is vertical when
/// `|dy| >= |dx|`, so an exact diagonal — and an exact centre
/// coincidence — goes vertical.
///
/// Mac's `sort(by:)` is unstable, so equidistant neighbours ordered
/// arbitrarily; core's order is `(squared distance, document index)`
/// with non-finite distances collapsed to `+∞` so they rank last and
/// still tie-break by document index (CD-19).
pub fn describe_relative(model: &CanvasModel, rect: Rect, exclude: &[NodeId]) -> Vec<RelativeDesc> {
    let (cx, cy) = rect.center();
    let excluded: std::collections::HashSet<&NodeId> = exclude.iter().collect();

    struct Candidate<'m> {
        id: &'m NodeId,
        dx: f64,
        dy: f64,
        doc: usize,
    }
    let mut candidates: Vec<Candidate> = model
        .spatial
        .nodes()
        .enumerate()
        .filter(|(_, (id, _, is_group))| !*is_group && !excluded.contains(id))
        .map(|(doc, (id, r, _))| {
            let (nx, ny) = r.center();
            Candidate {
                id,
                dx: nx - cx,
                dy: ny - cy,
                doc,
            }
        })
        .collect();
    // Hostile geometry can make a delta non-finite; ranking those as
    // `+∞` keeps the order total and deterministic instead of leaving
    // it to NaN comparison (the `canvasSafeInt` precedent).
    let distance = |c: &Candidate| {
        let d = c.dx * c.dx + c.dy * c.dy;
        if d.is_finite() { d } else { f64::INFINITY }
    };
    candidates.sort_by(|a, b| {
        distance(a)
            .total_cmp(&distance(b))
            .then_with(|| a.doc.cmp(&b.doc))
    });

    let title = |c: &Candidate| {
        model
            .summaries
            .get(c.id)
            .map(|s| s.display_title.clone())
            .unwrap_or_default()
    };
    let is_vertical = |c: &Candidate| c.dy.abs() >= c.dx.abs();
    let phrase = |c: &Candidate| {
        if is_vertical(c) {
            if c.dy < 0.0 {
                RelativeDesc::Below(title(c))
            } else {
                RelativeDesc::Above(title(c))
            }
        } else if c.dx < 0.0 {
            RelativeDesc::RightOf(title(c))
        } else {
            RelativeDesc::LeftOf(title(c))
        }
    };

    let Some(nearest) = candidates.first() else {
        return Vec::new();
    };
    let mut out = vec![phrase(nearest)];
    // A second, axis-DISTINCT neighbour completes the fix.
    let vertical = is_vertical(nearest);
    if let Some(second) = candidates
        .iter()
        .skip(1)
        .find(|c| is_vertical(c) != vertical)
    {
        out.push(phrase(second));
    }
    out
}

// ---------------------------------------------------------------------------
// Row H — bounds and group geometry (contract 0b-11)

/// Bounding box of every node, group frames included — what
/// `canvasFitCanvas` re-unions in Swift. `None` on an empty canvas.
pub fn bounds(model: &CanvasModel) -> Option<Rect> {
    model.spatial.bounds()
}

/// The group frame that would enclose `members`: their union inflated
/// by [`DEFAULT_GAP`] on all four sides
/// (`AppState+CanvasActions.swift:791–812`, whose literal `pad = 40.0`
/// IS that constant).
///
/// Members not in the canvas are skipped; a set with no resolvable
/// member returns `None` — mac's `guard minX.isFinite else { return }`
/// typed rather than silent (CD-24).
pub fn group_rect_around(model: &CanvasModel, members: &[NodeId]) -> Option<Rect> {
    let mut union: Option<Rect> = None;
    for id in members {
        let Some(r) = model.spatial.rect_of(id) else {
            continue;
        };
        union = Some(match union {
            None => r,
            Some(acc) => Rect {
                x0: acc.x0.min(r.x0),
                y0: acc.y0.min(r.y0),
                x1: acc.x1.max(r.x1),
                y1: acc.y1.max(r.y1),
            },
        });
    }
    union.map(|r| Rect {
        x0: r.x0 - DEFAULT_GAP,
        y0: r.y0 - DEFAULT_GAP,
        x1: r.x1 + DEFAULT_GAP,
        y1: r.y1 + DEFAULT_GAP,
    })
}

// ---------------------------------------------------------------------------
// Row K — the filter predicate (contract 0b-13 / 0b-14)

/// Node ids matching `query`, in reading order
/// (`CanvasDocument.swift:238–258`).
///
/// An empty or whitespace-only query matches EVERYTHING — mac's
/// `filteredOutline` short-circuits to the unfiltered list, and
/// returning nothing here would invert every consuming UI. Otherwise
/// the needle must be contained, case-folded, in the display title, the
/// kind type word, ANY ONE element of the group path, or the target.
///
/// Matching the kind word means typing `group` selects every group and
/// `link` matches every link card *and* any title containing "link";
/// matching the group path per element means a needle spanning the
/// separator never matches. Both are shipped mac behaviour, preserved
/// deliberately.
///
/// Both sides are lowercased with [`str::to_lowercase`] before the
/// containment test. That is Unicode's `Lowercase_Mapping`,
/// locale-independent and NOT a case fold: it is full (one scalar can
/// become several — `İ` U+0130 lowercases to `i` + combining dot
/// above), and it leaves the pairs case folding would collapse (`ß`
/// stays `ß` and never matches `SS`; final sigma is the documented
/// exception it does handle). So this diverges from mac's
/// `localizedCaseInsensitiveContains` — locale-sensitive, no full
/// mapping — AND from a strict simple case fold, in different places.
/// The three agree across ASCII and Latin-1, which is the whole fixture
/// corpus; `filter_folds_case_the_unicode_way_not_the_turkish_way`
/// pins the cases where they do not. Trimming is Rust's, which unlike
/// Swift's `.whitespaces` also removes newlines. (CD-22.)
pub fn filter(model: &CanvasModel, query: &str) -> Vec<NodeId> {
    let needle = query.trim();
    if needle.is_empty() {
        return model.reading_order.clone();
    }
    let needle = needle.to_lowercase();
    let hit = |haystack: &str| haystack.to_lowercase().contains(&needle);
    model
        .reading_order
        .iter()
        .filter(|id| {
            model.summaries.get(*id).is_some_and(|s| {
                hit(&s.display_title)
                    || hit(s.kind_label)
                    || s.group_path.iter().any(|g| hit(g))
                    || hit(&s.target)
            })
        })
        .cloned()
        .collect()
}

// ---------------------------------------------------------------------------
// Row M — id minting (contract 0b-4)

/// A fresh node/edge id: the JSON-Canvas convention mac ships, which is
/// the first 16 hex characters of a lowercased v4 UUID
/// (`AppState+CanvasActions.swift:68–71`).
///
/// Those 16 characters are `time_low` + `time_mid` +
/// `time_hi_and_version`, so index 12 is the version nibble and is
/// always `'4'`; the id therefore carries 60 bits of entropy, not 64.
/// **No uniqueness check against the canvas is performed** — mac does
/// none either, and a check would make the minter need a canvas.
pub fn new_id() -> String {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    let mut bytes = [0u8; 8];
    if getrandom::fill(&mut bytes).is_err() {
        // The OS entropy source is unavailable — vanishingly unlikely,
        // and a panic here would kill a user's committed action.
        //
        // The fallback is NOT a second source of randomness with any
        // guarantee behind it. `RandomState` promises only that its
        // keys are "not predictable across program invocations"; it is
        // not documented as CSPRNG-backed, and hashing the clock adds
        // resolution, not entropy. It is here so an id is still minted
        // and still differs from its neighbours in one process, and it
        // is unreachable on every platform this ships to.
        use std::hash::{BuildHasher, Hasher};
        let mut hasher = std::collections::hash_map::RandomState::new().build_hasher();
        hasher.write_u128(
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .map(|d| d.as_nanos())
                .unwrap_or_default(),
        );
        bytes = hasher.finish().to_le_bytes();
    }
    let mut out = String::with_capacity(16);
    for (index, nibble) in bytes.iter().flat_map(|b| [b >> 4, b & 0x0f]).enumerate() {
        out.push(if index == 12 {
            '4'
        } else {
            HEX[nibble as usize] as char
        });
    }
    out
}

#[cfg(test)]
#[path = "queries_tests.rs"]
mod tests;
