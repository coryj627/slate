# PR 29 — On the Mac, unsaved notes survive quitting, a vault switch and a crash, and come back unsaved when the vault reopens

Issue: #1525 (the Mac half; RD1). Size L. Two PRs: **29.1** (core: the
restore plan both hosts need, `split_note` over the FFI, one reason
fragment) and **29.2** (the Mac host). Lanes: 29.1 is rust, swift
(compile) and windows (compile: bindings regenerate, nothing consumes
the additions); 29.2 is swift, a11y/VO, and windows only for the plan
documents its censuses read. AT: VoiceOver field pass (29.2). Wave:
plan 43 W1, slot 29, after plan 44 WB-29a's store is on `main`
(#1734 `114f737d`, amended by #1738 `43f77563`) and WB-29's Windows
half (#1873 `b8ca1c20`). Decision: RD1 (plan 43 §11.5), WD20 as answered
2026-10-26, and WB-29's owner calls of 2026-11-02 (call 1 amended the
store, D-1 added Close and keep the copies, call 3 stops a late discard
re-keeping text), which bind both hosts.

29.2 closes #1525: the Windows half is merged (#1873) and passed NVDA in
its PR body. The issue's remaining design questions are answered by
WB-29a (`44_windows_backlog/specs/wb29a_hot_exit_shared_design_and_core_store.md`)
and WB-29 (`44_windows_backlog/specs/wb29_hot_exit_windows_half.md`);
this spec cites them as **A§N** and **W§N**.

## Goal

Quitting Slate, or switching to another vault, with unsaved notes never
asks and never writes the notes into the vault. Every dirty tab claims
its own entry in core's hot-exit store (outside the vault, under
Application Support, keyed by the vault's lasting identity) and writes
its whole note there as you type, so a crash or a force quit keeps it
too. Quit and switch settle for at most 4 s for every dirty tab to be
kept and every store call the vault owes to resolve. When that cannot
land they fall back to today's prompt and say why; they never discard
the only copy. When the vault reopens, each kept buffer comes back in
its own tab, dirty. Core classifies it against the disk and plans it
(A§3, 29.1). It is installed into the documents the layout restore
adopts, before the first tab activates. A note that changed on disk
comes back stale, and its first save conflicts. Close Vault still asks.
Its Discard All, and the fallback prompt's on quit and switch, completes
only once the store's discard is durable; otherwise the reader may try
again, cancel, or close and keep the copies (D-1). Core owns every
sentence. A vault with no key (A§7: no volume UUID, not local, `msdos`
or `exfat`) keeps today's prompt and speaks core's reason.

Closing the main window is unchanged and already meets RD1: on the Mac
it neither quits nor closes the vault, and nothing prompts
(`SlateMacApp.swift:56-58`, no window-close hook). The vault stays open
behind the closed window, and its buffers keep being written.

## Current state on main (f5a75a47)

Paths under `apps/slate-mac/Sources/SlateMac/` unless stated.

- **No hot-exit code in the Mac app.** Nothing under `apps/slate-mac`
  references `HotExit`, `hot_exit`, `fileIdentity` or
  `readTextIdentified`. The seven events are in core and in the Mac
  corpus mirror (`Tests/SlateMacTests/A11yCorpusCensusTests.swift:396-404`),
  constructed nowhere on the Mac: `38_notification_dispatcher_contracts.md:1010-1013`
  and `:1051-1053` record "no Mac construction site until plan 43 PR 29
  adopts hot exit", designated in
  `docs/plans/18_windows_port/a11y_trigger_designations.json:322-352`.
- **The store and its session reads** (`crates/slate-uniffi/src/lib.rs`):
  `HotExitStore` `:9197-9310` (`open(deviceRoot)`, `currentEpoch`,
  `claimEntry(key, epoch, entryId, expected, incarnation)`,
  `retireEntry`, `holdVault`, `writeBuffer`, `releaseEntry`,
  `discardVault`, `readBuffers`); `newHotExitIncarnation()` `:9332`;
  the reason accessors `:9338-9373` and
  `hotExitDiscardPendingChoices()` `:9393`; on `VaultSession`
  `readTextIdentified` `:1401`, `saveTextChecked` `:1440-1458`,
  `hotExitKey` `:1463`, `noteFileIdentity` `:1476`,
  `classifyHotExitEntries` `:1483`; `NotePartsBundle.file_identity`
  `:3788-3799`; `SaveReport.file_identity` `:5794`. `compose_note`
  is exported (`:273`); `split_note` is not
  (`crates/slate-core/src/session.rs:9534` uses it internally).
- **Two homes for a buffer.** The active tab's note is a set of
  `@Published` fields on `AppState`: `currentNoteText` `AppState.swift:4613`,
  `savedBaselineText` `:4618`, `currentNoteContentHash` `:4629`,
  `loadedFilePath` `:4635`, `hasUnsavedChanges` `:4640`, plus
  `currentNoteFMSource`, `bodyByteOffset` and `bodyLineOffset`. Each
  inactive tab parks in a `NoteDocument` (`Workspace/NoteDocument.swift:21-49`:
  `id`, `path`, `text`, `hasUnsavedChanges`, `savedBaselineText`,
  `contentHash`, `fmSource`, the offsets, `isMissingFromDisk`,
  `hasLoaded`), kept in `WorkspaceState.documents`. A tab switch
  (`activateTab`, `:3319-3401`) parks the outgoing fields and restores
  the incoming tab through `restoreParkedOrLoadFromDisk` (`:10152-10189`).
  That reads the parked document when `hasLoaded`, else starts
  `loadCurrentNote`. The buffer is the note's **body**; the frontmatter
  parks beside it and a save composes the two (`save_composed`).
- **Dirtiness.** `updateEditorText` (`:14723-14744`) sets the body and
  recomputes `hasUnsavedChanges` against `savedBaselineText`, so undo to
  the baseline turns clean, then mirrors into same-path parked documents
  (`WorkspaceState.mirrorEdit` `Workspace/WorkspaceState.swift:356-363`).
  A same-path tab never activated has no document. `mirrorSaveResult`
  (`:367-378`) moves a landed save's baseline into every same-path
  document. A tab turns clean at a landed save (`performSave` `:14896`,
  success `:15055-15074`), undo, a load (`loadCurrentNote` `:12952`,
  `:12999-13009`), Reload from disk (`resolveSaveConflictReloadFromDisk`
  `:15262`), and a rescan reload (`AppState+Rescan.swift:1558`,
  `WorkspaceState.reloadCleanDocuments` `:553-566`).
- **The baseline has a hash and no identity.** `loadCurrentNote` sets
  `currentNoteContentHash = parts.contentHash` (`:13001`) from one
  `readNoteParts`. `refreshNoteParts` sets it at `:3962`. A landed save
  sets it from the report (`:15069`). The identity the bundle and the
  report carry is dropped.
- **The save.** `saveCurrentNote` (`:14792-14843`) captures
  `expected = currentNoteContentHash`. `performSave` runs
  `AppState.writeNoteSave` (`AppState+CanonicalIdentity.swift:134-162`)
  detached: `saveComposed(path, fmSource, body, expectedContentHash)`
  when the hash is set (`:157-161`), else an exclusive create through
  `createReportingLanding`, whose `DestinationExists` becomes
  `WriteConflict` (`:150-156`). `WriteConflict` builds
  `currentSaveConflict` (`:15122-15138`).
- **Tab ids are stable.** `TabID { raw: UUID }`
  (`Workspace/WorkspaceModel.swift:20-24`) persists in
  `<vault>/.slate/workspace.json` (`Workspace/WorkspaceStore.swift:110-128`,
  written `:327-328`, restored `TabID(raw:)` `:410`); the store's header
  says unsaved buffers are not persisted (`:17-20`). `uuidString` (36
  characters, upper-case hex and hyphens) is a valid store entry id
  (at most 64 of `[0-9A-Za-z_-]`, A§1).
- **The open.** `openVault(at:)` (`:10546-10901`, synchronous on the
  main actor): the session opens synchronously
  (`VaultSession.openFilesystem` `:10591`), `currentSession` `:10653`;
  `loadSidebarVaultPreferences` `:10661`; WB-28's root-identity
  admission (`:10670-10677`, `VaultOpenIdentityRaceError`);
  `workspace.reset()` `:10795`; `restoreWorkspaceLayout()` `:10827`
  (`:4314-4339`: `WorkspaceStore.load`, `workspace.adopt` with **empty
  documents** (`WorkspaceState.swift:705-726`), then `activateTab` of
  the active group's active tab); the scan task `:10834-10859`. The
  vault-opened line is posted by `MainSplitView`'s `.onAppear`
  (`MainSplitView.swift:946-951`) on a first open, and by
  `announceDirectVaultSwitch` (`:10891`, body `:11379-11384`) when a
  vault replaces an open one.
- **Quit asks today (#784, PR 15b).** `SlateAppDelegate.applicationShouldTerminate`
  (`SlateMacApp.swift:42-66`; seam `terminationDecision` `:85-93`) calls
  `admitTermination(reply:)` (`AppState.swift:11927-11949`):
  `.terminateNow` when clean and the sidebar fence is clear,
  `.terminateLater` while `presentTerminationPrompt` (`:11955-11980`, the
  Close Vault prompts) or the fence holds, `.terminateCancel` on a
  refusal or a second quit. `finishPendingTermination` (`:11996-12016`)
  answers once; a quit never closes the vault. The
  `willTerminateNotification` observer saves the layout
  (`:4271-4279`).
- **Switch and Close Vault ask.** `switchToRecent` (`:11412-11451`) and
  `closeVaultFromUserAction` (`:11676-11702`) route a dirty vault to the
  single-note alert (`attemptCloseVault` `:24846-24865`,
  `pendingNavigation = .closeVault`) or the Save All / Discard All /
  Cancel prompt (`pendingVaultClose`, `MainSplitView.swift:518-557`).
  The resolvers: `resolveVaultCloseSaveAll` `:11757-11845`,
  `resolveVaultCloseDiscardAll` `:11849-11873` (in termination mode it
  answers the quit true at once: the process drops the buffers),
  `resolveVaultCloseCancel` `:11875-11881`. `closeVault()`
  (`:12064-12384`) saves the layout (`:12114`), resets the workspace
  (`:12281`) and opens a pending switch target at its tail
  (`:12373-12379`).
- **Per-tab discards.** `requestCloseTab` (`:3446-3546`) gates a dirty
  tab unless a sibling keeps the path (`dirtyBufferSurvivesElsewhere`
  `:9920-9931`); `resolveTabCloseDiscard` `:3590`; `performCloseTab`
  `:3629-3687`; `closeActivePane` (`:4574-4585`) closes tab by tab
  through the same gate; replacing the active tab's note with another
  note gates at `handleSelectionChange` (`:9911-9955`,
  `pendingNavigation = .selectFile`) and discards in
  `resolvePendingNavigationDiscard` (`:24721-24761`), but a canvas or
  base opened into the tab reroutes before that gate (`:9896-9909`);
  `replaceActiveItem` drops the parked document
  (`WorkspaceState.swift:610-626`). `requestCloseTab` treats a dirty tab
  as clean when any other markdown tab has its path
  (`hasOtherMarkdownTab` `:23190-23197`), loaded or not.
- **Drafts that are not tab buffers** also make a close dirty:
  uncommitted property drafts and missing-note recovery drafts
  (`uncommittedNoteDraftPaths` `:22678`, `dirtyVaultClosePaths`
  `:23222-23236`).
- **Device-local stores** use `~/Library/Application Support/Slate/`
  (`RecentVaultsStore.swift:143-151`). The sidebar journal is installed
  only by the app (`installProductionSidebarWriteJournal` `:5300-5305`,
  called `SlateMacApp.swift:355-357`); XCTest uses a temp directory
  (`:5296-5297`) and `installSidebarWriteJournalForTesting` (`:5308`).
- **Announcements** render core's `A11yEvent` through
  `AnnouncementPosting.post` (`AnnouncementPosting.swift:30-42`); the
  injected `announcer` (`AppState.swift:9058`).
- **Tests to mirror.** `TerminationFlowTests.swift` (`makeState`,
  `makeVault`, `openVault` `:71-135`, `quit()` through the delegate's
  seam), `WorkspaceTabsTests.swift`, `WorkspaceStoreTests.swift:365`
  (close then reopen restores the layout), `SidebarUtilityBarTests.swift`
  (switch).

## Design

The Mac half of the one design. **Every rule of W§0 (the model:
episodes, obligations, "kept", O1–O7) holds here unchanged**, and is not
restated; this section names its Mac seats. Where the Mac adds a choice
it says so, and each choice is listed under Deviations.

### 0. What differs from Windows, in one place

1. **Two homes for a buffer.** A tab's snapshot is read from the active
   fields while it is the loaded active tab, else from its
   `NoteDocument` (§3).
2. **The buffer is the body.** An entry's `text` is the whole note,
   `compose_note(fmSource, body)` (the bytes a save would write); a
   restore splits it with core's `split_note` (29.1).
3. **One `AppState` per process** owns the workspace for every window,
   so no two windows of one process ever hold one vault's buffers.
   The vault hold (W§1) still guards a second Slate process
   (`open -n`).
4. **The open is synchronous on the main actor**, and so is its
   hot-exit read (§2, Deviation 1).
5. **Quit is `.terminateLater`**, not a window close. The settle runs
   inside it with the run loop turning, as the sidebar fence does
   today (§4).
6. **Restore planning is core's** (29.1), not the host's: Windows plans
   peers and recovered names in C# (`WorkspaceViewModel.HotExit.cs:40-240`);
   the Mac calls core's plan, and Windows' adoption of it is a filed
   follow-up (§Docs).
7. **Drafts that are not tab buffers are not kept** (§4, Deviation 2).

### 1. The store host: one per process, joined by nothing

`HotExitStoreHost` (new, `HotExit/HotExitStoreHost.swift`, `final class`,
`@unchecked Sendable`, its state under one `NSLock`) owns the one
`HotExitStore`, the obligation ledger per vault key (O1, O2's per-entry
queues), every call in flight, and each vault's hold, exactly as W§1:

- **Installation.** `AppState` takes a host at init (`hotExitHost:`);
  the default is a host **with no store**, so every existing test keeps
  today's prompt and posts nothing (W§6's uninstalled host).
  `installProductionHotExitStore()` (`@MainActor static`, beside
  `installProductionSidebarWriteJournal`, called at the same site in
  `SlateMacApp.swift`) creates `~/Library/Application Support/Slate`
  if missing and makes its name durable in its parent
  (`F_FULLFSYNC` on the parent directory's descriptor; A§2's macOS
  sequence), on every install, then `HotExitStore.open(root)`. Any
  failure installs a host with no store and core's reason
  (`HotExitError`'s reason, else `hotExitReasonStoreUnwritable()`),
  logged once to the unified log (`Logger`, subsystem `Slate`,
  category `hot-exit`).
- **Calls** run each on its own work item of a dedicated concurrent
  `DispatchQueue` (`com.slate.hot-exit`, `.utility`), never the Swift
  cooperative pool, so a stalled store pins no thread a save or
  `Task` needs. A body converts the result to an outcome on that
  thread and resumes its waiter through a `CheckedContinuation`; it
  never throws. `beforeCallForTests` / `afterCallForTests`
  (`((HotExitStoreCall) -> HotExitFault?)?`) as W§1.
- **Parked retirements retry themselves** (1 s doubling to 30 s), as
  W§1. **Holds** are acquired at open, reused by a reopen in this
  process, and released once the vault is closed and no obligation of
  its key is unresolved. A `Damaged` retirement resolves with its
  reason (W§1).
- **Nothing joins it.** No teardown, `closeVault` or quit awaits a
  store call beyond a settle's budget. The calls take the key, never
  the session (A§8). `simulateProcessDeathForTests()` drops every hold
  and resolves every call held in `before` unissued (W§Tests).

### 2. Keys, the open and entry ids

In `openVault(at:)`, after WB-28's admission (`:10670-10677`) and before
`restoreWorkspaceLayout()` (`:10827`), on the main actor,
`hotExitHost.open(session:)` returns a `HotExitOpen`:

1. `session.hotExitKey()`; `Unavailable { reason }`, or a host with no
   store, gives `.none(reason)`.
2. Acquire (or reuse) the vault's hold; `HeldElsewhere` gives
   `.none(heldElsewhere)`, any other error `.none(storeUnwritable)`.
3. **Any unresolved obligation of the key from an earlier session in
   this process gives `.none(earlierWorkPending)` without waiting**
   (Deviation 1: the Mac open is synchronous; W§2 waits 4 s on its
   load worker). The parked retirements are re-issued once first, so
   the next open usually has hot exit again.
4. `readBuffers(key)`; `Damaged` gives `.damaged(key, reason)` (the
   writer inert, Close Vault's Discard All repairs, W§6); any other
   error `.none(reason)` (no Discard All touches the store, W§2).
5. `session.planHotExitRestore(snapshot, layoutEntryIds)` (29.1) with
   the entry ids of the layout's **markdown** tabs, taken from the
   validated model `WorkspaceStore.model(from:)` builds (it can refuse a
   snapshot or drop tabs, `Workspace/WorkspaceStore.swift:380-384`), not
   from the raw snapshot, so no entry is planned into a tab that is
   never built. A tab whose item is a canvas, base or other kind holds
   no entry id here: an entry under its id (left by an ungated replace,
   §3) opens as a new tab with a fresh `TabID`, keeping the entry's id.

Any failure of `openVault` after step 2 releases the hold (W§1), and the
writer starts only once the open has installed its session; the open's
own `clearActiveNoteFields()` (`:10633`) and `workspace.reset()`
(`:10794`) run with no vault open in production (`openVault(at:)` is
reached only from the welcome flow and `closeVault`'s tail,
`:11473-11476`, `:11512-11515`, `:12375`), where the writer holds
nothing; a debug assertion pins that precondition.

**Entry ids.** A tab gains `recoveryEntryId` (in
`WorkspaceState.recoveryEntryIds: [TabID: String]`, defaulting to
`id.raw.uuidString`): a tab made in this process, or a layout tab whose
`uuidString` equals an entry's id, uses `uuidString`; a planned new tab
whose entry id round-trips through `UUID(uuidString:)` to the same
string gets that `TabID`; any other entry id keeps a fresh `TabID` and
the entry's id for the tab's life (W§2, W Deviation 4).

### 3. Writing as you type

`HotExitWriter` (new, `HotExit/HotExitWriter.swift`, `@MainActor`), held
by `AppState`, per tab id: `recoveryEntryId`, the episode token, the live
claim, the next generation, the last snapshot issued and the last kept,
whether the claim was lost, and the inherited incarnations (W§3, W§5).

- **The snapshot.** `(path, body, fmSource, baselineHash,
  baselineFileIdentity)`, read from the active fields when the tab is
  the active group's active tab and `loadedFilePath` is set, else from
  its `NoteDocument` when `hasLoaded`; a tab with neither has no buffer
  and is not dirty. The tab is dirty when that source's
  `hasUnsavedChanges` is true. The entry's `text` is
  `composeNote(fmSource:body:)`, computed on the dispatching work item,
  not on the main actor.
- **Observation (O7).** A `bufferRevision` counter on `AppState` (bumped
  by `didSet` on each active field above, `hasUnsavedChanges`,
  `currentNoteFileIdentity` and the identity enforcement flag) and on
  `NoteDocument` (the same for its fields), and `WorkspaceState`'s
  model, `documents` map and `recoveryEntryIds` changes (a `didSet` on
  each, so a dirty document removed without a model change —
  `discardMissingDocuments(forPath:)`, `WorkspaceState.swift:583-594` —
  is seen), schedule one coalesced
  re-evaluation, posted with `DispatchQueue.main.async` so it runs after
  the mutation's caller returns. `didSet` sees every write, so the
  observation needs no census of write sites. A re-evaluation walks the
  dirty tabs and the tabs it holds state for, compares each snapshot
  with the last issued, and acts as W§3: a tab turned dirty claims and
  writes at once; a changed body is written 1 s after the last change
  (one `Task.sleep` per tab, cancelled by the next change); a changed
  path, baseline hash or identity is written at once; at most one write
  in flight per tab; outcomes per O3.
- **The identity travels with the hash (W§3).** `AppState` gains
  `currentNoteFileIdentity` and `NoteDocument` gains `fileIdentity`.
  Every statement that assigns a content hash assigns the identity
  beside it, from the same core result: `readNoteParts`'s bundle
  (`loadCurrentNote` `:13001`, `refreshNoteParts` `:3962`), a
  `SaveReport` (`performSave` `:15069`, Keep Mine, the property and
  frontmatter writes that advance the hash, the task toggle), a
  `PublishedUnindexed` or committed create, a restored entry, another
  buffer's baseline (tab park and restore, `mirrorSaveResult`,
  `mirrorFrontmatter`), or nil beside a nil hash. The census
  `HostCodeNeverPairsAHashWithASeparateIdentity` pins it (§Tests).
- **Ended by the reader → retire (O4), only on the reader's word.** An
  episode is retired only when the reader discarded it through a gate
  or it turned clean: `resolveTabCloseDiscard`, the navigation
  `resolvePendingNavigationDiscard` (each tells the writer before it
  mutates), `discardMissingNoteRecoveryDraft` (`:23310-23334`, an
  explicit discard of a recovery draft), closing a **clean** tab, and a
  clean transition seen by the re-evaluation (save landed, undo, reload,
  a peer's save mirrored). One `retireEntry` per ended episode naming
  every incarnation it holds and inherited, as W§3.
- **A dirty buffer dropped without a gate is abandoned, never
  retired.** Two routes on `main` drop a dirty buffer the reader never
  discarded: opening a canvas or base into a dirty markdown tab
  (`handleSelectionChange` reroutes before the dirty gate,
  `:9896-9909`; `openCanvasFile` parks then `replaceActiveItem`s,
  `Canvas/AppState+Canvas.swift:70-80`; Bases the same,
  `Bases/AppState+Bases.swift:496`, `:572`, `:638`), and ⌘W on a dirty
  tab whose same-path sibling was never loaded
  (`hasOtherMarkdownTab` `:23190-23197` treats the closing tab as
  clean; the sibling has no `NoteDocument`, so `mirrorEdit` never
  reached it). An ungated close whose sibling does hold a loaded buffer
  with the same text and path retires (the sibling's own entry keeps
  the text); otherwise the writer abandons the episode (O4): the store
  keeps the text, and the next open restores it as a new tab (§2 step
  5), so hot exit turns these silent losses into a restore. Both gaps
  are filed (#1932, #1933); this PR does not change those routes.
- **Abandoned → nothing retired.** `HotExitWriter.detach()` runs in
  `closeVault()` **after its refusal guards** (`:12065-12086`), just
  before `invalidateNoteAuthoringOwnership()` (`:12101`) and so before
  `workspace.reset()` (`:12281`) and `clearActiveNoteFields()`, which
  would otherwise read as clean transitions; a refused `closeVault()`
  leaves the writer attached. Disposal without a route (a quit)
  retires nothing either.
- **A save and a recovery write never wait on each other** (W§3,
  W Deviation 1).

### 4. Quitting, switching and Close Vault

- **The re-check, after every settle that leads to a close.** Every
  settle whose success ends in `closeVault()` or a quit's reply true —
  the kept quit and switch, a clean Close Vault, Save All's last save,
  the single-note alert's Save that reaches `applyPendingNavigation(.closeVault)`
  (`:24792-24796`), Try again, and a committed Discard All's
  `.retireOnly` settle — is followed by W§4's re-check: no save in
  flight, and the dirty set, each buffer's `bufferRevision` and
  `AppState.draftRevision` unchanged since the settle began (for a
  Discard All: against the covered buffers and the drafts it covered).
  `draftRevision` (new) is bumped by a `didSet` on every store the
  close prompts read for drafts that are not tab buffers —
  `propertiesSourceDraft` (`:22004`), `parkedPropertiesSourceDrafts`
  (`:22053`) and `missingNoteRecoveryDrafts` (`:22210`), the stores
  behind `uncommittedNoteDraftPaths` and `dirtyVaultClosePaths`
  (`:22678`, `:23117-23135`, `:23222-23236`) — so an edit to a property
  draft already dirty when the reader chose (which changes no tab
  snapshot and no dirty path; `updatePropertiesSourceDraft` `:22021`)
  still fails the re-check. Those drafts have no kept copy (Deviation
  2), so this re-check is their only guard. A failed re-check never closes and never opens the
  discard-pending prompt: it goes back to the route's own decision,
  which for Close Vault, a switch and a quit is today's prompt with the
  new dirty count, so text typed during a settle, or after a Discard
  All was chosen, is kept or asked about (owner call 3), never dropped.
- **Quit.** `admitTermination` (`:11927-11949`) runs, in order: the
  refusals as today; then, when the vault is dirty or its key has an
  unresolved obligation (and the host has a store and a key), it
  returns `.terminateLater`, marks the reply outstanding, sets
  `pendingTerminationSettle` (new; the reply it owes) and starts
  `hotExitTerminationTask` (test hook), which **first waits for the
  sidebar fence** (`settleSidebarWriterChainsForTermination`,
  `:5363-5380`, so no keystroke can land after the last re-check), then
  `await writer.settle(budget: 4 s, mode: .keep)` and the re-check.
  Kept and re-checked → reply true (silent, as a quit is today).
  NotKept(reason) → post `UnsavedNotesNotKept { reason }` (High), then
  `presentTerminationPrompt()`, whose every result now has a reply:
  `.presented` hands the reply to the prompt as today; `.clean` (the
  reader saved or discarded meanwhile) settles again; `.refused`
  replies false. A newer request during the settle — Close Vault, a
  switch, an open, a file switch — supersedes the quit as
  `supersedePendingTermination` does a prompt (it reads
  `pendingTerminationSettle` too): the task is cancelled and the quit
  answered false. A second ⌘Q is `.terminateCancel` as today. Whenever
  a prompt presents during a quit (the fallback prompt or the
  discard-pending prompt), a new `AppState.frontPromptHostForTermination()`
  activates the app and fronts `promptHostWindow`, or reopens the main
  window when it is closed — the re-front `applicationShouldTerminate`
  does today (`SlateMacApp.swift:51-61`), moved where every presentation
  can call it. An uninstalled host behaves as on `main`.
- **Vault switch.** `switchToRecent` with dirty tabs (and `openVault`
  over a dirty vault, which reroutes there, `:10562`) runs the same
  settle in a task (`hotExitSwitchTask`): kept and re-checked →
  `closeVault()` (which detaches the writer after its guards, so
  nothing retires) and the target opens at its tail, spoken as today's
  switch; a refused `closeVault()` leaves the vault open with the
  writer attached; NotKept → `UnsavedNotesNotKept { reason }`, then
  today's prompts.
- **Every successful close settles its obligations** (O5): before a
  clean quit replies, before a clean switch or Close Vault calls
  `closeVault()`, and after Save All's last save, the route settles and
  re-checks; `NotKept` with nothing dirty opens the discard-pending
  prompt below.
- **Close Vault still asks**, settles nothing before its prompt, and
  saves as today; Save All's clean transitions retire.
- **Every Discard All fences (O6), and nothing is reverted until it
  commits.** Close Vault's, and the fallback prompt's on quit and switch
  (including the single-note alert's Discard for `.closeVault`): the
  resolver runs the fence **before** it mutates any buffer —
  `resolveVaultCloseDiscardAll`'s `hasUnsavedChanges = false`
  (`:11869-11870`) and `resolvePendingNavigationDiscard`'s revert to the
  baseline (`:24750-24755`) move after the commit. The writer records
  the covered buffers, queues their retirements, fences the vault,
  issues `discardVault(key)` (or waits for the one in flight) and
  awaits it at most 4 s on the main actor (the run loop turns; the
  fence and the re-check, not a blocked thread, keep it safe).
  Committed → settle `.retireOnly` → re-check → the resolver's revert,
  then `ClosedChangesDiscarded` (quit: reply true). Not committed → the
  discard-pending prompt, every buffer still as the reader left it
  (covered ones discarded-unkept, W§4), so Cancel loses nothing.
  **Today's termination-mode Discard All, which answers the quit at
  once (`:11864-11866`), now waits for the fence first**, so a Discard
  All before ⌘Q never brings the text back at the next launch.
- **The discard-pending prompt (D-1).** A new `@Published
  pendingHotExitDiscard: HotExitDiscardPendingPrompt?` drives an alert
  in `MainSplitView` beside the vault-close prompt: its message is
  `UnsavedNotesNotDiscarded` rendered, its buttons core's labels
  (`hotExitDiscardPendingChoices()`: Try again, Close and keep the
  copies, Cancel), Cancel the cancel role. Try again settles once more
  (never a round); Close and keep the copies abandons every episode and
  closes (`ClosedCopiesKept`; Close Vault speaks `vaultClosed` once,
  contract 38 D-10; a quit replies true; a switch opens its target);
  Cancel keeps the vault open (a quit replies false). The alert hosts
  in `promptHostWindow`, fronted during a quit by
  `frontPromptHostForTermination()`.
- **A dirty tab's own Discard** ends its episode before the tab is
  closed or re-pointed (§3).
- **Drafts that are not tab buffers.** A quit or switch with an
  uncommitted property draft or a registry-only missing-note recovery
  draft settles the tab buffers, then falls back to today's prompt with
  `UnsavedNotesNotKept { reason: hotExitReasonDraftsNotKept() }`
  (29.1): "unsaved property changes and notes recovered from the Trash
  are not kept" (Deviation 2).

### 5. Restoring into the tabs the layout adopts

`restoreWorkspaceLayout` takes the `HotExitOpen`. After
`workspace.adopt(...)` and **before** `activateTab`:

- **A layout tab the plan names** gets a parked `NoteDocument` built
  from its planned entry: `split_note(text)` gives `fmSource`, `body`
  and the offsets; `hasLoaded = true`; `hasUnsavedChanges = true`;
  `path` the planned path. `Restore` and `RestoreStale`:
  `contentHash`/`fileIdentity` are the entry's baseline and
  `restoredIdentityEnforced = true`. For `Restore`, `savedBaselineText`
  is the disk body read through `readNoteParts`, used only when that
  read's hash equals the entry's baseline; otherwise, and for every
  other decision, the baseline text is unknown: `savedBaselineText` is
  a non-optional `String` (`NoteDocument.swift:27`), so a new flag
  `baselineTextUnknown` (on the document and the active fields, carried
  by park and restore) keeps `hasUnsavedChanges` true until a save
  lands or a reload replaces the buffer — `updateEditorText` computes
  `baselineTextUnknown || !matches(text, baseline)` — so emptying a
  restored buffer never reads as clean and retires it. `RestoreStale`'s
  first save conflicts. `ExistsNow`, `Gone` and `Recovered`: a new
  unsaved note, nil hash and identity, so the first save is the
  exclusive create, which refuses a file now there. A planned path
  differing from the layout's re-points the tab's item first
  (`.markdown(path:)`, `Workspace/WorkspaceModel.swift:158`, keeping its
  `TabID`). A `Restore` entry whose text equals the disk body restores
  clean and is retired at once.
- **Same-path siblings with no entry of their own are seeded.** A
  saved layout can hold several markdown tabs on one note while only
  the tab that was dirty and active had a buffer, and so an entry. Every
  markdown layout tab whose path equals a restored tab's planned path
  (`BaseExactIdentity.matches`, the rule `mirrorEdit` and
  `mirrorSaveResult` use, `Workspace/WorkspaceState.swift:356-378`) and
  that the plan names no entry for gets the same parked document: the
  restored text (the latest, where peers were reconciled), its
  baseline hash, identity, `restoredIdentityEnforced` and
  `baselineTextUnknown`, `hasLoaded = true`, dirty. Without this, the
  sibling's first activation reads the disk through
  `restoreParkedOrLoadFromDisk` (`:10152-10189`) and its first edit
  mirrors the disk text over the restored buffer (`mirrorEdit`), which
  the writer then keeps. Seeded siblings claim their own entries at
  their first change, as any dirty peer does (the peer rule, A§1); a
  further crash restores both entries with one text, which the plan
  restores as peers, never as a recovered note. Windows propagates
  restored state to its peers the same way
  (`MirrorSamePathDocumentState`).
- **A planned new tab** opens in the active group after the layout's
  tabs, in `written_at` order, without taking the focus; a layout that
  failed to load or held no tabs gets a fresh model for them.
- **Displaced layout tabs** (a recovered peer took their id, 29.1) take
  a fresh `TabID` before `adopt`, with their group's active tab
  remapped.
- **Then `activateTab`** as today: the active tab's document has
  `hasLoaded`, so `restoreParkedOrLoadFromDisk` installs the restored
  buffer and no disk read (or load error for a `Gone` path) runs first.
- **Each restored tab starts its episode adopting** (W§5): it claims
  `expected` = the snapshot's claim for its id and carries both
  inherited incarnations. **An orphan claim** (a claim with no payload)
  whose id a markdown layout tab holds is that tab's inherited
  incarnation: the tab claims expecting it on its first edit, and
  retires it if the reader closes or re-points the tab first (W§2,
  `WorkspaceViewModel.HotExit.cs:1406-1417`); an orphan no tab holds,
  and the plan's redundant entries, are retired once the workspace is
  adopted.
- **A restored baseline's identity is enforced** while
  `restoredIdentityEnforced` (carried by park/restore and copied by
  every path that copies a baseline): `writeNoteSave` gains
  `expectedFileIdentity`, and with both hash and identity set it saves
  through `composeNote` + `saveTextChecked`; a landed save clears the
  flag (for its peers too, through `mirrorSaveResult`). **Keep Mine
  clears it**: `resolveSaveConflictKeepMine` (`:15166-15240`) re-saves
  with the conflict's current hash and no identity — the reader's
  explicit overwrite of the file now there, which `WriteConflict` cannot
  describe by identity (`SaveConflict` carries none, `:136-150`) — so a
  replaced file's conflict can be resolved as any other.
- **The copy, once, after the opening line.** `AppState` keeps
  `pendingHotExitNotices: [A11yEvent]` (`UnsavedNotesRestored { count }`
  then each non-`Restore` decision's notice, in plan order). One new
  seam, `AppState.announceVaultOpened(vaultTitle:sidebarNotice:)`,
  posts the vault-opened line through the injected `announcer` and then
  drains the notices; `MainSplitView`'s `.onAppear`
  (`MainSplitView.swift:946-951`, today the global
  `postAccessibilityAnnouncement`) and `announceDirectVaultSwitch` call
  it, so the order is one function's and a recording announcer sees
  both. Nothing is spoken when nothing was restored.

### 6. A vault with no key, or a read that failed

As W§6: the writer is inert (no claim, no write); a clean close is
unaffected; quit and switch with dirty tabs keep today's prompt and post
`UnsavedNotesNotKept { reason }`, except an uninstalled host, silent as
on `main`; after `Damaged` every Discard All calls `discardVault` (the
repair), otherwise none. A crash keeps nothing, as on `main`.

### 7. The core additions (29.1)

- **`VaultSession::plan_hot_exit_restore(snapshot, layout_entry_ids:
  Vec<String>) -> HotExitRestorePlan`**: classifies the snapshot's
  entries (A§3, through `classify_hot_exit_entries`) and applies W§5's
  rules, ported from `HotExitRestorePlan.For`
  (`WorkspaceViewModel.HotExit.cs:77-197`) with one change: **peers are
  grouped by the file the filesystem says their paths name, never by a
  case rule** (contract 32 I1, `32_canonical_file_identity_contracts.md:118-131`:
  a global case rule merges distinct files on a case-sensitive volume).
  Two entries are peers when both are `Restore`/`RestoreStale` (a file
  exists at the path) and `canonical_path` (the provider's stored
  spelling, `session.rs:4186-4189`) answers the same spelling for both;
  where it answers `None` or fails, an entry's path is its own key
  (exact bytes), so it is never merged. On a case-insensitive volume
  `A.md` and `a.md` name one file and are peers; on a case-sensitive
  volume they are two files, each restored with its own text. Windows'
  `StringComparer.OrdinalIgnoreCase` grouping (`:115`) merges the
  latter; #1934 moves Windows onto this rule. Peers give the latest
  `written_at` text to every tab on the path; each other
  distinct text becomes a `Recovered` note at `{stem} (recovered).md`
  (then `(recovered 2)`, …), never on a name this plan already took or
  a file at that path (a name the plan took is compared without case,
  which can only skip a free name, never merge two notes), keeping its
  entry's id; a second peer with the
  same distinct text is redundant (retired, never a second note); a
  layout tab whose id a recovered peer took is displaced. Records:
  `HotExitRestorePlan { layout: Vec<HotExitPlannedTab>, displaced:
  Vec<String>, new_tabs: Vec<HotExitPlannedTab>, orphan_claims:
  Vec<HotExitCurrentClaim>, redundant: Vec<HotExitRedundantEntry {
  entry_id, incarnations }> }`; `HotExitPlannedTab { entry_id, entry,
  decision, path, text, observed_claim: Option<String> }`. A displaced
  layout tab keeps showing the latest text through its own
  `HotExitPlannedTab` under the id the host gives it (the host maps the
  displaced id to a fresh one). The host's id format stays the host's.
- **`split_note(text) -> NoteParts { fm_source, body, body_byte_offset,
  body_line_offset }`** over the FFI (`#[uniffi::export]`, beside
  `compose_note`), the inverse `read_note_parts` already uses.
- **`HOT_EXIT_REASON_DRAFTS_NOT_KEPT`** ("unsaved property changes and
  notes recovered from the Trash are not kept") with its accessor
  `hot_exit_reason_drafts_not_kept()`, beside the other reason
  fragments (`a11y.rs:3552-3570`, accessors `lib.rs:9338-9373`). A
  reason is a fragment of `UnsavedNotesNotKept`, not a corpus row:
  core's `every_hot_exit_reason_completes_the_not_kept_sentence` and the
  uniffi accessor test pin it, and no corpus fixture or host mirror
  moves.
- **Facts** (core, `session` tests): the plan's cases, each with a
  mutation it kills (§Tests); `compose_note(split_note(t)) == t` for
  every composed `t` over the corpus `compose_note` uses (`lib.rs:15432`)
  — byte equality of the note, not of the parts, since `compose_note`
  canonicalises some frontmatter (`frontmatter.rs:327-345`); the
  fragment renders.

### Deliberately not changed

The Close Vault prompt and its single-note alert; the save path except a
restored baseline's identity check and the identity taken from the
load's bundle and the landed save's report; `WorkspaceStore` and its
schema; closing the window; the sidebar fence (a hot-exit quit waits
for it before its settle); the two ungated routes that drop a dirty
buffer (filed, §3).

### Deviations, for the owner and Codex

W's Deviations 1, 3, 6, 7, 10, 11 and 14 hold on the Mac as written
(no `Admit`; no re-seat; D-1; the crash shape in facts is detach
without a route; a crash inside a baseline write's latency restores
`ExistsNow`; core's store-wide creation lock; Discard All repairs a
damaged vault). W Deviations 2, 4, 5 and 9 hold with the Mac's names
(the fallback prompt is Close Vault's; a non-canonical entry id; a
second process gets no hot exit). W Deviations 8, 12 and 13 are replaced
by:

1. **The hot-exit read runs synchronously on the main actor, at the
   open, and never waits.** The Mac open is synchronous today (the
   session opens on the main actor, `:10591`); the read is bounded by
   the store's caps (64 MiB, 256 entries per vault). A reopen in this
   process that meets an earlier session's unresolved obligation has
   no hot exit that session (core's earlier-work-pending reason) rather
   than wait 4 s on the main actor; W waits 4 s on its load worker.
   Reachable only after Close and keep the copies or a store fault.
2. **Drafts that are not tab buffers are not kept.** Uncommitted
   property drafts and registry-only recovery drafts have no tab
   buffer to claim; a quit or switch with one falls back to the prompt
   with core's new reason. Windows has no such drafts.
3. **A quit settles inside `.terminateLater`**: AppKit waits as long as
   the reply is owed, so the 4 s budget is the host's, not AppKit's; a
   logout or shutdown that AppKit ends first keeps what was durable, as
   a crash does (there is no `SessionEnding` analogue to add).
4. **Restore planning is core's on the Mac and C#'s on Windows** until
   #1934 moves Windows to it. 29.1's facts pin W's cases, with one
   difference: core groups peers by the filesystem's stored spelling
   (contract 32 I1), where Windows' C# groups them without case, which
   merges two distinct files on a case-sensitive NTFS directory. #1934
   fixes that by consuming core.
5. **An ungated drop abandons rather than retires** (§3). A canvas or
   base opened into a dirty tab, and ⌘W on a dirty tab whose sibling was
   never loaded, lose the buffer on `main` without asking; with hot
   exit the store keeps it and the next open restores it as a new tab.
   The reader may meet a note they believed gone; never a loss. The
   routes themselves are filed, not changed here.
6. **A restored buffer's baseline text can be unknown**
   (`baselineTextUnknown`, §5): undo to the original text does not turn
   it clean before a save or a reload, where an ordinary tab would.

## Tests

### 29.1 (core)

| Fact | Proves | Mutation it kills |
|---|---|---|
| `a_plan_restores_each_entry_into_the_layout_tab_with_its_id` | layout ids matched exactly; unmatched entries are new tabs in `written_at` order | match by path |
| `divergent_peers_give_the_latest_text_to_every_tab_and_recover_the_rest` | latest to each peer; each other distinct text a `Recovered` note keeping its id; the holder displaced | keep only the latest |
| `a_second_peer_with_the_same_text_is_redundant` | one recovered note per distinct text; the redundant entry's incarnations (payload and observed claim) listed | a second recovered note |
| `a_recovered_name_never_lands_on_a_file_or_a_taken_name` | `(recovered)`, `(recovered 2)` past a file on disk and a name the plan took | ignore the disk |
| `peers_are_the_entries_whose_paths_name_one_file` | a provider whose `canonical_path` maps `A.md` and `a.md` to one stored spelling (a case-insensitive volume): one peer group, the latest text to both tabs | exact-path grouping |
| `distinct_files_differing_only_in_case_are_never_peers` | a provider with both `A.md` and `a.md` stored (a case-sensitive volume): two groups, each tab restored with its own entry's text, no recovered note, nothing redundant | case-insensitive grouping (Windows' rule) |
| `an_unanswered_canonical_path_never_merges` | `canonical_path` errors for one entry: it is its own group | fall back to a case rule |
| `orphan_claims_are_claims_with_no_payload` | listed with their ids, a payload-less claim never planned as an entry | drop them |
| `split_note_inverts_compose_note` | `compose_note(split_note(t)) == t` for each composed corpus note; offsets equal `read_note_parts`' | compare the parts; re-derive offsets |
| `the_drafts_reason_renders_inside_not_kept` | the sentence and its priority | — |

### 29.2 (Mac)

XCTest `HotExitTests.swift` (new), each over its own temp vault and its
own `HotExitStoreHost` on a temp device root (the real core store),
`AppState` built as `TerminationFlowTests.makeState` with
`hotExitHost:`. A held call blocks in `beforeCallForTests` (its start
observed with a bounded wait, released in `defer`). "Reopening" is
`openVault(at:)` again in the same `AppState`; "a new process" is
`simulateProcessDeathForTests()` and a fresh host and `AppState` on the
same device root; "the crash shape" is `writer.detach()` with no route.
Facts wait for the writer's own completion seams (`settleForTests()`),
never on wall-clock sleeps beyond the 1 s debounce they test (the
flaky-seam rule: `await task?.value` is not a completion seam).

The W§Tests table carries over fact for fact, Swift-named
(`testADirtyTabClaimsAndWritesAtOnce`, …), except the Windows-only
`SigningOutFlushesWithoutAPrompt`, `ASessionEndThatIgnoresCancelAbandons`
and `ADiscardWaitNeverDeadlocksTheDispatcher`; `AReopenWaitsForTheLastSessionsObligations`
becomes `testAReopenWithEarlierWorkPendingHasNoHotExit` (Deviation 1).
The Mac adds:

| Fact | Asserts | Mutation |
|---|---|---|
| `testTheSnapshotFollowsTheBufferBetweenFieldsAndDocument` | edit the active tab, switch away, edit nothing, switch back, edit: one episode, generations rising, the entry's text the composed note throughout | read the fields only |
| `testAnEntryHoldsTheComposedNote` | frontmatter plus body; restore splits back to both | write the body only |
| `testAKeptQuitRepliesWithoutAPrompt` | dirty tabs; ⌘Q through `terminationDecision`: `.terminateLater`, no prompt, reply true after the settle and the fence; a new process restores each tab dirty, the file unchanged | prompt as on `main` |
| `testAQuitThatCannotKeepFallsBackWithItsReason` (cases: write error, cap refusal, timeout, no key) | `UnsavedNotesNotKept` with core's reason, then today's prompt; Cancel replies false | quit anyway |
| `testDiscardAllOnTheQuitPromptFencesTheStoreBeforeTheReply` | the fallback prompt's Discard All: `discardVault` committed before reply true; a new process restores nothing | reply at once (`main`'s shape) |
| `testAKeptSwitchOpensTheTargetWithoutAPrompt` | switch with dirty tabs: target open, no prompt; switching back restores them | prompt |
| `testCloseVaultTearsDownWithoutRetiring` | a kept switch's `closeVault()` resets the workspace: no retirement issued; the entries remain | observe the reset as clean |
| `testTheActiveRestoredTabNeverReadsTheDisk` | a `Gone` active tab: no load error, no `readNoteParts` before the restored buffer shows | activate before injecting |
| `testARestoredStaleTabsFirstSaveConflicts` | `currentSaveConflict` set, the file untouched | keep the disk's hash |
| `testAReplacedFileIsNeverOverwritten` | the file replaced with the same bytes after the entry was kept: the save refused | save through `saveComposed` |
| `testDraftsThatAreNotBuffersKeepThePrompt` (cases: a property draft, a recovery draft) | the quit prompts with the drafts reason; the tab buffers are kept | treat the vault as kept |
| `testTheRestoreSpeaksAfterTheOpeningLine` (cases: first open, switch) | the recording announcer hears the opening line, then `UnsavedNotesRestored`, then the notices, once | post at install |
| `testTheDiscardPendingPromptUsesCoresLabels` | message and three labels are core's; each choice's outcome | hard-coded labels |
| `testAClosedWindowKeepsWriting` | the window closed (no window); an edit through `updateEditorText` is written | — |
| `testInstallationSyncsTheDeviceRootIntoItsParent` | the parent synced before `open`; a failed sync installs no store, its reason logged | sync only a root this call made |
| `testTypingDuringASettleIsAskedAboutNotDropped` (cases: kept quit, kept switch, clean Close Vault, Save All, the single-note Save, a committed Discard All) | an edit posted into the settle's frame: the route does not close; Close Vault's prompt opens with the new count; the text is kept or saved | close on a settle's success without the re-check; or open the discard-pending prompt |
| `testAFailedDiscardKeepsEveryBufferAsTheReaderLeftIt` (cases: Discard All, the single-note Discard) | `discardVault` held past 4 s, Cancel: the editor still shows the edited text, dirty | revert before the commit (`main`'s order) |
| `testAnUngatedDropIsAbandonedNotRetired` (cases: a canvas opened into the dirty tab; ⌘W with a never-loaded sibling; ⌘W with a loaded dirty sibling) | the first two: the entry stays and a new process restores it as a new tab; the third retires (the sibling's entry holds the text) | retire at `replaceActiveItem` or every `performCloseTab` |
| `testARefusedCloseVaultKeepsTheWriterAttached` | a native import starts during a kept switch's settle: `closeVault()` refuses, a later edit is written | detach at `closeVault`'s start |
| `testEveryQuitPromptResultHasAReply` (cases: `.presented` then Cancel, `.clean`, `.refused`, superseded by Close Vault during the settle) | AppKit's reply is sent exactly once each time and a later ⌘Q is admitted | leave the reply outstanding |
| `testAPromptDuringAQuitWithTheWindowClosedReopensIt` | no window; the settle is NotKept: the main window reopens and hosts the prompt | present into no window |
| `testKeepMineResolvesAReplacedFilesConflict` | a restored tab, its file replaced: the save conflicts; Keep Mine saves over it | keep the restored identity on Keep Mine |
| `testEmptyingARestoredBufferStaysDirty` | a `RestoreStale` tab cleared to empty text: still dirty, its entry kept | compare against an empty baseline |
| `testAnOrphanClaimIsInheritedByItsLayoutTab` | a claim with no payload under a layout tab's id: the tab's first edit claims expecting it and is kept; the tab closed first retires it | retire every orphan at the landing |
| `testARestoreSeedsSamePathSiblingsWithoutEntries` | a layout with two tabs on `a.md`, one entry (the active tab's); after reopening, the other tab activated shows the restored text, dirty, with no disk read; edited, the edit mirrors into the restored tab; a kept close and a new process restore the edited text in both | leave the sibling to load from disk |
| `testEditingAPropertyDraftDuringADiscardAsksAgain` | a dirty note and a dirty properties-source draft on one path; Discard All with `discardVault` held; the draft edited through `updatePropertiesSourceDraft`; the discard commits: the route does not close and Close Vault's prompt opens again | re-check tab snapshots and dirty paths only |
| `testEveryDraftStoreBumpsTheDraftRevision` (census, source scan) | every stored property `uncommittedNoteDraftPaths`, `propertyRecoveryPaths` and `dirtyVaultClosePaths` read has a `didSet` that bumps `draftRevision` | a new draft store without the bump |
| `testADiscardedRecoveryDraftIsRetired` | `discardMissingNoteRecoveryDraft` removes a dirty parked document: its entry is retired with no other trigger | observe the model only |
| `HostCodeNeverPairsAHashWithASeparateIdentity` (census, source scan of `Sources/SlateMac`) | no call to `noteFileIdentity`; every assignment to `currentNoteContentHash` or a document's `contentHash` assigns the identity in the same block from a named source | set a hash without its identity |

`A11yCorpusCensusTests` (existing): its "consumed by a Mac site" list
gains the seven events (29.1 adds no corpus row: the drafts reason is a
fragment of `UnsavedNotesNotKept`). Every existing `TerminationFlowTests`, `WorkspaceTabsTests`
and switch test runs unchanged (no store installed).

## Accessibility

- What VoiceOver speaks changes (a quit no longer asks; the restore
  speaks; a new prompt), so 29.2 carries a VoiceOver field pass
  (`scripts/vo.sh`, `docs/runbooks/voiceover-feature-test.md`) with the
  phrases recorded in its PR: (1) type, ⌘Q: no prompt; relaunch:
  "Restored 1 unsaved note." after the vault's opening line, the tab
  dirty; (2) a note changed on disk while Slate was closed: the
  changed-on-disk sentence; ⌘S: the conflict dialog; (3) `kill -9`
  after the debounce, relaunch: restored; (4) a vault on an exFAT disk
  image: ⌘Q speaks the not-kept sentence with the weak-root reason and
  prompts; (5) the discard-pending prompt (a held store through a test
  launch flag): its message and three buttons read.
- `a11y-check` stays at 100 (the new alert carries labels and hints as
  the vault-close prompt does).
- A surface a watcher can republish: a restored tab's buffer survives a
  vault-watcher republish of its own file (a fact:
  `testARestoredBufferSurvivesAWatcherRepublish`).

## Docs and registers

- `38_notification_dispatcher_contracts.md:1010-1013`, `:1051-1053`: the
  Mac column becomes the construction sites (`HotExitWriter`,
  `AppState+HotExit.swift`); the designations leave
  `a11y_trigger_designations.json`; `python3 -I scripts/a11y_trigger_ledger.py
  --write`, then `--check`.
- Contract 38 D-10: a dated Mac amendment (quit and switch close kept
  without a prompt; the fallback prompt with core's reason; the
  discard-pending prompt; Close Vault still asks).
- `00_program.md:442` (row 29) points at this spec; §11.4 records 29.1
  and 29.2.
- WB-29a's spec: a dated note that the restore plan is core's (29.1).
- Filed with this spec: #1934 (Windows: `HotExitRestorePlan.For`
  consumes core's `plan_hot_exit_restore`); #1932 (Mac: opening a
  canvas or base into a dirty markdown tab drops the buffer without the
  dirty gate); #1933 (Mac: ⌘W on a dirty tab whose same-path sibling was
  never loaded closes without a prompt and the sibling loads clean from
  disk). 29.2's body refers to them; it closes none of them.
- `parity_matrix.md`: no new command, chord or leaf; rerun
  `python3 scripts/generate-parity-matrix.py --check` to prove it.
- Help docs: if `docs/help/` describes quitting with unsaved edits,
  update the sentence.

## Verification

29.1: `make ci`; `cargo nextest run -p slate-core hot_exit`;
`make regenerate-bindings`; the Mac build
(`./scripts/build-mac-app.sh --skip-a11y-check`) and
`A11yCorpusCensusTests`; `windows.yml` proves the C# side (no
consumer).

29.2: `./scripts/build-mac-app.sh` (a11y-check at 100); from
`apps/slate-mac`: `DYLD_LIBRARY_PATH="$PWD/../../target/debug" swift test
--filter HotExitTests`, then `TerminationFlowTests`, `WorkspaceTabsTests`,
`WorkspaceStoreTests`, `SidebarUtilityBarTests`, `A11yCorpusCensusTests`,
never piped to `tail`; the a11y trigger ledger `--check`; the parity
matrix `--check`; the VoiceOver pass above.

## Acceptance

- ⌘Q with dirty tabs on a keyed vault quits without a prompt, and the
  next launch restores each tab dirty with its text; the file is
  unchanged.
- A crash after the debounce restores the same.
- A note changed on disk restores stale and its first save conflicts; a
  replaced file is never overwritten.
- Close Vault asks; its Discard All, and the quit prompt's, leaves
  nothing to restore, or opens the discard-pending prompt.
- A vault with no key, a held vault, or a draft that is not a buffer
  keeps the prompt and speaks core's reason.
- The restore speaks once, after the opening line, in core's words.
- Every existing termination, tab and switch test passes unchanged.

## Review record

- **Red team, round 1 (2026-11-13; a fresh Claude agent against the
  tree at f5a75a47).** Thirteen findings, all folded in: the re-check
  after every settle that leads to a close; retire only on the reader's
  word, an ungated drop abandons (#1932, #1933); every quit-prompt
  result has a reply, and `frontPromptHostForTermination`; the fence
  before any revert; detach after `closeVault`'s guards; Keep Mine
  clears identity enforcement; the sidebar fence before the quit's
  settle; orphan claims inherited by their layout tab; the round trip as
  byte equality; one announcement seam; validated layout ids; the
  `documents` map observed; citations.
- **Codex adversarial review, round 1 (2026-11-13).** Three findings,
  all folded in: same-path layout siblings with no entry are seeded
  with the restored buffer (§5); `draftRevision` joins every re-check,
  so a property draft edited during a Discard All asks again (§4);
  peers are grouped by the filesystem's stored spelling, never a case
  rule (§7, contract 32 I1). Also corrected: the drafts reason is a
  fragment, not a corpus row (§7).

## Brief

Branch `claude/pr29-1-hot-exit-restore-plan` from `main` for 29.1, then
`claude/pr29-2-hot-exit-mac` after 29.1 merges. Read A§1–§8 and the
amendment, W§0–§6, `WorkspaceViewModel.HotExit.cs:40-240` (the plan to
port) and `HotExitStoreHost.cs` (the store host to mirror), then this
spec's Current state against the tree. Write each fact first and show
it fail. 29.2's body: `Closes #1525`, the VoiceOver phrases, the
follow-up's number, and `not delivered:` for anything not verified.
