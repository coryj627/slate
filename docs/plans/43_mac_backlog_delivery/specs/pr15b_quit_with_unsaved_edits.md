# PR 15b — Quit with unsaved edits saves or asks, as Close Vault does

Issue: #784 (retitled 2026-10-09 under D3). Size S to M. Lanes: swift,
a11y, windows. Depends on nothing; PR 23 follows it (both touch the
termination fence). Parallel with PRs 13, 14, 15, 25.

## Goal

⌘Q with dirty tabs behaves and speaks like Close Vault: save all, or
present the Save All / Discard All / Cancel prompt; never discard
silently. Clean tabs terminate immediately; the sidebar fence still
settles.

## Current state on main

- `SlateMacApp.swift:30-45` `applicationShouldTerminate`: `.terminateNow`
  unless `AppState.hasPendingSidebarWorkAtTermination` (`AppState.swift:
  5123-5127`: writer chains, a structural mutation in flight, outstanding
  Trash owners), in which case `.terminateLater` and
  `settleSidebarWriterChainsForTermination` (`:5136`) then
  `reply(toApplicationShouldTerminate: true)`.
- `AppState.swift:4171-4178`: the `willTerminateNotification` observer
  saves the workspace layout only.
- No autosave exists (`NoteEditorView.swift:274` describes it as a future
  hook); saving is explicit.
- Close Vault: `closeVaultFromUserAction()` (`:10849-10877`) aggregates
  dirtiness over `workspace.dirtyParkedDocuments()`,
  `uncommittedNoteDraftPaths()` and `hasUnsavedChanges`; the single-file
  case goes through `attemptCloseVault()` (`:23100`), the multi-tab case
  sets `pendingVaultClose = dirtyVaultClosePaths().count` (`:10881`) which
  drives the Save All / Discard All / Cancel alert in `MainSplitView`,
  resolved by `resolveVaultCloseSaveAll()` (`:10928`),
  `resolveVaultCloseDiscardAll()` (`:11012`), `resolveVaultCloseCancel()`
  (`:11031`); the outcomes speak VaultClosed / VaultClosedAllSaved /
  VaultClosedChangesDiscarded. `CloseVaultSheetParityTests` pins the sheet.

## Windows reference

How Windows handles the main window closing with dirty tabs (grep
`Closing` in `MainWindow.xaml.cs` and the workspace's close-all prompt);
keep the mac prompt's wording and outcomes consistent with it, and name
the Windows path in the PR.

## Design

1. **Confirm the loss first.** On current `main`: open a vault, type in a
   note, ⌘Q, relaunch, reopen; record in the PR that the edit was lost
   (the 2026-10-09 finding is by code reading).
2. **Termination flow.** `applicationShouldTerminate` returns
   `.terminateLater` when any tab is dirty or a draft is uncommitted (the
   same aggregation Close Vault uses), sets a `pendingTermination` mode
   on the state, and routes through the Close Vault flow: the single-file
   save or the multi-tab prompt. The resolvers, when in termination
   mode, finish with `NSApp.reply(toApplicationShouldTerminate: true)`
   after save-all or discard-all succeeds, and with `false` on Cancel or
   on a save conflict/error (the standard dialog owns recovery; the app
   stays running). After the reply path, the sidebar fence runs as today
   (settle, then reply) so the two waits compose: dirty tabs first, then
   the fence.
3. `.terminateNow` only when nothing is dirty and the fence is clear.
4. The layout save in `willTerminateNotification` is unchanged.

## Changes

- `SlateMacApp.swift:30-45`; `AppState.swift:10849-11050` (termination
  mode in the resolvers); the `MainSplitView` alert if its title or
  buttons should say "Quit" in termination mode (keep the three actions).

## Tests

(`CloseVaultSheetParityTests` plus a new `TerminationFlowTests` driving
the app delegate's decision function and the resolvers in termination
mode.)
- `testQuitWithADirtyTabPresentsThePromptAndNeverDiscardsSilently`.
- `testQuitSaveAllSavesEveryDirtyTabThenReplies`.
- `testQuitDiscardAllRepliesWithoutSaving`.
- `testQuitCancelKeepsTheAppRunning`.
- `testQuitWithCleanTabsTerminatesImmediately`.
- `testTheSidebarFenceStillSettlesAfterTheDirtyTabsResolve`.
- `testASaveConflictDuringQuitKeepsTheAppRunning`.

## Accessibility

The prompt is the existing accessible alert; the outcomes speak the
existing events. No field pass; a manual ⌘Q check with VoiceOver on is
recorded in the PR (the prompt is announced).

## Docs and registers

None. Comment #784 with the runtime confirmation and the new behaviour.

## Acceptance

- [ ] The runtime loss is confirmed and recorded before the fix.
- [ ] The seven facts pass.
- [ ] Lanes green.

## Risks

`.terminateLater` with a SwiftUI alert requires the reply to be sent on
every exit path from the prompt, including a vault close started while
the prompt is up; audit `supersedePendingTabCloseForVaultTransition` and
the vault-switch paths for a route that drops the pending reply.

## Brief

Make ⌘Q safe. Read `SlateMacApp.swift:26-46`, `AppState.swift:
5100-5150`, `:10849-11050`, `:23100`, `CloseVaultSheetParityTests`, and
the Windows window-closing path. First confirm the data loss at runtime
on `main` and record it. Then route termination through the Close Vault
flow: `.terminateLater` while any tab is dirty, the same prompt and
resolvers, a reply of true after save-all or discard-all and false on
cancel or conflict, the sidebar fence afterwards, `.terminateNow` only
when nothing is dirty and the fence is clear. Write the seven facts
first. Done when they pass, the lanes are green, and the PR body holds
the runtime confirmation.
