# WB-29 — Unsaved notes survive closing, quitting and a crash on Windows, and come back unsaved when the vault reopens

Issues: #1525 (Windows half; closes in whichever of WB-29 and plan 43
PR 29's mac half merges last — both hosts merged and field-passed, WD20;
earlier PRs write `Refs #1525`). Size L (design first, in plan 43 PR 29's
design). Lanes: windows (the core store comes from PR 29; this chunk adds
no core module). AT: NVDA full pass. Wave: W11, slot 2, after WB-28 and
after PR 29's core store is on `main` with green CI. Parallel with:
WB-30. Decision: WD20 (pending; recommended "one design for both hosts
written as plan 43's PR 29 design, fixing the store schema, the restore
rule and the open-sequence ordering; core owns the store; WB-29 starts
only after that implementation is on `main`"; under the other answer —
Windows writes its own store — this chunk grows a `HotExitStore.cs`
mirroring the design's schema and identity key, the FFI calls become
local file IO, and `parity_matrix.md` records the parity debt).

## Goal

Closing the main window or quitting Slate on Windows with unsaved edits
never asks and never writes the notes. Each unsaved buffer is written as
you type to the hot-exit store PR 29 ships (outside the vault, keyed by
the vault's identity), so a crash or a kill keeps it too; a close or a
switch waits for a durable write of the latest edits and, if that write
cannot land, falls back to today's prompt rather than discarding the only
copy. When the vault reopens, the buffers come back in their tabs, still
marked unsaved, read after the open scan and injected into the tabs the
layout restore constructs, and the launch landing happens once. A buffer
whose file changed on disk meanwhile is restored dirty and the existing
save-time conflict machinery decides at the next explicit save. Close
Vault still asks. Core owns the copy ("Restored 2 unsaved notes").

## Current state on main

Verified against the worktree; the plan's lines hold. **The shared
design does not exist yet:** `docs/plans/43_mac_backlog_delivery/` holds
designs 20, 21, 22 and the 24 trio only; `specs/` holds `pr22_…` only.
PR 29 is a row (`00_program.md:437`, "L (design first)"; RD1 as amended
`:557`). Nothing below can start until that design and its core-store PRs
are merged.

- **Closing asks today.** `MainWindow.xaml.cs:1986-1992` `Window_Closing`
  → `_viewModel.PrepareForApplicationClose()`; cancels on `false`.
  `VaultLifecycleViewModel.cs:637-650` `PrepareForApplicationClose`:
  refuses while busy, else `TryCloseWorkspace()` and
  `AnnounceTeardown(teardown, TeardownRoute.ApplicationClose)` (silent
  for that route, `:1433-1440`). `TryCloseWorkspace` `:1405` →
  `TryCloseWorkspaceCore` (`:1416-1568`): with `workspace.HasDirtyTabs`
  (`WorkspaceViewModel.Layout.cs:54`) it reads `DirtyTabsForPrompt()`
  (`WorkspaceViewModel.cs:2829`), calls `_confirmUnsavedClose()` (`:1538`;
  the `Func<VaultCloseDecision>` at `:78`, defaulted to `Cancel` `:213`),
  then `SaveAll()` (`:2753`) or, for Discard, requires `SavesIdle`
  (`:2816`) and `DirtyTabsStill(asked)` (`:2839`). The same core serves
  Close Vault (`:454`), vault switch (`:605`) and application close
  (`:644`).
- **Dirty state and edits.** `WorkspaceTabViewModel`
  (`WorkspaceViewModel.cs:94`, constructor `:138`) `IsDirty` `:435-451`
  (private set; bumps `ContentGeneration` `:239-241`, ends
  `IsTransient`); the buffer is `_editorSession` (`:430-432`
  `ReplaceAll`); `BumpContentGeneration` also at `:442`, `:478`.
  `WorkspaceSaveCoordinator.HasAdmittedSaveFor`
  (`WorkspaceSaveCoordinator.cs:83-87`) is the admission seam.
- **The open sequence.** `VaultLifecycleViewModel.cs:1176-1192`
  `WorkspacePersistence(root).Load()` (`:1180`) then `new
  WorkspaceViewModel(session, root, …)` (`:1184`, constructor
  `WorkspaceViewModel.cs:2103`), whose body restores the layout at
  `:2309` `(_root, _activeGroup) = Restore(_persistence.Load());`.
  **`Restore` builds new instances:** `RestoreNode`
  (`WorkspaceViewModel.Persistence.cs:53-108`) creates a new
  `WorkspaceGroupViewModel` and, per `WorkspaceTabState`, a new
  `WorkspaceTabViewModel(_session, tabState, …)` (`:69-82`), attaches
  documents (`:83`), adds it to the group (`:84`) and restores the active
  tab (`:88-91`); it neither finds nor reuses pre-existing tabs.
  `WorkspaceReady?.Invoke` at `:1280`. `OpenRecentAsync` `:724`. The
  layout store is `WorkspacePersistence.cs` (`Load` `:98`, `Save`
  `:168-214`).
- **Stores outside the vault.** `%LOCALAPPDATA%\Slate`
  (`FileRecentsStore.cs:32-36`; WB-1's `SlateDeviceState.Root` honouring
  `SLATE_LOCAL_APP_DATA`); `SidebarWriteJournal.cs:301` is the journalled
  store precedent. After WB-28 the Windows identity is the lossless
  `VaultRootIdentity`.
- **Interlocks.** `App.xaml.cs:151-159` `OnExit`; no `SessionEnding`
  handler. `IsExternallyStale` (`WorkspaceViewModel.cs:1827`) is the
  stale-tab flag the save path already honours. Re-seat by
  `capture_structural_identity` (`session.rs:16564`).

## Reference

- Plan 43 PR 29's design (to be written; WD20 fixes its contents).
- #1525's text (RD1): explicit save stands (#877), Close Vault still asks,
  storage keyed by vault identity outside the vault, written as you type
  (debounced), compared with disk on reopen, core-owned restore copy,
  interlocks (termination fence, layout save, layout restore, re-seat).
- Mac twin: PR 29's mac host half (the same store and FFI).
- Contract 38 D-10 (`AnnounceTeardown` doc `:1423-1432`): only the
  explicit Close speaks; application close is silent — unchanged here.

## What the shared design must have fixed before this starts

1. The store: core-owned, its FFI (`write_buffer(path, text,
   baseline_hash)` returning a durable write result, `read_buffers()`,
   `discard_buffer(path)`, `discard_vault()` — names per the design),
   under the host-supplied device-local root, keyed by `VaultRootIdentity`
   (WB-28's lossless form), bounded (per-buffer and per-vault caps, and
   what a write over the cap returns — a typed refusal the host can act
   on).
2. The write cadence (debounce; on every dirtiness change at least) and
   fsync policy; a write's result is durable-or-error, never fire-and-forget.
3. The restore rule: baseline hash equal → restore dirty; file changed →
   restore dirty and stale; file gone → a new unsaved note at the path;
   file moved (identity found elsewhere) → re-seat; a recovery entry with
   no tab in the saved layout → a new tab in the active group.
4. The open-sequence ordering (WD20's sentence) and the core copy:
   `A11yEvent::UnsavedNotesRestored { count }` and the per-buffer
   "changed on disk" notice (census rows, both corpus mirrors, §4 rule 6
   — PR 29's core PR adds them).
5. Interlocks: the layout save on close keeps running; Close Vault's
   prompt stays and, on Discard All, `discard_vault()` runs (Discard means
   discard).
6. **Every Discard route fences pending recovery writes before the
   durable discard.** The routes: Close Vault's Discard All; the Discard
   All of the prompt that application close and vault switch fall back to
   after a failed or timed-out flush (a timed-out flush can leave a
   `write_buffer` in flight while that prompt is open); and the
   per-buffer `discard_buffer` on a clean transition. The store's discard
   must supersede every write issued before it, including one that
   completes after it — for example a per-vault discard epoch that
   `write_buffer` carries and the store compares, the discard advancing
   it durably before it deletes, so an older epoch's write is dropped —
   and the design names the mechanism. Without it a late write leaves or
   re-creates a buffer the reader discarded, and the next open restores
   text the reader chose to throw away.

## Design

Windows only, consuming the merged core store.

- **Writing as you type.** `WorkspaceTabViewModel` raises a change signal
  at its `BumpContentGeneration` points (`:442`, `:478`); a new
  `HotExitWriter` (`WorkspaceViewModel.HotExit.cs`, new partial) debounces
  per tab on a dispatcher timer and calls `write_buffer(path, text,
  baselineHash)` tagged with the tab's `ContentGeneration`; a clean
  transition (`IsDirty` false) discards through the fence below
  (`FenceAndDiscard(tab)`), never a bare `discard_buffer`. The writer
  serialises through the save coordinator so a save's publication and a
  buffer write never interleave on one path (`HasAdmittedSaveFor`). It
  records, per tab, the last generation durably written.
- **Closing never asks — once the latest edits are durable.**
  `TryCloseWorkspaceCore` gains a route check for
  `TeardownRoute.ApplicationClose` and the switch: with dirty tabs it
  calls `HotExitWriter.FlushLatest(timeout: 5 s)` (new; synchronous on
  the dispatcher with a pumped wait whose `Wait(timeout)` result is
  asserted in facts) which writes every tab whose `ContentGeneration`
  exceeds its last durable generation and returns `FlushResult
  { Flushed, Failed(reason) }` (new). On `Flushed` the teardown returns
  `Closed` with no prompt (the layout saves as today). On `Failed` —
  disk full, access denied, the store's cap refusal, a timeout, a write
  still pending — the teardown falls back to today's prompt
  (`_confirmUnsavedClose`, Save All / Discard All / Cancel) with the
  reason in the status line (`ReportTerminalStatus`), so the only copy is
  never discarded silently. `TeardownRoute.Close` (Close Vault) keeps the
  prompt unconditionally. **Every Discard All — Close Vault's and the
  fallback prompt's on application close and switch — runs
  `HotExitWriter.FenceAndDiscardVault()`** (new), and a clean transition
  runs `HotExitWriter.FenceAndDiscard(tab)` (new): each first fences its
  scope (stops the scope's debounce timers and marks it fenced, so no new
  `write_buffer` is issued for it), then calls the store's discard
  (`discard_vault()` / `discard_buffer(path)`), which per the shared
  design's item 6 supersedes every write already in flight; a write that
  completes after the fence is ignored by the writer (no durable
  generation recorded, nothing re-issued). A tab's fence lasts until its
  next edit, whose writes are issued after the discard and so are newer
  than it; the vault fence lasts until the teardown completes. A
  timed-out flush therefore cannot bring a discarded buffer back.
- **Restoring, injected into the tabs the layout builds.**
  `VaultLifecycleViewModel` reads `session.ReadHotExitBuffers()` (the FFI
  name per the design) after the open scan and before `:1184`, and passes
  `IReadOnlyList<RestoredBuffer>` into the `WorkspaceViewModel`
  constructor as a new parameter. The constructor stores it and
  `Restore(_persistence.Load())` at `:2309` runs as today, but
  `RestoreNode` (`Persistence.cs:69-84`) consults it when it constructs
  each `WorkspaceTabViewModel`: a tab whose path (or identity, for a
  moved file) has a recovery entry is constructed with
  `RestoreUnsaved(text, baselineHash, changedOnDisk)` (new internal
  method on the tab) applied before `AttachTabDocumentsIfNeeded` (`:83`)
  and before `group.RestoreActive` (`:90`), so the activated, published
  tab is the dirty one. Entries no restored tab consumed are opened as
  new tabs in the active group after `Restore` returns and before
  `SyncPanels()` (`:2310`). `WorkspaceReady` (`:1280`) fires after; the
  host posts `UnsavedNotesRestored` once, after it. A restored tab takes
  `IsTransient = false` and `IsDirty = true` through the existing private
  setter; the single launch landing lands on the restored active tab.
- **Disk changed.** `changedOnDisk` sets `IsExternallyStale`, so the next
  `SaveAll`/Ctrl+S runs the existing conflict path; no new conflict code.
- Deliberately not changed: the Close Vault prompt; the save path;
  `WorkspacePersistence`; no new core module; no Windows-only store.

## Tests

xUnit `Noun.VerbPhrase`, in `SlateWindows.Tests/HotExitTests.cs` (new
class `HotExitTests`):

| Fact | Asserts | Mutation |
|---|---|---|
| `ATypedEditIsWrittenToTheStoreAfterTheDebounce` | after an edit and the timer, the store holds the text and baseline tagged with the generation | drop the write |
| `ACleanTabDiscardsItsBuffer` | save, or undo to clean: buffer gone | keep it |
| `ClosingTheWindowWithDirtyTabsFlushesThenNeverAsks` | an edit made after the last debounce; `PrepareForApplicationClose()` true, `_confirmUnsavedClose` never called, the store holds the final generation, file bytes unchanged | return `Closed` without flushing |
| `AFailedFlushFallsBackToThePromptAndDiscardsNothing` | the store's write seam returns `Failed` (disk full) → the prompt is invoked; Cancel keeps the window and the buffer in memory | return `Closed` on failure |
| `AFlushThatTimesOutFallsBackToThePrompt` | the write seam never completes; `Wait(5 s)` result false is asserted, the prompt follows, release in `finally` | treat timeout as flushed |
| `AWriteOverTheStoresCapFallsBackToThePrompt` | the cap refusal → prompt | — |
| `CloseVaultStillAsksAndDiscardAllDropsTheBuffers` | prompt invoked; Discard All calls `discard_vault()` through `FenceAndDiscardVault()`; a write the seam held in flight across the Discard, released after it, leaves `read_buffers()` empty | skip the discard; or discard without the fence (the late write's buffer survives) |
| `ATimedOutFlushThenDiscardAllLeavesNothingToRestore` (Theory: application close, vault switch) | the store's write seam holds one `write_buffer` in flight; the route's `FlushLatest` times out (its `Wait(5 s)` result false is asserted) and the fallback prompt answers Discard All; `discard_vault()` ran after the fence, and no `write_buffer` is issued after it; then the held write is released and completes late (its completion observed through an asserted `Wait(2 s)`, released in `finally`); reopening the vault over the same store root reads no buffers, builds no dirty tab and posts no `UnsavedNotesRestored` | the fallback route's Discard All without `discard_vault()` (the late write's buffer is restored); or the store discard before the fence (a debounce tick re-issues a write after it); or a store whose discard does not supersede an older in-flight write (the late write re-creates the buffer) |
| `ALateWriteAfterACleanTransitionDoesNotResurrectTheBuffer` | a `write_buffer` held in flight; the tab is saved (clean), so `FenceAndDiscard(tab)` runs; the held write is released and completes late (asserted `Wait(2 s)`, released in `finally`); reopening restores nothing for that path and the tab opens clean | a bare `discard_buffer` without the fence; or the writer recording the late completion as durable and re-issuing it |
| `ReopeningRestoresTheBuffersIntoTheTabsTheLayoutBuilds` | the tab instances in `Groups` after construction are dirty with the text; the active tab is the restored one; exactly one landing; `WorkspaceReady` after install (order recorded by a seam) | install into pre-built tabs |
| `ARecoveryEntryWithNoLayoutTabOpensANewDirtyTab` | a new tab in the active group, dirty | drop the entry |
| `ABufferWhoseFileChangedOnDiskRestoresDirtyAndStale` | `IsExternallyStale`; the next save takes the conflict path | clear the flag |
| `ABufferWhoseFileMovedReseatsOnItsNewPath` | identity match → tab on the new path | seat on the old path |
| `ARestoreSpeaksCoresSentenceOnce` | `UnsavedNotesRestored` posted once with the count | post per tab |
| `AWriteNeverInterleavesWithAnAdmittedSave` | with `HasAdmittedSaveFor` true, the writer waits; order recorded | write anyway |
| `A11yCorpusCensus` (existing) | PR 29's events present | — |
| FlaUI `ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote` (new partial file `ShellAccessibilityTests.HotExit.cs`) | type, close the window, relaunch, the tab is dirty with the text; NVDA log carries the restore sentence | — |
| existing open-order pins in `RescanTests` updated where the constructor gained a parameter | | |

Every `Wait(timeout)` in these facts asserts its boolean result and
releases its event in `finally`.

## Docs and registers

- Contract 38 D-10: append a dated amendment (closing never asks on both
  hosts once the latest edits are durable; a failed flush falls back to
  the prompt; Close Vault still asks; every Discard, the fallback
  prompt's included, fences pending recovery writes before it discards).
- `18_windows_port/w_c_matrix.md`: the Workspace tabs row's notification
  column gains the restore sentence; `18_windows_port/parity_matrix.md`:
  `python -I scripts/generate-parity-matrix.py --check`.
- `docs/plans/18_windows_port/a11y_trigger_designations.json` rows for the new events (PR 29
  adds the census rows); `python -I scripts/a11y_trigger_ledger.py --write`.
- Help docs: if `docs/help/` names the close prompt, update the sentence.
- PR body: `Refs #1525` (or `Closes #1525` if the mac half is already
  merged and field-passed), the NVDA phrases, "not delivered" items.

## Verification

PowerShell, repo root. Every filter is listed first and the block throws
unless each required fact is discovered under it; every command's
`$LASTEXITCODE` is checked (no output grepping for "Failed").

```powershell
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb29'
function Assert-Ok([string] $What) { if ($LASTEXITCODE -ne 0) { throw "$What exited $LASTEXITCODE" } }
./apps/slate-windows/generate-bindings.ps1 -Locked; Assert-Ok 'generate-bindings'   # PR 29's FFI
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release; Assert-Ok 'dotnet build'
$tests = 'apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj'
$filter = 'FullyQualifiedName~SlateWindows.Tests.HotExitTests|FullyQualifiedName~SlateWindows.Tests.RescanTests|FullyQualifiedName~SlateWindows.Tests.Censuses.A11yCorpusCensus'
$names = @(dotnet test $tests --no-build --configuration Release --list-tests --filter $filter | Select-String '^\s{4}\S' | ForEach-Object { ($_.Line.Trim() -split '\(', 2)[0] }); Assert-Ok 'test discovery'
$required = @('ATypedEditIsWrittenToTheStoreAfterTheDebounce', 'ACleanTabDiscardsItsBuffer', 'ClosingTheWindowWithDirtyTabsFlushesThenNeverAsks', 'AFailedFlushFallsBackToThePromptAndDiscardsNothing', 'AFlushThatTimesOutFallsBackToThePrompt', 'AWriteOverTheStoresCapFallsBackToThePrompt', 'CloseVaultStillAsksAndDiscardAllDropsTheBuffers', 'ATimedOutFlushThenDiscardAllLeavesNothingToRestore', 'ALateWriteAfterACleanTransitionDoesNotResurrectTheBuffer', 'ReopeningRestoresTheBuffersIntoTheTabsTheLayoutBuilds', 'ARecoveryEntryWithNoLayoutTabOpensANewDirtyTab', 'ABufferWhoseFileChangedOnDiskRestoresDirtyAndStale', 'ABufferWhoseFileMovedReseatsOnItsNewPath', 'ARestoreSpeaksCoresSentenceOnce', 'AWriteNeverInterleavesWithAnAdmittedSave') | ForEach-Object { "SlateWindows.Tests.HotExitTests.$_" }
$missing = @($required | Where-Object { $names -notcontains $_ })
if ($missing.Count -gt 0) { throw "the filter does not select: $($missing -join ', ')" }
if (-not ($names | Where-Object { $_ -like 'SlateWindows.Tests.Censuses.A11yCorpusCensus.*' })) { throw 'no A11yCorpusCensus fact discovered' }
dotnet test $tests --no-build --configuration Release --filter $filter; Assert-Ok 'HotExitTests, RescanTests, A11yCorpusCensus'
# NVDA closed, desktop free (the FlaUI facts live in the partial class ShellAccessibilityTests, so select by method name):
$at = 'apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj'
$atFilter = 'FullyQualifiedName~ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote'
$atNames = @(dotnet test $at --no-build --configuration Release --list-tests --filter $atFilter | Select-String '^\s{4}\S'); Assert-Ok 'FlaUI discovery'
if ($atNames.Count -lt 1) { throw "the FlaUI filter selects nothing: $atFilter" }
dotnet test $at --no-build --configuration Release --filter $atFilter; Assert-Ok 'FlaUI journey'
$cs = @('apps/slate-windows/src/SlateWindows/MainWindow.xaml.cs','apps/slate-windows/src/SlateWindows/VaultLifecycleViewModel.cs','apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs','apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Persistence.cs','apps/slate-windows/src/SlateWindows/WorkspaceViewModel.HotExit.cs','apps/slate-windows/tests/SlateWindows.Tests/HotExitTests.cs','apps/slate-windows/tests/SlateWindows.Tests/RescanTests.cs','apps/slate-windows/tests/SlateWindows.AccessibilityTests/ShellAccessibilityTests.HotExit.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $cs; Assert-Ok 'dotnet format'
python -I scripts/generate-parity-matrix.py --check; Assert-Ok 'parity matrix check'
python -I scripts/a11y_trigger_ledger.py --write; Assert-Ok 'trigger ledger'
python -I scripts/a11y_trigger_ledger.py --check; Assert-Ok 'trigger ledger check'
```

No Rust changes here (the store is PR 29's), so no cargo gates beyond the
binding regeneration. Mutation run per row, locally.

CI: `windows.yml` all lanes (FlaUI journey added). AT: NVDA full pass,
launched with `SLATE_LOCAL_APP_DATA` on a seeded root (§4 rule 4): (1)
type in a note, Alt+F4 — no dialog, the window closes; (2) relaunch and
open the vault — "Restored 1 unsaved note" (core's exact phrase) after
the vault-opened sentence, the dirty marker in the tab title, the caret
in the restored note; (3) change the file outside Slate first, then
reopen — the stale notice and, on Ctrl+S, the conflict sentence; (4)
Close Vault with a dirty tab — the prompt as today; (5) make the store
root read-only, type, Alt+F4 — the prompt with the reason in the status
line. Recorded in the PR body; the PR stays a draft until then.

## Acceptance

- [ ] WD20 recorded; PR 29's design and core store merged with green
      combined-head CI; WB-28 merged.
- [ ] The sixteen facts and the FlaUI journey pass, seen failing first.
- [ ] Application close and switch flush then never prompt; a failed or
      timed-out flush falls back to the prompt; Close Vault prompts;
      every Discard All (Close Vault's and the fallback prompt's) and every
      clean transition discards through the fence, so a write still in
      flight that completes after the discard restores nothing on reopen
      (timeout → Discard → late completion → reopen, on both routes).
- [ ] The shared design names how its discard supersedes an older
      in-flight write (its item 6); if it does not, this chunk does not
      start.
- [ ] Restore injected into `RestoreNode`'s tabs (the instances in
      `Groups` are the dirty ones); one landing; `WorkspaceReady` after.
- [ ] Registers: contract 38 amendment, matrices, ledger; NVDA pass
      recorded; `Refs`/`Closes #1525` per the closure matrix.
- [ ] Not delivered: anything the design deferred (named).

## Brief

Give Windows hot exit over the core store PR 29 ships. Pending decision:
WD20 (recommended: one shared design, core-owned store, start only after
its implementation is on `main`; else a Windows-only `HotExitStore.cs`
with a parity-matrix debt). Read PR 29's design and core PR first; then
`MainWindow.xaml.cs:1982-1992`, `VaultLifecycleViewModel.cs:76-82`,
`:210-216`, `:637-650`, `:1176-1192`, `:1272-1282`, `:1405-1570`,
`WorkspaceViewModel.cs:94`, `:138`, `:239-241`, `:430-452`, `:1827`,
`:2103`, `:2302-2316`, `:2753`, `:2816-2840`,
`WorkspaceViewModel.Persistence.cs:53-108`, `WorkspaceViewModel.Layout.cs:54`,
`WorkspaceSaveCoordinator.cs:80-87`, `WorkspacePersistence.cs:98`,
`:168-214`, `FileRecentsStore.cs:32-36`. Build: `HotExitWriter`
(`WorkspaceViewModel.HotExit.cs`: per-tab debounce, generation-tagged
`write_buffer`, a clean transition discarding through the fence below,
serialised behind
`HasAdmittedSaveFor`, `FlushLatest(timeout) -> FlushResult { Flushed,
Failed(reason) }`); the route check in `TryCloseWorkspaceCore`
(application close and switch: flush, then `Closed` without a prompt on
`Flushed`, today's prompt with the reason on `Failed`; Close Vault's
prompt unchanged; every Discard All through the fence below); the
recovery read in
`VaultLifecycleViewModel` before `:1184` passed into the
`WorkspaceViewModel` constructor; `RestoreNode` applying
`WorkspaceTabViewModel.RestoreUnsaved(text, baselineHash, changedOnDisk)`
to the tabs it constructs before attach and activation; leftover entries
opened as new dirty tabs; `UnsavedNotesRestored` posted once after
`WorkspaceReady`; and the discard fence — every Discard All (Close
Vault's, and the prompt application close and vault switch fall back to
after a failed or timed-out flush) runs `HotExitWriter.FenceAndDiscardVault()`
(new) and a clean transition runs `HotExitWriter.FenceAndDiscard(tab)`
(new): stop the scope's debounce timers and mark it fenced so no new
`write_buffer` is issued, then call `discard_vault()` /
`discard_buffer(path)`, and ignore any write that completes after the
fence. The shared design must already guarantee that its discard
supersedes every write issued before it, including one that completes
later (e.g. a durable per-vault discard epoch `write_buffer` carries);
if it does not, stop and raise it — this chunk does not start. Facts in
`HotExitTests`, seen failing first:
`ATypedEditIsWrittenToTheStoreAfterTheDebounce`, `ACleanTabDiscardsItsBuffer`,
`ClosingTheWindowWithDirtyTabsFlushesThenNeverAsks` (an edit after the
last debounce is durable before close), `AFailedFlushFallsBackToThePromptAndDiscardsNothing`,
`AFlushThatTimesOutFallsBackToThePrompt` (asserted `Wait` result, release
in `finally`), `AWriteOverTheStoresCapFallsBackToThePrompt`,
`CloseVaultStillAsksAndDiscardAllDropsTheBuffers` (Discard All through
`FenceAndDiscardVault()`; a write held in flight across it and released
after leaves no buffer),
`ATimedOutFlushThenDiscardAllLeavesNothingToRestore` (a Theory over
application close and vault switch: a held write makes `FlushLatest`
time out, asserted; the fallback prompt's Discard All runs
`discard_vault()` after the fence; the held write then completes late,
asserted `Wait(2 s)` released in `finally`; reopening reads no buffers,
builds no dirty tab and speaks no restore — mutations: no
`discard_vault()` on the fallback route, the discard before the fence, a
store discard that does not supersede the older write),
`ALateWriteAfterACleanTransitionDoesNotResurrectTheBuffer` (a held write
completing after a save's `FenceAndDiscard(tab)` restores nothing —
mutations: a bare `discard_buffer`, the late completion recorded as
durable), `ReopeningRestoresTheBuffersIntoTheTabsTheLayoutBuilds` (the `Groups`
instances are dirty; one landing; `WorkspaceReady` after),
`ARecoveryEntryWithNoLayoutTabOpensANewDirtyTab`,
`ABufferWhoseFileChangedOnDiskRestoresDirtyAndStale`,
`ABufferWhoseFileMovedReseatsOnItsNewPath`, `ARestoreSpeaksCoresSentenceOnce`,
`AWriteNeverInterleavesWithAnAdmittedSave`; the FlaUI journey
`ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote`.
Registers: contract 38 D-10 amendment, `w_c_matrix.md` Workspace tabs
row, `parity_matrix.md` (`--check`),
`docs/plans/18_windows_port/a11y_trigger_designations.json` and the
ledger (`--write`, then `--check`). Not delivered: whatever the design
defers. Done when the Verification block runs clean in PowerShell (the
fifteen `HotExitTests` facts and an `A11yCorpusCensus` fact discovered
by name, the FlaUI journey discovered, every `$LASTEXITCODE` 0), the
Windows lanes are green, each mutation was run locally, the
five-scenario NVDA pass is in the PR body, and the issue reference
follows the closure matrix. Commit:
`feat(windows): unsaved notes survive closing, quitting and a crash and come back unsaved when the vault reopens`.
