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
  or apply sets `Available`; and `Refresh` gains an availability probe
  BEFORE executing the retained handle — a core read of the definition
  (`_session.ReadNoteParts(Path)` through the FFI lock, the probe
  `TasksReviewViewModel` already uses at `:745`; a `DefinitionReadFaultForTests`
  seam) — whose `VaultException` closes the handle (`CloseHandleLocked`,
  `:1444-1451`), publishes `PublishFailed(BasePhrase.OpenFailed(e))` and
  sets `Unavailable`. The rescan's reload loop (`WorkspaceViewModel.Rescan.cs:276-290`)
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

**Workspace.** `BasesEditViewFiltersCommand` uses a definition predicate:
enabled when `document.DefinitionEditingDisabledReason is null`; when the
document is Failed with a non-null reason the command announces
`BasesFiltersOpenFailed(reason)` (mac speaks the reason). Data commands
keep `BasesDocumentInteractive`.

**Surface (`BaseSurfaceView`, WD8 as recommended).** A `BaseLoadFailedGroup`
(StackPanel, AutomationId, `AutomationProperties.Name` "This base could
not be loaded", a landmark group): heading `TextBlock` (HeadingLevel 2),
`StateMessage`, then `DefinitionEditingDisabledReason` if non-null else
"Edit the base definition to repair it.", and an `Edit` `Button` bound to
`BasesEditViewFiltersCommand` with HelpText "Opens the base definition in
the query builder.", `IsEnabled` from the reason. `RenderContent` shows it
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
  is non-null, Edit refuses with `BasesFiltersOpenFailed(reason)`; the same
  with the file made unreadable (`DefinitionReadFaultForTests` throwing a
  read `VaultException`); a detached-then-deleted definition's reopen
  fails with its own sentence; a failed reopen keeps the retarget reason.
  Mutation: compute the reason from `_handle is null` → the
  deleted-with-retained-handle case stays "editable" → fails.
- `ADetachedDefinitionReopensForTheEditWhenItsFileIsReadable` — the C6
  rollback detach closed the handle; the file is intact: the reason is
  null, Edit reopens the definition first (handle present again) and the
  builder opens on view 0. Mutation: treat a null handle as unavailable
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
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb14'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.BasesDocumentTests|FullyQualifiedName~SlateWindows.Tests.BasesSurfaceRecoveryTests|FullyQualifiedName~SlateWindows.Tests.BasesBuilderTests|FullyQualifiedName~SlateWindows.Tests.ShellBasesLandingTests|FullyQualifiedName~SlateWindows.Tests.CommandDriftTests|FullyQualifiedName~SlateWindows.Tests.Censuses.MenuBarCensus'
dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String 'ASavedEmptyBaseOpensInARecoverableStateWithEditingEnabled|AnIncompleteDefinitionShowsTheErrorAndKeepsEditingEnabled|AnUnloadableSelectedViewExplainsItselfAndKeepsEditingEnabled|EditFromTheErrorStateOpensTheBuilder|AMissingBaseFileStillDisablesEditingWithTheUnavailableReason|ADetachedDefinitionReopensForTheEditWhenItsFileIsReadable|NoBlankSurfaceForAFailedBase'   # seven lines, else the filter is wrong
dotnet test $tests --no-build --configuration Release --filter $filter
$cs = @(git diff --name-only origin/main -- '*.cs') + @(git ls-files --others --exclude-standard -- '*.cs')   # Bases/BaseDocumentViewModel.cs, Bases/BaseSurfaceView.cs, Bases/BaseQueryBuilderViewModel.cs, WorkspaceViewModel.Bases.cs, BasesDocumentTests.cs, BasesSurfaceRecoveryTests.cs (new)
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
```

CI: Windows lane. AT: NVDA full pass (§4 rule 4, the PR stays a draft
until recorded): open an empty `.base` → the tab lands on "This base could
not be loaded, heading level 2", then "No executable base views were
found.", "Edit the base definition to repair it.", "Edit, button, Opens the
base definition in the query builder."; Enter → "Editing filters for
<name>."; Save to View → "Saved builder changes to view." and the grid
lands; a deleted `.base` → the reason replaces the sentence and the button
reads "unavailable"; Edit View Filters from the palette on it → the
reason is spoken once.

## Acceptance

- [ ] Six twin facts (plus the amended `:110`) pass, seen failing first; mutations run.
- [ ] C4 amendment appended; `w_c_matrix.md:30` and the parity row updated.
- [ ] Windows lane green; format gate clean.
- [ ] NVDA pass recorded in the PR body (the phrases above).
- [ ] PR body: `Closes #1397`; names the three `BasePhrase` literals for PR 35. Not delivered: core-owned failed-state copy (PR 35's).

## Brief

Read `Bases/BaseDocumentViewModel.cs:26-32, 509-600, 604-612, 867-930,
944-1004, 1095-1110, 1172-1212, 1444-1451, 1485-1504`,
`WorkspaceViewModel.Bases.cs:127-150, 486-520`,
`WorkspaceViewModel.Rescan.cs:276-290`,
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
terminal outcome and probed in `Refresh` by a core read of the definition
before the retained handle executes (`DefinitionReadFaultForTests` seam;
a failed probe closes the handle and publishes `OpenFailed`);
`DefinitionEditingDisabledReason` derived from it in mac's precedence
(saved query → null; loading/reopening; unavailable or retarget-failed
reason; else null), never from `_handle` alone; `ViewEditQueryJson`
reopens a detached readable definition and seeds the empty-draft JSON for
zero views; `ApplyBuilderEdits` guards on that reason instead of
`Failed`. In `BaseQueryBuilderViewModel.SaveToView`, prepend
`new BaseEdit.AddView("type: table")` and address view 0 when the target
has no views. In `WorkspaceViewModel.Bases.cs`, `BasesEditViewFiltersCommand`
enables on a null reason and announces `BasesFiltersOpenFailed(reason)`
otherwise. In `BaseSurfaceView.RenderContent`, a `BaseLoadFailedGroup`
(heading "This base could not be loaded" at level 2, `StateMessage`, the
reason or "Edit the base definition to repair it.", an Edit button bound
to the command with HelpText "Opens the base definition in the query
builder.") replaces the collapse for Failed; the three strings are
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
reason non-null, Edit refuses; unreadable file the same; detached-then-
deleted reopen says why; failed reopen keeps the retarget reason);
`ADetachedDefinitionReopensForTheEditWhenItsFileIsReadable` (null handle,
intact file → reason null, reopen, builder on view 0);
`NoBlankSurfaceForAFailedBase` (hosted: empty, missing and unloadable
documents show the group with heading, message, sentence-or-reason and the
Edit button; loading shows the loading line); `MissingFileFailsWithTheRecoveryPosture`
(`:110`) gains the reason assertion. Mutations: the `ClampedViewIndex`
execute back; drop `AddView`; reason from `_handle is null`; a null handle
treated as unavailable; collapse the group for Failed. Registers: append a
C4 amendment row to `docs/plans/25_bases_grid_contracts.md` (after `:55`,
never rewriting C4); amend `docs/plans/18_windows_port/w_c_matrix.md:30`
(the group and its AutomationIds); set the `.base` recovery row in
`docs/plans/18_windows_port/parity_matrix.md` to implemented on both
hosts. Interlock: if plan 43 PR 35 (#1398) landed first, adopt its core
failed-state copy instead of the `BasePhrase` constants. Not delivered:
core-owned failed-state copy (PR 35's); rescan-driven detection of a
deleted base (an explicit Refresh is the recovery route, as on mac). Done
when the Windows lane is green, the format gate is clean, the mutation run
is recorded, and the NVDA pass ("This base could not be loaded, heading
level 2" → "No executable base views were found." → "Edit the base
definition to repair it." → "Edit, button, Opens the base definition in
the query builder."; Enter → "Editing filters for <name>."; Save to View →
"Saved builder changes to view."; a deleted base → the reason replaces the
sentence and Edit is unavailable) is in the PR body with `Closes #1397`.
Commit: `fix(windows): a base with no executable view loads as failed,
stays editable, and shows the repair group`.
