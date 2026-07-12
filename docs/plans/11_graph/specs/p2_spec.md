# P2 executable spec — Visual projection: layout kernel, LayoutSession FFI, Diagram mode

Issues: P2-1 ([#557](https://github.com/coryj627/slate/issues/557)) · P2-2 ([#558](https://github.com/coryj627/slate/issues/558)) · P2-3 ([#559](https://github.com/coryj627/slate/issues/559)) · P2-4 ([#560](https://github.com/coryj627/slate/issues/560)) · P2-5 ([#561](https://github.com/coryj627/slate/issues/561)) · P-D docs ([#562](https://github.com/coryj627/slate/issues/562)).
Milestone: [GH 16](https://github.com/coryj627/slate/milestone/16). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 4–6, 8–10; DoD §P-B/§P-C/§P-E). Gate: **P2-1 may start alongside Wave 2 (pure Rust); P2-2+ gate on P1 complete (DoD §P-A).**

**Execution order: P2-1 → P2-2 → P2-3 → (P2-4 ∥ P2-5) → P-D.**

Baseline facts (verified 2026-07-04; **re-verified 2026-07-11** — main 9ea8d21; P0/P1 facts assumed from their specs):

- Rendering decision locked: `NSView` + `CALayer` or Metal for graph; SwiftUI Canvas ruled out (05_locked_architecture_decisions.md:492).
- Per-element AX on a visual surface is **landed code**, not just plans: `CanvasCardAXElement: NSAccessibilityElement` at `Canvas/CanvasRendererView.swift:103` (T #367 — per-card AX elements with screen-coordinate frames tracking pan/zoom, VO-focus sync). Note canvas tiers by *viewport windowing*, not node count — P2-3's 1,500-node tier split is P's own normative addition on top of the per-element pattern.
- **Zoom-chord ownership (supersedes the 2026-07-04 "scope disjointness" wording):** the codebase's mechanism for two logical owners of one chord is a **single focus-routed menu owner** (the Undo/Redo `undoTargetsCanvas` pattern — a second parallel `.keyboardShortcut` declaration is explicitly avoided as undefined in SwiftUI). Canvas today claims ⌘= / ⌘- / ⌘0 via canvas-scoped menu items and ⌃⌘I via an action-side scope guard. In-flight HIG-audit #848 (branch `claude/chords-and-zoom`) converts ⌘=/⌘−/⌘0 into unified focus-routed items (canvas tab → canvas viewport; else → editor text zoom). **P2-3 joins the router** — a graph-tab case ahead of the editor fallback — rather than registering separately-scoped chords, and the test to write is a **routing-priority test** (canvas tab → canvas, graph tab → graph, else editor) plus a per-chord single-menu-owner assertion; no "scope disjointness" test exists today and Set<String>-based drift machinery cannot express one. Sequence: if #848 lands first, extend its router; if P2-3 goes first, P2-3 builds the router and #848 plugs in after. ⌥⌘0 "fit graph" is verified unclaimed; register it per T rule R3 (add to the inventory + drift-tested help table — and refresh the stale claimed-inventory line at 09_canvas/00_program.md:67 in the same PR, it predates ⌘N and several shipped chords).
- No RNG crates in the workspace today (no `rand`, no `rand_chacha`); SplitMix64 copies exist but all under `#[cfg(test)]` — graph_layout's jitter derivation will be the **first production copy**. Initial placement below is RNG-free by design; derive any jitter from a splitmix64 hash of the node key — **do not** add `rand` for this.
- `CancelToken` = `Arc<AtomicBool>`, `cancel()`/`is_cancelled()` (session.rs:396-413), taken by long ops (`scan_initial` :1350).
- uniffi interface-object precedent: `VaultSession`, `CancelToken` are `#[uniffi::Object]`s; records serialize per call (flat buffers cheap, vec-of-records hot paths not) — research brief §7.
- Atomic write convention: temp + rename, never in place (U DoD §F). `.slate/` vault-local config precedent hardened by O-5: `history_prefs.rs` is the Rust-side pattern — unknown-top-level-key preservation via read-merge-write, **refuse-to-clobber-unparseable**, unique per-process temp names, and a sidecar `.lock` flock when two writers share a file (atomic rename prevents torn JSON but NOT lost updates). These are shipped O invariants — don't weaken.

---

## P2-1 · Deterministic force-layout kernel (#557) — PR 1 (pure Rust, host-independent)

New `crates/slate-core/src/graph_layout.rs`. No new dependencies.

```rust
/// User-tunable forces, matching the Obsidian-parity sliders 1:1 (all 0.0..=1.0,
/// defaults 0.5; mapping to physical constants below is normative).
pub struct LayoutForces { pub center: f32, pub repel: f32, pub link: f32, pub link_distance: f32 }

pub struct LayoutConfig {
    pub seed: u64,                 // reserved for jitter derivation; same seed ⇒ same layout
    pub max_iterations: u32,       // cold solve budget; default 300
    pub warm_iterations: u32,      // warm-start budget; default 60
}

pub struct LayoutEngine { /* positions: Vec<[f64; 2]>, velocities, pinned: bitset,
                             awake: bitset, temperature: f64, iteration: u32 ... */ }

impl LayoutEngine {
    pub fn new(graph: &GraphIndex, filter: &GraphFilter, forces: LayoutForces, config: LayoutConfig) -> Self;
    pub fn step(&mut self, iterations: u32) -> StepReport;   // StepReport { iteration, max_displacement, converged }
    pub fn positions(&self) -> &[[f64; 2]];                  // node order = filtered nodes sorted by key (the SAME order P0-3 snapshots emit)
    pub fn set_forces(&mut self, f: LayoutForces);           // re-heats temperature to warm level
    pub fn pin(&mut self, node: usize, x: f64, y: f64);
    pub fn unpin(&mut self, node: usize);
    /// Graph changed: carry over positions of surviving nodes by key, seat new
    /// nodes at their neighbors' centroid (or golden-angle ring if isolated),
    /// wake only the ≤2-hop neighborhood of changes, pin-freeze the rest,
    /// re-heat to warm temperature.
    pub fn warm_update(&mut self, graph: &GraphIndex, filter: &GraphFilter) -> WarmReport;
}
```

### Algorithm (normative)

- **Initial placement (RNG-free, deterministic):** node i of n on a golden-angle spiral — radius `r = spread * sqrt(i)`, angle `θ = i * 2.399963229728653` rad (golden angle), `spread = link_distance_px`. Coincident-node degeneracy therefore impossible at init; during simulation, pairs closer than ε=1e-6 separate along a direction derived from `splitmix64(seed ^ min_key_hash)` — seeded, not `thread_rng`.
- **Forces (Fruchterman-Reingold + gravity):** ideal length `k = L` where `L = lerp(20.0, 200.0, link_distance)` px. Repulsion `C_r * k²/d` along pair vector, `C_r = lerp(0.2, 5.0, repel)`. Attraction per edge `C_a * d²/k`, `C_a = lerp(0.2, 5.0, link)`, applied once per edge with weight `ln(1 + count)`. Gravity toward centroid-origin `C_g * d`, `C_g = lerp(0.0, 0.1, center)` (keeps disconnected components framed). Displacement capped by temperature `t`; cooling `t ← t * 0.97` per iteration from `t₀ = 0.1 * sqrt(n) * k`, floor `0.01 * k`. Converged when `max_displacement < 0.001 * k`.
- **Determinism (DoD §P-C):** f64 accumulation; node iteration in index order; edge iteration in (source, target, kind) order; **no rayon in the force pass**; fixed budgets with early-stop allowed only via the deterministic convergence predicate. Same graph + seed + forces + budgets ⇒ bit-identical positions on a given platform.
- **Barnes-Hut tier:** quadtree with center-of-mass, opening criterion θ=0.9, rebuilt each iteration (O(n), don't over-engineer). **Auto-selected at n > 1,500**; exact solver below. Public behavior identical; the tier is an internal switch (test hook to force either).
- **Pinning:** pinned nodes accumulate no displacement; they still exert forces.

### Tests (PR 1)

- Golden: fixture graphs (10, 100 nodes) — exact position snapshots at iterations {60, 300} on CI arch.
- Property: no NaN/Inf ever; positions bounded (`≤ 10 * sqrt(n) * k` from origin) with center > 0; energy (Σ pair potential) non-increasing measured over 10-iteration windows after iteration 50; permutation of node insertion order ⇒ identical layout up to index relabeling (key-order canonicalization makes this exact equality).
- **Oracle census `census_barnes_hut_matches_exact`:** random graphs ≤ 500 nodes, forced-BH vs forced-exact, per-iteration force vectors within 5% relative tolerance and final layouts within `0.05 * k` per node — plus the qualitative invariants (no NaN, bounded) at 10k forced-BH.
- Criterion: `layout_cold/{300n, 1500n, 10k}`, `layout_warm_tick/{300n}` (budget < 2 ms, locked decision 10), `layout_cold_10k` converge ≤ 3 s single-threaded release on Apple silicon.

## P2-2 · LayoutSession FFI — flat-buffer position protocol (#558) — PR 2

`#[uniffi::Object]` — state stays pointer-side; only position buffers cross (research brief §7).

```rust
impl VaultSession {
    /// Snapshot the current graph under `filter`, seed a LayoutSession.
    fn start_graph_layout(&self, filter: GraphFilter, forces: LayoutForces, config: LayoutConfig)
        -> Result<Arc<LayoutSession>, VaultError>;
}
#[uniffi::export]
impl LayoutSession {
    fn node_ids(&self) -> Vec<u64>;          // once; order matches every positions() buffer
    fn edges(&self) -> Vec<GraphEdge>;        // once
    fn tick(&self, iterations: u32) -> LayoutFrame;    // internally synchronized (Mutex)
    fn run_to_convergence(&self, cancel: Arc<CancelToken>) -> LayoutFrame;  // checks cancel every 10 iters
    fn set_forces(&self, forces: LayoutForces);
    fn pin_node(&self, id: u64, x: f32, y: f32);
    fn unpin_node(&self, id: u64);
    /// Re-sync with the live GraphIndex (generation check → warm_update). Returns
    /// None when generation unchanged (cheap no-op probe).
    fn refresh(&self) -> Option<LayoutFrame>;
}
pub struct LayoutFrame {
    pub positions: Vec<f32>,       // interleaved x0,y0,x1,y1… node_ids order; f32 at boundary, f64 inside
    pub iteration: u32, pub converged: bool, pub generation: u64,
}
```

Threading contract (normative): all methods callable off-main; Swift drives ticks from a background task (AppState `Task.detached` pattern) — interactive cadence `tick(20)` per frame while force sliders are engaged or settle animation runs, stop at `converged`. Reduce Motion path never runs interactive ticks: `run_to_convergence` then one frame (P2-3).

Tests: id/position order lock (property: positions index i ↔ node_ids[i] across refresh with node churn — surviving keys keep their slots' *identity mapping* consistent, i.e. re-fetch node_ids after any refresh that reports topology change, and the contract test proves stale-buffer detection via generation); cancellation ≤ 10 iterations after cancel; determinism through the FFI (two sessions, same inputs ⇒ identical frames); frame size = 2×n f32s.

## P2-3 · Diagram mode — CALayer renderer + native AX surface (#559) — PR 3

`GraphDiagramView` (NSViewRepresentable) inside the Graph tab; `GraphTabMode` toggle goes live (Table ↔ Diagram, U3 toggle pattern: one coherent AX tree per mode, mode persisted in `.slate/graph.json` via P2-4).

### Rendering (normative tiers)

- **Node tier A (n ≤ 1,500 visible):** one `CALayer` per node (circle; diameter `8 + 6*ln(1+in_links)` pt clamped 8–28), one `CATextLayer` label per node above zoom-dependent fade threshold with a visible-label cap of 200 prioritized by in_links (text-fade parity). Edges: single `CAShapeLayer` holding one `CGPath` of all edge segments (rebuilt per frame during settle; static after convergence), line width `0.5 + link_thickness` setting, arrowheads only when the Arrows toggle is on.
- **Node tier B (n > 1,500):** nodes batched into tiled draw layers (`CATiledLayer` or manual tiles), no per-node layers/labels except the selection + hover set; the AX tree switches to summary mode (below). Tier switch is automatic per visible-filtered count; announced ("Large graph: summary accessibility mode; Table mode has every node.").
- 60 fps pan/zoom via layer transforms (no re-layout on pan/zoom); positions update only from `LayoutFrame`s. Settle animation = applying successive frames; **Reduce Motion ⇒ jump straight to converged frame** (single transition, no drift animation).
- Hit-testing: uniform spatial grid (cell = 64 pt) rebuilt per frame from the position buffer; hover → tooltip (label + "n in / m out"), click → select, double-click/Return → open.

### Accessibility (normative — the headline)

- Tier A: per-node `NSAccessibilityElement` children (Canvas #367 pattern): label = P1-1 row copy verbatim (`"{label}, {n} links in, {m} links out"` etc.), role `.button`-like with actions Open / Show connections / Pin; frame tracks the node's screen rect (updated on frame apply + pan/zoom). Edges are not individual AX elements; the node's `accessibilityCustomContent` lists its neighbor labels (first 10 + "and k more").
- Keyboard on the diagram (graph-focus scope, palette-mirrored per T rule R2): arrows = spatial nearest-neighbor move in that direction **among the focused node's graph neighbors first, falling back to nearest visible node** (MIT-VIS spatial navigation); Tab/⇧Tab = next/previous node in key order (structural); type-ahead = targeted jump by label prefix. ⌃⌘I "Where am I?" = full readback (node copy + component + zoom + active filters) through the GraphAnnouncer (p1_spec baseline). ⌘= / ⌘- / ⌘0 zoom **via the focus-routed menu owner** (baseline fact above — join/build the #848 router with a graph-tab case; routing-priority test, not per-surface chord claims), ⌥⌘0 fit graph (new chord — register per R3, refresh the inventory table + drift-tested help table in the same PR).
- Selection ring uses the system accent + a shape change (thicker ring + inner dot) — never color alone.

Tests: tier switch at the boundary count; AX element count/labels/actions on fixture graph (XCTest AX API); keyboard spatial-move determinism on a fixture layout; Reduce Motion path (no intermediate frames applied); zoom-chord routing-priority + single-menu-owner tests (baseline fact — replaces the "scope disjointness" drift test); APCA measurements for node/edge/label pairs in both appearances recorded in the PR.

## P2-4 · Graph controls + persistence (#560) — PR 4

Inspector (trailing popover/panel within the graph tab, both modes): **Filters** (text query — client-side label substring, same semantics as Table filter; Attachments; Unresolved; Orphans only), **Groups** (ordered list of query→color rules; query = same label/folder/tag matcher as the table filter; color from an 8-slot token palette, each slot APCA-checked in both appearances; each group also assigns a ring style — solid/dashed/double/dotted — cycling automatically so color is never the sole channel; first-match-wins, matching Obsidian), **Display** (Arrows, Text fade threshold, Node size multiplier, Link thickness), **Forces** (Center / Repel / Link force / Link distance sliders 0–1 → `set_forces`, live re-heat).

Persistence: `.slate/graph.json` v1 (normative schema in the PR): `{ "version": 1, "filters": {...}, "groups": [{"query","colorToken","ringStyle"}...], "display": {...}, "forces": {...}, "mode": "table"|"diagram", "connectionsDepth": 1 }`. Write discipline per the O-5 `history_prefs.rs` pattern (baseline fact): atomic unique-temp+rename, unknown future keys preserved on rewrite (forward-compat, same rule as `.obsidian` preservation), refuse-to-clobber-unparseable. **Decide writer ownership in the PR:** if only the Mac app writes it (expected — mirror `PrefsJsonStore.swift`), document single-writer and skip the lock; if Rust ever co-writes, add a sidecar `graph.json.lock` flock per history_prefs.rs (rename prevents torn JSON, not lost updates). A separate file — not a `prefs.json` section — precisely to avoid contending on `prefs.json.lock`. P1-1's depth setting migrates here.

Every control is a labeled, keyboard-operable standard control (sliders with value announcements); the inspector is a `.contain` AX group. Slider changes announce the resulting condition once settled ("Repel force 0.7; layout settling… settled.") — debounced through the GraphAnnouncer.

Tests: schema round-trip incl. unknown-key preservation; group precedence (first-match-wins) + ring-style assignment; filter equivalence table↔diagram (same predicate code path — assert single source of truth); slider → `set_forces` → re-settle E2E; a11y-check.

## P2-5 · Projection sync + visual-surface closure (#561) — PR 5

- **Shared selection/filter state:** one `GraphViewState` observable (selected node key, filter, groups) owned by the tab; Table rows, Diagram selection, and `Leaf.connections` re-rooting all read/write it. Selecting in any projection reflects in the others within one runloop tick; Diagram→Table mode switch lands focus on the selected node's row (and vice versa: Table→Diagram focuses its AX element).
- **Action-parity drift test (DoD §P-B):** enumerated action sets for diagram node / table row / connections row asserted equal (Open, Open in new tab, Show connections, Reveal, Create note-for-ghost, Pin [diagram-only, exempted with rationale in the test]).
- **VoiceOver E2E script:** the P1 walkthrough extended across projections: run "Graph: orphaned notes" → Table first row → switch to Diagram (focus lands on same node's AX element) → arrow to a neighbor → "Where am I?" → Show connections → depth 2 → back. Recorded in the PR; becomes docs material.
- Global sweep: announcement copy audit (all through the GraphAnnouncer, verbosity honored, `testNoDirectAnnouncementsUnderGraph` lint green), focus return on tab close/mode switch (WCAG 2.4.3), no keyboard traps, a11y-check 100/100, APCA table for every new pair, dark/light screenshots, `BENCHMARKS.md` refresh (§P-E budgets), full census suite re-run (`SLATE_CENSUS_FULL=1`).

## P-D · Docs: `docs/help/graph.md` (#562) — PR 6

User guide: what the graph shows (model, node/edge kinds, ghost semantics), the three projections and when each wins (incl. the honest large-vault guidance: table > diagram past the cognitive ceiling), full keyboard + VoiceOver reference (from the recorded walkthroughs), presets, groups, forces, `.slate/graph.json` format, Obsidian-parity notes + deliberate divergences (no Animate; typed embed edges; accessibility-first ordering) with one-line rationales linking [`../01_research_brief.md`](../01_research_brief.md) §8 for the positioning story. Routed through the in-app help system the same way `docs/help/canvas.md` (#526) lands.
