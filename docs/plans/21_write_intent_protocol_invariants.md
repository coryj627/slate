# 21 — Write-Intent / Move-Marker Protocol: Invariant Model (W4-3)

Status: locked by the W4-3 adversarial review arc (PR #1076, rounds 12–41 plus
the post-round-41 design audit). Any change to `save_text`, structural moves,
the task-query sweep, or open-time recovery must be checked against this model
before shipping — the protocol was built one counterexample at a time, and this
document exists so that never happens again.

## Why this protocol exists

`save_text` writes the FILE before committing its index transaction, and the
SQLite cache is shared across processes (app + CLI). A post-write failure
therefore leaves disk newer than the index with no committed state another
process could observe. Structural moves have the mirror problem: the filesystem
rename and the `files.path` rewrite are separate durable events with no shared
transaction, and neither is ordered against another process's in-flight save.

## Durable state

- `text_write_intents (path, token PK, created_ms, registered_epoch)` — one row
  per in-flight writer or move-touched path ("marker"). Tokens are per-writer
  (round 22); every clear is token-scoped. There are exactly two kinds:
  - **Save markers**: `created_ms` = wall clock at registration; re-stamped by
    the transaction-boundary fence (rounds 26/32) so the stamp provably equals
    the file's epoch AT the write, under the writer lock.
  - **Move markers**: `created_ms` = `HELD_MOVE_MARKER_MS` (`i64::MAX`) at
    plant time (round 40); the move's terminal state re-stamps them aged.
- `files.index_epoch` — stamped from the one-row, database-wide monotonic
  `index_epoch_clock` inside every SUCCESSFUL full-file index commit
  (`index_saved_file`, both `index_file` arms; rounds 31–32). Structural moves
  ZERO it ("unknown incarnation", round 34). The fast-path scan skip does not
  stamp: a tuple match reads nothing.
- `structural_batch_inflight` — the batch move journal; consumed only by the
  atomic recovery finalization (round 40).
- The vault structural lock (sidecar file lock + in-process registry) — held by
  every move flow and open-time recovery across its entire lifetime, acquired
  BEFORE marker planting.

## The invariants

- **INV1 (no unmarked divergence).** Any actor that may make index ≠ disk for a
  path commits a durable marker for that path BEFORE the divergence-creating
  action: saves register before `write_file` (round 21); moves plant on BOTH
  sides of every rename before any filesystem mutation (rounds 35–38), holding
  the structural lock. Provable no-writes clear their own token (round 27).
- **INV2 (read-derived resolution).** A marker may only be cleared by: (a) its
  owner, token-scoped, atomically with the commit that removes the divergence;
  (b) a repair that performed a full read of disk under the writer lock; or
  (c) epoch supersession — permitted ONLY for markers whose stamp was taken
  under the writer lock at divergence-creation time, i.e. save markers after
  the round-32 fence. HELD move markers are NEVER superseded (design audit):
  their stamp is plant-time, their divergence is later. Moves never vouch for
  content they did not read (round 34 — epoch zero, not restamp).
- **INV3 (guaranteed convergence).** Every marker eventually becomes
  sweep-eligible — by aging (saves, move terminal re-stamp) or by orphan
  detection (held markers when the structural lock is provably free, claimed
  and HELD by the sweep through repair and read, rounds 40–41) — and the sweep
  then converges the path by reading: repair to disk truth, NotFound to
  deletion convergence (round 23), or file-condition containment.
- **INV4 (honest containment).** While a live, non-superseded marker stands and
  its repair fails on a FILE condition (Io / non-UTF-8 / oversize, round 29),
  the path serves honest-empty — never unverified rows — with the marker kept;
  database failures propagate loudly (round 29). Containment fences: token
  survival + epoch (rounds 29–32) for sweeps, epoch equality for the host
  route (round 31), and every Contained outcome leaves an aged healing marker
  (round 32).

## Cross-actor schedule notes (the ones that were bugs)

Saves × sweeps: fence re-registers swept or stale-stamped rows at the
transaction boundary and holds the lock through the write (26, 32).
Saves × saves: per-writer tokens; conflict refusals clean up (25, 27, 23).
Sweeps × quarantine: token fence + durable-epoch supersession + file-only
gating + healing markers (28–32). Moves × everything: both-sides held markers
before any rename; terminal ensure-and-age restores consumed markers (35–39);
orphan sweeps claim and hold the structural lock (40–41). Recovery: per-file
markers from `inflight.moved` planted all-or-nothing before the first
byte-mutating step, fail-closed with the journal retained; finalization
(re-assert + journal delete) is one transaction whose failure defers (37–40).
Reads: `note_tasks_bounded` and `tasks_for_file` read one WAL snapshot
(33, audit) — rowids recycle, so id→rows must not tear.

## Accepted-risk register (documented, not defects)

1. `create_exclusive_binding` (new-file publish) can crash post-write with no
   marker: the residue is a file with NO index row — honest-absent until the
   watcher/scan discovers it, the same posture as any externally created file.
   Nothing stale serves.
2. Markers gate TASK surfaces only. FTS, links, and graph data for a contained
   or mid-move path can serve stale until the next scan re-reads it. Widening
   the protocol to those surfaces is future work, not a W4-3 defect.
3. Compensation re-assertion after a failed move transaction is best-effort:
   if it also fails, the planted rows remain HELD and the released structural
   lock makes them orphan-sweepable — covered by INV3, at the cost of one
   extra read.
4. A crashed recovery whose reversals ran but whose finalization deferred
   re-enters idempotently: physical/index truth detection tolerates the
   already-reversed state, and rewrite restoration is hash-guarded CAS.

## Rules for future changes

- New fallible steps between a save's registration and `provider.write_file`
  must clear the token on their error path (round 27's invariant comment).
- New `files.path` mutation sites must plant both-sides held markers first and
  ensure-and-age them in their index commit.
- New task-read entry points must sweep first, bind the returned orphan guard
  across the read, and read from one snapshot.
- Epoch stamps may only be written by code that read the file's bytes inside
  the same transaction (or zeroed by moves). Never stamp as bookkeeping.
- Test seams follow the `SLATE_TEST_*` env pattern, path-substring-triggered,
  serialized under `ENV_FAULT_GUARD`/`EnvFaultLock`; regressions must be
  bite-verified (temporarily disable the fix and watch the assert fail).
