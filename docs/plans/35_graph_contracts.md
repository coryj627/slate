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
is at revision 2 after round 1's sixteen findings.

**What stands today.** `GraphAnnouncer.swift` (239 lines): a
`GraphVerbosity` enum mirroring the canvas's, a `GraphRowRef` carrying a
node's label and counts, a five-case `GraphEvent` (`rowFocused`,
`summary(String)`, `reRooted(label)`, `status(String)`,
`error(String)`) of which three are free-text passthroughs, three
class-keyed debounced posters outside the event type
(`announceFilterCount`, `announceForceValue`, `announceSettle`), one
grammar (`rowPhrase`, `:158–170`) and one template (`"Connections:
{label}"`, `:179`), all posting through the one `// W0.5-3 residue:`
site (`:98–100`). The manifest below (0a-12) is the site inventory;
this paragraph no longer carries one. Core carries two workspace-level
graph events (`GraphOpensSinglePane`, `ReopenedGraph`) and zero
graph-announcer entries in the corpus; the two `audio_summary` strings
are composed in `session.rs` (`snapshot_audio_summary` `:9017`; the
neighbourhood string inline at `:1990–1997`), outside the vocabulary.

### Contracts

**0a-1 — Full coverage, no free text.** Every shipped graph
announcement string is a typed variant or a typed ARM of one. Mac's
three free-text cases (`summary`, `status`, `error`) and the three
text-taking posters dissolve. `String` payload is permitted only for
dynamic data — labels, file names, OS error detail, the name-filter
needle — **never for a whole sentence**. The vocabulary is enumerated
in full below (seventeen variants over the nested enums 0a-2b names)
as a concrete FFI schema; a string with no home in the table is a round
finding, not an implementation choice.

**0a-1b — One engine, one top-level variant.** The family is reached
through `A11yEvent::Graph { event: GraphA11yEvent }`, the nested
family-per-engine pattern 0a-1b of the canvas document made the rule
(`a11y.rs:1246–1255`); `GraphA11yEvent` owns its variants and its own
`priority()` / `render()`, which `A11yEvent` delegates to. `A11yEvent`
stands at **198** top-level variants today (`a11y.rs:557` onward,
counted by `declared_variants`); this PR adds one, to 199 of uniffi's
256, and `a11y_event_top_level_count_is_pinned` pins that number so a
later family bumps it on purpose. Variant NAMES keep their `Graph`
prefix. Pinned by `the_graph_family_occupies_one_top_level_variant`.

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
the ordered list of the graph's entries; the artifact grows from 448
to **492** entries, and the record states both numbers read from the
artifact.

**0a-2b — The payload types are named, closed, and reuse what core
has.** The schema (the table below) uses these uniffi types and no
prose shorthand: `GraphVerbosity { Terse, Standard, Verbose }` (new;
0aD-2); the EXISTING `GraphNodeKind { Note, Attachment, Ghost }`
(`crates/slate-uniffi/src/lib.rs:3231`, mirroring `graph::NodeKind`) for
a row's kind — no `GraphRowKind` (0aD-4); `GraphRowCopy { label:
String, kind: GraphNodeKind, in_links: u32, out_links: u32, references:
u32, embed: bool }` (a record, the P1 row's data; degrees are `u32` as
`GraphNode`'s are); `GraphSnapshotCounts { notes: u64, links: u64,
orphans: u64, unresolved: u64, filtered: bool }` and
`GraphNeighborhoodCounts { center_label: String, in_links: u32,
out_links: u32, note_count: u64, depth: u32 }` (records exported ON the
FFI's `GraphSnapshot` / `GraphNeighborhood` as `summary_counts`, 0a-7);
`GraphPresetOutcome { Orphans { count: u64 }, Unresolved { count: u64 },
MostLinked { label: String, in_links: u32 }, NoNotesToRank }` (each arm
carries only its own fields — `Orphans` cannot carry a top row);
`GraphForceControl { Center, Repel, Link, LinkDistance }`;
`GraphTabMode { Table, Diagram }`; `GraphWhereAmISelection { Node {
row: GraphRowCopy, component: u32 }, None }`; GraphStatusNote and
GraphBlockedReason (their arms below). Nested enums the inventory
lists: `GraphVerbosity`, GraphPresetOutcome, GraphForceControl,
`GraphTabMode`, GraphWhereAmISelection, GraphStatusNote,
GraphBlockedReason — **seven** — plus the reused `GraphNodeKind`,
covered through `GraphRowCopy.kind` at its sites.

**0a-3 — The tripwires are canvas-specific today and are generalised
here, by name.** Round 1 found that "the tripwires already cover the
graph" was false: every parser knows one family. This PR changes, and
the record names, each of: (i) core's inventory — GRAPH_NESTED_ENUMS
beside CANVAS_NESTED_ENUMS, `every_graph_variant_and_arm_is_represented_in_the_corpus`
and `every_graph_parameter_enum_is_listed_for_coverage` as the canvas
tests' twins, and `graph_variant_of` beside `canvas_variant_of`; (ii)
uniffi's `the_ffi_mirror_covers_every_core_a11y_variant`, whose
nested-family parser reads ONE inventory constant today
(`lib.rs:11668`) and reads both after, with the family count asserted
per inventory and the exact-arm pins extended to the graph's seven;
(iii) `the_windows_corpus_mirror_lists_every_event_in_order`, which
matches `new CanvasA11yEvent.` (`lib.rs:11222`) and matches `new
GraphA11yEvent.` too after; (iv) `the_mac_corpus_mirror_lists_every_event_in_order`,
likewise for `.graph(event:`; (v) the two coalescing switch-list tests,
which parse `CanvasA11yEvent::` markers and open only the canvas
announcer files (`lib.rs:11365, 11418, 11489`) — after, the class list
carries the graph's classes (0a-9) and the tests open `GraphAnnouncer.swift`
and, when PR A lands it, `GraphAnnouncer.cs`, asserting the graph's four
classes against the pinned list. Each of these fails on a forgotten
mirror in `cargo test`, before any host builds — after this PR, not
before.

**0a-4 — Completeness is asserted, not assumed.**
`every_graph_variant_and_arm_is_represented_in_the_corpus` parses this
module's `pub enum` declarations and fails when a graph variant — or a
closed-set ARM of any of the seven nested enums, or of `GraphNodeKind`
at its `GraphRowCopy.kind` site — never reaches `corpus()`; its
companion asserts the coverage table lists exactly the `Graph*`
parameter enums the module declares (the reused `GraphNodeKind` listed
by an explicit exemption line, since it is not a `Graph*` a11y enum).

**0a-5 — Verbosity is a parameter on exactly one family.**
`GraphVerbosity` is core, separate from `CanvasVerbosity` (0aD-2); it
is carried by `GraphRow` and by nothing else: P1's row copy is the one
graph template that varies (terse collapses to the label). Core stays
pure — no module state; each host owns the persisted preference (in
`.slate/graph.json` through 0b's config API) and passes it per event.
`graph_verbosity_matrix_pins_every_level` pins the nine (level ×
note/ghost/embed) renderings and asserts structurally that no other
variant carries the parameter.

**0a-6 — Where-am-I always renders the full row copy, and that is a
CORRECTION of shipped mac behaviour, not a divergence.** `GraphWhereAmI`
takes no verbosity parameter; its `Node` selection renders the row copy
at standard grade at every level. Mac composes it from `rowPhrase`
(`AppState+GraphDiagram.swift:256`), which collapses to the bare label
at terse, so a terse reader asking "Where am I?" hears less than P2-3
promises ("node copy + component + zoom + active filters"). Because
Task 0a-2 moves mac onto this event, mac's behaviour changes with it —
recorded as 0a-D1, a defect correction authorised by P2-3's own text
and the t0 §1.4 rule the canvas keeps. The exact outputs — selected and
unselected, every filter combination, an empty and a non-empty name
filter, the curly quotes, the trailing period — are pinned by the
witness matrix and `graph_where_am_i_renders_every_state_exactly`.

**0a-7 — The two summaries are typed events over exported count
records, rendered by the ONE formatter core owns — and this supersedes
the spec's "relayed, not re-rendered" sentence (0aD-1).**
`GraphSnapshotSummary { counts: GraphSnapshotCounts }` and
`GraphNeighborhoodSummary { counts: GraphNeighborhoodCounts }` render
through `crates/slate-core/src/graph_summary.rs` —
`pub fn snapshot_summary(&GraphSnapshotCounts) -> String` and
`pub fn neighborhood_summary(&GraphNeighborhoodCounts) -> String`, the
P0-3 formats with `graph::grouped_decimal` — which `session.rs` calls
to fill `audio_summary` and `a11y.rs` calls to render the events, so
the format has one home. The counts' semantics are the session's
today, now named: `notes` = nodes of kind Note in the FILTERED payload;
`links` = the sum of edge `count`s (reference-distinct); `orphans` =
nodes with `is_orphan`; `unresolved` = nodes of kind Ghost; `filtered`
= the filter deviates from `{attachments: false, ghosts: true,
orphans_only: false}`; `note_count` = Note-kind nodes within the depth
INCLUDING the centre only when the centre is a note (attachments and
ghosts excluded); `depth` = the clamped depth. Hosts obtain the counts
as the records core exports on `GraphSnapshot.summary_counts` and
`GraphNeighborhood.summary_counts` and construct the event from them —
no host counts anything (R-D). Pinned by
`summary_events_render_the_snapshot_fields_verbatim` over a mixed-kind
fixture with multi-count edges: rendering the event from the record
equals the record's `audio_summary` byte for byte, snapshot and
neighbourhood, at the default filter and off it.

**0a-8 — Priorities are mac's `.error` case, listed explicitly.**
`GraphBlocked` is `High`; every other graph event is `Medium`, Where-
am-I included: mac's comment at `AppState+GraphDiagram.swift:242` says
"assertively" while the code posts `.summary` at medium
(`GraphAnnouncer.swift:118–119`); the code is the shipped behaviour,
the comment is deleted in 0a-2, and the canvas's Where-am-I is Medium
too. `priority()` ends in a catch-all `_ => Medium`, so the High member
is named in the explicit arm and pinned both ways by
`graph_priorities_pin_the_error_tier`.

**0a-9 — Coalescing stays host-side; the class keys do not.** Timing is
the hosts' (a pure render has no clock), but the classes are pinned in
`a11y.rs`'s "Coalescing class keys" list — the ONE list the two
switch-list tests read (0a-3 v) — with the graph's four: **`navigation`**
= `GraphRow`; **`filter`** = GraphFilterCount; **`forceValue`** =
GraphForceValue; **`settle`** = GraphLayoutSettled; everything else
posts immediately; a `High` graph event — announced OR relayed — flushes
**and drops** every pending class. 200 ms latest-wins, each class
independent (`GraphAnnouncer.swift:74–84, 185–218`). Two host timing
rules that are NOT classes are recorded so both hosts copy them: the
filter count's **fire-time gate** (a queued count is dropped if the
graph is no longer the focused surface when the debounce fires,
`:123–133, 204–207`) and `cancelPending` on view departure or vault
change (`:224–226`). Task 0a-2's mac tests, and PR A's Windows twins,
cover every class: latest-wins within each of the four, pairwise
independence across all six pairs, the 200 ms window, the gate, the
cancel, and a High event (announced and relayed) flushing all four.

**0a-10 — No chord-bearing template.** No graph template carries a host
chord: the tier copy names "Table mode" by its label. The chord-
parameter convention (canvas 0a-9) is not exercised by this family.

**0a-11 — The row copy is one helper, spoken and labelled alike.** P1's
row template — `"{label}, {in} links in, {out} links out"`, ghosts
`"{label}, unresolved, {references} references"`, `", embed"` appended,
terse → the label — is a single core helper (`a11y.rs::graph_row_copy`
over `GraphRowCopy`) used by `GraphRow`'s render and by
`GraphWhereAmI`'s `Node` clause. The label sites (manifest rows L1–L2)
render `GraphRow` through `a11yRender`; `rowPhrase` and `GraphRowRef`
are deleted, the generated `GraphRowCopy` taking their place.

**0a-12 — The site manifest is the inventory.** One row per string
site: file:line, the current API, the copy's source, the role
(`posted` / `static label` / `custom content` / `canonical relay`), the
event, the pin. The record checks the manifest against the tree at
close; a site the manifest lacks is a round finding.

| # | Site | Current API | Copy source | Role | Event | Pin |
|---|---|---|---|---|---|---|
| S1 | `AppState.swift:3236` | `.status("The graph is already open.")` | literal | posted | `GraphStatus{AlreadyOpen}` | golden |
| S2 | `AppState+Connections.swift:114` | `.summary(hood.audioSummary)` | `session.rs:1990` | posted | GraphNeighborhoodSummary | 0a-7 test |
| S3 | `AppState+Connections.swift:123` | `.error("Couldn't load connections: …")` | literal + OS detail | posted | `GraphBlocked{ConnectionsLoadFailed}` | golden |
| S4 | `AppState+Connections.swift:168` | `.status("Connections panel.")` | literal | posted | `GraphStatus{ConnectionsPanel}` | golden |
| S5 | `AppState+Connections.swift:212` | `.reRooted(label:)` | `GraphAnnouncer.swift:179` | posted | `GraphReRooted` | golden |
| S6 | `AppState+Connections.swift:236` | `.reRooted(label:)` | same | posted | `GraphReRooted` | golden |
| S7 | `AppState+Connections.swift:287` | `.status("Created note ….")` | literal + name | posted | `GraphStatus{NoteCreated}` | golden |
| S8 | `AppState+Connections.swift:294` | `.error("Couldn't create note: …")` | literal + detail | posted | `GraphBlocked{NoteCreateFailed}` | golden |
| S9 | `AppState+GraphConfig.swift:131` | `announceForceValue(phrase)` | `:139–150` | posted (forceValue) | GraphForceValue | golden ×4 |
| S10 | `AppState+GraphDiagram.swift:177` | `announceSettle("Graph layout settled.")` | literal | posted (settle) | GraphLayoutSettled | golden |
| S11 | `AppState+GraphDiagram.swift:197` | `.rowFocused(ref)` | `rowPhrase` | posted (navigation) | `GraphRow` | matrix |
| S12 | `AppState+GraphDiagram.swift:231` | `.status("Unpinned.")` | literal | posted | `GraphPinned{false}` | golden |
| S13 | `AppState+GraphDiagram.swift:235` | `.status("Pinned.")` | literal | posted | `GraphPinned{true}` | golden |
| S14 | `AppState+GraphDiagram.swift:245` | `.summary(whereAmIText)` | `:252–267`, `:359–365`, `rowPhrase` at `:256` | posted | `GraphWhereAmI` | 0a-6 test |
| S15 | `AppState+GraphDiagram.swift:346` | `.status("Zoom … percent.")` | literal + percent | posted | `GraphZoom{fit:false}` | golden |
| S16 | `AppState+GraphDiagram.swift:355` | `.status("Fit graph. Zoom … percent.")` | literal + percent | posted | `GraphZoom{fit:true}` | golden |
| S17 | `AppState+GraphTable.swift:57` | `.status("Graph.")` | literal | posted | `GraphStatus{Opened}` | golden |
| S18 | `AppState+GraphTable.swift:197` | `.status(graphPresetAnnouncement(…))` | `:336–354` | posted | `GraphPreset` | golden ×4 |
| S19 | `AppState+GraphTable.swift:202` | `.summary(snap.audioSummary)` | `session.rs:9017` | posted | GraphSnapshotSummary | 0a-7 test |
| S20 | `AppState+GraphTable.swift:204` | `announceFilterCount(graphFilterCountText(snap), gate:)` | `:225–235` | posted (filter) | GraphFilterCount | golden |
| S21 | `AppState+GraphTable.swift:215` | `.error("Couldn't load the graph: …")` | literal + detail | posted | `GraphBlocked{LoadFailed}` | golden |
| S22 | `GraphDiagramView.swift:496–498` | `.status("Large graph: …")` | literal | posted | GraphTierEntered | golden |
| S23 | `GraphTableView.swift:100` | `.status("Diagram mode.")` | literal | posted | `GraphMode{Diagram}` | golden |
| S24 | `GraphTableView.swift:103` | `.status("Table mode.")` | literal | posted | `GraphMode{Table}` | golden |
| S25 | `GraphTableView.swift:340–342` | `announceFilterCount("… shown", gate:)` | inline literal (duplicate of S20's) | posted (filter) | GraphFilterCount | golden |
| S26 | `GraphTableView.swift:361` | `.status(a11yRender(event).text)` | the grid's own core events | **canonical relay** (priority dropped today) | `relay(A11yEvent)` (0a-13) | mac test |
| L1 | `ConnectionsPanel.swift:239` | `.accessibilityLabel(rowPhrase(row.rowRef))` | `rowPhrase` | static label (AX name) | `GraphRow` rendered | matrix |
| L2 | `GraphDiagramView.swift:843` | `axLabel` → `rowPhrase(ref)` | `rowPhrase` | static label (AX name) | `GraphRow` rendered | matrix |
| C1 | `GraphDiagramView.swift:851–869` | `AXCustomContent(label: "Connects to", value:)` | inline | custom content | GraphNeighborsContent | golden ×3 |
| C2 | `GraphDiagramView.swift:627–629` | `setAccessibilityLabel("… nodes — too many …")` | inline | static label (the summary element's name) | GraphTierSummary | golden ×2 |
| C3 | `GraphDiagramView.swift:631` | custom action `"Switch to Table"` | inline | static label | label inventory (0a-14) | §W-C |
| T1 | `ConnectionsPanel.swift:93, 103–104` | `"Local graph depth"` + hint | inline | static label | label inventory | §W-C |
| T2 | `ConnectionsPanel.swift:121` | `"Connections error: …"` | inline | static label | label inventory (the posted form is S3) | §W-C |
| T3 | `ConnectionsPanel.swift:125, 130` | `"This note has no connections."` (visible and AX) | inline | static label on mac; **posted on Windows** (0a-D3) | `GraphStatus{NoConnections}` | golden |
| T4 | `ConnectionsPanel.swift:137, 144` | `"Loading connections…"` visible / `"Loading connections."` AX | inline | static label on mac; **posted on Windows** (0a-D3) | `GraphStatus{LoadingConnections}` — the AX form, with the period | golden |
| T5 | `ConnectionsPanel.swift:301–309` | badges `Unresolved` / `Embed` / `Attachment` | inline | static label | label inventory | §W-C |
| T6 | `GraphTableView.swift:351, 614–622, 482–484` | `"Graph, data grid"`, column titles, kind labels | inline | static label | label inventory | §W-C |

**0a-13 — The one already-canonical site becomes a relay that keeps the
priority, on one seam with the typed announcements.** S26 reaches mac's
announcer as a Medium status, dropping core's priority — the defect the
canvas's 0a-2 fixed. Task 0a-2 pins the announcer's API:
`announce(_ event: GraphA11yEvent)` renders and classifies;
`relay(_ event: A11yEvent)` carries the render's text AND priority; both
converge on one private `emit(text, priority, class)` so a High RELAY
flushes and drops the four classes exactly as a High announcement does;
`GraphEvent` is deleted; `GraphRowRef` is replaced by the generated
`GraphRowCopy`; the generated `GraphVerbosity` gains a Swift extension
for its menu `title` and its `.slate/graph.json` spelling (`terse` /
`standard` / `verbose`, the strings the store already writes); the
poster seam is `postAccessibilityAnnouncement(event)` with the typed
event, never `.hostComposed`. PR A's `GraphAnnouncer.cs` mirrors the
three methods.

**0a-14 — Label-grade events are marked as such; "never spoken" is
the wrong word.** Two events are LABEL class — **not actively posted**;
assistive technology reads them as an element's name or content:
`GraphTierSummary { count }` (C2) and `GraphNeighborsContent { labels,
more }` (C1). Two more are dual-use by recorded divergence (0a-D3):
`GraphStatus{NoConnections}` and `GraphStatus{LoadingConnections}` are
static labels on mac (T3, T4) and posted on Windows when the leaf takes
focus in that state. Every other graph label — C3, T1, T2, T5, T6 —
stays §W-C label class, recorded in the label inventory PR A/B carry,
and is NOT in this vocabulary. The manifest is the exhaustive mapping.

**0a-15 — Every render arm is TOTAL over its payload domain, and every
preserved plural defect is listed.** An arm renders at every value its
payload admits: `GraphNeighborsContent { labels: [] }` renders the empty
string (the host omits the content); `GraphPresetOutcome::NoNotesToRank`
renders `"No notes to rank."`; `GraphWhereAmISelection::None` renders
`"No node selected"`. The copy rule preserves the shipped templates'
fixed plurals; every singular that therefore reads as a defect is
allow-listed as an (arm, text) PAIR by `graph_plural_defects_are_exactly_these`,
each proved to render from that arm and nowhere else: `GraphRow` —
`"1 links in"`, `"1 links out"`, `"1 references"`; GraphSnapshotSummary
— `"1 notes"`, `"1 links"`, `"1 orphans"`, `"1 unresolved targets"`;
GraphNeighborhoodSummary — `"1 links in"`, `"1 links out"`, `"1 notes"`,
`"within 1 links"` (`session.rs:780, 914` already pin two of these as
shipped); `GraphPreset` — `"1 orphaned notes"`, `"1 unresolved targets"`,
`"1 links in"`; GraphTierSummary — `"1 nodes"`; GraphNeighborsContent
— `"and 1 more"`; `GraphWhereAmI` inherits `GraphRow`'s. `"0 links out"`
is grammatical and is a boundary witness, not a defect. The matrix
(0a-17) carries a zero, a one and a many for every count.

**0a-16 — The shipped string is the authority where the P specs
differ, and each difference is recorded.** P2 writes the tier message
with a semicolon (`p2_spec.md:109`); mac shipped two sentences with a
period (`GraphDiagramView.swift:498`) — the shipped form moves (0a-D4).
P2 writes `"Repel force 0.7; layout settling… settled."` (`p2_spec.md:127`);
mac shipped `"Repel force 70 percent"` (a percent, S9) and a separate
`"Graph layout settled."` (S10) — both shipped forms move as two events
in that order (0a-D5). Punctuation, the numeric form (a rounded integer
percent) and the event order are pinned by the golden rows.

**0a-17 — The witness matrix is ordered and pinned.** The corpus gains
these entries, in this order, after `CanvasStatus { Loading }`; the
artifact total becomes 492 and `the_graph_family_witness_count_is_pinned`
asserts 44 graph entries:

| Variant | Witnesses (in order) |
|---|---|
| `GraphRow` (5) | Standard note "Alpha" 3/1 · Standard ghost "Missing Note" references 2 · Standard note "Pic" 1/0 embed · Terse note "Alpha" · Verbose note "Hub" 1,024/0 (grouped? — no: degrees are bare `u32`, "1024 links in"; the witness pins that degrees are NOT grouped) |
| `GraphReRooted` (1) | "Alpha" |
| GraphSnapshotSummary (3) | 247 notes, 1,032 links, 12 orphans, 3 unresolved · 1 notes, 0 links (second sentence omitted) · 40 notes, 60 links, 0/0, filtered |
| GraphNeighborhoodSummary (2) | "Alpha" 3/1, 7 notes within 1 · "Hub" 1,032/4, 1,206 notes within 3 |
| `GraphPreset` (4) | Orphans 12 · Unresolved 1 · MostLinked "Hub" 1,032 · NoNotesToRank |
| GraphFilterCount (2) | 3 of 247 · 1 of 1 |
| GraphForceValue (4) | Center 50 · Repel 70 · Link 100 · LinkDistance 0 |
| GraphLayoutSettled (1) | — |
| `GraphPinned` (2) | true · false |
| `GraphZoom` (2) | 125 plain · 63 fit |
| `GraphMode` (2) | Table · Diagram |
| `GraphWhereAmI` (3) | Node "Alpha" 3/1, component 2, zoom 100, orphans only + attachments shown + unresolved shown, name filter “alp”, unresolved only · Node ghost "Missing Note", component 0, zoom 250, unresolved hidden, no name filter · None, zoom 100, unresolved shown |
| GraphTierEntered (1) | — |
| GraphTierSummary (2) | 2,000 · 1 |
| GraphNeighborsContent (3) | ten labels, more 0 · three labels, more 1 · labels [] |
| `GraphStatus` (6) | Opened · AlreadyOpen · ConnectionsPanel · NoteCreated "Draft.md" · NoConnections · LoadingConnections |
| `GraphBlocked` (3) | LoadFailed "io error" · ConnectionsLoadFailed "io error" · NoteCreateFailed "exists" |

Forty-four. `GraphRow`'s fifth witness settles a question the round
raised: degrees render as bare integers everywhere the row copy speaks
them (mac's `\(ref.linksIn)` is a bare `UInt32`), and only the summary
counts are grouped (`grouped_decimal`), as P0-3 writes.

**0a-18 — Mac consumes in the same PR (Task 0a-2), and the module doc
and the census record it.** Every S-row's composition is replaced by
event construction + `a11yRender` through the API of 0a-13; the L-rows
render `GraphRow`; C1/C2 render their events; the two `"{k} of {n}
shown"` sites collapse to one event; GraphAnnouncerTests's copy
assertions (`:25–51`), `GraphCommandsTests.testPresetAnnouncementStringsAreVerbatim`
(`:76`) and `GraphConfigTests.testForcesChangePhraseNamesTheOneChangedControl`
(`:97`) move to the Rust golden and the Swift tests keep the coalescing
suite of 0a-9; `A11yResidueCensusTests.pinnedResidueSites` reads **28**
when `GraphAnnouncer.swift:98`'s marker goes; `a11y.rs:14–38`'s module
doc is rewritten so only the structural-mutation builder remains a
named host-composed exception, pinned by the residue census's own
comment and by `the_module_doc_names_no_engine_but_the_mutation_builder`
(a source scan for "remaining named engine"). The Swift edit is unrun
on this box (0aR-1).

### Decisions

- **0aD-1 — The summaries are typed events over exported count records,
  superseding the spec's "relayed, not re-rendered" sentence.** The
  spec's §4 PR 0a said the two `audio_summary` strings are relayed as
  text; that keeps a whole sentence in a `String` payload (0a-1) and
  leaves the corpus unable to witness P0-3's format. Core exports the
  counts (`summary_counts` on both records), renders the event and the
  field from one formatter (0a-7), and the spec's paragraph is amended
  in this PR to point here.
- **0aD-2 — `GraphVerbosity` is its own enum.** Not an alias of
  `CanvasVerbosity`: P persists the graph's level in the vault file and
  the canvas's on the device; one type would invite one preference.
- **0aD-3 — The filter count's fire-time gate and `cancelPending` are
  host rules, not events.** They govern WHETHER a queued count posts,
  which a pure render cannot decide; both hosts implement them and the
  contracts name them (0a-9).
- **0aD-4 — A row's kind reuses `GraphNodeKind`.** The FFI already
  carries the closed kind vocabulary; a second enum would be a
  duplicate with a mapping to maintain.
- **0aD-5 — The shipped string is the copy authority; the P specs'
  variants are recorded, not adopted.** 0a-16's two cases; a future P
  amendment changes the vocabulary through a revision, not a host.

### Recorded divergences (owner-recorded; off-limits for re-litigation)

- **0a-D1 — Where-am-I speaks the full row copy at every verbosity, on
  both hosts.** A correction of mac's terse coupling (0a-6), authorised
  by P2-3's text; mac's behaviour changes in this PR.
- **0a-D2 — The grid relay keeps core's priority** (0a-13); mac's
  `GraphTableView.swift:361` re-wrapped a rendered event as Medium.
- **0a-D3 — The Connections panel's empty and loading states are
  posted on Windows when the leaf takes focus in them**, and stay static
  labels on mac (T3, T4): the W4-7 UIA-delivery lesson — a label on a
  plain container is not heard. Mac's behaviour is unchanged by 0a; PR B
  pins Windows's.
- **0a-D4 — The tier message keeps mac's two sentences**, not P2's
  semicolon (`p2_spec.md:109`).
- **0a-D5 — The force value and the settle are two events with mac's
  wording**, not P2's single phrase (`p2_spec.md:127`).

### Accepted risks

- **0aR-1 — The Swift migration is unrun on this box.** Mac's CI lane
  arbitrates; a red mac lane is fixed here, not waived.
- **0aR-2 — The uniffi budget.** `A11yEvent` moves to 199 of 256
  top-level variants; the nesting rule (0a-1b) keeps every later engine
  at one.

### Round 1 — sixteen findings, dispositions

| Finding | Severity | Disposition |
|---|---|---|
| IG0a-1 | BLOCKER | taken — typed summaries kept, the counts exported as records, the formatter named, the spec's sentence amended and 0aD-1 records the supersession |
| IG0a-2 | BLOCKER | taken — 0a-2b names every payload type; GraphPresetOutcome and GraphWhereAmISelection close the invalid combinations; `GraphNodeKind` reused (0aD-4); widths pinned |
| IG0a-3 | BLOCKER | taken — 0a-3 rewritten as the list of parsers this PR generalises, each named |
| IG0a-4 | BLOCKER | taken — 0a-16 and 0aD-5: the shipped string is the authority; 0a-D4/0a-D5 record P2's variants |
| IG0a-5 | MAJOR | taken — 0a-15 lists every preserved singular as a pair; the matrix carries 0/1/many |
| IG0a-6 | MAJOR | taken — "not actively posted"; the manifest's role column classifies every string; the dual-use pair recorded |
| IG0a-7 | MAJOR | taken — the module and signatures named; `note_count` with its exact semantics; the mixed fixture |
| IG0a-8 | MAJOR | taken — 0a-13 pins the mac API, the deletion of `GraphEvent`/`GraphRowRef`, the verbosity extension, the poster seam |
| IG0a-9 | MAJOR | taken — 0a-9 requires the full coalescing suite on mac and its Windows twin in PR A |
| IG0a-10 | MAJOR | taken — 0a-17's ordered matrix, 44 witnesses, the total 492 pinned |
| IG0a-11 | MAJOR | taken — 0a-6 reclassified as a correction; exact outputs pinned |
| IG0a-12 | MAJOR | taken — 0a-12's manifest replaces the prose census (S11 restored; `:256` and `:361` reclassified) |
| IG0a-13 | MINOR | taken — 198 → 199, pinned |
| IG0a-14 | MINOR | taken — both test names corrected here and in the spec |
| IG0a-15 | MINOR | taken — the module-doc rewrite is an acceptance item with a scan (0a-18) |
| IG0a-16 | MINOR | taken — Medium pinned, the stale comment deleted (0a-8) |

### Tests that pin PR 0a

`crates/slate-core/src/a11y.rs`: `corpus_renders_the_shipped_strings`
(the 44 golden rows), `committed_corpus_artifact_matches_the_vocabulary`
(492), `every_graph_variant_and_arm_is_represented_in_the_corpus` and
`every_graph_parameter_enum_is_listed_for_coverage`,
`the_graph_family_occupies_one_top_level_variant`,
`a11y_event_top_level_count_is_pinned`,
`graph_verbosity_matrix_pins_every_level`,
`graph_where_am_i_renders_every_state_exactly`,
`graph_priorities_pin_the_error_tier`,
`graph_plural_defects_are_exactly_these`,
`the_graph_family_witness_count_is_pinned`,
`the_module_doc_names_no_engine_but_the_mutation_builder`.
`crates/slate-core/src/graph_summary.rs` and `session.rs`:
`summary_events_render_the_snapshot_fields_verbatim`.
`crates/slate-uniffi/src/lib.rs`: the generalised
`the_ffi_mirror_covers_every_core_a11y_variant`, the two corpus-order
tests, the two coalescing switch-list tests.
`apps/slate-windows/.../A11yCorpusCensus.cs`:
`EveryCorpusEventRendersTheCommittedIdentityTextAndPriority` over the
graph entries. `apps/slate-mac/Tests/SlateMacTests/`:
A11yCorpusCensusTests (the graph entries through the real FFI),
GraphAnnouncerTests (the 0a-9 suite; the funnel guard),
`A11yResidueCensusTests` (`pinnedResidueSites` 29 → **28**).

<!-- end of the graph contracts document -->
