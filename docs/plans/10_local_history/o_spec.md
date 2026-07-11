# O executable spec — Local history + change tracking

Issues: [#539](https://github.com/coryj627/slate/issues/539) (O-1) · [#540](https://github.com/coryj627/slate/issues/540) (O-2) · [#541](https://github.com/coryj627/slate/issues/541) (O-3) · [#542](https://github.com/coryj627/slate/issues/542) (O-4) · [#543](https://github.com/coryj627/slate/issues/543) (O-5) · [#544](https://github.com/coryj627/slate/issues/544) (O-6).
Milestone: [GH 15](https://github.com/coryj627/slate/milestone/15). One PR per issue.
Plan: [00_plan.md](00_plan.md). U-program Presentation-Ready DoD applies to O-5; backend norms
(fmt/clippy pre-push, adversarial censuses for correctness invariants — release-run, not
`debug_assert`) apply throughout.

**Execution order: O-1 → (O-2 ∥ O-3) → O-4 → O-5; O-6 after Milestone N ships.**

Baseline facts (verified 2026-07-03, this worktree — oplog.rs is 1260 lines incl. ~24 tests):

- On-disk format v1: 8-byte header `b"YOLG"` + version `1` + reserved (oplog.rs:86-88); entries =
  `body_len:u32 LE | body | body_checksum:u32 LE` (first 4 bytes of blake3(body), torn-write
  canary); body = `timestamp_ms:i64 | op_kind:u8 | actor_id (u16-len) | hash_before (u16-len,
  64-hex blake3) | hash_after (u16-len) | payload (u32-len)` (oplog.rs:32-52). Forward
  length-prefixed frames only — **no tail seek**; any "find the last X" is a forward walk.
- `OpKind`: `WholeFileReplace = 1` (payload = full contents; snapshot/replay anchor),
  `EditBatch = 2` (payload = encoded `Vec<EditOp>`) (oplog.rs:108-132). Unknown kind mid-log →
  reader stops, returns well-formed prefix (oplog.rs:99-106). Header version mismatch → hard
  `InvalidData` error. One entry per save is the module's atomicity contract (oplog.rs:17-21).
- `EditOp { Insert{pos,text} | Delete{start,end} | Replace{start,end,text} }`, UTF-8 byte offsets
  in OLD-content space (oplog.rs:136-152); encoding `op_count:u32 | [tag:u8|fields]*`, LE,
  length-prefixed text (oplog.rs:177-212); bounds guard `MAX_PLAUSIBLE_OP_COUNT` (oplog.rs:170-175).
- `OpLogEntry { timestamp_ms, user_actor_id, op_kind, content_hash_before, content_hash_after,
  payload_bytes }` (oplog.rs:336-347).
- API: `oplog_path(cache_dir, file_id)` :365, `append_entry` :369 (O_APPEND + OS exclusive lock
  across header-check+append, single write syscall, `sync_data`, first-append `fsync_dir`;
  currently returns `io::Result<()>`), `read_oplog` :476 (missing file → `Ok(vec![])`; per-entry
  corruption → prefix), `reconstruct_at_tail` :258 (last snapshot + replay batches, ops applied in
  descending old-offset order, out-of-bounds → typed error), `reconstruct_at_hash` :349 (first
  `hash_after` match by prefix reconstruction — it does NOT verify the output's hash),
  `encode_edit_batch`/`decode_edit_batch` :186/:217.
- Session integration: `append_save_to_oplog` (session.rs:975-1053) decides snapshot-vs-batch per
  save. It computes `diff_to_ops(old, new)` FIRST and then decides (session.rs:1001-1017) — so
  edit ops are in hand even for cadence-forced snapshots; `old_contents: Option<&str>` is `None`
  only for cold-cache / non-UTF-8 old content. Per-file `OplogAppendState { last_hash_after,
  bytes_since_snapshot }` (session.rs:508-518); `anchor_oplog_snapshot` (session.rs:4307) writes
  pure anchors (hash_before == hash_after) before link-rewrites; `save_text_locked` computes
  `now_ms()` internally (session.rs:872) — there is no caller-injectable clock today.
- `VaultSession::read_oplog(path)` (session.rs:1061) + uniffi mirror (lib.rs:446-453,
  `OpLogEntry`/`OpKind` records at lib.rs:1906-1944; EditBatch payloads currently opaque to hosts).
- `SessionConfig`: `oplog_compaction_threshold_bytes` (5 MiB) is **live today as the snapshot
  cadence** (session.rs:1017-1019); `oplog_compaction_threshold_entries` (10_000) and
  `oplog_retention_days` (90) are reserved/unenforced; `user_actor_id` default `"local"`
  (session.rs:44-125).
- **No** compaction execution, version API, `StructuredDiff`, or `DiffOperation` anywhere.
- **`VaultEventListener` does not exist** — it is an unimplemented sketch in `05` §4.4. The only
  callback trait is `ScanProgressListener` (session.rs:427; uniffi lib.rs:1574 — the uniffi
  callback-interface precedent). There is no error banner surface in the Swift app; alert flows
  follow the MainSplitView conventions.
- Logs keyed by `files.id` (SQLite, regenerable) — cache rebuild breaks the id↔log association
  (plan decision #3). `files.id` is `INTEGER PRIMARY KEY` **without** `AUTOINCREMENT`
  (migrations/001_init.sql:8) and `delete_file` deletes the row (session.rs:3849), so **SQLite
  reuses rowids** — the code itself documents the hazard (session.rs:501-507). Delete goes to the
  **system trash**, journaled in the structural journal, NOT undoable in-app (session.rs:3842-3846);
  nothing removes the `.oplog` file on delete (O-1 pins this with a test).
- There is no `create_note` API and `save_text` **silently overwrites** existing files
  (`DestinationExists` today fires only on folder/move paths — session.rs:3759/4069/4122); the
  Swift new-note flow is `save_text(path, "", nil)` (AppState.swift:4700-4716).
- Structural journal records `StructuralOpKind::{CreateFolder, RenameFolder, MoveFolder,
  RenameFile, MoveFile, DeleteFile, DeleteFolder}` with JSON payloads (session.rs:3766-3898).
- Block segmentation for diff classification: the **pure** walker is `reading_blocks_source`
  (reading.rs:130); `reading_blocks` (session.rs:2241) is its disk-backed wrapper. Frontmatter
  split/compose: `split_note`/`compose_note` (U3-5).
- Settings UI is a `TabView` with Math / Code / Bibliography **tabs** (SettingsView.swift:28-38).
- Leaf registry + panel/AppState patterns: as inventoried in
  [../09_sync_cli/m_spec.md](../09_sync_cli/m_spec.md) baseline facts. The injectable
  `AnnouncementPosting` seam is specified in M-3; whichever of M-3/O-5 lands first creates it,
  the other reuses it.
- Census convention: `census_*` fns in `crates/slate-core/src/session/tests/*.rs` with env-scaled
  `census_scale()`; benches in `crates/slate-core/benches/`, baselines in `BENCHMARKS.md`.

---

## O-1 · Op-log v2: durable identity + semantic annotations (#539) — PR 1

### Format v2 (oplog.rs)

**Header v2** — the 8-byte fixed header (magic + version `2` + reserved) followed immediately by:
`path_len:u16 LE | path (UTF-8, vault-relative, as at creation) | generation:u32 LE`.
`generation` starts at 0 and is incremented by each compaction rewrite (O-2 consumes it for
paging-cursor invalidation). Entries start after this block. Readers:

- v2 file → expose `OplogHeader { version: u8, created_path: Option<String>, generation: u32 }`.
- v1 file → `created_path: None, generation: 0`; entries read exactly as today (full backward
  compatibility; **no eager migration** — v1 logs are upgraded only when compaction rewrites them).
- New logs are always created v2. `append_entry` gains the creation path argument (used only when
  it writes a fresh header) and **returns the post-append file length**
  (`io::Result<u64>`) so the O-2 trigger check costs zero extra syscalls. The header-under-lock
  discipline (PR #105 fix) is unchanged.

**Op kind 3 — `Annotated`**: an annotated wrapper around a single inner entry, so every save
remains **one atomic entry** (the oplog.rs:17-21 contract is preserved — no entry pairs, ever).
Payload = `inner_kind:u8 (1|2) | inner_len:u32 LE | inner_payload | ann_count:u16 LE |
annotations`. Each annotation: `ann_tag:u8 | ann_len:u32 LE | ann_body (UTF-8 JSON)`.
**Unknown ann_tags are skipped using `ann_len`** (annotation vocabulary is forward-extensible
without a format bump — unlike op kinds, where unknown = truncate).

| tag | name | body |
|---|---|---|
| 1 | `SetProperty` | `{"key": String, "value_json": String}` |
| 2 | `RemoveProperty` | `{"key": String}` |
| 3 | `ToggleTask` | `{"ordinal": u32, "new_status": String(1 char)}` |
| 4 | `FrontmatterReplace` | `{}` (whole-fm edit via U3-4 show-source) |
| 5 | `PathChanged` | `{"from": String, "to": String}` |

```rust
pub enum OpAnnotation {
    SetProperty { key: String, value_json: String },
    RemoveProperty { key: String },
    ToggleTask { ordinal: u32, new_status: char },
    FrontmatterReplace,
    PathChanged { from: String, to: String },
}
pub fn encode_annotated(inner_kind: OpKind, inner_payload: &[u8], anns: &[OpAnnotation]) -> Vec<u8>
pub fn decode_annotated(payload: &[u8]) -> Result<(OpKind, Vec<u8>, Vec<OpAnnotation>), String>
```

`reconstruct_at_tail` / `reconstruct_at_hash` treat kind 3 by unwrapping and replaying the inner
entry (a wrapped snapshot is a replay anchor; a wrapped batch is a batch). A kind-3 entry wrapping
an **empty batch** with `hash_before == hash_after` is a pure marker (the `PathChanged` case).

**Marker hash rule (normative — protects version identity):** a pure marker's
`hash_before == hash_after` MUST be the log's current **tail hash** (the last entry's
`hash_after`, from `OplogAppendState` or a tail read) — NEVER the index's/disk's current hash,
which can differ after external edits and would introduce a `hash_after` whose prefix
reconstruction is not that hash's content. If the log is empty, no marker is written (there is no
history to re-path). This keeps the axiom "every `hash_after` in a log prefix-reconstructs to
bytes whose blake3 IS that hash" true for every entry kind; O-3's integrity verification enforces
it downstream.

**Downgrade note (documented in oplog.rs module docs):** builds older than O stop reading a log at
the first kind-3 entry (prefix rule) and fail hard on v2 headers. Acceptable: same-machine
downgrades mid-vault are already unsupported; the failure mode is "history unavailable", never
corruption.

### Write-path hooks (session.rs)

- `append_save_to_oplog` gains `annotations: &[OpAnnotation]`; kind selection: annotations empty →
  kind 1/2 exactly as today; annotations non-empty → **one** kind-3 entry wrapping whatever inner
  kind today's decision logic picked (cadence-forced snapshot with a `SetProperty` = kind 3
  wrapping kind 1 — one entry, intent preserved, atomicity preserved).
- Callers pass intent: `set_property` → `[SetProperty]`; `delete_property` → `[RemoveProperty]`;
  `set_frontmatter_source` → `[FrontmatterReplace]`; `toggle_task_status` → `[ToggleTask]`; plain
  `save_text`/`save_composed` → `[]`.
- `rename`/`move` (file variants): after the index update, append a pure `PathChanged` marker per
  the marker hash rule (skipped when the log is empty or absent). Folder moves append one marker
  per contained file (the move loop already enumerates them).
- `delete_file` **must leave the `.oplog` in place** (pinned by test; it already does — the pin
  prevents regression).

### Durable identity: `files.oplog_name` + log naming + scan-time reconcile

- Schema migration: `ALTER TABLE files ADD COLUMN oplog_name TEXT` (nullable). `oplog_name` is the
  log filename stem; **the column is the only binding** — log paths are NEVER derived from
  `files.id` after O-1 (every `oplog_path(cache_dir, file_id)` caller migrates to the column).
- **New-log naming (rowid-reuse defense — session.rs:501-507 documents the hazard):** new logs are
  named `<32 lowercase hex>.oplog` where the stem = first 16 bytes of
  `blake3(path ‖ now_ms ‖ session_counter)`, re-derived with a bumped counter if the file already
  exists. Existence-check + create are serialized by a **new directory-level lock file**
  (`.slate/oplog/.dir.lock`, OS exclusive lock — today's locks are per-log-file only and don't
  cover creation races). Stems are unique forever and mean nothing — no id math, no reuse. Legacy
  v1 logs keep their `<id>.oplog` names; because new files never get id-derived names, a recycled
  `files.id` can never collide with a dead file's log.
- Scan reconcile step (end of `scan_initial`):
  1. Fast path — every `*.oplog` whose stem matches some live `files.oplog_name` is bound; when
     the cache is intact this covers everything and the reconcile does **no** log IO.
  2. **v1 legacy adoption (cache intact):** an unbound `<digits>.oplog` whose stem equals a live
     `files.id` with `oplog_name IS NULL` → adopt directly (this IS today's binding, preserved
     verbatim — no hash precondition; today's code handles a diverged tail by re-snapshotting on
     next save, session.rs:962-1000, and that behavior is unchanged).
  3. Remaining unbound logs (the cache-rebuild / orphan cases): forward-walk the log once,
     collecting header path, last `PathChanged`, tail hash, tail timestamp, and the set of all
     `hash_after` values. Effective path = last `PathChanged.to`, else `created_path`, else (bare
     v1 log after a cache rebuild) unbindable-by-path. Then:
     - Effective path names a live file with `oplog_name IS NULL` → adopt.
     - No path match: salvage by content — if **exactly one** live file with `oplog_name IS NULL`
       has its current `content_hash` in the log's `hash_after` set → adopt; zero or multiple →
       no adoption.
  4. Still unbound + effective path known → **deleted-file remnant**. The reconcile retains
     these on the session as its output interface to O-3 (normative shape:
     `struct RemnantLog { stem: String, effective_path: String, tail_hash: String,
     tail_timestamp_ms: i64 }` — `Vec<RemnantLog>` on session state, refreshed per scan).
  5. Still unbound + no effective path, or any conflict (two logs claiming one file, a claimed
     file already bound) → quarantine: leave on disk, stderr warning, invisible to features.
     Never guess. (O-2's retention sweep eventually reclaims quarantined logs too.)
- Cost honesty: with an intact cache the reconcile is one `read_dir` + one indexed SQLite pass
  (step 1 + 2, no log reads). Only a cache rebuild leaves logs unbound, and then the reconcile
  reads each once (the census scenario; bounded by vault size, runs inside the scan that is
  already O(vault)).

### Per-op FFI accessors (unblocks O-4/O-5 rendering)

`decode_annotated` + a uniffi-exported
`decode_edit_batch_ops(payload: Vec<u8>) -> Result<Vec<FfiEditOp>, VaultError>` replace the
"payloads are opaque to hosts" note at lib.rs:1906-1909. (`FfiEditOp` = uniffi record mirror of
`EditOp`.)

### Tests + censuses (Rust)

- v2 header round-trip incl. path record + generation; v1 files still read (fixture bytes checked
  in, not generated — pins the wire format).
- kind-3 encode/decode: every annotation tag; wrapped-snapshot and wrapped-batch reconstruction;
  unknown tag 250 skipped, inner still applied; truncated annotation block → descriptive error
  (never panic; extend the corruption suite's style, oplog.rs:945-985).
- Marker hash rule: rename after an **external edit** (disk hash ≠ tail hash) → marker carries the
  tail hash; every `hash_after` in the log still reconstructs to content whose blake3 equals it
  (the identity axiom test — this is the regression test for the marker-vs-external-edit hazard).
- Rowid-reuse census: `census_history_never_cross_attaches` — randomized delete-newest → recreate
  → edit sequences (the exact rowid-recycling flow): the new file's history NEVER contains the
  dead file's entries; the dead file's log stays a remnant. Plus the copied-log conflict fixture →
  quarantined, not misbound.
- Legacy adoption: v1 log + intact cache + externally-edited file → adopted (not orphaned) — the
  strictly-no-lossier-than-today gate; v1 log after cache rebuild with unique content match →
  salvaged; ambiguous (two identical empty files) → not adopted.
- `census_oplog_v2_reconstruct` — 100k random histories (random mixes of kind 1/2/3, annotations,
  pure markers, CRLF/unicode/empty docs) → `reconstruct_at_tail` equals a plain-string reference
  model. Exhaustive over the fixture edge set (empty batch, marker-only log, annotated snapshot,
  v1→v2 mixed file).
- `census_reconcile_after_cache_rebuild` — build vault, edit N files, rename some, delete some,
  delete `cache.sqlite`, reopen + rescan → every live file's history reattaches (verified by
  tail reconstruction against disk bytes); deleted files' logs land in the remnant set with
  correct effective paths.
- Bench: `bench_append_annotated` — append with 3 annotations vs plain batch; assert overhead
  < 10% and absolute append cost at the #404 baseline resolution (numbers in the PR).

## O-2 · Compaction + retention (#540) — PR 2

### Fold semantics (normative — positional, never timestamp-filtered)

Wall-clock timestamps are not monotonic across entries (clock steps/NTP); filtering by timestamp
over an append-ordered log can double-apply or orphan entries. Therefore compaction is always a
**prefix fold at a position boundary P**: entries `[0..=P]` are replaced by one synthesized
anchor; entries `[P+1..]` are retained **verbatim, by position**. The replay chain stays valid by
construction because the anchor's content = `reconstruct(entries[0..=P])` and its
`hash_after` = entry P's `hash_after` (`hash_before == hash_after`, timestamp = entry P's
timestamp; annotations on folded entries are discarded — they described history that no longer
exists at save-point granularity).

Choosing P — two triggers, evaluated when compaction runs:
- **Retention fold:** `P_ret` = the largest index `i` with `entries[i].timestamp_ms ≤ cutoff`
  (`cutoff = now − retention_days`). Positionally-earlier entries with later timestamps (clock
  weirdness) fold too — they are positionally older, which is the order that matters.
- **Size fold** (fires when file length > `oplog_compaction_threshold_bytes` OR entry count >
  `oplog_compaction_threshold_entries`): `P_size` = `len − 1 − K` where
  `K = min(len − 1, floor(threshold_entries / 2))`, then increased (retaining fewer entries) until
  the retained entries' encoded size ≤ `floor(threshold_bytes / 2)` or only the tail entry
  remains. This deliberately folds save-points *within* retention for pathologically hot files —
  sanctioned by `05` §7.5 ("compacted into a snapshot + recent ops") and reflected in the plan DoD.
- `P = max(P_ret, P_size)` (fold the most that any trigger demands); if `P < 0` (nothing to fold)
  or the fold cannot reduce below the triggers (already anchor + minimal tail — e.g. one > 5 MiB
  snapshot payload), record `compaction_futile = true` in `OplogAppendState` (cleared by the next
  append) and stop — **no livelock**: futile logs are not re-enqueued until they grow again.
- Idempotence follows: a second run with the same `now` computes the same P over the folded log,
  finds `P < 0` (the anchor is the only pre-cutoff entry and folding one entry into itself is the
  identity — detected as output == input, skip the rewrite; the mtime-unchanged test pins this).

Rewrite: under the lock — new file = v2 header (path = current effective path, **generation + 1**
— this is where v1 logs upgrade and stale header paths heal) + anchor + retained entries, written
to `<stem>.oplog.tmp`, `sync_data`, rename over, `fsync_dir`, release. Update the session's
`OplogAppendState` (tail hash unchanged by construction — asserted).

**Append/compact race protocol (lock-then-verify-inode):** rename-over swaps the path→inode
binding, so a waiting appender could acquire the advisory lock on the *orphaned* inode and write a
lost entry. Both `append_entry` and the compactor, after acquiring the lock, `fstat` the open
handle and `stat` the path; on (dev, inode) mismatch → close, reopen, retry (bounded, 5 attempts →
hard error). This is the load-bearing invariant; it gets its own census.

### Scheduling, remnant reclamation, failure surfacing

- Per-append trigger check is pure arithmetic: `append_entry`'s returned file length vs the byte
  threshold, plus an in-memory per-file entry counter that starts unknown and is made exact
  whenever the log is read anyway (compaction, reconcile step 3, first `list_versions`). **No log
  walk ever runs on the save path**; a file can exceed the entry-count trigger transiently until
  one of those reads occurs — the byte cap bounds bloat meanwhile (documented).
- Firing enqueues the file on a session-owned background compaction worker (one `std::thread`,
  single-flight per file, idle when queue empty, joined on session close; a session-close flag is
  checked between files — mid-file compaction completes, sub-second by the perf gate).
- On-open sweep (after scan reconcile): enqueue any bound log whose file size exceeds the byte
  threshold (`stat` only). **Remnant reclamation:** any *unbound* log (remnant or quarantined)
  whose tail-entry timestamp ≤ `now − retention_days` is **deleted** — this is `05` §7.5's "old
  ops are discarded" applied to deleted files, it bounds `.slate/oplog` disk, and it is the
  mechanism by which `list_deleted_files` ages entries out (O-3). Within retention, remnants are
  never rewritten (full fidelity for recovery).
- **Failure surfacing — this issue builds the channel** (nothing exists today; `05` §4.4 is an
  unimplemented sketch): a minimal `VaultEventListener` trait
  (`fn on_error(&self, code: EventErrorCode, path: String, message: String)` — one method for
  now, documented as the partial §4.4 delivery) + `register_event_listener` /
  `unregister_event_listener` on `VaultSession`, uniffi-exported as a callback interface
  (`ScanProgressListener` at lib.rs:1574 is the mechanical precedent). The compaction worker
  dispatches `on_error(CompactionFailed, path, "Slate couldn't compact the edit history for
  <path>: <cause>. History for this file may grow unbounded.")` — §9.3.3's "failure is a
  user-visible hard error". O-5 provides the Mac-side conformance + alert.

### Tests + censuses

- Unit: prefix-fold correctness (anchor content/hash/timestamp; retained tail verbatim);
  P_ret vs P_size interaction; size fold boundary arithmetic (entries and bytes arms); futile
  detection (giant single entry → futile flag, no re-enqueue until append); idempotence
  (mtime unchanged on second run); v1 → v2 upgrade + generation bump + header-path heal after
  rename; remnant reclamation (out-of-retention remnant deleted; in-retention remnant untouched;
  quarantined log reclaimed on the same rule).
- **Error dispatch test:** a compaction failure injected via a test-only IO fault hook → a
  registered fake listener receives `CompactionFailed` with the exact copy (this is the g1 gap —
  the dispatch itself is tested in Rust, independent of the UI).
- `census_compaction_preserves_history` — random histories **including backwards-clock segments**
  (timestamps scripted per entry at the oplog layer), compact at random points interleaved with
  more edits and repeated compactions: `reconstruct_at_tail` byte-equal before/after every
  compaction; every retained `hash_after` still reconstructs byte-identically; generation strictly
  increases; runs at `census_scale()`.
- `census_append_compact_race` — writer threads appending while a compactor loops (both real
  file-locked code paths, not mocks): after quiescence, the log contains every appended entry
  exactly once, in order, and reconstructs to the reference model.
- §9.3.3 gates: `bench_compact_50k_ops` < 1s (release, background thread);
  `census_five_years_daily_editing` — synthetic 5×365 saves across a sample of files, periodic
  compaction, final state reconstructs + total `.slate/oplog` size bounded by a concrete ceiling
  computed in the test from the thresholds.
- Save-path bench (g2): `bench_save_with_trigger_check` — save against a pre-built 5 MiB log;
  assert the save path's added cost is arithmetic-only (no regression vs the #404 baseline).
- Editor-blocking assertion: an append during an in-flight compaction of the same file completes
  within the lock-retry budget (artificially slowed compactor via a test-only hook).

## O-3 · Version history + deleted-file recovery APIs (#541) — PR 3

### Session APIs (all uniffi-mirrored)

```rust
pub struct OpAnnotationSummary { pub kind: String, pub display: String }
// kind = the tag name ("SetProperty"); display = "Set property 'status'" — the UI chip text.

pub struct VersionSummary {
    pub position_from_tail: u32,      // 0 = newest. ROW identity for UI lists/selection
                                      // (content_hash_after is NOT unique per row: A→B→A).
    pub content_hash_after: String,   // the CONTENT identity (plan decision #4)
    pub timestamp_ms: i64,
    pub op_kind: OpKind,              // the inner kind for Annotated entries
    pub op_count: u32,                // decoded batch len; 0 for pure markers; 1 for snapshots
    pub byte_delta: i64,              // len(after) − len(before); snapshot with no prior = len
    pub annotations: Vec<OpAnnotationSummary>,
    pub is_marker: bool,              // hash_before == hash_after (anchors, PathChanged)
    pub audio_fragment: String,       // "12 operations, 340 bytes added" — Swift prepends the date
}

pub fn list_versions(&self, path: &str, paging: Paging) -> Result<Page<VersionSummary>, VaultError>
// Newest first. Markers included (UI filters; CLI/tests want the full ledger).
// Cursor = opaque token (position_from_tail, header GENERATION). A compaction between
// pages bumps the generation (O-2) → typed VaultError::InvalidArgument("history changed,
// restart paging") — the UI reloads page one. (Tail hash alone cannot detect compaction:
// the fold preserves the tail by construction.)

pub fn version_content(&self, path: &str, version_hash: &str) -> Result<String, VaultError>
// reconstruct_at_hash + MANDATORY integrity verification:
// blake3(result) == version_hash, else Err(HistoryUnavailable { path, reason:
// "version <hash> failed integrity verification" }). Unknown hash →
// InvalidArgument("no such version"). Same-hash duplicates are safe precisely
// because of this check: any occurrence that passes reconstructs the same bytes.

pub fn restore_version(&self, path: &str, version_hash: &str,
                       expected_content_hash: Option<&str>) -> Result<SaveReport, VaultError>
// version_content (verified) → the standard save_text machinery verbatim (atomic write,
// WriteConflict on expected-hash mismatch, index refresh, its own op-log entry).
// No history rewrite ever.

pub struct DeletedFileEntry {
    pub path: String,                 // effective path (last PathChanged / header path)
    pub deleted_at_ms: Option<i64>,   // structural-journal DeleteFile timestamp when available
    pub recoverable: bool,            // tail reconstructs + passes integrity verification
    pub size_bytes: Option<u64>,      // reconstructed tail length when recoverable
}
pub fn list_deleted_files(&self, paging: Paging) -> Result<Page<DeletedFileEntry>, VaultError>
// Source: O-1's remnant set, joined with structural-journal DeleteFile rows for timestamps.
// Multiple remnants with one effective path (delete → recreate → delete): one row, the
// remnant with the newest tail timestamp wins (older ones are invisible here and age out
// via O-2's reclamation sweep). Ordered by deleted_at_ms desc, None last. Out-of-retention
// remnants don't appear because O-2's sweep has DELETED their logs — this list needs no
// retention filter of its own.

pub fn recover_deleted_file(&self, path: &str) -> Result<SaveReport, VaultError>
// reconstruct_at_tail(newest matching remnant, integrity-verified) → create_exclusive
// (below). On success the remnant re-binds (oplog_name set) so the recovered file KEEPS
// its pre-deletion history, and the recovery save appends onto it.
```

New error variant `VaultError::HistoryUnavailable { path: String, reason: String }` (additive;
uniffi-mirrored) — the "history is corrupt/inconsistent, the operation refused rather than served
wrong bytes" signal. New write primitive:

```rust
pub fn create_exclusive(&self, path: &str, content: &str) -> Result<SaveReport, VaultError>
// The create-if-absent write path (save_text silently overwrites — session.rs:820-867 —
// so it CANNOT be used for recovery). Under the save lock: path exists on disk or in the
// index → Err(DestinationExists { path }); else the standard atomic-write + index +
// op-log machinery. (Generally useful; the Swift new-note flow adopting it is a filed
// follow-up.)
```

Honesty rule (from `03` §11 "if feasible" and the system-trash reality): files deleted having
never been saved through Slate have no log — they are simply absent from the list; the UI's
Deleted segment carries a standing footnote pointing at the system Trash (O-5).

### Tests

- Version listing: fixture with saves, property edits, a rename, a cadence snapshot → order,
  `position_from_tail`, op_counts, byte_deltas, annotations, `is_marker` all pinned; paging
  drains; forced compaction mid-drain → generation-bump → typed error (implementable now — the
  cursor carries the generation).
- `version_content` byte-equality at every version of the fixture (reference model); duplicate
  hashes (A→B→A) both resolve to identical bytes; a deliberately corrupted-chain fixture →
  `HistoryUnavailable`, never wrong bytes (the B2 regression test).
- Restore: mid-sequence restore equals historical bytes (integration across two sessions — the
  DoD case); stale `expected_content_hash` → `WriteConflict`, nothing written; restore appends
  (history grows by one; prior versions unchanged).
- `create_exclusive`: absent → created atomically; present on disk (even unindexed) →
  `DestinationExists`, nothing written; present in index → ditto.
- Deleted: delete a saved file → listed with journal timestamp, recoverable; recover → bytes
  equal pre-delete tail, history continuity (pre-deletion versions listed); recover onto an
  occupied path → `DestinationExists`; never-saved deleted file → absent; delete → recreate →
  delete → one row, newest remnant wins, recovery returns the newest content;
  post-cache-rebuild deletion (no journal row) → listed with `deleted_at_ms: None`.

## O-4 · StructuredDiff engine + changes-since-last-open (#542) — PR 4

### Types (adapting the `05` §7.4 sketch — single-timeline core; the conflict-specific
`local/remote` split stays V2)

```rust
pub struct StructuredDiff {
    pub file_path: String,
    pub from_hash: String,
    pub to_hash: String,
    pub operations: Vec<DiffOperation>,     // document order
    pub audio_summary: String,              // "5 changes: 2 property changes, 2 added paragraphs, 1 heading edit."
}
pub struct DiffOperation {
    pub kind: DiffOpClass,
    pub line: u32,                          // 1-based first line of the affected block
    pub line_end: u32,                      // 1-based last line (inclusive); == line for one-liners
                                            // (both in the TO version; FROM for pure removals)
    pub semantic_description: String,       // "Added heading 'Goals' at line 10"
    pub detail: Option<String>,             // e.g. the inserted text, truncated to 200 chars
}
pub enum DiffOpClass {
    HeadingAdded, HeadingRemoved, HeadingEdited,
    PropertySet, PropertyRemoved,
    ParagraphAdded, ParagraphRemoved, ParagraphEdited,
    ListItemAdded, ListItemRemoved, ListItemEdited,
    TaskStatusChanged,
    CodeBlockEdited, MathBlockEdited, DiagramEdited, TableEdited,
    Other,
}
pub fn diff_versions(&self, path: &str, from_hash: &str, to_hash: &str)
    -> Result<StructuredDiff, VaultError>   // hashes resolved via version_content (verified)
```

**uniffi (owned by this issue, not O-5):** `StructuredDiff`, `DiffOperation`, `DiffOpClass`,
`ChangesSinceOpen` mirrored as records/enums; `diff_versions`, `changes_since_last_open`,
`mark_opened` exported on the `VaultSession` object — O-5 consumes all of them and ships no FFI
of its own.

### Engine (new module `structured_diff.rs`; pure functions over two strings + optional annotations)

1. Split both versions with `split_note` (U3-5); frontmatter diff = key-level compare of the two
   parsed property sets → `PropertySet`/`PropertyRemoved` ops (value in `detail`,
   `"Set property 'status' to 'final'"`). Annotations from the underlying log entries (available
   on the changes-since-last-open path) refine descriptions but are never *required*: the engine
   must produce correct classes from content alone (the two-arbitrary-versions path has no
   annotation context).
2. Body diff: segment both bodies with `reading_blocks_source` (reading.rs:130 — the pure walker;
   NOT the disk-backed session wrapper); align blocks by LCS over `(kind, content-hash)`.
   **Deterministic pairing rule for unmatched runs:** between consecutive LCS anchors, take the
   removed-side and added-side blocks in document order; pair the i-th removed with the i-th
   added **iff** same `kind` and normalized-edit-distance similarity > 0.6 (pinned by fixtures) →
   `Edited`; everything unpaired → `Removed`/`Added`. No cross-run pairing, no reordering
   detection (a moved block reads as remove + add — documented).
3. Class mapping from the block kinds (Heading→Heading*, ListItem(task)→`TaskStatusChanged` when
   only the status char differs else ListItem*, CodeFence→`CodeBlockEdited`, MathBlock→
   `MathBlockEdited`, Diagram→`DiagramEdited`, Table→`TableEdited`, Paragraph/Quote/Html→
   Paragraph*, ThematicBreak→`Other`).
4. Descriptions (normative copy): `"Added heading 'Goals' at line 10"`, `"Removed paragraph at
   line 23"`, `"Edited list item at line 7"`, task copy by `new_status`: `'x'`/`'X'` →
   `"Completed task '<text>'"`, `' '` → `"Reopened task '<text>'"`, any other char →
   `"Changed task '<text>' status to '<char>'"` (text truncated 60 chars);
   `"Set property 'status' to 'final'"`. `audio_summary` follows the §7.3 pattern: count first,
   then by-class breakdown, largest class first.
5. Determinism + totality: same inputs → identical output (ordered structures only); every input
   pair produces a diff without panicking (worst case: one `Other` per unmatched block).

### Changes-since-last-open

- Schema: `CREATE TABLE open_marks (file_id INTEGER PRIMARY KEY, last_opened_ms INTEGER NOT NULL,
  content_hash_at_open TEXT NOT NULL)` — regenerable-by-design; lost marks degrade to "no
  baseline" (plan decision #6).
- APIs:
  ```rust
  pub enum ChangesSinceOpen { NoBaseline, Unchanged, Diff(StructuredDiff), BaselineCompacted }
  pub fn changes_since_last_open(&self, path: &str) -> Result<ChangesSinceOpen, VaultError>
  // mark missing → NoBaseline; mark hash == current hash → Unchanged; hash found in log
  // (integrity-verified) → diff_versions(mark, tail); hash absent or verification fails
  // (compacted past / log rebound) → BaselineCompacted.
  pub fn mark_opened(&self, path: &str) -> Result<(), VaultError>   // upsert now + current hash
  ```
- Ordering contract for the host (pinned in the doc comment): compute `changes_since_last_open`
  FIRST, then `mark_opened` — marking first would always report `Unchanged`.

### Tests + censuses

- Fixture suite: one fixture per `DiffOpClass` (add/remove/edit × block kinds, property set/remove,
  every task-status copy arm) with exact expected descriptions; the §7.3 walkthrough example
  (heading + property + paragraph) reproduced verbatim as a fixture; a moved-block fixture pinning
  the remove+add reading; a pairing-rule fixture (two removed + two added in one run, mixed kinds).
- `census_structured_diff_total` — random document pairs (unicode, CRLF, frontmatter presence mix,
  pathological: 2k blocks, single 1 MB paragraph): never panics; every changed line index (from a
  plain line-diff reference) falls within some operation's `[line, line_end]` range; descriptions
  non-empty; deterministic across two runs; runs at `census_scale()`.
- Changes-since-open: full matrix (NoBaseline / Unchanged / Diff / BaselineCompacted — the last
  via forced compaction past the mark); ordering contract (compute-then-mark) has a dedicated
  Rust test proving mark-first would have lied. (The Swift funnel ordering is O-5's g3 test.)
- Perf: `bench_structured_diff` — 500 KB / 2k-block pair diffs in < 50 ms release.

## O-5 · History leaf + retention settings (#543) — PR 5

### Leaf registration

`Leaf` gains `case history` — title "History", symbol `.history` (table below), inserted in
`Leaf.registered` **before** `.citations` (usage-frequency order: content leaves, then history,
then citations/bibliography — and sync last once M-3 lands; the two milestones' registry edits
compose in either landing order). Persistence/unknown-token rules as in M-3. `leafContent` →
`HistoryLeaf()`.

### `HistoryLeaf.swift` — segment "This note"

- Segmented control (the `BibliographyPanel.segment` pattern): "This note" / "Deleted".
  Segment state per-vault-session (`@State`, not persisted — matches BibliographyPanel).
- No document → `LeafEmptyState("Select a note to see its history.")`.
- **Since-last-open section** (only when the pref is on AND result is `Diff`): `LeafSection`
  header "Since you last opened" + operation rows (below). `Unchanged`/`NoBaseline` render
  nothing; `BaselineCompacted` renders one caption row "Earlier changes have been compacted."
  AppState calls `changes_since_last_open` then `mark_opened` (the pinned order) in the note-open
  funnel, only when the pref is on.
- **Versions section**: `LeafSection` header "Version history, N versions" (`.isHeader`).
  First page of `list_versions` (limit 50) on note open; "Show older versions" row loads the next
  page; a generation-bump paging error silently reloads page one (the list is fresher, not wrong).
  Markers (`is_marker`) filtered out of the default list; a "Show markers" toggle in a
  section-header menu reveals them (they explain gaps: "Renamed from X").
  - Row identity = `position_from_tail` (hashes repeat across A→B→A histories; SwiftUI `ForEach`
    ids and the selection model use position, API calls use the row's hash).
  - Row content: absolute date+time (`DateFormatter` medium/short — **not** bare relative time;
    relative shown as secondary text) + `audio_fragment` + annotation chips (`display` strings).
    Row AX label: "\(formattedDate), \(audio_fragment)\(annotations)".
  - Row actions (buttons, keyboard-reachable): **Compare** (diff this version → current, rendered
    inline in a disclosure under the row) and **Restore…**.
  - Two-version compare: each row has a "Select for comparison" toggle (`.isToggle` AX trait);
    with exactly two selected, a "Compare selected versions" button appears in the section header
    (older position = from, newer = to). Selecting a third replaces the older selection.
- **Restore flow**: confirmation alert — title "Restore version?", message "Restore the version
  from <date>? This replaces the current content of <name>. The replaced state remains available
  in version history." Buttons: Cancel (default) / Restore (destructive style — the MainSplitView
  alert conventions). Passes the current document hash as `expected_content_hash`; a
  `WriteConflict` (buffer dirty or file changed) routes to the existing conflict-alert flow;
  `HistoryUnavailable` → specific error alert ("This version can't be restored: its history
  failed an integrity check."), nothing written. Success: announce "Restored version from
  <date>." (.high), document reloads through the normal changed-file path, the list refreshes,
  and focus moves to the **new head row** (position 0 — the restored state; the old row's
  position shifted, so "return focus to the row" is defined as the new head; WCAG 2.4.3).
- **Diff rendering** (shared by Compare / since-last-open): a flat list of operation rows —
  `kind` icon (SlateSymbol per class family) + `semantic_description` as the row text + optional
  `detail` as secondary text. Header row = `audio_summary`. Every row is a plain AX element (VO
  reads operations one at a time — the §7.3 sequential-walkthrough contract); **never** a
  side-by-side textual diff. Empty diff → "No differences." row.

### Segment "Deleted"

- Rows: "\(path)" + "Deleted \(relative time)" (or "Deletion time unknown") + size when
  recoverable. Row AX label: "\(path), deleted \(time), restorable" / "…, not restorable".
- Restore button per recoverable row → `recover_deleted_file`: success announces "Restored
  \(name)." and the file-tree refresh follows the standard mutation-announcement flow (U2-6);
  `DestinationExists` → alert "A file already exists at \(path). Rename or move it first, then
  restore." (Restore-as-copy is the filed follow-up.)
- Standing footer caption: "Files deleted before Slate saved them go to the system Trash."
- Empty state: `LeafEmptyState("No recently deleted files.")`.

### Compaction-error surfacing (the O-2 channel's Mac half)

AppState registers a `VaultEventListener` conformance at session open (uniffi callback interface);
`on_error(CompactionFailed, …)` presents a non-blocking alert following the MainSplitView alert
conventions, with the O-2 message verbatim. One alert per (path, session) — repeated failures on
the same file don't re-alert (gate like the announcement gates).

### Settings

`SettingsView`'s `TabView` (SettingsView.swift:28-38) gains a **History tab** (alongside
Math/Code/Bibliography):
- "Keep edit history for" picker: 30 / 90 (default) / 180 / 365 days.
- "Show changes since last open" toggle, default off, footer text "Adds a summary of what changed
  to the History panel when you open a note."
- Persistence: retention → `.slate/prefs.json` via new session API
  `set_history_prefs(HistoryPrefs { retention_days: u32 })` +
  `history_prefs() -> HistoryPrefs` (per-vault, read at session open into the runtime config;
  a live change applies to the compaction worker immediately). The since-last-open toggle is a
  host preference (`PreferencesStore`, key `slate.prefs.historyShowChangesSinceOpen`) — it drives
  UI + mark writes only, no core behavior.

### Commands (registry + menu + palette; drift test updated — same registry/menu rule as M-3)

`slate.history.showPanel` ("Show history panel" — activates the leaf), menu item in the **View
menu, `CommandSection.view`, following the workspace-tabs precedent (SlateCommands.swift:36)** —
the same normative home M-3's `slate.diagnostics.refreshSync` uses (cross-referenced in both
specs so the two PRs converge on one menu regardless of landing order). Row actions are not
commands (they need row context).

### Tests (XCTest)

- Segment/state matrix: no-document, empty history, populated, markers-toggle, deleted-empty,
  deleted-populated, since-open in all four `ChangesSinceOpen` states × pref on/off.
- Restore flow: confirmation copy, destructive styling, hash handoff (spy asserts
  `expected_content_hash` == document hash), conflict routing, `HistoryUnavailable` alert,
  announcement (via the injectable `AnnouncementPosting` seam — see M-3), focus lands on the new
  head row.
- Compare selection model: 0/1/2/3-selection transitions; older/newer orientation by position;
  duplicate-hash rows select independently (position identity).
- Diff rows: fixture StructuredDiff renders one AX element per operation, in order, labels equal
  `semantic_description`; no side-by-side anywhere (inspection assert).
- Deleted restore success + `DestinationExists` alert copy; tree refresh announcement fires.
- **Funnel ordering (g3):** an AppState-level test with a recording fake session asserts
  `changes_since_last_open` is invoked before `mark_opened` on note open when the pref is on,
  and neither when off.
- Compaction-error alert: fake listener event → alert with the exact copy, once per path.
- Settings: retention picker round-trips through prefs.json (integration via temp vault); toggle
  gates the section and the mark call.
- Appearance snapshots (both modes) for versions + diff + deleted; APCA on annotation chips and
  secondary text; `a11y-check` 100 at the PR tip.

## O-6 · Temporal query operators (#544) — PR 6 — **blocked on Milestone N**

### Derived index: `oplog_events`

```sql
-- Plain rowid table, NO uniqueness constraints: the append path inserts each event
-- exactly once, and the rebuild path recreates the table from scratch — dedup has no
-- producer. (A composite PK over a NULLable column would be non-enforcing in SQLite,
-- and same-millisecond events must NOT be swallowed.)
CREATE TABLE oplog_events (
  file_id      INTEGER NOT NULL,          -- files.id (rebound by scan reconcile)
  ts_ms        INTEGER NOT NULL,
  event_class  INTEGER NOT NULL,          -- 1=content_change 2=property_set 3=property_remove
                                          -- 4=task_toggle 5=fm_replace (pure markers: no rows)
  property_key TEXT,                      -- classes 2/3 only, else NULL
  deleted_text TEXT                       -- class 1 only: removed spans this save, concatenated,
                                          -- capped 4096 bytes (UTF-8 boundary-safe truncation);
                                          -- NULL when no old content was in hand
);
CREATE INDEX oplog_events_file_ts ON oplog_events (file_id, ts_ms);
CREATE INDEX oplog_events_ts ON oplog_events (ts_ms);
```

- Population on append: after a successful `append_save_to_oplog`, insert rows derived from the
  entry — one class-1 row iff `hash_before != hash_after`, one row per annotation (classes 2-5;
  `PathChanged` markers produce **no** rows). `deleted_text` (normative): the concatenated
  removed spans of the save's edit ops — available on BOTH the batch path and the cadence-snapshot
  path, because the session computes `diff_to_ops` before deciding the kind (session.rs:1001-1017);
  `NULL` when `old_contents` was `None` (cold cache / non-UTF-8 old) — a documented sampling gap:
  content deleted across a cold-cache save is not searchable. Insert failure = stderr warning,
  never fatal (regenerable).
- Population on rebuild: scan reconcile regenerates the table from the logs when it is empty or
  `parser_version` bumps — `DELETE` all rows, then derive per log (decode annotations + edit-op
  spans; snapshot-to-snapshot boundaries where ops aren't recorded contribute class-1 rows with
  `deleted_text NULL`).
- Anchors and markers (`hash_before == hash_after`) produce no rows — this is the "excludes
  touch-only events" requirement (`05` §8.9) falling out of the hash rule.

### Operators (surface syntax binds to N's shipped filter grammar; semantics fixed here)

| Operator | Meaning (now = query execution time) | SQL lowering |
|---|---|---|
| `oplog.has_change_since(D)` | ∃ event `class=1` with `ts_ms ≥ now−D` for the candidate file | `EXISTS (SELECT 1 FROM oplog_events e WHERE e.file_id = f.id AND e.event_class = 1 AND e.ts_ms >= ?)` |
| `oplog.has_property_change(key, D)` | ∃ event `class IN (2,3,5)` with matching `property_key` (class 5 matches any key) in window | analogous, `(e.property_key = ? OR e.event_class = 5)` |
| `oplog.deleted_content_matches(pat, D)` | ∃ event `class=1` in window whose `deleted_text` contains `pat` **ASCII**-case-insensitively | `e.deleted_text LIKE '%' ⎮⎮ ? ⎮⎮ '%' ESCAPE '\'` with `pat` LIKE-escaped |

- Duration grammar: `^([1-9][0-9]*)(h|d|w)$` (hours/days/weeks). Anything else →
  `InvalidQuery { message }` naming the operator and the expected grammar.
- `pat` is a substring, not a regex/glob. Case-insensitivity is **ASCII-only** (SQLite `LIKE`
  semantics); the reference census implements the identical rule, and the limitation is
  documented (unicode-aware matching is a filed follow-up with the regex variant).
- Composability: each operator is one `FilterCondition` combinable with every other filter via the
  standard combinators; they add no new result-set semantics (`QueryResultSet` unchanged).
- Known limit (documented): `deleted_text` is a 4 KiB sample per save — a pattern deleted inside a
  single larger removal can miss. The cap is a constant, revisited on tester evidence.

### Tests + censuses

- Population fixtures: each event class; anchor/marker exclusion; cadence-snapshot save with
  deletions → class-1 row WITH `deleted_text` (the ops-in-hand case); cold-cache save →
  `deleted_text NULL`; cap truncation at a UTF-8 boundary; same-millisecond set+remove of one key
  → two rows (nothing swallowed); rebuild-from-logs equals append-time population for the same
  vault (modulo the documented snapshot-boundary NULLs — the fixture pins exactly which rows
  differ and why).
- `census_temporal_operators_vs_reference` — random edit histories with **scripted timestamps**,
  built at the oplog layer (`append_entry` with constructed entries) and fed through the same
  event-derivation function the session uses (the derivation fn is public-in-crate precisely so
  the census can drive it without a session clock seam): each operator's SQL result equals a
  brute-force scan over the decoded logs, across random windows/keys/patterns (ASCII-case rule on
  both sides); `census_scale()`.
- N-integration (the milestone DoD line): the three operators run against Milestone N's Bases
  test corpus composed with tag/property/folder filters; results match reference.
- Bench: `bench_temporal_query_10k_files` — `has_change_since(7d)` over a 10k-file synthetic vault
  < 50 ms warm (one indexed SQL query; the bench pins that it stays one).

---

## SlateSymbol additions

| Role | v7 | fallback | PR |
|---|---|---|---|
| `.history` | `clock.arrow.trianglehead.counterclockwise.rotate.90` | `clock.arrow.circlepath` | O-5 |
| `.restore` | `arrow.uturn.backward.circle` | `arrow.uturn.backward.circle` | O-5 |
| `.compare` | `arrow.left.arrow.right` | `arrow.left.arrow.right` | O-5 |
| `.diffAdded` | `plus.circle` | `plus.circle` | O-5 |
| `.diffRemoved` | `minus.circle` | `minus.circle` | O-5 |
| `.diffEdited` | `pencil.circle` | `pencil.circle` | O-5 |

(Reuse `.properties`, `.tasksLeaf`, `.code` glyph roles for diff-class icons where they exist —
consistency rule DoD §B; the three `diff*` roles cover add/remove/edit tinting.)

## Follow-ups to file during O

- Restore-as-copy ("Restore As…") for occupied destinations — file with O-5 (`enhancement`).
- Swift new-note flow adopting `create_exclusive` (today's `save_text(path, "", nil)` can clobber
  an existing file on a name race) — file with O-3 (`bug`-leaning `enhancement`).
- Config-file + `.canvas`/`.base` history coverage — file when T/N route writes through the save
  seam (`enhancement`, cross-referenced to T/N).
- Version-list day-grouping / condensation UI — file with O-5, decided by tester question #2.
- `slate history <vault> <path>` CLI command — file against the M CLI (`enhancement`).
- Regex + unicode-case-aware variant of `deleted_content_matches` — file with O-6, decided by
  tester question #3.
- Additional temporal operators (`oplog.created_since`, `oplog.untouched_for`) — file with O-6.
- Broadening `VaultEventListener` toward the full `05` §4.4 sketch (file-change events, index
  progress) — file with O-2 (`enhancement`). The proposed image-OCR program (specs pending
  filing as `docs/plans/19_image_ocr/`) is a named second consumer: it needs non-error events
  (background OCR result committed → live accessibility-label refresh; quiet failure counts; GC
  prompt triggers), and its mid-session external-replacement trigger is exactly §4.4's
  file-change events. Keep the code enum + registration/dispatch additive so these arrive
  without reshaping the O-2 channel — two parallel callback channels is the failure mode.
- Image-OCR GC ↔ history retention alignment — the OCR program's destructive sidecar GC treats
  O-5's retention window (`retention_days`) as the shared "recoverable past" horizon: orphaned
  sidecars stay undeleted at least that long (a Slate-deleted image restored from the system
  Trash re-validates its orphan by content hash). File with the OCR program when it lands;
  noted here so O-5's settings copy frames retention as the vault-wide history horizon, not an
  oplog-only knob.
