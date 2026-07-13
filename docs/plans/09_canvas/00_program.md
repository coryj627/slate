# 09 — Accessible Canvas Program (Milestone T): the first canvas a blind author can use

**Status:** 🏁 Code-complete (2026-07-05) — all 25 issues (#359–#373 + #517–#526) shipped across PRs #600–#628 in five waves (backend → entry/announce/outline/grid/table → navigator/undo/renderer/viewport → authoring/modes/connect/marks → parity/color/type/filter/docs/close-out). Gates met on every PR: a11y-check 100/100, APCA Lc ≥ 75 both appearances, census-gated invariants, byte-stable undo. Two adversarial-review (Codex) red-teams ran on the riskiest surfaces (#521 transient geometry, #367 AX-frame invalidation) and a post-merge adversarial pass caught two data-loss holes in card authoring — all fixed with regression tests (last: PR #629). **Residual (human-only):** the manual AT smoke pass ([`at_smoke_checklist.md`](at_smoke_checklist.md), t0 §4 — VoiceOver/Voice Control/Switch Control/braille/Dynamic Type/Increase Contrast/Reduce Motion on a real machine) and filing any tester-feedback issues; the GH milestone stays open until that pass completes. GH milestone 20; issues #359–#373 (original) + #517–#526 (added by the 2026-07 interview + gap review).

**Strategic goal.** Ship Obsidian-parity Canvas (`.canvas`, [JSON Canvas 1.0](https://jsoncanvas.org)) where **accessibility is the architecture, not an overlay**: one canvas model feeding four synchronized surfaces — visual renderer, accessible outline, table, and keyboard navigator — such that a **keyboard-only user can author a canvas from empty**, a **Voice Control user can operate every action by name**, and a **screen-reader/braille user can understand, navigate, and edit any canvas**. Obsidian's canvas is the reference for capability and file compatibility; it is explicitly *not* the reference for interaction, because its canvas is mouse-only and screen-reader-opaque. This milestone is the existence proof that a spatial thinking tool doesn't have to be.

Everything here inherits the UI-parity program's Presentation-Ready Definition of Done (`../08_ui_parity/00_program.md` §A–§G) — a11y-check 100/100, APCA Lc ≥ 75 measured in both appearances, census-gated invariants, atomic writes, one PR per issue. This document only adds what is canvas-specific.

---

## Locked scope decisions (user interview, 2026-07-03)

The user (a screen-reader and keyboard-only user) was interviewed against the original 15 issues plus Obsidian Canvas behavior (help docs + tutorial transcripts). These decisions are **locked**; don't re-litigate them in implementation PRs.

| # | Area | Decision |
|---|------|----------|
| 1 | New-card placement | Auto-place adjacent to the selected card, non-overlapping; canonical origin `(0,0)` on an empty canvas; every placement announced relatively ("Created text card below 'Research'"). Engine: #517. |
| 2 | Spatial move | **Both** mechanisms: (a) move mode — arrow-key grid nudge (⇧ = large step), relative-position + overlap-onset announcements, Return commits / Esc cancels (#521); (b) structural placement commands — "Place below ⟨card⟩", "Align with ⟨card⟩" via the card picker, zero coordinates (#522). |
| 3 | Renderer AX | The visual renderer publishes **per-card `NSAccessibilityElement`s** (label/frame/actions) — VoiceOver works *on* the visual surface and Voice Control "Show numbers" can target cards. Not an opaque view. (#367) |
| 4 | Multi-select | **Mark-then-act**: per-card mark toggle + a navigable marks list; group/move/delete/color act on the marked set; one summary announcement + one undo per bulk action. No shift-range selection. (#524) |
| 5 | Announcements | On-demand **"Where am I?"** full-context readback (braille-friendly, pull not push) + a **verbosity setting** (terse/standard/verbose). All canvas announcements route through one coordinator. (#518) |
| 6 | Viewport | Full keyboard command set: zoom in/out, actual size, fit canvas, zoom to selection, viewport-follows-selection toggle. (#520) |
| 7 | Text-card editing | Reuse the **real note editor** (`NoteEditorView` / post-U3 editing component) inline or in a sheet; Esc commits and returns focus to the card. No second editor to make accessible. (#368) |
| 8 | Connect flow | **Both**: filterable proximity-sorted picker (primary; sides auto-defaulted, optional label step) and a navigate-and-confirm connect mode. (#523) |
| 9 | Parity actions | In scope: create-connected-card, duplicate card, convert card→note, edit existing edge label, `#heading` subpath file cards. (#525) |
| 10 | URL cards | Static title/host card + "Open in Browser". **No live web embeds** — a deliberate divergence from Obsidian (WKWebViews inside a canvas are an a11y and focus-management liability). Documented in `docs/help/canvas.md`; embeds may return as a separately-evaluated enhancement. |
| 11 | Undo | Better than Obsidian (whose canvas undo is canvas-local toolbar buttons): every mutation is an op-log entry, ⌘Z routed by first responder, undo announced ("Undid: move 'Research'"). (#372) |

---

## Issue map, waves & dependencies

```
Wave 1 (backend)      #359 parser ─▶ #360 model ─▶ #361 schema/FFI (read + write API)
                                 └─▶ #517 placement    #366 serializer
Wave 2 (container +   #369 entry/routing ─ #518 announcer ─ #362 outline ─ #519 grid v2 ─▶ #363 table
        primary AT)
Wave 3 (nav + visual) #364 navigator ─ #367 renderer ─ #520 viewport ─ #372 undo
Wave 4 (authoring)    #368 actions ─ #521 move/resize ─ #522 picker/placement ─ #523 connect ─ #524 marks
Wave 5 (parity +      #525 parity extras ─ #370 color ─ #371 dynamic type ─ #373 filter ─ #365 E2E ─ #526 docs
        close-out)
```

Note on "four surfaces": the switchable *views* are **outline, table, and visual** — the keyboard **navigator (#364) is the canvas-wide command layer hosted by all of them**, not a fourth view (decision recorded in t2's shared architecture). The canvas **mutation FFI** (`canvas_apply` + ops) is owned by #361 and specified in t1 — Wave 4's UI issues consume it, they don't invent it.

| Wave | Issues | Gate |
|------|--------|------|
| 1 — Backend core | #359, #360, #517, #361, #366 | none (start any time) |
| 2 — Container + primary AT surfaces | #369, #518, #362, #519, #363 | Wave 1; #518 lands **before** #362/#363 (they phrase through it); #519 before #363 |
| 3 — Navigation + visual surface | #364, #367, #520, #372 | Wave 2 |
| 4 — Authoring | #368, #521, #522, #523, #524 | Wave 3 **+ U3 editor seam stable** (decision 7 pins text-card editing to the post-U3 editing component) |
| 5 — Parity, polish, close-out | #525, #370, #371, #373, #365, #526 | Wave 4; #525 convert-to-note uses U2-2 file creation (landed, #502) |

Specs: [t0 interaction contract](specs/t0_interaction_contract.md) (cross-cutting, normative for every wave) · [t1](specs/t1_spec.md) · [t2](specs/t2_spec.md) · [t3](specs/t3_spec.md) · [t4](specs/t4_spec.md) · [t5](specs/t5_spec.md) · [gap analysis](specs/gap_analysis.md).

---

## Shortcut allocation (normative)

Rules first — they matter more than any single chord:

- **R1.** Every canvas action is a `CommandRegistry` command (new FFI `CommandSection.canvas`; the enum change lands with **#369, first Wave-2 PR** — Wave 2 already registers commands — cross-language, backend-labeled) reachable via palette and menu. A chord is a convenience, never the only path.
- **R2.** Plain arrows / typing keys act **only while a canvas non-text surface has focus** (outline/table/navigator/renderer). VoiceOver Quick Nav intercepts plain arrows — every arrow-driven behavior therefore has a palette/menu equivalent, and the mode announcements name it.
- **R3.** No new chord may collide with the claimed inventory below or with VO (⌃⌥…) / FKA reserved combos. New chords are added to this table in the same PR that registers them; the existing chord↔surface **drift test** extends to the canvas section.
- **R4.** Chord mnemonics must survive dictation ("press Control Command M" is speakable; label text is full words).

**Claimed inventory (today, from `SlateCommands.swift` / `SlateMacApp.swift` + system):** ⇧⌘N (New from Template — note: New *Note* is also here, **not** ⌘N; the app replaces the system `.newItem` group, so **⌘N is currently free**) ⌘O ⌘S ⌘F ⌘J ⇧⌘J ⌘T ⌘W ⇧⌘W ⇧⌘] ⇧⌘[ ⌃⌘← ⌃⌘→ ⌘\ ⌥⌘\ ⌥⌘←→↑↓ ⌥⌘= ⌥⌘- ⇧⌘R ⇧⌘T ⌘, ⌘⇧P ⌘1–9 ⌘Z ⇧⌘Z (+ system edit chords).

*Amendment (2026-07-11, #863):* the inventory line above predates the chord reallocation — ⌘O is now Quick Open (⇧⌘O Open Vault), ⌘T Duplicate Tab, ⇧⌘T Reopen Closed Tab, ⌘R Tasks Review. The line is retained as the historical snapshot the gap analysis was computed against.

*Amendment (2026-07-13, #559, Milestone P P2-3):* the graph Diagram mode joins the focus-routed zoom router (⌘=/⌘−/⌘0 → graph viewport when a graph tab is in Diagram mode, ahead of the editor fallback; one menu owner per chord, #848) and registers the new **⌥⌘0 "Fit Graph"** chord (verified unclaimed against this inventory + VO/FKA reserved combos). All four are `CommandSection.graph` registry commands (palette-mirrored per R2) and are covered by the existing menu↔registry chord drift tests (`SlateCommandsTests`, forward + reverse); the user-facing graph keyboard reference table lands with P-D (#562, `docs/help/graph.md`).

**Canvas allocations (proposed here; final binding in the registering PR, drift-tested):**

| Command | Chord | Scope | Notes |
|---|---|---|---|
| New card | ⌥⌘N | canvas focus | ⌘N is deliberately left free (reserved for a future app-wide New Note binding) |
| Create connected card | ⌃⌥⌘N | canvas focus | direction prompt; #525 |
| Where am I? | ⌃⌘I | canvas focus | #518 |
| Toggle mark | ⌃⌘M | canvas focus | #524 |
| Move mode | ⌃⌘G | canvas focus | "grab"; #521 |
| Resize mode | ⌃⌘R | canvas focus | #521 |
| Connect to… | ⌃⌘C | canvas focus | picker; ⌘C untouched; #523 |
| Zoom in / out / actual | ⌘= / ⌘- / ⌘0 | canvas focus | one modifier from ⌥⌘= grow-pane — the drift test asserts both exist and differ; #520 |
| Fit canvas / zoom to selection | ⇧1 / ⇧2 | canvas surface focus only | Obsidian parity; typing keys, so R2 applies strictly; #520 |
| Filter canvas | ⌘F | canvas focus | shadows vault search *while canvas focused*; Esc restores (see Esc ladder, t0) |
| Next / previous card | ↓ / ↑ | outline/navigator | list semantics; R2 |
| Follow connection fwd / back | → / ← (navigator) | navigator | R2; palette equivalents "Follow Connection Forward/Back" |

---

## Canvas-specific Definition-of-Done deltas

On top of 08's §A–§G, every canvas issue must satisfy:

- **H. Announcement grammar** — all user-audible strings come from the t0 contract's grammar tables (per verbosity level); no ad-hoc `postAccessibilityAnnouncement` in canvas code — everything routes through the #518 coordinator. Bulk = one summary. Rapid = coalesced.
- **I. Mode contract** — any modal interaction (move/resize/connect) implements t0's mode-stack semantics: entry announcement naming the exit, Esc-cancel with restoration announced, Return-commit announced, queryable state (container AX value), auto-cancel on focus departure, visible-control triggers for Switch Control.
- **J. Inspectability (braille rule)** — any state change carried by an announcement is also readable from element state (AX value/label). Announcements are a courtesy copy, never the only copy.
- **K. Scale budget** — the 2,000-node fixture (#365) stays responsive: outline/table virtualized, renderer pan/zoom without stalls, no quadratic model derivation, AX-tree materialization windowed. Benchmarks recorded in `BENCHMARKS.md` at each wave close.
- **L. Never visual-only** — every mutation and every piece of information available on the renderer is available on at least one structured surface (outline/table/navigator/palette). The renderer is parity, not a requirement. (Locked principle 05 §5.2.)

---

## Documented divergences from Obsidian (deliberate)

| Obsidian | Slate | Why |
|---|---|---|
| URL cards render live embedded websites/videos | Static title/host card + "Open in Browser" | Web content inside a canvas is a screen-reader/focus trap; the browser is the accessible web surface. Re-evaluate as an enhancement post-T. |
| Canvas undo = canvas-local toolbar buttons, no ⌘Z | Op-log-backed ⌘Z/⇧⌘Z with named, announced undo steps | #372; strictly better, and consistent with notes. |
| Creation/connection are drag gestures | Every gesture has a first-class named command (create-connected-card, connect picker/mode, move/resize modes) | The command *is* the source of truth; drag is the enhancement (08 DoD §E). |
| No reading order — spatial only | Deterministic reading order + spatial adjacency graph (#360) | The structured equivalents are what make every other surface possible. |

---

## Sequencing gates

- **U1 (workspace shell): satisfied.** `Workspace/` is landed; `EditorItem` holds only `.markdown` with the `"canvas"` Codable discriminator reserved (tolerated-and-dropped on decode). **Activating `EditorItem.canvas` is #369's first deliverable** — every `EditorItem` switch, `WorkspaceStore` round-trip (and inversion of the drop-unknown test), `SlateSymbol` tab glyph, open-tab dedup, per-tab sub-surface persistence. A session saved by a T-era build downgrades gracefully in older builds (tab dropped, not crash) — keep that property tested.
- **U2 (file ops):** #525 convert-card→note **requires** the U2-2 file-creation API (landed, #502). **Link-integrity on move (U2-3, #503) must also rewrite `.canvas` file-node paths** when a referenced note moves/renames — otherwise canvases silently rot. Decision: **extend the rewriter** (the #366 serializer provides the safe write-back path); tracked as a scope note on #366 and a comment on the U2-3 thread. If the rewriter extension can't land with Wave 1, it is a *documented* divergence with a follow-up issue — never a silent one.
- **U3 (editor modes): soft gate on Wave 4.** Text-card editing (decision 7) pins to the post-U3 editing-mode component; `#heading` subpath rendering (#525) reuses U3's reading-view embed rendering. Waves 1–3 may run parallel with U3/U4; **Wave 4 starts after U3's editor seam stabilizes**.
- **Milestone N (Bases):** #519 (AccessibleDataGrid v2) is shared infrastructure — N consumes it, doesn't rebuild it. Noted in both programs.
- **Milestone R (themes):** canvas surfaces use tokens/system dynamic colors only (08 DoD §D); the six JSON-Canvas preset colors map through a palette function with an Increase Contrast collapse (#370), so R can re-skin.
- **Milestone Q (palette):** all canvas commands register in the existing registry; no parallel command surface. New `CommandSection.canvas` FFI case lands with #364.

---

## Cross-cutting process (project norms, unchanged)

One PR per issue; backend/Mac-UI split preserved. Pre-push `cargo fmt --check` + clippy; Swift tests green. Red-team the riskiest surfaces in a worktree before push — here: **#360 reading-order/containment derivation** and **#521 move/resize geometry** (census both hardest), plus **#367 AX-frame invalidation under pan/zoom**. Babysit PRs after push. File tester-feedback issues credited, per repo convention (#365 close-out).
