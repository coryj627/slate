# T2 executable spec — Container + primary AT surfaces (Wave 2)

Issues: #369 (entry/routing) · #518 (announcement coordinator) · #362 (outline) · #519 (AccessibleDataGrid v2) · #363 (table).
Every issue satisfies 08 DoD §A–§G + 09 deltas §H–§L + the [t0 contract](t0_interaction_contract.md). One PR per issue.

**Execution order: #369 → #518 → #362 → #519 → #363** (#518 before the surfaces that phrase through it; #519 before #363).

---

## Shared architecture

```
apps/slate-mac/Sources/SlateMac/Canvas/CanvasDocument.swift    (#369)  loads model via FFI; owns per-canvas state
apps/slate-mac/Sources/SlateMac/Canvas/CanvasContainerView.swift (#369) hosts surface switcher
apps/slate-mac/Sources/SlateMac/Canvas/CanvasSelection.swift   (#369)  shared selection + marks (published on AppState)
apps/slate-mac/Sources/SlateMac/Canvas/CanvasAnnouncer.swift   (#518)
apps/slate-mac/Sources/SlateMac/Canvas/CanvasOutlineView.swift (#362)
apps/slate-mac/Sources/SlateMac/AccessibleDataGrid.swift       (#519, upgraded in place)
apps/slate-mac/Sources/SlateMac/Canvas/CanvasTableView.swift   (#363)
```

`CanvasSelection` is the single source of truth all surfaces bind to: `selected: NodeID?`, `marked: Set<NodeID>` (#524 populates later), `activeSurface: CanvasSurface`. Mutating it is the only way to change selection — surfaces never hold local selection.

**Multi-pane scoping:** `CanvasSelection` lives on `CanvasDocument`, and — following the U1 `NoteDocument` registry pattern — there is **one `CanvasDocument` per open path**, shared by every pane/tab showing that canvas (panes showing the same canvas share selection and marks, like panes showing the same note share content). `AppState` mirrors the *focused pane's* document for global consumers (#518, palette enablement). Marks clear when the last tab for that path closes.

**The three switchable surfaces are Outline, Table, and Visual.** The "keyboard navigator" (#364) is deliberately **not a fourth view**: it is the canvas-wide command layer (next/prev, follow-connection, enter/exit group, trace path…) hosted by *every* surface and operating on `CanvasSelection`. `activeCanvasSurface` persists one of the three; there is no "Show Navigator" command.

## #369 — Entry point, routing, states, focus

1. **`EditorItem.canvas(path:)` activation** (first deliverable, own commit). The real seams (there is no literal commented-out case): add the case to `EditorItem` + its Codable discriminator; visit every `EditorItem` switch; extend `WorkspaceStore.FailableTab`'s kind whitelist (that's where tolerate-and-drop actually lives) and invert its drop test into a round-trip test, keeping a *forward*-compat test proving an older-build decode still drops the tab gracefully; `SlateSymbol` canvas tab glyph; openTab dedup treats equal paths as one tab; per-tab persistence gains an **optional, additive** `activeCanvasSurface` field on the tab snapshot (old snapshots decode with `nil` → default outline) so the outline/table/visual choice restores with the session.
2. `.canvas` files appear in the file tree / quick open — the backend filter change (today `session.rs` returns `.md` only) is **#361's scope**; this issue consumes it.
3. Selecting a `.canvas` presents `CanvasContainerView`; **default landing = outline** (structured-first); surface switching commands (Show Outline / Show Table / Show Visual) registered in `CommandSection.canvas`. **The `CommandSection.canvas` FFI enum case lands here** (first Wave-2 PR; cross-language Rust + regenerated bindings, backend-labeled commit) — Wave 2 registers real commands, so the section cannot wait for #364.
4. **Dirty/autosave policy (decided):** canvas mutations write through on commit (each committed action serializes + saves via #366). A canvas tab is therefore never "dirty" in the U1 close-gate sense; the close gate is bypassed for canvas tabs and this is asserted in a test. Conflicts surface per t0 §5.
5. Empty + parse-error states per `OutlineSidebar.emptyState` / `noteLoadError` conventions; the **empty state is actionable onboarding**: a focusable region naming the create path. Wave-2 copy: "Canvas is empty. Open the Command Palette (⌘⇧P) — every canvas action is there." (#368 updates it to lead with "Press ⌥⌘N to create your first card" when New Card ships in Wave 4 — don't advertise a command that doesn't exist yet.) Malformed canvas → t0 §5 warning surface, never a blank window.
6. **Focus:** opening a canvas moves VO/keyboard focus to the outline root; returning from an opened card restores focus to that card's row (WCAG 2.4.3).

**Tests:** routing (.canvas → container, .md → editor unchanged); store round-trip + forward-compat; sub-surface restore; empty/malformed states; focus landing + restoration; close-gate bypass.

## #518 — Announcement coordinator + verbosity + Where am I?

Implements t0 §1 in full: grammar assembly from backend-provided data (`canvas_where_am_i`, `CardSummary`, `RelativeDesc`), coalescing (~200 ms class-keyed debounce), bulk summaries, polite/assertive priorities, verbosity preference (persisted with existing settings; live-switchable), ⌃⌘I Where-am-I command + focusable transient panel. Exposes `announce(_ event: CanvasEvent)` — the only announcement API canvas code may use (lint/test guards direct `postAccessibilityAnnouncement` calls under `Canvas/`).
**Tests:** grammar strings per verbosity × event type (table-driven from t0 §1.2/1.3); coalescing; bulk; Where-am-I string + panel focusability.

## #362 — Accessible outline

As issued (hierarchy, per-type labels, rotors for cards/groups/connections, live regions via #518, selection drives `CanvasSelection`, activation opens the card) plus:
- **"N of M in ⟨group⟩"** in row AX values (t0 §1.2 standard level); connection rows use direction phrases (t0 §1.2) honoring `fromEnd`/`toEnd`.
- Media/image cards use the t0 §1.1 description derivation (the issue's "media/image card descriptions" addendum; gap G25) — never a raw path.
- **Text-card activation (Waves 2–3 interim):** opens a focusable **read-only text detail panel** (plain rendered content) — the real editor arrives with #368 in Wave 4 (U3-gated). #362's activation test asserts the interim panel; #368 swaps it.
- **Marked/dirty inspectability** (t0 §3): row AX value includes "marked" when marked (#524 arrives later; the value slot ships now).
- Alt-text extension: filename-derived alt is the floor; a Slate extension field (preserved via #359 `unknown`) is an **explicitly deferred decision** — logged in gap_analysis.md, not silently skipped.
- Virtualized (2,000-node fixture responsive; §K).

**Tests:** label strings per type incl. subpath + image derivation; rotor membership; N-of-M values; selection propagation both directions; activation per kind (file → note tab, link → browser, text → detail); 2,000-node responsiveness benchmark.

## #519 — AccessibleDataGrid v2

As issued (#519 body is the contract): NSTableView-backed, sorting + sort announcements + `NSAccessibilitySortDirection`, keyboard row model (arrows/Home/End/type-ahead/Return/row actions), "Row N of M" values, virtualization at 2,000 rows, generic for canvas + Bases; existing call sites migrate green.
**API note:** the current `columns: [(header, (Row) -> String)] + rows + summary` API cannot express this — v2 **extends** it (column descriptors gain optional sort comparators; new optional `selection` binding, `onActivate`/row-action callbacks, and an injectable `announce: (String) -> Void` hook). Existing call sites compile unchanged via defaults. **#363 injects the #518 coordinator as the announce hook** so table announcements ride the canvas funnel (DoD §H) — the grid itself stays canvas-agnostic.

## #363 — Canvas table

As issued: columns Type · Title · Group · Target · Connections · **Color** (named preset; #370's sortable color column lands here), reading `canvas_table_rows`; row activation opens, selection syncs `CanvasSelection`; sort announcements via #518.
**Tests:** rows match model; sort per column incl. color; selection sync; activation; 2,000-row benchmark (with #519's).

---

## Acceptance (wave close)

A VoiceOver user can open a `.canvas` from the file tree, land on the outline, read the entire structure with rotors and N-of-M context, switch to the table and sort it, and open any card — keyboard-only, with every string conforming to t0. a11y-check 100/100; APCA gates; benchmarks recorded.
