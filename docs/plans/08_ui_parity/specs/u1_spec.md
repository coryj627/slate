# U1 executable spec — Workspace shell: tabs + split panes

Issues: #453 (U1-1) · #454 (U1-2) · #455 (U1-3) · #456 (U1-4) · #457 (U1-5) · #458 (U1-6).
Milestone: GH 24. Every issue also satisfies the program DoD
(`00_program.md` §A–§G); this spec only adds what is U1-specific. One PR per issue.

**Execution order: U1-1 → U1-4 → U1-2 → U1-3 → U1-5 → U1-6** (differs from issue
numbering; see gap_analysis.md G4 — migration lands before tab UI so every PR keeps the
suite green).

---

## Shared architecture (read first)

### New types

```
apps/slate-mac/Sources/SlateMac/Workspace/WorkspaceModel.swift   (U1-1, pure — no SwiftUI import)
apps/slate-mac/Sources/SlateMac/Workspace/NoteDocument.swift     (U1-2)
apps/slate-mac/Sources/SlateMac/Workspace/WorkspaceState.swift   (U1-2)
apps/slate-mac/Sources/SlateMac/Workspace/WorkspaceView.swift    (U1-4)
apps/slate-mac/Sources/SlateMac/Workspace/TabBarView.swift       (U1-2)
apps/slate-mac/Sources/SlateMac/Workspace/SplitContainerView.swift (U1-3)
apps/slate-mac/Sources/SlateMac/Workspace/WorkspaceStore.swift   (U1-6)
```

`WorkspaceModel` is a **value type** (struct tree) so census tests can snapshot/compare
cheaply. `WorkspaceState: ObservableObject` wraps it (`@Published private(set) var
model`), owns the `[TabID: NoteDocument]` registry, and is owned by `AppState`
(`appState.workspace`). Mutations go through `WorkspaceState` methods only — never mutate
`model` from a view.

```swift
enum EditorItem: Hashable, Codable {
    case markdown(path: String)
    // Reserved tab kinds — Milestones N/T/P add renderers, not shell:
    // case base(path: String), canvas(path: String), graph
    // (cases stay commented until their milestone lands: an inhabited case with no
    // renderer would need dead-code paths in every switch. The Codable schema reserves
    // the discriminators "base"/"canvas"/"graph" — WorkspaceStore tolerates and drops
    // them on decode.)
}

struct TabID: Hashable, Codable { let raw: UUID }
struct GroupID: Hashable, Codable { let raw: UUID }

struct WorkspaceTab: Identifiable, Hashable, Codable {
    let id: TabID
    var item: EditorItem
}

indirect enum SplitNode: Hashable {
    case group(TabGroupNode)
    case split(SplitBranch)
}
struct TabGroupNode: Hashable {
    let id: GroupID
    var tabs: [WorkspaceTab]      // ordered
    var activeTabID: TabID?      // nil ⇔ tabs.isEmpty
}
struct SplitBranch: Hashable {
    enum Axis: Hashable { case horizontal, vertical } // horizontal = side-by-side
    let axis: Axis
    var children: [SplitNode]     // count ≥ 2 after normalization
    var weights: [Double]         // count == children.count, each ≥ minWeight, sums to 1
}

struct WorkspaceModel: Hashable {
    var root: SplitNode
    var activeGroupID: GroupID
}
```

### Invariants (the census contract — enforced by `WorkspaceModel.validate()`)

I1. `activeGroupID` names a group present in the tree.
I2. Every group's `activeTabID` is a member of its `tabs`; it is `nil` iff `tabs` is empty.
I3. An empty group exists **only** when it is the root (single-group empty workspace).
I4. Every `.split` has ≥ 2 children; children of a split never repeat the parent axis
    (same-axis children are flattened into the parent on insert).
I5. `weights.count == children.count`, every weight ≥ 0.15, `Σ == 1 ± 1e-9`.
    Corollary (capacity, census-found during U1-1): the workspace holds at most
    ⌊1/0.15⌋ = **6 groups total**; `split` rejects (nil, model unchanged) at the
    cap. The cap is global — not per-axis — because collapsing a cross-axis
    intermediary MERGES same-axis branches on normalize, so a per-axis
    split-time check still admitted 7-child branches whose floor is
    unsatisfiable. With ≤ 6 groups, every branch's floor is satisfiable
    unconditionally. (UI consequence for U1-3/U1-5: split affordances disable
    at 6 panes with a help explanation.)
I6. No two tabs share a `TabID`; no two groups share a `GroupID` (global uniqueness).
I7. Focus resolvability: `activeGroup.activeTabID` is non-nil whenever any tab exists
    anywhere in the tree.

`validate()` returns `[String]` violation descriptions (empty = valid). Census tests call
it after **every** operation; it is not a `debug_assert` — release-mode tests execute it.

### Model operations (all on `WorkspaceModel`, pure, returning mutated copies or in-place `mutating`)

```swift
mutating func openTab(_ item: EditorItem, in: GroupID?, activate: Bool) -> TabID
// Dedup rule: if `item` is already open in the target group, activate that tab instead
// of opening a duplicate (Obsidian behavior). A different group may hold the same item.
mutating func closeTab(_ id: TabID) -> CloseOutcome
// CloseOutcome: {focusedTab: TabID?, collapsedGroup: GroupID?} — see focus rules.
mutating func selectTab(_ id: TabID)
mutating func selectTab(ordinal: Int)      // ⌘1…⌘8; ordinal 9 = last tab (⌘9)
mutating func selectNextTab() / selectPreviousTab()   // wraps
mutating func moveTab(_ id: TabID, toIndex: Int)      // reorder within group
mutating func moveTab(_ id: TabID, toGroup: GroupID, index: Int?)
mutating func split(_ group: GroupID, axis: Axis, moveActiveTab: Bool) -> GroupID
// moveActiveTab=false duplicates the active tab's item into the new group (Obsidian
// "split right" keeps the doc visible in both panes); =true moves it.
// Split of a single-tab group with moveActiveTab=true is rejected (no-op returning the
// same group) — it would orphan the source group.
mutating func focusGroup(_ id: GroupID)
mutating func focusNeighbor(_ direction: Direction) -> GroupID? // ⌘⌥arrows; spatial
mutating func setWeight(delta: Double, for: GroupID)            // keyboard resize
```

**Close-tab focus rule (deterministic):** activate the tab to the **right** of the closed
tab; if none, the left neighbor; if the group empties: collapse the group node, renormalize
sibling weights proportionally, and focus the **nearest sibling group** — the previous
sibling in parent order, else the next; "nearest" descends into split children by taking
their **first** group depth-first. If the last group in the whole tree empties, the root
becomes that single empty group (empty-workspace state, I3).

**Spatial focus (`focusNeighbor`):** direction is resolved against the rendered geometry,
not tree order. The model computes each group's normalized rect (root = unit square,
splits partition by weights along their axis). Neighbor = the group whose rect is nearest
in the given direction with edge overlap ≥ 1pt-equivalent (ties → larger overlap, then
top/left-most). This is pure geometry on the model — censusable without a view.

### `NoteDocument` (U1-2) — parked per-tab state (ARCHITECTURE REVISED during U1-2)

**Amendment (2026-07-03, principal decision).** The original plan — extract AppState's
~35 note-scoped `@Published` fields into `NoteDocument` and forward — would repoint the
assertion targets of a large fraction of the existing suite, violating this spec's own
"assertions unmodified" constraint, and would move load/save/conflict machinery that
carries months of red-team history. Revised architecture, identical observable
semantics, fraction of the blast radius:

- **AppState's existing single-note fields ARE the active document.** No field moves.
  Every load/save/conflict/announcement path stays byte-identical.
- `final class NoteDocument: ObservableObject` is the **parked state of an inactive
  tab**: `{path, text, baselineText, contentHash, hasUnsavedChanges, saveError,
  saveConflict, hasLoaded}` (`@Published text/hasUnsavedChanges` so U1-3's unfocused
  panes can render it live).
- **Tab switch = snapshot ⊕ restore**: outgoing tab's fields snapshot into its
  `NoteDocument` (O(1), copy-on-write strings); incoming tab restores its parked fields
  and re-fires the async collection loads (headings/links/tasks/blocks/citations reload
  exactly as today's note-to-note switch does — the #90 stale-until-loaded discipline —
  but without the disk read for text). A tab never opened loads from disk via the
  unchanged `handleSelectionChange` path.
- **Same file in two tabs**: `updateEditorText` mirrors the new text (CoW assign, O(1))
  into every same-path parked document, so a duplicate pane renders current content.
- `activeDocument` as a projection is unnecessary: panels/toolbar keep binding
  `appState.current*` — which is by construction the focused tab's state. U4's leaf
  contract ("leaves describe the focused document") holds unchanged.
- The full extraction remains possible later if a real need appears (none of U3–U5
  requires it; U3's `fmSource`/`bodyText` ride the same snapshot).

**Data flow:** the sidebar keeps assigning `selectedFilePath`; the `$selectedFilePath`
sink → `handleSelectionChange` remains the single loader. That method gains the
workspace steps, in order: (1) if the incoming path is a *different tab* in the active
group → snapshot outgoing, restore incoming, skip the dirty gate (per-tab dirty is the
point of tabs); (2) otherwise the dirty gate fires exactly as today (the transition
would replace the active tab's buffer); (3) accepted selections snapshot the outgoing
tab, then mirror into the model (`replaceActiveTabItem` — the U1-4 line), then restore-
or-load. Tab-switch commands assign `selectedFilePath` and let the sink do the rest —
one funnel, no second loader. Tab close gates on that tab's dirty state (parked or
active) with the Save/Discard/Cancel alert; vault close aggregates over all tabs.

`AppState` keeps everything it has today and gains only `let workspace: WorkspaceState`
(already landed in U1-4). The dirty-navigation gate generalizes: switching **tabs**
never prompts; the gate fires only where a document would
be **discarded**: closing a dirty tab, closing the vault, or replacing the current tab's
item in-place (single-click open into the active tab, U1-5).

### Sidebar-selection contract change (U1-4)

Today `selectedFilePath` drives note loading. After migration: sidebar selection calls
`appState.openFile(path, target: .currentTab)` (U1-5 adds `.newTab` / `.split`).
`selectedFilePath` remains as the **selection mirror** for the sidebar highlight and
follows the active tab (switching tabs re-highlights the tree row); it no longer owns
loading.

---

## U1-1 · `WorkspaceModel` + census (#453) — PR 1

Deliverables: `WorkspaceModel.swift` exactly as above (pure Swift, `Foundation` only) +
`WorkspaceModelTests.swift`.

Tests (names are normative):
- `testInvariantsOnEveryConstructor` — fresh model, single-group, empty-workspace.
- `censusRandomOperationSequences` — **200k operations** across 2k seeded runs (Xoshiro
  seeded by run index; failures print the seed + op transcript for replay). Alphabet:
  open/close/select/next/prev/ordinal/reorder/moveToGroup/split(H,V,±move)/
  focusGroup/focusNeighbor(4)/setWeight(±). After every op: `validate()` empty, plus
  cross-op assertions — close focuses per the rule; split preserves total tab count
  (±0 moved / +1 duplicated); weights renormalize; dedup rule holds.
- `censusExhaustiveSmallN` — from the canonical 2-group/3-tab fixture, ALL operation
  sequences of length ≤ 3 over a bounded alphabet (≈ 20 ops ⇒ ≤ 8k sequences ⇒ fast):
  `validate()` empty after every step; no sequence reaches an unfocusable state (I7).
- `testSpatialFocusGeometry` — 2×2 grid fixture: every direction from every group lands
  per the geometry rule; a T-layout (left tall pane, right split) resolves ties per the
  overlap rule.
- `testCloseLastTabCollapsesToEmptyRoot`, `testSameAxisSplitFlattens`,
  `testMinWeightClamp`.

Acceptance = plan text + census clean in release mode (`swift test -c release` locally;
CI runs the standard config — both must pass).

## U1-4 · Migrate the center column (#456) — PR 2

Behavior-preserving reparent. `MainSplitView`'s `content:` column becomes
`WorkspaceView()`:

- `WorkspaceView` renders `workspace.model`: recursive `SplitNodeView` — `.group` →
  `TabGroupView` (tab strip hidden when `tabs.count == 1 && !splitExists` for this PR;
  strip arrives in U1-2), `.split` → `SplitContainerView` (arrives U1-3; until then the
  model is constrained to a single group).
- `TabGroupView` body for `.markdown`: today's `NoteContentView` unchanged, fed by the
  group's active `NoteDocument`… **except in this PR** `NoteDocument` doesn't exist yet:
  the single tab binds to AppState's existing single-note state 1:1. The workspace model
  runs with exactly one group and ≤ 1 tab, synchronized from `selectedFilePath` (select →
  tab item replaced; deselect → tab closed).
- Alert/save/conflict/popover/outline-scroll flows untouched (they still live on
  AppState state this PR).
- Empty-workspace state (no file selected) renders today's "Select a file to read."
  empty view, now inside the workspace region with `Tokens` styling.

Tests: full existing suite green, unmodified except mechanical view-path updates.
`PresentationReady.assertRendersInBothAppearances` over `WorkspaceView` in empty and
one-tab states. No new behavior ⇒ no new behavioral tests.

## U1-2 · Tab bar + lifecycle + `NoteDocument` (#454) — PR 3

Two halves, one PR (they're inseparable — a tab strip over shared state is a lie):

**(a) State generalization.** Introduce `NoteDocument` + `WorkspaceState` registry; move
the per-note fields/loads out of `AppState` (mechanical, guided by the field table in
§Shared architecture). Panels + toolbar rebind via `activeDocument`. The regression suite
is the safety net (this is the PR the plan calls "strictly behavior-preserving" — hold it
to that: with one tab, every existing test passes unmodified in assertion content; only
binding paths may change).

**(b) Tab strip UI.** `TabBarView` above the group content, height 30pt, `Tokens`-styled:

- Per tab: `SlateSymbol` glyph for the item kind (`.markdown` → none; kind glyphs arrive
  with N/T/P) + title (filename sans extension) + dirty dot (● 6pt, `accentText` color,
  **plus** the word "Edited" in the AX value — color is never the only carrier) + close
  button (visible on hover/focus AND always for the active tab; 16pt hit target inside a
  24pt row is a miss — the close button's tappable frame is ≥ 20×20 with the full-height
  row accepting ⌘-click close).
- Overflow: horizontal scroll with edge fade + "list all tabs" `moreActions` menu at the
  strip's trailing edge (menu = accessible fallback enumerating every tab).
- **AX contract:** the strip is `.accessibilityElement(children: .contain)`,
  `accessibilityLabel("Tabs")`, `accessibilityRole` exposed as a **tab list** — SwiftUI
  macOS has no native tab-list role on custom views, so each tab is a `Button` with
  `.accessibilityAddTraits(.isSelected)` when active and an
  `accessibilityValue` of `"tab N of M" + (dirty ? ", edited" : "")`; the strip's label
  plus per-tab values give VO the "tab 2 of 5, notes.md, edited" reading the plan
  requires. Close buttons: label "Close tab", value = tab title.
- Keyboard: ⌘T (new empty tab — opens the empty-state pane; typed as
  no-item? **No**: a tab always has an item; ⌘T opens the **file-open empty tab** =
  a `.markdown` tab whose path is nil? — resolved: `EditorItem` stays non-optional;
  ⌘T with no selection is a no-op that shows the palette (`slate.workspace.newTab`
  invokes quick-open once U1-5 lands; until then ⌘T duplicates the active tab). ⌘W close
  active tab (dirty → confirm alert, three-button Save/Discard/Cancel reusing
  pendingNavigation copy + focus-return). ⌘⇧[/⌘⇧] prev/next. ⌘1…⌘9 ordinal. **Keyboard
  reorder:** ⌃⌘← / ⌃⌘→ moves the active tab left/right (registered commands
  `slate.workspace.moveTabLeft/Right`) — the non-drag equivalent; drag reorder is the
  enhancement layered on the same `moveTab` call.
- All commands registered in `registerCoreCommands` under a new `CommandSection.workspace`
  with the drift test updated (`SlateCommandID.all`).
- Announcements: tab switch posts nothing (VO reads focus change); tab close announces
  "Closed <title>. <focused title> is active." via `postAccessibilityAnnouncement`
  (priority .medium); the dirty-close alert is a standard alert (self-announcing).

Tests: lifecycle census reuse (model already censused) + view-level: shortcut coverage
(each of the 8 shortcuts drives the expected model mutation through `WorkspaceState`),
VO value strings (unit-test the value-string builder), reorder-without-mouse, dirty-close
gate (Save/Discard/Cancel each verified incl. focus return), appearance snapshots via
`PresentationReady`, a11y-check 100.

## U1-3 · Split panes (#455) — PR 4

`SplitContainerView` renders a `SplitBranch`: children interleaved with 1pt
`Tokens.ColorRole.separator` dividers carrying an 8pt invisible grab zone
(`.gesture(DragGesture…)` updating `weights` live, clamped to I5's 0.15 min).

- **Divider keyboard resize:** dividers are not focusable (a divider with focus is a
  trap-prone oddity); instead `slate.workspace.growPane` (⌘⌥+) / `shrinkPane` (⌘⌥-)
  adjust the **focused group's** weight by 0.05 (clamped), announced: "Pane resized,
  N percent." Drag and keyboard route through the same `setWeight`.
- **Focus routing:** ⌘⌥←→↑↓ → `focusNeighbor`. The focused group is marked by (a) a 2pt
  `accentFill` top border on its tab strip (visible, not color-only: the strip also
  renders the active tab bolded) and (b) toolbar/status binding. Focus move announces
  "Editor pane N of M, <active tab title>." Each group is
  `.accessibilityElement(children: .contain)` labeled "Editor pane N of M" (N = spatial
  reading order — left→right, top→bottom, recomputed from the geometry the model already
  exposes).
- **Toolbar/status bind to focused pane:** already true after U1-2 (`activeDocument`).
- Split creation: ⌘\\ `splitRight`, ⌘⌥\\ `splitDown` (duplicate active item, per model
  dedup rules); context menu on tab adds "Split Right/Down". Close pane =
  close its last tab (⌘W chain) or `slate.workspace.closePane` (⌘⇧W conflicts with
  window close — use **⌘K ⌘W**? No chord infrastructure exists; resolved: no dedicated
  close-pane shortcut, palette + menu only; closing the last tab collapses the pane,
  which IS the keyboard path).
- NSTextView instances: one live editor per **visible group** (each group renders its
  active tab's document). Editors for other tabs in the same group do not exist;
  switching tabs swaps the editor's bound document (the U1-2 binding already does this).

**RED-TEAM census (the program's highest-risk surface):** before push, run a red-team
worktree pass focused on focus routing. Census
`censusFocusNeverLostAcrossSplitMutations`: random sequences over
split/close/move/focusNeighbor/resize on layouts up to depth 4; after every op assert I7
plus: focused group's rect is non-degenerate (weights ≥ min ⇒ visible), `focusNeighbor`
round-trips (→ then ← returns to origin when geometry is symmetric), and no
`focusNeighbor` result is a group absent from the tree. File findings as `audit` issues;
fix one per PR (project norm).

Tests: the census above (release mode), divider bounds (min weight, renormalization on
pane close), announcement strings, appearance snapshots, a11y-check 100.

## U1-5 · Open-in affordances + active-tab wiring (#457) — PR 5

- `appState.openFile(path, target:)` with `enum OpenTarget { currentTab, newTab,
  newSplit(Axis) }`; dirty semantics: `.currentTab` replaces the active tab's item —
  if that document is dirty, the pendingNavigation gate fires exactly as today;
  `.newTab`/`.newSplit` never prompt.
- Entry points wired: sidebar row (single-click = currentTab: **preserved behavior**;
  ⌘-click = newTab; context menu gains "Open in New Tab" / "Open in Split"), wikilink +
  backlink + outgoing-link activation (same trio: plain activate = currentTab, ⌘ =
  newTab; context menu on panel rows), search overlay result (Enter = currentTab,
  ⌘Enter = newTab), command palette (new commands below).
- Commands: `slate.workspace.openInNewTab` (⌘-less; palette-invoked on the current
  sidebar/panel selection), `slate.workspace.splitRight/-Down` (⌘\\ / ⌘⌥\\ — registered
  here since this PR wires them to *open-into*), `slate.workspace.moveTabLeft/Right`
  (from U1-2), `newTab` (⌘T now = open quick-open palette scoped to files, replacing the
  U1-2 duplicate-tab stopgap).

  *Amendment (2026-07-11, #863):* the ⌘T-for-quick-open allocation above (chosen by
  #495 on an Obsidian-muscle-memory premise that was factually wrong — Obsidian's
  default quick-switcher chord is **⌘O**, and Obsidian itself keeps the platform tab
  conventions) is superseded. New allocation: **⌘O = Quick Open…** (falls through to
  the vault picker on the welcome screen), **⇧⌘O = Open Vault…**, **⌘T = Duplicate
  Tab** (back in the tab family; `slate.workspace.newTab` keeps its id and its
  duplicate semantics), **⇧⌘T = Reopen Closed Tab** (new command
  `slate.workspace.reopenClosedTab`: a capacity-bounded per-vault-session closed-tab
  stack in `WorkspaceState`, pushed at the `close(_:)` funnel, reopened through this
  section's open-target path honoring the U1-2 dedup rule), **⌘R = Show Tasks
  Review** (was ⇧⌘T). Decision record in #863.
- Keyboard reachability proof: every target reachable via context menu (VoiceOver
  actions rotor picks up SwiftUI `contextMenu` items) AND palette. Tests assert the
  command registry contains the full set (drift test) and each entry point's ⌘-variant
  routes to the right `OpenTarget`.

## U1-6 · Session restoration (#458) — PR 6

- `WorkspaceStore` (pattern: `PrefsJsonStore`) reads/writes
  `<vault>/.slate/workspace.json`, atomic temp+rename, 256 KiB bounded read, schema:

```json
{ "version": 1,
  "activeGroup": "…",
  "root": { "kind": "split", "axis": "horizontal", "weights": [0.5, 0.5],
             "children": [ { "kind": "group", "id": "…", "activeTab": "…",
                              "tabs": [ { "id": "…", "item": { "kind": "markdown",
                                                                 "path": "notes/a.md" } } ] } ] } }
```

  *Amendment (2026-07-12, #873, revised same day):* top-level `"expandedDirPaths": [String]` — vault-relative dir paths in EXPANSION-RECENCY order (oldest→newest), optional/additive (absent = none, unknown fields dropped — the sparse version-tolerance contract). Write: order-preserving dedup (last occurrence wins) then cap 500 keeping the newest suffix. Read: entries must be non-empty, ≤ 1024 chars, relative (no leading `/`), no `..` components; same dedup+cap. Paths, not `dirs.id` rowids: SQLite reuses rowids after deletes, so a persisted id could expand an unrelated new folder.

- Save: debounced 500ms after any workspace mutation + on vault close + `applicationWillTerminate`.
- Restore on vault open: decode → drop tabs whose `item` kind is unknown (forward-compat
  with N/T/P discriminators) → validate() → if violations, fall back to fresh default
  (never crash, never half-restore); missing files are NOT dropped — the tab opens in a
  **per-tab error state** ("<name> was moved or deleted." + Close Tab button, `Tokens`
  styling, labeled region) satisfying the plan's "degrade gracefully".
- Restore does NOT restore dirty text (unsaved buffers are not persisted — data-loss
  honesty: the dirty-close/vault-close gates already prevent silent loss).
- Tests: round-trip (model → JSON → model == identity for a depth-3 fixture), unknown
  version → default, unknown item kind → dropped tab, missing file → error-state tab
  (view test), truncated/corrupt file → default + no crash, census
  `censusPersistRestoreIdentity` (random valid models × encode/decode).

---

## SlateSymbol additions (land with the PR that first renders them)

| Role | v7 | fallback | PR |
|---|---|---|---|
| `.splitDown` | `rectangle.split.1x2` | `rectangle.split.1x2` | U1-3 |
| (in use, already defined) `.newTab .closeTab .splitRight` | — | — | U1-2/3 |

## Benchmarks / perf gates (U1 close)

- Tab switch and focus move: no measurable main-thread stall (Instruments spot check +
  the census proves model ops are O(tree)); document in PR.
- No new per-keystroke work: `NoteDocument.updateEditorText` is byte-identical logic to
  today's.
- `swift test` wall time recorded pre/post in `BENCHMARKS.md` §UI notes (suite growth
  expected from censuses; keep census iteration counts tuned so the suite stays < 10 min
  on CI mac-actions runners — if 200k ops exceeds that, drop to the largest count that
  fits and record the number).
