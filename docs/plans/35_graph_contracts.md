# W6-2 — Graph on Windows (#746): contracts

Scope: the stacked PR series that ports the mac graph (Milestone P) to
Windows —
[`18_windows_port/specs/w6_2_graph_spec.md`](18_windows_port/specs/w6_2_graph_spec.md),
behaviourally governed by the P program's locked decisions
([`11_graph/00_program.md`](11_graph/00_program.md)) and its specs
([`p0_spec.md`](11_graph/specs/p0_spec.md), [`p1_spec.md`](11_graph/specs/p1_spec.md),
[`p2_spec.md`](11_graph/specs/p2_spec.md)). Written BEFORE each PR's
round 1 per [`24_red_team_protocol.md`](24_red_team_protocol.md) §0. The
divergence (GD), accepted-risk (GR) and owner-decision registers are
owner-recorded and **off-limits for review re-litigation**.

Contract ids are prefixed by PR letter (`0a-1…`, `A1…`) so a round can
cite them unambiguously; numbering is per-document. Per-section
decisions, divergences and risks use the §H grammar of the canvas
document (`0aD-`, `0a-D`, `0aR-`) so the reconciliation's key derivation
carries over unchanged.

> **Filename note.** The issue's 2026-08-09 delivery note names
> `docs/plans/33_graph_view_contracts.md`. **33 was taken** by
> `33_upgrade_fence_contracts.md` and 34 is the canvas's, so this
> document is **35**; the spec (§0 item 6) already says so.

---

## PR 0a — the graph announcer vocabulary moves to core

**Goal (spec §PR 0a).** Every graph announcement becomes a typed
`A11yEvent` in `crates/slate-core/src/a11y.rs` with its template,
priority and verbosity policy core-side; mac's `GraphAnnouncer` shrinks
to a relay + coalescer (Task 0a-2); `corpus.json` gains the graph
family; the residue census drops 29 → 28; §W-D becomes provable for the
graph. **Copy rule: shipped mac strings move verbatim** (0a-16 names the
authority per phrase); this PR does not redesign wording. This section
is at revision 3 after round 1's sixteen findings and round 2's twelve.

**What stands today.** `GraphAnnouncer.swift` (239 lines): a
`GraphVerbosity` enum mirroring the canvas's — declared, defaulted to
standard, and set from nowhere (no store, no menu; `GraphConfig` has no
field for it), a `GraphRowRef` carrying a node's label and counts, a
five-case `GraphEvent` (`rowFocused`, `summary(String)`,
`reRooted(label)`, `status(String)`, `error(String)`) of which three are
free-text passthroughs, three class-keyed debounced posters outside the
event type (`announceFilterCount`, `announceForceValue`,
`announceSettle`), one grammar (`rowPhrase`, `:158–170`) and one
template (`"Connections: {label}"`, `:179`), all posting through the one
`// W0.5-3 residue:` site (`:98–100`). The manifest (0a-12) is the site
inventory. Core carries two workspace-level graph events
(`GraphOpensSinglePane`, `ReopenedGraph`) and zero graph-announcer
entries in the corpus; the two `audio_summary` strings are composed in
`session.rs` (`snapshot_audio_summary` `:9017`; the neighbourhood string
inline at `:1990–1997`), outside the vocabulary.

### Contracts

**0a-1 — Full coverage, no free text.** Every shipped graph
announcement string is a typed variant or a typed ARM of one. Mac's
three free-text cases and the three text-taking posters dissolve.
`String` payload is permitted only for dynamic data — labels, file
names, OS error detail, the name-filter needle — **never for a whole
sentence**. The schema (0a-2b) is the vocabulary: seventeen variants
over seven nested a11y enums plus one reused graph enum; a string with
no home there is a round finding, not an implementation choice.

**0a-1b — One engine, one top-level variant.** The family is reached
through `A11yEvent::Graph { event: GraphA11yEvent }`, the nested
family-per-engine pattern 0a-1b of the canvas document made the rule
(`a11y.rs:1246–1255`); `GraphA11yEvent` owns its variants and its own
`priority()` / `render()`, which `A11yEvent` delegates to. `A11yEvent`
stands at **198** top-level variants today; this PR adds one, to 199 of
uniffi's 256, and `a11y_event_top_level_count_is_pinned` pins that
number so a later family bumps it on purpose. Variant NAMES keep their
`Graph` prefix. Pinned by `the_graph_family_occupies_one_top_level_variant`.

**0a-2 — The five-place rule.** A graph string is pinned in five
hand-maintained places, and all five move together: the variant and
its `render()` arm (`a11y.rs`), the in-file golden table
(`corpus_renders_the_shipped_strings`), the regenerated artifact
`tests/fixtures/a11y/corpus.json` (through the artifact test's own
regeneration path, never by hand), the FFI mirror
(`crates/slate-uniffi/src/lib.rs`), and **both** census mirrors
(`A11yCorpusCensusTests.swift`, `A11yCorpusCensus.cs`). Corpus order is
positional, so the graph family is **appended** after the canvas
family's last entry (`CanvasStatus { Loading }`, `a11y.rs:4491–4493`);
every pre-existing index is untouched. The witness matrix (0a-17) is
the ordered list of the graph's entries: **61** of them, the artifact
growing from 448 to **509**; `the_graph_witnesses_are_exactly_the_matrix_in_order`
(0a-19) is the executable form of the matrix.

**0a-2b — The schema, literally.** Every type below is a uniffi record
or enum; widths are as written; nothing is a prose shorthand.

Supporting types:

| Type | Definition | Note |
|---|---|---|
| `GraphVerbosity` | `enum { Terse, Standard, Verbose }` | new; 0aD-2 |
| `GraphNodeKind` | the EXISTING FFI enum `{ Note, Attachment, Ghost }` (`lib.rs:3231`) mirroring `graph::NodeKind` (`graph.rs:53`) | reused (0aD-4); this PR adds the reverse mapping `From<GraphNodeKind> for core::graph::NodeKind` so a host can construct a row |
| `GraphRowCopy` | `record { label: String, kind: GraphNodeKind, in_links: u32, out_links: u32, references: u32, embed: bool }` | P1's row data; degrees are `u32` as `GraphNode`'s |
| GraphSnapshotCounts | `record { notes: u64, links: u64, orphans: u64, unresolved: u64, filtered: bool }` | exported on `GraphSnapshot.summary_counts` (0a-7) |
| GraphNeighborhoodCounts | `record { center_label: String, in_links: u32, out_links: u32, note_count: u64, depth: u32 }` | exported on `GraphNeighborhood.summary_counts` (0a-7) |
| GraphPresetOutcome | `enum { Orphans { count: u64 }, Unresolved { count: u64 }, MostLinked { label: String, in_links: u32 }, NoNotesToRank }` | each arm carries only its fields |
| GraphForceControl | `enum { Center, Repel, Link, LinkDistance }` | |
| GraphSurfaceMode | `enum { Table, Diagram }` | named to avoid mac's local `GraphTabMode` (`GraphTableView.swift:9`), which 0a-18 deletes in its favour |
| GraphWhereAmISelection | `enum { Node { row: GraphRowCopy, component: u32 }, NoSelection }` | `NoSelection`, not `None` — the generated Swift `.none` collision the canvas's `Unstated` avoids (`lib.rs:7591`) |
| GraphStatusNote | `enum { Opened, AlreadyOpen, ConnectionsPanel, NoteCreated { name: String }, NoConnections, LoadingConnections }` | |
| GraphBlockedReason | `enum { LoadFailed { message: String }, ConnectionsLoadFailed { message: String }, NoteCreateFailed { message: String } }` | |

The seventeen variants of `GraphA11yEvent`. `Pri` `Medium` unless
**High**; `Class`: `nav` / `filter` / `force` / `settle` / `—`
(immediate). `⟨⟩` marks substitution; counts render as bare integers
except where "grouped" is written (0a-16).

| Variant | Fields (uniffi) | Template | Pri | Class |
|---|---|---|---|---|
| `GraphRow` | `verbosity: GraphVerbosity, row: GraphRowCopy` | Terse: `⟨label⟩`. Standard/Verbose: kind Note or Attachment → `⟨label⟩, ⟨in_links⟩ links in, ⟨out_links⟩ links out`; kind Ghost → `⟨label⟩, unresolved, ⟨references⟩ references`; then `, embed` when `embed` | | nav |
| `GraphReRooted` | `label: String` | `Connections: ⟨label⟩` | | — |
| GraphSnapshotSummary | `counts: GraphSnapshotCounts` | `⟨notes⟩ notes, ⟨links⟩ links.` + (when orphans > 0 or unresolved > 0) ` ⟨orphans⟩ orphans, ⟨unresolved⟩ unresolved targets.` + (when filtered) ` Filtered.` — all four counts grouped | | — |
| GraphNeighborhoodSummary | `counts: GraphNeighborhoodCounts` | `⟨center_label⟩: ⟨in_links⟩ links in, ⟨out_links⟩ links out. Showing ⟨note_count⟩ notes within ⟨depth⟩ links.` — the three counts grouped, depth bare | | — |
| `GraphPreset` | `outcome: GraphPresetOutcome` | Orphans → `⟨count⟩ orphaned notes.`; Unresolved → `⟨count⟩ unresolved targets.`; MostLinked → `Most linked: ⟨label⟩, ⟨in_links⟩ links in.`; NoNotesToRank → `No notes to rank.` | | — |
| GraphFilterCount | `shown: u32, total: u32` | `⟨shown⟩ of ⟨total⟩ shown` | | filter |
| GraphForceValue | `control: GraphForceControl, percent: u32` | `Center force ⟨percent⟩ percent` ‖ `Repel force ⟨percent⟩ percent` ‖ `Link force ⟨percent⟩ percent` ‖ `Link distance ⟨percent⟩ percent` | | force |
| GraphLayoutSettled | — | `Graph layout settled.` | | settle |
| `GraphPinned` | `pinned: bool` | `Pinned.` ‖ `Unpinned.` | | — |
| `GraphZoom` | `fit: bool, percent: u32` | `Zoom ⟨percent⟩ percent.` ‖ `Fit graph. Zoom ⟨percent⟩ percent.` | | — |
| `GraphMode` | `mode: GraphSurfaceMode` | `Table mode.` ‖ `Diagram mode.` | | — |
| `GraphWhereAmI` | `selection: GraphWhereAmISelection, zoom_percent: u32, orphans_only: bool, attachments_shown: bool, ghosts_shown: bool, name_filter: Option<String>, unresolved_only: bool` | parts joined by `, ` then `.`: Node → the row copy at Standard grade, `component ⟨component⟩`; NoSelection → `No node selected`; then `zoom ⟨zoom_percent⟩ percent`; then `filters: ` + the active of `orphans only`, `attachments shown`, and `unresolved shown` ‖ `unresolved hidden` joined by `, `; then (when `name_filter` is Some and non-empty after trimming) `name filter “⟨needle⟩”`; then (when `unresolved_only`) `unresolved only` | | — |
| GraphTierEntered | — | `Large graph: summary accessibility mode. Table mode has every node.` | | — |
| GraphTierSummary | `count: u32` | `⟨count⟩ nodes — too many for per-node navigation. Switch to Table mode for the full, navigable list.` — LABEL; count bare | | — |
| GraphNeighborsContent | `labels: Vec<String>, more: u32` | `⟨labels joined by ", "⟩` + (when more > 0) ` and ⟨more⟩ more`; empty labels → the empty string — LABEL (content title `Connects to`) | | — |
| `GraphStatus` | `note: GraphStatusNote` | Opened `Graph.` · AlreadyOpen `The graph is already open.` · ConnectionsPanel `Connections panel.` · NoteCreated `Created note ⟨name⟩.` · NoConnections `This note has no connections.` · LoadingConnections `Loading connections.` | | — |
| `GraphBlocked` | `reason: GraphBlockedReason` | LoadFailed `Couldn't load the graph: ⟨message⟩` · ConnectionsLoadFailed `Couldn't load connections: ⟨message⟩` · NoteCreateFailed `Couldn't create note: ⟨message⟩` | **High** | — |

**0a-3 — The tripwires are canvas-specific today and are generalised
here, by name, to what they can actually see.** (i) Core's inventory
becomes path-aware: `GRAPH_NESTED_ENUMS: &[(&str /*enum*/, &str
/*source path*/, &[(&str, &str)] /*sites*/)]` lists the seven a11y
enums with `src/a11y.rs` and `NodeKind` with `src/graph.rs`, and
`declared_variants_in(path, enum)` replaces `declared_variants` for
both inventories; `every_graph_variant_and_arm_is_represented_in_the_corpus`
and `every_graph_parameter_enum_is_listed_for_coverage` are the canvas
tests' twins; `graph_variant_of` beside `canvas_variant_of`. (ii)
uniffi's `the_ffi_mirror_covers_every_core_a11y_variant` reads BOTH
inventories (today one constant, `lib.rs:11668–11681`), opens the named
source file per entry, compares core and mirror arm sets both ways for
every family, asserts each inventory's family count, and pins the
graph's seven arm counts exactly; `GraphNodeKind`'s two conversion
directions are both required to exist (a compile fact). (iii)
`the_windows_corpus_mirror_lists_every_event_in_order` matches
`new CanvasA11yEvent.` today (`lib.rs:11222`) and `new GraphA11yEvent.`
after; (iv) `the_mac_corpus_mirror_lists_every_event_in_order` likewise
for `.graph(event:`; both are family-qualified so a canvas entry cannot
satisfy a graph position. (v) The coalescing switch-list tests: core's
ONE list gains the graph's classes (0a-9), `pinned_coalescing_classes`
becomes family-qualified (a `CanvasA11yEvent::` or `GraphA11yEvent::`
marker names its family), and the mac test opens `GraphAnnouncer.swift`
beside `CanvasAnnouncer.swift` and asserts the graph's four classes;
the Windows test asserts the canvas file in 0a and gains
`GraphAnnouncer.cs` in PR A — **0a makes no claim that a forgotten
Windows graph switch fails before PR A exists.** After this PR, a
forgotten core arm, FFI arm, mac or Windows corpus entry, or mac class
fails in `cargo test`; the Windows class fails from PR A.

**0a-4 — Completeness is asserted, not assumed.**
`every_graph_variant_and_arm_is_represented_in_the_corpus` fails when a
graph variant — or an arm of any inventory entry, `NodeKind` at its
`GraphRowCopy.kind` sites included — never reaches `corpus()`; its
companion asserts the inventory lists exactly the `Graph*` a11y enums
this module declares plus the one external entry by name.

**0a-5 — Verbosity is a parameter on exactly one family, and this PR
is default-only on both hosts.** `GraphVerbosity` is core, separate
from `CanvasVerbosity` (0aD-2); it is carried by `GraphRow` and by
nothing else. Core stays pure. Neither host persists the level in this
PR: mac never did (the `GraphVerbosity` enum is set from nowhere;
`GraphConfig` has no field; P2-4's v1 schema has none) and passes
`.standard`; 0b adds the `verbosity` key to the config schema and PR C
gives Windows its menu. The generated `GraphVerbosity` gets its Swift
extension in THIS PR — `Codable` with the persistence tags `terse` /
`standard` / `verbose`, explicit `allCases`, `title` — the canvas
extension's shape (`CanvasAnnouncer.swift:21–67`), so 0b's store has a
type to write. `graph_verbosity_matrix_pins_every_level` pins the nine
(level × Note/Ghost/embed) renderings and asserts structurally that no
other variant carries the parameter. The mac gap (no switch, no store)
is recorded in the mac-details register and filed at close-out.

**0a-6 — Where-am-I always renders the full row copy, a CORRECTION of
shipped mac behaviour.** `GraphWhereAmI` takes no verbosity; its `Node`
selection renders `GraphRowCopy` at Standard grade. Mac composes it
from `rowPhrase` (`AppState+GraphDiagram.swift:256`), which collapses
to the bare label at terse, so a terse reader asking "Where am I?"
hears less than P2-3 promises ("node copy + component + zoom + active
filters"). Task 0a-2 moves mac onto this event, so mac's behaviour
changes — recorded as 0a-D1, authorised by P2-3's own text and the t0
§1.4 rule the canvas keeps. Every state — selected and not, each filter
combination, an empty and a non-empty needle, the curly quotes, the
trailing period — is pinned by the matrix and by
`graph_where_am_i_renders_every_state_exactly`.

**0a-7 — The two summaries are typed events over exported count
records, rendered by the ONE formatter core owns; the counts' semantics
are the session's, named and independently asserted.**
`crates/slate-core/src/graph_summary.rs` — `pub fn snapshot_summary(&GraphSnapshotCounts) -> String`
and `pub fn neighborhood_summary(&GraphNeighborhoodCounts) -> String`,
P0-3's formats with `graph::grouped_decimal` — is called by
`session.rs` to fill `audio_summary` and by `a11y.rs` to render the
events. Semantics: `notes` = nodes of kind Note in the FILTERED
payload; `links` = the sum of `count` over every edge among the
filtered nodes, **Link and Embed alike** (`session.rs:9032`);
`orphans` = filtered nodes with `is_orphan`; `unresolved` = filtered
nodes of kind Ghost; `filtered` = the filter deviates from
`{attachments: false, ghosts: true, orphans_only: false}`;
`center_label` = the centre's label; `in_links`/`out_links` = the
centre's FULL-GRAPH `NodeMetrics` degrees — **Link edges only, embeds
excluded, unaffected by the requested depth or the active filter**
(`session.rs:1980, 1993–1994`); `note_count` = Note-kind nodes in the
neighbourhood payload, the centre included only when it is a note;
`depth` = the clamped depth. Two tests: `summary_counts_are_the_index_counts`
asserts every exported field independently over a fixture with a
multi-count edge, an embed edge, a filtered-out neighbour, a link
beyond the requested depth and an attachment centre; and
`summary_events_render_the_snapshot_fields_verbatim` asserts the event
rendered from the record equals the record's `audio_summary` byte for
byte (the formatter-consistency check, which cannot stand alone).
Hosts construct the events from `summary_counts` and count nothing
(R-D).

**0a-8 — Priorities are mac's `.error` case, listed explicitly.**
`GraphBlocked` is `High`; every other graph event is `Medium`,
Where-am-I included (mac's comment at `AppState+GraphDiagram.swift:242`
says "assertively"; the code posts `.summary` at medium; the code is
the shipped behaviour and the comment is deleted in 0a-2). Pinned both
ways by `graph_priorities_pin_the_error_tier`.

**0a-9 — Coalescing stays host-side; the class keys do not.** The
classes are pinned in `a11y.rs`'s "Coalescing class keys" list, family-
qualified: **`navigation`** = `GraphA11yEvent::GraphRow`; **`filter`** =
GraphFilterCount; **`forceValue`** = GraphForceValue; **`settle`** =
GraphLayoutSettled; everything else immediate; a `High` graph event —
announced or relayed — flushes **and drops** every pending class.
200 ms latest-wins, each class independent (`GraphAnnouncer.swift:74–84,
185–218`). Host rules that are not classes: the filter count's
**fire-time gate** (`:123–133, 204–207`) and `cancelPending` on view
departure or vault change (`:224–226`). Task 0a-2's mac tests cover:
latest-wins within each of the four classes, independence across all
six pairs, the 200 ms window, the gate, the cancel, and a High event
(announced and relayed) flushing all four; PR A's Windows announcer
repeats the suite.

**0a-10 — No chord-bearing template.** No graph template carries a host
chord; the tier copy names "Table mode" by its label.

**0a-11 — The row copy is one helper, spoken and labelled alike.**
`a11y.rs::graph_row_copy(&GraphRowCopy) -> String` renders P1's
template (Standard grade) and is used by `GraphRow`'s render (after the
terse check) and by `GraphWhereAmI`'s `Node` clause. Manifest rows L1–L2
render `GraphRow` through `a11yRender`; `rowPhrase` and `GraphRowRef`
are deleted.

**0a-12 — The site manifest is the inventory.** One row per string
site, with its role: `posted` / `static label` / `custom content` /
`canonical relay` / `composed label` (a label the host assembles from
data — recorded so both hosts compose it identically, §W-C). A site the
manifest lacks is a round finding.

| # | Site | Current API / string | Role | Event or class | Pin |
|---|---|---|---|---|---|
| S1 | `AppState.swift:3236` | `.status("The graph is already open.")` | posted | `GraphStatus{AlreadyOpen}` | golden |
| S2 | `AppState+Connections.swift:114` | `.summary(hood.audioSummary)` | posted | GraphNeighborhoodSummary | 0a-7 tests |
| S3 | `AppState+Connections.swift:123` | `.error("Couldn't load connections: …")` | posted | `GraphBlocked{ConnectionsLoadFailed}` | golden |
| S4 | `AppState+Connections.swift:168` | `.status("Connections panel.")` | posted | `GraphStatus{ConnectionsPanel}` | golden |
| S5 | `AppState+Connections.swift:212` | `.reRooted(label:)` | posted | `GraphReRooted` | golden |
| S6 | `AppState+Connections.swift:236` | `.reRooted(label:)` | posted | `GraphReRooted` | golden |
| S7 | `AppState+Connections.swift:287` | `.status("Created note ….")` | posted | `GraphStatus{NoteCreated}` | golden |
| S8 | `AppState+Connections.swift:294` | `.error("Couldn't create note: …")` | posted | `GraphBlocked{NoteCreateFailed}` | golden |
| S9 | `AppState+GraphConfig.swift:131` (`:139–150`) | `announceForceValue(phrase)` | posted (force) | GraphForceValue | golden ×4 |
| S10 | `AppState+GraphDiagram.swift:177` | `announceSettle("Graph layout settled.")` | posted (settle) | GraphLayoutSettled | golden |
| S11 | `AppState+GraphDiagram.swift:197` | `.rowFocused(ref)` | posted (navigation) | `GraphRow` | matrix |
| S12 | `AppState+GraphDiagram.swift:231` | `.status("Unpinned.")` | posted | `GraphPinned{false}` | golden |
| S13 | `AppState+GraphDiagram.swift:235` | `.status("Pinned.")` | posted | `GraphPinned{true}` | golden |
| S14 | `AppState+GraphDiagram.swift:245` (`:252–267`, `:359–365`) | `.summary(whereAmIText)` | posted | `GraphWhereAmI` | 0a-6 test |
| S15 | `AppState+GraphDiagram.swift:346` | `.status("Zoom … percent.")` | posted | `GraphZoom{fit:false}` | golden |
| S16 | `AppState+GraphDiagram.swift:355` | `.status("Fit graph. Zoom … percent.")` | posted | `GraphZoom{fit:true}` | golden |
| S17 | `AppState+GraphTable.swift:57` | `.status("Graph.")` | posted | `GraphStatus{Opened}` | golden |
| S18 | `AppState+GraphTable.swift:197` (`:336–354`) | `.status(graphPresetAnnouncement(…))` | posted | `GraphPreset` | golden ×4 |
| S19 | `AppState+GraphTable.swift:202` | `.summary(snap.audioSummary)` | posted | GraphSnapshotSummary | 0a-7 tests |
| S20 | `AppState+GraphTable.swift:204` (`:225–235`) | `announceFilterCount(…)` | posted (filter) | GraphFilterCount | golden |
| S21 | `AppState+GraphTable.swift:215` | `.error("Couldn't load the graph: …")` | posted | `GraphBlocked{LoadFailed}` | golden |
| S22 | `GraphDiagramView.swift:496–498` | `.status("Large graph: …")` | posted | GraphTierEntered | golden |
| S23 | `GraphTableView.swift:100` | `.status("Diagram mode.")` | posted | `GraphMode{Diagram}` | golden |
| S24 | `GraphTableView.swift:103` | `.status("Table mode.")` | posted | `GraphMode{Table}` | golden |
| S25 | `GraphTableView.swift:340–342` | `announceFilterCount("… shown", gate:)` | posted (filter) | GraphFilterCount | golden |
| S26 | `GraphTableView.swift:361` | `.status(a11yRender(event).text)` | canonical relay (priority dropped today) | `relay(A11yEvent)` (0a-13) | mac test |
| L1 | `ConnectionsPanel.swift:239` | `.accessibilityLabel(rowPhrase(row.rowRef))` | static label (AX name) | `GraphRow` rendered | matrix |
| L2 | `GraphDiagramView.swift:766, 841–843` | `setAccessibilityLabel(axLabel(…))` | static label (AX name) | `GraphRow` rendered | matrix |
| C1 | `GraphDiagramView.swift:851–869` | `AXCustomContent(label: "Connects to", value:)` | custom content | GraphNeighborsContent | golden ×4 |
| C2 | `GraphDiagramView.swift:627–629` | the tier summary element's label | static label | GraphTierSummary | golden ×2 |
| C3 | `GraphDiagramView.swift:631` | custom action `Switch to Table` | static label | label inventory | §W-C |
| C4 | `GraphDiagramView.swift:186` | `Graph, visual diagram` | static label | label inventory | §W-C |
| C5 | `GraphDiagramView.swift:791, 803, 811` | node help text; custom actions from `GraphRowAction.title`; `Pin` / `Unpin` | static label | label inventory (the action titles are 0b's `graph_row_actions`) | §W-C |
| T1 | `ConnectionsPanel.swift:93–104` | depth picker `Links` / `2 links away` / `3 links away`; `Local graph depth`; hint `How many links away from this note to include.` | static label | label inventory | §W-C |
| T2 | `ConnectionsPanel.swift:121` | `Connections error: ⟨e⟩` | static label (the posted form is S3) | label inventory | §W-C |
| T3 | `ConnectionsPanel.swift:125, 130` | `This note has no connections.` | static label on mac; **posted on Windows** (0a-D3) | `GraphStatus{NoConnections}` | golden |
| T4 | `ConnectionsPanel.swift:137, 144` | `Loading connections…` visible / `Loading connections.` AX | static label on mac; **posted on Windows** (0a-D3) | `GraphStatus{LoadingConnections}` (the AX form) | golden |
| T5 | `ConnectionsPanel.swift:179, 188` | section header `⟨title⟩, ⟨n⟩ note`/`notes` (host-pluralised) and the empty-section label | composed label | label inventory — the plural rule recorded for PR B | §W-C |
| T6 | `ConnectionsPanel.swift:240–243, 260–261` | row hint/help; action hints (`disabledReason ?? action.title`) | static label | label inventory | §W-C |
| T7 | `ConnectionsPanel.swift:301–309` | badges `Unresolved` / `Embed` / `Attachment` | static label | label inventory | §W-C |
| T8 | `GraphTableView.swift:129, 133–137, 297–309` | `Graph diagram error: ⟨e⟩`; `Laying out graph…` / `Laying out graph.`; `Loading graph…` / `Loading graph.`; `Graph error: ⟨e⟩` | static label | label inventory (the posted error is S21) | §W-C |
| T9 | `GraphTableView.swift:145–153` | picker `View`; `Graph view mode`; hint `Switch between the accessible table and the visual diagram.` | static label | label inventory | §W-C |
| T10 | `GraphTableView.swift:164–172`; `GraphInspectorView.swift:34–40` | `Filter graph by note name`; toggles `Attachments` / `Unresolved` / `Orphans only` with hints `Include attachment nodes.` / `Include unresolved link targets.` / `Show only notes with no links in or out.` | static label | label inventory (two copies on mac; one inventory row) | §W-C |
| T11 | `GraphTableView.swift:181–182` | `Toggle graph inspector`; help `Show the graph inspector — filters, colour groups, display, and forces.` | static label | label inventory | §W-C |
| T12 | `GraphTableView.swift:351, 614–622, 482–484` | `Graph, data grid`; the column titles; kind labels `Note` / `Attachment` / `Unresolved` | static label | label inventory | §W-C |
| T13 | `GraphInspectorView.swift:26, 69, 81, 88–113, 132–133` | `Graph inspector`; `No groups. Add one to colour matching nodes.`; add-group hint; `Group ⟨n⟩ query` / `colour` / `ring style`; `Remove group ⟨n⟩`; `Arrows` + hint | composed and static labels | label inventory (PR E) | §W-C |
| T14 | `GraphInspectorView.swift:134–201` | seven `labeledSlider`s: title, value `%.2f`, hint | static label + value | label inventory (PR E); the spoken change is S9 | §W-C |

**0a-13 — The one already-canonical site becomes a relay that keeps
the priority, on one seam with the typed announcements — the canvas
announcer's shape.** Task 0a-2 pins mac's API to `CanvasAnnouncer.swift:105–160`:
`announce(_ event: GraphA11yEvent)` → `emit(.graph(event: event),
coalescing: Self.coalescingClass(of: event))`; `relay(_ event: A11yEvent)`
→ `emit(event, coalescing: nil)`; the private `emit(_ event: A11yEvent,
coalescing: EventClass?)` renders through `a11yRender`, stores the
RENDERED `(text, priority)` for a coalesced class, and posts through
`AppKitAnnouncementPoster().post(text, priority:)` — never the string
primitive, never `.hostComposed`; a High render, coalesced or not,
flushes and drops every pending class before posting. `GraphEvent` is
deleted; `GraphRowRef` is replaced by the generated `GraphRowCopy`;
the three text-taking posters become `announce` calls on their events.

**0a-14 — Label-grade events are marked as such; "never spoken" is the
wrong word.** Two events are LABEL class — **not actively posted**;
assistive technology reads them as an element's name or content:
GraphTierSummary (C2) and GraphNeighborsContent (C1). Two are
dual-use by recorded divergence (0a-D3): `GraphStatus{NoConnections}`
and `GraphStatus{LoadingConnections}` are static labels on mac (T3, T4)
and posted on Windows when the leaf takes focus in that state. Every
other label in the manifest (C3–C5, T1–T2, T5–T14) is §W-C label class,
carried by the inventory of the PR that lands the surface, and is NOT
in this vocabulary.

**0a-15 — Every count slot renders at every reachable cardinality, and
every preserved plural defect is a (variant, slot) pair.** The
classifier `graph_count_slots` lists every count-bearing slot of the
schema with its reachable cardinalities and the shipped noun form; the
matrix carries a witness for each reachable cell; the copy rule
preserves the shipped fixed plurals, so a slot whose singular reads as
a defect is allow-listed by (variant, slot). Uniqueness is asserted per
slot, not per fragment: the same fragment `1 links in` renders from
four slots, each its own pair. "and 1 more" is grammatical and is not
a defect.

| Variant · slot | Reachable | Shipped form at one | Defect? |
|---|---|---|---|
| `GraphRow` · in_links / out_links | 0, 1, many | `1 links in` / `1 links out` | yes, both |
| `GraphRow` · references (Ghost) | 0, 1, many | `1 references` | yes |
| GraphSnapshotSummary · notes | 0, 1, many | `1 notes` | yes |
| GraphSnapshotSummary · links | 0, 1, many | `1 links` | yes |
| GraphSnapshotSummary · orphans / unresolved | 0 (the pair omitted when both are 0), 1, many | `1 orphans` / `1 unresolved targets` | yes, both |
| GraphNeighborhoodSummary · in_links / out_links | 0, 1, many | `1 links in` / `1 links out` | yes, both |
| GraphNeighborhoodSummary · note_count | 0, 1, many | `1 notes` | yes |
| GraphNeighborhoodSummary · depth | 1, 2, 3 (clamped) | `within 1 links` | yes |
| `GraphPreset` · Orphans.count / Unresolved.count | 0, 1, many | `1 orphaned notes` / `1 unresolved targets` | yes, both |
| `GraphPreset` · MostLinked.in_links | 0, 1, many | `1 links in` | yes |
| GraphFilterCount · shown / total | shown 0, 1, many; total 0 (empty graph), 1, many | — (no noun) | no |
| GraphForceValue · percent | 0…100 | — | no |
| `GraphZoom` · percent | positive | — | no |
| `GraphWhereAmI` · component | 0, many | — | no |
| GraphTierSummary · count | many only (the tier begins above core's threshold) | `⟨n⟩ nodes` | 0 and 1 unreachable, declared |
| GraphNeighborsContent · labels | 0 (empty string), 1, many | — | no |
| GraphNeighborsContent · more | 0 (clause omitted), 1, many | `and 1 more` | no — grammatical |

`graph_count_slots_render_at_every_reachable_cardinality` walks the
table over the matrix; `graph_plural_defects_are_exactly_these_slots`
asserts the allow-list is exactly the "yes" rows and that each renders
from its slot. `session/tests/graph.rs:780, 914` already pin two of the
summary forms as shipped.

**0a-16 — The shipped string is the copy authority where the P specs
differ, and each difference is recorded; only P0-3's summaries group
their counts.** P2 writes the tier message with a semicolon
(`p2_spec.md:109`); mac shipped two sentences with a period
(`GraphDiagramView.swift:498`) — the shipped form moves (0a-D4). P2
writes `"Repel force 0.7; layout settling… settled."` (`p2_spec.md:127`);
mac shipped `"Repel force 70 percent"` and a separate `"Graph layout
settled."` — both move as two events in that order (0a-D5). Grouped
decimals (`1,032`) appear only where `snapshot_audio_summary` and the
neighbourhood string use `grouped_decimal` — the five summary counts;
every other count — degrees in the row copy, the tier count
(`visibleIDs.count` interpolated bare, `GraphDiagramView.swift:628`),
presets, filter counts, percents — renders bare, and the matrix's
witnesses at 1,024 and 2000 pin the two behaviours.

**0a-17 — The witness matrix is ordered and pinned.** Sixty-one
entries, appended in this order after `CanvasStatus { Loading }`; the
artifact total becomes 509.

| Variant | Witnesses (in order) |
|---|---|
| `GraphRow` (9) | Standard Note "Alpha" 3/1 · Standard Ghost "Missing Note" references 2 · Standard Note "Pic" 1/0 embed · Terse Note "Alpha" · Verbose Note "Hub" 1024/0 (bare) · Standard Attachment "diagram.png" 0/2 · Standard Ghost "Draft" references 0 · Standard Ghost "Todo" references 1 · Standard Note "Alone" 0/0 |
| `GraphReRooted` (1) | "Alpha" |
| GraphSnapshotSummary (5) | 247 / 1,032 / 12 / 3 · 1 / 1 / 0 / 0 (second sentence omitted) · 0 / 0 / 0 / 0 · 40 / 60 / 1 / 1, filtered · 5 / 9 / 0 / 2 |
| GraphNeighborhoodSummary (4) | "Alpha" 3/1, 7 notes, depth 1 · "Hub" 1,032/4, 1,206 notes, depth 3 · "diagram.png" 0/0, 0 notes, depth 2 · "Solo" 1/1, 1 notes, depth 1 |
| `GraphPreset` (8) | Orphans 12 · Orphans 1 · Orphans 0 · Unresolved 1 · Unresolved 12 · MostLinked "Hub" 1032 · MostLinked "Only" 0 · NoNotesToRank |
| GraphFilterCount (3) | 3 of 247 · 1 of 1 · 0 of 0 |
| GraphForceValue (4) | Center 50 · Repel 70 · Link 100 · LinkDistance 0 |
| GraphLayoutSettled (1) | — |
| `GraphPinned` (2) | true · false |
| `GraphZoom` (2) | 125 plain · 63 fit |
| `GraphMode` (2) | Table · Diagram |
| `GraphWhereAmI` (4) | Node "Alpha" 3/1 component 2, zoom 100, orphans only + attachments shown + unresolved shown, needle "alp", unresolved only · Node Ghost "Missing Note" component 0, zoom 250, unresolved hidden, needle None · NoSelection, zoom 100, unresolved shown, needle Some("   ") (trimmed empty → omitted) · Node Attachment "diagram.png" 0/2 component 7, zoom 50, attachments shown + unresolved hidden |
| GraphTierEntered (1) | — |
| GraphTierSummary (2) | 2000 · 1501 |
| GraphNeighborsContent (4) | ten labels, more 0 · three labels, more 1 · ten labels, more 42 · labels [] |
| `GraphStatus` (6) | Opened · AlreadyOpen · ConnectionsPanel · NoteCreated "Draft.md" · NoConnections · LoadingConnections |
| `GraphBlocked` (3) | LoadFailed "io error" · ConnectionsLoadFailed "io error" · NoteCreateFailed "exists" |

9 + 1 + 5 + 4 + 8 + 3 + 4 + 1 + 2 + 2 + 2 + 4 + 1 + 2 + 4 + 6 + 3 = **61**.
The artifact total becomes **509**, and the two figures in 0a-2 read
61 and 509 — this table is the authority, the arithmetic is shown, and
`the_graph_family_witness_count_is_pinned` asserts 61.

**0a-19 — The matrix is executable, not descriptive.**
`the_graph_witnesses_are_exactly_the_matrix_in_order` asserts the graph
entries of `corpus()` are exactly the 61 identities above in that
order, each with its key payload markers (the variant, and for each
witness the discriminating field values named in the table); deleting,
substituting or reordering a witness fails here, not only in the golden
and the artifact (the E13 precedent, `file_type_not_openable_has_exactly_its_two_ordered_witnesses`).
Mutation-verified in the record.

**0a-18 — Mac consumes in the same PR (Task 0a-2); every migration is
owned.** Every S-row's composition is replaced by `announce` /
`relay`; L1–L2 render `GraphRow`; C1–C2 render their events; S20 and S25
collapse to one event; `GraphEvent`, `GraphRowRef`, `rowPhrase`,
`forcesChangePhrase`, `graphPresetAnnouncement`, `graphFilterCountText`,
`graphDiagramWhereAmIText`, `graphDiagramFilterPhrase` are deleted.
Generated-type migrations: the seven Swift fixtures that construct the
records gain `summaryCounts:` (`GraphCommandsTests.swift:70`;
`ConnectionsPanelTests.swift:78, 111, 133, 153, 166, 176`); the local
`GraphTabMode` (`GraphTableView.swift:9`) is deleted for the generated
GraphSurfaceMode, with a Swift extension supplying `CaseIterable`,
`title`, and the persistence tags `GraphConfigStore.swift:162` used
through `rawValue` (an exhaustive mapping, the canvas extension's
shape); the generated `GraphVerbosity` gets the extension of 0a-5. The
mac tests: GraphAnnouncerTests's copy assertions (`:25–51`),
`GraphCommandsTests.testPresetAnnouncementStringsAreVerbatim` (`:76`)
and `GraphConfigTests.testForcesChangePhraseNamesTheOneChangedControl`
(`:97`) move to the Rust golden; GraphAnnouncerTests keeps the 0a-9
suite and the funnel guard; GraphDiagramTests' Where-am-I cases
(`:357–393`) assert the exact corrected sentence.
`A11yResidueCensusTests.pinnedResidueSites` reads **28**; `a11y.rs:14–38`'s
module doc is rewritten so only the structural-mutation builder remains
a named host-composed exception, pinned by
`the_module_doc_names_no_engine_but_the_mutation_builder`. The Swift
edit is unrun on this box (0aR-1).

### Decisions

- **0aD-1 — The summaries are typed events over exported count records,
  superseding the spec's "relayed, not re-rendered" sentence.** The
  spec's §4 PR 0a is amended in this PR to this schema.
- **0aD-2 — `GraphVerbosity` is its own enum.** Not an alias of
  `CanvasVerbosity`: P intends the graph's level in the vault file and
  the canvas's on the device.
- **0aD-3 — The filter count's fire-time gate and `cancelPending` are
  host rules, not events.**
- **0aD-4 — A row's kind reuses `GraphNodeKind`**, with the reverse
  mapping added; no second kind enum.
- **0aD-5 — The shipped string is the copy authority; the P specs'
  variants are recorded, not adopted** (0a-16).
- **0aD-6 — Verbosity persistence is 0b's and PR C's, not 0a's.** 0a
  lands the parameter and the Swift extension; the schema key and the
  menu follow; mac's missing switch is a filed gap.

### Recorded divergences (owner-recorded; off-limits for re-litigation)

- **0a-D1 — Where-am-I speaks the full row copy at every verbosity, on
  both hosts** — a correction of mac's terse coupling (0a-6).
- **0a-D2 — The grid relay keeps core's priority** (0a-13).
- **0a-D3 — The Connections panel's empty and loading states are
  posted on Windows when the leaf takes focus in them**, static labels
  on mac (T3, T4); PR B pins Windows's.
- **0a-D4 — The tier message keeps mac's two sentences**, not P2's
  semicolon (`p2_spec.md:109`).
- **0a-D5 — The force value and the settle are two events with mac's
  wording**, not P2's single phrase (`p2_spec.md:127`).

### Mac details recorded while reading (not this issue's to fix)

- **The graph verbosity has no switch and no store on mac.**
  `GraphVerbosity` is declared and defaulted (`GraphAnnouncer.swift:7–22,
  95`), `GraphConfig` carries no field, and no menu sets it; P2-4's
  "persisted alongside the other graph settings" never landed. Filed at
  close-out; 0b adds the key, PR C the Windows menu.

### Accepted risks

- **0aR-1 — The Swift migration is unrun on this box.** Mac's CI lane
  arbitrates; a red mac lane is fixed here, not waived.
- **0aR-2 — The uniffi budget.** `A11yEvent` moves to 199 of 256.

### Round 1 — sixteen findings, dispositions

| Finding | Severity | Disposition |
|---|---|---|
| IG0a-1 | BLOCKER | taken — typed summaries over exported count records, the formatter named, the spec amended (0a-7, 0aD-1) |
| IG0a-2 | BLOCKER | taken — 0a-2b is a literal schema with every field and width; GraphPresetOutcome and GraphWhereAmISelection close the invalid combinations; `GraphNodeKind` reused |
| IG0a-3 | BLOCKER | taken — 0a-3 names every parser and what each can see after this PR |
| IG0a-4 | BLOCKER | taken — 0a-16, 0aD-5, 0a-D4, 0a-D5 |
| IG0a-5 | MAJOR | taken — 0a-15's slot table with reachability |
| IG0a-6 | MAJOR | taken — "not actively posted"; the manifest's role column |
| IG0a-7 | MAJOR | taken — 0a-7's semantics and two tests |
| IG0a-8 | MAJOR | taken — 0a-13 pins the mac API to the canvas announcer's shape |
| IG0a-9 | MAJOR | taken — 0a-9's suite on both hosts |
| IG0a-10 | MAJOR | taken — 0a-17's matrix, 0a-19's executable form |
| IG0a-11 | MAJOR | taken — 0a-6 a correction, pinned exactly |
| IG0a-12 | MAJOR | taken — 0a-12's manifest |
| IG0a-13 | MINOR | taken — 198 → 199, pinned |
| IG0a-14 | MINOR | taken — both test names corrected in the contracts and the spec |
| IG0a-15 | MINOR | taken — the module-doc rewrite with a scan (0a-18) |
| IG0a-16 | MINOR | taken — Medium pinned, the comment deleted (0a-8) |

### Round 2 — twelve findings, dispositions

| Finding | Severity | Disposition |
|---|---|---|
| IG0a-17 | BLOCKER | taken — 0a-2b's two literal tables; the spec's §4 amended to this schema (five places, one GraphForceValue, no GraphFilterPhrase, typed summaries, the Where-am-I policy, PR E's consumption) |
| IG0a-18 | BLOCKER | taken — the matrix recounted with the arithmetic shown (61, 509), an Attachment witness added, the two figures in 0a-2 corrected |
| IG0a-20 | BLOCKER | taken — a path-aware inventory naming `graph.rs` for `NodeKind`; both conversion directions required; the Windows coalescing assertion explicitly deferred to PR A, the false claim removed |
| IG0a-19 | MAJOR | taken — 0a-19's exact-ordered-witness test |
| IG0a-21 | MAJOR | taken — 0a-15 classifies by (variant, slot) with reachability; "and 1 more" struck; the matrix carries the boundaries |
| IG0a-22 | MAJOR | taken — 0a-7 names Link+Embed for `links`, full-graph Link-only degrees for the neighbourhood, and adds the independent field test |
| IG0a-23 | MAJOR | taken — 0a-18 owns the seven fixtures, deletes the local `GraphTabMode` for GraphSurfaceMode, and specifies the persistence extension |
| IG0a-24 | MAJOR | taken — the manifest gains C4–C5 and T8–T14; T5's composed plural recorded for PR B |
| IG0a-25 | MAJOR | taken — 0a-5 and 0aD-6: default-only in 0a on both hosts, the key in 0b, the Swift extension here; the false "already writes" claim removed; the mac gap recorded |
| IG0a-26 | MAJOR | taken — 0a-13 is the canvas-compatible shape: rendered `(text, priority)` pending, AppKitAnnouncementPoster |
| IG0a-27 | MAJOR | taken — the tier count renders bare (0a-16); the witness is 2000 |
| IG0a-28 | MINOR | taken — `NoSelection` |

### Tests that pin PR 0a

`crates/slate-core/src/a11y.rs`: `corpus_renders_the_shipped_strings`
(61 golden rows), `committed_corpus_artifact_matches_the_vocabulary`
(509), `every_graph_variant_and_arm_is_represented_in_the_corpus`,
`every_graph_parameter_enum_is_listed_for_coverage`,
`the_graph_family_occupies_one_top_level_variant`,
`a11y_event_top_level_count_is_pinned`,
`graph_verbosity_matrix_pins_every_level`,
`graph_where_am_i_renders_every_state_exactly`,
`graph_priorities_pin_the_error_tier`,
`graph_count_slots_render_at_every_reachable_cardinality`,
`graph_plural_defects_are_exactly_these_slots`,
`the_graph_family_witness_count_is_pinned`,
`the_graph_witnesses_are_exactly_the_matrix_in_order`,
`the_module_doc_names_no_engine_but_the_mutation_builder`.
`crates/slate-core/src/graph_summary.rs` and `session.rs`:
`summary_counts_are_the_index_counts`,
`summary_events_render_the_snapshot_fields_verbatim`.
`crates/slate-uniffi/src/lib.rs`: the generalised
`the_ffi_mirror_covers_every_core_a11y_variant`, the two family-
qualified corpus-order tests, the two coalescing switch-list tests
(mac over both announcer files; Windows over the canvas file until PR
A). `apps/slate-windows/.../A11yCorpusCensus.cs`:
`EveryCorpusEventRendersTheCommittedIdentityTextAndPriority` over the
graph entries. `apps/slate-mac/Tests/SlateMacTests/`:
A11yCorpusCensusTests (the graph entries through the real FFI),
GraphAnnouncerTests (the 0a-9 suite; the funnel guard),
GraphDiagramTests (the exact Where-am-I sentences),
`A11yResidueCensusTests` (`pinnedResidueSites` 29 → **28**).

<!-- end of the graph contracts document -->
