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
graph. **Copy rule: shipped mac strings move verbatim** — P1's
"VoiceOver copy (normative)" and P2-3's copy are the reference; this PR
does not redesign wording.

**What stands today.** `GraphAnnouncer.swift` (239 lines): a
`GraphVerbosity` enum mirroring the canvas's, a `GraphRowRef` carrying a
node's label and counts, a five-case `GraphEvent` (`rowFocused`,
`summary(String)`, `reRooted(label)`, `status(String)`,
`error(String)`) of which three are free-text passthroughs, three
class-keyed debounced posters outside the event type
(`announceFilterCount`, `announceForceValue`, `announceSettle`), one
grammar (`rowPhrase`, `:158–170`) and one template (`"Connections:
{label}"`, `:179`), all posting through the one `// W0.5-3 residue:`
site (`:98–100`). Twenty-six posting sites compose the prose:
`AppState.swift:3236`; `AppState+Connections.swift:114, 123, 168, 212,
236, 287, 294`; `AppState+GraphConfig.swift:131` (over
`forcesChangePhrase` `:139–150`); `AppState+GraphDiagram.swift:177, 231,
235, 245` (over `graphDiagramWhereAmIText` `:252–267` and
`graphDiagramFilterPhrase` `:359–365`), `:346, 355`;
`AppState+GraphTable.swift:57, 197` (over `graphPresetAnnouncement`
`:336–354`), `:202, 204` (over `graphFilterCountText` `:225–235`),
`:215`; `GraphDiagramView.swift:496–498`; `GraphTableView.swift:100,
103, 340–342, 361`. Three label-class sites render the row copy
without posting (`ConnectionsPanel.swift:239`,
`GraphDiagramView.swift:843`, `AppState+GraphDiagram.swift:256`). Core
carries two workspace-level graph events (`GraphOpensSinglePane`,
`ReopenedGraph`) and zero graph-announcer entries in the corpus; the
two `audio_summary` strings are composed in `session.rs`
(`snapshot_audio_summary` `:9017`; the neighbourhood string inline at
`:1990–1997`), outside the vocabulary.

### Contracts

**0a-1 — Full coverage, no free text.** Every shipped graph
announcement string is a typed variant or a typed ARM of one. Mac's
three free-text cases (`summary`, `status`, `error`) and the three
text-taking posters dissolve. `String` payload is permitted only for
dynamic data — labels, file names, OS error detail, the name-filter
needle, counts — **never for a whole sentence**. The vocabulary is
enumerated in full below (seventeen variants over seven closed nested
enums); a string with no home in the table is a round finding, not an
implementation choice.

**0a-1b — One engine, one top-level variant.** The family is reached
through `A11yEvent::Graph { event: GraphA11yEvent }`, the nested
family-per-engine pattern 0a-1b of the canvas document made the rule
(`a11y.rs:1246–1255`); `GraphA11yEvent` owns its variants and its own
`priority()` / `render()`, which `A11yEvent` delegates to. `A11yEvent`
stands at 199 top-level variants; this PR adds one. Variant NAMES keep
their `Graph` prefix so every citation stays literally true. Pinned by
`the_graph_family_occupies_one_top_level_variant`.

**0a-2 — The five-place rule.** A graph string is pinned in five
hand-maintained places, and all five move together: the variant and
its `render()` arm (`a11y.rs`), the in-file golden table
(`corpus_renders_the_shipped_strings`), the regenerated artifact
`tests/fixtures/a11y/corpus.json` (through the artifact test's own
regeneration path, never by hand), the FFI mirror
(`crates/slate-uniffi/src/lib.rs`), and **both** census mirrors
(`A11yCorpusCensusTests.swift`, `A11yCorpusCensus.cs`). Corpus order is
positional, so the graph family is **appended** after the canvas
family's last entry; every pre-existing index is untouched. The
artifact grows from 448 entries to 448 + the graph witnesses, and both
totals are stated in the record with the count read from the artifact.

**0a-3 — The tripwires already exist and cover the graph without
change.** `the_ffi_mirror_covers_every_core_a11y_variant` reads the
nested-family inventory from core's source (PR E13, IE13-14); the
graph's nested enums join CANVAS_NESTED_ENUMS's twin,
GRAPH_NESTED_ENUMS, and the inventory test that pins the family count
asserts the graph's; `the_mac_corpus_mirror_lists_every_event_in_order`
and its Windows twin parse the census files by name and order; the two
coalescing switch-list tests read core's ONE class list. Each of these
fails on a forgotten mirror in `cargo test`, before any host builds.

**0a-4 — Completeness is asserted, not assumed.**
`every_graph_variant_and_arm_is_represented_in_the_corpus` parses this
module's `pub enum` declarations and fails when a graph variant — or a
closed-set ARM of any of the graph's nested enums — never reaches
`corpus()`; its companion asserts the coverage table lists exactly the
`Graph*` parameter enums the module declares. The exact-count pins the
E13 slice introduced for four canvas families are extended to the
graph's nested enums as they land.

**0a-5 — Verbosity is a parameter on exactly one family.**
`GraphVerbosity { Terse, Standard, Verbose }` is core (a separate enum
from `CanvasVerbosity`, because P persists it separately — spec D-6);
it is carried by `GraphRow` and by nothing else: P1's row copy is the
one graph template that varies (terse collapses to the label). Core
stays pure — no module state, no "current verbosity"; each host owns
the persisted preference (in `.slate/graph.json` through 0b's config
API) and passes it per event. `graph_verbosity_matrix_pins_every_level`
pins the six (level × note/ghost) renderings and asserts structurally
that no other variant carries the parameter.

**0a-6 — Where-am-I is always verbose-grade, and that is a recorded
divergence from mac.** `GraphWhereAmI` takes **no** verbosity
parameter; its row clause renders the full row copy at every level.
Mac composes it from `rowPhrase`, which collapses to the bare label at
terse (`AppState+GraphDiagram.swift:256`), so a terse reader asking
"Where am I?" hears less than P2-3 promises ("node copy + component +
zoom + active filters"). Core follows the promise and the t0 §1.4 rule
the canvas already keeps (0a-D1; the mac detail is filed at close-out).

**0a-7 — The two summaries are typed events rendered by the ONE format
core already owns.** `GraphSnapshotSummary { notes, links, orphans,
unresolved, filtered }` and `GraphNeighborhoodSummary { label, in_links,
out_links, shown, depth }` render through the same functions that
produce `GraphSnapshot.audio_summary` and `GraphNeighborhood.audio_summary`
— `snapshot_audio_summary` and the neighbourhood formatter move from
`session.rs` into a shared core module that both `session.rs` and
`a11y.rs` call, so P0-3's format lives in one place. Pinned by
`summary_events_render_the_snapshot_fields_verbatim`: for a built
fixture, rendering the event constructed from the snapshot's counts
equals the snapshot's `audio_summary` byte for byte, and likewise for a
neighbourhood. The `audio_summary` fields stay on the FFI records for
the summary regions; hosts ANNOUNCE the typed event (mac's `.summary`
sites, `AppState+Connections.swift:114`, `AppState+GraphTable.swift:202`).

**0a-8 — Priorities are mac's `.error` case, listed explicitly.**
`GraphBlocked` is `High`; every other graph event is `Medium`.
`priority()` ends in a catch-all `_ => Medium`, so the High member is
named in the explicit arm and pinned both ways by
`graph_priorities_pin_the_error_tier`.

**0a-9 — Coalescing stays host-side; the class keys do not.** Timing is
the hosts' (a pure render has no clock), but the classes are pinned in
`a11y.rs`'s "Coalescing class keys" list — the ONE list the two
switch-list tests read — with the graph's four: **`navigation`** =
`GraphRow`; **`filter`** = GraphFilterCount; **`forceValue`** =
GraphForceValue; **`settle`** = GraphLayoutSettled; everything else
posts immediately; a `High` graph event flushes **and drops** every
pending class. 200 ms latest-wins, each class independent
(`GraphAnnouncer.swift:74–84, 185–218`). Two host timing rules that are
NOT classes are recorded here so both hosts copy them: the filter
count's **fire-time gate** (a queued count is dropped if the graph is no
longer the focused surface when the debounce fires, `:123–133,
204–207`) and `cancelPending` on view departure or vault change
(`:224–226`).

**0a-10 — No chord-bearing template.** Unlike the canvas's three, no
graph template carries a host chord: the tier-B copy names "Table mode"
by its label, not its chord. The chord-parameter convention (canvas
0a-9) is therefore not exercised by this family; a host that needs one
later adds the parameter, never the chord text.

**0a-11 — The row copy is one helper, spoken and labelled alike.** P1's
row template — `"{label}, {in} links in, {out} links out"`, ghosts
`"{label}, unresolved, {references} references"`, `", embed"` appended,
terse → the label, counts substituted verbatim with the plurals as the
spec writes them (`GraphAnnouncerTests.swift:34–38`: "1 references" is
the shipped copy, migrated under the copy rule and allow-listed as a
(arm, string) pair like the canvas's CR-3 defects) — is a single core
helper (`a11y.rs::graph_row_copy`) used by `GraphRow`'s render and by
`GraphWhereAmI`'s row clause. The three mac label sites
(`ConnectionsPanel.swift:239`, `GraphDiagramView.swift:843`,
`AppState+GraphDiagram.swift:256`) render `GraphRow` through
`a11yRender`; `rowPhrase` is deleted.

**0a-12 — The one already-canonical site becomes a relay that keeps the
priority.** The grid's own sort and filter events reach mac's announcer
as `.status(a11yRender(event).text)` (`GraphTableView.swift:361`),
which drops core's priority — the same defect the canvas's 0a-2 fixed.
`GraphAnnouncer.relay(_ event: A11yEvent)` carries the render's text AND
priority; the Windows relay does the same from day one (PR A).

**0a-13 — Label-grade events are marked as such.** Two events are LABEL
class, never spoken: `GraphTierSummary { count }` (the tier-B summary
element's name) and `GraphNeighborsContent { labels, more }` (the
"Connects to" custom content of a node peer). They live here because
both hosts must compose them identically from core data. Every other
graph label — the grid's `"Graph, data grid"` name, the column titles,
the kind labels Note / Attachment / Unresolved, the badges Unresolved /
Embed / Attachment, `"Local graph depth"` and its hint, the loading, no-
connections and error labels of the Connections panel, the custom
action name `"Switch to Table"` — stays §W-C label class, recorded in
the label inventory PR A/B carry, and is NOT in this vocabulary.

**0a-14 — Every render arm is TOTAL over its payload domain.** An arm
may not assume a cardinality its payload admits: counts render at every
value; `GraphNeighborsContent { labels: [] }` renders an empty content
value (the host omits the content); `GraphPreset { MostLinked, top:
None }` renders `"No notes to rank."`; the Where-am-I row clause with no
selection renders `"No node selected"`. Exception, by design, the
shipped plural defects of 0a-11, allow-listed as pairs.

**0a-15 — Mac consumes in the same PR (Task 0a-2), and the census
counts it.** `GraphAnnouncer.phrase`, `rowPhrase`, `forcesChangePhrase`,
`graphPresetAnnouncement`, `graphFilterCountText`'s string,
`graphDiagramWhereAmIText`, `graphDiagramFilterPhrase` and the status
literals are replaced by event construction + `a11yRender`; the
announcer's `post` closure posts rendered events through the canonical
vocabulary, so its `// W0.5-3 residue:` marker goes and
`A11yResidueCensusTests.pinnedResidueSites` reads **28**;
`testNoDirectAnnouncementsUnderGraph` keeps guarding; the copy
assertions of GraphAnnouncerTests (`:25–51`) and
`GraphCommandsTests.testPresetAnnouncementStringsVerbatim` and
`GraphConfigTests.testForcesChangePhraseNamesOneControl` move to the
Rust golden and the Swift tests keep coalescing, flush, the fire-time
gate, `cancelPending` and class independence. The Swift edit is unrun on
this box (0aR-1).

### The event enumeration (the PR's contract)

Seventeen variants of `GraphA11yEvent`, all reached through
`A11yEvent::Graph { event }` (0a-1b). `V` = the `GraphVerbosity`
parameter. Class: `nav` / `filter` / `force` / `settle` / `—`
(immediate). All priorities `Medium` unless marked **High**.

| Event | Payload | Template(s) | Pri | Class | mac site(s) replaced |
|---|---|---|---|---|---|
| `GraphRow` | `V, label, kind (Note‖Attachment‖Ghost), in_links, out_links, references, embed` | terse `⟨label⟩` · note/attachment `⟨label⟩, ⟨in⟩ links in, ⟨out⟩ links out` · ghost `⟨label⟩, unresolved, ⟨references⟩ references` · `+ ", embed"` | | nav | `GraphAnnouncer.swift:158–170` (spoken at `AppState+GraphDiagram.swift:197`; labelled at `ConnectionsPanel.swift:239`, `GraphDiagramView.swift:843`) |
| `GraphReRooted` | `label` | `Connections: ⟨label⟩` | | — | `GraphAnnouncer.swift:179`; `AppState+Connections.swift:212, 236` |
| GraphSnapshotSummary | `notes, links, orphans, unresolved, filtered` | P0-3: `⟨n⟩ notes, ⟨e⟩ links.[ ⟨o⟩ orphans, ⟨g⟩ unresolved targets.][ Filtered.]` (grouped decimals) | | — | `AppState+GraphTable.swift:202` (over `session.rs:9017`) |
| GraphNeighborhoodSummary | `label, in_links, out_links, shown, depth` | P0-3: `⟨label⟩: ⟨in⟩ links in, ⟨out⟩ links out. Showing ⟨k⟩ notes within ⟨d⟩ links.` | | — | `AppState+Connections.swift:114` (over `session.rs:1990–1997`) |
| `GraphPreset` | `preset (Orphans‖Unresolved‖MostLinked), count, top: Option<(label, in_links)>` | `⟨k⟩ orphaned notes.` ‖ `⟨k⟩ unresolved targets.` ‖ `Most linked: ⟨label⟩, ⟨n⟩ links in.` ‖ `No notes to rank.` | | — | `AppState+GraphTable.swift:197, 336–354` |
| GraphFilterCount | `shown, total` | `⟨shown⟩ of ⟨total⟩ shown` | | filter | `AppState+GraphTable.swift:204, 225–235`; `GraphTableView.swift:340–342` (two sites, one event) |
| GraphForceValue | `control (Center‖Repel‖Link‖LinkDistance), percent` | `Center force ⟨p⟩ percent` ‖ `Repel force ⟨p⟩ percent` ‖ `Link force ⟨p⟩ percent` ‖ `Link distance ⟨p⟩ percent` | | force | `AppState+GraphConfig.swift:131, 139–150` |
| GraphLayoutSettled | — | `Graph layout settled.` | | settle | `AppState+GraphDiagram.swift:177` |
| `GraphPinned` | `pinned` | `Pinned.` ‖ `Unpinned.` | | — | `AppState+GraphDiagram.swift:231, 235` |
| `GraphZoom` | `fit, percent` | `Zoom ⟨p⟩ percent.` ‖ `Fit graph. Zoom ⟨p⟩ percent.` | | — | `AppState+GraphDiagram.swift:346, 355` |
| `GraphMode` | `mode (Table‖Diagram)` | `Table mode.` ‖ `Diagram mode.` | | — | `GraphTableView.swift:100, 103` |
| `GraphWhereAmI` | `row: Option<GraphRow payload minus V>, component: Option<u32>, zoom_percent, orphans_only, attachments_shown, ghosts_shown, name_filter: Option<String>, unresolved_only` | `⟨row copy⟩, component ⟨c⟩` ‖ `No node selected` · `, zoom ⟨p⟩ percent` · `, filters: [orphans only, ][attachments shown, ]unresolved shown‖hidden` · `[, name filter “⟨q⟩”]` · `[, unresolved only]` · `.` — always verbose-grade (0a-6) | | — | `AppState+GraphDiagram.swift:245, 252–267, 359–365` |
| GraphTierEntered | — | `Large graph: summary accessibility mode. Table mode has every node.` | | — | `GraphDiagramView.swift:496–498` |
| GraphTierSummary | `count` | `⟨n⟩ nodes — too many for per-node navigation. Switch to Table mode for the full, navigable list.` — LABEL | | — | `GraphDiagramView.swift:627–629` |
| GraphNeighborsContent | `labels[], more` | `⟨a⟩, ⟨b⟩, …[ and ⟨k⟩ more]` — LABEL (content title `Connects to`) | | — | `GraphDiagramView.swift:851–869` |
| `GraphStatus` | `note` (6 arms) | see below | | — | see below |
| `GraphBlocked` | `reason` (3 arms) | see below | **High** | — | see below |

**GraphStatusNote (6 arms, all Medium).** `Opened` (`Graph.` —
`AppState+GraphTable.swift:57`) · `AlreadyOpen` (`The graph is already
open.` — `AppState.swift:3236`) · ConnectionsPanel (`Connections
panel.` — `AppState+Connections.swift:168`) · `NoteCreated { name }`
(`Created note ⟨name⟩.` — `:287`) · `NoConnections` (`This note has no
connections.` — the panel's empty state, `ConnectionsPanel.swift:125,
130`, spoken when the leaf is focused empty) · LoadingConnections
(`Loading connections.` — `:137, 144`).

**GraphBlockedReason (3 arms, all High).** `LoadFailed { message }`
(`Couldn't load the graph: ⟨m⟩` — `AppState+GraphTable.swift:215`) ·
`ConnectionsLoadFailed { message }` (`Couldn't load connections: ⟨m⟩` —
`AppState+Connections.swift:123`; the panel's error label
`Connections error: ⟨m⟩` at `ConnectionsPanel.swift:121` is label
class) · `NoteCreateFailed { message }` (`Couldn't create note: ⟨m⟩` —
`:294`).

The seventeen variants, in table order: `GraphRow`, `GraphReRooted`,
GraphSnapshotSummary, GraphNeighborhoodSummary, `GraphPreset`,
GraphFilterCount, GraphForceValue, GraphLayoutSettled,
`GraphPinned`, `GraphZoom`, `GraphMode`, `GraphWhereAmI`,
GraphTierEntered, GraphTierSummary, GraphNeighborsContent,
`GraphStatus`, `GraphBlocked`. The seven nested enums:
`GraphVerbosity`, `GraphRowKind`, GraphPresetKind,
GraphForceControl, `GraphTabMode`, GraphStatusNote,
GraphBlockedReason. The table is the authority for the round; a
variant the implementation adds or drops is a revision of this section,
not a footnote.

### Decisions

- **0aD-1 — The summaries are typed, and their format has one home.**
  P0-3's two formats move from `session.rs` to a shared core module
  called by the session's `audio_summary` producers and by the a11y
  render (0a-7). The alternative — relaying the snapshot's string as a
  `HostComposed`-shaped `GraphSummary { text }` — would put a whole
  sentence in a `String` payload, which 0a-1 forbids, and would leave
  the corpus unable to witness the format.
- **0aD-2 — `GraphVerbosity` is its own enum.** Not an alias of
  `CanvasVerbosity`: P persists the graph's level in the vault file and
  the canvas's on the device; one type would invite one preference.
- **0aD-3 — The filter count's fire-time gate and `cancelPending` are
  recorded as host rules, not events.** They govern WHETHER a queued
  count posts, which a pure render cannot decide; both hosts implement
  them and the contracts name them (0a-9).

### Recorded divergences (owner-recorded; off-limits for re-litigation)

- **0a-D1 — Where-am-I speaks the full row copy at every verbosity.**
  Mac collapses it at terse through `rowPhrase`; core renders the
  promise (P2-3, t0 §1.4). A mac detail, filed at close-out.
- **0a-D2 — The grid relay keeps core's priority.** Mac's
  `GraphTableView.swift:361` re-wraps a rendered event as a Medium
  status; both hosts relay text and priority.
- **0a-D3 — The Connections panel's empty and loading states are
  spoken through the vocabulary when the leaf takes focus empty.** Mac
  labels them only; Windows announces `GraphStatus` for the same
  states (the W4-7 UIA-delivery lesson: a label on a plain container is
  not heard). Mac's behaviour is unchanged by 0a; PR B pins Windows's.

### Accepted risks

- **0aR-1 — The Swift migration is unrun on this box.** Mac's CI lane
  arbitrates; a red mac lane is fixed here, not waived (HR-1's shape).
- **0aR-2 — The uniffi budget.** `A11yEvent` moves to 200 of 256
  top-level variants; the nesting rule (0a-1b) keeps every later engine
  at one.

### Tests that pin PR 0a

`crates/slate-core/src/a11y.rs`: `corpus_renders_the_shipped_strings`
(the graph golden rows), `committed_corpus_artifact_matches_the_vocabulary`,
`every_graph_variant_and_arm_is_represented_in_the_corpus` and its
inventory companion, `the_graph_family_occupies_one_top_level_variant`,
`graph_verbosity_matrix_pins_every_level`,
`summary_events_render_the_snapshot_fields_verbatim`,
`graph_priorities_pin_the_error_tier`, the plural-defect pair test.
`crates/slate-uniffi/src/lib.rs`: `the_ffi_mirror_covers_every_core_a11y_variant`
(over the graph's nested families), the two corpus-order tests, the two
coalescing switch-list tests. `apps/slate-windows/.../A11yCorpusCensus.cs`:
`EveryCorpusEventRendersTheCommittedIdentityTextAndPriority` over the
graph entries. `apps/slate-mac/Tests/SlateMacTests/`:
A11yCorpusCensusTests (the graph entries through the real FFI),
GraphAnnouncerTests (coalescing, flush, the gate, `cancelPending`,
class independence, the widened funnel guard), `A11yResidueCensusTests`
(`pinnedResidueSites` 29 → **28**).

<!-- end of the graph contracts document -->
