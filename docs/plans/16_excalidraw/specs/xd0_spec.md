# XD0 executable spec — Backend: parsers, model, SVG renderer, FFI + gates

Issues: XD0-1 ([#676](https://github.com/coryj627/slate/issues/676)) · XD0-2 ([#677](https://github.com/coryj627/slate/issues/677)) · XD0-3 ([#678](https://github.com/coryj627/slate/issues/678)) · XD0-4 ([#679](https://github.com/coryj627/slate/issues/679)) · XD0-5 ([#680](https://github.com/coryj627/slate/issues/680)). Milestone: [GH 34](https://github.com/coryj627/slate/milestone/34). One PR per issue.
Program: [00_program.md](../00_program.md) (locked decisions 2–5, 8, 11–13; DoD §XD-A/§XD-B/§XD-C/§XD-D/§XD-E). Format facts: [01_research_brief.md](../01_research_brief.md) — **normative for every field name in this spec**; when in doubt, the brief's primary-source citations win.
Backend norms: fmt/clippy pre-push, censuses for correctness invariants, host-independent slate-core (no macOS deps, no I/O in parsers).

**Execution order: XD0-1 → { XD0-2 ∥ XD0-3 } → XD0-4 → XD0-5.** (XD0-2 needs XD0-1's scene types only; XD0-5's census harness may be developed alongside and gates the wave.)

Baseline facts (verified 2026-07-05, this worktree):

- Pure-parser pattern to follow: `crates/slate-core/src/canvas/mod.rs` (#359) — "no I/O, no session state, deterministic output"; tolerant contract in its module doc: entry-level problems ⇒ warning + skip, never hard-fail; only not-JSON degrades the load. XD copies the *contract*, not the code.
- Model-derivation pattern: `canvas/model.rs` (#360) — reading order (containment by center point, depth-first, `(y, x, doc order)` in-container), adjacency, summaries. XD0-3 mirrors it with frames as the (single-level) containers.
- SVG + description precedent: `crates/slate-core/src/diagram.rs` — `DiagramBlock { svg: Option<Vec<u8>>, structured_description: String, … }` (:56), description non-empty even on render failure (:53), `structured_description()` (:144).
- Attachment caps: `SessionConfig.large_attachment_refuse_bytes` (default 50 MiB), enforced in `Session::read_attachment` (session.rs:782–803).
- Frontmatter: `frontmatter.rs` — `extract_frontmatter` (:465), `frontmatter_range` (:137) (yaml-rust2). Reuse; never re-implement detection.
- Handle-based FFI naming to mirror: slate-uniffi/src/lib.rs:3672 ("Canvas … 1:1 mirrors of the handle-based read API") — `open_canvas → u64`, `canvas_scene` (:4511), `canvas_outline` (:4547), `canvas_table_rows` (:4557), `canvas_neighbors` (:4567), `canvas_where_am_i` (:4581), `close_canvas` idempotent (:4542).
- Workspace JSON convention: serde_json with `preserve_order` is already enabled workspace-wide (canvas/mod.rs uses `Map<String, Value>` raw retention).
- Census/bench conventions: `census_*` test fns, `SLATE_CENSUS_FULL=1` via `census_scale()`; criterion benches in `crates/slate-core/benches/` (see `canvas_bench.rs`); baselines recorded in `BENCHMARKS.md`.
- New dependency (XD0-2): `lz-str` (MIT OR Apache-2.0), pinned exact version in workspace `Cargo.toml`; `decompress_from_base64 → Option<Vec<u16>>` ⇒ always `String::from_utf16` (brief §3). Compatibility already proven executably against the plugin's LZString (brief §3); the golden corpus test re-proves it in CI forever.

---

## XD0-1 · `.excalidraw` scene parser (#676) — PR 1

New module `crates/slate-core/src/excalidraw/mod.rs`: `pub fn parse(source: &str) -> (ExcalidrawScene, Vec<ExcalidrawWarning>)`.

### Types (pinned)

```rust
pub struct ElementId(pub String);

pub struct ExcalidrawScene {
    pub elements: Vec<Element>,          // parse order = file array order; isDeleted:true EXCLUDED here
    pub background: Option<String>,      // appState.viewBackgroundColor, verbatim
    pub theme_dark: bool,                // appState.theme == "dark" (plugin extension; default false)
    pub files: BTreeMap<String, BinaryFile>, // raw files map; empty for wrapper files (xd0-2 fills via sections)
    pub skipped: u32,                    // count of entry-level skips (viewer banner)
    pub deleted: u32,                    // count of isDeleted elements dropped (rule 3; feeds xd0-3 rule 6's "not shown" line)
    pub source_kind: SourceKind,         // Raw | Wrapper (set by xd0-2's entry point)
}

pub struct Element { pub id: ElementId, pub common: ElementCommon, pub kind: ElementKind }

pub struct ElementCommon {
    pub x: f64, pub y: f64, pub width: f64, pub height: f64, pub angle: f64,
    pub stroke_color: String, pub background_color: String,
    pub fill_style: FillStyle,           // Hachure | CrossHatch | Solid | Zigzag | Other (tolerant)
    pub stroke_width: f64, pub stroke_style: StrokeStyle, // Solid | Dashed | Dotted | Other
    pub roughness: u8, pub opacity: f64, // opacity 0–100 per brief §1
    pub seed: i64,                       // parsed now so XD-E1 (sketchy parity) stays renderer-internal (decision 3); 0 = unseeded (brief §5)
    pub group_ids: Vec<String>, pub frame_id: Option<ElementId>,
    pub roundness: Roundness,            // Sharp | Round { kind: u8, value: Option<f64> } (legacy strokeSharpness maps here)
    pub link: Option<String>, pub locked: bool,
    pub index: Option<String>,           // fractional index, verbatim
    pub bound_elements: Vec<BoundRef>,   // { id, kind: Arrow|Text|Other }
}

pub enum ElementKind {
    Rectangle, Ellipse, Diamond,
    Text(TextData),        // text, original_text, font_size, font_family: u32, text_align, vertical_align, container_id, line_height
    Arrow(LinearData),     // points: Vec<(f64,f64)> (element-local), start/end: Option<Binding>, start/end_arrowhead: Option<String> (verbatim), elbowed: bool
    Line(LinearData),
    Freedraw { points: Vec<(f64, f64)> },   // pressures ignored in v1 (decision 3)
    Image { file_id: Option<String>, scale: (f64, f64), crop: Option<Crop>, status: String },
    Frame { name: Option<String> },         // magicframe parses as Frame
    Embeddable,                             // URL in common.link (decision 6: placeholder render)
    Iframe,
    Unknown { type_name: String },          // forward-compat: renders as honest placeholder, appears in projections
}

pub struct Binding { pub element_id: ElementId }   // focus/gap AND fixedPoint/mode shapes both reduce to this (brief §1: static viewers need only the reference)
```

### Normative parse rules

1. **Load gate:** root must be JSON with `type == "excalidraw"` and an array `elements` (Excalidraw's own `isValidExcalidrawData` rule, brief §1). Anything else ⇒ `(empty scene, [ParseFailed])`. `version`/`source` are read but never gates.
2. **Tolerant per element** (canvas contract): an element missing `id`/`type`, or with un-coercible geometry, ⇒ one `ExcalidrawWarning::SkippedElement { index, reason }` + `skipped += 1`; parsing continues. Unknown *fields* are ignored everywhere; unknown *types* become `ElementKind::Unknown` (not skipped — they have geometry and must occupy space honestly).
3. **`isDeleted: true` elements are dropped** (brief §1) and counted in `scene.deleted` so the description can say "3 deleted elements not shown" only when > 0. `selection` elements are dropped silently.
4. Numeric coercion: JSON numbers only (no string-to-number guessing); missing optional numerics take Excalidraw defaults (opacity 100, angle 0, strokeWidth 1); missing required geometry ⇒ skip rule 2 — required = `x`/`y` for **every** element (linear points are element-local, brief §1), plus `width`/`height` for non-linear elements.
5. Bindings: accept `{elementId, focus, gap}` and `{elementId, fixedPoint, mode}`; both reduce to `Binding { element_id }`. A binding whose `elementId` is absent from the scene ⇒ keep the arrow, drop the binding, warn `DanglingBinding`.
6. Determinism: `parse` is a pure function; equal input strings give equal scenes + warnings (no clocks, no randomness, no locale).
7. `theme_dark` is parsed and exposed but **does not alter v1 rendering** (scene colors are author content; the wrapper's export-theme keys are plugin plumbing). Documented in help (xd3).

### Tests (PR 1)

- Fixture: `tests/fixtures/excalidraw/sample.excalidraw` — hand-authored per brief §1 covering every `ElementKind`, both binding shapes, bound text (container label + arrow label), frames, groups, roundness variants incl. legacy `strokeSharpness`, an `isDeleted` element, an unknown type, an unknown field at every level.
- Unit: rules 1–6 each; empty scene; `files` map with data URI.
- Property (proptest): parse never panics on arbitrary JSON; parse(s) == parse(s) (determinism).

- [ ] Types + `parse` per rules 1–6
- [ ] Fixture + unit + property tests
- [ ] fmt/clippy clean; host-independent; no I/O

## XD0-2 · `.excalidraw.md` wrapper (#677) — PR 2

Same module, `pub fn parse_wrapper(source: &str) -> Result<(ExcalidrawScene, WrapperData, Vec<ExcalidrawWarning>), WrapperError>`. All anatomy facts: brief §2.

```rust
pub struct WrapperData {
    pub text_mode: TextMode,                       // Parsed | Raw ("locked" ⇒ Parsed)
    pub text_elements: Vec<(ElementId, String)>,   // 8-char block id → raw markdown text
    pub element_links: Vec<(ElementId, String)>,   // "## Element Links" lines
    pub embedded_files: Vec<(String, EmbeddedFileRef)>, // fileId → Vault(target) | Latex(src) | Url(url)
}
```

### Normative rules

1. Frontmatter via `frontmatter_range`/`extract_frontmatter`; `excalidraw-plugin` key present = wrapper (any value). Absent ⇒ `WrapperError::NotExcalidraw` (callers fall through to normal note handling).
2. Normalize `\r\n` → `\n` before any section/heading matching (autocrlf vaults; the corpus includes a CRLF fixture). Drawing section: find `/\n##? Drawing\n/` (both heading levels — brief §2); accept ` ```compressed-json `, ` ```json `, or fenceless body; tolerate `%%` wrappers in both documented placements. **Key present but no Drawing section ⇒ not an error**: degraded empty scene + `MissingDrawing` warning (decision 11).
3. `compressed-json`: strip **all** `\n`/`\r` from the block body, `lz_str::decompress_from_base64`, `String::from_utf16`; then rule XD0-1.1 onward. Decompression **or** `from_utf16` failure ⇒ `WrapperError::DecompressFailed`. `WrapperError` has exactly two variants — `NotExcalidraw` and `DecompressFailed`; the **session** (XD0-5 `open_excalidraw`, XD1-1 resolve) converts `DecompressFailed` into a degraded empty scene + warning so viewer and embeds still render honestly (§XD-A).
4. Sections `## Text Elements` / `## Element Links` / `## Embedded Files` parsed per brief §2 grammar (8-char `^blockid`, `fileId: target` lines; `$$…$$` ⇒ `Latex`; wikilink ⇒ `Vault` with alias/anchor handling via the existing `links.rs` wikilink scanner — **reuse it, don't write a second one**; anything else ⇒ `Url`). Ignore the `^_dummy!_` entry. Sections are optional; missing = empty.
5. In `Parsed` text mode the scene's text elements keep the scene JSON's display text; `WrapperData.text_elements` carries the raw markdown for link exposure (decision 7). No text substitution in v1 — we render what Excalidraw rendered.
6. `files` in wrapper scenes is expected empty (brief §2); if non-empty (older files), keep it — `embedded_files` entries take precedence per `fileId` collision.

### Tests (PR 2)

- **Golden corpus** `tests/fixtures/excalidraw/wrapper/`: real-format files covering compressed (256-char chunking), uncompressed `json` fence, legacy level-1 headings, fenceless legacy, `%%`-before-data variant, Text Elements with wikilinks + aliases + emoji/multibyte (the executable-proof scene from brief §3 becomes a fixture), Embedded Files with vault/LaTeX/URL refs, `raw` and `locked` text modes.
- Round-trip proof in CI: corpus decompresses to byte-expected JSON (golden `.expected.json` files).
- Property: `parse_wrapper` never panics on arbitrary text; non-wrapper markdown always yields `NotExcalidraw`.

- [ ] `parse_wrapper` per rules 1–6; `lz-str` pinned in workspace Cargo.toml
- [ ] Golden corpus + expected-JSON round-trips in CI
- [ ] Wikilink parsing reuses `links.rs` scanner
- [ ] fmt/clippy; host-independent

## XD0-3 · Accessibility model + structured description (#678) — PR 3

`excalidraw/model.rs`: `pub fn derive(scene: &ExcalidrawScene, wrapper: Option<&WrapperData>) -> ExcalidrawModel`.

```rust
pub struct ExcalidrawModel {
    pub reading_order: Vec<ElementId>,       // total order over non-label elements (bound text folds into its container)
    pub frames: Vec<FrameEntry>,             // single-level containers: (frame element, ordered member ids)
    pub adjacency: BTreeMap<ElementId, Vec<Neighbor>>, // Neighbor { via_arrow: ElementId, other: ElementId, direction: Outgoing|Incoming|Undirected, label: Option<String> }
    pub summaries: BTreeMap<ElementId, ElementSummary>, // type_label, title, color_label, frame_name, group_note ("grouped with N others"), links: Vec<LinkRef>, size_pt: (f64,f64), position: RelativePosition (NW…SE ninth of the union bounds of non-deleted elements — model-side, independent of render view_box)
    pub scene_summary: String,               // §XD-A description; ALSO the embed description
}
```

### Normative rules

1. **Reading order** (canvas model.rs rule, adapted): containers are **frames only** (single level — Excalidraw frames don't nest; membership = child `frameId`, brief §1). Depth-first: unframed elements and frames interleaved by `(y, x, array order)` of their bounds; within a frame, members by `(y, x, array order)`. An element whose `frame_id` names a missing/skipped/deleted element is treated as unframed + `DanglingFrame` warning (canvas G8 precedent; census 2 covers it). Deterministic; census-gated (XD0-5).
2. **Labels fold into containers:** a text element with `containerId` is *not* a reading-order entry; it becomes its container's `title` (shapes) or the arrow's `label`. Standalone text elements are entries with their text as title.
3. **Adjacency** from arrow bindings (both schemas, already reduced): arrow with start A, end B ⇒ A gets `Outgoing(via, B)`, B gets `Incoming(via, A)`; one bound end ⇒ `Undirected` on that element. Arrow label (rule 2) rides `Neighbor.label`.
4. **Titles:** text content (rule 2) → frame `name` → image: alt-less "image" + filename when wrapper `embedded_files` names one → `link` host → honest fallback: "freehand stroke", "rectangle", "unknown ⟨type_name⟩" (decision 6 — never fabricate).
5. **`color_label`:** exact (case-insensitive) match of `strokeColor` against the pinned default-palette table in **brief §1 "Default color palettes"** (stroke + background picks; normative list in code with attribution header per decision 13); no match ⇒ "custom color". Deterministic — no nearest-color math.
6. **`scene_summary` grammar** (order fixed): element counts by type ("12 shapes, 4 arrows, 3 text, 1 image"), frame list by name, connection count, text inventory (first N=10 titles, then "and M more"), then warnings ("2 elements skipped", "3 deleted elements not shown"). Non-empty even for empty/degraded scenes ("Empty drawing." / "Drawing could not be read: not valid JSON.") — §XD-A.
7. Wrapper links (text_elements raw markdown, element_links, embedded_files Vault refs) surface in `summaries[..].links`; scene `link` fields likewise. Every `LinkRef` carries kind (Wikilink | Url) + verbatim target.

### Tests (PR 3)

Unit per rule; fixture-derived snapshot of reading order + scene_summary; proptest: every non-deleted, non-label element appears exactly once in `reading_order` (projection-equivalence precursor, §XD-C).

- [ ] `derive` per rules 1–7; deterministic
- [ ] Snapshot + property tests
- [ ] fmt/clippy; host-independent

## XD0-4 · Clean-geometry SVG renderer (#679) — PR 4

`excalidraw/render.rs`: `pub fn render_svg(scene: &ExcalidrawScene, model: &ExcalidrawModel, images: &ImageSources) -> RenderedDrawing { svg: Vec<u8>, view_box: (f64,f64,f64,f64), geometry: Vec<ElementGeometry> }` where `ElementGeometry { id, x, y, width, height, angle }` in the same document coordinates as `view_box` (the AX-overlay feed for xd2-4). `geometry` contains **exactly the reading-order entries** (xd0-3 rule 1) — bound labels fold into their container's geometry, matching outline/table/census 3.

```rust
pub type ImageSources = BTreeMap<String, ImageSource>;   // keyed by fileId
pub enum ImageSource {
    Bytes { mime: String, data: Vec<u8> },   // raw-scene data URIs (decoded) or wrapper vault files (session-resolved)
    Latex { src: String },                   // wrapper $$…$$ entries ⇒ rule-8 placeholder
    Url { url: String },                     // wrapper hyperlink entries ⇒ rule-8 placeholder
    Missing { name: String },                // resolution failed / over cap ⇒ rule-7 placeholder
}
```

The **session** populates `ImageSources` (XD0-5 `open_excalidraw`; shared helper reused by XD1-1): raw scenes from `files` data URIs, wrapper scenes from `embedded_files` via link_resolver + `read_attachment` caps. The renderer stays I/O-free.

### Normative rules

1. **View box** = union of element bounds + 16 pt padding; background rect = `scene.background` (default `#ffffff`); author content renders verbatim — APCA gates apply to Slate chrome only, never to user drawings.
2. **Z-order** = element array order; when any `index` is present and array order disagrees, sort by fractional `index` (string compare per brief §1) — warn `ZOrderRepaired`, don't fail.
3. Shapes: rectangle (`roundness` → `rx` per the exact `getCornerRadius` formula pinned in brief §1: with `x = min(w,h)` — proportional/legacy ⇒ `x×0.25`; adaptive ⇒ `fixed = value ?? 32`, `cutoff = fixed/0.25`, `x ≤ cutoff ? x×0.25 : fixed`), ellipse, diamond (polygon). `fillStyle`: `solid` ⇒ fill; `hachure`/`cross-hatch`/`zigzag` ⇒ SVG `<pattern>` line fills (45° / crossed / zigzag) in `backgroundColor` — clean-geometry stand-ins, one pattern def per (style,color). Stroke: `strokeColor`/`strokeWidth`; `dashed` ⇒ `stroke-dasharray: 8 8`, `dotted` ⇒ `1.5 6`, scaled by strokeWidth.
4. Arrows/lines: polyline through `points` offset by (x, y); `elbowed` renders its stored points (no re-routing — brief §1: geometry never depends on binding math). Arrowheads: normative mapping for all released values (brief §1 list) to marker defs; unknown value ⇒ plain `arrow` + warning. Labels (bound text) render centered on the midpoint with a background-color halo.
5. Freedraw: single smoothed path through `points` (Catmull-Rom → cubic; fixed tension 0.5), stroke = strokeColor, width = strokeWidth × 1.5, round caps/joins. No pressure modulation in v1.
6. Text: split `text` on `\n` (wrap breaks are in-band, brief §1); `<text>` per line, `dy` = lineHeight × fontSize; `text-anchor` from textAlign. **Font map (normative, attribution header):** codes 3, 8, 999 ⇒ `monospace`; 1000 ⇒ default + emoji fallback; all others ⇒ `sans-serif`. No font embedding, no width re-measurement (autoResize wrapping is already baked into `text`).
7. Images: `<image>` from `ImageSource::Bytes` re-embedded as a data URI; per-image cap = `large_attachment_refuse_bytes / 10` enforced by the session when populating `ImageSources` (over-cap ⇒ `Missing`); `scale` negatives flip via transform; `crop` via nested `<svg>` viewport. `Missing` ⇒ labeled placeholder rect ("image unavailable: ⟨name⟩").
8. Placeholders (one visual grammar): embeddable/iframe ("embedded content: ⟨host⟩" — full URL in description/links, decision 6), `ImageSource::Latex` ("equation: ⟨first 40 chars⟩"), `ImageSource::Url` ("image: ⟨host⟩"), unknown types ("⟨type_name⟩") — dashed border rect + centered label in scene-appropriate contrast.
9. `angle` ⇒ `transform="rotate(deg cx cy)"`; `opacity` ⇒ group opacity ÷ 100. Bound container labels center in their container (verticalAlign honored).
10. Output is self-contained SVG 1.1 (inline patterns/markers/data URIs; **no external refs** — SwiftDraw target) and deterministic (stable id generation: `xd-⟨element id⟩` prefixes; no clocks/randomness).

### Tests (PR 4)

Snapshot SVGs for the XD0-1 fixture (golden files, reviewed once, diffed forever); unit: pattern reuse, arrowhead map totality vs. brief list, dasharray scaling, crop/flip transforms, `Missing`-placeholder; property: render never panics, output always parses as XML, **every reading-order entry appears exactly once in `geometry`** (labels folded into containers).

- [ ] Rules 1–10; self-contained deterministic SVG
- [ ] Golden snapshots + property tests
- [ ] fmt/clippy; host-independent; renderer does no I/O

## XD0-5 · FFI surface + census/bench gate (#680) — PR 5

Session + uniffi (canvas handle-API mirror, additive only):

```rust
// slate-core Session
pub fn excalidraw_kind(&self, path: &str) -> ExcalidrawKind            // NotExcalidraw | Raw | Wrapper. Extension check; md ⇒ indexed-properties lookup for the `excalidraw-plugin` key (properties_db indexes every frontmatter key; files_with_property_key session.rs:1533); NO properties row (unscanned/stale) ⇒ bounded frontmatter sniff (frontmatter region only, never the whole file). Consumed by XD2-1 routing.
pub fn open_excalidraw(&self, path: &str) -> Result<u64, VaultError>   // routes: ".excalidraw" ⇒ parse; md + excalidraw-plugin frontmatter (real bytes, not the index) ⇒ parse_wrapper; else Err(VaultError::InvalidPath { path, reason: "not an Excalidraw drawing" }) — reuse the existing variant (lib.rs:121 carries reason), no new VaultError case. Reads via provider with large_attachment_refuse_bytes cap. Converts WrapperError::DecompressFailed into the degraded empty scene + warning (xd0-2 rule 3). Populates ImageSources (xd0-4) from files / embedded_files BEFORE render so render stays pure.
pub fn close_excalidraw(&self, handle: u64)                            // idempotent (close_canvas precedent :4542)
pub fn excalidraw_svg(&self, handle: u64) -> Result<Vec<u8>, VaultError>
pub fn excalidraw_geometry(&self, handle: u64) -> Result<Vec<ExcalidrawElementGeometry>, VaultError>
pub fn excalidraw_outline(&self, handle: u64) -> Result<Vec<ExcalidrawOutlineRow>, VaultError>   // reading order projected: id, depth, type_label, title, frame_name, in_count, out_count, color_label, size_pt, links, position — the full inspectability payload xd2-2/xd2-5 phrase from
pub fn excalidraw_table_rows(&self, handle: u64) -> Result<Vec<ExcalidrawTableRow>, VaultError>  // id, type_label, title, frame_name, in_count, out_count, color_label, size
pub fn excalidraw_neighbors(&self, handle: u64, element: String) -> Result<Vec<ExcalidrawNeighbor>, VaultError>
pub fn excalidraw_description(&self, handle: u64) -> Result<String, VaultError>                  // scene_summary
pub fn excalidraw_warnings(&self, handle: u64) -> Result<Vec<String>, VaultError>                // banner strings
```

uniffi mirrors are 1:1 records (no logic), `#[uniffi::export]` on `VaultSession`; regenerate bindings (`make regenerate-bindings`). **CLI check:** `slate.cli.v1` exposes none of these (M-5 surface is read/list/search/links/properties) — note the non-impact in the PR. **Deliberate divergence from the canvas mirror:** there is **no `excalidraw_where_am_i`** — mode and zoom are UI state, so XD2-5 composes the readback Swift-side from the outline row + viewport (recorded here so the asymmetry never reads as an omission).

**Censuses** (`crates/slate-core/src/excalidraw/tests.rs` + `session/tests/excalidraw.rs`):

1. `census_excalidraw_tolerant_parse` (§XD-B): random structured mutations of valid scenes (drop/retype fields, unknown fields/types, both binding schemas, master-shape arrows/freedraw, truncated JSON) — never panics; not-JSON ⇒ ParseFailed; else per-entry skips only. Random + exhaustive small-case sweep (adversarial-census methodology).
2. `census_excalidraw_reading_order` : model invariants at scale — every non-deleted non-label element exactly once; frame members contiguous under their frame; order equal across runs and across (parse ∘ serialize-fixture) permutations of unknown fields.
3. `census_excalidraw_projection_equivalence` (§XD-C): outline ids == table ids == geometry ids == reading order (bound labels excluded everywhere — folded into containers per xd0-3 rule 2); adjacency symmetric (A outgoing B ⟺ B incoming A).
4. `census_excalidraw_read_only` (§XD-E): open/read/render/close over a fixture vault ⇒ every vault byte identical (hash before/after).

**Benches** (`benches/excalidraw_bench.rs`, criterion; synthetic scenes 100/1k/5k elements): parse+derive p50 < 50 ms @1k · render < 100 ms @1k · wrapper decompress+parse+derive < 80 ms @1k · core resolve→SVG < 250 ms @typical (≤ 200 elements, decision 12). Record in `BENCHMARKS.md`. Assert the scan bench + save-path tests are untouched (§XD-D: XD adds no scanner/save code — the diff proves it).

- [ ] Session API + uniffi mirrors + regenerated bindings; CLI non-impact noted
- [ ] Censuses 1–4 clean incl. one `SLATE_CENSUS_FULL=1` release run in the PR description
- [ ] Bench baselines in BENCHMARKS.md
- [ ] fmt/clippy clean

**Wave-1 exit:** all four censuses clean, baselines recorded, bindings regenerated, no scanner/save deltas.
