# W5-4 — File management commands + the mutation harness (#744): contracts

Scope (spec §W5-4): the eight `slate.file.*` matrix rows, structural
undo, `StructuralReport` consumption, and the differential mutation
harness that is this issue's ship gate. Written BEFORE implementation
per `24_red_team_protocol.md` §0. The divergence (FD) and
accepted-risk (FR) registers are owner-recorded and **off-limits for
review re-litigation** — a reviewer may report that an entry is
factually wrong, not that the trade-off should be re-made.

Contract numbering is per-wave. "F3" here is unrelated to any other
document's contract 3.

## The findings that shape this issue

**1. "Bulk rename" is not in this issue.** The spec's own ownership
correction (w5_spec.md:34) routes the mac bulk-rename sheet to W4-4's
property bulk-rename, which shipped; **no file batch-rename primitive
exists or is claimed**. The title's phrase survives only as harness
scenario vocabulary.

**2. The issue's `StructuralReport` precedent does not exist.** No
Windows call site consumes `StructuralReport` — the W4-7 citation is
wrong. Tab retargeting after renames works today via the core
file-change EVENT stream (`HandleFileChange` → `RetargetPath`), not
the report; every single-entry mutation's `Rewritten`/`Failed`/
`UndoOpIds` are dropped on the floor, and the single-entry
`MoveFile`/`MoveFolder` FFIs are entirely unused. W5-4 adds the
report consumption; the event route stays (F9).

**3. Five of the eight "pending" rows already have working
implementations** registered under their exact ids (newNote,
newFolder, rename, moveTo→batch, delete→batch). The pending status is
literally true only for `duplicate`, `copyPath`, `revealInFinder`.
The real deliverable per existing verb is PARITY (mac flows, copy,
report consumption), not existence — ranked in the phase plan below.

**4. The Windows folder-rename probe is the raced probe mac removed.**
Windows branches on a host-side `HasFolderNote` check; mac routes
EVERY folder rename through the compound `renameFolderWithNote` FFI
because "presence is decided in CORE at operation time under the
structural lock (a Swift-side probe raced external creates)". W5-4
fixes the Windows branch to mac's rule (F3).

**5. Cancellation is not host-drivable.** No mutation FFI takes a
`CancelToken`; `RewriteFailureKind::Cancelled` is a reserved shape no
core site constructs. The spec's "cancellation" scenario cannot be
driven from a host today. It is REDEFINED for this issue (FD-6) as
the pre-mutation abort the marker-plant fault seam provides (nothing
mutated — the observable a user's cancel would produce), and true
mid-operation cancellation is recorded against future FFI.

**6. Interruption reachability splits the harness.** Host-reachable
(both platforms, across the FFI, via the env-var fault seams compiled
into release core): pre-mutation abort (`SLATE_TEST_FAULT_PLANT_MARKERS`
— runs before any fs mutation in moves, batch moves, and deletes),
mid-rewrite save faults (`SLATE_TEST_FAULT_PRE_WRITE`/`_AFTER_WRITE`
keyed to a rewriting file's path — link rewrites run through
`save_text_locked`, so a rename-with-rewrite interrupts
deterministically, producing `Failed[]` and the disk-newer-than-index
state), and reopen-recovery finalization. NOT host-reachable:
mid-BATCH partial interruption and provider-fault rollback — the
batch fault hooks and `FaultInjectingProvider` are `pub(crate)`, and
the FFI constructor is `OpenFilesystem` only. Those scenarios run
core-side (the 71-test `structural_batch.rs` suite is the existing
oracle; H4 records the split).

## Contracts — the verbs

**F1 — `slate.file.newNote`.** Auto-named untitled create, mac's
flow: name from the unique-untitled sequence (`Untitled.md`,
`Untitled 2.md`, `Untitled 3.md`, …), write via **`CreateExclusive`
only** (typed `DestinationExists`; never a pre-check), destination =
the sidebar creation-parent rule (shared with W5-3's T12). On
success: targeted tree update, open in the current tab, then the
selection **hands off to inline rename** with the stem selected (the
F2 flow re-armed programmatically). Announce **"Created note
{leaf}."**; failure **"Could not create note {name}: {reason}"**
(residue channel, F11). The shared `MutationName` text-box flow it
replaces goes away for this verb; CanExecute is selection-independent.
Chord: none — deliberately unbound (FD-1, the recorded TD-6-family
asymmetry).

**F2 — `slate.file.newFolder`.** Same shape: auto-name
`Untitled Folder` (unique-suffixed), **`CreateFolder`** (returns
`StructuralReport` — consumed per F9), inline-rename hand-off,
announce **"Created folder {leaf}."** / failure **"Could not create
folder {name}: {reason}"**. Chordless on both platforms.

**F3 — `slate.file.rename`.** F2 stays the chord (recorded
divergence). The mechanics stay Windows' pseudo-inline flow (the
expander field with the stem selected; Enter commits, Esc restores) —
FD-2 records the presentation divergence from mac's true in-row
field. Changes:
- Folder renames go through **`RenameFolderWithNote`
  unconditionally** — core decides folder-note presence under the
  structural lock (finding 4). File renames stay `RenameFile`.
- The returned report is CONSUMED (F9): retarget from `Moved`,
  announce **"Renamed {old} to {new}."** with the suffix
  **", updated links in N notes."** when `Rewritten` is non-empty
  (N = distinct rewritten paths), surface `Failed[]` per F9's
  refusal presentation, push the inverse onto the undo stack (F10).
- A rename failure **keeps the field open** with the reason inline
  (core's message relayed; the current flow's silent field-reset is
  the defect this fixes).

**F4 — `slate.file.moveTo`.** The Move-To picker replaces the raw
destination text box — the keyboard-first, drag-free move path:
- An in-window Fluent sheet (the W5-3 sheet idiom: `ModalSurface`
  member, admission via the established `Decide*` shape, focus
  capture/restore, Esc cancels): a filter-as-you-type list of every
  vault folder (paged `ListDirChildrenPage` walk, 50k cap), plus a
  pinned **"Vault root"** row and a **"New Folder…"** row (creates
  then moves — one user gesture, two core ops); **illegal
  destinations filtered before the pick** (the item's current
  parent; a folder's own subtree).
- One selected item → **`MoveFile`/`MoveFolder`** (the unused
  single-entry FFIs) with report consumption per F9; announce
  **"Moved {name} to {destLeaf}."** (destination `""` speaks
  "vault root") + the links suffix; inverse move pushed (F10).
- Multiple batch-checked items → `BatchMove` (as today), ONE summary
  **"Moved N items to {dest}."**; the batch report's `OpId` is the
  `UndoBatchMove` handle (F10). The shipped `Standing`-driven path
  transforms and summaries stay.
- The command targets the tree SELECTION when no batch checks are
  active (today it is dead without both checks and a typed
  destination — that CanExecute defect retires with the text box).
- Chord: mac ⇧⌘M stays `UnboundOnWindows` (FD-1).

**F5 — `slate.file.duplicate`** (new verb). mac's composition, no new
FFI: `ReadText(source)` + a `CreateExclusive` loop advancing on
`DestinationExists` (bounded, 200 candidates), with mac's
Finder-parity namer verbatim: `a.md → a copy.md → a copy 2.md`; an
existing ` copy`/` copy N` stem re-uses its base (`a copy.md → a
copy 2.md`, never `a copy copy.md`). Files only — a folder/multi
selection announces the canonical **`DuplicateFilesOnly`** ("Duplicate
applies to files only.") — the event exists in the vocabulary and is
consumed here for the first time on Windows. Success announces
**"Duplicated {source} as {candidate}."**; duplicate is a structural
history BARRIER (clears the undo stacks — mac's rule for creates).

**F6 — `slate.file.delete`.** mac's #860/#852 semantics replace
always-confirm:
- Files and EMPTY folders trash immediately — **no confirmation**
  (Finder parity). A **non-empty folder** (or a batch containing
  one) stages a styled confirmation; the child-count probe re-runs at
  stage time so a stale zero count cannot bypass it.
- Confirmation copy, mac-verbatim with the established Recycle Bin
  adaptation (FD-3): title **"Move “{name}” to the Recycle Bin?"** /
  batch **"Move {N} items to the Recycle Bin?"**; message **"Move
  “{name}” and its {N} items to the Recycle Bin. Slate can't undo
  this action."** / batch **"Move {N} items, including {M} folders
  with contents, to the Recycle Bin. Slate can't undo this
  action."**; buttons **"Move to the Recycle Bin"** (destructive) /
  **"Cancel"**. **Bare Enter never confirms**; Esc cancels; focus
  returns to the tree. The dialog rides an injectable seam with
  pinned labels (the `HistoryConfirmationDialog` precedent) — the
  bare `Func<string,bool>` `_confirmDestructive` seam is superseded
  for this verb.
- Targeting unified: `slate.file.delete` acts on the TREE SELECTION
  (mac parity — one-or-more items); the batch-checkbox flow keeps
  `slate.sidebar.trashSelected` semantics unchanged. Chord: the
  **Delete key, tree-scoped** (FD-4 — mac's ⌘⌫ maps to the Windows
  deletion convention; tree-scoped exactly as mac's is).
- FFI: selection of one file/folder → `DeleteFile`/`DeleteFolder`;
  multi → `BatchTrash`. Trash is **not undoable** and is a
  structural-history barrier (stacks cleared, mac's rule); open tabs
  at deleted paths follow the shipped event-stream invalidation.
- Announce **"Moved {name} to the Recycle Bin."** / **"Moved {N}
  items to the Recycle Bin."**; the shipped batch summary family
  (partial/unknown/rescan arms) stays.

**F7 — `slate.file.copyPath`** (new verb, nearly free). Copies the
**vault-relative path** (mac's semantics — the tree path string)
through the `_copyText` seam; announces the canonical
**`SelectionCopied`** ("Copied.") — the CopyWikilink pattern.
Exactly-one-item capability.

**F8 — `slate.file.revealInFinder`** (new verb). Windows surface:
`explorer.exe /select,` on the vault-resolved absolute path.
**Label: "Reveal in File Explorer"** — FD-5 records the deliberate
label divergence (the P3 byte-identical-label census gets the
divergence disposition the `ChordTableEntry` row supports). No
announcement (the OS surface change is the feedback), no chord, no
undo. Exactly-one-item capability.

## Contracts — cross-verb machinery

**F9 — `StructuralReport` consumption.** Every single-entry mutation
consumes its report at the call site:
- `Moved[]` → immediate `RetargetPath` per pair. The event-stream
  retarget stays wired and MUST remain idempotent under the double
  fire (report-first, event-second no-ops on already-retargeted
  tabs) — the report gives synchronous truth plus facts the event
  cannot carry.
- `Rewritten[]` → the ", updated links in N notes." announcement
  suffix (distinct paths).
- `Failed[]` → a recorded refusal presentation: per-file failures
  never abort the operation (core's rule); the host surfaces "Links
  in {N} notes could not be updated." with the affected paths
  reachable (fail-closed presentation, core's failure kinds relayed —
  never re-told).
- `OpId`/`UndoOpIds` → the undo stack entry (F10).

**F10 — Structural undo (the Windows first).** Scope: mac's #871
model, minus nothing the FFI supports:
- A host-side LIFO stack of inverse operations per vault (rename ↔
  rename-back, move ↔ move-back — mac's re-run-the-forward-FFI
  model; batch move → `UndoBatchMove(opId)`). *Amended
  (implementation finding, recorded in the mutation-golden README):*
  compound folder-note renames also reverse by re-running
  `RenameFolderWithNote` with inverse arguments — the recorded
  `UndoOpIds` sequence is not host-executable as FL6-1 documents it,
  because `undo_structural` admits only the LATEST journal row and
  every undo journals itself; a core follow-up records the contract
  contradiction. For the same reason a batch entry is undoable only
  while its row is still the journal's latest — any newer step
  purges recorded batch entries from both stacks rather than letting
  them surface later as a false "files have changed".
- Recording rules per verb (mac's exact table): rename & move =
  undoable; newNote/newFolder/duplicate (and W5-3's template create)
  = **history barrier** (stacks cleared); trash = not-undoable AND a
  barrier.
- Routing: **Ctrl+Z / Ctrl+Y route to structural undo/redo only
  while the files tree owns focus and no inline rename is active**
  (mac's `undoTargetsStructural` gate translated; everywhere else
  the editor's own undo stack keeps the chord). Tree-scoped
  delivery, like F2-rename.
- Empty-stack chord announces **"Nothing to undo."** (mac verbatim);
  an executability preflight that finds changed files drops the
  suspect history and announces **"Can't undo — the files have
  changed."**; completed ops announce **"Undid/Redid move of {x}." /
  "Undid/Redid rename to {y}."**
- Vault transition clears the stacks (workspace-lifetime state).

**F11 — Announcements.** The mutation sentence family is deliberately
**host-composed residue on both platforms** (mac's U2-6 wrappers
carry the same W0.5-3 markers) — this issue ships mac-verbatim
sentence parity in the residue channel with the established Recycle
Bin adaptation, and adopts the two canonical events that already
exist: `SelectionCopied` (F7) and `DuplicateFilesOnly` (F5). No new
canonical events (a cross-language vocabulary change is beyond #744;
FR-1 records it).

## Contracts — the mutation harness (the ship gate)

**H1 — Architecture: the shipped two-oracle mechanism, extended.**
The harness follows the read-side §W-A pattern exactly, because it is
the mechanism CI runs today: **shared committed goldens asserted
independently on each platform** (transitive cross-platform
equality), Windows as the regen authority (mac Swift is not locally
compilable; the read harness records the same rule), canonical JSON
per `CanonicalJson.cs`'s rules. Concretely:
- `tools/ParityHarness` gains a **mutation mode** (`--mutations --out
  <dir>`): builds a deterministic fixture vault, drives the scenario
  scripts (H3), serializes each scenario's terminal state, writes
  one artifact per scenario.
- Goldens: `crates/slate-core/tests/fixtures/mutation_golden/`.
- CI gates: a `MutationHarnessCensus` in SlateWindows.Tests re-runs
  in-process and byte-compares (the ParityHarnessCensus split); a
  Swift XCTest twin mirrors the scenario driver + serializer against
  the SAME goldens. Both green ⇒ the differential oracle holds; the
  goldens ARE the platform-independent oracle.

**H2 — The scenario artifact** (per scenario, canonical JSON):
- `tree`: ordered `{path, size, sha256}` for every file under the
  vault, **excluding `.slate/`** (cache bytes and oplog stems are
  legitimately platform/run-variant), forward slashes, byte-exact
  content pinned by hash (line endings never normalized — decision 9).
- `oplogs`: per vault-path `ReadOplog` projections with the
  normalization rules pinned by research: `TimestampMs` dropped;
  entries as `{opKind, contentHashBefore, contentHashAfter,
  payloadSha256}` in order; `.oplog` stem names never compared
  (random salt); `UserActorId` constant `"local"` via the FFI.
- `links`: the read-harness `LinksArtifact` re-emitted over the
  terminal vault — the referential-integrity oracle (H5-4).
- `reports`: each step's returned report, normalized — error-message
  strings reduced to failure KINDS/stages (platform io-text varies);
  `OpId`s recorded sequence-relative; `SaveReport.NewMtimeMs`
  dropped.
- `refusals`: each step expected to refuse records the typed
  exception kind (e.g. `DestinationExists` with the existing path's
  spelling).

**H3 — The scenario set** (each a scripted sequence over the fixture
vault; each asserted against BOTH oracles):
- **S1 occupied destination**: create/rename/move onto an occupied
  path (disk and case-insensitive-index arms) → typed
  `DestinationExists`, nothing written, unrelated files byte-
  unchanged.
- **S2 retry-after-conflict**: S1's refusal → remove the conflict →
  retry the identical op → success; the terminal state is
  byte-identical to the never-conflicted run (idempotence).
- **S3 rewrite interruption** (host-drivable, finding 6): a rename
  whose link-rewrite set includes a file keyed to
  `SLATE_TEST_FAULT_AFTER_WRITE` → the move lands, the faulted
  rewrite reports in `Failed[]`, the remaining rewrites land, and
  the terminal artifact captures the recorded divergence honestly;
  the `_PRE_WRITE` twin leaves the faulted file byte-unchanged. The
  reopen-recovery arm (`_RECOVERY_FINALIZE`) asserts the journal
  survives and the next open completes it.
- **S4 undo round-trips**: single rename, single move, the compound
  folder-note rename (two-op `UndoOpIds` sequence), and a batch move
  via `UndoBatchMove` — each undone to a **byte-exact** prior state
  (hash-verified, core's recorded pre-op hashes), then redone;
  plus the typed rejections: undoing a non-latest op ("only the
  latest structural op is undoable"), `UndoBatchMove` when the
  latest row is not a batch.
- **S5 pre-mutation abort** (the cancellation analog, finding 5 /
  FD-6): `SLATE_TEST_FAULT_PLANT_MARKERS` aborts a move, a batch
  move, and a delete before any fs mutation → the vault is
  byte-identical to pre-op, the op journals nothing user-visible,
  and a subsequent retry succeeds cleanly.
- **Core-side residue (H4)**: mid-batch partial interruption,
  rollback, `RollbackIncomplete`, and reopen recovery of a torn
  batch stay core-side in `structural_batch.rs` (71 tests). W5-4
  adds there only what the invariant list finds ungated, as
  FFI-SHAPED sequences (the ops a host would issue, in host order).

**H4 — The reachability split is recorded, not hidden.** The harness
README section in the artifact dir states which scenarios run
host-side on both platforms and which invariants are held by
core-side tests, with the `pub(crate)` seam boundary as the reason.
A future FFI cancellation seam upgrades S5; recorded in FR-2.

**H5 — The seven invariants** (asserted per scenario where
applicable, with research-pinned scoping):
1. Unrelated files byte-unchanged (tree-hash compare against the
   scenario's untouched set).
2. Destination never clobbered (S1/S2; #911's primitive underneath).
3. Rollback byte-exact **for the operations core guarantees it**:
   occupied-destination preflight and the marker-plant abort. Where
   core itself admits residue (single-move tx1 best-effort revert,
   `RollbackIncomplete` batches), the invariant is that the residue
   is TYPED AND REPORTED (`rollback_failures`, `requires_rescan`),
   never silent — FR-3 records the scoping.
4. Referentially correct link rewrites: every previously-resolved
   link resolves post-rewrite, targets equal moved identities
   (the `links` artifact; core's referential-stability census is the
   prior art).
5. Transactionally consistent op-logs per H2's projection:
   `PathChanged` markers precede rewrite batches, hash chains
   connect, `CreateExclusiveBytes` records no entry (by contract).
6. Idempotent retry (S2's byte-identity).
7. Byte-exact undo restoration (S4, hash-verified).

**H6 — Determinism rules.** Fixture vault built by the harness from
committed fixture content (not a checkout copy with live caches);
fixed name sets; no wall-clock in any artifact; scenario scripts are
data (an ordered op list) shared verbatim between the C# driver and
the Swift twin so "identical sequences on both platforms" is
enforced by construction, not by review.

## Divergence register (owner-recorded; off-limits for re-litigation)

- **FD-1 — Chord surface**: newNote/newFolder/moveTo stay chord-
  unbound on Windows (the recorded UnboundOnWindows divergences
  stand); rename stays F2; delete gains the **Delete key**,
  tree-scoped (mac ⌘⌫'s Windows convention twin); undo/redo are
  Ctrl+Z/Ctrl+Y **tree-scoped only** (the editor owns them
  elsewhere — mac's focus-gate translated).
- **FD-2 — Rename presentation**: Windows keeps the expander-field
  pseudo-inline rename (Enter/Esc semantics identical to mac's
  field); mac renames in-row. Mechanics, not semantics.
- **FD-3 — "Recycle Bin"** replaces "Trash"/"the system Trash" in
  every user-visible string (established at W1-2; delete
  confirmation copy adapts mac's sentences verbatim otherwise).
  *Carve-out (red team):* SHARED registry labels stay mac-byte-
  identical — the P3 census admits no label divergence for a shared
  id, so `slate.file.delete` keeps mac's "Move to Trash" label while
  its Windows-authored hint, every sentence, every button, and every
  Windows-only label say Recycle Bin. FD-5's `LabelDivergence`
  mechanism is reserved for surfaces the OS itself names (Finder →
  File Explorer), not word-preference relabels.
- **FD-4 — Delete chord**: Delete key vs mac's ⌘⌫; tree-scoped on
  both.
- **FD-5 — "Reveal in File Explorer"** label vs mac's "Reveal in
  Finder"; the P3 label census carries the disposition.
- **FD-6 — "Cancellation" scenario redefined** (finding 5): the
  host-drivable cancellation observable is the pre-mutation abort
  (S5); true mid-operation cancellation requires FFI that does not
  exist and is recorded as future work (FR-2), not silently claimed.
- **FD-7 — Windows mutations stay synchronous on the UI thread**
  (the shipped model; mac detaches under a structural gate). A
  large link-rewriting rename blocks the UI for its duration; the
  harness measures worst-case fixture timings so the trade-off is
  quantified, and FR-4 records it.

## Accepted-risk register (owner-recorded; off-limits for re-litigation)

- **FR-1 — Mutation sentences stay residue** on both platforms
  (mac's own U2-6 markers); canonical adoption limited to
  `SelectionCopied` + `DuplicateFilesOnly`. A vocabulary family for
  structural mutations is cross-language scope beyond #744.
- **FR-2 — No mutation cancellation FFI**; `RewriteFailureKind::
  Cancelled` stays a reserved shape. S5 is the analog; a
  cancel-bearing overload set is future core work.
- **FR-3 — Rollback residue states** (single-move tx1 best-effort
  revert; `RollbackIncomplete` + `rollback_failures` +
  `requires_rescan` batches; folder-note rename rewrite residue) are
  core-documented behavior; the harness pins that they are typed and
  reported, not that they cannot exist.
- **FR-4 — Sync UI-thread mutations** (FD-7): accepted for parity
  with every shipped Windows structural op; revisit if harness
  timings show user-visible stalls on realistic vaults.
- **FR-5 — Trash is a black hole**: bytes leave the vault via the
  platform trash; the tree oracle asserts absence + the journaled
  `oplog_name` payload + the surviving `.oplog`; restore-from-trash
  is core's recorded follow-up, not this issue's.
- **FR-6 — Windows CI skips `census_`-prefixed core tests** (the
  recorded environmental skip), so core's byte-exact-undo census
  runs on the other lanes only; the HARNESS's S4 gives Windows its
  own hash-verified undo coverage, which is the honest cover for
  that skip.
- **FR-7 — No journal-read FFI**: op-log consistency and "undo
  state" are defined over per-file `ReadOplog`, the returned
  reports, and typed rejections — not over the `structural_ops`
  table, which no FFI exposes.
- **FR-8 — Delete-staging TOCTOU** (codex round 1): the F6
  child-count probe and the trash execute without a shared lock or
  token — a concurrent writer (a sync client) can populate a folder
  between the probe and the trash, so contents can exceed what the
  confirmation named. Accepted: mac's staging has the identical
  host-probe shape, the destination is the recoverable Recycle Bin,
  and closing the window needs a core staged-confirmation token
  validated under the structural lock — a core FFI follow-up, filed
  at PR time, not host scope.
- **FR-9 — The Move-To enumeration is synchronous on the UI thread**
  (codex round 1): the paged walk (50k-folder bound, 1k-row pages)
  blocks the dispatcher for its duration on a pathologically large or
  slow vault, FD-7's trade-off extended to a read. Accepted at the
  recorded bound; an async, cancellable destination loader is the
  recorded upgrade path, filed at PR time.

## Phase plan

1. **Phase A** — report consumption + rename hardening + undo (F3,
   F9, F10): the machinery every verb shares.
2. **Phase B** — verbs: Duplicate, copyPath, reveal (F5/F7/F8 — the
   three new ids), delete-flow parity (F6), newNote/newFolder polish
   (F1/F2).
3. **Phase C** — the Move-To picker (F4).
4. **Phase D** — the harness (H1–H6) + close-out (matrix rows,
   chords.json, w_c_matrix, the FlaUI journey:
   create → rename → duplicate → move → delete via UIA with disk
   verification at each step, axe scans).
Each phase lands with facts; the recorded traps apply (STA trash —
the `DeleteOnSta` pattern; peered elements; public bindings; CRLF
format gate; async Invoke settle; `EnvFaultLock` serialization for
every env-seam test).
