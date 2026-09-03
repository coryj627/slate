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
is at revision 5: round 1 returned sixteen findings, round 2 twelve,
round 3 twelve, and round 3's verdict invoked the protocol's stopping
rule 4 — three consecutive rounds of blockers in one subsystem, twice
over — so revision 4 opened with the design pass that rule demands.
Round 4 found Subsystem A closed by it and Subsystem B not yet: its ten
findings are taken in revision 5, which audits the WHOLE schema against
design B(i)'s reachability rule rather than one variant.

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

### Design pass (protocol rule 4) — the two subsystems, modelled

Rounds 1–3 each fixed the site a finding named and not the class it
belonged to, in two subsystems. Before more prose or code, the class.

**Subsystem A — the closed-set inventory (IG0a-3 → IG0a-20 → IG0a-30).**
The rounds fixed the parser, then the path, then the mirror name. The
class: a closed set the family carries as a parameter has FOUR
consumers that must agree on it — core's coverage test (which file
declares it; which (variant, field) sites reach it), uniffi's mirror
test (which FFI enum mirrors it, both directions), and the two host
corpus mirrors (which host constructor names it). A positional tuple
cannot carry four consumers' facts without a reader guessing. **The
design: one self-describing inventory row, `NestedEnum { core, mirror,
path, sites }`, written with NAMED fields, and every consumer reads by
key.** `core` is the Rust enum, `mirror` the FFI enum (`GraphNodeKind`
for `NodeKind`; identical otherwise), `path` the crate-relative source
file, `sites` the (variant, field) pairs. uniffi's tripwire parses
`core:`, `mirror:` and `path:` out of the table by key — never by
position — and opens the named file; the two conversion directions of
every external enum are compile facts (`From` both ways). The canvas's
CANVAS_NESTED_ENUMS keeps its tuple shape (its consumers are E13's,
unchanged, pinned); the graph's table is the first of the named form
and every later family copies it. Contract 0a-3 is this design.

**Subsystem B — the literal schema against the governing copy
(IG0a-2/4 → IG0a-17 → IG0a-31/33).** Two classes were being patched
site by site. (i) **Payload shape:** a variant's payload is the closed
set of HOST-REACHABLE states, never a product of independent booleans. A
boolean field is admitted only when every combination with its siblings
is reachable on the shipped host; otherwise the states become the arms
of an enum, and the matrix carries one witness per arm. Applied:
`GraphWhereAmI`'s filter clause becomes `GraphWhereAmIFilter { Normal {
orphans_only, attachments_shown, ghosts_shown }, UnresolvedOnly }` —
the three toggles of the Normal arm are independent controls on mac
(all eight combinations reachable), while the unresolved preset is one
state that fixes the backend flags (attachments hidden, ghosts shown,
orphans off; `AppState+GraphTable.swift:282–284, 294–295`) and is
cleared by any manual toggle (`:255–262`). The selection-kind rule is a
host invariant, not a payload: a Ghost is selectable only while ghosts
are shown and an Attachment only while attachments are shown (the
diagram drops a selection outside its visible set,
`GraphDiagramView.swift:484`), under the unresolved preset only Ghost
rows exist to select, under orphans-only only Notes with no links
either way survive (`graph.rs::filtered_nodes`), and under a name
filter only labels containing the trimmed needle exist. Round 4 showed
the rule had been applied to one variant; revision 5 audits EVERY
payload of the schema and records each one's invariants in the
"Payload invariants" table under 0a-2b, asserted over the whole matrix
by `graph_witnesses_obey_the_payload_invariants` (Where-am-I's kind
rule keeps its own test, `graph_where_am_i_witnesses_are_host_reachable`).
The audit moved one more rule into core: the neighbour content's
"first ten, then the overflow" was a host cap, so a host could hand
core three labels and an overflow of one — a state it can never be in.
`GraphNeighborsContent` now carries the FULL ordered visible-neighbour
list and core owns the cap (`GRAPH_NEIGHBOR_LABEL_CAP = 10`). (ii)
**Copy authority:** the governing spec carries NO announcement strings.
Every template lives in exactly two places — this document's schema
(0a-2b) and the Rust golden — and the spec's §4 names EVENTS
(`GraphStatus{Opened}`, `GraphMode{Diagram}`), never quotes their text.
Applied in this revision to every quoted announcement in
`w6_2_graph_spec.md` §4 (PR A's items 2 and 7 and its acceptance, PR C's
filter count and Where-am-I grade, PR D's acceptance, PR E's force
sentence) and recorded as a §5 process rule there, so the spec cannot
drift from the schema again because it no longer restates it.

### Contracts

**0a-1 — Full coverage, no free text.** Every shipped graph
announcement string is a typed variant or a typed ARM of one. Mac's
three free-text cases and the three text-taking posters dissolve.
`String` payload is permitted only for dynamic data — labels, file
names, OS error detail, the name-filter needle — **never for a whole
sentence**. The schema (0a-2b) is the vocabulary: seventeen variants
over eight nested a11y enums plus one reused graph enum; a string with
no home there is a round finding, not an implementation choice.

**0a-1b — One engine, one top-level variant.** The family is reached
through `A11yEvent::Graph { event: GraphA11yEvent }`, the nested
family-per-engine pattern 0a-1b of the canvas document made the rule
(`a11y.rs:1246–1255`); `GraphA11yEvent` owns its variants and its own
`priority()` / `render()`, which `A11yEvent` delegates to. `A11yEvent`
stands at **198** top-level variants today; this PR adds one, to 199 of
uniffi's 256, and `a11y_event_top_level_count_is_pinned` pins that
number so a later family bumps it on purpose. Variant NAMES keep their
`Graph` prefix; the two workspace-level events that predate the family
(`GraphOpensSinglePane`, `ReopenedGraph`) are tab-management copy and
stay top-level. Pinned by `the_graph_family_occupies_one_top_level_variant`.

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
the ordered list of the graph's entries: **71** of them, the artifact
growing from 448 to **519**; `the_graph_witnesses_are_exactly_the_matrix_in_order`
(0a-19) is the executable form of the matrix.

**0a-2b — The schema, literally.** Every type below is a uniffi record
or enum; widths are as written; nothing is a prose shorthand.

Supporting types:

| Type | Definition | Note |
|---|---|---|
| `GraphVerbosity` | `enum { Terse, Standard, Verbose }` | new; 0aD-2 |
| `GraphNodeKind` | the EXISTING FFI enum `{ Note, Attachment, Ghost }` (`lib.rs:3231`) mirroring `graph::NodeKind` (`graph.rs:53`) | reused (0aD-4); this PR adds the reverse mapping `From<GraphNodeKind> for core::graph::NodeKind` so a host can construct a row |
| `GraphRowCopy` | `record { label: String, kind: GraphNodeKind, in_links: u32, out_links: u32, references: u32, embed: bool }` | P1's row data; degrees are `u32` as `GraphNode`'s; `embed` is independent of `kind` (a ghost reached only by `![[…]]` is a Ghost with `embed`) |
| `GraphSnapshotCounts` | `record { notes: u64, links: u64, orphans: u64, unresolved: u64, filtered: bool }` | exported on `GraphSnapshot.summary_counts` (0a-7) |
| `GraphNeighborhoodCounts` | `record { center_label: String, in_links: u32, out_links: u32, note_count: u64, depth: u32 }` | exported on `GraphNeighborhood.summary_counts` (0a-7) |
| `GraphPresetOutcome` | `enum { Orphans { count: u64 }, Unresolved { count: u64 }, MostLinked { label: String, in_links: u32 }, NoNotesToRank }` | each arm carries only its fields |
| `GraphForceControl` | `enum { Center, Repel, Link, LinkDistance }` | |
| `GraphSurfaceMode` | `enum { Table, Diagram }` | named to avoid mac's local `GraphTabMode` (`GraphTableView.swift:9`), which 0a-18 deletes in its favour |
| `GraphWhereAmISelection` | `enum { Node { row: GraphRowCopy, component: u32 }, NoSelection }` | `NoSelection`, not `None` — the generated Swift `.none` collision the canvas's `Unstated` avoids (`lib.rs:7591`) |
| `GraphWhereAmIFilter` | `enum { Normal { orphans_only: bool, attachments_shown: bool, ghosts_shown: bool }, UnresolvedOnly }` | design B(i): the unresolved preset is one state, not four flags; its backend flags are implied |
| GRAPH_NEIGHBOR_LABEL_CAP | `pub const usize = 10` (core, `a11y.rs`) | the neighbour content speaks the first ten labels and counts the rest — P2-3's rule, moved from the host (design B(i)) |
| `GraphStatusNote` | `enum { Opened, AlreadyOpen, ConnectionsPanel, NoteCreated { name: String }, NoConnections, LoadingConnections }` | |
| `GraphBlockedReason` | `enum { LoadFailed { message: String }, ConnectionsLoadFailed { message: String }, NoteCreateFailed { message: String } }` | |

The seventeen variants of `GraphA11yEvent`. `Pri` `Medium` unless
**High**; `Class`: `nav` / `filter` / `force` / `settle` / `—`
(immediate). `⟨⟩` marks substitution; counts render as bare integers
except where "grouped" is written (0a-16).

| Variant | Fields (uniffi) | Template | Pri | Class |
|---|---|---|---|---|
| `GraphRow` | `verbosity: GraphVerbosity, row: GraphRowCopy` | Terse: `⟨label⟩`. Standard/Verbose: kind Note or Attachment → `⟨label⟩, ⟨in_links⟩ links in, ⟨out_links⟩ links out`; kind Ghost → `⟨label⟩, unresolved, ⟨references⟩ references`; then `, embed` when `embed` (any kind) | | nav |
| `GraphReRooted` | `label: String` | `Connections: ⟨label⟩` | | — |
| `GraphSnapshotSummary` | `counts: GraphSnapshotCounts` | `⟨notes⟩ notes, ⟨links⟩ links.` + (when orphans > 0 or unresolved > 0) ` ⟨orphans⟩ orphans, ⟨unresolved⟩ unresolved targets.` + (when filtered) ` Filtered.` — all four counts grouped | | — |
| `GraphNeighborhoodSummary` | `counts: GraphNeighborhoodCounts` | `⟨center_label⟩: ⟨in_links⟩ links in, ⟨out_links⟩ links out. Showing ⟨note_count⟩ notes within ⟨depth⟩ links.` — the three counts grouped, depth bare | | — |
| `GraphPreset` | `outcome: GraphPresetOutcome` | Orphans → `⟨count⟩ orphaned notes.`; Unresolved → `⟨count⟩ unresolved targets.`; MostLinked → `Most linked: ⟨label⟩, ⟨in_links⟩ links in.`; NoNotesToRank → `No notes to rank.` | | — |
| `GraphFilterCount` | `shown: u32, total: u32` | `⟨shown⟩ of ⟨total⟩ shown` | | filter |
| `GraphForceValue` | `control: GraphForceControl, percent: u32` | `Center force ⟨percent⟩ percent` ‖ `Repel force ⟨percent⟩ percent` ‖ `Link force ⟨percent⟩ percent` ‖ `Link distance ⟨percent⟩ percent` | | force |
| `GraphLayoutSettled` | — | `Graph layout settled.` | | settle |
| `GraphPinned` | `pinned: bool` | `Pinned.` ‖ `Unpinned.` | | — |
| `GraphZoom` | `fit: bool, percent: u32` | `Zoom ⟨percent⟩ percent.` ‖ `Fit graph. Zoom ⟨percent⟩ percent.` | | — |
| `GraphMode` | `mode: GraphSurfaceMode` | `Table mode.` ‖ `Diagram mode.` | | — |
| `GraphWhereAmI` | `selection: GraphWhereAmISelection, zoom_percent: u32, filter: GraphWhereAmIFilter, name_filter: Option<String>` | parts joined by `, ` then `.`: Node → the row copy at Standard grade, `component ⟨component⟩`; NoSelection → `No node selected`; then `zoom ⟨zoom_percent⟩ percent`; then Normal → `filters: ` + the active of `orphans only`, `attachments shown`, and `unresolved shown` ‖ `unresolved hidden` joined by `, `, UnresolvedOnly → `filters: unresolved shown`; then (when `name_filter` is Some and non-empty after trimming) `name filter “⟨trimmed needle⟩”`; then UnresolvedOnly → `unresolved only` | | — |
| `GraphTierEntered` | — | `Large graph: summary accessibility mode. Table mode has every node.` | | — |
| `GraphTierSummary` | `count: u32` | `⟨count⟩ nodes — too many for per-node navigation. Switch to Table mode for the full, navigable list.` — LABEL; count bare | | — |
| `GraphNeighborsContent` | `labels: Vec<String>` — the FULL ordered list of visible unique neighbours | `⟨the first GRAPH_NEIGHBOR_LABEL_CAP labels joined by ", "⟩` + (when more than the cap) ` and ⟨len − cap⟩ more`; empty labels → the empty string — LABEL (content title `Connects to`) | | — |
| `GraphStatus` | `note: GraphStatusNote` | Opened `Graph.` · AlreadyOpen `The graph is already open.` · ConnectionsPanel `Connections panel.` · NoteCreated `Created note ⟨name⟩.` · NoConnections `This note has no connections.` · LoadingConnections `Loading connections.` | | — |
| `GraphBlocked` | `reason: GraphBlockedReason` | LoadFailed `Couldn't load the graph: ⟨message⟩` · ConnectionsLoadFailed `Couldn't load connections: ⟨message⟩` · NoteCreateFailed `Couldn't create note: ⟨message⟩` | **High** | — |

Payload invariants (design B(i), audited over the whole schema): every
combination a payload admits that the shipped host cannot reach is
named here, and `graph_witnesses_obey_the_payload_invariants` refuses a
witness that violates one.

| Payload | Invariant the host guarantees |
|---|---|
| `GraphRowCopy` | a Ghost's copy speaks `references` (its inbound reference count) and ignores `in_links`/`out_links`, which carry the node's degrees unchanged; `embed` is independent of `kind` (a ghost reached only by `![[…]]` is a Ghost with `embed`) |
| `GraphNeighborhoodCounts.depth` | 1..=3 — the session clamps before it exports |
| `GraphFilterCount` | `shown ≤ total` — the needle narrows the fetched set |
| `GraphForceValue.percent` | 0..=100 — a slider's value ×100, rounded |
| `GraphZoom.percent` | ≥ 1 — the viewport's scale ×100, rounded |
| `GraphWhereAmI` | a Ghost only while ghosts are shown (Normal with `ghosts_shown`, or UnresolvedOnly), an Attachment only while attachments are shown, a Note only under Normal; under `orphans_only` the selection is a Note with `in_links == 0 && out_links == 0`; under a non-empty needle the selected label contains the trimmed needle, case-insensitively |
| `GraphTierSummary.count` | above the tier-B threshold (1,500 as shipped, `GraphDiagramView.swift`; §2 row H moves the constant) |
| `GraphNeighborsContent.labels` | unique labels in the host's visible traversal order; the cap is core's |

**0a-3 — The inventory is the design of Subsystem A, and every tripwire
reads it by key.** (i) Core declares `GRAPH_NESTED_ENUMS: &[NestedEnum]`
with `struct NestedEnum { core: &str, mirror: &str, path: &str, sites:
&[(&str, &str)] }` — nine rows: the eight `Graph*` sets of `a11y.rs`
(mirror = core) and `NodeKind` (`mirror: "GraphNodeKind"`, `path:
"src/graph.rs"`, sites `(GraphRow, kind)` and `(GraphWhereAmI, kind)`).
`declared_variants_in(path, enum)` replaces `declared_variants` for both
inventories; `every_graph_variant_and_arm_is_represented_in_the_corpus`
and `every_graph_parameter_enum_is_listed_for_coverage` are the canvas
tests' twins over the named fields; `graph_variant_of` beside
`canvas_variant_of`. (ii) uniffi's
`the_ffi_mirror_covers_every_core_a11y_variant` reads BOTH inventories
(today one constant, `lib.rs:11668–11681`) — the canvas's tuples as
before, the graph's rows by the keys `core:`, `mirror:`, `path:` —
opens the named source per row, compares the core set to the MIRROR-
named FFI set both ways, asserts each inventory's row count (18 and 9),
and pins the graph's nine arm counts exactly (`GraphVerbosity` 3,
`GraphPresetOutcome` 4, `GraphForceControl` 4, `GraphSurfaceMode` 2,
`GraphWhereAmISelection` 2, `GraphWhereAmIFilter` 2, `GraphStatusNote`
6, `GraphBlockedReason` 3, `NodeKind` 3); both `GraphNodeKind`
conversion directions exist (a compile fact). (iii)
`the_windows_corpus_mirror_lists_every_event_in_order` derives the inner
constructor marker from the wrapper's name (`new ⟨Outer⟩A11yEvent.`) so
a canvas entry cannot satisfy a graph position; (iv)
`the_mac_corpus_mirror_lists_every_event_in_order` already reads
`event: .` generically. (v) The coalescing switch-list tests: core's ONE
list gains the graph's classes (0a-9), `pinned_coalescing_classes`
takes the family name and reads only that family's markers, the mac
test opens `GraphAnnouncer.swift` beside `CanvasAnnouncer.swift` and
asserts the graph's four classes; the Windows test asserts the canvas
file in 0a and gains `GraphAnnouncer.cs` in PR A — **0a makes no claim
that a forgotten Windows graph switch fails before PR A exists.** After
this PR, a forgotten core arm, FFI arm, mac or Windows corpus entry, or
mac class fails in `cargo test`; the Windows class fails from PR A.

**0a-4 — Completeness is asserted, not assumed.**
`every_graph_variant_and_arm_is_represented_in_the_corpus` fails when a
graph variant — or an arm of any inventory row, `NodeKind` at its
`GraphRowCopy.kind` sites included — never reaches `corpus()`; its
companion asserts the inventory's local rows are exactly the `Graph*`
a11y enums this module declares and its one external row is `NodeKind`
in `src/graph.rs`.

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
shipped mac behaviour; its states are the host's reachable ones.**
`GraphWhereAmI` takes no verbosity; its `Node` selection renders
`GraphRowCopy` at Standard grade. Mac composes it from `rowPhrase`
(`AppState+GraphDiagram.swift:256`), which collapses to the bare label
at terse, so a terse reader asking "Where am I?" hears less than P2-3
promises ("node copy + component + zoom + active filters"). Task 0a-2
moves mac onto this event, so mac's behaviour changes — recorded as
0a-D1, authorised by P2-3's own text and the t0 §1.4 rule the canvas
keeps. The needle: the substituted value is the TRIMMED needle
(Unicode White_Space at both ends — Rust's `str::trim`; mac hands the
raw field value over and core trims), and a needle empty after trimming
is omitted; the matrix witnesses the Unicode rule with U+00A0 (no-break
space) and U+2003 (em space), not ASCII spaces alone. The filter clause
is the closed `GraphWhereAmIFilter` (design B(i)); the payload
invariants of 0a-2b are the host's and the matrix obeys them. The seven
matrix witnesses cover: an orphan Note ("Alone", 0/0) under Normal with
all three toggles on and a needle it contains; UnresolvedOnly with a
Ghost selected at each reference cardinality (2, 0, 1); NoSelection with
a needle of U+00A0 U+2003 (trimmed empty → omitted); Normal with
attachments shown and ghosts hidden with an Attachment (1/2) selected;
and "Alpha" (3/1) with the needle U+2003`alpha`U+00A0 trimmed to
`alpha`. Pinned by `graph_where_am_i_renders_every_state_exactly`,
`graph_where_am_i_witnesses_are_host_reachable` and
`graph_witnesses_obey_the_payload_invariants`.

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
`GraphFilterCount`; **`forceValue`** = `GraphForceValue`; **`settle`** =
`GraphLayoutSettled`; everything else immediate; a `High` graph event —
announced or relayed — flushes **and drops** every pending class.
200 ms latest-wins, each class independent (`GraphAnnouncer.swift:74–84,
185–218`); the production window is the test-visible constant
`GraphAnnouncer.defaultCoalesceWindow`, pinned at 0.2 s by
`testProductionCoalesceWindowIsTwoHundredMilliseconds` (the behavioural
suite runs on a 60 s window and flushes explicitly, so timing never
enters a test's outcome). Host rules that are not classes: the filter count's
**fire-time gate** (`:123–133, 204–207`) and `cancelPending` on view
departure or vault change (`:224–226`). Task 0a-2's mac tests cover:
latest-wins within each of the four classes, independence across all
six pairs, the window constant, the gate, the cancel, and a High event
— announced AND relayed, each with all four classes queued — flushing
all four; PR A's Windows announcer repeats the suite.

**0a-10 — No chord-bearing template.** No graph template carries a host
chord; the tier copy names "Table mode" by its label.

**0a-11 — The row copy is one helper, spoken and labelled alike.**
`a11y.rs::graph_row_copy(&GraphRowCopy) -> String` renders P1's
template (Standard grade) and is used by `GraphRow`'s render (after the
terse check) and by `GraphWhereAmI`'s `Node` clause. Manifest rows L1–L2
render `GraphRow` through `a11yRender`; `rowPhrase` and `GraphRowRef`
are deleted.

**0a-12 — The site manifest is the inventory.** The row unit is the
PHYSICAL site: one call expression, or one contiguous run of sibling
literals inside one control (a picker's options, a grid's column
titles). Every string a user can hear or read is in a row with its
role: `posted` / `static label` / `custom content` / `canonical relay` /
`composed label` (a label the host assembles from data — recorded so
both hosts compose it identically, §W-C) / `excluded` (a literal no
user hears or reads, with the evidence). A site the manifest lacks is a
round finding.

| # | Site | Current API / string | Role | Event or class | Pin |
|---|---|---|---|---|---|
| S1 | `AppState.swift:3236` | `.status("The graph is already open.")` | posted | `GraphStatus{AlreadyOpen}` | golden |
| S2 | `AppState+Connections.swift:114` | `.summary(hood.audioSummary)` | posted | `GraphNeighborhoodSummary` | 0a-7 tests |
| S3 | `AppState+Connections.swift:123` | `.error("Couldn't load connections: …")` | posted | `GraphBlocked{ConnectionsLoadFailed}` | golden |
| S4 | `AppState+Connections.swift:168` | `.status("Connections panel.")` | posted | `GraphStatus{ConnectionsPanel}` | golden |
| S5 | `AppState+Connections.swift:212` | `.reRooted(label:)` | posted | `GraphReRooted` | golden |
| S6 | `AppState+Connections.swift:236` | `.reRooted(label:)` | posted | `GraphReRooted` | golden |
| S7 | `AppState+Connections.swift:287` | `.status("Created note ….")` | posted | `GraphStatus{NoteCreated}` | golden |
| S8 | `AppState+Connections.swift:294` | `.error("Couldn't create note: …")` | posted | `GraphBlocked{NoteCreateFailed}` | golden |
| S9 | `AppState+GraphConfig.swift:131` (`:139–150`) | `announceForceValue(phrase)` | posted (force) | `GraphForceValue` | golden ×4 |
| S10 | `AppState+GraphDiagram.swift:177` | `announceSettle("Graph layout settled.")` | posted (settle) | `GraphLayoutSettled` | golden |
| S11 | `AppState+GraphDiagram.swift:197` | `.rowFocused(ref)` | posted (navigation) | `GraphRow` | matrix |
| S12 | `AppState+GraphDiagram.swift:231` | `.status("Unpinned.")` | posted | `GraphPinned{false}` | golden |
| S13 | `AppState+GraphDiagram.swift:235` | `.status("Pinned.")` | posted | `GraphPinned{true}` | golden |
| S14 | `AppState+GraphDiagram.swift:245` (`:252–267`, `:359–365`) | `.summary(whereAmIText)` | posted | `GraphWhereAmI` | 0a-6 tests |
| S15 | `AppState+GraphDiagram.swift:346` | `.status("Zoom … percent.")` | posted | `GraphZoom{fit:false}` | golden |
| S16 | `AppState+GraphDiagram.swift:355` | `.status("Fit graph. Zoom … percent.")` | posted | `GraphZoom{fit:true}` | golden |
| S17 | `AppState+GraphTable.swift:57` | `.status("Graph.")` | posted | `GraphStatus{Opened}` | golden |
| S18 | `AppState+GraphTable.swift:197` (`:336–354`) | `.status(graphPresetAnnouncement(…))` | posted | `GraphPreset` | golden ×4 |
| S19 | `AppState+GraphTable.swift:202` | `.summary(snap.audioSummary)` | posted | `GraphSnapshotSummary` | 0a-7 tests |
| S20 | `AppState+GraphTable.swift:204` (`:225–235`) | `announceFilterCount(…)` | posted (filter) | `GraphFilterCount` | golden |
| S21 | `AppState+GraphTable.swift:215` | `.error("Couldn't load the graph: …")` | posted | `GraphBlocked{LoadFailed}` | golden |
| S22 | `GraphDiagramView.swift:496–498` | `.status("Large graph: …")` | posted | `GraphTierEntered` | golden |
| S23 | `GraphTableView.swift:100` | `.status("Diagram mode.")` | posted | `GraphMode{Diagram}` | golden |
| S24 | `GraphTableView.swift:103` | `.status("Table mode.")` | posted | `GraphMode{Table}` | golden |
| S25 | `GraphTableView.swift:340–342` | `announceFilterCount("… shown", gate:)` | posted (filter) | `GraphFilterCount` | golden |
| S26 | `GraphTableView.swift:361` | `.status(a11yRender(event).text)` | canonical relay (priority dropped today) | `relay(A11yEvent)` (0a-13) | mac test |
| L1 | `ConnectionsPanel.swift:239` | `.accessibilityLabel(rowPhrase(row.rowRef))` | static label (AX name) | `GraphRow` rendered | matrix |
| L2 | `GraphDiagramView.swift:766, 841–843` | `setAccessibilityLabel(axLabel(…))` | static label (AX name) | `GraphRow` rendered | matrix |
| C1 | `GraphDiagramView.swift:851–869` | `AXCustomContent(label: "Connects to", value:)` | custom content | `GraphNeighborsContent` | golden ×5 |
| C2 | `GraphDiagramView.swift:627–629` | the tier summary element's label | static label | `GraphTierSummary` | golden ×2 |
| T1 | `ConnectionsPanel.swift:23` | `Select a note to see its connections.` | static label | label inventory (PR B) | §W-C |
| T2 | `ConnectionsPanel.swift:29` | navigation title `Connections` | static label | label inventory (PR B) | §W-C |
| T3 | `ConnectionsPanel.swift:78` | header `Connections` | static label | label inventory (PR B) | §W-C |
| T4 | `ConnectionsPanel.swift:93` | picker label `Local graph depth` | static label | label inventory (PR B) | §W-C |
| T5 | `ConnectionsPanel.swift:98–100` | the depth options `Links` / `2 links away` / `3 links away` | static label | label inventory (PR B) | §W-C |
| T6 | `ConnectionsPanel.swift:103–104` | AX `Local graph depth`; hint `How many links away from this note to include.` | static label | label inventory (PR B) | §W-C |
| T7 | `ConnectionsPanel.swift:121` | `Connections error: ⟨e⟩` | static label (the posted form is S3) | label inventory (PR B) | §W-C |
| T8 | `ConnectionsPanel.swift:125, 130` | `This note has no connections.` (visible and AX) | static label on mac; **posted on Windows** (0a-D3) | `GraphStatus{NoConnections}` | golden |
| T9 | `ConnectionsPanel.swift:137, 144` | `Loading connections…` visible / `Loading connections.` AX | static label on mac; **posted on Windows** (0a-D3) | `GraphStatus{LoadingConnections}` (the AX form) | golden |
| T10 | `ConnectionsPanel.swift:149` | section `Linked from`; empty `Nothing links here.` | static label | label inventory (PR B) | §W-C |
| T11 | `ConnectionsPanel.swift:150` | section `Links to`; empty `This note links to nothing.` | static label | label inventory (PR B) | §W-C |
| T12 | `ConnectionsPanel.swift:179` | `.accessibilityLabel(empty)` — T10/T11's empty strings as the AX name | static label | label inventory (PR B) | §W-C |
| T13 | `ConnectionsPanel.swift:188` | section header `⟨title⟩, ⟨n⟩ note` / `notes` (host-pluralised) | composed label | label inventory (PR B) — the plural rule recorded | §W-C |
| T14 | `ConnectionsPanel.swift:242` | row hint `Unresolved. Choose Create note to add it.` ‖ `Opens the note.` (or the disabled reason) | static label | label inventory (PR B) | §W-C |
| T15 | `ConnectionsPanel.swift:245` | row help `Create a note for this unresolved link.` ‖ `Open the note.` (or the disabled reason) | static label | label inventory (PR B) | §W-C |
| T16 | `ConnectionsPanel.swift:260–261` | action hint/help `disabledReason ?? action.title` | static label | label inventory (PR B); the titles are 0b's `graph_row_actions` | §W-C |
| T17 | `ConnectionsPanel.swift:303, 305, 307` | badges `Unresolved` / `Embed` / `Attachment` | static label | label inventory (PR B) | §W-C |
| T18 | `GraphTableView.swift:59` | navigation title `Graph` | static label | label inventory (PR A) | §W-C |
| T19 | `GraphTableView.swift:129` | `Graph diagram error: ⟨e⟩` | static label | label inventory (PR D) | §W-C |
| T20 | `GraphTableView.swift:133, 137` | `Laying out graph…` visible / `Laying out graph.` AX | static label | label inventory (PR D) | §W-C |
| T21 | `GraphTableView.swift:145` | picker `View` | static label | label inventory (PR A) | §W-C |
| T22 | `GraphTableView.swift:146–147` | the mode titles `Table` / `Diagram` (from the mode enum's `title`; 0a-18 moves them to the `GraphSurfaceMode` extension) | static label | label inventory (PR A) | §W-C |
| T23 | `GraphTableView.swift:152–153` | AX `Graph view mode`; hint `Switch between the accessible table and the visual diagram.` | static label | label inventory (PR A) | §W-C |
| T24 | `GraphTableView.swift:160` | field placeholder `Filter notes` | static label | label inventory (PR C) | §W-C |
| T25 | `GraphTableView.swift:164` | AX `Filter graph by note name` | static label | label inventory (PR C) | §W-C |
| T26 | `GraphTableView.swift:167–168` | toggle `Attachments`; hint `Include attachment nodes.` | static label | label inventory (PR C) | §W-C |
| T27 | `GraphTableView.swift:169–170` | toggle `Unresolved`; hint `Include unresolved link targets.` | static label | label inventory (PR C) | §W-C |
| T28 | `GraphTableView.swift:171–172` | toggle `Orphans only`; hint `Show only notes with no links in or out.` | static label | label inventory (PR C) | §W-C |
| T29 | `GraphTableView.swift:179` | button `Inspector` | static label | label inventory (PR E) | §W-C |
| T30 | `GraphTableView.swift:181–182` | help `Show the graph inspector — filters, colour groups, display, and forces.`; AX `Toggle graph inspector` | static label | label inventory (PR E) | §W-C |
| T31 | `GraphTableView.swift:252` | `No notes match the current filters.` | static label | label inventory (PR A) | §W-C |
| T32 | `GraphTableView.swift:297, 301` | `Loading graph…` visible / `Loading graph.` AX | static label | label inventory (PR A) | §W-C |
| T33 | `GraphTableView.swift:309` | `Graph error: ⟨e⟩` | static label (the posted form is S21) | label inventory (PR A) | §W-C |
| T34 | `GraphTableView.swift:351` | grid AX `Graph, data grid` | static label | label inventory (PR A) | §W-C |
| T35 | `GraphTableView.swift:482–484` | kind cells `Note` / `Attachment` / `Unresolved` | static label | label inventory (PR A) | §W-C |
| T36 | `GraphTableView.swift:614–622` | column titles `Note`, `Links in`, `Links out`, `Embeds in`, `Embeds out`, `Component`, `Modified`, `Folder`, `Kind` | static label | label inventory (PR A) | §W-C |
| T37 | `GraphInspectorView.swift:26` | AX `Graph inspector` | static label | label inventory (PR E) | §W-C |
| T38 | `GraphInspectorView.swift:32` | section `Filters` | static label | label inventory (PR E) | §W-C |
| T39 | `GraphInspectorView.swift:33–34` | field `Filter by name`; AX `Filter graph by note name` | static label | label inventory (PR E) | §W-C |
| T40 | `GraphInspectorView.swift:35–36` | toggle `Attachments`; hint `Include attachment nodes.` | static label | label inventory (PR E) | §W-C |
| T41 | `GraphInspectorView.swift:37–38` | toggle `Unresolved`; hint `Include unresolved link targets.` | static label | label inventory (PR E) | §W-C |
| T42 | `GraphInspectorView.swift:39–40` | toggle `Orphans only`; hint `Show only notes with no links in or out.` | static label | label inventory (PR E) | §W-C |
| T43 | `GraphInspectorView.swift:67` | section `Groups` | static label | label inventory (PR E) | §W-C |
| T44 | `GraphInspectorView.swift:69` | `No groups. Add one to colour matching nodes.` | static label | label inventory (PR E) | §W-C |
| T45 | `GraphInspectorView.swift:79–81` | button `Add Group`; hint `Add a colour rule that highlights nodes whose name matches a query.` | static label | label inventory (PR E) | §W-C |
| T46 | `GraphInspectorView.swift:87–88` | field `Query`; AX `Group ⟨n⟩ query` | composed label | label inventory (PR E) | §W-C |
| T47 | `GraphInspectorView.swift:89–93` | picker `Colour`; AX `Group ⟨n⟩ colour` | composed label | label inventory (PR E) | §W-C |
| T48 | `GraphInspectorView.swift:94–98` | picker `Ring`; AX `Group ⟨n⟩ ring style` | composed label | label inventory (PR E) | §W-C |
| T49 | `GraphInspectorView.swift:113` | AX `Remove group ⟨n⟩` | composed label | label inventory (PR E) | §W-C |
| T50 | `GraphInspectorView.swift:131` | section `Display` | static label | label inventory (PR E) | §W-C |
| T51 | `GraphInspectorView.swift:132–133` | toggle `Arrows`; hint `Draw arrowheads on directed links.` | static label | label inventory (PR E) | §W-C |
| T52 | `GraphInspectorView.swift:135–136` | slider `Text fade`; hint `Zoom level below which node labels hide.` | static label | label inventory (PR E) | §W-C |
| T53 | `GraphInspectorView.swift:138–139` | slider `Node size`; hint `Multiplier on node circle size.` | static label | label inventory (PR E) | §W-C |
| T54 | `GraphInspectorView.swift:141–142` | slider `Link thickness`; hint `Edge line width.` | static label | label inventory (PR E) | §W-C |
| T55 | `GraphInspectorView.swift:159` | section `Forces` | static label | label inventory (PR E) | §W-C |
| T56 | `GraphInspectorView.swift:161–162` | slider `Center`; hint `Gravity pulling the graph toward the centre.` | static label | label inventory (PR E) | §W-C |
| T57 | `GraphInspectorView.swift:164–165` | slider `Repel`; hint `How strongly nodes push each other apart.` | static label | label inventory (PR E) | §W-C |
| T58 | `GraphInspectorView.swift:167–168` | slider `Link force`; hint `How strongly linked nodes pull together.` | static label | label inventory (PR E) | §W-C |
| T59 | `GraphInspectorView.swift:170–171` | slider `Link distance`; hint `The ideal length of a link.` | static label | label inventory (PR E) | §W-C |
| T60 | `GraphInspectorView.swift:187–201` | `labeledSlider`: the title as AX label, the value `%.2f`, the hint | static label + value | label inventory (PR E); the spoken change is S9 | §W-C |
| T61 | `GraphDiagramView.swift:186` | `Graph, visual diagram` | static label | label inventory (PR D) | §W-C |
| T62 | `GraphDiagramView.swift:631` | custom action `Switch to Table` (tier B summary) | static label | label inventory (PR D) | §W-C |
| T63 | `GraphDiagramView.swift:1328` | custom action `Switch to Table` (the view) | static label | label inventory (PR D) | §W-C |
| T64 | `GraphDiagramView.swift:767` | AX value `pinned` ‖ empty | static value | label inventory (PR D) | §W-C |
| T65 | `GraphDiagramView.swift:791–795` | node help `Unresolved. Press to create note.` ‖ `Graph node. Press to open.` (or the disabled reason) | static label | label inventory (PR D) | §W-C |
| T66 | `GraphViewState.swift:27–31` | `GraphRowAction.title`: `Open`, `Open in New Tab`, `Show connections`, `Reveal in File Tree`, `Create note` — consumed as custom-action names at `GraphDiagramView.swift:803` and as hint/help at T16 | static label | 0b's `graph_row_actions` | §W-C |
| T67 | `GraphDiagramView.swift:811` | custom action `Unpin` ‖ `Pin` | static label | label inventory (PR D) | §W-C |
| T68 | `GraphDiagramView.swift:1115` | hover tooltip `⟨label⟩ — ⟨in⟩ in / ⟨out⟩ out` | composed label | label inventory (PR D) | §W-C |
| T69 | `GraphAnnouncer.swift` (rewritten), the `GraphVerbosity` extension | `title`: `Terse` / `Standard` / `Verbose` | static label | label inventory (PR C) | §W-C |
| T70 | `GraphAnnouncer.swift` (rewritten), the `GraphSurfaceMode` extension | `title`: `Table` / `Diagram` | static label | label inventory (PR A) | §W-C |
| X1 | `GraphConfigStore.swift:42–110, 251` | the store's thrown and logged error texts | **excluded — not user-facing**: a read failure is discarded and the defaults apply (`AppState+GraphConfig.swift:48–52`); a write failure reaches `NSLog` only (`GraphConfigStore.swift:251`); neither is spoken or shown | — | — |

**0a-13 — The one already-canonical site becomes a relay that keeps
the priority, on one seam with the typed announcements — the canvas
announcer's shape, plus the one gated overload the filter class
needs.** Task 0a-2 pins mac's API to `CanvasAnnouncer.swift:105–160`
with one addition: `announce(_ event: GraphA11yEvent)` →
`emit(.graph(event: event), coalescing: Self.coalescingClass(of:
event), gate: { true })`; `announceFilterCount(shown: UInt32, total:
UInt32, gate: @escaping () -> Bool)` — the ONLY gated entry, restricted
to the filter class so no other event can acquire a host relevance
gate — builds `.graphFilterCount(shown:total:)` and emits it with the
gate; `relay(_ event: A11yEvent)` → `emit(event, coalescing: nil, gate:
{ true })`; the private `emit(_ event: A11yEvent, coalescing:
EventClass?, gate:)` renders through `a11yRender`, stores the RENDERED
`(text, priority, gate)` for a coalesced class — the pending tuple
keeps the gate and re-evaluates it at fire time (0a-9) — and posts
through `AppKitAnnouncementPoster().post(text, priority:)` — never the
string primitive, never `.hostComposed`; a High render, coalesced or
not, flushes and drops every pending class before posting.
`GraphEvent` is deleted; `GraphRowRef` is replaced by the generated
`GraphRowCopy`; the two remaining text-taking posters become
`announce` calls on their events.

**0a-14 — Label-grade events are marked as such; "never spoken" is the
wrong word.** Two events are LABEL class — **not actively posted**;
assistive technology reads them as an element's name or content:
`GraphTierSummary` (C2) and `GraphNeighborsContent` (C1). Two are
dual-use by recorded divergence (0a-D3): `GraphStatus{NoConnections}`
and `GraphStatus{LoadingConnections}` are static labels on mac (T8, T9)
and posted on Windows when the leaf takes focus in that state. Every
other label in the manifest (T1–T7, T10–T70) is §W-C label class,
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
four slots — `GraphRow`, `GraphNeighborhoodSummary`, `GraphPreset` and
`GraphWhereAmI`'s row — each its own pair, each with its own zero/one/
many witnesses. "and 1 more" is grammatical and is not a defect. Bare
slots (no noun) are witnessed, not boundary-matrixed.

| Variant · slot | Reachable | Shipped form at one | Defect? |
|---|---|---|---|
| `GraphRow` · in_links / out_links (Note, Attachment) | 0, 1, many | `1 links in` / `1 links out` | yes, both |
| `GraphRow` · references (Ghost) | 0, 1, many | `1 references` | yes |
| `GraphSnapshotSummary` · notes | 0, 1, many | `1 notes` | yes |
| `GraphSnapshotSummary` · links | 0, 1, many | `1 links` | yes |
| `GraphSnapshotSummary` · orphans / unresolved | 0 (spoken only beside a non-zero partner; the pair is omitted when both are 0), 1, many | `1 orphans` / `1 unresolved targets` | yes, both |
| `GraphNeighborhoodSummary` · in_links / out_links | 0, 1, many | `1 links in` / `1 links out` | yes, both |
| `GraphNeighborhoodSummary` · note_count | 0, 1, many | `1 notes` | yes |
| `GraphNeighborhoodSummary` · depth | 1, 2, 3 (clamped) | `within 1 links` | yes |
| `GraphPreset` · Orphans.count / Unresolved.count | 0, 1, many | `1 orphaned notes` / `1 unresolved targets` | yes, both |
| `GraphPreset` · MostLinked.in_links | 0, 1, many | `1 links in` | yes |
| `GraphFilterCount` · shown / total | shown 0, 1, many; total 0 (empty graph), 1, many | — (no noun) | no |
| `GraphForceValue` · percent | 0…100 (bare) | — | no |
| `GraphZoom` · percent | positive (bare) | — | no |
| `GraphWhereAmI` · row.in_links / row.out_links (Note, Attachment) | 0, 1, many | `1 links in` / `1 links out` | yes, both |
| `GraphWhereAmI` · row.references (Ghost) | 0, 1, many | `1 references` | yes |
| `GraphWhereAmI` · component | 0, many (bare) | — | no |
| `GraphWhereAmI` · zoom_percent | positive (bare) | — | no |
| `GraphTierSummary` · count | many only (the tier begins above core's threshold) | `⟨n⟩ nodes` | 0 and 1 unreachable, declared |
| `GraphNeighborsContent` · labels | 0 (empty string), 1, many | — | no |
| `GraphNeighborsContent` · more (derived: `labels.len() − 10` when above the cap) | 0 (clause omitted), 1, many | `and 1 more` | no — grammatical |

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
neighbourhood string use `grouped_decimal` — the SEVEN summary counts
(the snapshot's four, the neighbourhood's `in_links`, `out_links` and
`note_count`), each with an above-999 witness in the matrix; every
other count — degrees in the row copy, the tier count
(`visibleIDs.count` interpolated bare, `GraphDiagramView.swift:628`),
presets, filter counts, percents, the depth — renders bare, and the
matrix's witnesses at 1024 and 2000 pin the bare behaviour.

**0a-17 — The witness matrix is ordered and pinned.** Seventy-one
entries, appended in this order after `CanvasStatus { Loading }`; the
artifact total becomes 519.

| Variant | Witnesses (in order) |
|---|---|
| `GraphRow` (10) | Standard Note "Alpha" 3/1 · Standard Ghost "Missing Note" references 2 · Standard Note "Pic" 1/0 embed · Terse Note "Alpha" · Verbose Note "Hub" 1024/0 (bare) · Standard Attachment "diagram.png" 0/2 · Standard Ghost "Draft" references 0 · Standard Ghost "Todo" references 1 · Standard Note "Alone" 0/0 · Standard Ghost "Missing" references 1 embed |
| `GraphReRooted` (1) | "Alpha" |
| `GraphSnapshotSummary` (7) | 247 / 1,032 / 12 / 3 · 1 / 1 / 0 / 0 (second sentence omitted) · 0 / 0 / 0 / 0 · 40 / 60 / 1 / 1, filtered · 5 / 9 / 0 / 2 · 3 / 4 / 2 / 0 (unresolved zero rendered) · 1,200 / 3,400 / 1,000 / 1,001 (all four grouped) |
| `GraphNeighborhoodSummary` (5) | "Alpha" 3/1, 7 notes, depth 1 · "Hub" 1,032/4, 1,206 notes, depth 3 · "diagram.png" 0/0, 0 notes, depth 2 · "Solo" 1/1, 1 notes, depth 1 · "Hub2" 4/1,032, 12 notes, depth 2 (out grouped) |
| `GraphPreset` (10) | Orphans 12 · Orphans 1 · Orphans 0 · Unresolved 1 · Unresolved 12 · Unresolved 0 · MostLinked "Hub" 1032 · MostLinked "Only" 0 · MostLinked "Solo" 1 · NoNotesToRank |
| `GraphFilterCount` (3) | 3 of 247 · 1 of 1 · 0 of 0 |
| `GraphForceValue` (4) | Center 50 · Repel 70 · Link 100 · LinkDistance 0 |
| `GraphLayoutSettled` (1) | — |
| `GraphPinned` (2) | true · false |
| `GraphZoom` (2) | 125 plain · 63 fit |
| `GraphMode` (2) | Table · Diagram |
| `GraphWhereAmI` (7) | Node "Alone" 0/0 component 2, zoom 100, Normal{orphans only, attachments shown, ghosts shown}, needle "alo" · Node Ghost "Missing Note" references 2, component 0, zoom 250, UnresolvedOnly, needle None · NoSelection, zoom 100, Normal{false, false, ghosts shown}, needle Some(U+00A0 U+2003) (trimmed empty → omitted) · Node Attachment "diagram.png" 1/2 component 7, zoom 50, Normal{false, attachments shown, ghosts hidden} · Node "Alpha" 3/1 component 2, zoom 100, Normal{false, false, ghosts shown}, needle Some(U+2003 `alpha` U+00A0) (trimmed to `alpha`) · Node Ghost "Draft" references 0, component 1, zoom 100, UnresolvedOnly · Node Ghost "Todo" references 1, component 3, zoom 80, UnresolvedOnly |
| `GraphTierEntered` (1) | — |
| `GraphTierSummary` (2) | 2000 · 1501 |
| `GraphNeighborsContent` (5) | ten labels (the cap, no overflow) · eleven labels (`and 1 more`) · fifty-two labels (`and 42 more`) · one label · labels [] |
| `GraphStatus` (6) | Opened · AlreadyOpen · ConnectionsPanel · NoteCreated "Draft.md" · NoConnections · LoadingConnections |
| `GraphBlocked` (3) | LoadFailed "io error" · ConnectionsLoadFailed "io error" · NoteCreateFailed "exists" |

10 + 1 + 7 + 5 + 10 + 3 + 4 + 1 + 2 + 2 + 2 + 7 + 1 + 2 + 5 + 6 + 3 = **71**;
448 + 71 = **519**. This table is the authority, the arithmetic is
shown, and `the_graph_family_witness_count_is_pinned` asserts 71.

**0a-19 — The matrix is executable and lossless.**
`the_graph_witnesses_are_exactly_the_matrix_in_order` asserts the graph
entries of `corpus()`, rendered as their full Debug identities (the
same strings the artifact's `event` field carries — every field, no
markers), equal an ordered literal list of the 71 identities above;
deleting, substituting, reordering or editing ANY field of a witness
fails here on its own, not only in the golden and the artifact (the
E13 precedent, `file_type_not_openable_has_exactly_its_two_ordered_witnesses`,
made exact for a family whose payloads are not single fields).
Mutation-verified in the record.

**0a-18 — Mac consumes in the same PR (Task 0a-2); every migration is
owned by name.** Every S-row's composition is replaced by `announce` /
`announceFilterCount` / `relay`; L1–L2 render `GraphRow`; C1–C2 render
their events; S20 and S25 collapse to one event; `GraphEvent`,
`GraphRowRef`, `rowPhrase`, `forcesChangePhrase` (replaced by
`changedForce(old:new:) -> (control, percent)?`), `graphPresetAnnouncement`
(replaced by `graphPresetEvent(_:snap:) -> GraphA11yEvent`),
`graphFilterCountText` (replaced by `graphFilterCount(_:) -> (shown:
UInt32, total: UInt32)`, the payload of the ONE gated entry
`announceFilterCount(shown:total:gate:)` — a tuple rather than an event
because the event is constructed inside the gated entry and nowhere
else), `graphDiagramWhereAmIText` (replaced by
`graphDiagramWhereAmIEvent() -> GraphA11yEvent?`) and
`graphDiagramFilterPhrase` (its clause is core's) are deleted, and
their callers migrate with them: GraphCommandsTests
`testPresetAnnouncementStringsAreVerbatim` (`:76`) and
`testMostLinkedOnEmptyGraph` (`:110`) assert typed outcomes;
GraphTabRoutingTests `testGraphFilterCountText` (`:161`, renamed
`testGraphFilterCountFollowsTheNeedle`) asserts the tuple and
`testPresetAnnouncesOnceFromFreshSnapshotNoReplay`
(`:241`) renders the event for its expected text;
`GraphConfigTests.testForcesChangePhraseNamesTheOneChangedControl`
(`:97`) asserts the (control, percent) pair; GraphDiagramTests `:317`
asserts the readback is nil without a diagram and `:394–414` renders the
event and asserts the 0a-D1 correction (verbosity does not reach it).
The two remaining `GraphRowRef` constructors (`ConnectionsPanel.swift:365–369`,
`GraphDiagramModel.swift:97–106`) become `rowCopy` accessors over
`GraphRowCopy`, the connections row's kind from `isGhost` /
`isAttachment`. Generated-type migrations: the seven Swift fixtures
that construct the records gain `summaryCounts:` (`GraphCommandsTests.swift:70`
keeps its snapshot helper; `ConnectionsPanelTests.swift:78, 111, 133,
153, 166, 176`); the local `GraphTabMode` (`GraphTableView.swift:9–19`)
is deleted for the generated `GraphSurfaceMode` at every type site —
`GraphConfig.mode` (`GraphConfig.swift:18`), `setGraphMode`
(`AppState+GraphConfig.swift:178`), the container's `@State`
(`GraphTableView.swift:27`) and its picker's `allCases` (`:146`) — with
a Swift extension supplying `CaseIterable`, `title` (`Table` /
`Diagram`), and the persistence mapping the store used through
`rawValue`: `persistenceTag` writes the literal tags **`table`** and
**`diagram`** (`GraphConfigStore.swift:192`), `init?(persistenceTag:)`
reads them (`:162`), and an unknown tag decodes to `nil` so `config.mode`
keeps its default `.table` — the v1 file is unchanged on disk; the
generated `GraphVerbosity` gets the extension of 0a-5. The mac tests:
GraphAnnouncerTests's copy assertions (`:25–51`) move to the Rust
golden; GraphAnnouncerTests keeps the 0a-9 suite and the funnel
guard — which strips `//` comment lines before scanning, so the
announcer's own doc comment may name the primitive it forbids;
`A11yResidueCensusTests.pinnedResidueSites` reads **28**;
`a11y.rs:14–38`'s module doc is rewritten so only the structural-
mutation builder remains a named host-composed exception, pinned by
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
  host rules, not events;** the gate has exactly one typed entry point
  (0a-13).
- **0aD-4 — A row's kind reuses `GraphNodeKind`**, with the reverse
  mapping added; no second kind enum.
- **0aD-5 — The shipped string is the copy authority; the P specs'
  variants are recorded, not adopted** (0a-16).
- **0aD-6 — Verbosity persistence is 0b's and PR C's, not 0a's.** 0a
  lands the parameter and the Swift extension; the schema key and the
  menu follow; mac's missing switch is a filed gap.
- **0aD-7 — Payload shape follows host reachability** (design B(i)):
  booleans only where every combination is reachable; otherwise arms;
  every remaining invariant named in 0a-2b's table and asserted over
  the matrix. A rule a host applied before handing data to core (the
  neighbour cap) is core's.
- **0aD-8 — The governing spec names events and never quotes copy**
  (design B(ii)); recorded as a §5 rule of `w6_2_graph_spec.md`.

### Recorded divergences (owner-recorded; off-limits for re-litigation)

- **0a-D1 — Where-am-I speaks the full row copy at every verbosity, on
  both hosts** — a correction of mac's terse coupling (0a-6).
- **0a-D2 — The grid relay keeps core's priority** (0a-13).
- **0a-D3 — The Connections panel's empty and loading states are
  posted on Windows when the leaf takes focus in them**, static labels
  on mac (T8, T9); PR B pins Windows's.
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
| IG0a-2 | BLOCKER | taken — 0a-2b is a literal schema with every field and width; `GraphPresetOutcome` and `GraphWhereAmISelection` close the invalid combinations; `GraphNodeKind` reused |
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
| IG0a-17 | BLOCKER | taken — 0a-2b's two literal tables; the spec's §4 amended to this schema (five places, one `GraphForceValue`, no GraphFilterPhrase, typed summaries, the Where-am-I policy, PR E's consumption) |
| IG0a-18 | BLOCKER | taken — the matrix recounted with the arithmetic shown, an Attachment witness added, the two figures in 0a-2 corrected |
| IG0a-20 | BLOCKER | taken — a path-aware inventory naming `graph.rs` for `NodeKind`; both conversion directions required; the Windows coalescing assertion explicitly deferred to PR A, the false claim removed |
| IG0a-19 | MAJOR | taken — 0a-19's exact-ordered-witness test |
| IG0a-21 | MAJOR | taken — 0a-15 classifies by (variant, slot) with reachability; "and 1 more" struck; the matrix carries the boundaries |
| IG0a-22 | MAJOR | taken — 0a-7 names Link+Embed for `links`, full-graph Link-only degrees for the neighbourhood, and adds the independent field test |
| IG0a-23 | MAJOR | taken — 0a-18 owns the seven fixtures, deletes the local `GraphTabMode` for `GraphSurfaceMode`, and specifies the persistence extension |
| IG0a-24 | MAJOR | taken — the manifest gains the diagram and inspector rows; T13's composed plural recorded for PR B |
| IG0a-25 | MAJOR | taken — 0a-5 and 0aD-6: default-only in 0a on both hosts, the key in 0b, the Swift extension here; the false "already writes" claim removed; the mac gap recorded |
| IG0a-26 | MAJOR | taken — 0a-13 is the canvas-compatible shape: rendered `(text, priority)` pending, AppKitAnnouncementPoster |
| IG0a-27 | MAJOR | taken — the tier count renders bare (0a-16); the witness is 2000 |
| IG0a-28 | MINOR | taken — `NoSelection` |

### Round 3 — twelve findings, dispositions; rule 4 invoked

Round 3's verdict: revise again, but stop for a design pass first —
blockers had recurred for three rounds in two subsystems. The design
pass above is that stop; the dispositions follow from it.

| Finding | Severity | Disposition |
|---|---|---|
| IG0a-29 | BLOCKER (created by rev 3) | taken — 0a-13 adds the one gated entry, `announceFilterCount(shown:total:gate:)`, restricted to the filter class; the pending tuple keeps the gate |
| IG0a-30 | BLOCKER (carried ×3) | taken by design A — `NestedEnum { core, mirror, path, sites }` with named fields; the tripwire reads `mirror:` and compares the mirror-named FFI set |
| IG0a-31 | BLOCKER (carried) | taken by design B(ii) — PR E's sentence replaced by the two named events in order; every quoted announcement removed from the spec's §4; the §5 rule added |
| IG0a-32 | BLOCKER (created by rev 3) | **refuted** — `A11yResidueCensusTests` is in the citation census's auditable external allow-list (`ContractsCitationCensus.cs:1372–1376`, "the mac twins these sections compare against"), and `GraphContractsCitationCensus` passed 2/2 over revision 3 on this box before it was pushed; the floor stays at the measured population less one |
| IG0a-33 | BLOCKER (carried) | taken by design B(i) — `GraphWhereAmIFilter { Normal {…}, UnresolvedOnly }`; the selection-kind invariant stated as the host's and asserted over the matrix; the two unreachable witnesses replaced |
| IG0a-34 | MAJOR | taken — the slot table gains the Where-am-I row slots and zoom; the matrix gains Unresolved 0, MostLinked 1, one label, and a snapshot rendering `0 unresolved targets`; 69 / 517 |
| IG0a-35 | MAJOR | taken — 0a-19 compares full Debug identities, lossless |
| IG0a-36 | MAJOR | taken — 0a-18 names every caller, every type site, the two tags and the unknown-tag result; the snapshot fixture gains `summaryCounts` |
| IG0a-37 | MAJOR | taken — the row unit defined (physical site); the manifest rescanned literally (every string literal in `Sources/SlateMac/Graph`), T1–T69 and X1 |
| IG0a-38 | MAJOR | taken — the Ghost + embed witness (GraphRow 10) |
| IG0a-39 | MINOR | taken — the trimmed needle and its whitespace set stated; the padded-needle witness |
| IG0a-40 | MINOR | taken — seven grouped counts, each with an above-999 witness |

### Round 4 — ten findings, dispositions

Round 4's verdict: Subsystem A's design closed its class; Subsystem B's
did not — one Where-am-I witness and the neighbour content still
admitted unreachable states, and the governing spec still quoted copy.
Process deviation, recorded: the round read the working tree while the
implementation of revisions 1–4 was applied locally and a byte-restoring
mutation pass ran, so findings 43–50 cite the implementation; each was
verified against the restored tree before it was counted (session
lesson: a codex round reads the local tree, not the pushed head).

| Finding | Severity | Disposition |
|---|---|---|
| IG0a-41 | BLOCKER (carried) | taken — every quoted announcement template in the spec is gone (§0 items 3 and 9, §2 row D, §4 PR 0a item 5, PR B's re-root); the §5 rule distinguishes a template (never quoted) from a control's NAME (quoted where it names the control) |
| IG0a-42 | BLOCKER (carried) | taken — GraphFilterPhrase deleted from PR C; every PR's Consumes list matches the schema (PR A + `GraphSnapshotSummary`; PR B + `GraphNeighborhoodSummary`, `GraphBlocked`) |
| IG0a-43 | BLOCKER (carried) | taken — the orphan and needle invariants in 0a-2b's table; the first witness is an orphan Note ("Alone" 0/0) with a needle it contains; `graph_witnesses_obey_the_payload_invariants` |
| IG0a-44 | BLOCKER (missed) | taken — `GraphNeighborsContent { labels }` carries the full list and core owns GRAPH_NEIGHBOR_LABEL_CAP; witnesses at 10, 11, 52, 1, 0; the whole schema audited (the invariants table) |
| IG0a-45 | MAJOR (carried) | taken — the Where-am-I row slots require zero/one/many; two Ghost witnesses (references 0, 1) and the Attachment at 1/2; 71 / 519 |
| IG0a-46 | MAJOR (carried) | taken — T66 cites the literal site, T69/T70 split, X1 excluded with the evidence that neither failure is user-facing |
| IG0a-47 | MAJOR (carried) | taken — `GraphAnnouncer.defaultCoalesceWindow` pinned at 0.2 s; the relayed-High case queues all four classes |
| IG0a-48 | MAJOR (carried) | taken — 0a-18 states the tuple feeds the one gated entry; the routing test renamed |
| IG0a-49 | BLOCKER (created by rev 4) | taken — the funnel guard strips `//` comment lines before scanning |
| IG0a-50 | MINOR (carried) | taken — U+00A0 and U+2003 witnesses, all-whitespace and padded |

### Tests that pin PR 0a

`crates/slate-core/src/a11y.rs`: `corpus_renders_the_shipped_strings`
(71 golden rows), `committed_corpus_artifact_matches_the_vocabulary`
(519), `every_graph_variant_and_arm_is_represented_in_the_corpus`,
`every_graph_parameter_enum_is_listed_for_coverage`,
`the_graph_family_occupies_one_top_level_variant`,
`a11y_event_top_level_count_is_pinned`,
`graph_verbosity_matrix_pins_every_level`,
`graph_where_am_i_renders_every_state_exactly`,
`graph_where_am_i_witnesses_are_host_reachable`,
`graph_witnesses_obey_the_payload_invariants`,
`graph_priorities_pin_the_error_tier`,
`graph_count_slots_render_at_every_reachable_cardinality`,
`graph_plural_defects_are_exactly_these_slots`,
`the_graph_family_witness_count_is_pinned`,
`the_graph_witnesses_are_exactly_the_matrix_in_order`,
`the_module_doc_names_no_engine_but_the_mutation_builder`.
`crates/slate-core/src/graph_summary.rs` and `session/tests/graph.rs`:
`summary_counts_are_the_index_counts`,
`summary_events_render_the_snapshot_fields_verbatim`.
`crates/slate-uniffi/src/lib.rs`: the generalised
`the_ffi_mirror_covers_every_core_a11y_variant` (both inventories, the
graph's by key), the two family-qualified corpus-order tests, the
three coalescing switch-list tests (mac canvas, mac graph, Windows
canvas — Windows graph from PR A). `apps/slate-windows/.../A11yCorpusCensus.cs`:
`EveryCorpusEventRendersTheCommittedIdentityTextAndPriority` over the
graph entries. `apps/slate-mac/Tests/SlateMacTests/`:
A11yCorpusCensusTests (the graph entries through the real FFI),
GraphAnnouncerTests (the 0a-9 suite; the window constant; the funnel guard),
GraphDiagramTests (the readback event and the 0a-D1 correction),
`A11yResidueCensusTests` (`pinnedResidueSites` 29 → **28**).

<!-- end of the graph contracts document -->
