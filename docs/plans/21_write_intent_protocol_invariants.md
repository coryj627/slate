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

## Model-confirmation review resolutions (2026-08-02)

The single-pass model review surfaced eight classes; five became code fixes
(deletes/trash plant held markers; folder moves rewrite only their FROZEN
pre-rename snapshot; `.` path components are rejected — one canonical string
key per file; clock-anomaly markers (`now < created_ms`) are sweep-eligible;
`ensure_aged_healing_marker` counts only non-superseded or held rows), one
became a durability rule (below), and two are scope clarifications:

- **Durability rule.** Commits that must be durable BEFORE a filesystem
  mutation — save registrations, fence re-registrations, marker plants, the
  inflight journal INSERT — run under `PRAGMA synchronous = FULL`
  (`commit_durably`); everything else keeps WAL NORMAL. Inflight progress
  UPDATEs stay NORMAL deliberately: a lost counter only causes an idempotent
  re-reversal. Terminal aging and journal deletion stay NORMAL: losing them
  reverts markers to HELD/aged states whose convergence paths already cover
  re-entry.
- **Repair converges to SCANNER semantics.** `reindex_path_locked` indexes
  exactly as the scanner does: oversized files index as metadata-only (their
  tasks intentionally do not serve), and non-UTF-8 bytes decode lossily. A
  successful repair is BY DEFINITION convergence to what a scan would produce;
  the `FileTooLarge`/`InvalidUtf8` containment arms are defensive dead code
  for these normal cases and containment is effectively the `Io` class.
- **INV1 scope.** INV1 binds Slate-coordinated actors only. External editors
  mutate without markers by nature; their divergence is the watcher/scanner's
  domain, including the known coarse-mtime/same-size stat-tuple evasion — see
  accepted risk 5.

## Re-confirmation review resolutions (2026-08-02, second pass)

Three classes became code fixes: provider-invalid spellings (Windows reserved
names) are typed as provable no-mutation at the write and containable at the
sweep, so one bad API input can no longer poison queries; folder moves
classify every un-frozen prefix row by DISK TRUTH under the writer lock
(source-exists stays, destination-exists is rewritten and double-marked,
neither is marked for NotFound convergence), covering pre-rename entrants;
deletes keep rows and held markers for paths recreated between the filesystem
delete and the index transaction. Two classes are scope preconditions:

- **Canonical file identity (case/Unicode)** is a platform-wide property this
  protocol consumes but does not define. The raw-segment gate guarantees one
  key per exact spelling; case-insensitive and Unicode-normalizing volumes
  can still alias two spellings to one physical file, affecting links, rows,
  and search alike. Tracked as its own work item; the protocol adopts
  whatever canonical identity the platform layer establishes. **Satisfied
  by #1077** (`docs/plans/32_canonical_file_identity_contracts.md`): every
  mutation target binds to the filesystem's stored spelling, and the
  markers, rows, and epochs key on it. *Corrected by #1975 (2026-11-16):*
  that held for a save's and a delete's marker, not for a move's. A move
  plants under both spellings before the rename, so after a case-only
  rename one of its two markers was keyed on a spelling the volume does
  not store. The sweep's bind step, below, settles every marker under the
  stored spelling.
- **Same-protocol-version precondition.** All coordinated actors sharing a
  cache must run this protocol version. Sessions older than migration 035/036
  must not stay live across an upgrade; enforcement (open-time fencing of
  stale sessions) is an upgrade-machinery item, moot until W4-3 ships.
  **Enforced by #1078** (`docs/plans/33_upgrade_fence_contracts.md`): every
  cache-writer transaction begins through `db::begin_fenced`, which refuses
  — under the writer lock, before any mutation — unless the cache is at the
  schema version this build was opened at; the fence ships in the first
  release so every later schema bump inherits it.

## Final-confirmation review resolutions (2026-08-03, third pass)

Six classes, all resolved in code plus one register entry:

- **Membership fence** (`fence_prefix_and_mutate`): folder moves and folder
  deletes enumerate-and-mark the prefix under the SQLite writer lock — which
  blocks every coordinated save, since saves write inside their own writer
  transaction — and mutate the filesystem only while the verification holds.
  Late entrants are either marked before the bytes move or blocked until they
  are; persistent churn aborts before any mutation.
- **Deletes probe inside the writer lock, kind-aware**: the filesystem delete
  and the disk-truth probe run inside the index transaction; only PROVEN
  absence (Io NotFound) deletes rows and clears markers — live paths and
  unknown probe outcomes keep both.
- **Four-state stray classification**: source/destination probes distinguish
  Live / ProvenAbsent / Unknown; both-present keeps the SOURCE incarnation's
  row and marks both paths; any Unknown mutates nothing and marks both.
  Directory rows apply the same source-Live rule so file and directory trees
  cannot split.
- **Per-path deferral**: a path whose HELD marker's structural claim fails
  defers every token — aged save markers included — and the host repair route
  returns Declined, so no repair can commit a mid-move snapshot.
- **Durable publication**: `atomic_write` syncs the destination parent
  directory after the rename on POSIX, so the marker-clearing index commit can
  never out-live the publication it vouches for. Windows relies on NTFS
  metadata journaling (register entry 12).

## The bind step (#1975, 2026-11-16)

A move marks both sides of its rename before the kernel call (INV1). After
a case-only rename (`A.md` to `a.md`), whether it landed, was refused or was
cut short, one of the two markers names an **alias**: a spelling the volume
resolves to a file it stores under another. The sweep repaired that marker
by its own spelling. The read answered through the alias, no row stood under
that exact path, and the repair inserted a second `files` row for one file
(contract 32 I3). Planting cannot avoid it: neither spelling is known to be
the stored one before the rename, and a crash can fall on either side of it.

The sweep now **binds** each selected marker before it repairs
(`bind_marker_to_stored_spelling_locked`). It asks the provider for the
stored spelling of the marker's path:

- **The marker's own spelling, or nothing there.** The sweep repairs the
  path as before. A path that names nothing still converges to deletion.
- **The lookup fails.** The error is that marker's repair error. Nothing is
  read through the spelling that did not resolve. A file condition is
  contained under the marker's own key and the marker stays (contract 32
  I7); anything else fails the query, as a repair's failure does.
- **Another spelling.** In one writer transaction a `files` row under the
  alias is removed, as the not-found arm removes one, and the selected
  tokens move from the alias key to the stored key. The repair that follows
  reads the stored spelling once and clears the tokens the sweep selected:
  the moved ones, and the stored key's own selected ones. A token the sweep
  did not select stands. A live writer's fresh token under the stored
  spelling is one, and it outlives the bind (round 22).
- **A move owns either spelling** (a HELD marker stands under the alias or
  under the stored spelling, and the structural lock cannot be claimed).
  Nothing is done for the alias marker in this sweep. The per-path deferral
  follows both spellings.
- **The answer moved.** The first lookup runs before the writer
  transaction, so another process's case-only rename can land in between,
  and the spelling taken for the alias is then the stored one, with the
  index row under it. The sweep asks again inside the transaction, after
  the held check. If the answer is no longer the same stored spelling,
  nothing changes in this sweep: the row, the tokens and their stamps stay,
  nothing is sent, and the next sweep binds the marker afresh. A coordinated
  mover has by then either committed (the second lookup sees its result) or
  still has held markers standing (the bind waited). A second lookup that
  fails is the marker's repair error, as the first one's is.

Checked against the invariants:

- **INV1.** Holds. The bind step plants nothing and mutates no file. It can
  leave the stored file with no row for a moment: where the only row stood
  under the alias, the row is removed and the stored spelling gets its row
  from the repair that follows. That gap is marked: the moved tokens commit
  under the stored key in the same transaction that removes the row, so no
  divergence is ever unmarked.
- **INV2.** The bind step clears nothing. A moved token is cleared where
  every token is: by the repair's commit after a full read under the writer
  lock, or by supersession if it is not held. A moved token takes the
  stored row's current epoch as its `registered_epoch` (0 with no row),
  stamped under the writer lock after the divergence it guards, so it is
  superseded only by a full-file index commit that lands after the move. A
  held token stays held and is never superseded.
- **INV3.** A moved token keeps its `created_ms`: an aged one is repaired in
  the same sweep, a held one under the same structural claim. A marker whose
  lookup fails stays and is tried again by every later sweep. A marker that
  waits (a move owns a spelling, or the answer moved) keeps its
  `created_ms` too, so the next sweep selects it again.
- **INV4.** Containment for a bound marker is keyed by the stored spelling,
  where the rows that serve are. Before, a marker under an alias contained
  nothing, because no row stood under its key.
- **Token discipline (round 22).** Only the selected tokens move. Where the
  stored key already holds the same token, the two rows merge into the
  stronger of each column (the later `created_ms`, so held wins; the higher
  epoch): the move cannot fail on the table's key and cannot weaken a
  marker.
- **Durability rule.** The bind commit precedes no filesystem mutation and
  stays NORMAL. Losing it puts the tokens back under the alias key, where
  the next sweep binds them again.
- **Scanner semantics.** Removing a row under a spelling the volume does not
  store is what a scan's prune does. The removal is not announced by an
  event: a host is told only that the stored note was read again
  (`Modified`). It is therefore recorded in the change feed as a scan's
  removal is, not as an own write.
- **Cost.** One stored-spelling lookup for each SELECTED path (a path's
  tokens are one candidate), and one more, inside the transaction, for a
  path that is an alias. A query with no marker, a fresh marker and a scan
  pay nothing.

The two host repair routes, `reindex_path` and `repair_or_contain_path`,
bind their path at entry as a save does, for the same reason.

Found on the way and fixed with it: the directory rule of the
final-confirmation review ("Directory rows apply the same source-Live
rule") asked whether a folder's old path was still there, and after a
case-only rename of its parent the old path answers as an alias. The rule
now keeps a directory row only when its path is stored under its own
spelling.

Facts: `session/tests/canonical_identity.rs`, the section headed #1975.

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
5. External-edit staleness: an external same-size, coarse-mtime rewrite can
   evade the scanner's stat fast path with no marker. Watcher events cover the
   ordinary case; the pathological tuple-collision case is a filesystem
   limitation accepted vault-wide (see round-13's stat-poison for the repair
   path, which never fast-paths).
6. Quarantine DB failures are reported as the original file error (the
   database root cause is logged context, not the surfaced error); fail-closed
   behavior is what matters and is preserved.
7. Host containment's epoch-equality fence can theoretically ABA across a
   move-out/move-in with equal zero epochs; it requires several independent
   events and the planted healing marker converges the transient containment.
8. Fresh and actively-HELD markers do not gate task reads: queries may serve
   the committed pre-operation snapshot until the operation commits or the
   marker becomes sweep-eligible. This transient, bounded, marked staleness is
   intentional — the alternative is blocking reads on every in-flight write.
9. Batch-trash marker release is best-effort after proven outcomes; an
   uncleared marker costs one orphan-sweep read, never correctness. Its
   late-descendant and same-kind-recreation schedules leave residues covered
   by kept markers plus orphan sweeps or the requires_rescan invalidation.
10. A 64-bit token collision combined with one writer's post-write failure and
    the colliding writer's pre-write cleanup could erase a shared marker;
    below the bar by probability.
11. Delete-vs-recreate residue in the crash arm remains the honest-absent /
    marker-heals class; the live arm now keeps recreated rows by disk truth.
12. Windows directory-entry durability after rename/delete relies on NTFS
    metadata journaling ordering rather than an explicit directory sync (std
    exposes none); POSIX parents are synced explicitly.
13. `VaultProvider::write_file` returning `InvalidPath` is treated as
    pre-mutation by contract; the shipping filesystem provider satisfies this,
    and any future provider must preserve it or the marker-clearing rule must
    be revisited. Since #1452 the filesystem provider on macOS and Linux also
    refuses with `InvalidPath` when a folder on the path is a symbolic link or
    not a folder; its pinned walk refuses before anything is staged, so the
    rule holds (`write_ancestor_links.rs` pins the cleared marker).

## Rules for future changes

- New fallible steps between a save's registration and `provider.write_file`
  must clear the token on their error path (round 27's invariant comment).
- New `files.path` mutation sites must plant both-sides held markers first and
  ensure-and-age them in their index commit. New DELETION sites plant held
  markers before the filesystem delete and clear them token-scoped with the
  row deletion.
- Folder-scale index rewrites operate on the FROZEN pre-mutation snapshot the
  markers were planted for — never a fresh prefix enumeration.
- Pre-filesystem-mutation durable commits go through `commit_durably`.
- New task-read entry points must sweep first, bind the returned orphan guard
  across the read, and read from one snapshot.
- A repair reads a path under the spelling the volume stores (#1975). A new
  caller of `reindex_path_locked` binds its path first; a marker is bound by
  the sweep. A NEW disk-truth probe that must tell "this path is still its
  own entry" from "this path answers as an alias" asks for the stored
  spelling, not for existence. One existing probe is a named exception: the
  stray-file classification in `rename_prefix_in_index` still asks for
  existence. After a case-only folder rename it sees a stray's two spellings
  as both present, keeps the row under the old one and marks both sides, and
  the next sweep's bind step removes that row and reads the stored spelling.
  It converges one sweep later and is left as it is.
- Epoch stamps may only be written by code that read the file's bytes inside
  the same transaction (or zeroed by moves). Never stamp as bookkeeping.
- Test seams follow the `SLATE_TEST_*` env pattern, path-substring-triggered,
  serialized under `ENV_FAULT_GUARD`/`EnvFaultLock`; regressions must be
  bite-verified (temporarily disable the fix and watch the assert fail).
