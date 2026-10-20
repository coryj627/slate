# WB-23 — A parked citation summary is dropped when its note changes

Issues: #1314 (closes here). Size S. Lanes: windows. AT: NVDA spot (reopening the note is silent). Wave: W8 (slot 2, before WB-24). Depends on: nothing. Parallel with: WB-22, WB-21b. Decision: WD10 (pending; recommended "drop at B, even on a quick A→B→A").

## Goal

A Ctrl+Shift+J pressed while a note's citations are still loading is parked until the load publishes. If the reader closes the tab, or switches to a canvas, base or graph tab, before the publish, the park stays armed: nothing ever publishes for "no note", and when the same note is opened again — minutes later — its first publish opens the summary sheet, moves focus into a modal and speaks its counts with nobody having pressed anything. After this chunk the park is dropped the moment the citations leaf's note is no longer the note it was asked about, so a reopened note is silent and a press answered on its own note still opens the sheet.

## Current state on main

Verified against the worktree; the issue's approximate lines have drifted as noted.

- `WorkspaceViewModel.Citations.cs:255-298` `OpenCitationSummary`: admission (`:257-260`), then while `Citations.IsLoading` the press is parked — `_parkedSummaryPath = Citations.Path` (`:280`) and, once, `_summaryParked = true; Citations.RowsPublished += OpenSummaryWhenPublished` (`:281-285`); otherwise the sheet is built and `SheetShown()` announces its counts (`:288-297`, R-8 / #1251).
- `:386-387` the fields `_parkedSummaryPath`, `_summaryParked`; `:392-402` `OpenSummaryWhenPublished`: detaches, clears both fields, and reopens when `Citations.Path` equals the asked path (ordinal). Its own summary says a re-park would "leave a request armed against a question nobody asked" (`:389-391`).
- `Panels/CitationsPanelViewModel.cs:126-145` `NoteChanged(path)`: a same-path call returns; otherwise the generation is bumped (parked work for the old path never publishes), rows cleared, and for `null` `IsLoading = false` with no new load (`:139-143`) — so no publish follows and the handler stays subscribed; a different note's `Refresh()` does publish and detaches it.
- `WorkspaceViewModel.cs:2437-2451` `SyncPanels()`: the one caller of `Citations.NoteChanged(ActiveGroup.ActiveTab is { IsMarkdown: true } citedTab ? citedTab.Path : null)` (`:2444-2445`) — the issue's "~:1880-1891".
- `CitationAsyncInterleavingTests.cs`: the fixture (`OpenScanned` `:69`, `MakeAsyncWorkspace` `:78`, `QuiesceAsync` `:90`); `ADeferredSummaryDoesNotAnswerForADifferentNote` (`:411`), `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout` (`:445`); the hold seam `CitationsPanelViewModel.InterleaveForTests` (`:116`, invoked at `:201`, `:209`). `WorkspaceViewModel.CloseActiveTabCommand` (`:2319`, `:2590`). The issue's reproduction fact (`ADeferredSummaryIsDroppedWhenItsNoteCloses`) exists only on branch `claude/1285-deferred-summary-wip` and in the Wine probe it names; on `main` it does not exist and would fail on its last assertion.

## Reference

None on mac (the citation summary sheet and its deferred press are Windows surfaces; W4-5); the issue's text is the behavioural spec. Contract 40 R-8 (`40_nvda_matrix_remediation_contracts.md:21`) is why the stray sheet speaks ("a popover or sheet announces its outcome when it opens"); OD-12/R-10 forbids a modal appearing under keys nobody moved. #1251's `CitationSummaryShown` is unchanged.

## Design

`WorkspaceViewModel.SyncPanels()` (`:2444-2445`): right after `Citations.NoteChanged(…)`, call a new `DropParkedSummaryIfNoteChanged()` in `WorkspaceViewModel.Citations.cs`: `if (_summaryParked && !string.Equals(Citations.Path, _parkedSummaryPath, StringComparison.Ordinal)) { Citations.RowsPublished -= OpenSummaryWhenPublished; _summaryParked = false; _parkedSummaryPath = null; }`. The handler's own identity check stays (a publish for another path that slips through still answers nothing). `OpenCitationSummary`, `OpenSummaryWhenPublished`, `CitationsPanelViewModel.NoteChanged` and the admission gate are not changed. The drop is deterministic at the first `SyncPanels` whose note differs — a close, a switch to a non-Markdown tab, or a switch to another note.

WD10: recommended "yes, drop at B". Consequence: on a quick A→B→A before any publish, the press is dropped at B and A's later publish opens nothing; today that outcome depends on which publish lands first. If the owner answers otherwise (keep the press through A→B→A), the drop applies only when the new path is null (close, or a non-Markdown tab), the A→B→A fact below flips to "still answers", and the issue's timing-dependent race on a Markdown switch is recorded as accepted in contract 40.

## Tests

`CitationAsyncInterleavingTests.cs` (the fixture above; the load held through `InterleaveForTests` as #1285's fix does, so no fact depends on timing; `PumpedDispatcher.PumpUntilDrained(QuiesceAsync(workspace))` for the STA ones, as `:431`):

- `ADeferredSummaryIsDroppedWhenItsNoteCloses` — the issue's fact verbatim: park, `CloseActiveTabCommand`, release the hold, quiesce: `Citations.Path` null, `CitationSummary` null; reopen `cited.md`, quiesce: `CitationSummary` still null, and `announced` holds no `CitationSummaryShown`. Mutation: remove the drop → the sheet opens on the reopen (the issue's observed failure).
- `ADeferredSummaryIsDroppedWhenTheReaderSwitchesToABoardAndBack` — the same with a `.canvas` (or `.base`) tab activated instead of a close.
- `ADeferredSummaryIsDroppedOnAQuickSwitchAwayAndBack` (WD10) — park on `cited.md`, open `other.md`, return to `cited.md` before any publish, release: no sheet.
- `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout` (`:445`) and `ADeferredSummaryDoesNotAnswerForADifferentNote` (`:411`) stay green unchanged.
- `ADroppedParkLeavesNoHandlerSubscribed` — after the drop, a later publish for `cited.md` (the reopen's) does not run `OpenSummaryWhenPublished` (count through a test seam on the handler, or assert `_summaryParked` through an `IsSummaryParkedForTests` property).

No FlaUI journey (the sheet's hosted facts are the witness; the gate's citation journeys are unchanged). No census change (no new names or events).

## Docs and registers

- Contract 40: a dated addendum under R-8 (`:21`): "(WB-23, #1314) a parked Ctrl+Shift+J is dropped at the first active-note change away from the note it was asked about, so a reopened note never opens the sheet unasked." Append; never rewrite.
- `18_windows_port/w_c_matrix.md`: the citations row (grep "Citation summary" / W4-5) gains "parked press dropped on note change". No designations, ledger or parity change.

## Verification

```powershell
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb23'; ./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~CitationAsyncInterleavingTests"
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Citations.cs apps/slate-windows/tests/SlateWindows.Tests/CitationAsyncInterleavingTests.cs
```

Run the new facts 50× locally (`--filter` in a loop) since this file's facts have a flake history (#1285, #1316). Lanes: `windows.yml` only. NVDA spot check (§4 rule 4): open a cited note at startup (citations gated on the bibliography seed), press Ctrl+Shift+J at once, Ctrl+W before the leaf publishes, reopen the note from the tree: nothing is spoken but the note's own open, no sheet; then on the open note press Ctrl+Shift+J after the leaf loaded: "This note has N citations referencing M unique sources" as today.

## Acceptance

- [ ] WD10 answered (default: drop at B).
- [ ] The four new facts exist, failed first (the first one fails on its last assertion with the sheet open on `cited.md`'s counts); mutation recorded; the two existing facts green; 50× local run clean.
- [ ] `SyncPanels` drops the park when the citations leaf's note differs from the asked one; the handler's identity check kept.
- [ ] Contract 40 R-8 addendum and the `w_c_matrix.md` note appended.
- [ ] NVDA spot check in the PR body.
- [ ] PR body: `Closes #1314`; "Refs #1285" only as context (no closing keyword).

## Brief

Read `WorkspaceViewModel.Citations.cs:250-300` and `:380-405`, `Panels/CitationsPanelViewModel.cs:110-150`, `WorkspaceViewModel.cs:2437-2452`, and `CitationAsyncInterleavingTests.cs:60-120`, `:405-470`. Build one method, `DropParkedSummaryIfNoteChanged`, called from `SyncPanels` right after `Citations.NoteChanged`, that unsubscribes `OpenSummaryWhenPublished` and clears `_summaryParked`/`_parkedSummaryPath` when `Citations.Path` differs ordinally from the asked path. Facts: the issue's `ADeferredSummaryIsDroppedWhenItsNoteCloses` first (it fails on `main`), then the board-and-back, quick A→B→A and no-handler facts. Registers: contract 40 R-8 addendum, `w_c_matrix.md` citations row. Done when the lane is green and the spot check is in the PR body. Commit: `fix(windows): a parked citation summary is dropped when its note changes, so a reopened note never opens the sheet unasked`.
