# OCR Reconciliation State Machine

**Status:** 📝 Filed (2026-07-11) · **Milestone:** [PD — Accessible image OCR](https://github.com/coryj627/slate/milestone/35) · **Program:** [00_plan.md](../00_plan.md)

**Related specs:**
- [Accessible Image OCR & Description Storage](./storage_spec.md) (parent)
- [Embed Resolver Contract](./embed_resolver_contract.md)

---

## 1. Purpose

Gracefully handle every case where OCR "didn't happen" for an embedded image — on returning
to a note, or reopening a vault — without either **re-OCRing forever** or **silently never
retrying** work that should retry. "Didn't happen" is several distinct states that need
different handling; collapsing them is the core bug this spec prevents.

**Governing rule:** *Absence of a sidecar means "never attempted." Every other outcome —
text, empty, or failed — writes a durable marker.* The complement matters equally:
*presence of a sidecar means "attempted" — even when the cache has lost track of it.* This
is what makes reconciliation a fast, idempotent set-difference that only ever does
genuinely-pending work. (Text Extractor's gap was writing nothing for empty/failed
results, so it re-scanned them every launch.)

Reconciliation is keyed on the **existing files index**: the scanner already maintains
`files.content_hash` (BLAKE3) with a `(mtime, size, ctime)` fast path, so reconcile reads
hashes from SQLite — it never reads image bytes. (Bytes are read once, by the OCR worker,
when a queued image is actually processed.)

## 2. States

`status` below is the sidecar/cache enum from the storage spec §3.3
(`ok | empty | failed | failed_permanent`) — one field, not a boolean-plus-enum split.

| State | Detection | Meaning | Action |
|---|---|---|---|
| **Never attempted** | no sidecar, no cache row | ordinary backlog (pre-feature vault, synced-in image, quit before reaching it) | enqueue |
| **Ran — has text** | sidecar `status: ok`, non-empty body | done | skip |
| **Ran — empty** | sidecar `status: empty`, empty body | ran; image genuinely has no text (photo, logo, decorative) | skip |
| **Failed** | sidecar `status: failed`, `attempts: n` for the **current engine identity**, `last_attempt` | error/interrupt/decode failure | retry if `n < cap`; else → **Failed (permanent)** for that identity |
| **Failed (permanent)** | sidecar `status: failed_permanent`, current engine identity ∈ `failed_engines` | *this* engine exhausted its retries | skip; count + surface quietly |
| **Failed (foreign engine)** | `status: failed` / `failed_permanent`, current engine identity **∉** `failed_engines` | a *different* engine identity (another machine's shell, or an upgraded Vision) exhausted it | **eligible again:** fresh attempt cycle for this identity; prior failure provenance retained |
| **Done, sidecar missing** | cache row exists, backing sidecar gone | stores disagree (user cleaned / sync dropped / git op) | **sidecar canonical wins:** drop cache row, enqueue |
| **Done, cache row missing** | sidecar exists, no cache row (fresh machine, deleted `.slate/cache.sqlite`, sidecars synced in) | cache is behind canon | **adopt:** upsert row *from* the sidecar; **no re-OCR** |
| **Image changed** | note's embed now resolves to a `content_hash` with no record | edited image = new content identity | enqueue the new hash; the old sidecar ages into the GC orphan path (parent spec §5) |

The **adopt** row is the rebuild direction the governing rule promises: a machine that
receives `.imagedesc/` via sync (or loses its cache) must recover *processed* state from
disk, not re-run a vault's worth of OCR — and must honor synced-in `failed_permanent`
markers **for the engine identities they record** (foreign-engine markers re-enqueue
instead — see the row above).

**Failure state is engine-scoped; success state is not.** An engine identity is
`engine` + `engine_version` (e.g. `apple-vision/1.4`). `ok`/`empty` are properties of the
*image*: any engine's honest result stands until an explicit invalidation policy says
otherwise (the v1.5 lever). Failures are properties of an *engine identity*: a decode
failure on one machine's Vision must not permanently suppress OCR on a Windows shell —
or on next year's Vision — that could succeed. Retry-on-new-identity is bounded and cheap
because failures are rare and each identity gets only one `cap`-limited cycle, recorded in
`failed_engines` (§3).

Retry `cap` = 2–3 per engine identity. Distinguish *transient* (interrupted, offline) from
*hard* (decode failure) if cheap; even a plain attempt-count cap prevents the
infinite-retry pathology.

## 3. Markers (frontmatter / cache schema)

Recorded in the sidecar frontmatter and mirrored to the cache row (`image_text` table,
storage spec §3.4):

```
# Ran, has text
status: ok               # body = extracted text

# Ran, empty
status: empty            # body empty; still counts as "processed"

# Failed (retryable)
status: failed
attempts: 2                              # attempts by the identity below
last_attempt: 2026-07-10T00:00:00Z
failure_kind: transient   # or: hard

# Failed (permanent)
status: failed_permanent
attempts: 3
last_attempt: 2026-07-10T00:00:00Z
failed_engines: ["apple-vision/1.4"]     # identities that exhausted retries; grows, never shrinks
```

A later **success carries `failed_engines` forward** as provenance (the storage spec's
frontmatter keeps the field alongside `status: ok`), so "Vision 1.4 couldn't read this,
Vision 1.5 could" survives in the record instead of being erased by the win.

Cache row (keyed by content hash) carries kind-scoped mirrors — `ocr_status`,
`desc_status`, `attempts`, `last_attempt` — plus provenance (`engine`, `engine_version`).
"Processed" (the old `has_ocr` / `has_description` notion) is **derived**:
`status ∈ {ok, empty}`. Searchable text exists only when `status = ok`. The description
pass reuses this machine verbatim with its own `kind: description` sidecar and
`desc_status` column.

The mtime/size skip data lives in the `files` table already (§1) — it is **not** part of
this feature's schema. The cache is **disposable**: on any sidecar-vs-cache disagreement,
the sidecar wins.

## 4. Triggers

- **Note save** → reconcile **that note eagerly**. Hook: `save_text` already refreshes the
  saved file's index rows in one transaction; reconcile runs against the fresh snapshot
  immediately after (the note in front of the user is made correct first). This trigger
  covers Milestone O's history operations for free: `restore_version` and
  `recover_deleted_file` route through the standard `save_text` machinery *verbatim*
  (O plan decision #5 — one write path), so a restored or recovered note reconciles like
  any other save. The flip side is an invariant worth stating: **a writer that bypasses
  `save_text` bypasses reconcile too** — new write paths must route through the seam
  (the same U3-5 one-write-path rule O leans on).
- **Vault open** → the open scan refreshes `files.content_hash` for anything that changed
  (fast path skips the rest); then walk `.imagedesc/` once to repair the cache in **both**
  directions (drop-and-enqueue *and* adopt, §2); then enqueue the pending set as
  **low-priority background work** — drain incrementally, interruptibly; never block the
  active note on the backlog.
- **Sync activity** → the existing `SyncMarkerWatcher` signal (and any future sync events)
  re-runs the both-directions repair, because sidecars may have arrived or vanished
  underneath us (parent spec §5, sync safety).
- **Mid-session external replacement:** Slate has no general vault file-watcher today, so
  an image *replaced on disk* with no note edit is not re-indexed until the next
  open/scan. The stale-index window must never become a stale **label**: the renderer
  shows the *current* disk bytes (`read_attachment` reads fresh), so serving OCR keyed on
  the *old* indexed hash would caption the new image with the old image's text. Rule:
  every consumer of `files.content_hash` (label lookup, enumeration) **stat-verifies the
  row first** — the same `(mtime, size, ctime)` predicate as the scan fast path, and the
  render path already stats the file for the attachment size cap, so the check is free.
  On mismatch the hash is unverifiable: serve the *pending* label (§7), refresh that one
  file's row (targeted re-hash), reconcile it, and let live update (§7) deliver the honest
  label. What v1 accepts is only that OCR of the *new* bytes starts at that moment rather
  than at the disk event. The concrete future hook already exists on Milestone O's books:
  the follow-up broadening `VaultEventListener` toward `05` §4.4 **file-change events**
  (storage spec §3.7) — when that lands, it plugs in as a fourth trigger; nothing else
  changes.

## 5. Reconcile algorithm (set difference, not re-scan-and-redo)

For the note (save trigger) or the vault (open trigger):

1. **Enumerate embedded images** via the resolver contract's `list_image_embeds`
   (resolver spec §6) — remote / non-image / unresolved / svg / oversized are already
   filtered out with reasons, and nested embeds (depth ≤ 3) are included.
2. **Key on `content_hash` from the enumeration** — supplied from the files index and
   **stat-verified** against disk (§4); a mismatched target gets its row refreshed (a
   one-file re-hash) before branching. No other byte reads, no vault-wide hashing in this
   pass.
3. **Branch on state** (§2):
   - cache row present → act per its state (skip / retry / drop-and-enqueue).
   - cache miss → **consult disk before enqueueing**: a sidecar for that hash means
     adopt, not re-OCR. (The vault-open repair walk primes the cache so per-note saves
     almost never hit this disk check.)
   - **downgrade conflict** → the cache remembers `ok` but the (synced-in) sidecar says
     `empty`/`failed`: evidence of a sync race, not of truth. Re-enqueue a fresh local
     run instead of silently adopting the downgrade — the run re-establishes `ok`, or
     earns the downgrade honestly. The sidecar stays canonical about *what was
     attempted*; content conflicts resolve by re-verification. The vault converges to
     `ok` because machines that can succeed rewrite it and machines that can't stop
     retrying (engine-scoped failures, §2).
   - no record anywhere → enqueue.
   - failed with `attempts < cap` for the current engine identity → enqueue (increment on
     the next attempt).
   - `failed_permanent` for the current engine identity → skip + count; for a **foreign**
     identity → enqueue (§2, foreign-engine row).
4. **Process the queue** — the queue is a **hash-keyed set** (two notes embedding the same
   image enqueue it once). Recognition runs in the shell off the main actor; **per-image
   commit** (atomic sidecar write + cache upsert in one core call per image; quit at
   900/1200 → 900 durably done). Commit is **check-then-write**: re-read the on-disk
   sidecar at commit time and never overwrite a `status: ok` sidecar with a
   `failed`/`empty` result — drop the local downgrade, recording its identity in
   `failed_engines` provenance only. Success commits carry the existing `failed_engines`
   list forward (§3).

**Properties:**
- **Idempotent** — running twice does nothing the second time.
- **Content-keyed** — an image that moved, got re-embedded elsewhere, or whose note was
  renamed is still recognized as done; no spurious re-OCR. An image whose *bytes* changed
  is a new key: enqueued fresh, old record orphaned (§2, image-changed row).
- Reconcile = "make the cache reflect reality," where reality = *what's embedded* ∩ *what's
  on disk*, with the **sidecar as arbiter**.

## 6. Performance

- **Hash-skip fast path: already shipped.** The scanner skips re-hashing when
  `(mtime_ms, size_bytes, ctime_ms)` match the indexed row — the ctime axis also catches
  mtime-preserving writers (`cp -p`, `rsync -a`, snapshot restores) that a bare
  mtime+size cache would miss. This feature adds **no** hashing layer of its own; a
  returning user's reopen re-hashes nothing and reconcile is a SQLite set-difference.
- **Lazy drain.** Save-trigger makes the current note correct now; open-trigger enqueues
  the rest as interruptible background work.
- **Per-image commit** everywhere — never treat a batch as atomic.
- The vault-open `.imagedesc/` repair walk is one directory listing + frontmatter reads
  for files the cache doesn't already mirror; it is not an OCR pass and does not read
  image bytes.

## 7. Graceful degradation (accessibility label fallback)

The integration point is the existing `EmbedView.imageEmbedTitle` contract: the author's
alt text is the AT description (WCAG 1.1.1, #419); empty/whitespace alt collapses to the
filename (audit #198). Today the no-alt case dead-ends at the filename — that is the slot
OCR fills. The label must be **honest and useful**, never silence and never a stale lie.
The store is consulted **only under a freshness-verified hash** (§4); an unverifiable
hash renders as the *pending* row below, never as another image's text.

Total mapping — every store state × alt presence:

| Store state for the hash | No author alt | Author alt present |
|---|---|---|
| pending (never attempted / enqueued) | "image — text not yet extracted" (transient, informative; an unlabeled image is a dead end) | alt (unchanged) |
| `ok` | **OCR text** (replaces the filename dead-end) | alt stays the primary label; OCR text attaches as **secondary content** (e.g. `AXCustomContent` / help), never displacing author intent |
| `empty` | filename + "no text detected" — extraction *finished*; saying "not yet extracted" here would be the stale lie this section bans | alt |
| `failed` / `failed_permanent` | filename (failure surfaced quietly in counts/diagnostics, not shouted per-render) | alt |

An earlier draft's priority list ("1. alt, 2. transient, 3. OCR") left the alt-AND-OCR
case ambiguous — read literally, OCR would never surface for captioned images. The table
above is the resolution: **author alt always wins the primary label; OCR augments,
pending-state only ever shows where extraction is genuinely still possible.**

**Live update:** when a background OCR finishes and writes its sidecar, the *open*
preview's label updates in place — it must not wait for a reopen. Constraints: the update
must not move VoiceOver focus or interrupt in-progress speech (silent attribute update;
if the focused element is the image, re-announce politely), and batch completions must
coalesce rather than announce per-image. (Description text, when present in v1.5, layers
into the same slots per product decisions.)

## 8. Adversarial acceptance tests

Three sync/staleness attacks this machine must survive (from adversarial review), shipped
as tests alongside the state-machine suite:

1. **External same-session replacement.** Replace `diagram.png`'s bytes on disk
   mid-session (no note edit), re-render the note: the label must degrade to "text not
   yet extracted" — never the old image's OCR text — then live-update to the new bytes'
   text after the targeted refresh + OCR (§4).
2. **Synced permanent failure, different engine.** Sync a `failed_permanent` sidecar
   recorded by `windows-ocr/…` onto a Vision machine: reconcile must re-enqueue (identity
   not exhausted here), and a success must rewrite the sidecar to `ok` with the foreign
   failure provenance retained in `failed_engines` (§2, §3).
3. **Delayed older-engine sidecar arrival.** With a local `ok` in cache, deliver via sync
   an `empty`/`failed` sidecar for the same hash+kind: the downgrade must reach neither
   the label nor search; a fresh local run re-establishes `ok`. Run the cycle twice and
   assert convergence, not flip-flop — the failing identity stops retrying, and `ok` is
   terminal (§5, downgrade conflict).

## 9. State transition diagram (text)

```
                        ┌─────────────────┐
   image embedded  ───▶ │ Never attempted │ (no sidecar, no row)
                        └────────┬────────┘
                                 │ enqueue → OCR run
                 ┌───────────────┼───────────────┐
                 ▼               ▼               ▼
          ┌────────────┐  ┌────────────┐  ┌────────────┐
          │ status: ok │  │status:empty│  │  Failed    │
          │ (skip)     │  │ (skip)     │  │ attempts++ │
          └────────────┘  └────────────┘  └─────┬──────┘
                                                 │ attempts < cap → re-enqueue
                                                 │ attempts ≥ cap
                                                 ▼
                                        ┌──────────────────┐
                                        │ Failed permanent │ (skip + count — engine-
                                        └──────────────────┘  scoped: a NEW engine
                                                              identity re-enters the
                                                              retry cycle, §2)

   stores disagree — repair, both directions:
          ┌───────────────────────┐  drop row +
          │ Done, sidecar missing │ ─ re-enqueue ─▶ back to "Never attempted"
          └───────────────────────┘  (sidecar canonical wins)
          ┌───────────────────────┐  upsert row from sidecar
          │ Done, row missing     │ ─── adopt ────▶ back to its sidecar's state
          └───────────────────────┘  (NO re-OCR; honors failed_permanent)

   image bytes edited: new content_hash = new identity
          → new hash enters at "Never attempted"; old sidecar → GC orphan path
```

## 10. Summary (one line)

Absence of a sidecar = never attempted, presence = attempted (adopt it); text, empty, and
failed each write one durable `status` marker — failures scoped to the engine identity
that earned them, hashes trusted only when stat-verified against disk — so reconciliation
is a fast, idempotent, content-keyed set-difference over the existing files index that
does only genuinely-pending work, never serves stale or downgraded labels, and converges
to `ok` across machines — current note eagerly on save, backlog lazily in the background —
with an honest, alt-respecting accessibility-label fallback that updates live (and
politely) when results land.
