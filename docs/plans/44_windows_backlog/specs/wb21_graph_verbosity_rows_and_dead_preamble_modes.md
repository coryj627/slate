# WB-21 — The palette has the three graph verbosity rows mac has, and the code-preamble menu offers only the mode that does something

Issues: Closes #1415; Refs #1389 (its Windows half; the issue closes with whichever of WB-21 and plan 43 PR 52 merges last, per §5's closure matrix). Size S. Lanes: windows. AT: NVDA spot (the three palette rows read "Graph Verbosity: Terse/Standard/Verbose" and run; no verbosity announcement for a removed mode). Wave: W7. Depends on: WB-17 (`ChordTable.cs` / `chords.json` order). Parallel with: WB-19, WB-20. Decision: none (RD6 answered 2026-10-19).

## Goal

Mac registers one chordless palette row per graph verbosity level
(`slate.graph.setVerbosityTerse/Standard/Verbose`); Windows records them
as unregistered rows, so the registry parity the program holds to is
one-sided and `parity_matrix.md` lists them pending. After this chunk a
Windows reader finds the same three rows in the palette and each sets
the level through the setter the Graph menu already uses. In the same
pass, RD6: the "Preamble + first line" and "Preamble + all tokens" code
verbosity items, which change nothing a reader hears, leave the Editor
menu on Windows; a stored non-default value falls back to "Preamble
only" silently. WB-21b brings the real modes back through core.

## Current state on main

Verified against the worktree (`report_a11y.md`'s lines hold).

- `Commands/ChordTable.cs:1121-1126`: three `Unreg("windows.graph.setVerbosity{Terse,Standard,Verbose}", "Graph Verbosity: …", CommandSection.Graph, GraphVerbosityReason)`;
  `:1129-1137` `GraphVerbosityReason` ("a palette row cannot supply the
  parameter"; names #1415). `Reg(...)` helper at `:617-640`; the graph
  `Reg` rows at `:1077-1085` (`GraphPhrase` labels and hints).
- `Commands/SlateCommandRegistrar.cs:415` `BuildResolvers()`: id →
  `Func<ISlateCommandHost, ICommand?>`; the graph rows at `:532-546`
  all resolve to a `Workspace` command.
- `Graph/GraphPreferencesViewModel.cs:113-124` `SetVerbosityCommand`
  (a `RelayCommand` taking the level's `Tag`), `:161-182` `SetVerbosity(level)`
  (no-op re-assert on the same level, else store, `VerbosityChanged`,
  `ScheduleSave`); `:146` `Levels` is core's vector
  (`GraphVerbositySpec { verbosity, tag, title }`, `lib.rs:4900-4904`).
  The workspace holds it as `_graphPreferences` (`WorkspaceViewModel.cs:2245`),
  with no public property — the registrar needs one.
- `GraphPreferencesTests.cs:238-255` pins the three Unreg rows
  (`IsRegistered` false, reason contains "parameterized setter").
- `ChordTableTests.cs:233` `ChordsJson_IsExactlyTheTablesProjection`
  (regenerate with `SLATE_CHORDS_UPDATE=1`, never hand-edit);
  `deliveryEvidence.commands` maps each registered id to an evidence
  group (`chords.json:2809`; the graph group `:2733-2745`).
- `scripts/generate-parity-matrix.py:99-101` `ID_ISSUE_OVERRIDES` routes
  the three ids to #1415; `parity_matrix.md:148-150` lists them pending.
- Mac: `SlateCommands.swift:283-285` ids, `:1676-1696` the three
  registrations — labels "Graph Verbosity: Terse|Standard|Verbose", hints
  "Set the graph announcement verbosity to Terse|Standard|Verbose for this vault.", section `.graph`, chordless.
- **#1389 Windows half.** `MainWindow.xaml:537-556`: menu "Code Preamble
  Verbosity" with three `CheckMenuItem`s (AutomationIds
  `EditorCodeVerbosity{PreambleOnly,FirstLine,AllTokens}MenuItem`,
  `CommandParameter` `preambleOnly|preambleFirstLine|preambleAllTokens`).
  `EditorInteractions.cs:59-65` `EditorPreferencesViewModel.CodeVerbosityNames`
  (three keys); `:131-135` the load falls back to `preambleOnly` for an
  unknown key; `:185-205` `SetCodePreambleVerbosityCommand` announces
  `A11yEvent.CodePreambleVerbosity(display)`; `:375-400` the property
  persists through `AppPreferencesStore` (`AppPreferencesStore.cs:19-20`
  default `"preambleOnly"`, `:83-85` null normalisation). The sole
  render consumer, `Reading/ReadingDocumentBuilder.cs:409`, calls
  `CodeBlockPreamble(language, source)` and never reads the setting —
  the issue's claim holds. `ChordTable.cs:870-871` keeps
  `Unreg("windows.editor.setCodePreambleVerbosity", …, ParameterizedReason)`.
- Tests touching the modes: `ReadingViewTests.cs:3544-3552` (selects
  `preambleFirstLine` and pins "Code preamble verbosity: Preamble +
  first line."), `CheckItemGroupsTests.cs:114-115` (the three-item check
  group), `CommandDriftTests.cs:349-350` (the parameterised disposition).
- Plan 43 PR 52 (`00_program.md:464`, RD6 `:519`, `:562`) owns the mac
  half and the shared retitle; it is file-disjoint from this chunk.

## Reference

- Plan 43 PR 4's spec `43_mac_backlog_delivery/specs/pr04_graph_menu_parity.md`
  (the mac rows); plan 43 §4 rule 4 (registry parity by id, labels
  byte-identical, P3).
- RD6 (`00_program.md:519`): "Remove them on both hosts now. File the
  real modes as a core enhancement." The owner's comment on #1389
  (2026-10-19) adds: "A stored non-default value falls back to
  'Preamble only' silently."
- Contract 35 C-9 / W6-2 PR C (the one parameterised setter); contract
  28 P3 (labels byte-identical to mac).

## Design

**#1415.** Replace the three `Unreg` rows with `Reg(Ids.GraphSetVerbosityTerse, "Graph Verbosity: Terse", CommandSection.Graph, "Set the graph announcement verbosity to Terse for this vault.")`
(and Standard, Verbose), new `Ids` constants `slate.graph.setVerbosity{Terse,Standard,Verbose}`,
chordless. Delete `GraphVerbosityReason`. `GraphPreferencesViewModel`
gains three parameterless `ICommand`s (new) `SetVerbosityTerseCommand`,
`SetVerbosityStandardCommand`, `SetVerbosityVerboseCommand`, each
`new RelayCommand(_ => SetVerbosity(level), _ => true)` — tag-true like
mac; the Graph menu keeps `SetVerbosityCommand` and its check items
unchanged. `WorkspaceViewModel` exposes `internal GraphPreferencesViewModel GraphPreferences => _graphPreferences;`
(new) and the registrar maps the three ids to
`host => host.Workspace?.GraphPreferences.SetVerbosity…Command`. The
`VerbosityChanged` → `DropPendingNavigation` wiring (`WorkspaceViewModel.cs:2246`)
is untouched, so a palette-driven change drops a stale row line like a
menu-driven one. Regenerate `chords.json`; `deliveryEvidence.commands`
gains the three ids → `"graph"`. Drop the three
`ID_ISSUE_OVERRIDES` entries and regenerate `parity_matrix.md`.

**#1389, Windows half (RD6).** Remove the two `CheckMenuItem`s
(`MainWindow.xaml:544-556`) and the two keys from `CodeVerbosityNames`;
the menu keeps "Preamble Only" alone (or the submenu collapses to one
checked item — keep the submenu so WB-21b restores the items in place,
`§6`'s `MainWindow.xaml` ordering). The load path (`EditorInteractions.cs:131-135`)
already maps an unknown key to `preambleOnly` with no announcement —
that is the silent fallback; add a fact that pins it for a stored
`preambleFirstLine`. `AppPreferencesStore` and the `A11yEvent::CodePreambleVerbosity`
corpus entry stay (core and mac still speak it for the remaining mode;
plan 43 PR 52 decides the mac Settings picker). Not changed:
`CodeBlockPreamble`, `ReadingDocumentBuilder`, core, FFI, mac files.

## Tests

- `GraphPreferencesTests.TheThreeVerbosityRowsAreRegisteredChordlessWithMacsLabelsAndHints`
  replaces `TheDispositionsArePresentUnregisteredAndTagTrue`'s graph arm:
  for each core level, one `Reg` row with id `slate.graph.setVerbosity{Title}`,
  label `Graph Verbosity: {Title}`, mac's hint, `CommandSection.Graph`,
  no chord either side. Mutation: keep one `Unreg` row → fails.
- `GraphPreferencesTests.EachVerbosityRowSetsItsLevelAndReassertsOnTheSameOne`
  — execute each command: `Verbosity` changes, `VerbosityChanged` raised
  once; executing the current one re-asserts the choices and raises
  nothing. Mutation: wire two rows to the same level → fails.
- `CommandRegistrationTests.RegistryHoldsExactlyTheDeclaredCatalog_BothDirections`
  (`:165`) and `RegisteredCommandCarriesTheChordTablesHotkeySectionAndHint`
  (`:194`) cover the resolver and the hint without change; a hosted
  palette fact `CommandPaletteTests.TypingGraphVerbosityListsTheThreeRowsAndEnterSetsTheLevel`
  runs one through the shipped registry.
- `ChordTableTests.ChordsJson_IsExactlyTheTablesProjection` regenerated.
- `ReadingViewTests` `:3544-3552` rewritten as
  `W2EditorInteractionTests.AStoredFirstLineVerbosityFallsBackToPreambleOnlySilently`
  (store `preambleFirstLine`, construct: `IsCodeVerbosityPreambleOnly`
  true, nothing announced) and
  `ThePreambleVerbosityCommandRejectsTheRemovedKeys` (`Execute("preambleAllTokens")`
  changes nothing, announces nothing). `CheckItemGroupsTests.cs:114` and
  `CommandDriftTests.cs:349` updated to the one-item group.
- `ItemNameCensusTests` (`SlateWindows.AccessibilityTests`) and any census
  pinning the two menu AutomationIds lose them.

## Docs and registers

- `apps/slate-windows/chords.json` regenerated (`SLATE_CHORDS_UPDATE=1`).
- `scripts/generate-parity-matrix.py`: remove the three overrides;
  `python -I scripts/generate-parity-matrix.py` regenerates
  `parity_matrix.md` (the three rows leave "pending"); `--check` in CI.
- Contract 35: append a C-9 note ("the three levels are registered
  palette rows on both hosts since WB-21; the Graph menu's parameterised
  setter stands"). Contract 38 / designations: no new sentence.
- `w_c_matrix.md`: the Editor menu row's cell loses the two items (if
  it names them); the graph rows' automated-evidence cell cites the new
  facts.

## Verification

```powershell
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb21'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0 before reading counts
$env:SLATE_CHORDS_UPDATE='1'; dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter "FullyQualifiedName~ChordTableTests.ChordsJson_IsExactlyTheTablesProjection"; Remove-Item Env:SLATE_CHORDS_UPDATE
$filter = "FullyQualifiedName~GraphPreferencesTests|FullyQualifiedName~ChordTableTests|FullyQualifiedName~CommandRegistrationTests|FullyQualifiedName~CommandPaletteTests|FullyQualifiedName~W2EditorInteractionTests|FullyQualifiedName~CheckItemGroupsTests|FullyQualifiedName~CommandDriftTests|FullyQualifiedName~ReadingViewTests"
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --list-tests --filter $filter | Select-String "TheThreeVerbosityRowsAreRegisteredChordlessWithMacsLabelsAndHints|EachVerbosityRowSetsItsLevelAndReassertsOnTheSameOne|TypingGraphVerbosityListsTheThreeRowsAndEnterSetsTheLevel|AStoredFirstLineVerbosityFallsBackToPreambleOnlySilently|ThePreambleVerbosityCommandRejectsTheRemovedKeys" | Measure-Object | ForEach-Object { if ($_.Count -lt 5) { throw "new facts not discovered: $($_.Count)" } }
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter $filter
python -I scripts/generate-parity-matrix.py; python -I scripts/generate-parity-matrix.py --check
$files = @(
  'apps/slate-windows/src/SlateWindows/Commands/ChordTable.cs',
  'apps/slate-windows/src/SlateWindows/Commands/SlateCommandRegistrar.cs',
  'apps/slate-windows/src/SlateWindows/Graph/GraphPreferencesViewModel.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs',
  'apps/slate-windows/src/SlateWindows/EditorInteractions.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/GraphPreferencesTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/CommandPaletteTests.Shell.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/W2EditorInteractionTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/ReadingViewTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/CheckItemGroupsTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/CommandDriftTests.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files
```

(Every class in the filter is a real top-level class; `CommandPaletteTests`
is a partial class across `CommandPaletteTests*.cs`.)

CI: Windows lane (the parity `--check` runs there). AT: NVDA spot —
Ctrl+K, type "verbosity": three rows read "Graph Verbosity: Terse",
"… Standard", "… Verbose"; Enter on one speaks the graph verbosity line
the menu item speaks today; Editor ▸ Code Preamble Verbosity lists one
item; a preferences file holding `preambleFirstLine` launches silent.

## Acceptance

- [ ] Facts fail first, then pass; `chords.json` and `parity_matrix.md` regenerated, `--check` green.
- [ ] The three `windows.graph.setVerbosity*` rows and `GraphVerbosityReason` gone; the three `slate.graph.*` rows registered with mac's labels and hints.
- [ ] The two dead menu items and keys gone; stored non-default values fall back silently (fact).
- [ ] Contract 35 note appended; NVDA spot recorded.
- [ ] PR body: `Closes #1415`; `Refs #1389` with "the mac half is plan 43 PR 52; the issue closes when both are merged".

## Brief

Read `Commands/ChordTable.cs:617-690`, `:1077-1137`, `:860-872`,
`Commands/SlateCommandRegistrar.cs:413-560`, `Graph/GraphPreferencesViewModel.cs:100-182`,
`WorkspaceViewModel.cs:2240-2250`, `EditorInteractions.cs:43-70`,
`:125-140`, `:185-205`, `:370-400`, `MainWindow.xaml:537-556`,
`GraphPreferencesTests.cs:238-255`, `ReadingViewTests.cs:3540-3556`,
`CheckItemGroupsTests.cs:108-120`, `CommandDriftTests.cs:344-354`,
`ChordTableTests.cs:24-36` and `:233-270`, `scripts/generate-parity-matrix.py:90-110`
and mac's `SlateCommands.swift:1672-1696`. No owner decision is pending
(RD6 is recorded on #1389, 2026-10-19). Build: three `Reg` rows
`slate.graph.setVerbosityTerse|Standard|Verbose` (labels "Graph
Verbosity: Terse|Standard|Verbose", hints "Set the graph announcement
verbosity to Terse|Standard|Verbose for this vault.", `CommandSection.Graph`,
chordless) replacing the three `Unreg` rows and `GraphVerbosityReason`;
three parameterless `RelayCommand`s `SetVerbosityTerseCommand`,
`SetVerbosityStandardCommand`, `SetVerbosityVerboseCommand` on
`GraphPreferencesViewModel` calling `SetVerbosity(level)`; a new
`internal GraphPreferencesViewModel GraphPreferences => _graphPreferences;`
on `WorkspaceViewModel`; registrar entries
`[Ids.GraphSetVerbosity…] = host => host.Workspace?.GraphPreferences.SetVerbosity…Command`;
removal of the "Preamble + First Line" and "Preamble + All Tokens"
`CheckMenuItem`s (`MainWindow.xaml:544-556`) and of the two keys in
`CodeVerbosityNames`, the submenu kept. Facts, failing first:
`GraphPreferencesTests.TheThreeVerbosityRowsAreRegisteredChordlessWithMacsLabelsAndHints`
(one `Reg` row per core level, mac's label and hint, no chord either
side), `GraphPreferencesTests.EachVerbosityRowSetsItsLevelAndReassertsOnTheSameOne`
(`Verbosity` changes and `VerbosityChanged` fires once; the current level
re-asserts silently), `CommandPaletteTests.TypingGraphVerbosityListsTheThreeRowsAndEnterSetsTheLevel`
(hosted registry), `W2EditorInteractionTests.AStoredFirstLineVerbosityFallsBackToPreambleOnlySilently`
(a stored `preambleFirstLine` constructs as Preamble only with nothing
announced), `W2EditorInteractionTests.ThePreambleVerbosityCommandRejectsTheRemovedKeys`
(`Execute("preambleAllTokens")` changes and announces nothing);
`ReadingViewTests.cs:3544-3552`, `CheckItemGroupsTests.cs:114` and
`CommandDriftTests.cs:349` rewritten to the one-item group;
`ChordTableTests.ChordsJson_IsExactlyTheTablesProjection` regenerated
with `SLATE_CHORDS_UPDATE=1`. Mutations to run locally: keep one `Unreg`
row; wire two rows to one level; leave a removed key in
`CodeVerbosityNames`. Registers: `apps/slate-windows/chords.json`
(regenerated; `deliveryEvidence.commands` gains the three ids → `graph`),
`scripts/generate-parity-matrix.py` (drop the three overrides) and
`docs/plans/18_windows_port/parity_matrix.md` (regenerated, `--check`
green), a C-9 note appended to `docs/plans/35_graph_contracts.md`, the
`docs/plans/18_windows_port/w_c_matrix.md` cells for the Editor menu and
the graph rows. Done when the Windows lane is green, `dotnet format
--include` over the eleven files is clean, and the NVDA spot (Ctrl+K
"verbosity" lists the three rows and Enter speaks the level line;
Editor ▸ Code Preamble Verbosity lists one item; a stored
`preambleFirstLine` launches silent) is in the PR body. Not delivered:
the mac half of #1389 (plan 43 PR 52). PR body: `Closes #1415`,
`Refs #1389`. Commit: `feat(windows): the palette registers the three
graph verbosity rows and the code preamble menu drops the two modes that
did nothing`.
