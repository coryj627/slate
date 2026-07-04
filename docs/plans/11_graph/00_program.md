# 11 — Graph Program (Milestone P): the first graph view a screen-reader user can actually use

**Status:** 📝 Specs locked (2026-07-04); implementation not started. GH [milestone 16](https://github.com/coryj627/slate/milestone/16). Supersedes the Phase-1/Phase-2 ordering sketched in [`../01_detailed_roadmap.md`](../01_detailed_roadmap.md) §7 and the P row of [`../06_v1_milestones.md`](../06_v1_milestones.md) (owner decision 2026-07-04: accessible model ships first; the visual graph is a projection of it). Evidence base: [`01_research_brief.md`](01_research_brief.md).

**Strategic goal.** Ship an Obsidian-parity graph feature — global + local graph, filters, color groups, force controls — built as **one relational model with two synchronized projections**: an *accessible relational navigator* (connections leaf + sortable graph table, fully keyboard- and VoiceOver-operable) and a *visual force-directed diagram* (native `CALayer` rendering, layout computed deterministically in Rust). Every comparable app renders its graph as an opaque canvas a screen reader cannot enter, and every one of them degrades into an unreadable hairball at scale; the research brief documents both failures and the shared fix. The projections read the same model, stay selection-synchronized, and the visual one is never the only path to any datum or action. Obsidian is the capability reference, not the interaction reference — same stance as the Canvas program (`../09_canvas/00_program.md`).

Everything here inherits the UI-parity Presentation-Ready DoD (`../08_ui_parity/00_program.md` §A–§G): a11y-check 100/100, APCA Lc ≥ 75 measured in both appearances, census-gated invariants, atomic writes, one PR per issue, fmt/clippy pre-push. This document adds only what is graph-specific.

---

## Locked scope decisions (owner review, 2026-07-04)

| # | Area | Decision |
|---|------|----------|
| 1 | Build order | **Accessible model first.** P0 (graph backend) → P1 (navigator + table) → P2 (visual diagram). The visual layer arrives as a projection over a proven, shipped model — inverting `01` §7's "visually credible early, accessibility matures later." Rationale: research brief §2/§6 — the scoped/structured views carry the real workflows; the visual-first order is how every other app ended up with an inaccessible decoration. |
| 2 | One model, two projections | A single Rust `GraphIndex` (+ metrics) feeds both projections through one FFI surface. Selection and filter state are shared: selecting a node in the diagram focuses the same node in table/leaf contexts and vice versa. No projection-private data. |
| 3 | Model shape | Nodes: notes, attachments, **ghosts** (unresolved link targets). Edges typed from day one — `EdgeKind::{Link, Embed}` now (both already distinguished in the `links` table); tag/property edges reserved for P3, never a schema break. External URLs are not nodes. |
| 4 | Layout engine | **Hand-rolled deterministic Fruchterman-Reingold in slate-core** (+ Barnes-Hut above ~1.5k nodes, oracle-tested against the exact solver). Seeded, fixed-budget, warm-started, pin-capable. Crate survey and rejection rationale: research brief §7 — `forceatlas2` is license-compatible (both AGPL) but rejected on determinism/incrementality/maintenance grounds; the rest are abandoned or wrong-class. petgraph (`StableDiGraph`) is the only new dependency. |
| 5 | Rendering | `NSView` + `CALayer` per the locked decision `../05_locked_architecture_decisions.md:477`; **not** SwiftUI Canvas, **not** a webview, **not** Rust-side wgpu. Rust computes positions; AppKit draws; hit-testing and accessibility stay native. At local/filtered scale (≤ ~1,500 visible nodes) the surface publishes **per-node `NSAccessibilityElement`s** (Canvas #367 precedent); above that tier it renders tiled and the AX tree exposes a summary element that routes to Table mode — honest, not fake. |
| 6 | Anti-hairball stance | The cognitive ceiling (~a few hundred visible nodes) is treated as the design constraint, not a rendering problem. Defaults favor scoped views: the Connections leaf and depth-limited local diagram are the primary surfaces; the global diagram opens filtered (attachments off, ghosts on) with presets (orphans, unresolved, hubs) one command away. Node focus/announcement carries operational metadata (degrees, component, modified age) — the thing critics say graph views omit. |
| 7 | Surfaces | Graph opens as a **workspace tab** (the `"graph"` `EditorItem` discriminator reserved by U1-6, `WorkspaceModel.swift:38`) with a **Table ↔ Diagram mode toggle** (U3 reading/editing pattern: one coherent AX tree per mode; Table is the only mode until P2 lands). The local view ships as a **right-pane `Leaf.connections`** (M-3 `Leaf.syncDiagnostics` precedent, `RightPaneView.swift`). |
| 8 | Persistence | Graph settings (filters, groups, forces, mode) are vault-local `.slate/graph.json`, versioned, atomic temp+rename writes — same convention as `.slate/prefs.json`. |
| 9 | Obsidian parity set | Filters (search-scope, attachments, ghosts/existing-only, orphans), color **groups by query** (color from token palette, always paired with a non-color channel), display (text-fade by zoom, node size by degree, link thickness), forces (center / repel / link / link-distance), local depth 1–3, node actions (open, open-in-new-tab, show connections, reveal in tree). The "Animate" time-lapse is **out of scope** (demo candy; research brief §2). |
| 10 | Scale budgets | Metrics and censuses run at the standing 1k/10k/50k synthetic-vault scales (`BENCHMARKS.md`). Layout: local diagram (≤ 300 nodes) warm tick < 2 ms; global 10k-node converge ≤ 3 s off-main on Apple silicon; 50k = capped-iteration best effort with progress announced and Table mode suggested. All layout off the main thread; the FFI hot path is a flat `Vec<f32>` position buffer. |

---

## Phase map, waves & dependencies

```
Wave 1 (backend)   P0-1 GraphIndex ─▶ P0-2 metrics ─▶ P0-3 FFI surface ─▶ P0-4 census+bench gate
Wave 2 (navigator) P1-1 Connections leaf ─ P1-2 Graph tab: Table mode ─ P1-3 commands/presets
                   (∥ P2-1 layout kernel — pure Rust, no UI dependency)
Wave 3 (visual)    P2-2 LayoutSession FFI ─▶ P2-3 Diagram mode renderer ─ P2-4 controls ─ P2-5 projection sync + AX closure
Wave 4 (close-out) P-D docs/help/graph.md
```

| Wave | Issues | Gate |
|------|--------|------|
| 1 — Backend core | P0-1 → P0-2 → P0-3 → P0-4 | none (start any time; pure slate-core) |
| 2 — Accessible navigator | P1-1, P1-2, P1-3; P2-1 runs in parallel (backend-only) | Wave 1 complete. P1-2 prefers `AccessibleDataGrid` v2 (#519, Canvas Wave 2, shared with N) — if unlanded, build on v1 grid and note the upgrade path. |
| 3 — Visual projection | P2-2 → P2-3 → (P2-4 ∥ P2-5) | P2-1 + Wave 2 (P2-5's selection sync targets P1 surfaces) |
| 4 — Close-out | P-D | Wave 3 |

**Sequencing vs. other programs:** U1 shell (landed) provides the tab seam; U4's right-pane leaf registry (landed surfaces in `RightPaneView.swift`) hosts the leaf. The Canvas program (T) is sequenced ahead of P per the 2026-07-03 owner decision — P's Wave 1 (and P2-1) are pure backend and can interleave with T's UI waves without contention; P's UI waves should not start until T's Wave 2 lands `AccessibleDataGrid` v2 (#519) or explicitly falls back.

## Relationship to other milestones (do not duplicate)

- **N — Bases:** `01` §6 Phase 3 plans graph-derived Base fields (`file.inDegree`, `file.outDegree`, `file.isOrphan`, `file.cluster`). P0-2's metrics are that substrate; N consumes the same session queries — no parallel metric computation.
- **T — Canvas:** P reuses T's normative patterns wholesale: announcement coordinator + verbosity (t0 interaction contract), per-element AX on a visual surface (#367), `AccessibleDataGrid` v2 (#519), viewport zoom chords (⌘= / ⌘- / ⌘0 scoped to surface focus, drift-tested), "Where am I?" readback (⌃⌘I scoped to graph focus). Where T defined a convention, P adopts it rather than inventing a sibling.
- **Q — Command palette:** every graph action is a `CommandRegistry` command (new `CommandSection.graph`); presets are commands. No chords beyond the shared zoom/readback set — palette + menu are the paths (T rule R1).
- **R — Themes:** node/edge/group colors are semantic tokens; group palette ships APCA-checked pairs in both appearances. R re-skins tokens later; P hardcodes nothing.
- **W — Windows (parked):** `GraphIndex`, metrics, and the layout kernel are host-independent slate-core; nothing in P0/P2-1 may take a macOS dependency.

## Definition of Done (graph-specific, additive to U §A–§G)

- **§P-A Accessible-first gate:** P1 fully usable — every workflow (orphans, unresolved, hubs, local exploration, open/act) completable keyboard-only and with VoiceOver — **before any P2 issue merges.**
- **§P-B Projection equivalence:** any datum or action available in the diagram is available in table/leaf form; the drift test enumerates diagram actions against table/leaf actions.
- **§P-C Determinism:** same vault + same seed + same params ⇒ identical layout, golden-tested; permutation of file-insertion order ⇒ identical graph, metrics, and (up to relabeling) layout. No `thread_rng`, no wall-clock loops, no parallel force reductions.
- **§P-D Census:** incremental `GraphIndex` ≡ fresh rebuild from the links table after every mutation (adversarial random + exhaustive, `SLATE_CENSUS_FULL=1` scaling); Barnes-Hut ≡ exact solver within θ-tolerance. Census names and scales are normative in the specs.
- **§P-E Perf:** budgets in locked decision 10, baselined in `BENCHMARKS.md` at milestone close; no regression to `scan_initial` or save paths (the graph hooks ride existing write paths and must stay O(changed-file)).

## Specs

- [p0 — Graph backend: GraphIndex, metrics, FFI, censuses](specs/p0_spec.md)
- [p1 — Accessible navigator: Connections leaf, graph table, commands](specs/p1_spec.md)
- [p2 — Visual projection: layout kernel, LayoutSession FFI, Diagram mode](specs/p2_spec.md)

P3 (deferred, issues filed when P2 lands): tag/property edge kinds, Louvain/Leiden community groups (graphrs), path finder between two notes, cluster explorer, Base-driven graph result sets, large-vault layout caching.
