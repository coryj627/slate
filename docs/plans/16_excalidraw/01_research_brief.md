# XD research brief — Excalidraw file formats, verified

**Date:** 2026-07-05. **Method:** every schema claim below was checked against primary sources — the raw TypeScript in `excalidraw/excalidraw` (master **and** the released `v0.18.0` tag), the `zsviczian/obsidian-excalidraw-plugin` master sources, crates.io/GitHub API — not from memory. Compression compatibility was **proven executably**: the plugin's exact embedded LZString JS was run in Node to produce a `compressed-json` block (scene containing `é`, `✏️`, wikilinks), and the Rust `lz-str` crate round-tripped it byte-identically in both directions. This brief is the evidence base for `00_program.md` and the xd0–xd3 specs; where a spec and this brief disagree, fix the spec.

**Structural caveat (load-bearing):** there are **two schemas in play**. The *released* schema (npm `@excalidraw/excalidraw` 0.18.1, and the Obsidian plugin's pinned fork `@zsviczian/excalidraw` 0.18.112 — i.e. every file the plugin writes today) and the *master-branch* schema, which has unreleased breaking changes to arrow bindings and freedraw. A viewer must parse the released shape and tolerate the master shape (ignore unknown fields, accept both binding forms).

---

## 1. `.excalidraw` JSON schema

No formal JSON-Schema exists; the TS types are normative:

- [`packages/element/src/types.ts`](https://github.com/excalidraw/excalidraw/blob/master/packages/element/src/types.ts) (elements) · [`packages/excalidraw/data/types.ts`](https://github.com/excalidraw/excalidraw/blob/master/packages/excalidraw/data/types.ts) (`ExportedDataState`) · [`packages/excalidraw/types.ts`](https://github.com/excalidraw/excalidraw/blob/master/packages/excalidraw/types.ts) (`AppState`, `BinaryFileData`) · [`packages/common/src/constants.ts`](https://github.com/excalidraw/excalidraw/blob/master/packages/common/src/constants.ts) (enums/versions) · [`packages/excalidraw/data/restore.ts`](https://github.com/excalidraw/excalidraw/blob/master/packages/excalidraw/data/restore.ts) (lenient loading/migration — the de-facto normalization spec) · released baseline [`v0.18.0 element/types.ts`](https://github.com/excalidraw/excalidraw/blob/v0.18.0/packages/excalidraw/element/types.ts) · docs overview (partial only): [docs.excalidraw.com/docs/codebase/json-schema](https://docs.excalidraw.com/docs/codebase/json-schema).

### Top level (`ExportedDataState`, written by `serializeAsJSON` with `JSON.stringify(data, null, 2)`)

| key | type | value |
|---|---|---|
| `type` | string | `"excalidraw"` |
| `version` | number | **2** (`VERSIONS.excalidraw = 2`, constants.ts:352) |
| `source` | string | origin URL; the Obsidian plugin writes `https://github.com/zsviczian/obsidian-excalidraw-plugin/releases/tag/<version>` |
| `elements` | array | element objects |
| `appState` | object | export-whitelisted keys (below) |
| `files` | object \| absent | `BinaryFiles` map; may be absent; deleted-element files filtered out |

Excalidraw's own load check (`isValidExcalidrawData`) requires only `type === "excalidraw"` and an array `elements`; everything else is optional (`ImportedDataState` marks all keys `?`). Parse leniently.

### Common element fields (`_ExcalidrawElementBase`, types.ts:40–82; all verified)

- `id: string` (nanoid-like; §2: the plugin rewrites text-element ids to exactly 8 chars)
- `x, y, width, height: number` (floats; x/y = top-left, scene coords) · `angle: number` (radians)
- `strokeColor: string`, `backgroundColor: string` (CSS colors incl. `"transparent"`)
- `fillStyle: "hachure" | "cross-hatch" | "solid" | "zigzag"`
- `strokeWidth: number` · `strokeStyle: "solid" | "dashed" | "dotted"`
- `roughness: number` (presets 0 architect / 1 artist / 2 cartoonist)
- `opacity: number` — **0–100, not 0–1**
- `groupIds: string[]` (ordered deepest→shallowest; groups have no name and no element of their own)
- `frameId: string | null` — **frame membership lives on the child**, not on the frame
- `roundness: null | { type: 1|2|3, value?: number }` — null = sharp; type 3 `ADAPTIVE_RADIUS` = fixed-px radius for rectangles; type 2 `PROPORTIONAL_RADIUS` = ratio for diamonds/linear; type 1 legacy (render as 2). **Exact radius formula** (`getCornerRadius`, [packages/element/src/utils.ts](https://github.com/excalidraw/excalidraw/blob/master/packages/element/src/utils.ts), verified verbatim 2026-07-05; callers pass `x = min(width, height)`): proportional/legacy ⇒ `x × 0.25` (`DEFAULT_PROPORTIONAL_RADIUS`); adaptive ⇒ `fixed = value ?? 32` (`DEFAULT_ADAPTIVE_RADIUS`), `cutoff = fixed / 0.25`, then `x ≤ cutoff ? x × 0.25 : fixed`. Pre-roundness files used `strokeSharpness: "round"|"sharp"` instead — tolerate it.
- `seed: number` — seeds roughjs so sketchy rendering is stable across renders (see §5)
- `version: number` (sequential per change) · `versionNonce: number` (random tie-break) · `updated: number` (epoch ms)
- `index: string | null` — fractional index ([rocicorp/fractional-indexing](https://github.com/rocicorp/fractional-indexing)), e.g. `"a0"`, `"a1"`; kept in sync with array order. **Render in array order; fall back to sorting by `index` when present and inconsistent.**
- `isDeleted: boolean` — soft delete; **viewers must skip `isDeleted: true`**
- `boundElements: Array<{id, type: "arrow"|"text"}> | null` — elements bound *to* this one
- `link: string | null` · `locked: boolean` · `customData?: object` (plugin uses it heavily)

### Per-type fields

**`rectangle` / `ellipse` / `diamond`** — none (source groups them as `ExcalidrawGenericElement`). A transient `selection` element type also exists in the union — skip it.

**`text`** (types.ts:235–257): `text` (displayed, wrap line-breaks included), `fontSize: number`, `fontFamily: number`, `textAlign: "left"|"center"|"right"`, `verticalAlign: "top"|"middle"|"bottom"`, `containerId: string | null` (shape/arrow this label is bound inside), `originalText` (no wrap breaks), `autoResize: boolean`, `lineHeight: number` (unitless; px = lineHeight × fontSize). Font codes (constants.ts:130–141): 1 Virgil (legacy hand) · 2 Helvetica (legacy) · 3 Cascadia (legacy code) · 4 unused/Obsidian-custom · 5 Excalifont (current hand default) · 6 Nunito (current normal) · 7 Lilita One · 8 Comic Shanns (current code) · 9 Liberation Sans · 10 Assistant; fallbacks 100 CJK, 998 sans-serif, 999 monospace, 1000 emoji.

**`arrow` / `line`**: `points: [number,number][]` — **element-local**, relative to (x, y); first point `[0,0]`.
- *Released 0.18.x* (what exists in the wild): `lastCommittedPoint` (ignore), `startBinding`/`endBinding: { elementId, focus: number, gap: number } | null`; `startArrowhead`/`endArrowhead: null | "arrow" | "bar" | "circle" | "circle_outline" | "triangle" | "triangle_outline" | "diamond" | "diamond_outline" | "crowfoot_one" | "crowfoot_many" | "crowfoot_one_or_many" | "dot"(legacy)`; `arrow` adds `elbowed: boolean` (+ `fixedSegments`, `startIsSpecial`, `endIsSpecial` on elbow arrows).
- *Master (unreleased)*: bindings become `{ elementId, fixedPoint, mode }` (no focus/gap); `line` gains `polygon: boolean`; new `cardinality_*` arrowheads. Tolerate; for a static viewer bindings only matter as adjacency — **arrows always carry their own `points`**, so geometry never depends on binding math.

**`freedraw`**: `points: LocalPoint[]`, `pressures: number[]`, `simulatePressure: boolean`; master adds `strokeOptions`. Render as a stroked path through `points`; ignore pressure in v1.

**`image`**: `fileId: string | null` (key into `files`), `status: "pending"|"saved"|"error"`, `scale: [number, number]` (±1 axis flip), `crop: { x, y, width, height, naturalWidth, naturalHeight } | null` (natural-pixel space).

**`frame` / `magicframe`**: own field `name: string | null`; children point back via `frameId`. `magicframe` is the AI wireframe frame — treat as frame.

**`embeddable` / `iframe`**: `embeddable` carries its URL in the common `link` field (live web embed); `iframe` hosts generated HTML via `customData`. Render both as non-live placeholders (canvas T decision 10 stance).

### Default color palettes (normative for `color_label`, xd0-3 rule 5)

From [`packages/common/src/colors.ts`](https://github.com/excalidraw/excalidraw/blob/master/packages/common/src/colors.ts) (verified 2026-07-05):

| pick set | values |
|---|---|
| `DEFAULT_ELEMENT_STROKE_PICKS` | `#1e1e1e` black · `#e03131` red · `#2f9e44` green · `#1971c2` blue · `#f08c00` yellow |
| `DEFAULT_ELEMENT_BACKGROUND_PICKS` | `transparent` · `#ffc9c9` red · `#b2f2bb` green · `#a5d8ff` blue · `#ffec99` yellow |
| `DEFAULT_CANVAS_BACKGROUND_PICKS` | `#ffffff` · `#f8f9fa` · `#f5faff` · `#fffce8` · `#fdf8f6` |

(Open-color derived; the full picker exposes more shades — the label table matches these picks exactly and phrases everything else "custom color".)

### Arrow labels

A bound `text` element: the arrow's `boundElements` contains `{type:"text", id}` and the text element's `containerId` = the arrow's id (`ExcalidrawTextContainer = rectangle | diamond | ellipse | arrow`). Same mechanism as text-in-shape.

### `files` map

`Record<fileId, { mimeType, id, dataURL, created, lastRetrieved?, version? }>`; `dataURL` is a full base64 data URI; `mimeType` ∈ `image/svg+xml, png, jpeg, gif, webp, bmp, x-icon, avif, jfif` or `application/octet-stream`.

### `appState` export whitelist

Exactly four keys are exported (`APP_STATE_STORAGE_CONF`, identical in v0.18.0): **`viewBackgroundColor`**, **`gridSize`** (may be `null` in older files), **`gridStep`**, **`gridModeEnabled`**. Treat as open-ended — the Obsidian plugin writes extras (e.g. `theme: "dark"`, verified in its `DARK_BLANK_DRAWING`). A static viewer needs `viewBackgroundColor`, optionally `theme` + grid.

---

## 2. Obsidian excalidraw-plugin `.excalidraw.md` format

Plugin v2.25.2, pinned fork `@zsviczian/excalidraw` 0.18.112 ⇒ **every plugin-written file uses the released (focus/gap) schema**. Anatomy verified in [`src/shared/ExcalidrawData.ts`](https://github.com/zsviczian/obsidian-excalidraw-plugin/blob/master/src/shared/ExcalidrawData.ts) (`generateMDBase`/`getMarkdownDrawingSection`, ~:1598–1710, :280–302):

```
---
excalidraw-plugin: parsed
tags: [excalidraw]
---
==⚠  Switch to EXCALIDRAW VIEW … ⚠== …

# Excalidraw Data

## Text Elements
First label ^8charId1

Second [[Wiki Link]] label ^8charId2

## Element Links
someElId: [[Linked note]]

## Embedded Files
fileId1: [[folder/image.png]]
fileId2: $$e^{i\pi}$$

%%
## Drawing
```compressed-json
N4IgLgngDgpiBcIYA8DGBDANgSwCYCd0B3EAGhADcZ8BnbAewDsEAmc…

…more 256-char lines separated by blank lines…
```
%%
```

- **Frontmatter:** `excalidraw-plugin:` with values `parsed` or `raw`; legacy `locked` ≡ `parsed` ([`src/shared/TextMode.ts`](https://github.com/zsviczian/obsidian-excalidraw-plugin/blob/master/src/shared/TextMode.ts)). Optional `excalidraw-*` keys (`excalidraw-export-dark`, `-export-transparent`, `-export-padding`, `-export-pngscale`, `-export-embed-scene`, `-mask`, `-link-prefix`, `-onload-script`, …) — full list in `FRONTMATTER_KEYS`, [`src/constants/constants.ts`](https://github.com/zsviczian/obsidian-excalidraw-plugin/blob/master/src/constants/constants.ts):307. **The frontmatter key is the detection signal, not the filename.**
- **Headings:** current files write `# Excalidraw Data` with `## Text Elements` / `## Embedded Files` / `## Drawing`; **legacy files used level-1 `# Text Elements` / `# Drawing`**. The plugin's own regexes accept both (`/##? Text Elements/`, `/\n##? Drawing\n/`) — ours must too.
- **Text Elements entries:** `<raw text> ^<blockid>\n\n`, block id **exactly 8 chars** (parse regex `/\s\^(.{8})[\n]+/g`, :1013) **and equal to the text element's `id` in the scene JSON** — the plugin rewrites longer ids to 8-char nanoids so Obsidian block refs work (:~1283). Entries hold *raw* markdown (wikilinks intact). Optional `^_dummy!_` entry when the lint-support setting is on.
- **Element Links** (optional): `<elementId>: <link>` lines for non-text elements carrying `link`s.
- **Embedded Files entries:** `<fileId>: [[vault wikilink]]` (optionally + JSON colorMap suffix for SVG recolor), `<fileId>: $$latex$$` (LaTeX equation), or `<fileId>: <hyperlink>`. **The scene JSON's `files` map is emptied in `.md` files** — images live as real vault files keyed back via `fileId` (:1951). A viewer must resolve images through this section, not `files`.
- **`%%` comment:** `## Drawing` is wrapped in an Obsidian `%%…%%`; some files open the `%%` before `# Excalidraw Data` instead (whole data section commented). Handle both.
- **Compression — verified + executably proven:** `compressed-json` = **LZ-String `compressToBase64`**, output split into **256-char lines separated by blank lines**, trimmed. Decompress = strip *all* `\n`/`\r`, then `decompressFromBase64`. Exact code: `compress`/`decompress` in [`src/utils/sceneDataUtils.ts`](https://github.com/zsviczian/obsidian-excalidraw-plugin/blob/master/src/utils/sceneDataUtils.ts):113–144 (+ identical worker copy). Inner JSON uses **tab indentation**.
- **Uncompressed mode:** with the `compress` setting off (default **true** since 2.2.0, [`src/core/settings.ts`](https://github.com/zsviczian/obsidian-excalidraw-plugin/blob/master/src/core/settings.ts):577) the same section is a plain ` ```json ` fence. Truly legacy files may have **no fence at all** (ExcalidrawData.ts ~:270). A parser must accept `compressed-json`, `json`, and fenceless.
- **Raw `.excalidraw` files:** "compatibility mode" (`compatibilityMode`, default **false**, "99.9% of the cases you DO NOT want this on") plus optional auto-export/one-way-sync settings. **Default output is `.excalidraw.md`**; legacy `.excalidraw` opens in raw text mode.
- **Links inside drawings:** wikilink markup is preserved in the markdown sections (Text Elements / Element Links / Embedded Files) precisely so Obsidian's indexer sees them — backlinks and rename-tracking ride the *markdown*, not the JSON. (For Slate this means link indexing of `.excalidraw.md` files already works today via the standard markdown scan — verified in xd1's baseline facts.)

## 3. Rust ecosystem

**`lz-str`** ([crates.io](https://crates.io/crates/lz-str), [adumbidiot/lz-str-rs](https://github.com/adumbidiot/lz-str-rs)): port of pieroxy's lz-string; `compress_to_base64` / `decompress_from_base64` (src/lib.rs:49/54). License **MIT OR Apache-2.0**. Latest release 0.2.1 (2022-10) but repo actively maintained (last commit 2025-12) with JS-compat tests; ~378K downloads. **Round-trip proven both directions** against the plugin's embedded LZString. Caveats: (a) decompress returns `Option<Vec<u16>>` (UTF-16 code units) — always go through `String::from_utf16`; (b) trailing base64 **padding differs** from JS (payload identical; both sides decompress each other) — irrelevant read-only, matters only if we ever re-serialize. Alternative crate `lz-string` 0.1.1 is decompression-only and stale — rejected. Pin `lz-str` + golden-file corpus test in CI.

**Licenses** (GitHub API + LICENSE files, verified 2026-07-05): `excalidraw/excalidraw` **MIT** · roughjs (`rough-stuff/rough`) **MIT** · `zsviczian/obsidian-excalidraw-plugin` **AGPL-3.0** — *not* MIT (its `package.json` still says MIT; stale metadata). Slate is AGPL-3.0-or-later, so even format-logic derivation from the plugin is license-compatible; we re-implement from the format anyway, copying no code (LaTeX Suite stance).

## 4. Excalidraw accessibility status (the motivation)

Canvas content is not exposed to assistive tech; a11y is limited to the surrounding UI. Citable:
1. [excalidraw#5759](https://github.com/excalidraw/excalidraw/issues/5759) (open since 2022): screen-reader support "limited mainly to the interface, not the canvas contents."
2. [excalidraw#7492](https://github.com/excalidraw/excalidraw/issues/7492): Deque professional audit — keyboard users can pick tools but cannot draw; unnamed focusable elements; contrast failures.
3. [excalidraw#8088](https://github.com/excalidraw/excalidraw/issues/8088) (high-contrast themes, open) and [excalidraw#11378](https://github.com/excalidraw/excalidraw/issues/11378) (explores HTML-in-Canvas specifically to get "accessibility tree syncing" — i.e. today there is none).

Same failure class as every graph view (P research brief §2): an opaque canvas. Same fix: the data is structured; project it.

## 5. roughjs determinism (why sketchy-parity stays feasible later)

- Excalidraw passes `seed: element.seed` into roughjs per shape ([`packages/element/src/shape.ts`](https://github.com/excalidraw/excalidraw/blob/master/packages/element/src/shape.ts):201, 588).
- roughjs `Options.seed` feeds a **Park–Miller LCG**: `seed = Math.imul(48271, seed); return ((2**31−1) & seed) / 2**31` ([`src/math.ts`](https://github.com/rough-stuff/rough/blob/master/src/math.ts)). Port notes: `seed = 0` means *unseeded* (falls back to `Math.random()`); `Math.imul` is signed 32-bit wrapping multiply (`i32::wrapping_mul`); pixel-exact sketchiness requires porting the per-primitive fill/stroke algorithms and their PRNG call order, not just the LCG. This is why sketchy parity is a scoped *enhancement* (XD-E1), not v1.

## Flagged as unverified

- Which excalidraw *release* will ship the master-branch binding changes (npm latest 0.18.1 predates them; excalidraw.com may emit them earlier). Mitigation: tolerant parse, both binding shapes.
- When the plugin's license changed to AGPL-3.0 (current state verified; history not traced).
- Whether very old plugin versions chunked base64 differently than 256-char lines. Mitigation (what the plugin itself does): strip all `\n`/`\r` before decompressing — chunk width never matters.
