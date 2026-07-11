# Accessible Image OCR & Description Storage

**Status:** 📝 Filed (2026-07-11) · **Milestone:** [PD — Accessible image OCR](https://github.com/coryj627/slate/milestone/35) · **Program:** [00_plan.md](../00_plan.md)

**Related specs (one linked set):**
- [Embed Resolver Contract](./embed_resolver_contract.md)
- [OCR Reconciliation State Machine](./reconciliation_spec.md)

---

## 1. Summary

Make text *in* embedded images available to Slate as (a) an accessibility label in
markdown preview and (b) searchable content, without ever mutating the user's notes.
Two passes produce this content:

- **OCR** — printed/rendered text extracted from the image (free, v1).
- **Description** — a full natural-language description of the image (paid, v1.5).

Both are stored as canonical markdown sidecars in a `.imagedesc/` vault dotfolder and
mirrored into the existing disposable cache database (`.slate/cache.sqlite`) for fast
lookup and full-text search. The user's notes are never edited; Slate resolves each embed
live by content hash.

## 2. Motivation

Embedded images are opaque to a screen reader unless something surfaces their content.
The Obsidian plugins that do this (Text Extractor + Omnisearch) only store OCR text in a
search index, so an image is "accessible" solely via search — never in the reading view's
accessibility tree. Slate's goal is to feed the same extracted content into the rendered
image's accessibility label so it is announced inline while navigating a note, and into
search, from one content-addressed store.

Slate already has the missing half Obsidian plugins lack: `EmbedView.imageEmbedTitle`
(the WCAG 1.1.1 contract from #419/#198) is the exact seam where extracted text becomes
the AT description. Today that seam falls back to the *filename* when the author wrote no
alt text — the dead end this feature replaces.

The description pass is a deeper accessibility win that benefits *all* users (searchable,
meaningful descriptions of every image) and is the v1.5 paid feature.

## 3. Architecture overview

### 3.0 Where this sits in the core/shell split

The store, the reconcile logic, and search live in **`slate-core` (Rust)**, reached over
UniFFI — same as every other index. The **OCR engine runs in the platform shell**
(Swift, Apple Vision): the shell drains a work queue the core hands it and commits results
back through a core API. This split is what keeps the Windows port (Milestone W, parked)
honest — Windows swaps in `Windows.Media.Ocr` as another engine; the store, reconcile,
search, and CLI surfaces are already shared. A GRDB/Swift-side store (an earlier draft of
this spec) would have stranded all of that in one shell.

### 3.1 Content addressing

- Hash = **BLAKE3 of the raw image file bytes** — which is already the repo-wide standard:
  `vault::fs::content_hash` computes it, and **every indexed file already carries it** in
  `files.content_hash`, kept current by the scanner with a `(mtime, size, ctime)` fast
  path. **v1 builds no new hashing infrastructure**; the OCR key is a column read.
- The hash is the sole resolution key everywhere (sidecar frontmatter, cache row, lookups).
- **Oversized-file sentinel (correctness rule):** files whose body the scanner refused
  (over the large-file threshold) are indexed with the hash of an *empty* body. That value
  is a shared sentinel, not a content key — such files are **OCR-ineligible** (resolver
  spec §6) and must never key a sidecar. (`read_attachment`'s 50 MiB cap refuses them at
  read time anyway.)
- **Accepted caveat:** two visually identical images encoded differently produce different
  hashes and are processed twice. This is correct-by-content behavior; visual/AI-assisted
  dedupe is deferred to v1.5. "We can only protect the user from themselves so far."

### 3.2 Canonical store — `.imagedesc/` sidecars

- A root-level dotfolder (`.imagedesc/`) — automatically outside the note tree, graph,
  index, and search, because the scanner skips dot-prefixed entries (the same rule that
  hides `.obsidian/` and `.slate/`). Zero scanner changes needed.
- **Why not inside `.slate/`:** the locked storage layout
  (`docs/plans/05_locked_architecture_decisions.md`) defines `.slate/` as *local-only by
  default* and *disposable* — "if the SQLite database is deleted, Slate rebuilds it from
  vault content," and `.slate/cache.sqlite` / `oplog/` are the default sync-ignore set.
  Sidecars are the opposite: durable derived artifacts meant to **travel with the user's
  sync/git** (descriptions especially — regenerating them costs real money). Parking
  canonical data inside a folder whose contract is "safe to delete, never synced" would
  poison both contracts. Hence a sibling dotfolder with the durable-and-syncable contract.
- **Two files per image, split by pass:**
  - `{imageName}-ocr-{shortHash}.md`
  - `{imageName}-desc-{shortHash}.md`
- Split rationale: OCR (v1) and description (v1.5 paid) run at different times, regenerate
  on different cadences, and either may exist without the other. Separate files also keep
  each payload in its own body, which eliminates in-band delimiter collisions (§3.3).
- **Filename is cosmetic** — a human-browsable label only. Nothing ever resolves on it.
  - `shortHash` = first **12 hex chars** of the full hash (git short-SHA pattern); **full
    hash in frontmatter**. Real hex only; **no literal `#`** in filenames (fragment
    delimiter hazard).
  - **Filename collision rule:** if a write would land on an existing sidecar whose
    frontmatter `hash` differs (same image name + same 12-char prefix, different image),
    lengthen the short hash until unique. Resolution never keys on filenames, but a
    collision would *overwrite* another image's sidecar — the rule protects the bytes,
    not the lookup.
  - The `imageName` prefix may go stale on rename; that is acceptable because the reliable
    anchor is `hash` in frontmatter, kept current on reconcile.

### 3.3 Sidecar file format

YAML frontmatter carries identity/provenance; **the entire body is the raw payload** — no
fences, no headings, no in-band delimiters. Read frontmatter, then take the rest of the
file verbatim to end-of-file.

```
---
source: attachments/architecture.png     # vault-relative, forward slashes (matches files.path); updated on reconcile
image_name: architecture.png             # name as last seen (cosmetic)
hash: <full BLAKE3 hex>                  # = files.content_hash — the real resolution key
kind: ocr                                # or: description
engine: apple-vision                     # OCR engine, or the VLM for descriptions
engine_version: "<version>"
created: 2026-07-10T00:00:00Z
updated: 2026-07-10T00:00:00Z
status: ok                               # ok | empty | failed | failed_permanent — one enum, see reconciliation spec §3
attempts: 1                              # present on failed/failed_permanent (per engine identity)
last_attempt: 2026-07-10T00:00:00Z       # present on failed/failed_permanent
failed_engines: ["apple-vision/1.4"]     # engine identities that exhausted retries; carried forward on success
confidence: 0.93                         # optional: mean Vision observation confidence
companion: architecture-desc-<hash>.md   # ADVISORY ONLY — never resolved on; has_* truth comes from disk/cache
---
<raw OCR text, or raw VLM description — taken verbatim, no wrapping>
```

- `status` is **one enum**, scoped by the file's `kind` (an earlier draft used an
  `ocr_ran` boolean *and* an `ocr_status` enum across the two specs; unified here — the
  reconciliation spec's states map 1:1 onto this field).
- **Failure state is engine-scoped; success state is not** (reconciliation spec §2):
  `failed`/`failed_permanent` bind to the engine identities recorded in `failed_engines`
  (`engine/engine_version`). A different identity — another machine's shell, or an
  upgraded Vision — retries instead of inheriting the dead end. `ok`/`empty` stand for the
  image itself until an explicit invalidation policy (v1.5) says otherwise; a later
  success **keeps** `failed_engines` as provenance rather than erasing it.
- `companion` is advisory/informational — it is denormalized state across two files and
  will drift; nothing may ever trust it. (Cheapest correct option would be dropping it;
  kept because it makes hand-browsing `.imagedesc/` pleasant.)
- **Per-observation bounding boxes are NOT persisted in v1.** The body must stay raw text
  (a box array is in-band structure), and frontmatter is the only sidecar home that would
  survive a cache rebuild — but hundreds of `regions:` entries per image bloat every
  sidecar for a feature (navigable recognized regions) that isn't designed yet. v1 records
  the `confidence` summary scalar only; revisit region persistence with the v1.5 feature
  that consumes it.

**Why body-as-content, not fenced or in-frontmatter:** a VLM description of an engineering
diagram will contain its own fenced code blocks and `##` headings. Wrapping the payload in
fences forces dynamic fence-length computation to avoid collisions; putting it in YAML
scalars is an escaping minefield and hurts readability. Giving each payload its own file
body means there is nothing for it to collide with. (If a future feature ever streams *both*
payloads into one container — a combined export or "copy everything about this image" — the
collision risk returns and the fix is the CommonMark rule: fence with one more backtick than
the longest run in the content. Filed as a note, not a v1 concern.)

### 3.4 Disposable speed layer — new tables in `.slate/cache.sqlite`

- **Not a new database, and not GRDB.** The mirror is a migration (next in the numbered
  series under `crates/slate-core/migrations/`) adding to the existing core-owned
  `.slate/cache.sqlite`:
  - `image_text` — keyed by content hash: `source_path`, `image_name`, `ocr_status`,
    `desc_status`, `attempts`, `last_attempt`, `engine`, `engine_version`, timestamps.
    `has_ocr` / `has_description` are derived: `status ∈ {ok, empty}` means "processed."
  - `image_text_fts` — FTS5 over the OCR/description payloads, following the
    `006_fts5.sql` external-content pattern.
- **Everything the locked layout already promises applies for free:** delete
  `.slate/cache.sqlite` → rebuilt on next open (for these tables: by walking `.imagedesc/`
  and the files index); default sync-ignored; cross-process one-writer discipline via the
  existing file-locked IMMEDIATE-transaction pattern (`save_text_locked`).
- **Search comes out of the same pipe.** OCR hits surface through the existing
  `full_text_search` / `QueryResultSet` shape (`search_db.rs`, locked in
  `docs/plans/05` §8.4): a hit's `path` is the *image's* vault path (images are already
  `files` rows), snippet from the payload. That means SearchOverlay, reading-view search,
  and the `slate search` CLI verb all gain image-text hits with no new result shape.
  Open decision (§5): UNION into the default vault scope vs. a source flag on hits —
  either way, hits must be visually/AT-attributed as image text.
- **Sidecar-vs-cache conflicts always resolve in favor of the sidecar** (canonical wins).

### 3.5 Notes are never mutated

- No breadcrumb is written into the user's note. The breadcrumb's only remaining job was
  letting *foreign* tools discover the sidecars; a hidden marker was never going to be read
  aloud in another app anyway, so we forfeit automatic third-party wiring in exchange for
  strict plain-text purity and zero note-edit risk.
- Slate resolves embeds **live**: parse embed → resolve (shared resolver) → **read
  `files.content_hash` from the index, stat-verified** → look up. The label lookup trusts
  the indexed hash only while the file's current `(mtime, size, ctime)` still matches the
  row — the scanner's own fast-path predicate, and the render path already stats the file
  for the attachment size cap, so verification is free. On mismatch (image replaced on
  disk mid-session — the renderer shows the *new* bytes, the index still holds the *old*
  hash) cached OCR is **never served**: the label degrades to the pending state, a
  targeted one-file re-index runs, and the live-update path delivers the honest label.
  A stale index must degrade to "pending," never to another image's text. When OCR for
  the verified hash exists, `EmbedView.imageEmbedTitle`'s fallback chain consumes it
  (reconciliation spec §7).
- Sidecars are discoverable-by-convention (browse `.imagedesc/` in Finder/git), not
  self-announcing. (Dotfolders are hidden from Slate's own file tree, like `.obsidian/`.)
- **Trade-off retained as a possible opt-in later:** a "write alt text into the embed"
  command for portability (`![extracted text](image.png)`), off by default.

### 3.6 OCR engine

- v1 engine is **Apple Vision** (Swift `Vision` framework: `VNRecognizeTextRequest` /
  the newer `RecognizeTextRequest`) — on-device, offline, free, high quality, shared across
  macOS / iOS / iPadOS for the planned UIKit iOS app.
- The engine is a **shell-side plugin behind a core-defined seam** (§3.0): core owns the
  queue and the store; the shell recognizes bytes and commits results. `engine` +
  `engine_version` persist and are load-bearing, not decorative: (a) **failure markers are
  engine-scoped** — an upgraded Vision or a different shell retries what an older identity
  couldn't do (reconciliation spec §2), so one engine's limitation never permanently
  silences an image everywhere; (b) **successful results are invalidated deliberately** by
  an explicit re-OCR-on-upgrade policy (v1.5), never churned automatically; (c) a future
  Windows shell (`Windows.Media.Ocr`) writes into the same store without schema changes.
- Input eligibility follows the resolver spec §4: core's `IMAGE_EXTENSIONS` minus `svg`,
  first frame for `gif`, finalized against Vision's accepted inputs.
- Per-observation bounding boxes + confidence: **not persisted in v1** (§3.3); keep the
  mean-confidence scalar so low-confidence results can be filtered/re-run later.

### 3.7 Milestone O alignment (shared infrastructure — ride it, don't duplicate it)

Milestone O (local history, `docs/plans/10_local_history/`, **in flight** — O-1 shipped via
PR #790) is building infrastructure this program needs; O's own follow-ups list now names
this program as a consumer:

- **Core→shell events.** O-2 introduces the `VaultEventListener` uniffi callback interface
  (`register_event_listener` on `VaultSession`; `ScanProgressListener` is the mechanical
  precedent) — error-only in its v1, with an extensible code enum, and a filed follow-up
  ([#802](https://github.com/coryj627/slate/issues/802)) to broaden it toward `05` §4.4
  (file-change events, index progress). OCR's live label
  refresh (reconciliation spec §7), quiet failure counts, and GC prompts are **that
  channel's second consumer**. Whichever program lands second extends the other's
  registration/dispatch pattern; two parallel callback channels is the failure mode.
- **Background worker discipline.** O-2's compaction worker (session-owned thread,
  single-flight per item, idle when the queue is empty, close-flag checked between items,
  joined on session close) is the shape of the OCR drain. Core owns the queue and the
  commits either way; the only OCR difference is that recognition itself happens
  shell-side (§3.0).
- **One "recoverable past" horizon.** O-5's History settings expose `retention_days`
  (30 / 90 default / 180 / 365, per-vault via `history_prefs`). The GC destructive tier
  (§5) keys eligibility to the same window: an orphaned sidecar is never offered for
  deletion until it has been orphaned longer than the retention window. This is cheap
  insurance, not just consistency — images deleted through Slate's structural ops land in
  the **system Trash**, and a restore within the window brings back the same bytes → same
  hash → the orphan re-validates for free (descriptions especially: paid to regenerate).
- **Settings + prefs.** OCR's per-vault knobs (GC thresholds, backlog behavior, the v1.5
  description opt-in) follow O-5's `set_history_prefs` / `history_prefs` pattern into
  `.slate/prefs.json`, and the UI joins the same Settings-tab surface family.
- **Write-path coverage.** O-3's `restore_version` / `recover_deleted_file` route through
  `save_text` verbatim (O plan decision #5 — one write path), so history operations trigger
  OCR reconcile automatically (reconciliation spec §4).

## 4. Free / paid seam (v1 → v1.5)

- **v1 (free):** OCR pass writes `{name}-ocr-{hash}.md`, cache row shows OCR processed.
- **v1.5 (paid):** Description pass writes `{name}-desc-{hash}.md`, cache row shows
  description processed.
- Independent OCR/description status columns represent every combination: OCR-only,
  description-only, both, neither-yet. Adding descriptions is a new file + column, not a
  rewrite of a subsystem.
- **Privacy boundary:** OCR is on-device; descriptions send image bytes to a cloud VLM.
  The description pass requires explicit per-vault opt-in at enablement (not buried in the
  purchase flow), and never runs on vaults that haven't opted in.

## 5. Decisions still needed (implementation checklist)

- [ ] **GC thresholds.** Two-tier GC. Orphan definition: a sidecar whose `hash` no longer
      matches any indexed file's `content_hash` (sentinel excluded) — includes edited
      images (new hash = new identity; the old sidecar ages out here). Automatic tier
      (silent, non-destructive): loose cache rows, broken/unparseable frontmatter, orphans
      queued *for confirmation*, not deleted. Destructive tier (prompt + manual trigger):
      prompt when orphaned sidecars exceed **both** ~15–20% of total sidecars **and** an
      absolute floor of **~25 files**, *or* when reclaimable size exceeds an MB threshold
      regardless of ratio. Destructive eligibility additionally waits out the shared
      retention horizon (§3.7): track orphaned-since in the cache (derived), and never
      offer a sidecar orphaned less than `retention_days` ago — a Trash-restored image
      re-validates its orphan by hash. Rate-limit the prompt (don't re-ask for N days /
      until ratio worsens). Provide dry-run + confirm since it deletes user-inspectable
      files.
- [ ] **OCR-backlog prompt.** (The fresh-vault *hashing* pass an earlier draft budgeted
      for doesn't exist as new work — the scanner already hashes every file on vault add,
      behind its own progress UX.) The backlog prompt is about the OCR pass only: prompt
      with count + **live** time estimate measured from the user's actual first-N OCR
      calls ("~1,200 images, ~8–12 min"). Offer: OCR now / OCR in background / on-demand
      only. Re-prompt only on close-and-reopen.
- [ ] **Concurrency.** Queue and commits in core; recognition in the shell **off the main
      actor**, N images in flight (start N=2, tune against Vision's own parallelism). OCR
      is incremental and interruptible with **per-image commit** (quit at 900/1200 → 900
      done, never atomic). Commit = sidecar write + cache upsert in one core call. Drain
      discipline follows O-2's compaction worker (§3.7): single-flight, idle-when-empty,
      close-flag between items, joined on session close.
- [ ] **Search surfacing shape.** UNION image-text hits into the default vault scope vs.
      an opt-in source flag on `QueryResultSet`; either way hits are attributed as image
      text in UI and AT. (Existing `QueryHit.path` carries the image's path; "which notes
      embed this image" hops through the existing links index.)
- [ ] **Sync safety.** Content-keying + file-split keep concurrent machines from
      corrupting each other, but "blind last-write-wins" is not an acceptable merge
      policy — a delayed sync from an older engine could overwrite a newer `ok` result
      and propagate the downgrade into labels and search. **File-level conflicts belong
      to the user's sync tool** (Slate ships no sync writer — Milestone M is
      detection-only), so determinism lives in the two places Slate *does* control:
      - **Writer:** commit is check-then-write — never overwrite a `status: ok` sidecar
        with a `failed`/`empty` result for the same hash+kind (drop the local downgrade,
        record its engine identity in `failed_engines` provenance only); success commits
        carry `failed_engines` forward.
      - **Adopt path:** a downgrade-shaped conflict (cache remembers `ok`, sidecar says
        otherwise) re-enqueues a fresh local run instead of silently adopting
        (reconciliation spec §5).
      Together with engine-scoped failures these make the vault **converge to `ok`**:
      machines that can succeed rewrite it, machines that can't stop retrying, and no
      Slate-authored write ever downgrades a good result. Required invariants unchanged:
      **atomic writes** — the existing `write_file` discipline (temp under `.slate/tmp/`,
      `sync_data()` before rename; the scanner's dot-skip hides crash-leaked temps);
      **recompute processed-state from disk after sync activity** (hook `SyncMarkerWatcher`
      + vault open; never trust a machine's last-known state); **parse failure →
      regenerate**, not error. Residual accepted for v1: two `ok` payloads from different
      engine versions may alternate via the sync tool's own conflict handling — both are
      honest text of the same bytes. If that churn shows up in practice, the escalation
      path is immutable per-engine-identity records, not smarter last-write-wins.
- [ ] **Negative-result + failure markers.** Absence of a sidecar means *never attempted*.
      Empty results and failures **must** write durable markers so reconcile doesn't re-OCR
      forever. (Full model in the reconciliation spec.)
- [ ] **Bounding-box persistence** (deferred, §3.3): decide with the v1.5
      navigable-regions feature; constraint to carry forward — boxes must live somewhere
      that survives a cache rebuild (frontmatter) or be accepted as re-derivable.

## 6. Non-goals (v1) / future

- **Remote embeds** (`![](https://…)`): Obsidian fetches remote images on render and keeps
  no local copy. Slate has no local bytes to hash, so **detect and skip** external targets
  in v1 — detection is the shipped `looks_external` (any scheme, not just `http(s)`;
  resolver spec §4). v1.5+: fetch → materialize → re-enter the normal local pipeline
  (network + privacy surface; deliberately deferred).
- **SVG text extraction:** svg renders as an embed but is OCR-ineligible in v1 (vector
  input; Vision takes rasters). Rasterize-then-OCR — or parsing SVG `<text>` nodes
  directly — is v1.5 territory.
- **AI-assisted visual dedupe** (near-duplicate / re-encoded images): v1.5.
- **Write-alt-text-to-embed** portability command: possible opt-in, off by default.
- **Sync-tool caveat (documentation, not code):** `.imagedesc/` travels with tools that
  sync the whole vault folder (git, iCloud Drive, Syncthing — same class that carries
  `.obsidian/`). Obsidian's own Sync service whitelists only `.obsidian/` and would not
  carry it; users of that one tool just rebuild locally (OCR is free to regenerate).

## 7. Pipeline (one line)

Image added/changed → scanner indexes it with its BLAKE3 `content_hash` (existing) → OCR
now (Apple Vision in the shell, queue from core), describe later if entitled (VLM, opted
in) → write canonical `.imagedesc/` sidecar(s) atomically → upsert `.slate/cache.sqlite`
row + FTS5 → notes untouched; Slate resolves embeds live via the shared resolver + a
stat-verified hash lookup; preview accessibility label + search read from the store.
