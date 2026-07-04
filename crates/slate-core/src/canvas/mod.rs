// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! JSON Canvas (`.canvas`) parser — Milestone T, Wave 1 (#359).
//!
//! Parses Obsidian-compatible [JSON Canvas 1.0](https://jsoncanvas.org)
//! files into typed structures, following the crate's pure-parser
//! pattern (`links`, `tasks`, `frontmatter`): no I/O, no session state,
//! deterministic output.
//!
//! ## Contract (t1 spec, normative)
//!
//! - **Tolerant:** the parse never hard-fails on entry-level problems. A
//!   malformed node/edge yields a [`CanvasWarning`], is excluded from the
//!   typed model, and is **retained** in [`Canvas::skipped`] so a later
//!   save re-emits it in place — a save must never delete what the
//!   parser couldn't model. Only a file that isn't valid JSON at all (or
//!   whose root shape is unusable) degrades to an empty canvas with a
//!   [`CanvasWarning::ParseFailed`]; callers must treat such a canvas as
//!   read-only (see [`is_load_degraded`]) — the serializer (#366)
//!   refuses to write over a degraded load. Known limitation: a number
//!   beyond f64 range (`1e999`) fails JSON parsing entirely, so one
//!   such value degrades the whole file rather than skipping one entry;
//!   JS tooling can hand-produce such a file but never `JSON.stringify`
//!   one (Infinity serializes as `null`). Degraded = read-only, so this
//!   is a fidelity limit, never data loss.
//! - **Lossless:** every parsed node/edge retains its complete original
//!   JSON object (insertion-ordered, `preserve_order`) in `raw`. Unknown
//!   keys — at root, node, and edge level — ride along verbatim and are
//!   observable via the `unknown()` accessors. Typed fields are the
//!   source of truth for what Slate models; the serializer reconciles
//!   typed values back into `raw` per field so untouched fields keep
//!   their original representation (no float drift).
//! - **Color names are pinned here** (backend-owned, t0 §1.1): presets
//!   1–6 = red, orange, yellow, green, cyan, purple; hex values phrase
//!   as "custom color". Every surface phrases through [`color_name`].
//!
//! Downstream: `model.rs` (#360) derives reading order / containment /
//! adjacency; `placement.rs` (#517) computes non-overlapping slots;
//! `serialize.rs` (#366) writes round-trip-safe output.

use std::collections::HashSet;

use serde_json::{Map, Value};

pub mod model;

/// Insertion-ordered map of raw JSON fields (requires serde_json's
/// `preserve_order` feature, enabled workspace-wide).
pub type RawExtra = Map<String, Value>;

/// Node identifier, unique within one canvas file (not vault-wide).
#[derive(Debug, Clone, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct NodeId(pub String);

/// Edge identifier, unique within one canvas file.
#[derive(Debug, Clone, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct EdgeId(pub String);

/// A canvas color: one of the six spec presets or a `#RRGGBB` hex value.
///
/// Anything that isn't the literal `"1"`–`"6"` is carried verbatim as
/// `Hex` (lossless — we never normalize or validate the string).
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum CanvasColor {
    /// Preset `1..=6` per the JSON Canvas spec.
    Preset(u8),
    /// Verbatim color string (typically `#RRGGBB`).
    Hex(String),
}

/// The pinned, backend-owned color names all surfaces phrase through
/// (t0 §1.1). Presets follow JSON Canvas order.
pub fn color_name(color: &CanvasColor) -> &'static str {
    match color {
        CanvasColor::Preset(1) => "red",
        CanvasColor::Preset(2) => "orange",
        CanvasColor::Preset(3) => "yellow",
        CanvasColor::Preset(4) => "green",
        CanvasColor::Preset(5) => "cyan",
        CanvasColor::Preset(6) => "purple",
        // Preset is only ever constructed for 1..=6; anything else parses
        // as Hex. The arm exists to keep the match total.
        CanvasColor::Preset(_) | CanvasColor::Hex(_) => "custom color",
    }
}

/// Which side of a node an edge attaches to.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Side {
    Top,
    Right,
    Bottom,
    Left,
}

/// Edge endpoint decoration. Spec defaults: `fromEnd = None`,
/// `toEnd = Arrow` (a plain edge is a one-way arrow to its target).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EndStyle {
    None,
    Arrow,
}

/// Group background image scaling style.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum BackgroundStyle {
    Cover,
    Ratio,
    Repeat,
    /// Unrecognized style string, carried verbatim.
    Other(String),
}

/// Group background (image path + optional style).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Background {
    /// Vault-relative image path (`background` key).
    pub image: Option<String>,
    /// `backgroundStyle` key.
    pub style: Option<BackgroundStyle>,
}

/// The per-type payload of a node.
#[derive(Debug, Clone, PartialEq)]
pub enum NodeKind {
    Text {
        text: String,
    },
    File {
        /// Vault-relative path.
        file: String,
        /// `#heading` anchor within the file, verbatim (leading `#`).
        subpath: Option<String>,
    },
    Link {
        url: String,
    },
    Group {
        label: Option<String>,
        background: Option<Background>,
    },
}

impl NodeKind {
    /// The JSON Canvas `type` discriminator for this kind.
    pub fn type_str(&self) -> &'static str {
        match self {
            NodeKind::Text { .. } => "text",
            NodeKind::File { .. } => "file",
            NodeKind::Link { .. } => "link",
            NodeKind::Group { .. } => "group",
        }
    }
}

/// Keys the typed model owns for every node, regardless of kind.
const NODE_COMMON_KEYS: &[&str] = &["id", "type", "x", "y", "width", "height", "color"];

/// Keys the typed model owns on edges.
const EDGE_KEYS: &[&str] = &[
    "id", "fromNode", "fromSide", "fromEnd", "toNode", "toSide", "toEnd", "color", "label",
];

/// One parsed canvas node.
#[derive(Debug, Clone, PartialEq)]
pub struct Node {
    pub id: NodeId,
    pub kind: NodeKind,
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
    pub color: Option<CanvasColor>,
    /// The complete original JSON object for this node, key order
    /// preserved. Unknown keys live here verbatim (see [`Node::unknown`]);
    /// known keys keep their original representation so the serializer
    /// can re-emit untouched fields without drift.
    pub raw: RawExtra,
}

impl Node {
    fn known_keys(&self) -> &'static [&'static str] {
        match self.kind {
            NodeKind::Text { .. } => &["text"],
            NodeKind::File { .. } => &["file", "subpath"],
            NodeKind::Link { .. } => &["url"],
            NodeKind::Group { .. } => &["label", "background", "backgroundStyle"],
        }
    }

    /// Fields of the original JSON object the typed model does not
    /// represent, in original document order.
    pub fn unknown(&self) -> impl Iterator<Item = (&str, &Value)> {
        let kind_keys = self.known_keys();
        self.raw.iter().filter_map(move |(k, v)| {
            let known = NODE_COMMON_KEYS.contains(&k.as_str()) || kind_keys.contains(&k.as_str());
            (!known).then_some((k.as_str(), v))
        })
    }
}

/// One parsed canvas edge (user-facing surfaces say "connection").
#[derive(Debug, Clone, PartialEq)]
pub struct Edge {
    pub id: EdgeId,
    /// Source node + optional attachment side.
    pub from: (NodeId, Option<Side>),
    /// Target node + optional attachment side.
    pub to: (NodeId, Option<Side>),
    pub from_end: EndStyle,
    pub to_end: EndStyle,
    pub label: Option<String>,
    pub color: Option<CanvasColor>,
    /// Complete original JSON object, key order preserved (see [`Node::raw`]).
    pub raw: RawExtra,
}

impl Edge {
    /// Fields of the original JSON object the typed model does not
    /// represent, in original document order.
    pub fn unknown(&self) -> impl Iterator<Item = (&str, &Value)> {
        self.raw
            .iter()
            .filter_map(|(k, v)| (!EDGE_KEYS.contains(&k.as_str())).then_some((k.as_str(), v)))
    }
}

/// Which top-level array an entry came from.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Section {
    Nodes,
    Edges,
}

/// A nodes/edges entry the parser could not model, retained so a save
/// re-emits it in place (never silently dropped — R3).
#[derive(Debug, Clone, PartialEq)]
pub struct SkippedEntry {
    pub section: Section,
    /// Index in the original `nodes`/`edges` array.
    pub position: usize,
    /// The entry as parsed JSON, key order preserved. (Retention is at
    /// the JSON level, not the byte level: re-emission uses canonical
    /// formatting but identical content and key order.)
    pub raw: Value,
    /// Why it was skipped (also present in the parse warnings).
    pub warning: CanvasWarning,
}

/// A parsed canvas: the typed model plus everything needed to write the
/// file back without losing data the model doesn't represent.
#[derive(Debug, Clone, PartialEq, Default)]
pub struct Canvas {
    /// Nodes in document order (the final reading-order tiebreak, #360).
    pub nodes: Vec<Node>,
    /// Edges in document order.
    pub edges: Vec<Edge>,
    /// Unknown root-level keys, in original order.
    pub unknown: RawExtra,
    /// Every root-level key in original document order (including
    /// `nodes`/`edges`), so the serializer can keep root layout stable.
    pub root_key_order: Vec<String>,
    /// Entries excluded from the typed model but preserved for save.
    pub skipped: Vec<SkippedEntry>,
}

/// Structured, non-fatal problems found while parsing.
#[derive(Debug, Clone, PartialEq)]
pub enum CanvasWarning {
    /// The file is not usable JSON Canvas at all (invalid JSON, or the
    /// root/`nodes`/`edges` shapes are wrong). The canvas is empty and
    /// **must not be saved over** — see [`is_load_degraded`].
    ParseFailed { reason: String },
    /// A nodes entry that could not be modeled (skipped + retained).
    MalformedNode { index: usize, reason: String },
    /// An edges entry that could not be modeled (skipped + retained).
    MalformedEdge { index: usize, reason: String },
    /// A node whose `type` Slate doesn't know (future spec extension);
    /// skipped + retained. Drives the t0 §5 "unsupported items are
    /// preserved in the file but not shown" surfacing.
    UnknownNodeType { index: usize, node_type: String },
    /// Second occurrence of an id; the later entry is skipped + retained.
    DuplicateId {
        section: Section,
        index: usize,
        id: String,
    },
    /// An edge endpoint that doesn't resolve to a modeled node. The edge
    /// stays in [`Canvas::edges`] but #360 excludes it from adjacency.
    DanglingEdge {
        edge_id: EdgeId,
        missing_node: String,
    },
    /// An optional field with an unusable value; the field was treated
    /// as absent (the original value survives in `raw`).
    IgnoredValue {
        section: Section,
        index: usize,
        key: String,
        detail: String,
    },
}

/// True when the warnings indicate the file could not be loaded as a
/// canvas at all. A degraded canvas is read-only: writing it back would
/// replace the user's file with an empty one.
pub fn is_load_degraded(warnings: &[CanvasWarning]) -> bool {
    warnings
        .iter()
        .any(|w| matches!(w, CanvasWarning::ParseFailed { .. }))
}

/// Parse a `.canvas` file. Never returns an error: problems degrade to
/// warnings per the module contract. An empty/whitespace-only input is
/// an empty canvas (no warnings) — the "new canvas" bootstrap case.
pub fn parse(input: &str) -> (Canvas, Vec<CanvasWarning>) {
    let mut warnings = Vec::new();
    if input.trim().is_empty() {
        return (Canvas::default(), warnings);
    }

    let root: Value = match serde_json::from_str(input) {
        Ok(v) => v,
        Err(e) => {
            warnings.push(CanvasWarning::ParseFailed {
                reason: format!("invalid JSON: {e}"),
            });
            return (Canvas::default(), warnings);
        }
    };
    let Value::Object(mut root_map) = root else {
        warnings.push(CanvasWarning::ParseFailed {
            reason: "root is not a JSON object".to_string(),
        });
        return (Canvas::default(), warnings);
    };

    let root_key_order: Vec<String> = root_map.keys().cloned().collect();

    // `shift_remove` (not `remove`) so the residual map keeps the
    // original relative order of unknown root keys.
    let nodes_val = root_map.shift_remove("nodes");
    let edges_val = root_map.shift_remove("edges");
    for (key, val) in [("nodes", &nodes_val), ("edges", &edges_val)] {
        if let Some(v) = val
            && !v.is_array()
        {
            warnings.push(CanvasWarning::ParseFailed {
                reason: format!("\"{key}\" is not an array"),
            });
            return (Canvas::default(), warnings);
        }
    }

    let mut canvas = Canvas {
        unknown: root_map,
        root_key_order,
        ..Canvas::default()
    };

    let mut node_ids: HashSet<String> = HashSet::new();
    if let Some(Value::Array(items)) = nodes_val {
        for (index, item) in items.into_iter().enumerate() {
            match parse_node(&item, index, &node_ids, &mut warnings) {
                Ok(node) => {
                    node_ids.insert(node.id.0.clone());
                    canvas.nodes.push(node);
                }
                Err(warning) => {
                    warnings.push(warning.clone());
                    canvas.skipped.push(SkippedEntry {
                        section: Section::Nodes,
                        position: index,
                        raw: item,
                        warning,
                    });
                }
            }
        }
    }

    let mut edge_ids: HashSet<String> = HashSet::new();
    if let Some(Value::Array(items)) = edges_val {
        for (index, item) in items.into_iter().enumerate() {
            match parse_edge(&item, index, &edge_ids, &mut warnings) {
                Ok(edge) => {
                    edge_ids.insert(edge.id.0.clone());
                    canvas.edges.push(edge);
                }
                Err(warning) => {
                    warnings.push(warning.clone());
                    canvas.skipped.push(SkippedEntry {
                        section: Section::Edges,
                        position: index,
                        raw: item,
                        warning,
                    });
                }
            }
        }
    }

    // Dangling-edge pass: endpoints must resolve to *modeled* nodes (a
    // skipped node is absent from every surface, so an edge into it is
    // dangling for navigation purposes; the data itself is retained).
    for edge in &canvas.edges {
        for endpoint in [&edge.from.0, &edge.to.0] {
            if !node_ids.contains(&endpoint.0) {
                warnings.push(CanvasWarning::DanglingEdge {
                    edge_id: edge.id.clone(),
                    missing_node: endpoint.0.clone(),
                });
            }
        }
    }

    (canvas, warnings)
}

/// Extract a required string field.
fn req_str(obj: &RawExtra, key: &str) -> Result<String, String> {
    match obj.get(key) {
        Some(Value::String(s)) => Ok(s.clone()),
        Some(_) => Err(format!("\"{key}\" is not a string")),
        None => Err(format!("missing \"{key}\"")),
    }
}

/// Extract a required numeric field as f64.
fn req_num(obj: &RawExtra, key: &str) -> Result<f64, String> {
    match obj.get(key) {
        Some(Value::Number(n)) => n
            .as_f64()
            .ok_or_else(|| format!("\"{key}\" is out of range")),
        Some(_) => Err(format!("\"{key}\" is not a number")),
        None => Err(format!("missing \"{key}\"")),
    }
}

/// Extract an optional string field; a present-but-non-string value
/// yields an `IgnoredValue` warning and reads as absent.
fn opt_str(
    obj: &RawExtra,
    key: &str,
    section: Section,
    index: usize,
    warnings: &mut Vec<CanvasWarning>,
) -> Option<String> {
    match obj.get(key) {
        Some(Value::String(s)) => Some(s.clone()),
        Some(other) => {
            warnings.push(CanvasWarning::IgnoredValue {
                section,
                index,
                key: key.to_string(),
                detail: format!("expected a string, found {other}"),
            });
            None
        }
        None => None,
    }
}

fn parse_color(s: &str) -> CanvasColor {
    match s {
        "1" => CanvasColor::Preset(1),
        "2" => CanvasColor::Preset(2),
        "3" => CanvasColor::Preset(3),
        "4" => CanvasColor::Preset(4),
        "5" => CanvasColor::Preset(5),
        "6" => CanvasColor::Preset(6),
        other => CanvasColor::Hex(other.to_string()),
    }
}

fn parse_side(s: &str) -> Option<Side> {
    match s {
        "top" => Some(Side::Top),
        "right" => Some(Side::Right),
        "bottom" => Some(Side::Bottom),
        "left" => Some(Side::Left),
        _ => None,
    }
}

fn parse_node(
    item: &Value,
    index: usize,
    seen_ids: &HashSet<String>,
    warnings: &mut Vec<CanvasWarning>,
) -> Result<Node, CanvasWarning> {
    fn malformed(index: usize, reason: String) -> CanvasWarning {
        CanvasWarning::MalformedNode { index, reason }
    }
    let Value::Object(obj) = item else {
        return Err(malformed(index, "entry is not an object".to_string()));
    };

    let id = req_str(obj, "id").map_err(|r| malformed(index, r))?;
    if seen_ids.contains(&id) {
        return Err(CanvasWarning::DuplicateId {
            section: Section::Nodes,
            index,
            id,
        });
    }
    let node_type = req_str(obj, "type").map_err(|r| malformed(index, r))?;

    let x = req_num(obj, "x").map_err(|r| malformed(index, r))?;
    let y = req_num(obj, "y").map_err(|r| malformed(index, r))?;
    let width = req_num(obj, "width").map_err(|r| malformed(index, r))?;
    let height = req_num(obj, "height").map_err(|r| malformed(index, r))?;

    let kind = match node_type.as_str() {
        "text" => NodeKind::Text {
            text: req_str(obj, "text").map_err(|r| malformed(index, r))?,
        },
        "file" => NodeKind::File {
            file: req_str(obj, "file").map_err(|r| malformed(index, r))?,
            subpath: opt_str(obj, "subpath", Section::Nodes, index, warnings),
        },
        "link" => NodeKind::Link {
            url: req_str(obj, "url").map_err(|r| malformed(index, r))?,
        },
        "group" => {
            let label = opt_str(obj, "label", Section::Nodes, index, warnings);
            let image = opt_str(obj, "background", Section::Nodes, index, warnings);
            let style = opt_str(obj, "backgroundStyle", Section::Nodes, index, warnings).map(|s| {
                match s.as_str() {
                    "cover" => BackgroundStyle::Cover,
                    "ratio" => BackgroundStyle::Ratio,
                    "repeat" => BackgroundStyle::Repeat,
                    _ => BackgroundStyle::Other(s),
                }
            });
            let background =
                (image.is_some() || style.is_some()).then_some(Background { image, style });
            NodeKind::Group { label, background }
        }
        other => {
            return Err(CanvasWarning::UnknownNodeType {
                index,
                node_type: other.to_string(),
            });
        }
    };

    let color = opt_str(obj, "color", Section::Nodes, index, warnings).map(|s| parse_color(&s));

    Ok(Node {
        id: NodeId(id),
        kind,
        x,
        y,
        width,
        height,
        color,
        raw: obj.clone(),
    })
}

fn parse_edge(
    item: &Value,
    index: usize,
    seen_ids: &HashSet<String>,
    warnings: &mut Vec<CanvasWarning>,
) -> Result<Edge, CanvasWarning> {
    fn malformed(index: usize, reason: String) -> CanvasWarning {
        CanvasWarning::MalformedEdge { index, reason }
    }
    let Value::Object(obj) = item else {
        return Err(malformed(index, "entry is not an object".to_string()));
    };

    let id = req_str(obj, "id").map_err(|r| malformed(index, r))?;
    if seen_ids.contains(&id) {
        return Err(CanvasWarning::DuplicateId {
            section: Section::Edges,
            index,
            id,
        });
    }
    let from_node = req_str(obj, "fromNode").map_err(|r| malformed(index, r))?;
    let to_node = req_str(obj, "toNode").map_err(|r| malformed(index, r))?;

    let mut side = |key: &str| -> Option<Side> {
        let s = opt_str(obj, key, Section::Edges, index, warnings)?;
        let parsed = parse_side(&s);
        if parsed.is_none() {
            warnings.push(CanvasWarning::IgnoredValue {
                section: Section::Edges,
                index,
                key: key.to_string(),
                detail: format!("unknown side \"{s}\""),
            });
        }
        parsed
    };
    let from_side = side("fromSide");
    let to_side = side("toSide");

    let mut end = |key: &str, default: EndStyle| -> EndStyle {
        let Some(s) = opt_str(obj, key, Section::Edges, index, warnings) else {
            return default;
        };
        match s.as_str() {
            "none" => EndStyle::None,
            "arrow" => EndStyle::Arrow,
            _ => {
                warnings.push(CanvasWarning::IgnoredValue {
                    section: Section::Edges,
                    index,
                    key: key.to_string(),
                    detail: format!("unknown end style \"{s}\""),
                });
                default
            }
        }
    };
    let from_end = end("fromEnd", EndStyle::None);
    let to_end = end("toEnd", EndStyle::Arrow);

    let label = opt_str(obj, "label", Section::Edges, index, warnings);
    let color = opt_str(obj, "color", Section::Edges, index, warnings).map(|s| parse_color(&s));

    Ok(Edge {
        id: EdgeId(id),
        from: (NodeId(from_node), from_side),
        to: (NodeId(to_node), to_side),
        from_end,
        to_end,
        label,
        color,
        raw: obj.clone(),
    })
}

#[cfg(test)]
mod tests;
