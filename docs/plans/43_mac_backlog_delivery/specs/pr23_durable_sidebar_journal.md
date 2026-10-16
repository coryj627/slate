# PR 23 — A durable write journal for retained sidebar writes

Issue: #944 (FL-06 rounds 31 and 32). Size M to L. Lanes: swift, a11y,
windows. Depends on PR 15b (both change the termination fence). Parallel
with PRs 18, 19, 24.

## Goal

A sidebar organization write that was acknowledged ("Pinned") but failed
survives a quit or a crash: it is journaled as a typed intent before
acknowledgement and before the filesystem operation commits, replayed on
the next same-vault open through the existing chained drain, never
evicted, and never lost with the process.

## Current state on main

- `AppState.swift:5100-5110`: the in-memory registry
  `sidebarUnflushedWriteRetries: [SidebarVaultRootIdentity:
  [SidebarUnflushedWrite]]`, where an entry is a closure (`apply`) plus a
  `SidebarStructuralTransform` backlog; cap 64 (`:5109`), capacity gates
  new acknowledgements up front and parking never evicts.
- `:5116` the static mirror `structuralMutationInFlightForTermination`;
  `:5123` `hasPendingSidebarWorkAtTermination`; `:5136`
  `settleSidebarWriterChainsForTermination` (5 s bound for sidebar
  persistence; Trash drains without a deadline). PR 15b adds the
  dirty-tabs wait in front of this fence.
- The retained write dies with the process; the next launch starts empty
  (the issue's finding), and the termination fence cannot recover writes
  that already failed.

## Windows reference

Windows' sidebar settings store (`SidebarSettingsStore.cs`) and its
read-only reason; check whether it journals failed writes (likely not)
and, if not, note the mac-first design here so a Windows twin can follow
(file an issue; rule 8).

## Design

1. **Typed intents.** An `enum SidebarWriteIntent: Codable` covering the
   eleven organization commands and `SidebarStructuralTransform` (op plus
   arguments), replayed through the same exact-op appliers the closures
   call today. The registry holds intents, not closures.
2. **Journal.** An identity-keyed write-ahead journal under Application
   Support (never inside the vault, whose filesystem is what failed):
   one append-only file per vault identity (device plus inode; a replaced
   vault never matches), fsync'd before the action is acknowledged and,
   for structural renames and moves, before the filesystem operation can
   commit (round 32 (1)); pruned only after commit (the in-memory
   registry's commit-gated shrink).
3. **Overflow.** Non-lossy: the journal adopts the registry's contract
   (refuse new acknowledgements at capacity with the typed dispatch
   failure; never evict) (round 32 (2)).
4. **Replay.** On vault open, merge the durable journal for that identity
   with the in-memory registry and drain through the existing chained
   drain (run-time snapshot, per-entry final-refusal settlement); cap and
   age out entries, surfacing an aged-out intent rather than dropping it
   silently.
5. **Fence.** The termination fence keeps its bound; the journal makes
   the bound safe rather than lengthening it.

## Changes

- `AppState.swift:5090-5300` (the registry and drain), a new
  `Sidebar/SidebarWriteJournal.swift`, `Sidebar/SidebarOrganization.swift`
  (intent encoding).

## Tests

- `testAParkedIntentSurvivesARelaunchAndReplays` (park an intent, destroy
  `AppState`, reset the statics through a seam, rebuild, reopen, verify
  the replay).
- `testTheJournalIsWrittenBeforeAcknowledgementAndBeforeAStructuralCommit`.
- `testCapacityRefusesNewAcknowledgementsAndEvictsNothing`.
- `testAReplacedVaultNeverMatchesItsJournal`.
- `testAnAgedOutIntentIsSurfacedNotDropped`.
- `testTheTerminationFenceStillSettlesWithinItsBound`.

## Accessibility

Replay outcomes speak through the existing sidebar announcements; an
aged-out or refused intent is surfaced through the existing typed
dispatch failure (no new copy without a residue marker).

## Docs and registers

None; the FL-06 identity notes under `docs/plans/15_files_sidebar/` gain
a pointer to the journal. Comment #944.

## Acceptance

- [ ] Intents are typed and journaled; the relaunch regression passes.
- [ ] Overflow is non-lossy; the fence bound holds.
- [ ] Lanes green.

## Risks

Journaling before a structural commit adds an fsync to every rename and
move; measure with the structural facts' timing and keep it off the main
actor.

## Brief

Make retained sidebar writes durable across launches. Read
`AppState.swift:5090-5300` (the registry, the cap, the fence and the
drain), `Sidebar/SidebarOrganization.swift`, the FL-06 round 29 to 32
notes, and PR 15b's spec. Replace the closure registry with typed,
Codable intents; add an identity-keyed write-ahead journal under
Application Support written before acknowledgement and before a
structural commit, pruned after commit, non-lossy at capacity; replay
on open through the existing drain. Write the six facts first, including
the relaunch regression with a statics-reset seam. Done when they pass
and the lanes are green.
