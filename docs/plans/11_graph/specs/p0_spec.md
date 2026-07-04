# P0 executable spec — Graph backend: GraphIndex, metrics, FFI surface, censuses

Issues: P0-1 (#TBD) · P0-2 (#TBD) · P0-3 (#TBD) · P0-4 (#TBD).
Milestone: [GH 16](https://github.com/coryj627/slate/milestone/16). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 2–4, 10; DoD §P-C/§P-D/§P-E). Backend norms apply: fmt/clippy pre-push, censuses for correctness invariants, host-independent slate-core (no macOS deps).

**Execution order: P0-1 → P0-2 → P0-3 → P0-4.** (P0-4's census harness may be developed alongside P0-1; it gates the wave.)

Baseline facts (verified 2026-07-04, this worktree):

- `links` table (migrations/004_links.sql): one row per link reference, PK `(source_file_id, ordinal)`; columns `source_file_id` (FK `files.id` ON DELETE CASCADE), `target_path` (NULL = unresolved internal OR external), `target_raw`, `target_anchor`, `kind` ('wikilink'|'markdown'), `is_embed`, `is_external`, `snippet`, `span_start/end`. Indexes `idx_links_source`, `idx_links_target`. Rows are written **exclusively** by `links_db::replace_links_for_file` (links_db.rs:78).
- `replace_links_for_file` call sites — these are the complete set of link-mutation points and therefore the complete set of GraphIndex hook points: scan slow path (session.rs:940), save path (session.rs:3086), file-delete/empty path (session.rs:3148). Additionally `re_resolve_unresolved_links` (session.rs:2672, :4193) flips NULL `target_path`s to resolved after create/rename, and the rename/move flow rewrites link targets via `link_rewrite::plan_rewrites` then re-scans affected files.
- `files` table (001_init.sql): `id` INTEGER PK, `path` TEXT UNIQUE (vault-relative, `/`-separated), `is_markdown` 0|1, `mtime_ms`, `content_hash`.
- Existing query surface (session.rs): `backlinks(path, Paging)` :1416, `outgoing_links(path)` :1119, `note_load_bundle(path, Paging)` :1437 (single-mutex-acquisition bundle), `list_unresolved_links(Paging)` :1459. Query types: `OutgoingLink` / `Backlink` / `UnresolvedLink` (links_db.rs:22-66).
- Session state: `VaultSession` holds `Mutex<Connection>`; mutations are single transactions; no vault-wide in-memory link structure exists today (every backlink query is an indexed table scan).
- `audio_summary` convention: pre-rendered VoiceOver strings on report types (`QueryResultSet.summary` search_db.rs:59-88; `SyncDetectionReport.audio_summary` per m_spec).
- uniffi mirroring convention: record + `From` impl + `#[uniffi::export]` on the `VaultSession` wrapper (slate-uniffi/src/lib.rs:240-259); callback trait precedent `ScanProgressListener` (`#[uniffi::export(with_foreign)]`, lib.rs:1579); Swift bindings regenerate via `scripts/build-mac-app.sh:61-75`.
- Census convention: `census_*` test fns in `crates/slate-core/src/session/tests/*.rs`, scale via `SLATE_CENSUS_FULL=1` / `census_scale()` (session/tests/link_integrity.rs:10-19); censuses run in release under the standing red-team protocol.
- Bench harness: `crates/slate-core/benches/scan_bench.rs` (criterion), synthetic vaults at 1k/10k/50k via `benches/common`; `make bench`; baselines in `BENCHMARKS.md`.
- Workspace deps (Cargo.toml): no petgraph today. Project license AGPL-3.0-or-later; petgraph is MIT/Apache-2.0 — compatible inbound.
- `unicode-normalization` is already a direct dep (NFC sort keys, #459) — reuse for ghost-key folding.

---

## P0-1 · GraphIndex — in-memory adjacency mirror of the links table (#TBD) — PR 1

### Dependency

Add `petgraph = { version = "0.8", default-features = false, features = ["stable_graph"] }` to `[workspace.dependencies]` with a rationale comment (stable indices across removal = incremental link graph; MIT/Apache-2.0), mirrored into slate-core. No other new deps. If `algo::page_rank` requires an additional feature flag at the pinned version, enable exactly that flag (verify against docs.rs at implementation time).

### Rust: new module `crates/slate-core/src/graph.rs`

```rust
pub struct GraphIndex {
    graph: petgraph::stable_graph::StableDiGraph<NodeData, EdgeData>,
    by_key: HashMap<NodeKey, NodeIndex>,
    generation: u64,          // bumps on every applied mutation batch
}

/// Node identity. Path-keyed for real files; ghost-keyed for unresolved targets.
#[derive(Clone, PartialEq, Eq, Hash)]
pub enum NodeKey {
    /// Vault-relative path of an indexed file (notes AND attachments).
    Path(String),
    /// Normalized unresolved target: NFC + Unicode simple case-fold of
    /// `target_raw` with any `#heading`/`^block` suffix stripped. Matches the
    /// resolver's case-insensitive lookup convention so a ghost and the note
    /// that later materializes it collide on intent.
    Ghost(String),
}

pub enum NodeKind { Note, Attachment, Ghost }

pub struct NodeData {
    pub key: NodeKey,
    pub kind: NodeKind,
    /// Display label: file stem for Path nodes; the *first-seen authored*
    /// `target_raw` (anchor-stripped, original case) for ghosts.
    pub label: String,
}

pub enum EdgeKind { Link, Embed }

/// One edge per (source, target, kind); parallel references collapse.
pub struct EdgeData { pub kind: EdgeKind, pub count: u32 }
```

### Construction & maintenance rules (normative)

1. **Full build** `GraphIndex::build(conn: &Connection) -> Result<GraphIndex, VaultError>`: one pass over `files` (every indexed file becomes a `Path` node; `kind` = Note iff `is_markdown`, else Attachment) then one pass over `links` **excluding `is_external = 1` rows**. Resolved rows add an edge to the target `Path` node (creating an Attachment node if the target isn't a markdown file already seen — targets always exist in `files` when resolved). Unresolved rows add/reuse a `Ghost` node keyed per `NodeKey::Ghost` normalization. Edge collapsing: increment `count` on existing `(source, target, kind)`.
2. **Incremental** `apply_linkset_change(&mut self, source_path, old: &[LinkRow], new: &[LinkRow])`, `apply_file_added(path, is_markdown)`, `apply_file_removed(path)`, `apply_file_renamed(old, new)`. Semantics: recompute the source's out-edge multiset from `new`, diff against current out-edges, apply. A ghost node is removed when its last in-edge goes; a `Path` node is **never** removed by link changes (only by `apply_file_removed`). When a file is added, any ghost whose key matches the new file's resolution intent is **merged**: in-edges re-point to the `Path` node (this mirrors `re_resolve_unresolved_links` — the session hook below drives it with the actual re-resolved rows, so the graph replays what SQLite did rather than re-implementing resolver policy).
3. **Session wiring:** `VaultSession` owns `Mutex<Option<GraphIndex>>`, built lazily on first graph query (`ensure_graph()`), then maintained at the `replace_links_for_file` call sites (session.rs:940, :3086, :3148), the re-resolve sites (:2672, :4193), and file add/remove/rename bookkeeping. Each hook site passes the same rows it just wrote to SQLite — the graph never re-reads disk. If the index is `None` (never queried), hooks are no-ops: laziness means cold sessions pay zero cost.
4. **Determinism:** iteration order in build is `files.id` then `(source_file_id, ordinal)` — SQL `ORDER BY` explicit, never map order. `by_key` is storage only; anything enumerating nodes sorts by key.
5. **Generation:** bumps once per applied batch; exposed for cheap "did anything change" checks by the FFI layer and Swift refresh logic.

### Tests (PR 1)

- Unit: build on a fixture vault (resolved/unresolved/external/embed/parallel links, attachments) asserting exact node/edge sets; ghost merge on file-materialize; ghost removal on last-reference removal; rename keeps edges (via hooks replay).
- Property (proptest, links_roundtrip.rs pattern): random vault → build → node/edge counts match direct SQL aggregation; permutation of file insertion order yields identical sorted node/edge lists.
- The full mutation census lands in P0-4; PR 1 includes its `debug_assert`-free seam (`GraphIndex::deep_equals(&other)`).

## P0-2 · Graph metrics — degree, orphan, components, PageRank (#TBD) — PR 2

New `crates/slate-core/src/graph_metrics.rs`, computed on demand over a `&GraphIndex`, cached per generation (`Mutex<Option<(u64, MetricsSnapshot)>>` beside the index):

```rust
pub struct NodeMetrics {
    pub in_links: u32, pub out_links: u32,      // EdgeKind::Link, count-weighted? NO — reference-distinct: sum of count
    pub in_embeds: u32, pub out_embeds: u32,
    pub component: u32,                          // see labeling rule
    pub is_orphan: bool,                         // Note nodes with zero Link-kind edges either direction (embeds don't rescue; matches Obsidian's orphan filter intent)
    pub pagerank: f64,
}
pub struct MetricsSnapshot { /* per-NodeIndex dense storage + vault totals */
    pub note_count: u32, pub attachment_count: u32, pub ghost_count: u32,
    pub edge_count: u64, pub orphan_count: u32, pub component_count: u32,
}
```

Rules (normative):
- Degrees sum `EdgeData.count` (a note linking the same target 3× reads "3 links out to it" in totals — matches the links-table row count and the backlinks panel).
- Components: undirected connected components over Note+Attachment+Ghost; **component id = index of the component when components are ordered by their lexicographically-smallest member key** — stable across permutation, census-checkable.
- PageRank: `petgraph::algo::page_rank`, damping 0.85, **fixed 40 iterations**, f64, on the Link-kind subgraph (embeds excluded). If the pinned petgraph's signature differs (tolerance-based), wrap it with the fixed-iteration form ourselves — determinism per DoD §P-C outranks crate convenience.
- No rayon anywhere in metrics (float-order determinism, DoD §P-C).

Tests: golden metrics on the P0-1 fixture; property: orphan ⇔ zero Link degree; component labels invariant under insertion-order permutation; PageRank sums to ~1.0 (1e-9) and is bit-identical across two builds of the same vault.

## P0-3 · uniffi graph surface (#TBD) — PR 3

Records (slate-uniffi/src/lib.rs, mirror + `From` per convention):

```rust
pub struct GraphNode {   // uniffi::Record
    pub id: u64,                    // StableGraph index; stable within a session, NOT across sessions
    pub path: Option<String>,       // None for ghosts
    pub label: String,
    pub kind: GraphNodeKind,        // Note | Attachment | Ghost
    pub in_links: u32, pub out_links: u32, pub in_embeds: u32, pub out_embeds: u32,
    pub component: u32, pub is_orphan: bool, pub pagerank: f64,
    pub modified_ms: Option<i64>,   // files.mtime_ms; None for ghosts
}
pub struct GraphEdge { pub source_id: u64, pub target_id: u64, pub kind: GraphEdgeKind, pub count: u32 }
pub struct GraphFilter { pub include_attachments: bool, pub include_ghosts: bool, pub orphans_only: bool }
pub struct GraphSnapshot {
    pub nodes: Vec<GraphNode>, pub edges: Vec<GraphEdge>,
    pub generation: u64,
    /// "247 notes, 1,032 links. 12 orphans, 3 unresolved targets." — exact format normative below.
    pub audio_summary: String,
}
pub struct GraphNeighborhood { pub center_id: u64, pub depth: u32, pub nodes: Vec<GraphNode>, pub edges: Vec<GraphEdge>, pub audio_summary: String }
```

`VaultSession` methods (all sync, caller dispatches off-main per AppState pattern):

```rust
fn graph_snapshot(&self, filter: GraphFilter) -> Result<GraphSnapshot, VaultError>
fn graph_neighborhood(&self, path: String, depth: u32 /* clamped 1..=3 */, filter: GraphFilter) -> Result<GraphNeighborhood, VaultError>
fn graph_generation(&self) -> u64      // cheap; 0 when index not yet built
```

Rules:
- Filtering composes: excluded-kind nodes drop with their incident edges; `orphans_only` keeps orphan Notes only (then kind filters apply). Deterministic output order: nodes sorted by key, edges by (source, target, kind).
- `graph_neighborhood` = BFS over the **undirected** view from `path`'s node, depth-limited, filter applied before traversal (a ghost-excluded run never walks through ghosts). Unknown path → `VaultError::InvalidPath`.
- `audio_summary` formats (normative, `%n` = grouped decimal): snapshot: `"{n} notes, {e} links. {o} orphans, {g} unresolved targets."` — omit the second sentence when both are 0; append `" Filtered."` when any filter deviates from `{attachments:false, ghosts:true, orphans_only:false}` defaults. Neighborhood: `"{label}: {in} links in, {out} links out. Showing {k} notes within {d} links."`
- **No new callback interface in P0.** Swift refresh contract: AppState already knows every mutation it initiates and observes scan completion; it re-queries `graph_generation()` at those moments and refreshes graph surfaces on change. (A push listener is a P3 upgrade if polling-at-known-moments proves insufficient — record in the PR if evidence appears.)

Tests: FFI-shape unit tests on fixture vault (filter combinations, neighborhood depth clamp, summary strings verbatim); Swift side gets binding smoke tests with the regenerated bindings.

## P0-4 · Censuses + benchmarks — the wave gate (#TBD) — PR 4

**Censuses** (`crates/slate-core/src/session/tests/graph.rs`, `census_*` convention, `census_scale()` scaling):

1. `census_graph_matches_rebuild` — adversarial random walk: N ops drawn from {create note w/ random links (incl. to nonexistent targets, embeds, parallel repeats, attachments), edit links, rename, move, delete, create-materializing-a-ghost}; after **every** op, incremental `GraphIndex` `deep_equals` a fresh `GraphIndex::build` from SQLite. Random + an exhaustive small-vault sweep (every op pair over a 4-file vault) per the adversarial-census methodology.
2. `census_graph_permutation_invariance` — same file set inserted in shuffled orders ⇒ identical sorted node/edge/metric lists.
3. `census_metrics_match_naive` — degree/orphan/component from `MetricsSnapshot` ≡ naive per-node SQL recomputation.

**Benchmarks** (`crates/slate-core/benches/graph_bench.rs`, reusing `benches/common` vault generator): `graph_build/{1k,10k,50k}`, `graph_snapshot_default_filter/{10k}`, `graph_neighborhood_d2/{10k}`, `metrics_full/{10k,50k}`, `linkset_change_incremental/{10k}` (one file's links replaced; budget: O(changed-file), < 1 ms at 10k). Record baselines in `BENCHMARKS.md`; `scan_initial` and save-path benches re-run to prove the hooks are free when the index is unbuilt and O(edit) when built (DoD §P-E).

Wave-1 exit: all three censuses clean (incl. one `SLATE_CENSUS_FULL=1` release run), baselines recorded, no scan/save regression.
