# 16 — Excalidraw Viewer Program (Milestone XD): drawings a screen-reader user can actually read

**Status:** 📝 Specs locked (2026-07-06); implementation not started. GH [milestone 34](https://github.com/coryj627/slate/milestone/34), issues [#676–#688](https://github.com/coryj627/slate/milestone/34) (XD0: #676–680 · XD1: #681–682 · XD2: #683–687 · docs: #688). Evidence base: [`01_research_brief.md`](01_research_brief.md) — primary-source verification of both Excalidraw schemas (released 0.18.x + unreleased master), the Obsidian plugin's `.excalidraw.md` wrapper (compression **executably proven** compatible with the `lz-str` crate), licenses, and Excalidraw's documented canvas-inaccessibility ([excalidraw#5759](https://github.com/excalidraw/excalidraw/issues/5759), [#7492](https://github.com/excalidraw/excalidraw/issues/7492) Deque audit).

**Strategic goal.** Ship a **read-only Excalidraw viewer**: `![[drawing.excalidraw]]` embeds render inline in notes, and opening a drawing gets a workspace tab with three synchronized projections — visual render, accessible outline, and table — of **one scene model** derived in Rust. An `.excalidraw` scene is not opaque data: text elements carry strings, shapes carry type/color/geometry, frames and groups carry structure, and arrows carry `startBinding → endBinding` element references — a drawn flowchart *is* a directed graph. Excalidraw's own canvas hides all of that from assistive tech (research brief §4); this viewer extracts it. Excalidraw + the obsidian-excalidraw-plugin are the **capability and file-format reference, not the interaction reference** (Canvas `../09_canvas/00_program.md` / Graph `../11_graph/00_program.md` stance). The viewer must be *more* accessible than Excalidraw itself — that is the point, not a stretch goal.

Everything here inherits the UI-parity Presentation-Ready DoD (`../08_ui_parity/00_program.md` §A–§G): a11y-check 100/100 gated on each PR's own tip, APCA Lc ≥ 75 measured in both appearances, census-gated invariants, one PR per issue, fmt/clippy pre-push. This document adds only what is Excalidraw-specific.

---

## Locked scope decisions (owner review, 2026-07-05)

| # | Area | Decision |
|---|------|----------|
| 1 | Identity | **Milestone XD — Excalidraw viewer**, read-only. Specs in `docs/plans/16_excalidraw/`; phase prefixes XD0–XD3. Viewing only: no serializer, no mutation FFI, no editing surfaces. An accessible drawing *editor* is a separately-evaluated future milestone; nothing in XD may block it, and nothing in XD may ship a write path. |
| 2 | File forms | **Both** (owner decision): raw `.excalidraw` JSON **and** the Obsidian plugin's `.excalidraw.md` wrapper (frontmatter + `compressed-json`/`json`/fenceless Drawing section + Text Elements / Element Links / Embedded Files sections; legacy level-1 headings; `%%` placement variants — brief §2). Detection for `.md` files is the **frontmatter key `excalidraw-plugin:`** (any value), never the filename. Wrapper decompression via the pinned `lz-str` crate (MIT/Apache-2.0; round-trip proven, brief §3) + a golden-file corpus test. |
| 3 | Visual fidelity | **Clean geometric rendering in v1** (owner decision): exact geometry, colors, fills, stroke styles, arrowheads, text, rotation, opacity — legible and layout-faithful, without roughjs sketchiness. Sketchy parity is reserved enhancement **XD-E1** (feasible: per-element `seed` + Park–Miller LCG, brief §5) and must slot in as a renderer-internal change — no model or FFI shape may assume clean-only. |
| 4 | Parse-on-open, no index | No SQLite schema, no migration, no scanner hook. Drawings parse on open through a handle-based session API (canvas `open_canvas`/`canvas_scene`/`close_canvas` precedent, slate-uniffi/src/lib.rs:3672). Embeds resolve per-request like images (`resolve_embed` → `read_attachment` path). If a future feature needs drawing queries at rest, indexing is its own decision then. |
| 5 | One model, three projections | One Rust `ExcalidrawModel` (reading order, frame tree, adjacency from arrow bindings, per-element summaries, scene summary) feeds visual, outline, and table through one FFI surface, selection-synchronized. **Reading order mirrors the canvas rule**: frames as containers, depth-first, `(y, x, array order)` within a container (canvas model.rs precedent) — spatial reading order, not z-order. No projection-private data (Graph decision 2). |
| 6 | Never visual-only | Every datum reachable in the visual surface is reachable in outline/table; per-element AX on the visual surface (Canvas #367 precedent). Freehand strokes without text/bindings get an **honest** summary ("freehand stroke, ~200×150 pt, upper left") — never a fabricated description. `embeddable`/`iframe` elements render as static placeholders naming their host (full URL in the description and links list) — **no live web content** (Canvas decision 10). |
| 7 | Links follow | Element `link` fields, wikilinks in wrapper text elements, and Embedded Files targets are first-class: outline/table expose them and activation routes through the standard `openFile` funnel (AppState.swift:799). Link *indexing* of `.excalidraw.md` files already works today — the wrapper duplicates all links as plain markdown precisely so indexers see them (brief §2); XD adds zero link-graph work, and xd1 verifies backlinks-from-drawings in a test. |
| 8 | Images in drawings | Raw `.excalidraw`: decode `files` data URIs, subject to a per-image byte cap (reuse `large_attachment_refuse_bytes` config). Wrapper: resolve `## Embedded Files` wikilinks through the vault (`read_attachment`, same caps). Failure/oversize ⇒ labeled placeholder, never a hole. LaTeX Embedded Files (`$$…$$`) render as a labeled equation placeholder in v1 with the source in the description (reuse of the K math pipeline is enhancement XD-E2). |
| 9 | Routing & md ambiguity | `.excalidraw` paths take a new arm in the `openFile` funnel (canvas arm precedent, AppState.swift:807). `.md` files whose frontmatter says `excalidraw-plugin` open in the **viewer by default** with a per-tab **"Open as Markdown"** command as the escape hatch (the wrapper is a legitimate markdown note; never trap the user in the viewer). Both arrive via the same funnel; file tree, links, and recents need no special cases. Quick-open does: its listing uses `FileFilter::MarkdownAndCanvas` (session.rs:138/:3375; consumed AppState.swift:3221), so XD2-1 extends the filter to include drawings (canvas gap-R7 precedent — additive arm, existing callers untouched). |
| 10 | Commands, no new chords | Every viewer action registers in the core **`CommandRegistry`** (commands.rs:106; FFI mirror slate-uniffi/src/lib.rs:3621) under a new **`CommandSection::Excalidraw = 9`** (`#[repr(u8)]`, commands.rs:38 — "adding a section is a deliberate edit"; the cross-language enum change + bindings regen land with **XD2-1**, the first PR that registers a command — canvas R16 precedent). The palette renders via `CommandPaletteModel.swift`; note FL decision 15's "no CommandRegistry" was about a command-*ID* enum, which indeed doesn't exist — the registry object does. Viewport commands reuse the canvas chord set (⌘= / ⌘- / ⌘0, fit, zoom-to-selection) and Where-am-I (⌃⌘I) **scoped to excalidraw-surface focus**; all three drift tests (registration forward, menu-scrape reverse #330, help-table #526 — SlateCommandsTests.swift) extend to the new section. **Zero new global chords.** |
| 11 | Degraded loads | Tolerant parse per the canvas parser contract (canvas/mod.rs): entry-level problems yield warnings + skipped entries and never hard-fail; only not-JSON/unusable-root degrades the whole load. Read-only means degraded = still viewable (whatever parsed) + a warning banner listing skips. Both binding schemas (focus/gap and fixedPoint/mode) parse; unknown fields ignored everywhere (brief §1 caveat). |
| 12 | Scale budgets | Benches at 100 / 1k / 5k elements (BENCHMARKS.md convention): parse+derive p50 < 50 ms @1k; SVG render < 100 ms @1k off-main; embed **core resolve→SVG** < 250 ms @typical (≤ 200 elements) — Swift-side rasterization is measured in the xd3 E2E, not criterion-gated. Tiering: the visual surface's **> 1,500-visible-element AX summary tier** (xd2-4 rule 2) is *the* mechanism routing very large drawings to outline/table (Graph decision 5 stance); 5k-element scenes render best-effort. |
| 13 | Licensing | Format re-implemented from primary sources; no code copied. excalidraw MIT, roughjs MIT, obsidian-excalidraw-plugin **AGPL-3.0** (brief §3 — compatible with Slate's AGPL-3.0-or-later), `lz-str` MIT/Apache-2.0. Attribution header on any ported constant tables (font-code map). |
| 14 | l10n | Out of XD scope; the l10n program (`../14_l10n.md`) owns string externalization. Don't hardcode user-facing strings in ways that block it. |

---

## Phase map, waves & dependencies

```
Wave 1 (backend)   XD0-1 .excalidraw parser ─▶ XD0-3 model+description ─▶ XD0-4 SVG renderer ─▶ XD0-5 FFI + census/bench gate
                   XD0-2 .excalidraw.md wrapper ─┘   (XD0-2 needs XD0-1's scene types; parallel with XD0-3 otherwise)
Wave 2 (embeds)    XD1-1 core embed routing ─▶ XD1-2 EmbedView rendering
Wave 3 (viewer)    XD2-1 tab entry/routing ─▶ XD2-2 outline ─ XD2-3 table ─ XD2-4 visual surface ─ XD2-5 commands/navigation
Wave 4 (close-out) XD3-1 docs + AT checklist + E2E corpus
```

| Wave | Issues | Gate |
|------|--------|------|
| 1 — Backend core | XD0-1 → XD0-2, XD0-3 → XD0-4 → XD0-5 | none (pure slate-core; start any time) |
| 2 — Embeds | XD1-1 → XD1-2 | XD0-5 merged (FFI + gate) |
| 3 — Viewer tab | XD2-1 → XD2-2 ∥ XD2-3 ∥ XD2-4 → XD2-5 | Wave 2 (reuses embed plumbing + proves the render path); XD2-3 uses `AccessibleDataGrid` v2 (#519, landed) |
| 4 — Close-out | XD3-1 | Wave 3 |

**Sequencing vs. other programs:** XD takes no dependency on P (graph), V, X, or FL, and none of them depend on XD. Wave 1 is pure slate-core and can interleave with anything. The owner sequences XD against the standing queue (P implementation, FL implementation) at execution time; nothing in this program presumes a slot.

**Priority note:** if capacity forces a cut line, cut from Wave 3 backward — Waves 1–2 alone already deliver "view embedded drawings in notes," the highest-value slice.

**Reserved enhancements (not in any XD issue; each is its own future decision):** XD-E1 roughjs-parity sketchy rendering (brief §5 port notes) · XD-E2 LaTeX embedded-file rendering via the K math pipeline · XD-E3 editing (separate milestone-scale decision).

---

## Definition of Done — XD-specific deltas

- **§XD-A (structured description):** every drawing surface exposes a non-empty structured description even on degraded/failed loads (Mermaid `structured_description` precedent, diagram.rs:53) — counts by type, frame names, text inventory, connection list.
- **§XD-B (tolerant-parse census):** adversarial corpus census — random mutations of valid scenes (dropped fields, wrong types, unknown fields, both binding schemas, master-branch shapes) must never panic, never hard-fail entry-parse, and always produce warnings + a viewable remainder. Fixture corpus is format-faithful per brief §2 (compressed + uncompressed + legacy headings + fenceless + CRLF) and includes the executably-proven compression fixture from brief §3; genuine plugin-written captures are added as they become available.
- **§XD-C (projection equivalence):** outline/table/visual expose the same element set, titles, and adjacency (census-checked model-side; UI smoke-checked per surface).
- **§XD-D (budgets):** decision-12 numbers recorded in BENCHMARKS.md at wave close; no scan regression (XD touches neither the scanner nor the save path — the scan-bench diff and save-path tests prove it stays that way).
- **§XD-E (read-only invariant):** no XD code path writes vault bytes. Census-adjacent test: viewer session over a fixture vault leaves every file byte-identical.

Specs: [xd0](specs/xd0_spec.md) · [xd1](specs/xd1_spec.md) · [xd2](specs/xd2_spec.md) · [xd3](specs/xd3_spec.md) · [gap analysis](specs/gap_analysis.md).
