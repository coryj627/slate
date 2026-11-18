# CB-11 — A parked citation summary press follows its note's rename

Issues: Closes #1714. Size S. Lanes: windows (the app job's unit facts; no gate journey). AT: NVDA spot check (the sheet opens and says its counts where it did not open before; the sentence is an existing one). Wave: C5, slot 1. Depends on: nothing in code. Parallel with: CB-12 (core's repair event and one fact in `RescanTests.ChangeFeed.cs`) and CB-19. CB-9b follows CB-13's merge and edits `WorkspaceViewModel*.cs` too; see Overlap. Decision: none. Review: a red team, then Codex adversarial-review rounds until it approves (plan §4 rule 9).

Every `path:line` below is as of `main` at 3ae84200, read on 2026-11-17. `S/` is `apps/slate-windows/src/SlateWindows/`, `T/` is `apps/slate-windows/tests/SlateWindows.Tests/`, `WVM.X.cs` is `S/WorkspaceViewModel.X.cs`, and `WVM.cs` is `S/WorkspaceViewModel.cs`.

**Measured before this spec was written (step 0).** The mechanism was run, not only read, in a worktree at 3ae84200 with a Release build.

1. `ADeferredSummaryFollowsItsNoteThroughARename` and `ADeferredSummaryFollowsItsNoteThroughAnotherSessionsRename` (Tests, below) were written first. Both are red on `main`: every assertion up to the release holds (the leaf is on `renamed.md` straight after the hook), and each stops at `Assert.NotNull(workspace.CitationSummary)`.
2. With the design below in place (one helper, two call sites) both are green, and the other 21 facts of `CitationAsyncInterleavingTests` still pass (23 of 23).
3. The issue's related probe was run as a fact: a press parked on `cited.md`, the tab edited, `OpenPath("other.md")`, and the held load released inside the dirty-navigation decision with the dispatcher pumped there. Result: `asked=1 insidePrompt=True path=other.md summaryAfter=2 shown=1`. The sheet opens inside the prompt's frame, says its counts, and is still open, with `cited.md`'s counts, after the tab shows `other.md`. That is a defect of its own; see Found on the way.

*2026-11-17:* corrected from a red team's run of this spec against the tree (an Opus agent, ten probe facts, each run with the call sites removed, with the design, and with the design less one operand). The corrections narrowed the Goal for another session's rename, changed the helper's comparison, rewrote what the reader hears and the NVDA steps, re-sized Found on the way 2, and added Found on the way 3 and the out-of-scope line. It confirmed step 0's three results and every `path:line` it opened.

**Calls for the lead before code starts.** None for the owner. Four are the lead's:

1. Three finds are sized high and filed, not fixed here (Found on the way 1 to 3). Each is on `main` today; none is made by this change. Recommended: file all three, each with its probe as the failing test.
2. `Closes #1714` stands with two orders written as `not delivered` (Goal, last paragraph): another session's rename that is read before this window's rescan moves the tab, and a rename made outside Slate. The issue's own fix is the two hooks, and both are delivered. If the lead reads the issue's "an external rename the rescan picks up" as covering either order, the PR says `Refs` and the issue stays open for the filed finds.
3. The press follows the tab the citations leaf shows, through one helper at both hooks. The reading proposed two rules (replay the rename over the parked path at the first hook, read the feed's planned moves at the second). One rule was chosen because the hooks differ only in which tabs they move, and the fact for a second tab at the parked path shows the path alone is not enough at the feed's hook.
4. The helper compares the leaf's path with the parked path, not the tab's. The red team showed that a re-seat can leave the two spelled differently (Found on the way 2), and a press parked there would otherwise still be dropped by a later rename. It costs nothing and has its own fact.

**Overlap.** CB-11 edits three product files: `WVM.Citations.cs` (one new private member before `:416`), `WVM.cs` (one line before `:3593`), `WVM.Rescan.cs` (one line before `:264`). Tests: `T/CitationAsyncInterleavingTests.cs`. Documents: contract 40 (an R-8 addendum). CB-12 edits core and `T/RescanTests.ChangeFeed.cs`; CB-19 edits the gate and `MainWindow` files. Neither names a file above. CB-9b edits `WVM.Properties.cs`, `WVM.Bases.cs` and `WVM.cs` near `:3840-3850` (teardown) and adds a contract 38 addendum; no line is shared, and whichever merges second takes `main` in on the lead's word. If CB-12 or CB-19 also appends to contract 40, the paragraphs sit under different R rows and the second to merge keeps both.

## Goal

A reader who presses Ctrl+Shift+J while a note's citations are still loading gets the citation summary for that note when the load lands, even if this window renamed or moved the note in between (the Files tree's rename, a move, a batch move, their undo and redo). The same holds for a rename another Slate session made, once this window's rescan has moved the tab while the press is still parked. Today the press is dropped at the rename, and the key reads as dead. A press is still dropped when the reader leaves the note.

Not delivered, and said so in the PR:

- Another session's rename whose effect is read before this window's rescan. All sessions share the index, so the citation load for the old path can run after the other session's rename and publish no rows; the press is then answered with a count of zero, and the sheet stays open when the rescan later moves the tab. This is the usual order when the reader renames in the other window, because this window applies the feed at its next rescan. It is on `main` today and this chunk does not change it (Found on the way 3).
- A rename made outside Slate. On Windows a rescan sees it as a deletion and a creation, not a rename, so neither hook runs.

## Current state on main

**The park.** `WVM.Citations.cs`: `OpenCitationSummary` `:255`; the admission is consulted at `:257`; a loading leaf parks the press with the leaf's path at `:281-286`; the fields are `_parkedSummaryPath` `:386` and `_summaryParked` `:387`. `DropParkedSummaryIfNoteChanged` `:402-414` drops the press when the leaf's path is not the parked path, unless a persistence batch is open (`:404`). `OpenSummaryWhenPublished` `:420-431` detaches on the first publish and opens the sheet only when the leaf's path is still the parked one (`:427`).

**The leaf.** `S/Panels/CitationsPanelViewModel.cs`: `NoteChanged` `:130-149`. A different path bumps the generation (`:137`), so the load in flight for the old path can never publish (`Publish` discards it at `:273-278`), re-arms the once-per-path count sentence (`:141`), and `Refresh` `:172` starts a load of the new path. `RowsPublished` is raised at `:312`.

**The funnel.** `SyncPanels` `WVM.cs:2980`: the leaf follows the active group's active Markdown tab (`:2989-2990`), then the drop runs (`:2993`).

**The two rename hooks, each of which moves tabs in place and then syncs the panels.**

- Slate's own rename, move, and their undo and redo: `RetargetPath(oldPath, newPath)` `WVM.cs:3575`. Tabs move at `:3584-3595` (`ContinueChain` `:3592`, `tab.RetargetPath` `:3593`); `Persist` `:3621`; `SyncPanels` `:3626`. Callers: the sidebar's seam (`S/VaultLifecycleViewModel.cs:1675-1676`, set from `RetargetRequested`), and the listener's `Renamed` event (`S/VaultLifecycleViewModel.FileChanges.cs:42`), which calls it a second time for the same rename and finds no tab left at the old path. The Files tree replays core's report one moved file at a time, so a folder rename arrives as a pair per note; a batch move of a folder, and its undo, pass the folder pair (`S/FilesSidebarViewModel.cs:2316-2319`, `S/FilesSidebarViewModel.FileManagement.cs:424-426`), and `TryRetargetPath` (`WVM.cs:4442`) then moves each note under it.
- Another session's rename, from the change feed: `RetargetPathsForFeedRenames` `WVM.Rescan.cs:210`. Moves are planned per tab against the tabs as they were (`:233-253`, `FollowsFeedRename` `:310-324`), applied at `:261-265`, then `Persist` `:303` and `SyncPanels` `:304`. Caller: `S/VaultLifecycleViewModel.ChangeFeed.cs:567`.

Neither hook touches `_parkedSummaryPath`. After either, the leaf is on the new path, the parked path is the old one, and the drop at `:2993` removes the press.

**Where this differs from the reading** (`reading/03_save_chain_lifecycle.md:242-285`, written at 1ec9bf31). Moved: `SyncPanels` `:2975` is `:2980` and its drop `:2988` is `:2993`; `RetargetPath` `:3565` is `:3575`, its `Persist` `:3611` is `:3621`, its `SyncPanels` `:3616` is `:3626`; `TryRetargetPath` `:4432` is `:4442`; in the test file `ADeferredSummaryIsDroppedWhenItsNoteCloses` `:522` is `:539` and `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout` `:710` is `:727`. Unchanged: every `WVM.Citations.cs` line, `WVM.Rescan.cs:210`, `:261-265`, `:303`, `:304`, and `WVM.Layout.cs:748`. Not in the reading: the listener's second call of the first hook; five sites that move a tab's path in place without syncing the panels (the re-seats; Found on the way 2); and the order in which another session's rename is read before the feed (Found on the way 3). The reading's unverified statement, what the admission answers under the dirty-navigation prompt, is now measured: the prompt is a system message box (`S/MainWindow.xaml.cs:413-431`), which is not one of the window's modal surfaces, so `ModalSurfaces.DecideCitationSummaryOpen` (`S/ModalSurfaces.cs:456-472`) answers Open, and the sheet opens.

## Reference

`T/CitationAsyncInterleavingTests.cs:492-529` (a press parked behind a held load), `:554-591` (a press kept across a close that leaves the leaf on the note), `:726-757` (the parked press answered). `T/Support/HeldWork.cs`. `T/RescanTests.ChangeFeed.cs:840-864` (another session renames an open note) and `:903` on (`ANoteThatTookARenamedNotesPathKeepsItsTab`: two tabs at one path that are different notes). `T/PumpedSaveReentrancyTests.cs:1090` (`Host`, a lifecycle with its listener on a pumped dispatcher). Contract 40's R-8 and its WB-23 addendum (`docs/plans/40_nvda_matrix_remediation_contracts.md:23`).

## Design

**1. One helper, `WVM.Citations.cs`, beside the drop.**

```csharp
private void FollowParkedSummary(WorkspaceTabViewModel tab, string destination)
{
    if (_summaryParked
        && ReferenceEquals(tab, ActiveGroup.ActiveTab)
        && string.Equals(Citations.Path, _parkedSummaryPath, StringComparison.Ordinal))
    {
        _parkedSummaryPath = destination;
    }
}
```

It is called before the tab moves. The press belongs to the note the leaf shows, and the leaf shows the active group's active tab, so the press moves exactly when that tab moves and the leaf is still on the parked note.

- The second operand: a tab that is not the leaf's moves nothing, even at the same path. Two tabs at one path can be different notes for a while (another session moved one note away and this window renamed a second note into the freed name), and the feed then moves one and leaves the other.
- The third operand reads the leaf's path, not the tab's. They are the same string except after a re-seat that respelled the tab without syncing the panels (Found on the way 2). The press was parked under the leaf's spelling, so that is the one compared.

**2. Two call sites, one line each, before `tab.RetargetPath`.** `WVM.cs:3593`: `FollowParkedSummary(tab, retargeted);`. `WVM.Rescan.cs:264`: `FollowParkedSummary(tab, destination);`. Both loops already hold the tab and the exact string the tab is about to take, so the parked path and the leaf's next path are ordinally equal, for a file pair and for a note under a moved folder alike.

**3. What then happens, with no further change.** The hook's `SyncPanels` moves the leaf to the new path. The drop finds the leaf on the parked path and keeps the press. The leaf's `NoteChanged` discards the old path's load and starts one for the new path; that load's publish raises `RowsPublished`; `OpenSummaryWhenPublished` finds the leaf on the parked path and calls `OpenCitationSummary`, which consults the window's admission again and opens the sheet. The sheet says its counts through the existing `CitationSummaryShown`.

**4. What the reader hears.** Measured at the lifecycle level by the red team (the Files tree's rename through the sidebar, the listener attached):

- On `main`, after the rename: the rename's sentence, then, when the loads for the new path land, the outline's count and the citations leaf's count ("Citations, 2 citations."). Both leaves say their count again because the path is new to them. No sheet opens.
- With this chunk: the same three, then the sheet's sentence (`CitationSummaryShown`, High; the leaf's count is Medium and is posted in the same turn), and the keys move into the sheet. Escape puts them back where they were.
- Nothing is said for the old path: its held load never publishes.
- After another session's rename, applied by a rescan while the press is parked: the leaf's count and the sheet's sentence when the citations land. Only an explicit Refresh speaks a completion sentence first; a foreground or watcher rescan says nothing of its own for another session's changes (contract 40 AR-30).
- The answer passes the window's admission when it lands (`S/MainWindow.Citations.cs:143-170`), as any parked press does today. An open Quick Open, search overlay or palette is dismissed for the sheet. An open sheet (Move To, a property sheet) refuses it: the window says "Finish or cancel the current dialog before opening the citation summary." and the press is spent. The Files tree's inline rename editor is not a modal surface, so a second rename in progress loses the keys to the sheet. This chunk keeps a press alive through a restarted load with the reader in the Files tree, so these are more likely to be met than before; none is new.

**5. Not changed.** The drop rule and where it is judged. The admission. The leaf. The re-seats (`WVM.cs:3703`, `:3744`, `:3758`; `WVM.Rescan.cs:648`, `:708`). Core, the FFI, the Mac (it opens its summary sheet at the press, `apps/slate-mac/Sources/SlateMac/SlateCommands.swift:1934-1939`, and parks nothing, so it has no twin of this defect and no ledger line).

## Found on the way

All three are on `main` today. Each is high by `AGENTS.md`'s rule and is filed with its probe as the failing test; the PR names each number with no closing word before it.

1. **A publish inside a pumped frame answers a parked press, and the sheet outlives its note.** Measured (step 0, 3). `AdmitDirtyTab` (`WVM.Layout.cs:748`) asks through a system message box, which pumps; a publish for the note being left lands inside it; the handler finds the leaf still on the parked note and opens the sheet under the prompt; on Discard or Save the tab then shows the next note with the sheet, and the old note's counts, over it. Nothing closes a summary whose note changed. The same can happen inside any pumped wait of an action that then changes the note. High: the fix decides what the reader hears when an answer arrives under a prompt (hold it until the action ends, then open or drop), reaches `WVM.Layout.cs` and the window's sheet observer, and needs an NVDA pass on the prompt. This chunk widens its reach a little: a press that used to end at a rename can now be answered inside a later pumped frame. The probe's text is kept beside this spec as `cb11_probe_dirty_navigation.cs.txt`.
2. **A re-seat that respells the leaf's tab does not sync the panels.** Measured by the red team: a missing active tab on `Ghost.md`, its file back as `ghost.md`, `ReseatMissingTabs` (`WVM.cs:3674-3707`); the tab is on `ghost.md` and no longer missing, and the citations leaf is still on `Ghost.md`. `ReseatMissingTabsAsync` (`WVM.Rescan.cs:541`) has the same shape. Every panel stays on the old spelling until the next activation. High, not small, although the fix sits in files this PR changes: a sync after a re-seat makes the leaves say their counts at a new occasion (a re-seat from an event, or from a rescan that is otherwise silent), which needs the owner's call. Design 1's third operand keeps this chunk's own route working after a re-seat; the panels' stale spelling is the filed issue's.
3. **A parked press is answered from a read of a path the index no longer has.** Measured by the red team, with no seam (a 155,000-entry `.bib` held the seed about four seconds; another session renamed the note 12 to 26 ms after the press): `ListCitationsInFile` returns an empty list for a path with no file row (`crates/slate-core/src/citations_db.rs:81-92`), the leaf publishes no rows for the old path, and the sheet opens saying the note has no citations. The rescan then moves the tab and the leaf shows two rows, with the sheet still saying none. High: telling "no file at this path" from "no citations" needs core or the FFI, or a host design. Without a press the same read leaves the leaf saying a moved note has no citations until the rescan.

## Tests

All in `T/CitationAsyncInterleavingTests.cs`, in the shape of `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout` (`:726-757`): `StaThread.RunPumped`, `MakeAsyncWorkspace`, a `HeldWork` on `workspace.Citations.InterleaveForTests`, the release in a `finally`, `PumpedDispatcher.PumpUntilDrained(QuiesceAsync(workspace))`, `held.AssertHeldUntilReleased()`. No new fact adds a sleep, a delay or a fixed pump; `QuiesceAsync`'s one delay is already pinned by member and count in `TestWaitCensus` (`T/Censuses/TestWaitCensus.cs:2915`), and a call to it adds none. `TestWaitCensus` rule 4 (app files read through `GateFile`) covers the gate project only and does not apply here.

- `ADeferredSummaryFollowsItsNoteThroughARename` (written; red on `main`). Park on `cited.md`; `session.RenameFile("cited.md", "renamed.md")`, then `workspace.RetargetPath("cited.md", "renamed.md")`, which is the order the sidebar uses; the leaf is on `renamed.md` and no sheet is open; release. Then: the leaf is on `renamed.md`, the sheet is open with a total of 2, `OpenSummaryWhenPublishedInvokedForTests` is 1, one `CitationSummaryShown`. Mutation: remove the call at `WVM.cs:3593`.
- The same for a note under a moved folder, as a second fact or a theory row: a note `notes/deep.md` with two citations, written before the scan; the folder moved through the session; then `RetargetPath("notes", "papers")`, the folder pair a batch move of a folder passes. The sheet opens on `papers/deep.md`'s counts. It pins that the press takes the tab's own new path, not the pair's destination. The red team ran this row: green with the design, red with the call removed.
- `ADeferredSummaryFollowsItsNoteThroughAnotherSessionsRename` (written; red on `main`). Park on `cited.md`; read the tab's `SavedContentHash`; a second `VaultSession` on the fixture renames the file; `workspace.RetargetPathsForFeedRenames([new FeedRename("cited.md", "renamed.md", hash, OldIsGone: true)])` reports rename 0 moved a tab; release. Same ending. The hold is what makes this the order the Goal names: the feed moves the tab before any load reads the old path. Mutation: remove the call at `WVM.Rescan.cs:264`.
- `ADeferredSummaryStaysWithTheLeafsNoteWhenAnotherTabAtItsPathMoves` (new; the red team built it and it holds). `other.md` open in a tab of its own, then `cited.md` in a new tab, active, its load held, the press parked. A second session renames `other.md` to `moved.md`. This window renames `cited.md` to `other.md` (the session call, then `RetargetPath`), so both tabs name `other.md` and the press has followed its tab there. Then `RetargetPathsForFeedRenames([new FeedRename("other.md", "moved.md", <the first tab's baseline>, OldIsGone: false)])` moves the first tab only. Release. Then: the leaf is on `other.md` and the sheet is open with a total of 2, the cited note's counts. Mutation: remove the `ReferenceEquals` operand. The only planned move is of a tab that is not the leaf's and shares the parked path, so the press goes to `moved.md` with it and the sync drops it. Seen red before it is trusted.
- `ADeferredSummaryOnAReseatedNoteStillFollowsARename` (new; the red team's probe, kept). An active tab on `Ghost.md` whose file is missing and is back as `ghost.md`; `ReseatMissingTabs`; the tab is on `ghost.md` and the leaf still on `Ghost.md`; the leaf's load held and the press parked; the session renames `ghost.md` to `g2.md`, then `RetargetPath("ghost.md", "g2.md")`; release. The sheet opens on `g2.md`'s counts. Mutation: compare `tab.Path` in the third operand; the press is dropped. If Found on the way 2 is fixed before this merges, the fact's setup no longer produces the stale spelling; say so and drop the fact with the reason.
- `ADeferredSummaryFollowsItsNoteThroughTheFilesTreesRename` (new, lifecycle level; the red team built it with no new seam). `PumpedSaveReentrancyTests.Host`: create the cited note through `Lifecycle.SessionForTests.CreateExclusive`, `host.Settle()`, open the note in a new tab (the host's own tab is dirty), park the press behind the held load, select the note in the Files tree and run the sidebar's `TryRenameSelected`, drain so the listener's `Renamed` event has run its effects (the second `RetargetPath`, the re-seat, the dependents), release, and wait with `Settle` or `QuiesceAsync`. Then: the sheet is open with the note's counts, `OpenSummaryWhenPublishedInvokedForTests` is 1, and the announced events after the rename's own sentence end with one `CitationsCount` and one `CitationSummaryShown`. It is the route the reader takes, and the only fact that would see a sync added between the tab's move and the hook's own. Mutation: the first fact's.
- The existing drop facts stay green unchanged: `ADeferredSummaryDoesNotAnswerForADifferentNote`, the three `ADeferredSummaryIsDropped…`, `ADroppedParkLeavesNoHandlerSubscribed`, `ClosingASplitPaneThatLeavesTheNoteKeepsThePress`.

## NVDA spot check

Per `docs/runbooks/windows-test-obligations.md` and the field-pass launcher (`scripts/windows/launch-for-field-pass.ps1`), NVDA 2026.3.1, speech read from NVDA's log. The load must be slow enough to rename under: citation loads wait for the bibliography seed, so use a vault copy whose `.bib` holds the seed for several seconds on this machine (the red team's 155,000 entries gave about four; measure, and grow it if the hands need longer).

The vault holds one note, the cited one, so that no other row can be selected on the way: selecting a Files row opens that note (`S/FilesSidebarViewModel.cs:796-815`), and opening another note drops the press for the right reason and would make the branch look like `main`. Steps: start the app on the vault with the keys in the Files tree on the note's row and the note open; press Ctrl+Shift+J at once (nothing is spoken, the press is parked); press F2, type a new name, press Enter; wait. Record everything spoken from Enter on, and that no other note was opened.

- On a build of `main`, first: the rename's sentence, then the outline's and the citations leaf's counts. No sheet.
- On the branch: the same, then the sheet's sentence with the note's counts, the keys on the sheet's button; Escape returns them to the Files tree. Record whether both count sentences are heard before the sheet's, since the sheet's is High and is posted in the same turn as the leaf's Medium count.

The PR opens as a draft until the pass is recorded.

## Docs and registers

- `docs/plans/40_nvda_matrix_remediation_contracts.md`: an `*R-8 addendum (2026-11-…, plan 47 CB-11, #1714):*` paragraph after the WB-23 addendum at `:23`, in its form: a parked press follows its note through a rename or a move this window made, and through another session's once the rescan moves the tab; it names the facts and the three filed finds. The frozen row is not edited.
- Contract 38's ledger: no `A11yEvent` construction is added or moved; `python -I scripts/a11y_trigger_ledger.py --check` must pass unchanged.
- No census row: `ConnectionsLeafCensus` pins the two hooks as the callers of `Connections.Retarget` (`T/Censuses/ConnectionsLeafCensus.cs:110`), and no census scrapes `ContinueChain` or a tab's `RetargetPath`.
- No Mac ledger line, no parity-matrix change, no help document. Plan 47 §12's row goes in the wave's docs PR.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-cb11-parked-summary-rename'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$t = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~CitationAsyncInterleavingTests"
dotnet test $t -c Release --no-build --filter "FullyQualifiedName~CitationsPanelTests|FullyQualifiedName~CitationsFocusRestoreTests|FullyQualifiedName~RescanTests|FullyQualifiedName~FileManagementTests|FullyQualifiedName~PumpedSaveReentrancyTests|FullyQualifiedName~ConnectionsLeafTests|FullyQualifiedName~ConnectionsLeafCensus|FullyQualifiedName~TestWaitCensus|FullyQualifiedName~A11yTriggerParityCensus|FullyQualifiedName~SaveChainAdmissionCensus"
python -I scripts/a11y_trigger_ledger.py --check
python scripts/check_windows_census_trait.py
python scripts/check_windows_docs_trigger.py
$cs = git diff --name-only origin/main -- '*.cs'
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs
git diff --stat; git diff --ignore-cr-at-eol --stat   # the two must agree
```

The whole unit project runs once before the push. The PR body quotes each fact's red result and its green one and each mutation's result.

## Acceptance

- [ ] Each fact in Tests was seen red, on `main`'s code or by its named mutation, and then green; the PR body says which for each.
- [ ] The existing drop facts pass unchanged.
- [ ] `git diff --stat` shows three product files with 24, 1 and 1 added lines and none removed, one test file, and contract 40; it agrees with `git diff --ignore-cr-at-eol --stat`.
- [ ] The NVDA phrases, on `main` and on the branch, are in the PR body.
- [ ] The three finds are filed and their numbers are in the PR body with no closing word before any of them. The two `not delivered` orders are in the PR body.
- [ ] `gh pr view N --json closingIssuesReferences` lists #1714 only.
- [ ] Rule 9: the red team's findings and the Codex round count with its verdict are in the PR body, which ends with the count.

## Brief

Windows host only. Read this spec's Current state and open each line; then `WVM.Citations.cs:235-300` and `:384-431`, `S/Panels/CitationsPanelViewModel.cs:126-313`, `WVM.cs:3575-3627`, `WVM.Rescan.cs:192-324`, and the reference facts. The helper, its two call sites and the two facts marked "written" are already in the worktree from step 0, uncommitted. `WVM.cs` and `WVM.Citations.cs` hold lines that end in CRLF among lines that end in LF, and an editor that rewrites the file flattens them and turns the format gate red (it did once here). Make every C# edit so that only the lines you mean to change differ, and check `git diff --stat` against `git diff --ignore-cr-at-eol --stat` after each. Confirm the two written facts red with the call sites removed and green with them, then write the folder row, the second-tab fact, the re-seat fact and the lifecycle fact, each seen red by its mutation. Add contract 40's addendum. Run Verification. Do not change the drop rule, the admission, the leaf or the re-seats. Commit: `fix(windows): a citation summary press parked while its note loads follows the note through a rename`.

## What delivery changed

*2026-11-17, delivered as #1997 (6351c82b).* The design above, "the press follows the tab the leaf shows", was the first build. The red team and eight Codex rounds changed it; each change has a fact seen red first.

1. **The owner, not the active tab.** The press records the tab the citations leaf followed when the reader pressed (`_parkedSummaryTab`), and a rename moves it only with that tab. Two tabs at one path can be different notes for a while, and the reader can activate either (Codex round 1, `ADeferredSummaryIsDroppedWhenTheReaderActivatesAnotherNoteAtItsPathBeforeTheFeedMovesIt`). A first fix that compared saved hashes dropped valid presses on a feed swap (round 2, `ADeferredSummaryFollowsItsNoteWhenAnotherSessionSwapsItWithAnotherNote`) and was replaced by the owner.
2. **The handoff.** When the owner closes and the leaf stays on the path (a split pane's close), the press passes to the tab the leaf now follows, only when `HoldTheSameNote` proves the same saved version: the same baseline hash and the same file identity, both known (rounds 3 to 5: `ADeferredSummaryKeptThroughASplitPaneCloseFollowsALaterRename`, `ADeferredSummaryIsNotTakenOverByAnotherNotesTabWhenItsOwnTabCloses`, `ADeferredSummaryIsNotHandedToATabWhoseFileIdentityIsUnknown`).
3. **A note, and the tab's own version.** The destination must still be a note (the red team: `ADeferredSummaryIsDroppedWhenItsNoteIsRenamedOutOfTheNoteKind`, a theory with a feed row). Slate's own rename follows only when the index at the new path holds the owner's saved baseline, one read on the dispatcher in a turn that already calls core there (round 5: `ADeferredSummaryDoesNotFollowItsTabOntoAnotherNotesFile`). A feed move follows only when the record's hash and the window's present hash for the destination (`FeedRename.ToHash`, new, built in `PlanFeedRenames`) both equal the owner's baseline (rounds 6 and 7: `ADeferredSummaryDoesNotFollowATabTheFeedMovesByItsStaleMark`, and through the real lifecycle and feed `ADeferredSummaryDoesNotFollowAFeedRenameOfAnotherNoteAtItsPath`). The record-hash check has no fact of its own: the present-hash check stops every schedule built so far; it stays as a conservative guard.
4. **Files.** Five product files, not three: `WorkspaceViewModel.Citations.cs`, `WorkspaceViewModel.cs` (one line), `WorkspaceViewModel.Rescan.cs` (the move loop and `FeedRename`), `VaultLifecycleViewModel.ChangeFeed.cs` (`PlanFeedRenames`). Facts: 18 new, 42 in the class.
5. **What the reader hears** (NVDA 2026.3.1, on 2d45fbea): on the branch, "Citations, 2 citations." is cut off by the sheet's "Citation summary. 2 citations referencing 2 unique sources.", which is an ImportantMostRecent notification posted in the same turn; the counts are heard in the sheet's sentence. A line on #1955. Escape returns the keys to the renamed row.
6. **Finds.** Filed: #1988, #1989, #1990, #1993 (a WB-23 gap inside nested workspace actions, found by Codex round 1), #1994 (two notes with identical bytes both follow one rename, found by Codex round 2). Shapes added as comments: on #1990, a load reads whatever file is at the path when it runs (declined for CB-11 on the lead's call); on #1993, the same-path switch; on #1994, this window's rename of a refilled path and a coalesced window's shown hash.
7. **Not delivered**, as planned: another session's rename read before this window's rescan (#1990); a rename made outside Slate (R-9, AR-8).