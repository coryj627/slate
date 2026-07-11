# Embed Resolver Contract

**Status:** 📝 Filed (2026-07-11) · **Milestone:** [PD — Accessible image OCR](https://github.com/coryj627/slate/milestone/35) · **Program:** [00_plan.md](../00_plan.md)

**Related specs:**
- [Accessible Image OCR & Description Storage](./storage_spec.md) (parent)
- [OCR Reconciliation State Machine](./reconciliation_spec.md)

---

## 1. Purpose

Turn an image embed found in a note into a stable content-hash key for the OCR store.
Parsing the embed is trivial; **resolving it to a file is the hard, bug-prone part**, because
Obsidian link resolution is contextual — the *same* `![[diagram.png]]` in two notes can point
at two different files. The failure mode is silent: an unresolved embed simply never gets
OCR'd, with no error.

**Slate already ships this resolver.** An earlier draft of this spec called for "a
re-implementation of Obsidian's resolver as its own well-tested module" — that module
exists and is census-tested:

- `links::extract_links` (`crates/slate-core/src/links.rs`) — parses both embed grammars
  (§3); strips wikilink aliases into `display_text`, splits `#heading` / `#^block` / `^block`
  anchors, flags markdown externals.
- `embeds::looks_like_image` / `parse_embed_target` (`crates/slate-core/src/embeds.rs`) —
  image classification by extension, anchor splitting, `IMAGE_EXTENSIONS` allowlist.
- `link_resolver::resolve_link` (`crates/slate-core/src/link_resolver.rs`) — precedence and
  ambiguity tiebreak, locked by the U2-3 referential-stability census (seed 164).
- `session::resolve_image_embed` / `session::read_attachment`
  (`crates/slate-core/src/session.rs`) — index-snapshot resolution → bytes + MIME + alt
  (`EmbedResolution::Image`), 50 MiB attachment cap, nested embeds to depth 3.

**This spec is therefore a contract plus a gap list, not a build plan for a new module.**
The invariant it exists to protect:

> **OCR resolution ≡ render resolution.** The OCR pipeline must consume the same parse and
> resolution code path the preview renders with, so extracted text attaches to exactly the
> file the user sees. A private re-implementation that drifts by one tiebreak rule produces
> *wrong* labels — worse than no labels.

Second invariant (unchanged): resolution is context-dependent (needs the referencing note);
everything downstream is context-free. Pipeline: **resolve → look up `files.content_hash`
(stat-verified at consumption — storage spec §3.5) → OCR-store lookup.** The scan pipeline
already maintains a BLAKE3 hash per indexed file (storage spec §3.1), so enumeration and
reconciliation never read image bytes; bytes are read (via `read_attachment`) only when
OCR actually runs.

## 2. Scope

- **In scope:** local image embeds in both supported grammars (§3), **including images
  reached through nested note embeds** (`![[note]]` whose body embeds images — core resolves
  nesting to `MAX_EMBED_DEPTH = 3` and renders those images inline, so they need labels too).
- **Out of scope (short-circuit before any index lookup):**
  - **Remote** targets — Slate has no local bytes to hash. v1 skips. Detection already
    exists (`looks_external`, §4) and is broader than an `http(s)://` prefix check.
    (See parent spec §6; fetch-materialize is v1.5+.)
  - **Non-image** embeds (`![[note.md]]`, `![[audio.mp3]]`, etc.) — `looks_like_image`
    filters by extension before resolution branches.

## 3. Input grammars

### 3.1 Wikilink embeds — parsed by `links::scan_wikilinks`
- `![[image.png]]`
- `![[image.png|alt or size]]` — first `|` splits the alias into `display_text` (threaded
  through to the image's alt, #433)
- `![[folder/image.png]]` — folder-qualified path
- `![[image.png#heading]]` — anchor split off by `parse_embed_target`; ignored for images
- A newline inside `[[…]]` interrupts the link; `[[]]` is skipped; an escaped `!` is not an
  embed. (All shipped behavior — listed so fixture coverage is deliberate.)

### 3.2 Markdown embeds — parsed by pulldown-cmark `Image` events
- `![alt](image.png)`
- `![alt](sub/folder/image.png)`
- `![alt](<path with spaces.png>)` — **the parser unwraps angle brackets itself**
- `![alt](image.png "title")` — **the parser drops link titles itself**
- `![alt|160](image.png)` — Obsidian sizing inside the alt segment (see §5.1 size-alias gap)
- `![alt](image%20with%20spaces.png)` — URL-encoded; **currently unresolvable, see §5.1**
- `![alt](https://…)` — remote, skip (§2)

## 4. Normalization (what the shipped layers do, in order)

1. **Parse time (`links.rs`):**
   - Wikilinks: trim; strip `|alias` → `display_text`; split `#heading` / `#^block` /
     `^block` → `anchor`. Wikilinks are parsed with `is_external: false` — remote wikilink
     targets are caught later by the resolver's external short-circuit (fixture 13).
   - Markdown: destination arrives from pulldown-cmark already angle-unwrapped and
     title-free, otherwise **verbatim — no percent-decoding, no trimming** (deliberate:
     "the resolver reads destination bytes as authored"). Fragment `#…` splits into
     `anchor` for internal destinations only.
   - **External check** (`looks_external`) runs on the *full authored destination*: any
     RFC 3986 scheme (`https:`, `app:`, `obsidian:`, `file:`, …), protocol-relative
     `//host`, or fragment-only target is external. The scheme test requires ≥ 2 leading
     ALPHA characters so Windows drive letters (`C:\…`) stay internal (Milestone W).
     Spec'ing the narrower "starts with `http://` or `https://`" rule would re-open the
     remote-leakage hole for every other scheme — don't.
2. **Classification (`embeds.rs`):** `looks_like_image` fires on the raw target's extension
   (case-insensitive) *before* resolution; non-image targets take the note-embed branch and
   the OCR enumerator skips them.
   - **Render allowlist** (`IMAGE_EXTENSIONS`, shipped): `png jpg jpeg gif svg webp heic bmp
     tiff tif`.
   - **OCR-eligible subset:** the render allowlist **minus `svg`** (vector input; Apple
     Vision has no direct SVG path — rasterize-then-OCR is a possible v1.5). `gif` OCRs the
     first frame. Finalize the rest against Vision's accepted inputs. If the render list
     ever grows (e.g. `avif`), the OCR list inherits by **intersection with engine
     capability**, never as a second hand-maintained list.
3. **Resolution time (`link_resolver.rs`):** external short-circuit; leading `/` or `./`
   marks the target vault-rooted exact; then the qualified-vs-basename branch per §5.

## 5. Resolution precedence (as shipped in `link_resolver::resolve_link`)

| Target shape | Rule (shipped) |
|---|---|
| **External** (any scheme, `//host`, `#frag`) | `External` — never touches the file index. |
| **Vault-rooted** (`/x.png`, `./x.png`) | Exact match from vault root, case-insensitive. **No basename fallback** — a rooted miss is `Unresolved`. (U2-3 census, seed 164: the fallback made root files unpinnable by any authored text.) |
| **Folder-qualified** (`sub/diagram.png`) | Exact vault-relative match, case-insensitive. (Markdown-extension implication applies only to extensionless targets, so it never fires for image targets.) |
| **Bare filename** (`diagram.png`) | Case-insensitive basename scan of the whole index. |
| **Multiple basename matches** | **Smallest source→target directory distance, then alphabetical path order.** Deterministic cross-platform. Do not restate this as "shortest path wins" — a shallower folder can *lose* to a deeper folder near the source (fixture 3). |

If nothing resolves → `Unresolved` (log at debug; **silent skip**, never a user-facing
error). Reconciliation treats `Unresolved` as "no image," not "pending."

### 5.1 Known Obsidian-parity gaps (upstream fixes, never OCR-local ones)

These are divergences in the **shared** resolver. Closing any of them belongs in
`links.rs` / `link_resolver.rs` so rendering, backlinks, link-rewrite, and OCR all improve
in lockstep — an OCR-side workaround would violate the §1 invariant.

| Gap | Slate today | Obsidian | Notes |
|---|---|---|---|
| Percent-encoded markdown paths (`image%20with%20spaces.png`) | Unresolved (destinations kept verbatim by design) | Decodes; its "markdown links" setting *writes* `%20` paths | Highest-value gap: Obsidian-authored vaults are full of these. |
| Note-relative markdown paths (`../shared/x.png`) | Unresolved (`..` can never match an index path) | Resolves relative to the note's folder | `resolve_image_embed` already threads `host_path` through "so any future folder-relative resolution in `link_resolver` lights up automatically" — the hook is waiting. |
| `./x.png` in markdown | Vault-rooted (wikilink semantics, census-locked) | Note-relative | Any fix must not disturb the locked wikilink rooted semantics; needs a link-kind-aware branch. |
| Size-only alias (`![[x.png\|300]]`, `![alt\|160](x.png)`) | Whole alias threads through as alt text and becomes the AT description | Treated as display width | Fix belongs in links/EmbedView: recognize `N` / `NxM` aliases as sizing and exclude them from the AT label. Until then the OCR fallback tiers may see `"300"` as "author alt." |
| Unicode normalization (NFC vs NFD filenames) | Byte-wise compare (after ASCII case-folding) — an NFC-typed embed can miss an NFD-named file from APFS | Normalizes | Add NFC folding in `find_exact` / `collect_basename_matches`. |

## 6. Output

The contract enum maps onto existing types — do **not** mint a parallel enum that can drift:

```
ResolveResult ≙
  file(target_path)  → ResolvedLink::Resolved { target_path } /
                       EmbedResolution::Image { target_path, … }
  remote             → ParsedLink.is_external == true (markdown), or the resolver's
                       External short-circuit (wikilink remote targets)
  nonImage           → !embeds::looks_like_image(target)
  unresolved         → ResolvedLink::Unresolved / EmbedResolution::Unresolved
```

What v1 actually adds is an **enumeration API** in core, exposed over UniFFI:

```
list_image_embeds(note_path) → [ImageEmbedRef {
    target_path,      // vault-relative, forward slashes
    content_hash,     // from the files row — no byte read
    alt,              // display_text as threaded today (#433)
    eligible,         // false ⇒ skip, with reason
    reason,           // remote | nonImage | unresolved | svg | oversize
}]
```

- Resolution via `resolve_link` against the same files-index snapshot the renderer uses.
- `content_hash` is read from the `files` row and **stat-verified at emission** (the
  scanner's `(mtime, size, ctime)` fast-path predicate): a mismatch triggers a one-file
  re-hash before the ref is emitted, so consumers never key work — or labels — on a hash
  the disk has moved past. Beyond that guard, **enumeration performs no byte reads**.
- Nested embeds: enumeration walks the same depth-≤3 nested resolution the renderer performs.
- **Oversized sentinel (critical):** files whose body the scanner refused (size over the
  large-file threshold) are indexed with the hash of an *empty* body, so their
  `content_hash` is a shared sentinel, **not** a content key. These must come back
  `eligible: false, reason: oversize` — keying a sidecar on the sentinel would collide
  every oversized image in the vault into one OCR record. (`read_attachment` refuses them
  at 50 MiB anyway; this rule keeps the store honest, not just the reader.)

## 7. Fixture table

Referencing note context is `notes/project/spec.md` unless stated. `A/` and `B/` are
folders each containing a `diagram.png` for the ambiguity cases.

| # | Embed (in `spec.md`) | Vault state | Expected result (shipped semantics) |
|---|---|---|---|
| 1 | `![[diagram.png]]` | one `attachments/diagram.png` | `file(attachments/diagram.png)` — basename scan |
| 2 | `![[A/diagram.png]]` | `A/diagram.png`, `B/diagram.png` | `file(A/diagram.png)` — folder-qualified exact |
| 3 | `![[diagram.png]]` | `notes/project/diagram.png` **and** `attachments/diagram.png` | `file(notes/project/diagram.png)` — distance 0 beats distance 3. Note `attachments/` is the *shallower* path and still loses: the rule is directory distance, not path length. |
| 4 | `![[diagram.png\|Caption text]]` | one `attachments/diagram.png` | `file(…)` — alias → `display_text` → threads to alt (#433) |
| 5 | `![[diagram.png\|300]]` | one `attachments/diagram.png` | `file(…)` — alias `"300"` currently threads as alt; see §5.1 size-alias gap |
| 6 | `![[diagram.png#Section]]` | one `attachments/diagram.png` | `file(…)` — anchor split by `parse_embed_target`, ignored for images |
| 7 | `![Caption](attachments/diagram.png)` | that file exists | `file(attachments/diagram.png)` — qualified exact |
| 8 | `![Caption](../shared/diagram.png)` | `notes/shared/diagram.png` | **`unresolved` today** — upstream gap (§5.1). Flips to `file(notes/shared/diagram.png)` when note-relative resolution lands; keep the row pinned-current with a loud TODO. |
| 9 | `![Caption](image%20with%20spaces.png)` | `notes/project/image with spaces.png` | **`unresolved` today** — destinations verbatim; upstream gap (§5.1) |
| 10 | `![Caption](<path with spaces.png>)` | `notes/project/path with spaces.png` | `file(…)` — pulldown-cmark unwraps angle brackets |
| 11 | `![alt\|160](attachments/diagram.png)` | that file exists | `file(…)` — `"alt|160"` threads as alt; §5.1 |
| 12 | `![](https://example.com/x.png)` | — | `remote` — `is_external` at parse time; no index lookup |
| 13 | `![[https://example.com/x.png]]` | — | skip — wikilinks parse `is_external: false`, but the resolver's external short-circuit fires (`External` → unresolved for embeds). Deterministic; matches Obsidian (wikilinks don't render remote). |
| 14 | `![[note.md]]` | `note.md` exists | `nonImage` — note-embed branch. Its *nested* image embeds still enumerate (§2). |
| 15 | `![[recording.mp3]]` | exists | `nonImage` (skip) |
| 16 | `![[missing.png]]` | no such file | `unresolved` (skip + debug log) |
| 17 | `![[Pasted image 20260101.png]]` | exists once | `file(…)` — spaces in filenames are fine |
| 18 | `![alt](diagram.PNG)` | `attachments/diagram.PNG` | `file(…)` — name and extension matching are case-insensitive |
| 19 | `![alt](x.png "CommonMark title")` | `notes/project/x.png` | `file(…)` — the parser drops the title |
| 20 | `![[Café.png]]` | `Café.png` saved NFD (APFS default) | **must-test** — byte-compare may miss the NFD form; drives the §5.1 NFC fix |
| 21 | `![[huge.png]]` | 200 MB file | `eligible: false, reason: oversize` — sentinel hash must never key a sidecar (§6) |

## 8. Failure modes to test explicitly

- **Ambiguous bare filename** → deterministic (distance, then alphabetical) — rows 1–3,
  plus a property test alongside the existing U2-3 referential-stability census.
- **Rooted miss** must stay `Unresolved` — never fall back to the basename scan (census
  seed 164 regression guard).
- **Remote leakage** → no external form may reach the index scan: any scheme (not just
  `http(s)`), protocol-relative `//host` (rows 12–13).
- **Non-image** filtered before resolution (rows 14–15).
- **Case-insensitivity** (row 18) and **Unicode normalization** (row 20).
- **Oversized sentinel-hash collision** (row 21) — the nastiest silent failure: multiple
  large images sharing one OCR record.
- **Mid-session external replacement** — replace an embedded image's bytes on disk with no
  note edit: enumeration and label lookup must detect the stat mismatch and refresh rather
  than serve or key the stale hash (reconciliation spec §8, test 1).
- **OCR/render divergence** — a golden test asserting `list_image_embeds` returns exactly
  the set of images the preview resolves for the same note + index snapshot. This is the
  §1 invariant, executable.

## 9. Notes for implementers

- **Route through `link_resolver::resolve_link` / `session::resolve_image_embed`.** Do not
  hand-roll any resolution in the OCR layer — the precedence rules are census-locked and
  shared with rendering, backlinks, and the link-rewrite planner. Getting closest-wins
  subtly wrong means invisible failures.
- The genuinely new v1 code is small: the `list_image_embeds` enumeration API, the
  OCR-eligibility filter (svg / oversize / remote / unresolved reasons), and their UniFFI
  surface. Everything else in this document already exists.
- Keep enumeration pure and read-only: `(note_path, files-index snapshot) → [ImageEmbedRef]`.
  All I/O (attachment read, OCR) happens later, in the context-free worker stage.
- Ship §7 as tests colocated with the resolver's existing suite; add a row for every
  real-world case found. Rows 8–9 are pinned to *current* behavior — when their upstream
  gaps close, the flip must be a deliberate, visible test change, not a surprise.
