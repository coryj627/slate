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
1; every seam it names is cited at the line it sits on at `main` after
PR 0b (#1180) merged, and a stale spec citation is corrected here rather
than repeated (0bD-12).

**What stands today.** The Windows shell already has the singleton's
LIFECYCLE and nothing of its surface: `OpenGraph()` opens
`WorkspaceItemKind.Graph` at the literal path `graph:singleton`
(`WorkspaceViewModel.cs:1946–1947`; restore coerces every graph tab to
that token, `WorkspacePersistence.cs:377`); `TryOpenItem` collapses a
second open onto the one tab through `TryFocusGlobalGraph`
(`WorkspaceViewModel.Layout.cs:103–108, 178–192`); the split refusal
announces `GraphOpensSinglePane` (`:229–233`), the duplicate refusal is
silent (`:424–428`), the split command is disabled on a graph tab
(`:294–295`), and reopening a closed graph tab announces `ReopenedGraph`
(`:461–468, 475`; both events are shell-level `A11yEvent` arms,
`a11y.rs:601, 616`). The tab renders the placeholder: `IsPlaceholder`
is true for anything that is not markdown, base, saved query, dashboard
or canvas (`WorkspaceViewModel.cs:323–324`), `KindLabel` says "Graph"
(`:330`), and `PlaceholderText` composes the docked-surface sentence
(`:334–335`) that `WorkspaceTemplates.xaml:539, 547` shows.
`W1WorkspaceRedTeamTests` pins the dedup, the duplicate-and-split
refusals (`:101–128`) and the restore collapse
(`RestoreRejectsMoreThanSixGroups_AndDeduplicatesGraphGlobally`,
`:336–378`). Documents attach through ONE funnel,
`AttachTabDocumentsIfNeeded` (`WorkspaceViewModel.Bases.cs:1060–1079`),
whose doc-comment (`:1042–1059`) names its five call sites —
`TryOpenItem`'s in-place arm (`Layout.cs:161`), `AddTab` (`:211`),
`DuplicateActiveTab` (`:444`), `RestoreNode` (`Persistence.cs:83`),
`ReloadOpenTabFromDisk` (`History.cs:248`) — and
`CanvasAnnouncerCensus.TheAttachFunnelDocCommentNamesEveryCallSite`
(`CanvasAnnouncerCensus.cs:221–222`) fails when the comment and the
sites disagree. The canvas is the per-tab template: the tab's `Canvas`
property and `AttachCanvasDocument` (`WorkspaceViewModel.cs:296–309`;
the spec's `:289–300` drifted), the registry `CanvasDocumentFor` over
`_canvasDocuments` (`WorkspaceViewModel.Canvas.cs:23–24, 41–43`), its
release sweep (`:293–305`) and teardown (`:386–391`). The grid
substrate is `AccessibleDataGrid`: `Bind(columns, rows, summary,
accessibilityLabel, rowAudioDescription, rowActions, exportProducer,
rowActivated)` (`Grids/AccessibleDataGrid.cs:429–437`), the `Announce`
seam a funnel swaps (`:64`; the default posts through
`AccessibilityNotificationDispatcher`, `:173`), `ExternalSortHandler`
(`:655–663`: the handler dispatches, rows arrive by a later `Bind`, the
grid neither reorders nor announces), `SetSortIndicator` (`:668–685`),
`GridAutomationId` and its `…Summary` twin (`:408–419`), the summary
region named `Summary: {summary}` and the grid named by the label
(`:524–525`), `AccessibleGridColumn` with `Header`, `Cell`, `Sort`,
`AccessibilityHint`, `IsRowHeader`, `IsExternallySortable`
(`:1454–1476`), `AccessibleGridRowAction` with `Name`, `Execute`,
`IsVisible`, `IsEnabled`, `DisabledReason` (`:1481–1491`), the row
header seated per realized row (`:203–209, 512–515`), the cell name
`{Header}: {cell}` (`:1441–1444`), activation by key and double-click
(`:868, 1162`). It has NO item-status seam. The bases table is the
externally-sorted, core-column-driven consumer to copy:
`GridAutomationId = "BaseTabGrid"`, `ExternalSortHandler = OnExternalSort`
(`Bases/BaseSurfaceView.cs:164–168`), columns built in a loop over the
result's column vector with `IsRowHeader = column.Role ==
ColumnRole.Primary` and `IsExternallySortable = true` (`:648–669`), the
row-action literal (`:690–713`), `Bind` then `SetSortIndicator`
(`:714–728`); the canvas table is the funnel-swap precedent
(`Canvas/CanvasTableView.cs:49–58`: `Announce = _ => { }` until the
document attaches; the swap at `:641, 648`). Off-dispatcher work rides
`PanelWorkScheduler` (`Panels/PanelWorkScheduler.cs:16`: `StartWork`
`:75–95`, `Post` `:194`, `DrainForTests` `:174`; its doc `:9–15` says
subclasses guard their own publishes with generation or request
tokens), and `AccessibilityNotificationDispatcher.Post` renders and
raises (`:22–26, 36–51`). The vault-event fan-out is
`VaultLifecycleViewModel.HandleFileChange` (`:713–778`): generation-
guarded, it invalidates paths, notifies reading, bases
(`NotifyBasesOfVaultChange`, `:753, 764`) and history (`:759`), and
refreshes the sidebar under a 150 ms ticket (`:767–776`). The canvas
announcer is the relay-and-coalescer template
(`Canvas/CanvasAnnouncer.cs:28, 35, 45`: `Announce` `:89`, `Relay`
`:102`, `Emit` `:154`, `CoalescingClassOf` `:240`, `Debounce` `:253`,
`Fire` `:263`, the High flush-and-drop `:294, 303`), and two censuses
wall the `Canvas/` directory:
`AnnouncementSeamCensus.NoCanvasCodeReachesTheAnnouncerExceptThroughTheBoundary`
(`AnnouncementSeamCensus.cs:353`, sources `:526–534`, the named seams
each hit `:518–524`, the wall-is-the-directory caveat `:345–351`) and
`CanvasAnnouncerCensus` (`:21`; `NoCanvasSourceAnnouncesOutsideTheRelay`
`:38`, `EveryGridUnderCanvasRidesTheRelay` `:111`,
`TheRelayIsTheOneFileThatRenders` `:207`). The chord table's row shape
is `ChordTableEntry` (`Commands/ChordTable.cs:77–127`), `ChordScope`
ends at `Canvas` (`:21–70`; there is no `Graph` member),
`CommandSection.Graph` exists in the binding
(`slate_uniffi.cs:30838`), the chordless-row template is
`Reg(Ids.CanvasShowOutline, …, CommandSection.Canvas, …)` (`:923–924`,
rationale `:906–920`), and `Reg` forces `Scope = ChordScope.None` when
the Windows chord is null (`:578`); `chords.json` is regenerated by the
env-gated test (`ChordTableTests.cs:31`, SLATE_CHORDS_UPDATE) whose
projection preserves `deliveryEvidence` (`ChordTable.cs:481–489`;
`chords.json:2148`), validated by `generate-parity-matrix.py:802–809`.
The benchmarks project selects suites by flag (`Program.cs:12–17`,
`--canvas`, `--validate-budgets`) and asserts a budget per report with a
`PASS`/`MISS` line and a non-zero exit (the `--canvas` arm; the class
shape `CanvasRendererBenchmarks.cs:17–21`); `w_c_matrix.md` rows are ten
columns (`:5–6`; the canvas table row `:42` is the template) pinned by
`WcMatrixCanvasEvidenceCensus`. The sidebar's "Reveal" is
"Reveal in File Explorer" over ITS selected node
(`FilesSidebarViewModel.FileManagement.cs:1081–1107`, FD-5), not a
select-this-path verb; its untitled-note create is
`CreateOutcomes.CreateReporting` under the unique-untitled sequence
(`FilesSidebarViewModel.cs:1219–1262`); `StructuralMutationBusyReason`
is declared and never emitted — Windows has no structural-mutation gate
(`Commands/SlateCommandRegistrar.cs:176–196`). The binding already
carries everything this PR consumes: `GraphGeneration()`,
`GraphSnapshot(filter)`, `GraphTableRows(query, sort)` on the session
(`lib.rs:1323, 1301, 1379–1383`); the free `GraphTableColumns()`,
`GraphRowActions(kind)`, `GraphSurfaceModes()`,
`GraphStableKeyForPath(path)` (`lib.rs:3922, 3794, 4195, 3596`); the
records `GraphTableRow` (`stable_key`, `node_id`, `label`, `path`,
`kind`, nine `cells`, the five counts, `component`, `modified_ms`),
`GraphTableRows` (`generation`, `total`, `rows`), `GraphTableSort`,
`GraphVisibilityQuery`, `GraphTableColumnSpec`, `GraphRowActionSpec`,
`GraphSurfaceModeSpec` (`graph_queries.rs:565–626`; the FFI mirrors),
`GraphSnapshot.audio_summary`, and the 0a family under
`A11yEvent::Graph` (`a11y.rs:1271–1273`): `GraphRow{verbosity, row}`
over `GraphRowCopy` (`:3013–3023`), `GraphSnapshotSummary{counts}`,
`GraphFilterCount`, `GraphMode{mode}`, `GraphStatus{note}` with
`GraphStatusNote::{Opened, AlreadyOpen, ConnectionsPanel, NoteCreated,
NoConnections, LoadingConnections}` (`:3086–3093`),
`GraphBlocked{reason}` with `GraphBlockedReason::{LoadFailed,
ConnectionsLoadFailed, NoteCreateFailed}` (`:3098–3103`), `GraphBlocked`
High and the rest Medium (`:3234–3241`), and the four coalescing
classes `navigation` = `GraphRow`, `filter` = `GraphFilterCount`,
`forceValue`, `settle`, everything else immediate, a High graph event
flushing and dropping all four (`:138–152`). The mac's table half, the
parity target, is `AppState+GraphTable.swift`: `openGraphTab`
(`:45–73`) finds-or-opens the singleton (`:60–70`) and announces
`GraphStatus{Opened}` unconditionally after activation (`:72`);
`activateGraphTab` (`:80–118`) loads the config, applies the persisted
filter and calls `loadGraphTable` (`:105–109`);
`releaseGraphStateIfUnreferenced` resets every field on the last close
(`:131–150`, the default sort literal `:135`, the seen generation
`:149`); `loadGraphTable` (`:169–262`) fetches the snapshot AND the rows
in one detached task (`:189–200`), re-checks session and load sequence
(`:206–208`), decides whether to speak AFTER the fetch (`:215`),
publishes the snapshot with its filter (`:219–222`), publishes the rows
by token (`:226`), lets a pending preset supersede the summary
(`:235–243`; the spec's PR C citation `:195–199` drifted), announces
`GraphSnapshotSummary` (`:245–248`) or the filter count (`:249–253`),
and on failure sets the error, drops the snapshot, fails the token and
announces `GraphBlocked{LoadFailed}` (`:257–265`); the token API is
`issueGraphTableToken` (`:274–281`), `receiveGraphTableRows`
(`:291–310`: the four-step publish rule), `failGraphTableRows`
(`:313–316`), `requestGraphTableRows` (`:321–356`) and
`setGraphTableSort` (`:359–362`). `GraphTableView.swift` binds the
ACCEPTED sort to the grid and writes a REQUEST (`:213–226`), binds the
selection straight to `graphSelectedNodeKey` (`:239–244`;
`AppState.swift:3108`), switches loading → error → empty → grid
(`:246–260`; "Loading graph…" `:300–307`, "Graph error: …" `:309–316`,
"No notes match the current filters." `:257`), filters nothing itself
(`:320`), binds the grid with the summary, the label "Graph, data
grid", the local-sort-off flag, activation and modified activation, the
row actions and the relay (`:322–341`), opens in the current or a new
tab (`:343–357`), and builds every column from `graphTableColumns()`
(`:451–478`; no column is listed in Swift). `GraphViewState.swift:15–54`
is the action sugar over `graphRowActions(kind:)`; `GraphAnnouncer.swift`
is the relay-and-coalescer (`:101, 113, 128, 140–161, 165, 175, 225`).
`GraphTabRoutingTests.swift` pins the tab lifecycle
(`testOpenGraphTabRoutesAndDedups` `:48`, `testGraphTabLoadsSnapshot`
`:63`, `testGraphTabSurvivesSerializationRoundTrip` `:73`,
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
`CreateNote` alone for a ghost). The 0b artifact's `table` section
carries sixteen entries — the `all` query under eight columns × two
directions, every column but Modified — of twelve rows each keyed by
`stable_key` (`parity_golden/graph_queries.json`).

### Design — three rules, carried from PR 0b's pass

PR 0b's design pass (rules A, B, C above) was written for the mac
consumer and the harness; PR A is the first WINDOWS consumer, and each
rule has a Windows instance this section derives its contracts from,
so that a codex round finds the rule's next instance already covered
rather than a site the text forgot.

- **Rule A on Windows — one authority per consumer, one token per
  request, one publish.** The document is the table's only consumer in
  this PR. Its authority is the held snapshot with the filter it was
  fetched under and its generation; its token is (session identity,
  lifecycle generation, the full request record = query + sort, a
  sequence number); a rows result publishes only under a current token
  whose generation equals the authority's, rows-first in one
  dispatcher action; a stale, mismatched, out-of-order or
  failed result publishes NOTHING, and a generation mismatch re-fetches
  silently. The vault-change probe is a token issue like any other. A
  later PR that adds a consumer (the leaf, the diagram) adds an
  authority and a token, never a second reading of this one (A-2, A-3).
- **Rule B on Windows — one crossing per semantic epoch, every ordered
  inventory a core vector.** One worker body fetches the snapshot and
  the rows for one epoch (an open, a sort, a vault change); the columns,
  the row actions and the mode-switcher items are core's vectors
  iterated in order, and the default sort is fetched, not typed (AD-1);
  a census reads the shell for a typed column header and fails on one
  (A-5, A-8, A-11).
- **Rule C on Windows — evidence is core's rows, walked.** The
  document's published rows over the graph vault are compared to the 0b
  artifact's table section cell by cell for all sixteen sorts (A-14);
  the announcements go through one relay walled by the directory
  censuses (A-10); the FlaUI journey walks the grid by UIA and asserts
  the row names verbatim (A-16). Nothing in this PR is asserted about;
  it is asserted on.

### Contracts

**A-1 — One document per workspace, attached through the funnel,
released with the last graph tab.** A `Graph/` directory under the
Windows shell holds GraphDocumentViewModel (a `PanelWorkScheduler`
subclass, the canvas's base), GraphViewState, GraphSurfaceView (XAML +
code-behind), GraphTableView and GraphAnnouncer — new files; the
directory is the wall A-10's censuses read. The workspace holds AT MOST
ONE document, created on the first attach and kept in a nullable field
beside `_canvasDocuments` (`WorkspaceViewModel.Canvas.cs:23–24`), keyed
by nothing — the singleton IS the key (`graph:singleton`,
`WorkspaceViewModel.cs:1946`). Every tab of kind Graph attaches through
`AttachTabDocumentsIfNeeded` (`WorkspaceViewModel.Bases.cs:1060–1079`)
by a new arm beside the canvas's, so the five sites its doc-comment
names — and `TheAttachFunnelDocCommentNamesEveryCallSite` — cover the
graph without a sixth site; the tab gains a `Graph` property with the
canvas's shape (`WorkspaceViewModel.cs:296–309`). The document is
released when the last graph tab in the workspace closes (the mac's
`releaseGraphStateIfUnreferenced`, `AppState+GraphTable.swift:131–150`):
its workers shut down (`Shutdown`, the canvas's `:2018` precedent), its
token sequence advances so no in-flight result can land, its view state
clears (A-7), and the next open creates a fresh document — the
"refresh gate closes with the last graph tab" twin
(`testRefreshGateClosesWithLastGraphTab`). The placeholder retires for
Graph: `IsPlaceholder` (`WorkspaceViewModel.cs:323–324`) gains
`&& !IsGraph`, the template renders GraphSurfaceView for a graph tab,
and a fact asserts a graph tab's placeholder text is never shown.
Pinned by facts: one document across two opens and a restore; the
attach at each of the five sites; the release on last close and the
fresh document on reopen; the placeholder gone.

**A-2 — The load is 0b's design A on Windows: one worker body, two
queries, one token, one publish.** The document's `Load()` (an open, a
sort request, a vault-change probe that found a new generation) issues
a token — (session identity, the lifecycle generation, the request
record `{query: GraphVisibilityQuery, sort: GraphTableSort}`, `seq`
incremented) — and starts ONE `StartWork` body that calls
`GraphSnapshot(filter)` then `GraphTableRows(query, sort)` on the
session (the mac's `:189–200`). The body posts the result with its
token; the dispatcher-side receiver applies the mac's four-step rule
(`receiveGraphTableRows`, `:291–310`), verbatim in C#: (i) the token's
`seq` and request equal the document's current ones, else drop; (ii)
the held snapshot's filter equals the token's, else drop; (iii) the
rows' `generation` equals the held snapshot's, else drop and issue a
silent reload; (iv) publish rows, `total`, the accepted sort and the
snapshot in ONE synchronous assignment on the dispatcher, ROWS FIRST,
then the summary, then the load state. A failed query rolls the
requested sort back (`failGraphTableRows`, `:313–316`) and publishes
the error state; the snapshot fetched in the same body publishes with
the rows or not at all — the two are one epoch (design B). Whether to
SPEAK is decided after the fetch, on the dispatcher, from the tab's
liveness then (the mac's `:215`): a document whose tab closed during
the fetch publishes nothing and announces nothing. The session
identity in the token is the `VaultSession` reference the body was
started against; a vault close or reopen advances the lifecycle
generation and every result from the old session drops. The
first-load, sort and vault-change paths share this ONE receiver — a
fact drives each and asserts the same publish shape. Pinned by facts:
a stale `seq` drops; a request mismatch drops; a generation mismatch
drops and re-fetches, and the re-fetch publishes; rows-first order
observed through property-change notifications; a closed tab publishes
nothing; a failed fetch publishes the error and keeps the previous
rows (the canvas's "coherent past", `CanvasDocumentViewModel.cs:1414–1419`).

**A-3 — The generation probe on vault change, gated.** `HandleFileChange`
(`VaultLifecycleViewModel.cs:713–778`) gains one line beside
`NotifyBasesOfVaultChange` (`:753`): the workspace's
NotifyGraphOfVaultChange, which — when a graph document exists AND a
graph tab is live in the workspace — calls the document's probe. The
probe reads `GraphGeneration()` on the session; if it equals the held
snapshot's generation it does nothing; if it differs it issues a token
and reloads SILENTLY (the mac's `loadGraphTable(announce: .silent)`,
`:302`): no summary, no filter count, no status — the rows, the total
and the summary region update under the reader. The probe rides the
lifecycle generation the fan-out already checks (`:715`) and never runs
after the lifecycle advanced (`testRefreshProbeGatedOutWhenLifecycleAdvanced`);
a burst of events coalesces by the token — every probe issues a fresh
`seq`, so only the last fetch publishes. The probe is not debounced by
the sidebar's 150 ms ticket (`:767–776`); it is cheap (one `u64`) and
the token absorbs the burst. Pinned by facts: a save that changes the
graph reloads silently and the announcer records nothing; a save that
does not change the generation fetches nothing (the FFI call count
observed through the test session's counter); a closed tab probes
nothing; a lifecycle advance drops the probe.

**A-4 — Load states, with the mac's labels.** The surface has four
states, in this precedence: LOADING while the first snapshot is in
flight and none is held (the label "Loading graph…", accessible
"Loading graph.", `GraphTableView.swift:300–307`); ERROR when the last
load failed (the label "Graph error: {message}", `:309–316`, the
message core's `VaultError` text humanised as the shell does elsewhere;
the announcement `GraphBlocked{LoadFailed{message}}`, High); EMPTY when
the snapshot is held and the rows are empty (the label "No notes match
the current filters.", `:257`; the summary region still carries
`audio_summary` over zero counts — the empty vault's summary is 0a's
`GraphSnapshotSummary` rendering, and a preset's empty case is PR C's);
READY otherwise (the grid). A reload keeps the previous rows visible
under LOADING (the coherent past). The labels are view text, not
announcement templates (spec §5's copy rule); the announcements are the
family's. Pinned by facts for each state and each transition into it.

**A-5 — The table is core's columns, core's order, core's default
sort, with nothing typed in the shell.** GraphTableView configures an
`AccessibleDataGrid` with `GridAutomationId = "GraphTableGrid"` and
builds its `AccessibleGridColumn` list by iterating
`GraphTableColumns()` in order (`lib.rs:3922`; `table_columns`,
`graph_queries.rs:572–580`): `Header = spec.Header` (core's headers,
which are the mac's titles verbatim), `Cell = row =>
((GraphTableRow)row).Cells[index]`, `IsRowHeader = spec.Column ==
GraphTableColumn.Note`, `IsExternallySortable = true`, `Sort = null`,
`AccessibilityHint` = the ghost-creation admission reason for a ghost
row (the mac's `:465–477`). The grid's row objects are the
`GraphTableRow` records themselves — no wrapper: the record's `Cells`
vector is the projection (0b-7) and its `StableKey` the identity. The
sort is external: `ExternalSortHandler` (`:655–663`) maps the column
index through the SAME vector (index → `spec.Column`) to a
`GraphTableSort` and calls the document's SetGraphTableSort, which
issues a token (A-2) — the grid neither reorders nor announces; after
every publish the surface re-asserts `SetSortIndicator(accepted)`
(`:668–685`; the bases' `BaseSurfaceView.cs:728`). The default sort is
FETCHED: this PR adds the free function `graph_table_default_sort()`
returning `GraphTableSort::default()` (`graph_queries.rs:589–596`) to
core's surface list (twenty-four names; `GraphQuerySurfaceCensus`'s
floor rises) and its FFI mirror, and both hosts use it — the mac's
literal at `AppState+GraphTable.swift:135` becomes the call (AD-1). A
census reads every source under `Graph/` and asserts none of the nine
header strings appears as a literal. Pinned by facts: the grid's
headers equal core's vector in order; the row header column is Note;
each cell equals the record's cell; a sort request through the handler
issues a token whose sort maps back to the clicked column; the
indicator follows the ACCEPTED sort, not the requested one; the default
sort equals the fetched record.

**A-6 — The row's spoken form is core's row copy at the set
verbosity; the kind rides the item status.** `Bind`'s
`rowAudioDescription` returns, for a `GraphTableRow`, the rendered text
of `A11yEvent::Graph(GraphRow{verbosity, row})` with `GraphRowCopy`
built from the record — `label`, `kind`, `in_links` = `LinksIn`,
`out_links` = `LinksOut`, `references` = `LinksIn + EmbedsIn` for a
ghost (0a's rule), `embed = false` (a table row focuses no
relationship) — through `A11yRender`; the verbosity is Standard in
this PR (AD-6; PR C's menu and the config's `verbosity` key set it).
The rendering is 0a's corpus row, so the row's Name is P1's copy
byte-for-byte and no template is typed here. The kind badge: the
substrate gains an optional `rowItemStatus` parameter on `Bind` (a
`Func<object, string?>`), applied as `AutomationProperties.ItemStatus`
on each realized row in `OnLoadingRow` (`:203–209`); the graph passes
the Kind cell (`Cells[8]`, core's `kind_label`). The mac has no item
status; this is a recorded Windows-only enrichment (A-D1, AD-2) that
adds nothing to the Name. Pinned by facts: the row description equals
the corpus rendering for a note, an attachment and a ghost; the item
status equals the Kind cell; a Terse verbosity, when PR C sets it,
yields the label alone (the fact is written now against the document's
verbosity field).

**A-7 — Selection writes the shared key; the summary region is
`audio_summary` verbatim.** The grid's current row writes
`GraphViewState.SelectedKey` (the row's `StableKey`; the mac's
`graphSelectedNodeKey`, `AppState.swift:3108`); the view state is the
document's and survives a reload — after a publish the surface re-seats
the row whose key equals `SelectedKey`, or clears the key when the key
is not in the new rows (the mac's revalidation at the publish point,
`AppState+GraphTable.swift:364–368`). PR B's leaf and PR D's diagram
read and write the same field. `Bind`'s `summary` is the held
snapshot's `AudioSummary` verbatim and `accessibilityLabel` is "Graph,
data grid" (the mac's `:326–327`); the summary region's UIA name is
therefore `Summary: {audio_summary}` (`:524`). Pinned by facts:
selection round-trips through the key; a reload re-seats; a vanished
key clears; the summary region text equals the snapshot's summary.

**A-8 — Row actions are core's vector, wired to shell seams; runtime
admission is the host's.** For each row the surface builds
`AccessibleGridRowAction`s by iterating `GraphRowActions(kind)`
(`lib.rs:3794`; `row_actions`, `graph_queries.rs:810–821`) in order,
`Name = spec.Title`, so a note or attachment offers Open, Open in New
Tab, Show connections, Reveal in File Tree and a ghost offers Create
note — nothing typed, nothing reordered. Each action's `Execute` calls
a seam on the document the workspace wires at attach (the bases'
`OpenRowFromSurface`, `WorkspaceViewModel.Bases.cs:1338`): Open →
`OpenPath(path, WorkspaceOpenTarget.CurrentTab)`
(`WorkspaceViewModel.cs:1869`); Open in New Tab → `OpenPath(path,
NewTab)`; Reveal in File Tree → the files sidebar's new
SelectPathFromSurface(path), which expands the ancestors, selects the
node and focuses the tree (the mac's meaning; not the sidebar's
"Reveal in File Explorer", which is a different verb, FD-5); Create
note → the ghost's path from `GraphGhostNotePath` (0b-11), created
through `CreateOutcomes.CreateReporting` with empty content (the
sidebar's typed create, `FilesSidebarViewModel.cs:1240–1242`), then
opened in the current tab, announcing `GraphStatus{NoteCreated{name}}`
on success and `GraphBlocked{NoteCreateFailed{message}}` on failure;
Show connections → a seam PR B fills. Admission is host state (0bD-8):
`IsEnabled` is false and `DisabledReason` is the shell's reason when
the seam reports one — Create note under `StructuralMutationBusyReason`
when a structural gate exists (none today, `SlateCommandRegistrar.cs:184`)
— and Show connections is disabled without a reason until PR B wires
its seam (AD-3, AR-2). A disabled action is still LISTED: the vector is
core's. Pinned by facts: the names per kind equal the vector's titles
in order; each enabled action reaches its seam with the row's path or
ghost path; Create note creates at the ghost path and announces; a
failed create announces the High event; Show connections is present
and disabled, and enabling its seam enables it.

**A-9 — Activation is Open.** `Bind`'s `rowActivated` (Enter,
double-click; `:868, 1162`) is the Open action for the row's kind — a
note or attachment opens in the current tab, a ghost activates
nothing (Create note is a listed action, never an implicit activation;
the mac's `activate(_:)` gates on the path, `:357`). The open goes
through the workspace's `OpenPath`, and the announcement is the
workspace's for an opened file — the graph family posts no event of
its own on activation (the bases precedent, `BasesOpenRow`,
`WorkspaceViewModel.Bases.cs:473–481`, announces `OpenedFile`; the
graph's seam does the same through the same call). Pinned by facts:
activation of a note reaches `OpenPath` with `CurrentTab`; activation
of a ghost reaches nothing and the document records no event.

**A-10 — One relay, walled.** GraphAnnouncer is the Windows twin of
`GraphAnnouncer.swift` and the sibling of `CanvasAnnouncer`
(`Canvas/CanvasAnnouncer.cs:28`): `Announce(GraphA11yEvent)` classifies
by the pinned list (`a11y.rs:138–152`: `navigation` = `GraphRow`,
`filter` = `GraphFilterCount`, `forceValue` = `GraphForceValue`, `settle`
= `GraphLayoutSettled`; everything else immediate), coalesces each class
200 ms latest-wins with the canvas's timer shape, and a High event —
`GraphBlocked`, announced or relayed — flushes and DROPS all four
pending classes; `Relay(A11yEvent)` passes a non-graph event through
uncoalesced (the grid's own events: the sort, the focus, the copy). The
relay is the ONE file under `Graph/` that renders (`A11yRender`) and
posts (`AccessibilityNotificationDispatcher.Post`, the workspace's
seam); the grid's `Announce` is swapped to `Relay` at attach (the canvas
table's `:641, 648`) and is silent before it (`:49–58`). Two censuses
wall the directory, the canvas pair keyed on `Graph/`:
`AnnouncementSeamCensus` gains NoGraphCodeReachesTheAnnouncerExceptThroughTheBoundary
(sources `Graph/*.cs`, the forbidden surface read from the relay's own
members that reach its emit, named seams each hit — `:353, 526–534,
518–524`), and a GraphAnnouncerCensus with
NoGraphSourceAnnouncesOutsideTheRelay, EveryGridUnderGraphRidesTheRelay,
TheRelayIsTheOneFileThatRenders (`CanvasAnnouncerCensus.cs:38, 111, 207`).
The class list is pinned from the Rust side too: the slate-uniffi test
`the_windows_graph_coalescing_switch_matches_the_pinned_class_list`
reads `Graph/GraphAnnouncer.cs`'s switch and compares membership per
class both ways with the list core pins, as the mac's
`the_mac_graph_coalescing_switch_matches_the_pinned_class_list`
(`lib.rs:12864–12914`) and the Windows canvas twin (`:12916–12932`)
do. The open sequence is the mac's order: `GraphStatus{Opened}` at the
open (`AppState+GraphTable.swift:72` — after activation, before the
load; a second open of the live tab announces `GraphStatus{AlreadyOpen}`
through `TryFocusGlobalGraph`'s success arm, the mac's `activateTab`
path), then `GraphSnapshotSummary{counts}` when the first load
publishes with the tab live (`:245–248`); neither is coalesced. Pinned
by facts: the class membership (both hosts' switch-list tests); the
window, latest-wins and the High flush in a Windows GraphAnnouncerTests
twin of the canvas's; the open sequence on a fresh open, on a re-open
and on a load failure (`Opened` then `GraphBlocked`, and the summary
never); every announcement under `Graph/` through the relay (the
censuses).

**A-11 — The header: the mode switcher is core's vector; the Diagram
waits for PR D.** GraphSurfaceView's header carries the mode switcher
built from `GraphSurfaceModes()` (`lib.rs:4195`; `surface_modes()`,
`graph_config.rs`) in order — Table, Diagram — with each item's `Title`
as its label and `Tag` as its automation id, and the summary region
below the grid. In this PR only Table is selectable: the Diagram item
is present and disabled (admission again), no transition can occur, so
no `GraphMode` event fires here; PR D wires the switch and its event.
The header's own name is the tab's kind label ("Graph",
`WorkspaceViewModel.cs:330`). Pinned by facts: the items equal the
vector's titles in order; Diagram is disabled; Table is checked.

**A-12 — The command row, the scope, the matrix, the evidence.**
`ChordTable` gains `Ids.GraphOpenTab` = `slate.graph.openTab`, a
chordless row in `CommandSection.Graph` with the label "Graph: Open
Graph" and a hint, on the `Reg(Ids.CanvasShowOutline, …)` template
(`ChordTable.cs:923–924`); `Reg` gives it `ChordScope.None` (`:578`),
which is the correct scope for a chordless row — the spec's
"`ChordScope.Graph`" is DECLARED by this PR (the enum member after
`Canvas`, `:69`) and first used by PR C's chorded rows, and the spec
text is amended to say so (0bD-12). The command registers in the
palette through the registrar and calls `OpenGraph()`;
`chords.json` is regenerated under SLATE_CHORDS_UPDATE
(`ChordTableTests.cs:31`) and its `deliveryEvidence.commands` gains
`slate.graph.openTab` with the implementation and test members
(`chords.json:2148`'s shape; `generate-parity-matrix.py:802–809`
validates). `w_c_matrix.md` gains the row "Graph table (W6-2 PR A)" on
the canvas table row's shape (`:42`) with the three screen-reader
columns Pending, pinned by a WcMatrixGraphEvidenceCensus twin of
`WcMatrixCanvasEvidenceCensus`. The matrix's command row for
`slate.graph.openTab` is ✓; the surface row stays pending until PR F.

**A-13 — Persistence and the singleton twins.** The singleton restore
collapse stands as tested (`W1WorkspaceRedTeamTests.cs:336–378`) and
now attaches the ONE document to the surviving tab. The mac routing
suite's PR-A cases gain Windows twins in `W1WorkspaceRedTeamTests`:
GraphTabLoadsSnapshot (the document publishes over a real session),
GraphTabNotAddressableByPath (`OpenPath("graph:singleton")` opens no
tab), RefreshProbeGatedOutWhenLifecycleAdvanced,
RefreshGateClosesWithLastGraphTab; the existing dedup, duplicate-and-
split refusal and restore tests stand. Windows has no "park the dirty
outgoing note" case (`testOpenGraphTabParksDirtyOutgoingNote`): the
shell's editors are per tab and nothing is parked — recorded as
A-D3, not twinned.

**A-14 — §W-A: the document's rows equal the artifact's, cell by
cell.** GraphDocumentTests opens the graph vault
(`crates/slate-core/tests/fixtures/graph_vault/`, 0b-13) through a real
`VaultSession` and the document in synchronous mode, and for each of
the sixteen `table` entries of `parity_golden/graph_queries.json` sets
the entry's sort through the external-sort seam, drains, and asserts
the published rows' keys and cells equal the entry's rows in order —
cells 0 to 5, 7 and 8 (the artifact excludes the Modified cell,
0bD-5; the Windows fact excludes exactly that index, by the column
vector's `Modified` position, not a literal 6) and `total` equals the
entry's. The summary is pinned separately: the summary region's text
equals `AudioSummary`, and `AudioSummary` is 0a's corpus rendering
over the snapshot's counts (`corpus_renders_the_shipped_strings`). The
artifact is read, never regenerated, by this test; regeneration stays
the harness's (`--graph-fixtures`).

**A-15 — §K: snapshot marshalling through the binding, asserted.**
The benchmarks project gains GraphOpenBenchmarks beside
`CanvasOpenBenchmarks` (the class shape `CanvasRendererBenchmarks.cs:17–21`)
and a `--graph` runner arm on the `--canvas` pattern
(`Program.cs:12–17`): over synthetic linked vaults at 1k and 10k notes
(core's `generate_linked_vault` shape, built into a temp vault and
scanned), the workloads are GraphSnapshotDefaultFilter, GraphTableRowsDefaultSort
and `OpenToPublish` (the document's open through to the first publish
in synchronous mode). P set no host-side marshalling budget (locked
decision 10 budgets the layout and the backend, `00_program.md:24`); the
budgets asserted are the canvas's first-derivation precedent — 500 ms
median for `OpenToPublish` at 10k, 100 ms at 1k — recorded as AD-7, a
miss is a finding for the task loop, not a waiver. The medians land in
`BENCHMARKS.md` under "Milestone W6-2 — graph through the C# binding"
(spec §4 PR F item 2 opens that section; this PR writes its first
rows).

**A-16 — §W-C: the journey, the axe scan, CI's shell gate.** The FlaUI
journey GraphSurfaces_TableSortSelectionAndActivation_AreClean opens
the graph tab through the command, waits for the grid
(`GraphTableGrid`), walks the rows by UIA and asserts each row's Name
is the P1 copy and its ItemStatus the kind, reads the summary region's
Name, sorts by a header and asserts the indicator and the first row
changed as the artifact says, activates a row and asserts the note
opened, and runs axe with the scan id `graph-table` (the second id
after `canvas-table`). The gate is CI's shell accessibility lane; a
local run is a serialized smoke with no screen reader running (the
standing note). The journey's evidence is the matrix row's (A-12).

**A-17 — Tripwires and censuses this PR adds or extends.** The two
directory censuses (A-10); the no-typed-header census (A-5); the
switch-list twin (A-10); `TheAttachFunnelDocCommentNamesEveryCallSite`
unchanged and green with the graph arm; a placeholder census: no
`WorkspaceItemKind` that has a surface is a placeholder (Graph joins
canvas); `GraphQuerySurfaceCensus`'s floor at twenty-four (A-5);
`GraphContractsCitationCensus` gains the PR A tuple, its floor one
below the population; `ChordTableTests` sees the row and the JSON;
WcMatrixGraphEvidenceCensus (A-12); the announcement-seam census's
named-seam list gains the graph's seams so an unused exemption fails.

### Decisions

- **AD-1 — The default sort is fetched.** `graph_table_default_sort()`
  joins core's surface (twenty-four names) and its FFI, and the mac's
  literal becomes the call: an inventory of one is still an inventory
  (0bD-13), and the census that proves "nothing typed" cannot exempt
  one value.
- **AD-2 — The kind badge rides `ItemStatus` through a substrate
  seam.** `Bind` gains an optional `rowItemStatus`; the substrate is
  the shell's to extend, the seam is generic, and the graph is its
  first user (A-6). Windows-only (A-D1).
- **AD-3 — Show connections is listed and disabled until PR B.** The
  vector is core's and admission is the host's (0bD-8); a disabled
  action with no reason is the honest state between the two merges
  (AR-2).
- **AD-4 — The document is a `PanelWorkScheduler` and the row is the
  record.** One worker body per epoch through `StartWork`, publishes
  through `Post`, the synchronous test mode for the facts; the grid
  binds `GraphTableRow` records directly — the cells vector is the
  projection and a wrapper would be a second one.
- **AD-5 — The probe is a workspace notification, not a watcher of its
  own.** `HandleFileChange` already fans out to bases and history; the
  graph is one more line there (A-3), gated on a live tab, and the
  token absorbs bursts — no debounce, no second generation counter.
- **AD-6 — Verbosity is Standard in this PR.** The document carries the
  field and the row description reads it; PR C's menu and the config
  key (0bD-7) set it. The Terse fact is written now.
- **AD-7 — The §K budgets are the canvas precedent's numbers.** P
  budgeted the backend and the layout, not the host's marshalling of
  the snapshot; 500 ms / 100 ms are asserted so a regression fails a
  run, and the task loop records the measured medians beside them.
- **AD-8 — Create note creates and opens.** The ghost's path is core's
  (0b-11), the create is the sidebar's typed outcome, the open is the
  workspace's; the announcement is the family's (`NoteCreated`,
  `NoteCreateFailed`).
- **AD-9 — The spec's stale citations are corrected here, not
  repeated** (0bD-12): `WorkspaceViewModel.cs:289–300` → `:296–309`;
  `AppState+GraphTable.swift:195–199` → `:235–243`; "`ChordScope.Graph`"
  for the chordless row → declared here, used by PR C; the "flat
  snapshot if 0b is still in review" contingency → moot, 0b merged
  first.

### Recorded divergences (owner-recorded; off-limits for re-litigation)

- **A-D1 — The kind badge on `ItemStatus`** is Windows-only (A-6); the
  mac carries the kind in the Kind cell alone. The Name is identical on
  both.
- **A-D2 — The activation announcement is the workspace's** (`OpenedFile`
  through `OpenPath`), the bases precedent; the mac's table activation
  announces through its own open path. The graph family posts nothing
  on activation on either host.
- **A-D3 — No outgoing note is parked** on opening the graph tab
  (`testOpenGraphTabParksDirtyOutgoingNote`): the Windows shell's
  editors are per tab; nothing is displaced, so nothing is parked.
- **A-D4 — "Reveal in File Tree" selects the note in the files
  sidebar** (the mac's meaning) through a new sidebar seam; the
  sidebar's own "Reveal in File Explorer" (FD-5) is a different verb
  and stays where it is.

### Mac details recorded while reading (not this issue's to fix)

- `openGraphTab` announces `GraphStatus{Opened}` unconditionally
  (`AppState+GraphTable.swift:72`) — also when the tab was already
  active and `activateGraphTab` returned at its same-tab guard
  (`:81–83`); `AlreadyOpen` exists in the vocabulary (`a11y.rs:3087`)
  and is posted elsewhere. Windows posts `AlreadyOpen` on the second
  open of the live tab (A-10); recorded, not a divergence — a parity
  question for the mac.
- The mac's activation (`GraphTableView.swift:343–357`) opens through
  `AppState.OpenTarget`; whether it announces is the workspace's
  business there too (A-D2 records the Windows side).

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
  consumption of `graph_table_default_sort`; the lane arbitrates.
- **AR-5 — The sidebar's select-path seam is new** and its behaviour
  (expand, select, focus) is pinned by a fact, not by the journey; the
  journey asserts the action is listed and reaches the seam.

### Tests that pin PR A

- Windows facts: GraphDocumentTests (A-1, A-2, A-3, A-4, A-7, A-8, A-9,
  A-14), GraphTableTests (A-5, A-6, A-11), GraphAnnouncerTests (A-10);
  `W1WorkspaceRedTeamTests` twins (A-13); `ChordTableTests` (A-12);
  the censuses of A-17; the FlaUI journey (A-16); GraphOpenBenchmarks
  (A-15).
- Rust: `graph_table_default_sort` is the `Default`; the surface list
  is twenty-four; `the_windows_graph_coalescing_switch_matches_the_pinned_class_list`.
- Mac: the suites green with the default-sort literal replaced by the
  call.

<!-- end of the graph contracts document -->
