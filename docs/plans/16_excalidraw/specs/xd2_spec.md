# XD2 executable spec — Viewer tab: three projections + navigation

Issues: XD2-1 ([#683](https://github.com/coryj627/slate/issues/683)) · XD2-2 ([#684](https://github.com/coryj627/slate/issues/684)) · XD2-3 ([#685](https://github.com/coryj627/slate/issues/685)) · XD2-4 ([#686](https://github.com/coryj627/slate/issues/686)) · XD2-5 ([#687](https://github.com/coryj627/slate/issues/687)). Milestone: [GH 34](https://github.com/coryj627/slate/milestone/34). One PR per issue. Gate: Wave 2 merged.
Program: [00_program.md](../00_program.md) (decisions 5–7, 9–12; §XD-A–§XD-E). The canvas container/surfaces are the architectural template throughout; this spec names deltas, not repetition — when silent, do what the canvas surface does (t2/t3 specs).

Baseline facts (verified 2026-07-05, this worktree):

- `EditorItem` (Workspace/WorkspaceModel.swift:42): `markdown(path:) | canvas(path:)`, Codable via `kind` discriminator (:54–63); rename retarget precedent `WorkspaceState.swift:238`; restore mapping `WorkspaceStore.swift:311`.
- Routing funnel: `AppState.openFile(_:target:)` (AppState.swift:799) — "every open path routes here"; canvas arm (:807) `path.lowercased().hasSuffix(".canvas") ⇒ openCanvasFile`. Canvas tab lifecycle: `AppState+Canvas.swift` (:31–45 open/replace/new-tab; :109 close-tracking).
- Canvas container/mode pattern: `Canvas/CanvasContainerView.swift` (surface toggle, one coherent AX tree per mode), `CanvasDocument.swift` (per-path document owning the core handle), `CanvasSelection.swift` (shared selection), `CanvasAnnouncer.swift` (one announcement coordinator), `CanvasOutlineView.swift`, `CanvasTableView.swift` (AccessibleDataGrid v2, #519), `CanvasRendererView.swift` (`CanvasViewport`: `view = (canvas − offset) × scale`, min 0.1 / max 4.0; per-card `NSAccessibilityElement`s with frame invalidation on pan/zoom — #367).
- Command plumbing: core **`CommandRegistry`** struct (commands.rs:106), FFI mirror (slate-uniffi/src/lib.rs:3621); **`CommandSection`** is a `#[repr(u8)]` FFI enum (commands.rs:38, `Canvas = 8` — "adding a section is a deliberate edit"). `SlateCommands.swift` registers canvas commands with `section: .canvas`; the palette renders via `CommandPaletteModel.swift`. Drift tests in `SlateCommandsTests.swift`: registration forward, menu-scrape reverse (#330), canvas focus-routing (~:255), help-table (#526, ~:1024). (No command-*ID* enum exists — FL decision 15's note was about IDs, not the registry.)
- Tab dedup today is **path-only**: `activeGroupTab(forPath:)` matches `$0.item.path == path` with no kind check (WorkspaceState.swift:121–123) — XD2-1 rule 1 makes it kind-aware; rename `retarget` is already per-item-kind (WorkspaceState.swift:238).
- Canvas reload is **mutation-driven only** (`reloadAfterMutation`, CanvasDocument.swift:164, called from write ops); there is **no external-change reload seam** — a read-only viewer therefore has none either (rule 4).
- FFI available from Wave 1: `excalidraw_kind`, `open_excalidraw/close_excalidraw`, `excalidraw_svg/geometry/outline/table_rows/neighbors/description/warnings` (xd0_spec). No `excalidraw_where_am_i` — deliberate; readback composes Swift-side (xd0-5 note).
- Right-pane leaves registry: `RightPaneView.swift` (U4) — **not used by XD** (no XD leaf; noted to preempt scope creep).

Shared architecture (all five issues): `ExcalidrawDocument` (one per open path; owns handle, opens on init, `close_excalidraw` on deinit/tab-close — canvas Document lifecycle), `ExcalidrawSelection` (`ElementId?` + `activeSurface`), `ExcalidrawAnnouncer` (all strings funnel through it; t0 grammar). Selection syncs across surfaces (decision 5). Mode toggle = U3 reading/editing pattern: exactly one surface in the AX tree at a time.

---

## XD2-1 · Tab entry + routing (#683) — PR 1

1. `EditorItem.excalidraw(path:)`, Codable `kind: "excalidraw"`; extend `path` accessor, rename-retarget (WorkspaceState.swift:238 pattern), restore mapping (WorkspaceStore.swift:311), tab icon (SF Symbol, distinct from canvas) + AX tab label "⟨name⟩, drawing". **Tab dedup becomes (kind, path)-aware**: add a kind-filtered variant of `activeGroupTab(forPath:)` (:121–123 is path-only); the funnel activates only a *same-kind* existing tab; "Open as Markdown" dedups on `(.markdown, path)`. This is what makes viewer + markdown tabs for one wrapper path coexist coherently.
2. Funnel arm after the canvas arm (:807): `.excalidraw` suffix ⇒ `openExcalidrawFile(path, target:)` (mirror `AppState+Canvas.swift:31–45`). **Wrapper detection:** md paths consult `excalidraw_kind(path)` (Wave-1 FFI, xd0-5: indexed-properties lookup + bounded frontmatter sniff when no row exists — a just-created, not-yet-scanned wrapper still routes correctly). Wrapper ⇒ viewer tab (decision 9). **Stale-positive path pinned:** if the index said Wrapper but `open_excalidraw`'s real-bytes sniff disagrees, the viewer tab shows the degraded state with Open as Markdown available — never a crash, never a silent markdown fallback.
   **`CommandSection::Excalidraw = 9` lands in this PR** (first command-registering PR: Open as Markdown, Refresh) — cross-language enum edit + `make regenerate-bindings` (decision 10; canvas R16 precedent). Registration/menu drift tests extend here; the help-table drift entry lands with XD3-1 alongside the help file.
   **Quick-open:** add `FileFilter::OpenableDocuments` (markdown + canvas + excalidraw; additive — `MarkdownAndCanvas` stays for existing callers) in core (session.rs:138/:3375) and switch the palette call site (AppState.swift:3221) to it (decision 9; canvas gap-R7 precedent).
3. **Open as Markdown** (palette + tab context menu, wrapper tabs only): opens `EditorItem.markdown(path:)` directly (bypasses the funnel's sniff — the *command* is the escape hatch, the funnel stays deterministic). Both tabs may coexist (rule 1 dedup); the markdown tab is a normal note tab in every respect. This PR also adds the **Open Drawing** header action to the embed disclosure slot XD1-2 shipped (xd1-2 rule 5).
4. `ExcalidrawContainerView`: title bar (name + degraded-warning banner when `excalidraw_warnings` non-empty), mode toggle **Visual | Outline | Table** (default: **Outline** — accessible-first default; last mode per path in device-local UserDefaults), Refresh command re-opens the handle. **No automatic external-change reload in v1** (verified: canvas reload is mutation-driven only, CanvasDocument.swift:164, and a read-only viewer has no mutations) — Refresh is the path; xd3's help page documents it. Build no watch infrastructure.
5. Empty/degraded states: honest text ("Empty drawing", "Drawing could not be read — showing nothing") + description text always present (§XD-A).

Tests: routing unit (raw, wrapper, plain md unaffected, `excalidraw_kind` sniff-fallback for unscanned wrapper, stale-positive degraded path), kind-aware dedup (viewer + markdown tab coexistence; funnel prefers same-kind), tab restore round-trip, rename retarget, quick-open lists drawings (`OpenableDocuments`), drift tests green with the new section. a11y-check + APCA on tip.

- [ ] 1–5 · routing tests · a11y/APCA gates

## XD2-2 · Outline surface (#684) — PR 2

1. `ExcalidrawOutlineView` fed by `excalidraw_outline` (reading order, xd0-3 rule 1): virtualized list, frames as disclosure groups; row label grammar (normative): "⟨type_label⟩ ⟨title⟩, ⟨n⟩ of ⟨m⟩ in ⟨frame|drawing⟩" + suffixes ", ⟨k⟩ connections" and ", linked" when present. Full detail in AX value (inspectability: color_label, size, position ninth, links) — pull, not push.
2. Row activation ⇒ select (sync all surfaces + announce via coordinator); expand/collapse frames with standard disclosure AX.
3. **Links submenu** per row (context menu + AX custom actions): each `LinkRef` ⇒ "Open ⟨target⟩" routing through `openFile` (decision 7); URL refs open in browser (existing external-open path).
4. Type filter menu (All / Shapes / Text / Arrows / Images / Frames / Other) — filters rows, announces "Showing ⟨n⟩ of ⟨m⟩".

Tests: label grammar snapshots from the xd0 fixture; activation sync; filter counts; VO smoke in AT checklist (xd3).

- [ ] 1–4 · grammar snapshots · a11y/APCA gates

## XD2-3 · Table surface (#685) — PR 3

`ExcalidrawTableView` on **AccessibleDataGrid v2** (#519 — canvas `CanvasTableView.swift` as template). Columns: Type · Title · Frame · In · Out · Color · Size. Sortable per column (announcements through the coordinator, injected-announcer pattern); row activation selects + syncs; selection sync inbound highlights the row. No color-only column: Color shows `color_label` text.

Tests: sort determinism vs. model order; sync both directions; grid AX audit (grid semantics from #519 carry over).

- [ ] Grid + columns + sync · tests · a11y/APCA gates

## XD2-4 · Visual surface (#686) — PR 4

1. `ExcalidrawRendererView` (NSView): draws the XD0-4 SVG via SwiftDraw into a backing image, re-rasterized on zoom-tier change (0.5×/1×/2×/4× tiers — pan never re-rasterizes); reuse `CanvasViewport` semantics verbatim (same transform, min/max, zoom-at-center; factor out or mirror — implementer's call, but behavior identical and drift-tested by the shared viewport unit tests if factored).
2. **Per-element AX** (#367 pattern, non-negotiable): one `NSAccessibilityElement` per reading-order entry from `excalidraw_geometry` × viewport transform; label = outline row label; frames invalidated on every pan/zoom; windowing at viewport + one-margin beyond (canvas contract). Above **1,500 visible elements**: summary AX element that names Outline/Table as the path (Graph decision 5 tiering, honest not fake).
3. Keyboard: arrows move selection in reading order (plain-arrow scoping rule — only while the surface has focus; VO Quick Nav caveat per canvas R2), focus ring in screen-space overlay (never scaled), selection scrolls into view; Return on a linked element = Open link; no drag, no edit (§XD-E).
4. Selection sync + zoom announcements through the coordinator ("Zoom 150 %", terse).
5. Author content renders verbatim (xd0-4 rule 1); surface *chrome* (focus ring, banner, toggle) uses tokens and meets APCA both appearances.

Tests: AX-frame invalidation under pan/zoom (canvas #367 test pattern), tier fallback at >1,500, geometry→AX 1:1 (§XD-C at the UI edge), keyboard reading-order walk.

- [ ] 1–5 · AX/viewport tests · a11y/APCA gates

## XD2-5 · Commands, navigation, Where-am-I (#687) — PR 5

1. Palette + menu commands under `CommandSection.excalidraw` (enum landed in XD2-1; no new global chords — decision 10): Toggle View Mode (cycle) · Zoom In/Out/Actual/Fit/Zoom-to-Selection (canvas chords ⌘= ⌘- ⌘0 etc., **scoped to excalidraw focus**; drift test extended to assert scoped reuse, not collision) · Where Am I? (⌃⌘I scoped) · Next/Previous Element · Enter/Exit Frame · Follow Connection Forward/Back · Open Link… · Open Drawing Externally (NSWorkspace, raw + wrapper) · Open as Markdown (wrapper) · Refresh. Help-table drift entries for the section land with XD3-1 (the help file's PR).
2. Where-am-I readback (pull, braille-friendly — canvas #518 grammar): "⟨type⟩ ⟨title⟩, ⟨n⟩ of ⟨m⟩ in ⟨frame⟩, ⟨in⟩ incoming, ⟨out⟩ outgoing, ⟨color⟩, ⟨position⟩. Viewing ⟨mode⟩, zoom ⟨z⟩ %."
3. Follow-connection uses `excalidraw_neighbors`; multiple neighbors ⇒ picker (canvas connect-picker interaction pattern, read-only); traversal announces the arrow label when present.
4. Announcement grammar + verbosity: reuse the canvas announcer contract (t0 §1) — one coordinator, no doubling, on-demand detail.

Tests: command registration census (every command reachable via palette), drift test green, neighbor traversal unit, Where-am-I grammar snapshots.

- [ ] 1–4 · command/drift/grammar tests · a11y/APCA gates

**Wave-3 exit:** all five issues merged; a keyboard-only user and a VO user can open a drawing, understand it via outline/table/Where-am-I, walk the visual surface, and follow its links — with zero mouse and zero sight of the visual surface required for any datum (§XD-C).
