# Reading 03 — the save chain, vault lifecycle and other-session changes

Cluster: #1889 (class sweep), #1730, #1683, #1714, #1900.
Baseline: worktree `<worktree>` at 1ec9bf31.
Method: every issue read in full; every cited file opened at the baseline; `git log` checked for
the cited files and issue numbers. Nothing was built or run. A statement I did not check in the
code says **unverified**.

Paths below are relative to `apps/slate-windows/src/SlateWindows/` unless they start with
`crates/` or `tests/`.

None of the five is fixed on `main`. `git log --grep` finds no merge for #1889, #1730, #1683,
#1714 or #1900; the only related merges are #1659 (WB-2), #1885 (#1657), #1888 (#1658), #1715
(WB-23) and #1898 (the test that surfaced #1900).

Two results change the plan's sketch:

- **#1683 and #1900 are both core defects in the write-intent repair code** (plan 21's protocol,
  `crates/slate-core/src/session.rs`), not Windows host defects. Both causes are found by reading.
  The brief files them under "Windows host" and "SQLite"; they belong together in a core chunk.
- **The #1889 sweep found five more unjoined writers of an existing note** beyond the three the
  issue names, one of them with no compare-and-swap at all (the saved-query export), plus a
  family of exclusive creates.

---

## #1889 — the bulk-rename apply, RecoverDeletedFile and the rescan reload guard don't join a note's save chain

- **Verdict:** stands. All three named routes are as described. One line reference has drifted
  (the reload guard is at `WorkspaceViewModel.Rescan.cs:788-789`, not `:536`). The sweep adds
  siblings (cluster section).

- **Current state:**
  - The mechanism: `WorkspaceSaveCoordinator.cs` — `HasAdmittedSaveFor` `:83-87`, `Enqueue`
    `:94-131`, `Admit` `:150-170`, `ContinueChain` `:210-223`, `TrackWorker` `:258-275`,
    `SettlePumping` `:280-291`, `CloseAndJoinWorkers` `:297-318`. `Admit` takes one path;
    `Enqueue` already takes a list of keys and makes the ticket the tail of each.
  - **Bulk-rename apply.** `Panels/BulkRenameViewModel.cs`: `Apply()` `:168`, its only gate is
    `_anyOpenDraftDirty()` `:174` (properties-header drafts; it does not look at a tab's
    `IsDirty` or at pending saves). `Run` `:215`; the worker is `StartWork` `:236` calling
    `RenamePropertyByIdentityAcrossVault` / `RenamePropertyAcrossVault` `:243-245`. No `Admit`,
    no `TrackWorker`. `PublishRun` `:264` calls `_reconcileTabs(report)` `:282`, which is
    `WorkspaceViewModel.Properties.cs:668` `ReconcileTabsAfterBulkRename` →
    `ReconcileTabsAfterPropertyWrite` `:695` → `WorkspaceTabViewModel.Properties.cs:101`
    `RebaselineAfterPropertyWrite`, which reloads a clean tab's buffer from disk
    (`ReplaceAll` `:134`) without consulting `HasPendingSaves` or `HasAdmittedSaveFor`. So this
    route is both an unjoined writer and an unjoined reload.
    Teardown only cancels it (`WorkspaceViewModel.cs:3837-3839`); the save coordinator does not
    join its worker.
  - **`RecoverDeletedFile`.** `WorkspaceViewModel.History.cs:514-565`; the core call is `:529`
    on `RunHistoryWork` (`:636-644`, tracked only by `TrackRetiredBasesWork`, the bounded
    drain). No `Admit`. Its catch takes `VaultException` only (`:531-538`), so a panic through
    the binding escapes the worker and `completed` never runs — the fault WB-2 fixed for the
    restore (`RestoreAdmitted`), still present here.
    Core side, verified: `crates/slate-core/src/session.rs:6276` `recover_deleted_file` →
    `recover_remnant_to` `:6315` → `create_exclusive_binding` `:6335`. It is an exclusive
    create: it refuses an occupied path with `DestinationExists`.
  - **The rescan reload guard.** `WorkspaceViewModel.Rescan.cs:788-789`, in
    `ReloadMarkdownPathAsync` (`:767`):
    `saveInFlight = PropertyWriteInFlightFor(path) || live.Any(tab => IsMarkdownTabAt(tab, path) && (tab.IsTaskToggleInFlight || tab.HasPendingSaves))`.
    It does not ask `_saves.HasAdmittedSaveFor(path)`. The reload is `:832`
    (`ReloadKeepingCaretLine`). The two re-seats do ask it, through `KeepsBufferOnReseat`
    (`WorkspaceViewModel.cs:3724-3730`; callers `:3687` and `Rescan.cs:625`), and so does the
    unadmitted history reload (`History.cs:368-370`).
    What the guard misses today: an admitted History restore (it holds no property lease, no
    toggle flag and no tab save). The issue's "harmless today" holds by reading: the restore's
    continuation reloads again through `ReloadOpenTabFromDiskAdmitted` (`History.cs:391-401`).

- **Cause:** verified by reading. Each route predates `Admit` (#1659) and was listed for
  checking, not changed, by #1885 and #1888.

- **Fix shape:**
  1. Reload guard: add `|| _saves.HasAdmittedSaveFor(path)` to `Rescan.cs:788`. One line; the
     tab then takes the existing "stale against the index hash" arm instead of a reload.
  2. `RecoverDeletedFile`: wrap the worker in `_saves.Admit(path, …)`, hand the worker to
     `TrackWorker`, catch every fault but the fatal three, settle the ticket on every path
     (the `RestoreAdmitted` shape, `History.cs:195-330`). Same for the two sibling creates in
     the file if the plan joins them (`CommitRestoreAsDeleted` `:573`, `CommitRestoreAsVersion`
     `:434`); see the sweep for the alternative.
  3. Bulk-rename apply: needs a new primitive, because it rewrites many notes. Recommended:
     `WorkspaceSaveCoordinator.Admit(IReadOnlyCollection<string> paths, …)` over the folded keys
     of every open Markdown tab's path at apply time (the only paths that can hold an admitted
     save or a tab to reload). The ticket ends after `PublishRun`'s reconcile. The worker goes
     to `TrackWorker`. `BulkRenameViewModel` gets the admission as a delegate from
     `CreateBulkRename` (`Properties.cs:642`). Teardown must cancel the run **before** the
     save settle, or Close Vault waits for the whole apply.
  4. The siblings and the census (cluster section).
  5. `docs/plans/38_notification_dispatcher_contracts.md` trigger rows, if any announcement's
     construction site moves (`A11yTriggerParityCensus` pins them; #1885 and #1888 both had to).

- **Failing test first:** `tests/SlateWindows.Tests/SaveChainAdmissionTests.cs` (its own pumped
  harness; `RescanTests.Harness` is headless and cannot park a worker — WB-2's amendment).
  - `ABulkRenameApplyWaitsForAnOpenNotesAdmittedSaveAndRenamesOnTheSavedBytes` — park a save's
    write (`SaveWriteHookForTests`), apply, assert no core rename call started; release; one
    `NoteSaved`, one `RenameSummary`, the file holds the edit and the new key, no conflict.
  - `ASaveStartedUnderABulkRenameApplyWaitsForItsReconcile`.
  - `ARecoverStartedUnderAnAdmittedSaveWaitsForIt` and
    `ARecoverWhoseWorkerFaultsEndsAndFreesTheNotesChain` (hangs before the fix, as #1888's did).
  - `ARescanNeverReloadsATabUnderAnAdmittedRestore` — park the restore
    (`HistoryRestoreHookForTests`), run `ReSyncOpenDocumentsAsync` with a differing index hash,
    assert the tab's `ItemIdentity` is unchanged and it is marked stale, not reloaded.
  - `TeardownJoinsAnAdmittedBulkRenameAndRecover` (extend `TeardownJoinsAnAdmittedRestoreAndPropertyWrite`).
  - The census (cluster section) fails first on every unjoined route it finds.

- **Size:** L for the whole sweep as one chunk (three named routes, five siblings, a bound
  census, Codex rounds — #1885 took five). M if cut in two PRs as recommended below.
- **Lanes:** windows. rust and swift (compile only) only if the saved-query export sibling is
  fixed in core here (see the sweep, row W14).
- **Screen-reader work:** none, if every refusal reuses an existing sentence (WB-2, #1885 and
  #1888 all shipped with no speech change). NVDA spot if a new refusal sentence is added.
- **Changes text or speech a Mac user would hear:** no, for the Windows joins. Yes for row W14
  if fixed in core: an existing core sentence ("Base paths must end in .base") would be spoken
  for a Mac export it is not spoken for today.
- **Owner decision needed:**
  1. A save requested while a bulk rename (or a multi-note tag edit) is applying: wait for it,
     or refuse? Recommended: wait, as WD2 answered for the property write. Cost: Ctrl+S during a
     long apply says "Saved" only when the apply ends.
  2. Link rewrites made by a rename or move (sweep row W9) cannot join the chain without the
     set of notes they will rewrite, which core does not give in advance. Recommended: leave
     them guarded by core's per-file CAS and name them in the census as a dated exception.
  3. The exclusive-create family (sweep rows W15–W18): exempt as a class, or admit each?
     Recommended: exempt as a class with one class fact, and admit only `RecoverDeletedFile`,
     which the issue names.
- **Shares files with:** #1714 (`WorkspaceViewModel.Rescan.cs`, `WorkspaceViewModel.cs`).
  Large shared files: `WorkspaceViewModel.cs` (4455 lines), `WorkspaceViewModel.Bases.cs` (2229),
  `WorkspaceViewModel.Rescan.cs` (1451), `WorkspaceViewModel.Properties.cs` (1116),
  `FilesSidebarViewModel.cs`, `tests/…/SaveChainAdmissionTests.cs` (1530),
  `docs/plans/38_notification_dispatcher_contracts.md`.

---

## #1730 — a failed vault switch returns to the welcome view twice

- **Verdict:** stands.
- **Current state:** `VaultLifecycleViewModel.cs`, `OpenVaultAsync` `:572-825`.
  - `:613` `CloseSession()`; `:616` `bool returnedToWelcome = IsVaultOpen;` `:617-626` the
    switch's own return (`ReturnedToWelcome` raised `:625`).
  - `:655-667` the null-identity branch, already guarded by `if (!returnedToWelcome)` `:661`
    (WB-28 PR 2; `tests/…/VaultOpenIdentityTests.cs:104`
    `ASwitchRefusedForNoIdentityReturnsToWelcomeOnce`).
  - `catch (VaultException)` `:770-788` raises unconditionally at `:782`.
    `catch (Exception)` `:789-802` raises unconditionally at `:796`.
  - `MainWindow.xaml.cs:511-514` queues `OpenVaultButton.Focus()` per event.
  - Drift from the issue's text: the workspace is now published only after the scan and the
    first tree publication (`IsVaultOpen = true` at `:757`, #1684). So "a scan failure once the
    workspace shows" can no longer happen; the only failure after the vault shows is a throw
    from `PublishWorkspace` or `StartSyncMarkerWatch` (`:762-766`).
- **Cause:** verified by reading. The flag exists; the two catch arms don't read it.
- **Fix shape:** `VaultLifecycleViewModel.cs` only. Guard `:782` and `:796` with
  `if (!returnedToWelcome)`, and set `returnedToWelcome = false` where `IsVaultOpen` becomes
  true (`:757`), so a failure after the workspace showed still returns once.
- **Failing test first:** `tests/SlateWindows.Tests/VaultOpenIdentityTests.cs`, beside the
  existing fact: `ASwitchWhoseOpenFailsReturnsToWelcomeOnce` as a theory with two arms — a
  `VaultException` (the `SLATE_TEST_ROOT_FILE_ID_UNAVAILABLE` seam on the second vault's folder
  name, as the first fact in the file uses it) and a non-vault exception (the constructor's
  `openSession` seam, `VaultLifecycleViewModel.cs:226`, throwing). Each asserts
  `returnedToWelcome == 1`, `IsVaultOpen == false`, the status sentence. Both arms give 2 today.
- **Size:** S.
- **Lanes:** windows.
- **Screen-reader work:** NVDA spot (one focus move, and so one "Open vault, button" readout,
  is removed; the pass confirms the reader's place on the welcome view is kept).
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** none.
- **Shares files with:** nothing else in this cluster's recommended fixes.
  `VaultLifecycleViewModel.cs` (2870 lines) is a large shared file for other clusters.

---

## #1683 — another session's rename then an own rename announces the old path missing

- **Verdict:** stands, and partly misdescribed: the emitter the issue could not identify is
  found, and it is core's, not the host's. The own rename is incidental; any task query on the
  old path triggers it.
- **Current state (all in `crates/slate-core/src/session.rs` unless said):**
  - Every structural move plants held markers on both paths and ages them in its index commit:
    `structural_move_file` `:17452`, plant `:17505` (`[from, to]`), age `:17605`
    (`age_move_markers_in_tx` `:8178`, "so the first task query after the move re-reads every
    touched path"). The same commit moves the index row (`UPDATE files SET path …` `:17522`).
    The index and `text_write_intents` are shared by every session on the vault.
  - A task query sweeps aged markers first: `tasks_for_file` `:7742-7746`, `note_tasks`
    `:7753-7755` (scope: that path), `tasks_in_vault` `:9936-9944` (scope: all), through
    `repair_abandoned_intents_locked` `:8348` → `reindex_path_locked` `:8488` → `:8681`.
  - `reindex_path_locked`, on `NotFound`, sets `converged_to_deletion`, runs
    `DELETE FROM files WHERE path = ?1` (`:8769`) and then announces
    `FileChangeKind::Deleted` (`:8795-8802`) — whether or not the DELETE removed a row.
  - So: session B renames `first.md` → `second.md` and leaves an aged marker on `first.md`.
    Session A's next task query for `first.md` sweeps it, reads `NotFound`, deletes zero rows
    (B's commit already moved the row), and tells A's listeners `Deleted first.md`.
  - Host: `VaultLifecycleViewModel.FileChanges.cs:44-46` → `WorkspaceViewModel.InvalidatePath`
    `WorkspaceViewModel.cs:3757` → the tab is marked missing and
    "`first.md is missing from disk. Open editor content was preserved.`" is spoken High
    (`:3801-3807`). It also prunes the closed-tab history and the Connections stack for the
    path (`:3786-3792`).
  - Why the own rename mattered in the reproduction: its `Renamed` event runs
    `InvalidateAllInteractionStates` (`FileChanges.cs:62`), and a tab still at `first.md`
    re-queries its tasks (`EditorInteractions.cs:1322` → `:2532` `TasksForFile(path)`; also
    `WorkspaceViewModel.cs:2016` and `Panels/RightPanePanelsViewModel.cs:908`). **Unverified:**
    which of those three queries fires in the headless harness.
  - The existing fact pins the defect as "as on main" and clears the events:
    `tests/…/RescanTests.ChangeFeed.cs:806-831`
    `AForeignRenameThenAnOwnRenameOfItsDestinationMovesTheTabThere` (comment `:817-819`).
  - WB-10 already knows the shape: `VaultLifecycleViewModel.ChangeFeed.cs:775-777`, "a scan
    reports another session's rename of that path as [a deletion] too, and the file's record
    decides it."
- **Cause:** verified by reading, not by a run: a repair that converges a path to deletion
  announces `Deleted` even when it removed nothing from the index.
- **Fix shape (recommended, core):** `reindex_path_locked` announces `Deleted` only when its
  DELETE removed a row (`tx.execute` returns the count at `:8769`); a convergence that changed
  nothing announces nothing. Round 24's case (a conflicted save against a note deleted outside
  Slate: the row exists and the repair removes it) still announces. No FFI change. Then the
  Windows fact asserts the quiet aftermath. The tab stays at `first.md`, not missing, until the
  next rescan's change-feed window moves it (already the fact's ending).
  Alternative (host only): `FileChanges.cs`'s `Deleted` arm defers the missing mark and its
  sentence for a deletion this window did not ask for, and lets a change-feed rescan decide.
  Larger (M), needs a registry of the sidebar's own deletes, and leaves the Mac with the defect.
- **Failing test first:**
  - Core: `crates/slate-core/src/session/tests/two_sessions.rs` (or `events.rs`),
    `a_repair_that_removes_no_index_row_announces_no_deletion` — session A with a listener and
    `first.md` indexed; session B renames it; A calls `tasks_for_file("first.md")`; A's listener
    heard no `Deleted`. Plus a guard that the round-24 convergence (row present, file gone)
    still says `Deleted`. Plan 21's rule applies: bite-verify by switching the fix off.
  - Windows: in the existing fact (`RescanTests.ChangeFeed.cs:806`), replace the
    `h.Events.Clear()` at `:822` with `Assert.Empty(h.Spoken)` and
    `Assert.False(tab.IsMissingFromDisk)` before the rescan.
- **Size:** S (core fix), M (host alternative).
- **Lanes:** rust, windows (the fact), swift (compile only; no FFI change, no Swift edit expected).
- **Screen-reader work:** NVDA spot — a spoken High sentence is removed. The pass needs two
  Slate sessions on one vault.
- **Changes text or speech a Mac user would hear:** yes, by the core fix. No string or FFI
  change; one event stops arriving. A Mac window with the note open would no longer be told the
  old path was deleted after another session's rename. **Unverified:** what the Mac host does
  with that event today. One line on the "Mac work owed" ledger.
- **Owner decision needed:** fix in core (a repair announces only what it changed) or in the
  Windows host (defer the sentence to the change feed)? Recommended: core. It is the smaller
  change, it is the true cause, and it fixes both hosts.
- **Shares files with:** #1900 (`crates/slate-core/src/session.rs`, the same repair functions).
  `session.rs` is a very large shared file for the core SQLite chunks (#1917, #1718).
  `tests/…/RescanTests.ChangeFeed.cs` is shared with nothing else in this cluster.

---

## #1714 — a citation summary press parked while its note loads is dropped when the note is renamed

- **Verdict:** stands. WB-23 landed as #1715, so the "with WB-23" behaviour is the one on `main`.
- **Current state:**
  - `WorkspaceViewModel.Citations.cs`: `OpenCitationSummary` `:255`; the park `:281-286`;
    `_parkedSummaryPath` `:386`; `DropParkedSummaryIfNoteChanged` `:402-414` (drops when
    `Citations.Path != _parkedSummaryPath`); `OpenSummaryWhenPublished` `:420-431`.
  - `SyncPanels` `WorkspaceViewModel.cs:2975`, which calls the drop at `:2988`.
  - Rename hook 1 (Slate's own rename): `WorkspaceViewModel.cs:3565` `RetargetPath(old, new)`,
    `Persist` `:3611`, `SyncPanels` `:3616`.
  - Rename hook 2 (another session's rename, from the change feed):
    `WorkspaceViewModel.Rescan.cs:210` `RetargetPathsForFeedRenames`; tabs move `:261-265`;
    `Persist` `:303`; `SyncPanels` `:304` (the issue's `:304` is exact).
  - Neither hook touches `_parkedSummaryPath`; `TryRetargetPath` is at
    `WorkspaceViewModel.cs:4432`.
  - The facts: `tests/SlateWindows.Tests/CitationAsyncInterleavingTests.cs`
    (`ADeferredSummaryIsDroppedWhenItsNoteCloses` `:522`,
    `ADeferredSummaryStillAnswersForTheNoteItWasAskedAbout` `:710`).
- **Cause:** verified by reading. The rename moves the leaf's path; the parked path stays, so
  the boundary's drop sees a different note.
- **Fix shape:** `WorkspaceViewModel.cs` and `WorkspaceViewModel.Rescan.cs` (and a small helper
  in `WorkspaceViewModel.Citations.cs`). Before each hook's `SyncPanels`, move
  `_parkedSummaryPath` with the rename. Hook 1: `TryRetargetPath(parked, source, destination)`.
  Hook 2: the feed plans moves against the tabs as they were (a swap or a chain moves each tab
  once), so retarget from the planned `moves` list for the tab that held the parked path, not
  by replaying renames in order.
- **Failing test first:** `CitationAsyncInterleavingTests`:
  `ADeferredSummaryFollowsItsNoteThroughARename` (park under a held load, own rename, release:
  the sheet opens on the renamed note's counts, `OpenSummaryWhenPublishedInvokedForTests == 1`)
  and `ADeferredSummaryFollowsItsNoteThroughAnotherSessionsRename` (the feed hook).
- **Size:** S.
- **Lanes:** windows.
- **Screen-reader work:** NVDA spot (the sheet opens and says its counts where nothing was
  said before; the sentence itself is existing).
- **Changes text or speech a Mac user would hear:** no.
- **Owner decision needed:** none.
- **The issue's related probe** (a publish landing inside the dirty-navigation modal's pumped
  frame, `WorkspaceViewModel.Layout.cs:748` `AdmitDirtyTab`): not reproduced here. By reading,
  `OpenSummaryWhenPublished` calls `OpenCitationSummary`, which consults
  `CitationSummaryOpenAdmission` again at `:257`. **Unverified:** what that admission answers
  while the dirty-navigation prompt is up. Under the new filing rule it is a small find to
  probe in this PR.
- **Shares files with:** #1889 (`WorkspaceViewModel.Rescan.cs`; `WorkspaceViewModel.cs` if
  #1889's joins touch it).

---

## #1900 — a rescan with an unreadable note once failed the files tree load with a sqlite error

- **Verdict:** stands, and the cause can be explained from the code. It is a core defect, and
  it is reachable in ordinary use, not only on CI. The chunk does not need to begin with an
  open-ended diagnosis; it begins with a deterministic core fact.
- **Current state:**
  - The truncated text is rusqlite's range error. The locked rusqlite is 0.41.1; its only
    message beginning "Integ" is `IntegralValueOutOfRange`: "Integer {val} out of range at
    index {col}" (`rusqlite-0.41.1/src/error.rs:288-292`). Core prefixes it:
    `crates/slate-core/src/db.rs:46` `#[error("sqlite error: {0}")]`. Bundled SQLite has no
    capitalised "Integ…" message, and no core string starts that way (grep).
  - Core writes a sentinel into the stat columns of a note it cannot read:
    `UPDATE files SET mtime_ms = -1, size_bytes = -1, ctime_ms = -1` at
    `crates/slate-core/src/session.rs:7912` (the host route's containment,
    `repair_or_contain_path` `:7806`), `:8666` (the sweep's quarantine, committed, after a
    repair fails on a file condition — `Io`, non-UTF-8, too large, invalid path; `:8488-8500`)
    and `:8726` (the repair's own stat poison, rolled back when the read fails).
  - The file listing refuses that sentinel:
    `decode_file_summary_query_row` `:12207`, `size_bytes` at `:12242-12245`
    (`u64::try_from(raw).map_err(|_| IntegralValueOutOfRange(4, raw))`). The Files tree's file
    page uses it (`crates/slate-core/src/session/directory_page.rs:747-770`, decode at `:761`),
    as do `:12391`, `:12527`, `:12567`, `:12810`, `properties_db.rs:303`/`:448` and
    `change_feed.rs:1024`.
  - So one quarantined row fails every listing page that holds it with
    "sqlite error: Integer -1 out of range at index 4", for as long as the file stays
    unreadable (a scan cannot heal a row whose file it cannot read).
  - The CI test matches: another session renamed `a.md` → `b.md` (aged markers on both, as in
    #1683), then `b.md`'s read was denied for a whole rescan. A task query on `b.md` in that
    window sweeps the marker, the re-read fails with `Io`, the row is quarantined, and the
    tree refresh then decodes it. The query's timing against the tree refresh explains "seen
    once, not in 20 local runs". **Unverified:** which query swept it on CI, and that the ACL
    denial surfaces as `VaultError::Io`.
  - Other readers of the sentinel, same family: `:12042` `size_bytes as u64` in
    `get_file_metadata_impl` (−1 becomes 2^64−1, silently), `:21787` the canvas pass
    (`size_bytes as u64 > large_file_refuse_bytes`, so a quarantined board is treated as too
    large), `change_feed.rs:461-463` `to_u64` (clamps to 0, correct). `mtime_ms = -1` passes
    through as an `i64` to both hosts. **Unverified:** how each host shows it.
  - Host sentence: `FilesSidebarViewModel.TreeOperations.cs:309` and `:456` build
    "Could not load files: {exception.Message}", the binding's `@message=` dump — #1890's
    class, not this issue's fix.
  - Ordinary-use route, by reading: a save or toggle that fails on a locked file registers a
    pending repair; `Panels/TaskIndexRepairCoordinator.cs:176`/`:232` call
    `RepairOrContainPath`; a still-unreadable file is `Contained` (`:7912`); the folder then
    fails to list.
- **Cause:** verified by reading, not reproduced. The quarantine reuses `size_bytes` as a
  sentinel and the listing decoder rejects a negative size.
- **Evidence the CI run left:** run 43050889133 (workflow `windows`, branch
  `claude/1894-rescan-unloaded-read`, head 859e1b9c, 2026-11-11), job 130627442884. The failed
  log holds one useful line, the xUnit collection dump:
  `["Files refreshed with errors. 2 errors; results may"···, "Could not load files: @message=sqlite error: Integ"···]`
  at `RescanTests.ChangeFeed.cs:line 527`. Three artifacts are kept and unexpired:
  `slate-windows-app-results-43050889133-1` (1.8 MB) and two `windows-model-…` (42 KB each).
  Not downloaded. The test results would carry the same truncated assertion text, so they are
  unlikely to add the number; the code already gives it.
- **Fix shape:** core, `crates/slate-core/src/session.rs`. Make every reader of the stat
  columns tolerate the sentinel: the listing decoder reports a quarantined row's size as 0
  instead of failing (`:12242`), and `:12042` and `:21787` stop wrapping it. Existing indexes
  may already hold −1 rows, so the readers must change even if the three writers stop using
  `size_bytes`. Decide in the chunk what `mtime_ms` a quarantined note reports.
  The `@message=` sentence goes to #1890's sweep (two more sites for its census:
  `FilesSidebarViewModel.TreeOperations.cs:309`, `:456`).
- **Failing test first:** core, beside `repair_or_contain_releases_a_permanently_unreadable_path`
  (`crates/slate-core/src/session/tests/tasks.rs:1043`), which already drives containment with
  the `SLATE_TEST_FAULT_REINDEX` seam: `a_contained_note_still_lists_in_its_folder` — after
  `Contained`, `list_dir_files_page` (and `get_file_summary`, `filter_files`,
  `get_file_metadata`) return the note, size 0. By reading, the first of these returns the
  range error today. The issue's own "done when" fact (deny a note's read across a rescan and
  hold the tree loading) is then the Windows twin in `RescanTests.ChangeFeed.cs`, made
  deterministic by issuing the task query inside the denial.
- **Size:** S.
- **Lanes:** rust, windows (the twin fact), swift (compile only).
- **Screen-reader work:** none (no sentence changes; a sentence stops being needed).
- **Changes text or speech a Mac user would hear:** no string or FFI change. Behaviour both
  hosts consume: a folder holding a quarantined note lists instead of failing. One ledger line
  is prudent.
- **Owner decision needed:** none. (If the chunk's reading shows a host renders `mtime_ms = -1`
  as a 1969 date, that is a small find for the same PR.)
- **Shares files with:** #1683 (`crates/slate-core/src/session.rs`, the same repair and
  quarantine functions); #1890's sweep (the two host sentences).

---

# Cluster section

## The sweep: every route that writes a note's bytes, or reloads an open tab's buffer

Found by binding-free search of every `VaultSession` call in `apps/slate-windows/src` (all
session variables are named `*session*`; checked), every direct file write, and every caller of
the tab methods that replace a buffer. Classified against the `VaultSession` methods the FFI
exports (`crates/slate-uniffi/src/lib.rs:844`, `:12920` and the canvas blocks from `:14382`).

"Joins" means: runs inside a ticket of the note's path key, its worker is handed to
`TrackWorker`, and a save requested after it waits.

### A. Writers of an existing note's bytes

| # | Route | file:line | Writes or reloads | Joins? | How / why not |
|---|---|---|---|---|---|
| W1 | Tab save (Save, Save All, close and navigation gates) | `WorkspaceViewModel.cs:1011` (`Enqueue`), write `:1279` / `:1295-1296`, worker `:1221` | writes | yes | `Saves.Enqueue(KeysFor(tab, path))`; the chain's own member |
| W2 | History restore | `WorkspaceViewModel.History.cs:190` (`Admit`), write `:232`, worker `:328` | writes, then reloads `:307` | yes | `Admit` → `RestoreAdmitted`; reload through `ReloadOpenTabFromDiskAdmitted` `:391` |
| W3 | Task toggle through a tab (editor, reading view, tasks panel, Tasks Review with a tab open) | `WorkspaceViewModel.cs:1542` (`Admit`), write `:1781`, worker `:1638`; callers `EditorInteractions.cs:2055`, `Reading/ReadingContentViewModel.cs:449`, `WorkspaceViewModel.cs:4065`, `:4221` | writes, splices the buffer | yes | `Admit` → `StartAdmittedTaskToggle` (#1888) |
| W4 | Properties header: row set, row delete, add, both Keep Mine retries | `WorkspaceViewModel.Properties.cs:211`, `:253`, `:437`, `:545` (`AdmitPropertyWrite` `:769`); writes `:430`, `:435`, `:533`, `:542`, `:1045`, `:1097`; workers `:1003`, `:1040`, `:1095` | writes, rebaselines tabs `:594`/`:273` | yes | `AdmitPropertyWrite` → `_saves.Admit` (WB-2) |
| W5 | Bases cell edit, note open in a tab | `WorkspaceViewModel.Bases.cs:1879`, write `:1970-1974` | writes, rebaselines `:1985` | yes | `AdmitPropertyWrite` → `BasesCellWriteAdmitted` (#1885) |
| W6 | **Bases cell edit, no tab on the note** | `WorkspaceViewModel.Bases.cs:1884-1903`, write `:2010` / `:2014` | writes | **no** | Decided tabless at request; unconditional write (null expected hash) under the note's property lease only; tracked by the bounded Bases drain, not the coordinator. A tab opened and saved during the worker is not joined. |
| W7 | **Tasks Review toggle, no tab on the note** | `Panels/TasksReviewViewModel.cs:680`, write `:718` | writes | **no** | CAS on the row's snapshot hash; no admission; not tracked by the coordinator. A tab that opens mid-flight is only marked stale (`WorkspaceViewModel.cs:4183` → `:2034`). |
| W8 | **Bulk property rename, apply** (named in #1889) | `Panels/BulkRenameViewModel.cs:236-245`; reconcile `WorkspaceViewModel.Properties.cs:668` | writes many notes; reloads clean tabs | **no** | Worker outside the chain; core CAS per file; gate checks header drafts only (`:174`) |
| W9 | **Link rewrites by a rename or move** (file rename, folder-with-note rename, move, batch move, and their undo and redo) | `FilesSidebarViewModel.FileManagement.cs:239-240`, `:382-383`, `:404-405`, `:422`, `:1125-1126`; `FilesSidebarViewModel.cs:2308` | writes every note that links to the moved one | **no** | Core rewrites and reports them (`report.Rewritten`, per-file CAS, `session.rs:17930`); the host only counts them (`FileManagement.cs:75`). An open tab on a rewritten note is marked stale by the `Modified` event, not reloaded. The set is not known before the call. |
| W10 | **Sidebar tag add / remove on checked files** | `FilesSidebarViewModel.cs:1693`, write `:1708-1709` | writes each checked note's frontmatter | **no** | Synchronous on the dispatcher; core CAS against the indexed hash (`session.rs:9060-9160`); no dirty-tab gate, no admission, no tab rebaseline (open clean tabs are left stale until a rescan) |
| W11 | **Bases view export onto an existing vault file** | `WorkspaceViewModel.Bases.cs:950` (`SaveText` on the observed hash); create arm `:945` | writes | **no** | WB-12's observe-then-publish: CAS on the hash seen before the reader confirmed. Both losers are refused honestly. Can target a note when the reader exports Markdown over one. |
| W12 | Bases view export outside the vault or onto a scanner-hidden path | `WorkspaceViewModel.Bases.cs:860` (`File.WriteAllText`) | writes | n/a | Not a vault note |
| W13 | Task-index repair | `Panels/TaskIndexRepairCoordinator.cs:176`, `:232` (`RepairOrContainPath`) | index only | n/a | Writes no note bytes (but see #1900: it writes the −1 sentinel) |
| W14 | **Saved-query "export as base"** | `WorkspaceViewModel.Bases.cs:1738`, write `:1760`; picker `MainWindow.Bases.cs:336-364` | writes | **no — and no CAS** | Core `export_saved_query_as_base` (`session.rs:19620-19629`) calls `save_text(path, text, None)`: an unconditional overwrite at a reader-chosen path, with no `.base` check (its twin `save_query_as_base` `:19770` requires `.base` and is create-only). The host checks only for a rooted path or `..`. Picking an existing note in the save dialog replaces it, under any admitted save. The only guard is the OS dialog's own replace prompt. Found by this sweep. |

### B. Creators (a path that should be free)

| # | Route | file:line | Joins? | How / why not |
|---|---|---|---|---|
| W15 | Template create | `WorkspaceViewModel.Templates.cs:192` (create, on the dispatcher), reload sweep `:276-288` | create: no; sweep: yes | WB-2 admitted the same-path reload only. The create is exclusive and takes the path first; a parked ghost save is then refused (`ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave`). |
| W16 | **`RecoverDeletedFile`** (named in #1889), Recover As, Restore As (a live version to a new path) | `WorkspaceViewModel.History.cs:529`, `:593`, `:462` | no | Exclusive creates (`session.rs:6335`); `RunHistoryWork` `:636`; each worker catches `VaultException` only (`:531-538`, `:595-603`, `:465-473`) |
| W17 | Sidebar creates: untitled note, folder note, duplicate, canvas and surface note creators (graph, Connections), untitled canvas | `FilesSidebarViewModel.cs:1812`, `:2052`, `:1901`; `FilesSidebarViewModel.FileManagement.cs:1254`; `FilesSidebarViewModel.CanvasNotes.cs:26`; `FilesSidebarViewModel.SurfaceNotes.cs:32` | no | All through `CreateOutcomes.CreateReporting` (`CreateOutcomes.cs:39-42`), exclusive |
| W18 | Import | `FilesSidebarViewModel.Import.cs:352` (`CreateExclusiveBytes`) | no | Exclusive; advances the name on `DestinationExists` |

### C. Not note content (listed so the census classifies them)

| Route | file:line | Why outside the class |
|---|---|---|
| `.base` definition edits, Save As base | `Bases/BaseDocumentViewModel.cs:599`, `:759`; `Bases/BaseQueryBuilderViewModel.cs:721` | Core writes the `.base` through its handle with CAS (`session.rs:19956-20005`); `.base` only. No editor buffer save exists for a base (`KeysFor` gives a path key to Markdown only, `WorkspaceViewModel.cs:1012`). |
| Canvas edits | `Canvas/CanvasDocumentViewModel.cs:5155` (`CanvasApply`) | `.canvas` through its handle |
| Renames, moves, trash, delete of the note itself | `FilesSidebarViewModel.cs:2001-2002`, `:2095`, `:2382`; the W9 sites | Structural. A rename carries the chain (`ContinueChain` at `WorkspaceViewModel.cs:3582` and `Rescan.cs:263`). |
| Hot exit | `WorkspaceViewModel.HotExit.cs` | Writes the app's store, not the vault |
| Saved queries, dashboards, preferences, recents, journals | `WorkspaceViewModel.Bases.cs:1194`, `:1201`; `AppPreferencesStore.cs`; `SidebarWriteJournal.cs`; others | The cache or app data |

`SaveComposed` and `SetFrontmatterSource` have no Windows call site.

### D. Reloads of an open tab's buffer from disk

| # | Route | file:line | Joins? | How / why not |
|---|---|---|---|---|
| R1 | Restore's reload | `WorkspaceViewModel.History.cs:391-401` | yes | Takes the restore's `SaveTicket`; `ReloadKeepingQueuedSave` (`WorkspaceViewModel.cs:819`) |
| R2 | Unadmitted history reload | `WorkspaceViewModel.History.cs:365-380` | guarded | `!tab.HasPendingSaves && !_saves.HasAdmittedSaveFor(path)`; only caller is the test seam `:351` |
| R3 | Template same-path sweep | `WorkspaceViewModel.Templates.cs:276-288` | yes | Inside `Admit` |
| R4 | Re-seat on a Created or Renamed event | `WorkspaceViewModel.cs:3664`, guard `:3687`, replace `:3695` | guarded | `KeepsBufferOnReseat` `:3724-3730` asks `HasAdmittedSaveFor` |
| R5 | Rescan's re-seat of missing tabs | `WorkspaceViewModel.Rescan.cs:541`, guard `:625`, replace `:659` / `:711` | guarded | Same rule |
| R6 | **Rescan's reload of a changed note** (named in #1889) | `WorkspaceViewModel.Rescan.cs:767`, guard `:788-789`, reload `:832` | **partly** | Asks the property lease, the toggle flag and tab saves; not `HasAdmittedSaveFor`. Misses an admitted restore today, and any future admitted operation. |
| R7 | Rebaseline after a property write | `WorkspaceTabViewModel.Properties.cs:101-144`; callers `WorkspaceViewModel.Properties.cs:273`, `:594`, `:632`, `WorkspaceViewModel.Bases.cs:1985` | yes | Each runs in the admitted write's completion |
| R8 | **Rebaseline after a bulk rename** | same method; caller `WorkspaceViewModel.Properties.cs:677` | **no** | Part of W8 |
| R9 | Task toggle's publication | `WorkspaceViewModel.cs:1855` | yes | Inside W3's ticket |
| R10 | Navigation re-points a tab at another item | `WorkspaceViewModel.Layout.cs:354-363` (from `:219`, `:321`) | n/a | A different item, behind `AdmitDirtyTab`'s settle (`:748-784`). Retiring the old item's queued save is the design (`WorkspaceViewModel.cs:995-998`). |
| R11 | Board and base reloads | `WorkspaceViewModel.Rescan.cs:479`, `:502`, `:669`, `:678`; `Canvas/WorkspaceViewModel.Canvas.cs:294` | n/a | Not Markdown; no path chain exists |
| R12 | Peer mirror, hot-exit restore | `WorkspaceViewModel.cs:4339-4356`, `WorkspaceViewModel.HotExit.cs:1466` | n/a | Not from disk |
| R13 | Event `Modified` on an open note | `WorkspaceViewModel.cs:4378-4392` | n/a | Marks stale only; never reloads |

### Count

Unjoined today: **the three #1889 names (W8 with R8, W16's `RecoverDeletedFile`, R6) and five
it does not name** — W6 (tabless Bases cell write), W7 (tabless Tasks Review toggle), W9 (link
rewrites), W10 (sidebar tag edit), W11 (Bases export over a vault file) — plus **W14, which is
worse than unjoined** (no CAS), plus the exclusive-create family (W15's create, the other two
W16 routes, W17, W18: eleven call sites).

In none of the CAS-guarded routes did I find a path to silent loss of a note's bytes: core's
per-file CAS or exclusive create refuses one side. What they produce is a conflict prompt or a
skipped file for work the reader did in one window, a tab left stale after an in-app action
(W9, W10), or a "Saved" the reader waits on. W14 is the exception.

### Recommended treatment per unjoined route

| Route | Recommendation |
|---|---|
| R6 | Join the rule: one line |
| W16 `RecoverDeletedFile` | Admit (named; cheap; one path). Widen the catch. |
| W16 Recover As, Restore As; W15 create; W17; W18 | Exempt as the class "exclusive create at a path the reader or the namer chose", with one class fact: a parked ghost tab's save and a create at its path — exactly one lands, the other is refused, nothing is overwritten. Widen the two narrow catches in `History.cs` anyway. |
| W6, W7 | Admit by path through `AdmitPropertyWrite` / a workspace seam; at the turn, re-check that no tab is on the note (a tab that appeared: refuse as the open-tab route does). Give W6 an expected hash, or record why it has none. |
| W8 | Admit over many paths (new primitive); cancel before teardown's settle |
| W10 | Same primitive as W8, plus the tab reconcile W8 already has (`ReconcileTabsAfterPropertyWrite`). Needs a sidebar → workspace seam. |
| W9 | Dated census exception and an owner decision row. A cheap improvement that needs no admission: rebaseline clean open tabs from `report.Rewritten` (it carries `hash_after`), as bulk rename does. |
| W11 | Exempt with the reason recorded: both sides CAS, both refusals are true. |
| W14 | Fix, not exempt. Smallest: core refuses a path that does not end in `.base`, as `save_query_as_base` does, so the route can never name a note. Core change, no FFI change; a Mac user could hear an existing sentence in a new place → ledger line. The unconditional overwrite of an existing `.base` remains and is outside this class. |

## The census

**Model:** `tests/SlateWindows.Tests/Censuses/BasesErrorDetailCensus.cs` (#1697) for the
method — it binds call sites through `ShellCompilation`
(`tests/SlateWindows.Tests/Support/ShellCompilation.cs`) instead of scraping spellings, and
fails closed when a source does not bind — and
`tests/SlateWindows.Tests/Censuses/ShellSealAdmissionCensus.cs` for the shape: reviewed
allow-tables keyed `(File, Method)` with the reason as the value. The simplest relative is
`CitationWriteSurfaceCensus.cs` ("the mutator has exactly one call site"), and
`BindingSurfaceCensus.cs` shows reflection over the generated binding.

**Name:** `SaveChainAdmissionCensus`, `[Trait("census", "save-chain-admission")]`.

**What it scans for:**

1. **The writer list is closed.** Reflect over `VaultSession`'s public methods in the binding
   assembly. Each is in exactly one of three tables: note-byte writer, creator, or neither
   (with a word of why). A new FFI method fails the census until someone classifies it. This
   is what would have caught `ExportSavedQueryAsBase`.
2. **Every writer call site is known.** Bind every invocation in the shell's sources whose
   target is a `VaultSession` method from the writer or creator table. Each site must have a
   row `(File, Method) → Joined | Exempt(reason, date)`. An unlisted site fails; a row with no
   site fails (stale).
3. **A `Joined` row is really joined.** The call's enclosing method must be reachable only
   through an operation passed to `WorkspaceSaveCoordinator.Admit` or `Enqueue` (directly, or
   through `AdmitPropertyWrite`). `ShellSealAdmissionCensus` already walks a call graph
   (`TheFocusObserversTransitionCallGraphHasNoSideEffects`); reuse that. And the worker the
   route starts is an argument to `TrackWorker`.
4. **Every reload site is guarded.** Bind every call to the tab methods that replace a buffer
   from disk: `ReplaceItem`, `ReplaceItemWithReadText`, `ReloadKeepingCaretLine`,
   `ReloadKeepingQueuedSave`, `RebaselineAfterPropertyWrite`. The enclosing method must take a
   `SaveTicket`, sit inside an `Admit` operation, or read `HasAdmittedSaveFor` (directly or
   through `KeepsBufferOnReseat`) before the call; else it needs an exempt row (navigation's
   `ReplaceTabItem`).
5. **A planted call is named**, with file and line, so the census cannot pass by matching
   nothing (`BoundedFfiListCensus.TheScrapeNamesAPlantedCallsFileAndLine` is the precedent).

The behavioural half stays in `SaveChainAdmissionTests`: one parked-save fact per joined
route, and one fact per exempt class.

Land the census first, with today's unjoined routes as dated exceptions; each later PR deletes
the exceptions it joins. That makes each join visible in a diff and stops a new sibling while
the sweep is in flight.

## Recommended cut into PRs

| PR | Issues | What | Lanes | Why this cut |
|---|---|---|---|---|
| 1 | #1900 | Core: a quarantined note still lists; the sentinel's other readers | rust, windows, swift compile | Independent, small, protects the Files tree. No decision. First user of `session.rs` in this cluster. |
| 2 | #1730 | The two catch arms | windows | Independent of everything here; one file. Can run beside PR 1 and PR 3. |
| 3 | refs #1889 | The census with dated exceptions; the single-path joins (R6, `RecoverDeletedFile`, W6, W7); the create-class fact and the two catch widenings | windows | No new primitive. Codex review. Does not close #1889. |
| 4 | #1889 | The many-path admission; bulk rename (W8, R8); tag edit (W10); W9's exception or its rebaseline; W14 if taken here; the last exceptions removed | windows (+ rust and swift compile if W14 is fixed in core) | The new primitive and its two users ride together. Codex review. Closes #1889. |
| 5 | #1714 | The parked press follows a rename | windows | After PR 3: both touch `WorkspaceViewModel.Rescan.cs`. |
| 6 | #1683 | Core: a repair announces only what it changed; the Windows fact's quiet aftermath | rust, windows, swift compile | After PR 1 (same functions in `session.rs`) and after the owner's answer. Codex review suggested: plan 21's protocol took 41 adversarial rounds. |

#1683 and #1900 could ride in one PR (same file, same protocol, both S). I keep them apart
because #1683 waits on a decision and changes an event the Mac consumes, and #1900 does not.

Wave view: PRs 1, 2 and 3 share no files and can run together. PR 4 follows 3. PR 5 follows 3.
PR 6 follows 1.

## Risks

- **A held chain hangs Close Vault.** `SettlePumping` waits for every ticket. An admitted
  bulk rename or tag edit must be cancelled before teardown's settle and must settle its ticket
  on cancel, on a worker fault and on a throwing announcer (#1888's rounds 1 and 2 were exactly
  these). `RecoverDeletedFile`'s narrow catch would hold the note's chain for good if admitted
  as it stands.
- **The many-path key set.** Keys taken from open tabs at apply time miss a note opened during
  the apply; keys taken from the armed preview miss notes that gained the key since. Either
  leaves a small CAS-guarded window. A coordinator-wide hold (every later save waits) closes it
  and is simpler to census, at the cost of holding unrelated saves for the apply's length.
  Decide in the spec.
- **A waiting operation's captured target goes stale** (#1885's five rounds): a tab or an
  ordinal captured at request must be re-checked at the turn or refused. W6 and W7 are the
  routes most exposed: "no tab" at request can be false at the turn.
- **R6's wider guard marks more tabs stale** during admitted operations. Check that an
  admitted property write that finds its tab stale at its turn does not now refuse where it
  wrote before (`BasesCellWriteAdmitted` refuses on `IsExternallyStale`,
  `WorkspaceViewModel.Bases.cs:1965`).
- **Core changes in plan 21's protocol** (#1683, #1900): `docs/plans/21_write_intent_protocol_invariants.md`
  requires bite-verified regressions. #1683's fix changes an event both hosts receive.
- **The census's reachability rule** is the hard part. If the call-graph check proves brittle,
  ship rules 1, 2, 4 and 5 with the reviewed table and leave rule 3 to the behavioural facts,
  and say so in the PR.
- **Contract 38's trigger rows** move whenever an announcement's construction site moves.
- **#1683's field pass** needs two sessions on one vault through
  `scripts/windows/launch-for-field-pass.ps1`. **Unverified:** that the script can start two.
- **Shared large files** across clusters: `crates/slate-core/src/session.rs`,
  `WorkspaceViewModel.cs`, `VaultLifecycleViewModel.cs`, `FilesSidebarViewModel.cs`.

## Statements not verified in the code

- That an ACL `ReadData` denial reaches core as `VaultError::Io`, and which task query swept
  the marker in the CI run (#1900).
- Which host task query fires in `RescanTests.Harness` after the own rename (#1683).
- What the Mac host does with a repair's `Deleted` event (#1683) or shows for `mtime_ms = -1`
  (#1900).
- What `CitationSummaryOpenAdmission` answers under the dirty-navigation prompt (#1714's probe).
- That core's property and tag writes with a null expected hash re-read under the writer lock
  (W6's "applies on top of the saved bytes").
- Nothing here was run. Every "today this returns / speaks X" is from reading.
