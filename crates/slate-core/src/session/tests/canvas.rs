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

    let conn = session.conn.lock().unwrap();
    let meta: (i64, i64, String) = conn
        .query_row(
            "SELECT fm.word_count, fm.char_count, fm.preview
             FROM file_meta fm
             JOIN files f ON f.id = fm.file_id
             WHERE f.path = 'board.canvas'",
            [],
            |row| Ok((row.get(0)?, row.get(1)?, row.get(2)?)),
        )
        .unwrap();
    assert_eq!(meta, (0, 0, String::new()));
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
fn note_move_rewrites_canvas_file_references() {
    let (_tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();

    // Rename the note the sample canvas's plain file card references.
    let report = session
        .rename_file("notes/canvas research.md", "research log.md")
        .unwrap();
    assert!(
        report.rewritten.iter().any(|r| r.path == "board.canvas"),
        "canvas rewrite must be reported, not silent: {report:?}"
    );

    // The canvas on disk now points at the new path…
    let text = session.read_text("board.canvas").unwrap();
    assert!(text.contains("notes/research log.md"), "{text}");
    assert!(!text.contains("notes/canvas research.md"));
    // …and the rewrite is per-field: unrelated lines are untouched.
    assert!(text.contains("\"id\":\"card-question\",\"type\":\"text\""));
    assert!(text.contains("\"subpath\":\"#Announcement grammar\""));

    // The index followed (save path reindexes canvas rows).
    let conn = session.conn.lock().unwrap();
    let target: String = conn
        .query_row(
            "SELECT target FROM canvas_nodes WHERE node_id = 'card-notes'",
            [],
            |r| r.get(0),
        )
        .unwrap();
    assert_eq!(target, "notes/research log.md");
}

#[test]
fn subpath_card_rewrites_and_unreferenced_moves_leave_canvas_alone() {
    let (_tmp, session) = make_vault(|p| {
        p.write_file("board.canvas", SAMPLE.as_bytes()).unwrap();
        p.write_file("notes/canvas research.md", b"# n").unwrap();
        p.write_file("specs/interaction.md", b"# Announcement grammar\n")
            .unwrap();
        p.write_file("unrelated.md", b"# lonely").unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let before = session.read_text("board.canvas").unwrap();

    // The subpath card references specs/interaction.md — the path
    // rewrites, the subpath anchor rides along untouched.
    let report = session
        .rename_file("specs/interaction.md", "renamed spec.md")
        .unwrap();
    assert!(report.rewritten.iter().any(|r| r.path == "board.canvas"));
    let after = session.read_text("board.canvas").unwrap();
    assert!(after.contains("\"file\":\"specs/renamed spec.md\""));
    assert!(after.contains("\"subpath\":\"#Announcement grammar\""));
    assert_ne!(before, after);

    // A rename nothing on the canvas references leaves it
    // byte-identical (no gratuitous canvas churn) and unreported.
    let untouched_before = session.read_text("board.canvas").unwrap();
    let report = session
        .rename_file("unrelated.md", "still lonely.md")
        .unwrap();
    assert!(report.rewritten.iter().all(|r| r.path != "board.canvas"));
    let untouched_after = session.read_text("board.canvas").unwrap();
    assert_eq!(untouched_before, untouched_after);
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

#[test]
fn canvas_apply_journals_named_semantic_entries() {
    use crate::canvas::apply::{CanvasAction, CanvasOp, action_from_json};

    let (_tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("board.canvas").unwrap();

    let result = session
        .canvas_apply(
            info.handle,
            CanvasAction {
                name: "move 'Unfiled thought'".into(),
                ops: vec![CanvasOp::UpdateNodeGeometry {
                    id: "card-loose".into(),
                    x: 20.0,
                    y: 480.0,
                    width: 200.0,
                    height: 100.0,
                }],
            },
        )
        .unwrap();

    // The per-file journal now holds BOTH the byte-level text entry
    // (from the save path) and the semantic CanvasApply record.
    let conn = session.conn.lock().unwrap();
    let log_name: String = conn
        .query_row(
            "SELECT oplog_name FROM files WHERE path = 'board.canvas'",
            [],
            |r| r.get(0),
        )
        .unwrap();
    drop(conn);
    let entries = crate::oplog::read_oplog(&session.config.cache_dir, &log_name).unwrap();
    let semantic: Vec<_> = entries
        .iter()
        .filter(|e| e.op_kind == crate::oplog::OpKind::CanvasApply)
        .collect();
    assert_eq!(
        semantic.len(),
        1,
        "one committed action = one journal entry"
    );
    let payload: serde_json::Value = serde_json::from_slice(&semantic[0].payload_bytes).unwrap();
    assert_eq!(
        payload.get("name").and_then(|v| v.as_str()),
        Some("move 'Unfiled thought'")
    );
    // The stored inverse decodes and matches what the API returned.
    let stored_inverse = action_from_json(payload.get("inverse").unwrap()).unwrap();
    assert_eq!(stored_inverse, result.inverse);
    assert_eq!(semantic[0].content_hash_after, result.new_content_hash);

    // Text replay (Milestone O reconstruct) ignores semantic records.
    let replayed = crate::oplog::reconstruct_at_tail(&entries).unwrap();
    assert_eq!(replayed, session.read_text("board.canvas").unwrap());
}

// --- W6-1 PR 0b: the structural queries over a handle ---------------------

/// The queries answer through the handle, and every one of them refuses
/// a closed handle and an unknown node the same way (0b-2, 0b-8).
#[test]
fn canvas_structural_queries_answer_through_the_handle() {
    let (_tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("board.canvas").unwrap();
    let h = info.handle;

    assert_eq!(
        session.canvas_parent_of(h, "card-question").unwrap(),
        Some("grp-research".to_string())
    );
    assert_eq!(session.canvas_parent_of(h, "card-loose").unwrap(), None);
    assert_eq!(
        session.canvas_children_of(h, "grp-research").unwrap(),
        ["card-question", "card-evidence", "card-notes", "card-spec"]
    );
    assert_eq!(
        session
            .canvas_order_nodes(
                h,
                vec![
                    "card-loose".into(),
                    "ghost".into(),
                    "grp-research".into(),
                    "card-loose".into()
                ]
            )
            .unwrap(),
        ["grp-research", "card-loose"]
    );
    let hops = session.canvas_trace_path(h, "card-question").unwrap();
    assert_eq!(
        hops.iter()
            .map(|hop| (hop.edge_id.as_str(), hop.node_id.as_str()))
            .collect::<Vec<_>>(),
        [("edge-q-evidence", "card-evidence")]
    );
    let bounds = session.canvas_bounds(h).unwrap().expect("not empty");
    assert_eq!((bounds.x, bounds.y), (-40.0, -40.0));
    let around = session
        .canvas_group_rect_around(h, vec!["card-question".into()])
        .unwrap()
        .expect("member resolves");
    assert_eq!((around.x, around.y), (-40.0, -40.0));
    assert_eq!(
        session
            .canvas_group_rect_around(h, vec!["ghost".into()])
            .unwrap(),
        None
    );
    // Inspiration is (600, -40) 320 × 400 holding two 240 × 140 cards:
    // a small card fits at the inset, a card the width of the existing
    // ones does not fit anywhere and answers `Full` rather than a point
    // outside the frame.
    assert_eq!(
        session
            .canvas_place_inside_group(h, "grp-inspiration", 20.0, 20.0, Vec::new())
            .unwrap(),
        crate::canvas::placement::InsideGroupPlacement::Placed { x: 620.0, y: 0.0 }
    );
    assert_eq!(
        session
            .canvas_place_inside_group(h, "grp-inspiration", 100.0, 50.0, Vec::new())
            .unwrap(),
        crate::canvas::placement::InsideGroupPlacement::Full
    );
    assert_eq!(
        session.canvas_filter(h, "  ReSeArCh ").unwrap(),
        [
            "grp-research",
            "card-question",
            "card-evidence",
            "card-notes",
            "card-spec"
        ]
    );
    assert_eq!(
        session.canvas_filter(h, "").unwrap().len(),
        session.canvas_outline(h).unwrap().len(),
        "an empty query matches everything"
    );
    let descs = session
        .canvas_describe_relative(
            h,
            CanvasRectArg {
                x: 0.0,
                y: 600.0,
                width: 200.0,
                height: 100.0,
            },
            vec!["card-loose".to_string()],
        )
        .unwrap();
    assert!(!descs.is_empty());

    // An unknown node is `bad_node` wherever a node is named — that is
    // how a caller tells "no parent" from "no such node".
    for outcome in [
        session.canvas_parent_of(h, "ghost").err(),
        session.canvas_children_of(h, "ghost").err(),
        session.canvas_trace_path(h, "ghost").err(),
        session
            .canvas_place_inside_group(h, "ghost", 10.0, 10.0, Vec::new())
            .err(),
    ] {
        assert!(
            matches!(outcome, Some(VaultError::InvalidArgument { .. })),
            "unknown node must be rejected"
        );
    }

    // …and a closed handle is `bad_handle` everywhere.
    session.close_canvas(h);
    assert!(session.canvas_parent_of(h, "card-loose").is_err());
    assert!(session.canvas_children_of(h, "grp-research").is_err());
    assert!(session.canvas_order_nodes(h, Vec::new()).is_err());
    assert!(session.canvas_trace_path(h, "card-loose").is_err());
    assert!(session.canvas_bounds(h).is_err());
    assert!(session.canvas_group_rect_around(h, Vec::new()).is_err());
    assert!(
        session
            .canvas_place_inside_group(h, "grp-research", 10.0, 10.0, Vec::new())
            .is_err()
    );
    assert!(session.canvas_filter(h, "x").is_err());
    assert!(
        session
            .canvas_describe_relative(
                h,
                CanvasRectArg {
                    x: 0.0,
                    y: 0.0,
                    width: 1.0,
                    height: 1.0
                },
                Vec::new()
            )
            .is_err()
    );
}

/// Contract 0b-6: `speakable_name` reaches the two SQLite-served row
/// types by an in-memory join against the handle's model, and the four
/// record types therefore agree on one answer per node.
#[test]
fn speakable_names_join_onto_the_indexed_rows() {
    let (_tmp, session) = make_vault(|p| {
        // Two cards share a title and a third owns the obvious ordinal
        // — the case that separates core's algorithm from mac's.
        p.write_file(
            "twins.canvas",
            br#"{"nodes":[
                {"id":"t1","type":"text","text":"Same","x":0,"y":0,"width":10,"height":10},
                {"id":"t2","type":"text","text":"Same","x":0,"y":40,"width":10,"height":10},
                {"id":"t3","type":"text","text":"Same 2","x":0,"y":80,"width":10,"height":10}
            ],"edges":[]}"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("twins.canvas").unwrap();
    let h = info.handle;

    let outline = session.canvas_outline(h).unwrap();
    let spoken: Vec<(&str, &str)> = outline
        .iter()
        .map(|r| (r.node_id.as_str(), r.speakable_name.as_str()))
        .collect();
    assert_eq!(spoken, [("t1", "Same"), ("t2", "Same 3"), ("t3", "Same 2")]);
    // The title column is UNCHANGED — the join adds a field, it does
    // not rewrite one (0b-6, CD-23).
    assert!(outline.iter().all(|r| r.title.starts_with("Same")));
    assert_eq!(outline[1].title, "Same");

    // Table rows, scene nodes and Where-am-I agree with the outline.
    for row in session.canvas_table_rows(h).unwrap() {
        let expected = spoken
            .iter()
            .find(|(id, _)| *id == row.node_id)
            .expect("row is in the outline")
            .1;
        assert_eq!(row.speakable_name, expected);
    }
    let (scene_nodes, _) = session.canvas_scene(h).unwrap();
    for node in scene_nodes {
        let expected = spoken
            .iter()
            .find(|(id, _)| *id == node.node_id)
            .expect("node is in the outline")
            .1;
        assert_eq!(node.speakable_name, expected);
        let where_am_i = session.canvas_where_am_i(h, &node.node_id).unwrap();
        assert_eq!(where_am_i.speakable_name, expected);
    }
}

/// Contract 0b-6's join reads the handle's OPEN-TIME model, and a
/// rescan rewrites `canvas_nodes` underneath it. Holding a handle
/// therefore guarantees a model, but not that the model and the rows
/// agree about which nodes exist — so the join can miss, and a missing
/// name must not leave a card unaddressable.
#[test]
fn speakable_name_falls_back_to_the_title_when_the_rows_outrun_the_handle() {
    let (tmp, session) = make_vault(|p| {
        p.write_file(
            "board.canvas",
            br#"{"nodes":[
                {"id":"before","type":"text","text":"Original","x":0,"y":0,"width":10,"height":10}
            ],"edges":[]}"#,
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("board.canvas").unwrap();
    let h = info.handle;
    assert_eq!(
        session.canvas_outline(h).unwrap()[0].speakable_name,
        "Original"
    );

    // An external writer replaces the card with one carrying a
    // DIFFERENT id, and a rescan re-derives the index rows. The open
    // handle's model still knows only `before`.
    let provider = FsVaultProvider::new(tmp.path().to_path_buf());
    provider
        .write_file(
            "board.canvas",
            br#"{"nodes":[
                {"id":"after","type":"text","text":"Replaced","x":0,"y":0,"width":10,"height":10}
            ],"edges":[]}"#,
        )
        .unwrap();
    session.scan_initial(&CancelToken::new()).unwrap();

    let outline = session.canvas_outline(h).unwrap();
    assert_eq!(outline.len(), 1);
    assert_eq!(outline[0].node_id, "after", "the rows moved on");
    assert_eq!(
        outline[0].speakable_name, "Replaced",
        "a joined name that misses falls back to the row's own title, never to \
         the empty string — an empty speakable name is a card Voice Control \
         cannot address at all"
    );
    let table = session.canvas_table_rows(h).unwrap();
    assert_eq!(table[0].speakable_name, "Replaced");

    // Reopening reconciles both halves, which is the host's recovery.
    let reopened = session.open_canvas("board.canvas").unwrap();
    assert_eq!(
        session.canvas_outline(reopened.handle).unwrap()[0].speakable_name,
        "Replaced"
    );
}

/// A card's rect is not a container: `canvas_place_inside_group`
/// refuses a non-group id rather than answering geometry "inside"
/// something that cannot hold it (0b-12; PR E is the first consumer).
#[test]
fn place_inside_group_refuses_a_node_that_is_not_a_group() {
    let (_tmp, session) = canvas_vault();
    session.scan_initial(&CancelToken::new()).unwrap();
    let info = session.open_canvas("board.canvas").unwrap();

    let refused = session
        .canvas_place_inside_group(info.handle, "card-question", 10.0, 10.0, Vec::new())
        .expect_err("a text card is not a group");
    match refused {
        VaultError::InvalidArgument { ref message } => {
            assert!(message.contains("not a group"), "{message}");
            // Distinct from the unknown-node message: the two send a
            // caller to different fixes.
            assert!(!message.contains("not found"), "{message}");
        }
        other => panic!("expected InvalidArgument, got {other:?}"),
    }

    // The real group still answers.
    assert!(matches!(
        session
            .canvas_place_inside_group(info.handle, "grp-inspiration", 20.0, 20.0, Vec::new())
            .unwrap(),
        crate::canvas::placement::InsideGroupPlacement::Placed { .. }
    ));
}
