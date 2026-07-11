# 19 — Milestone PD plan: Accessible image OCR

**Status:** 📝 Planned (2026-07-11). Not started. GitHub [milestone 35](https://github.com/coryj627/slate/milestone/35).
**Executable specs:** [specs/embed_resolver_contract.md](specs/embed_resolver_contract.md) · [specs/storage_spec.md](specs/storage_spec.md) · [specs/reconciliation_spec.md](specs/reconciliation_spec.md) — grounded against the shipped core (2026-07-10) and hardened by an adversarial challenge review (2026-07-11: stale-label freshness gate, engine-scoped failure state, converge-to-`ok` sync policy) before filing.
**Inherits:** the UI-parity Presentation-Ready Definition of Done (`../08_ui_parity/00_program.md` §A–§G) — a11y-check 100/100, APCA Lc ≥ 75 both appearances, census-gated invariants, atomic writes, one PR per issue.

**Goal.** A VoiceOver user hears the text *inside* an embedded image announced inline while
reading a note; every user finds notes by text that exists only inside images — from one
content-addressed store, without Slate ever mutating a note. v1 is the free, on-device OCR
pass (Apple Vision); the paid description pass (VLM) is a v1.5 seam this design leaves open,
not a v1 deliverable.

---

## What already exists (why this milestone is smaller than it reads)

The specs were written against the shipped core, and most of the proposal's machinery turned
out to already exist:

- **Resolution:** `links::extract_links` (both embed grammars, alias→display_text, anchors),
  `embeds::looks_like_image` / `IMAGE_EXTENSIONS`, `link_resolver::resolve_link` (precedence +
  distance-then-alphabetical tiebreak, census-locked, U2-3 seed 164), and
  `session::resolve_image_embed` / `read_attachment` (bytes + MIME + per-occurrence alt,
  50 MiB cap, depth-3 nested embeds).
- **Content addressing:** `files.content_hash` is a BLAKE3 hash for *every* indexed file,
  maintained by the scanner with a `(mtime, size, ctime)` fast path. No new hashing layer.
- **Store substrate:** `.slate/cache.sqlite` (numbered migrations, FTS5 per `006_fts5.sql`,
  delete→rebuild contract, cross-process one-writer locking) and the `.slate/tmp` atomic-write
  discipline (`sync_data()` before rename).
- **The label seam:** `EmbedView.imageEmbedTitle` (WCAG 1.1.1, #419/#198) — author alt is the
  AT description; the no-alt case dead-ends at the *filename* today. That dead end is the slot
  this milestone fills.
- **Invisibility for free:** the scanner skips dot-prefixed entries, so the `.imagedesc/`
  sidecar folder stays out of the note tree, graph, index, and search with zero changes.

## Scope decisions (locked for this milestone)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **OCR resolution ≡ render resolution.** Enumeration routes through the shared resolver; a golden test pins that `list_image_embeds` returns exactly what the preview resolves. Obsidian-parity gaps (`%20`, `../`, `./`, size-alias, NFC — resolver contract §5.1) are fixed **upstream in the links layer or not at all**, never OCR-locally. | A private re-implementation that drifts by one tiebreak rule produces *wrong* labels — worse than none. |
| 2 | **Key = `files.content_hash`, stat-verified at consumption.** Every consumer (label lookup, enumeration) re-checks the scan fast-path predicate before trusting the row; a mismatch serves *pending* and triggers a one-file re-hash. Oversized files carry a sentinel (empty-body) hash and are **OCR-ineligible**. | A stale index must degrade to "pending," never to another image's text (adversarial finding 1); the sentinel would otherwise collide every oversized image into one record. |
| 3 | **Canon = `.imagedesc/` sidecars; speed = cache tables.** Sidecars (one per image × pass, body = raw payload, no in-band structure) are durable derived artifacts that travel with the user's sync/git. The mirror is a migration in the existing `.slate/cache.sqlite` — **not GRDB, not a second database** — covered by the locked delete→rebuild and sync-ignore contracts. Not under `.slate/` because the locked layout defines that folder as local-only + disposable. | Descriptions cost money to regenerate; caches don't. Splitting canon from speed follows the layout doctrine (`05` §9.2). |
| 4 | **Engine in the shell, everything else in core.** Apple Vision runs in `slate-mac`; the queue, store, reconcile, and search live in `slate-core` behind uniffi. Engine identity (`engine/engine_version`) is recorded on every result. | Keeps Windows (Milestone W, parked) honest — `Windows.Media.Ocr` becomes another engine identity writing into the same store, no schema change. |
| 5 | **One status enum, engine-scoped failures.** `ok \| empty \| failed \| failed_permanent`; failures bind to the engine identities in `failed_engines` (grows, never shrinks; carried forward on success). A *different* identity retries a foreign `failed_permanent`; `ok`/`empty` stand until an explicit invalidation policy (v1.5). | One engine's decode limitation must never permanently silence an image everywhere (adversarial finding 2); success-churn on every engine update would be a re-OCR storm. |
| 6 | **No blind last-write-wins.** Slate's writer is check-then-write (never overwrite `ok` with `failed`/`empty`); the adopt path re-enqueues downgrade-shaped conflicts instead of trusting them. File-level conflicts belong to the user's sync tool; within Slate's control the vault **converges to `ok`**. | A delayed sync from an older engine must not clobber good text into labels and search (adversarial finding 3). |
| 7 | **Author alt always wins the label.** OCR fills the no-alt slot (today's filename dead-end) and attaches as secondary content when alt exists; pending/empty/failed states get honest, distinct copy (total state × alt mapping, reconciliation spec §7). Live updates never move VoiceOver focus and coalesce across batch completions. | WCAG 1.1.1 contract (#419) is author-intent-first; "text not yet extracted" after extraction finished would be the stale lie the spec bans. |
| 8 | **Search rides the existing pipe.** `image_text_fts` surfaces through `full_text_search` / `QueryResultSet` with the *image's* vault path as the hit path — SearchOverlay, reading-view search, and the `slate` CLI gain image-text hits with no new result shape. (Union-vs-source-flag is PD-6's one open decision; hits are attributed as image text either way.) | One search surface; `BasesResultSet` and the locked `05` §8.4 shape stay untouched. |
| 9 | **Bounding boxes are not persisted in v1.** The sidecar body stays raw text; only a mean-confidence scalar is recorded. Region persistence is decided with the v1.5 navigable-regions feature that would consume it. | Frontmatter is the only rebuild-surviving home and hundreds of boxes per image bloat every sidecar for an undesigned feature. |
| 10 | **Ride Milestone O's infrastructure** (storage spec §3.7): the `VaultEventListener` channel (O-2, broadened by [#802](https://github.com/coryj627/slate/issues/802) — PD is its named second consumer), O-2's background-worker discipline for the drain, O-5's `retention_days` as the GC destructive-tier horizon, and O-3's restores auto-covered because they route through `save_text`. | Two parallel callback channels / worker patterns / retention horizons is the failure mode; O's follow-ups already name this program. |

## Issue map

| ID | Issue | Track | Depends on | Labels |
|----|-------|-------|-----------|--------|
| PD-1 ([#804](https://github.com/coryj627/slate/issues/804)) | Embed enumeration API: `list_image_embeds` + eligibility + stat-verified hashes (+ uniffi) | Rust | — | `backend` |
| PD-2 ([#805](https://github.com/coryj627/slate/issues/805)) | Sidecar store (`.imagedesc/`) + cache migration (`image_text`, `image_text_fts`) + rebuild/adopt | Rust | PD-1 | `backend`, `schema` |
| PD-3 ([#806](https://github.com/coryj627/slate/issues/806)) | Reconciliation engine: set-difference, triggers, engine-scoped states, downgrade guards, queue | Rust | PD-2 | `backend` |
| PD-4 ([#807](https://github.com/coryj627/slate/issues/807)) | Vision OCR worker (shell drain) + backlog prompt UX | Swift | PD-3 | `swift-ui` |
| PD-5 ([#808](https://github.com/coryj627/slate/issues/808)) | Accessibility label integration + live updates | Swift | PD-3, PD-4 (soft: O-2 [#540](https://github.com/coryj627/slate/issues/540)/[#802](https://github.com/coryj627/slate/issues/802)) | `swift-ui`, `a11y` |
| PD-6 ([#809](https://github.com/coryj627/slate/issues/809)) | Search surfacing: FTS union/attribution + SearchOverlay + CLI | Rust | PD-2 | `backend` |
| PD-7 ([#810](https://github.com/coryj627/slate/issues/810)) | GC + retention + settings (two-tier, retention horizon, prefs) | Rust + Swift | PD-3 | `backend`, `swift-ui` |

```
PD-1 ──▶ PD-2 ──▶ PD-3 ──▶ PD-4 ──▶ PD-5
           │        └─────────────▶ PD-7
           └──────────────────────▶ PD-6
```

## Cross-milestone alignment

- **O (local history, in flight):** the four shared-infrastructure points in decision #10;
  sequencing rule from storage spec §3.7 — whichever program lands its channel/worker second
  extends the other's pattern. PD-5's live update *prefers* O-2's `VaultEventListener`; if PD
  reaches that point first, PD builds the minimal channel (`ScanProgressListener` precedent)
  and [#802](https://github.com/coryj627/slate/issues/802) extends it.
- **W (Windows, parked):** decision #4's engine seam is the deliberate slot; nothing else.
- **N (Bases):** no dependency either way — decision #8 keeps result shapes shared-but-untouched.
- **T (canvas):** explicitly *not* this milestone's surface; images on canvas cards inherit the
  store transparently if/when canvas rendering consumes embed labels.

## v1 non-goals (the v1.5 seam)

Descriptions (paid VLM — per-vault, explicit privacy opt-in), remote-embed fetch-materialize,
AI visual dedupe, write-alt-text-to-embed portability command, SVG text extraction,
bounding-box persistence. Each is a documented seam in the specs, none is v1 work.

## Follow-ups to file during PD

- Link-resolver Obsidian-parity gaps as individual upstream issues (`%20` percent-decoding —
  highest value; `../` + `./` note-relative markdown paths; size-alias-as-alt-text; NFC
  filename folding) — file with PD-1, labels `backend`, cross-referenced to the resolver
  contract §5.1 table.
- Backlog-prompt estimate calibration (live per-vault measurement UX) — file with PD-4 if the
  first-N-images estimator proves noisy.
- Record the PD-6 union-vs-source-flag decision as a `05` §8.4 addendum once made.
- GC ↔ history-retention alignment is already recorded on O's books (o_spec follow-ups) and
  in decision #10 — no separate issue beyond PD-7.
