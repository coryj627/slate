# T3 executable spec — Navigation + visual surface (Wave 3)

Issues: #364 (keyboard navigator + palette commands) · #367 (visual renderer) · #520 (viewport commands) · #372 (op-log undo).
Every issue satisfies 08 DoD §A–§G + 09 deltas §H–§L + the [t0 contract](t0_interaction_contract.md). One PR per issue.

**Execution order: #364 → #372 → #367 → #520.**

---

## #364 — Keyboard navigator + palette commands

As issued (next/prev card in reading order, enter/exit group, follow connection forward/back, jump to connected card N, trace path) plus the contract work:

- **`CommandSection.canvas`** FFI enum case lands here (cross-language: Rust enum + regenerated bindings via `make`; backend-labeled commit). The deferred `.workspace` case may land in the same enum change if trivial.
- All navigator movements are `CommandSection.canvas` commands with t0-conformant announcements via #518 (destination phrasing, N-of-M, direction phrases). Plain-arrow bindings follow program rule R2 (canvas-surface focus only; palette equivalents always; VO Quick Nav caveat documented in each command's help).
- **Mode-stack plumbing** (t0 §2) ships here as shared infrastructure (`CanvasModeController`): entry/exit/announce/queryable-value/auto-cancel-on-focus-departure/Esc-ladder — consumed by #521/#523 in Wave 4. M1–M7 tests land now against a test mode.
- Chords per the program allocation table; the existing chord↔surface **drift test** extends to the canvas section.
- "Trace path from selected card": walks the outgoing chain (cycle-safe), announcing each hop; any dead-end announced ("End of path — 4 cards visited").

**Tests:** each movement on fixture canvases (dead-ends, group boundaries, multi-edge, cycles); drift test; mode-controller M1–M7; announcement strings.

## #372 — Op-log + undo

As issued plus:
- **Responder-chain routing:** ⌘Z inside an inline text-card editor (Wave 4) drives that editor's `NSUndoManager`; ⌘Z on canvas surfaces drives the canvas op-log. The seam is first-responder-based, tested.
- Every committed action = **one** `OpLogEntry` (bulk marked-set ops included — single entry, single undo; #524 relies on this).
- Undo/redo announce the op name (t0 §1.3): "Undid: move 'Research'"; redo symmetric.
- Undo after an external-change conflict behaves safely (op-log entries validate against the current content hash; stale undo → conflict surface per t0 §5, never a blind overwrite).

**Tests:** entry per action; undo restores exact prior canvas (parse→undo→serialize equality); sequences; responder routing; conflict-undo safety.

## #367 — Visual renderer (read-only)

As issued (NSView + CALayer, pan/zoom, all node kinds + labelled edges, `CanvasSelection` sync, Reduce Motion) plus the decision-3 and gap work:

- **Per-card AX elements:** an `NSAccessibilityElement` per visible card and edge — label from t0 §1.1 (same strings as the outline), `accessibilityFrame` in screen coordinates, actions (activate, and Wave-4 actions as they land). **Frame invalidation on every pan/zoom/resize** — a test zooms then asserts queried frames match new geometry (stale frames are the classic failure: VO cursor on empty space, Voice Control numbers floating). Off-viewport elements are not materialized (windowed AX tree; §K).
- **Voice Control label uniqueness:** speakable names disambiguate duplicates ("Untitled 3" — t0 §1.1); no two elements on the surface share a speakable name (test).
- **FKA/first-responder:** `acceptsFirstResponder`; predictable key-loop position (after the surface switcher); visible focus indication for view-focus and card-selection distinctly.
- **Focus visibility (WCAG 2.4.7/2.4.11):** the selection indicator is drawn in a **screen-space overlay layer** with a minimum screen-space thickness (never scaled sub-pixel at low zoom); keyboard selection **always** scrolls the card into view regardless of the follow-selection toggle; indicator contrast vs card fill and canvas background is APCA-measured in both appearances (with #370).
- **Hit-testing/z:** overlapping cards resolve to the topmost by document order (consistent with t1 tiebreak).

**Tests:** selection sync both directions; AX-frame invalidation; label uniqueness; renders all node kinds on the fixture; Reduce Motion; focus-ring metrics; scroll-into-view.

## #520 — Viewport commands

As issued (#520 body): zoom in/out (⌘= / ⌘-), actual size (⌘0), fit (⇧1), zoom to selection (⇧2), follow-selection toggle (default ON, silent auto-pan per t0 §1.5); Reduce Motion = instant transform; zoom level announced on command and inspectable in the renderer's AX value.
**Tests:** transforms per command; follow-selection pans from every surface; silence of auto-pan; Reduce Motion; drift test rows.

---

## Acceptance (wave close)

A keyboard/voice user traverses every card and connection without the renderer; a VoiceOver user can also do it *on* the renderer, whose elements track pan/zoom correctly; a low-vision keyboard user controls the viewport fully; every mutation-to-date is undoable with named announcements. a11y-check 100/100; benchmarks (renderer pan/zoom at 2,000 nodes) recorded.
