# WB-2 — A History restore, a template reload and a property write join an admitted save of the same note instead of racing it

Issues: Closes #1322, Closes #1325. Size S–M. Lanes: windows. AT: none (no speech changes). Wave: W1. Depends on: nothing. Parallel with: WB-1, WB-3. Decision: WD2 (answered 2026-10-21: wait; the property write waits for the header refresh).

## Amended at implementation (2026-10-26)

This section outranks the text below it.

- **The facts run in the app's mode, not on the rescan harness.** `RescanTests.Harness` builds its workspace headless. Its serial context is not a dispatcher context, so `startInteractionBackgroundWork` resolves false. The restore's worker then runs inline, and the properties header that activation attaches is synchronous. A fact that parks either one parks the thread that has to pump. `SaveChainAdmissionTests` instead builds its own vault and a `WorkspaceViewModel(…, startInteractionBackgroundWork: true)` under `StaThread.RunPumped`, and it waits for every park by pumping. The property facts' peer save is `B.SaveAsync` with the Save command's `onSaved` and `announce: true`, because `SaveActiveCommand` returns no task to wait on.
- **The template fact's parked save conflicts.** A template create refuses an existing note, so the only same-path tab it can sweep is a ghost. The save parked before its write is therefore a ghost's create, and the template's create takes the path first. After release that save finds the note and is refused with `NoteSaveBlocked` ("Something named Ghost.md already exists there."); it never overwrites the note. Then the sweep reloads the tab once, and the save queued behind it publishes `NoteSaved` once.
- **The template's caret request moved into the sweep's operation**, after the reloads, so a deferred sweep's caret lands on the reloaded text. It is guarded on the landed tab: it must still be open on the path, clean, and not externally stale.
- **The restore's continuation runs on the save chain's dispatcher** (`RunOnSaveDispatcher`, inline when already there). Main used `Application.Current`'s dispatcher, which a headless host does not have. The restore's worker is tracked by `TrackWorker` and by the bounded history drain.
- **The D-14 line is built in one place.** `AnnounceRestoreNeedsSave` builds it, for staging and for the admitted start. Its designation is re-keyed to `WorkspaceViewModel.History.cs#WorkspaceViewModel.AnnounceRestoreNeedsSave@1` and the ledger regenerated.
- **Found on the way, not delivered here:** a Bases grid cell edit on an open note writes its property outside the save chain, so it races an admitted save the same way. Filed as #1657.

## Goal

Since #1280 a save's write runs off the dispatcher, and #1309 made the two
re-seats save-aware. Three paths still act on a note while its save is
admitted: the History restore's reload, a template's same-path reload, and
a property write from a peer tab. After this chunk each of them waits for
the admitted save to finish and then acts on the saved bytes: no reload
lands over an edit made during the write, no save publication is retired
by a reload's epoch bump ("Saved" is always spoken), and a property commit
in a split-pane peer never produces a conflict prompt for work the reader
did in the same window a moment apart — it also waits for its header to
re-read the saved bytes, so its CAS token is the saved hash.

## Current state on main

Verified against the worktree (the plan's and `report_rel.md`'s lines
hold).

- `WorkspaceSaveCoordinator.cs:83-87` `HasAdmittedSaveFor(path)`: true
  while the ticket recorded as the tail of the path's folded key
  (`PathKey`, `:74-75`) has not completed; dispatcher-only. `:94-131`
  `Enqueue(keys, start)` makes each new ticket the tail of every key it
  names (`:112-115`) and runs `start` on the dispatcher once every earlier
  save under any of the keys has completed — this is the join primitive
  the chunk reuses. `KeysFor(tab, path)` (`:68-69`) is the tab-plus-path
  key set a save uses. So while an operation admitted under the path
  runs, `HasAdmittedSaveFor(path)` is true because of that operation's
  own ticket, or of a save queued behind it. `IsIdle` (`:42`) is true when
  no admitted ticket is pending; `IsClosed` (`:45`) once teardown's
  `CloseAndJoinWorkers` (`:228-249`) ran.
- `WorkspaceViewModel.cs:3003-3034` `KeepsBufferOnReseat(tab, stored)`:
  the one save-aware rule (`tab.HasPendingSaves || _saves.HasAdmittedSaveFor(tab.Path) || _saves.HasAdmittedSaveFor(stored)`),
  used by the Created-event re-seat (`:2966`) and the rescan's
  (`WorkspaceViewModel.Rescan.cs:396`). `_saves` is the workspace's
  coordinator (`:2000-2001`); `SavesForTests` exposes it (`:2842`).
- The tab's save queue (`WorkspaceTabViewModel`, in `WorkspaceViewModel.cs`):
  `:126` `_queuedSave`, the admitted, not-yet-started save that later
  requests join while its `ItemEpoch` equals the tab's `_itemEpoch`
  (`SaveAsync`, `:769-807`); `:849-869` `SaveBatch`, whose `ItemEpoch`
  is init-only (`:853`); `:810` `HasPendingSaves` (the latest admitted
  save has not completed). `StartSave` retires a batch whose `ItemEpoch`
  is not the tab's `_itemEpoch` silently (`:914-918`, `ticket.Complete(false)`,
  nothing spoken) and publishes a clean tab's save with no write
  (`:919-925`). `ReplaceItem` (`:516-612`) bumps `_itemEpoch` (`:528`),
  so a same-item reload of a tab with a queued save retires that save.
  A landed write's publication (`PublishSave`, `:1067-1150`) adopts the
  written bytes (`AdoptLandedWrite`, `:1170-1193`): a buffer changed while
  the write ran (`MarkSavedBehindEdits`) stays dirty behind the saved
  bytes. Same-path tabs mirror one another's dirty state and saved hash
  (`MirrorDocumentStateFrom`, `:658-692`; `ApplyPeerDocumentEvent`,
  `:694-741`).
- **#1322 site 1:** `WorkspaceViewModel.History.cs:255-269`
  `ReloadOpenTabFromDisk(path)`: `FindPathBackedTab(path)` and, when
  `IsDirty` is false, `tab.ReplaceItem(tab.Item)` — no admitted-save
  check. Its caller is the restore continuation at `:228`, after
  `_session.RestoreVersion(...)` ran on the history worker (`:177-198`,
  a CAS write with `request.ExpectedContentHash`). So the restore's own
  write also runs beside an in-flight save; WD2's "wait" has to cover the
  write, not only the reload. `ReloadOpenTabFromDiskForTests` (`:245-246`)
  is the existing seam and the only other caller. The stage-time rule
  (`RequestRestoreVersion`, `:125-169`) refuses a dirty tab with the
  D-14 line "Save the note before restoring a version. The editor has
  unsaved changes in {file}." (`:131-141`) and stages the expected hash
  by tab kind (`:142-150`, H7/H12): a Markdown tab's `SavedContentHash`;
  for a non-Markdown tab (`.canvas`, `.base`) or no open tab, the loaded
  list's `History.HeadContentHash` (`Panels/HistoryViewModel.cs:218-224`,
  the newest loaded row's hash for the path the panel shows). A
  non-Markdown tab has no saved editor hash: `Load` returns before the
  hash is set for any kind but Markdown (`WorkspaceViewModel.cs:1828-1842`),
  so its `SavedContentHash` stays null. A null staged hash refuses
  silently (`:151-154`), because core documents a null expected hash as
  an unconditional save. `HistoryPanelTests.BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer`
  (`HistoryPanelTests.cs:683-710`) pins the non-Markdown arm: a `.base`
  restore staged on a head hash that an external write made stale gets
  the "Restore failed" alert and leaves the newer bytes. A non-Markdown
  tab's save is keyed by its tab alone (`SaveAsync`,
  `WorkspaceSaveCoordinator.KeysFor(Id, IsMarkdown ? Path : null)`,
  `WorkspaceViewModel.cs:780-781`), so no save of a `.canvas` or `.base`
  file is ever admitted under its path.
- **#1322 site 2:** `WorkspaceViewModel.Templates.cs:267-277`: after the
  create, every clean same-path Markdown tab whose text differs from
  `rendered.Body` (or is missing from disk) gets `ReplaceItem` — no
  admitted-save check.
- **#1325:** `WorkspaceViewModel.Properties.cs:38-50` the note-scoped
  write lease (`_propertyWritePaths`, `TryAcquirePropertyWriteLease`,
  `WithLeaseRelease`); dispatch sites `:205-215` (add,
  `CommitAddProperty`, `:177-216`, CAS token `intent.ContentHash`),
  `:244-250` (Keep Mine add retry, `:237-257`), `:412-437`
  (`DispatchRowWrite`, set/delete, CAS token `row.ContentHash` at
  `:433-436`) and `:507-515` (Keep Mine row retry, `:498-536`). Both
  retries pass `null` and read the disk hash on the worker (`:253`,
  `:519`, `:528`). Each calls
  `tab.WriteProperty(expectedHash, write, completion)`
  (`WorkspaceTabViewModel.Properties.cs:146-215`, returns `void`), which
  runs the core write on an untracked `Task.Run` (`:153`) — never through
  `_saves`. Its publication re-baselines a clean tab in place
  (`:83-125`, no epoch change) and flags a dirty one stale
  (`MarkPropertyStale`, `:127-137`: "Properties changed on disk, but the
  editor no longer matches it. …"). The request-time gates
  (`RefusePropertyWriteGates`, `Properties.cs:454-475`): the row's
  `WriteInFlight` or the note's lease ("Wait for the current save to
  finish.", `:382-387`), a dirty tab (`AnnounceDirtyTabPropertyRefusal`,
  `:444-452`), and a stale tab or `tab.SavedContentHash != row.ContentHash`
  → `AnnouncePropertyConflict` (`:481-492`: `PropertyEditConflict` and
  the dialog — Keep Mine retries the same operation on a fresh hash,
  Reload discards the draft on the row's owner header). The add's twin
  is `AnnounceAddConflict` (`:222-233`); the sheet's collision rule is
  ordinal against the intent's top-level keys (`Panels/AddPropertyViewModel.cs:126`).
  A write's completion (`PropertyWriteCompletion`, `:573-608`;
  `AddWriteCompletion`, `:259-295`) speaks `PropertyChanged`, or
  `PropertyEditConflict` / `PropertyEditFailed`. Within one tab the race
  is closed by the dirty refusal pinned at `PropertiesLandingTests.cs:448`
  `ADayPickedWhileItsNoteSavesWritesNothingUntilTheSavePublishes`. A
  peer tab is not: same-path tabs mirror dirty state, so the window is a
  peer's save whose edit the reader took back while it wrote (both tabs
  clean, the save still admitted) — the commit passes the request-time
  gates and its CAS races the save's.
- **The header refresh is not joined to the save (codex round 7).**
  `SaveActive` (the Save command, `WorkspaceViewModel.cs:3211-3236`)
  passes an `onSaved` that calls `RefreshPropertiesFor(tab.Path)`
  (`:3233`; the W4-4 comment at `:3229-3232`: the header re-derives from
  the just-saved bytes so its rows and CAS tokens are never a stale
  generation behind the tab — contract 4). `onSaved` runs inside the
  save's publication (`PublishSaved`, `:1155-1165`), and the save's
  ticket completes right after, at `:995`. `RefreshPropertiesFor`
  (`Properties.cs:704-724`) calls `NotePropertiesViewModel.RefreshProperties`
  (`Panels/NotePropertiesViewModel.cs:185-218`) on every same-path header
  (the first with `FailureOnly`, the rest `None`): it takes a new request
  id (`:194`), sets `IsLoading`, and starts a worker
  (`PanelWorkScheduler.StartWork`, `Panels/PanelWorkScheduler.cs:142-160`)
  that reads `ReadNoteParts` (`:200`) and later `Post`s `PublishProperties`
  (`:223-304`) to the dispatcher — a read failure is caught and posted as
  `LoadError` (`:208-216`). Nothing joins that publish to the ticket. An
  accepted publish rebuilds every row (`PropertyRowViewModel`, pinning
  the read's `ContentHash`, `PropertyRowViewModel.cs:70-72`, its `Owner`,
  `:57`, and its `CommittedBaseline`, `:98`), parks dirty drafts onto the
  rebuilt row of the same key (`NotePropertiesViewModel.cs:252-279`), and
  clears rows on a failed read (`:262-263`). Stale publishes (generation
  or request id) discard silently (`:234-239`); `Load` of a new path bumps
  the generation and clears rows (`:155-171`); `Shutdown` bumps it
  (`:306-310`), called from the tab's `Dispose` (`WorkspaceViewModel.cs:1203`,
  `ShutdownProperties`). Only the Save command refreshes headers: Save All
  (`WorkspaceViewModel.cs:2779`), the close and replace gates
  (`WorkspaceViewModel.Layout.cs:608`, `:733`) and a bare `SaveAsync`
  start none. So a property write admitted behind a save that read its
  hash at start would read the pre-save row hash and report exactly the
  conflict #1325 removes. `AwaitRepublished` in `PropertiesLandingTests`
  is a test wait, not a production join.
- Test seams: `WorkspaceViewModel.cs:880` `SaveWriteHookForTests` (runs
  on the save worker before the core write), `:885`
  `SaveWrittenHookForTests`; used by `RescanTests.FollowUpB.cs:83-121`
  `ARescanNeverReloadsOverAnAdmittedSave` (the reader takes the edit back
  while the write is parked, so the tab is clean with `HasPendingSaves`
  true) and `:130-172` `ARescansReseatKeepsATabWithAnAdmittedSave`, the
  twin shape for the new facts. `NotePropertiesTests.cs:160-235` runs an
  asynchronous header under a `QueueingSynchronizationContext` whose
  publishes wait until the fact drains them — the shape for the header
  facts.

## Reference

No mac twin (mac has no off-dispatcher save chain). Contract 38 D-10
(`docs/plans/38_notification_dispatcher_contracts.md`): a reload must
never retire an admitted save's publication. Contract 40 R-9's
`KeepsBufferOnReseat` doc (`WorkspaceViewModel.cs:2978-3002`) is the rule
being extended. W4-4 contract 4: the header is the single hash authority
for property writes.

## Design

WD2 is answered (owner, 2026-10-21; plan §3.1): wait — each site joins
the admitted save through `Admit` (item 1) and then acts on the saved
bytes, #1325 ships, and the admitted property write also waits for the
header refresh the save's publication started (item 4).
`HasAdmittedSaveFor` stays the test of whether some save is admitted —
the unadmitted reload's guard — and an admitted operation never asks it
about the path it holds, because its own ticket makes it true.

1. **New admission primitive (new):**
   `Task<bool> WorkspaceSaveCoordinator.Admit(string path, Func<SaveTicket, Task> operation)`
   — dispatcher-only. It ALWAYS enqueues a ticket under `PathKey(path)`
   through `Enqueue` (`:94-131`), idle chain included, so a save admitted
   after it waits for it exactly as it waits for an earlier save (the
   restore IS a save; the file's chain stays ordered in both directions).
   When the earlier saves have settled, `operation(ticket)` runs on the
   dispatcher; the ticket completes (`ticket.Complete(true)`, or
   `ticket.Fail`) only when the Task the operation returns completes —
   after the operation's dispatcher publication or its terminal outcome,
   never when the call returns. The operation's worker Task is handed to
   `TrackWorker` (`:189-206`) so teardown joins it like a save's. A closed
   coordinator returns `false` and the operation never runs (the caller
   speaks nothing; the shell is closing). Because `Admit` runs an
   operation only after every save admitted before it has completed, the
   saves still pending on the path while the operation runs are its own
   ticket and its queued successors — never an earlier save.
2. **History restore** (`PerformRestore`, `History.cs:171-237`): the
   whole restore is the operation — the worker's `RestoreVersion`
   (`:177-198`) and the continuation (`:199-235`) — and the operation's
   Task completes at the end of that continuation. When the operation
   starts it re-applies the stage-time rules, because it may now start
   later than the stage: a tab that is dirty by then (the reader's edit
   made while the save wrote stays an unsaved change behind the saved
   bytes once the save publishes) gets the existing D-14 line
   (`:131-141`) once and nothing is written. Otherwise the CAS basis is
   chosen by the stage-time rule (`:142-150`), re-applied to what is live
   now: when `FindPathBackedTab(path)` is a Markdown tab whose
   `SavedContentHash` is not null, that hash, so a restore admitted
   behind a save lands on the saved bytes instead of conflicting with
   them; in every other case — a `.canvas` or `.base` tab (it has no
   saved editor hash), no open tab, or a Markdown tab whose load left no
   hash — the request's captured `ExpectedContentHash`, the hash staged
   for this path (`History.HeadContentHash` for a non-Markdown or absent
   tab), never re-read from the History panel, which may show another
   path by then. The stage-time null refusal (`:151-154`) is unchanged,
   so the captured hash is never null and the operation never passes a
   null expected hash to `RestoreVersion` (core's unconditional save). A
   non-Markdown restore therefore keeps main's CAS exactly: no save of
   its file is ever admitted under the path (Current state), so it waits
   behind nothing of the tab's own, and a stale head hash still takes
   the existing "Restore failed" alert. A restore whose Markdown tab
   closed while it waited keeps the captured hash too; if the save it
   waited for changed the file, the CAS takes that same alert — never a
   silent overwrite.

   The continuation reloads through a new admission-aware reload,
   `private void ReloadOpenTabFromDiskAdmitted(string path, WorkspaceSaveCoordinator.SaveTicket admittedBy)`
   (new, `History.cs`), never through the guarded `ReloadOpenTabFromDisk`.
   Inside the operation `HasAdmittedSaveFor(path)` is always true — the
   restore's own ticket is the path's tail until the operation's Task
   completes, and a save queued meanwhile becomes the tail — and a tab
   with a queued save reports `HasPendingSaves`; a guard on either would
   skip the mandatory reload in both queue directions (codex round 6).
   The ticket parameter is the proof of admission: only `admittedBy` and
   its queued successors can be pending (item 1), and neither may block
   the reload. The admitted reload finds the tab as today; a dirty tab is
   not reloaded (unchanged: the buffer outranks the restore); otherwise
   it calls `WorkspaceTabViewModel.ReloadKeepingQueuedSave()` (new,
   `internal void ReloadKeepingQueuedSave()`, `WorkspaceViewModel.cs`),
   then `AttachTabDocumentsIfNeeded` and `ReloadCanvasDocumentAt` as
   today. `ReloadKeepingQueuedSave` is `ReplaceItem(Item)` — a same-item
   reload — followed by re-stamping the tab's queued, not-yet-started
   save (`_queuedSave`, `:126`, when its `ItemEpoch` was the pre-reload
   `_itemEpoch`) to the new item epoch through
   `SaveBatch.Rebase(int itemEpoch)` (new; `SaveBatch.ItemEpoch`, `:853`,
   gains a private setter). While the operation runs, a save of a tab on
   the path can only be queued behind it, never writing, so `_queuedSave`
   is the whole of the tab's successor; without the re-stamp `StartSave`'s
   epoch check (`:914-918`) would retire it silently — "Saved" never
   spoken, the D-10 failure this chunk closes. The successor then starts
   against the reloaded document: clean, it publishes `NoteSaved` with
   no write (`:919-925`); edited after the reload, it writes on the
   restored hash. A tab dirtied while the restore wrote is not reloaded,
   so its successor's CAS from the pre-restore hash raises
   `NoteSaveConflict` and the restore's bytes are never silently
   overwritten.

   The guarded `ReloadOpenTabFromDisk(path)` stays for every unadmitted
   caller (after this chunk, only `ReloadOpenTabFromDiskForTests`,
   `:245-246`) and gains the D-10 guard: a tab with `HasPendingSaves` or
   `HasAdmittedSaveFor(path)` keeps its buffer and `ItemIdentity`.
3. **Template same-path reload** (`Templates.cs:267-277`): the sweep is
   itself an `Admit(path, …)` operation whose Task is already complete
   when it returns (the reload is synchronous on the dispatcher); it
   re-checks `IsDirty` and the text difference at run time (the save may
   have made them equal) and reloads each matching tab through
   `ReloadKeepingQueuedSave()` — never a bare `ReplaceItem`, and never
   under a `HasAdmittedSaveFor` or `HasPendingSaves` guard: the
   template's own ticket is the path's tail while the sweep runs, and a
   save a tab queued between the template's admission and the deferred
   sweep is its successor, whose publication a bare `ReplaceItem` would
   retire (the restore's round-6 defect, here too). With no admitted save
   it runs in the same dispatcher turn as today.
4. **Property writes** (`Properties.cs:205`, `:244`, `:412`, `:507`) —
   WD2's answer: the admitted write waits for the header refresh.
   `CommitAddProperty`, `DispatchRowWrite` and the two Keep Mine retries
   run their request-time gates and acquire the lease as today, then
   `Admit(path, ticket => …)` an operation. The operation returns a Task
   it completes itself (a `TaskCompletionSource`): from the wrapped
   completion (`WithLeaseRelease`, `:46-50`) after a write, or right
   after a terminal non-write outcome below — never earlier, so a save
   admitted behind it waits for its publication. `WriteProperty` returns
   its worker Task (`internal Task WriteProperty(string? expectedHash, Func<string?, SaveReport> write, Action<SaveReport?, VaultException?, string?> completion)`,
   changed from `void`), which the operation hands to `TrackWorker`.
   The lease is held from dispatch to the operation's end, so a second
   commit on the note meanwhile is refused with the existing "Wait for
   the current save to finish." line, whichever row object it comes from
   (the header refresh replaces the committed-from row; its
   `WriteInFlight` stays true until the operation re-targets or ends).
   Every non-write outcome releases the lease and clears the in-flight
   flags BEFORE it speaks or opens the conflict dialog (Keep Mine
   re-acquires the lease and is admitted behind this operation), then
   completes the Task; a fault inside the operation does the same before
   the ticket fails. A closed coordinator (teardown began) counts as
   abandoned at every step: the operation never starts a write after
   `CloseAndJoinWorkers`.

   a. **Start-time gates.** The operation re-resolves the tab
      (`FindTabForPath(path)`): none → the abandoned outcome (c). A dirty
      tab — a taken-back edit is an unsaved change once the save
      published (`AdoptLandedWrite`) — gets the existing dirty-tab line
      once (`AnnounceDirtyTabPropertyRefusal`, `:444-452`) and nothing is
      written (an add also marks its sheet failed); the restore's
      start-time rule.
   b. **The header wait (new).** A row write awaits
      `row.Owner.WhenPublished(path, tab.SavedContentHash)` (a row with
      no `Owner` is abandoned); an add awaits it on the header new
      `private NotePropertiesViewModel? HeaderFor(string path)` returns
      (the header of the first open tab on `path` whose header shows
      `path` — every same-path header reads the same bytes; none →
      abandoned). Keep Mine retries skip the wait: admitted behind the
      save, their worker-side disk read (`:253`, `:519`, `:528`) already
      sees the saved bytes, and they write with no snapshot token. The
      mechanism, in `Panels/NotePropertiesViewModel.cs`:
      `internal Task<HeaderRefreshOutcome> WhenPublished(string path, string? savedHash)`
      (new) and `internal enum HeaderRefreshOutcome { Published, Failed, Abandoned }`
      (new). Called and completed on the header's owner thread (the
      dispatcher), its Task comes from a `TaskCompletionSource` created
      with `RunContinuationsAsynchronously`, so the operation never
      resumes inside a publish. Rules:
      - shut down, or `Path` is no longer `path` → `Abandoned` at once;
      - the current publication already holds the saved bytes
        (`LoadError` null and `ContentHash == savedHash`) → `Published`
        at once (an idle chain stays in today's dispatcher turn);
      - otherwise it registers a waiter whose floor is the header's
        current request id, then, when no refresh is in flight
        (`IsLoading` false — the save refreshed no header: Save All, a
        close's save, a bare `SaveAsync`; or the header failed earlier),
        requests one itself, `RefreshProperties()` with
        `ReloadAnnounce.None` (registered first, so a synchronous
        header's inline publish completes it). A refresh already in
        flight — the Save command's, `FailureOnly` — is never superseded,
        so its failure is still spoken;
      - each ACCEPTED `PublishProperties` (a stale publish discards as
        today and completes nothing) settles the waiters after its rebuild
        and notifications, from a snapshot of the list that is cleared
        first (a synchronous header's re-request publishes inline): a
        failed read → `Failed`; `ContentHash == savedHash` → `Published`;
        another hash from a request above the floor (requested after the
        wait began, so it read the saved bytes or newer: the disk moved)
        → `Published`, and the gates in (c) take it to the conflict path;
        another hash from a request at or below the floor (a read that
        may predate the save) → the waiter is re-armed and the header
        requests one silent refresh, whose id is above the floor;
      - `Load` of another path (the generation bump that discards the
        in-flight read — the superseded refresh) and `Shutdown` (the tab
        closed, or the workspace torn down: `ShutdownProperties` from the
        tab's `Dispose`) complete every waiter `Abandoned`.
      So the wait always ends: an accepted request always publishes (the
      worker posts a read failure as `LoadError`, `:208-216`), and both
      ways a request can be discarded abandon the waiter. Test seams
      (new): `internal Action? RefreshReadHookForTests { get; set; }`,
      run on the refresh worker inside the body's `try` before
      `ReadNoteParts` (`:200`), so a hook that throws presents as a
      failed read; `internal int PublishWaitersForTests { get; }`, the
      registered waiters.
   c. **After the wait** (the operation resumes on the dispatcher and
      re-resolves the tab):
      - `Abandoned`, or the tab gone → nothing written; a row write says
        nothing (its header and the draft in it are gone — the row
        seam's "finds no tab and does nothing" posture, `:366-369`); an
        add marks its sheet failed (`MarkAddFailed`, `AddPropertyViewModel.cs:153`:
        "The property was not added. Your draft is still here.").
      - `Failed` → nothing written; one `PropertyEditFailed` carrying the
        header's `LoadError` (the existing failure event, `:605-606`;
        `"unknown failure"` if a later publication cleared it); an add
        also marks its sheet failed. The save's own refresh, carrying
        `FailureOnly`, has said `PropertiesReloadFailed` for the save;
        this line is the property commit's.
      - `Published` → the held gates on the fresh header: new
        `private bool RefuseHeldPropertyWrite(WorkspaceTabViewModel tab, PropertyRowViewModel row, bool deleted)`
        — `RefusePropertyWriteGates` (`:454-475`) without its in-flight
        gate, which the operation's own lease would trip
        (`RefusePropertyWriteGates` becomes the in-flight gate plus this).
        For a row write, `fresh` is the owner's row with the committed
        row's `KeyIdentity`. A dirty tab → the dirty line. No `fresh`
        (the saved bytes removed the key) → the committed-from row's
        pre-save hash fails the hash gate → `AnnouncePropertyConflict(tab, row, deleted)`.
        A stale tab or `fresh.ContentHash != tab.SavedContentHash` (the
        disk moved past the save) → `AnnouncePropertyConflict(tab, fresh, deleted)`.
        `fresh.CommittedBaseline` not `PropertyDraft.ValueEquals` the
        committed-from row's `CommittedBaseline` (the saved bytes changed
        the edited key's value) → `AnnouncePropertyConflict(tab, fresh, deleted)`
        — the existing conflict path (`PropertyEditConflict` once and the
        dialog); the fresh row carries the user's draft, parked by key at
        the publish. Otherwise the write dispatches: the committed-from
        row's `WriteInFlight` cleared, `fresh.WriteInFlight` set,
        `tab.WriteProperty(fresh.ContentHash, write, WithLeaseRelease(path, PropertyWriteCompletion(path, fresh, deleted, dispatched)))`,
        with the draft captured at commit (`dispatched`, unchanged). For
        an add: the gates against the header's `ContentHash` (a mismatch
        → `AnnounceAddConflict`); the key now among the header's
        `TopLevelKeys` (ordinal, the sheet's rule; the saved bytes added
        it) → `AnnounceAddConflict`; otherwise
        `WriteProperty(header.ContentHash, …)` with the add completion as
        today.

   The header stays the single hash authority (contract 4; the W4-4
   comment at `WorkspaceViewModel.cs:3229-3232`): the operation never
   reads a CAS token from disk and never from a header publication older
   than the save. Not changed: the request-time gates, the hash check,
   the conflict prompt and its resolutions, the lease, the one-tab dirty
   refusal pinned at `PropertiesLandingTests.cs:448`, `SaveActive`'s
   refresh. The property publication re-baselines in place (no epoch
   change), so it needs no queued-save carry.

Not changed: `KeepsBufferOnReseat`, `HasAdmittedSaveFor`, the rescan
paths, mac, core, FFI.

## Tests

`SlateWindows.Tests`, new file `SaveChainAdmissionTests.cs` (class
`SaveChainAdmissionTests`; the harness of `RescanTests.FollowUpB.cs:83-121`
— file of the partial class `RescanTests`; park the save at
`SaveWriteHookForTests`; "takes the edit back" means setting `Text` to
the loaded text while the write is parked, as that fact does; "retypes
the edit" means setting `Text` back to the saved snapshot's text before
release, so the save's publication adopts it and leaves the tab clean).
Every hold is observed: each `ManualResetEventSlim.Wait(TimeSpan)` result
is asserted (`Assert.True(parked.Wait(TimeSpan.FromSeconds(10)), "…never parked")`;
a timeout is the fact's failure, never a silent release) and every gate
is `Set()` in `finally`, the hook nulled there. Every save a fact
observes is requested with `SaveAsync(announce: true)`, so its
publication is a `NoteSaved` the fact can count — except the property
facts' peer save, which goes through the Save command (`SaveActiveCommand`
with tab B active, `WorkspaceViewModel.cs:3211-3236`: `SaveAsync` with
`announce: true` and the `onSaved` whose `RefreshPropertiesFor` starts
the header refresh). In the property facts tabs A and B show the same
note in a split pane and only A's header is attached
(`EnsureActiveTabProperties(synchronousForTests: false)` with A active,
on the harness's dispatcher thread, so its publish posts to the
dispatcher as in production; a synchronous header publishes inside the
save's publication and would hide the race); "A commits `due`" means
setting the `due` row's draft and calling `SetPanelProperty(row)`; the
write's worker counter is a `PropertyWriteHookForTests` that increments
it.

- `AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes`
  — queue direction 1, a restore behind a save: the save parks; the
  reader takes the edit back (the tab clean, so the stage-time refusal
  passes), confirms a restore (admitted behind the save), then retypes
  the edit; after release the save publishes first (`save.Result`
  true, `NoteSaved` once) and adopts the retyped text as the baseline, so
  the tab is clean when the restore starts; the restore writes on the
  saved hash and its continuation — run while the restore's own ticket is
  still the path's tail — reloads the tab: `ItemIdentity` advances
  exactly once and `Text` is the restored version; the saved bytes are a
  history version. Mutations: drop the `Admit` wrap → the restore runs
  first and the ordering assertions fail; call the guarded
  `ReloadOpenTabFromDisk` from the continuation → the restore's own
  ticket keeps `HasAdmittedSaveFor` true, the reload is skipped and
  `Text` stays the saved text; keep the request's captured hash for the
  live Markdown tab (no start-time refresh) → the restore's CAS carries
  the pre-save hash against the saved bytes: the "Restore failed" alert
  ("The file changed after history was loaded. …"), no
  `RestoredVersionFrom`, and `Text` stays the saved text.
- `AHistoryRestoreNeverSilencesTheSaveItWaitedFor` — the
  `RestoredVersionFrom` line follows the save's `NoteSaved`; no save
  publication was retired (`SettleSaves()` true; `ItemIdentity`
  unchanged until the restore's own reload).
- `AHistoryRestoreThatFindsItsTabDirtyWhenItStartsRefusesAndWritesNothing`
  — the first fact without the retyping: once the save publishes, the
  reader's taken-back text is an unsaved change, so the restore's start
  speaks the D-14 line once, writes nothing (the file holds the saved
  bytes; no `RestoredVersionFrom`), and leaves the buffer and
  `ItemIdentity`; its ticket completes, so a save requested afterwards
  writes. Mutation: drop the start-time re-check → the restore writes
  under the dirty tab and `RestoredVersionFrom` is spoken while the
  editor shows the reader's text.
- `ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication`
  — queue direction 2, a save behind a restore: the restore's worker
  parks at a new `HistoryRestoreHookForTests` (`internal Action? HistoryRestoreHookForTests { get; set; }`
  on the workspace, run on the history worker before `RestoreVersion`,
  the `SaveWriteHookForTests` shape); the clean tab requests a save
  meanwhile (a queued successor): `save.IsCompleted` stays false while
  the restore is parked; after release the continuation reloads the tab
  despite the successor (`ItemIdentity` advances once, `Text` is the
  restored version), then the successor publishes `NoteSaved` once,
  after `RestoredVersionFrom`, with `save.Result` true; the file holds
  the restore's bytes. Mutations: complete the ticket when `Admit`'s
  operation returns instead of when its Task completes → `NoteSaved`
  comes before `RestoredVersionFrom`; guard the admitted reload with
  `HasPendingSaves || HasAdmittedSaveFor` → the successor suppresses the
  reload and `Text` stays the pre-restore text; drop the `Rebase` → the
  reload's epoch bump retires the successor (`save.Result` false, no
  `NoteSaved`).
- `ADirtySaveQueuedBehindARestoreReportsAConflictAndOverwritesNothing`
  — queue direction 2 with an edit: the restore's worker parked, the
  reader edits and requests a save; after release the dirty tab is not
  reloaded (the edit stays, `IsDirty` true), and the successor's CAS
  from the pre-restore hash speaks `NoteSaveConflict` once
  (`save.Result` false); the file holds the restore's bytes. Mutation:
  drop the `IsDirty` check in the admitted reload → the edit is replaced
  by the restored text.
- `TheUnadmittedReloadStillKeepsATabWithAnAdmittedSave` — the guard kept
  for unadmitted callers: the save parks, the reader takes the edit back
  (clean, `HasPendingSaves` true), `ReloadOpenTabFromDiskForTests(path)`
  leaves `ItemIdentity` and `Text` unchanged; after release the save
  publishes `NoteSaved` once. Mutation: drop the D-10 guard from
  `ReloadOpenTabFromDisk` → `ItemIdentity` changes and the save's
  publication is retired (no `NoteSaved`).
- `ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave` — a clean
  same-path tab with a parked save keeps its buffer and `ItemIdentity`
  through the template create; a save that tab requests while the sweep
  waits is queued behind it; after release the tab reloads once and that
  queued save still publishes `NoteSaved` once. Mutations: remove the
  admission → `ItemIdentity` changes under the save; a bare `ReplaceItem`
  in the sweep → the queued save is retired (no `NoteSaved`).
- `APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce` (#1325)
  — B's save parked; the reader takes the edit back (both tabs clean:
  #1325's window); A commits `due` (admitted behind the save); the reader
  retypes the edit; no property write until release (worker counter 0);
  after it, `NoteSaved` once, then one property write on the saved hash
  (`PropertyChanged` once), no `PropertyEditConflict`, the conflict
  dialog never invoked; the file holds both the edit and the new `due`.
  Mutation: call `WriteProperty` directly → the property write lands
  while the save is parked, and the save's CAS then fails:
  `NoteSaveConflict` once and the file lacks the edit.
- `AnAdmittedPropertyWriteWaitsForTheHeaderRefreshTheSavePublished`
  (WD2's answer; codex round 7) — the previous fact's steps, with A's
  `RefreshReadHookForTests` armed to park before release: the save
  publishes (`NoteSaved` once, the tabs clean) and A's header read parks
  (`Assert.True(readParked.Wait(TimeSpan.FromSeconds(10)))`); the
  operation has started and waits (`PublishWaitersForTests == 1`, pumped
  until true) and the write has not started (worker counter 0, no new
  `due` in the file, `PropertyWriteInFlightFor(path)` true, no
  `PropertyEditConflict`); after the read is released the header
  publishes the saved hash and the write runs once: `PropertyChanged`
  once, no `PropertyEditConflict`, the dialog never invoked, the file
  holds the edit and the new `due`, and `HasAdmittedSaveFor(path)` is
  false. Mutation: read the CAS token from the committed-from row at the
  operation's start without awaiting `WhenPublished` → its pre-save hash
  fails the gate while the read is parked: `PropertyEditConflict` once
  and the waiter count never reaches 1.
- `AnAdmittedPropertyWriteWhoseHeaderRefreshFailsWritesNothingAndEnds`
  — as the previous fact to the parked read; the hook then throws, so
  the header publishes `LoadError`: the operation ends `Failed` — worker
  counter 0, the file holds the saved bytes without the new `due`,
  `PropertyEditFailed` once, no `PropertyEditConflict`,
  `PropertyWriteInFlightFor(path)` false, `HasAdmittedSaveFor(path)`
  false — and a save requested afterwards publishes `NoteSaved` (nothing
  hangs). Mutation: settle waiters only on a matching hash (a failed
  publication ignored) → the operation never ends: the lease and the
  admitted ticket stay held past a 10-second pump and the later save
  never publishes.
- `AnAdmittedPropertyWriteWhoseHeaderMovesToAnotherNoteWritesNothingAndEnds`
  — the superseded refresh: as above to the parked read; A's header is
  then pointed at `b.md` (`Load("b.md")`, the generation bump a re-pointed
  tab's activation makes through `EnsureActiveTabProperties`,
  `Properties.cs:328-331`), so the parked read's publish will be
  discarded: the operation ends `Abandoned` — nothing written to `a.md`,
  none of `PropertyChanged`, `PropertyEditConflict`, `PropertyEditFailed`
  spoken, the lease released, the ticket completed; the parked read,
  released afterwards, publishes nothing for `a.md`. Mutation: leave the
  waiters registered on `Load` of another path → the lease and the
  ticket stay held past a 10-second pump.
- `AnAdmittedPropertyWriteWhoseKeyTheSaveChangedTakesTheConflictPath` —
  B's edit also changes `due: 2026-09-10` to `due: 2026-09-12`; saved
  and parked, taken back, A commits `due` = 2026-09-11 from the row whose
  baseline is 2026-09-10, the edit retyped; after release the header
  publishes the saved hash with `due` = 2026-09-12, so the existing
  conflict path runs: `PropertyEditConflict` once, the dialog invoked
  once for `due`, worker counter 0 (the file holds `due: 2026-09-12`), the
  fresh `due` row carries A's draft (2026-09-11), the lease released, the
  ticket completed. Mutation: drop the baseline re-validation → the write
  lands on the saved hash: the file holds `due: 2026-09-11` and no
  `PropertyEditConflict` is spoken.
- `AnAdmittedPropertyWriteThatFindsItsNoteDirtyWhenItStartsRefusesAndWritesNothing`
  — the #1325 fact without the retyping: the save's publication leaves
  the tabs dirty (the taken-back text behind the saved bytes), so the
  operation's start speaks the dirty-tab line ("Save the note before
  editing properties. The editor has unsaved changes in a.md.") once and
  writes nothing (worker counter 0); no `PropertyEditConflict`; the
  lease released; the ticket completed. Mutation: drop the operation's
  dirty gates (at start and in `RefuseHeldPropertyWrite`) → the property
  is written under the dirty tab and the "Properties changed on disk, but
  the editor no longer matches it." line is spoken.
- `AnAdmittedPropertyAddWaitsForTheHeaderRefreshAndAddsOnTheSavedHash`
  — the add twin: A's add sheet opened before the save (its intent pins
  the pre-save hash), the save parked, the edit taken back, the add
  committed (admitted), the edit retyped, A's header read parked, then
  released: one write on the saved hash, the sheet closed, `PropertyChanged`
  once, no `PropertyEditConflict`, the file holds the edit and the new
  key. Mutation: write the add with `intent.ContentHash` → the held gate
  sends it to `AnnounceAddConflict`: `PropertyEditConflict` once and the
  sheet marked failed.
- `ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion` (#1325)
  — the property write's worker is parked at `PropertyWriteFaultForTests`'s
  neighbour seam (a new `internal Action? PropertyWriteHookForTests { get; set; }`
  on `WorkspaceTabViewModel`, run on the worker before the core write);
  the clean tab requests a save meanwhile: `save.IsCompleted` stays false
  while the write is parked; after release `PropertyChanged` is spoken,
  then `NoteSaved` once with `save.Result` true — the property's
  publication re-baselined the clean tab, so the save writes nothing;
  one write in all, the file holds the property. (A dirty tab's save
  would take the property publication's stale flag and report
  `NoteSaveConflict` — main's posture for a property written under a
  dirty buffer, not this chunk's.) Mutation: complete the ticket when
  the operation returns → `NoteSaved` before `PropertyChanged`.
- `TeardownJoinsAnAdmittedRestoreAndPropertyWrite` — dispose the
  workspace with each operation parked: `Dispose` waits for the tracked
  worker (contract 35 A-1) and the session is not disposed under it; and
  with a property operation parked in the header wait (A's read parked,
  `PublishWaitersForTests == 1`): `Dispose` returns, the tab's
  `ShutdownProperties` abandons the waiter, and after a drain
  `SavesForTests.IsIdle` is true, the worker counter is 0 and no property
  line is spoken. Mutations: skip `TrackWorker` for the operation → the
  join returns early and the fact's "worker still running at dispose"
  probe fires; leave waiters registered on `Shutdown` → the coordinator
  is never idle after `Dispose`.
- `NotePropertiesTests` (existing file and class, the asynchronous
  header under its `QueueingSynchronizationContext`, `:160-235`; the
  note rewritten through `session.SaveText`):
  `WhenPublishedWaitsPastAReadOlderThanTheSaveAndAsksAgain` (on a header
  that shows hash H0, `WhenPublished(path, H0)` is `Published` at once;
  a refresh is requested and its worker drained, so its queued publish
  holds H0; the note is rewritten (H1); `WhenPublished(path, H1)` is not
  complete; draining the queue publishes H0 — still not complete, and
  `IsLoading` is true again (the header asked for a fresh read); draining
  again publishes H1 → `Published`; mutation: drop the floor, so any
  other-hash publication counts as newer → the wait completes on the H0
  publication),
  `WhenPublishedEndsFailedWhenTheReadFails` (the note deleted on disk; a
  wait with no refresh in flight requests one; after the drains it is
  `Failed` and `LoadError` is set; mutation: ignore failed publications
  → the wait is still incomplete after the drains),
  `WhenPublishedEndsAbandonedOnShutdownAndOnAnotherNote` (a wait
  registered while a refresh is queued ends `Abandoned` on
  `Load("other.md")`; another ends `Abandoned` on `Shutdown()`; a wait on
  a shut-down header, or for a path the header no longer shows, is
  `Abandoned` at once; mutation: leave waiters registered on `Load` and
  `Shutdown` → both waits stay incomplete).
- `WorkspaceSaveCoordinatorTests` (new file, class
  `WorkspaceSaveCoordinatorTests`):
  `AdmitAlwaysReservesThePathEvenOnAnIdleChain` (a save enqueued right
  after an idle `Admit` waits for the operation's Task),
  `AdmitCompletesItsTicketOnlyWhenTheOperationsTaskCompletes`,
  `AdmitOnAClosedCoordinatorReturnsFalseAndRunsNothing`.
- `HistoryPanelTests` (existing file and class, `HistoryPanelTests.cs`;
  its harness — `NewWorkspace`, `SaveVersion`, `_announced`, `_alerts` —
  and the twin of `BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer`,
  `:683-710`): `BaseFileRestoreWithItsTabOpenKeepsTheStagedHeadHash` —
  `Guard.base` saved twice (`name: One`, then `name: Two`);
  `workspace.OpenPath("Guard.base")`, with the precondition asserted (the
  active tab's `Path` is `Guard.base`, `IsMarkdown` false,
  `SavedContentHash` null); `History.NoteChanged("Guard.base")` and the
  oldest non-marker row taken; an external `SaveVersion` writes
  `name: Three`, so the staged head hash is stale; `RequestRestoreVersion(oldest)`
  → exactly one alert, starting "Restore failed: ", no
  `RestoredVersionFrom`, and the file still holds `name: Three`. (An
  idle chain runs `Admit`'s operation in the same dispatcher turn —
  `Enqueue` calls `Run` at once when no earlier ticket is pending,
  `WorkspaceSaveCoordinator.cs:115-118` — and the harness's history
  worker is synchronous, so the fact's synchronous shape is main's.)
  Mutation: take the start-time basis from whatever tab is open on the
  path (`FindPathBackedTab(path) is { } open ? open.SavedContentHash : request.ExpectedContentHash`)
  → the `.base` tab's null hash reaches `RestoreVersion` as core's
  unconditional save: `name: One` lands, no alert, `RestoredVersionFrom`
  spoken. The existing `:683` fact is the absent-tab witness for the
  same rule: re-reading the basis as `FindPathBackedTab(path)?.SavedContentHash`
  (the round-10 design's unconditional refresh) passes null with no tab
  open, and that fact fails the same way.

Existing facts that must keep passing: `PropertiesLandingTests.cs:448`,
`RescanTests.FollowUpB.cs:83`, `:130`, `SaveOffDispatcherTests`, the rest
of `NotePropertiesTests`; every History restore fact in `HistoryPanelTests`
(`RestoreRoundTripsThroughCoreAndReloadsTheTab`, `:362`;
`DirtyTabRestoreRefusesWithTheSharedSentence`, `:424`;
`RestoreConflictAlertsWithTheRecordedTitleAndReloads`, `:651`, whose open
Markdown tab's stale saved hash still conflicts after the start-time
refresh; `BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer`, `:683`), now
that every restore runs through `Admit`; and the two facts that drive the
guarded reload directly, `CanvasDocumentTests.RestoringAVersionReloadsAnOpenCanvasTab`
(`CanvasDocumentTests.cs:2909`) and `CanvasDocumentTests.TheReloadKeepsTheSharedDocumentObject`
(`:2946`), whose clean canvas tabs have no admitted save, so the new D-10
guard leaves their reload in place.

## Docs and registers

- Contract 38: append a D-10 note ("D-10 extended to the History
  restore, the template reload and property writes; an admitted
  operation's own reload runs inside its ticket and carries a queued
  save across its epoch bump; an admitted property write waits for its
  header to publish the saved hash (`NotePropertiesViewModel.WhenPublished`)
  and re-validates the edited row before it writes; WB-2, WD2 answered
  2026-10-21, #1322 #1325") under the plan-44 heading; never rewrite the
  frozen row.
- No census, designation or `chords.json` change; no new sentence: the
  restore's start-time refusal reuses the existing D-14 line, and the
  property operation's outcomes reuse the dirty-tab line,
  `PropertyEditConflict`, `PropertyEditFailed`, `PropertyChanged` and the
  sheet's `AddFailedDraftKept`.

## Verification

```powershell
$ErrorActionPreference = 'Stop'
$env:CARGO_TARGET_DIR = 'C:\dev\slate\target-wb02'
./apps/slate-windows/generate-bindings.ps1 -Locked   # throws on failure
dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)" }
$required = @(
  'AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes',
  'AHistoryRestoreNeverSilencesTheSaveItWaitedFor',
  'AHistoryRestoreThatFindsItsTabDirtyWhenItStartsRefusesAndWritesNothing',
  'ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication',
  'ADirtySaveQueuedBehindARestoreReportsAConflictAndOverwritesNothing',
  'TheUnadmittedReloadStillKeepsATabWithAnAdmittedSave',
  'ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave',
  'APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce',
  'AnAdmittedPropertyWriteWaitsForTheHeaderRefreshTheSavePublished',
  'AnAdmittedPropertyWriteWhoseHeaderRefreshFailsWritesNothingAndEnds',
  'AnAdmittedPropertyWriteWhoseHeaderMovesToAnotherNoteWritesNothingAndEnds',
  'AnAdmittedPropertyWriteWhoseKeyTheSaveChangedTakesTheConflictPath',
  'AnAdmittedPropertyWriteThatFindsItsNoteDirtyWhenItStartsRefusesAndWritesNothing',
  'AnAdmittedPropertyAddWaitsForTheHeaderRefreshAndAddsOnTheSavedHash',
  'ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion',
  'TeardownJoinsAnAdmittedRestoreAndPropertyWrite',
  'WhenPublishedWaitsPastAReadOlderThanTheSaveAndAsksAgain',
  'WhenPublishedEndsFailedWhenTheReadFails',
  'WhenPublishedEndsAbandonedOnShutdownAndOnAnotherNote',
  'AdmitAlwaysReservesThePathEvenOnAnIdleChain',
  'AdmitCompletesItsTicketOnlyWhenTheOperationsTaskCompletes',
  'AdmitOnAClosedCoordinatorReturnsFalseAndRunsNothing',
  'ADayPickedWhileItsNoteSavesWritesNothingUntilTheSavePublishes',
  'ARescanNeverReloadsOverAnAdmittedSave',
  'ARescansReseatKeepsATabWithAnAdmittedSave',
  'BaseFileRestoreWithItsTabOpenKeepsTheStagedHeadHash',
  'BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer',
  'RestoreRoundTripsThroughCoreAndReloadsTheTab',
  'DirtyTabRestoreRefusesWithTheSharedSentence',
  'RestoreConflictAlertsWithTheRecordedTitleAndReloads',
  'RestoringAVersionReloadsAnOpenCanvasTab',
  'TheReloadKeepsTheSharedDocumentObject')
$filter = 'FullyQualifiedName~SaveChainAdmissionTests|FullyQualifiedName~WorkspaceSaveCoordinatorTests|FullyQualifiedName~NotePropertiesTests|FullyQualifiedName~PropertiesLandingTests|FullyQualifiedName~RescanTests|FullyQualifiedName~SaveOffDispatcherTests|FullyQualifiedName~HistoryPanelTests|FullyQualifiedName~CanvasDocumentTests.RestoringAVersionReloadsAnOpenCanvasTab|FullyQualifiedName~CanvasDocumentTests.TheReloadKeepsTheSharedDocumentObject'
$results = Join-Path $env:TEMP 'wb02-results'
if (Test-Path $results) { Remove-Item $results -Recurse -Force }
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --no-build --configuration Release --filter $filter --logger 'trx;LogFileName=wb02.trx' --results-directory $results
if ($LASTEXITCODE -ne 0) { throw "dotnet test failed ($LASTEXITCODE)" }
[xml]$trx = Get-Content (Join-Path $results 'wb02.trx')
$passed = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Passed' } | ForEach-Object { $_.testName })
$missing = @($required | Where-Object { $fact = $_; -not ($passed | Where-Object { $_ -like "*.$fact" }) })
if ($missing.Count -gt 0) { throw "required facts not selected and passed: $($missing -join ', ')" }
$files = @(
  'apps/slate-windows/src/SlateWindows/WorkspaceSaveCoordinator.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.History.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Templates.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceViewModel.Properties.cs',
  'apps/slate-windows/src/SlateWindows/WorkspaceTabViewModel.Properties.cs',
  'apps/slate-windows/src/SlateWindows/Panels/NotePropertiesViewModel.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/SaveChainAdmissionTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/WorkspaceSaveCoordinatorTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/NotePropertiesTests.cs',
  'apps/slate-windows/tests/SlateWindows.Tests/HistoryPanelTests.cs')
dotnet format apps/slate-windows/SlateWindows.slnx --verify-no-changes --include $files
if ($LASTEXITCODE -ne 0) { throw "dotnet format reports changes ($LASTEXITCODE)" }
```

(`RescanTests` is the real class name; `RescanTests.FollowUpB` is only
a file suffix and would select nothing. The TRX check proves each named
fact was selected by the filter and passed; xUnit's `testName` is the
fully qualified `Namespace.Class.Method`. `WorkspaceViewModel.cs` holds
`WorkspaceTabViewModel`, where `ReloadKeepingQueuedSave` and
`SaveBatch.Rebase` land. `HistoryPanelTests` is selected whole, so every
History restore fact on main runs beside the new one; the two
`CanvasDocumentTests` facts are selected by name, because they drive the
guarded reload this chunk changes. Three of the touched sources carry
mixed line endings on main — CR bytes: `WorkspaceViewModel.cs` 9,
`.History.cs` 3, `.Properties.cs` 21 — so count them before and after
editing; a flattened file shows up in the `dotnet format` gate.) CI: the
always-on Windows lane. AT: none — the answered WD2 adds no sentence.

## Acceptance

- [ ] WD2's answer quoted in the PR body: "answered 2026-10-21: wait; the property write waits for the header refresh" (#1325 ships).
- [ ] The sixteen `SaveChainAdmissionTests` facts, the three `NotePropertiesTests` `WhenPublished` facts and the three coordinator facts fail first, then pass; `HistoryPanelTests.BaseFileRestoreWithItsTabOpenKeepsTheStagedHeadHash` passes on main (it pins main's non-Markdown CAS basis) and fails under the unconditional-refresh mutation, as `BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer` does; mutations run locally per §4 rule 1 (both queue directions, the header wait, the failed and the abandoned refresh, the baseline re-validation among them); every hold's wait result asserted.
- [ ] The restore's continuation reloads through `ReloadOpenTabFromDiskAdmitted`, and the guarded `ReloadOpenTabFromDisk` keeps its D-10 guard for unadmitted callers.
- [ ] An admitted property write awaits `NotePropertiesViewModel.WhenPublished` before it reads its CAS token, and every outcome of the wait (published, failed, abandoned) ends the operation and its ticket.
- [ ] `PropertiesLandingTests.cs:448`, the FollowUpB facts, every History restore fact in `HistoryPanelTests` (`:362`, `:424`, `:651`, `:683`) and the two canvas reload facts (`CanvasDocumentTests.cs:2909`, `:2946`) unchanged and green.
- [ ] The restore's start-time CAS basis is a live Markdown tab's non-null `SavedContentHash`, else the request's captured, path-specific hash (main's `History.HeadContentHash` for a non-Markdown or absent tab); a null expected hash never reaches `RestoreVersion`.
- [ ] Contract 38 D-10 note appended.
- [ ] PR body: `Closes #1322`, `Closes #1325`.

## Brief

Read `WorkspaceSaveCoordinator.cs:27-131` and `:187-314` (`IsIdle`,
`IsClosed`, `Enqueue`, `TrackWorker`, `CloseAndJoinWorkers`,
`SaveTicket`), `WorkspaceViewModel.cs:120-130`, `:516-612`
(`ReplaceItem`), `:658-741` (same-path mirroring), `:769-1006`
(`SaveAsync`, `HasPendingSaves`, `SaveBatch`, `StartSave`; the ticket
completes at `:995`), `:1067-1193` (`PublishSave`, `PublishSaved`,
`AdoptLandedWrite`), `:2978-3034`, `:3089-3209` (`Dispose`) and
`:3211-3236` (`SaveActive`, whose `onSaved` calls `RefreshPropertiesFor`
at `:3233`), `WorkspaceViewModel.History.cs:113-269`,
`WorkspaceViewModel.Templates.cs:226-287`,
`WorkspaceViewModel.Properties.cs:38-52`, `:177-333`, `:364-608` and
`:704-736`, `WorkspaceTabViewModel.Properties.cs:24-215`,
`Panels/NotePropertiesViewModel.cs:44-311` (`Load`, `RefreshProperties`
— a worker reads `ReadNoteParts` and `Post`s `PublishProperties` later,
unjoined to the save's ticket — `PublishProperties`, `Shutdown`),
`Panels/PanelWorkScheduler.cs:142-176` and `:309-322`,
`Panels/PropertyRowViewModel.cs:54-110` and `:255-266`,
`Panels/AddPropertyViewModel.cs:19-20` and `:106-153`,
`Panels/HistoryViewModel.cs:218-224` (`HeadContentHash`, the newest
loaded row's hash), `WorkspaceViewModel.cs:780-781` (a non-Markdown
tab's save is keyed by the tab alone) and `:1828-1842` (`Load` sets
`SavedContentHash` only for Markdown), and the twin facts at
`RescanTests.FollowUpB.cs:76-172`, `PropertiesLandingTests.cs:448-515`,
`NotePropertiesTests.cs:160-235`, `HistoryPanelTests.cs:362-710` (the
restore facts; `BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer` at
`:683` pins the non-Markdown CAS basis) and `CanvasDocumentTests.cs:2909-2963`
(the two facts that drive the guarded reload). Decision WD2 is answered
(2026-10-21: wait; the property write waits for the header refresh):
every site joins the admitted save and then acts, #1325 ships, and an
admitted property write also waits until its header has re-read the
saved bytes; the header stays the single hash authority (contract 4).
Build: `Task<bool> WorkspaceSaveCoordinator.Admit(string path, Func<SaveTicket, Task> operation)`
— always enqueues a ticket under the path key, runs the operation on the
dispatcher after every earlier save settles (so only its own ticket and
its queued successors can be pending while it runs), completes the
ticket only when the operation's Task completes, tracks its worker for
teardown; a closed coordinator returns `false` and runs nothing. Wrap
the History restore (write and continuation) in it; at the operation's
start re-apply the stage-time rules — a tab dirty by then gets the
existing D-14 line ("Save the note before restoring a version. The
editor has unsaved changes in {file}.") and nothing is written;
otherwise the CAS basis follows the stage-time kind rule (`History.cs:142-154`)
applied to what is live: a Markdown tab on the path whose
`SavedContentHash` is not null → that hash (so a restore behind a save
lands on the saved bytes); a `.canvas` or `.base` tab (no saved editor
hash: it stays null), no open tab, or a Markdown tab with no hash → the
request's captured `ExpectedContentHash` (main staged it from
`History.HeadContentHash` for a non-Markdown or absent tab; never re-read
from the panel, which may show another path by then). The stage-time
null refusal is unchanged, so the captured hash is never null and no
restore ever sends core a null expected hash (its unconditional save). The
continuation reloads through new
`private void ReloadOpenTabFromDiskAdmitted(string path, WorkspaceSaveCoordinator.SaveTicket admittedBy)`,
which consults neither `HasAdmittedSaveFor` (true because of its own
ticket or a queued successor) nor `HasPendingSaves`, skips a dirty tab,
and otherwise calls new `internal void WorkspaceTabViewModel.ReloadKeepingQueuedSave()`:
`ReplaceItem(Item)` then re-stamp the tab's queued, not-yet-started
`_queuedSave` to the new item epoch with new `SaveBatch.Rebase(int itemEpoch)`
(`ItemEpoch` gains a private setter), so `StartSave`'s epoch check never
retires the successor. The guarded `ReloadOpenTabFromDisk` keeps a D-10
guard (`HasPendingSaves || HasAdmittedSaveFor(path)` keeps the buffer)
for unadmitted callers (`ReloadOpenTabFromDiskForTests`). Wrap the
template same-path sweep in `Admit` and reload each matching clean tab
through `ReloadKeepingQueuedSave()`, never a bare `ReplaceItem`.
Property writes: `CommitAddProperty`, `DispatchRowWrite` and both Keep
Mine retries keep their request-time gates and lease, then `Admit` an
operation whose Task (a `TaskCompletionSource`) completes from the
wrapped completion after a write or right after a non-write outcome;
`WriteProperty` returns its worker `Task` (changed from `void`), handed
to `TrackWorker`. At start the operation re-resolves the tab (none →
abandoned) and refuses a dirty one with the existing dirty-tab line
("Save the note before editing properties. The editor has unsaved
changes in {file}."), writing nothing. A row write then awaits
`row.Owner.WhenPublished(path, tab.SavedContentHash)`; an add awaits it
on new `private NotePropertiesViewModel? HeaderFor(string path)` (the
first open same-path tab's header that shows the path); Keep Mine
retries skip the wait (their worker reads the disk hash after
admission). New in `Panels/NotePropertiesViewModel.cs`:
`internal Task<HeaderRefreshOutcome> WhenPublished(string path, string? savedHash)`
and `internal enum HeaderRefreshOutcome { Published, Failed, Abandoned }`
— dispatcher-only, its Task from a `TaskCompletionSource` with
`RunContinuationsAsynchronously`; `Abandoned` at once when shut down or
re-pointed; `Published` at once when the current publication has no
`LoadError` and `ContentHash == savedHash`; otherwise a waiter with the
current request id as its floor, plus a silent `RefreshProperties()` when
none is in flight (an in-flight refresh — the Save command's
`FailureOnly` — is never superseded); each accepted `PublishProperties`
settles waiters after its rebuild from a cleared snapshot: a failed read
→ `Failed`, the saved hash → `Published`, another hash above the floor →
`Published` (the disk moved; the gates conflict it), another hash at or
below the floor → re-armed with one silent refresh; `Load` of another
path and `Shutdown` → `Abandoned`. After the wait: `Abandoned` → nothing
written or spoken (an add marks its sheet failed); `Failed` → nothing
written, one `PropertyEditFailed` with the header's `LoadError` (an add
also marks its sheet failed); `Published` → new
`private bool RefuseHeldPropertyWrite(WorkspaceTabViewModel tab, PropertyRowViewModel row, bool deleted)`
(`RefusePropertyWriteGates` minus the in-flight gate its own lease would
trip) against the fresh row of the same `KeyIdentity` — dirty → the
dirty line; no fresh row, a stale tab, or a hash that is not the saved
hash → `AnnouncePropertyConflict`; a fresh `CommittedBaseline` that is
not `PropertyDraft.ValueEquals` the committed-from row's →
`AnnouncePropertyConflict(tab, fresh, deleted)`; otherwise
`WriteProperty(fresh.ContentHash, …)` with `PropertyWriteCompletion(path, fresh, deleted, dispatched)`.
An add checks the header's `ContentHash` and that its key is not now in
the header's `TopLevelKeys` (ordinal), else `AnnounceAddConflict`. Every
non-write outcome releases the lease and clears the in-flight flags
before it speaks or opens the dialog; a closed coordinator counts as
abandoned. Add the seams `internal Action? HistoryRestoreHookForTests`
(workspace, before `RestoreVersion`), `internal Action? PropertyWriteHookForTests`
(`WorkspaceTabViewModel`, before the core property write),
`internal Action? RefreshReadHookForTests` (`NotePropertiesViewModel`,
on the refresh worker inside the `try` before `ReadNoteParts`, so a
throwing hook is a failed read) and `internal int PublishWaitersForTests`
(`NotePropertiesViewModel`). Facts, failing first, in new
`SaveChainAdmissionTests.cs` (every observed save requested with
`SaveAsync(announce: true)`, except the property facts' peer save, which
goes through `SaveActiveCommand` with B active so its publication starts
the header refresh; in the property facts A and B show one note in a
split pane, only A's header attached, asynchronously, on the harness's
dispatcher thread):
`AHistoryRestoreStartedUnderAnAdmittedSaveWaitsThenRestoresOverTheSavedBytes`
(save parked, edit taken back, restore confirmed, edit retyped: the save
publishes first, then the restore's own reload runs under its live ticket
— `ItemIdentity` +1, `Text` restored),
`AHistoryRestoreNeverSilencesTheSaveItWaitedFor` (`NoteSaved` before
`RestoredVersionFrom`, no publication retired),
`AHistoryRestoreThatFindsItsTabDirtyWhenItStartsRefusesAndWritesNothing`
(no retyping: the D-14 line once, file holds the saved bytes, no
`RestoredVersionFrom`),
`ASaveStartedUnderAnAdmittedRestoreWaitsForTheRestoresPublication`
(restore parked at `HistoryRestoreHookForTests`, a clean tab's save
queued: the reload still runs, then `NoteSaved` once after
`RestoredVersionFrom`, `save.Result` true),
`ADirtySaveQueuedBehindARestoreReportsAConflictAndOverwritesNothing`
(edit stays, `NoteSaveConflict` once, file holds the restore's bytes),
`TheUnadmittedReloadStillKeepsATabWithAnAdmittedSave`
(`ReloadOpenTabFromDiskForTests` under a parked save keeps
`ItemIdentity`; the save's `NoteSaved` still spoken),
`ATemplatesSamePathReloadWaitsForTheNotesAdmittedSave` (`ItemIdentity`
kept until release, one reload after, a save queued meanwhile still
speaks `NoteSaved`),
`APropertyCommitInAPeerTabWaitsForTheOtherTabsSaveAndWritesOnce` (B's
save parked, edit taken back, A commits `due`, edit retyped: no write
until release, then `NoteSaved`, one property write on the saved hash,
`PropertyChanged` once, no `PropertyEditConflict`, file holds both),
`AnAdmittedPropertyWriteWaitsForTheHeaderRefreshTheSavePublished` (A's
header read parked after the save published: `PublishWaitersForTests`
1, no write started, no `PropertyEditConflict`; after release one write,
`PropertyChanged` once, no conflict, `HasAdmittedSaveFor` false),
`AnAdmittedPropertyWriteWhoseHeaderRefreshFailsWritesNothingAndEnds`
(the parked read throws: no write, `PropertyEditFailed` once, lease and
ticket released, a later save publishes),
`AnAdmittedPropertyWriteWhoseHeaderMovesToAnotherNoteWritesNothingAndEnds`
(A's header `Load("b.md")` while its read is parked: nothing written or
spoken, lease and ticket released),
`AnAdmittedPropertyWriteWhoseKeyTheSaveChangedTakesTheConflictPath` (the
save changed `due` to 2026-09-12 under A's 2026-09-11 edit:
`PropertyEditConflict` and the dialog once, nothing written, the fresh
row keeps A's draft),
`AnAdmittedPropertyWriteThatFindsItsNoteDirtyWhenItStartsRefusesAndWritesNothing`
(no retyping: the dirty-tab line once, nothing written),
`AnAdmittedPropertyAddWaitsForTheHeaderRefreshAndAddsOnTheSavedHash`
(the add twin: one write on the saved hash, sheet closed, no conflict),
`ASaveStartedUnderAnAdmittedPropertyWriteWaitsForItsCompletion` (a clean
tab's save while the property write is parked: `PropertyChanged`, then
`NoteSaved` once, `save.Result` true, one write in all),
`TeardownJoinsAnAdmittedRestoreAndPropertyWrite` (parked workers joined
by `Dispose`; an operation in the header wait abandoned by the tab's
`ShutdownProperties`, the coordinator idle, nothing written); in
`NotePropertiesTests.cs` (existing class, queueing context):
`WhenPublishedWaitsPastAReadOlderThanTheSaveAndAsksAgain` (`Published`
at once on the current hash; a queued pre-save read does not complete
it and makes the header read again; the fresh read does),
`WhenPublishedEndsFailedWhenTheReadFails` (deleted note: `Failed`,
`LoadError` set), `WhenPublishedEndsAbandonedOnShutdownAndOnAnotherNote`
(`Load("other.md")` and `Shutdown()` each end a wait `Abandoned`; a wait
on a shut-down or re-pointed header is `Abandoned` at once); and in new
`WorkspaceSaveCoordinatorTests.cs`: `AdmitAlwaysReservesThePathEvenOnAnIdleChain`,
`AdmitCompletesItsTicketOnlyWhenTheOperationsTaskCompletes`,
`AdmitOnAClosedCoordinatorReturnsFalseAndRunsNothing`; and in
`HistoryPanelTests.cs` (existing class and harness):
`BaseFileRestoreWithItsTabOpenKeepsTheStagedHeadHash` (`Guard.base`
saved as `name: One` then `name: Two`, opened as a tab — asserted
non-Markdown with a null `SavedContentHash` — the history list loaded,
an external save of `name: Three` making the staged head hash stale;
`RequestRestoreVersion(oldest)` → one "Restore failed: " alert, no
`RestoredVersionFrom`, the file still `name: Three`; it passes on main
and guards the start-time rule). Every hold asserts its `Wait` result
and releases in `finally`. Mutations to run
locally: drop the `Admit` wrap; complete the ticket on return instead of
on Task completion; call the guarded `ReloadOpenTabFromDisk` from the
restore's continuation (direction 1 fails); keep the captured hash for a
live Markdown tab (direction 1's restore conflicts with the saved bytes:
the "Restore failed" alert, no `RestoredVersionFrom`); take the basis
from whatever tab is open on the path, or re-read it unconditionally as
`FindPathBackedTab(path)?.SavedContentHash` (a null hash reaches core as
an unconditional save: `BaseFileRestoreWithItsTabOpenKeepsTheStagedHeadHash`
and `BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer` see `name: One`
land and no alert); guard the admitted reload
with `HasPendingSaves || HasAdmittedSaveFor` (direction 2 fails); drop
`Rebase`; drop the restore's start-time dirty re-check; drop the
admitted reload's `IsDirty` check; drop the unadmitted reload's D-10
guard; a bare `ReplaceItem` in the template sweep; skip `TrackWorker`;
call `WriteProperty` directly (`NoteSaveConflict`); read the row's CAS
token at the operation's start without awaiting `WhenPublished`
(`PropertyEditConflict` while the read is parked); ignore a failed
publication in the waiters (the operation never ends); leave waiters
registered on `Load` of another path, and on `Shutdown`; drop the
`WhenPublished` floor; drop the baseline re-validation (the property
lands over the saved value); drop the operation's dirty gates; write the
add with `intent.ContentHash`. Registers: append the D-10 note to
`docs/plans/38_notification_dispatcher_contracts.md`. Done when the
twenty-three new facts (twenty-two seen failing first, plus the
`HistoryPanelTests` guard fact, which passes on main) and
`PropertiesLandingTests`, `NotePropertiesTests`, `RescanTests`,
`SaveOffDispatcherTests`, `HistoryPanelTests` (every restore fact on
main, `BaseFileRestoreIsCasGuardedWithoutAnEditorBuffer` among them) and
`CanvasDocumentTests.RestoringAVersionReloadsAnOpenCanvasTab` and
`.TheReloadKeepsTheSharedDocumentObject` pass locally (`dotnet test
--filter` by class name — the two canvas facts by method name — with a
TRX log; the run throws on a nonzero exit code and on any required fact
missing from the passed results), `dotnet format --include` over the
eleven files (`WorkspaceSaveCoordinator.cs`, `WorkspaceViewModel.cs`,
`.History.cs`, `.Templates.cs`, `.Properties.cs`,
`WorkspaceTabViewModel.Properties.cs`, `Panels/NotePropertiesViewModel.cs`,
the two new test files, `NotePropertiesTests.cs` and
`HistoryPanelTests.cs`; count the CR bytes of `WorkspaceViewModel.cs`
(9), `.History.cs` (3) and `.Properties.cs` (21) before and after
editing, since main's mixed endings must survive) is clean, and the
Windows lane is green; no
AT pass (no sentence changes). PR body: WD2's answer quoted,
`Closes #1322`, `Closes #1325`. Commit: `fix(windows): a history
restore, a template reload and a property write wait for an admitted
save of the same note, and the property write for its header's re-read`.
