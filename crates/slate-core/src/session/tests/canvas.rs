// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Canvas session API tests (#361): migration, scan indexing, the
//! handle-based read surface, quick-open filter, and degraded loads.

use super::common::*;
use super::*;

const SAMPLE: &str = include_str!("../../../tests/fixtures/canvas/sample.canvas");
const MALFORMED: &str = include_str!("../../../tests/fixtures/canvas/malformed.canvas");

fn canvas_vault() -> (tempfile::TempDir, VaultSession) {
    make_vault(|p| {
        p.write_file("board.canvas", SAMPLE.as_bytes()).unwrap();
        p.write_file(
            "notes/canvas research.md",
            b"---\ntitle: Canvas Research Log\n---\n# Body\n",
        )
        .unwrap();
        p.write_file("specs/interaction.md", b"# Announcement grammar\n")
            .unwrap();
    })
}

#[test]
fn migration_creates_canvas_tables() {
    let (_tmp, session) = make_vault(|_| {});
    let conn = session.conn.lock().unwrap();
    for table in ["canvas_nodes", "canvas_edges"] {
        let count: i64 = conn
            .query_row(
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?1",
                rusqlite::params![table],
                |row| row.get(0),
            )
            .unwrap();
        assert_eq!(count, 1, "missing table {table}");
    }
}

#[test]
fn scan_indexes_canvas_rows_with_frontmatter_titles() {
    let (_tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();

    let conn = session.conn.lock().unwrap();
    let node_count: i64 = conn
        .query_row("SELECT COUNT(*) FROM canvas_nodes", [], |r| r.get(0))
        .unwrap();
    let edge_count: i64 = conn
        .query_row("SELECT COUNT(*) FROM canvas_edges", [], |r| r.get(0))
        .unwrap();
    assert_eq!(node_count, 9);
    assert_eq!(edge_count, 5);

    // The file card resolves its note's frontmatter title — even though
    // the canvas sorts before the note alphabetically, the canvas pass
    // runs after the walk (first-scan ordering).
    let title: String = conn
        .query_row(
            "SELECT title FROM canvas_nodes WHERE node_id = 'card-notes'",
            [],
            |r| r.get(0),
        )
        .unwrap();
    assert_eq!(title, "Canvas Research Log");

    // Derived positional columns match the model rules.
    let (depth, ordinal, total): (i64, i64, i64) = conn
        .query_row(
            "SELECT depth, ordinal_n, total_m FROM canvas_nodes WHERE node_id = 'card-question'",
            [],
            |r| Ok((r.get(0)?, r.get(1)?, r.get(2)?)),
        )
        .unwrap();
    assert_eq!((depth, ordinal, total), (1, 1, 4));
}

#[test]
fn rescan_reflects_external_change_and_note_retitle() {
    let (tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();

    // Retitle the note externally; the .canvas bytes don't change.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file(
            "notes/canvas research.md",
            b"---\ntitle: Renamed Log\n---\n# Body\n",
        )
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let conn = session.conn.lock().unwrap();
    let title: String = conn
        .query_row(
            "SELECT title FROM canvas_nodes WHERE node_id = 'card-notes'",
            [],
            |r| r.get(0),
        )
        .unwrap();
    assert_eq!(title, "Renamed Log");
}

#[test]
fn open_canvas_reads_and_navigates() {
    let (_tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();

    let info = session.open_canvas("board.canvas").unwrap();
    assert!(!info.degraded);
    assert!(info.warnings.is_empty());
    assert_eq!((info.node_count, info.edge_count), (9, 5));

    // Outline: reading order, depth, N-of-M.
    let outline = session.canvas_outline(info.handle).unwrap();
    assert_eq!(outline.len(), 9);
    assert_eq!(outline[0].node_id, "grp-research");
    assert_eq!(outline[0].depth, 0);
    assert_eq!(outline[1].node_id, "card-question");
    assert_eq!(outline[1].depth, 1);
    assert_eq!(outline[1].group_path, vec!["Research".to_string()]);
    assert_eq!((outline[1].ordinal_n, outline[1].total_m), (1, 4));
    assert_eq!(outline[1].color_name.as_deref(), Some("red"));

    // Table: targets per kind.
    let rows = session.canvas_table_rows(info.handle).unwrap();
    let by_id = |id: &str| rows.iter().find(|r| r.node_id == id).unwrap();
    assert_eq!(by_id("card-notes").target, "notes/canvas research.md");
    assert_eq!(
        by_id("card-jsoncanvas").target,
        "https://jsoncanvas.org/spec/1.0"
    );
    assert_eq!(by_id("card-question").target, "");
    assert_eq!(by_id("card-diagram").kind, "image");

    // Neighbors with direction + phrase data.
    let neighbors = session
        .canvas_neighbors(info.handle, "card-question")
        .unwrap();
    assert_eq!(neighbors.len(), 3);
    let out_edge = neighbors
        .iter()
        .find(|n| n.edge_id == "edge-q-evidence")
        .unwrap();
    assert_eq!(
        out_edge.direction,
        crate::canvas::model::EdgeDirection::Outgoing
    );
    assert_eq!(out_edge.other_title, "Evidence so far");
    assert!(out_edge.self_is_from);

    // Where am I?
    let ctx = session
        .canvas_where_am_i(info.handle, "card-question")
        .unwrap();
    assert_eq!(ctx.title, "Core question");
    assert_eq!(ctx.group_path, vec!["Research".to_string()]);
    assert_eq!((ctx.in_count, ctx.out_count), (1, 2));

    // Placement + overlap.
    let p = session
        .canvas_place_new(
            info.handle,
            Some("card-loose".to_string()),
            260.0,
            140.0,
            None,
            Vec::new(),
        )
        .unwrap();
    assert!(matches!(
        &p.relative,
        crate::canvas::placement::RelativeDesc::Below(t) if t == "Unfiled thought"
    ));
    let overlaps = session
        .canvas_check_overlap(
            info.handle,
            CanvasRectArg {
                x: 0.0,
                y: 0.0,
                width: 100.0,
                height: 100.0,
            },
            vec!["card-question".to_string()],
        )
        .unwrap();
    assert!(overlaps.is_empty());

    let sp = session
        .canvas_place_set(
            info.handle,
            Some("card-loose".to_string()),
            vec![
                CanvasRectArg {
                    x: 0.0,
                    y: 0.0,
                    width: 100.0,
                    height: 50.0,
                },
                CanvasRectArg {
                    x: 150.0,
                    y: 30.0,
                    width: 100.0,
                    height: 50.0,
                },
            ],
            None,
            Vec::new(),
        )
        .unwrap();
    assert_eq!(sp.origins.len(), 2);
    assert_eq!(sp.origins[1].0 - sp.origins[0].0, 150.0);
    assert_eq!(sp.origins[1].1 - sp.origins[0].1, 30.0);

    // Close: handle becomes invalid, closing again is a no-op.
    session.close_canvas(info.handle);
    assert!(session.canvas_outline(info.handle).is_err());
    session.close_canvas(info.handle);
}

#[test]
fn open_canvas_works_before_first_scan() {
    let (_tmp, session) = canvas_vault();
    let info = session.open_canvas("board.canvas").unwrap();
    let outline = session.canvas_outline(info.handle).unwrap();
    assert_eq!(outline.len(), 9);
    // Frontmatter title is unavailable pre-scan (properties not yet
    // indexed) — the humanized-filename floor applies, never a path.
    let notes = outline.iter().find(|r| r.node_id == "card-notes").unwrap();
    assert_eq!(notes.title, "canvas research");
}

#[test]
fn malformed_canvas_surfaces_warnings_not_failure() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("broken.canvas", MALFORMED.as_bytes()).unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("broken.canvas").unwrap();
    assert!(!info.degraded);
    assert_eq!(info.node_count, 2);
    assert!(
        info.warnings
            .iter()
            .any(|w| w.kind == CanvasLoadWarningKind::SkippedEntry)
    );
    assert!(
        info.warnings
            .iter()
            .any(|w| w.kind == CanvasLoadWarningKind::DanglingEdge)
    );
}

#[test]
fn degraded_canvas_is_flagged_and_unindexed() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("bad.canvas", b"not json at all").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("bad.canvas").unwrap();
    assert!(info.degraded);
    assert_eq!(info.node_count, 0);
    assert!(
        info.warnings
            .iter()
            .any(|w| w.kind == CanvasLoadWarningKind::ParseFailed)
    );
    assert!(session.canvas_outline(info.handle).unwrap().is_empty());
}

#[test]
fn file_filter_markdown_and_canvas() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("a.md", b"").unwrap();
        p.write_file("b.canvas", b"{}").unwrap();
        p.write_file("c.txt", b"").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();

    let names = |filter| {
        session
            .list_files(filter, Paging::first(100))
            .unwrap()
            .items
            .iter()
            .map(|f| f.name.clone())
            .collect::<Vec<_>>()
    };
    assert_eq!(names(FileFilter::MarkdownOnly), vec!["a.md"]);
    assert_eq!(
        names(FileFilter::MarkdownAndCanvas),
        vec!["a.md", "b.canvas"]
    );
    assert_eq!(names(FileFilter::All).len(), 3);
}

#[test]
fn canvas_rows_pruned_when_file_deleted() {
    let (tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();

    std::fs::remove_file(tmp.path().join("board.canvas")).unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let conn = session.conn.lock().unwrap();
    // The files row disappears via the scanner's prune; ON DELETE
    // CASCADE clears the canvas rows with it (regenerable index).
    let orphans: i64 = conn
        .query_row(
            "SELECT COUNT(*) FROM canvas_nodes cn
             LEFT JOIN files f ON f.id = cn.file_id
             WHERE f.id IS NULL",
            [],
            |r| r.get(0),
        )
        .unwrap();
    assert_eq!(orphans, 0);
}

#[test]
fn canvas_apply_writes_reindexes_and_returns_inverse() {
    use crate::canvas::apply::{CanvasAction, CanvasNodeContent, CanvasOp};

    let (_tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("board.canvas").unwrap();
    let disk_before = session.read_text("board.canvas").unwrap();

    // One action = create card + connect it (the create-connected-card
    // shape, #525) = one write, one inverse.
    let result = session
        .canvas_apply(
            info.handle,
            CanvasAction {
                name: "create connected card".into(),
                ops: vec![
                    CanvasOp::CreateNode {
                        id: "cc-1".into(),
                        content: CanvasNodeContent::Text {
                            text: "Connected thought".into(),
                        },
                        x: 0.0,
                        y: 640.0,
                        width: 260.0,
                        height: 140.0,
                        color: None,
                    },
                    CanvasOp::AddEdge {
                        id: "cc-1-edge".into(),
                        from_node: "card-loose".into(),
                        from_side: None,
                        to_node: "cc-1".into(),
                        to_side: None,
                        from_end: crate::canvas::EndStyle::None,
                        to_end: crate::canvas::EndStyle::Arrow,
                        label: None,
                        color: None,
                    },
                ],
            },
        )
        .unwrap();

    // Written through to disk…
    let disk_after = session.read_text("board.canvas").unwrap();
    assert!(disk_after.contains("Connected thought"));
    assert_ne!(disk_before, disk_after);
    // …reindexed (outline sees the new card with a connection)…
    let outline = session.canvas_outline(info.handle).unwrap();
    let new_row = outline.iter().find(|r| r.node_id == "cc-1").unwrap();
    assert_eq!(new_row.title, "Connected thought");
    assert_eq!(new_row.connection_count, 1);
    // …and the handle's model followed (navigation sees it too).
    let neighbors = session.canvas_neighbors(info.handle, "cc-1").unwrap();
    assert_eq!(neighbors.len(), 1);
    assert_eq!(neighbors[0].other_title, "Unfiled thought");

    // Undo via the returned inverse: disk returns to the exact bytes.
    let undo = session.canvas_apply(info.handle, result.inverse).unwrap();
    assert_eq!(session.read_text("board.canvas").unwrap(), disk_before);
    assert!(session.canvas_neighbors(info.handle, "cc-1").is_err());

    // Redo via the undo's inverse.
    session.canvas_apply(info.handle, undo.inverse).unwrap();
    assert_eq!(session.read_text("board.canvas").unwrap(), disk_after);
}

#[test]
fn canvas_apply_conflicts_on_external_change_and_rejects_bad_ops() {
    use crate::canvas::apply::{CanvasAction, CanvasOp};

    let (tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("board.canvas").unwrap();

    // Invalid op: rejected, nothing written.
    let disk = session.read_text("board.canvas").unwrap();
    let err = session
        .canvas_apply(
            info.handle,
            CanvasAction {
                name: "bad".into(),
                ops: vec![CanvasOp::DeleteNode { id: "ghost".into() }],
            },
        )
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }));
    assert_eq!(session.read_text("board.canvas").unwrap(), disk);

    // External writer changes the file → next apply must conflict,
    // never blind-overwrite (t0 §5).
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file("board.canvas", b"{\"nodes\":[],\"edges\":[]}")
        .unwrap();
    let err = session
        .canvas_apply(
            info.handle,
            CanvasAction {
                name: "move".into(),
                ops: vec![CanvasOp::UpdateNodeGeometry {
                    id: "card-loose".into(),
                    x: 20.0,
                    y: 480.0,
                    width: 200.0,
                    height: 100.0,
                }],
            },
        )
        .unwrap_err();
    assert!(matches!(err, VaultError::WriteConflict { .. }), "{err:?}");
}

#[test]
fn canvas_apply_refuses_degraded_canvas() {
    use crate::canvas::apply::{CanvasAction, CanvasOp};

    let (_tmp, session) = make_vault(|p| {
        p.write_file("bad.canvas", b"not json").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("bad.canvas").unwrap();
    assert!(info.degraded);
    let err = session
        .canvas_apply(
            info.handle,
            CanvasAction {
                name: "any".into(),
                ops: vec![CanvasOp::DeleteNode { id: "x".into() }],
            },
        )
        .unwrap_err();
    assert!(matches!(err, VaultError::InvalidArgument { .. }));
    // The broken file is untouched.
    assert_eq!(session.read_text("bad.canvas").unwrap(), "not json");
}
