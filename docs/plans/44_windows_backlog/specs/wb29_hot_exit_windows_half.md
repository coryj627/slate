# WB-29 — Unsaved notes survive closing, quitting and a crash on Windows, and come back unsaved when the vault reopens

Issues: #1525 (Windows half; closes in whichever of WB-29 and plan 43
PR 29's mac half merges last — both hosts merged and field-passed, WD20;
earlier PRs write `Refs #1525`). Size L (design first, in plan 43 PR 29's
design). Lanes: windows (the core store comes from PR 29; this chunk adds
no core module). AT: NVDA full pass. Wave: W11, slot 2, after WB-28 and
after PR 29's core store is on `main` with green CI (§6: "WB-29 … rebases
and re-verifies the combined open, identity-admission and recovery flow
after `main`'s CI is green on WB-28"). Parallel with: WB-30. Decision:
WD20 (recommended and consumed as written).

## Goal

Closing the main window or quitting Slate on Windows with unsaved edits
never asks and never writes the notes. Each unsaved buffer is written as
you type to the hot-exit store PR 29 ships (outside the vault, keyed by
the vault's identity), so a crash or a kill keeps it too. When the vault
reopens, the buffers come back in their tabs, still marked unsaved, after
the open scan and before the layout restores, and the launch landing
happens once. A buffer whose file changed on disk meanwhile is restored
dirty and the existing save-time conflict machinery decides at the next
explicit save. Close Vault still asks (Save All, Discard All, Cancel).
Core owns the copy ("Restored 2 unsaved notes").

## Current state on main

Verified against the worktree; the plan's lines hold. **The shared
design does not exist yet:** `docs/plans/43_mac_backlog_delivery/` holds
designs 20, 21, 22 and the 24 trio only; `specs/` holds `pr22_…` only.
PR 29 is a row (`00_program.md:437`, "L (design first)"; RD1 as amended
`:557`: "the design decides whether core owns the store and whether
Windows adopts hot exit too"). Plan 44 WD20 fixes that the design covers
both hosts and that core owns the store. Nothing below can start until
that design and its core-store PRs are merged.

- **Closing asks today.** `MainWindow.xaml.cs:1986-1992` `Window_Closing`
  → `_viewModel.PrepareForApplicationClose()`; cancels the close on
  `false`. `VaultLifecycleViewModel.cs:637-650` `PrepareForApplicationClose`:
  refuses while busy, else `TryCloseWorkspace()` and
  `AnnounceTeardown(teardown, TeardownRoute.ApplicationClose)` (silent for
  that route, `:1433-1440`). `TryCloseWorkspace` `:1405` →
  `TryCloseWorkspaceCore` (`:1416-1568`): with `workspace.HasDirtyTabs`
  (`WorkspaceViewModel.Layout.cs:54`) it reads `DirtyTabsForPrompt()`
  (`WorkspaceViewModel.cs:2829`), calls `_confirmUnsavedClose()` (`:1538`;
  the `Func<VaultCloseDecision>` at `:78`, defaulted to `Cancel` `:213`),
  then `SaveAll()` (`:2753`) or, for Discard, requires `SavesIdle`
  (`:2816`) and `DirtyTabsStill(asked)` (`:2839`). The same core serves
  Close Vault (`:454`), vault switch (`:605`) and application close
  (`:644`) — the three routes share one prompt.
- **Dirty state.** `WorkspaceTabViewModel` (`WorkspaceViewModel.cs:94`)
  `IsDirty` `:435-451` (private set; bumps `ContentGeneration` `:239-241`,
  ends `IsTransient`); the buffer is `_editorSession` (`:430-432`
  `ReplaceAll`). `WorkspaceSaveCoordinator.HasAdmittedSaveFor`
  (`WorkspaceSaveCoordinator.cs:83-87`) is the admission seam WB-2 made
  reloads join; a restore must join it too.
- **The open sequence.** `VaultLifecycleViewModel.cs:1176-1192`
  `WorkspacePersistence(root).Load()` (`:1180`) then `new
  WorkspaceViewModel(session, root, …)` (`:1184`, constructor at
  `WorkspaceViewModel.cs:2103`), whose body restores the layout at
  `:2309` `(_root, _activeGroup) = Restore(_persistence.Load());` and
  `SyncPanels()`; `WorkspaceReady?.Invoke` at `:1280` after the sidebar
  and switcher are wired (`:1272-1279`). `OpenRecentAsync` `:724`. The
  layout store is `WorkspacePersistence.cs` (`Load` `:98`, `Save`
  `:168-214` through `AnchoredVaultStore`). The launch landing is the
  reading/editor landing slot WB-9 reworks (`EditorLandingSlot.cs`),
  armed once per open.
- **Stores outside the vault.** `%LOCALAPPDATA%\Slate` is the device-local
  root (`FileRecentsStore.cs:32-36`; WB-1's `SlateDeviceState.Root`
  honouring `SLATE_LOCAL_APP_DATA`); the sidebar write journal
  (`SidebarWriteJournal.cs:301`) is the precedent for a journalled store
  keyed by vault identity (#944/#1440). After WB-28 the Windows identity is
  the lossless `VaultRootIdentity` (its key form).
- **Interlocks.** `App.xaml.cs:151-159` `OnExit`; no `SessionEnding`
  handler (a Windows shutdown runs `Window_Closing` only if the session
  allows). Re-seat on a moved file: `WorkspaceTabViewModel` keeps tabs on
  the file through renames (contract 38 D-10, the `HasAdmittedSaveFor`
  doc); the canonical identity `capture_structural_identity`
  (`session.rs:16564`) is what a restored buffer re-seats by.

## Reference

- Plan 43 PR 29's design (to be written; WD20 fixes its contents: store
  schema, restore rule, open-sequence ordering — "recovery state read after
  the open scan and before the layout restores, `WorkspaceReady` raised
  only after restored buffers are installed, one launch landing").
- #1525's text (RD1): explicit save stands (#877), Close Vault still asks,
  storage keyed by vault identity and outside the vault, written as you
  type (debounced), compared with disk on reopen, core-owned restore copy,
  interlocks (termination fence, layout save, layout restore, re-seat).
- Mac twin: PR 29's mac host half (the same store and FFI).
- Windows prompt today: contract 38 D-10 (`AnnounceTeardown` doc
  `:1423-1432`): only the explicit Close speaks; application close is
  silent — unchanged here.

## What the shared design must have fixed before this starts

Each is a prerequisite; the chunk does not begin until PR 29's design
answers it and its core PRs are merged:

1. The store: core-owned (`HotExitStore` or the design's name), its FFI
   (`write_buffer(path, identity, text, baseline_hash)`,
   `read_buffers()`, `discard_buffer(path)` — names per the design),
   located under the host-supplied device-local root, keyed by
   `VaultRootIdentity` (WB-28's lossless form on Windows) and bounded
   (per-buffer and per-vault caps; what happens over the cap).
2. The write cadence (debounce interval; on every dirtiness change at
   least) and fsync policy.
3. The restore rule: a buffer whose on-disk hash equals its recorded
   baseline restores dirty and clean of conflict; one whose file changed
   restores dirty with the save-time conflict machinery armed; one whose
   file is gone restores as a new unsaved note at the path; one whose
   file moved (identity found elsewhere) re-seats.
4. The open-sequence ordering (WD20's sentence) and the core copy:
   `A11yEvent::UnsavedNotesRestored { count }` and the per-buffer
   "changed on disk" notice (census rows, both corpus mirrors, §4 rule 6
   — PR 29's core PR adds them, not this chunk).
5. The interlocks: the termination fence (mac PR 15b) has no Windows twin
   needed; the layout save on close keeps running; Close Vault's prompt
   stays and, on Discard All, the store's buffers for that vault are
   discarded too (so Discard means discard).

## Design

Windows only, consuming the merged core store.

- **Writing as you type.** `WorkspaceTabViewModel` subscribes to its
  editor session's change notification (the `BumpContentGeneration`
  points, `:442`, `:478`) and schedules a debounced
  `_hotExit.Write(path, text, baselineHash)` through a new
  `HotExitWriter` (new, in `WorkspaceViewModel.HotExit.cs`, a partial per
  §6's rule) on a dispatcher timer; a clean transition (`IsDirty` false)
  discards the buffer. The writer serialises through the save coordinator
  so a save's publication and a buffer write never interleave on one
  path (`HasAdmittedSaveFor`).
- **Closing never asks.** `TryCloseWorkspaceCore` gains a route check: for
  `TeardownRoute.ApplicationClose` (and the vault switch, per the design),
  `HasDirtyTabs` no longer opens the prompt; the buffers are already in
  the store (the writer flushes synchronously on close, bounded), the
  layout saves as today, and the teardown returns `Closed`.
  `TeardownRoute.Close` (Close Vault) keeps the prompt; its Discard All
  also calls `_hotExit.DiscardVault()`.
- **Restoring.** `VaultLifecycleViewModel` reads the store after the open
  scan and before `:1184`: `IReadOnlyList<RestoredBuffer> restored =
  session.ReadHotExitBuffers()` (the FFI name per the design), and passes
  it into the `WorkspaceViewModel` constructor as a new parameter;
  the constructor installs the buffers into their tabs *before* `:2309`'s
  `Restore(_persistence.Load())` runs, so the layout restore finds the
  tabs dirty and the single launch landing lands on the restored active
  tab; `WorkspaceReady` (`:1280`) fires after. The host posts the core
  event once, after `WorkspaceReady`. A restored buffer takes
  `IsTransient = false` and `IsDirty = true` through the existing private
  setter (an internal `RestoreUnsaved(text, baseline, changedOnDisk)`
  method, new).
- **Disk changed.** `changedOnDisk` marks the tab `IsExternallyStale`
  (the existing flag at `:1827`), so the next `SaveAll`/Ctrl+S runs the
  conflict path as it does for a stale tab today; no new conflict code.
- Deliberately not changed: the Close Vault prompt; the save path;
  `WorkspacePersistence`; no new core module; no Windows-only store.

What happens if the owner answers WD20 otherwise (Windows writes its own
store): this chunk grows a `HotExitStore.cs` mirroring the design's
schema and the identity key, and the FFI calls become local file IO —
recorded as a parity debt in `parity_matrix.md`.

## Tests

xUnit `Noun.VerbPhrase`, in `SlateWindows.Tests`:

| Fact | Asserts | Mutation |
|---|---|---|
| `HotExitTests.ATypedEditIsWrittenToTheStoreAfterTheDebounce` | after an edit and the timer, the store holds the text and baseline | drop the write |
| `HotExitTests.ACleanTabDiscardsItsBuffer` | save, or undo to clean: buffer gone | keep it |
| `HotExitTests.ClosingTheWindowWithDirtyTabsNeverAsksAndWritesNoNote` | `PrepareForApplicationClose()` true, `_confirmUnsavedClose` never called, file bytes unchanged, buffer present | route through the prompt |
| `HotExitTests.CloseVaultStillAsksAndDiscardAllDropsTheBuffers` | prompt invoked; Discard All empties the vault's buffers | skip the discard |
| `HotExitTests.ReopeningRestoresTheBuffersDirtyBeforeTheLayoutRestores` | tabs dirty, layout's active tab the restored one, exactly one landing, `WorkspaceReady` after install (order recorded by a seam) | install after `:2309` |
| `HotExitTests.ABufferWhoseFileChangedOnDiskRestoresDirtyAndStale` | `IsExternallyStale`, the next save takes the conflict path | clear the flag |
| `HotExitTests.ABufferWhoseFileMovedReseatsOnItsNewPath` | identity match → tab on the new path | seat on the old path |
| `HotExitTests.ARestoreSpeaksCoresSentenceOnce` | the `UnsavedNotesRestored` event posted once with the count | post per tab |
| `HotExitTests.AWriteNeverInterleavesWithAnAdmittedSave` | with `HasAdmittedSaveFor` true, the writer waits; order recorded | write anyway |
| `A11yCorpusCensus` (existing) | the new events present in `corpus.json` from PR 29 | — |
| FlaUI `ShellAccessibilityTests.HotExit_ClosingAndReopeningKeepsAnUnsavedNote` | type, close the window, relaunch, the tab is dirty with the text; NVDA log carries the restore sentence | — |
| `RescanTests` / `VaultLifecycleTests` existing open-order pins updated where the construction gained a parameter | | |

## Docs and registers

- Contract 38 D-10: append a dated amendment (closing never asks on both
  hosts; Close Vault still does) — core-owned copy rows for the new
  events (PR 29's core PR adds the census rows; this chunk cites them).
- `18_windows_port/w_c_matrix.md`: the Workspace tabs row's notification
  column gains the restore sentence; `parity_matrix.md` row for
  `slate.vault.close` semantics regenerated (`generate-parity-matrix.py
  --check`).
- Trigger ledger: `python -I scripts/a11y_trigger_ledger.py --write` after
  the designation rows for the new events.
- Help docs (`docs/help/` if the Windows help names the close prompt):
  update the sentence.
- PR body: `Refs #1525` (or `Closes #1525` if the mac half is already
  merged and field-passed), the NVDA phrases, "not delivered" items.

## Verification

```
$env:CARGO_TARGET_DIR='C:\dev\slate\target-wb29'
./apps/slate-windows/generate-bindings.ps1 -Locked           # PR 29's FFI
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --filter "FullyQualifiedName~HotExit|FullyQualifiedName~VaultLifecycle|FullyQualifiedName~RescanTests"
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --filter "FullyQualifiedName~HotExit"   # NVDA closed, desktop free
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include <edited .cs files>
python -I scripts/generate-parity-matrix.py --check
python -I scripts/a11y_trigger_ledger.py --write
```

CI: `windows.yml` all lanes (FlaUI journey added). AT: NVDA full pass,
launched with `SLATE_LOCAL_APP_DATA` on a seeded root (§4 rule 4, WB-1's
script): (1) type in a note, Alt+F4 — expect no dialog, the window
closes; (2) relaunch and open the vault — expect "Restored 1 unsaved
note" (core's exact phrase) after the vault-opened sentence, the tab
title with the dirty marker, the caret in the restored note; (3) change
the file outside Slate first, then reopen — expect the stale notice and,
on Ctrl+S, the conflict sentence; (4) Close Vault with a dirty tab —
expect the prompt as today. Recorded in the PR body; the PR stays a
draft until then.

## Acceptance

- [ ] PR 29's design and core store merged with green combined-head CI;
      WB-28 merged (the identity key).
- [ ] The nine facts and the FlaUI journey pass, seen failing first.
- [ ] Application close never prompts; Close Vault does; Discard All
      discards the store's buffers.
- [ ] Restore order proven (buffers → layout → `WorkspaceReady`, one
      landing).
- [ ] Registers: contract 38 amendment, matrices, ledger; NVDA pass
      recorded; `Refs`/`Closes #1525` per the closure matrix.
- [ ] Not delivered: anything the design deferred (named).

## Brief

Give Windows hot exit over the core store PR 29 ships. Read PR 29's
design and its core PR first; then `MainWindow.xaml.cs:1982-1992`,
`VaultLifecycleViewModel.cs:76-82`, `:210-216`, `:637-650`, `:1176-1192`,
`:1272-1282`, `:1405-1570`, `WorkspaceViewModel.cs:94`, `:239-241`,
`:430-452`, `:2103`, `:2302-2316`, `:2753`, `:2816-2840`,
`WorkspaceViewModel.Layout.cs:54`, `WorkspaceSaveCoordinator.cs:80-87`,
`WorkspacePersistence.cs:98`, `:168-214`, `FileRecentsStore.cs:32-36`.
Build the debounced writer, the route-aware teardown, the restore before
the layout restore with `WorkspaceReady` after it, and the stale/re-seat
handling; facts and the FlaUI journey per Tests, failing first; registers
per Docs. Done when the lanes are green and the NVDA pass is in the PR
body. Commit:
`feat(windows): unsaved notes survive closing, quitting and a crash and come back unsaved when the vault reopens`.
