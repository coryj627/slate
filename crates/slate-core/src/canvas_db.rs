// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! SQLite write path for the canvas index (`canvas_nodes` /
//! `canvas_edges`, migration 020, Milestone T #361).
//!
//! Rows are scanner-managed derived data: rebuilt wholesale per file on
//! the scan slow path and on external file change, DELETE-then-INSERT
//! keyed by `file_id`, mirroring `tags_db` / `properties_db`. The
//! `.canvas` file on disk is the source of truth; the index is
//! regenerable at any time.
//!
//! Derivation reuses `canvas::parse` + `canvas::model::derive_with` —
//! never a second parser — so the DB rows, the open-canvas handle, and
//! every UI surface agree on one reading order and one title per card
//! (t1 shared-architecture requirement).

use rusqlite::{Transaction, params};

use crate::VaultError;
use crate::canvas::model::{CanvasModel, FileTitleSource, derive_with};
use crate::canvas::{self, CanvasColor, NodeKind, Side, color_name};

/// Serialize the ancestor-path list as a JSON array for the
/// `group_path` column (denormalized so outline reads are one query).
fn group_path_json(path: &[String]) -> String {
    serde_json::to_string(path).unwrap_or_else(|_| "[]".to_string())
}

fn color_raw(color: &Option<CanvasColor>) -> Option<String> {
    color.as_ref().map(|c| match c {
        CanvasColor::Preset(p) => p.to_string(),
        CanvasColor::Hex(s) => s.clone(),
    })
}

fn side_str(side: Option<Side>) -> Option<&'static str> {
    side.map(|s| match s {
        Side::Top => "top",
        Side::Right => "right",
        Side::Bottom => "bottom",
        Side::Left => "left",
    })
}

/// Atomically replace the canvas index rows for `file_id` from the
/// `.canvas` source text. Returns the derived model so callers that
/// need it (the open-canvas path) don't re-derive.
///
/// A degraded parse (`is_load_degraded`) clears the file's rows — the
/// UI shows the t0 §5 error state from the live parse warnings, and an
/// empty index is honest about "nothing modelable here".
pub(crate) fn replace_canvas_for_file(
    tx: &Transaction,
    file_id: i64,
    canvas_source: &str,
    titles: &dyn FileTitleSource,
) -> Result<(canvas::Canvas, Vec<canvas::CanvasWarning>, CanvasModel), VaultError> {
    tx.execute(
        "DELETE FROM canvas_nodes WHERE file_id = ?1",
        params![file_id],
    )?;
    tx.execute(
        "DELETE FROM canvas_edges WHERE file_id = ?1",
        params![file_id],
    )?;

    let (parsed, warnings) = canvas::parse(canvas_source);
    let model = derive_with(&parsed, titles);

    let order_of = |id: &canvas::NodeId| -> i64 {
        model
            .reading_order
            .iter()
            .position(|n| n == id)
            .expect("every node is in reading order (census invariant)") as i64
    };

    {
        let mut stmt = tx.prepare_cached(
            "INSERT INTO canvas_nodes
                (file_id, node_id, kind, title, group_id, group_path, depth,
                 order_idx, ordinal_n, total_m, conn_count, in_count, out_count,
                 color, color_name, target, x, y, w, h)
             VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11,?12,?13,?14,?15,?16,?17,?18,?19,?20)",
        )?;
        for node in &parsed.nodes {
            let summary = &model.summaries[&node.id];
            let target = match &node.kind {
                NodeKind::File { file, .. } => file.clone(),
                NodeKind::Link { url } => url.clone(),
                _ => String::new(),
            };
            stmt.execute(params![
                file_id,
                node.id.0,
                summary.kind_label,
                summary.display_title,
                summary.container.as_ref().map(|c| c.0.as_str()),
                group_path_json(&summary.group_path),
                summary.group_path.len() as i64,
                order_of(&node.id),
                summary.position_in_container as i64,
                summary.container_size as i64,
                summary.connection_count as i64,
                summary.in_count as i64,
                summary.out_count as i64,
                color_raw(&node.color),
                node.color.as_ref().map(color_name),
                target,
                node.x,
                node.y,
                node.width,
                node.height,
            ])?;
        }
    }

    {
        let mut stmt = tx.prepare_cached(
            "INSERT INTO canvas_edges
                (file_id, edge_id, from_id, to_id, from_side, to_side,
                 from_end, to_end, label, color, color_name)
             VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10,?11)",
        )?;
        for edge in &parsed.edges {
            let end = |e: canvas::EndStyle| match e {
                canvas::EndStyle::None => "none",
                canvas::EndStyle::Arrow => "arrow",
            };
            stmt.execute(params![
                file_id,
                edge.id.0,
                edge.from.0.0,
                edge.to.0.0,
                side_str(edge.from.1),
                side_str(edge.to.1),
                end(edge.from_end),
                end(edge.to_end),
                edge.label,
                color_raw(&edge.color),
                edge.color.as_ref().map(color_name),
            ])?;
        }
    }

    Ok((parsed, warnings, model))
}
