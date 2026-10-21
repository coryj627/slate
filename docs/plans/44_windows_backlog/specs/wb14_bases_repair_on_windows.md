# WB-14 — A saved empty or incomplete `.base` can be repaired on Windows: it loads as failed with core's wording, stays editable, and its failed state is mac's group with an Edit button

Issues: Closes #1397. Size M. Lanes: windows. AT: NVDA (full pass). Wave: W5 (slot 2, after WB-13). Depends on: nothing in this plan. Parallel with: WB-10, WB-15. Decision: WD8 (pending; recommended "mac's group, recorded as C4's amendment"). Interlock: plan 43 PR 35 (#1398) touches the same failed state — WB-14 first and PR 35 rebases, or PR 35 first and WB-14 adopts its core copy (§6).

## Goal

A `.base` saved empty, filters-only, or mid-edit has no executable view.
Windows executes view 0 anyway, core refuses with "base view 0 is out of
range", the document goes Degraded under a banner that quotes the
refusal, Edit View Filters fails with the same words, and a Failed document
collapses its content area to nothing with no way to repair it. After this
chunk: a definition with no view loads as Failed with C4's own sentence
("No executable base views were found."), its definition stays editable,
Edit opens an empty draft, Save to View writes the draft as the
definition's first view through `BaseEdit::AddView`, and the failed state
renders mac's group (heading, core's message, the repair sentence or the
reason editing is unavailable, an Edit button). The six facts of mac's
`BaseContainerRecoveryTests.swift` gain their Windows twins.

## Current state on main

Verified in the worktree; the plan's and `report_a11y.md`'s lines hold.

- `Bases/BaseDocumentViewModel.cs:26-32` `BaseLoadState { Loading, Ready,
  Degraded, Failed }`. `LoadBody` (`:867`) reads views then
  `ExecuteBody(generation, (uint)ClampedViewIndex(views.Length), …)`
  (`:610`); `Refresh` does the same (`:950-954`); `ClampedViewIndex`
  (`:956-957`) returns 0 for an empty list, so view 0 is executed. Core
  refuses: `session.rs:19243-19247` (`base_query_json_for_view`, "base view
  {view} is out of range"), the same guard at `:18635`.
- The refusal lands in the execute catch (`:1095-1105`):
  `BasePhrase.ExecuteFailed(exception)` → `PublishDegraded` — so the
  `PublishResult` arm `summary is null → PublishDegraded(BasePhrase.NoExecutableViews)`
  (`:1179-1182`) is unreachable on this path. `PublishFailed` (`:1202-1212`)
  sets `State = Failed`, `Result = null`; the handle is not closed there.
  `BasePhrase` (`:1485-1504`): `NoExecutableViews = "No executable base
  views were found."` (`:1490`), `OpenFailed` (`:1497`), `ExecuteFailed`
  (`:1500`).
- `ViewEditQueryJson(viewIndex, …)` (`:509-553`) calls
  `_session.BaseViewEditQueryJson(handle, viewIndex)` (`:531-532`) — fails
  for zero views; `ApplyBuilderEdits` (`:555-600`) refuses `Failed` or
  `Loading` outright (`:557-563`, "the base is not ready to accept edits")
  and applies `_session.BaseApplyEdits(handle, batch)` (`:585`).
- `WorkspaceViewModel.Bases.cs:127-128` `BasesEditViewFiltersCommand =
  BasesCommand(document => …)` with `requireInteractive: true`
  (`:500-506`); `BasesDocumentInteractive` (`:514-515`) is Ready or
  Degraded — every Failed document is excluded, including one the C6
  rollback detach closed. The edit-JSON failure announces
  `BasesFiltersOpenFailed(failure)` (`:146-147`).
- **Saved queries reach `Refresh` too.** A saved-query document
  (`BaseDocumentViewModel.ForSavedQuery`, `:95-100`) is built with
  `Path = string.Empty` (`:76-89`) and opens an ephemeral query handle
  through `OpenSavedQuery` (`:886`); it has no definition file. It reaches
  `Refresh` (`:996-1003`) through `RefreshAsync` (`:970-981`, the
  non-Failed arm) from the rescan's Bases dependent, which re-runs every
  base document **and the Bases dock whatever its kind**
  (`WorkspaceViewModel.Rescan.cs:696-715`, the dock appended at
  `:702-704`), and directly from the dock's follow
  (`WorkspaceViewModel.Bases.cs:1119`), the Slate-owned change refresh
  (`:1815`, `:1826`) and the quick-filter clear
  (`WorkspaceViewModel.cs:1249`). A published failure is a terminal
  failure the rescan counts (`BaseDocumentViewModel.cs:809-823`).
- **How the palette speaks an unavailable command.**
  `CommandPaletteViewModel.Invoke` re-asks the source for a disabled reason
  and, when there is one, announces `PaletteCommandUnavailable(reason)`
  verbatim and returns without reaching the command
  (`CommandPaletteViewModel.cs:825-831`). The reason is
  `SlateCommandRegistrar.DisabledReason` (`Commands/SlateCommandRegistrar.cs:307-331`):
  for a command whose `CanExecute` is false it is
  `(command as IUnavailableReason)?.UnavailableReason ?? UnavailableReason`
  (`:330`; the generic "This command is not available right now.", `:174`).
  `ReasonedCommand(Action execute, Func<string?> unavailableReason)`
  (`Commands/ReasonedCommand.cs:29-57`) implements `IUnavailableReason`, is
  enabled exactly when its reason is null and re-checks in `Execute`
  (`FilesSidebarViewModel.cs:585` uses it). `BasesEditViewFiltersCommand`
  is a `RelayCommand` built by `BasesCommand`
  (`WorkspaceViewModel.Bases.cs:42`, `:127-128`, `:500-512`), so a disabled
  Edit View Filters would speak only the generic sentence. The workspace
  raises the Bases commands when a document's `State` changes
  (`InstallBaseDocumentSeams`, `:1464-1478`) through
  `RaiseBasesCommandStates` (`:704-722`, an array typed `RelayCommand?`);
  `SlateCommandRegistrar.RaiseCommandStates` raises a `ReasonedCommand`
  as well (`:344-363`). The Bases menu binds the same command
  (`MainWindow.xaml:433`).
- `Bases/BaseQueryBuilderViewModel.cs:780-888` `SaveToView`: emits
  `RemoveViewKey`/`SetViewFilters`/`SetFormula` on `Context.ViewIndex`
  (`:846-852`); no `AddView` — `BaseEdit.AddView` (core `bases/mod.rs:212-214`)
  has no Windows caller (grep: none).
- `Bases/BaseSurfaceView.cs:669-715` `RenderContent`: for `Failed` the
  grid and list collapse and `_emptyState` (a `TextBlock`, `:173-182`,
  AutomationId `BaseEmptyState`) is collapsed too (`:701-703`) — only the
  state banner remains, no heading, no action.
- Facts today: `BasesDocumentTests.cs:110`
  `MissingFileFailsWithTheRecoveryPosture` (Failed, "This Base could not
  be opened:" prefix), `:125` `EmptyResultShowsTheEmptyState`.
- Contract 25 C4 (`25_bases_grid_contracts.md:49-55`): the no-views
  wording is an informational banner ABOVE content. `w_c_matrix.md:30` is
  the Bases surfaces row.

## Reference

Mac (plan 43 PR 15, #1401 merged): `Bases/BaseContainerView.swift:696-745`
`BaseLoadFailure { message, editDisabledReason }` and
`BaseLoadFailedView` — `Text("This base could not be loaded")` with
`.isHeader`, `Text(failure.message)`, `Text(reason)` when editing is
unavailable else `Text("Edit the base definition to repair it.")`,
`Button("Edit")` disabled when the reason is non-nil, hint "Opens the base
definition in the query builder.", `.accessibilityElement(children: .contain)`.
`AppState+Bases.swift:228-247`: `baseDefinitionEditingDisabledReason` (nil
for a saved query; else the path reason or the definition-availability
reason) and `baseLoadFailure`. `:2121-2140` `baseDefinitionBuilder`: a
detached definition reopens first; no executable view → a NEW empty
builder model on view 0. `:2446-2454` `baseBuilderSaveEdits`: zero views →
`[.addView(yaml: "type: table")] + draft edits for view 0`. The six facts:
`BaseContainerRecoveryTests.swift:77, 98, 124, 152, 220, 301`.

WD8 recommendation (quoted): "Mac's group (heading, core message, repair
sentence, Edit), recorded as C4's amendment; Windows and mac then match."
If the owner keeps C4's banner instead, the Windows change stops at the
failed-load classification and the editing availability; the group is not
built and `NoBlankSurfaceForAFailedBase` asserts the banner carries the
message and the Edit command stays enabled.

## Design

**Document (`BaseDocumentViewModel`).**
- `LoadBody` and `Refresh`: when `views.Length == 0`,
  `PublishFailed(BasePhrase.NoExecutableViews)` and do not execute; the
  handle stays open (the definition was read). The `:1179-1182` arm stays
  for the summary-less execute.
- New `string? DefinitionEditingDisabledReason` (mirrors mac `:228-232`),
  computed from a new `_definitionAvailability` field — never from
  `_handle` alone, because a retained handle outlives the file: `Refresh`
  executes the retained handle (`:996-1003`) and a failed execute keeps it
  (`:1101-1104`), so a definition deleted after the open would otherwise
  stay "editable". Precedence, as mac's path-reason-then-availability
  order: (1) `IsSavedQuery` → null; (2) `State is Loading` or a rescan
  retarget in flight → `BasePhrase.DefinitionReopening`; (3)
  `_definitionAvailability is Unavailable(reason)` → that reason
  (`BasePhrase.DefinitionUnavailable`, mac's
  `baseDocumentUnavailableDisabledReason` text verbatim, for missing,
  unreadable or quarantined; `DefinitionRetargetFailed` for a failed
  reopen); (4) else null — the definition was read (handle present) or is
  readable again and reopens for the edit. `_definitionAvailability` is
  published at every terminal outcome: `Load`'s open failure
  (`BasePhrase.OpenFailed`, `:927`) sets `Unavailable`; a successful open
  or apply sets `Available`; and `Refresh` of a file-backed document
  (`!IsSavedQuery`) gains an availability probe BEFORE executing the
  retained handle — a core read of the definition
  (`_session.ReadNoteParts(Path)` through the FFI lock, the probe
  `TasksReviewViewModel` already uses at `:745`; new seam
  `internal Action? DefinitionReadFaultForTests { get; set; }`, invoked
  inside the probe just before the core read) — whose `VaultException`
  closes the handle (`CloseHandleLocked`, `:1444-1451`), publishes
  `PublishFailed(BasePhrase.OpenFailed(e))` and sets `Unavailable`. A
  saved query is never probed: it has no definition file (`Path` is
  `string.Empty`, `:76-89`), so a probe would read `ReadNoteParts("")`,
  fail, close a valid query handle and publish Failed — on every rescan
  (the Bases dependent re-runs the dock whatever its kind,
  `WorkspaceViewModel.Rescan.cs:702-704`) and every dock follow — and the
  rescan would count it as an error. Its `Refresh` executes its handle as
  today, and its `DefinitionEditingDisabledReason` stays null (precedence
  (1)). The rescan's reload loop (`WorkspaceViewModel.Rescan.cs:276-290`)
  only reloads hash-changed bases, so an explicit Refresh is the recovery
  route for a deleted file, as on mac (`basesRefresh()`); the PR body
  says so. Exposed with `OnPropertyChanged` on every publication.
- `ViewEditQueryJson`: if `_handle is null` and the definition file is
  present, reopen first (mac `:2126-2128`); if `Views.Length == 0`, hand
  the continuation the empty-draft JSON (what `BaseQueryBuilderViewModel`
  seeds a new query with — read its `_basesNewQueryCommand` seed before
  coding and share one helper) instead of calling core.
- `ApplyBuilderEdits`: the guard becomes `IsShutDown || IsSavedQuery ||
  DefinitionEditingDisabledReason is not null || State is Loading` — a
  Failed document with its definition read accepts edits; after
  `BaseApplyEdits` the existing reload (`:586-590`) runs, so a repaired
  definition goes Ready.

**Builder.** `SaveToView`: when `target.Views.Length == 0`, prepend
`new BaseEdit.AddView("type: table")` and address every edit to view 0
(mac `:2454`); the announcement stays `BasesBuilderSaved`.

**Workspace.** `BasesEditViewFiltersCommand` becomes a `ReasonedCommand`
(the field at `WorkspaceViewModel.Bases.cs:42` is retyped
`ReasonedCommand?`): `new ReasonedCommand(EditViewFiltersBody,
EditViewFiltersUnavailableReason)`, where new
`private void EditViewFiltersBody()` is today's body (`:128-166`) over
`ActiveBaseDocument`, and new `private string? EditViewFiltersUnavailableReason()`
is `ActiveBaseDocument switch { null => SlateCommandRegistrar.UnavailableReason,
{ IsSavedQuery: true } document => BasesDocumentInteractive(document) ? null
: SlateCommandRegistrar.UnavailableReason, { } document =>
document.DefinitionEditingDisabledReason }` — a saved query keeps today's
admission (Ready or Degraded; it has no definition to repair, and its
reason is null by precedence (1), so the definition predicate would
enable Edit on a saved query whose open failed and has no handle). A
file-backed document's command is enabled exactly when its definition is
editable — a Failed document whose definition was read included — and
disabled with the reason otherwise; the button and the
Bases menu item (`MainWindow.xaml:433`) bind the command, so both are
disabled. The reason is never spoken by the command's body: a disabled
command's body does not run (`ReasonedCommand.Execute` re-checks, and the
palette stops before invoking, `CommandPaletteViewModel.cs:825-831`).
It reaches the reader instead through `IUnavailableReason`: the palette's
one resolver returns it (`SlateCommandRegistrar.cs:330`), the palette row
shows it and Enter on the row announces `PaletteCommandUnavailable(reason)`
verbatim — the definition-specific sentence, as mac speaks it, not the
generic "This command is not available right now." — and the failed
group's own text carries it in place of the repair sentence (Surface).
The body's existing `BasesFiltersOpenFailed(failure)` stays for a real
edit-JSON fetch failure (`:146-147`). Requery: `InstallBaseDocumentSeams`
(`:1464-1478`) raises `RaiseBasesCommandStates()` on
`nameof(BaseDocumentViewModel.DefinitionEditingDisabledReason)` as it
does on `State`, and `RaiseBasesCommandStates` (`:704-722`, whose array
is typed `RelayCommand?`) also calls
`_basesEditViewFiltersCommand?.RaiseCanExecuteChanged()`. Data commands
keep `BasesCommand` and `BasesDocumentInteractive`.

**Surface (`BaseSurfaceView`, WD8 as recommended).** A `BaseLoadFailedGroup`
(StackPanel, AutomationId, `AutomationProperties.Name` "This base could
not be loaded", a landmark group): heading `TextBlock` (HeadingLevel 2),
`StateMessage`, then `DefinitionEditingDisabledReason` if non-null else
"Edit the base definition to repair it.", and an `Edit` `Button` bound to
`BasesEditViewFiltersCommand` with HelpText "Opens the base definition in
the query builder.", enabled exactly when the command's `CanExecute` is
true — that is, when the reason is null — so the button, the text above
it and the palette's row cannot disagree. `RenderContent` shows it
for `Failed` instead of collapsing everything (`:701-703`), keeps the
`Loading`-under-the-keys rule (`:688-693`), and leaves Ready/Degraded as
they are. Strings go into `BasePhrase` as constants, mac's verbatim —
host literals on both hosts until plan 43 PR 35 (#1398) moves failed-state
copy into core; the PR body names them for PR 35.

Not changed: the Degraded banners, `BasesDocumentInteractive` for data
verbs, the C6 rollback detach itself.

## Tests

Facts fail first; twin names of the six mac facts, in `BasesDocumentTests`
(document-level) and a hosted `BasesSurfaceRecoveryTests` (STA):

- `ASavedEmptyBaseOpensInARecoverableStateWithEditingEnabled` — `""` →
  `State == Failed`, `StateMessage == BasePhrase.NoExecutableViews`,
  handle present, `DefinitionEditingDisabledReason == null`,
  `BasesEditViewFiltersCommand.CanExecute` true. Mutation: restore the
  `ClampedViewIndex` execute → message quotes "out of range" → fails.
- `AnIncompleteDefinitionShowsTheErrorAndKeepsEditingEnabled` —
  filters-only and the truncated `views:` list: same message, no "YAML"
  in it, editing enabled.
- `AnUnloadableSelectedViewExplainsItselfAndKeepsEditingEnabled` — an
  execute fault on view 1 (`ExecuteFaultForTests`): the message is core's
  detail (no `@message=`), handle kept, editing enabled, the view picker
  usable.
- `EditFromTheErrorStateOpensTheBuilder` — Edit on the empty base opens
  the builder on view 0; Save to View emits `AddView` first (assert the
  edit batch through a `BaseApplyEditsHookForTests`), the file gains
  `views:`, `Views.Length == 1`, `State == Ready`; and a definition
  detached by the C6 rollback reopens for the edit while data verbs stay
  disabled. Mutation: drop the `AddView` → core refuses view 0 → fails.
- `AMissingBaseFileStillDisablesEditingWithTheUnavailableReason` — the
  document is Ready with its handle RETAINED, the file is deleted on disk,
  then `Refresh`: the availability probe fails, the handle closes, the
  document is Failed with `BasePhrase.OpenFailed`'s sentence, the reason
  is non-null, `BasesEditViewFiltersCommand.CanExecute(null)` is false,
  `((IUnavailableReason)workspace.BasesEditViewFiltersCommand).UnavailableReason`
  equals the reason and so does
  `SlateCommandRegistrar.DisabledReason(host, ChordTable.Ids.BasesEditViewFilters)`
  over `new CanvasDocumentTests.CanvasCommandHost(workspace)`
  (`CanvasDocumentTests.cs:2870-2889`, the workspace-backed command host);
  `Execute(null)` opens no builder (`BaseQueryBuilderSheet` stays null)
  and announces nothing; the same with the file made unreadable
  (`DefinitionReadFaultForTests` throwing a read `VaultException`); a
  detached-then-deleted definition's reopen fails with its own sentence; a
  failed reopen keeps the retarget reason. Mutation: compute the reason
  from `_handle is null` → the deleted-with-retained-handle case stays
  "editable" → fails.
- `ADetachedDefinitionReopensForTheEditWhenItsFileIsReadable` — the C6
  rollback detach closed the handle; the file is intact: the reason is
  null, Edit reopens the definition first (handle present again) and the
  builder opens on view 0. Mutation: treat a null handle as unavailable
  → fails.
- `CommandPaletteTests.TheEditViewFiltersRowSpeaksTheDefinitionReasonAndNeverRunsTheCommand`
  (new, in new file `tests/SlateWindows.Tests/CommandPaletteTests.Bases.cs`,
  partial class `CommandPaletteTests`, so its private `InlineWorkLane`,
  `CommandPaletteTests.cs:1127`, makes `Open()`'s snapshot synchronous — the
  real-source shape of `AnUnpersistedRecentsWriteIsLoggedThroughTheRealStore`,
  `CommandPaletteTests.Lane.cs:712-747`). A fixture vault with a
  one-view `Notes.base`, its session and a workspace
  (`startInteractionBackgroundWork: false`); the base opened and Ready;
  the file deleted on disk; `Refresh` → Failed with the unavailable
  reason. The host is new, test-only,
  `private sealed class WorkspaceCommandHost(WorkspaceViewModel workspace) : ISlateCommandHost`
  in the same file: `Workspace` is the workspace, `FileSidebar` and
  `QuickSwitcher` null, `IsVaultOpen` true, and `OpenVaultCommand`,
  `CloseVaultCommand`, `ToggleSearchCommand` inert
  `new RelayCommand(_ => { }, _ => true)` — not
  `CanvasDocumentTests.CanvasCommandHost`, whose three vault commands throw
  `NotSupportedException` (`CanvasDocumentTests.cs:2881-2888`), because
  `Open()` asks every listed row's reason
  (`CommandPaletteViewModel.cs:1356`), which resolves those commands too.
  Then the real palette:
  `using var source = new PaletteCommandSource(new WorkspaceCommandHost(workspace), Dispatcher.CurrentDispatcher, new CommandPaletteRecentsStore(Path.Combine(fixture.Root + "-device", "command-palette-recents.json")))`
  (an explicit store, as the lane fact passes one, so nothing reads or
  writes the profile's recents) and
  `new CommandPaletteViewModel(source, announced.Add, lane: new InlineWorkLane())`;
  `Open()`; the row whose `Id` is `ChordTable.Ids.BasesEditViewFilters`
  is `IsUnavailable` and shows the reason; `Invoke(row)` announces exactly
  one `A11yEvent.PaletteCommandUnavailable` whose `Reason` equals
  `document.DefinitionEditingDisabledReason` (the definition's sentence,
  not `SlateCommandRegistrar.UnavailableReason`), the palette stays open,
  `workspace.BaseQueryBuilderSheet` stays null and the workspace announced
  no `BasesFiltersOpenFailed`. Mutation: leave the command a `RelayCommand`
  from `BasesCommand` with the same predicate (no `IUnavailableReason`) →
  the palette speaks "This command is not available right now." → fails.
- `BasesQueriesTests.ASavedQueryRefreshSkipsTheDefinitionProbeAndKeepsItsHandle`
  (new, in the class's saved-query fixture, `BasesQueriesTests.cs:21-50`)
  — `RunSavedQuery` opens "All notes" Ready with its three notes;
  `document.OpensForTests` is 1; arm
  `document.DefinitionReadFaultForTests = () => throw new VaultException.Io("probe")`
  (a probe that ran would fail); `document.Refresh()` — the entry the
  dock's follow, the Slate-owned change refresh and the quick-filter clear
  take — leaves `State == Ready`, the same rows, `OpensForTests` still 1
  (the query handle neither closed nor reopened),
  `DefinitionEditingDisabledReason` null and
  `workspace.BasesEditViewFiltersCommand.CanExecute(null)` true (a Ready
  saved query keeps today's admission). Mutation: probe saved queries
  too (drop `!IsSavedQuery`) → the armed probe throws, the handle closes,
  the document is Failed → fails.
- `RescanTests.ASavedQueryInTheBasesDockIsReRunByARescanWithItsHandleKept`
  (new, in `RescanTests.Round6.cs` beside the view-less base facts, class
  `RescanTests`) — a `Harness` vault with `Notes.base` (`file.ext == "md"`,
  one table view), `a.md` and `b.md`; a saved query "All notes" made from
  the base's view 0 (`OpenBase`, `BaseViewQueryJson`, `CloseBase`,
  `SaveQuery` on `h.Lifecycle.SessionForTests`, as
  `BasesQueriesTests.cs:35-49` does); `h.Workspace.DockSavedQueryToSidebar(id, "All notes")`
  and pump until the dock document (`IsSavedQuery`) is Ready; arm its
  `DefinitionReadFaultForTests` to throw; write `c.md` on disk (so the run
  has a change and its Bases dependent runs, before and after WB-10, which
  re-runs Bases on any record); `RescanAsync(RescanReason.Explicit)` to
  completion: the single event is `A11yEvent.VaultRescanFinished` (not
  `VaultRescanIncomplete`), the dock is Ready with `c.md` among its rows
  (its handle executed again) and `OpensForTests` is 1. Mutation: drop
  `!IsSavedQuery` → the dock's probe throws, its handle closes, it
  publishes Failed and the run is `VaultRescanIncomplete` with one error
  → fails.
- `NoBlankSurfaceForAFailedBase` (hosted) — for empty, missing and
  unloadable documents the surface shows `BaseLoadFailedGroup` with the
  heading, a non-empty message, the sentence or the reason, and the Edit
  button (enabled/disabled accordingly); `BaseEmptyState` is not the
  visible element; a loading document shows the loading line. Mutation:
  collapse the group for Failed (today's `:701`) → fails.
- `BasesDocumentTests.MissingFileFailsWithTheRecoveryPosture` (`:110`)
  gains the reason assertion.
- `CommandDriftTests` / `MenuBarCensus` stay green (a new button's command
  is an existing one).

## Docs and registers

- Contract 25: append a C4 amendment row (WD8): "a definition with no
  executable view loads as Failed; the failed content area is a group —
  heading, core's message, repair sentence or unavailability reason, Edit
  — on both hosts (mac #1401, Windows #1397)". Never rewrite the frozen
  C4 text.
- `w_c_matrix.md:30`: the Bases row gains the failed-state group and its
  AutomationIds.
- `parity_matrix.md`: the `.base` recovery row → implemented both hosts.
- If PR 35 (#1398) lands first: adopt its core failed-state copy in place
  of the `BasePhrase` constants and say so.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb14'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
function Assert-Discovered([string[]] $Listed, [string[]] $Required) {
  $names = $Listed | ForEach-Object { ($_ -split '\(', 2)[0] }
  $missing = @($Required | Where-Object { $names -notcontains $_ })
  if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
}
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
# Whole classes for the Bases surfaces; method names for the facts that live in large partial classes
# (CommandPaletteTests, RescanTests), including the three view-less-base rescan facts on main this chunk's
# Failed classification must keep green (RescanTests.Round6.cs:36-91).
$filter = 'FullyQualifiedName~SlateWindows.Tests.BasesDocumentTests|FullyQualifiedName~SlateWindows.Tests.BasesSurfaceRecoveryTests|FullyQualifiedName~SlateWindows.Tests.BasesQueriesTests|FullyQualifiedName~SlateWindows.Tests.BasesBuilderTests|FullyQualifiedName~SlateWindows.Tests.ShellBasesLandingTests|FullyQualifiedName~SlateWindows.Tests.CommandDriftTests|FullyQualifiedName~SlateWindows.Tests.Censuses.MenuBarCensus|FullyQualifiedName~SlateWindows.Tests.CommandPaletteTests.TheEditViewFiltersRowSpeaksTheDefinitionReasonAndNeverRunsTheCommand|FullyQualifiedName~SlateWindows.Tests.RescanTests.ASavedQueryInTheBasesDockIsReRunByARescanWithItsHandleKept|FullyQualifiedName~SlateWindows.Tests.RescanTests.AViewlessBaseIsCountedAndEveryRescanEnds|FullyQualifiedName~SlateWindows.Tests.RescanTests.TwoViewlessBasesAreTwoErrors|FullyQualifiedName~SlateWindows.Tests.RescanTests.ACaseOnlyReseatedViewlessBaseIsCounted'
$listed = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { $_.Line.Trim() }); Assert-Ok 'unit discovery'
Assert-Discovered $listed @(
  'SlateWindows.Tests.BasesDocumentTests.ASavedEmptyBaseOpensInARecoverableStateWithEditingEnabled',
  'SlateWindows.Tests.BasesDocumentTests.AnIncompleteDefinitionShowsTheErrorAndKeepsEditingEnabled',
  'SlateWindows.Tests.BasesDocumentTests.AnUnloadableSelectedViewExplainsItselfAndKeepsEditingEnabled',
  'SlateWindows.Tests.BasesDocumentTests.EditFromTheErrorStateOpensTheBuilder',
  'SlateWindows.Tests.BasesDocumentTests.AMissingBaseFileStillDisablesEditingWithTheUnavailableReason',
  'SlateWindows.Tests.BasesDocumentTests.ADetachedDefinitionReopensForTheEditWhenItsFileIsReadable',
  'SlateWindows.Tests.BasesDocumentTests.MissingFileFailsWithTheRecoveryPosture',
  'SlateWindows.Tests.BasesSurfaceRecoveryTests.NoBlankSurfaceForAFailedBase',
  'SlateWindows.Tests.CommandPaletteTests.TheEditViewFiltersRowSpeaksTheDefinitionReasonAndNeverRunsTheCommand',
  'SlateWindows.Tests.BasesQueriesTests.ASavedQueryRefreshSkipsTheDefinitionProbeAndKeepsItsHandle',
  'SlateWindows.Tests.RescanTests.ASavedQueryInTheBasesDockIsReRunByARescanWithItsHandleKept',
  'SlateWindows.Tests.RescanTests.AViewlessBaseIsCountedAndEveryRescanEnds',
  'SlateWindows.Tests.RescanTests.TwoViewlessBasesAreTwoErrors',
  'SlateWindows.Tests.RescanTests.ACaseOnlyReseatedViewlessBaseIsCounted')
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'WB-14 facts'
# Mutations, each rebuilt and run against the same filter, each required to exit nonzero naming its fact, then reverted:
# the ClampedViewIndex execute restored; AddView dropped; the reason from _handle is null; a null handle treated as
# unavailable; the group collapsed for Failed; !IsSavedQuery dropped from the probe (both saved-query facts);
# BasesEditViewFiltersCommand left a RelayCommand from BasesCommand (the palette fact).
$cs = @(git diff --name-only origin/main -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')   # Bases/BaseDocumentViewModel.cs, Bases/BaseSurfaceView.cs, Bases/BaseQueryBuilderViewModel.cs, WorkspaceViewModel.Bases.cs, BasesDocumentTests.cs, BasesSurfaceRecoveryTests.cs (new), BasesQueriesTests.cs, CommandPaletteTests.Bases.cs (new), RescanTests.Round6.cs
if ($cs.Count -eq 0) { throw 'no C# change found' }
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
```

CI: Windows lane. AT: NVDA full pass (§4 rule 4, the PR stays a draft
until recorded): open an empty `.base` → the tab lands on "This base could
not be loaded, heading level 2", then "No executable base views were
found.", "Edit the base definition to repair it.", "Edit, button, Opens the
base definition in the query builder."; Enter → "Editing filters for
<name>."; Save to View → "Saved builder changes to view." and the grid
lands; a deleted `.base`, then the Bases refresh or a vault Refresh →
the reason replaces the sentence and the button reads "unavailable"; the
command palette (Ctrl+Shift+P) on that tab lists Edit View Filters with
the reason, and Enter on the row speaks the reason once — the definition's
own sentence, never "This command is not available right now." — and
opens no builder; a saved query docked in the sidebar keeps its rows and
says nothing through a vault Refresh.

## Acceptance

- [ ] Six twin facts (plus the amended `:110`) and the three new facts — `CommandPaletteTests.TheEditViewFiltersRowSpeaksTheDefinitionReasonAndNeverRunsTheCommand`, `BasesQueriesTests.ASavedQueryRefreshSkipsTheDefinitionProbeAndKeepsItsHandle`, `RescanTests.ASavedQueryInTheBasesDockIsReRunByARescanWithItsHandleKept` — pass, seen failing first; mutations run; the three view-less-base rescan facts on `main` stay green.
- [ ] The definition probe runs only for a file-backed document (`!IsSavedQuery`); a saved query's `Refresh` and the rescan's re-run of a docked saved query keep its handle and rows.
- [ ] `BasesEditViewFiltersCommand` is a `ReasonedCommand`: disabled with the definition's reason, which the palette speaks as `PaletteCommandUnavailable(reason)`; its body never runs while a reason stands.
- [ ] C4 amendment appended; `w_c_matrix.md:30` and the parity row updated.
- [ ] Windows lane green; the verification block ran clean with `$LASTEXITCODE` checked (its discovery throws on any of the fourteen named facts missing); format gate clean.
- [ ] NVDA pass recorded in the PR body (the phrases above).
- [ ] PR body: `Closes #1397`; names the three `BasePhrase` literals for PR 35. Not delivered: core-owned failed-state copy (PR 35's).

## Brief

Read `Bases/BaseDocumentViewModel.cs:26-32, 509-600, 604-612, 867-930,
944-1004, 1095-1110, 1172-1212, 1444-1451, 1485-1504`,
`WorkspaceViewModel.Bases.cs:42, 127-166, 486-520, 704-722, 1119, 1464-1478, 1800-1830`,
`WorkspaceViewModel.Rescan.cs:276-290, 696-715` (the Bases dependent re-runs
the dock whatever its kind), `Bases/BaseDocumentViewModel.cs:76-100` (a saved
query's `Path` is empty) and `:792-823`, `:970-1003` (`RefreshAsync`,
`Refresh`), `CommandPaletteViewModel.cs:812-831` (an unavailable row is
announced and never invoked), `Commands/SlateCommandRegistrar.cs:174, 307-331, 344-363`,
`Commands/ReasonedCommand.cs:15-57`, `MainWindow.xaml:433`,
`tests/SlateWindows.Tests/CommandPaletteTests.Lane.cs:712-747` and
`CommandPaletteTests.cs:1127-1147` (the real-source palette shape and the
inline lane), `CanvasDocumentTests.cs:2870-2889` (the workspace-backed
command host), `BasesQueriesTests.cs:21-60` (the saved-query fixture),
`RescanTests.Round6.cs:20-180` (the view-less-base rescan facts),
`Bases/BaseQueryBuilderViewModel.cs:780-892`,
`Bases/BaseSurfaceView.cs:170-185, 665-715`, core `bases/mod.rs:207-222`
and `session.rs:19240-19250`, mac `Bases/BaseContainerView.swift:696-745`,
`Bases/AppState+Bases.swift:228-247, 2118-2140, 2440-2454`, and the six
facts in `BaseContainerRecoveryTests.swift:77-385`. Pending decision WD8
(recommended: mac's failed-state group recorded as a C4 amendment; under
the banner answer, no group is built — the chunk stops at the Failed
classification, the availability predicate and the editing paths, and
`NoBlankSurfaceForAFailedBase` asserts the banner carries the message with
Edit enabled). Build: in `BaseDocumentViewModel`, `LoadBody`/`Refresh`
call `PublishFailed(BasePhrase.NoExecutableViews)` for zero views with the
handle kept; a `_definitionAvailability` field (`Available`,
`Unavailable(reason)`, `Reopening`, `RetargetFailed`) published at every
terminal outcome and, for a file-backed document only (`!IsSavedQuery`:
a saved query has no definition file, its `Path` is empty, and the rescan
re-runs a docked saved query through `RefreshAsync`), probed in `Refresh`
by a core read of the definition (`ReadNoteParts(Path)`) before the
retained handle executes (new seam
`internal Action? DefinitionReadFaultForTests { get; set; }`, invoked just
before the read; a failed probe closes the handle and publishes
`OpenFailed`); a saved query's `Refresh` executes its handle as today;
`DefinitionEditingDisabledReason` derived from it in mac's precedence
(saved query → null; loading/reopening; unavailable or retarget-failed
reason; else null), never from `_handle` alone; `ViewEditQueryJson`
reopens a detached readable definition and seeds the empty-draft JSON for
zero views; `ApplyBuilderEdits` guards on that reason instead of
`Failed`. In `BaseQueryBuilderViewModel.SaveToView`, prepend
`new BaseEdit.AddView("type: table")` and address view 0 when the target
has no views. In `WorkspaceViewModel.Bases.cs`, `BasesEditViewFiltersCommand`
becomes `new ReasonedCommand(EditViewFiltersBody,
EditViewFiltersUnavailableReason)` (field retyped `ReasonedCommand?`; new
`private void EditViewFiltersBody()` is today's body; new
`private string? EditViewFiltersUnavailableReason()` is the generic
`SlateCommandRegistrar.UnavailableReason` with no active base, today's
admission for a saved query — null when Ready or Degraded, else the
generic reason — and `DefinitionEditingDisabledReason` for a file-backed
document): enabled exactly when that reason is null, disabled with it
otherwise, so its `IUnavailableReason` is what the palette's resolver
returns and the palette announces `PaletteCommandUnavailable(reason)` —
the body, which never runs under a reason, does not speak it;
`InstallBaseDocumentSeams` raises `RaiseBasesCommandStates()` on
`DefinitionEditingDisabledReason` as on `State`, and
`RaiseBasesCommandStates` also raises the reasoned command. In `BaseSurfaceView.RenderContent`, a `BaseLoadFailedGroup`
(heading "This base could not be loaded" at level 2, `StateMessage`, the
reason or "Edit the base definition to repair it.", an Edit button bound
to the command with HelpText "Opens the base definition in the query
builder.", enabled exactly when the command can execute) replaces the
collapse for Failed; the three strings are
`BasePhrase` constants named in the PR body for plan 43 PR 35. Facts,
written first and seen failing, in `BasesDocumentTests` and a new hosted
`BasesSurfaceRecoveryTests`: `ASavedEmptyBaseOpensInARecoverableStateWithEditingEnabled`
(`""` → Failed, `NoExecutableViews`, handle kept, reason null, command
enabled); `AnIncompleteDefinitionShowsTheErrorAndKeepsEditingEnabled`
(filters-only and truncated `views:`: same message, no "YAML", editable);
`AnUnloadableSelectedViewExplainsItselfAndKeepsEditingEnabled` (execute
fault on view 1: core's detail, handle kept, editable, picker usable);
`EditFromTheErrorStateOpensTheBuilder` (builder on view 0; Save to View
emits `AddView` first, file gains `views:`, one view, Ready; a
C6-detached definition reopens for the edit with data verbs disabled);
`AMissingBaseFileStillDisablesEditingWithTheUnavailableReason` (deleted
while the handle is retained, then Refresh → Failed with `OpenFailed`,
reason non-null, the command disabled with that reason as its
`IUnavailableReason` and as `SlateCommandRegistrar.DisabledReason`'s
answer, `Execute` opening nothing; unreadable file the same; detached-then-
deleted reopen says why; failed reopen keeps the retarget reason);
`ADetachedDefinitionReopensForTheEditWhenItsFileIsReadable` (null handle,
intact file → reason null, reopen, builder on view 0);
`NoBlankSurfaceForAFailedBase` (hosted: empty, missing and unloadable
documents show the group with heading, message, sentence-or-reason and the
Edit button; loading shows the loading line); `MissingFileFailsWithTheRecoveryPosture`
(`:110`) gains the reason assertion; new
`CommandPaletteTests.TheEditViewFiltersRowSpeaksTheDefinitionReasonAndNeverRunsTheCommand`
(new file `CommandPaletteTests.Bases.cs`: a deleted, refreshed `.base`;
the real `PaletteCommandSource` over a new test-only
`WorkspaceCommandHost(workspace)` — `ISlateCommandHost` with the
workspace, `IsVaultOpen` true and inert vault commands, since
`CanvasDocumentTests.CanvasCommandHost`'s throw and `Open()` resolves
every listed row — and a `CommandPaletteViewModel` on the class's
`InlineWorkLane`; the Edit View
Filters row is unavailable, `Invoke` announces exactly one
`PaletteCommandUnavailable` whose reason is the document's
`DefinitionEditingDisabledReason`, the palette stays open and no builder
opens); new `BasesQueriesTests.ASavedQueryRefreshSkipsTheDefinitionProbeAndKeepsItsHandle`
(a saved query with its probe seam armed to throw: `Refresh` leaves it
Ready, same rows, `OpensForTests` 1, reason null, Edit View Filters still
enabled); new
`RescanTests.ASavedQueryInTheBasesDockIsReRunByARescanWithItsHandleKept`
(in `RescanTests.Round6.cs`: a saved query docked in the sidebar, its
probe seam armed to throw, `c.md` written, an explicit rescan: one
`VaultRescanFinished`, the dock Ready with `c.md` in its rows,
`OpensForTests` 1); the view-less-base rescan facts
(`AViewlessBaseIsCountedAndEveryRescanEnds`, `TwoViewlessBasesAreTwoErrors`,
`ACaseOnlyReseatedViewlessBaseIsCounted`) stay green. Mutations: the
`ClampedViewIndex` execute back; drop `AddView`; reason from
`_handle is null`; a null handle treated as unavailable; collapse the
group for Failed; drop `!IsSavedQuery` from the probe (both saved-query
facts fail); leave the command a `RelayCommand` from `BasesCommand` (the
palette speaks the generic sentence; the palette fact fails). Registers: append a
C4 amendment row to `docs/plans/25_bases_grid_contracts.md` (after `:55`,
never rewriting C4); amend `docs/plans/18_windows_port/w_c_matrix.md:30`
(the group and its AutomationIds); set the `.base` recovery row in
`docs/plans/18_windows_port/parity_matrix.md` to implemented on both
hosts. Interlock: if plan 43 PR 35 (#1398) landed first, adopt its core
failed-state copy instead of the `BasePhrase` constants. Not delivered:
core-owned failed-state copy (PR 35's); rescan-driven detection of a
deleted base (an explicit Refresh is the recovery route, as on mac). Done
when, in PowerShell with `$LASTEXITCODE` checked after every command, the
discovery over the Bases classes plus the named `CommandPaletteTests` and
`RescanTests` facts lists all fourteen facts (the six twins,
`MissingFileFailsWithTheRecoveryPosture`, `NoBlankSurfaceForAFailedBase`,
the three new facts and the three view-less-base rescan facts; it throws
on any miss) and they pass, the Windows lane is green, the format gate is
clean, the mutation run is recorded, and the NVDA pass ("This base could not be loaded, heading
level 2" → "No executable base views were found." → "Edit the base
definition to repair it." → "Edit, button, Opens the base definition in
the query builder."; Enter → "Editing filters for <name>."; Save to View →
"Saved builder changes to view."; a deleted base → the reason replaces the
sentence and Edit is unavailable; the palette's Edit View Filters row
speaks the definition's reason once and opens nothing; a docked saved
query keeps its rows through a vault Refresh) is in the PR body with
`Closes #1397`.
Commit: `fix(windows): a base with no executable view loads as failed,
stays editable, and shows the repair group`.
