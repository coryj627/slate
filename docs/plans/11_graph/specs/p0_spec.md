# P0 executable spec — Graph backend: GraphIndex, metrics, FFI surface, censuses

Issues: P0-1 ([#550](https://github.com/coryj627/slate/issues/550)) · P0-2 ([#551](https://github.com/coryj627/slate/issues/551)) · P0-3 ([#552](https://github.com/coryj627/slate/issues/552)) · P0-4 ([#553](https://github.com/coryj627/slate/issues/553)).
Milestone: [GH 16](https://github.com/coryj627/slate/milestone/16). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 2–4, 10; DoD §P-C/§P-D/§P-E). Backend norms apply: fmt/clippy pre-push, censuses for correctness invariants, host-independent slate-core (no macOS deps).

**Execution order: P0-1 → P0-2 → P0-3 → P0-4.** (P0-4's census harness may be developed alongside P0-1; it gates the wave.)

Baseline facts (verified 2026-07-04; **re-verified 2026-07-11** after Milestones M/O/T and the HIG audit landed — symbol anchors below are current as of main 9ea8d21, and symbol names outrank line numbers wherever they disagree):

- `links` table (migrations/004_links.sql): one row per link reference, PK `(source_file_id, ordinal)`; columns `source_file_id` (FK `files.id` ON DELETE CASCADE), `target_path` (NULL = unresolved internal OR external), `target_raw` (anchor already stripped for internal links by `links::extract_links`), `target_anchor`, `kind` ('wikilink'|'markdown'), `is_embed`, `is_external`, `snippet`, `span_start/end`, `display_text` (migration 015, graph-irrelevant). Indexes `idx_links_source`, `idx_links_target`. Rows are **bulk-written exclusively** by `links_db::replace_links_for_file` (links_db.rs:78) — but see the next bullet: two more mutation classes exist, and the links_db.rs module doc claiming "nothing else touches the table" is stale (fix it in the P0-1 PR).
- The complete set of links/files mutation points — and therefore the complete set of GraphIndex hook points (function-anchored; session.rs line numbers drift by thousands of lines per milestone):
  1. `replace_links_for_file` callers, all three inside named helpers — hook the **helpers**, which automatically covers every present and future caller: `index_saved_file` (save path — reached from `save_text_locked` [saves, property/task/frontmatter edits, link-rewrite apply, restores, canvas serialization] AND from `create_exclusive_binding`, the O-3 #792 create/recover path), `index_markdown_derivatives` (scan slow path via `index_file`, plus `refresh_text_derived_indexes_after_reclassification` when a rename flips markdown classification), `purge_markdown_derivatives` (oversized-file purge + reclassification purge).
  2. `re_resolve_unresolved_links` callers: `scan_vault` (post-walk) and `finish_structural_move` (tx1) — flips NULL `target_path`s to resolved.
  3. **Direct mutations with no `replace_links_for_file` call** (missed by the 2026-07-04 baseline): `finish_structural_move` tx1 bulk-repoints inbound edges (`UPDATE links SET target_path = ?1 WHERE target_path = ?2` per moved file, U2-3 #503), and FK CASCADE erases a deleted file's outgoing rows at `delete_file`, `delete_folder`, and the scan reconcile's `prune_unseen_files` (#641) — SQLite gives no per-row cascade signal, so removal hooks must fire at these Rust call sites.
  4. **Open-before-first-scan heals** (found during P0-4 census work): `open_canvas` and `ensure_open_base_indexed` insert a `files` row for a file opened before its filesystem event is indexed — the base heal rides `index_file` (hook class 1); the canvas heal inserts directly and needs its own FileAdded hook.
  The rename/move flow additionally rewrites link text via `link_rewrite::plan_rewrites` and re-saves each affected source through `save_text_locked` → `index_saved_file` — covered by hook class 1.
- **Dangling resolved targets are a real state** (reachable since U-era `delete_file`; the 2026-07-04 claim "targets always exist in `files` when resolved" is false): deleting file B leaves other files' rows with `target_path = 'B'` resolved-but-dangling — the cascade removes only B's own outgoing rows, nothing un-resolves inbound rows (no `SET target_path = NULL` exists anywhere), and unchanged sources fast-path on the next scan. Normative handling in P0-1 rule 1a below.
- `files` table (001_init.sql + later migrations): `id` INTEGER PK, `path` TEXT UNIQUE (vault-relative, `/`-separated), `is_markdown` 0|1, `mtime_ms`, `content_hash` (+ `ctime_ms`, `body_text`, `oplog_name` [027], `birthtime_ms` [030] — none graph-relevant; `birthtime_ms` is available if the UI ever wants a "created" sort).
- Existing query surface (session.rs, symbols current): `outgoing_links(path)` :3475, `backlinks(path, Paging)` :3763, `note_load_bundle(path, Paging)` :3784 (single-mutex-acquisition bundle), `list_unresolved_links(Paging)` :3805. Query types: `OutgoingLink` / `Backlink` / `UnresolvedLink` (links_db.rs:22-63).
- Session state: `VaultSession` holds `Mutex<Connection>`; mutations are single transactions; no vault-wide in-memory link structure exists today. Post-O precedents to mirror: `bases_generation: AtomicU64` bumped at index-changing seams (generation-counter shape), `remnant_logs: Mutex<(u64, Vec<…>)>` ((generation, data) under ONE mutex — the shape for P0-2's metrics cache), and the **#802 `VaultEventListener`** (`on_file_change` post-commit Created/Modified/Deleted/Renamed + `on_index_phase` ScanStarted…ScanFinished, session.rs:454; uniffi `with_foreign` export lib.rs:2076, registration `register_event_listener` lib.rs:525) — the push channel the 2026-07-04 spec assumed absent.
- `audio_summary` convention: pre-rendered VoiceOver strings on report types (`QueryResultSet.summary` search_db.rs:59-88; `SyncDetectionReport.audio_summary` per m_spec).
- uniffi mirroring convention: record + `From` impl (e.g. `Heading`, lib.rs:24-36) + `#[uniffi::export]` on the `VaultSession` wrapper (`#[derive(uniffi::Object)]` :290, export impl :295); callback trait precedents `ScanProgressListener` (`#[uniffi::export(with_foreign)]`, lib.rs:1975) and — newer, richer — `VaultEventListener` (lib.rs:2076); Swift bindings regenerate via `make regenerate-bindings` (`scripts/build-mac-app.sh:67-80`).
- Census convention: `census_*` test fns in `crates/slate-core/src/session/tests/*.rs`, registered via `#[path = "…"] mod …;` inside session.rs's `#[cfg(test)] mod tests` block (grants private `VaultSession` access); scale via `SLATE_CENSUS_FULL=1` / a **per-file local** `census_scale()` + per-file local SplitMix64 (link_integrity.rs:13-33 — the codebase deliberately duplicates these per test file); censuses run in release under the standing red-team protocol (full-scale runs are manual, recorded in BENCHMARKS.md + the PR; CI runs default scale via `cargo test --workspace`).
- Bench harness: `crates/slate-core/benches/scan_bench.rs` (criterion), synthetic vaults at 1k/10k/50k via `benches/common` (`generate_linked_vault(n)`, common/mod.rs:314 — hub topology, ~3 outlinks/file — is the graph-ready generator); baselines in `BENCHMARKS.md` as appended dated sections. **`make bench` runs scan_bench only** — record graph baselines via `cargo bench -p slate-core --bench graph_bench` (the N/O practice) and extend the Makefile target or note the direct invocation in the PR.
- Workspace deps (Cargo.toml): no petgraph today. Project license AGPL-3.0-or-later; petgraph is MIT/Apache-2.0 — compatible inbound.
- `unicode-normalization` is a direct dep (NFC tree sort keys #459, bases text folding) — but it has **no role in ghost keys**: the link resolver never normalizes (see P0-1 ghost-key rule).

---

## P0-1 · GraphIndex — in-memory adjacency mirror of the links table (#550) — PR 1

### Dependency

Add `petgraph = { version = "0.8", default-features = false, features = ["stable_graph", "std"] }` to `[workspace.dependencies]` with a rationale comment (stable indices across removal = incremental link graph; MIT/Apache-2.0), mirrored into slate-core as `{ workspace = true }`. No other new deps. **`"std"` is required explicitly**: it is a default feature at 0.8.x, so `default-features = false` without it silently switches petgraph to its no_std/hashbrown internals. Verified at 0.8.3 (latest): `algo::page_rank` is ungated and iteration-based (`damping_factor, nb_iter`) — the fixed-iteration rule in P0-2 maps directly — but its implementation iterates `0..node_count` via `from_index(i)`, which is **wrong or panicky on a `StableGraph` with removal holes** (`node_bound() > node_count()`); P0-2 therefore computes PageRank on a compact temporary graph, never on the live `StableDiGraph`. `parallel_page_rank` is rayon-gated and must stay unused (DoD §P-C).

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
    /// Normalized unresolved target: `target_raw` (already anchor-stripped by
    /// `links::extract_links`), trimmed, leading `./`/`/` stripped, then
    /// `str::to_lowercase()` — EXACTLY the resolver's comparison convention
    /// (link_resolver.rs `find_exact`/`collect_basename_matches`; full Unicode
    /// lowercase, NO NFC, no simple case-fold) so a ghost and the note that
    /// later materializes it collide on intent. An NFD-authored `[[Café]]`
    /// and NFC `Café.md` stay distinct here because the resolver keeps them
    /// distinct — the graph replays SQLite, it does not improve on it.
    Ghost(String),
}

pub enum NodeKind { Note, Attachment, Ghost }

pub struct NodeData {
    pub key: NodeKey,
    pub kind: NodeKind,
    /// Display label. Notes: file stem (markdown extension dropped).
    /// Attachments: final path component WITH extension (stems collide
    /// across extensions; matches Obsidian). Ghosts: the
    /// lexicographically-smallest *currently-authored* variant of
    /// `target_raw` (anchor-stripped, original case) among the ghost's
    /// live references — NOT "first-seen", which is unstable under both
    /// insertion-order permutation (census §P-C would fail on rebuild)
    /// and incremental maintenance (deep_equals would fail vs rebuild).
    /// Maintaining it incrementally requires a per-ghost variant
    /// refcount (BTreeMap<String, u32>) beside the node.
    pub label: String,
}

pub enum EdgeKind { Link, Embed }

/// One edge per (source, target, kind); parallel references collapse.
pub struct EdgeData { pub kind: EdgeKind, pub count: u32 }
```

### Construction & maintenance rules (normative)

1. **Full build** `GraphIndex::build(conn: &Connection) -> Result<GraphIndex, VaultError>`: one pass over `files` (every indexed file becomes a `Path` node; `kind` = Note iff `is_markdown`, else Attachment) then one pass over `links` **excluding `is_external = 1` rows**. Resolved rows add an edge to the target `Path` node. Unresolved rows add/reuse a `Ghost` node keyed per `NodeKey::Ghost` normalization. Edge collapsing: increment `count` on existing `(source, target, kind)`.
   1a. **Dangling resolved targets (normative):** a row whose `target_path` names no `files` row (real state — delete leaves inbound rows resolved-but-dangling; nothing un-resolves them until each source re-scans) maps to a `Ghost` keyed by the ghost-normalization of that row's **`target_raw`** — the same node the row would produce after its source re-scans and the resolver returns Unresolved. This keeps `build` ≡ `build`-after-any-subset-of-sources-rescans, and gives `apply_file_removed` its replay rule: query the victim's inbound rows (`source`, `target_raw`, `kind`, count) before the CASCADE, then re-point each in-edge to its per-row ghost. Fixture + census op: delete-then-build ≡ incremental delete.
2. **Incremental** `apply_linkset_change(&mut self, source_path, old: &[LinkRow], new: &[LinkRow])`, `apply_file_added(path, is_markdown)`, `apply_file_removed(path)`, `apply_file_renamed(old, new)`. Semantics: recompute the source's out-edge multiset from `new`, diff against current out-edges, apply. A ghost node is removed when its last in-edge goes; a `Path` node is **never** removed by link changes (only by `apply_file_removed`). When a file is added, any ghost whose key matches the new file's resolution intent is **merged**: in-edges re-point to the `Path` node (this mirrors `re_resolve_unresolved_links` — the session hook below drives it with the actual re-resolved rows, so the graph replays what SQLite did rather than re-implementing resolver policy).
3. **Session wiring:** `VaultSession` owns `Mutex<Option<GraphIndex>>`, built lazily on first graph query (`ensure_graph()`), then maintained at the hook points enumerated in the baseline facts (function-anchored): inside `index_saved_file` / `index_markdown_derivatives` / `purge_markdown_derivatives` (covers every `replace_links_for_file` caller — saves, creates/recovers, scan slow path, reclassification, oversize purge), the two `re_resolve_unresolved_links` callers (`scan_vault`, `finish_structural_move`), `finish_structural_move`'s bulk inbound repoint (drives `apply_file_renamed`), and the three removal sites `delete_file` / `delete_folder` / `prune_unseen_files` (drive `apply_file_removed` — the FK cascade emits no per-row signal). Each hook passes the same rows it just wrote to SQLite (or, for removals, the inbound rows read in-transaction before the cascade) — the graph never re-reads disk.
   3a. **Transaction discipline (normative):** graph mutations must never outlive a rolled-back transaction. Hooks stage a pending batch while the tx is open; the batch applies to the `GraphIndex` only after the enclosing commit succeeds, and on any failure the pending batch is dropped and the index resets to `None` (lazy rebuild repairs). Hooks mutate in-memory graph state ONLY — they MUST NOT call `notify_file_change` or any listener fan-out: the seam in `save_text_locked` stays the sole emission seat and dispatch stays strictly post-commit (#802 invariants — do not weaken; extend the events.rs seam test to assert exactly one Modified per save with the graph index built).
   If the index is `None` (never queried), hooks are no-ops: laziness means cold sessions pay zero cost.
4. **Determinism:** iteration order in build is `files.id` then `(source_file_id, ordinal)` — SQL `ORDER BY` explicit, never map order. `by_key` is storage only; anything enumerating nodes sorts by key.
5. **Generation:** bumps once per applied batch; exposed for cheap "did anything change" checks by the FFI layer and Swift refresh logic.

### Tests (PR 1)

- Unit: build on a fixture vault (resolved/unresolved/external/embed/parallel links, attachments) asserting exact node/edge sets; ghost merge on file-materialize; ghost removal on last-reference removal; rename keeps edges (via hooks replay).
- Property (proptest, links_roundtrip.rs pattern): random vault → build → node/edge counts match direct SQL aggregation; permutation of file insertion order yields identical sorted node/edge lists.
- The full mutation census lands in P0-4; PR 1 includes its `debug_assert`-free seam (`GraphIndex::deep_equals(&other)`).

## P0-2 · Graph metrics — degree, orphan, components, PageRank (#551) — PR 2

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
- PageRank: **hand-rolled sparse power iteration** (amended 2026-07-11 after reading petgraph 0.8.3's source: `algo::page_rank` is O(V²·E) *per iteration* — it rescans every candidate source's out-edges for every node pair — and its random-jump term is a nonstandard formulation; unusable at the 10k/50k budgets and not worth oracle-testing against). Canonical formulation: damping 0.85, **fixed 40 iterations**, f64, uniform teleport, **dangling mass redistributed uniformly each iteration** (every ghost is dangling — the leak would otherwise be structural), on the Link-kind subgraph (embeds excluded; one structural edge per collapsed pair, counts not weighted). Deterministic: accumulation in node-key order, no rayon. Ranks sum to 1 ± 1e-9 by construction. petgraph stays for `StableDiGraph` storage; nothing in metrics calls `petgraph::algo`.
- No rayon anywhere in metrics (float-order determinism, DoD §P-C).
- Degree wording, one phrase both here and in #551: **reference-distinct (sum of `EdgeData.count`)** — equals the links-table row count.
- **Relationship to Bases (shipped, don't re-litigate):** Milestone N's `file.inDegree`/`file.outDegree` are embed-inclusive, external-exclusive, reference-distinct links-table counts (test-pinned in `bases_engine.rs` `links_and_embeds_are_complete_and_partitioned`) — i.e. N's `inDegree` ≡ P0-2's `in_links + in_embeds`. The two surfaces stay deliberately different: the graph splits Link/Embed (orphan rule needs the split), Bases folds them. P1-2's table shows both columns so no number is hidden; `docs/help/bases.md` gets one clarifying line; a Bases adoption of `MetricsSnapshot` totals is N-E5 follow-up material, not P0-2 scope.

Tests: golden metrics on the P0-1 fixture; property: orphan ⇔ zero Link degree; component labels invariant under insertion-order permutation; PageRank sums to ~1.0 (1e-9) and is bit-identical across two builds of the same vault; **delete-then-measure** (metrics on an index with removal holes ≡ metrics on a fresh build — the `census_metrics_match_naive` case that would have caught the `page_rank`-on-holes hazard).

## P0-3 · uniffi graph surface (#552) — PR 3

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
- **No new callback interface in P0** — the decision stands, and is now trivially satisfied: the push channel already exists (#802's broadened `VaultEventListener`, shipped after this spec froze). Swift refresh contract (normative, supersedes the 2026-07-04 polling-at-known-moments wording): AppState subscribes graph refresh to the existing `VaultEventListener` — `on_file_change` for ALL `FileChangeKind` values (created materializes ghosts, modified changes linksets, deleted/renamed changes nodes/edges) plus `on_index_phase` filtered to `ScanFinished` (external edits never emit file-change events; the watcher is a stub). Handler discipline per the listener's documented contract: marshal to the main actor first (never call the session synchronously from the callback — it may arrive with session locks held), debounce ~100–250 ms (folder ops and undo emit one event per touched file), then re-query `graph_generation()` off-main and refresh surfaces only on change — the generation stays the cheap discriminator that prevents repaints on saves that didn't change the graph. Register a dedicated graph listener token via `register_event_listener` (multiple listeners are supported) rather than contending for the existing `VaultEventAdapter` hooks, and update the stale "Milestone PD wires the first real consumers" comment in `AppState+History.swift` when P becomes the first consumer.

Tests: FFI-shape unit tests on fixture vault (filter combinations, neighborhood depth clamp, summary strings verbatim); Swift side gets binding smoke tests with the regenerated bindings.

## P0-4 · Censuses + benchmarks — the wave gate (#553) — PR 4

**Censuses** (`crates/slate-core/src/session/tests/graph.rs`, `census_*` convention, `census_scale()` scaling):

1. `census_graph_matches_rebuild` — adversarial random walk: N ops drawn from {create note w/ random links (incl. to nonexistent targets, embeds, parallel repeats, attachments), edit links, rename, move, delete, create-materializing-a-ghost}; after **every** op, incremental `GraphIndex` `deep_equals` a fresh `GraphIndex::build` from SQLite. Random + an exhaustive small-vault sweep (every op pair over a 4-file vault) per the adversarial-census methodology.
2. `census_graph_permutation_invariance` — same file set inserted in shuffled orders ⇒ identical sorted node/edge/metric lists.
3. `census_metrics_match_naive` — degree/orphan/component from `MetricsSnapshot` ≡ naive per-node SQL recomputation.

**Benchmarks** (`crates/slate-core/benches/graph_bench.rs`, declaring `mod common;` and reusing `common::generate_linked_vault`): `graph_build/{1k,10k,50k}`, `graph_snapshot_default_filter/{10k}`, `graph_neighborhood_d2/{10k}`, `metrics_full/{10k,50k}`, `linkset_change_incremental/{10k}` (one file's links replaced; budget: O(changed-file), < 1 ms at 10k). Run/record via `cargo bench -p slate-core --bench graph_bench` (`make bench` is hard-wired to scan_bench — extend the Makefile target or note the direct invocation in the PR); append a dated `## Milestone P — …` section to `BENCHMARKS.md` following the O-milestone entries; `scan_initial` and save-path benches re-run to prove the hooks are free when the index is unbuilt and O(edit) when built (DoD §P-E).

Wave-1 exit: all three censuses clean (incl. one `SLATE_CENSUS_FULL=1` release run), baselines recorded, no scan/save regression.
