# 11 — Graph research brief: what graph views are for, why they fail, and why Slate's is different

**Status:** Research locked 2026-07-03/04 (three parallel research passes: product/UX usage, accessibility state of the art, Rust backend tooling). This document is the evidence base for the decisions locked in [`00_program.md`](00_program.md). It is deliberately written to double as source material for later marketing/positioning writeups — every claim carries its citation, and secondary/opinion claims are flagged as such.

---

## 1. The one-paragraph thesis

Every note app in Slate's category ships a graph view, and every one of them ships the same two failures: **it collapses into an unreadable hairball a few hundred notes in** (a design failure), and **it is rendered as an opaque WebGL/canvas image that no screen reader or keyboard can enter** (an accessibility failure — in seven apps surveyed, without exception). The same architectural move fixes both: make a **structured, queryable, navigable relational model** the source of truth, and treat the picture as one *projection* of that model rather than the artifact itself. Slate is unusually well positioned to be the app that does this, because the structured-equivalents principle is already product law here (`05` §5.2, `01` §4: "Visual features must have structured equivalents").

---

## 2. What people actually use graph views for

Synthesized from Obsidian's official docs and named practitioner writeups. Reddit sentiment was not reachable in this research pass, so *relative popularity* of workflows is under-sampled; the workflows themselves are concrete and consistent across sources.

**Genuinely useful (multiple independent sources):**

1. **Orphan / index-maintenance detection.** The most-cited real workflow. "If there are a bunch of orphan things hanging out around the edges… I know that I have not updated my indexes" — Eleanor Konik, [It's not just a pretty gimmick: in defense of Obsidian's graph view](https://www.eleanorkonik.com/p/its-not-just-a-pretty-gimmick-in-defense-of-obsidians-graph-view).
2. **Maturity check.** Sparsely-linked notes read as "idea not developed yet"; densely-linked ones as publishable. (Konik, ibid.)
3. **Duplicate / sync-artifact detection.** A top-down view catches accidental duplicates the file tree hides. (Konik, ibid.)
4. **Structural discovery via color-groups-by-query.** Konik filtered by her organizational system and discovered newsletters acting as connective "glue" between atomic notes — an insight that changed how she synthesized. Groups-by-query doing real analytic work.
5. **Local-graph navigation.** "Choose-your-own-adventure" traversal from note to neighbor to neighbor — [The Sweet Setup, The power of Obsidian's local graph](https://thesweetsetup.com/the-power-of-obsidians-local-graph/). The one *navigation* use that survives vault growth.

**Mostly demo candy:** staring at the global graph for serendipity (Konik herself concedes shared graph screenshots serve "motivational and presentational purposes"), the animated time-lapse, and global-graph-as-primary-navigation.

## 3. The honest case against — and the design brief hidden in it

Primary critique: [Code Culture, "Obsidian Graph View: Beautiful and Almost Completely Useless"](https://codeculture.store/blogs/developer-culture/obsidian-graph-view-useful); corroborating: [worklifewinrepeat](https://worklifewinrepeat.com), Tamara Munzner's dictum via [Ann P., Visualizing connections](https://medium.com/@ann_p/visualizing-connections-graph-views-in-obsidian-tana-and-anytype-3c767e08fe66): *"The goal of visualization is insight, not pictures."*

- **Community-consensus usability thresholds** (informed opinion, not measured): useful < 50 notes; degrading ~200; "reliably a hairball" 500+.
- **The structural critique:** the graph "does not show note status. It does not show priorities. It does not tell you which notes are stale, which are drafts, and which are finished." All links are equal, untyped, undirected edges, so force layout collapses dense regions into indistinguishable blobs. Power users navigate with backlinks panes, quick switchers, and queries instead, and some run typed overlays (Excalibrain) to escape the flat mesh.
- **What critics say would make it useful:** (1) typed/weighted edges, not a flat mesh; (2) operational metadata (status, staleness) encoded, not just topology; (3) scoped local/depth-limited views over the global blob; (4) actionability — read-only is the complaint.

This is Slate's design brief, near verbatim — see locked decisions 2, 5, 6 in `00_program.md`.

## 4. Scale: two ceilings, and where Obsidian actually breaks

- **Cognitive ceiling:** a few hundred notes (above). No renderer fixes this; only scoping, filtering, and metadata do.
- **Technical ceiling:** [Obsidian forum, "Graph view doesn't work for a large vault"](https://forum.obsidian.md/t/obsidian-graph-view-doesnt-work-for-a-large-vault/106287) — with developer/moderator response, the firmest datapoint found: **130k notes froze the graph outright; ~29k was functional; developer: "I don't think anything above 25K files is practical with a modern desktop computer."** The telling detail: on an i7-14700KF / 64 GB / RTX 4090, the graph pegged **one CPU core at 100% while the GPU sat idle**. Obsidian renders via PixiJS/WebGL; the wall is the **single-threaded force simulation**, not drawing.

Implication: a Rust core that runs layout off-main (and Barnes-Hut above ~1.5k nodes) comfortably clears the technical ceiling — but the program treats the *cognitive* ceiling as the real constraint and designs for scoped views first.

## 5. Comparables

| App | Approach | Verdict for Slate |
|---|---|---|
| **Obsidian** | Global + local force graph ([official docs](https://obsidian.md/help/plugins/graph)): filters (query/tags/attachments/existing-only/orphans), color groups by search query, display (arrows, text fade, node size, link thickness), four forces (center/repel/link/link-distance), local depth slider | The capability reference. Parity target for controls; not the interaction reference. |
| **Roam** | Per-page graph spatially separates **mentions (top)** from **outbound links (bottom)** ([comparison](https://tharan.medium.com/comparing-roamresearch-graph-view-with-logseq-and-obsidian-b0c1fd51c2ee)); global view called "boring… brick-like" | **The single best idea to steal**: the in/out split maps directly onto an accessible two-list layout. |
| **Logseq** | Force graph, best-in-class filtering per the same comparison; global view "almost useless… except to show off" | Filter UX reference. |
| **Zettlr** | D3 force graph buried in a Statistics window; docs [explicitly warn](https://docs.zettlr.com/en/pkms/graph/) "placement… does not have any inherent meaning"; no local graph | Cautionary: a graph nobody scoped. |
| **Tana** | **No visual graph at all** — supertags + queries/views ([tana.inc](https://tana.inc/knowledge-graph)); users maintain an open feature request for one | Proof "structure over picture" is a legitimate stance — and that people still want the picture. |
| **Reflect** | "Map" as a first-class panel; pitched squarely at orphan detection | Validates orphans as the marquee workflow. |
| **Anytype** | Typed-object graph; filter/group by type | Closest to the "typed edges fix the hairball" prescription. |

## 6. Accessibility: the category-wide zero, and the research base for doing better

**State of the art is an opaque image.** Obsidian's graph renders to PixiJS/WebGL on a `<canvas>`. A canvas is a single flattened bitmap to assistive tech — "custom rasterization … negates all interactivity and accessibility features when elements are flattened into pixels" ([Anneka Goss, Accessible WebGL](https://annekagoss.medium.com/accessible-webgl-43d15f9caa21)). No element to tab to, no role, no label. In the main [Obsidian screen-reader thread](https://forum.obsidian.md/t/accessibility-obsidian-with-screen-readers/19669), blind users discuss the editor, links, palette, and Canvas — **the graph view is never even raised**: an unaddressed, dead surface. None of the seven surveyed apps has a keyboard- or screen-reader-accessible graph. (Absence-of-evidence finding, but the canvas/WebGL rationale makes it near-certain rather than merely unobserved.)

**The research base for an accessible graph exists and is directly actionable:**

- **MIT Visualization Group, [Rich Screen Reader Experiences for Accessible Data Visualization](https://vis.csail.mit.edu/pubs/rich-screen-reader-vis-experiences/)** — three design dimensions: **structure** (expose the vis as a traversable hierarchy with granularity levels: existence → overview → detail), **navigation** (three modes: *structural* parent/child/sibling stepping, *spatial* movement to connected neighbors, *targeted* jump via search), **description** (semantic content at each level, with clear "where am I" reference points).
- **Cambridge Intelligence, [Building accessible data visualization apps](https://cambridge-intelligence.com/build-accessible-data-visualization-apps-with-keylines/)** — keyboard-first (every node/edge reachable without a mouse), read out node/link info on focus, never rely on color alone (pair with size/shape/border/position), and: *"Accessibility considerations should influence your core design choices from the start, not be added later."*

Slate's translation (locked in `00_program.md`): the **Connections navigator** (Roam's in/out split as two keyboard lists + depth tree = the local graph, accessibly), the **graph table** (the global graph as a sortable grid = MIT-VIS "targeted" mode), per-node focus announcements carrying degree + status metadata (closing the a11y gap and the "decorative" critique with one move), and per-node `NSAccessibilityElement`s on the visual surface itself at local scale (the Canvas program's #367 precedent).

## 7. Rust backend tooling survey

Full crate-level survey conducted 2026-07-03. Summary of findings and the recommendation they force:

| Concern | Finding | Decision |
|---|---|---|
| Graph structure | [petgraph](https://github.com/petgraph/petgraph) 0.8.x (MIT/Apache-2.0, ~30M dl/mo, 12k dependents) provides `StableGraph` — node/edge indices stay valid across removals, i.e. built for an incrementally-maintained link graph. Algorithms: connected components, SCC, PageRank, BFS/DFS, toposort. Serde optional. Alternatives (pathfinding, graaf) are narrower and lack stable-index mutation. | **Adopt petgraph (`StableDiGraph`)** — the only new required dependency. |
| Force layout | The crate landscape is collectively unshippable: [forceatlas2-rs](https://framagit.org/ZettaScript/forceatlas2-rs) (best algorithm, Barnes-Hut, actively maintained) is **AGPL-3.0-only** — *license-compatible with Slate's AGPL-3.0-or-later*, but rejected on capability grounds: no warm-start/incremental API, no determinism contract (parallel reductions), and a copyleft-pinned dependency we'd be shaping our FFI around. [fdg-sim](https://lib.rs/crates/fdg-sim) abandoned (Dec 2022); [fdg](https://github.com/grantshandy/fdg) a perpetual unpublished rewrite; [layout-rs](https://github.com/nadavrot/layout) is Sugiyama/hierarchical (wrong class); [egui_graphs](https://lib.rs/crates/egui_graphs) a naive-FR reference, not a dependency. | **Hand-roll** Fruchterman-Reingold (~150 lines) + Barnes-Hut quadtree (~300 lines, oracle-tested against the exact solver). Full control of determinism, warm-start, pinning. This is precisely the kind of small numerically-delicate kernel the project already census-tests well (#379, #404). |
| Communities/centrality | [graphrs](https://github.com/malcolmvr/graphrs) (MIT, active) has Louvain/Leiden + betweenness, but uses its own graph type and pulls nalgebra/ndarray. [single-clustering](https://lib.rs/crates/single-clustering) is petgraph-native Leiden but self-describes "not production ready." | **Defer.** v1 sizing/coloring uses petgraph-native degree, components, PageRank — deterministic, zero extra deps. Revisit graphrs for true modularity clustering in P3. |
| FFI shape | UniFFI serializes records/sequences into a `RustBuffer` **on every call** ([UniFFI internals](https://mozilla.github.io/uniffi-rs/latest/internals/lifting_and_lowering.html)); interface objects pass one pointer. Streaming a `Vec<NodePos{id,x,y}>` at frame rate is allocation churn; a flat contiguous `Vec<f32>` is one cheap buffer. | **Layout session = uniffi interface object (by pointer); hot path = interleaved flat `Vec<f32>` positions; ids/edges sent once.** |
| Determinism traps | Float summation is non-associative → a rayon-parallelized force reduction is nondeterministic bit-for-bit. `thread_rng` and wall-clock convergence loops likewise. | Seeded placement, fixed iteration budgets, `f64` accumulation single-threaded in deterministic index order, `f32` only at the FFI boundary. |

## 8. The positioning story (marketing raw material)

The claims below are the defensible core of a future writeup; each traces to a section above.

1. **"The first graph view a screen-reader user can actually use."** Category-wide gap, verified across seven apps (§6). Slate's graph is a projection of a structured model — every node reachable, announced, and actionable by keyboard and VoiceOver, on the visual surface itself at local scale, and through first-class table/navigator projections at any scale.
2. **"A graph that answers questions instead of posing them."** Orphans, unresolved links, hubs, staleness — the workflows practitioners actually report (§2) — are one command away as presets, not squint-and-hunt exercises (§3's critique, inverted into the feature list).
3. **"Engineered past Obsidian's wall."** Obsidian's own developers call >25k files impractical; the simulation is single-threaded (§4). Slate's layout runs in Rust, off the main thread, deterministic and census-tested, with Barnes-Hut scaling — and the honest admission that above the cognitive ceiling the *table* is the better tool is itself a differentiator.
4. **"Same physics, same controls, none of the lock-in."** Center/repel/link/link-distance forces, filters, and color groups mirror the controls Obsidian users already know (§5), stored vault-locally in plain JSON.

## 9. Source register

Product/UX: [Obsidian graph docs](https://obsidian.md/help/plugins/graph) · [Konik](https://www.eleanorkonik.com/p/its-not-just-a-pretty-gimmick-in-defense-of-obsidians-graph-view) · [Sweet Setup](https://thesweetsetup.com/the-power-of-obsidians-local-graph/) · [Code Culture](https://codeculture.store/blogs/developer-culture/obsidian-graph-view-useful) · [Roam/Logseq/Obsidian comparison](https://tharan.medium.com/comparing-roamresearch-graph-view-with-logseq-and-obsidian-b0c1fd51c2ee) · [Ann P.](https://medium.com/@ann_p/visualizing-connections-graph-views-in-obsidian-tana-and-anytype-3c767e08fe66) · [Zettlr docs](https://docs.zettlr.com/en/pkms/graph/) · [Tana](https://tana.inc/knowledge-graph) · [Reflect](https://reflect.app/) · [Obsidian scale thread](https://forum.obsidian.md/t/obsidian-graph-view-doesnt-work-for-a-large-vault/106287).
Accessibility: [MIT VIS](https://vis.csail.mit.edu/pubs/rich-screen-reader-vis-experiences/) · [Cambridge Intelligence](https://cambridge-intelligence.com/build-accessible-data-visualization-apps-with-keylines/) · [Accessible WebGL](https://annekagoss.medium.com/accessible-webgl-43d15f9caa21) · [Obsidian screen-reader thread](https://forum.obsidian.md/t/accessibility-obsidian-with-screen-readers/19669).
Rust tooling: [petgraph](https://github.com/petgraph/petgraph) · [forceatlas2-rs](https://framagit.org/ZettaScript/forceatlas2-rs) · [fdg](https://github.com/grantshandy/fdg) · [fdg-sim](https://lib.rs/crates/fdg-sim) · [layout-rs](https://github.com/nadavrot/layout) · [egui_graphs](https://lib.rs/crates/egui_graphs) · [graphrs](https://github.com/malcolmvr/graphrs) · [single-clustering](https://lib.rs/crates/single-clustering) · [UniFFI lifting/lowering](https://mozilla.github.io/uniffi-rs/latest/internals/lifting_and_lowering.html) · [Barnes-Hut explainer (Heer)](https://jheer.github.io/barnes-hut/).

**Flagged as unverified/opinion:** the 200/500-note usability thresholds and the "60% of PKM users" survey figure (Code Culture) are community consensus, not measurement. The 25k/130k/single-core figures come from a forum thread with developer response and are firm.
