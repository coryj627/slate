# T1 executable spec — Backend core (Wave 1)

Issues: #359 (parser) · #360 (model) · #517 (placement) · #361 (schema/FFI) · #366 (serializer).
Milestone: GH 20. Every issue also satisfies the 08 program DoD §A–§G and the 09 deltas §H–§L; this spec adds what is Wave-1-specific. One PR per issue. All Rust; no UI.

**Execution order: #359 → #360 → (#517 ∥ #361) → #366.** (#361's `canvas_apply` write surface may land as a second PR after #366, since it serializes through it — but its API shape below is locked now because Wave 4 builds against it.)

---

## Shared architecture (read first)

```
crates/slate-core/src/canvas/mod.rs        (#359)  parse: &str -> (Canvas, Vec<CanvasWarning>)
crates/slate-core/src/canvas/model.rs      (#360)  derive: &Canvas -> CanvasModel
crates/slate-core/src/canvas/placement.rs  (#517)  place_new(...) -> Placement
crates/slate-core/src/canvas/serialize.rs  (#366)  serialize: &Canvas -> String
crates/slate-core/migrations/NNN_canvas.sql  (#361, repo 3-digit convention)
crates/slate-uniffi/src/lib.rs             (#361)  1:1 mirrors, From<core::X>, no logic
```

```rust
// #359 — raw, spec-faithful, lossless
pub struct Canvas {
    pub nodes: Vec<Node>, pub edges: Vec<Edge>, pub unknown: RawExtra,
    // Malformed/unrecognized entries are SKIPPED from nodes/edges but RETAINED here
    // verbatim (raw JSON + document position) so #366 re-emits them in place.
    // A save must never delete what the parser couldn't model (t0 §5's
    // "preserved in the file but not shown" is this field).
    pub skipped: Vec<SkippedEntry>,   // { position: usize, raw: RawValue, warning: CanvasWarning }
}
pub enum NodeKind { Text { text: String },
                    File { file: String, subpath: Option<String> },
                    Link { url: String },
                    Group { label: Option<String>, background: Option<Background> } }
pub struct Node { pub id: NodeId, pub kind: NodeKind,
                  pub x: f64, pub y: f64, pub width: f64, pub height: f64,
                  pub color: Option<CanvasColor>,   // Preset(1..=6) | Hex(String)
                  pub unknown: RawExtra }           // unrecognized keys, retained verbatim
pub struct Edge { pub id: EdgeId, pub from: (NodeId, Option<Side>), pub to: (NodeId, Option<Side>),
                  pub from_end: EndStyle, pub to_end: EndStyle,   // none | arrow (defaults per spec)
                  pub label: Option<String>, pub color: Option<CanvasColor>, pub unknown: RawExtra }

// #360 — derived, deterministic, what every surface reads
pub struct CanvasModel {
    pub tree: GroupTree,              // groups -> child cards/sub-groups (containment resolved)
    pub reading_order: Vec<NodeId>,   // total order (see rules below)
    pub adjacency: AdjacencyMap,      // per node: Vec<Neighbor{edge, direction, side, label}>
    pub summaries: HashMap<NodeId, CardSummary>, // type, display_title, group_path, in/out counts, color name
    pub spatial: SpatialIndex,        // overlap / nearest-slot queries (feeds #517, #521)
}
```

### Reading-order & containment rules (#360 — normative, census-gated)

1. **Containment is by node center point** inside the group rect; ties (center on boundary) resolve to *not contained*. A card whose center is inside multiple groups belongs to the **smallest-area** containing group; equal areas → the later group in document order. Nested groups form the tree by the same rule applied group-to-group.
2. **Reading order**: depth-first over the group tree; within a container, cards sort by `(y, x, document order)` — document order is the **final total-order tiebreak**, so identical coordinates are still deterministic.
3. Zero-size, negative-coordinate, and coincident nodes are all legal inputs (they occur in real Obsidian vaults) and must produce a valid order.
4. Determinism contract: `derive(parse(s))` is a pure function of `s` — equal files give equal orders across runs/reloads.

**Census (release-mode, per the adversarial-census methodology):** random canvases (sizes 0–2,000; random overlaps, nestings, degenerate geometry) × exhaustive small cases (all containment topologies ≤ 4 nodes) assert: every node appears in `reading_order` exactly once; tree parenting matches rule 1; order is stable under re-parse; adjacency is symmetric with edge direction preserved.

---

## #359 — Parser

As issued, plus: `RawExtra` preserves unknown keys **and their order** where serde allows, so #366 can round-trip byte-stably in practice; a malformed node/edge yields `CanvasWarning { index, reason }`, is skipped from `nodes`/`edges`, and is **retained in `Canvas.skipped`** (see struct above) — the file never hard-fails (frontmatter-parser contract) and never loses data on save. Edge referencing a missing node parses but is flagged (`DanglingEdge`) for t0 §5 surfacing.
**Color names pinned here** (backend-owned, t0 §1.1): presets 1–6 = *red, orange, yellow, green, cyan, purple* (JSON Canvas order); `Hex` values phrase as *"custom color"* (verbose level) — #370 later verifies contrast and may refine hex→nearest-preset naming, but Wave 1 ships these strings.
**Fixtures:** committed here in `crates/slate-core/tests/fixtures/canvas/`, including the **2,000-node fixture** (checked-in generator script) that t1 benchmarks and #365 later reuse — plus a malformed-entry fixture that must round-trip with the malformed entry intact.
**Tests:** fixture per node kind, labelled/unlabelled/directed/undirected edges, nested groups, unknown fields at node/edge/root level, malformed entries (parse → serialize retains them), empty file, `{}` file.

## #360 — Model

As issued + the normative rules above. `CardSummary.display_title` implements the t0 §1.1 derivation (the *backend* owns title derivation so all surfaces agree; frontmatter lookups go through the existing note-index APIs). `AdjacencyMap` exposes direction phrases' raw data (from/to + end styles) — phrasing itself is UI (#518).
**Tests:** census above; multi-edge between the same pair; self-edges; dangling edges excluded from adjacency but listed in warnings.

## #517 — Placement engine

As issued (see the issue body for the full contract). Key spec points: preference order below→right→above→left from the anchor, first non-overlapping grid-aligned slot at the default gap; occupied ring → expand ring. Exports `GRID_STEP`, `GRID_STEP_LARGE`, `DEFAULT_CARD_SIZE`, `DEFAULT_GAP` (single source of truth for #521/#366; numeric values are the implementing dev's choice, exported once). Returns `Placement { x, y, relative: RelativeDesc }` — `RelativeDesc` is a **typed enum** `{ Below(anchor), RightOf(anchor), Above(anchor), LeftOf(anchor), AtOrigin }` (anchor = display title); phrasing/localization stays UI-side (#518).
**Rigid sets:** `place_set(anchor, boxes: Vec<Rect>) -> Vec<Point>` places a marked set / duplicated set as one unit — pairwise offsets preserved, the set's bounding box placed by the same slot search (#522/#524/#525 consume; never UI math).
**Tests:** non-overlap census (random models × anchors × exhaustive hints); determinism; empty-canvas origin; dense-ring fallback; place_set offset preservation.

## #361 — Schema + VaultSession + uniffi (read **and write** surface)

As issued, with the open decision **closed: derived columns, not a model blob**. Schema: `canvas_files(path, hash, parsed_at)`, `canvas_nodes(file, id, kind, title, group_id, order_idx, color, x, y, w, h)`, `canvas_edges(file, id, from_id, to_id, from_side, to_side, from_end, to_end, label, color)` — derived columns (`title`, `group_id`, `order_idx`) come from #360 so `canvas_table_rows`/`canvas_outline` are single queries. Append-only migration (follow the repo's `NNN_` naming); refuses newer schema; index regenerable from `.canvas` source of truth. Reindex on scan + file-watcher change like other content types. **The scan and quick-open filters extend to include `.canvas`** (today `session.rs` is hard-filtered to `.md` — that change is backend work owned here, consumed by #369).

**Handle-based API** (node IDs are unique per file, not per vault): `open_canvas(path) -> CanvasHandle`; all other calls take the handle.

*Read:* `canvas_outline(h) -> Vec<OutlineRow>` (depth-first flattening: `{ node_id, depth, kind, title, group_path, ordinal_n, total_m, connection_count, color_name }`), `canvas_table_rows(h) -> Vec<TableRow>` (`{ node_id, kind, title, group_path, target, connection_count, color_name }`; `target` = file path / URL host / empty per kind), `canvas_neighbors(h, node) -> Vec<Neighbor>` (`{ edge_id, other_node, direction, side, label }`), `canvas_where_am_i(h, node) -> WhereAmI` (`{ title, kind, group_path, ordinal_n, total_m, in_count, out_count, color_name }` — mark state is UI-owned and merged UI-side), `canvas_place_new` / `canvas_place_set` (#517), `canvas_check_overlap(h, rect, exclude: Vec<NodeId>) -> Vec<NodeId>` (mode-transient overlap warnings, #521).

*Write (the Wave-4 mutation surface — owned here, consumed by #368/#521–#525):* `canvas_apply(h, action: CanvasAction) -> ApplyResult`. `CanvasAction` = `{ name: String /* op-log + undo label */, ops: Vec<CanvasOp> }`; `CanvasOp` = `CreateNode | UpdateNodeGeometry | SetNodeColor | SetNodeContent | DeleteNode | AddEdge | UpdateEdge | DeleteEdge | CreateGroup | RenameGroup | Ungroup`. One committed user action = **one** `canvas_apply` (bulk ops batch into one action) = one serialize+atomic write (#366) + one journal entry (#372); `ApplyResult` returns the reindexed deltas or a typed conflict error (t0 §5). The engine computes and returns the **inverse action** for the undo stack (#372).

**Tests:** migration forward on fresh/existing DB; round-trip open→API parity with #360 unit expectations; uniffi type-mirror parity test; watcher reindex; `.canvas` in scan/quick-open results; every `CanvasOp` applies + inverts to the prior state (apply→invert→byte-equal serialize); overlap query correctness.

## #366 — Serializer

As issued: spec-compliant emit, unknown fields intact, node/edge order preserved, spatial precision preserved (no float drift on untouched nodes — serialize the retained raw values for unmodified fields), atomic temp+rename via `VaultProvider`, content-hash conflict detection mirroring note saves.
Additional scope (program gate): **`.canvas` participation in link-integrity rewriting** — when U2-3 rewrites links on note move/rename, `.canvas` `file` node paths (and `subpath` anchors on heading rename, if U2-3 covers those for markdown) rewrite through this serializer. If this slips out of Wave 1 it becomes a tracked follow-up issue, never silent.
**Tests:** parse→serialize→parse structural equality incl. unknown fields on every fixture; byte-stability for untouched files; atomic-write + conflict tests; a note-move fixture whose canvas reference is rewritten.

---

## Acceptance (wave close)

`cargo test` + clippy clean; censuses clean in release mode; every fixture in `crates/slate-core/tests/fixtures/canvas/` (committed with #359, extended per issue) round-trips; Swift can drive the full read API over FFI (#361 integration test). Benchmarks: parse+derive for the 2,000-node fixture recorded in `BENCHMARKS.md` (no quadratic derivation — §K).
