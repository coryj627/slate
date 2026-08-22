# The mutation goldens (W5-4, #744)

These artifacts are the platform-independent oracle for structural
file-management mutations — the write-side extension of the §W-A
read-side harness one directory over (`parity_golden/`). The same
two-oracle mechanism: shared committed goldens asserted independently
per platform (`MutationHarnessCensus` on Windows,
`MutationHarnessTests.swift` on mac), so both lanes green proves
cross-platform byte-identity transitively. Windows is the regen
authority (mac Swift is not locally compilable here; the read harness
records the same rule):

```
dotnet run --project apps/slate-windows/tools/ParityHarness -- \
  --mutations \
  --scenarios crates/slate-core/tests/fixtures/mutation_golden/scenarios.json \
  --out crates/slate-core/tests/fixtures/mutation_golden
```

`scenarios.json` is the scenario SCRIPT SET, not an artifact: both
platform drivers execute it verbatim (H6 — identical sequences by
construction). Everything else here is generated output; hand-edits
fail the censuses.

## The reachability split (H4)

Host-side, both platforms (this directory):

- **S1 occupied destination** — typed `DestinationExists` on the disk
  and case-insensitive-index arms; nothing written.
- **S2 retry-after-conflict** — refusal → vacate → identical retry;
  terminal tree byte-identical to the never-conflicted run
  (invariant 6, driver-enforced across the two artifacts).
- **S3 rewrite interruption** — `SLATE_TEST_FAULT_AFTER_WRITE` lands
  the move and records the faulted rewrite honestly (the file appears
  in `rewritten` — bytes landed — AND `failed` with the typed kind:
  disk newer than index, core's real partial-failure boundary);
  `_PRE_WRITE` leaves the faulted file byte-unchanged; a reopen
  reconciles.
- **S4 undo round-trips** — byte-exact (hash-verified checkpoint
  pairs) for single rename, single move, the compound folder-note
  rename, and batch move via `UndoBatchMove`; each leg is UNDONE to
  its pre-op tree AND REDONE to its post-op tree (redo = the forward
  FFI re-run — the host model — with `UndoBatchMove` for the batch
  redo's reversal); plus the typed non-latest and wrong-endpoint
  rejections.
- **S5 pre-mutation abort** — `SLATE_TEST_FAULT_PLANT_MARKERS` aborts
  a move, a batch move, and a delete before ANY fs mutation (the
  delete never reaches the Recycle Bin); the vault is byte-identical
  to pre-op, and the retry succeeds after a reopen (the abort's
  held-marker discipline requires a reconcile before the same path
  mutates again — recorded, not hidden).

Core-side residue (the `pub(crate)` seam boundary is the reason —
FR-2 records the future FFI cancellation seam that would upgrade S5):

- Mid-batch partial interruption, rollback, `RollbackIncomplete`, and
  reopen recovery of a torn batch: `structural_batch.rs` (71 tests).
- The `_RECOVERY_FINALIZE` reopen-recovery arm: exercised core-side
  where the journal tables are inspectable; the host-side S3 reopen
  covers the completed-recovery path.

## Findings the scenarios pinned

- **FL6-1's `UndoOpIds` sequence is not host-executable as
  documented.** `undo_structural` admits only the LATEST journal row,
  and every undo journals itself — so the second id of a compound
  (folder+note rename) sequence, and any multi-step LIFO walk over
  recorded ids, always fails the gate. Hosts (mac #871, Windows F10)
  reverse compounds by re-running the forward FFI with inverse
  arguments; S4 scripts exactly that. Resolved by #1127: the FL6-1
  contract text (`StructuralReport::undo_op_ids`, core and FFI) now
  records the field as the journal record and the forward-inverse
  model as the host consumption, pinned by
  `compound_undo_ids_are_the_journal_record_not_a_host_walk`.
- A structural-op abort under `PLANT_MARKERS` leaves held write-intent
  markers that refuse further structural ops on the same paths until a
  reconcile (S5's reopen-before-retry).

## Normalization (H2)

`tree` excludes `.slate/` (cache bytes and oplog stems are
platform/run-variant); hashes are lowercase-hex SHA-256 of exact bytes
(line endings never normalized — decision 9). `oplogs` drop
`TimestampMs`, hash payloads, never name `.oplog` stems, and pin
`UserActorId` to the FFI constant. Reports reduce error strings to
kinds/stages, record `OpId`s sequence-relative (first-appearance
interning), and drop `SaveReport.NewMtimeMs`. `links` is the read
harness's artifact re-emitted over the terminal vault (invariant 4).
