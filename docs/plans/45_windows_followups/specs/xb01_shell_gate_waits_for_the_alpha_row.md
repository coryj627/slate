# XB-1 — The shell gate's files-tree tests wait for their rows instead of looking once

Issues: #1586. Size S. Lanes: windows (the FlaUI shell gate). AT: — (test-only). Wave: X1, slot 1. Depends on: nothing. Parallel with: XB-3, XB-4a (no shared files). Decision: none. Interlock: plan 44's WB-7 edits `ShellAccessibilityTests.cs:11876-11889` (`OpenCanvasFromTree`); XB-1 lands first and leaves that helper alone, so WB-7 rebases cleanly.

## Goal

The history gate test, and every other gate test that looks for a Files-tree row exactly once after waiting only for the tree, waits for the row with the file's own helper. A slow scan or tree load then costs seconds, not a red gate on an unrelated merge (run 39873324510 on 55342ecd, whose tree was identical to a green run 45 minutes earlier).

## Current state on main

- **The helper.** `apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:11759-11793` `WaitForTreeItemStartingWith(AutomationElement group, UIA3Automation automation, string prefix, bool requireItems = false)`: `SpinWait.SpinUntil` (`:11764`) over `FindAllDescendants(ByControlType(TreeItem)).FirstOrDefault` (`:11769-11771`), prefix compared `StringComparison.Ordinal` on `item.Properties.Name.ValueOrDefault` (`:11772`), a `COMException` retries (`:11780-11782`), timeout 10 s (`:11785`), the failure lists every descendant row's name (`:11786-11791`). 24 call sites across the partial files.
- **The one-shot in the history test.** `HistorySurfaces_LeafDiffAndRestore_AreClean` (`[Fact]` `:4714`, `[Trait("gate","W-C")]`): `:4758-4759` waits 30 s for `FilesTree`; `:4760-4765` then does one `FindAllDescendants(...).FirstOrDefault(item => item.Name.StartsWith("alpha", OrdinalIgnoreCase)) ?? throw new XunitException("The alpha TreeItem is absent.")`. Its vault is inline (`:4718-4725`): one note `alpha.md` ("# Alpha\n\nOriginal body.\n"), not `WriteShellFixtureVault`.
- **The same pattern elsewhere in the file**, each after only a `WaitForElement` on the tree: `:324-327` (`RunShellAccessibilityGate`, "note"), `:1436-1441` (`ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable`, "note"), `:1771-1776` (`ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer`, "note"), `:2733-2738` (`RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates`, "host"), `:2937-2942` (`TaskPanels_RowsToggleAndReviewCarriesTheMacShapes`, "todo"), `:3449-3454` (`CitationSurfaces_GridsSheetsAndChords_AreClean`, "cited"), `:4263-4268` (`BasesSurfaces_GridBuilderAndLeaves_AreClean`, "Notes"). Two more look up canvas-outline rows after a wait of their own (`:8739-8743`, `:9727-9730`) and are not this defect; `OpenCanvasFromTree` (`:11883-11888`) is WB-7's.
- **The other failure the issue names.** `FilesTree_ArrowsKeepFocusEnterOpens` (`ShellAccessibilityTests.FilesTree.cs:39`) already waits for each of its four rows with the helper (`:74-77`) and again at `:86-88` and `:125`. Run 39728855123's failure is therefore not this one-shot; its cause is unread.
- **Row names.** The tree rows are file names: the FilesTree test waits for `"alpha.md"` with the Ordinal helper and passes, so a row's name starts with its lowercase file name. The history test's `"alpha"` with `OrdinalIgnoreCase` matched the same row.

## Reference

The file's own waiting helpers (`WaitForTreeItemStartingWith`, `WaitForTreeItems` `:11895-11905`, `SelectTreeItem` `:7199-7215`); plan 44 WB-7's doctrine ("the lane waits on conditions, not the clock").

## Design

1. `:4760-4765` becomes `AutomationElement noteItem = WaitForTreeItemStartingWith(filesTree, automation, "alpha.md");` — the full file name, Ordinal, as `FilesTree.cs:74` does. Confirm the row's name in one local run first (the helper's failure message lists every row, so a wrong prefix is self-explaining).
2. The seven sibling one-shots (`:324`, `:1436`, `:1771`, `:2733`, `:2937`, `:3449`, `:4263`) take the same helper with the row's full file name from each test's own fixture (`note.md`, `host.md`, `todo.md`, `cited.md`, `Notes.base` — read each fixture's `File.WriteAllText` lines to get the exact spelling and case). Same defect, same file, same helper; the PR body lists each site. Not touched: `OpenCanvasFromTree` (WB-7), the two canvas-outline lookups, every site that already waits.
3. Read run 39728855123's log for `FilesTree_ArrowsKeepFocusEnterOpens` (`gh run view 39728855123 --log-failed`). If it is a one-shot lookup this chunk missed, fix it here; otherwise file an issue with the log's failing assertion and leave it.
4. No product code changes.

## Tests

The eight edited facts are the tests. Each must still pass on the lane and locally (NVDA closed, desktop free). No new fact: a wait that replaces a one-shot has no red-first form except the flake itself; the PR body quotes the run that failed and the helper's timeout.

## Docs and registers

None. No announcement, chord or matrix changes.

## Verification

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-xb1'
./apps/slate-windows/generate-bindings.ps1 -Locked
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release   # Error(s)=0
$gate = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$filter = 'FullyQualifiedName~HistorySurfaces_LeafDiffAndRestore_AreClean|FullyQualifiedName~ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable|FullyQualifiedName~ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer|FullyQualifiedName~RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates|FullyQualifiedName~TaskPanels_RowsToggleAndReviewCarriesTheMacShapes|FullyQualifiedName~CitationSurfaces_GridsSheetsAndChords_AreClean|FullyQualifiedName~BasesSurfaces_GridBuilderAndLeaves_AreClean|FullyQualifiedName~FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean'
dotnet test $gate -c Release --no-build --filter $filter      # NVDA closed, desktop free
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs
gh run view 39728855123 --log-failed | Select-String -Context 3 'FilesTree_ArrowsKeepFocusEnterOpens'
```

CI lanes: `windows.yml` (the shell gate on the PR). AT: none.

## Acceptance

- [ ] `:4760-4765` uses `WaitForTreeItemStartingWith(filesTree, automation, "alpha.md")`; the seven siblings use the helper with their fixture's file name.
- [ ] The gate is green on the PR; the eight facts pass locally.
- [ ] Run 39728855123 read; fixed here or filed, named in the PR body.
- [ ] `Closes #1586`. Not delivered: nothing (or the filed issue's number for the other failure).

## Brief

Read `apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.cs:11755-11793` (the helper), `:4714-4770` (the history test and its inline fixture), and the seven sibling sites `:320-330`, `:1432-1444`, `:1767-1779`, `:2729-2741`, `:2933-2945`, `:3445-3457`, `:4259-4271`, plus each test's fixture-writing lines to get the exact file names; `ShellAccessibilityTests.FilesTree.cs:39-90` for the waiting pattern. Build: replace each one-shot `FindAllDescendants(...).FirstOrDefault(... StartsWith(prefix, OrdinalIgnoreCase)) ?? throw` with `WaitForTreeItemStartingWith(<tree>, automation, "<file name>")` (Ordinal, full file name as the fixture writes it); leave `OpenCanvasFromTree` (`:11876-11889`, plan 44 WB-7) and the two canvas-outline lookups (`:8739`, `:9727`) alone. Run the eight facts locally with NVDA closed, then `dotnet format --verify-no-changes --include` on the one file. Read `gh run view 39728855123 --log-failed` for `FilesTree_ArrowsKeepFocusEnterOpens`: fix here if it is the same one-shot class, else file an issue quoting the failing assertion. PR body: the eight sites, the run ids 39873324510 and 39728855123, `Closes #1586`. Commit: `test(windows): the shell gate waits for a files-tree row instead of looking for it once`.
