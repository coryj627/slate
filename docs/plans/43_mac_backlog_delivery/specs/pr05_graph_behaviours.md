# PR 5 — Graph behaviours: the Connections leaf loads, filters persist by field, a restored Diagram is silent

Issues: #1217, #1218, #1220. Size M. Lanes: swift, a11y, windows. Depends
on nothing. Parallel with PRs 1 to 3, 6, 7; PR 13 waits for this (shared
`AppState+GraphTable.swift`); PR 4's verbosity setter follows this PR's
per-field rule.

## Goal

Three divergences from the Windows graph recorded by the W6-2 close-out,
each a mac behaviour bug: a mounted rail switch to a stale Connections
leaf shows loading text and loads nothing; a needle typed under a
transient preset persists the preset's backend filter; restoring a
persisted Diagram speaks the mode line. Plus the unused constant the same
reading found.

## Current state on main

- `Graph/ConnectionsPanel.swift:30` loads only in `.onAppear` (when the
  leaf is active) and in `.onChange(of: appState.selectedFilePath)`
  (`:31-37`, only while active and not re-rooted). A leaf that was
  mounted but inactive while the note changed, then activated through
  the rail, shows "Loading connections…" and starts no load.
  `connectionsLoading` (`AppState.swift:2968`) is written at
  `Graph/AppState+Connections.swift:57`, `:77`, `:85`, `:116` and read by
  nothing (#1217).
- `Graph/AppState+GraphConfig.swift:61-71` `graphConfigSaveAggregate()`
  builds `cfg.filters` from the live `graphTableFilter` and
  `graphTableTextFilter`. A preset writes the live filter
  (`Graph/AppState+GraphTable.swift:554`, in `openGraphPreset` `:538`), a
  user flag change goes through `setGraphTableFilter` (`:514-523`), and
  the restore applies `graphTableFilter = graphConfig.filters.backend`
  (`AppState+GraphConfig.swift:81`). So a needle typed during a preset
  persists the preset's flags to `.slate/graph.json` (#1218, C-D7).
- `Graph/GraphTableView.swift:87-110` `onChange(of: mode)`: announces
  `.graphMode(mode:)` on every change, the restore's included;
  `suppressFocusBumpOnce` suppresses only the focus bump (#1220).
- `Graph/GraphDiagramView.swift:174` `labelFadeZoom = 0.55` is declared
  and never read (the decision reads `display.textFadeZoom`, `:527`).

## Windows reference

- B-D11 (contract 35 `:5503`): "A mounted rail switch to a STALE leaf
  loads (Term 3(b))"; the leaf renders its loading row from the loading
  flag.
- Term W7 (`:8878-8892`): one mutable config per workspace; each trigger
  updates ITS field atomically before the schedule (`SetNameQuery →
  filters.nameQuery`, `SetVerbosityCommand → verbosity`, depth,
  mode, the inspector's filter change → the three backend flags); the
  aggregate handed to the writer IS the config; the live view state is
  not folded in, so a transient preset never persists. C-D7 (`:9889`).
- DD-18 (`:13224`): a persisted Diagram mode builds at the seat and
  speaks no mode line; `GraphMode` is the switch's line (Term M1).

## Design

1. **Connections (#1217).** Add `.onChange(of: appState.
   connectionsLeafActiveForView)`: when it becomes true and the loaded
   path differs from `connectionsEffectivePath` (stale), call
   `loadConnections()`. Render the loading row from `connectionsLoading`
   (true → "Loading connections…"; false with no rows → the empty state),
   so the text can no longer be shown without a load in flight. Keep the
   re-root guard.
2. **Persist by field (#1218).** `graphConfig` becomes the one mutable
   config: `setGraphTableFilter` writes `graphConfig.filters`' backend
   flags (and the live filter); the needle setter writes
   `graphConfig.filters.nameQuery`; `openGraphPreset` writes only the
   live `graphTableFilter` (transient); `graphConfigSaveAggregate()`
   returns `graphConfig` with depth folded as today and verbosity written
   by PR 4's setter (until PR 4 lands, keep folding verbosity from the
   announcer; the aggregate must stop reading `graphTableFilter` and
   `graphTableTextFilter`). The plain-open restore still applies
   `graphConfig.filters.backend` to the live state (`:81`).
3. **Silent restore (#1220).** In `onChange(of: mode)`, when the change
   is the restore (`suppressFocusBumpOnce` true, or a dedicated
   `restoringMode` flag if the bump flag is reused elsewhere), skip the
   announcement as well as the bump. A user switch still speaks.
4. Delete `labelFadeZoom`.

## Changes

- `Graph/ConnectionsPanel.swift:18-48`.
- `Graph/AppState+GraphConfig.swift:55-85`; `Graph/AppState+GraphTable.
  swift:514-560` (the setters and the preset).
- `Graph/GraphTableView.swift:87-110`.
- `Graph/GraphDiagramView.swift:174`.

## Tests

- `ConnectionsPanelTests.testAMountedRailSwitchToAStaleLeafLoads` and
  `testTheLoadingRowFollowsConnectionsLoading`.
- `GraphConfigTests.testANeedleTypedUnderAPresetLeavesThePersistedFlagsUnchanged`:
  open the orphans preset, type a needle, read `.slate/graph.json`: the
  backend flags equal the pre-preset flags, `nameQuery` equals the
  needle. `testAUserFlagChangeUnderAPresetPersistsThatFlag`.
  `testAPlainOpenReappliesThePersistedFilters` (regression).
- `GraphTableViewTests.testARestoredDiagramSpeaksNoModeLine` and
  `testAUserSwitchToDiagramSpeaksTheModeLine`.

## Accessibility

A restore that spoke "Diagram" before now says nothing, matching
Windows; the user's own switch still announces. No new copy.

## Docs and registers

`docs/plans/35_graph_contracts.md`: PR B "Mac details" (`:5514-5519`)
resolved; C-D7 (`:9889`) retired; PR D "Mac details" items
`labelFadeZoom` (`:13320-13321`) and the restore-driven `onChange`
(`:13325-13326`) resolved; the #746 reconciliation rows for #1217, #1218,
#1220 closed.

## Acceptance

- [ ] The six facts above fail before and pass after.
- [ ] The aggregate no longer reads the live table filter.
- [ ] Registers updated; lanes green.

## Risks

The per-field change touches every writer of the graph filter; a missed
writer silently stops persisting a user change. The
`testAUserFlagChangeUnderAPresetPersistsThatFlag` and plain-open facts
are the guard; grep every `graphTableFilter =` and `graphTableTextFilter
=` site (`AppState+GraphConfig.swift:81`, `AppState+GraphTable.swift:139`,
`:523`, `:554`) and classify each as user, preset or restore in the PR.

## Brief

Fix three graph behaviours and remove one dead constant. Read
`Graph/ConnectionsPanel.swift`, `Graph/AppState+Connections.swift`,
`Graph/AppState+GraphConfig.swift:55-85`,
`Graph/AppState+GraphTable.swift:514-560`,
`Graph/GraphTableView.swift:84-112`, and contract 35's B-D11, Term W7,
C-D7 and DD-18. (1) Make the Connections leaf load when it becomes the
active leaf while stale, and render its loading row from
`connectionsLoading`. (2) Persist graph filters by field: user flag
changes and the needle write `graphConfig`; a preset writes only the
live transient filter; the save aggregate returns `graphConfig` and
never folds the live filter. (3) Make the restore-driven mode change
silent. (4) Delete `labelFadeZoom`. Write the facts under "Tests" first.
Close the register rows named above. Done when the facts pass, every
filter writer is classified in the PR body, and the lanes are green.
