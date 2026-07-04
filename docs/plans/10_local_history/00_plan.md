# 10 — Milestone O plan: Local history + change tracking

**Status:** 📝 Planned (2026-07-03). Not started. GitHub [milestone 15](https://github.com/coryj627/slate/milestone/15).
**Implements:** `05_locked_architecture_decisions.md` §7.5 (op-log persistence + compaction), §9.3.3 (compaction at scale, hard constraints), §8.9 (op-log-aware temporal queries), the V1 half of §7.3 (structured-diff plumbing, no conflict UI), and the "Local history and recovery" workstream from `03_phase_1_plan.md` §11.
**Executable spec:** [o_spec.md](o_spec.md).

**Goal.** A tester recovers prior versions of any note from Slate's local history, recovers recently deleted files, sees "what changed since I last opened this note" as a structured, screen-reader-first diff, and (once Bases ships) runs op-log-aware temporal queries.

---

## What already exists (why this milestone is smaller than it reads)

Milestone F + the #404 buffer work shipped far more than "op log v0":

- Per-file append-only logs at `.slate/oplog/<files.id>.oplog` with a versioned 8-byte header,
  length-prefixed checksummed records, torn-write recovery to a well-formed prefix, and locked
  concurrent appends (oplog.rs).
- **Fine-grained edits already recorded:** each save appends an `EditBatch` of `Insert`/`Delete`/
  `Replace` ops (the minimal diff old→new via `diff_to_ops`), chained by before/after blake3 hashes,
  with `WholeFileReplace` snapshot anchors emitted on a byte-budget cadence
  (`oplog_compaction_threshold_bytes` already drives *snapshot cadence* today).
- Reconstruction: `reconstruct_at_tail` and `reconstruct_at_hash` exist and are load-bearing
  (U2's `undo_structural` restores file bytes through them).
- `SessionConfig` already carries the three knobs: `oplog_compaction_threshold_bytes` is **live
  today** (it drives the snapshot cadence above); `oplog_compaction_threshold_entries` and
  `oplog_retention_days` are reserved/unenforced. No compaction/retention *execution* exists.
- `read_oplog` is FFI-exposed (entries with opaque payloads).

What does **not** exist: retention/compaction execution, a version-history API, deleted-file
recovery, `StructuredDiff`, per-op FFI accessors, temporal query operators, and any UI.

## Scope decisions (locked for this milestone)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **A "version" is a save-point, not a keystroke.** | The GH milestone text says "edit-keystroke resolution"; this plan supersedes it. Each save already records the minimal `EditBatch` diff — for ordinary typing that *is* the coalesced keystroke stream. Appending + fsyncing per keystroke would put disk IO on the keystroke path, violating the #404 budget (sub-ms, flat) for zero user-visible gain: restore/diff/temporal queries all operate on save-points anyway. |
| 2 | **Semantic operations are annotations on save entries** (new op kind), recorded only where the write path knows intent: `set_property`, `delete_property`, `set_frontmatter_source`, `toggle_task_status`, rename/move (`PathChanged`). Free-typed edits get their semantic classification at **read time** from the structured-diff engine. | `05` §7.4 sketches `InsertHeading`/`MoveListItem` as logged ops, but no write path carries that intent — headings are typed. Write-time annotations where intent exists; derived semantics where it doesn't. The user-facing contract (§7.3: "Added heading 'Goals' at line 10") is met by the diff engine either way. |
| 3 | **Fix op-log identity before building UI on it** (O-1). Logs are keyed by `files.id`, which lives in the *regenerable* cache — deleting `cache.sqlite` today orphans or (worse) mis-associates every log on rebuild, and `files.id` is a **reusable rowid** (no `AUTOINCREMENT`; delete-then-create recycles ids — the hazard session.rs:501-507 already documents), so a new note can silently inherit a dead note's log. O-1 adds a v2 header carrying the vault-relative path, collision-proof log naming decoupled from `files.id`, `PathChanged` annotations on rename/move, a `files.oplog_name` binding column, and scan-time reconciliation. | Version history shown to users must never attach another file's history. This also makes deleted-file detection fall out naturally: an op log that reconciles to no live file *is* a deleted file. |
| 4 | **Version identity = `content_hash_after`, verified on materialization.** Every reconstruction that serves a version (`version_content`, restore, diff, recovery) re-hashes its output and refuses (`HistoryUnavailable`) on mismatch — wrong bytes are never served. Row identity in lists is the log position (hashes repeat across A→B→A histories). | Compaction shifts indices; hashes survive it. The "same hash ⇒ same bytes" property is *enforced* by the verification step (and by O-1's marker-hash rule), not assumed. |
| 5 | **Restore appends; history is never rewritten.** Restore materializes the chosen version and routes it through the standard `save_text` machinery (atomic temp+rename, conflict detection via expected hash, index refresh, its own op-log entry). | One write path (the U3-5 rule); an undo of a restore is just another restore. |
| 6 | **Two derived SQLite tables are added** (`oplog_events` for temporal queries, `open_marks` for changes-since-last-open), both regenerable from the logs / droppable without data loss. | The GH milestone text said "no new tables"; this plan supersedes it. Temporal queries cannot scan thousands of binary logs per query — `05` §9.2's rule is that SQLite is the regenerable index over filesystem truth, which is exactly what these are. Losing `open_marks` on a cache rebuild degrades "since last open" to "no baseline yet" — honest and harmless. |
| 7 | **UI is one right-pane leaf** (`Leaf.history`) with two segments: "This note" (versions, compare, restore, since-last-open) and "Deleted" (recovery within retention). Retention lives in a new Settings **tab**; "changes since last open" is opt-in, default off. | Post-U surface rules (same reconciliation as Milestone M decision #1). Segments follow the `BibliographyPanel.segment` precedent; 12 rail icons (10 today + M's sync + this) is within the Obsidian-parity envelope. |
| 8 | **Temporal query operators are the last slice (O-6) and are hard-gated on Milestone N.** Everything else in O is N-independent. | The operators' DoD ("land in the same `SlateQuery` AST, run against N's corpus") is only meetable after N ships. O can start before N and close after it; the `oplog_events` design here fixes the semantics so O-6 is a thin integration. |
| 9 | **Coverage now: `.md` files** (everything the save paths touch). `.canvas` / `.base` history hooks in automatically when T / N route their writes through the same save seam; config-file snapshots are a follow-up. | `03` §11 lists canvas/base/config, but those editors don't exist yet. The seam — not per-type code — is the deliverable. |

## Issue map

| ID | Issue | Track | Depends on | Labels |
|----|-------|-------|-----------|--------|
| O-1 ([#539](https://github.com/coryj627/slate/issues/539)) | Op-log v2: durable identity (header path, `PathChanged`, reconcile) + semantic annotations | Rust | — | `backend`, `schema` |
| O-2 ([#540](https://github.com/coryj627/slate/issues/540)) | Compaction + retention: background job, atomic rewrite, race-safe locking, remnant reclamation, and the `VaultEventListener` error channel (built here — `05` §4.4 is an unimplemented sketch today) | Rust | O-1 | `backend` |
| O-3 ([#541](https://github.com/coryj627/slate/issues/541)) | Version-history + deleted-file-recovery session APIs (+ uniffi) | Rust | O-1 (not O-2) | `backend` |
| O-4 ([#542](https://github.com/coryj627/slate/issues/542)) | StructuredDiff engine + changes-since-last-open | Rust | O-3 | `backend` |
| O-5 ([#543](https://github.com/coryj627/slate/issues/543)) | History leaf + retention settings (Mac UI) | Swift | O-2, O-3, O-4 | `swift-ui`, `a11y` |
| O-6 ([#544](https://github.com/coryj627/slate/issues/544)) | Temporal query operators (`oplog_events` + `oplog.*` filters) | Rust | O-1, **Milestone N** | `backend`, `blocked` until N |

```
O-1 ──▶ O-2 ──────────────┐
  └───▶ O-3 ──▶ O-4 ──────┴──▶ O-5
  └───────────────────────────▶ O-6  (waits for N)
```

One PR per issue. O-2 and O-3 can run in parallel worktrees after O-1.

**Issue-body contract** (the U-program convention, `08_ui_parity/00_program.md` §DoD): each filed issue body carries (a) a link to its spec section in [o_spec.md](o_spec.md), (b) the issue's condensed DoD checklist — the spec's test/census list for that issue verbatim, as checkboxes, (c) the labels from the map above, and (d) its dependency edge ("blocked by O-1"; O-6 additionally "blocked by Milestone N").

## Relationship to other milestones

- **N (Bases):** O-6 only. `oplog_events` semantics are fixed in this spec; O-6's surface syntax
  binds to N's shipped filter grammar.
- **M (Sync/CLI):** none. (`slate history` CLI command is a filed follow-up, not scope.)
- **T (Canvas):** when canvas writes route through the composed-save seam they inherit history for
  free; O adds no canvas-specific code.
- **V2 conflict resolution (`05` §7.3):** O ships the `StructuredDiff` type, the diff engine, and
  the op-level semantic descriptions — the data plumbing. Per-operation *resolution* UI stays V2.
- **W (Windows, parked):** the whole milestone is slate-core + one Mac leaf; nothing Mac-bound
  leaks into core types (the portability review discipline).

## Definition of done (milestone)

- Every `.md` file edited in Slate has restorable save-point history within retention, **subject
  to the `05` §7.5 size caps**: a file exceeding 10k entries / 5 MiB of log inside the window has
  its oldest save-points folded into a snapshot (documented; hot-file thinning is the locked §7.5
  behavior, not a bug). Restore is atomic, conflict-detected, integrity-verified, and itself
  versioned. Byte-equality integration test: edit across sessions, restore a mid-sequence version,
  `read_text` equals the bytes at that point.
- Cache-rebuild honesty: delete `cache.sqlite`, rescan → every live file's history reattaches;
  deleted files within retention appear in the Deleted list; a recycled `files.id` (delete newest,
  create new) never attaches the dead file's history. (All census-gated.)
- No version operation ever serves bytes whose hash doesn't match the requested version —
  integrity verification refuses with a typed error instead. (Census + corrupted-chain fixture.)
- Compaction meets §9.3.3: 50k-op file compacts < 1s in the background without blocking the
  editor; failure is a user-visible error; synthetic 5-years-of-daily-editing workload passes at
  the release gate. Append/compact races lose no entries (census-gated).
- Diff reads as named operations on VoiceOver ("Added heading 'Goals' at line 10"), never a
  side-by-side text dump; restore confirmation announces destination + destructive nature.
- Temporal operators (O-6) return results identical to a brute-force log-scan reference on the
  census corpus, and run against Milestone N's Bases test corpus.
- `a11y-check` 100 at O-5's tip; APCA Lc ≥ 75 on new UI text pairs, both appearances; benchmarks:
  keystroke/save paths unchanged vs #404 baselines (append overhead with annotations measured, in
  the PR).

## Out of scope (deferred)

- Conflict-resolution UI (V2 — `05` §7.3; the diff plumbing ships here).
- Sync replication of op logs (never — local-only state per `05` §7.5).
- CRDT migration (V3+ per `05` §7.1).
- Keystroke-granularity logging (decision #1).
- `.canvas`/`.base`/config-file history (decision #9 — seam only), visual time-machine polish,
  `slate history` CLI, restore-as-copy UX (follow-up filed by O-5).

## Tester feedback questions (carried from the GH milestone)

1. Is "changes since I last opened" useful, or noisy enough that it stays off?
2. Version list defaults: every save, or daily condensation? (Drives a possible grouping layer on
   top of the log — the API already supports either.)
3. Which temporal operators do you actually reach for? (Prioritizes operators beyond the three.)
4. Retention default — is 90 days too short / too long?
