// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canvas serializer — Milestone T, Wave 1 (#366).
//!
//! Emits spec-compliant JSON Canvas, round-trip-safe:
//!
//! - **Per-field reconciliation.** Every node/edge keeps its original
//!   ordered JSON object in `raw` (#359). Emission starts from that map
//!   and writes a typed field back only when it *semantically differs*
//!   from the retained value — an untouched `"x":10` stays the integer
//!   `10` (no float drift), unknown keys ride along verbatim, and key
//!   order is original-first (new keys append; brand-new nodes use the
//!   canonical key order below).
//! - **Skipped entries re-emit in place** (#359 retention, R3): the
//!   original array interleave is reconstructed from their recorded
//!   positions, so a save never deletes what the parser couldn't model.
//! - **Canonical layout** — tab-indented root, one compact line per
//!   node/edge, trailing newline (the format the committed fixtures are
//!   written in). `parse → serialize` of a file in canonical form is
//!   byte-identical; for foreign formatting (e.g. a canvas written by
//!   Obsidian) the first save normalizes layout while preserving
//!   content, key order, and numeric representations, and emission is
//!   idempotent from then on.
//! - Root keys keep their original document order (`root_key_order`);
//!   an empty parse (`{}`) re-emits as `{}` — `nodes`/`edges` keys are
//!   only materialized when present originally or non-empty.
//!
//! **Degraded loads are unwritable:** callers must never serialize a
//! canvas whose parse produced [`CanvasWarning::ParseFailed`] — that
//! would replace the user's file with an empty one. The session save
//! path (#361/#366) enforces this with [`is_load_degraded`].
//!
//! Atomic temp+rename writes, content-hash conflict detection, and
//! `.canvas` participation in link-integrity rewriting live in the
//! session layer, not here — this module is pure.

use serde_json::{Map, Value};

use super::model::Rect;
use super::{
    Background, BackgroundStyle, Canvas, CanvasColor, Edge, EndStyle, Node, NodeKind, Section, Side,
};

/// Canonical key order for brand-new nodes (raw map empty).
/// Existing nodes keep their original order.
const CANONICAL_NODE_ORDER: &[&str] = &[
    "id",
    "type",
    "text",
    "file",
    "subpath",
    "url",
    "label",
    "background",
    "backgroundStyle",
    "x",
    "y",
    "width",
    "height",
    "color",
];

const CANONICAL_EDGE_ORDER: &[&str] = &[
    "id", "fromNode", "fromSide", "fromEnd", "toNode", "toSide", "toEnd", "color", "label",
];

/// Serialize a canvas to its `.canvas` text (canonical layout, trailing
/// newline).
pub fn serialize(canvas: &Canvas) -> String {
    // Assemble the root key sequence: original order first, then any
    // keys that exist now but weren't in the original document.
    let mut keys: Vec<&str> = canvas
        .root_key_order
        .iter()
        .map(String::as_str)
        .filter(|k| match *k {
            "nodes" | "edges" => true,
            other => canvas.unknown.contains_key(other),
        })
        .collect();
    for extra in ["nodes", "edges"] {
        let present = keys.contains(&extra);
        let needed = match extra {
            "nodes" => !canvas.nodes.is_empty() || has_skipped(canvas, Section::Nodes),
            _ => !canvas.edges.is_empty() || has_skipped(canvas, Section::Edges),
        };
        if !present && needed {
            keys.push(extra);
        }
    }
    for k in canvas.unknown.keys() {
        if !keys.contains(&k.as_str()) {
            keys.push(k);
        }
    }

    if keys.is_empty() {
        return "{}\n".to_string();
    }

    let mut out = String::from("{\n");
    for (i, key) in keys.iter().enumerate() {
        let comma = if i + 1 < keys.len() { "," } else { "" };
        match *key {
            "nodes" => emit_array(&mut out, "nodes", node_lines(canvas), comma),
            "edges" => emit_array(&mut out, "edges", edge_lines(canvas), comma),
            other => {
                let value = compact(&canvas.unknown[other]);
                out.push_str(&format!("\t{}:{value}{comma}\n", quote(other)));
            }
        }
    }
    out.push_str("}\n");
    out
}

fn has_skipped(canvas: &Canvas, section: Section) -> bool {
    canvas.skipped.iter().any(|s| s.section == section)
}

fn emit_array(out: &mut String, name: &str, lines: Vec<String>, comma: &str) {
    if lines.is_empty() {
        out.push_str(&format!("\t\"{name}\":[]{comma}\n"));
        return;
    }
    out.push_str(&format!("\t\"{name}\":[\n"));
    for (i, line) in lines.iter().enumerate() {
        let entry_comma = if i + 1 < lines.len() { "," } else { "" };
        out.push_str(&format!("\t\t{line}{entry_comma}\n"));
    }
    out.push_str(&format!("\t]{comma}\n"));
}

/// Reconstruct the original array interleave: skipped entries occupy
/// their recorded positions, modeled entries fill the remaining slots
/// in document order. Out-of-range positions (possible after deletions)
/// append at the end — retained data is never dropped.
fn merged_lines(modeled: Vec<String>, skipped: Vec<(usize, String)>) -> Vec<String> {
    let total = modeled.len() + skipped.len();
    let mut slots: Vec<Option<String>> = vec![None; total];
    let mut overflow: Vec<String> = Vec::new();
    for (pos, text) in skipped {
        match slots.get_mut(pos) {
            Some(slot @ None) => *slot = Some(text),
            _ => overflow.push(text),
        }
    }
    let mut modeled_it = modeled.into_iter();
    for slot in &mut slots {
        if slot.is_none() {
            *slot = modeled_it.next();
        }
    }
    let mut out: Vec<String> = slots.into_iter().flatten().collect();
    out.extend(modeled_it);
    out.extend(overflow);
    out
}

fn node_lines(canvas: &Canvas) -> Vec<String> {
    let modeled = canvas
        .nodes
        .iter()
        .map(|n| compact_obj(&node_map(n)))
        .collect();
    let skipped = canvas
        .skipped
        .iter()
        .filter(|s| s.section == Section::Nodes)
        .map(|s| (s.position, compact(&s.raw)))
        .collect();
    merged_lines(modeled, skipped)
}

fn edge_lines(canvas: &Canvas) -> Vec<String> {
    let modeled = canvas
        .edges
        .iter()
        .map(|e| compact_obj(&edge_map(e)))
        .collect();
    let skipped = canvas
        .skipped
        .iter()
        .filter(|s| s.section == Section::Edges)
        .map(|s| (s.position, compact(&s.raw)))
        .collect();
    merged_lines(modeled, skipped)
}

/// Write `value` into the map only when it differs semantically from
/// the retained entry — otherwise the original representation stays.
fn set_str(map: &mut Map<String, Value>, key: &str, value: &str) {
    if map.get(key).and_then(Value::as_str) != Some(value) {
        map.insert(key.to_string(), Value::from(value));
    }
}

/// Numbers compare by value: an untouched `10` never becomes `10.0`.
/// Changed values emit as integers when integral (grid coordinates are
/// integral in practice — #517 constants).
fn set_num(map: &mut Map<String, Value>, key: &str, value: f64) {
    if map.get(key).and_then(Value::as_f64) == Some(value) {
        return;
    }
    let num = if value.fract() == 0.0 && value.abs() < 9.0e15 {
        Value::from(value as i64)
    } else {
        Value::from(value)
    };
    map.insert(key.to_string(), num);
}

/// `Some` writes; `None` leaves the retained entry untouched (a `None`
/// only ever means "absent" or "tolerated-unusable original" — the
/// mutation layer (#361) removes keys explicitly when clearing).
fn set_opt_str(map: &mut Map<String, Value>, key: &str, value: Option<&str>) {
    if let Some(v) = value {
        set_str(map, key, v);
    }
}

fn color_str(color: &CanvasColor) -> String {
    match color {
        CanvasColor::Preset(p) => p.to_string(),
        CanvasColor::Hex(s) => s.clone(),
    }
}

fn side_str(side: Side) -> &'static str {
    match side {
        Side::Top => "top",
        Side::Right => "right",
        Side::Bottom => "bottom",
        Side::Left => "left",
    }
}

fn end_str(end: EndStyle) -> &'static str {
    match end {
        EndStyle::None => "none",
        EndStyle::Arrow => "arrow",
    }
}

fn node_map(node: &Node) -> Map<String, Value> {
    let mut m = node.raw.clone();
    set_str(&mut m, "id", &node.id.0);
    set_str(&mut m, "type", node.kind.type_str());
    set_num(&mut m, "x", node.x);
    set_num(&mut m, "y", node.y);
    set_num(&mut m, "width", node.width);
    set_num(&mut m, "height", node.height);
    set_opt_str(
        &mut m,
        "color",
        node.color.as_ref().map(color_str).as_deref(),
    );
    match &node.kind {
        NodeKind::Text { text } => set_str(&mut m, "text", text),
        NodeKind::File { file, subpath } => {
            set_str(&mut m, "file", file);
            set_opt_str(&mut m, "subpath", subpath.as_deref());
        }
        NodeKind::Link { url } => set_str(&mut m, "url", url),
        NodeKind::Group { label, background } => {
            set_opt_str(&mut m, "label", label.as_deref());
            if let Some(Background { image, style }) = background {
                set_opt_str(&mut m, "background", image.as_deref());
                let style_str = style.as_ref().map(|s| match s {
                    BackgroundStyle::Cover => "cover".to_string(),
                    BackgroundStyle::Ratio => "ratio".to_string(),
                    BackgroundStyle::Repeat => "repeat".to_string(),
                    BackgroundStyle::Other(o) => o.clone(),
                });
                set_opt_str(&mut m, "backgroundStyle", style_str.as_deref());
            }
        }
    }
    ordered(m, CANONICAL_NODE_ORDER, !node.raw.is_empty())
}

fn edge_map(edge: &Edge) -> Map<String, Value> {
    let mut m = edge.raw.clone();
    set_str(&mut m, "id", &edge.id.0);
    set_str(&mut m, "fromNode", &edge.from.0.0);
    set_str(&mut m, "toNode", &edge.to.0.0);
    set_opt_str(&mut m, "fromSide", edge.from.1.map(side_str));
    set_opt_str(&mut m, "toSide", edge.to.1.map(side_str));
    // End styles: spec defaults stay implicit, and a retained entry is
    // only overwritten when the typed value differs from what that
    // entry *parses to* — so tolerated-unusable originals (e.g.
    // `"toEnd":"sparkle"`, which reads as the default) survive a save
    // untouched instead of being silently normalized.
    for (key, end, default) in [
        ("fromEnd", edge.from_end, EndStyle::None),
        ("toEnd", edge.to_end, EndStyle::Arrow),
    ] {
        let retained_reads_as = match m.get(key).and_then(Value::as_str) {
            Some("none") => Some(EndStyle::None),
            Some("arrow") => Some(EndStyle::Arrow),
            Some(_) => Some(default), // unusable → parser fell back
            None => None,
        };
        match retained_reads_as {
            Some(parsed) if parsed != end => set_str(&mut m, key, end_str(end)),
            Some(_) => {}
            None if end != default => set_str(&mut m, key, end_str(end)),
            None => {}
        }
    }
    set_opt_str(
        &mut m,
        "color",
        edge.color.as_ref().map(color_str).as_deref(),
    );
    set_opt_str(&mut m, "label", edge.label.as_deref());
    ordered(m, CANONICAL_EDGE_ORDER, !edge.raw.is_empty())
}

/// For brand-new entries (no retained map), order keys canonically so
/// programmatic output is stable and diff-friendly. Entries with a
/// retained map keep it untouched (original order, new keys appended).
fn ordered(map: Map<String, Value>, canonical: &[&str], had_raw: bool) -> Map<String, Value> {
    if had_raw {
        return map;
    }
    let mut out = Map::new();
    let mut rest = map;
    for key in canonical {
        if let Some(v) = rest.shift_remove(*key) {
            out.insert((*key).to_string(), v);
        }
    }
    for (k, v) in rest {
        out.insert(k, v);
    }
    out
}

fn compact(value: &Value) -> String {
    serde_json::to_string(value).expect("JSON value serialization cannot fail")
}

fn compact_obj(map: &Map<String, Value>) -> String {
    serde_json::to_string(map).expect("JSON map serialization cannot fail")
}

fn quote(s: &str) -> String {
    compact(&Value::from(s))
}

/// Convenience for mutation code (#361): a node's rect after typed
/// geometry updates.
pub fn node_rect(node: &Node) -> Rect {
    Rect::from_node(node)
}

#[cfg(test)]
#[path = "serialize_tests.rs"]
mod tests;
