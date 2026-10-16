# PR 10 — Canonical file identity on mac: re-seat missing tabs (I6), hashless saves create (I8)

Issue: #1147 (#1077 Phase 4, mac half). Size M to L. Lanes: swift, a11y,
windows. Depends on PR 9 (the hashless save uses the reporting create).
PR 19 reuses the re-seat. Parallel with PRs 8 and 11.

## Goal

Close the W5-3 codex round-2 scenario on mac: a parked tab of a deleted
note, the note recreated under another spelling, and the parked tab's
hashless save silently overwriting the new note. Core canonicalizes at
its boundary (I2 to I5); the host half is two rules Windows already
ships.

## Current state on main

- Core exports `canonical_path(path) -> Option<String>` (`lib.rs:1195`):
  the spelling the filesystem stores for an existing vault-relative
  entry, or nil.
- `Workspace/NoteDocument.swift:41` `isMissingFromDisk`; marked at
  `Workspace/WorkspaceState.swift:505` and `:519`; consulted at
  `AppState.swift:9436-9443` (parked tabs).
- `AppState.swift:16323` `publishTreeMutation(_ kind:…)` is the single
  choke point every structural operation routes through on success
  (`.createNote`, `.createFolder`, `.rename`, `.move`, `.delete`, the
  batch kinds); the sidebar reconciles from `appState.treeMutation`
  (`FileTreeSidebar.swift:5688`, `:6265`).
- `AppState.swift:13569` `saveCurrentNote()`; the core write is
  `saveText(path:text:expectedContentHash:)` with a nil hash meaning
  unconditional (core's documented contract, unchanged by this PR).
- Comparators are `==` everywhere (the I6 rule: keep them ordinal).

## Windows reference

- `WorkspaceViewModel.cs:2943-2975` `ReseatMissingTabs`: for each tab
  missing from disk with a path, `CanonicalPath(tab.Path)`; a
  `VaultException` → leave it missing (fail closed, I7); nil → still
  missing; `KeepsBufferOnReseat(tab, stored)` (`:2977-3000`: a tab with an
  admitted save, or a dirty tab, keeps its buffer and document: under
  another spelling it takes the stored path, under its own it stays as
  is); otherwise retarget when the spelling differs ordinally and
  `ReplaceItem` (the same-path reload). Called on Created and Renamed
  publications and by the rescan (`WorkspaceViewModel.Rescan.cs:316`).
- `WorkspaceViewModel.cs:1023-1052` `WriteForSave`: `ExpectedContentHash
  == null` → `CreateExclusiveReporting`: `Committed` → saved with the new
  hash; `PublishedUnindexed` → saved with the caveat; `DestinationExists`
  → the conflict arm; never `SaveText(path, text, null)`.
- `CanonicalIdentityTests.cs`: `AMissingTabReseatsWhenItsFileComesBackUnder
  AnotherSpelling` (`:24`, volume-probed), `ADirtyMissingTabIsRetargetedBut
  NeverReloaded` (`:68`), `AHashlessSaveNeverLandsOnAnExistingFile`
  (`:108`), `TheLifecycleReseatsMissingTabsOnCreatedAndRenamedPublications`
  (`:150`). Mutation-verified: with I8 off and the re-seat a no-op, three
  runtime facts fail.
- Contract 32 I6 (`:183`), I7, I8 (`:205`).

## Design

1. **I6.** `reseatMissingTabs()` on `AppState` (or the workspace model):
   iterate open documents with `isMissingFromDisk` and a path; ask
   `canonicalPath`; a thrown `VaultError` or nil leaves the tab missing;
   a non-nil spelling that differs ordinally retargets the tab (path,
   title, the parked identity); a clean tab then reloads through the
   existing same-path reload so its missing state and content hash stop
   being stale; a dirty tab (or one with a save admitted) retargets only.
   Call it after every `.createNote` and `.rename` (and `.move`, which is
   a rename of a path) publication inside `publishTreeMutation`, and
   expose it for PR 19's rescan.
2. **I8.** In the save path, a document with no content hash goes through
   PR 9's reporting-create helper: `Committed` → the save completes with
   the new hash; `PublishedUnindexed` → completes with the caveat;
   `DestinationExists` → `currentSaveConflict` (the existing conflict
   presentation), never `saveText(…, nil)`. A document with a hash saves
   as today.
3. Comparators stay `==`; identity is corrected once, at publication.

## Changes

- `AppState.swift:16323-16368` (the call), a new
  `AppState+CanonicalIdentity.swift` (the re-seat), `Workspace/
  WorkspaceState.swift` (retarget support if missing), the save path
  around `:13569`.

## Tests

(`CanonicalIdentityTests.swift`, mirroring the C# names.)
- `testAMissingTabReseatsWhenItsFileComesBackUnderAnotherSpelling`:
  volume-probed (create `Ghost.md`, open it, delete it, create
  `ghost.md`; on APFS case-insensitive the tab re-seats to `ghost.md` and
  its next save extends the new note; on a case-sensitive volume the fact
  is vacuous and says so).
- `testADirtyMissingTabIsRetargetedButNeverReloaded`: the buffer is kept
  and its hashless save conflicts instead of overwriting.
- `testAHashlessSaveNeverLandsOnAnExistingFile`: not volume-dependent; a
  free path is still created.
- `testTheLifecycleReseatsMissingTabsOnCreatedAndRenamedPublications`.
- Mutation check recorded in the PR: with I8 off and the re-seat a no-op,
  the three runtime facts fail.

## Accessibility

A conflict is presented through the existing conflict resolver (already
accessible); a landed-unindexed save speaks PR 9's caveat.

## Docs and registers

`docs/plans/32_canonical_file_identity_contracts.md`: the I6 and I8 mac
column marked delivered (and I7's fail-closed note).

## Acceptance

- [ ] Four facts pass; the mutation check is recorded.
- [ ] No `saveText` call with a nil hash remains on the save path.
- [ ] Contract 32 updated; lanes green.

## Risks

The volume-probed fact must detect case sensitivity at runtime
(`pathconf`/create-and-stat probe) rather than assume APFS; the Windows
fact's probe is the model.

## Brief

Implement contract 32's host rules I6 and I8 on mac, mirroring the
Windows shape. Read `lib.rs:1195`, `Workspace/NoteDocument.swift:41`,
`Workspace/WorkspaceState.swift:500-535`, `AppState.swift:16323-16368`
and `:13569`, the Windows `WorkspaceViewModel.cs:2943-3000` and
`:1023-1052`, `CanonicalIdentityTests.cs`, and contract 32 I6 to I8. Add
a re-seat that runs after Created and Renamed publications (fail closed
on error; dirty tabs retarget only; clean tabs reload), and route
hashless saves through PR 9's reporting-create helper with
`DestinationExists` as the conflict. Keep every comparator ordinal.
Write the four twin-named facts first, with the volume probe. Record
the mutation check. Update contract 32. Done when the facts pass and
the lanes are green.
