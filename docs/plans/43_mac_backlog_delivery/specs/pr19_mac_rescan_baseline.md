# PR 19 — Mac rescan baseline: Files sidebar Refresh and the foreground rescan (R-9 on mac)

Issue: #1333 (the mac half of #1267's baseline). Size M to L. Lanes:
swift, a11y, windows. Depends on PR 10 (the re-seat) and PR 12 (token
discipline). Parallel with PRs 18, 23, 24. PRs 20 and 22 follow it.

## Goal

The mac has no rescan trigger at all today: a file a sync client drops
into the vault stays invisible until the vault is reopened. This PR
gives mac what Windows shipped in W7-7 PR 7: an explicit Refresh command
that runs core's incremental rescan and re-synchronizes every surface
from the index, and a silent foreground rescan when the app becomes
active after being away, speaking only when something changed or the
scan was incomplete.

## Correction to the issue

There is no `crates/slate-core/src/scan_delta.rs` and no "retained delta
ledger" on `main`. Core's rescan is `rescan(cancel) -> ScanReport` and
`rescan_with_progress(cancel, listener)` (`crates/slate-uniffi/src/lib.
rs:1313-1329`); the report carries `files_seen`, `files_indexed` (a read
count), `files_changed` (new or changed by committed hash), the removed
count, `error_count` and `error_samples` (`:6403-6420`). The host
re-synchronizes from the index afterwards (contract 40 R-9: "it retains
nothing"), comparing open tabs through `indexed_content_hashes(paths,
cancel)` (`:1335-1341`).

## Current state on main

- The only scan is the open scan: `AppState.swift:11538-11555`
  (`scanInitial` plus the `openableDocuments` page loop under one token
  via `withTaskCancellationHandler`); `VaultScanFinished` is posted at
  `:23155` with both counts (OD-6).
- A `didBecomeActiveNotification` hook exists at `SlateMacApp.swift:
  1012-1016` (it refreshes template availability); nothing rescans.
- Command ids live in `SlateCommands.swift`; sidebar actions in
  `Sidebar/SidebarActionCatalog.swift` (the FL-07 shape: registry id,
  palette row, sidebar home, menu item); the Windows chord table names
  the command `slate.sidebar.refresh` (`Commands/ChordTable.cs:437`).
- Core renders `VaultRescanFinished { changed, removed }` ("Files
  refreshed. No changes." / "Files refreshed. {changed} new or changed,
  {removed} removed.") and `VaultRescanIncomplete { errors }` ("Files
  refreshed with errors. {errors} error(s); results may be incomplete.")
  (`a11y.rs:1985-1998`).

## Windows reference

- `VaultLifecycleViewModel.Rescan.cs:81-130`: `RescanAsync(reason)`; a
  request during a running rescan joins a pending-reason lattice (never
  dropped; Explicit dominates Foreground); blockers are the initial open
  scan, an import and a trash operation; a 5 s cooldown applies only to
  starting a Foreground run. `:200-250`: ONE cancel token for the whole
  run (the scan and every page read), cancelled on close or vault switch.
- `ForegroundRescanRoute.cs:23-80`: the first activation never rescans;
  a rescan is forwarded only after at least 2 s away and when no modal
  surface is open.
- The re-sync (R-9, contract 40 `:23`): every step awaited to its
  publication, every core call through the rescan worker with the run's
  token (`ListFiles` included, pages of 1..=10 000), nothing speaks or
  moves the keys; the files tree refreshes silently
  (`FilesSidebarViewModel.TreeOperations.cs:96` `RefreshForRescanAsync`,
  failures counted not announced); open tabs are compared through
  `indexed_content_hashes` and clean tabs whose hash changed reload, nil
  marks missing, and missing tabs re-seat (`WorkspaceViewModel.Rescan.
  cs:316` `ReseatMissingTabsAsync`, PR 10's rule); Quick Open and the
  dependents (backlinks, outline, embeds leaves) republish through their
  own surfaces; a removed selected row lands the keys on the nearest
  survivor without selecting it.
- Announcement policy: an explicit Refresh always speaks the finished
  sentence; a foreground rescan speaks only when `changed + removed > 0`
  or the scan was incomplete; the incomplete sentence counts the tree
  failures too.

## Design

1. **Command.** Registry id `slate.sidebar.refresh` (same id as Windows),
   palette row "Files: Refresh", sidebar home in the Files action
   catalog, a menu item in the View menu's sidebar group. Disabled with
   a reason while the open scan, an import or a Trash operation is
   running; a request during a running rescan joins the pending lattice.
2. **Run.** `rescanVault(reason:)` on `AppState`: one `CancelToken` for
   the run (PR 12's rule), cancelled on close or vault switch; `session.
   rescan(cancel:)` (or `rescan_with_progress` for the explicit run if
   the scan strip should show progress) on a detached task; then the
   re-sync, every step awaited: the `files` inventory page loop with the
   run's token; the file tree's refetch (silent, failures counted); open
   tabs through `indexedContentHashes` → reload clean changed tabs, mark
   missing, re-seat via PR 10; Quick Open and the leaves through their
   existing reload paths; the selected row landing rule.
3. **Foreground route.** Beside the template hook at `SlateMacApp.swift:
   1012`: track `didResignActive` time; on `didBecomeActive`, if not the
   first activation, at least 2 s away, no sheet or palette open, and the
   5 s cooldown passed, request a Foreground rescan.
4. **Announcements.** Post `VaultRescanFinished` / `VaultRescanIncomplete`
   per the policy above; never speak during the re-sync; never move the
   keys.

## Changes

- New `AppState+Rescan.swift`; `SlateCommands.swift`;
  `Sidebar/SidebarActionCatalog.swift`; `SlateMacApp.swift:1005-1025`;
  the tree view-model's silent refetch; `Workspace/` for the tab reload
  by hash.

## Tests

(`VaultRescanTests`, mirroring the Windows fact names where they exist.)
- `testAnExplicitRefreshIndexesAFileDroppedIntoTheVaultAndSpeaksTheCount`.
- `testAForegroundRescanIsSilentWhenNothingChanged` and
  `testAForegroundRescanSpeaksWhenSomethingChanged`.
- `testAnIncompleteRescanSpeaksTheIncompleteSentence`.
- `testACleanOpenTabReloadsWhenItsIndexedHashChangedAndADirtyOneDoesNot`.
- `testAMissingTabReseatsOnRescan` (PR 10's rule through the rescan).
- `testARequestDuringARunningRescanJoinsAndIsNeverDropped`.
- `testTheForegroundRouteSkipsTheFirstActivationShortAbsencesAndModalSurfaces`.
- `testOneTokenCoversTheScanAndEveryPage` (PR 12's census stays green).

## Accessibility

Two core sentences, spoken on the Windows policy; nothing else speaks;
the keys never move. A short VoiceOver check of an explicit Refresh is
recorded in the PR.

## Docs and registers

`docs/plans/40_nvda_matrix_remediation_contracts.md`: R-9's mac column
and AR-3's "no mac fallback" note updated; contract 38 D-3 amendment
noted for mac. `docs/help/` if the Windows Refresh has a help entry the
mac should mirror. `docs/runbooks/voiceover-feature-test.md` §A gains
the Refresh step.

## Acceptance

- [ ] Refresh and the foreground route exist with the Windows policy.
- [ ] The re-sync covers tree, inventory, open tabs, Quick Open and the
      leaves, silently, with one token.
- [ ] The nine facts pass; registers updated; lanes green.

## Risks

The re-sync touches every surface's reload path; keep each step behind
its existing publication so nothing is re-implemented, and keep the
whole run cancellable so a vault switch mid-run cannot publish into the
next vault.

## Brief

Give mac a rescan. Read `lib.rs:1300-1345` and `:6403-6420`,
`AppState.swift:11530-11560` and `:23140-23165`, `SlateMacApp.swift:
1005-1025`, contract 40 R-9 (`:23`), and the Windows
`VaultLifecycleViewModel.Rescan.cs:81-250`, `ForegroundRescanRoute.cs`,
`FilesSidebarViewModel.TreeOperations.cs:90-140` and
`WorkspaceViewModel.Rescan.cs:300-340`. Note that the issue's "delta
ledger" does not exist: follow R-9's re-sync-from-the-index shape. Add
the `slate.sidebar.refresh` command (registry, palette, sidebar home,
menu), the one-token rescan run with the awaited re-sync (tree,
inventory, open tabs by indexed hash with PR 10's re-seat, Quick Open,
leaves), the foreground route with Windows' first-activation, 2 s away,
modal and 5 s cooldown rules, and the two core sentences on the Windows
policy. Write the nine facts first. Done when they pass, the token
census stays green, and the lanes are green.
