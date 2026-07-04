-- Migration 020: canvas index (Milestone T, #361).
--
-- Derived, regenerable rows for `.canvas` files — the `.canvas` source
-- on disk is the single source of truth; these tables are rebuilt from
-- `slate_core::canvas::{parse, model::derive}` on scan and on external
-- change, exactly like every other content type. Derived columns (the
-- t1 "columns, not a model blob" decision) make `canvas_outline` and
-- `canvas_table_rows` single indexed queries at the 2,000-node scale
-- budget (§K).
--
-- The t1 draft sketched a separate `canvas_files(path, hash,
-- parsed_at)` registry; that role is already played by `files`
-- (which rows every vault file with `content_hash` + `indexed_at_ms`),
-- so canvas rows key on `files.id` instead of duplicating a registry.
--
-- canvas_nodes columns:
--   file_id     — owning `.canvas` file (cascades on delete).
--   node_id     — JSON Canvas node id, unique per file (NOT vault-wide;
--                 every API is handle/file scoped for exactly this
--                 reason).
--   kind        — announcement type word: text|file|image|link|group
--                 (image = file card with an image extension, t0 §1.1).
--   title       — backend-derived display title (t0 §1.1) — the one
--                 string outline/table/renderer/Voice Control share.
--   group_id    — containing group's node_id (NULL at canvas root).
--   group_path  — JSON array of ancestor group titles, root → parent
--                 (denormalized so outline rows are one query).
--   depth       — nesting depth (0 at root); the outline indent.
--   order_idx   — 0-based position in the canvas-wide reading order.
--   ordinal_n / total_m — 1-based position among siblings ("n of m in
--                 ⟨group‖canvas⟩", t0 §1.2).
--   conn_count / in_count / out_count — adjacency digests (dangling
--                 edges excluded; self-loops count both directions).
--   color       — raw JSON Canvas color ("1".."6" or hex), NULL if unset.
--   color_name  — pinned name (red/orange/yellow/green/cyan/purple/
--                 "custom color"), NULL if unset.
--   target      — file path (file/image), URL (link), '' otherwise;
--                 feeds the table view's Target column.
--   x / y / w / h — raw geometry (REAL; JSON Canvas allows any finite
--                 numbers, integers in practice).
--
-- canvas_edges columns mirror the JSON Canvas edge shape with spec
-- defaults materialized (from_end 'none', to_end 'arrow') so queries
-- never re-derive defaults.

CREATE TABLE canvas_nodes (
    file_id     INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    node_id     TEXT    NOT NULL,
    kind        TEXT    NOT NULL CHECK (kind IN ('text','file','image','link','group')),
    title       TEXT    NOT NULL,
    group_id    TEXT,
    group_path  TEXT    NOT NULL DEFAULT '[]',
    depth       INTEGER NOT NULL DEFAULT 0,
    order_idx   INTEGER NOT NULL,
    ordinal_n   INTEGER NOT NULL,
    total_m     INTEGER NOT NULL,
    conn_count  INTEGER NOT NULL DEFAULT 0,
    in_count    INTEGER NOT NULL DEFAULT 0,
    out_count   INTEGER NOT NULL DEFAULT 0,
    color       TEXT,
    color_name  TEXT,
    target      TEXT    NOT NULL DEFAULT '',
    x           REAL    NOT NULL,
    y           REAL    NOT NULL,
    w           REAL    NOT NULL,
    h           REAL    NOT NULL,
    PRIMARY KEY (file_id, node_id)
);

-- The outline/table read path: one range scan per canvas.
CREATE INDEX idx_canvas_nodes_order ON canvas_nodes(file_id, order_idx);

CREATE TABLE canvas_edges (
    file_id     INTEGER NOT NULL REFERENCES files(id) ON DELETE CASCADE,
    edge_id     TEXT    NOT NULL,
    from_id     TEXT    NOT NULL,
    to_id       TEXT    NOT NULL,
    from_side   TEXT,
    to_side     TEXT,
    from_end    TEXT    NOT NULL DEFAULT 'none'  CHECK (from_end IN ('none','arrow')),
    to_end      TEXT    NOT NULL DEFAULT 'arrow' CHECK (to_end IN ('none','arrow')),
    label       TEXT,
    color       TEXT,
    color_name  TEXT,
    PRIMARY KEY (file_id, edge_id)
);

CREATE INDEX idx_canvas_edges_from ON canvas_edges(file_id, from_id);
CREATE INDEX idx_canvas_edges_to   ON canvas_edges(file_id, to_id);
