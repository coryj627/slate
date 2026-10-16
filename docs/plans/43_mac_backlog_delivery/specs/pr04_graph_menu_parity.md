# PR 4 — Graph menu parity: the presets and the Verbosity switch

Issues: #1219, #1216. Size S. Lanes: swift, a11y, windows. D8 decided
2026-10-16: a View ▸ Graph submenu. Parallel with PRs 1, 3, 6, 7; shares
`SlateMacApp.swift` and `SlateCommands.swift` with PR 2 (land PR 2 first)
and the graph config files with PR 5.

## Goal

The three graph presets and the graph verbosity level are reachable from
the menu bar on mac, the way Windows' Graph menu (contract 35 C-12)
offers them, so the registry, the palette and the menu agree on both
hosts and `GraphVerbosity` finally has a switch to go with its store.

## Current state on main

- Registry ids exist: `SlateCommands.swift:245-247` (`graphOrphans`,
  `graphUnresolved`, `graphMostLinked`), registered at `:1538-1555`
  (`label: "Graph: Orphaned Notes"` etc., `section: .graph`, closures
  calling `openGraphPreset(.orphans)` and so on). `openGraphTab`
  (`:258`) exists. The menu bar names none of the presets; the only
  graph-specific menu items are "Fit Graph" (`SlateMacApp.swift:725`,
  ⌥⌘0, in the zoom group) and the routed "Where Am I?" (`:598`).
- Mac builds its menus with `CommandGroup`s inside the standard menus
  only (`SlateMacApp.swift:209`, `:377`, `:403`, `:437`, `:810`, `:822`,
  `:897`); there is no custom top-level `CommandMenu`.
- Verbosity: `GraphVerbosity` is core's enum, extended in
  `Graph/GraphAnnouncer.swift:5-24` with `specs` (titles and persistence
  tags fetched once from `graphVerbosities()`) and `allCases`;
  `GraphAnnouncer.verbosity` is `@Published` (`:95-97`). The store half
  shipped: `graphConfigSaveAggregate()` folds
  `cfg.verbosity = graphAnnouncer.verbosity` (`Graph/AppState+GraphConfig.
  swift:69`) and the restore path applies the persisted level. Nothing
  sets the level from the UI (#1216).

## Windows reference

- `MainWindow.xaml:677-693`: a top-level `_Graph` menu with Open Graph,
  a separator, `GraphOrphansMenuItem`, `GraphUnresolvedMenuItem`,
  `GraphMostLinkedMenuItem`, Where Am I?, a separator and the Verbosity
  submenu. Every item's command is the registrar's for the same id
  (drift test 2, `ChordTableTests`).
- `Graph/GraphVerbosityMenu.cs:25-50` `Populate`: one checkable item per
  `preferences.Choices` (title from core's vector, tag as the parameter),
  `SetVerbosityCommand`, automation id `GraphVerbosity.<tag>`, checked
  state bound to the choice.
- Contract 35 C-12 (`:9612`); C-D9 (`:9895`) records "Menu items for Open
  Graph and the presets" as a Windows enrichment; §0a "Mac details"
  (`:734-738`) records the missing verbosity switch; `w6_2_graph_spec.md`
  D-6.

## Design

1. **Placement (D8, decided).** A `View ▸ Graph` submenu (`Menu
   ("Graph")` inside the existing View-menu `CommandGroup`) containing
   Open Graph (bound to `openGraphTab`), a divider, Orphaned Notes,
   Unresolved Links, Most Linked (bound to the three ids), a divider and
   a `Verbosity` submenu. "Fit Graph" and "Where Am I?" stay where they
   are (chord parity, not placement, is the invariant).
2. **Binding rule.** Menu items invoke the registry closure for the same
   id (never a second code path), so the palette, the chord table and
   the menu cannot drift; disabled states follow the registry's
   availability (vault open, graph available).
3. **Verbosity.** The submenu is built from `GraphVerbosity.specs` in
   core's order, one checkmark item per level (title from the spec),
   checked when `graphAnnouncer.verbosity == level`. Selecting a level
   calls one setter, `setGraphVerbosity(_:)`, which writes
   `graphAnnouncer.verbosity` and schedules the config save (the Term W7
   per-field write; PR 5 makes the aggregate per-field for the filters,
   this PR for verbosity: the setter must not fold the live table filter
   into the save, so coordinate with PR 5 or land after it). Palette rows
   "Graph Verbosity: {Title}", one per level, same setter, in the graph
   section.
4. **Announcement.** Selecting a level speaks nothing new of its own
   (the next graph event renders at the new level); if the canvas
   verbosity switch announces its change on mac, mirror that policy for
   consistency and say which in the PR.

## Changes

- `SlateMacApp.swift`: the submenu in the View menu (or the top-level
  menu under D8's alternative).
- `SlateCommands.swift`: the verbosity ids and `register` calls; the
  preset ids are unchanged.
- `Graph/AppState+GraphConfig.swift`: `setGraphVerbosity(_:)` and its
  save scheduling.

## Tests

- `CommandRegistryTests`: the verbosity ids exist in the graph section
  with hints; the preset ids are unchanged.
- Menu census: whichever test pins menu ids (`SlateCommandsTests` or
  `MenusObservabilityTests`) includes the new items and asserts each is
  bound to its registry id.
- `GraphConfigTests.testSettingTheVerbosityPersistsToGraphJsonAndRestores`:
  set a level, save, reload a fresh `AppState` over the same vault, the
  announcer starts at that level.
- `GraphAnnouncerTests`: a row event renders differently under two
  levels (proves the switch reaches rendering).

## Accessibility

Menu items are native; VoiceOver reads them. The checked verbosity item
reads as checked. No field pass needed.

## Docs and registers

`docs/plans/35_graph_contracts.md`: C-D9 (`:9895`) retired (mac has the
items); §0a "Mac details" verbosity gap (`:734-738`) resolved; the #746
issue-reconciliation rows for #1216 and #1219 closed.
`docs/plans/18_windows_port/specs/w6_2_graph_spec.md` D-6 recorded
delivered on mac.

## Acceptance

- [ ] The presets and the verbosity levels are in the menu bar and the
      palette, bound to the registry.
- [ ] Verbosity persists and restores through `.slate/graph.json`.
- [ ] Registers updated; lanes green.

## Risks

Menu edits touch the same file as PR 2; rebase. The verbosity setter and
PR 5's per-field persistence touch `AppState+GraphConfig.swift`; agree
on the setter shape (PR 5's spec names it).

## Brief

Give mac the graph menu items Windows has. Read `SlateMacApp.swift`
around `:598` and `:725`, `SlateCommands.swift:245-258` and
`:1538-1555`, `Graph/GraphAnnouncer.swift:5-24`, `Graph/AppState+
GraphConfig.swift:55-75`, the Windows `MainWindow.xaml:677-693` and
`Graph/GraphVerbosityMenu.cs`. Add a View ▸ Graph submenu (D8) with Open Graph, the three presets and a
Verbosity submenu built from `GraphVerbosity.specs`; every item invokes
the registry closure for its id. Add `setGraphVerbosity(_:)` that writes
the announcer's level and schedules the save, plus one palette row per
level. Write the facts under "Tests" first. Retire C-D9 and the §0a
verbosity note in contract 35 and record D-6 delivered. Done when the
menu census and config round-trip facts pass and the lanes are green.
