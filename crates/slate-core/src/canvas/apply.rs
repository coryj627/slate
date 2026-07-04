// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canvas mutation engine — Milestone T, #361 write surface.
//!
//! One committed user action = one [`CanvasAction`] = one
//! `canvas_apply` = one serialize + atomic write + one journal entry
//! (t1 pipeline; the journal kind lands with #372). The UI builds
//! [`CanvasOp`]s; this module mutates the typed [`Canvas`] and returns
//! the **inverse action** for the undo stack.
//!
//! ## Semantics
//!
//! - **Atomic per action**: ops apply in order against a working copy;
//!   the first invalid op rejects the whole action (`ApplyError`), and
//!   the caller's canvas is untouched.
//! - **Inverses are exact**: `apply(a); apply(inverse(a))` restores the
//!   canvas to byte-equal serialization. Deleting a node deletes its
//!   incident connections too (Obsidian semantics); the inverse restores
//!   node and connections **with their full original JSON** — unknown
//!   fields included — via the `RestoreNode` / `RestoreEdge` ops, which
//!   carry the entry's canonical JSON text and original array position.
//!   The UI never constructs Restore ops directly; they exist so undo
//!   loses nothing (R3 discipline extended to mutation).
//! - **Key removal is the mutator's job**: the serializer leaves raw
//!   entries alone when a typed field is `None` (tolerated-garbage
//!   retention), so clearing color/label/subpath here removes the raw
//!   key explicitly.

use serde_json::Value;

use super::serialize::{edge_map, node_map};
use super::{Canvas, Edge, EdgeId, EndStyle, Node, NodeId, NodeKind, RawExtra, Side};

/// A named, undoable batch of ops (the op-log/undo label is `name`,
/// e.g. `move 'Research'` — announced as "Undid: move 'Research'").
#[derive(Debug, Clone, PartialEq)]
pub struct CanvasAction {
    pub name: String,
    pub ops: Vec<CanvasOp>,
}

/// New-node payload for [`CanvasOp::CreateNode`].
#[derive(Debug, Clone, PartialEq)]
pub enum CanvasNodeContent {
    Text {
        text: String,
    },
    File {
        file: String,
        subpath: Option<String>,
    },
    Link {
        url: String,
    },
}

/// One primitive mutation. Geometry is raw JSON Canvas coordinates;
/// grid discipline belongs to the callers (#517 constants).
#[derive(Debug, Clone, PartialEq)]
pub enum CanvasOp {
    CreateNode {
        id: String,
        content: CanvasNodeContent,
        x: f64,
        y: f64,
        width: f64,
        height: f64,
        /// Raw color string ("1".."6" or hex).
        color: Option<String>,
    },
    CreateGroup {
        id: String,
        label: Option<String>,
        x: f64,
        y: f64,
        width: f64,
        height: f64,
        color: Option<String>,
    },
    UpdateNodeGeometry {
        id: String,
        x: f64,
        y: f64,
        width: f64,
        height: f64,
    },
    SetNodeColor {
        id: String,
        /// `None` clears the color.
        color: Option<String>,
    },
    /// Replace a card's content — also the kind-conversion path
    /// (convert text card → file card, #525).
    SetNodeContent {
        id: String,
        content: CanvasNodeContent,
    },
    /// Delete a node and every connection touching it.
    DeleteNode {
        id: String,
    },
    AddEdge {
        id: String,
        from_node: String,
        from_side: Option<Side>,
        to_node: String,
        to_side: Option<Side>,
        from_end: EndStyle,
        to_end: EndStyle,
        label: Option<String>,
        color: Option<String>,
    },
    /// Replace a connection's mutable attributes (endpoints are
    /// immutable — reconnecting is delete + add).
    UpdateEdge {
        id: String,
        from_side: Option<Side>,
        to_side: Option<Side>,
        from_end: EndStyle,
        to_end: EndStyle,
        label: Option<String>,
        color: Option<String>,
    },
    DeleteEdge {
        id: String,
    },
    RenameGroup {
        id: String,
        /// `None` clears the label.
        label: Option<String>,
    },
    /// Delete a group frame, leaving its (geometrically contained)
    /// children in place.
    Ungroup {
        id: String,
    },
    /// Undo-only: reinsert a node from its full original JSON at its
    /// original array position. Never constructed by the UI.
    RestoreNode {
        node_json: String,
        position: u32,
    },
    /// Undo-only: reinsert a connection from its full original JSON.
    RestoreEdge {
        edge_json: String,
        position: u32,
    },
    /// Undo-only: replace an existing node's full content (same id,
    /// same array position) from its original JSON — the exact-restore
    /// inverse for attribute mutations, preserving raw key order.
    RestoreNodeInPlace {
        node_json: String,
    },
    /// Undo-only: replace an existing connection's full content.
    RestoreEdgeInPlace {
        edge_json: String,
    },
}

/// Why an action was rejected (whole-action, no partial application).
#[derive(Debug, Clone, PartialEq, thiserror::Error)]
pub enum ApplyError {
    #[error("unknown node {0:?}")]
    UnknownNode(String),
    #[error("unknown connection {0:?}")]
    UnknownEdge(String),
    #[error("id {0:?} already exists")]
    DuplicateId(String),
    #[error("node {0:?} is not a group")]
    NotAGroup(String),
    #[error("node {0:?} is a group; use group ops")]
    IsAGroup(String),
    #[error("connection endpoint {0:?} does not exist")]
    MissingEndpoint(String),
    #[error("restore payload is not valid: {0}")]
    BadRestorePayload(String),
}

/// Apply `action` to `canvas` in place. On success returns the inverse
/// action (same name, ops inverted and reversed). On error the canvas
/// is left **unmodified** (the caller passes a working copy or relies
/// on this guarantee — internally ops run against a clone).
pub fn apply(canvas: &mut Canvas, action: &CanvasAction) -> Result<CanvasAction, ApplyError> {
    let mut work = canvas.clone();
    let mut inverse_ops: Vec<CanvasOp> = Vec::new();
    for op in &action.ops {
        let mut inv = apply_one(&mut work, op)?;
        inv.reverse(); // multi-op inverses (delete-node) stay internally ordered
        inverse_ops.extend(inv);
    }
    inverse_ops.reverse();
    *canvas = work;
    Ok(CanvasAction {
        name: action.name.clone(),
        ops: inverse_ops,
    })
}

fn node_index(canvas: &Canvas, id: &str) -> Result<usize, ApplyError> {
    canvas
        .nodes
        .iter()
        .position(|n| n.id.0 == id)
        .ok_or_else(|| ApplyError::UnknownNode(id.to_string()))
}

fn edge_index(canvas: &Canvas, id: &str) -> Result<usize, ApplyError> {
    canvas
        .edges
        .iter()
        .position(|e| e.id.0 == id)
        .ok_or_else(|| ApplyError::UnknownEdge(id.to_string()))
}

fn id_taken(canvas: &Canvas, id: &str) -> bool {
    canvas.nodes.iter().any(|n| n.id.0 == id) || canvas.edges.iter().any(|e| e.id.0 == id)
}

fn content_to_kind(content: &CanvasNodeContent) -> NodeKind {
    match content {
        CanvasNodeContent::Text { text } => NodeKind::Text { text: text.clone() },
        CanvasNodeContent::File { file, subpath } => NodeKind::File {
            file: file.clone(),
            subpath: subpath.clone(),
        },
        CanvasNodeContent::Link { url } => NodeKind::Link { url: url.clone() },
    }
}

fn kind_to_content(kind: &NodeKind) -> Option<CanvasNodeContent> {
    match kind {
        NodeKind::Text { text } => Some(CanvasNodeContent::Text { text: text.clone() }),
        NodeKind::File { file, subpath } => Some(CanvasNodeContent::File {
            file: file.clone(),
            subpath: subpath.clone(),
        }),
        NodeKind::Link { url } => Some(CanvasNodeContent::Link { url: url.clone() }),
        NodeKind::Group { .. } => None,
    }
}

fn color_of(raw: &Option<String>) -> Option<super::CanvasColor> {
    raw.as_ref().map(|s| super::parse_color(s))
}

/// Remove the raw keys belonging to a kind so content replacement /
/// clearing never leaves stale fields behind.
fn strip_kind_keys(raw: &mut RawExtra) {
    for key in [
        "text",
        "file",
        "subpath",
        "url",
        "label",
        "background",
        "backgroundStyle",
    ] {
        raw.shift_remove(key);
    }
}

fn restore_json(map: &RawExtra) -> String {
    serde_json::to_string(&Value::Object(map.clone())).expect("raw map serializes")
}

/// Apply one op; returns its inverse op(s) in forward order.
fn apply_one(canvas: &mut Canvas, op: &CanvasOp) -> Result<Vec<CanvasOp>, ApplyError> {
    match op {
        CanvasOp::CreateNode {
            id,
            content,
            x,
            y,
            width,
            height,
            color,
        } => {
            if id_taken(canvas, id) {
                return Err(ApplyError::DuplicateId(id.clone()));
            }
            canvas.nodes.push(Node {
                id: NodeId(id.clone()),
                kind: content_to_kind(content),
                x: *x,
                y: *y,
                width: *width,
                height: *height,
                color: color_of(color),
                raw: RawExtra::new(),
            });
            Ok(vec![CanvasOp::DeleteNode { id: id.clone() }])
        }
        CanvasOp::CreateGroup {
            id,
            label,
            x,
            y,
            width,
            height,
            color,
        } => {
            if id_taken(canvas, id) {
                return Err(ApplyError::DuplicateId(id.clone()));
            }
            canvas.nodes.push(Node {
                id: NodeId(id.clone()),
                kind: NodeKind::Group {
                    label: label.clone(),
                    background: None,
                },
                x: *x,
                y: *y,
                width: *width,
                height: *height,
                color: color_of(color),
                raw: RawExtra::new(),
            });
            Ok(vec![CanvasOp::DeleteNode { id: id.clone() }])
        }
        CanvasOp::UpdateNodeGeometry {
            id,
            x,
            y,
            width,
            height,
        } => {
            let idx = node_index(canvas, id)?;
            let node = &mut canvas.nodes[idx];
            let inverse = CanvasOp::UpdateNodeGeometry {
                id: id.clone(),
                x: node.x,
                y: node.y,
                width: node.width,
                height: node.height,
            };
            node.x = *x;
            node.y = *y;
            node.width = *width;
            node.height = *height;
            Ok(vec![inverse])
        }
        CanvasOp::SetNodeColor { id, color } => {
            let idx = node_index(canvas, id)?;
            let node = &mut canvas.nodes[idx];
            // Snapshot inverse: clearing + re-adding a key would lose
            // its original position, so undo restores the full node.
            let inverse = CanvasOp::RestoreNodeInPlace {
                node_json: restore_json(&node_map(node)),
            };
            node.color = color_of(color);
            if color.is_none() {
                node.raw.shift_remove("color");
            }
            Ok(vec![inverse])
        }
        CanvasOp::SetNodeContent { id, content } => {
            let idx = node_index(canvas, id)?;
            let node = &mut canvas.nodes[idx];
            if kind_to_content(&node.kind).is_none() {
                return Err(ApplyError::IsAGroup(id.clone()));
            }
            let inverse = CanvasOp::RestoreNodeInPlace {
                node_json: restore_json(&node_map(node)),
            };
            let converting = std::mem::discriminant(&content_to_kind(content))
                != std::mem::discriminant(&node.kind);
            node.kind = content_to_kind(content);
            if converting {
                strip_kind_keys(&mut node.raw);
            } else if matches!(content, CanvasNodeContent::File { subpath: None, .. }) {
                node.raw.shift_remove("subpath");
            }
            Ok(vec![inverse])
        }
        CanvasOp::DeleteNode { id } | CanvasOp::Ungroup { id } => {
            let idx = node_index(canvas, id)?;
            if matches!(op, CanvasOp::Ungroup { .. })
                && !matches!(canvas.nodes[idx].kind, NodeKind::Group { .. })
            {
                return Err(ApplyError::NotAGroup(id.clone()));
            }
            let node = canvas.nodes.remove(idx);
            let mut inverse = vec![CanvasOp::RestoreNode {
                node_json: restore_json(&node_map(&node)),
                position: idx as u32,
            }];
            // Record incident connections in ascending original order
            // (that's the order restore must re-insert them so each
            // recorded position is exact), then remove them from the
            // highest index down so the earlier indices stay valid.
            let incident: Vec<usize> = canvas
                .edges
                .iter()
                .enumerate()
                .filter(|(_, e)| e.from.0.0 == *id || e.to.0.0 == *id)
                .map(|(i, _)| i)
                .collect();
            for &edge_idx in &incident {
                inverse.push(CanvasOp::RestoreEdge {
                    edge_json: restore_json(&edge_map(&canvas.edges[edge_idx])),
                    position: edge_idx as u32,
                });
            }
            for &edge_idx in incident.iter().rev() {
                canvas.edges.remove(edge_idx);
            }
            Ok(inverse)
        }
        CanvasOp::AddEdge {
            id,
            from_node,
            from_side,
            to_node,
            to_side,
            from_end,
            to_end,
            label,
            color,
        } => {
            if id_taken(canvas, id) {
                return Err(ApplyError::DuplicateId(id.clone()));
            }
            for endpoint in [from_node, to_node] {
                if !canvas.nodes.iter().any(|n| n.id.0 == *endpoint) {
                    return Err(ApplyError::MissingEndpoint(endpoint.clone()));
                }
            }
            canvas.edges.push(Edge {
                id: EdgeId(id.clone()),
                from: (NodeId(from_node.clone()), *from_side),
                to: (NodeId(to_node.clone()), *to_side),
                from_end: *from_end,
                to_end: *to_end,
                label: label.clone(),
                color: color_of(color),
                raw: RawExtra::new(),
            });
            Ok(vec![CanvasOp::DeleteEdge { id: id.clone() }])
        }
        CanvasOp::UpdateEdge {
            id,
            from_side,
            to_side,
            from_end,
            to_end,
            label,
            color,
        } => {
            let idx = edge_index(canvas, id)?;
            let edge = &mut canvas.edges[idx];
            let inverse = CanvasOp::RestoreEdgeInPlace {
                edge_json: restore_json(&edge_map(edge)),
            };
            edge.from.1 = *from_side;
            edge.to.1 = *to_side;
            edge.from_end = *from_end;
            edge.to_end = *to_end;
            edge.label = label.clone();
            edge.color = color_of(color);
            for (key, present) in [
                ("fromSide", from_side.is_some()),
                ("toSide", to_side.is_some()),
                ("label", label.is_some()),
                ("color", color.is_some()),
            ] {
                if !present {
                    canvas.edges[idx].raw.shift_remove(key);
                }
            }
            Ok(vec![inverse])
        }
        CanvasOp::DeleteEdge { id } => {
            let idx = edge_index(canvas, id)?;
            let edge = canvas.edges.remove(idx);
            Ok(vec![CanvasOp::RestoreEdge {
                edge_json: restore_json(&edge_map(&edge)),
                position: idx as u32,
            }])
        }
        CanvasOp::RenameGroup { id, label } => {
            let idx = node_index(canvas, id)?;
            let node = &mut canvas.nodes[idx];
            if !matches!(node.kind, NodeKind::Group { .. }) {
                return Err(ApplyError::NotAGroup(id.clone()));
            }
            let inverse = CanvasOp::RestoreNodeInPlace {
                node_json: restore_json(&node_map(node)),
            };
            let NodeKind::Group {
                label: old_label, ..
            } = &mut node.kind
            else {
                unreachable!("checked above");
            };
            *old_label = label.clone();
            if label.is_none() {
                node.raw.shift_remove("label");
            }
            Ok(vec![inverse])
        }
        CanvasOp::RestoreNode {
            node_json,
            position,
        } => {
            let value: Value = serde_json::from_str(node_json)
                .map_err(|e| ApplyError::BadRestorePayload(e.to_string()))?;
            let mut warnings = Vec::new();
            let node =
                super::parse_node(&value, 0, &std::collections::HashSet::new(), &mut warnings)
                    .map_err(|w| ApplyError::BadRestorePayload(format!("{w:?}")))?;
            if id_taken(canvas, &node.id.0) {
                return Err(ApplyError::DuplicateId(node.id.0.clone()));
            }
            let idx = (*position as usize).min(canvas.nodes.len());
            let inverse = CanvasOp::DeleteNode {
                id: node.id.0.clone(),
            };
            canvas.nodes.insert(idx, node);
            Ok(vec![inverse])
        }
        CanvasOp::RestoreNodeInPlace { node_json } => {
            let value: Value = serde_json::from_str(node_json)
                .map_err(|e| ApplyError::BadRestorePayload(e.to_string()))?;
            let mut warnings = Vec::new();
            let node =
                super::parse_node(&value, 0, &std::collections::HashSet::new(), &mut warnings)
                    .map_err(|w| ApplyError::BadRestorePayload(format!("{w:?}")))?;
            let idx = node_index(canvas, &node.id.0)?;
            let inverse = CanvasOp::RestoreNodeInPlace {
                node_json: restore_json(&node_map(&canvas.nodes[idx])),
            };
            canvas.nodes[idx] = node;
            Ok(vec![inverse])
        }
        CanvasOp::RestoreEdgeInPlace { edge_json } => {
            let value: Value = serde_json::from_str(edge_json)
                .map_err(|e| ApplyError::BadRestorePayload(e.to_string()))?;
            let mut warnings = Vec::new();
            let edge =
                super::parse_edge(&value, 0, &std::collections::HashSet::new(), &mut warnings)
                    .map_err(|w| ApplyError::BadRestorePayload(format!("{w:?}")))?;
            let idx = edge_index(canvas, &edge.id.0)?;
            let inverse = CanvasOp::RestoreEdgeInPlace {
                edge_json: restore_json(&edge_map(&canvas.edges[idx])),
            };
            canvas.edges[idx] = edge;
            Ok(vec![inverse])
        }
        CanvasOp::RestoreEdge {
            edge_json,
            position,
        } => {
            let value: Value = serde_json::from_str(edge_json)
                .map_err(|e| ApplyError::BadRestorePayload(e.to_string()))?;
            let mut warnings = Vec::new();
            let edge =
                super::parse_edge(&value, 0, &std::collections::HashSet::new(), &mut warnings)
                    .map_err(|w| ApplyError::BadRestorePayload(format!("{w:?}")))?;
            if canvas.edges.iter().any(|e| e.id == edge.id) {
                return Err(ApplyError::DuplicateId(edge.id.0.clone()));
            }
            let idx = (*position as usize).min(canvas.edges.len());
            let inverse = CanvasOp::DeleteEdge {
                id: edge.id.0.clone(),
            };
            canvas.edges.insert(idx, edge);
            Ok(vec![inverse])
        }
    }
}

#[cfg(test)]
#[path = "apply_tests.rs"]
mod tests;
