# Cancellable file management (#1126)

## Scope and current state

This specification completes the practical cancellation and destination-loading
follow-up recorded in `31_file_management_contracts.md` FR-9/FR-11. PRs #1201
and #1202 are merged. Their staged Trash inventory and Windows conditional
inverse protections remain prerequisites, as do the write-intent invariants in
`21_write_intent_protocol_invariants.md`.

Current evidence:

- Core exposes a shared cooperative `CancelToken` in `session.rs`, but
  `VaultSession::stage_trash`, staged single/batch deletion, `TrashConfirmation`,
  and the provider inventory have no cancellation parameter.
- `TrashConfirmation::validate_item` and `take_trash_confirmation` currently
  translate all inventory errors to `TrashConfirmationChanged`. Cancellation
  needs its own typed path. The latter also holds the confirmation mutex while
  validating the inventory.
- `FilesSidebarViewModel.Trash.cs` owns a dedicated STA worker and session lease.
  `FilesSidebarViewModel.SessionWork.cs` closes admission before draining work.
- Mac `AppState` stages and executes Trash in detached work and already has
  session/generation ownership. Its cancellation/close hook currently invalidates
  ownership without signalling the native Trash operation.
- Windows `OpenMoveTo` enumerates destinations before modal admission;
  `EnumerateVaultFolders` blocks its caller and owns a token nobody can cancel.
  `MoveToPickerViewModel` currently receives a complete immutable folder list.
- Mac `loadAllFolders` already runs native paging off-main with cancellation.
  `MoveToFolderSheet` must also suppress publication after its task is cancelled.

The new surfaces and APIs below are planned contracts until their implementation
step lands. Contract numbers in this document are local to this work.

## Observable behavior

### C1 — One cancellation owner per operation

An admitted Trash operation owns its captured native session, cancellation token,
and host generation until the native call and required result handling finish.
Cancellation is idempotent. A cancellation request changes the visible state to
stopping and signals core; it does not dispose the session, free an active token,
or pretend the worker has completed.

Single files and empty folders keep one owner across staging and automatic
execution, without an idle gap. A displayed confirmation ends preparation;
confirmation starts execution with a fresh cancellation owner and the original
staged token. No host may restage silently on confirmation.

### C2 — Cooperative core cancellation

Add cancellable variants of staging and staged single/batch Trash, using the
existing `CancelToken`; retain legacy endpoints as uncancelled wrappers.
Inventory checks cancellation before/between entries, enumeration pages, child
opens, and before returning a usable snapshot. Windows and Unix retain their
handle-relative, no-follow traversal, existing budgets, and metadata checks.

Cancellable entry paths check while waiting for the sidecar structural lock,
in-process operation mutex, and connection mutex. Acquire locks in the existing
order. A cancelled waiter must neither mutate files nor block unrelated vaults.
SQLite writer-fence contention must also offer bounded retry/cancellation before
physical mutation without weakening the schema fence or durability protocol.

`Cancelled` must survive error mapping. A failed/cancelled inventory never
produces a confirmation or authorizes an unknown-count deletion. Providers
without cancellable traversal check cancellation around their existing snapshot;
they retain the documented OS-call limitation.

### C3 — Confirmation consumption and dismissal

Claim a matching staged token under a short confirmation-mutex critical section,
then validate under structural ownership. Claiming consumes the token before a
cancellable wait, so a cancelled execution cannot later replay it. Do not hold
the confirmation mutex across inventory I/O.

Expose exact-token discard for dismissed/superseded confirmations. Discarding an
old token must never erase a newer confirmation. Cancelled staging clears only
the staging generation it owns and cannot publish after cancellation.

### C4 — The physical-operation boundary

| Phase when cancellation is observed | Required result |
| --- | --- |
| Preparing, waiting for locks, initial validation/planning | `Cancelled`; no physical mutation and no usable confirmation |
| Final validation before first OS Trash dispatch | `Cancelled` or an explicit report proving no item was attempted; preserve history and tabs |
| An OS Trash call has started | Let that item reach its actual outcome and finish its index/journal/marker bookkeeping |
| Between batch items | Do not dispatch another item; finish and report earlier outcomes, mark remaining plans as cancelled/unattempted |
| All requested items already completed | Report completion; a late Cancel cannot turn success into a false cancellation |

Never escape through `?`/a generic cancellation exception after physical work
and lose the batch ledger. Cancellation is represented explicitly in the batch
failure stage/remainder data; successful, remaining, and unknown items retain
their existing meanings. A stopped batch preserves completed Trash operations;
it does not attempt to restore them from the system Trash.

Marker cleanup is token-scoped. Known unattempted items may clear their own
markers; uncertain outcomes retain recovery evidence. Cancellation never clears
another writer's marker or bypasses normal reconciliation/events.

### C5 — Trash UI and accessibility

Both hosts expose a named, keyboard-reachable Cancel action while preparation or
execution is active. Use existing native controls and progress/status surfaces.
The action remains visible when the relevant sidebar is hidden on Mac.

Provide distinct preparing, executing, stopping, and final states. Announce a
phase change and one final outcome, not every visited entry. Explain that a
current system operation may finish and completed items remain in Trash/Recycle
Bin. Repeated Cancel has no extra side effects or repeated announcements.

Clean cancellation preserves history, selection, and open documents. A partial
or unknown physical outcome uses the existing report-driven refresh, tab
reconciliation, history barrier, and recovery guidance. Final messages survive
asynchronous tree refresh. Completion never steals focus from a newer surface.

### C6 — Close, replacement, and drain

Shutdown closes admission, signals cancellation, and retains/drains native work
before releasing session resources. Old-generation UI completions stay silent.
On Mac, retain an outstanding-native-work/termination fence or defer transition
until executing Trash has drained; resetting the normal structural-busy flag
alone must not make an in-flight physical mutation invisible to termination.

No forced thread termination, abandoned mutation worker, or timeout-based release
of live session resources is allowed. A blocked OS call can delay drain; the UI
must describe stopping honestly rather than claiming that the call was stopped.

### C7 — Windows destination loading

Admit and present the existing Move-To picker immediately in a Loading state,
then enumerate on an admitted worker with an owned cancellation token. Freeze
the move source selection when opening; single and batch execution use that
captured selection rather than re-reading current tree checks.

Publish bounded pages into only the captured picker/generation on the dispatcher.
Keep the existing 1,000-row page size and 50,000-folder bound. Preserve the typed
filter and selection by destination as pages arrive. Exclude illegal destinations
before exposure, using the existing legality rule. Preserve the pinned root and
New Folder behavior.

Cancel/Escape/dismissal, replacement of the picker, and vault shutdown signal the
loader. A stale completion cannot reopen a sheet, alter another picker, reset
focus, or announce a result. Keep errors in the admitted sheet with Retry/Cancel;
retry starts a fresh generation. Never present a partial failed enumeration as a
complete destination set. Loading/error/truncation states have accessible text.

The picker may filter/navigate known rows while loading. Mutating activation is
enabled only after the current enumeration completes successfully; this prevents
moving a source while its destination read still owns session work. Show the cap
as a limit, not a complete-vault count. Hide New Folder until discovery completes;
a partial folder set cannot prove that the typed destination is absent. Cancel
stays available in every state. Guard queued initial focus against the current
sidebar, picker and topmost modal; page publication never moves focus.

### C8 — Mac Move-To publication

Preserve Mac's existing asynchronous paged loader and cancellation bridge. Guard
the sheet's post-await assignments/selection against task cancellation so a
dismissed request cannot publish. Cover this path without inventing a second
loader or changing platform interaction conventions.

## Boundaries and accepted limits

- Cancellation is cooperative between safe boundaries. Synchronous OS calls and
  faulty/unresponsive drivers do not provide a universal interruption deadline.
  This work does not force-cancel threads or promise that every hung filesystem
  call stops immediately. Platform interruption of active mutations is excluded.
- Entry/depth/page limits remain. Cancellation does not add an unsafe traversal
  fallback, silently accept a partial Trash inventory, or relax destination rules.
- #1125 remains open for its final validation-to-system-Trash external-writer
  window and macOS/Linux identity-conditional rename limitations. #1126 can close
  once this specification's cancellable workflows and Windows loader land.
- This work preserves unrelated native UI conventions and existing command
  identifiers. No global product redesign or new telemetry requirement is added.

## Delivery plan and validation

| Step | Deliverable | Verification |
| --- | --- | --- |
| 0 | Commit this spec before implementation review; independent design audit | Check cancellation phases, ownership, and write-intent invariants |
| 1 — PR A | Core/provider/FFI cancellation, exact-token discard, lock waits, typed stopped-batch outcomes | Pre-cancel; cancel during traversal; lock wait; token replay/discard races; between-item cancellation; in-flight success/failure/unknown; marker/index/journal consistency; both binding generators |
| 2 — PR B | Windows and Mac Trash cancellation controls, ownership and lifecycle | Real worker scheduling; repeated Cancel; clean/partial outcomes; no late deletion; close/reopen and termination drain; accessible controls and persistent announcements |
| 3 — PR C | Windows asynchronous Move-To loader and Mac publication guard | Blocked-page responsiveness; paging/filter/selection; stale generation; Retry; cap; frozen batch selection; modal/focus ownership; shutdown drain; keyboard/AT checks |
| 4 | Final integration and issue/contract updates | Platform CI, independent red-team approval before every push, all Codoki comment surfaces reviewed |

Use deterministic barriers and injected provider/page boundaries for concurrency
tests. Do not use filesystem speed as an ordering assertion. Exercise production
worker scheduling. Run repository formatting, clippy, required Rust/host tests,
bench compilation, license checks and native accessibility gates appropriate to
each PR. Generated bindings remain untracked.

PRs are dependent when they consume new APIs; each description states its base
and completed checks. User authorization requires independent review before
push, which takes precedence over the older remote-first review wording in
`24_red_team_protocol.md`. Do not merge new PRs without a new user request.

## Review record

Initial read-only audits identified cancellation swallowed by stale-confirmation
mapping, confirmation mutex held over I/O, batch partial-outcome loss through
early return, Windows source-selection drift, and Mac transition/termination
ownership. C2–C8 include these requirements before implementation.
