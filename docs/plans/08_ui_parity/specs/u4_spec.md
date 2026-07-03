# U4 executable spec — Right-hand leaves + utility rail

Issues: #470 (U4-1) · #471 (U4-2) · #472 (U4-3) · #473 (U4-4).
Milestone: GH 27. Depends on U1 (leaves reflect the focused tab). Parallel with U3 —
**coordination point:** U3-3 removes PropertiesPanel from the sidebar stack; U4-2 ports
the remaining seven and the detail-column three. If U4-2 lands first, it ports all
panels EXCEPT Properties and leaves the stack shell for U3-3 to delete… **Resolved:**
U4-2 ports the seven non-Properties stack panels AND deletes the stack `ScrollView`;
if U3-3 has not landed, `PropertiesPanel` moves to a temporary bottom section of the
left sidebar (one `DisclosureGroup`, unchanged bindings) that U3-3 then deletes. This
keeps both milestones independently mergeable in either order with no dead week.

Execution order matches issue order: U4-1 → U4-2 → U4-3 → U4-4.

---

## U4-1 · Leaf container + vertical icon rail (#470) — PR 1

### Layout

`MainSplitView`'s `detail:` column is replaced by `RightPaneView`:

```
HStack(spacing: 0) {
    activeLeafContent   // ZStack of all leaves, mounted-retention gated
    Divider (Tokens separator)
    LeafRailView        // vertical icon rail, fixed 40pt wide, trailing edge
}
```

Rail on the **trailing edge** (Obsidian parity; the pane collapses leftward). The
NavigationSplitView detail column keeps its native collapse control; a rail icon click
when collapsed expands the column (via `NavigationSplitViewVisibility` binding).

### Leaves (registry)

```swift
enum Leaf: String, CaseIterable, Identifiable, Codable {
    case outline, backlinks, outgoingLinks, embeds, math, code, diagrams,
         tasks, citations, bibliography
}
```

Ships in U4-1 with the three current detail tabs (outline, citations, bibliography)
live and the other seven placeholder-registered (cases exist; content arrives in U4-2 —
the rail renders only leaves whose content is registered, so this PR shows 3 icons and
the pane is behavior-equivalent to today's segmented picker, minus the picker).

### Rail semantics

- `@Published var activeLeaf: Leaf` on `WorkspaceState` (persisted in
  `workspace.json` — `"activeLeaf": "outline"`, absent = outline).
- Each rail item: `Button` with `SlateSymbol` glyph (`image(label:)` — labeled), 28pt
  glyph in a 40×36 hit target, `help(leaf.title + " (" + shortcutHint + ")")`,
  `.accessibilityAddTraits(.isSelected)` when active. Selected state: `accentText`
  tint + a 2pt leading selection bar (`accentFill`) — shape + color, never color alone.
- Container: `.accessibilityElement(children: .contain)`, label "Panel rail",
  `accessibilityHint("Choose which panel is shown")`. **Radio-group interaction:** the
  rail is one focus stop; ↑/↓ move a `@FocusState`-tracked highlight inside it
  (`onMoveCommand`), Space/Return activates — matching the segmented picker it replaces
  (arrow-within, Tab-out). VO users get "Panel rail, list … Outline, selected, 1 of 10".
- Leaf switch announces "\(leaf.title) panel." (.medium) — replaces the picker's native
  announcement.

### Retention gating (the load-bearing pattern, verbatim from MainSplitView:118-131)

`ZStack { ForEach(registered leaves) { leaf in leafContent(leaf)
  .opacity(activeLeaf == leaf ? 1 : 0)
  .allowsHitTesting(activeLeaf == leaf)
  .accessibilityHidden(activeLeaf != leaf) } }`

All registered leaves stay mounted (state retention: BibliographyPanel.segment/hasLoaded,
OutlineSidebar.announcedFilePath — the exact regressions the comment documents). Only
the visible leaf exists for AX/pointer. This is today's cost envelope: the 8 stack
panels + 3 detail panels are all permanently mounted already.

### Tests

State retention across switches (BibliographyPanel segment survives outline→bib→outline;
load-fire spy proves no re-fetch), only-visible-leaf AX (hidden leaves absent from AX
tree — inspection assert), rail keyboard navigation (arrow/activate mapping), rail AX
labels/values/selected, persistence round-trip, appearance snapshots + APCA on
selected/rest rail states (PresentationReady), a11y-check 100.

## U4-2 · Port panels to leaves; retire the sidebar stack (#471) — PR 2

- Move `OutlineSidebar`, `BacklinksPanel`, `OutgoingLinksPanel`, `EmbedsPanel`,
  `MathBlocksPanel`, `CodeBlocksPanel`, `DiagramsPanel`, `TasksPanel` (and keep
  `CitationsPanel`, `BibliographyPanel` where U4-1 put them) into the leaf registry.
  Panels move **unchanged** — same files, same bindings (they already bind to
  `appState.activeDocument` after U1-2), same AX. The only edits: their outer
  self-hiding `EmptyView` gates become leaf empty states ("Select a note to see its
  outline." etc. — a leaf must never be a blank rectangle when its icon is selectable;
  DoD §A empty-state rule supersedes the stack's self-hiding, which existed to avoid
  pushing the file list around — a constraint that no longer applies).
- Delete the sidebar `ScrollView` panel stack from `FileTreeSidebar` (Properties per the
  header coordination note). The left sidebar is now: tree + (U4-3) utility row.
- Leaf order in the rail = the registry order above (outline first — most used; matches
  the old default tab).
- Each leaf's `DisclosureGroup` headers ("Backlinks, N entries") become the leaf's
  header row (no longer collapsible — the rail selects, the leaf fills the pane; the
  disclosure was a stack-era space-saver. Headers keep their `.isHeader` trait and
  count).

Tests: every existing panel test re-pointed at the leaf host and passing unchanged in
assertion content (this is the "identical capability" acceptance); per-leaf empty states
render + are labeled; the sidebar contains only the tree (+utilities); Milestone K/L
regression subsets green; a11y-check; appearance snapshots for the two densest leaves
(tasks, bibliography).

## U4-3 · Bottom-left utility icon buttons (#472) — PR 3

- `SidebarUtilityBar.swift`: a 36pt-tall `HStack` pinned at the sidebar bottom
  (below the tree, above nothing), `Tokens.Spacing.sm` padding, separator above:
  - **Settings** (`SlateSymbol.settings`, label "Settings", help "Settings (⌘,)"):
    sends `showSettingsWindow:` — the exact selector `registerCoreCommands` already
    uses (one implementation, two entry points).
  - **Help** (`.help`, label "Help"): opens the project README URL via the existing
    external-open path (gap G13); registered as `slate.help.open`.
  - **Vault switcher** (`.vaultSwitch`, label "Switch vault"): a `Menu` listing recent
    vaults (from `RecentVaultsStore`, current vault checkmarked + disabled), divider,
    "Open Other Vault…" (`pickAndOpenVault`), "Close Vault" (`closeVaultFromUserAction`
    — the dirty-gate + announcement flow, unchanged). Switching to a recent vault =
    close-current-then-open (routes through the same dirty gate; on cancel, no switch).
- All three: 28pt glyphs in ≥ 36×32 targets, focusable, menus keyboard-operable (SwiftUI
  `Menu` is), `.accessibilityElement(children: .contain)` container labeled "Vault
  utilities".
- The toolbar "Close Vault" button is removed in this PR (the utility bar + command
  registry + File menu cover it; the toolbar was the wrong prominence for a destructive-
  adjacent action — DoD §C action-hierarchy rationale in the PR).

Tests: each button's action routing (settings selector send spy, help URL spy, vault
menu = recents + correct enabled states), dirty-gate on switch (cancel keeps everything),
AX labels/help, keyboard operation of the menu, appearance snapshots, a11y-check.

## U4-4 · Focus routing editor ↔ right pane + leaf context (#473) — PR 4

- **Leaf context:** already correct by construction after U1-2 (leaves bind
  `activeDocument`, which tracks the focused pane's active tab). This PR adds the
  **tests** that pin it: switch tab → outline/backlinks/tasks update; switch pane focus
  → ditto; close tab → leaves fall back to the new active document; no document →
  leaf empty states.
- **Focus routing:** extend U1-3's `focusNeighbor` geometry to treat the right pane as
  the easternmost region: ⌘⌥→ from the rightmost editor group moves focus INTO the
  active leaf (AX focus to the leaf's first element via `@AccessibilityFocusState`;
  keyboard focus to its first focusable); ⌘⌥← from the leaf returns to that rightmost
  group (the model remembers `lastFocusedGroup` — focus is never lost, I7 extended).
  From the leaf, ⌘⌥→ is a no-op (edge). The file tree is likewise the westernmost
  region: ⌘⌥← from the leftmost group focuses the tree; ⌘⌥→ from the tree returns.
  Announcements: "\(leaf.title) panel." / "Editor pane N of M, <title>." / "Files."
- No trap: from any region, ⌘⌥arrows + Tab both exit (Tab order is the window's native
  order; the routing commands are an overlay, not a replacement — assert both in tests).
- Commands `slate.workspace.focusLeftPane/RightPane/PaneAbove/PaneBelow` registered
  (palette names: "Focus pane left/right/above/below") — these are U1-3's bindings,
  formally extended to the two terminal regions here; drift test updated.

Tests: routing census extension — `censusFocusRoutingWithTerminalRegions` (U1-3's census
alphabet + tree/leaf terminals; focus always resolvable, round-trips at edges),
announcement strings, leaf-context matrix above, a11y-check.

---

## SlateSymbol additions

| Role | v7 | fallback | PR |
|---|---|---|---|
| `.outline` | `list.bullet.indent` | `list.bullet.indent` | U4-1 |
| `.backlinks` | `arrow.uturn.backward` | `arrow.uturn.backward` | U4-2 |
| `.outgoingLinks` | `arrow.up.right` | `arrow.up.right` | U4-2 |
| `.embed` | `photo.on.rectangle` | `photo.on.rectangle` | U4-2 |
| `.diagram` | `point.3.connected.trianglepath.dotted` | `point.3.connected.trianglepath.dotted` | U4-2 |
| `.tasksLeaf` | `checklist` (shared with `.tasksReview`) | `checklist` | U4-2 |
| `.settings` | `gearshape` | `gearshape` | U4-3 |
| `.help` | `questionmark.circle` | `questionmark.circle` | U4-3 |
| `.vaultSwitch` | `externaldrive` | `externaldrive` | U4-3 |

(`.math`, `.code`, `.bibliography`, `.citationSummary` exist from U0 — leaves reuse
them; `.tasksLeaf` aliases `.tasksReview`'s glyph deliberately: same metaphor, same
glyph — consistency rule from DoD §B.)

## Follow-ups filed during U4

- Right-pane width persistence + per-leaf width memory — file with U4-1 as
  `enhancement` (workspace.json v1 carries pane width if trivial; else follow-up).
- Leaf pinning / multiple simultaneous leaves (Obsidian stacks leaves) — explicitly out
  of v1 scope; file as `enhancement` with U4-2.
