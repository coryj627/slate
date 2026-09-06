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
is at revision 6: round 1 returned sixteen findings, round 2 twelve,
round 3 twelve, and round 3's verdict invoked the protocol's stopping
rule 4 — three consecutive rounds of blockers in one subsystem, twice
over — so revision 4 opened with the design pass that rule demands.
Round 4 found Subsystem A closed by it and Subsystem B not yet; revision
5 audited the whole schema against design B(i)'s reachability rule;
round 5 froze Subsystem A and returned seven findings on B — the row
copy's two CONSTRUCTORS impose relations the audit had not modelled,
and two spec spots remained — taken in revision 6.

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
the ordered list of the graph's entries: **73** of them, the artifact
growing from 448 to **521**; `the_graph_witnesses_are_exactly_the_matrix_in_order`
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
| `GraphRowCopy` | built by exactly two constructors — the Connections row (`ConnectionsPanel.swift:424–435`: `references = in_links + in_embeds`, `embed` = reached by embeds only) and the diagram row (`GraphDiagramModel.swift:97–106`: `references = in_links`, `embed = false`) — so `references ≥ in_links` always, with equality from the diagram; an Attachment or a Ghost has no file to link FROM, so `out_links == 0` for both, and an embed-only relationship INTO one is an incoming embed, so `embed` on a Ghost or Attachment implies `references > in_links`; a row without the embed flag whose references exceed its link degree still has a degree (`in_links > 0 || out_links > 0`); a Ghost's copy speaks `references`, a Note's or Attachment's its degrees; `embed` is independent of `kind` |
| `GraphSnapshotCounts` | `orphans ≤ notes` — an orphan is a Note; `links > 0 ⇒ notes > 0` — every edge originates from a Note; `unresolved ≤ links` — every retained ghost was created by a retained reference (`session.rs::snapshot_summary_counts`) |
| `GraphNeighborhoodCounts` | `in_links > 0 ∨ out_links > 0 ⇒ note_count > 0` — a linked centre has a depth-one Note neighbour or is one (`session.rs:1949–1988`) |
| `GraphNeighborhoodCounts.depth` | 1..=3 — the session clamps before it exports |
| `GraphFilterCount` | `shown ≤ total` — the needle narrows the fetched set |
| `GraphForceValue.percent` | 0..=100 — a slider's value ×100, rounded |
| `GraphZoom.percent`, `GraphWhereAmI.zoom_percent` | 10..=400 — the shared viewport clamps its scale to 0.1…4.0 (`CanvasRendererView.swift:12–13`) |
| `GraphWhereAmI` | the selection is a DIAGRAM row (`references == in_links`, `embed == false`, the kind rules above); a Ghost only while ghosts are shown (Normal with `ghosts_shown`, or UnresolvedOnly), an Attachment only while attachments are shown, a Note only under Normal; under `orphans_only` the selection is a Note with `in_links == 0 && out_links == 0`; under a non-empty needle the selected label contains the trimmed needle under mac's predicate — case- AND diacritic-insensitive (`graphNameMatches`, `AppState+GraphConfig.swift:16–20`; core folds with NFD, drops combining marks, lowercases) |
| `GraphTierSummary.count` | above the tier-B threshold (1,500 as shipped, `GraphDiagramView.swift`; §2 row H moves the constant) |
| `GraphNeighborsContent.labels` | one label per unique visible neighbour ID, in the host's traversal order — the TEXT may repeat (`a/Foo.md` and `b/Foo.md` both say `Foo`); the cap is core's |

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
space) and U+2003 (em space), not ASCII spaces alone, and the
diacritic-insensitive match with "Café" under the needle `cafe`. The
filter clause is the closed `GraphWhereAmIFilter` (design B(i)); the
payload invariants of 0a-2b are the host's and the matrix obeys them.
The eight matrix witnesses cover: an orphan Note ("Alone", 0/0) under
Normal with all three toggles on and a needle it contains;
UnresolvedOnly with a Ghost selected at each reference cardinality
(2, 0, 1 — each with `in_links == references`, the diagram's
constructor); NoSelection with a needle of U+00A0 U+2003 (trimmed
empty → omitted); Normal with attachments shown and ghosts hidden with
an Attachment (1 in, 0 out) selected; "Alpha" (3/1) with the needle
U+2003`alpha`U+00A0 trimmed to `alpha`; and "Café" (2/4) under the
needle `cafe`. Pinned by `graph_where_am_i_renders_every_state_exactly`,
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
| T71 | `GraphConfig.swift:109–110, 125` | `GraphColorToken.title` — the colour picker's options, `rawValue.capitalized`: `Red`, `Orange`, `Yellow`, `Green`, `Teal`, `Blue`, `Purple`, `Pink` (consumed by T47's picker, `GraphInspectorView.swift:89–93`) | static label | label inventory (PR E), the eight ordered | §W-C |
| T72 | `GraphConfig.swift:130–131` | `GraphRingStyle` — the ring picker's options, `rawValue.capitalized`: `Solid`, `Dashed`, `Double`, `Dotted` (consumed by T48's picker, `GraphInspectorView.swift:94–98`) | static label | label inventory (PR E), the four ordered | §W-C |
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
other label in the manifest (T1–T7, T10–T72) is §W-C label class,
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
| `GraphZoom` · percent | 10..=400 (bare; the viewport's clamp) | — | no |
| `GraphWhereAmI` · row.in_links / row.out_links (Note, Attachment) | 0, 1, many | `1 links in` / `1 links out` | yes, both |
| `GraphWhereAmI` · row.references (Ghost) | 0, 1, many | `1 references` | yes |
| `GraphWhereAmI` · component | 0, 1, many (bare) | — | no |
| `GraphWhereAmI` · zoom_percent | 10..=400 (bare; the viewport's clamp) | — | no |
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

**0a-17 — The witness matrix is ordered and pinned.** Seventy-three
entries, appended in this order after `CanvasStatus { Loading }`; the
artifact total becomes 521. Every row copy obeys its constructor: a
row's `references` is written beside its degrees as `in/out/refs`.

| Variant | Witnesses (in order) |
|---|---|
| `GraphRow` (10) | Standard Note "Alpha" 3/1/3 · Standard Ghost "Missing Note" 2/0/2 · Standard Note "Pic" 1/0/1 embed · Terse Note "Alpha" 3/1/3 · Verbose Note "Hub" 1024/3/1024 (bare) · Standard Attachment "diagram.png" 2/0/2 · Standard Ghost "Draft" 0/0/0 · Standard Ghost "Todo" 1/0/1 · Standard Note "Alone" 0/0/0 · Standard Ghost "Missing" 0/0/1 embed (an embed-only ghost: its one reference is the embed) |
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
| `GraphWhereAmI` (8) | Node "Alone" 0/0/0 component 2, zoom 100, Normal{orphans only, attachments shown, ghosts shown}, needle "alo" · Node Ghost "Missing Note" 2/0/2, component 0, zoom 250, UnresolvedOnly, needle None · NoSelection, zoom 100, Normal{false, false, ghosts shown}, needle Some(U+00A0 U+2003) (trimmed empty → omitted) · Node Attachment "diagram.png" 1/0/1 component 7, zoom 50, Normal{false, attachments shown, ghosts hidden} · Node "Alpha" 3/1/3 component 2, zoom 100, Normal{false, false, ghosts shown}, needle Some(U+2003 `alpha` U+00A0) (trimmed to `alpha`) · Node Ghost "Draft" 0/0/0, component 1, zoom 100, UnresolvedOnly · Node Ghost "Todo" 1/0/1, component 3, zoom 80, UnresolvedOnly · Node "Café" 2/4/2 component 5, zoom 200, Normal{false, false, ghosts shown}, needle "cafe" |
| `GraphTierEntered` (1) | — |
| `GraphTierSummary` (2) | 2000 · 1501 |
| `GraphNeighborsContent` (6) | ten labels (the cap, no overflow) · eleven labels (`and 1 more`) · fifty-two labels (`and 42 more`) · one label · labels [] · two neighbours both labelled "Foo" (text repeats, ids differ) |
| `GraphStatus` (6) | Opened · AlreadyOpen · ConnectionsPanel · NoteCreated "Draft.md" · NoConnections · LoadingConnections |
| `GraphBlocked` (3) | LoadFailed "io error" · ConnectionsLoadFailed "io error" · NoteCreateFailed "exists" |

10 + 1 + 7 + 5 + 10 + 3 + 4 + 1 + 2 + 2 + 2 + 8 + 1 + 2 + 6 + 6 + 3 = **73**;
448 + 73 = **521**. This table is the authority, the arithmetic is
shown, and `the_graph_family_witness_count_is_pinned` asserts 73.

**0a-19 — The matrix is executable and lossless.**
`the_graph_witnesses_are_exactly_the_matrix_in_order` asserts the graph
entries of `corpus()`, rendered as their full Debug identities (the
same strings the artifact's `event` field carries — every field, no
markers), equal an ordered literal list of the 73 identities above;
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

### Round 5 — seven findings, dispositions; Subsystem A frozen

Round 5's verdict: Subsystem A may freeze at revision 5 (its
named-field inventory, the keyed tripwire, both conversion directions
and the census checks all verified); Subsystem B's audit had skipped
the row copy's constructor relations and two spec spots. Taken in
revision 6.

| Finding | Severity | Disposition |
|---|---|---|
| IG0a-51 | BLOCKER (carried) | taken — the invariants table models the two constructors (`references ≥ in_links`, equality from the diagram; Attachment and Ghost `out_links == 0`), `orphans ≤ notes`, zoom 10..=400; `graph_witnesses_obey_the_payload_invariants` asserts every `GraphRow`, `GraphSnapshotSummary`, `GraphZoom` and Where-am-I witness against them; every row copy in the matrix rewritten with its `references` |
| IG0a-52 | BLOCKER (carried) | taken — Where-am-I's rows are diagram rows (`references == in_links`, `embed == false`); an eighth witness ("Café" 2/4) carries the out-many cell; 73 / 521 |
| IG0a-53 | BLOCKER (carried) | taken — §2 row A names the events; the one quoted string left in the spec is the control name "Connects to" |
| IG0a-54 | BLOCKER (carried) | taken — PR A's empty case no longer cites a preset; PR C says the headline supersedes the summary (mac's rule, `AppState+GraphTable.swift:195–199`); PR D consumes `GraphRow` and `GraphMode` |
| IG0a-55 | MAJOR (created by rev 5) | taken — one label per unique neighbour ID, text may repeat; the uniqueness assertion removed; the "Foo, Foo" witness |
| IG0a-56 | MAJOR (created by rev 5) | taken — the needle predicate is mac's, case- and diacritic-insensitive; core folds with NFD and drops combining marks; the "Café"/`cafe` witness |
| IG0a-57 | MAJOR (missed) | taken — T71 and T72, the two ordered option sets |

### THE FREEZE — revision 6 stands

Round 4's verdict: revise again — Subsystem A frozen (round 5), Subsystem B(ii) frozen (round 6), Subsystem B(i) with one blocker CREATED by revision 6's fix (an unconstructible embed-only ghost tuple), one earlier-round miss and one minor. The section is frozen at revision 4
on 2026-09-03; the round-4 findings below are the ledger the task
loop discharges by code. Precedent applied; the owner may overrule.

### Round 6 — three findings; the ledger

| Finding | Severity | Disposition |
|---|---|---|
| IG0a-58 | BLOCKER (created by rev 6) | ledger — discharged by code: the witness is the embed-only ghost `0/0/1 embed`; `row_copy_is_constructible` gains the two rules (embed on a Ghost or Attachment ⇒ `references > in_links`; references above the link degree without the flag ⇒ a degree); 0a-2b and 0a-17 corrected in the frozen text as the discharge |
| IG0a-59 | BLOCKER (missed) | ledger — discharged by code: `links > 0 ⇒ notes > 0`, `unresolved ≤ links`, and the neighbourhood's `in ∨ out ⇒ note_count > 0` named in 0a-2b and asserted by `graph_witnesses_obey_the_payload_invariants` |
| IG0a-60 | MINOR (missed) | ledger — discharged in the text: 0a-15's zoom rows read 10..=400, the component row 0, 1, many |

The freeze applies the standing precedent (E13's: a round whose blocker
was created by the previous fix, rule 5, with the remaining findings
carried as a ledger the task loop discharges by code) — **precedent
applied; the owner may overrule.** Subsystem A was frozen by round 5,
B(ii) by round 6; B(i)'s three items are the ledger above, every one
discharged before the PR opened.

### Task loop — records

**TG0a-0 — Baselines.** `A11yEvent` 198 top-level variants;
`tests/fixtures/a11y/corpus.json` 448 entries; CANVAS_NESTED_ENUMS 18
families; `A11yResidueCensusTests.pinnedResidueSites` 29;
`GraphAnnouncer.swift` 239 lines with one `// W0.5-3 residue:` site; the
graph citation census floor 7 over 8 (revision 4, before the bindings).

**TG0a-1 — Core: the count records, the formatter, the session.**
`graph.rs` gains `GraphSnapshotCounts` and `GraphNeighborhoodCounts`
with the semantics of 0a-7 in their doc comments and a `summary_counts`
field on each record; `graph_summary.rs` (new) holds `snapshot_summary`
and `neighborhood_summary`; `session.rs`'s `snapshot_audio_summary`
becomes `snapshot_summary_counts` and the neighbourhood composer is
gone — both `audio_summary` strings are the formatter over the exported
counts. Facts: `summary_counts_are_the_index_counts` (a vault with a
count-2 link, an embed, a ghost, a far note beyond depth 1 and an
attachment centre: `links` 6 under the default filter and 7 with
attachments shown; the centre's degrees 2/1 at depth 1 AND depth 2;
`note_count` 2 then 3; the attachment centre 0/0 with one note) and
`summary_events_render_the_snapshot_fields_verbatim`; the two module
tests of `graph_summary.rs`; the 36 pre-existing session graph facts
unchanged, the two shipped summary strings among them.

**TG0a-2 — Core: the family.** `a11y.rs` gains the graph section —
`GraphVerbosity`, `GraphRowCopy`, `GraphPresetOutcome`,
`GraphForceControl`, `GraphSurfaceMode`, `GraphWhereAmISelection`,
`GraphWhereAmIFilter`, `GraphStatusNote`, `GraphBlockedReason`,
`GraphA11yEvent` (17 variants), `graph_row_copy`, the `priority()` /
`render()` pair — `A11yEvent::Graph { event }` with its two delegation
arms (198 → 199), the graph rows of the ONE class-key list, the module
doc rewritten (the graph is no longer a named engine; only the
structural-mutation builder remains), `graph_corpus()` (73 witnesses,
appended after `CanvasStatus { Loading }`), 73 golden rows, and the
tests of 0a's list — every one generated from one matrix source
(`g0a-matrix.py` in the session scratch: the Rust corpus body, the
golden rows, the 73 Debug identities and both host mirrors are five
renderings of one table, so the five places cannot disagree). The
artifact regenerated through its own path: 448 → 521, 73 graph entries.
Facts green: `cargo test -p slate-core --lib a11y::` (25, the 16 graph
tests among them), `graph_summary::` (2), `session::tests::graph::` (36).

**TG0a-3 — FFI.** `lib.rs` mirrors every type of 0a-2b (`GraphWhereAmIFilter`
included), adds `From<GraphNodeKind> for core::graph::NodeKind`, the two
count records both ways, `summary_counts` on the two graph records, and
the `Graph` arm; the tripwires generalised as 0a-3 says:
`pinned_coalescing_classes(core_source, family)` reads one family's
markers; `the_mac_graph_coalescing_switch_matches_the_pinned_class_list`
(new) over `GraphAnnouncer.swift`; the Windows corpus-order parser
derives the inner marker from the wrapper; the FFI mirror test reads
the canvas tuples as before and the graph's `NestedEnum` rows BY KEY
(`core:`, `mirror:`, `path:`), opens the named source, compares the
mirror-named set, and pins the nine graph arm counts. Facts green:
`cargo test -p slate-uniffi --lib -- a11y corpus_mirror coalescing
ffi_mirror` (7). Clippy clean over both crates; `cargo fmt --check`
clean.

**TG0a-4 — The census mirrors.** 73 entries appended to
`A11yCorpusCensus.cs` and `A11yCorpusCensusTests.swift` after the
canvas's last, in matrix order, from the one matrix source. Bindings
regenerated (`generate-bindings.ps1`; 83 mentions of `GraphA11yEvent`
in the generated C#); `A11yCorpusCensus` and
`GraphContractsCitationCensus` green on the fresh Windows build (4).

**TG0a-5 — Task 0a-2, the mac host.** `GraphAnnouncer.swift` rewritten
to 0a-13's API (`announce`, `announceFilterCount(shown:total:gate:)`,
`relay`; the private `emit` over `a11yRender`, pending `(text,
priority, gate)`, AppKitAnnouncementPoster, the High flush) with the
`GraphVerbosity` and `GraphSurfaceMode` extensions (0a-5, 0a-18); every
S-row migrated by name (S1–S26), L1–L2 and C1–C2 rendering their events,
the two `GraphRowRef` constructors now `rowCopy` accessors, the five
text builders replaced by their typed twins (`changedForce`,
`graphPresetEvent`, `graphFilterCount`, `graphDiagramWhereAmIEvent`;
the filter clause is core's), `GraphTabMode` deleted for
`GraphSurfaceMode` at its four type sites with the tags `table` /
`diagram` kept, the seven fixtures given `summaryCounts:`, every named
test caller migrated, GraphAnnouncerTests rewritten to the 0a-9 suite
(latest-wins ×4, independence, the gate, the cancel, the High flush
announced and relayed, the relay's priority, the empty render, the
funnel guard now also refusing `.hostComposed`), `pinnedResidueSites`
29 → 28. Unrun on this box (0aR-1): the mac lane arbitrates.

**TG0a-6 — Mutations, byte-restored (absolute paths; every file's hash
asserted equal after restore).**

| # | Mutation | Caught by |
|---|---|---|
| M1 | a witness deleted from `graph_corpus()` | `the_graph_witnesses_are_exactly_the_matrix_in_order`, `the_graph_family_witness_count_is_pinned` |
| M2 | two witnesses reordered | `the_graph_witnesses_are_exactly_the_matrix_in_order` ("diverges at index") |
| M3 | a template's trailing period dropped | `corpus_renders_the_shipped_strings` |
| M4 | the `GraphWhereAmIFilter` row dropped from the inventory | `every_graph_parameter_enum_is_listed_for_coverage` |
| M5 | the inventory's `mirror` for `NodeKind` misnamed | `the_ffi_mirror_covers_every_core_a11y_variant` |
| M6 | the `forceValue` bullet dropped from the class-key list | `the_mac_graph_coalescing_switch_matches_the_pinned_class_list` |
| M7 | `links` counted Link edges only | `summary_counts_are_the_index_counts` |
| M8 | the unresolved-preset Ghost witness given ghosts-hidden flags | `graph_where_am_i_witnesses_are_host_reachable` ("unreachable Where-am-I witness") |
| M9 | the `Unresolved { count: 0 }` witness deleted | `graph_count_slots_render_at_every_reachable_cardinality` ("reachable at [Zero]") |
| M10 | a Windows mirror entry deleted | `the_windows_corpus_mirror_lists_every_event_in_order` |
| M11 | `case .graphForceValue` dropped from the mac switch | `the_mac_graph_coalescing_switch_matches_the_pinned_class_list` |
| M12 | `GraphTierEntered` dropped from the FFI mirror | `the_ffi_mirror_covers_every_core_a11y_variant` (a compile error, then the mirror difference) |
| M13 | GRAPH_NEIGHBOR_LABEL_CAP moved to 9 | `corpus_renders_the_shipped_strings` |

Thirteen of thirteen caught; every mutated file restored byte for byte
(hash asserted).

**TG0a-7 — Floors.** The graph citation census floor: 7 over 8 at
revision 4, then 77 over 78 at revision 5 once the regenerated bindings
declared the family's names in the Windows tree (the census counts a
backticked name declared anywhere it scans, generated bindings
included), then **79 over 80** at revision 6.

### Close-out — PR 0a shipped (#1179)

**The PR.** `feat/w6-2-0a`, PR #1179, head `f2658ed` at the gate:
thirteen checks green on that exact head — `build + test (windows
x64)`, `shell accessibility gate (windows x64)`, `app build + test
(windows x64)`, `rust tests (windows x64)`, `test (nextest, ci
profile)`, `fmt / clippy`, `bench-compile`, `SPDX header present on
every .rs/.swift`, `a11y-check` (100/100), `SwiftUI Accessibility
Check`, `Mac app XCTest suite`, `semgrep-cloud-platform/scan`, `Codoki
PR Review`. **0aR-1 discharged by the lane:** the Swift migration
compiled and the mac suites passed on the first run — GraphAnnouncerTests
(the 0a-9 suite, the window constant, the comment-aware funnel guard),
A11yCorpusCensusTests over the 73 graph entries through the real FFI,
`A11yResidueCensusTests` at 28, GraphDiagramTests' readback event,
GraphCommandsTests, GraphConfigTests, GraphTabRoutingTests,
ConnectionsPanelTests with the seven `summaryCounts:` fixtures.

**PR review round 1 (§0a, codoki).** One inline finding, High: the C#
census's `Labels: […]` "is not valid C# and will cause a compile error".
**Refuted on the thread** with the build as the evidence — a C# 12
collection expression targeting the generated `List<string>` parameter,
the project on `net10.0-windows` (C# 14), the same form shipped by the
canvas entries (`GroupPath: []`), and the exact head compiling and
passing `A11yCorpusCensus` on the fresh build here and on the CI
Windows lanes. Thread resolved. Codoki's summary on `f2658ed` read Requires changes on that one finding while the CI Windows lanes on the same head (`build + test (windows x64)`, `app build + test (windows x64)`) had compiled and run the file, and its re-run on the close-out head `fe688eb` (thirteen checks green) kept the finding open. **The compile claim stays refuted; one detail of the refutation was wrong and is corrected here:** the generated parameter is `string[]`, not a list — the collection expression compiled because it targets that array, and codoki's suggested `new[] { … }` was type-correct. The gate being mechanical, the six `GraphNeighborsContent` entries of the C# mirror were rewritten in that explicit array form (`new[] { … }`, `new string[0]` for the empty list) — semantically identical — with the matrix generator emitting it, so the five places still come from one source. No behaviour, count or identity changed.

**What this PR leaves for the series.** 0b: the `verbosity` key in the
`.slate/graph.json` schema (0aD-6); the structural queries. PR A: the
Windows `GraphAnnouncer.cs` over the four pinned classes, and the
Windows half of the graph coalescing tripwire (0a-3 (v)). PR B: the
posted `NoConnections` / `LoadingConnections` (0a-D3) and the composed
section header's plural rule (T13). PR C: the verbosity menu. PR E: the
inspector's label inventory (T37–T60, T71, T72). Filed at close-out:
the mac verbosity gap (no switch, no store — the mac-details register).

**The five places, final.** `a11y.rs` (17 variants, 73 witnesses, 73
golden rows), `tests/fixtures/a11y/corpus.json` (521), `lib.rs`
(every mirror, both `GraphNodeKind` directions), `A11yCorpusCensus.cs`
and `A11yCorpusCensusTests.swift` (73 each) — all five renderings of
one matrix source, and the frozen section's 0a-17 the authority.

### Tests that pin PR 0a

`crates/slate-core/src/a11y.rs`: `corpus_renders_the_shipped_strings`
(73 golden rows), `committed_corpus_artifact_matches_the_vocabulary`
(521), `every_graph_variant_and_arm_is_represented_in_the_corpus`,
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

## PR 0b — the graph structural queries move to core

**Goal (spec §PR 0b).** §W-G rows B–K become core queries over the
existing `GraphIndex`, metrics and layout: the visible set and the
diagram's whole topology — nodes and visible edges — in one record, the
Connections tree in reading order with its summary, the stable node key,
the name filter, the `.slate/graph.json` schema and merge policy, the
table rows with core-formatted cells, a core column model and a total
order, the constants, the action set and the option tables, the spatial
and structural steps, the ghost's note path. Task 0b-1 is the Rust + FFI
+ Windows-harness half; Task 0b-2 is the mac consumption half (every
Swift copy named in §2 deleted there). This section is at revision 4:
round 1 returned 25 findings, round 2 thirteen with eight blockers live
— three of them CREATED by revision 2's fixes, the protocol's rule 5, so
rule 4's stop applied and revision 3 opened with the design pass; round
3 found that pass had not yet closed any of its three classes and
returned fourteen findings with nine blockers, three created by revision
3 — rule 5 again — so revision 4 restated each model as a RULE that
catches its next instance; round 4 judged that every rule now does (a
fourth consumer, a fourth ordered inventory, a new key-bearing field)
and returned twelve findings against the contracts derived from them,
eight blockers, three created by revision 4 — rule 5 for the third time.
Revision 5 corrects the text for every one of them and FREEZES: the
standing precedent (E13's, applied to PR 0a at its round 6) freezes a
section whose round's blockers the previous fix created, with that
round's findings carried as a ledger the task loop discharges by code —
**precedent applied; the owner may overrule.** The section is frozen at
revision 5 on 2026-09-03; the ledgers are below.

**What stands today.** Core exposes `graph_snapshot(filter)`,
`graph_neighborhood(path, depth, filter)` (flat nodes and edges),
`graph_generation()` and the layout session (`lib.rs:1301–1349`);
`GraphIndex` owns `filtered_nodes` (`graph.rs:583`, sorted by `NodeKey`'s
derived order — paths before ghosts), `edges_among` (`:612`),
`neighborhood_ids` (`:645`), `ghost_key` (`:43`) and the `NodeKey`
namespaces (`:29`); `NodeMetrics` carries the degrees, the component and
the orphan flag (`graph_metrics.rs:24–38`); `GraphSnapshot` carries
`generation` (`graph.rs:319`), `GraphNeighborhood` does not
(`:329–339`), and `graph.rs:249–254` says a generation change may
reassign ids. Everything in §2 rows B–K is derived in Swift:
ConnectionsModel (`ConnectionsPanel.swift:375–499`: undirected
adjacency, the in/out split by centre incidence, self-edges dropped,
Link+Embed merged with an embed-only flag, recursion with an ancestor
guard, per-occurrence path ids, `localizedStandardCompare` per level,
`references = in_links + in_embeds`) loaded beside a second call for the
snippet bundle (`AppState+Connections.swift:60–110`); `GraphNodeKey`
(`GraphViewState.swift:57–78`: `p:` path, `g:` +
`addingPercentEncoding(withAllowedCharacters: .alphanumerics)` of the
`en_US_POSIX`-lowercased LABEL); the diagram's visible set
(`GraphDiagramView.swift:385–408`: the name predicate AND the preset
kind filter), recomputed on EVERY settling frame (`applyFrame`
`:328–349` calls `refreshVisibleSet`; `rebuildTopology` `:471–481`), its
label priority (`:640–648`: the top 200 by in-links, ties unordered), its
visible-edge filter (`rebuildEdges`, `:702–722`), its per-node accessible
neighbour content built at every tier-A rebuild (`:759–768` calls
`:856–874` for each visible node: visible-gated, deduplicated, edge
order, the node's OWN label for a self-link), its per-node group lookup
(`:531`) and diameter (`:412–413, 461–464`), and its spatial neighbours
and scoring (`:1221–1262`); the layout the diagram renders carries an
immutable backend filter (`GraphDiagramModel.swift:25`) and the layout's
generation (`:40`); `graphNameMatches` (`AppState+GraphConfig.swift:16–20`,
`.caseInsensitive, .diacriticInsensitive`) and the count's second copy
of the same predicate (`AppState+GraphTable.swift:228–238`); the config
codec with its clamps, version rule (`if let v = root["version"] as?
Int, v > 1`: any other shape of `version` is ignored and later
overwritten; `connectionsDepth as? Int`: a fractional depth is the
default) and unknown-key preservation (`GraphConfigStore.swift:38,
131–202`, the I/O and the writer actor around it); the nine-column model
with `byLabel`, `directionalComparator` (Folder by
`localizedStandardCompare`, `:568–573`) and the `rawValue` column index
the grid's sort state uses (`GraphTableView.swift:212–213, 444–628`) and
the folder derivation (`AppState+GraphTable.swift:418–420`); the preset
headline that re-sorts the snapshot itself (`:340–360`); the pickers
iterating hand-written `allCases` of the colour and ring enums
(`GraphInspectorView.swift:90, 95`; `GraphConfig.swift:109–142`); the
constants (`GraphDiagramModel.swift:58` tier 1,500;
`GraphDiagramView.swift:461–464` diameter; `:643` label cap 200;
`AppState+Connections.swift:31–33` depth clamp); `GraphRowAction`
(`GraphViewState.swift:15–48`); `ghostNotePath`
(`AppState+Connections.swift:306–315`). The layout's own refresh path
(`AppState+GraphDiagram.swift:110–145`: `layout.refresh()` then the
atomic adoption of ids, edges, metadata and the frame's generation) is
the diagram's generation authority. The Windows shell has no `Graph/`
directory: nothing to delete there, which is why 0b precedes PR A–E.

### Design pass (protocol rule 4, reached through rule 5 twice) — three subsystems, modelled as rules

Revision 2 fixed the site each finding named; revision 3 named three
classes but wrote each as a description of the sites then known, and
round 3 found the next instance of each class outside the description —
a fourth ordered inventory, a consumer whose authority is not a
generation, a value that carries an id under an allowed name. A model is
a RULE: it names the class by a property any new instance has, and the
contracts below are derived from the rule, not enumerated beside it.

**Subsystem A — the query lifecycle (IG0b-1, -5, -28, -29, -30, -40,
-41, -42).** *The rule:* a projection renders exactly ONE record — its
AUTHORITY — and every query it issues is answered against that record's
IDENTITY, which is whatever fields make the record what it is, not a
generation alone. A result publishes only under a current TOKEN and in
agreement with EVERY field of the authority's identity; a result that
IS the authority replaces it.

- *The authorities.* The table, the count and the selection render the
  SNAPSHOT: identity = (the backend filter it was fetched under, its
  `generation`) — `GraphSnapshot` carries no filter, so the host pairs the
  filter with the snapshot at the publish point, and a rows result agrees
  only when the token's `query.filter` equals it and the generations
  match (filter-B rows never land on a held filter-A snapshot). The
  diagram renders the LAYOUT:
  identity = (the layout handle, its immutable backend filter, its
  generation) — a topology agrees when the token's backend filter equals
  the layout's filter and its generation equals the layout's, so a
  filter change that starts an asynchronous layout rebuild can never
  land a topology on a layout that has no positions for it. The leaf
  renders the TREE, and the tree is its own authority: a token-current
  tree result publishes unconditionally and REPLACES the held state (the
  first load establishes it; a mutation's newer tree replaces it); the
  tree carries `summary_counts` so the leaf's structure is one record.
- *The token.* Every request carries `(session identity, the full
  request record, seq)`, where the request record is the complete input
  — GraphVisibilityQuery alone, or with `GraphTableSort`, or with
  `GraphConfig` for the topology — and `seq` is a per-consumer monotonic
  counter that EVERY input change advances (needle, kind, filter, sort,
  preset, config, vault, layout). A result whose token differs from the
  current token in any field is dropped whole; two same-generation
  answers can therefore never publish out of order.
- *Recovery.* Snapshot consumers re-fetch the snapshot, which resets the
  identity, then reissue; layout consumers run `layout.refresh()` and
  adopt ids, edges, metadata, frame and generation atomically
  (`AppState+GraphDiagram.swift:110–145`), then reissue — a snapshot
  fetch never advances a layout and is never the diagram's recovery; the
  leaf needs none.
- *One publish.* A result publishes rows, the accepted sort and the
  headline it implies in ONE synchronous assignment, rows first (the
  grid's owner contract, `AccessibleDataGrid.swift:173–187`); a preset is
  a token change like any other — it sets filter, kind, needle AND the
  default sort in one token, and its headline is that result's first
  row.
- *Decoration.* The leaf's snippet overlay reads the host's
  `NoteLoadBundle`, a second payload with its own lifecycle: it is
  DECORATION keyed by path — a snippet whose path is not in the tree is
  ignored, a row without a snippet shows none — and the leaf's
  generation refresh (`refreshConnectionsIfGraphChanged`) re-pairs the
  two after a mutation. Recorded as 0bR-4; the structure never depends
  on it.

**Subsystem B — one crossing per epoch (IG0b-3, -20, -27, -43, -47,
-48).** *The rule:* anything a projection needs PER NODE, PER EDGE or
PER MEMBER OF AN ORDERED SET comes from core in ONE record per SEMANTIC
EPOCH, and no host holds a literal that core could have listed.

- *Ordered inventories are core vectors.* Every ordered set a host
  iterates — the table columns, the colour tokens, the ring styles, the
  surface modes, the verbosity levels, the actions a kind is eligible
  for — is a core vector of records carrying the enum, its persistence
  tag where it has one, and its title; the host
  builds its grids, pickers and `allCases` adapters FROM the vector, and
  a test on each lane pins the vector's order and membership against the
  enum's arms. A hand-written `allCases`, `ordered` or `rawValue` index
  is the class this rule forbids.
- *The topology record.* `graph_topology(query, config)` returns the
  visible nodes with everything the diagram renders, speaks or acts on
  per node — the key, the label, the path, the kind, all four degrees,
  the component, the orphan flag, core's diameter, the matching group,
  the label slot, the visible neighbours as records — AND the visible
  EDGES in order, so the row copy, Where-am-I, the four navigation
  actions, the styling, the labels, the neighbour content and
  `rebuildEdges` read one record and no second per-node source.
- *Per epoch, not per frame.* The diagram fetches the topology when its
  SEMANTIC EPOCH changes — (the layout handle, its generation, the
  visibility query, the config) — and caches it across the settling
  frames, which update positions and paint from the cache; a call-count
  fact drives at least two non-converged frames under one unchanged
  epoch and asserts exactly one fetch, then changes each epoch component
  in turn — the query, the config, the generation and the layout handle
  through an adoption — and asserts exactly one more fetch per change.
- *Constant tables once.* The constants, the column specs, the option
  vectors and the action vectors are fetched once per process into host
  statics; a per-node `title` call is a re-crossing and is forbidden.
- *The count from the rows.* The filter count is the rows result's
  `rows.count` of `total`, announced when that result publishes — one
  path, no per-label predicate crossing.

**Subsystem C — the artifact as evidence (IG0b-9, -31, -37, -44, -45).**
*The rule:* a census fact is a TYPED SCHEMA the artifact is validated
against, value by value, and every claim the artifact makes is checked
against something the artifact does not own; a guard proves itself by a
mutation at each place it guards.

- *Artifact-only shapes.* The serializer never writes a core record; it
  copies each into an artifact shape that has no numeric-id field at
  all, so an id can only enter by an explicit write.
- *The schema walk.* The census walks every value of the golden against
  a per-section schema table: every KEY-BEARING value — a `key`, a
  `center_key`, an element of `visible`, `labeled` or `neighbors`, an
  endpoint of an edge — is a string that is a member of the
  fixture-owned inventory; every `occurrence` is `in` or `out` followed
  by `/` + the percent-encoding of inventory keys, and every `parent` is
  `null` exactly when `level` is 1 and otherwise an occurrence that
  appeared EARLIER in the same list and is a prefix of the row's own
  occurrence; every NUMBER sits under a name in the numeric allow-list (`in_links`,
  `out_links`, `in_embeds`, `out_embeds`, `component`, `total`, `level`,
  `references`, `depth`, `tree_depth`, `note_count`, `diameter_x100`,
  `group`, `dx`, `dy`, `group_count`, `connections_depth`, the
  `constants` fields, the `_x100` config fields); every other value is a
  string, a boolean or null. A numeric id under any name fails here.
- *The pins.* Each of the eleven sections has an independent pin the
  artifact does not own: the inventory (the snapshot's key list, the
  topology's and every table sort's key lists SORTED — exact lists, so a
  duplicated or missing entry fails, never a set), the pinned query names, sort names, `(path, depth)` pairs,
  targets, directions, structural steps, the three kinds, the constants'
  field names, the config input and its decoded field names; the
  top-level key set is exactly the eleven.
- *Mutations that bite.* The sweep makes the serializer (a) write a
  numeric id at EACH key-bearing location in turn, (b) write a forbidden
  property, (c) drop one node, (d) duplicate one topology node and one
  table row, (e) drop one section, (f) reorder one section's entries,
  (g) drop one pin field, (h) write a level-1 parent — each must fail
  exactly one named fact.

The contracts below are derived from these three rules; the ledgers map
every finding to the rule that takes it.

### Contracts

**0b-1 — One derivation per structural rule.** Every rule §2 rows B–K
names lives once, in `crates/slate-core/src/graph_queries.rs` (the
visible set and the topology, the tree, the key, the filter, the rows
and the column model, the constants, the actions, the steps, the ghost
path) and `crates/slate-core/src/graph_config.rs` (the schema, defaults,
clamps, decode and encode rules, the group rules, the option tables), as
pure functions of the session's graph surface or of their arguments. The
session layer adds handle-based methods that resolve the index and
delegate; `slate-uniffi` adds 1:1 mirrors with no logic. Nothing in a
host recomputes any of them, and no host holds a literal core lists
(R-D; design B). Task 0b-2 deletes the Swift copies, and the §W-G
register below reads "moved" per row with the deleted range and the
consuming site.

**0b-2 — The query surface.** Session-based unless marked free; every
session query reads the built index under the session's lock order
(`conn → graph → graph_metrics`) exactly as `graph_snapshot` does,
returns `Result<_, VaultError>` like every session query, and every
session result carries the `generation` of the index it read. The names
here SUPERSEDE the seed names in spec §2 rows B–K, §4 PR 0b and the PR
A–E "Consumes" and "Builds" lists, which the same PR amends to these
(0bD-12).

| Query | Shape | Reads |
|---|---|---|
| `graph_visibility` | `(GraphVisibilityQuery) -> GraphVisibility` | `filtered_nodes`, labels, metrics |
| `graph_topology` | `(GraphVisibilityQuery, GraphConfig) -> GraphTopology` | the snapshot's nodes and edges, metrics, the config's groups |
| `graph_neighbors` | `(GraphVisibilityQuery, id) -> GraphNeighbors` | `edges_among` over the visible set — the single-node form; an absent or invisible id yields an empty list |
| `graph_connections_tree` | `(path, depth, filter) -> GraphConnectionsTree` | `neighborhood_ids`, `edges_among`, metrics, the neighbourhood's counts |
| `graph_table_rows` | `(GraphVisibilityQuery, GraphTableSort) -> GraphTableRows` | the snapshot's nodes, metrics, mtimes |
| `GraphNode.stable_key` | a field on the existing record | `NodeKey` |
| `graph_stable_key_for_path` | `(path) -> String` | **free** — the `p:` namespace for a path the host holds |
| `graph_label_matches` | `(label, query) -> bool` | **free** — the one predicate; the group matcher's |
| `graph_table_columns` | `() -> Vec<GraphTableColumnSpec>` | **free** — the ordered column model |
| `graph_constants` | `() -> GraphConstants` | **free** |
| `graph_node_diameter` | `(in_links) -> f64` | **free** — the curve; the topology carries it per node |
| `graph_row_actions` | `(kind) -> Vec<GraphRowActionSpec>` | **free** — the ordered eligibility vector per kind: action and title |
| `graph_spatial_step` | `(points, neighbors, from, dx, dy) -> Option<u64>` | **free** — geometry only |
| `graph_structural_step` | `(visible, from, forward) -> Option<u64>` | **free** |
| `graph_ghost_note_path` | `(target_raw) -> String` | **free** |
| `graph_config_default` | `() -> GraphConfig` | **free** |
| `graph_config_decode` | `(json) -> Result<GraphConfigRead, GraphConfigError>` | **free** |
| `graph_config_encode` | `(config, existing_json: Option<String>) -> Result<String, GraphConfigError>` | **free** |
| `graph_config_matching_group` | `(config, label) -> Option<u32>` | **free** — the topology carries it per node |
| `graph_config_next_group_style` | `(group_count) -> GraphGroupStyle` | **free** |
| `graph_color_tokens` | `() -> Vec<GraphColorTokenSpec>` | **free** — the ordered palette: token, tag, title |
| `graph_ring_styles` | `() -> Vec<GraphRingStyleSpec>` | **free** — the ordered ring styles: style, tag, title |
| `graph_surface_modes` | `() -> Vec<GraphSurfaceModeSpec>` | **free** — the ordered modes: mode, tag, title |
| `graph_verbosities` | `() -> Vec<GraphVerbositySpec>` | **free** — the ordered levels: verbosity, tag, title |

Twenty-three names; core declares them in GRAPH_QUERY_SURFACE (0b-15).
`GraphVisibilityQuery { filter: GraphFilter, name_query: String,
kind_only: Option<NodeKind> }` is the ONE visibility record both
projections hold: the backend filter, the name needle, and the preset's
kind overlay (`kind_only = Ghost` is the Unresolved preset, which
`GraphFilter` cannot express — `GraphTableView.swift:305–315`,
`GraphDiagramView.swift:385–394`). Every visible-set consumer — the
table's rows, the diagram's topology, the count — takes it, so "shown"
has one definition (spec §P2-4 "filter equivalence", now provable).

**0b-2b — Authority, identity, token; the host's rule (design A).** Node
ids are `StableGraph` indices that a rebuild may reassign
(`graph.rs:249–254`). Every session result (GraphVisibility,
`GraphTopology`, `GraphNeighbors`, `GraphTableRows`,
GraphConnectionsTree) carries `generation: u64`, the value
`graph_generation()` would have returned under the same lock. The host
rule, both lanes: a consumer renders ONE record, its authority, and
issues requests under ONE token `(session, request, seq)` where
`request` is the complete input record and every input change advances
`seq`; a result publishes only when its token equals the current token
in every field AND it agrees with every field of the authority's
identity — the table, the count and the selection: the filter the
snapshot was fetched under (equal to the token's `query.filter`) and the
snapshot's generation; the diagram: the layout's backend filter (equal
to the token's `query.filter`) and the layout's generation; the leaf: none, the
tree being its own authority, so a token-current tree publishes and
replaces the held state. Anything else is dropped whole. Recovery is the
consumer's own: snapshot consumers re-fetch the snapshot then reissue;
layout consumers run `layout.refresh()` and adopt ids, edges, metadata,
frame and generation atomically (`AppState+GraphDiagram.swift:110–145`),
then reissue; the leaf needs none. Pinned by
`session_results_carry_the_generation_they_read` (a mutation between a
snapshot and a query yields a different generation and the new index's
ids), and on the mac by the token tests in 0b-14 (a reordered pair of
same-generation results under different needles, filter-B rows against
a held filter-A snapshot, a topology under another backend filter than
the layout's, a mutation between a layout adoption and a topology query,
the leaf's first load and its replacement after a mutation).

**0b-3 — The stable key is core's, one algorithm.** `GraphNode.stable_key`
is `p:` + the vault-relative path for a Note or Attachment, and `g:` +
the percent-encoded `ghost_key` for a Ghost — the SAME folded string
`NodeKey::Ghost` carries (`ghost_key` over the authored `target_raw`:
Rust `trim`, strip ONE leading `./` or `/`, Rust `to_lowercase`).
Percent-encoding: every UTF-8 byte of a character that is not Unicode
alphanumeric (`char::is_alphanumeric`) is written `%XX` uppercase;
alphanumerics pass through, so `Missing Note` keys as
`g:missing%20note` and `café` as `g:café`. Two disjoint namespaces,
byte-stable across generations and sessions.
`graph_stable_key_for_path(path)` returns the `p:` form for a path the
host holds without a node (the three selection sites that key from a
path). Divergences from the deleted Swift, recorded (0b-D1) and made
EXECUTABLE on the mac lane by GraphKeyCharacterizationTests — a test
that carries the OLD Swift algorithm verbatim and pins its bytes beside
core's for every witness, so the table below is asserted, not asserted
about: (i) the SOURCE — mac keyed the displayed LABEL (the smallest
authored variant, prefix intact), core keys the stored `NodeKey::Ghost`
derived from `target_raw`, so a ghost authored `./Foo` moves from
`g:%2E%2Ffoo` to `g:foo`, and `Foo` / `./Foo` / `/foo` are one node
under one key; (ii) the fold — `lowercased(with: en_US_POSIX)` there,
Unicode's locale-independent lowercase mapping here (`İ` lowers to `i` +
U+0307 in core; the mac lane's characterization records what
`en_US_POSIX` yields); (iii) the encoding — Foundation's
`addingPercentEncoding(withAllowedCharacters:)` applies its allowed set
to 7-bit characters only and percent-encodes EVERY non-ASCII byte, so
mac wrote `café` as `g:caf%C3%A9` and the NFD form as `g:cafe%CC%81`,
where core writes `g:café` and `g:cafe%CC%81`: core keeps non-ASCII
alphanumerics bare, and both keep the two normalization forms
byte-distinct (the property the mac test asserted). Witnesses on both
lanes: `./Foo`, `/Bar`, `Foo` beside `FOO`, NFC and NFD `café`,
`İstanbul` — the Rust facts `stable_key_is_the_two_namespaces` and
`stable_key_percent_encodes_by_rust_alphanumerics`, the
characterization test, and the graph vault's ghost targets in the
artifact (0b-13).

**0b-4 — The Connections tree is FLAT pre-order rows carrying its
summary, not a recursive record** (0bD-1). `GraphConnectionsTree {
generation, center_id, center_key, depth, summary_counts:
GraphNeighborhoodCounts, incoming: Vec<GraphConnectionRow>, outgoing:
Vec<GraphConnectionRow> }` — the neighbourhood's own counts (0a-7)
travel with the tree so the leaf's structure is ONE record and the tree
is its own authority (design A); `GraphConnectionRow { id: String,
level: u32, parent_id: Option<String>, node_id: u64, stable_key, label,
path: Option<String>, target_raw: String, kind: NodeKind, embed_only:
bool, in_links, out_links, references: u32 }`. Rules, each the mac's:
the neighbourhood is `graph_neighborhood`'s payload (depth clamped
1..=3, the filter applied before traversal), read under the SAME lock as
the generation; adjacency is undirected over its edges; the first hop
splits by centre incidence — `source == centre` ⇒ outgoing (the target),
`target == centre` ⇒ incoming (the source); a self-edge is dropped from
both; a neighbour reached by Link and Embed is ONE row, `embed_only` iff
no Link edge joined them; `references = in_links + in_embeds` from
metrics; `target_raw = label`; nesting recurses `depth − 1` further
levels, excluding the ancestors on the current path (the cycle guard),
and a level-N row's children follow it immediately (pre-order), each
with `parent_id` = the row's id; every level is ordered by 0b-5. Row ids
are OCCURRENCE PATHS BUILT FROM STABLE KEYS, never from node ids
(0bD-10): `in` or `out`, then `/` + enc(key) for each key from the
level-1 row down to the row itself, where enc is 0b-3's percent-encoder
applied to the whole stable key (`:`, `/` and `%` are not alphanumeric,
so every key is one `/`-free segment) — `in/p%3Ahub%2Emd`,
`in/p%3Ahub%2Emd/g%3Aghost%2520one`. A diamond descendant is distinct
under each parent, and the id is byte-stable across generations and
lanes. Snippets are NOT here: the depth-one snippet overlay reads the
host's `NoteLoadBundle` and is decoration, keyed by path (0bD-2, 0bR-4).
Pinned by the tree goldens (ConnectionsPanelTests' five cases as Rust
facts: the split with ghosts and embeds, depth two nesting, depth three
with the guard, Link+Embed collapse, the self-edge; the counts equal to
`graph_neighborhood`'s) and by `graph_queries.json`.

**0b-5 — Label order is core's, one comparator, total.** The tree's
levels, the table's label tier and the label-priority tie-break order by
`graph_label_order(a, b)`: (1) the FOLDED labels (0b-6's fold — NFD,
combining marks dropped, lowercase) compared segment-wise, where a
segment is a maximal run of ASCII digits `0-9` or a maximal run of other
characters — non-digit runs bytewise, and a digit run against a digit run
PARSE-FREE: strip leading zeros from both; the run with more remaining
digits is greater; equal length → bytewise; equal value → the run with
FEWER leading zeros first (so `2` < `02` < `002`, `10` < `010`, `0` <
`00`); a digit run sorts before a non-digit run at the same position;
when one segment list is a prefix of the other, the shorter sorts first
(`a` < `a0`, `a` < `ab`, `1` < `1a`); Unicode digits outside `0-9` are
ordinary characters; (2) the raw labels bytewise; (3) `stable_key`; (4)
`node_id` — a strict total order on distinct rows unconditionally, with
no integer parse (a thousand-digit run is a witness). Divergences
recorded (0b-D2): the mac's TABLE ordered by Foundation's
`localizedStandardCompare` (Finder's locale-sensitive collation, its own
diacritic and case handling), then the stable id, then `nodeID`
(`GraphTableView.swift:535–543`) — core replaces the first tier with the
locale-free fold and INSERTS the raw-bytes tier; the mac's TREE levels
ordered by `localizedStandardCompare` alone with NO tie-break — core
gives the tree the same four tiers; the mac's FOLDER column ordered by
`localizedStandardCompare` (`:568–573`) — core orders folders by the
same fold-natural comparison, then the raw folder bytes, then falls
through to the label order (0b-7). Pinned by
`label_order_is_natural_folded_and_total` (ASCII, Latin-1, the digit
runs above, the all-zero runs, the long run, the prefix cases, equal
folds distinguished by raw bytes, equal labels by key) and by the
artifact's `2` / `10` / `010` notes.

**0b-6 — The name filter is core's fold, one production definition; the
visible set.** `graph_queries::name_filter_fold(text)` (pub) is the
fold: NFD, combining marks dropped, `to_lowercase`; the needle is
trimmed with Rust `trim` (Unicode `White_Space`, newlines included) and
an empty needle matches everything; `graph_label_matches(label, query)`
is the predicate (folded label contains folded needle); 0a's test helper
of the same name delegates to it. `graph_visibility(q)` returns
`GraphVisibility { generation, total: u64, ids: Vec<u64>, labeled:
Vec<u64> }`: `total` = the node count under `q.filter` alone; `ids` =
the nodes that also pass the needle and `kind_only`, in the snapshot's
node order; `labeled` = the ids that take a label slot — all of `ids`
when their count ≤ `label_cap`, else the top `label_cap` by `in_links`
descending with 0b-5's order as the tie-break, in that ranked order (the
mac's `labelPriorityIDs`, whose ties were unordered; the boundary at
200/201 is a witness). Divergences from Foundation recorded (0b-D3):
`.caseInsensitive, .diacriticInsensitive` containment and core's fold
are different definitions that agree on case, on decomposable
diacritics (`café` under `cafe`) and on `İ` under `i` (both drop the
dot), and are not claimed equal beyond the witnesses the
characterization test (0b-3) pins — and that test found the sharp s:
Foundation's case-insensitive comparison matches `Straße` under
`strasse`, core's NFD + lowercase does not; and the trim — mac's `.whitespaces` kept a newline, core's `trim` drops it, so
`"\ncafe\n"` matches `café` here and matched nothing there. Both
projections consume core's answer and apply no predicate of their own;
the count is the rows result's `rows.count` of `total` (design B).
Pinned by `filter_ids_fold_case_and_marks` (the cases above plus
U+3000), `labeled_is_the_capped_priority_set`, and the artifact's needle
list.

**0b-6b — The topology: one record per epoch (design B).**
`graph_topology(q, config)` returns `GraphTopology { generation, total:
u64, nodes: Vec<GraphTopologyNode>, edges: Vec<GraphEdge> }` — `nodes`
one entry per visible node in the snapshot's node order:
`GraphTopologyNode { id, stable_key, label, path: Option<String>, kind:
NodeKind, in_links, out_links, in_embeds, out_embeds, component: u32,
is_orphan: bool, diameter: f64, group: Option<u32>, labeled: bool,
neighbors: Vec<GraphNeighbor> }` — the payload the row copy
(`GraphDiagramModel.swift:97`), Where-am-I (`AppState+GraphDiagram.swift:257`)
and the navigation actions (`GraphDiagramView.swift:984`) read, so the
diagram holds no second per-node source — where `diameter` is 0b-8's curve unscaled (the
display multiplier is a rendering the host applies), `group` is 0b-12's
first-match index over `config.groups`, `labeled` is membership of
`graph_visibility`'s `labeled` set, and `neighbors` are the node's
VISIBLE neighbours as `GraphNeighbor { id, stable_key, label }` — over
the snapshot's edges in order, for each edge incident to the node the
OTHER endpoint, kept iff visible and not yet listed (a Link and an Embed
to the same node yield one entry; a self-edge yields none — a recorded
divergence, 0b-D9: the mac's diagram content listed the node's own label
for a self-link, its tree dropped it, and core drops it from both); and
`edges` the snapshot's edges whose BOTH endpoints are visible, in the
snapshot's edge order, self-edges included (they are edges, not
neighbours), which `rebuildEdges` draws as given. The diagram's
accessible neighbour content (`GraphDiagramView.swift:856–874`: the
labels, in this order, handed whole to 0a's `GraphNeighborsContent`,
whose cap is core's), the spatial step's neighbour-first list
(`:1221–1228`: the ids), the styling (`:531`), the label decision
(`:640–648`) and the drawn edges (`:702–722`) all read the record; the
diagram crosses the FFI once per semantic epoch. `graph_neighbors(q, id)
-> GraphNeighbors { generation, neighbors }` is the same list for one
node — an absent or invisible `id` yields `neighbors: []` under the
generation read — for a host that asks on demand. Pinned by
`topology_is_the_visible_nodes_with_their_neighbours` (the topology's
ids equal `graph_visibility`'s, its `labeled` flags its `labeled` set,
each node's `neighbors` equal `graph_neighbors`', each `diameter` the
curve, each `group` the matcher), `topology_carries_the_visible_edges`
(the edges are exactly the snapshot's edges among the visible ids, in
order, the self-edge kept), `neighbors_are_unique_visible_and_ordered`
(the self-edge and Link+Embed fixtures) and the artifact's `topology`
section.

**0b-7 — Table rows are core-formatted and core-ordered under a core
column model.** `graph_table_columns()` returns nine
`GraphTableColumnSpec { column: GraphTableColumn, header: String }` in
display order — `Note`/"Note", `LinksIn`/"Links in", `LinksOut`/"Links
out", `EmbedsIn`/"Embeds in", `EmbedsOut`/"Embeds out",
`Component`/"Component", `Modified`/"Modified", `Folder`/"Folder",
`Kind`/"Kind"; a grid is built from this vector, its sort state's column
index IS the vector index, and a row's cell for index `i` is `cells[i]`
(design B — no host lists the columns). `graph_table_rows(q, sort)`
returns `GraphTableRows { generation, total: u64, rows:
Vec<GraphTableRow> }` over `q`'s visible set (0b-6), sorted by
`GraphTableSort { column: GraphTableColumn, ascending: bool }`;
`GraphTableRow { stable_key, node_id, label, path: Option<String>, kind:
NodeKind, cells: Vec<String>, links_in, links_out, embeds_in,
embeds_out, component: u32, modified_ms: Option<i64> }`, where `cells`
holds EXACTLY nine strings in the column order — the label, the five
counts in decimal, `modified_text`, the folder, the kind label — so no
host formats a value (the raw fields remain for actions and logic). The
comparator is the mac's except where 0b-D2 records otherwise: a numeric
primary key in the requested direction with the label order (0b-5) as
the tie-break ALWAYS ascending; `Note` is the label order in the
requested direction; `Modified` orders `modified_ms` with a missing
value lowest; `Folder` orders the folder by 0b-5's fold-natural
comparison in the requested direction, then the raw folder bytes in the
requested direction, then falls through to the ascending label order;
`Kind` orders the kind label bytewise then falls through. The default
sort is `LinksIn` descending (hubs first). Cells: the kind label is
`Note` / `Attachment` / `Unresolved`; the folder is the path up to its
last `/` (empty at the vault root and for ghosts); `modified_text` is
`YYYY-MM-DD HH:MM` in UTC from `modified_ms`, empty for a ghost — a
recorded divergence from mac's locale medium date and short time
(0b-D4): a §W-A cell cannot carry a locale or a zone. A PRESET is a
request like any other (design A): it sets the filter, the kind, the
needle and the DEFAULT sort in one token, and its headline is the first
row of that result (`most linked`) or the result's `rows.count`
(orphans, unresolved), spoken when the rows publish. Pinned by
`table_rows_sort_is_the_mac_comparator` (the GraphTableViewTests cases
as Rust facts: descending hubs then labels, ascending flips the number
not the tie, same-label distinct paths totally ordered, the ghost's
empty modified text, the kind labels, distinct folders with equal folds
by bytes), `table_cells_are_nine_and_formatted`,
`table_columns_are_the_ordered_specs`, and the artifact's sort list.

**0b-8 — The constants have one source.** `graph_constants()` returns
`GraphConstants { tier_b_threshold: 1500, label_cap: 200,
connections_depth_min: 1, connections_depth_max: 3, node_diameter_min:
8.0, node_diameter_max: 28.0, neighbor_label_cap: 10 }`, every field
the named constant in `graph_queries.rs` (and `neighbor_label_cap` the
0a constant GRAPH_NEIGHBOR_LABEL_CAP), asserted field by field, never
a re-typed literal. `graph_node_diameter(in_links)` is
`8 + 6·ln(1 + in_links)` clamped to 8..=28 (`GraphDiagramView.swift:461–464`);
the topology carries it per node (0b-6b). The tier boundary is
inclusive: 1,500 visible nodes are tier A, 1,501 tier B
(`GraphDiagramTests.testTierBoundaryIsInclusiveAt1500AndSwitchesAt1501`
as a Rust fact). The zoom clamp (10..=400 percent) stays the canvas
viewport's, host-designated (§2 row L); 0a's invariants test names it
as the host's.

**0b-9 — The action set is core's: the order, the titles, the kind
eligibility, as one vector of records.** `GraphRowAction { Open,
OpenInNewTab, ShowConnections, Reveal, CreateNote }` in that order;
`graph_row_actions(kind)` returns the ordered ELIGIBILITY vector of
`GraphRowActionSpec { action: GraphRowAction, title: String }` — the
titles `Open`, `Open in New Tab`, `Show connections`, `Reveal in File
Tree`, `Create note` (manifest T66); the four navigation actions for a
Note or an Attachment and `Create note` alone for a Ghost: no projection
offers an action the node's kind cannot take (`GraphViewState.swift:39–47`).
The vectors are fetched once per process into host statics, and a host's
`allCases` is the union of the per-kind vectors in core's order (design
B; the record rule, no scalar title function). Runtime admission is the
host's (0bD-8): while a structural mutation is unavailable, the Table
and the Connections leaf show `CreateNote` DISABLED with
`structuralMutationDisabledReason` as the hint, and the Diagram OMITS it
from the node's custom actions (an NSAccessibilityCustomAction has no
disabled state) — the shipped mac behaviour at each site, kept. Pin/Unpin
stays diagram-only and host-side, exempt from parity by the recorded
rationale. Pinned by `row_actions_by_kind_are_the_parity_set`; the
busy-state cases stay in GraphDiagramTests and GraphTableViewTests.

**0b-10 — The spatial step is core's scoring, over host positions, total
over malformed input.** `graph_spatial_step(points: Vec<GraphPoint { id,
x, y }>, neighbors: Vec<u64>, from: u64, dx: f64, dy: f64) -> Option<u64>`:
first among the candidates in `neighbors` that have a point, then — when
none scores — among every point but `from`; a candidate scores only when
its distance from `from` exceeds 1e-4 and `cos θ = (v·d)/(|v|·|d|)`
exceeds 0.1; the score is `|v| / max(cos θ, 1e-4)`; the lowest score
wins, ties to the lower id (`GraphDiagramView.swift:1230–1250`; the
host passes unit axes, and core normalises `d` so any non-zero vector
means the same). Totality: a point with a non-finite coordinate is
ignored; a duplicated point id keeps its first occurrence; `from` without
a point, or a zero or non-finite `(dx, dy)`, returns `None`. The
neighbour list is the topology entry's (0b-6b).
`graph_structural_step(visible: Vec<u64>, from: Option<u64>, forward:
bool) -> Option<u64>` is the wrapping next/previous over the given order
from the FIRST index of `from`, the first (or last) element when `from`
is absent or not in the list, `None` for an empty list (`:1254–1262`).
Type-ahead stays a host list search (§2 row J). Pinned by
`spatial_step_prefers_neighbours_then_falls_back`
(`testSpatialMoveNeighborsFirstThenFallbackOnAFixedLayout` as a Rust
fact over the same four points), `spatial_step_is_total_over_bad_input`,
`structural_step_wraps`, and the artifact's point table.

**0b-11 — The ghost's note path, total over `/`-strings.**
`graph_ghost_note_path(target_raw)`: Rust `trim`; strip ONE leading `./`
or `/`; the last component is the text after the last `/` (a backslash
is an ordinary character — vault paths are `/`-separated, and
`std::path` is not consulted); its extension is the text after the last
`.` of that component when that `.` is not its first character (so the
dotfile `.md` has none); append `.md` unless the extension is `md`,
`markdown`, `mdown` or `mkd` case-insensitively (`.MD` counts). So
`Foo` → `Foo.md`, `notes/Foo.MD` → `notes/Foo.MD`, `./a/b` → `a/b.md`,
`././a` → `./a.md`, `dir/` → `dir/.md`, `.md` → `.md.md`, `a\b` →
`a\b.md`, and the empty target → `.md` (the host refuses an empty target
before asking). Rust `trim` against mac's `.whitespaces` is the same
recorded divergence as 0b-6's (0b-D3). Pinned by the
`testGhostNotePathHonorsFolderAndExtension` cases as Rust facts plus the
cases above, and the artifact's target list.

**0b-12 — The config schema and merge policy are core's, IDL complete;
the option tables are vectors; the I/O is the host's.** The records,
exact: `GraphFilterConfig { include_attachments: bool, include_ghosts:
bool, orphans_only: bool, name_query: String }`; `GraphGroup { query:
String, color_token: GraphColorToken, ring_style: GraphRingStyle }`;
`GraphDisplay { arrows: bool, text_fade_zoom: f64, node_size_multiplier:
f64, link_thickness: f64 }`; `GraphForcesConfig { center: f64, repel:
f64, link: f64, link_distance: f64 }`; `GraphConfig { filters, groups:
Vec<GraphGroup>, display, forces, mode: GraphSurfaceMode,
connections_depth: u32, verbosity: GraphVerbosity }`; `GraphConfigRead {
config: GraphConfig, unknown_json: String }` — the canonical JSON text
of an object holding every top-level key the schema does not own (`{}`
when none); `GraphConfigError { Unparseable { reason: String },
NewerVersion { version: u64 } }` (a uniffi error enum); `GraphGroupStyle
{ color_token, ring_style }`; `GraphColorTokenSpec { token:
GraphColorToken, tag: String, title: String }` and `GraphRingStyleSpec {
style: GraphRingStyle, tag: String, title: String }`, the ORDERED option
tables `graph_color_tokens()` (`red, orange, yellow, green, teal, blue,
purple, pink`) and `graph_ring_styles()` (`solid, dashed, double,
dotted`) return, each entry's tag its lowercase name and its title the
capitalised name (T71, T72); likewise `GraphSurfaceModeSpec { mode: GraphSurfaceMode, tag, title }`
from `graph_surface_modes()` (`table`/"Table", `diagram`/"Diagram") and
`GraphVerbositySpec { verbosity: GraphVerbosity, tag, title }` from
`graph_verbosities()` (`terse`/"Terse", `standard`/"Standard",
`verbose`/"Verbose") — the shipped mac titles and persistence tags
(`GraphAnnouncer.swift:19–94`) verbatim; a host's pickers, menus and
its `allCases` are built from the vectors (design B), and a test on each
lane pins each vector's length and order against the enum's arms. `graph_config_default()`
is the default, and `graph_config_decode("{}")` equals it (a test); an
absent file is the HOST's case and it uses `graph_config_default()`.
Schema v1 of `.slate/graph.json`: `version: 1`; `filters {
includeAttachments, includeGhosts, orphansOnly, nameQuery }`; `groups:
[{ query, colorToken, ringStyle }]`; `display { arrows, textFadeZoom,
nodeSizeMultiplier, linkThickness }`; `forces { center, repel, link,
linkDistance }`; `mode`; `connectionsDepth`; and — new here, 0aD-6 —
`verbosity` (`terse` / `standard` / `verbose`, default `standard`).
Defaults and clamps are the mac's (`GraphConfigStore.swift:131–169`):
filters false/true/false/""; `textFadeZoom` 0.1..=4.0 default 0.55;
`nodeSizeMultiplier` 0.5..=2.0 default 1.0; `linkThickness` 0.5..=4.0
default 1.0; each force 0..=1 default 0.5; `connectionsDepth` 1..=3
default 1; `mode` `table` unless the tag is `diagram`. The decode truth
table classifies the PARSED VALUE — the workspace enables only
`serde_json/preserve_order` (`Cargo.toml:68–73`), so a non-integer
number token is an `f64` and two tokens with the same `f64` value are
the same value (`1.0000000000000001` is `1.0`; no `arbitrary_precision`
is wanted, and this is stated, not hidden): text that is not JSON, or
whose number tokens overflow `f64`, is `Unparseable` (JSON carries no
`NaN` or infinity, so no inbound value is non-finite); the root must be
a JSON object, else `Unparseable`; `version` absent → 1; a number whose
value is a non-negative integer ≤ `u64::MAX` (`1`, `1.0`, `1e0`,
`1.0000000000000001` alike) → that integer, greater than 1 →
`NewerVersion { version }` (never decoded, never rewritten); any other
`version` value (fractional, negative, boolean, string, null, above
`u64`) → `Unparseable` — a recorded divergence (0b-D8) from the mac,
which classified only an `Int` above 1 and otherwise decoded the file
and later overwrote it; a section that is not an object is ignored
whole (the defaults stand); a float field that is missing or
non-numeric takes its default, else is clamped; `connectionsDepth` is a
number whose value is INTEGRAL and within `i64` (Swift's `Int` bridge),
clamped into 1..=3 (`2`, `2.0`, `1e0`, `99` → 3, `-0` → 1, `-5` → 1,
`9223372036854775807` → 3), while a fractional value, an integral value
beyond `i64` (`1e20`, `9223372036854775808`) or a non-number is the
default 1 — the mac's `as? Int` classification, kept, boundary included; a boolean
field that is not a boolean takes its default; `groups` that is not an
array is ignored, and WITHIN an array each element that is not an object
or has no string `query` is SKIPPED while the others survive — a
recorded divergence (0b-D6) from the mac, whose `[[String: Any]]` cast
dropped the whole array when any element was not an object; an unknown
`colorToken` reads `blue`, an unknown `ringStyle` `solid`; an unknown
`mode` or `verbosity` tag reads the default. `graph_config_encode(config,
existing_json)`: `existing_json` absent means a fresh file; a text that
is not a JSON object → `Unparseable`; a version that cannot be
classified → `Unparseable`; a newer version → `NewerVersion`; otherwise
the known sections replace theirs and every unknown top-level key is
preserved SEMANTICALLY (parsed, re-emitted canonically — formatting and
number spelling of an unknown value are not byte-preserved), and every
outbound value is clamped as an inbound one — a float `NaN` → the
default, `±∞` → the bound, the depth into 1..=3 — so the file is always
in range. The writer is CANONICAL, an explicit pass — `preserve_order`
makes `Map` insertion-ordered and no sorting is implicit: every object's
keys sorted recursively by Unicode scalar order, serde's pretty printer
(two-space indent, `"key": value`, `\n` line ends, no trailing newline)
— a recorded divergence in file FORMATTING from Foundation's pretty
printer (0b-D5): the bytes differ, the object does not, and either
reader accepts both. Atomic temp-and-rename, the single-writer actor and
its monotonic generation gate stay host-designated (§2 row F; 0bD-3),
with their tests (0b-14). Groups: `graph_config_matching_group(config,
label)` is first-match-wins over the ordered groups, a group's trimmed
query matched by `graph_label_matches`, a blank query never matching —
the trim and the fold are core's (0b-D3 extended: the mac's matcher
trimmed `.whitespaces` and compared with Foundation's options, so a
group query `"\nfoo\n"` styles nodes here and styled none there; a
characterization witness on the mac lane);
the topology carries the index per node (0b-6b) — LABEL-ONLY, the
shipped mac matcher, by owner ruling (0bD-9, 0b-D7);
`graph_config_next_group_style(group_count)` cycles the ring and the
palette by index over the two option vectors. The mac's `color:
NSColor` mapping and its APCA test are host-designated (a colour is a
rendering). Pinned by the GraphConfigTests SCHEMA cases as Rust facts
(round-trip of every section, the empty object as the default, unknown
top-level keys preserved, the unparseable and newer-version refusals as
errors, clamping, first-match, the ring cycle), `decode_truth_table`
(every row above: each version category including the f64 witness,
each depth category, a mixed groups array, each number category,
invalid JSON and an overflowing exponent, non-finite outbound),
`encode_is_canonical_bytes` (a golden string with nested unknown keys,
escaping, indentation), `option_vectors_are_the_enum_arms` (the palette,
the ring styles, the modes, the verbosities, the per-kind action specs),
and the artifact's config round-trip; the host I/O facts stay Swift
(0b-14).

**0b-13 — The §W-A `graph_queries` section, from a dedicated graph
vault, keyed by stable key, id-free, schema-checked (design C).** The
harness gains one artifact, `graph_queries.json`, produced from a NEW
fixture vault `crates/slate-core/tests/fixtures/graph_vault/` opened as
a second session behind `--graph-fixtures` — the markdown vault has no
digit-run labels, no nested folder, no authored-variant ghosts, and its
attachments fall outside the default filter (0bD-4, revised). The vault,
each file a witness: `hub.md` (links to `2`, `10`, `010`,
`notes/nested/deep`, the ghosts `Ghost One`, `./Ghost One`, `/ghost
two`, `café`, `İstanbul`, the embed `![[pic.png]]`); `2.md` (`[[hub]]`);
`10.md` (`[[hub]]` and `![[hub]]` — the Link+Embed pair); `010.md`
(`![[hub]]` — embed-only); `notes/nested/deep.md` (`[[2]]`, `[[Ghost
One]]` — the diamond); `self.md` (`[[self]]`); `orphan.md` (no links);
`pic.png` (the markdown vault's `tiny.png` bytes). Both lanes copy the
vault RECURSIVELY with relative paths and parent directories (the
Windows `CopyTree`, the mac's `copyItem` per top-level item, which
copies a directory whole) — the canvas pass's flat copy would turn the
nested note into a ghost, and the inventory fact would fail on it. Its
stable-key inventory, byte order, is a literal in the census and the twin:
`g:café`, `g:ghost%20one`, `g:ghost%20two`, `g:i%CC%87stanbul`,
`p:010.md`, `p:10.md`, `p:2.md`, `p:hub.md`, `p:notes/nested/deep.md`,
`p:orphan.md`, `p:pic.png`, `p:self.md`. The serializer copies every
core record into an ARTIFACT SHAPE with no numeric-id field (design C);
the ELEVEN sections, in order: `snapshot` under the INCLUSIVE filter
(attachments and ghosts in, orphans-only off), SORTED BY THE SERIALIZER
into `stable_key` byte order (core's own snapshot order is `NodeKey`'s,
paths before ghosts; the sort is the serializer's UTF-8 comparison, the
twin's `utf8` comparison the same) — per node `key`, `label`, `kind`,
`path`, `in_links`, `out_links`, `in_embeds`, `out_embeds`, `component`,
`is_orphan` (NOT `pagerank`: a float no 0b query consumes; NOT
`modified_ms`: the checkout time); `visibility` — for each pinned query
its `query` name, `total`, the `visible` keys and the `labeled` keys in
core's order; `topology` — for the `all` query under the pinned config,
in core's order: `nodes`, per node `key`, `diameter_x100` (the curve
rounded to an integer), `group`, `labeled`, `neighbors` as keys, and
`edges`, per edge `from`, `to` (keys) and `kind`; `table` — for each
pinned sort its `query` name, `sort` name, `total`, and in core's order
per row `key` and the eight cells named `note`, `links_in`, `links_out`,
`embeds_in`, `embeds_out`, `component`, `folder`, `kind` (Modified
OMITTED, no placeholder: the checkout time; its format is a Rust fact);
`connections` — for each pinned `(path, depth)` the `center_key`, the
`summary` (`center_label`, `in_links`, `out_links`, `note_count`,
`depth`), and the flat rows in core's order with `occurrence`, `parent`,
`level`, `key`, `kind`, `embed_only`, `references`; `ghost_paths` — for
each pinned target the path; `spatial` — the pinned point table's answer
per direction, as the point's LABEL (the table is labelled `a`..`d`; ids
are local); `structural` — the pinned order's next and previous from
each label and from none; `constants`; `actions` per kind with titles;
`config` — the pinned input decoded then encoded, the output text and
the unknown keys. The pinned vectors, literal, `public static readonly`
on `SurfaceSerializer` and copied by the twin, named in a comment beside
each: queries — the inclusive filter × needles `""`, `hub`, `HUB`,
`café`, `cafe`, `ghost`, `istanbul`, `  2  `, `\n10\n`, `zzz`; the
default filter × `""`; the inclusive filter × `""` × `kind_only =
Ghost`; orphans-only × `""` (thirteen, named `all`, `all:hub`, …,
`all:padded-2`, `all:newline-10`, `all:zzz`, `default`,
`all:ghosts-only`, `orphans`); sorts — each of the eight non-Modified
columns ascending and descending (sixteen) over the `all` query;
connections — `hub.md` at depths 1, 2, 3, `notes/nested/deep.md` at 2,
`10.md` at 1, `self.md` at 1; targets — `Ghost One`, `./Ghost One`,
`/ghost two`, `notes/Foo.MD`, `dir/`, `.md`, `a\b`, `café`; points —
`a (0,0)`, `b (10,0)`, `c (0,10)`, `d (10,10)` with neighbours of `a` =
`[d]`, from `a`, the four unit axes; structural order `[a, b, c, d]`;
the three kinds; the constants' seven field names; the config input — a
v1 object with a nested unknown key, a groups array mixing a valid group,
a number, a query-less object and a group with unknown tags, a
fractional force, an out-of-range depth — and its decoded field names. A
literal example of the schema is committed beside the golden directory
as `crates/slate-core/tests/fixtures/graph_queries.example.json` (the
eleven keys, each list trimmed to its first entry; the census asserts its
key set). Four Windows census facts, each against something the artifact
does not own: GraphQueriesArtifactMatchesTheVaultInventory — the
snapshot section's key LIST equals the literal inventory, and the
`topology` nodes' and every `table` sort's key LISTS, sorted, equal it
(exact lists — a duplicate or a missing entry fails; never a set); GraphQueriesArtifactCarriesThePinnedVectors — the
top-level key set is exactly the eleven names, each section's length
equals its pin's, and each entry's pin fields (`query`, `sort`, `path`
and `depth`, `target`, `dx`/`dy`, `from`/`forward`, the kinds, the
constants' names, the config input and field names) equal the pin in
order; GraphQueriesArtifactMatchesItsSchema — the schema walk of design
C over every value, the `parent` rule included (null exactly at level
1; otherwise an earlier occurrence of the same list that prefixes the
row's own); GraphQueriesArtifactCarriesNoNodeIds — the golden
text has no property named `id`, `node_id`, `parent_id`, `center_id`,
`source_id`, `target_id` or `generation`, and has `occurrence` and
`parent`. The mutation sweep makes the serializer write a numeric id at
each key-bearing location in turn, write a forbidden property, drop one
node, duplicate one topology node and one table row, drop one section,
reorder one section, drop one pin field and write a level-1 parent, each
failing exactly one named fact. The mac twin is Task 0b-2's, in the same
PR; the committed golden arbitrates.

**0b-14 — Mac consumes in the same PR (Task 0b-2); every consumer is
owned by name, production and test.**

*The token and the authorities (design A).* `AppState` holds one
`graphVisibilityQuery` (filter, needle, kind), one `graphTableRequest`
(the query and the accepted sort), and per consumer a `seq` that every
input change advances; a result is applied through ONE receiver per
consumer (`receiveGraphTableRows(token:result:)` against the snapshot's
generation; the diagram's `acceptGraphTopology` against the layout's
backend filter and generation; the leaf's tree replacing the held tree)
that checks the token in every field and the authority's identity, and
publishes rows, accepted sort and headline in one synchronous
assignment, rows first; a failed query rolls the request back to the
accepted state and announces through the announcer.

*The visible set and the counts.* `graphNameMatches`
(`AppState+GraphConfig.swift:16–20`) and the count's copy
(`AppState+GraphTable.swift:228–238`) are deleted; the table's rows are
`graph_table_rows(query, sort)`'s and the count announced on publish is
`rows.count` of `total`; a needle or kind change is a token change that
reissues the rows request.

*The topology, per epoch.* The diagram holds ONE `GraphTopology` beside
the model, fetched through `graphTopologyForDiagram` when the semantic
epoch — (the layout handle, `model.generation`, `graphVisibilityQuery`,
`graphConfig`) — changes and NOT on a settling frame: `applyFrame`
(`GraphDiagramView.swift:328–349`) updates positions and paints from the
cache; `isVisible` / `refreshVisibleSet` (`:385–408`),
`labelPriorityIDs` (`:640–648`), the per-node `matchingGroup` (`:531`)
and diameter (`:412–413, 522`), `rebuildEdges` (`:702–722`),
`neighborCustomContent` (`:856–874`) and the spatial `graphNeighbors`
(`:1221–1228`) all read the record; a topology under another backend
filter than the layout's, or another generation, is dropped and the
visible set empties until `refreshGraphDiagramIfGraphChanged`'s
adoption (`AppState+GraphDiagram.swift:110–145`) changes the epoch.

*The tree.* ConnectionsModel (`ConnectionsPanel.swift:375–499`) becomes
an adapter over `graph_connections_tree`, held on `AppState` as
`connectionsTree` — the tree is the leaf's authority: a token-current
result replaces it; the leaf's summary reads the tree's
`summary_counts`, `connectionsNeighborhood` is no longer loaded; the
adapter nests the flat rows by `parent_id` for `OutlineGroup` in one
pass and overlays the bundle's snippets by path as decoration (0bR-4);
its derivation is deleted.

*The key.* `GraphNodeKey` (`GraphViewState.swift:57–78`) is deleted;
node sites read `stableKey` (`GraphTableView.swift:492`,
`AppState+GraphDiagram.swift:194, 209`, `AppState+GraphTable.swift:248`),
path-only sites call `graph_stable_key_for_path`
(`AppState+Connections.swift:185, 210, 235`).

*The table.* `GraphTableColumn.byLabel` / `directionalComparator` /
`cell` / `header` / `columns` and `GraphTableRow.init(node:folder:)`
(`GraphTableView.swift:444–628`), the view's `allRows` / `filteredRows`
(`:300–315`) and `AppState.folder(of:)` (`AppState+GraphTable.swift:418–420`)
are deleted — the rows arrive filtered and formatted; the publish point
pairs the snapshot with the filter it was fetched under, and a rows
result under another filter is dropped; the generated
`GraphTableRow` gets `Identifiable` by `stableKey`; the grid's columns
are built from `graphTableColumns()` fetched once (a static), the sort
state's index is that vector's index (`:212–213, 335`; GraphTableViewTests
`:81–82`), and each column's value is `cells[index]`. THE SORT
LIFECYCLE: `sortsRowsLocally: false`; the grid's `sortState` binding
reads the ACCEPTED sort and its setter advances the token with the
REQUESTED sort; the receiver publishes rows and the accepted sort
together or drops the result. The preset's headline
(`AppState+GraphTable.swift:340–360`) is the receiver's: a preset sets
filter, kind, needle and the default sort in one token, and the
headline is the published result's first row or count.

*Constants, steps, path.* `GraphDiagramModel.tierBThreshold`,
`GraphDiagramNSView.nodeDiameter` and `labelCap` read `graph_constants`
(a static); `bestInDirection` and `structuralMove` (`:1230–1262`) call
the two steps with the topology entry's neighbours;
`clampConnectionsDepth` reads the constants; `ghostNotePath`
(`AppState+Connections.swift:306–315`) calls core.

*Actions and the option tables (design B).* `GraphRowAction`
(`GraphViewState.swift:15–48`) is the generated enum plus a Swift
extension whose `allCases`, `title`, `actions(forGhost:)` and
`applies(toGhost:)` are read from statics fetched once
(`graph_row_actions` per kind, `graph_row_action_title`), so
`ConnectionsPanel.swift:259`, `GraphDiagramView.swift:800`,
`GraphTableView.swift:394, 403`, GraphActionParityTests `:19–35` and
GraphDiagramTests `:707–750` compile unchanged in meaning; the runtime
admission overlay (0bD-8) stays at each site as shipped. GraphColorToken
and `GraphRingStyle` (`GraphConfig.swift:109–142`) are the generated
enums plus extensions whose `allCases` and `title` are read from
`graph_color_tokens()` / `graph_ring_styles()` fetched once, so the
pickers (`GraphInspectorView.swift:90, 95`) iterate core's vectors;
`GraphVerbosity` and `GraphSurfaceMode` (`GraphAnnouncer.swift:19–94`)
likewise read `graph_verbosities()` / `graph_surface_modes()` for
`allCases`, `title` and the persistence tag, so the mode switcher
(`GraphTableView.swift:134`) and any verbosity menu iterate core's
vectors; `color` and `dashPattern` remain renderings; a mac test pins
each vector's order against the generated enum's cases.

*Config.* GraphConfigStore's `decode` / `encode` / `clampD` (`:131–202`)
are deleted; `read` calls `graph_config_decode` and maps
GraphConfigError onto `PrefsJsonStoreError.parseFailed`; `write` calls
`graph_config_encode(config, existing)` and maps onto `writeFailed`; the
I/O, the atomic rename and the writer actor stay. The Swift `GraphConfig`,
GraphFilterConfig, `GraphDisplay`, GraphForcesConfig and `GraphGroup`
(`GraphConfig.swift:11–108`) are the generated records plus extensions:
`GraphConfig.default = graphConfigDefault()`, `GraphFilterConfig.backend`,
`GraphForcesConfig.layoutForces`, `matchingGroup(for:)` over core;
`addGraphGroup` (`AppState+GraphConfig.swift:172–177`) calls
`graph_config_next_group_style`. VERBOSITY: `loadGraphConfig` (`:40–53`)
applies the loaded config through `applyLoadedGraphConfig` — the
aggregate, the depth, and `graphAnnouncer.verbosity = cfg.verbosity` —
and its failure path through `applyGraphConfigLoadFailure` (defaults,
`.standard`, read-only); `scheduleGraphConfigSave` (`:77–86`) persists
`graphConfigSaveAggregate()`, which folds the live filter, depth and
the announcer's verbosity into the config; a verbosity change schedules
a save.

*Tests.* GraphTabRoutingTests `:353–356`, FileTreeDragDropTests' key
site, GraphDiagramTests `:775, 788, 806` and GraphActionParityTests
`:51–69` read `stableKey` / `graphStableKeyForPath`; the SCHEMA cases in
ConnectionsPanelTests, GraphTableViewTests, GraphConfigTests and
GraphDiagramTests move to Rust facts (named in 0b-4..0b-12); the Swift
tests that remain assert the host: ConnectionsPanelTests — the nesting
by `parent_id`, the snippet overlay as decoration (an unmatched path
ignored), the busy reason, the leaf's first load establishing its
authority and a mutation's tree replacing it; GraphTableViewTests — the
column adapters over the specs, the `stableKey` identity, the action
extensions, the busy hints, the token lifecycle (a delayed result, a
reordered pair of same-generation results under different needles, a
failed query rolling back, a stale generation dropped, a preset
publishing rows and the default sort together with its headline from
row zero after a Folder sort); GraphConfigTests — the host I/O facts
kept verbatim (`testMissingFileReadsDefault`,
`testRefusesToClobberUnparseableFile`,
`testRefusesToDowngradeANewerVersionFile`,
`testWriterDropsASupersededGeneration`, the byte-intact assertions)
plus the adapter's error mapping for each GraphConfigError, the
verbosity load / fallback / save, and the pickers' vectors against the
enum cases; GraphDiagramTests — the steps through core on the fixed
layout, the tier boundary through `graph_constants`, the topology token
(a mutation between adoption and the query is dropped; a topology under
another backend filter is dropped), the call-count fact (at least two
non-converged frames under one epoch → one fetch; then one more per
changed component — the query, the config, the generation through an
adoption), the row copy, Where-am-I and the navigation actions from the
topology entry, the drawn edges from the record, the busy-state
omission; GraphKeyCharacterizationTests — the old key
algorithm beside core's over 0b-D1's witnesses. The Swift edit is unrun
on this box (0bR-1); the mac lane arbitrates.

**0b-15 — Tripwires cover the surface, the records, their types and the
vectors.** `the_ffi_mirror_covers_every_graph_query_enum` (slate-uniffi)
parses core's `GraphRowAction`, GraphTableColumn, GraphColorToken,
`GraphRingStyle` and GraphConfigError against their mirrors both ways,
the a11y mirror test's parser reused, with each arm count pinned (5, 9,
8, 4, 2) and, for GraphConfigError, each variant's payload field names.
`the_ffi_mirror_exports_every_graph_query`: core declares
GRAPH_QUERY_SURFACE — the twenty-two names in 0b-2's table — a core
test asserts each is a `pub fn` of `graph_queries`, `graph_config` or
the session, and the uniffi test asserts each is exported from `lib.rs`
(a `pub fn name(` under `#[uniffi::export]` or a `VaultSession` method).
`the_ffi_records_mirror_core_field_for_field`: every `pub struct` in the
two modules AND every `graph.rs` record the surface reuses — `GraphNode`,
`GraphEdge`, `GraphFilter`, `GraphNeighborhoodCounts` — is parsed for
its `(field, type)` pairs and compared, in order, against the mirror of
the same name in `lib.rs`, the types compared as text after the one
alias map core→FFI (`NodeKind`→`GraphNodeKind`,
`EdgeKind`→`GraphEdgeKind`, path prefixes dropped, `usize` disallowed) — a width or an `Option` that drifts fails here, not in a
host. `option_vectors_are_the_enum_arms` (core): `graph_color_tokens()`,
`graph_ring_styles()`, `graph_surface_modes()`, `graph_verbosities()`,
`graph_table_columns()` and the per-kind `graph_row_actions` are exactly
their enums' arms in declaration order, and every tag round-trips. `graph_constants_are_the_named_constants`
asserts every `GraphConstants` field against its constant. The Windows
`ParityHarnessCensus` gains the artifact and the four facts (0b-13), and
GraphQuerySurfaceCensus asserts by reflection that every name in core's
surface list exists on the generated `SlateUniffiMethods` or
`VaultSession` (the C# spelling), the list read from core's source; the
Swift twin references each name once in `ParityHarnessTests.swift` so
the binding is compile-checked. ConnectionsPanelTests,
GraphTableViewTests, GraphConfigTests and GraphDiagramTests keep the
host-side cases and lose the schema cases, each named in 0b-14.

### Round 1 ledger (25 findings; disposition by revision 2, re-verified by rounds 2 and 3)

| # | Finding (severity) | Disposition |
|---|---|---|
| IG0b-1 | generation-less ids (B) | discharged by design A: every result carries `generation`; the authorities and their identities |
| IG0b-2 | the Unresolved preset inexpressible (B) | discharged — `GraphVisibilityQuery { filter, name_query, kind_only }` |
| IG0b-3 | accessible neighbours left in Swift (B) | discharged — the topology entry's neighbour records; the self-edge recorded as 0b-D9 (IG0b-49) |
| IG0b-4 | rows without every cell's text (B) | discharged — `cells` of nine |
| IG0b-5 | external sort not atomic (B) | discharged — one publish; the preset case by design A |
| IG0b-6 | config ABI incomplete (B) | discharged — the IDL; the typed record test |
| IG0b-7 | label-only groups vs p2_spec (B) | discharged — 0bD-9 / 0b-D7 |
| IG0b-8 | occurrence ids carry node ids (B) | discharged — key-built ids; the schema walk (design C) |
| IG0b-9 | the coverage census self-referential (B) | discharged by design C's rule: a typed schema over every value, a pin per section, a mutation per key-bearing location (IG0b-45) |
| IG0b-10 | the migration leaves consumers behind (B) | discharged — 0b-14 names `rebuildEdges` and the pickers too (IG0b-43, -47) |
| IG0b-11 | label priority nondeterministic (M) | discharged — `labeled` |
| IG0b-12 | the key's source divergence unrecorded (M) | discharged — 0b-D1 with the 7-bit rule, the characterization test, the summary corrected (IG0b-52) |
| IG0b-13 | the comparator claim false (M) | discharged — 0b-D2 with Folder |
| IG0b-14 | natural digits not total (M) | discharged — the prefix rule |
| IG0b-15 | two filter implementations, trim divergence (M) | discharged — one production fold; the count from the rows |
| IG0b-16 | sorted keys false under `preserve_order` (M) | discharged — the canonical writer |
| IG0b-17 | no decode truth table (M) | discharged — the table classifies the parsed value (IG0b-39, -50) |
| IG0b-18 | verbosity decoded, never applied (M) | discharged — 0b-14 "config" |
| IG0b-19 | eligibility vs runtime admission (M) | discharged — 0b-9 states each site's shipped behaviour |
| IG0b-20 | hot-loop FFI (M) | discharged by design B's rule: one record per epoch, never per frame (IG0b-48), the statics, the count from the rows |
| IG0b-21 | the parity scenario unenumerated (M) | discharged — the graph vault and the vectors; enforcement by design C |
| IG0b-22 | ghost path platform-dependent (M) | discharged — 0b-11 |
| IG0b-23 | tripwires cover enums only (M) | discharged — 0b-15's `(field, type)` pairs, `GraphNode`, the payloads, the vectors |
| IG0b-24 | step edge cases unspecified (m) | discharged — 0b-10 |
| IG0b-25 | artifact ordering ambiguous (m) | discharged — the serializer's byte order stated as the serializer's (IG0b-44); eleven keys |

### Round 2 — thirteen findings, dispositions; rule 5 invoked

Round 2's verdict: revise, with eight live blockers, three of them
created by revision 2's fixes — rule 5, which makes the round count
double, so rule 4's stop applied at round 2. The design pass above is
that stop; the dispositions follow from it.

| Finding | Severity | Disposition |
|---|---|---|
| IG0b-26 | BLOCKER | taken — 0bD-12: the revision names supersede the spec's seed names; §2 rows B–K, §4 PR 0b and PR A–E amended in the same PR (completed for §4 and PR A in revision 4, IG0b-46) |
| IG0b-27 | BLOCKER (created by rev 2) | taken by design B — `graph_table_columns` returns ordered specs; the grid is built from the vector; generalised in revision 4 to every ordered inventory (IG0b-47) |
| IG0b-28 | BLOCKER | taken by design A — a preset is one token that sets filter, kind, needle and the default sort; rows, the accepted sort and the headline publish together; the after-Folder case is a mac test |
| IG0b-29 | BLOCKER (created by rev 2) | taken by design A — recovery per consumer class; the leaf's tree is its own authority (revision 4, IG0b-41) |
| IG0b-30 | BLOCKER | taken by design A — the load token is (session, the full request, seq); every input change advances seq; publication checks the token in every field and the authority's identity |
| IG0b-31 | BLOCKER (created by rev 2) | taken by design C — artifact-only shapes, `center_id` forbidden, and in revision 4 the schema walk over every value (IG0b-45) |
| IG0b-32 | MAJOR | taken — 0b-D8 records the strict version rule against the mac's `as? Int` classification; restated over the parsed value in revision 4 (IG0b-39) |
| IG0b-33 | MAJOR | taken — the depth rule in 0b-12: an integral value clamped, the mac's classification kept (IG0b-50) |
| IG0b-34 | MAJOR | taken — 0b-D1 rewritten: Foundation's 7-bit allowed set encodes every non-ASCII byte; the `İ` filter claim withdrawn; GraphKeyCharacterizationTests carries the old algorithm |
| IG0b-35 | MAJOR | taken — 0b-D2 and 0b-7 record Folder |
| IG0b-36 | MAJOR | taken — the host I/O and actor facts stay Swift verbatim |
| IG0b-37 | MINOR | taken — eleven sections; the census asserts the exact key set |
| IG0b-38 | MINOR | taken — invalid JSON and overflowing exponents are `Unparseable`; the non-finite rule is encode's only |

### Round 3 — fourteen findings, dispositions; rule 5 again

Round 3's verdict: revise, with nine blockers, three created by revision
3 — rule 5 again — and the design pass judged not to close any of its
classes. Revision 4 restates each model as a rule; the dispositions
follow from the rules.

| Finding | Severity | Disposition |
|---|---|---|
| IG0b-39 | BLOCKER (created by rev 3) | taken — 0b-12 classifies the PARSED VALUE: `1.0000000000000001` is `1.0` in `f64` and reads as 1, stated; no `arbitrary_precision`; the witness and the `u64` boundary pinned |
| IG0b-40 | BLOCKER | taken by design A's rule — the diagram's authority is (the layout handle, its backend filter, its generation); a topology under another filter is dropped; a mac test |
| IG0b-41 | BLOCKER (created by rev 3) | taken by design A's rule — a result that IS the authority replaces it: the leaf's first load establishes the tree, a mutation's tree replaces it; a mac test |
| IG0b-42 | BLOCKER | taken — the snippet overlay is decoration (0bR-4): keyed by path, an unmatched path ignored, re-paired by the leaf's generation refresh; the structure never depends on it; the owner may overrule toward a bundled record |
| IG0b-43 | BLOCKER | taken by design B's rule — `GraphTopology.edges`, `rebuildEdges` draws them, the artifact's `topology.edges`, `topology_carries_the_visible_edges` |
| IG0b-44 | BLOCKER (created by rev 3) | refuted in part, clarified — the serializer SORTS the snapshot section into byte order itself (`CompareUtf8`, the twin's `utf8` comparison), so the list equals the byte-ordered inventory; 0b-13 now says so and names core's own order for the topology and table sections |
| IG0b-45 | BLOCKER | taken by design C's rule — the schema walk over every value with the inventory and the numeric allow-list, a pin for each of the eleven sections, a mutation per key-bearing location and per section |
| IG0b-46 | BLOCKER | taken — spec §4 PR 0b Goal/Builds/Behavior/Hand-off and PR A Consumes/Builds amended to 0b-2's surface and the D1–D9 policy (0bD-12) |
| IG0b-47 | BLOCKER | taken by design B's rule — `graph_color_tokens` / `graph_ring_styles` vectors; the pickers and `allCases` built from them; pinned against the arms on both lanes |
| IG0b-48 | MAJOR | taken by design B's rule — the topology per SEMANTIC EPOCH, cached across settling frames; a call-count fact |
| IG0b-49 | MAJOR | taken — 0b-D9 records the self-edge: not a neighbour in core, the mac's diagram content listed the node itself, its tree did not; owner-recorded |
| IG0b-50 | MAJOR | taken — the depth keeps the mac's integral classification: a fractional value is the default; no divergence |
| IG0b-51 | MINOR | taken — `graph_neighbors` yields `neighbors: []` for an absent or invisible id; every session query is `Result<_, VaultError>` |
| IG0b-52 | MINOR | taken — 0b-D1's summary: the stored `NodeKey::Ghost` over `target_raw` against the displayed label |

### Round 4 — twelve findings, dispositions; rule 5 for the third time; THE FREEZE

Round 4's verdict: revise, with eight blockers, three created by
revision 4 — and the judgement that each rule now catches its next
instance while the contracts derived from them still had holes. Rule 5
for the third time is the precedent's condition: the section is FROZEN
at revision 5, the text corrected for every finding below as the
discharge, and the findings carried as the ledger the task loop
discharges by code. **Precedent applied; the owner may overrule.**

| Finding | Severity | Disposition |
|---|---|---|
| IG0b-53 | BLOCKER | ledger — discharged in the frozen text (design A, 0b-2b: the snapshot's identity is the filter it was fetched under and its generation) and by code: the publish point pairs them, the receiver checks both, the filter-B-rows-on-filter-A-snapshot test |
| IG0b-54 | BLOCKER | ledger — discharged in the frozen text (0b-6b: `path`, the four degrees, `component`, `is_orphan` on the topology node) and by code: the row copy, Where-am-I and the actions read the entry; a topology-only consumer test |
| IG0b-55 | BLOCKER (created by rev 4) | ledger — discharged in the frozen text (0b-2, 0b-9, 0b-12: GraphRowActionSpec, `graph_surface_modes`, `graph_verbosities`; twenty-three names) and by code: the mode switcher and the verbosity extension read the vectors |
| IG0b-56 | BLOCKER (created by rev 4) | ledger — discharged by the spec amendment in the same commit: §2 row D and PR D read "per semantic epoch", PR A consumes the action specs and the modes, PR C the verbosities, PR E the palette and ring vectors |
| IG0b-57 | BLOCKER | refuted for the drafted lanes, recorded — both copies are recursive (0b-13 now says so: `CopyTree` with relative paths; `copyItem` per top-level item); the inventory fact fails on a flattened vault since `p:notes/nested/deep.md` is a listed key |
| IG0b-58 | BLOCKER (created by rev 4) | ledger — discharged in the frozen text (design C, 0b-13: `parent` null exactly at level 1, otherwise an earlier occurrence that prefixes the row's) and by code: the walk's rule and the level-1-parent mutation |
| IG0b-59 | BLOCKER | ledger — discharged in the frozen text (sorted key LISTS, exact) and by code: the fact and the duplicate-insertion mutations |
| IG0b-60 | BLOCKER | ledger — discharged in the frozen text (0b-14, §W-G G: `allRows` / `filteredRows` `:300–315`, `folder(of:)` `:418–420` deleted) and by code: the residue greps |
| IG0b-61 | MAJOR | ledger — discharged in the frozen text (0b-15: `GraphEdge`, `GraphFilter`, `GraphNeighborhoodCounts`; `EdgeKind`→`GraphEdgeKind`) and by code |
| IG0b-62 | MAJOR | ledger — discharged in the frozen text (0b-12: integral within `i64`, beyond it the default — the mac's `Int` bridge, boundary included) and by code: the boundary witnesses |
| IG0b-63 | MAJOR | ledger — discharged in the frozen text (0b-D3 extended to group matching) and by code: the characterization witness |
| IG0b-64 | MAJOR | ledger — discharged in the frozen text (design B, 0b-14: at least two non-converged frames per epoch, one more fetch per changed component) and by code |

### Task loop — records (PR 0b)

**TG0b-0 — Baselines.** `GraphNode` twelve fields; the FFI graph
records at `lib.rs:3227–3480`; `crates/slate-core/tests/fixtures/parity_golden`
30 artifacts; the markdown vault untouched; no `graph_queries.rs`, no
`graph_config.rs`, no graph vault; the graph citation census floor 7
over 8 at revision 5, before the code existed; GRAPH_QUERY_SURFACE
twenty-three names by the frozen text.

**TG0b-1 — Core: the two modules, the session, the fold.**
`crates/slate-core/src/graph_queries.rs` (the constants, the stable key
and its encoder, the fold and the predicate, `GraphVisibilityQuery` /
`GraphVisibility` / `visibility`, `GraphNeighbor` / `GraphNeighbors` /
`neighbors`, `GraphTopologyNode` / `GraphTopology` / `topology` with the
visible edges, the segment-wise natural order and `label_order`,
`GraphTableColumn` / `GraphTableColumnSpec` / `table_columns`,
`GraphTableSort`, `GraphTableRow` with its nine `cells` /
`GraphTableRows` / `table_rows` / `compare_rows`, `GraphRowAction` /
`GraphRowActionSpec` / `row_actions`, `ghost_note_path`, `GraphPoint` /
`spatial_step` / `structural_step`, `GraphConnectionRow` /
`GraphConnectionsTree` with `summary_counts` / `connections_tree` with
key-built occurrence ids, GRAPH_QUERY_SURFACE) and
`crates/slate-core/src/graph_config.rs` (the two enums with tags and
titles, the option-table specs and `color_tokens` / `ring_styles` /
`surface_modes` / `verbosities`, the five records, GraphConfigError,
`GraphConfigRead`, `default`, the version rule over the parsed value,
`decode` with the truth table and the `as_i64`-first depth, the
canonical writer, `matching_group`, `next_group_style`); `GraphNode`
gains `stable_key` (filled by `graph_node_payload`); the session's
`graph_neighborhood` splits into the lock and `graph_neighborhood_locked`
so `graph_connections_tree` reads the payload and the generation under
ONE lock; `graph_visibility`, `graph_topology`, `graph_neighbors`,
`graph_table_rows` on the session; 0a's test fold delegates to the one
production definition. Facts: 27 in `graph_queries` (the five tree
facts with the counts, the key and its encoder, the fold and the visible
set, the capped priority set, the topology's nodes and its edges, the
neighbours, the natural order with the exhausted prefix and the
thousand-digit run, the comparator with Folder's tiers, the nine cells,
the column specs, the UTC minute, the constants, the curve, the tier,
the parity action set, the steps and their totality, the ghost path's
sixteen cases, the surface list), 10 in `graph_config` (the eight schema
facts, the truth table with the `f64` witness and the `i64` depth
boundary, the canonical bytes, the option vectors), the session's
`session_results_carry_the_generation_they_read` (37 in
`session::tests::graph` green with it), a11y's 25 unchanged.

**TG0b-2 — FFI: the mirrors and the three tripwires.** Twenty-two
records mirrored field for field (`GraphConstants`, `GraphVisibilityQuery`,
`GraphVisibility`, `GraphNeighbor`, `GraphNeighbors`, `GraphTopologyNode`,
`GraphTopology`, `GraphPoint`, `GraphTableColumnSpec`, `GraphTableSort`,
`GraphTableRow`, `GraphTableRows`, `GraphConnectionRow`,
`GraphConnectionsTree`, `GraphFilterConfig`, `GraphGroup`, `GraphDisplay`,
`GraphForcesConfig`, `GraphConfig`, `GraphConfigRead`, `GraphGroupStyle`,
`GraphColorTokenSpec`, `GraphRingStyleSpec`, `GraphSurfaceModeSpec`,
`GraphVerbositySpec`, `GraphRowActionSpec`), five enums both ways,
GraphConfigError as a uniffi error, `GraphFilter` and `GraphEdge` gain
`PartialEq`/`Eq` so the new records can derive it, the core→FFI
direction for `GraphSurfaceMode` and `GraphVerbosity`, the nineteen free
functions and the four session methods of 0b-2. Tripwires:
`the_ffi_mirror_covers_every_graph_query_enum` (5, 9, 8, 4, 2 and the
error payloads), `the_ffi_mirror_exports_every_graph_query` (the
twenty-three names, each under `#[uniffi::export]` or on
`VaultSession`), `the_ffi_records_mirror_core_field_for_field` (every
`(field, type)` pair of the two modules' records and of `GraphNode`,
`GraphEdge`, `GraphFilter` and `GraphNeighborhoodCounts`, under the alias
map) — 6 green in the crate.

**TG0b-3 — The graph vault, the harness, the golden, the censuses.**
`crates/slate-core/tests/fixtures/graph_vault/` (`hub.md`, `2.md`,
`10.md`, `010.md`, `notes/nested/deep.md`, `self.md`, `orphan.md`,
`pic.png`); `SurfaceSerializer.GraphQueriesArtifact` with the pinned
vectors (thirteen queries, sixteen sorts, six connections, eight targets,
the labelled point table, the structural order, the config input),
`CopyTree` and `CompareUtf8`; Program.cs's `--graph-fixtures` pass (31
artifacts); the golden `graph_queries.json` and the example
`crates/slate-core/tests/fixtures/graph_queries.example.json` (eleven
keys). The four facts plus the reflection census
(`GraphQuerySurfaceCensus.EveryCoreGraphQueryIsBound`, the list read from
core's source): 16 of 16 in the parity and surface census run, the
byte-for-byte fact over all 31 artifacts among them. Discharges recorded in the code against the
frozen text: the topology's edge endpoints are written as `from_key` /
`to_key` (0b-13 said `from` / `to` — the schema walk types values by
name, and `from` / `to` are the step sections' labels while `target` is
the ghost-path pin), and the UTC witness's timestamp names the minute it
actually is; the mac lane's characterization test overturned the
sharp-s witness (0b-6 said `ß` matches `ss` on neither; Foundation
matches it), corrected in the frozen text and in 0b-D3 — the test doing
exactly what 0b-3 built it for.

**TG0b-4 — Mac (Task 0b-2), unrun on this box (0bR-1).**
`g0b-mac-edits.py` applies 0b-14 site by site: `GraphViewState.swift`,
`GraphConfig.swift` and the two extensions in `GraphAnnouncer.swift`
read core's vectors; `GraphConfigStore.swift` keeps the I/O and maps
GraphConfigError; `AppState+GraphConfig.swift` applies and persists
the verbosity; `AppState+Connections.swift` loads the tree as one
record; `AppState.swift` holds the query, the token state and the
snapshot's filter; `AppState+GraphTable.swift` issues tokens, publishes
rows and the accepted sort together and reads the preset headline from
the result; `AppState+GraphDiagram.swift` accepts a topology against the
layout's filter and generation; `GraphDiagramView.swift` fetches per
epoch, draws the record's edges and reads the entry for the row copy,
the neighbours, the styling and the actions; `GraphTableView.swift`
builds its columns from the specs; `ConnectionsPanel.swift` nests core's
rows; the tests named in 0b-14 with GraphKeyCharacterizationTests. The
mac lane arbitrates; a red lane is fixed here.

**TG0b-5 — Mutations.** Twenty-one, each byte-restored, each caught
by exactly the fact that guards it (`g0b-mutations.py`, the working
tree for the Rust ones and the committed golden's text for the artifact
ones — what a mutated serializer would have written). Rust: M1 the tree
keeps the self-edge → `tree_omits_a_self_edge_from_both_lists`; M2
more leading zeros sort first → `label_order_is_natural_folded_and_total`;
M3 the fold keeps combining marks → `filter_ids_fold_case_and_marks`; M4
eight cells → `table_cells_are_nine_and_formatted`; M5 lowercase hex in
the key → `stable_key_percent_encodes_by_rust_alphanumerics`; M6 no
cosine threshold → `spatial_step_prefers_neighbours_then_falls_back`;
M7 a fractional version accepted → `decode_truth_table`; M8 the writer
without the canonical pass → `encode_is_canonical_bytes`; M9 topology
neighbours ignore visibility →
`topology_is_the_visible_nodes_with_their_neighbours`; M10 a mirror
field widens (`in_links: u64`) →
`the_ffi_records_mirror_core_field_for_field`; M11 a surface name
dropped → `graph_query_surface_names_pub_fns`. Artifact: M12 a
forbidden property → GraphQueriesArtifactCarriesNoNodeIds; M13 a node
dropped, M14 a topology node duplicated, M15 a table row duplicated →
GraphQueriesArtifactMatchesTheVaultInventory; M16 a section dropped, M17
a section reordered, M18 a pin field dropped →
GraphQueriesArtifactCarriesThePinnedVectors; M19 a level-1 parent, M20 a
numeric id under a key list, M21 a numeric id under an edge endpoint →
GraphQueriesArtifactMatchesItsSchema.

**TG0b-6 — Gates.** `cargo test --workspace`: 1,996 passed, 6 failed
— the five `session::tests::dir_tree` censuses this box always fails
(OS error 123; CI-Windows is the oracle, the standing note) and
`perf_guard_root_listing_under_100ms_on_10k_files`, a wall-clock guard
that failed under the 536-second workspace run with three dotnet build
servers resident and passed in isolation in 11 seconds, untouched by
this PR. `cargo clippy --workspace --all-targets -- -D warnings` clean;
`cargo fmt --check` clean; `dotnet format` clean; the whole Windows test
project 2,065 of 2,065. The rebuild from a clean code tree
(`g0b-rebuild.sh`: the scripts, fmt, build, the facts, the tripwires,
clippy, the bindings, the golden, the censuses) is the reproduction; it
ran green on its fourth pass after three corrections — the FFI derives
`GraphFilter` / `GraphEdge` lacked, the clippy lints in the facts and
the `Leaf` alias, and the two schema-walk name collisions that gave the
edges their `from_key` / `to_key` names. The Windows Rust lane on the
PR's third head (7ce8c53, mac test files only; run 33805992482) failed
one session search test, `tag_scope_rows_clear_when_tag_removed`,
untouched by this PR: it rewrites a 17-byte body with another 17-byte
body and re-scans, and the re-scan's fast path skips a file whose mtime
and size are both unchanged — Windows file times move with the ~16 ms
system tick, so a rewrite inside the first write's tick left the stale
tag rows in place. Root-caused, not a flake: the test now rewrites
through the session tests' rewrite-until-the-mtime-advances helper, the
remedy the other slow-path tests already use. Codoki's round on 2c60824
flagged the sharp-s witness as platform-dependent — true of Foundation,
moot for a target that builds for macOS only — and the test now says
so: the Foundation half sits under an `#if os(macOS)` whose other arm
fails loud, so no lane ever asserts an answer nobody measured; the
refutation stands on the thread, the hardening is the discharge.

### Decisions

- **0bD-1 — The tree is flat pre-order rows** with `level` and
  `parent_id`, not a recursive record: both hosts rebuild a hierarchy
  from a list in one pass, and no binding has to carry a self-referential
  record.
- **0bD-2 — Snippets stay host-side, as decoration.** The depth-one
  snippet overlay reads the host's note bundle, not the graph; the tree
  carries paths so the host can key it; the structure never depends on
  it (0bR-4).
- **0bD-3 — Config I/O is host-designated; the schema and merge policy
  are core's.** Atomic temp-and-rename, the single-writer actor and its
  generation gate stay in Swift (§2 row F's tier-3 half) with their
  tests; decode, encode, defaults, clamps, the version rule and the group
  rules are core's.
- **0bD-4 (revised) — The `graph_queries` artifact reads a dedicated
  graph vault** behind `--graph-fixtures`, not the markdown vault: the
  witnesses 0b-5, 0b-3 and 0b-4 need (digit runs, a nested folder,
  authored ghost variants, an in-filter attachment) are not in the
  corpus, and adding them there would perturb every earlier artifact.
- **0bD-5 — The artifact is keyed by `stable_key`** and excludes
  `pagerank`, the modified fields and every numeric id (0b-13).
- **0bD-6 (revised) — Session queries return generation-tagged
  results and hosts publish by token against an authority**, not pure
  functions over marshalled data: one query surface (R-A), and design A
  makes a stale, mismatched or out-of-order answer detectable (0b-2b).
- **0bD-7 — The `verbosity` key lands in the schema now** (0aD-6), so
  PR C's menu and a future mac switch have a field to write — and the mac
  applies it on load from this PR (0b-14).
- **0bD-8 — Runtime admission is host state.** Whether "Create note" can
  run NOW (a structural mutation in flight) is session state the host
  owns; core answers kind eligibility only (0b-9), and each site keeps
  its shipped rendering of unavailability.
- **0bD-9 — Group matching is label-only.** p2_spec §P2-4 says "the same
  matcher as the table filter" and, in the same sentence, that the table
  filter is a label substring; the shipped mac matches labels for both,
  Milestone P closed on it, the graph carries no tags, and §2's register
  names no cheat here — so the mac IS the parity target. Extending both
  matchers to folder and tag is a feature for both lanes, not a parity
  item, recorded as 0b-D7; the owner may overrule.
- **0bD-10 — Occurrence ids are built from stable keys** (0b-4), so the
  tree's ids are byte-stable across generations and lanes and the
  artifact can carry them.
- **0bD-11 (revised) — Per-node and per-edge data come in one topology
  record per semantic epoch** (0b-6b), and constant tables are fetched
  once per process: one FFI crossing per epoch, none per frame; the
  scalar forms remain for the inspector and the tests.
- **0bD-12 (extended) — The contracts' names supersede the spec's seed
  names.** §2 rows B–K, §4 PR 0b and PR A–E's "Consumes" and "Builds"
  were written before the queries existed; this PR amends them to 0b-2's
  names with a note pointing here, so one document is normative for the
  surface.
- **0bD-13 — Every ordered inventory a host iterates is a core vector**
  (design B): the columns, the palette, the ring styles, the actions per
  kind — the host builds its grids, pickers and `allCases` from the
  vector, never from a literal.
- **0bD-14 — Numbers are classified by their parsed value** (0b-12): the
  workspace parses a non-integer token to `f64` and no arbitrary
  precision is wanted; two tokens with one `f64` value are one value, and
  the rule says so rather than promising a distinction the parser cannot
  make.

### Recorded divergences (owner-recorded; off-limits for re-litigation)

- **0b-D1 — The stable key's source, fold and encoding** (0b-3): the
  stored `NodeKey::Ghost` over `target_raw` against the displayed label;
  Unicode lowercase over `en_US_POSIX`; Rust alphanumerics bare over
  Foundation's 7-bit percent-encoding of every non-ASCII byte —
  executable on the mac lane by the characterization test.
- **0b-D2 — Label and folder order are core's natural fold with the
  raw-bytes tier**, not `localizedStandardCompare` with the mac's two
  table tie-breaks, the tree's none and the Folder column's none (0b-5,
  0b-7).
- **0b-D3 — The name filter, the group matcher and the ghost path trim
  with Rust `trim`** and fold with core's fold, not Foundation's options
  and `.whitespaces` (0b-6, 0b-11, 0b-12); the two folds agree on every
  pinned witness but the sharp s (`Straße` under `strasse`: Foundation
  matches, core does not — found by the characterization test on the
  mac lane, recorded here).
- **0b-D4 — The Modified cell is `YYYY-MM-DD HH:MM` UTC** (0b-7).
- **0b-D5 — The config file's bytes are core's canonical writer** —
  sorted keys, serde's pretty form, unknown values re-emitted
  canonically — not Foundation's pretty printer (0b-12).
- **0b-D6 — An invalid group element is skipped, not the whole array**
  (0b-12).
- **0b-D7 — Group matching is label-only** where p2_spec §P2-4's sentence
  also names folder and tag (0bD-9).
- **0b-D8 — A `version` whose parsed value cannot be classified refuses
  the file**, on read and on write, where the mac decoded it and later
  overwrote it (0b-12).
- **0b-D9 — A self-link is not a neighbour** (0b-6b): the mac's diagram
  content listed the node's own label for a self-link where its tree
  dropped it; core drops it from both, and keeps it among the drawn
  edges.

### Accepted risks

- **0bR-1 — The Swift migration is unrun on this box.** The mac lane
  arbitrates; a red lane is fixed here, not waived.
- **0bR-2 — Node ids are scan-order indices.** The artifact never
  writes one (the schema walk proves it); the census would show a
  key-order difference if the lanes disagreed on anything but ids.
- **0bR-3 — Floats are out of the artifact.** `pagerank` and the
  layout are not 0b queries; the diameter is written as a rounded
  integer; the spatial section's inputs are integers and its outputs
  labels; the config's encoded text is core's own bytes.
- **0bR-4 — The leaf's snippets are decoration from a second payload.**
  A mutation between the tree and the bundle can leave a snippet stale or
  missing until the leaf's generation refresh re-pairs them; no
  structural claim depends on a snippet, and an unmatched path is
  ignored.

### §W-G register (seeded from spec §2; the close-out re-greps the Windows tree against it)

| # | Pocket | Status after 0b |
|---|---|---|
| A | the announcer grammar | moved (PR 0a, #1179) |
| B | the Connections tree | moved — `graph_connections_tree` with its counts; deleted `ConnectionsPanel.swift:375–499`'s derivation |
| C | the node identity | moved — `GraphNode.stable_key`, `graph_stable_key_for_path`; deleted `GraphViewState.swift:57–78` |
| D | visible neighbours and the content cap | moved — the topology entry's `neighbors` and the topology's `edges`, `graph_neighbors` (the cap 0a's); deleted `GraphDiagramView.swift:856–874`'s derivation, `:702–722`'s edge filter and `:1221–1228` |
| E | the name filter and the visible set | moved — `graph_visibility`, `graph_topology`, `graph_label_matches`; deleted `AppState+GraphConfig.swift:16–20`, `AppState+GraphTable.swift:228–238`'s predicate, `GraphDiagramView.swift:385–408, 640–648` |
| F | the config schema and policy | moved — `graph_config_decode/encode`, the group rules, the option vectors; the I/O host-designated; deleted `GraphConfigStore.swift:131–202`, the hand-written `allCases` in `GraphConfig.swift:109–142` and `GraphAnnouncer.swift:19–94` |
| G | the table columns | moved — `graph_table_rows`, `graph_table_columns`; deleted `GraphTableView.swift:300–315, 444–628`, `AppState+GraphTable.swift:418–420` |
| H | the constants | moved — `graph_constants`, `graph_node_diameter` |
| I | the action set | moved — `graph_row_actions`, `graph_row_action_title`; deleted `GraphViewState.swift:15–48`; runtime admission host (0bD-8) |
| J | the spatial and structural steps | moved — `graph_spatial_step`, `graph_structural_step`; deleted `GraphDiagramView.swift:1230–1262`; type-ahead host |
| K | the ghost's note path | moved — `graph_ghost_note_path`; deleted `AppState+Connections.swift:306–315` |
| L | viewport math | host-by-designation (the canvas viewport) |
| M | the hit-test grid and tiles | host-by-designation |
| N | the coalescing state machine | host timers; the class keys core's (0a) |
| O | the singleton tab lifecycle | host (W1-3) |
| P | driving the layout session | host driver over core's session |

### Tests that pin PR 0b (Task 0b-1)

`crates/slate-core/src/graph_queries.rs` (tests module): the tree facts
(five, the counts included), `stable_key_is_the_two_namespaces`,
`stable_key_percent_encodes_by_rust_alphanumerics`,
`label_order_is_natural_folded_and_total`, `filter_ids_fold_case_and_marks`,
`labeled_is_the_capped_priority_set`,
`topology_is_the_visible_nodes_with_their_neighbours`,
`topology_carries_the_visible_edges`,
`neighbors_are_unique_visible_and_ordered`,
`table_rows_sort_is_the_mac_comparator`, `table_cells_are_nine_and_formatted`,
`table_columns_are_the_ordered_specs`, `modified_text_is_utc_minutes`,
`graph_constants_are_the_named_constants`,
`node_diameter_is_the_spec_curve_clamped`, `tier_boundary_is_inclusive_at_1500`,
`row_actions_by_kind_are_the_parity_set`,
`spatial_step_prefers_neighbours_then_falls_back`,
`spatial_step_is_total_over_bad_input`, `structural_step_wraps`,
`ghost_note_path_honours_folder_and_extension`,
`graph_query_surface_names_pub_fns`. `crates/slate-core/src/session/tests/graph.rs`:
`session_results_carry_the_generation_they_read`.
`crates/slate-core/src/graph_config.rs` (tests module): the schema facts
(round-trip, the empty object, unknown keys, the two refusals, clamps,
first-match, the ring cycle), `verbosity_defaults_to_standard`,
`decode_truth_table`, `encode_is_canonical_bytes`,
`encode_preserves_unknown_keys_and_refuses_the_two_cases`,
`option_vectors_are_the_enum_arms`.
`crates/slate-uniffi/src/lib.rs`: `the_ffi_mirror_covers_every_graph_query_enum`,
`the_ffi_mirror_exports_every_graph_query`,
`the_ffi_records_mirror_core_field_for_field`.
`apps/slate-windows/tests/SlateWindows.Tests/Censuses/ParityHarnessCensus.cs`:
`graph_queries.json` byte for byte; GraphQueriesArtifactMatchesTheVaultInventory
(exact sorted lists); GraphQueriesArtifactCarriesThePinnedVectors;
GraphQueriesArtifactMatchesItsSchema (the parent rule included);
GraphQueriesArtifactCarriesNoNodeIds. `Censuses/GraphQuerySurfaceCensus.cs`:
the surface by reflection.

### Tests that pin PR 0b (Task 0b-2, the mac half)

`apps/slate-mac/Tests/SlateMacTests/ParityHarnessTests.swift`: the
`graph_queries` twin and the surface references. ConnectionsPanelTests:
the adapter's nesting by `parent_id`, the snippet overlay as
decoration, the busy reason, the leaf's authority (first load,
replacement). GraphTableViewTests: the column adapters over the specs,
the `stableKey` identity, the token lifecycle (delayed, reordered under
different needles, failed, stale generation, the preset after a Folder
sort). GraphConfigTests: the host I/O facts verbatim, the error mapping,
the writer's generation gate, the verbosity load, fallback and save,
the option vectors (palette, rings, modes, verbosities) against the enum
cases. GraphDiagramTests: the steps through core on the fixed layout,
the tier boundary through `graph_constants`, the topology token
(generation, backend filter), the call-count fact over a settling run
and each epoch component, the row copy and the actions from the entry,
the drawn edges from the record, the busy-state omission. GraphActionParityTests: the extensions over core's
vectors. GraphKeyCharacterizationTests: 0b-D1 executable.

## PR A — the graph document, the tab wiring, the table projection

**Goal (spec §PR A).** The placeholder the Windows shell shows for a
graph tab becomes the graph: one document per workspace behind the
existing singleton tab, loading the snapshot and the table rows
off-dispatcher under 0b's load token, and the TEXTUAL projection first
— the table on the W4-1 grid substrate with core's nine columns, core's
preset-free default sort, core's row actions, and a summary region
carrying core's `audio_summary` verbatim. Default landing is the Table
(P locked decision 1; §P-A). The Diagram (PR D), the presets, the filter
and the config (PR C) and the Connections leaf (PR B) are later slices;
this section pins what they will attach to. The section is at revision
5: round 1 returned nineteen findings (ten blockers) and revision 2
answered each at the line it named; round 2 discharged twelve and
returned sixteen more, two blockers — both in the tab-liveness
subsystem and both CREATED by revision 2's three-site hook, the
protocol's rule 5, so that round counts double, and with round 1's
lifecycle blockers that is three consecutive rounds of blockers in one
subsystem: rule 4's stop; revision 3 opened with the design pass for
that subsystem, a rule naming the funnel the shell already has. Round 3
discharged thirteen of the sixteen and returned seventeen more, six
blockers, four of them created by revision 3's own text — a false
premise about the open path (the graph opens into a NEW tab), a cause
consumed by the wrong funnel call, a reopen order neither host can
produce, and an Open rule that could not recover from an error — rule
5 for the second consecutive round. Revision 4 did what PR 0b's
revision 4 did: it restated rule L so that each class the round found
is caught by the rule's own terms — three liveness levels, a cause
scoped to its mutation and consumed only by the graph's own transition,
the mac's load guard as the load rule, one scheduling primitive that
never runs a receiver inside the mutation that started it — and
answered the eleven majors and minors beside it. Round 4 discharged
twenty-one of the twenty-six items it re-verified and returned nineteen
findings, six blockers — three created by revision 4's own text (an
apply allowed on the completing worker, the mac's guard mistranslated
for an Open across groups, an address activated at completion instead
of invocation) and three present in revision 3 and missed (a landed
create with silent exits, a reopen of a shown-and-loaded graph promised
a summary no load produces, the sort no-op guard transcribed by half) —
rule 5 for the third consecutive round. Revision 5 corrects the text
for every one of the nineteen and FREEZES: the standing precedent
(E13's, applied to PR 0a at its round 6 and to PR 0b at its round 4)
freezes a section whose round's blockers the previous fix created,
with that round's findings carried as a ledger the task loop discharges
by code — **precedent applied; the owner may overrule.** The section is
frozen at revision 5 on 2026-09-03; the four ledgers are below. Every
seam is cited at the line it sits on at `main` after PR 0b (#1180)
merged; a stale spec citation is corrected here rather than repeated
(0bD-12).

**What stands today.** The Windows shell already has the singleton's
LIFECYCLE and nothing of its surface: `OpenGraph()` opens
`WorkspaceItemKind.Graph` at the literal path `graph:singleton` with the
NEW-TAB target, always (`WorkspaceViewModel.cs:1946–1948`,
`WorkspaceOpenTarget.NewTab`; restore coerces every graph tab to that
token, `WorkspacePersistence.cs:377`); `TryOpenItem` collapses a second
open onto the one tab through `TryFocusGlobalGraph`
(`WorkspaceViewModel.Layout.cs:103–108, 178–192`), which assigns
`ActiveGroup = owner` FIRST and `owner.ActiveTab = graph` SECOND — two
assignments, each a funnel call when it CHANGES a reference (both
setters ignore a same-reference assignment, `Layout.cs:40–43`,
`WorkspaceViewModel.cs:1301–1304`), so a graph hidden in another group
costs two funnel calls and a graph that is already its group's active
tab costs one — and requests editor focus; otherwise
the new-tab arm adds the tab to the active group and activates it
(`AddTab(ActiveGroup, item, activate: true)`, `:128–131`). The graph
never takes the current-tab replace arm (`:141–172`) and never meets
the dirty gate (`:143–151`): the note the reader was editing keeps its
tab. `AddTab` attaches at `:211` and adds the tab to its group at `:217`
(activation `:218–221`); the split
refusal announces `GraphOpensSinglePane` (`:229–233`), the duplicate
refusal is silent (`:424–428`), the split command is disabled on a
graph tab (`:294–295`), and reopening a closed graph tab announces
`ReopenedGraph` — through `TryFocusGlobalGraph` when the tab still
exists (`:461–467`) and after `AddTab(…, activate: true)` when it does
not (`:471–479`); both events are shell-level `A11yEvent` arms
(`a11y.rs:601, 616`). The EFFECTIVE active tab is `ActiveGroup.ActiveTab`
and the shell has ONE funnel that follows it: `SyncPanels()`
(`WorkspaceViewModel.cs:1725–1749` — the bases dock, the panels, the
citations and the history each re-derive their note from
`ActiveGroup.ActiveTab` there), reached from the `ActiveGroup` setter
(`Layout.cs:35–51`, `:49`), from `Activate(group, tab)` (`:68–93`,
`:92` — "EVERY tab activation re-derives the panels' note") which the
group's `ActiveTab` setter calls on every change of reference
(`WorkspaceViewModel.cs:1298–1312`: same reference ignored, else
`Deactivate` then `_owner.Activate(this, value)`), from the end of every
OUTERMOST workspace mutation (`WorkspaceViewModel.Persistence.cs:163–169`
— "mutations can replace the active tab's item in place"), from the end
of `Restore` (`WorkspaceViewModel.cs:1615–1616`, after `RestoreActive`
seated the tabs without the setter, `:1314–1318`, and while `_restoring`
silenced `Activate`, `Layout.cs:70–73`), and from a rename (`:2003`).
The paths that change the effective tab all end there: the header click
(`WorkspaceTemplates.xaml:568`, a two-way binding to `ActiveTab`), the
cycle chords (`CycleTab`, `Layout.cs:509–518`), the close of a
neighbouring tab (`group.ActiveTab = successor`, `:322–326`), the pane
focus (`SelectGroupFromKeyboardFocus`, `:56–66`; `FocusPane`, `:520`),
the restore (`:1616`), and every `OpenGraph()` and `ReopenClosedTab`
(through `Activate`, twice for a graph in another group, and the
outermost-mutation hook) — round 3 traced every assignment and found
none that misses it. The tab renders the
placeholder: `IsPlaceholder` is true for anything that is not markdown,
base, saved query, dashboard or canvas (`WorkspaceViewModel.cs:323–324`),
`KindLabel` says "Graph" (`:330`), and `PlaceholderText` composes the
docked-surface sentence (`:334–335`) that `WorkspaceTemplates.xaml:539,
547` shows. `W1WorkspaceRedTeamTests` pins the dedup, the duplicate-and-
split refusals (`:101–128`) and the restore collapse
(`RestoreRejectsMoreThanSixGroups_AndDeduplicatesGraphGlobally`,
`:336–378`). Documents attach through ONE funnel,
`AttachTabDocumentsIfNeeded` (`WorkspaceViewModel.Bases.cs:1060–1079`),
whose doc-comment (`:1042–1059`) names its five call sites —
`TryOpenItem`'s in-place arm (`Layout.cs:161`), `AddTab` (`:211`),
`DuplicateActiveTab` (`:444`), `RestoreNode` (`Persistence.cs:83`),
`ReloadOpenTabFromDisk` (`History.cs:248`) — and
`CanvasAnnouncerCensus.TheAttachFunnelDocCommentNamesEveryCallSite`
(`CanvasAnnouncerCensus.cs:221–222`) fails when the comment and the
sites disagree. The funnel runs BEFORE the tab is live: `AddTab`
attaches before it adds; `RestoreNode` attaches before the group holds
the tab, and the workspace's root is assigned only when `Restore`
returns (`WorkspaceViewModel.cs:1615`); `Restore` runs OUTSIDE
`RunWorkspaceMutation`, under its own `_restoring` flag
(`Persistence.cs:17, 21–26`), and the one funnel call that follows it
is the constructor's `SyncPanels()` (`:1616`). The canvas registry starts its
document's `Load()` inside the attach (`WorkspaceViewModel.Canvas.cs:150–152`),
which a canvas can afford (it announces nothing on open) and the graph
cannot (rule L). The canvas is otherwise the per-tab template: the
tab's `Canvas` property and `AttachCanvasDocument`
(`WorkspaceViewModel.cs:296–309`; the spec's `:289–300` drifted), the
registry `CanvasDocumentFor` over `_canvasDocuments`
(`WorkspaceViewModel.Canvas.cs:23–24, 41–43`), the release sweep that
shuts a retired document down and tracks its drain through
`TrackRetiredBasesWork` (`:293–301`), the teardown
`ShutdownCanvasDocuments(drains)` (`:384–392`) that `Dispose` calls in
the one bounded pre-session drain (`WorkspaceViewModel.cs:2184–2186`)
before the vault lifecycle disposes the workspace and then the session
(`VaultLifecycleViewModel.cs:928–936`); the canvas's publication is ONE
immutable record installed through a compare-exchange slot
(`Canvas/CanvasPublicationSlot.cs:220–222` the one read, `:235–254` the
publish under a gate that refuses reentry, `:270–282` the
compare-exchange that installs the successor). The grid substrate is
`AccessibleDataGrid`: `Bind(columns, rows, summary, accessibilityLabel,
rowAudioDescription, rowActions, exportProducer, rowActivated)`
(`Grids/AccessibleDataGrid.cs:429–437`), the `Announce` seam a funnel
swaps (`:64`; the default posts through
`AccessibilityNotificationDispatcher`, `:173`), `ExternalSortHandler`
(`:652–663`: the handler dispatches, rows arrive by a later `Bind`, the
grid neither reorders nor announces — "the surface announces its own
canonical event when the rows actually land"), `SetSortIndicator`
(`:665–680`, silent), `ApplySort` delegating an externally-sortable
column (`:687–698`) and posting `GridSorted(header, ascending)` for a
local one (`:759`), `GridAutomationId` and its `…Summary` twin
(`:408–419`), the summary region named `Summary: {summary}` and the grid
named by the label (`:523–525`), `AccessibleGridColumn` with `Header`,
`Cell`, `Sort`, `AccessibilityHint`, `IsRowHeader`,
`IsExternallySortable` (`:1454–1476`), `AccessibleGridRowAction` with
`Name`, `Execute`, `IsVisible`, `IsEnabled`, `DisabledReason`
(`:1481–1491`) — ONE list per grid, filtered per current row by
`IsVisible` when the menu builds (`:1312–1335`) — the row header seated
per realized row (`:203–209, 512–515`), the cell name `{Header}: {cell}`
(`:1441–1444`), `rowAudioDescription` consumed ONLY by the row-move
announcement `GridRowMoved(description, focusedCell)` (`:466, 840–846`;
no row receives an automation name), activation by Enter and
double-click with no modifier arm (`:906–914, 1162`), `Bind`'s
reader-position restore keyed on the row-header text with the previous
ordinal breaking ties, run silently while `CurrentRowChanged` still
fires (`:471–487, 527–528, 822–829`), and STANDARD virtualisation —
containers are created and discarded, never recycled — with only the
`LoadingRow` handler subscribed (`:117, 128`). It has no item-status
seam and no row-name seam; a realized `DataGridRow`'s automation peer
reads `AutomationProperties.Name` when it is set (WPF's item peer
delegates to the wrapper's). The bases table is the externally-sorted,
core-column-driven consumer to copy: `GridAutomationId = "BaseTabGrid"`,
`ExternalSortHandler = OnExternalSort` (`Bases/BaseSurfaceView.cs:164–168`),
columns built in a loop over the result's column vector with
`IsRowHeader = column.Role == ColumnRole.Primary` and
`IsExternallySortable = true` (`:648–669`), the row-action literal
(`:690–713`), `Bind` then `SetSortIndicator` (`:714–728`); the canvas
table is the funnel-swap and the selection-guard precedent
(`Canvas/CanvasTableView.cs:186–200`: `Announce = model.GridRelaySeam`
once the document is attached, `_ => { }` before; `:240–278`: the bind
and the re-seat under `_syncingSelection` because the substrate's
silent restore still raises `CurrentRowChanged`). Off-dispatcher work
rides `PanelWorkScheduler` (`Panels/PanelWorkScheduler.cs:16`:
`StartWork` `:76–94` — inline when synchronous `:82–85`, else a tracked
`Task.Run` through the PRIVATE `TrackWork` `:157–172` — `RunIfLive`
`:103–110`, `RunGatedAsync` `:121–155` ("ALWAYS hand the body to the
pool, never run it inline"), `Post` `:190–203` inline when synchronous
OR when no UI context was captured, `DrainForTests` and
`WhenWorkDrained` `:174–188` waiting for the TRACKED tasks only — a
callback `Post` queued to a context is not among them — `Shutdown`
`:59`; its doc `:9–15` says subclasses guard their own publishes with
generation or request tokens), and `AccessibilityNotificationDispatcher.Post` renders
and raises (`:22–26, 36–51`). The vault-event listener is
`UiVaultEventListener` (`VaultLifecycleViewModel.cs:1703–1723`), built
at `:435–439` with three delegates — the error and file-change arms
marshalled through `_enqueueUi` with the lifecycle generation, and the
INDEX-PHASE arm discarded (`(_, _) => { }`, `:439`); `HandleFileChange`
(`:713–778`) is generation-guarded and fans out to paths, reading, bases
(`NotifyBasesOfVaultChange`, `:753, 764`), history (`:759`) and the
sidebar under a 150 ms ticket (`:767–776`). Core's listener contract
(`session.rs:690–709`): `on_file_change` covers Slate's OWN writes —
the watcher is a stub, so an external edit surfaces at the next scan
(`on_index_phase` with `IndexPhase::ScanFinished`), and neither
callback may call a session API synchronously (it may arrive with
session locks held); `graph_generation` (`:1850–1854`) says the refresh
contract re-queries it on file-change AND scan-finished and refreshes
only on change. The canvas announcer is the relay-and-coalescer
template (`Canvas/CanvasAnnouncer.cs:28, 35, 45`: `Announce` `:89`,
`Relay` `:102`, `Emit` `:154`, `CoalescingClassOf` `:240`, `Debounce`
`:253`, `Fire` `:263`, the High flush-and-drop `:294, 303`), and two
censuses wall the `Canvas/` directory:
`AnnouncementSeamCensus.NoCanvasCodeReachesTheAnnouncerExceptThroughTheBoundary`
(`AnnouncementSeamCensus.cs:353`, sources `:526–534`, the named seams
each hit `:518–524`, the wall-is-the-directory caveat `:345–351`) and
`CanvasAnnouncerCensus` (`:21`; `NoCanvasSourceAnnouncesOutsideTheRelay`
`:38`, `EveryGridUnderCanvasRidesTheRelay` `:111`,
`TheRelayIsTheOneFileThatRenders` `:207`; it enumerates the directory
recursively, `:457`, and exempts the relay by BASENAME, `:43`). The
chord table's row shape is `ChordTableEntry`
(`Commands/ChordTable.cs:77–127`), `ChordScope` ends at `Canvas`
(`:21–70`; there is no `Graph` member), `CommandSection.Graph` exists in
the binding (`slate_uniffi.cs:30838`), the chordless-row template is
`Reg(Ids.CanvasShowOutline, …, CommandSection.Canvas, …)` (`:922–923`,
rationale `:906–919`), and `Reg` forces `Scope = ChordScope.None` when
the Windows chord is null (`:578`); `chords.json` is regenerated by the
env-gated test (`ChordTableTests.cs:31`, the SLATE_CHORDS_UPDATE
variable) whose projection preserves `deliveryEvidence`
(`ChordTable.cs:481–489`; `chords.json:2148`), validated by
`generate-parity-matrix.py:802–809`. The benchmarks project selects
suites by flag (`Program.cs:12–17`, `--canvas`, `--validate-budgets`);
its `--canvas` arm RUNS `CanvasOpenBenchmarks` and discards that
summary (`:27`), then gates only the renderer suite's reports against
a name-keyed budget table, failing on a missing median or an unlisted
report, with a `PASS`/`MISS` line per report and a non-zero exit
(`:28–57`; the class shape `CanvasRendererBenchmarks.cs:17–21`); the canvas's §K fact runs in BOTH
scheduling modes (`CanvasDocumentTests.cs:4813–4852`, the W4-5 lesson
the protocol records: "test the mode users run",
`24_red_team_protocol.md:71–73`), and the canvas's presentation-engine
tests run their asynchronous engine under an INSTALLED
DispatcherSynchronizationContext pumped by DispatcherFrames
(`CanvasPresentationEngineTests.WithPumpedContext`, `:300–316`;
`PumpUntil` and `DrainDispatcher`, `:318–335`) — the harness for a
document whose applies must land on a dispatcher; the scheduler's own
doc says a null or test context publishes on an arbitrary worker and
races the constructing thread (`PanelWorkScheduler.cs:39`); `w_c_matrix.md` rows are ten columns
(`:5–6`; the canvas table row `:42` is the template) pinned by
`WcMatrixCanvasEvidenceCensus`. The sidebar's "Reveal" is "Reveal in
File Explorer" over ITS selected node
(`FilesSidebarViewModel.FileManagement.cs:1081–1107`, FD-5), not a
select-this-path verb; its untitled-note create runs
`CreateOutcomes.CreateReporting(session, path, content, displayName)`
(`CreateOutcomes.cs:33–45`: core's typed `CreateExclusiveReporting`,
a caveat for the landed-but-unindexed arm, `DestinationExists` thrown
through) under a session-work lease, then the structural-history
barrier, the tree refresh and the open (`FilesSidebarViewModel.cs:1219–1262`);
the canvas already consumes that path through a TWO-PHASE seam —
`ICanvasNoteCreator.TryCreateNote(path, content)` returning
`CanvasNoteCreateResult.{Landed(caveat), Exists, Failed(message),
Unavailable}` on the worker (`Exists` carries no message, `:35`;
`Unavailable` is the session-work refusal at shutdown, `:41`,
`FilesSidebarViewModel.CanvasNotes.cs:24–31`) and `NoteLanded(path,
caveat)` on the dispatcher (`Canvas/CanvasNoteCreation.cs:13–28`;
`FilesSidebarViewModel.CanvasNotes.cs:16–59`;
`CanvasDocumentViewModel.cs:3880, 3912`). `StructuralMutationBusyReason`
is declared and never emitted — Windows has no structural-mutation gate
(`Commands/SlateCommandRegistrar.cs:176–196`). `OpenPath(path, target)`
runs `OpenPathCore` under a workspace mutation and announces NOTHING
(`WorkspaceViewModel.cs:1869–1870, 1928`); the bases add `OpenedFile`
after it themselves (`BasesOpenRow`, `WorkspaceViewModel.Bases.cs:473–481`).
The parity harness pins the artifact's `all` query as the INCLUSIVE
filter — attachments and ghosts in, orphans-only off
(`tools/ParityHarness/SurfaceSerializer.cs:1191, 1196`) — not core's
default; the harness writes the table section at `:1430–1462`, and
the mac twin serializer is `ParityHarnessTests.swift`'s
`graphQueriesArtifact` (`:305–308`, "key for key and in the same
order"). The binding already carries everything this PR consumes:
`GraphGeneration()`, `GraphSnapshot(filter)`, `GraphTableRows(query,
sort)` on the session (`lib.rs:1323, 1301, 1379–1383`); the free
`GraphTableColumns()`, `GraphRowActions(kind)`, `GraphSurfaceModes()`,
`GraphStableKeyForPath(path)` (`lib.rs:3922, 3794, 4195, 3596`); the
records `GraphSnapshot` (`nodes`, `edges`, `generation`,
`audio_summary`, `summary_counts` — NO filter, `graph.rs:319–330`),
`GraphTableRow` (`stable_key`, `node_id`, `label`, `path`, `kind`, nine
`cells`, the five counts, `component`, `modified_ms`), `GraphTableRows`
(`generation`, `total`, `rows` — nothing else, `graph_queries.rs:620–626`),
`GraphTableSort`, `GraphVisibilityQuery`, `GraphTableColumnSpec`,
`GraphRowActionSpec`, `GraphSurfaceModeSpec` (`:565–626`; the FFI
mirrors), and the 0a family under `A11yEvent::Graph`
(`a11y.rs:1271–1273`): `GraphRow{verbosity, row}` over `GraphRowCopy`
(`:3013–3023`), `GraphSnapshotSummary{counts}`, `GraphFilterCount`,
`GraphMode{mode}`, `GraphStatus{note}` with `GraphStatusNote::{Opened,
AlreadyOpen, ConnectionsPanel, NoteCreated, NoConnections,
LoadingConnections}` (`:3086–3093`), `GraphBlocked{reason}` with
`GraphBlockedReason::{LoadFailed, ConnectionsLoadFailed,
NoteCreateFailed}` (`:3098–3103`), `GraphBlocked` High and the rest
Medium (`:3234–3241`), and the four coalescing classes `navigation` =
`GraphRow`, `filter` = `GraphFilterCount`, `forceValue`, `settle`,
everything else immediate, a High graph event flushing and dropping all
four (`:138–152`). The mac's table half, the parity target, is
`AppState+GraphTable.swift`: `openGraphTab` (`:45–73`) finds-or-opens
the singleton, activates it (`:60–70`, which runs `activateGraphTab`
and so INVOKES the load) and then announces `GraphStatus{Opened}`
unconditionally (`:72`) — the load is asynchronous, so the audible
order is Opened, then the summary; `activateGraphTab` (`:80–118`)
returns at its guard ONLY when the tab is already the active group's
active tab AND a snapshot is held (`:81–83` — "same-tab no-op"; a
same-tab call with no snapshot falls through and loads), else parks,
selects the tab (`:93`), loads the config, applies the persisted filter
on a fresh open and calls `loadGraphTable` (`:105–109`) — so switching
TO the graph tab reloads and speaks the summary, a pane focus onto a
group whose active tab is a loaded graph does not (`focusPane`
`AppState.swift:4350–4363`: `focusGroup` first, then
`enterEditorGroup` → `selectTab` of the group's active tab
`:4388–4393`, which reaches the guard with the tab already active), and
`restoreWorkspaceLayout` calls `activateTab` for the restored active
tab (`:4177–4179`), so a restored active graph tab speaks its summary
at launch; `reopenClosedTab` calls `openGraphTab()` and IMMEDIATELY
posts `reopenedGraph` (`:3612–3614`) while the summary arrives from the
detached load, so a reopened graph tab speaks Opened, the reopen line,
then the summary; `AlreadyOpen` is posted by Duplicate Tab (`newTab()`,
`:3266–3269`), not by `openGraphTab`; the refresh probe runs only while
a graph tab is the active tab of SOME group (`anyGraphTabVisible`,
`AppState.swift:3037–3047` — "currently rendered somewhere, not merely
open in a background position"), speech only while it is the active
group's (`graphTabActive`, `:3031–3035`), and a probe that finds a
changed generation issues a load that SUPERSEDES an in-flight one
through the load sequence (`refreshGraphTableIfGraphChanged`,
`AppState+GraphTable.swift:477–503`); `releaseGraphStateIfUnreferenced`
resets every field on the last close and cancels the announcer's
pending speech (`:131–150`, the default sort literal `:135`, the seen
generation `:149`, `cancelPending` `:150`); `loadGraphTable`
(`:169–262`) fetches the snapshot AND the rows in one detached task
(`:189–200`), re-checks session and load sequence (`:206–208`), decides
whether to speak AFTER the fetch (`:215`), INSTALLS the candidate
snapshot and its filter (`:219–222`) and only then publishes the rows
by token (`:226`), lets a pending preset supersede the summary
(`:235–243`; the spec's PR C citation `:195–199` drifted), announces
`GraphSnapshotSummary` (`:245–248`) or the filter count (`:249–253`),
and on failure sets the error, NILS the snapshot, fails the token and
announces `GraphBlocked{LoadFailed}` (`:257–265`) — the row buffer
survives, hidden under the error, and a retry shows the initial loading
state; the token API is `issueGraphTableToken` (`:274–281`),
`receiveGraphTableRows` (`:291–310`: the four-step publish rule),
`failGraphTableRows` (`:313–316`), `requestGraphTableRows` (`:321–356`:
a SORT fetches ROWS ONLY, `:332`; its FAILURE arm rolls the requested
sort back and announces `GraphBlocked{LoadFailed}` while the published
snapshot and rows stand, `:348–353`) and `setGraphTableSort`
(`:359–362`); `revalidateGraphSelection(against:)` drops the shared key
when the SNAPSHOT's nodes no longer carry it (`:370–375`).
`GraphTableView.swift` carries its `tabID` (`:208`), binds the
ACCEPTED sort to the grid and writes a REQUEST (`:213–226`), binds the
selection straight to `graphSelectedNodeKey` (`:239–244`;
`AppState.swift:3108`), switches loading → error → empty → grid
(`:246–260`; "Loading graph…" `:300–307`; the error view shows the raw
message and carries "Graph error: {message}" as its ACCESSIBILITY label
`:309–316`; "No notes match the current filters." `:257`), filters
nothing itself (`:320`), binds the grid with the summary, the label
"Graph, data grid", the local-sort-off flag, activation and MODIFIED
activation, the row actions and the relay — and NO row description: the
mac grid's rows carry no P1 row label, only their cells (`:322–341`;
the grid's row-description default is nil, `AccessibleDataGrid.swift:208–221`),
and activates through one gate (`:343–375`): `focusOwningGroup` first —
the tab's own group is made active so `.currentTab` and `.newTab`
resolve in THIS pane (`:372–375`) — then plain activation opens in the
current tab through `openFile`, which posts NO graph event (`:363`),
⌘Return / ⌘-double-click in a new tab (`:347–352`), and a GHOST
activates Create note under the structural-mutation admission
(`:359–361`, `createNoteFromGhost(targetRaw:)`,
`AppState+Connections.swift:256–310`: admit, `createExclusive` with
EMPTY content off the main actor `:271`, the structural refresh, the
undo-stack barrier, `openFile(path, target: .currentTab)` `:294`, the
created entry's inline RENAME `:295–296`, then ONE
`GraphStatus{NoteCreated{name}}` `:297–298` — after the open attempt,
unconditionally; the failure arm `GraphBlocked{NoteCreateFailed}`
`:302–305`); it builds every column from `graphTableColumns()`
(`:451–478`; no column is listed in Swift). The mac grid defers an
ENGINE-BACKED sort's `GridSorted` until the reload that adopts the
owner's committed rows, and drops it if the owner reverts
(`AccessibleDataGrid.swift:426–435, 630–642`).
`GraphViewState.swift:15–54` is the action sugar over
`graphRowActions(kind:)`, fetched ONCE per kind (`:18–19`);
`GraphAnnouncer.swift` is the relay-and-coalescer (`:101, 113, 128,
140–161, 165, 175, 225`). `GraphTabRoutingTests.swift` pins the tab
lifecycle (`testOpenGraphTabRoutesAndDedups` `:48`,
`testGraphTabLoadsSnapshot` `:63`,
`testGraphTabSurvivesSerializationRoundTrip` `:73`,
`testGraphTabIsHardSingleton` `:88`,
`testRestoreCollapsesDuplicateGraphTabs` `:113`,
`testOpenGraphTabParksDirtyOutgoingNote` `:134`,
`testGraphTabNotAddressableByPath` `:158`,
`testRefreshProbeGatedOutWhenLifecycleAdvanced` `:391`,
`testRefreshGateClosesWithLastGraphTab` `:436`; the preset, filter and
connections cases are PR B's and C's). Core's table half:
`GraphTableColumn::ALL` and `header()` (`graph_queries.rs:527–551`),
`table_columns` (`:572–580`), `GraphTableSort::default()` = links in,
descending (`:589–596`), `kind_label` (`:629–635`), `modified_text`
UTC (`:649–656`), `row_of` (`:658–683`), `compare_rows` with the
label tie-break always ascending (`:690–742`), `table_rows`
(`:745–758`), `GraphRowAction::ALL`, `row_action_title`, `row_actions`
(`:765–821`: four navigation actions for a note or attachment,
`CreateNote` alone for a ghost). P1 says the snapshot is fetched once
per generation and the grid renders virtualised, with a 10k-row test
that instruments row instantiation (`p1_spec.md:67, 73`); spec §1 says
the view state is `{ SelectedKey, Filter, NameQuery, Groups, Mode }`
and NOTHING else (`w6_2_graph_spec.md:30`). The 0b artifact's `table`
section carries sixteen entries — the `all` query under eight columns ×
two directions, every column but Modified — of twelve rows each keyed
by `stable_key`, and no summary text (`parity_golden/graph_queries.json`).

### Design pass (protocol rule 4, reached through rule 5 twice) — subsystem L, the graph tab's liveness, modelled as a rule

Round 1 found the attach funnel running before the tab was live and
the open sequence inverted under synchronous scheduling (IGA-3);
revision 2 answered with a hook at three named sites; round 2 found
activation paths outside those sites (IGA-20) and a reopen rule that
forbade its only hooks (IGA-21); revision 3 named the funnel and a
cause field; round 3 found the funnel right and the rule's TERMS wrong
— the open path misdescribed (IGA-36), the cause consumed by a funnel
call that saw another tab (IGA-37), a reopen order neither host makes
(IGA-38), an Open that could not restart a failed load (IGA-39), a
probe that could lose a generation (IGA-40), the row actions unaddressed
to their pane (IGA-41), a liveness level the mac has and the rule
lacked (IGA-44), a pane focus the mac does not reload (IGA-45). Each is
a term of the rule restated below, written so that its next instance
is decided by the term and not by a site list.

- **Term 1 — the funnel.** The effective active tab is
  `ActiveGroup.ActiveTab`, and every path that changes it ends in
  `SyncPanels()` (`WorkspaceViewModel.cs:1725–1749`; the setter, `Activate`,
  the outermost-mutation hook, the end of `Restore`, the rename — the
  citations above, traced assignment by assignment in round 3). The
  graph adds ONE line there, GraphFollowActiveTab(), and NOTHING else in
  the shell starts a graph load — a census asserts the document's load
  entry points have that one caller outside the document. The attach
  funnel seats and never starts.
- **Term 2 — three liveness levels, the mac's.** LIVE: a graph tab
  exists in any group — the document exists and a publication may
  install. VISIBLE: the graph tab is its group's active tab, in ANY
  group — the probe runs (the mac's `anyGraphTabVisible`,
  `AppState.swift:3037–3047`). EFFECTIVE: visible and its group is the
  active group — announcements are spoken (the mac's `graphTabActive`,
  `:3031–3035`). Each gate names its level: the probe VISIBLE, the
  receiver's speech decision EFFECTIVE at dispatch, the publication
  LIVE. A hidden graph (open behind another tab) is not probed — the
  next activation loads it (Term 4).
- **Term 3 — the follow method classifies, and acts on transitions
  only.** It computes the graph's level from `ActiveGroup.ActiveTab` and
  the groups, compares with the classification it cached on its
  previous call (the level and the tab OBJECT, so a tab replaced in
  place is a change), stores the new one, and acts only when the graph
  BECAME effective. Two kinds of transition are distinguished by what
  changed: BY TAB — the graph was not visible and is now effective (its
  group's active tab changed to it, or a new graph tab was added
  active); BY GROUP — the graph was already visible and its group
  became the active group (the `ActiveGroup` setter's funnel call,
  `Layout.cs:49`, with `ActiveTab` unchanged).
- **Term 4 — the load rule is the mac's guard.** The mac loads on every
  `activateGraphTab` except when the tab is already the active group's
  active tab AND a snapshot is held (`AppState+GraphTable.swift:81–83`).
  In Term 3's words, and aware of the cause and of the level BEFORE the
  mutation: a transition BY TAB always loads (a pair with the summary);
  a transition BY GROUP under Activation loads only when no READY
  publication is held (the pane-focus case, `AppState.swift:4350–4363,
  4388–4393`); an explicit Open or Reopen loads whenever the graph's
  level before the mutation was not EFFECTIVE — VISIBLE in another group
  included — because the mac's guard runs before the group changes
  (`openGraphTab` activates the existing tab while another group's tab
  is still the active one, `:60–70`, so `:81` fails and it loads); an
  explicit Open or Reopen with the graph ALREADY effective loads only
  when no READY publication is held (the same guard; LOADING and ERROR
  hold none, so an Open after a failure restarts the pair, and a
  shown-and-loaded graph reloads nothing). READY is A-4's: a
  publication with a snapshot; a pair in flight over a held snapshot
  keeps it, a first or retry pair holds none, a retired document is not
  LIVE. The restore's funnel call is a transition BY TAB (the restored
  document holds nothing). A load started by this rule is a PAIR with
  the summary policy of the cause; the probe's loads are silent (A-3).
- **Term 5 — the cause is scoped to its mutation and consumed only by
  the graph's own transition.** The workspace holds a pending cause —
  Open, set by `OpenGraph()` before its mutation; Reopen, set by
  `ReopenClosedTab` before its mutation, for a graph record. Restore
  sets NO cause: its policy is Activation's (the summary alone), and
  `Restore` runs outside any mutation with its one funnel call after
  `_restoring` clears (`Persistence.cs:21–26`, `WorkspaceViewModel.cs:1615–1616`),
  so a stored cause there would have no boundary to clear it. A funnel
  call that finds the graph NOT effective leaves the cause pending (for
  a graph hidden in another group, the first of `TryFocusGlobalGraph`'s
  assignments makes that group active with its own active tab,
  `Layout.cs:188`, and the graph becomes effective at the second,
  `:189`; for a graph that is already its group's active tab, the first
  assignment is the transition and the second is a no-op). The funnel call that finds the graph's
  transition, or an explicit Open with the graph already effective,
  consumes the cause. The outermost-mutation hook
  (`WorkspaceViewModel.Persistence.cs:163–169`, the last funnel call of
  the mutation) clears an unconsumed cause, so a cause never leaks into
  the next path; a path outside any mutation (the header click's
  binding, the cycle chord) never sets one and reads Activation.
- **Term 6 — the announcement policy per cause, the mac's.** Open:
  `GraphStatus{Opened}` always (`openGraphTab`, `:72`), then the pair
  when Term 4 says so. Activation — the restore's funnel call included — no status; the
  summary when the pair publishes. Reopen: `Opened`, then the shell's
  `ReopenedGraph` at its existing sites — immediately after the
  activation, `Layout.cs:463` and `:475` — then the summary when the
  pair publishes (the mac's order, `AppState.swift:3612–3614`: the
  reopen line is posted synchronously, the summary by the detached
  load); a reopen of a graph tab that still exists (`:461–467`) is an
  Open of that tab with the reopen line after it — and when that tab
  was effective and READY, Term 4 loads nothing and the sequence is
  `Opened`, `ReopenedGraph`, no summary (the mac's `:81` guard under
  `openGraphTab`, then `reopenedGraph`). The status is posted before
  the load starts and the summary is posted by the receiver, which
  never runs inside the mutation that started the load (Term 7), so
  `Opened` precedes the reopen line precedes the summary wherever a
  load runs; a failed pair puts `GraphBlocked{LoadFailed}` where the
  summary would be. Every sequence in this section is a PROJECTION onto
  the graph family plus `ReopenedGraph`: the shell's own `TabFocused`
  (`Layout.cs:79–83`), `TabClosed` (`:326`) and pane announcement
  (`:64`) are posted where the shell posts them today and are outside
  the assertion.
- **Term 7 — one scheduling primitive, never inline.** Every graph body
  — pair, rows only, probe, create — runs through
  StartWorkAlwaysAsync(compute, apply) (A-2): compute on the pool,
  apply ALWAYS on the serialized owner context — the WPF dispatcher in
  production, an installed and pumped DispatcherSynchronizationContext
  in every test — never on the completing worker, the tracked task
  completing after the apply. The receiver therefore never runs inside,
  or races, the mutation that started it, and the order of Term 6 is a
  property of the design, not of a mode.
- **The facts enumerate the paths; the census closes the class.** One
  fact per path, each asserting the graph-family announcement sequence
  (Term 6's projection) and the LOAD count under the pumped dispatcher;
  the funnel-call count is state-dependent and is not asserted (round
  4's census: a fresh Open reaches `SyncPanels` twice, an Open of the
  effective tab once, an Open of a graph hidden in another group three
  times, a header click once, a cycle chord twice, a close of the
  neighbour twice, a pane focus once, the restore once — the cached
  classification of Term 3 makes the redundant calls no-ops):
  `OpenGraph()` with no graph tab (the new-tab arm); `OpenGraph()` with the graph effective and READY (Opened
  alone, no load); `OpenGraph()` with the graph effective in ERROR
  (Opened, the pair restarts); `OpenGraph()` with the graph hidden in
  another group (the cause survives the funnel calls that see another
  tab; one load); `OpenGraph()` with the graph visible in another group
  (Open from a level below EFFECTIVE: the mac's guard reloads because
  its active tab was another group's — Opened, the pair); the header click; both cycle
  chords; the close of the neighbour that makes the graph current; the
  pane focus onto a group whose active tab is a READY graph (no load,
  no status); the same onto a graph in ERROR (the pair); the restore
  with the graph effective (the summary); the restore with the graph in
  a non-active group and its later activation; both reopen arms
  (Opened, the reopen line, the summary; the failure arm; and the
  existing-tab arm with the tab effective and READY — Opened, the
  reopen line, no load); an Open followed by an Activation (no leak).
  And the census: no workspace source calls the document's
  load outside the follow method.

### Design — three rules, carried from PR 0b's pass

PR 0b's design pass (rules A, B, C above) was written for the mac
consumer and the harness; PR A is the first WINDOWS consumer, and each
rule has a Windows instance this section derives its contracts from.

- **Rule A on Windows — one authority per consumer, one token per
  request, one publication; a replacement is validated against ITSELF
  through its envelope.** The document is the table's only consumer in
  this PR. Its authority is the held publication's snapshot with the
  filter it was fetched under and its generation. A request is either
  a PAIR (snapshot + rows, for the follow method's transitions, a
  backend-filter change, a changed generation) or ROWS ONLY (a sort).
  The worker returns an ENVELOPE — the filter it passed to
  `GraphSnapshot`, the query and sort it passed to `GraphTableRows`,
  and the two results — because neither result carries its inputs
  (`GraphSnapshot` has no filter, `GraphTableRows` has generation,
  total and rows only). A pair is validated from its envelope — the
  envelope's filter equals the envelope's query filter equals the
  current request's, the envelope's query and sort equal the request's, and the rows'
  generation equals the snapshot's — and then REPLACES the authority
  and the rows as one immutable publication record installed in one
  property swap; a rows-only result is validated against the HELD
  publication (filter and generation) and against the request (query
  and sort) and installs a new record carrying the held snapshot with
  the new rows and accepted sort. The token is (document instance,
  session identity, lifecycle generation, the full request record,
  `seq`); a stale, mismatched, out-of-order result installs nothing; a
  pair failure installs an ERROR record without a snapshot; a rows-only
  failure installs nothing and rolls the requested sort back; a
  rows-only generation mismatch issues a silent pair.
- **Rule B on Windows — one crossing per semantic epoch, every ordered
  inventory a core vector, fetched once.** A pair is two crossings for
  one epoch and a sort is one; the columns and the mode-switcher items
  are core's vectors iterated in order; the row actions are core's
  three per-kind vectors fetched ONCE per document and unioned in core's
  order, never per row; the default sort is fetched, not typed (AD-1);
  a census reads the shell for a typed column header and fails on one;
  a call-count fact proves the crossings do not scale with the rows
  (A-5, A-8, A-11).
- **Rule C on Windows — evidence is core's rows, walked, in the mode
  users run.** The document's published rows over the graph vault are
  compared to the 0b artifact's table section cell by cell for all
  sixteen sorts under the artifact's own filter, and the summary to the
  artifact's (A-14); the load, the reorder, the close-during-fetch, the
  generation retry and the teardown are pinned in BOTH scheduling modes
  (A-2); the announcements go through one relay walled by the directory
  censuses (A-10); the FlaUI journey walks the grid by UIA and asserts
  the row names verbatim (A-16); the 10k grid is bound to the real
  substrate and its live containers counted (A-15). Nothing in this PR
  is asserted about; it is asserted on.

### Contracts

**A-1 — One document per workspace: seated by the funnel, started by
the follow method, retired with the last graph tab, drained at
teardown.** A `Graph/` directory under the Windows shell holds
GraphDocumentViewModel (a `PanelWorkScheduler` subclass, the canvas's
base), GraphViewState, GraphPublication (the immutable record of rule
A), GraphSurfaceView (XAML + code-behind), GraphTableView and
GraphAnnouncer — new files; the directory is the wall A-10's censuses
read. The workspace holds AT MOST ONE document in a nullable field
beside `_canvasDocuments` (`WorkspaceViewModel.Canvas.cs:23–24`), keyed
by nothing — the singleton IS the key (`graph:singleton`,
`WorkspaceViewModel.cs:1946`). The view state is spec §1's record and
nothing else: `SelectedKey: string?` (null), `Filter: GraphFilter`
(core's default), `NameQuery: string` (empty), `Groups` (the config's
groups, empty until PR C reads them), `Mode: GraphSurfaceMode` (Table)
— five fields with those defaults, owned by the document, with a
census that no other type under `Graph/` declares a MUTABLE field of
those five names or a second mutable copy of the filter, the query or
the mode; the immutable request record, token, envelope and
publication of A-2 carry copies by design. **Amended by the owner on
2026-09-06 (W6-2 PR B2, B2D-1):** the view state is owned by the
WORKSPACE — one instance, constructed in the workspace's constructor
beside the relay, handed to the document at construction and to the
Connections leaf, surviving the document's retirement (which no longer
resets it) and dropped with the workspace; the census's wall widens to
the workspace's graph partials and an instance census counts the one
construction (§PR B2, B2-1). Attachment SEATS
and never starts: every tab of kind Graph attaches through
`AttachTabDocumentsIfNeeded` (`WorkspaceViewModel.Bases.cs:1060–1079`)
by a new arm beside the canvas's that creates the document on the first
attach and gives the tab a `Graph` property with the canvas's shape
(`WorkspaceViewModel.cs:296–309`), so the five sites its doc-comment
names — and `TheAttachFunnelDocCommentNamesEveryCallSite` — cover the
graph without a sixth site, and so the attach can run before the tab
is in its group without a load running against a tab that is not yet
live. The LOAD starts from rule L's follow method in `SyncPanels`, under
Terms 3 to 6, and from nowhere else (the census); the document is
created with NO publication, so its first transition loads. Retirement: when the last graph tab in the
workspace closes (the tab-set boundary the canvas sweep uses,
`WorkspaceViewModel.Canvas.cs:286–301`), the workspace calls
RetireGraphDocument: the document's `seq` advances (an in-flight result
can no longer match), `Shutdown()` (the scheduler's,
`PanelWorkScheduler.cs:59`) stops new work, the announcer's Shutdown
drops its pending classes (the mac's `cancelPending`, `:150`), the
publication is retired (a retired record refuses every later apply, the
canvas's `Retired`, `CanvasDocumentViewModel.cs:1425`), the view state
resets to its defaults, the field is nulled, and the document's
`WhenWorkDrained()` is tracked through `TrackRetiredBasesWork` so a
blocked worker is waited for at teardown; the next open creates a
FRESH document — the "refresh gate closes with the last graph tab"
twin (`testRefreshGateClosesWithLastGraphTab`). Teardown: `Dispose`
gains ShutdownGraphDocument(drains) beside `ShutdownCanvasDocuments`
(`WorkspaceViewModel.cs:2184–2186`), retiring a live document into the
same bounded pre-session drain, so no graph worker outlives the
session (`VaultLifecycleViewModel.cs:928–936`). The placeholder retires
for Graph: `IsPlaceholder` (`WorkspaceViewModel.cs:323–324`) gains
`&& !IsGraph`, the template renders GraphSurfaceView for a graph tab,
and a fact asserts a graph tab's placeholder text is never shown.
**Amended by the owner on 2026-09-05 (W6-2 PR B, BD-12 — option 1 of
the round-6 decision).** The RELAY is no longer the document's: ONE
`GraphAnnouncer` per workspace is constructed in the workspace's
constructor over its rendered seam and handed to every graph surface's
document — the table's and the Connections leaf's — and it shuts down
in the workspace's bounded drain, not at the document's retirement.
Retirement DROPS the retiring document's pending classes (the mac's
`cancelPending` on view departure, 0a-9) and leaves the relay live for
the other surface. Every other clause of A-1 stands: one document,
seated by the funnel, started by the follow method, retired with the
last graph tab, drained at teardown.
Pinned by facts: one document across two opens and a restore; the
attach at each of the five sites seats and starts nothing (no FFI
call, no announcement); rule L's path facts, each with its exact
sequence and load count, under the pumped dispatcher; the graph's open
never touches the current tab (a dirty note keeps its tab and its
text, GraphOpenKeepsTheDirtyNote); the release on last close (seq advanced, shutdown, pending speech dropped, publication
retired, drain tracked) with a BLOCKED worker and a pending
announcement that never land; a fresh document on reopen; teardown
with a live document and with a retired one still draining, across a
vault close and reopen; the placeholder gone.

**A-2 — The load is 0b's design A on Windows: a pair or rows only, one
token, one envelope, one publication record, one scheduling primitive,
no inline mode.** `PanelWorkScheduler` gains a protected
primitive, StartWorkAlwaysAsync(compute, apply): the compute runs on
the pool through the private `TrackWork` (`:157–172`) and `RunIfLive`
(`:103–110`) in EVERY mode — never inline, the `RunGatedAsync` rule at
`:143–154` made unconditional — and the apply ALWAYS runs on the
serialized owner context the document captured at construction — the
WPF dispatcher in production, an installed DispatcherSynchronizationContext
in a test — never on the completing worker (the scheduler's own
invariant, `:39`); the tracked task completes AFTER the apply, so the
drains wait for the apply too (today's `Post` is outside the tracked
set, `:190–203`), and the document's drain is a FIXED POINT: it
re-snapshots the tracked set after each wait until nothing remains, so
a silent pair an apply enqueued is drained with its parent
(`WhenWorkDrained` snapshots once, `:180–188`). The graph document uses
this primitive for EVERY body — pair, rows only, probe, create — and
none other, and it has NO inline mode: constructing it without a
SynchronizationContext is refused, `synchronousForTests` is not a
parameter it takes, and every fact runs under the canvas's pumped
harness (`CanvasPresentationEngineTests.WithPumpedContext`, `:300–316`,
with `PumpUntil`, `:318–327`) — the production shape, the mode users
run. A receiver therefore never runs inside, or races, the mutation
that started its body (rule L, Term 7). The document's `Load(kind, announce)` issues a token — (this
document, the `VaultSession` reference the body is started against,
the lifecycle generation, the request record `{query:
GraphVisibilityQuery, sort: GraphTableSort}`, `seq` incremented) — and
starts ONE body. A PAIR body calls `GraphSnapshot(filter)` then
`GraphTableRows(query, sort)` (the mac's `:189–200`); a ROWS-ONLY body
calls `GraphTableRows(query, sort)` alone (the mac's `:332`; P1's "once
per generation", `p1_spec.md:67`). The body returns an envelope — the
filter, the query, the sort it used, the results, the token — and
posts it; the dispatcher-side receiver applies the rule at DISPATCH
time, reading the document's fields when it runs (the canvas's
`ApplyPublication` discipline, `CanvasDocumentViewModel.cs:1405–1412`):
(i) the token's document is this document and not retired, its session
is the current session, its lifecycle generation is current, and its
`seq` and request equal the document's current ones — else drop; (ii)
the envelope's query equals the request's query and the envelope's
sort equals the request's sort — else drop; for a PAIR, the envelope's
filter equals the envelope's query filter, and the rows' `generation`
equals the candidate snapshot's — else drop and issue a silent pair
(the two crossings straddled a rebuild); for ROWS ONLY, the held
publication's filter equals the envelope's query filter and the rows'
`generation` equals the held snapshot's — else drop and issue a silent
pair; (iii) build ONE immutable GraphPublication record — the snapshot,
its filter, its generation, the rows, the total, the accepted sort, the
summary text, the load state; no derived index of the snapshot (spec
R-A: no surface caches a derived structure; membership questions scan
`Snapshot.Nodes`) — and install it in ONE property swap (the canvas
slot's shape, `CanvasPublicationSlot.cs:220–254, 270–282`);
every observer binds from that record, so no `PropertyChanged` handler
can see a candidate snapshot with old rows or new rows with an old
total, and the surface re-binds from the record it is handed. A
replacement is never validated against the authority it replaces (the
mac installs the candidate at `:219–222` before `:226`). Failure has
two arms: a PAIR failure installs an ERROR record with the message and
NO snapshot (the mac's `:257–265`: the row buffer is kept in the record,
hidden under the error, so the next pair shows LOADING, not a coherent
old grid — parity, not the canvas's clear at `:1414–1419`); a ROWS-ONLY
failure installs NOTHING, rolls the requested sort back
(`failGraphTableRows`, `:313–316`), keeps the READY record standing,
posts no `GridSorted`, and posts `GraphBlocked{LoadFailed{message}}`
when the tab is effective-active (the mac's `:348–353`). Whether to
SPEAK is decided after the fetch, on the dispatcher, from the
effective-active level then (the mac's `:215`): a document whose tab
closed during the fetch publishes nothing and announces nothing. The
follow method's transitions, the sort and the probe share this ONE
receiver. Pinned by facts, each under the pumped dispatcher context (the
primitive's own facts: the compute never runs on the calling thread,
the apply always runs on the context's thread, the drain returns only
after the apply and after the work the apply enqueued — the fixed
point — and constructing the document without a context throws): a stale `seq` drops; a request mismatch
drops; a document mismatch drops (a result addressed to a retired
document); a session mismatch drops after a vault reopen; an injected
envelope whose snapshot and rows disagree on generation drops and
re-fetches, and the re-fetch publishes; an injected envelope whose
filter disagrees with its query drops; injected envelopes whose name
needle, kind overlay or sort disagree with the request each drop; a
rows-only result under a changed generation drops and the pair that
follows publishes; every observer callback sees a self-consistent
record (a handler that reads the record's rows and total during the
swap finds them from one record); reordered requests (a sort issued
during a pair) publish only the last token; a closed tab publishes
nothing; a failed pair installs the error record, hides the rows and
shows LOADING on retry; a failed rows-only keeps READY, rolls the sort
back and posts the High event; teardown during a fetch lands nothing.

**A-3 — The generation probe on file change AND scan finished, gated,
never inline anywhere.** `VaultLifecycleViewModel` gains one line in
`HandleFileChange` beside `NotifyBasesOfVaultChange` (`:753`) and a new
marshalled arm for the index phase — the third delegate at `:439`
becomes `(phase, seen) => _enqueueUi(() => HandleIndexPhase(generation,
phase, seen))`, and HandleIndexPhase acts only on
`IndexPhase.ScanFinished` under the same lifecycle check
`HandleFileChange` uses (`:715`) — both calling the workspace's
NotifyGraphOfVaultChange, which, when a graph document exists AND the
graph tab is VISIBLE (rule L, Term 2 — the mac's `anyGraphTabVisible`;
a hidden graph is not probed and loads on its next activation), calls
the document's Probe. The probe's compute runs through
StartWorkAlwaysAsync (A-2) — never inline — because the listener
callbacks may arrive with session locks held and forbid a synchronous
session call (`session.rs:700–702, 707`), and a test's inline
`_enqueueUi` would otherwise call `GraphGeneration()` inside the
listener's frame; the compute reads `GraphGeneration()` and the apply
decides: when a READY publication is held and the probed generation
differs from its generation, issue a SILENT pair with a fresh `seq`
(the mac's `loadGraphTable(announce: .silent)`, `:302`) — a pair
already in flight is thereby SUPERSEDED, its result dropping at step
(i) (the mac's load sequence, `:488–491`), so a generation that arose
after the in-flight pair's crossings is never lost; when NO
publication is held or the held one is not READY (a first pair or a
retry in flight), the probed generation is kept as a HIGH-WATER MARK
and, when the next pair publishes with a generation below it, a silent
pair follows. A silent pair announces nothing: no summary, no filter
count, no status — the rows, the total and the summary region update
under the reader. The probe is not debounced by the sidebar's 150 ms
ticket (`:767–776`). Pinned by facts: a Slate write that changes the graph (a
save adding a link) reloads silently and the announcer records
nothing; an external edit surfaces at the scan-finished arm and
reloads; a save that does not change the generation issues no pair
(the FFI call count observed through the test session's counter); a
closed tab probes nothing; a lifecycle advance drops the probe; a
listener driven with an INLINE enqueue delegate makes no session call
inside the listener's frame — asserted with a call-stack sentinel (a
scoped flag the listener sets around its callback, checked by the test
session's `GraphGeneration` wrapper), not with thread identities; a
burst of five events yields one published pair; a generation that
arises after an in-flight pair fetched both its results and before it
applies ends in a publication at the newer generation (the gated
fact, the canvas's publish-gate seam shape); a hidden graph is not
probed and its next activation loads the newer generation.

**A-4 — Load states, with the mac's labels.** The surface has four
states, in this precedence: LOADING while a pair is in flight and the
publication holds no snapshot (the label "Loading graph…", accessible
"Loading graph.", `GraphTableView.swift:300–307`); ERROR when the last
PAIR failed (the visible text is the raw message, core's `VaultError`
text humanised as the shell does elsewhere, and the view's accessible
name is "Graph error: {message}" — the mac's split, `:309–316`; the
announcement `GraphBlocked{LoadFailed{message}}`, High); EMPTY when the
snapshot is held and the rows are empty (the label "No notes match the
current filters.", `:257`; the summary region still carries
`audio_summary` over zero counts — the empty vault's summary is 0a's
`GraphSnapshotSummary` rendering, and a preset's empty case is PR C's);
READY otherwise (the grid). A rows-only reload, and its failure, keep
the grid under READY; a pair reload with a held snapshot keeps the
grid; a pair after a PAIR failure shows LOADING (A-2). The labels are
view text, not announcement templates (spec §5's copy rule); the
announcements are the family's. Pinned by facts for each state and each
transition into it, including pair failure → retry and rows-only
failure → still READY.

**A-5 — The table is core's columns, core's order, core's default
sort, with nothing typed in the shell; a sort speaks once, on
adoption.** GraphTableView configures an `AccessibleDataGrid` with
`GridAutomationId = "GraphTableGrid"` and builds its
`AccessibleGridColumn` list by iterating `GraphTableColumns()` in order
(`lib.rs:3922`; `table_columns`, `graph_queries.rs:572–580`): `Header =
spec.Header` (core's headers, which are the mac's titles verbatim),
`Cell = row => ((GraphTableRow)row).Cells[index]`, `IsRowHeader =
spec.Column == GraphTableColumn.Note`, `IsExternallySortable = true`,
`Sort = null`, `AccessibilityHint` = the ghost-creation admission
reason for a ghost row (the mac's `:465–477`). The grid's row objects
are the `GraphTableRow` records themselves — no wrapper: the record's
`Cells` vector is the projection (0b-7) and its `StableKey` the
identity. The sort is external: `ExternalSortHandler` (`:652–663`)
maps the column index through the SAME vector (index → `spec.Column`)
to a `GraphTableSort` and calls the document's SetGraphTableSort, which
issues a ROWS-ONLY token (A-2) — the grid neither reorders nor
announces; after every publication the surface re-asserts
`SetSortIndicator(accepted)` (`:665–680`; the bases'
`BaseSurfaceView.cs:728`). The sort's announcement is the surface's, on
ADOPTION (the mac's deferred `GridSorted`, `AccessibleDataGrid.swift:630–642`,
and the substrate's own doc, `:657–659`): when a publication answers a
sort request and the accepted sort differs from the previously accepted
one, the surface relays `GridSorted(header, ascending)` — core's
event, the header from the vector — exactly once; a rejected, failed or
superseded request (a newer token before the publication) announces
nothing, and a sort request equal to the accepted sort is a no-op ONLY
while no request is pending — with a request pending it issues a token
that supersedes the pending one, so the reader's latest intent wins
(the mac's whole guard, `:360`: the sort differs from the accepted one
OR a request is pending). The default sort is FETCHED: this PR adds the free
function `graph_table_default_sort()` returning
`GraphTableSort::default()` (`graph_queries.rs:589–596`) to core's
surface list (twenty-four names; `GraphQuerySurfaceCensus`'s floor
rises), its FFI mirror and the generated binding, and both hosts use it
— the mac's literal at `AppState+GraphTable.swift:135` becomes the call
(AD-1; the spec's Consumes list gains the name, AD-9). A census reads
every source under `Graph/` and asserts none of the nine header strings
appears as a literal. Pinned by facts: the grid's headers equal core's
vector in order; the row header column is Note; each cell equals the
record's cell; a sort request through the handler issues a rows-only
token whose sort maps back to the clicked column; the indicator follows
the ACCEPTED sort, not the requested one; `GridSorted` once on
adoption, none on rejection, failure or supersession, none for a no-op;
accepted A, B pending, A requested again → B's result drops and A
stands with no announcement; the default sort equals the fetched
record.

**A-6 — The row's Name is core's row copy at the set verbosity through
a row-name seam; the kind rides the item status.** The substrate gains
two optional `Bind` parameters, generic and graph-first:
`rowAutomationName` (a `Func<object, string?>`) applied as
`AutomationProperties.Name` on each realized `DataGridRow` in
`OnLoadingRow` (`:203–209`) and cleared in a new `UnloadingRow`
handler, so the UIA row Name is the delegate's text and not the
container's default; and `rowItemStatus` (a `Func<object, string?>`)
applied as `AutomationProperties.ItemStatus` the same way. The
substrate's virtualisation stays STANDARD (`:117`): containers are
created for the rows that come into view and discarded when they leave,
never reused, so the seams are applied to every NEWLY realized row and
there is no recycled container to re-label. `rowAudioDescription`
keeps its one consumer, `GridRowMoved` (`:840–846`). The graph passes
the SAME text to `rowAutomationName` and `rowAudioDescription`: the
rendered text of `A11yEvent::Graph(GraphRow{verbosity, row})` with
`GraphRowCopy` built from the record — `label`, `kind`, `in_links` =
`LinksIn`, `out_links` = `LinksOut`, `references` = `LinksIn +
EmbedsIn` for a ghost (0a's rule), `embed = false` (a table row focuses
no relationship) — obtained through the relay's non-posting boundary
GraphAnnouncer.RenderLabel(event), which calls `A11yRender` and returns
the text, so the relay stays the one file that renders (A-10). The
verbosity is Standard in this PR (AD-6; PR C's menu and the config's
`verbosity` key set it). The rendering is 0a's corpus row, so the
row's Name is P1's copy byte-for-byte and no template is typed here
(the mac speaks the same copy on row focus through its announcer's
`GraphRow`, not as a row label).
`rowItemStatus` returns the Kind cell through the document's ONE cell
lookup — a helper keyed by `GraphTableColumn` that finds the column's
position in the fetched vector — and a census asserts that no source
under `Graph/` reads `Cells` except that helper, so no host-side
position knowledge can be expressed in any syntax; a fact drives the
helper with a deliberately reordered vector and still finds the Kind
cell by column — the mac has no item status; a recorded Windows-only
enrichment (A-D1, AD-2). The row NAME itself is a recorded divergence
too: the mac grid's rows carry no P1 row label — its `GraphTableView`
passes no row description and the cells alone are read
(`GraphTableView.swift:322–341`) — where the spec requires the Windows
row's Name to be the P1 copy (A-D3). Pinned by facts:
a realized row's `AutomationProperties.Name` equals the corpus
rendering for a note, an attachment and a ghost, and its ItemStatus
the Kind cell; a row realized after a scroll carries its own name; an
unloaded container's name is cleared; `GridRowMoved` carries the same
text; a Terse verbosity, when PR C sets it, yields the label alone (the
fact is written now against the document's verbosity field);
`RenderLabel` posts nothing (the dispatcher's activity count
unchanged).

**A-7 — Selection writes the shared key, revalidated against the
authority; the summary region is `audio_summary` verbatim.** The
grid's current row writes `GraphViewState.SelectedKey` (the row's
`StableKey`; the mac's `graphSelectedNodeKey`, `AppState.swift:3108`).
The DOCUMENT revalidates the key at every pair publication by scanning
the record's `Snapshot.Nodes` for the key (no derived set, spec R-A),
clearing it only when the node is gone from the SNAPSHOT (the mac's `revalidateGraphSelection(against:)`, `:370–375`)
— never against the rows, so a key hidden by PR C's name or kind
overlay survives; the SURFACE, after each publication, re-seats the
grid's current row on the row whose key equals `SelectedKey` and, when
no visible row carries it, clears the grid's currency WITHOUT writing
the key. The bind and the re-seat run under a syncing guard (the
canvas's `_syncingSelection`, `CanvasTableView.cs:262–278`): `Bind`
restores the reader's position by row-header text and ordinal and
raises `CurrentRowChanged` while doing so (`:471–487, 527–528,
822–829`), and a view→model write during that window would overwrite
the key with the substrate's guess. PR B's leaf and PR D's diagram read
and write the same field. `Bind`'s `summary` is the record's
`AudioSummary` verbatim and `accessibilityLabel` is "Graph, data grid"
(the mac's `:326–327`); the summary region's UIA name is therefore
`Summary: {audio_summary}` (`:524`). Pinned by facts: selection
round-trips through the key; a publication re-seats the same key
across a reorder; two rows with one label re-seat by key, not by header
text; a key hidden from the rows keeps `SelectedKey` and clears
currency; a key gone from the snapshot clears `SelectedKey`; a
background publication writes nothing to the key and moves focus
nowhere (the summary region keeps focus when it had it); the summary
region text equals the record's summary.

**A-8 — Row actions are core's vectors, fetched once, unioned in
core's order; each action is a shell seam; the create is the
sidebar's two-phase seam with typed outcomes.** The document fetches
`GraphRowActions(kind)` (`lib.rs:3794`; `row_actions`,
`graph_queries.rs:810–821`) ONCE per kind — Note, Attachment, Ghost —
when it is created (the mac caches the three vectors,
`GraphViewState.swift:18–19`), and the surface builds ONE
`AccessibleGridRowAction` list for the grid (the substrate's shape,
`:1312–1335`) by iterating `GraphRowAction::ALL`'s order as the union
of the three vectors — Open, Open in New Tab, Show connections, Reveal
in File Tree, Create note, each `Name = spec.Title` from the vector that
carries it — with `IsVisible = row => vector(row.Kind).Contains(action)`,
so a note or attachment shows the four navigation actions and a ghost
shows Create note; nothing typed, nothing reordered, no crossing per
row. Every action and activation is ADDRESSED: the surface captures its
graph tab and the tab's group when the action is invoked and makes
them active THEN, synchronously, at invocation (the mac's `tabID` and
`focusOwningGroup`, `GraphTableView.swift:208, 372–375`; the shell's
`Activate(group, tab)`, `Layout.cs:68–93`), so a synchronous open lands
in the graph's own pane; an asynchronous completion (the create) never
activates anything — at completion the captured address is checked,
and when it is no longer current (the reader moved, the graph retired)
the VISUAL open is suppressed while the completion itself still runs
(below); focus is never taken from a reader who moved. Each action's
`Execute` calls a seam on the document the workspace wires at attach
(the bases' `OpenRowFromSurface`, `WorkspaceViewModel.Bases.cs:1338`):
Open and Open in New Tab → the workspace's
OpenGraphRowFromSurface(address, path, target, after) (A-9); Reveal
in File Tree → the files sidebar's new SelectPathFromSurface(path),
which expands the ancestors, selects the node and focuses the tree (the
mac's meaning; AD-11); Show connections → a seam PR B fills; Create
note → the ghost's path from `GraphGhostNotePath` (0b-11) through the
note-creator seam generalised from `ICanvasNoteCreator` to a
surface-neutral interface with the same two members and TYPED outcomes
— `Landed(caveat)`, `Exists(message)`, `Failed(message)`, `Unavailable`
— where `Exists` gains the refusal's message (the `DestinationExists`
exception's, which the sidebar's arm at
`FilesSidebarViewModel.CanvasNotes.cs:35–37` discards today; the canvas
keeps matching by type) and `Unavailable` keeps its one meaning, the
session-work refusal at shutdown (`:24–31`). The create runs through
StartWorkAlwaysAsync (A-2; never on the dispatcher, never inline) with
EMPTY content (the mac's `:271`), and its completion is the
WORKSPACE's, not the document's: on `Landed` the apply hands the result
to a workspace completion keyed by the LIFECYCLE generation alone —
independent of the graph tab's liveness, because the file exists once
`createExclusive` returned and a graph closed in the meantime must not
swallow the bookkeeping — which, while the lifecycle generation is
current, runs the sidebar's barrier and tree refresh (the landing's
first half), then opens the note in the captured pane's current tab
through OpenGraphRowFromSurface when the captured address is still
current (that tab is the graph tab, never dirty, so the dirty gate
`Layout.cs:143–151` cannot refuse it; when the address is not current
the visual open is suppressed), then posts ONE
`A11yEvent::Graph(GraphStatus{NoteCreated{leaf}})` through the
workspace's announcer — after the open attempt, whatever it did (the
mac's order and its unconditional post, `AppState+Connections.swift:294–298`)
— and THEN speaks the landed-but-unindexed caveat when there is one
(the sidebar's order: the confirmation first, the caveat after,
`CreateOutcomes.cs:19–24`); the workspace posts the event because
delivery must not depend on the graph's liveness — the open may
replace the graph tab and retire its relay, or may activate an
existing missing-file tab for the same path and leave the graph live
(`Layout.cs:117–126`) — and AD-8 records it as the one graph-family
event posted from outside `Graph/`; the mac then installs an inline
rename for the created entry (`:295–296`) — Windows does not (A-D4); `Exists(message)` and `Failed(message)` →
`GraphBlocked{NoteCreateFailed{message}}` through the relay (High; the
mac's `createExclusive` failure arm); `Unavailable` → nothing: the
session is shutting down and the token is dead (a retired-token drop);
a vault close during the create lands nothing. Admission is host state
(0bD-8): `IsEnabled` is false and `DisabledReason` is the shell's
reason when the seam reports one — Windows has no structural-mutation
gate today, so Create note is admitted, and the seam's reason is the
hook a future gate fills — and Show connections is disabled without a
reason until PR B wires its seam (AD-3, AR-2). A disabled action is
still LISTED. Pinned by facts: the visible names per kind equal the
kind's vector in order; the crossing count for the actions is three
for a 1-row and a 10,000-row grid; each enabled action reaches its seam
with the row's path or ghost path and the graph's own group is made
active at invocation (a graph visible in a non-focused pane opens in
ITS pane); Create note runs its create off the dispatcher with empty
content, lands, opens in the graph's pane, and ONE `NoteCreated` posts
after the open — no `OpenedFile` — with the caveat, when there is one,
after it; a graph closed between `createExclusive` and the apply still
gets its barrier, refresh and `NoteCreated`, with the open suppressed;
a reader who moved to another pane during the create is not moved back
and the open is suppressed; a missing-file tab already open for the
ghost path is activated by the open and the graph stays live, and
`NoteCreated` still posts; `Exists` and `Failed` each announce their High event
with the message and open nothing; `Unavailable` announces nothing; a
vault close during the create lands nothing; Show connections is
present and disabled, and setting its seam enables it.

**A-9 — Activation is Open; modified activation is Open in New Tab; a
ghost's activation is Create note; the open seam carries its
announcement.** The substrate gains a generic `rowActivatedModified`
callback beside `rowActivated`: Ctrl+Enter and Ctrl+double-click
deliver it (`:906–914, 1162` gain the modifier arm); plain Enter and
double-click deliver `rowActivated`. The graph wires `rowActivated` to
Open (current tab) and `rowActivatedModified` to Open in New Tab, and
for a GHOST both deliver Create note under the same admission (the
mac's one gate, `GraphTableView.swift:357–365`). The open is the
workspace's OpenGraphRowFromSurface(address, path, target, after): it
checks the address is current (A-8: for a synchronous activation it
always is, the group and tab having been made active at invocation),
runs `OpenPathCore(path, target)` under the workspace mutation
(`WorkspaceViewModel.cs:1869–1870, 1928–1944`) and, on success, posts
the `after` event through the WORKSPACE's announcer (`_announce`) — for
Open and Open in New Tab that is `OpenedFile(leaf)`, the shell's
convention for a surface open (the bases', `BasesOpenRow`,
`WorkspaceViewModel.Bases.cs:473–481`), where the mac's `openFile`
posts nothing (`:363`) — a recorded Windows-only announcement (A-D2);
the create passes no `after` — its `NoteCreated` is the create's own
completion, posted whatever the open did (A-8); the graph family posts
no event of its own on activation. The event rides the workspace
and not the relay because opening in the current tab replaces the
graph tab and retires the document (A-1). `OpenPathCore` opens any non-blank path — it checks nothing on disk
(`:1930–1935`; a note deleted since the snapshot opens as the shell's
missing-file tab, the shell's business, and the next probe drops the
row) — so the only refusal is the dirty gate's, which the graph's own
pane cannot raise (A-8); a refused open posts nothing. Pinned by facts:
plain activation of a note reaches the seam with `CurrentTab` in the
graph's pane and `OpenedFile` posts through the workspace after the
graph document retired; modified activation reaches it with `NewTab`
and the graph document stays; a ghost's plain and modified activation
both enter the create funnel; a path deleted since the snapshot opens
and posts `OpenedFile` like any other.

**A-10 — One relay, walled by path; the sequences.** GraphAnnouncer is
the Windows twin of `GraphAnnouncer.swift` and the sibling of
`CanvasAnnouncer` (`Canvas/CanvasAnnouncer.cs:28`):
`Announce(GraphA11yEvent)` classifies by the pinned list
(`a11y.rs:138–152`: `navigation` = `GraphRow`, `filter` =
`GraphFilterCount`, `forceValue` = `GraphForceValue`, `settle` =
`GraphLayoutSettled`; everything else immediate), coalesces each class
200 ms latest-wins with the canvas's timer shape, and a High event —
`GraphBlocked`, announced or relayed — flushes and DROPS all four
pending classes; `Relay(A11yEvent)` passes a non-graph event through
uncoalesced (the grid's own events: `GridSorted` on adoption,
`GridRowMoved`, `GridCellMoved`, the copy); RenderLabel(event) renders
without posting (A-6); Shutdown() drops every pending class and
refuses further posts (the mac's `cancelPending`, `:150`; A-1). The
relay is the ONE file under `Graph/` that renders (`A11yRender`) and
posts (`AccessibilityNotificationDispatcher.Post`, the workspace's
seam); the grid's `Announce` is swapped to `Relay` at attach (the
canvas table's `CanvasTableView.cs:195`) and is silent before it
(`:199`). Two censuses wall the directory, the canvas pair keyed on
`Graph/`: `AnnouncementSeamCensus` gains
NoGraphCodeReachesTheAnnouncerExceptThroughTheBoundary (sources
`Graph/**/*.cs`, the forbidden surface read from the relay's own members
that reach its emit, named seams each hit — `:353, 526–534, 518–524`),
and a GraphAnnouncerCensus with NoGraphSourceAnnouncesOutsideTheRelay,
EveryGridUnderGraphRidesTheRelay, TheRelayIsTheOneFileThatRenders
(`CanvasAnnouncerCensus.cs:38, 111, 207`) that exempts exactly the
normalised relative path `Graph/GraphAnnouncer.cs`, asserts exactly one
such file exists, and scans every nested `Graph/**/*.cs` (the canvas's
basename exemption at `:43` is not copied). The class list is pinned
from the Rust side too: the slate-uniffi test
`the_windows_graph_coalescing_switch_matches_the_pinned_class_list`
reads `Graph/GraphAnnouncer.cs`'s switch and compares membership per
class both ways with the list core pins, as the mac's
`the_mac_graph_coalescing_switch_matches_the_pinned_class_list`
(`lib.rs:12864–12914`) and the Windows canvas twin (`:12916–12932`)
do. The sequences are rule L's Term 6, projected onto the graph family
plus `ReopenedGraph`: Open → `GraphStatus{Opened}`, then
`GraphSnapshotSummary` when Term 4 loads; Open with the graph effective
and READY → `Opened` alone; Activation by tab (the restore's included)
→ the summary alone; Activation by group → nothing when READY, the
summary otherwise; Reopen → `Opened`, the shell's `ReopenedGraph`, then
the summary when Term 4 loads and nothing more when it does not; a
load failure → the status the cause owes, then `GraphBlocked{LoadFailed}`
where the summary would be; none of these is coalesced.
**Amended by the owner on 2026-09-05 (W6-2 PR B, BD-12 — option 1 of
the round-6 decision).** "One relay" is one INSTANCE per WORKSPACE —
the mac's one announcer per app: constructed once in
`WorkspaceViewModel`'s constructor over `_announceRendered` (the one
seed the census counts, moved out of `NewGraphDocument`), handed to the
graph document and to the Connections leaf's document, so the four
coalescing classes and the High flush span every graph surface;
`Shutdown()` runs in the workspace's drain; a document's retirement
calls the relay's `DropAllPending()` — the mac's `cancelPending` on view
departure (`:150`), which A-1's amendment names — and the relay stays
live. The relay also gains the filter class's FIRE-TIME GATE 0a-9 names
(`GraphAnnouncer.swift:194–207`): a pending `GraphFilterCount` fires
only while the graph tab is EFFECTIVE, the mac's `graphTabActive`, and
is dropped otherwise; PR A's Windows relay had omitted it (found by PR
B's round 5, IGF-4). Every other clause of A-10 stands: the file wall,
the classes, the window, the High flush, the sequences.
Pinned by facts: the class membership (both hosts'
switch-list tests); the window, latest-wins and the High flush in a
Windows GraphAnnouncerTests twin of the canvas's; each sequence, under the
pumped dispatcher; Shutdown drops a pending navigation event;
every announcement under `Graph/` through the relay (the censuses).

**A-11 — The header: the mode switcher is core's vector; the Diagram
waits for PR D.** GraphSurfaceView's header carries the mode switcher
built from `GraphSurfaceModes()` (`lib.rs:4195`; `surface_modes()`,
`graph_config.rs`) in order — Table, Diagram — with each item's `Title`
as its label and `Tag` as its automation id, and the summary region
below the grid. The switcher reflects `GraphViewState.Mode`. In this PR
only Table is selectable: the Diagram item is present and disabled
(admission again), no transition can occur, so no `GraphMode` event
fires here; PR D wires the switch and its event. The header's own name
is the tab's kind label ("Graph", `WorkspaceViewModel.cs:330`). Pinned
by facts: the items equal the vector's titles in order; Diagram is
disabled; Table is checked and equals the view state's mode.

**A-12 — The command row, the scope, the matrix, the evidence.**
`ChordTable` gains `Ids.GraphOpenTab` = `slate.graph.openTab`, a
chordless row in `CommandSection.Graph` with the label "Graph: Open
Graph" and a hint, on the `Reg(Ids.CanvasShowOutline, …)` template
(`ChordTable.cs:922–923`); `Reg` gives it `ChordScope.None` (`:578`),
which is the correct scope for a chordless row — the spec's
"`ChordScope.Graph`" is DECLARED by this PR (the enum member after
`Canvas`, `:69`) and first used by PR C's chorded rows, and the spec
text is amended to say so (AD-9). The command registers in the palette
through the registrar and calls `OpenGraph()`, which sets rule L's
cause to Open before its mutation; `chords.json` is regenerated under
the SLATE_CHORDS_UPDATE variable (`ChordTableTests.cs:31`) and its
`deliveryEvidence.commands` gains `slate.graph.openTab` with the
implementation and test members (`chords.json:2148`'s shape;
`generate-parity-matrix.py:802–809` validates). `w_c_matrix.md` gains
the row "Graph table (W6-2 PR A)" on the canvas table row's shape
(`:42`) with the three screen-reader columns Pending, pinned by a
WcMatrixGraphEvidenceCensus twin of `WcMatrixCanvasEvidenceCensus`. The
matrix's command row for `slate.graph.openTab` is ✓; the surface row
stays pending until PR F.

**A-13 — Persistence and the singleton twins.** The singleton restore
collapse stands as tested (`W1WorkspaceRedTeamTests.cs:336–378`) and
now seats the ONE document on the surviving tab; rule L's Restore
cause loads it with the summary when it is the effective tab, and a
graph tab restored into a non-active group loads when it first
becomes effective (a transition BY TAB or, if it was its group's active
tab, BY GROUP with no publication held — both load). The mac routing suite's PR-A cases gain
Windows twins in `W1WorkspaceRedTeamTests`: GraphTabLoadsSnapshot (the
document publishes over a real session), GraphTabNotAddressableByPath
(path resolution never yields the graph tab, and opening the token as
a path makes an ordinary markdown tab for a missing file, never the
graph — `OpenPathCore` takes any non-blank path, `:1928–1935`),
RefreshProbeGatedOutWhenLifecycleAdvanced,
RefreshGateClosesWithLastGraphTab, and GraphOpenKeepsTheDirtyNote (the
observable both hosts share: dirty editor text survives opening the
graph and returning to the note — the mac parks a shared buffer, the
Windows editors are per tab, AD-14); the existing dedup,
duplicate-and-split refusal and restore tests stand.

**A-14 — §W-A: the document's rows and summary equal the artifact's,
under the artifact's filter.** GraphDocumentTests opens the graph vault
(`crates/slate-core/tests/fixtures/graph_vault/`, 0b-13) through a real
`VaultSession` and the document, and for each of the sixteen `table`
entries of `parity_golden/graph_queries.json`: sets the document's
filter to the entry's query — the artifact's `all` is the harness's
INCLUSIVE filter (`SurfaceSerializer.cs:1191`), not core's default —
so a pair is issued under that filter, then sets the entry's sort
through the external-sort seam, drains, and asserts the published rows'
keys and cells equal the entry's rows in order — every cell but the
Modified one (the artifact excludes it, 0bD-5; the fact excludes it by
the column vector's `Modified` position, not a literal index) — that
`total` equals the entry's, and that the session observed the entry's
filter as the pair's argument (the test session's recorded calls). The
summary is compared to the ARTIFACT's: this PR adds `summary` to each
table entry of the `graph_queries` artifact — core's `audio_summary`
under the entry's filter — in both serializers (the harness's table writer,
`SurfaceSerializer.cs:1430–1462`, and the mac twin,
`ParityHarnessTests.swift`'s `graphQueriesArtifact`, `:305–308`),
regenerates the golden through the harness, extends the schema-walk
census with the new string key, and the fact asserts the record's
summary equals the entry's `summary` byte for byte (AD-13; the mac
lane's twin test proves the mac serializer writes the same bytes,
AR-4). The fact runs under the pumped dispatcher. The artifact
is read, never regenerated, by the test; regeneration stays the
harness's (`--graph-fixtures`).

**A-15 — §K: snapshot marshalling through the binding, asserted; the
10k grid virtualised, counted.** The benchmarks project gains
GraphOpenBenchmarks beside `CanvasOpenBenchmarks` (the class shape
`CanvasRendererBenchmarks.cs:17–21`) and a `--graph` runner arm on the
`--canvas` pattern (`Program.cs:12–17`): over synthetic linked vaults at
1k and 10k notes (core's `generate_linked_vault` shape, built into a
temp vault and scanned), the workloads are the snapshot under the
default filter, the table rows under the default sort, and the
document's open through to the first publication, each at 1k and 10k —
six reports. The `--graph` arm CAPTURES the suite's `Summary` (the
`--canvas` arm discards its open suite's, `Program.cs:27`, and gates
only the renderer's, `:28–57` — the shape is copied, the omission is
not) and walks a pinned inventory: every (workload, scale) has an
entry, each entry names a budget or is marked measurement-only, a
report missing from the inventory or an inventory entry without a
report fails the run, and every budgeted entry prints its `PASS`/`MISS`
line. P set no host-side marshalling budget (locked decision 10 budgets
the layout and the backend, `00_program.md:24`); the budgets asserted
are Windows host budgets this PR sets — 500 ms median for
open-to-publication at 10k, 100 ms at 1k, the canvas's
first-derivation precedent; the snapshot and rows workloads are
measurement-only, their medians recorded — and the spec's "against
P's budgets" is amended to name them (AD-7, AD-9); a miss is a finding
for the task loop, not a waiver. The medians
land in `BENCHMARKS.md` under "Milestone W6-2 — graph through the C#
binding" (spec §4 PR F item 2 opens that section; this PR writes its
first rows). P1's virtualisation test (`p1_spec.md:73`) is a Windows
fact in GraphTableTests: 10,000 rows bound to a REAL
`AccessibleDataGrid` in a test window under the substrate's STANDARD
virtualisation, the live `DataGridRow` containers counted through the
loading and unloading handlers (loaded minus unloaded) while the reader
pages from the first row to the last, asserted to stay bounded by the
viewport's row capacity plus the panel's cache length — never the row
count — with the action-inventory crossing count asserted constant in
the same fact (A-8).

**A-16 — §W-C: the journey, the axe scan, CI's shell gate.** The FlaUI
journey GraphSurfaces_TableSortSelectionAndActivation_AreClean opens
the graph tab through the command, waits for the grid
(`GraphTableGrid`), walks the rows by UIA and asserts each row's Name
is the P1 copy (A-6's seam) and its ItemStatus the kind, reads the
summary region's Name, sorts by a header and asserts the indicator and
the first row changed as the artifact says, activates a row and
asserts the note opened, and runs axe with the scan id `graph-table`
(the second id after `canvas-table`). The gate is CI's shell
accessibility lane; a local run is a serialized smoke with no screen
reader running (the standing note). The journey's evidence is the
matrix row's (A-12).

**A-17 — Tripwires and censuses this PR adds or extends; what the
citation census does and does not guarantee.** The two directory
censuses (A-10); the no-typed-header census (A-5); the one-cell-lookup census
(A-6); the one-caller census of rule L (A-1); the switch-list twin (A-10);
`TheAttachFunnelDocCommentNamesEveryCallSite` unchanged and green with
the graph arm; a placeholder census: no `WorkspaceItemKind` that has a
surface is a placeholder (Graph joins canvas); the view-state census
(A-1); `GraphQuerySurfaceCensus`'s floor at twenty-four (A-5); the
schema-walk census's new `summary` key (A-14);
`GraphContractsCitationCensus` gains the PR A tuple, its floor one
below the population; `ChordTableTests` sees the row and the JSON;
WcMatrixGraphEvidenceCensus (A-12); the announcement-seam census's
named-seam list gains the graph's seams so an unused exemption fails.
The citation census guarantees that every backticked identifier of
fifteen or more characters exists in the Windows tree and that the
section cites at least its floor; it does NOT resolve a `file:line`
citation, and a drifted range passes it — the red-team rounds are the
line check, and AD-10 records the scope.

### Decisions

- **AD-1 — The default sort is fetched.** `graph_table_default_sort()`
  joins core's surface (twenty-four names), its FFI and the binding,
  and the mac's literal becomes the call: an inventory of one is still
  an inventory (0bD-13), and the census that proves "nothing typed"
  cannot exempt one value.
- **AD-2 — The row Name and the kind badge ride two substrate seams.**
  `Bind` gains `rowAutomationName` and `rowItemStatus`; the substrate
  had no row-name seam (`rowAudioDescription` feeds only the row-move
  announcement), the seams are generic, and the graph is their first
  user (A-6). The item status is Windows-only (A-D1).
- **AD-3 — Show connections is listed and disabled until PR B.** The
  vector is core's and admission is the host's (0bD-8); a disabled
  action with no reason is the honest state between the two merges
  (AR-2).
- **AD-4 — The document is a `PanelWorkScheduler` with ONE scheduling
  primitive and NO inline mode, its publication is one record, and the
  row is the record.** Every body — pair, rows only, probe, create —
  runs through StartWorkAlwaysAsync(compute, apply): compute on the
  pool, apply on the serialized owner context the document captured
  (a dispatcher; a test installs and pumps one), the tracked task
  complete after the apply, the drain a fixed point; the document
  never uses the inline `StartWork` or `Post` and refuses to exist
  without a context, so a receiver never runs inside or races the
  mutation that started it (A-2). Validated at dispatch time, installed
  as one immutable GraphPublication in one property swap; the grid
  binds `GraphTableRow` records directly — the cells vector is the
  projection and a wrapper would be a second one.
- **AD-5 — The probe is a workspace notification on both listener
  arms, gated VISIBLE, superseding.** `HandleFileChange` already fans
  out to bases and history; the graph is one more line there and one
  new marshalled arm for `ScanFinished` (A-3), gated on a VISIBLE graph
  tab (the mac's), issuing a superseding silent pair on a changed
  generation and keeping a high-water mark while no READY publication
  is held — no debounce, no second generation counter. The probe's
  session call runs on the pool in every mode because the listener
  forbids synchronous reentry and a test's inline enqueue would
  otherwise produce it.
- **AD-6 — Verbosity is Standard in this PR.** The document carries the
  field and the row name reads it; PR C's menu and the config key
  (0bD-7) set it. The Terse fact is written now.
- **AD-7 — The §K budgets are Windows host budgets this PR sets**, on
  the canvas precedent's numbers: P budgeted the backend and the layout,
  not the host's marshalling of the snapshot; 500 ms / 100 ms are
  asserted so a regression fails a run, the task loop records the
  measured medians beside them, and the spec's §K line names them
  (AD-9).
- **AD-8 — Create note is the sidebar's two-phase seam with typed
  outcomes, addressed at invocation, completed by the workspace under
  the lifecycle generation, opened when the address is still current,
  and announced once after the attempt.** The ghost's path is core's
  (0b-11), the content empty (the mac's), the create is the sidebar's
  worker-safe phase and dispatcher-side landing (the canvas's
  precedent, generalised, `Exists` gaining its message), the open is the
  workspace's in the addressed pane, and the family's `NoteCreated` is
  the ONE graph-family event posted from outside `Graph/` — by the
  workspace, after the open attempt and regardless of its result,
  because the note exists and delivery must not depend on the graph's
  liveness (A-8). The
  directory censuses wall `Graph/`; this one posting site is named in
  the announcement-seam census's exemptions and asserted to be the only
  one.
- **AD-9 — The spec's stale or superseded lines are corrected here,
  not repeated** (0bD-12): `WorkspaceViewModel.cs:289–300` → `:296–309`;
  `AppState+GraphTable.swift:195–199` → `:235–243`; "`ChordScope.Graph`"
  for the chordless row → declared here, used by PR C; the "flat
  snapshot if 0b is still in review" contingency → moot, 0b merged
  first; "sort announced by the substrate" → announced by the surface
  on adoption (A-5); "against P's budgets" → the Windows host budgets
  of AD-7; the Consumes list → gains `graph_table_default_sort` (AD-1);
  "the announcement on open is `GraphStatus{Opened}` then
  `GraphSnapshotSummary`, mac's order" → that is the Open cause's
  sequence; Activation and the restore speak the summary alone; a
  reopen speaks `Opened`, then the shell's `ReopenedGraph`, then the
  summary when a load runs and nothing more when the tab was already
  shown and loaded — the reopen line sits BETWEEN the two, not after
  both (Term 6; the spec's item 7 amended again by this revision).
- **AD-10 — The citation census's guarantee is identifier existence
  and a floor**, not line resolution (A-17). A resolver for `file:line`
  anchors is not built in this PR; the rounds check the lines, and a
  drift found is corrected in the next revision.
- **AD-11 — "Reveal in File Tree" selects the note in the files
  sidebar** through a new sidebar seam (A-8); this is the mac's
  meaning, so it is parity, and the sidebar's own "Reveal in File
  Explorer" (FD-5) is a different verb under a similar name — a
  clarification, not a divergence.
- **AD-12 — Rule L's cause is a mutation-scoped field, not a
  parameter, and only Open and Reopen are causes.** `SyncPanels` has
  no arguments and five callers; threading a cause through them would
  touch every caller for a value only two of them know. The two
  explicit paths set the field before their mutation; the follow method
  consumes it only at the graph's own transition (or at an explicit
  Open with the graph already effective), leaving it pending across a
  funnel call that saw another tab; the outermost-mutation hook clears
  whatever was not consumed (Term 5). Restore is not a cause: it runs
  outside any mutation and its policy is Activation's. Facts: a cause
  never leaks past its mutation, and survives the funnel calls of
  `TryFocusGlobalGraph` that see another tab.
- **AD-13 — The `graph_queries` artifact's table entries gain
  `summary`.** Spec §4 requires the summary byte-identical over the
  shared corpus and the 0b artifact carried none; adding the string to
  each table entry keeps 0bD-5's exclusions (no floats, no ids), costs
  one key in the schema walk, and gives both lanes the same bytes to
  compare (A-14). Both serializers change; the golden is regenerated by
  the harness; the mac twin arbitrates on its lane (AR-4).
- **AD-14 — The dirty note's survival is pinned as the observable, not
  the mechanism.** The mac parks its one shared editor buffer before
  the graph tab takes the pane (`AppState+GraphTable.swift:63, 90`); the
  Windows graph opens into a NEW tab and never touches the current one
  (`WorkspaceViewModel.cs:1946–1948`); both hosts keep the dirty text —
  GraphOpenKeepsTheDirtyNote pins that (A-13), and no divergence is
  recorded.
- **AD-15 — Rule L's load rule IS the mac's guard, transcribed with
  its timing.** The mac has one decision — load unless the tab is
  already the active group's active tab with a snapshot held
  (`:81–83`) — reached from every activation path, and evaluated BEFORE
  `openGraphTab` changes the group (`:60–70`); Terms 3 and 4 transcribe
  it into the shell's transition vocabulary (by tab, by group, already
  effective, the level before the mutation for an explicit cause)
  rather than enumerating paths, so a path the shell grows later is
  decided by the same guard.

### Recorded divergences (owner-recorded; off-limits for re-litigation)

- **A-D1 — The kind badge on `ItemStatus`** is Windows-only (A-6); the
  mac carries the kind in the Kind cell alone.
- **A-D2 — A note opened from the table speaks `OpenedFile` on
  Windows** (A-9): the shell's convention for every surface open (the
  bases'), where the mac's `openFile` posts nothing
  (`GraphTableView.swift:363`). The create path posts one
  `NoteCreated` on both.
- **A-D3 — The Windows row's Name is the P1 row copy; the mac's rows
  carry no row label** (A-6): the spec requires the Name on Windows,
  the mac grid passes no row description (`GraphTableView.swift:322–341`)
  and speaks the copy on row focus through its announcer instead; the
  cells are identical on both.
- **A-D4 — A ghost's created note is not handed to inline rename on
  Windows** (A-8): the mac installs a rename for the created entry
  (`AppState+Connections.swift:295–296`); on Windows the note keeps the
  ghost's name and the sidebar's F2 renames it.

### Mac details recorded while reading (not this issue's to fix)

- `openGraphTab` announces `GraphStatus{Opened}` on every call
  (`AppState+GraphTable.swift:72`), also when `activateGraphTab` returned
  at its same-tab guard (`:81–83`); the `AlreadyOpen` note is posted by
  Duplicate Tab (`AppState.swift:3266–3269`) and nowhere else. Windows
  follows: Open on the effective-active tab speaks `Opened` alone (rule
  L), and `AlreadyOpen` stays unused by this PR.
- A pane focus onto a group whose active tab is a loaded graph does
  not reload on the mac (`focusPane` → `focusGroup` → `enterEditorGroup`
  → `selectTab` → the guard, `AppState.swift:4350–4363, 4388–4393`);
  rule L's Term 4 transcribes that (no load BY GROUP when READY).

### Accepted risks

- **AR-1 — The FlaUI journey is CI's to arbitrate.** A local run needs
  no screen reader and serialization; the shell gate's lane is the
  oracle.
- **AR-2 — Show connections is disabled between PR A's merge and PR
  B's.** Listed, reachable, inert. PR B's first task enables it.
- **AR-3 — The §K numbers are unmeasured until the task loop.** The
  runner asserts; a miss is root-caused there, and the record carries
  the medians.
- **AR-4 — The mac lane is unrun on this box** (0bR-1) for the one-line
  consumption of `graph_table_default_sort` and the artifact's new
  `summary` key in the Swift serializer; the lane arbitrates.
- **AR-5 — The sidebar's select-path seam is new** and its behaviour
  (expand, select, focus) is pinned by a fact, not by the journey; the
  journey asserts the action is listed and reaches the seam.
- **AR-6 — The graph document has no inline mode.** Every fact runs
  under an installed, pumped dispatcher context; a fact that forgets to
  pump reads a stale publication. The primitive's own facts (A-2), the
  constructor's refusal of a missing context and the harness's
  `PumpUntil` are the guard.

### Round 1 ledger (nineteen findings; disposition by revision 2, re-verified by round 2, the remainders closed by revision 3)

| # | Finding | Disposition |
|---|---|---|
| IGA-1 | The receiver validated a replacement against the authority it replaces | Rule A: a pair validates from its ENVELOPE — filter, query AND sort against the request (revision 4, IGA-43) — and replaces in one record; rows-only validates against the held publication and the request (A-2) |
| IGA-2 | Every sort re-read the snapshot | Pair vs rows-only requests; a sort fetches rows only; call-count facts (A-2, A-5) — discharged |
| IGA-3 | Attach before liveness; synchronous mode inverted the open sequence; `ReopenedGraph` duplicated | Attach seats only; rule L's follow method with Terms 3–7 (revision 4: the cause scoped, the mac's guard, the mac's reopen order, one never-inline primitive) |
| IGA-4 | `rowAudioDescription` is not the UIA row Name | `rowAutomationName` and `rowItemStatus` seams; `RenderLabel` (A-6, AD-2) — discharged |
| IGA-5 | `HandleFileChange` misses scan-finished; inline reentry hazard | The index-phase arm; the always-async primitive and the call-stack sentinel (A-2, A-3) — discharged |
| IGA-6 | Retirement not connected to the teardown drain; announcer not shut down | RetireGraphDocument; ShutdownGraphDocument in `Dispose`'s drain (A-1, A-10) — discharged |
| IGA-7 | External sorting produced no announcement | `GridSorted` once on adoption (A-5, AD-9) — discharged |
| IGA-8 | Per-row action fetch; the grid takes one list | Three vectors once per document, unioned, `IsVisible` per row (A-8) — discharged |
| IGA-9 | `CreateReporting` is not the shell's complete create seam | The two-phase seam with typed outcomes, ADDRESSED to the graph's pane, opened first, announced once after the attempt (revision 4, IGA-41) |
| IGA-10 | The production scheduling path unpinned | Theories over both modes (A-2, A-10, A-14) — discharged |
| IGA-11 | Selection cleared against the rows | Revalidated against the record's node-key set (A-7) — discharged |
| IGA-12 | Bind's restore overwrote `SelectedKey` | The syncing guard (A-7) — discharged |
| IGA-13 | Ghost activation and modified activation misread | The create funnel; `rowActivatedModified` (A-9) — discharged |
| IGA-14 | `OpenPath` announces nothing; A-D2 false | OpenGraphRowFromSurface with its `after` event (A-9) — discharged |
| IGA-15 | The view state's shape unpinned | The five fields, defaults, ownership, the no-shadow census (A-1, A-17) — discharged |
| IGA-16 | 10k virtualisation evidence missing | The live-container fact under Standard virtualisation (A-15) — discharged |
| IGA-17 | Failure publication contradicted itself and the mac | Two failure arms: pair → ERROR without a snapshot; rows-only → READY stands, the sort rolls back, the High event (A-2, A-4) — discharged |
| IGA-18 | Drifted line ranges; the census cannot see them | Corrected; the census's scope recorded (A-17, AD-10) — discharged |
| IGA-19 | A-D4 was not a divergence | Moved to AD-11 — discharged |

### Round 2 ledger (sixteen findings; disposition by revision 3)

| # | Finding | Disposition |
|---|---|---|
| IGA-20 | The three-site hook missed the replace arm and five activation paths | Rule L: the follow method in `SyncPanels`, which every path reaches (round 3 traced them all); the open path corrected — a NEW tab, no replace arm (revision 4, IGA-36); the cause scoped to its mutation (IGA-37) |
| IGA-21 | Reopen had to load while its only hooks were forbidden | Reopen is a cause with the mac's order: `Opened`, the shell's `ReopenedGraph`, then the summary (revision 4, IGA-38) |
| IGA-22 | The candidate snapshot carries no filter | The worker ENVELOPE carries the filter, the query and the sort it used; the pair validates all three against the request (revision 4, IGA-43) |
| IGA-23 | A dispatcher callback is serialized, not observer-atomic | One immutable GraphPublication installed in one property swap, the canvas slot's shape; the self-consistent-observer fact (rule A, A-2, AD-4) |
| IGA-24 | No tracked always-async primitive; the thread-id fact unsound | StartWorkAlwaysAsync(compute, apply): tracked through the apply, the apply on the captured dispatcher (revision 4, IGA-42); the call-stack sentinel (A-2, A-3) |
| IGA-25 | Pair and rows-only failures conflated | Two arms: pair → ERROR record without a snapshot; rows-only → nothing installed, sort rolled back, READY stands, the High event (A-2, A-4) |
| IGA-26 | `Exists` has no message; `Unavailable` means shutdown | Typed outcomes: `Exists(message)`; `Unavailable` is a retired-token drop; the busy-reason mapping removed (A-8) |
| IGA-27 | Two announcements on a ghost create; activation parity unrecorded | Open first, ONE `NoteCreated` by the workspace after the ATTEMPT (revision 4, IGA-41); `OpenedFile` on activation recorded as A-D2 (A-8, A-9, AD-8) |
| IGA-28 | The artifact test used the wrong filter and no artifact summary | The pair under the entry's filter (the harness's inclusive `all`); `summary` added to the artifact's table entries in both serializers (A-14, AD-13) |
| IGA-29 | Recycled-row facts impossible under Standard virtualisation | The seams applied per newly realized row and cleared on unload; the live-container count; "cache length", not "recycling slack" (A-6, A-15) |
| IGA-30 | The `AlreadyOpen` decision cited Duplicate Tab | Open on the effective tab speaks `Opened` and loads unless READY — the mac's guard, not "no load" (revision 4, IGA-39); `AlreadyOpen` unused |
| IGA-31 | Silent restore was an unrecorded divergence | Restore speaks the summary, the mac's (`activateTab` at restore); no divergence (rule L, AD-9) |
| IGA-32 | The announcer census could be bypassed by a nested same-name file | Exempt the normalised path, assert exactly one relay, scan `Graph/**/*.cs` (A-10) |
| IGA-33 | A-D3 was an implementation difference | Moved to AD-14 with the shared observable pinned (A-13) |
| IGA-34 | Non-P budgets substituted silently | Named as Windows host budgets; the spec's §K line amended (AD-7, AD-9) |
| IGA-35 | The default-sort export absent from the spec's Consumes list | Added; AD-9 records it with the core, FFI, binding and census obligations (A-5, AD-1, AD-9) |

### Round 3 ledger (seventeen findings; disposition by revision 4)

| # | Finding | Disposition |
|---|---|---|
| IGA-36 | Rule L was built on a current-tab `OpenGraph` path that does not exist | `OpenGraph()` opens with the new-tab target, always; the replace-arm and dirty-fallback facts removed; the dirty note keeps its tab by construction (what stands today, A-1, AD-14) |
| IGA-37 | Clear-on-read consumed the cause at the wrong funnel call | The cause is mutation-scoped: consumed only at the graph's own transition, pending across a call that saw another tab, cleared by the outermost-mutation hook; Restore is not a cause (Term 5, AD-12; revision 5, IGA-59) |
| IGA-38 | The reopen order was impossible and not the mac's | `Opened`, the shell's `ReopenedGraph` at its existing sites, then the summary from the receiver — the mac's `:3612–3614`; the failure arm; no summary for a shown-and-loaded tab; the apply never on a worker (Term 6, A-10; revision 5, IGA-53/57) |
| IGA-39 | "Opened alone when effective" blocked recovery from ERROR | The mac's guard transcribed: no load only when READY; LOADING, ERROR and no-publication restart the pair (Term 4, AD-15) |
| IGA-40 | A probe dropped during an in-flight pair lost a later generation | A changed generation issues a superseding silent pair; a high-water mark while no READY publication is held; the gated fact (A-3, AD-5) |
| IGA-41 | Row actions unaddressed to the graph's pane; a landed create could be silent | Every action captures its tab and group and activates them at invocation; the create completes under the workspace's lifecycle generation, independent of the graph's liveness; `NoteCreated` after the attempt regardless; empty content; the rename divergence (A-8, A-9, AD-8, A-D4; revision 5, IGA-55/56) |
| IGA-42 | The always-async primitive lacked dispatcher-side, drainable completion | StartWorkAlwaysAsync(compute, apply): the apply always on the serialized owner context, the tracked task complete after it, the drain a fixed point; no inline mode (A-2, AD-4, AR-6; revision 5, IGA-53/60) |
| IGA-43 | The envelope validated only the filter and generation | Query and sort validated against the request in both receivers; three more injected-mismatch facts (rule A, A-2) |
| IGA-44 | The mac's VISIBLE level was missing | Three levels: LIVE, VISIBLE (the probe), EFFECTIVE (speech) (Term 2, A-3) |
| IGA-45 | Pane focus does not reload on the mac; AR-6 deferred a known fact | A transition BY GROUP loads only when no READY publication is held; the mac path cited; AR-6 replaced (Term 4, mac details) |
| IGA-46 | "The Name is identical" was false | A-D3: the Windows row's Name is the P1 copy, the mac's rows carry no row label; A-D1's last sentence deleted (A-6; revision 5, IGA-68) |
| IGA-47 | The disappeared-path refusal fact could not be written | `OpenPathCore` opens any non-blank path; the claim removed; the deleted-path fact pins the shell's behaviour (A-9) |
| IGA-48 | The node-key set was a derived cache | Membership scans `Snapshot.Nodes`; no derived index in the record (A-2, A-7) |
| IGA-49 | `Cells[8]` re-typed the column order | The Kind cell's index from the fetched vector; a census forbids `Cells[<digit>]` under `Graph/` (A-6, A-17) |
| IGA-50 | The cited runner gated only the renderer; two workloads had no budget | The `--graph` arm captures its `Summary` and walks a pinned inventory — budgets or measurement-only per entry, a missing or unlisted report fails (A-15) |
| IGA-51 | The error label conflated visible text and accessible name | The raw message visible, "Graph error: …" as the accessible name — the mac's split (A-4) |
| IGA-52 | Three citations drifted | `graph.rs:319–330`; `CanvasPublicationSlot.cs:270–282`; `ParityHarnessTests.swift`'s `graphQueriesArtifact` `:305–308` (what stands today, A-14) |

### Round 4 — nineteen findings; THE FREEZE — revision 5 stands

Round 4 re-verified twenty-six items and discharged twenty-one; it
returned nineteen findings, six blockers — IGA-53, 54 and 56 created by
revision 4's text, IGA-55, 57 and 58 present in revision 3 and missed —
and its own verdict was to freeze under the recorded precedent. Rule 5
for the third consecutive round; the precedent applies: revision 5
corrects the text for every finding below and freezes, and the task
loop discharges the ledger by code, each row's record naming the fact
that pins it. Precedent applied; the owner may overrule.

| # | Finding | Disposition |
|---|---|---|
| IGA-53 | The no-context apply raced the workspace mutation | The apply ALWAYS runs on the serialized owner context; every fact under an installed, pumped dispatcher; no inline mode; the constructor refuses a missing context (Term 7, A-2, AD-4, AR-6) |
| IGA-54 | The mac's guard mistranslated for an explicit Open across groups | Term 4 is cause- and pre-mutation-aware: an explicit Open or Reopen loads whenever the level before the mutation was not EFFECTIVE; BY GROUP under Activation loads only without a READY publication (Term 4, AD-15) |
| IGA-55 | A landed create had silent exits after the graph retired | The completion is the workspace's under the lifecycle generation, independent of the graph's liveness: barrier, refresh, the open when the address is current, `NoteCreated`, the caveat (A-8, AD-8) |
| IGA-56 | Addressed asynchronous completion stole focus | The address is activated at INVOCATION; a completion never activates; a stale address suppresses the visual open and completes anyway (A-8, A-9) |
| IGA-57 | Reopen of a shown-and-loaded graph promised a summary no load produces | `Opened`, `ReopenedGraph`, no load, no summary for the effective READY tab; the spec's item 7 amended and AD-9 records the reopen line's position (Term 6, A-10, AD-9) |
| IGA-58 | The sort no-op guard was half the mac's | No-op only while no request is pending; a pending request is superseded; the A → pending B → A fact (A-5) |
| IGA-59 | Restore never reached the cause-clearing boundary | Restore is not a cause; the restore's funnel call is an Activation BY TAB (Term 5, AD-12) |
| IGA-60 | The drain was not a fixed point over apply-triggered retries | The document's drain re-snapshots until the tracked set is empty (A-2, AD-4) |
| IGA-61 | GraphTabNotAddressableByPath asserted behaviour Windows does not have | The twin asserts resolution never yields the graph tab and the token opens an ordinary missing-file tab (A-13) |
| IGA-62 | The unindexed caveat preceded `NoteCreated` | The landing's halves split: barrier and refresh, the open, `NoteCreated`, then the caveat (A-8) |
| IGA-63 | "Each assignment is a funnel call" was wrong | A funnel call when the reference changes; the state-dependent counts recorded and not asserted; the load count asserted (what stands today, Term 5, the facts) |
| IGA-64 | "Exact" sequences omitted the shell's own events | Every sequence is a projection onto the graph family plus `ReopenedGraph`; `TabFocused`, `TabClosed` and the pane line are the shell's (Term 6, A-10) |
| IGA-65 | The no-shadow fact contradicted the envelope and publication | The census forbids MUTABLE shadows; immutable request, token, envelope and publication copies are by design (A-1) |
| IGA-66 | The cited production arm installed no dispatcher | `CanvasPresentationEngineTests.WithPumpedContext` and `PumpUntil` cited and reused (what stands today, A-2) |
| IGA-67 | Two refusal facts had no production witness | The refused-Open fact removed; the dead-address create path made real and pinned (the facts, A-8) |
| IGA-68 | A-D1 still said the Name is identical | The sentence deleted (A-D1) |
| IGA-69 | The cell-index census was bypassable | One cell lookup keyed by column; a census on every other `Cells` read under `Graph/`; the reordered-vector fact (A-6, A-17) |
| IGA-70 | The workspace-post rationale assumed the graph always retires | The rationale is liveness-independent delivery; the existing-missing-file-tab fact (A-8, AD-8) |
| IGA-71 | The artifact writer's line drifted | `SurfaceSerializer.cs:1430–1462` (what stands today, A-14) |

### Task loop — records (PR A)

**TGA-0 — Baselines.** `main` at d457916 (PR 0b merged at c35c529);
GRAPH_QUERY_SURFACE twenty-three names; the artifact's table entries
without a summary; the Windows shell without a `Graph/` directory,
`ChordScope` ending at `Canvas`, `IsPlaceholder` true for Graph;
`AccessibleDataGrid.Bind` with eight parameters and one activation
callback; `PanelWorkScheduler` with `StartWork` (inline when synchronous)
and a one-shot drain; the mac's default sort typed at three sites.

**TGA-1 — Core, the FFI, the artifact (A-5, A-14; AD-1, AD-13).**
`table_default_sort()` in `graph_queries.rs` beside `GraphTableSort`'s
`Default`, the name in the surface list (twenty-four), the free
`graph_table_default_sort()` with a `From<core GraphTableSort>` mirror in
`lib.rs`; the core fact `table_default_sort_is_the_default` and the
surface facts' counts raised on both sides (`GraphQuerySurfaceCensus`
twenty-four); the mac's `AppState.swift:2981`, `AppState+GraphTable.swift:135,
183` call `graphTableDefaultSort()`. Both serializers write `summary` per
table entry — the snapshot's `audio_summary` under the entry's inclusive
filter, fetched once before the loop — and the golden regenerated with
only `graph_queries.json` changed ("7 notes, 17 links. 1 orphans, 4
unresolved targets. Filtered." — the inclusive filter is not the default,
so the summary says Filtered); the example regenerated. Gates: 28 core
facts, the six FFI tripwires, clippy, the bindings carry
`GraphTableDefaultSort`, ten Windows census facts (`ParityHarnessCensus`,
`GraphQuerySurfaceCensus`, `BindingSurfaceCensus`). Commit 79687e1.

**TGA-2 — The scheduling primitive (A-2; AD-4; IGA-42, 53, 60).**
`StartWorkAlwaysAsync(compute, apply)`: the compute through the private
`TrackWork` and `RunIfLive` on the pool in every mode, the apply posted
to the owner context with a completion the tracked task awaits, so the
drains wait for the apply; `WhenAllWorkDrained` re-snapshots until the
tracked set is empty; a second constructor takes the owner context by
name, and `HasUiContext` says whether one was captured. Five facts in
`PanelWorkSchedulerTests` — the compute never on the calling thread (a
theory over both flags), the apply on the context's thread under a
pumped DispatcherSynchronizationContext, the tracked task complete
after the apply, the fixed-point drain following work an apply enqueued,
shutdown stopping both halves. Commit ed786c9 (with TGA-3).

**TGA-3 — The substrate seams (A-6, A-9; AD-2).** `Bind` gains
`rowAutomationName` and `rowItemStatus`, applied in `OnLoadingRow` and
cleared by a new `UnloadingRow` handler (Standard virtualisation: a
container is discarded, never reused), and `rowActivatedModified`;
Enter and double-click reach one `ActivateCurrentRow(modified)` seam
that reads Ctrl from the keyboard device and falls back to the plain
handler when no modified one was bound. Two facts in
`AccessibleDataGridTests` (the seams follow realized rows and clear on
unload, in a shown window; the modified activation and its fallback);
the thirty-four existing facts stand.

**TGA-4 — The graph directory and the shell (A-1..A-4, A-7..A-13).**
`Graph/GraphViewState.cs` (the five fields; the default filter is
core's `GraphFilter::default()` — notes and unresolved targets in,
attachments out — a first draft typed ghosts out and three facts found
it), `GraphPublication.cs` (the immutable record with `ContainsNode`
scanning the snapshot's nodes, `FromPair`, `WithRows`, `AsPairFailure`),
`GraphAnnouncer.cs` (the four classes in the canvas announcer's shape,
`RenderLabel`, `Shutdown`, `PendingForTests`), `GraphDocumentViewModel.cs`
(the token with the document instance and the session reference, the
envelope, the receiver's four steps at dispatch time, the two failure
arms, the probe superseding on a changed generation and holding a
high-water mark otherwise, the three action vectors fetched once and
unioned in `GraphRowAction`'s order, the one cell lookup by column, the
row copy and `RowName` through `RenderLabel`, `Retire`; the residue
`AnnouncerForTests` and the named seams `AnnounceStatus`,
`AnnounceIfEffective`, `GridRelaySeam`, `RelayGridEvent`),
`GraphTableView.cs` (core's columns from the vector, the records as
rows, the external sort as a rows-only token, `GridSorted` once on
adoption, the row seams, the union list of actions with per-kind
visibility, the syncing guard around bind and re-seat, currency cleared
without writing the key), `GraphSurfaceView.cs` (the switcher from the
vector with Diagram disabled, the four states with the mac's visible
text and accessible names), `Graph/WorkspaceViewModel.Graph.cs` (the
one document, `AttachGraphDocumentTo` seating only,
`GraphFollowActiveTab` in `SyncPanels` with the cached classification —
level and tab object — and the cause consumed at the graph's transition
or an explicit Open on the effective tab, `ClearGraphCauseAtMutationBoundary`
in the outermost hook, `ReleaseGraphDocumentIfUnreferenced` beside the
canvas sweep at both tab-set boundaries, `ShutdownGraphDocument` in
`Dispose`'s drain, `NotifyGraphOfVaultChange` gated VISIBLE,
`OpenGraphRowFromSurface` activating the graph's address first and
posting `OpenedFile`, the create funnel on the workspace-owned
`GraphNoteCreationWorker` completing under the workspace's life with the
open when the address is current and ONE `NoteCreated` after the
attempt, then the caveat; `OpenGraphCommand`). The shell: the tab's
`Graph` property, `AttachGraphDocument`, `IsGraph`, `IsGraphVisible`,
`IsPlaceholder` minus Graph; `SyncPanels` ends in the follow method;
`OpenGraph()` sets the Open cause; `ReopenClosedTab` sets Reopen for a
graph record; the funnel's graph arm; the vault lifecycle's index-phase
arm marshalled and `HandleIndexPhase` probing on `ScanFinished`, the
file-change arm probing beside the bases; the sidebar's
`ISurfaceNoteCreator` implementation (explicit for `TryCreateNote` —
the canvas seam shares the parameter list — with `Exists` carrying the
vault's message) and `SelectPathFromSurface` with a pending selection
consumed at tree publication; `FileManagement/SurfaceNoteCreation.cs`;
the template hosts `GraphSurfaceView`; `ChordTable`'s `Ids.GraphOpenTab`,
`ChordScope.Graph`, `GraphRows()`; the registrar's resolver;
`ChordTableTests`' scope disposition for Graph; `chords.json`
regenerated with the row and its delivery evidence (group `graph`, the
command, issue #746); `generate-parity-matrix.py`'s
W6_2_DELIVERED_COMMANDS and the issue; the parity matrix row ✓;
`w_c_matrix.md`'s "Graph table (W6-2 PR A)" row with the census that
pins it.

**TGA-5 — The facts (A-1..A-16).** `GraphDocumentTests` (twenty facts
over the graph vault through the real workspace under the pumped
dispatcher: the fresh open's sequence and one load, Opened alone on the
effective READY tab, the summary alone on a tab switch, the retirement
with a fresh document on reopen, a result for a retired document
installing nothing, the stale sequence dropping, a sort rows-only with
the mac's whole no-op guard, the one-record observer, the probe's silent
reload and its no-op on an unchanged generation, the hidden graph
unprobed, the generation arriving during an in-flight pair ending in the
newer publication, the selection surviving a reorder and clearing only
against the snapshot, the three action vectors once and unioned, plain
activation replacing the graph tab and posting `OpenedFile` through the
workspace, modified activation in a new tab with the graph standing, a
ghost's create — off the dispatcher, empty content, landed, opened, one
`NoteCreated` after, no `OpenedFile` — a create landing after the graph
closed still completing with the open suppressed, `Exists` announcing
the High event with its message, and the §W-A comparison: all sixteen
table entries under the artifact's inclusive filter, every cell but
Modified, the total and the `summary` byte for byte); `GraphTableTests`
(seven facts on an STA thread under the pumped dispatcher: core's
headers in order with Note as row header and the records as rows, the
header sort as a rows-only token with `GridSorted` once on adoption and
none for a no-op, the realized row's Name as the corpus copy and its
ItemStatus the Kind cell with the summary region named "Summary: …",
the cell lookup by column under a reordered vector, the switcher from
the vector with Diagram disabled, the states' labels, and 10,000 rows on
the real substrate with live containers bounded while paging and the
action inventory at three); `GraphAnnouncerTests` (five: latest-wins,
the immediate post and the High drop, the relay's priority,
`RenderLabel` posting nothing, Shutdown dropping a pending line);
`GraphAnnouncerCensus` (six: no source announces outside the relay,
the relay is the one file that renders, every grid rides the relay, the
document's load has one caller — `Graph/WorkspaceViewModel.Graph.cs:GraphFollowActiveTab`
— only the lookup reads `Cells`, no header typed, no mutable shadow);
`AnnouncementSeamCensus.NoGraphCodeReachesTheAnnouncerExceptThroughTheBoundary`
(the residue `AnnouncerForTests`, the four named seams each hit);
`WcMatrixGraphEvidenceCensus` (three); the five W1 twins
(`GraphTabLoadsSnapshot`, `GraphTabNotAddressableByPath`,
`RefreshProbeGatedOutWhenLifecycleAdvanced`, `RefreshGateClosesWithLastGraphTab`,
`GraphOpenKeepsTheDirtyNote`); the Rust twin
`the_windows_graph_coalescing_switch_matches_the_pinned_class_list`
(four classes, both directions); the FlaUI journey
`GraphSurfaces_TableSortSelectionAndActivation_AreClean` (the palette
row, the switcher with Diagram disabled, the nine headers, the summary
region, the first row's Name and ItemStatus, Ctrl+Alt+S sorting the Note
column ascending, axe `graph-table`, Enter replacing the graph tab with
the note) — CI's shell gate arbitrates it (AR-1).

**TGA-6 — §K (A-15; AD-7).** `GraphOpenBenchmarks` in the benchmarks
project with the `--graph` arm capturing its `Summary` and walking the
pinned inventory of six (workload, notes) entries — the two
open-to-publication budgets, four measurement-only. Measured on this box
(`--configuration Release -- --graph --validate-budgets`):
the snapshot under the default filter 1.053 ms at 1k and 13.618 ms at
10k; the rows under the default sort 5.831 ms and 80.121 ms (the nine
core-formatted cells per row, the projection's cost by design); the
document's open to its first publication 7.522 ms against 100 ms and
98.069 ms against 500 ms — both PASS, the runner's exit code 0. Recorded
in `BENCHMARKS.md` ("Milestone W6-2 PR A — graph through the C#
binding").

**TGA-7 — Mutations.** Twenty-six mutations, each byte-restored, each
caught by the fact or census named for it. The first pass caught
twenty-one; five survived because their facts were too weak, not
because the code was wrong, and the facts were strengthened until each
mutant died: an identical repeated request (only the sequence can tell
the two apart — one install), the high-water mark exercised while
nothing is held (a document of its own, the gate armed before its first
pair, a probe from the worker), the selection under a name-query
overlay that empties the rows while the snapshot keeps the node, a
sort reversal to the accepted sort (no `GridSorted`; the first mutant
was EQUIVALENT — the answered-request flag already implies the accepted
sort changed — and was replaced by one that relays on every install),
and a slow chained body under the one-shot drain. The mutants: the Opened dropped, an Open
of the effective tab reloading, a tab switch loading nothing, the
announcer kept alive at retirement, a stale sequence installing, a sort
fetching a pair, half the no-op guard, the probe ignoring a changed
generation, a hidden graph probed, the high-water mark dropped, the
selection revalidated against the rows, a fourth action crossing, the
`OpenedFile` silenced, the create never opening, a landed create dropped
after the graph closed, the pair fetched under the default filter
instead of the request's, `GridSorted` on every publish, the row Name as
the bare label, the Diagram item enabled, a typed cell index, a typed
header, a second load caller, the grid's default seam kept, the unload
keeping the name, a one-shot drain, and the Graph scope scraped from
nothing.

**TGA-8 — Gates and the verified details.** `cargo test --workspace`:
1,997 passed, 6 failed — the five `session::tests::dir_tree` censuses
this box always fails (OS error 123; CI-Windows is the oracle, the
standing note) and `perf_guard_root_listing_under_100ms_on_10k_files`,
the wall-clock guard that fails under the 528-second workspace run and
passed in isolation in 11.16 seconds, untouched by this PR; `cargo
clippy --workspace --all-targets -- -D warnings` clean; `cargo fmt
--check` clean; `dotnet format` applied (two test files re-wrapped) and
clean; the whole Windows test project 2,124 of 2,124 after three
corrections the full run found — `chords.json` regenerated through the
projection rather than hand-edited (PINV-5; the delivery evidence is
preserved by the projection), the row's label made the mac's
byte-identical "Open Graph" (P3, `MacCatalogParityTests` — the frozen
text's "Graph: Open Graph" was wrong), and the matrix cell's "until PR
D" reworded because the staged-claim census reads "PR D" as the
canvas's shipped PR D; the accessibility test project builds with the
journey; the benchmarks project builds in Release. Verified during
implementation, recorded here as the frozen text's discharge: (i) the
document does not REFUSE construction without a context (A-2's
sentence) — it installs the constructing thread's dispatcher context as
its owner when none is current, the deterministic single-thread context
the round-4 ledger's IGA-53 fix names, so every existing workspace fact
keeps constructing graph tabs and every graph fact pumps that
dispatcher; (ii) a rows-only publish speaks the filter count
(`GraphFilterCount`, the mac's `requestGraphTableRows` `:343–347`),
coalesced, a mac behaviour the section did not pin — pinned by the
sort facts' announcement sets; (iii) the create's two outcomes are
posted from the one workspace site AD-8 names — `NoteCreated` and, for
`Exists`/`Failed`, `NoteCreateFailed` — so a failure that lands after the
graph retired is spoken too; (iv) the view state's default filter is
core's `GraphFilter::default()` with unresolved targets IN; (v)
`ISurfaceNoteCreator` is the generalisation beside `ICanvasNoteCreator`,
both implemented by the sidebar, the canvas untouched (the contract's
"generalised from" read as a sibling rather than a rename); (vi) the
command row's label is the mac's "Open Graph" byte for byte (P3), not
A-12's "Graph: Open Graph", and the journey looks for that; (vii) the
document's `IsRetired` guard in the receiver and the announcer's
`Shutdown` in `Retire` are what make a result for a retired document
install nothing and speak nothing (A-1's facts).

**TGA-9 — The post-implementation pass, and the journey's failure it
explained.** The standing gate found what TGA-8 had not run: the shell
accessibility gate failed on `ed92f9d` at the journey's Enter step —
"Enter on Alpha did not replace the graph tab with the note" — on CI and,
reproduced before any change, on this box in 18 seconds, deterministic
both times (TGA-8's "builds with the journey" was exactly that: built,
never run to its last step). A codex pass at xhigh over the branch's
diff against the frozen text (brief: the contracts most severe first,
then races and thread affinity, then vacuous facts and evadable
censuses, then unrecorded mac divergences, then TGA-8's deviations)
returned thirteen findings, IPA-1..13 — five blockers, seven majors, one
minor — and the first named the journey's cause. Every one was verified
against the code before a line changed; none was refuted. Discharged by
code, most severe first: (IPA-1, A-9/A-16) `ReplaceItem` cleared
`Base`/`Dashboard`/`Canvas` and `NotifyItemChanged` raised every surface
predicate EXCEPT `IsGraph`/`IsGraphVisible`, so a note opened into the
graph's tab left the graph surface Visible over it — the tab now drops
`Graph` and raises both, pinned by a W1 twin that opens a row's note over
the graph tab and asserts the predicates, the notifications, the null
document and the retirement, and by the journey itself, green locally
after the fix; (IPA-2, A-1) `CloseActivePane` swept bases, dashboards
and canvases but not the graph — the release sweep runs there too,
pinned by a twin that closes the pane holding the last graph tab with a
probe in flight and reopens a fresh document; (IPA-3, Term 6) a reopen
whose graph tab was already effective changed no reference, so the
follow method ran only at the outermost boundary, AFTER the shell's
line — `ReopenedGraph`, `Opened`, the frozen sequence reversed; the
reopen arm now calls the funnel (`SyncPanels`) before posting the line,
and three facts over a merged timeline pin the effective READY case
(`Opened`, `ReopenedGraph`, no load), the hidden-tab case and the
recreated-tab case (`Opened`, `ReopenedGraph`, summary); (IPA-4, A-8/A-9,
IGA-41/56) only Open activated its address — Reveal invoked the sidebar
seam directly and Create merely captured the tab; both now run through
`FocusGraphAddress` at invocation (`RevealGraphRowFromSurface`; the
create's invocation), pinned by two-pane facts in which the graph is
VISIBLE in the pane that is not the active group and each action makes
that pane active first, the landed note opening there; (IPA-5, Term 7)
`RunAlwaysAsync` checked `_isShutDown` before `Task.Run` and never
again before the compute — the window `RunIfLive` closes for `StartWork`
— so the compute now runs under a liveness check ON THE POOL and a
refused compute applies nothing, pinned by two scheduler facts through
a pool-side seam (`BeforeComputeForTests`): a shutdown between queueing
and pickup refuses the compute; a shutdown after the compute skips the
apply; (IPA-6, rule A/AD-8) the token carried no lifecycle generation and
the create's completion was keyed by the worker's life alone — the
lifecycle now hands the workspace its counter (`LifecycleGeneration`,
`_generation` at `InitializeWorkspace`), the document reads it into
every token and compares it at step (i), the create captures it at
invocation and its completion drops whole when it moved, and the
envelope's own consistency (filter against query) is validated at step
(ii) before EITHER arm; pinned by a bare-document fact whose gate
advances the generation between the crossings and the dispatch (nothing
installs, nothing speaks, the next token publishes) and a create fact
whose creator advances it (the file lands, no bookkeeping, no open, no
event); (IPA-7, AD-8) the one posting site outside `Graph/` sat inside
`Graph/WorkspaceViewModel.Graph.cs`, the announcer census saw no bare
`_announce(...)` and the seam census exempted the relay by basename —
the completion now lives in the root partial
`WorkspaceViewModel.GraphCreate.cs`, the announcer census treats any
`A11yEvent.Graph` constructed under `Graph/` outside the relay as a
posting site, the seam census exempts the relay by its normalised path
and asserts the root partial's `GraphNoteCreationCompleted` is the ONLY
site in the shell that constructs a graph-family event; (IPA-8) the
pumped harness observed only `IsCompleted` — `PumpUntilDrained` now
rethrows a faulted or cancelled drain; (IPA-9, A-4/Term 6) the
pair-failure fact said in its own text that no failure was injected —
it now throws from the fetch gate after the crossings, asserts ERROR
without a snapshot and `LoadFailed` where the summary would be, then an
explicit Open showing LOADING, `Opened`, and the summary on READY; (IPA-10,
A-3) the supersession fact let the pair apply before the probe — the
pair now PARKS in the worker on a gate while the world moves to G2 and
the probe issues the superseding pair against the held G1 snapshot, and
the fact asserts exactly one install, the superseding pair's, with the
stale G1 result dropping at step (i); (IPA-11, A-6) the reordered-vector
fact never handed the vector to the document — a test seam
(`ReplaceColumnInventoryForTests`) does, the lookup answers index 0 for
Kind and returns the cell AT that index, and the census now requires
`CellOf` to index through `CellIndexOf` and forbids a literal element
index in either helper; (IPA-12, A-15) the runner's `OpenToPublication`
could pass a failed or unfinished load — it now fails on timeout,
observes the drain, and requires a READY held snapshot carrying the
synthetic vault's whole population (the notes plus one ghost per
hundred); (IPA-13, A-12) the matrix generator had no W6-2 branch, so the
delivered row fell through to W1's 2026-07-20 status — a `W6_2_STATUS`
dated 2026-09-03 routes the command and the issue, and the matrix is
regenerated. Mutations: 39 in the sweep, the twenty-six of
TGA-7 plus thirteen for this pass (each fix reinstated, each new fact
failing against it — the visibility notifications, the document kept on
replace, the pane sweep, the reopen funnel call, the unaddressed reveal
and create, the pool-side check, the unchecked generation in the token
and in the completion, a posting under `Graph/`, the failure arm, the
stale sequence against the supersession fact, a literal cell index),
39 caught. Gates: 2135 Windows facts on a fresh
build; the journey passed locally in 9 seconds after the fix (18 seconds to the same failure before it); `dotnet format` clean; the benchmarks and
the accessibility projects build; the Rust and mac lanes untouched by
this pass.

**TGA-10 — The standing gate on `1de19b4`, and the second codex pass.**
Three things arrived on the new head. Codoki re-reviewed the whole PR
and COMMENTED with one Medium finding on `GraphTableView.cs:75`: when
`Model` becomes null the grid keeps the previously bound rows and the
delegates that captured the old document, so a retired document stays
reachable — a consequence of IPA-1's fix, since before it the tab never
dropped its document on an in-place replacement; verified and accepted:
the null arm now rebinds the grid to an empty column and row set beside
silencing `Announce`, pinned by a GraphTableTests fact that nulls the
model and asserts an empty grid. The Windows app lane failed at
`GraphTableTests.TenThousandRowsStayVirtualisedAndTheActionInventoryStaysThree`
— "STA test body timed out" at the harness's 120-second `Join` — a fact
that passed on `ed92f9d`'s run and passes here: measured on this box it
took 1 minute 50 seconds, ten seconds under the budget. Not a flake: a
fact with no headroom, dominated by ten thousand FILES written and
scanned rather than by the grid it measures. The vault is now
ghost-heavy — a hundred notes each linking to a hundred unresolved
targets, ten thousand ghost rows for a hundred files — and the fact runs
in 2 seconds, where the ten-thousand-file vault needed 110 on this box. The second codex pass at xhigh over the fix commit returned
ten findings, IPB-1..10 (three blockers, five majors, two minors), each
verified; nine discharged by code and one recorded: (IPB-1, rule A) the
workspace's constructor restores a persisted graph tab and starts its
first load INSIDE the constructor, before the lifecycle installed the
generation provider, so a restored graph's token carried 0 and was
dropped at dispatch against the real counter — a regression IPA-6
introduced; the provider is now a CONSTRUCTOR input (`lifecycleGeneration`,
handed in by `InitializeWorkspace`), pinned by a fact that persists a
graph tab, constructs the workspace under a non-zero counter, and
asserts READY with the summary alone; (IPB-2, Term 7) the pool-side
check was a plain read — admission and the shutdown flip now share the
work lock, one transition: a compute admitted before the flip is drained,
one that reads the flag set never starts (the residual — a compute that
started before the flip runs to completion — is the bounded pre-session
drain's purpose, A-1); (IPB-3, A-1/A-2) the tracked task completes after
the posted apply, and the workspace's teardown blocks the dispatcher
while it drains, so a body between compute and apply could never
complete and every close during graph work waited the full bound —
`Shutdown` now SETTLES every pending apply (the apply is refused after
shutdown anyway) and refuses to post a new one, pinned by a scheduler
fact whose apply is queued on a thread that never pumps and by a
workspace fact that parks a pair, disposes on the owner's thread without
pumping, and asserts the teardown returned within seconds with nothing
installed; (IPB-4, A-8/A-9) the frozen text's immutable `(group, tab)`
address handed through every seam versus the code's resolution of the
singleton's tab and group at invocation — RECORDED as deviation (viii)
below rather than built, and the two facts codex asked for added: the
reveal captures the active group AT the seam's call, and a create parked
while the reader moves to a note completes with the open suppressed and
`NoteCreated` spoken; (IPB-5, A-17) the censuses' textual shapes — under
`Graph/` no `using` alias exists and every call on an announce delegate
passes exactly one explicit construction of a non-graph shell event, the
document's seam exemptions match by normalised path like the relay's,
and across the shell an alias naming the family or a target-typed `new`
handed to an announce delegate is an offender; the residual that a
pre-built graph event handed by identifier outside `Graph/` is unseen is
stated here — a semantic census is a later slice; (IPB-6, A-3) the
supersession fact now waits for the pair to REACH the gate before the
world moves, so the parked envelope is stale by construction; (IPB-7,
Terms 4/6) the hidden and recreated reopen facts assert exactly one load,
and the reopen's failure arm is pinned — `Opened`, `ReopenedGraph`,
`LoadFailed`; (IPB-8) the scheduler facts capture one drain, pump it to
completion and observe it; (IPB-9, A-15) the runner requires the
snapshot's node count and the publication's total to equal the synthetic
population, not only the rows; (IPB-10, A-1) the pane-close fact parks a
pair over a changed filter — a result that WOULD install — before the
close, and asserts nothing installed after the release. Deviation
(viii): the address of contracts A-8/A-9 is resolved by the workspace at
invocation as the singleton graph tab and its owning group
(`FocusGraphAddress`), activated then, and captured for the completion's
comparison — one graph tab exists at most, so the immutable record the
frozen text names collapses to that resolution; a completion never
activates. Mutations: 46 in the sweep (7 for this pass —
the provider not injected, the pending applies left unsettled, the post
after shutdown admitted, the null model keeping its rows, a `using`
alias under `Graph/`, the retirement skipped at the pane close); the
first run left two SURVIVORS, both weak facts rather than sound code —
the restore fact accepted a workspace agreeing with its own constant
(it now asserts the counter is the lifecycle's and that a token carries
it, 7 then 8) and the teardown fact reached the post refusal rather than
the settle (a second teardown fact queues the apply BEFORE the teardown
blocks the thread) — and the survivors' mutations were re-run against
the strengthened facts: 46 caught.
Gates: 2142 Windows facts on a fresh build; the journey passed locally in 8 seconds;
`dotnet format` clean; the benchmarks and the accessibility projects
build.

**TGA-11 — The standing gate on `c0eaae1`, and the third codex pass.**
Codoki re-reviewed the whole PR on the new head and APPROVED it: "Codoki
auto-approved this PR (no issues found)". The Windows app lane failed
once more at
`GraphTableTests.TenThousandRowsStayVirtualisedAndTheActionInventoryStaysThree`,
now within a minute of suite time rather than at the harness's budget:
"417 live containers at the end" against the fact's literal bound of
400, after every page of the scroll had stayed under it — a bound this
box satisfies and the runner does not, because the live count under
Standard virtualisation is a multiple of the rows the viewport holds,
which the runner's row metrics make larger. The fact's claim is that the
live count stays BOUNDED and never approaches the row count; the literal
was an artefact of one machine. The bound is now a tenth of the
population at every page and at the end, beside the unchanged
assertions that the first page realises fewer than two hundred containers
and that fewer rows than the population were ever realised. The third
codex pass at xhigh over the second pass's commit returned no blocker,
three majors and two minors, IPC-1..5, each verified and discharged:
(IPC-1, Term 7/AD-4) the pending apply had no single owner — the promise
was registered under the lock but posted outside it, and the callback's
liveness read was unlocked, so a shutdown could settle a promise while
its apply ran or a callback could be posted after the flip; the promise
is now QUEUED under the work lock (registered and posted as one
transition against the flip) and the callback CLAIMS it under the lock
before applying — shutdown settles only what is still queued, a claimed
apply runs to completion and settles itself — and the no-context arm
admits its apply under the same lock; (IPC-2) the settle facts guessed
with sleeps that the apply had been queued and never ran the late
callback — a seam under the lock (`ApplyQueuedForTests`) fires the
instant the apply is queued, both facts wait on it, the scheduler fact
then runs the late callback's frame and asserts no apply and a completed
drain, and the parked-pair teardown fact releases its gate only once the
document reports retired; (IPC-3, A-17) `using static …A11yEvent;` would
have let a bare `new Graph(…)` dodge the suffix rule and
`this._announce(…)` dodged the callee rule — under `Graph/` no alias and
no static import exists at all, and every callee is normalised to its
terminal identifier (bare, member-qualified, parenthesised) in both
censuses; (IPC-4, A-8/A-9) deviation (viii) said the owning group was
captured while the completion compared only the tab — the create now
captures the `(group, tab)` pair at invocation and its completion
compares both identities, the group still present and owning the tab,
active, with the tab its active one; (IPC-5, A-8/A-15) the 10k fact
accepted any population of at least ten thousand and read a crossing
count that was a literal — it now asserts exactly one hundred notes and
ten thousand ghosts, that both kinds were realised along the scroll, and
the crossings are COUNTED through the wrapper the document fetches its
action vectors with (`FetchRowActions`, the `graph_row_actions` key),
never assigned. Mutations added for this pass: the claim removed (the
callback applying without claiming — the settle facts), a `using static`
under `Graph/` (the announcer census), the crossings assigned rather than
counted (the 10k fact); the whole sweep re-run: 49 caught of
49. Gates: 2142 Windows facts on a fresh build; `dotnet
format` clean; the journey passed locally in 8 seconds; the benchmarks and the
accessibility projects build.

**TGA-12 — The standing gate on `fd19a05`, and the fourth codex pass.**
Codoki APPROVED the head ("no issues found") and its one inline thread
from `1de19b4` — the null model keeping its rows, discharged in TGA-10 —
was answered and resolved, so the PR carries zero unresolved threads.
The Windows app lane passed. The shell accessibility gate failed at a
CANVAS journey, W6-1's
`CanvasVerbs_GroupConnectDuplicateLinkConvertAndUndo_AreReachable` —
"the undo chain never returned to two cards after 12 chords" — with the
graph journey and the other forty-one green; the journey passes here in 44 seconds, and nothing in this PR's diff reaches the canvas undo chain (the shared scheduler's changes are confined to shutdown and the always-async primitive, which the canvas documents do not use), so it is not called a flake but not root-caused in this record either — the gate re-runs on this push and a second failure on this branch would be chased with the app log. The fourth codex
pass at xhigh over the third pass's commit returned no blocker, three
majors and two minors, IPD-1..5, each verified: (IPD-1, Term 7/AD-4)
the apply's post and the test seam ran under the work lock — a context
whose `Post` runs the callback inline would apply under the lock, a
blocking one could invert with `Shutdown`, a throwing seam would fault
the tracked task; the promise is registered under the lock and posted
OUTSIDE it, the callback's claim under the lock unchanged, and a post
the owner context refuses withdraws the promise and completes the
tracked task without a fault — so TGA-10's "no callback posted after the
flip" is corrected here to what the design guarantees: a callback may be
posted after the flip and then fails to CLAIM, applying nothing; pinned
by two hostile-context facts — a Post that runs the callback inline applies once, on the worker, with no deadlock and a completed drain; a Post that throws applies nothing and faults no tracked task; (IPD-2, A-8/A-9) the `(group, tab)` address could in
principle be re-satisfied by the same objects re-seated — REFUTED as
reachable (a closed tab is disposed and never re-added, a closed pane's
group is never re-added, and the replace arm never seats a Graph item,
so a tab cannot become the graph again) and HARDENED anyway: the address
captures the document seated at invocation and the completion requires
that same document still seated and live; (IPD-3, A-15) the 10k fact's
bound was a tenth of the population where the frozen text says "the
viewport's row capacity plus the panel's cache length" — the bound is now READ from the realised panel — the substrate scrolls by item with a one-item cache each side, so the capacity is the viewport's rows plus two, and the ceiling is six times that, because WPF's Standard virtualisation cleans containers up in a deferred pass and this box shows up to 70 live over a 15-row viewport between cleanups — the first page must fit the capacity, unloading must have happened, and the panel's scroll unit is asserted so a pixel-scrolling substrate would fail loudly;
(IPD-4) the parked-pair release spun on a non-volatile field —
`_retired` is volatile; (IPD-5, A-17) `_announce.Invoke(…)`,
`_announce?.Invoke(…)` and `(this)._announce(…)` dodged the callee rule
— every delegate call is folded to its terminal identifier through
`.Invoke` and conditional access, and under `Graph/` every OTHER
reference to the workspace's delegate (captured, passed along, assigned)
is an offender, so no event built elsewhere can reach it; the outside
scan folds the same shapes. Mutations added for this pass: the refused post rethrowing (the refusing-context fact), row virtualisation switched off (the 10k fact), a pre-built event handed to the delegate's Invoke under Graph/ (the announcer census); the whole sweep re-run: 52 caught of 52. Gates: 2144 Windows facts on
a fresh build; `dotnet format` clean; the graph journey passed locally in 8 seconds; the
benchmarks and the accessibility projects build.

**TGA-13 — The standing gate MET on `cfd968a`, and the fifth codex pass.**
On `cfd968a` every CI lane passed — the shell accessibility gate
included, the canvas undo journey of TGA-12 among the forty-three green
— codoki APPROVED the head ("no issues found") and the PR carried zero
unresolved threads: the gate the program names was met on that head.
The fifth codex pass at xhigh over the fourth pass's commit returned no
blocker, three majors and two minors, IPE-1..5, each verified: (IPE-1,
A-2/AD-4) WPF's `DispatcherSynchronizationContext.Post` discards the
operation it enqueues, and a dispatcher that has shut down ABORTS the
operation instead of throwing, so a promise posted to a dead dispatcher
by a scheduler nobody shut down would await forever — the owner
Dispatcher is now captured at construction (a dispatcher context is
always constructed on its dispatcher's thread) and the apply posts
through `BeginInvoke`, refusing when the dispatcher has started shutting
down and withdrawing the promise when the operation is aborted, with
the context's `Post` kept for a non-dispatcher owner; pinned by a fact
over a REAL dispatcher run on its own thread and shut down before the
body posts — the drain completes without pumping, nothing applies,
nothing faults; (IPE-2, A-15) the fact's ceiling was six capacities —
codex's premise that WPF's default cache is one PAGE each side is
refuted by the measurement (the substrate's grid scrolls by item with a
one-item cache each side, read from the panel), but the multiplier was
unjustified; measured again, WPF's Standard virtualisation cleans
containers up neither on every measure nor on a timer — pumping for
700 ms after a jump left two pages live — but lets about four pages
accumulate before it unloads back to one, so the ceiling is FIVE
capacities, the smallest integer above the observed cadence, stated
with its reason, and a panel that never unloads exceeds it within five
pages; (IPE-3, Term 7) the inline-context fact could not see the lock's
position because a monitor re-enters — a BLOCKING context now parks the
post and a shutdown from another thread must complete while it is
parked, which it cannot if the post is made under the lock; (IPE-4) the
seam ran outside the try and a throwing seam faulted tracked work — it
is now non-faulting, its comment and the ownership comment corrected
(a callback posted after the flip fails to claim), pinned by a
throwing-seam fact; (IPE-5, A-17) the workspace's rendered-announcement
delegate escaped the reference rule — under `Graph/` `_announceRendered`
is referenced exactly once, as the argument of the root
`new GraphAnnouncer(…)`, and any other reference is an offender.
Mutations added: the aborted post left unwithdrawn (the dead-dispatcher
fact), the post moved back under the lock (the blocking-context fact),
the seam's guard removed (the throwing-seam fact), the rendered delegate
captured under `Graph/` (the announcer census); the whole sweep re-run:
56 caught of 56. Gates: 2147 Windows facts on a
fresh build; `dotnet format` clean; the graph journey passed locally in 8 seconds; the
benchmarks and the accessibility projects build. The post-implementation loop has now run five passes, each returning fewer and lesser findings than the last (thirteen, ten, five, five, five; no blocker since the second) while the gate the program names has been met on two consecutive heads. A sixth pass runs over this commit; by the standing precedent its findings, if any, are carried in the PR conversation as the owner's ledger rather than as further commits — precedent applied; the owner may overrule.

**TGA-14 — The sixth pass's ledger, discharged by the owner's word.**
TGA-13 carried the sixth pass's findings as a ledger on the PR rather
than as commits, by the standing precedent; the owner read it and said
proceed, so IPF-1..6 are discharged here by code. The gate stood met on
`61e3982` throughout — thirteen CI lanes green, codoki auto-approved on
that head, zero unresolved threads — and none of the six was a product
defect: (IPF-1, A-2) the dead-dispatcher fact shut its dispatcher down
BEFORE the body posted, so the HasShutdownStarted refusal returned
first and the `DispatcherOperation.Aborted` withdrawal the record
claimed was never reached — a second fact now occupies the dispatcher
thread inside a blocking operation, waits on the queued seam to prove
the apply was ENQUEUED behind it, and lets that operation shut the
dispatcher down, so the pending operation is genuinely aborted; the
refusal fact stays beside it as the other arm; (IPF-2, A-15) the
five-capacity ceiling had been applied to the FIRST page too, where
nothing is in flight and the frozen sentence admits no excess — the
first page and the resting count are back to the capacity plus two, the
paging ceiling is stated for what it is (an empirical allowance over the
contract's resting bound, five capacities against a measured peak near
four, not a reading of "the viewport's row capacity plus the panel's
cache length"), the stale "six capacities" comment is gone, and the fact
now also asserts row virtualisation ON in STANDARD mode, the scroll unit,
the realised cache values, that unloading happened, and that the first
row holds NO container once the reader is ten thousand rows away;
(IPF-3, A-2) the owner dispatcher was inferred from "a dispatcher
context is constructed on its dispatcher's thread", which
DispatcherSynchronizationContext does not guarantee — it is now a
CONSTRUCTOR parameter, named only by callers that know it (the graph
document when it chooses the context; the base constructor when the
context is the one current on this thread), and a context handed in
names none and posts through itself, pinned by a fact that hands in a
context targeting another thread's dispatcher and asserts the apply
lands THERE; (IPF-4) the blocking-context fact read a pool timeout as
lock inversion — a dedicated thread with an entered barrier, a release
in `finally` and a join; (IPF-5) the ownership comment still claimed
registration and posting were one locked transition and that no callback
is posted after the flip — corrected to what the code does: the post is
outside the lock, a callback CAN be posted after the flip, and it fails
to claim; (IPF-6, A-17) the rendered-delegate rule proved a spelling —
under `Graph/` no declaration may take that name, and the one permitted
reference must sit in the workspace's own `NewGraphDocument`. Mutations
added: the aborted operation left unwithdrawn, the virtualisation mode
switched to Recycling, the foreign dispatcher inferred, the rendered
delegate shadowed by a local; the whole sweep re-run: 60 caught
of 60. One mutant was withdrawn as EQUIVALENT rather than
counted: widening the first page's bound from the resting one to the
paging ceiling is undetectable on this fixture, because the page
realises sixteen containers against bounds of seventeen and
eighty-five — no test that passes today can tell the two apart, and the
policy the capacity is read from is pinned by the Recycling mutant
instead. Gates: 2149 Windows facts on a
fresh build; `dotnet format` clean; the graph journey passed locally in 8 seconds; the
benchmarks and the accessibility projects build. The loop closes here:
six passes, 13 → 10 → 5 → 5 → 5 → 6 findings, no blocker since the
second, no product defect since the fourth.

### Tests that pin PR A

- Windows facts: GraphDocumentTests (A-1, A-2, A-3, A-4, A-7, A-8, A-9,
  A-14 — the load, reorder, close-during-fetch, generation-retry and
  teardown facts under the pumped dispatcher; the primitive's own
  facts; rule L's path facts; after TGA-9: the three reopen sequences
  over a merged timeline, the addressed Reveal and Create from an
  unfocused pane, the lifecycle generation in the token and in the
  create's completion, the injected pair failure, the parked pair's
  supersession; after TGA-10: the restore under the lifecycle's
  generation, the teardown that drains without pumping, the create
  parked while the reader moved, the reopen's failure arm),
  GraphTableTests (A-5, A-6, A-11, A-15's virtualisation over the
  ghost-heavy vault; the reordered vector handed to the document; the
  null model unbound), GraphAnnouncerTests (A-10);
  PanelWorkSchedulerTests (A-2; the pool-side liveness check; the
  settled apply); `W1WorkspaceRedTeamTests` twins (A-13; the graph tab
  replaced in place, the pane close as a boundary with a parked pair);
  `ChordTableTests` (A-12); the censuses of A-17 (the graph-family
  construction under `Graph/`, the relay by normalised path, the one
  posting site, the lookup keyed by the vector); the FlaUI journey
  (A-16); GraphOpenBenchmarks (A-15).
- Rust: `graph_table_default_sort` is the `Default`; the surface list
  is twenty-four; `the_windows_graph_coalescing_switch_matches_the_pinned_class_list`.
- Mac: the suites green with the default-sort literal replaced by the
  call and the artifact twin writing `summary`.

## PR B — the Connections leaf

**Goal (spec §PR B, as SPLIT by the owner on 2026-09-05).** PR B is two
slices. THIS section is slice B1: the right pane's `connections` leaf —
in the catalogue since W1 and empty ever since
(`WorkspaceViewModel.cs:1716`) — becomes a STANDALONE local-graph
surface: core's connections tree at depth 1–3 for the note in view, the
neighbourhood summary spoken on load and depth change, the depth-one
snippets, the generation refresh, and ghost rows that create the note.
Slice B2, a following PR before PR D (whose diagram selection sync reads
the re-root seam), adds what B1 defers and records below: re-root on a
neighbour with the back stack, the table's and the Bases surfaces' Show
connections, and the selection shared with the graph document (spec
R-B). The section is FROZEN at revision 8 under the PR 0b precedent
(rule 5 for the third time — the round-7 record below); revisions 1–7
and the seven rounds are the ledger, the owner's split is BD-1, and the
owner's amendment of A-1 and A-10 is BD-12.

**Why the split, and why the amendment.** Rounds 1–3 returned 18, 19
and 24 blockers, rising, concentrated on one question: what carries the
graph's relay and selection when only the leaf is open. Frozen A-1 read
"retired with the last graph tab", frozen A-10 tied the relay's
shutdown to that retirement, and spec R-B placed the one view state on
that document. The owner split PR B (BD-1) so B1 could ship the leaf as
its own document; rounds 4–6 then found that a SECOND relay instance
contradicts A-10's "One relay" and its High flush, whatever the file
wall says (IGE-1, IGG-1). On 2026-09-05 the owner chose, of three
options, the mac's shape: ONE relay per workspace, constructed with the
workspace and shut down in its drain, shared by the graph document and
the leaf — A-1 and A-10 as amended above (BD-12). The leaf keeps its
own document and its own selection; the shared selection stays B2's
(B-D1).

**What stands today.** The leaf catalogue is static
(`WorkspaceViewModel.cs:1711–1729`); `ActiveLeaf`'s setter announces
`A11yEvent.LeafPanelShown(value.Title)` ONLY when the leaf reference
changes (`:1818`) and runs the per-leaf reveal hooks; right-pane
visibility is separate state — the field is INITIALISED true
(`:1401`), absent from the snapshot (`WorkspacePersistence.cs:66`), and
its setter posts `RightPaneShown`/`RightPaneHidden` on every flip
(`:1870–1880`) — flipped directly by `ToggleRightPaneCommand` (`:1697`),
by directional focus, which then posts `LeafPanelShown(ActiveLeaf.Title)`
unconditionally (`WorkspaceViewModel.Layout.cs:598–611`), and by the
leaf-revealing commands, which reveal BEFORE they switch the leaf and
then post their own line (Tasks Review, `:1854–1868`: `RightPaneShown`,
`LeafPanelShown`, `TasksReviewShown`; History,
`WorkspaceViewModel.History.cs:62`) where the mac switches first
(`AppState+History.swift:716`); activating an EXISTING tab posts
`TabFocused` (`Layout.cs:68–79`), closing one posts `TabClosed`
(`:298`), and closing the sole tab of a split reaches `SyncPanels`
TWICE inside one outer mutation — a null successor, then the empty
group's removal (`:298`, `:338`, `:367`); the restore writes the leaf
field directly (`WorkspaceViewModel.Persistence.cs:44`); a deleted
file's tabs are PRESERVED and marked missing, with a shell line
(`WorkspaceViewModel.cs:2135`), as the mac keeps them
(`AppState.swift:19045`). Every leaf body is a `DockPanel` in
`MainWindow.xaml` gated on `ActiveLeaf.Id`; a collapsed pane is a
retained subtree (`MainWindow.xaml:937`) where the mac's pane is
destroyed; the newest bodies host a `UserControl` with a `Model`
dependency property that resolves by reflection, so the workspace
property must be PUBLIC (`WorkspaceViewModel.History.cs:20–24`). The
active note reaches leaves through `SyncPanels()`
(`WorkspaceViewModel.cs:1759–1775`). The generic placeholder collapses
only for the leaf ids it names (`MainWindow.xaml:1944–1995`). The
right-pane focus boundary lands on the rail and posts no status
(`MainWindow.xaml.cs:624`), and outbound directional focus is
recognised only while the rail owns focus — focus inside a leaf's
content falls through to the workspace root's editor-geometry route
(`:900`); the mac has an explicit return-to-editor route
(`AppState.swift:4364`) and a persistent leaf focus anchor that works
for empty leaves (`RightPaneView.swift:291`). Off-dispatcher work rides
PR A's primitive as TGA-9..TGA-14 left it; the graph document's
announce gate is `GraphTabIsEffective()` (`WorkspaceViewModel.Graph.cs:
131`), the mac's `graphTabActive` at its publish points
(`AppState+GraphTable.swift:214`, `:343`, `:350`); the graph table's own
follow method speaks its lines when the graph tab becomes effective
(`WorkspaceViewModel.Graph.cs:147–194`); PR A's relay gates the filter
count at ENQUEUE only (`GraphDocumentViewModel.cs:511`) and fires it
unconditionally (`GraphAnnouncer.cs:151–162`), is constructed in
`NewGraphDocument` (`WorkspaceViewModel.Graph.cs:85`) and shut down by
the document's `Retire()` (`GraphDocumentViewModel.cs:633–641`) — all
of which change under BD-12.

### Design pass (protocol rule 4, reached through rule 5) — subsystem C, the leaf's lifecycle, modelled as a rule

Rounds 3–7 each returned blockers in one subsystem — what starts a load
of the standalone leaf, what supersedes it, what is spoken when — and
rounds 5, 6 and 7 were CREATED by the previous fix (rule 5, three times
running). Revision 6 traced the mac's leaf lifecycle and restated the
rule; rounds 6 and 7 found the terms incomplete in ways a hand-written
timeline table cannot close: every route of the shell crossed with
every publication, pane, leaf and root state. Revision 8 corrects each
finding in the text and, for the announcements, replaces the table as
the contract with a RULE the table only illustrates: the merged
timeline of any route is the shell's own lines for it, unchanged,
followed by the projection of the one trigger Term 3 classifies — and
the task loop pins that rule by an executable MODEL over the reachable
routes and states, not by rows (B-10).

**The mac, traced.** The right pane is DESTROYED when it collapses and
rebuilt when it shows (`MainSplitView.swift:163–175`), which destroys
every panel's view-local `@State` — the Connections selection included
(`ConnectionsPanel.swift:18`); while the pane exists, EVERY leaf is
mounted, the inactive ones hidden by opacity and `accessibilityHidden`
(`RightPaneView.swift:262–289`). The panel's `onAppear` therefore runs at
every MOUNT and loads only when the leaf is active
(`ConnectionsPanel.swift:30`); a rail switch between leaves does NOT
remount and does not load (`RightPaneView.swift:361–364`: it sets the
leaf and posts the panel line, nothing else); a note change loads only
while the leaf is active AND the panel is mounted (`:31–37`); the
leaf-revealing commands set the leaf BEFORE revealing the pane
(`AppState+History.swift:716`; `showConnectionsPanel`,
`AppState+Connections.swift:175–180`, which loads explicitly, reveals —
a second `onAppear` load when the pane was collapsed — then posts the
panel line); a depth change loads (`:162–166`); `loadConnections` with
no note in view clears the state and RETURNS before the sequence
advances (`:64–72`; the increment is `:73`), and a stale completion is
rejected against the LIVE effective path (`:104–107`); the generation
refresh loads silently at every level and in every publication state
and, sharing the one sequence, supersedes a load in flight (`:146–158`,
`:73–74`); the speech gate at publish is the active leaf alone
(`:110–114`); a same-root reload keeps the rows AND a current error
visible — the loading row shows only when the loaded path is not the
effective path (`ConnectionsPanel.swift:90–93`, `:115–125`), and
`connectionsLoading` is written but never read (`AppState+Connections.
swift:78`, `:109`); the selection is unscoped and survives a root
change, a depth change and a refresh (`ConnectionsPanel.swift:18`,
`:152`); the ghost create's completion opens after its ownership check
alone (`:278–294`); the pane-revealing helper assigns visibility
without a shell line (`AppState.swift:8864–8867`), the toggle alone
posts `RightPaneShown` (`:8855–8856`), directional entry posts
`LeafPanelShown` (`:4374–4377`) and there is an explicit return route
to the editor (`:4364`); the leaf region has a persistent focus anchor
(`RightPaneView.swift:291`); the one announcer flushes every pending
class on a High (`GraphAnnouncer.swift:183–188`) and stores the filter
count's gate with the pending line, re-checked at fire (`:150`,
`:194–207`, 0a-9). One mac detail is recorded and diverged from: a
mounted switch to the leaf after the note changed while it was inactive
shows the loading row and starts NO load (`:31–36` skipped the change;
`onAppear` does not re-fire) — Windows loads (Term 3(b), B-D11).

### Rule C — the standalone leaf on the shared relay, in ten terms

- **Term 1 — Its own document and selection; the workspace's one
  relay.** ConnectionsLeafViewModel is a `PanelWorkScheduler` subclass
  beside `GraphDocumentViewModel`, not a second instance of it: A-1's
  "at most one document" is about the graph tab's document, which B1
  does not touch. The relay is the workspace's ONE `GraphAnnouncer`
  (A-10 as amended, BD-12): constructed once, in `NewGraphRelay()` under
  `Graph/WorkspaceViewModel.Graph.cs` — the one seed the census counts,
  its owner method renamed — before either document, handed to
  `NewGraphDocument` and to NewConnectionsLeaf, shut down in the
  workspace's drain after both documents; a document's retirement calls
  `DropAllPending()` and leaves it live. The four coalescing classes and
  the High flush span both surfaces, as the mac's one announcer spans
  its app. The leaf keeps its own view state (the root, the depth, the
  selected OCCURRENCE) and writes nothing to `GraphViewState` (B-D1).
- **Term 2 — Three levels: MOUNTED, ACTIVE, SPEAKING; view state is
  MOUNT-scoped; SPEAKING is decided at apply and nowhere earlier.**
  MOUNTED: the right pane is visible (`IsRightPaneVisible`) — the mac's
  pane exists. ACTIVE: the leaf is `ActiveLeaf` — the mac's
  `connectionsLeafActiveForView` (`AppState+Connections.swift:20–27`).
  SPEAKING: ACTIVE at the moment a body's apply runs, evaluated at
  DISPATCH (`:110–114`) — a switch away and back before a completion
  lands speaks; a hide then a switch away does not (IGH-14); MOUNTED is
  not consulted for speech on either host (B-D3). A MOUNTED true →
  false transition clears the view's selection, expansion and pending
  focus, as the mac's destruction of the pane does (IGG-9); the
  document's publication, root, depth and epoch survive it. ACTIVE and
  the graph tab's EFFECTIVE are independent predicates on both hosts.
- **Term 3 — The trigger matrix, classified once per mutation at its
  boundary.** A load starts from exactly these triggers and no other; a
  census names the callers (B-19 iii). Every trigger except the probe
  requires a root: with none, the NoNote transition installs and
  nothing starts (`:64–72`). Root RECONCILIATION is buffered to the
  OUTERMOST workspace mutation's boundary (PR A's boundary hook,
  `WorkspaceViewModel.Graph.cs:201`): `SyncPanels` inside a mutation
  records the candidate root, and the boundary reconciles ONCE with the
  final root — so closing the sole tab of a split (A → none → B inside
  one mutation, `Layout.cs:298`, `:338`, `:367`) is one A → B transition
  and at most one load (IGH-4); outside a mutation, reconciliation is
  immediate. ONE mutation yields at most ONE trigger, by precedence
  (c) > (a) > (b) > (d) > (g) — the palette's Show consumes the mount
  and the switch its own reveal causes, and a mount consumes the switch
  inside the same reveal (IGG-2).
  - **(a) MOUNT with the leaf ACTIVE at the boundary** — the pane
    revealed by ANY route; MOUNT is evaluated when the revealing route
    RETURNS, with the leaf active THEN (IGG-3). The pane setter arms a
    pending mount; every route that writes `IsRightPaneVisible = true`
    consumes it at its own end, and the census names every writer and
    asserts the consume (B-19 iii). The INITIAL mount is seeded, not
    armed: the field is initialised true and the snapshot carries no
    visibility (`:1401`, `WorkspacePersistence.cs:66`), so the
    constructor seeds ONE pending mount after the leaf's construction
    and consumes it after the restore and the first `SyncPanels`,
    posting no `RightPaneShown`; the census pins the seed (IGH-7). An
    AUDIBLE load whether or not a tree is held: the mac's `onAppear`
    (`ConnectionsPanel.swift:30`).
  - **(b) Leaf switch while MOUNTED** — the rail, or a leaf command's
    switch — an AUDIBLE load ONLY when the publication is not CURRENT
    for the root (Term 7); when it is current, nothing loads and nothing
    more is spoken: the mac's mounted switch (`RightPaneView.swift:
    361–364`). The stale case is B-D11.
  - **(c) The palette's Show** — ONE explicit AUDIBLE load always
    (`AppState+Connections.swift:175–180`), current or not; the reveal
    and the switch it performs raise no (a) or (b). The mac issues a
    second load when the pane was collapsed; Windows one (B-D7: the same
    lines).
  - **(d) Root change while ACTIVE and MOUNTED** — a CHANGE reconciled
    while active and mounted starts an AUDIBLE load (`:31–37`). The
    constructor's first sync is the initial value, not a change: launch
    is (a) alone. Inactive or unmounted, the root is tracked, the
    publication becomes STALE (Term 7), and (a) or (b) or the probe
    (Term 6) loads later. A rename reaches the leaf through the same
    funnel (`RetargetPath` ends in `SyncPanels`, `WorkspaceViewModel.cs:
    2042–2047`); a delete does not change the root — the tab is
    preserved and marked missing (`:2135`) — and the probe decides
    (IGH-10). Every root transition advances the leaf's ROOT EPOCH and
    the sequence, so an in-flight result for the old root is foreign
    (Term 8; IGH-3).
  - **(e) Depth change with a root** — an AUDIBLE load (`:162–166`); a
    bound is a no-op; without a root, nothing.
  - **(f) The generation probe** — Term 6's state machine, silent.
  - **(g) Root → none** — the synchronous NoNote install, no load; the
    mac returns before its sequence advances and rejects the old note's
    completion against the live path (`:65–72`, `:104–107`); Windows
    rejects it by the epoch (Term 8) and advances the sequence too.
- **Term 4 — Supersession is audible-over-anything; the probe never
  supersedes.** Every load advances `seq`; a completion whose seq is
  stale is dropped whole at the receiver's first check and changes
  nothing. An audible load issued after another speaks its own line
  only. The probe issues no load while one is IN FLIGHT (Term 6), so no
  silent load ever supersedes an audible one — the mac's refresh can
  (`:146–158` after `:73–74`), and Windows keeps the line: B-D12.
- **Term 5 — The policy rides the token.** The token carries the announce
  policy — PR A's `GraphAnnouncePolicy` (`GraphDocumentViewModel.cs:
  20–24`) — fixed when the load is issued by Term 3; nothing lives on the
  document to be promoted, and no envelope can consume a cause. The
  token's every field stays constant.
- **Term 6 — The probe is a state machine over the request and the
  presentation.** On the lifecycle's file-change and scan-finished arms,
  at EVERY level, the probe reads the generation off the dispatcher
  (A-3's crossing) and, at dispatch, consults Term 7's IN-FLIGHT flag
  before the presentation (IGH-1): a load in flight → the high-water
  mark is raised and no load is issued — the in-flight install compares
  the tree's generation with the mark and issues a silent load when the
  mark is newer (A-3's rule, `GraphDocumentViewModel.cs:501–505`); no
  load in flight and a root — Ready and CURRENT with a generation older
  than the read → a SILENT load; Ready and current and equal → nothing;
  Error or STALE → ONE silent load, so the leaf cannot stay errored or
  stale without a user action (the mac reloads in every state,
  `:146–158`; IGH-2); no root → nothing. B-D12 records the in-flight
  case only; the outcomes otherwise agree with the mac's.
- **Term 7 — The publication holds a PRESENTATION and a REQUEST; the
  start transition is defined for every state.** The publication is two
  records installed together: the PRESENTATION — NoNote, Loading,
  Ready(tree, bundle?), Error(message), each keyed by the request that
  produced it — and the REQUEST — the newest issued {root, depth,
  filter, epoch}, its `seq`, and the IN-FLIGHT flag, set at every start
  and cleared when that seq lands, accepted or not (IGH-1). CURRENT:
  the presentation's root is the leaf's root. STALE: it is not — the
  view shows the loading label, as the mac shows its loading row when
  the loaded path is not the effective path (`ConnectionsPanel.swift:
  115–118`). A load's START transition: the request is replaced and
  in-flight set; for a DIFFERENT root the presentation becomes Loading;
  for the SAME root the presentation is KEPT — Ready keeps its rows with
  no indicator (`:90–93`), Error keeps its message visible, the mac's
  retry (`:115–125`), Loading stays Loading. Empty is not a state: a
  Ready tree with no rows is the empty neighbourhood.
- **Term 8 — The envelope authenticates BOTH calls, and the token its
  root and epoch.** The body makes two calls — `graph_connections_tree
  (path, depth, filter)` (`lib.rs:1365–1370`) and, at depth one,
  `note_load_bundle(path, paging)` (`:1258–1262`, the mac's paging:
  cursor none, limit 200, `AppState+Connections.swift:91–92`) — and the
  envelope echoes the ACTUAL arguments of each separately. The token
  carries the root EPOCH beside the request (IGH-3). The receiver, at
  dispatch and before either arm, checks the token's every field — the
  document, the session, the lifecycle generation, `seq`, the request,
  and the epoch against the leaf's LIVE root and epoch (the mac's
  effective-path guard, `:104–107`) — then both echoes against the
  request, then the tree's depth and centre key, the key compared with
  `graph_stable_key_for_path(root)` (`lib.rs:3596`), sound because core
  indexes the exact `NodeKey::Path` (`session.rs:1962`) and the query
  preserves the exact path (`graph_queries.rs:133`). A mismatch installs
  nothing, advances nothing, speaks nothing.
- **Term 9 — Focus has an anchor; entry speaks the presentation's
  projection once; exit has a route.** The leaf host carries a
  persistent focus ANCHOR (the mac's, `RightPaneView.swift:291`): the
  tree when it has rows, else the host's own focusable element
  (AutomationId ConnectionsLeaf) in NoNote, Loading, STALE, Error and
  the empty neighbourhood (IGH-5). The right-pane content region is a
  NAMED focus boundary in the window: the boundary lands on the anchor
  when the leaf is active (today it lands on the rail,
  `MainWindow.xaml.cs:624`); directional focus LEFT from inside the
  leaf returns to the active editor group (the mac's return route,
  `AppState.swift:4364`), and RIGHT posts the shell's terminal line for
  the right boundary — never the editor-geometry fallthrough (`:900`;
  IGH-6). On ENTRY with nothing to read the document posts once: STALE
  or Loading → `GraphStatus{LoadingConnections}`; Ready with no rows →
  `GraphStatus{NoConnections}` (0a-D3); Error → nothing, the static
  error label reads (T5); NoNote → nothing (T1 reads); Ready with rows →
  nothing, the focused row reads. The speech entry point is censused
  with the load entry points (B-19 iii; IGG-12).
- **Term 10 — One relay; the mac line for line, the divergences
  listed.** With the workspace's one relay the four classes and the
  High flush span both surfaces and the filter count's gate is stored
  with the pending line and re-checked at fire — the graph document's
  rows-only publish enqueues through the relay's GATED filter entry
  with `GraphTabIsEffective`, and the census asserts that exact call and
  no ungated filter enqueue (A-10 as amended; IGH-8) — so every
  reachable sequence of the two surfaces matches the mac's; the
  differences that remain are each a recorded divergence: B-D3, B-D7,
  B-D8, B-D9, B-D10, B-D11, B-D12.

### The contracts (slice B1)

**B-1 — One leaf per workspace, constructed before the restore, retired
with it; the relay before both; the initial mount seeded.** The
workspace constructs the relay first (Term 1), then the leaf, once,
BEFORE `Restore()` and the constructor's first `SyncPanels()` (beside
`Citations`, `WorkspaceViewModel.cs:1600–1603`; the restore is `:1649`,
the sync `:1650`), with its lifecycle-generation provider and seams
installed (IPB-1's ordering), exposed as a PUBLIC property for the leaf
body's `Model` binding; the constructor seeds one pending mount and
consumes it after the restore and the first sync (Term 3(a): launch
with the pane visible and the leaf active and a note restored is ONE
load; with another leaf or no note, none). Shutdown: the leaf retires
into the bounded drain beside the graph document, then the relay shuts
down (`ShutdownGraphDocument`, `WorkspaceViewModel.Graph.cs:223–233`,
extended); a vault replacement builds a new workspace, whose retired
predecessor's tokens are foreign. The graph document's `Retire()`
(`GraphDocumentViewModel.cs:633–641`) calls the relay's
`DropAllPending()` in place of `Shutdown()` (A-1 as amended).

**B-2 — The load: one body, two calls, a token with the epoch and a
two-part envelope, validated before either arm; a rejection changes
nothing.** Term 8. The mac fetches the tree and, at depth one, the
bundle in ONE detached task and a bundle failure fails the load
(`AppState+Connections.swift:77–96`); B1 does the same in one body. The
TOKEN is (this document, the session the body was started against, the
lifecycle generation, the request {root, depth, filter, epoch}, `seq`,
the announce policy). The depth passed is core's clamp — B-15 — never a
host `min`/`max` (IGD-21).

**B-3 — The publication: the presentation and the request, CURRENT or
STALE, the start transition per state.** Term 7. The tree record
already carries the neighbourhood's counts (`graph_queries.rs:
998–1006`); the publication stores no second copy of the summary
(IGD-17). Rows render only when the presentation is CURRENT.

**B-4 — Liveness is rule C's Terms 2–10.**

**B-5 — Depth is core's clamp through a core query, read once per
PROCESS.** The bounds are core's constants 1 and 3 (`graph_queries.rs:
73`, `:74`), read through `graph_constants` by the process-wide lazy
accessor 0bD-11 requires; the CLAMP is core's `clamp_depth` (`:113–115`),
which B-15 exports so no host reimplements it. The control is "Local
graph depth", hinted "How many links away from this note to include."
(`ConnectionsPanel.swift:97`, `:108`), tags "Links", "2 links away",
"3 links away" (`:102–104`). A depth change is Term 3(e). Depth is
session-scoped (B-D4).

**B-6 — The tree is core's; the OCCURRENCE is the identity, scoped by
the root and the mount.** Core's in/out split, self-edge omission and
Link+Embed collapsing (`graph_queries.rs:1269`, `:1337`, `:1370`,
`:1402`, `:1423`, `:1437`); the host reorders and deduplicates nothing;
nesting is by level and order, the mac's `ConnectionsModel.nest`
(`ConnectionsPanel.swift:406–435`). The occurrence id —
`GraphConnectionRow`'s `id`, not its `node_id` or `stable_key` — is the
view model's identity, the UIA element's identity and the key for
selection, expansion and focus restoration. The id names a direction
and a key path from the first hop down (`graph_queries.rs:1054`,
`:1162`), NOT the root, so two roots sharing a neighbour reuse an id:
the view's selection, expansion and pending focus are keyed by (root,
occurrence id) — a root change CLEARS all three, a same-root refresh or
depth change PRUNES them to the ids the new tree carries, and a collapse
of the pane clears them (Term 2). The mac's selection is unscoped and
survives a root change, a depth change and a refresh
(`ConnectionsPanel.swift:18`, `:152`), and is destroyed with the pane:
the clears are B-D8; the pane clear is the mac's own.

**B-7 — Depth one carries core's snippets**, fetched in the same body,
overlaid by exact path at level one only (`AppState+Connections.swift:
77–94`); a bundle failure is a load failure, as the mac has it (IGD-18);
the bundle's own arguments are authenticated (Term 8).

**B-8 — Row copy is core's, `embed_only` the predicate, badges in
order.** `GraphRow` rendered through the relay (`a11y.rs:3250–3253`),
never announced; ", embed" when `embed_only`; ItemStatus "Unresolved"
before "Embed" before "Attachment" (`ConnectionsPanel.swift:311`,
`:313`, `:315`), hidden from the accessibility tree as the mac hides
them (`:328`).

**B-9 — The row's actions are core's inventory; admission is the
host's; the row's hint is its activation's.** `graph_row_actions` once
per kind; in B1: Open on Return, Open in a new tab on Ctrl+Return,
Reveal in the file tree, Create note on a ghost; Show connections is
LISTED and DISABLED. The ROW's hint stays T15/T16 — the ghost's or
"Opens the note." — because Return still opens the note (the mac's
`:243–250`; IGG-15); the one B1 reason, "Show connections is not
available yet." — a Windows string the leaf owns until B2 wires the
action and removes it (B-D6) — appears ONLY on the disabled menu
action's HelpText and in the disabled-action fact, the substrate's
per-action reason (`Grids/AccessibleDataGrid.cs:1366`; the mac's menu
hint, `:258–270`). Core's `GraphRowActionSpec` carries only the action
and its title, and its own comment says runtime admission is the host's
(`graph_queries.rs:806–819`, IGD-23). Invoke, SelectionItem,
ExpandCollapse and ScrollItem on the DATA peer of every row, top-level
and nested; Alt+Up and Alt+Down between the groups; Menu and Shift+F10
for the actions.

**B-10 — The announcements: a generative rule, pinned by a model; the
graph family's projection; worked rows.** `LeafPanelShown{title}`
renders "Connections panel." (`a11y.rs:1682`), byte-identical to
`GraphStatusNote::ConnectionsPanel` (`:3370`). Windows posts the
shell's line on a leaf SWITCH (the setter's own) and the graph family's
line on a palette Show of the already-active leaf, so the panel is
named once per invocation on both hosts (B-D5); the restore posts
neither. THE RULE (rounds 6 and 7, IGG-11/13, IGH-10..15): the merged
timeline of ANY route is (1) the shell's own lines for that route,
unchanged and pinned by the shell's own facts — `RightPaneShown`,
`RightPaneHidden`, `LeafPanelShown`, `TabFocused`, `TabClosed`,
`EditorPaneFocused`, `TasksReviewShown`, the History line, the
missing-file line, the graph table's own lines when the graph tab
becomes effective (`WorkspaceViewModel.Graph.cs:147–194`); then (2) the
projection of the ONE trigger Term 3 classifies for the route's final
state, or nothing when it classifies none; then (3) Term 9's entry line
when focus enters the anchor; and (4) every completion's line iff
SPEAKING at apply (Term 2), whatever happened between issue and apply.
The task loop pins the rule by a MODEL fact: an executable model of
Terms 2–9 derives the timeline for every route below crossed with the
pane, leaf, root and presentation states and the in-flight flag, drives
the workspace through the same route, and asserts the recorded events
equal the derivation — a route or a state the model cannot derive fails
it, and a row is never the contract. The graph family's projection:

| Cause (Term 3) | Outcome at dispatch | Spoken |
| --- | --- | --- |
| (a) mount, (b) stale switch, (c) Show, (d) root change, (e) depth change — WITH a root | READY | the summary — the counts zero when the tree has no rows |
| the same | ERROR | `GraphBlocked{ConnectionsLoadFailed}` |
| (b) switch with a CURRENT presentation | no load | nothing |
| any trigger WITHOUT a root | NoNote, no load | nothing |
| (g) root → none | NoNote | nothing |
| an audible load superseded by an audible load | the superseder's | its summary or failure line only |
| (f) probe, any state | Term 6 | nothing |
| Focus enters the anchor (Term 9) | STALE or Loading | `GraphStatus{LoadingConnections}` |
| Focus enters the anchor | READY, no rows | `GraphStatus{NoConnections}` |
| Focus enters the anchor | READY with rows, NoNote, or Error | nothing — the element reads |
| any completion not SPEAKING at apply | any | nothing |

Worked rows — illustrations of the rule, each also a fact, none the
contract ("the summary" means the summary or, on failure, the failure
line; "the focus line" means Term 9's line or nothing; "iff SPEAKING"
means Term 2 at apply):

| Route | Windows lines, in order | The mac |
| --- | --- | --- |
| Rail switch to the leaf, CURRENT | `LeafPanelShown` (`:1818`); nothing more | the same (`RightPaneView.swift:361–364`) |
| Rail switch, STALE | `LeafPanelShown`; the summary iff SPEAKING | `leafPanelShown`; the loading row, no load (B-D11) |
| Rail switch, no root | `LeafPanelShown`; nothing more | the same |
| Rail switch AWAY, a completion in flight | `LeafPanelShown` for the other leaf; the completion iff SPEAKING at apply — silent unless the user switched back first (IGH-14) | the same |
| Pane HIDE, a completion in flight | `RightPaneHidden`; the view state clears; the completion iff SPEAKING at apply (B-D3) | the same, the view destroyed |
| Palette Show, pane visible, leaf not active, with a root | `LeafPanelShown` (the setter); the focus line; the summary iff SPEAKING | `.connectionsPanel`; the summary |
| Palette Show, leaf active, with a root | `GraphStatus{ConnectionsPanel}`; the focus line; the summary iff SPEAKING | the same |
| Palette Show, no root | the panel line as above; nothing more | the same |
| Palette Show, pane collapsed | `RightPaneShown` (B-D9); then the row above; ONE load (B-D7) | no pane line; two loads, one summary |
| Pane toggle revealing the pane, leaf active, with a root | `RightPaneShown` (`:1877`); the summary iff SPEAKING — Term 3(a) | `.rightPaneShown` (`AppState.swift:8855`); the summary (`onAppear`) |
| Pane toggle revealing the pane, ANOTHER leaf active | `RightPaneShown`; no Connections load | the same |
| Directional focus into a collapsed pane, leaf active | `RightPaneShown` (B-D9); `LeafPanelShown` (`Layout.cs:608–610`); the focus line; the summary iff SPEAKING — Term 3(a) | `leafPanelShown` (`AppState.swift:4374–4377`); the summary |
| Directional focus into a visible pane, leaf active | `LeafPanelShown`; the focus line; NO load | the same |
| Directional focus into the pane, another leaf active | `LeafPanelShown` for that leaf; no Connections load or line | the same |
| Directional focus LEFT from inside the leaf | the editor group's own focus line (Term 9's route) | the mac's return route (`AppState.swift:4364`) |
| Directional focus RIGHT from inside the leaf | the shell's terminal line for the right boundary | the same |
| Tasks Review or History command, from any pane state | `RightPaneShown` when collapsed; `LeafPanelShown` when the leaf changed; that command's own line (`:1854–1868`; `History.cs:62`); ZERO Connections loads (Term 3(a) sees the final leaf) | that leaf's own sequence |
| Launch or restore, pane visible, leaf active, note restored | nothing from the shell; the summary — Term 3(a), ONE load | the same |
| Launch or restore, another leaf or no note | nothing; no load | the same |
| Tab activation to another NOTE while ACTIVE and MOUNTED | `TabFocused` when an existing tab is activated (`Layout.cs:68–79`), none for an in-place open; the summary iff SPEAKING | the same |
| Tab activation to a DUPLICATE of the same note | `TabFocused`; no root change, nothing more | the same |
| Tab activation to a NON-note tab while active | `TabFocused`; NoNote for the leaf, nothing from it; the GRAPH tab's own lines when it is the graph (PR A) | the same |
| Tab close, the successor another note, while active and mounted | `TabFocused` when applicable; `TabClosed` (`Layout.cs:298`); the summary iff SPEAKING | the same |
| Tab close of a duplicate, the successor the same note | `TabFocused`; `TabClosed`; no root change, nothing more | the same |
| Tab close leaving no note | `TabClosed`; NoNote, nothing more | the same |
| Close of a split's sole tab (A → none → B in one mutation) | the shell's lines; ONE A → B reconciliation at the boundary; the summary iff SPEAKING, or nothing when B is A | the same |
| Split | `TabFocused` for the duplicate (`Layout.cs:228`); no load, the root unchanged | the same |
| Group close onto the same root | `EditorPaneFocused` (`Layout.cs:371`); nothing more | the same |
| Group close onto another root | `EditorPaneFocused`; the summary iff SPEAKING, or NoNote | the same |
| Rename of the root, ACTIVE and MOUNTED, nothing in flight | the shell's rename lines; ONE audible load — Term 3(d); the probe's generation change lands while it is in flight → the mark only (B-D12) | the selection-change load, then the refresh's silent load superseding it |
| Rename of the root, inactive or unmounted | the shell's rename lines; no root-change load; the probe → STALE → one silent load (Term 6) | the refresh's silent load |
| Delete of the root | the shell's missing-file line (`WorkspaceViewModel.cs:2135`); the tab preserved, the root kept; Term 6: a silent load or the mark | the same, the tab kept (`AppState.swift:19045`) |
| Depth change with a root | the summary iff SPEAKING | the same |
| Depth change without a root, or at a bound | nothing | nothing |
| Ghost create, source current, in-place open, ACTIVE and MOUNTED | the open is silent; `GraphStatus{NoteCreated}`; the caveat's High `HostComposed` when landed with one (`FilesSidebarViewModel.SurfaceNotes.cs:52–56`); the root change's summary iff SPEAKING — Term 3(d) | `.noteCreated`; the refresh |
| Ghost create, source current, in-place open, inactive or unmounted | the open is silent; `NoteCreated`; the caveat if any; no audible load (Term 3(d)); the probe's silent refresh | the same lines |
| Ghost create, source current, the note already open in a tab | `TabFocused`; `NoteCreated`; the caveat if any; the summary iff a root-change load was issued and SPEAKING | — |
| Ghost create, source current, dirty-navigation refusal | `NoteCreated`; the caveat if any; no root move, nothing more | — |
| Ghost create, source not current (B-11) | `NoteCreated`; the caveat if any | `.noteCreated` and the open (B-D10) |
| Ghost create fails | `GraphBlocked{NoteCreateFailed}` | the same |
| Table sort CHANGED, its count pending; leaf failure inside 200 ms | `GridSorted` (`GraphTableView.cs:85`); `GraphBlocked{ConnectionsLoadFailed}`; the count DROPPED (the one relay's High flush) | the same |
| Table sort returned to the accepted sort, its count pending; leaf failure inside 200 ms | no `GridSorted`; the failure line; the count dropped | the same |
| Table sort's count pending; the graph tab leaves EFFECTIVE before 200 ms | the count dropped at fire time (the stored gate, A-10 as amended) | the same |
| Vault replacement | the old workspace retires: every token foreign, nothing spoken; the new workspace: the launch row | the same |
| Shutdown | nothing: the leaf retires into the drain, the relay after it | the same |

**B-11 — The ghost create is addressed by the LEAF's root and epoch, so
it needs no graph tab.** PR A's funnel returns early without a graph tab
(`WorkspaceViewModel.GraphCreate.cs:28`); the worker is generalised to
a SOURCE address — the graph document's or the leaf's — keeping AD-8's
every other rule and ORDER: the open before the one `NoteCreated`
(`:65–78`), the lifecycle generation, the admission reason,
`NoteLanded`, the `Unavailable` drop, the typed failures, the caveat.
The leaf's address is (the root the ghost belonged to, the root EPOCH at
invocation); at completion the open runs only if the lifecycle
generation, the root path AND the exact epoch still hold — the leaf's
ACTIVE state is NOT consulted (IGG-8; the mac opens whatever leaf shows)
— so a reader who left A for B and came back to A has advanced the
epoch twice, and a create parked across the excursion lands its file
and its `NoteCreated` line but opens nothing (PR A's immutable-address
precedent, IPC-4/IPD-2). The mac opens after its ownership check alone
(`AppState+Connections.swift:278–294`) — B-D10. Whether the open's root
change then loads audibly is Term 3(d)'s (IGH-13); the probe follows
the create's generation change (Term 6).

**B-12 — The probe is Term 6.**

**B-13 — The tree is the outline's shape, the model is released, the
anchor and the boundary are the host's, the placeholder retires.**
`Canvas/CanvasOutlineView.cs`: patterns on the DATA peer (`:184–197`,
`:219–239`, the nested rows' peer `:285–287`), Standard virtualisation
(`:359–365`), focus that realises the container (`:431–453`). `Model`
unsubscribes before it subscribes and on null clears rows, indexes,
selection and pending focus and disables the delegates (TGA-10's
finding in tree form); a root change and a collapse clear the same
(B-6, Term 2). The leaf host carries Term 9's anchor and the window
names the right-pane content region as a focus boundary with its left
and right routes. The generic placeholder collapses for `ActiveLeaf.Id
== "connections"` beside the leaves it names (`MainWindow.xaml:
1944–1995`).

**B-14 — Three chordless rows in `ChordScope.None`, always enabled, the
bounds a no-op.** `slate.graph.showConnections` (Term 3(c) — the
palette's meaning; the row action's re-root is B2),
`slate.graph.connectionsDeeper` and `slate.graph.connectionsShallower`,
chordless and therefore `ChordScope.None` through `Reg`'s rule
(`Commands/ChordTable.cs:66`, `:72`, the graph rows at `:937–942`);
labels and hints the mac's byte for byte (`SlateCommands.swift:
1516–1535`, which define exactly these three — IGD-25); each names a
workspace command and a resolver in `Commands/SlateCommandRegistrar.cs`'s
`BuildResolvers()` (`:499`). Deeper and Shallower are ALWAYS enabled and
a boundary invocation is a no-op through core's clamp — no load, no line
— the mac's guard (`AppState+Connections.swift:162–166`); the facts
invoke through the registrar and the palette, at the bounds and without
a root included. `chords.json` regenerated through the projection.

**B-15 — Core gains two queries and the surface rises to twenty-six.**
`connections_filter()` returns the mac's fixed local filter —
attachments ON, ghosts on, orphans off, NOT `GraphFilter::default()`
(`graph.rs:242` excludes attachments; the mac's reason at
`AppState+Connections.swift:12–19`) — and `clamp_connections_depth(depth)`
exports `clamp_depth`. Both join GRAPH_QUERY_SURFACE
(`graph_queries.rs:35–60`); the Rust fact that asserts twenty-four
(`:2402`) asserts twenty-six, and the FFI tripwire's floor
(`lib.rs:13462`) and the Windows census's floor
(`Censuses/GraphQuerySurfaceCensus.cs:53`) rise to twenty-six (IGD-29);
the mac's literal filter (`AppState+Connections.swift:17–19`) and its
`clampConnectionsDepth` (`:34–37`) become the calls, the mac wrapper
kept for its `Int` callers and its tests
(`ConnectionsPanelTests.swift:40–43`). The spec's Consumes also names
`graph_stable_key_for_path` (Term 8).

**B-16 — The label inventory T1–T17, byte for byte**, as a theory with
the substitutions exercised: the no-note label, the loading label and its
accessible name, the empty and error labels, both group headers with
singular and plural counts, both group empties, the three badges, the
row and action hints, and B1's one Windows string on the disabled menu
action alone (B-D6, B-9).

**B-17 — §W-A: the golden keeps 0b's schema; the full record is an
in-process fact.** The `connections` entries of `graph_queries.json` are
0b-13's, unchanged: stable-keyed, id-free — `occurrence`, `parent`,
`level`, `key`, `kind`, `embed_only`, `references` per row, the centre
key, the tree depth and the summary per entry (`tools/ParityHarness/
SurfaceSerializer.cs:1477–1505`; 0bD-5; `GraphQueriesArtifactCarriesNoNodeIds`).
A Windows fact asserts, for every pinned (path, depth) pair
(`PinnedGraphConnections`, `:1228–1236`), that the leaf's rows carry the
session's tree record field by field — `id`, `level`, `parent_id`,
`node_id`, `stable_key`, `label`, `path`, `target_raw`, `kind`,
`embed_only`, `in_links`, `out_links`, `references`
(`graph_queries.rs:971–991`) — and that the golden's projection of those
rows equals the artifact's entries.

**B-18 — §W-C: the journey, the axe scan, the evidence manifest.**
GraphConnections_LeafWalkDepthAndReRoot_AreClean — the name kept for B2's
continuation — in B1 asserts: the leaf heading's AutomationId and Name;
the tree's AutomationId, ControlType Tree, and Selection support; on a
top-level row AND a nested row, all four patterns Invoke, SelectionItem,
ExpandCollapse and ScrollItem, unconditionally (the container's patterns
are not what AT reads, `CanvasOutlineView.cs:188–197`); the summary
region's exact text for the fixture's root; both group headers' Names
with their counts; the first row's Name equal to core's row copy and its
ItemStatus; the depth control's Name, hint and value before and after a
change to 2, and the summary's changed text; a ghost row's Name and
Create note landing the file with the `NoteCreated` line in the
timeline; directional focus left from the tree landing in the editor.
Axe scope graph-connections. `Censuses/WcMatrixGraphEvidenceCensus.cs`'s
manifest (`:30–40`) gains the row. RUN to the last step locally before
the push.

**B-19 — The censuses, falsifiable.** The relay wall extends to the leaf
with no new FILE exemption. (i) The INSTANCE census: every object
creation of type `GraphAnnouncer` across `src/SlateWindows` — by
declared type, whatever the receiver is named — is counted; exactly ONE
exists, in `NewGraphRelay` under `Graph/`, with `_announceRendered` as
its argument (A-10 as amended); a second anywhere, under any name, fails
it (IGF-11, IGG-1). (ii) `AnnouncementSeamCensus` names two boundary
files — the graph document's and the leaf's — with each file's own
seams. (iii) The trigger and speech census: the leaf's load entry
points (Term 3) and its speech entry points (Term 9) have exactly the
callers the terms name, in the workspace's Connections partial, the
mutation boundary and the leaf view's focus handler, and no other
caller in the shell; every writer of `IsRightPaneVisible = true` in the
shell consumes the pending mount at its end and the constructor seeds
the initial one (Term 3(a)); the graph document's filter count is
enqueued through the relay's gated entry and no ungated filter enqueue
exists (Term 10); each route is pinned to ONE load per invocation by
the model fact. (iv) The root census: `SyncPanels` is the ONLY method in
the shell that records the leaf's candidate root, and the boundary hook
the only reconciler (Term 3). (v) The depth census, by DATAFLOW across
the whole shell: every write to the leaf's depth storage has
`SlateUniffiMethods.GraphClampConnectionsDepth(` as its right side;
every value reaching that call's argument is one of exactly four
producers — the raw parameter of the leaf's public depth entry, the
depth control's index converted, and `Depth + 1` / `Depth - 1` inside
`Deeper` and `Shallower` alone (the mac's `:171–172`) — traced through
the shell's own syntax trees with no other arithmetic, comparison,
conditional, `Math.*` call or helper call on the way; an alias, a
pre-clamp, or a helper in any partial fails it (IGF-12). (vi) The
citation census's B tuple carries a floor of one below the measured
population.

**B-20 — The matrix rows, as a contract.** `parity_matrix.md`: the three
command rows `slate.graph.showConnections`,
`slate.graph.connectionsDeeper`, `slate.graph.connectionsShallower` move
to `W6_2_STATUS` through `scripts/generate-parity-matrix.py`'s
W6_2_DELIVERED_COMMANDS; `w_c_matrix.md` gains the row "Graph
connections leaf (W6-2 PR B)" whose evidence cell names the journey and
the axe label; a census asserts both rows exist with those statuses.

### Decisions (PR B, slice B1)

- **BD-1 — The owner split PR B on 2026-09-05.** B1 is this section; B2
  carries re-root and Back, the table's and the Bases' Show connections,
  and the shared selection (spec R-B), which is where R-B's amendment is
  decided; A-1 and A-10 are decided (BD-12).
- **BD-2 — The leaf owns its document and selection**; the relay is the
  workspace's (BD-12).
- **BD-3 — The local filter and the depth clamp are core queries.**
- **BD-4 — Verbosity is Standard until PR C.**
- **BD-5 — The occurrence is the identity**, scoped by the root and the
  mount (B-6).
- **BD-6 — Depth is session-scoped in B1.**
- **BD-7 — The spec's stale lines are amended**: the Goal (IGD-3; its
  "own relay", and — revision 8 — its "where the amendment is decided",
  IGH-9), the behaviour paragraph, the Consumes and Builds lines (IGE-9;
  Builds' "own `GraphAnnouncer` instance" and BD-9, IGH-9),
  `graph_stable_key_for_path` in Consumes (IGF-1; its term, IGH-16).
- **BD-8 — Show connections is listed and disabled in B1**, with the one
  reason of B-9 on the menu action alone, until B2 wires it.
- **BD-9 — Superseded by BD-12.**
- **BD-10 — The policy rides the token; audible supersedes; the probe
  defers only while a load is in flight.** Terms 4–6.
- **BD-11 — Rule 4 invoked at round 5**; the design pass; revision 8
  makes the announcement rule generative and pins it by a model.
- **BD-12 — The owner amended A-1 and A-10 on 2026-09-05 (option 1 of
  the round-6 decision): one relay per workspace**, the mac's one
  announcer per app, constructed with the workspace and shut down in its
  drain, shared by the graph document and the leaf; a document's
  retirement drops its pending classes and leaves the relay live; the
  relay gains 0a-9's fire-time gate, stored with the pending line. The
  amendments are recorded in place under A-1 and A-10.
- **BD-13 — Frozen at revision 8 under the PR 0b precedent** (rule 5 for
  the third time, rounds 5–7): the text corrected for every round-7
  finding as the discharge, the findings carried as the ledger the task
  loop discharges by code and by the model fact. Precedent applied; the
  owner may overrule.

### Recorded divergences (PR B, slice B1)

- **B-D1 — The leaf does not write `GraphViewState.SelectedKey`** in B1;
  spec R-B's shared selection arrives with B2.
- **B-D2 — Withdrawn** with BD-12: one relay, one coalescing space, one
  High flush, the mac's gate.
- **B-D3 — Speech gates on the active leaf alone**, the mac's predicate;
  MOUNTED is not consulted, as the mac does not consult it.
- **B-D4 — Depth does not persist across sessions in B1** and resets on
  vault replacement.
- **B-D5 — `LeafPanelShown` on a switch, the graph family's line on a
  Show of the active leaf**; the strings are identical.
- **B-D6 — "Show connections is not available yet."** is a Windows-only
  B1 string with no mac twin, on the disabled menu action's HelpText
  alone; B2 removes it.
- **B-D7 — One load on a collapsed-pane Show** where the mac issues two
  (the explicit load, then the remount's); the spoken lines are the
  same.
- **B-D8 — Selection, expansion and pending focus clear on a root
  change and are pruned on a same-root refresh or depth change** (B-6);
  the mac's unscoped `@State` selection survives all three (a vanished
  id stays selected there). The pane collapse clears them on both
  hosts.
- **B-D9 — `RightPaneShown` on every pane reveal** — the shell's W1
  setter posts it (`WorkspaceViewModel.cs:1877`) on a palette Show and a
  directional focus that reveal a collapsed pane, where the mac's helper
  assigns visibility silently (`AppState.swift:8864–8867`) and only its
  toggle posts the line. A shell divergence B1 records and does not
  change.
- **B-D10 — The ghost create's open is suppressed when the leaf's root or
  epoch moved** (B-11); the mac opens after its ownership check alone
  (`AppState+Connections.swift:278–294`). The leaf's active state is not
  consulted on either host.
- **B-D11 — A mounted rail switch to a STALE leaf loads** (Term 3(b));
  the mac shows its loading row and starts nothing (`ConnectionsPanel.
  swift:31–36`, `:115–118`), the recorded mac detail.
- **B-D12 — The probe defers to a load IN FLIGHT** (Term 6, A-3's
  high-water mark), so an audible load's line is never lost to a silent
  refresh; the mac's refresh supersedes it and speaks nothing
  (`AppState+Connections.swift:146–158`). In every other state the probe
  loads as the mac does.

### Mac details recorded while reading (not this issue's to fix)

- A mounted rail switch to the Connections leaf after the note changed
  while it was inactive shows "Loading connections…" and starts no load
  (`ConnectionsPanel.swift:31–36`; `onAppear` does not re-fire on a
  mounted view). Windows loads (B-D11).
- `connectionsLoading` is written (`AppState+Connections.swift:78`,
  `:109`) and never read by the panel.
- PR A's Windows relay gated the filter count at enqueue and fired it
  unconditionally (`GraphDocumentViewModel.cs:511`, `GraphAnnouncer.cs:
  151–162`), where 0a-9 names the mac's stored, fire-time gate
  (`GraphAnnouncer.swift:150`, `:194–207`); A-10 as amended adds it, and
  B1's task loop lands it with the relay's move.

### The rounds — the ledger

**Round 1 (revision 1): thirty-one findings, eighteen blockers.** Each
answered by revision 2; the answers that survive into B1 are B-2 (IGB-3),
B-3 (IGB-4), B-12 (IGB-5), Term 3 (IGB-6, IGB-7), B-11 (IGB-11), B-10
(IGB-12, IGB-13), B-9 (IGB-14), B-14 (IGB-15, IGB-16), B-1 (IGB-17),
B-13 (IGB-18, IGB-25), B-7 (IGB-19), BD-7 (IGB-21), B-5 (IGB-22,
IGB-23), B-8 (IGB-24), the corrected citations (IGB-26), B-16 (IGB-27),
B-6 (IGB-28), B-17 (IGB-29), the tests (IGB-30), B-18 and B-20 (IGB-31);
deferred to B2 with the split: IGB-8, IGB-9, IGB-10, IGB-20; IGB-1 and
IGB-2 (one relay) — answered at last by BD-12 (its core query survives
as B-15).

**Round 2 (revision 2): twenty-two findings, nineteen blockers; rule 5,
rule 4.** IGC-1, IGC-2 — the relay's lifetime: BD-12; IGC-3 — Term 3;
IGC-4 — Term 6; IGC-5 — Terms 3 and 5; IGC-6 — Term 3(d) (no pinned
root in B1); IGC-7, IGC-8, IGC-9, IGC-10 — B2 (re-root); IGC-11 — B2
(shared key); IGC-12 — B-7; IGC-13 — B-9; IGC-14 — B-10; IGC-15 — B-11;
IGC-16, IGC-17 — B-14 (three rows, None; Back to B2); IGC-18, IGC-19 —
BD-7 and B-15; IGC-20 — the tests below; IGC-21 — B-16; IGC-22 — this
inventory.

**Round 3 (revision 3): twenty-nine findings, twenty-four blockers; the
split.** IGD-1 — the brief's inventory is generated from the section
(this revision: B-1..B-20, BD-1..BD-13, B-D1..B-D12); IGD-2 — the
round-2 ledger above, one entry per finding; IGD-3 — BD-7; IGD-4, IGD-5,
IGD-6 — BD-1 and BD-12 (the relay), Term 10; IGD-7 — Terms 4–5; IGD-8 —
Term 3(a) and B-10's restore row; IGD-9 — Term 3 and B-10; IGD-10,
IGD-11 — B2; IGD-12 — Term 2 and B-D3; IGD-13 — Term 6; IGD-14 — Term
3(d) (no pinning); IGD-15 — Term 7; IGD-16 — B-10's rule and rows;
IGD-17 — B-3; IGD-18 — B-7; IGD-19 — Term 3(d); IGD-20 — B2; IGD-21 —
B-15 and B-2; IGD-22 — B-11 (the epoch); IGD-23 — B-9 (the string, on
the menu action); IGD-24 — B-17; IGD-25 — B-14; IGD-26 — B-18; IGD-27 —
B-19 (v); IGD-28 — B-20; IGD-29 — B-15.

**Round 4 (revision 4): sixteen findings, six blockers, falling.**
IGE-1 — BD-12 (the owner's amendment; BD-9 superseded); IGE-2 — B-17;
IGE-3 — Term 10 (one relay); IGE-4 — Term 7, Terms 3–5 and BD-10; IGE-5
— B-10's rule and rows; IGE-6 — Term 3(d) and B-11, B-D10; IGE-7 —
Terms 4–5, 8 and B-2; IGE-8 — B-6 and B-13; IGE-9 — BD-7; IGE-10 — B-9
and B-D6 (the menu action alone, IGG-15); IGE-11 — B-14; IGE-12 — B-19
(v); IGE-13 — B-17; IGE-14 — B-18; IGE-15 — B-D4; IGE-16 — B-15.

**Round 5 (revision 5): twelve findings, ten blockers, rising; rule 5,
rule 4 — the design pass.** IGF-1 — Term 8, B-15 and BD-7; IGF-2 — the
mac traced; Terms 2–3; B-D7, B-D11; IGF-3 — Term 8 and B-2, B-7; IGF-4
— Term 10 and A-10 as amended (the gate; B-D2 withdrawn); IGF-5 — Term
4, Term 6 and B-D12; IGF-6 — B-10's ghost rows and B-11; IGF-7 — B-D9
and B-10's pane rows; IGF-8 — Term 3's root requirement, 3(g), B-10's
no-root rows, B-14; IGF-9 — Term 7 and B-3; IGF-10 — B-6 and B-D8;
IGF-11 — B-19 (i); IGF-12 — B-19 (v).

**Round 6 (revision 6): fifteen findings, fifteen blockers; the design
judged unsound; the owner's amendment.** IGG-1 — BD-12; Term 1, Term
10, B-1, B-19 (i); IGG-2 — Term 3's precedence, one trigger per
mutation, the model fact; IGG-3 — Term 3(a): MOUNT evaluated at the
route's end; the pending mount and its census; IGG-4 — Term 6 and Term
4, B-D12; IGG-5 — Term 7's start transition per state; IGG-6 — Term
3(g); IGG-7 — B-D11 and Term 10; IGG-8 — B-11 (ACTIVE not consulted)
and B-D10; IGG-9 — Term 2 and B-6; IGG-10 — B-D8; IGG-11 — B-10's rule
and rows by root; IGG-12 — Term 9 and B-19 (iii); IGG-13 — B-10's rule
and the rows it illustrates; IGG-14 — B-10's two sort rows and the gate
row; IGG-15 — B-9, B-16, B-D6.

### Round 7 — seventeen findings, dispositions; rule 5 for the third time; THE FREEZE

Round 7's verdict: the design not sound, the contracts not following —
fifteen blockers, of which the in-flight identification (IGH-1), the
seeded mount (IGH-7), the gate's wiring (IGH-8), the reconciliation of
one mutation (IGH-4) and the rows of B-10 (IGH-10..15) were CREATED by
revision 7's fixes, as round 6's were by revision 6's and round 5's by
revision 5's. Rule 5 for the third time is the PR 0b precedent's
condition: the section is FROZEN at revision 8, the text corrected for
every finding below as the discharge, and the findings carried as the
ledger the task loop discharges by code and by the model fact of B-10.
**Precedent applied; the owner may overrule.**

| Finding | Severity | Disposition |
|---|---|---|
| IGH-1 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (Term 7: the presentation and the request as two records, the IN-FLIGHT flag set at every start and cleared when its seq lands; Term 6 reads the flag before the presentation) and by code: the in-flight token, the same-root Show-then-probe fact |
| IGH-2 | BLOCKER | ledger — discharged in the frozen text (Term 6: Error or STALE with a root and nothing in flight → one silent load; B-D12 narrowed to the in-flight case) and by code: the errored-then-generation-change and stale-then-generation-change facts |
| IGH-3 | BLOCKER | ledger — discharged in the frozen text (Term 8: the token carries the epoch and the receiver checks the live root and epoch before either arm; Term 3(d): every root transition advances the epoch and the sequence) and by code: the unmounted A → B in-flight race fact |
| IGH-4 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (Term 3: reconciliation buffered to the outermost mutation boundary; one A → B transition) and by code: the split's sole-tab close fact, one load or none |
| IGH-5 | BLOCKER | ledger — discharged in the frozen text (Term 9: the anchor; Error → nothing; entry detected at the host) and by code: the Error and NoNote entry facts |
| IGH-6 | BLOCKER | ledger — discharged in the frozen text (Term 9: the named right-pane boundary, left to the editor, right to the terminal line; B-13, B-18) and by code: the window's boundary and the journey's left step |
| IGH-7 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (Term 3(a), B-1: the seeded initial mount, consumed after the restore, no `RightPaneShown`; the census pins the seed) and by code: the launch facts with the leaf active, with another leaf, with no note |
| IGH-8 | BLOCKER (created by the amendment) | ledger — discharged in the frozen text (Term 10, B-19 iii: the gated filter entry, the stored gate, the census of the exact production call) and by code: queue → leave effective → fire → no output |
| IGH-9 | BLOCKER | discharged by the spec amendment in the same commit: Builds names the workspace relay injected into the leaf's document, BD-9 removed, the Goal says B2 decides re-root and the shared selection only |
| IGH-10 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (Term 3(d), B-10's delete row: the tab preserved, the root kept, Term 6 decides) and by code: the delete fact |
| IGH-11 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (B-10's two rename rows by ACTIVE/MOUNTED/in-flight; the mac column's second load) and by code: both rename facts |
| IGH-12 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (B-10's rows by final root equality and successor kind; the graph tab's own lines) and by code: the duplicate-close, same-root group-close and graph-successor facts |
| IGH-13 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (B-10's ghost rows by liveness; B-11's last sentence) and by code: the inactive-open ghost fact |
| IGH-14 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (Term 2: SPEAKING at apply and nowhere earlier; B-10's switch-away and hide rows) and by code: the away → back and hide → away facts |
| IGH-15 | BLOCKER (created by rev 7) | ledger — discharged in the frozen text (B-10's RULE and its model fact; the added rows: other-leaf reveals, Tasks Review and History, launch with another leaf or no note, replacement, shutdown) and by code: the model fact over every route and state |
| IGH-16 | MINOR | discharged by the spec amendment in the same commit: Term 8 |
| IGH-17 | MINOR | discharged in the frozen text: `graph.rs:242` |

### Task loop — records (PR B, slice B1)

**TGB-0 — Baselines at the freeze (0fc3bbd).** GRAPH_QUERY_SURFACE
twenty-four names; the leaf catalogue's `connections` entry empty; the
relay constructed in `NewGraphDocument` and shut down by the document's
`Retire()`; PR A's suites green on main at a87764e's successor.

**TGB-1 — Core and FFI (B-15): the local filter and the clamp are
queries; the surface is twenty-six.** `connections_filter()` and
`clamp_connections_depth(depth)` in `graph_queries.rs` beside
`clamp_depth`; both names appended to GRAPH_QUERY_SURFACE; the count
fact asserts twenty-six; two facts — the filter is attachments ON,
ghosts on, orphans off and NOT the default (`graph.rs:242`), and the
clamp is the named constants at 0, 1, 3 and 99. The FFI exports
`graph_connections_filter()` (the record built field by field: the
mirror converts FFI → core only, `lib.rs:3415`) and
`graph_clamp_connections_depth(depth)`; the mirror tripwire's floor
rises to twenty-six. The mac's `connectionsFilter` becomes
`graphConnectionsFilter()` and `clampConnectionsDepth(_:)` wraps
`graphClampConnectionsDepth(depth:)` — the `Int` wrapper kept for its
callers (`AppState+Connections.swift:75`, `:163`,
`AppState+GraphConfig.swift:44`, `:68`) and its tests
(`ConnectionsPanelTests.swift:40–43`); a negative depth clamps at the
floor. The Windows census's floor rises to twenty-six. Gates: `cargo
fmt` applied, clippy clean; the core lib's graph_queries facts (30
passed, the three new included) and the FFI tripwire; the workspace's
tests, 1999 passed, with six failures root-caused and none the change's
— the five dir-tree censuses fail on this box with OS error 123 on
every run (the standing local deviation; CI-Windows is their oracle),
and `perf_guard_root_listing_under_100ms_on_10k_files` failed under the
parallel workspace run and passed alone in 13 s; the binding
regenerated (`generate-bindings.ps1`) and `GraphQuerySurfaceCensus`
green; the whole Windows test project on a fresh build, 2151 passed;
the accessibility project built and PR A's journey
GraphSurfaces_TableSortSelectionAndActivation_AreClean RUN — it failed
at the palette step ("CommandPaletteSearch did not become available",
the 10 s wait) when run directly after the five-minute suite on the
same box, and passed alone in 10 s; recorded as TGA-12 recorded the
canvas journey, and CI is its oracle on the push. The mac suites are
CI's — no Xcode on this box (a verified deviation: the mac lane is the
oracle for the wrapper). Mutations, each restored byte for byte, each
caught by the named fact and by it alone: the filter returns the
default (`connections_filter_is_the_macs_local_filter`); the clamp
returns its argument (`clamp_connections_depth_is_the_named_constants`);
the clamp's name dropped from the surface
(`graph_query_surface_names_pub_fns`).

**TGB-2 — The leaf's document on the workspace's one relay (rule C, Terms
1–8; B-1..B-3, B-5..B-7, B-11, B-12; A-1 and A-10 as amended).**
`Graph/ConnectionsLeafViewModel.cs` is the document: the presentation
(NoNote, Loading, Ready, Error, each keyed by the request that produced
it) and the request in flight beside it; CURRENT and STALE computed from
the presentation's root; the token carrying the root EPOCH; the
two-echo envelope; the receiver's checks in Term 8's order — the newest
sequence's landing clears in-flight before the lifecycle, request, root
and epoch checks decide; the start transition per state; the probe as
Term 6's state machine; the row copy and the actions with the row's hint
its activation's and B-D6's string on the menu action alone; retirement
dropping this document's pending classes from the shared relay.
`WorkspaceViewModel.Connections.cs` is the shell's side: the leaf
constructed after the relay and before the restore; the pending mount
armed by the pane's setter and SEEDED by the constructor, consumed at
every reveal route's end — the toggle, directional focus, Show, Tasks
Review, History, the bibliography jump, the three Bases reveals, and
the outermost mutation boundary; the root recorded inside a mutation and
reconciled once at the boundary (`WorkspaceViewModel.Persistence.cs`,
before PR A's cause clear); Show suppressing the mount and switch its
own reveal causes and issuing ONE load; the probe arm at every level
from `NotifyGraphOfVaultChange`; the drain beside the graph document,
the relay shut down after both. The relay: `NewGraphRelay()` under
`Graph/WorkspaceViewModel.Graph.cs` is the one seed; `GraphAnnouncer`
gains the GATED filter entry whose gate is stored with the pending line
and re-checked at fire, `DropAllPending` made internal; the graph
document's rows-only publish enqueues through it and its `Retire()`
drops instead of shutting down. `WorkspaceViewModel.GraphCreate.cs`
generalised to a SOURCE address — the graph tab's every identity or the
leaf's root and epoch, the leaf's ACTIVE state not consulted.
`GraphCoreConstants` fetches core's constants once per process (0bD-11).
Facts: ConnectionsLeafTests in four partials — the record field by field
and the golden's projection for the six pinned pairs; the filter and the
clamp through core; the four states and the in-flight flag; the start
transition; STALE; the receiver's rejections (ten rewrites of the token
and the echoes, the reversed completion, the unmounted A → B race, the
lifecycle replacement, the shutdown before dispatch); the trigger matrix
route by route with one load each (the seeded launch mount with the leaf
active, with another leaf; a reveal-then-switch command; the mounted
switch current and stale; Show current, stale and from a collapsed pane;
the root change active-and-mounted and not; the split's sole-tab close
as one reconciliation; depth with and without a root and at the bound;
root → none); the probe's state machine (equal, older, in flight,
errored, stale, no root); supersession; SPEAKING at apply (away → back
speaks, hide → away silent); the selection scoped by root and mount and
pruned by a refresh; the shared neighbour; focus entry per state; the row
copy, hint and actions; the open from the leaf; the three commands; the
label inventory. GraphAnnouncerTests gains the stored gate read at fire,
the cross-surface High flush and the live relay after a drop;
GraphDocumentTests' retirement fact asserts the relay live, drained and
shared. Censuses: the instance census (exactly one construction, in
`NewGraphRelay`, over the rendered seam); the seam census's two boundary
files with their own seams; ConnectionsLeafCensus — the trigger entry
points' one owner each, `SyncPanels` the only recorder and the boundary
the only reconciler, every pane reveal consuming and the constructor
seeding, the filter count gated and nowhere ungated, and the depth's
dataflow: the wrapper's body IS the FFI clamp, every `_depth` write its
result, the clamp's one caller `SetDepth` with its parameter, and
`SetDepth`'s producers exactly core's minimum in the constructor,
`_depth + 1`, `_depth - 1` (the view's index conversion joins in TGB-4).
Verified deviations: (i) the first depth request is core's own minimum
through the process-wide accessor — a producer B-19 (v)'s four do not
name; the census names it as the constructor's, and no literal enters
the depth; (ii) the depth census's `Math.*` sweep is scoped to `Graph/`
and the leaf's partial — the reading view's LIST depth
(`Reading/ReadingDocumentBuilder.cs`) is another word; (iii) a
fabricated NEWER sequence is not a rejection shape the theory carries —
an older sequence's landing leaves the newer in flight by design, the
reversed-completion fact its witness; (iv) the gates' journey is PR A's
(the leaf has no view until TGB-4); (v) the receiver's standalone
comparison of the token's root and epoch against the live ones was an
EQUIVALENT MUTANT — the request record carries the epoch and its
identity check rejects the same envelopes, and every root transition
advances the sequence — so it is written once, the sequence advance the
guard the facts pin (the unmounted A → B race), and no mutation that
would survive is carried; (vi) PR A's fact
AGenerationThatArrivesDuringAnInFlightPairSupersedesItAndTheStaleResultInstallsNothing
parked only the stale pair and asserted "nothing installed yet" against
a superseding pair that could land first — a race the leaf's probe on
the same vault-change arm widened (it failed one run in three alone);
the fact now parks the superseding pair too and passed five runs
running. Gates: `cargo fmt` clean, clippy clean; the workspace's tests
1999 passed with the standing six — the five dir-tree censuses (OS
error 123 on this box; CI-Windows their oracle) and the perf guard
under the parallel run, which passed alone in 13.7 s; `dotnet format`
applied; the whole Windows test project on a fresh build, 2219 passed
(the 120 graph and leaf facts and censuses among them); the
accessibility project built and PR A's journey RUN — it failed at the
palette step directly after the five-minute suite, as at TGB-1, and
passed alone in 10 s; CI is its oracle on the push; the mac lane is
CI's. Mutations, twenty, each restored byte for
byte, each caught by the named fact: the relay shut down at the
document's retirement (the retirement fact); the gate ignored at fire
(the stored-gate fact); a second relay instance (the instance census);
speech unconditional (the away-and-back fact); a load on a current
mounted switch (the current-switch fact); two loads on a collapsed Show
(the collapsed-Show fact); a reveal-then-switch command not consuming
(the Tasks Review fact); the seeded launch mount skipped (the launch
fact); reconciliation inside the mutation (the split's sole-tab close);
the probe superseding a load in flight (the in-flight probe fact); an
errored leaf unprobed (the errored-and-stale probe fact); the open on a
stale epoch (the excursion fact); the open suppressed on an inactive
leaf (the inactive fact); no sequence advance on a root transition (the
unmounted race); the bundle's echo unchecked and the centre key
unchecked (the rejection theory); a host `Math.Clamp` and an aliased
pre-clamp in Deeper (the depth census); the disabled reason on the
row's hint (the row fact); the filter count enqueued ungated (the gated
entry census).

**TGB-3 — The ghost create addressed by the leaf (B-11) landed with
TGB-2**: the SOURCE address (`GraphCreateSource.GraphTab` — PR A's every
identity — or `GraphCreateSource.Leaf`, the root and its epoch), the
completion's order kept (the open, then the one `NoteCreated`, then the
caveat), the leaf's ACTIVE state not consulted (B-D10); the four ghost
facts (lands, opens and speaks; parked across a root move; parked across
A → B → A; parked while the leaf went inactive) and their mutations are
TGB-2's.

**TGB-4 — The tree view in the outline's shape (B-6, B-8, B-9, B-13,
B-16; rule C, Terms 2 and 9).** `Graph/ConnectionsLeafView.cs`: the row
view model (group, empty marker, connection row named by core's row copy
through the relay, ItemStatus the badge in the mac's precedence,
HelpText the activation's hint, the depth-one snippet overlaid by exact
path), the tree's peer trio — the tree peer overriding the item-peer
factory, the DATA peer carrying Invoke beside WPF's SelectionItem,
ExpandCollapse and ScrollItem, and the item peer projecting NESTED rows
through the same factory (`CanvasOutlineView.cs:171–290`); Standard
virtualisation; the model released on null; the view-local expansion,
selection and pending focus keyed by the root and cleared on a root
change, pruned on a same-root refresh, cleared on a collapse through
the document's selection; the mac's `nest` in one pass; the heading
(the note's file name; "Connections" with no note), the summary region
(core's render), the depth ComboBox (T17, the tags core's window in
order, the selection mapped to the model's `SetDepth` by the tag's
position — the view's one producer), the state label (T1–T5) under
Term 9's ANCHOR, a focusable host element with the state's accessible
name; focus ENTRY — from outside the leaf only — posting the document's
line; the row menu built from core's action inventory with the disabled
Show connections carrying B-D6's reason as its HelpText and the row's
hint left the activation's; Return, Ctrl+Return, Alt+Up / Alt+Down
between the groups, Menu and Shift+F10; the container realised before a
focus delivery (the outline's walk). `MainWindow.xaml`: the leaf body
gated on `connections` hosting the view bound to the workspace's public
document; the placeholder's collapse trigger for the leaf; the right
pane's landmark named RightPaneBorder. `MainWindow.xaml.cs`: the
right-pane focus boundary lands on the leaf's anchor when the leaf is
active (Term 9); the pane-navigation route keys on the whole right pane
so focus inside a leaf's tree goes LEFT to the editor and RIGHT to the
shell's terminal line, never through the editor-geometry fallthrough
(IGH-6). Facts: ConnectionsLeafViewTests — the four patterns on a
top-level and a nested data peer with the occurrence as the automation
id and the row's Name, ItemStatus and HelpText; expansion surviving a
same-root refresh and cleared on a root change; the model's release and
the state labels per state; the empty neighbourhood under the summary
with the anchor taking focus; the depth control's Name, HelpText, tags
and mapping both ways; the row menu's inventory with Show connections
disabled and its reason on the action. ConnectionsLeafCensus gains the
XAML fact: the body gated on the id, the view bound to `Connections`,
the placeholder's trigger, the named boundary and the window's two
routes. Verified deviations: (i) Term 9's RIGHT route reuses the
shell's existing terminal line (`AnnounceNoPaneInDirection`), the rail's
own; (ii) the focus-out routes are pinned by the journey (TGB-6), not
headlessly — the window's key route is a preview handler over the real
window; (iii) the view clears its expansion and pending focus on the
document's VIEW-STATE EPOCH, which the document advances on a root
change and on a collapse and on nothing else — the first sweep found
the view's root-change clear shadowed by its reaction to the
document's cleared selection, which would also have cleared on a prune
against B-6, so the epoch is the one signal and a vanished selection is
not a clear — and the view compares the epoch's VALUE, since the second
sweep found it clearing on the property's notification alone; the
root-change clear is pinned by a shared-neighbour fact (two roots
linking the same note reuse the occurrence id, so the prune alone would
keep the memory); (iv) the first sweep also found the patterns fact
accepting a pattern OBJECT from a peer that no longer implemented the
provider — the fact now asserts the four provider interfaces. Gates:
`cargo fmt` clean, clippy clean; the workspace's tests 2000 passed with
the standing five dir-tree censuses (CI-Windows their oracle) and the
perf guard green this run; `dotnet format` applied; the whole Windows
test project on a fresh build, 2227 passed (the 72 leaf, view and
census facts among them); the accessibility project built and PR A's
journey RUN — failed at the palette step directly after the five-minute
suite, the third such run, and passed alone in 9 s; the local gates
script now runs the journey twice so the first launch's cost is
visible in one run; CI is its oracle on the push; the mac lane is
CI's. Mutations, nine, each restored byte for byte, each caught by
the named fact: the view keyed by node id, the Invoke provider dropped
from the data peer, the nested rows' factory removed (the patterns
fact); the epoch not advanced on a root change, the view clearing on a
prune, the collapse not advancing the epoch (the expansion fact); the
reason dropped from the menu action (the menu fact); the placeholder's
trigger dropped and the focus route through the editor geometry (the
XAML census).

**TGB-5 — The three chordless rows, the matrix rows and their censuses
(B-14, B-19, B-20).** `Commands/ChordTable.cs`: `slate.graph.showConnections`,
`slate.graph.connectionsDeeper`, `slate.graph.connectionsShallower` in
`GraphRows()` through `Reg` — chordless, `ChordScope.None` by Reg's rule,
section Graph, the labels and hints the mac's byte for byte
(`SlateCommands.swift:1516–1535`); `Commands/SlateCommandRegistrar.cs`:
the three resolvers to the workspace's `ShowConnectionsCommand`,
`ConnectionsDeeperCommand` and `ConnectionsShallowerCommand` — always
enabled, a bound a no-op through core's clamp (the mac's guard).
`chords.json` regenerated through the projection; its delivery evidence
maps the three ids to the graph group, whose implementation references
gain the leaf's document, the Show route and the view, and whose test
references gain ConnectionsLeafTests and the journey.
`scripts/generate-parity-matrix.py`: the three ids join
W6_2_DELIVERED_COMMANDS and `parity_matrix.md` is regenerated — the
three rows move from pending to W6_2_STATUS. `w_c_matrix.md` gains the
row "Graph connections leaf (W6-2 PR B)" — its ten cells naming the
leaf's automation ids, the tree's control types and patterns, the
Name / ItemStatus / HelpText sources, the routes, the announcement
family, the evidence and the axe label — and
`Censuses/WcMatrixGraphEvidenceCensus.cs`'s manifest gains the
matching surface. Facts: ChordTableTests — the three rows present,
chordless, scope None, the labels the mac's; the registrar's resolvers
resolving through the palette; the chords projection; the parity
matrix's three statuses (`Censuses/ConnectionsLeafCensus`'s `TheParityMatrixCarriesTheThreeRowsAtTheW62Status`); the W-C matrix
row's ten cells, ids, control types, patterns, name sources, evidence
names and axe label. Gates: `cargo fmt` clean, clippy clean; the workspace's tests 2000 passed with the standing five dir-tree censuses (CI-Windows their oracle) and the perf guard failing in sequence and passing alone (12 s); `dotnet format` applied; the whole Windows test project on a fresh build, 2230 passed (the model fact, 31 s, among them); the accessibility project built and the leaf journey RUN — it failed at the palette step on both attempts when the gates ran from a BACKGROUNDED shell after the five-minute suite (a process launched without the foreground cannot take it, so the first chord never reached the app), and passed alone in the foreground in 10 s, the fourth such pass this session; the local gates now run from the foreground; CI is its oracle on the push; the mac lane is CI's. Mutations, each restored byte for
byte, each caught by the named fact: the Deeper resolver dropped (`RegistryHoldsExactlyTheDeclaredCatalog_BothDirections`); Show Connections' label drifting from the mac's (`EverySharedCommandIdCarriesMacsLabel`); the projection stale by one label (`ChordsJson_IsExactlyTheTablesProjection`); the W6-2 status stale by a day (the parity fact); the axe scan dropped from the journey and the record fact dropped from the matrix row (`EveryEvidenceNameResolvesAndEveryAxeLabelIsScanned`); and Show Connections dropped from the delivery evidence's command map, which SURVIVED — the scope-completeness census walks the issues' commands and never saw a delivered id missing from the map — so `Censuses/DeliveryEvidenceCensus` gains `EveryImplementedCommandMapsToACommandGroup` (every command the parity matrix records as implemented maps to a group) and the mutation is caught by it.

**TGB-6 — The journey (B-18, §W-C), and the ghost create's heal it
found.** `GraphConnections_LeafWalkDepthAndReRoot_AreClean` over a
four-note vault (Alpha → Beta, Gamma, the ghost Missing Note; Beta →
Alpha, Delta; Gamma → Alpha; Delta → Beta), its expected labels core's
renders through the binding BEFORE the app opens the vault (the
accessibility project gains the SlateUniffi reference for it; the
journey carries the mac's group-header and badge literals itself, the
shell's `ConnectionsPhrase` being internal): Alpha opened through the
graph table (PR A's route), Show Connections through the palette, the
heading `Alpha.md`, the tree (ControlType Tree, Selection), the summary
region's exact render, both group headers with their counts, the first
outgoing row's Name and ItemStatus and its four patterns, the depth
control's Name, HelpText and value "Links", the change to "2 links
away" with the summary's changed render, the nested Delta row under
Beta with its four patterns (the containers regenerate on the layout
pass after the depth change, so the row is the one WITH nested items),
the ghost row's Name and "Unresolved", Enter landing the file and the
created note becoming the root, the healed neighbourhood — "Linked
from, 1 note" with Alpha, "Links to, 0 notes" — Ctrl+Alt+Left from the
Alpha row landing in the editor, and axe over `graph-connections`.
Verified deviations: (i) the app writes no announcement log, so the
timeline's `NoteCreated` line and the Show line are pinned headlessly
(ConnectionsLeafTests), the journey pinning the surface; (ii) THE
FINDING — the created note read "This note has no connections." for
as long as the journey waited: core re-resolves a ghost's rows only at
scan, rename and move time (`create_note_becomes_node_without_resolving_old_ghosts`
pinned it: "creating a file does NOT heal existing ghost rows"), the
mac's `createNoteFromGhost` runs its structural refresher — a full
scan (`AppState.swift:1663`) — before it opens the note, and the
Windows shell never rescans after a structural mutation (its create
funnel's landing is the sidebar's barrier and tree relist, A-8), so
the leaf's probe found the generation moved (the new node) and loaded
a neighbourhood with Alpha's link still a ghost. Fixed in CORE, the
smallest parity-true repair: `create_exclusive_binding` (`session.rs`)
calls `re_resolve_unresolved_links` inside the create's own transaction
and replays each affected source's linkset to the graph — the rename
and batch-move paths' exact pattern — so SQLite heals at scan, move AND
create time and the graph keeps replaying it; the pinned test is
rewritten as `create_note_heals_matching_ghosts_in_its_own_transaction`
(the ghost merges at the create, the edge points at the real note
before any scan, a later scan changes nothing) with
`create_bytes_heals_matching_embed_ghosts_too` for the bytes sibling
(an attachment heals its embeds); the census op-pair sweep's
"materialize+scan" op keeps agreeing with the rebuild; the mac's
follow-up scan becomes belt and braces; no host test pinned the old
order. B-11's "the probe follows the create's generation change" now
delivers the healed neighbourhood, which the journey asserts; the
owner's attention is drawn to the core change in the PR body; (iii)
the first launch after a fresh build and the five-minute suite pays
the JIT and scan cost inside the palette's wait (TGA-12's precedent),
so the local gates run the journey twice and the second run is the
verdict; (iv) the view's rows were rebuilt two or three times per
publication (IsCurrent, IsStale and the publication each rendering),
which the journey saw as stale group handles — the view now renders a
publication once (`_renderedPublication`) and re-seats the selection
otherwise, ConnectionsLeafViewTests unchanged. Gates: `cargo fmt` clean, clippy clean; the workspace's tests 2000 passed with the standing five dir-tree censuses (CI-Windows their oracle) and the perf guard failing in sequence and passing alone (12 s); `dotnet format` applied; the whole Windows test project on a fresh build, 2230 passed (the model fact, 31 s, among them); the accessibility project built and the leaf journey RUN — it failed at the palette step on both attempts when the gates ran from a BACKGROUNDED shell after the five-minute suite (a process launched without the foreground cannot take it, so the first chord never reached the app), and passed alone in the foreground in 10 s, the fourth such pass this session; the local gates now run from the foreground; CI is its oracle on the push; the mac lane is CI's.
Mutations, each restored byte for byte, each caught by the named fact:
the heal removed from the create's transaction, and the heal kept but its linksets not replayed to the graph — each caught by both core facts; the journey's Create step is its own witness, a shell mutation that fails the journey being restorable only with the desktop in the loop, so the two core mutations stand for it.

**TGB-7 — The MODEL fact (B-10).**
`ConnectionsLeafTests.Model.cs`: an executable derivation of Terms 2–9
— `Derive(cell)` returns the shell lines, the relay lines (the summary
a placeholder resolved after the settle), the loads the route issues
synchronously, and the final root and staleness — over nine routes
(the switch to the leaf, the switch away, Show, the pane toggle, a root
change, a depth change, root → none through the tab close, the Tasks
Review command, focus entering the anchor) crossed with the pane
(visible, collapsed), the leaf (Connections, another), the root (a
note, none), the presentation (current, stale) and the in-flight flag:
288 cells (the counts first recorded here, 192 and 69, were miscounted — corrected in the pass-1 sweep, TGB-8), of which 165 are not states of the system and the model NAMES
why (no root has no stale presentation and nothing in flight; a root
transition clears the flag, so stale never has a load in flight; an
active and mounted leaf with a root has loaded, so stale is only ever
unmounted or inactive at rest; focus enters the anchor only while the
leaf is shown), and 123 are driven — each from a fresh copy of the
graph vault, arranged (the note opened while active and mounted for a
current presentation, then moved away from or hidden; opened while
inactive for a stale one; the last load left pending for the flag),
the timeline cleared, the route applied, the loads compared at once,
the workspace settled, and the timeline, root and staleness compared
with the derivation; every divergence is listed by cell. Thirty-one
seconds. Findings: (i) the first derivation gated speech on ACTIVE AND
MOUNTED and eleven cells diverged — the implementation speaks a
completion after a hide while the leaf stays active, and a depth change
from the palette with the pane collapsed — which is the frozen text
(Term 2: "MOUNTED is not consulted"; B-D3: "speech gates on the active
leaf alone, the mac's predicate"), so the model was corrected, not the
code; (ii) an open that creates the FIRST tab posts the shell's
`TabFocused`, an in-place open into an existing tab none — the model
carries both. Mutations only the model catches, each restored byte for
byte: the workspace's ACTIVE predicate consulting the pane; the graph
family's panel line skipped when Show revealed the pane — each caught by the model fact and by no other.

**TGB-8 — Post-implementation pass 1 (IPB-1..5) and codoki's two items:
the sweep.** PR #1184 opened on 5c3a08b with CI green on the head (13
checks), codoki APPROVED with two items, zero threads; codex's first
post-implementation pass over the branch (xhigh, PR A's precedent)
returned five findings — two blockers, three majors, no minor — and
the verdict "not mergeable as is". Each, and its discharge in code:
(IPB-1, blocker) the create's `Exists` and `Failed` arms posted
`GraphBlocked{NoteCreateFailed}` through the SHELL seam, bypassing the
one relay's High flush (A-10 as amended) — a filter count or a leaf
line pending on the relay could speak after the failure; both arms
now ride `_graphRelay`, the NoteCreated post alone stays A-8's direct
one, the seam census counts that method's graph-family creations
without `Distinct()` (a second is a second entry), and
`AFailedCreateRidesTheRelayAndItsHighFlushDropsAPendingClass` queues
a filter count on the workspace's relay, fails a create through a
deterministic creator, and proves the count dropped and the failure
the only line; PR A's existing-destination fact reads the relay's
rendered line instead of the shell seam. (IPB-2, blocker) Bases' Show
backlinks — its row command and its surface seam, two copies —
opened the note in an OUTER mutation of its own, so the boundary
reconciled the Connections root while the leaf was still active and
mounted and loaded audibly, then switched to Backlinks: a route whose
final leaf forbids the load (Term 3, B-10's final-state rule). ONE
helper, `ShowBacklinksFor`, wraps the open, the switch, the reveal and
the consume in one outer `RunWorkspaceMutation`, the Bases line after
it; both sites call it; the route joined the model (ShowBacklinks:
zero loads, STALE, the new root) and a mutation restores the old
shape and fails the model. (IPB-3, major) TGB-7's cardinalities were
miscounted — five binary dimensions over nine routes are 288 cells,
165 named, 123 driven, not 192 and 69 (the record is corrected in
place) — and the fact pinned only `driven >= 100`; and the nine
routes were far short of B-10's worked rows. The model now drives
TWENTY-FIVE routes: the leaf switch both ways, Show, the pane toggle,
a root change, a depth change and Deeper at the bound, root → none,
the Tasks Review and History commands, Bases' Show backlinks, focus
entry, tab activation to another note and to a duplicate, the tab
close with another successor and with a duplicate's, the split, the
group close onto the same root and onto another, the ghost create and
its failure, the rename and the delete of the root, the launch and the
shutdown — 800 cells, 498 named as not states of the system (each
with its reason: no root has no stale presentation or load in flight;
a root transition clears the flag; an active mounted leaf with a root
has loaded; focus enters only a shown leaf; the tab routes need a
second tab; a ghost row is activated from a rendered tree; a launch
restores no presentation, no load in flight and, the pane's visibility
not being persisted, comes up mounted), 302 driven, the four totals
and the route count pinned as literals; the loads are compared after
the settle (a probe's load is asynchronous) and the root by name. The
extension found TWO things in the code and two shell lines: (a) a
SILENT reload that fails spoke its failure line whenever the leaf was
active — the mac's `speak = announce && active` gates the failure as
it gates the summary (`AppState+Connections.swift:118, 135`), B-10's
probe row says "nothing" — so the receiver's failure arm now consults
the token's policy like its success arm (Term 5), pinned by the
DeleteRoot cells and a mutation; (b) nothing else in the code — the
rest were the derivation's: the close's `TabFocused` precedes
`TabClosed`; a retarget and a launch re-derive the outline, whose
`OutlineCount` is the shell's line; the delete is driven as the
lifecycle sees an external one (core's own delete trashes through
COM and wants a thread of its own). Rows the model does not drive and
what pins them: the directional-focus routes (the journey), the
table's sort and count against the one relay (GraphAnnouncerTests'
cross-surface flush), the non-note tab's own lines (PR A's facts), the
vault replacement (the lifecycle fact and Launch). (IPB-4, major) the
instance census read a target-typed `new(...)` by the enclosing
method's return type first, so `GraphAnnouncer extra = new(...)`
inside a void method was invisible: the census now compiles the
shell's own trees (no metadata — the shell's types bind, the
framework's do not, and the relay is the shell's) and asks the
semantic model, the enclosing declaration its fallback, the local's
before the method's; the exact shape is a mutation. (IPB-5, major)
the trigger census tested the receiver's SPELLING (`Connections` or
`leaf`) and the reveal census any consume in the member: the receiver
is now RESOLVED through the member's locals (a local bound to the leaf
or to its construction, a local or parameter declared as the leaf) and
the consume must FOLLOW the assignment in its own block or an
enclosing one; a call through a local and a consume before the reveal
are mutations. Codoki: every FFI-crossing counter takes one locked
increment (`CountCrossing`); the high-water mark's fact parks a
fetch, marks, and proves the silent reload fires only when the mark is
strictly above the installed tree's generation and clears afterwards,
an equal mark reloading nothing — two mutations (`>=`; the clear
dropped). Gates: `cargo fmt` clean, clippy clean, the crates byte-identical to ac6504b's (the workspace's run of 2000 passed with the six known local failures stands); `dotnet format` applied; the whole Windows test project on a fresh build, 2232 passed (the model's 302 cells, 78 s, among them); the accessibility project built and the leaf journey passed twice in the foreground, 10 s each; benchmarks build; CI and codoki on the push are the oracle. Mutations, each restored byte for byte, each
caught by the named fact: the Failed arm back through the shell seam (`AFailedCreateRidesTheRelayAndItsHighFlushDropsAPendingClass`) and the Exists arm too (the seam census's one-site count); Show backlinks outside the mutation (the model's ShowBacklinks cells); a target-typed second relay in a void method (`ExactlyOneGraphRelayIsConstructedInTheShell`); a trigger through a local (`EveryTriggerEntryPointHasExactlyTheCallersRuleCNames`); the consume before the reveal (`EveryPaneRevealConsumesThePendingMountAndTheConstructorSeedsIt`); the silent failure spoken (the model's DeleteRoot cells); the high-water mark's equal reload and its clear dropped (`TheHighWaterMarkReloadsOnlyWhenStrictlyAboveTheInstalledTreeAndClearsAfterwards`) — nine, none survived.

**TGB-9 — Post-implementation pass 2 (IPB-6..11): the sweep.** On
d81cc4f, CI green (13 checks), codoki's second review a false positive
(a C# 12 collection expression read as invalid syntax; refuted with
the build check's link and resolved) and a request to restore the very
fallback order IPB-4 found wrong; codex's second pass returned six
findings — one blocker, four majors, one minor — and confirmed IPB-1
and IPB-2 discharged. Each, and its discharge: (IPB-6, blocker) the
view's dropped rows kept their activation delegates and their
expansion handlers, so a screen reader's cached automation peer could
invoke a row of a tree the document no longer held — for a ghost, the
stale target paired with the CURRENT root and epoch. Two walls: the
view releases every dropped row (the delegate nulled, the handler
detached, recursively) on a root change, a refresh and the model's
release; the document acts on a row only while the current
publication's tree lists its occurrence id (B-6: a same-root refresh
keeps the ids, so the rows of an earlier tree of the same root are the
same rows), `IsRowCurrent` in `Execute`'s admission. Facts:
`ARowOfATreeTheDocumentNoLongerHoldsIsInertAndARefreshedRootsRowIsNot`
(a ghost row and a note row of Hub's tree, neither listed by Two's,
inert after the root moved — no create, no open, no line — and current
after a depth refresh) and `ADroppedRowIsInertForACachedPeer` (the
cached row's delegate gone after a root change and after the release,
its toggle writing no expansion memory). (IPB-7, major, reopening
IPB-3) the model excluded reachable states and read its expected
summaries from the workspace's own publication. Now: the root has
THREE states — a note, no tab, and the GRAPH tab in view with a note
beside it (its tab activation, its close and its restore derived; the
singleton's duplicate, split and group routes named as refused by
their commands); the presentation THREE — Ready and current, stale,
and ERROR (a missing path, every audible load's line the failure core
reports, the probe's silent reload over it, the switch that does not
reload it); a ghost is actionable during a same-root reload (Term 7
keeps the rows — the in-flight load for the ghost routes and the Error
presentation is Deeper's, the rows or the Error kept); FIVE more
worked routes — the non-note activation (PR A's Opened and snapshot
summary through the same relay, Opened alone when already effective),
the split's sole-tab close (A → none → B in one mutation), the ghost
create onto an already-open tab, its dirty-navigation refusal, and its
source moved while parked; THIRTY routes, 2160 cells, 1549 named, 611
driven, the totals pinned; every expected line — a tree's summary or a
failure — derived from the FIXTURE through the session for the root
and depth the derivation names, never from the publication; the root
and the depth asserted always. Making "in flight during the route" a
STATE rather than a race: a load left in flight is PARKED inside its
fetch after its crossings (the seam now parks a failing fetch too) and
released only after the route's synchronous part; the probe's decision
is awaited before that release (the mark, or the load); a ghost route
drains the creation without settling the leaf. The extension found
nothing further in the code — the derivation's own corrections were
the close's `TabFocused` before `TabClosed`, the outline's count on a
retarget and on a launch of an existing note, and the graph document's
own drain where a route seats one. (IPB-8, major, reopening IPB-4) an
explicit creation through a `using` alias evaded the spelling test:
every creation, explicit or target-typed, is now BOUND over one
compilation of the shell's own trees with the test process's metadata
(`Support/ShellCompilation.cs`, the SDK's implicit usings supplied) and
compared with the relay's fully qualified symbol; an alias naming the
relay anywhere in the shell fails outright. (IPB-9, major, reopening
IPB-5) the trigger census now binds every invocation to its method
symbol — a call through a field, a property, a cast or a method group
counts like any other, and the document's own two `AnnounceStatus`
calls (Term 9) join the expected owners — and the reveal census walks
Roslyn's control-flow graph: every path from the reveal to the member's
exit must pass a consume, so a `return` between them is the offence it
is (the syntactic order stays the fallback for a member the graph is
not built for). (IPB-10, major) the depth census admitted the view by
path: the view's ONE producer is named exactly, the depth control's
selected index converted. (IPB-11, minor) the mac's `Int` clamp
converted with `UInt32(max(0, depth))`, which traps above
`UInt32.max`: `UInt32(clamping:)` saturates both ways, and the test
takes `Int.min` and `Int.max`. Gates: `cargo fmt` clean, clippy clean, the crates byte-identical to ac6504b's (the workspace's run stands); `dotnet format` applied; the whole Windows test project on a fresh build, 2233 passed without the model, and the model alone, 611 cells in 2 m 38 s — run concurrently with the suite once, the model showed the graph tab's arrangement load landing inside a route, so the arrangement now drains the graph document before the timeline clears; the accessibility project built and the leaf journey passed twice in the foreground, 10 s each; benchmarks build; the mac's XCTest lane is CI's, where the clamp's new cases run; CI and codoki on the push are the oracle. Mutations, each restored
byte for byte, each caught by the named fact: the stale row acting (`ARowOfATreeTheDocumentNoLongerHoldsIsInertAndARefreshedRootsRowIsNot`); the view keeping a dropped row's delegate (`ADroppedRowIsInertForACachedPeer`); the reconciliation ignoring the mount (the model's collapsed root-change cells); a second relay through a `using` alias in a class of its own (`ExactlyOneGraphRelayIsConstructedInTheShell`); a trigger through a property (`EveryTriggerEntryPointHasExactlyTheCallersRuleCNames`); a return between the reveal and the consume (`EveryPaneRevealConsumesThePendingMountAndTheConstructorSeedsIt`); a literal producer in the view (`EveryDepthWriteIsTheFfiClampsResultAndOnlyTheNamedProducersReachIt`) — seven, none survived.

**TGB-10 — Post-implementation pass 3 (IPB-12..18): the sweep.** On
95387c9, CI green (13 checks), no thread open, codoki re-raising its
false positive twice over ("remains open from a previous review") with
its confidence at 1/5 — the one flagged assertion now uses an explicit
array, a concession to the reviewer's fixation and not a defect; codex's
third pass returned seven findings — one blocker, six majors, no minor.
Each, and its discharge: (IPB-12, blocker, reopening IPB-6) the row
menu's click closures captured the document and the core row, and
after a root change the leaf did not load for — inactive or unmounted,
Term 3(d) — the OLD tree stays rendered and STALE, so the document's
occurrence check admitted a row of it against the root that replaced
it. Two walls again: the document acts on a row only while the current
publication's tree was PRODUCED for the current root
(`IsRowCurrent` compares the producing request's root), and a menu
item acts only through a row the view still holds and a model it still
shows — a collapse and a root change both RE-RENDER (the epoch clears
the view state and the rebuild releases every dropped row), so the
mac's destroyed view has its twin in released rows; a suspension flag
written first alongside proved redundant (its mutation survived: the
release already made the item inert) and was removed. Fact:
`ACachedMenuItemIsInertAfterACollapseAndAfterAStaleRootChange` (a
cached item opens nothing after the collapse; after the inactive root
change the re-render released the row and the document refuses its
record). (IPB-13, major) `StartWorkAlwaysAsync` tracked the task an
async method returned AFTER that method's synchronous prefix had
queued the pool body, so a drain on another thread could see an empty
set while a compute was live: a placeholder joins the tracked set
under the lock BEFORE the worker is scheduled and completes, faulted or
not, with the real task;
`AComputeStartsOnlyAfterItsRegistrationAndADrainWaitsForIt` reads the
tracked count and a drain's state from inside the compute. (IPB-14,
major, reopening IPB-7) the model's "current, in flight" cells were
the root's FIRST load parked — Loading, no tree — while the dimension
said Ready: the presentation gains LOADING as its own value (the flag
set by definition), and a load in flight over Ready or Error is now
always a same-root RELOAD with the rows or the Error kept (Term 7) —
Deeper's, parked — the arranged state asserted before the route (the
flag, the publication's state, the root, the pending request's depth);
at the bound over a current tree no reload can be issued (Deeper is
the clamp, the probe finds the tree equal), named. (IPB-15, major,
reopening IPB-7) the graph tab in a pane of its own beside a note's
pane: the pane's close onto the note and the graph's sole-tab close
are driven (a root change from none); the close onto the SAME root
would need a second non-note and stays named. THIRTY routes over three
roots and FOUR presentations: 2880 cells, 2172 named, 708 driven, the
totals pinned. (IPB-16, major, reopening IPB-8) the instance census
counted allocation sites: a second `NewGraphRelay()` call would have
been a second relay unseen — the factory's calls and its method-group
references are censused, bound: the constructor's one call, nothing
else. (IPB-17, major, reopening IPB-9) a delegate-bound call (`Action
fire = Connections.Mounted; fire();`) hid behind `Action.Invoke`, and
so would a `SetDepth` through a delegate: every method-group reference
to a trigger or to `SetDepth` is an offence, every call to one of those
names the compilation binds to nothing is refused rather than trusted
(fail closed), the depth producers are bound rather than spelled, and
the compilation now carries the XAML-generated partials from `obj/`
(the newest copy of each) so a call through an `x:Name` field binds.
(IPB-18, major, reopening IPB-9) the reveal inside the ToggleRightPane
command's lambda fell back to statement order: the control-flow graph
now descends into every anonymous function or local function enclosing
the reveal, the consume is bound to the workspace's own method, and a
reveal whose scope cannot be analysed is an offence (no fallback).
Gates: `cargo fmt` clean, clippy clean, the crates and the mac sources byte-identical to 95387c9's (their runs stand); `dotnet format` applied; the whole Windows test project on a fresh build, 2235 passed without the model; the model alone, 708 cells in 3 m 4 s — run concurrently with the suite it starved a probe wait, and alone it found the wait itself wrong for a rename with the leaf active (the new root's load may complete before the probe decides, a valid order), so the driver now waits on the leaf's tracked-work count, which falls only once the probe's own task has applied; the accessibility project built and the leaf journey passed twice in the foreground, 10 s each; benchmarks build; CI and codoki on the push are the oracle. Mutations, each restored byte for byte, each caught by
the named fact: the menu item ignoring a released row (`ACachedMenuItemIsInertAfterACollapseAndAfterAStaleRootChange`); the stale tree's row acting (the same fact, the document's wall); the registration after the worker, the caller parked at the seam (`AComputeStartsOnlyAfterItsRegistrationAndADrainWaitsForIt`); a second factory call (`ExactlyOneGraphRelayIsConstructedInTheShell`); a trigger through a delegate (`EveryTriggerEntryPointHasExactlyTheCallersRuleCNames`); the depth through a delegate (`EveryDepthWriteIsTheFfiClampsResultAndOnlyTheNamedProducersReachIt`); a return inside the command lambda (`EveryPaneRevealConsumesThePendingMountAndTheConstructorSeedsIt`) — seven, none survived; the suspension flag's mutation, which survived, took the flag with it.

**TGB-11 — Post-implementation pass 4 (IPB-19..28): the sweep.** On
c1938c5 the standing gate was met — CI green (13 checks), codoki
"auto-approved (no issues found)" on that head, no thread open — and
codex's fourth pass returned ten findings: no blocker, ten majors, no
minor (its validation note: the lifecycle receiver fact could not reach
its graph setup in codex's environment, where the spell-checker's COM
creation is refused; the fact runs here). Each, and its discharge:
(IPB-19, major) the leaf's Reveal handed the path to the GRAPH
surface's reveal, which returns before the sidebar seam when no graph
tab is effective — so the standalone leaf's Reveal did nothing without
a graph tab beside it (B-9): the leaf's reveal is its own
(`RevealConnectionsRowFromSurface`) and reaches the sidebar seam
directly; fact `RevealFromTheLeafReachesTheSidebarWithNoGraphTab`.
(IPB-20, major) the create worker built its synchronization context
but handed the scheduler no owner dispatcher, so a completion posted
after the dispatcher's shutdown could neither be observed nor
withdrawn: the worker's constructor chain passes its dispatcher and an
aborted post withdraws the promise; fact
`ACreateCompletionAbortedByTheDispatchersShutdownWithdrawsItsPromise`.
(IPB-21, major) `Park` waited ten seconds and discarded the result,
and two receiver facts started the load before the seam was armed: the
parked fetch is an object that holds thirty seconds and records a
time-out the model reports as a divergence of its own ("the parked
fetch resumed on its own before the route released it"), and every
park is armed before the load it parks. (IPB-22, major, reopening
IPB-14) Loading with nothing in flight — the newest sequence landed
REJECTED, the flag clear and the presentation still Loading (Term 7) —
was named unreachable: it is arranged through a rejected first-load
envelope (the seam rewrites the tree's path echo) and driven across
every route, the model asserting nothing in flight before it. (IPB-23,
major, reopening IPB-7 and IPB-14) the model read the pending load's
audibility off the ROUTE: the cell gains a third dimension — nothing,
an AUDIBLE load or a SILENT one in flight — the audible one Deeper's
reload over a tree or an Error (Shallower first at the bound), the
silent one the probe's over a moved vault, an Error, a Missing or a
Loading; the arranged token's policy is asserted and the derivation
speaks the completion iff the token is audible and the leaf active at
apply (Term 5, B-D3). (IPB-24, major, reopening IPB-14) Deeper at the
bound with a reload in flight IS reachable — Shallower then Deeper,
the second parked: driven, the route the clamp's no-op and the parked
reload speaking after it; the Error arm at the bound injects two
failures, since the Shallower that precedes the parked Deeper would
otherwise heal it. (IPB-25, major, reopening IPB-15) the pane close
onto the SAME no-note root: a canvas beside the note in the surviving
group, the graph in a pane of its own — the close lands on the canvas
and the root stays none; driven. (IPB-26, major, reopening IPB-7)
Error was the missing root's: the presentations split — ERROR a
transient failure over an existing root (the fetch seam throws once,
the envelope carries the failure), MISSING the root that is not in the
graph (its own Error state, from core) — so the rename of an Error
root is driven and only the rename of a missing one is named (core
refuses it); the fetch seam runs once per fetch, after the fetch's own
try/catch, on either path. (IPB-27, major, reopening IPB-16) the
instance census accepted one factory EXPRESSION wherever it sat: the
one call must be the direct right-hand side of the field's assignment
in the constructor with no loop, lambda or branch above it — a call
inside any of those is an offence; fact
`ExactlyOneGraphRelayIsConstructedInTheShell`. (IPB-28, major) the
recorder and reconciler census recognised the textual callee: bound
now, and every method-group reference to a recorder or a reconciler is
an offence, so a delegate cannot carry one; fact
`SyncPanelsIsTheOnlyRecorderAndTheBoundaryTheOnlyReconciler`. The
model, rebuilt to these: thirty routes over three roots, FIVE
presentations and THREE loads in flight — 5400 cells, 4017 named, 1383
driven, the totals pinned; every cell now compares the publication's
STATE after the route beside the timeline, the loads, the root, the
staleness and the depth. What the rebuild taught: STALE at rest is
NoNote over a root (the note opened while the leaf was inactive — a
root the presentation is not for); a switch to the leaf speaks the
audible completion parked before it (IGH-14's other half — the
derivation had read the leaf's activity before the route); the split's
sole-tab close over a missing survivor holds Two Ready, so its silent
reload needs the vault moved; the graph tab's summary after the
arrangement's bump counts the note added (the fixture reports both
lines from core over a copy of its own); a launch with another leaf
restored active records the root and loads nothing. Gates: the crates and the mac sources byte-identical to c1938c5's, so TGB-10's cargo runs stand; `dotnet format` applied; the whole Windows test project on a fresh build, 2238 passed without the model; the model alone, 1383 cells in 5 m 55 s; the accessibility project built and the leaf journey passed twice in the foreground, 10 s each; benchmarks build; CI and codoki on the push are the oracle.
Mutations, each restored byte for byte, each caught by the named fact:
the reveal through the graph surface's address (`RevealFromTheLeafReachesTheSidebarWithNoGraphTab`); the create worker without its dispatcher (`ACreateCompletionAbortedByTheDispatchersShutdownWithdrawsItsPromise`); the factory call inside a lambda (`ExactlyOneGraphRelayIsConstructedInTheShell`); a recorder through a delegate (`SyncPanelsIsTheOnlyRecorderAndTheBoundaryTheOnlyReconciler`); and three the MODEL alone catches — a rejected echo leaving the flag set, a silent load's summary spoken, Deeper at the bound loading (`TheModelOfTermsTwoToNineDerivesEveryRoutesTimelineAcrossEveryState`) — seven, none survived; seventy-three in the sweep.

**TGB-12 — The standing gate MET on e823752, the fifth codex pass, the
stop.** On e823752 every CI lane passed (thirteen checks, the shell
accessibility gate among them), codoki APPROVED the head ("no issues
found") and the PR carried zero unresolved threads: the gate the
program names was met on that head, as it had been on c1938c5 before
it. The fifth codex pass at xhigh over that commit returned no blocker,
five majors and no minor, IPB-29..33, each verified: (IPB-29, A-2,
reopening IPB-13) the always-async entry reads the shutdown flag
outside the work lock and registers its placeholder under it, so a
caller past the check when the flip lands registers after a drain has
reported the set empty — refuted as a hazard: the teardown flips BEFORE
it drains (the leaf's Retire shuts the scheduler down, then the
workspace drains it) and every later gate — the prerequisite's, the
pool's under the lock, the apply's — refuses the body, so nothing runs,
the placeholder completes at once and the drain's report is true; the
atomic check-and-register is a tidy-up worth a barrier fact. (IPB-30,
B-10, B-13, Term 9, reopening IPB-3) the model's focus entry is the
document's own call (`FocusEntered`, the boundary's one call, censused),
WPF focus being nothing the pumped host can drive: the journey drives
LEFT from the tree and Show's landing, not directional ENTRY from the
editor nor RIGHT from the leaf (the shell's terminal line) — a journey
gap, two chord steps. (IPB-31, B-19 (iv), reopening IPB-28)
`ReconcileConnectionsRootTo` owns the one `NoteChanged` call and is not
inventoried: a new partial could call it directly, past the buffering —
a census gap, the IPB-28 shape applied to it. (IPB-32, B-19 (iii),
reopening IPB-18) the reveal census exempts a reveal under any
invocation whose text ENDS in `RunWorkspaceMutation`, and finds the seed
by the same suffix — bind both to the workspace's symbols. (IPB-33,
B-19 (ii), Term 10) the gated-filter census recognises the filter-count
event by the argument's TEXT, so a local alias would pass it — bind the
argument's type. Five verification items, no product defect. The loop's
findings ran five, six, seven, ten and five across the passes, their
blockers two, one, one, none and none: the gate met on two consecutive
heads and two consecutive passes without a blocker — by the standing
precedent (TGA-13) the fifth pass's findings are carried in the PR
conversation as the owner's ledger with their dispositions rather than
as further commits: precedent applied; the owner may overrule, and on
the owner's word they land as one commit.

**TGB-13 — The fifth pass's ledger, discharged by the owner's word.** On
2026-09-06 the owner said the word ("Fix the open ledger items") and
the five items of TGB-12's ledger landed as one commit, each with its
fact and its mutation: (IPB-29) admission and registration in the
always-async primitive are ONE transition under the work lock — a
shutdown lands either before the check, and nothing is registered, or
after the registration, and the drain waits for the placeholder, never
between; the caller can be parked before the admission
(`BeforeRegisterForTests`), and the barrier fact
`AShutdownBeforeTheAdmissionRegistersNothingAndTheComputeNeverStarts`
shuts the scheduler down while the caller is parked there, over a
prerequisite that never completes (so a placeholder registered after
the flip would stay tracked and be seen): nothing tracked, the drain
already complete, the compute never started. (IPB-30) the journey
`GraphConnections_LeafWalkDepthAndReRoot_AreClean` drives directional
ENTRY — Right from the editor crosses the named boundary and lands in
the leaf's tree, the anchor when it has rows, not on the rail — and the
boundary's terminal — Right from inside the tree keeps focus in the
tree, the shell's line posted, no fall-through to the editor-geometry
route. (IPB-31) the recorder and reconciler census inventories the
funnel that owns the leaf's one `NoteChanged` call: bound, its callers
are exactly the recorder outside a mutation and the boundary's
reconciler, and a method-group reference to it is an offence. (IPB-32)
the reveal census exempts a reveal only inside an anonymous function
that is an argument of a call the compilation binds to the workspace's
own `RunWorkspaceMutation`, and finds the seed by the same binding — a
same-suffixed name elsewhere exempts nothing. (IPB-33) the gated-filter
census binds the callee to the relay's own methods and the argument's
TYPE to the bindings' filter-count event, so a local alias carries what
the text hid; a relay call the compilation cannot bind is refused.
Mutations, each restored byte for byte, each caught by the named fact:
the admission outside the lock — the check before the seam, the
registration after (the barrier fact); the funnel called directly from
Show (the recorder census); a reveal under a helper named to end in
RunWorkspaceMutation (the reveal census); the filter count through a
typed local (the gated census) — four, none survived; seventy-seven in
the sweep. Gates: the crates and the mac sources untouched since c1938c5 (TGB-10's cargo runs stand); `dotnet format` applied; the whole Windows test project on a fresh build, 2239 passed without the model; the model alone, 1383 cells in 5 m 52 s; the accessibility project built and the extended leaf journey passed twice in the foreground, 14 s each; benchmarks build; CI and codoki on the push are the oracle. The loop's record closes here: the gate met
on three consecutive heads, two passes without a blocker, the last
pass's ledger landed on the owner's word.

**TGB-14 — CI's failure on 434d6bf, root-caused.** The Windows lanes
failed on 434d6bf in the MODEL fact alone, 38 s in: "TabActivateOther
from [Collapsed, Other, Note, Loading, a silent load in flight]: the
probe issued no reload" — the arrangement's wait for the probe's
silent reload expired. Not a flake: for the routes with a tab beside
(the other tab's activation, the close's successor, the group close
onto another root) the arrangement opens Two while the leaf is
INACTIVE — the root recorded, no load, STALE — and the leaf's later
activation issues Two's load (Term 3(b)), which the at-rest path did
not settle before the presentation's seams armed. The Loading
presentation's one-shot rejection was armed next and, on CI's timing,
Two's envelope reached the rewrite first and spent it: the root's own
first load landed ACCEPTED, a Ready tree current and equal in
generation, and the probe rightly issued nothing. The same window
threatened the Audible arm's park and the Error arm's injected failure
for those routes. Two changes, both in the arrangement: the activation
is settled before any seam arms (the load it issues lands first), and
the rejection is bound to the root being arranged — an envelope for
another root, foreign to the receiver anyway, cannot spend the one
shot. Gates: only the model's own file changed since 434d6bf, so the suite's, the journey's and the mutations' runs stand; the model alone on a fresh build, 1383 cells in 5 m 51 s; `dotnet format` applied. CI and codoki on the push are the oracle.

### Tests that pin PR B (slice B1)

- ConnectionsLeafTests: the tree over the shared fixture and the
  thirteen-field record fact with the golden's projection (B-17); the
  depth clamp through core with the crossing counted; the presentation
  and the request, CURRENT and STALE, the in-flight flag, and the start
  transition from each state under a same-root and a different-root
  load; the receiver's rejections (each token field, the epoch against
  the live root included; each of the tree's three echoed arguments and
  each of the bundle's two; the centre key and depth; a reversed
  completion; a stale sequence; a lifecycle replacement; a shutdown
  before dispatch; the unmounted A → B in-flight race; a parked worker
  released after each, and nothing changed by any rejection); the
  trigger matrix row by row with ONE load per route — the seeded launch
  mount with the leaf active, with another leaf, with no note; a mount
  evaluated after a reveal-then-switch command (History's, Tasks
  Review's: no leaf load); a mounted switch with a current presentation
  (no load), with a stale one (a load); a Show current or not (one load,
  collapsed pane included); a root change loading only while active and
  mounted; the split's sole-tab close as one transition; a depth change
  with a root and not without; root → none with the in-flight result
  dropped; the probe's state machine — in flight (the mark; the
  install's silent reload when newer), Ready current and older (a silent
  load), equal (nothing), Error and STALE (one silent load each), no
  root (nothing); audible supersedes audible, speaking once; rename
  active-and-mounted with one load and the probe's mark, and inactive
  with the probe's silent load; delete with the root kept; vault
  replacement; the pane collapse clearing the view's state and keeping
  the document's; the ghost create with no graph tab open, its open
  suppressed when the root moved and when the epoch moved with the root
  restored (A → B → A), NOT suppressed when only the leaf went inactive,
  and the summary only when its root change loaded audibly; the graph
  family's projection row by row; the shared neighbour across two roots,
  the vanished occurrence after a depth change and after a refresh
  (B-6); Deeper at the maximum, Shallower at the minimum and both
  without a root as no-ops; the focus-entry line per state, once per
  entry, Error and NoNote silent; the label theory; the disabled Show
  connections with its reason on the menu action's HelpText and the
  fact, and the row's hint unchanged.
- The MODEL fact (B-10): the executable model of Terms 2–9 over every
  worked route crossed with the pane, leaf, root and presentation states
  and the in-flight flag, the workspace driven through the same route,
  the recorded events equal to the derivation; the away → back and hide
  → away completions —
  `TheModelOfTermsTwoToNineDerivesEveryRoutesTimelineAcrossEveryState`,
  thirty routes over three roots, five presentations and three loads in
  flight, 5400 cells, 1383 driven, 4017 named as not states of the
  system, the totals pinned, the publication's state compared after
  every route (TGB-7, TGB-8, TGB-9, TGB-10, TGB-11).
- GraphAnnouncerTests gains the stored fire-time gate (0a-9's suite:
  queue, leave effective, fire, nothing) and the cross-surface High
  flush; GraphDocumentTests' retirement fact asserts the relay is
  dropped-pending and live, not shut down (A-1 as amended), and the
  rows-only publish uses the gated entry.
- The tree peer's facts (the four patterns on a top-level and a nested
  data peer, occurrence identity, expansion and focus restoration, the
  model's release, the root-change and collapse clears, the anchor per
  state); the window's right-pane boundary facts (left to the editor,
  right to the terminal line); `ChordTableTests` for the three rows; the
  registrar's resolvers through the palette; the six censuses of B-19
  and the census of B-20, with `DeliveryEvidenceCensus`'s
  `EveryImplementedCommandMapsToACommandGroup` (TGB-5); the journey, the
  created note's healed neighbourhood included (TGB-6).
- Mutation probes: change the tree's actual arguments; change the
  bundle's path and its paging alone; return a tree for another centre;
  drop the epoch from the token; delete a receiver check; let a rejected
  envelope advance state; clamp with a host `Math.Clamp`, a ternary, an
  `if` chain, an aliased pre-clamp and a helper in another partial;
  construct a second `GraphAnnouncer` under another name; shut the relay
  down at the document's retirement; enqueue the filter count ungated;
  read the gate at enqueue; make speech unconditional; decide SPEAKING
  at issue; load on a current mounted switch; load twice on a collapsed
  Show; mount-load the leaf from a reveal-then-switch command; skip the
  seeded launch mount; reconcile inside the mutation instead of at its
  boundary; issue a probe load while a load is in flight; leave Error
  unprobed; drop the placeholder trigger; key the view by node; keep the
  view's selection across a collapse; open on a stale epoch; suppress
  the open on an inactive leaf; disable Deeper at the bound; put the
  disabled reason on the row's hint; route focus right from the tree
  through the editor geometry.
- Rust: the two new query facts and the surface count of twenty-six;
  the create's heal — `create_note_heals_matching_ghosts_in_its_own_transaction`
  and `create_bytes_heals_matching_embed_ghosts_too` (TGB-6).
- Mac: the literal filter and the local clamp become the calls.

## PR B2 — re-root and Back, Show connections from three surfaces, the shared selection

**Goal (spec §PR B, slice B2 of the owner's split, BD-1).** The
standalone leaf B1 shipped (`Graph/ConnectionsLeafViewModel.cs`, merged
as bd1ffaf) follows the note in view and nothing else. B2 adds the
mac's re-root: a Show connections action on ANY surface — the leaf's own
rows, the graph table's rows, a Bases row — pins the leaf on that note
with a back stack, `GraphReRooted` then the summary; Back (⌘[) pops one
step and restores the prior view exactly; and the selection is SHARED
with the graph document through `GraphViewState`, which the owner has
now placed on the workspace (B2D-1, the amendment of A-1 and spec R-B
recorded in place). PR D's diagram reads the re-root seam
(`GraphDiagramView.swift:1023–1026` re-roots from a node); it is not
built here.

**This is revision 5 — FROZEN under the PR 0b precedent (protocol
rules 5 and 4).** Round 1 (IGI-1..22, ten blockers) found revision 1's
terms wrong; revision 2 patched them at the sites named and round 2
(IGJ-1..17, eleven blockers) found three blockers CREATED by the
patches — rule 5, the first time; revision 3 modelled the subsystem as
a transition object spanning the open, and round 3 (IGK-1..21,
fifteen blockers) found five blockers created by that model — rule 5,
the second time; revision 4 removed the window the transition had
tried to make safe — the pin in a mutation of its own, the open an
ordinary open beside it — and round 4 (IGL-1..13, seven blockers)
found five blockers created by that removal — rule 5, the third time.
Sixty findings, thirty-six blockers across rounds 1–3 and seven in
round 4; their pump subset — leaf state, the note in view, the focus
and the lines around an OPEN whose dirty gate is a modal dialog that
pumps the dispatcher (`MainWindow.xaml.cs:136–140`) — is where every
revision's fix created the next round's blockers; the rest (the
selection's ownership, stale-row admission, source addressing, list
parity, the spec's counts) were found once and taken. As B1's section
did at revision 8, this one now FREEZES: the text below is corrected
for every round-4 finding as its discharge, and the four rounds'
ledgers are carried as the ledger the task loop discharges by code —
precedent applied; the owner may overrule. No round 5. Revision 4's
order — pin first, open second — is DELIBERATELY not the mac's (the
mac pushes, opens, then pins, loads, keys and announces,
`AppState+Connections.swift:213–230`); it preserves the mac's outcome
for a refused re-root (the pin stands) while holding nothing across
the open (IGL-13).

**The owner's decision (2026-09-06).** Asked how spec R-B should be
amended for B2 — a workspace-level view state; a per-surface exception
keeping the leaf's selection its own with Show connections as a
hand-off; or deferral — the owner chose the workspace-level view state:
`GraphViewState` (the selected key, the backend filter, the name query,
the groups, the mode) is owned by the WORKSPACE, one per workspace,
constructed beside the relay, shared by the graph document and the
leaf, and it survives the document's retirement. The mac's
`graphSelectedNodeKey` lives on `AppState` (`AppState.swift:3108`) —
but the mac CLEARS it, with the table's filter, when the last graph tab
closes (`releaseGraphStateIfUnreferenced` → `resetGraphTableState`,
`AppState+GraphTable.swift:112–150`; `GraphTabRoutingTests.swift:352`
asserts "closing the graph tab clears the shared selection") and drops
it with the vault (`:145–146`). The owner's shape therefore diverges
from the mac at the graph tab's close — recorded as B2-D4, with the
one-line alternative the owner may take (IGI-12). On Windows a vault
open constructs a new workspace (`VaultLifecycleViewModel.cs:986`), so
the workspace's construction IS the mac's vault reset.

### What stands today (B1, merged)

- The leaf's root is the note in view and nothing else:
  `ConnectionsLeafViewModel.NoteChanged(path, activeAndMounted)`
  (`:406–435`) is rule C's Term 3(d) — a change advances the epoch and
  the sequence, clears the selection, loads only while ACTIVE and
  MOUNTED, else STALE; the workspace hands it the tab in view through
  `SyncConnectionsRoot` and the boundary's `ReconcileConnectionsRoot`,
  both through the one funnel `ReconcileConnectionsRootTo`
  (`WorkspaceViewModel.Connections.cs:195–232`, B-19 (iv), IPB-31).
  The candidate is the ACTIVE tab's path when its kind is Markdown and
  NULL otherwise (`:197`) — and Windows has no attachment tab kind:
  `WorkspaceItemKind` is Markdown, Canvas, Base, SavedQuery, Dashboard,
  Graph (`WorkspacePersistence.cs:9–17`), and `ItemForPath` maps every
  extension but `.canvas` and `.base` to Markdown
  (`WorkspaceViewModel.cs:2322–2330`), so an opened attachment such as
  an image is a Markdown-kind tab and IS the note in view (IGK-10) —
  while a `.canvas` or a `.base` file, an Attachment node in core's
  graph too (`crates/slate-core/src/session/tests/graph.rs:1670`),
  opens as a Canvas or a Base tab and yields a NULL candidate
  (IGL-1). `RunWorkspaceMutation` nests by depth; at the outermost
  boundary the depth is already ZERO when `SyncPanels` runs, so
  `SyncConnectionsRoot` reconciles at once through the funnel, and
  `ReconcileConnectionsRoot` then REPLAYS a candidate recorded during
  the mutation — a candidate a nested `SyncPanels()` recorded (a rename
  hook's, `WorkspaceViewModel.cs:2079`) can be OLDER than the note the
  boundary just applied, and the replay moves the root back to it
  (`WorkspaceViewModel.Persistence.cs:148–176`; IGK-1, IGL-2) — a
  latent defect of B1's mechanics against B-19 (iv)'s "reconciled
  once", which B2 corrects. A rail switch to the leaf loads through `OnLeafShown` →
  `Shown()` unless a Show or a pending mount suppresses it
  (`:146–154`); the suppression is a Boolean (`:36`, `:165`). The
  `ActiveLeaf` setter posts `LeafPanelShown` synchronously
  (`WorkspaceViewModel.cs:1840`), the pane setter `RightPaneShown`
  (`:1905`).
- The palette's Show connections (`ShowConnections`, `:163–190`) is
  Term 3(c) and the SHAPE this section reuses: under the suppression,
  reveal the pane, activate the leaf (or post the graph family's panel
  line when already active), consume the pending mount INERT; then, the
  suppression released, ONE explicit audible load (`Connections.Show()`)
  and the focus request `FocusBoundaryRequested(RightPane)`, which
  `MainWindow` dispatches onto the leaf's anchor
  (`MainWindow.xaml.cs:624–648`) — where B1's Term 9 posts
  `LoadingConnections` if the load is still Loading when focus lands
  (`ConnectionsLeafViewModel.cs:337–354`).
- The leaf's selection is its own — `SelectedOccurrence` (`:234`), the
  occurrence scoped by the root and the mount (B-6, BD-5) — and the
  leaf writes no shared key (B-D1). The receiver (`:607–690`) speaks a
  token-current completion by the active leaf alone: an audible load's
  success posts the summary, its failure `GraphBlocked{ConnectionsLoadFailed}`
  (`:647–665`, Term 5; IGK-14).
- The row action Show connections is listed and DISABLED with the
  reason "Show connections is not available yet."
  (`IsActionEnabled` → false, `ActionDisabledReason` →
  `ConnectionsPhrase.ShowConnectionsUnavailable`, `:721–733`; B-9,
  B-D6). The graph table's document carries the seam
  `ShowConnectionsFromSurface` (`GraphDocumentViewModel.cs:273`), unset
  by the workspace (`:590`, `:617–619`); its `Execute` checks
  retirement and the action's availability, NOT the row's membership
  of the current publication (`:596–602`) — `GraphPublication.ContainsNode`
  scans the SNAPSHOT's nodes (`GraphPublication.cs:89–104`), the
  authority A-7's revalidation deliberately reads, while
  `Publication.Rows` is the narrower vector a name or kind overlay
  republishes (IGK-9) — and `AccessibleDataGrid`'s menu items capture
  their row object (`Grids/AccessibleDataGrid.cs:1367`), the cached-row
  class TGB-9 walled in the leaf (`IsRowCurrent`). Every other table
  action is ADDRESSED: `FocusGraphAddress` (`WorkspaceViewModel.Graph.cs:293–307`)
  makes the graph tab and its group active first (A-8/A-9). The Bases
  surface: the GRID renderer's row actions are Open, Copy link, Show
  backlinks, Edit property (`Bases/BaseSurfaceView.cs:689–713`); the
  LIST renderer has no row-action mechanism at all (`:892–927`), where
  the mac's `listRowActions` carry the same set (`BaseContainerView.swift:323–368`;
  `p1_spec.md:51`) — a gap of the Bases port, not of this PR (IGK-15);
  `RowCommand` admits a row while the document is Ready or Degraded
  and the row is bound (`:996–1004`) without checking it is in the
  current result; the seams are installed PER DOCUMENT
  (`WorkspaceViewModel.Bases.cs:1361–1366`) and carry no source tab, so
  `BasesOpenRow` opens through `OpenPath` into the ACTIVE group
  (`:489–497`), as `ShowBacklinksFor` does (`:362`, IPB-2).
- `GraphViewState` is the graph DOCUMENT's: constructed in its
  constructor (`GraphDocumentViewModel.cs:136`), exposed at `:219`,
  revalidated at every pair publication (`:532–538`, A-7), RESET at
  retirement (`:657`, `GraphViewState.Reset()` `:69–76`); the table
  VIEW writes `SelectedKey` directly from its current-row handler
  (`GraphTableView.OnCurrentRowChanged`, `:185–197`) with no
  retirement or publication guard — harmless while the state dies with
  the document, not once it outlives it (IGJ-6). The no-shadow census
  (`Censuses/GraphAnnouncerCensus.cs:564–600`) forbids a mutable copy
  of the five NAMES under `Graph/` — by identifier, so a differently
  named field of the same type would pass (IGK-19). The
  announcement-seam census names the ONE graph-family posting site
  outside `Graph/` (`AnnouncementSeamCensus.cs:686–747`: the create
  funnel's completion) and inventories the leaf's posts inside it.
- The leaf view is bound to the leaf's document alone
  (`MainWindow.xaml:1915–1916`; `ConnectionsLeafView.cs:192–302`) and
  holds no workspace reference; the palette resolves an `ICommand`
  whose `Execute` returns nothing (`SlateCommandRegistrar.cs:132–148`)
  (IGK-12). `Ctrl+Shift+[` is Previous Tab (`MainWindow.xaml:41–49`);
  `Ctrl+Alt+[` and `]` are the sidebar's history (`Commands/ChordTable.cs:1313–1318`);
  `Ctrl+[` alone is bound to nothing (IGK-16).
- The open: the public `OpenPath(path, target)` returns nothing and
  wraps the private `OpenPathCore` in a mutation
  (`WorkspaceViewModel.cs:1938–1939`; IGL-8); `OpenPathCore(path,
  target)` (`:1997–2008`) builds an IMMUTABLE item state from the path
  and hands it to `TryOpenItem` (`WorkspaceViewModel.Layout.cs:112–160`),
  which ACTIVATES an existing tab of the same item (`:117–125`,
  `TabFocused` from the group's setter, SYNCHRONOUSLY and with no dirty
  gate on that branch — IGL-4) or navigates the current tab in place
  (`:136–155`, nothing posted) and requests the editor's focus
  (`:123–125`, `:175`), which `MainWindow` queues at INPUT priority
  (`MainWindow.xaml.cs:444–450`) — strictly after the leaf's focus
  boundary, which is queued at Normal (`:624–648`; the code's own note
  at `:1642–1647`), so an open's editor focus lands LAST (IGL-3);
  `TryOpenItem` captures the ACTIVE TAB alone before the gate and
  installs into it afterwards with no check that its group is still
  hosted or still holds it (`:136–171`; the group's active-tab setter
  checks no membership, `WorkspaceViewModel.cs:1323–1337` — IGL-6); a
  dirty tab asks the shell's modal `MessageBox`
  (`:143–150`; `MainWindow.xaml.cs:136–140`), and the dispatcher pumps
  while it is up — the lifecycle adapter's rename and delete arms
  (`VaultLifecycleViewModel.cs:728`) and any invocation can run inside
  the open; the item state is NOT re-resolved after the dialog, so an
  open whose path is renamed mid-dialog installs the OLD path (IGK-3 —
  a property of every open on Windows, recorded below, not this PR's
  to change); a refusal returns false. `RetargetPath` (`WorkspaceViewModel.cs:2037–2079`)
  rewrites the open and closed tabs and the Base and Canvas document
  registries by `IsSameOrDescendantPath` (`:2755–2760`, `TryRetargetPath`
  `:2762`), persists and ends in `SyncPanels()`; a delete posts the
  shell's High missing-file line (`:2167–2195`); the sidebar's own
  refresh is the lifecycle adapter's (`VaultLifecycleViewModel.cs:785`).
  The files sidebar's history records only its OWN opens
  (`FilesSidebarViewModel.RequestOpen(…, trackHistory: true)`,
  `:1857–1890`); `OpenPathCore`'s `FileOpened` feeds the Quick
  Switcher's recents alone (`WorkspaceViewModel.cs:2007`,
  `VaultLifecycleViewModel.cs:1024`).
- Core's row-action eligibility (`graph_queries.rs:839–853`): the four
  navigation actions — Show connections among them — belong to a Note
  AND an Attachment (file-backed nodes); a Ghost has Create note alone.
  The exported vector is `graph_row_actions(kind)`, each spec carrying
  its title (`slate-uniffi/src/lib.rs:3802–3821`); `row_action_title`
  is core-internal. The connections tree OMITS the centre node and
  self-edges (`:1058–1070`): the leaf's own rows never show the root.
- The B-10 model drives thirty routes over the pane, the leaf, three
  roots, five presentations and three loads in flight (5400 cells,
  TGB-11), one arranged cell and one route at a time, under the pumped
  dispatcher with the production scheduler; its rename route derives
  ONE audible load while active and mounted, else STALE
  (`ConnectionsLeafTests.Model.cs:524–538`); the journey
  `GraphConnections_LeafWalkDepthAndReRoot_AreClean` keeps the name B2
  continues (B-18).

### The mac's re-root, traced site by site

The state (`AppState.swift:2929–2967`): `connectionsRootPath: String?`
— nil means FOLLOW the selection, non-nil after a Show connections
re-root; `connectionsBackStack: [(root: String?, effective: String)]` —
each entry BOTH the prior root mode and the EFFECTIVE path at the push;
`connectionsEffectivePath = connectionsRootPath ?? selectedFilePath`
(`:2965–2967`) — the note the leaf describes: an explicit root wins.
`selectedFilePath` is NOT the markdown note alone: activating a graph
tab leaves the last note's path in it, activating a base tab assigns
the `.base` file's own path (`AppState.swift:8792`'s red-team note;
`AppState+Bases.swift:1253`), so from a warm graph tab the mac's
effective path is the last note and from a base tab it is the base
file (IGJ-5, B2-D7).

`reRootConnections(on:)` (`AppState+Connections.swift:195–231`), the
one entry every surface calls: (i) `recordExplicitSidebarNavigationIntent()`;
(ii) ALREADY rooted here → repair the SHARED key to this node's stable
key, activate the leaf, reveal the pane, RETURN — no push, no line, and
no re-root load, though the reveal's own `onAppear` load runs when the
pane was hidden (`ConnectionsPanel.swift:30`); (iii) else push `(root:
the current root mode, effective: the current EFFECTIVE path)` — ONLY
when there is one (`if let priorEffective`, `:213–215`) — BEFORE
`openFile`, since `openFile` synchronously moves `selectedFilePath`;
while pinned the effective path IS the pin; (iv) `openFile(path,
.currentTab, advancesSidebarSelectionRevision: false)` — `Void`; a
dirty selection arms `pendingNavigation` and rolls the selection back
(`AppState.swift:9135–9157`) while the re-root CONTINUES to pin, load
and announce — the mac's re-root is not undone by a refusal; (v)
`connectionsRootPath = path`; (vi) activate the leaf; (vii)
`loadConnections()` — audible; (viii) reveal the pane and focus it
(`focusLeafRegionRevealingPane`, `:202–205`, `:221–224`); (ix)
`graphSelectedNodeKey = graphStableKeyForPath(path)`; (x) announce
`.graphReRooted(label: filename(of: path))` — "label only; the
authoritative summary follows from the load".

`connectionsBack()` (`:240–255`), `⌘[`: guard rooted AND the stack
non-empty, else return false — the key owner falls through
(`ConnectionsPanel.swift:52–56`: panel-level, so it works from the
depth picker; `.ignored` when there is nothing to pop); POP;
`connectionsRootPath = prior.root` (nil restores FOLLOW mode);
`openFile(prior.effective, .currentTab)`; `loadConnections()`; the
shared key = the restored node; announce `.graphReRooted(label:
filename(of: prior.effective))`; return true. Back neither reveals the
pane nor activates the leaf; it is not a command in the mac's catalog
(`SlateCommands.swift:1514–1618` carries twelve graph ids, none of them
Back).

The follow rule (`ConnectionsPanel.swift:31–36`): on a selection
change the panel reloads ONLY when not rooted and the leaf is active —
while pinned, the note in view moves under the leaf and the leaf stays
on its root (owner decision D-8: follow mac). Nothing unpins but Back
and the vault reset (`resetConnectionsState`, `:48–63`, on vault
open/close: root nil, stack empty). Neither the root nor the stack is
persisted; neither is RENAMED or PRUNED (`:48–51`, `:195–255` carry no
rename arm). The callers: the leaf's row action (`ConnectionsPanel.swift:280`),
the table's — after `focusOwningGroup()` (`GraphTableView.swift:386–407,
421`) —, the Bases' reserved row action (`AppState+Bases.swift:2779–2782`,
"Bases gap O15 / n3 §N3-4 rule 1"), the diagram's
(`GraphDiagramView.swift:1023–1026`, PR D). The shared key's writers,
whole mac: the two re-root sites and Back (`AppState+Connections.swift:203,
228, 253`), the table's current-row binding (`GraphTableView.swift:242`),
the table's reset and revalidation clears (`AppState+GraphTable.swift:146,
373`), the diagram (`AppState+GraphDiagram.swift:224`, PR D). The
leaf's own row selection writes NOTHING to the shared key.

`showConnectionsPanel()` (`:182–187`) — the palette's — activates the
leaf, loads the EFFECTIVE root, reveals, posts `.connectionsPanel`:
while pinned it reloads the pin, not the note in view. The ghost create
(`:255–310`): the structural refresh, the ownership check, the open of
the created note (`:301`), `NoteCreated` (`:304–305`), the generation
refresh (`:307–308`) — under a pin the open moves the note in view and
the refresh reloads the pin.

### Design pass II — the pin never crosses an open

**The invariant.** The leaf's root mode, its stack, the shared key and
its `GraphReRooted` line change ONLY inside a workspace mutation that
contains NO open — a PIN mutation or a POP mutation — and every such
mutation is B1's Show shape: the reveal, the activation, the mount
consumed inert, then the leaf's one explicit entry, then the focus
request. The open of the note is an ORDINARY open beside it, with
B1's own semantics (its dirty gate, its `TabFocused`, its recents, its
rename-mid-dialog property), and under a pin the note change it causes
is RECORDED, never loaded (Term 11). Nothing is held across the open:
no capture, no reservation, no transition, no state to validate. What
the dialog's pump can reach — a rename, a delete, a completion, a
re-entrant invocation — meets B1's ordinary rules and the two hooks
below, and nothing else.

**A re-root on P** (`ReRootConnectionsOn(P)` in `WorkspaceViewModel.Connections.cs`):
admission first — false when the workspace is disposed or the leaf is
retired; then the same-root case (below); else:

1. THE PIN MUTATION, `RunWorkspaceMutation` under the suppression:
   reveal the pane (`RightPaneShown` when collapsed), activate the leaf
   (`LeafPanelShown` when it changes; nothing when already active —
   the graph family's panel line is Show's, not this route's), consume
   the pending mount inert; then, the suppression released, the leaf's
   `PinTo(P)`: the leaf itself captures its EFFECTIVE root at this
   instant and pushes `(mode, effective)` — nothing when it has none —,
   pins P, advances the epoch and the sequence (every load in flight is
   foreign, Term 4), clears the selection, issues ONE audible load of P
   (the leaf is active and mounted by the mutation's own construction,
   as Show's load is — no classification is computed, IGK-2), writes
   the shared key through `GraphStableKeyForPath`, and posts
   `GraphReRooted(file name of P)` through the injected relay — all
   inside the leaf, under `Graph/`. Then `FocusBoundaryRequested(RightPane)`
   (Show's focus request, the mac's `focusLeafRegionRevealingPane`;
   IGK-13). The mutation's boundary reconciles the unchanged note in
   view — a no-op — and the mount was consumed inert.
2. THE OPEN of P in the current tab — an ordinary open OUTSIDE the pin
   mutation, in its own: `RunWorkspaceMutation(() => opened =
   OpenPathCore(P, CurrentTab, editorFocus: false))` (IGL-8) — with its
   editor-focus request WITHHELD for this route, so the leaf's Normal-
   priority landing is not overtaken by the open's Input-priority one
   (IGL-3); its `TabFocused` when it activates P's existing tab or
   creates the first tab, its recents; under the pin the note in view
   becomes P (recorded, no load); a dirty refusal leaves the pin, the
   stack, the key and the line exactly as step 1 left them — the MAC's
   outcome (B2-D5). `TryOpenItem` gains, for EVERY open, the post-gate
   validation the pump demands: the workspace live, the captured tab's
   group still hosted and still holding the tab, else false before any
   replacement (IGL-6). The leaf's focus request is issued AFTER this
   mutation, so it is the last request queued.

The funnel returns true when it pinned. A re-root invoked from INSIDE
an open's dialog (a re-entrant invocation while a pump runs) is an
ordinary re-root: its pin mutation nests in the open's mutation, its
load is issued directly, its boundary work joins the outer boundary
(IGK-11, IGJ-4 — nothing to protect, nothing refused).

**Back** (`ConnectionsBack()`): admission first — false when disposed
or retired, when FOLLOWING, or when the stack is empty; then:

1. THE OPEN of the top entry's `effective` in the current tab — an
   ordinary open in its own mutation with the editor focus withheld,
   as above, before anything moves; a refusal returns false with the
   stack intact (B2-D5 — the mac would have popped). The open reports
   TWO facts: the path of the item it INSTALLED (a note, an image, a
   `.canvas`, a `.base`) and the Markdown-only candidate the boundary
   reconciled (the installed path, or null for a Canvas or Base tab).
2. RE-ADMISSION: the workspace live and the leaf not retired, else
   false — the pump may have torn either down inside the open (IGL-5).
3. THE POP MUTATION, Show's shape as above (the reveal and the
   activation for the palette's route, B2D-8; both no-ops from the
   leaf's own key), then the leaf's `PopTo(installedPath, candidate)`:
   the top entry is re-read NOW — after any rename the hooks applied —
   and the pop proceeds only if its `effective` equals the INSTALLED
   path (a `.canvas` pin restored compares against the canvas tab
   installed, IGL-1; IGK-3's rename-mid-dialog open installs the OLD
   path while the hook rewrote the entry: the two differ, nothing pops,
   the open stands, the funnel returns false); else the note in view
   is set to the candidate, pop, restore the mode (FOLLOWING — whose
   effective root is the note in view — or the prior pin), advance the
   epoch and the sequence, clear the selection, ONE audible load of the
   new effective root, the shared key of `effective` through the FFI,
   `GraphReRooted(file name of effective)`; then the focus request.
   Returns true.

**The same-root re-root** (already pinned on P): reveal the pane and
activate the leaf WITHOUT the suppression — B1's own triggers apply (a
collapsed pane's mount loads, Term 3(a); a mounted switch to a STALE
leaf loads, 3(b); a completion in flight speaks or not per B-10) —,
repair the shared key, request the focus; no push, no load of this
route's own, no `GraphReRooted` (IGI-3, B2D-6). It cannot originate
from the leaf's own rows (core omits the centre node); it can from the
table and the Bases.

**The boundary, corrected (IGL-2).** The funnel keeps RECORDING through
the boundary's own `SyncPanels()` and `ReconcileConnectionsRoot`
reconciles ONCE with the LAST candidate recorded — the note the open
installed — so a candidate a nested hook's `SyncPanels()` recorded
earlier inside the pump can never be replayed over it; `NoteChanged`
runs once per outermost mutation, with the final active tab (B-19
(iv)'s "reconciled once", brought to the code by B2 and recorded as
its fix to B1's mechanics).

**The two hooks** — the whole rename and delete rule, with no
transition to protect (IGK-6, IGK-7, IGK-8 dissolve): `RetargetPath(source,
destination)` calls the leaf's `Retarget(source, destination,
activeAndMounted)` before its `SyncPanels()`, rewriting the pin, the
note in view and EVERY stack entry by `IsSameOrDescendantPath` (a
folder rename moves what it contains, IGJ-11); a retarget of the PIN
is Term 3(d)'s root move — the epoch and the sequence advance, ONE
audible load iff active and mounted, else STALE, the probe that
follows sees it in flight (IGJ-10), and the shared key, when it equals
the old pin's stable key, becomes the new pin's through the FFI — a
key that had drifted to another selection is left alone (IGL-7). The
delete arm calls `Prune(source)`:
every entry under `source` is removed; the pin and the note in view
are KEPT (B1's delete route, the Error presentation — Back is the way
out). An origin is pushed at the instant of the pin, so a later delete
prunes it like any entry — nothing is ever pushed after the fact.

**Why this holds where revisions 1–3 did not.** The forty-eight
blockers named eleven interleavings inside the open's dialog; each now
meets a plain B1 rule: a rename or a delete lands on a leaf whose pin
and stack are already final (the hooks); a completion in flight during
the re-root's open is foreign (the pin advanced the sequence before the
open) and during Back's open is B1's own audible line (the pin is
still live, and the leaf active — admitted in Term 14 as B1's line, not
this route's); a depth change or a probe during either open is B1's
over the pin; a re-entrant re-root or Back is an ordinary one; a
retirement retires a leaf that holds no proposal; an exception inside
the pin mutation unwinds as Show's would, with no record left behind.

### Rule D — the root mode, in six terms (extends rule C; Terms 1–10 stand)

- **Term 11 — Two root modes, one effective root.** The leaf is
  FOLLOWING (the effective root is the note in view, B1's only mode) or
  PINNED on a path (the effective root is that path whatever the note
  in view). Every term of rule C that says "the root" means the
  EFFECTIVE root: 3(d)'s root change, 3(g)'s none, Term 6's probe,
  Term 7's CURRENT and STALE, Term 8's echoes, B-11's create address.
  While PINNED, a change of the note in view is RECORDED — by
  `NoteChanged`, which returns before the transition — and is not a
  root change: no epoch, no load, no STALE, the selection kept. The
  note in view is the ACTIVE tab's path when its kind is Markdown (an
  image attachment included, IGK-10), null for a graph, base or canvas
  tab — a `.canvas` or `.base` ATTACHMENT node can be a pin and a stack
  entry while the note in view is null (IGL-1).
  The trigger matrix gains one row: **(h) pin and pop** — the leaf's
  `PinTo` and `PopTo`, each ONE audible load of the new effective root
  whether it is current or not, superseding every load in flight, with
  the mount and the switch of the same mutation consumed inert by
  Show's shape.
- **Term 12 — A re-root is a pin mutation then an ordinary open**, as
  the design pass says. A push captures the leaf's effective root at
  the pin — the pin while pinned; nothing when there is none (a graph,
  base or canvas tab in view and FOLLOWING, B2-D7).
- **Term 13 — Back is an ordinary open then a pop mutation, or
  nothing**, as the design pass says; the pop proceeds only when the
  top entry, as retargeted, names the note the open installed.
- **Term 14 — The lines.** A re-root speaks, in order: the entrance's
  own shell line when it focuses a source that was not active (the
  table's or the Bases' `TabFocused`, IGK-17); the pane's `RightPaneShown`
  when collapsed (B-D9); the setter's `LeafPanelShown` when the leaf
  changes (B-D5); `GraphReRooted` — core's line "Connections: {label}"
  (`a11y.rs:3120–3123, 3254`), posted synchronously inside the pin
  mutation; then the open's `TabFocused`, synchronous, when it
  activates an existing tab or creates the first — the shell's line
  after the leaf's, since Windows opens second (B2-D8), and BEFORE the
  load's asynchronous result (IGL-4); then B1's Term 9
  `LoadingConnections` iff the focus lands while the pin's load is
  still Loading (IGK-13); then, at the load's apply iff SPEAKING (Term
  2), the summary on success or `GraphBlocked{ConnectionsLoadFailed}`
  on failure (Term 5, IGK-14). Back speaks the open's `TabFocused`
  first, then the pop mutation's lines in the same order. Nothing else
  of this route's: no `OpenedFile` (the funnel does not post the shell's
  open line; the mac's `openFile` from this route announces nothing),
  no `ConnectionsPanel` line (Show's). B1's own lines keep their
  places — a completion in flight during Back's open, the shell's High
  missing-file line for a delete that lands mid-dialog — and the model
  compares the WHOLE timeline (B-10's rule). A refused re-root's open
  speaks what the gate posts and nothing more; the pin mutation's lines
  came before it.
- **Term 15 — The shared key.** `GraphViewState.SelectedKey` is written
  by exactly five writers: the leaf's `PinTo` (the same-root repair
  included), the leaf's `PopTo`, the leaf's `Retarget` when the key
  equals the old pin's (IGL-7), the graph DOCUMENT's guarded selection
  method (`SelectRow(key)` — A-7's write moves from the table VIEW into
  the document, which refuses when retired, when the key is not in its
  current snapshot, or when it is not the workspace's seated document;
  the view calls it and holds no write of its own — IGJ-6) and the
  document's revalidation clear (A-7); PR D adds the diagram as the
  sixth. Every key the leaf writes is core's, through
  `SlateUniffiMethods.GraphStableKeyForPath` bound at the write (the
  census reads the right-hand side, the crossing is counted once per
  pin, once per pop and once per key-moving retarget — IGI-19); a host-composed "p:" prefix is the
  offence the census and a mutation name. The property's backing field
  is written by its setter alone (IGK-19). The leaf's
  `SelectedOccurrence` never writes the key (B-D1, B2-D1). The
  document, while it lives, revalidates the key against its snapshot as
  A-7 says; a key written while no document lives is revalidated at the
  next document's first pair publication. `GraphViewState.Reset()` is
  DELETED (IGI-17): the workspace's disposal drops the object.
- **Term 16 — Lifecycle.** The pin, the note in view and the stack live
  on the leaf's document; a workspace teardown drops them with the leaf
  (B-1's drain); a launch comes up FOLLOWING with an empty stack;
  nothing is persisted (B2D-2). The two hooks are the whole rename and
  delete rule (IGI-8, IGJ-2, IGJ-10, IGJ-11, IGK-7; the mac has
  neither, B2-D3). After the workspace's disposal or the leaf's
  retirement every funnel returns false and every leaf entry refuses
  WITHOUT mutation (IGI-20); Back re-admits after its open, before its
  pop mutation, since the open's pump can tear either down (IGL-5).

### The contracts (slice B2)

**B2-1 — The view state is the workspace's (A-1 and spec R-B as amended
by the owner, B2D-1).** ONE `GraphViewState` per workspace, constructed
in the workspace's constructor beside the relay (NewGraphViewState
under `Graph/WorkspaceViewModel.Graph.cs`, the direct right-hand side
of a field assignment, no loop, lambda or branch above it — the IPB-27
shape) and handed to the graph document at construction
(`GraphDocumentViewModel`'s constructor takes it; `:136` no longer
constructs one) and to the leaf. `Retire()` no longer resets it (`:657`
goes) and `GraphViewState.Reset()` is deleted (Term 15). A re-seated
document reads the state the previous one left: the selection survives
closing and reopening the graph tab (B2-D4). The table view's direct
write moves into the document's guarded `SelectRow` (Term 15). The
censuses are COMPILATION-WIDE and symbol-based (IGJ-14) and judge by
TYPE as A-1's "no second mutable copy" requires (IGK-19): every mutable
field or property anywhere in the shell compilation whose type is
`GraphFilter`, `GraphSurfaceMode` or the groups' list, outside the one
instance and outside the immutable request, token, envelope and
publication records (exempted by their declared kinds), is an offence;
every assignment to `SelectedKey` is one of Term 15's five by owner;
the backing field's writers are the setter alone; an instance census
counts the constructions: exactly one, in the workspace's constructor.

**B2-2 — The root mode on the leaf's document.** `ConnectionsLeafViewModel`
gains `RootMode` (Following | Pinned with the path), `NoteInView`,
`Root` (now the EFFECTIVE root: the pin, else the note in view) and the
back stack of `(RootMode, string effective)` entries; and the entries
the design pass names — `PinTo(path)`, `PopTo(noteInView)`,
`Retarget(source, destination, activeAndMounted)`, `Prune(source)` —
each called ONLY by the workspace's funnels or hooks, each refusing
without mutation once retired (Term 16). `NoteChanged` while PINNED
records the note in view and returns before the transition (Term 11).
`GraphReRooted` is posted from `PinTo` and `PopTo` through the injected
relay, under `Graph/`: the announcement-seam census's inventory of the
leaf's posting sites gains the two, and the outside-wall inventory is
unchanged (IGK-18). The trigger census (B-19 (iii)) extends to the four
entries: their callers are the workspace's and nothing else,
method-group references refused. A `Func<bool>` seam BackFromSurface
is installed on the leaf by `NewConnectionsLeaf` for the view's key
owner (IGK-12).

**B2-3 — One re-root funnel, three ADDRESSED entrances, each walled
against a stale row.** `WorkspaceViewModel.Connections.cs` gains
ReRootConnectionsOn (Term 12; returns whether it pinned) and
ConnectionsBack (Term 13; returns whether it popped). The entrances:
the leaf's `Execute(ShowConnections, row)` through a new seam
ShowConnectionsFromRow, enabled exactly when core's vector lists the
action for the row's kind — a Note and an Attachment, both file-backed;
a Ghost never (IGI-11) — with no host-side kind predicate, and admitted
only while the row's tree was PRODUCED for the current effective root
(`IsRowCurrent`, TGB-9); the table's `ShowConnectionsFromSurface`, set
by the workspace to the addressed ReRootGraphRowFromSurface that runs
`FocusGraphAddress` first (A-8's rule; the mac's `focusOwningGroup()`
before `perform`, IGI-4) and admitted only while the captured row is
the SAME object in the document's current `Publication.Rows` — a
current-row guard by reference identity, not `ContainsNode`, which is
the snapshot's and A-7's (IGK-9) — a wall added for EVERY table action
as a B2 fix to A-8's admission (IGJ-7); the Bases' new row action
(B2-5), addressed by its source lease. `ActionDisabledReason(ShowConnections)`
returns null and `ConnectionsPhrase.ShowConnectionsUnavailable` is
deleted (B-D6 withdrawn). Two caller censuses, bound: ReRootConnectionsOn's
callers are exactly the three entrance delegates; ConnectionsBack's are
exactly the BackFromSurface delegate `NewConnectionsLeaf` installs
and ConnectionsBackCommand's body (IGI-2, IGK-12).

**B2-4 — Back: the chord row and the key owner.** A `ChordTable` row
`slate.graph.connectionsBack` ("Connections: Back"), mac `⌘[`, Windows
`Ctrl+[` (the ⌘→Ctrl rule; `Ctrl+Alt+[` is the sidebar's history and
`Ctrl+Shift+[` Previous Tab, both untouched), in a new
`ChordScope.Connections` delivered by the leaf's body
(`ConnectionsLeafView`'s `PreviewKeyDown` — the mac's panel-level
owner, so it works from the depth control and the anchor as from the
tree) and NOT by a window-level binding: the view matches
`Keyboard.Modifiers == ModifierKeys.Control` EXACTLY (IGK-16), calls
BackFromSurface and marks the event handled iff it returned true —
the mac's `.ignored` fall-through. ConnectionsBackCommand, resolved
by the registrar for the palette (R-E's drift test 1, the only one of
the three that sees a row with no menu item — IGI-16), runs the same
funnel; the row's resolver identity, its scope, the leaf's delivery,
the exact-modifier fall-through for `Ctrl+Alt+[` and `Ctrl+Shift+[`
from the tree, the depth control and the anchor, the empty-stack
fall-through, and the deliberate absence of a menu item are pinned by
dedicated facts. The mac's key owner accepts ANY modifier set that
contains Command (`ConnectionsPanel.swift:52–54`); Windows requires
Control alone — B2-D11 (IGL-10). The spec's §0, §1 table, §7 table,
R-E, §PR B's acceptance line (four connections ids) and §5.3's matrix
row (thirteen command rows) are amended in place for the row (IGI-15,
IGJ-17, IGL-11). This PR verifies `Ctrl+[` free in every scope.

**B2-5 — The Bases' Show connections, addressed.** A "Show connections"
row action after "Show backlinks" in `BaseSurfaceView`'s GRID row
actions, through a new seam on `BaseDocumentViewModel` that carries the
SOURCE — the invoking surface's tab — beside the row (the existing
`Action<BasesRow>` seams carry none, and a document shared by base tabs
in two groups cannot say which invoked it, IGJ-9): the workspace's
BasesShowConnectionsCommand validates the source tab is still hosted,
makes its group and the tab active (the table's `FocusGraphAddress`
shape), then enters the funnel with the row's file path. `RowCommand`
captures the RESULT it was built over and admits the row only while
that result is the document's current one (the stale-menu class,
TGB-9; IGJ-8). The LIST renderer gains nothing: it has no row-action
mechanism for any action, the Bases port's own gap against the mac's
`listRowActions` — recorded as B2-D9, not closed here (IGK-15). The
row's name is the `ShowConnections` spec's title from the Note vector
of `graph_row_actions`, fetched ONCE per process (design B; the
crossing counted) — not a Bases literal (IGI-21). The Bases post no
line of their own for it: the leaf's `GraphReRooted` is the route's
line (the mac's `basesShowConnections` posts no base-action event,
unlike `basesShowBacklinks` at `:2771–2776`). From a base tab there is
no effective root on Windows: nothing is pushed and the first Back
after it falls through (B2-D7).

**B2-6 — The follow rule while pinned (owner decision D-8: mac).** A
note change while PINNED — a tab activation, a close's successor, a
group close, an open from anywhere, the ghost create's open — records
the note in view and moves nothing (Term 11); `NotifyGraphOfVaultChange`'s
probe (Term 6) and a depth change (Term 3(e)) reload the PIN; a rename
retargets and a delete prunes as the hooks say; a launch comes up
FOLLOWING.

**B2-7 — The ghost create while pinned (B-11 over the effective root).**
The create is addressed by the leaf's effective root and epoch; its
open moves the note in view under the pin (the mac's order: the
structural refresh, the ownership check, the open at
`AppState+Connections.swift:301`, `NoteCreated` at `:304–305`, the
generation refresh at `:307–308`); the pinned root's neighbourhood
refreshes SILENTLY through the probe (the healed ghost row updates),
and B-D10's suppression reads the effective root.

**B2-8 — The model (B-10): the arrangements, the routes, the composed
routes, the whole state, the pumped dispatcher.** Three literal PINNED
arrangements (IGJ-12): PinnedFresh — pin P, note in view P, stack
`[(FOLLOWING, A)]`; PinnedDrifted — pin P, note in view N ≠ P, stack
`[(FOLLOWING, A), (PINNED B)]`; PinnedNoOrigin — pin P, note in view P,
stack empty. Every B1 route is crossed with each (3(d)'s routes
exercise Term 11; the rename and delete routes exercise the hooks over
the pin, the note in view and the entries, with a file and a folder).
New routes: ReRootFromLeaf, ReRootFromTable, ReRootFromBases (a Note
and an Attachment target each; the same-root case for the table and the
Bases — the leaf's is named unreachable), the open's gate decision as a
dimension of every re-root and Back route (allowed, refused), `Back`
(to FOLLOWING, to a prior pin, with an empty stack, from FOLLOWING,
after shutdown). The composed routes: the refusal after the pin (the
pin stands); a rename and a delete of the pin, of an entry and of the
just-pushed origin landing inside the open's dialog (file and folder);
Back with the reserved note renamed inside its open (nothing pops); a
re-entrant re-root and Back from the dialog; a completion of the
pin's load and of a pre-existing load during each open; the pin's load
FAILING and the pop's load failing (the failure line); the focus
request landing before and after the pin's and the pop's load applies
(Term 9's line) and the FINAL focused element after a drain — the leaf,
never the editor (IGL-3); an image attachment as the source tab and as
the target; a `.canvas` and a `.base` as the target and as a prior pin
restored by Back (IGL-1); a canvas origin; a re-root from a cold graph
tab, a warm graph tab and a base tab; a retirement, a tab change, a
group change, a tab close, a depth change and a probe landing inside
each open (IGL-5, IGL-6); the clean open's synchronous `TabFocused`
order (IGL-4); a rename of the pin with the graph document alive,
absent and re-seated (IGL-7). The state gains the mode, the note in
view, the shared key, the WHOLE stack (every entry's mode and
effective path — IGI-7), the pending mount and the focused element,
compared after every route beside the timeline, the loads, the root,
the staleness, the depth and the publication's state (TGB-11's rule).
The dialog is the pumped dispatcher's: the model's gate seam pumps a
real dispatcher frame while it decides, under the production scheduler
(IGK-20). The totals are literals in the model, published by the
task-loop record that lands it, as B1's were (IGL-9); every cell named
unreachable with its reason.

**B2-9 — The journey continues.** `GraphConnections_LeafWalkDepthAndReRoot_AreClean`
gains: Show connections on the healed incoming row (the heading moves
to that note, the tree re-roots and takes focus, the summary's text
changes), `Ctrl+[` from inside the tree (the heading and the summary
return), `Ctrl+[` again (nothing to pop: focus stays, no change), the
table's Show connections from the graph tab (the graph's group focused
first, the leaf re-roots and takes focus), and axe over
`graph-connections` after the re-root. The Bases' route is a fact over
the document's seam (a base fixture in the journey is PR-scope creep).

**B2-10 — The censuses.** (i) the view-state instance census (B2-1);
(ii) the shared-key writers census, compilation-wide: every assignment
to `SelectedKey` is one of Term 15's five by owner, the backing field's
writers are the setter alone, and the leaf's right-hand sides bind to
`GraphStableKeyForPath`; (iii) the two funnel-caller censuses (B2-3);
(iv) the trigger census's four new protected names (B2-2); (v) drift
test 1 over the new row and the dedicated facts of B2-4; (vi) the
no-shadow census, compilation-wide and by type (B2-1); (vii) the
announcement-seam census: the leaf's inventory gains `PinTo`'s and
`PopTo`'s posts, the outside-wall inventory unchanged, with a duplicate-
line and a bypass mutation (IGK-18); (viii) the delivery-evidence
census's command map gains the row; (ix) a membership census: every
row-action `Execute` under `Graph/` binds the current-row guard and the
Bases' `RowCommand` the captured-result guard before the seam.

**B2-11 — The matrix rows.** `parity_matrix.md`'s connections rows gain
re-root, Back and the Bases' action with their evidence ids;
`w_c_matrix.md`'s "Graph connections leaf (W6-2 PR B)" row cites the
continued journey; `chords.json` through the projection (never by
hand, the staged-claim rule).

**B2-12 — Core unchanged.** No new query: the stable key
(`graph_stable_key_for_path`), the row actions and their titles
(`graph_row_actions`, 0b-9), `GraphReRooted` (0a) are consumed as they
stand; the surface stays twenty-six.

### Decisions (PR B2)

- **B2D-1 — The owner amended A-1 and spec R-B on 2026-09-06:**
  `GraphViewState` is the workspace's, shared by the document and the
  leaf, surviving the document's retirement.
- **B2D-2 — Nothing persists:** the pin and the stack are session-scoped
  (the mac persists neither); a launch is FOLLOWING.
- **B2D-3 — Back's chord is `Ctrl+[` in `ChordScope.Connections`,
  delivered by the leaf's body with the exact modifier, falling through
  when there is nothing to pop**; the spec's §0, §1 table, §7 table and
  R-E amended for the row.
- **B2D-4 — The leaf's own selection stays its own** (`SelectedOccurrence`,
  B-6); only a pin and a pop write the shared key from the leaf's side
  (Term 15).
- **B2D-5 — The pin never crosses an open:** a re-root is a pin
  mutation on Show's shape then an ordinary open in its own mutation
  with the editor focus withheld; Back is such an open, a re-admission,
  then a pop mutation; nothing is held across either open.
- **B2D-6 — The same-root re-root repairs the shared key, reveals and
  focuses** without the suppression; B1's own triggers apply; no push,
  no load of its own, no `GraphReRooted`.
- **B2D-7 — A refusal undoes nothing:** a re-root's refused open leaves
  the pin (the mac's outcome); Back's refused open pops nothing (the
  mac would have popped) — and a pop proceeds only when the top entry
  names the note the open installed (B2-D5).
- **B2D-8 — Back from the palette reveals the pane and activates the
  leaf** as a re-root does, so its one load is not STALE; the mac's Back
  is panel-only and never faces the question (B2-D6).
- **B2D-9 — A rename retargets the pin, the note in view and the stack
  by the same-or-descendant predicate, a retarget of the pin being Term
  3(d)'s root move; a delete prunes the stack and keeps the pin** (the
  hooks; B2-D3).
- **B2D-10 — A re-entrant re-root or Back is an ordinary one:** nothing
  is refused for nesting, since nothing is held.

### Recorded divergences (PR B2)

- **B2-D1 — The leaf's row selection does not write the shared key**
  (B-D1, kept): the mac's leaf never writes `graphSelectedNodeKey` from
  a row; only re-root and Back do.
- **B2-D2 — No navigation-intent seam.** The mac records an explicit
  sidebar navigation intent on every re-root, the same-root case
  included (`AppState+Connections.swift:196`), and withholds the
  sidebar's selection revision on the open (`advancesSidebarSelectionRevision:
  false`). Windows has no twin of either: the files sidebar's history
  records only its own opens, so a re-root's open enters no sidebar
  history and `Ctrl+Alt+[` never steps through it; there is no intent
  to record (IGI-14).
- **B2-D3 — A rename retargets the pin and the stack; a delete prunes
  the stack.** The mac leaves both stale (`AppState+Connections.swift:48–51,
  195–255` carry no rename or delete arm); Windows follows its own
  `RetargetPath` discipline, descendants included (IGI-8, IGJ-11).
- **B2-D4 — The shared key survives the graph tab's close.** The mac
  clears it with the table's filter when the last graph tab closes
  (`releaseGraphStateIfUnreferenced` → `resetGraphTableState`,
  `AppState+GraphTable.swift:112–150`; `GraphTabRoutingTests.swift:352`);
  the owner's amendment keeps it on the workspace. The one-line
  alternative — the workspace resets the key and the filter when the
  last graph tab closes while the leaf is FOLLOWING — is the owner's to
  take (IGI-12).
- **B2-D5 — Back's refused open pops nothing, and a pop needs the
  opened note.** The mac pops before its `Void` `openFile`; Windows
  opens first and pops only when the top entry, as retargeted, names
  the note the open installed. A re-root's refused open leaves the pin
  on both platforms (B2D-7; IGI-13, IGK-3).
- **B2-D6 — Back is a command.** The mac's Back is the panel's key alone
  (no catalog id); Windows' `slate.graph.connectionsBack` is
  palette-reachable (R-E) and, from the palette, reveals and activates
  (B2D-8).
- **B2-D7 — No origin from a graph, a base or a canvas tab.** The mac's
  effective path from a warm graph tab is the last note and from a base
  tab the `.base` file itself, so its re-root pushes those and Back
  returns to them; Windows' note in view is the Markdown-kind tab in
  view and null otherwise (`WorkspaceViewModel.Connections.cs:197`), so
  a re-root from a cold or a warm graph tab, a base tab or a canvas tab
  pushes nothing and the first Back after it falls through. Recorded;
  the owner may ask for the mac's warm-graph memory (IGJ-5).
- **B2-D8 — The open's shell line comes after the leaf's, before the
  load's result.** The mac opens first and silently; Windows pins first,
  so the open's synchronous `TabFocused` follows the synchronous
  `GraphReRooted` and precedes the asynchronous summary (Term 14,
  IGL-4).
- **B2-D9 — The Bases' list renderer carries no row actions**, this one
  included: the mac's `listRowActions` carry the set
  (`BaseContainerView.swift:323–368`; `p1_spec.md:51`), the Windows
  list renderer has no mechanism for any (`BaseSurfaceView.cs:892–927`)
  — the Bases port's gap, recorded here, not closed here (IGK-15).
- **B2-D11 — Back's chord needs Control alone.** The mac's key owner
  fires on any modifier set containing Command
  (`ConnectionsPanel.swift:52–54`); Windows matches
  `ModifierKeys.Control` exactly so that `Ctrl+Shift+[` (Previous Tab)
  and `Ctrl+Alt+[` (the sidebar's history) keep their owners (B2-4,
  IGL-10).
- **B2-D10 — An open renamed mid-dialog installs the old path** on
  Windows (`OpenPathCore` builds the item before the gate and
  `TryOpenItem` installs it after, `WorkspaceViewModel.cs:1997–2008`,
  `WorkspaceViewModel.Layout.cs:136–155`) — a property of every open,
  outside this PR; B2's pop refuses on it (B2-D5) and its pin does not
  depend on it (IGK-3).

### The rounds — the ledger

#### Round 1 on revision 1 — twenty-two findings (IGI-1..22): ten blockers, eleven majors, one minor

| # | Severity | Disposition |
|---|---|---|
| IGI-1 | BLOCKER | the boundary had no trigger for the pin's load — revision 2's pending-first boundary (reopened by IGJ-1, IGJ-4), revision 3's commit (reopened by IGK-1, IGK-2); revision 4: the pin mutation issues its own load on Show's shape, no boundary hook (design pass II) |
| IGI-2 | BLOCKER | Back popped before the open — revision 2's peek (reopened by IGJ-3), revision 3's reservation (reopened by IGK-4); revision 4: the open first, the pop re-reads the top entry and needs the opened note (Term 13) |
| IGI-3 | BLOCKER | the same-root case issues no re-root load and no `GraphReRooted`; B1's triggers stand and are modelled (B2D-6) |
| IGI-4 | BLOCKER | the table's entrance is addressed through `FocusGraphAddress`; revision 1's B2-D3 deleted |
| IGI-5 | BLOCKER | no push without an effective root; reopened by IGJ-5 (the mac's warm graph and base origins); B2-D7 and the three modelled origins |
| IGI-6 | BLOCKER | the push captures the EFFECTIVE root — now by the leaf itself at the pin (Term 12) |
| IGI-7 | BLOCKER | the whole stack compared; the composed routes; the leaf's same-root unreachable; completed by IGJ-12's arrangements |
| IGI-8 | BLOCKER | Retarget and Prune — reopened by IGJ-2, IGJ-3, IGJ-10, IGJ-11 and IGK-7; revision 4: the two hooks with nothing pending to protect |
| IGI-9 | BLOCKER | the pane first, then the leaf — Show's shape (design pass II) |
| IGI-10 | BLOCKER | the open's `TabFocused` in the timeline — now AFTER the leaf's lines (Term 14, B2-D8) |
| IGI-11 | MAJOR | Note and Attachment per core's vector, a Ghost never; completed by IGJ-13 and IGK-10 |
| IGI-12 | MAJOR | the mac's reset at the graph tab's close cited; B2-D4 with the owner's alternative |
| IGI-13 | MAJOR | B2-D5 rewritten in revision 4: the refusal undoes nothing, as on the mac |
| IGI-14 | MAJOR | B2-D2: no sidebar history entry, no intent seam |
| IGI-15 | MAJOR | the spec's §0, §7 row and R-E amended; the mac's Back is a key, not a command (B2-D6) |
| IGI-16 | MAJOR | drift test 1 alone; dedicated facts (B2-4) |
| IGI-17 | MAJOR | `Reset()` deleted; reopened by IGJ-6 for the table view's write; the document's guarded `SelectRow` |
| IGI-18 | MAJOR | the wall widened; reopened by IGJ-14 and IGK-19; compilation-wide by type (B2-1) |
| IGI-19 | MAJOR | the leaf's right-hand sides bound to `GraphStableKeyForPath`, the crossing counted, a "p:" mutation |
| IGI-20 | MAJOR | the funnels return false and the entries refuse after disposal or retirement; reopened by IGJ-3, IGJ-6 and IGK-4; revision 4 holds nothing across the open |
| IGI-21 | MAJOR | the title from the fetched-once Note vector of `graph_row_actions` |
| IGI-22 | MINOR | the citation and the order corrected (B2-7) |

#### Round 2 on revision 2 — seventeen findings (IGJ-1..17): eleven blockers (three created by revision 2 — rule 5, first time), three majors, three minors

| # | Severity | Disposition |
|---|---|---|
| IGJ-1 | BLOCKER | created by revision 2 — revision 3's commit with the final candidate (reopened by IGK-1, IGK-2, IGK-5, IGK-6, IGK-8); revision 4: the pop mutation runs AFTER the ordinary open, with the note in view already final (Term 13) |
| IGJ-2 | BLOCKER | created by revision 2 — revision 3's registered proposal (reopened by IGK-3, IGK-7); revision 4: nothing is held across the open |
| IGJ-3 | BLOCKER | the revisioned reservation (reopened by IGK-4); revision 4: the pop re-reads the top entry after the open and needs the opened note |
| IGJ-4 | BLOCKER | created by revision 2 — one slot, a depth counter (reopened by IGK-8, IGK-11); revision 4: nothing is held, so nothing is refused for nesting (B2D-10) |
| IGJ-5 | BLOCKER | the mac's warm-graph and base origins traced; B2-D7 |
| IGJ-6 | BLOCKER | the table view's write into the document's guarded `SelectRow` (Term 15) |
| IGJ-7 | BLOCKER | a wall before every table action's seam — corrected by IGK-9 to a current-row guard by reference identity |
| IGJ-8 | BLOCKER | `RowCommand` captures the result and admits the row only while it is current (B2-5) |
| IGJ-9 | BLOCKER | the Bases' seam carries the source tab; the command focuses it (B2-5) |
| IGJ-10 | BLOCKER | a retarget of the pin is Term 3(d)'s root move with the classification passed in (the hooks) |
| IGJ-11 | BLOCKER | `IsSameOrDescendantPath` everywhere; folder routes (the hooks, B2D-9) |
| IGJ-12 | MAJOR | PinnedFresh, PinnedDrifted, PinnedNoOrigin, each crossed with every B1 route (B2-8) |
| IGJ-13 | MAJOR | Attachment targets and sources — corrected by IGK-10: an attachment is a Markdown-kind tab and the note in view |
| IGJ-14 | MAJOR | compilation-wide censuses — completed by IGK-19: by type, and the backing field's writers |
| IGJ-15 | MINOR | `RetargetPath`'s scope corrected; the sidebar's refresh the adapter's |
| IGJ-16 | MINOR | `OpenPathCore` and `TryOpenItem` both cited; `showConnectionsPanel` at `:182–187` |
| IGJ-17 | MINOR | the spec's architecture table amended |

#### Round 3 on revision 3 — twenty-one findings (IGK-1..21): fifteen blockers (five created by revision 3 — rule 5, second time), five majors, one minor; the second design pass

| # | Severity | Disposition in revision 4 |
|---|---|---|
| IGK-1 | BLOCKER | taken — the boundary's true order recorded (depth zero at `SyncPanels`, the funnel reconciles at once, the replay a no-op); no boundary hook: the pin mutation issues its load directly (design pass II) |
| IGK-2 | BLOCKER | taken — no classification at the pin: the mutation constructs the leaf active and mounted, as Show does |
| IGK-3 | BLOCKER | taken — created by revision 3: nothing is held across the open; the open's rename-mid-dialog property recorded as B2-D10; the pop refuses when the top entry does not name the opened note |
| IGK-4 | BLOCKER | taken — the key and the line are written INSIDE the pin and pop mutations by the leaf's entries, with nothing to validate later |
| IGK-5 | BLOCKER | taken — the pin advances the sequence BEFORE the re-root's open (a completion in flight is foreign); Back's open admits B1's own completion line in the whole timeline (Term 14) |
| IGK-6 | BLOCKER | taken — created by revision 3: no deferred intents; a depth change, a probe or a pin retarget after the pin is B1's ordinary rule over the pin |
| IGK-7 | BLOCKER | taken — created by revision 3: the origin is pushed at the instant of the pin, so a later delete prunes it like any entry |
| IGK-8 | BLOCKER | taken — created by revision 3: no transition record, no state machine; the pin mutation unwinds as Show's would |
| IGK-9 | BLOCKER | taken — the table's wall is a current-row guard by reference identity in `Publication.Rows`; `ContainsNode` stays A-7's snapshot check (B2-3) |
| IGK-10 | BLOCKER | taken — Windows has no attachment tab kind: an attachment is a Markdown-kind tab and the note in view; the model's attachment cells follow (Term 11, B2-8) |
| IGK-11 | BLOCKER | taken — created by revision 3: admission (disposed, retired) first, then the same-root case; a re-entrant call is an ordinary one (B2D-10) |
| IGK-12 | BLOCKER | taken — BackFromSurface (`Func<bool>`) installed by `NewConnectionsLeaf` for the view; ConnectionsBackCommand for the registrar; the caller census over both (B2-2, B2-3, B2-4) |
| IGK-13 | BLOCKER | taken — the focus request is Show's, in the pin and pop mutations; Term 9's line admitted in Term 14 |
| IGK-14 | BLOCKER | taken — the summary OR `GraphBlocked{ConnectionsLoadFailed}` at apply (Term 14); the failing pin's load a composed route |
| IGK-15 | BLOCKER | taken — the grid's row actions gain the action; the list renderer's absence of any row-action mechanism recorded as the Bases port's gap (B2-D9) |
| IGK-16 | MAJOR | taken — the exact `Control` modifier; facts for `Ctrl+Alt+[` and `Ctrl+Shift+[` from the tree, the depth control and the anchor (B2-4) |
| IGK-17 | MAJOR | taken — the entrance's source-focus line first; the shell's lines at their positions; the whole timeline compared (Term 14) |
| IGK-18 | MAJOR | taken — `GraphReRooted` posted from the leaf's entries under `Graph/`; the seam census's leaf inventory gains them, with mutations (B2-2, B2-10) |
| IGK-19 | MAJOR | taken — the no-shadow census by type; the backing field's writers the setter alone (B2-1, Term 15) |
| IGK-20 | MAJOR | taken — the state and the composed routes enumerated; the gate seam pumps a real frame under the production scheduler (B2-8) |
| IGK-21 | MINOR | taken — the citation corrected (Term 16) |

#### Round 4 on revision 4 — thirteen findings (IGL-1..13): seven blockers (five created by revision 4 — rule 5, third time), four majors, two minors → THE FREEZE at revision 5

| # | Severity | Disposition in revision 5 (the discharge; carried to the task loop) |
|---|---|---|
| IGL-1 | BLOCKER | taken — created by revision 4: `.canvas` and `.base` are Attachment nodes that open as Canvas and Base tabs (a null candidate); the open reports the installed path and the candidate separately, `PopTo` compares the top against the installed path and sets the note in view from the candidate (Term 11, Term 13, B2-8) |
| IGL-2 | BLOCKER | taken — created by revision 4: the boundary records through its own `SyncPanels` and reconciles ONCE with the last candidate; no replay of an older one (the boundary, corrected) |
| IGL-3 | BLOCKER | taken — created by revision 4: the route's open withholds its Input-priority editor-focus request; the leaf's request is queued after the open; the final focused element asserted after a drain (the re-root's open, B2-8) |
| IGL-4 | BLOCKER | taken — created by revision 4: `GraphReRooted`, then the open's synchronous `TabFocused`, then the asynchronous load result (Term 14, B2-D8) |
| IGL-5 | BLOCKER | taken — created by revision 4: Back re-admits after its open, before the pop mutation; retirement inside each open modelled (Term 16, B2-8) |
| IGL-6 | BLOCKER | taken — stood before: `TryOpenItem` validates, after the gate and for every open, that the workspace is live and the captured tab's group is hosted and still holds it, else false before replacement; the pump routes modelled (the re-root's open, B2-8) |
| IGL-7 | BLOCKER | taken — stood before: `Retarget` is the fifth key writer, conditionally, through the FFI; the censuses count it; the document alive, absent and re-seated modelled (Term 15, the hooks) |
| IGL-8 | MAJOR | taken — `RunWorkspaceMutation(() => opened = OpenPathCore(target, CurrentTab, editorFocus: false))`; Back proceeds iff opened (design pass II) |
| IGL-9 | MAJOR | taken — the composed routes enumerated; the totals are the model's literals, published by the record that lands it (B2-8) |
| IGL-10 | MAJOR | taken — B2-D11: Control alone on Windows, any set containing Command on the mac |
| IGL-11 | MAJOR | taken — the spec's §PR B acceptance (four connections ids) and §5.3 (thirteen command rows) amended in place |
| IGL-12 | MINOR | taken — the arithmetic (sixty findings, thirty-six blockers before round 4) and the class's scope corrected |
| IGL-13 | MINOR | taken — the introduction says the order is deliberately not the mac's |

**The freeze.** Rule 5 fired at rounds 2, 3 and 4: three revisions in
a row created the next round's blockers inside one subsystem, the
pump subset. Under the PR 0b precedent, as B1's section at revision
8, the section is frozen at revision 5 with the round-4 text as its
discharge and the four ledgers as the task loop's; the task loop
discharges each row by code — a fact, a mutation, a record — and any
row it cannot discharge as written is recorded as a verified deviation
in its TGB2 record. Precedent applied; the owner may overrule.

### Task loop — records (PR B2)

**TGB2-1 — T1: the workspace's one view state, the document's guarded
selection (B2-1, Term 15's document side).** `GraphViewState` is
constructed ONCE, in the workspace's constructor beside the relay
(`NewGraphViewState`, the direct right-hand side of the field's
assignment), handed to the graph document at construction (the
constructor's third parameter; a bare document in a fact passes its
own) and read back through `GraphViewStateForTests`; `Retire()` no
longer resets it and `GraphViewState.Reset()` is gone — a closed graph
tab leaves the selection on the workspace (B2-D4) and a reopened one
seats a document over the SAME instance, which revalidates the key it
inherits at its first pair publication (A-7). The table view's
current-row write moves into the document's `SelectRow(key)`, which
refuses once retired, when the document is not the workspace's seated
one (the factory's `isSeated` names the funnel's field; a bare
document is its own) and when the current snapshot lacks the key
(IGJ-6). Facts: `ClosingTheLastGraphTabRetiresTheDocumentAndAReopenSeatsAFreshOne`
now asserts the survival, the shared instance and the refusal of the
retired document; `SelectRowRefusesAnAbsentKeyAnUnseatedDocumentAndARetiredOne`
(a second document over the same state, unseated by its predicate);
`ARetainedTableViewOverAClosedTabWritesNothing` (the live grid's row
writes through the document, the retained one over the closed tab
moves nothing). Censuses: `ExactlyOneGraphViewStateIsConstructedInTheShell`
(the relay census's shape — every bound creation the factory's, the
factory's one call the constructor's direct assignment, no method-group
reference); `TheSharedKeyIsWrittenByTheNamedOwnersAlone` (every bound
assignment to `SelectedKey` in the shell compilation is `SelectRow`'s
or `RevalidateSelection`'s — T2 adds the leaf's three — and the
backing field is written by the setter alone, `ref` arguments
included, IGK-19); `NoMutableShadowOfTheViewStateExistsInTheShell`
(compilation-wide by TYPE — the filter, the surface mode, a list of the
groups — with A-1's NAME rule kept under `Graph/`, where it belongs: a
canvas's text `Filter` or the workspace's `Mode` elsewhere is its own,
which the first run found). Mutations, each restored byte for byte, each caught by the named fact: retirement clearing the key (the retirement fact); a second `GraphViewState` constructed in the document factory (the instance census); the table view writing the key itself (the writers census); `SelectRow` without its snapshot guard, and a document seated unconditionally (the refusal fact, both); a mutable `GraphFilter` field under another name in the workspace's graph partial (the no-shadow census) — six, none survived. Gates: `dotnet format` clean; the whole Windows test project on a fresh build, 2245 passed without the model; the model alone, 1383 cells in 5 m 52 s; the accessibility project built and the leaf journey passed in the foreground, 14 s; benchmarks build (its one document construction takes the view state); CI and codoki on the push are the oracle.

### Tests that pin PR B2 (revision 5's list; the task loop records what lands)

- `ConnectionsLeafTests.ReRoot.cs`: the pin mutation's order on Show's
  shape (the reveal, the activation, the mount inert, `PinTo`'s push
  and pin and one load, the key, the line, the focus request), the
  ordinary open after it with the editor focus withheld (allowed and
  refused: the pin stands; the leaf the final focused element), the
  same-root return with each B1 trigger, Back's open then pop (allowed;
  refused: nothing pops; the top entry renamed inside the open:
  nothing pops), Back to FOLLOWING with a drifted note in view (one load
  of the opened note), Back to a prior pin, Back with an empty stack
  and from FOLLOWING falling through, a re-entrant re-root and Back
  from the dialog, the pinned note's change recorded and not loaded,
  the probe and the depth change over the pin, the rename of the pin
  (the audible root move) and of an entry and of a just-pushed origin
  and their folders, the delete pruning the stack, the pin's load
  failing (the failure line), the focus request landing while Loading
  (Term 9's line), the ghost create while pinned, an attachment as the
  source and the target, the shared key after each through the FFI
  crossing, every funnel and entry after shutdown.
- The model (B2-8), alone in its collection, under the pumped
  dispatcher with the production scheduler.
- `GraphDocumentTests`: the injected view state; retirement leaves it;
  a re-seated document revalidates the key it inherits; `SelectRow`
  refusing when retired, unseated or the key absent; a retained table
  view across close and reopen writing nothing; `Execute` refusing a
  captured row a rows-only republish removed while its node stays in
  the snapshot.
- The Bases facts: the row action's name from the fetched vector, its
  source-carrying seam, the command's focus of the source tab, a shared
  document in two panes, the stale-result refusal, the no-origin push.
- `ChordTableTests` and the dedicated facts of B2-4: the row, its
  scope, the palette reachability through ConnectionsBackCommand, the
  leaf's delivery and exact-modifier fall-through, no menu item,
  `Ctrl+[` free elsewhere.
- The censuses of B2-10, with mutations for each shape they name.
- The journey (B2-9), run to its last step locally before every push
  (the foreground rule).

<!-- end of the graph contracts document -->
